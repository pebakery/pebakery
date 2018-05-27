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

using PEBakery.IniLib;
using PEBakery.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
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
using PEBakery.Helper;
using System.Threading;
using System.Collections.ObjectModel;
using System.Drawing.Text;
using PEBakery.WPF.Controls;
using Ookii.Dialogs.Wpf;

namespace PEBakery.WPF
{
    #region SettingWindow
    public partial class SettingWindow : Window
    {
        #region Field and Constructor
        public SettingViewModel Model;

        public SettingWindow(SettingViewModel model)
        {
            Model = model;
            DataContext = Model;
            InitializeComponent();
        }
        #endregion

        #region Button Event Handler
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            Model.WriteToFile();
            DialogResult = true;
        }

        private void DefaultButton_Click(object sender, RoutedEventArgs e)
        {
            Model.SetToDefault();
        }

        private void Button_ClearCache_Click(object sender, RoutedEventArgs e)
        {
            if (ScriptCache.dbLock == 0)
            {
                Interlocked.Increment(ref ScriptCache.dbLock);
                try
                {
                    Model.ClearCacheDB();
                }
                finally
                {
                    Interlocked.Decrement(ref ScriptCache.dbLock);
                }
            }
        }

        private void Button_ClearLog_Click(object sender, RoutedEventArgs e)
        {
            Model.ClearLogDB();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Model.UpdateCacheDBState();
            Model.UpdateLogDBState();
            Model.UpdateProjectList();
        }

        private void CheckBox_EnableLongFilePath_Click(object sender, RoutedEventArgs e)
        {
            if (Model.General_EnableLongFilePath)
            {
                const string msg = "Enabling this option may cause problems!\r\nDo you really want to continue?";
                MessageBoxResult res = MessageBox.Show(msg, "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                Model.General_EnableLongFilePath = res != MessageBoxResult.Yes;
            }
        }

        private void Button_SourceDirectory_Click(object sender, RoutedEventArgs e)
        {
            VistaFolderBrowserDialog dialog = new VistaFolderBrowserDialog();
            if (0 < Model.Project_SourceDirectoryList.Count)
                dialog.SelectedPath = Model.Project_SourceDirectoryList[Model.Project_SourceDirectoryIndex];

            if (dialog.ShowDialog(this) == true)
            {
                bool exist = false;
                for (int i = 0; i < Model.Project_SourceDirectoryList.Count; i++)
                {
                    string projName = Model.Project_SourceDirectoryList[i];
                    if (projName.Equals(dialog.SelectedPath, StringComparison.OrdinalIgnoreCase))
                    { // Selected Path exists
                        exist = true;
                        Model.Project_SourceDirectoryIndex = i;
                        break;
                    }
                }

                if (!exist) // Add to list
                {
                    ObservableCollection<string> newSourceDirList = new ObservableCollection<string>
                    {
                        dialog.SelectedPath
                    };
                    foreach (string dir in Model.Project_SourceDirectoryList)
                        newSourceDirList.Add(dir);
                    Model.Project_SourceDirectoryList = newSourceDirList;
                    Model.Project_SourceDirectoryIndex = 0;
                }
            }
        }

        private void Button_ResetSourceDirectory_Click(object sender, RoutedEventArgs e)
        {
            Model.Project_SourceDirectoryList = new ObservableCollection<string>();

            int idx = Model.Project_SelectedIndex;
            string fullPath = Model.Projects[idx].MainScript.RealPath;
            Ini.WriteKey(fullPath, "Main", "SourceDir", string.Empty);
        }

        private void Button_TargetDirectory_Click(object sender, RoutedEventArgs e)
        {
            VistaFolderBrowserDialog dialog = new VistaFolderBrowserDialog()
            {
                SelectedPath = Model.Project_TargetDirectory,
            };

            if (dialog.ShowDialog(this) == true)
            {
                Model.Project_TargetDirectory = dialog.SelectedPath;
            }
        }

        private void Button_ISOFile_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog dialog = new Microsoft.Win32.SaveFileDialog()
            {
                Filter = "ISO File (*.iso)|*.iso",
                FileName = Model.Project_ISOFile,
            };

            if (dialog.ShowDialog(this) == true)
            {
                Model.Project_ISOFile = dialog.FileName;
            }
        }

        private void Button_MonospaceFont_Click(object sender, RoutedEventArgs e)
        {
            Model.Interface_MonospaceFont = FontHelper.ChooseFontDialog(Model.Interface_MonospaceFont, this, false, true);
        }

        private void Button_CustomEditorPath_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog()
            {
                Filter = "Executable|*.exe",
                FileName = Model.Interface_CustomEditorPath,
            };

            if (dialog.ShowDialog(this) == true)
            {
                Model.Interface_CustomEditorPath = dialog.FileName;
            }
        }
        #endregion
    }
    #endregion

    #region SettingViewModel
    public class SettingViewModel : INotifyPropertyChanged
    {
        #region Field and Constructor
        private readonly string _settingFile;
        public LogDatabase LogDB { get; set; }
        public ScriptCache CacheDB { get; set; }
        public ProjectCollection Projects { get; private set; }

        public SettingViewModel(string settingFile)
        {
            _settingFile = settingFile;
            ReadFromFile();

            ApplySetting();
        }
        #endregion

        #region Property - Project
        private string Project_DefaultStr;
        public string Project_Default => Project_List[project_DefaultIndex];

        private ObservableCollection<string> project_List;
        public ObservableCollection<string> Project_List
        {
            get => project_List;
            set
            {
                project_List = value;
                OnPropertyUpdate(nameof(Project_List));
            }
        }

        private int project_DefaultIndex;
        public int Project_DefaultIndex
        {
            get => project_DefaultIndex;
            set
            {
                project_DefaultIndex = value;
                OnPropertyUpdate(nameof(Project_DefaultIndex));
            }
        }

        private int project_SelectedIndex;
        public int Project_SelectedIndex
        {
            get => project_SelectedIndex;
            set
            {
                project_SelectedIndex = value;
                
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
                    keys = Ini.ReadKeys(fullPath, keys);

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
                        project_SourceDirectoryIndex = 0;
                        OnPropertyUpdate(nameof(Project_SourceDirectoryIndex));
                    }

                    if (keys[1].Value != null)
                    {
                        project_TargetDirectory = keys[1].Value;
                        OnPropertyUpdate(nameof(Project_TargetDirectory));
                    }
                    
                    if (keys[2].Value != null)
                    {
                        project_ISOFile = keys[2].Value;
                        OnPropertyUpdate(nameof(Project_ISOFile));
                    }
                }

                OnPropertyUpdate(nameof(Project_SelectedIndex));
            }
        }

        private bool project_PathEnabled = true;
        public bool Project_PathEnabled
        {
            get => project_PathEnabled;
            set
            {
                project_PathEnabled = value;
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

        private int project_SourceDirectoryIndex;
        public int Project_SourceDirectoryIndex
        {
            get => project_SourceDirectoryIndex;
            set
            {
                project_SourceDirectoryIndex = value;

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
                    Ini.WriteKey(project.MainScript.RealPath, "Main", "SourceDir", b.ToString());
                }
                
                OnPropertyUpdate(nameof(Project_SourceDirectoryIndex));
            }
        }

        private string project_TargetDirectory;
        public string Project_TargetDirectory
        {
            get => project_TargetDirectory;
            set
            {
                if (value.Equals(project_TargetDirectory, StringComparison.OrdinalIgnoreCase) == false)
                {
                    Project project = Projects[project_SelectedIndex];
                    string fullPath = project.MainScript.RealPath;
                    Ini.WriteKey(fullPath, "Main", "TargetDir", value);
                    project.Variables.SetValue(VarsType.Fixed, "TargetDir", value);
                }

                project_TargetDirectory = value;

                OnPropertyUpdate(nameof(Project_TargetDirectory));
            }
        }

        private string project_ISOFile;
        public string Project_ISOFile
        {
            get => project_ISOFile;
            set
            {
                if (value.Equals(project_ISOFile, StringComparison.OrdinalIgnoreCase) == false)
                {
                    Project project = Projects[project_SelectedIndex];
                    string fullPath = project.MainScript.RealPath;
                    Ini.WriteKey(fullPath, "Main", "ISOFile", value);
                    project.Variables.SetValue(VarsType.Fixed, "ISOFile", value);
                }

                project_ISOFile = value;

                OnPropertyUpdate(nameof(Project_ISOFile));
            }
        }
        #endregion

        #region Property - General
        // Build
        private bool general_OptimizeCode;
        public bool General_OptimizeCode
        {
            get => general_OptimizeCode;
            set
            {
                general_OptimizeCode = value;
                OnPropertyUpdate(nameof(General_OptimizeCode));
            }
        }

        private bool general_ShowLogAfterBuild;
        public bool General_ShowLogAfterBuild
        {
            get => general_ShowLogAfterBuild;
            set
            {
                general_ShowLogAfterBuild = value;
                OnPropertyUpdate(nameof(General_ShowLogAfterBuild));
            }
        }

        private bool general_StopBuildOnError;
        public bool General_StopBuildOnError
        {
            get => general_StopBuildOnError;
            set
            {
                general_StopBuildOnError = value;
                OnPropertyUpdate(nameof(General_StopBuildOnError));
            }
        }

        // Path Length Limit
        private bool general_EnableLongFilePath;
        public bool General_EnableLongFilePath
        {
            get => general_EnableLongFilePath;
            set
            {
                general_EnableLongFilePath = value;

                // Enabled  = Path Length Limit = 32767
                // Disabled = Path Legnth Limit = 260
                AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling", !value);

                OnPropertyUpdate(nameof(General_EnableLongFilePath));
            }
        }

        // Custom User-Agent
        private bool general_UseCustomUserAgent;
        public bool General_UseCustomUserAgent
        {
            get => general_UseCustomUserAgent;
            set
            {
                general_UseCustomUserAgent = value;
                OnPropertyUpdate(nameof(General_UseCustomUserAgent));
            }
        }

        private string general_CustomUserAgent;
        public string General_CustomUserAgent
        {
            get => general_CustomUserAgent;
            set
            {
                general_CustomUserAgent = value;
                OnPropertyUpdate(nameof(General_CustomUserAgent));
            }
        } 
        #endregion

        #region Property - Interface
        private string interface_MonospaceFontStr;
        public string Interface_MonospaceFontStr
        {
            get => interface_MonospaceFontStr;
            set
            {
                interface_MonospaceFontStr = value;
                OnPropertyUpdate(nameof(Interface_MonospaceFontStr));
            }
        }

        private FontHelper.WPFFont interface_MonospaceFont;
        public FontHelper.WPFFont Interface_MonospaceFont
        {
            get => interface_MonospaceFont;
            set
            {
                interface_MonospaceFont = value;

                OnPropertyUpdate(nameof(Interface_MonospaceFont));
                Interface_MonospaceFontStr = $"{value.FontFamily.Source}, {value.FontSizeInPoint}pt";

                OnPropertyUpdate(nameof(Interface_MonospaceFontFamily));
                OnPropertyUpdate(nameof(Interface_MonospaceFontWeight));
                OnPropertyUpdate(nameof(Interface_MonospaceFontSize));
            }
        }

        public FontFamily Interface_MonospaceFontFamily => interface_MonospaceFont.FontFamily;
        public FontWeight Interface_MonospaceFontWeight => interface_MonospaceFont.FontWeight;
        public double Interface_MonospaceFontSize => interface_MonospaceFont.FontSizeInDIP;

        private double interface_ScaleFactor;
        public double Interface_ScaleFactor
        {
            get => interface_ScaleFactor;
            set
            {
                interface_ScaleFactor = value;
                OnPropertyUpdate(nameof(Interface_ScaleFactor));
            }
        }

        private bool interface_UseCustomEditor;
        public bool Interface_UseCustomEditor
        {
            get => interface_UseCustomEditor;
            set
            {
                interface_UseCustomEditor = value;
                OnPropertyUpdate(nameof(Interface_UseCustomEditor));
            }
        }

        private string interface_CustomEditorPath;
        public string Interface_CustomEditorPath
        {
            get => interface_CustomEditorPath;
            set
            {
                interface_CustomEditorPath = value;
                OnPropertyUpdate(nameof(Interface_CustomEditorPath));
            }
        }

        private bool interface_DisplayShellExecuteConOut;
        public bool Interface_DisplayShellExecuteConOut
        {
            get => interface_DisplayShellExecuteConOut;
            set
            {
                interface_DisplayShellExecuteConOut = value;
                OnPropertyUpdate(nameof(Interface_DisplayShellExecuteConOut));
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

        private bool _scriptDeepInspectAttachedFile;
        public bool Script_DeepInspectAttachedFile
        {
            get => _scriptDeepInspectAttachedFile;
            set
            {
                _scriptDeepInspectAttachedFile = value;
                OnPropertyUpdate(nameof(Script_DeepInspectAttachedFile));
            }
        }
        #endregion

        #region Property - Logging
        private ObservableCollection<string> log_DebugLevelList = new ObservableCollection<string>()
        {
            DebugLevel.Production.ToString(),
            DebugLevel.PrintException.ToString(),
            DebugLevel.PrintExceptionStackTrace.ToString()
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

        public DebugLevel Log_DebugLevel
        {
            get
            {
                switch (Log_DebugLevelIndex)
                {
                    case 0:
                        return DebugLevel.Production;
                    case 1:
                        return DebugLevel.PrintException;
                    default:
                        return DebugLevel.PrintExceptionStackTrace;
                }
            }
            set
            {
                switch (value)
                {
                    case DebugLevel.Production:
                        log_DebugLevelIndex = 0;
                        break;
                    case DebugLevel.PrintException:
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

        private bool log_Macro;
        public bool Log_Macro
        {
            get => log_Macro;
            set
            {
                log_Macro = value;
                OnPropertyUpdate(nameof(Log_Macro));
            }
        }

        private bool log_Comment;
        public bool Log_Comment
        {
            get => log_Comment;
            set
            {
                log_Comment = value;
                OnPropertyUpdate(nameof(Log_Comment));
            }
        }

        private bool log_DelayedLogging;
        public bool Log_DelayedLogging
        {
            get => log_DelayedLogging;
            set
            {
                log_DelayedLogging = value;
                OnPropertyUpdate(nameof(Log_DelayedLogging));
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

        private bool compat_RegWriteLegacy;
        public bool Compat_RegWriteLegacy
        {
            get => compat_RegWriteLegacy;
            set
            {
                compat_RegWriteLegacy = value;
                OnPropertyUpdate(nameof(Compat_RegWriteLegacy));
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
            set
            {
                compat_OverridableFixedVariables = value;
                OnPropertyUpdate(nameof(Compat_OverridableFixedVariables));
            }
        }

        private bool compat_EnableEnvironmentVariables;
        public bool Compat_EnableEnvironmentVariables
        {
            get => compat_EnableEnvironmentVariables;
            set
            {
                compat_EnableEnvironmentVariables = value;
                OnPropertyUpdate(nameof(Compat_EnableEnvironmentVariables));
            }
        }
        #endregion

        #region ApplySetting
        public void ApplySetting()
        {
            CodeParser.OptimizeCode = General_OptimizeCode;
            Engine.StopBuildOnError = General_StopBuildOnError;
            Logger.DebugLevel = Log_DebugLevel;
            MainViewModel.DisplayShellExecuteConOut = Interface_DisplayShellExecuteConOut;
            ProjectCollection.AsteriskBugDirLink = Compat_AsteriskBugDirLink;
            CodeParser.AllowLegacyBranchCondition = Compat_LegacyBranchCondition;
            CodeParser.AllowRegWriteLegacy = Compat_RegWriteLegacy;
            UIRenderer.IgnoreWidthOfWebLabel = Compat_IgnoreWidthOfWebLabel;
            Variables.OverridableFixedVariables = Compat_OverridableFixedVariables;
            Variables.EnableEnvironmentVariables = Compat_EnableEnvironmentVariables;
            ScriptEditViewModel.DeepInspectAttachedFile = Script_DeepInspectAttachedFile;
        }
        #endregion

        #region SetToDefault
        public void SetToDefault()
        {
            // Project
            Project_DefaultStr = string.Empty;

            // General
            General_OptimizeCode = true;
            General_ShowLogAfterBuild = true;
            General_StopBuildOnError = true;
            General_EnableLongFilePath = false;
            General_UseCustomUserAgent = false;
            // Custom User-Agent is set to Edge's on Windows 10 v1709
            General_CustomUserAgent = @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.36 Edge/16.16299";

            // Interface
            using (InstalledFontCollection fonts = new InstalledFontCollection())
            {
                // Every Windows have Consolas installed
                string fontFamily = "Consolas";

                // Prefer D2Coding over Consolas
                if (0 < fonts.Families.Count(x => x.Name.Equals("D2Coding", StringComparison.Ordinal)))
                    fontFamily = "D2Coding";

                Interface_MonospaceFont = new FontHelper.WPFFont(new FontFamily(fontFamily), FontWeights.Regular, 12);
            }
            Interface_ScaleFactor = 100;
            Interface_DisplayShellExecuteConOut = true;
            Interface_UseCustomEditor = false;
            Interface_CustomEditorPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "notepad.exe");

            // Script
            Script_EnableCache = true;
            Script_AutoSyntaxCheck = true;
            Script_DeepInspectAttachedFile = false;

            // Log
#if DEBUG
            Log_DebugLevelIndex = 2;
#else
            Log_DebugLevelIndex = 0;
#endif
            Log_Macro = true;
            Log_Comment = true;
            Log_DelayedLogging = true;

            // Compatibility
            Compat_AsteriskBugDirCopy = true;
            Compat_AsteriskBugDirLink = false;
            Compat_FileRenameCanMoveDir = true;
            Compat_LegacyBranchCondition = true;
            Compat_RegWriteLegacy = true;
            Compat_IgnoreWidthOfWebLabel = false;
            Compat_OverridableFixedVariables = false;
            Compat_EnableEnvironmentVariables = false;
        }
        #endregion

        #region ReadFromFile, WriteToFile
        public void ReadFromFile()
        {
            // If key not specified or value malformed, default value will be used.
            SetToDefault();

            if (File.Exists(_settingFile) == false)
                return;

            const string generalStr = "General";
            const string interfaceStr = "Interface";
            const string scriptStr = "Script";
            const string logStr = "Log";
            const string compatStr = "Compat";

            // General_CustomUserAgent
            IniKey[] keys = 
            {
                new IniKey("Project", "DefaultProject"), // String
                new IniKey(generalStr, KeyPart(nameof(General_OptimizeCode), generalStr)), // Boolean
                new IniKey(generalStr, KeyPart(nameof(General_ShowLogAfterBuild), generalStr)), // Boolean
                new IniKey(generalStr, KeyPart(nameof(General_StopBuildOnError), generalStr)), // Boolean
                new IniKey(generalStr, KeyPart(nameof(General_EnableLongFilePath), generalStr)), // Boolean
                new IniKey(generalStr, KeyPart(nameof(General_UseCustomUserAgent), generalStr)), // Boolean
                new IniKey(generalStr, KeyPart(nameof(General_CustomUserAgent), generalStr)), // String
                new IniKey(interfaceStr, KeyPart(nameof(Interface_MonospaceFontFamily), interfaceStr)),
                new IniKey(interfaceStr, KeyPart(nameof(Interface_MonospaceFontWeight), interfaceStr)),
                new IniKey(interfaceStr, KeyPart(nameof(Interface_MonospaceFontSize), interfaceStr)),
                new IniKey(interfaceStr, KeyPart(nameof(Interface_ScaleFactor), interfaceStr)), // Integer 100 ~ 200
                new IniKey(interfaceStr, KeyPart(nameof(Interface_UseCustomEditor), interfaceStr)), // Boolean
                new IniKey(interfaceStr, KeyPart(nameof(Interface_CustomEditorPath), interfaceStr)), // String
                new IniKey(interfaceStr, KeyPart(nameof(Interface_DisplayShellExecuteConOut), interfaceStr)), // Boolean
                new IniKey(scriptStr, KeyPart(nameof(Script_EnableCache), scriptStr)), // Boolean
                new IniKey(scriptStr, KeyPart(nameof(Script_AutoSyntaxCheck), scriptStr)), // Boolean
                new IniKey(scriptStr, KeyPart(nameof(Script_DeepInspectAttachedFile), scriptStr)), // Boolean
                new IniKey(logStr, KeyPart(nameof(Log_DebugLevel), logStr)), // Integer
                new IniKey(logStr, KeyPart(nameof(Log_Macro), logStr)), // Boolean
                new IniKey(logStr, KeyPart(nameof(Log_Comment), logStr)), // Boolean
                new IniKey(logStr, KeyPart(nameof(Log_DelayedLogging), logStr)), // Boolean
                new IniKey(compatStr, KeyPart(nameof(Compat_AsteriskBugDirCopy), compatStr)), // Boolean
                new IniKey(compatStr, KeyPart(nameof(Compat_AsteriskBugDirLink), compatStr)), // Boolean
                new IniKey(compatStr, KeyPart(nameof(Compat_FileRenameCanMoveDir), compatStr)), // Boolean
                new IniKey(compatStr, KeyPart(nameof(Compat_LegacyBranchCondition), compatStr)), // Boolean
                new IniKey(compatStr, KeyPart(nameof(Compat_RegWriteLegacy), compatStr)), // Boolean
                new IniKey(compatStr, KeyPart(nameof(Compat_IgnoreWidthOfWebLabel), compatStr)), // Boolean
                new IniKey(compatStr, KeyPart(nameof(Compat_OverridableFixedVariables), compatStr)), // Boolean
                new IniKey(compatStr, KeyPart(nameof(Compat_EnableEnvironmentVariables), compatStr)), // Boolean
            };
            
            keys = Ini.ReadKeys(_settingFile, keys);
            Dictionary<string, string> dict = keys.ToDictionary(x => $"{x.Section}_{x.Key}", x => x.Value);

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
                App.Logger.SystemWrite(new LogInfo(LogState.Error, $"Setting [{section}.{key}] has wrong value: {valStr}"));
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
                App.Logger.SystemWrite(new LogInfo(LogState.Error, $"Setting [{section}.{key}] has wrong value: {valStr}"));
                return defaultValue;
            }

            // Project
            if (dict["Project_DefaultProject"] != null)
                Project_DefaultStr = dict["Project_DefaultProject"];

            // General
            General_OptimizeCode = ParseBoolean(nameof(General_OptimizeCode), General_OptimizeCode);
            General_ShowLogAfterBuild = ParseBoolean(nameof(General_ShowLogAfterBuild), General_ShowLogAfterBuild);
            General_StopBuildOnError = ParseBoolean(nameof(General_StopBuildOnError), General_StopBuildOnError);
            General_EnableLongFilePath = ParseBoolean(nameof(General_EnableLongFilePath), General_EnableLongFilePath);
            General_UseCustomUserAgent = ParseBoolean(nameof(General_UseCustomUserAgent), General_UseCustomUserAgent);
            General_CustomUserAgent = ParseString(nameof(General_CustomUserAgent), General_CustomUserAgent);

            // Interface
            FontFamily monoFontFamiliy = Interface_MonospaceFont.FontFamily;
            FontWeight monoFontWeight = Interface_MonospaceFont.FontWeight;
            if (dict[nameof(Interface_MonospaceFontFamily)] != null)
                monoFontFamiliy = new FontFamily(dict[nameof(Interface_MonospaceFontFamily)]);
            if (dict[nameof(Interface_MonospaceFontWeight)] != null)
                monoFontWeight = FontHelper.FontWeightConvert_StringToWPF(dict[nameof(Interface_MonospaceFontWeight)]);
            int monoFontSize = ParseInteger(nameof(Interface_MonospaceFontSize), Interface_MonospaceFont.FontSizeInPoint, 1, -1);
            Interface_MonospaceFont = new FontHelper.WPFFont(monoFontFamiliy, monoFontWeight, monoFontSize);

            Interface_ScaleFactor = ParseInteger(nameof(Interface_ScaleFactor), (int)Interface_ScaleFactor, 100, 200);
            Interface_UseCustomEditor = ParseBoolean(nameof(Interface_UseCustomEditor), Interface_UseCustomEditor);
            Interface_CustomEditorPath = ParseString(nameof(Interface_CustomEditorPath), Interface_CustomEditorPath);
            Interface_DisplayShellExecuteConOut = ParseBoolean(nameof(Interface_DisplayShellExecuteConOut), Interface_DisplayShellExecuteConOut);

            // Script
            Script_EnableCache = ParseBoolean(nameof(Script_EnableCache), Script_EnableCache);
            Script_AutoSyntaxCheck = ParseBoolean(nameof(Script_AutoSyntaxCheck), Script_AutoSyntaxCheck);
            Script_DeepInspectAttachedFile = ParseBoolean(nameof(Script_DeepInspectAttachedFile), Script_DeepInspectAttachedFile);

            // Log
            Log_DebugLevelIndex = ParseInteger(nameof(Log_DebugLevel), Log_DebugLevelIndex, 0, 2);
            Log_Macro = ParseBoolean(nameof(Log_Macro), Log_Macro);
            Log_Comment = ParseBoolean(nameof(Log_Comment), Log_Comment);
            Log_DelayedLogging = ParseBoolean(nameof(Log_DelayedLogging), Log_DelayedLogging);

            // Compatibility
            Compat_AsteriskBugDirCopy = ParseBoolean(nameof(Compat_AsteriskBugDirCopy), Compat_AsteriskBugDirCopy);
            Compat_AsteriskBugDirLink = ParseBoolean(nameof(Compat_AsteriskBugDirLink), Compat_AsteriskBugDirLink);
            Compat_FileRenameCanMoveDir = ParseBoolean(nameof(Compat_FileRenameCanMoveDir), Compat_FileRenameCanMoveDir);
            Compat_LegacyBranchCondition = ParseBoolean(nameof(Compat_LegacyBranchCondition), Compat_LegacyBranchCondition);
            Compat_RegWriteLegacy = ParseBoolean(nameof(Compat_RegWriteLegacy), Compat_RegWriteLegacy);
            Compat_IgnoreWidthOfWebLabel = ParseBoolean(nameof(Compat_IgnoreWidthOfWebLabel), Compat_IgnoreWidthOfWebLabel);
            Compat_OverridableFixedVariables = ParseBoolean(nameof(Compat_OverridableFixedVariables), Compat_OverridableFixedVariables);
            Compat_EnableEnvironmentVariables = ParseBoolean(nameof(Compat_EnableEnvironmentVariables), Compat_EnableEnvironmentVariables);
        }

        public void WriteToFile()
        {
            const string generalStr = "General";
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
                new IniKey(interfaceStr, KeyPart(nameof(Interface_MonospaceFontFamily), interfaceStr), Interface_MonospaceFont.FontFamily.Source),
                new IniKey(interfaceStr, KeyPart(nameof(Interface_MonospaceFontWeight), interfaceStr), Interface_MonospaceFont.FontWeight.ToString()),
                new IniKey(interfaceStr, KeyPart(nameof(Interface_MonospaceFontSize), interfaceStr), Interface_MonospaceFont.FontSizeInPoint.ToString()),
                new IniKey(interfaceStr, KeyPart(nameof(Interface_ScaleFactor), interfaceStr), Interface_ScaleFactor.ToString(CultureInfo.InvariantCulture)), // Integer
                new IniKey(interfaceStr, KeyPart(nameof(Interface_UseCustomEditor), interfaceStr), Interface_UseCustomEditor.ToString()), // Boolean
                new IniKey(interfaceStr, KeyPart(nameof(Interface_CustomEditorPath), interfaceStr), Interface_CustomEditorPath), // String
                new IniKey(interfaceStr, KeyPart(nameof(Interface_DisplayShellExecuteConOut), interfaceStr), Interface_DisplayShellExecuteConOut.ToString()), // Boolean
                new IniKey(scriptStr, KeyPart(nameof(Script_EnableCache), scriptStr), Script_EnableCache.ToString()), // Boolean
                new IniKey(scriptStr, KeyPart(nameof(Script_AutoSyntaxCheck), scriptStr), Script_AutoSyntaxCheck.ToString()), // Boolean
                new IniKey(scriptStr, KeyPart(nameof(Script_DeepInspectAttachedFile), scriptStr), Script_DeepInspectAttachedFile.ToString()), // Boolean
                new IniKey(logStr, KeyPart(nameof(Log_DebugLevel), logStr), Log_DebugLevelIndex.ToString()), // Integer
                new IniKey(logStr, KeyPart(nameof(Log_Macro), logStr), Log_Macro.ToString()), // Boolean
                new IniKey(logStr, KeyPart(nameof(Log_Comment), logStr), Log_Comment.ToString()), // Boolean
                new IniKey(logStr, KeyPart(nameof(Log_DelayedLogging), logStr), Log_DelayedLogging.ToString()), // Boolean
                new IniKey("Project", "DefaultProject", Project_Default), // String
                new IniKey(compatStr, KeyPart(nameof(Compat_AsteriskBugDirCopy), compatStr), Compat_AsteriskBugDirCopy.ToString()), // Boolean
                new IniKey(compatStr, KeyPart(nameof(Compat_AsteriskBugDirLink), compatStr), Compat_AsteriskBugDirLink.ToString()), // Boolean
                new IniKey(compatStr, KeyPart(nameof(Compat_FileRenameCanMoveDir), compatStr), Compat_FileRenameCanMoveDir.ToString()), // Boolean
                new IniKey(compatStr, KeyPart(nameof(Compat_LegacyBranchCondition), compatStr), Compat_LegacyBranchCondition.ToString()), // Boolean
                new IniKey(compatStr, KeyPart(nameof(Compat_RegWriteLegacy), compatStr), Compat_RegWriteLegacy.ToString()), // Boolean
                new IniKey(compatStr, KeyPart(nameof(Compat_IgnoreWidthOfWebLabel), compatStr), Compat_IgnoreWidthOfWebLabel.ToString()), // Boolean
                new IniKey(compatStr, KeyPart(nameof(Compat_OverridableFixedVariables), compatStr), Compat_OverridableFixedVariables.ToString()), // Boolean
                new IniKey(compatStr, KeyPart(nameof(Compat_EnableEnvironmentVariables), compatStr), Compat_EnableEnvironmentVariables.ToString()), // Boolean
            };
            Ini.WriteKeys(_settingFile, keys);
        }

        private static string KeyPart(string str, string section)
        {
            return str.Substring(section.Length + 1);
        }
        #endregion

        #region Database Operation
        public void ClearLogDB()
        {
            LogDB.DeleteAll<DB_SystemLog>();
            LogDB.DeleteAll<DB_BuildInfo>();
            LogDB.DeleteAll<DB_Script>();
            LogDB.DeleteAll<DB_Variable>();
            LogDB.DeleteAll<DB_BuildLog>();

            UpdateLogDBState();
        }

        public void ClearCacheDB()
        {
            if (CacheDB != null)
            {
                CacheDB.DeleteAll<DB_ScriptCache>();
                UpdateCacheDBState();
            }
        }

        public void UpdateLogDBState()
        {
            int systemLogCount = LogDB.Table<DB_SystemLog>().Count();
            int codeLogCount = LogDB.Table<DB_BuildLog>().Count();
            Log_DBState = $"{systemLogCount} System Logs, {codeLogCount} Build Logs";
        }

        public void UpdateCacheDBState()
        {
            if (CacheDB == null)
            {
                Script_CacheState = "Cache not enabled";
            }
            else
            {
                int cacheCount = CacheDB.Table<DB_ScriptCache>().Count();
                Script_CacheState = $"{cacheCount} scripts cached";
            }
        }

        public void UpdateProjectList()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (Application.Current.MainWindow is MainWindow w)
                {
                    Projects = w.Projects;

                    bool foundDefault = false;
                    List<string> projNameList = Projects.ProjectNames;
                    Project_List = new ObservableCollection<string>();
                    for (int i = 0; i < projNameList.Count; i++)
                    {
                        Project_List.Add(projNameList[i]);
                        if (projNameList[i].Equals(Project_DefaultStr, StringComparison.OrdinalIgnoreCase))
                        {
                            foundDefault = true;
                            Project_SelectedIndex = Project_DefaultIndex = i;
                        }
                    }

                    if (foundDefault == false)
                        Project_SelectedIndex = Project_DefaultIndex = Projects.Count - 1;
                }
            });
        }
        #endregion

        #region OnPropertyUpdate
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyUpdate(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
    #endregion
}
