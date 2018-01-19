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

using PEBakery.Helper;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
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
    /// <summary>
    /// AboutWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class AboutWindow : Window
    {
        AboutViewModel m;

        public AboutWindow(FontHelper.WPFFont monoFont)
        {
            m = new AboutViewModel(monoFont);

            InitializeComponent();
            DataContext = m;
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(e.Uri.AbsoluteUri);
            e.Handled = true;
        }
    }

    #region UtiltiyViewModel
    public class AboutViewModel : INotifyPropertyChanged
    {
        #region Field, Property, Constructor
        public FontHelper.WPFFont MonoFont { get; private set; }
        public FontFamily MonoFontFamily => MonoFont.FontFamily;
        public FontWeight MonoFontWeight => MonoFont.FontWeight;
        public double MonoFontSize => MonoFont.FontSizeInDIP;

        public AboutViewModel(FontHelper.WPFFont monoFont)
        {
            MonoFont = monoFont;

            // Info_PEBakeryVersion = typeof(App).Assembly.GetName().Version.ToString();
            Info_PEBakeryVersion = Properties.Resources.StringVersion;
            Info_BuildDate = "Build " + Properties.Resources.BuildDate;

            License_Text = Properties.Resources.LicenseSimple;
        }
        #endregion

        #region Information
        private string info_PEBakeryVersion = string.Empty;
        public string Info_PEBakeryVersion
        {
            get => info_PEBakeryVersion;
            set
            {
                info_PEBakeryVersion = value;
                OnPropertyUpdate("Info_PEBakeryBanner");
            }
        }

        private string info_BuildDate = string.Empty;
        public string Info_BuildDate
        {
            get => info_BuildDate;
            set
            {
                info_BuildDate = value;
                OnPropertyUpdate("Info_BuildDate");
            }
        }
        #endregion

        #region License
        private string license_Text = string.Empty;
        public string License_Text
        {
            get => license_Text;
            set
            {
                license_Text = value;
                OnPropertyUpdate("License_Text");
            }
        }
        #endregion

        #region Utility
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyUpdate(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
    #endregion
}
