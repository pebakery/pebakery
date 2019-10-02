/*
    MIT License (MIT)

    Copyright (C) 2018-2019 Hajin Jang
	
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

using PEBakery.Helper;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PEBakery.WPF.Controls
{
    public partial class ColorPicker : UserControl
    {
        #region Fields and Constructor
        private bool _dragSaturationValueCanvas = false;
        private bool _dragHueCanvas = false;
        private double _hue = 0;
        private double _saturation = 1;
        private double _value = 1;

        private enum FocusedElement
        {
            None = 0,
            SaturationValueCanvas = 1,
            HueCanvas = 2,
        }
        private FocusedElement _focusedElement = FocusedElement.None;

        public ColorPicker()
        {
            InitializeComponent();

            Color = Colors.Red;
        }

        private void ColorPickerControl_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateAllTracks();
        }
        #endregion

        #region Depedency Property
        public Color Color
        {
            get => (Color)GetValue(ColorProperty);
            set => SetValue(ColorProperty, value);
        }

        public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(nameof(Color), typeof(Color), typeof(ColorPicker),
            new FrameworkPropertyMetadata(Colors.Red, OnColorChanged));

        private static void OnColorChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            if (!(obj is ColorPicker control))
                return;
            if (!(args.NewValue is Color c))
                return;

            control.SampleColor.Background = new SolidColorBrush(c);
            if (control.RedNumberBox != null)
                control.RedNumberBox.Value = c.R;
            if (control.GreenNumberBox != null)
                control.GreenNumberBox.Value = c.G;
            if (control.BlueNumberBox != null)
                control.BlueNumberBox.Value = c.B;

            if (control._focusedElement == FocusedElement.None)
            {
                (control._hue, control._saturation, control._value) = FromRgbToHsv(c);
                control.UpdateAllTracks();
            }
        }
        #endregion

        #region Internal Event Handler
        private void RedNumberBox_ValueChanged(object sender, RoutedPropertyChangedEventArgs<decimal> e)
        {
            // Only if NumberBox was touched by the user, not canvases
            if (_focusedElement == FocusedElement.None)
                Color = Color.FromRgb((byte)e.NewValue, Color.G, Color.B);
        }

        private void GreenNumberBox_ValueChanged(object sender, RoutedPropertyChangedEventArgs<decimal> e)
        {
            // Only if NumberBox was touched by the user, not canvases
            if (_focusedElement == FocusedElement.None)
                Color = Color.FromRgb(Color.R, (byte)e.NewValue, Color.B);
        }

        private void BlueNumberBox_ValueChanged(object sender, RoutedPropertyChangedEventArgs<decimal> e)
        {
            // Only if NumberBox was touched by the user, not canvases
            if (_focusedElement == FocusedElement.None)
                Color = Color.FromRgb(Color.R, Color.G, (byte)e.NewValue);
        }
        #endregion

        #region SaturationValueCanvas Mouse Event Handlers
        private void SaturationValueCanvas_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_dragSaturationValueCanvas)
                return;

            SaturationValueCanvas.CaptureMouse();
            _focusedElement = FocusedElement.SaturationValueCanvas;
            _dragSaturationValueCanvas = true;
        }

        private void SaturationValueCanvas_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragSaturationValueCanvas)
                return;
            if (!(sender is IInputElement ie))
                return;

            // Get mouse position in the canvas
            double width = SaturationValueCanvas.ActualWidth;
            double height = SaturationValueCanvas.ActualHeight;

            Point p = e.GetPosition(ie);
            (double x, double y) = GuardPointCoordinate(p, width, height);

            // Get new Saturation and Value
            _saturation = x / width;
            _value = 1 - y / height;

            // Set SaturationValueTrack's position
            Canvas.SetLeft(SaturationValueTrack, x - SaturationValueTrack.ActualWidth / 2);
            Canvas.SetTop(SaturationValueTrack, y - SaturationValueTrack.ActualHeight / 2);

            // Convert HSV to RGB
            Color = FromHsvToRgb(_hue, _saturation, _value);
            SaturationValueTrack.Background = new SolidColorBrush(Color);
        }

        private void SaturationValueCanvas_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            SaturationValueCanvas.ReleaseMouseCapture();
            _focusedElement = FocusedElement.None;
            _dragSaturationValueCanvas = false;
        }
        #endregion

        #region HueCanvas Mouse Event Handlers
        private void HueCanvas_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_dragHueCanvas)
                return;

            HueCanvas.CaptureMouse();
            _focusedElement = FocusedElement.HueCanvas;
            _dragHueCanvas = true;
        }

        private void HueCanvas_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragHueCanvas)
                return;
            if (!(sender is IInputElement ie))
                return;

            // Get mouse position in the canvas
            double width = HueCanvas.ActualWidth;

            Point p = e.GetPosition(ie);
            (double x, _) = GuardPointCoordinate(p, width, 0);

            // Get new Hue
            _hue = x / width;

            // Set HueTrack's position
            Canvas.SetLeft(HueTrack, x - HueTrack.ActualWidth / 2);

            // Set SaturationValueCanvas' Hue
            Color hueColor = FromHsvToRgb(_hue, 1, 1);
            SaturationValueCanvasHue.Color = hueColor;
            HueTrack.Background = new SolidColorBrush(hueColor);

            // Convert HSV to RGB
            Color = FromHsvToRgb(_hue, _saturation, _value);
            SaturationValueTrack.Background = new SolidColorBrush(Color);
        }

        private void HueCanvas_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            HueCanvas.ReleaseMouseCapture();
            _focusedElement = FocusedElement.None;
            _dragHueCanvas = false;
        }
        #endregion

        #region UpdateAllTrakcs
        private void UpdateAllTracks()
        {
            // Set SaturationValueCanvas/HueTrack's Hue
            Color hueColor = FromHsvToRgb(_hue, 1, 1);
            SaturationValueCanvasHue.Color = hueColor;
            HueTrack.Background = new SolidColorBrush(hueColor);

            // Set SaturationValueTrack's position
            Canvas.SetLeft(SaturationValueTrack, _saturation * SaturationValueCanvas.ActualWidth - SaturationValueTrack.ActualWidth / 2);
            Canvas.SetTop(SaturationValueTrack, (1 - _value) * SaturationValueCanvas.ActualHeight - SaturationValueTrack.ActualHeight / 2);
            SaturationValueTrack.Background = new SolidColorBrush(Color);

            // Set HueTrack's position
            Canvas.SetLeft(HueTrack, _hue * HueCanvas.ActualWidth - HueTrack.ActualWidth / 2);
        }
        #endregion

        #region GuardPointCoordinate
        public (double x, double y) GuardPointCoordinate(Point p, double widthLimit, double heightLimit)
        {
            double x = p.X;
            double y = p.Y;

            if (x < 0)
                x = 0;
            else if (widthLimit < x)
                x = widthLimit;

            if (y < 0)
                y = 0;
            else if (heightLimit < y)
                y = heightLimit;

            return (x, y);
        }
        #endregion

        #region HSV and RGB
        private static (double h, double s, double v) FromRgbToHsv(Color c)
        {
            // https://en.wikipedia.org/wiki/HSL_and_HSV

            // Convert R, G, B to [0, 1] range
            double r = c.R / 255.0;
            double g = c.G / 255.0;
            double b = c.B / 255.0;

            // Get max and min
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));

            // Hue
            double h;
            if (NumberHelper.DoubleEquals(max, min))
                h = 0;
            else if (NumberHelper.DoubleEquals(max, r))
                h = (g - b) / (max - min) / 6;
            else if (NumberHelper.DoubleEquals(max, g))
                h = (2 + (b - r) / (max - min)) / 6;
            else // if (NumberHelper.DoubleEquals(max, b))
                h = (4 + (r - g) / (max - min)) / 6;
            while (h < 0)
                h += 1;

            // Saturation
            double s;
            if (NumberHelper.DoubleEquals(max, 0))
                s = 0;
            else
                s = (max - min) / max;

            // Value is max
            return (h, s, max);
        }

        /// <summary>
        /// Convert HSV to RGB
        /// </summary>
        /// <param name="h">Hue, 0 ~ 1</param>
        /// <param name="s">Saturation, 0 ~ 1</param>
        /// <param name="v">Value, 0 ~ 1</param>
        /// <returns></returns>
        private static Color FromHsvToRgb(double h, double s, double v)
        {
            // https://en.wikipedia.org/wiki/HSL_and_HSV#From_HSV

            if (NumberHelper.DoubleEquals(h, 1))
                h = 0;

            // Chroma
            double c = v * s;

            // Get R, G, B
            double h2 = 6 * h;
            double x = c * (1 - Math.Abs(h2 % 2 - 1));

            double r = 0;
            double g = 0;
            double b = 0;
            switch ((int)h2)
            {
                case 0:
                    r = c;
                    g = x;
                    b = 0;
                    break;
                case 1:
                    r = x;
                    g = c;
                    b = 0;
                    break;
                case 2:
                    r = 0;
                    g = c;
                    b = x;
                    break;
                case 3:
                    r = 0;
                    g = x;
                    b = c;
                    break;
                case 4:
                    r = x;
                    g = 0;
                    b = c;
                    break;
                case 5:
                case 6:
                    r = c;
                    g = 0;
                    b = x;
                    break;
            }

            // Convert R, G, B to [0, 255] range
            double m = v - c;
            r = r + m;
            g = g + m;
            b = b + m;

            // Return R, G, B
            return Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
        }
        #endregion
    }
}
