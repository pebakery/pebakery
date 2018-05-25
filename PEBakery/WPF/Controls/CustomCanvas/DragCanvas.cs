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
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace PEBakery.WPF.Controls
{
    public class DragCanvas : Canvas
    {
        #region Fields
        private Point _dragStartCursorPos;
        private Point _dragStartElementPos;
        private bool _isBeingDragged;
        private UIElement _selectedElement;
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
        public class UIElementDragEventArgs : EventArgs
        {
            public UIElement Element { get; set; }
            public UIElementDragEventArgs(UIElement element)
            {
                Element = element;
            }
        }
        public delegate void UIElementDragEventHandler(object sender, UIElementDragEventArgs e);
#pragma warning disable 67
        public event UIElementDragEventHandler UIElementDragEvent;
#pragma warning restore 67
        #endregion

        #region Event Handler
        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (_isBeingDragged)
                return;

            if (e.Source is DependencyObject dObj)
            {
                _selectedElement = FindTargetUIElement(dObj);
                if (_selectedElement == null)
                    return;
            }

            double x = Canvas.GetLeft(_selectedElement);
            double y = Canvas.GetTop(_selectedElement);
            _dragStartCursorPos = e.GetPosition(this);
            _dragStartElementPos = new Point(x, y);

            Canvas.SetZIndex(_selectedElement, MaxZIndex + 1);
            _isBeingDragged = true;
            e.Handled = true;
        }

        protected override void OnPreviewMouseMove(MouseEventArgs e)
        {
            if (!_isBeingDragged || _selectedElement == null)
                return;

            Point nowCursorPoint = e.GetPosition(this);

            Point CalcNewPosition(Point cursorStart, Point cursorNow, Point elementStart)
            {
                double x = cursorNow.X - cursorStart.X + elementStart.X;
                double y = cursorNow.Y - cursorStart.Y + elementStart.Y;

                // Do not check ActualWidth and ActualHeight here, or canvas cannot be expanded
                if (x < 0)
                    x = 0;
                if (y < 0)
                    y = 0;

                return new Point(x, y);
            }

            Point newElementPos = CalcNewPosition(_dragStartCursorPos, nowCursorPoint, _dragStartElementPos);

            Canvas.SetLeft(_selectedElement, newElementPos.X);
            Canvas.SetTop(_selectedElement, newElementPos.Y);
        }

        protected override void OnPreviewMouseUp(MouseButtonEventArgs e)
        {
            if (_isBeingDragged || _selectedElement != null)
            {               
                _isBeingDragged = false;
                _selectedElement = null;
            }
        }

        public UIElement FindTargetUIElement(DependencyObject dObj)
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
        #endregion
    }
}
