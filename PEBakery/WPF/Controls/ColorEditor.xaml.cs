using System;
using System.Collections.Generic;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PEBakery.WPF.Controls
{
    /// <summary>
    /// ColorNumberBox.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ColorEditor : UserControl
    {
        public ColorEditor()
        {
            InitializeComponent();

            Color = Colors.Black;
            SampleColor.Background = new SolidColorBrush(Color);
        }

        #region Property
        public Color Color
        {
            get => (Color)GetValue(ColorProperty);
            set => SetValue(ColorProperty, value);
        }

        public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(nameof(Color), typeof(Color), typeof(ColorEditor),
            new FrameworkPropertyMetadata(Colors.Black, OnColorChanged));

        private static void OnColorChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            if (!(obj is ColorEditor control))
                return;
            if (!(args.NewValue is Color c))
                return;

            control.SampleColor.Background = new SolidColorBrush(c);
            control.RedNumberBox.Value = c.R;
            control.GreenNumberBox.Value = c.G;
            control.BlueNumberBox.Value = c.B;
        }
        #endregion

        #region Internal Event Handler
        private void RedNumberBox_ValueChanged(object sender, RoutedPropertyChangedEventArgs<decimal> e)
        {
            Color = Color.FromRgb((byte)e.NewValue, Color.G, Color.B);
            SampleColor.Background = new SolidColorBrush(Color);
        }

        private void GreenNumberBox_ValueChanged(object sender, RoutedPropertyChangedEventArgs<decimal> e)
        {
            Color = Color.FromRgb(Color.R, (byte)e.NewValue, Color.B);
            SampleColor.Background = new SolidColorBrush(Color);
        }

        private void BlueNumberBox_ValueChanged(object sender, RoutedPropertyChangedEventArgs<decimal> e)
        {
            Color = Color.FromRgb(Color.R, Color.G, (byte)e.NewValue);
            SampleColor.Background = new SolidColorBrush(Color);
        }
        #endregion
    }
}
