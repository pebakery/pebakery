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
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using PEBakery.Core.ViewModels;

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
            Application.Current.Dispatcher.Invoke(() =>
            {
                _m.SystemLogs.Add(e.Log);
                _m.SystemLogsSelectedIndex = _m.SystemLogs.Count - 1;
                SystemLogListView.UpdateLayout();
                SystemLogListView.ScrollIntoView(SystemLogListView.Items[_m.SystemLogsSelectedIndex]);
            });
            _m.OnPropertyUpdate(nameof(_m.SystemLogs));
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
            _m.RefreshScript(e.Log.BuildId, true);
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

            Interlocked.Decrement(ref LogWindow.Count);
            CommandManager.InvalidateRequerySuggested();
        }
        #endregion

        #region BuildLog Event
        private void FullLogViewCopy_Click(object sender, RoutedEventArgs e)
        {
            if (_m.FullBuildLogSelectedIndex < 0 || _m.BuildLogs.Count <= _m.FullBuildLogSelectedIndex)
                return;

            DB_BuildLog log = _m.BuildLogs[_m.FullBuildLogSelectedIndex];
            Clipboard.SetText(log.Export(LogExportType.Text, false));
        }

        private void SimpleLogViewCopy_Click(object sender, RoutedEventArgs e)
        {
            if (_m.SimpleBuildLogSelectedIndex < 0 || _m.BuildLogs.Count <= _m.SimpleBuildLogSelectedIndex)
                return;

            DB_BuildLog log = _m.BuildLogs[_m.SimpleBuildLogSelectedIndex];
            Clipboard.SetText(log.Export(LogExportType.Text, false));
        }

        private void VariableLogViewCopy_Click(object sender, RoutedEventArgs e)
        {
            if (_m.VariableLogSelectedIndex < 0 || _m.VariableLogs.Count <= _m.VariableLogSelectedIndex)
                return;

            DB_Variable log = _m.VariableLogs[_m.VariableLogSelectedIndex];
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
            SystemLogs = new ObservableCollection<DB_SystemLog>();
            BuildEntries = new ObservableCollection<Tuple<string, int>>();
            ScriptEntries = new ObservableCollection<Tuple<string, int, int>>();
            LogStats = new ObservableCollection<Tuple<LogState, int>>();
            BuildLogs = new ObservableCollection<DB_BuildLog>();
            VariableLogs = new ObservableCollection<DB_Variable>();

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
            foreach (DB_SystemLog log in LogDb.Table<DB_SystemLog>())
            {
                log.Time = log.Time.ToLocalTime();
                SystemLogs.Add(log);
            }
            SystemLogsSelectedIndex = SystemLogs.Count - 1;
        }

        public void RefreshBuildLog()
        {
            LogStats.Clear();
            VariableLogs.Clear();

            // Populate SelectBuildEntries
            DB_BuildInfo[] buildEntries = LogDb.Table<DB_BuildInfo>()
                .OrderByDescending(x => x.StartTime)
                .ToArray();
            BuildEntries = new ObservableCollection<Tuple<string, int>>(
                buildEntries.Select(x => new Tuple<string, int>(x.Text, x.Id))
            );

            SelectedBuildIndex = 0;
        }

        public void RefreshScript(int? buildId, bool showLastScript)
        {
            ScriptEntries.Clear();

            if (buildId == null)
            {  // Clear
                SelectedScriptIndex = 0;
            }
            else
            {
                // Populate SelectScriptEntries
                ScriptEntries.Add(new Tuple<string, int, int>("Total Summary", -1, (int)buildId));
                DB_Script[] scripts = LogDb.Table<DB_Script>()
                    .Where(x => x.BuildId == buildId && 0 < x.Order)
                    .OrderBy(x => x.Order)
                    .ToArray();
                foreach (DB_Script sc in scripts)
                {
                    ScriptEntries.Add(new Tuple<string, int, int>($"[{sc.Order}/{scripts.Length}] {sc.Name} ({sc.TreePath})", sc.Id, (int)buildId));
                }

                if (showLastScript)
                    SelectedScriptIndex = ScriptEntries.Count - 1; // Last Script, which is just added
                else
                    SelectedScriptIndex = 0;
            }
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
                { // Summary
                  // BuildLog
                    _allBuildLogs = new List<DB_BuildLog>();
                    foreach (LogState state in new LogState[] { LogState.Error, LogState.Warning })
                    {
                        var bLogs = LogDb.Table<DB_BuildLog>().Where(x => x.BuildId == buildId && x.State == state);
                        _allBuildLogs.AddRange(bLogs);
                    }
                    if (_allBuildLogs.Count == 0)
                    {
                        _allBuildLogs.Add(new DB_BuildLog
                        {
                            BuildId = buildId,
                            State = LogState.Info,
                            Message = "No Error or Warning",
                            Time = DateTime.MinValue,
                        });
                    }
                    BuildLogs = new ObservableCollection<DB_BuildLog>(_allBuildLogs);

                    // Variables
                    var varLogs = LogDb.Table<DB_Variable>()
                        .Where(x => x.BuildId == buildId && x.Type != VarsType.Local)
                        .OrderBy(x => x.Type)
                        .ThenBy(x => x.Key);
                    VariableLogs = new ObservableCollection<DB_Variable>(varLogs);

                    // Statistics
                    List<Tuple<LogState, int>> fullStat = new List<Tuple<LogState, int>>();
                    var existStates = ((LogState[])Enum.GetValues(typeof(LogState))).Where(x => x != LogState.None && x != LogState.CriticalError);
                    foreach (LogState state in existStates)
                    {
                        int count = LogDb
                            .Table<DB_BuildLog>()
                            .Count(x => x.BuildId == buildId && x.State == state);

                        fullStat.Add(new Tuple<LogState, int>(state, count));
                    }
                    LogStats = new ObservableCollection<Tuple<LogState, int>>(fullStat);
                }
                else
                { // Per Script
                  // BuildLog
                    var builds = LogDb.Table<DB_BuildLog>()
                        .Where(x => x.BuildId == buildId && x.ScriptId == scriptId);
                    if (!BuildLogShowComments)
                        builds = builds.Where(x => (x.Flags & DbBuildLogFlag.Comment) != DbBuildLogFlag.Comment);
                    if (!BuildLogShowMacros)
                        builds = builds.Where(x => (x.Flags & DbBuildLogFlag.Macro) != DbBuildLogFlag.Macro);
                    _allBuildLogs = new List<DB_BuildLog>(builds);
                    BuildLogs = new ObservableCollection<DB_BuildLog>(_allBuildLogs);

                    // Variables
                    List<DB_Variable> varLogs = new List<DB_Variable>();
                    varLogs.AddRange(LogDb.Table<DB_Variable>()
                        .Where(x => x.BuildId == buildId && x.Type != VarsType.Local)
                        .OrderBy(x => x.Type)
                        .ThenBy(x => x.Key));
                    varLogs.AddRange(LogDb.Table<DB_Variable>()
                        .Where(x => x.BuildId == buildId && x.ScriptId == scriptId && x.Type == VarsType.Local)
                        .OrderBy(x => x.Key));
                    VariableLogs = new ObservableCollection<DB_Variable>(varLogs);

                    // Statistics
                    List<Tuple<LogState, int>> fullStat = new List<Tuple<LogState, int>>();
                    var existStates = ((LogState[])Enum.GetValues(typeof(LogState))).Where(x => x != LogState.None && x != LogState.CriticalError);
                    foreach (LogState state in existStates)
                    {
                        int count = LogDb
                            .Table<DB_BuildLog>()
                            .Count(x => x.BuildId == buildId && x.ScriptId == scriptId && x.State == state);

                        fullStat.Add(new Tuple<LogState, int>(state, count));
                    }
                    LogStats = new ObservableCollection<Tuple<LogState, int>>(fullStat);
                }
            }
            else
            {
                BuildLogs = new ObservableCollection<DB_BuildLog>();
            }
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
        public int SystemLogsSelectedIndex
        {
            get => _systemLogsSelectedIndex;
            set
            {
                _systemLogsSelectedIndex = value;
                OnPropertyUpdate(nameof(SystemLogsSelectedIndex));
            }
        }

        private readonly object _systemLogsLock = new object();
        private ObservableCollection<DB_SystemLog> _systemLogs;
        public ObservableCollection<DB_SystemLog> SystemLogs
        {
            get => _systemLogs;
            set
            {
                _systemLogs = value;
                BindingOperations.EnableCollectionSynchronization(_systemLogs, _systemLogsLock);
                OnPropertyUpdate(nameof(SystemLogs));
            }
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

                if (-1 < value && 0 < _buildEntries.Count)
                {
                    int buildId = _buildEntries[value].Item2;
                    RefreshScript(buildId, false);
                }
                else
                {
                    RefreshScript(null, false);
                }

                OnPropertyUpdate(nameof(SelectedBuildIndex));
            }
        }

        private readonly object _buildEntriesLock = new object();
        private ObservableCollection<Tuple<string, int>> _buildEntries;
        public ObservableCollection<Tuple<string, int>> BuildEntries
        {
            get => _buildEntries;
            set
            {
                _buildEntries = value;
                BindingOperations.EnableCollectionSynchronization(_buildEntries, _buildEntriesLock);
                OnPropertyUpdate(nameof(BuildEntries));
            }
        }

        public bool CheckSelectBulidIndex() => 0 <= SelectedBuildIndex && SelectedBuildIndex < BuildEntries.Count;

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
            set
            {
                _scriptEntries = value;
                BindingOperations.EnableCollectionSynchronization(_scriptEntries, _scriptEntriesLock);
                OnPropertyUpdate(nameof(ScriptEntries));
            }
        }

        private readonly object _logStatsLock = new object();
        private ObservableCollection<Tuple<LogState, int>> _logStats;
        public ObservableCollection<Tuple<LogState, int>> LogStats
        {
            get => _logStats;
            set
            {
                _logStats = value;
                BindingOperations.EnableCollectionSynchronization(_logStats, _logStatsLock);
                OnPropertyUpdate(nameof(LogStats));
            }
        }

        private List<DB_BuildLog> _allBuildLogs = new List<DB_BuildLog>();
        private readonly object _buildLogsLock = new object();
        private ObservableCollection<DB_BuildLog> _buildLogs;
        public ObservableCollection<DB_BuildLog> BuildLogs
        {
            get => _buildLogs;
            set
            {
                _buildLogs = value;
                BindingOperations.EnableCollectionSynchronization(_buildLogs, _buildLogsLock);
                OnPropertyUpdate(nameof(BuildLogs));
            }
        }

        private int _simpleBuildLogSelectedIndex;
        public int SimpleBuildLogSelectedIndex
        {
            get => _simpleBuildLogSelectedIndex;
            set
            {
                _simpleBuildLogSelectedIndex = value;
                OnPropertyUpdate(nameof(SimpleBuildLogSelectedIndex));
            }
        }

        private int _fullBuildLogSelectedIndex;
        public int FullBuildLogSelectedIndex
        {
            get => _fullBuildLogSelectedIndex;
            set
            {
                _fullBuildLogSelectedIndex = value;
                OnPropertyUpdate(nameof(FullBuildLogSelectedIndex));
            }
        }

        private readonly object _variableLogsLock = new object();
        private ObservableCollection<DB_Variable> _variableLogs;
        public ObservableCollection<DB_Variable> VariableLogs
        {
            get => _variableLogs;
            set
            {
                _variableLogs = value;
                BindingOperations.EnableCollectionSynchronization(_variableLogs, _variableLogsLock);
                OnPropertyUpdate(nameof(VariableLogs));
            }
        }

        private int _variableLogSelectedIndex;
        public int VariableLogSelectedIndex
        {
            get => _variableLogSelectedIndex;
            set
            {
                _variableLogSelectedIndex = value;
                OnPropertyUpdate(nameof(VariableLogSelectedIndex));
            }
        }

        private bool _buildLogShowComments = true;
        public bool BuildLogShowComments
        {
            get => _buildLogShowComments;
            set
            {
                _buildLogShowComments = value;
                OnPropertyUpdate(nameof(BuildLogShowMacros));
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
                OnPropertyUpdate(nameof(BuildLogShowMacros));
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
        public static readonly RoutedCommand CloseCommand = new RoutedUICommand("Close", "Close", typeof(LogViewCommands));
        #endregion
    }
    #endregion

    #region Converters
    public class LocalTimeToStrConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return string.Empty;

            DateTime time = (DateTime)value;
            return time == DateTime.MinValue ? string.Empty : time.ToLocalTime().ToString("yyyy-MM-dd hh:mm:ss tt", CultureInfo.InvariantCulture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is string str))
                return DateTime.Now;
            return DateTime.TryParse(str, out DateTime time) ? time : DateTime.Now;
        }
    }

    public class LogStateToStrConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return string.Empty;

            LogState state = (LogState)value;
            return state == LogState.None ? string.Empty : state.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Not Implemented
            return LogState.None;
        }
    }

    public class LineIdxToStrConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return string.Empty;

            int lineIdx = (int)value;
            return lineIdx == 0 ? string.Empty : lineIdx.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Not Implemented
            return LogState.None;
        }
    }

    public class BuildLogFlagToStrConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return string.Empty;

            DbBuildLogFlag flags = (DbBuildLogFlag)value;
            string result = string.Empty;
            if ((flags & DbBuildLogFlag.Comment) == DbBuildLogFlag.Comment)
                result += 'C';
            if ((flags & DbBuildLogFlag.Macro) == DbBuildLogFlag.Macro)
                result += 'M';
            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return string.Empty;
            if (!(value is string str))
                return null;

            DbBuildLogFlag flags = DbBuildLogFlag.None;
            if (str.Contains('C'))
                flags |= DbBuildLogFlag.Comment;
            if (str.Contains('M'))
                flags |= DbBuildLogFlag.Macro;
            return flags;
        }
    }
    #endregion
}
