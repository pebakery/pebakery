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

        private Brush _borderBrushBackup;
        private Thickness _borderThicknessBackup;
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
            if (e.Source is DependencyObject dObj)
            {
                _selectedElement = FindTopParentFrameworkElement(dObj);
                if (_selectedElement == null)
                    return;
            }

            // Set Z Index to top
            Canvas.SetZIndex(_selectedElement, MaxZIndex + 1);

            // Draw red borderline
            if (_selectedElement is Control control)
            {
                _borderBrushBackup = control.BorderBrush;
                _borderThicknessBackup = control.BorderThickness;

                control.BorderBrush = Brushes.Red;
                control.BorderThickness = new Thickness(2);
            }

            UIControlSelected?.Invoke(this, new UIControlSelectedEventArgs(_selectedElement, _selectedElement.Tag as UIControl));

            e.Handled = true;
        }

        protected override void OnPreviewMouseMove(MouseEventArgs e)
        {
            base.OnPreviewMouseMove(e);
        }

        protected override void OnPreviewMouseUp(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseUp(e);
        }

        public UIElement FindTopParentUIElement(DependencyObject dObj)
        {
            while (dObj != null)
            {
                if (dObj is UIElement element && Children.Contains(element))
                    return element;

                if (dObj is Visual || dObj is Visual3D)
                    dObj = VisualTreeHelper.GetParent(dObj);
                else
                    dObj = LogicalTreeHelper.GetParent(dObj);
            }
            return null;
        }

        public FrameworkElement FindTopParentFrameworkElement(DependencyObject dObj)
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
