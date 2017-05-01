using PEBakery.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
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
        private LogViewModel model;

        public LogWindow(Logger logger)
        {
            this.model = new LogViewModel(logger);
            this.DataContext = model;

            model.Logger.SystemLogUpdated += SystemLogUpdateEventHandler;
            model.Logger.BuildInfoUpdated += BuildInfoUpdateEventHandler;
            model.Logger.BuildLogUpdated += BuildLogUpdateEventHandler;
            model.Logger.PluginUpdated += PluginUpdateEventHandler;
            model.Logger.VariableUpdated += VariableUpdateEventHandler;

            InitializeComponent();
        }

       ~LogWindow()
        {
            model.Logger.SystemLogUpdated -= SystemLogUpdateEventHandler;
            model.Logger.BuildInfoUpdated -= BuildInfoUpdateEventHandler;
            model.Logger.BuildLogUpdated -= BuildLogUpdateEventHandler;
            model.Logger.PluginUpdated -= PluginUpdateEventHandler;
            model.Logger.VariableUpdated -= VariableUpdateEventHandler;
        }

        #region EventHandler
        public void SystemLogUpdateEventHandler(object sender, SystemLogUpdateEventArgs e)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                model.SystemLogListModel.Add(e.Log);
                if (model.SystemLogListSelectedIndex + 2 == model.SystemLogListModel.Count)
                {
                    model.SystemLogListSelectedIndex += 1;
                    SystemLogListView.UpdateLayout();
                    SystemLogListView.ScrollIntoView(SystemLogListView.Items[model.SystemLogListSelectedIndex]);
                }
            });
            model.OnPropertyUpdate("SystemLogListModel");
        }

        public void BuildInfoUpdateEventHandler(object sender, BuildInfoUpdateEventArgs e)
        {
            model.RefreshBuildLog();
        }

        public void BuildLogUpdateEventHandler(object sender, BuildLogUpdateEventArgs e)
        {
            if (model.SelectBuildIdEntries != null)
            {
                int idx = model.SelectBuildIdEntries.IndexOf(e.Log.BuildId);
                if (idx != -1)
                {
                    App.Current.Dispatcher.Invoke(() => { model.BuildLogListModel.Add(e.Log); });
                    model.OnPropertyUpdate("BuildLogListModel");
                }
            }
        }

        public void PluginUpdateEventHandler(object sender, PluginUpdateEventArgs e)
        {
            // PluginListModel.Add(e.Log);
            // OnPropertyUpdate("SystemLogListModel");
        }

        public void VariableUpdateEventHandler(object sender, VariableUpdateEventArgs e)
        {
            // VariableListModel.Add(e.Log);
            // OnPropertyUpdate("SystemLogListModel");
        }
        #endregion

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Close();
        }

        private void SelectBuildComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox box = sender as ComboBox;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            int idx = MainTab.SelectedIndex;
            switch (idx)
            {
                case 0: // System Log 
                    model.RefreshSystemLog();
                    break;
                case 1: // Build Log
                    model.RefreshBuildLog();
                    break;
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            int idx = MainTab.SelectedIndex;
            switch (idx)
            {
                case 0: // System Log 
                    model.LogDB.DeleteAll<DB_SystemLog>();
                    model.RefreshSystemLog();
                    break;
                case 1: // Build Log
                    model.LogDB.DeleteAll<DB_BuildInfo>();
                    model.LogDB.DeleteAll<DB_BuildLog>();
                    model.LogDB.DeleteAll<DB_Plugin>();
                    model.LogDB.DeleteAll<DB_Variable>();
                    model.RefreshBuildLog();
                    break;
            }
        }
    }

    #region LogListModel
    public class SystemLogListModel : ObservableCollection<DB_SystemLog> { }
    public class PluginListModel : ObservableCollection<DB_Plugin> { }
    public class VariableListModel : ObservableCollection<DB_Variable> { }
    public class BuildLogListModel : ObservableCollection<DB_BuildLog> { }
    #endregion

    #region LogViewModel
    public class LogViewModel : INotifyPropertyChanged
    {
        public Logger Logger { get; set; }
        public LogDB LogDB { get => Logger.DB; }

        public LogViewModel(Logger logger)
        {
            Logger = logger;

            

            RefreshSystemLog();
            RefreshBuildLog();
        }

        ~LogViewModel()
        {
            
        }

        

        #region Refresh 
        public void RefreshSystemLog()
        {
            SystemLogListModel list = new SystemLogListModel();
            foreach (DB_SystemLog log in LogDB.Table<DB_SystemLog>())
                list.Add(log);
            SystemLogListModel = list;

            SystemLogListSelectedIndex = SystemLogListModel.Count - 1;
        }

        public void RefreshBuildLog()
        {
            // Populate SelectBuildEntries
            List<long> idList = new List<long>();
            List<string> nameList = new List<string>();
            foreach (DB_BuildInfo b in LogDB.Table<DB_BuildInfo>().OrderByDescending(x => x.StartTime))
            {
                string timeStr = b.StartTime.ToLocalTime().ToString("yyyy-MM-dd hh:mm:ss tt", CultureInfo.InvariantCulture);
                nameList.Add($"[{timeStr}] {b.Name} ({b.Id})");
                idList.Add(b.Id);
            }
            // Keep order!
            SelectBuildIdEntries = idList;
            SelectBuildEntries = nameList;

            SelectBuildIndex = 0;
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

                if (0 < selectBuildIdEntries.Count)
                {
                    long idx = selectBuildIdEntries[value];

                    BuildLogListModel buildLogListModel = new BuildLogListModel();
                    foreach (DB_BuildLog b in LogDB.Table<DB_BuildLog>().Where(x => x.BuildId == idx))
                        buildLogListModel.Add(b);
                    BuildLogListModel = buildLogListModel;
                }
                else
                {
                    BuildLogListModel = new BuildLogListModel();
                }

                OnPropertyUpdate("SelectBuildIndex");
            }
        }

        private List<string> selectBuildEntries;
        public List<string> SelectBuildEntries
        {
            get => selectBuildEntries;
            set
            {
                selectBuildEntries = value;
                OnPropertyUpdate("SelectBuildEntries");
            }
        }

        private List<long> selectBuildIdEntries;
        public List<long> SelectBuildIdEntries
        {
            get => selectBuildIdEntries;
            set => selectBuildIdEntries = value;
        }

        private BuildLogListModel buildLogListModel;
        public BuildLogListModel BuildLogListModel
        {
            get => buildLogListModel;
            set
            {
                buildLogListModel = value;
                OnPropertyUpdate("BuildLogListModel");
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
