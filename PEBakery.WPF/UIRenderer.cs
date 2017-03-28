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
using MahApps.Metro.IconPacks;

namespace PEBakery.WPF
{
    public class UIRenderer
    {
        public const double PixelScale = 1; // WB082's coord seems too small in WPF's canvas.
        public const double FontScale = 1.4; // WB082's font size seems too small in WPF's canvas.
        public const int DefaultFontSize = 8; // WB082 hard-coded default font size to 8.

        private RenderInfo renderInfo;
        private List<UICommand> uiCodes;

        public UIRenderer(Canvas canvas, Window window, Plugin plugin, double scale)
        {
            renderInfo = new RenderInfo(canvas, window, plugin, scale);
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

            InitCanvas(renderInfo.Canvas);

            foreach (UICommand uiCmd in uiCodes)
            {
                if (uiCmd.Visibility == false || uiCmd.Info.Valid == false)
                    continue;

                try
                {
                    switch (uiCmd.Type)
                    {
                        case UIControlType.TextBox:
                            UIRenderer.RenderTextBox(renderInfo, uiCmd);
                            break;
                        case UIControlType.TextLabel:
                            UIRenderer.RenderTextLabel(renderInfo, uiCmd);
                            break;
                        case UIControlType.NumberBox:
                            UIRenderer.RenderNumberBox(renderInfo, uiCmd);
                            break;
                        case UIControlType.CheckBox:
                            UIRenderer.RenderCheckBox(renderInfo, uiCmd);
                            break;
                        case UIControlType.ComboBox:
                            UIRenderer.RenderComboBox(renderInfo, uiCmd);
                            break;
                        case UIControlType.Image:
                            UIRenderer.RenderImage(renderInfo, uiCmd);
                            break;
                        case UIControlType.TextFile:
                            UIRenderer.RenderTextFile(renderInfo, uiCmd);
                            break;
                        case UIControlType.Button:
                            UIRenderer.RenderButton(renderInfo, uiCmd);
                            break;
                        case UIControlType.CheckList:
                            break;
                        case UIControlType.WebLabel:
                            UIRenderer.RenderWebLabel(renderInfo, uiCmd);
                            break;
                        case UIControlType.RadioButton:
                            UIRenderer.RenderRadioButton(renderInfo, uiCmd);
                            break;
                        case UIControlType.Bevel:
                            UIRenderer.RenderBevel(renderInfo, uiCmd);
                            break;
                        case UIControlType.FileBox:
                            UIRenderer.RenderFileBox(renderInfo, uiCmd);
                            break;
                        case UIControlType.RadioGroup:
                            UIRenderer.RenderRadioGroup(renderInfo, uiCmd);
                            break;
                        default:
                            break;
                    }
                }
                // catch (Exception e)
                catch
                {
                    // Log failure
                }
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
        public static void RenderTextBox(RenderInfo r, UICommand uiCmd)
        {
            // It took time finding that WB082 textbox control's y coord is of textbox's, not textlabel's.
            UIInfo_TextBox info = uiCmd.Info as UIInfo_TextBox;
            if (info == null)
                return;

            TextBox box = new TextBox()
            {
                Text = info.Value,
                FontSize = DefaultFontSize * FontScale * r.MasterScale,
            };
            box.LostFocus += (object sender, RoutedEventArgs e) =>
            {
                TextBox tBox = sender as TextBox;
                info.Value = tBox.Text;
                UIRenderer.UpdatePlugin(uiCmd);
            };
            SetToolTip(box, info.ToolTip);
            DrawToCanvas(r, box, uiCmd.Rect);

            if (string.Equals(uiCmd.Text, string.Empty, StringComparison.Ordinal) == false)
            {
                TextBlock block = new TextBlock()
                {
                    Text = uiCmd.Text,
                    LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                    LineHeight = DefaultFontSize * FontScale * r.MasterScale,
                    FontSize = DefaultFontSize * FontScale * r.MasterScale,
                };
                SetToolTip(block, info.ToolTip);
                Rect blockRect = new Rect(uiCmd.Rect.Left, uiCmd.Rect.Top - (block.FontSize + 4), uiCmd.Rect.Width, uiCmd.Rect.Height);
                DrawToCanvas(r, block, blockRect);
            }
        }

        /// <summary>
        /// Render TextLabel control.
        /// Return true if failed.
        /// </summary>
        /// <param name="r.Canvas">Parent r.Canvas</param>
        /// <param name="uiCmd">UICommand</param>
        /// <returns>Success = false, Failure = true</returns>
        public static void RenderTextLabel(RenderInfo r, UICommand uiCmd)
        {
            UIInfo_TextLabel info = uiCmd.Info as UIInfo_TextLabel;
            if (info == null)
                return;

            TextBlock block = new TextBlock()
            {
                Text = uiCmd.Text,
                TextWrapping = TextWrapping.Wrap,
                LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                LineHeight = info.FontSize * FontScale * r.MasterScale,
                FontSize = info.FontSize * FontScale * r.MasterScale,
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
            DrawToCanvas(r, block, uiCmd.Rect);
        }

        /// <summary>
        /// Render NumberBox control.
        /// Return true if failed.
        /// </summary>
        /// <param name="r.Canvas">Parent r.Canvas</param>
        /// <param name="r.MasterScale">Master Scale Factor</param>
        /// <param name="uiCmd">UICommand</param>
        /// <returns>Success = false, Failure = true</returns>
        public static void RenderNumberBox(RenderInfo r, UICommand uiCmd)
        {
            UIInfo_NumberBox info = uiCmd.Info as UIInfo_NumberBox;
            if (info == null)
                return;

            SpinnerControl spinner = new SpinnerControl()
            {
                Value = info.Value,
                FontSize = DefaultFontSize * FontScale * r.MasterScale,
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
            DrawToCanvas(r, spinner, uiCmd.Rect);
        }

        /// <summary>
        /// Render CheckBox control.
        /// Return true if failed.
        /// </summary>
        /// <param name="r.Canvas">Parent r.Canvas</param>
        /// <param name="uiCmd">UICommand</param>
        /// <returns>Success = false, Failure = true</returns>
        public static void RenderCheckBox(RenderInfo r, UICommand uiCmd)
        {
            UIInfo_CheckBox info = uiCmd.Info as UIInfo_CheckBox;
            if (info == null)
                return;

            CheckBox checkBox = new CheckBox()
            {
                Content = uiCmd.Text,
                IsChecked = info.Value,
                FontSize = DefaultFontSize * FontScale * r.MasterScale,
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
            DrawToCanvas(r, checkBox, uiCmd.Rect);
        }

        /// <summary>
        /// Render ComboBox control.
        /// Return true if failed.
        /// </summary>
        /// <param name="r.Canvas">Parent r.Canvas</param>
        /// <param name="uiCmd">UICommand</param>
        /// <returns>Success = false, Failure = true</returns>
        public static void RenderComboBox(RenderInfo r, UICommand uiCmd)
        {
            UIInfo_ComboBox info = uiCmd.Info as UIInfo_ComboBox;
            if (info == null)
                return;

            ComboBox comboBox = new ComboBox()
            {
                FontSize = DefaultFontSize * FontScale * r.MasterScale,
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
            DrawToCanvas(r, comboBox, uiCmd.Rect);
        }

        /// <summary>
        /// Render Image control.
        /// Return true if failed.
        /// </summary>
        /// <param name="r.Canvas">Parent r.Canvas</param>
        /// <param name="uiCmd">UICommand</param>
        /// <returns>Success = false, Failure = true</returns>
        public static void RenderImage(RenderInfo r, UICommand uiCmd)
        {
            UIInfo_Image info = uiCmd.Info as UIInfo_Image;
            if (info == null)
                return;

            Image image = new Image()
            {
                UseLayoutRounding = true,
            };
            MemoryStream mem = EncodedFile.ExtractInterfaceEncoded(uiCmd.Addr.Plugin, uiCmd.Text);
            if (ImageHelper.GetImageType(uiCmd.Text, out ImageType type))
                return;

            if (info.URL == null)
            {
                if (type == ImageType.Svg)
                {
                    double width = uiCmd.Rect.Width * PixelScale * r.MasterScale;
                    double height = uiCmd.Rect.Height * PixelScale * r.MasterScale;
                    image.Source = ImageHelper.SvgToBitmapImage(mem, width, height);
                }
                else
                {
                    image.Source = ImageHelper.ImageToBitmapImage(mem);
                }

                SetToolTip(image, info.ToolTip);
                DrawToCanvas(r, image, uiCmd.Rect);
            }
            else
            {
                Button button = new Button();
                button.Style = (Style) r.Window.FindResource("ImageButton");
                if (type == ImageType.Svg)
                {
                    double width = uiCmd.Rect.Width * PixelScale * r.MasterScale;
                    double height = uiCmd.Rect.Height * PixelScale * r.MasterScale;
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
                DrawToCanvas(r, button, uiCmd.Rect);
            }
        }

        /// <summary>
        /// Render TextFile control.
        /// Return true if failed.
        /// </summary>
        /// <param name="r.Canvas">Parent r.Canvas</param>
        /// <param name="uiCmd">UICommand</param>
        /// <returns>Success = false, Failure = true</returns>
        public static void RenderTextFile(RenderInfo r, UICommand uiCmd)
        {
            UIInfo_TextFile info = uiCmd.Info as UIInfo_TextFile;
            if (info == null)
                return;

            MemoryStream mem = EncodedFile.ExtractInterfaceEncoded(uiCmd.Addr.Plugin, uiCmd.Text);

            StreamReader reader = new StreamReader(mem, FileHelper.DetectTextEncoding(mem));
            TextBox textBox = new TextBox()
            {
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                IsReadOnly = true,
                Text = reader.ReadToEnd(),
                FontSize = DefaultFontSize * FontScale * r.MasterScale,
            };
            reader.Close();
            ScrollViewer.SetHorizontalScrollBarVisibility(textBox, ScrollBarVisibility.Auto);
            ScrollViewer.SetVerticalScrollBarVisibility(textBox, ScrollBarVisibility.Auto);
            ScrollViewer.SetCanContentScroll(textBox, true);

            SetToolTip(textBox, info.ToolTip);
            DrawToCanvas(r, textBox, uiCmd.Rect);
        }

        /// <summary>
        /// Render Button control.
        /// Return true if failed.
        /// </summary>
        /// <param name="r.Canvas">Parent r.Canvas</param>
        /// <param name="uiCmd">UICommand</param>
        /// <returns>Success = false, Failure = true</returns>
        public static void RenderButton(RenderInfo r, UICommand uiCmd)
        {
            UIInfo_Button info = uiCmd.Info as UIInfo_Button;
            if (info == null)
                return;

            Button button = new Button()
            {
                Content = uiCmd.Text,
                FontSize = DefaultFontSize * FontScale * r.MasterScale,
            };

            button.Click += (object sender, RoutedEventArgs e) =>
            {
                Button bt = sender as Button;
                // Engine.RunSection(); -- Not implemented
                Console.WriteLine("Engine.RunSection not implemented");
            };

            if (info.Picture != null)
            {
                MemoryStream mem = EncodedFile.ExtractInterfaceEncoded(uiCmd.Addr.Plugin, info.Picture);
                if (ImageHelper.GetImageType(uiCmd.Text, out ImageType type))
                    return;
                   
                if (type == ImageType.Svg)
                {
                    double width = uiCmd.Rect.Width * PixelScale * r.MasterScale;
                    double height = uiCmd.Rect.Height * PixelScale * r.MasterScale;
                    button.Background = ImageHelper.SvgToImageBrush(mem, width, height);
                }
                else
                {
                    button.Background = ImageHelper.ImageToImageBrush(mem);
                }

                SetToolTip(button, info.ToolTip);
                DrawToCanvas(r, button, uiCmd.Rect);
            }
        }

        /// <summary>
        /// Render WebLabel control.
        /// Return true if failed.
        /// </summary>
        /// <param name="r.Canvas">Parent r.Canvas</param>
        /// <param name="uiCmd">UICommand</param>
        /// <returns>Success = false, Failure = true</returns>
        public static void RenderWebLabel(RenderInfo r, UICommand uiCmd)
        {
            UIInfo_WebLabel info = uiCmd.Info as UIInfo_WebLabel;
            if (info == null)
                return;

            TextBlock block = new TextBlock()
            {
                TextWrapping = TextWrapping.Wrap,
                FontSize = DefaultFontSize * FontScale * r.MasterScale,
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
            DrawToCanvas(r, block, uiCmd.Rect);
        }

        /// <summary>
        /// Render Bevel control.
        /// Return true if failed.
        /// </summary>
        /// <param name="r.Canvas">Parent r.Canvas</param>
        /// <param name="uiCmd">UICommand</param>
        /// <returns>Success = false, Failure = true</returns>
        public static void RenderBevel(RenderInfo r, UICommand uiCmd)
        {
            UIInfo_Bevel info = uiCmd.Info as UIInfo_Bevel;
            if (info == null)
                return;

            Border bevel = new Border()
            {
                IsHitTestVisible = false,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.Gray,
                ToolTip = info.ToolTip,
            };

            DrawToCanvas(r, bevel, uiCmd.Rect);
        }

        /// <summary>
        /// Render RadioGroup control.
        /// Return true if failed.
        /// </summary>
        /// <param name="r.Canvas">Parent r.Canvas</param>
        /// <param name="uiCmd">UICommand</param>
        /// <returns>Success = false, Failure = true</returns>
        public static void RenderRadioButton(RenderInfo r, UICommand uiCmd)
        {
            UIInfo_RadioButton info = uiCmd.Info as UIInfo_RadioButton;
            if (info == null)
                return;

            double fontSize = DefaultFontSize * FontScale * r.MasterScale;

            RadioButton radio = new RadioButton()
            {
                GroupName = r.Plugin.FullPath,
                Content = uiCmd.Text,
                FontSize = fontSize,
                IsChecked = info.Selected,
            };

            radio.Checked += (object sender, RoutedEventArgs e) =>
            {
                RadioButton btn = sender as RadioButton;
                info.Selected = true;
                UIRenderer.UpdatePlugin(uiCmd);
            };
            radio.Unchecked += (object sender, RoutedEventArgs e) =>
            {
                RadioButton btn = sender as RadioButton;
                info.Selected = false;
                UIRenderer.UpdatePlugin(uiCmd);
            };

            SetToolTip(radio, info.ToolTip);
            DrawToCanvas(r, radio, uiCmd.Rect);
        }

        /// <summary>
        /// Render FileBox control.
        /// Return true if failed.
        /// </summary>
        /// <param name="canvas">Parent canvas</param>
        /// <param name="uiCmd">UICommand</param>
        public static void RenderFileBox(RenderInfo r, UICommand uiCmd)
        {
            // It took time finding that WB082 textbox control's y coord is of textbox's, not textlabel's.
            UIInfo_FileBox info = uiCmd.Info as UIInfo_FileBox;
            if (info == null)
                return;

            TextBox box = new TextBox()
            {
                Text = uiCmd.Text,
                FontSize = DefaultFontSize * FontScale * r.MasterScale,
            };
            box.LostFocus += (object sender, RoutedEventArgs e) =>
            {
                TextBox tBox = sender as TextBox;
                uiCmd.Text = tBox.Text;
                UIRenderer.UpdatePlugin(uiCmd);
            };
            SetToolTip(box, info.ToolTip);

            Button button = new Button()
            {
                FontSize = DefaultFontSize * FontScale * r.MasterScale,
                Content = MainWindow.GetMaterialIcon(PackIconMaterialKind.FolderUpload, 0),
            };
            SetToolTip(button, info.ToolTip);

            button.Click += (object sender, RoutedEventArgs e) =>
            {
                Button bt = sender as Button;

                if (info.IsFile)
                {
                    Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog()
                    {
                        // TODO
                        // Variable expand of uiCmd.Text, then use as GetDirectoryName
                        // InitialDirectory = Path.GetDirectoryName()
                    };
                    if (dialog.ShowDialog() == true)
                        box.Text = dialog.FileName;
                }
                else
                {
                    System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog()
                    {
                        // TODO
                        // Variable expand of uiCmd.Text, then use as GetDirectoryName
                        // RootFolder = Path.GetDirectoryName()
                    };
                    System.Windows.Forms.DialogResult result = dialog.ShowDialog();
                    if (result == System.Windows.Forms.DialogResult.OK)
                    {
                        box.Text = dialog.SelectedPath;
                    }
                }
            };

            double margin = 5;
            Rect boxRect = new Rect(uiCmd.Rect.Left, uiCmd.Rect.Top, uiCmd.Rect.Width - (uiCmd.Rect.Height + margin), uiCmd.Rect.Height);
            Rect btnRect = new Rect(boxRect.Right + margin, uiCmd.Rect.Top, uiCmd.Rect.Height, uiCmd.Rect.Height);
            DrawToCanvas(r, box, boxRect);
            DrawToCanvas(r, button, btnRect);
        }

        /// <summary>
        /// Render RadioGroup control.
        /// Return true if failed.
        /// </summary>
        /// <param name="r.Canvas">Parent r.Canvas</param>
        /// <param name="uiCmd">UICommand</param>
        /// <returns>Success = false, Failure = true</returns>
        public static void RenderRadioGroup(RenderInfo r, UICommand uiCmd)
        {
            UIInfo_RadioGroup info = uiCmd.Info as UIInfo_RadioGroup;
            if (info == null)
                return;

            double fontSize = DefaultFontSize * FontScale * r.MasterScale;

            Border bevel = new Border()
            {
                IsHitTestVisible = false,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.Gray,
                ToolTip = info.ToolTip,
            };

            Border border = new Border()
            {
                Background = Brushes.White,
                BorderThickness = new Thickness(2, 0, 2, 0),
                BorderBrush = Brushes.White,
            };
            TextBlock block = new TextBlock()
            {
                Text = uiCmd.Text,
                FontSize = fontSize,
            };
            border.Child = block;

            SetToolTip(bevel, info.ToolTip);
            SetToolTip(border, info.ToolTip);

            List<RadioButton> list = new List<RadioButton>();
            for (int i = 0; i < info.Items.Count; i++)
            {
                RadioButton radio = new RadioButton()
                {
                    GroupName = r.Plugin.FullPath + uiCmd.Key,
                    Content = info.Items[i],
                    Tag = i,
                    FontSize = fontSize,
                };

                if (i == info.Selected)
                    radio.IsChecked = true;
                else
                    radio.IsChecked = false;

                radio.Checked += (object sender, RoutedEventArgs e) =>
                {
                    RadioButton btn = sender as RadioButton;
                    info.Selected = (int)btn.Tag;
                    UIRenderer.UpdatePlugin(uiCmd);
                };

                SetToolTip(radio, info.ToolTip);
            
                list.Add(radio);
            }

            double pushToBottom = fontSize * 0.6;
            Rect bevelRect = new Rect(uiCmd.Rect.Left, uiCmd.Rect.Top + pushToBottom, uiCmd.Rect.Width, uiCmd.Rect.Height - pushToBottom);
            Rect textRect = new Rect(uiCmd.Rect.Left + 5, uiCmd.Rect.Top, double.NaN, double.NaN); // NaN for auto width/height

            // Keep order!
            DrawToCanvas(r, bevel, bevelRect);
            DrawToCanvas(r, border, textRect);
            double margin = fontSize + (7 * r.MasterScale);
            for (int i = 0; i < list.Count; i++)
            {
                Rect rect = new Rect(uiCmd.Rect.Left + 5, uiCmd.Rect.Top + margin * (i + 1), double.NaN, double.NaN); // NaN for auto width/height
                DrawToCanvas(r, list[i], rect);
            }
        }
        #endregion

        #region Utility
        private static void SetToolTip(FrameworkElement element, string toolTip)
        {
            if (toolTip != null)
                element.ToolTip = toolTip;
        }

        private static void InitCanvas(Canvas canvas)
        {
            canvas.Width = double.NaN;
            canvas.Height = double.NaN;
        }

        private static void DrawToCanvas(RenderInfo r, FrameworkElement element, Rect coord)
        {
            double left = coord.Left * PixelScale * r.MasterScale;
            double top = coord.Top * PixelScale * r.MasterScale;
            double width = coord.Width * PixelScale * r.MasterScale;
            double height = coord.Height * PixelScale * r.MasterScale;
            Canvas.SetLeft(element, left);
            Canvas.SetTop(element, top);
            element.Width = width;
            element.Height = height;
            
            r.Canvas.Children.Add(element);
            if (double.IsNaN(r.Canvas.Width) || r.Canvas.Width < left + width)
                r.Canvas.Width = left + width;
            if (double.IsNaN(r.Canvas.Height) || r.Canvas.Height < top + height)
                r.Canvas.Height = top + height;
        }

        private static void UpdatePlugin(UICommand uiCmd)
        {
            Ini.SetKey(uiCmd.Addr.Plugin.FullPath, new IniKey("Interface", uiCmd.Key, uiCmd.ForgeRawLine(false)));
        }
        #endregion
    }

    #region RenderInfo
    public struct RenderInfo
    {
        public readonly double MasterScale;
        public readonly Canvas Canvas;
        public readonly Window Window;
        public readonly Plugin Plugin;

        public RenderInfo(Canvas canvas, Window window, Plugin plugin, double masterScale)
        {
            Canvas = canvas;
            Window = window;
            Plugin = plugin;
            MasterScale = masterScale;
        }
    }
    #endregion
}
