/*
    Copyright (C) 2018 Hajin Jang
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
using PEBakery.Helper;
using PEBakery.Ini;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
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

namespace PEBakery.Core.ViewModels
{
    #region MainViewModel
    public class MainViewModel : ViewModelBase
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

        #region Constants
        internal const int ScriptAuthorLenLimit = 35;
        #endregion

        #region UIRenderer Properties
        private UIRenderer _renderer;
        #endregion

        #region TreeItem Properties
        public ProjectTreeItemModel CurMainTree { get; set; }
        public ProjectTreeItemModel CurBuildTree { get; set; }
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

                CommandManager.InvalidateRequerySuggested();
            }
        }

        public const string DefaultTitleBar = "PEBakery";
        private string _titleBar = DefaultTitleBar;
        public string TitleBar
        {
            get => _titleBar;
            set
            {
                _titleBar = value;
                OnPropertyUpdate(nameof(TitleBar));
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
        private PackIconMaterialKind? _scriptLogoIcon = PackIconMaterialKind.None;
        public PackIconMaterialKind? ScriptLogoIcon
        {
            get => _scriptLogoIcon;
            set
            {
                _scriptLogoIcon = value;
                _scriptLogoImage = null;
                _scriptLogoSvg = null;
                OnPropertyUpdate(nameof(ScriptLogoIcon));
                OnPropertyUpdate(nameof(ScriptLogoImage));
                OnPropertyUpdate(nameof(ScriptLogoSvg));
            }
        }

        private ImageSource _scriptLogoImage;
        public ImageSource ScriptLogoImage
        {
            get => _scriptLogoImage;
            set
            {
                _scriptLogoIcon = null;
                _scriptLogoImage = value;
                _scriptLogoSvg = null;
                OnPropertyUpdate(nameof(ScriptLogoIcon));
                OnPropertyUpdate(nameof(ScriptLogoImage));
                OnPropertyUpdate(nameof(ScriptLogoSvg));
            }
        }

        private DrawingBrush _scriptLogoSvg;
        public DrawingBrush ScriptLogoSvg
        {
            get => _scriptLogoSvg;
            set
            {
                _scriptLogoIcon = null;
                _scriptLogoImage = null;
                _scriptLogoSvg = value;
                OnPropertyUpdate(nameof(ScriptLogoIcon));
                OnPropertyUpdate(nameof(ScriptLogoImage));
                OnPropertyUpdate(nameof(ScriptLogoSvg));
            }
        }

        private double _scriptLogoSvgWidth;
        public double ScriptLogoSvgWidth
        {
            get => _scriptLogoSvgWidth;
            set
            {
                _scriptLogoSvgWidth = value;
                OnPropertyUpdate(nameof(ScriptLogoSvgWidth));
            }
        }

        private double _scriptLogoSvgHeight;
        public double ScriptLogoSvgHeight
        {
            get => _scriptLogoSvgHeight;
            set
            {
                _scriptLogoSvgHeight = value;
                OnPropertyUpdate(nameof(ScriptLogoSvgHeight));
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
                    TaskBarProgressState = TaskbarItemProgressState.None;

                    NormalInterfaceVisibility = Visibility.Visible;
                    BuildInterfaceVisibility = Visibility.Collapsed;
                }
                else
                { // To Build View
                    BuildPosition = string.Empty;
                    BuildEchoMessage = string.Empty;

                    BuildScriptProgressBarValue = 0;
                    BuildFullProgressBarValue = 0;
                    TaskBarProgressState = TaskbarItemProgressState.Normal;

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

        public bool DisplayShellExecuteConOut = true;
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

        private FontHelper.FontInfo _monospacedFont;
        public FontHelper.FontInfo MonospacedFont
        {
            get => _monospacedFont;
            set
            {
                _monospacedFont = value;
                OnPropertyUpdate(nameof(MonospacedFont));
                OnPropertyUpdate(nameof(MonospacedFontFamily));
                OnPropertyUpdate(nameof(MonospacedFontWeight));
                OnPropertyUpdate(nameof(MonospacedFontSize));
            }
        }
        public FontFamily MonospacedFontFamily => _monospacedFont.FontFamily;
        public FontWeight MonospacedFontWeight => _monospacedFont.FontWeight;
        public double MonospacedFontSize => _monospacedFont.DeviceIndependentPixelSize;

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

        // Taskbar Progress State
        //
        // None - Hidden
        // Inderterminate - Pulsing green indicator
        // Normal - Green
        // Error - Red
        // Paused - Yellow
        private TaskbarItemProgressState _taskbarProgressState;
        public TaskbarItemProgressState TaskBarProgressState
        {
            get => _taskbarProgressState;
            set
            {
                _taskbarProgressState = value;
                OnPropertyUpdate(nameof(TaskBarProgressState));
            }
        }
        #endregion

        #region Build Interface Methods
        public void SetBuildCommandProgress(string title, double max = 100)
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

        #region Background Tasks
        public int ProjectsLoading = 0;
        public int ScriptRefreshing = 0;
        public int SyntaxChecking = 0;

        public Task StartLoadingProjects(bool quiet = false)
        {
            if (ProjectsLoading != 0)
                return Task.CompletedTask;

            // Clear MainTreeItems
            MainTreeItems.Clear();

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
                BottomProgressBarValue = loadedScriptCount;

                int stage = 0;
                string msg = string.Empty;
                switch (x.Type)
                {
                    case Project.LoadReport.FindingScript:
                        ScriptDescriptionText = "Finding script files";
                        return;
                    case Project.LoadReport.LoadingCache:
                        ScriptDescriptionText = "Loading script cache";
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
                    ScriptDescriptionText = msg;
                }
            });

            return Task.Run(() =>
            {
                Interlocked.Increment(ref ProjectsLoading);
                if (!quiet)
                    WorkInProgress = true;
                SwitchStatusProgressBar = false; // Show Progress Bar
                try
                {
                    Stopwatch watch = Stopwatch.StartNew();

                    // Prepare PEBakery Loading Information
                    if (!quiet)
                    {
                        ScriptTitleText = "PEBakery loading...";
                        ScriptDescriptionText = string.Empty;
                    }
                    Global.Logger.SystemWrite(new LogInfo(LogState.Info, $"Loading from [{Global.BaseDir}]"));

                    // Load CommentProcessing Icon, Clear interfaces
                    ScriptLogoIcon = PackIconMaterialKind.CommentProcessing;
                    MainTreeItems.Clear();
                    BuildTreeItems.Clear();
                    Application.Current.Dispatcher.Invoke(ClearScriptInterface);

                    BottomProgressBarMinimum = 0;
                    BottomProgressBarMaximum = 100;
                    BottomProgressBarValue = 0;

                    // Init ProjectCollection
                    if (Global.Setting.Script.EnableCache && Global.ScriptCache != null) // Use ScriptCache
                    {
                        if (Global.ScriptCache.CheckCacheRevision(Global.BaseDir))
                            Global.Projects = new ProjectCollection(Global.BaseDir, Global.ScriptCache);
                        else // Cache is invalid
                        {
                            Global.ScriptCache.ClearTable(new ScriptCache.ClearTableOptions
                            {
                                CacheInfo = false,
                                ScriptCache = true,
                            });
                            Global.Projects = new ProjectCollection(Global.BaseDir, null);
                        }
                    }
                    else // Do not use ScriptCache
                    {
                        Global.Projects = new ProjectCollection(Global.BaseDir, null);
                    }

                    // Prepare by getting script paths
                    progress.Report((Project.LoadReport.FindingScript, null));
                    (totalScriptCount, stage2LinkCount) = Global.Projects.PrepareLoad();
                    ifaceUpdateFreq = totalScriptCount / 64 + 1;
                    BottomProgressBarMaximum = totalScriptCount + stage2LinkCount;

                    // Load projects in parallel
                    List<LogInfo> errorLogs = Global.Projects.Load(progress);
                    Global.Logger.SystemWrite(errorLogs);
                    // Global.Setting.UpdateProjectList();

                    if (0 < Global.Projects.ProjectNames.Count)
                    { // Load success
                        // Populate TreeView
                        Application.Current?.Dispatcher.Invoke(() =>
                        {
                            foreach (Project project in Global.Projects.ProjectList)
                            {
                                ProjectTreeItemModel projectRoot = PopulateOneTreeItem(project.MainScript, null, null);
                                ScriptListToTreeViewModel(project, project.VisibleScripts, true, projectRoot);
                                MainTreeItems.Add(projectRoot);
                            }

                            CurMainTree = null;
                            string defaultProjectName = Global.Setting.Project.DefaultProject;
                            for (int i = 0; i < MainTreeItems.Count; i++)
                            {
                                ProjectTreeItemModel itemModel = MainTreeItems[i];
                                string itemProjectName = itemModel.Script.Project.ProjectName;
                                if (itemProjectName.Equals(defaultProjectName, StringComparison.OrdinalIgnoreCase))
                                {
                                    CurMainTree = itemModel;
                                    CurMainTree.IsExpanded = true;
                                    if (Global.Projects[i] != null)
                                        DisplayScript(Global.Projects[i].MainScript);
                                }
                            }
                        });

                        Global.Logger.SystemWrite(new LogInfo(LogState.Info, $"Projects [{string.Join(", ", Global.Projects.ProjectList.Select(x => x.ProjectName))}] loaded"));

                        watch.Stop();
                        double t = watch.Elapsed.TotalMilliseconds / 1000.0;
                        string msg;
                        if (Global.Setting.Script.EnableCache)
                        {
                            double cachePercent = (double)(stage1CachedCount + stage2CachedCount) * 100 / (totalScriptCount + stage2LinkCount);
                            msg = $"{totalScriptCount} scripts loaded ({t:0.#}s) - {cachePercent:0.#}% cached";
                            StatusBarText = msg;
                        }
                        else
                        {
                            msg = $"{totalScriptCount} scripts loaded ({t:0.#}s)";
                            StatusBarText = msg;
                        }

                        Global.Logger.SystemWrite(new LogInfo(LogState.Info, msg));
                        Global.Logger.SystemWrite(Logger.LogSeperator);

                        // If script cache is enabled, update cache.
                        if (Global.Setting.Script.EnableCache)
                            StartScriptCaching();
                    }
                    else
                    { // Load failure
                        ScriptTitleText = "Unable to find project.";
                        ScriptDescriptionText = $"Please provide project in [{Global.Projects.ProjectRoot}]";
                        StatusBarText = "Unable to find project.";
                    }
                }
                finally
                {
                    if (!quiet)
                        WorkInProgress = false;
                    SwitchStatusProgressBar = true; // Show Status Bar
                    Interlocked.Decrement(ref ProjectsLoading);

                    // Enable Button/Context Menu Commands
                    Application.Current?.Dispatcher.Invoke(CommandManager.InvalidateRequerySuggested);
                }
            });
        }

        public Task StartScriptCaching()
        {
            if (ScriptCache.DbLock != 0)
                return Task.CompletedTask;

            return Task.Run(() =>
            {
                Interlocked.Increment(ref ScriptCache.DbLock);
                WorkInProgress = true;
                try
                {
                    Stopwatch watch = Stopwatch.StartNew();
                    (_, int updatedCount) = Global.ScriptCache.CacheScripts(Global.Projects, Global.BaseDir);
                    watch.Stop();

                    double t = watch.Elapsed.TotalMilliseconds / 1000.0;
                    Global.Logger.SystemWrite(new LogInfo(LogState.Info, $"{updatedCount} script cache updated ({t:0.###}s)"));
                    Global.Logger.SystemWrite(Logger.LogSeperator);
                }
                finally
                {
                    WorkInProgress = false;
                    Interlocked.Decrement(ref ScriptCache.DbLock);

                    // Enable Button/Context Menu Commands
                    Application.Current?.Dispatcher.Invoke(CommandManager.InvalidateRequerySuggested);
                }
            });
        }

        public Task StartSyntaxCheck(bool quiet)
        {
            if (CurMainTree?.Script == null)
                return Task.CompletedTask;
            if (SyntaxChecking != 0)
                return Task.CompletedTask;

            Script sc = CurMainTree.Script;
            if (sc.Type == ScriptType.Directory)
                return Task.CompletedTask;

            if (!quiet)
                WorkInProgress = true;

            return Task.Run(() =>
            {
                Interlocked.Increment(ref SyntaxChecking);
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

                    ScriptCheckResult = result;
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
                                    using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                                        w.Write(b.ToString());

                                    OpenTextFile(tempFile);
                                }
                                break;
                        }

                        WorkInProgress = false;
                    }
                }
                finally
                {
                    Interlocked.Decrement(ref SyntaxChecking);

                    // Enable Button/Context Menu Commands
                    Application.Current?.Dispatcher.Invoke(CommandManager.InvalidateRequerySuggested);
                }
            });
        }

        public Task StartRefreshScript()
        {
            if (CurMainTree?.Script == null)
                return Task.CompletedTask;
            if (CurMainTree.Script.Type == ScriptType.Directory)
                return Task.CompletedTask;
            if (ScriptRefreshing != 0)
                return Task.CompletedTask;

            ProjectTreeItemModel node = CurMainTree;
            return Task.Run(() =>
            {
                Interlocked.Increment(ref ScriptRefreshing);
                WorkInProgress = true;
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
                        StatusBarText = $"{Path.GetFileName(node.Script.TreePath)} reloaded. ({t:0.000}s)";
                    }
                    else
                    {
                        StatusBarText = $"{Path.GetFileName(node.Script.TreePath)} reload failed. ({t:0.000}s)";
                    }
                }
                finally
                {
                    WorkInProgress = false;
                    Interlocked.Decrement(ref ScriptRefreshing);

                    // Enable Button/Context Menu Commands
                    Application.Current?.Dispatcher.Invoke(CommandManager.InvalidateRequerySuggested);
                }
            });
        }

        private void PostRefreshScript(ProjectTreeItemModel node, Script sc)
        {
            node.Script = sc;
            node.ParentCheckedPropagation();
            UpdateTreeViewIcon(node);
            DisplayScript(node.Script);
        }
        #endregion

        #region DisplayScript Methods
        public void DisplayScript(Script sc)
        {
            DisplayScriptLogo(sc);
            DisplayScriptTexts(sc, null);

            ScriptCheckResult = CodeValidator.Result.Unknown;
            if (sc.Type == ScriptType.Directory)
            {
                ClearScriptInterface();
            }
            else
            {
                DisplayScriptInterface(sc);

                // Run CodeValidator
                // Do not use await, let it run in background
                if (Global.Setting.Script.AutoSyntaxCheck)
                    StartSyntaxCheck(true);
            }

            IsTreeEntryFile = sc.Type != ScriptType.Directory;
            OnPropertyUpdate(nameof(MainCanvas));
        }

        public void DisplayScriptInterface(Script sc)
        {
            // Current UIRenderer can only run in interface thread.
            // Guard instance owner exception using Application.Current.Dispatcher.Invoke()
            Application.Current?.Dispatcher.Invoke(() =>
            {
                // Set scale factor
                double scaleFactor = Global.Setting.Interface.ScaleFactor / 100.0;
                ScaleTransform scale;
                if (scaleFactor - 1 < double.Epsilon)
                    scale = new ScaleTransform(1, 1);
                else
                    scale = new ScaleTransform(scaleFactor, scaleFactor);
                MainCanvas.LayoutTransform = scale;

                // Render script interface
                ClearScriptInterface();

                _renderer = new UIRenderer(MainCanvas, Application.Current?.MainWindow, sc, scaleFactor, true, sc.Project.Compat.IgnoreWidthOfWebLabel);
                _renderer.Render();
            });
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
                    ScriptLogoIcon = PackIconMaterialKind.FolderMove;
                else
                    ScriptLogoIcon = PackIconMaterialKind.Folder;
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
                                ScriptLogoSvg = new DrawingBrush { Drawing = svgDrawing };
                                ScriptLogoSvgWidth = width;
                                ScriptLogoSvgHeight = height;
                                break;
                            default:
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    // ReSharper disable once AccessToDisposedClosure
                                    ScriptLogoImage = ImageHelper.ImageToBitmapImage(ms);
                                });
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

                    ScriptLogoIcon = iconKind;
                }
            }
        }

        /// <summary>
        /// Display script title, description, version and author
        /// </summary>
        /// <param name="sc">Source script to read information</param>
        /// <param name="s">Set to non-null to notify running in build mode</param>
        public void DisplayScriptTexts(Script sc, EngineState s)
        {
            if (sc.Type == ScriptType.Directory && s == null)
            { // In build mode, there are no directory scripts
                ScriptTitleText = StringEscaper.Unescape(sc.Title);
                ScriptDescriptionText = string.Empty;
                ScriptVersionText = string.Empty;
                ScriptAuthorText = string.Empty;
            }
            else
            {
                // Script Title
                if (s != null && s.RunMode == EngineMode.RunAll)
                    ScriptTitleText = $"({s.CurrentScriptIdx + 1}/{s.Scripts.Count}) {StringEscaper.Unescape(sc.Title)}";
                else
                    ScriptTitleText = StringEscaper.Unescape(sc.Title);

                // Script Description
                ScriptDescriptionText = StringEscaper.Unescape(sc.Description);

                // Script Version
                string verStr = StringEscaper.ProcessVersionString(sc.Version);
                if (verStr == null)
                {
                    if (s != null)
                    { // Normal mode -> Notify script developer to fix
                        ScriptVersionText = "Error";
                        Global.Logger.SystemWrite(new LogInfo(LogState.Error, $"Script [{sc.Title}] contains invalid version string [{sc.Version}]"));
                    }
                    else
                    { // Build mode -> Suppress error log
                        ScriptVersionText = sc.Version;
                    }
                }
                else
                {
                    ScriptVersionText = $"v{verStr}";
                }

                // Script Author
                string author = StringEscaper.Unescape(sc.Author);
                if (ScriptAuthorLenLimit < author.Length)
                    ScriptAuthorText = author.Substring(0, ScriptAuthorLenLimit) + "...";
                else
                    ScriptAuthorText = author;
            }
        }
        #endregion

        #region TreeView Methods
        public void UpdateScriptTree(Project project, bool redrawProject, bool assertDirExist = true)
        {
            ProjectTreeItemModel projectRoot = MainTreeItems.FirstOrDefault(x => x.Script.Project.Equals(project));
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

        public static ProjectTreeItemModel PopulateOneTreeItem(Script sc, ProjectTreeItemModel projectRoot, ProjectTreeItemModel parent)
        {
            ProjectTreeItemModel item = new ProjectTreeItemModel(projectRoot, parent) { Script = sc };
            UpdateTreeViewIcon(item);
            parent?.Children.Add(item);

            return item;
        }

        public static void ScriptListToTreeViewModel(Project project, List<Script> scList, bool assertDirExist, ProjectTreeItemModel projectRoot)
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
        #endregion

        #region PrintBuildElapsedStatus Method
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

        #region ShellExecute Alternative - OpenTextFile, OpenFolder
        /// <summary>
        /// Open text file using specified/default code editor, without Administrator privilege.
        /// </summary>
        /// <param name="filePath"></param>
        public static void OpenTextFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                MessageBox.Show($"File [{filePath}] does not exist!", "Invalid Path", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (Global.Setting.Interface.UseCustomEditor)
            {
                string ext = Path.GetExtension(Global.Setting.Interface.CustomEditorPath);
                if (ext != null && !ext.Equals(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show($"Custom editor [{Global.Setting.Interface.CustomEditorPath}] is not a executable!", "Invalid Custom Editor", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!File.Exists(Global.Setting.Interface.CustomEditorPath))
                {
                    MessageBox.Show($"Custom editor [{Global.Setting.Interface.CustomEditorPath}] does not exist!", "Invalid Custom Editor", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                ProcessStartInfo info = new ProcessStartInfo
                {
                    UseShellExecute = false,
                    FileName = Global.Setting.Interface.CustomEditorPath,
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

        /// <summary>
        /// Open folder in explorer.
        /// </summary>
        /// <param name="filePath"></param>
        public static void OpenFolder(string filePath)
        {
            if (!Directory.Exists(filePath))
            {
                MessageBox.Show($"Directory [{filePath}] does not exist!", "Invalid Path", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // In most circumstances, explorer.exe is already running as a shell without Administrator privilege.
            Process proc = new Process
            {
                StartInfo = new ProcessStartInfo(filePath)
            };
            proc.Start();
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
                if (_sc.Mandatory || _sc.Selected == SelectedState.None)
                    return;

                Global.MainViewModel.WorkInProgress = true;
                try
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
                finally
                {
                    Global.MainViewModel.WorkInProgress = false;
                }
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
                _sc.Selected = value ? SelectedState.True : SelectedState.False;
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
                if (exist != 1)
                    continue;

                IniReadWriter.WriteKey(path, "Main", "Selected", "False");
                ProjectTreeItemModel found = FindScriptByRealPath(path);
                if (found == null)
                    continue;

                if (sc.Type != ScriptType.Directory && sc.Mandatory == false && sc.Selected != SelectedState.None)
                    found.Checked = false;
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
}
