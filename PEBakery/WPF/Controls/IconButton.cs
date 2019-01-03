/*
    MIT License (MIT)

    Copyright (c) 2018 Hajin Jang
	
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

using MahApps.Metro.IconPacks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PEBakery.WPF.Controls
{
    public class IconButton : Button
    {
        #region Constructor
        public IconButton()
        {
            BorderBrush = Brushes.Transparent;
            BorderThickness = new Thickness(0);
            Background = Brushes.Transparent;
            Foreground = Brushes.Transparent;

            Content = _iconMaterial = new PackIconMaterial
            {
                Width = double.NaN,
                Height = double.NaN,
                Margin = new Thickness(4),
                Kind = IconMaterialKind,
                Foreground = BaseForeground,
            };

            MouseEnter += MainIconButton_MouseEnter;
            MouseLeave += MainIconButton_MouseLeave;
            IsEnabledChanged += MainIconButton_IsEnabledChanged;
        }

        ~IconButton()
        {
            MouseEnter -= MainIconButton_MouseEnter;
            MouseLeave -= MainIconButton_MouseLeave;
            IsEnabledChanged -= MainIconButton_IsEnabledChanged;
        }
        #endregion

        #region Event Handler
        private void MainIconButton_MouseEnter(object sender, MouseEventArgs e)
        { // Focused
            if (_iconMaterial == null)
                return;

            Color c = BaseForeground.Color;
            _iconMaterial.Foreground = new SolidColorBrush(Color.FromArgb(153, c.R, c.G, c.B));
        }

        private void MainIconButton_MouseLeave(object sender, MouseEventArgs e)
        { // Default
            if (_iconMaterial == null)
                return;

            _iconMaterial.Foreground = BaseForeground;
        }

        private void MainIconButton_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_iconMaterial == null)
                return;
            if ((bool)e.NewValue)
            { // Enabled
                _iconMaterial.Foreground = BaseForeground;
            }
            else
            { // Disabled
                Color c = BaseForeground.Color;
                _iconMaterial.Foreground = new SolidColorBrush(Color.FromArgb(102, c.R, c.G, c.B));
            }
        }

        #endregion

        #region Property
        private readonly PackIconMaterial _iconMaterial;

        public static readonly DependencyProperty IconMaterialKindProperty = DependencyProperty.Register(nameof(IconMaterialKind),
            typeof(PackIconMaterialKind), typeof(IconButton), new FrameworkPropertyMetadata(DefaultIconMaterialKind, OnPackIconMaterialPropertyChanged));
        private const PackIconMaterialKind DefaultIconMaterialKind = PackIconMaterialKind.None;
        public PackIconMaterialKind IconMaterialKind
        {
            get => (PackIconMaterialKind)GetValue(IconMaterialKindProperty);
            set => SetValue(IconMaterialKindProperty, value);
        }

        public static readonly DependencyProperty BaseForegroundProperty = DependencyProperty.Register(nameof(BaseForeground),
            typeof(SolidColorBrush), typeof(IconButton), new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromArgb(255, 0, 0, 0)), OnBaseForegroundPropertyChanged));
        public SolidColorBrush BaseForeground
        {
            get => (SolidColorBrush)GetValue(BaseForegroundProperty);
            set => SetValue(BaseForegroundProperty, value);
        }
        #endregion

        #region Callbacks
        private static void OnPackIconMaterialPropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            if (!(obj is IconButton button))
                return;

            if (button.Content is PackIconMaterial control)
                control.Kind = (PackIconMaterialKind)args.NewValue;
        }

        private static void OnBaseForegroundPropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            if (!(obj is IconButton button))
                return;
            if (!(args.NewValue is SolidColorBrush newBrush))
                return;

            if (button.Content is PackIconMaterial control)
                control.Foreground = newBrush;
        }
        #endregion
    }
}
