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

    Additional permission under GNU GPL version 3 section 7

    If you modify this program, or any covered work, by linking
    or combining it with external libraries, containing parts
    covered by the terms of various license, the licensors of
    this program grant you additional permission to convey the
    resulting work. An external library is a library which is
    not derived from or based on this program. 
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
        internal const int ScriptAuthorLenLimit = 35;
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
        private ScriptCache scriptCache;

        const int MaxDpiScale = 4;
        private int allScriptCount = 0;
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
                if (args[i].Equals("/basedir", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length)
                    {
                        argBaseDir = System.IO.Path.GetFullPath(args[i + 1]);
                        if (Directory.Exists(argBaseDir) == false)
                        {
                            MessageBox.Show($"Directory [{argBaseDir}] does not exist", "Invalid BaseDir", MessageBoxButton.OK, MessageBoxImage.Error);
                            Environment.Exit(1); // Force Shutdown
                        }
                        Environment.CurrentDirectory = argBaseDir;
                    }
                    else
                    {
                        Console.WriteLine("\'/basedir\' must be used with path\r\n");
                    }
                }
                else if (args[i].Equals("/?", StringComparison.OrdinalIgnoreCase)
                    || args[i].Equals("/help", StringComparison.OrdinalIgnoreCase)
                    || args[i].Equals("/h", StringComparison.OrdinalIgnoreCase))
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

            // If script cache is enabled, generate cache after 5 seconds
            if (setting.Script_EnableCache)
            {
                string cacheDBFile = System.IO.Path.Combine(dbDir, "PEBakeryCache.db");
                try
                {
                    this.scriptCache = new ScriptCache(cacheDBFile);
                    logger.System_Write(new LogInfo(LogState.Info, $"ScriptCache enabled, {scriptCache.Table<DB_ScriptCache>().Count()} cached scripts found"));
                }
                catch (SQLiteException e)
                { // Update failure
                    string msg = $"SQLite Error : {e.Message}\r\n\r\nCache database is corrupted.\r\nPlease delete PEBakeryCache.db and restart.";
                    MessageBox.Show(msg, "SQLite Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                    Application.Current.Shutdown(1);
                }

                this.setting.CacheDB = scriptCache;
            }
            else
            {
                logger.System_Write(new LogInfo(LogState.Info, $"ScriptCache disabled"));
            }

            StartLoadWorker();
        }
        #endregion

        #region Background Workers
        public AutoResetEvent StartLoadWorker(bool quiet = false)
        {
            AutoResetEvent resetEvent = new AutoResetEvent(false);
            Stopwatch watch = Stopwatch.StartNew();

            /*
            // Load PEBakery Logo
            Image image = new Image()
            { 
                UseLayoutRounding = true,
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.DownOnly,
                Source = Application.Current.TryFindResource("DonutSvgImage") as DrawingImage,
            };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            ScriptLogo.Content = image;
            */

            // Load CommentProcessing Icon
            ScriptLogo.Content = ImageHelper.GetMaterialIcon(PackIconMaterialKind.CommentProcessing, 10);

            // Prepare PEBakery Loading Information
            if (quiet == false)
            {
                Model.ScriptTitleText = "Welcome to PEBakery!";
                Model.ScriptDescriptionText = "PEBakery loading...";
            }
            logger.System_Write(new LogInfo(LogState.Info, $@"Loading from [{baseDir}]"));
            MainCanvas.Children.Clear();
            (MainTreeView.DataContext as TreeViewModel).Children.Clear();

            int stage2LinksCount = 0;
            int loadedScriptCount = 0;
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
                if (setting.Script_EnableCache && scriptCache != null) // Use ScriptCache
                {
                    if (scriptCache.IsGlobalCacheValid(baseDir))
                        projects = new ProjectCollection(baseDir, scriptCache);
                    else // Cache is invalid
                        projects = new ProjectCollection(baseDir, null);
                }
                else  // Do not use ScriptCache
                {
                    projects = new ProjectCollection(baseDir, null);
                }

                allScriptCount = projects.PrepareLoad(out stage2LinksCount);
                Dispatcher.Invoke(() => { Model.BottomProgressBarMaximum = allScriptCount + stage2LinksCount; });

                // Let's load scripts parallelly
                List<LogInfo> errorLogs = projects.Load(worker);
                Logger.System_Write(errorLogs);
                setting.UpdateProjectList();

                if (0 < projects.ProjectNames.Count)
                { // Load Success
                    // Populate TreeView
                    Dispatcher.Invoke(() =>
                    {
                        foreach (Project project in projects.Projects)
                            ScriptListToTreeViewModel(project, project.VisibleScripts, Model.MainTree);

                        int pIdx = setting.Project_DefaultIndex;
                        curMainTree = Model.MainTree.Children[pIdx];
                        curMainTree.IsExpanded = true;
                        if (projects[pIdx] != null)
                            DrawScript(projects[pIdx].MainScript);
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
                Interlocked.Increment(ref loadedScriptCount);
                Model.BottomProgressBarValue = loadedScriptCount;
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
                    msg = $"Stage {stage} ({loadedScriptCount} / {allScriptCount}) \r\n{msg}";
                else
                    msg = $"Stage {stage} ({stage2LoadedCount} / {stage2LinksCount}) \r\n{msg}";

                Model.ScriptDescriptionText = $"PEBakery loading...\r\n{msg}";               
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
                    if (setting.Script_EnableCache)
                    {
                        double cachePercent = (double)(stage1CachedCount + stage2CachedCount) * 100 / (allScriptCount + stage2LinksCount);
                        msg = $"{allScriptCount} scripts loaded ({t:0.#}s) - {cachePercent:0.#}% cached";
                        Model.StatusBarText = msg;
                    }
                    else
                    {
                        msg = $"{allScriptCount} scripts loaded ({t:0.#}s)";
                        Model.StatusBarText = msg;
                    }
                    if (quiet == false)
                        Model.WorkInProgress = false;
                    Model.SwitchStatusProgressBar = true; // Show Status Bar

                    logger.System_Write(new LogInfo(LogState.Info, msg));
                    logger.System_Write(Logger.LogSeperator);

                    // If script cache is enabled, generate cache.
                    if (setting.Script_EnableCache)
                        StartCacheWorker();
                }
                else
                {
                    Model.ScriptTitleText = "Unable to find projects.";
                    Model.ScriptDescriptionText = $"Please populate project in [{projects.ProjectRoot}]";

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
            if (ScriptCache.dbLock == 0)
            {
                Interlocked.Increment(ref ScriptCache.dbLock);
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
                        scriptCache.CacheScripts(projects, baseDir, worker);
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

                        double cachePercent = (double)updatedCount * 100 / allScriptCount;

                        double t = watch.Elapsed.TotalMilliseconds / 1000.0;
                        string msg = $"{allScriptCount} scripts cached ({t:0.###}s), {cachePercent:0.#}% updated";
                        logger.System_Write(new LogInfo(LogState.Info, msg));
                        logger.System_Write(Logger.LogSeperator);

                        Model.WorkInProgress = false;
                    };
                    cacheWorker.RunWorkerAsync();
                }
                finally
                {
                    Interlocked.Decrement(ref ScriptCache.dbLock);
                }
            }
        }
        
        public AutoResetEvent StartReloadScriptWorker()
        {
            AutoResetEvent resetEvent = new AutoResetEvent(false);

            if (curMainTree == null || curMainTree.Script == null)
                return null;

            if (refreshWorker.IsBusy)
                return null;

            Stopwatch watch = new Stopwatch();

            Model.WorkInProgress = true;
            refreshWorker = new BackgroundWorker();
            refreshWorker.DoWork += (object sender, DoWorkEventArgs e) =>
            {
                watch.Start();
                e.Result = curMainTree.Script.Project.RefreshScript(curMainTree.Script);
            };
            refreshWorker.RunWorkerCompleted += (object sender, RunWorkerCompletedEventArgs e) =>
            {
                if (e.Result is Script p)
                {
                    curMainTree.Script = p;
                    curMainTree.ParentCheckedPropagation();
                    UpdateTreeViewIcon(curMainTree);

                    DrawScript(curMainTree.Script);                   
                }

                Model.WorkInProgress = false;
                watch.Stop();
                double sec = watch.Elapsed.TotalSeconds;
                if ((Script)e.Result == null)
                    Model.StatusBarText = $"{Path.GetFileName(curMainTree.Script.ShortPath)} reload failed. ({sec:0.000}sec)";
                else
                    Model.StatusBarText = $"{Path.GetFileName(curMainTree.Script.ShortPath)} reloaded. ({sec:0.000}sec)";

                resetEvent.Set();
            };
            refreshWorker.RunWorkerAsync();

            return resetEvent;
        }

        private void StartSyntaxCheckWorker(bool quiet)
        {
            if (curMainTree == null || curMainTree.Script == null)
                return;

            if (syntaxCheckWorker.IsBusy)
                return;

            if (quiet == false)
                Model.WorkInProgress = true;

            Script p = curMainTree.Script;

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
                    Model.ScriptCheckButtonColor = new SolidColorBrush(Colors.LightGreen);                   

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
                    Model.ScriptCheckButtonColor = new SolidColorBrush(Colors.Orange);

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

        #region DrawScript
        public void DrawScript(Script p)
        {
            Stopwatch watch = new Stopwatch();
            DrawScriptLogo(p);

            Model.ScriptCheckButtonColor = new SolidColorBrush(Colors.LightGray);

            MainCanvas.Children.Clear();
            if (p.Type == ScriptType.Directory)
            {
                Model.ScriptTitleText = StringEscaper.Unescape(p.Title);
                Model.ScriptDescriptionText = string.Empty;
                Model.ScriptVersionText = string.Empty;
                Model.ScriptAuthorText = string.Empty;
            }
            else
            {
                Model.ScriptTitleText = StringEscaper.Unescape(p.Title);
                Model.ScriptDescriptionText = StringEscaper.Unescape(p.Description);
                Model.ScriptVersionText = "v" + p.Version;
                if (ScriptAuthorLenLimit < p.Author.Length)
                    Model.ScriptAuthorText = p.Author.Substring(0, ScriptAuthorLenLimit) + "...";
                else
                    Model.ScriptAuthorText = p.Author;

                double scaleFactor = setting.Interface_ScaleFactor / 100;
                ScaleTransform scale = new ScaleTransform(scaleFactor, scaleFactor);
                UIRenderer render = new UIRenderer(MainCanvas, this, p, logger, scaleFactor);
                MainCanvas.LayoutTransform = scale;
                render.Render();
                
                if (setting.Script_AutoSyntaxCheck)
                    StartSyntaxCheckWorker(true);
            }
            Model.OnPropertyUpdate("MainCanvas");
        }

        public void DrawScriptLogo(Script p)
        {
            double size = ScriptLogo.ActualWidth * MaxDpiScale;
            if (p.Type == ScriptType.Directory)
            {
                if (p.IsDirLink)
                    ScriptLogo.Content = ImageHelper.GetMaterialIcon(PackIconMaterialKind.FolderMove, 0);
                else
                    ScriptLogo.Content = ImageHelper.GetMaterialIcon(PackIconMaterialKind.Folder, 0);
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

                    ScriptLogo.Content = grid;
                }
                catch
                { // No logo file - use default
                    if (p.Type == ScriptType.Script)
                    {
                        if (p.IsDirLink)
                            ScriptLogo.Content = ImageHelper.GetMaterialIcon(PackIconMaterialKind.FileSend, 5);
                        else
                            ScriptLogo.Content = ImageHelper.GetMaterialIcon(PackIconMaterialKind.FileDocument, 0);
                    }
                    else if (p.Type == ScriptType.Link)
                    {
                        ScriptLogo.Content = ImageHelper.GetMaterialIcon(PackIconMaterialKind.FileSend, 5);
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

                if (curMainTree == null || curMainTree.Script == null || Model.WorkInProgress)
                {
                    Interlocked.Decrement(ref Engine.WorkingLock);
                    return;
                }

                // Determine current project
                Project project = curMainTree.Script.Project;

                Model.BuildTree.Children.Clear();
                ScriptListToTreeViewModel(project, project.ActiveScripts, Model.BuildTree);
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
                DrawScript(curMainTree.Script);

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
                bool old_Script_EnableCache = setting.Script_EnableCache;

                SettingWindow dialog = new SettingWindow(setting);
                bool? result = dialog.ShowDialog();
                if (result == true)
                {
                    // Scale Factor
                    double newScaleFactor = setting.Interface_ScaleFactor;
                    if (double.Epsilon < Math.Abs(newScaleFactor - old_Interface_ScaleFactor)) // Not Equal
                        DrawScript(curMainTree.Script);

                    // Script
                    if (old_Script_EnableCache == false && setting.Script_EnableCache)
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

        #region Script Buttons
        private async void ScriptRunButton_Click(object sender, RoutedEventArgs e)
        {
            if (curMainTree == null || curMainTree.Script == null)
                return;

            if (Model.WorkInProgress)
                return;

            Script p = curMainTree.Script;
            if (p.Sections.ContainsKey("Process"))
            {
                if (Engine.WorkingLock == 0)  // Start Build
                {
                    Interlocked.Increment(ref Engine.WorkingLock);

                    // Populate BuildTree
                    Model.BuildTree.Children.Clear();
                    PopulateOneTreeView(p, Model.BuildTree, Model.BuildTree);
                    curBuildTree = null;

                    EngineState s = new EngineState(p.Project, logger, Model, EngineMode.RunMainAndOne, p);
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
                    DrawScript(curMainTree.Script);

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

        private void ScriptEditButton_Click(object sender, RoutedEventArgs e)
        {
            if (curMainTree == null || curMainTree.Script == null)
                return;

            if (Model.WorkInProgress)
                return;

            OpenTextFile(curMainTree.Script.FullPath, false);
        }

        private void ScriptRefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (curMainTree == null || curMainTree.Script == null)
                return;

            if (Model.WorkInProgress)
                return;

            StartReloadScriptWorker();
        }

        private void ScriptCheckButton_Click(object sender, RoutedEventArgs e)
        {
            if (curMainTree == null || curMainTree.Script == null)
                return;

            if (Model.WorkInProgress)
                return;

            StartSyntaxCheckWorker(false);
        }
        #endregion

        #region TreeView Methods
        private void ScriptListToTreeViewModel(Project project, List<Script> pList, TreeViewModel treeRoot, TreeViewModel projectRoot = null)
        {
            Dictionary<string, TreeViewModel> dirDict = new Dictionary<string, TreeViewModel>(StringComparer.OrdinalIgnoreCase);

            // Populate MainScript
            if (projectRoot == null)
                projectRoot = PopulateOneTreeView(project.MainScript, treeRoot, treeRoot);

            foreach (Script p in pList)
            {
                Debug.Assert(p != null);

                if (p.Equals(project.MainScript))
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
                        Script dirScript = new Script(ScriptType.Directory, fullPath, project, project.ProjectRoot, p.Level, false, false, p.IsDirLink);
                        treeParent = PopulateOneTreeView(dirScript, treeRoot, treeParent);
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

        public void UpdateScriptTree(Project project, bool redrawScript)
        {
            TreeViewModel projectRoot = Model.MainTree.Children.FirstOrDefault(x => x.Script.Project.Equals(project));
            if (projectRoot != null) // Remove existing project tree
                projectRoot.Children.Clear();

            ScriptListToTreeViewModel(project, project.VisibleScripts, Model.MainTree, projectRoot);

            if (redrawScript)
            {
                curMainTree = projectRoot;
                curMainTree.IsExpanded = true;
                DrawScript(projectRoot.Script);
            }
        }

        public TreeViewModel PopulateOneTreeView(Script p, TreeViewModel treeRoot, TreeViewModel treeParent)
        {
            TreeViewModel item = new TreeViewModel(treeRoot, treeParent)
            {
                Script = p
            };
            treeParent.Children.Add(item);
            UpdateTreeViewIcon(item);

            return item;
        }

        TreeViewModel UpdateTreeViewIcon(TreeViewModel item)
        {
            Script p = item.Script;

            if (p.Type == ScriptType.Directory)
            {
                if (p.IsDirLink)
                    item.Icon = ImageHelper.GetMaterialIcon(PackIconMaterialKind.FolderMove, 0);
                else
                    item.Icon = ImageHelper.GetMaterialIcon(PackIconMaterialKind.Folder, 0);
            }
            else if (p.Type == ScriptType.Script)
            {
                if (p.IsMainScript)
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
            else if (p.Type == ScriptType.Link)
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
                    DrawScript(item.Script);
                    watch.Stop();
                    double msec = watch.Elapsed.TotalMilliseconds;
                    string filename = Path.GetFileName(curMainTree.Script.ShortPath);
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

            if (scriptCache != null)
                scriptCache.WaitClose();
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

        private string scriptTitleText = "Welcome to PEBakery!";
        public string ScriptTitleText
        {
            get => scriptTitleText;
            set
            {
                scriptTitleText = value;
                OnPropertyUpdate("ScriptTitleText");
            }
        }

        private string scriptAuthorText = string.Empty;
        public string ScriptAuthorText
        {
            get => scriptAuthorText;
            set
            {
                scriptAuthorText = value;
                OnPropertyUpdate("ScriptAuthorText");
            }
        }

        private string scriptVersionText = Properties.Resources.StringVersion;
        public string ScriptVersionText
        {
            get => scriptVersionText;
            set
            {
                scriptVersionText = value;
                OnPropertyUpdate("ScriptVersionText");
            }
        }

        private string scriptDescriptionText = "PEBakery is now loading, please wait...";
        public string ScriptDescriptionText
        {
            get => scriptDescriptionText;
            set
            {
                scriptDescriptionText = value;
                OnPropertyUpdate("ScriptDescriptionText");
            }
        }

        private Brush scriptCheckButtonColor = new SolidColorBrush(Colors.LightGray);
        public Brush ScriptCheckButtonColor
        {
            get => scriptCheckButtonColor;
            set
            {
                scriptCheckButtonColor = value;
                OnPropertyUpdate("ScriptCheckButtonColor");
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

                    BuildScriptProgressBarValue = 0;
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
        private double buildScriptProgressBarMax = 100;
        public double BuildScriptProgressBarMax
        {
            get => buildScriptProgressBarMax;
            set
            {
                buildScriptProgressBarMax = value;
                OnPropertyUpdate("BuildScriptProgressBarMax");
            }
        }

        private double buildScriptProgressBarValue = 0;
        public double BuildScriptProgressBarValue
        {
            get => buildScriptProgressBarValue;
            set
            {
                buildScriptProgressBarValue = value;
                OnPropertyUpdate("BuildScriptProgressBarValue");
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
                switch (script.Selected)
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
                    if (script.Mandatory == false && script.Selected != SelectedState.None)
                    {
                        if (value)
                        {
                            script.Selected = SelectedState.True;

                            try
                            {
                                // Run 'Disable' directive
                                List<LogInfo> errorLogs = DisableScripts(root, script);
                                w.Logger.System_Write(errorLogs);
                            }
                            catch (Exception e)
                            {
                                w.Logger.System_Write(new LogInfo(LogState.Error, e));
                            }
                        }
                        else
                        {
                            script.Selected = SelectedState.False;
                        }

                        if (script.IsMainScript == false)
                        {
                            if (0 < this.Children.Count)
                            { // Set child scripts, too -> Top-down propagation
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

            if (script.Mandatory == false && script.Selected != SelectedState.None)
            {
                if (value)
                    script.Selected = SelectedState.True;
                else
                    script.Selected = SelectedState.False;
            }

            OnPropertyUpdate("Checked");
            ParentCheckedPropagation();
        }

        public Visibility CheckBoxVisible
        {
            get
            {
                if (script.Selected == SelectedState.None)
                    return Visibility.Collapsed;
                else
                    return Visibility.Visible;
            }
        }

        public string Text { get => script.Title; }

        private Script script;
        public Script Script
        {
            get => script;
            set
            {
                script = value;
                OnPropertyUpdate("Script");
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
                .OrderBy(x => x.Script.Level)
                .ThenBy(x => x.Script.Type)
                .ThenBy(x => x.Script.FullPath);
            children = new ObservableCollection<TreeViewModel>(sorted);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyUpdate(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public TreeViewModel FindScriptByFullPath(string fullPath)
        {
            return RecursiveFindScriptByFullPath(root, fullPath); 
        }

        private static TreeViewModel RecursiveFindScriptByFullPath(TreeViewModel cur, string fullPath)
        {
            if (cur.Script != null)
            {
                if (fullPath.Equals(cur.Script.FullPath, StringComparison.OrdinalIgnoreCase))
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

        private List<LogInfo> DisableScripts(TreeViewModel root, Script p)
        {
            if (root == null || p == null)
                return new List<LogInfo>();

            string[] paths = Script.GetDisableScriptPaths(p, out List<LogInfo> errorLogs);
            if (paths == null)
                return new List<LogInfo>();

            foreach (string path in paths)
            {
                int exist = p.Project.AllScripts.Count(x => x.FullPath.Equals(path, StringComparison.OrdinalIgnoreCase));
                if (exist == 1)
                {
                    Ini.SetKey(path, "Main", "Selected", "False");
                    TreeViewModel found = FindScriptByFullPath(path);
                    if (found != null)
                    {
                        if (p.Type != ScriptType.Directory && p.Mandatory == false && p.Selected != SelectedState.None)
                            found.Checked = false;
                    }
                }
            }

            return errorLogs;
        }
    }
    #endregion
}
