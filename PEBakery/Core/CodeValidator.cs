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
using System.Linq;

namespace PEBakery.Core
{
    public class CodeValidator
    {
        #region Field and Property
        private readonly Script _sc;
        private readonly List<ScriptSection> _visitedSections = new List<ScriptSection>();

        public int CodeSectionCount => _sc.Sections.Count(x => x.Value.Type == SectionType.Code);
        public int VisitedSectionCount => _visitedSections.Count;
        public double Coverage
        {
            get
            {
                if (CodeSectionCount == 0)
                    return 0;
                return (double)VisitedSectionCount / CodeSectionCount;
            }
        }

        private readonly List<LogInfo> _logInfos = new List<LogInfo>();
        public LogInfo[] LogInfos
        {
            get
            { // Call .ToArray to get logInfo's copy 
                LogInfo[] list = _logInfos.ToArray();
                _logInfos.Clear();
                return list;
            }
        }
        #endregion

        #region Constructor
        public CodeValidator(Script sc)
        {
            _sc = sc ?? throw new ArgumentNullException(nameof(sc));
        }
        #endregion

        #region Validate
        public void Validate()
        {
            // Codes
            if (_sc.Sections.ContainsKey("Process"))
                _logInfos.AddRange(ValidateCodeSection(_sc.Sections["Process"]));

            // UICtrls
            if (_sc.Sections.ContainsKey("Interface"))
                _logInfos.AddRange(ValidateInterfaceSection(_sc.Sections["Interface"]));

            if (_sc.MainInfo.ContainsKey("Interface"))
            {
                string ifaceSection = _sc.MainInfo["Interface"];
                if (_sc.Sections.ContainsKey(ifaceSection))
                    _logInfos.AddRange(ValidateInterfaceSection(_sc.Sections[ifaceSection]));
            }
        }

        #region ValidateCodeSection
        private List<LogInfo> ValidateCodeSection(ScriptSection section, string rawLine = null, int lineIdx = 0)
        {
            // Already processed, so skip
            if (_visitedSections.Contains(section))
                return new List<LogInfo>();

            // Force parsing of code, bypassing caching by section.GetCodes()
            List<string> lines;
            try
            {
                lines = section.GetLines();
            }
            catch (InternalException)
            {
                string msg = $"Section [{section.Name}] is not a valid code section";
                if (rawLine != null)
                    msg += $" ({rawLine})";
                if (0 < lineIdx)
                    msg += $" (Line {lineIdx})";

                return new List<LogInfo> { new LogInfo(LogState.Error, msg) };
            }

            List<CodeCommand> codes = CodeParser.ParseStatements(lines, section, out List<LogInfo> logs);

            _visitedSections.Add(section);
            InternalValidateCodes(codes, logs);

            return logs;
        }

        private void InternalValidateCodes(List<CodeCommand> codes, List<LogInfo> logs)
        {
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
                                if (info.Condition.Arg1.Equals("%ScriptFile%", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (info.Embed.Type == CodeType.Run || info.Embed.Type == CodeType.Exec)
                                    {
                                        CodeInfo_RunExec subInfo = info.Embed.Info.Cast<CodeInfo_RunExec>();

                                        if (subInfo.ScriptFile.Equals("%ScriptFile%", StringComparison.OrdinalIgnoreCase))
                                        {
                                            if (info.Condition.Arg2.Equals(subInfo.SectionName, StringComparison.OrdinalIgnoreCase))
                                                continue;
                                        }
                                    }
                                }
                            }

                            InternalValidateCodes(info.Link, logs);
                        }
                        break;
                    case CodeType.Else:
                        {
                            CodeInfo_Else info = cmd.Info.Cast<CodeInfo_Else>();

                            InternalValidateCodes(info.Link, logs);
                        }
                        break;
                    case CodeType.Run:
                    case CodeType.Exec:
                    case CodeType.RunEx:
                        {
                            CodeInfo_RunExec info = cmd.Info.Cast<CodeInfo_RunExec>();

                            // CodeValidator does not have Variable information, so just check with predefined literal
                            if (info.ScriptFile.Equals("%ScriptFile%", StringComparison.OrdinalIgnoreCase) &&
                                CodeParser.StringContainsVariable(info.SectionName))
                            {
                                if (_sc.Sections.ContainsKey(info.SectionName))
                                    logs.AddRange(ValidateCodeSection(_sc.Sections[info.SectionName], cmd.RawCode, cmd.LineIdx));
                                else
                                    logs.Add(new LogInfo(LogState.Error, $"Section [{info.SectionName}] does not exist", cmd));
                            }
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
                                CodeParser.StringContainsVariable(info.SectionName))
                            {
                                if (_sc.Sections.ContainsKey(info.SectionName))
                                    logs.AddRange(ValidateCodeSection(_sc.Sections[info.SectionName], cmd.RawCode, cmd.LineIdx));
                                else
                                    logs.Add(new LogInfo(LogState.Error, $"Section [{info.SectionName}] does not exist", cmd));
                            }
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
                            {
                                if (_sc.Sections.ContainsKey(info.Section))
                                    logs.AddRange(ValidateInterfaceSection(_sc.Sections[info.Section], cmd.RawCode, cmd.LineIdx));
                                else
                                    logs.Add(new LogInfo(LogState.Error, $"Section [{info.Section}] does not exist", cmd));
                            }
                        }
                        break;
                    case CodeType.ReadInterface:
                        {
                            CodeInfo_ReadInterface info = cmd.Info.Cast<CodeInfo_ReadInterface>();

                            // CodeValidator does not have Variable information, so just check with predefined literal
                            if (info.ScriptFile.Equals("%ScriptFile%", StringComparison.OrdinalIgnoreCase) &&
                                CodeParser.StringContainsVariable(info.Section))
                            {
                                if (_sc.Sections.ContainsKey(info.Section))
                                    logs.AddRange(ValidateInterfaceSection(_sc.Sections[info.Section], cmd.RawCode, cmd.LineIdx));
                                else 
                                    logs.Add(new LogInfo(LogState.Error, $"Section [{info.Section}] does not exist", cmd));
                            }
                        }
                        break;
                    case CodeType.WriteInterface:
                        {
                            CodeInfo_WriteInterface info = cmd.Info.Cast<CodeInfo_WriteInterface>();

                            // CodeValidator does not have Variable information, so just check with predefined literal
                            if (info.ScriptFile.Equals("%ScriptFile%", StringComparison.OrdinalIgnoreCase) &&
                                CodeParser.StringContainsVariable(info.Section))
                            {
                                if (_sc.Sections.ContainsKey(info.Section))
                                    logs.AddRange(ValidateInterfaceSection(_sc.Sections[info.Section], cmd.RawCode, cmd.LineIdx));
                                else
                                    logs.Add(new LogInfo(LogState.Error, $"Section [{info.Section}] does not exist", cmd));
                            }
                        }
                        break;
                        #endregion
                }
            }
        }
        #endregion

        #region ValidateInterfaceSection
        private List<LogInfo> ValidateInterfaceSection(ScriptSection section, string rawLine = null, int lineIdx = 0)
        {
            // Already processed, so skip
            if (_visitedSections.Contains(section))
                return new List<LogInfo>();

            // Force parsing of code, bypassing caching by section.GetUICtrls()
            List<string> lines;
            try
            {
                lines = section.GetLines();
            }
            catch (InternalException)
            {
                string msg = $"Section [{section.Name}] is not a valid interface section";
                if (rawLine != null)
                    msg += $" ({rawLine})";
                if (0 < lineIdx)
                    msg += $" (Line {lineIdx})";

                return new List<LogInfo> { new LogInfo(LogState.Error, msg) };
            }
            List<UIControl> uiCtrls = UIParser.ParseStatements(lines, section, out List<LogInfo> logs);

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
                            logs.Add(new LogInfo(LogState.Error, $"Image resource [{uiCtrl.Text}] does not exist", uiCtrl));
                        break;
                    case UIControlType.TextFile:
                        if (!uiCtrl.Text.Equals(UIInfo_TextFile.NoResource, StringComparison.OrdinalIgnoreCase) &&
                            !EncodedFile.ContainsInterface(_sc, uiCtrl.Text))
                            logs.Add(new LogInfo(LogState.Error, $"Text resource [{uiCtrl.Text}] does not exist", uiCtrl));
                        break;
                    case UIControlType.Button:
                        {
                            UIInfo_Button info = uiCtrl.Info.Cast<UIInfo_Button>();

                            if (info.Picture != null &&
                                !info.Picture.Equals(UIInfo_Button.NoPicture, StringComparison.OrdinalIgnoreCase) &&
                                !EncodedFile.ContainsInterface(_sc, info.Picture))
                                logs.Add(new LogInfo(LogState.Error, $"Image resource [{info.Picture}] does not exist", uiCtrl));

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

            _visitedSections.Add(section);
            return logs;
        }
        #endregion

        #endregion
    }
}
