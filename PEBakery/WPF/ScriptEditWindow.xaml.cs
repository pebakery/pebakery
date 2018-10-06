using MahApps.Metro.IconPacks;
using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using PEBakery.Core;
using PEBakery.Helper;
using PEBakery.Ini;
using PEBakery.WPF.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

// ReSharper disable InconsistentNaming

namespace PEBakery.WPF
{
    #region ScriptEditWindow
    // ReSharper disable once RedundantExtendsListEntry
    public partial class ScriptEditWindow : Window
    {
        #region Field and Property
        public static int Count = 0;

        private Script _sc;
        private UIRenderer _renderer;
        private readonly ScriptEditViewModel m;
        private string _ifaceSectionName;
        #endregion

        #region Constructor
        public ScriptEditWindow(Script sc)
        {
            Interlocked.Increment(ref Count);

            try
            {
                _sc = sc ?? throw new ArgumentNullException(nameof(sc));

                InitializeComponent();
                DataContext = m = new ScriptEditViewModel();
                m.InterfaceCanvas.UIControlSelected += InterfaceCanvas_UIControlSelected;
                m.InterfaceCanvas.UIControlDragged += InterfaceCanvas_UIControlDragged;
                m.UIControlModified += ViewModel_UIControlModified;

                ReadScriptGeneral();
                ReadScriptInterface();
                ReadScriptAttachment();
            }
            catch (Exception e)
            { // Rollback Count to 0
                Interlocked.Decrement(ref Count);

                Global.Logger.SystemWrite(new LogInfo(LogState.CriticalError, e));
                MessageBox.Show($"[Error Message]\r\n{Logger.LogExceptionMessage(e)}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Window Event
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            bool scriptSaved = false;
            if (m.ScriptHeaderNotSaved)
            {
                switch (MessageBox.Show("Script header was modified.\r\nSave changes?", "Save Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Exclamation))
                {
                    case MessageBoxResult.Yes:
                        if (WriteScriptGeneral(false))
                            scriptSaved = true;
                        else
                        {
                            e.Cancel = true; // Error while saving, do not close ScriptEditWindow
                            return;
                        }
                        break;
                    case MessageBoxResult.No:
                        // Cancel updated changes
                        m.ScriptHeaderUpdated = false;
                        break;
                    default:
                        throw new InvalidOperationException("Internal Logic Error at ScriptEditWindow.CloseButton_Click");
                }
            }

            if (m.InterfaceNotSaved)
            {
                switch (MessageBox.Show("Script interface was modified.\r\nSave changes?", "Save Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Exclamation))
                {
                    case MessageBoxResult.Yes:
                        // Do not use e.Cancel here, when script file is moved the method will always fail
                        if (WriteScriptInterface(false))
                            scriptSaved = true;
                        break;
                    case MessageBoxResult.No:
                        // Cancel updated changes
                        m.InterfaceUpdated = false;
                        break;
                    default:
                        throw new InvalidOperationException("Internal Logic Error at ScriptEditWindow.CloseButton_Click");
                }
            }

            if (scriptSaved)
                RefreshMainWindow();

            // If script was updated, force MainWindow to refresh script
            DialogResult = m.ScriptHeaderUpdated || m.ScriptLogoUpdated || m.InterfaceUpdated || m.ScriptAttachUpdated;

            Tag = _sc;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            m.InterfaceCanvas.UIControlSelected -= InterfaceCanvas_UIControlSelected;
            m.UIControlModified -= ViewModel_UIControlModified;

            _renderer.Clear();
            Interlocked.Decrement(ref Count);
        }
        #endregion

        #region ReadScript
        private void ReadScriptGeneral()
        {
            // Nested Function
            string GetStringValue(string key, string defaultValue = "") => _sc.MainInfo.ContainsKey(key) ? _sc.MainInfo[key] : defaultValue;

            // General
            if (EncodedFile.ContainsLogo(_sc))
            {
                (EncodedFileInfo info, string errMsg) = EncodedFile.GetLogoInfo(_sc, true);
                if (errMsg != null)
                {
                    Global.Logger.SystemWrite(new LogInfo(LogState.Error, errMsg));
                    MessageBox.Show($"Unable to read script logo\r\n\r\n[Message]\r\n{errMsg}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                using (MemoryStream ms = EncodedFile.ExtractLogo(_sc, out ImageHelper.ImageType type))
                {
                    switch (type)
                    {
                        case ImageHelper.ImageType.Svg:
                            DrawingGroup svgDrawing = ImageHelper.SvgToDrawingGroup(ms);
                            Rect svgSize = svgDrawing.Bounds;
                            (double width, double height) = ImageHelper.StretchSizeAspectRatio(svgSize.Width, svgSize.Height, 90, 90);
                            m.ScriptLogoSvg = new DrawingBrush { Drawing = svgDrawing };
                            m.ScriptLogoSvgWidth = width;
                            m.ScriptLogoSvgHeight = height;
                            break;
                        default:
                            m.ScriptLogoImage = ImageHelper.ImageToBitmapImage(ms);
                            break;
                    }
                }
                m.ScriptLogoInfo = info;
            }
            else
            { // No script logo
                m.ScriptLogoIcon = PackIconMaterialKind.BorderNone;
                m.ScriptLogoInfo = null;
            }

            m.ScriptTitle = _sc.Title;
            m.ScriptAuthor = _sc.Author;
            m.ScriptVersion = _sc.Version;
            m.ScriptDate = GetStringValue("Date");
            m.ScriptLevel = _sc.Level;
            m.ScriptDescription = StringEscaper.Unescape(_sc.Description);
            m.ScriptSelectedState = _sc.Selected;
            m.ScriptMandatory = _sc.Mandatory;

            m.ScriptHeaderNotSaved = false;
            m.ScriptHeaderUpdated = false;
        }

        private void ReadScriptInterface()
        {
            _ifaceSectionName = UIRenderer.GetInterfaceSectionName(_sc);

            // Make a copy of uiCtrls, to prevent change in interface should not affect script file immediately.
            (List<UIControl> uiCtrls, List<LogInfo> errLogs) = UIRenderer.LoadInterfaces(_sc);
            if (uiCtrls == null) // No Interface -> empty list
            {
                if (0 < errLogs.Count)
                {
                    Global.Logger.SystemWrite(errLogs);

                    StringBuilder b = new StringBuilder();
                    b.AppendLine("[Error Messages]");
                    foreach (LogInfo log in errLogs)
                        b.AppendLine(log.Message);
                    MessageBox.Show(b.ToString(), "Interface Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                uiCtrls = new List<UIControl>();
            }

            _renderer = new UIRenderer(m.InterfaceCanvas, this, _sc, uiCtrls.ToList(), 1, false, Global.Setting.Compat_IgnoreWidthOfWebLabel);

            m.InterfaceUICtrls = new ObservableCollection<string>(uiCtrls.Select(x => x.Key));
            m.InterfaceUICtrlIndex = -1;

            m.InterfaceNotSaved = false;
            m.InterfaceUpdated = false;

            DrawScript();
            ResetSelectedUICtrl();
        }

        private void ReadScriptAttachment()
        {
            // Attachment
            m.AttachedFiles.Clear();

            (Dictionary<string, List<EncodedFileInfo>> fileDict, string errMsg) = EncodedFile.GetAllFilesInfo(_sc, false);
            if (errMsg != null)
            {
                Global.Logger.SystemWrite(new LogInfo(LogState.Error, errMsg));
                MessageBox.Show($"Unable to read script attachments\r\n\r\n[Message]\r\n{errMsg}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            foreach (var kv in fileDict)
            {
                string dirName = kv.Key;

                AttachedFileItem item = new AttachedFileItem(true, dirName);
                foreach (EncodedFileInfo fi in kv.Value)
                {
                    AttachedFileItem child = new AttachedFileItem(false, fi.FileName, fi);
                    item.Children.Add(child);
                }
                m.AttachedFiles.Add(item);
            }
        }

        private void ReadUIControlInfo(UIControl uiCtrl)
        {
            m.UIControlModifiedEventToggle = true;

            switch (uiCtrl.Type)
            {
                case UIControlType.TextBox:
                    {
                        UIInfo_TextBox info = uiCtrl.Info.Cast<UIInfo_TextBox>();

                        m.UICtrlTextBoxInfo = info;
                        break;
                    }
                case UIControlType.TextLabel:
                    {
                        UIInfo_TextLabel info = uiCtrl.Info.Cast<UIInfo_TextLabel>();

                        m.UICtrlTextLabelInfo = info;
                        break;
                    }
                case UIControlType.NumberBox:
                    {
                        UIInfo_NumberBox info = uiCtrl.Info.Cast<UIInfo_NumberBox>();

                        m.UICtrlNumberBoxInfo = info;
                        break;
                    }
                case UIControlType.CheckBox:
                    {
                        UIInfo_CheckBox info = uiCtrl.Info.Cast<UIInfo_CheckBox>();

                        m.UICtrlCheckBoxInfo = info;
                        m.UICtrlSectionToRun = info.SectionName;
                        m.UICtrlHideProgress = info.HideProgress;
                        break;
                    }
                case UIControlType.ComboBox:
                    {
                        UIInfo_ComboBox info = uiCtrl.Info.Cast<UIInfo_ComboBox>();

                        m.UICtrlComboBoxInfo = info;
                        m.UICtrlSectionToRun = info.SectionName;
                        m.UICtrlHideProgress = info.HideProgress;
                        break;
                    }
                case UIControlType.Image:
                    {
                        UIInfo_Image info = uiCtrl.Info.Cast<UIInfo_Image>();

                        m.UICtrlImageInfo = info;
                        m.UICtrlImageSet = EncodedFile.ContainsInterface(_sc, uiCtrl.Text);
                        break;
                    }
                case UIControlType.TextFile:
                    {
                        Debug.Assert(uiCtrl.Info.GetType() == typeof(UIInfo_TextFile), "Invalid UIInfo");

                        m.UICtrlTextFileSet = EncodedFile.ContainsInterface(_sc, uiCtrl.Text);
                        break;
                    }
                case UIControlType.Button:
                    {
                        UIInfo_Button info = uiCtrl.Info.Cast<UIInfo_Button>();

                        m.UICtrlButtonInfo = info;
                        m.UICtrlSectionToRun = info.SectionName;
                        m.UICtrlHideProgress = info.HideProgress;
                        m.UICtrlButtonPictureSet = info.Picture != null && EncodedFile.ContainsInterface(_sc, info.Picture);
                        break;
                    }
                case UIControlType.WebLabel:
                    {
                        UIInfo_WebLabel info = uiCtrl.Info.Cast<UIInfo_WebLabel>();

                        m.UICtrlWebLabelInfo = info;
                        break;
                    }
                case UIControlType.RadioButton:
                    {
                        UIInfo_RadioButton info = uiCtrl.Info.Cast<UIInfo_RadioButton>();

                        m.UICtrlRadioButtonList = _renderer.UICtrls.Where(x => x.Type == UIControlType.RadioButton).ToList();
                        m.UICtrlRadioButtonInfo = info;
                        m.UICtrlSectionToRun = info.SectionName;
                        m.UICtrlHideProgress = info.HideProgress;
                        break;
                    }
                case UIControlType.Bevel:
                    {
                        UIInfo_Bevel info = uiCtrl.Info.Cast<UIInfo_Bevel>();

                        m.UICtrlBevelInfo = info;
                        break;
                    }
                case UIControlType.FileBox:
                    {
                        UIInfo_FileBox info = uiCtrl.Info.Cast<UIInfo_FileBox>();

                        m.UICtrlFileBoxInfo = info;
                        break;
                    }
                case UIControlType.RadioGroup:
                    {
                        UIInfo_RadioGroup info = uiCtrl.Info.Cast<UIInfo_RadioGroup>();

                        m.UICtrlRadioGroupInfo = info;
                        m.UICtrlSectionToRun = info.SectionName;
                        m.UICtrlHideProgress = info.HideProgress;
                        break;
                    }
            }

            m.UIControlModifiedEventToggle = false;
        }
        #endregion

        #region WriteScript
        private bool WriteScriptGeneral(bool refresh = true)
        {
            if (_sc == null)
                return false;

            // Check m.ScriptVersion
            string verStr = StringEscaper.ProcessVersionString(m.ScriptVersion);
            if (verStr == null)
            {
                string errMsg = $"Invalid version string [{m.ScriptVersion}], please check again";
                Global.Logger.SystemWrite(new LogInfo(LogState.Error, errMsg));
                MessageBox.Show(errMsg + '.', "Invalid Version String", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            IniKey[] keys =
            {
                new IniKey("Main", "Title", m.ScriptTitle),
                new IniKey("Main", "Author", m.ScriptAuthor),
                new IniKey("Main", "Version", m.ScriptVersion),
                new IniKey("Main", "Date", m.ScriptDate),
                new IniKey("Main", "Level", ((int)m.ScriptLevel).ToString()),
                new IniKey("Main", "Description", StringEscaper.Escape(m.ScriptDescription)),
                new IniKey("Main", "Selected", m.ScriptSelectedState.ToString()),
                new IniKey("Main", "Mandatory", m.ScriptMandatory.ToString()),
            };

            IniReadWriter.WriteKeys(_sc.RealPath, keys);
            _sc = _sc.Project.RefreshScript(_sc);

            if (refresh)
                RefreshMainWindow();

            m.ScriptHeaderNotSaved = false;
            return true;
        }

        private bool WriteScriptInterface(bool refresh = true)
        {
            if (_renderer == null)
                return false;

            try
            {
                if (m.SelectedUICtrl != null)
                    WriteUIControlInfo(m.SelectedUICtrl);

                UIControl.Update(_renderer.UICtrls);
                UIControl.Delete(m.UICtrlToBeDeleted);
                m.UICtrlToBeDeleted.Clear();
                IniReadWriter.DeleteKeys(_sc.RealPath, m.UICtrlKeyChanged.Select(x => new IniKey(_ifaceSectionName, x)));
                m.UICtrlKeyChanged.Clear();

                if (refresh)
                    RefreshMainWindow();

                _sc = _sc.Project.RefreshScript(_sc);
            }
            catch (Exception e)
            {
                MessageBox.Show($"Interface save failed.\r\n\r\n[Message]\r\n{Logger.LogExceptionMessage(e)}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            m.InterfaceNotSaved = false;
            return true;
        }

        private void RefreshMainWindow()
        {
            Application.Current?.Dispatcher.BeginInvoke((Action)(() =>
            {
                MainWindow w = Application.Current.MainWindow as MainWindow;
                w?.DisplayScript(_sc);
            }));
        }

        private void WriteUIControlInfo(UIControl uiCtrl)
        {
            switch (uiCtrl.Type)
            {
                case UIControlType.CheckBox:
                    {
                        UIInfo_CheckBox info = uiCtrl.Info.Cast<UIInfo_CheckBox>();

                        info.SectionName = string.IsNullOrWhiteSpace(m.UICtrlSectionToRun) ? null : m.UICtrlSectionToRun;
                        info.HideProgress = m.UICtrlHideProgress;
                        break;
                    }
                case UIControlType.ComboBox:
                    {
                        UIInfo_ComboBox info = uiCtrl.Info.Cast<UIInfo_ComboBox>();

                        uiCtrl.Text = info.Items[info.Index];
                        info.SectionName = string.IsNullOrWhiteSpace(m.UICtrlSectionToRun) ? null : m.UICtrlSectionToRun;
                        info.HideProgress = m.UICtrlHideProgress;
                        break;
                    }
                case UIControlType.Image:
                    {
                        Debug.Assert(uiCtrl.Info.GetType() == typeof(UIInfo_Image), "Invalid UIInfo");

                        m.UICtrlImageSet = EncodedFile.ContainsInterface(_sc, uiCtrl.Text);
                        break;
                    }
                case UIControlType.TextFile:
                    {
                        Debug.Assert(uiCtrl.Info.GetType() == typeof(UIInfo_TextFile), "Invalid UIInfo");

                        m.UICtrlTextFileSet = EncodedFile.ContainsInterface(_sc, uiCtrl.Text);
                        break;
                    }
                case UIControlType.Button:
                    {
                        UIInfo_Button info = uiCtrl.Info.Cast<UIInfo_Button>();

                        m.UICtrlButtonPictureSet = info.Picture != null && EncodedFile.ContainsInterface(_sc, info.Picture);
                        info.SectionName = string.IsNullOrWhiteSpace(m.UICtrlSectionToRun) ? null : m.UICtrlSectionToRun;
                        info.HideProgress = m.UICtrlHideProgress;
                        break;
                    }
                case UIControlType.RadioButton:
                    {
                        UIInfo_RadioButton info = uiCtrl.Info.Cast<UIInfo_RadioButton>();

                        info.SectionName = string.IsNullOrWhiteSpace(m.UICtrlSectionToRun) ? null : m.UICtrlSectionToRun;
                        info.HideProgress = m.UICtrlHideProgress;
                        break;
                    }
                case UIControlType.RadioGroup:
                    {
                        UIInfo_RadioGroup info = uiCtrl.Info.Cast<UIInfo_RadioGroup>();

                        info.SectionName = string.IsNullOrWhiteSpace(m.UICtrlSectionToRun) ? null : m.UICtrlSectionToRun;
                        info.HideProgress = m.UICtrlHideProgress;
                        break;
                    }
            }
        }
        #endregion

        #region Event Handler - Logo
        private void ScriptLogoAttachButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "Supported Image (bmp, jpg, png, gif, ico, svg)|*.bmp;*.jpg;*.png;*.gif;*.ico;*.svg",
            };

            if (dialog.ShowDialog() != true)
                return;

            string srcFile = dialog.FileName;
            try
            {
                string srcFileName = Path.GetFileName(srcFile);
                _sc = EncodedFile.AttachLogo(_sc, srcFileName, srcFile);
                MessageBox.Show("Logo successfully attached.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                m.ScriptLogoUpdated = true;
                ReadScriptGeneral();
            }
            catch (Exception ex)
            {
                Global.Logger.SystemWrite(new LogInfo(LogState.Error, ex));
                MessageBox.Show($"Attach failed.\r\n\r\n[Message]\r\n{Logger.LogExceptionMessage(ex)}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ScriptLogoExtractButton_Click(object sender, RoutedEventArgs e)
        {
            if (!EncodedFile.ContainsLogo(_sc))
            {
                MessageBox.Show($"Script [{_sc.Title}] does not have logo attached", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            using (MemoryStream ms = EncodedFile.ExtractLogo(_sc, out ImageHelper.ImageType type))
            {
                SaveFileDialog dialog = new SaveFileDialog
                {
                    InitialDirectory = Global.BaseDir,
                    OverwritePrompt = true,
                    Filter = $"Image ({type.ToString().ToLower()})|*.{type}",
                    DefaultExt = $".{type}",
                    AddExtension = true,
                };

                if (dialog.ShowDialog() != true)
                    return;

                string destPath = dialog.FileName;
                try
                {
                    using (FileStream fs = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        ms.CopyTo(fs);
                    }

                    MessageBox.Show("Logo successfully extracted.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    Global.Logger.SystemWrite(new LogInfo(LogState.Error, ex));
                    MessageBox.Show($"Extraction failed.\r\n\r\n[Message]\r\n{Logger.LogExceptionMessage(ex)}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ScriptLogoDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!EncodedFile.ContainsLogo(_sc))
            {
                MessageBox.Show("Logo does not exist.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string errMsg;
            (_sc, errMsg) = EncodedFile.DeleteLogo(_sc);
            if (errMsg == null)
            {
                MessageBox.Show("Logo successfully deleted.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                m.ScriptLogoUpdated = true;
                ReadScriptGeneral();
            }
            else
            {
                Global.Logger.SystemWrite(new LogInfo(LogState.Error, errMsg));
                MessageBox.Show($"There was an issue while deleting logo.\r\n\r\n[Message]\r\n{errMsg}", "Warning", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Event Handler - Interface
        #region For Editor
        private void DrawScript()
        {
            if (m == null)
                return;

            double scaleFactor = m.InterfaceScaleFactor / 100;
            if (scaleFactor - 1 < double.Epsilon)
                scaleFactor = 1;
            _renderer.ScaleFactor = scaleFactor;
            m.InterfaceCanvas.LayoutTransform = new ScaleTransform(scaleFactor, scaleFactor);

            _renderer.Render();
            m.InterfaceLoaded = true;
        }

        private void ResetSelectedUICtrl()
        {
            m.SelectedUICtrl = null;
            m.InterfaceCanvas.ResetSelectedBorder();
        }

        private void ScaleFactorSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            DrawScript();
        }

        private void UIControlComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (m.InterfaceUICtrlIndex < 0 || _renderer.UICtrls.Count <= m.InterfaceUICtrlIndex)
                return;

            m.SelectedUICtrl = _renderer.UICtrls[m.InterfaceUICtrlIndex];
            m.InterfaceCanvas.ResetSelectedBorder();
            m.InterfaceCanvas.DrawSelectedBorder(m.SelectedUICtrl);
        }

        private void InterfaceCanvas_UIControlSelected(object sender, EditCanvas.UIControlSelectedEventArgs e)
        {
            if (e.UIControl == null)
                return;

            m.SelectedUICtrl = e.UIControl;

            if (m.SelectedUICtrl != null)
                ReadUIControlInfo(m.SelectedUICtrl);

            int idx = _renderer.UICtrls.FindIndex(x => x.Key.Equals(e.UIControl.Key));
            Debug.Assert(idx != -1, "Internal Logic Error at ViewModel_UIControlSelected");
            m.InterfaceUICtrlIndex = idx;
        }

        private void InterfaceCanvas_UIControlDragged(object sender, DragCanvas.UIControlDraggedEventArgs e)
        {
            if (e.UIControl == null)
                return;

            // m.SelectedUICtrl should have been set to e.UIControl by InterfaceCanvas_UIControlSelected
            if (m.SelectedUICtrl != e.UIControl)
                return;

            m.InvokeUIControlEvent(true);
        }

        private void ViewModel_UIControlModified(object sender, ScriptEditViewModel.UIControlModifiedEventArgs e)
        {
            UIControl uiCtrl = e.UIControl;

            if (!e.Direct)
                WriteUIControlInfo(uiCtrl);

            int idx = _renderer.UICtrls.FindIndex(x => x.Key.Equals(uiCtrl.Key));
            Debug.Assert(idx != -1, "Internal Logic Error at ViewModel_UIControlModified");
            _renderer.UICtrls[idx] = uiCtrl;

            m.InterfaceCanvas.ResetSelectedBorder();
            _renderer.Render();
            m.InterfaceCanvas.DrawSelectedBorder(m.SelectedUICtrl);
        }

        private void UICtrlAddType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UIControlType type = UIControl.UIControlZeroBasedDict[m.UICtrlAddTypeIndex];
            if (type == UIControlType.None)
                return;
            m.UICtrlAddName = StringEscaper.GetUniqueKey(type.ToString(), _renderer.UICtrls.Select(x => x.Key));
        }

        private void UICtrlAddCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (m == null)
            {
                e.CanExecute = false;
            }
            else
            {
                e.CanExecute = (int)UIControlType.None < m.UICtrlAddTypeIndex;
            }
        }

        private void UICtrlAddCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            UIControlType type = UIControl.UIControlZeroBasedDict[m.UICtrlAddTypeIndex];
            if (type == UIControlType.None)
            {
                MessageBox.Show("Please select interface control's type", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (string.IsNullOrWhiteSpace(m.UICtrlAddName))
            {
                MessageBox.Show("New interface control's name is empty", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            string key = m.UICtrlAddName.Trim();
            if (_renderer.UICtrls.Select(x => x.Key).Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                MessageBox.Show($"Interface key [{key}] is duplicated", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!_sc.Sections.ContainsKey(_ifaceSectionName))
            { // No [Interface] section, so add it
                IniReadWriter.AddSection(_sc.DirectRealPath, _ifaceSectionName);
                _sc = _sc.Project.RefreshScript(_sc);
            }

            ScriptSection ifaceSection = _sc.Sections[_ifaceSectionName];
            string line = UIControl.GetUIControlTemplate(type, key);

            UIControl uiCtrl = UIParser.ParseStatement(line, ifaceSection, out List<LogInfo> errorLogs);
            Debug.Assert(uiCtrl != null, "Internal Logic Error at UICtrlAddButton_Click");
            Debug.Assert(errorLogs.Count == 0, "Internal Logic Error at UICtrlAddButton_Click");

            _renderer.UICtrls.Add(uiCtrl);
            m.InterfaceUICtrls = new ObservableCollection<string>(_renderer.UICtrls.Select(x => x.Key));
            m.InterfaceUICtrlIndex = 0;

            m.InterfaceCanvas.ResetSelectedBorder();
            _renderer.Render();
            m.SelectedUICtrl = uiCtrl;
            m.InterfaceCanvas.DrawSelectedBorder(uiCtrl);

            m.InterfaceNotSaved = true;
            m.InterfaceUpdated = true;
        }

        private void UICtrlDeleteCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = m != null && m.UICtrlEditEnabled;
        }

        private void UICtrlDeleteCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (m.SelectedUICtrl == null)
                return;

            UIControl uiCtrl = m.SelectedUICtrl;
            m.UICtrlToBeDeleted.Add(uiCtrl);

            _renderer.UICtrls.Remove(uiCtrl);
            m.InterfaceUICtrls = new ObservableCollection<string>(_renderer.UICtrls.Select(x => x.Key));
            m.InterfaceUICtrlIndex = 0;

            _renderer.Render();
            m.SelectedUICtrl = null;

            m.InterfaceNotSaved = true;
            m.InterfaceUpdated = true;
        }

        private void SaveCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (m == null)
            {
                e.CanExecute = false;
            }
            else
            {
                // Only in Tab [General] or [Interface]
                e.CanExecute = m.TabIndex == 0 || m.TabIndex == 1;
            }
        }

        private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            switch (m.TabIndex)
            {
                case 0:
                    // Changing focus is required to make sure changes in UI updated to ViewModel
                    MainSaveButton.Focus();
                    WriteScriptGeneral();
                    break;
                case 1:
                    WriteScriptInterface();
                    break;
            }
        }

        private void UICtrlReloadCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (m == null)
            {
                e.CanExecute = false;
            }
            else
            {
                // Only in Tab [Interface]
                e.CanExecute = m.TabIndex == 1;
            }
        }

        private void UICtrlReloadCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ReadScriptInterface();
        }
        #endregion
        #region For Image
        private void UICtrlImageAutoResizeButton_Click(object sender, RoutedEventArgs e)
        {
            const string internalErrorMsg = "Internal Logic Error at UICtrlImageAutoResizeButton_Click";

            Debug.Assert(m.SelectedUICtrl != null, internalErrorMsg);
            Debug.Assert(m.SelectedUICtrl.Type == UIControlType.Image, internalErrorMsg);

            UIControl uiCtrl = m.SelectedUICtrl;
            string fileName = uiCtrl.Text;
            string ext = Path.GetExtension(uiCtrl.Text);

            Debug.Assert(fileName != null, internalErrorMsg);
            Debug.Assert(ext != null, internalErrorMsg);

            if (m.InterfaceNotSaved)
            {
                MessageBoxResult result = MessageBox.Show("Interface should be saved before editing image.\r\nSave changes?", "Save Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
                if (result == MessageBoxResult.Yes)
                    WriteScriptInterface(false);
                else
                    return;
            }

            if (!ImageHelper.GetImageType(fileName, out ImageHelper.ImageType type))
            {
                MessageBox.Show($"Unsupported image [{fileName}]", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            using (MemoryStream ms = EncodedFile.ExtractFileInMem(_sc, ScriptSection.Names.InterfaceEncoded, fileName))
            {
                int width, height;
                if (type == ImageHelper.ImageType.Svg)
                    (width, height) = ImageHelper.GetSvgSizeInt(ms);
                else
                    (width, height) = ImageHelper.GetImageSize(ms);

                uiCtrl.Width = width;
                uiCtrl.Height = height;
                m.InvokeUIControlEvent(false);
                WriteScriptInterface(false);
            }
        }
        #endregion
        #region For RadioButton
        private void UICtrlRadioButtonSelect_Click(object sender, RoutedEventArgs e)
        {
            const string internalErrorMsg = "Internal Logic Error at UICtrlRadioButtonSelect_Click";

            Debug.Assert(m.SelectedUICtrl != null, internalErrorMsg);
            Debug.Assert(m.SelectedUICtrl.Type == UIControlType.RadioButton, internalErrorMsg);
            Debug.Assert(m.UICtrlRadioButtonInfo != null, internalErrorMsg);
            Debug.Assert(m.UICtrlRadioButtonInfo.Selected == false, internalErrorMsg);
            Debug.Assert(m.UICtrlRadioButtonList != null, internalErrorMsg);

            foreach (UIControl uncheck in m.UICtrlRadioButtonList.Where(x => !x.Key.Equals(m.SelectedUICtrl.Key)))
            {
                Debug.Assert(uncheck.Info.GetType() == typeof(UIInfo_RadioButton), "Invalid UIInfo");
                UIInfo_RadioButton subInfo = uncheck.Info as UIInfo_RadioButton;
                Debug.Assert(subInfo != null, "Invalid UIInfo");

                subInfo.Selected = false;
            }
            m.UICtrlRadioButtonInfo.Selected = true;

            m.OnPropertyUpdate(nameof(m.UICtrlRadioButtonSelectEnable));
            m.InvokeUIControlEvent(true);
        }
        #endregion
        #region For (Common) ListItemBox
        private void ListNewItem_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Prohibit '|'
            if (e.Text.Contains('|'))
                e.Handled = true;

            OnPreviewTextInput(e);
        }

        private void UICtrlListItemBoxUp_Click(object sender, RoutedEventArgs e)
        {
            const string internalErrorMsg = "Internal Logic Error at UICtrlListItemBoxUp_Click";

            Debug.Assert(m.SelectedUICtrl != null, internalErrorMsg);
            List<string> items;
            switch (m.SelectedUICtrl.Type)
            {
                case UIControlType.ComboBox:
                    Debug.Assert(m.UICtrlComboBoxInfo != null, internalErrorMsg);
                    items = m.UICtrlComboBoxInfo.Items;
                    break;
                case UIControlType.RadioGroup:
                    Debug.Assert(m.UICtrlRadioGroupInfo != null, internalErrorMsg);
                    items = m.UICtrlRadioGroupInfo.Items;
                    break;
                default:
                    throw new InvalidOperationException(internalErrorMsg);
            }
            Debug.Assert(items.Count == m.UICtrlListItemBoxItems.Count, internalErrorMsg);

            int idx = m.UICtrlListItemBoxSelectedIndex;
            if (0 < idx)
            {
                string item = items[idx];
                items.RemoveAt(idx);
                items.Insert(idx - 1, item);

                var editItem = m.UICtrlListItemBoxItems[idx];
                m.UICtrlListItemBoxItems.RemoveAt(idx);
                m.UICtrlListItemBoxItems.Insert(idx - 1, editItem);

                switch (m.SelectedUICtrl.Type)
                {
                    case UIControlType.ComboBox:
                        if (m.UICtrlComboBoxInfo.Index == idx)
                            m.UICtrlComboBoxInfo.Index = idx - 1;
                        break;
                    case UIControlType.RadioGroup:
                        if (m.UICtrlRadioGroupInfo.Selected == idx)
                            m.UICtrlRadioGroupInfo.Selected = idx - 1;
                        break;
                    default:
                        throw new InvalidOperationException(internalErrorMsg);
                }

                m.UICtrlListItemBoxSelectedIndex = idx - 1;
                m.InvokeUIControlEvent(false);
            }
        }

        private void UICtrlListItemBoxDown_Click(object sender, RoutedEventArgs e)
        {
            const string internalErrorMsg = "Internal Logic Error at UICtrlListItemBoxDown_Click";

            Debug.Assert(m.SelectedUICtrl != null, internalErrorMsg);
            List<string> items;
            switch (m.SelectedUICtrl.Type)
            {
                case UIControlType.ComboBox:
                    Debug.Assert(m.UICtrlComboBoxInfo != null, internalErrorMsg);
                    items = m.UICtrlComboBoxInfo.Items;
                    Debug.Assert(items.Count == m.UICtrlListItemBoxItems.Count, internalErrorMsg);
                    break;
                case UIControlType.RadioGroup:
                    Debug.Assert(m.UICtrlRadioGroupInfo != null, internalErrorMsg);
                    items = m.UICtrlRadioGroupInfo.Items;
                    Debug.Assert(items.Count == m.UICtrlListItemBoxItems.Count, internalErrorMsg);
                    break;
                default:
                    throw new InvalidOperationException(internalErrorMsg);
            }

            int idx = m.UICtrlListItemBoxSelectedIndex;
            if (idx + 1 < items.Count)
            {
                string item = items[idx];
                items.RemoveAt(idx);
                items.Insert(idx + 1, item);

                string editItem = m.UICtrlListItemBoxItems[idx];
                m.UICtrlListItemBoxItems.RemoveAt(idx);
                m.UICtrlListItemBoxItems.Insert(idx + 1, editItem);

                switch (m.SelectedUICtrl.Type)
                {
                    case UIControlType.ComboBox:
                        if (m.UICtrlComboBoxInfo.Index == idx)
                            m.UICtrlComboBoxInfo.Index = idx + 1;
                        break;
                    case UIControlType.RadioGroup:
                        if (m.UICtrlRadioGroupInfo.Selected == idx)
                            m.UICtrlRadioGroupInfo.Selected = idx + 1;
                        break;
                    default:
                        throw new InvalidOperationException(internalErrorMsg);
                }

                m.UICtrlListItemBoxSelectedIndex = idx + 1;
                m.InvokeUIControlEvent(false);
            }
        }

        private void UICtrlListItemBoxSelect_Click(object sender, RoutedEventArgs e)
        {
            const string internalErrorMsg = "Internal Logic Error at UICtrlListItemBoxSelect_Click";

            Debug.Assert(m.SelectedUICtrl != null, internalErrorMsg);
            switch (m.SelectedUICtrl.Type)
            {
                case UIControlType.ComboBox:
                    Debug.Assert(m.UICtrlComboBoxInfo != null, internalErrorMsg);
                    Debug.Assert(m.UICtrlComboBoxInfo.Items.Count == m.UICtrlListItemBoxItems.Count, internalErrorMsg);
                    m.UICtrlComboBoxInfo.Index = m.UICtrlListItemBoxSelectedIndex;
                    break;
                case UIControlType.RadioGroup:
                    Debug.Assert(m.UICtrlRadioGroupInfo != null, internalErrorMsg);
                    Debug.Assert(m.UICtrlRadioGroupInfo.Items.Count == m.UICtrlListItemBoxItems.Count, internalErrorMsg);
                    m.UICtrlRadioGroupInfo.Selected = m.UICtrlListItemBoxSelectedIndex;
                    break;
                default:
                    throw new InvalidOperationException(internalErrorMsg);
            }

            m.InvokeUIControlEvent(false);
        }

        private void UICtrlListItemBoxDelete_Click(object sender, RoutedEventArgs e)
        {
            const string internalErrorMsg = "Internal Logic Error at UICtrlListItemBoxDelete_Click";

            Debug.Assert(m.SelectedUICtrl != null, internalErrorMsg);
            List<string> items;
            switch (m.SelectedUICtrl.Type)
            {
                case UIControlType.ComboBox:
                    Debug.Assert(m.UICtrlComboBoxInfo != null, internalErrorMsg);
                    items = m.UICtrlComboBoxInfo.Items;
                    break;
                case UIControlType.RadioGroup:
                    Debug.Assert(m.UICtrlRadioGroupInfo != null, internalErrorMsg);
                    items = m.UICtrlRadioGroupInfo.Items;
                    break;
                default:
                    throw new InvalidOperationException(internalErrorMsg);
            }
            Debug.Assert(items.Count == m.UICtrlListItemBoxItems.Count, internalErrorMsg);

            int idx = m.UICtrlListItemBoxSelectedIndex;

            items.RemoveAt(idx);
            m.UICtrlListItemBoxItems.RemoveAt(idx);

            switch (m.SelectedUICtrl.Type)
            {
                case UIControlType.ComboBox:
                    if (m.UICtrlComboBoxInfo.Index == idx)
                        m.UICtrlComboBoxInfo.Index = 0;
                    break;
                case UIControlType.RadioGroup:
                    if (m.UICtrlRadioGroupInfo.Selected == idx)
                        m.UICtrlRadioGroupInfo.Selected = 0;
                    break;
                default:
                    throw new InvalidOperationException(internalErrorMsg);
            }

            m.UICtrlListItemBoxSelectedIndex = 0;
            m.InvokeUIControlEvent(false);
        }

        private void UICtrlListItemBoxAdd_Click(object sender, RoutedEventArgs e)
        {
            const string internalErrorMsg = "Internal Logic Error at UICtrlListItemBoxAdd_Click";

            Debug.Assert(m.SelectedUICtrl != null, internalErrorMsg);

            string newItem = m.UICtrlListItemBoxNewItem;
            switch (m.SelectedUICtrl.Type)
            {
                case UIControlType.ComboBox:
                    Debug.Assert(m.UICtrlComboBoxInfo != null, internalErrorMsg);
                    m.UICtrlComboBoxInfo.Items.Add(newItem);
                    break;
                case UIControlType.RadioGroup:
                    Debug.Assert(m.UICtrlRadioGroupInfo != null, internalErrorMsg);
                    m.UICtrlRadioGroupInfo.Items.Add(newItem);
                    break;
                default:
                    throw new InvalidOperationException(internalErrorMsg);
            }
            m.UICtrlListItemBoxItems.Add(newItem);

            m.InvokeUIControlEvent(false);
        }
        #endregion
        #region For (Common) InterfaceEncoded 
        private void UICtrlInterfaceAttachButton_Click(object sender, RoutedEventArgs e)
        {
            const string internalErrorMsg = "Internal Logic Error at UICtrlInterfaceAttachButton_Click";

            Button button = sender as Button;
            Debug.Assert(button != null, internalErrorMsg);

            UIControlType selectedType;
            string saveConfirmMsg;
            string extFilter;
            if (button.Equals(UICtrlImageAttachButton))
            {
                selectedType = UIControlType.Image;
                saveConfirmMsg = "Interface should be saved before editing image.\r\nSave changes?";
                extFilter = "Image|*.bmp;*.jpg;*.png;*.gif;*.ico;*.svg";
            }
            else if (button.Equals(UICtrlTextFileAttachButton))
            {
                selectedType = UIControlType.TextFile;
                saveConfirmMsg = "Interface should be saved before editing text file.\r\nSave changes?";
                extFilter = "Text File|*.txt";
            }
            else if (button.Equals(UICtrlButtonPictureAttachButton))
            {
                selectedType = UIControlType.Button;
                saveConfirmMsg = "Interface should be saved before editing image.\r\nSave changes?";
                extFilter = "Image|*.bmp;*.jpg;*.png;*.gif;*.ico;*.svg";
            }
            else
            {
                throw new InvalidOperationException(internalErrorMsg);
            }

            Debug.Assert(m.SelectedUICtrl != null, internalErrorMsg);
            Debug.Assert(m.SelectedUICtrl.Type == selectedType, internalErrorMsg);

            if (m.InterfaceNotSaved)
            {
                MessageBoxResult result = MessageBox.Show(saveConfirmMsg, "Save Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
                if (result == MessageBoxResult.Yes)
                    WriteScriptInterface(false);
                else
                    return;
            }

            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = extFilter,
            };

            if (dialog.ShowDialog() != true)
                return;

            string srcFilePath = dialog.FileName;
            string srcFileName = Path.GetFileName(srcFilePath);
            if (EncodedFile.ContainsInterface(_sc, srcFileName))
            {
                (List<EncodedFileInfo> infos, string errMsg) = EncodedFile.GetFolderInfo(_sc, ScriptSection.Names.InterfaceEncoded, false);
                if (errMsg != null)
                {
                    Global.Logger.SystemWrite(new LogInfo(LogState.Error, errMsg));
                    MessageBox.Show($"Attach failed.\r\n\r\n[Message]\r\n{errMsg}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                srcFileName = StringEscaper.GetUniqueFileName(srcFileName, infos.Select(x => x.FileName));
            }

            // Pratically PEBakery is capable of handling large files.
            // -> But large file in interface requires lots of memory to decompress and make unreponsive time longer.
            // -> Threshold is fully debatable.
            long fileLen = new FileInfo(srcFilePath).Length;
            if (EncodedFile.InterfaceSizeLimit < fileLen) // 4MB limit
            {
                MessageBoxResult result = MessageBox.Show("File is too large, it can make PEBakery irresponsive!\r\nContinue?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
                if (result == MessageBoxResult.No)
                    return;
            }

            try
            {
                _sc = EncodedFile.AttachInterface(_sc, srcFileName, srcFilePath);

                UIControl.ReplaceAddress(_renderer.UICtrls, _sc);

                switch (selectedType)
                {
                    case UIControlType.Image:
                        m.SelectedUICtrl.Text = srcFileName;
                        m.UICtrlImageSet = true;
                        break;
                    case UIControlType.TextFile:
                        m.SelectedUICtrl.Text = srcFileName;
                        m.UICtrlTextFileSet = true;
                        break;
                    case UIControlType.Button:
                        m.UICtrlButtonInfo.Picture = srcFileName;
                        m.UICtrlButtonPictureSet = true;
                        break;
                }

                m.InvokeUIControlEvent(false);
                WriteScriptInterface(false);
            }
            catch (Exception ex)
            {
                Global.Logger.SystemWrite(new LogInfo(LogState.Error, ex));
                MessageBox.Show($"Attach failed.\r\n\r\n[Message]\r\n{Logger.LogExceptionMessage(ex)}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void UICtrlInterfaceExtractButton_Click(object sender, RoutedEventArgs e)
        {
            const string internalErrorMsg = "Internal Logic Error at UICtrlInterfaceExtractButton_Click";

            Button button = sender as Button;
            Debug.Assert(button != null, internalErrorMsg);

            UIControlType selectedType;
            string cannotFindFile;
            if (button.Equals(UICtrlImageExtractButton))
            {
                selectedType = UIControlType.Image;
                cannotFindFile = "Unable to find image";
            }
            else if (button.Equals(UICtrlTextFileExtractButton))
            {
                selectedType = UIControlType.TextFile;
                cannotFindFile = "Unable to find text";
            }
            else if (button.Equals(UICtrlButtonPictureExtractButton))
            {
                selectedType = UIControlType.Button;
                cannotFindFile = "Unable to find image";
            }
            else
            {
                throw new InvalidOperationException(internalErrorMsg);
            }

            Debug.Assert(m.SelectedUICtrl != null, internalErrorMsg);
            Debug.Assert(m.SelectedUICtrl.Type == selectedType, internalErrorMsg);

            UIControl uiCtrl = m.SelectedUICtrl;
            string fileName = uiCtrl.Text;
            if (selectedType == UIControlType.Button)
            {
                Debug.Assert(m.UICtrlButtonInfo != null, internalErrorMsg);
                fileName = m.UICtrlButtonInfo.Picture;
            }
            string ext = Path.GetExtension(fileName);

            Debug.Assert(fileName != null, internalErrorMsg);
            Debug.Assert(ext != null, internalErrorMsg);

            string extFilter;
            if (selectedType == UIControlType.TextFile)
                extFilter = $"Text File|*{ext}";
            else
                extFilter = $"Image|*{ext}";

            if (!EncodedFile.ContainsInterface(_sc, fileName))
            {
                MessageBox.Show($"{cannotFindFile} [{fileName}]", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            SaveFileDialog dialog = new SaveFileDialog
            {
                OverwritePrompt = true,
                Filter = extFilter,
                DefaultExt = ext,
                AddExtension = true,
            };
            if (dialog.ShowDialog() != true)
                return;

            string destPath = dialog.FileName;
            using (FileStream fs = new FileStream(destPath, FileMode.Create, FileAccess.Write))
            {
                try
                {
                    EncodedFile.ExtractFile(_sc, ScriptSection.Names.InterfaceEncoded, fileName, fs);
                }
                catch (Exception ex)
                {
                    Global.Logger.SystemWrite(new LogInfo(LogState.Error, ex));
                    MessageBox.Show($"Extraction failed.\r\n\r\n[Message]\r\n{Logger.LogExceptionMessage(ex)}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void UICtrlInterfaceResetButton_Click(object sender, RoutedEventArgs e)
        {
            const string internalErrorMsg = "Internal Logic Error at UICtrlInterfaceResetButton_Click";

            Button button = sender as Button;
            Debug.Assert(button != null, internalErrorMsg);

            UIControlType selectedType;
            string saveConfirmMsg;
            if (button.Equals(UICtrlImageResetButton))
            {
                selectedType = UIControlType.Image;
                saveConfirmMsg = "Interface should be saved before editing image.\r\nSave changes?";
            }
            else if (button.Equals(UICtrlTextFileResetButton))
            {
                selectedType = UIControlType.TextFile;
                saveConfirmMsg = "Interface should be saved before editing text file.\r\nSave changes?";
            }
            else if (button.Equals(UICtrlButtonPictureResetButton))
            {
                selectedType = UIControlType.Button;
                saveConfirmMsg = "Interface should be saved before editing image.\r\nSave changes?";
            }
            else
            {
                throw new InvalidOperationException(internalErrorMsg);
            }

            Debug.Assert(m.SelectedUICtrl != null, internalErrorMsg);
            Debug.Assert(m.SelectedUICtrl.Type == selectedType, internalErrorMsg);

            UIControl uiCtrl = m.SelectedUICtrl;
            string fileName = uiCtrl.Text;
            if (selectedType == UIControlType.Button)
            {
                Debug.Assert(m.UICtrlButtonInfo != null, internalErrorMsg);
                fileName = m.UICtrlButtonInfo.Picture;
            }
            Debug.Assert(fileName != null, internalErrorMsg);

            if (m.InterfaceNotSaved)
            {
                MessageBoxResult result = MessageBox.Show(saveConfirmMsg, "Save Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
                if (result == MessageBoxResult.Yes)
                    WriteScriptInterface(false);
                else
                    return;
            }

            if (!EncodedFile.ContainsInterface(_sc, fileName))
            { // Unable to find encoded image, so just remove image entry from uiCtrl
                switch (selectedType)
                {
                    case UIControlType.Image:
                        m.SelectedUICtrl.Text = UIInfo_Image.NoResource;
                        m.UICtrlImageSet = false;
                        break;
                    case UIControlType.TextFile:
                        m.SelectedUICtrl.Text = UIInfo_TextFile.NoResource;
                        m.UICtrlTextFileSet = false;
                        break;
                    case UIControlType.Button:
                        m.UICtrlButtonInfo.Picture = null;
                        m.UICtrlButtonPictureSet = false;
                        break;
                }
                m.InvokeUIControlEvent(false);

                MessageBox.Show("Incorrect file entry deleted.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string errMsg;
            (_sc, errMsg) = EncodedFile.DeleteFile(_sc, ScriptSection.Names.InterfaceEncoded, fileName);
            if (errMsg == null)
            {
                UIControl.ReplaceAddress(_renderer.UICtrls, _sc);

                switch (selectedType)
                {
                    case UIControlType.Image:
                        m.SelectedUICtrl.Text = UIInfo_Image.NoResource;
                        m.UICtrlImageSet = false;
                        break;
                    case UIControlType.TextFile:
                        m.SelectedUICtrl.Text = UIInfo_TextFile.NoResource;
                        m.UICtrlTextFileSet = false;
                        break;
                    case UIControlType.Button:
                        m.UICtrlButtonInfo.Picture = null;
                        m.UICtrlButtonPictureSet = false;
                        break;
                }

                m.InvokeUIControlEvent(false);
                WriteScriptInterface(false);
            }
            else
            {
                Global.Logger.SystemWrite(new LogInfo(LogState.Error, errMsg));
                MessageBox.Show($"There was an issue while deleting [{fileName}].\r\n\r\n[Message]\r\n{errMsg}", "Warning", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion
        #region For (Common) RunOptional
        private void SectionName_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Prohibit invalid path characters
            if (!StringEscaper.IsFileNameValid(e.Text, new char[] { '[', ']', '\t' }))
                e.Handled = true;

            OnPreviewTextInput(e);
        }
        #endregion
        #endregion

        #region Event Handler - Attachment
        private void ScriptAttachTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is AttachedFileItem item)
            {
                m.AttachSelected = item;
                m.UpdateAttachFileDetail();
            }
        }

        #region Buttons for Folder
        private void AddFolderButton_Click(object sender, RoutedEventArgs e)
        {
            string folderName = m.AddFolderName.Trim();
            m.AddFolderName = string.Empty;

            if (folderName.Length == 0)
            {
                MessageBox.Show("Folder name is empty", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                if (EncodedFile.ContainsFolder(_sc, folderName))
                {
                    MessageBox.Show($"Cannot overwrite folder [{folderName}]", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                _sc = EncodedFile.AddFolder(_sc, folderName, false);
            }
            catch (Exception ex)
            {
                Global.Logger.SystemWrite(new LogInfo(LogState.Error, ex));
                MessageBox.Show($"Unable to add folder.\r\n\r\n[Message]\r\n{Logger.LogExceptionMessage(ex)}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            m.ScriptAttachUpdated = true;
            ReadScriptAttachment();

            m.AttachSelected = null;
            m.UpdateAttachFileDetail();
        }

        private void ExtractFolderButton_Click(object sender, RoutedEventArgs e)
        {
            AttachedFileItem item = m.AttachSelected;
            if (item == null)
                return;

            Debug.Assert(item.Detail == null);

            VistaFolderBrowserDialog dialog = new VistaFolderBrowserDialog();
            if (dialog.ShowDialog(this) == true)
            {
                string destDir = dialog.SelectedPath;

                (List<EncodedFileInfo> fileInfos, string errMsg) = EncodedFile.GetFolderInfo(_sc, item.Name, false);
                if (errMsg != null)
                {
                    Global.Logger.SystemWrite(new LogInfo(LogState.Error, errMsg));
                    MessageBox.Show($"Extraction failed.\r\n\r\n[Message]\r\n{errMsg}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                StringBuilder b = new StringBuilder();
                bool fileOverwrited = false;
                for (int i = 0; i < fileInfos.Count; i++)
                {
                    EncodedFileInfo info = fileInfos[i];

                    string destFile = Path.Combine(destDir, info.FileName);
                    if (File.Exists(destFile))
                    {
                        fileOverwrited = true;

                        b.Append(destFile);
                        if (i + 1 < fileInfos.Count)
                            b.Append(", ");
                    }
                }

                bool proceedExtract = false;
                if (fileOverwrited)
                {
                    MessageBoxResult owResult = MessageBox.Show($"File [{b}] would be overwrited.\r\nProceed with overwrite?",
                                                                "Overwrite?",
                                                                MessageBoxButton.YesNo,
                                                                MessageBoxImage.Information);

                    if (owResult == MessageBoxResult.Yes)
                        proceedExtract = true;
                    else if (owResult != MessageBoxResult.No)
                        throw new InternalException("Internal Logic Error at ScriptEditWindow.ExtractFolderButton_Click");
                }
                else
                {
                    proceedExtract = true;
                }

                if (!proceedExtract)
                    return;

                foreach (EncodedFileInfo info in fileInfos)
                {
                    try
                    {
                        string destFile = Path.Combine(destDir, info.FileName);
                        using (FileStream fs = new FileStream(destFile, FileMode.Create, FileAccess.Write))
                        {
                            EncodedFile.ExtractFile(_sc, info.FolderName, info.FileName, fs);
                        }

                        MessageBox.Show("File successfully extracted.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        Global.Logger.SystemWrite(new LogInfo(LogState.Error, ex));
                        MessageBox.Show($"Extraction failed.\r\n\r\n[Message]\r\n{Logger.LogExceptionMessage(ex)}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void DeleteFolderButton_Click(object sender, RoutedEventArgs e)
        {
            AttachedFileItem item = m.AttachSelected;
            if (item == null)
                return;

            Debug.Assert(item.Detail == null);

            MessageBoxResult result = MessageBox.Show(
                $"Are you sure to delete [{item.Name}]?",
                "Delete Confirm",
                MessageBoxButton.YesNo,
                MessageBoxImage.Error);
            if (result == MessageBoxResult.No)
                return;

            string errMsg;
            (_sc, errMsg) = EncodedFile.DeleteFolder(_sc, item.Name);
            if (errMsg == null)
            {
                m.ScriptAttachUpdated = true;
                ReadScriptAttachment();

                m.AttachSelected = null;
                m.UpdateAttachFileDetail();
            }
            else // Failure
            {
                Global.Logger.SystemWrite(new LogInfo(LogState.Error, errMsg));
                MessageBox.Show($"Delete failed.\r\n\r\n[Message]\r\n{errMsg}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AttachNewFileChooseButto_Click(object sender, RoutedEventArgs e)
        {
            AttachedFileItem item = m.AttachSelected;
            if (item == null)
                return;

            Debug.Assert(item.Detail == null);

            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "All Files|*.*",
            };

            if (dialog.ShowDialog() == true)
            {
                m.AttachNewFilePath = dialog.FileName;
                m.AttachNewFileName = Path.GetFileName(dialog.FileName);
            }
        }

        private void AttachFileButton_Click(object sender, RoutedEventArgs e)
        {
            AttachedFileItem item = m.AttachSelected;
            if (item == null)
                return;

            Debug.Assert(item.Detail == null);

            string srcFile = m.AttachNewFilePath;
            if (!File.Exists(srcFile))
            {
                MessageBox.Show($"Unable to find file [{srcFile}]", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(m.AttachNewFileName))
            {
                MessageBox.Show("File name is empty", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            EncodedFile.EncodeMode mode;
            switch (m.AttachNewCompressionIndex)
            {
                case 0:
                    mode = EncodedFile.EncodeMode.Raw;
                    break;
                case 1:
                    mode = EncodedFile.EncodeMode.ZLib;
                    break;
                case 2:
                    mode = EncodedFile.EncodeMode.XZ;
                    break;
                default:
                    MessageBox.Show("Internal Logic Error at ScriptEditWindow.AttachFileButton_Click", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
            }

            try
            {
                if (EncodedFile.ContainsFile(_sc, item.Name, m.AttachNewFileName))
                {
                    MessageBoxResult result = MessageBox.Show(
                        $"Attached file [{m.AttachNewFileName}] will be overwritten.\r\nContinue?",
                        "Confirm",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Error);
                    if (result == MessageBoxResult.No)
                        return;
                }

                _sc = EncodedFile.AttachFile(_sc, item.Name, m.AttachNewFileName, srcFile, mode);
                MessageBox.Show("File successfully attached.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                m.AttachNewFilePath = string.Empty;
                m.AttachNewFileName = string.Empty;

                m.ScriptAttachUpdated = true;
                ReadScriptAttachment();

                m.AttachSelected = null;
                m.UpdateAttachFileDetail();
            }
            catch (Exception ex)
            {
                Global.Logger.SystemWrite(new LogInfo(LogState.Error, ex));
                MessageBox.Show($"Attach failed.\r\n\r\n[Message]\r\n{Logger.LogExceptionMessage(ex)}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Buttons for File
        private void ExtractFileButton_Click(object sender, RoutedEventArgs e)
        {
            AttachedFileItem item = m.AttachSelected;
            if (item == null)
                return;

            Debug.Assert(item.Detail != null);

            EncodedFileInfo info = item.Detail;

            string ext = Path.GetExtension(info.FileName);
            SaveFileDialog dialog = new SaveFileDialog
            {
                OverwritePrompt = true,
                Filter = $"{ext} file|*{ext}"
            };

            if (dialog.ShowDialog() == true)
            {
                string destPath = dialog.FileName;
                try
                {
                    using (FileStream fs = new FileStream(destPath, FileMode.Create, FileAccess.Write))
                    {
                        EncodedFile.ExtractFile(_sc, info.FolderName, info.FileName, fs);
                    }

                    MessageBox.Show("File successfully extracted.", "Extract Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    Global.Logger.SystemWrite(new LogInfo(LogState.Error, ex));
                    MessageBox.Show($"Extraction failed.\r\n\r\n[Message]\r\n{Logger.LogExceptionMessage(ex)}", "Extract Failure", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void DeleteFileButton_Click(object sender, RoutedEventArgs e)
        {
            AttachedFileItem item = m.AttachSelected;
            if (item == null)
                return;

            Debug.Assert(item.Detail != null);
            EncodedFileInfo info = item.Detail;

            MessageBoxResult result = MessageBox.Show(
                $"Are you sure to delete [{info.FileName}]?",
                "Delete Confirm",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.No)
                return;

            string errMsg;
            (_sc, errMsg) = EncodedFile.DeleteFile(_sc, info.FolderName, info.FileName);
            if (errMsg == null)
            {
                m.ScriptAttachUpdated = true;
                ReadScriptAttachment();

                m.AttachSelected = null;
                m.UpdateAttachFileDetail();
            }
            else // Failure
            {
                Global.Logger.SystemWrite(new LogInfo(LogState.Error, errMsg));
                MessageBox.Show($"Delete failed.\r\n\r\n[Message]\r\n{errMsg}", "Delete Failure", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InspectFileButton_Click(object sender, RoutedEventArgs e)
        {
            AttachedFileItem item = m.AttachSelected;
            if (item == null)
                return;

            Debug.Assert(item.Detail != null);
            EncodedFileInfo info = item.Detail;
            string dirName = info.FolderName;
            string fileName = info.FileName;

            string errMsg;
            (info, errMsg) = EncodedFile.GetFileInfo(_sc, dirName, fileName, true);
            if (errMsg == null)
            {
                item.Detail = info;
                m.UpdateAttachFileDetail();
            }
            else // Failure
            {
                Global.Logger.SystemWrite(new LogInfo(LogState.Error, errMsg));
                MessageBox.Show($"Unable to inspect file [{fileName}]\r\n\r\n[Message]\r\n{errMsg}", "Inspect Failure", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #endregion
    }
    #endregion

    #region ScriptEditViewModel
    public class ScriptEditViewModel : INotifyPropertyChanged
    {
        #region Constructor
        public ScriptEditViewModel()
        {
            DragCanvas canvas = new DragCanvas
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(10, 10, 10, 10),
            };
            Grid.SetRow(canvas, 0);
            Grid.SetColumn(canvas, 0);
            Panel.SetZIndex(canvas, -1);

            InterfaceCanvas = canvas;
        }
        #endregion

        #region Events
        public class UIControlModifiedEventArgs : EventArgs
        {
            public UIControl UIControl { get; set; }
            public bool Direct { get; set; }

            public UIControlModifiedEventArgs(UIControl uiCtrl, bool direct)
            {
                UIControl = uiCtrl;
                Direct = direct;
            }
        }
        public delegate void UIControlModifiedHandler(object sender, UIControlModifiedEventArgs e);
        public event UIControlModifiedHandler UIControlModified;
        #endregion

        #region Property - Tab Index
        private int _tabIndex = 0;
        public int TabIndex
        {
            get => _tabIndex;
            set
            {
                _tabIndex = value;
                OnPropertyUpdate(nameof(TabIndex));
            }
        }
        #endregion

        #region Property - General - Script Header
        public bool ScriptHeaderNotSaved { get; set; } = false;
        public bool ScriptHeaderUpdated { get; set; } = false;

        private string _scriptTitle = string.Empty;
        public string ScriptTitle
        {
            get => _scriptTitle;
            set
            {
                _scriptTitle = value;
                ScriptHeaderNotSaved = true;
                ScriptHeaderUpdated = true;
                OnPropertyUpdate(nameof(ScriptTitle));
            }
        }

        private string _scriptAuthor = string.Empty;
        public string ScriptAuthor
        {
            get => _scriptAuthor;
            set
            {
                _scriptAuthor = value;
                ScriptHeaderNotSaved = true;
                ScriptHeaderUpdated = true;
                OnPropertyUpdate(nameof(ScriptAuthor));
            }
        }

        private string _scriptVersion = "0";
        public string ScriptVersion
        {
            get => _scriptVersion;
            set
            {
                _scriptVersion = value;
                ScriptHeaderNotSaved = true;
                ScriptHeaderUpdated = true;
                OnPropertyUpdate(nameof(ScriptVersion));
            }
        }

        private string _scriptDate = string.Empty;
        public string ScriptDate
        {
            get => _scriptDate;
            set
            {
                _scriptDate = value;
                ScriptHeaderNotSaved = true;
                ScriptHeaderUpdated = true;
                OnPropertyUpdate(nameof(ScriptDate));
            }
        }

        private decimal _scriptLevel = 0;
        public decimal ScriptLevel
        {
            get => _scriptLevel;
            set
            {
                _scriptLevel = value;
                ScriptHeaderNotSaved = true;
                ScriptHeaderUpdated = true;
                OnPropertyUpdate(nameof(ScriptLevel));
            }
        }

        private string _scriptDescription = string.Empty;
        public string ScriptDescription
        {
            get => _scriptDescription;
            set
            {
                _scriptDescription = value;
                ScriptHeaderNotSaved = true;
                ScriptHeaderUpdated = true;
                OnPropertyUpdate(nameof(ScriptDescription));
            }
        }

        private SelectedState _scriptSelectedState = SelectedState.False;
        public SelectedState ScriptSelectedState
        {
            get => _scriptSelectedState;
            set
            {
                _scriptSelectedState = value;
                ScriptHeaderNotSaved = true;
                ScriptHeaderUpdated = true;
                OnPropertyUpdate(nameof(ScriptSelected));
            }
        }

        [SuppressMessage("ReSharper", "RedundantCaseLabel")]
        public bool? ScriptSelected
        {
            get
            {
                switch (_scriptSelectedState)
                {
                    case SelectedState.True:
                        return true;
                    default:
                    case SelectedState.False:
                        return false;
                    case SelectedState.None:
                        return null;
                }
            }
            set
            {
                switch (value)
                {
                    case true:
                        _scriptSelectedState = SelectedState.True;
                        break;
                    default:
                    case false:
                        _scriptSelectedState = SelectedState.False;
                        break;
                    case null:
                        _scriptSelectedState = SelectedState.None;
                        break;
                }
                ScriptHeaderNotSaved = true;
                ScriptHeaderUpdated = true;
            }
        }

        private bool _scriptMandatory = false;
        public bool ScriptMandatory
        {
            get => _scriptMandatory;
            set
            {
                _scriptMandatory = value;
                ScriptHeaderNotSaved = true;
                ScriptHeaderUpdated = true;
                OnPropertyUpdate(nameof(ScriptMandatory));
            }
        }
        #endregion

        #region Property - General - Script Logo
        public bool ScriptLogoUpdated { get; set; } = false;

        #region ScriptLogo
        private PackIconMaterialKind? _scriptLogoIcon;
        public PackIconMaterialKind? ScriptLogoIcon
        {
            get => _scriptLogoIcon;
            set
            {
                _scriptLogoIcon = value;
                _scriptLogoImage = null;
                _scriptLogoSvg = null;
                OnPropertyUpdate(nameof(ScriptLogoIcon));
                OnPropertyUpdate(nameof(ScriptLogoImage));
                OnPropertyUpdate(nameof(ScriptLogoSvg));
            }
        }

        private ImageSource _scriptLogoImage;
        public ImageSource ScriptLogoImage
        {
            get => _scriptLogoImage;
            set
            {
                _scriptLogoIcon = null;
                _scriptLogoImage = value;
                _scriptLogoSvg = null;
                OnPropertyUpdate(nameof(ScriptLogoIcon));
                OnPropertyUpdate(nameof(ScriptLogoImage));
                OnPropertyUpdate(nameof(ScriptLogoSvg));
            }
        }

        private DrawingBrush _scriptLogoSvg;
        public DrawingBrush ScriptLogoSvg
        {
            get => _scriptLogoSvg;
            set
            {
                _scriptLogoIcon = null;
                _scriptLogoImage = null;
                _scriptLogoSvg = value;
                OnPropertyUpdate(nameof(ScriptLogoIcon));
                OnPropertyUpdate(nameof(ScriptLogoImage));
                OnPropertyUpdate(nameof(ScriptLogoSvg));
            }
        }

        private double _scriptLogoSvgWidth;
        public double ScriptLogoSvgWidth
        {
            get => _scriptLogoSvgWidth;
            set
            {
                _scriptLogoSvgWidth = value;
                OnPropertyUpdate(nameof(ScriptLogoSvgWidth));
            }
        }

        private double _scriptLogoSvgHeight;
        public double ScriptLogoSvgHeight
        {
            get => _scriptLogoSvgHeight;
            set
            {
                _scriptLogoSvgHeight = value;
                OnPropertyUpdate(nameof(ScriptLogoSvgHeight));
            }
        }
        #endregion

        private EncodedFileInfo _scriptLogoInfo;
        public EncodedFileInfo ScriptLogoInfo
        {
            get => _scriptLogoInfo;
            set
            {
                _scriptLogoInfo = value;
                OnPropertyUpdate(nameof(ScriptLogoLoaded));
                OnPropertyUpdate(nameof(ScriptLogoName));
                OnPropertyUpdate(nameof(ScriptLogoRawSize));
                OnPropertyUpdate(nameof(ScriptLogoEncodedSize));
                OnPropertyUpdate(nameof(ScriptLogoCompression));
                OnPropertyUpdate(nameof(ScriptLogoInfoVisibility));
            }
        }

        public Visibility ScriptLogoInfoVisibility => ScriptLogoInfo == null ? Visibility.Collapsed : Visibility.Visible;
        public bool ScriptLogoLoaded => ScriptLogoInfo != null;

        public string ScriptLogoName => ScriptLogoInfo == null ? string.Empty : ScriptLogoInfo.FileName;

        public string ScriptLogoRawSize
        {
            get
            {
                if (ScriptLogoInfo == null)
                    return string.Empty; // Invalid value

                string str = NumberHelper.ByteSizeToSIUnit(ScriptLogoInfo.RawSize, 1);
                return $"{str} ({ScriptLogoInfo.RawSize})";
            }
        }

        public string ScriptLogoEncodedSize
        {
            get
            {
                if (ScriptLogoInfo == null)
                    return string.Empty; // Invalid value

                string str = NumberHelper.ByteSizeToSIUnit(ScriptLogoInfo.EncodedSize, 1);
                return $"{str} ({ScriptLogoInfo.EncodedSize})";
            }
        }

        public string ScriptLogoCompression
        {
            get
            {
                if (ScriptLogoInfo == null)
                    return string.Empty; // Empty value

                return ScriptLogoInfo.EncodeMode == null ? string.Empty : EncodedFile.EncodeModeStr(ScriptLogoInfo.EncodeMode, false);
            }
        }
        #endregion

        #region Property - Interface - Panels
        public bool InterfaceNotSaved { get; set; } = false;
        public bool InterfaceUpdated { get; set; } = false;

        // Canvas
        // private EditCanvas _interfaceCanvas;
        // public EditCanvas InterfaceCanvas
        private DragCanvas _interfaceCanvas;
        public DragCanvas InterfaceCanvas
        {
            get => _interfaceCanvas;
            set
            {
                _interfaceCanvas = value;
                OnPropertyUpdate(nameof(InterfaceCanvas));
            }
        }
        private double _interfaceScaleFactor = 100;
        public double InterfaceScaleFactor
        {
            get => _interfaceScaleFactor;
            set
            {
                _interfaceScaleFactor = value;
                OnPropertyUpdate(nameof(InterfaceScaleFactor));
            }
        }

        // Add
        private int _uiCtrlAddTypeIndex = -1;
        public int UICtrlAddTypeIndex
        {
            get => _uiCtrlAddTypeIndex;
            set
            {
                _uiCtrlAddTypeIndex = value;
                OnPropertyUpdate(nameof(UICtrlAddTypeIndex));
            }
        }
        private string _uiCtrlAddName;
        public string UICtrlAddName
        {
            get => _uiCtrlAddName;
            set
            {
                _uiCtrlAddName = value;
                OnPropertyUpdate(nameof(UICtrlAddName));
            }
        }

        // Delete
        public List<UIControl> UICtrlToBeDeleted = new List<UIControl>();
        public List<string> UICtrlKeyChanged = new List<string>();
        #endregion

        #region Property - Interface - Editor
        // Editor
        private bool _interfaceLoaded = false;
        public bool InterfaceLoaded
        {
            get => _interfaceLoaded;
            set
            {
                _interfaceLoaded = value;
                OnPropertyUpdate(nameof(InterfaceLoaded));
            }
        }
        private ObservableCollection<string> _interfaceUICtrls = new ObservableCollection<string>();
        public ObservableCollection<string> InterfaceUICtrls
        {
            get => _interfaceUICtrls;
            set
            {
                _interfaceUICtrls = value;
                OnPropertyUpdate(nameof(InterfaceUICtrls));
            }
        }
        private int _interfaceUICtrlIndex;
        public int InterfaceUICtrlIndex
        {
            get => _interfaceUICtrlIndex;
            set
            {
                _interfaceUICtrlIndex = value;
                OnPropertyUpdate(nameof(InterfaceUICtrlIndex));
            }
        }
        private UIControl _selectedUICtrl = null;
        public UIControl SelectedUICtrl
        {
            get => _selectedUICtrl;
            set
            {
                _selectedUICtrl = value;

                // UIControl Shared Argument
                OnPropertyUpdate(nameof(UICtrlEditEnabled));
                OnPropertyUpdate(nameof(UICtrlKey));
                OnPropertyUpdate(nameof(UICtrlText));
                OnPropertyUpdate(nameof(UICtrlVisible));
                OnPropertyUpdate(nameof(UICtrlX));
                OnPropertyUpdate(nameof(UICtrlY));
                OnPropertyUpdate(nameof(UICtrlWidth));
                OnPropertyUpdate(nameof(UICtrlHeight));
                OnPropertyUpdate(nameof(UICtrlToolTip));

                // UIControl Visibility
                OnPropertyUpdate(nameof(IsUICtrlTextBox));
                OnPropertyUpdate(nameof(IsUICtrlTextLabel));
                OnPropertyUpdate(nameof(IsUICtrlNumberBox));
                OnPropertyUpdate(nameof(IsUICtrlCheckBox));
                OnPropertyUpdate(nameof(IsUICtrlComboBox));
                OnPropertyUpdate(nameof(IsUICtrlImage));
                OnPropertyUpdate(nameof(IsUICtrlTextFile));
                OnPropertyUpdate(nameof(IsUICtrlButton));
                OnPropertyUpdate(nameof(IsUICtrlWebLabel));
                OnPropertyUpdate(nameof(IsUICtrlRadioButton));
                OnPropertyUpdate(nameof(IsUICtrlBevel));
                OnPropertyUpdate(nameof(IsUICtrlFileBox));
                OnPropertyUpdate(nameof(IsUICtrlRadioGroup));
                OnPropertyUpdate(nameof(ShowUICtrlListItemBox));
                OnPropertyUpdate(nameof(ShowUICtrlRunOptional));
            }
        }

        #region Shared Arguments
        public bool UICtrlEditEnabled => _selectedUICtrl != null;
        public string UICtrlKey
        {
            get => _selectedUICtrl != null ? _selectedUICtrl.Key : string.Empty;
            set
            {
                if (_selectedUICtrl == null)
                    return;

                // Prevent other key being overwritten
                if (_interfaceUICtrls.Contains(value, StringComparer.OrdinalIgnoreCase))
                    return; // Ignore

                // Remove this key later
                UICtrlKeyChanged.Add(_selectedUICtrl.Key);

                _selectedUICtrl.Key = value;
                InvokeUIControlEvent(true);
            }
        }
        public string UICtrlText
        {
            get => _selectedUICtrl != null ? _selectedUICtrl.Text : string.Empty;
            set
            {
                if (_selectedUICtrl == null)
                    return;

                _selectedUICtrl.Text = value;
                InvokeUIControlEvent(true);
            }
        }
        public bool UICtrlVisible
        {
            get => _selectedUICtrl != null && _selectedUICtrl.Visibility;
            set
            {
                if (_selectedUICtrl == null)
                    return;

                _selectedUICtrl.Visibility = value;
                InvokeUIControlEvent(true);
            }
        }
        public int UICtrlX
        {
            get => _selectedUICtrl != null ? (int)_selectedUICtrl.Rect.X : 0;
            set
            {
                if (_selectedUICtrl == null)
                    return;

                _selectedUICtrl.X = value;
                InvokeUIControlEvent(true);
            }
        }
        public int UICtrlY
        {
            get => _selectedUICtrl != null ? (int)_selectedUICtrl.Rect.Y : 0;
            set
            {
                if (_selectedUICtrl == null)
                    return;

                _selectedUICtrl.Y = value;
                InvokeUIControlEvent(true);
            }
        }
        public int UICtrlWidth
        {
            get => _selectedUICtrl != null ? (int)_selectedUICtrl.Rect.Width : 0;
            set
            {
                if (_selectedUICtrl == null)
                    return;

                _selectedUICtrl.Width = value;
                InvokeUIControlEvent(true);
            }
        }
        public int UICtrlHeight
        {
            get => _selectedUICtrl != null ? (int)_selectedUICtrl.Rect.Height : 0;
            set
            {
                if (_selectedUICtrl == null)
                    return;

                _selectedUICtrl.Height = value;
                InvokeUIControlEvent(true);
            }
        }
        public string UICtrlToolTip
        {
            get => _selectedUICtrl != null ? _selectedUICtrl.Info.ToolTip : string.Empty;
            set
            {
                if (_selectedUICtrl == null)
                    return;

                _selectedUICtrl.Info.ToolTip = value;
                InvokeUIControlEvent(true);
            }
        }
        #endregion

        #region IsUICtrl Visibility Series
        public Visibility IsUICtrlTextBox => _selectedUICtrl != null && _selectedUICtrl.Type == UIControlType.TextBox ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IsUICtrlTextLabel => _selectedUICtrl != null && _selectedUICtrl.Type == UIControlType.TextLabel ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IsUICtrlNumberBox => _selectedUICtrl != null && _selectedUICtrl.Type == UIControlType.NumberBox ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IsUICtrlCheckBox => _selectedUICtrl != null && _selectedUICtrl.Type == UIControlType.CheckBox ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IsUICtrlComboBox => _selectedUICtrl != null && _selectedUICtrl.Type == UIControlType.ComboBox ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IsUICtrlImage => _selectedUICtrl != null && _selectedUICtrl.Type == UIControlType.Image ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IsUICtrlTextFile => _selectedUICtrl != null && _selectedUICtrl.Type == UIControlType.TextFile ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IsUICtrlButton => _selectedUICtrl != null && _selectedUICtrl.Type == UIControlType.Button ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IsUICtrlWebLabel => _selectedUICtrl != null && _selectedUICtrl.Type == UIControlType.WebLabel ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IsUICtrlRadioButton => _selectedUICtrl != null && _selectedUICtrl.Type == UIControlType.RadioButton ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IsUICtrlBevel => _selectedUICtrl != null && _selectedUICtrl.Type == UIControlType.Bevel ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IsUICtrlFileBox => _selectedUICtrl != null && _selectedUICtrl.Type == UIControlType.FileBox ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IsUICtrlRadioGroup => _selectedUICtrl != null && _selectedUICtrl.Type == UIControlType.RadioGroup ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ShowUICtrlListItemBox
        {
            get
            {
                if (_selectedUICtrl == null)
                    return Visibility.Collapsed;

                switch (_selectedUICtrl.Type)
                {
                    case UIControlType.ComboBox:
                    case UIControlType.RadioGroup:
                        return Visibility.Visible;
                    default:
                        return Visibility.Collapsed;
                }
            }
        }
        public Visibility ShowUICtrlRunOptional
        {
            get
            {
                if (_selectedUICtrl == null)
                    return Visibility.Collapsed;

                switch (_selectedUICtrl.Type)
                {
                    case UIControlType.CheckBox:
                    case UIControlType.ComboBox:
                    case UIControlType.Button:
                    case UIControlType.RadioButton:
                    case UIControlType.RadioGroup:
                        return Visibility.Visible;
                    default:
                        return Visibility.Collapsed;
                }
            }
        }
        #endregion

        #region For TextBox
        private UIInfo_TextBox _uiCtrlTextBoxInfo;
        public UIInfo_TextBox UICtrlTextBoxInfo
        {
            get => _uiCtrlTextBoxInfo;
            set
            {
                _uiCtrlTextBoxInfo = value;
                if (value == null)
                    return;

                OnPropertyUpdate(nameof(UICtrlTextBoxValue));
            }
        }
        public string UICtrlTextBoxValue
        {
            get => _uiCtrlTextBoxInfo?.Value ?? string.Empty;
            set
            {
                if (_uiCtrlTextBoxInfo == null)
                    return;

                _uiCtrlTextBoxInfo.Value = value;
                OnPropertyUpdate(nameof(UICtrlTextBoxValue));
                InvokeUIControlEvent(true);
            }
        }
        #endregion
        #region For TextLabel
        private UIInfo_TextLabel _uiCtrlTextLabelInfo;
        public UIInfo_TextLabel UICtrlTextLabelInfo
        {
            get => _uiCtrlTextLabelInfo;
            set
            {
                _uiCtrlTextLabelInfo = value;
                if (value == null)
                    return;

                OnPropertyUpdate(nameof(UICtrlTextLabelFontSize));
                OnPropertyUpdate(nameof(UICtrlTextLabelFontWeightIndex));
                OnPropertyUpdate(nameof(UICtrlTextLabelFontStyleIndex));
            }
        }
        public int UICtrlTextLabelFontSize
        {
            get => _uiCtrlTextLabelInfo?.FontSize ?? 8;
            set
            {
                if (_uiCtrlTextLabelInfo == null)
                    return;

                _uiCtrlTextLabelInfo.FontSize = value;
                OnPropertyUpdate(nameof(UICtrlTextLabelFontSize));
                InvokeUIControlEvent(true);
            }
        }
        public int UICtrlTextLabelFontWeightIndex
        {
            get => (int)(_uiCtrlTextLabelInfo?.FontWeight ?? UIFontWeight.Normal);
            set
            {
                if (_uiCtrlTextLabelInfo == null)
                    return;

                _uiCtrlTextLabelInfo.FontWeight = (UIFontWeight)value;
                OnPropertyUpdate(nameof(UICtrlTextLabelFontWeightIndex));
                InvokeUIControlEvent(true);
            }
        }
        public int UICtrlTextLabelFontStyleIndex
        {
            get
            {
                if (_uiCtrlTextLabelInfo?.FontStyle == null)
                    return 0;
                return (int)_uiCtrlTextLabelInfo.FontStyle + 1;
            }
            set
            {
                if (_uiCtrlTextLabelInfo == null)
                    return;

                if (value == 0)
                    _uiCtrlTextLabelInfo.FontStyle = null;
                else
                    _uiCtrlTextLabelInfo.FontStyle = (UIFontStyle)(value - 1);
                OnPropertyUpdate(nameof(UICtrlTextLabelFontStyleIndex));
                InvokeUIControlEvent(true);
            }
        }
        #endregion
        #region For NumberBox
        private UIInfo_NumberBox _uiCtrlNumberBoxInfo;
        public UIInfo_NumberBox UICtrlNumberBoxInfo
        {
            get => _uiCtrlNumberBoxInfo;
            set
            {
                _uiCtrlNumberBoxInfo = value;
                if (value == null)
                    return;

                OnPropertyUpdate(nameof(UICtrlNumberBoxValue));
                OnPropertyUpdate(nameof(UICtrlNumberBoxMin));
                OnPropertyUpdate(nameof(UICtrlNumberBoxMax));
                OnPropertyUpdate(nameof(UICtrlNumberBoxTick));
            }
        }
        public int UICtrlNumberBoxValue
        {
            get => _uiCtrlNumberBoxInfo?.Value ?? 0;
            set
            {
                if (_uiCtrlNumberBoxInfo == null)
                    return;

                _uiCtrlNumberBoxInfo.Value = value;
                OnPropertyUpdate(nameof(UICtrlNumberBoxValue));
                InvokeUIControlEvent(true);
            }
        }
        public int UICtrlNumberBoxMin
        {
            get => _uiCtrlNumberBoxInfo?.Min ?? 0;
            set
            {
                if (_uiCtrlNumberBoxInfo == null)
                    return;

                _uiCtrlNumberBoxInfo.Min = value;
                OnPropertyUpdate(nameof(UICtrlNumberBoxMin));
                InvokeUIControlEvent(true);
            }
        }
        public int UICtrlNumberBoxMax
        {
            get => _uiCtrlNumberBoxInfo?.Max ?? 100;
            set
            {
                if (_uiCtrlNumberBoxInfo == null)
                    return;

                _uiCtrlNumberBoxInfo.Max = value;
                OnPropertyUpdate(nameof(UICtrlNumberBoxMax));
                InvokeUIControlEvent(true);
            }
        }
        public int UICtrlNumberBoxTick
        {
            get => _uiCtrlNumberBoxInfo?.Tick ?? 1;
            set
            {
                if (_uiCtrlNumberBoxInfo == null)
                    return;

                _uiCtrlNumberBoxInfo.Tick = value;
                OnPropertyUpdate(nameof(UICtrlNumberBoxTick));
                InvokeUIControlEvent(true);
            }
        }
        #endregion
        #region For CheckBox
        private UIInfo_CheckBox _uiCtrlCheckBoxInfo;
        public UIInfo_CheckBox UICtrlCheckBoxInfo
        {
            get => _uiCtrlCheckBoxInfo;
            set
            {
                _uiCtrlCheckBoxInfo = value;
                if (value == null)
                    return;

                OnPropertyUpdate(nameof(UICtrlCheckBoxValue));
            }
        }
        public bool UICtrlCheckBoxValue
        {
            get => _uiCtrlCheckBoxInfo?.Value ?? false;
            set
            {
                if (_uiCtrlCheckBoxInfo == null)
                    return;

                _uiCtrlCheckBoxInfo.Value = value;
                OnPropertyUpdate(nameof(UICtrlCheckBoxValue));
                InvokeUIControlEvent(true);
            }
        }
        #endregion
        #region For ComboBox
        private UIInfo_ComboBox _uiCtrlComboBoxInfo;
        public UIInfo_ComboBox UICtrlComboBoxInfo
        {
            get => _uiCtrlComboBoxInfo;
            set
            {
                _uiCtrlComboBoxInfo = value;
                if (value == null)
                    return;

                UICtrlListItemBoxItems = new ObservableCollection<string>(_uiCtrlComboBoxInfo.Items);
                UICtrlListItemBoxSelectedIndex = _uiCtrlComboBoxInfo.Index;
                UICtrlListItemBoxNewItem = string.Empty;
            }
        }
        #endregion
        #region For Image
        private UIInfo_Image _uiCtrlImageInfo;
        public UIInfo_Image UICtrlImageInfo
        {
            get => _uiCtrlImageInfo;
            set
            {
                _uiCtrlImageInfo = value;
                if (value == null)
                    return;

                OnPropertyUpdate(nameof(UICtrlImageUrl));
            }
        }
        private bool _uiCtrlImageSet = false;
        public bool UICtrlImageSet
        {
            get => _uiCtrlImageSet;
            set
            {
                _uiCtrlImageSet = value;
                OnPropertyUpdate(nameof(UICtrlImageLoaded));
                OnPropertyUpdate(nameof(UICtrlImageUnloaded));
            }
        }
        public Visibility UICtrlImageLoaded => _uiCtrlImageSet ? Visibility.Visible : Visibility.Collapsed;
        public Visibility UICtrlImageUnloaded => !_uiCtrlImageSet ? Visibility.Visible : Visibility.Collapsed;
        public string UICtrlImageUrl
        {
            get => _uiCtrlImageInfo?.Url ?? string.Empty;
            set
            {
                if (_uiCtrlImageInfo == null)
                    return;

                _uiCtrlImageInfo.Url = string.IsNullOrWhiteSpace(value) ? null : value;
                OnPropertyUpdate(nameof(UICtrlImageInfo.Url));
                InvokeUIControlEvent(true);
            }
        }
        #endregion
        #region For TextFile
        private bool _uiCtrlTextFileSet = false;
        public bool UICtrlTextFileSet
        {
            get => _uiCtrlTextFileSet;
            set
            {
                _uiCtrlTextFileSet = value;
                OnPropertyUpdate(nameof(UICtrlTextFileLoaded));
                OnPropertyUpdate(nameof(UICtrlTextFileUnloaded));
            }
        }
        public Visibility UICtrlTextFileLoaded => _uiCtrlTextFileSet ? Visibility.Visible : Visibility.Collapsed;
        public Visibility UICtrlTextFileUnloaded => !_uiCtrlTextFileSet ? Visibility.Visible : Visibility.Collapsed;
        #endregion
        #region For Button
        public UIInfo_Button UICtrlButtonInfo { get; set; }
        private bool _uiCtrlButtonPictureSet = false;
        public bool UICtrlButtonPictureSet
        {
            get => _uiCtrlButtonPictureSet;
            set
            {
                _uiCtrlButtonPictureSet = value;
                OnPropertyUpdate(nameof(UICtrlButtonPictureLoaded));
                OnPropertyUpdate(nameof(UICtrlButtonPictureUnloaded));
            }
        }
        public Visibility UICtrlButtonPictureLoaded => _uiCtrlButtonPictureSet ? Visibility.Visible : Visibility.Collapsed;
        public Visibility UICtrlButtonPictureUnloaded => !_uiCtrlButtonPictureSet ? Visibility.Visible : Visibility.Collapsed;
        #endregion
        #region For WebLabel
        private UIInfo_WebLabel _uiCtrlWebLabelInfo;
        public UIInfo_WebLabel UICtrlWebLabelInfo
        {
            get => _uiCtrlWebLabelInfo;
            set
            {
                _uiCtrlWebLabelInfo = value;
                if (value == null)
                    return;

                OnPropertyUpdate(nameof(UICtrlWebLabelUrl));
            }
        }
        public string UICtrlWebLabelUrl
        {
            get => _uiCtrlWebLabelInfo?.Url ?? string.Empty;
            set
            {
                if (_uiCtrlWebLabelInfo == null)
                    return;

                _uiCtrlWebLabelInfo.Url = string.IsNullOrWhiteSpace(value) ? null : value;
                OnPropertyUpdate(nameof(UICtrlWebLabelInfo.Url));
                InvokeUIControlEvent(true);
            }
        }
        #endregion
        #region For RadioButton
        public List<UIControl> UICtrlRadioButtonList { get; set; }
        private UIInfo_RadioButton _uiCtrlRadioButtonInfo;
        public UIInfo_RadioButton UICtrlRadioButtonInfo
        {
            get => _uiCtrlRadioButtonInfo;
            set
            {
                _uiCtrlRadioButtonInfo = value;
                if (value == null)
                    return;

                OnPropertyUpdate(nameof(UICtrlRadioButtonSelectEnable));
            }
        }
        public bool UICtrlRadioButtonSelectEnable => !_uiCtrlRadioButtonInfo?.Selected ?? false;
        #endregion
        #region For Bevel
        private UIInfo_Bevel _uiCtrlBevelInfo;
        public UIInfo_Bevel UICtrlBevelInfo
        {
            get => _uiCtrlBevelInfo;
            set
            {
                _uiCtrlBevelInfo = value;
                if (value == null)
                    return;

                OnPropertyUpdate(nameof(UICtrlBevelCaptionEnabled));
                OnPropertyUpdate(nameof(UICtrlBevelFontSize));
                OnPropertyUpdate(nameof(UICtrlBevelFontWeightIndex));
                OnPropertyUpdate(nameof(UICtrlBevelFontStyleIndex));
            }
        }
        public bool UICtrlBevelCaptionEnabled
        {
            get
            {
                if (_selectedUICtrl == null || _selectedUICtrl.Type != UIControlType.Bevel)
                    return false;

                return UICtrlBevelInfo.CaptionEnabled;
            }
            set
            {
                UICtrlBevelInfo.CaptionEnabled = value;
                OnPropertyUpdate(nameof(UICtrlBevelCaptionEnabled));
                InvokeUIControlEvent(true);
            }
        }
        public string UICtrlBevelCaption
        {
            get
            {
                if (_selectedUICtrl == null || _selectedUICtrl.Type != UIControlType.Bevel)
                    return string.Empty;

                return UICtrlBevelInfo.CaptionEnabled ? _selectedUICtrl.Text : string.Empty;
            }
            set
            {
                if (_selectedUICtrl == null || _selectedUICtrl.Type != UIControlType.Bevel)
                    return;

                _selectedUICtrl.Text = value;
                OnPropertyUpdate(nameof(UICtrlBevelFontSize));
                InvokeUIControlEvent(true);
            }
        }
        public int UICtrlBevelFontSize
        {
            get => _uiCtrlBevelInfo?.FontSize ?? UIControl.DefaultFontPoint;
            set
            {
                if (_uiCtrlBevelInfo == null)
                    return;

                UICtrlBevelInfo.CaptionEnabled = true;

                _uiCtrlBevelInfo.FontSize = value;
                OnPropertyUpdate(nameof(UICtrlBevelFontSize));
                InvokeUIControlEvent(true);
            }
        }
        public int UICtrlBevelFontWeightIndex
        {
            get => (int?)_uiCtrlBevelInfo?.FontWeight ?? 0;
            set
            {
                if (_uiCtrlBevelInfo == null)
                    return;

                UICtrlBevelInfo.CaptionEnabled = true;

                _uiCtrlBevelInfo.FontWeight = (UIFontWeight)value;
                OnPropertyUpdate(nameof(UICtrlBevelFontWeightIndex));
                InvokeUIControlEvent(true);
            }
        }
        public int UICtrlBevelFontStyleIndex
        {
            get
            {
                if (_uiCtrlBevelInfo?.FontStyle == null)
                    return 0;
                return (int)_uiCtrlBevelInfo.FontStyle + 1;
            }
            set
            {
                if (_uiCtrlBevelInfo == null)
                    return;

                UICtrlBevelInfo.CaptionEnabled = true;

                if (value == 0)
                    _uiCtrlBevelInfo.FontStyle = null;
                else
                    _uiCtrlBevelInfo.FontStyle = (UIFontStyle)(value - 1);

                OnPropertyUpdate(nameof(UICtrlBevelFontStyleIndex));
                InvokeUIControlEvent(true);
            }
        }
        #endregion
        #region For FileBox
        private UIInfo_FileBox _uiCtrlFileBoxInfo;
        public UIInfo_FileBox UICtrlFileBoxInfo
        {
            get => _uiCtrlFileBoxInfo;
            set
            {
                _uiCtrlFileBoxInfo = value;
                if (value == null)
                    return;

                OnPropertyUpdate(nameof(UICtrlFileBoxFileChecked));
                OnPropertyUpdate(nameof(UICtrlFileBoxDirChecked));
            }
        }
        public bool UICtrlFileBoxFileChecked
        {
            get => _uiCtrlFileBoxInfo?.IsFile ?? false;
            set
            {
                if (_uiCtrlFileBoxInfo == null)
                    return;

                _uiCtrlFileBoxInfo.IsFile = value;
                OnPropertyUpdate(nameof(UICtrlFileBoxFileChecked));
                OnPropertyUpdate(nameof(UICtrlFileBoxDirChecked));
                InvokeUIControlEvent(true);
            }
        }
        public bool UICtrlFileBoxDirChecked
        {
            get => !_uiCtrlFileBoxInfo?.IsFile ?? false;
            set
            {
                if (_uiCtrlFileBoxInfo == null)
                    return;

                _uiCtrlFileBoxInfo.IsFile = !value;
                OnPropertyUpdate(nameof(UICtrlFileBoxFileChecked));
                OnPropertyUpdate(nameof(UICtrlFileBoxDirChecked));
                InvokeUIControlEvent(true);
            }
        }
        #endregion
        #region For RadioGroup
        private UIInfo_RadioGroup _uiCtrlRadioGroupInfo;
        public UIInfo_RadioGroup UICtrlRadioGroupInfo
        {
            get => _uiCtrlRadioGroupInfo;
            set
            {
                _uiCtrlRadioGroupInfo = value;
                if (value == null)
                    return;

                UICtrlListItemBoxItems = new ObservableCollection<string>(_uiCtrlRadioGroupInfo.Items);
                UICtrlListItemBoxSelectedIndex = _uiCtrlRadioGroupInfo.Selected;
                UICtrlListItemBoxNewItem = string.Empty;
            }
        }
        #endregion
        #region For (Common) ListItemBox
        public ObservableCollection<string> _uiCtrlListItemBoxItems;
        public ObservableCollection<string> UICtrlListItemBoxItems
        {
            get => _uiCtrlListItemBoxItems;
            set
            {
                _uiCtrlListItemBoxItems = value;
                OnPropertyUpdate(nameof(UICtrlListItemBoxItems));
                InvokeUIControlEvent(true);
            }
        }
        private int _uiCtrlListItemBoxSelectedIndex;
        public int UICtrlListItemBoxSelectedIndex
        {
            get => _uiCtrlListItemBoxSelectedIndex;
            set
            {
                _uiCtrlListItemBoxSelectedIndex = value;
                OnPropertyUpdate(nameof(UICtrlListItemBoxSelectedIndex));
            }
        }
        private string _uiCtrlListItemBoxNewItem;
        public string UICtrlListItemBoxNewItem
        {
            get => _uiCtrlListItemBoxNewItem;
            set
            {
                _uiCtrlListItemBoxNewItem = value;
                OnPropertyUpdate(nameof(UICtrlListItemBoxNewItem));
            }
        }
        #endregion
        #region For (Common) RunOptional
        private bool _uiCtrlRunOptionalEnabled;
        public bool UICtrlRunOptionalEnabled
        {
            get => _uiCtrlRunOptionalEnabled;
            set
            {
                _uiCtrlRunOptionalEnabled = value;
                OnPropertyUpdate(nameof(UICtrlRunOptionalEnabled));
                InvokeUIControlEvent(false);
            }
        }
        private string _uiCtrlSectionToRun;
        public string UICtrlSectionToRun
        {
            get => _uiCtrlSectionToRun;
            set
            {
                _uiCtrlSectionToRun = value;
                OnPropertyUpdate(nameof(UICtrlSectionToRun));
                InvokeUIControlEvent(false);
            }
        }
        private bool _uiCtrlHideProgress;
        public bool UICtrlHideProgress
        {
            get => _uiCtrlHideProgress;
            set
            {
                _uiCtrlHideProgress = value;
                OnPropertyUpdate(nameof(UICtrlHideProgress));
                InvokeUIControlEvent(false);
            }
        }
        #endregion
        #endregion

        #region Property - Attachment
        public bool ScriptAttachUpdated { get; set; } = false;

        public ObservableCollection<AttachedFileItem> AttachedFiles { get; private set; } = new ObservableCollection<AttachedFileItem>();

        public AttachedFileItem AttachSelected;

        public Visibility AttachDetailFileVisiblity
        {
            get
            {
                if (AttachSelected == null)
                    return Visibility.Collapsed;
                if (AttachSelected.IsFolder)
                    return Visibility.Collapsed;
                return Visibility.Visible;
            }
        }

        public Visibility AttachDetailFolderVisiblity
        {
            get
            {
                if (AttachSelected == null)
                    return Visibility.Collapsed;
                if (AttachSelected.IsFolder)
                    return Visibility.Visible;
                return Visibility.Collapsed;
            }
        }

        public Visibility AttachAddFolderVisiblity
        {
            get
            {
                if (AttachSelected == null)
                    return Visibility.Visible;
                if (AttachSelected.IsFolder)
                    return Visibility.Visible;
                return Visibility.Collapsed;
            }
        }

        private string _addFolderName = string.Empty;
        public string AddFolderName
        {
            get => _addFolderName;
            set
            {
                _addFolderName = value;
                OnPropertyUpdate(nameof(AddFolderName));
            }
        }

        public string AttachFileName
        {
            get
            {
                if (AttachSelected == null || AttachSelected.IsFolder)
                    return string.Empty; // Empty value
                return AttachSelected.Name;
            }
        }

        public string AttachFileRawSize
        {
            get
            {
                if (AttachSelected == null || AttachSelected.IsFolder)
                    return string.Empty; // Invalid value
                Debug.Assert(AttachSelected.Detail != null);

                string str = NumberHelper.ByteSizeToSIUnit(AttachSelected.Detail.RawSize, 1);
                return $"{str} ({AttachSelected.Detail.RawSize})";
            }
        }

        public string AttachFileEncodedSize
        {
            get
            {
                if (AttachSelected == null || AttachSelected.IsFolder)
                    return string.Empty; // Invalid value
                Debug.Assert(AttachSelected.Detail != null);

                string str = NumberHelper.ByteSizeToSIUnit(AttachSelected.Detail.EncodedSize, 1);
                return $"{str} ({AttachSelected.Detail.EncodedSize})";
            }
        }

        public string AttachFileCompression
        {
            get
            {
                if (AttachSelected == null || AttachSelected.IsFolder)
                    return string.Empty; // Empty value
                Debug.Assert(AttachSelected.Detail != null);

                return AttachSelected.Detail.EncodeMode == null ? "-" : EncodedFile.EncodeModeStr(AttachSelected.Detail.EncodeMode, false);
            }
        }

        private string _attachNewFilePath = string.Empty;
        public string AttachNewFilePath
        {
            get => _attachNewFilePath;
            set
            {
                _attachNewFilePath = value;
                OnPropertyUpdate(nameof(AttachNewFilePath));
            }
        }

        private string _attachNewFileName = string.Empty;
        public string AttachNewFileName
        {
            get => _attachNewFileName;
            set
            {
                _attachNewFileName = value;
                OnPropertyUpdate(nameof(AttachNewFileName));
            }
        }

        private int _attachNewCompressionIndex = 1;
        public int AttachNewCompressionIndex
        {
            get => _attachNewCompressionIndex;
            set
            {
                _attachNewCompressionIndex = value;
                OnPropertyUpdate(nameof(AttachNewCompressionIndex));
            }
        }
        #endregion

        #region UpdateAttachFileDetail

        public void UpdateAttachFileDetail()
        {
            OnPropertyUpdate(nameof(AttachFileName));
            OnPropertyUpdate(nameof(AttachFileRawSize));
            OnPropertyUpdate(nameof(AttachFileEncodedSize));
            OnPropertyUpdate(nameof(AttachFileCompression));
            OnPropertyUpdate(nameof(AttachDetailFileVisiblity));
            OnPropertyUpdate(nameof(AttachDetailFolderVisiblity));
            OnPropertyUpdate(nameof(AttachAddFolderVisiblity));
        }
        #endregion

        #region InvokeUIControlEvent
        public bool UIControlModifiedEventToggle = false;
        public void InvokeUIControlEvent(bool direct)
        {
            if (UIControlModifiedEventToggle)
                return;

            InterfaceNotSaved = true;
            InterfaceUpdated = true;
            UIControlModified?.Invoke(this, new UIControlModifiedEventArgs(_selectedUICtrl, direct));
        }
        #endregion

        #region OnPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyUpdate(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
    #endregion

    #region AttachedFilesItem
    public class AttachedFileItem : INotifyPropertyChanged
    {
        #region Constructor
        public AttachedFileItem(bool isFolder, string name, EncodedFileInfo detail = null)
        {
            if (!isFolder && detail == null) // If file, info must not be null
                throw new ArgumentException($"File's [{nameof(detail)}] must not be null");
            if (isFolder && detail != null) // If folder, info must be null
                throw new ArgumentException($"Folder's [{nameof(detail)}] must be null");

            IsFolder = isFolder;
            Name = name;
            Detail = detail;
            Icon = isFolder ? PackIconMaterialKind.Folder : PackIconMaterialKind.File;

            Children = new ObservableCollection<AttachedFileItem>();
        }
        #endregion

        #region Property - TreeView
        private bool _isFolder;
        public bool IsFolder
        {
            get => _isFolder;
            set
            {
                _isFolder = value;
                OnPropertyUpdate(nameof(IsFolder));
            }
        }

        private string _name;
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyUpdate(nameof(Name));
            }
        }

        // null if folder
        public EncodedFileInfo Detail;

        public ObservableCollection<AttachedFileItem> Children { get; private set; }

        private PackIconMaterialKind _icon;
        public PackIconMaterialKind Icon
        {
            get => _icon;
            set
            {
                _icon = value;
                OnPropertyUpdate(nameof(Icon));
            }
        }
        #endregion

        #region OnPropertyChnaged
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyUpdate(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
    #endregion

    #region ScriptEditCommands
    public static class ScriptEditCommands
    {
        public static readonly RoutedUICommand Save = new RoutedUICommand(nameof(Save), nameof(Save), typeof(ScriptEditCommands), new InputGestureCollection
        {
            new KeyGesture(Key.S, ModifierKeys.Control),
        });
        public static readonly RoutedUICommand UICtrlReload = new RoutedUICommand(nameof(UICtrlReload), nameof(UICtrlReload), typeof(ScriptEditCommands), new InputGestureCollection
        {
            new KeyGesture(Key.R, ModifierKeys.Control),
        });
        public static readonly RoutedUICommand UICtrlAdd = new RoutedUICommand(nameof(UICtrlAdd), nameof(UICtrlAdd), typeof(ScriptEditCommands));
        public static readonly RoutedUICommand UICtrlDelete = new RoutedUICommand(nameof(UICtrlDelete), nameof(UICtrlDelete), typeof(ScriptEditCommands));
    }
    #endregion
}
