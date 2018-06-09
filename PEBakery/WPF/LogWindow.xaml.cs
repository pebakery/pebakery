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
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PEBakery.WPF
{
    // ReSharper disable once RedundantExtendsListEntry
    public partial class LogWindow : Window
    {
        public static int Count = 0;
        private readonly LogViewModel _m = new LogViewModel();

        public LogWindow(int selectedTabIndex = 0)
        {
            Interlocked.Increment(ref LogWindow.Count);

            InitializeComponent();
            DataContext = _m;

            _m.SelectedTabIndex = selectedTabIndex;
            _m.Logger.SystemLogUpdated += SystemLogUpdateEventHandler;
            _m.Logger.BuildInfoUpdated += BuildInfoUpdateEventHandler;
            _m.Logger.ScriptUpdated += ScriptUpdateEventHandler;
            _m.Logger.BuildLogUpdated += BuildLogUpdateEventHandler;
            _m.Logger.VariableUpdated += VariableUpdateEventHandler;
            _m.Logger.FullRefresh += FullRefreshEventHandler;

            SystemLogListView.UpdateLayout();
            if (1 < SystemLogListView.Items.Count)
                SystemLogListView.ScrollIntoView(SystemLogListView.Items[SystemLogListView.Items.Count - 1]);
        }

        #region Logger EventHandler
        public void SystemLogUpdateEventHandler(object sender, SystemLogUpdateEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _m.SystemLogListModel.Add(e.Log);
                _m.SystemLogListSelectedIndex = _m.SystemLogListModel.Count - 1;
                SystemLogListView.UpdateLayout();
                SystemLogListView.ScrollIntoView(SystemLogListView.Items[_m.SystemLogListSelectedIndex]);
            });
            _m.OnPropertyUpdate(nameof(SystemLogListModel));
        }

        public void BuildInfoUpdateEventHandler(object sender, BuildInfoUpdateEventArgs e)
        {
            _m.RefreshBuildLog();
        }

        public void BuildLogUpdateEventHandler(object sender, BuildLogUpdateEventArgs e)
        {
            if (_m.SelectBuildEntries != null &&
                _m.SelectBuildIndex < _m.SelectBuildEntries.Count &&
                _m.SelectBuildEntries[_m.SelectBuildIndex].Item2 == e.Log.BuildId &&
                _m.SelectScriptEntries != null &&
                _m.SelectScriptIndex < _m.SelectScriptEntries.Count &&
                _m.SelectScriptEntries[_m.SelectScriptIndex].Item2 == e.Log.ScriptId)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _m.BuildLogListModel.Add(e.Log);
                    _m.OnPropertyUpdate(nameof(BuildLogListModel));

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
            if (_m.SelectBuildEntries != null &&
                0 <= _m.SelectBuildIndex && _m.SelectBuildIndex < _m.SelectBuildEntries.Count &&
                _m.SelectBuildEntries[_m.SelectBuildIndex].Item2 == e.Log.BuildId)
            {
                if (e.Log.Type != VarsType.Local)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _m.VariableListModel.Add(e.Log);
                        _m.OnPropertyUpdate(nameof(VariableListModel));
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
        }
        #endregion

        #region Button Event
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            int idx = MainTab.SelectedIndex;
            switch (idx)
            {
                case 0: // System Log 
                    _m.RefreshSystemLog();
                    break;
                case 1: // Build Log
                    _m.RefreshBuildLog();
                    break;
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            bool busy = false;
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (!(Application.Current.MainWindow is MainWindow w))
                    return;
                busy = w.Model.WorkInProgress || 0 < Engine.WorkingLock;
            });
            if (busy)
            {
                MessageBox.Show("PEBakery is busy, please wait.", "Please Wait", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int idx = MainTab.SelectedIndex;
            switch (idx)
            {
                case 0: // System Log 
                    _m.LogDB.DeleteAll<DB_SystemLog>();
                    _m.RefreshSystemLog();
                    break;
                case 1: // Build Log
                    _m.LogDB.DeleteAll<DB_BuildInfo>();
                    _m.LogDB.DeleteAll<DB_BuildLog>();
                    _m.LogDB.DeleteAll<DB_Script>();
                    _m.LogDB.DeleteAll<DB_Variable>();
                    _m.RefreshBuildLog();
                    break;
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            string baseDir;
            {
                if (!(Application.Current.MainWindow is MainWindow w))
                    return;
                baseDir = w.BaseDir;
            }

            string title;
            if (_m.SelectedTabIndex == 0) // System Log
                title = "Export System Log";
            else // Build Log
                title = "Export Build Log";

            Microsoft.Win32.SaveFileDialog dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = title,
                Filter = "Text Format (*.txt)|*.txt|HTML Format (*.html)|*.html",
                InitialDirectory = baseDir,
            };

            if (dialog.ShowDialog() == true)
            {
                string ext = System.IO.Path.GetExtension(dialog.FileName);
                LogExportType type = LogExportType.Text;
                if (ext.Equals(".html", StringComparison.OrdinalIgnoreCase))
                    type = LogExportType.Html;

                if (_m.SelectedTabIndex == 0) // System Log
                {
                    _m.Logger.ExportSystemLog(type, dialog.FileName);
                }
                else // Build Log
                {
                    int idx = _m.SelectBuildIndex;
                    int buildId = _m.SelectBuildEntries[idx].Item2; // Build Id
                    _m.Logger.ExportBuildLog(type, dialog.FileName, buildId);
                }

                // Open log file
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (!(Application.Current.MainWindow is MainWindow w))
                        return;
                    w.OpenTextFile(dialog.FileName);
                });
            }
        }
        #endregion  
    }

    #region LogListModel
    public class LogStatModel : ObservableCollection<Tuple<LogState, int>> { }
    public class SystemLogListModel : ObservableCollection<DB_SystemLog> { }
    // public class ScriptListModel : ObservableCollection<DB_Script> { }
    public class VariableListModel : ObservableCollection<DB_Variable> { }
    public class BuildLogListModel : ObservableCollection<DB_BuildLog> { }
    #endregion

    #region LogViewModel
    public class LogViewModel : INotifyPropertyChanged
    {
        #region Fields and Properties
        // ReSharper disable once InconsistentNaming
        public LogDatabase LogDB => Logger.DB;
        public Logger Logger { get; set; }
        #endregion

        #region Constructor
        public LogViewModel()
        {
            Logger = App.Logger;
            RefreshSystemLog();
            RefreshBuildLog();
        }
        #endregion

        #region Refresh 
        public void RefreshSystemLog()
        {
            SystemLogListModel list = new SystemLogListModel();
            foreach (DB_SystemLog log in LogDB.Table<DB_SystemLog>())
            {
                log.Time = log.Time.ToLocalTime();
                list.Add(log);
            }

            SystemLogListModel = list;

            SystemLogListSelectedIndex = SystemLogListModel.Count - 1;
        }

        public void RefreshBuildLog()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                LogStatModel.Clear();
                VariableListModel.Clear();

                // Populate SelectBuildEntries
                SelectBuildEntries.Clear();
                foreach (DB_BuildInfo b in LogDB.Table<DB_BuildInfo>().OrderByDescending(x => x.StartTime))
                {
                    string timeStr = b.StartTime.ToLocalTime().ToString("yyyy-MM-dd hh:mm:ss tt", CultureInfo.InvariantCulture);
                    SelectBuildEntries.Add(new Tuple<string, int>($"[{timeStr}] {b.Name} ({b.Id})", b.Id));
                }
                SelectBuildIndex = 0;
            });
        }

        public void RefreshScript(int? buildId, bool showLastScript)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                SelectScriptEntries.Clear();

                if (buildId == null)
                {  // Clear
                    SelectScriptIndex = 0;
                }
                else
                {
                    // Populate SelectScriptEntries
                    SelectScriptEntries.Add(new Tuple<string, int, int>("Total Summary", -1, (int)buildId));
                    DB_Script[] scripts = LogDB.Table<DB_Script>().Where(x => x.BuildId == buildId).OrderBy(x => x.Order).ToArray();
                    foreach (DB_Script sc in scripts)
                    {
                        SelectScriptEntries.Add(new Tuple<string, int, int>($"[{sc.Order}/{scripts.Length}] {sc.Name} ({sc.Path})", sc.Id, (int)buildId));
                    }

                    if (showLastScript)
                        SelectScriptIndex = SelectScriptEntries.Count - 1; // Last Script, which is just added
                    else
                        SelectScriptIndex = 0;
                }
            });
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
            }
        }
        #endregion

        #region SystemLog
        private int _systemLogListSelectedIndex;
        public int SystemLogListSelectedIndex
        {
            get => _systemLogListSelectedIndex;
            set
            {
                _systemLogListSelectedIndex = value;
                OnPropertyUpdate(nameof(SystemLogListSelectedIndex));
            }
        }

        private SystemLogListModel _systemLogListModel = new SystemLogListModel();
        public SystemLogListModel SystemLogListModel
        {
            get => _systemLogListModel;
            set
            {
                _systemLogListModel = value;
                OnPropertyUpdate(nameof(SystemLogListModel));
            }
        }
        #endregion

        #region BuildLog
        private int _selectBuildIndex;
        public int SelectBuildIndex
        {
            get => _selectBuildIndex;
            set
            {
                _selectBuildIndex = value;

                if (0 < _selectBuildEntries.Count)
                {
                    int buildId = _selectBuildEntries[value].Item2;
                    RefreshScript(buildId, false);
                }
                else
                {
                    RefreshScript(null, false);
                }

                OnPropertyUpdate(nameof(SelectBuildIndex));
            }
        }

        private ObservableCollection<Tuple<string, int>> _selectBuildEntries = new ObservableCollection<Tuple<string, int>>();
        public ObservableCollection<Tuple<string, int>> SelectBuildEntries
        {
            get => _selectBuildEntries;
            set
            {
                _selectBuildEntries = value;
                OnPropertyUpdate(nameof(SelectBuildEntries));
            }
        }

        private int _selectScriptIndex;
        public int SelectScriptIndex
        {
            get => _selectScriptIndex;
            set
            {
                _selectScriptIndex = value;
                if (value != -1 && 0 < _selectScriptEntries.Count)
                {
                    int scriptId = _selectScriptEntries[value].Item2;
                    int buildId = _selectScriptEntries[value].Item3;

                    if (scriptId == -1)
                    { // Summary
                        // BuildLog
                        BuildLogListModel buildLogListModel = new BuildLogListModel();
                        foreach (LogState state in new LogState[] { LogState.Error, LogState.Warning })
                        {
                            var bLogs = LogDB.Table<DB_BuildLog>().Where(x => x.BuildId == buildId && x.State == state);
                            foreach (DB_BuildLog b in bLogs)
                                buildLogListModel.Add(b);
                        }
                        if (!buildLogListModel.Any())
                        {
                            buildLogListModel.Add(new DB_BuildLog
                            {
                                BuildId = buildId,
                                State = LogState.Info,
                                Message = "No Error or Warning",
                                Time = DateTime.MinValue,
                            });
                        }
                        BuildLogListModel = buildLogListModel;

                        // Variables
                        VariableListModel vModel = new VariableListModel();
                        var vLogs = LogDB.Table<DB_Variable>()
                            .Where(x => x.BuildId == buildId && x.Type != VarsType.Local)
                            .OrderBy(x => x.Type)
                            .ThenBy(x => x.Key);
                        foreach (DB_Variable v in vLogs)
                            vModel.Add(v);
                        VariableListModel = vModel;

                        // Statistics
                        LogStatModel stat = new LogStatModel();
                        var states = ((LogState[])Enum.GetValues(typeof(LogState))).Where(x => x != LogState.None && x != LogState.CriticalError);
                        foreach (LogState state in states)
                        {
                            int count = LogDB
                                .Table<DB_BuildLog>()
                                .Count(x => x.BuildId == buildId && x.State == state);

                            stat.Add(new Tuple<LogState, int>(state, count));
                        }
                        LogStatModel = stat;
                    }
                    else
                    { // Per Script
                        // BuildLog
                        BuildLogListModel buildLogListModel = new BuildLogListModel();
                        foreach (DB_BuildLog b in LogDB.Table<DB_BuildLog>().Where(x => x.BuildId == buildId && x.ScriptId == scriptId))
                            buildLogListModel.Add(b);
                        BuildLogListModel = buildLogListModel;

                        // Variables
                        VariableListModel vModel = new VariableListModel();
                        var vars = LogDB.Table<DB_Variable>()
                            .Where(x => x.BuildId == buildId && x.Type != VarsType.Local)
                            .OrderBy(x => x.Type)
                            .ThenBy(x => x.Key);
                        foreach (DB_Variable v in vars)
                            vModel.Add(v);
                        vars = LogDB.Table<DB_Variable>()
                            .Where(x => x.BuildId == buildId && x.ScriptId == scriptId && x.Type == VarsType.Local)
                            .OrderBy(x => x.Key);
                        foreach (DB_Variable var in vars)
                            vModel.Add(var);
                        VariableListModel = vModel;

                        // Statistics
                        LogStatModel stat = new LogStatModel();
                        var states = ((LogState[])Enum.GetValues(typeof(LogState))).Where(x => x != LogState.None && x != LogState.CriticalError);
                        foreach (LogState state in states)
                        {
                            int count = LogDB
                                .Table<DB_BuildLog>()
                                .Count(x => x.BuildId == buildId && x.ScriptId == scriptId && x.State == state);

                            stat.Add(new Tuple<LogState, int>(state, count));
                        }
                        LogStatModel = stat;
                    }
                }
                else
                {
                    BuildLogListModel = new BuildLogListModel();
                }

                OnPropertyUpdate(nameof(SelectScriptIndex));
            }
        }

        // Script Name, Script Id, Build Id
        private ObservableCollection<Tuple<string, int, int>> _selectScriptEntries = new ObservableCollection<Tuple<string, int, int>>();
        public ObservableCollection<Tuple<string, int, int>> SelectScriptEntries
        {
            get => _selectScriptEntries;
            set
            {
                _selectScriptEntries = value;
                OnPropertyUpdate(nameof(SelectScriptEntries));
            }
        }

        private LogStatModel _logStatModel = new LogStatModel();
        public LogStatModel LogStatModel
        {
            get => _logStatModel;
            set
            {
                _logStatModel = value;
                OnPropertyUpdate(nameof(LogStatModel));
            }
        }

        private BuildLogListModel _buildLogListModel = new BuildLogListModel();
        public BuildLogListModel BuildLogListModel
        {
            get => _buildLogListModel;
            set
            {
                _buildLogListModel = value;
                OnPropertyUpdate(nameof(BuildLogListModel));
            }
        }

        private int _buildLogSimpleSelectedIndex;
        public int BuildLogSimpleSelectedIndex
        {
            get => _buildLogSimpleSelectedIndex;
            set
            {
                _buildLogSimpleSelectedIndex = value;
                OnPropertyUpdate(nameof(BuildLogSimpleSelectedIndex));
            }
        }

        private int _buildLogDetailSelectedIndex;
        public int BuildLogDetailSelectedIndex
        {
            get => _buildLogDetailSelectedIndex;
            set
            {
                _buildLogDetailSelectedIndex = value;
                OnPropertyUpdate(nameof(BuildLogDetailSelectedIndex));
            }
        }

        private VariableListModel _variableListModel = new VariableListModel();
        public VariableListModel VariableListModel
        {
            get => _variableListModel;
            set
            {
                _variableListModel = value;
                OnPropertyUpdate(nameof(VariableListModel));
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

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyUpdate(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
    #endregion
}
