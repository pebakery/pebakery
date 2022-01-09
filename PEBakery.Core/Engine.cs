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

using PEBakery.Core.Commands;
using PEBakery.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Shell;

namespace PEBakery.Core
{
    #region Engine
    public class Engine
    {
        #region Variables and Constructor
        private static bool _isRunning;
        public static bool IsRunning => _isRunning;

        public static Engine? WorkingEngine { get; set; } // Only 1 instance allowed to run at one time
        private static readonly object WorkingLock = new object();
        private static readonly object EnterLock = new object();

        private readonly EngineState s;
        public EngineState State => s;
        private Task<int>? _task;

        public static readonly string DefaultUserAgent = $"PEBakery/{Global.Const.ProgramVersionStr}";

        public Engine(EngineState state)
        {
            s = state;
        }
        #endregion

        #region Ready, Finish Script
        /// <summary>
        /// Ready to run an script
        /// </summary>
        internal static void ReadyRunScript(EngineState s, Script? sc = null)
        {
            // Turn off System,ErrorOff
            s.ErrorOff = null;
            // Turn off temporary log suspend
            s.Logger.SuspendBuildLog = false;

            Debug.Assert(s.PeekDepth == 0, "Incorrect DepthInfoStack handling");

            // Set CurrentScript
            // Note: s.CurrentScriptIdx is not changed here
            if (sc == null)
                sc = s.CurrentScript;
            else
                s.CurrentScript = sc;

            // Init Per-Script Log
            s.ScriptId = s.Logger.BuildScriptInit(s, s.CurrentScript, s.CurrentScriptIdx + 1);

            // Determine EntrySection
            string entrySection = GetEntrySection(s);

            // Log Script Build Start Message
            string msg;
            if (s.RunMode == EngineMode.RunAll)
                msg = $"[{s.CurrentScriptIdx + 1}/{s.Scripts.Count}] Processing script [{sc.Title}] ({sc.TreePath})";
            else
                msg = $"[{s.CurrentScriptIdx + 1}/{s.Scripts.Count}] Processing section [{entrySection}] of script [{sc.TreePath}]";
            s.Logger.BuildWrite(s, msg);
            s.Logger.BuildWrite(s, Logger.LogSeparator);

            // Load Default Per-Script Variables
            s.Variables.ResetVariables(VarsType.Local);
            s.Logger.BuildWrite(s, s.Variables.LoadDefaultScriptVariables(sc));

            // Load Per-Script Macro
            s.Macro.ResetMacroDict(MacroType.Local);
            s.Logger.BuildWrite(s, s.Macro.LoadMacroDict(MacroType.Local, sc, false));

            // Reset Current Section Parameters
            s.CurSectionInParams = new Dictionary<int, string>();
            s.CurSectionOutParams = null;

            // Clear Processed Section Hashes
            s.ProcessedSectionSet.Clear();

            // Set Interface using MainWindow, MainViewModel
            long allLineCount = 0;
            foreach (ScriptSection section in s.CurrentScript.Sections.Values.Where(x => x.Type == SectionType.Code))
            {
                Debug.Assert(section.Lines != null, "CodeSection should return proper \'Lines\' property");
                allLineCount += section.Lines.Length;
            }

            // Reset progress report
            s.ResetScriptProgress();
            s.ResetFullProgress();

            // Skip displaying script information when running a single script
            if (s.RunMode != EngineMode.RunAll && s.MainScript.Equals(s.CurrentScript) && s.CurrentScriptIdx == 0)
                return;

            // Display script information
            s.MainViewModel.DisplayScriptTexts(sc, s);
            s.MainViewModel.BuildEchoMessage = $"Processing Section [{entrySection}]...";
            Application.Current?.Dispatcher?.BeginInvoke((Action)(() =>
            {
                s.MainViewModel.DisplayScriptLogo(sc);

                // BuildTree is empty -> return
                if (s.MainViewModel.BuildTreeItems.Count == 0)
                    return;

                if (s.MainViewModel.CurBuildTree != null)
                    s.MainViewModel.CurBuildTree.Focus = false;
                s.MainViewModel.CurBuildTree = ProjectTreeItemModel.FindScriptByRealPath(s.MainViewModel.BuildTreeItems[0], s.CurrentScript.RealPath);
                if (s.MainViewModel.CurBuildTree != null)
                    s.MainViewModel.CurBuildTree.Focus = true;
            }));
        }

        private static void FinishRunScript(EngineState s)
        {
            // Finish Per-script Log
            s.Logger.BuildWrite(s, $"End of Script [{s.CurrentScript.TreePath}]");
            s.Logger.BuildWrite(s, Logger.LogSeparator);
            s.Logger.BuildScriptFinish(s, s.Variables.GetVarDict(VarsType.Local));
        }
        #endregion

        #region Run, Stop 
        public Task<int> Run(string runName)
        {
            _task = Task.Run(() =>
            {
                s.StartTime = DateTime.UtcNow;
                s.BuildId = s.Logger.BuildInit(s, runName);

                if (s.Macro.MacroEnabled && s.Macro.MacroScript is not null)
                    s.Logger.BuildRefScriptWrite(s, s.Macro.MacroScript, true);

                // Update project variables
                s.Project.UpdateProjectVariables();

                // Script execution loop
                while (true)
                {
                    // Prepare execution of a script
                    ReadyRunScript(s);

                    // Run Main Section
                    string entrySection = GetEntrySection(s);
                    if (s.CurrentScript.Sections.ContainsKey(entrySection))
                    {
                        ScriptSection mainSection = s.CurrentScript.Sections[entrySection];
                        s.Logger.LogStartOfSection(s, mainSection, 0, true, null, null);
                        RunSection(s, mainSection, new List<string>(0), new List<string>(0), new EngineLocalState());
                        s.Logger.LogEndOfSection(s, mainSection, 0, true, null);
                    }

                    // End execution of a script
                    FinishRunScript(s);

                    if (s.HaltFlags.CheckBuildHalt())
                        s.MainViewModel.TaskBarProgressState = TaskbarItemProgressState.Error;

                    // OnScriptExit event callback
                    {
                        // Backup halt flags
                        EngineHaltFlags bak = s.HaltFlags.Backup();

                        // Create event param to pass it to callback
                        string eventParam = s.HaltFlags.FinishCallbackEventParam();

                        // Reset Halt Flags before running OnScriptExit
                        // Otherwise only first command is executed
                        s.HaltFlags.Reset();

                        // Run `OnScriptExit` callback
                        CheckAndRunCallback(s, s.OnScriptExit, eventParam, "OnScriptExit");
                        s.OnScriptExit = null;

                        // Restore halt flags
                        s.HaltFlags.Restore(bak);
                    }

                    bool runOneScriptExit = false;
                    switch (s.RunMode)
                    {
                        case EngineMode.RunMainAndOne when s.CurrentScriptIdx != 0:
                        case EngineMode.RunOne:
                            runOneScriptExit = true;
                            break;
                    }

                    if (s.Scripts.Count - 1 <= s.CurrentScriptIdx ||
                        runOneScriptExit || s.HaltFlags.CheckBuildHalt())
                    { // End of Build
                        // Backup halt flags
                        EngineHaltFlags bak = s.HaltFlags.Backup();

                        if (s.HaltFlags.UserHalt)
                        {
                            s.MainViewModel.ScriptDescriptionText = "Build stop requested by user";
                            s.Logger.BuildWrite(s, Logger.LogSeparator);
                            s.Logger.BuildWrite(s, new LogInfo(LogState.Info, "Build stop requested by user"));
                        }

                        // Create event param to pass it to callback
                        string eventParam = s.HaltFlags.FinishCallbackEventParam();

                        // Reset Halt Flags before running OnBuildExit
                        // Otherwise only first command is executed
                        s.HaltFlags.Reset();

                        // OnBuildExit event callback
                        if (s.RunMode == EngineMode.RunAll || s.TestMode)
                        {
                            // OnBuildExit is called in full project build or in test mode.
                            // OnBuildExit is not called on script interface control or CodeBox.
                            // (Script interface and CodeBox use EngineMode.RunMainAndOne or EngineMode.RunOne)
                            CheckAndRunCallback(s, s.OnBuildExit, eventParam, "OnBuildExit", true);
                            s.OnBuildExit = null;
                        }

                        // Recover mouse cursor icon
                        if (s.CursorWait)
                        {
                            Application.Current?.Dispatcher?.Invoke(() =>
                            {
                                System.Windows.Input.Mouse.OverrideCursor = null;
                            });
                            s.CursorWait = false;
                        }

                        // Restore halt flags
                        s.HaltFlags.Restore(bak);

                        // Log build halt reason if the build was aborted
                        LogInfo? logHaltReason = s.HaltFlags.LogHaltReason();
                        if (logHaltReason != null)
                            s.Logger.BuildWrite(s, logHaltReason);

                        break;
                    }

                    // Run Next Script
                    s.CurrentScriptIdx += 1;
                    s.CurrentScript = s.Scripts[s.CurrentScriptIdx];
                    s.HaltFlags.ScriptHalt = false;
                }

                // Log Finished Time
                s.EndTime = DateTime.UtcNow;
                s.Logger.BuildFinish(s);

                // Cleanup MainViewModel
                s.MainViewModel.WaitingSubProcFinish = false;

                return s.BuildId;
            });

            return _task;
        }
        #endregion

        #region ForceStop, KillSubProcess
        public void ForceStop(bool forceKillSubProc)
        {
            lock (s.ForceStopLock)
            {
                s.MainViewModel.TaskBarProgressState = TaskbarItemProgressState.Error;
                if (forceKillSubProc)
                    KillSubProcess();

                s.CancelWebGet?.Cancel();
                s.MainViewModel.ScriptDescriptionText = "Build stop requested, please wait...";
                if (s.RunningSubProcess != null)
                    s.MainViewModel.WaitingSubProcFinish = true;

                s.HaltFlags.UserHalt = true;
            }
        }

        public Task ForceStopWait(bool forceKillSubProc)
        {
            if (_task is null)
                return Task.CompletedTask;
            
            ForceStop(forceKillSubProc);
            return Task.Run(() => _task.Wait());
        }

        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<보류 중>")]
        public void KillSubProcess()
        {
            lock (s.KillSubProcLock)
            {
                if (s.RunningSubProcess == null)
                    return;

                try
                {
                    s.RunningSubProcess.Kill();
                    s.RunningSubProcess.Dispose();
                }
                catch { /* Ignore error */ }
                s.RunningSubProcess = null;
            }
        }
        #endregion

        #region RunSection
        public static void RunSection(EngineState s, ScriptSection section, List<string> inParams, List<string>? outParams, EngineLocalState ls)
        {
            // Must copy inParams and outParams by value, not reference
            Dictionary<int, string> inParamDict = new Dictionary<int, string>();
            for (int i = 0; i < inParams.Count; i++)
                inParamDict[i + 1] = StringEscaper.ExpandSectionParams(s, inParams[i]);
            outParams = outParams == null ? new List<string>() : new List<string>(outParams);

            InternalRunSection(s, section, inParamDict, outParams, ls);
        }

        public static void RunSection(EngineState s, ScriptSection section, Dictionary<int, string> inParams, List<string>? outParams, EngineLocalState ls)
        {
            // Must copy inParams and outParams by value, not reference
            Dictionary<int, string> inParamDict = new Dictionary<int, string>(inParams);
            outParams = outParams == null ? new List<string>() : new List<string>(outParams);

            InternalRunSection(s, section, inParamDict, outParams, ls);
        }

        private static void InternalRunSection(EngineState s, ScriptSection section, Dictionary<int, string> inParams, List<string> outParams, EngineLocalState ls)
        {
            // Push ExecutionDepth
            int newDepth = s.PushLocalState(ls.IsMacro, ls.RefScriptId);

            if (section.Lines == null)
                s.Logger.BuildWrite(s, new LogInfo(LogState.CriticalError, $"Unable to load section [{section.Name}]", newDepth));

            CodeParser parser = new CodeParser(section, Global.Setting, s.Project.Compat);
            (CodeCommand[] cmds, _) = parser.ParseStatements();

            // Set CurrentSection
            s.CurrentSection = section;

            // Clear SectionReturnValue
            s.ReturnValue = string.Empty;

            // Run parsed commands
            RunCommands(s, section, cmds, inParams, outParams, false);

            // Update script progress (precise)
            s.PreciseUpdateScriptProgress(section);

            // Pop ExecutionDepth
            s.PopLocalState();
        }
        #endregion

        #region RunCommands
        public static List<LogInfo>? RunCommands(EngineState s, ScriptSection section, IReadOnlyList<CodeCommand> cmds, Dictionary<int, string> inParams, List<string>? outParams, bool pushDepth)
        {
            if (cmds.Count == 0)
            {
                s.Logger.BuildWrite(s, new LogInfo(LogState.Warning, $"No code in section [{section.Name}]", s.PeekDepth));
                return null;
            }

            if (pushDepth)
            {
                EngineLocalState ls = s.PeekLocalState();
                s.PushLocalState(ls.IsMacro, ls.RefScriptId);
            }

            List<LogInfo>? allLogs = s.TestMode ? new List<LogInfo>() : null;
            foreach (CodeCommand cmd in cmds)
            {
                s.CurSectionInParams = inParams;
                s.CurSectionOutParams = outParams;

                List<LogInfo> logs = ExecuteCommand(s, cmd);
                if (s.TestMode && allLogs != null)
                {
                    // ReSharper disable once PossibleNullReferenceException
                    allLogs.AddRange(logs);
                }

                // Update script progress (approximate)
                s.IncrementalUpdateSriptProgress(section);

                if (s.HaltFlags.CheckScriptHalt())
                    break;
            }

            // Check if SetLocal should be implicitly disabled
            if (DisableSetLocal(s))
            {
                // If SetLocal is implicitly disabled due to the halt flags, do not log the warning.
                if (!s.HaltFlags.CheckScriptHalt())
                {
                    int stackDepth = s.LocalVarsStateStack.Count + 1; // If SetLocal is disabled, SetLocalStack is decremented. 
                    s.Logger.BuildWrite(s, new LogInfo(LogState.Warning, $"Local variable isolation (depth {stackDepth}) implicitly disabled", s.PeekDepth));
                    s.Logger.BuildWrite(s, new LogInfo(LogState.Info, "Explicit use of [System.EndLocal] is recommended", s.PeekDepth));
                }
            }

            DisableErrorOff(s, ErrorOffState.ForceDisable);

            if (pushDepth)
                s.PopLocalState();

            return s.TestMode ? allLogs : null;
        }
        #endregion

        #region ExecuteCommand
        public static List<LogInfo> ExecuteCommand(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();
            EngineLocalState ls = s.PeekLocalState();

            // Check CodeType / CodeInfo deprecation
            if (cmd.IsTypeDeprecated)
                logs.Add(new LogInfo(LogState.Warning, $"Command [{cmd.Type}] is deprecated"));
            if (cmd.Info.IsInfoDeprecated)
                logs.Add(new LogInfo(LogState.Warning, cmd.Info.DeprecateMessage()));

            // If last command enabled ErrorOff, activate it now.
            // It is to prevent muting error of [System,ErrorOff] itself.
            if (s.ErrorOffWaitingRegister != null)
            {
                s.ErrorOff = s.ErrorOffWaitingRegister;
                s.ErrorOffWaitingRegister = null;
            }

            try
            {
                switch (cmd.Type)
                {
                    #region 00 Misc
                    case CodeType.None:
                        logs.Add(new LogInfo(LogState.Ignore, string.Empty));
                        break;
                    case CodeType.Error:
                        logs.Add(new LogInfo(LogState.Error, cmd.Info.Cast<CodeInfo_Error>().ErrorMessage));
                        break;
                    case CodeType.Comment:
                        logs.Add(new LogInfo(LogState.Ignore, string.Empty));
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
                    case CodeType.RegWriteEx:
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
                    case CodeType.RegCopy:
                        logs.AddRange(CommandRegistry.RegCopy(s, cmd));
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
                    #region 04 Ini
                    case CodeType.IniRead:
                        logs.AddRange(CommandIni.IniRead(s, cmd));
                        break;
                    case CodeType.IniReadOp:
                        logs.AddRange(CommandIni.IniReadOp(s, cmd));
                        break;
                    case CodeType.IniWrite:
                        logs.AddRange(CommandIni.IniWrite(s, cmd));
                        break;
                    case CodeType.IniWriteOp:
                        logs.AddRange(CommandIni.IniWriteOp(s, cmd));
                        break;
                    case CodeType.IniDelete:
                        logs.AddRange(CommandIni.IniDelete(s, cmd));
                        break;
                    case CodeType.IniDeleteOp:
                        logs.AddRange(CommandIni.IniDeleteOp(s, cmd));
                        break;
                    case CodeType.IniReadSection:
                        logs.AddRange(CommandIni.IniReadSection(s, cmd));
                        break;
                    case CodeType.IniReadSectionOp:
                        logs.AddRange(CommandIni.IniReadSectionOp(s, cmd));
                        break;
                    case CodeType.IniAddSection:
                        logs.AddRange(CommandIni.IniAddSection(s, cmd));
                        break;
                    case CodeType.IniAddSectionOp:
                        logs.AddRange(CommandIni.IniAddSectionOp(s, cmd));
                        break;
                    case CodeType.IniDeleteSection:
                        logs.AddRange(CommandIni.IniDeleteSection(s, cmd));
                        break;
                    case CodeType.IniDeleteSectionOp:
                        logs.AddRange(CommandIni.IniDeleteSectionOp(s, cmd));
                        break;
                    case CodeType.IniWriteTextLine:
                        logs.AddRange(CommandIni.IniWriteTextLine(s, cmd));
                        break;
                    case CodeType.IniWriteTextLineOp:
                        logs.AddRange(CommandIni.IniWriteTextLineOp(s, cmd));
                        break;
                    case CodeType.IniMerge:
                        logs.AddRange(CommandIni.IniMerge(s, cmd));
                        break;
                    case CodeType.IniCompact:
                        logs.AddRange(CommandIni.IniCompact(s, cmd));
                        break;
                    #endregion
                    #region 05 Wim
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
                        logs.AddRange(CommandWim.WimExtractOp(s, cmd));
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
                        logs.AddRange(CommandWim.WimPathOp(s, cmd));
                        break;
                    case CodeType.WimOptimize:
                        logs.AddRange(CommandWim.WimOptimize(s, cmd));
                        break;
                    case CodeType.WimExport:
                        logs.AddRange(CommandWim.WimExport(s, cmd));
                        break;
                    #endregion
                    #region 06 Archive
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
                    #region 07 Network
                    case CodeType.WebGet:
                    case CodeType.WebGetIfNotExist: // Deprecated
                        logs.AddRange(CommandNetwork.WebGet(s, cmd));
                        break;
                    #endregion
                    #region 08 Hash
                    case CodeType.Hash:
                        logs.AddRange(CommandHash.Hash(s, cmd));
                        break;
                    #endregion
                    #region 09 Script
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
                    #region 10 Interface
                    case CodeType.Visible:
                        logs.AddRange(CommandInterface.Visible(s, cmd));
                        break;
                    case CodeType.VisibleOp:
                        logs.AddRange(CommandInterface.VisibleOp(s, cmd));
                        break;
                    case CodeType.ReadInterface:
                        logs.AddRange(CommandInterface.ReadInterface(s, cmd));
                        break;
                    case CodeType.ReadInterfaceOp:
                        logs.AddRange(CommandInterface.ReadInterfaceOp(s, cmd));
                        break;
                    case CodeType.WriteInterface:
                        logs.AddRange(CommandInterface.WriteInterface(s, cmd));
                        break;
                    case CodeType.WriteInterfaceOp:
                        logs.AddRange(CommandInterface.WriteInterfaceOp(s, cmd));
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
                    #region 20 String
                    case CodeType.StrFormat:
                        logs.AddRange(CommandString.StrFormat(s, cmd));
                        break;
                    #endregion
                    #region 21 Math
                    case CodeType.Math:
                        logs.AddRange(CommandMath.Math(s, cmd));
                        break;
                    #endregion
                    #region 22 List
                    case CodeType.List:
                        logs.AddRange(CommandList.List(s, cmd));
                        break;
                    #endregion
                    #region 80 Branch
                    case CodeType.Run:
                    case CodeType.Exec:
                    case CodeType.RunEx:
                        CommandBranch.RunExec(s, cmd, new CommandBranch.RunExecOptions());
                        break;
                    case CodeType.Loop:
                    case CodeType.LoopLetter:
                    case CodeType.LoopEx:
                    case CodeType.LoopLetterEx:
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
                    #region 98 Debug
                    case CodeType.Debug:
                        logs.AddRange(CommandDebug.DebugCmd(s, cmd));
                        break;
                    #endregion
                    #region 99 External Macro
                    case CodeType.Macro:
                        CommandMacro.Macro(s, cmd);
                        break;
                    #endregion
                    #region Error
                    case CodeType.Retrieve: // Must be translated prior to different commands by CodeParser.
                        logs.Add(new LogInfo(LogState.CriticalError, "Internal Logic Error at Engine.ExecuteCommand"));
                        break;
                    default:
                        logs.Add(new LogInfo(LogState.Error, $"Cannot execute [{cmd.Type}] command"));
                        break;
                        #endregion
                }
            }
            catch (CriticalErrorException e)
            { // Critical Error, stop build
                logs.Add(new LogInfo(LogState.CriticalError, e, cmd, ls.Depth));
                s.HaltFlags.ErrorHalt = true;
            }
            catch (ExecuteException e)
            {
                // ExecuteException is for commands not returning List<LogInfo>.
                // Do not use LogInfo's default exception message generator here.
                logs.Add(new LogInfo(LogState.Error, e.Message, cmd, ls.Depth));
            }
            catch (InvalidCodeCommandException e)
            {
                logs.Add(new LogInfo(LogState.Error, e, e.Cmd, ls.Depth));
            }
            catch (Exception e)
            {
                logs.Add(new LogInfo(LogState.Error, e, cmd, ls.Depth));
            }

            // Mute LogState.{Error|Warning} if ErrorOff is enabled, and disable ErrorOff when necessary
            ProcessErrorOff(s, cmd.LineIdx, logs);

            // Stop build on error
            if (s.StopBuildOnError)
            {
                if (logs.Any(x => x.State == LogState.Error))
                    s.HaltFlags.ErrorHalt = true;
            }

            s.Logger.BuildWrite(s, LogInfo.AddCommandDepth(logs, cmd, ls.Depth));

            // Return logs, used in unit test
            return logs;
        }
        #endregion

        #region CheckAndRunCallback
        private static void CheckAndRunCallback(EngineState s, CodeCommand? cbCmd, string eventParam, string eventName, bool changeCurrentScript = false)
        {
            if (cbCmd == null)
                return;

            s.Logger.BuildWrite(s, $"Processing callback of event [{eventName}]");

            if (changeCurrentScript)
                s.CurrentScript = cbCmd.Section.Script;

            s.InitLocalStateStack();
            if (cbCmd.Type == CodeType.Run || cbCmd.Type == CodeType.Exec)
            {
                CodeInfo_RunExec info = cbCmd.Info.Cast<CodeInfo_RunExec>();

                if (1 <= info.InParams.Count)
                    info.InParams[0] = eventParam;
                else
                    info.InParams.Add(eventParam);

                CommandBranch.RunExec(s, cbCmd, new CommandBranch.RunExecOptions());
            }
            else
            {
                ExecuteCommand(s, cbCmd);
            }

            s.Logger.BuildWrite(s, new LogInfo(LogState.Info, $"End of callback [{eventName}]", s.PeekDepth));
            s.Logger.BuildWrite(s, Logger.LogSeparator);
        }
        #endregion

        #region SetLocal
        public static void EnableSetLocal(EngineState s)
        {
            s.LocalVarsStateStack.Push(new LocalVarsState(s.PeekLocalState(), s.Variables.GetVarDict(VarsType.Local)));
        }

        public static bool DisableSetLocal(EngineState s)
        {
            if (0 < s.LocalVarsStateStack.Count)
            {
                LocalVarsState last = s.LocalVarsStateStack.Peek();
                EngineLocalState ls = s.PeekLocalState();
                if (ls.Equals(last.LocalState))
                {
                    s.LocalVarsStateStack.Pop();

                    // Restore local variables
                    if (s.CurSectionOutParams == null)
                    {
                        s.Variables.SetVarDict(VarsType.Local, last.LocalVarsBackup);
                    }
                    else
                    {
                        List<string> keysToPreserve = new List<string>(s.CurSectionOutParams.Count);
                        foreach (string? varKey in s.CurSectionOutParams.Select(key => Variables.GetVariableName(s, key)))
                        {
                            if (varKey != null)
                                keysToPreserve.Add(varKey);
                        }
                        s.Variables.SetVarDict(VarsType.Local, last.LocalVarsBackup, keysToPreserve);
                    }

                    return true;
                }
            }

            return false;
        }
        #endregion

        #region ErrorOff
        public static void ProcessErrorOff(EngineState s, int lineIdx, List<LogInfo> logs)
        {
            if (s.ErrorOff == null)
                return;

            // When muting error, never check lineIdx.
            // If lineIdx is involved, ErrorOff will not work properly in RunExec.
            MuteLogError(logs);

            DisableErrorOff(s, lineIdx);
        }

        public static void DisableErrorOff(EngineState s, int lineIdx)
        {
            if (s.ErrorOff is ErrorOffState es)
            {
                EngineLocalState ls = s.PeekLocalState();
                if (ls.Equals(es.LocalState) &&
                    (lineIdx == ErrorOffState.ForceDisable || es.StartLineIdx + es.LineCount <= lineIdx))
                {
                    s.ErrorOff = null;
                    s.ErrorOffWaitingRegister = null;
                    s.ErrorOffDepthMinusOne = false;
                }
            }
        }

        private static void MuteLogError(List<LogInfo> logs)
        {
            for (int i = 0; i < logs.Count; i++)
            {
                LogInfo log = logs[i];
                if (log.State == LogState.Error || log.State == LogState.Warning || log.State == LogState.Overwrite)
                {
                    log.State = LogState.Muted;
                    logs[i] = log;
                }
            }
        }
        #endregion

        #region Utility Methods
        public static string GetEntrySection(EngineState s)
        {
            string? entrySection = ScriptSection.Names.Process;
            switch (s.RunMode)
            {
                case EngineMode.RunMainAndOne when s.CurrentScriptIdx != 0:
                    entrySection = s.RunOneEntrySection;
                    break;
                case EngineMode.RunOne:
                    entrySection = s.RunOneEntrySection;
                    break;
            }
            if (entrySection == null)
                throw new InternalException($"{nameof(entrySection)} is null");
            return entrySection;
        }

        /// <summary>
        /// Get script instance from path string.
        /// </summary>
        public static Script GetScriptInstance(EngineState s, string currentScriptPath, string loadScriptPath, out bool isCurrentScript)
        {
            isCurrentScript = loadScriptPath.Equals(currentScriptPath, StringComparison.OrdinalIgnoreCase) ||
                              loadScriptPath.Equals(Path.GetDirectoryName(currentScriptPath), StringComparison.OrdinalIgnoreCase);

            string realPath = loadScriptPath;
            Script? sc = s.Project.GetScriptByRealPath(realPath);
            if (sc == null)
            { // Cannot Find Script in Project.AllScripts
                // Try searching s.Scripts
                sc = s.Scripts.Find(x => x.RealPath.Equals(realPath, StringComparison.OrdinalIgnoreCase));
                if (sc == null)
                { // Still not found in s.Scripts
                    if (!File.Exists(realPath))
                        throw new ExecuteException($"No script in [{realPath}]");

                    sc = s.Project.LoadScriptRuntime(realPath, new LoadScriptRuntimeOptions { IgnoreMain = true });
                    if (sc == null)
                        throw new ExecuteException($"Unable to load script [{realPath}]");
                }
            }
            return sc;
        }

        /// <summary>
        /// Update script instance (e.g. Encode command).
        /// Script must already exist in EngineState s and the s.Project.
        /// </summary>
        /// <param name="s">Current EngineState</param>
        /// <param name="newScript">New instance to update</param>
        /// <returns>True if succeed</returns>
        public static bool UpdateScriptInstance(EngineState s, Script newScript)
        {
            int eIdx = s.Scripts.FindIndex(x => x.Equals(newScript));
            if (eIdx != -1)
                s.Scripts[eIdx] = newScript;
            else
                return false;

            int pIdx = s.Project.AllScripts.FindIndex(x => x.Equals(newScript));
            if (pIdx != -1)
                s.Project.AllScripts[pIdx] = newScript;
            else
                return false;

            return true;
        }
        #endregion

        #region Lock Methods
        /// <summary>
        /// Try to acquire global Engine lock 
        /// </summary>
        /// <returns>Return true if successfully acquired lock</returns>
        public static bool TryEnterLock()
        {
            lock (EnterLock)
            {
                if (_isRunning)
                    return false;

                if (Monitor.TryEnter(WorkingLock))
                {
                    _isRunning = true;
                    return true;
                }
                else
                {
                    _isRunning = false;
                    return false;
                }
            }
        }

        public static void ExitLock()
        {
            lock (EnterLock)
            {
                if (!_isRunning)
                    return;

                Monitor.Exit(WorkingLock);
                _isRunning = false;
            }
        }
        #endregion
    }
    #endregion

    #region EngineState Enums
    public enum EngineMode
    {
        /// <summary>
        /// Run all target scripts.
        /// </summary>
        RunAll,
        /// <summary>
        /// Run one script, executing MainScript before it.
        /// </summary>
        RunMainAndOne,
        /// <summary>
        /// Run only one script.
        /// </summary>
        RunOne,
    }

    public enum LogMode
    {
        /// <summary>
        /// For debugging
        /// - Worst performance
        /// - Write to database without any delays
        /// </summary>
        NoDefer,
        /// <summary>
        /// For normal use-case
        /// - Medium performance
        /// - Write to database when script is finished
        /// </summary>
        PartDefer,
        /// <summary>
        /// For interface button
        /// - Maximum performance
        /// - Disable trivial LogWindow event
        /// - Write to database after build is finished
        /// </summary>
        FullDefer,
    }
    #endregion

    #region EngineState
    public class EngineState
    {
        #region Base Properties
        public Project Project { get; private set; }
        public List<Script> Scripts { get; private set; }
        public Macro Macro { get; private set; }
        public Logger Logger { get; private set; }
        public EngineMode RunMode { get; private set; }
        public readonly string? RunOneEntrySection;
        public LogMode LogMode { get; set; } = LogMode.NoDefer; // Deferred logging is used for performance
        public MainViewModel MainViewModel { get; private set; }
        public Random Random { get; private set; }
        #endregion

        #region Derived Properties
        public string BaseDir => Project.BaseDir;
        public Script MainScript => Project.MainScript;
        public Variables Variables => Project.Variables;
        public int CurSectionInParamsCount => 0 < CurSectionInParams.Count ? CurSectionInParams.Keys.Max() : 0;
        public int CurSectionOutParamsCount => CurSectionOutParams?.Count ?? 0;
        #endregion

        #region State Properties
        public Script CurrentScript { get; set; }
        public int CurrentScriptIdx { get; set; }
        private ScriptSection? _currentSection;
        public ScriptSection CurrentSection
        {
            get
            {
                if (_currentSection == null)
                    throw new ExecuteException($"{nameof(_currentSection)} is null");
                return _currentSection;
            }
            set => _currentSection = value;
        }
        /// <summary>
        /// The 1-based index of in-params of current section.
        /// </summary>
        public Dictionary<int, string> CurSectionInParams { get; set; } = new Dictionary<int, string>();
        public List<string>? CurSectionOutParams { get; set; }
        public string ReturnValue { get; set; } = string.Empty;
        /// <summary>
        /// The flag represents whether an Engine should enter `else` command or not.
        /// </summary>
        public bool ElseFlag { get; set; } = false;
        public EngineHaltFlags HaltFlags { get; private set; }
        public bool CursorWait { get; set; } = false;
        public int BuildId { get; set; } = 0; // Used in logging
        public int ScriptId { get; set; } = 0; // Used in logging
        #endregion

        #region Progress Tracking Properties
        /// <summary>
        /// Which sections are already processed (so they should be not processed again in Processed* counters?)
        /// </summary>
        public HashSet<string> ProcessedSectionSet { get; private set; } = new HashSet<string>(16, StringComparer.OrdinalIgnoreCase);
        /// <summary>
        /// Accurate counter of how many section lines of the script was processed. 
        /// </summary>
        public long ProcessedSectionLines { get; private set; }
        /// <summary>
        /// Approximate counter of how many sections of the script was processed. It is occasionally reset to value of ProcessSectionLines.
        /// </summary>
        public long ProcessedCodeCount { get; private set; }
        /// <summary>
        /// Total number of section lines of the script to be processed. 
        /// </summary>
        public long TotalSectionLines { get; private set; }
        /// <summary>
        /// Accurate counter of how many scripts were processed. 
        /// </summary>
        public long ProcessedScripts { get; private set; }
        /// <summary>
        /// Total number of scripts to be processed?
        /// </summary>
        public long TotalScripts { get; private set; }
        #endregion

        #region Loop State Stack Properties
        /// <summary>
        /// Should be managed only in CommandBranch.Loop (and in Variables with compat option)
        /// </summary>
        public Stack<EngineLoopState> LoopStateStack { get; set; } = new Stack<EngineLoopState>(4);
        /// <summary>
        /// Should be managed only in Engine.RunSection() and CommandMacro.Macro()
        /// </summary>
        /// <remarks>
        /// At least one EngineLocalState (depth of 0) is guaranteed in the stack.
        /// </remarks>
        private readonly Stack<EngineLocalState> _localStateStack = new Stack<EngineLocalState>(16);
        public int PeekDepth => _localStateStack.Count == 0 ? 0 : _localStateStack.Peek().Depth;
        #endregion

        #region Build Elapsed Time Properties
        public DateTime StartTime { get; set; } = DateTime.MinValue;
        public DateTime EndTime { get; set; } = DateTime.MinValue;
        public TimeSpan Elapsed
        {
            get
            {
                if (DateTime.MinValue < EndTime) // EndTime is valid
                    return EndTime - StartTime;
                return DateTime.UtcNow - StartTime; // Engine is running
            }
        }
        #endregion

        #region Normal Option Properties
        /// <summary>
        /// Setting TestMode to true makes Engine.RunCommands return logs
        /// </summary>
        public bool TestMode { get; set; } = false;
        /// <summary>
        /// If engine is called by interface and FullDelayed is not set, disabling logger is advised for performance.
        /// </summary>
        public bool DisableLogger { get; set; } = false;
        /// <summary>
        /// User agent for WebGet command
        /// </summary>
        public string? CustomUserAgent { get; set; }
        public bool StopBuildOnError { get; set; } = true;
        #endregion

        #region Compat Option Properties
        public bool CompatDirCopyBug { get; set; } = false;
        public bool CompatFileRenameCanMoveDir { get; set; } = false;
        public bool CompatAllowLetterInLoop { get; set; } = false;
        public bool CompatAllowSetModifyInterface { get; set; } = false;
        public bool CompatDisableExtendedSectionParams { get; set; } = false;
        public bool CompatOverridableLoopCounter { get; set; } = false;
        public bool CompatAutoCompactIniWriteCommand { get; set; } = false;
        #endregion

        #region Command State Fields and Properties
        /// <summary>
        /// Project build callback
        /// </summary>
        public CodeCommand? OnBuildExit { get; set; }
        /// <summary>
        /// Script build callback
        /// </summary>
        public CodeCommand? OnScriptExit { get; set; }
        /// <summary>
        /// Command state of System,ErrorOff
        /// </summary>
        public ErrorOffState? ErrorOffWaitingRegister { get; set; }
        /// <summary>
        /// Command state of System,ErrorOff
        /// </summary>
        public bool ErrorOffDepthMinusOne { get; set; } = false;
        /// <summary>
        /// Command state of System,ErrorOff
        /// </summary>
        public ErrorOffState? ErrorOff { get; set; }
        /// <summary>
        /// Command state of System,SetLocal
        /// </summary>
        public Stack<LocalVarsState> LocalVarsStateStack { get; set; } = new Stack<LocalVarsState>(16);
        /// <summary>
        /// Command state of ShellExecute
        /// </summary>
        public Process? RunningSubProcess { get; set; }
        /// <summary>
        /// Command state of WebGet
        /// </summary>
        public CancellationTokenSource? CancelWebGet { get; set; } = null;
        #endregion

        #region Lock Fields
        public readonly object ForceStopLock = new object();
        public readonly object KillSubProcLock = new object();
        public readonly object RunningSubProcLock = new object();
        #endregion

        #region Constructor
        public EngineState(Project project, Logger logger, MainViewModel mainViewModel,
            EngineMode mode = EngineMode.RunAll, Script? runSingle = null, string entrySection = ScriptSection.Names.Process)
        {
            Project = project;
            Logger = logger;

            Macro = new Macro(Project, Variables, out _);

            RunMode = mode;
            switch (RunMode)
            {
                case EngineMode.RunAll:
                    { // Run All
                        Scripts = project.ActiveScripts;
                        TotalScripts = Scripts.Count;

                        CurrentScript = Scripts[0]; // MainScript
                        CurrentScriptIdx = 0;

                        RunOneEntrySection = null;
                    }
                    break;
                case EngineMode.RunMainAndOne:
                    { // Run one script, executing MainScript before it.
                        if (runSingle == null)
                            throw new ArgumentNullException(nameof(runSingle));
                        if (runSingle.Equals(project.MainScript) && entrySection.Equals(ScriptSection.Names.Process, StringComparison.Ordinal))
                            goto case EngineMode.RunOne;

                        Scripts = new List<Script>(2) { project.MainScript, runSingle };
                        TotalScripts = 1;

                        CurrentScript = Scripts[0];
                        CurrentScriptIdx = 0;

                        RunOneEntrySection = entrySection;
                    }
                    break;
                case EngineMode.RunOne:
                    { // Run only one script
                        if (runSingle == null)
                            throw new ArgumentNullException(nameof(runSingle));
                        Scripts = new List<Script>(1) { runSingle };
                        TotalScripts = 1;

                        CurrentScript = runSingle;
                        CurrentScriptIdx = Scripts.IndexOf(runSingle);

                        RunOneEntrySection = entrySection;
                    }
                    break;
                default:
                    throw new InvalidOperationException($"Invalid {nameof(RunMode)} [{RunMode}]");
            }

            // Init LocalStateStack
            InitLocalStateStack();

            // Init HaltFlags
            HaltFlags = new EngineHaltFlags();

            // Use secure random number generator to feed seed of pseudo random number generator.
            int seed;
            using (RandomNumberGenerator secureRandom = RandomNumberGenerator.Create())
            {
                byte[] seedArray = new byte[4];
                secureRandom.GetBytes(seedArray);
                seed = BitConverter.ToInt32(seedArray, 0);
            }
            Random = new Random(seed);

            // MainViewModel
            MainViewModel = mainViewModel;
            SetFullProgressMax();
        }
        #endregion

        #region SetOptions, SetCompat Methods
        public void SetOptions(Setting setting)
        {
            CustomUserAgent = setting.General.UseCustomUserAgent ? setting.General.CustomUserAgent : null;
            StopBuildOnError = setting.General.StopBuildOnError;

            LogMode = setting.Log.DeferredLogging ? LogMode.PartDefer : LogMode.NoDefer;
        }

        public void SetCompat(CompatOption compat)
        {
            CompatDirCopyBug = compat.AsteriskBugDirCopy;
            CompatFileRenameCanMoveDir = compat.FileRenameCanMoveDir;
            CompatAllowLetterInLoop = compat.AllowLetterInLoop;
            CompatAllowSetModifyInterface = compat.AllowSetModifyInterface;
            CompatDisableExtendedSectionParams = compat.DisableExtendedSectionParams;
            CompatOverridableLoopCounter = compat.OverridableLoopCounter;
            CompatAutoCompactIniWriteCommand = compat.AutoCompactIniWriteCommand;
        }
        #endregion

        #region Reset Methods
        public void ResetFull()
        {
            // Halt Flags
            HaltFlags.Reset();

            // Engine State
            ReturnValue = string.Empty;
            InitLocalStateStack();
            ElseFlag = false;
            LoopStateStack.Clear();

            // Command State
            OnBuildExit = null;
            OnScriptExit = null;
            ErrorOff = null;
            ErrorOffDepthMinusOne = false;
            ErrorOffWaitingRegister = null;
            LocalVarsStateStack.Clear();
            RunningSubProcess = null;
        }
        #endregion

        #region Local State Methods
        public void InitLocalStateStack()
        {
            _localStateStack.Clear();
            _localStateStack.Push(new EngineLocalState
            {
                Depth = 0,
            });
        }

        /// <summary>
        /// Push new local state.
        /// </summary>
        /// <returns>New execution depth.</returns>
        public int PushLocalState(bool isMacro, int refScriptId)
        {
            Debug.Assert(0 < _localStateStack.Count, "InitDepth() was not called properly");

            EngineLocalState ls = new EngineLocalState
            {
                Depth = _localStateStack.Peek().Depth + 1,
                IsMacro = isMacro,
                RefScriptId = refScriptId,
            };

            Debug.Assert(ls.Depth != -1, "Incorrect EngineLocalState.Depth handling");

            _localStateStack.Push(ls);
            return ls.Depth;
        }

        /// <summary>
        /// Remove current local state
        /// </summary>
        /// <returns></returns>
        public void PopLocalState()
        {
            Debug.Assert(0 < _localStateStack.Count, "InitDepth() was not called properly");
            _localStateStack.Pop();
        }

        /// <summary>
        /// Peek current local state
        /// </summary>
        /// <returns></returns>
        public EngineLocalState PeekLocalState()
        {
            Debug.Assert(0 < _localStateStack.Count, "InitDepth() was not called properly");
            return _localStateStack.Peek();
        }
        #endregion

        #region Progress Tracking Methods
        /// <summary>
        /// Reset script progress before executing a script
        /// </summary>
        public void ResetScriptProgress()
        {
            ProcessedSectionLines = 0;
            ProcessedCodeCount = 0;
            TotalSectionLines = CurrentScript != null ? CurrentScript.Sections.Values.Where(x => x.Type == SectionType.Code).Sum(s => s.Lines.Length) : 0;

            MainViewModel.BuildScriptProgressValue = 0;
            MainViewModel.BuildScriptProgressMax = TotalSectionLines;
            DisplayScriptProgress();
        }

        /// <summary>
        /// Reset full progress before starting a build
        /// </summary>
        public void SetFullProgressMax()
        {
            MainViewModel.BuildFullProgressMax = TotalScripts;
        }

        /// <summary>
        /// Set full progress base value before executing a script
        /// </summary>
        public void ResetFullProgress()
        {
            if (RunMode == EngineMode.RunMainAndOne) // Hide the fact that we are running the main script first
                ProcessedScripts = 0;
            else
                ProcessedScripts = CurrentScriptIdx;

            MainViewModel.BuildFullProgressValue = ProcessedScripts;
        }


        /// <summary>
        /// Update script/full progress precisely (line count)
        /// </summary>
        /// <param name="section">Current ScriptSection being run</param>
        public void PreciseUpdateScriptProgress(ScriptSection section)
        {
            if (CurrentScript == null)
                return;

            // Increase only if cmd came from CurrentScript
            // Q) Why reset BuildScriptProgressValue with proper processed line count, not relying on `IncrementalUpdateSriptProgress()`?
            // A) Computing exact progress of a script is very hard due to loose WinBuilder's ini-based format.
            //    So PEBakery approximate it by adding a section's LINE COUNT (not a CODE COUNT) to progress when it runs first time.
            //    (LINE COUNT and CODE COUNT is different, some of the LINES may not be actually runned due to If and Else branch commands).
            //    But this stragety does not work well if a section is too long, making a progress bar irresponsive.
            //    To mitigate it, `IncrementalUpdateSriptProgress()` increases CODE COUNT and show it to the user as a progress temporary.
            //    After a section was successfully finished, PEBakery reset the script progress with correct LINE COUNT value.
            if (CurrentScript.Equals(section.Script))
            {
                // Only increase BuildScriptProgressValue once per section
                ProcessedSectionSet.Add(section.Name);

                // Q) Why we have to apply Math.Max(s.ProcessedSectionLines, s.ProcessedCodeLines)?
                // A) Some branch commands (If, Else, Loop) call RunSection and RunCommands themselves.
                //    Their recursive calling of RunSection disturbs `section.Line.Length` checking.
                //    Current progress tracking impl does not take account of how much commands are actually executed, but how many lines were processed.
                //    If a branch command ran in a middle of section, sometimes it results in decresasing the script progress %.
                //    In order to prevent (hide, in fact) this issue, Math.Max() is used.
                //    The progress bar is eventually set to correct value after a section (which contains branch command) is finished.
                ProcessedSectionLines += section.Lines.Length;
                ProcessedCodeCount = Math.Max(ProcessedSectionLines, ProcessedCodeCount);
                DisplayScriptProgress();
                DisplayFullProgress();
            }
        }

        /// <summary>
        /// Update script/full progress approximately (code count)
        /// </summary>
        /// <param name="section">Current ScriptSection being run</param>
        public void IncrementalUpdateSriptProgress(ScriptSection section)
        {
            if (CurrentScript == null)
                return;

            // Increase only if the current section came from CurrentScript to prevent Macro section being counted.
            // s.ProcessedCodeCount is a temporary value; It will be reset to s.ProcessedSectionLines later in InternalRunSection().
            if (CurrentScript.Equals(section.Script) && !ProcessedSectionSet.Contains(section.Name))
            {
                ProcessedCodeCount = Math.Min(ProcessedSectionLines + section.Lines.Length, ProcessedCodeCount + 1);
                DisplayScriptProgress();
                DisplayFullProgress();
            }
        }

        /// <summary>
        /// Display script progress to the screen
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DisplayScriptProgress()
        {
            MainViewModel.BuildScriptProgressValue = ProcessedCodeCount;
        }

        /// <summary>
        /// Display full progress to the screen
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DisplayFullProgress()
        {
            MainViewModel.BuildFullProgressValue = ProcessedScripts + ((double)ProcessedCodeCount / TotalSectionLines);
        }
        #endregion

        #region RunResultReport Methods
        public string? RunResultReport()
        {
            string? reason = null;
            if (HaltFlags.CheckBuildHalt())
            {
                reason = HaltFlags.ReportHaltReason();
                Debug.Assert(reason != null, "Invalid reason string");

                MainViewModel.BuildEndedWithIssue = true;
                MainViewModel.TaskBarProgressState = TaskbarItemProgressState.Error;
                MainViewModel.BuildFullProgressValue = MainViewModel.BuildFullProgressMax;
            }
            else
            {
                MainViewModel.BuildEndedWithIssue = false;
            }

            return reason;
        }
        #endregion
    }
    #endregion

    #region EngineLoopState
    public class EngineLoopState
    {
        public enum LoopState
        {
            OnIndex,
            OnDriveLetter,
        }

        public LoopState State { get; set; }
        public long CounterIndex;
        public char CounterLetter;

        public EngineLoopState(long ctrIdx)
        {
            State = LoopState.OnIndex;
            CounterIndex = ctrIdx;
            CounterLetter = '\0';
        }

        public EngineLoopState(char ctrLetter)
        {
            State = LoopState.OnDriveLetter;
            CounterIndex = 0;
            if ('A' <= ctrLetter && ctrLetter <= 'Z' || ctrLetter == '\0')
                CounterLetter = ctrLetter;
            else if ('a' <= ctrLetter && ctrLetter <= 'z')
                CounterLetter = char.ToUpper(ctrLetter); // Use capital alphabet
            else
                throw new CriticalErrorException("Invalid LoopLetter Handling");
        }
    }
    #endregion

    #region EngineLocalState
    public class EngineLocalState : IEquatable<EngineLocalState>
    {
        #region Fields and Properties
        /// <summary>
        /// Current Depth.
        /// [Process] starts from 1.
        /// If no section is running, it must be 0.
        /// </summary>
        public int Depth { get; set; }
        /// <summary>
        /// Set to true is running from an Macro command.
        /// </summary>
        public bool IsMacro { get; set; }
        /// <summary>
        /// If the state is created inside referenced script, record its log script id. If not, set to 0.
        /// </summary>
        public int RefScriptId { get; set; }
        public bool IsRefScript => RefScriptId != 0;
        #endregion

        #region Default, UpdateDepth
        public void Default()
        {
            Depth = -1;
            IsMacro = false;
            RefScriptId = 0;
        }

        public EngineLocalState UpdateDepth(int newDepth)
        {
            return new EngineLocalState
            {
                Depth = newDepth,
                IsMacro = this.IsMacro,
                RefScriptId = this.RefScriptId,
            };
        }
        #endregion

        #region Interface Methods
        public bool Equals(EngineLocalState? ls)
        {
            if (ls is null)
                return false;

            return Depth == ls.Depth &&
                   IsMacro == ls.IsMacro &&
                   RefScriptId == ls.RefScriptId;
        }

        public override bool Equals(object? obj)
        {
            if (obj is EngineLocalState ls)
                return Equals(ls);
            return false;
        }

        public override int GetHashCode()
        {
            return Depth ^ (RefScriptId >> 16) ^ (IsMacro ? 1 >> 31 : 0);
        }

        public static bool operator ==(EngineLocalState left, EngineLocalState right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(EngineLocalState left, EngineLocalState right)
        {
            return !(left == right);
        }
        #endregion
    }
    #endregion

    #region LocalVarsState
    public class LocalVarsState
    {
        public EngineLocalState LocalState { get; private set; }
        public Dictionary<string, string> LocalVarsBackup { get; private set; }

        public LocalVarsState(EngineLocalState localState, Dictionary<string, string> localVarsBackup)
        {
            LocalState = localState;
            LocalVarsBackup = localVarsBackup;
        }
    }
    #endregion

    #region ErrorOffState
    public class ErrorOffState
    {
        public const int ForceDisable = -1;

        public EngineLocalState LocalState { get; private set; }
        public int StartLineIdx { get; private set; }
        public int LineCount { get; private set; }

        public ErrorOffState(EngineLocalState localState, int startLineIdx, int lineCount)
        {
            LocalState = localState;
            StartLineIdx = startLineIdx;
            LineCount = lineCount;
        }
    }
    #endregion

    #region EngineHaltFlags
    public class EngineHaltFlags
    {
        #region Properties
        /// <summary>
        /// The flag representes stopping by user request.
        /// </summary>
        /// <remarks>
        /// Logged with 1st priority.
        /// </remarks>
        public bool UserHalt { get; set; } = false;
        /// <summary>
        /// The flag representes stopping by occurence of error.
        /// </summary>
        /// /// <remarks>
        /// Logged with 2nd priority.
        /// </remarks>
        public bool ErrorHalt { get; set; } = false;
        /// <summary>
        /// The flag representes stopping by Halt command.
        /// </summary>
        /// /// <remarks>
        /// Logged with 3rd priority.
        /// </remarks>
        public bool CmdHalt { get; set; } = false;
        /// <summary>
        /// The flag represents early exit of curernt script by Exit command.
        /// </summary>
        /// /// <remarks>
        /// Logged with 4th priority.
        /// </remarks>
        public bool ScriptHalt { get; set; } = false;
        #endregion

        #region Check Halt
        public bool CheckScriptHalt() => UserHalt || ErrorHalt || CmdHalt || ScriptHalt;
        public bool CheckBuildHalt() => UserHalt || ErrorHalt || CmdHalt;
        #endregion

        #region Reset
        public void Reset()
        {
            UserHalt = false;
            ErrorHalt = false;
            CmdHalt = false;
            ScriptHalt = false;
        }
        #endregion

        #region Backup, Restore
        public EngineHaltFlags Backup()
        {
            return new EngineHaltFlags
            {
                UserHalt = this.UserHalt,
                ErrorHalt = this.ErrorHalt,
                CmdHalt = this.CmdHalt,
                ScriptHalt = this.ScriptHalt,
            };
        }

        public void Restore(EngineHaltFlags bak)
        {
            UserHalt = bak.UserHalt;
            ErrorHalt = bak.ErrorHalt;
            CmdHalt = bak.CmdHalt;
            ScriptHalt = bak.ScriptHalt;
        }
        #endregion

        #region FinishCallbackEventParam
        public string FinishCallbackEventParam()
        {
            if (UserHalt)
                return "STOP";
            if (ErrorHalt)
                return "ERROR";
            if (CmdHalt || ScriptHalt)
                return "COMMAND";
            return "DONE";
        }
        #endregion

        #region LogHaltReason, ReportHaltReason
        public LogInfo? LogHaltReason()
        {
            if (UserHalt)
                return new LogInfo(LogState.Warning, "Build stopped by user");
            if (ErrorHalt)
                return new LogInfo(LogState.Warning, "Build stopped by error");
            if (CmdHalt)
                return new LogInfo(LogState.Warning, "Build stopped by [Halt] command");
            if (ScriptHalt)
                return new LogInfo(LogState.Warning, "Build stopped by [Exit] command");
            return null;
        }

        public string? ReportHaltReason()
        {
            if (UserHalt)
                return "user";
            if (ErrorHalt)
                return "error";
            if (CmdHalt)
                return "[Halt] command";
            if (ScriptHalt)
                return "[Exit] command";
            return null;
        }
        #endregion
    }
    #endregion
}