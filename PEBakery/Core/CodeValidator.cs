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
using System.Text;
using System.Threading.Tasks;

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
                else
                    return (double) VisitedSectionCount / CodeSectionCount;
            }
        }

        private readonly List<LogInfo> logInfos = new List<LogInfo>();
        public LogInfo[] LogInfos
        {
            get
            { // Call .ToArray to get logInfo's copy 
                LogInfo[] list = logInfos.ToArray();
                logInfos.Clear();
                return list;
            }
        }
        #endregion

        #region Constructor
        public CodeValidator(Script sc)
        {
            this._sc = sc ?? throw new ArgumentNullException(nameof(sc));
        }
        #endregion

        #region Validate
        public void Validate()
        {
            // Codes
            if (_sc.Sections.ContainsKey("Process"))
                logInfos.AddRange(ValidateCodeSection(_sc.Sections["Process"]));

            // UICtrls
            if (_sc.Sections.ContainsKey("Interface"))
                logInfos.AddRange(ValidateUISection(_sc.Sections["Interface"]));

            if (_sc.MainInfo.ContainsKey("Interface"))
            {
                string ifaceSection = _sc.MainInfo["Interface"];
                if (_sc.Sections.ContainsKey(ifaceSection))
                    logInfos.AddRange(ValidateUISection(_sc.Sections[ifaceSection]));
            }
        }

        #region ValidateCodeSection
        private List<LogInfo> ValidateCodeSection(ScriptSection section, string rawLine = null)
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
                string msg;
                if (rawLine == null)
                    msg = $"Section [{section.Name}] is not a valid code section";
                else
                    msg = $"Section [{section.Name}] is not a valid code section ({rawLine})";

                return new List<LogInfo> { new LogInfo(LogState.Error, msg) };
            }

            SectionAddress addr = new SectionAddress(_sc, section);
            List<CodeCommand> codes = CodeParser.ParseStatements(lines, addr, out List<LogInfo> logs);

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
                    case CodeType.If:
                        {
                            CodeInfo_If info = cmd.Info.Cast<CodeInfo_If>();

                            if (info.Condition.Type == BranchConditionType.ExistSection)
                            { 
                                // Exception Handling for Win10PESE's 1-files.script
                                // If,ExistSection,%ScriptFile%,Cache_Delete_B,Run,%ScriptFile%,Cache_Delete_B
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
                        {
                            CodeInfo_RunExec info = cmd.Info.Cast<CodeInfo_RunExec>();

                            // CodeValidator does not have Variable information, so just check with predefined literal
                            if (info.ScriptFile.Equals("%ScriptFile%", StringComparison.OrdinalIgnoreCase))
                            {
                                if (_sc.Sections.ContainsKey(info.SectionName))
                                    logs.AddRange(ValidateCodeSection(_sc.Sections[info.SectionName], cmd.RawCode));
                                else if (CodeParser.StringContainsVariable(info.SectionName) == false)
                                    logs.Add(new LogInfo(LogState.Error, $"Section [{info.SectionName}] does not exist", cmd));
                            }
                        }
                        break;
                    case CodeType.Loop:
                        {
                            CodeInfo_Loop info = cmd.Info.Cast<CodeInfo_Loop>();

                            if (info.Break)
                                continue;

                            // CodeValidator does not have Variable information, so just check with predefined literal
                            if (info.ScriptFile.Equals("%ScriptFile%", StringComparison.OrdinalIgnoreCase))
                            {
                                if (_sc.Sections.ContainsKey(info.SectionName))
                                    logs.AddRange(ValidateCodeSection(_sc.Sections[info.SectionName], cmd.RawCode));
                                else if (CodeParser.StringContainsVariable(info.SectionName) == false)
                                    logs.Add(new LogInfo(LogState.Error, $"Section [{info.SectionName}] does not exist", cmd));
                            }
                        }
                        break;
                }
            }
        }
        #endregion

        #region ValidateUISection
        private List<LogInfo> ValidateUISection(ScriptSection section)
        {
            // Force parsing of code, bypassing caching by section.GetUICtrls()
            List<string> lines;
            try
            {
                lines = section.GetLines();
            }
            catch (InternalException)
            {
                return new List<LogInfo> { new LogInfo(LogState.Error, $"Section [{section.Name}] is not a valid interface section") };
            }
            SectionAddress addr = new SectionAddress(_sc, section);
            List<UIControl> uiCtrls = UIParser.ParseStatements(lines, addr, out List<LogInfo> logs);

            foreach (UIControl uiCmd in uiCtrls)
            {
                switch (uiCmd.Type)
                {
                    case UIControlType.CheckBox:
                        {
                            Debug.Assert(uiCmd.Info.GetType() == typeof(UIInfo_CheckBox));
                            UIInfo_CheckBox info = uiCmd.Info as UIInfo_CheckBox;

                            if (info.SectionName != null)
                            {
                                if (_sc.Sections.ContainsKey(info.SectionName)) // Only if section exists
                                    logs.AddRange(ValidateCodeSection(_sc.Sections[info.SectionName], uiCmd.RawLine));
                            }
                        }
                        break;
                    case UIControlType.Button:
                        {
                            Debug.Assert(uiCmd.Info.GetType() == typeof(UIInfo_Button));
                            UIInfo_Button info = uiCmd.Info as UIInfo_Button;

                            if (info.SectionName != null)
                            {
                                if (_sc.Sections.ContainsKey(info.SectionName)) // Only if section exists
                                    logs.AddRange(ValidateCodeSection(_sc.Sections[info.SectionName], uiCmd.RawLine));
                            }
                        }
                        break;
                    case UIControlType.RadioButton:
                        {
                            Debug.Assert(uiCmd.Info.GetType() == typeof(UIInfo_RadioButton));
                            UIInfo_RadioButton info = uiCmd.Info as UIInfo_RadioButton;

                            if (info.SectionName != null)
                            {
                                if (_sc.Sections.ContainsKey(info.SectionName)) // Only if section exists
                                    logs.AddRange(ValidateCodeSection(_sc.Sections[info.SectionName], uiCmd.RawLine));
                            }
                        }
                        break;
                }
            }

            return logs;
        }
        #endregion

        #endregion
    }
}
