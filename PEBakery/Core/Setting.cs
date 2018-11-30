using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using PEBakery.Helper;

namespace PEBakery.Core
{
    public class Setting : INotifyPropertyChanged
    {
        #region struct General
        public struct General
        {
            public bool OptimizeCode;
            public bool ShowLogAfterBuild;
            public bool StopBuildOnError;
            public bool EnableLongFilePath;
            public bool UseCustomUserAgent;
            public string CustomUserAgent;
        }
        #endregion

        #region struct Interface
        public struct Interface
        {
            public string MonospaceFontStr;
            public FontHelper.FontInfo MonospaceFont;
            public double ScaleFactor;
            public bool UseCustomEditor;
            public string CustomEditorPath;
            public bool DisplayShellExecuteConOut;
        }
        #endregion

        #region struct Script
        public struct Script
        {
            public bool EnableCache;
            public string CacheState;
            public bool AutoSyntaxCheck;
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
}
