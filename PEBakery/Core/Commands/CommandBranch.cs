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

            string scriptFile = StringEscaper.Preprocess(s, info.ScriptFile);
            string sectionName = StringEscaper.Preprocess(s, info.SectionName);
            List<string> paramList = StringEscaper.Preprocess(s, info.Parameters);

            Script p = Engine.GetScriptInstance(s, cmd, s.CurrentScript.FullPath, scriptFile, out bool inCurrentScript);

            // Does section exists?
            if (!p.Sections.ContainsKey(sectionName))
                throw new ExecuteException($"[{scriptFile}] does not have section [{sectionName}]");

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
            s.Logger.LogStartOfSection(s, nextAddr, s.CurDepth, inCurrentScript, paramDict, cmd, forceLog);

            Dictionary<string, string> localVars = null;
            Dictionary<string, string> fixedVars = null;
            Dictionary<string, CodeCommand> localMacros = null;
            if (cmd.Type == CodeType.Exec)
            {
                // Backup Varaibles and Macros
                localVars = s.Variables.GetVarDict(VarsType.Local);
                fixedVars = s.Variables.GetVarDict(VarsType.Fixed);
                localMacros = s.Macro.LocalDict;

                // Load Per-Script Variables
                s.Variables.ResetVariables(VarsType.Local);
                List<LogInfo> varLogs = s.Variables.LoadDefaultScriptVariables(p);
                s.Logger.Build_Write(s, LogInfo.AddDepth(varLogs, s.CurDepth + 1));

                // Load Per-Script Macro
                s.Macro.ResetLocalMacros();
                List<LogInfo> macroLogs = s.Macro.LoadLocalMacroDict(p, false);
                s.Logger.Build_Write(s, LogInfo.AddDepth(macroLogs, s.CurDepth + 1));
            }

            // Run Section
            int depthBackup = s.CurDepth;
            int errorOffStartLineIdxBackup = s.ErrorOffStartLineIdx;
            int erroroffCountBackup = s.ErrorOffLineCount;
            Engine.RunSection(s, nextAddr, paramDict, s.CurDepth + 1, callback);

            if (cmd.Type == CodeType.Exec)
            {
                // Restore Variables
                s.Variables.SetVarDict(VarsType.Local, localVars);
                s.Variables.SetVarDict(VarsType.Fixed, fixedVars);

                // Restore Local Macros
                s.Macro.SetLocalMacros(localMacros);
            }

            s.CurDepth = depthBackup;
            s.ErrorOffStartLineIdx = errorOffStartLineIdxBackup;
            s.ErrorOffLineCount = erroroffCountBackup;
            s.Logger.LogEndOfSection(s, nextAddr, s.CurDepth, inCurrentScript, cmd, forceLog);
        }  

        public static void Loop(EngineState s, CodeCommand cmd)
        {
            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Loop));
            CodeInfo_Loop info = cmd.Info as CodeInfo_Loop;

            if (info.Break)
            {
                if (s.LoopState == LoopState.Off)
                {
                    s.Logger.Build_Write(s, new LogInfo(LogState.Error, "Loop is not running", cmd, s.CurDepth));
                }
                else
                {
                    s.LoopState = LoopState.Off;
                    s.Logger.Build_Write(s, new LogInfo(LogState.Info, "Breaking loop", cmd, s.CurDepth));

                    // Reset LoopCounter, to be sure
                    s.LoopLetter = ' ';
                    s.LoopCounter = 0;
                }
            }
            else if (s.LoopState != LoopState.Off)
            { // If loop is already turned on, throw error
                s.Logger.Build_Write(s, new LogInfo(LogState.Error, "Nested loop is not supported", cmd, s.CurDepth));
            }
            else
            {
                string startStr = StringEscaper.Preprocess(s, info.StartIdx);
                string endStr = StringEscaper.Preprocess(s, info.EndIdx);

                // Prepare Loop
                string scriptFile = StringEscaper.Preprocess(s, info.ScriptFile);
                string sectionName = StringEscaper.Preprocess(s, info.SectionName);
                List<string> paramList = StringEscaper.Preprocess(s, info.Parameters);

                Script p = Engine.GetScriptInstance(s, cmd, s.CurrentScript.FullPath, scriptFile, out bool inCurrentScript);

                // Does section exists?
                if (!p.Sections.ContainsKey(sectionName))
                    throw new ExecuteException($"[{scriptFile}] does not have section [{sectionName}]");

                // Section Parameter
                Dictionary<int, string> paramDict = new Dictionary<int, string>();
                for (int i = 0; i < paramList.Count; i++)
                    paramDict[i + 1] = paramList[i];

                //
                long loopCount = 0;
                long startIdx = 0, endIdx = 0;
                char startLetter = ' ', endLetter = ' ';
                switch (cmd.Type)
                {
                    case CodeType.Loop:
                        { // Integer Index
                            if (!NumberHelper.ParseInt64(startStr, out startIdx))
                                throw new ExecuteException($"Argument [{startStr}] is not a valid integer");
                            if (!NumberHelper.ParseInt64(endStr, out endIdx))
                                throw new ExecuteException($"Argument [{endStr}] is not a valid integer");
                            loopCount = endIdx - startIdx + 1;
                        }
                        break;
                    case CodeType.LoopLetter:
                        { // Drive Letter
                            if (!(startStr.Length == 1 && StringHelper.IsAlphabet(startStr[0])))
                                throw new ExecuteException($"Argument [{startStr}] is not a valid drive letter");
                            if (!(endStr.Length == 1 && StringHelper.IsAlphabet(endStr[0])))
                                throw new ExecuteException($"Argument [{endStr}] is not a valid drive letter");

                            startLetter = char.ToUpper(startStr[0]);
                            endLetter = char.ToUpper(endStr[0]);

                            if (endLetter < startLetter)
                                throw new ExecuteException($"StartLetter must be smaller than EndLetter in lexicographic order");

                            loopCount = endLetter - startLetter + 1;
                        }
                        break;
                    default:
                        throw new InternalException("Internal Logic Error at CommandBranch.Loop");
                }

                // Log Messages
                string logMessage;
                if (inCurrentScript)
                    logMessage = $"Loop Section [{sectionName}] [{loopCount}] times";
                else
                    logMessage = $"Loop [{p.Title}]'s Section [{sectionName}] [{loopCount}] times";
                s.Logger.Build_Write(s, new LogInfo(LogState.Info, logMessage, cmd, s.CurDepth));

                // Loop it
                SectionAddress nextAddr = new SectionAddress(p, p.Sections[sectionName]);
                int loopIdx = 1;
                switch (cmd.Type)
                {
                    case CodeType.Loop:
                        for (s.LoopCounter = startIdx; s.LoopCounter <= endIdx; s.LoopCounter++)
                        { // Counter Variable is [#c]
                            s.Logger.Build_Write(s, new LogInfo(LogState.Info, $"Entering Loop with [{s.LoopCounter}] ({loopIdx}/{loopCount})", cmd, s.CurDepth));
                            s.Logger.LogSectionParameter(s, s.CurDepth, paramDict, cmd);

                            int depthBackup = s.CurDepth;
                            s.LoopState = LoopState.OnIndex;
                            Engine.RunSection(s, nextAddr, paramDict, s.CurDepth + 1, true);
                            if (s.LoopState == LoopState.Off) // Loop,Break
                                break;
                            s.LoopState = LoopState.Off;
                            s.CurDepth = depthBackup;

                            s.Logger.Build_Write(s, new LogInfo(LogState.Info, $"End of Loop with [{s.LoopCounter}] ({loopIdx}/{loopCount})", cmd, s.CurDepth));
                            loopIdx += 1;
                        }
                        break;
                    case CodeType.LoopLetter:
                        for (s.LoopLetter = startLetter; s.LoopLetter <= endLetter; s.LoopLetter++)
                        { // Counter Variable is [#c]
                            s.Logger.Build_Write(s, new LogInfo(LogState.Info, $"Entering Loop with [{s.LoopLetter}] ({loopIdx}/{loopCount})", cmd, s.CurDepth));
                            s.Logger.LogSectionParameter(s, s.CurDepth, paramDict, cmd);

                            int depthBackup = s.CurDepth;
                            s.LoopState = LoopState.OnDriveLetter;
                            Engine.RunSection(s, nextAddr, paramDict, s.CurDepth + 1, true);
                            if (s.LoopState == LoopState.Off) // Loop,Break
                                break;
                            s.LoopState = LoopState.Off;
                            s.CurDepth = depthBackup;

                            s.Logger.Build_Write(s, new LogInfo(LogState.Info, $"End of Loop with [{s.LoopLetter}] ({loopIdx}/{loopCount})", cmd, s.CurDepth));
                            loopIdx += 1;
                        }
                        break;
                    default:
                        throw new InternalException("Internal Logic Error at CommandBranch.Loop");
                }

                // Reset LoopCounter, to be sure
                s.LoopLetter = ' ';
                s.LoopCounter = 0;
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
