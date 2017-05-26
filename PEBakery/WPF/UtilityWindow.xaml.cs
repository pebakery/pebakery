using PEBakery.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
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
    /// <summary>
    /// UtilityWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class UtilityWindow : Window
    {
        UtilityViewModel model = new UtilityViewModel();

        public UtilityWindow()
        {
            InitializeComponent();
            DataContext = model;
        }

        private void EscapeButton_Click(object sender, RoutedEventArgs e)
        {
            string str = StringEscaper.QuoteEscape(model.StringToConvert);
            if (model.EscapePercentChecked)
                model.ConvertedString = StringEscaper.EscapePercent(str);
            else
                model.ConvertedString = str;
        }

        private void UnescapeButton_Click(object sender, RoutedEventArgs e)
        {
            string str = StringEscaper.QuoteUnescape(model.StringToConvert);
            if (model.EscapePercentChecked)
                model.ConvertedString = StringEscaper.UnescapePercent(str);
            else
                model.ConvertedString = str;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void EscapeSequenceLegend_Click(object sender, RoutedEventArgs e)
        {
            model.ConvertedString = StringEscaper.Legend;
        }
    }

    #region UtiltiyViewModel
    public class UtilityViewModel : INotifyPropertyChanged
    {
        public UtilityViewModel()
        {
            MainWindow w = Application.Current.MainWindow as MainWindow;
        }

        private string stringToConvert = string.Empty;
        public string StringToConvert
        {
            get => stringToConvert;
            set
            {
                stringToConvert = value;
                OnPropertyUpdate("StringToConvert");
            }
        }

        private string convertedString = string.Empty;
        public string ConvertedString
        {
            get => convertedString;
            set
            {
                convertedString = value;
                OnPropertyUpdate("ConvertedString");
            }
        }

        private bool escapePercentChecked = false;
        public bool EscapePercentChecked
        {
            get => escapePercentChecked;
            set
            {
                escapePercentChecked = value;
                OnPropertyUpdate("EscapePercentChecked");
            }
        }

        // 

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
