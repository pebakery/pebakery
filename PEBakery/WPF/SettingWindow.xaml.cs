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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace PEBakery.WPF
{
    #region SettingWindow
    // ReSharper disable once RedundantExtendsListEntry
    public partial class SettingWindow : Window
    {
        #region Field and Constructor
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
            _m.UpdateProjectNames();
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
                const string msg = "All settings will be reset to default!\r\nDo you really want to continue?";
                MessageBoxResult res = MessageBox.Show(msg, "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (res == MessageBoxResult.Yes)
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
                await Task.Run(() =>
                {
                    _m.WriteToFile();
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
                string fullPath = Global.Projects[idx].MainScript.RealPath;
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
        private void SelectMonospacedFontCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            _m.CanExecuteCommand = false;
            try
            {
                _m.Setting.Interface.MonospacedFont = FontHelper.ChooseFontDialog(_m.Setting.Interface.MonospacedFont, this, monospaced: true);
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
                    _m.Setting.Interface.CustomEditorPath = dialog.FileName;
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
    public class SettingViewModel : ViewModelBase
    {
        #region Field and Constructor
        public Setting Setting { get; }

        public SettingViewModel(Setting setting)
        {
            Setting = setting;
            ProjectSourceDirs = new ObservableCollection<string>();

            ReadFromFile();

            ApplySetting();
        }
        #endregion

        #region CanExecuteCommand
        public bool CanExecuteCommand { get; set; } = true;
        #endregion

        #region Property - Project
        private ObservableCollection<string> _projectNames = new ObservableCollection<string>();
        public ObservableCollection<string> ProjectNames
        {
            get => _projectNames;
            set => SetProperty(ref _projectNames, value);
        }

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
                if (0 <= SelectedProjectIndex && SelectedProjectIndex < ProjectNames.Count)
                    return Global.Projects[DefaultProjectIndex];
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
                Project oldProject = SelectedProject;
                _selectProjectIndex = value;
                LoadSelectedProject(value, oldProject);
                OnPropertyUpdate(nameof(SelectedProjectIndex));
                OnPropertyUpdate(nameof(SelectedProject));
            }
        }

        public async void LoadSelectedProject(int newValue, Project oldProject)
        {
            if (newValue < 0 || ProjectNames.Count <= newValue)
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
                SaveCompatOption(oldProject.Compat);
                LoadCompatOption(SelectedProject.Compat);
            });
        }

        public Project SelectedProject
        {
            get
            {
                if (0 <= SelectedProjectIndex && SelectedProjectIndex < ProjectNames.Count)
                    return Global.Projects[SelectedProjectIndex];
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

                Project p = DefaultProject;
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
        public bool GeneralOptimizeCode
        {
            get => Setting.General.OptimizeCode;
            set => SetProperty(ref Setting.General.OptimizeCode, value);
        }

        public bool GeneralShowLogAfterBuild
        {
            get => Setting.General.ShowLogAfterBuild;
            set => SetProperty(ref Setting.General.ShowLogAfterBuild, value);
        }

        public bool GeneralStopBuildOnError
        {
            get => Setting.General.StopBuildOnError;
            set => SetProperty(ref Setting.General.StopBuildOnError, value);
        }

        public bool GeneralEnableLongFilePath
        {
            get => Setting.General.EnableLongFilePath;
            set => SetProperty(ref Setting.General.EnableLongFilePath, value);
        }

        public bool GeneralUseCustomUserAgent
        {
            get => Setting.General.UseCustomUserAgent;
            set => SetProperty(ref Setting.General.UseCustomUserAgent, value);
        }

        public string GeneralCustomUserAgent
        {
            get => Setting.General.CustomUserAgent;
            set => SetProperty(ref Setting.General.CustomUserAgent, value);
        }
        #endregion

        #region Property - Interface
        public FontHelper.FontInfo InterfaceMonospacedFont
        {
            get => Setting.Interface.MonospacedFont;
            set => SetProperty(ref Setting.Interface.MonospacedFont, value);
        }

        public double InterfaceScaleFactor
        {
            get => Setting.Interface.ScaleFactor;
            set
            {
                Setting.Interface.ScaleFactor = (int)value;
                OnPropertyUpdate(nameof(InterfaceScaleFactor));
            }
        }

        public bool InterfaceUseCustomEditor
        {
            get => Setting.Interface.UseCustomEditor;
            set => SetProperty(ref Setting.Interface.UseCustomEditor, value);
        }

        public string InterfaceCustomEditorPath
        {
            get => Setting.Interface.CustomEditorPath;
            set => SetProperty(ref Setting.Interface.CustomEditorPath, value);
        }

        public bool InterfaceDisplayShellExecuteConOut
        {
            get => Setting.Interface.DisplayShellExecuteConOut;
            set => SetProperty(ref Setting.Interface.DisplayShellExecuteConOut, value);
        }

        public bool InterfaceUseCustomTitle
        {
            get => Setting.Interface.UseCustomTitle;
            set => SetProperty(ref Setting.Interface.UseCustomTitle, value);
        }

        public string InterfaceCustomTitle
        {
            get => Setting.Interface.CustomTitle;
            set => SetProperty(ref Setting.Interface.CustomTitle, value);
        }
        #endregion

        #region Property - Script
        private string _scriptCacheState;
        public string ScriptCacheState
        {
            get => _scriptCacheState;
            set => SetProperty(ref _scriptCacheState, value);
        }

        public bool ScriptEnableCache
        {
            get => Setting.Script.EnableCache;
            set => SetProperty(ref Setting.Script.EnableCache, value);
        }

        public bool ScriptAutoSyntaxCheck
        {
            get => Setting.Script.AutoSyntaxCheck;
            set => SetProperty(ref Setting.Script.AutoSyntaxCheck, value);
        }
        #endregion

        #region Property - Logging
        private string _logDatabaseState;
        public string LogDatabaseState
        {
            get => _logDatabaseState;
            set => SetProperty(ref _logDatabaseState, value);
        }

        public ObservableCollection<string> LogDebugLevels { get; } = new ObservableCollection<string>
        {
            LogDebugLevel.Production.ToString(),
            LogDebugLevel.PrintException.ToString(),
            LogDebugLevel.PrintExceptionStackTrace.ToString()
        };

        public int LogDebugLevelIndex
        {
            get => (int)Setting.Log.DebugLevel;
            set
            {
                Setting.Log.DebugLevel = (LogDebugLevel)value;
                OnPropertyUpdate(nameof(LogDebugLevelIndex));
            }
        }

        public bool LogDeferredLogging
        {
            get => Global.Setting.Log.DeferredLogging;
            set => SetProperty(ref Global.Setting.Log.DeferredLogging, value);
        }

        public bool LogMinifyHtmlExport
        {
            get => Global.Setting.Log.MinifyHtmlExport;
            set => SetProperty(ref Global.Setting.Log.MinifyHtmlExport, value);
        }
        #endregion

        #region Property - Compatibility
        // Asterisk
        private bool _compatAsteriskBugDirCopy;
        public bool CompatAsteriskBugDirCopy
        {
            get => _compatAsteriskBugDirCopy;
            set => SetProperty(ref _compatAsteriskBugDirCopy, value);
        }

        private bool _compatAsteriskBugDirLink;
        public bool CompatAsteriskBugDirLink
        {
            get => _compatAsteriskBugDirLink;
            set => SetProperty(ref _compatAsteriskBugDirLink, value);
        }

        // Command
        private bool _compatFileRenameCanMoveDir;
        public bool CompatFileRenameCanMoveDir
        {
            get => _compatFileRenameCanMoveDir;
            set => SetProperty(ref _compatFileRenameCanMoveDir, value);
        }

        private bool _compatAllowLetterInLoop;
        public bool CompatAllowLetterInLoop
        {
            get => _compatAllowLetterInLoop;
            set => SetProperty(ref _compatAllowLetterInLoop, value);
        }

        private bool _compatLegacyBranchCondition;
        public bool CompatLegacyBranchCondition
        {
            get => _compatLegacyBranchCondition;
            set => SetProperty(ref _compatLegacyBranchCondition, value);
        }

        private bool _compatLegacyRegWrite;
        public bool CompatLegacyRegWrite
        {
            get => _compatLegacyRegWrite;
            set => SetProperty(ref _compatLegacyRegWrite, value);
        }

        private bool _compatAllowSetModifyInterface;
        public bool CompatAllowSetModifyInterface
        {
            get => _compatAllowSetModifyInterface;
            set => SetProperty(ref _compatAllowSetModifyInterface, value);
        }

        private bool _compatLegacyInterfaceCommand;
        public bool CompatLegacyInterfaceCommand
        {
            get => _compatLegacyInterfaceCommand;
            set => SetProperty(ref _compatLegacyInterfaceCommand, value);
        }

        private bool _compatLegacySectionParamCommand;
        public bool CompatLegacySectionParamCommand
        {
            get => _compatLegacySectionParamCommand;
            set => SetProperty(ref _compatLegacySectionParamCommand, value);
        }

        // Script Interface
        private bool _compatIgnoreWidthOfWebLabel;
        public bool CompatIgnoreWidthOfWebLabel
        {
            get => _compatIgnoreWidthOfWebLabel;
            set => SetProperty(ref _compatIgnoreWidthOfWebLabel, value);
        }

        // Variable
        private bool _compatOverridableFixedVariables;
        public bool CompatOverridableFixedVariables
        {
            get => _compatOverridableFixedVariables;
            set => SetProperty(ref _compatOverridableFixedVariables, value);
        }

        private bool _compatOverridableLoopCounter;
        public bool CompatOverridableLoopCounter
        {
            get => _compatOverridableLoopCounter;
            set => SetProperty(ref _compatOverridableLoopCounter, value);
        }

        private bool _compatEnableEnvironmentVariables;
        public bool CompatEnableEnvironmentVariables
        {
            get => _compatEnableEnvironmentVariables;
            set => SetProperty(ref _compatEnableEnvironmentVariables, value);
        }

        private bool _compatDisableExtendedSectionParams;
        public bool CompatDisableExtendedSectionParams
        {
            get => _compatDisableExtendedSectionParams;
            set => SetProperty(ref _compatDisableExtendedSectionParams, value);
        }
        #endregion

        #region LoadCompatOption, SaveCompatOption
        public void LoadCompatOption(CompatOption compat)
        {
            // Asterisk
            CompatAsteriskBugDirCopy = compat.AsteriskBugDirCopy;
            CompatAsteriskBugDirLink = compat.AsteriskBugDirLink;
            // Command
            CompatFileRenameCanMoveDir = compat.FileRenameCanMoveDir;
            CompatAllowLetterInLoop = compat.AllowLetterInLoop;
            CompatLegacyBranchCondition = compat.LegacyBranchCondition;
            CompatLegacyRegWrite = compat.LegacyRegWrite;
            CompatAllowSetModifyInterface = compat.AllowSetModifyInterface;
            CompatLegacyInterfaceCommand = compat.LegacyInterfaceCommand;
            CompatLegacySectionParamCommand = compat.LegacySectionParamCommand;
            // Script Interface
            CompatIgnoreWidthOfWebLabel = compat.IgnoreWidthOfWebLabel;
            // Variable
            CompatOverridableFixedVariables = compat.OverridableFixedVariables;
            CompatOverridableLoopCounter = compat.OverridableLoopCounter;
            CompatEnableEnvironmentVariables = compat.EnableEnvironmentVariables;
            CompatDisableExtendedSectionParams = compat.DisableExtendedSectionParams;
        }

        public void SaveCompatOption(CompatOption compat)
        {
            // Asterisk
            compat.AsteriskBugDirCopy = CompatAsteriskBugDirCopy;
            compat.AsteriskBugDirLink = CompatAsteriskBugDirLink;
            // Command
            compat.FileRenameCanMoveDir = CompatFileRenameCanMoveDir;
            compat.AllowLetterInLoop = CompatAllowLetterInLoop;
            compat.LegacyBranchCondition = CompatLegacyBranchCondition;
            compat.LegacyRegWrite = CompatLegacyRegWrite;
            compat.AllowSetModifyInterface = CompatAllowSetModifyInterface;
            compat.LegacyInterfaceCommand = CompatLegacyInterfaceCommand;
            compat.LegacySectionParamCommand = CompatLegacySectionParamCommand;
            // Script Interface
            compat.IgnoreWidthOfWebLabel = CompatIgnoreWidthOfWebLabel;
            // Variable
            compat.OverridableFixedVariables = CompatOverridableFixedVariables;
            compat.OverridableLoopCounter = CompatOverridableLoopCounter;
            compat.EnableEnvironmentVariables = CompatEnableEnvironmentVariables;
            compat.DisableExtendedSectionParams = CompatDisableExtendedSectionParams;
        }
        #endregion

        #region SetToDefault
        public void SetToDefault()
        {
            Setting.SetToDefault();
            foreach (Project p in Global.Projects)
                p.Compat.SetToDefault();
        }
        #endregion

        #region ApplySetting
        public void ApplySetting()
        {
            Setting.ApplySetting();
        }
        #endregion

        #region ReadFromFile, WriteToFile
        public void ReadFromFile()
        {
            Setting.ReadFromFile();
            foreach (Project p in Global.Projects)
                p.Compat.ReadFromFile();
        }

        public void WriteToFile()
        {
            Setting.WriteToFile();
            SaveCompatOption(SelectedProject.Compat);
            foreach (Project p in Global.Projects)
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
            int systemLogCount = Global.Logger.Db.Table<DB_SystemLog>().Count();
            int codeLogCount = Global.Logger.Db.Table<DB_BuildLog>().Count();
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
                int cacheCount = Global.ScriptCache.Table<DB_ScriptCache>().Count();
                ScriptCacheState = $"{cacheCount} scripts cached";
            }
        }
        #endregion

        #region UpdateProjectList
        public void UpdateProjectNames()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                bool foundDefault = false;
                List<string> pNames = Global.Projects.ProjectNames;
                ProjectNames.Clear();
                for (int i = 0; i < pNames.Count; i++)
                {
                    ProjectNames.Add(pNames[i]);
                    if (pNames[i].Equals(Setting.Project.DefaultProject, StringComparison.OrdinalIgnoreCase))
                    {
                        foundDefault = true;
                        SelectedProjectIndex = DefaultProjectIndex = i;
                    }
                }

                if (!foundDefault)
                    SelectedProjectIndex = DefaultProjectIndex = Global.Projects.Count - 1;
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
