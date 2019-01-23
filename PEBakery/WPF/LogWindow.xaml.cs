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

using PEBakery.Core;
using PEBakery.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PEBakery.WPF
{
    // ReSharper disable once RedundantExtendsListEntry
    public partial class LogWindow : Window
    {
        #region Field and Constructor
        public static int Count = 0;
        private readonly LogViewModel _m = new LogViewModel();

        public LogWindow(int selectedTabIndex = 0)
        {
            Interlocked.Increment(ref LogWindow.Count);

            DataContext = _m;
            InitializeComponent();

            _m.SelectedTabIndex = selectedTabIndex;
            _m.Logger.SystemLogUpdated += SystemLogUpdateEventHandler;
            _m.Logger.BuildInfoUpdated += BuildInfoUpdateEventHandler;
            _m.Logger.ScriptUpdated += ScriptUpdateEventHandler;
            _m.Logger.BuildLogUpdated += BuildLogUpdateEventHandler;
            _m.Logger.VariableUpdated += VariableUpdateEventHandler;
            _m.Logger.FullRefresh += FullRefreshEventHandler;

            _m.WindowWidth = Global.Setting.LogViewer.LogWindowWidth;
            _m.WindowHeight = Global.Setting.LogViewer.LogWindowHeight;
            _m.BuildLogShowTime = Global.Setting.LogViewer.BuildFullLogShowTime;
            _m.BuildLogShowScriptOrigin = Global.Setting.LogViewer.BuildFullLogShowScriptOrigin;
            _m.BuildLogShowDepth = Global.Setting.LogViewer.BuildFullLogShowDepth;
            _m.BuildLogShowState = Global.Setting.LogViewer.BuildFullLogShowState;
            _m.BuildLogShowFlags = Global.Setting.LogViewer.BuildFullLogShowFlags;
            _m.BuildLogShowMessage = Global.Setting.LogViewer.BuildFullLogShowMessage;
            _m.BuildLogShowRawCode = Global.Setting.LogViewer.BuildFullLogShowRawCode;
            _m.BuildLogShowLineNumber = Global.Setting.LogViewer.BuildFullLogShowLineNumber;

            SystemLogListView.UpdateLayout();
            if (1 < SystemLogListView.Items.Count)
            {
                int idx = SystemLogListView.Items.Count - 1;
                SystemLogListView.ScrollIntoView(SystemLogListView.Items[idx]);
            }
        }
        #endregion

        #region Logger EventHandler
        public void SystemLogUpdateEventHandler(object sender, SystemLogUpdateEventArgs e)
        {
            // Another dirty hack (Dispatcher) to avoid crash from threading issue
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (e.Log != null)
                {
                    _m.SystemLogs.Add(e.Log);
                }
                else if (e.Logs != null)
                {
                    // e.Logs
                    foreach (LogModel.SystemLog dbLog in e.Logs)
                        _m.SystemLogs.Add(dbLog);
                }
                else
                {
                    Debug.Assert(false, $"Invalid {nameof(SystemLogUpdateEventArgs)}");
                }

                _m.SystemLogSelectedIndex = SystemLogListView.Items.Count - 1;
                SystemLogListView.UpdateLayout();
                SystemLogListView.ScrollIntoView(SystemLogListView.Items[_m.SystemLogSelectedIndex]);
            });
        }

        public void BuildInfoUpdateEventHandler(object sender, BuildInfoUpdateEventArgs e)
        {
            _m.RefreshBuildLog();
        }

        public void BuildLogUpdateEventHandler(object sender, BuildLogUpdateEventArgs e)
        {
            if (_m.BuildEntries != null &&
                _m.SelectedBuildIndex < _m.BuildEntries.Count &&
                _m.BuildEntries[_m.SelectedBuildIndex].Item2 == e.Log.BuildId &&
                _m.ScriptEntries != null &&
                _m.SelectedScriptIndex < _m.ScriptEntries.Count &&
                _m.ScriptEntries[_m.SelectedScriptIndex].Item2 == e.Log.ScriptId)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _m.BuildLogs.Add(e.Log);
                    _m.OnPropertyUpdate(nameof(_m.BuildLogs));

                    if (0 < BuildLogSimpleListView.Items.Count)
                    {
                        BuildLogSimpleListView.UpdateLayout();
                        BuildLogSimpleListView.ScrollIntoView(BuildLogSimpleListView.Items[BuildLogSimpleListView.Items.Count - 1]);
                    }

                    if (0 < BuildLogDetailListView.Items.Count)
                    {
                        BuildLogDetailListView.UpdateLayout();
                        BuildLogDetailListView.ScrollIntoView(BuildLogDetailListView.Items[BuildLogDetailListView.Items.Count - 1]);
                    }
                });
            }
        }

        public void ScriptUpdateEventHandler(object sender, ScriptUpdateEventArgs e)
        {
            _m.RefreshScripts(e.Log.BuildId, true);
        }

        public void VariableUpdateEventHandler(object sender, VariableUpdateEventArgs e)
        {
            if (_m.BuildEntries != null &&
                0 <= _m.SelectedBuildIndex && _m.SelectedBuildIndex < _m.BuildEntries.Count &&
                _m.BuildEntries[_m.SelectedBuildIndex].Item2 == e.Log.BuildId)
            {
                if (e.Log.Type != VarsType.Local)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _m.VariableLogs.Add(e.Log);
                        _m.OnPropertyUpdate(nameof(_m.VariableLogs));
                    });
                }
            }
        }

        public void FullRefreshEventHandler(object sender, EventArgs e)
        {
            // Refresh Build Log
            _m.RefreshBuildLog();
        }
        #endregion

        #region Window Event
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Close();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            _m.Logger.SystemLogUpdated -= SystemLogUpdateEventHandler;
            _m.Logger.BuildInfoUpdated -= BuildInfoUpdateEventHandler;
            _m.Logger.ScriptUpdated -= ScriptUpdateEventHandler;
            _m.Logger.BuildLogUpdated -= BuildLogUpdateEventHandler;
            _m.Logger.VariableUpdated -= VariableUpdateEventHandler;

            Global.Setting.LogViewer.LogWindowWidth = _m.WindowWidth;
            Global.Setting.LogViewer.LogWindowHeight = _m.WindowHeight;
            Global.Setting.LogViewer.BuildFullLogShowTime = _m.BuildLogShowTime;
            Global.Setting.LogViewer.BuildFullLogShowScriptOrigin = _m.BuildLogShowScriptOrigin;
            Global.Setting.LogViewer.BuildFullLogShowDepth = _m.BuildLogShowDepth;
            Global.Setting.LogViewer.BuildFullLogShowState = _m.BuildLogShowState;
            Global.Setting.LogViewer.BuildFullLogShowFlags = _m.BuildLogShowFlags;
            Global.Setting.LogViewer.BuildFullLogShowMessage = _m.BuildLogShowMessage;
            Global.Setting.LogViewer.BuildFullLogShowRawCode = _m.BuildLogShowRawCode;
            Global.Setting.LogViewer.BuildFullLogShowLineNumber = _m.BuildLogShowLineNumber;
            Global.Setting.WriteToFile();

            Interlocked.Decrement(ref LogWindow.Count);
            CommandManager.InvalidateRequerySuggested();
        }
        #endregion

        #region Context Menus
        private void SystemLogViewCopy_Click(object sender, RoutedEventArgs e)
        {
            if (_m.SystemLogSelectedIndex < 0 || _m.SystemLogs.Count <= _m.SystemLogSelectedIndex)
                return;

            LogModel.SystemLog log = _m.SystemLogs[_m.SystemLogSelectedIndex];
            Clipboard.SetText(log.State == LogState.None ? log.Message : $"[{log.State}] {log.Message}");
        }

        private void FullLogViewCopy_Click(object sender, RoutedEventArgs e)
        {
            if (_m.FullBuildLogSelectedIndex < 0 || _m.BuildLogs.Count <= _m.FullBuildLogSelectedIndex)
                return;

            LogModel.BuildLog log = _m.BuildLogs[_m.FullBuildLogSelectedIndex];
            Clipboard.SetText(log.Export(LogExportType.Text, false));
        }

        private void SimpleLogViewCopy_Click(object sender, RoutedEventArgs e)
        {
            if (_m.SimpleBuildLogSelectedIndex < 0 || _m.BuildLogs.Count <= _m.SimpleBuildLogSelectedIndex)
                return;

            LogModel.BuildLog log = _m.BuildLogs[_m.SimpleBuildLogSelectedIndex];
            Clipboard.SetText(log.Export(LogExportType.Text, false));
        }

        private void VariableLogViewCopy_Click(object sender, RoutedEventArgs e)
        {
            if (_m.VariableLogSelectedIndex < 0 || _m.VariableLogs.Count <= _m.VariableLogSelectedIndex)
                return;

            LogModel.Variable log = _m.VariableLogs[_m.VariableLogSelectedIndex];
            Clipboard.SetText($"[{log.Type}] %{log.Key}%={log.Value}");
        }
        #endregion

        #region Commands
        private void RefreshCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _m != null && _m.CanExecuteCommand;
        }

        private void RefreshCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            _m.CanExecuteCommand = false;
            try
            {
                switch (_m.SelectedTabIndex)
                {
                    case 0: // System Log 
                        _m.RefreshSystemLog();
                        break;
                    case 1: // Build Log
                        _m.RefreshBuildLog();
                        break;
                }
            }
            finally
            {
                _m.CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void ClearCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _m != null && _m.CanExecuteCommand && !Global.MainViewModel.WorkInProgress && Engine.WorkingLock == 0;
        }

        private async void ClearCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            _m.CanExecuteCommand = false;
            try
            {
                await Task.Run(() =>
                {
                    switch (_m.SelectedTabIndex)
                    {
                        case 0: // System Log 
                            _m.Logger.Db.ClearTable(new LogDatabase.ClearTableOptions
                            {
                                SystemLog = true,
                            });
                            _m.RefreshSystemLog();
                            break;
                        case 1: // Build Log
                            _m.Logger.Db.ClearTable(new LogDatabase.ClearTableOptions
                            {
                                BuildInfo = true,
                                BuildLog = true,
                                Script = true,
                                Variable = true,
                            });
                            _m.RefreshBuildLog();
                            break;
                    }
                });
            }
            finally
            {
                _m.CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void ExportCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _m != null && _m.CanExecuteCommand;
        }

        private void ExportCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            _m.CanExecuteCommand = false;
            try
            {
                LogExportModel exportModel = new LogExportModel(_m.Logger, _m.BuildEntries);

                if (_m.SelectedTabIndex == 0) // Export System Logs
                    exportModel.SetSystemLog();
                else // Export Build Logs
                    exportModel.SetBuildLog(_m.SelectedBuildIndex, _m.BuildLogShowComments, _m.BuildLogShowMacros);

                LogExportWindow dialog = new LogExportWindow(exportModel) { Owner = this };
                dialog.ShowDialog();
            }
            finally
            {
                _m.CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void CloseCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _m != null && _m.CanExecuteCommand;
        }

        private void CloseCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Close();
        }

        private void LogOptionsCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _m != null && _m.CanExecuteCommand;
        }

        private void LogOptionsCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            // Open Context Menu
            if (e.Source is Button button && button.ContextMenu is ContextMenu menu)
            {
                menu.PlacementTarget = button;
                menu.IsOpen = true;
            }
        }
        #endregion
    }

    #region LogViewModel
    public class LogViewModel : ViewModelBase
    {
        #region Fields and Properties
        public LogDatabase LogDb => Logger.Db;
        public Logger Logger { get; set; }
        #endregion

        #region Constructor
        public LogViewModel()
        {
            // Set Logger
            Logger = Global.Logger;

            // Set ObservableCollection
            SystemLogs = new ObservableCollection<LogModel.SystemLog>();
            BuildEntries = new ObservableCollection<Tuple<string, int>>();
            ScriptEntries = new ObservableCollection<Tuple<string, int, int>>();
            LogStats = new ObservableCollection<Tuple<LogState, int>>();
            BuildLogs = new ObservableCollection<LogModel.BuildLog>();
            VariableLogs = new ObservableCollection<LogModel.Variable>();

            // Prepare Logs
            RefreshSystemLog();
            RefreshBuildLog();
        }
        #endregion

        #region CanExecuteCommand
        public bool CanExecuteCommand { get; set; } = true;
        #endregion

        #region Refresh 
        public void RefreshSystemLog()
        {
            SystemLogs.Clear();
            foreach (LogModel.SystemLog log in LogDb.Table<LogModel.SystemLog>())
            {
                log.Time = log.Time.ToLocalTime();
                SystemLogs.Add(log);
            }
            SystemLogSelectedIndex = SystemLogs.Count - 1;
        }

        public void RefreshBuildLog()
        {
            // I don't know why, but LogStats.Clear throws thread exception even though EnableCollectionSynchronization is used.
            // Reproduce: Remove Dispatcher.Invoke, and run CodeBox three times in a row (without closing LogWindow).
            // TODO: This is a quick dirty fix. Apply better patch later.
            Application.Current?.Dispatcher.Invoke(() =>
            {
                LogStats.Clear();
                VariableLogs.Clear();

                // Populate SelectBuildEntries
                LogModel.BuildInfo[] buildEntries = LogDb.Table<LogModel.BuildInfo>()
                    .OrderByDescending(x => x.StartTime)
                    .ToArray();
                BuildEntries = new ObservableCollection<Tuple<string, int>>(
                    buildEntries.Select(x => new Tuple<string, int>(x.Text, x.Id))
                );
            });

            SelectedBuildIndex = 0;
            // Print summary
            SelectedScriptIndex = 0;
        }

        public void RefreshScripts(int? buildId, bool showLastScript)
        {
            // Without dispatcher, ScriptEntries.Clear sets SelectedScriptIndex to -1 too late.
            Application.Current?.Dispatcher.Invoke(() =>
            {
                ScriptEntries.Clear();

                if (buildId == null)
                {
                    // Clear
                    SelectedScriptIndex = -1;
                }
                else
                {
                    // Populate SelectScriptEntries
                    ScriptEntries.Add(new Tuple<string, int, int>("Total Summary", -1, (int)buildId));
                    LogModel.Script[] scripts = LogDb.Table<LogModel.Script>()
                        .Where(x => x.BuildId == buildId && 0 < x.Order)
                        .OrderBy(x => x.Order)
                        .ToArray();
                    foreach (LogModel.Script sc in scripts)
                    {
                        ScriptEntries.Add(new Tuple<string, int, int>($"[{sc.Order}/{scripts.Length}] {sc.Name} ({sc.TreePath})", sc.Id, (int)buildId));
                    }

                    if (showLastScript)
                        SelectedScriptIndex = ScriptEntries.Count - 1; // Display Last Script
                    else
                        SelectedScriptIndex = 0; // Summary is always index 0
                }
            });

        }

        /// <summary>
        /// Update build logs
        /// </summary>
        public void RefreshBuildLog(int scriptIdx)
        {
            if (scriptIdx != -1 && 0 < _scriptEntries.Count)
            {
                int scriptId = _scriptEntries[scriptIdx].Item2;
                int buildId = _scriptEntries[scriptIdx].Item3;

                if (scriptId == -1)
                { // Total Summary
                    // BuildLog
                    _allBuildLogs = new List<LogModel.BuildLog>();
                    foreach (LogState state in new LogState[] { LogState.Error, LogState.Warning })
                    {
                        var bLogs = LogDb.Table<LogModel.BuildLog>().Where(x => x.BuildId == buildId && x.State == state);
                        _allBuildLogs.AddRange(bLogs);
                    }
                    if (_allBuildLogs.Count == 0)
                    {
                        _allBuildLogs.Add(new LogModel.BuildLog
                        {
                            BuildId = buildId,
                            State = LogState.Info,
                            Message = "No Error or Warning",
                            Time = DateTime.MinValue,
                        });
                    }
                    BuildLogs = new ObservableCollection<LogModel.BuildLog>(_allBuildLogs);

                    // Variables
                    var varLogs = LogDb.Table<LogModel.Variable>()
                        .Where(x => x.BuildId == buildId && x.Type != VarsType.Local)
                        .OrderBy(x => x.Type)
                        .ThenBy(x => x.Key);
                    VariableLogs = new ObservableCollection<LogModel.Variable>(varLogs);

                    // Statistics
                    List<Tuple<LogState, int>> fullStat = new List<Tuple<LogState, int>>();
                    var existStates = ((LogState[])Enum.GetValues(typeof(LogState))).Where(x => x != LogState.None && x != LogState.CriticalError);
                    foreach (LogState state in existStates)
                    {
                        int count = LogDb
                            .Table<LogModel.BuildLog>()
                            .Count(x => x.BuildId == buildId && x.State == state);

                        fullStat.Add(new Tuple<LogState, int>(state, count));
                    }
                    LogStats = new ObservableCollection<Tuple<LogState, int>>(fullStat);
                }
                else
                { // Per Script
                    // Script Title Dict for script origin
                    ScriptTitleDict = Global.Logger.Db.Table<LogModel.Script>()
                        .Where(x => x.BuildId == buildId)
                        .ToDictionary(x => x.Id, x => x.Name);

                    // BuildLog
                    var builds = LogDb.Table<LogModel.BuildLog>()
                        .Where(x => x.BuildId == buildId && x.ScriptId == scriptId);
                    if (!BuildLogShowComments)
                        builds = builds.Where(x => (x.Flags & LogModel.BuildLogFlag.Comment) != LogModel.BuildLogFlag.Comment);
                    if (!BuildLogShowMacros)
                        builds = builds.Where(x => (x.Flags & LogModel.BuildLogFlag.Macro) != LogModel.BuildLogFlag.Macro);
                    _allBuildLogs = new List<LogModel.BuildLog>(builds);
                    BuildLogs = new ObservableCollection<LogModel.BuildLog>(_allBuildLogs);

                    // Variables
                    List<LogModel.Variable> varLogs = new List<LogModel.Variable>();
                    varLogs.AddRange(LogDb.Table<LogModel.Variable>()
                        .Where(x => x.BuildId == buildId && x.Type != VarsType.Local)
                        .OrderBy(x => x.Type)
                        .ThenBy(x => x.Key));
                    varLogs.AddRange(LogDb.Table<LogModel.Variable>()
                        .Where(x => x.BuildId == buildId && x.ScriptId == scriptId && x.Type == VarsType.Local)
                        .OrderBy(x => x.Key));
                    VariableLogs = new ObservableCollection<LogModel.Variable>(varLogs);

                    // Statistics
                    List<Tuple<LogState, int>> fullStat = new List<Tuple<LogState, int>>();
                    var existStates = ((LogState[])Enum.GetValues(typeof(LogState))).Where(x => x != LogState.None && x != LogState.CriticalError);
                    foreach (LogState state in existStates)
                    {
                        int count = LogDb
                            .Table<LogModel.BuildLog>()
                            .Count(x => x.BuildId == buildId && x.ScriptId == scriptId && x.State == state);

                        fullStat.Add(new Tuple<LogState, int>(state, count));
                    }
                    LogStats = new ObservableCollection<Tuple<LogState, int>>(fullStat);
                }
            }
            else
            {
                BuildLogs = new ObservableCollection<LogModel.BuildLog>();
            }
        }
        #endregion

        #region Window Resolution
        private int _windowWidth = 900;
        public int WindowWidth
        {
            get => _windowWidth;
            set => SetProperty(ref _windowWidth, value);
        }

        private int _windowHeight = 640;
        public int WindowHeight
        {
            get => _windowHeight;
            set => SetProperty(ref _windowHeight, value);
        }
        #endregion

        #region TabIndex
        private int _selectedTabIndex;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set
            {
                _selectedTabIndex = value;
                OnPropertyUpdate(nameof(SelectedTabIndex));
                OnPropertyUpdate(nameof(BuildLogSelected));
            }
        }

        public Visibility BuildLogSelected => _selectedTabIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        #endregion

        #region SystemLog
        private int _systemLogsSelectedIndex;
        public int SystemLogSelectedIndex
        {
            get => _systemLogsSelectedIndex;
            set => SetProperty(ref _systemLogsSelectedIndex, value);
        }

        private readonly object _systemLogsLock = new object();
        private ObservableCollection<LogModel.SystemLog> _systemLogs;
        public ObservableCollection<LogModel.SystemLog> SystemLogs
        {
            get => _systemLogs;
            set => SetCollectionProperty(ref _systemLogs, _systemLogsLock, value);
        }
        #endregion

        #region BuildLog
        private int _selectBuildIndex;
        public int SelectedBuildIndex
        {
            get => _selectBuildIndex;
            set
            {
                _selectBuildIndex = value;

                if (0 <= value && 0 < _buildEntries.Count)
                {
                    int buildId = _buildEntries[value].Item2;
                    RefreshScripts(buildId, false);
                }
                else
                {
                    RefreshScripts(null, false);
                }

                OnPropertyUpdate(nameof(SelectedBuildIndex));
            }
        }

        // Build Name, Build Id
        private readonly object _buildEntriesLock = new object();
        private ObservableCollection<Tuple<string, int>> _buildEntries;
        public ObservableCollection<Tuple<string, int>> BuildEntries
        {
            get => _buildEntries;
            set => SetCollectionProperty(ref _buildEntries, _buildEntriesLock, value);
        }

        public bool CheckSelectBuildIndex() => 0 <= SelectedBuildIndex && SelectedBuildIndex < BuildEntries.Count;

        private int _selectedScriptIndex;
        public int SelectedScriptIndex
        {
            get => _selectedScriptIndex;
            set
            {
                _selectedScriptIndex = value;
                RefreshBuildLog(_selectedScriptIndex);
                OnPropertyUpdate(nameof(SelectedScriptIndex));
            }
        }

        // Script Name, Script Id, Build Id
        private readonly object _scriptEntriesLock = new object();
        private ObservableCollection<Tuple<string, int, int>> _scriptEntries;
        public ObservableCollection<Tuple<string, int, int>> ScriptEntries
        {
            get => _scriptEntries;
            set => SetCollectionProperty(ref _scriptEntries, _scriptEntriesLock, value);
        }

        private readonly object _logStatsLock = new object();
        private ObservableCollection<Tuple<LogState, int>> _logStats;
        public ObservableCollection<Tuple<LogState, int>> LogStats
        {
            get => _logStats;
            set => SetCollectionProperty(ref _logStats, _logStatsLock, value);
        }

        private List<LogModel.BuildLog> _allBuildLogs = new List<LogModel.BuildLog>();
        private readonly object _buildLogsLock = new object();
        private ObservableCollection<LogModel.BuildLog> _buildLogs;
        public ObservableCollection<LogModel.BuildLog> BuildLogs
        {
            get => _buildLogs;
            set => SetCollectionProperty(ref _buildLogs, _buildLogsLock, value);
        }

        private Dictionary<int, string> _scriptTitleDict;
        public Dictionary<int, string> ScriptTitleDict
        {
            get => _scriptTitleDict;
            set => SetProperty(ref _scriptTitleDict, value);
        }

        private int _simpleBuildLogSelectedIndex;
        public int SimpleBuildLogSelectedIndex
        {
            get => _simpleBuildLogSelectedIndex;
            set => SetProperty(ref _simpleBuildLogSelectedIndex, value);
        }

        private int _fullBuildLogSelectedIndex;
        public int FullBuildLogSelectedIndex
        {
            get => _fullBuildLogSelectedIndex;
            set => SetProperty(ref _fullBuildLogSelectedIndex, value);
        }

        private readonly object _variableLogsLock = new object();
        private ObservableCollection<LogModel.Variable> _variableLogs;
        public ObservableCollection<LogModel.Variable> VariableLogs
        {
            get => _variableLogs;
            set => SetCollectionProperty(ref _variableLogs, _variableLogsLock, value);
        }

        private int _variableLogSelectedIndex;
        public int VariableLogSelectedIndex
        {
            get => _variableLogSelectedIndex;
            set => SetProperty(ref _variableLogSelectedIndex, value);
        }

        // Log Filters
        private bool _buildLogShowComments = true;
        public bool BuildLogShowComments
        {
            get => _buildLogShowComments;
            set
            {
                _buildLogShowComments = value;
                OnPropertyUpdate();
                RefreshBuildLog(SelectedScriptIndex);
            }
        }

        private bool _buildLogShowMacros = true;
        public bool BuildLogShowMacros
        {
            get => _buildLogShowMacros;
            set
            {
                _buildLogShowMacros = value;
                OnPropertyUpdate();
                RefreshBuildLog(SelectedScriptIndex);
            }
        }

        // Show Columns
        private bool _buildLogShowTime = true;
        public bool BuildLogShowTime
        {
            get => _buildLogShowTime;
            set
            {
                _buildLogShowTime = value;
                OnPropertyUpdate();
                RefreshBuildLog(SelectedScriptIndex);
            }
        }

        private bool _buildLogShowScriptOrigin = false;
        public bool BuildLogShowScriptOrigin
        {
            get => _buildLogShowScriptOrigin;
            set
            {
                _buildLogShowScriptOrigin = value;
                OnPropertyUpdate();
                RefreshBuildLog(SelectedScriptIndex);
            }
        }

        private bool _buildLogShowDepth = true;
        public bool BuildLogShowDepth
        {
            get => _buildLogShowDepth;
            set
            {
                _buildLogShowDepth = value;
                OnPropertyUpdate();
                RefreshBuildLog(SelectedScriptIndex);
            }
        }

        private bool _buildLogShowState = true;
        public bool BuildLogShowState
        {
            get => _buildLogShowState;
            set
            {
                _buildLogShowState = value;
                OnPropertyUpdate();
                RefreshBuildLog(SelectedScriptIndex);
            }
        }

        private bool _buildLogShowFlags = true;
        public bool BuildLogShowFlags
        {
            get => _buildLogShowFlags;
            set
            {
                _buildLogShowFlags = value;
                OnPropertyUpdate();
                RefreshBuildLog(SelectedScriptIndex);
            }
        }

        private bool _buildLogShowMessage = true;
        public bool BuildLogShowMessage
        {
            get => _buildLogShowMessage;
            set
            {
                _buildLogShowMessage = value;
                OnPropertyUpdate();
                RefreshBuildLog(SelectedScriptIndex);
            }
        }

        private bool _buildLogShowRawCode = true;
        public bool BuildLogShowRawCode
        {
            get => _buildLogShowRawCode;
            set
            {
                _buildLogShowRawCode = value;
                OnPropertyUpdate();
                RefreshBuildLog(SelectedScriptIndex);
            }
        }

        private bool _buildLogShowLineNumber = true;
        public bool BuildLogShowLineNumber
        {
            get => _buildLogShowLineNumber;
            set
            {
                _buildLogShowLineNumber = value;
                OnPropertyUpdate();
                RefreshBuildLog(SelectedScriptIndex);
            }
        }
        #endregion

        #region Utility
        private void ResizeGridViewColumn(GridViewColumn column)
        {
            if (double.IsNaN(column.Width))
                column.Width = column.ActualWidth;
            column.Width = double.NaN;
        }
        #endregion
    }
    #endregion

    #region LogViewCommands
    public static class LogViewCommands
    {
        #region Main Buttons
        public static readonly RoutedCommand RefreshCommand = new RoutedUICommand("Refresh logs", "Refresh", typeof(LogViewCommands),
            new InputGestureCollection
            {
                new KeyGesture(Key.F5),
            });
        public static readonly RoutedCommand ClearCommand = new RoutedUICommand("Clear logs", "Clear", typeof(LogViewCommands));
        public static readonly RoutedCommand ExportCommand = new RoutedUICommand("Export logs", "Export", typeof(LogViewCommands));
        public static readonly RoutedCommand LogOptionsCommand = new RoutedUICommand("Log Options", "Options", typeof(LogViewCommands));
        public static readonly RoutedCommand CloseCommand = new RoutedUICommand("Close", "Close", typeof(LogViewCommands));
        #endregion
    }
    #endregion
}
