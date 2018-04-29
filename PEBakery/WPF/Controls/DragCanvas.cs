using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PEBakery.WPF.Controls
{
    public class DragCanvas : Canvas
    {
        #region Fields and Properties
        private Point _dragStartPoint;
        private bool _isBeingDragged;
        private UIElement _selectedElement;
        #endregion
    }
}
