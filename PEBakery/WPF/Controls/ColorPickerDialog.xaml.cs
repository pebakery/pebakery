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

using PEBakery.Core.ViewModels;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace PEBakery.WPF.Controls
{
    public partial class ColorPickerDialog : Window
    {
        public ColorPickerDialog(ColorPickerViewModel model)
        {
            DataContext = model;
            InitializeComponent();
        }

        private void ApplyCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }

    public class ColorPickerViewModel : ViewModelBase
    {
        private Color _color;
        public Color Color
        {
            get => _color;
            set => SetProperty(ref _color, value);
        }

        public ColorPickerViewModel(Color c)
        {
            Color = c;
        }
    }

    public static class ColorPickerViewCommands
    {
        public static readonly RoutedCommand ApplyCommand = new RoutedUICommand("Apply Color", nameof(ApplyCommand), typeof(ColorPickerViewCommands), new InputGestureCollection
        {
            new KeyGesture(Key.S, ModifierKeys.Control),
        });
    }
}
