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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace PEBakery.Core.UIControls
{
    /// <summary>
    /// Interaction logic for ModalDialog.xaml
    /// </summary>
    internal partial class CustomMessageBoxWindow : Window
    {
        #region Fields and Properties
        internal string Caption
        {
            get => Title;
            set => Title = value;
        }

        internal string Message
        {
            get => TextBlock_Message.Text;
            set => TextBlock_Message.Text = value;
        }

        internal string OkButtonText
        {
            get => Label_Ok.Content.ToString();
            set => Label_Ok.Content = TryAddKeyboardAccellerator(value);
        }

        internal string CancelButtonText
        {
            get => Label_Cancel.Content.ToString();
            set => Label_Cancel.Content = TryAddKeyboardAccellerator(value);
        }

        internal string YesButtonText
        {
            get => Label_Yes.Content.ToString();
            set => Label_Yes.Content = TryAddKeyboardAccellerator(value);
        }

        internal string NoButtonText
        {
            get => Label_No.Content.ToString();
            set => Label_No.Content = TryAddKeyboardAccellerator(value);
        }

        internal int TimeoutSecond { get; set; } // Set this to 0 to disable
        public MessageBoxResult Result { get; set; }
        #endregion

        #region Constructor
        internal CustomMessageBoxWindow(string message, int timeout = 0)
        {
            InitializeComponent();

            Message = message;
            Image_MessageBox.Visibility = Visibility.Collapsed;
            DisplayButtons(MessageBoxButton.OK);
            SetTimeout(timeout);
        }

        internal CustomMessageBoxWindow(string message, string caption, int timeout = 0)
        {
            InitializeComponent();

            Message = message;
            Caption = caption;
            Image_MessageBox.Visibility = Visibility.Collapsed;
            DisplayButtons(MessageBoxButton.OK);
            SetTimeout(timeout);
        }

        internal CustomMessageBoxWindow(string message, string caption, MessageBoxButton button, int timeout = 0)
        {
            InitializeComponent();

            Message = message;
            Caption = caption;
            Image_MessageBox.Visibility = Visibility.Collapsed;

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
            Image_MessageBox.Visibility = Visibility.Collapsed;

            DisplayButtons(button);
            DisplayImage(image);
            SetTimeout(timeout);
        }
        #endregion

        #region Internal Methods
        private void DisplayButtons(MessageBoxButton button)
        {
            switch (button)
            {
                case MessageBoxButton.OKCancel:
                    // Hide all but OK, Cancel
                    Button_OK.Visibility = Visibility.Visible;
                    Button_OK.Focus();
                    Button_Cancel.Visibility = Visibility.Visible;

                    Button_Yes.Visibility = Visibility.Collapsed;
                    Button_No.Visibility = Visibility.Collapsed;
                    break;
                case MessageBoxButton.YesNo:
                    // Hide all but Yes, No
                    Button_Yes.Visibility = Visibility.Visible;
                    Button_Yes.Focus();
                    Button_No.Visibility = Visibility.Visible;

                    Button_OK.Visibility = Visibility.Collapsed;
                    Button_Cancel.Visibility = Visibility.Collapsed;
                    break;
                case MessageBoxButton.YesNoCancel:
                    // Hide only OK
                    Button_Yes.Visibility = Visibility.Visible;
                    Button_Yes.Focus();
                    Button_No.Visibility = Visibility.Visible;
                    Button_Cancel.Visibility = Visibility.Visible;

                    Button_OK.Visibility = Visibility.Collapsed;
                    break;
                default:
                    // Hide all but OK
                    Button_OK.Visibility = Visibility.Visible;
                    Button_OK.Focus();

                    Button_Yes.Visibility = Visibility.Collapsed;
                    Button_No.Visibility = Visibility.Collapsed;
                    Button_Cancel.Visibility = Visibility.Collapsed;
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

            Image_MessageBox.Source = ToImageSource(icon);
            Image_MessageBox.Visibility = Visibility.Visible;
        }

        private void SetTimeout(int timeout)
        {
            TimeoutSecond = timeout;
            if (timeout == 0)
            { // No Timeout

                TextBlock_Timeout.Visibility = Visibility.Collapsed;
                ProgressBar_Timeout.Visibility = Visibility.Collapsed;
            }
            else
            {  // Timeout is set
                TextBlock_Timeout.Visibility = Visibility.Visible;
                ProgressBar_Timeout.Visibility = Visibility.Visible;
            }
        }
        #endregion

        #region Event Handler
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

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (TimeoutSecond == 0)
                return; // Timeout not set

            /*
            // 
            TextBlock_Timeout.Text = TimeoutSecond.ToString();
            SetPercent(ProgressBar_Timeout, 100.0 / TimeoutSecond, TimeSpan.FromSeconds(1));
            */

            // Setup ProgressBar Timer and let it run in background
            int elapsed = 0;
            while (true)
            {
                TextBlock_Timeout.Text = (TimeoutSecond - elapsed).ToString();
                SetPercent(ProgressBar_Timeout, (elapsed + 1) * 100.0 / TimeoutSecond, TimeSpan.FromSeconds(1));

                if (elapsed == TimeoutSecond)
                {
                    Result = MessageBoxResult.None;
                    Close();
                }

                elapsed += 1;
                await Task.Delay(1000);
            }
        }
        #endregion

        #region Utility
        internal static ImageSource ToImageSource(Icon icon)
        {
            ImageSource imageSource = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            return imageSource;
        }

        /// <summary>
        /// Keyboard Accellerators are used in Windows to allow easy shortcuts to controls like Buttons and 
        /// MenuItems. These allow users to press the Alt key, and a shortcut key will be highlighted on the 
        /// control. If the user presses that key, that control will be activated.
        /// This method checks a string if it contains a keyboard accellerator. If it doesn't, it adds one to the
        /// beginning of the string. If there are two strings with the same accellerator, Windows handles it.
        /// The keyboard accellerator character for WPF is underscore (_). It will not be visible.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        internal static string TryAddKeyboardAccellerator(string input)
        {
            const string accellerator = "_";            // This is the default WPF accellerator symbol - used to be & in WinForms

            // If it already contains an accellerator, do nothing
            if (input.Contains(accellerator)) return input;

            return accellerator + input;
        }

        public static void SetPercent(ProgressBar progressBar, double percentage, TimeSpan duration)
        {
            DoubleAnimation animation = new DoubleAnimation(percentage, duration);
            progressBar.BeginAnimation(RangeBase.ValueProperty, animation);
        }
        #endregion
    }
}
