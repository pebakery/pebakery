using PEBakery.Core;
using PEBakery.Helper;
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
        UtilityViewModel m;

        public UtilityWindow(FontHelper.WPFFont monoFont)
        {
            m = new UtilityViewModel(monoFont);

            InitializeComponent();
            DataContext = m;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void EscapeButton_Click(object sender, RoutedEventArgs e)
        {
            string str = StringEscaper.QuoteEscape(m.Escaper_StringToConvert);
            if (m.EscapePercentChecked)
                m.Escaper_ConvertedString = StringEscaper.EscapePercent(str);
            else
                m.Escaper_ConvertedString = str;
        }

        private void UnescapeButton_Click(object sender, RoutedEventArgs e)
        {
            string str = StringEscaper.QuoteUnescape(m.Escaper_StringToConvert);
            if (m.EscapePercentChecked)
                m.Escaper_ConvertedString = StringEscaper.UnescapePercent(str);
            else
                m.Escaper_ConvertedString = str;
        }

        private void EscapeSequenceLegend_Click(object sender, RoutedEventArgs e)
        {
            m.Escaper_ConvertedString = StringEscaper.Legend;
        }

        private void SyntaxCheckButton_Click(object sender, RoutedEventArgs e)
        {
            string[] lines = m.Syntax_InputCode.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            m.Syntax_Output = "Not Imeplemented";

        }

        private void CodeBoxRunButton_Click(object sender, RoutedEventArgs e)
        {
            // m.CodeBox_Input;
            //p = new Plugin(PluginType.Plugin, pPath, this, projectRoot, null);
        }
    }

    #region UtiltiyViewModel
    public class UtilityViewModel : INotifyPropertyChanged
    {
        public FontHelper.WPFFont MonoFont { get; private set; }
        public FontFamily MonoFontFamily { get => MonoFont.FontFamily; }
        public FontWeight MonoFontWeight { get => MonoFont.FontWeight; }
        public double MonoFontSize { get => MonoFont.FontSizeInDIP; }

        public UtilityViewModel(FontHelper.WPFFont monoFont)
        {
            // MainWindow w = Application.Current.MainWindow as MainWindow;
            MonoFont = monoFont;
        }

        #region String Escaper
        private string escaper_StringToConvert = string.Empty;
        public string Escaper_StringToConvert
        {
            get => escaper_StringToConvert;
            set
            {
                escaper_StringToConvert = value;
                OnPropertyUpdate("Escaper_StringToConvert");
            }
        }

        private string escaper_ConvertedString = string.Empty;
        public string Escaper_ConvertedString
        {
            get => escaper_ConvertedString;
            set
            {
                escaper_ConvertedString = value;
                OnPropertyUpdate("Escaper_ConvertedString");
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
        #endregion

        #region CodeBox
        private int codeBox_SelectedProjectIndex;
        public int CodeBox_SelectedProjectIndex
        {
            get => codeBox_SelectedProjectIndex;
            set
            {
                codeBox_SelectedProjectIndex = value;
                OnPropertyUpdate("CodeBox_SelectedProjectIndex");
            }
        }

        private Tuple<string, Project> codeBox_Projects;
        public Tuple<string, Project> CodeBox_Projects
        {
            get => codeBox_Projects;
            set
            {
                codeBox_Projects = value;
                OnPropertyUpdate("CodeBox_Projects");
            }
        }

        private string codeBox_Input = string.Empty;
        public string CodeBox_Input
        {
            get => codeBox_Input;
            set
            {
                codeBox_Input = value;
                OnPropertyUpdate("CodeBox_Input");
            }
        }
        #endregion

        #region Syntax Checker
        private string syntax_InputCode = string.Empty;
        public string Syntax_InputCode
        {
            get => syntax_InputCode;
            set
            {
                syntax_InputCode = value;
                OnPropertyUpdate("Syntax_InputCode");
            }
        }

        private string syntax_Output = string.Empty;
        public string Syntax_Output
        {
            get => syntax_Output;
            set
            {
                syntax_Output = value;
                OnPropertyUpdate("Syntax_Output");
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
