/*
    Based on work of paraglider (https://github.com/paraglidernc)

    MIT License (MIT)
    
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
using System.Windows.Controls;

namespace PEBakery.WPF.Controls
{
    public static class BringIntoViewBehavior
    {
        public static readonly DependencyProperty BringIntoViewProperty = DependencyProperty.RegisterAttached("BringIntoView", typeof(bool), typeof(BringIntoViewBehavior), new UIPropertyMetadata(false, BringIntoViewChanged));

        /// <summary>
        /// Get accessor for dependency property
        /// </summary>
        /// <param name="obj">Current dependency object</param>
        /// <returns>Value of BringIntoView Property</returns>
        public static bool GetBringIntoView(DependencyObject obj)
        {
            return (bool)obj.GetValue(BringIntoViewProperty);
        }

        /// <summary>
        /// Setter for  dependency property
        /// </summary>
        /// <param name="obj">Current dependency object</param>
        /// <param name="value">New value</param>
        public static void SetBringIntoView(DependencyObject obj, bool value)
        {
            obj.SetValue(BringIntoViewProperty, value);
        }

        /// <summary>
        /// Dependency Property Changed Event
        /// </summary>
        /// <param name="objControl">Sending control</param>
        /// <param name="e">Event Data</param>
        /// <remarks>This used to attach the TreeViewItemSelected method to treeview item selected property</remarks>
        private static void BringIntoViewChanged(DependencyObject objControl, DependencyPropertyChangedEventArgs e)
        {
            if (objControl is not TreeViewItem treeitem || e.NewValue is not bool newVal)
                return;

            if (newVal)
                treeitem.Selected += TreeViewItemSelected;
            else
                treeitem.Selected -= TreeViewItemSelected;
        }

        /// <summary>
        /// Tree View Selected item Event
        /// </summary>
        /// <param name="sender">Sending control</param>
        /// <param name="e">Event data</param>
        private static void TreeViewItemSelected(object sender, RoutedEventArgs e)
        {
            if (!sender.Equals(e.OriginalSource))
                return;
            if (sender is TreeViewItem treeitem)
                treeitem.BringIntoView();
        }
    }
}
