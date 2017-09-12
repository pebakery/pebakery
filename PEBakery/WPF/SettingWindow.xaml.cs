using PEBakery.Lib;
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

namespace PEBakery.WPF
{
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
            if (PluginCache.dbLock == 0)
            {
                Interlocked.Increment(ref PluginCache.dbLock);
                try
                {
                    Model.ClearCacheDB();
                }
                finally
                {
                    Interlocked.Decrement(ref PluginCache.dbLock);
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
            System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog()
            {
                ShowNewFolderButton = true,
            };

            if (0 < Model.Project_SourceDirectoryList.Count)
                dialog.SelectedPath = Model.Project_SourceDirectoryList[Model.Project_SourceDirectoryIndex];

            System.Windows.Forms.DialogResult result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
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
                    ObservableCollection<string> newSourceDirList = new ObservableCollection<string>();
                    newSourceDirList.Add(dialog.SelectedPath);
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
            string fullPath = Model.Projects[idx].MainPlugin.FullPath;
            Ini.SetKey(fullPath, "Main", "SourceDir", string.Empty);
        }

        private void Button_TargetDirectory_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog()
            {
                ShowNewFolderButton = true,
                SelectedPath = Model.Project_TargetDirectory,
            };
            System.Windows.Forms.DialogResult result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                Model.Project_TargetDirectory = dialog.SelectedPath;
            }
        }

        private void Button_ISOFile_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog dialog = new Microsoft.Win32.SaveFileDialog()
            {
                Filter = "ISO File (*.iso)|*.iso",
                InitialDirectory = System.IO.Path.GetDirectoryName(Model.Project_ISOFile),
            };
            if (dialog.ShowDialog() == true)
            {
                Model.Project_ISOFile = dialog.FileName;
            }
        }

        private void Button_MonospaceFont_Click(object sender, RoutedEventArgs e)
        {
            Model.General_MonospaceFont = FontHelper.ChooseFontDialog(Model.General_MonospaceFont, this, false, true);
        }
    }

    #region SettingViewModel
    public class SettingViewModel : INotifyPropertyChanged
    {
        private readonly string settingFile;

        private LogDB logDB;
        public LogDB LogDB { set => logDB = value; }

        private PluginCache cacheDB;
        public PluginCache CacheDB { set => cacheDB = value; }

        private ProjectCollection projects;
        public ProjectCollection Projects { get => projects; }

        public SettingViewModel(string settingFile)
        {
            this.settingFile = settingFile;
            ReadFromFile();
        }

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
                    string fullPath = projects[value].MainPlugin.FullPath;
                    IniKey[] keys = new IniKey[]
                    {
                        new IniKey("Main", "SourceDir"),
                        new IniKey("Main", "TargetDir"),
                        new IniKey("Main", "ISOFile"),
                    };
                    keys = Ini.GetKeys(fullPath, keys);

                    Project_SourceDirectoryList = new ObservableCollection<string>();
                    string[] rawDirList = keys[0].Value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string rawDir in rawDirList)
                    {
                        string dir = rawDir.Trim();
                        if (dir.Equals(string.Empty, StringComparison.Ordinal) == false)
                            Project_SourceDirectoryList.Add(dir);
                    }

                    if (0 < Project_SourceDirectoryList.Count)
                    {
                        project_SourceDirectoryIndex = 0;
                        OnPropertyUpdate("Project_SourceDirectoryIndex");
                    }

                    project_TargetDirectory = keys[1].Value;
                    project_ISOFile = keys[2].Value;
                    OnPropertyUpdate("Project_TargetDirectory");
                    OnPropertyUpdate("Project_ISOFile");
                }

                OnPropertyUpdate("Project_SelectedIndex");
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
                    project.Variables.SetFixedValue("SourceDir", Project_SourceDirectoryList[value]);

                    StringBuilder b = new StringBuilder(Project_SourceDirectoryList[value]);
                    for (int x = 0; x < Project_SourceDirectoryList.Count; x++)
                    {
                        if (x == value)
                            continue;

                        b.Append(",");
                        b.Append(Project_SourceDirectoryList[x]);
                    }
                    Ini.SetKey(project.MainPlugin.FullPath, "Main", "SourceDir", b.ToString());
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
                    string fullPath = project.MainPlugin.FullPath;
                    Ini.SetKey(fullPath, "Main", "TargetDir", value);
                    project.Variables.SetFixedValue("TargetDir", value);
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
                    string fullPath = project.MainPlugin.FullPath;
                    Ini.SetKey(fullPath, "Main", "ISOFile", value);
                    project.Variables.SetFixedValue("ISOFile", value);
                }

                project_ISOFile = value;

                OnPropertyUpdate("Project_ISOFile");
            }
        }
        #endregion

        #region General
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

        private string general_MonospaceFontStr;
        public string General_MonospaceFontStr
        {
            get => general_MonospaceFontStr;
            set
            {
                general_MonospaceFontStr = value;
                OnPropertyUpdate("General_MonospaceFontStr");
            }
        }

        private FontHelper.WPFFont general_MonospaceFont;
        public FontHelper.WPFFont General_MonospaceFont
        {
            get => general_MonospaceFont;
            set
            {
                general_MonospaceFont = value;

                OnPropertyUpdate("General_MonospaceFont");
                General_MonospaceFontStr = $"{value.FontFamily.Source}, {value.FontSizeInPoint}pt";

                OnPropertyUpdate("General_MonospaceFontFamily");
                OnPropertyUpdate("General_MonospaceFontWeight");
                OnPropertyUpdate("General_MonospaceFontSize");
            }
        }

        public FontFamily General_MonospaceFontFamily { get => general_MonospaceFont.FontFamily; }
        public FontWeight General_MonospaceFontWeight { get => general_MonospaceFont.FontWeight; }
        public double General_MonospaceFontSize { get => general_MonospaceFont.FontSizeInDIP; }
        #endregion

        #region Interface
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
        #endregion

        #region Plugin
        private string plugin_CacheState;
        public string Plugin_CacheState
        {
            get => plugin_CacheState;
            set
            {
                plugin_CacheState = value;
                OnPropertyUpdate("Plugin_CacheState");
            }
        }

        private bool plugin_EnableCache;
        public bool Plugin_EnableCache
        {
            get => plugin_EnableCache;
            set
            {
                plugin_EnableCache = value;
                OnPropertyUpdate("Plugin_EnableCache");
            }
        }

        private bool plugin_AutoConvertToUTF8;
        public bool Plugin_AutoConvertToUTF8
        {
            get => plugin_AutoConvertToUTF8;
            set
            {
                plugin_AutoConvertToUTF8 = value;
                OnPropertyUpdate("Plugin_AutoConvertToUTF8");
            }
        }
        #endregion

        #region Log
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
        #endregion

        #region Utility
        public void SetToDefault()
        {
            // Project
            Project_DefaultStr = string.Empty;

            // General
            General_EnableLongFilePath = false;
            General_OptimizeCode = true;
            General_MonospaceFont = new FontHelper.WPFFont(new FontFamily("Consolas"), FontWeights.Regular, 12);

            // Interface
            Interface_ScaleFactor = 100;

            // Plugin
            Plugin_EnableCache = true;
            Plugin_AutoConvertToUTF8 = false;

            // Log
#if DEBUG
            Log_DebugLevelIndex = 2; 
#else
            Log_DebugLevelIndex = 0;
#endif
            Log_Macro = true;
            Log_Comment = true;
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
                new IniKey("General", "MonospaceFontFamily"),
                new IniKey("General", "MonospaceFontWeight"),
                new IniKey("General", "MonospaceFontSize"),
                new IniKey("Interface", "ScaleFactor"), // Integer 100 ~ 200
                new IniKey("Plugin", "EnableCache"), // Boolean
                new IniKey("Plugin", "AutoConvertToUTF8"), // Boolean
                new IniKey("Log", "DebugLevel"), // Integer
                new IniKey("Log", "Macro"), // Boolean
                new IniKey("Log", "Comment"), // Boolean
                new IniKey("Project", "DefaultProject"), // String
            };
            keys = Ini.GetKeys(settingFile, keys);

            Dictionary<string, string> dict = keys.ToDictionary(x => x.Key, x => x.Value);
            string str_General_EnableLongFilePath = dict["EnableLongFilePath"];
            string str_General_OptimizeCode = dict["OptimizeCode"];
            string str_Gereal_MonospaceFontFamiliy = dict["MonospaceFontFamily"];
            string str_Gereal_MonospaceFontWeight = dict["MonospaceFontWeight"];
            string str_Gereal_MonospaceFontSize = dict["MonospaceFontSize"];
            string str_Interface_ScaleFactor = dict["ScaleFactor"];
            string str_Plugin_EnableCache = dict["EnableCache"];
            string str_Plugin_AutoConvertToUTF8 = dict["AutoConvertToUTF8"];
            string str_Log_DebugLevelIndex = dict["DebugLevel"];
            string str_Log_Macro = dict["Macro"];
            string str_Log_Comment = dict["Comment"];

            // Project
            if (dict["DefaultProject"] != null)
                Project_DefaultStr = dict["DefaultProject"];

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

            // General - MonospaceFont (Default = Consolas, Regular, 12pt
            FontFamily monoFontFamiliy = General_MonospaceFont.FontFamily;
            FontWeight monoFontWeight = General_MonospaceFont.FontWeight;
            int monoFontSize = General_MonospaceFont.FontSizeInPoint;
            if (str_Gereal_MonospaceFontFamiliy != null)
                monoFontFamiliy = new FontFamily(str_Gereal_MonospaceFontFamiliy);
            if (str_Gereal_MonospaceFontWeight != null)
                monoFontWeight = FontHelper.FontWeightConvert_StringToWPF(str_Gereal_MonospaceFontWeight);
            if (str_Gereal_MonospaceFontSize != null)
            {
                if (int.TryParse(str_Gereal_MonospaceFontSize, NumberStyles.Integer, CultureInfo.InvariantCulture, out int newMonoFontSize))
                {
                    if (0 < newMonoFontSize)
                        monoFontSize = newMonoFontSize;
                }
            }
            General_MonospaceFont = new FontHelper.WPFFont(monoFontFamiliy, monoFontWeight, monoFontSize);

            // Interface - ScaleFactor (Default = 100)
            if (str_Interface_ScaleFactor != null)
            {
                if (int.TryParse(str_Interface_ScaleFactor, NumberStyles.Integer, CultureInfo.InvariantCulture, out int scaleFactor))
                {
                    if (100 <= scaleFactor && scaleFactor <= 200)
                        Interface_ScaleFactor = scaleFactor;
                }
            }

            // Plugin - EnableCache (Default = True)
            if (str_Plugin_EnableCache != null)
            {
                if (str_Plugin_EnableCache.Equals("False", StringComparison.OrdinalIgnoreCase))
                    Plugin_EnableCache = false;
            }

            // Plugin - AutoConvertToUTF8 (Default = False)
            if (str_Plugin_AutoConvertToUTF8 != null)
            {
                if (str_Plugin_AutoConvertToUTF8.Equals("True", StringComparison.OrdinalIgnoreCase))
                    Plugin_AutoConvertToUTF8 = true;
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
        }

        public void WriteToFile()
        {
            IniKey[] keys = new IniKey[]
            {
                new IniKey("General", "OptimizeCode", General_OptimizeCode.ToString()),
                new IniKey("General", "EnableLongFilePath", General_EnableLongFilePath.ToString()),
                new IniKey("General", "MonospaceFontFamily", General_MonospaceFont.FontFamily.Source),
                new IniKey("General", "MonospaceFontWeight", General_MonospaceFont.FontWeight.ToString()),
                new IniKey("General", "MonospaceFontSize", General_MonospaceFont.FontSizeInPoint.ToString()),
                new IniKey("Interface", "ScaleFactor", Interface_ScaleFactor.ToString()),
                new IniKey("Plugin", "EnableCache", Plugin_EnableCache.ToString()),
                new IniKey("Plugin", "AutoConvertToUTF8", Plugin_AutoConvertToUTF8.ToString()),
                new IniKey("Log", "DebugLevel", log_DebugLevelIndex.ToString()),
                new IniKey("Log", "Macro", Log_Macro.ToString()),
                new IniKey("Log", "Comment", Log_Comment.ToString()),
                new IniKey("Project", "DefaultProject", Project_Default),
            };
            Ini.SetKeys(settingFile, keys);

        }

        public void ClearLogDB()
        {
            logDB.DeleteAll<DB_SystemLog>();
            logDB.DeleteAll<DB_BuildInfo>();
            logDB.DeleteAll<DB_Plugin>();
            logDB.DeleteAll<DB_Variable>();
            logDB.DeleteAll<DB_BuildLog>();

            UpdateLogDBState();
        }

        public void ClearCacheDB()
        {
            if (cacheDB != null)
            {
                cacheDB.DeleteAll<DB_PluginCache>();
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
                Plugin_CacheState = "Cache not enabled";
            }
            else
            {
                int cacheCount = cacheDB.Table<DB_PluginCache>().Count();
                Plugin_CacheState = $"{cacheCount} plugins cached";
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
