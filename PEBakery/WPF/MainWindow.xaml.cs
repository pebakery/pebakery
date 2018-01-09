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

using MahApps.Metro.IconPacks;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using SQLite;
using System.Text;
using PEBakery.Helper;
using PEBakery.IniLib;
using PEBakery.Core;
using System.Net;

namespace PEBakery.WPF
{
    #region MainWindow
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Constants
        internal const int PluginAuthorLenLimit = 35;
        #endregion

        #region Variables
        private ProjectCollection projects;
        public ProjectCollection Projects => projects;

        private string baseDir;
        public string BaseDir => baseDir;

        private BackgroundWorker loadWorker = new BackgroundWorker();
        private BackgroundWorker refreshWorker = new BackgroundWorker();
        private BackgroundWorker cacheWorker = new BackgroundWorker();
        private BackgroundWorker syntaxCheckWorker = new BackgroundWorker();

        private TreeViewModel curMainTree;
        public TreeViewModel CurMainTree => curMainTree;

        private TreeViewModel curBuildTree;
        public TreeViewModel CurBuildTree { get => curBuildTree; set => curBuildTree = value; }

        private Logger logger;
        public Logger Logger => logger;
        private PluginCache pluginCache;

        const int MaxDpiScale = 4;
        private int allPluginCount = 0;
        private readonly string settingFile;
        private SettingViewModel setting;
        public SettingViewModel Setting => setting;
        public MainViewModel Model { get; private set; }
        public Canvas MainCanvas => Model.MainCanvas;

        public LogWindow LogDialog = null;
        public UtilityWindow UtilityDialog = null;
        #endregion

        #region Constructor
        public MainWindow()
        {
            InitializeComponent();
            Model = this.DataContext as MainViewModel;

            string[] args = App.Args;
            if (int.TryParse(Properties.Resources.EngineVersion, NumberStyles.Integer, CultureInfo.InvariantCulture, out App.Version) == false)
            {
                MessageBox.Show($"Invalid version [{App.Version}]", "Invalid Version", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }  

            string argBaseDir = Environment.CurrentDirectory;
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "/basedir", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length)
                    {
                        argBaseDir = System.IO.Path.GetFullPath(args[i + 1]);
                        if (Directory.Exists(argBaseDir) == false)
                        {
                            MessageBox.Show($"Directory [{argBaseDir}] does not exist", "Invalid BaseDir", MessageBoxButton.OK, MessageBoxImage.Error);
                            Environment.Exit(1); // Force Shutdown
                            // Application.Current.Shutdown(1); // Grateful Shutdown
                        }
                        Environment.CurrentDirectory = argBaseDir;
                    }
                    else
                    {
                        Console.WriteLine("\'/basedir\' must be used with path\r\n");
                    }
                }
                else if (string.Equals(args[i], "/?", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(args[i], "/help", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(args[i], "/h", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Sorry, help message not implemented\r\n");
                }
            }

            this.baseDir = argBaseDir;

            this.settingFile = System.IO.Path.Combine(baseDir, "PEBakery.ini");
            this.setting = new SettingViewModel(settingFile);

            string dbDir = System.IO.Path.Combine(baseDir, "Database");
            if (!Directory.Exists(dbDir))
                Directory.CreateDirectory(dbDir);

            string logDBFile = System.IO.Path.Combine(dbDir, "PEBakeryLog.db");
            try
            {
                App.Logger = logger = new Logger(logDBFile);
                logger.System_Write(new LogInfo(LogState.Info, $"PEBakery launched"));
            }
            catch (SQLiteException e)
            { // Update failure
                string msg = $"SQLite Error : {e.Message}\r\n\r\nLog database is corrupted.\r\nPlease delete PEBakeryLog.db and restart.";
                MessageBox.Show(msg, "SQLite Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown(1);
            }
            this.setting.LogDB = logger.DB;

            // If plugin cache is enabled, generate cache after 5 seconds
            if (setting.Plugin_EnableCache)
            {
                string cacheDBFile = System.IO.Path.Combine(dbDir, "PEBakeryCache.db");
                try
                {
                    this.pluginCache = new PluginCache(cacheDBFile);
                    logger.System_Write(new LogInfo(LogState.Info, $"PluginCache enabled, {pluginCache.Table<DB_PluginCache>().Count()} cached plugin found"));
                }
                catch (SQLiteException e)
                { // Update failure
                    string msg = $"SQLite Error : {e.Message}\r\n\r\nCache database is corrupted.\r\nPlease delete PEBakeryCache.db and restart.";
                    MessageBox.Show(msg, "SQLite Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                    Application.Current.Shutdown(1);
                }

                this.setting.CacheDB = pluginCache;
            }
            else
            {
                logger.System_Write(new LogInfo(LogState.Info, $"PluginCache disabled"));
            }

            StartLoadWorker();
        }
        #endregion

        #region Background Workers
        public AutoResetEvent StartLoadWorker(bool quiet = false)
        {
            AutoResetEvent resetEvent = new AutoResetEvent(false);
            Stopwatch watch = Stopwatch.StartNew();

            // Set PEBakery Logo
            Image image = new Image()
            {
                UseLayoutRounding = true,
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.DownOnly,
                Source = ImageHelper.ToBitmapImage(Properties.Resources.DonutPng),
            };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            PluginLogo.Content = image;

            // Prepare PEBakery Loading Information
            if (quiet == false)
            {
                Model.PluginTitleText = "Welcome to PEBakery!";
                Model.PluginDescriptionText = "PEBakery loading...";
            }
            logger.System_Write(new LogInfo(LogState.Info, $@"Loading from [{baseDir}]"));
            MainCanvas.Children.Clear();
            (MainTreeView.DataContext as TreeViewModel).Children.Clear();

            int stage2LinksCount = 0;
            int loadedPluginCount = 0;
            int stage1CachedCount = 0;
            int stage2LoadedCount = 0;
            int stage2CachedCount = 0;

            Model.BottomProgressBarMinimum = 0;
            Model.BottomProgressBarMaximum = 100;
            Model.BottomProgressBarValue = 0;
            if (quiet == false)
                Model.WorkInProgress = true;
            Model.SwitchStatusProgressBar = false; // Show Progress Bar
            
            loadWorker = new BackgroundWorker();
            loadWorker.DoWork += (object sender, DoWorkEventArgs e) =>
            {
                string baseDir = (string)e.Argument;
                BackgroundWorker worker = sender as BackgroundWorker;

                // Init ProjectCollection
                if (setting.Plugin_EnableCache && pluginCache != null) // Use PluginCache - Fast speed, more memory
                {
                    if (pluginCache.IsGlobalCacheValid(baseDir))
                        projects = new ProjectCollection(baseDir, pluginCache);
                    else // Cache is invalid
                        projects = new ProjectCollection(baseDir, null);
                }
                else  // Do not use PluginCache - Slow speed, less memory
                {
                    projects = new ProjectCollection(baseDir, null);
                }

                allPluginCount = projects.PrepareLoad(out stage2LinksCount);
                Dispatcher.Invoke(() => { Model.BottomProgressBarMaximum = allPluginCount + stage2LinksCount; });

                // Let's load plugins parallelly
                List<LogInfo> errorLogs = projects.Load(worker);
                Logger.System_Write(errorLogs);
                setting.UpdateProjectList();

                if (0 < projects.ProjectNames.Count)
                { // Load Success
                    // Populate TreeView
                    Dispatcher.Invoke(() =>
                    {
                        foreach (Project project in projects.Projects)
                            PluginListToTreeViewModel(project, project.VisiblePlugins, Model.MainTree);

                        int pIdx = setting.Project_DefaultIndex;
                        curMainTree = Model.MainTree.Children[pIdx];
                        curMainTree.IsExpanded = true;
                        if (projects[pIdx] != null)
                            DrawPlugin(projects[pIdx].MainPlugin);
                    });

                    e.Result = true;
                }
                else
                {
                    e.Result = false;
                }
            };
            loadWorker.WorkerReportsProgress = true;
            loadWorker.ProgressChanged += (object sender, ProgressChangedEventArgs e) =>
            {
                Interlocked.Increment(ref loadedPluginCount);
                Model.BottomProgressBarValue = loadedPluginCount;
                string msg = string.Empty;
                switch (e.ProgressPercentage)
                {
                    case 0:  // Stage 1
                        if (e.UserState == null)
                            msg = $"Error";
                        else
                            msg = $"{e.UserState}";
                        break;
                    case 1:  // Stage 1, Cached
                        Interlocked.Increment(ref stage1CachedCount);
                        if (e.UserState == null)
                            msg = $"Cached - Error";
                        else
                            msg = $"Cached - {e.UserState}";
                        break;
                    case 2:  // Stage 2
                        Interlocked.Increment(ref stage2LoadedCount);
                        if (e.UserState == null)
                            msg = $"Error";
                        else
                            msg = $"{e.UserState}";
                        break;
                    case 3:  // Stage 2, Cached
                        Interlocked.Increment(ref stage2LoadedCount);
                        Interlocked.Increment(ref stage2CachedCount);
                        if (e.UserState == null)
                            msg = $"Cached - Error";
                        else
                            msg = $"Cached - {e.UserState}";
                        break;
                }
                int stage = e.ProgressPercentage / 2 + 1;
                if (stage == 1)
                    msg = $"Stage {stage} ({loadedPluginCount} / {allPluginCount}) \r\n{msg}";
                else
                    msg = $"Stage {stage} ({stage2LoadedCount} / {stage2LinksCount}) \r\n{msg}";

                Model.PluginDescriptionText = $"PEBakery loading...\r\n{msg}";               
            };
            loadWorker.RunWorkerCompleted += (object sender, RunWorkerCompletedEventArgs e) =>
            {
                if ((bool)e.Result)
                { // Load Success
                    StringBuilder b = new StringBuilder();
                    b.Append("Projects [");
                    List<Project> projList = projects.Projects;
                    for (int i = 0; i < projList.Count; i++)
                    {
                        b.Append(projList[i].ProjectName);
                        if (i + 1 < projList.Count)
                            b.Append(", ");
                    }
                    b.Append("] loaded");
                    logger.System_Write(new LogInfo(LogState.Info, b.ToString()));

                    watch.Stop();
                    double t = watch.Elapsed.TotalMilliseconds / 1000.0;
                    string msg;
                    if (setting.Plugin_EnableCache)
                    {
                        double cachePercent = (double)(stage1CachedCount + stage2CachedCount) * 100 / (allPluginCount + stage2LinksCount);
                        msg = $"{allPluginCount} plugins loaded ({t:0.#}s) - {cachePercent:0.#}% cached";
                        Model.StatusBarText = msg;
                    }
                    else
                    {
                        msg = $"{allPluginCount} plugins loaded ({t:0.#}s)";
                        Model.StatusBarText = msg;
                    }
                    if (quiet == false)
                        Model.WorkInProgress = false;
                    Model.SwitchStatusProgressBar = true; // Show Status Bar

                    logger.System_Write(new LogInfo(LogState.Info, msg));
                    logger.System_Write(Logger.LogSeperator);

                    // If plugin cache is enabled, generate cache.
                    if (setting.Plugin_EnableCache)
                        StartCacheWorker();
                }
                else
                {
                    Model.PluginTitleText = "Unable to find projects.";
                    Model.PluginDescriptionText = $"Please populate project in [{projects.ProjectRoot}]";

                    if (quiet == false)
                        Model.WorkInProgress = false;
                    Model.SwitchStatusProgressBar = true; // Show Status Bar
                    Model.StatusBarText = "Unable to find projects.";
                }

                resetEvent.Set();
            };

            loadWorker.RunWorkerAsync(baseDir);

            return resetEvent;
        }

        private void StartCacheWorker()
        {
            if (PluginCache.dbLock == 0)
            {
                Interlocked.Increment(ref PluginCache.dbLock);
                try
                {
                    Stopwatch watch = new Stopwatch();
                    cacheWorker = new BackgroundWorker();

                    Model.WorkInProgress = true;
                    int updatedCount = 0;
                    int cachedCount = 0;
                    cacheWorker.DoWork += (object sender, DoWorkEventArgs e) =>
                    {
                        BackgroundWorker worker = sender as BackgroundWorker;

                        watch = Stopwatch.StartNew();
                        pluginCache.CachePlugins(projects, baseDir, worker);
                    };

                    cacheWorker.WorkerReportsProgress = true;
                    cacheWorker.ProgressChanged += (object sender, ProgressChangedEventArgs e) =>
                    {
                        Interlocked.Increment(ref cachedCount);
                        if (e.ProgressPercentage == 1)
                            Interlocked.Increment(ref updatedCount);
                    };
                    cacheWorker.RunWorkerCompleted += (object sender, RunWorkerCompletedEventArgs e) =>
                    {
                        watch.Stop();

                        double cachePercent = (double)updatedCount * 100 / allPluginCount;

                        double t = watch.Elapsed.TotalMilliseconds / 1000.0;
                        string msg = $"{allPluginCount} plugins cached ({t:0.###}s), {cachePercent:0.#}% updated";
                        logger.System_Write(new LogInfo(LogState.Info, msg));
                        logger.System_Write(Logger.LogSeperator);

                        Model.WorkInProgress = false;
                    };
                    cacheWorker.RunWorkerAsync();
                }
                finally
                {
                    Interlocked.Decrement(ref PluginCache.dbLock);
                }
            }
        }
        
        public AutoResetEvent StartReloadPluginWorker()
        {
            AutoResetEvent resetEvent = new AutoResetEvent(false);

            if (curMainTree == null || curMainTree.Plugin == null)
                return null;

            if (refreshWorker.IsBusy)
                return null;

            Stopwatch watch = new Stopwatch();

            Model.WorkInProgress = true;
            refreshWorker = new BackgroundWorker();
            refreshWorker.DoWork += (object sender, DoWorkEventArgs e) =>
            {
                watch.Start();
                e.Result = curMainTree.Plugin.Project.RefreshPlugin(curMainTree.Plugin);
            };
            refreshWorker.RunWorkerCompleted += (object sender, RunWorkerCompletedEventArgs e) =>
            {
                if (e.Result is Plugin p)
                {
                    curMainTree.Plugin = p;
                    curMainTree.ParentCheckedPropagation();
                    UpdateTreeViewIcon(curMainTree);

                    DrawPlugin(curMainTree.Plugin);                   
                }

                Model.WorkInProgress = false;
                watch.Stop();
                double sec = watch.Elapsed.TotalSeconds;
                if ((Plugin)e.Result == null)
                    Model.StatusBarText = $"{Path.GetFileName(curMainTree.Plugin.ShortPath)} reload failed. ({sec:0.000}sec)";
                else
                    Model.StatusBarText = $"{Path.GetFileName(curMainTree.Plugin.ShortPath)} reloaded. ({sec:0.000}sec)";

                resetEvent.Set();
            };
            refreshWorker.RunWorkerAsync();

            return resetEvent;
        }

        private void StartSyntaxCheckWorker(bool quiet)
        {
            if (curMainTree == null || curMainTree.Plugin == null)
                return;

            if (syntaxCheckWorker.IsBusy)
                return;

            if (quiet == false)
                Model.WorkInProgress = true;

            Plugin p = curMainTree.Plugin;

            syntaxCheckWorker = new BackgroundWorker();
            syntaxCheckWorker.DoWork += (object sender, DoWorkEventArgs e) =>
            {
                CodeValidator v = new CodeValidator(p);
                v.Validate();

                e.Result = v;
            };
            syntaxCheckWorker.RunWorkerCompleted += (object sender, RunWorkerCompletedEventArgs e) =>
            {
                CodeValidator v = (CodeValidator) e.Result;

                LogInfo[] logs = v.LogInfos;
                LogInfo[] errorLogs = logs.Where(x => x.State == LogState.Error).ToArray();
                LogInfo[] warnLogs = logs.Where(x => x.State == LogState.Warning).ToArray();

                int errorWarns = 0;
                StringBuilder b = new StringBuilder();
                if (0 < errorLogs.Length)
                {
                    errorWarns += errorLogs.Length;

                    if (quiet == false)
                    {
                        b.AppendLine($"{errorLogs.Length} syntax error detected at [{p.ShortPath}]");
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

                    if (quiet == false)
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
                    Model.PluginCheckButtonColor = new SolidColorBrush(Colors.LightGreen);                   

                    if (quiet == false)
                    {
                        b.AppendLine("No syntax error detected");
                        b.AppendLine();
                        b.AppendLine($"Section Coverage : {v.Coverage * 100:0.#}% ({v.VisitedSectionCount}/{v.CodeSectionCount})");

                        MessageBox.Show(b.ToString(), "Syntax Check", MessageBoxButton.OK, MessageBoxImage.Information);
                    } 
                }
                else
                {
                    Model.PluginCheckButtonColor = new SolidColorBrush(Colors.Orange);

                    if (quiet == false)
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

                if (quiet == false)
                    Model.WorkInProgress = false;
            };
            syntaxCheckWorker.RunWorkerAsync();
        }
        #endregion

        #region DrawPlugin
        public void DrawPlugin(Plugin p)
        {
            Stopwatch watch = new Stopwatch();
            DrawPluginLogo(p);

            Model.PluginCheckButtonColor = new SolidColorBrush(Colors.LightGray);

            MainCanvas.Children.Clear();
            if (p.Type == PluginType.Directory)
            {
                Model.PluginTitleText = StringEscaper.Unescape(p.Title);
                Model.PluginDescriptionText = string.Empty;
                Model.PluginVersionText = string.Empty;
                Model.PluginAuthorText = string.Empty;
            }
            else
            {
                Model.PluginTitleText = StringEscaper.Unescape(p.Title);
                Model.PluginDescriptionText = StringEscaper.Unescape(p.Description);
                Model.PluginVersionText = "v" + p.Version;
                if (PluginAuthorLenLimit < p.Author.Length)
                    Model.PluginAuthorText = p.Author.Substring(0, PluginAuthorLenLimit) + "...";
                else
                    Model.PluginAuthorText = p.Author;

                double scaleFactor = setting.Interface_ScaleFactor / 100;
                ScaleTransform scale = new ScaleTransform(scaleFactor, scaleFactor);
                UIRenderer render = new UIRenderer(MainCanvas, this, p, logger, scaleFactor);
                MainCanvas.LayoutTransform = scale;
                render.Render();
                
                if (setting.Plugin_AutoSyntaxCheck)
                    StartSyntaxCheckWorker(true);
            }
            Model.OnPropertyUpdate("MainCanvas");
        }

        public void DrawPluginLogo(Plugin p)
        {
            double size = PluginLogo.ActualWidth * MaxDpiScale;
            if (p.Type == PluginType.Directory)
            {
                if (p.IsDirLink)
                    PluginLogo.Content = ImageHelper.GetMaterialIcon(PackIconMaterialKind.FolderMove, 0);
                else
                    PluginLogo.Content = ImageHelper.GetMaterialIcon(PackIconMaterialKind.Folder, 0);
            }
            else
            {
                try
                {
                    ImageSource imageSource;

                    using (MemoryStream mem = EncodedFile.ExtractLogo(p, out ImageHelper.ImageType type))
                    {
                        mem.Position = 0;
                        
                        if (type == ImageHelper.ImageType.Svg)
                            imageSource = ImageHelper.SvgToBitmapImage(mem, size, size);
                        else
                            imageSource = ImageHelper.ImageToBitmapImage(mem);
                    }

                    Image image = new Image
                    {
                        StretchDirection = StretchDirection.DownOnly,
                        Stretch = Stretch.Uniform,
                        UseLayoutRounding = true, // To prevent blurry image rendering
                        Source = imageSource,
                    };

                    Grid grid = new Grid();
                    grid.Children.Add(image);

                    PluginLogo.Content = grid;
                }
                catch
                { // No logo file - use default
                    if (p.Type == PluginType.Plugin)
                    {
                        if (p.IsDirLink)
                            PluginLogo.Content = ImageHelper.GetMaterialIcon(PackIconMaterialKind.FileSend, 5);
                        else
                            PluginLogo.Content = ImageHelper.GetMaterialIcon(PackIconMaterialKind.FileDocument, 0);
                    }
                    else if (p.Type == PluginType.Link)
                    {
                        PluginLogo.Content = ImageHelper.GetMaterialIcon(PackIconMaterialKind.FileSend, 5);
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

                if (curMainTree == null || curMainTree.Plugin == null || Model.WorkInProgress)
                {
                    Interlocked.Decrement(ref Engine.WorkingLock);
                    return;
                }

                // Determine current project
                Project project = curMainTree.Plugin.Project;

                Model.BuildTree.Children.Clear();
                PluginListToTreeViewModel(project, project.ActivePlugins, Model.BuildTree);
                curBuildTree = null;

                EngineState s = new EngineState(project, logger, Model);
                s.SetOption(setting);

                Engine.WorkingEngine = new Engine(s);

                // Build Start, Switch to Build View
                Model.SwitchNormalBuildInterface = false;

                // Turn on progress ring
                Model.WorkInProgress = true;

                Stopwatch watch = Stopwatch.StartNew();

                // Run
                long buildId = await Engine.WorkingEngine.Run($"Project {project.ProjectName}");

#if DEBUG  // TODO: Remove this later, this line is for Debug
                logger.ExportBuildLog(LogExportType.Text, Path.Combine(s.BaseDir, "LogDebugDump.txt"), buildId);
#endif
                // Turn off progress ring
                Model.WorkInProgress = false;

                // Build Ended, Switch to Normal View
                Model.SwitchNormalBuildInterface = true;
                DrawPlugin(curMainTree.Plugin);

                watch.Stop();
                TimeSpan t = watch.Elapsed;
                Model.StatusBarText = $"{project.ProjectName} build done ({t:h\\:mm\\:ss})";

                if (setting.General_ShowLogAfterBuild && LogWindow.Count == 0)
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
            if (loadWorker.IsBusy == false)
            {
                (MainTreeView.DataContext as TreeViewModel).Children.Clear();

                StartLoadWorker();
            }
        }

        private void SettingButton_Click(object sender, RoutedEventArgs e)
        {
            if (loadWorker.IsBusy == false)
            {
                double old_Interface_ScaleFactor = setting.Interface_ScaleFactor;
                bool old_Plugin_EnableCache = setting.Plugin_EnableCache;

                SettingWindow dialog = new SettingWindow(setting);
                bool? result = dialog.ShowDialog();
                if (result == true)
                {
                    // Scale Factor
                    double newScaleFactor = setting.Interface_ScaleFactor;
                    if (double.Epsilon < Math.Abs(newScaleFactor - old_Interface_ScaleFactor)) // Not Equal
                        DrawPlugin(curMainTree.Plugin);

                    // Plugin
                    if (old_Plugin_EnableCache == false && setting.Plugin_EnableCache)
                        StartCacheWorker();

                    // Apply
                    Setting.ApplySetting();
                }
            }
        }

        private void UtilityButton_Click(object sender, RoutedEventArgs e)
        {
            if (loadWorker.IsBusy == false)
            {
                if (UtilityWindow.Count == 0)
                {
                    UtilityDialog = new UtilityWindow(setting.Interface_MonospaceFont);
                    UtilityDialog.Show();
                }
            }
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

            Model.WorkInProgress = true;

            using (WebClient c = new WebClient())
            {
                // string str = c.DownloadData();
            }

            Model.WorkInProgress = false;
            MessageBox.Show("Not Implemented", "Sorry", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            AboutWindow dialog = new AboutWindow(setting.Interface_MonospaceFont);
            dialog.ShowDialog();
        }
        #endregion

        #region Plugin Buttons
        private async void PluginRunButton_Click(object sender, RoutedEventArgs e)
        {
            if (curMainTree == null || curMainTree.Plugin == null)
                return;

            if (Model.WorkInProgress)
                return;

            Plugin p = curMainTree.Plugin;
            if (p.Sections.ContainsKey("Process"))
            {
                if (Engine.WorkingLock == 0)  // Start Build
                {
                    Interlocked.Increment(ref Engine.WorkingLock);

                    // Populate BuildTree
                    Model.BuildTree.Children.Clear();
                    PopulateOneTreeView(p, Model.BuildTree, Model.BuildTree);
                    curBuildTree = null;

                    EngineState s = new EngineState(p.Project, logger, Model, p);
                    s.SetOption(setting);

                    Engine.WorkingEngine = new Engine(s);

                    // Switch to Build View
                    Model.SwitchNormalBuildInterface = false;

                    // Run
                    long buildId = await Engine.WorkingEngine.Run($"{p.Title} - Run");

#if DEBUG  // TODO: Remove this later, this line is for Debug
                    logger.ExportBuildLog(LogExportType.Text, Path.Combine(s.BaseDir, "LogDebugDump.txt"), buildId);
#endif

                    // Build Ended, Switch to Normal View
                    Model.SwitchNormalBuildInterface = true;
                    DrawPlugin(curMainTree.Plugin);

                    if (setting.General_ShowLogAfterBuild && LogWindow.Count == 0)
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
                Model.StatusBarText = $"Section [Process] does not exist in {p.Title}";
            }
        }

        private void PluginEditButton_Click(object sender, RoutedEventArgs e)
        {
            if (curMainTree == null || curMainTree.Plugin == null)
                return;

            if (Model.WorkInProgress)
                return;

            OpenTextFile(curMainTree.Plugin.FullPath, false);
        }

        private void PluginRefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (curMainTree == null || curMainTree.Plugin == null)
                return;

            if (Model.WorkInProgress)
                return;

            StartReloadPluginWorker();
        }

        private void PluginCheckButton_Click(object sender, RoutedEventArgs e)
        {
            if (curMainTree == null || curMainTree.Plugin == null)
                return;

            if (Model.WorkInProgress)
                return;

            StartSyntaxCheckWorker(false);
        }
        #endregion

        #region TreeView Methods
        private void PluginListToTreeViewModel(Project project, List<Plugin> pList, TreeViewModel treeRoot, TreeViewModel projectRoot = null)
        {
            Dictionary<string, TreeViewModel> dirDict = new Dictionary<string, TreeViewModel>(StringComparer.OrdinalIgnoreCase);

            // Populate MainPlugin
            if (projectRoot == null)
                projectRoot = PopulateOneTreeView(project.MainPlugin, treeRoot, treeRoot);

            foreach (Plugin p in pList)
            {
                Debug.Assert(p != null);

                if (p.Equals(project.MainPlugin))
                    continue;

                // Current Parent
                TreeViewModel treeParent = projectRoot;

                int idx = p.ShortPath.IndexOf('\\');
                if (idx == -1)
                    continue;
                string[] paths = p.ShortPath
                    .Substring(idx + 1)
                    .Split(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });

                // Ex) Apps\Network\Mozilla_Firefox_CR.script
                for (int i = 0; i < paths.Length - 1; i++)
                {
                    string pathKey = Project.PathKeyGenerator(paths, i);
                    string key = $"{p.Level}_{pathKey}";
                    if (dirDict.ContainsKey(key))
                    {
                        treeParent = dirDict[key];
                    }
                    else
                    {
                        string fullPath = Path.Combine(project.ProjectRoot, project.ProjectName, pathKey);
                        Plugin dirPlugin = new Plugin(PluginType.Directory, fullPath, project, project.ProjectRoot, p.Level, false, false, p.IsDirLink);
                        treeParent = PopulateOneTreeView(dirPlugin, treeRoot, treeParent);
                        dirDict[key] = treeParent;
                    }
                }

                PopulateOneTreeView(p, treeRoot, treeParent);
            }

            // Reflect Directory's Selected value
            RecursiveDecideDirectorySelectedValue(treeRoot, 0);
        }

        private SelectedState RecursiveDecideDirectorySelectedValue(TreeViewModel parent, int depth)
        {
            SelectedState final = SelectedState.None;
            foreach (TreeViewModel item in parent.Children)
            {
                if (0 < item.Children.Count)
                { // Has child plugins
                    SelectedState state = RecursiveDecideDirectorySelectedValue(item, depth + 1);
                    if (depth != 0)
                    {
                        if (state == SelectedState.True)
                            final = item.Plugin.Selected = SelectedState.True;
                        else if (state == SelectedState.False)
                        {
                            if (final != SelectedState.True)
                                final = SelectedState.False;
                            if (item.Plugin.Selected != SelectedState.True)
                                item.Plugin.Selected = SelectedState.False;
                        }
                    }
                }
                else // Does not have child plugin
                {
                    switch (item.Plugin.Selected)
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

        public void UpdatePluginTree(Project project, bool redrawPlugin)
        {
            TreeViewModel projectRoot = Model.MainTree.Children.FirstOrDefault(x => x.Plugin.Project.Equals(project));
            if (projectRoot != null) // Remove existing project tree
                projectRoot.Children.Clear();

            PluginListToTreeViewModel(project, project.VisiblePlugins, Model.MainTree, projectRoot);

            if (redrawPlugin)
            {
                curMainTree = projectRoot;
                curMainTree.IsExpanded = true;
                DrawPlugin(projectRoot.Plugin);
            }
        }

        public TreeViewModel PopulateOneTreeView(Plugin p, TreeViewModel treeRoot, TreeViewModel treeParent)
        {
            TreeViewModel item = new TreeViewModel(treeRoot, treeParent)
            {
                Plugin = p
            };
            treeParent.Children.Add(item);
            UpdateTreeViewIcon(item);

            return item;
        }

        TreeViewModel UpdateTreeViewIcon(TreeViewModel item)
        {
            Plugin p = item.Plugin;

            if (p.Type == PluginType.Directory)
            {
                if (p.IsDirLink)
                    item.Icon = ImageHelper.GetMaterialIcon(PackIconMaterialKind.FolderMove, 0);
                else
                    item.Icon = ImageHelper.GetMaterialIcon(PackIconMaterialKind.Folder, 0);
            }
            else if (p.Type == PluginType.Plugin)
            {
                if (p.IsMainPlugin)
                    item.Icon = ImageHelper.GetMaterialIcon(PackIconMaterialKind.Settings, 0);
                else
                {
                    if (p.IsDirLink)
                    {
                        if (p.Mandatory)
                            item.Icon = ImageHelper.GetMaterialIcon(PackIconMaterialKind.LockOutline, 0);
                        else
                            item.Icon = ImageHelper.GetMaterialIcon(PackIconMaterialKind.OpenInNew, 0);
                    }
                    else
                    {
                        if (p.Mandatory)
                            item.Icon = ImageHelper.GetMaterialIcon(PackIconMaterialKind.LockOutline, 0);
                        else
                            item.Icon = ImageHelper.GetMaterialIcon(PackIconMaterialKind.File, 0);
                    }

                }

            }
            else if (p.Type == PluginType.Link)
            {
                item.Icon = ImageHelper.GetMaterialIcon(PackIconMaterialKind.OpenInNew, 0);
            }
            else
            { // Error
                item.Icon = ImageHelper.GetMaterialIcon(PackIconMaterialKind.WindowClose, 0);
            }

            return item;
        }

        private void MainTreeView_Loaded(object sender, RoutedEventArgs e)
        {
            Window window = Window.GetWindow(this);
            window.KeyDown += MainTreeView_KeyDown;
        }

        /// <summary>
        /// Used to ensure pressing 'Space' to toggle TreeView's checkbox.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainTreeView_KeyDown(object sender, KeyEventArgs e)
        {
            // Window window = sender as Window;
            base.OnKeyDown(e);

            if (e.Key == Key.Space)
            {
                if (Keyboard.FocusedElement is FrameworkElement focusedElement)
                {
                    if (focusedElement.DataContext is TreeViewModel node)
                    {
                        if (node.Checked == true)
                            node.Checked = false;
                        else if (node.Checked == false)
                            node.Checked = true;
                        e.Handled = true;
                    }
                }
            }
        }

        private void MainTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var tree = sender as TreeView;

            if (tree.SelectedItem is TreeViewModel)
            {
                TreeViewModel item = curMainTree = tree.SelectedItem as TreeViewModel;

                Dispatcher.Invoke(() =>
                {
                    Stopwatch watch = new Stopwatch();
                    watch.Start();
                    DrawPlugin(item.Plugin);
                    watch.Stop();
                    double msec = watch.Elapsed.TotalMilliseconds;
                    string filename = Path.GetFileName(curMainTree.Plugin.ShortPath);
                    Model.StatusBarText = $"{filename} rendered ({msec:0}ms)";
                });
            }
        }
        #endregion

        #region OpenTextFile
        public Process OpenTextFile(string textFile, bool deleteTextFile = false)
        {
            Process proc = new Process();

            bool startInfoValid = false;
            if (setting.Interface_UseCustomEditor)
            {
                if (!Path.GetExtension(setting.Interface_CustomEditorPath).Equals(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show($"Custom editor [{setting.Interface_CustomEditorPath}] is not a executable!", "Invalid Custom Editor", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else if (!File.Exists(setting.Interface_CustomEditorPath))
                {
                    MessageBox.Show($"Custom editor [{setting.Interface_CustomEditorPath}] does not exist!", "Invalid Custom Editor", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    proc.StartInfo = new ProcessStartInfo(setting.Interface_CustomEditorPath)
                    {
                        UseShellExecute = true,
                        Arguments = textFile,
                    };
                    startInfoValid = true;
                }
            }
            
            if (startInfoValid == false)
            {
                proc.StartInfo = new ProcessStartInfo(textFile)
                {
                    UseShellExecute = true,
                };
            }

            if (deleteTextFile)
                proc.Exited += (object pSender, EventArgs pEventArgs) => File.Delete(textFile);

            proc.Start();

            return proc;
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

            // TODO: Do this in more clean way
            while (refreshWorker.IsBusy)
                await Task.Delay(500);

            while (cacheWorker.IsBusy)
                await Task.Delay(500);

            while (loadWorker.IsBusy)
                await Task.Delay(500);

            if (pluginCache != null)
                pluginCache.WaitClose();
            logger.DB.Close();
        }

        private void BuildConOutRedirectTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            var focusedBackup = FocusManager.GetFocusedElement(this);

            textBox.CaretIndex = textBox.Text.Length;
            textBox.ScrollToEnd();

            FocusManager.SetFocusedElement(this, focusedBackup);
        }
        #endregion
    }
    #endregion

    #region MainViewModel
    public class MainViewModel : INotifyPropertyChanged
    {
        public MainViewModel()
        {
            MainTree = new TreeViewModel(null, null);
            BuildTree = new TreeViewModel(null, null);

            Canvas canvas = new Canvas()
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

        #region Normal Interface
        private bool workInProgress = false;
        public bool WorkInProgress
        {
            get => workInProgress;
            set
            {
                workInProgress = value;
                OnPropertyUpdate("WorkInProgress");
            }
        }

        private string pluginTitleText = "Welcome to PEBakery!";
        public string PluginTitleText
        {
            get => pluginTitleText;
            set
            {
                pluginTitleText = value;
                OnPropertyUpdate("PluginTitleText");
            }
        }

        private string pluginAuthorText = string.Empty;
        public string PluginAuthorText
        {
            get => pluginAuthorText;
            set
            {
                pluginAuthorText = value;
                OnPropertyUpdate("PluginAuthorText");
            }
        }

        private string pluginVersionText = Properties.Resources.StringVersion;
        public string PluginVersionText
        {
            get => pluginVersionText;
            set
            {
                pluginVersionText = value;
                OnPropertyUpdate("PluginVersionText");
            }
        }

        private string pluginDescriptionText = "PEBakery is now loading, please wait...";
        public string PluginDescriptionText
        {
            get => pluginDescriptionText;
            set
            {
                pluginDescriptionText = value;
                OnPropertyUpdate("PluginDescriptionText");
            }
        }

        private Brush pluginCheckButtonColor = new SolidColorBrush(Colors.LightGray);
        public Brush PluginCheckButtonColor
        {
            get => pluginCheckButtonColor;
            set
            {
                pluginCheckButtonColor = value;
                OnPropertyUpdate("PluginCheckButtonColor");
            }
        }

        private string statusBarText = string.Empty;
        public string StatusBarText
        {
            get => statusBarText;
            set
            {
                statusBarText = value;
                OnPropertyUpdate("StatusBarText");
            }
        }

        // True - StatusBar, False - ProgressBar
        private bool switchStatusProgressBar = false;
        public bool SwitchStatusProgressBar
        {
            get => switchStatusProgressBar;
            set
            {
                switchStatusProgressBar = value;
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

        private Visibility bottomStatusBarVisibility = Visibility.Collapsed;
        public Visibility BottomStatusBarVisibility
        {
            get => bottomStatusBarVisibility;
            set
            {
                bottomStatusBarVisibility = value;
                OnPropertyUpdate("BottomStatusBarVisibility");
            }
        }

        private double bottomProgressBarMinimum = 0;
        public double BottomProgressBarMinimum
        {
            get => bottomProgressBarMinimum;
            set
            {
                bottomProgressBarMinimum = value;
                OnPropertyUpdate("BottomProgressBarMinimum");
            }
        }

        private double bottomProgressBarMaximum = 100;
        public double BottomProgressBarMaximum
        {
            get => bottomProgressBarMaximum;
            set
            {
                bottomProgressBarMaximum = value;
                OnPropertyUpdate("BottomProgressBarMaximum");
            }
        }

        private double bottomProgressBarValue = 0;
        public double BottomProgressBarValue
        {
            get => bottomProgressBarValue;
            set
            {
                bottomProgressBarValue = value;
                OnPropertyUpdate("BottomProgressBarValue");
            }
        }

        private Visibility bottomProgressBarVisibility = Visibility.Visible;
        public Visibility BottomProgressBarVisibility
        {
            get => bottomProgressBarVisibility;
            set
            {
                bottomProgressBarVisibility = value;
                OnPropertyUpdate("BottomProgressBarVisibility");
            }
        }

        // True - Normal, False - Build
        private bool switchNormalBuildInterface = true;
        public bool SwitchNormalBuildInterface
        {
            get => switchNormalBuildInterface;
            set
            {
                switchNormalBuildInterface = value;
                if (value)
                { // To Normal View
                    NormalInterfaceVisibility = Visibility.Visible;
                    BuildInterfaceVisibility = Visibility.Collapsed;
                }
                else
                { // To Build View
                    BuildPosition = string.Empty;
                    BuildEchoMessage = string.Empty;

                    BuildPluginProgressBarValue = 0;
                    BuildFullProgressBarValue = 0;

                    NormalInterfaceVisibility = Visibility.Collapsed;
                    BuildInterfaceVisibility = Visibility.Visible;
                }
            }
        }

        private Visibility normalInterfaceVisibility = Visibility.Visible;
        public Visibility NormalInterfaceVisibility
        {
            get => normalInterfaceVisibility;
            set
            {
                normalInterfaceVisibility = value;
                OnPropertyUpdate("NormalInterfaceVisibility");
            }
        }

        private Visibility buildInterfaceVisibility = Visibility.Collapsed;
        public Visibility BuildInterfaceVisibility
        {
            get => buildInterfaceVisibility;
            set
            {
                buildInterfaceVisibility = value;
                OnPropertyUpdate("BuildInterfaceVisibility");
            }
        }

        private TreeViewModel mainTree;
        public TreeViewModel MainTree
        {
            get => mainTree;
            set
            {
                mainTree = value;
                OnPropertyUpdate("MainTree");
            }
        }

        private Canvas mainCanvas;
        public Canvas MainCanvas
        {
            get => mainCanvas;
            set
            {
                mainCanvas = value;
                OnPropertyUpdate("MainCanvas");
            }
        }
        #endregion

        #region Build Interface
        private TreeViewModel buildTree;
        public TreeViewModel BuildTree
        {
            get => buildTree;
            set
            {
                buildTree = value;
                OnPropertyUpdate("BuildTree");
            }
        }

        private string buildPosition = string.Empty;
        public string BuildPosition
        {
            get => buildPosition;
            set
            {
                buildPosition = value;
                OnPropertyUpdate("BuildPosition");
            }
        }

        private string buildEchoMessage = string.Empty;
        public string BuildEchoMessage
        {
            get => buildEchoMessage;
            set
            {
                buildEchoMessage = value;
                OnPropertyUpdate("BuildEchoMessage");
            }
        }

        // ProgressBar
        private double buildPluginProgressBarMax = 100;
        public double BuildPluginProgressBarMax
        {
            get => buildPluginProgressBarMax;
            set
            {
                buildPluginProgressBarMax = value;
                OnPropertyUpdate("BuildPluginProgressBarMax");
            }
        }

        private double buildPluginProgressBarValue = 0;
        public double BuildPluginProgressBarValue
        {
            get => buildPluginProgressBarValue;
            set
            {
                buildPluginProgressBarValue = value;
                OnPropertyUpdate("BuildPluginProgressBarValue");
            }
        }

        private Visibility buildFullProgressBarVisibility = Visibility.Visible;
        public Visibility BuildFullProgressBarVisibility
        {
            get => buildFullProgressBarVisibility;
            set
            {
                buildFullProgressBarVisibility = value;
                OnPropertyUpdate("BuildFullProgressBarVisibility");
            }
        }

        private double buildFullProgressBarMax = 100;
        public double BuildFullProgressBarMax
        {
            get => buildFullProgressBarMax;
            set
            {
                buildFullProgressBarMax = value;
                OnPropertyUpdate("BuildFullProgressBarMax");
            }
        }

        private double buildFullProgressBarValue = 0;
        public double BuildFullProgressBarValue
        {
            get => buildFullProgressBarValue;
            set
            {
                buildFullProgressBarValue = value;
                OnPropertyUpdate("BuildFullProgressBarValue");
            }
        }

        // ShellExecute Console Output
        private string buildConOutRedirect = string.Empty;
        public string BuildConOutRedirect
        {
            get => buildConOutRedirect;
            set
            {
                buildConOutRedirect = value;
                OnPropertyUpdate("BuildConOutRedirect");
            }
        }

        public static bool DisplayShellExecuteConOut = true;
        private Visibility buildConOutRedirectVisibility = Visibility.Collapsed;
        public Visibility BuildConOutRedirectVisibility
        {
            get
            {
                if (DisplayShellExecuteConOut)
                    return buildConOutRedirectVisibility;
                else
                    return Visibility.Collapsed;
            }
        }
        public bool BuildConOutRedirectShow
        {
            set
            {
                if (value)
                    buildConOutRedirectVisibility = Visibility.Visible;
                else
                    buildConOutRedirectVisibility = Visibility.Collapsed;
                OnPropertyUpdate("BuildConOutRedirectVisibility");
            }
        }

        // Command Progress
        private string buildCommandProgressTitle = string.Empty;
        public string BuildCommandProgressTitle
        {
            get => buildCommandProgressTitle;
            set
            {
                buildCommandProgressTitle = value;
                OnPropertyUpdate("BuildCommandProgressTitle");
            }
        }

        private string buildCommandProgressText = string.Empty;
        public string BuildCommandProgressText
        {
            get => buildCommandProgressText;
            set
            {
                buildCommandProgressText = value;
                OnPropertyUpdate("BuildCommandProgressText");
            }
        }

        private double buildCommandProgressMax = 100;
        public double BuildCommandProgressMax
        {
            get => buildCommandProgressMax;
            set
            {
                buildCommandProgressMax = value;
                OnPropertyUpdate("BuildCommandProgressMax");
            }
        }

        private double buildCommandProgressValue = 0;
        public double BuildCommandProgressValue
        {
            get => buildCommandProgressValue;
            set
            {
                buildCommandProgressValue = value;
                OnPropertyUpdate("BuildCommandProgressValue");
            }
        }

        private Visibility buildCommandProgressVisibility = Visibility.Collapsed;
        public Visibility BuildCommandProgressVisibility => buildCommandProgressVisibility;
        public bool BuildCommandProgressShow
        {
            set
            {
                if (value)
                    buildCommandProgressVisibility = Visibility.Visible;
                else
                    buildCommandProgressVisibility = Visibility.Collapsed;
                OnPropertyUpdate("BuildCommandProgressVisibility");
            }
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
        private TreeViewModel root;
        public TreeViewModel Root { get => root; }

        private TreeViewModel parent;
        public TreeViewModel Parent { get => parent; }

        public TreeViewModel(TreeViewModel root, TreeViewModel parent)
        {
            if (root == null)
                this.root = this;
            else
                this.root = root;
            this.parent = parent;
        }

        private bool isExpanded = false;
        public bool IsExpanded
        {
            get => isExpanded;
            set
            {
                isExpanded = value;
                OnPropertyUpdate("IsExpanded");
            }
        }

        #region Build Mode Property
        private bool buildFocus = false;
        public bool BuildFocus
        {
            get => buildFocus;
            set
            {
                buildFocus = value;
                icon.Foreground = BuildBrush;
                OnPropertyUpdate("BuildFontWeight");
                OnPropertyUpdate("BuildBrush");
                OnPropertyUpdate("BuildIcon");
            }
        }

        public FontWeight BuildFontWeight
        {
            get
            {
                if (buildFocus)
                    return FontWeights.Bold;
                else
                    return FontWeights.Normal;
            }
        }

        public Brush BuildBrush
        {
            get
            {
                if (buildFocus)
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
                switch (plugin.Selected)
                {
                    case SelectedState.True:
                        return true;
                    default:
                        return false;
                }
            }
            set
            {
                MainWindow w = (Application.Current.MainWindow as MainWindow);
                w.Dispatcher.Invoke(() =>
                {
                    w.Model.WorkInProgress = true;
                    if (plugin.Mandatory == false && plugin.Selected != SelectedState.None)
                    {
                        if (value)
                        {
                            plugin.Selected = SelectedState.True;

                            try
                            {
                                // Run 'Disable' directive
                                List<LogInfo> errorLogs = DisablePlugins(root, plugin);
                                w.Logger.System_Write(errorLogs);
                            }
                            catch (Exception e)
                            {
                                w.Logger.System_Write(new LogInfo(LogState.Error, e));
                            }
                        }
                        else
                        {
                            plugin.Selected = SelectedState.False;
                        }

                        if (plugin.IsMainPlugin == false)
                        {
                            if (0 < this.Children.Count)
                            { // Set child plugins, too -> Top-down propagation
                                foreach (TreeViewModel childModel in this.Children)
                                {
                                    if (value)
                                        childModel.Checked = true;
                                    else
                                        childModel.Checked = false;
                                }
                            }

                            ParentCheckedPropagation();
                        }

                        OnPropertyUpdate("Checked");
                    }
                    w.Model.WorkInProgress = false;
                });
            }
        }

        public void ParentCheckedPropagation()
        { // Bottom-up propagation of Checked property
            if (parent == null)
                return;

            bool setParentChecked = false;

            foreach (TreeViewModel sibling in parent.Children)
            { // Siblings
                if (sibling.Checked)
                    setParentChecked = true;
            }

            parent.SetParentChecked(setParentChecked);
        }

        public void SetParentChecked(bool value)
        {
            if (parent == null)
                return;

            if (plugin.Mandatory == false && plugin.Selected != SelectedState.None)
            {
                if (value)
                    plugin.Selected = SelectedState.True;
                else
                    plugin.Selected = SelectedState.False;
            }

            OnPropertyUpdate("Checked");
            ParentCheckedPropagation();
        }

        public Visibility CheckBoxVisible
        {
            get
            {
                if (plugin.Selected == SelectedState.None)
                    return Visibility.Collapsed;
                else
                    return Visibility.Visible;
            }
        }

        public string Text { get => plugin.Title; }

        private Plugin plugin;
        public Plugin Plugin
        {
            get => plugin;
            set
            {
                plugin = value;
                OnPropertyUpdate("Plugin");
                OnPropertyUpdate("Checked");
                OnPropertyUpdate("CheckBoxVisible");
                OnPropertyUpdate("Text");
                OnPropertyUpdate("MainCanvas");
            }
        }

        private Control icon;
        public Control Icon
        {
            get => icon;
            set
            {
                icon = value;
                OnPropertyUpdate("Icon");
            }
        }

        private ObservableCollection<TreeViewModel> children = new ObservableCollection<TreeViewModel>();
        public ObservableCollection<TreeViewModel> Children { get => children; }

        public void SortChildren()
        {
            IOrderedEnumerable<TreeViewModel> sorted = children
                .OrderBy(x => x.Plugin.Level)
                .ThenBy(x => x.Plugin.Type)
                .ThenBy(x => x.Plugin.FullPath);
            children = new ObservableCollection<TreeViewModel>(sorted);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyUpdate(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public TreeViewModel FindPluginByFullPath(string fullPath)
        {
            return RecursiveFindPluginByFullPath(root, fullPath); 
        }

        private static TreeViewModel RecursiveFindPluginByFullPath(TreeViewModel cur, string fullPath)
        {
            if (cur.Plugin != null)
            {
                if (fullPath.Equals(cur.Plugin.FullPath, StringComparison.OrdinalIgnoreCase))
                    return cur;
            }

            if (0 < cur.Children.Count)
            {
                foreach (TreeViewModel next in cur.Children)
                {
                    TreeViewModel found = RecursiveFindPluginByFullPath(next, fullPath);
                    if (found != null)
                        return found;
                }
            }

            // Not found in this path
            return null;
        }

        private List<LogInfo> DisablePlugins(TreeViewModel root, Plugin p)
        {
            if (root == null || p == null)
                return new List<LogInfo>();

            string[] paths = Plugin.GetDisablePluginPaths(p, out List<LogInfo> errorLogs);
            if (paths == null)
                return new List<LogInfo>();

            foreach (string path in paths)
            {
                int exist = p.Project.AllPlugins.Count(x => x.FullPath.Equals(path, StringComparison.OrdinalIgnoreCase));
                if (exist == 1)
                {
                    Ini.SetKey(path, "Main", "Selected", "False");
                    TreeViewModel found = FindPluginByFullPath(path);
                    if (found != null)
                    {
                        if (p.Type != PluginType.Directory && p.Mandatory == false && p.Selected != SelectedState.None)
                            found.Checked = false;
                    }
                }
            }

            return errorLogs;
        }
    }
    #endregion
}
