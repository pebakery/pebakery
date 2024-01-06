﻿/*
    Copyright (C) 2016-2023 Hajin Jang
    Licensed under GPL 3.0
 
    PEBakery is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.

    Additional permission under GNU GPL version 3 section 7

    If you modify this program, or any covered work, by linking
    or combining it with external libraries, containing parts
    covered by the terms of various license, the licensors of
    this program grant you additional permission to convey the
    resulting work. An external library is a library which is
    not derived from or based on this program. 
*/

using System;
using System.Collections.Generic;
using System.Linq;

namespace PEBakery.Core
{
    public class SyntaxChecker
    {
        #region (docs) Compat Option Detector (Idea only)
        /*
        Detect patterns noting that a project requires compat options enabling.
        Standalone to SyntaxChecker? Expand SyntaxChecker?

        [Compat Option Detection Metric] 
        1. Loop using letter
            Ex) Loop,%ScriptFile%,ForLoop,A,Z
        2. Use of deprecated legacy branch conditions
            - NotExistFile
            - NotExistDir
            - NotExistSection
            - NotExistRegSection
            - NotExistRegKey
            - NotExistVar
        3. Use of RegWriteLegacy
            Ex) RegWrite,#1,0x4,SOFTWARE\PEBakery\IntValue,DWORD,1234
        4. Use of Visible
            CodeCommand's Type is CodeType.Macro, and RawCode starts with "Visible"
            Ex) Visible 
        5. Use of GetParam
            CodeCommand's Type is CodeType.Macro, and RawCode starts with "GetParam"
            Ex) GetParam
        6. Use of PackParam
            CodeCommand's Type is CodeType.Macro, and RawCode starts with "PackParam"
            Ex) Param
        7. Reference of deprecated fixed variables
            - %TempDir%
            - %UserName%
            - %UserProfile%
            - %WindowsDir%
            - %ProgramFilesDir%
            - %ProgramFilesDir_x86%
            - %WindowsVersion%
            - %ProcessorType%
        8. Set is overriding fixed variables
            - %ScriptFile%
            - %ScriptDir%
            - %ScriptTitle%
            - %ProjectTitle%
            - %BaseDir%
            - %Version%
        9. Set is overriding #c
            Ex) Set,#c,Override
        10. Set is modifying interface control
            Is Set command overwriting current interface's control?
        */
        #endregion

        #region (docs) Section Deep Detector 
        #endregion

        #region Field and Property
        private readonly Script _sc;

        // Coverage
        private readonly HashSet<string> _visitedSections = new HashSet<string>();
        public int CodeSectionCount => _sc.Sections.Count(x => x.Value.Type == SectionType.Code || x.Value.Type == SectionType.Interface);
        public int VisitedSectionCount => _visitedSections.Count;
        public double Coverage => CodeSectionCount == 0 ? 0 : (double)VisitedSectionCount / CodeSectionCount;

        // 
        // TODO: Compat option detector

        #endregion

        #region Constructor
        public SyntaxChecker(Script sc)
        {
            _sc = sc ?? throw new ArgumentNullException(nameof(sc));
        }
        #endregion

        #region CheckScript
        public (List<LogInfo>, Result) CheckScript()
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Deep inspect unknown sections to figure out hidden code sections.
            _sc.DeepInspectSections();

            // Codes
            if (_sc.Sections.ContainsKey(ScriptSection.Names.Process))
                logs.AddRange(CheckCodeSection(_sc.Sections[ScriptSection.Names.Process]));

            // UICtrls - [Interface]
            List<string> processedInterfaces = new List<string>();
            if (_sc.Sections.ContainsKey(ScriptSection.Names.Interface))
            {
                processedInterfaces.Add(ScriptSection.Names.Interface);
                logs.AddRange(CheckInterfaceSection(_sc.Sections[ScriptSection.Names.Interface]));
            }
            // UICtrls - Interface=
            if (_sc.MainInfo.ContainsKey(ScriptSection.Names.Interface))
            {
                string ifaceSection = _sc.MainInfo[ScriptSection.Names.Interface];
                processedInterfaces.Add(ifaceSection);
                if (_sc.Sections.ContainsKey(ifaceSection))
                    logs.AddRange(CheckInterfaceSection(_sc.Sections[ifaceSection]));
                else
                    logs.Add(new LogInfo(LogState.Error, $"Section [{ifaceSection}] does not exist (Interface={ifaceSection})"));
            }
            // UICtrls - InterfaceList= (Stage 1)
            if (_sc.MainInfo.ContainsKey(Script.Const.InterfaceList))
            {
                // Check if InterfaceList contains proper sections
                string interfaceList = _sc.MainInfo[Script.Const.InterfaceList];
                try
                {
                    string? remainder = interfaceList;
                    while (remainder != null)
                    {
                        string next;
                        (next, remainder) = CodeParser.GetNextArgument(remainder);

                        // Does this section exist?
                        if (!_sc.Sections.ContainsKey(next))
                            logs.Add(new LogInfo(LogState.Error, $"Section [{next}] does not exist (InterfaceList={interfaceList})"));
                    }
                }
                catch (InvalidCommandException e)
                {
                    logs.Add(new LogInfo(LogState.Error, e));
                }
            }
            // UICtrls - InterfaceList= (Stage 2) 
            // Do not enable deepScan, SyntaxChecker have its own implementation of `IniWrite` pattern scanning
            foreach (string ifaceSection in _sc.GetInterfaceSectionNames(false)
                .Where(x => !processedInterfaces.Contains(x, StringComparer.OrdinalIgnoreCase)))
            {
                if (_sc.Sections.ContainsKey(ifaceSection))
                    logs.AddRange(CheckInterfaceSection(_sc.Sections[ifaceSection]));
            }

            // Check more deep-inspected code sections
            foreach (ScriptSection section in _sc.Sections.Values.Where(x => x.Type == SectionType.Code && !_visitedSections.Contains(x.Name)))
                logs.AddRange(CheckCodeSection(section));

            // Check more deep-inspected interface sections
            foreach (ScriptSection section in _sc.Sections.Values.Where(x => x.Type == SectionType.Interface && !_visitedSections.Contains(x.Name)))
                logs.AddRange(CheckInterfaceSection(section));

            // Result
            Result result = Result.Clean;
            if (logs.Any(x => x.State == LogState.Error || x.State == LogState.CriticalError))
                result = Result.Error;
            else if (logs.Any(x => x.State == LogState.Warning))
                result = Result.Warning;

            return (logs, result);
        }
        #endregion

        #region CheckCodeSection
        private List<LogInfo> CheckCodeSection(ScriptSection section, string? rawLine = null, int lineIdx = 0)
        {
            // If this section was already visited, return.
            if (_visitedSections.Contains(section.Name))
                return new List<LogInfo>();
            _visitedSections.Add(section.Name);

            if (section.Lines == null)
            {
                string msg = $"Unable to load section [{section.Name}]";
                if (rawLine != null)
                    msg += $" ({rawLine})";
                if (0 < lineIdx)
                    msg += $" (Line {lineIdx})";

                return new List<LogInfo> { new LogInfo(LogState.Error, msg) };
            }

            CodeParser parser = new CodeParser(section, Global.Setting, section.Project.Compat);
            (CodeCommand[] cmds, List<LogInfo> logs) = parser.ParseStatements();
            RecursiveFindCodeSection(cmds, logs);

            return logs;
        }

        private void RecursiveFindCodeSection(IReadOnlyList<CodeCommand> codes, List<LogInfo> logs)
        {
            foreach (CodeCommand cmd in codes)
            {
                string? targetCodeSection = null;
                string? targetInterfaceSection = null;
                switch (cmd.Type)
                {
                    #region Check CodeSections
                    case CodeType.If:
                        {
                            CodeInfo_If info = (CodeInfo_If)cmd.Info;

                            if (info.Condition.Type == BranchConditionType.ExistSection)
                            {
                                // Break is false -> Other properties must not be null
                                if (info.Condition.Arg1 is not string arg1)
                                    throw new InternalException($"{nameof(info.Condition.Arg1)} is null");
                                if (info.Condition.Arg2 is not string arg2)
                                    throw new InternalException($"{nameof(info.Condition.Arg2)} is null");

                                // For recursive section call
                                // Ex) If,ExistSection,%ScriptFile%,DoWork,Run,%ScriptFile%,DoWork
                                if (arg1.Equals(Script.Const.ScriptFile, StringComparison.OrdinalIgnoreCase) &&
                                    info.Embed.Type == CodeType.Run || info.Embed.Type == CodeType.RunEx || info.Embed.Type == CodeType.Exec)
                                {
                                    CodeInfo_RunExec subInfo = (CodeInfo_RunExec)info.Embed.Info;
                                    if (subInfo.ScriptFile.Equals(Script.Const.ScriptFile, StringComparison.OrdinalIgnoreCase))
                                    {
                                        if (arg2.Equals(subInfo.SectionName, StringComparison.OrdinalIgnoreCase))
                                            continue;
                                    }
                                }
                            }

                            RecursiveFindCodeSection(info.Link, logs);
                        }
                        break;
                    case CodeType.Else:
                        {
                            CodeInfo_Else info = (CodeInfo_Else)cmd.Info;

                            RecursiveFindCodeSection(info.Link, logs);
                        }
                        break;
                    case CodeType.Run:
                    case CodeType.Exec:
                    case CodeType.RunEx:
                        {
                            CodeInfo_RunExec info = (CodeInfo_RunExec)cmd.Info;

                            // SyntaxChecker does not have Variable information, so just check with predefined literal
                            if (info.ScriptFile.Equals(Script.Const.ScriptFile, StringComparison.OrdinalIgnoreCase) &&
                                !CodeParser.StringContainsVariable(info.SectionName))
                                targetCodeSection = info.SectionName;
                        }
                        break;
                    case CodeType.Loop:
                    case CodeType.LoopLetter:
                    case CodeType.LoopEx:
                    case CodeType.LoopLetterEx:
                        {
                            CodeInfo_Loop info = (CodeInfo_Loop)cmd.Info;

                            // info.Break -> CodeInfo_Loop is empty
                            if (info.Break)
                                continue;

                            // Break is false -> Other properties must not be null
                            if (info.ScriptFile is not string scriptFile)
                                throw new InternalException($"{nameof(info.ScriptFile)} is null");
                            if (info.SectionName is not string sectionName)
                                throw new InternalException($"{nameof(info.SectionName)} is null");

                            // SyntaxChecker does not have Variable information, so just check with predefined literal
                            if (info.ScriptFile.Equals(Script.Const.ScriptFile, StringComparison.OrdinalIgnoreCase) &&
                                !CodeParser.StringContainsVariable(info.SectionName))
                                targetCodeSection = info.SectionName;
                        }
                        break;
                    case CodeType.UserInput:
                        {
                            CodeInfo_UserInput info = (CodeInfo_UserInput)cmd.Info;

                            UserInputType type = info.Type;
                            switch (type)
                            {
                                case UserInputType.DirPath:
                                case UserInputType.FilePath:
                                    {

                                        UserInputInfo_DirFile subInfo = (UserInputInfo_DirFile)info.SubInfo;

                                        if (info.Type == UserInputType.FilePath)
                                        { // Select File
                                            if (subInfo.Filter != null)
                                            {
                                                string filter = StringEscaper.Unescape(subInfo.Filter);
                                                if (StringEscaper.IsFileFilterValid(filter) == false)
                                                    logs.Add(new LogInfo(LogState.Error, $"File filter pattern [{filter}] is invalid", cmd));
                                            }
                                        }
                                        else
                                        { // Select Folder
                                            if (subInfo.Filter != null)
                                                logs.Add(new LogInfo(LogState.Warning, $"File filters cannot be used for folder selection", cmd));
                                        }
                                    }
                                    break;
                            }
                        }
                        break;
                    #endregion
                    #region Check InterfaceSections
                    case CodeType.AddInterface:
                        {
                            CodeInfo_AddInterface info = (CodeInfo_AddInterface)cmd.Info;

                            // CodeValidator does not have Variable information, so just check with predefined literal
                            if (info.ScriptFile.Equals(Script.Const.ScriptFile, StringComparison.OrdinalIgnoreCase) &&
                                !CodeParser.StringContainsVariable(info.Section))
                                targetInterfaceSection = info.Section;
                        }
                        break;
                    case CodeType.ReadInterface:
                        {
                            CodeInfo_ReadInterface info = (CodeInfo_ReadInterface)cmd.Info;

                            // CodeValidator does not have Variable information, so just check with predefined literal
                            if (info.ScriptFile.Equals(Script.Const.ScriptFile, StringComparison.OrdinalIgnoreCase) &&
                                !CodeParser.StringContainsVariable(info.Section))
                                targetInterfaceSection = info.Section;
                        }
                        break;
                    case CodeType.WriteInterface:
                        {
                            CodeInfo_WriteInterface info = (CodeInfo_WriteInterface)cmd.Info;

                            // CodeValidator does not have Variable information, so just check with predefined literal
                            if (info.ScriptFile.Equals(Script.Const.ScriptFile, StringComparison.OrdinalIgnoreCase) &&
                                !CodeParser.StringContainsVariable(info.Section))
                                targetInterfaceSection = info.Section;
                        }
                        break;
                    case CodeType.IniWrite:
                        {
                            // To detect multi-interface without `InterfaceList=`,
                            // Inspect pattern `IniWrite,%ScriptFile%,Main,Interface,<NewInterfaceSection>`
                            CodeInfo_IniWrite info = (CodeInfo_IniWrite)cmd.Info;

                            // CodeValidator does not have Variable information, so just check with predefined literal
                            if (info.FileName.Equals(Script.Const.ScriptFile, StringComparison.OrdinalIgnoreCase) &&
                                info.Section.Equals(ScriptSection.Names.Main, StringComparison.OrdinalIgnoreCase) &&
                                info.Key.Equals(ScriptSection.Names.Interface, StringComparison.OrdinalIgnoreCase) &&
                                !CodeParser.StringContainsVariable(info.Value))
                                targetInterfaceSection = info.Value;
                        }
                        break;
                        #endregion
                }

                if (targetCodeSection != null)
                {
                    if (_sc.Sections.ContainsKey(targetCodeSection))
                        logs.AddRange(CheckCodeSection(_sc.Sections[targetCodeSection], cmd.RawCode, cmd.LineIdx));
                    else
                        logs.Add(new LogInfo(LogState.Error, $"Section [{targetCodeSection}] does not exist", cmd));
                }

                if (targetInterfaceSection != null)
                {
                    if (_sc.Sections.ContainsKey(targetInterfaceSection))
                        logs.AddRange(CheckInterfaceSection(_sc.Sections[targetInterfaceSection], cmd.RawCode, cmd.LineIdx));
                    else
                        logs.Add(new LogInfo(LogState.Error, $"Interface section [{targetInterfaceSection}] does not exist", cmd));
                }
            }
        }
        #endregion

        #region CheckInterfaceSection
        private List<LogInfo> CheckInterfaceSection(ScriptSection section, string? rawLine = null, int lineIdx = 0)
        {
            // If this section was already visited, return.
            if (_visitedSections.Contains(section.Name))
                return new List<LogInfo>();
            _visitedSections.Add(section.Name);

            // Force parsing of code, bypassing caching by section.GetUICtrls()
            string[] lines = section.Lines;
            if (lines == null)
            {
                string msg = $"Section [{section.Name}] is not a valid interface section";
                if (rawLine != null)
                    msg += $" ({rawLine})";
                if (0 < lineIdx)
                    msg += $" (Line {lineIdx})";

                return new List<LogInfo> { new LogInfo(LogState.Error, msg) };
            }

            (List<UIControl> uiCtrls, List<LogInfo> logs) = UIParser.ParseStatements(lines, section);
            foreach (UIControl uiCtrl in uiCtrls)
            {
                switch (uiCtrl.Type)
                {
                    case UIControlType.CheckBox:
                        {
                            UIInfo_CheckBox info = (UIInfo_CheckBox)uiCtrl.Info;

                            if (info.SectionName != null)
                            {
                                if (_sc.Sections.ContainsKey(info.SectionName)) // Only if section exists
                                    logs.AddRange(CheckCodeSection(_sc.Sections[info.SectionName], uiCtrl.RawLine, uiCtrl.LineIdx));
                                else
                                    logs.Add(new LogInfo(LogState.Error, $"Section [{info.SectionName}] does not exist", uiCtrl));
                            }
                        }
                        break;
                    case UIControlType.ComboBox:
                        {
                            UIInfo_ComboBox info = (UIInfo_ComboBox)uiCtrl.Info;

                            // Practically, this means info.Index is -1 -> uiCtrl.Text not being one of info.Items
                            if (info.Index < 0 || info.Items.Count <= info.Index)
                                logs.Add(new LogInfo(LogState.Warning, $"Incorrect selected value [{uiCtrl.Text}]", uiCtrl));

                            if (info.SectionName != null)
                            {
                                if (_sc.Sections.ContainsKey(info.SectionName)) // Only if section exists
                                    logs.AddRange(CheckCodeSection(_sc.Sections[info.SectionName], uiCtrl.RawLine, uiCtrl.LineIdx));
                                else
                                    logs.Add(new LogInfo(LogState.Error, $"Section [{info.SectionName}] does not exist", uiCtrl));
                            }
                        }
                        break;
                    case UIControlType.Image:
                        {
                            // Check encoded image
                            string imageSection = StringEscaper.Unescape(uiCtrl.Text);
                            if (!imageSection.Equals(UIInfo_Image.NoResource, StringComparison.OrdinalIgnoreCase) &&
                                !EncodedFile.ContainsInterface(_sc, imageSection))
                                logs.Add(new LogInfo(LogState.Warning, $"Image resource [{imageSection}] does not exist", uiCtrl));

                            UIInfo_Image info = (UIInfo_Image)uiCtrl.Info;

                            // Check if image control have empty or invalid url.
                            // Ex) Colors_Image=ThemeColors.jpg,1,5,11,228,260,80,
                            if (info.Url != null)
                            {
                                string url = StringEscaper.Unescape(info.Url);
                                if (!StringEscaper.IsUrlValid(url))
                                {
                                    if (url.IndexOf("://", StringComparison.Ordinal) != -1)
                                        logs.Add(new LogInfo(LogState.Warning, $"Incorrect URL [{url}]", uiCtrl));
                                    else
                                        logs.Add(new LogInfo(LogState.Warning, "URL does not have a scheme. Did you omit \"http(s)://\"?", uiCtrl));
                                }
                            }
                        }
                        break;
                    case UIControlType.TextFile:
                        {
                            string textSection = StringEscaper.Unescape(uiCtrl.Text);
                            if (!textSection.Equals(UIInfo_TextFile.NoResource, StringComparison.OrdinalIgnoreCase) &&
                                !EncodedFile.ContainsInterface(_sc, textSection))
                                logs.Add(new LogInfo(LogState.Warning, $"Text resource [{textSection}] does not exist", uiCtrl));
                        }
                        break;
                    case UIControlType.Button:
                        {
                            UIInfo_Button info = (UIInfo_Button)uiCtrl.Info;

                            string? pictureSection = info.Picture;
                            if (pictureSection != null &&
                                !pictureSection.Equals(UIInfo_Button.NoPicture, StringComparison.OrdinalIgnoreCase) &&
                                !EncodedFile.ContainsInterface(_sc, pictureSection))
                            {
                                if (pictureSection.Length == 0) // Due to quirks of WinBuilder's editor, many buttons have '' instead of '0' in the place of <Picture>.
                                    logs.Add(new LogInfo(LogState.Warning, "Image resource entry is empty. Use [0] to represent not having an image resource.", uiCtrl));
                                else
                                    logs.Add(new LogInfo(LogState.Warning, $"Image resource [{pictureSection}] does not exist", uiCtrl));
                            }

                            if (info.SectionName != null)
                            {
                                if (_sc.Sections.ContainsKey(info.SectionName)) // Only if section exists
                                    logs.AddRange(CheckCodeSection(_sc.Sections[info.SectionName], uiCtrl.RawLine, uiCtrl.LineIdx));
                                else
                                    logs.Add(new LogInfo(LogState.Error, $"Section [{info.SectionName}] does not exist", uiCtrl));
                            }
                        }
                        break;
                    case UIControlType.WebLabel:
                        {
                            UIInfo_WebLabel info = (UIInfo_WebLabel)uiCtrl.Info;

                            // Sometime developers forget to put proper scheme in WebLabel's url.
                            // Ex) PStart_WebLabel="PStart Homepage",1,10,668,122,98,18,www.pegtop.de/start/
                            string url = StringEscaper.Unescape(info.Url);
                            if (!StringEscaper.IsUrlValid(url))
                            {
                                if (url.IndexOf("://", StringComparison.Ordinal) != -1)
                                    logs.Add(new LogInfo(LogState.Warning, $"Incorrect URL [{url}]", uiCtrl));
                                else
                                    logs.Add(new LogInfo(LogState.Warning, "URL does not have a scheme. Did you omit \"http(s)://\"?", uiCtrl));
                            }
                        }
                        break;
                    case UIControlType.RadioButton:
                        {
                            UIInfo_RadioButton info = (UIInfo_RadioButton)uiCtrl.Info;

                            if (info.SectionName != null)
                            {
                                if (_sc.Sections.ContainsKey(info.SectionName)) // Only if section exists
                                    logs.AddRange(CheckCodeSection(_sc.Sections[info.SectionName], uiCtrl.RawLine, uiCtrl.LineIdx));
                                else
                                    logs.Add(new LogInfo(LogState.Error, $"Section [{info.SectionName}] does not exist", uiCtrl));
                            }
                        }
                        break;
                    case UIControlType.FileBox:
                        {
                            UIInfo_FileBox info = (UIInfo_FileBox)uiCtrl.Info;

                            if (info.IsFile)
                            { // Select File
                                if (info.Filter != null)
                                {
                                    string filter = StringEscaper.Unescape(info.Filter);
                                    if (StringEscaper.IsFileFilterValid(filter) == false)
                                        logs.Add(new LogInfo(LogState.Warning, $"File filter pattern [{filter}] is invalid", uiCtrl));
                                }
                            }
                            else
                            { // Select Folder
                                if (info.Filter != null)
                                    logs.Add(new LogInfo(LogState.Warning, $"File filters cannot be used for folder selection", uiCtrl));
                            }
                        }
                        break;
                    case UIControlType.RadioGroup:
                        {
                            UIInfo_RadioGroup info = (UIInfo_RadioGroup)uiCtrl.Info;

                            if (info.SectionName != null)
                            {
                                if (_sc.Sections.ContainsKey(info.SectionName)) // Only if section exists
                                    logs.AddRange(CheckCodeSection(_sc.Sections[info.SectionName], uiCtrl.RawLine, uiCtrl.LineIdx));
                                else
                                    logs.Add(new LogInfo(LogState.Error, $"Section [{info.SectionName}] does not exist", uiCtrl));
                            }

                            // Practically, this means info.Index is -1 -> uiCtrl.Text not being one of info.Items
                            if (info.Selected < 0 || info.Items.Count <= info.Selected)
                                logs.Add(new LogInfo(LogState.Warning, $"Incorrect selected index [{info.Selected}]", uiCtrl));
                        }
                        break;
                    case UIControlType.PathBox:
                        {
                            UIInfo_PathBox info = (UIInfo_PathBox)uiCtrl.Info;

                            if (info.IsFile)
                            { // Select File
                                if (info.Filter != null)
                                {
                                    string filter = StringEscaper.Unescape(info.Filter);
                                    if (StringEscaper.IsFileFilterValid(filter) == false)
                                        logs.Add(new LogInfo(LogState.Warning, $"File filter pattern [{filter}] is invalid", uiCtrl));
                                }
                            }
                            else
                            { // Select Folder
                                if (info.Filter != null)
                                    logs.Add(new LogInfo(LogState.Warning, $"File filters cannot be used for folder selection", uiCtrl));
                            }

                            if (info.SectionName != null)
                            {
                                if (_sc.Sections.ContainsKey(info.SectionName)) // Only if section exists
                                    logs.AddRange(CheckCodeSection(_sc.Sections[info.SectionName], uiCtrl.RawLine, uiCtrl.LineIdx));
                                else
                                    logs.Add(new LogInfo(LogState.Error, $"Section [{info.SectionName}] does not exist", uiCtrl));
                            }
                        }
                        break;
                }
            }
            return logs;
        }
        #endregion

        #region enum Result
        public enum Result
        {
            Unknown,
            Clean,
            Warning,
            Error
        }
        #endregion
    }
}
