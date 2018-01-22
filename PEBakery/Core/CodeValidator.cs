/*
    Copyright (C) 2016-2017 Hajin Jang
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

using PEBakery.Helper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PEBakery.Core
{
    public class CodeValidator
    {
        #region Field and Property
        private Script p;
        private List<ScriptSection> visitedSections = new List<ScriptSection>();

        public int CodeSectionCount => p.Sections.Where(x => x.Value.Type == SectionType.Code).Count();
        public int VisitedSectionCount => visitedSections.Count;
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

        private List<LogInfo> logInfos = new List<LogInfo>();
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
        public CodeValidator(Script p)
        {
            this.p = p ?? throw new ArgumentNullException("p");
        }
        #endregion

        #region Validate
        public void Validate()
        {
            // Codes
            if (p.Sections.ContainsKey("Process"))
                logInfos.AddRange(ValidateCodeSection(p.Sections["Process"]));

            // UICodes
            if (p.Sections.ContainsKey("Interface"))
                logInfos.AddRange(ValidateUISection(p.Sections["Interface"]));

            if (p.MainInfo.ContainsKey("Interface"))
            {
                string ifaceSection = p.MainInfo["Interface"];
                if (p.Sections.ContainsKey(ifaceSection))
                    logInfos.AddRange(ValidateUISection(p.Sections[ifaceSection]));
            }
        }

        #region ValidateCodeSection
        private List<LogInfo> ValidateCodeSection(ScriptSection section)
        {
            // Already processed, so skip
            if (visitedSections.Contains(section))
                return new List<LogInfo>();

            // Force parsing of code, bypassing caching by section.GetCodes()
            List<string> lines = section.GetLines();
            SectionAddress addr = new SectionAddress(p, section);
            List<CodeCommand> codes = CodeParser.ParseStatements(lines, addr, out List<LogInfo> logs);

            visitedSections.Add(section);
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
                            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_If));
                            CodeInfo_If info = cmd.Info as CodeInfo_If;

                            if (info.Condition.Type == BranchConditionType.ExistSection)
                            { 
                                // Exception Handling for 1-files.script
                                // If,ExistSection,%ScriptFile%,Cache_Delete_B,Run,%ScriptFile%,Cache_Delete_B
                                if (info.Condition.Arg1.Equals("%ScriptFile%", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (info.Embed.Type == CodeType.Run || info.Embed.Type == CodeType.Exec)
                                    {
                                        Debug.Assert(info.Embed.Info.GetType() == typeof(CodeInfo_RunExec));
                                        CodeInfo_RunExec subInfo = info.Embed.Info as CodeInfo_RunExec;

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
                            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Else));
                            CodeInfo_Else info = cmd.Info as CodeInfo_Else;

                            InternalValidateCodes(info.Link, logs);
                        }
                        break;
                    case CodeType.Run:
                    case CodeType.Exec:
                        {
                            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_RunExec));
                            CodeInfo_RunExec info = cmd.Info as CodeInfo_RunExec;

                            // CodeValidator does not have Variable information, so just check with predefined literal
                            if (info.ScriptFile.Equals("%ScriptFile%", StringComparison.OrdinalIgnoreCase))
                            {
                                if (p.Sections.ContainsKey(info.SectionName))
                                    logs.AddRange(ValidateCodeSection(p.Sections[info.SectionName]));
                                else if (CodeParser.StringContainsVariable(info.SectionName) == false)
                                    logs.Add(new LogInfo(LogState.Error, $"Section [{info.SectionName}] does not exist", cmd));
                            }
                        }
                        break;
                    case CodeType.Loop:
                        {
                            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Loop));
                            CodeInfo_Loop info = cmd.Info as CodeInfo_Loop;

                            if (info.Break)
                                continue;

                            // CodeValidator does not have Variable information, so just check with predefined literal
                            if (info.ScriptFile.Equals("%ScriptFile%", StringComparison.OrdinalIgnoreCase))
                            {
                                if (p.Sections.ContainsKey(info.SectionName))
                                    logs.AddRange(ValidateCodeSection(p.Sections[info.SectionName]));
                                else if (CodeParser.StringContainsVariable(info.SectionName) == false)
                                    logs.Add(new LogInfo(LogState.Error, $"Section [{info.SectionName}] does not exist", cmd));
                            }
                        }
                        break;
                    default:
                        break;
                }
            }
        }
        #endregion

        #region ValidateUISection
        private List<LogInfo> ValidateUISection(ScriptSection section)
        {
            // Force parsing of code, bypassing caching by section.GetUICodes()
            List<string> lines = section.GetLines();
            SectionAddress addr = new SectionAddress(p, section);
            List<UICommand> uiCodes = UIParser.ParseRawLines(lines, addr, out List<LogInfo> logs);

            foreach (UICommand uiCmd in uiCodes)
            {
                switch (uiCmd.Type)
                {
                    case UIType.CheckBox:
                        {
                            Debug.Assert(uiCmd.Info.GetType() == typeof(UIInfo_CheckBox));
                            UIInfo_CheckBox info = uiCmd.Info as UIInfo_CheckBox;

                            if (info.SectionName != null)
                            {
                                if (p.Sections.ContainsKey(info.SectionName)) // Only if section exists
                                    logs.AddRange(ValidateCodeSection(p.Sections[info.SectionName]));
                            }
                        }
                        break;
                    case UIType.Button:
                        {
                            Debug.Assert(uiCmd.Info.GetType() == typeof(UIInfo_Button));
                            UIInfo_Button info = uiCmd.Info as UIInfo_Button;

                            if (info.SectionName != null)
                            {
                                if (p.Sections.ContainsKey(info.SectionName)) // Only if section exists
                                    logs.AddRange(ValidateCodeSection(p.Sections[info.SectionName]));
                            }
                        }
                        break;
                    case UIType.RadioButton:
                        {
                            Debug.Assert(uiCmd.Info.GetType() == typeof(UIInfo_RadioButton));
                            UIInfo_RadioButton info = uiCmd.Info as UIInfo_RadioButton;

                            if (info.SectionName != null)
                            {
                                if (p.Sections.ContainsKey(info.SectionName)) // Only if section exists
                                    logs.AddRange(ValidateCodeSection(p.Sections[info.SectionName]));
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
