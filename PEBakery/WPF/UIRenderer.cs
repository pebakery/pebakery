/*
    Copyright (C) 2016-2018 Hajin Jang
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

    Additional permission under GNU GPL version 3 section 7

    If you modify this program, or any covered work, by linking
    or combining it with external libraries, containing parts
    covered by the terms of various license, the licensors of
    this program grant you additional permission to convey the
    resulting work. An external library is a library which is
    not derived from or based on this program. 
*/

using PEBakery.Core;
using PEBakery.Helper;
using PEBakery.Exceptions;
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
using System.Diagnostics.CodeAnalysis;
using MahApps.Metro.IconPacks;
using System.Windows.Threading;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using Ookii.Dialogs.Wpf;

namespace PEBakery.WPF
{
    // TODO: Fix potential memory leak due to event handler
    #region UIRenderer
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class UIRenderer
    {
        #region Fields
        public const int DefaultFontPoint = 8; // WB082 hard-coded default font point to 8.
        public const double PointToDeviceIndependentPixel = 96f / 72f; // Point - 72DPI, Device Independent Pixel - 96DPI
        public const int MaxDpiScale = 4;
        public const int MaxUrlDisplayLen = 47;

        // Compatibility Option
        public static bool IgnoreWidthOfWebLabel = false;
        public static bool DisableBevelCaption = false;

        private readonly RenderInfo _renderInfo;
        private readonly List<UIControl> _uiCtrls;
        private readonly Variables _variables;
        private readonly Logger _logger;
        #endregion

        #region Constructor
        public UIRenderer(Canvas canvas, MainWindow window, Script script, Logger logger, double scale)
        {
            _logger = logger;
            _variables = script.Project.Variables;

            // Check if script has custom interface section
            string interfaceSectionName = "Interface";
            if (script.MainInfo.ContainsKey("Interface")) 
                interfaceSectionName = script.MainInfo["Interface"];

            _renderInfo = new RenderInfo(canvas, window, logger, script, interfaceSectionName, scale);

            if (script.Sections.ContainsKey(interfaceSectionName))
            {
                try
                {
                    _uiCtrls = script.Sections[interfaceSectionName].GetUICtrls(true).Where(x => x.Visibility).ToList();
                    logger.System_Write(script.Sections[interfaceSectionName].LogInfos);
                }
                catch
                {
                    _uiCtrls = null;
                    logger.System_Write(new LogInfo(LogState.Error, $"Cannot read interface controls from [{script.TreePath}]"));
                }
            }
            else
            {
                _uiCtrls = null;
                logger.System_Write(new LogInfo(LogState.Error, $"Cannot read interface controls from [{script.TreePath}]"));
            }
        }
        #endregion

        #region Render All
        public void Render()
        {
            if (_uiCtrls == null) // This script does not have 'Interface' section
                return;

            InitCanvas(_renderInfo.Canvas);
            UIControl[] radioButtons = _uiCtrls.Where(x => x.Type == UIControlType.RadioButton).ToArray();
            foreach (UIControl uiCmd in _uiCtrls)
            {
                try
                {
                    switch (uiCmd.Type)
                    {
                        case UIControlType.TextBox:
                            UIRenderer.RenderTextBox(_renderInfo, uiCmd);
                            break;
                        case UIControlType.TextLabel:
                            UIRenderer.RenderTextLabel(_renderInfo, uiCmd);
                            break;
                        case UIControlType.NumberBox:
                            UIRenderer.RenderNumberBox(_renderInfo, uiCmd);
                            break;
                        case UIControlType.CheckBox:
                            UIRenderer.RenderCheckBox(_renderInfo, uiCmd);
                            break;
                        case UIControlType.ComboBox:
                            UIRenderer.RenderComboBox(_renderInfo, uiCmd);
                            break;
                        case UIControlType.Image:
                            UIRenderer.RenderImage(_renderInfo, uiCmd);
                            break;
                        case UIControlType.TextFile:
                            UIRenderer.RenderTextFile(_renderInfo, uiCmd);
                            break;
                        case UIControlType.Button:
                            UIRenderer.RenderButton(_renderInfo, uiCmd, _logger);
                            break;
                        case UIControlType.WebLabel:
                            UIRenderer.RenderWebLabel(_renderInfo, uiCmd);
                            break;
                        case UIControlType.RadioButton:
                            UIRenderer.RenderRadioButton(_renderInfo, uiCmd, radioButtons);
                            break;
                        case UIControlType.Bevel:
                            UIRenderer.RenderBevel(_renderInfo, uiCmd);
                            break;
                        case UIControlType.FileBox:
                            UIRenderer.RenderFileBox(_renderInfo, uiCmd, _variables);
                            break;
                        case UIControlType.RadioGroup:
                            UIRenderer.RenderRadioGroup(_renderInfo, uiCmd);
                            break;
                        default:
                            _logger.System_Write(new LogInfo(LogState.Error, $"Unable to render [{uiCmd.RawLine}]"));
                            break;
                    }
                }
                catch (Exception e)
                { // Log failure
                    _logger.System_Write(new LogInfo(LogState.Error, $"{Logger.LogExceptionMessage(e)} [{uiCmd.RawLine}]"));
                }
            }
        }
        #endregion

        #region Render Each Control
        /// <summary>
        /// Render TextBox control.
        /// Return true if failed.
        /// </summary>
        /// <returns>Success = false, Failure = true</returns>
        public static void RenderTextBox(RenderInfo r, UIControl uiCtrl)
        {
            // WB082 textbox control's y coord is of textbox's, not textlabel's.
            Debug.Assert(uiCtrl.Info.GetType() == typeof(UIInfo_TextBox), "Invalid UIInfo");
            UIInfo_TextBox info = uiCtrl.Info as UIInfo_TextBox;
            Debug.Assert(info != null, "Invalid UIInfo");

            TextBox box = new TextBox
            {
                Text = info.Value,
                FontSize = CalcFontPointScale(),
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            box.LostFocus += (object sender, RoutedEventArgs e) =>
            {
                TextBox tBox = sender as TextBox;
                Debug.Assert(tBox != null);

                info.Value = tBox.Text;
                uiCtrl.Update();
            };
            SetToolTip(box, info.ToolTip);
            DrawToCanvas(r, box, uiCtrl.Rect);

            if (uiCtrl.Text.Equals(string.Empty, StringComparison.Ordinal) == false)
            {
                TextBlock block = new TextBlock()
                {
                    Text = uiCtrl.Text,
                    LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                    LineHeight = CalcFontPointScale(),
                    FontSize = CalcFontPointScale(),
                };
                SetToolTip(block, info.ToolTip);
                const double margin = PointToDeviceIndependentPixel * DefaultFontPoint * 1.2;
                Rect blockRect = new Rect(uiCtrl.Rect.Left, uiCtrl.Rect.Top - margin, uiCtrl.Rect.Width, uiCtrl.Rect.Height);
                DrawToCanvas(r, block, blockRect);
            }
        }

        /// <summary>
        /// Render TextLabel control.
        /// Return true if failed.
        /// </summary>
        /// <returns>Success = false, Failure = true</returns>
        public static void RenderTextLabel(RenderInfo r, UIControl uiCtrl)
        {
            Debug.Assert(uiCtrl.Info.GetType() == typeof(UIInfo_TextLabel), "Invalid UIInfo");
            UIInfo_TextLabel info = uiCtrl.Info as UIInfo_TextLabel;
            Debug.Assert(info != null, "Invalid UIInfo");

            TextBlock block = new TextBlock
            {
                Text = uiCtrl.Text,
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
            DrawToCanvas(r, block, uiCtrl.Rect);
        }

        /// <summary>
        /// Render NumberBox control.
        /// Return true if failed.
        /// </summary>
        /// <returns>Success = false, Failure = true</returns>
        public static void RenderNumberBox(RenderInfo r, UIControl uiCtrl)
        {
            Debug.Assert(uiCtrl.Info.GetType() == typeof(UIInfo_NumberBox), "Invalid UIInfo");
            UIInfo_NumberBox info = uiCtrl.Info as UIInfo_NumberBox;
            Debug.Assert(info != null, "Invalid UIInfo");

            FreeNumberBox box = new FreeNumberBox
            {
                Value = info.Value,
                FontSize = CalcFontPointScale(),
                Minimum = info.Min,
                Maximum = info.Max,
                DecimalPlaces = 0,
                IncrementUnit = info.Interval,
                VerticalContentAlignment = VerticalAlignment.Center,
            };

            box.ValueChanged += (object sender, RoutedPropertyChangedEventArgs<decimal> e) =>
            {
                info.Value = (int)e.NewValue;
                uiCtrl.Update();
            };

            SetToolTip(box, info.ToolTip);
            DrawToCanvas(r, box, uiCtrl.Rect);
        }

        /// <summary>
        /// Render CheckBox control.
        /// Return true if failed.
        /// </summary>
        /// <returns>Success = false, Failure = true</returns>
        public static void RenderCheckBox(RenderInfo r, UIControl uiCtrl)
        {
            Debug.Assert(uiCtrl.Info.GetType() == typeof(UIInfo_CheckBox), "Invalid UIInfo");
            UIInfo_CheckBox info = uiCtrl.Info as UIInfo_CheckBox;
            Debug.Assert(info != null, "Invalid UIInfo");

            CheckBox checkBox = new CheckBox
            {
                Content = uiCtrl.Text,
                IsChecked = info.Value,
                FontSize = CalcFontPointScale(),
                VerticalContentAlignment = VerticalAlignment.Center,
            };

            if (info.SectionName != null)
            {
                checkBox.Click += (object sender, RoutedEventArgs e) =>
                {
                    if (r.Script.Sections.ContainsKey(info.SectionName)) // Only if section exists
                    {
                        SectionAddress addr = new SectionAddress(r.Script, r.Script.Sections[info.SectionName]);
                        UIRenderer.RunOneSection(addr, $"{r.Script.Title} - CheckBox [{uiCtrl.Key}]", info.HideProgress);
                    }
                    else
                    {
                        r.Logger.System_Write(new LogInfo(LogState.Error, $"Section [{info.SectionName}] does not exists"));
                    }
                };
            }
            
            checkBox.Checked += (object sender, RoutedEventArgs e) =>
            {
                info.Value = true;
                uiCtrl.Update();
            };
            checkBox.Unchecked += (object sender, RoutedEventArgs e) =>
            {
                info.Value = false;
                uiCtrl.Update();
            };

            SetToolTip(checkBox, info.ToolTip);
            DrawToCanvas(r, checkBox, uiCtrl.Rect);
        }

        /// <summary>
        /// Render ComboBox control.
        /// Return true if failed.
        /// </summary>
        /// <returns>Success = false, Failure = true</returns>
        public static void RenderComboBox(RenderInfo r, UIControl uiCtrl)
        {
            Debug.Assert(uiCtrl.Info.GetType() == typeof(UIInfo_ComboBox), "Invalid UIInfo");
            UIInfo_ComboBox info = uiCtrl.Info as UIInfo_ComboBox;
            Debug.Assert(info != null, "Invalid UIInfo");

            ComboBox comboBox = new ComboBox
            {
                FontSize = CalcFontPointScale(),
                ItemsSource = info.Items,
                SelectedIndex = info.Index,
                VerticalContentAlignment = VerticalAlignment.Center,
            };

            comboBox.LostFocus += (object sender, RoutedEventArgs e) =>
            {
                ComboBox box = sender as ComboBox;
                Debug.Assert(box != null);

                if (info.Index != box.SelectedIndex)
                {
                    info.Index = box.SelectedIndex;
                    uiCtrl.Text = info.Items[box.SelectedIndex];
                    uiCtrl.Update();
                }
            };

            if (info.SectionName != null)
            {
                comboBox.SelectionChanged += (object sender, SelectionChangedEventArgs e) =>
                {
                    if (r.Script.Sections.ContainsKey(info.SectionName)) // Only if section exists
                    {
                        SectionAddress addr = new SectionAddress(r.Script, r.Script.Sections[info.SectionName]);
                        UIRenderer.RunOneSection(addr, $"{r.Script.Title} - CheckBox [{uiCtrl.Key}]", info.HideProgress);
                    }
                    else
                    {
                        r.Logger.System_Write(new LogInfo(LogState.Error, $"Section [{info.SectionName}] does not exists"));
                    }
                };
            }

            SetToolTip(comboBox, info.ToolTip);
            DrawToCanvas(r, comboBox, uiCtrl.Rect);
        }

        /// <summary>
        /// Render Image control.
        /// Return true if failed.
        /// </summary>
        /// <returns>Success = false, Failure = true</returns>
        public static void RenderImage(RenderInfo r, UIControl uiCtrl)
        {
            Debug.Assert(uiCtrl.Info.GetType() == typeof(UIInfo_Image), "Invalid UIInfo");
            UIInfo_Image info = uiCtrl.Info as UIInfo_Image;
            Debug.Assert(info != null, "Invalid UIInfo");

            Image image = new Image
            {
                StretchDirection = StretchDirection.DownOnly,
                Stretch = Stretch.Uniform,
                UseLayoutRounding = true,
            };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            Button button;

            using (MemoryStream ms = EncodedFile.ExtractInterfaceEncoded(uiCtrl.Addr.Script, uiCtrl.Text))
            {
                if (!ImageHelper.GetImageType(uiCtrl.Text, out ImageHelper.ImageType type))
                    return;

                button = new Button
                {
                    Style = (Style)r.Window.FindResource("ImageButton")
                };

                if (type == ImageHelper.ImageType.Svg)
                {
                    double width = uiCtrl.Rect.Width * r.MasterScale;
                    double height = uiCtrl.Rect.Height * r.MasterScale;
                    button.Background = ImageHelper.SvgToImageBrush(ms, width, height);
                }
                else
                {
                    button.Background = ImageHelper.ImageToImageBrush(ms);
                }
            }
                
            bool hasUrl = false;
            if (!string.IsNullOrEmpty(info.URL))
            {
                if (Uri.TryCreate(info.URL, UriKind.Absolute, out Uri _)) // Success
                    hasUrl = true;
                else // Failure
                    throw new InvalidUIControlException($"Invalid URL [{info.URL}]", uiCtrl);
            }

            string toolTip = info.ToolTip;
            if (hasUrl)
            { // Open URL
                button.Click += (object sender, RoutedEventArgs e) =>
                {
                    Process.Start(info.URL);
                };

                toolTip = UIRenderer.AppendUrlToToolTip(info.ToolTip, info.URL);
            }
            else
            { // Open picture with external viewer
                button.Click += (object sender, RoutedEventArgs e) =>
                {
                    if (ImageHelper.GetImageType(uiCtrl.Text, out ImageHelper.ImageType t))
                        return;
                    string path = Path.ChangeExtension(Path.GetTempFileName(), "." + t.ToString().ToLower());

                    using (MemoryStream ms = EncodedFile.ExtractInterfaceEncoded(uiCtrl.Addr.Script, uiCtrl.Text))
                    using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                    {
                        ms.Position = 0;
                        ms.CopyTo(fs);
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

            SetToolTip(button, toolTip);
            DrawToCanvas(r, button, uiCtrl.Rect);
        }

        /// <summary>
        /// Render TextFile control.
        /// Return true if failed.
        /// </summary>
        /// <returns>Success = false, Failure = true</returns>
        public static void RenderTextFile(RenderInfo r, UIControl uiCtrl)
        {
            Debug.Assert(uiCtrl.Info.GetType() == typeof(UIInfo_TextFile), "Invalid UIInfo");
            UIInfo_TextFile info = uiCtrl.Info as UIInfo_TextFile;
            Debug.Assert(info != null, "Invalid UIInfo");

            TextBox textBox;
            using (MemoryStream ms = EncodedFile.ExtractInterfaceEncoded(uiCtrl.Addr.Script, uiCtrl.Text))
            using (StreamReader sr = new StreamReader(ms, FileHelper.DetectTextEncoding(ms)))
            {
                textBox = new TextBox
                {
                    TextWrapping = TextWrapping.Wrap,
                    AcceptsReturn = true,
                    IsReadOnly = true,
                    Text = sr.ReadToEnd(),
                    FontSize = CalcFontPointScale(),
                };
            }
            
            ScrollViewer.SetHorizontalScrollBarVisibility(textBox, ScrollBarVisibility.Auto);
            ScrollViewer.SetVerticalScrollBarVisibility(textBox, ScrollBarVisibility.Auto);
            ScrollViewer.SetCanContentScroll(textBox, true);

            SetToolTip(textBox, info.ToolTip);
            DrawToCanvas(r, textBox, uiCtrl.Rect);
        }

        /// <summary>
        /// Render Button control.
        /// Return true if failed.
        /// </summary>
        /// <returns>Success = false, Failure = true</returns>
        public static void RenderButton(RenderInfo r, UIControl uiCtrl, Logger logger)
        {
            Debug.Assert(uiCtrl.Info.GetType() == typeof(UIInfo_Button), "Invalid UIInfo");
            UIInfo_Button info = uiCtrl.Info as UIInfo_Button;
            Debug.Assert(info != null, "Invalid UIInfo");

            Button button = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            button.Click += (object sender, RoutedEventArgs e) =>
            {
                if (r.Script.Sections.ContainsKey(info.SectionName)) // Only if section exists
                {
                    SectionAddress addr = new SectionAddress(r.Script, r.Script.Sections[info.SectionName]);
                    UIRenderer.RunOneSection(addr, $"{r.Script.Title} - Button [{uiCtrl.Key}]", info.ShowProgress);
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MainWindow w = Application.Current.MainWindow as MainWindow;
                        w?.Logger.System_Write(new LogInfo(LogState.Error, $"Section [{info.SectionName}] does not exists"));
                    });
                }
            };

            if (info.Picture != null && uiCtrl.Addr.Script.Sections.ContainsKey($"EncodedFile-InterfaceEncoded-{info.Picture}"))
            { // Has Picture
                if (!ImageHelper.GetImageType(info.Picture, out ImageHelper.ImageType type))
                    return;

                Image image = new Image
                {
                    StretchDirection = StretchDirection.DownOnly,
                    Stretch = Stretch.Uniform,
                    UseLayoutRounding = true,
                };
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

                using (MemoryStream ms = EncodedFile.ExtractInterfaceEncoded(uiCtrl.Addr.Script, info.Picture))
                {
                    if (type == ImageHelper.ImageType.Svg)
                        image.Source = ImageHelper.SvgToBitmapImage(ms);
                    else
                        image.Source = ImageHelper.ImageToBitmapImage(ms);
                }

                if (uiCtrl.Text.Equals(string.Empty, StringComparison.Ordinal))
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
                        Text = uiCtrl.Text,
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
                button.Content = uiCtrl.Text;
                button.FontSize = CalcFontPointScale();
            }

            SetToolTip(button, info.ToolTip);
            DrawToCanvas(r, button, uiCtrl.Rect);
        }

        /// <summary>
        /// Render WebLabel control.
        /// Return true if failed.
        /// </summary>
        /// <returns>Success = false, Failure = true</returns>
        public static void RenderWebLabel(RenderInfo r, UIControl uiCtrl)
        {
            Debug.Assert(uiCtrl.Info.GetType() == typeof(UIInfo_WebLabel), "Invalid UIInfo");
            UIInfo_WebLabel info = uiCtrl.Info as UIInfo_WebLabel;
            Debug.Assert(info != null, "Invalid UIInfo");

            TextBlock block = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontSize = CalcFontPointScale(),
            };

            Hyperlink hyperLink = new Hyperlink
            {
                NavigateUri = new Uri(info.URL),
            };
            hyperLink.Inlines.Add(uiCtrl.Text);
            hyperLink.RequestNavigate += (object sender, RequestNavigateEventArgs e) =>
            {
                Process.Start(e.Uri.ToString());
            };
            block.Inlines.Add(hyperLink);

            string toolTip = UIRenderer.AppendUrlToToolTip(info.ToolTip, info.URL);
            SetToolTip(block, toolTip);

            if (IgnoreWidthOfWebLabel)
            {
                Rect rect = uiCtrl.Rect;
                rect.Width = block.Width;
                DrawToCanvas(r, block, rect);
            }
            else
            {
                DrawToCanvas(r, block, uiCtrl.Rect);
            }
        }

        /// <summary>
        /// Render Bevel control.
        /// Return true if failed.
        /// </summary>
        /// <returns>Success = false, Failure = true</returns>
        public static void RenderBevel(RenderInfo r, UIControl uiCtrl)
        {
            Debug.Assert(uiCtrl.Info.GetType() == typeof(UIInfo_Bevel), "Invalid UIInfo");
            UIInfo_Bevel info = uiCtrl.Info as UIInfo_Bevel;
            Debug.Assert(info != null, "Invalid UIInfo");

            Border bevel = new Border
            {
                IsHitTestVisible = false,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.Gray,
                ToolTip = info.ToolTip,
            };
            DrawToCanvas(r, bevel, uiCtrl.Rect);

            if (DisableBevelCaption == false &&
                !uiCtrl.Text.Equals(uiCtrl.Key, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(uiCtrl.Text))
            { // PEBakery Extension - see https://github.com/pebakery/pebakery/issues/34
                int fontSize = DefaultFontPoint;
                if (info.FontSize != null)
                    fontSize = (int) info.FontSize;

                Border textBorder = new Border
                {
                    // Don't use info.FontSize for border thickness. It throws off X Pos.
                    BorderThickness = new Thickness(CalcFontPointScale() / 3), 
                    BorderBrush = Brushes.Transparent,
                };
                TextBlock textBlock = new TextBlock
                {
                    Text = uiCtrl.Text,
                    FontSize = CalcFontPointScale(fontSize),
                    Padding = new Thickness(CalcFontPointScale(fontSize) / 3, 0, CalcFontPointScale(fontSize) / 3, 0),
                    Background = Brushes.White,
                };
                textBorder.Child = textBlock;
                if (info.Style == UIInfo_BevelCaption_Style.Bold)
                    textBlock.FontWeight = FontWeights.Bold;

                Rect blockRect = new Rect
                {
                    X = uiCtrl.Rect.X + CalcFontPointScale(fontSize) / 3,
                    Y = uiCtrl.Rect.Y - CalcFontPointScale(fontSize),
                    Width = double.NaN,
                    Height = double.NaN,
                };
                DrawToCanvas(r, textBorder, blockRect);
            }
        }

        /// <summary>
        /// Render RadioGroup control.
        /// Return true if failed.
        /// </summary>
        /// <returns>Success = false, Failure = true</returns>
        public static void RenderRadioButton(RenderInfo r, UIControl uiCtrl, UIControl[] radioButtons)
        {
            Debug.Assert(uiCtrl.Info.GetType() == typeof(UIInfo_RadioButton), "Invalid UIInfo");
            UIInfo_RadioButton info = uiCtrl.Info as UIInfo_RadioButton;
            Debug.Assert(info != null, "Invalid UIInfo");

            double fontSize = CalcFontPointScale();

            RadioButton radio = new RadioButton
            {
                GroupName = r.Script.RealPath,
                Content = uiCtrl.Text,
                FontSize = fontSize,
                IsChecked = info.Selected,
                VerticalContentAlignment = VerticalAlignment.Center,
            };

            if (info.SectionName != null)
            {
                radio.Click += (object sender, RoutedEventArgs e) =>
                {
                    if (r.Script.Sections.ContainsKey(info.SectionName)) // Only if section exists
                    {
                        SectionAddress addr = new SectionAddress(r.Script, r.Script.Sections[info.SectionName]);
                        UIRenderer.RunOneSection(addr, $"{r.Script.Title} - RadioButton [{uiCtrl.Key}]", info.HideProgress);
                    }
                    else
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MainWindow w = Application.Current.MainWindow as MainWindow;
                            w?.Logger.System_Write(new LogInfo(LogState.Error, $"Section [{info.SectionName}] does not exists"));
                        });
                    }
                };
            }

            radio.Checked += (object sender, RoutedEventArgs e) =>
            {
                // RadioButton btn = sender as RadioButton;
                info.Selected = true;

                // Uncheck the other RadioButtons
                List<UIControl> updateList = radioButtons.Where(x => !x.Key.Equals(uiCtrl.Key, StringComparison.Ordinal)).ToList();
                foreach (UIControl uncheck in updateList)
                {
                    Debug.Assert(uncheck.Info.GetType() == typeof(UIInfo_RadioButton), "Invalid UIInfo");
                    UIInfo_RadioButton unInfo = uncheck.Info as UIInfo_RadioButton;
                    Debug.Assert(unInfo != null, "Invalid UIInfo");

                    unInfo.Selected = false;
                }

                updateList.Add(uiCtrl);
                UIControl.Update(updateList);
            };

            SetToolTip(radio, info.ToolTip);
            DrawToCanvas(r, radio, uiCtrl.Rect);
        }

        /// <summary>
        /// Render FileBox control.
        /// Return true if failed.
        /// </summary>
        /// <returns>Success = false, Failure = true</returns>
        public static void RenderFileBox(RenderInfo r, UIControl uiCtrl, Variables variables)
        {
            // It took time to find WB082 textbox control's y coord is of textbox's, not textlabel's.
            Debug.Assert(uiCtrl.Info.GetType() == typeof(UIInfo_FileBox), "Invalid UIInfo");
            UIInfo_FileBox info = uiCtrl.Info as UIInfo_FileBox;
            Debug.Assert(info != null, "Invalid UIInfo");

            TextBox box = new TextBox
            {
                Text = uiCtrl.Text,
                FontSize = CalcFontPointScale(),
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            box.TextChanged += (object sender, TextChangedEventArgs e) =>
            {
                TextBox tBox = sender as TextBox;
                Debug.Assert(tBox != null);

                uiCtrl.Text = tBox.Text;
                uiCtrl.Update();
            };
            SetToolTip(box, info.ToolTip);

            Button button = new Button
            {
                FontSize = CalcFontPointScale(),
                Content = ImageHelper.GetMaterialIcon(PackIconMaterialKind.FolderOpen, 0),
            };
            SetToolTip(button, info.ToolTip);

            button.Click += (object sender, RoutedEventArgs e) =>
            {
                // Button bt = sender as Button;
                
                if (info.IsFile)
                { // File
                    string currentPath = StringEscaper.Preprocess(variables, uiCtrl.Text);
                    if (File.Exists(currentPath))
                        currentPath = Path.GetDirectoryName(currentPath);
                    else
                        currentPath = string.Empty;
                    Debug.Assert(currentPath != null);
                    
                    Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog
                    { 
                        Filter = "All Files|*.*",
                        InitialDirectory = currentPath,
                    };
                    if (dialog.ShowDialog() == true)
                    {
                        box.Text = dialog.FileName;
                    }
                }
                else
                { // Directory
                    VistaFolderBrowserDialog dialog = new VistaFolderBrowserDialog();

                    string currentPath = StringEscaper.Preprocess(variables, uiCtrl.Text);
                    if (Directory.Exists(currentPath))
                        dialog.SelectedPath = currentPath;

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (dialog.ShowDialog(r.Window) == true)
                        {
                            box.Text = dialog.SelectedPath;
                            if (!dialog.SelectedPath.EndsWith("\\", StringComparison.Ordinal))
                                box.Text += "\\";
                        }
                    });
                }
            };

            double margin = 5;
            Rect boxRect = new Rect(uiCtrl.Rect.Left, uiCtrl.Rect.Top, uiCtrl.Rect.Width - (uiCtrl.Rect.Height + margin), uiCtrl.Rect.Height);
            Rect btnRect = new Rect(boxRect.Right + margin, uiCtrl.Rect.Top, uiCtrl.Rect.Height, uiCtrl.Rect.Height);
            DrawToCanvas(r, box, boxRect);
            DrawToCanvas(r, button, btnRect);
        }

        /// <summary>
        /// Render RadioGroup control.
        /// Return true if failed.
        /// </summary>
        /// <returns>Success = false, Failure = true</returns>
        public static void RenderRadioGroup(RenderInfo r, UIControl uiCtrl)
        {
            Debug.Assert(uiCtrl.Info.GetType() == typeof(UIInfo_RadioGroup), "Invalid UIInfo");
            UIInfo_RadioGroup info = uiCtrl.Info as UIInfo_RadioGroup;
            Debug.Assert(info != null, "Invalid UIInfo");

            double fontSize = CalcFontPointScale();

            GroupBox box = new GroupBox
            {
                Header = uiCtrl.Text,
                FontSize = fontSize,
                BorderBrush = Brushes.LightGray,
            };
            SetToolTip(box, info.ToolTip);

            Grid grid = new Grid();
            box.Content = grid;

            for (int i = 0; i < info.Items.Count; i++)
            {
                RadioButton radio = new RadioButton
                {
                    GroupName = r.Script.RealPath + uiCtrl.Key,
                    Content = info.Items[i],
                    Tag = i,
                    FontSize = fontSize,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    IsChecked = i == info.Selected,
                };

                radio.Checked += (object sender, RoutedEventArgs e) =>
                {
                    RadioButton btn = sender as RadioButton;
                    Debug.Assert(btn != null);

                    info.Selected = (int)btn.Tag;
                    uiCtrl.Update();
                };

                if (info.SectionName != null)
                {
                    radio.Click += (object sender, RoutedEventArgs e) =>
                    {
                        if (r.Script.Sections.ContainsKey(info.SectionName)) // Only if section exists
                        {
                            SectionAddress addr = new SectionAddress(r.Script, r.Script.Sections[info.SectionName]);
                            UIRenderer.RunOneSection(addr, $"{r.Script.Title} - RadioGroup [{uiCtrl.Key}]", info.HideProgress);
                        }
                        else
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                MainWindow w = Application.Current.MainWindow as MainWindow;
                                w?.Logger.System_Write(new LogInfo(LogState.Error, $"Section [{info.SectionName}] does not exists"));
                            });
                        }
                    };
                }

                SetToolTip(radio, info.ToolTip);

                Grid.SetRow(radio, i);
                grid.RowDefinitions.Add(new RowDefinition());
                grid.Children.Add(radio);
            }

            Rect rect = new Rect(uiCtrl.Rect.Left, uiCtrl.Rect.Top, uiCtrl.Rect.Width, uiCtrl.Rect.Height);
            DrawToCanvas(r, box, rect);
        }
        #endregion

        #region Utility
        private static void InitCanvas(FrameworkElement canvas)
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

        private static string AppendUrlToToolTip(string toolTip, string url)
        {
            if (url == null)
            {
                return toolTip;
            }
            else
            {
                if (MaxUrlDisplayLen < url.Length)
                    url = url.Substring(0, MaxUrlDisplayLen) + "...";

                if (toolTip == null)
                    return url;
                else
                    return toolTip + Environment.NewLine + Environment.NewLine + url;
            }
        }

        private static async void RunOneSection(SectionAddress addr, string logMsg, bool hideProgress)
        {
            if (Engine.WorkingLock == 0)
            {
                Interlocked.Increment(ref Engine.WorkingLock);

                Logger logger = null;
                SettingViewModel setting = null;
                MainViewModel mainModel = null;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (Application.Current.MainWindow is MainWindow w)
                    {
                        logger = w.Logger;
                        mainModel = w.Model;
                        setting = w.Setting;

                        // Populate BuildTree
                        if (!hideProgress)
                        {
                            w.Model.BuildTree.Children.Clear();
                            w.PopulateOneTreeView(addr.Script, w.Model.BuildTree, w.Model.BuildTree);
                            w.CurBuildTree = null;
                        }
                    }
                });

                mainModel.WorkInProgress = true;

                EngineState s = new EngineState(addr.Script.Project, logger, mainModel, EngineMode.RunMainAndOne, addr.Script, addr.Section.Name);
                s.SetOption(setting);
                s.DisableLogger = !setting.Log_InterfaceButton;

                Engine.WorkingEngine = new Engine(s);

                // Build Start, Switch to Build View
                if (!hideProgress)
                    mainModel.SwitchNormalBuildInterface = false;

                // Run
                // long buildId = await Engine.WorkingEngine.Run(logMsg);
                await Engine.WorkingEngine.Run(logMsg);

                // Build Ended, Switch to Normal View
                if (!hideProgress)
                    mainModel.SwitchNormalBuildInterface = true;

                // Turn off ProgressRing
                mainModel.WorkInProgress = false;

                Engine.WorkingEngine = null;
                Interlocked.Decrement(ref Engine.WorkingLock);

                if (!hideProgress)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MainWindow w = Application.Current.MainWindow as MainWindow;
                        w?.DrawScript(w.CurMainTree.Script);
                    });
                }
            }
        }
        #endregion
    }
    #endregion

    #region RenderInfo
    public struct RenderInfo
    {
        public readonly double MasterScale;
        public readonly Canvas Canvas;
        public readonly MainWindow Window;
        public readonly Script Script;
        public readonly string InterfaceSectionName;
        public readonly Logger Logger;

        public RenderInfo(Canvas canvas, MainWindow window, Logger logger, Script script, string interfaceSectionName, double masterScale)
        {
            Canvas = canvas;
            Window = window;
            Logger = logger;
            Script = script;
            InterfaceSectionName = interfaceSectionName;
            MasterScale = masterScale;
        }
    }
    #endregion
}
