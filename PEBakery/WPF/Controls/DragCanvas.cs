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
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

// ReSharper disable InconsistentNaming
namespace PEBakery.WPF.Controls
{
    public class DragCanvas : Canvas
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

        // HeightLimit
        private const int CanvasWidthHeightLimit = 600;
        private const int ElementWidthHeightLimit = 100;

        // DragHandle
        private const int DragHandleLength = 6;
        private const int DragHandleShowThreshold = 20;
        #endregion

        #region Fields, Properties
        // SelectedElement
        private FrameworkElement _selectedElement;
        private DragMode _dragMode;

        // Border
        private Border _selectedBorder;

        // Shared
        private bool _isBeingDragged;
        private Point _dragStartCursorPos;
        private ResizeClickPosition _selectedClickPos;
        private Rect _dragStartElementRect;

        // DragHandle
        private readonly List<Border> _dragHandles = new List<Border>(8);
        private Border _selectedDragHandle;

        // Max Z Index
        private int MaxZIndex
        {
            get
            {
                int max = GetZIndex(this);
                foreach (UIElement element in Children)
                {
                    int z = GetZIndex(element);
                    if (max < z)
                        max = z;
                }
                return max;
            }
        }
        #endregion

        #region Constructor
        public DragCanvas()
        {
            // Set Background to always fire OnPreviewMouseMove on DragCanvas
            Background = Brushes.Transparent;
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

        public class UIControlDraggedEventArgs : EventArgs
        {
            public FrameworkElement Element { get; set; }
            public UIControl UIControl { get; set; }
            public double DeltaX { get; set; }
            public double DeltaY { get; set; }
            public bool ForceUpdate { get; set; }
            public UIControlDraggedEventArgs(FrameworkElement element, UIControl uiCtrl, double deltaX, double deltaY, bool forceUpdate = false)
            {
                Element = element;
                UIControl = uiCtrl;
                DeltaX = deltaX;
                DeltaY = deltaY;
                ForceUpdate = forceUpdate;
            }
        }
        public delegate void UIControlMovedEventHandler(object sender, UIControlDraggedEventArgs e);
        public event UIControlMovedEventHandler UIControlMoved;

        public delegate void UIControlResizedEventHandler(object sender, UIControlDraggedEventArgs e);
        public event UIControlResizedEventHandler UIControlResized;
        #endregion

        #region Mouse Event Handler
        /// <summary>
        /// Clear mouse cursor when mouse is not hovering DragCanvas, such as close of the window.
        /// </summary>
        protected override void OnMouseLeave(MouseEventArgs e)
        {
            // Sometime this is called outside of STA thread
            Dispatcher.Invoke(ResetMouseCursor);
        }

        /// <summary>
        /// Start dragging
        /// </summary>
        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (_isBeingDragged)
                return;

            FrameworkElement element = FindRootFrameworkElement(e.Source);

            // No UIControl was selected
            if (element == null)
                return;

            if (element is Border border && border.Tag is DragHandleInfo info)
            { // Clicked drag handle
                // Resize mode
                _dragMode = DragMode.Resize;

                // Record select information
                _selectedDragHandle = border;
                _selectedClickPos = info.ClickPos;
                _selectedElement = info.Parent;
                _isBeingDragged = true;

                // Record position and size
                _dragStartCursorPos = e.GetPosition(this);
                _dragStartElementRect = GetElementSize(_selectedElement);

                // Set Cursor
                SetMouseCursor(_selectedClickPos);

                // Capture mouse
                _selectedDragHandle.CaptureMouse();
            }
            else if (element.Tag is UIControl)
            { // Clicked UIControl
                ClearSelectedBorderHandles();

                // Move mode
                _dragMode = DragMode.Move;

                // Record select information
                _selectedDragHandle = null;
                _selectedClickPos = ResizeClickPosition.Inside;
                _selectedElement = element;
                _isBeingDragged = true;

                // Record position and size
                _dragStartCursorPos = e.GetPosition(this);
                _dragStartElementRect = GetElementSize(_selectedElement);

                DrawSelectedBorderHandles();

                // Set Cursor
                SetMouseCursor();

                // Capture mouse
                _selectedElement.CaptureMouse();
            }
            else
            { // Clicked background
                _selectedDragHandle = null;
                _selectedElement = null;
                _isBeingDragged = false;

                ClearSelectedBorderHandles();
            }

            e.Handled = true;
        }

        /// <summary>
        /// Middle of dragging 
        /// </summary>
        /// <param name="e"></param>
        protected override void OnPreviewMouseMove(MouseEventArgs e)
        {
            // Change cursor following underlying element
            if (!_isBeingDragged)
            {
                FrameworkElement hoverElement = FindRootFrameworkElement(e.Source);
                if (hoverElement == null) // Outside
                    ResetMouseCursor();
                else if (hoverElement is Border border && border.Tag is DragHandleInfo info)
                    SetMouseCursor(info.ClickPos);
                else if (hoverElement.Tag is UIControl) // Inside
                    SetMouseCursor();
                else 
                    ResetMouseCursor();
                return;
            }

            Debug.Assert(_selectedElement != null, "SelectedElement was not set");

            // Moving/Resizing a UIControl
            Point nowCursorPos = e.GetPosition(this);

            switch (_dragMode)
            {
                case DragMode.Move:
                    Point dragStartElementPos = new Point(_dragStartElementRect.X, _dragStartElementRect.Y);
                    Point newElementPos = CalcNewPosition(_dragStartCursorPos, nowCursorPos, dragStartElementPos);

                    MoveSelectedBorderHandles(new Rect
                    {
                        X = newElementPos.X,
                        Y = newElementPos.Y,
                        Width = _dragStartElementRect.Width,
                        Height = _dragStartElementRect.Height,
                    });
                    break;
                case DragMode.Resize:
                    Rect newElementRect = CalcNewSize(_dragStartCursorPos, nowCursorPos, _dragStartElementRect, _selectedClickPos);

                    ResizeSelectedBorderHandles(newElementRect);
                    break;
            }
        }

        /// <summary>
        /// End of dragging
        /// </summary>
        /// <param name="e"></param>
        protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (!_isBeingDragged)
                return;

            Debug.Assert(_selectedElement != null, "SelectedElement was not set");

            _selectedElement.ReleaseMouseCapture();
            _selectedDragHandle?.ReleaseMouseCapture();

            if (!(_selectedElement.Tag is UIControl uiCtrl))
                return;

            double deltaX;
            double deltaY;
            Point nowCursorPoint = e.GetPosition(this);
            switch (_dragMode)
            {
                case DragMode.Move:
                    Point newCtrlPos = CalcNewPosition(_dragStartCursorPos, nowCursorPoint, new Point(uiCtrl.X, uiCtrl.Y));
                    deltaX = newCtrlPos.X - uiCtrl.X;
                    deltaY = newCtrlPos.Y - uiCtrl.Y;

                    // UIControl should have position/size of int
                    uiCtrl.X = (int) newCtrlPos.X;
                    uiCtrl.Y = (int) newCtrlPos.Y;

                    UIControlMoved?.Invoke(this, new UIControlDraggedEventArgs(_selectedElement, uiCtrl, deltaX, deltaY));
                    break;
                case DragMode.Resize:
                    Rect newCtrlRect = CalcNewSize(_dragStartCursorPos, nowCursorPoint, uiCtrl.Rect, _selectedClickPos);
                    deltaX = newCtrlRect.X - uiCtrl.X;
                    deltaY = newCtrlRect.Y - uiCtrl.Y;

                    // UIControl should have position/size of int
                    uiCtrl.X = (int) newCtrlRect.X;
                    uiCtrl.Y = (int) newCtrlRect.Y;
                    uiCtrl.Width = (int) newCtrlRect.Width;
                    uiCtrl.Height = (int) newCtrlRect.Height;

                    UIControlResized?.Invoke(this, new UIControlDraggedEventArgs(_selectedElement, uiCtrl, deltaX, deltaY));
                    break;
            }

            ResetMouseCursor();

            _isBeingDragged = false;
            _selectedDragHandle = null;
        }

        public void MoveSelectedUIControl(int deltaX, int deltaY)
        {
            if (!(_selectedElement?.Tag is UIControl uiCtrl))
                return;

            // Update UIControl
            uiCtrl.X += deltaX;
            uiCtrl.Y += deltaY;

            // Updating SelectedElement is should be done by UIRenderer
            UIControlMoved?.Invoke(this, new UIControlDraggedEventArgs(_selectedElement, uiCtrl, deltaX, deltaY, true));
        }

        public void ResizeSelectedUIControl(int deltaX, int deltaY)
        {
            if (!(_selectedElement?.Tag is UIControl uiCtrl))
                return;

            // Update UIControl
            uiCtrl.Width += deltaX;
            uiCtrl.Height += deltaY;

            // Updating SelectedElement is should be done by UIRenderer
            UIControlResized?.Invoke(this, new UIControlDraggedEventArgs(_selectedElement, uiCtrl, deltaX, deltaY, true));
        }
        #endregion

        #region (public) SelectedBorderHandles
        /// <summary>
        /// Clear border and drag handles around selected element
        /// </summary>
        public void ClearSelectedBorderHandles()
        {
            if (_selectedBorder != null)
            {
                UIRenderer.RemoveFromCanvas(this, _selectedBorder);
                _selectedBorder = null;
            }

            foreach (Border dragHandle in _dragHandles)
            {
                UIRenderer.RemoveFromCanvas(this, dragHandle);
            }
            _dragHandles.Clear();
        }

        /// <summary>
        /// Draw border and drag handles around selected element
        /// </summary>
        public void DrawSelectedBorderHandles(UIControl uiCtrl)
        {
            if (uiCtrl == null)
                return;

            foreach (FrameworkElement child in Children)
            {
                if (!(child.Tag is UIControl ctrl))
                    continue;
                if (!ctrl.Key.Equals(uiCtrl.Key, StringComparison.Ordinal))
                    continue;

                _selectedElement = child;
                break;
            }

            DrawSelectedBorderHandles();
        }

        /// <summary>
        /// Draw border and drag handles around selected element
        /// </summary>
        public void DrawSelectedBorderHandles()
        {
            if (_selectedElement == null)
                return;
            if (!(_selectedElement.Tag is UIControl uiCtrl))
                return;

            int z = MaxZIndex;

            // Draw selected border
            _selectedBorder = new Border
            {
                Opacity = 0.75,
                BorderBrush = Brushes.Red,
                BorderThickness = new Thickness(2),
                Focusable = false,
            };

            if (uiCtrl.Type != UIControlType.Bevel)
            {
                SetZIndex(_selectedElement, z + 1);
                SetZIndex(_selectedBorder, z + 2);
            }

            Rect rect = new Rect
            {
                X = GetLeft(_selectedElement),
                Y = GetTop(_selectedElement),
                Width = _selectedElement.Width,
                Height = _selectedElement.Height,
            };
            UIRenderer.DrawToCanvas(this, _selectedBorder, rect);

            // Draw drag handle
            Rect elementRect = new Rect
            {
                X = GetLeft(_selectedElement),
                Y = GetTop(_selectedElement),
                Width = _selectedElement.Width,
                Height = _selectedElement.Height,
            };

            List<ResizeClickPosition> clickPosList = new List<ResizeClickPosition>(9)
            {
                // Only visible if ElementRect.Height is longer than 20px
                ResizeClickPosition.Left,
                ResizeClickPosition.Right,
                // Only visible if ElementRect.Width is longer than 20px
                ResizeClickPosition.Top,
                ResizeClickPosition.Bottom,
                // Always visible
                ResizeClickPosition.LeftTop,
                ResizeClickPosition.RightTop,
                ResizeClickPosition.LeftBottom,
                ResizeClickPosition.RightBottom,
            };

            foreach (ResizeClickPosition clickPos in clickPosList)
            {
                Border dragHandle = new Border
                {
                    BorderThickness = new Thickness(1),
                    Tag = new DragHandleInfo(clickPos, _selectedElement, rect),
                };
                SetDragHandleVisibility(dragHandle, clickPos, elementRect);
                SetZIndex(dragHandle, z + 3);

                _dragHandles.Add(dragHandle);

                Point p = CalcDragHandlePosition(clickPos, elementRect);
                Rect r = new Rect(p.X, p.Y, DragHandleLength, DragHandleLength);
                UIRenderer.DrawToCanvas(this, dragHandle, r);
            }

            // Invoke event handlers
            UIControlSelected?.Invoke(this, new UIControlSelectedEventArgs(_selectedElement, uiCtrl));
        }
        #endregion

        #region (public) Mouse Cursor
        public static void SetMouseCursor(ResizeClickPosition clickPos = ResizeClickPosition.Inside)
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
                case ResizeClickPosition.Inside:
                    newCursor = Cursors.SizeAll;
                    break;
            }

            if (Mouse.OverrideCursor != newCursor)
                Mouse.OverrideCursor = newCursor;
        }

        public static void ResetMouseCursor()
        {
            if (Mouse.OverrideCursor != null)
                Mouse.OverrideCursor = null;
        }
        #endregion

        #region (private) DragHandle Utility
        private void MoveSelectedBorderHandles(Rect newRect)
        {
            Point newPos = new Point(newRect.X, newRect.Y);

            Debug.Assert(_selectedElement != null, "SelectedElement was not set");
            SetElementPosition(_selectedElement, newPos);

            Debug.Assert(_selectedBorder != null, "SelectedBorder was not set");
            SetElementPosition(_selectedBorder, newPos);

            foreach (Border dragHandle in _dragHandles)
            {
                Debug.Assert(dragHandle.Tag.GetType() == typeof(DragHandleInfo));
                DragHandleInfo info = (DragHandleInfo)dragHandle.Tag;

                SetDragHandleVisibility(dragHandle, info.ClickPos, newRect);
                Point p = CalcDragHandlePosition(info.ClickPos, newRect);
                SetElementPosition(dragHandle, p);
            }
        }

        private void ResizeSelectedBorderHandles(Rect newRect)
        {
            Debug.Assert(_selectedElement != null, "SelectedElement was not set");
            Debug.Assert(_selectedBorder != null, "SelectedBorder was not set");

            SetElementSize(_selectedElement, newRect);
            SetElementSize(_selectedBorder, newRect);

            foreach (Border dragHandle in _dragHandles)
            {
                Debug.Assert(dragHandle.Tag.GetType() == typeof(DragHandleInfo));
                DragHandleInfo info = (DragHandleInfo)dragHandle.Tag;

                SetDragHandleVisibility(dragHandle, info.ClickPos, newRect);
                Point p = CalcDragHandlePosition(info.ClickPos, newRect);
                SetElementPosition(dragHandle, p);
            }
        }
        
        private static Point CalcDragHandlePosition(ResizeClickPosition clickPos, Rect elementRect)
        {
            double x = elementRect.X;
            double y = elementRect.Y;
            switch (clickPos)
            {
                case ResizeClickPosition.Left:
                    x -= DragHandleLength;
                    y -= DragHandleLength / 2.0;
                    y += elementRect.Height / 2;
                    break;
                case ResizeClickPosition.Right:
                    x += elementRect.Width;
                    y -= DragHandleLength / 2.0;
                    y += elementRect.Height / 2;
                    break;
                case ResizeClickPosition.Top:
                    x -= DragHandleLength / 2.0;
                    x += elementRect.Width / 2;
                    y -= DragHandleLength;
                    break;
                case ResizeClickPosition.Bottom:
                    x -= DragHandleLength / 2.0;
                    x += elementRect.Width / 2;
                    y += elementRect.Height;
                    break;
                case ResizeClickPosition.LeftTop:
                    x -= DragHandleLength;
                    y -= DragHandleLength;
                    break;
                case ResizeClickPosition.LeftBottom:
                    x -= DragHandleLength;
                    y += elementRect.Height;
                    break;
                case ResizeClickPosition.RightTop:
                    x += elementRect.Width;
                    y -= DragHandleLength;
                    break;
                case ResizeClickPosition.RightBottom:
                    x += elementRect.Width;
                    y += elementRect.Height;
                    break;
                default:
                    throw new ArgumentException(nameof(clickPos));
            }

            return new Point(x, y);
        }

        private static void SetDragHandleVisibility(Border dragHandle, ResizeClickPosition clickPos, Rect elementRect)
        {
            bool visible;
            switch (clickPos)
            {
                // Only visible if ElementRect.Height is longer than 20px
                case ResizeClickPosition.Left:
                case ResizeClickPosition.Right:
                    visible = DragHandleShowThreshold < elementRect.Height;
                    break;
                // Only visible if ElementRect.Width is longer than 20px
                case ResizeClickPosition.Top:
                case ResizeClickPosition.Bottom:
                    visible = DragHandleShowThreshold < elementRect.Width;
                    break;
                // Always visible
                case ResizeClickPosition.LeftTop:
                case ResizeClickPosition.LeftBottom:
                case ResizeClickPosition.RightTop:
                case ResizeClickPosition.RightBottom:
                    visible = true;
                    break;
                default:
                    throw new ArgumentException(nameof(clickPos));
            }

            if (visible)
            {
                dragHandle.Background = new SolidColorBrush(Color.FromRgb(240, 240, 240));
                dragHandle.BorderBrush = Brushes.Black;
                dragHandle.Focusable = true;
            }
            else
            {
                dragHandle.Background = Brushes.Transparent;
                dragHandle.BorderBrush = Brushes.Transparent;
                dragHandle.Focusable = false;
            }
        }
        #endregion

        #region (private) Move/Resize Utility
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

        private static void SetElementPosition(FrameworkElement element, Point p)
        {
            SetLeft(element, p.X);
            SetTop(element, p.Y);
        }

        private static void SetElementSize(FrameworkElement element, Rect rect)
        {
            SetLeft(element, rect.X);
            SetTop(element, rect.Y);
            element.Width = rect.Width;
            element.Height = rect.Height;
        }

        private static Rect GetElementSize(FrameworkElement element)
        {
            return new Rect
            {
                X = GetLeft(element),
                Y = GetTop(element),
                Width = element.Width,
                Height = element.Height,
            };
        }
        #endregion

        #region (private) FrameworkElement Utility
        private FrameworkElement FindRootFrameworkElement(object obj)
        {
            if (obj is DependencyObject dObj)
                return FindRootFrameworkElement(dObj);
            else
                return null;
        }

        private FrameworkElement FindRootFrameworkElement(DependencyObject dObj)
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

        #region class DragHandleInfo
        protected class DragHandleInfo
        {
            public ResizeClickPosition ClickPos;
            public FrameworkElement Parent;
            public Rect ParentRect;

            public DragHandleInfo(ResizeClickPosition clickPos, FrameworkElement parent, Rect parentRect)
            {
                ClickPos = clickPos;
                Parent = parent;
                ParentRect = parentRect;
            }
        }
        #endregion
    }
}
