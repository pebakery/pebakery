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
using PEBakery.Exceptions;
using PEBakery.Lib;
using PEBakery.WPF.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Diagnostics;
using MahApps.Metro.IconPacks;
using System.Windows.Media.Imaging;
using System.ComponentModel;
using System.Windows.Threading;
using System.Threading.Tasks;
using System.Threading;

namespace PEBakery.WPF
{
    // TODO: Fix potential memory leak due to event handler
    public class UIRenderer
    {
        public const int DefaultFontPoint = 8; // WB082 hard-coded default font point to 8.
        public const double PointToDeviceIndependentPixel = 96f / 72f; // Point - 72DPI, Device Independent Pixel - 96DPI
        public const int MaxDpiScale = 4;

        private readonly RenderInfo renderInfo;
        private readonly List<UICommand> uiCodes;
        private readonly Variables variables;
        private readonly Logger logger;

        public UIRenderer(Canvas canvas, MainWindow window, Plugin plugin, Logger logger, double scale)
        {
            this.logger = logger;
            this.variables = plugin.Project.Variables;

            // Check if plugin has custom interface section
            string interfaceSectionName = "Interface";
            if (plugin.MainInfo.ContainsKey("Interface")) 
                interfaceSectionName = plugin.MainInfo["Interface"];

            this.renderInfo = new RenderInfo(canvas, window, logger, plugin, interfaceSectionName, scale);

            if (plugin.Sections.ContainsKey(interfaceSectionName))
            {
                try
                {
                    this.uiCodes = plugin.Sections[interfaceSectionName].GetUICodes(true);
                    logger.System_Write(plugin.Sections[interfaceSectionName].LogInfos);
                }
                catch
                {
                    this.uiCodes = null;
                    logger.System_Write(new LogInfo(LogState.Error, $"Cannot read interface controls from [{plugin.ShortPath}]"));
                }
            }
            else
            {
                this.uiCodes = null;
                logger.System_Write(new LogInfo(LogState.Error, $"Cannot read interface controls from [{plugin.ShortPath}]"));
            }
        }

        #region Render All
        public void Render()
        {
            if (uiCodes == null) // This plugin does not have 'Interface' section
                return;

            InitCanvas(renderInfo.Canvas);

            foreach (UICommand uiCmd in uiCodes)
            {
                if (uiCmd.Visibility == false)
                    continue;

                try
                {
                    switch (uiCmd.Type)
                    {
                        case UIType.TextBox:
                            UIRenderer.RenderTextBox(renderInfo, uiCmd);
                            break;
                        case UIType.TextLabel:
                            UIRenderer.RenderTextLabel(renderInfo, uiCmd);
                            break;
                        case UIType.NumberBox:
                            UIRenderer.RenderNumberBox(renderInfo, uiCmd);
                            break;
                        case UIType.CheckBox:
                            UIRenderer.RenderCheckBox(renderInfo, uiCmd);
                            break;
                        case UIType.ComboBox:
                            UIRenderer.RenderComboBox(renderInfo, uiCmd);
                            break;
                        case UIType.Image:
                            UIRenderer.RenderImage(renderInfo, uiCmd);
                            break;
                        case UIType.TextFile:
                            UIRenderer.RenderTextFile(renderInfo, uiCmd);
                            break;
                        case UIType.Button:
                            UIRenderer.RenderButton(renderInfo, uiCmd, logger);
                            break;
                        case UIType.WebLabel:
                            UIRenderer.RenderWebLabel(renderInfo, uiCmd);
                            break;
                        case UIType.RadioButton:
                            UIRenderer.RenderRadioButton(renderInfo, uiCmd);
                            break;
                        case UIType.Bevel:
                            UIRenderer.RenderBevel(renderInfo, uiCmd);
                            break;
                        case UIType.FileBox:
                            UIRenderer.RenderFileBox(renderInfo, uiCmd, variables);
                            break;
                        case UIType.RadioGroup:
                            UIRenderer.RenderRadioGroup(renderInfo, uiCmd);
                            break;
                        default:
                            logger.System_Write(new LogInfo(LogState.Error, $"Unable to render [{uiCmd.RawLine}]"));
                            break;
                    }
                }
                catch (Exception e)
                { // Log failure
                    logger.System_Write(new LogInfo(LogState.Error, $"{Logger.LogExceptionMessage(e)} [{uiCmd.RawLine}]"));
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
            // WB082 textbox control's y coord is of textbox's, not textlabel's.
            Debug.Assert(uiCmd.Info.GetType() == typeof(UIInfo_TextBox));
            UIInfo_TextBox info = uiCmd.Info as UIInfo_TextBox;

            TextBox box = new TextBox()
            {
                Text = info.Value,
                FontSize = CalcFontPointScale(),
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            box.LostFocus += (object sender, RoutedEventArgs e) =>
            {
                TextBox tBox = sender as TextBox;
                info.Value = tBox.Text;
                UIRenderer.UpdatePlugin(r.InterfaceSectionName, uiCmd);
            };
            SetToolTip(box, info.ToolTip);
            DrawToCanvas(r, box, uiCmd.Rect);

            if (string.Equals(uiCmd.Text, string.Empty, StringComparison.Ordinal) == false)
            {
                TextBlock block = new TextBlock()
                {
                    Text = uiCmd.Text,
                    LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                    LineHeight = CalcFontPointScale(),
                    FontSize = CalcFontPointScale(),
                };
                SetToolTip(block, info.ToolTip);
                double margin = PointToDeviceIndependentPixel * DefaultFontPoint * 1.2;
                Rect blockRect = new Rect(uiCmd.Rect.Left, uiCmd.Rect.Top - margin, uiCmd.Rect.Width, uiCmd.Rect.Height);
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
            Debug.Assert(uiCmd.Info.GetType() == typeof(UIInfo_TextLabel));
            UIInfo_TextLabel info = uiCmd.Info as UIInfo_TextLabel;

            TextBlock block = new TextBlock()
            {
                Text = uiCmd.Text,
                TextWrapping = TextWrapping.Wrap,
                LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                LineHeight = CalcFontPointScale(info.FontSize),
                FontSize = CalcFontPointScale(info.FontSize),
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
            Debug.Assert(uiCmd.Info.GetType() == typeof(UIInfo_NumberBox));
            UIInfo_NumberBox info = uiCmd.Info as UIInfo_NumberBox;

            SpinnerControl spinner = new SpinnerControl()
            {
                Value = info.Value,
                FontSize = CalcFontPointScale(),
                Minimum = info.Min,
                Maximum = info.Max,
                DecimalPlaces = 0,
                Change = info.Interval,
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            spinner.LostFocus += (object sender, RoutedEventArgs e) =>
            {
                SpinnerControl spin = sender as SpinnerControl;
                info.Value = (int) spin.Value;
                UIRenderer.UpdatePlugin(r.InterfaceSectionName, uiCmd);
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
            Debug.Assert(uiCmd.Info.GetType() == typeof(UIInfo_CheckBox));
            UIInfo_CheckBox info = uiCmd.Info as UIInfo_CheckBox;

            CheckBox checkBox = new CheckBox()
            {
                Content = uiCmd.Text,
                IsChecked = info.Value,
                FontSize = CalcFontPointScale(),
                VerticalContentAlignment = VerticalAlignment.Center,
            };

            if (info.SectionName != null)
            {
                checkBox.Click += (object sender, RoutedEventArgs e) =>
                {
                    if (r.Plugin.Sections.ContainsKey(info.SectionName)) // Only if section exists
                    {
                        SectionAddress addr = new SectionAddress(r.Plugin, r.Plugin.Sections[info.SectionName]);
                        UIRenderer.RunOneSection(addr, $"{r.Plugin.Title} - CheckBox [{uiCmd.Key}]", info.ShowProgress);
                    }
                    else
                    {
                        r.Logger.System_Write(new LogInfo(LogState.Error, $"Section [{info.SectionName}] does not exists"));
                    }
                };
            }
            
            checkBox.Checked += (object sender, RoutedEventArgs e) =>
            {
                CheckBox box = sender as CheckBox;
                info.Value = true;
                UIRenderer.UpdatePlugin(r.InterfaceSectionName, uiCmd);
            };
            checkBox.Unchecked += (object sender, RoutedEventArgs e) =>
            {
                CheckBox box = sender as CheckBox;
                info.Value = false;
                UIRenderer.UpdatePlugin(r.InterfaceSectionName, uiCmd);
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
            Debug.Assert(uiCmd.Info.GetType() == typeof(UIInfo_ComboBox));
            UIInfo_ComboBox info = uiCmd.Info as UIInfo_ComboBox;

            ComboBox comboBox = new ComboBox()
            {
                FontSize = CalcFontPointScale(),
                ItemsSource = info.Items,
                SelectedIndex = info.Index,
                VerticalContentAlignment = VerticalAlignment.Center,
            };

            comboBox.LostFocus += (object sender, RoutedEventArgs e) =>
            {
                ComboBox box = sender as ComboBox;
                if (info.Index != box.SelectedIndex)
                {
                    info.Index = box.SelectedIndex;
                    uiCmd.Text = info.Items[box.SelectedIndex];
                    UIRenderer.UpdatePlugin(r.InterfaceSectionName, uiCmd);
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
            Debug.Assert(uiCmd.Info.GetType() == typeof(UIInfo_Image));
            UIInfo_Image info = uiCmd.Info as UIInfo_Image;

            Image image = new Image()
            {
                UseLayoutRounding = true,
            };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            Button button;

            using (MemoryStream ms = EncodedFile.ExtractInterfaceEncoded(uiCmd.Addr.Plugin, uiCmd.Text))
            {
                if (ImageHelper.GetImageType(uiCmd.Text, out ImageHelper.ImageType type))
                    return;

                button = new Button()
                {
                    Style = (Style)r.Window.FindResource("ImageButton")
                };
                if (type == ImageHelper.ImageType.Svg)
                {
                    double width = uiCmd.Rect.Width * r.MasterScale;
                    double height = uiCmd.Rect.Height * r.MasterScale;
                    button.Background = ImageHelper.SvgToImageBrush(ms, width, height);
                }
                else
                {
                    button.Background = ImageHelper.ImageToImageBrush(ms);
                }
                ms.Close();
            }
                
            bool hasUrl = false;
            if (info.URL != null && string.Equals(info.URL, string.Empty, StringComparison.Ordinal) == false)
            {
                if (Uri.TryCreate(info.URL, UriKind.Absolute, out Uri unused))
                { // Success
                    hasUrl = true;
                }
                else
                { // Failure
                    throw new InvalidUICommandException($"Invalid URL [{info.URL}]", uiCmd);
                }
            }

            if (hasUrl)
            { // Open URL
                button.Click += (object sender, RoutedEventArgs e) =>
                {
                    Process.Start(info.URL);
                };
            }
            else
            { // Open picture with external viewer
                button.Click += (object sender, RoutedEventArgs e) =>
                {
                    if (ImageHelper.GetImageType(uiCmd.Text, out ImageHelper.ImageType t))
                        return;
                    string path = System.IO.Path.ChangeExtension(System.IO.Path.GetTempFileName(), "." + t.ToString().ToLower());

                    using (MemoryStream ms = EncodedFile.ExtractInterfaceEncoded(uiCmd.Addr.Plugin, uiCmd.Text))
                    using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                    {
                        ms.Position = 0;
                        ms.CopyTo(fs);
                        fs.Close();
                        ms.Close();
                    }
                        
                    ProcessStartInfo procInfo = new ProcessStartInfo()
                    {
                        Verb = "open",
                        FileName = path,
                        UseShellExecute = true
                    };
                    Process.Start(procInfo);
                };
            }

            SetToolTip(button, info.ToolTip);
            DrawToCanvas(r, button, uiCmd.Rect);
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
            Debug.Assert(uiCmd.Info.GetType() == typeof(UIInfo_TextFile));
            UIInfo_TextFile info = uiCmd.Info as UIInfo_TextFile;

            TextBox textBox;
            using (MemoryStream ms = EncodedFile.ExtractInterfaceEncoded(uiCmd.Addr.Plugin, uiCmd.Text))
            using (StreamReader sr = new StreamReader(ms, FileHelper.DetectTextEncoding(ms)))
            {
                textBox = new TextBox()
                {
                    TextWrapping = TextWrapping.Wrap,
                    AcceptsReturn = true,
                    IsReadOnly = true,
                    Text = sr.ReadToEnd(),
                    FontSize = CalcFontPointScale(),
                };
                sr.Close();
                ms.Close();
            }
            
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
        public static void RenderButton(RenderInfo r, UICommand uiCmd, Logger logger)
        {
            Debug.Assert(uiCmd.Info.GetType() == typeof(UIInfo_Button));
            UIInfo_Button info = uiCmd.Info as UIInfo_Button;

            Button button = new Button()
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            button.Click += (object sender, RoutedEventArgs e) =>
            {
                if (r.Plugin.Sections.ContainsKey(info.SectionName)) // Only if section exists
                {
                    SectionAddress addr = new SectionAddress(r.Plugin, r.Plugin.Sections[info.SectionName]);
                    UIRenderer.RunOneSection(addr, $"{r.Plugin.Title} - Button [{uiCmd.Key}]", info.ShowProgress);
                }
                else
                {
                    Application.Current.Dispatcher.Invoke((Action)(() =>
                    {
                        MainWindow w = Application.Current.MainWindow as MainWindow;
                        w.Logger.System_Write(new LogInfo(LogState.Error, $"Section [{info.SectionName}] does not exists"));
                    }));
                }
            };

            if (info.Picture != null && uiCmd.Addr.Plugin.Sections.ContainsKey($"EncodedFile-InterfaceEncoded-{info.Picture}"))
            { // Has Picture

                if (ImageHelper.GetImageType(info.Picture, out ImageHelper.ImageType type))
                    return;

                Image image = new Image()
                {
                    UseLayoutRounding = true,
                    Stretch = Stretch.Uniform,
                };
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
                int margin = 5;

                using (MemoryStream ms = EncodedFile.ExtractInterfaceEncoded(uiCmd.Addr.Plugin, info.Picture))
                {
                    if (type == ImageHelper.ImageType.Svg)
                    {
                        ImageHelper.GetSvgSize(ms, out double width, out double height);
                        if (uiCmd.Rect.Width < uiCmd.Rect.Height)
                        {
                            width = (uiCmd.Rect.Width - margin);
                            height = (uiCmd.Rect.Width - margin) * height / width;
                        }
                        else
                        {
                            width = (uiCmd.Rect.Height - margin) * width / height;
                            height = (uiCmd.Rect.Height - margin);
                        }
                        BitmapImage bitmap = ImageHelper.SvgToBitmapImage(ms, width, height);
                        image.Width = width;
                        image.Height = height;
                        image.Source = bitmap;
                    }
                    else
                    {
                        BitmapImage bitmap = ImageHelper.ImageToBitmapImage(ms);
                        double width, height;
                        if (uiCmd.Rect.Width < uiCmd.Rect.Height)
                        {
                            width = (uiCmd.Rect.Width - margin);
                            height = (uiCmd.Rect.Width - margin) * bitmap.Height / bitmap.Width;
                        }
                        else
                        {
                            width = (uiCmd.Rect.Height - margin) * bitmap.Width / bitmap.Height;
                            height = (uiCmd.Rect.Height - margin);
                        }
                        image.Width = width;
                        image.Height = height;
                        image.Source = bitmap;
                    }

                    ms.Close();
                }

                if (uiCmd.Text.Equals(string.Empty, StringComparison.Ordinal))
                { // No text, just image
                    button.Content = image;
                }
                else
                { // Button has text
                    StackPanel panel = new StackPanel()
                    {
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Orientation = Orientation.Horizontal,
                    };

                    TextBlock text = new TextBlock()
                    {
                        Text = uiCmd.Text,
                        FontSize = CalcFontPointScale(),
                        Height = double.NaN,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(CalcFontPointScale() / 2, 0, 0, 0),
                    };

                    panel.Children.Add(image);
                    panel.Children.Add(text);
                    button.Content = panel;
                }
            }
            else
            { // No picture
                button.Content = uiCmd.Text;
                button.FontSize = CalcFontPointScale();
            }

            SetToolTip(button, info.ToolTip);
            DrawToCanvas(r, button, uiCmd.Rect);
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
            Debug.Assert(uiCmd.Info.GetType() == typeof(UIInfo_WebLabel));
            UIInfo_WebLabel info = uiCmd.Info as UIInfo_WebLabel;

            TextBlock block = new TextBlock()
            {
                TextWrapping = TextWrapping.Wrap,
                FontSize = CalcFontPointScale(),
            };
            Hyperlink hyperLink = new Hyperlink()
            {
                NavigateUri = new Uri(info.URL),
            };
            hyperLink.Inlines.Add(uiCmd.Text);
            hyperLink.RequestNavigate += (object sender, RequestNavigateEventArgs e) =>
            {
                Process.Start(e.Uri.ToString());
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
            Debug.Assert(uiCmd.Info.GetType() == typeof(UIInfo_Bevel));
            UIInfo_Bevel info = uiCmd.Info as UIInfo_Bevel;

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
            Debug.Assert(uiCmd.Info.GetType() == typeof(UIInfo_RadioButton));
            UIInfo_RadioButton info = uiCmd.Info as UIInfo_RadioButton;

            double fontSize = CalcFontPointScale();

            RadioButton radio = new RadioButton()
            {
                GroupName = r.Plugin.FullPath,
                Content = uiCmd.Text,
                FontSize = fontSize,
                IsChecked = info.Selected,
                VerticalContentAlignment = VerticalAlignment.Center,
            };

            if (info.SectionName != null)
            {
                radio.Click += (object sender, RoutedEventArgs e) =>
                {
                    if (r.Plugin.Sections.ContainsKey(info.SectionName)) // Only if section exists
                    {
                        SectionAddress addr = new SectionAddress(r.Plugin, r.Plugin.Sections[info.SectionName]);
                        UIRenderer.RunOneSection(addr, $"{r.Plugin.Title} - CheckBox [{uiCmd.Key}]", info.ShowProgress);
                    }
                    else
                    {
                        Application.Current.Dispatcher.Invoke((Action)(() =>
                        {
                            MainWindow w = Application.Current.MainWindow as MainWindow;
                            w.Logger.System_Write(new LogInfo(LogState.Error, $"Section [{info.SectionName}] does not exists"));
                        }));
                    }
                };
            }

            radio.Checked += (object sender, RoutedEventArgs e) =>
            {
                RadioButton btn = sender as RadioButton;
                info.Selected = true;
                UIRenderer.UpdatePlugin(r.InterfaceSectionName, uiCmd);
            };
            radio.Unchecked += (object sender, RoutedEventArgs e) =>
            {
                RadioButton btn = sender as RadioButton;
                info.Selected = false;
                UIRenderer.UpdatePlugin(r.InterfaceSectionName, uiCmd);
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
        public static void RenderFileBox(RenderInfo r, UICommand uiCmd, Variables variables)
        {
            // It took time to find WB082 textbox control's y coord is of textbox's, not textlabel's.
            Debug.Assert(uiCmd.Info.GetType() == typeof(UIInfo_FileBox));
            UIInfo_FileBox info = uiCmd.Info as UIInfo_FileBox;

            TextBox box = new TextBox()
            {
                Text = uiCmd.Text,
                FontSize = CalcFontPointScale(),
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            box.LostFocus += (object sender, RoutedEventArgs e) =>
            {
                TextBox tBox = sender as TextBox;
                uiCmd.Text = tBox.Text;
                UIRenderer.UpdatePlugin(r.InterfaceSectionName, uiCmd);
            };
            SetToolTip(box, info.ToolTip);

            Button button = new Button()
            {
                FontSize = CalcFontPointScale(),
                Content = ImageHelper.GetMaterialIcon(PackIconMaterialKind.FolderUpload, 0),
            };
            SetToolTip(button, info.ToolTip);

            button.Click += (object sender, RoutedEventArgs e) =>
            {
                Button bt = sender as Button;

                if (info.IsFile)
                {
                    Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog()
                    { 
                        Filter = "All Files|*.*",
                        InitialDirectory = System.IO.Path.GetDirectoryName(StringEscaper.Preprocess(variables, uiCmd.Text)),
                    };
                    if (dialog.ShowDialog() == true)
                    {
                        box.Text = dialog.FileName;
                    }
                }
                else
                {
                    System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog()
                    {
                        ShowNewFolderButton = true,
                        SelectedPath = StringEscaper.Preprocess(variables, uiCmd.Text),   
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
            Debug.Assert(uiCmd.Info.GetType() == typeof(UIInfo_RadioGroup));
            UIInfo_RadioGroup info = uiCmd.Info as UIInfo_RadioGroup;

            double fontSize = CalcFontPointScale();

            GroupBox box = new GroupBox()
            {
                Header = uiCmd.Text,
                FontSize = fontSize,
                BorderBrush = Brushes.LightGray,
            };
            SetToolTip(box, info.ToolTip);

            StackPanel panel = new StackPanel()
            {
                Orientation = Orientation.Vertical,
            };
            box.Content = panel;

            Thickness margin = new Thickness(0, 0, 0, fontSize * 0.4);
            for (int i = 0; i < info.Items.Count; i++)
            {
                RadioButton radio = new RadioButton()
                {
                    GroupName = r.Plugin.FullPath + uiCmd.Key,
                    Content = info.Items[i],
                    Tag = i,
                    FontSize = fontSize,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Margin = margin,
                };

                radio.IsChecked = (i == info.Selected);
                radio.Checked += (object sender, RoutedEventArgs e) =>
                {
                    RadioButton btn = sender as RadioButton;
                    info.Selected = (int)btn.Tag;
                    UIRenderer.UpdatePlugin(r.InterfaceSectionName, uiCmd);
                };

                SetToolTip(radio, info.ToolTip);

                panel.Children.Add(radio);
            }

            Rect rect = new Rect(uiCmd.Rect.Left, uiCmd.Rect.Top, uiCmd.Rect.Width, uiCmd.Rect.Height);
            DrawToCanvas(r, box, rect);
        }
        #endregion

        #region Utility
        private static void InitCanvas(Canvas canvas)
        {
            canvas.Width = double.NaN;
            canvas.Height = double.NaN;
        }

        private static void DrawToCanvas(RenderInfo r, FrameworkElement element, Rect coord)
        {
            Canvas.SetLeft(element, coord.Left);
            Canvas.SetTop(element, coord.Top);
            element.Width = coord.Width;
            element.Height = coord.Height;
            
            r.Canvas.Children.Add(element);
            if (double.IsNaN(r.Canvas.Width) || r.Canvas.Width < coord.Left + coord.Width)
                r.Canvas.Width = coord.Left + coord.Width;
            if (double.IsNaN(r.Canvas.Height) || r.Canvas.Height < coord.Top + coord.Height)
                r.Canvas.Height = coord.Top + coord.Height;
        }

        private static void SetToolTip(FrameworkElement element, string toolTip)
        {
            if (toolTip != null)
                element.ToolTip = toolTip;
        }

        private static double CalcFontPointScale(double fontPoint = DefaultFontPoint) 
        {
            return fontPoint * PointToDeviceIndependentPixel;
        }

        public static void UpdatePlugin(string interfaceSectionName, UICommand uiCmd)
        {
            Ini.SetKey(uiCmd.Addr.Plugin.FullPath, new IniKey(interfaceSectionName, uiCmd.Key, uiCmd.ForgeRawLine(false)));
        }

        public static void UpdatePlugin(string interfaceSectionName, List<UICommand> uiCmdList)
        {
            List<IniKey> keys = new List<IniKey>();
            foreach (UICommand uiCmd in uiCmdList)
                keys.Add(new IniKey(interfaceSectionName, uiCmd.Key, uiCmd.ForgeRawLine(false)));

            Ini.SetKeys(uiCmdList[0].Addr.Plugin.FullPath, keys);
        }

        private static async void RunOneSection(SectionAddress addr, string logMsg, bool showProgress)
        {
            if (Engine.WorkingLock == 0)
            {
                Interlocked.Increment(ref Engine.WorkingLock);

                Logger logger = App.Logger;
                SettingViewModel setting = App.Setting;
                MainViewModel mainModel = null;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MainWindow w = Application.Current.MainWindow as MainWindow;

                    mainModel = w.Model;

                    // Populate BuildTree
                    w.Model.BuildTree.Children.Clear();
                    w.PopulateOneTreeView(addr.Plugin, w.Model.BuildTree, w.Model.BuildTree);
                    w.CurBuildTree = null;
                });

                mainModel.ProgressRingActive = true;

                EngineState s = new EngineState(addr.Plugin.Project, logger, mainModel, addr.Plugin, addr.Section.SectionName);
                s.SetLogOption(setting);

                Engine.WorkingEngine = new Engine(s);

                // Build Start, Switch to Build View
                if (showProgress)
                    mainModel.SwitchNormalBuildInterface = false;

                // Run
                long buildId = await Engine.WorkingEngine.Run(logMsg);

                // Build Ended, Switch to Normal View
                if (showProgress)
                    mainModel.SwitchNormalBuildInterface = true;

                // Turn off ProgressRing
                mainModel.ProgressRingActive = false;

                Engine.WorkingEngine = null;
                Interlocked.Decrement(ref Engine.WorkingLock);

                if (showProgress)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MainWindow w = Application.Current.MainWindow as MainWindow;
                        w.DrawPlugin(addr.Plugin);
                    });
                }
            }
        }
        #endregion
    }

    #region RenderInfo
    public struct RenderInfo
    {
        public readonly double MasterScale;
        public readonly Canvas Canvas;
        public readonly MainWindow Window;
        public readonly Plugin Plugin;
        public readonly string InterfaceSectionName;
        public readonly Logger Logger;

        public RenderInfo(Canvas canvas, MainWindow window, Logger logger, Plugin plugin, string interfaceSectionName, double masterScale)
        {
            Canvas = canvas;
            Window = window;
            Logger = logger;
            Plugin = plugin;
            InterfaceSectionName = interfaceSectionName;
            MasterScale = masterScale;
        }
    }
    #endregion
}
