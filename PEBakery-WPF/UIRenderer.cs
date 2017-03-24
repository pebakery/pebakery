using PEBakery.Core;
using PEBakery.Helper;
using PEBakery.Lib;
using System;
using System.Collections.Generic;
using System.IO;
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
        public const double PixelScale = 1; // WB082's coord seems too small in WPF's canvas.
        public const double FontScale = 1.4; // WB082's font size seems too small in WPF's canvas.
        public const int DefaultFontSize = 8; // WB082 hard-coded default font size to 8.

        public readonly double masterScale;
        private Canvas canvas;
        private Plugin plugin;
        private List<UICommand> uiCodes;

        public UIRenderer(Canvas canvas, double scale, Plugin plugin)
        {
            this.canvas = canvas;
            this.plugin = plugin;
            this.masterScale = scale;
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
                        failed = UIRenderer.RenderTextBox(canvas, masterScale, uiCmd);
                        break;
                    case UIControlType.TextLabel:
                        failed = UIRenderer.RenderTextLabel(canvas, masterScale, uiCmd);
                        break;
                    case UIControlType.NumberBox:
                        break;
                    case UIControlType.CheckBox:
                        failed = UIRenderer.RenderCheckBox(canvas, masterScale, uiCmd);
                        break;
                    case UIControlType.ComboBox:
                        break;
                    case UIControlType.Image:
                        failed = UIRenderer.RenderImage(canvas, masterScale, uiCmd);
                        break;
                    case UIControlType.TextFile:
                        break;
                    case UIControlType.Button:
                        break;
                    case UIControlType.CheckList:
                        break;
                    case UIControlType.WebLabel:
                        failed = UIRenderer.RenderWebLabel(canvas, masterScale, uiCmd);
                        break;
                    case UIControlType.RadioButton:
                        break;
                    case UIControlType.Bevel:
                        failed = UIRenderer.RenderBevel(canvas, masterScale, uiCmd);
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
        public static bool RenderTextBox(Canvas canvas, double masterScale, UICommand uiCmd)
        {
            UIInfo_TextBox info = uiCmd.Info as UIInfo_TextBox;
            if (info == null)
                return true;

            TextBlock block = new TextBlock()
            {
                Text = uiCmd.Text,
                FontSize = DefaultFontSize * FontScale * masterScale,
            };
            TextBox box = new TextBox()
            {
                Text = info.Value,
                FontSize = DefaultFontSize * FontScale * masterScale,
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
            DrawToCanvas(canvas, masterScale, grid, uiCmd.Rect);
            return false;
        }

        /// <summary>
        /// Render TextLabel control.
        /// Return true if failed.
        /// </summary>
        /// <param name="canvas">Parent canvas</param>
        /// <param name="uiCmd">UICommand</param>
        /// <returns>Success = false, Failure = true</returns>
        public static bool RenderTextLabel(Canvas canvas, double masterScale, UICommand uiCmd)
        {
            UIInfo_TextLabel info = uiCmd.Info as UIInfo_TextLabel;
            if (info == null)
                return true;

            TextBlock block = new TextBlock()
            {
                Text = uiCmd.Text,
                TextWrapping = TextWrapping.Wrap,
                FontSize = info.FontSize * FontScale * masterScale,
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
            DrawToCanvas(canvas, masterScale, block, uiCmd.Rect);
            return false;
        }

        /// <summary>
        /// Render CheckBox control.
        /// Return true if failed.
        /// </summary>
        /// <param name="canvas">Parent canvas</param>
        /// <param name="uiCmd">UICommand</param>
        /// <returns>Success = false, Failure = true</returns>
        public static bool RenderCheckBox(Canvas canvas, double masterScale, UICommand uiCmd)
        {
            UIInfo_CheckBox info = uiCmd.Info as UIInfo_CheckBox;
            if (info == null)
                return true;

            CheckBox checkBox = new CheckBox()
            {
                Content = uiCmd.Text,
                IsChecked = info.Value,
                FontSize = DefaultFontSize * FontScale * masterScale,
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
            DrawToCanvas(canvas, masterScale, checkBox, uiCmd.Rect);
            return false;
        }

        /// <summary>
        /// Render Image control.
        /// Return true if failed.
        /// </summary>
        /// <param name="canvas">Parent canvas</param>
        /// <param name="uiCmd">UICommand</param>
        /// <returns>Success = false, Failure = true</returns>
        public static bool RenderImage(Canvas canvas, double masterScale, UICommand uiCmd)
        {
            UIInfo_Image info = uiCmd.Info as UIInfo_Image;
            if (info == null)
                return true;

            Image image = new Image();
            if (EncodedFile.ExtractInterfaceEncoded(uiCmd.Addr.Plugin, uiCmd.Text, out MemoryStream mem))
                return true;
            if (ImageHelper.GetImageType(uiCmd.Text, out ImageType type))
                return true;
            if (type == ImageType.Svg)
            {
                double width = uiCmd.Rect.Width * PixelScale * masterScale;
                double height = uiCmd.Rect.Height * PixelScale * masterScale;
                image.Source = ImageHelper.SvgToBitmapImage(mem, width, height); 
            }
            else
            {
                image.Source = ImageHelper.ImageToBitmapImage(mem);
            }

            SetToolTip(image, info.ToolTip);
            DrawToCanvas(canvas, masterScale, image, uiCmd.Rect);
            return false;
        }

        /// <summary>
        /// Render WebLabel control.
        /// Return true if failed.
        /// </summary>
        /// <param name="canvas">Parent canvas</param>
        /// <param name="uiCmd">UICommand</param>
        /// <returns>Success = false, Failure = true</returns>
        public static bool RenderWebLabel(Canvas canvas, double masterScale, UICommand uiCmd)
        {
            UIInfo_WebLabel info = uiCmd.Info as UIInfo_WebLabel;
            if (info == null)
                return true;

            TextBlock block = new TextBlock()
            {
                TextWrapping = TextWrapping.Wrap,
                FontSize = DefaultFontSize * FontScale * masterScale,
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
            DrawToCanvas(canvas, masterScale, block, uiCmd.Rect);
            return false;
        }

        /// <summary>
        /// Render Bevel control.
        /// Return true if failed.
        /// </summary>
        /// <param name="canvas">Parent canvas</param>
        /// <param name="uiCmd">UICommand</param>
        /// <returns>Success = false, Failure = true</returns>
        public static bool RenderBevel(Canvas canvas, double masterScale, UICommand uiCmd)
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

            DrawToCanvas(canvas, masterScale, bevel, uiCmd.Rect);
            return false;
        }
        #endregion

        #region Utility
        private static void SetToolTip(FrameworkElement element, string toolTip)
        {
            if (toolTip != null)
                element.ToolTip = toolTip;
        }

        private static void DrawToCanvas(Canvas canvas, double masterScale, FrameworkElement element, Rect coord)
        {
            Canvas.SetLeft(element, coord.Left * PixelScale * masterScale);
            Canvas.SetTop(element, coord.Top * PixelScale * masterScale);
            element.Width = coord.Width * PixelScale * masterScale;
            element.Height = coord.Height * PixelScale * masterScale;
            canvas.Children.Add(element);
        }

        private static void UpdatePlugin(UICommand uiCmd)
        {
            Ini.SetKey(uiCmd.Addr.Plugin.FullPath, new IniKey("Interface", uiCmd.Key, uiCmd.ForgeRawLine(false)));
        }
        #endregion
    }   
}
