/*
    Copyright (C) 2016-2017 Hajin Jang
    Licensed under GPL 3.0
 
    PEBakery is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using PEBakery.Core;
using PEBakery.Helper;
using PEBakery.Lib;
using Btl.Controls;
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
        private Window window;
        private Plugin plugin;
        private List<UICommand> uiCodes;

        public UIRenderer(Canvas canvas, double scale, Window window, Plugin plugin)
        {
            this.window = window;
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
                        failed = UIRenderer.RenderNumberBox(canvas, masterScale, uiCmd);
                        break;
                    case UIControlType.CheckBox:
                        failed = UIRenderer.RenderCheckBox(canvas, masterScale, uiCmd);
                        break;
                    case UIControlType.ComboBox:
                        failed = UIRenderer.RenderComboBox(canvas, masterScale, uiCmd);
                        break;
                    case UIControlType.Image:
                        failed = UIRenderer.RenderImage(canvas, masterScale, window, uiCmd);
                        break;
                    case UIControlType.TextFile:
                        failed = UIRenderer.RenderTextFile(canvas, masterScale, uiCmd);
                        break;
                    case UIControlType.Button:
                        failed = UIRenderer.RenderButton(canvas, masterScale, window, uiCmd);
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
            // It took time finding that WB082 textbox control's y coord is of textbox's, not textlabel's.
            UIInfo_TextBox info = uiCmd.Info as UIInfo_TextBox;
            if (info == null)
                return true;

            TextBox box = new TextBox()
            {
                Text = info.Value,
                FontSize = DefaultFontSize * FontScale * masterScale,
            };
            box.LostFocus += (object sender, RoutedEventArgs e) =>
            {
                TextBox tBox = sender as TextBox;
                info.Value = tBox.Text;
                UIRenderer.UpdatePlugin(uiCmd);
            };
            SetToolTip(box, info.ToolTip);
            DrawToCanvas(canvas, masterScale, box, uiCmd.Rect);

            if (string.Equals(uiCmd.Text, string.Empty, StringComparison.Ordinal) == false)
            {
                TextBlock block = new TextBlock()
                {
                    Text = uiCmd.Text,
                    LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                    LineHeight = DefaultFontSize * FontScale * masterScale,
                    FontSize = DefaultFontSize * FontScale * masterScale,
                };
                SetToolTip(block, info.ToolTip);
                Rect blockRect = new Rect(uiCmd.Rect.Left, uiCmd.Rect.Top - (block.FontSize + 5), uiCmd.Rect.Width, uiCmd.Rect.Height);
                DrawToCanvas(canvas, masterScale, block, blockRect);
            }

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
                LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                LineHeight = info.FontSize * FontScale * masterScale,
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
        /// Render NumberBox control.
        /// Return true if failed.
        /// </summary>
        /// <param name="canvas">Parent canvas</param>
        /// <param name="masterScale">Master Scale Factor</param>
        /// <param name="uiCmd">UICommand</param>
        /// <returns>Success = false, Failure = true</returns>
        public static bool RenderNumberBox(Canvas canvas, double masterScale, UICommand uiCmd)
        {
            UIInfo_NumberBox info = uiCmd.Info as UIInfo_NumberBox;
            if (info == null)
                return true;

            SpinnerControl spinner = new SpinnerControl()
            {
                Value = info.Value,
                FontSize = DefaultFontSize * FontScale * masterScale,
                Minimum = info.Min,
                Maximum = info.Max,
                DecimalPlaces = 0,
                Change = info.Interval,
            };
            spinner.LostFocus += (object sender, RoutedEventArgs e) => {
                SpinnerControl spin = sender as SpinnerControl;
                info.Value = (int) spin.Value;
                UIRenderer.UpdatePlugin(uiCmd);
            };

            SetToolTip(spinner, info.ToolTip);
            DrawToCanvas(canvas, masterScale, spinner, uiCmd.Rect);
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
        /// Render ComboBox control.
        /// Return true if failed.
        /// </summary>
        /// <param name="canvas">Parent canvas</param>
        /// <param name="uiCmd">UICommand</param>
        /// <returns>Success = false, Failure = true</returns>
        public static bool RenderComboBox(Canvas canvas, double masterScale, UICommand uiCmd)
        {
            UIInfo_ComboBox info = uiCmd.Info as UIInfo_ComboBox;
            if (info == null)
                return true;

            ComboBox comboBox = new ComboBox()
            {
                FontSize = DefaultFontSize * FontScale * masterScale,
                ItemsSource = info.Items,
                SelectedIndex = info.Index,
            };

            comboBox.LostFocus += (object sender, RoutedEventArgs e) =>
            {
                ComboBox box = sender as ComboBox;
                if (info.Index != box.SelectedIndex)
                {
                    info.Index = box.SelectedIndex;
                    uiCmd.Text = info.Items[box.SelectedIndex];
                    UIRenderer.UpdatePlugin(uiCmd);
                }
            };

            SetToolTip(comboBox, info.ToolTip);
            DrawToCanvas(canvas, masterScale, comboBox, uiCmd.Rect);
            return false;
        }

        /// <summary>
        /// Render Image control.
        /// Return true if failed.
        /// </summary>
        /// <param name="canvas">Parent canvas</param>
        /// <param name="uiCmd">UICommand</param>
        /// <returns>Success = false, Failure = true</returns>
        public static bool RenderImage(Canvas canvas, double masterScale, Window window, UICommand uiCmd)
        {
            UIInfo_Image info = uiCmd.Info as UIInfo_Image;
            if (info == null)
                return true;

            Image image = new Image();
            if (EncodedFile.ExtractInterfaceEncoded(uiCmd.Addr.Plugin, uiCmd.Text, out MemoryStream mem))
                return true;
            if (ImageHelper.GetImageType(uiCmd.Text, out ImageType type))
                return true;
                 

            if (info.URL == null)
            {
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
            }
            else
            {
                Button button = new Button();
                button.Style = (Style)window.FindResource("ImageButton");
                if (type == ImageType.Svg)
                {
                    double width = uiCmd.Rect.Width * PixelScale * masterScale;
                    double height = uiCmd.Rect.Height * PixelScale * masterScale;
                    button.Background = ImageHelper.SvgToImageBrush(mem, width, height);
                }
                else
                {
                    button.Background = ImageHelper.ImageToImageBrush(mem);
                }
                button.Click += (object sender, RoutedEventArgs e) =>
                {
                    System.Diagnostics.Process.Start(info.URL.ToString());
                };

                SetToolTip(button, info.ToolTip);
                DrawToCanvas(canvas, masterScale, button, uiCmd.Rect);
            }
            
            return false;
        }

        /// <summary>
        /// Render TextFile control.
        /// Return true if failed.
        /// </summary>
        /// <param name="canvas">Parent canvas</param>
        /// <param name="uiCmd">UICommand</param>
        /// <returns>Success = false, Failure = true</returns>
        public static bool RenderTextFile(Canvas canvas, double masterScale, UICommand uiCmd)
        {
            UIInfo_TextFile info = uiCmd.Info as UIInfo_TextFile;
            if (info == null)
                return true;

            if (EncodedFile.ExtractInterfaceEncoded(uiCmd.Addr.Plugin, uiCmd.Text, out MemoryStream mem))
                return true;

            StreamReader reader = new StreamReader(mem, FileHelper.DetectTextEncoding(mem));
            TextBox textBox = new TextBox()
            {
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                IsReadOnly = true,
                Text = reader.ReadToEnd(),
                FontSize = DefaultFontSize * FontScale * masterScale,
            };
            reader.Close();
            ScrollViewer.SetHorizontalScrollBarVisibility(textBox, ScrollBarVisibility.Auto);
            ScrollViewer.SetVerticalScrollBarVisibility(textBox, ScrollBarVisibility.Auto);
            ScrollViewer.SetCanContentScroll(textBox, true);

            SetToolTip(textBox, info.ToolTip);
            DrawToCanvas(canvas, masterScale, textBox, uiCmd.Rect);
            return false;
        }

        /// <summary>
        /// Render Button control.
        /// Return true if failed.
        /// </summary>
        /// <param name="canvas">Parent canvas</param>
        /// <param name="uiCmd">UICommand</param>
        /// <returns>Success = false, Failure = true</returns>
        public static bool RenderButton(Canvas canvas, double masterScale, Window window, UICommand uiCmd)
        {
            UIInfo_Button info = uiCmd.Info as UIInfo_Button;
            if (info == null)
                return true;

            Button button = new Button()
            {
                Content = uiCmd.Text,
                FontSize = DefaultFontSize * FontScale * masterScale,
            };

            button.Click += (object sender, RoutedEventArgs e) =>
            {
                Button bt = sender as Button;
                // Engine.RunSection(); -- Not implemented
                Console.WriteLine("Engine.RunSection not implemented");
            };

            if (info.Picture != null)
            {
                if (EncodedFile.ExtractInterfaceEncoded(uiCmd.Addr.Plugin, info.Picture, out MemoryStream mem) == false)
                {
                    if (ImageHelper.GetImageType(uiCmd.Text, out ImageType type) == false)
                    {
                        if (type == ImageType.Svg)
                        {
                            double width = uiCmd.Rect.Width * PixelScale * masterScale;
                            double height = uiCmd.Rect.Height * PixelScale * masterScale;
                            button.Background = ImageHelper.SvgToImageBrush(mem, width, height);
                        }
                        else
                        {
                            button.Background = ImageHelper.ImageToImageBrush(mem);
                        }
                        button.Style = (Style)window.FindResource("BackgroundButton");
                    }
                }
            }

            SetToolTip(button, info.ToolTip);
            DrawToCanvas(canvas, masterScale, button, uiCmd.Rect);
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
            hyperLink.RequestNavigate += (object sender, RequestNavigateEventArgs e) =>
            {
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

        private static void DrawToCanvas(Canvas canvas, double masterScale, FrameworkElement element, Rect coord, bool useFontScale = false)
        {
            Canvas.SetLeft(element, coord.Left * PixelScale * masterScale);
            Canvas.SetTop(element, coord.Top * PixelScale * masterScale);
            if (useFontScale)
            {
                element.Width = coord.Width * FontScale * masterScale;
                element.Height = coord.Height * FontScale * masterScale;
            }
            else
            {
                element.Width = coord.Width * PixelScale * masterScale;
                element.Height = coord.Height * PixelScale * masterScale;
            }
            
            canvas.Children.Add(element);
        }

        private static void UpdatePlugin(UICommand uiCmd)
        {
            Ini.SetKey(uiCmd.Addr.Plugin.FullPath, new IniKey("Interface", uiCmd.Key, uiCmd.ForgeRawLine(false)));
        }
        #endregion
    }   
}
