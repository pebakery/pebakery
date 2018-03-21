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
using PEBakery.WPF;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace PEBakery.Core.Commands
{
    public static class CommandSystem
    {
        public static List<LogInfo> SystemCmd(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_System));
            CodeInfo_System info = cmd.Info as CodeInfo_System;

            // ReSharper disable once PossibleNullReferenceException
            SystemType type = info.Type;
            switch (type)
            {
                case SystemType.Cursor:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(SystemInfo_Cursor));
                        SystemInfo_Cursor subInfo = info.SubInfo as SystemInfo_Cursor;

                        // ReSharper disable once PossibleNullReferenceException
                        string iconStr = StringEscaper.Preprocess(s, subInfo.IconKind);

                        if (iconStr.Equals("WAIT", StringComparison.OrdinalIgnoreCase))
                        {
                            Application.Current?.Dispatcher.Invoke(() =>
                            {
                                System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
                            });
                            logs.Add(new LogInfo(LogState.Success, "Mouse cursor icon set to [Wait]"));
                        }
                        else if (iconStr.Equals("NORMAL", StringComparison.OrdinalIgnoreCase))
                        {
                            Application.Current?.Dispatcher.Invoke(() =>
                            {
                                System.Windows.Input.Mouse.OverrideCursor = null;
                            });
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
                        Debug.Assert(info.SubInfo.GetType() == typeof(SystemInfo_ErrorOff));
                        SystemInfo_ErrorOff subInfo = info.SubInfo as SystemInfo_ErrorOff;

                        // ReSharper disable once PossibleNullReferenceException
                        string linesStr = StringEscaper.Preprocess(s, subInfo.Lines);
                        if (!NumberHelper.ParseInt32(linesStr, out int lines))
                            throw new ExecuteException($"[{linesStr}] is not a valid integer");
                        if (lines <= 0)
                            throw new ExecuteException($"[{linesStr}] must be a positive integer");

                        // +1 to not count ErrorOff itself
                        s.ErrorOffSection = cmd.Addr.Section;
                        s.ErrorOffStartLineIdx = cmd.LineIdx + 1;
                        s.ErrorOffLineCount = lines;

                        logs.Add(new LogInfo(LogState.Success, $"Error is off for [{lines}] lines"));
                    }
                    break;
                case SystemType.GetEnv:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(SystemInfo_GetEnv));
                        SystemInfo_GetEnv subInfo = info.SubInfo as SystemInfo_GetEnv;

                        // ReSharper disable once PossibleNullReferenceException
                        string envVarName = StringEscaper.Preprocess(s, subInfo.EnvVarName);
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
                        Debug.Assert(info.SubInfo.GetType() == typeof(SystemInfo_GetFreeDrive));
                        SystemInfo_GetFreeDrive subInfo = info.SubInfo as SystemInfo_GetFreeDrive;

                        DriveInfo[] drives = DriveInfo.GetDrives();
                        const string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
                        char lastFreeLetter = letters.Except(drives.Select(d => d.Name[0])).LastOrDefault();

                        if (lastFreeLetter != '\0') // Success
                        {
                            logs.Add(new LogInfo(LogState.Success, $"Last free drive letter is [{lastFreeLetter}]"));
                            // ReSharper disable once PossibleNullReferenceException
                            List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, lastFreeLetter.ToString());
                            logs.AddRange(varLogs);
                        }
                        else // No Free Drives
                        {
                            // TODO: Is it correct WB082 behavior?
                            logs.Add(new LogInfo(LogState.Ignore, "No free drive letter"));
                            // ReSharper disable once PossibleNullReferenceException
                            List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, string.Empty);
                            logs.AddRange(varLogs);
                        }
                    }
                    break;
                case SystemType.GetFreeSpace:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(SystemInfo_GetFreeSpace));
                        SystemInfo_GetFreeSpace subInfo = info.SubInfo as SystemInfo_GetFreeSpace;

                        // ReSharper disable once PossibleNullReferenceException
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
                        Debug.Assert(info.SubInfo.GetType() == typeof(SystemInfo_IsAdmin));
                        SystemInfo_IsAdmin subInfo = info.SubInfo as SystemInfo_IsAdmin;

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

                        // ReSharper disable once PossibleNullReferenceException
                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, isAdmin.ToString());
                        logs.AddRange(varLogs);
                    }
                    break;
                case SystemType.OnBuildExit:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(SystemInfo_OnBuildExit));
                        SystemInfo_OnBuildExit subInfo = info.SubInfo as SystemInfo_OnBuildExit;

                        // ReSharper disable once PossibleNullReferenceException
                        s.OnBuildExit = subInfo.Cmd;

                        logs.Add(new LogInfo(LogState.Success, "OnBuildExit callback registered"));
                    }
                    break;
                case SystemType.OnScriptExit:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(SystemInfo_OnScriptExit));
                        SystemInfo_OnScriptExit subInfo = info.SubInfo as SystemInfo_OnScriptExit;

                        // ReSharper disable once PossibleNullReferenceException
                        s.OnScriptExit = subInfo.Cmd;

                        logs.Add(new LogInfo(LogState.Success, "OnScriptExit callback registered"));
                    }
                    break;
                case SystemType.RefreshInterface:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(SystemInfo));

                        AutoResetEvent resetEvent = null;
                        Application.Current?.Dispatcher.Invoke(() =>
                        {
                            MainWindow w = Application.Current.MainWindow as MainWindow;
                            resetEvent = w?.StartRefreshScriptWorker();
                        });
                        resetEvent?.WaitOne();

                        logs.Add(new LogInfo(LogState.Success, $"Rerendered script [{cmd.Addr.Script.Title}]"));
                    }
                    break;
                case SystemType.RescanScripts:
                case SystemType.LoadAll:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(SystemInfo));

                        // Refresh Project
                        AutoResetEvent resetEvent = null;
                        Application.Current?.Dispatcher.Invoke(() =>
                        {
                            MainWindow w = Application.Current.MainWindow as MainWindow;
                            resetEvent = w?.StartLoadWorker(true);
                        });
                        resetEvent?.WaitOne();

                        logs.Add(new LogInfo(LogState.Success, $"Reload project [{cmd.Addr.Script.Project.ProjectName}]"));
                    }
                    break;
                case SystemType.Load:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(SystemInfo_Load));
                        SystemInfo_Load subInfo = info.SubInfo as SystemInfo_Load;

                        // ReSharper disable once PossibleNullReferenceException
                        string filePath = StringEscaper.Preprocess(s, subInfo.FilePath);
                        SearchOption searchOption = SearchOption.AllDirectories;
                        if (subInfo.NoRec)
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

                        int successCount = 0;
                        foreach (string f in files)
                        { 
                            string scRealPath = Path.GetFullPath(f);

                            // Does this file already exists in project.AllScripts?
                            Project project = cmd.Addr.Project;
                            if (project.ContainsScriptByRealPath(scRealPath))
                            { // Project Tree conatins this script, so just refresh it
                                // RefreshScript -> Update Project.AllScripts
                                // TODO: Update EngineState.Scripts?
                                Script sc = Engine.GetScriptInstance(s, cmd, cmd.Addr.Script.RealPath, scRealPath, out _);
                                sc = s.Project.RefreshScript(sc);
                                if (sc == null)
                                {
                                    logs.Add(new LogInfo(LogState.Error, $"Unable to refresh script [{scRealPath}]"));
                                    continue;
                                }

                                // Update MainWindow and redraw Script
                                Application.Current?.Dispatcher.Invoke(() =>
                                {
                                    if (!(Application.Current.MainWindow is MainWindow w))
                                        return;

                                    w.UpdateScriptTree(project, false);
                                    if (sc.Equals(w.CurMainTree.Script))
                                    {
                                        w.CurMainTree.Script = sc;
                                        w.DrawScript(w.CurMainTree.Script);
                                    }
                                });

                                logs.Add(new LogInfo(LogState.Success, $"Refreshed script [{f}]"));
                                successCount += 1;
                            }
                            else
                            { // Add scripts into Project.AllScripts
                                Script sc = cmd.Addr.Project.LoadScriptMonkeyPatch(scRealPath, true, false);
                                if (sc == null)
                                {
                                    logs.Add(new LogInfo(LogState.Error, $"Unable to load script [{scRealPath}]"));
                                    continue;
                                }

                                // Update MainWindow.MainTree and redraw Script
                                Application.Current?.Dispatcher.Invoke(() =>
                                {
                                    if (!(Application.Current.MainWindow is MainWindow w))
                                        return;

                                    w.UpdateScriptTree(project, false);
                                    if (sc.Equals(w.CurMainTree.Script))
                                    {
                                        w.CurMainTree.Script = sc;
                                        w.DrawScript(w.CurMainTree.Script);
                                    }
                                });

                                logs.Add(new LogInfo(LogState.Success, $"Loaded script [{f}], added to script tree"));
                                successCount += 1;
                            }
                        }

                        if (1 < files.Length)
                            logs.Add(new LogInfo(LogState.Success, $"Refresh or loaded [{successCount}] scripts"));
                    }
                    break;
                case SystemType.SaveLog:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(SystemInfo_SaveLog));
                        SystemInfo_SaveLog subInfo = info.SubInfo as SystemInfo_SaveLog;

                        // ReSharper disable once PossibleNullReferenceException
                        string destPath = StringEscaper.Preprocess(s, subInfo.DestPath);
                        string logFormatStr = StringEscaper.Preprocess(s, subInfo.LogFormat);

                        LogExportType logFormat = Logger.ParseLogExportType(logFormatStr);

                        if (!s.DisableLogger)
                        { // When logger is disabled, s.BuildId is invalid.
                            s.Logger.Build_Write(s, new LogInfo(LogState.Success, $"Exported Build Logs to [{destPath}]", cmd, s.CurDepth));
                            s.Logger.ExportBuildLog(logFormat, destPath, s.BuildId);
                        }
                    }   
                    break;
                case SystemType.SetLocal:
                    { // SetLocal
                        Debug.Assert(info.SubInfo.GetType() == typeof(SystemInfo));

                        Engine.EnableSetLocal(s, cmd.Addr.Section);

                        logs.Add(new LogInfo(LogState.Success, "Local variables are isolated"));
                    }
                    break;
                case SystemType.EndLocal:
                    { // EndLocal
                        // No CodeInfo
                        Debug.Assert(info.SubInfo.GetType() == typeof(SystemInfo));

                        Engine.DisableSetLocal(s, cmd.Addr.Section);

                        logs.Add(new LogInfo(LogState.Success, "Local variables are no longer isolated"));
                    }
                    break;
                // WB082 Compatibility Shim
                case SystemType.HasUAC:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(SystemInfo_HasUAC));
                        SystemInfo_HasUAC subInfo = info.SubInfo as SystemInfo_HasUAC;

                        logs.Add(new LogInfo(LogState.Warning, "[System,HasUAC] is deprecated"));

                        // Deprecated, WB082 Compability Shim
                        // ReSharper disable once PossibleNullReferenceException
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
                        var varLogs = s.Variables.LoadDefaultGlobalVariables();
                        logs.AddRange(LogInfo.AddDepth(varLogs, s.CurDepth + 1));

                        // Load Per-Script Variables
                        varLogs = s.Variables.LoadDefaultScriptVariables(cmd.Addr.Script);
                        logs.AddRange(LogInfo.AddDepth(varLogs, s.CurDepth + 1));

                        // Load Per-Script Macro
                        s.Macro.ResetLocalMacros();
                        varLogs = s.Macro.LoadLocalMacroDict(cmd.Addr.Script, false);
                        logs.AddRange(LogInfo.AddDepth(varLogs, s.CurDepth + 1));

                        logs.Add(new LogInfo(LogState.Success, "Variables are reset to their default state"));
                    }
                    break;
                default: // Error
                    return LogInfo.LogErrorMessage(logs, $"Wrong SystemType [{type}]");
            }

            return logs;
        }

        public static List<LogInfo> ShellExecute(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_ShellExecute));
            CodeInfo_ShellExecute info = cmd.Info as CodeInfo_ShellExecute;

            // ReSharper disable once PossibleNullReferenceException
            string verb = StringEscaper.Preprocess(s, info.Action);
            string filePath = StringEscaper.Preprocess(s, info.FilePath);

            if (cmd.Type == CodeType.ShellExecuteDelete)
            {
                if (!StringEscaper.PathSecurityCheck(filePath, out string errorMsg))
                    return LogInfo.LogErrorMessage(logs, errorMsg);
            }

            // Must not check existance of filePath with File.Exists()!
            // Because of PATH envrionment variable, it prevents call of system executables.
            // Ex) cmd.exe does not exist in %BaseDir%, but in System32 directory.
            StringBuilder b = new StringBuilder(filePath);
            using (Process proc = new Process())
            {
                proc.StartInfo = new ProcessStartInfo(filePath);
                if (!string.IsNullOrEmpty(info.Params))
                {
                    string parameters = StringEscaper.Preprocess(s, info.Params);
                    proc.StartInfo.Arguments = parameters;
                    b.Append(" ");
                    b.Append(parameters);
                }

                string pathVarBackup = null;
                if (info.WorkDir != null)
                {
                    string workDir = StringEscaper.Preprocess(s, info.WorkDir);
                    proc.StartInfo.WorkingDirectory = workDir;

                    // Set PATH environment variable (only for this process)
                    pathVarBackup = Environment.GetEnvironmentVariable("PATH");
                    Environment.SetEnvironmentVariable("PATH", workDir + ";" + pathVarBackup);
                }

                bool redirectStandardStream = false;
                Stopwatch watch = Stopwatch.StartNew();
                StringBuilder bConOut = new StringBuilder();
                void ConsoleDataReceivedHandler(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data == null)
                        return;

                    bConOut.AppendLine(e.Data);
                    s.MainViewModel.BuildConOutRedirect = bConOut.ToString();
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
                        if (MainViewModel.DisplayShellExecuteConOut && cmd.Type != CodeType.ShellExecuteEx)
                        {
                            redirectStandardStream = true;

                            // Windows console uses OEM code pages
                            Encoding cmdEncoding = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);

                            proc.StartInfo.RedirectStandardOutput = true;
                            proc.StartInfo.StandardOutputEncoding = cmdEncoding;
                            proc.OutputDataReceived += ConsoleDataReceivedHandler;

                            proc.StartInfo.RedirectStandardError = true;
                            proc.StartInfo.StandardErrorEncoding = cmdEncoding;
                            proc.ErrorDataReceived += ConsoleDataReceivedHandler;

                            // Without this, XCOPY.exe of Windows 7 will not work properly.
                            // https://stackoverflow.com/questions/14218642/xcopy-does-not-work-with-useshellexecute-false
                            proc.StartInfo.RedirectStandardInput = true;

                            s.MainViewModel.BuildConOutRedirectShow = true;
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

                    // Register process instance in EngineState, and run it
                    s.RunningSubProcess = proc;
                    proc.Exited += (object sender, EventArgs e) => 
                    {
                        s.RunningSubProcess = null;
                        if (redirectStandardStream)
                        {
                            s.MainViewModel.BuildConOutRedirect = bConOut.ToString();
                            watch.Stop();
                        }
                    };
                    proc.Start();

                    if (redirectStandardStream)
                    {
                        proc.BeginOutputReadLine();
                        proc.BeginErrorReadLine();
                    }

                    long tookTime = (long)watch.Elapsed.TotalSeconds;
                    switch (cmd.Type)
                    {
                        case CodeType.ShellExecute:
                            proc.WaitForExit();
                            logs.Add(new LogInfo(LogState.Success, $"Executed [{b}], returned exit code [{proc.ExitCode}], took [{tookTime}s]"));
                            break;
                        case CodeType.ShellExecuteEx:
                            logs.Add(new LogInfo(LogState.Success, $"Executed [{b}]"));
                            break;
                        case CodeType.ShellExecuteDelete:
                            proc.WaitForExit();
                            File.Delete(filePath);
                            logs.Add(new LogInfo(LogState.Success, $"Executed and deleted [{b}], returned exit code [{proc.ExitCode}], took [{tookTime}s]"));
                            break;
                        default:
                            return LogInfo.LogErrorMessage(logs, $"Internal Error! Invalid CodeType [{cmd.Type}]. Please report to issue tracker.");
                    }

                    if (cmd.Type != CodeType.ShellExecuteEx)
                    {
                        // WB082 behavior -> even if info.ExitOutVar is not specified, it will save value to %ExitCode%
                        string exitOutVar = info.ExitOutVar ?? "%ExitCode%";
                        LogInfo log = Variables.SetVariable(s, exitOutVar, proc.ExitCode.ToString()).First();
                        if (log.State == LogState.Success)
                            logs.Add(new LogInfo(LogState.Success, $"Exit code [{proc.ExitCode}] saved into variable [{exitOutVar}]"));
                        else
                            logs.Add(log);

                        if (redirectStandardStream)
                        {
                            string conout = bConOut.ToString().Trim();
                            if (0 < conout.Length)
                            {
                                if (conout.IndexOf('\n') == -1) // No NewLine
                                    logs.Add(new LogInfo(LogState.Success, $"[Console Output] {conout}"));
                                else // With NewLine
                                    logs.Add(new LogInfo(LogState.Success, $"[Console Output]\r\n{conout}\r\n"));
                            }
                        }
                    }
                }
                finally
                {
                    // Restore PATH environment variable
                    if (pathVarBackup != null)
                        Environment.SetEnvironmentVariable("PATH", pathVarBackup);

                    if (redirectStandardStream)
                    {
                        s.MainViewModel.BuildConOutRedirect = string.Empty;
                        s.MainViewModel.BuildConOutRedirectShow = false;
                    }
                }
            }

            return logs;
        }
    }
}
