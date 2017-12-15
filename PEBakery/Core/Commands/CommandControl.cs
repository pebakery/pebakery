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
*/

using PEBakery.Exceptions;
using PEBakery.Helper;
using PEBakery.IniLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PEBakery.Core.Commands
{
    public static class CommandControl
    {
        public static List<LogInfo> Set(EngineState s, CodeCommand cmd)
        {
            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Set));
            CodeInfo_Set info = cmd.Info as CodeInfo_Set;

            Variables.VarKeyType varType = Variables.DetermineType(info.VarKey);
            if (varType == Variables.VarKeyType.None)
            {
                // Check Macro
                if (Regex.Match(info.VarKey, Macro.MacroNameRegex, RegexOptions.Compiled).Success) // Macro Name Validation
                {
                    string macroCommand = StringEscaper.Preprocess(s, info.VarValue);

                    if (macroCommand.Equals("NIL", StringComparison.OrdinalIgnoreCase))
                        macroCommand = null;

                    LogInfo log = s.Macro.SetMacro(info.VarKey, macroCommand, cmd.Addr, info.Permanent);
                    return new List<LogInfo>(1) { log };
                }
            }

            // [WB082 Behavior]
            // If PERMANENT was used but the key exists in interface command, the value will not be written to script.project but in interface.
            // Need to investigate where the logs are saved in this case.
            switch (info.Permanent)
            {
                case true:
                    { // Check if interface contains VarKey
                        List<LogInfo> logs = new List<LogInfo>();

                        if (Variables.DetermineType(info.VarKey) != Variables.VarKeyType.Variable)
                            goto case false;

                        string varKey = Variables.TrimPercentMark(info.VarKey);
                        string finalValue = StringEscaper.Preprocess(s, info.VarValue);

                        #region Set UI
                        Plugin p = cmd.Addr.Plugin;
                        PluginSection iface = p.GetInterface(out string sectionName);
                        if (iface == null)
                            goto case false;

                        List<UICommand> uiCmds = iface.GetUICodes(true);
                        UICommand uiCmd = uiCmds.Find(x => x.Key.Equals(varKey));
                        if (uiCmd == null)
                            goto case false;

                        bool match = false;
                        switch (uiCmd.Type)
                        {
                            case UIType.TextBox:
                                {
                                    Debug.Assert(uiCmd.Info.GetType() == typeof(UIInfo_TextBox));
                                    UIInfo_TextBox uiInfo = uiCmd.Info as UIInfo_TextBox;

                                    uiInfo.Value = finalValue;

                                    logs.Add(new LogInfo(LogState.Success, $"Interface [{varKey}] set to [{finalValue}]"));
                                    match = true;
                                }
                                break;
                            case UIType.NumberBox:
                                {
                                    Debug.Assert(uiCmd.Info.GetType() == typeof(UIInfo_NumberBox));
                                    UIInfo_NumberBox uiInfo = uiCmd.Info as UIInfo_NumberBox;

                                    // WB082 just write string value in case of error, but PEBakery will throw warning
                                    if (!NumberHelper.ParseInt32(finalValue, out int intVal))
                                    {
                                        logs.Add(new LogInfo(LogState.Warning, $"[{finalValue}] is not valid integer"));
                                        return logs;
                                    }

                                    uiInfo.Value = intVal;

                                    logs.Add(new LogInfo(LogState.Success, $"Interface [{varKey}] set to [{finalValue}]"));
                                    match = true;
                                }
                                break;
                            case UIType.CheckBox:
                                {
                                    Debug.Assert(uiCmd.Info.GetType() == typeof(UIInfo_CheckBox));
                                    UIInfo_CheckBox uiInfo = uiCmd.Info as UIInfo_CheckBox;

                                    if (finalValue.Equals("True", StringComparison.OrdinalIgnoreCase))
                                    {
                                        uiInfo.Value = true;

                                        logs.Add(new LogInfo(LogState.Success, $"Interface [{varKey}] set to [True]"));
                                        match = true;
                                    }
                                    else if (finalValue.Equals("False", StringComparison.OrdinalIgnoreCase))
                                    {
                                        uiInfo.Value = false;

                                        logs.Add(new LogInfo(LogState.Success, $"Interface [{varKey}] set to [False]"));
                                        match = true;
                                    }
                                    else
                                    { // WB082 just write string value in case of error, but PEBakery will throw warning
                                        logs.Add(new LogInfo(LogState.Warning, $"[{finalValue}] is not valid boolean"));
                                        return logs;
                                    }
                                }
                                break;
                            case UIType.ComboBox:
                                {
                                    Debug.Assert(uiCmd.Info.GetType() == typeof(UIInfo_ComboBox));
                                    UIInfo_ComboBox uiInfo = uiCmd.Info as UIInfo_ComboBox;

                                    int idx = uiInfo.Items.FindIndex(x => x.Equals(finalValue, StringComparison.OrdinalIgnoreCase));
                                    if (idx == -1)
                                    { // Invalid Index
                                        logs.Add(new LogInfo(LogState.Warning, $"[{finalValue}] not found in item list"));
                                        return logs;
                                    }

                                    uiInfo.Index = idx;
                                    uiCmd.Text = uiInfo.Items[idx];

                                    logs.Add(new LogInfo(LogState.Success, $"Interface [{varKey}] set to [{uiCmd.Text}]"));
                                    match = true;
                                }
                                break;
                            case UIType.RadioButton:
                                {
                                    Debug.Assert(uiCmd.Info.GetType() == typeof(UIInfo_RadioButton));
                                    UIInfo_RadioButton uiInfo = uiCmd.Info as UIInfo_RadioButton;

                                    if (finalValue.Equals("True", StringComparison.OrdinalIgnoreCase))
                                    {
                                        uiInfo.Selected = true;

                                        logs.Add(new LogInfo(LogState.Success, $"Interface [{varKey}] set to true]"));
                                        match = true;
                                    }
                                    else if (finalValue.Equals("False", StringComparison.OrdinalIgnoreCase))
                                    {
                                        uiInfo.Selected = false;

                                        logs.Add(new LogInfo(LogState.Success, $"Interface [{varKey}] set to [False]"));
                                        match = true;
                                    }
                                    else
                                    { // WB082 just write string value, but PEBakery will ignore and throw and warning
                                        logs.Add(new LogInfo(LogState.Warning, $"[{finalValue}] is not valid boolean"));
                                        return logs;
                                    }
                                }
                                break;
                            case UIType.FileBox:
                                {
                                    Debug.Assert(uiCmd.Info.GetType() == typeof(UIInfo_FileBox));
                                    UIInfo_FileBox uiInfo = uiCmd.Info as UIInfo_FileBox;

                                    uiCmd.Text = finalValue;

                                    logs.Add(new LogInfo(LogState.Success, $"Interface [{varKey}] set to [{finalValue}]"));
                                    match = true;
                                }
                                break;
                            case UIType.RadioGroup:
                                {
                                    Debug.Assert(uiCmd.Info.GetType() == typeof(UIInfo_RadioGroup));
                                    UIInfo_RadioGroup uiInfo = uiCmd.Info as UIInfo_RadioGroup;

                                    int idx = uiInfo.Items.FindIndex(x => x.Equals(finalValue, StringComparison.OrdinalIgnoreCase));
                                    if (idx == -1)
                                    { // Invalid Index
                                        logs.Add(new LogInfo(LogState.Warning, $"[{finalValue}] not found in item list"));
                                        return logs;
                                    }

                                    uiInfo.Selected = idx;

                                    logs.Add(new LogInfo(LogState.Success, $"Interface [{varKey}] set to [{finalValue}]"));
                                    match = true;
                                }
                                break;
                        }

                        if (match)
                        {
                            uiCmd.Update();

                            logs.AddRange(Variables.SetVariable(s, info.VarKey, info.VarValue, false, false));
                            return logs;
                        }
                        else
                        {
                            goto case false;
                        }
                        #endregion
                    }
                case false:
                default:
                    return Variables.SetVariable(s, info.VarKey, info.VarValue, info.Global, info.Permanent);
            }
        }

        public static List<LogInfo> SetMacro(EngineState s, CodeCommand cmd)
        { // SetMacro,<MacroName>,<MacroCommand>,[PERMANENT]
            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_SetMacro));
            CodeInfo_SetMacro info = cmd.Info as CodeInfo_SetMacro;

            string macroCommand = StringEscaper.Preprocess(s, info.MacroCommand);

            if (macroCommand.Equals("NIL", StringComparison.OrdinalIgnoreCase))
                macroCommand = null;

            LogInfo log = s.Macro.SetMacro(info.MacroName, macroCommand, cmd.Addr, info.Permanent);
            return new List<LogInfo>(1) { log };
        }

        public static List<LogInfo> AddVariables(EngineState s, CodeCommand cmd)
        {
            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_AddVariables));
            CodeInfo_AddVariables info = cmd.Info as CodeInfo_AddVariables;

            string pluginFile = StringEscaper.Preprocess(s, info.PluginFile);
            string sectionName = StringEscaper.Preprocess(s, info.SectionName);

            Plugin p = Engine.GetPluginInstance(s, cmd, s.CurrentPlugin.FullPath,  pluginFile, out bool inCurrentPlugin);

            // Does section exists?
            if (!p.Sections.ContainsKey(sectionName))
                throw new ExecuteException($"Plugin [{pluginFile}] does not have section [{sectionName}]");

            // Directly read from file
            List<string> lines = Ini.ParseRawSection(p.FullPath, sectionName);

            // Add Variables
            Dictionary<string, string> varDict = Ini.ParseIniLinesVarStyle(lines);
            List<LogInfo> varLogs = s.Variables.AddVariables(info.Global ? VarsType.Global : VarsType.Local, varDict);

            // Add Macros
            SectionAddress addr = new SectionAddress(p, p.Sections[sectionName]);
            List<LogInfo> macroLogs = s.Macro.LoadLocalMacroDict(addr, lines, true);
            varLogs.AddRange(macroLogs);

            if (varLogs.Count == 0) // No variables
                varLogs.Add(new LogInfo(LogState.Info, $"Plugin [{pluginFile}]'s section [{sectionName}] does not have any variables"));

            return varLogs;
        }

        public static List<LogInfo> Exit(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Exit));
            CodeInfo_Exit info = cmd.Info as CodeInfo_Exit;

            s.PassCurrentPluginFlag = true;

            logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Warning, info.Message, cmd));

            return logs;
        }

        public static List<LogInfo> Halt(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Halt));
            CodeInfo_Halt info = cmd.Info as CodeInfo_Halt;

            if (s.RunningSubProcess != null)
                s.RunningSubProcess.Kill();
            s.CmdHaltFlag = true;

            logs.Add(new LogInfo(LogState.Warning, info.Message, cmd));

            return logs;
        }

        public static List<LogInfo> Wait(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Wait));
            CodeInfo_Wait info = cmd.Info as CodeInfo_Wait;

            if (NumberHelper.ParseInt32(info.Second, out int second) == false)
                throw new InvalidCodeCommandException($"Argument [{info.Second}] is not valid number", cmd);

            Task.Delay(second * 1000).Wait();

            logs.Add(new LogInfo(LogState.Success, $"Slept [{info.Second}] seconds", cmd));

            return logs;
        }

        public static List<LogInfo> Beep(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Beep));
            CodeInfo_Beep info = cmd.Info as CodeInfo_Beep;

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

        public static List<LogInfo> GetParam(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>(2);

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_GetParam));
            CodeInfo_GetParam info = cmd.Info as CodeInfo_GetParam;

            string indexStr = StringEscaper.Preprocess(s, info.Index);
            if (!NumberHelper.ParseInt32(indexStr, out int index))
                throw new ExecuteException($"[{indexStr}] is not a valid integer");

            if (s.CurSectionParams.ContainsKey(index) && index <= s.CurSectionParamsCount)
            {
                string parameter = StringEscaper.Escape(s.CurSectionParams[index], true, false);
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

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_PackParam));
            CodeInfo_PackParam info = cmd.Info as CodeInfo_PackParam;

            string startIndexStr = StringEscaper.Preprocess(s, info.StartIndex);
            if (!NumberHelper.ParseInt32(startIndexStr, out int startIndex))
                throw new ExecuteException($"[{startIndexStr}] is not a valid integer");

            int varCount = s.CurSectionParamsCount;
            if (startIndex <= varCount)
            {
                StringBuilder b = new StringBuilder();
                for (int i = 1; i <= varCount; i++)
                {
                    b.Append('"');
                    if (s.CurSectionParams.ContainsKey(i))
                        b.Append(StringEscaper.Escape(s.CurSectionParams[i], true, false));
                    b.Append('"');
                    if (i + 1 <= varCount)
                        b.Append(',');
                }

                logs.AddRange(Variables.SetVariable(s, info.DestVar, b.ToString(), false, false));
            }
            else
            {
                logs.Add(new LogInfo(LogState.Ignore, $"StartIndex [#{startIndex}] is invalid, [{varCount}] section parameters provided."));
                logs.AddRange(Variables.SetVariable(s, info.DestVar, string.Empty, false, false));
            }

            if (info.VarCount != null)
                logs.AddRange(Variables.SetVariable(s, info.VarCount, varCount.ToString(), false, false));

            return logs;
        }
    }
}
