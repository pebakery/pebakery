/*
    MIT License (MIT)

    Copyright (C) 2018-2020 Hajin Jang
	
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
using PEBakery.Helper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;

// ReSharper disable InconsistentNaming
namespace PEBakery.WPF.Controls
{
    #region DragCanvas
    public class DragCanvas : Canvas
    {
        #region Const
        // Length
        public const int CanvasLengthLimit = 700;
        public const int ElementLengthMin = 16;

        // DragHandle
        private const int DragHandleLength = 6;
        private const int DragHandleShowThreshold = 20;
        #endregion

        #region Fields, Properties
        // SelectedElement
        private readonly List<SelectedElement> _selectedElements = new List<SelectedElement>();
        private int _selectedElementIndex = -1;
        private ResizeClickPosition _selectedClickPos;
        /// <summary>
        /// Helper for single move/resize of SelectedElements
        /// </summary>
        private SelectedElement Selected
        {
            get
            {
                if (!(0 <= _selectedElementIndex && _selectedElementIndex < _selectedElements.Count))
                    return null;
                return _selectedElements[_selectedElementIndex];
            }
        }

        // Drag
        private DragMode _dragMode;
        private DragState _dragState;
        private Point _dragStartCursorPos;
        private Rectangle _dragAreaRectangle;

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
        public event UIControlSelectedEventHandler UIControlSelected;

        public event UIControlDraggedEventHandler UIControlMoved;
        public event UIControlDraggedEventHandler UIControlResized;
        #endregion

        #region Mouse Event Handler
        /// <inheritdoc />
        /// <summary>
        /// Clear mouse cursor when mouse is not hovering DragCanvas, such as close of the window.
        /// </summary>
        protected override void OnMouseLeave(MouseEventArgs e)
        {
            // Sometime this is called outside of STA thread
            Dispatcher.Invoke(ResetMouseCursor);
        }

        /// <summary>
        /// Invoke OnPreviewMouseLeftButonDown from outside (e.g. ScriptEditWindow)
        /// </summary>
        /// <param name="e"></param>
        public void TriggerPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            OnPreviewMouseLeftButtonDown(e);
        }

        /// <inheritdoc />
        /// <summary>
        /// Start dragging
        /// </summary>
        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (_dragState != DragState.None)
                return;

            // Capture mouse
            CaptureMouse();

            // Record position and size
            // Always run this code, one for move/resize and one for drag-to-select.
            _dragStartCursorPos = e.GetPosition(this);

            // Which element was selected?
            FrameworkElement focusedElement = FindRootFrameworkElement(e.Source);

            void ResetSelectedElements()
            {
                _dragMode = DragMode.DragToSelect;

                _dragState = DragState.Moving;

                // Do not call UIRenderer.DrawToCanvas here, we don't need to expand canvas here
                _dragAreaRectangle = new Rectangle
                {
                    Fill = new SolidColorBrush(Color.FromArgb(32, 0, 0, 0)),
                };
                Children.Add(_dragAreaRectangle);

                ClearSelectedElements(true);
            }

            // No UIControl was selected
            if (focusedElement == null)
            { // Clicked background -> Reset selected elements
                ResetSelectedElements();
            }
            else if (focusedElement is Border dragHandle && dragHandle.Tag is DragHandleTag info)
            { // Clicked drag handle
                // Resize mode
                _dragMode = 2 <= _selectedElements.Count ? DragMode.MultiResize : DragMode.SingleResize;

                // Record select information
                _dragState = DragState.Moving;
                _selectedClickPos = info.ClickPos;
                _selectedElementIndex = _selectedElements.FindIndex(x => x.Element.Equals(info.Parent));
                Debug.Assert(_selectedElementIndex != -1, "Incorrect SelectedElement handling");

                // Set Cursor
                SetMouseCursor(_selectedClickPos);
            }
            else if (focusedElement.Tag is UIControl uiCtrl)
            { // Clicked UIControl
                // Move mode
                // Multi-select mode handling
                int idx = _selectedElements.FindIndex(x => x.UIControl.Key.Equals(uiCtrl.Key, StringComparison.OrdinalIgnoreCase));
                if (_dragMode != DragMode.MultiMove)
                {
                    //bool multiClick = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control || (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
                    //if (multiClick || (_dragMode == DragMode.SingleMove && idx == -1))
                    if (_dragMode == DragMode.SingleMove && idx == -1)
                    {
                        _dragMode = DragMode.MultiMove;
                    }
                    else
                    {
                        ClearSelectedElements(true);
                        _dragMode = DragMode.SingleMove;
                    }
                }

                // Record select information
                SelectedElement selected = new SelectedElement(focusedElement);
                if (_dragMode == DragMode.SingleMove || _dragMode == DragMode.MultiMove && idx == -1)
                    _selectedElements.Add(selected); // Add to list only if (1) single-select mode or (2) multi-select and the element is not added yet
                _selectedElementIndex = idx == -1 ? _selectedElements.Count - 1 : idx;
                _selectedClickPos = ResizeClickPosition.Inside;
                _dragState = DragState.Moving;

                // Let ScriptEditWindow know about the drag event
                UIControlMoved?.Invoke(this, new UIControlDraggedEventArgs(uiCtrl, _dragStartCursorPos, new Vector(0, 0), false, DragState.Start));

                // Draw border and drag handles
                DrawSelectedElements();

                // Set Cursor
                SetMouseCursor();
            }
            else
            { // Clicked background -> Reset selected elements
                ResetSelectedElements();
            }

            e.Handled = true;
        }

        /// <inheritdoc />
        /// <summary>
        /// In the state of dragging 
        /// </summary>
        /// <param name="e"></param>
        protected override void OnPreviewMouseMove(MouseEventArgs e)
        {
            // Change cursor following underlying element
            if (_dragState != DragState.Moving)
            {
                FrameworkElement hoverElement = FindRootFrameworkElement(e.Source);
                if (hoverElement == null) // Outside
                    ResetMouseCursor();
                else if (hoverElement is Border border && border.Tag is DragHandleTag info)
                    SetMouseCursor(info.ClickPos);
                else if (hoverElement.Tag is UIControl) // Inside
                    SetMouseCursor();
                else // Since the mouse is captured by DragCanvas, the element outside of the canvas can be captured
                    ResetMouseCursor();
                return;
            }

            // Moving/Resizing a UIControl
            Point nowCursorPos = e.GetPosition(this);

            switch (_dragMode)
            {
                case DragMode.DragToSelect:
                    {
                        Debug.Assert(_selectedElements.Count == 0, "Incorrect SelectedElement handling");
                        Rect dragRect = new Rect(_dragStartCursorPos, nowCursorPos);
                        SetElementRect(_dragAreaRectangle, dragRect);
                    }
                    break;
                case DragMode.SingleMove:
                    {
                        Debug.Assert(Selected != null, "SelectedElement is null");
                        Point newElementPos = CalcNewPosition(_dragStartCursorPos, nowCursorPos, Selected.ElementInitialRect);
                        Rect r = new Rect
                        {
                            X = newElementPos.X,
                            Y = newElementPos.Y,
                            Width = Selected.ElementInitialRect.Width,
                            Height = Selected.ElementInitialRect.Height,
                        };
                        MoveSelectedElement(Selected, r);

                        // Send UIControlDraggedEvent
                        UIControl uiCtrl = Selected.UIControl;
                        Vector delta = new Vector(newElementPos.X - uiCtrl.X, newElementPos.Y - uiCtrl.Y);
                        UIControlMoved?.Invoke(this, new UIControlDraggedEventArgs(uiCtrl, _dragStartCursorPos, delta, false, DragState.Moving));
                    }
                    break;
                case DragMode.MultiMove:
                    {
                        Debug.Assert(0 < _selectedElements.Count, "Incorrect SelectedElement handling");
                        Rect[] elementRectList = _selectedElements.Select(se => se.ElementInitialRect).ToArray();
                        (List<Point> newPosList, Vector delta) = CalcNewPositions(_dragStartCursorPos, nowCursorPos, elementRectList);

                        for (int i = 0; i < _selectedElements.Count; i++)
                        {
                            SelectedElement selected = _selectedElements[i];
                            Point newElementPos = newPosList[i];

                            Rect r = new Rect
                            {
                                X = newElementPos.X,
                                Y = newElementPos.Y,
                                Width = selected.ElementInitialRect.Width,
                                Height = selected.ElementInitialRect.Height,
                            };
                            MoveSelectedElement(selected, r);
                        }

                        // Send UIControlDraggedEvent
                        List<UIControl> uiCtrls = _selectedElements.Select(x => x.UIControl).ToList();
                        UIControlMoved?.Invoke(this, new UIControlDraggedEventArgs(uiCtrls, _dragStartCursorPos, delta, false, DragState.Moving));
                    }
                    break;
                case DragMode.SingleResize:
                    {
                        Debug.Assert(Selected != null, "SelectedElement is null");
                        Rect newElementRect = CalcNewSize(_dragStartCursorPos, nowCursorPos, Selected.ElementInitialRect, _selectedClickPos);
                        ResizeSelectedElement(Selected, newElementRect);

                        // Send UIControlDraggedEvent
                        UIControl uiCtrl = Selected.UIControl;
                        Vector delta = CalcResizeDeltas(newElementRect, uiCtrl);
                        UIControlResized?.Invoke(this, new UIControlDraggedEventArgs(uiCtrl, _dragStartCursorPos, delta, false, DragState.Moving));
                    }
                    break;
                case DragMode.MultiResize:
                    {
                        Debug.Assert(0 < _selectedElements.Count, "Incorrect SelectedElement handling");
                        Rect[] elementRectList = _selectedElements.Select(se => se.ElementInitialRect).ToArray();
                        (List<Rect> newRectList, Vector delta) = CalcNewSizes(_dragStartCursorPos, nowCursorPos, elementRectList, _selectedClickPos);
                        for (int i = 0; i < _selectedElements.Count; i++)
                        {
                            SelectedElement selected = _selectedElements[i];
                            Rect r = newRectList[i];
                            ResizeSelectedElement(selected, r);
                        }

                        // Send UIControlDraggedEvent
                        List<UIControl> uiCtrls = _selectedElements.Select(x => x.UIControl).ToList();
                        UIControlResized?.Invoke(this, new UIControlDraggedEventArgs(uiCtrls, _dragStartCursorPos, delta, false, DragState.Moving));
                    }
                    break;
            }
        }

        /// <inheritdoc />
        /// <summary>
        /// End of dragging
        /// </summary>
        /// <param name="e"></param>
        protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            Point nowCursorPos = e.GetPosition(this);
            ReleaseMouseCapture();

            if (_dragState != DragState.Moving)
                return;

            switch (_dragMode)
            {
                case DragMode.DragToSelect:
                    {
                        Debug.Assert(_selectedElements.Count == 0, "Incorrect SelectedElement handling");
                        Children.Remove(_dragAreaRectangle);
                        _dragAreaRectangle = null;

                        // Check if any element was caught by drag-to-select
                        Rect dragRect = new Rect(_dragStartCursorPos, nowCursorPos);
                        foreach (FrameworkElement element in Children)
                        {
                            Rect elementRect = GetElementRect(element);
                            if (dragRect.Contains(elementRect) && element.Tag is UIControl)
                                _selectedElements.Add(new SelectedElement(element));
                        }

                        DrawSelectedElements();
                    }
                    break;
                case DragMode.SingleMove:
                    {
                        Debug.Assert(Selected != null, "SelectedElement was not set");
                        UIControl uiCtrl = Selected.UIControl;

                        Point newCtrlPos = CalcNewPosition(_dragStartCursorPos, nowCursorPos, uiCtrl.Rect);
                        Vector delta = new Vector(newCtrlPos.X - uiCtrl.X, newCtrlPos.Y - uiCtrl.Y);

                        // UIControl should have position/size of int
                        uiCtrl.X = (int)newCtrlPos.X;
                        uiCtrl.Y = (int)newCtrlPos.Y;

                        UIControlMoved?.Invoke(this, new UIControlDraggedEventArgs(uiCtrl, uiCtrl.Point, delta, false, DragState.Finished));
                    }
                    break;
                case DragMode.MultiMove:
                    {
                        Debug.Assert(0 < _selectedElements.Count, "Incorrect SelectedElement handling");
                        List<UIControl> uiCtrls = _selectedElements.Select(x => x.UIControl).ToList();
                        Rect[] uiCtrlRectList = _selectedElements.Select(se => se.UIControl.Rect).ToArray();
                        (List<Point> newPosList, Vector delta) = CalcNewPositions(_dragStartCursorPos, nowCursorPos, uiCtrlRectList);

                        for (int i = 0; i < _selectedElements.Count; i++)
                        {
                            SelectedElement selected = _selectedElements[i];
                            UIControl uiCtrl = selected.UIControl;
                            Point newCtrlPos = newPosList[i];

                            // UIControl should have position/size of int
                            uiCtrl.X = (int)newCtrlPos.X;
                            uiCtrl.Y = (int)newCtrlPos.Y;
                        }

                        UIControlMoved?.Invoke(this, new UIControlDraggedEventArgs(uiCtrls, _dragStartCursorPos, delta, false, DragState.Finished));
                    }
                    break;
                case DragMode.SingleResize:
                    {
                        Debug.Assert(Selected != null, "SelectedElement was not set");
                        UIControl uiCtrl = Selected.UIControl;

                        Rect newCtrlRect = CalcNewSize(_dragStartCursorPos, nowCursorPos, uiCtrl.Rect, _selectedClickPos);
                        Vector delta = CalcResizeDeltas(newCtrlRect, uiCtrl);

                        // UIControl should have position/size of int
                        uiCtrl.X = (int)newCtrlRect.X;
                        uiCtrl.Y = (int)newCtrlRect.Y;
                        uiCtrl.Width = (int)newCtrlRect.Width;
                        uiCtrl.Height = (int)newCtrlRect.Height;

                        UIControlResized?.Invoke(this, new UIControlDraggedEventArgs(uiCtrl, _dragStartCursorPos, delta, false, DragState.Finished));
                    }
                    break;
                case DragMode.MultiResize:
                    {
                        Debug.Assert(0 < _selectedElements.Count, "Incorrect SelectedElement handling");
                        List<UIControl> uiCtrls = _selectedElements.Select(x => x.UIControl).ToList();
                        Rect[] uiCtrlRectList = _selectedElements.Select(se => se.UIControl.Rect).ToArray();
                        (List<Rect> newRectList, Vector delta) = CalcNewSizes(_dragStartCursorPos, nowCursorPos, uiCtrlRectList, _selectedClickPos);

                        for (int i = 0; i < _selectedElements.Count; i++)
                        {
                            SelectedElement selected = _selectedElements[i];
                            UIControl uiCtrl = selected.UIControl;
                            Rect newCtrlRect = newRectList[i];

                            // UIControl should have position/size of int
                            uiCtrl.X = (int)newCtrlRect.X;
                            uiCtrl.Y = (int)newCtrlRect.Y;
                            uiCtrl.Width = (int)newCtrlRect.Width;
                            uiCtrl.Height = (int)newCtrlRect.Height;
                        }

                        UIControlResized?.Invoke(this, new UIControlDraggedEventArgs(uiCtrls, _dragStartCursorPos, delta, false, DragState.Finished));
                    }
                    break;
            }

            ResetMouseCursor();

            _dragState = DragState.None;
        }
        #endregion

        #region (public) SelectedElements
        /// <summary>
        /// Clear border and drag handles around selected element
        /// </summary>
        public void ClearSelectedElements(bool clearList)
        {
            foreach (SelectedElement selected in _selectedElements)
            {
                if (selected.Border != null)
                    UIRenderer.RemoveFromCanvas(this, selected.Border);
                foreach (Border dragHandle in selected.DragHandles)
                    UIRenderer.RemoveFromCanvas(this, dragHandle);
            }

            if (clearList)
                _selectedElements.Clear();
        }

        /// <summary>
        /// Draw border and drag handles around selected elements
        /// </summary>
        public void DrawSelectedElements()
        {
            ClearSelectedElements(false);

            if (1 < _selectedElements.Count)
            {
                List<UIControl> uiCtrls = new List<UIControl>(_selectedElements.Count);
                foreach (SelectedElement selected in _selectedElements)
                {
                    uiCtrls.Add(selected.UIControl);
                    DrawSelectedElement(selected, true);
                }

                UIControlSelected?.Invoke(this, new UIControlSelectedEventArgs(uiCtrls));
            }
            else if (_selectedElements.Count == 1)
            {
                SelectedElement selected = _selectedElements[0];
                DrawSelectedElement(selected, false);

                UIControlSelected?.Invoke(this, new UIControlSelectedEventArgs(selected.UIControl));
            }
        }

        /// <summary>
        /// Draw border and drag handles around a selected element
        /// </summary>
        private void DrawSelectedElement(SelectedElement selected, bool multiSelect)
        {
            UIControl uiCtrl = selected.Element.Tag as UIControl;
            Debug.Assert(uiCtrl != null, "Incorrect SelectedElement handling");

            int z = MaxZIndex;
            Rect elementRect = GetElementRect(selected.Element);

            // Draw selected border
            selected.Border = new Border
            {
                Opacity = 0.75,
                BorderBrush = multiSelect ? Brushes.Blue : Brushes.Red,
                BorderThickness = new Thickness(2),
                Focusable = false,
            };

            if (uiCtrl.Type != UIControlType.Bevel)
            {
                SetZIndex(selected.Element, z + 1);
                SetZIndex(selected.Border, z + 2);
            }

            UIRenderer.DrawToCanvas(this, selected.Border, elementRect);

            // Draw drag handle
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
                    Tag = new DragHandleTag(clickPos, selected.Element, elementRect),
                };
                SetDragHandleVisibility(dragHandle, clickPos, elementRect);
                SetZIndex(dragHandle, z + 3);

                selected.DragHandles.Add(dragHandle);

                Point p = CalcDragHandlePosition(clickPos, elementRect);
                Rect r = new Rect(p.X, p.Y, DragHandleLength, DragHandleLength);
                UIRenderer.DrawToCanvas(this, dragHandle, r);
            }
        }

        /// <summary>
        /// Draw border and drag handles around selected element, from outside
        /// </summary>
        public void DrawSelectedElement(UIControl uiCtrl)
        {
            if (uiCtrl == null)
                return;

            foreach (FrameworkElement child in Children)
            {
                if (!(child.Tag is UIControl ctrl))
                    continue;
                if (!ctrl.Key.Equals(uiCtrl.Key, StringComparison.Ordinal))
                    continue;

                _selectedElements.Clear();
                _selectedElements.Add(new SelectedElement(child));
                break;
            }

            DrawSelectedElements();
        }

        /// <summary>
        /// Draw border and drag handles around selected element, from outside
        /// </summary>
        public void DrawSelectedElements(List<UIControl> uiCtrls)
        {
            if (uiCtrls == null)
                return;

            _selectedElements.Clear();
            foreach (UIControl uiCtrl in uiCtrls)
            {
                foreach (FrameworkElement child in Children)
                {
                    if (!(child.Tag is UIControl ctrl))
                        continue;
                    if (!ctrl.Key.Equals(uiCtrl.Key, StringComparison.Ordinal))
                        continue;

                    _selectedElements.Add(new SelectedElement(child));
                    break;
                }
            }

            DrawSelectedElements();
        }

        private static void MoveSelectedElement(SelectedElement selected, Rect newRect)
        {
            // Assertion
            Debug.Assert(selected.Element != null, "Incorrect SelectedElement handling");
            Debug.Assert(selected.Border != null, "Incorrect SelectedElement handling");

            // Move element and border
            Point newPos = new Point(newRect.X, newRect.Y);
            SetElementPosition(selected.Element, newPos);
            SetElementPosition(selected.Border, newPos);

            // Move drag handles
            foreach (Border dragHandle in selected.DragHandles)
            {
                Debug.Assert(dragHandle.Tag.GetType() == typeof(DragHandleTag), "Incorrect SelectedElement handling");
                DragHandleTag info = (DragHandleTag)dragHandle.Tag;

                SetDragHandleVisibility(dragHandle, info.ClickPos, newRect);
                Point p = CalcDragHandlePosition(info.ClickPos, newRect);
                SetElementPosition(dragHandle, p);
            }
        }

        private static void ResizeSelectedElement(SelectedElement selected, Rect newRect)
        {
            // Assertion
            Debug.Assert(selected.Element != null, "Incorrect SelectedElement handling");
            Debug.Assert(selected.Border != null, "Incorrect SelectedElement handling");

            // Resize element and border
            SetElementRect(selected.Element, newRect);
            SetElementRect(selected.Border, newRect);

            // Resize drag handles
            foreach (Border dragHandle in selected.DragHandles)
            {
                Debug.Assert(dragHandle.Tag.GetType() == typeof(DragHandleTag));
                DragHandleTag info = (DragHandleTag)dragHandle.Tag;

                SetDragHandleVisibility(dragHandle, info.ClickPos, newRect);
                Point p = CalcDragHandlePosition(info.ClickPos, newRect);
                SetElementPosition(dragHandle, p);
            }
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

        #region (private) Move Utility
        private static Point CalcNewPosition(Point cursorStart, Point cursorNow, Rect elementStart)
        {
            double x = elementStart.X + cursorNow.X - cursorStart.X;
            double y = elementStart.Y + cursorNow.Y - cursorStart.Y;

            // Do not use Width and Height here, or canvas cannot be expanded
            // Guard new X and Y in 0 ~ 600
            if (x < 0)
                x = 0;
            else if (CanvasLengthLimit < x + elementStart.Width)
                x = CanvasLengthLimit - elementStart.Width;
            if (y < 0)
                y = 0;
            else if (CanvasLengthLimit < y + elementStart.Height)
                y = CanvasLengthLimit - elementStart.Height;

            return new Point(x, y);
        }

        private static (List<Point> NewPosList, Vector CaliDelta) CalcNewPositions(Point cursorStart, Point cursorNow, IReadOnlyList<Rect> elementStartList)
        {
            double deltaX = cursorNow.X - cursorStart.X;
            double deltaY = cursorNow.Y - cursorStart.Y;
            double caliDeltaX = deltaX;
            double caliDeltaY = deltaY;

            // Calibrate deltaX, deltaY
            foreach (Rect elementStart in elementStartList)
            {
                double x = elementStart.X + deltaX;
                double y = elementStart.Y + deltaY;

                // Do not use Width and Height here, or canvas cannot be expanded
                // Guard new X and Y in 0 ~ 600
                if (x < 0)
                    caliDeltaX = Math.Max(caliDeltaX, deltaX - x);
                else if (CanvasLengthLimit < x + elementStart.Width)
                    caliDeltaX = Math.Min(caliDeltaX, deltaX + CanvasLengthLimit - (x + elementStart.Width));

                if (y < 0)
                    caliDeltaY = Math.Max(caliDeltaY, deltaY - y);
                else if (CanvasLengthLimit < y + elementStart.Height)
                    caliDeltaY = Math.Min(caliDeltaY, deltaY + CanvasLengthLimit - (y + elementStart.Height));
            }

            // Apply calibrated deltaX, deltaY
            List<Point> newPosList = new List<Point>(elementStartList.Count);
            foreach (Rect elementStart in elementStartList)
            {
                double x = elementStart.X + caliDeltaX;
                double y = elementStart.Y + caliDeltaY;

                // double always have subtle rounding error, so guard again
                if (x < 0)
                    x = 0;
                else if (CanvasLengthLimit < x + elementStart.Width)
                    x = CanvasLengthLimit - elementStart.Width;

                if (y < 0)
                    y = 0;
                else if (CanvasLengthLimit < y + elementStart.Height)
                    y = CanvasLengthLimit - elementStart.Height;

                newPosList.Add(new Point(x, y));
            }

            return (newPosList, new Vector(caliDeltaX, caliDeltaY));
        }

        private static void SetElementPosition(FrameworkElement element, Point p)
        {
            SetLeft(element, p.X);
            SetTop(element, p.Y);
        }
        #endregion

        #region (private) Resize Utility
        private static (Rect NewRect, double CaliDeltaX, double CaliDeltaY) InternalCalcNewSize(double deltaX, double deltaY, Rect elementRect, ResizeClickPosition clickPos)
        {
            // Do not touch Width and Height if border was not clicked
            switch (clickPos)
            {
                case ResizeClickPosition.Inside:
                case ResizeClickPosition.Outside:
                    return (elementRect, deltaX, deltaY);
            }

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
                    if (NumberHelper.DoubleEquals(deltaX, 0))
                        break;
                    if (0 < deltaX)
                    { // L [->    ] R, delta is positive
                        if (deltaX + ElementLengthMin < width)
                        {
                            x += deltaX;
                            width -= deltaX;
                        }
                        else
                        { // Guard
                            x += width - ElementLengthMin;
                            width = ElementLengthMin;
                        }
                    }
                    else
                    { // L <-[    ] R, delta is negative
                        x += deltaX;
                        width -= deltaX;
                    }
                    break;
                case ResizeClickPosition.Right:
                case ResizeClickPosition.RightTop:
                case ResizeClickPosition.RightBottom:
                    if (NumberHelper.DoubleEquals(deltaX, 0))
                        break;
                    if (0 < deltaX)
                    { // L [    ]-> R, delta is positive
                        width += deltaX;
                    }
                    else
                    { // L [    <-] R, delta is negative
                        if (ElementLengthMin < width + deltaX) // Guard
                            width += deltaX;
                        else
                            width = ElementLengthMin;
                    }
                    break;
            }

            // Y Direction
            switch (clickPos)
            {
                case ResizeClickPosition.Top:
                case ResizeClickPosition.LeftTop:
                case ResizeClickPosition.RightTop:
                    if (NumberHelper.DoubleEquals(deltaY, 0))
                        break;
                    if (0 < deltaY)
                    { // T [->    ] B, delta is positive
                        if (deltaY + ElementLengthMin < height)
                        {
                            y += deltaY;
                            height -= deltaY;
                        }
                        else
                        { // Guard
                            y += height - ElementLengthMin;
                            height = ElementLengthMin;
                        }
                    }
                    else
                    { // T <-[    ] B, delta is negative
                        y += deltaY;
                        height -= deltaY;
                    }
                    break;
                case ResizeClickPosition.Bottom:
                case ResizeClickPosition.LeftBottom:
                case ResizeClickPosition.RightBottom:
                    if (NumberHelper.DoubleEquals(deltaY, 0))
                        break;
                    if (0 < deltaY)
                    { // T [    ]-> B, delta is positive
                        height += deltaY;
                    }
                    else
                    { // T [    <-] B, delta is negative
                        if (ElementLengthMin < height + deltaY) // Guard
                            height += deltaY;
                        else
                            height = ElementLengthMin;
                    }
                    break;
            }

            // Check if X and Width is in 0 - 600 range
            switch (clickPos)
            {
                case ResizeClickPosition.Left:
                case ResizeClickPosition.LeftTop:
                case ResizeClickPosition.LeftBottom:
                    if (x < 0)
                    {
                        deltaX -= x;

                        width += x;
                        x = 0;
                    }
                    break;
                case ResizeClickPosition.Right:
                case ResizeClickPosition.RightTop:
                case ResizeClickPosition.RightBottom:
                    if (CanvasLengthLimit < x + width)
                    {
                        deltaX += CanvasLengthLimit - (x + width);

                        width = CanvasLengthLimit - x;
                    }
                    break;
            }

            // Check if Y and Height is in 0 - 600 range
            switch (clickPos)
            {
                case ResizeClickPosition.Top:
                case ResizeClickPosition.LeftTop:
                case ResizeClickPosition.RightTop:
                    if (y < 0)
                    {
                        deltaY -= y;

                        height += y;
                        y = 0;
                    }
                    break;
                case ResizeClickPosition.Bottom:
                case ResizeClickPosition.LeftBottom:
                case ResizeClickPosition.RightBottom:
                    if (CanvasLengthLimit < y + height)
                    {
                        deltaY += CanvasLengthLimit - (y + height);

                        height = CanvasLengthLimit - y;
                    }
                    break;
            }

            return (new Rect(x, y, width, height), deltaX, deltaY);
        }

        private static Rect CalcNewSize(Point cursorStart, Point cursorNow, Rect elementRect, ResizeClickPosition clickPos)
        {
            // Get delta of X and Y
            double deltaX = cursorNow.X - cursorStart.X;
            double deltaY = cursorNow.Y - cursorStart.Y;

            (Rect newRect, _, _) = InternalCalcNewSize(deltaX, deltaY, elementRect, clickPos);
            return newRect;
        }

        private static (List<Rect> NewRects, Vector DeltaX) CalcNewSizes(Point cursorStart, Point cursorNow, IReadOnlyList<Rect> elementRects, ResizeClickPosition clickPos)
        {
            // Get delta of X and Y
            double deltaX = cursorNow.X - cursorStart.X;
            double deltaY = cursorNow.Y - cursorStart.Y;
            double caliDeltaX = deltaX;
            double caliDeltaY = deltaY;

            foreach (Rect elementRect in elementRects)
            {
                (_, double newDeltaX, double newDeltaY) = InternalCalcNewSize(deltaX, deltaY, elementRect, clickPos);

                // Calibrate deltaX
                switch (clickPos)
                {
                    // L <-[    ] R, deltaX is negative
                    case ResizeClickPosition.Left:
                    case ResizeClickPosition.LeftTop:
                    case ResizeClickPosition.LeftBottom:
                        caliDeltaX = Math.Max(caliDeltaX, newDeltaX);
                        break;
                    // L [    ]-> R, deltaX is positive
                    case ResizeClickPosition.Right:
                    case ResizeClickPosition.RightTop:
                    case ResizeClickPosition.RightBottom:
                        caliDeltaX = Math.Min(caliDeltaX, newDeltaX);
                        break;
                }

                // Calibrate deltaY
                switch (clickPos)
                {
                    // T <-[    ] B, deltaY is negative
                    case ResizeClickPosition.Top:
                    case ResizeClickPosition.LeftTop:
                    case ResizeClickPosition.RightTop:
                        caliDeltaY = Math.Max(caliDeltaY, newDeltaY);
                        break;
                    // T [    ]-> B, deltaY is positive
                    case ResizeClickPosition.Bottom:
                    case ResizeClickPosition.LeftBottom:
                    case ResizeClickPosition.RightBottom:
                        caliDeltaY = Math.Min(caliDeltaY, newDeltaY);
                        break;
                }
            }

            List<Rect> newRects = new List<Rect>(elementRects.Count);
            foreach (Rect elementRect in elementRects)
            {
                Rect newRect;
                (newRect, _, _) = InternalCalcNewSize(caliDeltaX, caliDeltaY, elementRect, clickPos);
                newRects.Add(newRect);
            }

            return (newRects, new Vector(caliDeltaX, caliDeltaY));
        }

        private static Vector CalcResizeDeltas(Rect newCtrlRect, UIControl uiCtrl)
        {
            double deltaX = Math.Max(Math.Abs(newCtrlRect.X - uiCtrl.X), Math.Abs(newCtrlRect.Width - uiCtrl.Width));
            double deltaY = Math.Max(Math.Abs(newCtrlRect.Y - uiCtrl.Y), Math.Abs(newCtrlRect.Height - uiCtrl.Height));
            return new Vector(deltaX, deltaY);
        }

        public static void SetElementRect(FrameworkElement element, Rect rect)
        {
            SetLeft(element, rect.X);
            SetTop(element, rect.Y);
            element.Width = rect.Width;
            element.Height = rect.Height;
        }

        public static Rect GetElementRect(FrameworkElement element)
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

        #region (public) Apply to UIControl
        public static void ApplyUIControlPosition(UIControl uiCtrl, int deltaX, int deltaY)
        {
            int x = uiCtrl.X + deltaX;
            int y = uiCtrl.Y + deltaY;

            // Do not use Width and Height here, or canvas cannot be expanded
            // Guard new X and Y in 0 ~ 600
            if (x < 0)
                x = 0;
            else if (CanvasLengthLimit < x)
                x = CanvasLengthLimit;
            if (y < 0)
                y = 0;
            else if (CanvasLengthLimit < y)
                y = CanvasLengthLimit;

            uiCtrl.X = x;
            uiCtrl.Y = y;
        }

        public static void ApplyUIControlPositions(List<UIControl> uiCtrls, int deltaX, int deltaY)
        {
            int caliDeltaX = deltaX;
            int caliDeltaY = deltaY;

            // Calibrate deltaX, deltaY
            foreach (UIControl uiCtrl in uiCtrls)
            {
                int x = uiCtrl.X + deltaX;
                int y = uiCtrl.Y + deltaY;

                // Do not use Width and Height here, or canvas cannot be expanded
                // Guard new X and Y in 0 ~ 600
                if (x < 0)
                    caliDeltaX = Math.Max(caliDeltaX, deltaX - x);
                else if (CanvasLengthLimit < x)
                    caliDeltaX = Math.Min(caliDeltaX, deltaX + CanvasLengthLimit - x);

                if (y < 0)
                    caliDeltaY = Math.Max(caliDeltaY, deltaY - y);
                else if (CanvasLengthLimit < y)
                    caliDeltaY = Math.Min(caliDeltaY, deltaY + CanvasLengthLimit - y);
            }

            // Apply calibrated deltaX, deltaY
            foreach (UIControl uiCtrl in uiCtrls)
            {
                uiCtrl.X += caliDeltaX;
                uiCtrl.Y += caliDeltaY;
            }
        }

        public static void ApplyUIControlSize(UIControl uiCtrl, int deltaX, int deltaY)
        {
            int width = uiCtrl.Width + deltaX;
            int height = uiCtrl.Height + deltaY;

            if (width < ElementLengthMin)
                width = ElementLengthMin;
            else if (CanvasLengthLimit < uiCtrl.X + width)
                width = CanvasLengthLimit - uiCtrl.X;
            if (height < ElementLengthMin)
                height = ElementLengthMin;
            else if (CanvasLengthLimit < uiCtrl.Y + height)
                height = CanvasLengthLimit - uiCtrl.Y;

            uiCtrl.Width = width;
            uiCtrl.Height = height;
        }

        public static void ApplyUIControlSizes(List<UIControl> uiCtrls, int deltaX, int deltaY)
        {
            int caliDeltaX = deltaX;
            int caliDeltaY = deltaY;

            // Check deltaX, deltaY
            foreach (UIControl uiCtrl in uiCtrls)
            {
                if (uiCtrl.Width + deltaX < ElementLengthMin)
                    caliDeltaX = Math.Max(caliDeltaX, ElementLengthMin - uiCtrl.Width);
                else if (CanvasLengthLimit < uiCtrl.X + uiCtrl.Width + deltaX)
                    caliDeltaX = Math.Min(caliDeltaX, CanvasLengthLimit - (uiCtrl.X + uiCtrl.Width));
                if (uiCtrl.Height + deltaY < ElementLengthMin)
                    caliDeltaY = Math.Max(caliDeltaY, ElementLengthMin - uiCtrl.Height);
                else if (CanvasLengthLimit < uiCtrl.Y + uiCtrl.Height + deltaY)
                    caliDeltaY = Math.Min(caliDeltaY, CanvasLengthLimit - (uiCtrl.Y + uiCtrl.Height));
            }

            // Apply calibrated deltaX, deltaY
            foreach (UIControl uiCtrl in uiCtrls)
            {
                uiCtrl.Width += caliDeltaX;
                uiCtrl.Height += caliDeltaY;
            }
        }
        #endregion

        #region (private) FrameworkElement Utility
        public FrameworkElement FindRootFrameworkElement(object obj)
        {
            if (obj is DependencyObject dObj)
                return FindRootFrameworkElement(dObj);
            else
                return null;
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
    #endregion

    #region enum DragMode, ResizeClickPosition
    public enum DragMode
    {
        DragToSelect,
        SingleMove,
        MultiMove,
        SingleResize,
        MultiResize,
    }

    public enum DragState
    {
        /// <summary>
        /// Not dragging, for use in DragCanvas
        /// </summary>
        None = 0,
        /// <summary>
        /// Started dragging, for use in DraggedEventArgs
        /// </summary>
        Start,
        /// <summary>
        /// In the middle of dragging, for use in DragCanvas and DraggedEventArgs
        /// </summary>
        Moving,
        /// <summary>
        /// Stopped dragging, for use in DraggedEventArgs
        /// </summary>
        Finished,
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
    #endregion

    #region class Events
    public class UIControlSelectedEventArgs : EventArgs
    {
        /// <summary>
        /// (Single select) Selected an UIControl
        /// </summary>
        public UIControl UIControl { get; set; }
        /// <summary>
        /// (Multi select) Selected multiple UIControls
        /// </summary>
        public List<UIControl> UIControls { get; set; }
        /// <summary>
        /// Selected multiple UIControls
        /// </summary>
        public bool MultiSelect => UIControls != null;

        public UIControlSelectedEventArgs()
        { 
        }

        public UIControlSelectedEventArgs(UIControl uiCtrl)
        {
            UIControl = uiCtrl;
        }

        public UIControlSelectedEventArgs(List<UIControl> uiCtrls)
        {
            UIControls = uiCtrls;
        }
    }
    public delegate void UIControlSelectedEventHandler(object sender, UIControlSelectedEventArgs e);

    public class UIControlDraggedEventArgs : EventArgs
    {
        /// <summary>
        /// (Single select) Selected an UIControl
        /// </summary>
        public UIControl UIControl { get; set; }
        /// <summary>
        /// (Multi select) Selected multiple UIControls
        /// </summary>
        public List<UIControl> UIControls { get; set; }
        /// <summary>
        /// Selected multiple UIControls
        /// </summary>
        public bool MultiSelect => UIControls != null;
        /// <summary>
        /// Original coord/width 
        /// </summary>
        public Point Origin { get; set; }
        /// <summary>
        /// Delta coord/width
        /// </summary>
        public Vector Delta { get; set; }
        /// <summary>
        /// Force to re-render UIControl regardless of delta coord.
        /// </summary>
        public bool ForceUpdate { get; set; }
        /// <summary>
        /// Is dragging finished?
        /// </summary>
        public DragState DragState { get; set; }

        public UIControlDraggedEventArgs(UIControl uiCtrl, Point origin, Vector delta, bool forceUpdate, DragState dragState)
        {
            UIControl = uiCtrl;
            UIControls = null;
            Origin = origin;
            Delta = delta;
            ForceUpdate = forceUpdate;
            DragState = dragState;
        }

        public UIControlDraggedEventArgs(List<UIControl> uiCtrls, Point origin, Vector delta, bool forceUpdate, DragState dragState)
        {
            UIControl = null;
            UIControls = uiCtrls;
            Origin = origin;
            Delta = delta;
            ForceUpdate = forceUpdate;
            DragState = dragState;
        }
    }
    public delegate void UIControlDraggedEventHandler(object sender, UIControlDraggedEventArgs e);
    #endregion

    #region class DragHandleTag
    public class DragHandleTag
    {
        public ResizeClickPosition ClickPos;
        public FrameworkElement Parent;
        public Rect ParentRect;

        public DragHandleTag(ResizeClickPosition clickPos, FrameworkElement parent, Rect parentRect)
        {
            ClickPos = clickPos;
            Parent = parent;
            ParentRect = parentRect;
        }
    }
    #endregion

    #region class SelectedElement
    public class SelectedElement
    {
        public FrameworkElement Element;
        public UIControl UIControl => Element.Tag as UIControl;
        public Rect ElementInitialRect;
        public Border Border;
        public readonly List<Border> DragHandles = new List<Border>();

        public SelectedElement(FrameworkElement element)
        {
            Debug.Assert(element.Tag.GetType() == typeof(UIControl), "Incorrect Element.Tag");
            Element = element;
            ElementInitialRect = DragCanvas.GetElementRect(element);
        }
    }
    #endregion
}
