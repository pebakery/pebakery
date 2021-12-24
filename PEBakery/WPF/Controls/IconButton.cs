/*
    MIT License (MIT)

    Copyright (C) 2018-2022 Hajin Jang
	
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
using System.Windows.Media;

namespace PEBakery.WPF.Controls
{
    /// <summary>
    /// A button with PackIconMaterial. Must be paired with 'IconButtonStyle' of Styles.xaml.
    /// </summary>
    public class IconButton : Button
    {
        #region Constructor
        public IconButton()
        {
            BorderBrush = Brushes.Transparent;
            BorderThickness = new Thickness(0);
            Background = Brushes.Transparent;
            Foreground = Brushes.Transparent;

            Content = new PackIconMaterial
            {
                Width = double.NaN,
                Height = double.NaN,
                Margin = IconMargin,
                Kind = IconMaterialKind,
                Foreground = IconForeground,
            };
        }

        ~IconButton()
        {
        }
        #endregion

        #region Event Handler
        #endregion

        #region Property
        public static readonly DependencyProperty IconMaterialKindProperty = DependencyProperty.Register(nameof(IconMaterialKind),
            typeof(PackIconMaterialKind), typeof(IconButton), new FrameworkPropertyMetadata(DefaultIconMaterialKind, OnPackIconMaterialPropertyChanged));
        private const PackIconMaterialKind DefaultIconMaterialKind = PackIconMaterialKind.None;
        public PackIconMaterialKind IconMaterialKind
        {
            get => (PackIconMaterialKind)GetValue(IconMaterialKindProperty);
            set => SetValue(IconMaterialKindProperty, value);
        }

        public static readonly DependencyProperty IconForegroundProperty = DependencyProperty.Register(nameof(IconForeground),
            typeof(Brush), typeof(IconButton), new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromArgb(255, 0, 0, 0)), OnIconForegroundPropertyChanged));
        public Brush IconForeground
        {
            get => (Brush)GetValue(IconForegroundProperty);
            set => SetValue(IconForegroundProperty, value);
        }

        public static readonly DependencyProperty IconOpacityProperty = DependencyProperty.Register(nameof(IconOpacity),
            typeof(double), typeof(IconButton), new FrameworkPropertyMetadata(1.0, OnIconOpacityPropertyChanged));
        public double IconOpacity
        {
            get => (double)GetValue(IconOpacityProperty);
            set => SetValue(IconOpacityProperty, value);
        }

        public static readonly DependencyProperty IconMarginProperty = DependencyProperty.Register(nameof(IconMargin),
            typeof(Thickness), typeof(IconButton), new FrameworkPropertyMetadata(new Thickness(0), OnIconMarginPropertyChanged));
        public Thickness IconMargin
        {
            get => (Thickness)GetValue(IconMarginProperty);
            set => SetValue(IconMarginProperty, value);
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

        private static void OnIconForegroundPropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            if (!(obj is IconButton button))
                return;
            if (!(args.NewValue is SolidColorBrush newBrush))
                return;

            if (button.Content is PackIconMaterial control)
                control.Foreground = newBrush;
        }

        private static void OnIconOpacityPropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            if (!(obj is IconButton button))
                return;
            if (!(args.NewValue is double newOpacity))
                return;

            if (button.Content is PackIconMaterial control)
                control.Opacity = newOpacity;
        }

        private static void OnIconMarginPropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            if (!(obj is IconButton button))
                return;
            if (!(args.NewValue is Thickness newMargin))
                return;

            if (button.Content is PackIconMaterial control)
                control.Margin = newMargin;
        }
        #endregion
    }
}
