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

using PEBakery.Helper;
using PEBakery.Ini;
using System;
using System.Collections.Generic;
using System.Media;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PEBakery.Core.Commands
{
    public static class CommandControl
    {
        public static List<LogInfo> Set(EngineState s, CodeCommand cmd)
        {
            CodeInfo_Set info = (CodeInfo_Set)cmd.Info;

            Variables.VarKeyType varType = Variables.DetectType(info.VarKey);
            if (varType == Variables.VarKeyType.None)
            {
                // Check Macro
                if (Regex.Match(info.VarKey, Macro.MacroNameRegex,
                    RegexOptions.Compiled | RegexOptions.CultureInvariant).Success) // Macro Name Validation
                {
                    string? macroCommand = StringEscaper.Preprocess(s, info.VarValue);

                    if (macroCommand.Equals("NIL", StringComparison.OrdinalIgnoreCase))
                        macroCommand = null;

                    LogInfo log = s.Macro.SetMacro(info.VarKey, macroCommand, cmd.Section, info.Permanent, false);
                    return new List<LogInfo>(1) { log };
                }
            }

            // [WB082 Behavior] -> Enabled if s.CompatAllowSetModifyInterface == true
            // If PERMANENT was used but the key exists in interface command, the value will not be written to script.project but in interface.
            // Need to investigate where the logs are saved in this case.
            switch (info.Permanent)
            {
                case true:
                    {
                        // Check if interface contains VarKey
                        List<LogInfo> logs = new List<LogInfo>();

                        if (Variables.DetectType(info.VarKey) != Variables.VarKeyType.Variable)
                            goto case false;

                        #region Set interface control's value (Compat)
                        if (s.CompatAllowSetModifyInterface)
                        {
                            string? varKey = Variables.TrimPercentMark(info.VarKey);
                            string finalValue = StringEscaper.Preprocess(s, info.VarValue);

                            Script sc = cmd.Section.Script;
                            ScriptSection? iface = sc.GetInterfaceSection(out _);
                            if (iface == null)
                                goto case false;

                            (List<UIControl> uiCtrls, _) = UIParser.ParseStatements(iface.Lines, iface);
                            UIControl? uiCtrl = uiCtrls.Find(x => x.Key.Equals(varKey, StringComparison.OrdinalIgnoreCase));
                            if (uiCtrl == null)
                                goto case false;

                            bool valid = uiCtrl.SetValue(finalValue, false, out List<LogInfo> varLogs);
                            logs.AddRange(varLogs);

                            if (valid)
                            {
                                // Update uiCtrl into file
                                uiCtrl.Update();

                                // Also update local variables
                                logs.AddRange(Variables.SetVariable(s, info.VarKey, info.VarValue, false, false));
                                return logs;
                            }
                        }

                        goto case false;
                        #endregion
                    }

                case false:
                default:
                    return Variables.SetVariable(s, info.VarKey, info.VarValue, info.Global, info.Permanent);
            }
        }

        public static List<LogInfo> SetMacro(EngineState s, CodeCommand cmd)
        {
            // SetMacro,<MacroName>,<MacroCommand>,[GLOBAL|PERMANENT]
            CodeInfo_SetMacro info = (CodeInfo_SetMacro)cmd.Info;

            string? macroCommand = StringEscaper.Preprocess(s, info.MacroCommand);

            if (macroCommand.Equals("NIL", StringComparison.OrdinalIgnoreCase))
                macroCommand = null;

            LogInfo log = s.Macro.SetMacro(info.MacroName, macroCommand, cmd.Section, info.Global, info.Permanent);
            return new List<LogInfo>(1) { log };
        }

        public static List<LogInfo> AddVariables(EngineState s, CodeCommand cmd)
        {
            CodeInfo_AddVariables info = (CodeInfo_AddVariables)cmd.Info;

            string scriptFile = StringEscaper.Preprocess(s, info.ScriptFile);
            string sectionName = StringEscaper.Preprocess(s, info.SectionName);

            Script sc = Engine.GetScriptInstance(s, s.CurrentScript.RealPath, scriptFile, out _);

            // Does section exist?
            if (!sc.Sections.ContainsKey(sectionName))
                return new List<LogInfo>
                    {new LogInfo(LogState.Error, $"Script [{scriptFile}] does not have section [{sectionName}]")};

            // Directly read from file
            List<string>? lines = IniReadWriter.ParseRawSection(sc.RealPath, sectionName);
            if (lines == null)
                return new List<LogInfo>
                    {new LogInfo(LogState.Error, $"Script [{scriptFile}] does not have section [{sectionName}]")};

            // Add Variables
            Dictionary<string, string> varDict = IniReadWriter.ParseIniLinesVarStyle(lines);
            List<LogInfo> varLogs = s.Variables.AddVariables(info.Global ? VarsType.Global : VarsType.Local, varDict);

            // Add Macros
            ScriptSection localSection = sc.Sections[sectionName];
            List<LogInfo> macroLogs = s.Macro.LoadMacroDict(info.Global ? MacroType.Global : MacroType.Local, localSection, lines, true);
            varLogs.AddRange(macroLogs);

            if (varLogs.Count == 0) // No variables
                varLogs.Add(new LogInfo(LogState.Info,
                    $"Script [{scriptFile}]'s section [{sectionName}] does not have any variables"));

            return varLogs;
        }

        public static List<LogInfo> Exit(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_Exit info = (CodeInfo_Exit)cmd.Info;

            string message = StringEscaper.Preprocess(s, info.Message);

            s.HaltReturnFlags.ScriptHalt = true;

            logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Warning, message, cmd));

            return logs;
        }

        public static List<LogInfo> Halt(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_Halt info = (CodeInfo_Halt)cmd.Info;

            string message = StringEscaper.Preprocess(s, info.Message);

            s.MainViewModel.TaskBarProgressState = System.Windows.Shell.TaskbarItemProgressState.Error;
            s.RunningSubProcess?.Kill();
            s.HaltReturnFlags.CmdHalt = true;

            logs.Add(new LogInfo(LogState.Warning, message, cmd));

            return logs;
        }

        public static List<LogInfo> Wait(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_Wait info = (CodeInfo_Wait)cmd.Info;

            if (!NumberHelper.ParseInt32(info.Second, out int second))
            {
                logs.Add(new LogInfo(LogState.Error, $"Argument [{info.Second}] is not a valid integer"));
                return logs;
            }

            if (second < 0)
            {
                logs.Add(new LogInfo(LogState.Error, $"Argument [{info.Second}] should be larger than 0"));
                return logs;
            }

            Task.Delay(TimeSpan.FromSeconds(second)).Wait();

            logs.Add(new LogInfo(LogState.Success, $"Slept [{info.Second}] seconds", cmd));

            return logs;
        }

        public static List<LogInfo> Beep(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_Beep info = (CodeInfo_Beep)cmd.Info;

            switch (info.Type)
            {
                case BeepType.OK:
                    SystemSounds.Beep.Play();
                    break;
                case BeepType.Error:
                    SystemSounds.Hand.Play();
                    break;
                case BeepType.Asterisk:
                    SystemSounds.Asterisk.Play();
                    break;
                case BeepType.Confirmation:
                    SystemSounds.Question.Play();
                    break;
            }

            logs.Add(new LogInfo(LogState.Success, $"Played sound [{info.Type}]", cmd));

            return logs;
        }

        /// <summary>
        /// Stop processing of current section and return.
        /// </summary>
        public static List<LogInfo> Return(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>(1);

            CodeInfo_Return info = (CodeInfo_Return)cmd.Info;

            string? returnValue = null;
            if (info.ReturnValue != null)
                returnValue = StringEscaper.Preprocess(s, info.ReturnValue);

            s.HaltReturnFlags.SectionReturn = true;

            string msg;
            if (returnValue == null)
            {
                msg = $"Returned from section [{s.CurrentSection.Name}]";
            }
            else
            {
                s.ReturnValue = returnValue;
                msg = $"Returned [{returnValue}] from section [{s.CurrentSection.Name}]";
            }

            logs.Add(new LogInfo(LogState.Info, msg, cmd));

            return logs;
        }

        #region GetParam, PackParam
        public static List<LogInfo> GetParam(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>(2);

            CodeInfo_GetParam info = (CodeInfo_GetParam)cmd.Info;

            string indexStr = StringEscaper.Preprocess(s, info.Index);
            if (!NumberHelper.ParseInt32(indexStr, out int index))
            {
                logs.Add(new LogInfo(LogState.Error, $"[{indexStr}] is not a valid integer"));
                return logs;
            }

            if (s.CurSectionInParams.ContainsKey(index) && index <= s.CurSectionInParamsCount)
            {
                string parameter = StringEscaper.Escape(s.CurSectionInParams[index], true, false);
                List<LogInfo> varLogs = Variables.SetVariable(s, info.DestVar, parameter, false, false);
                logs.AddRange(varLogs);
            }
            else
            {
                logs.Add(new LogInfo(LogState.Ignore, $"Section parameter [#{index}] does not exist"));
                List<LogInfo> varLogs = Variables.SetVariable(s, info.DestVar, string.Empty, false, false);
                logs.AddRange(varLogs);
            }

            return logs;
        }

        public static List<LogInfo> PackParam(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>(4);

            CodeInfo_PackParam info = (CodeInfo_PackParam)cmd.Info;

            string startIndexStr = StringEscaper.Preprocess(s, info.StartIndex);
            if (!NumberHelper.ParseInt32(startIndexStr, out int startIndex))
            {
                logs.Add(new LogInfo(LogState.Error, $"[{startIndexStr}] is not a valid integer"));
                return logs;
            }

            int varCount = s.CurSectionInParamsCount;
            if (startIndex <= varCount)
            {
                StringBuilder b = new StringBuilder();
                for (int i = 1; i <= varCount; i++)
                {
                    b.Append('"');
                    if (s.CurSectionInParams.ContainsKey(i))
                        b.Append(StringEscaper.Escape(s.CurSectionInParams[i], true, false));
                    b.Append('"');
                    if (i + 1 <= varCount)
                        b.Append(',');
                }

                logs.AddRange(Variables.SetVariable(s, info.DestVar, b.ToString(), false, false));
            }
            else
            {
                logs.Add(new LogInfo(LogState.Ignore,
                    $"StartIndex [#{startIndex}] is invalid, [{varCount}] section parameters provided."));
                logs.AddRange(Variables.SetVariable(s, info.DestVar, string.Empty, false, false));
            }

            if (info.VarCount != null)
                logs.AddRange(Variables.SetVariable(s, info.VarCount, varCount.ToString(), false, false));

            return logs;
        }
        #endregion
    }
}
