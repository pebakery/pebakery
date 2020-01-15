/*
    MIT License (MIT)

    Copyright (C) 2018-2020 Hajin Jang
	
	Permission is hereby granted, free of charge, to any person obtaining a copy
	of this software and associated documentation files (the "Software"), to deal
	in the Software without restriction, including without limitation the rights
	to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
	copies of the Software, and to permit persons to whom the Software is
	furnished to do so, subject to the following conditions:
	
	The above copyright notice and this permission notice shall be included in all
	copies or substantial portions of the Software.
	
	THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
	IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
	FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
	AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
	LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
	OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
	SOFTWARE.
*/

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PEBakery.WPF.Controls
{
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

        private void ColorPickButton_Click(object sender, RoutedEventArgs e)
        {
            ColorPickerViewModel pickerModel = new ColorPickerViewModel(Color);
            ColorPickerDialog picker = new ColorPickerDialog(pickerModel) { Owner = Window.GetWindow(this) };

            if (picker.ShowDialog() == true)
            {
                Color = pickerModel.Color;
            }
        }
        #endregion
    }
}
