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
