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
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using PEBakery.Core;
using PEBakery.Helper;

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
            if (_m.InProgress)
            {
                e.CanExecute = false;
            }
            else if (_m.ExportSystemLog)
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

        private async void ExportCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Choose Destination Path",
            };

            switch (_m.FileFormat)
            {
                case LogExportType.Text:
                    dialog.Filter = "Text Format (*.txt)|*.txt";
                    break;
                case LogExportType.Html:
                    dialog.Filter = "HTML Format (*.html)|*.html";
                    break;
                default:
                    Debug.Assert(false, "Internal Logic Error at LogExportWindow.ExportCommand_Executed");
                    break;
            }

            bool? result = dialog.ShowDialog();
            // If user cancelled SaveDialog, do nothing
            if (result != true)
                return;
            string destFile = dialog.FileName;

            _m.InProgress = true;
            try
            {
                await Task.Run(() =>
                {
                    if (_m.ExportSystemLog)
                    {
                        _m.Logger.ExportSystemLog(_m.FileFormat, destFile);
                    }
                    else if (_m.ExportBuildLog)
                    {
                        Debug.Assert(0 < _m.BuildEntries.Count, "Internal Logic Error at LogExportWindow.ExportCommand_Executed");
                        int buildId = _m.BuildEntries[_m.SelectedBuildEntryIndex].Item2;
                        _m.Logger.ExportBuildLog(_m.FileFormat, destFile, buildId, new LogExporter.BuildLogOptions
                        {
                            IncludeComments = _m.BuildLogIncludeComments,
                            IncludeMacros = _m.BuildLogIncludeMacros,
                        });
                    }
                });
            }
            finally
            {
                _m.InProgress = false;
            }

            // Open log file
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (!(Application.Current.MainWindow is MainWindow w))
                    return;

                if (_m.FileFormat == LogExportType.Html)
                {
                    // open .html files with the default browser
                    FileHelper.OpenUri(destFile);
                }
                else
                {
                    MainViewModel.OpenTextFile(destFile);
                }
            });

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
                OnPropertyUpdate(nameof(BuildLogOptionEnabled));
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
                OnPropertyUpdate(nameof(BuildLogOptionEnabled));
            }
        }
        #endregion

        #region File Format
        public LogExportType FileFormat = LogExportType.Text;

        public bool FileFormatText
        {
            get => FileFormat == LogExportType.Text;
            set
            {
                FileFormat = value ? LogExportType.Text : LogExportType.Html;
                OnPropertyUpdate(nameof(FileFormatText));
            }
        }

        public bool FileFormatHtml
        {
            get => FileFormat == LogExportType.Html;
            set
            {
                FileFormat = value ? LogExportType.Html : LogExportType.Text;
                OnPropertyUpdate(nameof(FileFormatHtml));
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
                OnPropertyUpdate(nameof(BuildLogRadioEnabled));
                OnPropertyUpdate(nameof(BuildLogOptionEnabled));
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

        #region Progress
        private bool _inProgress = false;
        public bool InProgress
        {
            get => _inProgress;
            set
            {
                _inProgress = value;
                OnPropertyUpdate(nameof(InProgress));
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

    #region Converter
    public class InvertBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || value.GetType() != typeof(bool))
                return null;

            return !(bool)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || value.GetType() != typeof(bool))
                return null;

            return !(bool)value;
        }
    }
    #endregion
}
