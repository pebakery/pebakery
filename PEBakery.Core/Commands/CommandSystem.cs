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

using PEBakery.Helper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Windows;

namespace PEBakery.Core.Commands
{
    public static class CommandSystem
    {
        public static List<LogInfo> SystemCmd(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>(1);
            CodeInfo_System info = cmd.Info.Cast<CodeInfo_System>();

            SystemType type = info.Type;
            switch (type)
            {
                case SystemType.Cursor:
                    {
                        SystemInfo_Cursor subInfo = info.SubInfo.Cast<SystemInfo_Cursor>();

                        string iconStr = StringEscaper.Preprocess(s, subInfo.State);

                        if (iconStr.Equals("WAIT", StringComparison.OrdinalIgnoreCase))
                        {
                            Application.Current?.Dispatcher.Invoke(() =>
                            {
                                System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
                            });
                            s.CursorWait = true;
                            logs.Add(new LogInfo(LogState.Success, "Mouse cursor icon set to [Wait]"));
                        }
                        else if (iconStr.Equals("NORMAL", StringComparison.OrdinalIgnoreCase))
                        {
                            Application.Current?.Dispatcher.Invoke(() =>
                            {
                                System.Windows.Input.Mouse.OverrideCursor = null;
                            });
                            s.CursorWait = false;
                            logs.Add(new LogInfo(LogState.Success, "Mouse cursor icon set to [Normal]"));
                        }
                        else
                        {
                            logs.Add(new LogInfo(LogState.Error, $"Wrong mouse cursor icon [{iconStr}]"));
                        }
                    }
                    break;
                case SystemType.ErrorOff:
                    {
                        SystemInfo_ErrorOff subInfo = info.SubInfo.Cast<SystemInfo_ErrorOff>();

                        string linesStr = StringEscaper.Preprocess(s, subInfo.Lines);
                        if (!NumberHelper.ParseInt32(linesStr, out int lines))
                            return LogInfo.LogErrorMessage(logs, $"[{linesStr}] is not a positive integer");
                        if (lines <= 0)
                            return LogInfo.LogErrorMessage(logs, $"[{linesStr}] is not a positive integer");

                        if (s.ErrorOff == null)
                        {
                            // Enable s.ErrorOff
                            // Write to s.ErrorOffWaitingRegister instead of s.ErrorOff, to prevent muting error of [System,ErrorOff] itself.
                            EngineLocalState ls = s.PeekLocalState();
                            if (s.ErrorOffDepthMinusOne)
                                ls = ls.UpdateDepth(ls.Depth - 1);

                            ErrorOffState newState = new ErrorOffState
                            {
                                LocalState = ls,
                                StartLineIdx = cmd.LineIdx,
                                LineCount = lines,
                            };

                            s.ErrorOffWaitingRegister = newState;
                            s.ErrorOffDepthMinusOne = false;
                            logs.Add(new LogInfo(LogState.Success, $"Error and warning logs will be muted for [{lines}] lines"));
                        }
                        else
                        { // If s.ErrorOff is already enabled, do nothing. Ex) Nested ErrorOff
                            logs.Add(new LogInfo(LogState.Ignore, "ErrorOff is already enabled"));
                        }
                    }
                    break;
                case SystemType.GetEnv:
                    {
                        SystemInfo_GetEnv subInfo = info.SubInfo.Cast<SystemInfo_GetEnv>();

                        string envVarName = StringEscaper.Preprocess(s, subInfo.EnvVar);
                        string envVarValue = Environment.GetEnvironmentVariable(envVarName);
                        if (envVarValue == null) // Failure
                        {
                            logs.Add(new LogInfo(LogState.Ignore, $"Cannot get environment variable [%{envVarName}%]'s value"));
                            envVarValue = string.Empty;
                        }

                        logs.Add(new LogInfo(LogState.Success, $"Environment variable [{envVarName}]'s value is [{envVarValue}]"));
                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, envVarValue);
                        logs.AddRange(varLogs);
                    }
                    break;
                case SystemType.GetFreeDrive:
                    {
                        SystemInfo_GetFreeDrive subInfo = info.SubInfo.Cast<SystemInfo_GetFreeDrive>();

                        DriveInfo[] drives = DriveInfo.GetDrives();
                        const string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
                        char lastFreeLetter = letters.Except(drives.Select(d => d.Name[0])).LastOrDefault();

                        if (lastFreeLetter != '\0') // Success
                        {
                            logs.Add(new LogInfo(LogState.Success, $"Last free drive letter is [{lastFreeLetter}]"));
                            List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, lastFreeLetter.ToString());
                            logs.AddRange(varLogs);
                        }
                        else // No Free Drives
                        {
                            // TODO: Is it correct WB082 behavior?
                            logs.Add(new LogInfo(LogState.Ignore, "No free drive letter"));
                            List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, string.Empty);
                            logs.AddRange(varLogs);
                        }
                    }
                    break;
                case SystemType.GetFreeSpace:
                    {
                        SystemInfo_GetFreeSpace subInfo = info.SubInfo.Cast<SystemInfo_GetFreeSpace>();

                        string path = StringEscaper.Preprocess(s, subInfo.Path);

                        FileInfo f = new FileInfo(path);
                        if (f.Directory == null)
                            return LogInfo.LogErrorMessage(logs, $"Unable to get drive information of [{path}]");
                        DriveInfo drive = new DriveInfo(f.Directory.Root.FullName);
                        long freeSpaceMB = drive.TotalFreeSpace / (1024 * 1024); // B to MB

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, freeSpaceMB.ToString());
                        logs.AddRange(varLogs);
                    }
                    break;
                case SystemType.IsAdmin:
                    {
                        SystemInfo_IsAdmin subInfo = info.SubInfo.Cast<SystemInfo_IsAdmin>();

                        bool isAdmin;
                        using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                        {
                            WindowsPrincipal principal = new WindowsPrincipal(identity);
                            isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
                        }

                        if (isAdmin)
                            logs.Add(new LogInfo(LogState.Success, "PEBakery is running as Administrator"));
                        else
                            logs.Add(new LogInfo(LogState.Success, "PEBakery is not running as Administrator"));

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, isAdmin.ToString());
                        logs.AddRange(varLogs);
                    }
                    break;
                case SystemType.OnBuildExit:
                    {
                        SystemInfo_OnBuildExit subInfo = info.SubInfo.Cast<SystemInfo_OnBuildExit>();

                        s.OnBuildExit = subInfo.Cmd;

                        logs.Add(new LogInfo(LogState.Success, "OnBuildExit callback registered"));
                    }
                    break;
                case SystemType.OnScriptExit:
                    {
                        SystemInfo_OnScriptExit subInfo = info.SubInfo.Cast<SystemInfo_OnScriptExit>();

                        s.OnScriptExit = subInfo.Cmd;

                        logs.Add(new LogInfo(LogState.Success, "OnScriptExit callback registered"));
                    }
                    break;
                case SystemType.RefreshInterface:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(SystemInfo), "Invalid CodeInfo");

                        s.MainViewModel.StartRefreshScript().Wait();

                        logs.Add(new LogInfo(LogState.Success, $"Re-rendered script [{cmd.Section.Script.Title}]"));
                    }
                    break;
                case SystemType.RescanScripts:
                case SystemType.RefreshAllScripts:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(SystemInfo), "Invalid CodeInfo");

                        // Refresh Project
                        s.MainViewModel.StartLoadingProjects(true, true).Wait(); 

                        logs.Add(new LogInfo(LogState.Success, $"Reload project [{cmd.Section.Script.Project.ProjectName}]"));
                    }
                    break;
                case SystemType.LoadNewScript:
                    {
                        SystemInfo_LoadNewScript subInfo = info.SubInfo.Cast<SystemInfo_LoadNewScript>();

                        string srcFilePath = StringEscaper.Preprocess(s, subInfo.SrcFilePath);
                        string destTreeDir = StringEscaper.Preprocess(s, subInfo.DestTreeDir);
                        SearchOption searchOption = SearchOption.AllDirectories;
                        if (subInfo.NoRecFlag)
                            searchOption = SearchOption.TopDirectoryOnly;

                        Debug.Assert(srcFilePath != null, "Internal Logic Error at CommandSystem.LoadNewScript");

                        // Check wildcard
                        string wildcard = Path.GetFileName(srcFilePath);
                        bool containsWildcard = wildcard.IndexOfAny(new[] { '*', '?' }) != -1;

                        string[] files;
                        if (containsWildcard)
                        { // With wildcard
                            files = FileHelper.GetFilesEx(FileHelper.GetDirNameEx(srcFilePath), wildcard, searchOption);
                            if (files.Length == 0)
                                return LogInfo.LogErrorMessage(logs, $"Script [{srcFilePath}] does not exist");
                        }
                        else
                        { // No wildcard
                            if (!File.Exists(srcFilePath))
                                return LogInfo.LogErrorMessage(logs, $"Script [{srcFilePath}] does not exist");

                            files = new string[] { srcFilePath };
                        }
                        List<Script> newScripts = new List<Script>(files.Length);

                        string srcDirPath = Path.GetDirectoryName(srcFilePath);
                        Debug.Assert(srcDirPath != null, $"{nameof(srcDirPath)} is null (CommandSystem.LoadNewScript)");

                        (string realPath, string treePath)[] fileTuples = files
                            .Select(x => (x, x.Substring(srcDirPath.Length).Trim('\\')))
                            .ToArray();

                        int successCount = 0;
                        foreach ((string realPath, string treePath) in fileTuples)
                        {
                            // Add scripts into Project.AllScripts
                            string scRealPath = Path.GetFullPath(realPath);

                            string destTreePath = Path.Combine(s.Project.ProjectName, destTreeDir, treePath);
                            if (s.Project.ContainsScriptByTreePath(destTreePath))
                            {
                                if (subInfo.PreserveFlag)
                                {
                                    logs.Add(new LogInfo(subInfo.NoWarnFlag ? LogState.Ignore : LogState.Warning, $"Script [{destTreeDir}] already exists in project tree", cmd));
                                    continue;
                                }

                                logs.Add(new LogInfo(subInfo.NoWarnFlag ? LogState.Ignore : LogState.Overwrite, $"Script [{destTreeDir}] will be overwritten", cmd));
                            }

                            Script sc = s.Project.LoadScriptRuntime(scRealPath, destTreePath, new LoadScriptRuntimeOptions
                            {
                                AddToProjectTree = true,
                                OverwriteToProjectTree = true,
                            });
                            if (sc == null)
                            {
                                logs.Add(new LogInfo(LogState.Error, $"Unable to load script [{scRealPath}]"));
                                continue;
                            }

                            newScripts.Add(sc);
                            logs.Add(new LogInfo(LogState.Success, $"Loaded script [{scRealPath}] into project tree [{destTreeDir}]"));
                            successCount += 1;
                        }

                        s.Project.SortAllScripts();

                        // Update MainWindow.MainTree and redraw Script
                        Application.Current?.Dispatcher.Invoke(() =>
                        {
                            s.MainViewModel.UpdateScriptTree(s.Project, false, false);
                        });
                        foreach (Script sc in newScripts)
                        {
                            if (s.MainViewModel.CurMainTree != null && sc.Equals(s.MainViewModel.CurMainTree.Script))
                            {
                                s.MainViewModel.CurMainTree.Script = sc;
                                s.MainViewModel.DisplayScript(s.MainViewModel.CurMainTree.Script);
                            }
                        }

                        if (1 < files.Length)
                            logs.Add(new LogInfo(LogState.Success, $"Loaded [{successCount}] new scripts"));
                    }
                    break;
                case SystemType.RefreshScript:
                    {
                        SystemInfo_RefreshScript subInfo = info.SubInfo.Cast<SystemInfo_RefreshScript>();

                        string filePath = StringEscaper.Preprocess(s, subInfo.FilePath);
                        SearchOption searchOption = SearchOption.AllDirectories;
                        if (subInfo.NoRecFlag)
                            searchOption = SearchOption.TopDirectoryOnly;

                        // Check wildcard
                        string wildcard = Path.GetFileName(filePath);
                        bool containsWildcard = wildcard?.IndexOfAny(new[] { '*', '?' }) != -1;

                        string[] files;
                        if (containsWildcard)
                        { // With wildcard
                            files = FileHelper.GetFilesEx(FileHelper.GetDirNameEx(filePath), wildcard, searchOption);
                            if (files.Length == 0)
                            {
                                logs.Add(new LogInfo(LogState.Error, $"Script [{filePath}] does not exist"));
                                return logs;
                            }
                        }
                        else
                        { // No wildcard
                            if (!File.Exists(filePath))
                            {
                                logs.Add(new LogInfo(LogState.Error, $"Script [{filePath}] does not exist"));
                                return logs;
                            }

                            files = new string[] { filePath };
                        }
                        List<Script> newScripts = new List<Script>(files.Length);

                        int successCount = 0;
                        foreach (string f in files)
                        {
                            string scRealPath = Path.GetFullPath(f);

                            if (!s.Project.ContainsScriptByRealPath(scRealPath))
                            {
                                logs.Add(new LogInfo(LogState.Error, $"Unable to find script [{scRealPath}]"));
                                continue;
                            }

                            // RefreshScript -> Update Project.AllScripts
                            Script sc = Engine.GetScriptInstance(s, cmd.Section.Script.RealPath, scRealPath, out _);
                            sc = s.Project.RefreshScript(sc, s);
                            if (sc == null)
                            {
                                logs.Add(new LogInfo(LogState.Error, $"Unable to refresh script [{scRealPath}]"));
                                continue;
                            }

                            newScripts.Add(sc);
                            logs.Add(new LogInfo(LogState.Success, $"Refreshed script [{scRealPath}]"));
                            successCount += 1;
                        }

                        // Update MainWindow and redraw Script
                        Application.Current?.Dispatcher.Invoke(() =>
                        {
                            s.MainViewModel.UpdateScriptTree(s.Project, false);
                        });
                        foreach (Script sc in newScripts)
                        {
                            if (sc.Equals(s.MainViewModel.CurMainTree.Script))
                            {
                                s.MainViewModel.CurMainTree.Script = sc;
                                s.MainViewModel.DisplayScript(s.MainViewModel.CurMainTree.Script);
                            }
                        }

                        if (1 < files.Length)
                            logs.Add(new LogInfo(LogState.Success, $"Refresh [{successCount}] scripts"));
                    }
                    break;
                case SystemType.SaveLog:
                    {
                        SystemInfo_SaveLog subInfo = info.SubInfo.Cast<SystemInfo_SaveLog>();

                        string destPath = StringEscaper.Preprocess(s, subInfo.DestPath);
                        Debug.Assert(destPath != null, "Invalid SubInfo");

                        LogExportType logFormat;
                        if (subInfo.LogFormat == null)
                        {
                            string ext = Path.GetExtension(destPath);
                            if (ext.Equals(".htm", StringComparison.OrdinalIgnoreCase) ||
                                ext.Equals(".html", StringComparison.OrdinalIgnoreCase))
                                logFormat = LogExportType.Html;
                            else
                                logFormat = LogExportType.Text;
                        }
                        else
                        {
                            string logFormatStr = StringEscaper.Preprocess(s, subInfo.LogFormat);
                            logFormat = Logger.ParseLogExportType(logFormatStr);
                        }

                        if (!s.DisableLogger)
                        { // When logger is disabled, s.BuildId is invalid.
                            // Flush deferred logs into database
                            int realBuildId = s.Logger.Flush(s);

                            // This message should make it on exported log
                            s.Logger.BuildWrite(s, new LogInfo(LogState.Success, $"Exported build logs to [{destPath}]", cmd, s.PeekDepth));

                            // Do not use s.BuildId, for case of FullDeferredLogging
                            s.Logger.ExportBuildLog(logFormat, destPath, realBuildId, new LogExporter.BuildLogOptions
                            {
                                IncludeComments = true,
                                IncludeMacros = true,
                                ShowLogFlags = true,
                            });
                        }
                    }
                    break;
                case SystemType.SetLocal:
                    { // SetLocal
                        Debug.Assert(info.SubInfo.GetType() == typeof(SystemInfo), "Invalid CodeInfo");

                        Engine.EnableSetLocal(s);

                        logs.Add(new LogInfo(LogState.Success, $"Local variable isolation (depth {s.LocalVarsStateStack.Count}) enabled"));
                    }
                    break;
                case SystemType.EndLocal:
                    { // EndLocal
                        // No CodeInfo
                        Debug.Assert(info.SubInfo.GetType() == typeof(SystemInfo), "Invalid CodeInfo");

                        if (Engine.DisableSetLocal(s, cmd.Section)) // If SetLocal is disabled, SetLocalStack is decremented. 
                            logs.Add(new LogInfo(LogState.Success, $"Local variable isolation (depth {s.LocalVarsStateStack.Count + 1}) disabled"));
                        else
                            logs.Add(new LogInfo(LogState.Error, "[System,EndLocal] must be used with [System,SetLocal]"));
                    }
                    break;
                // WB082 Compatibility Shim
                case SystemType.HasUAC:
                    {
                        SystemInfo_HasUAC subInfo = info.SubInfo.Cast<SystemInfo_HasUAC>();

                        logs.Add(new LogInfo(LogState.Warning, "[System,HasUAC] is deprecated"));

                        // Deprecated, WB082 Compability Shim
                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, "True");
                        logs.AddRange(varLogs);
                    }
                    break;
                case SystemType.FileRedirect: // Do nothing
                    logs.Add(new LogInfo(LogState.Ignore, "[System,FileRedirect] is not necessary in PEBakery"));
                    break;
                case SystemType.RegRedirect: // Do nothing
                    logs.Add(new LogInfo(LogState.Ignore, "[System,RegRedirect] is not necessary in PEBakery"));
                    break;
                case SystemType.RebuildVars:
                    { // Reset Variables to clean state
                        s.Variables.ResetVariables(VarsType.Fixed);
                        s.Variables.ResetVariables(VarsType.Global);
                        s.Variables.ResetVariables(VarsType.Local);

                        // Load Global Variables
                        List<LogInfo> varLogs = s.Variables.LoadDefaultGlobalVariables();
                        logs.AddRange(LogInfo.AddDepth(varLogs, s.PeekDepth + 1));

                        // Load Per-Script Variables
                        varLogs = s.Variables.LoadDefaultScriptVariables(cmd.Section.Script);
                        logs.AddRange(LogInfo.AddDepth(varLogs, s.PeekDepth + 1));

                        // Load Per-Script Macro
                        s.Macro.ResetLocalMacros();
                        varLogs = s.Macro.LoadLocalMacroDict(cmd.Section.Script, false);
                        logs.AddRange(LogInfo.AddDepth(varLogs, s.PeekDepth + 1));

                        logs.Add(new LogInfo(LogState.Success, "Variables are reset to their default state"));
                    }
                    break;
                default: // Error
                    throw new InternalException("Internal Logic Error at CommandSystem.System");
            }

            return logs;
        }

        public static List<LogInfo> ShellExecute(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>(4);

            CodeInfo_ShellExecute info = cmd.Info.Cast<CodeInfo_ShellExecute>();

            string verb = StringEscaper.Preprocess(s, info.Action);
            string filePath = StringEscaper.Preprocess(s, info.FilePath);

            if (cmd.Type == CodeType.ShellExecuteDelete)
            {
                if (!StringEscaper.PathSecurityCheck(filePath, out string errorMsg))
                    return LogInfo.LogErrorMessage(logs, errorMsg);
            }

            // Must not check existence of filePath with File.Exists()!
            // Because of PATH environment variable, it prevents call of system executables.
            // Ex) cmd.exe does not exist in %BaseDir%, but in System32 directory.
            StringBuilder b = new StringBuilder(filePath);
            using (Process proc = new Process())
            {
                proc.EnableRaisingEvents = true;
                proc.StartInfo = new ProcessStartInfo(filePath);
                if (!string.IsNullOrEmpty(info.Params))
                {
                    string parameters = StringEscaper.Preprocess(s, info.Params);
                    proc.StartInfo.Arguments = parameters;
                    b.Append(" ");
                    b.Append(parameters);
                }

                string pathVarBak = null;
                if (info.WorkDir != null)
                {
                    string workDir = StringEscaper.Preprocess(s, info.WorkDir);
                    proc.StartInfo.WorkingDirectory = workDir;

                    // Set PATH environment variable (only for this process)
                    pathVarBak = Environment.GetEnvironmentVariable("PATH");
                    Environment.SetEnvironmentVariable("PATH", workDir + ";" + pathVarBak);
                }

                bool redirectStandardStream = false;
                object bConOutLock = new object();
                StringBuilder bStdOut = new StringBuilder();
                StringBuilder bStdErr = new StringBuilder();
                void StdOutDataReceivedHandler(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data == null)
                        return;

                    lock (bConOutLock)
                    {
                        bStdOut.AppendLine(e.Data);
                        s.MainViewModel.BuildConOutRedirectTextLines.Add(new Tuple<string, bool>(e.Data, false));
                    }
                }
                void StdErrDataReceivedHandler(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data == null)
                        return;

                    lock (bConOutLock)
                    {
                        bStdErr.AppendLine(e.Data);
                        s.MainViewModel.BuildConOutRedirectTextLines.Add(new Tuple<string, bool>(e.Data, true));
                    }
                }

                try
                {
                    if (verb.Equals("Open", StringComparison.OrdinalIgnoreCase))
                    {
                        proc.StartInfo.UseShellExecute = true;
                    }
                    else if (verb.Equals("Hide", StringComparison.OrdinalIgnoreCase))
                    {
                        proc.StartInfo.UseShellExecute = false;
                        proc.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        proc.StartInfo.CreateNoWindow = true;

                        // Redirecting standard stream without reading can full buffer, which leads to hang
                        if (Global.MainViewModel.DisplayShellExecuteConOut && cmd.Type != CodeType.ShellExecuteEx)
                        {
                            redirectStandardStream = true;

                            // Windows console uses OEM code pages
                            // Console.OutputEncoding -> System default locale for non-Unicode apps
                            // CultureInfo.CurrentCulture.TextInfo.OEMCodePage -> System's text encoding?
                            Encoding oemEncoding = Console.OutputEncoding;
                            // Encoding oemEncoding = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);

                            proc.StartInfo.RedirectStandardOutput = true;
                            proc.StartInfo.StandardOutputEncoding = oemEncoding;
                            proc.OutputDataReceived += StdOutDataReceivedHandler;

                            proc.StartInfo.RedirectStandardError = true;
                            proc.StartInfo.StandardErrorEncoding = oemEncoding;
                            proc.ErrorDataReceived += StdErrDataReceivedHandler;

                            // Without this, XCOPY.exe of Windows 7 will not work properly.
                            // https://stackoverflow.com/questions/14218642/xcopy-does-not-work-with-useshellexecute-false
                            proc.StartInfo.RedirectStandardInput = true;

                            s.MainViewModel.BuildConOutRedirectTextLines.Clear();
                            s.MainViewModel.BuildConOutRedirectVisibility = Visibility.Visible;
                        }
                    }
                    else if (verb.Equals("Min", StringComparison.OrdinalIgnoreCase))
                    {
                        proc.StartInfo.UseShellExecute = true;
                        proc.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
                    }
                    else
                    {
                        proc.StartInfo.Verb = verb;
                        proc.StartInfo.UseShellExecute = true;
                    }

                    // Register process instance to EngineState
                    if (cmd.Type != CodeType.ShellExecuteEx)
                        s.RunningSubProcess = proc;

                    Stopwatch watch = Stopwatch.StartNew();
                    proc.Start();

                    if (cmd.Type == CodeType.ShellExecuteEx)
                    {
                        watch.Stop();
                        logs.Add(new LogInfo(LogState.Success, $"Executed [{b}]"));
                    }
                    else
                    {
                        if (redirectStandardStream)
                        {
                            proc.BeginOutputReadLine();
                            proc.BeginErrorReadLine();
                        }

                        // Wait until exit
                        proc.WaitForExit();

                        // Unregister process instance from EngineState
                        s.RunningSubProcess = null;

                        watch.Stop();
                        long tookTime = (long)watch.Elapsed.TotalSeconds;

                        if (cmd.Type == CodeType.ShellExecute)
                        {
                            logs.Add(new LogInfo(LogState.Success, $"Executed [{b}], returned exit code [{proc.ExitCode}], took [{tookTime}s]"));
                        }
                        else if (cmd.Type == CodeType.ShellExecuteDelete)
                        {
                            File.Delete(filePath);
                            logs.Add(new LogInfo(LogState.Success, $"Executed and deleted [{b}], returned exit code [{proc.ExitCode}], took [{tookTime}s]"));
                        }

                        // WB082 behavior -> even if info.ExitOutVar is not specified, it will save value to %ExitCode%
                        string exitOutVar = info.ExitOutVar ?? "%ExitCode%";
                        LogInfo log = Variables.SetVariable(s, exitOutVar, proc.ExitCode.ToString()).First();
                        if (log.State == LogState.Success)
                            logs.Add(new LogInfo(LogState.Success, $"Exit code [{proc.ExitCode}] saved into variable [{exitOutVar}]"));
                        else
                            logs.Add(log);

                        if (redirectStandardStream)
                        {
                            string stdOut;
                            string stdErr;
                            lock (bConOutLock)
                            {
                                stdOut = bStdOut.ToString().Trim();
                                stdErr = bStdErr.ToString().Trim();
                            }

                            if (0 < stdOut.Length)
                            {
                                if (stdOut.IndexOf('\n') == -1) // No NewLine
                                    logs.Add(new LogInfo(LogState.Success, $"[Standard Output] {stdOut}"));
                                else // With NewLine
                                    logs.Add(new LogInfo(LogState.Success, $"[Standard Output]\r\n{stdOut}\r\n"));
                            }

                            if (0 < stdErr.Length)
                            {
                                if (stdErr.IndexOf('\n') == -1) // No NewLine
                                    logs.Add(new LogInfo(LogState.Success, $"[Standard Error] {stdErr}"));
                                else // With NewLine
                                    logs.Add(new LogInfo(LogState.Success, $"[Standard Error]\r\n{stdErr}\r\n"));
                            }
                        }
                    }
                }
                finally
                {
                    // Restore PATH environment variable
                    if (pathVarBak != null)
                        Environment.SetEnvironmentVariable("PATH", pathVarBak);

                    if (redirectStandardStream)
                    {
                        s.MainViewModel.BuildConOutRedirectVisibility = Visibility.Collapsed;
                        s.MainViewModel.BuildConOutRedirectTextLines.Clear();
                    }
                }
            }

            return logs;
        }
    }
}
