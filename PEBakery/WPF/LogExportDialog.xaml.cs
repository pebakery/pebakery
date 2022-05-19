/*
    Copyright (C) 2018-2022 Hajin Jang
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
using PEBakery.Helper;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace PEBakery.WPF
{
    public partial class LogExportDialog : Window
    {
        #region Field and Constructor
        private readonly LogExportModel _m;

        public LogExportDialog(LogExportModel model)
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
            else if (_m.LogExportKind == LogExportKind.System)
            {
                e.CanExecute = true;
            }
            else if (_m.LogExportKind == LogExportKind.Build)
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
                Title = "Save Log As",
            };

            switch (_m.ExportFileFormat)
            {
                case LogExportFormat.Text:
                    dialog.Filter = "Text Format (*.txt)|*.txt";
                    break;
                case LogExportFormat.Html:
                    dialog.Filter = "HTML Format (*.html)|*.html";
                    break;
                default:
                    Debug.Assert(false, "Internal Logic Error at LogExportWindow.ExportCommand_Executed");
                    break;
            }

            if (_m.LogExportKind == LogExportKind.System)
            {
                // Local time should be ok because the filename is likely only useful to the person exporting the log
                DateTime localTime = DateTime.Now;
                dialog.FileName = $"SystemLog_{localTime.ToString("yyyy_MM_dd_HHmmss", CultureInfo.InvariantCulture)}";
            }
            else if (_m.LogExportKind == LogExportKind.Build)
            {
                Debug.Assert(0 < _m.BuildEntries.Count, "Internal Logic Error at LogExportWindow.ExportCommand_Executed");
                LogModel.BuildInfo bi = _m.BuildEntries[_m.SelectedBuildEntryIndex];

                // Filter invalid filename chars
                List<char> filteredChars = new List<char>(bi.Name.Length);
                List<char> invalidChars = Path.GetInvalidFileNameChars().ToList();
                invalidChars.Add('['); // Remove [ and ]
                invalidChars.Add(']');
                invalidChars.Add(' '); // Spaces are not very script or web friendly, let's remove them too.
                foreach (char ch in bi.Name)
                {
                    if (invalidChars.Contains(ch))
                    {
                        filteredChars.Add(Convert.ToChar("_")); // Replace invalid chars and spaces with an underscore
                    }
                    else
                    {
                        filteredChars.Add(ch);
                    }
                }
                string filteredName = new string(filteredChars.ToArray());
                // The log stores dateTime as UTC so its safe to use ToLocalTime() to convert to the users timezone
                dialog.FileName = $"BuildLog_{bi.StartTime.ToLocalTime().ToString("yyyy_MM_dd_HHmmss", CultureInfo.InvariantCulture)}_{filteredName}";
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
                    if (_m.LogExportKind == LogExportKind.System)
                    {
                        _m.Logger.ExportSystemLog(_m.ExportFileFormat, destFile);
                    }
                    else if (_m.LogExportKind == LogExportKind.Build)
                    {
                        Debug.Assert(0 < _m.BuildEntries.Count, "Internal Logic Error at LogExportWindow.ExportCommand_Executed");
                        int buildId = _m.BuildEntries[_m.SelectedBuildEntryIndex].Id;
                        _m.Logger.ExportBuildLog(_m.ExportFileFormat, destFile, buildId, new BuildLogOptions
                        {
                            IncludeComments = _m.BuildLogIncludeComments,
                            IncludeMacros = _m.BuildLogIncludeMacros,
                            ShowLogFlags = _m.BuildLogShowLogFlags,
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
                if (_m.ExportFileFormat == LogExportFormat.Html)
                {
                    // Call FileHelper.OpenUri (instead of OpenPath) to open .html files with the default browser.
                    ResultReport result = FileHelper.OpenUri(destFile);
                    if (!result.Success)
                    {
                        MessageBox.Show(this, $"URL [{destFile}] could not be opened.\r\n\r\n{result.Message}.",
                            "Error Opening URL", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MainViewModel.OpenTextFile(destFile);
                }
            });

            // Write user preferences to settings
            Global.Setting.LogViewer.LogExportFileFormat = _m.ExportFileFormat;
            Global.Setting.LogViewer.LogExportBuildIncludeComments = _m.BuildLogIncludeComments;
            Global.Setting.LogViewer.LogExportBuildIncludeMacros = _m.BuildLogIncludeMacros;
            Global.Setting.LogViewer.LogExportBuildShowLogFlags = _m.BuildLogShowLogFlags;
            Global.Setting.WriteToFile();

            // Close LogExportWindow
            Close();
        }
        #endregion
    }

    #region LogExportModel
    public class LogExportModel : ViewModelBase
    {
        #region Constructor, SetSystemLog, SetBuildLog
        public LogExportModel(Logger logger, IReadOnlyList<LogModel.BuildInfo> buildEntries)
        {
            Logger = logger;
            BuildEntries = new ObservableCollection<LogModel.BuildInfo>(buildEntries);

            ExportFileFormat = Global.Setting.LogViewer.LogExportFileFormat;
            BuildLogIncludeComments = Global.Setting.LogViewer.LogExportBuildIncludeComments;
            BuildLogIncludeMacros = Global.Setting.LogViewer.LogExportBuildIncludeMacros;
            BuildLogShowLogFlags = Global.Setting.LogViewer.LogExportBuildShowLogFlags;
        }

        public void SetSystemLog()
        {
            LogExportKind = LogExportKind.System;
        }

        public void SetBuildLog(int favoredBuildEntryIndex, bool includeComments, bool includeMacros)
        {
            if (BuildEntries.Count == 0)
            { // No build logs -> Select SystemLog instead
                SetSystemLog();
                return;
            }

            LogExportKind = LogExportKind.Build;

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
        private LogExportKind _logExportKind;
        public LogExportKind LogExportKind
        {
            get => _logExportKind;
            set
            {
                SetProperty(ref _logExportKind, value);
                OnPropertyUpdate(nameof(BuildLogOptionEnabled));
            }
        }
        #endregion

        #region File Format

        private LogExportFormat _logFileFormat = LogExportFormat.Text;
        public LogExportFormat ExportFileFormat
        {
            get => _logFileFormat;
            set => SetProperty(ref _logFileFormat, value);
        }
        #endregion

        #region IsEnabled
        public bool BuildLogRadioEnabled => 0 < BuildEntries.Count;
        public bool BuildLogOptionEnabled => LogExportKind == LogExportKind.Build && 0 < BuildEntries.Count;
        #endregion

        #region Build Log Export
        private ObservableCollection<LogModel.BuildInfo> _buildEntries = new ObservableCollection<LogModel.BuildInfo>();
        public ObservableCollection<LogModel.BuildInfo> BuildEntries
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
            set => SetProperty(ref _buildLogIncludeComments, value);
        }

        private bool _buildLogIncludeMacros = true;
        public bool BuildLogIncludeMacros
        {
            get => _buildLogIncludeMacros;
            set => SetProperty(ref _buildLogIncludeMacros, value);
        }

        private bool _buildLogShowLogFlags = true;
        public bool BuildLogShowLogFlags
        {
            get => _buildLogShowLogFlags;
            set => SetProperty(ref _buildLogShowLogFlags, value);
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
                return Binding.DoNothing;

            return !(bool)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || value.GetType() != typeof(bool))
                return Binding.DoNothing;

            return !(bool)value;
        }
    }
    #endregion
}
