/*
    MIT License (MIT)

    Copyright (c) 2018 Hajin Jang
	
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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using PEBakery.Core;
// ReSharper disable InconsistentNaming

namespace PEBakery.WPF.Controls
{
    public class EditCanvas : Canvas
    {
        #region Fields
        private FrameworkElement _selectedElement;
        private Border _selectedBorder;
        #endregion

        #region Properties
        private int MaxZIndex
        {
            get
            {
                int max = Canvas.GetZIndex(this);
                foreach (UIElement element in Children)
                {
                    int z = Canvas.GetZIndex(element);
                    if (max < z)
                        max = z;
                }
                return max;
            }
        }
        #endregion

        #region Events
        public class UIControlSelectedEventArgs : EventArgs
        {
            public FrameworkElement Element { get; set; }
            public UIControl UIControl { get; set; }
            public UIControlSelectedEventArgs(FrameworkElement element, UIControl uiCtrl)
            {
                Element = element;
                UIControl = uiCtrl;
            }
        }
        public delegate void UIControlSelectedEventHandler(object sender, UIControlSelectedEventArgs e);
        public event UIControlSelectedEventHandler UIControlSelected;
        #endregion

        #region Event Handler
        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            ResetSelectedBorder();

            FrameworkElement element = null;
            if (e.Source is DependencyObject dObj)
            {
                element = FindRootFrameworkElement(dObj);
            }

            if (element == null)
                return;

            DrawSelectedBorder(element);

            e.Handled = true;
        }

        public void ResetSelectedBorder()
        {
            if (_selectedBorder != null)
            {
                UIRenderer.RemoveFromCanvas(this, _selectedBorder);
                _selectedBorder = null;
            }
        }

        public void DrawSelectedBorder(UIControl uiCtrl)
        {
            FrameworkElement element = null;
            foreach (FrameworkElement child in Children)
            {
                if (child.Tag is UIControl ctrl)
                {
                    if (ctrl.Key.Equals(uiCtrl.Key, StringComparison.Ordinal))
                    {
                        element = child;
                        break;
                    }
                }
            }

            DrawSelectedBorder(element);
        }

        public void DrawSelectedBorder(FrameworkElement element)
        {
            if (element == null)
                return;

            _selectedElement = element;
            if (_selectedElement.Tag is UIControl uiCtrl)
            {                 
                _selectedBorder = new Border
                {
                    Opacity = 0.75,
                    BorderBrush = Brushes.Red,
                    BorderThickness = new Thickness(2),
                };

                // Set Z Index to top
                if (uiCtrl.Type != UIControlType.Bevel)
                {
                    Canvas.SetZIndex(_selectedElement, MaxZIndex + 1);
                    Canvas.SetZIndex(_selectedBorder, MaxZIndex + 1);
                }

                UIRenderer.DrawToCanvas(this, _selectedBorder, uiCtrl.Rect);

                UIControlSelected?.Invoke(this, new UIControlSelectedEventArgs(_selectedElement, uiCtrl));
            }
        }

        public FrameworkElement FindRootFrameworkElement(DependencyObject dObj)
        {
            while (dObj != null)
            {
                if (dObj is FrameworkElement element && Children.Contains(element))
                    return element;

                if (dObj is Visual || dObj is Visual3D)
                    dObj = VisualTreeHelper.GetParent(dObj);
                else
                    dObj = LogicalTreeHelper.GetParent(dObj);
            }
            return null;
        }
        #endregion
    }
}
