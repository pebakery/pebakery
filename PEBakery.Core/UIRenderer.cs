/*
    Copyright (C) 2016-2019 Hajin Jang
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
using PEBakery.Core.ViewModels;
using PEBakery.Core.WpfControls;
using PEBakery.Helper;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace PEBakery.Core
{
    #region UIRenderer
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class UIRenderer
    {
        #region Fields and Properties
        public const int MaxUrlDisplayLen = 50 - 3;
        private readonly Variables _variables;

        private readonly Canvas _canvas;
        private readonly Window _window; // Can be null
        private readonly Script _sc;
        /// <summary>
        /// Custom scale factor of interface. Independent from system monitor dpi.
        /// </summary>
        public double ScaleFactor;
        /// <summary>
        /// true in MainWindow, false in ScriptEditWindow
        /// </summary>
        private readonly bool _viewMode;
        // Compatibility Option
        private readonly bool _ignoreWidthOfWebLabel = false;

        public readonly List<UIControl> UICtrls;
        private UIControl[] _visibleCtrls => _viewMode ? UICtrls.Where(x => x.Visibility).ToArray() : UICtrls.ToArray();
        private UIControl[] _radioButtons => _visibleCtrls.Where(x => x.Type == UIControlType.RadioButton).ToArray();
        private readonly List<RenderCleanInfo> _cleanInfos = new List<RenderCleanInfo>();
        #endregion

        #region Constructor
        public UIRenderer(Canvas canvas, Window window, Script script, double scaleFactor, bool viewMode, bool compatWebLabel)
        {
            _variables = script.Project.Variables;
            _canvas = canvas;
            _window = window;
            _sc = script;
            ScaleFactor = scaleFactor;
            _viewMode = viewMode;
            _ignoreWidthOfWebLabel = compatWebLabel;

            (List<UIControl> uiCtrls, List<LogInfo> errLogs) = LoadInterfaces(script);
            UICtrls = uiCtrls ?? new List<UIControl>(0);

            Global.Logger.SystemWrite(errLogs);
        }

        public UIRenderer(Canvas canvas, Window window, Script script, List<UIControl> uiCtrls, double scaleFactor, bool viewMode, bool compatWebLabel)
        {
            _variables = script.Project.Variables;
            _canvas = canvas;
            _window = window;
            _sc = script;
            ScaleFactor = scaleFactor;
            _viewMode = viewMode;
            _ignoreWidthOfWebLabel = compatWebLabel;

            UICtrls = uiCtrls ?? new List<UIControl>(0);
        }
        #endregion

        #region Load Utility
        /// <summary>
        /// Load script interface's UIControl list.
        /// </summary>
        /// <param name="sc">Script to get interface.</param>
        /// <param name="sectionName">Set to null for auto detection.</param>
        /// <returns></returns>
        public static (List<UIControl>, List<LogInfo>) LoadInterfaces(Script sc, string sectionName = null)
        {
            // Check if script has custom interface section
            string ifaceSectionName = sectionName ?? sc.InterfaceSectionName;

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
        #endregion

        #region Render
        public void Render()
        {
            if (UICtrls == null) // This script does not have 'Interface' section
                return;

            InitCanvas(_canvas);
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
                            Global.Logger.SystemWrite(new LogInfo(LogState.Error, $"Unknown UIControlType [{uiCtrl.Type}] ({uiCtrl.RawLine})"));
                            break;
                    }

                    // In edit mode (ScriptEditWindow), all event handler is disabled -> no need to clean events
                    if (_viewMode && clean is RenderCleanInfo ci)
                        _cleanInfos.Add(ci);
                }
                catch (Exception e)
                {
                    // Log failure
                    Global.Logger.SystemWrite(new LogInfo(LogState.Error, $"{Logger.LogExceptionMessage(e)} [{uiCtrl.RawLine}]"));
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
            InitCanvas(_canvas);
        }
        #endregion

        #region Methods for Control
        #region TextBox
        public RenderCleanInfo RenderTextBox(UIControl uiCtrl)
        {
            // WB082 textbox control's y coord is of textbox's, not textlabel's.
            UIInfo_TextBox info = uiCtrl.Info.Cast<UIInfo_TextBox>();

            TextBox box = new TextBox
            {
                Text = StringEscaper.Unescape(info.Value),
                Height = uiCtrl.Height,
                FontSize = CalcFontPointScale(),
                VerticalContentAlignment = VerticalAlignment.Center,
                Tag = uiCtrl,
            };

            if (_viewMode)
                ManageTextBoxEvent(box, true);

            if (uiCtrl.Text.Length == 0)
            { // No caption
                SetToolTip(box, info.ToolTip);
                SetEditModeProperties(box, uiCtrl);
                DrawToCanvas(box, uiCtrl);
            }
            else
            { // Print caption
                TextBlock block = new TextBlock
                {
                    Text = StringEscaper.Unescape(uiCtrl.Text),
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
                SetEditModeProperties(grid, uiCtrl);

                Rect gridRect = new Rect(uiCtrl.X, uiCtrl.Y - UIInfo_TextBox.AddWidth, uiCtrl.Width, uiCtrl.Height + UIInfo_TextBox.AddWidth);
                DrawToCanvas(grid, uiCtrl, gridRect);
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
            UIControl uiCtrl = box.Tag as UIControl;
            Debug.Assert(uiCtrl != null, $"Wrong tag in [{nameof(TextBox_LostFocus)}]");
            Debug.Assert(uiCtrl.Type == UIControlType.TextBox, $"Wrong UIControlType in [{nameof(TextBox_LostFocus)}]");
            UIInfo_TextBox info = uiCtrl.Info.Cast<UIInfo_TextBox>();

            info.Value = StringEscaper.Escape(box.Text);
            uiCtrl.Update();
        }
        #endregion

        #region TextLabel
        public void RenderTextLabel(UIControl uiCtrl)
        {
            UIInfo_TextLabel info = uiCtrl.Info.Cast<UIInfo_TextLabel>();

            TextBlock block = new TextBlock
            {
                Text = StringEscaper.Unescape(uiCtrl.Text),
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
            SetEditModeProperties(block, uiCtrl);
            DrawToCanvas(block, uiCtrl);
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

            if (_viewMode)
                ManageNumberBoxEvent(box, true);

            SetToolTip(box, info.ToolTip);
            SetEditModeProperties(box, uiCtrl);
            DrawToCanvas(box, uiCtrl);

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
            UIControl uiCtrl = box.Tag as UIControl;
            Debug.Assert(uiCtrl != null, $"Wrong tag in [{nameof(NumberBox_ValueChanged)}]");
            Debug.Assert(uiCtrl.Type == UIControlType.NumberBox, $"Wrong UIControlType in [{nameof(NumberBox_ValueChanged)}]");
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
                Content = StringEscaper.Unescape(uiCtrl.Text),
                IsChecked = info.Value,
                FontSize = CalcFontPointScale(),
                VerticalContentAlignment = VerticalAlignment.Center,
            };

            if (_viewMode)
                ManageCheckBoxEvent(box, true, info.SectionName);

            SetToolTip(box, info.ToolTip);
            SetEditModeProperties(box, uiCtrl);
            DrawToCanvas(box, uiCtrl);

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
            UIControl uiCtrl = box.Tag as UIControl;
            Debug.Assert(uiCtrl != null, $"Wrong tag in [{nameof(CheckBox_Checked)}]");
            Debug.Assert(uiCtrl.Type == UIControlType.CheckBox, $"Wrong UIControlType in [{nameof(CheckBox_Checked)}]");
            UIInfo_CheckBox info = uiCtrl.Info.Cast<UIInfo_CheckBox>();

            info.Value = true;
            uiCtrl.Update();
        }

        public void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            CheckBox box = sender as CheckBox;
            Debug.Assert(box != null, $"Wrong sender in [{nameof(CheckBox_Unchecked)}]");
            UIControl uiCtrl = box.Tag as UIControl;
            Debug.Assert(uiCtrl != null, $"Wrong tag in [{nameof(CheckBox_Unchecked)}]");
            Debug.Assert(uiCtrl.Type == UIControlType.CheckBox, $"Wrong UIControlType in [{nameof(CheckBox_Unchecked)}]");
            UIInfo_CheckBox info = uiCtrl.Info.Cast<UIInfo_CheckBox>();

            info.Value = false;
            uiCtrl.Update();
        }

        public void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            CheckBox box = sender as CheckBox;
            Debug.Assert(box != null, $"Wrong sender in [{nameof(CheckBox_Click)}]");
            UIControl uiCtrl = box.Tag as UIControl;
            Debug.Assert(uiCtrl != null, $"Wrong tag in [{nameof(CheckBox_Click)}]");
            Debug.Assert(uiCtrl.Type == UIControlType.CheckBox, $"Wrong UIControlType in [{nameof(CheckBox_Click)}]");

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
                FontFamily = Global.Setting.Interface.MonospacedFontFamily,
                ItemsSource = new ObservableCollection<string>(info.Items.Select(x => StringEscaper.Unescape(x))),
                SelectedIndex = info.Index,
                VerticalContentAlignment = VerticalAlignment.Center,
            };

            if (_viewMode)
                ManageComboBoxEvent(box, true, info.SectionName);

            SetToolTip(box, info.ToolTip);
            SetEditModeProperties(box, uiCtrl);
            DrawToCanvas(box, uiCtrl);

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
            Debug.Assert(box != null, $"Wrong sender in [{nameof(ComboBox_LostFocus)}]");
            UIControl uiCtrl = box.Tag as UIControl;
            Debug.Assert(uiCtrl != null, $"Wrong tag in [{nameof(ComboBox_LostFocus)}]");
            Debug.Assert(uiCtrl.Type == UIControlType.ComboBox, $"Wrong UIControlType in [{nameof(ComboBox_LostFocus)}]");
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
            UIControl uiCtrl = box.Tag as UIControl;
            Debug.Assert(uiCtrl != null, $"Wrong tag in [{nameof(ComboBox_SelectionChanged)}]");
            Debug.Assert(uiCtrl.Type == UIControlType.ComboBox, $"Wrong UIControlType in [{nameof(ComboBox_SelectionChanged)}]");

            UIInfo_ComboBox info = uiCtrl.Info.Cast<UIInfo_ComboBox>();
            RunOneSection(uiCtrl.Type, uiCtrl.Key, info.SectionName, info.HideProgress);
        }
        #endregion

        #region Image
        public RenderCleanInfo? RenderImage(UIControl uiCtrl)
        {
            UIInfo_Image info = uiCtrl.Info.Cast<UIInfo_Image>();

            string imageSection = uiCtrl.Text;
            if (imageSection.Equals(UIInfo_Image.NoResource, StringComparison.OrdinalIgnoreCase))
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
                SetEditModeProperties(border, uiCtrl);
                DrawToCanvas(border, uiCtrl);
                return null;
            }

            if (!EncodedFile.ContainsInterface(uiCtrl.Section.Script, imageSection))
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
                SetEditModeProperties(border, uiCtrl);
                DrawToCanvas(border, uiCtrl);

                Global.Logger.SystemWrite(new LogInfo(LogState.Error, $"Unable to find encoded image [{imageSection}] ({uiCtrl.RawLine})"));
                return null;
            }

            if (!ImageHelper.GetImageFormat(imageSection, out ImageHelper.ImageFormat imgType))
            {
                Global.Logger.SystemWrite(new LogInfo(LogState.Error, $"Image [{Path.GetExtension(imageSection)}] is not supported"));
                return null;
            }

            if (_viewMode)
            {
                Brush brush;

                // Use EncodedFile.ExtractInterface for maximum performance (at the cost of high memory usage)
                using (MemoryStream ms = EncodedFile.ExtractInterface(uiCtrl.Section.Script, imageSection))
                {
                    switch (imgType)
                    {
                        case ImageHelper.ImageFormat.Svg:
                            brush = new DrawingBrush { Drawing = ImageHelper.SvgToDrawingGroup(ms) };
                            break;
                        default:
                            brush = ImageHelper.ImageToImageBrush(ms);
                            break;
                    }
                }

                Style imageButtonStyle = Application.Current.FindResource("ImageButtonStyle") as Style;
                Debug.Assert(imageButtonStyle != null);
                Button button = new Button
                {
                    Style = imageButtonStyle,
                    Background = brush,
                };

                bool hasUrl = false;
                string url = null;
                if (!string.IsNullOrEmpty(info.Url))
                {
                    url = StringEscaper.Unescape(info.Url);
                    hasUrl = StringEscaper.IsUrlValid(url);
                }

                string toolTip = info.ToolTip;
                ManageImageEvent(button, true, hasUrl);
                if (hasUrl) // Open URL
                    toolTip = AppendUrlToToolTip(info.ToolTip, url);

                SetToolTip(button, toolTip);
                DrawToCanvas(button, uiCtrl);
                return new RenderCleanInfo(uiCtrl, button, hasUrl);
            }
            else
            {
                FrameworkElement element;

                // Use EncodedFile.ExtractInterface for maximum performance (at the cost of high memory usage)
                using (MemoryStream ms = EncodedFile.ExtractInterface(uiCtrl.Section.Script, imageSection))
                {
                    switch (imgType)
                    {
                        case ImageHelper.ImageFormat.Svg:
                            Border border = new Border
                            {
                                Background = ImageHelper.SvgToDrawingBrush(ms),
                                UseLayoutRounding = true,
                            };
                            element = border;
                            break;
                        default:
                            Image image = new Image
                            {
                                StretchDirection = StretchDirection.Both,
                                Stretch = Stretch.Fill,
                                UseLayoutRounding = true,
                                Source = ImageHelper.ImageToBitmapImage(ms),
                            };
                            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
                            element = image;
                            break;
                    }
                }

                SetToolTip(element, info.ToolTip);
                SetEditModeProperties(element, uiCtrl);
                DrawToCanvas(element, uiCtrl);
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
            Button button = sender as Button;
            Debug.Assert(button != null, $"Wrong sender in [{nameof(Image_Click_OpenUrl)}]");
            UIControl uiCtrl = button.Tag as UIControl;
            Debug.Assert(uiCtrl != null, $"Wrong tag in [{nameof(Image_Click_OpenUrl)}]");
            Debug.Assert(uiCtrl.Type == UIControlType.Image, $"Wrong UIControlType in [{nameof(Image_Click_OpenUrl)}]");
            UIInfo_Image info = uiCtrl.Info.Cast<UIInfo_Image>();

            string url = StringEscaper.Unescape(info.Url);
            Debug.Assert(StringEscaper.IsUrlValid(url), $"Invalid URL [{url}]");
            FileHelper.OpenUri(url);
        }

        /// <summary>
        /// Open picture with external viewer
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void Image_Click_OpenImage(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            Debug.Assert(button != null, $"Wrong sender in [{nameof(Image_Click_OpenImage)}]");
            UIControl uiCtrl = button.Tag as UIControl;
            Debug.Assert(uiCtrl != null, $"Wrong tag in [{nameof(Image_Click_OpenImage)}]");
            Debug.Assert(uiCtrl.Type == UIControlType.Image, $"Wrong UIControlType in [{nameof(Image_Click_OpenImage)}]");

            string imageSection = StringEscaper.Unescape(uiCtrl.Text);
            if (!ImageHelper.GetImageFormat(imageSection, out _))
            {
                Global.Logger.SystemWrite(new LogInfo(LogState.Error, $"Image format [{Path.GetExtension(imageSection)}] is not supported"));
                return;
            }

            try
            {
                string imageFile;
                using (MemoryStream ms = EncodedFile.ExtractInterface(uiCtrl.Section.Script, imageSection))
                {
                    // Do not clear tempDir right after calling OpenPath(). Doing this will trick the image viewer.
                    // Instead, leave it to Global.Cleanup() when program is exited.
                    string tempDir = FileHelper.GetTempDir();
                    imageFile = Path.Combine(tempDir, imageSection);
                    using (FileStream fs = new FileStream(imageFile, FileMode.Create, FileAccess.Write))
                    {
                        ms.Position = 0;
                        ms.CopyTo(fs);
                    }
                }
                FileHelper.OpenPath(imageFile);
            }
            catch (Exception ex)
            {
                Global.Logger.SystemWrite(new LogInfo(LogState.Error, ex));
            }
        }
        #endregion

        #region TextFile
        public void RenderTextFile(UIControl uiCtrl)
        {
            UIInfo_TextFile info = uiCtrl.Info.Cast<UIInfo_TextFile>();

            string textSection = uiCtrl.Text;
            TextBoxBase box;

            if (textSection.Equals(UIInfo_TextFile.NoResource, StringComparison.OrdinalIgnoreCase))
            {
                box = new TextBox { IsReadOnly = true };
            }
            else
            {
                string ext = Path.GetExtension(textSection);
                if (ext.Equals(".rtf", StringComparison.OrdinalIgnoreCase))
                { // RichTextBox
                    RichTextBox rtfBox = new RichTextBox
                    {
                        AcceptsReturn = true,
                        IsReadOnly = true,
                        FontSize = CalcFontPointScale(),
                    };

                    TextRange textRange = new TextRange(rtfBox.Document.ContentStart, rtfBox.Document.ContentEnd);
                    using (MemoryStream ms = EncodedFile.ExtractInterface(uiCtrl.Section.Script, textSection))
                    {
                        textRange.Load(ms, DataFormats.Rtf);
                    }

                    box = rtfBox;
                }
                else
                { // TextBox
                    // Even if a file extension is not a ".txt", just display.
                    TextBox textBox = new TextBox
                    {
                        AcceptsReturn = true,
                        IsReadOnly = true,
                        FontSize = CalcFontPointScale(),
                    };

                    if (!EncodedFile.ContainsInterface(uiCtrl.Section.Script, textSection))
                    { // Wrong encoded text
                        string errMsg = $"Unable to find encoded text [{textSection}]";
                        textBox.Text = errMsg;
                        Global.Logger.SystemWrite(new LogInfo(LogState.Error, $"{errMsg} ({uiCtrl.RawLine})"));
                    }
                    else
                    {
                        using (MemoryStream ms = EncodedFile.ExtractInterface(uiCtrl.Section.Script, textSection))
                        {
                            Encoding encoding = EncodingHelper.DetectBom(ms);
                            ms.Position = 0;
                            using (StreamReader sr = new StreamReader(ms, encoding, false))
                            {
                                textBox.Text = sr.ReadToEnd();
                            }
                        }
                    }

                    box = textBox;
                }
            }

            ScrollViewer.SetHorizontalScrollBarVisibility(box, ScrollBarVisibility.Auto);
            ScrollViewer.SetVerticalScrollBarVisibility(box, ScrollBarVisibility.Auto);
            ScrollViewer.SetCanContentScroll(box, true);

            SetToolTip(box, info.ToolTip);
            SetEditModeProperties(box, uiCtrl);
            DrawToCanvas(box, uiCtrl);
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

            if (_viewMode)
                ManageButtonEvent(button, true, info.SectionName);

            string pictureSection = info.Picture;
            if (pictureSection != null &&
                !pictureSection.Equals(UIInfo_Button.NoPicture, StringComparison.OrdinalIgnoreCase) &&
                EncodedFile.ContainsInterface(uiCtrl.Section.Script, pictureSection))
            { // Has Picture
                if (!ImageHelper.GetImageFormat(pictureSection, out ImageHelper.ImageFormat imgType))
                    return null;

                FrameworkElement imageContent;

                // Use EncodedFile.ExtractInterface for maximum performance (at the cost of high memory usage)
                using (MemoryStream ms = EncodedFile.ExtractInterface(uiCtrl.Section.Script, pictureSection))
                {
                    switch (imgType)
                    {
                        case ImageHelper.ImageFormat.Svg:
                            DrawingGroup svgDrawing = ImageHelper.SvgToDrawingGroup(ms);
                            Rect svgSize = svgDrawing.Bounds;
                            (double width, double height) = ImageHelper.StretchSizeAspectRatio(svgSize.Width, svgSize.Height, uiCtrl.Width, uiCtrl.Height);
                            Border border = new Border
                            {
                                Width = width,
                                Height = height,
                                HorizontalAlignment = HorizontalAlignment.Center,
                                VerticalAlignment = VerticalAlignment.Center,
                                UseLayoutRounding = true,
                                Background = new DrawingBrush { Drawing = svgDrawing },
                            };
                            imageContent = border;
                            break;
                        case ImageHelper.ImageFormat.Bmp:
                            BitmapSource srcBitmap = ImageHelper.ImageToBitmapImage(ms);
                            BitmapSource newBitmap = ImageHelper.MaskWhiteAsTransparent(srcBitmap);
                            Image bitmapImage = new Image
                            {
                                Width = newBitmap.PixelWidth,
                                Height = newBitmap.PixelHeight,
                                Stretch = Stretch.Uniform, // Ignore image's DPI
                                HorizontalAlignment = HorizontalAlignment.Center,
                                VerticalAlignment = VerticalAlignment.Center,
                                UseLayoutRounding = true,
                                Source = newBitmap,
                            };
                            RenderOptions.SetBitmapScalingMode(bitmapImage, BitmapScalingMode.HighQuality);
                            imageContent = bitmapImage;
                            break;
                        default:
                            BitmapImage bitmap = ImageHelper.ImageToBitmapImage(ms);
                            Image image = new Image
                            {
                                Width = bitmap.PixelWidth,
                                Height = bitmap.PixelHeight,
                                Stretch = Stretch.Uniform, // Ignore image's DPI
                                HorizontalAlignment = HorizontalAlignment.Center,
                                VerticalAlignment = VerticalAlignment.Center,
                                UseLayoutRounding = true,

                                Source = bitmap,
                            };
                            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
                            imageContent = image;
                            break;
                    }
                }

                if (uiCtrl.Text.Length == 0)
                { // No text, just image
                    button.Content = imageContent;
                }
                else
                { // Button has text
                    TextBlock text = new TextBlock
                    {
                        Text = StringEscaper.Unescape(uiCtrl.Text),
                        FontSize = CalcFontPointScale(),
                        Height = double.NaN,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(CalcFontPointScale() / 4, 0, 0, 0),
                    };

                    StackPanel panel = new StackPanel
                    {
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Orientation = Orientation.Horizontal,
                    };

                    panel.Children.Add(imageContent);
                    panel.Children.Add(text);
                    button.Content = panel;
                }
            }
            else
            { // No picture
                button.Content = StringEscaper.Unescape(uiCtrl.Text);
                button.FontSize = CalcFontPointScale();
            }

            SetToolTip(button, info.ToolTip);
            SetEditModeProperties(button, uiCtrl);
            DrawToCanvas(button, uiCtrl);

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
            Button button = sender as Button;
            Debug.Assert(button != null, $"Wrong sender in [{nameof(Button_Click)}]");
            UIControl uiCtrl = button.Tag as UIControl;
            Debug.Assert(uiCtrl != null, $"Wrong tag in [{nameof(Button_Click)}]");
            Debug.Assert(uiCtrl.Type == UIControlType.Button, $"Wrong UIControlType in [{nameof(Button_Click)}]");

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

            string url = StringEscaper.Unescape(info.Url);
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
                throw new InvalidCommandException($"Invalid URL [{url}]");
            Hyperlink link = new Hyperlink { NavigateUri = uri };
            link.Inlines.Add(StringEscaper.Unescape(uiCtrl.Text));
            if (_viewMode)
                ManageWebLabelEvent(link, true);
            block.Inlines.Add(link);

            string toolTip = AppendUrlToToolTip(info.ToolTip, url);
            SetToolTip(block, toolTip);
            SetEditModeProperties(block, uiCtrl);

            if (_ignoreWidthOfWebLabel && _viewMode)
            { // Disable this in edit mode to encourage script developer address this issue
                Rect rect = new Rect(uiCtrl.X, uiCtrl.Y, block.Width, uiCtrl.Height);
                DrawToCanvas(block, uiCtrl, rect);
            }
            else
            {
                DrawToCanvas(block, uiCtrl);
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
                GroupName = _sc.RealPath,
                Content = StringEscaper.Unescape(uiCtrl.Text),
                FontSize = fontSize,
                IsChecked = info.Selected,
                VerticalContentAlignment = VerticalAlignment.Center,
            };

            if (_viewMode)
                ManageRadioButtonEvent(radio, true, info.SectionName);

            SetToolTip(radio, info.ToolTip);
            SetEditModeProperties(radio, uiCtrl);
            DrawToCanvas(radio, uiCtrl);

            return new RenderCleanInfo(uiCtrl, radio, info.SectionName);
        }

        public void ManageRadioButtonEvent(RadioButton radio, bool addMode, string sectionName)
        {
            Debug.Assert(radio != null, $"null in [{nameof(ManageRadioButtonEvent)}]");
            if (addMode)
            {
                radio.Checked += RadioButton_Checked;
                if (sectionName != null)
                    radio.Click += RadioButton_Click;
            }
            else
            {
                radio.Checked -= RadioButton_Checked;
                if (sectionName != null)
                    radio.Click -= RadioButton_Click;
            }
        }

        public void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            RadioButton radio = sender as RadioButton;
            Debug.Assert(radio != null, $"Wrong sender in [{nameof(RadioButton_Checked)}]");
            UIControl uiCtrl = radio.Tag as UIControl;
            Debug.Assert(uiCtrl != null, $"Wrong tag in [{nameof(RadioButton_Checked)}]");
            Debug.Assert(uiCtrl.Type == UIControlType.RadioButton, $"Wrong UIControlType in [{nameof(RadioButton_Checked)}]");
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
            UIControl uiCtrl = radio.Tag as UIControl;
            Debug.Assert(uiCtrl != null, $"Wrong tag in [{nameof(RadioButton_Click)}]");
            Debug.Assert(uiCtrl.Type == UIControlType.RadioButton, $"Wrong UIControlType in [{nameof(RadioButton_Click)}]");

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
                // Focusable only in edit mode
                IsHitTestVisible = !_viewMode,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0.7),
                BorderBrush = Brushes.Gray,
            };

            if (!_viewMode)
                Panel.SetZIndex(bevel, -1); // Should have lowest z-index

            SetToolTip(bevel, info.ToolTip);
            if (info.FontSize == null)
            { // No caption (WinBuilder compatible)
                SetEditModeProperties(bevel, uiCtrl);
                DrawToCanvas(bevel, uiCtrl);
            }
            else
            { // PEBakery Extension - see https://github.com/pebakery/pebakery/issues/34
                int fontSize = info.FontSize ?? UIControl.DefaultFontPoint;

                Border textBorder = new Border
                {
                    // Focusable only in edit mode
                    IsHitTestVisible = !_viewMode,
                    // Don't use info.FontSize for border thickness. It throws off X Pos.
                    BorderThickness = new Thickness(CalcFontPointScale() / 3),
                    BorderBrush = Brushes.Transparent,
                };

                TextBlock textBlock = new TextBlock
                {
                    Text = StringEscaper.Unescape(uiCtrl.Text),
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
                bevel.Width = uiCtrl.Width;
                bevel.Height = uiCtrl.Height;
                subCanvas.Children.Add(bevel);
                Canvas.SetLeft(textBorder, CalcFontPointScale(fontSize) / 3);
                Canvas.SetTop(textBorder, -1 * CalcFontPointScale(fontSize));
                subCanvas.Children.Add(textBorder);
                SetEditModeProperties(subCanvas, uiCtrl);

                if (!_viewMode)
                    Panel.SetZIndex(subCanvas, -1); // Should have lowest z-index

                DrawToCanvas(subCanvas, uiCtrl);
            }
        }
        #endregion

        #region FileBox
        public RenderCleanInfo RenderFileBox(UIControl uiCtrl, Variables variables)
        {
            UIInfo_FileBox info = uiCtrl.Info.Cast<UIInfo_FileBox>();

            TextBox box = new TextBox
            {
                Text = StringEscaper.Unescape(uiCtrl.Text),
                FontSize = CalcFontPointScale(),
                Margin = new Thickness(0, 0, 5, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                Tag = uiCtrl,
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
                Tag = new Tuple<UIControl, TextBox>(uiCtrl, box),
            };

            if (_viewMode)
                ManageFileBoxEvent(box, button, true);

            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(uiCtrl.Height) });

            Grid.SetColumn(box, 0);
            grid.Children.Add(box);
            Grid.SetColumn(button, 1);
            grid.Children.Add(button);

            SetToolTip(grid, info.ToolTip);
            SetEditModeProperties(grid, uiCtrl);
            DrawToCanvas(grid, uiCtrl);

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
            UIControl uiCtrl = box.Tag as UIControl;
            Debug.Assert(uiCtrl != null, $"Wrong tag in [{nameof(FileBox_TextChanged)}]");
            Debug.Assert(uiCtrl.Type == UIControlType.FileBox, $"Wrong UIControlType in [{nameof(FileBox_TextChanged)}]");

            uiCtrl.Text = StringEscaper.Escape(box.Text);
            uiCtrl.Update();
        }

        public void FileBox_ButtonClick(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            Debug.Assert(button != null, $"Wrong sender in [{nameof(FileBox_ButtonClick)}]");
            Tuple<UIControl, TextBox> tup = button.Tag as Tuple<UIControl, TextBox>;
            Debug.Assert(tup != null, $"Wrong tag in [{nameof(FileBox_ButtonClick)}]");

            UIControl uiCtrl = tup.Item1;
            TextBox box = tup.Item2;

            Debug.Assert(uiCtrl.Type == UIControlType.FileBox, $"Wrong UIControlType in [{nameof(FileBox_ButtonClick)}]");
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
                    bool? result = _window == null ? dialog.ShowDialog() : dialog.ShowDialog(_window);
                    if (result == true)
                    {
                        string path = dialog.SelectedPath;
                        if (!path.EndsWith("\\", StringComparison.Ordinal))
                            path += "\\";
                        box.Text = path;
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
            SetEditModeProperties(box, uiCtrl);

            Grid grid = new Grid();
            box.Content = grid;

            RadioButton[] radios = new RadioButton[info.Items.Count];
            for (int i = 0; i < info.Items.Count; i++)
            {
                RadioButton radio = new RadioButton
                {
                    GroupName = $"{_sc.RealPath}_{uiCtrl.Key}",
                    Content = StringEscaper.Unescape(info.Items[i]),
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

            if (_viewMode)
                ManageRadioGroupEvent(radios, true, info.SectionName);

            Rect rect = new Rect(uiCtrl.X, uiCtrl.Y, uiCtrl.Width, uiCtrl.Height);
            DrawToCanvas(box, uiCtrl, rect);

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
            Tuple<UIControl, int> tup = button.Tag as Tuple<UIControl, int>;
            Debug.Assert(tup != null, $"Wrong tag in [{nameof(RadioGroup_Checked)}]");

            UIControl uiCtrl = tup.Item1;
            int idx = tup.Item2;

            Debug.Assert(uiCtrl.Type == UIControlType.RadioGroup, $"Wrong UIControlType in [{nameof(RadioGroup_Checked)}]");
            UIInfo_RadioGroup info = uiCtrl.Info.Cast<UIInfo_RadioGroup>();

            info.Selected = idx;
            uiCtrl.Update();
        }

        public void RadioGroup_Click(object sender, RoutedEventArgs e)
        {
            RadioButton button = sender as RadioButton;
            Debug.Assert(button != null, $"Wrong sender in [{nameof(RadioGroup_Click)}]");
            Tuple<UIControl, int> tup = button.Tag as Tuple<UIControl, int>;
            Debug.Assert(tup != null, $"Wrong tag in [{nameof(RadioGroup_Checked)}]");
            UIControl uiCtrl = tup.Item1;
            Debug.Assert(uiCtrl.Type == UIControlType.RadioGroup, $"Wrong UIControlType in [{nameof(RadioGroup_Checked)}]");

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
        private void DrawToCanvas(FrameworkElement element, UIControl uiCtrl)
        {
            DrawToCanvas(_canvas, element, uiCtrl, uiCtrl.Rect);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawToCanvas(FrameworkElement element, UIControl uiCtrl, Rect rect)
        {
            DrawToCanvas(_canvas, element, uiCtrl, rect);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                element.ToolTip = StringEscaper.Unescape(toolTip);
        }

        private void SetEditModeProperties(FrameworkElement element, UIControl uiCtrl)
        {
            // Do nothing in view mode (MainWindow)
            if (_viewMode)
                return;

            // Only for edit mode (ScriptEditWindow)
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
                return StringEscaper.Unescape(toolTip);

            if (MaxUrlDisplayLen < url.Length)
                url = url.Substring(0, MaxUrlDisplayLen) + "...";

            if (toolTip == null)
                return url;
            return StringEscaper.Unescape(toolTip) + Environment.NewLine + Environment.NewLine + url;
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
            if (_sc.Sections.ContainsKey(sectionName)) // Only if section exists
            {
                ScriptSection targetSection = _sc.Sections[sectionName];
                InternalRunOneSection(targetSection, $"{_sc.Title} - {ctrlType} [{ctrlKey}]", hideProgress);
            }
            else
            {
                Global.Logger.SystemWrite(new LogInfo(LogState.Error, $"Section [{sectionName}] does not exist"));
            }
        }

        private static async void InternalRunOneSection(ScriptSection section, string logMsg, bool hideProgress)
        {
            if (Engine.WorkingLock == 0)
            {
                Interlocked.Increment(ref Engine.WorkingLock);

                Logger logger = Global.Logger;

                MainViewModel mainModel = Global.MainViewModel;
                // Populate BuildTree
                if (!hideProgress)
                {
                    mainModel.BuildTreeItems.Clear();
                    ProjectTreeItemModel itemRoot = MainViewModel.PopulateOneTreeItem(section.Script, null, null);
                    mainModel.BuildTreeItems.Add(itemRoot);
                    mainModel.CurBuildTree = null;
                }

                mainModel.WorkInProgress = true;

                EngineState s = new EngineState(section.Project, logger, mainModel, EngineMode.RunMainAndOne, section.Script, section.Name);
                s.SetOptions(Global.Setting);
                s.SetCompat(section.Project.Compat);
                if (s.LogMode == LogMode.PartDefer) // Use FullDefer in UIRenderer
                    s.LogMode = LogMode.FullDefer;

                Engine.WorkingEngine = new Engine(s);

                // Build Start, Switch to Build View
                if (!hideProgress)
                    mainModel.SwitchNormalBuildInterface = false;

                // Set StatusBar Text
                CancellationTokenSource ct = new CancellationTokenSource();
                Task printStatus = MainViewModel.PrintBuildElapsedStatus(logMsg, s, ct.Token);

                // Run
                await Engine.WorkingEngine.Run(logMsg);

                // Cancel and Wait until PrintBuildElapsedStatus stops
                ct.Cancel();
                await printStatus;
                mainModel.StatusBarText = $"{logMsg} took {s.Elapsed:h\\:mm\\:ss}";

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
                        s.MainViewModel.DisplayScript(mainModel.CurMainTree.Script);
                    });
                }
            }
        }
        #endregion
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
