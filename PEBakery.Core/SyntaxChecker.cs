/*
    Copyright (C) 2016-2018 Hajin Jang
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
using System.Diagnostics;
using System.Linq;

namespace PEBakery.Core
{
    public class SyntaxChecker
    {
        #region Field and Property
        private readonly Script _sc;
        private readonly List<string> _visitedSections = new List<string>();

        public int CodeSectionCount => _sc.Sections.Count(x => x.Value.Type == SectionType.Code);
        public int VisitedSectionCount => _visitedSections.Count;
        public double Coverage => CodeSectionCount == 0 ? 0 : (double)VisitedSectionCount / CodeSectionCount;
        #endregion

        #region Constructor
        public SyntaxChecker(Script sc)
        {
            _sc = sc ?? throw new ArgumentNullException(nameof(sc));
        }
        #endregion

        #region Validate
        public (List<LogInfo>, Result) Validate()
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Codes
            if (_sc.Sections.ContainsKey(ScriptSection.Names.Process))
                logs.AddRange(ValidateCodeSection(_sc.Sections[ScriptSection.Names.Process]));

            // UICtrls - [Interface]
            List<string> processedInterfaces = new List<string>();
            if (_sc.Sections.ContainsKey(ScriptSection.Names.Interface))
            {
                processedInterfaces.Add(ScriptSection.Names.Interface);
                logs.AddRange(ValidateInterfaceSection(_sc.Sections[ScriptSection.Names.Interface]));
            }
            // UICtrls - Interface=
            if (_sc.MainInfo.ContainsKey(ScriptSection.Names.Interface))
            {
                string ifaceSection = _sc.MainInfo[ScriptSection.Names.Interface];
                if (_sc.Sections.ContainsKey(ifaceSection))
                {
                    processedInterfaces.Add(ifaceSection);
                    logs.AddRange(ValidateInterfaceSection(_sc.Sections[ifaceSection]));
                }
                    
            }
            // UICtrls - InterfaceList=
            foreach (string ifaceSection in _sc.InterfaceSectionNames
                .Where(x => !processedInterfaces.Contains(x, StringComparer.OrdinalIgnoreCase) &&
                            _sc.Sections.ContainsKey(x)))
            {
                logs.AddRange(ValidateInterfaceSection(_sc.Sections[ifaceSection]));
            }

            // Result
            Result result = Result.Clean;
            if (logs.Any(x => x.State == LogState.Error || x.State == LogState.CriticalError))
                result = Result.Error;
            else if (logs.Any(x => x.State == LogState.Warning))
                result = Result.Warning;

            return (logs, result);
        }
        #endregion

        #region ValidateCodeSection
        private List<LogInfo> ValidateCodeSection(ScriptSection section, string rawLine = null, int lineIdx = 0)
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
            ValidateCodeCommands(cmds, logs);

            return logs;
        }

        private void ValidateCodeCommands(CodeCommand[] codes, List<LogInfo> logs)
        {
            string targetCodeSection = null;
            string targetInterfaceSection = null;
            foreach (CodeCommand cmd in codes)
            {
                switch (cmd.Type)
                {
                    #region Check CodeSections
                    case CodeType.If:
                        {
                            CodeInfo_If info = cmd.Info.Cast<CodeInfo_If>();

                            if (info.Condition.Type == BranchConditionType.ExistSection)
                            {
                                // For recursive section call
                                // Ex) If,ExistSection,%ScriptFile%,DoWork,Run,%ScriptFile%,DoWork
                                if (info.Condition.Arg1.Equals("%ScriptFile%", StringComparison.OrdinalIgnoreCase) &&
                                    info.Embed.Type == CodeType.Run || info.Embed.Type == CodeType.Exec)
                                {
                                    CodeInfo_RunExec subInfo = info.Embed.Info.Cast<CodeInfo_RunExec>();
                                    if (subInfo.ScriptFile.Equals("%ScriptFile%", StringComparison.OrdinalIgnoreCase))
                                    {
                                        if (info.Condition.Arg2.Equals(subInfo.SectionName, StringComparison.OrdinalIgnoreCase))
                                            continue;
                                    }
                                }
                            }

                            ValidateCodeCommands(info.Link.ToArray(), logs);
                        }
                        break;
                    case CodeType.Else:
                        {
                            CodeInfo_Else info = cmd.Info.Cast<CodeInfo_Else>();

                            ValidateCodeCommands(info.Link.ToArray(), logs);
                        }
                        break;
                    case CodeType.Run:
                    case CodeType.Exec:
                    case CodeType.RunEx:
                        {
                            CodeInfo_RunExec info = cmd.Info.Cast<CodeInfo_RunExec>();

                            // CodeValidator does not have Variable information, so just check with predefined literal
                            if (info.ScriptFile.Equals("%ScriptFile%", StringComparison.OrdinalIgnoreCase) &&
                                !CodeParser.StringContainsVariable(info.SectionName))
                                targetCodeSection = info.SectionName;
                        }
                        break;
                    case CodeType.Loop:
                    case CodeType.LoopLetter:
                    case CodeType.LoopEx:
                    case CodeType.LoopLetterEx:
                        {
                            CodeInfo_Loop info = cmd.Info.Cast<CodeInfo_Loop>();

                            if (info.Break)
                                continue;

                            // CodeValidator does not have Variable information, so just check with predefined literal
                            if (info.ScriptFile.Equals("%ScriptFile%", StringComparison.OrdinalIgnoreCase) &&
                                !CodeParser.StringContainsVariable(info.SectionName))
                                targetCodeSection = info.SectionName;
                        }
                        break;
                    #endregion
                    #region Check InterfaceSections
                    case CodeType.AddInterface:
                        {
                            CodeInfo_AddInterface info = cmd.Info.Cast<CodeInfo_AddInterface>();

                            // CodeValidator does not have Variable information, so just check with predefined literal
                            if (info.ScriptFile.Equals("%ScriptFile%", StringComparison.OrdinalIgnoreCase) &&
                                CodeParser.StringContainsVariable(info.Section))
                                targetInterfaceSection = info.Section;
                        }
                        break;
                    case CodeType.ReadInterface:
                        {
                            CodeInfo_ReadInterface info = cmd.Info.Cast<CodeInfo_ReadInterface>();

                            // CodeValidator does not have Variable information, so just check with predefined literal
                            if (info.ScriptFile.Equals("%ScriptFile%", StringComparison.OrdinalIgnoreCase) &&
                                CodeParser.StringContainsVariable(info.Section))
                                targetInterfaceSection = info.Section;
                        }
                        break;
                    case CodeType.WriteInterface:
                        {
                            CodeInfo_WriteInterface info = cmd.Info.Cast<CodeInfo_WriteInterface>();

                            // CodeValidator does not have Variable information, so just check with predefined literal
                            if (info.ScriptFile.Equals("%ScriptFile%", StringComparison.OrdinalIgnoreCase) &&
                                CodeParser.StringContainsVariable(info.Section))
                                targetInterfaceSection = info.Section;
                        }
                        break;
                        #endregion
                }

                if (targetCodeSection != null)
                {
                    if (_sc.Sections.ContainsKey(targetCodeSection))
                        logs.AddRange(ValidateCodeSection(_sc.Sections[targetCodeSection], cmd.RawCode, cmd.LineIdx));
                    else
                        logs.Add(new LogInfo(LogState.Error, $"Section [{targetCodeSection}] does not exist", cmd));
                }

                if (targetInterfaceSection != null)
                {
                    if (_sc.Sections.ContainsKey(targetInterfaceSection))
                        logs.AddRange(ValidateInterfaceSection(_sc.Sections[targetInterfaceSection], cmd.RawCode, cmd.LineIdx));
                    else
                        logs.Add(new LogInfo(LogState.Error, $"Section [{targetInterfaceSection}] does not exist", cmd));
                }
            }
        }
        #endregion

        #region ValidateInterfaceSection
        private List<LogInfo> ValidateInterfaceSection(ScriptSection section, string rawLine = null, int lineIdx = 0)
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
                            UIInfo_CheckBox info = uiCtrl.Info.Cast<UIInfo_CheckBox>();

                            if (info.SectionName != null)
                            {
                                if (_sc.Sections.ContainsKey(info.SectionName)) // Only if section exists
                                    logs.AddRange(ValidateCodeSection(_sc.Sections[info.SectionName], uiCtrl.RawLine, uiCtrl.LineIdx));
                                else
                                    logs.Add(new LogInfo(LogState.Error, $"Section [{info.SectionName}] does not exist", uiCtrl));
                            }
                        }
                        break;
                    case UIControlType.Image:
                        if (!uiCtrl.Text.Equals(UIInfo_Image.NoResource, StringComparison.OrdinalIgnoreCase) &&
                            !EncodedFile.ContainsInterface(_sc, uiCtrl.Text))
                            logs.Add(new LogInfo(LogState.Warning, $"Image resource [{uiCtrl.Text}] does not exist", uiCtrl));
                        break;
                    case UIControlType.TextFile:
                        if (!uiCtrl.Text.Equals(UIInfo_TextFile.NoResource, StringComparison.OrdinalIgnoreCase) &&
                            !EncodedFile.ContainsInterface(_sc, uiCtrl.Text))
                            logs.Add(new LogInfo(LogState.Warning, $"Text resource [{uiCtrl.Text}] does not exist", uiCtrl));
                        break;
                    case UIControlType.Button:
                        {
                            UIInfo_Button info = uiCtrl.Info.Cast<UIInfo_Button>();

                            if (info.Picture != null &&
                                !info.Picture.Equals(UIInfo_Button.NoPicture, StringComparison.OrdinalIgnoreCase) &&
                                !EncodedFile.ContainsInterface(_sc, info.Picture))
                            {
                                if (info.Picture.Length == 0) // Due to WinBuilder's editor quirks, many buttons have '' instead of '0' in the place of <Picture>.
                                    logs.Add(new LogInfo(LogState.Warning, "Image resource entry is empty. Use [0] to represent not having an image resource.", uiCtrl)); 
                                else
                                    logs.Add(new LogInfo(LogState.Warning, $"Image resource [{info.Picture}] does not exist", uiCtrl));
                            }

                            if (info.SectionName != null)
                            {
                                if (_sc.Sections.ContainsKey(info.SectionName)) // Only if section exists
                                    logs.AddRange(ValidateCodeSection(_sc.Sections[info.SectionName], uiCtrl.RawLine, uiCtrl.LineIdx));
                                else
                                    logs.Add(new LogInfo(LogState.Error, $"Section [{info.SectionName}] does not exist", uiCtrl));
                            }
                        }
                        break;
                    case UIControlType.RadioButton:
                        {
                            UIInfo_RadioButton info = uiCtrl.Info.Cast<UIInfo_RadioButton>();

                            if (info.SectionName != null)
                            {
                                if (_sc.Sections.ContainsKey(info.SectionName)) // Only if section exists
                                    logs.AddRange(ValidateCodeSection(_sc.Sections[info.SectionName], uiCtrl.RawLine, uiCtrl.LineIdx));
                                else
                                    logs.Add(new LogInfo(LogState.Error, $"Section [{info.SectionName}] does not exist", uiCtrl));
                            }
                        }
                        break;
                    case UIControlType.RadioGroup:
                        {
                            UIInfo_RadioGroup info = uiCtrl.Info.Cast<UIInfo_RadioGroup>();

                            if (info.SectionName != null)
                            {
                                if (_sc.Sections.ContainsKey(info.SectionName)) // Only if section exists
                                    logs.AddRange(ValidateCodeSection(_sc.Sections[info.SectionName], uiCtrl.RawLine, uiCtrl.LineIdx));
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
