/*
    Copyright (C) 2016-2019 Hajin Jang
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

using Ookii.Dialogs.Wpf;
using PEBakery.Core;
using PEBakery.Core.ViewModels;
using PEBakery.Helper;
using PEBakery.Ini;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace PEBakery.WPF
{
    #region SettingWindow
    // ReSharper disable once RedundantExtendsListEntry
    public partial class SettingWindow : Window
    {
        #region Fields and Constructor
        private readonly SettingViewModel _m;

        public SettingWindow(SettingViewModel model)
        {
            DataContext = _m = model;
            InitializeComponent();
        }
        #endregion

        #region Window Event Handler
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _m.UpdateCacheDbState();
            _m.UpdateLogDbState();
            _m.LoadProjectEntries();

            // Calculate proper state for compat option toggle button
            _m.GetCompatToggleNextState();
        }
        #endregion

        #region Commands
        #region Global Button Commands
        private void Command_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _m != null && _m.CanExecuteCommand;
        }

        private void DefaultSettingCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            _m.CanExecuteCommand = false;
            try
            {
                const string msg = "All settings will be reset to default!\r\nDo you really want to continue?";
                MessageBoxResult res = MessageBox.Show(msg, "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (res == MessageBoxResult.Yes)
                {
                    _m.SetToDefault();
                }
            }
            finally
            {
                _m.CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private async void SaveSettingCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            _m.CanExecuteCommand = false;
            try
            {
                await Task.Run(() =>
                {
                    _m.WriteToSetting();
                    _m.WriteToFile();

                    _m.Setting.ApplySetting();
                });
                DialogResult = true;
            }
            finally
            {
                _m.CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }
        #endregion

        #region Project Setting Commands
        private void SelectSourceDirCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            _m.CanExecuteCommand = false;
            try
            {
                VistaFolderBrowserDialog dialog = new VistaFolderBrowserDialog();
                if (0 < _m.ProjectSourceDirs.Count)
                    dialog.SelectedPath = _m.ProjectSourceDirs[_m.ProjectSourceDirIndex];

                if (dialog.ShowDialog(this) == true)
                {
                    bool exist = false;
                    for (int i = 0; i < _m.ProjectSourceDirs.Count; i++)
                    {
                        string projectName = _m.ProjectSourceDirs[i];
                        if (projectName.Equals(dialog.SelectedPath, StringComparison.OrdinalIgnoreCase))
                        { // Selected Path exists
                            _m.ProjectSourceDirIndex = i;
                            exist = true;
                            break;
                        }
                    }

                    if (!exist) // Add to list
                    {
                        _m.ProjectSourceDirs.Insert(0, dialog.SelectedPath);
                        _m.ProjectSourceDirIndex = 0;
                    }
                }
            }
            finally
            {
                _m.CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void ResetSourceDirCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            _m.CanExecuteCommand = false;
            try
            {
                _m.ProjectSourceDirs.Clear();

                int idx = _m.SelectedProjectIndex;
                string fullPath = _m.Projects[idx].MainScript.RealPath;
                IniReadWriter.WriteKey(fullPath, "Main", "SourceDir", string.Empty);
            }
            finally
            {
                _m.CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void SelectTargetDirCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            _m.CanExecuteCommand = false;
            try
            {
                VistaFolderBrowserDialog dialog = new VistaFolderBrowserDialog
                {
                    SelectedPath = _m.ProjectTargetDir,
                };

                if (dialog.ShowDialog(this) == true)
                {
                    _m.ProjectTargetDir = dialog.SelectedPath;
                }
            }
            finally
            {
                _m.CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void SelectIsoFileCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            _m.CanExecuteCommand = false;
            try
            {
                Microsoft.Win32.SaveFileDialog dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "ISO File (*.iso)|*.iso",
                    FileName = _m.ProjectIsoFile,
                };

                if (dialog.ShowDialog(this) == true)
                {
                    _m.ProjectIsoFile = dialog.FileName;
                }
            }
            finally
            {
                _m.CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }
        #endregion

        #region General Setting Commands
        private void EnableLongFilePathCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (!_m.GeneralEnableLongFilePath)
                return;

            _m.CanExecuteCommand = false;
            try
            {
                const string msg = "Enabling this option may cause problems!\r\nDo you really want to continue?";
                MessageBoxResult res = MessageBox.Show(msg, "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                _m.GeneralEnableLongFilePath = res == MessageBoxResult.Yes;
            }
            finally
            {
                _m.CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }
        #endregion

        #region Interface Setting Commands
        private void ResetScaleFactorCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            _m.CanExecuteCommand = false;
            try
            {
                _m.InterfaceScaleFactor = 100;
            }
            finally
            {
                _m.CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void SelectMonospacedFontCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            _m.CanExecuteCommand = false;
            try
            {
                _m.InterfaceMonospacedFont = FontHelper.ChooseFontDialog(_m.Setting.Interface.MonospacedFont, this, monospaced: true);
            }
            finally
            {
                _m.CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void SelectCustomEditorPathCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _m != null && _m.CanExecuteCommand && _m.Setting.Interface.UseCustomEditor;
        }

        private void SelectCustomEditorPathCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            _m.CanExecuteCommand = false;
            try
            {
                Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Executable|*.exe",
                    FileName = _m.Setting.Interface.CustomEditorPath,
                };

                if (dialog.ShowDialog(this) == true)
                {
                    _m.InterfaceCustomEditorPath = dialog.FileName;
                }
            }
            finally
            {
                _m.CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }
        #endregion

        #region Script Setting Commands
        private void ClearCacheDatabaseCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _m != null && _m.CanExecuteCommand && ScriptCache.DbLock == 0;
        }

        private async void ClearCacheDatabaseCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (ScriptCache.DbLock != 0)
                return;

            Interlocked.Increment(ref ScriptCache.DbLock);
            try
            {
                await Task.Run(() => { _m.ClearCacheDatabase(); });
            }
            finally
            {
                Interlocked.Decrement(ref ScriptCache.DbLock);
            }
        }
        #endregion

        #region Theme Setting Commands
        private void SelectThemeCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            _m.CanExecuteCommand = false;
            try
            {
                Setting.ThemeType newTheme = (Setting.ThemeType)e.Parameter;
                Debug.Assert(Enum.IsDefined(typeof(Setting.ThemeType), newTheme), "Check SettingWindow.xaml's theme tab.");

                _m.ThemeType = newTheme;
            }
            finally
            {
                _m.CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }
        #endregion

        #region Log Setting Commands
        private async void ClearLogDatabaseCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Interlocked.Increment(ref ScriptCache.DbLock);
            try
            {
                await Task.Run(() => { _m.ClearLogDatabase(); });
            }
            finally
            {
                Interlocked.Decrement(ref ScriptCache.DbLock);
            }
        }
        #endregion

        #region Compat Options Commands
        private void ToggleCompatOptionsCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            _m.CanExecuteCommand = false;
            try
            {
                _m.ApplyCompatToggleNextState(_m.ToggleNextState);
            }
            finally
            {
                _m.CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        #endregion
        #endregion
    }
    #endregion

    #region SettingViewModel
    public class SettingViewModel : ViewModelBase
    {
        #region Field and Constructor
        public Setting Setting { get; }
        public ProjectCollection Projects { get; }
        private bool _firstLoad = true;

        public SettingViewModel(Setting setting, ProjectCollection projects)
        {
            Setting = setting;
            Projects = projects;

            ProjectNames = new ObservableCollection<string>(Projects.Select(p => p.ProjectName));
            ProjectSourceDirs = new ObservableCollection<string>();

            ReadFromSetting();
        }
        #endregion

        #region CanExecuteCommand
        public bool CanExecuteCommand { get; set; } = true;
        #endregion

        #region Need Flags
        public bool NeedProjectRefresh { get; private set; }
        public bool NeedScriptRedraw { get; private set; }
        public bool NeedScriptCaching { get; private set; }
        #endregion

        #region Property - Project
        public ObservableCollection<string> ProjectNames { get; private set; }

        private int _projectDefaultIndex;
        public int DefaultProjectIndex
        {
            get => _projectDefaultIndex;
            set
            {
                _projectDefaultIndex = value;
                OnPropertyUpdate(nameof(DefaultProjectIndex));
                OnPropertyUpdate(nameof(DefaultProject));
            }
        }

        public Project DefaultProject
        {
            get
            {
                if (0 <= SelectedProjectIndex && SelectedProjectIndex < Projects.Count)
                    return Projects[DefaultProjectIndex];
                else
                    return null;
            }
        }

        private int _selectProjectIndex;
        public int SelectedProjectIndex
        {
            get => _selectProjectIndex;
            set
            {
                int oldIndex = _selectProjectIndex;
                _selectProjectIndex = value;
                LoadSelectedProject(value, oldIndex);
                OnPropertyUpdate(nameof(SelectedProjectIndex));
                OnPropertyUpdate(nameof(SelectedProject));
            }
        }

        public Project SelectedProject
        {
            get
            {
                if (0 <= SelectedProjectIndex && SelectedProjectIndex < Projects.Count)
                    return Projects[SelectedProjectIndex];
                else
                    return null;
            }
        }

        private readonly object _projectSourceDirsLock = new object();
        private ObservableCollection<string> _projectSourceDirs;
        public ObservableCollection<string> ProjectSourceDirs
        {
            get => _projectSourceDirs;
            set => SetCollectionProperty(ref _projectSourceDirs, _projectSourceDirsLock, value);
        }

        private int _projectSourceDirIndex;
        public int ProjectSourceDirIndex
        {
            get => _projectSourceDirIndex;
            set
            {
                _projectSourceDirIndex = value;

                Project p = SelectedProject;
                if (0 <= value && value < ProjectSourceDirs.Count)
                {
                    p.Variables.SetValue(VarsType.Fixed, "SourceDir", ProjectSourceDirs[value]);

                    // Generate new SourceDir string, with selected source dir being first.
                    StringBuilder b = new StringBuilder(ProjectSourceDirs[value]);
                    for (int x = 0; x < ProjectSourceDirs.Count; x++)
                    {
                        if (x == value)
                            continue;
                        b.Append(",");
                        b.Append(ProjectSourceDirs[x]);
                    }
                    string newVal = b.ToString();

                    IniReadWriter.WriteKey(p.MainScript.RealPath, "Main", "SourceDir", newVal);
                    p.Variables.SetValue(VarsType.Fixed, "SourceDir", newVal);
                    p.MainScript.MainInfo["SourceDir"] = newVal;
                }

                OnPropertyUpdate(nameof(ProjectSourceDirIndex));
            }
        }

        private string _projectTargetDir;
        public string ProjectTargetDir
        {
            get => _projectTargetDir;
            set
            {
                if (!value.Equals(_projectTargetDir, StringComparison.OrdinalIgnoreCase))
                {
                    Project p = SelectedProject;
                    if (p != null)
                    {
                        string fullPath = p.MainScript.RealPath;
                        IniReadWriter.WriteKey(fullPath, "Main", "TargetDir", value);
                        p.Variables.SetValue(VarsType.Fixed, "TargetDir", value);
                        p.MainScript.MainInfo["TargetDir"] = value;
                    }
                }

                _projectTargetDir = value;
                OnPropertyUpdate(nameof(ProjectTargetDir));
            }
        }

        private string _projectIsoFile;
        public string ProjectIsoFile
        {
            get => _projectIsoFile;
            set
            {
                if (!value.Equals(_projectIsoFile, StringComparison.OrdinalIgnoreCase))
                {
                    Project p = SelectedProject;
                    if (p != null)
                    {
                        string fullPath = p.MainScript.RealPath;
                        IniReadWriter.WriteKey(fullPath, "Main", "ISOFile", value);
                        p.Variables.SetValue(VarsType.Fixed, "ISOFile", value);
                        p.MainScript.MainInfo["ISOFile"] = value;
                    }
                }

                _projectIsoFile = value;
                OnPropertyUpdate(nameof(ProjectIsoFile));
            }
        }
        #endregion

        #region Property - General
        private bool _generalOptimizeCode;
        public bool GeneralOptimizeCode
        {
            get => _generalOptimizeCode;
            set => SetProperty(ref _generalOptimizeCode, value);
        }

        private bool _generalShowLogAfterBuild;
        public bool GeneralShowLogAfterBuild
        {
            get => _generalShowLogAfterBuild;
            set => SetProperty(ref _generalShowLogAfterBuild, value);
        }

        private bool _generalStopBuildOnError;
        public bool GeneralStopBuildOnError
        {
            get => _generalStopBuildOnError;
            set => SetProperty(ref _generalStopBuildOnError, value);
        }

        private bool _generalEnableLongFilePath;
        public bool GeneralEnableLongFilePath
        {
            get => _generalEnableLongFilePath;
            set => SetProperty(ref _generalEnableLongFilePath, value);
        }

        private bool _generalUseCustomUserAgent;
        public bool GeneralUseCustomUserAgent
        {
            get => _generalUseCustomUserAgent;
            set => SetProperty(ref _generalUseCustomUserAgent, value);
        }

        private string _generalCustomUserAgent;
        public string GeneralCustomUserAgent
        {
            get => _generalCustomUserAgent;
            set => SetProperty(ref _generalCustomUserAgent, value);
        }
        #endregion

        #region Property - Interface
        private FontHelper.FontInfo _interfaceMonospacedFont;
        public FontHelper.FontInfo InterfaceMonospacedFont
        {
            get => _interfaceMonospacedFont;
            set => SetProperty(ref _interfaceMonospacedFont, value);
        }

        private bool _interfaceUseCustomTitle;
        public bool InterfaceUseCustomTitle
        {
            get => _interfaceUseCustomTitle;
            set => SetProperty(ref _interfaceUseCustomTitle, value);
        }

        private string _interfaceCustomTitle;
        public string InterfaceCustomTitle
        {
            get => _interfaceCustomTitle;
            set => SetProperty(ref _interfaceCustomTitle, value);
        }

        private bool _interfaceUseCustomEditor;
        public bool InterfaceUseCustomEditor
        {
            get => _interfaceUseCustomEditor;
            set => SetProperty(ref _interfaceUseCustomEditor, value);
        }

        private string _interfaceCustomEditorPath;
        public string InterfaceCustomEditorPath
        {
            get => _interfaceCustomEditorPath;
            set => SetProperty(ref _interfaceCustomEditorPath, value);
        }

        private int _interfaceScaleFactor;
        public double InterfaceScaleFactor
        {
            get => _interfaceScaleFactor;
            set
            {
                int newVal = (int)value;
                if (_interfaceScaleFactor != newVal)
                    NeedScriptRedraw = true;
                _interfaceScaleFactor = newVal;
                OnPropertyUpdate(nameof(InterfaceScaleFactor));
            }
        }

        private bool _interfaceDisplayShellExecuteConOut;
        public bool InterfaceDisplayShellExecuteConOut
        {
            get => _interfaceDisplayShellExecuteConOut;
            set => SetProperty(ref _interfaceDisplayShellExecuteConOut, value);
        }

        public ObservableCollection<Setting.InterfaceSize> InterfaceSizes { get; } = new ObservableCollection<Setting.InterfaceSize>
        {
            Setting.InterfaceSize.Adaptive,
            Setting.InterfaceSize.Standard,
            Setting.InterfaceSize.Small,
        };

        private Setting.InterfaceSize _interfaceSize;
        public Setting.InterfaceSize InterfaceSize
        {
            get => _interfaceSize;
            set => SetProperty(ref _interfaceSize, value);
        }
        #endregion

        #region Property - Theme
        private Setting.ThemeType _themeType;
        public Setting.ThemeType ThemeType
        {
            get => _themeType;
            set
            {
                _themeType = value;
                OnPropertyUpdate(nameof(ThemeType));
            }
        }

        private Color _themeCustomTopPanelBackground;
        public Color ThemeCustomTopPanelBackground
        {
            get => _themeCustomTopPanelBackground;
            set => SetProperty(ref _themeCustomTopPanelBackground, value);
        }

        private Color _themeCustomTopPanelForeground;
        public Color ThemeCustomTopPanelForeground
        {
            get => _themeCustomTopPanelForeground;
            set => SetProperty(ref _themeCustomTopPanelForeground, value);
        }

        private Color _themeCustomTreePanelBackground;
        public Color ThemeCustomTreePanelBackground
        {
            get => _themeCustomTreePanelBackground;
            set => SetProperty(ref _themeCustomTreePanelBackground, value);
        }

        private Color _themeCustomTreePanelForeground;
        public Color ThemeCustomTreePanelForeground
        {
            get => _themeCustomTreePanelForeground;
            set => SetProperty(ref _themeCustomTreePanelForeground, value);
        }

        private Color _themeCustomTreePanelHighlight;
        public Color ThemeCustomTreePanelHighlight
        {
            get => _themeCustomTreePanelHighlight;
            set => SetProperty(ref _themeCustomTreePanelHighlight, value);
        }

        private Color _themeCustomScriptPanelBackground;
        public Color ThemeCustomScriptPanelBackground
        {
            get => _themeCustomScriptPanelBackground;
            set => SetProperty(ref _themeCustomScriptPanelBackground, value);
        }

        private Color _themeCustomScriptPanelForeground;
        public Color ThemeCustomScriptPanelForeground
        {
            get => _themeCustomScriptPanelForeground;
            set => SetProperty(ref _themeCustomScriptPanelForeground, value);
        }

        private Color _themeCustomStatusBarBackground;
        public Color ThemeCustomStatusBarBackground
        {
            get => _themeCustomStatusBarBackground;
            set => SetProperty(ref _themeCustomStatusBarBackground, value);
        }

        private Color _themeCustomStatusBarForeground;
        public Color ThemeCustomStatusBarForeground
        {
            get => _themeCustomStatusBarForeground;
            set => SetProperty(ref _themeCustomStatusBarForeground, value);
        }
        #endregion

        #region Property - Script
        private string _scriptCacheState;
        public string ScriptCacheState
        {
            get => _scriptCacheState;
            set => SetProperty(ref _scriptCacheState, value);
        }

        private bool _scriptEnableCache;
        public bool ScriptEnableCache
        {
            get => _scriptEnableCache;
            set
            {
                if (!_scriptEnableCache && value) // Was false, now true
                    NeedScriptCaching = true; // Notify caller "You should generate cache!"
                _scriptEnableCache = value;
                OnPropertyUpdate(nameof(ScriptEnableCache));
            }
        }

        private bool _scriptAutoSyntaxCheck;
        public bool ScriptAutoSyntaxCheck
        {
            get => _scriptAutoSyntaxCheck;
            set => SetProperty(ref _scriptAutoSyntaxCheck, value);
        }
        #endregion

        #region Property - Logging
        private string _logDatabaseState;
        public string LogDatabaseState
        {
            get => _logDatabaseState;
            set => SetProperty(ref _logDatabaseState, value);
        }

        public ObservableCollection<LogDebugLevel> LogDebugLevels { get; } = new ObservableCollection<LogDebugLevel>
        {
            LogDebugLevel.Production,
            LogDebugLevel.PrintException,
            LogDebugLevel.PrintExceptionStackTrace,
        };

        private LogDebugLevel _logSelectedDebugLevel;
        public LogDebugLevel LogSelectedDebugLevel
        {
            get => _logSelectedDebugLevel;
            set => SetProperty(ref _logSelectedDebugLevel, value);
        }

        private bool _logDeferredLogging;
        public bool LogDeferredLogging
        {
            get => _logDeferredLogging;
            set => SetProperty(ref _logDeferredLogging, value);
        }

        private bool _logMinifyHtmlExport;
        public bool LogMinifyHtmlExport
        {
            get => _logMinifyHtmlExport;
            set => SetProperty(ref _logMinifyHtmlExport, value);
        }
        #endregion

        #region Property - Compatibility
        private readonly List<CompatOption> _compatOptions = new List<CompatOption>();
        public CompatOption SelectedCompatOption
        {
            get
            {
                if (_compatOptions != null && 0 <= SelectedProjectIndex && SelectedProjectIndex < _compatOptions.Count)
                    return _compatOptions[SelectedProjectIndex];
                else
                    return null;
            }
        }

        // Toggle Button
        private bool _toggleNextState;
        public bool ToggleNextState
        {
            get => _toggleNextState;
            set => SetProperty(ref _toggleNextState, value);
        }

        // Asterisk
        private bool _compatAsteriskBugDirCopy;
        public bool CompatAsteriskBugDirCopy
        {
            get => _compatAsteriskBugDirCopy;
            set
            {
                _compatAsteriskBugDirCopy = value;
                GetCompatToggleNextState();
                OnPropertyUpdate();
            }
        }

        private bool _compatAsteriskBugDirLink;
        public bool CompatAsteriskBugDirLink
        {
            get => _compatAsteriskBugDirLink;
            set
            {
                _compatAsteriskBugDirLink = value;
                GetCompatToggleNextState();
                OnPropertyUpdate();
            }
        }

        // Command
        private bool _compatFileRenameCanMoveDir;
        public bool CompatFileRenameCanMoveDir
        {
            get => _compatFileRenameCanMoveDir;
            set
            {
                _compatFileRenameCanMoveDir = value;
                GetCompatToggleNextState();
                OnPropertyUpdate();
            }
        }

        private bool _compatAllowLetterInLoop;
        public bool CompatAllowLetterInLoop
        {
            get => _compatAllowLetterInLoop;
            set
            {
                _compatAllowLetterInLoop = value;
                GetCompatToggleNextState();
                OnPropertyUpdate();
            }
        }

        private bool _compatLegacyBranchCondition;
        public bool CompatLegacyBranchCondition
        {
            get => _compatLegacyBranchCondition;
            set
            {
                _compatLegacyBranchCondition = value;
                GetCompatToggleNextState();
                OnPropertyUpdate();
            }
        }

        private bool _compatLegacyRegWrite;
        public bool CompatLegacyRegWrite
        {
            get => _compatLegacyRegWrite;
            set
            {
                _compatLegacyRegWrite = value;
                GetCompatToggleNextState();
                OnPropertyUpdate();
            }
        }

        private bool _compatAllowSetModifyInterface;
        public bool CompatAllowSetModifyInterface
        {
            get => _compatAllowSetModifyInterface;
            set
            {
                _compatAllowSetModifyInterface = value;
                GetCompatToggleNextState();
                OnPropertyUpdate();
            }
        }

        private bool _compatLegacyInterfaceCommand;
        public bool CompatLegacyInterfaceCommand
        {
            get => _compatLegacyInterfaceCommand;
            set
            {
                _compatLegacyInterfaceCommand = value;
                GetCompatToggleNextState();
                OnPropertyUpdate();
            }
        }

        private bool _compatLegacySectionParamCommand;
        public bool CompatLegacySectionParamCommand
        {
            get => _compatLegacySectionParamCommand;
            set
            {
                _compatLegacySectionParamCommand = value;
                GetCompatToggleNextState();
                OnPropertyUpdate();
            }
        }

        // Script Interface
        private bool _compatIgnoreWidthOfWebLabel;
        public bool CompatIgnoreWidthOfWebLabel
        {
            get => _compatIgnoreWidthOfWebLabel;
            set
            {
                if (_compatIgnoreWidthOfWebLabel != value)
                    NeedScriptRedraw = true;
                _compatIgnoreWidthOfWebLabel = value;
                GetCompatToggleNextState();
                OnPropertyUpdate();
            }
        }

        // Variable
        private bool _compatOverridableFixedVariables;
        public bool CompatOverridableFixedVariables
        {
            get => _compatOverridableFixedVariables;
            set
            {
                _compatOverridableFixedVariables = value;
                GetCompatToggleNextState();
                OnPropertyUpdate();
            }
        }

        private bool _compatOverridableLoopCounter;
        public bool CompatOverridableLoopCounter
        {
            get => _compatOverridableLoopCounter;
            set
            {
                _compatOverridableLoopCounter = value;
                GetCompatToggleNextState();
                OnPropertyUpdate();
            }
        }

        private bool _compatEnableEnvironmentVariables;
        public bool CompatEnableEnvironmentVariables
        {
            get => _compatEnableEnvironmentVariables;
            set
            {
                _compatEnableEnvironmentVariables = value;
                GetCompatToggleNextState();
                OnPropertyUpdate();
            }
        }

        private bool _compatDisableExtendedSectionParams;
        public bool CompatDisableExtendedSectionParams
        {
            get => _compatDisableExtendedSectionParams;
            set
            {
                _compatDisableExtendedSectionParams = value;
                GetCompatToggleNextState();
                OnPropertyUpdate();
            }
        }
        #endregion

        #region LoadProjectEntries
        public void LoadProjectEntries()
        {
            // Select default project
            int idx = Projects.IndexOf(Setting.Project.DefaultProject);
            if (idx == -1)
                SelectedProjectIndex = DefaultProjectIndex = Projects.Count - 1;
            else
                SelectedProjectIndex = idx;

            // Compat Options
            _compatOptions.Clear();
            foreach (Project p in Projects)
            {
                CompatOption compat = p.Compat.Clone();
                _compatOptions.Add(compat);
            }
        }
        #endregion

        #region LoadSelectedProject
        public async void LoadSelectedProject(int newIndex, int oldIndex)
        {
            if (newIndex < 0 || Projects.Count <= newIndex)
                return;

            await Task.Run(() =>
            {
                // Project 
                Dictionary<string, string> infoDict = SelectedProject.MainScript.MainInfo;

                // SourceDir
                ProjectSourceDirs.Clear();
                if (infoDict.ContainsKey("SourceDir"))
                {
                    string valStr = infoDict["SourceDir"];
                    foreach (string rawDir in StringHelper.SplitEx(valStr, ",", StringComparison.Ordinal))
                    {
                        string dir = rawDir.Trim();
                        if (0 < dir.Length)
                            ProjectSourceDirs.Add(dir);
                    }
                }
                if (0 < ProjectSourceDirs.Count)
                    ProjectSourceDirIndex = 0;

                // TargetDir
                if (infoDict.ContainsKey("TargetDir"))
                    ProjectTargetDir = infoDict["TargetDir"];
                else
                    ProjectTargetDir = string.Empty;

                // ISOFile
                if (infoDict.ContainsKey("ISOFile"))
                    ProjectIsoFile = infoDict["ISOFile"];
                else
                    ProjectIsoFile = string.Empty;

                // Compat Options
                if (!_firstLoad && newIndex != oldIndex &&
                    0 <= oldIndex && oldIndex < _compatOptions.Count)
                    SaveCompatOptionTo(_compatOptions[oldIndex]);
                CompatOption compat = SelectedCompatOption;
                Debug.Assert(compat != null, "Invalid SelectedProjectIndex");
                LoadCompatOptionFrom(compat);

                _firstLoad = false;
            });
        }
        #endregion

        #region LoadCompatOptionFrom, SaveCompatOptionTo
        public void LoadCompatOptionFrom(CompatOption src)
        {
            // Asterisk
            CompatAsteriskBugDirCopy = src.AsteriskBugDirCopy;
            CompatAsteriskBugDirLink = src.AsteriskBugDirLink;
            // Command
            CompatFileRenameCanMoveDir = src.FileRenameCanMoveDir;
            CompatAllowLetterInLoop = src.AllowLetterInLoop;
            CompatLegacyBranchCondition = src.LegacyBranchCondition;
            CompatLegacyRegWrite = src.LegacyRegWrite;
            CompatAllowSetModifyInterface = src.AllowSetModifyInterface;
            CompatLegacyInterfaceCommand = src.LegacyInterfaceCommand;
            CompatLegacySectionParamCommand = src.LegacySectionParamCommand;
            // Script Interface
            CompatIgnoreWidthOfWebLabel = src.IgnoreWidthOfWebLabel;
            // Variable
            CompatOverridableFixedVariables = src.OverridableFixedVariables;
            CompatOverridableLoopCounter = src.OverridableLoopCounter;
            CompatEnableEnvironmentVariables = src.EnableEnvironmentVariables;
            CompatDisableExtendedSectionParams = src.DisableExtendedSectionParams;
        }

        public void SaveCompatOptionTo(CompatOption dest)
        {
            // Asterisk
            dest.AsteriskBugDirCopy = CompatAsteriskBugDirCopy;
            dest.AsteriskBugDirLink = CompatAsteriskBugDirLink;
            // Command
            dest.FileRenameCanMoveDir = CompatFileRenameCanMoveDir;
            dest.AllowLetterInLoop = CompatAllowLetterInLoop;
            dest.LegacyBranchCondition = CompatLegacyBranchCondition;
            dest.LegacyRegWrite = CompatLegacyRegWrite;
            dest.AllowSetModifyInterface = CompatAllowSetModifyInterface;
            dest.LegacyInterfaceCommand = CompatLegacyInterfaceCommand;
            dest.LegacySectionParamCommand = CompatLegacySectionParamCommand;
            // Script Interface
            dest.IgnoreWidthOfWebLabel = CompatIgnoreWidthOfWebLabel;
            // Variable
            dest.OverridableFixedVariables = CompatOverridableFixedVariables;
            dest.OverridableLoopCounter = CompatOverridableLoopCounter;
            dest.EnableEnvironmentVariables = CompatEnableEnvironmentVariables;
            dest.DisableExtendedSectionParams = CompatDisableExtendedSectionParams;
        }
        #endregion

        #region ToggleCompatOption
        /// <summary>
        /// If all compat options are true, set ToggleNextState to false.
        /// If some compat options are true, set ToggleNextState to true.
        /// If none of compat option is true, set ToggleNextState to true.
        /// </summary>
        public void GetCompatToggleNextState()
        {
            // Get current state
            bool currentState = true;

            // Asterisk
            currentState &= CompatAsteriskBugDirCopy;
            currentState &= CompatAsteriskBugDirLink;
            // Command
            currentState &= CompatFileRenameCanMoveDir;
            currentState &= CompatAllowLetterInLoop;
            currentState &= CompatLegacyBranchCondition;
            currentState &= CompatLegacyRegWrite;
            currentState &= CompatAllowSetModifyInterface;
            currentState &= CompatLegacyInterfaceCommand;
            currentState &= CompatLegacySectionParamCommand;
            // Script Interface
            currentState &= CompatIgnoreWidthOfWebLabel;
            // Variable
            currentState &= CompatOverridableFixedVariables;
            currentState &= CompatOverridableLoopCounter;
            currentState &= CompatEnableEnvironmentVariables;
            currentState &= CompatDisableExtendedSectionParams;

            ToggleNextState = !currentState;
        }

        /// <summary>
        /// Apply ToggleNextState to compat options
        /// </summary>
        public void ApplyCompatToggleNextState(bool nextState)
        {
            // Asterisk
            CompatAsteriskBugDirCopy = nextState;
            CompatAsteriskBugDirLink = nextState;
            // Command
            CompatFileRenameCanMoveDir = nextState;
            CompatAllowLetterInLoop = nextState;
            CompatLegacyBranchCondition = nextState;
            CompatLegacyRegWrite = nextState;
            CompatAllowSetModifyInterface = nextState;
            CompatLegacyInterfaceCommand = nextState;
            CompatLegacySectionParamCommand = nextState;
            // Script Interface
            CompatIgnoreWidthOfWebLabel = nextState;
            // Variable
            CompatOverridableFixedVariables = nextState;
            CompatOverridableLoopCounter = nextState;
            CompatEnableEnvironmentVariables = nextState;
            CompatDisableExtendedSectionParams = nextState;
        }
        #endregion

        #region SetToDefault
        public void SetToDefault()
        {
            // When constructor of `Setting`'s subclass is called, default values are set.

            // [Projects]
            // Select default project
            // If default project is not set, use last project (Some PE projects starts with 'W' from Windows)
            Setting.ProjectSetting newProject = new Setting.ProjectSetting();
            string defaultProjectName = newProject.DefaultProject;
            int defaultProjectIdx = Projects.IndexOf(defaultProjectName);
            if (defaultProjectIdx == -1)
                DefaultProjectIndex = Projects.Count - 1;
            else
                DefaultProjectIndex = defaultProjectIdx;

            // [General]
            Setting.GeneralSetting newGeneral = new Setting.GeneralSetting();
            GeneralOptimizeCode = newGeneral.OptimizeCode;
            GeneralShowLogAfterBuild = newGeneral.ShowLogAfterBuild;
            GeneralStopBuildOnError = newGeneral.StopBuildOnError;
            GeneralEnableLongFilePath = newGeneral.EnableLongFilePath;
            GeneralUseCustomUserAgent = newGeneral.UseCustomUserAgent;
            GeneralCustomUserAgent = newGeneral.CustomUserAgent;

            // [Interface]
            Setting.InterfaceSetting newInterface = new Setting.InterfaceSetting();
            InterfaceMonospacedFont = newInterface.MonospacedFont;
            InterfaceScaleFactor = newInterface.ScaleFactor;
            InterfaceUseCustomEditor = newInterface.UseCustomEditor;
            InterfaceCustomEditorPath = newInterface.CustomEditorPath;
            InterfaceDisplayShellExecuteConOut = newInterface.DisplayShellExecuteConOut;
            InterfaceUseCustomTitle = newInterface.UseCustomTitle;
            InterfaceCustomTitle = newInterface.CustomTitle;

            // [Theme]
            Setting.ThemeSetting newTheme = new Setting.ThemeSetting();
            ThemeType = newTheme.ThemeType;
            ThemeCustomTopPanelBackground = newTheme.CustomTopPanelBackground;
            ThemeCustomTopPanelForeground = newTheme.CustomTopPanelForeground;
            ThemeCustomTreePanelBackground = newTheme.CustomTreePanelBackground;
            ThemeCustomTreePanelForeground = newTheme.CustomTreePanelForeground;
            ThemeCustomTreePanelHighlight = newTheme.CustomTreePanelHighlight;
            ThemeCustomScriptPanelBackground = newTheme.CustomScriptPanelBackground;
            ThemeCustomScriptPanelForeground = newTheme.CustomScriptPanelForeground;
            ThemeCustomStatusBarBackground = newTheme.CustomStatusBarBackground;
            ThemeCustomStatusBarForeground = newTheme.CustomStatusBarForeground;

            // [Script]
            Setting.ScriptSetting newScript = new Setting.ScriptSetting();
            ScriptEnableCache = newScript.EnableCache;
            ScriptAutoSyntaxCheck = newScript.AutoSyntaxCheck;

            // [Log]
            Setting.LogSetting newLog = new Setting.LogSetting();
            LogSelectedDebugLevel = newLog.DebugLevel;
            LogDeferredLogging = newLog.DeferredLogging;
            LogMinifyHtmlExport = newLog.MinifyHtmlExport;

            // Reset need flags
            ResetNeedFlags();

            // Do not touch compat options.
            // TODO: The right way to handle compat options?
        }

        public void ResetNeedFlags()
        {
            NeedProjectRefresh = false;
            NeedScriptRedraw = false;
            NeedScriptCaching = false;
        }
        #endregion

        #region ReadFromSetting, WriteToSetting, WriteToFile
        public void ReadFromSetting()
        {
            // [Projects]
            // Select default project
            // If default project is not set, use last project (Some PE projects starts with 'W' from Windows)
            string defaultProjectName = Setting.Project.DefaultProject;
            int defaultProjectIdx = Projects.IndexOf(defaultProjectName);
            if (defaultProjectIdx == -1)
                DefaultProjectIndex = Projects.Count - 1;
            else
                DefaultProjectIndex = defaultProjectIdx;

            // [General]
            GeneralOptimizeCode = Setting.General.OptimizeCode;
            GeneralShowLogAfterBuild = Setting.General.ShowLogAfterBuild;
            GeneralStopBuildOnError = Setting.General.StopBuildOnError;
            GeneralEnableLongFilePath = Setting.General.EnableLongFilePath;
            GeneralUseCustomUserAgent = Setting.General.UseCustomUserAgent;
            GeneralCustomUserAgent = Setting.General.CustomUserAgent;

            // [Interface]
            InterfaceUseCustomTitle = Setting.Interface.UseCustomTitle;
            InterfaceCustomTitle = Setting.Interface.CustomTitle;
            InterfaceUseCustomEditor = Setting.Interface.UseCustomEditor;
            InterfaceCustomEditorPath = Setting.Interface.CustomEditorPath;
            InterfaceMonospacedFont = Setting.Interface.MonospacedFont;
            InterfaceScaleFactor = Setting.Interface.ScaleFactor;
            InterfaceDisplayShellExecuteConOut = Setting.Interface.DisplayShellExecuteConOut;
            InterfaceSize = Setting.Interface.InterfaceSize;

            // [Theme]
            ThemeType = Setting.Theme.ThemeType;
            ThemeCustomTopPanelBackground = Setting.Theme.CustomTopPanelBackground;
            ThemeCustomTopPanelForeground = Setting.Theme.CustomTopPanelForeground;
            ThemeCustomTreePanelBackground = Setting.Theme.CustomTreePanelBackground;
            ThemeCustomTreePanelForeground = Setting.Theme.CustomTreePanelForeground;
            ThemeCustomTreePanelHighlight = Setting.Theme.CustomTreePanelHighlight;
            ThemeCustomScriptPanelBackground = Setting.Theme.CustomScriptPanelBackground;
            ThemeCustomScriptPanelForeground = Setting.Theme.CustomScriptPanelForeground;
            ThemeCustomStatusBarBackground = Setting.Theme.CustomStatusBarBackground;
            ThemeCustomStatusBarForeground = Setting.Theme.CustomStatusBarForeground;

            // [Script]
            ScriptEnableCache = Setting.Script.EnableCache;
            ScriptAutoSyntaxCheck = Setting.Script.AutoSyntaxCheck;

            // [Log]
            LogSelectedDebugLevel = Setting.Log.DebugLevel;
            LogDeferredLogging = Setting.Log.DeferredLogging;
            LogMinifyHtmlExport = Setting.Log.MinifyHtmlExport;

            ResetNeedFlags();
        }

        public void WriteToSetting()
        {
            // Set default project
            Project defaultProject = DefaultProject;
            Setting.Project.DefaultProject = defaultProject != null ? defaultProject.ProjectName : string.Empty;

            // [Projects]
            // Select default project
            // If default project is not set, use last project (Some PE projects starts with 'W' from Windows)
            string defaultProjectName = Setting.Project.DefaultProject;
            int defaultProjectIdx = Projects.IndexOf(defaultProjectName);
            if (defaultProjectIdx == -1)
                DefaultProjectIndex = Projects.Count - 1;
            else
                DefaultProjectIndex = defaultProjectIdx;

            // [General]
            Setting.General.OptimizeCode = GeneralOptimizeCode;
            Setting.General.ShowLogAfterBuild = GeneralShowLogAfterBuild;
            Setting.General.StopBuildOnError = GeneralStopBuildOnError;
            Setting.General.EnableLongFilePath = GeneralEnableLongFilePath;
            Setting.General.UseCustomUserAgent = GeneralUseCustomUserAgent;
            Setting.General.CustomUserAgent = GeneralCustomUserAgent;

            // [Interface]
            Setting.Interface.UseCustomTitle = InterfaceUseCustomTitle;
            Setting.Interface.CustomTitle = InterfaceCustomTitle;
            Setting.Interface.UseCustomEditor = InterfaceUseCustomEditor;
            Setting.Interface.CustomEditorPath = InterfaceCustomEditorPath;
            Setting.Interface.MonospacedFont = InterfaceMonospacedFont;
            Setting.Interface.ScaleFactor = _interfaceScaleFactor;
            Setting.Interface.DisplayShellExecuteConOut = InterfaceDisplayShellExecuteConOut;
            Setting.Interface.InterfaceSize = InterfaceSize;

            // [Theme]
            Setting.Theme.ThemeType = ThemeType;
            Setting.Theme.CustomTopPanelBackground = ThemeCustomTopPanelBackground;
            Setting.Theme.CustomTopPanelForeground = ThemeCustomTopPanelForeground;
            Setting.Theme.CustomTreePanelBackground = ThemeCustomTreePanelBackground;
            Setting.Theme.CustomTreePanelForeground = ThemeCustomTreePanelForeground;
            Setting.Theme.CustomTreePanelHighlight = ThemeCustomTreePanelHighlight;
            Setting.Theme.CustomScriptPanelBackground = ThemeCustomScriptPanelBackground;
            Setting.Theme.CustomScriptPanelForeground = ThemeCustomScriptPanelForeground;
            Setting.Theme.CustomStatusBarBackground = ThemeCustomStatusBarBackground;
            Setting.Theme.CustomStatusBarForeground = ThemeCustomStatusBarForeground;

            // [Script]
            Setting.Script.EnableCache = ScriptEnableCache;
            Setting.Script.AutoSyntaxCheck = ScriptAutoSyntaxCheck;

            // [Log]
            Setting.Log.DebugLevel = _logSelectedDebugLevel; // LogDebugLevel
            Setting.Log.DeferredLogging = LogDeferredLogging;
            Setting.Log.MinifyHtmlExport = LogMinifyHtmlExport;

            // Compat options
            if (SelectedCompatOption != null)
                SaveCompatOptionTo(SelectedCompatOption);
            for (int i = 0; i < _compatOptions.Count; i++)
            {
                CompatOption compat = _compatOptions[i];
                Project p = Projects[i];

                Dictionary<string, bool> diffDict = compat.Diff(p.Compat);
                Debug.Assert(diffDict.ContainsKey(nameof(compat.AsteriskBugDirLink)), "Invalid compat option field name");
                Debug.Assert(diffDict.ContainsKey(nameof(compat.OverridableFixedVariables)), "Invalid compat option field name");
                Debug.Assert(diffDict.ContainsKey(nameof(compat.EnableEnvironmentVariables)), "Invalid compat option field name");
                if (diffDict[nameof(compat.AsteriskBugDirLink)] ||
                    diffDict[nameof(compat.OverridableFixedVariables)] ||
                    diffDict[nameof(compat.EnableEnvironmentVariables)])
                    NeedProjectRefresh = true;

                compat.CopyTo(p.Compat);
            }
        }

        public void WriteToFile()
        {
            // Write to file
            Setting.WriteToFile();
            foreach (Project p in Projects)
                p.Compat.WriteToFile();
        }
        #endregion

        #region ShallowCopy
        public SettingViewModel ShallowCopy() => MemberwiseClone() as SettingViewModel;
        #endregion

        #region Database Operation
        public void ClearLogDatabase()
        {
            Global.Logger.Db.ClearTable(new LogDatabase.ClearTableOptions
            {
                SystemLog = true,
                BuildInfo = true,
                BuildLog = true,
                Script = true,
                Variable = true,
            });
            UpdateLogDbState();
        }

        public void ClearCacheDatabase()
        {
            if (Global.ScriptCache == null)
                return;

            Global.ScriptCache.ClearTable(new ScriptCache.ClearTableOptions
            {
                ScriptCache = true,
            });
            UpdateCacheDbState();
        }

        public void UpdateLogDbState()
        {
            int systemLogCount = Global.Logger.Db.Table<LogModel.SystemLog>().Count();
            int codeLogCount = Global.Logger.Db.Table<LogModel.BuildLog>().Count();
            LogDatabaseState = $"{systemLogCount} System Logs, {codeLogCount} Build Logs";
        }

        public void UpdateCacheDbState()
        {
            if (Global.ScriptCache == null)
            {
                ScriptCacheState = "Cache not enabled";
            }
            else
            {
                int cacheCount = Global.ScriptCache.Table<CacheModel.ScriptCache>().Count();
                ScriptCacheState = $"{cacheCount} scripts cached";
            }
        }
        #endregion
    }
    #endregion

    #region SettingViewCommands
    public static class SettingViewCommands
    {
        #region Global
        public static readonly RoutedCommand DefaultSettingCommand = new RoutedUICommand("Reset to default settings", "DefaultSetting", typeof(SettingViewCommands));
        public static readonly RoutedCommand SaveSettingCommand = new RoutedUICommand("Save settings", "SaveSetting", typeof(SettingViewCommands));
        #endregion

        #region Project Settings
        public static readonly RoutedCommand SelectSourceDirCommand = new RoutedUICommand("Select source directory", "SelectSourceDir", typeof(SettingViewCommands));
        public static readonly RoutedCommand ResetSourceDirCommand = new RoutedUICommand("Reset source directory", "ResetSourceDir", typeof(SettingViewCommands));
        public static readonly RoutedCommand SelectTargetDirCommand = new RoutedUICommand("Select target directory", "SelectTargetDir", typeof(SettingViewCommands));
        public static readonly RoutedCommand SelectIsoFileCommand = new RoutedUICommand("Select ISO file directory", "SelectIsoFile", typeof(SettingViewCommands));
        #endregion

        #region General Settting
        public static readonly RoutedCommand EnableLongFilePathCommand = new RoutedUICommand("Enable long file path support", "EnableLongFilePath", typeof(SettingViewCommands));
        #endregion

        #region Interface Setting
        public static readonly RoutedCommand ResetScaleFactorCommand = new RoutedUICommand("Reset scale factor", "ResetScaleFactor", typeof(SettingViewCommands));
        public static readonly RoutedCommand SelectMonospacedFontCommand = new RoutedUICommand("Select monospaced font", "SelectMonospacedFont", typeof(SettingViewCommands));
        public static readonly RoutedCommand SelectCustomEditorPathCommand = new RoutedUICommand("Select custom editor path", "SelectCustomEditorPath", typeof(SettingViewCommands));
        #endregion

        #region Theme Setting
        public static readonly RoutedCommand SelectThemeCommand = new RoutedUICommand("Select theme", "SelectThemePath", typeof(SettingViewCommands));
        #endregion

        #region Script Setting
        public static readonly RoutedCommand ClearCacheDatabaseCommand = new RoutedUICommand("Clear cache database", "ClearCacheDatabase", typeof(SettingViewCommands));
        #endregion

        #region Log Setting
        public static readonly RoutedCommand ClearLogDatabaseCommand = new RoutedUICommand("Clear log database", "ClearLogDatabase", typeof(SettingViewCommands));
        #endregion

        #region Compat Setting
        public static readonly RoutedCommand ToggleCompatOptionsCommand = new RoutedUICommand("Toggle compat options", "ToggleCompatOptions", typeof(SettingViewCommands));
        #endregion
    }
    #endregion
}
