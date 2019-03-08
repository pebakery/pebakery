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

using System.Windows;
using MahApps.Metro.IconPacks;

namespace PEBakery.WPF.Controls
{
    /// <summary>
    /// TextBoxDialog.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class TextBoxDialog : Window
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

        public string InputText
        {
            get => InputTextBox.Text;
            set => InputTextBox.Text = value;
        }
        #endregion

        #region Constructor
        public TextBoxDialog(string title, string message, PackIconMaterialKind icon = PackIconMaterialKind.None)
        {
            InitializeComponent();

            Title = title;
            MessageText = message;
            InputText = string.Empty;
            MessageIcon = icon;
        }

        public TextBoxDialog(string title, string message, string defaultInput, PackIconMaterialKind icon = PackIconMaterialKind.None)
        {
            InitializeComponent();

            Title = title;
            MessageText = message;
            InputText = defaultInput;
            MessageIcon = icon;
        }

        public TextBoxDialog(Window owner, string title, string message, PackIconMaterialKind icon = PackIconMaterialKind.None)
        {
            InitializeComponent();

            Owner = owner;
            Title = title;
            MessageText = message;
            InputText = string.Empty;
            MessageIcon = icon;
        }

        public TextBoxDialog(Window owner, string title, string message, string defaultInput, PackIconMaterialKind icon = PackIconMaterialKind.None)
        {
            InitializeComponent();

            Owner = owner;
            Title = title;
            MessageText = message;
            InputText = defaultInput;
            MessageIcon = icon;
        }
        #endregion

        #region Event Handler
        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
        #endregion
    }
}
