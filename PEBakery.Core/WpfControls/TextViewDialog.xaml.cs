/*
    MIT License (MIT)

    Copyright (C) 2019-2023 Hajin Jang
	
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

namespace PEBakery.WPF.Controls
{
    public partial class TextViewDialog : Window
    {
        #region Properties
        public PackIconMaterialKind MessageIcon
        {
            get => MessageIconMaterial.Kind;
            set
            {
                if (value == PackIconMaterialKind.None)
                    MessageIconMaterial.Visibility = Visibility.Collapsed;
                else
                    MessageIconMaterial.Visibility = Visibility.Visible;

                MessageIconMaterial.Kind = value;
            }
        }

        public string MessageText
        {
            get => MessageTextBlock.Text;
            set => MessageTextBlock.Text = value;
        }

        public string ViewText
        {
            get => ViewTextBox.Text;
            set => ViewTextBox.Text = value;
        }

        public TextWrapping TextWrapping
        {
            get => ViewTextBox.TextWrapping;
            set
            {
                switch (value)
                {
                    case TextWrapping.NoWrap:
                        TextScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                        break;
                    case TextWrapping.Wrap:
                    case TextWrapping.WrapWithOverflow:
                        TextScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                        break;
                }
                ViewTextBox.TextWrapping = value;
            }
        }
        #endregion

        #region Constructor
        public TextViewDialog(string title, string message, string viewText,
            PackIconMaterialKind icon = PackIconMaterialKind.None)
        {
            InitializeComponent();

            Title = title;
            MessageText = message;
            ViewText = viewText;
            MessageIcon = icon;
            TextWrapping = TextWrapping.NoWrap;
        }

        public TextViewDialog(Window owner, string title, string message, string viewText,
            PackIconMaterialKind icon = PackIconMaterialKind.None)
        {
            InitializeComponent();

            Owner = owner;
            Title = title;
            MessageText = message;
            ViewText = viewText;
            MessageIcon = icon;
            TextWrapping = TextWrapping.NoWrap;
        }
        #endregion

        #region Methods
        /// <summary>
        /// Displays a message box within a Dispatcher, that has a message, title bar caption, button, and icon; and that returns a result.
        /// </summary>
        public static void DispatcherShow(Window? owner, string title, string message, string viewText, PackIconMaterialKind icon = PackIconMaterialKind.None)
        {
            if (Application.Current?.Dispatcher != null)
            {
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    TextViewDialog tvDialog;
                    if (owner != null)
                        tvDialog = new TextViewDialog(owner, title, message, viewText, icon);
                    else if (Application.Current.MainWindow != null)
                        tvDialog = new TextViewDialog(Application.Current.MainWindow, title, message, viewText, icon);
                    else
                        tvDialog = new TextViewDialog(title, message, viewText, icon);
                    tvDialog.Show();
                });
            }
            else
            {
                TextViewDialog tvDialog = new TextViewDialog(title, message, viewText, icon);
                tvDialog.Show();
            }
        }
        #endregion

        #region Event Handler
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        #endregion
    }
}