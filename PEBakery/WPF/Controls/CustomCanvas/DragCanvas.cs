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

            double leftPos = Canvas.GetLeft(_selectedElement);
            double topPos = Canvas.GetTop(_selectedElement);
            _dragStartCursorPos = e.GetPosition(this);
            _dragStartElementPos = new Point(leftPos, topPos);

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
