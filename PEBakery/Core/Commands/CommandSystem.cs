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

using PEBakery.Exceptions;
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

            SystemType type = info.Type;
            switch (type)
            {
                case SystemType.Cursor:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(SystemInfo_Cursor));
                        SystemInfo_Cursor subInfo = info.SubInfo as SystemInfo_Cursor;

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

                        string linesStr = StringEscaper.Preprocess(s, subInfo.Lines);
                        if (!NumberHelper.ParseInt32(linesStr, out int lines))
                            throw new ExecuteException($"[{linesStr}] is not a valid integer");
                        if (lines <= 0)
                            throw new ExecuteException($"[{linesStr}] must be positive integer");

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
                        string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
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
                        Debug.Assert(info.SubInfo.GetType() == typeof(SystemInfo_GetFreeSpace));
                        SystemInfo_GetFreeSpace subInfo = info.SubInfo as SystemInfo_GetFreeSpace;

                        string path = StringEscaper.Preprocess(s, subInfo.Path);

                        FileInfo f = new FileInfo(path);
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

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, isAdmin.ToString());
                        logs.AddRange(varLogs);
                    }
                    break;
                case SystemType.OnBuildExit:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(SystemInfo_OnBuildExit));
                        SystemInfo_OnBuildExit subInfo = info.SubInfo as SystemInfo_OnBuildExit;

                        s.OnBuildExit = subInfo.Cmd;

                        logs.Add(new LogInfo(LogState.Success, "OnBuildExit callback registered"));
                    }
                    break;
                case SystemType.OnScriptExit:
                case SystemType.OnPluginExit:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(SystemInfo_OnPluginExit));
                        SystemInfo_OnPluginExit subInfo = info.SubInfo as SystemInfo_OnPluginExit;

                        s.OnPluginExit = subInfo.Cmd;

                        logs.Add(new LogInfo(LogState.Success, "OnPluginExit callback registered"));
                    }
                    break;
                case SystemType.RefreshInterface:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(SystemInfo_RefreshInterface));
                        SystemInfo_RefreshInterface subInfo = info.SubInfo as SystemInfo_RefreshInterface;

                        AutoResetEvent resetEvent = null;
                        Application.Current?.Dispatcher.Invoke(() =>
                        {
                            MainWindow w = (Application.Current.MainWindow as MainWindow);
                            resetEvent = w.StartReloadPluginWorker();
                        });
                        if (resetEvent != null)
                            resetEvent.WaitOne();

                        logs.Add(new LogInfo(LogState.Success, $"Rerendered plugin [{cmd.Addr.Plugin.Title}]"));
                    }
                    break;
                case SystemType.RescanScripts:
                case SystemType.LoadAll:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(SystemInfo_LoadAll));
                        SystemInfo_LoadAll subInfo = info.SubInfo as SystemInfo_LoadAll;

                        // Reload Project
                        AutoResetEvent resetEvent = null;
                        Application.Current?.Dispatcher.Invoke(() =>
                        {
                            MainWindow w = (Application.Current.MainWindow as MainWindow);
                            resetEvent = w.StartLoadWorker(true);                
                        });
                        if (resetEvent != null)
                            resetEvent.WaitOne();

                        logs.Add(new LogInfo(LogState.Success, $"Reload project [{cmd.Addr.Plugin.Project.ProjectName}]"));
                    }
                    break;
                case SystemType.Load:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(SystemInfo_Load));
                        SystemInfo_Load subInfo = info.SubInfo as SystemInfo_Load;

                        string filePath = StringEscaper.Preprocess(s, subInfo.FilePath);
                        SearchOption searchOption = SearchOption.AllDirectories;
                        if (subInfo.NoRec)
                            searchOption = SearchOption.TopDirectoryOnly;
                            
                        // Check wildcard
                        string wildcard = Path.GetFileName(filePath);
                        bool containsWildcard = (wildcard.IndexOfAny(new char[] { '*', '?' }) != -1);

                        string[] files;
                        if (containsWildcard)
                        { // With wildcard
                            files = FileHelper.GetFilesEx(FileHelper.GetDirNameEx(filePath), wildcard, searchOption);
                            if (files.Length == 0)
                            {
                                logs.Add(new LogInfo(LogState.Error, $"Plugin [{filePath}] does not exist"));
                                return logs;
                            }
                        }
                        else
                        { // No wildcard
                            if (!File.Exists(filePath))
                            {
                                logs.Add(new LogInfo(LogState.Error, $"Plugin [{filePath}] does not exist"));
                                return logs;
                            }

                            files = new string[1] { filePath };
                        }

                        int successCount = 0;
                        foreach (string f in files)
                        { 
                            string pFullPath = Path.GetFullPath(f);

                            // Does this file already exists in project.AllPlugins?
                            Project project = cmd.Addr.Project;
                            if (project.ContainsPluginByFullPath(pFullPath))
                            { // Project Tree conatins this plugin, so just refresh it
                                // RefreshPlugin -> Update Project.AllPlugins
                                // TODO: Update EngineState.Plugins?
                                Plugin p = Engine.GetPluginInstance(s, cmd, cmd.Addr.Plugin.FullPath, pFullPath, out bool inCurrentPlugin);
                                p = s.Project.RefreshPlugin(p);
                                if (p == null)
                                {
                                    logs.Add(new LogInfo(LogState.Error, $"Unable to refresh plugin [{pFullPath}]"));
                                    continue;
                                }

                                // Update MainWindow and redraw Plugin
                                Application.Current?.Dispatcher.Invoke(() =>
                                {
                                    MainWindow w = (Application.Current.MainWindow as MainWindow);

                                    w.UpdatePluginTree(project, false);
                                    if (p.Equals(w.CurMainTree.Plugin))
                                    {
                                        w.CurMainTree.Plugin = p;
                                        w.DrawPlugin(w.CurMainTree.Plugin);
                                    }
                                });

                                logs.Add(new LogInfo(LogState.Success, $"Refreshed plugin [{f}]"));
                                successCount += 1;
                            }
                            else
                            { // Add plugins into Project.AllPlugins
                                Plugin p = cmd.Addr.Project.LoadPluginMonkeyPatch(pFullPath, true, false);
                                if (p == null)
                                {
                                    logs.Add(new LogInfo(LogState.Error, $"Unable to load plugin [{pFullPath}]"));
                                    continue;
                                }

                                // Update MainWindow.MainTree and redraw Plugin
                                Application.Current?.Dispatcher.Invoke(() =>
                                {
                                    MainWindow w = (Application.Current.MainWindow as MainWindow);

                                    w.UpdatePluginTree(project, false);
                                    if (p.Equals(w.CurMainTree.Plugin))
                                    {
                                        w.CurMainTree.Plugin = p;
                                        w.DrawPlugin(w.CurMainTree.Plugin);
                                    }
                                });

                                logs.Add(new LogInfo(LogState.Success, $"Loaded plugin [{f}], added to plugin tree"));
                                successCount += 1;
                            }
                        }

                        if (1 < files.Length)
                            logs.Add(new LogInfo(LogState.Success, $"Refresh or loaded [{successCount}] plugins"));
                    }
                    break;
                case SystemType.SaveLog:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(SystemInfo_SaveLog));
                        SystemInfo_SaveLog subInfo = info.SubInfo as SystemInfo_SaveLog;

                        string destPath = StringEscaper.Preprocess(s, subInfo.DestPath);
                        string logFormatStr = StringEscaper.Preprocess(s, subInfo.LogFormat);

                        LogExportType logFormat = Logger.ParseLogExportType(logFormatStr);

                        if (s.DisableLogger == false)
                        { // When logger is disabled, s.BuildId is invalid.
                            s.Logger.Build_Write(s, new LogInfo(LogState.Success, $"Exported Build Logs to [{destPath}]", cmd, s.CurDepth));
                            s.Logger.ExportBuildLog(logFormat, destPath, s.BuildId);
                        }
                    }   
                    break;
                case SystemType.SetLocal:
                    { // SetLocal
                        // No SystemInfo
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

                        logs.Add(new LogInfo(LogState.Warning, $"[System,HasUAC] is deprecated"));

                        // Deprecated, WB082 Compability Shim
                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, "True");
                        logs.AddRange(varLogs);
                    }
                    break;
                case SystemType.FileRedirect: // Do nothing
                    logs.Add(new LogInfo(LogState.Ignore, $"[System,FileRedirect] is not necessary in PEBakery"));
                    break;
                case SystemType.RegRedirect: // Do nothing
                    logs.Add(new LogInfo(LogState.Ignore, $"[System,RegRedirect] is not necessary in PEBakery"));
                    break;
                case SystemType.RebuildVars: 
                    { // Reset Variables to clean state
                        s.Variables.ResetVariables(VarsType.Fixed);
                        s.Variables.ResetVariables(VarsType.Global);
                        s.Variables.ResetVariables(VarsType.Local);

                        // Load Global Variables
                        List<LogInfo> varLogs;
                        varLogs = s.Variables.LoadDefaultGlobalVariables();
                        logs.AddRange(LogInfo.AddDepth(varLogs, s.CurDepth + 1));

                        // Load Per-Plugin Variables
                        varLogs = s.Variables.LoadDefaultPluginVariables(cmd.Addr.Plugin);
                        logs.AddRange(LogInfo.AddDepth(varLogs, s.CurDepth + 1));

                        // Load Per-Plugin Macro
                        s.Macro.ResetLocalMacros();
                        varLogs = s.Macro.LoadLocalMacroDict(cmd.Addr.Plugin, false);
                        logs.AddRange(LogInfo.AddDepth(varLogs, s.CurDepth + 1));

                        logs.Add(new LogInfo(LogState.Success, $"Variables are reset to default state"));
                    }
                    break;
                default: // Error
                    throw new InvalidCodeCommandException($"Wrong SystemType [{type}]");
            }

            return logs;
        }

        public static List<LogInfo> ShellExecute(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_ShellExecute));
            CodeInfo_ShellExecute info = cmd.Info as CodeInfo_ShellExecute;

            string verb = StringEscaper.Preprocess(s, info.Action);
            string filePath = StringEscaper.Preprocess(s, info.FilePath);

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

                            proc.StartInfo.RedirectStandardOutput = true;
                            proc.OutputDataReceived += (object sender, DataReceivedEventArgs e) =>
                            {
                                if (e.Data != null)
                                {
                                    bConOut.AppendLine(e.Data);
                                    s.MainViewModel.BuildConOutRedirect = bConOut.ToString();
                                } 
                            };

                            proc.StartInfo.RedirectStandardError = true;
                            proc.ErrorDataReceived += (object sender, DataReceivedEventArgs e) =>
                            {
                                if (e.Data != null)
                                {
                                    bConOut.AppendLine(e.Data);
                                    s.MainViewModel.BuildConOutRedirect = bConOut.ToString();
                                }
                            };

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

                    if (cmd.Type == CodeType.ShellExecuteSlow)
                        proc.PriorityClass = ProcessPriorityClass.BelowNormal;

                    if (redirectStandardStream)
                    {
                        proc.BeginOutputReadLine();
                        proc.BeginErrorReadLine();
                    }

                    long tookTime = (long)watch.Elapsed.TotalSeconds;
                    switch (cmd.Type)
                    {
                        case CodeType.ShellExecute:
                        case CodeType.ShellExecuteSlow:
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
                            throw new InternalException($"Internal Error! Invalid CodeType [{cmd.Type}]. Please report to issue tracker.");
                    }

                    if (cmd.Type != CodeType.ShellExecuteEx)
                    {
                        string exitOutVar;
                        if (info.ExitOutVar == null)
                            exitOutVar = "%ExitCode%"; // WB082 behavior -> even if info.ExitOutVar is not specified, it will save value to %ExitCode%
                        else
                            exitOutVar = info.ExitOutVar;

                        LogInfo log = Variables.SetVariable(s, exitOutVar, proc.ExitCode.ToString()).First();

                        if (log.State == LogState.Success)
                            logs.Add(new LogInfo(LogState.Success, $"Exit code [{proc.ExitCode}] saved into variable [{exitOutVar}]"));
                        else if (log.State == LogState.Error)
                            logs.Add(log);
                        else
                            throw new InternalException($"Internal Error! Invalid LogType [{log.State}]. Please report to issue tracker.");

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
