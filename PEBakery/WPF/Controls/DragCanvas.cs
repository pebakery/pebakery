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

using PEBakery.Core;
using System;
using System.Windows;
using System.Windows.Input;

// ReSharper disable InconsistentNaming
namespace PEBakery.WPF.Controls
{
    public class DragCanvas : EditCanvas
    {
        #region Enums and Const
        public enum DragMode
        {
            Move,
            Resize,
        }

        public enum ResizeClickPosition
        {
            Left,
            Right,
            Top,
            Bottom,
            LeftTop,
            RightTop,
            LeftBottom,
            RightBottom,
            Inside,
            Outside,
        }

        private const int CanvasWidthHeightLimit = 600;
        private const int ElementWidthHeightLimit = 100;
        #endregion

        #region Fields, Properties
        public DragMode Mode { get; set; }

        // Shared
        protected bool _isBeingDragged;
        protected Point _dragStartCursorPos;

        // Move
        protected Point _dragStartElementPos;

        // Resize
        protected ResizeClickPosition _resizeClickPos;
        protected Rect _dragStartElementRect;
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
        public delegate void UIControlMovedEventHandler(object sender, UIControlDraggedEventArgs e);
        public event UIControlMovedEventHandler UIControlMoved;

        public delegate void UIControlResizedEventHandler(object sender, UIControlDraggedEventArgs e);
        public event UIControlResizedEventHandler UIControlResized;
        #endregion

        #region Event Handler
        /// <summary>
        /// Start dragging
        /// </summary>
        /// <param name="e"></param>
        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (_isBeingDragged)
                return;

            base.OnPreviewMouseLeftButtonDown(e);
            if (_selectedElement == null)
                return;

            _dragStartCursorPos = e.GetPosition(this);
            double x = GetLeft(_selectedElement);
            double y = GetTop(_selectedElement);

            switch (Mode)
            {
                case DragMode.Move:
                    _dragStartElementPos = new Point(x, y);
                    SetMovingMouseCursor();
                    break;
                case DragMode.Resize:
                    _dragStartElementRect = new Rect(x, y, _selectedElement.Width, _selectedElement.Height);
                    _resizeClickPos = DetectResizeClickPosition(_dragStartCursorPos, _dragStartElementRect);
                    SetResizingMouseCursor(_resizeClickPos);
                    break;
            }

            _selectedElement.CaptureMouse();

            _isBeingDragged = true;
            e.Handled = true;
        }

        /// <summary>
        /// Middle of dragging 
        /// </summary>
        /// <param name="e"></param>
        protected override void OnPreviewMouseMove(MouseEventArgs e)
        {
            if (!_isBeingDragged || _selectedElement == null)
                return;

            // Moving/Resizing a UIControl
            Point nowCursorPoint = e.GetPosition(this);
            switch (Mode)
            {
                case DragMode.Move:
                    Point newElementPos = CalcNewPosition(_dragStartCursorPos, nowCursorPoint, _dragStartElementPos);
                    SetLeft(_selectedElement, newElementPos.X);
                    SetTop(_selectedElement, newElementPos.Y);
                    if (_selectedBorder != null)
                    {
                        SetLeft(_selectedBorder, newElementPos.X);
                        SetTop(_selectedBorder, newElementPos.Y);
                    }
                    break;
                case DragMode.Resize:
                    Rect newElementRect = CalcNewSize(_dragStartCursorPos, nowCursorPoint, _dragStartElementRect, _resizeClickPos);
                    SetLeft(_selectedElement, newElementRect.X);
                    SetTop(_selectedElement, newElementRect.Y);
                    _selectedElement.Width = newElementRect.Width;
                    _selectedElement.Height = newElementRect.Height;
                    if (_selectedBorder != null)
                    {
                        SetLeft(_selectedBorder, newElementRect.X);
                        SetTop(_selectedBorder, newElementRect.Y);
                        _selectedBorder.Width = newElementRect.Width;
                        _selectedBorder.Height = newElementRect.Height;
                    }
                    break;
            }
        }

        /// <summary>
        /// End of dragging
        /// </summary>
        /// <param name="e"></param>
        protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (!_isBeingDragged || _selectedElement == null)
                return;

            _selectedElement.ReleaseMouseCapture();

            if (!(_selectedElement.Tag is UIControl uiCtrl))
                return;

            Point nowCursorPoint = e.GetPosition(this);
            switch (Mode)
            {
                case DragMode.Move:
                    Point newCtrlPos = CalcNewPosition(_dragStartCursorPos, nowCursorPoint, new Point(uiCtrl.X, uiCtrl.Y));

                    // UIControl should have position/size of int
                    uiCtrl.X = (int)newCtrlPos.X;
                    uiCtrl.Y = (int)newCtrlPos.Y;

                    UIControlMoved?.Invoke(this, new UIControlDraggedEventArgs(_selectedElement, uiCtrl));
                    break;
                case DragMode.Resize:
                    Rect newCtrlRect = CalcNewSize(_dragStartCursorPos, nowCursorPoint, uiCtrl.Rect, _resizeClickPos);

                    // UIControl should have position/size of int
                    uiCtrl.X = (int)newCtrlRect.X;
                    uiCtrl.Y = (int)newCtrlRect.Y;
                    uiCtrl.Width = (int)newCtrlRect.Width;
                    uiCtrl.Height = (int)newCtrlRect.Height;

                    UIControlResized?.Invoke(this, new UIControlDraggedEventArgs(_selectedElement, uiCtrl));
                    break;
            }

            ResetMouseCursor();

            _isBeingDragged = false;
            _selectedElement = null;
        }
        #endregion

        #region Move Utility
        private static Point CalcNewPosition(Point cursorStart, Point cursorNow, Point elementStart)
        {
            double x = elementStart.X + cursorNow.X - cursorStart.X;
            double y = elementStart.Y + cursorNow.Y - cursorStart.Y;

            // Do not use Width and Height here, or canvas cannot be expanded
            if (x < 0)
                x = 0;
            else if (CanvasWidthHeightLimit < x)
                x = CanvasWidthHeightLimit;
            if (y < 0)
                y = 0;
            else if (CanvasWidthHeightLimit < y)
                y = CanvasWidthHeightLimit;

            return new Point(x, y);
        }
        #endregion

        #region Resize Utility
        private ResizeClickPosition DetectResizeClickPosition(Point cursor, Rect elementRect)
        {
            ResizeClickPosition clickPos;

            // Get Border Margin
            double xBorderMargin = elementRect.Width / 4;
            double yBorderMargin = elementRect.Height / 4;

            // Decide direction of X
            double leftRelPos = cursor.X - elementRect.Left;
            double rightRelPos = elementRect.Right - cursor.X;

            // Decide direction of Y
            double topRelPos = cursor.Y - elementRect.Top;
            double bottomRelPos = elementRect.Bottom - cursor.Y;

            // Get ResizeDirection
            if (Math.Abs(leftRelPos) <= xBorderMargin)
            { // Left
                if (Math.Abs(topRelPos) <= yBorderMargin) // Top
                    clickPos = ResizeClickPosition.LeftTop;
                else if (Math.Abs(bottomRelPos) <= yBorderMargin) // Bottom
                    clickPos = ResizeClickPosition.LeftBottom;
                else if (0 <= topRelPos && 0 <= bottomRelPos) // Y-Inside
                    clickPos = ResizeClickPosition.Left;
                else // Y-Outside
                    clickPos = ResizeClickPosition.Outside;
            }
            else if (Math.Abs(rightRelPos) <= xBorderMargin)
            { // Right
                if (Math.Abs(topRelPos) <= yBorderMargin) // Top
                    clickPos = ResizeClickPosition.RightTop;
                else if (Math.Abs(bottomRelPos) <= yBorderMargin) // Bottom
                    clickPos = ResizeClickPosition.RightBottom;
                else if (0 <= topRelPos && 0 <= bottomRelPos) // Y-Inside
                    clickPos = ResizeClickPosition.Right;
                else // Y-Outside
                    clickPos = ResizeClickPosition.Outside;
            }
            else if (0 <= leftRelPos && 0 <= rightRelPos)
            { // X-Inside
                if (Math.Abs(topRelPos) <= yBorderMargin) // Top
                    clickPos = ResizeClickPosition.Top;
                else if (Math.Abs(bottomRelPos) <= yBorderMargin) // Bottom
                    clickPos = ResizeClickPosition.Bottom;
                else if (0 <= topRelPos && 0 <= bottomRelPos) // Y-Inside
                    clickPos = ResizeClickPosition.Inside;
                else // Y-Outside
                    clickPos = ResizeClickPosition.Outside;
            }
            else
            { // X-Outside
                clickPos = ResizeClickPosition.Outside;
            }

            return clickPos;
        }

        private static Rect CalcNewSize(Point cursorStart, Point cursorNow, Rect elementRect, ResizeClickPosition clickPos)
        {
            const int MinLineLen = 16;

            // Do not touch Width and Height if border was not clicked
            switch (clickPos)
            {
                case ResizeClickPosition.Inside:
                case ResizeClickPosition.Outside:
                    return elementRect;
            }

            // Get delta of X and Y
            double xDelta = cursorNow.X - cursorStart.X;
            double yDelta = cursorNow.Y - cursorStart.Y;

            // Prepare variables
            double x = elementRect.X;
            double y = elementRect.Y;
            double width = elementRect.Width;
            double height = elementRect.Height;

            // X Direction
            switch (clickPos)
            {
                case ResizeClickPosition.Left:
                case ResizeClickPosition.LeftTop:
                case ResizeClickPosition.LeftBottom:
                    if (Math.Abs(xDelta) < double.Epsilon)
                        break;
                    if (0 < xDelta)
                    { // L [->    ] R, delta is positive
                        if (xDelta + MinLineLen < width)
                        {
                            x += xDelta;
                            width -= xDelta;
                        }
                        else
                        { // Guard
                            x += width - MinLineLen;
                            width = MinLineLen;
                        }
                    }
                    else
                    { // L <-[    ] R, delta is negative
                        x += xDelta;
                        width -= xDelta;
                    }
                    break;
                case ResizeClickPosition.Right:
                case ResizeClickPosition.RightTop:
                case ResizeClickPosition.RightBottom:
                    if (Math.Abs(xDelta) < double.Epsilon)
                        break;
                    if (0 < xDelta)
                    { // L [    ]-> R, delta is positive
                        width += xDelta;
                    }
                    else
                    { // L [    <-] R, delta is negative
                        if (MinLineLen < width + xDelta) // Guard
                            width += xDelta;
                        else
                            width = MinLineLen;
                    }
                    break;
            }

            // Y Direction
            switch (clickPos)
            {
                case ResizeClickPosition.Top:
                case ResizeClickPosition.LeftTop:
                case ResizeClickPosition.RightTop:
                    if (Math.Abs(yDelta) < double.Epsilon)
                        break;
                    if (0 < yDelta)
                    { // T [->    ] B, delta is positive
                        if (yDelta + MinLineLen < height)
                        {
                            y += yDelta;
                            height -= yDelta;
                        }
                        else
                        { // Guard
                            y += height - MinLineLen;
                            height = MinLineLen;
                        }
                    }
                    else
                    { // T <-[    ] B, delta is negative
                        y += yDelta;
                        height -= yDelta;
                    }
                    break;
                case ResizeClickPosition.Bottom:
                case ResizeClickPosition.LeftBottom:
                case ResizeClickPosition.RightBottom:
                    if (Math.Abs(yDelta) < double.Epsilon)
                        break;
                    if (0 < yDelta)
                    { // T [    ]-> B, delta is positive
                        height += yDelta;
                    }
                    else
                    { // T [    <-] B, delta is negative
                        if (MinLineLen < height + yDelta) // Guard
                            height += yDelta;
                        else
                            height = MinLineLen;
                    }
                    break;
            }

            // Check if X and Width is correct
            switch (clickPos)
            {
                case ResizeClickPosition.Left:
                case ResizeClickPosition.LeftTop:
                case ResizeClickPosition.LeftBottom:
                    if (x < 0)
                    {
                        width += x;
                        x = 0;
                    }
                    break;
                case ResizeClickPosition.Right:
                case ResizeClickPosition.RightTop:
                case ResizeClickPosition.RightBottom:
                    if (CanvasWidthHeightLimit + ElementWidthHeightLimit < x + width)
                        width = CanvasWidthHeightLimit + ElementWidthHeightLimit - x;
                    break;
            }

            // Check if Y and Height is correct
            switch (clickPos)
            {
                case ResizeClickPosition.Top:
                case ResizeClickPosition.LeftTop:
                case ResizeClickPosition.RightTop:
                    if (y < 0)
                    {
                        height += y;
                        y = 0;
                    }
                    break;
                case ResizeClickPosition.Bottom:
                case ResizeClickPosition.LeftBottom:
                case ResizeClickPosition.RightBottom:
                    if (CanvasWidthHeightLimit + ElementWidthHeightLimit < y + height)
                        height = CanvasWidthHeightLimit + ElementWidthHeightLimit - y;
                    break;
            }

            return new Rect(x, y, width, height);
        }
        #endregion

        #region Mouse Cursor
        private static void SetMovingMouseCursor()
        {
            if (Mouse.OverrideCursor != Cursors.Hand)
                Mouse.OverrideCursor = Cursors.Hand;
        }

        private static void SetResizingMouseCursor(ResizeClickPosition clickPos)
        {
            Cursor newCursor = null;
            switch (clickPos)
            {
                case ResizeClickPosition.Left:
                case ResizeClickPosition.Right:
                    newCursor = Cursors.SizeWE;
                    break;
                case ResizeClickPosition.Top:
                case ResizeClickPosition.Bottom:
                    newCursor = Cursors.SizeNS;
                    break;
                case ResizeClickPosition.LeftTop:
                case ResizeClickPosition.RightBottom:
                    newCursor = Cursors.SizeNWSE;
                    break;
                case ResizeClickPosition.RightTop:
                case ResizeClickPosition.LeftBottom:
                    newCursor = Cursors.SizeNESW;
                    break;
            }

            if (Mouse.OverrideCursor != newCursor)
                Mouse.OverrideCursor = newCursor;
        }

        private static void ResetMouseCursor()
        {
            if (Mouse.OverrideCursor != null)
                Mouse.OverrideCursor = null;
        }
        #endregion
    }
}
