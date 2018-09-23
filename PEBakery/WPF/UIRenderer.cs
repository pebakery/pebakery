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

using MahApps.Metro.IconPacks;
using Ookii.Dialogs.Wpf;
using PEBakery.Core;
using PEBakery.Helper;
using PEBakery.WPF.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace PEBakery.WPF
{
    #region UIRenderer
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class UIRenderer
    {
        #region Fields and Properties
        public const int MaxDpiScale = 4;
        public const int MaxUrlDisplayLen = 47;
        private readonly Variables _variables;

        private RenderInfo _r;
        public double ScaleFactor
        {
            get => _r.ScaleFactor;
            set => _r.ScaleFactor = value;
        }

        public readonly List<UIControl> UICtrls;
        private UIControl[] _visibleCtrls => _r.ViewMode ? UICtrls.Where(x => x.Visibility).ToArray() : UICtrls.ToArray();
        private UIControl[] _radioButtons => _visibleCtrls.Where(x => x.Type == UIControlType.RadioButton).ToArray();
        private readonly List<RenderCleanInfo> _cleanInfos = new List<RenderCleanInfo>();

        // Compatibility Option
        public static bool IgnoreWidthOfWebLabel = false;
        #endregion

        #region Constructor
        public UIRenderer(Canvas canvas, Window window, Script script, double scaleFactor, bool viewMode)
        {
            _variables = script.Project.Variables;
            _r = new RenderInfo(canvas, window, script, scaleFactor, viewMode);

            (List<UIControl> uiCtrls, List<LogInfo> errLogs) = LoadInterfaces(script);
            UICtrls = uiCtrls ?? new List<UIControl>(0);

            App.Logger.SystemWrite(errLogs);
        }

        public UIRenderer(Canvas canvas, Window window, Script script, List<UIControl> uiCtrls, double scaleFactor, bool viewMode)
        {
            _variables = script.Project.Variables;
            _r = new RenderInfo(canvas, window, script, scaleFactor, viewMode);

            UICtrls = uiCtrls ?? new List<UIControl>(0);
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
                    ScriptSection ifaceSection = sc.Sections[ifaceSectionName];
                    (List<UIControl> uiCtrls, List<LogInfo> errorLogs) = UIParser.ParseStatements(ifaceSection.Lines, ifaceSection);
                    return (uiCtrls, errorLogs);
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
            if (sc.MainInfo.ContainsKey(ScriptSection.Names.Interface))
                return sc.MainInfo[ScriptSection.Names.Interface];
            return ScriptSection.Names.Interface;
        }
        #endregion

        #region Render
        public void Render()
        {
            if (UICtrls == null) // This script does not have 'Interface' section
                return;

            InitCanvas(_r.Canvas);
            _cleanInfos.Clear();
            foreach (UIControl uiCtrl in _visibleCtrls)
            {
                try
                {
                    // Render and add event
                    RenderCleanInfo? clean = null;
                    switch (uiCtrl.Type)
                    {
                        case UIControlType.TextBox:
                            clean = RenderTextBox(uiCtrl);
                            break;
                        case UIControlType.TextLabel:
                            RenderTextLabel(uiCtrl);
                            break;
                        case UIControlType.NumberBox:
                            clean = RenderNumberBox(uiCtrl);
                            break;
                        case UIControlType.CheckBox:
                            clean = RenderCheckBox(uiCtrl);
                            break;
                        case UIControlType.ComboBox:
                            clean = RenderComboBox(uiCtrl);
                            break;
                        case UIControlType.Image:
                            clean = RenderImage(uiCtrl);
                            break;
                        case UIControlType.TextFile:
                            RenderTextFile(uiCtrl);
                            break;
                        case UIControlType.Button:
                            clean = RenderButton(uiCtrl);
                            break;
                        case UIControlType.WebLabel:
                            clean = RenderWebLabel(uiCtrl);
                            break;
                        case UIControlType.RadioButton:
                            clean = RenderRadioButton(uiCtrl);
                            break;
                        case UIControlType.Bevel:
                            RenderBevel(uiCtrl);
                            break;
                        case UIControlType.FileBox:
                            clean = RenderFileBox(uiCtrl, _variables);
                            break;
                        case UIControlType.RadioGroup:
                            clean = RenderRadioGroup(uiCtrl);
                            break;
                        default:
                            App.Logger.SystemWrite(new LogInfo(LogState.Error, $"Unknown UIControlType [{uiCtrl.Type}] ({uiCtrl.RawLine})"));
                            break;
                    }

                    // In edit mode (ScriptEditWindow), all event handler is disabled -> no need to clean events
                    if (_r.ViewMode && clean is RenderCleanInfo ci)
                        _cleanInfos.Add(ci);
                }
                catch (Exception e)
                {
                    // Log failure
                    App.Logger.SystemWrite(new LogInfo(LogState.Error, $"{Logger.LogExceptionMessage(e)} [{uiCtrl.RawLine}]"));
                }
            }
        }
        #endregion

        #region Clear
        public void Clear()
        {
            if (UICtrls == null) // This script does not have 'Interface' section
                return;

            foreach (RenderCleanInfo ci in _cleanInfos)
            {
                // Remove Event
                switch (ci.UICtrl.Type)
                {
                    case UIControlType.TextBox:
                        ManageTextBoxEvent(ci.Element as TextBox, false);
                        break;
                    case UIControlType.NumberBox:
                        ManageNumberBoxEvent(ci.Element as NumberBox, false);
                        break;
                    case UIControlType.CheckBox:
                        ManageCheckBoxEvent(ci.Element as CheckBox, false, (string)ci.Tag);
                        break;
                    case UIControlType.ComboBox:
                        ManageComboBoxEvent(ci.Element as ComboBox, false, (string)ci.Tag);
                        break;
                    case UIControlType.Image:
                        ManageImageEvent(ci.Element as Button, false, (bool)ci.Tag);
                        break;
                    case UIControlType.Button:
                        ManageButtonEvent(ci.Element as Button, false, (string)ci.Tag);
                        break;
                    case UIControlType.WebLabel:
                        ManageWebLabelEvent(ci.Element as Hyperlink, false);
                        break;
                    case UIControlType.RadioButton:
                        ManageRadioButtonEvent(ci.Element as RadioButton, false, (string)ci.Tag);
                        break;
                    case UIControlType.FileBox:
                        Debug.Assert(ci.Elements != null, $"null in [UIRenderer.{nameof(Clear)}, {UIControlType.FileBox}]");
                        Debug.Assert(ci.Elements.Length == 2, $"null in [UIRenderer.{nameof(Clear)}, {UIControlType.FileBox}]]");
                        ManageFileBoxEvent(ci.Elements[0] as TextBox, ci.Elements[1] as Button, false);
                        break;
                    case UIControlType.RadioGroup:
                        Debug.Assert(ci.Elements != null, $"null in [UIRenderer.{nameof(Clear)}, {UIControlType.RadioGroup}]");
                        ManageRadioGroupEvent(ci.Elements.Select(x => x as RadioButton).ToArray(), false, (string)ci.Tag);
                        break;
                }
            }

            _cleanInfos.Clear();
            InitCanvas(_r.Canvas);
        }
        #endregion

        #region Render Control
        #region TextBox
        public RenderCleanInfo RenderTextBox(UIControl uiCtrl)
        {
            // WB082 textbox control's y coord is of textbox's, not textlabel's.
            UIInfo_TextBox info = uiCtrl.Info.Cast<UIInfo_TextBox>();

            TextBox box = new TextBox
            {
                Text = info.Value,
                Height = uiCtrl.Height,
                FontSize = CalcFontPointScale(),
                VerticalContentAlignment = VerticalAlignment.Center,
            };

            if (_r.ViewMode)
                ManageTextBoxEvent(box, true);

            if (uiCtrl.Text.Length == 0)
            { // No caption
                SetToolTip(box, info.ToolTip);
                SetEditModeProperties(_r, box, uiCtrl);
                DrawToCanvas(_r, box, uiCtrl);
            }
            else
            { // Print caption
                TextBlock block = new TextBlock
                {
                    Text = uiCtrl.Text,
                    VerticalAlignment = VerticalAlignment.Top,
                    LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                    LineHeight = CalcFontPointScale(),
                    FontSize = CalcFontPointScale(),
                };

                // Render to canvas
                Grid grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(UIInfo_TextBox.AddWidth) });
                grid.RowDefinitions.Add(new RowDefinition());

                Grid.SetRow(block, 0);
                grid.Children.Add(block);
                Grid.SetRow(box, 1);
                grid.Children.Add(box);

                SetToolTip(grid, info.ToolTip);
                SetEditModeProperties(_r, grid, uiCtrl);

                Rect gridRect = new Rect(uiCtrl.X, uiCtrl.Y - UIInfo_TextBox.AddWidth, uiCtrl.Width, uiCtrl.Height + UIInfo_TextBox.AddWidth);
                DrawToCanvas(_r, grid, uiCtrl, gridRect);
            }

            return new RenderCleanInfo(uiCtrl, box);
        }

        public void ManageTextBoxEvent(TextBox box, bool addMode)
        {
            Debug.Assert(box != null, $"null in [{nameof(ManageTextBoxEvent)}]");
            if (addMode)
                box.LostFocus += TextBox_LostFocus;
            else
                box.LostFocus -= TextBox_LostFocus;
        }

        public void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox box = sender as TextBox;
            Debug.Assert(box != null, $"Wrong sender in [{nameof(TextBox_LostFocus)}]");
            if (!(box.Tag is UIControl uiCtrl))
                return;

            Debug.Assert(uiCtrl.Type == UIControlType.TextBox);
            UIInfo_TextBox info = uiCtrl.Info.Cast<UIInfo_TextBox>();
            info.Value = box.Text;
            uiCtrl.Update();
        }
        #endregion

        #region TextLabel
        public void RenderTextLabel(UIControl uiCtrl)
        {
            UIInfo_TextLabel info = uiCtrl.Info.Cast<UIInfo_TextLabel>();

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
            SetEditModeProperties(_r, block, uiCtrl);
            DrawToCanvas(_r, block, uiCtrl);
        }
        #endregion

        #region NumberBox
        public RenderCleanInfo RenderNumberBox(UIControl uiCtrl)
        {
            UIInfo_NumberBox info = uiCtrl.Info.Cast<UIInfo_NumberBox>();

            NumberBox box = new NumberBox
            {
                Value = info.Value,
                FontSize = CalcFontPointScale(),
                Minimum = info.Min,
                Maximum = info.Max,
                DecimalPlaces = 0,
                IncrementUnit = info.Tick,
                VerticalContentAlignment = VerticalAlignment.Center,
            };

            if (_r.ViewMode)
                ManageNumberBoxEvent(box, true);

            SetToolTip(box, info.ToolTip);
            SetEditModeProperties(_r, box, uiCtrl);
            DrawToCanvas(_r, box, uiCtrl);

            return new RenderCleanInfo(uiCtrl, box);
        }

        public void ManageNumberBoxEvent(NumberBox box, bool addMode)
        {
            Debug.Assert(box != null, $"null in [{nameof(ManageNumberBoxEvent)}]");
            if (addMode)
                box.ValueChanged += NumberBox_ValueChanged;
            else
                box.ValueChanged -= NumberBox_ValueChanged;
        }

        public void NumberBox_ValueChanged(object sender, RoutedPropertyChangedEventArgs<decimal> e)
        {
            NumberBox box = sender as NumberBox;
            Debug.Assert(box != null, $"Wrong sender in [{nameof(NumberBox_ValueChanged)}]");
            if (!(box.Tag is UIControl uiCtrl))
                return;

            Debug.Assert(uiCtrl.Type == UIControlType.NumberBox);
            UIInfo_NumberBox info = uiCtrl.Info.Cast<UIInfo_NumberBox>();
            info.Value = (int)e.NewValue;
            uiCtrl.Update();
        }
        #endregion

        #region CheckBox
        public RenderCleanInfo RenderCheckBox(UIControl uiCtrl)
        {
            UIInfo_CheckBox info = uiCtrl.Info.Cast<UIInfo_CheckBox>();

            CheckBox box = new CheckBox
            {
                Content = uiCtrl.Text,
                IsChecked = info.Value,
                FontSize = CalcFontPointScale(),
                VerticalContentAlignment = VerticalAlignment.Center,
            };

            if (_r.ViewMode)
                ManageCheckBoxEvent(box, true, info.SectionName);

            SetToolTip(box, info.ToolTip);
            SetEditModeProperties(_r, box, uiCtrl);
            DrawToCanvas(_r, box, uiCtrl);

            return new RenderCleanInfo(uiCtrl, box, info.SectionName);
        }

        public void ManageCheckBoxEvent(CheckBox box, bool addMode, string sectionName)
        {
            Debug.Assert(box != null, $"null in [{nameof(ManageCheckBoxEvent)}]");
            if (addMode)
            {
                box.Checked += CheckBox_Checked;
                box.Unchecked += CheckBox_Unchecked;
                if (sectionName != null)
                    box.Click += CheckBox_Click;
            }
            else
            {
                box.Checked -= CheckBox_Checked;
                box.Unchecked -= CheckBox_Unchecked;
                if (sectionName != null)
                    box.Click -= CheckBox_Click;
            }
        }

        public void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox box = sender as CheckBox;
            Debug.Assert(box != null, $"Wrong sender in [{nameof(CheckBox_Checked)}]");
            if (!(box.Tag is UIControl uiCtrl))
                return;

            Debug.Assert(uiCtrl.Type == UIControlType.CheckBox);
            UIInfo_CheckBox info = uiCtrl.Info.Cast<UIInfo_CheckBox>();
            info.Value = true;
            uiCtrl.Update();
        }

        public void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            CheckBox box = sender as CheckBox;
            Debug.Assert(box != null, $"Wrong sender in [{nameof(CheckBox_Unchecked)}]");
            if (!(box.Tag is UIControl uiCtrl))
                return;
            
            Debug.Assert(uiCtrl.Type == UIControlType.CheckBox);
            UIInfo_CheckBox info = uiCtrl.Info.Cast<UIInfo_CheckBox>();
            info.Value = false;
            uiCtrl.Update();
        }

        public void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            CheckBox box = sender as CheckBox;
            Debug.Assert(box != null, $"Wrong sender in [{nameof(CheckBox_Click)}]");
            if (!(box.Tag is UIControl uiCtrl))
                return;

            Debug.Assert(uiCtrl.Type == UIControlType.CheckBox);
            UIInfo_CheckBox info = uiCtrl.Info.Cast<UIInfo_CheckBox>();
            RunOneSection(uiCtrl.Type, uiCtrl.Key, info.SectionName, info.HideProgress);
        }
        #endregion

        #region ComboBox
        public RenderCleanInfo RenderComboBox(UIControl uiCtrl)
        {
            UIInfo_ComboBox info = uiCtrl.Info.Cast<UIInfo_ComboBox>();

            ComboBox box = new ComboBox
            {
                FontSize = CalcFontPointScale(),
                ItemsSource = info.Items,
                SelectedIndex = info.Index,
                VerticalContentAlignment = VerticalAlignment.Center,
            };

            if (_r.ViewMode)
                ManageComboBoxEvent(box, true, info.SectionName);

            SetToolTip(box, info.ToolTip);
            SetEditModeProperties(_r, box, uiCtrl);
            DrawToCanvas(_r, box, uiCtrl);

            return new RenderCleanInfo(uiCtrl, box, info.SectionName);
        }

        public void ManageComboBoxEvent(ComboBox box, bool addMode, string sectionName)
        {
            Debug.Assert(box != null, $"null in [{nameof(ManageComboBoxEvent)}]");
            if (addMode)
            {
                box.LostFocus += ComboBox_LostFocus;
                if (sectionName != null)
                    box.SelectionChanged += ComboBox_SelectionChanged;
            }
            else
            {
                box.LostFocus -= ComboBox_LostFocus;
                if (sectionName != null)
                    box.SelectionChanged -= ComboBox_SelectionChanged;
            }
        }

        public void ComboBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ComboBox box = sender as ComboBox;
            Debug.Assert(box != null, $"Wrong sender in [{nameof(CheckBox_Checked)}]");
            if (!(box.Tag is UIControl uiCtrl))
                return;

            Debug.Assert(uiCtrl.Type == UIControlType.CheckBox);
            UIInfo_ComboBox info = uiCtrl.Info.Cast<UIInfo_ComboBox>();

            if (info.Index != box.SelectedIndex)
            {
                info.Index = box.SelectedIndex;
                uiCtrl.Text = info.Items[box.SelectedIndex];
                uiCtrl.Update();
            }
        }

        public void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox box = sender as ComboBox;
            Debug.Assert(box != null, $"Wrong sender in [{nameof(ComboBox_SelectionChanged)}]");
            if (!(box.Tag is UIControl uiCtrl))
                return;

            Debug.Assert(uiCtrl.Type == UIControlType.ComboBox);
            UIInfo_ComboBox info = uiCtrl.Info.Cast<UIInfo_ComboBox>();
            RunOneSection(uiCtrl.Type, uiCtrl.Key, info.SectionName, info.HideProgress);
        }
        #endregion

        #region Image
        public RenderCleanInfo? RenderImage(UIControl uiCtrl)
        {
            UIInfo_Image info = uiCtrl.Info.Cast<UIInfo_Image>();

            if (uiCtrl.Text.Equals(UIInfo_Image.NoResource, StringComparison.OrdinalIgnoreCase))
            { // Empty image
                PackIconMaterial noImage = new PackIconMaterial
                {
                    Kind = PackIconMaterialKind.BorderNone,
                    Width = double.NaN,
                    Height = double.NaN,
                    Foreground = new SolidColorBrush(Color.FromArgb(96, 0, 0, 0))
                };
                Border border = new Border
                {
                    Focusable = true,
                    Width = double.NaN,
                    Height = double.NaN,
                    Background = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255)),
                    BorderThickness = new Thickness(0),
                    Child = noImage,
                };
                SetToolTip(border, info.ToolTip);
                SetEditModeProperties(_r, border, uiCtrl);
                DrawToCanvas(_r, border, uiCtrl);
                return null;
            }

            if (!EncodedFile.ContainsInterface(uiCtrl.Section.Script, uiCtrl.Text))
            { // Encoded image does not exist
                PackIconMaterial alertImage = new PackIconMaterial
                {
                    Kind = PackIconMaterialKind.Alert,
                    Width = double.NaN,
                    Height = double.NaN,
                    Foreground = new SolidColorBrush(Color.FromArgb(96, 0, 0, 0)),
                };
                Border border = new Border
                {
                    Focusable = true,
                    Width = double.NaN,
                    Height = double.NaN,
                    Background = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255)),
                    BorderThickness = new Thickness(0),
                    Child = alertImage,
                };
                SetToolTip(border, info.ToolTip);
                SetEditModeProperties(_r, border, uiCtrl);
                DrawToCanvas(_r, border, uiCtrl);

                App.Logger.SystemWrite(new LogInfo(LogState.Error, $"Unable to find encoded image [{uiCtrl.Text}] ({uiCtrl.RawLine})"));
                return null;
            }

            BitmapImage bitmap;
            using (MemoryStream ms = EncodedFile.ExtractInterface(uiCtrl.Section.Script, uiCtrl.Text))
            {
                if (!ImageHelper.GetImageType(uiCtrl.Text, out ImageHelper.ImageType type))
                {
                    App.Logger.SystemWrite(new LogInfo(LogState.Error, $"Image [{Path.GetExtension(uiCtrl.Text)}] is not supported"));
                    return null;
                }

                switch (type)
                {
                    case ImageHelper.ImageType.Svg:
                        double width = uiCtrl.Rect.Width * _r.ScaleFactor;
                        double height = uiCtrl.Rect.Height * _r.ScaleFactor;
                        bitmap = ImageHelper.SvgToBitmapImage(ms, width, height);
                        break;
                    default:
                        bitmap = ImageHelper.ImageToBitmapImage(ms);
                        break;
                }
            }

            if (_r.ViewMode)
            {
                Button button = new Button
                {
                    Style = (Style)_r.Window.FindResource("ImageButton"),
                    Background = ImageHelper.BitmapImageToImageBrush(bitmap)
                };

                bool hasUrl = false;
                if (!string.IsNullOrEmpty(info.Url))
                {
                    if (Uri.TryCreate(info.Url, UriKind.Absolute, out Uri _)) // Success
                        hasUrl = true;
                    else // Failure
                        throw new InvalidCommandException($"Invalid URL [{info.Url}]");
                }

                string toolTip = info.ToolTip;
                ManageImageEvent(button, true, hasUrl);
                if (hasUrl) // Open URL
                    toolTip = AppendUrlToToolTip(info.ToolTip, info.Url);

                SetToolTip(button, toolTip);
                DrawToCanvas(_r, button, uiCtrl);
                return new RenderCleanInfo(uiCtrl, button, hasUrl);
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
                SetEditModeProperties(_r, image, uiCtrl);
                DrawToCanvas(_r, image, uiCtrl);
                return null;
            }
        }

        public void ManageImageEvent(Button button, bool addMode, bool hasUrl)
        {
            Debug.Assert(button != null, $"null in [{nameof(ManageImageEvent)}]");
            if (addMode)
            {
                if (hasUrl)
                    button.Click += Image_Click_OpenUrl;
                else
                    button.Click += Image_Click_OpenImage;
            }
            else
            {
                if (hasUrl)
                    button.Click -= Image_Click_OpenUrl;
                else
                    button.Click -= Image_Click_OpenImage;
            }
        }

        /// <summary>
        /// Open URL
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void Image_Click_OpenUrl(object sender, RoutedEventArgs e)
        { 
            Image image = sender as Image;
            Debug.Assert(image != null, $"Wrong sender in [{nameof(Image_Click_OpenUrl)}]");
            if (!(image.Tag is UIControl uiCtrl))
                return;

            Debug.Assert(uiCtrl.Type == UIControlType.Image);
            UIInfo_Image info = uiCtrl.Info.Cast<UIInfo_Image>();

            FileHelper.OpenUri(info.Url);
        }

        /// <summary>
        /// Open picture with external viewer
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void Image_Click_OpenImage(object sender, RoutedEventArgs e)
        {
            Image image = sender as Image;
            Debug.Assert(image != null, $"Wrong sender in [{nameof(Image_Click_OpenImage)}]");
            if (!(image.Tag is UIControl uiCtrl))
                return;

            Debug.Assert(uiCtrl.Type == UIControlType.Image);
            if (!ImageHelper.GetImageType(uiCtrl.Text, out ImageHelper.ImageType t))
            {
                App.Logger.SystemWrite(new LogInfo(LogState.Error, $"Image [{Path.GetExtension(uiCtrl.Text)}] is not supported"));
                return;
            }

            string path = Path.ChangeExtension(Path.GetTempFileName(), "." + t.ToString().ToLower());

            using (MemoryStream ms = EncodedFile.ExtractInterface(uiCtrl.Section.Script, uiCtrl.Text))
            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                ms.Position = 0;
                ms.CopyTo(fs);
            }

            FileHelper.OpenPath(path);
        }
        #endregion

        #region TextFile
        public void RenderTextFile(UIControl uiCtrl)
        {
            UIInfo_TextFile info = uiCtrl.Info.Cast<UIInfo_TextFile>();

            TextBox textBox = new TextBox
            {
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                IsReadOnly = true,
                FontSize = CalcFontPointScale(),
            };

            if (!uiCtrl.Text.Equals(UIInfo_TextFile.NoResource, StringComparison.OrdinalIgnoreCase))
            {
                if (!EncodedFile.ContainsInterface(uiCtrl.Section.Script, uiCtrl.Text))
                { // Wrong encoded text
                    string errMsg = $"Unable to find encoded text [{uiCtrl.Text}]";
                    textBox.Text = errMsg;
                    App.Logger.SystemWrite(new LogInfo(LogState.Error, $"{errMsg} ({uiCtrl.RawLine})"));
                }
                else
                {
                    using (MemoryStream ms = EncodedFile.ExtractInterface(uiCtrl.Section.Script, uiCtrl.Text))
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
            SetEditModeProperties(_r, textBox, uiCtrl);
            DrawToCanvas(_r, textBox, uiCtrl);
        }
        #endregion

        #region Button
        public RenderCleanInfo? RenderButton(UIControl uiCtrl)
        {
            UIInfo_Button info = uiCtrl.Info.Cast<UIInfo_Button>();

            Button button = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            if (_r.ViewMode)
                ManageButtonEvent(button, true, info.SectionName);

            if (info.Picture != null &&
                !info.Picture.Equals(UIInfo_Button.NoPicture, StringComparison.OrdinalIgnoreCase) &&
                EncodedFile.ContainsInterface(uiCtrl.Section.Script, info.Picture))
            { // Has Picture
                if (!ImageHelper.GetImageType(info.Picture, out ImageHelper.ImageType type))
                    return null;

                Image image = new Image
                {
                    StretchDirection = StretchDirection.DownOnly,
                    Stretch = Stretch.Uniform,
                    UseLayoutRounding = true,
                };
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

                using (MemoryStream ms = EncodedFile.ExtractInterface(uiCtrl.Section.Script, info.Picture))
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
                    StackPanel panel = new StackPanel
                    {
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Orientation = Orientation.Horizontal,
                    };

                    TextBlock text = new TextBlock
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
            SetEditModeProperties(_r, button, uiCtrl);
            DrawToCanvas(_r, button, uiCtrl);

            return new RenderCleanInfo(uiCtrl, button, info.SectionName);
        }

        public void ManageButtonEvent(Button button, bool addMode, string sectionName)
        {
            Debug.Assert(button != null, $"null in [{nameof(ManageButtonEvent)}]");
            if (addMode)
            {
                if (sectionName != null)
                    button.Click += Button_Click;
            }
            else
            {
                if (sectionName != null)
                    button.Click -= Button_Click;
            }
        }

        public void Button_Click(object sender, RoutedEventArgs e)
        {
            ComboBox box = sender as ComboBox;
            Debug.Assert(box != null, $"Wrong sender in [{nameof(ComboBox_SelectionChanged)}]");
            if (!(box.Tag is UIControl uiCtrl))
                return;

            Debug.Assert(uiCtrl.Type == UIControlType.Button);
            UIInfo_Button info = uiCtrl.Info.Cast<UIInfo_Button>();
            RunOneSection(uiCtrl.Type, uiCtrl.Key, info.SectionName, info.HideProgress);
        }
        #endregion

        #region WebLabel
        public RenderCleanInfo RenderWebLabel(UIControl uiCtrl)
        {
            UIInfo_WebLabel info = uiCtrl.Info.Cast<UIInfo_WebLabel>();

            TextBlock block = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontSize = CalcFontPointScale(),
            };

            Hyperlink link = new Hyperlink {NavigateUri = new Uri(info.Url)};
            link.Inlines.Add(uiCtrl.Text);
            if (_r.ViewMode)
                ManageWebLabelEvent(link, true);
            block.Inlines.Add(link);

            string toolTip = AppendUrlToToolTip(info.ToolTip, info.Url);
            SetToolTip(block, toolTip);
            SetEditModeProperties(_r, block, uiCtrl);

            if (IgnoreWidthOfWebLabel && _r.ViewMode)
            { // Disable this in edit mode to encourage script developer address this issue
                Rect rect = new Rect(uiCtrl.X, uiCtrl.Y, block.Width, uiCtrl.Height);
                DrawToCanvas(_r, block, uiCtrl, rect);
            }
            else
            {
                DrawToCanvas(_r, block, uiCtrl);
            }

            return new RenderCleanInfo(uiCtrl, link);
        }

        public void ManageWebLabelEvent(Hyperlink link, bool addMode)
        {
            Debug.Assert(link != null, $"null in [{nameof(ManageButtonEvent)}]");
            if (addMode)
                link.RequestNavigate += WebLabel_RequestNavigate;
            else
                link.RequestNavigate -= WebLabel_RequestNavigate;
        }

        public void WebLabel_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            FileHelper.OpenUri(e.Uri.AbsoluteUri);
        }
        #endregion

        #region RadioButton
        public RenderCleanInfo RenderRadioButton(UIControl uiCtrl)
        {
            UIInfo_RadioButton info = uiCtrl.Info.Cast<UIInfo_RadioButton>();

            double fontSize = CalcFontPointScale();

            RadioButton radio = new RadioButton
            {
                GroupName = _r.Script.RealPath,
                Content = uiCtrl.Text,
                FontSize = fontSize,
                IsChecked = info.Selected,
                VerticalContentAlignment = VerticalAlignment.Center,
            };

            if (_r.ViewMode)
                ManageRadioButtonEvent(radio, true, info.SectionName);

            SetToolTip(radio, info.ToolTip);
            SetEditModeProperties(_r, radio, uiCtrl);
            DrawToCanvas(_r, radio, uiCtrl);

            return new RenderCleanInfo(uiCtrl, radio, info.SectionName);
        }

        public void ManageRadioButtonEvent(RadioButton radio, bool addMode, string sectionName)
        {
            Debug.Assert(radio != null, $"null in [{nameof(ManageRadioButtonEvent)}]");
            if (addMode)
            {
                radio.Checked += RadioButton_Checked;
                if (sectionName != null)
                    radio.Click += Button_Click;
            }
            else
            {
                radio.Checked -= RadioButton_Checked;
                if (sectionName != null)
                    radio.Click -= Button_Click;
            }
        }

        public void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            RadioButton radio = sender as RadioButton;
            Debug.Assert(radio != null, $"Wrong sender in [{nameof(RadioButton_Checked)}]");
            if (!(radio.Tag is UIControl uiCtrl))
                return;

            Debug.Assert(uiCtrl.Type == UIControlType.RadioButton);
            UIInfo_RadioButton info = uiCtrl.Info.Cast<UIInfo_RadioButton>();

            info.Selected = true;

            // Uncheck the other RadioButtons
            List<UIControl> updateList = _radioButtons.Where(x => !x.Key.Equals(uiCtrl.Key, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (UIControl uncheck in updateList)
            {
                UIInfo_RadioButton unInfo = uncheck.Info.Cast<UIInfo_RadioButton>();
                unInfo.Selected = false;
            }

            updateList.Add(uiCtrl);
            UIControl.Update(updateList);
        }

        public void RadioButton_Click(object sender, RoutedEventArgs e)
        {
            RadioButton radio = sender as RadioButton;
            Debug.Assert(radio != null, $"Wrong sender in [{nameof(RadioButton_Click)}]");
            if (!(radio.Tag is UIControl uiCtrl))
                return;

            Debug.Assert(uiCtrl.Type == UIControlType.RadioButton);
            UIInfo_RadioButton info = uiCtrl.Info.Cast<UIInfo_RadioButton>();
            RunOneSection(uiCtrl.Type, uiCtrl.Key, info.SectionName, info.HideProgress);
        }
        #endregion

        #region Bevel
        public void RenderBevel(UIControl uiCtrl)
        {
            UIInfo_Bevel info = uiCtrl.Info.Cast<UIInfo_Bevel>();

            Border bevel = new Border
            {
                IsHitTestVisible = false, // Focus is not given when clicked
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0.7),
                BorderBrush = Brushes.Gray,
            };

            if (!_r.ViewMode)
            {
                bevel.IsHitTestVisible = true; // Focus is given when clicked
                Panel.SetZIndex(bevel, -1); // Should have lowest z-index
            }

            SetToolTip(bevel, info.ToolTip);
            if (info.FontSize == null)
            { // No caption (WinBuilder compatible)
                SetEditModeProperties(_r, bevel, uiCtrl);
                DrawToCanvas(_r, bevel, uiCtrl);
            }
            else
            { // PEBakery Extension - see https://github.com/pebakery/pebakery/issues/34
                int fontSize = info.FontSize ?? UIControl.DefaultFontPoint;

                Border textBorder = new Border
                {
                    // Don't use info.FontSize for border thickness. It throws off X Pos.
                    BorderThickness = new Thickness(CalcFontPointScale() / 3),
                    BorderBrush = Brushes.Transparent,
                };

                if (!_r.ViewMode) // Focus is given when clicked
                    textBorder.IsHitTestVisible = true;

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

                Canvas subCanvas = new Canvas();
                Canvas.SetLeft(bevel, 0);
                Canvas.SetTop(bevel, 0);
                bevel.Width = uiCtrl.Rect.Width;
                bevel.Height = uiCtrl.Rect.Height;
                subCanvas.Children.Add(bevel);
                Canvas.SetLeft(textBorder, CalcFontPointScale(fontSize) / 3);
                Canvas.SetTop(textBorder, -1 * CalcFontPointScale(fontSize));
                subCanvas.Children.Add(textBorder);
                SetEditModeProperties(_r, subCanvas, uiCtrl);
                DrawToCanvas(_r, subCanvas, uiCtrl);
            }
        }
        #endregion

        #region FileBox
        public RenderCleanInfo RenderFileBox(UIControl uiCtrl, Variables variables)
        {
            UIInfo_FileBox info = uiCtrl.Info.Cast<UIInfo_FileBox>();

            TextBox box = new TextBox
            {
                Text = uiCtrl.Text,
                FontSize = CalcFontPointScale(),
                Margin = new Thickness(0, 0, 5, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
            };

            Button button = new Button
            {
                FontSize = CalcFontPointScale(),
                Content = new PackIconMaterial
                {
                    Kind = PackIconMaterialKind.FolderOpen,
                    Width = double.NaN,
                    Height = double.NaN,
                },
            };

            if (_r.ViewMode)
                ManageFileBoxEvent(box, button, true);

            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(uiCtrl.Height) });

            Grid.SetColumn(box, 0);
            grid.Children.Add(box);
            Grid.SetColumn(button, 1);
            grid.Children.Add(button);

            SetToolTip(grid, info.ToolTip);
            SetEditModeProperties(_r, grid, uiCtrl);
            DrawToCanvas(_r, grid, uiCtrl);

            return new RenderCleanInfo(uiCtrl, new object[] { box, button });
        }

        public void ManageFileBoxEvent(TextBox box, Button button, bool addMode)
        {
            Debug.Assert(box != null, $"null in [{nameof(ManageFileBoxEvent)}]");
            if (addMode)
            {
                box.TextChanged += FileBox_TextChanged;
                button.Click += FileBox_ButtonClick;
            }
            else
            {
                box.TextChanged -= FileBox_TextChanged;
                button.Click -= FileBox_ButtonClick;
            }
        }

        public void FileBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox box = sender as TextBox;
            Debug.Assert(box != null, $"Wrong sender in [{nameof(FileBox_TextChanged)}]");
            if (!(box.Tag is UIControl uiCtrl))
                return;

            Debug.Assert(uiCtrl.Type == UIControlType.FileBox);
            uiCtrl.Text = box.Text;
            uiCtrl.Update();
        }

        public void FileBox_ButtonClick(object sender, RoutedEventArgs e)
        {
            TextBox box = sender as TextBox;
            Debug.Assert(box != null, $"Wrong sender in [{nameof(FileBox_ButtonClick)}]");
            if (!(box.Tag is UIControl uiCtrl))
                return;

            Debug.Assert(uiCtrl.Type == UIControlType.FileBox);
            UIInfo_FileBox info = uiCtrl.Info.Cast<UIInfo_FileBox>();

            if (info.IsFile)
            { // File
                string currentPath = StringEscaper.Preprocess(_variables, uiCtrl.Text);
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

                string currentPath = StringEscaper.Preprocess(_variables, uiCtrl.Text);
                if (Directory.Exists(currentPath))
                    dialog.SelectedPath = currentPath;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (dialog.ShowDialog(_r.Window) == true)
                    {
                        box.Text = dialog.SelectedPath;
                        if (!dialog.SelectedPath.EndsWith("\\", StringComparison.Ordinal))
                            box.Text += "\\";
                    }
                });
            }
        }
        #endregion

        #region RadioGroup
        public RenderCleanInfo RenderRadioGroup(UIControl uiCtrl)
        {
            UIInfo_RadioGroup info = uiCtrl.Info.Cast<UIInfo_RadioGroup>();

            double fontSize = CalcFontPointScale();

            GroupBox box = new GroupBox
            {
                Header = uiCtrl.Text,
                FontSize = fontSize,
                BorderBrush = Brushes.LightGray,
            };
            SetToolTip(box, info.ToolTip);
            SetEditModeProperties(_r, box, uiCtrl);

            Grid grid = new Grid();
            box.Content = grid;

            RadioButton[] radios = new RadioButton[info.Items.Count];
            for (int i = 0; i < info.Items.Count; i++)
            {
                RadioButton radio = new RadioButton
                {
                    GroupName = $"{_r.Script.RealPath}_{uiCtrl.Key}",
                    Content = info.Items[i],
                    Tag = new Tuple<UIControl, int>(uiCtrl, i),
                    FontSize = fontSize,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    IsChecked = i == info.Selected,
                };

                SetToolTip(radio, info.ToolTip);

                Grid.SetRow(radio, i);
                grid.RowDefinitions.Add(new RowDefinition());
                grid.Children.Add(radio);
                radios[i] = radio;
            }

            if (_r.ViewMode)
                ManageRadioGroupEvent(radios, true, info.SectionName);

            Rect rect = new Rect(uiCtrl.X, uiCtrl.Y, uiCtrl.Width, uiCtrl.Height);
            DrawToCanvas(_r, box, uiCtrl, rect);

            return new RenderCleanInfo(uiCtrl, radios.Select(x => (object)x).ToArray());
        }

        public void ManageRadioGroupEvent(RadioButton[] buttons, bool addMode, string sectionName)
        {
            Debug.Assert(buttons != null, $"null in [{nameof(ManageRadioGroupEvent)}]");
            foreach (RadioButton button in buttons)
            {
                if (addMode)
                {
                    button.Checked += RadioGroup_Checked;
                    if (sectionName != null)
                        button.Click += RadioGroup_Click;
                }
                else
                {
                    button.Checked -= RadioGroup_Checked;
                    if (sectionName != null)
                        button.Click -= RadioGroup_Click;
                }
            }   
        }

        public void RadioGroup_Checked(object sender, RoutedEventArgs e)
        {
            RadioButton button = sender as RadioButton;
            Debug.Assert(button != null, $"Wrong sender in [{nameof(RadioGroup_Checked)}]");
            if (!(button.Tag is Tuple<UIControl, int> tup))
                return;

            UIControl uiCtrl = tup.Item1;
            int idx = tup.Item2;

            Debug.Assert(uiCtrl.Type == UIControlType.CheckBox);
            UIInfo_RadioGroup info = uiCtrl.Info.Cast<UIInfo_RadioGroup>();

            info.Selected = idx;
            uiCtrl.Update();
        }

        public void RadioGroup_Click(object sender, RoutedEventArgs e)
        {
            RadioButton button = sender as RadioButton;
            Debug.Assert(button != null, $"Wrong sender in [{nameof(RadioGroup_Click)}]");
            if (!(button.Tag is Tuple<UIControl, int> tup))
                return;

            UIControl uiCtrl = tup.Item1;

            Debug.Assert(uiCtrl.Type == UIControlType.RadioGroup);
            UIInfo_RadioGroup info = uiCtrl.Info.Cast<UIInfo_RadioGroup>();
            RunOneSection(uiCtrl.Type, uiCtrl.Key, info.SectionName, info.HideProgress);
        }
        #endregion
        #endregion

        #region Render Utility
        private static void InitCanvas(Canvas canvas)
        {
            canvas.Children.Clear();
            canvas.Width = double.NaN;
            canvas.Height = double.NaN;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void DrawToCanvas(RenderInfo r, FrameworkElement element, UIControl uiCtrl)
        {
            DrawToCanvas(r.Canvas, element, uiCtrl, uiCtrl.Rect);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void DrawToCanvas(RenderInfo r, FrameworkElement element, UIControl uiCtrl, Rect rect)
        {
            DrawToCanvas(r.Canvas, element, uiCtrl, rect);
        }

        public static void DrawToCanvas(Canvas canvas, FrameworkElement element, UIControl uiCtrl, Rect rect)
        {
            // Add Tag to uiCtrl
            element.Tag = uiCtrl;
            DrawToCanvas(canvas, element, rect);
        }

        public static void DrawToCanvas(Canvas canvas, FrameworkElement element, Rect rect)
        {
            // Set Position
            Canvas.SetLeft(element, rect.X);
            Canvas.SetTop(element, rect.Y);
            element.Width = rect.Width;
            element.Height = rect.Height;

            // Expand canvas if needed
            canvas.Children.Add(element);
            if (double.IsNaN(canvas.Width) || canvas.Width < rect.X + rect.Width)
                canvas.Width = rect.X + rect.Width;
            if (double.IsNaN(canvas.Height) || canvas.Height < rect.Y + rect.Height)
                canvas.Height = rect.Y + rect.Height;
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
        #endregion

        #region RunOneSection
        private void RunOneSection(UIControlType ctrlType, string ctrlKey, string sectionName, bool hideProgress)
        {
            if (_r.Script.Sections.ContainsKey(sectionName)) // Only if section exists
            {
                ScriptSection targetSection = _r.Script.Sections[sectionName];
                InternalRunOneSection(targetSection, $"{_r.Script.Title} - {ctrlType} [{ctrlKey}]", hideProgress);
            }
            else
            {
                App.Logger.SystemWrite(new LogInfo(LogState.Error, $"Section [{sectionName}] does not exist"));
            }
        }

        private static async void InternalRunOneSection(ScriptSection section, string logMsg, bool hideProgress)
        {
            if (Engine.WorkingLock == 0)
            {
                Interlocked.Increment(ref Engine.WorkingLock);

                Logger logger = App.Logger;

                MainViewModel mainModel = null;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (!(Application.Current.MainWindow is MainWindow w))
                        return;

                    mainModel = w.Model;

                    // Populate BuildTree
                    if (!hideProgress)
                    {
                        w.Model.BuildTreeItems.Clear();
                        ProjectTreeItemModel itemRoot = w.PopulateOneTreeItem(section.Script, null, null);
                        w.Model.BuildTreeItems.Add(itemRoot);
                        w.CurBuildTree = null;
                    }
                });

                mainModel.WorkInProgress = true;

                EngineState s = new EngineState(section.Project, logger, mainModel, EngineMode.RunMainAndOne, section.Script, section.Name);
                s.SetOptions(App.Setting);
                if (s.LogMode == LogMode.PartDefer) // Use FullDefer in UIRenderer
                    s.LogMode = LogMode.FullDefer;

                Engine.WorkingEngine = new Engine(s);

                // Build Start, Switch to Build View
                if (!hideProgress)
                    mainModel.SwitchNormalBuildInterface = false;

                // Set StatusBar Text
                CancellationTokenSource ct = new CancellationTokenSource();
                Task printStatus = MainWindow.PrintBuildElapsedStatus(logMsg, mainModel, s.Watch, ct.Token);

                // Run
                await Engine.WorkingEngine.Run(logMsg);

                // Cancel and Wait until PrintBuildElapsedStatus stops
                ct.Cancel();
                await printStatus;
                mainModel.StatusBarText = $"{logMsg} took {s.Watch.Elapsed:h\\:mm\\:ss}";

                // Build Ended, Switch to Normal View
                if (!hideProgress)
                    mainModel.SwitchNormalBuildInterface = true;

                // Flush FullDelayedLogs
                if (s.LogMode == LogMode.FullDefer)
                {
                    DeferredLogging deferred = logger.Deferred;
                    deferred.FlushFullDeferred(s);
                }

                // Turn off ProgressRing
                mainModel.BuildTreeItems.Clear();
                mainModel.WorkInProgress = false;

                Engine.WorkingEngine = null;
                Interlocked.Decrement(ref Engine.WorkingLock);

                if (!hideProgress)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MainWindow w = Application.Current.MainWindow as MainWindow;
                        w?.DisplayScript(w.CurMainTree.Script);
                    });
                }
            }
        }
        #endregion
    }
    #endregion

    #region struct RenderInfo
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

        public RenderInfo(Canvas canvas, Window window, Script script, double scale, bool viewMode)
        {
            ScaleFactor = scale;
            Canvas = canvas;
            Window = window;
            Script = script;
            ViewMode = viewMode;
        }
    }
    #endregion

    #region struct RenderCleanInfo
    public struct RenderCleanInfo
    {
        public readonly UIControl UICtrl;
        public readonly object Element;
        public readonly object[] Elements;
        public readonly object Tag;

        public RenderCleanInfo(UIControl uiCtrl, object element, object tag = null)
        {
            UICtrl = uiCtrl;
            Element = element;
            Elements = null;
            Tag = tag;
        }

        public RenderCleanInfo(UIControl uiCtrl, object[] elements, object tag = null)
        {
            UICtrl = uiCtrl;
            Element = null;
            Elements = elements;
            Tag = tag;
        }
    }
    #endregion
}
