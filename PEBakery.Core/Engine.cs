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

using PEBakery.Core.Commands;
using PEBakery.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;

namespace PEBakery.Core
{
    #region Engine
    public class Engine
    {
        #region Variables and Constructor
        public static Engine WorkingEngine; // Only 1 instance allowed to run at one time
        public static int WorkingLock = 0;
        public static bool StopBuildOnError = true;

        // ReSharper disable once InconsistentNaming
        public EngineState s;
        private Task<int> _task;

        public static readonly string DefaultUserAgent = $"PEBakery/{Global.Const.StringVersion}";

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
            s.ErrorOff = null;
            // Turn off temporary log suspend
            s.Logger.SuspendBuildLog = false;

            Debug.Assert(s.PeekDepth == 0, "Incorrect DepthInfoStack handling");

            // Set CurrentScript
            // Note: s.CurrentScriptIdx is not touched here
            if (sc == null)
                sc = s.CurrentScript;
            else
                s.CurrentScript = sc;

            // Init Per-Script Log
            bool prepareBuild = s.MainScript.Equals(s.CurrentScript) && s.CurrentScriptIdx == 0;
            s.ScriptId = s.Logger.BuildScriptInit(s, s.CurrentScript, s.CurrentScriptIdx + 1, prepareBuild && s.RunMode != EngineMode.RunOne);

            // Determine EntrySection
            string entrySection = Engine.GetEntrySection(s);

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
            s.Macro.ResetLocalMacros();
            s.Logger.BuildWrite(s, s.Macro.LoadLocalMacroDict(sc, false));

            // Reset Current Section Parameters
            s.CurSectionInParams = new Dictionary<int, string>();
            s.CurSectionOutParams = null;

            // Clear Processed Section Hashes
            s.ProcessedSectionHashes.Clear();

            // Set Interface using MainWindow, MainViewModel
            long allLineCount = 0;
            foreach (ScriptSection section in s.CurrentScript.Sections.Values.Where(x => x.Type == SectionType.Code))
            {
                Debug.Assert(section.Lines != null, "CodeSection should return proper \'Lines\' property");
                allLineCount += section.Lines.Length;
            }

            s.MainViewModel.BuildScriptProgressBarMax = allLineCount;
            s.MainViewModel.BuildScriptProgressBarValue = 0;
            s.MainViewModel.BuildFullProgressBarValue = s.CurrentScriptIdx;

            if (!prepareBuild || s.RunMode == EngineMode.RunAll)
            {
                s.MainViewModel.DisplayScriptTexts(sc, s);

                s.MainViewModel.BuildEchoMessage = $"Processing Section [{entrySection}]...";

                Application.Current?.Dispatcher.BeginInvoke((Action)(() =>
                {
                    s.MainViewModel.DisplayScriptLogo(sc);

                    if (0 < s.MainViewModel.BuildTreeItems.Count)
                    {
                        if (s.MainViewModel.CurBuildTree != null)
                            s.MainViewModel.CurBuildTree.BuildFocus = false;
                        s.MainViewModel.CurBuildTree = ProjectTreeItemModel.FindScriptByRealPath(s.MainViewModel.BuildTreeItems[0], s.CurrentScript.RealPath);
                        if (s.MainViewModel.CurBuildTree != null)
                            s.MainViewModel.CurBuildTree.BuildFocus = true;
                    }
                }));
            }
        }

        private static void FinishRunScript(EngineState s)
        {
            // Finish Per-Script Log
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

                s.MainViewModel.BuildFullProgressBarMax = s.RunMode == EngineMode.RunMainAndOne ? 1 : s.Scripts.Count;

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
                        s.Logger.LogStartOfSection(s, mainSection, 0, true, null, null);
                        Engine.RunSection(s, mainSection, new List<string>(), new List<string>(), new EngineLocalStateOptions());
                        s.Logger.LogEndOfSection(s, mainSection, 0, true, null);
                    }

                    // End of Script
                    FinishRunScript(s);

                    if (s.ErrorHaltFlag || s.UserHaltFlag || s.CmdHaltFlag)
                        s.MainViewModel.TaskBarProgressState = System.Windows.Shell.TaskbarItemProgressState.Error;

                    // OnScriptExit event callback
                    {
                        bool bakPassCurrentScriptFlag = s.PassCurrentScriptFlag;
                        bool bakErrorHalt = s.ErrorHaltFlag;
                        bool bakUserHalt = s.UserHaltFlag;
                        bool bakCmdHalt = s.CmdHaltFlag;

                        string eventParam = FinishEventParam();

                        // Reset Halt Flags before running OnScriptExit
                        // Otherwise only first command is executed
                        s.ResetHaltFlags();

                        CheckAndRunCallback(s, ref s.OnScriptExit, eventParam, "OnScriptExit");

                        s.PassCurrentScriptFlag = bakPassCurrentScriptFlag;
                        s.ErrorHaltFlag = bakErrorHalt;
                        s.UserHaltFlag = bakUserHalt;
                        s.CmdHaltFlag = bakCmdHalt;
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
                        runOneScriptExit || s.ErrorHaltFlag || s.UserHaltFlag || s.CmdHaltFlag)
                    { // End of Build
                        bool alertPassCurrentScriptFlag = s.PassCurrentScriptFlag;
                        bool alertErrorHalt = s.ErrorHaltFlag;
                        bool alertUserHalt = s.UserHaltFlag;
                        bool alertCmdHalt = s.CmdHaltFlag;

                        if (s.UserHaltFlag)
                        {
                            s.MainViewModel.ScriptDescriptionText = "Build stop requested by user";
                            s.Logger.BuildWrite(s, Logger.LogSeparator);
                            s.Logger.BuildWrite(s, new LogInfo(LogState.Info, "Build stop requested by user"));
                        }

                        string eventParam = FinishEventParam();

                        // Reset Halt Flags before running OnBuildExit
                        // Otherwise only first command is executed
                        s.ResetHaltFlags();

                        // OnBuildExit event callback
                        if (s.RunMode == EngineMode.RunAll || s.TestMode)
                        {
                            // OnBuildExit is not called on script interface control, or codebox
                            // (which uses EngineMode.RunMainAndOne or EngineMode.RunOne)
                            // But it should be called in full script unit test
                            CheckAndRunCallback(s, ref s.OnBuildExit, eventParam, "OnBuildExit", true);
                        }

                        // Recover mouse cursor icon
                        if (s.CursorWait)
                        {
                            Application.Current?.Dispatcher.Invoke(() =>
                            {
                                System.Windows.Input.Mouse.OverrideCursor = null;
                            });
                            s.CursorWait = false;
                        }

                        if (!s.TestMode)
                        { // Disable MessageBox in TestMode for automated CI test
                            if (alertUserHalt)
                                MessageBox.Show("Build stopped by user", "Build Halt", MessageBoxButton.OK, MessageBoxImage.Information);
                            else if (alertErrorHalt)
                                MessageBox.Show("Build stopped by error", "Build Halt", MessageBoxButton.OK, MessageBoxImage.Information);
                            else if (alertCmdHalt)
                                MessageBox.Show("Build stopped by Halt command", "Build Halt", MessageBoxButton.OK, MessageBoxImage.Information);
                            else if (alertPassCurrentScriptFlag)
                                MessageBox.Show("Build stopped by Exit command", "Build Halt", MessageBoxButton.OK, MessageBoxImage.Information);
                        }

                        break;
                    }

                    // Run Next Script
                    s.CurrentScriptIdx += 1;
                    s.CurrentScript = s.Scripts[s.CurrentScriptIdx];
                    s.PassCurrentScriptFlag = false;
                }

                s.EndTime = DateTime.UtcNow;
                s.Logger.BuildFinish(s);

                return s.BuildId;
            });

            return _task;
        }
        #endregion

        #region ForceStop
        public void ForceStop()
        {
            s.MainViewModel.TaskBarProgressState = System.Windows.Shell.TaskbarItemProgressState.Error;
            if (s.RunningSubProcess != null)
            {
                try { s.RunningSubProcess.Kill(); }
                catch { /* Ignore error */ }
                s.RunningSubProcess = null;
            }
            s.RunningWebClient?.CancelAsync();
            s.UserHaltFlag = true;
            s.MainViewModel.ScriptDescriptionText = "Build stop requested, please wait...";
        }

        public Task ForceStopWait()
        {
            ForceStop();
            return Task.Run(() => _task.Wait());
        }
        #endregion

        #region RunSection
        public static void RunSection(EngineState s, ScriptSection section, List<string> inParams, List<string> outParams, EngineLocalStateOptions opts)
        {
            // Push ExecutionDepth
            int newDepth = s.PushLocalState(s, opts);

            if (section.Lines == null)
                s.Logger.BuildWrite(s, new LogInfo(LogState.CriticalError, $"Unable to load section [{section.Name}]", newDepth));

            CodeParser parser = new CodeParser(section, new CodeParser.Options());
            (CodeCommand[] cmds, _) = parser.ParseStatements();

            // Set CurrentSection
            s.CurrentSection = section;

            // Set SectionReturnValue to empty string
            s.SectionReturnValue = string.Empty;

            Dictionary<int, string> inParamDict = new Dictionary<int, string>();
            for (int i = 0; i < inParams.Count; i++)
                inParamDict[i + 1] = StringEscaper.ExpandSectionParams(s, inParams[i]);

            // Must copy ParamDict by value, not reference
            RunCommands(s, section, cmds, inParamDict, new List<string>(outParams), false);

            // Increase only if cmd resides in CurrentScript
            if (s.CurrentScript.Equals(section.Script))
                s.ProcessedSectionHashes.Add(section.GetHashCode());

            // Pop ExecutionDepth
            s.PopLocalState();
        }

        public static void RunSection(EngineState s, ScriptSection section, Dictionary<int, string> inParams, List<string> outParams, EngineLocalStateOptions opts)
        {
            // Push ExecutionDepth
            int newDepth = s.PushLocalState(s, opts);

            if (section.Lines == null)
                s.Logger.BuildWrite(s, new LogInfo(LogState.CriticalError, $"Unable to load section [{section.Name}]", newDepth));

            CodeParser parser = new CodeParser(section, Global.Setting, s.Project.Compat);
            (CodeCommand[] cmds, _) = parser.ParseStatements();

            // Set CurrentSection
            s.CurrentSection = section;

            // Set SectionReturnValue to empty string
            s.SectionReturnValue = string.Empty;

            // Must copy ParamDict by value, not reference
            outParams = outParams == null ? new List<string>() : new List<string>(outParams);
            RunCommands(s, section, cmds, new Dictionary<int, string>(inParams), outParams, false);

            // Increase only if cmd resides in CurrentScript
            if (s.CurrentScript.Equals(section.Script))
                s.ProcessedSectionHashes.Add(section.GetHashCode());

            // Pop ExecutionDepth
            s.PopLocalState();
        }
        #endregion

        #region RunCommands
        public static List<LogInfo> RunCommands(EngineState s, ScriptSection section, IList<CodeCommand> cmds, Dictionary<int, string> inParams, List<string> outParams, bool pushDepth)
        {
            if (cmds.Count == 0)
            {
                s.Logger.BuildWrite(s, new LogInfo(LogState.Warning, $"No code in script [{section.Script.TreePath}]'s section [{section.Name}]", s.PeekDepth + 1));
                return null;
            }

            if (pushDepth)
                s.PushLocalState(s, s.LocalStateStack.Peek().ToOptions());

            List<LogInfo> allLogs = s.TestMode ? new List<LogInfo>() : null;
            foreach (CodeCommand cmd in cmds)
            {
                s.CurSectionInParams = inParams;
                s.CurSectionOutParams = outParams;

                List<LogInfo> logs = ExecuteCommand(s, cmd);
                if (s.TestMode)
                {
                    // ReSharper disable once PossibleNullReferenceException
                    allLogs.AddRange(logs);
                }

                if (s.PassCurrentScriptFlag || s.ErrorHaltFlag || s.UserHaltFlag || s.CmdHaltFlag)
                    break;
            }

            if (Engine.DisableSetLocal(s, section))
            {
                int stackDepth = s.SetLocalStack.Count + 1; // If SetLocal is disabled, SetLocalStack is decremented. 
                s.Logger.BuildWrite(s, new LogInfo(LogState.Warning, $"Local variable isolation (depth {stackDepth}) implicitly disabled", s.PeekDepth));
                s.Logger.BuildWrite(s, new LogInfo(LogState.Info, "Explicit use of [System.EndLocal] is recommended", s.PeekDepth));
            }

            DisableErrorOff(s, section, s.PeekDepth, ErrorOffState.ForceDisable);

            if (pushDepth)
                s.PopLocalState();

            return s.TestMode ? allLogs : null;
        }
        #endregion

        #region ExecuteCommand
        public static List<LogInfo> ExecuteCommand(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();
            EngineLocalState di = s.LocalStateStack.Peek();

            if (CodeCommand.DeprecatedCodeType.Contains(cmd.Type))
                logs.Add(new LogInfo(LogState.Warning, $"Command [{cmd.Type}] is deprecated"));

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
                    #region 99 External Macro
                    case CodeType.Macro:
                        CommandMacro.Macro(s, cmd);
                        break;
                    #endregion
                    #region Error
                    case CodeType.Retrieve: // Must be translated by CodeParser to different commands
                        logs.Add(new LogInfo(LogState.CriticalError, "Internal Logic Error at Engine.ExecuteCommand"));
                        break;
                    default:
                        logs.Add(new LogInfo(LogState.Error, $"Cannot execute [{cmd.Type}] command"));
                        break;
                        #endregion
                }
            }
            catch (CriticalErrorException)
            { // Critical Error, stop build
                logs.Add(new LogInfo(LogState.CriticalError, "Critical Error!", cmd, di.Depth));
                s.ErrorHaltFlag = true;
            }
            catch (InvalidCodeCommandException e)
            {
                logs.Add(new LogInfo(LogState.Error, e, e.Cmd, di.Depth));
            }
            catch (Exception e)
            {
                logs.Add(new LogInfo(LogState.Error, e, cmd, di.Depth));
            }

            // Mute LogState.{Error|Warning} if ErrorOff is enabled, and disable ErrorOff when necessary
            ProcessErrorOff(s, cmd.Section, di.Depth, cmd.LineIdx, logs);

            // Stop build on error
            if (StopBuildOnError)
            {
                if (0 < logs.Count(x => x.State == LogState.Error))
                    s.ErrorHaltFlag = true;
            }

            s.Logger.BuildWrite(s, LogInfo.AddCommandDepth(logs, cmd, di.Depth));

            // Increase only if cmd resides in CurrentScript.
            // So if a section is from Macro, it will not be count.
            if (!s.ProcessedSectionHashes.Contains(cmd.Section.GetHashCode()) && s.CurrentScript.Equals(cmd.Section.Script))
                s.MainViewModel.BuildScriptProgressBarValue += 1;

            // Return logs, used in unit test
            return logs;
        }
        #endregion

        #region CheckAndRunCallback
        private static void CheckAndRunCallback(EngineState s, ref CodeCommand cbCmd, string eventParam, string eventName, bool changeCurrentScript = false)
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
            cbCmd = null;
        }

        public string FinishEventParam()
        {
            if (s.UserHaltFlag)
                return "STOP";
            if (s.ErrorHaltFlag)
                return "ERROR";
            if (s.CmdHaltFlag || s.PassCurrentScriptFlag)
                return "COMMAND";
            return "DONE";
        }
        #endregion

        #region SetLocal
        public static void EnableSetLocal(EngineState s, ScriptSection section)
        {
            s.SetLocalStack.Push(new SetLocalState
            {
                Section = section,
                SectionDepth = s.PeekDepth,
                LocalVarsBackup = s.Variables.GetVarDict(VarsType.Local),
            });
        }

        public static bool DisableSetLocal(EngineState s, ScriptSection section)
        {
            if (0 < s.SetLocalStack.Count)
            {
                SetLocalState last = s.SetLocalStack.Peek();
                if (last.Section.Equals(section) && last.SectionDepth == s.PeekDepth)
                {
                    s.SetLocalStack.Pop();
                    s.Variables.SetVarDict(VarsType.Local, last.LocalVarsBackup);
                    return true;
                }
            }

            return false;
        }
        #endregion

        #region ErrorOff
        public static bool ProcessErrorOff(EngineState s, ScriptSection section, int depth, int lineIdx, List<LogInfo> logs)
        {
            if (s.ErrorOff == null)
                return false;

            // When muting error, never check lineIdx.
            // If lineIdx is involved, ErrorOff will not work properly in RunExec.
            MuteLogError(logs);

            return DisableErrorOff(s, section, depth, lineIdx);
        }

        public static bool DisableErrorOff(EngineState s, ScriptSection section, int depth, int lineIdx)
        {
            if (s.ErrorOff is ErrorOffState es)
            {
                if (es.Section.Equals(section) && es.SectionDepth == depth &&
                    (lineIdx == ErrorOffState.ForceDisable || es.StartLineIdx + es.LineCount <= lineIdx))
                {
                    s.ErrorOff = null;
                    s.ErrorOffWaitingRegister = null;
                    s.ErrorOffDepthMinusOne = false;
                    return true;
                }
            }
            return false;
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
            string entrySection = "Process";
            switch (s.RunMode)
            {
                case EngineMode.RunMainAndOne when s.CurrentScriptIdx != 0:
                    entrySection = s.RunOneEntrySection;
                    break;
                case EngineMode.RunOne:
                    entrySection = s.RunOneEntrySection;
                    break;
            }

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
            Script sc = s.Project.GetScriptByRealPath(realPath);
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
    }
    #endregion

    #region EngineState Enums
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
    [SuppressMessage("ReSharper", "RedundantDefaultMemberInitializer")]
    public class EngineState
    {
        #region Field and Properties
        public Project Project;
        public List<Script> Scripts;
        public Variables Variables => Project.Variables;
        public Macro Macro;
        public Logger Logger;
        public EngineMode RunMode;
        public LogMode LogMode = LogMode.NoDefer; // For performance (deferred logging)
        public MainViewModel MainViewModel;
        public Random Random;

        // Property
        public string BaseDir => Project.BaseDir;
        public Script MainScript => Project.MainScript;
        public int CurSectionInParamsCount => 0 < CurSectionInParams.Count ? CurSectionInParams.Keys.Max() : 0;
        public int CurSectionOutParamsCount => CurSectionOutParams?.Count ?? 0;

        // State of the engine
        public Script CurrentScript;
        public int CurrentScriptIdx;
        public ScriptSection CurrentSection;
        public Dictionary<int, string> CurSectionInParams;
        public List<string> CurSectionOutParams = null;
        public string SectionReturnValue = string.Empty;
        public List<int> ProcessedSectionHashes = new List<int>(16);
        public bool ElseFlag = false;
        public LoopState LoopState = LoopState.Off;
        public long LoopCounter = 0;
        public char LoopLetter = ' '; // Use capital alphabet
        public bool PassCurrentScriptFlag = false; // Exit Command
        public bool ErrorHaltFlag = false;
        public bool CmdHaltFlag = false; // Halt Command
        public bool UserHaltFlag = false;
        public bool CursorWait = false;
        public int BuildId = 0; // Used in logging
        public int ScriptId = 0; // Used in logging

        // Local State Stack
        /// <summary>
        /// Should be managed only in Engine.RunSection() and CommandMacro.Macro()
        /// </summary>
        public Stack<EngineLocalState> LocalStateStack = new Stack<EngineLocalState>(16);
        public int PeekDepth => LocalStateStack.Count == 0 ? 0 : LocalStateStack.Peek().Depth;

        // Elapsed Time
        public DateTime StartTime = DateTime.MinValue;
        public DateTime EndTime = DateTime.MinValue;
        public TimeSpan Elapsed
        {
            get
            {
                if (DateTime.MinValue < EndTime) // EndTime is valid
                    return EndTime - StartTime;
                return DateTime.UtcNow - StartTime; // Engine is running
            }
        }

        // Normal Options
        public bool TestMode = false; // For test of engine -> Engine.RunCommands will return logs
        public bool DisableLogger = false; // If engine is called by interface and FullDelayed is not set, disabling logger is advised for performance.
        public string CustomUserAgent = null; // For WebGet

        // Compat Options
        public bool CompatDirCopyBug = false;
        public bool CompatFileRenameCanMoveDir = false;
        public bool CompatAllowLetterInLoop = false;
        public bool CompatAllowSetModifyInterface = false;
        public bool CompatDisableExtendedSectionParams = false;
        public bool CompatOverridableLoopCounter;

        // Command State
        // |- Callback
        public CodeCommand OnBuildExit = null;
        public CodeCommand OnScriptExit = null;
        // |- System,ErrorOff
        public ErrorOffState? ErrorOffWaitingRegister = null;
        public bool ErrorOffDepthMinusOne = false;
        public ErrorOffState? ErrorOff = null;
        // |- System,SetLocal
        public Stack<SetLocalState> SetLocalStack = new Stack<SetLocalState>(16);
        // |- ShellExecute
        public Process RunningSubProcess = null;
        // |- WebGet
        public WebClient RunningWebClient = null;

        // Readonly Fields
        public readonly string RunOneEntrySection;
        #endregion

        #region Constructor
        public EngineState(Project project, Logger logger, MainViewModel mainModel, EngineMode mode = EngineMode.RunAll, Script runSingle = null, string entrySection = "Process")
        {
            Project = project;
            Logger = logger;

            Macro = new Macro(Project, Variables, out List<LogInfo> macroLogs);
            logger.BuildWrite(BuildId, macroLogs);

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

            // Init DepthInfoStack
            InitLocalStateStack();

            // Current Section
            CurrentSection = null;
            CurSectionInParams = new Dictionary<int, string>();

            // MainViewModel
            MainViewModel = mainModel;

            // Use secure random number generator to feed seed to pseudo random number generator.
            byte[] seedArray = new byte[4];
            RNGCryptoServiceProvider secureRandom = new RNGCryptoServiceProvider();
            secureRandom.GetBytes(seedArray);
            int seed = BitConverter.ToInt32(seedArray, 0);
            Random = new Random(seed);
        }
        #endregion

        #region SetOptions
        public void SetOptions(Setting setting, CompatOption compat)
        {
            CustomUserAgent = setting.General.UseCustomUserAgent ? setting.General.CustomUserAgent : null;

            LogMode = setting.Log.DeferredLogging ? LogMode.PartDefer : LogMode.NoDefer;

            CompatDirCopyBug = compat.AsteriskBugDirCopy;
            CompatFileRenameCanMoveDir = compat.FileRenameCanMoveDir;
            CompatAllowLetterInLoop = compat.AllowLetterInLoop;
            CompatAllowSetModifyInterface = compat.AllowSetModifyInterface;
            CompatDisableExtendedSectionParams = compat.DisableExtendedSectionParams;
            CompatOverridableLoopCounter = compat.OverridableLoopCounter;
        }
        #endregion

        #region Reset
        public void ResetFull()
        {
            // Halt Flags
            ResetHaltFlags();

            // Engine State
            SectionReturnValue = string.Empty;
            InitLocalStateStack();
            ElseFlag = false;
            LoopState = LoopState.Off;
            LoopCounter = 0;
            LoopLetter = ' ';

            // Command State
            OnBuildExit = null;
            OnScriptExit = null;
            ErrorOff = null;
            ErrorOffDepthMinusOne = false;
            ErrorOffWaitingRegister = null;
            SetLocalStack.Clear();
            RunningSubProcess = null;
            RunningWebClient = null;
        }

        public void ResetHaltFlags()
        {
            // Halt Flags
            PassCurrentScriptFlag = false;
            ErrorHaltFlag = false;
            UserHaltFlag = false;
            CmdHaltFlag = false;
        }
        #endregion

        #region Local State
        public void InitLocalStateStack()
        {
            LocalStateStack.Clear();
            LocalStateStack.Push(new EngineLocalState
            {
                Depth = 0,
                IsMacro = false,
                IsRefScript = false,
                RefScriptId = 0,
            });
        }

        /// <summary>
        /// Push new local state.
        /// </summary>
        /// <returns>New execution depth.</returns>
        public int PushLocalState(EngineState s, EngineLocalStateOptions opts)
        {
            Debug.Assert(0 < LocalStateStack.Count, "InitDepth() was not called properly");
            int newDepth = LocalStateStack.Peek().Depth + 1;

            EngineLocalState di;
            if (opts.RefScriptId != 0)
            {
                di = new EngineLocalState
                {
                    Depth = newDepth,
                    IsMacro = opts.IsMacro,
                    IsRefScript = true,
                    RefScriptId = opts.RefScriptId,
                };
            }
            else
            {
                di = new EngineLocalState
                {
                    Depth = newDepth,
                    IsMacro = opts.IsMacro,
                    IsRefScript = false,
                    RefScriptId = s.ScriptId,
                };
            }

            LocalStateStack.Push(di);
            return newDepth;
        }

        /// <summary>
        /// Remove latest local state
        /// </summary>
        /// <returns></returns>
        public void PopLocalState()
        {
            if (0 < LocalStateStack.Count)
                LocalStateStack.Pop();
        }
        #endregion
    }
    #endregion

    #region EngineLocalState
    public struct EngineLocalStateOptions
    {
        public bool IsMacro;
        public int RefScriptId;
    }

    public class EngineLocalState
    {
        /// <summary>
        /// Current Depth.
        /// [Process] starts from 1.
        /// If no section is running, it must be 0.
        /// </summary>
        public int Depth;

        /// <summary>
        /// Is running from Macro command?
        /// </summary>
        public bool IsMacro;

        /// <summary>
        /// Is running from Referenced Script?
        /// </summary>
        public bool IsRefScript;
        public int RefScriptId;

        public EngineLocalStateOptions ToOptions()
        {
            return new EngineLocalStateOptions
            {
                IsMacro = IsMacro,
                RefScriptId = IsRefScript ? RefScriptId : 0,
            };
        }
    }
    #endregion

    #region struct SetLocalState
    public struct SetLocalState
    {
        public ScriptSection Section;
        public int SectionDepth;
        public Dictionary<string, string> LocalVarsBackup;
    }
    #endregion

    #region struct ErrorOffState
    public struct ErrorOffState
    {
        public ScriptSection Section;
        public int SectionDepth;
        public int StartLineIdx;
        public int LineCount;

        public const int ForceDisable = -1;
    }
    #endregion
}