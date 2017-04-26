using PEBakery.Lib;
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
    }

    #region SettingViewModel
    public class SettingViewModel : INotifyPropertyChanged
    {
        private readonly string settingFile;

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
        private bool enable_EnableCache;
        public bool Plugin_EnableCache
        {
            get => enable_EnableCache;
            set
            {
                enable_EnableCache = value;
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

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyUpdate(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
    #endregion
}
