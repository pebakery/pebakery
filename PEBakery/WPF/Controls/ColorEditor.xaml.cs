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
            control.Red = c.R;
            control.Green = c.G;
            control.Blue = c.B;
        }

        public byte Red
        {
            get => Color.R;
            set => Color = Color.FromRgb(value, Color.G, Color.B);
        }
        public byte Green
        {
            get => Color.G;
            set => Color = Color.FromRgb(Color.R, value, Color.B);
        }
        public byte Blue
        {
            get => Color.B;
            set => Color = Color.FromRgb(Color.R, Color.G, value);
        }
        #endregion
    }
}
