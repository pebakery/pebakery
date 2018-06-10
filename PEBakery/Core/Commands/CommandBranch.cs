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
using System.Windows;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Net.NetworkInformation;
using Microsoft.Win32;
using ManagedWimLib;
using PEBakery.Helper;
using PEBakery.IniLib;
using PEBakery.WPF.Controls;

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
            CodeInfo_RunExec info = cmd.Info.Cast<CodeInfo_RunExec>();

            string scriptFile = StringEscaper.Preprocess(s, info.ScriptFile);
            string sectionName = StringEscaper.Preprocess(s, info.SectionName);
            List<string> paramList = StringEscaper.Preprocess(s, info.Parameters);

            Script sc = Engine.GetScriptInstance(s, cmd, s.CurrentScript.RealPath, scriptFile, out bool inCurrentScript);

            // Does section exists?
            if (!sc.Sections.ContainsKey(sectionName))
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
            SectionAddress nextAddr = new SectionAddress(sc, sc.Sections[sectionName]);
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
                List<LogInfo> varLogs = s.Variables.LoadDefaultScriptVariables(sc);
                s.Logger.BuildWrite(s, LogInfo.AddDepth(varLogs, s.CurDepth + 1));

                // Load Per-Script Macro
                s.Macro.ResetLocalMacros();
                List<LogInfo> macroLogs = s.Macro.LoadLocalMacroDict(sc, false);
                s.Logger.BuildWrite(s, LogInfo.AddDepth(macroLogs, s.CurDepth + 1));
            }

            // Run Section
            int depthBackup = s.CurDepth;
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
            s.Logger.LogEndOfSection(s, nextAddr, s.CurDepth, inCurrentScript, cmd, forceLog);
        }

        public static void Loop(EngineState s, CodeCommand cmd)
        {
            CodeInfo_Loop info = cmd.Info.Cast<CodeInfo_Loop>();

            if (info.Break)
            {
                if (s.LoopState == LoopState.Off)
                {
                    s.Logger.BuildWrite(s, new LogInfo(LogState.Error, "Loop is not running", cmd, s.CurDepth));
                }
                else
                {
                    s.LoopState = LoopState.Off;
                    s.Logger.BuildWrite(s, new LogInfo(LogState.Info, "Breaking loop", cmd, s.CurDepth));

                    // Reset LoopCounter, to be sure
                    s.LoopLetter = ' ';
                    s.LoopCounter = 0;
                }
            }
            else if (s.LoopState != LoopState.Off)
            { // If loop is already turned on, throw error
                s.Logger.BuildWrite(s, new LogInfo(LogState.Error, "Nested loop is not supported", cmd, s.CurDepth));
            }
            else
            {
                string startStr = StringEscaper.Preprocess(s, info.StartIdx);
                string endStr = StringEscaper.Preprocess(s, info.EndIdx);

                // Prepare Loop
                string scriptFile = StringEscaper.Preprocess(s, info.ScriptFile);
                string sectionName = StringEscaper.Preprocess(s, info.SectionName);
                List<string> paramList = StringEscaper.Preprocess(s, info.Parameters);

                Script sc = Engine.GetScriptInstance(s, cmd, s.CurrentScript.RealPath, scriptFile, out bool inCurrentScript);

                // Does section exists?
                if (!sc.Sections.ContainsKey(sectionName))
                    throw new ExecuteException($"[{scriptFile}] does not have section [{sectionName}]");

                // Section Parameter
                Dictionary<int, string> paramDict = new Dictionary<int, string>();
                for (int i = 0; i < paramList.Count; i++)
                    paramDict[i + 1] = paramList[i];

                long loopCount;
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
                s.Logger.BuildWrite(s, new LogInfo(LogState.Info, logMessage, cmd, s.CurDepth));

                // Loop it
                SectionAddress nextAddr = new SectionAddress(sc, sc.Sections[sectionName]);
                int loopIdx = 1;
                switch (cmd.Type)
                {
                    case CodeType.Loop:
                        for (s.LoopCounter = startIdx; s.LoopCounter <= endIdx; s.LoopCounter++)
                        { // Counter Variable is [#c]
                            s.Logger.BuildWrite(s, new LogInfo(LogState.Info, $"Entering Loop with [{s.LoopCounter}] ({loopIdx}/{loopCount})", cmd, s.CurDepth));
                            s.Logger.LogSectionParameter(s, s.CurDepth, paramDict, cmd);

                            int depthBackup = s.CurDepth;
                            s.LoopState = LoopState.OnIndex;
                            Engine.RunSection(s, nextAddr, paramDict, s.CurDepth + 1, true);
                            if (s.LoopState == LoopState.Off) // Loop,Break
                                break;
                            s.LoopState = LoopState.Off;
                            s.CurDepth = depthBackup;

                            s.Logger.BuildWrite(s, new LogInfo(LogState.Info, $"End of Loop with [{s.LoopCounter}] ({loopIdx}/{loopCount})", cmd, s.CurDepth));
                            loopIdx += 1;
                        }
                        break;
                    case CodeType.LoopLetter:
                        for (s.LoopLetter = startLetter; s.LoopLetter <= endLetter; s.LoopLetter++)
                        { // Counter Variable is [#c]
                            s.Logger.BuildWrite(s, new LogInfo(LogState.Info, $"Entering Loop with [{s.LoopLetter}] ({loopIdx}/{loopCount})", cmd, s.CurDepth));
                            s.Logger.LogSectionParameter(s, s.CurDepth, paramDict, cmd);

                            int depthBackup = s.CurDepth;
                            s.LoopState = LoopState.OnDriveLetter;
                            Engine.RunSection(s, nextAddr, paramDict, s.CurDepth + 1, true);
                            if (s.LoopState == LoopState.Off) // Loop,Break
                                break;
                            s.LoopState = LoopState.Off;
                            s.CurDepth = depthBackup;

                            s.Logger.BuildWrite(s, new LogInfo(LogState.Info, $"End of Loop with [{s.LoopLetter}] ({loopIdx}/{loopCount})", cmd, s.CurDepth));
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
            CodeInfo_If info = cmd.Info.Cast<CodeInfo_If>();

            if (CheckBranchCondition(s, info.Condition, out string msg))
            { // Condition matched, run it
                s.Logger.BuildWrite(s, new LogInfo(LogState.Success, msg, cmd, s.CurDepth));

                RunBranchLink(s, cmd.Addr, info.Link);

                s.Logger.BuildWrite(s, new LogInfo(LogState.Info, "End of CodeBlock", cmd, s.CurDepth));

                s.ElseFlag = false;
            }
            else
            { // Do not run
                s.Logger.BuildWrite(s, new LogInfo(LogState.Ignore, msg, cmd, s.CurDepth));

                s.ElseFlag = true;
            }
        }

        public static void Else(EngineState s, CodeCommand cmd)
        {
            CodeInfo_Else info = cmd.Info.Cast<CodeInfo_Else>();

            if (s.ElseFlag)
            {
                s.Logger.BuildWrite(s, new LogInfo(LogState.Success, "Else condition met", cmd, s.CurDepth));

                RunBranchLink(s, cmd.Addr, info.Link);

                s.Logger.BuildWrite(s, new LogInfo(LogState.Info, "End of CodeBlock", cmd, s.CurDepth));

                s.ElseFlag = false;
            }
            else
            {
                s.Logger.BuildWrite(s, new LogInfo(LogState.Ignore, "Else condition not met", cmd, s.CurDepth));
            }
        }

        private static void RunBranchLink(EngineState s, SectionAddress addr, List<CodeCommand> link)
        {
            int depthBackup = s.CurDepth;
            if (link.Count == 1)
            { // Check if link[0] is System,ErrorOff
                CodeCommand subCmd = link[0];
                if (subCmd.Type == CodeType.System)
                {
                    CodeInfo_System info = subCmd.Info.Cast<CodeInfo_System>();

                    if (info.Type == SystemType.ErrorOff)
                        s.ErrorOffDepthMinusOne = true;
                }
            }

            Engine.RunCommands(s, addr, link, s.CurSectionParams, s.CurDepth + 1, false);
            s.CurDepth = depthBackup;
        }

        #region BranchConditionCheck
        public static bool CheckBranchCondition(EngineState s, BranchCondition c, out string logMessage)
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
                        string filePath = StringEscaper.Preprocess(s, c.Arg1);

                        // Check filePath contains wildcard
                        bool containsWildcard = Path.GetFileName(filePath)?.IndexOfAny(new char[] { '*', '?' }) != -1;

                        // Check if file exists
                        if (filePath.Trim().Equals(string.Empty, StringComparison.Ordinal))
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
                        string dirPath = StringEscaper.Preprocess(s, c.Arg1);

                        // Check filePath contains wildcard
                        bool containsWildcard = Path.GetFileName(dirPath)?.IndexOfAny(new char[] { '*', '?' }) != -1;

                        // Check if directory exists
                        if (dirPath.Trim().Equals(string.Empty, StringComparison.Ordinal))
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
                        string iniFile = StringEscaper.Preprocess(s, c.Arg1);
                        string section = StringEscaper.Preprocess(s, c.Arg2);

                        match = Ini.ContainsSection(iniFile, section);
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
                        string rootKey = StringEscaper.Preprocess(s, c.Arg1);
                        string subKey = StringEscaper.Preprocess(s, c.Arg2);

                        RegistryKey regRoot = RegistryHelper.ParseStringToRegKey(rootKey);
                        if (regRoot == null)
                            throw new InvalidOperationException($"Invalid registry root key [{rootKey}]");
                        using (RegistryKey regSubKey = regRoot.OpenSubKey(subKey))
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
                        string rootKey = StringEscaper.Preprocess(s, c.Arg1);
                        string subKey = StringEscaper.Preprocess(s, c.Arg2);
                        string valueName = StringEscaper.Preprocess(s, c.Arg3);

                        match = true;
                        RegistryKey regRoot = RegistryHelper.ParseStringToRegKey(rootKey);
                        if (regRoot == null)
                            throw new InvalidOperationException($"Invalid registry root key [{rootKey}]");
                        using (RegistryKey regSubKey = regRoot.OpenSubKey(subKey))
                        {
                            if (regSubKey == null)
                            {
                                match = false;
                            }
                            else
                            {
                                object value = regSubKey.GetValue(valueName);
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
                        string rootKey = StringEscaper.Preprocess(s, c.Arg1);
                        string subKey = StringEscaper.Preprocess(s, c.Arg2);
                        string valueName = StringEscaper.Preprocess(s, c.Arg3);
                        string searchStr = StringEscaper.Preprocess(s, c.Arg4);

                        match = false;
                        RegistryKey regRoot = RegistryHelper.ParseStringToRegKey(rootKey);
                        if (regRoot == null)
                            throw new InvalidOperationException($"Invalid registry root key [{rootKey}]");
                        using (RegistryKey regSubKey = regRoot.OpenSubKey(subKey))
                        {
                            if (regSubKey == null)
                            {
                                logMessage = $"Registry SubKey [{rootKey}\\{subKey}] does not exist";
                            }
                            else
                            {
                                object valueData = regSubKey.GetValue(valueName, null);
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
                        Variables.VarKeyType type = Variables.DetermineType(c.Arg1);
                        if (type == Variables.VarKeyType.Variable)
                        {
                            match = s.Variables.ContainsKey(Variables.TrimPercentMark(c.Arg1));
                            if (match)
                                logMessage = $"Variable [{c.Arg1}] exists";
                            else
                                logMessage = $"Variable [{c.Arg1}] does not exist";
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
                        string macroName = StringEscaper.Preprocess(s, c.Arg1);
                        match = s.Macro.MacroDict.ContainsKey(macroName) || s.Macro.LocalDict.ContainsKey(macroName);

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
                                    using (Wim wim = Wim.OpenWim(wimFile, OpenFlags.DEFAULT))
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
                                logMessage = $"Wim [{wimFile}] does not exist";
                            }
                        }

                        if (c.NotFlag)
                            match = !match;
                    }
                    break;
                case BranchConditionType.WimExistFile:
                    {
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
                                    using (Wim wim = Wim.OpenWim(wimFile, OpenFlags.DEFAULT))
                                    {
                                        bool isFile = false;
                                        CallbackStatus WimExistFileCallback(DirEntry dentry, object userData)
                                        {
                                            if ((dentry.Attributes & FileAttribute.DIRECTORY) == 0)
                                                isFile = true;

                                            return CallbackStatus.CONTINUE;
                                        }

                                        try
                                        {
                                            wim.IterateDirTree(imageIndex, filePath, IterateFlags.DEFAULT, WimExistFileCallback, null);

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
                                                case ErrorCode.INVALID_IMAGE:
                                                    logMessage = $"File [{filePath}] does not have image index [{imageIndex}]";
                                                    break;
                                                case ErrorCode.PATH_DOES_NOT_EXIST:
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
                                logMessage = $"Wim [{wimFile}] does not exist";
                            }
                        }

                        if (c.NotFlag)
                            match = !match;
                    }
                    break;
                case BranchConditionType.WimExistDir:
                    {
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
                                    using (Wim wim = Wim.OpenWim(wimFile, OpenFlags.DEFAULT))
                                    {
                                        bool isDir = false;
                                        CallbackStatus WimExistFileCallback(DirEntry dentry, object userData)
                                        {
                                            if ((dentry.Attributes & FileAttribute.DIRECTORY) != 0)
                                                isDir = true;

                                            return CallbackStatus.CONTINUE;
                                        }

                                        try
                                        {
                                            wim.IterateDirTree(imageIndex, dirPath, IterateFlags.DEFAULT, WimExistFileCallback, null);

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
                                                case ErrorCode.INVALID_IMAGE:
                                                    logMessage = $"Dir [{dirPath}] does not have image index [{imageIndex}]";
                                                    break;
                                                case ErrorCode.PATH_DOES_NOT_EXIST:
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
                                logMessage = $"Wim [{wimFile}] does not exist";
                            }
                        }

                        if (c.NotFlag)
                            match = !match;
                    }
                    break;
                case BranchConditionType.WimExistImageInfo:
                    {
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
                                    using (Wim wim = Wim.OpenWim(wimFile, OpenFlags.DEFAULT))
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
                                logMessage = $"Wim [{wimFile}] does not exist";
                            }
                        }

                        if (c.NotFlag)
                            match = !match;
                    }
                    break;
                case BranchConditionType.Ping:
                    {
                        string host = StringEscaper.Preprocess(s, c.Arg1);

                        try
                        {
                            using (Ping pinger = new Ping())
                            {
                                PingReply reply = pinger.Send(host);
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
                            logMessage = "System is online";
                        else
                            logMessage = "System is offline";

                        if (c.NotFlag)
                            match = !match;
                    }
                    break;
                case BranchConditionType.Question: // can have 1 or 3 argument
                    {
                        string message = StringEscaper.Preprocess(s, c.Arg1);

                        bool autoTimeout = c.Arg2 != null && c.Arg3 != null;

                        int timeout = 0;
                        bool defaultChoice = false;
                        if (autoTimeout)
                        {
                            string timeoutStr = StringEscaper.Preprocess(s, c.Arg2);
                            if (NumberHelper.ParseInt32(timeoutStr, out timeout) == false)
                                autoTimeout = false;
                            if (timeout <= 0)
                                autoTimeout = false;

                            string defaultChoiceStr = StringEscaper.Preprocess(s, c.Arg3);
                            if (defaultChoiceStr.Equals("True", StringComparison.OrdinalIgnoreCase))
                                defaultChoice = true;
                            else if (defaultChoiceStr.Equals("False", StringComparison.OrdinalIgnoreCase))
                                defaultChoice = false;
                        }

                        System.Windows.Shell.TaskbarItemProgressState oldTaskbarItemProgressState = s.MainViewModel.TaskbarProgressState; // Save our progress state
                        s.MainViewModel.TaskbarProgressState = System.Windows.Shell.TaskbarItemProgressState.Paused;

                        if (autoTimeout)
                        {
                            MessageBoxResult result = MessageBoxResult.None;
                            Application.Current?.Dispatcher.Invoke(() =>
                            {
                                result = CustomMessageBox.Show(message, "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question, timeout);
                            });

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
                            MessageBoxResult result = MessageBox.Show(message, "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
                            switch (result)
                            {
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

                        if (c.NotFlag)
                            match = !match;

                        s.MainViewModel.TaskbarProgressState = oldTaskbarItemProgressState;
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
