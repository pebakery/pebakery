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

        public SettingViewModel(string settingFile)
        {
            this.settingFile = settingFile;
            ReadFromFile();
        }

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
                OnPropertyUpdate("General_EnableLongFilePath");
            }
        }
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
            DebugLevel.PrintExceptionType.ToString(),
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
                        return DebugLevel.PrintExceptionType;
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
                    case DebugLevel.PrintExceptionType:
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
            // General
            General_EnableLongFilePath = false;
            General_OptimizeCode = true;
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
                new IniKey("Interface", "ScaleFactor"), // Integer 100 ~ 200
                new IniKey("Plugin", "EnableCache"), // Boolean
                new IniKey("Plugin", "AutoConvertToUTF8"), // Boolean
                new IniKey("Log", "DebugLevel"), // Integer
                new IniKey("Log", "Macro"), // Boolean
                new IniKey("Log", "Comment"), // Boolean
            };
            keys = Ini.GetKeys(settingFile, keys);

            Dictionary<string, string> dict = keys.ToDictionary(x => x.Key, x => x.Value);
            string str_General_EnableLongFilePath = dict["EnableLongFilePath"];
            string str_General_OptimizeCode = dict["OptimizeCode"];
            string str_Interface_ScaleFactor = dict["ScaleFactor"];
            string str_Plugin_EnableCache = dict["EnableCache"];
            string str_Plugin_AutoConvertToUTF8 = dict["AutoConvertToUTF8"];
            string str_Log_DebugLevelIndex = dict["DebugLevel"];
            string str_Log_Macro = dict["Macro"];
            string str_Log_Comment = dict["Comment"];

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
                new IniKey("General", "EnableLongFilePath", General_EnableLongFilePath.ToString()),
                new IniKey("General", "OptimizeCode", General_OptimizeCode.ToString()),
                new IniKey("Interface", "ScaleFactor", Interface_ScaleFactor.ToString()),
                new IniKey("Plugin", "EnableCache", Plugin_EnableCache.ToString()),
                new IniKey("Plugin", "AutoConvertToUTF8", Plugin_AutoConvertToUTF8.ToString()),
                new IniKey("Log", "DebugLevel", log_DebugLevelIndex.ToString()),
                new IniKey("Log", "Macro", Log_Macro.ToString()),
                new IniKey("Log", "Comment", Log_Comment.ToString()),
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

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyUpdate(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
#endregion
    }
#endregion
}
