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
            // Interface
            Interface_ScaleFactor = 100;
            // Plugin
            Plugin_EnableCache = true;
            Plugin_AutoConvertToUTF8 = false;
            // Log
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
                new IniKey("Interface", "ScaleFactor"), // Integer 100 ~ 200
                new IniKey("Plugin", "EnableCache"), // Boolean
                new IniKey("Plugin", "AutoConvertToUTF8"), // Boolean
            };
            keys = Ini.GetKeys(settingFile, keys);

            Dictionary<string, string> dict = keys.ToDictionary(x => x.Key, x => x.Value);
            string str_General_EnableLongFilePath = dict["EnableLongFilePath"];
            string str_Interface_ScaleFactor = dict["ScaleFactor"];
            string str_Plugin_EnableCache = dict["EnableCache"];
            string str_Plugin_AutoConvertToUTF8 = dict["AutoConvertToUTF8"];

            // General - EnableLongFilePath (Default = False)
            if (str_General_EnableLongFilePath.Equals("True", StringComparison.OrdinalIgnoreCase))
                General_EnableLongFilePath = true;

            // Interface - ScaleFactor (Default = 100)
            if (int.TryParse(str_Interface_ScaleFactor, NumberStyles.Integer, CultureInfo.InvariantCulture, out int scaleFactor))
            {
                if (100 <= scaleFactor && scaleFactor <= 200)
                    Interface_ScaleFactor = scaleFactor;
            }

            // Plugin - EnableCache (Default = True)
            if (str_Plugin_EnableCache.Equals("False", StringComparison.OrdinalIgnoreCase))
                Plugin_EnableCache = false;

            // Plugin - AutoConvertToUTF8 (Default = False)
            if (str_Plugin_AutoConvertToUTF8.Equals("True", StringComparison.OrdinalIgnoreCase))
                Plugin_AutoConvertToUTF8 = true;
        }

        public void WriteToFile()
        {
            IniKey[] keys = new IniKey[]
            {
                new IniKey("General", "EnableLongFilePath", General_EnableLongFilePath.ToString()),
                new IniKey("Interface", "ScaleFactor", Interface_ScaleFactor.ToString()),
                new IniKey("Plugin", "EnableCache", Plugin_EnableCache.ToString()),
                new IniKey("Plugin", "AutoConvertToUTF8", Plugin_AutoConvertToUTF8.ToString()),
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
