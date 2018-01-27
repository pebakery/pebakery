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

namespace PEBakery.WPF
{
    #region SettingWindow
    public partial class SettingWindow : Window
    {
        public SettingViewModel Model;

        public SettingWindow(SettingViewModel model)
        {
            this.Model = model;
            this.DataContext = Model;
            InitializeComponent();
        }

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
                string msg = "Enabling this option may cause problems!\r\nDo you really want to continue?";
                MessageBoxResult res = MessageBox.Show(msg, "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (res == MessageBoxResult.Yes)
                    Model.General_EnableLongFilePath = true;
                else
                    Model.General_EnableLongFilePath = false;
            }
        }

        private void Button_SourceDirectory_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (0 < Model.Project_SourceDirectoryList.Count)
                dialog.SelectedPath = Model.Project_SourceDirectoryList[Model.Project_SourceDirectoryIndex];

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
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

                Project project = Model.Projects[Model.Project_SelectedIndex];
                if (exist == false) // Add to list
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
            string fullPath = Model.Projects[idx].MainScript.FullPath;
            Ini.SetKey(fullPath, "Main", "SourceDir", string.Empty);
        }

        private void Button_TargetDirectory_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog()
            {
                SelectedPath = Model.Project_TargetDirectory,
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
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
    }
    #endregion

    #region SettingViewModel
    public class SettingViewModel : INotifyPropertyChanged
    {
        #region Field and Constructor
        private readonly string settingFile;

        private LogDB logDB;
        public LogDB LogDB { set => logDB = value; }

        private ScriptCache cacheDB;
        public ScriptCache CacheDB { set => cacheDB = value; }

        private ProjectCollection projects;
        public ProjectCollection Projects => projects;

        public SettingViewModel(string settingFile)
        {
            this.settingFile = settingFile;
            ReadFromFile();

            ApplySetting();
        }
        #endregion

        #region Project
        private string Project_DefaultStr;
        public string Project_Default
        {
            get => Project_List[project_DefaultIndex];
        }

        private ObservableCollection<string> project_List;
        public ObservableCollection<string> Project_List
        {
            get => project_List;
            set
            {
                project_List = value;
                OnPropertyUpdate("Project_List");
            }
        }

        private int project_DefaultIndex;
        public int Project_DefaultIndex
        {
            get => project_DefaultIndex;
            set
            {
                project_DefaultIndex = value;
                OnPropertyUpdate("Project_DefaultIndex");
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
                    string fullPath = projects[value].MainScript.FullPath;
                    IniKey[] keys = new IniKey[]
                    {
                        new IniKey("Main", "SourceDir"),
                        new IniKey("Main", "TargetDir"),
                        new IniKey("Main", "ISOFile"),
                        new IniKey("Main", "PathSetting"),
                    };
                    keys = Ini.GetKeys(fullPath, keys);

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
                            if (dir.Equals(string.Empty, StringComparison.Ordinal) == false)
                                Project_SourceDirectoryList.Add(dir);
                        }
                    }
                    
                    if (0 < Project_SourceDirectoryList.Count)
                    {
                        project_SourceDirectoryIndex = 0;
                        OnPropertyUpdate("Project_SourceDirectoryIndex");
                    }

                    if (keys[1].Value != null)
                    {
                        project_TargetDirectory = keys[1].Value;
                        OnPropertyUpdate("Project_TargetDirectory");
                    }
                    
                    if (keys[2].Value != null)
                    {
                        project_ISOFile = keys[2].Value;
                        OnPropertyUpdate("Project_ISOFile");
                    }
                }

                OnPropertyUpdate("Project_SelectedIndex");
            }
        }

        private bool project_PathEnabled = true;
        public bool Project_PathEnabled
        {
            get => project_PathEnabled;
            set
            {
                project_PathEnabled = value;
                OnPropertyUpdate("Project_PathEnabled");
            }
        }

        private ObservableCollection<string> project_SourceDirectoryList;
        public ObservableCollection<string> Project_SourceDirectoryList
        {
            get => project_SourceDirectoryList;
            set
            {
                project_SourceDirectoryList = value;
                OnPropertyUpdate("Project_SourceDirectoryList");
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
                    Ini.SetKey(project.MainScript.FullPath, "Main", "SourceDir", b.ToString());
                }
                
                OnPropertyUpdate("Project_SourceDirectoryIndex");
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
                    Project project = projects[project_SelectedIndex];
                    string fullPath = project.MainScript.FullPath;
                    Ini.SetKey(fullPath, "Main", "TargetDir", value);
                    project.Variables.SetValue(VarsType.Fixed, "TargetDir", value);
                }

                project_TargetDirectory = value;

                OnPropertyUpdate("Project_TargetDirectory");
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
                    Project project = projects[project_SelectedIndex];
                    string fullPath = project.MainScript.FullPath;
                    Ini.SetKey(fullPath, "Main", "ISOFile", value);
                    project.Variables.SetValue(VarsType.Fixed, "ISOFile", value);
                }

                project_ISOFile = value;

                OnPropertyUpdate("Project_ISOFile");
            }
        }
        #endregion

        #region General
        private bool general_EnableLongFilePath;
        public bool General_EnableLongFilePath
        {
            get => general_EnableLongFilePath;
            set
            {
                general_EnableLongFilePath = value;

                if (value)
                    AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling", false); // Path Length Limit = 32767
                else
                    AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling", true); // Path Length Limit = 260

                OnPropertyUpdate("General_EnableLongFilePath");
            }
        }

        private bool general_OptimizeCode;
        public bool General_OptimizeCode
        {
            get => general_OptimizeCode;
            set
            {
                general_OptimizeCode = value;
                OnPropertyUpdate("General_OptimizeCode");
            }
        }

        private bool general_ShowLogAfterBuild;
        public bool General_ShowLogAfterBuild
        {
            get => general_ShowLogAfterBuild;
            set
            {
                general_ShowLogAfterBuild = value;
                OnPropertyUpdate("General_ShowLogAfterBuild");
            }
        }

        private bool general_StopBuildOnError;
        public bool General_StopBuildOnError
        {
            get => general_StopBuildOnError;
            set
            {
                general_StopBuildOnError = value;
                OnPropertyUpdate("General_StopBuildOnError");
            }
        }
        #endregion

        #region Interface
        private string interface_MonospaceFontStr;
        public string Interface_MonospaceFontStr
        {
            get => interface_MonospaceFontStr;
            set
            {
                interface_MonospaceFontStr = value;
                OnPropertyUpdate("Interface_MonospaceFontStr");
            }
        }

        private FontHelper.WPFFont interface_MonospaceFont;
        public FontHelper.WPFFont Interface_MonospaceFont
        {
            get => interface_MonospaceFont;
            set
            {
                interface_MonospaceFont = value;

                OnPropertyUpdate("Interface_MonospaceFont");
                Interface_MonospaceFontStr = $"{value.FontFamily.Source}, {value.FontSizeInPoint}pt";

                OnPropertyUpdate("Interface_MonospaceFontFamily");
                OnPropertyUpdate("Interface_MonospaceFontWeight");
                OnPropertyUpdate("Interface_MonospaceFontSize");
            }
        }

        public FontFamily Interface_MonospaceFontFamily { get => interface_MonospaceFont.FontFamily; }
        public FontWeight Interface_MonospaceFontWeight { get => interface_MonospaceFont.FontWeight; }
        public double Interface_MonospaceFontSize { get => interface_MonospaceFont.FontSizeInDIP; }

        private double interface_ScaleFactor;
        public double Interface_ScaleFactor
        {
            get => interface_ScaleFactor;
            set
            {
                interface_ScaleFactor = value;
                OnPropertyUpdate("Interface_ScaleFactor");
            }
        }

        private bool interface_UseCustomEditor;
        public bool Interface_UseCustomEditor
        {
            get => interface_UseCustomEditor;
            set
            {
                interface_UseCustomEditor = value;
                OnPropertyUpdate("Interface_UseCustomEditor");
            }
        }

        private string interface_CustomEditorPath;
        public string Interface_CustomEditorPath
        {
            get => interface_CustomEditorPath;
            set
            {
                interface_CustomEditorPath = value;
                OnPropertyUpdate("Interface_CustomEditorPath");
            }
        }

        private bool interface_DisplayShellExecuteConOut;
        public bool Interface_DisplayShellExecuteConOut
        {
            get => interface_DisplayShellExecuteConOut;
            set
            {
                interface_DisplayShellExecuteConOut = value;
                OnPropertyUpdate("Interface_DisplayShellExecuteConOut");
            }
        }
        #endregion

        #region Script
        private string script_CacheState;
        public string Script_CacheState
        {
            get => script_CacheState;
            set
            {
                script_CacheState = value;
                OnPropertyUpdate("Script_CacheState");
            }
        }

        private bool script_EnableCache;
        public bool Script_EnableCache
        {
            get => script_EnableCache;
            set
            {
                script_EnableCache = value;
                OnPropertyUpdate("Script_EnableCache");
            }
        }

        private bool script_AutoConvertToUTF8;
        public bool Script_AutoConvertToUTF8
        {
            get => script_AutoConvertToUTF8;
            set
            {
                script_AutoConvertToUTF8 = value;
                OnPropertyUpdate("Script_AutoConvertToUTF8");
            }
        }

        private bool script_AutoSyntaxCheck;
        public bool Script_AutoSyntaxCheck
        {
            get => script_AutoSyntaxCheck;
            set
            {
                script_AutoSyntaxCheck = value;
                OnPropertyUpdate("Script_AutoSyntaxCheck");
            }
        }
        #endregion

        #region Logging
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
                OnPropertyUpdate("Log_DebugLevelList");
            }
        }

        private int log_DebugLevelIndex;
        public int Log_DebugLevelIndex
        {
            get => log_DebugLevelIndex;
            set
            {
                log_DebugLevelIndex = value;
                OnPropertyUpdate("Log_DebugLevelIndex");
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
                OnPropertyUpdate("Log_DBState");
            }
        }

        private bool log_Macro;
        public bool Log_Macro
        {
            get => log_Macro;
            set
            {
                log_Macro = value;
                OnPropertyUpdate("Log_Macro");
            }
        }

        private bool log_Comment;
        public bool Log_Comment
        {
            get => log_Comment;
            set
            {
                log_Comment = value;
                OnPropertyUpdate("Log_Comment");
            }
        }

        private bool log_DisableInInterface;
        public bool Log_DisableInInterface
        {
            get => log_DisableInInterface;
            set
            {
                log_DisableInInterface = value;
                OnPropertyUpdate("Log_DisableInInterface");
            }
        }

        private bool log_DisableDelayedLogging;
        public bool Log_DisableDelayedLogging
        {
            get => log_DisableDelayedLogging;
            set
            {
                log_DisableDelayedLogging = value;
                OnPropertyUpdate("Log_DisableDelayedLogging");
            }
        }
        #endregion

        #region Compatibility
        private bool compat_DirCopyBug;
        public bool Compat_DirCopyBug
        {
            get => compat_DirCopyBug;
            set
            {
                compat_DirCopyBug = value;
                OnPropertyUpdate("Compat_DirCopyBug");
            }
        }

        private bool compat_FileRenameCanMoveDir;
        public bool Compat_FileRenameCanMoveDir
        {
            get => compat_FileRenameCanMoveDir;
            set
            {
                compat_FileRenameCanMoveDir = value;
                OnPropertyUpdate("Compat_FileRenameCanMoveDir");
            }
        }

        private bool compat_LegacyBranchCondition;
        public bool Compat_LegacyBranchCondition
        {
            get => compat_LegacyBranchCondition;
            set
            {
                compat_LegacyBranchCondition = value;
                OnPropertyUpdate("Compat_LegacyBranchCondition");
            }
        }

        private bool compat_RegWriteLegacy;
        public bool Compat_RegWriteLegacy
        {
            get => compat_RegWriteLegacy;
            set
            {
                compat_RegWriteLegacy = value;
                OnPropertyUpdate("Compat_RegWriteLegacy");
            }
        }

        private bool compat_IgnoreWidthOfWebLabel;
        public bool Compat_IgnoreWidthOfWebLabel
        {
            get => compat_IgnoreWidthOfWebLabel;
            set
            {
                compat_IgnoreWidthOfWebLabel = value;
                OnPropertyUpdate("Compat_IgnoreWidthOfWebLabel");
            }
        }

        private bool compat_DisableBevelCaption;
        public bool Compat_DisableBevelCaption
        {
            get => compat_DisableBevelCaption;
            set
            {
                compat_DisableBevelCaption = value;
                OnPropertyUpdate("Compat_DisableBevelCaption");
            }
        }
        #endregion

        #region Utility
        public void ApplySetting()
        {
            CodeParser.OptimizeCode = this.General_OptimizeCode;
            Engine.StopBuildOnError = this.General_StopBuildOnError;
            Logger.DebugLevel = this.Log_DebugLevel;
            CodeParser.AllowLegacyBranchCondition = this.Compat_LegacyBranchCondition;
            CodeParser.AllowRegWriteLegacy = this.Compat_RegWriteLegacy;
            UIRenderer.IgnoreWidthOfWebLabel = this.Compat_IgnoreWidthOfWebLabel;
            UIRenderer.DisableBevelCaption = this.Compat_DisableBevelCaption;
            MainViewModel.DisplayShellExecuteConOut = this.Interface_DisplayShellExecuteConOut;
        }

        public void SetToDefault()
        {
            // Project
            Project_DefaultStr = string.Empty;

            // General
            General_EnableLongFilePath = false;
            General_OptimizeCode = true;
            General_ShowLogAfterBuild = true;
            General_StopBuildOnError = true;

            // Interface
            using (InstalledFontCollection fonts = new InstalledFontCollection())
            {
                if (fonts.Families.FirstOrDefault(x => x.Name.Equals("D2Coding", StringComparison.Ordinal)) == null)
                    Interface_MonospaceFont = new FontHelper.WPFFont(new FontFamily("Consolas"), FontWeights.Regular, 12);
                else // Prefer D2Coding over Consolas
                    Interface_MonospaceFont = new FontHelper.WPFFont(new FontFamily("D2Coding"), FontWeights.Regular, 12);
            }
            Interface_ScaleFactor = 100;
            Interface_DisplayShellExecuteConOut = true;
            Interface_UseCustomEditor = false;
            Interface_CustomEditorPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "notepad.exe");

            // Script
            Script_EnableCache = true;
            Script_AutoConvertToUTF8 = false;
            Script_AutoSyntaxCheck = true;

            // Log
#if DEBUG
            Log_DebugLevelIndex = 2;
#else
            Log_DebugLevelIndex = 0;
#endif
            Log_Macro = true;
            Log_Comment = true;
            Log_DisableInInterface = true;
            Log_DisableDelayedLogging = false;

            // Compatibility
            Compat_DirCopyBug = true;
            Compat_FileRenameCanMoveDir = true;
            Compat_LegacyBranchCondition = true;
            Compat_RegWriteLegacy = true;
            Compat_IgnoreWidthOfWebLabel = true;
            Compat_DisableBevelCaption = true;
        }

        public void ReadFromFile()
        {
            // If key not specified or value malformed, default value will be used.
            SetToDefault();

            if (File.Exists(settingFile) == false)
                return;

            IniKey[] keys = new IniKey[]
            {
                new IniKey("General", "EnableLongFilePath"), // Boolean
                new IniKey("General", "OptimizeCode"), // Boolean
                new IniKey("General", "ShowLogAfterBuild"), // Boolean
                new IniKey("General", "StopBuildOnError"), // Boolean
                new IniKey("Interface", "MonospaceFontFamily"),
                new IniKey("Interface", "MonospaceFontWeight"),
                new IniKey("Interface", "MonospaceFontSize"),
                new IniKey("Interface", "ScaleFactor"), // Integer 100 ~ 200
                new IniKey("Interface", "UseCustomEditor"), // Boolean
                new IniKey("Interface", "CustomEditorPath"), // String
                new IniKey("Interface", "DisplayShellExecuteConOut"), // Boolean
                new IniKey("Script", "EnableCache"), // Boolean
                new IniKey("Script", "SpeedupLoading"), // Boolean
                new IniKey("Script", "AutoConvertToUTF8"), // Boolean
                new IniKey("Script", "AutoSyntaxCheck"), // Boolean
                new IniKey("Log", "DebugLevel"), // Integer
                new IniKey("Log", "Macro"), // Boolean
                new IniKey("Log", "Comment"), // Boolean
                new IniKey("Log", "DisableInInterface"), // Boolean
                new IniKey("Log", "DisableDelayedLogging"), // Boolean
                new IniKey("Project", "DefaultProject"), // String
                new IniKey("Compat", "DirCopyBug"), // Boolean
                new IniKey("Compat", "FileRenameCanMoveDir"), // Boolean
                new IniKey("Compat", "LegacyBranchCondition"), // Boolean
                new IniKey("Compat", "RegWriteLegacy"), // Boolean
                new IniKey("Compat", "IgnoreWidthOfWebLabel"), // Boolean
                new IniKey("Compat", "DisableBevelCaption"), // Boolean
            }; 
            keys = Ini.GetKeys(settingFile, keys);

            Dictionary<string, string> dict = keys.ToDictionary(x => $"{x.Section}_{x.Key}", x => x.Value);
            string str_General_EnableLongFilePath = dict["General_EnableLongFilePath"];
            string str_General_OptimizeCode = dict["General_OptimizeCode"];
            string str_General_ShowLogAfterBuild = dict["General_ShowLogAfterBuild"];
            string str_General_StopBuildOnError = dict["General_StopBuildOnError"];
            string str_Interface_MonospaceFontFamiliy = dict["Interface_MonospaceFontFamily"];
            string str_Interface_MonospaceFontWeight = dict["Interface_MonospaceFontWeight"];
            string str_Interface_MonospaceFontSize = dict["Interface_MonospaceFontSize"];
            string str_Interface_UseCustomEditor = dict["Interface_UseCustomEditor"];
            string str_Interface_CustomEditorPath = dict["Interface_CustomEditorPath"];
            string str_Interface_DisplayShellExecuteConOut = dict["Interface_DisplayShellExecuteConOut"];
            string str_Interface_ScaleFactor = dict["Interface_ScaleFactor"];
            string str_Script_EnableCache = dict["Script_EnableCache"];
            string str_Script_SpeedupLoading = dict["Script_SpeedupLoading"];
            string str_Script_AutoConvertToUTF8 = dict["Script_AutoConvertToUTF8"];
            string str_Script_AutoSyntaxCheck = dict["Script_AutoSyntaxCheck"];
            string str_Log_DebugLevelIndex = dict["Log_DebugLevel"];
            string str_Log_Macro = dict["Log_Macro"];
            string str_Log_Comment = dict["Log_Comment"];
            string str_Log_DisableInInterface = dict["Log_DisableInInterface"];
            string str_Log_DisableDelayedLogging = dict["Log_DisableDelayedLogging"];
            string str_Compat_DirCopyBug = dict["Compat_DirCopyBug"];
            string str_Compat_FileRenameCanMoveDir = dict["Compat_FileRenameCanMoveDir"];
            string str_Compat_LegacyBranchCondition = dict["Compat_LegacyBranchCondition"];
            string str_Compat_RegWriteLegacy = dict["Compat_RegWriteLegacy"];
            string str_Compat_IgnoreWidthOfWebLabel = dict["Compat_IgnoreWidthOfWebLabel"];
            string str_Compat_DisableBevelCaption = dict["Compat_DisableBevelCaption"];

            // Project
            if (dict["Project_DefaultProject"] != null)
                Project_DefaultStr = dict["Project_DefaultProject"];

            // General - EnableLongFilePath (Default = False)
            if (str_General_EnableLongFilePath != null)
            {
                if (str_General_EnableLongFilePath.Equals("True", StringComparison.OrdinalIgnoreCase))
                    General_EnableLongFilePath = true;
            }

            // General - EnableLongFilePath (Default = True)
            if (str_General_OptimizeCode != null)
            {
                if (str_General_OptimizeCode.Equals("False", StringComparison.OrdinalIgnoreCase))
                    General_OptimizeCode = false;
            }

            // General - ShowLogAfterBuild (Default = True)
            if (str_General_ShowLogAfterBuild != null)
            {
                if (str_General_ShowLogAfterBuild.Equals("False", StringComparison.OrdinalIgnoreCase))
                    General_ShowLogAfterBuild = false;
            }

            // General - StopBuildOnError (Default = True)
            if (str_General_StopBuildOnError != null)
            {
                if (str_General_StopBuildOnError.Equals("False", StringComparison.OrdinalIgnoreCase))
                    General_StopBuildOnError = false;
            }

            // Interface - MonospaceFont (Default = Consolas, Regular, 12pt
            FontFamily monoFontFamiliy = Interface_MonospaceFont.FontFamily;
            FontWeight monoFontWeight = Interface_MonospaceFont.FontWeight;
            int monoFontSize = Interface_MonospaceFont.FontSizeInPoint;
            if (str_Interface_MonospaceFontFamiliy != null)
                monoFontFamiliy = new FontFamily(str_Interface_MonospaceFontFamiliy);
            if (str_Interface_MonospaceFontWeight != null)
                monoFontWeight = FontHelper.FontWeightConvert_StringToWPF(str_Interface_MonospaceFontWeight);
            if (str_Interface_MonospaceFontSize != null)
            {
                if (int.TryParse(str_Interface_MonospaceFontSize, NumberStyles.Integer, CultureInfo.InvariantCulture, out int newMonoFontSize))
                {
                    if (0 < newMonoFontSize)
                        monoFontSize = newMonoFontSize;
                }
            }
            Interface_MonospaceFont = new FontHelper.WPFFont(monoFontFamiliy, monoFontWeight, monoFontSize);

            // Interface - ScaleFactor (Default = 100)
            if (str_Interface_ScaleFactor != null)
            {
                if (int.TryParse(str_Interface_ScaleFactor, NumberStyles.Integer, CultureInfo.InvariantCulture, out int scaleFactor))
                {
                    if (100 <= scaleFactor && scaleFactor <= 200)
                        Interface_ScaleFactor = scaleFactor;
                }
            }

            // Interface_UseCustomEditor (Default = False)
            if (str_Interface_UseCustomEditor != null)
            {
                if (str_Interface_UseCustomEditor.Equals("True", StringComparison.OrdinalIgnoreCase))
                    Interface_UseCustomEditor = true;
            }

            // Interface_CustomEditorPath
            if (dict["Interface_CustomEditorPath"] != null)
                Interface_CustomEditorPath = dict["Interface_CustomEditorPath"];

            // Interface - DisplayShellExecuteStdOut (Default = True)
            if (str_Interface_DisplayShellExecuteConOut != null)
            {
                if (str_Interface_DisplayShellExecuteConOut.Equals("False", StringComparison.OrdinalIgnoreCase))
                    Interface_DisplayShellExecuteConOut = false;
            }

            // Script - EnableCache (Default = True)
            if (str_Script_EnableCache != null)
            {
                if (str_Script_EnableCache.Equals("False", StringComparison.OrdinalIgnoreCase))
                    Script_EnableCache = false;
            }

            // Script - AutoConvertToUTF8 (Default = False)
            if (str_Script_AutoConvertToUTF8 != null)
            {
                if (str_Script_AutoConvertToUTF8.Equals("True", StringComparison.OrdinalIgnoreCase))
                    Script_AutoConvertToUTF8 = true;
            }

            // Script - AutoSyntaxCheck (Default = False)
            if (str_Script_AutoSyntaxCheck != null)
            {
                if (str_Script_AutoSyntaxCheck.Equals("True", StringComparison.OrdinalIgnoreCase))
                    Script_AutoSyntaxCheck = true;
            }

            // Log - DebugLevel (Default = 0)
            if (str_Log_DebugLevelIndex != null)
            {
                if (int.TryParse(str_Log_DebugLevelIndex, NumberStyles.Integer, CultureInfo.InvariantCulture, out int debugLevelIdx))
                {
                    if (0 <= debugLevelIdx && debugLevelIdx <= 2)
                        Log_DebugLevelIndex = debugLevelIdx;
                }
            }

            // Log - Macro (Default = True)
            if (str_Log_Macro != null)
            {
                if (str_Log_Macro.Equals("False", StringComparison.OrdinalIgnoreCase))
                    Log_Macro = false;
            }

            // Log - Comment (Default = True)
            if (str_Log_Comment != null)
            {
                if (str_Log_Comment.Equals("False", StringComparison.OrdinalIgnoreCase))
                    Log_Comment = false;
            }

            // Log - DisableInInterface (Default = True)
            if (str_Log_DisableInInterface != null)
            {
                if (str_Log_DisableInInterface.Equals("False", StringComparison.OrdinalIgnoreCase))
                    Log_DisableInInterface = false;
            }

            // Log - DisableDelayedLogging (Default = False)
            if (str_Log_DisableDelayedLogging != null)
            {
                if (str_Log_DisableDelayedLogging.Equals("True", StringComparison.OrdinalIgnoreCase))
                    Log_DisableDelayedLogging = true;
            }

            // Compatibility - DirCopyBug (Default = True)
            if (str_Compat_DirCopyBug != null)
            {
                if (str_Compat_DirCopyBug.Equals("False", StringComparison.OrdinalIgnoreCase))
                    Compat_DirCopyBug = false;
            }

            // Compatibility - FileRenameCanMoveDir (Default = True)
            if (str_Compat_FileRenameCanMoveDir != null)
            {
                if (str_Compat_FileRenameCanMoveDir.Equals("False", StringComparison.OrdinalIgnoreCase))
                    Compat_FileRenameCanMoveDir = false;
            }

            // Compatibility - LegacyBranchCondition (Default = True)
            if (str_Compat_LegacyBranchCondition != null)
            {
                if (str_Compat_LegacyBranchCondition.Equals("False", StringComparison.OrdinalIgnoreCase))
                    Compat_LegacyBranchCondition = false;
            }

            // Compatibility - RegWriteLegacy (Default = True)
            if (str_Compat_RegWriteLegacy != null)
            {
                if (str_Compat_RegWriteLegacy.Equals("False", StringComparison.OrdinalIgnoreCase))
                    Compat_RegWriteLegacy = false;
            }

            // Compatibility - IgnoreWidthOfWebLabel (Default = True)
            if (str_Compat_IgnoreWidthOfWebLabel != null)
            {
                if (str_Compat_IgnoreWidthOfWebLabel.Equals("False", StringComparison.OrdinalIgnoreCase))
                    Compat_IgnoreWidthOfWebLabel = false;
            }

            // Compatibility - DisableBevelCaption (Default = True)
            if (str_Compat_DisableBevelCaption != null)
            {
                if (str_Compat_DisableBevelCaption.Equals("False", StringComparison.OrdinalIgnoreCase))
                    Compat_DisableBevelCaption = false;
            }
        }

        public void WriteToFile()
        {
            IniKey[] keys = new IniKey[]
            {
                new IniKey("General", "OptimizeCode", General_OptimizeCode.ToString()),
                new IniKey("General", "EnableLongFilePath", General_EnableLongFilePath.ToString()),
                new IniKey("General", "ShowLogAfterBuild", General_ShowLogAfterBuild.ToString()),
                new IniKey("General", "StopBuildOnError", General_StopBuildOnError.ToString()),
                new IniKey("Interface", "MonospaceFontFamily", Interface_MonospaceFont.FontFamily.Source),
                new IniKey("Interface", "MonospaceFontWeight", Interface_MonospaceFont.FontWeight.ToString()),
                new IniKey("Interface", "MonospaceFontSize", Interface_MonospaceFont.FontSizeInPoint.ToString()),
                new IniKey("Interface", "ScaleFactor", Interface_ScaleFactor.ToString()),
                new IniKey("Interface", "UseCustomEditor", Interface_UseCustomEditor.ToString()),
                new IniKey("Interface", "CustomEditorPath", Interface_CustomEditorPath),
                new IniKey("Interface", "DisplayShellExecuteConOut", Interface_DisplayShellExecuteConOut.ToString()),
                new IniKey("Script", "EnableCache", Script_EnableCache.ToString()),
                new IniKey("Script", "AutoConvertToUTF8", Script_AutoConvertToUTF8.ToString()),
                new IniKey("Script", "AutoSyntaxCheck", Script_AutoSyntaxCheck.ToString()),
                new IniKey("Log", "DebugLevel", log_DebugLevelIndex.ToString()),
                new IniKey("Log", "Macro", Log_Macro.ToString()),
                new IniKey("Log", "Comment", Log_Comment.ToString()),
                new IniKey("Log", "DisableInInterface", Log_DisableInInterface.ToString()),
                new IniKey("Log", "DisableDelayedLogging", Log_DisableDelayedLogging.ToString()),
                new IniKey("Project", "DefaultProject", Project_Default),
                new IniKey("Compat", "DirCopyBug", Compat_DirCopyBug.ToString()),
                new IniKey("Compat", "FileRenameCanMoveDir", Compat_FileRenameCanMoveDir.ToString()),
                new IniKey("Compat", "LegacyBranchCondition", Compat_LegacyBranchCondition.ToString()),
                new IniKey("Compat", "RegWriteLegacy", Compat_RegWriteLegacy.ToString()),
                new IniKey("Compat", "IgnoreWidthOfWebLabel", Compat_IgnoreWidthOfWebLabel.ToString()),
                new IniKey("Compat", "DisableBevelCaption", Compat_DisableBevelCaption.ToString()),
            };
            Ini.SetKeys(settingFile, keys);
        }

        public void ClearLogDB()
        {
            logDB.DeleteAll<DB_SystemLog>();
            logDB.DeleteAll<DB_BuildInfo>();
            logDB.DeleteAll<DB_Script>();
            logDB.DeleteAll<DB_Variable>();
            logDB.DeleteAll<DB_BuildLog>();

            UpdateLogDBState();
        }

        public void ClearCacheDB()
        {
            if (cacheDB != null)
            {
                cacheDB.DeleteAll<DB_ScriptCache>();
                UpdateCacheDBState();
            }
        }

        public void UpdateLogDBState()
        {
            int systemLogCount = logDB.Table<DB_SystemLog>().Count();
            int codeLogCount = logDB.Table<DB_BuildLog>().Count();
            Log_DBState = $"{systemLogCount} System Logs, {codeLogCount} Build Logs";
        }

        public void UpdateCacheDBState()
        {
            if (cacheDB == null)
            {
                Script_CacheState = "Cache not enabled";
            }
            else
            {
                int cacheCount = cacheDB.Table<DB_ScriptCache>().Count();
                Script_CacheState = $"{cacheCount} scripts cached";
            }
        }

        public void UpdateProjectList()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MainWindow w = Application.Current.MainWindow as MainWindow;
                projects = w.Projects;
            });

            bool foundDefault = false;
            List<string> projNameList = projects.ProjectNames;
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
                Project_SelectedIndex = Project_DefaultIndex = projects.Count - 1;
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
