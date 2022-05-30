/*
    Copyright (C) 2016-2022 Hajin Jang
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

using ManagedWimLib;
using Microsoft.Win32;
using PEBakery.Core.WpfControls;
using PEBakery.Helper;
using PEBakery.Ini;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Shell;

namespace PEBakery.Core.Commands
{
    public class RunExecOptions
    {
        public bool PreserveCurrentParams { get; set; }
        public bool IsMacro { get; set; }
    }

    public static class CommandBranch
    {
        public static void RunExec(EngineState s, CodeCommand cmd, RunExecOptions opts)
        {
            CodeInfo_RunExec info = (CodeInfo_RunExec)cmd.Info;
            EngineLocalState ls = s.PeekLocalState();

            Debug.Assert((cmd.Type == CodeType.Run || cmd.Type == CodeType.Exec) && info.OutParams == null ||
                         cmd.Type == CodeType.RunEx && info.OutParams != null);

            string scriptFile = StringEscaper.Preprocess(s, info.ScriptFile);
            string sectionName = StringEscaper.Preprocess(s, info.SectionName);
            List<string> inParams = StringEscaper.Preprocess(s, info.InParams);

            Script sc = Engine.GetScriptInstance(s, s.CurrentScript.RealPath, scriptFile, out bool inCurrentScript);

            // Does section exists?
            if (!sc.Sections.ContainsKey(sectionName))
                throw new ExecuteException($"[{scriptFile}] does not have section [{sectionName}]");

            // Section Parameter
            Dictionary<int, string> newInParams = new Dictionary<int, string>();
            if (opts.PreserveCurrentParams)
            {
                newInParams = s.CurSectionInParams;
            }
            else
            {
                for (int i = 0; i < inParams.Count; i++)
                    newInParams[i + 1] = inParams[i];
            }

            // Prepare to branch to a new section
            ScriptSection targetSection = sc.Sections[sectionName];
            s.Logger.LogStartOfSection(s, targetSection, s.PeekDepth, inCurrentScript, newInParams, info.OutParams, cmd);

            // Backup Variables and Macros for Exec
            Dictionary<string, string>? localVars = null;
            Dictionary<string, string>? fixedVars = null;
            Dictionary<string, CodeCommand>? localMacros = null;
            if (cmd.Type == CodeType.Exec)
            {
                // Backup Variables and Macros
                localVars = s.Variables.GetVarDict(VarsType.Local);
                fixedVars = s.Variables.GetVarDict(VarsType.Fixed);
                localMacros = s.Macro.GetMacroDict(MacroType.Local);

                // Load Per-Script Variables
                s.Variables.ResetVariables(VarsType.Local);
                List<LogInfo> varLogs = s.Variables.LoadDefaultScriptVariables(sc);
                s.Logger.BuildWrite(s, LogInfo.AddDepth(varLogs, ls.Depth + 1));

                // Load Per-Script Macro
                s.Macro.ResetMacroDict(MacroType.Local);
                List<LogInfo> macroLogs = s.Macro.LoadMacroDict(MacroType.Local, sc, false);
                s.Logger.BuildWrite(s, LogInfo.AddDepth(macroLogs, ls.Depth + 1));
            }

            // Run Section
            Engine.RunSection(s, targetSection, newInParams, info.OutParams, new EngineLocalState
            {
                IsMacro = opts.IsMacro | ls.IsMacro,
                RefScriptId = inCurrentScript ? 0 : s.Logger.BuildRefScriptWrite(s, sc, false),
            });

            // Restore Variables and Macros for Exec
            if (cmd.Type == CodeType.Exec)
            {
                if (localVars == null)
                    throw new CriticalErrorException($"{localVars} is null");
                if (fixedVars == null)
                    throw new CriticalErrorException($"{fixedVars} is null");
                if (localMacros == null)
                    throw new CriticalErrorException($"{localMacros} is null");

                // Restore Variables
                s.Variables.SetVarDict(VarsType.Local, localVars);
                s.Variables.SetVarDict(VarsType.Fixed, fixedVars);

                // Restore Local Macros
                s.Macro.SetMacroDict(MacroType.Local, localMacros);
            }

            s.Logger.LogEndOfSection(s, targetSection, ls.Depth, inCurrentScript, cmd);
        }

        public static void Loop(EngineState s, CodeCommand cmd)
        {
            CodeInfo_Loop info = (CodeInfo_Loop)cmd.Info;
            EngineLocalState ls = s.PeekLocalState();

            if (info.Break)
            {
                if (s.LoopStateStack.Count == 0)
                {
                    s.Logger.BuildWrite(s, new LogInfo(LogState.Error, "Loop is not running", cmd, ls.Depth));
                }
                else
                {
                    s.Logger.BuildWrite(s, new LogInfo(LogState.Info, "Breaking loop", cmd, ls.Depth));
                    s.LoopStateStack.Pop();
                }
            }
            else
            {
                if (info.StartIdx == null)
                    throw new CriticalErrorException($"{info.StartIdx} is null");
                if (info.EndIdx == null)
                    throw new CriticalErrorException($"{info.EndIdx} is null");
                if (info.ScriptFile == null)
                    throw new CriticalErrorException($"{info.ScriptFile} is null");
                if (info.SectionName == null)
                    throw new CriticalErrorException($"{info.SectionName} is null");
                if (info.InParams == null)
                    throw new CriticalErrorException($"{info.InParams} is null");

                string startStr = StringEscaper.Preprocess(s, info.StartIdx);
                string endStr = StringEscaper.Preprocess(s, info.EndIdx);

                // Prepare Loop
                string scriptFile = StringEscaper.Preprocess(s, info.ScriptFile);
                string sectionName = StringEscaper.Preprocess(s, info.SectionName);
                List<string> inParams = StringEscaper.Preprocess(s, info.InParams);

                Script sc = Engine.GetScriptInstance(s, s.CurrentScript.RealPath, scriptFile, out bool inCurrentScript);

                // Does section exist?
                if (!sc.Sections.ContainsKey(sectionName))
                    throw new ExecuteException($"[{scriptFile}] does not have section [{sectionName}]");

                // Section In Parameter
                Dictionary<int, string> newInParams = new Dictionary<int, string>();
                for (int i = 0; i < inParams.Count; i++)
                    newInParams[i + 1] = inParams[i];

                long loopCount;
                long startIdx = 0, endIdx = 0;
                char startLetter = ' ';
                char endLetter = ' ';
                CodeType type = cmd.Type;
                switch (type)
                {
                    case CodeType.Loop:
                    case CodeType.LoopEx:
                        { // Integer Index
                            bool startIdxError = false;
                            bool endIdxError = false;

                            if (!NumberHelper.ParseInt64(startStr, out startIdx))
                                startIdxError = true;
                            if (!NumberHelper.ParseInt64(endStr, out endIdx))
                                endIdxError = true;

                            if (s.CompatAllowLetterInLoop && startIdxError && endIdxError &&
                                startStr.Length == 1 && StringHelper.IsAlphabet(startStr[0]) &&
                                endStr.Length == 1 && StringHelper.IsAlphabet(endStr[0]))
                            {
                                type = CodeType.LoopLetter;
                                goto case CodeType.LoopLetter;
                            }
                            else if (startIdxError)
                                throw new ExecuteException($"Argument [{startStr}] is not a valid integer");
                            else if (endIdxError)
                                throw new ExecuteException($"Argument [{endStr}] is not a valid integer");

                            loopCount = endIdx - startIdx + 1;
                        }
                        break;
                    case CodeType.LoopLetter:
                    case CodeType.LoopLetterEx:
                        { // Drive Letter
                            if (!(startStr.Length == 1 && StringHelper.IsAlphabet(startStr[0])))
                                throw new ExecuteException($"Argument [{startStr}] is not a valid drive letter");
                            if (!(endStr.Length == 1 && StringHelper.IsAlphabet(endStr[0])))
                                throw new ExecuteException($"Argument [{endStr}] is not a valid drive letter");

                            startLetter = char.ToUpper(startStr[0]);
                            endLetter = char.ToUpper(endStr[0]);

                            if (endLetter < startLetter)
                                throw new ExecuteException("<StartLetter> must be smaller than <EndLetter> in lexicographic order");

                            loopCount = endLetter - startLetter + 1;
                        }
                        break;
                    default:
                        throw new InternalException("Internal Logic Error at CommandBranch.Loop");
                }

                // Log Messages
                string logMessage;
                if (inCurrentScript)
                    logMessage = $"Loop Section [{sectionName}] [{loopCount}] times ({startStr} ~ {endStr})";
                else
                    logMessage = $"Loop [{sc.Title}]'s Section [{sectionName}] [{loopCount}] times";
                s.Logger.BuildWrite(s, new LogInfo(LogState.Info, logMessage, cmd, ls.Depth));

                // Loop it
                ScriptSection targetSection = sc.Sections[sectionName];
                int loopIdx = 1;
                switch (type)
                {
                    case CodeType.Loop:
                    case CodeType.LoopEx:
                        for (long i = startIdx; i <= endIdx; i++)
                        { // Counter Variable is [#c]
                            s.Logger.BuildWrite(s, new LogInfo(LogState.Info, $"Entering Loop with [{i}] ({loopIdx}/{loopCount})", cmd, ls.Depth));
                            s.Logger.LogSectionParameter(s, ls.Depth, newInParams, info.OutParams, cmd);

                            // Push EngineLoopState
                            EngineLoopState loop = new EngineLoopState(i);
                            s.LoopStateStack.Push(loop);
                            int stackCount = s.LoopStateStack.Count;

                            // Run Loop Section
                            Engine.RunSection(s, targetSection, newInParams, info.OutParams, new EngineLocalState
                            {
                                IsMacro = ls.IsMacro,
                                RefScriptId = inCurrentScript ? 0 : s.Logger.BuildRefScriptWrite(s, sc, false),
                            });

                            // Loop,Break can pop loop state stack.
                            // Check stackCount to know if Loop,Break was called.
                            if (stackCount != s.LoopStateStack.Count)
                                break;

                            // Pop EngineLoopState
                            EngineLoopState popLoop = s.LoopStateStack.Pop();

                            // Log message
                            string msg = $"End of Loop with [{i}] ({loopIdx}/{loopCount})";
                            if (s.CompatOverridableLoopCounter)
                            {
                                if (popLoop.CounterIndex != i)
                                    msg = $"End of Loop with [{popLoop.CounterIndex}] (Overridden) ({loopIdx}/{loopCount})";
                            }
                            s.Logger.BuildWrite(s, new LogInfo(LogState.Info, msg, cmd, ls.Depth));

                            // Increase loop index
                            loopIdx += 1;
                        }
                        break;
                    case CodeType.LoopLetter:
                    case CodeType.LoopLetterEx:
                        for (char ch = startLetter; ch <= endLetter; ch++)
                        { // Counter Variable is [#c]
                            s.Logger.BuildWrite(s, new LogInfo(LogState.Info, $"Entering Loop with [{ch}] ({loopIdx}/{loopCount})", cmd, ls.Depth));
                            s.Logger.LogSectionParameter(s, ls.Depth, newInParams, info.OutParams, cmd);

                            // Push EngineLoopState
                            EngineLoopState loop = new EngineLoopState(ch);
                            s.LoopStateStack.Push(loop);
                            int stackCount = s.LoopStateStack.Count;

                            // Run Loop Section
                            Engine.RunSection(s, targetSection, newInParams, info.OutParams, new EngineLocalState
                            {
                                IsMacro = ls.IsMacro,
                                RefScriptId = inCurrentScript ? 0 : s.Logger.BuildRefScriptWrite(s, sc, false),
                            });

                            // Loop,Break can pop loop state stack.
                            // Check stackCount to know if Loop,Break was called.
                            if (stackCount != s.LoopStateStack.Count)
                                break;

                            // Pop EngineLoopState
                            EngineLoopState popLoop = s.LoopStateStack.Pop();

                            // Log message
                            string msg = $"End of Loop with [{ch}] ({loopIdx}/{loopCount})";
                            if (s.CompatOverridableLoopCounter)
                            {
                                if (popLoop.CounterLetter != ch)
                                    msg = $"End of Loop with [{popLoop.CounterLetter}] (Overridden) ({loopIdx}/{loopCount})";
                            }
                            s.Logger.BuildWrite(s, new LogInfo(LogState.Info, msg, cmd, ls.Depth));

                            // Increase loop index
                            loopIdx += 1;
                        }
                        break;
                    default:
                        throw new InternalException("Internal Logic Error at CommandBranch.Loop");
                }
            }
        }

        public static void If(EngineState s, CodeCommand cmd)
        {
            CodeInfo_If info = (CodeInfo_If)cmd.Info;
            EngineLocalState ls = s.PeekLocalState();

            if (EvalBranchCondition(s, info.Condition, out string msg))
            { // Condition matched, run it
                s.Logger.BuildWrite(s, new LogInfo(LogState.Success, msg, cmd, ls.Depth));

                RunBranchLink(s, cmd.Section, info.Link);

                s.Logger.BuildWrite(s, new LogInfo(LogState.Info, "End of CodeBlock", cmd, ls.Depth));

                s.ElseFlag = false;
            }
            else
            { // Do not run
                s.Logger.BuildWrite(s, new LogInfo(LogState.Ignore, msg, cmd, ls.Depth));

                s.ElseFlag = true;
            }
        }

        public static void Else(EngineState s, CodeCommand cmd)
        {
            CodeInfo_Else info = (CodeInfo_Else)cmd.Info;
            EngineLocalState ls = s.PeekLocalState();

            if (s.ElseFlag)
            {
                s.Logger.BuildWrite(s, new LogInfo(LogState.Success, "Else condition met", cmd, ls.Depth));

                RunBranchLink(s, cmd.Section, info.Link);

                s.Logger.BuildWrite(s, new LogInfo(LogState.Info, "End of CodeBlock", cmd, ls.Depth));

                // Do not turn of ElseFlag for If command, to allow If-Else chain.
                // https://github.com/pebakery/pebakery/issues/114
                CodeCommand[] filtered = info.Link.Where(x => x.Type != CodeType.Comment).ToArray();
                if (!(filtered.Length == 1 && filtered[0].Type == CodeType.If))
                    s.ElseFlag = false;
            }
            else
            {
                s.Logger.BuildWrite(s, new LogInfo(LogState.Ignore, "Else condition not met", cmd, ls.Depth));
            }
        }

        private static void RunBranchLink(EngineState s, ScriptSection section, List<CodeCommand> link)
        {
            if (link.Count == 1)
            { // Check if link[0] is System,ErrorOff
                CodeCommand subCmd = link[0];
                if (subCmd.Type == CodeType.System)
                {
                    CodeInfo_System info = (CodeInfo_System)subCmd.Info;

                    if (info.Type == SystemType.ErrorOff)
                        s.ErrorOffDepthMinusOne = true;
                }
            }

            Engine.RunCommands(s, section, link, s.CurSectionInParams, s.CurSectionOutParams, true);
        }

        #region EvalBranchCondition
        public static bool EvalBranchCondition(EngineState s, BranchCondition c, out string logMessage)
        {
            bool match = false;
            switch (c.Type)
            {
                case BranchConditionType.Equal:
                case BranchConditionType.Smaller:
                case BranchConditionType.Bigger:
                case BranchConditionType.SmallerEqual:
                case BranchConditionType.BiggerEqual:
                case BranchConditionType.EqualX:
                    {
                        if (c.Arg1 == null)
                            throw new CriticalErrorException($"{nameof(c.Arg1)} is null");
                        if (c.Arg2 == null)
                            throw new CriticalErrorException($"{nameof(c.Arg2)} is null");

                        string compArg1 = StringEscaper.Preprocess(s, c.Arg1);
                        string compArg2 = StringEscaper.Preprocess(s, c.Arg2);

                        bool ignoreCase = c.Type != BranchConditionType.EqualX;

                        NumberHelper.CompareStringNumberResult comp = NumberHelper.CompareStringNumber(compArg1, compArg2, ignoreCase);
                        switch (comp)
                        {
                            case NumberHelper.CompareStringNumberResult.Equal: // For String and Number
                                {
                                    if (c.Type == BranchConditionType.Equal && !c.NotFlag ||
                                        c.Type == BranchConditionType.SmallerEqual && !c.NotFlag ||
                                        c.Type == BranchConditionType.BiggerEqual && !c.NotFlag ||
                                        c.Type == BranchConditionType.Smaller && c.NotFlag ||
                                        c.Type == BranchConditionType.Bigger && c.NotFlag ||
                                        c.Type == BranchConditionType.EqualX && !c.NotFlag)
                                        match = true;
                                    logMessage = $"[{compArg1}] is equal to [{compArg2}]";
                                }
                                break;
                            case NumberHelper.CompareStringNumberResult.Smaller: // For Number
                                {
                                    if (c.Type == BranchConditionType.Smaller && !c.NotFlag ||
                                        c.Type == BranchConditionType.SmallerEqual && !c.NotFlag ||
                                        c.Type == BranchConditionType.Bigger && c.NotFlag ||
                                        c.Type == BranchConditionType.BiggerEqual && c.NotFlag ||
                                        c.Type == BranchConditionType.Equal && c.NotFlag ||
                                        c.Type == BranchConditionType.EqualX && c.NotFlag)
                                        match = true;
                                    logMessage = $"[{compArg1}] is smaller than [{compArg2}]";
                                }
                                break;
                            case NumberHelper.CompareStringNumberResult.Bigger: // For Number
                                {
                                    if (c.Type == BranchConditionType.Bigger && !c.NotFlag ||
                                        c.Type == BranchConditionType.BiggerEqual && !c.NotFlag ||
                                        c.Type == BranchConditionType.Smaller && c.NotFlag ||
                                        c.Type == BranchConditionType.SmallerEqual && c.NotFlag ||
                                        c.Type == BranchConditionType.Equal && c.NotFlag ||
                                        c.Type == BranchConditionType.EqualX && c.NotFlag)
                                        match = true;
                                    logMessage = $"[{compArg1}] is bigger than [{compArg2}]";
                                }
                                break;
                            case NumberHelper.CompareStringNumberResult.NotEqual: // For String
                                {
                                    if (c.Type == BranchConditionType.Equal && c.NotFlag ||
                                        c.Type == BranchConditionType.EqualX && c.NotFlag)
                                        match = true;
                                    logMessage = $"[{compArg1}] is not equal to [{compArg2}]";
                                }
                                break;
                            default:
                                throw new InternalException($"Cannot compare [{compArg1}] and [{compArg2}]");
                        }
                    }
                    break;
                case BranchConditionType.ExistFile:
                    {
                        if (c.Arg1 == null)
                            throw new CriticalErrorException($"{nameof(c.Arg1)} is null");

                        string filePath = StringEscaper.Preprocess(s, c.Arg1);

                        // Check filePath contains wildcard
                        bool containsWildcard = Path.GetFileName(filePath).IndexOfAny(new char[] { '*', '?' }) != -1;

                        // Check if file exists
                        if (filePath.Length == 0)
                        {
                            match = false;
                        }
                        else if (containsWildcard)
                        {
                            if (!Directory.Exists(FileHelper.GetDirNameEx(filePath)))
                            {
                                match = false;
                            }
                            else
                            {
                                string[] list = Directory.GetFiles(FileHelper.GetDirNameEx(filePath), Path.GetFileName(filePath));
                                if (0 < list.Length)
                                    match = true;
                                else
                                    match = false;
                            }
                        }
                        else
                        {
                            match = File.Exists(filePath);
                        }

                        if (match)
                            logMessage = $"File [{filePath}] exists";
                        else
                            logMessage = $"File [{filePath}] does not exist";

                        if (c.NotFlag)
                            match = !match;
                    }
                    break;
                case BranchConditionType.ExistDir:
                    {
                        if (c.Arg1 == null)
                            throw new CriticalErrorException($"{nameof(c.Arg1)} is null");

                        string dirPath = StringEscaper.Preprocess(s, c.Arg1);
                        Debug.Assert(dirPath != null, "Internal Logic Error at CommandBranch.CheckBranchCondition");

                        // Check filePath contains wildcard
                        bool containsWildcard = Path.GetFileName(dirPath).IndexOfAny(new char[] { '*', '?' }) != -1;

                        // Check if directory exists
                        if (dirPath.Length == 0)
                        {
                            match = false;
                        }
                        else if (containsWildcard)
                        {
                            if (!Directory.Exists(FileHelper.GetDirNameEx(dirPath)))
                            {
                                match = false;
                            }
                            else
                            {
                                string[] list = Directory.GetDirectories(FileHelper.GetDirNameEx(dirPath), Path.GetFileName(dirPath));
                                if (0 < list.Length)
                                    match = true;
                                else
                                    match = false;
                            }
                        }
                        else
                        {
                            match = Directory.Exists(dirPath);
                        }

                        if (match)
                            logMessage = $"Directory [{dirPath}] exists";
                        else
                            logMessage = $"Directory [{dirPath}] does not exist";

                        if (c.NotFlag)
                            match = !match;
                    }
                    break;
                case BranchConditionType.ExistSection:
                    {
                        if (c.Arg1 == null)
                            throw new CriticalErrorException($"{nameof(c.Arg1)} is null");
                        if (c.Arg2 == null)
                            throw new CriticalErrorException($"{nameof(c.Arg2)} is null");

                        string iniFile = StringEscaper.Preprocess(s, c.Arg1);
                        string section = StringEscaper.Preprocess(s, c.Arg2);

                        match = IniReadWriter.ContainsSection(iniFile, section);
                        if (match)
                            logMessage = $"Section [{section}] exists in INI file [{iniFile}]";
                        else
                            logMessage = $"Section [{section}] does not exist in INI file [{iniFile}]";

                        if (c.NotFlag)
                            match = !match;
                    }
                    break;
                case BranchConditionType.ExistRegSection:
                case BranchConditionType.ExistRegSubKey:
                    {
                        if (c.Arg1 == null)
                            throw new CriticalErrorException($"{nameof(c.Arg1)} is null");
                        if (c.Arg2 == null)
                            throw new CriticalErrorException($"{nameof(c.Arg2)} is null");

                        string rootKey = StringEscaper.Preprocess(s, c.Arg1);
                        string subKey = StringEscaper.Preprocess(s, c.Arg2);

                        RegistryKey? regRoot = RegistryHelper.ParseStringToRegKey(rootKey);
                        if (regRoot == null)
                            throw new InvalidOperationException($"Invalid registry root key [{rootKey}]");
                        using (RegistryKey? regSubKey = regRoot.OpenSubKey(subKey))
                        {
                            match = regSubKey != null;
                            if (match)
                                logMessage = $"Registry SubKey [{rootKey}\\{subKey}] exists";
                            else
                                logMessage = $"Registry SubKey [{rootKey}\\{subKey}] does not exist";
                        }

                        if (c.NotFlag)
                            match = !match;
                    }
                    break;
                case BranchConditionType.ExistRegKey:
                case BranchConditionType.ExistRegValue:
                    {
                        if (c.Arg1 == null)
                            throw new CriticalErrorException($"{nameof(c.Arg1)} is null");
                        if (c.Arg2 == null)
                            throw new CriticalErrorException($"{nameof(c.Arg2)} is null");
                        if (c.Arg3 == null)
                            throw new CriticalErrorException($"{nameof(c.Arg3)} is null");

                        string rootKey = StringEscaper.Preprocess(s, c.Arg1);
                        string subKey = StringEscaper.Preprocess(s, c.Arg2);
                        string valueName = StringEscaper.Preprocess(s, c.Arg3);

                        match = true;
                        RegistryKey? regRoot = RegistryHelper.ParseStringToRegKey(rootKey);
                        if (regRoot == null)
                            throw new InvalidOperationException($"Invalid registry root key [{rootKey}]");
                        using (RegistryKey? regSubKey = regRoot.OpenSubKey(subKey))
                        {
                            if (regSubKey == null)
                            {
                                match = false;
                            }
                            else
                            {
                                object? value = regSubKey.GetValue(valueName);
                                if (value == null)
                                    match = false;
                            }

                            if (match)
                                logMessage = $"Registry Value [{rootKey}\\{subKey}\\{valueName}] exists";
                            else
                                logMessage = $"Registry Value [{rootKey}\\{subKey}\\{valueName}] does not exist";
                        }

                        if (c.NotFlag)
                            match = !match;
                    }
                    break;
                case BranchConditionType.ExistRegMulti:
                    {
                        if (c.Arg1 == null)
                            throw new CriticalErrorException($"{nameof(c.Arg1)} is null");
                        if (c.Arg2 == null)
                            throw new CriticalErrorException($"{nameof(c.Arg2)} is null");
                        if (c.Arg3 == null)
                            throw new CriticalErrorException($"{nameof(c.Arg3)} is null");
                        if (c.Arg4 == null)
                            throw new CriticalErrorException($"{nameof(c.Arg4)} is null");

                        string rootKey = StringEscaper.Preprocess(s, c.Arg1);
                        string subKey = StringEscaper.Preprocess(s, c.Arg2);
                        string valueName = StringEscaper.Preprocess(s, c.Arg3);
                        string searchStr = StringEscaper.Preprocess(s, c.Arg4);

                        match = false;
                        RegistryKey? regRoot = RegistryHelper.ParseStringToRegKey(rootKey);
                        if (regRoot == null)
                            throw new InvalidOperationException($"Invalid registry root key [{rootKey}]");
                        using (RegistryKey? regSubKey = regRoot.OpenSubKey(subKey))
                        {
                            if (regSubKey == null)
                            {
                                logMessage = $"Registry SubKey [{rootKey}\\{subKey}] does not exist";
                            }
                            else
                            {
                                object? valueData = regSubKey.GetValue(valueName, null);
                                if (valueData == null)
                                {
                                    logMessage = $"Registry Value [{rootKey}\\{subKey}\\{valueName}] does not exist";
                                }
                                else
                                {
                                    RegistryValueKind kind = regSubKey.GetValueKind(valueName);
                                    if (kind != RegistryValueKind.MultiString)
                                    {
                                        logMessage = $"Registry Value [{rootKey}\\{subKey}\\{valueName}] is not REG_MULTI_SZ";
                                    }
                                    else
                                    {
                                        string[] strs = (string[])valueData;
                                        if (strs.Contains(searchStr, StringComparer.OrdinalIgnoreCase))
                                        {
                                            match = true;
                                            logMessage = $"Registry Value [{rootKey}\\{subKey}\\{valueName}] contains substring [{searchStr}]";
                                        }
                                        else
                                        {
                                            logMessage = $"Registry Value [{rootKey}\\{subKey}\\{valueName}] does not contain substring [{searchStr}]";
                                        }
                                    }
                                }
                            }
                        }

                        if (c.NotFlag)
                            match = !match;
                    }
                    break;
                case BranchConditionType.ExistVar:
                    {
                        if (c.Arg1 == null)
                            throw new CriticalErrorException($"{nameof(c.Arg1)} is null");

                        Variables.VarKeyType type = Variables.DetectType(c.Arg1);
                        if (type == Variables.VarKeyType.Variable)
                        {
                            string? varKey = Variables.TrimPercentMark(c.Arg1);
                            if (varKey == null)
                            {
                                match = false;
                                logMessage = $"Variable key [{c.Arg1}] is not a valid variable format";
                            }
                            else
                            {
                                match = s.Variables.ContainsKey(varKey);
                                if (match)
                                    logMessage = $"Variable [{c.Arg1}] exists";
                                else
                                    logMessage = $"Variable [{c.Arg1}] does not exist";
                            }
                        }
                        else
                        {
                            match = false;
                            logMessage = $"[{c.Arg1}] is not a variable";
                        }

                        if (c.NotFlag)
                            match = !match;
                    }
                    break;
                case BranchConditionType.ExistMacro:
                    {
                        if (c.Arg1 == null)
                            throw new CriticalErrorException($"{nameof(c.Arg1)} is null");

                        string macroName = StringEscaper.Preprocess(s, c.Arg1);
                        match = s.Macro.GlobalDict.ContainsKey(macroName) || s.Macro.LocalDict.ContainsKey(macroName);

                        if (match)
                            logMessage = $"Macro [{macroName}] exists";
                        else
                            logMessage = $"Macro [{macroName}] does not exist";

                        if (c.NotFlag)
                            match = !match;
                    }
                    break;
                case BranchConditionType.WimExistIndex:
                    {
                        if (c.Arg1 == null)
                            throw new CriticalErrorException($"{nameof(c.Arg1)} is null");
                        if (c.Arg2 == null)
                            throw new CriticalErrorException($"{nameof(c.Arg2)} is null");

                        string wimFile = StringEscaper.Preprocess(s, c.Arg1);
                        string imageIndexStr = StringEscaper.Preprocess(s, c.Arg2);

                        if (!NumberHelper.ParseInt32(imageIndexStr, out int imageIndex))
                            logMessage = $"Index [{imageIndexStr}] is not a positive integer";
                        else if (imageIndex < 1)
                            logMessage = $"Index [{imageIndexStr}] is not a positive integer";
                        else
                        {
                            if (File.Exists(wimFile))
                            {
                                try
                                {
                                    using (Wim wim = Wim.OpenWim(wimFile, OpenFlags.None))
                                    {
                                        WimInfo wi = wim.GetWimInfo();
                                        if (imageIndex <= wi.ImageCount)
                                        {
                                            match = true;
                                            logMessage = $"ImageIndex [{imageIndex}] exists in [{wimFile}]";
                                        }
                                        else
                                        {
                                            logMessage = $"ImageIndex [{imageIndex}] does not exist in [{wimFile}]";
                                        }
                                    }
                                }
                                catch (WimLibException e)
                                {
                                    logMessage = $"Error [{e.ErrorCode}] occured while handling [{wimFile}]";
                                }
                            }
                            else
                            {
                                logMessage = $"Wim file [{wimFile}] does not exist";
                            }
                        }

                        if (c.NotFlag)
                            match = !match;
                    }
                    break;
                case BranchConditionType.WimExistFile:
                    {
                        if (c.Arg1 == null)
                            throw new CriticalErrorException($"{nameof(c.Arg1)} is null");
                        if (c.Arg2 == null)
                            throw new CriticalErrorException($"{nameof(c.Arg2)} is null");
                        if (c.Arg3 == null)
                            throw new CriticalErrorException($"{nameof(c.Arg3)} is null");

                        string wimFile = StringEscaper.Preprocess(s, c.Arg1);
                        string imageIndexStr = StringEscaper.Preprocess(s, c.Arg2);
                        string filePath = StringEscaper.Preprocess(s, c.Arg3);

                        if (!NumberHelper.ParseInt32(imageIndexStr, out int imageIndex))
                            logMessage = $"Index [{imageIndexStr}] is not a positive integer";
                        else if (imageIndex < 1)
                            logMessage = $"Index [{imageIndexStr}] is not a positive integer";
                        else
                        {
                            if (File.Exists(wimFile))
                            {
                                try
                                {
                                    using (Wim wim = Wim.OpenWim(wimFile, OpenFlags.None))
                                    {
                                        bool isFile = false;
                                        int WimExistFileCallback(DirEntry dentry, object userData)
                                        {
                                            if ((dentry.Attributes & FileAttributes.Directory) == 0)
                                                isFile = true;

                                            return Wim.IterateCallbackSuccess;
                                        }

                                        try
                                        {
                                            wim.IterateDirTree(imageIndex, filePath, IterateDirTreeFlags.None, WimExistFileCallback, null);

                                            if (isFile)
                                            {
                                                match = true;
                                                logMessage = $"File [{filePath}] exists in [{wimFile}]";
                                            }
                                            else
                                            {
                                                logMessage = $"File [{filePath}] does not exist in [{wimFile}]";
                                            }
                                        }
                                        catch (WimLibException e)
                                        {
                                            switch (e.ErrorCode)
                                            {
                                                case ErrorCode.InvalidImage:
                                                    logMessage = $"File [{filePath}] does not have image index [{imageIndex}]";
                                                    break;
                                                case ErrorCode.PathDoesNotExist:
                                                    logMessage = $"File [{filePath}] does not exist in [{wimFile}]";
                                                    break;
                                                default:
                                                    logMessage = $"Error [{e.ErrorCode}] occured while handling [{wimFile}]";
                                                    break;
                                            }
                                        }
                                    }
                                }
                                catch (WimLibException e)
                                {
                                    logMessage = $"Error [{e.ErrorCode}] occured while handling [{wimFile}]";
                                }
                            }
                            else
                            {
                                logMessage = $"Wim file [{wimFile}] does not exist";
                            }
                        }

                        if (c.NotFlag)
                            match = !match;
                    }
                    break;
                case BranchConditionType.WimExistDir:
                    {
                        if (c.Arg1 == null)
                            throw new CriticalErrorException($"{nameof(c.Arg1)} is null");
                        if (c.Arg2 == null)
                            throw new CriticalErrorException($"{nameof(c.Arg2)} is null");
                        if (c.Arg3 == null)
                            throw new CriticalErrorException($"{nameof(c.Arg3)} is null");

                        string wimFile = StringEscaper.Preprocess(s, c.Arg1);
                        string imageIndexStr = StringEscaper.Preprocess(s, c.Arg2);
                        string dirPath = StringEscaper.Preprocess(s, c.Arg3);

                        if (!NumberHelper.ParseInt32(imageIndexStr, out int imageIndex))
                            logMessage = $"Index [{imageIndexStr}] is not a positive integer";
                        else if (imageIndex < 1)
                            logMessage = $"Index [{imageIndexStr}] is not a positive integer";
                        else
                        {
                            if (File.Exists(wimFile))
                            {
                                try
                                {
                                    using (Wim wim = Wim.OpenWim(wimFile, OpenFlags.None))
                                    {
                                        bool isDir = false;
                                        int WimExistFileCallback(DirEntry dentry, object userData)
                                        {
                                            if ((dentry.Attributes & FileAttributes.Directory) != 0)
                                                isDir = true;

                                            return Wim.IterateCallbackSuccess;
                                        }

                                        try
                                        {
                                            wim.IterateDirTree(imageIndex, dirPath, IterateDirTreeFlags.None, WimExistFileCallback, null);

                                            if (isDir)
                                            {
                                                match = true;
                                                logMessage = $"Dir [{dirPath}] exists in [{wimFile}]";
                                            }
                                            else
                                            {
                                                logMessage = $"Dir [{dirPath}] does not exist in [{wimFile}]";
                                            }
                                        }
                                        catch (WimLibException e)
                                        {
                                            switch (e.ErrorCode)
                                            {
                                                case ErrorCode.InvalidImage:
                                                    logMessage = $"Dir [{dirPath}] does not have image index [{imageIndex}]";
                                                    break;
                                                case ErrorCode.PathDoesNotExist:
                                                    logMessage = $"Dir [{dirPath}] does not exist in [{wimFile}]";
                                                    break;
                                                default:
                                                    logMessage = $"Error [{e.ErrorCode}] occured while handling [{wimFile}]";
                                                    break;
                                            }
                                        }
                                    }
                                }
                                catch (WimLibException e)
                                {
                                    logMessage = $"Error [{e.ErrorCode}] occured while handling [{wimFile}]";
                                }
                            }
                            else
                            {
                                logMessage = $"Wim file [{wimFile}] does not exist";
                            }
                        }

                        if (c.NotFlag)
                            match = !match;
                    }
                    break;
                case BranchConditionType.WimExistImageInfo:
                    {
                        if (c.Arg1 == null)
                            throw new CriticalErrorException($"{nameof(c.Arg1)} is null");
                        if (c.Arg2 == null)
                            throw new CriticalErrorException($"{nameof(c.Arg2)} is null");
                        if (c.Arg3 == null)
                            throw new CriticalErrorException($"{nameof(c.Arg3)} is null");

                        string wimFile = StringEscaper.Preprocess(s, c.Arg1);
                        string imageIndexStr = StringEscaper.Preprocess(s, c.Arg2);
                        string key = StringEscaper.Preprocess(s, c.Arg3).ToUpper();

                        if (!NumberHelper.ParseInt32(imageIndexStr, out int imageIndex))
                            logMessage = $"Index [{imageIndexStr}] is not a positive integer";
                        else if (imageIndex < 1)
                            logMessage = $"Index [{imageIndexStr}] is not a positive integer";
                        else
                        {
                            if (File.Exists(wimFile))
                            {
                                try
                                {
                                    using (Wim wim = Wim.OpenWim(wimFile, OpenFlags.None))
                                    {
                                        string dest = wim.GetImageProperty(imageIndex, key);
                                        if (dest != null)
                                        {
                                            match = true;
                                            logMessage = $"Key [{key}] exists in [{wimFile}:{imageIndex}]";
                                        }
                                        else
                                        {
                                            logMessage = $"Key [{key}] does not exist in [{wimFile}:{imageIndex}]";
                                        }
                                    }
                                }
                                catch (WimLibException e)
                                {
                                    logMessage = $"Error [{e.ErrorCode}] occured while handling [{wimFile}]";
                                }
                            }
                            else
                            {
                                logMessage = $"Wim file [{wimFile}] does not exist";
                            }
                        }

                        if (c.NotFlag)
                            match = !match;
                    }
                    break;
                case BranchConditionType.Ping:
                    {
                        if (c.Arg1 == null)
                            throw new CriticalErrorException($"{nameof(c.Arg1)} is null");

                        string host = StringEscaper.Preprocess(s, c.Arg1);

                        try
                        {
                            using (Ping ping = new Ping())
                            {
                                PingReply reply = ping.Send(host);
                                Debug.Assert(reply != null, nameof(reply) + " != null");
                                if (reply.Status == IPStatus.Success)
                                    match = true;
                                else
                                    match = false;
                            }

                            if (match)
                                logMessage = $"[{host}] responded to Ping";
                            else
                                logMessage = $"[{host}] did not respond to Ping";
                        }
                        catch (PingException e) when (e.InnerException != null)
                        {
                            match = false;

                            // ReSharper disable once PossibleNullReferenceException
                            logMessage = $"Error while pinging [{host}] : [{e.InnerException.Message}]";
                        }
                        catch (Exception e)
                        {
                            match = false;
                            logMessage = $"Error while pinging [{host}] : [{e.Message}]";
                        }

                        if (c.NotFlag)
                            match = !match;
                    }
                    break;
                case BranchConditionType.Online:
                    {
                        // Note that system connected only to local network also returns true
                        match = NetworkInterface.GetIsNetworkAvailable();

                        if (match)
                            logMessage = "Network is online";
                        else
                            logMessage = "Network is offline";

                        if (c.NotFlag)
                            match = !match;
                    }
                    break;
                case BranchConditionType.Question: // can have 1 or 3 argument
                    {
                        if (c.Arg1 == null)
                            throw new CriticalErrorException($"{nameof(c.Arg1)} is null");

                        string message = StringEscaper.Preprocess(s, c.Arg1);

                        bool autoTimeout = c.Arg2 != null && c.Arg3 != null;

                        int timeout = 0;
                        bool defaultChoice = false;
                        if (autoTimeout)
                        {
                            if (c.Arg2 == null)
                                throw new CriticalErrorException($"{nameof(c.Arg2)} is null");
                            string timeoutStr = StringEscaper.Preprocess(s, c.Arg2);
                            if (NumberHelper.ParseInt32(timeoutStr, out timeout) == false)
                                autoTimeout = false;
                            if (timeout <= 0)
                                autoTimeout = false;

                            if (c.Arg3 == null)
                                throw new CriticalErrorException($"{nameof(c.Arg3)} is null");
                            string defaultChoiceStr = StringEscaper.Preprocess(s, c.Arg3);
                            if (defaultChoiceStr.Equals("True", StringComparison.OrdinalIgnoreCase))
                                defaultChoice = true;
                            else if (defaultChoiceStr.Equals("False", StringComparison.OrdinalIgnoreCase))
                                defaultChoice = false;
                        }

                        TaskbarItemProgressState oldTaskBarItemProgressState = s.MainViewModel.TaskBarProgressState; // Save our progress state
                        s.MainViewModel.TaskBarProgressState = TaskbarItemProgressState.Paused;

                        if (autoTimeout)
                        {
                            MessageBoxResult result = CustomMessageBox.DispatcherShow(s.OwnerWindow, message, "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question, timeout);

                            switch (result)
                            {
                                case MessageBoxResult.None:
                                    match = defaultChoice;
                                    if (defaultChoice)
                                        logMessage = "[Yes] was automatically chosen";
                                    else
                                        logMessage = "[No] was automatically chosen";
                                    break;
                                case MessageBoxResult.Yes:
                                    match = true;
                                    logMessage = "[Yes] was chosen";
                                    break;
                                case MessageBoxResult.No:
                                    match = false;
                                    logMessage = "[No] was chosen";
                                    break;
                                default:
                                    throw new InternalException("Internal Logic Error at Check() of If,Question");
                            }
                        }
                        else
                        {
                            MessageBoxResult result = SystemHelper.MessageBoxDispatcherShow(s.OwnerWindow, message, "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
                                
                            switch (result)
                            {
                                case MessageBoxResult.Yes:
                                    match = true;
                                    logMessage = "[Yes] was chosen";
                                    break;
                                case MessageBoxResult.No:
                                default:
                                    match = false;
                                    logMessage = "[No] was chosen";
                                    break;
                            }
                        }

                        if (c.NotFlag)
                            match = !match;

                        s.MainViewModel.TaskBarProgressState = oldTaskBarItemProgressState;
                    }
                    break;
                default:
                    throw new InternalException("Internal BranchCondition check error");
            }
            return match;
        }
        #endregion
    }
}
