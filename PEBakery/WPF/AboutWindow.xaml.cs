/*
    Copyright (C) 2016-2023 Hajin Jang
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

using PEBakery.Core;
using PEBakery.Core.ViewModels;
using PEBakery.Helper;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Navigation;

namespace PEBakery.WPF
{
    public partial class AboutWindow : Window
    {
        public AboutWindow(FontHelper.FontInfo monoFont)
        {
            InitializeComponent();
            DataContext = new AboutViewModel(monoFont);
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            string uri = e.Uri.AbsoluteUri;
            ResultReport result = FileHelper.OpenUri(uri);
            if (!result.Success)
            {
                MessageBox.Show(this, $"URL [{uri}] could not be opened.\r\n\r\n{result.Message}.",
                    "Error Opening URL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    #region AboutViewModel
    public class AboutViewModel : ViewModelBase
    {
        #region Field, Property, Constructor
        public FontHelper.FontInfo MonospacedFont { get; }

        public AboutViewModel(FontHelper.FontInfo monospacedFont)
        {
            MonospacedFont = monospacedFont;

            InfoPEBakeryVersion = Global.Const.ProgramVersionStrFull;
            InfoBuildDate = $"Build {Global.BuildDate:yyyyMMdd}";

            EnvInfoBuilder envInfos = new EnvInfoBuilder();
            EnvInfoSection msgSection = new EnvInfoSection(EnvInfoBuilder.FirstSectionOrder);
            msgSection.KeyValues.Add(new KeyValuePair<string, string>(string.Empty, "Please provide this info when posting to issue tracker."));
            envInfos.AddSection(msgSection);

            StringBuilder b = new StringBuilder();
            b.Append(envInfos.ToString());
            EnvironmentText = b.ToString();

            LicenseText = Properties.Resources.LicenseSimple;

        }
        #endregion

        #region Information
        private string _infoPEBakeryVersion = string.Empty;
        public string InfoPEBakeryVersion
        {
            get => _infoPEBakeryVersion;
            set => SetProperty(ref _infoPEBakeryVersion, value);
        }

        private string _infoBuildDate = "BuildDate";
        public string InfoBuildDate
        {
            get => _infoBuildDate;
            set => SetProperty(ref _infoBuildDate, value);
        }
        #endregion

        #region Environment
        private string _environmentText = string.Empty;
        public string EnvironmentText
        {
            get => _environmentText;
            set => SetProperty(ref _environmentText, value);
        }
        #endregion

        #region License
        private string _licenseText = string.Empty;
        public string LicenseText
        {
            get => _licenseText;
            set => SetProperty(ref _licenseText, value);
        }
        #endregion
    }
    #endregion
}
