// Based on work of paraglider

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            if (!(objControl is TreeViewItem treeitem) || !(e.NewValue is bool newVal))
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
