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
    public class MainIconButton : Button
    {
        #region Constructor
        public MainIconButton()
        {
            _foreground = new SolidColorBrush(Color.FromArgb(255, 0, 0, 0));

            Loaded += MainIconButton_Loaded;

            MouseEnter += MainIconButton_MouseEnter;
            MouseLeave += MainIconButton_MouseLeave;
            IsEnabledChanged += MainIconButton_IsEnabledChanged;
        }

        ~MainIconButton()
        {
            MouseEnter -= MainIconButton_MouseEnter;
            MouseLeave -= MainIconButton_MouseLeave;
            IsEnabledChanged -= MainIconButton_IsEnabledChanged;
        }
        #endregion

        #region Event Handler
        private void MainIconButton_Loaded(object sender, RoutedEventArgs e)
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
                Foreground = _foreground,
            };

            // Need to be called only once
            Loaded -= MainIconButton_Loaded;
        }

        private void MainIconButton_MouseEnter(object sender, MouseEventArgs e)
        {
            if (_iconMaterial == null)
                return;
            Color c = _foreground.Color;
            _iconMaterial.Foreground = _foreground = new SolidColorBrush(Color.FromArgb(127, c.R, c.G, c.B));
        }

        private void MainIconButton_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_iconMaterial == null)
                return;
            Color c = _foreground.Color;
            _iconMaterial.Foreground = _foreground = new SolidColorBrush(Color.FromArgb(255, c.R, c.G, c.B));
        }

        private void MainIconButton_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_iconMaterial == null)
                return;
            if ((bool)e.NewValue)
            { // Enabled
                Color c = _foreground.Color;
                _iconMaterial.Foreground = _foreground = new SolidColorBrush(Color.FromArgb(c.A, 0, 0, 0));
            }
            else
            { // Disabled
                Color c = _foreground.Color;
                _iconMaterial.Foreground = _foreground = new SolidColorBrush(Color.FromArgb(c.A, 128, 128, 128));
            }
        }

        #endregion

        #region Property
        private PackIconMaterial _iconMaterial;
        private SolidColorBrush _foreground;

        private const PackIconMaterialKind DefaultIconMaterialKind = PackIconMaterialKind.None;
        public PackIconMaterialKind IconMaterialKind
        {
            get => (PackIconMaterialKind)GetValue(IconMaterialKindProperty);
            set => SetValue(IconMaterialKindProperty, value);
        }

        public static readonly DependencyProperty IconMaterialKindProperty = DependencyProperty.Register(nameof(IconMaterialKind),
            typeof(PackIconMaterialKind), typeof(PackIconMaterial), new FrameworkPropertyMetadata(DefaultIconMaterialKind, OnPropertyChanged));
        #endregion

        #region Callbacks
        private static void OnPropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            PackIconMaterial control = (PackIconMaterial)obj;
            control.Kind = (PackIconMaterialKind)args.NewValue;
        }
        #endregion
    }
}
