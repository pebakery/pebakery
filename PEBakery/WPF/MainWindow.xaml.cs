﻿/*
    Copyright (C) 2016-2023 Hajin Jang
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

using MahApps.Metro.IconPacks;
using PEBakery.Core;
using PEBakery.Core.ViewModels;
using PEBakery.Helper;
using PEBakery.WPF.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shell;

namespace PEBakery.WPF
{
    #region MainWindow
    public partial class MainWindow : Window
    {
        #region Fields and Properties
        // Shortcut to Global
        public static string BaseDir => Global.BaseDir;
        public static Logger Logger => Global.Logger;
        private static MainViewModel Model => Global.MainViewModel;

        // Window 
        public LogWindow? LogDialog = null;
        public UtilityWindow? UtilityDialog = null;
        public ScriptEditWindow? ScriptEditDialog = null;
        #endregion

        #region Constructor
        public MainWindow()
        {
            InitializeComponent();
            Global.MainViewModel = (MainViewModel)DataContext;

            // Init global properties
            Global.Init();
            CommandManager.InvalidateRequerySuggested();

            // Setup SystemLogHasIssue event handler
            Model.SubscribeSystemLogUpdateEvent();

            // Load Projects
            Model.StartLoadingProjects(false, false);

            // Read Window Layout/Position
            ReadLayoutFromSetting();
            SizeToFit();
            MoveIntoView();
        }
        #endregion

        #region Main Buttons
        private void ProjectBuildStartCommand_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = Model?.CurMainTree?.Script != null && !Model.WorkInProgress && !Engine.IsRunning &&
                           Global.Projects != null && Global.Projects.FullyLoaded;
        }

        private async void ProjectBuildStartCommand_Executed(object? sender, ExecutedRoutedEventArgs e)
        {
            // Force update of script interface
            ProjectBuildStartButton.Focus();

            if (Model.CurMainTree is not ProjectTreeItemModel curMainTree)
                return;

            if (!Engine.TryEnterLock())
                return;
            try
            {
                // Get current project
                Project p = curMainTree.Script.Project;

                Model.BuildTreeItems.Clear();
                ProjectTreeItemModel treeRoot = MainViewModel.PopulateOneTreeItem(p.MainScript, null, null);
                MainViewModel.ScriptListToTreeViewModel(p, p.ActiveScripts, false, treeRoot);
                Model.BuildTreeItems.Add(treeRoot);
                Model.CurBuildTree = null;

                EngineState s = new EngineState(p, Logger, Model, this);
                s.SetOptions(Global.Setting);
                s.SetCompat(p.Compat);

                Engine.WorkingEngine = new Engine(s);

                // Build Start, Switch to Build View
                Model.SwitchNormalBuildInterface = false;

                // Turn on progress ring
                Model.WorkInProgress = true;

                // Set StatusBar Text
                using (CancellationTokenSource ct = new CancellationTokenSource())
                {
                    Task printStatus = MainViewModel.PrintBuildElapsedStatus($"Building {p.ProjectName}...", s, ct.Token);

                    // Run
                    int buildId = await Engine.WorkingEngine.Run($"Project {p.ProjectName}");

#if DEBUG
                    Logger.ExportBuildLog(LogExportFormat.Text, Path.Combine(s.BaseDir, "LogDebugDump.txt"), buildId, new BuildLogOptions
                    {
                        IncludeComments = true,
                        IncludeMacros = true,
                        ShowLogFlags = true,
                    });
#endif

                    // Cancel and wait until PrintBuildElapsedStatus stops
                    ct.Cancel();
                    await printStatus;
                }

                // Turn off progress ring
                Model.WorkInProgress = false;

                // Build ended, Switch to Normal View
                Model.SwitchNormalBuildInterface = true;
                Model.BuildTreeItems.Clear();
                Model.DisplayScript(Model.CurMainTree.Script);

                // Report elapsed time
                string? reason = s.RunResultReport();
                if (reason != null)
                    Model.StatusBarText = $"{p.ProjectName} build stopped by {reason}. ({s.Elapsed:h\\:mm\\:ss})";
                else
                    Model.StatusBarText = $"{p.ProjectName} build finished. ({s.Elapsed:h\\:mm\\:ss})";

                if (Global.Setting.General.ShowLogAfterBuild && LogWindow.IsRunning() == false)
                { // Open BuildLogWindow
                    LogDialog = new LogWindow(1);
                    LogDialog.Show();
                }
            }
            finally
            {
                Engine.WorkingEngine = null;
                Engine.ExitLock();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void ProjectBuildStopCommand_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = Engine.WorkingEngine != null && Engine.IsRunning;
        }

        private void ProjectBuildStopCommand_Executed(object? sender, ExecutedRoutedEventArgs e)
        {
            // Force update of script interface
            ProjectBuildStopButton.Focus();

            // Request build to stop.
            ForceStopBuild();
        }

        private void ProjectLoading_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = Model != null && Model.IsProjectsLoading() == false;
        }

        private void ProjectRefreshCommand_Executed(object? sender, ExecutedRoutedEventArgs e)
        {
            // Force update of script interface
            ProjectRefreshButton.Focus();

            Model.StartLoadingProjects(true, false);
        }

        private void SettingWindowCommand_Executed(object? sender, ExecutedRoutedEventArgs e)
        {
            // Force update of script interface
            SettingWindowButton.Focus();

            if (Global.Projects == null)
                return;

            SettingViewModel svModel = new SettingViewModel(Global.Setting, Global.Projects);
            SettingWindow dialog = new SettingWindow(svModel) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                // Refresh Projects
                if (svModel.NeedProjectRefresh)
                {
                    Model.StartLoadingProjects(true, false);
                }
                else
                {
                    // Scale Factor
                    if (svModel.NeedScriptRedraw && Model.CurMainTree?.Script != null)
                        Model.DisplayScript(Model.CurMainTree.Script);

                    // Script
                    if (svModel.NeedScriptCaching)
                        Model.StartScriptCaching();
                }
            }
        }

        private void UtilityWindowCommand_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = Model != null && Model.IsProjectsLoading() == false && UtilityWindow.IsRunning() == false;
        }

        private void UtilityWindowCommand_Executed(object? sender, ExecutedRoutedEventArgs e)
        {
            // Force update of script interface
            UtilityWindowButton.Focus();

            UtilityDialog = new UtilityWindow(Global.Setting.Interface.MonospacedFont) { Owner = this };
            UtilityDialog.Show();
        }

        private void LogWindowCommand_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = Model != null && Model.IsProjectsLoading() == false && LogWindow.IsRunning() == false;
        }

        private void LogWindowCommand_Executed(object? sender, ExecutedRoutedEventArgs e)
        {
            // Force update of script interface
            LogWindowButton.Focus();

            // Reset TaskBar progress state when build is not running
            if (Engine.WorkingEngine == null)
                Global.MainViewModel.TaskBarProgressState = TaskbarItemProgressState.None;

            // If last build ended with issue, show build log instead of system log
            int selectedTabIndex = Global.MainViewModel.BuildEndedWithIssue ? 1 : 0;
            Global.MainViewModel.BuildEndedWithIssue = false;
            
            // Clear the flag that denotes the system log has an error.
            Global.MainViewModel.SystemLogHasIssue = false;

            LogDialog = new LogWindow(selectedTabIndex) { Owner = this };
            LogDialog.Show();
        }

        private void ProjectUpdateCommand_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
        {
            if (Model != null && !Model.WorkInProgress &&
                Global.Projects != null && Global.Projects.FullyLoaded &&
                Model.CurMainTree != null)
            {
                Project p = Model.CurMainTree.Script.Project;
                e.CanExecute = p.IsUpdateable;
            }
            else
            {
                e.CanExecute = false;
            }
        }

        private void ProjectUpdateCommand_Executed(object? sender, ExecutedRoutedEventArgs e)
        {
            // Force update of script interface
            ProjectUpdateButton.Focus();

            Model.WorkInProgress = true;
            try
            {
                MessageBox.Show(this, "To be implemented", "Sorry", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Model.WorkInProgress = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void AboutWindowCommand_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
        {
            // e.CanExecute = Global.Setting != null;
            e.CanExecute = true;
        }

        private void AboutWindowCommand_Executed(object? sender, ExecutedRoutedEventArgs e)
        {
            AboutWindow dialog = new AboutWindow(Global.Setting.Interface.MonospacedFont) { Owner = this };
            dialog.ShowDialog();
        }
        #endregion

        #region Script Button and Context Menu
        private void ScriptCommand_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = Model?.CurMainTree?.Script != null && !Model.WorkInProgress;
        }

        private void ScriptUpdateCommand_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = false;

            if (Model?.CurMainTree?.Script == null || Model.WorkInProgress)
                return;

            Script targetScript = Model.CurMainTree.Script;
            if (targetScript.Type == ScriptType.Directory)
            {
                e.CanExecute = Model.CurMainTree.IsDirectoryUpdateable();
            }
            else
            {
                e.CanExecute = targetScript.IsUpdateable;
            }
        }

        private async void ScriptRunCommand_Executed(object? sender, ExecutedRoutedEventArgs e)
        {
            // Force update of script interface
            ScriptRunButton.Focus();

            if (Model.CurMainTree is not ProjectTreeItemModel curMainTree)
                return;

            Script sc = curMainTree.Script;
            if (!sc.Sections.ContainsKey(ScriptSection.Names.Process))
            {
                Model.StatusBarText = $"Section [Process] does not exist in {sc.Title}";
                return;
            }

            if (Engine.TryEnterLock())
            { // Engine is not running, so we can start new engine
                try
                {
                    Model.BuildTreeItems.Clear();
                    ProjectTreeItemModel rootItem = MainViewModel.PopulateOneTreeItem(sc, null, null);
                    Model.BuildTreeItems.Add(rootItem);
                    Model.CurBuildTree = null;

                    EngineState s = new EngineState(sc.Project, Logger, Model, this, EngineMode.RunMainAndOne, sc);
                    s.SetOptions(Global.Setting);
                    s.SetCompat(sc.Project.Compat);

                    Engine.WorkingEngine = new Engine(s);

                    // Switch to Build View
                    Model.SwitchNormalBuildInterface = false;

                    TimeSpan t;
                    Task printStatus;
                    using (CancellationTokenSource ct = new CancellationTokenSource())
                    {
                        printStatus = MainViewModel.PrintBuildElapsedStatus($"Running {sc.Title}...", s, ct.Token);
                        // Run
                        int buildId = await Engine.WorkingEngine.Run($"{sc.Title} - Run");

#if DEBUG
                        Logger.ExportBuildLog(LogExportFormat.Text, Path.Combine(s.BaseDir, "LogDebugDump.txt"), buildId, new BuildLogOptions
                        {
                            IncludeComments = true,
                            IncludeMacros = true,
                            ShowLogFlags = true,
                        });
#endif

                        // Cancel and Wait until PrintBuildElapsedStatus stops
                        // Report elapsed time
                        t = s.Elapsed;

                        ct.Cancel();
                    }

                    // Report elapsed time
                    await printStatus;
                    string? haltReason = s.RunResultReport();
                    if (haltReason != null)
                        Model.StatusBarText = $"{sc.Title} processed in {s.Elapsed:h\\:mm\\:ss}, stopped by {haltReason}";
                    else
                        Model.StatusBarText = $"{sc.Title} processed in {s.Elapsed:h\\:mm\\:ss}";

                    // Build Ended, Switch to Normal View
                    Model.SwitchNormalBuildInterface = true;
                    Model.BuildTreeItems.Clear();
                    Model.DisplayScript(Model.CurMainTree.Script);

                    if (Global.Setting.General.ShowLogAfterBuild && LogWindow.IsRunning() == false)
                    { // Open BuildLogWindow
                        LogDialog = new LogWindow(1);
                        LogDialog.Show();
                    }
                }
                finally
                {
                    Engine.WorkingEngine = null;
                    Engine.ExitLock();
                }
            }
            else // Stop Build
            {
                // Request engine to stop the build.
                ForceStopBuild();
            }
        }

        private void ScriptRefreshCommand_Executed(object? sender, ExecutedRoutedEventArgs e)
        {
            // Force update of script interface
            ScriptRefreshButton.Focus();

            Model.StartRefreshScript();
        }

        private void ScriptEditCommand_Executed(object? sender, ExecutedRoutedEventArgs e)
        {
            // Force update of script interface
            ScriptEditButton.Focus();

            if (Model.IsTreeEntryFile)
            { // Open Context Menu
                if (e.Source is Button button && button.ContextMenu is ContextMenu menu)
                {
                    menu.PlacementTarget = button;
                    menu.IsOpen = true;
                }
            }
            else
            { // Open Folder
                MainViewCommands.ScriptExternalEditorCommand.Execute(null, null);
            }
        }

        private void ScriptInternalEditorCommand_Executed(object? sender, ExecutedRoutedEventArgs e)
        {
            // Force update of script interface
            ScriptEditButton.Focus();

            if (Model.CurMainTree is not ProjectTreeItemModel curMainTree)
                return;

            Script sc = curMainTree.Script;
            if (ScriptEditWindow.IsRunning())
                return;

            ScriptEditDialog = new ScriptEditWindow(sc, Model) { Owner = this };

            // Open as Modal
            // If ScriptEditWindow returns true in DialogResult, refresh script
            if (ScriptEditDialog.ShowDialog() == true)
            {
                sc = (Script)ScriptEditDialog.Tag;

                Model.DisplayScript(sc);
                Model.CurMainTree.Script = sc;
            }
        }

        private void ScriptExternalEditorCommand_Executed(object? sender, ExecutedRoutedEventArgs e)
        {
            // Force update of script interface
            ScriptEditButton.Focus();

            if (Model.CurMainTree is not ProjectTreeItemModel curMainTree)

                return;
            Script sc = curMainTree.Script;
            switch (sc.Type)
            {
                case ScriptType.Script:
                case ScriptType.Link:
                    MainViewModel.OpenTextFile(sc.RealPath);
                    break;
                default:
                    MainViewModel.OpenFolder(sc.RealPath);
                    break;
            }
        }

        private async void ScriptUpdateCommand_Executed(object? sender, ExecutedRoutedEventArgs e)
        {
            // Force update of script interface controls (if changed)
            ScriptUpdateButton.Focus();

            if (Model.CurMainTree is not ProjectTreeItemModel curMainTree)
                return;

            // Must be filtered by ScriptCommand_CanExecute before
            if (Model.WorkInProgress)
            {
                Global.Logger.SystemWrite(new LogInfo(LogState.CriticalError, $"Race condition with {nameof(Model.WorkInProgress)} happened in {nameof(ScriptUpdateCommand_Executed)}"));
                return;
            }

            // Get instances of Script and Project
            Script targetScript = curMainTree.Script;
            Project p = curMainTree.Script.Project;
            // Do not apply updateMultipleScript to MainScript, because users should use project update for this job.
            bool updateMultipleScript = targetScript.Type == ScriptType.Directory;
            if (!updateMultipleScript && !targetScript.IsUpdateable)
            {
                Global.Logger.SystemWrite(new LogInfo(LogState.CriticalError, $"Race condition with {nameof(Script.IsUpdateable)} happened in {nameof(ScriptUpdateCommand_Executed)}"));
                return;
            }

            // Define local variables
            Script[]? targetScripts = null;
            // Update one script
            Script? newScript = null;
            LogInfo? updaterLog = null;
            // Update scripts
            Script[]? newScripts = null;
            LogInfo[]? updaterLogs = null;

            // Turn on progress ring
            Model.WorkInProgress = true;
            try
            {
                // Populate BuildTree
                ProjectTreeItemModel treeRoot = MainViewModel.PopulateOneTreeItem(targetScript, null, null);
                Model.BuildTreeItems.Clear();
                if (updateMultipleScript)
                { // Update a list of scripts
                    // We have to search in p.AllScripts rather than in ProjectTreeItemModel to find hidden scripts
                    // (ProjectTreeItemModel only contains visible scripts)
                    targetScripts = p.AllScripts
                        .Where(x => x.TreePath.StartsWith(targetScript.TreePath, StringComparison.OrdinalIgnoreCase) &&
                                    x.IsUpdateable)
                        .ToArray();
                    MainViewModel.ScriptListToTreeViewModel(p, targetScripts, false, treeRoot);
                    targetScripts = targetScripts.Where(x => x.Type != ScriptType.Directory).ToArray();
                    if (targetScripts.Length == 0)
                    {
                        // Ask user for confirmation
                        MessageBox.Show(this,
                            $"Directory [{targetScript.Title}] does not contain any scripts that are able to be updated.",
                            "No updateable scripts",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }
                }

                Model.BuildTreeItems.Add(treeRoot);
                Model.CurBuildTree = null;
                Debug.Assert(updateMultipleScript && targetScript != null && targetScripts != null ||
                             !updateMultipleScript && targetScript != null && targetScripts == null,
                    $"Check {updateMultipleScript}");

                // Ask user for confirmation
                string targetScriptCountStr;
                if (updateMultipleScript)
                {
                    if (targetScripts == null)
                        throw new InvalidOperationException($"{nameof(targetScripts)} is null");
                    targetScriptCountStr = targetScripts.Length == 1 ? $"script [{targetScripts[0].Title}]" : $"{targetScripts.Length} scripts";
                }
                else
                {
                    targetScriptCountStr = $"script [{targetScript.Title}]";
                }
                MessageBoxResult result = MessageBox.Show(this,
                    $"Are you sure you want to update {targetScriptCountStr}?",
                    "Continue?",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (result == MessageBoxResult.No)
                    return;

                // Switch to Build View
                Model.BuildScriptFullProgressVisibility = Visibility.Collapsed;
                Model.SwitchNormalBuildInterface = false;

                Stopwatch watch = Stopwatch.StartNew();

                // Run Updater
                string? customUserAgent = Global.Setting.General.UseCustomUserAgent ? Global.Setting.General.CustomUserAgent : null;
                FileUpdater updater = new FileUpdater(p, Model, customUserAgent);
                if (updateMultipleScript) // Update a list of scripts
                {
                    if (targetScripts == null)
                        throw new InvalidOperationException($"{nameof(targetScripts)} is null");
                    (newScripts, updaterLogs) = await updater.UpdateScriptsAsync(targetScripts, true);
                    Logger.SystemWrite(updaterLogs);
                }
                else
                {
                    (newScript, updaterLog) = await updater.UpdateScriptAsync(targetScript, true);
                    Logger.SystemWrite(updaterLog);
                }

                watch.Stop();
                TimeSpan t = watch.Elapsed;
                Model.StatusBarText = $"Updated {targetScript.Title} ({t:h\\:mm\\:ss})";
            }
            finally
            {
                // Turn off progress ring
                Model.BuildScriptFullProgressVisibility = Visibility.Visible;
                Model.WorkInProgress = false;

                // Build Ended, Switch to Normal View
                Model.SwitchNormalBuildInterface = true;
                Model.DisplayScript(Model.CurMainTree.Script);
            }

            // Report results
            if (updateMultipleScript)
            { // Updated multiple scripts
                if (targetScripts == null)
                    throw new InvalidOperationException($"{nameof(targetScripts)} is null");
                if (updaterLogs == null)
                    throw new InvalidOperationException($"{nameof(updaterLogs)} is null");
                if (newScripts == null)
                    throw new InvalidOperationException($"{nameof(newScripts)} is null");

                PackIconMaterialKind msgBoxIcon = PackIconMaterialKind.Information;
                StringBuilder b = new StringBuilder(updaterLogs.Length + 6);
                if (0 < newScripts.Length)
                    b.AppendLine($"Successfully updated [{newScripts.Length}] scripts");

                foreach (Script newSc in newScripts)
                {
                    ProjectTreeItemModel? node = Model.CurMainTree.FindScriptByRealPath(newSc.RealPath);
                    if (node == null)
                        throw new InvalidOperationException($"{nameof(node)} is null");
                    Model.PostRefreshScript(node, newSc);

                    b.AppendLine($"- {newSc.Title}");
                }

                LogInfo[] errorLogs = updaterLogs.Where(x => x.State == LogState.Error).ToArray();
                if (0 < errorLogs.Length)
                { // Failure
                    if (0 < newScripts.Length)
                        b.AppendLine();
                    b.AppendLine($"Failed to update [{targetScripts.Length - newScripts.Length}] scripts");
                    foreach (LogInfo log in errorLogs)
                        b.AppendLine($"- {log.Message}");

                    msgBoxIcon = PackIconMaterialKind.Alert;
                }

                const string msgTitle = "Script Update Report";
                TextViewDialog dialog = new TextViewDialog(this, msgTitle, msgTitle, b.ToString(), msgBoxIcon);
                dialog.ShowDialog();
            }
            else
            { // Updated single script
                if (newScript != null)
                { // Success
                    ProjectTreeItemModel? node = Model.CurMainTree.FindScriptByRealPath(newScript.RealPath);
                    if (node == null)
                        throw new InvalidOperationException($"{nameof(node)} is null");
                    Model.PostRefreshScript(node, newScript);

                    MessageBox.Show(this, $"Successfully updated script {newScript.Title}",
                        "Script Update Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                { // Failure
                    if (updaterLogs == null)
                        throw new InvalidOperationException($"{nameof(updaterLogs)} is null");

                    StringBuilder b = new StringBuilder(updaterLogs.Length + 6);

                    LogInfo[] errorLogs = updaterLogs.Where(x => x.State == LogState.Error).ToArray();
                    foreach (LogInfo log in errorLogs)
                        b.AppendLine($"- {log.Message}");

                    MessageBox.Show(this, b.ToString(), "Script Update Failure", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ScriptSyntaxCheckCommand_Executed(object? sender, ExecutedRoutedEventArgs e)
        {
            // Do not use await, let it run in background
            Model.StartSyntaxCheck(false);
        }

        private void ScriptOpenFolderCommand_Executed(object? sender, ExecutedRoutedEventArgs e)
        {
            if (Model.CurMainTree is not ProjectTreeItemModel curMainTree)
                return;

            Script sc = curMainTree.Script;
            string? openPath = sc.Type == ScriptType.Directory ? sc.RealPath : Path.GetDirectoryName(sc.RealPath);
            if (openPath == null)
                throw new InvalidOperationException($"Invalid path {sc.RealPath}");
            MainViewModel.OpenFolder(openPath);
        }

        private void ScriptDirMainCommand_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = false;

            if (Model?.CurMainTree?.Script != null && !Model.WorkInProgress)
            {
                Script sc = Model.CurMainTree.Script;
                if (sc.Type == ScriptType.Directory || sc.Equals(sc.Project.MainScript))
                    e.CanExecute = true;
            }
        }

        private void DirectoryExpandTreeCommand_Executed(object? sender, ExecutedRoutedEventArgs e)
        {
            if (Model.CurMainTree is not ProjectTreeItemModel curMainTree)
                return;

            ProjectTreeItemModel selectedItem = curMainTree;
            selectedItem.IsExpanded = true;

            Queue<ProjectTreeItemModel> q = new Queue<ProjectTreeItemModel>(selectedItem.Children.Where(x => x.Script.Type == ScriptType.Directory));
            while (0 < q.Count)
            {
                ProjectTreeItemModel dirItem = q.Dequeue();
                dirItem.IsExpanded = true;

                foreach (ProjectTreeItemModel subItem in dirItem.Children.Where(x => x.Script.Type == ScriptType.Directory))
                    q.Enqueue(subItem);
            }
        }

        private void DirectoryCollapseTreeCommand_Executed(object? sender, ExecutedRoutedEventArgs e)
        {
            if (Model.CurMainTree is not ProjectTreeItemModel curMainTree)
                return;

            ProjectTreeItemModel selectedItem = curMainTree;
            selectedItem.IsExpanded = false;

            Queue<ProjectTreeItemModel> q = new Queue<ProjectTreeItemModel>(selectedItem.Children.Where(x => x.Script.Type == ScriptType.Directory));
            while (0 < q.Count)
            {
                ProjectTreeItemModel dirItem = q.Dequeue();
                dirItem.IsExpanded = false;

                foreach (ProjectTreeItemModel subItem in dirItem.Children.Where(x => x.Script.Type == ScriptType.Directory))
                    q.Enqueue(subItem);
            }
        }

        private async void CreateScriptMetaFilesCommand_Executed(object? sender, ExecutedRoutedEventArgs e)
        {
            // Force update of script interface controls (if changed)
            ScriptUpdateButton.Focus();

            if (Model.CurMainTree is not ProjectTreeItemModel curMainTree)
                return;

            // Must be filtered by ScriptCommand_CanExecute before
            if (Model.WorkInProgress)
            {
                Global.Logger.SystemWrite(new LogInfo(LogState.CriticalError, $"Race condition with {nameof(Model.WorkInProgress)} happened in {nameof(ScriptUpdateCommand_Executed)}"));
                return;
            }

            // Get instances of Script and Project
            Script targetScript = curMainTree.Script;
            Project p = curMainTree.Script.Project;

            // Define local variables
            Script[] targetScripts;
            List<LogInfo> logs = new List<LogInfo>();

            // Turn on progress ring
            Model.WorkInProgress = true;
            int successCount = 0;
            int errorCount = 0;
            try
            {
                // Populate BuildTree
                ProjectTreeItemModel treeRoot = MainViewModel.PopulateOneTreeItem(targetScript, null, null);
                Model.BuildTreeItems.Clear();
                if (targetScript.Type == ScriptType.Directory || targetScript.IsMainScript)
                { // Update a list of scripts
                    // We have to search in p.AllScripts rather than in ProjectTreeItemModel to find hidden scripts
                    // (ProjectTreeItemModel only contains visible scripts)
                    if (targetScript.IsMainScript)
                    {
                        targetScripts = p.AllScripts.ToArray();
                    }
                    else
                    {
                        targetScripts = p.AllScripts
                            .Where(x => x.TreePath.StartsWith(targetScript.TreePath, StringComparison.OrdinalIgnoreCase))
                            .ToArray();
                    }

                    MainViewModel.ScriptListToTreeViewModel(p, targetScripts, false, treeRoot);
                    targetScripts = targetScripts.Where(x => x.Type != ScriptType.Directory).ToArray();
                    if (targetScripts.Length == 0)
                    {
                        // Ask user for confirmation
                        MessageBox.Show(this,
                            $"Directory [{targetScript.Title}] does not contain any scripts.",
                            "No child scripts",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }
                }
                else
                {
                    targetScripts = new Script[] { targetScript };
                }

                Model.BuildTreeItems.Add(treeRoot);
                Model.CurBuildTree = null;

                // Switch to Build View
                Model.BuildScriptProgressVisibility = Visibility.Collapsed;
                Model.BuildFullProgressMax = targetScripts.Length;
                Model.BuildFullProgressValue = 0;
                Model.SwitchNormalBuildInterface = false;
                // I do not know why, but this line must come after SwitchNormalBuildInterface.
                Model.BuildEchoMessage = "Creating meta files...";

                Stopwatch watch = Stopwatch.StartNew();

                // Run Updater
                int idx = 0;
                foreach (Script sc in targetScripts)
                {
                    // Display script information
                    idx += 1;
                    Model.BuildFullProgressValue = idx;
                    Model.DisplayScriptTexts(sc, null);
                    Model.ScriptTitleText = Model.ScriptTitleText;
                    Model.BuildEchoMessage = $"Creating meta files... ({idx * 100 / targetScripts.Length}%)";
                    Application.Current?.Dispatcher?.BeginInvoke(() =>
                    {
                        Model.DisplayScriptLogo(sc);

                        // BuildTree is empty -> return
                        if (Model.BuildTreeItems.Count == 0)
                            return;

                        if (Model.CurBuildTree != null)
                            Model.CurBuildTree.Focus = false;
                        Model.CurBuildTree = ProjectTreeItemModel.FindScriptByRealPath(Model.BuildTreeItems[0], sc.RealPath);
                        if (Model.CurBuildTree != null)
                            Model.CurBuildTree.Focus = true;
                    });

                    // Do the real job
                    string destJsonFile = Path.ChangeExtension(sc.RealPath, ".meta.json");
                    try
                    {
                        await UpdateJson.CreateScriptUpdateJsonAsync(sc, destJsonFile);
                        logs.Add(new LogInfo(LogState.Success, $"Created meta file for [{sc.Title}]"));
                        successCount += 1;
                    }
                    catch (Exception ex)
                    {
                        logs.Add(new LogInfo(LogState.Error, $"Unable to create meta file for [{sc.Title}] - {Logger.LogExceptionMessage(ex)}"));
                        errorCount += 1;
                    }
                }

                // Log messages
                Logger.SystemWrite(logs);

                watch.Stop();
                TimeSpan t = watch.Elapsed;
                Model.StatusBarText = $"Updated {targetScript.Title} ({t:h\\:mm\\:ss})";
            }
            finally
            {
                // Turn off progress ring
                Model.WorkInProgress = false;

                // Build Ended, Switch to Normal View
                Model.BuildScriptProgressVisibility = Visibility.Visible;
                Model.BuildEchoMessage = string.Empty;
                Model.SwitchNormalBuildInterface = true;
                Model.DisplayScript(curMainTree.Script);
            }

            PackIconMaterialKind msgBoxIcon = PackIconMaterialKind.Information;
            StringBuilder b = new StringBuilder(targetScripts.Length + 4);
            b.AppendLine($"Created [{successCount}] script meta files.");

            foreach (LogInfo log in logs.Where(x => x.State == LogState.Success))
                b.AppendLine($"- {log.Message}");

            if (0 < errorCount)
            { // Failure
                b.AppendLine();
                b.AppendLine($"Failed to create [{errorCount}] script meta files");
                foreach (LogInfo log in logs.Where(x => x.State == LogState.Error))
                    b.AppendLine($"- {log.Message}");

                msgBoxIcon = PackIconMaterialKind.Alert;
            }

            const string msgTitle = "Script Meta Files Report";
            TextViewDialog dialog = new TextViewDialog(this, msgTitle, msgTitle, b.ToString(), msgBoxIcon);
            dialog.ShowDialog();
        }
        #endregion

        #region TreeView Event Handler
        private void MainTreeView_Loaded(object sender, RoutedEventArgs e)
        {
            // Register MainTreeView_KeyDown as global in MainWindow
            KeyDown += MainTreeView_KeyDown;
        }

        private void MainTreeView_Unloaded(object sender, RoutedEventArgs e)
        {
            KeyDown -= MainTreeView_KeyDown;
        }

        /// <summary>
        /// Used to ensure pressing 'Space' to toggle TreeView's checkbox.
        /// </summary>
        private void MainTreeView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space &&
                Keyboard.FocusedElement is TreeViewItem focusedItem &&
                focusedItem.DataContext is ProjectTreeItemModel node)
            {
                node.Checked = !node.Checked;

                focusedItem.IsSelected = true;
                MainTreeView.Focus();

                e.Handled = true;
            }
        }

        /// <summary>
        /// A new script is selected in MainTreeView
        /// </summary>
        private void MainTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (sender is not TreeView tree || tree.SelectedItem is not ProjectTreeItemModel selectedModel)
                return;

            Model.CurMainTree = selectedModel;
            Script sc = selectedModel.Script;

            // Display new selected script
            Dispatcher?.BeginInvoke(new Action(() =>
            {
                Stopwatch watch = Stopwatch.StartNew();
                Model.DisplayScript(sc);
                watch.Stop();

                double msec = watch.Elapsed.TotalMilliseconds;
                Model.StatusBarText = $"{sc.Title} rendered ({msec:0}ms)";
            }));

            MainTreeView.Focus();
        }

        private void MainTreeView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not DependencyObject depObj)
                return;
            TreeViewItem? selectedItem = ControlsHelper.VisualUpwardSearch<TreeViewItem>(depObj);
            if (selectedItem == null)
                return;

            // Do not invoke MainTreeView_SelectedItemChanged
            // ProjectTreeItemModel selectedModel = selectedItem.DataContext as ProjectTreeItemModel;
            // Model.CurMainTree = selectedModel;

            // Invoke MainTreeView_SelectedItemChanged
            selectedItem.Focus();
            selectedItem.IsSelected = true;

            // Disable event routing, one call to Focus() is enough
            e.Handled = true;
        }
        #endregion

        #region Window Event Handler
        private async void Window_Closing(object sender, CancelEventArgs e)
        {
            if (Engine.WorkingEngine != null)
                await Engine.WorkingEngine.ForceStopWait(false);

            // Clear SystemLogHasIssue event handler
            Model.ClearSystemLogUpdateEvent();

            // Close all sub-windows.
            if (LogDialog != null && LogWindow.IsRunning())
            {
                LogDialog.Close();
                LogDialog = null;
            }

            if (UtilityDialog != null && UtilityWindow.IsRunning())
            {
                UtilityDialog.Close();
                UtilityDialog = null;
            }

            if (ScriptEditDialog != null && ScriptEditWindow.IsRunning())
            {
                ScriptEditDialog.Close();
                ScriptEditDialog = null;
            }

            // TODO: No better way?
            while (Model.IsProjectsLoading())
                await Task.Delay(500);
            while (ScriptCache.IsRunning())
                await Task.Delay(500);
            if (Model.IsProjectsLoading())
                await Task.Delay(500);

            Global.Cleanup();

            // Save Main Window Layout/Position
            WriteLayoutToSetting();
        }

        private void BuildConOutRedirectListBox_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (sender is not ListBox listBox)
                return;
            listBox.Items.MoveCurrentToLast();
            listBox.ScrollIntoView(listBox.Items.CurrentItem);
        }
        #endregion

        #region ReadLayoutFromSetting, WriteLayoutToSetting
        public static void ReadLayoutFromSetting()
        {
            // Read Main Window Size/Layout/Position
            Global.MainViewModel.WindowTop = Global.Setting.Interface.MainWindowTop;
            Global.MainViewModel.WindowLeft = Global.Setting.Interface.MainWindowLeft;
            Global.MainViewModel.WindowWidth = Global.Setting.Interface.MainWindowWidth;
            Global.MainViewModel.WindowHeight = Global.Setting.Interface.MainWindowHeight;
            Global.MainViewModel.MainTreeViewWidth = Global.Setting.Interface.MainTreeViewWidth;
        }

        public static void WriteLayoutToSetting()
        {
            // Write Main Window Size/Layout/Position
            Global.Setting.Interface.MainWindowTop = Global.MainViewModel.WindowTop;
            Global.Setting.Interface.MainWindowLeft = Global.MainViewModel.WindowLeft;
            Global.Setting.Interface.MainWindowWidth = Global.MainViewModel.WindowWidth;
            Global.Setting.Interface.MainWindowHeight = Global.MainViewModel.WindowHeight;
            Global.Setting.Interface.MainTreeViewWidth = (int)Global.MainViewModel.MainTreeViewWidth;
            Global.Setting.WriteToFile();
        }
        #endregion

        #region SizeToFit, MoveIntoView
        /// <summary>
        /// Resize the window to fit the current monitor/resolution
        /// </summary>
        public static void SizeToFit()
        {
            if (Global.MainViewModel.WindowWidth > System.Windows.SystemParameters.VirtualScreenWidth)
            {
                Global.MainViewModel.WindowWidth = (int)System.Windows.SystemParameters.VirtualScreenWidth;
            }

            if (Global.MainViewModel.WindowHeight > System.Windows.SystemParameters.VirtualScreenHeight)
            {
                Global.MainViewModel.WindowHeight = (int)System.Windows.SystemParameters.VirtualScreenHeight;
            }
        }

        /// <summary>
        /// Move the window into the visible region of the monitor in case a user changes resolution or the number of monitors.
        /// </summary>
        public static void MoveIntoView()
        {
            // If more then half our window is off screen move it back into view
            if (Global.MainViewModel.WindowTop + Global.MainViewModel.WindowHeight / 2 > System.Windows.SystemParameters.VirtualScreenHeight)
            {
                double taskbarHeight = System.Windows.SystemParameters.VirtualScreenHeight - System.Windows.SystemParameters.WorkArea.Height;
                Global.MainViewModel.WindowTop = (int)(System.Windows.SystemParameters.VirtualScreenHeight - taskbarHeight) - Global.MainViewModel.WindowHeight;
            }

            if (Global.MainViewModel.WindowLeft + Global.MainViewModel.WindowWidth / 2 > System.Windows.SystemParameters.VirtualScreenWidth)
            {
                double taskbarWidth = System.Windows.SystemParameters.VirtualScreenWidth - System.Windows.SystemParameters.WorkArea.Width;
                Global.MainViewModel.WindowLeft = (int)(System.Windows.SystemParameters.VirtualScreenWidth - taskbarWidth) - Global.MainViewModel.WindowWidth;
            }

            // Move the window back on screen if we have a negative value.
            // This should not happen due to PEBakery checking the minimum allowed value when the settings are parsed, but better safe then sorry.
            if (Global.MainViewModel.WindowTop < 0)
            {
                Global.MainViewModel.WindowTop = 0;
            }

            if (Global.MainViewModel.WindowLeft < 0)
            {
                Global.MainViewModel.WindowLeft = 0;
            }
        }
        #endregion

        #region ForceStopBuild
        /// <summary>
        /// Request engine to stop the build.
        /// </summary>
        private void ForceStopBuild()
        {
            // Safety check
            if (Engine.WorkingEngine == null)
                return;
            if (!Engine.IsRunning)
                return;

            // Stop and wait for the build to end, or forcefully stop it immediately.
            EngineState s = Engine.WorkingEngine.State;
            if (s.HaltReturnFlags.UserHalt && s.RunningSubProcess != null)
            { // Stop is already requested, but waiting for sub-process to end
                MessageBoxResult result;
                lock (s.RunningSubProcLock)
                { // Lock s.RunningSubProcess so no one can modity the Process instance
                    // Ugly, but required to prevent null exception.
                    if (s.RunningSubProcess == null)
                        return;

                    Process proc = s.RunningSubProcess;
                    string msgBox = $"PEBakery is wating for sub-process [{proc.ProcessName}] (PID: {proc.Id}) to terminate.\r\n\r\nKilling a running process may corrupt the system, and in some instances child/grandchild processes may not be truly terminated.\r\n\r\nAre you sure you want to forcefully stop the build?";
                    result = MessageBox.Show(this, msgBox, "Force Stop?", MessageBoxButton.YesNo, MessageBoxImage.Question);

                    // Sub-process may have finished while PEBakery waited for user input.
                    // We need to make sure the build is still running.
                    if (result == MessageBoxResult.Yes && Engine.IsRunning && Engine.WorkingEngine != null && s.RunningSubProcess != null)
                    {
                        Model.ScriptDescriptionText = "Immediate build stop requested, killing the sub-process [{proc.ProcessName}]...";
                        Engine.WorkingEngine.KillSubProcess();
                    }
                }
            }
            else
            {
                // Request engine to stop the build.
                // Do not set Engine.WorkingEngine to null, it takes some time to finish a build.
                Engine.WorkingEngine.ForceStop(false);
            }
        }
        #endregion
    }
    #endregion

    #region MainViewCommands
    public static class MainViewCommands
    {
        #region Main Buttons
        public static readonly RoutedCommand ProjectBuildStartCommand = new RoutedUICommand("Build the Selected Project", "ProjectBuildStart", typeof(MainViewCommands),
            new InputGestureCollection
            {
                new KeyGesture(Key.F10),
            });
        public static readonly RoutedCommand ProjectBuildStopCommand = new RoutedUICommand("Stop Project Build", "ProjectBuildStop", typeof(MainViewCommands),
            new InputGestureCollection
            {
                new KeyGesture(Key.F10),
            });
        public static readonly RoutedCommand ProjectRefreshCommand = new RoutedUICommand("Refresh Projects (F5)", "ProjectRefresh", typeof(MainViewCommands),
            new InputGestureCollection
            {
                new KeyGesture(Key.F5),
            });
        public static readonly RoutedCommand SettingWindowCommand = new RoutedUICommand("Settings", "SettingWindow", typeof(MainViewCommands));
        public static readonly RoutedCommand LogWindowCommand = new RoutedUICommand("View Logs (Ctrl + L)", "LogWindow", typeof(MainViewCommands),
            new InputGestureCollection
            {
                new KeyGesture(Key.L, ModifierKeys.Control),
            });
        public static readonly RoutedCommand UtilityWindowCommand = new RoutedUICommand("Open Utilities", "UtilityWindow", typeof(MainViewCommands));
        public static readonly RoutedCommand ProjectUpdateCommand = new RoutedUICommand("Update Project", "ProjectUpdate", typeof(MainViewCommands));
        public static readonly RoutedCommand AboutWindowCommand = new RoutedUICommand("About PEBakery", "AboutWindow", typeof(MainViewCommands));
        #endregion

        #region Script Buttons and Context Menus
        public static readonly RoutedCommand ScriptRunCommand = new RoutedUICommand("Run Script", "ScriptRun", typeof(MainViewCommands));
        public static readonly RoutedCommand ScriptRefreshCommand = new RoutedUICommand("Refresh Script (Ctrl + F5)", "ScriptRefresh", typeof(MainViewCommands),
            new InputGestureCollection
            {
                new KeyGesture(Key.F5, ModifierKeys.Control),
            });
        public static readonly RoutedCommand ScriptEditCommand = new RoutedUICommand("Edit Script", "ScriptEdit", typeof(MainViewCommands));
        public static readonly RoutedCommand ScriptInternalEditorCommand = new RoutedUICommand("Edit Script Properties", "ScriptInternalEditor", typeof(MainViewCommands));
        public static readonly RoutedCommand ScriptExternalEditorCommand = new RoutedUICommand("Edit Script Source", "ScriptExternalEditor", typeof(MainViewCommands));
        public static readonly RoutedCommand ScriptUpdateCommand = new RoutedUICommand("Update Script", "ScriptUpdate", typeof(MainViewCommands));
        public static readonly RoutedCommand ScriptSyntaxCheckCommand = new RoutedUICommand("Syntax Check", "ScriptSyntaxCheck", typeof(MainViewCommands));
        public static readonly RoutedCommand DirectoryExpandTreeCommand = new RoutedUICommand("Collapse items", "DirectoryExpandTree", typeof(MainViewCommands));
        public static readonly RoutedCommand DirectoryCollapseTreeCommand = new RoutedUICommand("Collapse items", "DirectoryCollapseTree", typeof(MainViewCommands));
        public static readonly RoutedCommand ScriptOpenFolderCommand = new RoutedUICommand("Open Script Folder", "ScriptOpenFolder", typeof(MainViewCommands));
        public static readonly RoutedCommand CreateScriptMetaFilesCommand = new RoutedUICommand("Create Script Meta Files", "CreateScriptMetaFiles", typeof(MainViewCommands));
        #endregion
    }
    #endregion
}
