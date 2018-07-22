using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PEBakery.Core;

namespace PEBakery.WPF.Controls
{
    public class CorrectMenuItem : MenuItem
    {
        public CorrectMenuItem()
        {
            if (!(Icon is FrameworkElement iconElement))
                return;
            if (!(VisualTreeHelper.GetParent(iconElement) is ContentPresenter presenter))
                return;

            const int presenterMargin = 2;
            double maxMargin = presenterMargin;
            Thickness margin = iconElement.Margin;
            if (maxMargin < margin.Top)
                maxMargin = margin.Top;
            else if (maxMargin < margin.Bottom)
                maxMargin = margin.Bottom;
            else if (maxMargin < margin.Left)
                maxMargin = margin.Left;
            else if (maxMargin < margin.Right)
                maxMargin = margin.Right;

            iconElement.Width -= maxMargin;
            iconElement.Height -= maxMargin;
            iconElement.HorizontalAlignment = HorizontalAlignment.Center;
            iconElement.VerticalAlignment = VerticalAlignment.Center;
            presenter.Margin = new Thickness(presenterMargin);
        }
    }
}
