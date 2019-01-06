/*
    Copyright (C) 2018 Hajin Jang
    Licensed under MIT License.
 
    MIT License

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

using System.Collections.Generic;
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
