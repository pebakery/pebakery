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

using Ookii.Dialogs.Wpf;
using PEBakery.Core;
using PEBakery.Core.ViewModels;
using PEBakery.Helper;
using PEBakery.Ini;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
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
        #region Field and Constructor
        private readonly SettingViewModel _m;

        public SettingWindow()
        {
            DataContext = _m = Global.Setting;
            InitializeComponent();
        }
        #endregion

        #region Window Event Handler
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _m.UpdateCacheDbState();
            _m.UpdateLogDbState();
            _m.UpdateProjectList();
        }
        #endregion

        #region Button Event Handler
        #region Global Buttons
        private void Command_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _m != null && _m.CanExecuteCommand;
        }

        private void DefaultSettingCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            _m.CanExecuteCommand = false;
            try
            {
                _m.SetToDefault();
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
                await _m.WriteToFileAsync();
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
                if (0 < _m.Project_SourceDirectoryList.Count)
                    dialog.SelectedPath = _m.Project_SourceDirectoryList[_m.Project_SourceDirectoryIndex];

                if (dialog.ShowDialog(this) == true)
                {
                    bool exist = false;
                    for (int i = 0; i < _m.Project_SourceDirectoryList.Count; i++)
                    {
                        string projectName = _m.Project_SourceDirectoryList[i];
                        if (projectName.Equals(dialog.SelectedPath, StringComparison.OrdinalIgnoreCase))
                        { // Selected Path exists
                            _m.Project_SourceDirectoryIndex = i;
                            exist = true;
                            break;
                        }
                    }

                    if (!exist) // Add to list
                    {
                        _m.Project_SourceDirectoryList.Insert(0, dialog.SelectedPath);
                        _m.Project_SourceDirectoryIndex = 0;
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
                _m.Project_SourceDirectoryList.Clear();

                int idx = _m.Project_SelectedIndex;
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
                VistaFolderBrowserDialog dialog = new VistaFolderBrowserDialog()
                {
                    SelectedPath = _m.Project_TargetDirectory,
                };

                if (dialog.ShowDialog(this) == true)
                {
                    _m.Project_TargetDirectory = dialog.SelectedPath;
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
                    FileName = _m.Project_ISOFile,
                };

                if (dialog.ShowDialog(this) == true)
                {
                    _m.Project_ISOFile = dialog.FileName;
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
            if (!_m.General_EnableLongFilePath)
                return;

            _m.CanExecuteCommand = false;
            try
            {
                const string msg = "Enabling this option may cause problems!\r\nDo you really want to continue?";
                MessageBoxResult res = MessageBox.Show(msg, "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                _m.General_EnableLongFilePath = res == MessageBoxResult.Yes;
            }
            finally
            {
                _m.CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }
        #endregion

        #region Interface Setting Commands
        private void SelectMonospacedFontCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            _m.CanExecuteCommand = false;
            try
            {
                _m.Interface_MonospacedFont = FontHelper.ChooseFontDialog(_m.Interface_MonospacedFont, this, monospaced: true);
            }
            finally
            {
                _m.CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void SelectCustomEditorPathCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _m != null && _m.CanExecuteCommand && _m.Interface_UseCustomEditor;
        }

        private void SelectCustomEditorPathCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            _m.CanExecuteCommand = false;
            try
            {
                Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Executable|*.exe",
                    FileName = _m.Interface_CustomEditorPath,
                };

                if (dialog.ShowDialog(this) == true)
                {
                    _m.Interface_CustomEditorPath = dialog.FileName;
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
        #endregion
    }
    #endregion

    #region SettingViewModel
    /// <summary>
    /// TODO: Split Model and ViewModel
    /// </summary>
    public class SettingViewModel : ViewModelBase
    {
        #region Field and Constructor
        private readonly string _settingFile;
        public LogDatabase LogDb { get; set; }
        public ScriptCache ScriptCache { get; set; }
        public ProjectCollection Projects { get; private set; }

        public SettingViewModel(string settingFile)
        {
            _settingFile = settingFile;
            ReadFromFile();

            ApplySetting();
        }
        #endregion

        #region CanExecuteCommand
        public bool CanExecuteCommand { get; set; } = true;
        #endregion

        #region Property - Project
        private string _projectDefault;
        public string Project_Default
        {
            get
            {
                if (0 <= _projectDefaultIndex && _projectDefaultIndex < Project_List.Count)
                    return Project_List[_projectDefaultIndex];
                else
                    return string.Empty;
            }
        }

        private ObservableCollection<string> _projectNames;
        public ObservableCollection<string> Project_List
        {
            get => _projectNames;
            set
            {
                _projectNames = value;
                OnPropertyUpdate(nameof(Project_List));
            }
        }

        private int _projectDefaultIndex;
        public int Project_DefaultIndex
        {
            get => _projectDefaultIndex;
            set
            {
                _projectDefaultIndex = value;
                OnPropertyUpdate(nameof(Project_DefaultIndex));
            }
        }

        private int _projectSelectedIndex;
        public int Project_SelectedIndex
        {
            get => _projectSelectedIndex;
            set
            {
                _projectSelectedIndex = value;

                if (0 <= value && value < Project_List.Count)
                {
                    string fullPath = Projects[value].MainScript.RealPath;
                    IniKey[] keys = new IniKey[]
                    {
                        new IniKey("Main", "SourceDir"),
                        new IniKey("Main", "TargetDir"),
                        new IniKey("Main", "ISOFile"),
                        new IniKey("Main", "PathSetting"),
                    };
                    keys = IniReadWriter.ReadKeys(fullPath, keys);

                    // PathSetting
                    if (keys[3].Value != null && keys[3].Value.Equals("False", StringComparison.OrdinalIgnoreCase))
                        Project_PathEnabled = false;
                    else
                        Project_PathEnabled = true;

                    // SourceDir
                    Project_SourceDirectoryList = new ObservableCollection<string>();
                    if (keys[0].Value != null)
                    {
                        string[] rawDirList = keys[0].Value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string rawDir in rawDirList)
                        {
                            string dir = rawDir.Trim();
                            if (0 < dir.Length)
                                Project_SourceDirectoryList.Add(dir);
                        }
                    }

                    if (0 < Project_SourceDirectoryList.Count)
                    {
                        _projectSourceDirectoryIndex = 0;
                        OnPropertyUpdate(nameof(Project_SourceDirectoryIndex));
                    }

                    if (keys[1].Value != null)
                    {
                        _projectTargetDirectory = keys[1].Value;
                        OnPropertyUpdate(nameof(Project_TargetDirectory));
                    }

                    if (keys[2].Value != null)
                    {
                        _projectIsoFile = keys[2].Value;
                        OnPropertyUpdate(nameof(Project_ISOFile));
                    }
                }

                OnPropertyUpdate(nameof(Project_SelectedIndex));
            }
        }

        private bool _projectPathEnabled = true;
        public bool Project_PathEnabled
        {
            get => _projectPathEnabled;
            set
            {
                _projectPathEnabled = value;
                OnPropertyUpdate(nameof(Project_PathEnabled));
            }
        }

        private ObservableCollection<string> project_SourceDirectoryList;
        public ObservableCollection<string> Project_SourceDirectoryList
        {
            get => project_SourceDirectoryList;
            set
            {
                project_SourceDirectoryList = value;
                OnPropertyUpdate(nameof(Project_SourceDirectoryList));
            }
        }

        private int _projectSourceDirectoryIndex;
        public int Project_SourceDirectoryIndex
        {
            get => _projectSourceDirectoryIndex;
            set
            {
                _projectSourceDirectoryIndex = value;

                Project project = Projects[Project_SelectedIndex];
                if (0 <= value && value < Project_SourceDirectoryList.Count)
                {
                    project.Variables.SetValue(VarsType.Fixed, "SourceDir", Project_SourceDirectoryList[value]);

                    StringBuilder b = new StringBuilder(Project_SourceDirectoryList[value]);
                    for (int x = 0; x < Project_SourceDirectoryList.Count; x++)
                    {
                        if (x == value)
                            continue;

                        b.Append(",");
                        b.Append(Project_SourceDirectoryList[x]);
                    }
                    IniReadWriter.WriteKey(project.MainScript.RealPath, "Main", "SourceDir", b.ToString());
                }

                OnPropertyUpdate(nameof(Project_SourceDirectoryIndex));
            }
        }

        private string _projectTargetDirectory;
        public string Project_TargetDirectory
        {
            get => _projectTargetDirectory;
            set
            {
                if (!value.Equals(_projectTargetDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    Project project = Projects[_projectSelectedIndex];
                    string fullPath = project.MainScript.RealPath;
                    IniReadWriter.WriteKey(fullPath, "Main", "TargetDir", value);
                    project.Variables.SetValue(VarsType.Fixed, "TargetDir", value);
                }

                _projectTargetDirectory = value;

                OnPropertyUpdate(nameof(Project_TargetDirectory));
            }
        }

        private string _projectIsoFile;
        public string Project_ISOFile
        {
            get => _projectIsoFile;
            set
            {
                if (value.Equals(_projectIsoFile, StringComparison.OrdinalIgnoreCase) == false)
                {
                    Project project = Projects[_projectSelectedIndex];
                    string fullPath = project.MainScript.RealPath;
                    IniReadWriter.WriteKey(fullPath, "Main", "ISOFile", value);
                    project.Variables.SetValue(VarsType.Fixed, "ISOFile", value);
                }

                _projectIsoFile = value;

                OnPropertyUpdate(nameof(Project_ISOFile));
            }
        }
        #endregion

        #region Property - General
        // Build
        private bool _generalOptimizeCode;
        public bool General_OptimizeCode
        {
            get => _generalOptimizeCode;
            set
            {
                _generalOptimizeCode = value;
                OnPropertyUpdate(nameof(General_OptimizeCode));
            }
        }

        private bool _generalShowLogAfterBuild;
        public bool General_ShowLogAfterBuild
        {
            get => _generalShowLogAfterBuild;
            set
            {
                _generalShowLogAfterBuild = value;
                OnPropertyUpdate(nameof(General_ShowLogAfterBuild));
            }
        }

        private bool _generalStopBuildOnError;
        public bool General_StopBuildOnError
        {
            get => _generalStopBuildOnError;
            set
            {
                _generalStopBuildOnError = value;
                OnPropertyUpdate(nameof(General_StopBuildOnError));
            }
        }

        // Path Length Limit
        private bool _generalEnableLongFilePath;
        public bool General_EnableLongFilePath
        {
            get => _generalEnableLongFilePath;
            set
            {
                _generalEnableLongFilePath = value;

                // Enabled  = Path Length Limit = 32767
                // Disabled = Path Length Limit = 260
                AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling", !value);

                OnPropertyUpdate(nameof(General_EnableLongFilePath));
            }
        }

        // Custom User-Agent
        private bool _generalUseCustomUserAgent;
        public bool General_UseCustomUserAgent
        {
            get => _generalUseCustomUserAgent;
            set
            {
                _generalUseCustomUserAgent = value;
                OnPropertyUpdate(nameof(General_UseCustomUserAgent));
            }
        }

        private string _generalCustomUserAgent;
        public string General_CustomUserAgent
        {
            get => _generalCustomUserAgent;
            set
            {
                _generalCustomUserAgent = value;
                OnPropertyUpdate(nameof(General_CustomUserAgent));
            }
        }
        #endregion

        #region Property - Interface
        private FontHelper.FontInfo _interfaceMonospacedFont;
        public FontHelper.FontInfo Interface_MonospacedFont
        {
            get => _interfaceMonospacedFont;
            set
            {
                _interfaceMonospacedFont = value;

                OnPropertyUpdate(nameof(Interface_MonospacedFont));
                OnPropertyUpdate(nameof(Interface_MonospacedFontFamily));
                OnPropertyUpdate(nameof(Interface_MonospacedFontWeight));
                OnPropertyUpdate(nameof(Interface_MonospacedFontSize));
            }
        }

        public FontFamily Interface_MonospacedFontFamily => _interfaceMonospacedFont.FontFamily;
        public FontWeight Interface_MonospacedFontWeight => _interfaceMonospacedFont.FontWeight;
        public double Interface_MonospacedFontSize => _interfaceMonospacedFont.FontSizeInDIP;

        private double _interfaceScaleFactor;
        public double Interface_ScaleFactor
        {
            get => _interfaceScaleFactor;
            set
            {
                _interfaceScaleFactor = value;
                OnPropertyUpdate(nameof(Interface_ScaleFactor));
            }
        }

        private bool _interfaceUseCustomEditor;
        public bool Interface_UseCustomEditor
        {
            get => _interfaceUseCustomEditor;
            set
            {
                _interfaceUseCustomEditor = value;
                OnPropertyUpdate(nameof(Interface_UseCustomEditor));
            }
        }

        private string _interfaceCustomEditorPath;
        public string Interface_CustomEditorPath
        {
            get => _interfaceCustomEditorPath;
            set
            {
                _interfaceCustomEditorPath = value;
                OnPropertyUpdate(nameof(Interface_CustomEditorPath));
            }
        }

        private bool _interfaceDisplayShellExecuteConOut;
        public bool Interface_DisplayShellExecuteConOut
        {
            get => _interfaceDisplayShellExecuteConOut;
            set
            {
                _interfaceDisplayShellExecuteConOut = value;
                OnPropertyUpdate(nameof(Interface_DisplayShellExecuteConOut));
            }
        }

        // Custom Title
        private bool _interfaceUseCustomTitle;
        public bool Interface_UseCustomTitle
        {
            get => _interfaceUseCustomTitle;
            set
            {
                _interfaceUseCustomTitle = value;
                OnPropertyUpdate(nameof(Interface_UseCustomTitle));
            }
        }

        private string _interfaceCustomTitle;
        public string Interface_CustomTitle
        {
            get => _interfaceCustomTitle;
            set
            {
                _interfaceCustomTitle = value;
                OnPropertyUpdate(nameof(Interface_CustomTitle));
            }
        }
        #endregion

        #region Property - Script
        private string _scriptCacheState;
        public string Script_CacheState
        {
            get => _scriptCacheState;
            set
            {
                _scriptCacheState = value;
                OnPropertyUpdate(nameof(Script_CacheState));
            }
        }

        private bool _scriptEnableCache;
        public bool Script_EnableCache
        {
            get => _scriptEnableCache;
            set
            {
                _scriptEnableCache = value;
                OnPropertyUpdate(nameof(Script_EnableCache));
            }
        }

        private bool _scriptAutoSyntaxCheck;
        public bool Script_AutoSyntaxCheck
        {
            get => _scriptAutoSyntaxCheck;
            set
            {
                _scriptAutoSyntaxCheck = value;
                OnPropertyUpdate(nameof(Script_AutoSyntaxCheck));
            }
        }
        #endregion

        #region Property - Logging
        private ObservableCollection<string> log_DebugLevelList = new ObservableCollection<string>
        {
            LogDebugLevel.Production.ToString(),
            LogDebugLevel.PrintException.ToString(),
            LogDebugLevel.PrintExceptionStackTrace.ToString()
        };
        public ObservableCollection<string> Log_DebugLevelList
        {
            get => log_DebugLevelList;
            set
            {
                log_DebugLevelList = value;
                OnPropertyUpdate(nameof(Log_DebugLevelList));
            }
        }

        private int log_DebugLevelIndex;
        public int Log_DebugLevelIndex
        {
            get => log_DebugLevelIndex;
            set
            {
                log_DebugLevelIndex = value;
                OnPropertyUpdate(nameof(Log_DebugLevelIndex));
            }
        }

        public LogDebugLevel Log_DebugLevel
        {
            get
            {
                switch (Log_DebugLevelIndex)
                {
                    case 0:
                        return LogDebugLevel.Production;
                    case 1:
                        return LogDebugLevel.PrintException;
                    default:
                        return LogDebugLevel.PrintExceptionStackTrace;
                }
            }
            set
            {
                switch (value)
                {
                    case LogDebugLevel.Production:
                        log_DebugLevelIndex = 0;
                        break;
                    case LogDebugLevel.PrintException:
                        log_DebugLevelIndex = 1;
                        break;
                    default:
                        log_DebugLevelIndex = 2;
                        break;
                }
            }
        }

        private string log_DBState;
        public string Log_DBState
        {
            get => log_DBState;
            set
            {
                log_DBState = value;
                OnPropertyUpdate(nameof(Log_DBState));
            }
        }

        private bool log_DeferredLogging;
        public bool Log_DeferredLogging
        {
            get => log_DeferredLogging;
            set
            {
                log_DeferredLogging = value;
                OnPropertyUpdate(nameof(Log_DeferredLogging));
            }
        }

        private bool log_MinifyHtmlExport;
        public bool Log_MinifyHtmlExport
        {
            get => log_MinifyHtmlExport;
            set
            {
                log_MinifyHtmlExport = value;
                OnPropertyUpdate(nameof(Log_MinifyHtmlExport));
            }
        }
        #endregion

        #region Property - Compatibility
        private bool compat_AsteriskBugDirCopy;
        public bool Compat_AsteriskBugDirCopy
        {
            get => compat_AsteriskBugDirCopy;
            set
            {
                compat_AsteriskBugDirCopy = value;
                OnPropertyUpdate(nameof(Compat_AsteriskBugDirCopy));
            }
        }

        private bool compat_AsteriskBugDirLink;
        public bool Compat_AsteriskBugDirLink
        {
            get => compat_AsteriskBugDirLink;
            set
            {
                compat_AsteriskBugDirLink = value;
                OnPropertyUpdate(nameof(Compat_AsteriskBugDirLink));
            }
        }

        private bool compat_FileRenameCanMoveDir;
        public bool Compat_FileRenameCanMoveDir
        {
            get => compat_FileRenameCanMoveDir;
            set
            {
                compat_FileRenameCanMoveDir = value;
                OnPropertyUpdate(nameof(Compat_FileRenameCanMoveDir));
            }
        }

        private bool compat_AllowLetterInLoop;
        public bool Compat_AllowLetterInLoop
        {
            get => compat_AllowLetterInLoop;
            set
            {
                compat_AllowLetterInLoop = value;
                OnPropertyUpdate(nameof(Compat_AllowLetterInLoop));
            }
        }

        private bool compat_LegacyBranchCondition;
        public bool Compat_LegacyBranchCondition
        {
            get => compat_LegacyBranchCondition;
            set
            {
                compat_LegacyBranchCondition = value;
                OnPropertyUpdate(nameof(Compat_LegacyBranchCondition));
            }
        }

        private bool compat_LegacyRegWrite;
        public bool Compat_LegacyRegWrite
        {
            get => compat_LegacyRegWrite;
            set
            {
                compat_LegacyRegWrite = value;
                OnPropertyUpdate(nameof(Compat_LegacyRegWrite));
            }
        }

        private bool compat_AllowSetModifyInterface;
        public bool Compat_AllowSetModifyInterface
        {
            get => compat_AllowSetModifyInterface;
            set
            {
                compat_AllowSetModifyInterface = value;
                OnPropertyUpdate(nameof(Compat_AllowSetModifyInterface));
            }
        }

        private bool compat_LegacyInterfaceCommand;
        public bool Compat_LegacyInterfaceCommand
        {
            get => compat_LegacyInterfaceCommand;
            set
            {
                compat_LegacyInterfaceCommand = value;
                OnPropertyUpdate(nameof(Compat_LegacyInterfaceCommand));
            }
        }

        private bool compat_LegacySectionParamCommand;
        public bool Compat_LegacySectionParamCommand
        {
            get => compat_LegacySectionParamCommand;
            set
            {
                compat_LegacySectionParamCommand = value;
                OnPropertyUpdate(nameof(Compat_LegacySectionParamCommand));
            }
        }

        private bool compat_IgnoreWidthOfWebLabel;
        public bool Compat_IgnoreWidthOfWebLabel
        {
            get => compat_IgnoreWidthOfWebLabel;
            set
            {
                compat_IgnoreWidthOfWebLabel = value;
                OnPropertyUpdate(nameof(Compat_IgnoreWidthOfWebLabel));
            }
        }

        private bool compat_OverridableFixedVariables;
        public bool Compat_OverridableFixedVariables
        {
            get => compat_OverridableFixedVariables;
            set => SetProperty(ref compat_OverridableFixedVariables, value);
        }

        private bool compat_OverridableLoopCounter;
        public bool Compat_OverridableLoopCounter
        {
            get => compat_OverridableLoopCounter;
            set => SetProperty(ref compat_OverridableLoopCounter, value);
        }

        private bool compat_EnableEnvironmentVariables;
        public bool Compat_EnableEnvironmentVariables
        {
            get => compat_EnableEnvironmentVariables;
            set => SetProperty(ref compat_EnableEnvironmentVariables, value);
        }

        private bool compat_DisableExtendedSectionParams;
        public bool Compat_DisableExtendedSectionParams
        {
            get => compat_DisableExtendedSectionParams;
            set => SetProperty(ref compat_DisableExtendedSectionParams, value);
        }
        #endregion

        #region ApplySetting
        public void ApplySetting()
        {
            // Static
            Engine.StopBuildOnError = General_StopBuildOnError;
            Logger.DebugLevel = Log_DebugLevel;
            Logger.MinifyHtmlExport = Log_MinifyHtmlExport;
            ProjectCollection.AsteriskBugDirLink = Compat_AsteriskBugDirLink;

            // Instance
            Global.MainViewModel.DisplayShellExecuteConOut = Interface_DisplayShellExecuteConOut;
            Global.MainViewModel.TitleBar = Interface_UseCustomTitle ? Interface_CustomTitle : MainViewModel.DefaultTitleBar;
        }

        public CodeParser.Options ExportCodeParserOptions()
        {
            return new CodeParser.Options
            {
                OptimizeCode = General_OptimizeCode,
                AllowLegacyBranchCondition = Compat_LegacyBranchCondition,
                AllowLegacyRegWrite = Compat_LegacyRegWrite,
                AllowLegacyInterfaceCommand = Compat_LegacyInterfaceCommand,
                AllowLegacySectionParamCommand = Compat_LegacySectionParamCommand,
                AllowExtendedSectionParams = !Compat_DisableExtendedSectionParams,
            };
        }

        public Variables.Options ExportVariablesOptions()
        {
            return new Variables.Options
            {
                OverridableFixedVariables = Compat_OverridableFixedVariables,
                EnableEnvironmentVariables = Compat_EnableEnvironmentVariables,
            };
        }
        #endregion

        #region SetToDefault
        public void SetToDefault()
        {
            // Project
            _projectDefault = string.Empty;

            // General
            General_OptimizeCode = true;
            General_ShowLogAfterBuild = true;
            General_StopBuildOnError = true;
            General_EnableLongFilePath = false;
            General_UseCustomUserAgent = false;
            // Custom User-Agent is set to Edge's on Windows 10 v1809
            General_CustomUserAgent = @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/64.0.3282.140 Safari/537.36 Edge/18.17763";

            // Interface
            // Every Windows have Consolas installed
            Interface_MonospacedFont = new FontHelper.FontInfo(new FontFamily("Consolas"), FontWeights.Regular, 12);
            Interface_ScaleFactor = 100;
            Interface_DisplayShellExecuteConOut = true;
            Interface_UseCustomEditor = false;
            Interface_CustomEditorPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "notepad.exe");
            Interface_UseCustomTitle = false;
            Interface_CustomTitle = string.Empty;

            // Script
            Script_EnableCache = true;
            Script_AutoSyntaxCheck = true;

            // Log
#if DEBUG
            Log_DebugLevelIndex = 2;
#else
            Log_DebugLevelIndex = 0;
#endif
            Log_DeferredLogging = true;
            Log_MinifyHtmlExport = true;

            // Compatibility
            Compat_AsteriskBugDirCopy = false;
            Compat_AsteriskBugDirLink = false;
            Compat_FileRenameCanMoveDir = false;
            Compat_AllowLetterInLoop = false;
            Compat_LegacyBranchCondition = false;
            Compat_LegacyRegWrite = false;
            Compat_AllowSetModifyInterface = false;
            Compat_LegacyInterfaceCommand = false;
            Compat_LegacySectionParamCommand = false;
            Compat_IgnoreWidthOfWebLabel = false;
            Compat_OverridableFixedVariables = false;
            Compat_OverridableLoopCounter = false;
            Compat_EnableEnvironmentVariables = false;
            Compat_DisableExtendedSectionParams = false;
        }
        #endregion

        #region ReadFromFile, WriteToFile
        public async Task ReadFromFileAsync()
        {
            await Task.Run(() => { ReadFromFile(); });
        }

        public void ReadFromFile()
        {
            // If key not specified or value malformed, default value will be used.
            SetToDefault();

            if (!File.Exists(_settingFile))
                return;

            const string projectStr = "Project";
            const string generalStr = "General";
            const string interfaceStr = "Interface";
            const string scriptStr = "Script";
            const string logStr = "Log";
            const string compatStr = "Compat";

            // General_CustomUserAgent
            IniKey[] keys =
            {
                new IniKey(projectStr, "DefaultProject"), // String
                new IniKey(generalStr, KeyPart(nameof(General_OptimizeCode), generalStr)), // Boolean
                new IniKey(generalStr, KeyPart(nameof(General_ShowLogAfterBuild), generalStr)), // Boolean
                new IniKey(generalStr, KeyPart(nameof(General_StopBuildOnError), generalStr)), // Boolean
                new IniKey(generalStr, KeyPart(nameof(General_EnableLongFilePath), generalStr)), // Boolean
                new IniKey(generalStr, KeyPart(nameof(General_UseCustomUserAgent), generalStr)), // Boolean
                new IniKey(generalStr, KeyPart(nameof(General_CustomUserAgent), generalStr)), // String
                new IniKey(interfaceStr, KeyPart(nameof(Interface_MonospacedFontFamily), interfaceStr)),
                new IniKey(interfaceStr, KeyPart(nameof(Interface_MonospacedFontWeight), interfaceStr)),
                new IniKey(interfaceStr, KeyPart(nameof(Interface_MonospacedFontSize), interfaceStr)),
                new IniKey(interfaceStr, KeyPart(nameof(Interface_ScaleFactor), interfaceStr)), // Integer 100 ~ 200
                new IniKey(interfaceStr, KeyPart(nameof(Interface_UseCustomEditor), interfaceStr)), // Boolean
                new IniKey(interfaceStr, KeyPart(nameof(Interface_CustomEditorPath), interfaceStr)), // String
                new IniKey(interfaceStr, KeyPart(nameof(Interface_DisplayShellExecuteConOut), interfaceStr)), // Boolean
                new IniKey(interfaceStr, KeyPart(nameof(Interface_UseCustomTitle), interfaceStr)), // Boolean
                new IniKey(interfaceStr, KeyPart(nameof(Interface_CustomTitle), interfaceStr)), // String
                new IniKey(scriptStr, KeyPart(nameof(Script_EnableCache), scriptStr)), // Boolean
                new IniKey(scriptStr, KeyPart(nameof(Script_AutoSyntaxCheck), scriptStr)), // Boolean
                new IniKey(logStr, KeyPart(nameof(Log_DebugLevel), logStr)), // Integer
                new IniKey(logStr, KeyPart(nameof(Log_DeferredLogging), logStr)), // Boolean
                new IniKey(logStr, KeyPart(nameof(Log_MinifyHtmlExport), logStr)), // Boolean
                new IniKey(compatStr, KeyPart(nameof(Compat_AsteriskBugDirCopy), compatStr)), // Boolean
                new IniKey(compatStr, KeyPart(nameof(Compat_AsteriskBugDirLink), compatStr)), // Boolean
                new IniKey(compatStr, KeyPart(nameof(Compat_FileRenameCanMoveDir), compatStr)), // Boolean
                new IniKey(compatStr, KeyPart(nameof(Compat_AllowLetterInLoop), compatStr)), // Boolean
                new IniKey(compatStr, KeyPart(nameof(Compat_LegacyBranchCondition), compatStr)), // Boolean
                new IniKey(compatStr, KeyPart(nameof(Compat_LegacyRegWrite), compatStr)), // Boolean
                new IniKey(compatStr, KeyPart(nameof(Compat_AllowSetModifyInterface), compatStr)), // Boolean
                new IniKey(compatStr, KeyPart(nameof(Compat_LegacyInterfaceCommand), compatStr)), // Boolean
                new IniKey(compatStr, KeyPart(nameof(Compat_LegacySectionParamCommand), compatStr)), // Boolean
                new IniKey(compatStr, KeyPart(nameof(Compat_IgnoreWidthOfWebLabel), compatStr)), // Boolean
                new IniKey(compatStr, KeyPart(nameof(Compat_OverridableFixedVariables), compatStr)), // Boolean
                new IniKey(compatStr, KeyPart(nameof(Compat_OverridableLoopCounter), compatStr)), // Boolean
                new IniKey(compatStr, KeyPart(nameof(Compat_EnableEnvironmentVariables), compatStr)), // Boolean
                new IniKey(compatStr, KeyPart(nameof(Compat_DisableExtendedSectionParams), compatStr)), // Boolean
            };

            keys = IniReadWriter.ReadKeys(_settingFile, keys);
            Dictionary<string, string> dict = keys.ToDictionary(x => $"{x.Section}_{x.Key}", x => x.Value);

            #region Parse Helpers
            (string Section, string Key) SplitSectionKey(string varName)
            {
                int sIdx = varName.IndexOf('_');
                string section = varName.Substring(0, sIdx);
                string key = varName.Substring(sIdx + 1);
                return (section, key);
            }

            string ParseString(string varName, string defaultValue) => dict[varName] ?? defaultValue;

            bool ParseBoolean(string varName, bool defaultValue)
            {
                string valStr = dict[varName];
                if (valStr == null) // No warning, just use default value
                    return defaultValue;

                if (valStr.Equals("True", StringComparison.OrdinalIgnoreCase))
                    return true;
                if (valStr.Equals("False", StringComparison.OrdinalIgnoreCase))
                    return false;

                (string section, string key) = SplitSectionKey(varName);
                Global.Logger.SystemWrite(new LogInfo(LogState.Error, $"Setting [{section}.{key}] has wrong value: {valStr}"));
                return defaultValue;
            }

            int ParseInteger(string varName, int defaultValue, int min, int max)
            {
                string valStr = dict[varName];
                if (valStr == null) // No warning, just use default value
                    return defaultValue;

                if (int.TryParse(valStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int valInt))
                {
                    if (min == -1)
                    { // No Min
                        if (max == -1) // No Max
                            return valInt;
                        if (valInt <= max) // Have Min
                            return valInt;
                    }
                    else
                    { // Have Min
                        if (max == -1 && min <= valInt) // No Max
                            return valInt;
                        if (min <= valInt && valInt <= max) // Have Min
                            return valInt;
                    }
                }

                (string section, string key) = SplitSectionKey(varName);
                Global.Logger.SystemWrite(new LogInfo(LogState.Error, $"Setting [{section}.{key}] has wrong value: {valStr}"));
                return defaultValue;
            }
            #endregion

            // Project
            if (dict["Project_DefaultProject"] != null)
                _projectDefault = dict["Project_DefaultProject"];

            // General
            General_OptimizeCode = ParseBoolean(nameof(General_OptimizeCode), General_OptimizeCode);
            General_ShowLogAfterBuild = ParseBoolean(nameof(General_ShowLogAfterBuild), General_ShowLogAfterBuild);
            General_StopBuildOnError = ParseBoolean(nameof(General_StopBuildOnError), General_StopBuildOnError);
            General_EnableLongFilePath = ParseBoolean(nameof(General_EnableLongFilePath), General_EnableLongFilePath);
            General_UseCustomUserAgent = ParseBoolean(nameof(General_UseCustomUserAgent), General_UseCustomUserAgent);
            General_CustomUserAgent = ParseString(nameof(General_CustomUserAgent), General_CustomUserAgent);

            // Interface
            FontFamily monoFontFamiliy = Interface_MonospacedFont.FontFamily;
            FontWeight monoFontWeight = Interface_MonospacedFont.FontWeight;
            if (dict[nameof(Interface_MonospacedFontFamily)] != null)
                monoFontFamiliy = new FontFamily(dict[nameof(Interface_MonospacedFontFamily)]);
            if (dict[nameof(Interface_MonospacedFontWeight)] != null)
                monoFontWeight = FontHelper.ParseFontWeight(dict[nameof(Interface_MonospacedFontWeight)]);
            int monoFontSize = ParseInteger(nameof(Interface_MonospacedFontSize), Interface_MonospacedFont.FontSizeInPoint, 1, -1);
            Interface_MonospacedFont = new FontHelper.FontInfo(monoFontFamiliy, monoFontWeight, monoFontSize);

            Interface_ScaleFactor = ParseInteger(nameof(Interface_ScaleFactor), (int)Interface_ScaleFactor, 100, 200);
            Interface_UseCustomEditor = ParseBoolean(nameof(Interface_UseCustomEditor), Interface_UseCustomEditor);
            Interface_CustomEditorPath = ParseString(nameof(Interface_CustomEditorPath), Interface_CustomEditorPath);
            Interface_DisplayShellExecuteConOut = ParseBoolean(nameof(Interface_DisplayShellExecuteConOut), Interface_DisplayShellExecuteConOut);
            Interface_UseCustomTitle = ParseBoolean(nameof(Interface_UseCustomTitle), Interface_UseCustomTitle);
            Interface_CustomTitle = ParseString(nameof(Interface_CustomTitle), Interface_CustomTitle);

            // Script
            Script_EnableCache = ParseBoolean(nameof(Script_EnableCache), Script_EnableCache);
            Script_AutoSyntaxCheck = ParseBoolean(nameof(Script_AutoSyntaxCheck), Script_AutoSyntaxCheck);

            // Log
            Log_DebugLevelIndex = ParseInteger(nameof(Log_DebugLevel), Log_DebugLevelIndex, 0, 2);
            Log_DeferredLogging = ParseBoolean(nameof(Log_DeferredLogging), Log_DeferredLogging);
            Log_MinifyHtmlExport = ParseBoolean(nameof(Log_MinifyHtmlExport), Log_MinifyHtmlExport);

            // Compatibility
            Compat_AsteriskBugDirCopy = ParseBoolean(nameof(Compat_AsteriskBugDirCopy), Compat_AsteriskBugDirCopy);
            Compat_AsteriskBugDirLink = ParseBoolean(nameof(Compat_AsteriskBugDirLink), Compat_AsteriskBugDirLink);
            Compat_FileRenameCanMoveDir = ParseBoolean(nameof(Compat_FileRenameCanMoveDir), Compat_FileRenameCanMoveDir);
            Compat_AllowLetterInLoop = ParseBoolean(nameof(Compat_AllowLetterInLoop), Compat_AllowLetterInLoop);
            Compat_LegacyBranchCondition = ParseBoolean(nameof(Compat_LegacyBranchCondition), Compat_LegacyBranchCondition);
            Compat_LegacyRegWrite = ParseBoolean(nameof(Compat_LegacyRegWrite), Compat_LegacyRegWrite);
            Compat_AllowSetModifyInterface = ParseBoolean(nameof(Compat_AllowSetModifyInterface), Compat_AllowSetModifyInterface);
            Compat_LegacyInterfaceCommand = ParseBoolean(nameof(Compat_LegacyInterfaceCommand), Compat_LegacyInterfaceCommand);
            Compat_LegacySectionParamCommand = ParseBoolean(nameof(Compat_LegacySectionParamCommand), Compat_LegacySectionParamCommand);
            Compat_IgnoreWidthOfWebLabel = ParseBoolean(nameof(Compat_IgnoreWidthOfWebLabel), Compat_IgnoreWidthOfWebLabel);
            Compat_OverridableFixedVariables = ParseBoolean(nameof(Compat_OverridableFixedVariables), Compat_OverridableFixedVariables);
            Compat_OverridableLoopCounter = ParseBoolean(nameof(Compat_OverridableLoopCounter), Compat_OverridableLoopCounter);
            Compat_EnableEnvironmentVariables = ParseBoolean(nameof(Compat_EnableEnvironmentVariables), Compat_EnableEnvironmentVariables);
            Compat_DisableExtendedSectionParams = ParseBoolean(nameof(Compat_DisableExtendedSectionParams), Compat_DisableExtendedSectionParams);
        }

        public async Task WriteToFileAsync()
        {
            await Task.Run(() => { WriteToFile(); });
        }

        public void WriteToFile()
        {
            const string generalStr = "General";
            const string projectStr = "Project";
            const string interfaceStr = "Interface";
            const string scriptStr = "Script";
            const string logStr = "Log";
            const string compatStr = "Compat";

            IniKey[] keys =
            {
                new IniKey(generalStr, KeyPart(nameof(General_EnableLongFilePath), generalStr), General_EnableLongFilePath.ToString()), // Boolean
                new IniKey(generalStr, KeyPart(nameof(General_OptimizeCode), generalStr), General_OptimizeCode.ToString()), // Boolean
                new IniKey(generalStr, KeyPart(nameof(General_ShowLogAfterBuild), generalStr), General_ShowLogAfterBuild.ToString()), // Boolean
                new IniKey(generalStr, KeyPart(nameof(General_StopBuildOnError), generalStr), General_StopBuildOnError.ToString()), // Boolean
                new IniKey(generalStr, KeyPart(nameof(General_UseCustomUserAgent), generalStr), General_UseCustomUserAgent.ToString()), // Boolean
                new IniKey(generalStr, KeyPart(nameof(General_CustomUserAgent), generalStr), General_CustomUserAgent), // String
                new IniKey(interfaceStr, KeyPart(nameof(Interface_MonospacedFontFamily), interfaceStr), Interface_MonospacedFont.FontFamily.Source),
                new IniKey(interfaceStr, KeyPart(nameof(Interface_MonospacedFontWeight), interfaceStr), Interface_MonospacedFont.FontWeight.ToString()),
                new IniKey(interfaceStr, KeyPart(nameof(Interface_MonospacedFontSize), interfaceStr), Interface_MonospacedFont.FontSizeInPoint.ToString()),
                new IniKey(interfaceStr, KeyPart(nameof(Interface_ScaleFactor), interfaceStr), Interface_ScaleFactor.ToString(CultureInfo.InvariantCulture)), // Integer
                new IniKey(interfaceStr, KeyPart(nameof(Interface_UseCustomEditor), interfaceStr), Interface_UseCustomEditor.ToString()), // Boolean
                new IniKey(interfaceStr, KeyPart(nameof(Interface_CustomEditorPath), interfaceStr), Interface_CustomEditorPath), // String
                new IniKey(interfaceStr, KeyPart(nameof(Interface_DisplayShellExecuteConOut), interfaceStr), Interface_DisplayShellExecuteConOut.ToString()), // Boolean
                new IniKey(interfaceStr, KeyPart(nameof(Interface_UseCustomTitle), interfaceStr), Interface_UseCustomTitle.ToString()), // Boolean
                new IniKey(interfaceStr, KeyPart(nameof(Interface_CustomTitle), interfaceStr), Interface_CustomTitle), // String
                new IniKey(scriptStr, KeyPart(nameof(Script_EnableCache), scriptStr), Script_EnableCache.ToString()), // Boolean
                new IniKey(scriptStr, KeyPart(nameof(Script_AutoSyntaxCheck), scriptStr), Script_AutoSyntaxCheck.ToString()), // Boolean
                new IniKey(logStr, KeyPart(nameof(Log_DebugLevel), logStr), Log_DebugLevelIndex.ToString()), // Integer
                new IniKey(logStr, KeyPart(nameof(Log_DeferredLogging), logStr), Log_DeferredLogging.ToString()), // Boolean
                new IniKey(logStr, KeyPart(nameof(Log_MinifyHtmlExport), logStr), Log_MinifyHtmlExport.ToString()), // Boolean
                new IniKey(projectStr, "DefaultProject", Project_Default), // String
                new IniKey(compatStr, KeyPart(nameof(Compat_AsteriskBugDirCopy), compatStr), Compat_AsteriskBugDirCopy.ToString()), // Boolean
                new IniKey(compatStr, KeyPart(nameof(Compat_AsteriskBugDirLink), compatStr), Compat_AsteriskBugDirLink.ToString()), // Boolean
                new IniKey(compatStr, KeyPart(nameof(Compat_FileRenameCanMoveDir), compatStr), Compat_FileRenameCanMoveDir.ToString()), // Boolean
                new IniKey(compatStr, KeyPart(nameof(Compat_AllowLetterInLoop), compatStr), Compat_AllowLetterInLoop.ToString()), // Boolean
                new IniKey(compatStr, KeyPart(nameof(Compat_LegacyBranchCondition), compatStr), Compat_LegacyBranchCondition.ToString()), // Boolean
                new IniKey(compatStr, KeyPart(nameof(Compat_LegacyRegWrite), compatStr), Compat_LegacyRegWrite.ToString()), // Boolean
                new IniKey(compatStr, KeyPart(nameof(Compat_AllowSetModifyInterface), compatStr), Compat_AllowSetModifyInterface.ToString()), // Boolean
                new IniKey(compatStr, KeyPart(nameof(Compat_LegacyInterfaceCommand), compatStr), Compat_LegacyInterfaceCommand.ToString()), // Boolean
                new IniKey(compatStr, KeyPart(nameof(Compat_LegacySectionParamCommand), compatStr), Compat_LegacySectionParamCommand.ToString()), // Boolean
                new IniKey(compatStr, KeyPart(nameof(Compat_IgnoreWidthOfWebLabel), compatStr), Compat_IgnoreWidthOfWebLabel.ToString()), // Boolean
                new IniKey(compatStr, KeyPart(nameof(Compat_OverridableFixedVariables), compatStr), Compat_OverridableFixedVariables.ToString()), // Boolean
                new IniKey(compatStr, KeyPart(nameof(Compat_OverridableLoopCounter), compatStr), Compat_OverridableLoopCounter.ToString()), // Boolean
                new IniKey(compatStr, KeyPart(nameof(Compat_EnableEnvironmentVariables), compatStr), Compat_EnableEnvironmentVariables.ToString()), // Boolean
                new IniKey(compatStr, KeyPart(nameof(Compat_DisableExtendedSectionParams), compatStr), Compat_DisableExtendedSectionParams.ToString()), // Boolean
            };
            IniReadWriter.WriteKeys(_settingFile, keys);
        }

        private static string KeyPart(string str, string section)
        {
            return str.Substring(section.Length + 1);
        }
        #endregion

        #region ShallowCopy
        public SettingViewModel ShallowCopy() => MemberwiseClone() as SettingViewModel;
        #endregion

        #region Database Operation
        public void ClearLogDatabase()
        {
            LogDb.ClearTable(new LogDatabase.ClearTableOptions
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
            if (ScriptCache != null)
            {
                ScriptCache.ClearTable(new ScriptCache.ClearTableOptions
                {
                    ScriptCache = true,
                });
                UpdateCacheDbState();
            }
        }

        public void UpdateLogDbState()
        {
            int systemLogCount = LogDb.Table<DB_SystemLog>().Count();
            int codeLogCount = LogDb.Table<DB_BuildLog>().Count();
            Log_DBState = $"{systemLogCount} System Logs, {codeLogCount} Build Logs";
        }

        public void UpdateCacheDbState()
        {
            if (ScriptCache == null)
            {
                Script_CacheState = "Cache not enabled";
            }
            else
            {
                int cacheCount = ScriptCache.Table<DB_ScriptCache>().Count();
                Script_CacheState = $"{cacheCount} scripts cached";
            }
        }
        #endregion

        #region UpdateProjectList
        public void UpdateProjectList()
        {
            Projects = Global.Projects;
            Application.Current?.Dispatcher.Invoke(() =>
            {
                bool foundDefault = false;
                List<string> projNameList = Projects.ProjectNames;
                Project_List = new ObservableCollection<string>();
                for (int i = 0; i < projNameList.Count; i++)
                {
                    Project_List.Add(projNameList[i]);
                    if (projNameList[i].Equals(_projectDefault, StringComparison.OrdinalIgnoreCase))
                    {
                        foundDefault = true;
                        Project_SelectedIndex = Project_DefaultIndex = i;
                    }
                }

                if (!foundDefault)
                    Project_SelectedIndex = Project_DefaultIndex = Projects.Count - 1;
            });
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
        public static readonly RoutedCommand SelectMonospacedFontCommand = new RoutedUICommand("Select monospaced font", "SelectMonospacedFont", typeof(SettingViewCommands));
        public static readonly RoutedCommand SelectCustomEditorPathCommand = new RoutedUICommand("Select custom editor path", "SelectCustomEditorPath", typeof(SettingViewCommands));
        #endregion

        #region Script Setting
        public static readonly RoutedCommand ClearCacheDatabaseCommand = new RoutedUICommand("Clear cache database", "ClearCacheDatabase", typeof(SettingViewCommands));
        #endregion

        #region Log Setting
        public static readonly RoutedCommand ClearLogDatabaseCommand = new RoutedUICommand("Clear log database", "ClearLogDatabase", typeof(SettingViewCommands));
        #endregion
    }
    #endregion
}
