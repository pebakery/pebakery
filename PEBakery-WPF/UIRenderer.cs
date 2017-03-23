using PEBakery.Core;
using PEBakery.Lib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PEBakery.WPF
{
    public class UIRenderer
    {
        public const double PixelScale = 1.5; // WB082's coord seems too small in WPF's canvas.
        public const int DefaultFontSize = 8; // WB082 hard-coded default font size to 8.

        private Canvas canvas;
        private Plugin plugin;
        private List<UICommand> uiCodes;

        public UIRenderer(Canvas canvas, Plugin plugin)
        {
            this.canvas = canvas;
            this.plugin = plugin;
            if (plugin.Sections.ContainsKey("Interface"))
                this.uiCodes = plugin.Sections["Interface"].GetUICodes(true);
            else
                this.uiCodes = null;
        }

        #region Render All
        public void Render()
        {
            if (uiCodes == null) // This plugin does not have 'Interface' section
                return; 

            foreach (UICommand uiCmd in uiCodes)
            {
                if (uiCmd.Visibility == false || uiCmd.Info.Valid == false)
                    continue;

                bool failed = false;
                switch (uiCmd.Type)
                {
                    case UIControlType.TextBox:
                        failed = UIRenderer.RenderTextBox(canvas, uiCmd);
                        break;
                    case UIControlType.TextLabel:
                        failed = UIRenderer.RenderTextLabel(canvas, uiCmd);
                        break;
                    case UIControlType.NumberBox:
                        break;
                    case UIControlType.CheckBox:
                        failed = UIRenderer.RenderCheckBox(canvas, uiCmd);
                        break;
                    case UIControlType.ComboBox:
                        break;
                    case UIControlType.Image:
                        break;
                    case UIControlType.TextFile:
                        break;
                    case UIControlType.Button:
                        break;
                    case UIControlType.CheckList:
                        break;
                    case UIControlType.WebLabel:
                        failed = UIRenderer.RenderWebLabel(canvas, uiCmd);
                        break;
                    case UIControlType.RadioButton:
                        break;
                    case UIControlType.Bevel:
                        failed = UIRenderer.RenderBevel(canvas, uiCmd);
                        break;
                    case UIControlType.FileBox:
                        break;
                    case UIControlType.RadioGroup:
                        break;
                    default:
                        break;
                }

                if (failed)
                    continue;
            }

            return;
        }
        #endregion

        #region Render Each Control
        /// <summary>
        /// Render TextBox control.
        /// Return true if failed.
        /// </summary>
        /// <param name="canvas">Parent canvas</param>
        /// <param name="uiCmd">UICommand</param>
        /// <returns>Success = false, Failure = true</returns>
        public static bool RenderTextBox(Canvas canvas, UICommand uiCmd)
        {
            UIInfo_TextBox info = uiCmd.Info as UIInfo_TextBox;
            if (info == null)
                return true;

            TextBlock block = new TextBlock()
            {
                Text = uiCmd.Text,
                FontSize = DefaultFontSize * PixelScale,
            };
            TextBox box = new TextBox()
            {
                Text = info.Value,
                FontSize = DefaultFontSize * PixelScale,
            };

            box.LostFocus += (object sender, RoutedEventArgs e) => {
                TextBox tBox = sender as TextBox;
                info.Value = tBox.Text;
                UIRenderer.UpdatePlugin(uiCmd);
            };

            Grid grid = new Grid()
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            grid.RowDefinitions.Add(new RowDefinition());
            grid.RowDefinitions.Add(new RowDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            Grid.SetRow(block, 0);
            Grid.SetColumn(block, 0);
            grid.Children.Add(block);
            Grid.SetRow(box, 1);
            Grid.SetColumn(box, 0);
            grid.Children.Add(box);

            SetToolTip(grid, info.ToolTip);
            DrawToCanvas(canvas, grid, uiCmd.Rect);
            return false;
        }

        /// <summary>
        /// Render TextLabel control.
        /// Return true if failed.
        /// </summary>
        /// <param name="canvas">Parent canvas</param>
        /// <param name="uiCmd">UICommand</param>
        /// <returns>Success = false, Failure = true</returns>
        public static bool RenderTextLabel(Canvas canvas, UICommand uiCmd)
        {
            UIInfo_TextLabel info = uiCmd.Info as UIInfo_TextLabel;
            if (info == null)
                return true;

            TextBlock block = new TextBlock()
            {
                Text = uiCmd.Text,
                TextWrapping = TextWrapping.Wrap,
                FontSize = info.FontSize * PixelScale,
            };

            switch (info.Style)
            {
                case UIInfo_TextLabel_Style.Bold:
                    block.FontWeight = FontWeights.Bold;
                    break;
                case UIInfo_TextLabel_Style.Italic:
                    block.FontStyle = FontStyles.Italic;
                    break;
                case UIInfo_TextLabel_Style.Underline:
                    block.TextDecorations = TextDecorations.Underline;
                    break;
                case UIInfo_TextLabel_Style.Strike:
                    block.TextDecorations = TextDecorations.Strikethrough;
                    break;
            }

            SetToolTip(block, info.ToolTip);
            DrawToCanvas(canvas, block, uiCmd.Rect);
            return false;
        }

        /// <summary>
        /// Render CheckBox control.
        /// Return true if failed.
        /// </summary>
        /// <param name="canvas">Parent canvas</param>
        /// <param name="uiCmd">UICommand</param>
        /// <returns>Success = false, Failure = true</returns>
        public static bool RenderCheckBox(Canvas canvas, UICommand uiCmd)
        {
            UIInfo_CheckBox info = uiCmd.Info as UIInfo_CheckBox;
            if (info == null)
                return true;

            CheckBox checkBox = new CheckBox()
            {
                Content = uiCmd.Text,
                IsChecked = info.Value,
                FontSize = DefaultFontSize * PixelScale,
            };
            checkBox.Checked += (object sender, RoutedEventArgs e) => {
                CheckBox box = sender as CheckBox;
                info.Value = true;
                UIRenderer.UpdatePlugin(uiCmd);
            };
            checkBox.Unchecked += (object sender, RoutedEventArgs e) => {
                CheckBox box = sender as CheckBox;
                info.Value = false;
                UIRenderer.UpdatePlugin(uiCmd);
            };

            SetToolTip(checkBox, info.ToolTip);
            DrawToCanvas(canvas, checkBox, uiCmd.Rect);
            return false;
        }

        /// <summary>
        /// Render WebLabel control.
        /// Return true if failed.
        /// </summary>
        /// <param name="canvas">Parent canvas</param>
        /// <param name="uiCmd">UICommand</param>
        /// <returns>Success = false, Failure = true</returns>
        public static bool RenderWebLabel(Canvas canvas, UICommand uiCmd)
        {
            UIInfo_WebLabel info = uiCmd.Info as UIInfo_WebLabel;
            if (info == null)
                return true;

            TextBlock block = new TextBlock()
            {
                TextWrapping = TextWrapping.Wrap,
                FontSize = DefaultFontSize * PixelScale,
            };
            Hyperlink hyperLink = new Hyperlink()
            {
                NavigateUri = new Uri(info.URL),
            };
            hyperLink.Inlines.Add(uiCmd.Text);
            hyperLink.RequestNavigate += (object sender, RequestNavigateEventArgs e) => {
                System.Diagnostics.Process.Start(e.Uri.ToString());
            };
            block.Inlines.Add(hyperLink);

            SetToolTip(block, info.ToolTip);
            DrawToCanvas(canvas, block, uiCmd.Rect);
            return false;
        }

        /// <summary>
        /// Render Bevel control.
        /// Return true if failed.
        /// </summary>
        /// <param name="canvas">Parent canvas</param>
        /// <param name="uiCmd">UICommand</param>
        /// <returns>Success = false, Failure = true</returns>
        public static bool RenderBevel(Canvas canvas, UICommand uiCmd)
        {
            UIInfo_Bevel info = uiCmd.Info as UIInfo_Bevel;
            if (info == null)
                return true;

            Border bevel = new Border()
            {
                IsHitTestVisible = false,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(2),
                BorderBrush = Brushes.Gray,
                ToolTip = info.ToolTip,
            };

            DrawToCanvas(canvas, bevel, uiCmd.Rect);
            return false;
        }
        #endregion

        #region Utility
        private static void SetToolTip(FrameworkElement element, string toolTip)
        {
            if (toolTip != null)
                element.ToolTip = toolTip;
        }

        private static void DrawToCanvas(Canvas canvas, FrameworkElement element, Rect coord)
        {
            Canvas.SetLeft(element, coord.Left * PixelScale);
            Canvas.SetTop(element, coord.Top * PixelScale);
            element.Width = coord.Width * PixelScale;
            element.Height = coord.Height * PixelScale;
            canvas.Children.Add(element);
        }

        private static void UpdatePlugin(UICommand uiCmd)
        {
            Ini.SetKey(uiCmd.Addr.Plugin.FullPath, new IniKey("Interface", uiCmd.Key, uiCmd.ForgeRawLine(false)));
        }
        #endregion
    }   
}
