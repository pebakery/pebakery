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
using MahApps.Metro.IconPacks;
using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using PEBakery.Core;
using PEBakery.Exceptions;
using PEBakery.Helper;
using PEBakery.IniLib;
using PEBakery.WPF.Controls;
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
        private UIRenderer _render;
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
                m.UIControlModified += ViewModel_UIControlModified;

                ReadScriptGeneral();
                ReadScriptInterface();
                ReadScriptAttachment();
            }
            catch (Exception e)
            { // Rollback Count to 0
                Interlocked.Decrement(ref Count);

                App.Logger.SystemWrite(new LogInfo(LogState.CriticalError, e));
                MessageBox.Show($"[Error Message]\r\n{e.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Window Event
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (m.ScriptHeaderNotSaved)
            {
                switch (MessageBox.Show("Script header was modified.\r\nSave changes?", "Save Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Exclamation))
                {
                    case MessageBoxResult.Yes:
                        if (!WriteScriptGeneral())
                        {
                            e.Cancel = true; // Error while saving, do not close ScriptEditWindow
                            return;
                        }
                        break;
                    case MessageBoxResult.No:
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
                        WriteScriptInterface();
                        break;
                    case MessageBoxResult.No:
                        break;
                    default:
                        throw new InvalidOperationException("Internal Logic Error at ScriptEditWindow.CloseButton_Click");
                }
            }

            // If script was updated, force MainWindow to refresh script
            DialogResult = m.ScriptHeaderUpdated || m.InterfaceUpdated;

            Tag = _sc;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            m.InterfaceCanvas.UIControlSelected -= InterfaceCanvas_UIControlSelected;
            m.UIControlModified -= ViewModel_UIControlModified;

            Interlocked.Decrement(ref Count);
        }

        private void MainSaveButton_Click(object sender, RoutedEventArgs e)
        {
            WriteScriptGeneral();
        }
        #endregion

        #region ReadScript
        private void ReadScriptGeneral()
        {
            // Nested Function
            string GetStringValue(string key, string defaultValue = "") => _sc.MainInfo.ContainsKey(key) ? _sc.MainInfo[key] : defaultValue;
            /*
            int GetIntValue(string key, int defaultValue = 0)
            {
                if (_sc.MainInfo.ContainsKey(key))
                    return NumberHelper.ParseInt32(key, out int intVal) ? intVal : defaultValue;
                else
                    return defaultValue;
            }
            */

            // General
            if (EncodedFile.ContainsLogo(_sc))
            {
                m.ScriptLogoImage = EncodedFile.ExtractLogoImage(_sc, ScriptLogo.ActualWidth);
                m.ScriptLogoInfo = EncodedFile.GetLogoInfo(_sc, true);
            }
            else
            {
                m.ScriptLogoImage = ScriptEditViewModel.ScriptLogoImageDefault;
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
            (List<UIControl> uiCtrls, List<LogInfo> errLogs) = UIRenderer.LoadInterfaces(_sc);
            _render = new UIRenderer(m.InterfaceCanvas, this, _sc, uiCtrls, 1, false);

            m.InterfaceUICtrls = new ObservableCollection<string>(uiCtrls.Select(x => x.Key));
            m.InterfaceUICtrlIndex = 0;

            m.InterfaceNotSaved = false;
            m.InterfaceUpdated = false;

            if (0 < errLogs.Count)
            {
                App.Logger.SystemWrite(errLogs);

                StringBuilder b = new StringBuilder();
                b.AppendLine("[Error Messages]");
                foreach (LogInfo log in errLogs)
                    b.AppendLine(log.Message);
                MessageBox.Show(b.ToString(), "Interface Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ReadScriptAttachment()
        {
            // Attachment
            m.AttachedFiles.Clear();

            Dictionary<string, List<EncodedFileInfo>> fileDict = EncodedFile.GetAllFilesInfo(_sc, ScriptEditViewModel.DeepInspectAttachedFile);
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
        #endregion

        #region WriteScript
        private bool WriteScriptGeneral()
        {
            if (_sc == null)
                return false;

            // Check m.ScriptVersion
            string verStr = StringEscaper.ProcessVersionString(m.ScriptVersion);
            if (verStr == null)
            {
                string errMsg = $"Invalid version string [{m.ScriptVersion}], please check again";
                App.Logger.SystemWrite(new LogInfo(LogState.Error, errMsg));
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

            Ini.WriteKeys(_sc.RealPath, keys);
            _sc = _sc.Project.RefreshScript(_sc);

            Application.Current?.Dispatcher.BeginInvoke((Action)(() =>
            {
                MainWindow w = Application.Current.MainWindow as MainWindow;
                w?.DrawScript(_sc);
            }));

            m.ScriptHeaderNotSaved = false;
            return true;
        }

        private void WriteScriptInterface()
        {
            if (_render == null)
                return;

            try
            {
                UIControl.Update(_render.UICtrls);

                Application.Current?.Dispatcher.BeginInvoke((Action)(() =>
                {
                    MainWindow w = Application.Current.MainWindow as MainWindow;
                    w?.DrawScript(_sc);
                }));
            }
            catch (Exception e)
            {
                MessageBox.Show($"Save of interface failed.\r\n\r\n[Message]\r\n{e.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            m.InterfaceNotSaved = false;
        }
        #endregion

        #region Event Handler - Logo
        private void ScriptLogoAttachButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "Supported Image (bmp, jpg, png, gif, ico, svg)|*.bmp;*.jpg;*.png;*.gif;*.ico;*.svg",
            };

            if (dialog.ShowDialog() == true)
            {
                string srcFile = dialog.FileName;
                try
                { 
                    string srcFileName = System.IO.Path.GetFileName(srcFile);
                    _sc = EncodedFile.AttachLogo(_sc, srcFileName, srcFile);
                    MessageBox.Show("Logo successfully attached.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                    ReadScriptGeneral();
                }
                catch (Exception ex)
                {
                    App.Logger.SystemWrite(new LogInfo(LogState.Error, ex));
                    MessageBox.Show($"Attach failed.\r\n\r\n[Message]\r\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ScriptLogoExtractButton_Click(object sender, RoutedEventArgs e)
        {
            if (EncodedFile.ContainsLogo(_sc))
            {
                using (MemoryStream ms = EncodedFile.ExtractLogo(_sc, out ImageHelper.ImageType type))
                {
                    SaveFileDialog dialog = new SaveFileDialog
                    {
                        InitialDirectory = App.BaseDir,
                        OverwritePrompt = true,
                        Filter = $"Image ({type.ToString().ToLower()})|*.{type}",
                        DefaultExt = $".{type}",
                        AddExtension = true,
                    };

                    if (dialog.ShowDialog() == true)
                    {
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
                            App.Logger.SystemWrite(new LogInfo(LogState.Error, ex));
                            MessageBox.Show($"Extraction failed.\r\n\r\n[Message]\r\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            else
            {
                MessageBox.Show($"Script [{_sc.Title}] does not have logo attached", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ScriptLogoDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (EncodedFile.ContainsLogo(_sc))
            {
                _sc = EncodedFile.DeleteLogo(_sc, out string errorMsg);
                if (errorMsg == null)
                {
                    MessageBox.Show("Logo successfully deleted.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    ReadScriptGeneral();
                }
                else
                {
                    App.Logger.SystemWrite(new LogInfo(LogState.Error, errorMsg));
                    MessageBox.Show($"Delete of logo had some issues.\r\n\r\n[Message]\r\n{errorMsg}", "Warning", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Logo does not exist.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Event Handler - Interface

        private void DrawScript()
        {
            if (m == null)
                return;

            double scaleFactor = m.InterfaceScaleFactor / 100;
            if (scaleFactor - 1 < double.Epsilon)
                scaleFactor = 1;
            _render.ScaleFactor = scaleFactor;
            m.InterfaceCanvas.LayoutTransform = new ScaleTransform(scaleFactor, scaleFactor);

            _render.Render();
            m.InterfaceLoaded = true;
        }

        private void ScaleFactorSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            DrawScript();
        }

        private void Interface_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            m.SelectedUICtrl = _render.UICtrls[m.InterfaceUICtrlIndex];
            m.InterfaceCanvas.ResetSelectedBorder();
            m.InterfaceCanvas.DrawSelectedBorder(m.SelectedUICtrl);
        }

        private void InterfaceCanvas_UIControlSelected(object sender, EditCanvas.UIControlSelectedEventArgs e)
        {
            if (e.UIControl == null)
                return;

            m.SelectedUICtrl = e.UIControl;

            int idx = _render.UICtrls.FindIndex(x => x.Key.Equals(e.UIControl.Key));
            Debug.Assert(idx != -1, "Internal Logic Error at ViewModel_UIControlSelected");
            m.InterfaceUICtrlIndex = idx;
        }

        private void ViewModel_UIControlModified(object sender, ScriptEditViewModel.UIControlModifiedEventArgs e)
        {
            int idx = _render.UICtrls.FindIndex(x => x.Key.Equals(e.UIControl.Key));
            Debug.Assert(idx != -1, "Internal Logic Error at ViewModel_UIControlModified");
            _render.UICtrls[idx] = e.UIControl;

            m.InterfaceCanvas.ResetSelectedBorder();
            _render.Render();
            m.InterfaceCanvas.DrawSelectedBorder(m.SelectedUICtrl);
        }

        private void InterfaceLoadButton_Click(object sender, RoutedEventArgs e)
        {
            DrawScript();
        }

        private void InterfaceSaveButton_Click(object sender, RoutedEventArgs e)
        {
            WriteScriptInterface();
        }

        private void UICtrlAddType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UIControlType type = UIControl.UIControlZeroBasedDict[m.UICtrlAddTypeIndex];
            int idx = 0;
            string key;
            bool duplicate;
            do
            {
                idx++;
                duplicate = false;

                key = $"{type}{idx:D2}";

                if (_render.UICtrls.Select(x => x.Key).Contains(key, StringComparer.OrdinalIgnoreCase))
                    duplicate = true;
            } while (duplicate);
            m.UICtrlAddName = key;
        }

        private void UICtrlAddButton_Click(object sender, RoutedEventArgs e)
        {
            // _render.UICtrls.Add
            // m.UICtrlAddTypeIndex;
            UIControlType type = UIControl.UIControlZeroBasedDict[m.UICtrlAddTypeIndex];
            if (string.IsNullOrWhiteSpace(m.UICtrlAddName))
            {
                MessageBox.Show("New UIControl name is empty", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            string key = m.UICtrlAddName.Trim();
            if (_render.UICtrls.Select(x => x.Key).Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                MessageBox.Show($"Interface key [{key}] is duplicated", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            SectionAddress addr = new SectionAddress(_sc, _sc.Sections[_ifaceSectionName]);
            string line = UIControl.GetUIControlTemplate(type, key);

            UIControl uiCtrl = UIParser.ParseStatement(line, addr, out List<LogInfo> errorLogs);
            Debug.Assert(uiCtrl != null, "Internal Logic Error at UICtrlAddButton_Click");
            Debug.Assert(errorLogs.Count == 0, "Internal Logic Error at UICtrlAddButton_Click");

            _render.UICtrls.Add(uiCtrl);
            m.InterfaceUICtrls = new ObservableCollection<string>(_render.UICtrls.Select(x => x.Key));
            m.InterfaceUICtrlIndex = 0;

            m.InterfaceCanvas.ResetSelectedBorder();
            _render.Render();
            m.SelectedUICtrl = uiCtrl;
            m.InterfaceCanvas.DrawSelectedBorder(uiCtrl);
        }
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
                App.Logger.SystemWrite(new LogInfo(LogState.Error, ex));
                MessageBox.Show($"Unable to add folder.\r\n\r\n[Message]\r\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

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

                List<EncodedFileInfo> fileInfos = EncodedFile.GetFolderInfo(_sc, item.Name, false);

                StringBuilder b = new StringBuilder();
                bool fileOverwrited = false;
                for (int i = 0; i < fileInfos.Count; i++)
                {
                    EncodedFileInfo info = fileInfos[i];

                    string destFile = System.IO.Path.Combine(destDir, info.FileName);
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
                        throw new InternalException("Internal Logic Error at ScriptEditWindow.ExtractFileButton_Click");
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
                        string destFile = System.IO.Path.Combine(destDir, info.FileName);
                        using (FileStream fs = new FileStream(destFile, FileMode.Create, FileAccess.Write))
                        {
                            EncodedFile.ExtractFile(_sc, info.DirName, info.FileName, fs);
                        }

                        MessageBox.Show("File successfully extracted.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        App.Logger.SystemWrite(new LogInfo(LogState.Error, ex));
                        MessageBox.Show($"Extraction failed.\r\n\r\n[Message]\r\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

            _sc = EncodedFile.DeleteFolder(_sc, item.Name, out string errMsg);
            if (errMsg == null)
            {
                ReadScriptAttachment();

                m.AttachSelected = null;
                m.UpdateAttachFileDetail();
            }
            else // Failure
            {
                App.Logger.SystemWrite(new LogInfo(LogState.Error, errMsg));
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

                ReadScriptAttachment();
                m.AttachSelected = null;
                m.UpdateAttachFileDetail();
            }
            catch (Exception ex)
            {
                App.Logger.SystemWrite(new LogInfo(LogState.Error, ex));
                MessageBox.Show($"Attach failed.\r\n\r\n[Message]\r\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

            string ext = System.IO.Path.GetExtension(info.FileName);
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
                        EncodedFile.ExtractFile(_sc, info.DirName, info.FileName, fs);
                    }

                    MessageBox.Show("File successfully extracted.", "Extract Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    App.Logger.SystemWrite(new LogInfo(LogState.Error, ex));
                    MessageBox.Show("Extraction failed.\r\nSee system log for details.", "Extract Failure", MessageBoxButton.OK, MessageBoxImage.Error);
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

            _sc = EncodedFile.DeleteFile(_sc, info.DirName, info.FileName, out string errMsg);
            if (errMsg == null)
            {
                ReadScriptAttachment();

                m.AttachSelected = null;
                m.UpdateAttachFileDetail();
            }
            else // Failure
            {
                App.Logger.SystemWrite(new LogInfo(LogState.Error, errMsg));
                MessageBox.Show("Delete failed.\r\nSee system log for details.", "Delete Failure", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion
        #endregion

        #region ShortCut Command Handler
        private void ScriptMain_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Command is RoutedCommand rCmd)
            {
                if (rCmd.Name.Equals("Save", StringComparison.Ordinal))
                {
                    // Changing focus is required to make sure changes in UI updated to ViewModel
                    MainSaveButton.Focus();

                    WriteScriptGeneral();
                }
            }
        }

        private void ScriptMain_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            // Only in Tab [General]
            e.CanExecute = m.TabIndex == 0;
        }

        #endregion
    }
    #endregion

    #region ScriptEditViewModel
    public class ScriptEditViewModel : INotifyPropertyChanged
    {
        #region Constructor
        public ScriptEditViewModel()
        {
            ScriptLogoImageDefault.Foreground = new SolidColorBrush(Color.FromArgb(96, 0, 0, 0));

            EditCanvas canvas = new EditCanvas
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
            public UIControlModifiedEventArgs(UIControl uiCtrl)
            {
                UIControl = uiCtrl;
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
        public static readonly PackIconMaterial ScriptLogoImageDefault = ImageHelper.GetMaterialIcon(PackIconMaterialKind.BorderNone);
        private FrameworkElement _scriptLogoImage = ScriptLogoImageDefault;
        public FrameworkElement ScriptLogoImage
        {
            get => _scriptLogoImage;
            set
            {
                _scriptLogoImage = value;
                OnPropertyUpdate(nameof(ScriptLogoImage));
            }
        }

        private EncodedFileInfo _scriptLogoInfo;
        public EncodedFileInfo ScriptLogoInfo
        {
            get => _scriptLogoInfo;
            set
            {
                _scriptLogoInfo = value;
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

                string str = NumberHelper.ByteSizeToHumanReadableString(ScriptLogoInfo.RawSize, 1);
                return $"{str} ({ScriptLogoInfo.RawSize})";
            }
        }

        public string ScriptLogoEncodedSize
        {
            get
            {
                if (ScriptLogoInfo == null)
                    return string.Empty; // Invalid value

                string str = NumberHelper.ByteSizeToHumanReadableString(ScriptLogoInfo.EncodedSize, 1);
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

        #region Property - Interface
        public bool InterfaceNotSaved { get; set; } = false;
        public bool InterfaceUpdated { get; set; } = false;

        // Canvas
        private EditCanvas _interfaceCanvas;
        public EditCanvas InterfaceCanvas
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
        private int _uiCtrlAddTypeIndex;
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
        private int _interfaceUICtrlIndex = 0;
        public int InterfaceUICtrlIndex
        {
            get => _interfaceUICtrlIndex;
            set
            {
                _interfaceUICtrlIndex = value;
                OnPropertyUpdate(nameof(InterfaceUICtrlIndex));
            }
        }

        private UIControl _selectedUICtrl;
        public UIControl SelectedUICtrl
        {
            get => _selectedUICtrl;
            set
            {
                _selectedUICtrl = value;
                OnPropertyUpdate(nameof(UICtrlEditEnabled));
                OnPropertyUpdate(nameof(UICtrlText));
                OnPropertyUpdate(nameof(UICtrlVisible));
                OnPropertyUpdate(nameof(UICtrlX));
                OnPropertyUpdate(nameof(UICtrlY));
                OnPropertyUpdate(nameof(UICtrlWidth));
                OnPropertyUpdate(nameof(UICtrlHeight));
                OnPropertyUpdate(nameof(UICtrlToolTip));
            }
        }
        public bool UICtrlEditEnabled => _selectedUICtrl != null;
        public string UICtrlText
        {
            get => _selectedUICtrl != null ? _selectedUICtrl.Text : string.Empty;
            set
            {
                if (_selectedUICtrl == null)
                    return;

                _selectedUICtrl.Text = value;
                InterfaceNotSaved = true;
                InterfaceUpdated = true;
                UIControlModified?.Invoke(this, new UIControlModifiedEventArgs(_selectedUICtrl));
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
                InterfaceNotSaved = true;
                InterfaceUpdated = true;
                UIControlModified?.Invoke(this, new UIControlModifiedEventArgs(_selectedUICtrl));
            }
        }
        public int UICtrlX
        {
            get => _selectedUICtrl != null ? (int) _selectedUICtrl.Rect.X : 0;
            set
            {
                if (_selectedUICtrl == null)
                    return;

                _selectedUICtrl.Rect.X = value;
                InterfaceNotSaved = true;
                InterfaceUpdated = true;
                UIControlModified?.Invoke(this, new UIControlModifiedEventArgs(_selectedUICtrl));
            }
        }
        public int UICtrlY
        {
            get => _selectedUICtrl != null ? (int)_selectedUICtrl.Rect.Y : 0;
            set
            {
                if (_selectedUICtrl == null)
                    return;

                _selectedUICtrl.Rect.Y = value;
                InterfaceNotSaved = true;
                InterfaceUpdated = true;
                UIControlModified?.Invoke(this, new UIControlModifiedEventArgs(_selectedUICtrl));
            }
        }
        public int UICtrlWidth
        {
            get => _selectedUICtrl != null ? (int)_selectedUICtrl.Rect.Width : 0;
            set
            {
                if (_selectedUICtrl == null)
                    return;

                _selectedUICtrl.Rect.Width = value;
                InterfaceNotSaved = true;
                InterfaceUpdated = true;
                UIControlModified?.Invoke(this, new UIControlModifiedEventArgs(_selectedUICtrl));
            }
        }
        public int UICtrlHeight
        {
            get => _selectedUICtrl != null ? (int)_selectedUICtrl.Rect.Height : 0;
            set
            {
                if (_selectedUICtrl == null)
                    return;

                _selectedUICtrl.Rect.Height = value;
                InterfaceNotSaved = true;
                InterfaceUpdated = true;
                UIControlModified?.Invoke(this, new UIControlModifiedEventArgs(_selectedUICtrl));
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
                InterfaceNotSaved = true;
                InterfaceUpdated = true;
                UIControlModified?.Invoke(this, new UIControlModifiedEventArgs(_selectedUICtrl));
            }
        }
        #endregion

        #region Property - Attachment
        public static bool DeepInspectAttachedFile = false;

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

                string str = NumberHelper.ByteSizeToHumanReadableString(AttachSelected.Detail.RawSize, 1);
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

                string str = NumberHelper.ByteSizeToHumanReadableString(AttachSelected.Detail.EncodedSize, 1);
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

        #region OnPropertyChnaged
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
            Icon = ImageHelper.GetMaterialIcon(isFolder ? PackIconMaterialKind.Folder : PackIconMaterialKind.File, 0);

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

        private Control _icon;
        public Control Icon
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
}
