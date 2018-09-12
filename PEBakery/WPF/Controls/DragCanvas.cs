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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using PEBakery.Core;

// ReSharper disable InconsistentNaming

namespace PEBakery.WPF.Controls
{
    public class DragCanvas : EditCanvas
    {
        #region Fields
        protected Point _dragStartCursorPos;
        protected Point _dragStartElementPos;
        protected bool _isBeingDragged;
        #endregion

        #region Events
        public class UIControlDraggedEventArgs : EventArgs
        {
            public FrameworkElement Element { get; set; }
            public UIControl UIControl { get; set; }
            public UIControlDraggedEventArgs(FrameworkElement element, UIControl uiCtrl)
            {
                Element = element;
                UIControl = uiCtrl;
            }
        }
        public delegate void UIControlDraggedEventHandler(object sender, UIControlDraggedEventArgs e);
        public event UIControlDraggedEventHandler UIControlDragged;
        #endregion

        #region Event Handler
        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (_isBeingDragged)
                return;

            base.OnPreviewMouseLeftButtonDown(e);
            if (_selectedElement == null)
                return;

            double x = GetLeft(_selectedElement);
            double y = GetTop(_selectedElement);
            _dragStartCursorPos = e.GetPosition(this);
            _dragStartElementPos = new Point(x, y);
            _selectedElement.CaptureMouse();

            _isBeingDragged = true;
            e.Handled = true;
        }

        protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (_isBeingDragged && _selectedElement != null)
            {
                _selectedElement.ReleaseMouseCapture();

                if (!(_selectedElement.Tag is UIControl uiCtrl))
                    return;

                Point nowCursorPoint = e.GetPosition(this);
                Point newCtrlPos = CalcNewPosition(_dragStartCursorPos, nowCursorPoint, new Point(uiCtrl.Rect.X, uiCtrl.Rect.Y));

                // UIControl should have position/size of int
                uiCtrl.Rect.X = (int)newCtrlPos.X;
                uiCtrl.Rect.Y = (int)newCtrlPos.Y;

                UIControlDragged?.Invoke(this, new UIControlDraggedEventArgs(_selectedElement, uiCtrl));

                _isBeingDragged = false;
                _selectedElement = null;
            }
        }

        protected override void OnPreviewMouseMove(MouseEventArgs e)
        {
            if (!_isBeingDragged || _selectedElement == null)
                return;

            Point nowCursorPoint = e.GetPosition(this);
            Point newElementPos = CalcNewPosition(_dragStartCursorPos, nowCursorPoint, _dragStartElementPos);

            SetLeft(_selectedElement, newElementPos.X);
            SetTop(_selectedElement, newElementPos.Y);
            if (_selectedBorder != null)
            {
                SetLeft(_selectedBorder, newElementPos.X);
                SetTop(_selectedBorder, newElementPos.Y);
            }
        }

        public Point CalcNewPosition(Point cursorStart, Point cursorNow, Point elementStart)
        {
            double x = cursorNow.X - cursorStart.X + elementStart.X;
            double y = cursorNow.Y - cursorStart.Y + elementStart.Y;

            // Do not check Width and Height here, or canvas cannot be expanded
            if (x < 0)
                x = 0;
            //else if (Width - _selectedElement.Width < x)
            //    x = Width - _selectedElement.Width;
            if (y < 0)
                y = 0;
            //else if (Height - _selectedElement.Height < y)
            //    y = Height - _selectedElement.Height;

            return new Point(x, y);
        }
        #endregion
    }
}
