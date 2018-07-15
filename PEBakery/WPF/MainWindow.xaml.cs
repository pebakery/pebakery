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


using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Shell;
using SQLite;
using PEBakery.Helper;
using PEBakery.IniLib;
using PEBakery.Core;
using MahApps.Metro.IconPacks;

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
        public ProjectCollection Projects { get; private set; }
        public string BaseDir { get; }

        private BackgroundWorker _loadWorker = new BackgroundWorker();
        private BackgroundWorker _refreshWorker = new BackgroundWorker();
        private BackgroundWorker _cacheWorker = new BackgroundWorker();
        private BackgroundWorker _syntaxCheckWorker = new BackgroundWorker();

        public TreeViewModel CurMainTree { get; private set; }
        public TreeViewModel CurBuildTree { get; set; }

        public Logger Logger { get; }
        private readonly ScriptCache _scriptCache;

        public const int MaxDpiScale = 4;
        private int _allScriptCount = 0;
        public SettingViewModel Setting { get; }

        public MainViewModel Model { get; }
        public Canvas MainCanvas => Model.MainCanvas;

        public LogWindow LogDialog = null;
        public UtilityWindow UtilityDialog = null;
        public ScriptEditWindow ScriptEditDialog = null;
        #endregion

        #region Constructor
        public MainWindow()
        {
            InitializeComponent();
            Model = DataContext as MainViewModel;

            string[] args = App.Args;
            if (!int.TryParse(Properties.Resources.EngineVersion, NumberStyles.Integer, CultureInfo.InvariantCulture, out App.Version))
            {
                MessageBox.Show($"Invalid version [{App.Version}]", "Invalid Version", MessageBoxButton.OK, MessageBoxImage.Error);
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

            App.BaseDir = BaseDir = argBaseDir;

            string settingFile = Path.Combine(BaseDir, "PEBakery.ini");
            Setting = new SettingViewModel(settingFile);

            string dbDir = Path.Combine(BaseDir, "Database");
            if (!Directory.Exists(dbDir))
                Directory.CreateDirectory(dbDir);

            string logDbFile = Path.Combine(dbDir, "PEBakeryLog.db");
            try
            {
                App.Logger = Logger = new Logger(logDbFile);
                Logger.SystemWrite(new LogInfo(LogState.Info, "PEBakery launched"));
            }
            catch (SQLiteException e)
            { // Update failure
                string msg = $"SQLite Error : {e.Message}\r\n\r\nLog database is corrupted.\r\nPlease delete PEBakeryLog.db and restart.";
                MessageBox.Show(msg, "SQLite Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown(1);
            }
            Setting.LogDB = Logger.DB;

            // If script cache is enabled, generate cache after 5 seconds
            if (Setting.Script_EnableCache)
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

                Setting.CacheDB = _scriptCache;
            }
            else
            {
                Logger.SystemWrite(new LogInfo(LogState.Info, "ScriptCache disabled"));
            }

            StartLoadWorker();
        }
        #endregion

        #region Background Workers
        public AutoResetEvent StartLoadWorker(bool quiet = false)
        {
            AutoResetEvent resetEvent = new AutoResetEvent(false);
            Stopwatch watch = Stopwatch.StartNew();

            // Load CommentProcessing Icon
            ScriptLogo.Content = ImageHelper.GetMaterialIcon(PackIconMaterialKind.CommentProcessing, 10);

            // Prepare PEBakery Loading Information
            if (!quiet)
            {
                Model.ScriptTitleText = "PEBakery loading...";
                Model.ScriptDescriptionText = string.Empty;
            }
            Logger.SystemWrite(new LogInfo(LogState.Info, $@"Loading from [{BaseDir}]"));
            MainCanvas.Children.Clear();
            (MainTreeView.DataContext as TreeViewModel)?.Children.Clear();

            int stage2LinksCount = 0;
            int loadedScriptCount = 0;
            int stage1CachedCount = 0;
            int stage2LoadedCount = 0;
            int stage2CachedCount = 0;

            Model.BottomProgressBarMinimum = 0;
            Model.BottomProgressBarMaximum = 100;
            Model.BottomProgressBarValue = 0;
            if (!quiet)
                Model.WorkInProgress = true;
            Model.SwitchStatusProgressBar = false; // Show Progress Bar

            _loadWorker = new BackgroundWorker();
            _loadWorker.DoWork += (object sender, DoWorkEventArgs e) =>
            {
                string baseDir = (string)e.Argument;
                BackgroundWorker worker = sender as BackgroundWorker;

                // Init ProjectCollection
                if (Setting.Script_EnableCache && _scriptCache != null) // Use ScriptCache
                {
                    if (_scriptCache.IsGlobalCacheValid(baseDir))
                        Projects = new ProjectCollection(baseDir, _scriptCache);
                    else // Cache is invalid
                        Projects = new ProjectCollection(baseDir, null);
                }
                else // Do not use ScriptCache
                {
                    Projects = new ProjectCollection(baseDir, null);
                }

                _allScriptCount = Projects.PrepareLoad(out stage2LinksCount);
                Dispatcher.Invoke(() => { Model.BottomProgressBarMaximum = _allScriptCount + stage2LinksCount; });

                // Load scripts in parallel
                List<LogInfo> errorLogs = Projects.Load(worker);
                Logger.SystemWrite(errorLogs);
                Setting.UpdateProjectList();

                if (0 < Projects.ProjectNames.Count)
                { // Load Success
                    // Populate TreeView
                    Dispatcher.Invoke(() =>
                    {
                        foreach (Project project in Projects.Projects)
                            ScriptListToTreeViewModel(project, project.VisibleScripts, true, Model.MainTree);

                        int pIdx = Setting.Project_DefaultIndex;
                        CurMainTree = Model.MainTree.Children[pIdx];
                        CurMainTree.IsExpanded = true;
                        if (Projects[pIdx] != null)
                            DrawScript(Projects[pIdx].MainScript);
                    });

                    e.Result = true;
                }
                else
                {
                    e.Result = false;
                }
            };
            _loadWorker.WorkerReportsProgress = true;
            _loadWorker.ProgressChanged += (object sender, ProgressChangedEventArgs e) =>
            {
                Interlocked.Increment(ref loadedScriptCount);
                Model.BottomProgressBarValue = loadedScriptCount;
                string msg = string.Empty;
                switch (e.ProgressPercentage)
                {
                    case -1: // Loading Cache
                        msg = "Loading script cache";
                        break;
                    case 0:  // Stage 1
                        msg = e.UserState == null ? "Error" : $"{e.UserState}";
                        break;
                    case 1:  // Stage 1, Cached
                        Interlocked.Increment(ref stage1CachedCount);
                        msg = e.UserState == null ? "Cached - Error" : $"Cached - {e.UserState}";
                        break;
                    case 2:  // Stage 2
                        Interlocked.Increment(ref stage2LoadedCount);
                        msg = e.UserState == null ? "Error" : $"{e.UserState}";
                        break;
                    case 3:  // Stage 2, Cached
                        Interlocked.Increment(ref stage2LoadedCount);
                        Interlocked.Increment(ref stage2CachedCount);
                        msg = e.UserState == null ? "Cached - Error" : $"Cached - {e.UserState}";
                        break;
                }

                if (0 <= e.ProgressPercentage)
                {
                    int stage = e.ProgressPercentage / 2 + 1;
                    if (stage == 1)
                        msg = $"Stage {stage} ({loadedScriptCount} / {_allScriptCount}) \r\n{msg}";
                    else
                        msg = $"Stage {stage} ({stage2LoadedCount} / {stage2LinksCount}) \r\n{msg}";
                }

                Model.ScriptDescriptionText = msg;
            };
            _loadWorker.RunWorkerCompleted += (object sender, RunWorkerCompletedEventArgs e) =>
            {
                if ((bool)e.Result)
                { // Load Success
                    StringBuilder b = new StringBuilder();
                    b.Append("Projects [");
                    List<Project> projList = Projects.Projects;
                    for (int i = 0; i < projList.Count; i++)
                    {
                        b.Append(projList[i].ProjectName);
                        if (i + 1 < projList.Count)
                            b.Append(", ");
                    }
                    b.Append("] loaded");
                    Logger.SystemWrite(new LogInfo(LogState.Info, b.ToString()));

                    watch.Stop();
                    double t = watch.Elapsed.TotalMilliseconds / 1000.0;
                    string msg;
                    if (Setting.Script_EnableCache)
                    {
                        double cachePercent = (double)(stage1CachedCount + stage2CachedCount) * 100 / (_allScriptCount + stage2LinksCount);
                        msg = $"{_allScriptCount} scripts loaded ({t:0.#}s) - {cachePercent:0.#}% cached";
                        Model.StatusBarText = msg;
                    }
                    else
                    {
                        msg = $"{_allScriptCount} scripts loaded ({t:0.#}s)";
                        Model.StatusBarText = msg;
                    }
                    if (!quiet)
                        Model.WorkInProgress = false;
                    Model.SwitchStatusProgressBar = true; // Show Status Bar

                    Logger.SystemWrite(new LogInfo(LogState.Info, msg));
                    Logger.SystemWrite(Logger.LogSeperator);

                    // If script cache is enabled, generate cache.
                    if (Setting.Script_EnableCache)
                        StartCacheWorker();
                }
                else
                {
                    Model.ScriptTitleText = "Unable to find project.";
                    Model.ScriptDescriptionText = $"Please provide project in [{Projects.ProjectRoot}]";

                    if (!quiet)
                        Model.WorkInProgress = false;
                    Model.SwitchStatusProgressBar = true; // Show Status Bar
                    Model.StatusBarText = "Unable to find project.";
                }

                resetEvent.Set();
            };

            _loadWorker.RunWorkerAsync(BaseDir);

            return resetEvent;
        }

        private void StartCacheWorker()
        {
            if (ScriptCache.DbLock == 0)
            {
                Interlocked.Increment(ref ScriptCache.DbLock);
                try
                {
                    Stopwatch watch = new Stopwatch();
                    _cacheWorker = new BackgroundWorker();

                    Model.WorkInProgress = true;
                    int updatedCount = 0;
                    int cachedCount = 0;
                    _cacheWorker.DoWork += (object sender, DoWorkEventArgs e) =>
                    {
                        BackgroundWorker worker = sender as BackgroundWorker;

                        watch = Stopwatch.StartNew();
                        _scriptCache.CacheScripts(Projects, BaseDir, worker);
                    };

                    _cacheWorker.WorkerReportsProgress = true;
                    _cacheWorker.ProgressChanged += (object sender, ProgressChangedEventArgs e) =>
                    {
                        Interlocked.Increment(ref cachedCount);
                        if (e.ProgressPercentage == 1)
                            Interlocked.Increment(ref updatedCount);
                    };
                    _cacheWorker.RunWorkerCompleted += (object sender, RunWorkerCompletedEventArgs e) =>
                    {
                        watch.Stop();

                        double cachePercent = (double)updatedCount * 100 / _allScriptCount;

                        double t = watch.Elapsed.TotalMilliseconds / 1000.0;
                        string msg = $"{_allScriptCount} scripts cached ({t:0.###}s), {cachePercent:0.#}% updated";
                        Logger.SystemWrite(new LogInfo(LogState.Info, msg));
                        Logger.SystemWrite(Logger.LogSeperator);

                        Model.WorkInProgress = false;
                    };
                    _cacheWorker.RunWorkerAsync();
                }
                finally
                {
                    Interlocked.Decrement(ref ScriptCache.DbLock);
                }
            }
        }

        public AutoResetEvent StartRefreshScriptWorker()
        {
            if (CurMainTree?.Script == null)
                return null;
            if (_refreshWorker.IsBusy)
                return null;
            if (CurMainTree.Script.Type == ScriptType.Directory)
                return null;

            AutoResetEvent resetEvent = new AutoResetEvent(false);

            Stopwatch watch = new Stopwatch();

            Model.WorkInProgress = true;
            _refreshWorker = new BackgroundWorker();
            _refreshWorker.DoWork += (object sender, DoWorkEventArgs e) =>
            {
                watch.Start();
                Script sc = CurMainTree.Script;
                if (sc.Type != ScriptType.Directory)
                    e.Result = sc.Project.RefreshScript(CurMainTree.Script);
            };
            _refreshWorker.RunWorkerCompleted += (object sender, RunWorkerCompletedEventArgs e) =>
            {
                if (e.Result is Script sc)
                    PostRefreshScript(sc);

                Model.WorkInProgress = false;
                watch.Stop();
                double sec = watch.Elapsed.TotalSeconds;
                if (e.Result is Script)
                    Model.StatusBarText = $"{Path.GetFileName(CurMainTree.Script.TreePath)} reloaded. ({sec:0.000}s)";
                else
                    Model.StatusBarText = $"{Path.GetFileName(CurMainTree.Script.TreePath)} reload failed. ({sec:0.000}s)";

                resetEvent.Set();
            };
            _refreshWorker.RunWorkerAsync();

            return resetEvent;
        }

        private void PostRefreshScript(Script sc)
        {
            CurMainTree.Script = sc;
            CurMainTree.ParentCheckedPropagation();
            UpdateTreeViewIcon(CurMainTree);
            DrawScript(CurMainTree.Script);
        }

        private void StartSyntaxCheckWorker(bool quiet)
        {
            if (CurMainTree?.Script == null)
                return;

            if (_syntaxCheckWorker.IsBusy)
                return;

            Script sc = CurMainTree.Script;
            if (sc.Type == ScriptType.Directory)
                return;

            if (!quiet)
                Model.WorkInProgress = true;

            _syntaxCheckWorker = new BackgroundWorker();
            _syntaxCheckWorker.DoWork += (object sender, DoWorkEventArgs e) =>
            {
                CodeValidator v = new CodeValidator(sc);
                v.Validate();

                e.Result = v;
            };
            _syntaxCheckWorker.RunWorkerCompleted += (object sender, RunWorkerCompletedEventArgs e) =>
            {
                if (!(e.Result is CodeValidator v))
                    return;

                LogInfo[] logs = v.LogInfos;
                LogInfo[] errorLogs = logs.Where(x => x.State == LogState.Error).ToArray();
                LogInfo[] warnLogs = logs.Where(x => x.State == LogState.Warning).ToArray();

                int errorWarns = 0;
                StringBuilder b = new StringBuilder();
                if (0 < errorLogs.Length)
                {
                    errorWarns += errorLogs.Length;

                    if (!quiet)
                    {
                        b.AppendLine($"{errorLogs.Length} syntax error detected at [{sc.TreePath}]");
                        b.AppendLine();
                        for (int i = 0; i < errorLogs.Length; i++)
                        {
                            LogInfo log = errorLogs[i];
                            if (log.Command != null)
                                b.AppendLine($"[{i + 1}/{errorLogs.Length}] {log.Message} ({log.Command}) (Line {log.Command.LineIdx})");
                            else
                                b.AppendLine($"[{i + 1}/{errorLogs.Length}] {log.Message}");
                        }
                        b.AppendLine();
                    }
                }

                if (0 < warnLogs.Length)
                {
                    errorWarns += warnLogs.Length;

                    if (!quiet)
                    {
                        b.AppendLine($"{errorLogs.Length} syntax warning detected");
                        b.AppendLine();
                        for (int i = 0; i < warnLogs.Length; i++)
                        {
                            LogInfo log = warnLogs[i];
                            b.AppendLine($"[{i + 1}/{warnLogs.Length}] {log.Message} ({log.Command})");
                        }
                        b.AppendLine();
                    }
                }

                if (errorWarns == 0)
                {
                    Model.ScriptCheckResult = true;

                    if (!quiet)
                    {
                        b.AppendLine("No syntax error detected");
                        b.AppendLine();
                        b.AppendLine($"Section Coverage : {v.Coverage * 100:0.#}% ({v.VisitedSectionCount}/{v.CodeSectionCount})");

                        MessageBox.Show(b.ToString(), "Syntax Check", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    Model.ScriptCheckResult = false;

                    if (!quiet)
                    {
                        MessageBoxResult result = MessageBox.Show($"{errorWarns} syntax error detected!\r\n\r\nOpen logs?", "Syntax Check", MessageBoxButton.OKCancel, MessageBoxImage.Error);
                        if (result == MessageBoxResult.OK)
                        {
                            b.AppendLine($"Section Coverage : {v.Coverage * 100:0.#}% ({v.VisitedSectionCount}/{v.CodeSectionCount})");

                            string tempFile = Path.GetTempFileName();
                            File.Delete(tempFile);
                            tempFile = Path.GetTempFileName().Replace(".tmp", ".txt");
                            using (StreamWriter sw = new StreamWriter(tempFile, false, Encoding.UTF8))
                                sw.Write(b.ToString());

                            OpenTextFile(tempFile, true);
                        }
                    }
                }

                if (!quiet)
                    Model.WorkInProgress = false;
            };
            _syntaxCheckWorker.RunWorkerAsync();
        }
        #endregion

        #region DrawScript
        public void DrawScript(Script sc)
        {
            DrawScriptLogo(sc);

            Model.ScriptCheckResult = null;

            if (sc.Type == ScriptType.Directory)
            {
                MainCanvas.Children.Clear();

                Model.ScriptTitleText = StringEscaper.Unescape(sc.Title);
                Model.ScriptDescriptionText = string.Empty;
                Model.ScriptVersionText = string.Empty;
                Model.ScriptAuthorText = string.Empty;
            }
            else
            {
                Model.ScriptTitleText = StringEscaper.Unescape(sc.Title);
                Model.ScriptDescriptionText = StringEscaper.Unescape(sc.Description);

                string verStr = StringEscaper.ProcessVersionString(sc.Version);
                if (verStr == null)
                {
                    Model.ScriptVersionText = string.Empty;
                    Logger.SystemWrite(new LogInfo(LogState.Error, $"Script [{sc.Title}] contains invalid version string [{sc.Version}]"));
                }
                else
                {
                    Model.ScriptVersionText = "v" + verStr;
                }

                if (ScriptAuthorLenLimit < sc.Author.Length)
                    Model.ScriptAuthorText = sc.Author.Substring(0, ScriptAuthorLenLimit) + "...";
                else
                    Model.ScriptAuthorText = sc.Author;

                double scaleFactor = Setting.Interface_ScaleFactor / 100;
                ScaleTransform scale;
                if (scaleFactor - 1 < double.Epsilon)
                    scale = new ScaleTransform(1, 1);
                else
                    scale = new ScaleTransform(scaleFactor, scaleFactor);
                UIRenderer render = new UIRenderer(MainCanvas, this, sc, scaleFactor, true);
                MainCanvas.LayoutTransform = scale;
                render.Render();

                if (Setting.Script_AutoSyntaxCheck)
                    StartSyntaxCheckWorker(true);
            }

            Model.IsTreeEntryFile = sc.Type != ScriptType.Directory;
            Model.OnPropertyUpdate(nameof(MainViewModel.MainCanvas));
        }

        public void DrawScriptLogo(Script sc)
        {
            double size = ScriptLogo.ActualWidth * MaxDpiScale;
            if (sc.Type == ScriptType.Directory)
            {
                if (sc.IsDirLink)
                    ScriptLogo.Content = ImageHelper.GetMaterialIcon(PackIconMaterialKind.FolderMove, 10);
                else
                    ScriptLogo.Content = ImageHelper.GetMaterialIcon(PackIconMaterialKind.Folder, 10);
            }
            else
            {
                try
                {
                    Image image = EncodedFile.ExtractLogoImage(sc, size);

                    Grid grid = new Grid();
                    grid.Children.Add(image);

                    ScriptLogo.Content = grid;
                }
                catch
                { // No logo file - use default
                    if (sc.Type == ScriptType.Script)
                    {
                        if (sc.IsDirLink)
                            ScriptLogo.Content = ImageHelper.GetMaterialIcon(PackIconMaterialKind.FileSend, 10);
                        else
                            ScriptLogo.Content = ImageHelper.GetMaterialIcon(PackIconMaterialKind.FileDocument, 10);
                    }
                    else if (sc.Type == ScriptType.Link)
                    {
                        ScriptLogo.Content = ImageHelper.GetMaterialIcon(PackIconMaterialKind.FileSend, 10);
                    }
                }
            }
        }
        #endregion

        #region Main Buttons
        private async void BuildButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Exact Locking without Race Condition
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

                Model.BuildTree.Children.Clear();
                ScriptListToTreeViewModel(p, p.ActiveScripts, false, Model.BuildTree, null);
                CurBuildTree = null;

                EngineState s = new EngineState(p, Logger, Model);
                s.SetOption(Setting);

                Engine.WorkingEngine = new Engine(s);

                // Build Start, Switch to Build View
                Model.SwitchNormalBuildInterface = false;

                // Turn on progress ring
                Model.WorkInProgress = true;

                Stopwatch watch = Stopwatch.StartNew();

                // Run
                int buildId = await Engine.WorkingEngine.Run($"Project {p.ProjectName}");

#if DEBUG  // TODO: Remove this later, this line is for Debug
                Logger.ExportBuildLog(LogExportType.Text, Path.Combine(s.BaseDir, "LogDebugDump.txt"), buildId);
#endif

                // Turn off progress ring
                Model.WorkInProgress = false;

                // Build Ended, Switch to Normal View
                Model.SwitchNormalBuildInterface = true;
                DrawScript(CurMainTree.Script);

                watch.Stop();
                TimeSpan t = watch.Elapsed;
                Model.StatusBarText = $"{p.ProjectName} build done ({t:h\\:mm\\:ss})";

                if (Setting.General_ShowLogAfterBuild && LogWindow.Count == 0)
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
            if (_loadWorker.IsBusy)
                return;

            (MainTreeView.DataContext as TreeViewModel)?.Children.Clear();

            StartLoadWorker();
        }

        private void SettingButton_Click(object sender, RoutedEventArgs e)
        {
            if (_loadWorker.IsBusy)
                return;

            double oldInterfaceScaleFactor = Setting.Interface_ScaleFactor;
            bool oldScriptEnableCache = Setting.Script_EnableCache;

            SettingWindow dialog = new SettingWindow(Setting);
            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                // Scale Factor
                double newScaleFactor = Setting.Interface_ScaleFactor;
                if (double.Epsilon < Math.Abs(newScaleFactor - oldInterfaceScaleFactor)) // Not Equal
                    DrawScript(CurMainTree.Script);

                // Script
                if (!oldScriptEnableCache && Setting.Script_EnableCache)
                    StartCacheWorker();

                // Apply
                Setting.ApplySetting();
            }
        }

        private void UtilityButton_Click(object sender, RoutedEventArgs e)
        {
            if (_loadWorker.IsBusy)
                return;

            if (0 < UtilityWindow.Count)
                return;

            UtilityDialog = new UtilityWindow(Setting.Interface_MonospaceFont);
            UtilityDialog.Show();
        }

        private void LogButton_Click(object sender, RoutedEventArgs e)
        {
            if (LogWindow.Count == 0)
            {
                LogDialog = new LogWindow();
                LogDialog.Show();
            }
        }

        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (Model.WorkInProgress)
                return;

            /*
            Model.WorkInProgress = true;

            using (WebClient c = new WebClient())
            {
                // string str = c.DownloadData();
            }

            Model.WorkInProgress = false;
            */

            MessageBox.Show("Not Implemented", "Sorry", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            AboutWindow dialog = new AboutWindow(Setting.Interface_MonospaceFont);
            dialog.ShowDialog();
        }
        #endregion

        #region Script Buttons
        private async void ScriptRunButton_Click(object sender, RoutedEventArgs e)
        {
            if (CurMainTree?.Script == null)
                return;

            if (Model.WorkInProgress)
                return;

            Script sc = CurMainTree.Script;
            if (sc.Sections.ContainsKey("Process"))
            {
                if (Engine.WorkingLock == 0)  // Start Build
                {
                    Interlocked.Increment(ref Engine.WorkingLock);

                    // Populate BuildTree
                    Model.BuildTree.Children.Clear();
                    PopulateOneTreeView(sc, Model.BuildTree, Model.BuildTree);
                    CurBuildTree = null;

                    EngineState s = new EngineState(sc.Project, Logger, Model, EngineMode.RunMainAndOne, sc);
                    s.SetOption(Setting);

                    Engine.WorkingEngine = new Engine(s);

                    // Switch to Build View
                    Model.SwitchNormalBuildInterface = false;

                    // Run
                    int buildId = await Engine.WorkingEngine.Run($"{sc.Title} - Run");

#if DEBUG
                    Logger.ExportBuildLog(LogExportType.Text, Path.Combine(s.BaseDir, "LogDebugDump.txt"), buildId);
#endif

                    // Build Ended, Switch to Normal View
                    Model.SwitchNormalBuildInterface = true;
                    DrawScript(CurMainTree.Script);

                    if (Setting.General_ShowLogAfterBuild && LogWindow.Count == 0)
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

            StartRefreshScriptWorker();
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
                if (ScriptEditDialog.ShowDialog() == true)
                {
                    sc = ScriptEditDialog.Tag as Script;
                    Debug.Assert(sc != null, $"{nameof(sc)} != null");

                    DrawScript(sc);
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
                    OpenTextFile(sc.RealPath, false);
                    break;
                default:
                    OpenFolder(sc.RealPath);
                    break;
            }
        }

        private void ScriptUpdateButton(object sender, RoutedEventArgs e)
        {
            if (CurMainTree?.Script == null)
                return;
            if (Model.WorkInProgress)
                return;

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
                App.Logger.SystemWrite(new LogInfo(LogState.Error, msg));
            }
            else
            {
                PostRefreshScript(newScript);

                MessageBox.Show(msg, "Update Success", MessageBoxButton.OK, MessageBoxImage.Information);
                App.Logger.SystemWrite(new LogInfo(LogState.Success, msg));
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
            
        }

        private void ScriptCheckButton_Click(object sender, RoutedEventArgs e)
        {
            if (CurMainTree?.Script == null)
                return;
            if (Model.WorkInProgress)
                return;

            StartSyntaxCheckWorker(false);
        }

        private void ScriptOpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (CurMainTree?.Script == null)
                return;
            if (Model.WorkInProgress)
                return;

            Script sc = CurMainTree.Script;
            OpenFolder(Path.GetDirectoryName(sc.RealPath));
        }
        #endregion

        #region TreeView Methods
        private void ScriptListToTreeViewModel(
            Project project, List<Script> scList, bool assertDirExist,
            TreeViewModel treeRoot, TreeViewModel projectRoot = null)
        {
            Dictionary<string, TreeViewModel> dirDict = new Dictionary<string, TreeViewModel>(StringComparer.OrdinalIgnoreCase);

            // Populate MainScript
            if (projectRoot == null)
                projectRoot = PopulateOneTreeView(project.MainScript, treeRoot, treeRoot);

            foreach (Script sc in scList.Where(x => x.Type != ScriptType.Directory))
            {
                Debug.Assert(sc != null);

                if (sc.Equals(project.MainScript))
                    continue;

                // Current Parent
                TreeViewModel treeParent = projectRoot;

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
                            Debug.Assert(ts != null, "Internal Logic Error at ScriptListToTreeViewModel");

                        if (ts != null)
                        {
                            dirScript = new Script(ScriptType.Directory, ts.RealPath, ts.TreePath, project, project.ProjectRoot, sc.Level, false, false, ts.IsDirLink);
                        }
                        else
                        {
                            string fullTreePath = Path.Combine(project.ProjectRoot, project.ProjectName, pathKey);
                            dirScript = new Script(ScriptType.Directory, fullTreePath, fullTreePath, project, project.ProjectRoot, sc.Level, false, false, sc.IsDirLink);
                        }

                        treeParent = PopulateOneTreeView(dirScript, treeRoot, treeParent);
                        dirDict[key] = treeParent;
                    }
                }

                PopulateOneTreeView(sc, treeRoot, treeParent);
            }

            // Reflect Directory's Selected value
            RecursiveDecideDirectorySelectedValue(treeRoot, 0);
        }

        private static SelectedState RecursiveDecideDirectorySelectedValue(TreeViewModel parent, int depth)
        {
            SelectedState final = SelectedState.None;
            foreach (TreeViewModel item in parent.Children)
            {
                if (0 < item.Children.Count)
                { // Has child scripts
                    SelectedState state = RecursiveDecideDirectorySelectedValue(item, depth + 1);
                    if (depth != 0)
                    {
                        if (state == SelectedState.True)
                            final = item.Script.Selected = SelectedState.True;
                        else if (state == SelectedState.False)
                        {
                            if (final != SelectedState.True)
                                final = SelectedState.False;
                            if (item.Script.Selected != SelectedState.True)
                                item.Script.Selected = SelectedState.False;
                        }
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

        public void UpdateScriptTree(Project project, bool redrawScript, bool assertDirExist = true)
        {
            TreeViewModel projectRoot = Model.MainTree.Children.FirstOrDefault(x => x.Script.Project.Equals(project));
            projectRoot?.Children.Clear();

            ScriptListToTreeViewModel(project, project.VisibleScripts, assertDirExist, Model.MainTree, projectRoot);

            if (redrawScript && projectRoot != null)
            {
                CurMainTree = projectRoot;
                CurMainTree.IsExpanded = true;
                DrawScript(projectRoot.Script);
            }
        }

        public TreeViewModel PopulateOneTreeView(Script sc, TreeViewModel treeRoot, TreeViewModel treeParent)
        {
            TreeViewModel item = new TreeViewModel(treeRoot, treeParent)
            {
                Script = sc
            };
            treeParent.Children.Add(item);
            UpdateTreeViewIcon(item);

            return item;
        }

        public static TreeViewModel UpdateTreeViewIcon(TreeViewModel item)
        {
            Script sc = item.Script;

            if (sc.Type == ScriptType.Directory)
            {
                if (sc.IsDirLink)
                    item.Icon = ImageHelper.GetMaterialIcon(PackIconMaterialKind.FolderMove, 0);
                else
                    item.Icon = ImageHelper.GetMaterialIcon(PackIconMaterialKind.Folder, 0);
            }
            else if (sc.Type == ScriptType.Script)
            {
                if (sc.IsMainScript)
                    item.Icon = ImageHelper.GetMaterialIcon(PackIconMaterialKind.Settings, 0);
                else
                {
                    if (sc.IsDirLink)
                    {
                        if (sc.Mandatory)
                            item.Icon = ImageHelper.GetMaterialIcon(PackIconMaterialKind.LockOutline, 0);
                        else
                            item.Icon = ImageHelper.GetMaterialIcon(PackIconMaterialKind.OpenInNew, 0);
                    }
                    else
                    {
                        if (sc.Mandatory)
                            item.Icon = ImageHelper.GetMaterialIcon(PackIconMaterialKind.LockOutline, 0);
                        else
                            item.Icon = ImageHelper.GetMaterialIcon(PackIconMaterialKind.File, 0);
                    }
                }
            }
            else if (sc.Type == ScriptType.Link)
                item.Icon = ImageHelper.GetMaterialIcon(PackIconMaterialKind.OpenInNew, 0);
            else // Error
                item.Icon = ImageHelper.GetMaterialIcon(PackIconMaterialKind.WindowClose, 0);

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
                    if (focusedElement.DataContext is TreeViewModel node)
                    {
                        node.Checked = !node.Checked;
                        e.Handled = true;
                    }
                }
            }
        }

        private void MainTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (sender is TreeView tree && tree.SelectedItem is TreeViewModel model)
            {
                TreeViewModel item = CurMainTree = model;

                Dispatcher.Invoke(() =>
                {
                    Stopwatch watch = new Stopwatch();
                    watch.Start();
                    DrawScript(item.Script);
                    watch.Stop();
                    double msec = watch.Elapsed.TotalMilliseconds;
                    string filename = Path.GetFileName(CurMainTree.Script.TreePath);
                    Model.StatusBarText = $"{filename} rendered ({msec:0}ms)";
                });
            }
        }

        private void MainTreeView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            TreeViewItem treeViewItem = VisualUpwardSearch(e.OriginalSource as DependencyObject);

            if (treeViewItem != null)
            {
                treeViewItem.Focus();
                treeViewItem.IsSelected = true;
                e.Handled = true;
            }
        }

        static TreeViewItem VisualUpwardSearch(DependencyObject source)
        {
            while (source != null && !(source is TreeViewItem))
                source = VisualTreeHelper.GetParent(source);

            return source as TreeViewItem;
        }
        #endregion

        #region OpenTextFile, OpenFolder
        public void OpenTextFile(string filePath, bool deleteTextFile = false)
        {
            if (!File.Exists(filePath))
            {
                MessageBox.Show($"File [{filePath}] does not exist!", "Invalid Path", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Process proc = new Process();
            if (Setting.Interface_UseCustomEditor)
            {
                string ext = Path.GetExtension(Setting.Interface_CustomEditorPath);
                if (ext != null && !ext.Equals(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show($"Custom editor [{Setting.Interface_CustomEditorPath}] is not a executable!", "Invalid Custom Editor", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!File.Exists(Setting.Interface_CustomEditorPath))
                {
                    MessageBox.Show($"Custom editor [{Setting.Interface_CustomEditorPath}] does not exist!", "Invalid Custom Editor", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                proc.StartInfo = new ProcessStartInfo(Setting.Interface_CustomEditorPath)
                {
                    UseShellExecute = true,
                    Arguments = StringEscaper.Doublequote(filePath),
                };
            }
            else
            {
                proc.StartInfo = new ProcessStartInfo(filePath);
            }

            if (deleteTextFile)
                proc.Exited += (object pSender, EventArgs pEventArgs) => File.Delete(filePath);

            proc.Start();
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
            while (_refreshWorker.IsBusy)
                await Task.Delay(500);
            while (_cacheWorker.IsBusy)
                await Task.Delay(500);
            while (_loadWorker.IsBusy)
                await Task.Delay(500);

            _scriptCache?.WaitClose();
            Logger.DB.Close();
        }

        private void BuildConOutRedirectTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var focusedBackup = FocusManager.GetFocusedElement(this);

            if (sender is TextBox textBox)
            {
                textBox.CaretIndex = textBox.Text.Length;
                textBox.ScrollToEnd();
            }

            FocusManager.SetFocusedElement(this, focusedBackup);
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
            MainTree = new TreeViewModel(null, null);
            BuildTree = new TreeViewModel(null, null);

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

        private bool _isTreeEntryFile = true;
        public bool IsTreeEntryFile
        {
            get => _isTreeEntryFile;
            set
            {
                _isTreeEntryFile = value;
                OnPropertyUpdate(nameof(IsTreeEntryFile));
                OnPropertyUpdate(nameof(ScriptCheckVisiblility));
                OnPropertyUpdate(nameof(OpenExternalButtonToolTip));
                OnPropertyUpdate(nameof(OpenExternalButtonIconKind));
            }
        }

        public string OpenExternalButtonToolTip => IsTreeEntryFile ? "Edit Script" : "Open Folder";
        public PackIconMaterialKind OpenExternalButtonIconKind => IsTreeEntryFile ? PackIconMaterialKind.Pencil : PackIconMaterialKind.Folder;

        public string ScriptUpdateButtonToolTip => IsTreeEntryFile ? "Update Script" : "Update Scripts";

        private bool? _scriptCheckResult = null;
        public bool? ScriptCheckResult
        {
            get => _scriptCheckResult;
            set
            {
                _scriptCheckResult = value;
                OnPropertyUpdate(nameof(ScriptCheckIcon));
                OnPropertyUpdate(nameof(ScriptCheckColor));
                OnPropertyUpdate(nameof(ScriptCheckVisiblility));
            }
        }

        public PackIconMaterialKind ScriptCheckIcon
        {
            get
            {
                switch (_scriptCheckResult)
                {
                    case true:
                        return PackIconMaterialKind.Check;
                    case false:
                        return PackIconMaterialKind.Close;
                    default: // null
                        return PackIconMaterialKind.Magnify;
                }
            }
        }

        public Brush ScriptCheckColor
        {
            get
            {
                switch (_scriptCheckResult)
                {
                    case true:
                        return new SolidColorBrush(Colors.Green);
                    case false:
                        return new SolidColorBrush(Colors.Red);
                    default: // null
                        return new SolidColorBrush(Colors.Gray);
                }
            }
        }

        public Visibility ScriptCheckVisiblility
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

        private double bottomProgressBarValue = 0;
        public double BottomProgressBarValue
        {
            get => bottomProgressBarValue;
            set
            {
                bottomProgressBarValue = value;
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
                OnPropertyUpdate(nameof(ScriptCheckVisiblility));
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
                OnPropertyUpdate(nameof(ScriptCheckVisiblility));
            }
        }

        private TreeViewModel _mainTree;
        public TreeViewModel MainTree
        {
            get => _mainTree;
            set
            {
                _mainTree = value;
                OnPropertyUpdate(nameof(MainTree));
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
        private TreeViewModel _buildTree;
        public TreeViewModel BuildTree
        {
            get => _buildTree;
            set
            {
                _buildTree = value;
                OnPropertyUpdate(nameof(BuildTree));
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
        private string _buildConOutRedirect = string.Empty;
        public string BuildConOutRedirect
        {
            get => _buildConOutRedirect;
            set
            {
                _buildConOutRedirect = value;
                OnPropertyUpdate(nameof(BuildConOutRedirect));
            }
        }

        public static bool DisplayShellExecuteConOut = true;
        private Visibility _buildConOutRedirectVisibility = Visibility.Collapsed;
        public Visibility BuildConOutRedirectVisibility
        {
            get
            {
                if (DisplayShellExecuteConOut)
                    return _buildConOutRedirectVisibility;
                else
                    return Visibility.Collapsed;
            }
        }
        public bool BuildConOutRedirectShow
        {
            set
            {
                if (value)
                    _buildConOutRedirectVisibility = Visibility.Visible;
                else
                    _buildConOutRedirectVisibility = Visibility.Collapsed;
                OnPropertyUpdate(nameof(BuildConOutRedirectVisibility));
            }
        }

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

        private Visibility _buildCommandProgressVisibility = Visibility.Collapsed;
        public Visibility BuildCommandProgressVisibility => _buildCommandProgressVisibility;
        public bool BuildCommandProgressShow
        {
            set
            {
                if (value)
                    _buildCommandProgressVisibility = Visibility.Visible;
                else
                    _buildCommandProgressVisibility = Visibility.Collapsed;
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
    public class TreeViewModel : INotifyPropertyChanged
    {
        public TreeViewModel Root { get; }
        public TreeViewModel Parent { get; }

        public TreeViewModel(TreeViewModel root, TreeViewModel parent)
        {
            Root = root ?? this;
            Parent = parent;
        }

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

        #region Build Mode Property
        private bool _buildFocus = false;
        public bool BuildFocus
        {
            get => _buildFocus;
            set
            {
                _buildFocus = value;
                _icon.Foreground = BuildBrush;
                OnPropertyUpdate(nameof(BuildFontWeight));
                OnPropertyUpdate(nameof(BuildBrush));
                OnPropertyUpdate("BuildIcon");
            }
        }

        public FontWeight BuildFontWeight
        {
            get
            {
                if (_buildFocus)
                    return FontWeights.Bold;
                else
                    return FontWeights.Normal;
            }
        }

        public Brush BuildBrush
        {
            get
            {
                if (_buildFocus)
                    return Brushes.Red;
                else
                    return Brushes.Black;
            }
        }
        #endregion

        public bool Checked
        {
            get
            {
                switch (_script.Selected)
                {
                    case SelectedState.True:
                        return true;
                    default:
                        return false;
                }
            }
            set
            {
                MainWindow w = Application.Current.MainWindow as MainWindow;
                w?.Dispatcher.Invoke(() =>
                {
                    w.Model.WorkInProgress = true;
                    if (_script.Mandatory == false && _script.Selected != SelectedState.None)
                    {
                        if (value)
                        {
                            _script.Selected = SelectedState.True;

                            try
                            {
                                // Run 'Disable' directive
                                List<LogInfo> errorLogs = DisableScripts(Root, _script);
                                w.Logger.SystemWrite(errorLogs);
                            }
                            catch (Exception e)
                            {
                                w.Logger.SystemWrite(new LogInfo(LogState.Error, e));
                            }
                        }
                        else
                        {
                            _script.Selected = SelectedState.False;
                        }

                        if (_script.IsMainScript == false)
                        {
                            if (0 < Children.Count)
                            { // Set child scripts, too -> Top-down propagation
                                foreach (TreeViewModel childModel in Children)
                                    childModel.Checked = value;
                            }

                            ParentCheckedPropagation();
                        }

                        OnPropertyUpdate(nameof(Checked));
                    }
                    w.Model.WorkInProgress = false;
                });
            }
        }

        public void ParentCheckedPropagation()
        { // Bottom-up propagation of Checked property
            if (Parent == null)
                return;

            bool setParentChecked = false;

            foreach (TreeViewModel sibling in Parent.Children)
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

            if (!_script.Mandatory && _script.Selected != SelectedState.None)
            {
                if (value)
                    _script.Selected = SelectedState.True;
                else
                    _script.Selected = SelectedState.False;
            }

            OnPropertyUpdate(nameof(Checked));
            ParentCheckedPropagation();
        }

        public Visibility CheckBoxVisible
        {
            get
            {
                if (_script.Selected == SelectedState.None)
                    return Visibility.Collapsed;
                else
                    return Visibility.Visible;
            }
        }

        public string Text => _script.Title;

        private Script _script;
        public Script Script
        {
            get => _script;
            set
            {
                _script = value;
                OnPropertyUpdate(nameof(Script));
                OnPropertyUpdate(nameof(Checked));
                OnPropertyUpdate(nameof(CheckBoxVisible));
                OnPropertyUpdate(nameof(Text));
                OnPropertyUpdate(nameof(MainViewModel.MainCanvas));
            }
        }

        private Control _icon;
        public Control Icon
        {
            get => _icon;
            set
            {
                _icon = value;
                OnPropertyUpdate(nameof(Icon));
            }
        }

        public ObservableCollection<TreeViewModel> Children { get; private set; } = new ObservableCollection<TreeViewModel>();

        public void SortChildren()
        {
            IOrderedEnumerable<TreeViewModel> sorted = Children
                .OrderBy(x => x.Script.Level)
                .ThenBy(x => x.Script.Type)
                .ThenBy(x => x.Script.RealPath);
            Children = new ObservableCollection<TreeViewModel>(sorted);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyUpdate(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public TreeViewModel FindScriptByFullPath(string fullPath)
        {
            return RecursiveFindScriptByFullPath(Root, fullPath);
        }

        private static TreeViewModel RecursiveFindScriptByFullPath(TreeViewModel cur, string fullPath)
        {
            if (cur.Script != null)
            {
                if (fullPath.Equals(cur.Script.RealPath, StringComparison.OrdinalIgnoreCase))
                    return cur;
            }

            if (0 < cur.Children.Count)
            {
                foreach (TreeViewModel next in cur.Children)
                {
                    TreeViewModel found = RecursiveFindScriptByFullPath(next, fullPath);
                    if (found != null)
                        return found;
                }
            }

            // Not found in this path
            return null;
        }

        private List<LogInfo> DisableScripts(TreeViewModel root, Script sc)
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
                    TreeViewModel found = FindScriptByFullPath(path);
                    if (found != null)
                    {
                        if (sc.Type != ScriptType.Directory && sc.Mandatory == false && sc.Selected != SelectedState.None)
                            found.Checked = false;
                    }
                }
            }

            return errorLogs;
        }
    }
    #endregion

    #region Converters
    public class TaskbarProgressConverter : System.Windows.Data.IMultiValueConverter
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
    #endregion
}
