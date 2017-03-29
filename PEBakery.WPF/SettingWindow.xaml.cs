using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    /// SettingWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class SettingWindow : Window
    {
        public SettingViewModel Model;

        public SettingWindow(SettingViewModel model)
        {
            Model = model;
            this.DataContext = Model;
            InitializeComponent();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void DefaultButton_Click(object sender, RoutedEventArgs e)
        {
            Model.ScaleFactor = 100;
        }
    }

    #region SettingViewModel
    public class SettingViewModel : INotifyPropertyChanged
    {
        public SettingViewModel(double scaleFactor)
        {
            this.ScaleFactor = scaleFactor;
            this.ScaleFactorText = $"Scale Factor of Plugin Interface: {scaleFactor * 100:0}%";
        }

        private string scaleFactorText;
        public string ScaleFactorText
        {
            get => scaleFactorText;
            set
            {
                scaleFactorText = value;
                OnPropertyUpdate("ScaleFactor");
            }
        }


        private double scaleFactor;
        public double ScaleFactor
        {
            get => scaleFactor;
            set
            {
                scaleFactor = value;
                ScaleFactorText = $"Scale Factor of Plugin Interface: {scaleFactor * 100:0}%";
                OnPropertyUpdate("ScaleFactor");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyUpdate(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    #endregion
}
