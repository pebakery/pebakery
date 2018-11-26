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
using PEBakery.Core.ViewModels;
using PEBakery.Helper;
using SQLite;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace PEBakery.WPF
{
    #region MainWindow
    // ReSharper disable once RedundantExtendsListEntry
    public partial class MainWindow : Window
    {
        #region Constants
        internal const int ScriptAuthorLenLimit = 35;
        #endregion

        #region Fields and Properties
        public string BaseDir => Global.BaseDir;

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
                        if (!Directory.Exists(argBaseDir))
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
                else if (args[i].Equals("/?", StringComparison.OrdinalIgnoreCase) ||
                         args[i].Equals("/help", StringComparison.OrdinalIgnoreCase) ||
                         args[i].Equals("/h", StringComparison.OrdinalIgnoreCase))
                {
                    // ReSharper disable once LocalizableElement
                    Console.WriteLine("Sorry, help message not implemented\r\n");
                }
            }

            Global.BaseDir = argBaseDir;

            // Setting File
            string settingFile = Path.Combine(BaseDir, "PEBakery.ini");
            Global.Setting = new SettingViewModel(settingFile);
            Model.MonospaceFont = Global.Setting.Interface_MonospaceFont;

            // Custom Title
            if (Global.Setting.Interface_UseCustomTitle)
                Model.TitleBar = Global.Setting.Interface_CustomTitle;

            // Database Directory
            string dbDir = Path.Combine(BaseDir, "Database");
            if (!Directory.Exists(dbDir))
                Directory.CreateDirectory(dbDir);

            // Log Database
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

        #region Background Tasks
        public Task StartLoadingProjects(bool quiet = false)
        {
            if (Model.ProjectsLoading != 0)
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
                Interlocked.Increment(ref Model.ProjectsLoading);
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
                        {
                            _scriptCache.ClearTable(new ScriptCache.ClearTableOptions
                            {
                                CacheInfo = false,
                                ScriptCache = true,
                            });
                            Global.Projects = new ProjectCollection(BaseDir, null);
                        }
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
                                Model.CurMainTree = Model.MainTreeItems[pIdx];
                                Model.CurMainTree.IsExpanded = true;
                                if (Global.Projects[pIdx] != null)
                                    DisplayScript(Global.Projects[pIdx].MainScript);
                            }
                            else
                            {
                                Model.CurMainTree = null;
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
                    Interlocked.Decrement(ref Model.ProjectsLoading);
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
            if (Model.CurMainTree?.Script == null)
                return Task.CompletedTask;
            if (Model.CurMainTree.Script.Type == ScriptType.Directory)
                return Task.CompletedTask;
            if (Model.ScriptRefreshing != 0)
                return Task.CompletedTask;

            ProjectTreeItemModel node = Model.CurMainTree;
            return Task.Run(() =>
            {
                Interlocked.Increment(ref Model.ScriptRefreshing);
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
                    Interlocked.Decrement(ref Model.ScriptRefreshing);
                }
            });
        }

        private void PostRefreshScript(ProjectTreeItemModel node, Script sc)
        {
            node.Script = sc;
            node.ParentCheckedPropagation();
            MainViewModel.UpdateTreeViewIcon(node);
            Application.Current.Dispatcher.Invoke(() =>
            {
                DisplayScript(node.Script);
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
                DisplayScriptInterface(sc);

                // Run CodeValidator
                // Do not use await, let it run in background
                if (Global.Setting.Script_AutoSyntaxCheck)
                    Model.StartSyntaxCheck(true);
            }

            Model.IsTreeEntryFile = sc.Type != ScriptType.Directory;
            Model.OnPropertyUpdate(nameof(MainViewModel.MainCanvas));
        }

        public void DisplayScriptInterface(Script sc)
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
                    using (MemoryStream ms = EncodedFile.ExtractLogo(sc, out ImageHelper.ImageType type))
                    {
                        switch (type)
                        {
                            case ImageHelper.ImageType.Svg:
                                DrawingGroup svgDrawing = ImageHelper.SvgToDrawingGroup(ms);
                                Rect svgSize = svgDrawing.Bounds;
                                (double width, double height) = ImageHelper.StretchSizeAspectRatio(svgSize.Width, svgSize.Height, 90, 90);
                                Model.ScriptLogoSvg = new DrawingBrush { Drawing = svgDrawing };
                                Model.ScriptLogoSvgWidth = width;
                                Model.ScriptLogoSvgHeight = height;
                                break;
                            default:
                                Model.ScriptLogoImage = ImageHelper.ImageToBitmapImage(ms);
                                break;
                        }
                    }
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

                if (Model.CurMainTree?.Script == null || Model.WorkInProgress)
                {
                    Interlocked.Decrement(ref Engine.WorkingLock);
                    return;
                }

                // Determine current project
                Project p = Model.CurMainTree.Script.Project;

                Model.BuildTreeItems.Clear();
                ProjectTreeItemModel treeRoot = PopulateOneTreeItem(p.MainScript, null, null);
                ScriptListToTreeViewModel(p, p.ActiveScripts, false, treeRoot);
                Model.BuildTreeItems.Add(treeRoot);
                Model.CurBuildTree = null;

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
                DisplayScript(Model.CurMainTree.Script);

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
            if (Model.ProjectsLoading != 0)
                return;

            (MainTreeView.DataContext as ProjectTreeItemModel)?.Children.Clear();

            StartLoadingProjects();
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private void SettingButton_Click(object sender, RoutedEventArgs e)
        {
            if (Model.ProjectsLoading != 0)
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
                        DisplayScript(Model.CurMainTree.Script);

                    // Script
                    if (!old_Script_EnableCache && Global.Setting.Script_EnableCache)
                        StartScriptCaching();
                }
            }
        }

        private void UtilityButton_Click(object sender, RoutedEventArgs e)
        {
            if (Model.ProjectsLoading != 0)
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
            if (Model.CurMainTree?.Script == null || Model.WorkInProgress)
                return;

            Script sc = Model.CurMainTree.Script;
            if (sc.Sections.ContainsKey(ScriptSection.Names.Process))
            {
                if (Engine.WorkingLock == 0)  // Start Build
                {
                    Interlocked.Increment(ref Engine.WorkingLock);

                    // Populate BuildTree
                    Model.BuildTreeItems.Clear();
                    ProjectTreeItemModel rootItem = PopulateOneTreeItem(sc, null, null);
                    Model.BuildTreeItems.Add(rootItem);
                    Model.CurBuildTree = null;

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
                    DisplayScript(Model.CurMainTree.Script);

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
            if (Model.CurMainTree?.Script == null)
                return;
            if (Model.WorkInProgress)
                return;

            StartRefreshScript();
        }

        private void ScriptEditButton_Click(object sender, RoutedEventArgs e)
        {
            if (Model.CurMainTree?.Script == null)
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
            if (Model.CurMainTree?.Script == null)
                return;
            if (Model.WorkInProgress)
                return;

            Script sc = Model.CurMainTree.Script;
            if (ScriptEditWindow.Count == 0)
            {
                ScriptEditDialog = new ScriptEditWindow(sc) { Owner = this };

                // Open as Modal
                // If ScriptEditWindow returns true in DialogResult, refresh script
                if (ScriptEditDialog.ShowDialog() == true)
                {
                    sc = ScriptEditDialog.Tag as Script;
                    Debug.Assert(sc != null, $"{nameof(sc)} != null");

                    DisplayScript(sc);
                    Model.CurMainTree.Script = sc;
                }
            }
        }

        private void ScriptExternalEditor_Click(object sender, RoutedEventArgs e)
        {
            if (Model.CurMainTree?.Script == null)
                return;
            if (Model.WorkInProgress)
                return;

            Script sc = Model.CurMainTree.Script;
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

        private void ScriptUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (Model.CurMainTree?.Script == null)
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
            if (Model.CurMainTree?.Script == null)
                return;
            if (Model.WorkInProgress)
                return;

            // Do not use await, let it run in background
            Model.StartSyntaxCheck(false);
        }

        private void ScriptOpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (Model.CurMainTree?.Script == null)
                return;
            if (Model.WorkInProgress)
                return;

            Script sc = Model.CurMainTree.Script;
            if (sc.Type == ScriptType.Directory)
                MainViewModel.OpenFolder(sc.RealPath);
            else
                MainViewModel.OpenFolder(Path.GetDirectoryName(sc.RealPath));
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
                Model.CurMainTree = projectRoot;
                Model.CurMainTree.IsExpanded = true;
                DisplayScript(Model.CurMainTree.Script);
            }
        }

        public ProjectTreeItemModel PopulateOneTreeItem(Script sc, ProjectTreeItemModel projectRoot, ProjectTreeItemModel parent)
        {
            ProjectTreeItemModel item = new ProjectTreeItemModel(projectRoot, parent)
            {
                Script = sc,
            };
            MainViewModel.UpdateTreeViewIcon(item);
            parent?.Children.Add(item);

            return item;
        }



        private void MainTreeView_Loaded(object sender, RoutedEventArgs e)
        {
            KeyDown += MainTreeView_KeyDown;
        }

        private void MainTreeView_Unloaded(object sender, RoutedEventArgs e)
        {
            KeyDown -= MainTreeView_KeyDown;
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
            if (sender is TreeView tree && tree.SelectedItem is ProjectTreeItemModel itemModel)
            {
                ProjectTreeItemModel item = Model.CurMainTree = itemModel;

                Dispatcher.Invoke(() =>
                {
                    Stopwatch watch = new Stopwatch();
                    watch.Start();
                    DisplayScript(item.Script);
                    watch.Stop();
                    double msec = watch.Elapsed.TotalMilliseconds;
                    string filename = Path.GetFileName(Model.CurMainTree.Script.TreePath);
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

        #region PrintBuildElapsedStatus
        public static Task PrintBuildElapsedStatus(string msg, EngineState s, CancellationToken token)
        {
            return Task.Run(() =>
            {
                while (true)
                {
                    if (s.StartTime == DateTime.MinValue)
                    { // Engine not started yet
                        s.MainViewModel.StatusBarText = msg;
                        return;
                    }

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
            while (Model.ScriptRefreshing != 0)
                await Task.Delay(500);
            while (ScriptCache.DbLock != 0)
                await Task.Delay(500);
            if (Model.ProjectsLoading != 0)
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

    public class ScriptLogoVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
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
