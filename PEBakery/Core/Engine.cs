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
using System.Windows;
using System.ComponentModel;
using System.Threading;
using PEBakery.Helper;
using PEBakery.Exceptions;
using PEBakery.Core.Commands;
using PEBakery.WPF;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Net;

namespace PEBakery.Core
{
    #region Engine
    public class Engine
    {
        #region Variables and Constructor
        public static Engine WorkingEngine; // Only 1 Instance can run at one time
        public static int WorkingLock = 0;
        public static bool StopBuildOnError = true;

        public EngineState s;
        private Task<long> task;

        public Engine(EngineState state)
        {
            s = state;
        }
        #endregion

        #region Ready, Finish Script
        /// <summary>
        /// Ready to run an script
        /// </summary>
        internal static void ReadyRunScript(EngineState s, Script sc = null)
        {
            // Turn off System,ErrorOff
            s.ErrorOffStartLineIdx = -1;
            s.ErrorOffLineCount = 0;
            // Turn off System,Log,Off
            s.Logger.SuspendLog = false;

            // Assert s.CurDepth == 1
            Debug.Assert(s.CurDepth == 1);

            // Set CurrentScript
            // Note: s.CurrentScriptIdx is not touched here
            if (sc == null)
                sc = s.CurrentScript;
            else
                s.CurrentScript = sc;

            // Init Per-Script Log
            s.ScriptId = s.Logger.Build_Script_Init(s, s.CurrentScript, s.CurrentScriptIdx + 1);

            // Determine EntrySection
            string entrySection = Engine.GetEntrySection(s);

            // Log Script Build Start Message
            string msg;
            if (s.RunMode == EngineMode.RunAll)
                msg = $"[{s.CurrentScriptIdx + 1}/{s.Scripts.Count}] Processing script [{sc.Title}] ({sc.TreePath})";
            else
                msg = $"[{s.CurrentScriptIdx + 1}/{s.Scripts.Count}] Processing section [{entrySection}] of script [{sc.TreePath}]";
            s.Logger.Build_Write(s, msg);
            s.Logger.Build_Write(s, Logger.LogSeperator);

            // Load Default Per-Script Variables
            s.Variables.ResetVariables(VarsType.Local);
            s.Logger.Build_Write(s, s.Variables.LoadDefaultScriptVariables(sc));

            // Load Per-Script Macro
            s.Macro.ResetLocalMacros();
            s.Logger.Build_Write(s, s.Macro.LoadLocalMacroDict(sc, false));

            // Reset Current Section Parameter
            s.CurSectionParams = new Dictionary<int, string>();

            // Clear Processed Section Hashes
            s.ProcessedSectionHashes.Clear();

            // Set Interface using MainWindow, MainViewModel
            if (s.RunMode == EngineMode.RunAll)
                s.MainViewModel.ScriptTitleText = $"({s.CurrentScriptIdx + 1}/{s.Scripts.Count}) {StringEscaper.Unescape(sc.Title)}";
            else 
                s.MainViewModel.ScriptTitleText = StringEscaper.Unescape(sc.Title);
                
            s.MainViewModel.ScriptDescriptionText = StringEscaper.Unescape(sc.Description);
            s.MainViewModel.ScriptVersionText = "v" + sc.Version;
            if (MainWindow.ScriptAuthorLenLimit < sc.Author.Length)
                s.MainViewModel.ScriptAuthorText = sc.Author.Substring(0, MainWindow.ScriptAuthorLenLimit) + "...";
            else
                s.MainViewModel.ScriptAuthorText = sc.Author;
            s.MainViewModel.BuildEchoMessage = $"Processing Section [{entrySection}]...";

            long allLineCount = 0;
            foreach (var kv in s.CurrentScript.Sections.Where(x => x.Value.Type == SectionType.Code))
                allLineCount += kv.Value.Lines.Count; // Why not Codes? PEBakery compiles code on-demand, so we have only Lines at this time.

            s.MainViewModel.BuildScriptProgressBarMax = allLineCount;
            s.MainViewModel.BuildScriptProgressBarValue = 0;
            s.MainViewModel.BuildFullProgressBarValue = s.CurrentScriptIdx;

            if (Application.Current != null)
            {
                Application.Current.Dispatcher.BeginInvoke((Action)(() =>
                {
                    MainWindow w = Application.Current.MainWindow as MainWindow;

                    w.DrawScriptLogo(sc);

                    if (w.CurBuildTree != null)
                        w.CurBuildTree.BuildFocus = false;

                    w.CurBuildTree = s.MainViewModel.BuildTree.FindScriptByFullPath(s.CurrentScript.RealPath);

                    if (w.CurBuildTree != null)
                        w.CurBuildTree.BuildFocus = true;
                }));
            }
        }

        private static void FinishRunScript(EngineState s)
        {
            // Finish Per-PScript Log
            s.Logger.Build_Write(s, $"End of Script [{s.CurrentScript.TreePath}]");
            s.Logger.Build_Write(s, Logger.LogSeperator);
            s.Logger.Build_Script_Finish(s, s.Variables.GetVarDict(VarsType.Local));
        }
        #endregion

        #region Run, Stop 
        public Task<long> Run(string runName)
        {
            task = Task.Run(() =>
            {
                s.BuildId = s.Logger.Build_Init(s, runName);

                s.MainViewModel.BuildFullProgressBarMax = s.Scripts.Count;
                
                // Update project variables
                s.Project.UpdateProjectVariables();

                while (true)
                {
                    ReadyRunScript(s);

                    // Run Main Section
                    string entrySection = Engine.GetEntrySection(s);
                    if (s.CurrentScript.Sections.ContainsKey(entrySection))
                    {
                        ScriptSection mainSection = s.CurrentScript.Sections[entrySection];
                        SectionAddress addr = new SectionAddress(s.CurrentScript, mainSection);
                        s.Logger.LogStartOfSection(s, addr, 0, true, null, null);
                        Engine.RunSection(s, new SectionAddress(s.CurrentScript, mainSection), new List<string>(), 1, false);
                        s.Logger.LogEndOfSection(s, addr, 0, true, null);
                    }

                    // End of Script
                    FinishRunScript(s);

                    if(s.ErrorHaltFlag || s.UserHaltFlag || s.CmdHaltFlag)
                        s.MainViewModel.TaskbarProgressState = System.Windows.Shell.TaskbarItemProgressState.Error;

                    // OnScriptExit event callback
                    {
                        bool bakPassCurrentScriptFlag = s.PassCurrentScriptFlag;
                        bool bakErrorHalt = s.ErrorHaltFlag;
                        bool bakUserHalt = s.UserHaltFlag;
                        bool bakCmdHalt = s.CmdHaltFlag;

                        string eventParam = FinishEventParam(s);

                        // Reset Halt Flags before running OnScriptExit
                        // Otherwise only first command is executed
                        s.PassCurrentScriptFlag = false;
                        s.ErrorHaltFlag = false;
                        s.UserHaltFlag = false;
                        s.CmdHaltFlag = false;

                        Engine.CheckAndRunCallback(s, ref s.OnScriptExit, eventParam, "OnScriptExit");

                        s.PassCurrentScriptFlag = bakPassCurrentScriptFlag;
                        s.ErrorHaltFlag = bakErrorHalt;
                        s.UserHaltFlag = bakUserHalt;
                        s.CmdHaltFlag = bakCmdHalt;
                    }

                    bool runOneScriptExit = false;
                    if (s.RunMode == EngineMode.RunMainAndOne && s.CurrentScriptIdx != 0)
                        runOneScriptExit = true;
                    else if (s.RunMode == EngineMode.RunOne)
                        runOneScriptExit = true;

                    if (s.Scripts.Count - 1 <= s.CurrentScriptIdx ||
                        runOneScriptExit || s.ErrorHaltFlag || s.UserHaltFlag || s.CmdHaltFlag)
                    { // End of Build
                        bool alertPassCurrentScriptFlag = s.PassCurrentScriptFlag;
                        bool alertErrorHalt = s.ErrorHaltFlag;
                        bool alertUserHalt = s.UserHaltFlag;
                        bool alertCmdHalt = s.CmdHaltFlag;

                        if (s.UserHaltFlag)
                        {
                            s.MainViewModel.ScriptDescriptionText = "Build stop requested by user";
                            s.Logger.Build_Write(s, Logger.LogSeperator);
                            s.Logger.Build_Write(s, new LogInfo(LogState.Info, "Build stop requested by user"));
                        }

                        string eventParam = FinishEventParam(s);

                        // Reset Halt Flags before running OnBuildExit
                        // Otherwise only first command is executed
                        s.PassCurrentScriptFlag = false;
                        s.ErrorHaltFlag = false;
                        s.UserHaltFlag = false;
                        s.CmdHaltFlag = false;

                        // OnBuildExit event callback
                        if (s.RunMode == EngineMode.RunAll)
                        {
                            // OnBuildExit is not called on script interface control, or codebox
                            // (which uses EngineMode.RunMainAndOne or EngineMode.RunOne)
                            Engine.CheckAndRunCallback(s, ref s.OnBuildExit, eventParam, "OnBuildExit", true);
                        }

                        if (alertUserHalt)
                            MessageBox.Show("Build Stopped by User", "Build Halt", MessageBoxButton.OK, MessageBoxImage.Information);
                        else if (alertErrorHalt)
                            MessageBox.Show("Build Stopped by Error", "Build Halt", MessageBoxButton.OK, MessageBoxImage.Information);
                        else if (alertCmdHalt)
                            MessageBox.Show("Build Stopped by Halt Command", "Build Halt", MessageBoxButton.OK, MessageBoxImage.Information);
                        else if (alertPassCurrentScriptFlag)
                            MessageBox.Show("Build Stopped by Exit Command", "Build Halt", MessageBoxButton.OK, MessageBoxImage.Information);

                        break;
                    }

                    // Run Next Script
                    s.CurrentScriptIdx += 1;
                    s.CurrentScript = s.Scripts[s.CurrentScriptIdx];
                    s.PassCurrentScriptFlag = false;
                }

                s.Logger.Build_Finish(s);

                return s.BuildId;
            });

            return task;
        }

        public void ForceStop()
        {
            s.MainViewModel.TaskbarProgressState = System.Windows.Shell.TaskbarItemProgressState.Error;
            s.RunningSubProcess?.Kill();
            s.RunningWebClient?.CancelAsync();
            s.UserHaltFlag = true;
        }

        public Task ForceStopWait()
        {
            ForceStop();
            return Task.Run(() => task.Wait());
        }
        #endregion

        #region RunSection
        public static void RunSection(EngineState s, SectionAddress addr, List<string> sectionParams, int depth, bool callback)
        {
            List<CodeCommand> codes = addr.Section.GetCodes(true);
            s.Logger.Build_Write(s, LogInfo.AddDepth(addr.Section.LogInfos, s.CurDepth + 1));

            // Set CurrentSection
            s.CurrentSection = addr.Section;

            // Set SectionReturnValue to empty string
            s.SectionReturnValue = string.Empty;

            Dictionary<int, string> paramDict = new Dictionary<int, string>();
            for (int i = 0; i < sectionParams.Count; i++)
                paramDict[i + 1] = StringEscaper.ExpandSectionParams(s, sectionParams[i]);

            RunCommands(s, addr, codes, paramDict, depth, callback);

            // Increase only if cmd resides in CurrentScript
            if (s.CurrentScript.Equals(addr.Script))
                s.ProcessedSectionHashes.Add(addr.Section.GetHashCode());
        }

        public static void RunSection(EngineState s, SectionAddress addr, Dictionary<int, string> paramDict, int depth, bool callback)
        {
            List<CodeCommand> codes = addr.Section.GetCodes(true);
            s.Logger.Build_Write(s, LogInfo.AddDepth(addr.Section.LogInfos, s.CurDepth + 1));

            // Set CurrentSection
            s.CurrentSection = addr.Section;

            // Set SectionReturnValue to empty string
            s.SectionReturnValue = string.Empty;

            // Must copy ParamDict by value, not reference
            RunCommands(s, addr, codes, new Dictionary<int, string>(paramDict), depth, callback);

            // Increase only if cmd resides is CurrentScript
            if (s.CurrentScript.Equals(addr.Script))
                s.ProcessedSectionHashes.Add(addr.Section.GetHashCode());
        }
        #endregion

        #region RunCommands, RunCallback
        public static void RunCommands(EngineState s, SectionAddress addr, List<CodeCommand> codes, Dictionary<int, string> sectionParams, int depth, bool callback = false)
        {
            if (codes.Count == 0)
            {
                s.Logger.Build_Write(s, new LogInfo(LogState.Warning, $"No code in [{addr.Script.TreePath}]::[{addr.Section.SectionName}]", s.CurDepth + 1));
                return;
            }

            for (int idx = 0; idx < codes.Count; idx++)
            {
                try
                {
                    var curCommand = codes[idx];
                    s.CurDepth = depth;
                    s.CurSectionParams = sectionParams;
                    ExecuteCommand(s, curCommand);

                    if (s.PassCurrentScriptFlag || s.ErrorHaltFlag || s.UserHaltFlag || s.CmdHaltFlag)
                        break;
                }
                catch (CriticalErrorException)
                { // Critical Error, stop build
                    s.ErrorHaltFlag = true;
                    break;
                }
            }

            DisableSetLocal(s, addr.Section);
        }
        #endregion

        #region ExecuteCommand
        public static List<LogInfo> ExecuteCommand(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();
            int curDepth = s.CurDepth;

            if (CodeCommand.DeprecatedCodeType.Contains(cmd.Type))
                logs.Add(new LogInfo(LogState.Warning, $"Command [{cmd.Type}] is deprecated"));

            try
            {
                switch (cmd.Type)
                {
                    #region 00 Misc
                    case CodeType.None:
                        logs.Add(new LogInfo(LogState.Ignore, string.Empty));
                        break;
                    case CodeType.Comment:
                        {
                            if (s.LogComment)
                                logs.Add(new LogInfo(LogState.Ignore, string.Empty));
                        }
                        break;
                    case CodeType.Error:
                        {
                            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Error));
                            CodeInfo_Error info = cmd.Info as CodeInfo_Error;

                            logs.Add(new LogInfo(LogState.Error, info.ErrorMessage));
                        }
                        break;
                    #endregion
                    #region 01 File
                    case CodeType.FileCopy:
                        logs.AddRange(CommandFile.FileCopy(s, cmd));
                        break;
                    case CodeType.FileDelete:
                        logs.AddRange(CommandFile.FileDelete(s, cmd));
                        break;
                    case CodeType.FileRename:
                    case CodeType.FileMove:
                        logs.AddRange(CommandFile.FileRename(s, cmd));
                        break;
                    case CodeType.FileCreateBlank:
                        logs.AddRange(CommandFile.FileCreateBlank(s, cmd));
                        break;
                    case CodeType.FileSize:
                        logs.AddRange(CommandFile.FileSize(s, cmd));
                        break;
                    case CodeType.FileVersion:
                        logs.AddRange(CommandFile.FileVersion(s, cmd));
                        break;
                    case CodeType.DirCopy:
                        logs.AddRange(CommandFile.DirCopy(s, cmd));
                        break;
                    case CodeType.DirDelete:
                        logs.AddRange(CommandFile.DirDelete(s, cmd));
                        break;
                    case CodeType.DirMove:
                        logs.AddRange(CommandFile.DirMove(s, cmd));
                        break;
                    case CodeType.DirMake:
                        logs.AddRange(CommandFile.DirMake(s, cmd));
                        break;
                    case CodeType.DirSize:
                        logs.AddRange(CommandFile.DirSize(s, cmd));
                        break;
                    case CodeType.PathMove:
                        logs.AddRange(CommandFile.PathMove(s, cmd));
                        break;
                    #endregion
                    #region 02 Registry
                    case CodeType.RegHiveLoad:
                        logs.AddRange(CommandRegistry.RegHiveLoad(s, cmd));
                        break;
                    case CodeType.RegHiveUnload:
                        logs.AddRange(CommandRegistry.RegHiveUnload(s, cmd));
                        break;
                    case CodeType.RegRead:
                        logs.AddRange(CommandRegistry.RegRead(s, cmd));
                        break;
                    case CodeType.RegWrite:
                        logs.AddRange(CommandRegistry.RegWrite(s, cmd));
                        break;
                    case CodeType.RegWriteLegacy: // WB082 Compatibility Shim
                        logs.AddRange(CommandRegistry.RegWriteLegacy(s, cmd));
                        break;
                    case CodeType.RegDelete:
                        logs.AddRange(CommandRegistry.RegDelete(s, cmd));
                        break;
                    case CodeType.RegMulti:
                        logs.AddRange(CommandRegistry.RegMulti(s, cmd));
                        break;
                    case CodeType.RegImport:
                        logs.AddRange(CommandRegistry.RegImport(s, cmd));
                        break;
                    case CodeType.RegExport:
                        logs.AddRange(CommandRegistry.RegExport(s, cmd));
                        break;
                    #endregion
                    #region 03 Text
                    case CodeType.TXTAddLine:
                        logs.AddRange(CommandText.TXTAddLine(s, cmd));
                        break;
                    case CodeType.TXTAddLineOp:
                        logs.AddRange(CommandText.TXTAddLineOp(s, cmd));
                        break;
                    case CodeType.TXTReplace:
                        logs.AddRange(CommandText.TXTReplace(s, cmd));
                        break;
                    case CodeType.TXTReplaceOp:
                        logs.AddRange(CommandText.TXTReplaceOp(s, cmd));
                        break;
                    case CodeType.TXTDelLine:
                        logs.AddRange(CommandText.TXTDelLine(s, cmd));
                        break;
                    case CodeType.TXTDelLineOp:
                        logs.AddRange(CommandText.TXTDelLineOp(s, cmd));
                        break;
                    case CodeType.TXTDelSpaces:
                        logs.AddRange(CommandText.TXTDelSpaces(s, cmd));
                        break;
                    case CodeType.TXTDelEmptyLines:
                        logs.AddRange(CommandText.TXTDelEmptyLines(s, cmd));
                        break;
                    #endregion
                    #region 04 INI
                    case CodeType.INIRead:
                        logs.AddRange(CommandIni.IniRead(s, cmd));
                        break;
                    case CodeType.INIReadOp:
                        logs.AddRange(CommandIni.IniReadOp(s, cmd));
                        break;
                    case CodeType.INIWrite:
                        logs.AddRange(CommandIni.IniWrite(s, cmd));
                        break;
                    case CodeType.INIWriteOp:
                        logs.AddRange(CommandIni.IniWriteOp(s, cmd));
                        break;
                    case CodeType.INIDelete:
                        logs.AddRange(CommandIni.IniDelete(s, cmd));
                        break;
                    case CodeType.INIDeleteOp:
                        logs.AddRange(CommandIni.IniDeleteOp(s, cmd));
                        break;
                    case CodeType.INIReadSection:
                        logs.AddRange(CommandIni.IniReadSection(s, cmd));
                        break;
                    case CodeType.INIReadSectionOp:
                        logs.AddRange(CommandIni.IniReadSectionOp(s, cmd));
                        break;
                    case CodeType.INIAddSection:
                        logs.AddRange(CommandIni.IniAddSection(s, cmd));
                        break;
                    case CodeType.INIAddSectionOp:
                        logs.AddRange(CommandIni.IniAddSectionOp(s, cmd));
                        break;
                    case CodeType.INIDeleteSection:
                        logs.AddRange(CommandIni.IniDeleteSection(s, cmd));
                        break;
                    case CodeType.INIDeleteSectionOp:
                        logs.AddRange(CommandIni.IniDeleteSectionOp(s, cmd));
                        break;
                    case CodeType.INIWriteTextLine:
                        logs.AddRange(CommandIni.IniWriteTextLine(s, cmd));
                        break;
                    case CodeType.INIWriteTextLineOp:
                        logs.AddRange(CommandIni.IniWriteTextLineOp(s, cmd));
                        break;
                    case CodeType.INIMerge:
                        logs.AddRange(CommandIni.IniMerge(s, cmd));
                        break;
                    #endregion
                    #region 05 Archive
                    case CodeType.Compress:
                        logs.AddRange(CommandArchive.Compress(s, cmd));
                        break;
                    case CodeType.Decompress:
                        logs.AddRange(CommandArchive.Decompress(s, cmd));
                        break;
                    case CodeType.Expand:
                        logs.AddRange(CommandArchive.Expand(s, cmd));
                        break;
                    case CodeType.CopyOrExpand:
                        logs.AddRange(CommandArchive.CopyOrExpand(s, cmd));
                        break;
                    #endregion
                    #region 06 Network
                    case CodeType.WebGet:
                    case CodeType.WebGetIfNotExist: // Deprecated
                        logs.AddRange(CommandNetwork.WebGet(s, cmd));
                        break;
                    #endregion
                    #region 07 Script
                    case CodeType.ExtractFile:
                        logs.AddRange(CommandScript.ExtractFile(s, cmd));
                        break;
                    case CodeType.ExtractAndRun:
                        logs.AddRange(CommandScript.ExtractAndRun(s, cmd));
                        break;
                    case CodeType.ExtractAllFiles:
                        logs.AddRange(CommandScript.ExtractAllFiles(s, cmd));
                        break;
                    case CodeType.Encode:
                        logs.AddRange(CommandScript.Encode(s, cmd));
                        break;
                    #endregion
                    #region 08 Interface
                    case CodeType.Visible:
                        logs.AddRange(CommandInterface.Visible(s, cmd));
                        break;
                    case CodeType.VisibleOp:
                        logs.AddRange(CommandInterface.VisibleOp(s, cmd));
                        break;
                    case CodeType.ReadInterface:
                        logs.AddRange(CommandInterface.ReadInterface(s, cmd));
                        break;
                    case CodeType.WriteInterface:
                        logs.AddRange(CommandInterface.WriteInterface(s, cmd));
                        break;
                    case CodeType.Message:
                        logs.AddRange(CommandInterface.Message(s, cmd));
                        break;
                    case CodeType.Echo:
                        logs.AddRange(CommandInterface.Echo(s, cmd));
                        break;
                    case CodeType.EchoFile:
                        logs.AddRange(CommandInterface.EchoFile(s, cmd));
                        break;
                    case CodeType.UserInput:
                        logs.AddRange(CommandInterface.UserInput(s, cmd));
                        break;
                    case CodeType.AddInterface:
                        logs.AddRange(CommandInterface.AddInterface(s, cmd));
                        break;
                    #endregion
                    #region 09 Hash
                    case CodeType.Hash:
                        logs.AddRange(CommandHash.Hash(s, cmd));
                        break;
                    #endregion
                    #region 10 String
                    case CodeType.StrFormat:
                        logs.AddRange(CommandString.StrFormat(s, cmd));
                        break;
                    #endregion
                    #region 11 Math
                    case CodeType.Math:
                        logs.AddRange(CommandMath.Math(s, cmd));
                        break;
                    #endregion
                    #region 12 Wim
                    case CodeType.WimMount:
                        logs.AddRange(CommandWim.WimMount(s, cmd));
                        break;
                    case CodeType.WimUnmount:
                        logs.AddRange(CommandWim.WimUnmount(s, cmd));
                        break;
                    case CodeType.WimInfo:
                        logs.AddRange(CommandWim.WimInfo(s, cmd));
                        break;
                    case CodeType.WimApply:
                        logs.AddRange(CommandWim.WimApply(s, cmd));
                        break;
                    case CodeType.WimExtract:
                        logs.AddRange(CommandWim.WimExtract(s, cmd));
                        break;
                    case CodeType.WimExtractOp:
                        break;
                    case CodeType.WimExtractBulk:
                        logs.AddRange(CommandWim.WimExtractBulk(s, cmd));
                        break;
                    case CodeType.WimCapture:
                        logs.AddRange(CommandWim.WimCapture(s, cmd));
                        break;
                    case CodeType.WimAppend:
                        logs.AddRange(CommandWim.WimAppend(s, cmd));
                        break;
                    case CodeType.WimDelete:
                        logs.AddRange(CommandWim.WimDelete(s, cmd));
                        break;
                    case CodeType.WimPathAdd:
                        logs.AddRange(CommandWim.WimPathAdd(s, cmd));
                        break;
                    case CodeType.WimPathDelete:
                        logs.AddRange(CommandWim.WimPathDelete(s, cmd));
                        break;
                    case CodeType.WimPathRename:
                        logs.AddRange(CommandWim.WimPathRename(s, cmd));
                        break;
                    case CodeType.WimPathOp:
                        break;
                    case CodeType.WimOptimize:
                        logs.AddRange(CommandWim.WimOptimize(s, cmd));
                        break;
                    case CodeType.WimExport:
                        logs.AddRange(CommandWim.WimExport(s, cmd));
                        break;
                    #endregion
                    #region 80 Branch
                    case CodeType.Run:
                    case CodeType.Exec:
                        CommandBranch.RunExec(s, cmd);
                        break;
                    case CodeType.Loop:
                    case CodeType.LoopLetter:
                        CommandBranch.Loop(s, cmd);
                        break;
                    case CodeType.If:
                        CommandBranch.If(s, cmd);
                        break;
                    case CodeType.Else:
                        CommandBranch.Else(s, cmd);
                        break;
                    case CodeType.Begin:
                        throw new InternalParserException("CodeParser Error");
                    case CodeType.End:
                        throw new InternalParserException("CodeParser Error");
                    #endregion
                    #region 81 Control
                    case CodeType.Set:
                        logs.AddRange(CommandControl.Set(s, cmd));
                        break;
                    case CodeType.SetMacro:
                        logs.AddRange(CommandControl.SetMacro(s, cmd));
                        break;
                    case CodeType.AddVariables:
                        logs.AddRange(CommandControl.AddVariables(s, cmd));
                        break;
                    case CodeType.Exit:
                        logs.AddRange(CommandControl.Exit(s, cmd));
                        break;
                    case CodeType.Halt:
                        logs.AddRange(CommandControl.Halt(s, cmd));
                        break;
                    case CodeType.Wait:
                        logs.AddRange(CommandControl.Wait(s, cmd));
                        break;
                    case CodeType.Beep:
                        logs.AddRange(CommandControl.Beep(s, cmd));
                        break;
                    case CodeType.GetParam:
                        logs.AddRange(CommandControl.GetParam(s, cmd));
                        break;
                    case CodeType.PackParam:
                        logs.AddRange(CommandControl.PackParam(s, cmd));
                        break;
                    #endregion
                    #region 82 System
                    case CodeType.System:
                        logs.AddRange(CommandSystem.SystemCmd(s, cmd));
                        break;
                    case CodeType.ShellExecute:
                    case CodeType.ShellExecuteEx:
                    case CodeType.ShellExecuteDelete:
                        logs.AddRange(CommandSystem.ShellExecute(s, cmd));
                        break;
                    #endregion
                    #region 99 External Macro
                    case CodeType.Macro:
                        CommandMacro.Macro(s, cmd);
                        break;
                    #endregion
                    #region Error
                    // Error
                    default:
                        logs.Add(new LogInfo(LogState.Error, $"Cannot execute [{cmd.Type}] command"));
                        break;
                        #endregion
                }
            }
            catch (CriticalErrorException)
            { // Stop Building
                // Currently not used
                logs.Add(new LogInfo(LogState.CriticalError, "Critical Error!", cmd, curDepth));
                throw new CriticalErrorException();
            }
            catch (InvalidCodeCommandException e)
            {
                logs.Add(new LogInfo(LogState.Error, e, e.Cmd, curDepth));
            }
            catch (Exception e)
            {
                logs.Add(new LogInfo(LogState.Error, e, cmd, curDepth));
            }

            // If ErrorOffCount is on, ignore LogState.Error and LogState.Warning
            ProcessErrorOff(s, cmd, logs);

            // Stop build on error
            if (StopBuildOnError)
            {
                if (0 < logs.Count(x => x.State == LogState.Error))
                    s.ErrorHaltFlag = true;
            }

            s.Logger.Build_Write(s, LogInfo.AddCommandDepth(logs, cmd, curDepth));

            // Increase only if cmd resides in CurrentScript.
            // So if a setion is from Macro, it will not be count.
            if (!s.ProcessedSectionHashes.Contains(cmd.Addr.Section.GetHashCode()) && s.CurrentScript.Equals(cmd.Addr.Script))
                s.MainViewModel.BuildScriptProgressBarValue += 1;

            // Return logs, used in unit test
            return logs; 
        }

        private static void CheckAndRunCallback(EngineState s, ref CodeCommand cbCmd, string eventParam, string eventName, bool changeCurrentScript = false)
        {
            if (cbCmd == null)
                return;
            
            s.Logger.Build_Write(s, $"Processing callback of event [{eventName}]");

            if (changeCurrentScript)
                s.CurrentScript = cbCmd.Addr.Script;

            s.CurDepth = 0;
            if (cbCmd.Type == CodeType.Run || cbCmd.Type == CodeType.Exec)
            {
                Debug.Assert(cbCmd.Info.GetType() == typeof(CodeInfo_RunExec));
                CodeInfo_RunExec info = cbCmd.Info as CodeInfo_RunExec;
                if (1 <= info.Parameters.Count)
                    info.Parameters[0] = eventParam;
                else
                    info.Parameters.Add(eventParam);

                CommandBranch.RunExec(s, cbCmd, false, false, true);
            }
            else
            {
                ExecuteCommand(s, cbCmd);
            }

            s.Logger.Build_Write(s, new LogInfo(LogState.Info, $"End of callback [{eventName}]", s.CurDepth));
            s.Logger.Build_Write(s, Logger.LogSeperator);
            cbCmd = null;
        }

        public string FinishEventParam(EngineState s)
        {
            if (s.UserHaltFlag)
                return "STOP";
            else if (s.ErrorHaltFlag)
                return "ERROR";
            else if (s.CmdHaltFlag || s.PassCurrentScriptFlag)
                return "COMMAND";
            else
                return "DONE";
        }
        #endregion

        #region Utility Methods
        public static void ProcessErrorOff(EngineState s, CodeCommand cmd, List<LogInfo> logs)
        {
            /*
            if (s.ErrorOffSection != null &&
                0 <= s.ErrorOffStartLineIdx &&
                s.ErrorOffStartLineIdx <= cmd.LineIdx &&
                cmd.LineIdx < s.ErrorOffStartLineIdx + s.ErrorOffLineCount)
            {
                MuteLogError(logs);
                if (s.ErrorOffSection.Equals(cmd.Addr.Section) &&
                    cmd.LineIdx + 1 == s.ErrorOffStartLineIdx + s.ErrorOffLineCount)
                {
                    s.ErrorOffStartLineIdx = -1;
                    if (s.ErrorOffStartLineIdx == 0)
                        s.ErrorOffSection = null;
                }
            }
            */

            if (s.ErrorOffSection != null &&
                s.ErrorOffSection.Equals(cmd.Addr.Section) &&
                0 <= s.ErrorOffStartLineIdx &&
                s.ErrorOffStartLineIdx <= cmd.LineIdx &&
                cmd.LineIdx < s.ErrorOffStartLineIdx + s.ErrorOffLineCount)
            {
                MuteLogError(logs);
                if (cmd.LineIdx + 1 == s.ErrorOffStartLineIdx + s.ErrorOffLineCount)
                    s.ErrorOffStartLineIdx = -1;
            }
        }

        private static void MuteLogError(List<LogInfo> logs)
        {
            for (int i = 0; i < logs.Count; i++)
            {
                LogInfo log = logs[i];
                if (log.State == LogState.Error || log.State == LogState.Warning)
                {
                    log.State = LogState.Muted;
                    logs[i] = log;
                }
            }
        }

        public static string GetEntrySection(EngineState s)
        {
            string entrySection = "Process";
            if (s.RunMode == EngineMode.RunMainAndOne && s.CurrentScriptIdx != 0)
                entrySection = s.RunOneEntrySection;
            else if (s.RunMode == EngineMode.RunOne)
                entrySection = s.RunOneEntrySection;

            return entrySection;
        }

        /// <summary>
        /// Get script instance from path string.
        /// </summary>
        public static Script GetScriptInstance(EngineState s, CodeCommand cmd, string currentScriptPath, string loadScriptPath, out bool inCurrentScript)
        {
            inCurrentScript = false;
            if (loadScriptPath.Equals(currentScriptPath, StringComparison.OrdinalIgnoreCase) ||
                loadScriptPath.Equals(Path.GetDirectoryName(currentScriptPath), StringComparison.OrdinalIgnoreCase))
                inCurrentScript = true; // Sometimes this value is not legal, so always use Project.GetScriptByFullPath.

            string fullPath = loadScriptPath;
            Script sc = s.Project.GetScriptByRealPath(fullPath);
            if (sc == null)
            { // Cannot Find Script in Project.AllScripts
                // Try searching s.Scripts
                sc = s.Scripts.Find(x => x.RealPath.Equals(fullPath, StringComparison.OrdinalIgnoreCase));
                if (sc == null)
                { // Still not found in s.Scripts
                    if (!File.Exists(fullPath))
                        throw new ExecuteException($"No script in [{fullPath}]");
                    sc = s.Project.LoadScriptMonkeyPatch(fullPath, false, true);
                    if (sc == null)
                        throw new ExecuteException($"Unable to load script [{fullPath}]");
                }
            }

            return sc;
        }

        public static void EnableSetLocal(EngineState s, ScriptSection section)
        {
            s.LocalVarsBackup = s.Variables.GetVarDict(VarsType.Local);
            s.SetLocalSection = section;
        }

        public static void DisableSetLocal(EngineState s, ScriptSection section)
        {
            if (s.SetLocalSection != null && s.SetLocalSection.Equals(section) &&
                s.LocalVarsBackup != null)
            {
                s.Variables.SetVarDict(VarsType.Local, s.LocalVarsBackup);
                s.LocalVarsBackup = null;
                s.SetLocalSection = null;
            }
        }
        #endregion
    }
    #endregion

    #region EngineState
    public enum EngineMode
    {
        RunAll,
        RunMainAndOne,
        RunOne,
    }

    public enum LoopState
    {
        Off,
        OnIndex,
        OnDriveLetter,
    }

    public class EngineState
    {
        #region Field and Properties
        public Project Project;
        public List<Script> Scripts;
        public Variables Variables => Project.Variables;
        public Macro Macro;
        public Logger Logger;
        public EngineMode RunMode;
        public MainViewModel MainViewModel;

        // Property
        public string BaseDir => Project.BaseDir;
        public Script MainScript => Project.MainScript;
        public int CurSectionParamsCount
        {
            get
            {
                if (0 < CurSectionParams.Count)
                    return CurSectionParams.Keys.Max();
                else
                    return 0;
            }
        }

        // Engine's state
        public Script CurrentScript;
        public int CurrentScriptIdx;
        public ScriptSection CurrentSection;
        public Dictionary<int, string> CurSectionParams;
        public string SectionReturnValue = string.Empty;
        public List<int> ProcessedSectionHashes = new List<int>();
        public int CurDepth = 1;
        public bool ElseFlag = false;
        public LoopState LoopState = LoopState.Off;
        public long LoopCounter = 0;
        public char LoopLetter = ' ';
        public Dictionary<string, string> LocalVarsBackup = null; // For System,SetLocal
        public bool InMacro = false;
        public bool PassCurrentScriptFlag = false; // Exit Command
        public bool ErrorHaltFlag = false; 
        public bool CmdHaltFlag = false; // Halt Command
        public bool UserHaltFlag = false;
        public long BuildId; // Used in logging
        public long ScriptId; // Used in logging

        // ErrorOff
        public ScriptSection ErrorOffSection = null;
        public int ErrorOffStartLineIdx = -1; // -1 means ErrorOff is turned off
        public int ErrorOffLineCount = 0;

        // ShellExecute
        public Process RunningSubProcess = null;
        // System,SetLocal
        public ScriptSection SetLocalSection = null; 
        // WebGet
        public WebClient RunningWebClient = null;

        // Options
        public bool LogComment = true; // Used in logging
        public bool LogMacro = true; // Used in logging
        public bool CompatDirCopyBug = false; // Compatibility
        public bool CompatFileRenameCanMoveDir = false; // Compatibility
        public bool DisableLogger = false; // For performance (when engine runned by interface)
        public bool DelayedLogging = true; // For performance

        // System Commands
        public CodeCommand OnBuildExit = null;
        public CodeCommand OnScriptExit = null;

        // Readonly Fields
        public readonly string RunOneEntrySection;
        #endregion

        public EngineState(Project project, Logger logger, MainViewModel mainModel, EngineMode mode = EngineMode.RunAll, Script runSingle = null, string entrySection = "Process")
        {
            this.Project = project;
            this.Logger = logger;

            Macro = new Macro(Project, Variables, out List<LogInfo> macroLogs);
            logger.Build_Write(BuildId, macroLogs);

            RunMode = mode;
            switch (RunMode)
            {
                case EngineMode.RunAll:
                    { // Run All
                        Scripts = project.ActiveScripts;

                        CurrentScript = Scripts[0]; // MainScript
                        CurrentScriptIdx = 0;

                        RunOneEntrySection = null;
                    }
                    break;
                case EngineMode.RunMainAndOne:
                    { // Run one script, executing MainScript before it.
                        if (runSingle == null)
                            throw new ArgumentNullException(nameof(runSingle));
                        if (runSingle.Equals(project.MainScript) && entrySection.Equals("Process", StringComparison.Ordinal))
                            goto case EngineMode.RunOne;

                        Scripts = new List<Script>(2) { project.MainScript, runSingle };

                        CurrentScript = Scripts[0];
                        CurrentScriptIdx = 0;

                        RunOneEntrySection = entrySection;
                    }
                    break;
                case EngineMode.RunOne:
                    { // Run only one script
                        Scripts = new List<Script>(1) { runSingle };

                        CurrentScript = runSingle;
                        CurrentScriptIdx = Scripts.IndexOf(runSingle);

                        RunOneEntrySection = entrySection;
                    }
                    break;
            }

            CurrentSection = null;
            CurSectionParams = new Dictionary<int, string>();
            MainViewModel = mainModel;
        }

        #region SetOption
        public void SetOption(SettingViewModel m)
        {
            LogComment = m.Log_Comment;
            LogMacro = m.Log_Macro;
            CompatDirCopyBug = m.Compat_AsteriskBugDirCopy;
            CompatFileRenameCanMoveDir = m.Compat_FileRenameCanMoveDir;
            DelayedLogging = m.Log_DelayedLogging;
        }
        #endregion
    }
    #endregion

    #region Exception
    [Serializable]
    public class ExecuteException : Exception
    {
        public ExecuteException() { }
        public ExecuteException(string message) : base(message) { }
        public ExecuteException(string message, Exception inner) : base(message, inner) { }
    }
    #endregion
}