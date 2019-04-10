using PEBakery.Core;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

// #define EXPERIMENTAL_GLOBAL_TEXTBOX_AUTOFOCUS

namespace PEBakery
{
    // ReSharper disable RedundantExtendsListEntry
    public partial class App : Application
    {
        internal void App_Startup(object sender, StartupEventArgs e)
        {
            Global.PreInit(e.Args, false);

#if EXPERIMENTAL_GLOBAL_TEXTBOX_AUTOFOCUS
            RegisterTextBoxEvents();
#endif
        }

#if EXPERIMENTAL_GLOBAL_TEXTBOX_AUTOFOCUS
        #region Event Handler
        /// <summary>
        /// Select all text in a TextBox control when the control gets the focus.
        /// </summary>
        protected void RegisterTextBoxEvents()
        {
            EventManager.RegisterClassHandler(typeof(TextBox), UIElement.PreviewMouseLeftButtonDownEvent,
              new MouseButtonEventHandler(TextBoxHandleMouseButton), true);
            EventManager.RegisterClassHandler(typeof(TextBox), UIElement.GotKeyboardFocusEvent,
              new RoutedEventHandler(TextBoxSelectAllText), true);
        }

        private static void TextBoxHandleMouseButton(object sender, MouseButtonEventArgs e)
        {
            var textbox = (sender as TextBox);
            if (textbox != null && !textbox.IsKeyboardFocusWithin)
            {
                if (e.OriginalSource.GetType().Name == "TextBoxView")
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
    #endif
    }
}
