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

using MahApps.Metro.IconPacks;
using PEBakery.Core;
using PEBakery.Helper;
using PEBakery.IniLib;
using SQLite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;

namespace PEBakery.WPF
{
    #region MainWindow
    // ReSharper disable once RedundantExtendsListEntry
    public partial class MainWindow : Window
    {
        #region Constants
        internal const int ScriptAuthorLenLimit = 35;
        #endregion

        #region Variables
        public string BaseDir { get; }

        private int _projectsLoading = 0;
        private int _scriptRefreshing = 0;
        private int _syntaxChecking = 0;

        public ProjectTreeItemModel CurMainTree { get; private set; }
        public ProjectTreeItemModel CurBuildTree { get; set; }
        private UIRenderer _renderer;

        public Logger Logger { get; }
        private readonly ScriptCache _scriptCache;

        private static MainViewModel Model
        {
            get => Global.MainViewModel;
            set => Global.MainViewModel = value;
        }

        public LogWindow LogDialog = null;
        public UtilityWindow UtilityDialog = null;
        public ScriptEditWindow ScriptEditDialog = null;
        #endregion

        #region Constructor
        public MainWindow()
        {
            InitializeComponent();
            Model = DataContext as MainViewModel;
            if (Model == null)
            {
                MessageBox.Show("MainViewModel is null", "Internal Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }

            string[] args = Global.Args;
            if (!NumberHelper.ParseInt32(Properties.Resources.EngineVersion, out Global.Version))
            {
                MessageBox.Show($"Invalid version [{Global.Version}]", "Invalid Version", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }

            string argBaseDir = Environment.CurrentDirectory;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals("/basedir", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length)
                    {
                        argBaseDir = Path.GetFullPath(args[i + 1]);
                        if (Directory.Exists(argBaseDir) == false)
                        {
                            MessageBox.Show($"Directory [{argBaseDir}] does not exist", "Invalid BaseDir", MessageBoxButton.OK, MessageBoxImage.Error);
                            Environment.Exit(1); // Force Shutdown
                        }
                        Environment.CurrentDirectory = argBaseDir;
                    }
                    else
                    {
                        // ReSharper disable once LocalizableElement
                        Console.WriteLine("\'/basedir\' must be used with path\r\n");
                    }
                }
                else if (args[i].Equals("/?", StringComparison.OrdinalIgnoreCase)
                    || args[i].Equals("/help", StringComparison.OrdinalIgnoreCase)
                    || args[i].Equals("/h", StringComparison.OrdinalIgnoreCase))
                {
                    // ReSharper disable once LocalizableElement
                    Console.WriteLine("Sorry, help message not implemented\r\n");
                }
            }

            Global.BaseDir = BaseDir = argBaseDir;

            string settingFile = Path.Combine(BaseDir, "PEBakery.ini");
            Global.Setting = new SettingViewModel(settingFile);
            Model.MonospaceFont = Global.Setting.Interface_MonospaceFont;

            string dbDir = Path.Combine(BaseDir, "Database");
            if (!Directory.Exists(dbDir))
                Directory.CreateDirectory(dbDir);

            string logDbFile = Path.Combine(dbDir, "PEBakeryLog.db");
            try
            {
                Global.Logger = Logger = new Logger(logDbFile);
                Logger.SystemWrite(new LogInfo(LogState.Info, "PEBakery launched"));
            }
            catch (SQLiteException e)
            { // Update failure
                string msg = $"SQLite Error : {e.Message}\r\n\r\nLog database is corrupted.\r\nPlease delete PEBakeryLog.db and restart.";
                MessageBox.Show(msg, "SQLite Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown(1);
            }
            Global.Setting.LogDb = Logger.Db;

            // If script cache is enabled, generate cache after 5 seconds
            if (Global.Setting.Script_EnableCache)
            {
                string cacheDbFile = Path.Combine(dbDir, "PEBakeryCache.db");
                try
                {
                    _scriptCache = new ScriptCache(cacheDbFile);
                    Logger.SystemWrite(new LogInfo(LogState.Info, $"ScriptCache enabled, {_scriptCache.Table<DB_ScriptCache>().Count()} cached scripts found"));
                }
                catch (SQLiteException e)
                { // Update failure
                    string msg = $"SQLite Error : {e.Message}\r\n\r\nCache database is corrupted.\r\nPlease delete PEBakeryCache.db and restart.";
                    MessageBox.Show(msg, "SQLite Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                    Application.Current.Shutdown(1);
                }

                Global.Setting.ScriptCache = _scriptCache;
            }
            else
            {
                Logger.SystemWrite(new LogInfo(LogState.Info, "ScriptCache disabled"));
            }

            // Load Projects
            StartLoadingProjects();
        }
        #endregion

        #region Background Workers and Tasks
        public Task StartLoadingProjects(bool quiet = false)
        {
            if (_projectsLoading != 0)
                return Task.CompletedTask;

            // Number of total scripts
            int totalScriptCount = 0;
            int stage2LinkCount = 0;
            int ifaceUpdateFreq = 1;

            // Progress handler
            int loadedScriptCount = 0;
            int stage1CachedCount = 0;
            int stage2LoadedCount = 0;
            int stage2CachedCount = 0;
            IProgress<(Project.LoadReport Type, string Path)> progress = new Progress<(Project.LoadReport Type, string Path)>(x =>
            {
                Interlocked.Increment(ref loadedScriptCount);
                Model.BottomProgressBarValue = loadedScriptCount;

                int stage = 0;
                string msg = string.Empty;
                switch (x.Type)
                {
                    case Project.LoadReport.FindingScript:
                        Model.ScriptDescriptionText = "Finding script files";
                        return;
                    case Project.LoadReport.LoadingCache:
                        Model.ScriptDescriptionText = "Loading script cache";
                        return;
                    case Project.LoadReport.Stage1:
                        stage = 1;
                        msg = x.Path == null ? "Error" : $"{x.Path}";
                        break;
                    case Project.LoadReport.Stage1Cached:
                        stage = 1;
                        Interlocked.Increment(ref stage1CachedCount);
                        msg = x.Path == null ? "Cached - Error" : $"Cached - {x.Path}";
                        break;
                    case Project.LoadReport.Stage2:
                        stage = 2;
                        Interlocked.Increment(ref stage2LoadedCount);
                        msg = x.Path == null ? "Error" : $"{x.Path}";
                        break;
                    case Project.LoadReport.Stage2Cached:
                        stage = 2;
                        Interlocked.Increment(ref stage2LoadedCount);
                        Interlocked.Increment(ref stage2CachedCount);
                        msg = x.Path == null ? "Cached - Error" : $"Cached - {x.Path}";
                        break;
                }

                if (loadedScriptCount % ifaceUpdateFreq == 0)
                {
                    if (stage == 1)
                        msg = $"Stage {stage} ({loadedScriptCount} / {totalScriptCount})\r\n{msg}";
                    else
                        msg = $"Stage {stage} ({stage2LoadedCount} / {stage2LinkCount})\r\n{msg}";
                    Model.ScriptDescriptionText = msg;
                }
            });

            return Task.Run(() =>
            {
                Interlocked.Increment(ref _projectsLoading);
                if (!quiet)
                    Model.WorkInProgress = true;
                Model.SwitchStatusProgressBar = false; // Show Progress Bar
                try
                {
                    Stopwatch watch = Stopwatch.StartNew();

                    // Prepare PEBakery Loading Information
                    if (!quiet)
                    {
                        Model.ScriptTitleText = "PEBakery loading...";
                        Model.ScriptDescriptionText = string.Empty;
                    }
                    Logger.SystemWrite(new LogInfo(LogState.Info, $"Loading from [{BaseDir}]"));

                    // Load CommentProcessing Icon, Clear interfaces
                    Model.ScriptLogoIcon = PackIconMaterialKind.CommentProcessing;
                    Model.MainTreeItems.Clear();
                    Model.BuildTreeItems.Clear();
                    Application.Current.Dispatcher.Invoke(ClearScriptInterface);

                    Model.BottomProgressBarMinimum = 0;
                    Model.BottomProgressBarMaximum = 100;
                    Model.BottomProgressBarValue = 0;

                    // Init ProjectCollection
                    if (Global.Setting.Script_EnableCache && _scriptCache != null) // Use ScriptCache
                    {
                        if (_scriptCache.CheckCacheRevision(BaseDir))
                            Global.Projects = new ProjectCollection(BaseDir, _scriptCache);
                        else // Cache is invalid
                            Global.Projects = new ProjectCollection(BaseDir, null);
                    }
                    else // Do not use ScriptCache
                    {
                        Global.Projects = new ProjectCollection(BaseDir, null);
                    }

                    // Prepare by getting script paths
                    progress.Report((Project.LoadReport.FindingScript, null));
                    (totalScriptCount, stage2LinkCount) = Global.Projects.PrepareLoad();
                    ifaceUpdateFreq = totalScriptCount / 64 + 1;
                    Model.BottomProgressBarMaximum = totalScriptCount + stage2LinkCount;

                    // Load projects in parallel
                    List<LogInfo> errorLogs = Global.Projects.Load(progress);
                    Logger.SystemWrite(errorLogs);
                    Global.Setting.UpdateProjectList();

                    if (0 < Global.Projects.ProjectNames.Count)
                    { // Load success
                        // Populate TreeView
                        Dispatcher.Invoke(() =>
                        {
                            foreach (Project project in Global.Projects.ProjectList)
                            {
                                ProjectTreeItemModel projectRoot = PopulateOneTreeItem(project.MainScript, null, null);
                                ScriptListToTreeViewModel(project, project.VisibleScripts, true, projectRoot);
                                Model.MainTreeItems.Add(projectRoot);
                            }

                            int pIdx = Global.Setting.Project_DefaultIndex;
                            if (0 <= pIdx && pIdx < Model.MainTreeItems.Count)
                            {
                                CurMainTree = Model.MainTreeItems[pIdx];
                                CurMainTree.IsExpanded = true;
                                if (Global.Projects[pIdx] != null)
                                    DisplayScript(Global.Projects[pIdx].MainScript);
                            }
                            else
                            {
                                CurMainTree = null;
                            }
                        });

                        Logger.SystemWrite(new LogInfo(LogState.Info, $"Projects [{string.Join(", ", Global.Projects.ProjectList.Select(x => x.ProjectName))}] loaded"));

                        watch.Stop();
                        double t = watch.Elapsed.TotalMilliseconds / 1000.0;
                        string msg;
                        if (Global.Setting.Script_EnableCache)
                        {
                            double cachePercent = (double)(stage1CachedCount + stage2CachedCount) * 100 / (totalScriptCount + stage2LinkCount);
                            msg = $"{totalScriptCount} scripts loaded ({t:0.#}s) - {cachePercent:0.#}% cached";
                            Model.StatusBarText = msg;
                        }
                        else
                        {
                            msg = $"{totalScriptCount} scripts loaded ({t:0.#}s)";
                            Model.StatusBarText = msg;
                        }

                        Logger.SystemWrite(new LogInfo(LogState.Info, msg));
                        Logger.SystemWrite(Logger.LogSeperator);

                        // If script cache is enabled, update cache.
                        if (Global.Setting.Script_EnableCache)
                            StartScriptCaching();
                    }
                    else
                    { // Load failure
                        Model.ScriptTitleText = "Unable to find project.";
                        Model.ScriptDescriptionText = $"Please provide project in [{Global.Projects.ProjectRoot}]";
                        Model.StatusBarText = "Unable to find project.";
                    }
                }
                finally
                {
                    if (!quiet)
                        Model.WorkInProgress = false;
                    Model.SwitchStatusProgressBar = true; // Show Status Bar
                    Interlocked.Decrement(ref _projectsLoading);
                }
            });
        }

        private Task StartScriptCaching()
        {
            if (ScriptCache.DbLock != 0)
                return Task.CompletedTask;

            return Task.Run(() =>
            {
                Interlocked.Increment(ref ScriptCache.DbLock);
                Model.WorkInProgress = true;
                try
                {
                    Stopwatch watch = Stopwatch.StartNew();
                    (_, int updatedCount) = _scriptCache.CacheScripts(Global.Projects, BaseDir);
                    watch.Stop();

                    double t = watch.Elapsed.TotalMilliseconds / 1000.0;
                    Logger.SystemWrite(new LogInfo(LogState.Info, $"{updatedCount} script cache updated ({t:0.###}s)"));
                    Logger.SystemWrite(Logger.LogSeperator);
                }
                finally
                {
                    Model.WorkInProgress = false;
                    Interlocked.Decrement(ref ScriptCache.DbLock);
                }
            });
        }

        public Task StartRefreshScript()
        {
            if (CurMainTree?.Script == null)
                return Task.CompletedTask;
            if (CurMainTree.Script.Type == ScriptType.Directory)
                return Task.CompletedTask;
            if (_scriptRefreshing != 0)
                return Task.CompletedTask;

            ProjectTreeItemModel node = CurMainTree;
            return Task.Run(() =>
            {
                Interlocked.Increment(ref _scriptRefreshing);
                Model.WorkInProgress = true;
                try
                {
                    Stopwatch watch = Stopwatch.StartNew();

                    Script sc = node.Script;
                    if (sc.Type != ScriptType.Directory)
                        sc = sc.Project.RefreshScript(node.Script);

                    watch.Stop();
                    double t = watch.Elapsed.TotalSeconds;

                    if (sc != null)
                    {
                        PostRefreshScript(node, sc);
                        Model.StatusBarText = $"{Path.GetFileName(node.Script.TreePath)} reloaded. ({t:0.000}s)";
                    }
                    else
                    {
                        Model.StatusBarText = $"{Path.GetFileName(node.Script.TreePath)} reload failed. ({t:0.000}s)";
                    }
                }
                finally
                {
                    Model.WorkInProgress = false;
                    Interlocked.Decrement(ref _scriptRefreshing);
                }
            });
        }

        private void PostRefreshScript(ProjectTreeItemModel node, Script sc)
        {
            node.Script = sc;
            node.ParentCheckedPropagation();
            Application.Current.Dispatcher.Invoke(() =>
            {
                UpdateTreeViewIcon(node);
                DisplayScript(node.Script);
            });
        }

        private Task StartSyntaxCheck(bool quiet)
        {
            if (CurMainTree?.Script == null)
                return Task.CompletedTask;
            if (_syntaxChecking != 0)
                return Task.CompletedTask;

            Script sc = CurMainTree.Script;
            if (sc.Type == ScriptType.Directory)
                return Task.CompletedTask;

            if (!quiet)
                Model.WorkInProgress = true;

            return Task.Run(() =>
            {
                Interlocked.Increment(ref _syntaxChecking);
                try
                {
                    CodeValidator v = new CodeValidator(sc);
                    (List<LogInfo> logs, CodeValidator.Result result) = v.Validate();
                    LogInfo[] errorLogs = logs.Where(x => x.State == LogState.Error).ToArray();
                    LogInfo[] warnLogs = logs.Where(x => x.State == LogState.Warning).ToArray();

                    int errorWarns = errorLogs.Length + warnLogs.Length;
                    StringBuilder b = new StringBuilder();
                    if (0 < errorLogs.Length)
                    {
                        if (!quiet)
                        {
                            b.AppendLine($"{errorLogs.Length} syntax error detected at [{sc.TreePath}]");
                            b.AppendLine();
                            for (int i = 0; i < errorLogs.Length; i++)
                            {
                                LogInfo log = errorLogs[i];
                                b.Append($"[{i + 1}/{errorLogs.Length}] {log.Message}");
                                if (log.Command != null)
                                {
                                    b.Append($" ({log.Command})");
                                    if (0 < log.Command.LineIdx)
                                        b.Append($" (Line {log.Command.LineIdx})");
                                }
                                else if (log.UIControl != null)
                                {
                                    b.Append($" ({log.UIControl})");
                                    if (0 < log.UIControl.LineIdx)
                                        b.Append($" (Line {log.UIControl.LineIdx})");
                                }
                                b.AppendLine();
                            }
                            b.AppendLine();
                        }
                    }

                    if (0 < warnLogs.Length)
                    {
                        if (!quiet)
                        {
                            b.AppendLine($"{warnLogs.Length} syntax warning detected at [{sc.TreePath}]");
                            b.AppendLine();
                            for (int i = 0; i < warnLogs.Length; i++)
                            {
                                LogInfo log = warnLogs[i];
                                b.Append($"[{i + 1}/{warnLogs.Length}] {log.Message}");
                                if (log.Command != null)
                                {
                                    b.Append($" ({log.Command})");
                                    if (0 < log.Command.LineIdx)
                                        b.Append($" (Line {log.Command.LineIdx})");
                                }
                                else if (log.UIControl != null)
                                {
                                    b.Append($" ({log.UIControl})");
                                    if (0 < log.UIControl.LineIdx)
                                        b.Append($" (Line {log.UIControl.LineIdx})");
                                }
                                b.AppendLine();
                            }
                            b.AppendLine();
                        }
                    }

                    Model.ScriptCheckResult = result;
                    if (!quiet)
                    {
                        switch (result)
                        {
                            case CodeValidator.Result.Clean:
                                b.AppendLine("No syntax issue detected");
                                b.AppendLine();
                                b.AppendLine($"Section Coverage : {v.Coverage * 100:0.#}% ({v.VisitedSectionCount}/{v.CodeSectionCount})");
                                MessageBox.Show(b.ToString(), "Syntax Check", MessageBoxButton.OK, MessageBoxImage.Information);
                                break;
                            case CodeValidator.Result.Warning:
                            case CodeValidator.Result.Error:
                                string dialogMsg = $"{errorWarns} syntax {(errorWarns == 1 ? "issue" : "issues")} detected!\r\n\r\nOpen logs?";
                                MessageBoxImage dialogIcon = result == CodeValidator.Result.Error ? MessageBoxImage.Error : MessageBoxImage.Exclamation;
                                MessageBoxResult dialogResult = MessageBox.Show(dialogMsg, "Syntax Check", MessageBoxButton.OKCancel, dialogIcon);
                                if (dialogResult == MessageBoxResult.OK)
                                {
                                    b.AppendLine($"Section Coverage : {v.Coverage * 100:0.#}% ({v.VisitedSectionCount}/{v.CodeSectionCount})");

                                    string tempFile = Path.GetTempFileName();
                                    File.Delete(tempFile);
                                    tempFile = Path.GetTempFileName().Replace(".tmp", ".txt");
                                    using (StreamWriter sw = new StreamWriter(tempFile, false, Encoding.UTF8))
                                        sw.Write(b.ToString());

                                    OpenTextFile(tempFile);
                                }
                                break;
                        }

                        Model.WorkInProgress = false;
                    }
                }
                finally
                {
                    Interlocked.Decrement(ref _syntaxChecking);
                }
            });
        }
        #endregion

        #region DisplayScript
        public void DisplayScript(Script sc)
        {
            DisplayScriptLogo(sc);
            DisplayScriptTexts(sc, Model, null);

            Model.ScriptCheckResult = CodeValidator.Result.Unknown;
            if (sc.Type == ScriptType.Directory)
            {
                ClearScriptInterface();
            }
            else
            {
                DisplayScriptInerface(sc);

                // Run CodeValidator
                // Do not use await, let it run in background
                if (Global.Setting.Script_AutoSyntaxCheck)
                    StartSyntaxCheck(true);
            }

            Model.IsTreeEntryFile = sc.Type != ScriptType.Directory;
            Model.OnPropertyUpdate(nameof(MainViewModel.MainCanvas));
        }

        public void DisplayScriptInerface(Script sc)
        {
            // Set scale factor
            double scaleFactor = Global.Setting.Interface_ScaleFactor / 100;
            ScaleTransform scale;
            if (scaleFactor - 1 < double.Epsilon)
                scale = new ScaleTransform(1, 1);
            else
                scale = new ScaleTransform(scaleFactor, scaleFactor);
            Model.MainCanvas.LayoutTransform = scale;

            // Render script interface
            ClearScriptInterface();
            _renderer = new UIRenderer(Model.MainCanvas, this, sc, scaleFactor, true, Global.Setting.Compat_IgnoreWidthOfWebLabel);
            _renderer.Render();
        }

        public void ClearScriptInterface()
        {
            if (_renderer == null)
                return;

            _renderer.Clear();
            _renderer = null;
        }

        public void DisplayScriptLogo(Script sc)
        {
            if (sc.Type == ScriptType.Directory)
            {
                if (sc.IsDirLink)
                    Model.ScriptLogoIcon = PackIconMaterialKind.FolderMove;
                else
                    Model.ScriptLogoIcon = PackIconMaterialKind.Folder;
            }
            else
            {
                try
                {
                    const double svgSize = 100 * UIRenderer.MaxDpiScale;
                    Model.ScriptLogoImage = EncodedFile.ExtractLogoImageSource(sc, svgSize);
                }
                catch
                { // No logo file - use default
                    PackIconMaterialKind iconKind = PackIconMaterialKind.None;
                    if (sc.Type == ScriptType.Script)
                    {
                        if (sc.IsDirLink)
                            iconKind = PackIconMaterialKind.FileSend;
                        else
                            iconKind = PackIconMaterialKind.FileDocument;
                    }
                    else if (sc.Type == ScriptType.Link)
                    {
                        iconKind = PackIconMaterialKind.FileSend;
                    }

                    Model.ScriptLogoIcon = iconKind;
                }
            }
        }

        /// <summary>
        /// Display script title, description, version and author
        /// </summary>
        /// <param name="sc">Source script to read information</param>
        /// <param name="m">MainViewModel of MainWindow</param>
        /// <param name="s">Set to non-null to notify running in build mode</param>
        public static void DisplayScriptTexts(Script sc, MainViewModel m, EngineState s)
        {
            if (sc.Type == ScriptType.Directory && s == null)
            { // In build mode, there are no directory scripts
                m.ScriptTitleText = StringEscaper.Unescape(sc.Title);
                m.ScriptDescriptionText = string.Empty;
                m.ScriptVersionText = string.Empty;
                m.ScriptAuthorText = string.Empty;
            }
            else
            {
                // Script Title
                if (s != null && s.RunMode == EngineMode.RunAll)
                    m.ScriptTitleText = $"({s.CurrentScriptIdx + 1}/{s.Scripts.Count}) {StringEscaper.Unescape(sc.Title)}";
                else
                    m.ScriptTitleText = StringEscaper.Unescape(sc.Title);

                // Script Description
                m.ScriptDescriptionText = StringEscaper.Unescape(sc.Description);

                // Script Version
                string verStr = StringEscaper.ProcessVersionString(sc.Version);
                if (verStr == null)
                {
                    if (s != null)
                    { // Normal mode -> Notify script developer to fix
                        m.ScriptVersionText = "Error";
                        Global.Logger.SystemWrite(new LogInfo(LogState.Error, $"Script [{sc.Title}] contains invalid version string [{sc.Version}]"));
                    }
                    else
                    { // Build mode -> Suppress error log
                        m.ScriptVersionText = sc.Version;
                    }
                }
                else
                {
                    m.ScriptVersionText = $"v{verStr}";
                }

                // Script Author
                string author = StringEscaper.Unescape(sc.Author);
                if (ScriptAuthorLenLimit < author.Length)
                    m.ScriptAuthorText = author.Substring(0, ScriptAuthorLenLimit) + "...";
                else
                    m.ScriptAuthorText = author;
            }
        }
        #endregion

        #region Main Buttons
        private async void BuildButton_Click(object sender, RoutedEventArgs e)
        {
            if (Engine.WorkingLock == 0)  // Start Build
            {
                Interlocked.Increment(ref Engine.WorkingLock);

                if (CurMainTree?.Script == null || Model.WorkInProgress)
                {
                    Interlocked.Decrement(ref Engine.WorkingLock);
                    return;
                }

                // Determine current project
                Project p = CurMainTree.Script.Project;

                Model.BuildTreeItems.Clear();
                ProjectTreeItemModel treeRoot = PopulateOneTreeItem(p.MainScript, null, null);
                ScriptListToTreeViewModel(p, p.ActiveScripts, false, treeRoot);
                Model.BuildTreeItems.Add(treeRoot);
                CurBuildTree = null;

                EngineState s = new EngineState(p, Logger, Model);
                s.SetOptions(Global.Setting);

                Engine.WorkingEngine = new Engine(s);

                // Build Start, Switch to Build View
                Model.SwitchNormalBuildInterface = false;

                // Turn on progress ring
                Model.WorkInProgress = true;

                // Set StatusBar Text
                CancellationTokenSource ct = new CancellationTokenSource();
                Task printStatus = PrintBuildElapsedStatus($"Building {p.ProjectName}...", s, ct.Token);

                // Run
                int buildId = await Engine.WorkingEngine.Run($"Project {p.ProjectName}");

#if DEBUG
                Logger.ExportBuildLog(LogExportType.Text, Path.Combine(s.BaseDir, "LogDebugDump.txt"), buildId, new LogExporter.BuildLogOptions
                {
                    IncludeComments = true,
                    IncludeMacros = true,
                });
#endif

                // Cancel and Wait until PrintBuildElapsedStatus stops
                // Report elapsed time
                ct.Cancel();
                await printStatus;
                Model.StatusBarText = $"{p.ProjectName} build done ({s.Elapsed:h\\:mm\\:ss})";

                // Turn off progress ring
                Model.WorkInProgress = false;

                // Build Ended, Switch to Normal View
                Model.SwitchNormalBuildInterface = true;
                Model.BuildTreeItems.Clear();
                DisplayScript(CurMainTree.Script);

                if (Global.Setting.General_ShowLogAfterBuild && LogWindow.Count == 0)
                { // Open BuildLogWindow
                    LogDialog = new LogWindow(1);
                    LogDialog.Show();
                }

                Engine.WorkingEngine = null;
                Interlocked.Decrement(ref Engine.WorkingLock);
            }
            else // Stop Build
            {
                Engine.WorkingEngine?.ForceStop();
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (_projectsLoading != 0)
                return;

            (MainTreeView.DataContext as ProjectTreeItemModel)?.Children.Clear();

            StartLoadingProjects();
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private void SettingButton_Click(object sender, RoutedEventArgs e)
        {
            if (_projectsLoading != 0)
                return;

            double old_Interface_ScaleFactor = Global.Setting.Interface_ScaleFactor;
            bool old_Compat_AsteriskBugDirLink = Global.Setting.Compat_AsteriskBugDirLink;
            bool old_Compat_OverridableFixedVariables = Global.Setting.Compat_OverridableFixedVariables;
            bool old_Compat_EnableEnvironmentVariables = Global.Setting.Compat_EnableEnvironmentVariables;
            bool old_Script_EnableCache = Global.Setting.Script_EnableCache;

            SettingWindow dialog = new SettingWindow { Owner = this };
            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                // Apply
                Global.Setting.ApplySetting();

                // Refresh Projects
                if (old_Compat_AsteriskBugDirLink != Global.Setting.Compat_AsteriskBugDirLink ||
                    old_Compat_OverridableFixedVariables != Global.Setting.Compat_OverridableFixedVariables ||
                    old_Compat_EnableEnvironmentVariables != Global.Setting.Compat_EnableEnvironmentVariables)
                {
                    StartLoadingProjects();
                }
                else
                {
                    // Scale Factor
                    double newScaleFactor = Global.Setting.Interface_ScaleFactor;
                    if (double.Epsilon < Math.Abs(newScaleFactor - old_Interface_ScaleFactor)) // Not Equal
                        DisplayScript(CurMainTree.Script);

                    // Script
                    if (!old_Script_EnableCache && Global.Setting.Script_EnableCache)
                        StartScriptCaching();
                }
            }
        }

        private void UtilityButton_Click(object sender, RoutedEventArgs e)
        {
            if (_projectsLoading != 0)
                return;
            if (0 < UtilityWindow.Count)
                return;

            UtilityDialog = new UtilityWindow(Global.Setting.Interface_MonospaceFont) { Owner = this };
            UtilityDialog.Show();
        }

        private void LogButton_Click(object sender, RoutedEventArgs e)
        {
            if (LogWindow.Count == 0)
            {
                LogDialog = new LogWindow { Owner = this };
                LogDialog.Show();
            }
        }

        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (Model.WorkInProgress)
                return;

            /*
            Model.WorkInProgress = true;

            Model.WorkInProgress = false;
            */

            MessageBox.Show("To be implemented", "Sorry", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            AboutWindow dialog = new AboutWindow(Global.Setting.Interface_MonospaceFont) { Owner = this };
            dialog.ShowDialog();
        }
        #endregion

        #region Script Buttons
        private async void ScriptRunButton_Click(object sender, RoutedEventArgs e)
        {
            if (CurMainTree?.Script == null || Model.WorkInProgress)
                return;

            Script sc = CurMainTree.Script;
            if (sc.Sections.ContainsKey("Process"))
            {
                if (Engine.WorkingLock == 0)  // Start Build
                {
                    Interlocked.Increment(ref Engine.WorkingLock);

                    // Populate BuildTree
                    Model.BuildTreeItems.Clear();
                    ProjectTreeItemModel rootItem = PopulateOneTreeItem(sc, null, null);
                    Model.BuildTreeItems.Add(rootItem);
                    CurBuildTree = null;

                    EngineState s = new EngineState(sc.Project, Logger, Model, EngineMode.RunMainAndOne, sc);
                    s.SetOptions(Global.Setting);

                    Engine.WorkingEngine = new Engine(s);

                    // Switch to Build View
                    Model.SwitchNormalBuildInterface = false;
                    CancellationTokenSource ct = new CancellationTokenSource();
                    Task printStatus = PrintBuildElapsedStatus($"Running {sc.Title}...", s, ct.Token);

                    // Run
                    int buildId = await Engine.WorkingEngine.Run($"{sc.Title} - Run");

#if DEBUG
                    Logger.ExportBuildLog(LogExportType.Text, Path.Combine(s.BaseDir, "LogDebugDump.txt"), buildId, new LogExporter.BuildLogOptions
                    {
                        IncludeComments = true,
                        IncludeMacros = true,
                    });
#endif

                    // Cancel and Wait until PrintBuildElapsedStatus stops
                    // Report elapsed time
                    TimeSpan t = s.Elapsed;

                    ct.Cancel();
                    await printStatus;
                    Model.StatusBarText = $"{sc.Title} took {t:h\\:mm\\:ss}";

                    // Build Ended, Switch to Normal View
                    Model.SwitchNormalBuildInterface = true;
                    Model.BuildTreeItems.Clear();
                    DisplayScript(CurMainTree.Script);

                    if (Global.Setting.General_ShowLogAfterBuild && LogWindow.Count == 0)
                    { // Open BuildLogWindow
                        LogDialog = new LogWindow(1);
                        LogDialog.Show();
                    }

                    Engine.WorkingEngine = null;
                    Interlocked.Decrement(ref Engine.WorkingLock);
                }
                else // Stop Build
                {
                    Engine.WorkingEngine?.ForceStop();
                }
            }
            else
            {
                Model.StatusBarText = $"Section [Process] does not exist in {sc.Title}";
            }
        }

        private void ScriptRefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (CurMainTree?.Script == null)
                return;
            if (Model.WorkInProgress)
                return;

            StartRefreshScript();
        }

        private void ScriptEditButton_Click(object sender, RoutedEventArgs e)
        {
            if (CurMainTree?.Script == null)
                return;
            if (Model.WorkInProgress)
                return;

            if (Model.IsTreeEntryFile)
            { // Open Context Menu
                if (sender is Button button && button.ContextMenu is ContextMenu menu)
                {
                    menu.PlacementTarget = button;
                    menu.IsOpen = true;
                }
            }
            else
            { // Open Folder
                ScriptExternalEditor_Click(sender, e);
            }

            e.Handled = true;
        }

        private void ScriptInternalEditor_Click(object sender, RoutedEventArgs e)
        {
            if (CurMainTree?.Script == null)
                return;
            if (Model.WorkInProgress)
                return;

            Script sc = CurMainTree.Script;
            if (ScriptEditWindow.Count == 0)
            {
                ScriptEditDialog = new ScriptEditWindow(sc);

                // Open as Modal
                // If ScriptEditWindow returns true in DialogResult, refresh script
                if (ScriptEditDialog.ShowDialog() == true)
                {
                    sc = ScriptEditDialog.Tag as Script;
                    Debug.Assert(sc != null, $"{nameof(sc)} != null");

                    DisplayScript(sc);
                    CurMainTree.Script = sc;
                }
            }
        }

        private void ScriptExternalEditor_Click(object sender, RoutedEventArgs e)
        {
            if (CurMainTree?.Script == null)
                return;
            if (Model.WorkInProgress)
                return;

            Script sc = CurMainTree.Script;
            switch (sc.Type)
            {
                case ScriptType.Script:
                case ScriptType.Link:
                    OpenTextFile(sc.RealPath);
                    break;
                default:
                    OpenFolder(sc.RealPath);
                    break;
            }
        }

        private void ScriptUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (CurMainTree?.Script == null)
                return;
            if (Model.WorkInProgress)
                return;

            MessageBox.Show("To be implemented", "Sorry", MessageBoxButton.OK, MessageBoxImage.Error);
            /*
            Script sc = CurMainTree.Script;
            Project p = CurMainTree.Script.Project;

            Model.BuildTree.Children.Clear();
            ScriptListToTreeViewModel(p, new List<Script> { sc }, false, Model.BuildTree, null);
            CurBuildTree = null;

            // Switch to Build View
            Model.BuildScriptFullProgressVisibility = Visibility.Collapsed;
            Model.SwitchNormalBuildInterface = false;

            // Turn on progress ring
            Model.WorkInProgress = true;

            Stopwatch watch = Stopwatch.StartNew();

            Script newScript = null;
            string msg = string.Empty;
            Task task = Task.Run(() =>
            {
                FileUpdaterOptions opts = new FileUpdaterOptions { Model = Model };
                if (Setting.General_UseCustomUserAgent)
                    opts.UserAgent = Setting.General_CustomUserAgent;

                (newScript, msg) = FileUpdater.UpdateScript(p, sc, opts);
            });
            task.Wait();

            if (newScript == null)
            { // Failure
                MessageBox.Show(msg, "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Global.Logger.SystemWrite(new LogInfo(LogState.Error, msg));
            }
            else
            {
                PostRefreshScript(CurMainTree, newScript);

                MessageBox.Show(msg, "Update Success", MessageBoxButton.OK, MessageBoxImage.Information);
                Global.Logger.SystemWrite(new LogInfo(LogState.Success, msg));
            }

            watch.Stop();
            TimeSpan t = watch.Elapsed;
            Model.StatusBarText = $"{p.ProjectName} build done ({t:h\\:mm\\:ss})";

            // Turn off progress ring
            Model.BuildScriptFullProgressVisibility = Visibility.Visible;
            Model.WorkInProgress = false;

            // Build Ended, Switch to Normal View
            Model.SwitchNormalBuildInterface = true;
            DrawScript(CurMainTree.Script);
            */
        }

        private void ScriptCheckButton_Click(object sender, RoutedEventArgs e)
        {
            if (CurMainTree?.Script == null)
                return;
            if (Model.WorkInProgress)
                return;

            // Do not use await, let it run in background
            StartSyntaxCheck(false);
        }

        private void ScriptOpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (CurMainTree?.Script == null)
                return;
            if (Model.WorkInProgress)
                return;

            Script sc = CurMainTree.Script;
            if (sc.Type == ScriptType.Directory)
                OpenFolder(sc.RealPath);
            else
                OpenFolder(Path.GetDirectoryName(sc.RealPath));
        }
        #endregion

        #region TreeView Methods
        private void ScriptListToTreeViewModel(Project project, List<Script> scList, bool assertDirExist, ProjectTreeItemModel projectRoot)
        {
            Dictionary<string, ProjectTreeItemModel> dirDict = new Dictionary<string, ProjectTreeItemModel>(StringComparer.OrdinalIgnoreCase);

            // Populate MainScript
            if (projectRoot == null)
                projectRoot = PopulateOneTreeItem(project.MainScript, null, null);

            foreach (Script sc in scList.Where(x => x.Type != ScriptType.Directory))
            {
                Debug.Assert(sc != null);

                if (sc.Equals(project.MainScript))
                    continue;

                // Current Parent
                ProjectTreeItemModel treeParent = projectRoot;

                int idx = sc.TreePath.IndexOf('\\');
                if (idx == -1)
                    continue;
                string[] paths = sc.TreePath
                    .Substring(idx + 1)
                    .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                // Ex) Apps\Network\Mozilla_Firefox_CR.script
                for (int i = 0; i < paths.Length - 1; i++)
                {
                    string pathKey = Project.PathKeyGenerator(paths, i);
                    string key = $"{sc.Level}_{pathKey}";
                    if (dirDict.ContainsKey(key))
                    {
                        treeParent = dirDict[key];
                    }
                    else
                    {
                        string treePath = Path.Combine(project.ProjectName, pathKey);
                        Script ts = scList.FirstOrDefault(x => x.TreePath.Equals(treePath, StringComparison.OrdinalIgnoreCase));
                        Script dirScript;

                        if (assertDirExist)
                            Debug.Assert(ts != null, $"{nameof(ts)} is null (MainWindow.ScriptListToTreeViewModel)");

                        if (ts != null)
                        {
                            dirScript = new Script(ScriptType.Directory, ts.RealPath, ts.TreePath, project, sc.Level, false, false, ts.IsDirLink);
                        }
                        else
                        {
                            string fullTreePath = Path.Combine(project.ProjectRoot, treePath);
                            dirScript = new Script(ScriptType.Directory, fullTreePath, treePath, project, sc.Level, false, false, sc.IsDirLink);
                        }

                        treeParent = PopulateOneTreeItem(dirScript, projectRoot, treeParent);
                        dirDict[key] = treeParent;
                    }
                }

                PopulateOneTreeItem(sc, projectRoot, treeParent);
            }

            // Reflect Directory's Selected value
            RecursiveDecideDirectorySelectedValue(projectRoot);
        }

        private static SelectedState RecursiveDecideDirectorySelectedValue(ProjectTreeItemModel parent)
        {
            SelectedState final = SelectedState.None;
            foreach (ProjectTreeItemModel item in parent.Children)
            {
                if (0 < item.Children.Count)
                {
                    // Has child scripts
                    SelectedState state = RecursiveDecideDirectorySelectedValue(item);
                    switch (state)
                    {
                        case SelectedState.True:
                            final = item.Script.Selected = SelectedState.True;
                            break;
                        case SelectedState.False:
                            if (final != SelectedState.True)
                                final = SelectedState.False;
                            if (item.Script.Selected != SelectedState.True)
                                item.Script.Selected = SelectedState.False;
                            break;
                    }
                }
                else // Does not have child script
                {
                    switch (item.Script.Selected)
                    {
                        case SelectedState.True:
                            final = SelectedState.True;
                            break;
                        case SelectedState.False:
                            if (final == SelectedState.None)
                                final = SelectedState.False;
                            break;
                    }
                }
            }

            return final;
        }

        public void UpdateScriptTree(Project project, bool redrawProject, bool assertDirExist = true)
        {
            ProjectTreeItemModel projectRoot = Model.MainTreeItems.FirstOrDefault(x => x.Script.Project.Equals(project));
            if (projectRoot == null)
                return; // Unable to continue

            projectRoot.Children.Clear();
            ScriptListToTreeViewModel(project, project.VisibleScripts, assertDirExist, projectRoot);

            if (redrawProject)
            {
                CurMainTree = projectRoot;
                CurMainTree.IsExpanded = true;
                DisplayScript(CurMainTree.Script);
            }
        }

        public ProjectTreeItemModel PopulateOneTreeItem(Script sc, ProjectTreeItemModel projectRoot, ProjectTreeItemModel parent)
        {
            ProjectTreeItemModel item = new ProjectTreeItemModel(projectRoot, parent)
            {
                Script = sc,
            };
            UpdateTreeViewIcon(item);
            parent?.Children.Add(item);

            return item;
        }

        public static ProjectTreeItemModel UpdateTreeViewIcon(ProjectTreeItemModel item)
        {
            Script sc = item.Script;

            if (sc.Type == ScriptType.Directory)
            {
                if (sc.IsDirLink)
                    item.Icon = PackIconMaterialKind.FolderMove;
                else
                    item.Icon = PackIconMaterialKind.Folder;
            }
            else if (sc.Type == ScriptType.Script)
            {
                if (sc.IsMainScript)
                    item.Icon = PackIconMaterialKind.Settings;
                else
                {
                    if (sc.IsDirLink)
                    {
                        if (sc.Mandatory)
                            item.Icon = PackIconMaterialKind.LockOutline;
                        else
                            item.Icon = PackIconMaterialKind.OpenInNew;
                    }
                    else
                    {
                        if (sc.Mandatory)
                            item.Icon = PackIconMaterialKind.LockOutline;
                        else
                            item.Icon = PackIconMaterialKind.File;
                    }
                }
            }
            else if (sc.Type == ScriptType.Link)
                item.Icon = PackIconMaterialKind.OpenInNew;
            else // Error
                item.Icon = PackIconMaterialKind.WindowClose;

            return item;
        }

        private void MainTreeView_Loaded(object sender, RoutedEventArgs e)
        {
            KeyDown += MainTreeView_KeyDown;
        }

        /// <summary>
        /// Used to ensure pressing 'Space' to toggle TreeView's checkbox.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainTreeView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                if (Keyboard.FocusedElement is FrameworkElement focusedElement)
                {
                    if (focusedElement.DataContext is ProjectTreeItemModel node)
                    {
                        node.Checked = !node.Checked;
                        e.Handled = true;
                    }
                }
            }
        }

        private void MainTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (sender is TreeView tree && tree.SelectedItem is ProjectTreeItemModel model)
            {
                ProjectTreeItemModel item = CurMainTree = model;

                Dispatcher.Invoke(() =>
                {
                    Stopwatch watch = new Stopwatch();
                    watch.Start();
                    DisplayScript(item.Script);
                    watch.Stop();
                    double msec = watch.Elapsed.TotalMilliseconds;
                    string filename = Path.GetFileName(CurMainTree.Script.TreePath);
                    Model.StatusBarText = $"{filename} rendered ({msec:0}ms)";
                });
            }
        }

        private void MainTreeView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            TreeViewItem treeViewItem = ControlsHelper.VisualUpwardSearch<TreeViewItem>(e.OriginalSource as DependencyObject);

            if (treeViewItem != null)
            {
                treeViewItem.Focus();
                treeViewItem.IsSelected = true;
                e.Handled = true;
            }
        }
        #endregion

        #region OpenTextFile, OpenFolder
        public void OpenTextFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                MessageBox.Show($"File [{filePath}] does not exist!", "Invalid Path", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (Global.Setting.Interface_UseCustomEditor)
            {
                string ext = Path.GetExtension(Global.Setting.Interface_CustomEditorPath);
                if (ext != null && !ext.Equals(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show($"Custom editor [{Global.Setting.Interface_CustomEditorPath}] is not a executable!", "Invalid Custom Editor", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!File.Exists(Global.Setting.Interface_CustomEditorPath))
                {
                    MessageBox.Show($"Custom editor [{Global.Setting.Interface_CustomEditorPath}] does not exist!", "Invalid Custom Editor", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                ProcessStartInfo info = new ProcessStartInfo
                {
                    UseShellExecute = false,
                    FileName = Global.Setting.Interface_CustomEditorPath,
                    Arguments = StringEscaper.Doublequote(filePath),
                };

                try { UACHelper.UACHelper.StartWithShell(info); }
                catch { Process.Start(info); }
            }
            else
            {
                FileHelper.OpenPath(filePath);
            }
        }

        public void OpenFolder(string filePath)
        {
            if (!Directory.Exists(filePath))
            {
                MessageBox.Show($"Directory [{filePath}] does not exist!", "Invalid Path", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Process proc = new Process
            {
                StartInfo = new ProcessStartInfo(filePath)
            };
            proc.Start();
        }
        #endregion

        #region PrintBuildElapsedStatus
        public static Task PrintBuildElapsedStatus(string msg, EngineState s, CancellationToken token)
        {
            return Task.Run(() =>
            {
                while (true)
                {
                    TimeSpan t = DateTime.UtcNow - s.StartTime;
                    s.MainViewModel.StatusBarText = $"{msg} ({t:h\\:mm\\:ss})";

                    if (token.IsCancellationRequested)
                        return;
                    Thread.Sleep(500);
                }
            }, token);
        }
        #endregion

        #region Window Event Handler
        private async void Window_Closing(object sender, CancelEventArgs e)
        {
            if (Engine.WorkingEngine != null)
                await Engine.WorkingEngine.ForceStopWait();

            if (0 < LogWindow.Count)
            {
                LogDialog.Close();
                LogDialog = null;
            }

            if (0 < UtilityWindow.Count)
            {
                UtilityDialog.Close();
                UtilityDialog = null;
            }

            if (0 < ScriptEditWindow.Count)
            {
                ScriptEditDialog.Close();
                ScriptEditDialog = null;
            }

            // TODO: Do this in more cleaner way
            while (_scriptRefreshing != 0)
                await Task.Delay(500);
            while (ScriptCache.DbLock != 0)
                await Task.Delay(500);
            if (_projectsLoading != 0)
                await Task.Delay(500);

            _scriptCache?.WaitClose();
            Logger.Db.Close();
        }

        private void BuildConOutRedirectListBox_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (!(sender is ListBox listBox))
                return;
            listBox.Items.MoveCurrentToLast();
            listBox.ScrollIntoView(listBox.Items.CurrentItem);
        }
        #endregion
    }
    #endregion

    #region MainViewModel
    public class MainViewModel : INotifyPropertyChanged
    {
        #region Constructor
        public MainViewModel()
        {
            MainTreeItems = new ObservableCollection<ProjectTreeItemModel>();
            BuildTreeItems = new ObservableCollection<ProjectTreeItemModel>();
            BuildConOutRedirectTextLines = new ObservableCollection<Tuple<string, bool>>();

            Canvas canvas = new Canvas
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(10, 10, 10, 10),
            };
            Grid.SetRow(canvas, 0);
            Grid.SetColumn(canvas, 0);
            Panel.SetZIndex(canvas, -1);
            MainCanvas = canvas;
        }
        #endregion

        #region Normal Interface Properties
        private bool _workInProgress = false;
        public bool WorkInProgress
        {
            get => _workInProgress;
            set
            {
                _workInProgress = value;
                OnPropertyUpdate(nameof(WorkInProgress));
            }
        }

        private string _scriptTitleText = "Welcome to PEBakery!";
        public string ScriptTitleText
        {
            get => _scriptTitleText;
            set
            {
                _scriptTitleText = value;
                OnPropertyUpdate(nameof(ScriptTitleText));
            }
        }

        private string _scriptAuthorText = string.Empty;
        public string ScriptAuthorText
        {
            get => _scriptAuthorText;
            set
            {
                _scriptAuthorText = value;
                OnPropertyUpdate(nameof(ScriptAuthorText));
            }
        }

        private string _scriptVersionText = Properties.Resources.StringVersionFull;
        public string ScriptVersionText
        {
            get => _scriptVersionText;
            set
            {
                _scriptVersionText = value;
                OnPropertyUpdate(nameof(ScriptVersionText));
            }
        }

        private string _scriptDescriptionText = "PEBakery is now loading, please wait...";
        public string ScriptDescriptionText
        {
            get => _scriptDescriptionText;
            set
            {
                _scriptDescriptionText = value;
                OnPropertyUpdate(nameof(ScriptDescriptionText));
            }
        }

        #region ScriptLogo
        private bool _scriptLogoToggle;
        public bool ScriptLogoToggle
        {
            get => _scriptLogoToggle;
            set
            {
                _scriptLogoToggle = value;
                OnPropertyUpdate(nameof(ScriptLogoImageVisible));
                OnPropertyUpdate(nameof(ScriptLogoIconVisible));
            }
        }

        public Visibility ScriptLogoImageVisible => !ScriptLogoToggle ? Visibility.Visible : Visibility.Hidden;
        private ImageSource _scriptLogoImage;
        public ImageSource ScriptLogoImage
        {
            get => _scriptLogoImage;
            set
            {
                _scriptLogoImage = value;
                ScriptLogoToggle = false;
                OnPropertyUpdate(nameof(ScriptLogoImage));
            }
        }

        public Visibility ScriptLogoIconVisible => ScriptLogoToggle ? Visibility.Visible : Visibility.Hidden;
        private PackIconMaterialKind _scriptLogoIcon;
        public PackIconMaterialKind ScriptLogoIcon
        {
            get => _scriptLogoIcon;
            set
            {
                _scriptLogoIcon = value;
                ScriptLogoToggle = true;
                OnPropertyUpdate(nameof(ScriptLogoIcon));
            }
        }
        #endregion

        private bool _isTreeEntryFile = true;
        public bool IsTreeEntryFile
        {
            get => _isTreeEntryFile;
            set
            {
                _isTreeEntryFile = value;
                OnPropertyUpdate(nameof(IsTreeEntryFile));
                OnPropertyUpdate(nameof(ScriptCheckVisibility));
                OnPropertyUpdate(nameof(OpenExternalButtonToolTip));
                OnPropertyUpdate(nameof(OpenExternalButtonIconKind));
            }
        }

        public string OpenExternalButtonToolTip => IsTreeEntryFile ? "Edit Script" : "Open Folder";
        public PackIconMaterialKind OpenExternalButtonIconKind => IsTreeEntryFile ? PackIconMaterialKind.Pencil : PackIconMaterialKind.Folder;

        public string ScriptUpdateButtonToolTip => IsTreeEntryFile ? "Update Script" : "Update Scripts";

        private CodeValidator.Result _scriptCheckResult = CodeValidator.Result.Unknown;
        public CodeValidator.Result ScriptCheckResult
        {
            get => _scriptCheckResult;
            set
            {
                _scriptCheckResult = value;
                OnPropertyUpdate(nameof(ScriptCheckResult));
                OnPropertyUpdate(nameof(ScriptCheckVisibility));
            }
        }

        public Visibility ScriptCheckVisibility
        {
            get
            {
                if (!IsTreeEntryFile || !SwitchNormalBuildInterface)
                    return Visibility.Collapsed;
                return Visibility.Visible;
            }
        }

        private string statusBarText = string.Empty;
        public string StatusBarText
        {
            get => statusBarText;
            set
            {
                statusBarText = value;
                OnPropertyUpdate(nameof(StatusBarText));
            }
        }

        // True - StatusBar, False - ProgressBar
        private bool _switchStatusProgressBar = false;
        public bool SwitchStatusProgressBar
        {
            get => _switchStatusProgressBar;
            set
            {
                _switchStatusProgressBar = value;
                if (value)
                {
                    BottomStatusBarVisibility = Visibility.Visible;
                    BottomProgressBarVisibility = Visibility.Collapsed;
                }
                else
                {
                    BottomStatusBarVisibility = Visibility.Collapsed;
                    BottomProgressBarVisibility = Visibility.Visible;
                }
            }
        }

        private Visibility _bottomStatusBarVisibility = Visibility.Collapsed;
        public Visibility BottomStatusBarVisibility
        {
            get => _bottomStatusBarVisibility;
            set
            {
                _bottomStatusBarVisibility = value;
                OnPropertyUpdate(nameof(BottomStatusBarVisibility));
            }
        }

        private double _bottomProgressBarMinimum = 0;
        public double BottomProgressBarMinimum
        {
            get => _bottomProgressBarMinimum;
            set
            {
                _bottomProgressBarMinimum = value;
                OnPropertyUpdate(nameof(BottomProgressBarMinimum));
            }
        }

        private double _bottomProgressBarMaximum = 100;
        public double BottomProgressBarMaximum
        {
            get => _bottomProgressBarMaximum;
            set
            {
                _bottomProgressBarMaximum = value;
                OnPropertyUpdate(nameof(BottomProgressBarMaximum));
            }
        }

        private double _bottomProgressBarValue = 0;
        public double BottomProgressBarValue
        {
            get => _bottomProgressBarValue;
            set
            {
                _bottomProgressBarValue = value;
                OnPropertyUpdate(nameof(BottomProgressBarValue));
            }
        }

        private Visibility bottomProgressBarVisibility = Visibility.Visible;
        public Visibility BottomProgressBarVisibility
        {
            get => bottomProgressBarVisibility;
            set
            {
                bottomProgressBarVisibility = value;
                OnPropertyUpdate(nameof(BottomProgressBarVisibility));
            }
        }

        // True - Normal, False - Build
        private bool _switchNormalBuildInterface = true;
        public bool SwitchNormalBuildInterface
        {
            get => _switchNormalBuildInterface;
            set
            {
                _switchNormalBuildInterface = value;
                if (value)
                { // To Normal View
                    BuildScriptProgressBarValue = 0;
                    BuildFullProgressBarValue = 0;
                    TaskbarProgressState = TaskbarItemProgressState.None;

                    NormalInterfaceVisibility = Visibility.Visible;
                    BuildInterfaceVisibility = Visibility.Collapsed;
                }
                else
                { // To Build View
                    BuildPosition = string.Empty;
                    BuildEchoMessage = string.Empty;

                    BuildScriptProgressBarValue = 0;
                    BuildFullProgressBarValue = 0;
                    TaskbarProgressState = TaskbarItemProgressState.Normal;

                    NormalInterfaceVisibility = Visibility.Collapsed;
                    BuildInterfaceVisibility = Visibility.Visible;
                }
            }
        }

        private Visibility _normalInterfaceVisibility = Visibility.Visible;
        public Visibility NormalInterfaceVisibility
        {
            get => _normalInterfaceVisibility;
            set
            {
                _normalInterfaceVisibility = value;
                OnPropertyUpdate(nameof(NormalInterfaceVisibility));
                OnPropertyUpdate(nameof(ScriptCheckVisibility));
            }
        }

        private Visibility _buildInterfaceVisibility = Visibility.Collapsed;
        public Visibility BuildInterfaceVisibility
        {
            get => _buildInterfaceVisibility;
            set
            {
                _buildInterfaceVisibility = value;
                OnPropertyUpdate(nameof(BuildInterfaceVisibility));
                OnPropertyUpdate(nameof(ScriptCheckVisibility));
            }
        }

        private readonly object _mainTreeItemsLock = new object();
        private ObservableCollection<ProjectTreeItemModel> _mainTreeItems;
        public ObservableCollection<ProjectTreeItemModel> MainTreeItems
        {
            get => _mainTreeItems;
            set
            {
                _mainTreeItems = value;
                BindingOperations.EnableCollectionSynchronization(_mainTreeItems, _mainTreeItemsLock);
                OnPropertyUpdate(nameof(MainTreeItems));
            }
        }

        private Canvas _mainCanvas;
        public Canvas MainCanvas
        {
            get => _mainCanvas;
            set
            {
                _mainCanvas = value;
                OnPropertyUpdate(nameof(MainCanvas));
            }
        }
        #endregion

        #region Build Interface Properties
        private readonly object _buildTreeItemsLock = new object();
        private ObservableCollection<ProjectTreeItemModel> _buildTreeItems;
        public ObservableCollection<ProjectTreeItemModel> BuildTreeItems
        {
            get => _buildTreeItems;
            set
            {
                _buildTreeItems = value;
                BindingOperations.EnableCollectionSynchronization(_buildTreeItems, _buildTreeItemsLock);
                OnPropertyUpdate(nameof(BuildTreeItems));
            }
        }

        private string _buildPosition = string.Empty;
        public string BuildPosition
        {
            get => _buildPosition;
            set
            {
                _buildPosition = value;
                OnPropertyUpdate(nameof(BuildPosition));
            }
        }

        private string _buildEchoMessage = string.Empty;
        public string BuildEchoMessage
        {
            get => _buildEchoMessage;
            set
            {
                _buildEchoMessage = value;
                OnPropertyUpdate(nameof(BuildEchoMessage));
            }
        }

        // ProgressBar
        private double _buildScriptProgressBarMax = 100;
        public double BuildScriptProgressBarMax
        {
            get => _buildScriptProgressBarMax;
            set
            {
                _buildScriptProgressBarMax = value;
                OnPropertyUpdate(nameof(BuildScriptProgressBarMax));
            }
        }

        private double _buildScriptProgressBarValue = 0;
        public double BuildScriptProgressBarValue
        {
            get => _buildScriptProgressBarValue;
            set
            {
                _buildScriptProgressBarValue = value;
                OnPropertyUpdate(nameof(BuildScriptProgressBarValue));
            }
        }

        private Visibility _buildScriptFullProgressVisibility = Visibility.Visible;
        public Visibility BuildScriptFullProgressVisibility
        {
            get => _buildScriptFullProgressVisibility;
            set
            {
                _buildScriptFullProgressVisibility = value;
                OnPropertyUpdate(nameof(BuildScriptFullProgressVisibility));
            }
        }

        private double _buildFullProgressBarMax = 100;
        public double BuildFullProgressBarMax
        {
            get => _buildFullProgressBarMax;
            set
            {
                _buildFullProgressBarMax = value;
                OnPropertyUpdate(nameof(BuildFullProgressBarMax));
            }
        }

        private double _buildFullProgressBarValue = 0;
        public double BuildFullProgressBarValue
        {
            get => _buildFullProgressBarValue;
            set
            {
                _buildFullProgressBarValue = value;
                OnPropertyUpdate(nameof(BuildFullProgressBarValue));
            }
        }

        // ShellExecute Console Output
        private readonly object _buildConOutRedirectTextLinesLock = new object();
        private ObservableCollection<Tuple<string, bool>> _buildConOutRedirectTextLines;
        public ObservableCollection<Tuple<string, bool>> BuildConOutRedirectTextLines
        {
            get => _buildConOutRedirectTextLines;
            set
            {
                _buildConOutRedirectTextLines = value;
                BindingOperations.EnableCollectionSynchronization(_buildConOutRedirectTextLines, _buildConOutRedirectTextLinesLock);
                OnPropertyUpdate(nameof(BuildConOutRedirectTextLines));
            }
        }

        public static bool DisplayShellExecuteConOut = true;
        private Visibility _buildConOutRedirectVisibility = Visibility.Collapsed;
        public Visibility BuildConOutRedirectVisibility => DisplayShellExecuteConOut ? _buildConOutRedirectVisibility : Visibility.Collapsed;

        public bool BuildConOutRedirectShow
        {
            set
            {
                _buildConOutRedirectVisibility = value ? Visibility.Visible : Visibility.Collapsed;
                OnPropertyUpdate(nameof(BuildConOutRedirectVisibility));
            }
        }

        private FontHelper.WPFFont _monospaceFont;
        public FontHelper.WPFFont MonospaceFont
        {
            get => _monospaceFont;
            set
            {
                _monospaceFont = value;
                OnPropertyUpdate(nameof(MonospaceFont));
                OnPropertyUpdate(nameof(MonospaceFontFamily));
                OnPropertyUpdate(nameof(MonospaceFontWeight));
                OnPropertyUpdate(nameof(MonospaceFontSize));
            }
        }
        public FontFamily MonospaceFontFamily => _monospaceFont.FontFamily;
        public FontWeight MonospaceFontWeight => _monospaceFont.FontWeight;
        public double MonospaceFontSize => _monospaceFont.FontSizeInDIP;

        // Command Progress
        private string _buildCommandProgressTitle = string.Empty;
        public string BuildCommandProgressTitle
        {
            get => _buildCommandProgressTitle;
            set
            {
                _buildCommandProgressTitle = value;
                OnPropertyUpdate(nameof(BuildCommandProgressTitle));
            }
        }

        private string _buildCommandProgressText = string.Empty;
        public string BuildCommandProgressText
        {
            get => _buildCommandProgressText;
            set
            {
                _buildCommandProgressText = value;
                OnPropertyUpdate(nameof(BuildCommandProgressText));
            }
        }

        private double _buildCommandProgressMax = 100;
        public double BuildCommandProgressMax
        {
            get => _buildCommandProgressMax;
            set
            {
                _buildCommandProgressMax = value;
                OnPropertyUpdate(nameof(BuildCommandProgressMax));
            }
        }

        private double _buildCommandProgressValue = 0;
        public double BuildCommandProgressValue
        {
            get => _buildCommandProgressValue;
            set
            {
                _buildCommandProgressValue = value;
                OnPropertyUpdate(nameof(BuildCommandProgressValue));
            }
        }

        public Visibility BuildCommandProgressVisibility { get; private set; } = Visibility.Collapsed;
        public bool BuildCommandProgressShow
        {
            set
            {
                BuildCommandProgressVisibility = value ? Visibility.Visible : Visibility.Collapsed;
                OnPropertyUpdate(nameof(BuildCommandProgressVisibility));
            }
        }

        public void EnableBuildCommandProgress(string title, string text, int progressMax)
        {
            BuildCommandProgressTitle = title;
            BuildCommandProgressText = text;
            BuildCommandProgressMax = progressMax;
            BuildCommandProgressShow = true;
        }

        public void DisableBuildCommandProgress()
        {
            BuildCommandProgressShow = false;
            BuildCommandProgressTitle = "Progress";
            BuildCommandProgressText = string.Empty;
            BuildCommandProgressValue = 0;
        }

        // Taskbar Progress State
        //
        // None - Hidden
        // Inderterminate - Pulsing green indicator
        // Normal - Green
        // Error - Red
        // Paused - Yellow
        private TaskbarItemProgressState _taskbarProgressState;
        public TaskbarItemProgressState TaskbarProgressState
        {
            get => _taskbarProgressState;
            set
            {
                _taskbarProgressState = value;
                OnPropertyUpdate(nameof(TaskbarProgressState));
            }
        }
        #endregion

        #region Build Interface Methods
        public void SetBuildCommandProgress(string title, int max = 100)
        {
            BuildCommandProgressTitle = title;
            BuildCommandProgressText = string.Empty;
            BuildCommandProgressMax = max;
            BuildCommandProgressShow = true;
        }

        public void ResetBuildCommandProgress()
        {
            BuildCommandProgressShow = false;
            BuildCommandProgressTitle = "Progress";
            BuildCommandProgressText = string.Empty;
            BuildCommandProgressValue = 0;
        }
        #endregion

        #region OnPropertyUpdate
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyUpdate(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
    #endregion

    #region TreeViewModel
    public class ProjectTreeItemModel : INotifyPropertyChanged
    {
        #region Basic Property and Constructor
        public ProjectTreeItemModel ProjectRoot { get; }
        public ProjectTreeItemModel Parent { get; }

        public ProjectTreeItemModel(ProjectTreeItemModel root, ProjectTreeItemModel parent)
        {
            ProjectRoot = root ?? this;
            Parent = parent;
        }
        #endregion

        #region Shared Property
        private bool _isExpanded = false;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;
                OnPropertyUpdate(nameof(IsExpanded));
            }
        }

        public string Text => StringEscaper.Unescape(_sc.Title);

        private Script _sc;
        public Script Script
        {
            get => _sc;
            set
            {
                _sc = value;
                OnPropertyUpdate(nameof(Script));
                OnPropertyUpdate(nameof(Checked));
                OnPropertyUpdate(nameof(CheckBoxVisible));
                OnPropertyUpdate(nameof(Text));
                OnPropertyUpdate(nameof(MainViewModel.MainCanvas));
            }
        }

        private PackIconMaterialKind _icon;
        public PackIconMaterialKind Icon
        {
            get => _icon;
            set
            {
                _icon = value;
                OnPropertyUpdate(nameof(Icon));
            }
        }

        public ObservableCollection<ProjectTreeItemModel> Children { get; private set; } = new ObservableCollection<ProjectTreeItemModel>();

        public void SortChildren()
        {
            IOrderedEnumerable<ProjectTreeItemModel> sorted = Children
                .OrderBy(x => x.Script.Level)
                .ThenBy(x => x.Script.Type)
                .ThenBy(x => x.Script.RealPath);
            Children = new ObservableCollection<ProjectTreeItemModel>(sorted);
        }
        #endregion

        #region Build Mode Property
        private bool _buildFocus = false;
        public bool BuildFocus
        {
            get => _buildFocus;
            set
            {
                _buildFocus = value;
                OnPropertyUpdate(nameof(Icon));
                OnPropertyUpdate(nameof(BuildFocus));
                OnPropertyUpdate(nameof(BuildFontWeight));
            }
        }
        public FontWeight BuildFontWeight => _buildFocus ? FontWeights.SemiBold : FontWeights.Normal;
        #endregion

        #region Enabled CheckBox
        public bool Checked
        {
            get
            {
                switch (_sc.Selected)
                {
                    case SelectedState.True:
                        return true;
                    default:
                        return false;
                }
            }
            set
            {
                Global.MainViewModel.WorkInProgress = true;
                if (!_sc.Mandatory && _sc.Selected != SelectedState.None)
                {
                    if (value)
                    {
                        _sc.Selected = SelectedState.True;

                        try
                        {
                            // Run 'Disable' directive
                            List<LogInfo> errorLogs = DisableScripts(ProjectRoot, _sc);
                            Global.Logger.SystemWrite(errorLogs);
                        }
                        catch (Exception e)
                        {
                            Global.Logger.SystemWrite(new LogInfo(LogState.Error, e));
                        }
                    }
                    else
                    {
                        _sc.Selected = SelectedState.False;
                    }

                    if (!_sc.IsMainScript)
                    {
                        // Set also child scripts (Top-down propagation)
                        // Disable for Project's MainScript
                        if (0 < Children.Count)
                        { 
                            foreach (ProjectTreeItemModel childModel in Children)
                                childModel.Checked = value;
                        }

                        ParentCheckedPropagation();
                    }

                    OnPropertyUpdate(nameof(Checked));
                }
                Global.MainViewModel.WorkInProgress = false;
            }
        }

        public void ParentCheckedPropagation()
        { // Bottom-up propagation of Checked property
            if (Parent == null)
                return;

            bool setParentChecked = false;

            foreach (ProjectTreeItemModel sibling in Parent.Children)
            { // Siblings
                if (sibling.Checked)
                    setParentChecked = true;
            }

            Parent.SetParentChecked(setParentChecked);
        }

        public void SetParentChecked(bool value)
        {
            if (Parent == null)
                return;

            if (!_sc.Mandatory && _sc.Selected != SelectedState.None)
            {
                if (value)
                    _sc.Selected = SelectedState.True;
                else
                    _sc.Selected = SelectedState.False;
            }

            OnPropertyUpdate(nameof(Checked));
            ParentCheckedPropagation();
        }

        public Visibility CheckBoxVisible
        {
            get
            {
                if (_sc.Selected == SelectedState.None)
                    return Visibility.Collapsed;
                return Visibility.Visible;
            }
        }

        private List<LogInfo> DisableScripts(ProjectTreeItemModel root, Script sc)
        {
            if (root == null || sc == null)
                return new List<LogInfo>();

            string[] paths = Script.GetDisableScriptPaths(sc, out List<LogInfo> errorLogs);
            if (paths == null)
                return new List<LogInfo>();

            foreach (string path in paths)
            {
                int exist = sc.Project.AllScripts.Count(x => x.RealPath.Equals(path, StringComparison.OrdinalIgnoreCase));
                if (exist == 1)
                {
                    Ini.WriteKey(path, "Main", "Selected", "False");
                    ProjectTreeItemModel found = FindScriptByRealPath(path);
                    if (found != null)
                    {
                        if (sc.Type != ScriptType.Directory && sc.Mandatory == false && sc.Selected != SelectedState.None)
                            found.Checked = false;
                    }
                }
            }

            return errorLogs;
        }
        #endregion

        #region Find Script
        public ProjectTreeItemModel FindScriptByRealPath(string realPath)
        {
            return RecursiveFindScriptByRealPath(ProjectRoot, realPath);
        }

        public static ProjectTreeItemModel FindScriptByRealPath(ProjectTreeItemModel root, string realPath)
        {
            return RecursiveFindScriptByRealPath(root, realPath);
        }

        private static ProjectTreeItemModel RecursiveFindScriptByRealPath(ProjectTreeItemModel cur, string fullPath)
        {
            if (cur.Script != null)
            {
                if (fullPath.Equals(cur.Script.RealPath, StringComparison.OrdinalIgnoreCase))
                    return cur;
            }

            if (0 < cur.Children.Count)
            {
                foreach (ProjectTreeItemModel next in cur.Children)
                {
                    ProjectTreeItemModel found = RecursiveFindScriptByRealPath(next, fullPath);
                    if (found != null)
                        return found;
                }
            }

            // Not found in this path
            return null;
        }
        #endregion

        #region OnProperetyUpdate
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyUpdate(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region ToString
        public override string ToString() => Text;
        #endregion
    }
    #endregion

    #region Converters
    public class BuildConOutForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return Brushes.Black;
            return (bool)value ? Brushes.Red : Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class TaskbarProgressConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            return (double)values[1] / (double)values[0];
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class CodeValidatorResultIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || value.GetType() != typeof(CodeValidator.Result))
                return null;

            PackIconMaterialKind icon;
            CodeValidator.Result result = (CodeValidator.Result)value;
            switch (result)
            {
                case CodeValidator.Result.Clean:
                    icon = PackIconMaterialKind.Check;
                    break;
                case CodeValidator.Result.Warning:
                    icon = PackIconMaterialKind.Alert;
                    break;
                case CodeValidator.Result.Error:
                    icon = PackIconMaterialKind.Close;
                    break;
                default:
                    icon = PackIconMaterialKind.Magnify;
                    break;
            }
            return icon;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class CodeValidatorResultColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || value.GetType() != typeof(CodeValidator.Result))
                return null;

            Brush brush;
            CodeValidator.Result result = (CodeValidator.Result)value;
            switch (result)
            {
                case CodeValidator.Result.Clean:
                    brush = new SolidColorBrush(Colors.Green);
                    break;
                case CodeValidator.Result.Warning:
                    brush = new SolidColorBrush(Colors.OrangeRed);
                    break;
                case CodeValidator.Result.Error:
                    brush = new SolidColorBrush(Colors.Red);
                    break;
                default:
                    brush = new SolidColorBrush(Colors.Gray);
                    break;
            }
            return brush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    #endregion
}
