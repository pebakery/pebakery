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
using System.Windows;
using System.ComponentModel;
using System.Threading;
using PEBakery.Helper;
using PEBakery.Exceptions;
using PEBakery.Core.Commands;
using PEBakery.WPF;
using System.Diagnostics;
using System.Collections.Concurrent;

namespace PEBakery.Core
{
    #region Engine
    public class Engine
    {
        #region Variables and Constructor
        public static Engine WorkingEngine; // Only 1 Instance can run at one time
        public static int WorkingLock;

        public EngineState s;
        private Task<long> task;

        public Engine(EngineState state)
        {
            s = state;
        }
        #endregion

        #region Ready, Finish Plugin
        /// <summary>
        /// Ready to run an plugin
        /// </summary>
        internal static void ReadyRunPlugin(EngineState s, Plugin p = null)
        {
            long buildId = s.BuildId;

            // Turn off System,ErrorOff
            s.Logger.ErrorOffCount = 0;
            // Turn off System,Log,Off
            s.Logger.SuspendLog = false;

            // Assert s.CurDepth == 1
            Debug.Assert(s.CurDepth == 1);

            // Set CurrentPlugin
            // Note: s.CurrentPluginIdx is not touched here
            if (p == null)
                p = s.CurrentPlugin;
            else
                s.CurrentPlugin = p;

            // Init Per-Plugin Log
            s.PluginId = s.Logger.Build_Plugin_Init(buildId, s.CurrentPlugin, s.CurrentPluginIdx + 1);

            // Log Plugin Build Start Message
            string msg;
            if (s.RunOnePlugin && s.EntrySection.Equals("Process", StringComparison.OrdinalIgnoreCase) == false)
                msg = $"Processing section [{s.EntrySection}] of plugin [{p.ShortPath}] ({s.CurrentPluginIdx + 1}/{s.Plugins.Count})";
            else
                msg = $"Processing plugin [{p.ShortPath}] ({s.CurrentPluginIdx + 1}/{s.Plugins.Count})";
            s.Logger.Build_Write(s, msg);
            s.Logger.Build_Write(s, Logger.LogSeperator);

            // Load Default Per-Plugin Variables
            s.Variables.ResetVariables(VarsType.Local);
            s.Logger.Build_Write(s, s.Variables.LoadDefaultPluginVariables(p));

            // Load Per-Plugin Macro
            s.Macro.ResetLocalMacros();
            s.Logger.Build_Write(s, s.Macro.LoadLocalMacroDict(p));

            // Reset Current Section Parameter
            s.CurSectionParams = new Dictionary<int, string>();

            // Clear Processed Section Hashes
            s.ProcessedSectionHashes.Clear();

            // Set Interface using MainWindow, MainViewModel
            if (s.RunOnePlugin)
                s.MainViewModel.PluginTitleText = StringEscaper.Unescape(p.Title);
            else
                s.MainViewModel.PluginTitleText = $"({s.CurrentPluginIdx + 1}/{s.Plugins.Count}) {StringEscaper.Unescape(p.Title)}";
            s.MainViewModel.PluginDescriptionText = StringEscaper.Unescape(p.Description);
            s.MainViewModel.PluginVersionText = $"v{p.Version}";
            s.MainViewModel.PluginAuthorText = p.Author;
            s.MainViewModel.BuildEchoMessage = $"Processing Section [{s.EntrySection}]...";

            long allLineCount = 0;
            foreach (var kv in s.CurrentPlugin.Sections.Where(x => x.Value.Type == SectionType.Code))
                allLineCount += kv.Value.Lines.Count; // Why not Codes? PEBakery compiles code on-demand, so we have only Lines at this time.

            s.MainViewModel.BuildPluginProgressBarMax = allLineCount;
            s.MainViewModel.BuildPluginProgressBarValue = 0;
            s.MainViewModel.BuildFullProgressBarValue = s.CurrentPluginIdx;

            if (Application.Current != null)
            {
                Application.Current.Dispatcher.BeginInvoke((Action)(() =>
                {
                    MainWindow w = Application.Current.MainWindow as MainWindow;

                    w.DrawPluginLogo(p);

                    if (w.CurBuildTree != null)
                        w.CurBuildTree.BuildFocus = false;

                    w.CurBuildTree = s.MainViewModel.BuildTree.FindPluginByFullPath(s.CurrentPlugin.FullPath);

                    if (w.CurBuildTree != null)
                        w.CurBuildTree.BuildFocus = true;
                }));
            }
        }

        private void FinishRunPlugin(long pluginId)
        {
            // Finish Per-Plugin Log
            s.Logger.Build_Write(s, $"End of plugin [{s.CurrentPlugin.ShortPath}]");
            s.Logger.Build_Write(s, Logger.LogSeperator);
            s.Logger.Build_Plugin_Finish(s.BuildId, pluginId, s.Variables.GetVarDict(VarsType.Local));
        }
        #endregion

        #region Run, Stop 
        public Task<long> Run(string runName)
        {
            task = Task.Run(() =>
            {
                s.BuildId = s.Logger.Build_Init(runName, s);

                s.MainViewModel.BuildFullProgressBarMax = s.Plugins.Count;

                while (true)
                {
                    ReadyRunPlugin(s);

                    // Run Main Section
                    PluginSection mainSection = s.CurrentPlugin.Sections[s.EntrySection];
                    SectionAddress addr = new SectionAddress(s.CurrentPlugin, mainSection);
                    s.Logger.LogStartOfSection(s, addr, 0, true, null, null);
                    Engine.RunSection(s, new SectionAddress(s.CurrentPlugin, mainSection), new List<string>(), 1, false);
                    s.Logger.LogEndOfSection(s, addr, 0, true, null);

                    // End of Plugin
                    FinishRunPlugin(s.PluginId);

                    // OnPluginExit event callback
                    Engine.CheckAndRunCallback(s, ref s.OnPluginExit, FinishEventParam(s), "OnPluginExit");

                    if (s.Plugins.Count - 1 <= s.CurrentPluginIdx ||
                        s.RunOnePlugin || s.ErrorHaltFlag || s.UserHaltFlag || s.CmdHaltFlag)
                    { // End of Build
                        bool alertErrorHalt = s.ErrorHaltFlag;
                        bool alertUserHalt = s.UserHaltFlag;
                        bool alertCmdHalt = s.CmdHaltFlag;

                        if (s.UserHaltFlag)
                        {
                            s.MainViewModel.PluginDescriptionText = "Build stop requested by user";
                            s.Logger.Build_Write(s, Logger.LogSeperator);
                            s.Logger.Build_Write(s, new LogInfo(LogState.Info, "Build stop requested by user"));
                        }

                        string eventParam = FinishEventParam(s);

                        // Reset Halt Flags before running OnBuildExit
                        s.ErrorHaltFlag = false;
                        s.UserHaltFlag = false;
                        s.CmdHaltFlag = false;

                        // OnBuildExit event callback
                        Engine.CheckAndRunCallback(s, ref s.OnBuildExit, eventParam, "OnBuildExit", true);

                        if (alertUserHalt)
                            MessageBox.Show("Build Stopped by User", "Build Halt", MessageBoxButton.OK, MessageBoxImage.Information);
                        else if (alertCmdHalt)
                            MessageBox.Show("Build Stopped by Halt Command", "Build Halt", MessageBoxButton.OK, MessageBoxImage.Information);
                        else if (alertErrorHalt)
                            MessageBox.Show("Build Stopped by Error", "Build Halt", MessageBoxButton.OK, MessageBoxImage.Information);

                        break;
                    }
                    s.Logger.Build_Write(s, string.Empty);

                    // Run Next Plugin
                    s.CurrentPluginIdx += 1;
                    s.CurrentPlugin = s.Plugins[s.CurrentPluginIdx];
                    s.PassCurrentPluginFlag = false;
                }

                s.Logger.Build_Finish(s.BuildId);

                return s.BuildId;
            });

            return task;
        }

        public void ForceStop()
        {
            s.UserHaltFlag = true;
        }

        public Task ForceStopWait()
        {
            s.UserHaltFlag = true;
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

            Dictionary<int, string> paramDict = new Dictionary<int, string>();
            for (int i = 0; i < sectionParams.Count; i++)
                paramDict[i + 1] = StringEscaper.ExpandSectionParams(s, sectionParams[i]);

            RunCommands(s, addr, codes, paramDict, depth, callback);

            // Increase only if cmd resides is CurrentPlugin
            if (s.CurrentPlugin.Equals(addr.Plugin))
                s.ProcessedSectionHashes.Add(addr.Section.GetHashCode());
        }

        public static void RunSection(EngineState s, SectionAddress addr, Dictionary<int, string> paramDict, int depth, bool callback)
        {
            List<CodeCommand> codes = addr.Section.GetCodes(true);
            s.Logger.Build_Write(s, LogInfo.AddDepth(addr.Section.LogInfos, s.CurDepth + 1));

            // Set CurrentSection
            s.CurrentSection = addr.Section;

            // Must copy ParamDict by value, not reference
            RunCommands(s, addr, codes, new Dictionary<int, string>(paramDict), depth, callback);

            // Increase only if cmd resides is CurrentPlugin
            if (s.CurrentPlugin.Equals(addr.Plugin))
                s.ProcessedSectionHashes.Add(addr.Section.GetHashCode());
        }
        #endregion

        #region RunCommands, RunCallback, ExecuteCommand
        public static void RunCommands(EngineState s, SectionAddress addr, List<CodeCommand> codes, Dictionary<int, string> sectionParams, int depth, bool callback = false)
        {
            if (codes.Count == 0)
            {
                s.Logger.Build_Write(s, new LogInfo(LogState.Warning, $"No code in [{addr.Plugin.ShortPath}]::[{addr.Section.SectionName}]", s.CurDepth + 1));
                return;
            }

            CodeCommand curCommand;
            for (int idx = 0; idx < codes.Count; idx++)
            {
                try
                {
                    curCommand = codes[idx];
                    s.CurDepth = depth;
                    s.CurSectionParams = sectionParams;
                    ExecuteCommand(s, curCommand);

                    if (s.PassCurrentPluginFlag || s.ErrorHaltFlag || s.UserHaltFlag)
                        break;
                }
                catch (CriticalErrorException)
                { // Critical Error, stop build
                    s.ErrorHaltFlag = true;
                    break;
                }
            }
        }

        public static List<LogInfo> ExecuteCommand(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();
            int curDepth = s.CurDepth;

            if (CodeCommand.DeprecatedCodeType.Contains(cmd.Type))
            {
                logs.Add(new LogInfo(LogState.Warning, $"Command [{cmd.Type}] is deprecated"));
            }

            s.MainViewModel.BuildCommandProgressBarValue = 0;

            try
            {
                switch (cmd.Type)
                {
                    #region 00 Misc
                    case CodeType.None:
                        logs.Add(new LogInfo(LogState.Ignore, string.Empty));
                        break;
                    case CodeType.Comment:
                        if (s.LogComment)
                            logs.Add(new LogInfo(LogState.Ignore, string.Empty));
                        break;
                    case CodeType.Error:
                        logs.Add(new LogInfo(LogState.Error, string.Empty));
                        break;
                    case CodeType.Unknown:
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
                    #endregion
                    #region 02 Registry
                    case CodeType.RegHiveLoad:
                        logs.AddRange(CommandRegistry.RegHiveLoad(s, cmd));
                        break;
                    case CodeType.RegHiveUnload:
                        logs.AddRange(CommandRegistry.RegHiveUnload(s, cmd));
                        break;
                    case CodeType.RegImport:
                        logs.AddRange(CommandRegistry.RegImport(s, cmd));
                        break;
                    case CodeType.RegExport:
                        logs.AddRange(CommandRegistry.RegExport(s, cmd));
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
                    case CodeType.TXTDelLine:
                        logs.AddRange(CommandText.TXTDelLine(s, cmd));
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
                        logs.AddRange(CommandINI.INIRead(s, cmd));
                        break;
                    case CodeType.INIReadOp:
                        logs.AddRange(CommandINI.INIReadOp(s, cmd));
                        break;
                    case CodeType.INIWrite:
                        logs.AddRange(CommandINI.INIWrite(s, cmd));
                        break;
                    case CodeType.INIWriteOp:
                        logs.AddRange(CommandINI.INIWriteOp(s, cmd));
                        break;
                    case CodeType.INIDelete:
                        logs.AddRange(CommandINI.INIDelete(s, cmd));
                        break;
                    case CodeType.INIDeleteOp:
                        logs.AddRange(CommandINI.INIDeleteOp(s, cmd));
                        break;
                    case CodeType.INIAddSection:
                        logs.AddRange(CommandINI.INIAddSection(s, cmd));
                        break;
                    case CodeType.INIAddSectionOp:
                        logs.AddRange(CommandINI.INIAddSectionOp(s, cmd));
                        break;
                    case CodeType.INIDeleteSection:
                        logs.AddRange(CommandINI.INIDeleteSection(s, cmd));
                        break;
                    case CodeType.INIDeleteSectionOp:
                        logs.AddRange(CommandINI.INIDeleteSectionOp(s, cmd));
                        break;
                    case CodeType.INIWriteTextLine:
                        logs.AddRange(CommandINI.INIWriteTextLine(s, cmd));
                        break;
                    case CodeType.INIWriteTextLineOp:
                        logs.AddRange(CommandINI.INIWriteTextLineOp(s, cmd));
                        break;
                    case CodeType.INIMerge:
                        logs.AddRange(CommandINI.INIMerge(s, cmd));
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
                    #region 07 Plugin
                    case CodeType.ExtractFile:
                        logs.AddRange(CommandPlugin.ExtractFile(s, cmd));
                        break;
                    case CodeType.ExtractAndRun:
                        logs.AddRange(CommandPlugin.ExtractAndRun(s, cmd));
                        break;
                    case CodeType.ExtractAllFiles:
                        logs.AddRange(CommandPlugin.ExtractAllFiles(s, cmd));
                        break;
                    case CodeType.Encode:
                        logs.AddRange(CommandPlugin.Encode(s, cmd));
                        break;
                    #endregion
                    #region 08 Interface
                    case CodeType.Visible:
                        logs.AddRange(CommandInterface.Visible(s, cmd));
                        break;
                    case CodeType.VisibleOp:
                        logs.AddRange(CommandInterface.VisibleOp(s, cmd));
                        break;
                    case CodeType.Message:
                        logs.AddRange(CommandInterface.Message(s, cmd));
                        break;
                    case CodeType.Echo:
                        logs.AddRange(CommandInterface.Echo(s, cmd));
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
                    #region 12 System
                    case CodeType.System:
                        logs.AddRange(CommandSystem.SystemCmd(s, cmd));
                        break;
                    case CodeType.ShellExecute:
                    case CodeType.ShellExecuteEx:
                    case CodeType.ShellExecuteDelete:
                        logs.AddRange(CommandSystem.ShellExecute(s, cmd));
                        break;
                    #endregion
                    #region 13 Branch
                    case CodeType.Run:
                    case CodeType.Exec:
                        CommandBranch.RunExec(s, cmd);
                        break;
                    case CodeType.Loop:
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
                    #region 14 Control
                    case CodeType.Set:
                        logs.AddRange(CommandControl.Set(s, cmd));
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
                    #endregion
                    #region 15 External Macro
                    case CodeType.Macro:
                        CommandMacro.Macro(s, cmd);
                        break;
                    #endregion
                    #region Error
                    // Error
                    default:
                        throw new ExecuteException($"Cannot execute [{cmd.Type}] command");
                        #endregion
                }
            }
            catch (CriticalErrorException)
            { // Stop Building
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

            // If ErrorOffCount is on, ignore LogState.Error
            if (0 < s.Logger.ErrorOffCount)
            {
                MuteLogError(logs);
                s.Logger.ErrorOffCount -= 1;
            }

            s.Logger.Build_Write(s, LogInfo.AddCommandDepth(logs, cmd, curDepth));

            // Increase only if cmd resides is CurrentPlugin
            if (!s.ProcessedSectionHashes.Contains(cmd.Addr.Section.GetHashCode()) &&
                (s.CurrentPlugin.Equals(cmd.Addr.Plugin)))
            {
                s.MainViewModel.BuildPluginProgressBarValue += 1;
            }

            s.MainViewModel.BuildCommandProgressBarValue = 1000;

            // This one is for Unit Test
            return logs; 
        }

        private static void CheckAndRunCallback(EngineState s, ref CodeCommand cbCmd, string eventParam, string eventName, bool changeCurrentPlugin = false)
        {
            if (cbCmd != null)
            {
                s.Logger.Build_Write(s, $"Processing callback of event [{eventName}]");

                if (changeCurrentPlugin)
                    s.CurrentPlugin = cbCmd.Addr.Plugin;

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
        }

        public string FinishEventParam(EngineState s)
        {
            if (s.UserHaltFlag)
                return "STOP";
            else if (s.CmdHaltFlag)
                return "HALT";
            else if (s.ErrorHaltFlag)
                return "ERROR";
            else
                return "DONE";
        }
        #endregion

        #region Utility Methods
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

        /// <summary>
        /// Get plugin instance from path string.
        /// </summary>
        public static Plugin GetPluginInstance(EngineState s, CodeCommand cmd, string currentPluginPath, string loadPluginPath, out bool inCurrentPlugin)
        {
            inCurrentPlugin = false;
            if (loadPluginPath.Equals(currentPluginPath, StringComparison.OrdinalIgnoreCase) ||
                loadPluginPath.Equals(Path.GetDirectoryName(currentPluginPath), StringComparison.OrdinalIgnoreCase))
                inCurrentPlugin = true; // Sometimes this value is not legal, so always use Project.GetPluginByFullPath.

            // string fullPath = StringEscaper.ExpandVariables(s, loadPluginPath);
            string fullPath = loadPluginPath;
            Plugin p = s.Project.GetPluginByFullPath(fullPath);
            if (p == null)
            { // Cannot Find Plugin in Project
                if (!File.Exists(fullPath))
                    throw new ExecuteException($"No plugin in [{fullPath}]");
                p = s.Project.LoadPluginMonkeyPatch(fullPath, false, true);
                if (p == null)
                    throw new ExecuteException($"Unable to load plugin [{fullPath}]");
            }

            return p;
        }

        internal static void UpdateCommandProgressBar(EngineState s, double val)
        {
            if (Application.Current != null) // for Unit Test
            {
                Application.Current.Dispatcher.BeginInvoke((Action)(() =>
                {
                    s.MainViewModel.BuildCommandProgressBarValue = val;
                }));
            }
            
        }
        #endregion
    }
    #endregion

    #region EngineState
    public class EngineState
    {
        // Fields used globally
        public Project Project;
        public List<Plugin> Plugins;
        public Variables Variables => Project.Variables;
        public Macro Macro;
        public Logger Logger;
        public bool RunOnePlugin;
        public MainViewModel MainViewModel;

        // Properties
        public string BaseDir => Project.BaseDir;
        public Plugin MainPlugin => Project.MainPlugin;

        // Fields : Engine's state
        public Plugin CurrentPlugin;
        public int CurrentPluginIdx;
        public PluginSection CurrentSection;
        public Dictionary<int, string> CurSectionParams = new Dictionary<int, string>();
        public List<int> ProcessedSectionHashes = new List<int>();
        public int CurDepth = 1;
        public bool ElseFlag = false;
        public bool LoopRunning = false;
        public long LoopCounter;
        public bool PassCurrentPluginFlag = false;
        public bool ErrorHaltFlag = false;
        public bool CmdHaltFlag = false;
        public bool UserHaltFlag = false;
        public long BuildId; // Used in logging
        public long PluginId; // Used in logging

        // Options
        public bool LogComment = true; // Used in logging
        public bool LogMacro = true; // Used in logging
        public bool CompatDirCopyBug = false; // Compatibility

        // Fields : System Commands
        public CodeCommand OnBuildExit = null;
        public CodeCommand OnPluginExit = null;

        // Readonly Fields
        public readonly string EntrySection;

        public EngineState(Project project, Logger logger, MainViewModel mainModel, Plugin runSingle = null, string entrySection = "Process")
        {
            this.Project = project;
            this.Logger = logger;

            Macro = new Macro(Project, Variables, out List<LogInfo> macroLogs);
            logger.Build_Write(BuildId, macroLogs);

            if (runSingle == null)
            {
                Plugins = project.ActivePlugins;

                CurrentPlugin = Plugins[0]; // Main Plugin, since its internal level is -256
                CurrentPluginIdx = 0;

                RunOnePlugin = false;
            }
            else
            {  // Run only one plugin
                Plugins = new List<Plugin>() { runSingle };

                CurrentPlugin = runSingle;
                CurrentPluginIdx = Plugins.IndexOf(runSingle);

                RunOnePlugin = true;
            }

            CurrentSection = null;
            EntrySection = entrySection;

            CurSectionParams = new Dictionary<int, string>();

            MainViewModel = mainModel;
        }

        #region SetOption
        public void SetOption(SettingViewModel m)
        {
            LogComment = m.Log_Comment;
            LogMacro = m.Log_Macro;
            CompatDirCopyBug = m.Compat_DirCopyBug;
        }
        #endregion
    }
    #endregion
}