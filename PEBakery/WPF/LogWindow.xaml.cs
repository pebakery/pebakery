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

using PEBakery.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PEBakery.WPF
{
    /// <summary>
    /// LogWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class LogWindow : Window
    {
        public static int Count = 0;

        private LogViewModel m = new LogViewModel();

        public LogWindow(int selectedTabIndex = 0)
        {
            Interlocked.Increment(ref LogWindow.Count);

            InitializeComponent();
            DataContext = m;

            m.SelectedTabIndex = selectedTabIndex;
            m.Logger.SystemLogUpdated += SystemLogUpdateEventHandler;
            m.Logger.BuildInfoUpdated += BuildInfoUpdateEventHandler;
            m.Logger.ScriptUpdated += ScriptUpdateEventHandler;
            m.Logger.BuildLogUpdated += BuildLogUpdateEventHandler;
            m.Logger.VariableUpdated += VariableUpdateEventHandler;

            SystemLogListView.UpdateLayout();
            if (1 < SystemLogListView.Items.Count)
                SystemLogListView.ScrollIntoView(SystemLogListView.Items[SystemLogListView.Items.Count - 1]);
        }

        #region EventHandler
        public void SystemLogUpdateEventHandler(object sender, SystemLogUpdateEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                m.SystemLogListModel.Add(e.Log);
                m.SystemLogListSelectedIndex = m.SystemLogListModel.Count - 1;
                SystemLogListView.UpdateLayout();
                SystemLogListView.ScrollIntoView(SystemLogListView.Items[m.SystemLogListSelectedIndex]);
            });
            m.OnPropertyUpdate("SystemLogListModel");
        }

        public void BuildInfoUpdateEventHandler(object sender, BuildInfoUpdateEventArgs e)
        {
            m.RefreshBuildLog();
        }

        public void BuildLogUpdateEventHandler(object sender, BuildLogUpdateEventArgs e)
        {
            if (m.SelectBuildEntries != null &&
                m.SelectBuildIndex < m.SelectBuildEntries.Count &&
                m.SelectBuildEntries[m.SelectBuildIndex].Item2 == e.Log.BuildId &&
                m.SelectScriptEntries != null &&
                m.SelectScriptIndex < m.SelectScriptEntries.Count &&
                m.SelectScriptEntries[m.SelectScriptIndex].Item2 == e.Log.ScriptId)
            {
                Application.Current.Dispatcher.Invoke(() => 
                {
                    m.BuildLogListModel.Add(e.Log);
                    m.OnPropertyUpdate("BuildLogListModel");

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
            m.RefreshScript(e.Log.BuildId, true);
        }

        public void VariableUpdateEventHandler(object sender, VariableUpdateEventArgs e)
        {
            if (m.SelectBuildEntries != null &&
                0 <= m.SelectBuildIndex && m.SelectBuildIndex < m.SelectBuildEntries.Count &&
                m.SelectBuildEntries[m.SelectBuildIndex].Item2 == e.Log.BuildId)
            {
                if (e.Log.Type != VarsType.Local)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        m.VariableListModel.Add(e.Log);
                        m.OnPropertyUpdate("VariableListModel");
                    });
                }
            }
        }
        #endregion

        #region Window Event
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Close();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            m.Logger.SystemLogUpdated -= SystemLogUpdateEventHandler;
            m.Logger.BuildInfoUpdated -= BuildInfoUpdateEventHandler;
            m.Logger.ScriptUpdated -= ScriptUpdateEventHandler;
            m.Logger.BuildLogUpdated -= BuildLogUpdateEventHandler;
            m.Logger.VariableUpdated -= VariableUpdateEventHandler;

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
                    m.RefreshSystemLog();
                    break;
                case 1: // Build Log
                    m.RefreshBuildLog();
                    break;
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            bool busy = false;
            Application.Current.Dispatcher.Invoke(() =>
            {
                MainWindow w = Application.Current.MainWindow as MainWindow;
                busy = w.Model.WorkInProgress || (0 < Engine.WorkingLock);
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
                    m.LogDB.DeleteAll<DB_SystemLog>();
                    m.RefreshSystemLog();
                    break;
                case 1: // Build Log
                    m.LogDB.DeleteAll<DB_BuildInfo>();
                    m.LogDB.DeleteAll<DB_BuildLog>();
                    m.LogDB.DeleteAll<DB_Script>();
                    m.LogDB.DeleteAll<DB_Variable>();
                    m.RefreshBuildLog();
                    break;
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            string baseDir;
            {
                MainWindow w = Application.Current.MainWindow as MainWindow;
                baseDir = w.BaseDir;
            }

            string title;
            if (m.SelectedTabIndex == 0) // System Log
            {
                title = "Export System Log";
            }
            else // Build Log
            {
                title = "Export Build Log";
            }

            Microsoft.Win32.SaveFileDialog dialog = new Microsoft.Win32.SaveFileDialog()
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

                if (m.SelectedTabIndex == 0) // System Log
                {
                    m.Logger.ExportSystemLog(type, dialog.FileName);
                }
                else // Build Log
                {
                    int idx = m.SelectBuildIndex;
                    long buildId = m.SelectBuildEntries[idx].Item2; // Build Id
                    m.Logger.ExportBuildLog(type, dialog.FileName, buildId);
                }

                // Open log file
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MainWindow w = Application.Current.MainWindow as MainWindow;
                    w.OpenTextFile(dialog.FileName);
                });
            }
        }
        #endregion  
    }

    #region LogListModel
    public class LogStatModel : ObservableCollection<Tuple<LogState, int>> { }
    public class SystemLogListModel : ObservableCollection<DB_SystemLog> { }
    public class ScriptListModel : ObservableCollection<DB_Script> { }
    public class VariableListModel : ObservableCollection<DB_Variable> { }
    public class BuildLogListModel : ObservableCollection<DB_BuildLog> { }
    #endregion

    #region LogViewModel
    public class LogViewModel : INotifyPropertyChanged
    {
        public Logger Logger { get; set; }
        public LogDB LogDB { get => Logger.DB; }

        public LogViewModel()
        {
            MainWindow w = Application.Current.MainWindow as MainWindow;
            Logger = w.Logger;

            RefreshSystemLog();
            RefreshBuildLog();
        }

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
                    SelectBuildEntries.Add(new Tuple<string, long>($"[{timeStr}] {b.Name} ({b.Id})", b.Id));
                }
                SelectBuildIndex = 0;
            });
        }

        public void RefreshScript(long? buildId, bool showLastScript)
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
                    SelectScriptEntries.Add(new Tuple<string, long, long>("Total Summary", -1, (long)buildId));
                    var scripts = LogDB.Table<DB_Script>().Where(x => x.BuildId == buildId).OrderBy(x => x.Order).ToArray();
                    foreach (DB_Script p in scripts)
                    {
                        SelectScriptEntries.Add(new Tuple<string, long, long>($"[{p.Order}/{scripts.Length}] {p.Name} ({p.Path})", p.Id, (long)buildId));
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
        private int selectedTabIndex;
        public int SelectedTabIndex
        {
            get => selectedTabIndex;
            set
            {
                selectedTabIndex = value;
                OnPropertyUpdate("SelectedTabIndex");
            }
        }
        #endregion

        #region SystemLog
        private int systemLogListSelectedIndex;
        public int SystemLogListSelectedIndex
        {
            get => systemLogListSelectedIndex;
            set
            {
                systemLogListSelectedIndex = value;
                OnPropertyUpdate("SystemLogListSelectedIndex");
            }
        }

        private SystemLogListModel systemLogListModel = new SystemLogListModel();
        public SystemLogListModel SystemLogListModel
        {
            get => systemLogListModel;
            set
            {
                systemLogListModel = value;
                OnPropertyUpdate("SystemLogListModel");
            }
        }
        #endregion

        #region BuildLog
        private int selectBuildIndex;
        public int SelectBuildIndex
        {
            get => selectBuildIndex;
            set
            {
                selectBuildIndex = value;

                if (0 < selectBuildEntries.Count)
                {
                    long buildId = selectBuildEntries[value].Item2;

                    RefreshScript(SelectBuildEntries[value].Item2, false);
                }
                else
                {
                    RefreshScript(null, false);
                }

                OnPropertyUpdate("SelectBuildIndex");
            }
        }

        private ObservableCollection<Tuple<string, long>> selectBuildEntries = new ObservableCollection<Tuple<string, long>>();
        public ObservableCollection<Tuple<string, long>> SelectBuildEntries
        {
            get => selectBuildEntries;
            set
            {
                selectBuildEntries = value;
                OnPropertyUpdate("SelectBuildEntries");
            }
        }

        private int selectScriptIndex;
        public int SelectScriptIndex
        {
            get => selectScriptIndex;
            set
            {
                selectScriptIndex = value;
                if (value != -1 && 0 < selectScriptEntries.Count)
                {
                    long scriptId = selectScriptEntries[value].Item2;
                    long buildId = selectScriptEntries[value].Item3;

                    if (scriptId == -1)
                    { // Summary
                        // BuildLog
                        BuildLogListModel buildLogListModel = new BuildLogListModel();
                        foreach (LogState state in new LogState[] { LogState.Error, LogState.Warning })
                        {
                            var bLogs = LogDB.Table<DB_BuildLog>()
                           .Where(x => x.BuildId == buildId && x.State == state);
                            foreach (DB_BuildLog b in bLogs)
                                buildLogListModel.Add(b);
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
                            int count = LogDB.Table<DB_BuildLog>()
                                .Where(x => x.BuildId == buildId && x.State == state)
                                .Count();

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
                            int count = LogDB.Table<DB_BuildLog>()
                                .Where(x => x.BuildId == buildId && x.ScriptId == scriptId && x.State == state)
                                .Count();

                            stat.Add(new Tuple<LogState, int>(state, count));
                        }
                        LogStatModel = stat;
                    }
                }
                else
                {
                    BuildLogListModel = new BuildLogListModel();
                }

                OnPropertyUpdate("SelectScriptIndex");
            }
        }

        // Script Name, Script Id, Build Id
        private ObservableCollection<Tuple<string, long, long>> selectScriptEntries = new ObservableCollection<Tuple<string, long, long>>();
        public ObservableCollection<Tuple<string, long, long>> SelectScriptEntries
        {
            get => selectScriptEntries;
            set
            {
                selectScriptEntries = value;
                OnPropertyUpdate("SelectScriptEntries");
            }
        }

        private LogStatModel logStatModel = new LogStatModel();
        public LogStatModel LogStatModel
        {
            get => logStatModel;
            set
            {
                logStatModel = value;
                OnPropertyUpdate("LogStatModel");
            }
        }

        private BuildLogListModel buildLogListModel = new BuildLogListModel();
        public BuildLogListModel BuildLogListModel
        {
            get => buildLogListModel;
            set
            {
                buildLogListModel = value;
                OnPropertyUpdate("BuildLogListModel");
            }
        }

        private int buildLogSimpleSelectedIndex;
        public int BuildLogSimpleSelectedIndex
        {
            get => buildLogSimpleSelectedIndex;
            set
            {
                buildLogSimpleSelectedIndex = value;
                OnPropertyUpdate("BuildLogListSimpleSelectedIndex");
            }
        }

        private int buildLogDetailSelectedIndex;
        public int BuildLogDetailSelectedIndex
        {
            get => buildLogDetailSelectedIndex;
            set
            {
                buildLogDetailSelectedIndex = value;
                OnPropertyUpdate("BuildLogListDetailSelectedIndex");
            }
        }

        private VariableListModel variableListModel = new VariableListModel();
        public VariableListModel VariableListModel
        {
            get => variableListModel;
            set
            {
                variableListModel = value;
                OnPropertyUpdate("VariableListModel");
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
