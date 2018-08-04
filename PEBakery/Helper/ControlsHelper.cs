using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PEBakery.Helper
{
    public static class ControlsHelper
    {
        public static T VisualUpwardSearch<T>(DependencyObject source) where T : FrameworkElement
        {
            while (source != null && !(source is TreeViewItem))
                source = VisualTreeHelper.GetParent(source);
            return source as T;
        }

        public static T VisualDownwardSearch<T>(DependencyObject source) where T : FrameworkElement
        {
            Queue<DependencyObject> q = new Queue<DependencyObject>(new[] { source });
            do
            {
                source = q.Dequeue();
                if (source is T item)
                    return item;
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(source); i++)
                    q.Enqueue(VisualTreeHelper.GetChild(source, i));
            } while (q.Count > 0);
            return null;
        }
    }
}
