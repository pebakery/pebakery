/*
    Derived from https://github.com/evanwon/WPFCustomMessageBox commit 5ee25a2d4fc71d369e73d2e5aed5ae178df9ee8d

    MIT License (MIT)

    Copyright (c) 2013 Evan Wondrasek / Apricity Software LLC
    
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
using System.Drawing;
using System.Windows;
using System.Windows.Threading;

namespace PEBakery.WPF.Controls
{
    /// <summary>
    /// Interaction logic for ModalDialog.xaml
    /// </summary>
    internal partial class CustomMessageBoxWindow : Window
    {
        internal string Caption
        {
            get
            {
                return Title;
            }
            set
            {
                Title = value;
            }
        }

        internal string Message
        {
            get
            {
                return TextBlock_Message.Text;
            }
            set
            {
                TextBlock_Message.Text = value;
            }
        }

        internal string OkButtonText
        {
            get
            {
                return Label_Ok.Content.ToString();
            }
            set
            {
                Label_Ok.Content = value.TryAddKeyboardAccellerator();
            }
        }

        internal string CancelButtonText
        {
            get
            {
                return Label_Cancel.Content.ToString();
            }
            set
            {
                Label_Cancel.Content = value.TryAddKeyboardAccellerator();
            }
        }

        internal string YesButtonText
        {
            get
            {
                return Label_Yes.Content.ToString();
            }
            set
            {
                Label_Yes.Content = value.TryAddKeyboardAccellerator();
            }
        }

        internal string NoButtonText
        {
            get
            {
                return Label_No.Content.ToString();
            }
            set
            {
                Label_No.Content = value.TryAddKeyboardAccellerator();
            }
        }

        internal int Timeout { get; set; } // Set this to 0 to disable

        public MessageBoxResult Result { get; set; }

        internal CustomMessageBoxWindow(string message, int timeout = 0)
        {
            InitializeComponent();

            Message = message;
            Image_MessageBox.Visibility = System.Windows.Visibility.Collapsed;
            DisplayButtons(MessageBoxButton.OK);
            SetTimeout(timeout);
        }

        internal CustomMessageBoxWindow(string message, string caption, int timeout = 0)
        {
            InitializeComponent();

            Message = message;
            Caption = caption;
            Image_MessageBox.Visibility = System.Windows.Visibility.Collapsed;
            DisplayButtons(MessageBoxButton.OK);
            SetTimeout(timeout);
        }

        internal CustomMessageBoxWindow(string message, string caption, MessageBoxButton button, int timeout = 0)
        {
            InitializeComponent();

            Message = message;
            Caption = caption;
            Image_MessageBox.Visibility = System.Windows.Visibility.Collapsed;

            DisplayButtons(button);
            SetTimeout(timeout);
        }

        internal CustomMessageBoxWindow(string message, string caption, MessageBoxImage image, int timeout = 0)
        {
            InitializeComponent();

            Message = message;
            Caption = caption;
            DisplayImage(image);
            DisplayButtons(MessageBoxButton.OK);
            SetTimeout(timeout);
        }

        internal CustomMessageBoxWindow(string message, string caption, MessageBoxButton button, MessageBoxImage image, int timeout = 0)
        {
            InitializeComponent();

            Message = message;
            Caption = caption;
            Image_MessageBox.Visibility = System.Windows.Visibility.Collapsed;
            
            DisplayButtons(button);
            DisplayImage(image);
            SetTimeout(timeout);
        }

        private void DisplayButtons(MessageBoxButton button)
        {
            switch (button)
            {
                case MessageBoxButton.OKCancel:
                    // Hide all but OK, Cancel
                    Button_OK.Visibility = System.Windows.Visibility.Visible;
                    Button_OK.Focus();
                    Button_Cancel.Visibility = System.Windows.Visibility.Visible;

                    Button_Yes.Visibility = System.Windows.Visibility.Collapsed;
                    Button_No.Visibility = System.Windows.Visibility.Collapsed;
                    break;
                case MessageBoxButton.YesNo:
                    // Hide all but Yes, No
                    Button_Yes.Visibility = System.Windows.Visibility.Visible;
                    Button_Yes.Focus();
                    Button_No.Visibility = System.Windows.Visibility.Visible;

                    Button_OK.Visibility = System.Windows.Visibility.Collapsed;
                    Button_Cancel.Visibility = System.Windows.Visibility.Collapsed;
                    break;
                case MessageBoxButton.YesNoCancel:
                    // Hide only OK
                    Button_Yes.Visibility = System.Windows.Visibility.Visible;
                    Button_Yes.Focus();
                    Button_No.Visibility = System.Windows.Visibility.Visible;
                    Button_Cancel.Visibility = System.Windows.Visibility.Visible;

                    Button_OK.Visibility = System.Windows.Visibility.Collapsed;
                    break;
                default:
                    // Hide all but OK
                    Button_OK.Visibility = System.Windows.Visibility.Visible;
                    Button_OK.Focus();

                    Button_Yes.Visibility = System.Windows.Visibility.Collapsed;
                    Button_No.Visibility = System.Windows.Visibility.Collapsed;
                    Button_Cancel.Visibility = System.Windows.Visibility.Collapsed;
                    break;
            }
        }

        private void DisplayImage(MessageBoxImage image)
        {
            Icon icon;

            switch (image)
            {
                case MessageBoxImage.Exclamation:       // Enumeration value 48 - also covers "Warning"
                    icon = SystemIcons.Exclamation;
                    break;
                case MessageBoxImage.Error:             // Enumeration value 16, also covers "Hand" and "Stop"
                    icon = SystemIcons.Hand;
                    break;
                case MessageBoxImage.Information:       // Enumeration value 64 - also covers "Asterisk"
                    icon = SystemIcons.Information;
                    break;
                case MessageBoxImage.Question:
                    icon = SystemIcons.Question;
                    break;
                default:
                    icon = SystemIcons.Information;
                    break;
            }

            Image_MessageBox.Source = icon.ToImageSource();
            Image_MessageBox.Visibility = System.Windows.Visibility.Visible;
        }

        private void SetTimeout(int timeout)
        {
            if (0 < timeout) // Timeout is set
            {
                Timeout = timeout;
                TextBlock_Timeout.Visibility = Visibility.Visible;
                ProgressBar_Timeout.Visibility = Visibility.Visible;
            }
            else // No Timeout
            {
                Timeout = 0; 
                TextBlock_Timeout.Visibility = Visibility.Collapsed;
                ProgressBar_Timeout.Visibility = Visibility.Collapsed;
            }
        }

        private void Button_OK_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.OK;
            Close();
        }

        private void Button_Cancel_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Cancel;
            Close();
        }

        private void Button_Yes_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Yes;
            Close();
        }

        private void Button_No_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.No;
            Close();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (0 < Timeout)
            { // Timeout set
                TextBlock_Timeout.Text = Timeout.ToString();
                ProgressBar_Timeout.SetPercent(100.0 / Timeout, TimeSpan.FromSeconds(1));

                DispatcherTimer Timer = new DispatcherTimer();

                int elapsed = 0;
                Timer.Interval = TimeSpan.FromSeconds(1);
                Timer.Tick += (object tickSender, EventArgs tickArgs) =>
                {
                    elapsed += 1;
                    TextBlock_Timeout.Text = (Timeout - elapsed).ToString();
                    ProgressBar_Timeout.SetPercent((elapsed + 1) * 100.0 / Timeout, TimeSpan.FromSeconds(1));

                    if (elapsed == Timeout)
                    {
                        Result = MessageBoxResult.None;
                        Close();
                    }
                };
                Timer.Start();
            }
        }
    }
}
