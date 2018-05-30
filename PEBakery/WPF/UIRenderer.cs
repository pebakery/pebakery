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
using System.Drawing;
using MahApps.Metro.IconPacks;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
using Ookii.Dialogs.Wpf;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Image = System.Windows.Controls.Image;

namespace PEBakery.WPF
{
    // TODO: Fix potential memory leak due to event handler
    #region UIRenderer
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class UIRenderer
    {
        #region Fields
        public const int MaxDpiScale = 4;
        public const int MaxUrlDisplayLen = 47;
        public const string Interface = "Interface";

        private readonly Variables _variables;

        public RenderInfo RenderInfo;
        public List<UIControl> UICtrls { get; }
        public double ScaleFactor
        {
            get => RenderInfo.ScaleFactor;
            set => RenderInfo.ScaleFactor = value;
        }

        // Compatibility Option
        public static bool IgnoreWidthOfWebLabel = false;
        #endregion

        #region Constructor
        public UIRenderer(Canvas canvas, Window window, Script script, double scaleFactor, bool viewMode)
        {
            _variables = script.Project.Variables;
            RenderInfo = new RenderInfo(canvas, window, script, scaleFactor, viewMode);

            (List<UIControl> uiCtrls, List<LogInfo> errLogs) = LoadInterfaces(script);
            if (uiCtrls == null)
                uiCtrls = new List<UIControl>(0); // Create empty uiCtrls to prevent crash

            UICtrls = uiCtrls;

            App.Logger.SystemWrite(errLogs);
        }

        public UIRenderer(Canvas canvas, Window window, Script script, List<UIControl> uiCtrls, double scaleFactor, bool viewMode)
        {
            _variables = script.Project.Variables;
            RenderInfo = new RenderInfo(canvas, window, script, scaleFactor, viewMode);

            if (uiCtrls == null)
                uiCtrls = new List<UIControl>(0); // Create empty uiCtrls to prevent crash

            UICtrls = uiCtrls;
        }
        #endregion

        #region Load Utility

        public static (List<UIControl>, List<LogInfo>) LoadInterfaces(Script sc)
        {
            // Check if script has custom interface section
            string ifaceSectionName = GetInterfaceSectionName(sc);

            if (sc.Sections.ContainsKey(ifaceSectionName))
            {
                try
                {
                    List<UIControl> uiCtrls = sc.Sections[ifaceSectionName].GetUICtrls(true);
                    List<LogInfo> logInfos = sc.Sections[ifaceSectionName].LogInfos;
                    return (uiCtrls, logInfos);
                }
                catch (Exception e)
                {
                    return (null, new List<LogInfo>
                    {
                        new LogInfo(LogState.Error, $"Cannot read interface controls from [{sc.TreePath}]\r\n{Logger.LogExceptionMessage(e)}"),
                    });
                }
            }
            return (new List<UIControl>(), new List<LogInfo>());
        }

        public static string GetInterfaceSectionName(Script sc)
        {
            // Check if script has custom interface section
            if (sc.MainInfo.ContainsKey(Interface))
                return sc.MainInfo[Interface];
            return Interface;
        }
        #endregion

        #region Render All
        public void Render()
        {
            if (UICtrls == null) // This script does not have 'Interface' section
                return;

            UIControl[] uiCtrlArr = RenderInfo.ViewMode ? UICtrls.Where(x => x.Visibility).ToArray() : UICtrls.ToArray();

            InitCanvas(RenderInfo.Canvas);
            UIControl[] radioButtons = uiCtrlArr.Where(x => x.Type == UIControlType.RadioButton).ToArray();
            foreach (UIControl uiCmd in uiCtrlArr)
            {
                try
                {
                    switch (uiCmd.Type)
                    {
                        case UIControlType.TextBox:
                            UIRenderer.RenderTextBox(RenderInfo, uiCmd);
                            break;
                        case UIControlType.TextLabel:
                            UIRenderer.RenderTextLabel(RenderInfo, uiCmd);
                            break;
                        case UIControlType.NumberBox:
                            UIRenderer.RenderNumberBox(RenderInfo, uiCmd);
                            break;
                        case UIControlType.CheckBox:
                            UIRenderer.RenderCheckBox(RenderInfo, uiCmd);
                            break;
                        case UIControlType.ComboBox:
                            UIRenderer.RenderComboBox(RenderInfo, uiCmd);
                            break;
                        case UIControlType.Image:
                            UIRenderer.RenderImage(RenderInfo, uiCmd);
                            break;
                        case UIControlType.TextFile:
                            UIRenderer.RenderTextFile(RenderInfo, uiCmd);
                            break;
                        case UIControlType.Button:
                            UIRenderer.RenderButton(RenderInfo, uiCmd);
                            break;
                        case UIControlType.WebLabel:
                            UIRenderer.RenderWebLabel(RenderInfo, uiCmd);
                            break;
                        case UIControlType.RadioButton:
                            UIRenderer.RenderRadioButton(RenderInfo, uiCmd, radioButtons);
                            break;
                        case UIControlType.Bevel:
                            UIRenderer.RenderBevel(RenderInfo, uiCmd);
                            break;
                        case UIControlType.FileBox:
                            UIRenderer.RenderFileBox(RenderInfo, uiCmd, _variables);
                            break;
                        case UIControlType.RadioGroup:
                            UIRenderer.RenderRadioGroup(RenderInfo, uiCmd);
                            break;
                        default:
                            App.Logger.SystemWrite(new LogInfo(LogState.Error, $"Unable to render [{uiCmd.RawLine}]"));
                            break;
                    }
                }
                catch (Exception e)
                {
                    // Log failure
                    App.Logger.SystemWrite(new LogInfo(LogState.Error, $"{Logger.LogExceptionMessage(e)} [{uiCmd.RawLine}]"));
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

            if (r.ViewMode)
            {
                box.LostFocus += (object sender, RoutedEventArgs e) =>
                {
                    TextBox tBox = sender as TextBox;
                    Debug.Assert(tBox != null);

                    info.Value = tBox.Text;
                    uiCtrl.Update();
                };
            }

            SetToolTip(box, info.ToolTip);
            SetEditModeProperties(r, box, uiCtrl);
            DrawToCanvas(r, box, uiCtrl.Rect);

            if (0 < uiCtrl.Text.Length)
            {
                TextBlock block = new TextBlock
                {
                    Text = uiCtrl.Text,
                    LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                    LineHeight = CalcFontPointScale(),
                    FontSize = CalcFontPointScale(),
                };
                SetToolTip(block, info.ToolTip);
                SetEditModeProperties(r, block, uiCtrl);
                const double margin = UIControl.PointToDeviceIndependentPixel * UIControl.DefaultFontPoint * 1.2;
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

            switch (info.FontWeight)
            {
                case UIFontWeight.Bold:
                    block.FontWeight = FontWeights.Bold;
                    break;
            }

            switch (info.FontStyle)
            {
                case UIFontStyle.Italic:
                    block.FontStyle = FontStyles.Italic;
                    break;
                case UIFontStyle.Underline:
                    block.TextDecorations = TextDecorations.Underline;
                    break;
                case UIFontStyle.Strike:
                    block.TextDecorations = TextDecorations.Strikethrough;
                    break;
            }

            SetToolTip(block, info.ToolTip);
            SetEditModeProperties(r, block, uiCtrl);
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
                IncrementUnit = info.Tick,
                VerticalContentAlignment = VerticalAlignment.Center,
            };

            if (r.ViewMode)
            {
                box.ValueChanged += (object sender, RoutedPropertyChangedEventArgs<decimal> e) =>
                {
                    info.Value = (int)e.NewValue;
                    uiCtrl.Update();
                };
            }

            SetToolTip(box, info.ToolTip);
            SetEditModeProperties(r, box, uiCtrl);
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

            if (info.SectionName != null && r.ViewMode)
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
                        App.Logger.SystemWrite(new LogInfo(LogState.Error, $"Section [{info.SectionName}] does not exists"));
                    }
                };
            }

            if (r.ViewMode)
            {
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
            }

            SetToolTip(checkBox, info.ToolTip);
            SetEditModeProperties(r, checkBox, uiCtrl);
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

            if (r.ViewMode)
            {
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
            }

            if (info.SectionName != null && r.ViewMode)
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
                        App.Logger.SystemWrite(new LogInfo(LogState.Error, $"Section [{info.SectionName}] does not exists"));
                    }
                };
            }

            SetToolTip(comboBox, info.ToolTip);
            SetEditModeProperties(r, comboBox, uiCtrl);
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

            if (uiCtrl.Text.Equals(UIInfo_Image.NoImage, StringComparison.OrdinalIgnoreCase))
            { // Empty image
                PackIconMaterial noImage = ImageHelper.GetMaterialIcon(PackIconMaterialKind.BorderNone);
                noImage.Foreground = new SolidColorBrush(Color.FromArgb(96, 0, 0, 0));
                SetToolTip(noImage, info.ToolTip);
                SetEditModeProperties(r, noImage, uiCtrl);
                DrawToCanvas(r, noImage, uiCtrl.Rect);
                return;
            }

            if (!EncodedFile.ContainsFile(uiCtrl.Addr.Script, EncodedFile.InterfaceEncoded, uiCtrl.Text))
            { // Wrong encoded image
                PackIconMaterial alertImage = ImageHelper.GetMaterialIcon(PackIconMaterialKind.Alert);
                alertImage.Foreground = new SolidColorBrush(Color.FromArgb(96, 0, 0, 0));
                SetToolTip(alertImage, info.ToolTip);
                SetEditModeProperties(r, alertImage, uiCtrl);
                DrawToCanvas(r, alertImage, uiCtrl.Rect);

                App.Logger.SystemWrite(new LogInfo(LogState.Error, $"Unable to find encoded image [{uiCtrl.Text}] ({uiCtrl.RawLine})"));
                return;
            }

            BitmapImage bitmap;
            using (MemoryStream ms = EncodedFile.ExtractInterface(uiCtrl.Addr.Script, uiCtrl.Text))
            {
                if (!ImageHelper.GetImageType(uiCtrl.Text, out ImageHelper.ImageType type))
                {
                    App.Logger.SystemWrite(new LogInfo(LogState.Error, $"Image [{Path.GetExtension(uiCtrl.Text)}] is not supported"));
                    return;
                }

                if (type == ImageHelper.ImageType.Svg)
                {
                    double width = uiCtrl.Rect.Width * r.ScaleFactor;
                    double height = uiCtrl.Rect.Height * r.ScaleFactor;
                    bitmap = ImageHelper.SvgToBitmapImage(ms, width, height);
                }
                else
                {
                    bitmap = ImageHelper.ImageToBitmapImage(ms);
                }
            }

            if (r.ViewMode)
            {
                Button button = new Button
                {
                    Style = (Style)r.Window.FindResource("ImageButton"),
                    Background = ImageHelper.BitmapImageToImageBrush(bitmap)
                };

                bool hasUrl = false;
                if (!string.IsNullOrEmpty(info.Url))
                {
                    if (Uri.TryCreate(info.Url, UriKind.Absolute, out Uri _)) // Success
                        hasUrl = true;
                    else // Failure
                        throw new InvalidUIControlException($"Invalid URL [{info.Url}]", uiCtrl);
                }

                string toolTip = info.ToolTip;
                if (hasUrl)
                { // Open URL
                    button.Click += (object sender, RoutedEventArgs e) =>
                    {
                        Process.Start(info.Url);
                    };

                    toolTip = UIRenderer.AppendUrlToToolTip(info.ToolTip, info.Url);
                }
                else
                { // Open picture with external viewer
                    button.Click += (object sender, RoutedEventArgs e) =>
                    {
                        if (!ImageHelper.GetImageType(uiCtrl.Text, out ImageHelper.ImageType t))
                        {
                            App.Logger.SystemWrite(new LogInfo(LogState.Error, $"Image [{Path.GetExtension(uiCtrl.Text)}] is not supported"));
                            return;
                        }

                        string path = Path.ChangeExtension(Path.GetTempFileName(), "." + t.ToString().ToLower());

                        using (MemoryStream ms = EncodedFile.ExtractInterface(uiCtrl.Addr.Script, uiCtrl.Text))
                        using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                        {
                            ms.Position = 0;
                            ms.CopyTo(fs);
                        }

                        ProcessStartInfo procInfo = new ProcessStartInfo
                        {
                            FileName = path,
                            UseShellExecute = true
                        };
                        Process.Start(procInfo);
                    };
                }

                SetToolTip(button, toolTip);
                DrawToCanvas(r, button, uiCtrl.Rect);
            }
            else
            {
                Image image = new Image
                {
                    StretchDirection = StretchDirection.Both,
                    Stretch = Stretch.Fill,
                    UseLayoutRounding = true,
                    Source = bitmap,
                };
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

                SetToolTip(image, info.ToolTip);
                SetEditModeProperties(r, image, uiCtrl);
                DrawToCanvas(r, image, uiCtrl.Rect);
            }
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

            TextBox textBox = new TextBox
            {
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                IsReadOnly = true,
                FontSize = CalcFontPointScale(),
            };

            if (!uiCtrl.Text.Equals(UIInfo_TextFile.NoImage, StringComparison.OrdinalIgnoreCase))
            {
                if (!EncodedFile.ContainsFile(uiCtrl.Addr.Script, EncodedFile.InterfaceEncoded, uiCtrl.Text))
                { // Wrong encoded text
                    string errMsg = $"Unable to find encoded text [{uiCtrl.Text}]";
                    textBox.Text = errMsg;
                    App.Logger.SystemWrite(new LogInfo(LogState.Error, $"{errMsg} ({uiCtrl.RawLine})"));
                }
                else
                {
                    using (MemoryStream ms = EncodedFile.ExtractInterface(uiCtrl.Addr.Script, uiCtrl.Text))
                    using (StreamReader sr = new StreamReader(ms, FileHelper.DetectTextEncoding(ms)))
                    {
                        textBox.Text = sr.ReadToEnd();
                    }
                }
            }
            
            ScrollViewer.SetHorizontalScrollBarVisibility(textBox, ScrollBarVisibility.Auto);
            ScrollViewer.SetVerticalScrollBarVisibility(textBox, ScrollBarVisibility.Auto);
            ScrollViewer.SetCanContentScroll(textBox, true);

            SetToolTip(textBox, info.ToolTip);
            SetEditModeProperties(r, textBox, uiCtrl);
            DrawToCanvas(r, textBox, uiCtrl.Rect);
        }

        /// <summary>
        /// Render Button control.
        /// Return true if failed.
        /// </summary>
        /// <returns>Success = false, Failure = true</returns>
        public static void RenderButton(RenderInfo r, UIControl uiCtrl)
        {
            Debug.Assert(uiCtrl.Info.GetType() == typeof(UIInfo_Button), "Invalid UIInfo");
            UIInfo_Button info = uiCtrl.Info as UIInfo_Button;
            Debug.Assert(info != null, "Invalid UIInfo");

            Button button = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            if (r.ViewMode)
            {
                button.Click += (object sender, RoutedEventArgs e) =>
                {
                    if (r.Script.Sections.ContainsKey(info.SectionName)) // Only if section exists
                    {
                        SectionAddress addr = new SectionAddress(r.Script, r.Script.Sections[info.SectionName]);
                        UIRenderer.RunOneSection(addr, $"{r.Script.Title} - Button [{uiCtrl.Key}]", info.HideProgress);
                    }
                    else
                    {
                        App.Logger.SystemWrite(new LogInfo(LogState.Error, $"Section [{info.SectionName}] does not exists"));
                    }
                };
            }
                
            if (info.Picture != null && uiCtrl.Addr.Script.Sections.ContainsKey(EncodedFile.GetSectionName(EncodedFile.InterfaceEncoded, info.Picture)))
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

                using (MemoryStream ms = EncodedFile.ExtractInterface(uiCtrl.Addr.Script, info.Picture))
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
            SetEditModeProperties(r, button, uiCtrl);
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
                NavigateUri = new Uri(info.Url),
            };
            hyperLink.Inlines.Add(uiCtrl.Text);
            if (r.ViewMode)
            {
                hyperLink.RequestNavigate += (object sender, RequestNavigateEventArgs e) =>
                {
                    FileHelper.OpenUri(e.Uri.AbsoluteUri);
                };
            }
            block.Inlines.Add(hyperLink);

            string toolTip = UIRenderer.AppendUrlToToolTip(info.ToolTip, info.Url);
            SetToolTip(block, toolTip);
            SetEditModeProperties(r, block, uiCtrl);

            if (IgnoreWidthOfWebLabel && r.ViewMode)
            { // Disable this in edit mode to encourage script developer address this issue
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

            if (info.SectionName != null && r.ViewMode)
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
                            w?.Logger.SystemWrite(new LogInfo(LogState.Error, $"Section [{info.SectionName}] does not exists"));
                        });
                    }
                };
            }

            if (r.ViewMode)
            {
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
            }

            SetToolTip(radio, info.ToolTip);
            SetEditModeProperties(r, radio, uiCtrl);
            DrawToCanvas(r, radio, uiCtrl.Rect);
        }

        /// <summary>
        /// Render Bevel control.
        /// Return true if failed.
        /// </summary>
        /// <returns>Success = false, Failure = true</returns>
        public static void RenderBevel(RenderInfo r, UIControl uiCtrl)
        {
            UIInfo_Bevel info = uiCtrl.Info.Cast<UIInfo_Bevel>();

            Border bevel = new Border
            {
                IsHitTestVisible = false, // Focus is not given when clicked
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0.7),
                BorderBrush = Brushes.Gray,
            };

            if (!r.ViewMode) // Focus is given when clicked
                bevel.IsHitTestVisible = true;

            SetToolTip(bevel, info.ToolTip);
            
            SetEditModeProperties(r, bevel, uiCtrl);
            DrawToCanvas(r, bevel, uiCtrl.Rect);

            if (info.FontSize != null)
            { // PEBakery Extension - see https://github.com/pebakery/pebakery/issues/34
                int fontSize = info.FontSize ?? UIControl.DefaultFontPoint;

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
                switch (info.FontWeight)
                {
                    case UIFontWeight.Bold:
                        textBlock.FontWeight = FontWeights.Bold;
                        break;
                }

                switch (info.FontStyle)
                {
                    case UIFontStyle.Italic:
                        textBlock.FontStyle = FontStyles.Italic;
                        break;
                    case UIFontStyle.Underline:
                        textBlock.TextDecorations = TextDecorations.Underline;
                        break;
                    case UIFontStyle.Strike:
                        textBlock.TextDecorations = TextDecorations.Strikethrough;
                        break;
                }

                Rect blockRect = new Rect
                {
                    X = uiCtrl.Rect.X + CalcFontPointScale(fontSize) / 3,
                    Y = uiCtrl.Rect.Y - CalcFontPointScale(fontSize),
                    Width = double.NaN,
                    Height = double.NaN,
                };

                SetEditModeProperties(r, textBorder, uiCtrl);
                DrawToCanvas(r, textBorder, blockRect);
            }
        }

        /// <summary>
        /// Render FileBox control.
        /// Return true if failed.
        /// </summary>
        /// <returns>Success = false, Failure = true</returns>
        public static void RenderFileBox(RenderInfo r, UIControl uiCtrl, Variables variables)
        {
            Debug.Assert(uiCtrl.Info.GetType() == typeof(UIInfo_FileBox), "Invalid UIInfo");
            UIInfo_FileBox info = uiCtrl.Info as UIInfo_FileBox;
            Debug.Assert(info != null, "Invalid UIInfo");

            TextBox box = new TextBox
            {
                Text = uiCtrl.Text,
                FontSize = CalcFontPointScale(),
                VerticalContentAlignment = VerticalAlignment.Center,
            };

            if (r.ViewMode)
            {
                box.TextChanged += (object sender, TextChangedEventArgs e) =>
                {
                    TextBox tBox = sender as TextBox;
                    Debug.Assert(tBox != null);

                    uiCtrl.Text = tBox.Text;
                    uiCtrl.Update();
                };
            }
                
            SetToolTip(box, info.ToolTip);
            SetEditModeProperties(r, box, uiCtrl);

            Button button = new Button
            {
                FontSize = CalcFontPointScale(),
                Content = ImageHelper.GetMaterialIcon(PackIconMaterialKind.FolderOpen, 0),
            };
            SetToolTip(button, info.ToolTip);
            SetEditModeProperties(r, button, uiCtrl);

            if (r.ViewMode)
            {
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
            }

            const double margin = 5;
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
            SetEditModeProperties(r, box, uiCtrl);

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

                if (r.ViewMode)
                {
                    radio.Checked += (object sender, RoutedEventArgs e) =>
                    {
                        RadioButton btn = sender as RadioButton;
                        Debug.Assert(btn != null);

                        info.Selected = (int)btn.Tag;
                        uiCtrl.Update();
                    };
                }

                if (info.SectionName != null && r.ViewMode)
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
                                w?.Logger.SystemWrite(new LogInfo(LogState.Error, $"Section [{info.SectionName}] does not exists"));
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

        #region Update

        public void Update(UIControl uiCtrl)
        {
            /*
            FrameworkElement element = null;
            foreach (FrameworkElement child in RenderInfo.Canvas.Children)
            {
                if (child.Tag is UIControl ctrl)
                {
                    if (ctrl.Key.Equals(uiCtrl.Key, StringComparison.Ordinal))
                    {
                        element = child;
                        break;
                    }
                }
            }

            UIRenderer.RemoveFromCanvas(RenderInfo.Canvas, element);
            */
        }
        #endregion

        #region Render Utility
        private static void InitCanvas(Canvas canvas)
        {
            canvas.Children.Clear();
            canvas.Width = double.NaN;
            canvas.Height = double.NaN;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void DrawToCanvas(RenderInfo r, FrameworkElement element, Rect coord)
        {
            DrawToCanvas(r.Canvas, element, coord);
        }

        public static void DrawToCanvas(Canvas canvas, FrameworkElement element, Rect coord)
        {
            Canvas.SetLeft(element, coord.Left);
            Canvas.SetTop(element, coord.Top);
            element.Width = coord.Width;
            element.Height = coord.Height;

            canvas.Children.Add(element);
            if (double.IsNaN(canvas.Width) || canvas.Width < coord.Left + coord.Width)
                canvas.Width = coord.Left + coord.Width;
            if (double.IsNaN(canvas.Height) || canvas.Height < coord.Top + coord.Height)
                canvas.Height = coord.Top + coord.Height;
        }

        public static void RemoveFromCanvas(Canvas canvas, FrameworkElement element)
        {
            canvas.Children.Remove(element);
        }

        private static void SetToolTip(FrameworkElement element, string toolTip)
        {
            if (toolTip != null)
                element.ToolTip = toolTip;
        }

        private static void SetEditModeProperties(RenderInfo r, FrameworkElement element, UIControl uiCtrl)
        {
            if (r.ViewMode)
                return;

            // Only for EditMode
            element.Tag = uiCtrl;
            if (!uiCtrl.Visibility)
                element.Opacity = 0.5;
        }

        private static double CalcFontPointScale(double fontPoint = UIControl.DefaultFontPoint) 
        {
            return fontPoint * UIControl.PointToDeviceIndependentPixel;
        }

        private static string AppendUrlToToolTip(string toolTip, string url)
        {
            if (url == null)
                return toolTip;

            if (MaxUrlDisplayLen < url.Length)
                url = url.Substring(0, MaxUrlDisplayLen) + "...";

            if (toolTip == null)
                return url;
            return toolTip + Environment.NewLine + Environment.NewLine + url;
        }

        public static int GetMaxZIndex(Canvas canvas)
        {
            int max = Panel.GetZIndex(canvas);
            foreach (UIElement element in canvas.Children)
            {
                int z = Panel.GetZIndex(element);
                if (max < z)
                    max = z;
            }
            return max;
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
                    if (!(Application.Current.MainWindow is MainWindow w))
return;

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
                });

                mainModel.WorkInProgress = true;

                EngineState s = new EngineState(addr.Script.Project, logger, mainModel, EngineMode.RunMainAndOne, addr.Script, addr.Section.Name);
                s.SetOption(setting);
                if (s.LogMode == LogMode.PartDelay)
                    s.LogMode = LogMode.FullDelay;

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

                // Flush FullDelayedLogs
                if (s.LogMode == LogMode.FullDelay)
                {
                    DelayedLogging delayed = logger.Delayed;
                    delayed.FlushFullDelayed(s);
                }

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
        public double ScaleFactor;
        public readonly Canvas Canvas;
        public readonly Window Window;
        public readonly Script Script;
        /// <summary>
        /// true in MainWindow, false in ScriptEditWindow
        /// </summary>
        public readonly bool ViewMode;

        public RenderInfo(Canvas canvas, Window window, Script script, double scale, bool allowModify)
        {
            ScaleFactor = scale;
            Canvas = canvas;
            Window = window;
            Script = script;
            ViewMode = allowModify;
        }
    }
    #endregion
}
