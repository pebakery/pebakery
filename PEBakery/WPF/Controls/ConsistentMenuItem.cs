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
            Loaded += (object sender, RoutedEventArgs e) =>
            {
                if (!(Icon is FrameworkElement iconElement))
                    return;
                if (!(VisualTreeHelper.GetParent(iconElement) is ContentPresenter presenter))
                    return;

                App.Logger.SystemWrite(new LogInfo(LogState.Info, $"IconElement.Margin = {iconElement.Margin}"));
                App.Logger.SystemWrite(new LogInfo(LogState.Info, $"Presenter.Margin   = {presenter.Margin}"));
                iconElement.Width = Height;
                iconElement.Height = Height;
                iconElement.HorizontalAlignment = HorizontalAlignment.Center;
                iconElement.VerticalAlignment = VerticalAlignment.Center;
            };
        }
    }
}
