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

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
using PEBakery.Core;

namespace PEBakery.WPF
{
    [SuppressMessage("ReSharper", "RedundantExtendsListEntry")]
    public partial class LogExportWindow : Window
    {
        #region Field and Constructor
        private readonly LogExportModel _m;

        public LogExportWindow(LogExportModel model)
        {
            DataContext = _m = model;
            InitializeComponent();
        }
        #endregion

        #region Commands
        private void ExportCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (_m.ExportSystemLog)
            {
                e.CanExecute = true;
            }
            else if (_m.ExportBuildLog)
            {
                if (0 < _m.BuildEntries.Count)
                    e.CanExecute = true;
            }
            else
            {
                e.CanExecute = false;
            }
        }

        private void ExportCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Choose Destination Path",
                Filter = "Text Format (*.txt)|*.txt|HTML Format (*.html)|*.html",
            };

            string destFile = null;
            if (_m.ExportSystemLog)
            {
                if (dialog.ShowDialog() == true)
                {
                    destFile = dialog.FileName;
                    string ext = System.IO.Path.GetExtension(destFile);
                    LogExportType type = LogExportType.Text;
                    if (ext.Equals(".html", StringComparison.OrdinalIgnoreCase))
                        type = LogExportType.Html;

                    _m.Logger.ExportSystemLog(type, destFile);
                }
            }
            else if (_m.ExportBuildLog)
            {
                if (dialog.ShowDialog() == true)
                {
                    destFile = dialog.FileName;
                    string ext = System.IO.Path.GetExtension(destFile);
                    LogExportType type = LogExportType.Text;
                    if (ext.Equals(".html", StringComparison.OrdinalIgnoreCase))
                        type = LogExportType.Html;

                    Debug.Assert(0 < _m.BuildEntries.Count, "Internal Logic Error at LogExportWindow.ExportCommand_Executed");
                    int buildId = _m.BuildEntries[_m.SelectedBuildEntryIndex].Item2;
                    _m.Logger.ExportBuildLog(type, destFile, buildId);
                }
            }
            else
            {
                const string errMsg = "Internal Logic Error at LogExportWindow.ExportCommand_Executed";
                _m.Logger.SystemWrite(new LogInfo(LogState.Error, errMsg));
                MessageBox.Show(errMsg, "Internal Logic Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Open log file
            if (destFile != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (!(Application.Current.MainWindow is MainWindow w))
                        return;
                    w.OpenTextFile(destFile);
                });
            }

            // Close LogExportWindow
            Close();
        }
        #endregion
    }

    #region LogExportModel
    public class LogExportModel : INotifyPropertyChanged
    {
        #region Constructor, SetSystemLog, SetBuildLog
        public LogExportModel(Logger logger, IEnumerable<Tuple<string, int>> buildEntries)
        {
            Logger = logger;
            BuildEntries = new ObservableCollection<Tuple<string, int>>(buildEntries);
        }

        public void SetSystemLog()
        {
            ExportSystemLog = true;
            ExportBuildLog = false;
        }

        public void SetBuildLog(int favoredBuildEntryIndex, bool includeComments, bool includeMacros)
        {
            if (BuildEntries.Count == 0)
            { // No build logs -> Select SystemLog instead
                SetSystemLog();
                return;
            }

            ExportSystemLog = false;
            ExportBuildLog = true;

            if (0 <= favoredBuildEntryIndex && favoredBuildEntryIndex < BuildEntries.Count)
                SelectedBuildEntryIndex = favoredBuildEntryIndex;

            BuildLogIncludeComments = includeComments;
            BuildLogIncludeMacros = includeMacros;
        }
        #endregion

        #region Logger
        public Logger Logger { get; set; }
        #endregion

        #region Log Type
        private bool _exportSystemLog;
        public bool ExportSystemLog
        {
            get => _exportSystemLog;
            set
            {
                _exportSystemLog = value;
                OnPropertyUpdate(nameof(ExportSystemLog));
            }
        }

        private bool _exportBuildLog;
        public bool ExportBuildLog
        {
            get => _exportBuildLog;
            set
            {
                _exportBuildLog = value;
                OnPropertyUpdate(nameof(ExportBuildLog));
            }
        }
        #endregion

        #region IsEnabled
        public bool BuildLogRadioEnabled => 0 < BuildEntries.Count;
        public bool BuildLogOptionEnabled => ExportBuildLog && !ExportSystemLog && 0 < BuildEntries.Count;
        #endregion

        #region Build Log Export
        private ObservableCollection<Tuple<string, int>> _buildEntries = new ObservableCollection<Tuple<string, int>>();
        public ObservableCollection<Tuple<string, int>> BuildEntries
        {
            get => _buildEntries;
            set
            {
                _buildEntries = value;
                OnPropertyUpdate(nameof(BuildEntries));
            }
        }

        private int _selectedBuildEntryIndex;
        public int SelectedBuildEntryIndex
        {
            get => _selectedBuildEntryIndex;
            set
            {
                _selectedBuildEntryIndex = value;
                OnPropertyUpdate(nameof(SelectedBuildEntryIndex));
            }
        }

        private bool _buildLogIncludeComments = true;
        public bool BuildLogIncludeComments
        {
            get => _buildLogIncludeComments;
            set
            {
                _buildLogIncludeComments = value;
                OnPropertyUpdate(nameof(BuildLogIncludeComments));
            }
        }

        private bool _buildLogIncludeMacros = true;
        public bool BuildLogIncludeMacros
        {
            get => _buildLogIncludeMacros;
            set
            {
                _buildLogIncludeMacros = value;
                OnPropertyUpdate(nameof(BuildLogIncludeMacros));
            }
        }
        #endregion

        #region Utility
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyUpdate(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
    #endregion

    #region LogExportCommands
    public static class LogExportCommands
    {
        public static readonly RoutedUICommand Export = new RoutedUICommand("Export", "Export", typeof(LogExportCommands));
    }
    #endregion
}
