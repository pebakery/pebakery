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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Win32;
using PEBakery.Exceptions;
using System.Globalization;
using System.Diagnostics;
using PEBakery.Helper;

namespace PEBakery.Core.Commands
{
    public static class CommandBranch
    {
        public static void RunExec(EngineState s, CodeCommand cmd, bool preserveCurParams = false, bool forceLog = false)
        {
            RunExec(s, cmd, preserveCurParams, forceLog, false);
        }

        public static void RunExec(EngineState s, CodeCommand cmd, bool preserveCurParams, bool forceLog, bool callback)
        {
            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_RunExec));
            CodeInfo_RunExec info = cmd.Info as CodeInfo_RunExec;

            string pluginFile = StringEscaper.Preprocess(s, info.PluginFile);
            string sectionName = StringEscaper.Preprocess(s, info.SectionName);
            List<string> paramList = StringEscaper.Preprocess(s, info.Parameters);

            Plugin p = Engine.GetPluginInstance(s, cmd, s.CurrentPlugin.FullPath, pluginFile, out bool inCurrentPlugin);

            // Does section exists?
            if (!p.Sections.ContainsKey(sectionName))
                throw new ExecuteException($"[{pluginFile}] does not have section [{sectionName}]");

            // Section Parameter
            Dictionary<int, string> paramDict = new Dictionary<int, string>();
            if (preserveCurParams)
            {
                paramDict = s.CurSectionParams;
            }
            else
            {
                for (int i = 0; i < paramList.Count; i++)
                    paramDict[i + 1] = paramList[i];
            }

            // Branch to new section
            SectionAddress nextAddr = new SectionAddress(p, p.Sections[sectionName]);
            s.Logger.LogStartOfSection(s, nextAddr, s.CurDepth, inCurrentPlugin, paramDict, cmd, forceLog);

            Dictionary<string, string> localVars = null;
            Dictionary<string, string> globalVars = null;
            Dictionary<string, string> fixedVars = null;
            Dictionary<string, CodeCommand> localMacros = null;
            if (cmd.Type == CodeType.Exec)
            {
                // Backup Varaibles and Macros
                localVars = s.Variables.GetVarDict(VarsType.Local);
                globalVars = s.Variables.GetVarDict(VarsType.Global);
                fixedVars = s.Variables.GetVarDict(VarsType.Fixed);
                localMacros = s.Macro.LocalDict;

                // Load Per-Plugin Variables
                s.Variables.ResetVariables(VarsType.Local);
                List<LogInfo> varLogs = s.Variables.LoadDefaultPluginVariables(p);
                s.Logger.Build_Write(s, LogInfo.AddDepth(varLogs, s.CurDepth + 1));

                // Load Per-Plugin Macro
                s.Macro.ResetLocalMacros();
                List<LogInfo> macroLogs = s.Macro.LoadLocalMacroDict(p);
                s.Logger.Build_Write(s, LogInfo.AddDepth(macroLogs, s.CurDepth + 1));
            }

            // Run Section
            int depthBackup = s.CurDepth;
            Engine.RunSection(s, nextAddr, paramDict, s.CurDepth + 1, callback);

            if (cmd.Type == CodeType.Exec)
            {
                // Restore Variables
                s.Variables.SetVarDict(VarsType.Local, localVars);
                s.Variables.SetVarDict(VarsType.Global, globalVars);
                s.Variables.SetVarDict(VarsType.Fixed, fixedVars);

                // Restore Local Macros
                s.Macro.LocalDict = localMacros;
            }

            s.CurDepth = depthBackup;
            s.Logger.LogEndOfSection(s, nextAddr, s.CurDepth, inCurrentPlugin, cmd, forceLog);
        }
            

        public static void Loop(EngineState s, CodeCommand cmd)
        {
            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Loop));
            CodeInfo_Loop info = cmd.Info as CodeInfo_Loop;

            if (info.Break)
            {
                if (s.LoopRunning)
                {
                    s.LoopRunning = false;
                    s.Logger.Build_Write(s, new LogInfo(LogState.Info, "Breaking Loop", cmd, s.CurDepth));
                }
                else
                {
                    s.Logger.Build_Write(s, new LogInfo(LogState.Error, "Loop is not running", cmd, s.CurDepth));
                }
            }
            else
            {
                string startIdxStr = StringEscaper.Preprocess(s, info.StartIdx);
                if (NumberHelper.ParseInt64(startIdxStr, out long startIdx) == false)
                    throw new ExecuteException($"Argument [{startIdxStr}] is not a valid integer");
                string endIdxStr = StringEscaper.Preprocess(s, info.EndIdx);
                if (NumberHelper.ParseInt64(endIdxStr, out long endIdx) == false)
                    throw new ExecuteException($"Argument [{endIdxStr}] is not a valid integer");
                long loopCount = endIdx - startIdx + 1;

                // Prepare Loop
                string pluginFile = StringEscaper.Preprocess(s, info.PluginFile);
                string sectionName = StringEscaper.Preprocess(s, info.SectionName);
                List<string> paramList = StringEscaper.Preprocess(s, info.Parameters);

                Plugin p = Engine.GetPluginInstance(s, cmd, s.CurrentPlugin.FullPath, pluginFile, out bool inCurrentPlugin);

                // Does section exists?
                if (!p.Sections.ContainsKey(sectionName))
                    throw new ExecuteException($"[{pluginFile}] does not have section [{sectionName}]");

                string logMessage;
                if (inCurrentPlugin)
                    logMessage = $"Loop Section [{sectionName}] [{loopCount}] times";
                else
                    logMessage = $"Loop [{p.Title}]'s Section [{sectionName}] [{loopCount}] times";
                s.Logger.Build_Write(s, new LogInfo(LogState.Info, logMessage, cmd, s.CurDepth));

                // Section Parameter
                Dictionary<int, string> paramDict = new Dictionary<int, string>();
                for (int i = 0; i < paramList.Count; i++)
                    paramDict[i + 1] = paramList[i];

                // Loop it
                SectionAddress nextAddr = new SectionAddress(p, p.Sections[sectionName]);
                for (s.LoopCounter = startIdx; s.LoopCounter <= endIdx; s.LoopCounter++)
                { // Counter Variable is [#c]
                    s.Logger.Build_Write(s, new LogInfo(LogState.Info, $"Entering Loop [{s.LoopCounter}/{loopCount}]", cmd, s.CurDepth));
                    s.Logger.LogSectionParameter(s, s.CurDepth, paramDict, cmd);

                    int depthBackup = s.CurDepth;
                    s.LoopRunning = true;
                    Engine.RunSection(s, nextAddr, paramDict, s.CurDepth + 1, true);
                    if (s.LoopRunning == false) // Loop,Break
                        break;
                    s.LoopRunning = false;
                    s.CurDepth = depthBackup;

                    s.Logger.Build_Write(s, new LogInfo(LogState.Info, $"End of Loop [{s.LoopCounter}/{loopCount}]", cmd, s.CurDepth));
                }
            }
        }

        public static void If(EngineState s, CodeCommand cmd)
        {
            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_If));
            CodeInfo_If info = cmd.Info as CodeInfo_If;

            if (info.Condition.Check(s, out string msg))
            { // Condition matched, run it
                s.Logger.Build_Write(s, new LogInfo(LogState.Success, msg, cmd, s.CurDepth));

                int depthBackup = s.CurDepth;
                Engine.RunCommands(s, cmd.Addr, info.Link, s.CurSectionParams, s.CurDepth + 1, false);
                s.CurDepth = depthBackup;
                s.Logger.Build_Write(s, new LogInfo(LogState.Info, $"End of CodeBlock", cmd, s.CurDepth));

                s.ElseFlag = false;
            }
            else
            { // Do not run
                s.Logger.Build_Write(s, new LogInfo(LogState.Ignore, msg, cmd, s.CurDepth));

                s.ElseFlag = true;
            }
        }

        public static void Else(EngineState s, CodeCommand cmd)
        {
            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Else));
            CodeInfo_Else info = cmd.Info as CodeInfo_Else;

            if (s.ElseFlag)
            {
                s.Logger.Build_Write(s, new LogInfo(LogState.Success, "Else condition met", cmd, s.CurDepth));

                int depthBackup = s.CurDepth;
                Engine.RunCommands(s, cmd.Addr, info.Link, s.CurSectionParams, s.CurDepth + 1, false);
                s.CurDepth = depthBackup;
                s.Logger.Build_Write(s, new LogInfo(LogState.Info, $"End of CodeBlock", cmd, s.CurDepth));

                s.ElseFlag = false;
            }
            else
            {
                s.Logger.Build_Write(s, new LogInfo(LogState.Ignore, "Else condition not met", cmd, s.CurDepth));
            }
        }
    }
}
