using PEBakery.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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

        public LogWindow(LogDB logDB)
        {
            this.model = new LogViewModel(logDB);
            this.DataContext = model;
            InitializeComponent();
            

        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Close();
        }
    }

    #region LogListModel
    public class SystemLogListModel : ObservableCollection<DB_SystemLog> { }
    #endregion

    #region LogViewModel
    public class LogViewModel : INotifyPropertyChanged
    {
        private LogDB logDB;
        public LogDB LogDB { get => logDB;  set => logDB = value; }

        public LogViewModel(LogDB logDB)
        {
            this.logDB = logDB;

            // Populate SelectBuildEntries
            List<string> buildList = new List<string>();
            foreach (DB_Build b in logDB.Table<DB_Build>())
                buildList.Add($"[{b.Id}] {b.Name} ({b.StartTime:yyyy-MM-dd HH-mm-ss zzz})");
            SelectBuildEntries = buildList;

            this.systemLogListModel = new SystemLogListModel();
            var systemLogList = logDB.Table<DB_SystemLog>();
            foreach (DB_SystemLog log in systemLogList)
                systemLogListModel.Add(log);
        }

        #region SystemLog
        private SystemLogListModel systemLogListModel;
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

        #region Build
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
