/*
    MIT License (MIT)

    Copyright (C) 2019 Hajin Jang
	
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

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PEBakery.Core.WpfControls
{
    public class SelectTextOnFocus : DependencyObject
    {
        #region Properties
        public bool Active
        {
            get => (bool)GetValue(ActiveProperty);
            set => SetValue(ActiveProperty, value);
        }

        public static readonly DependencyProperty ActiveProperty = DependencyProperty.RegisterAttached(nameof(Active),
            typeof(bool), typeof(SelectTextOnFocus), new FrameworkPropertyMetadata(false, OnActiveChanged));
        #endregion

        #region Depedency Property Get/Set
        [AttachedPropertyBrowsableForType(typeof(TextBox))]
        [AttachedPropertyBrowsableForChildren(IncludeDescendants = false)]
        public static bool GetActive(DependencyObject obj) => (bool)obj.GetValue(ActiveProperty);
        public static void SetActive(DependencyObject obj, bool value) => obj.SetValue(ActiveProperty, value);
        #endregion

        #region Callback
        private static void OnActiveChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            if (!(obj is TextBox textBox && e.NewValue is bool b))
                return;

            if (b)
            {
                textBox.PreviewMouseLeftButtonDown += TextBoxHandleMouseButton;
                textBox.GotKeyboardFocus += TextBoxSelectAllText;
            }
            else
            {
                textBox.PreviewMouseLeftButtonDown -= TextBoxHandleMouseButton;
                textBox.GotKeyboardFocus -= TextBoxSelectAllText;
            }
        }
        #endregion

        #region TextBox Event Handler
        private static void TextBoxHandleMouseButton(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox textbox && !textbox.IsKeyboardFocusWithin)
            {
                if (e.OriginalSource.GetType().Name.Equals("TextBoxView", StringComparison.Ordinal))
                {
                    e.Handled = true;
                    textbox.Focus();
                }
            }
        }

        private static void TextBoxSelectAllText(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is TextBox textBox)
                textBox.SelectAll();
        }
        #endregion
    }
}
