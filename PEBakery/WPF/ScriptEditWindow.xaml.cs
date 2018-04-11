using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using MahApps.Metro.IconPacks;
using Microsoft.Win32;
using PEBakery.Core;
using PEBakery.Helper;
using PEBakery.IniLib;
using PEBakery.WPF.Controls;

namespace PEBakery.WPF
{
    #region ScriptEditWindow
    public partial class ScriptEditWindow : Window
    {
        #region Field and Property
        public static int Count = 0;

        private Script _sc;
        private readonly ScriptEditViewModel m;
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

                ReadScript();
            }
            catch
            { // Rollback Count to 0
                Interlocked.Decrement(ref Count);
                throw;
            }
        }
        #endregion

        #region ReadScript
        private void ReadScript()
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
            if (EncodedFile.LogoExists(_sc))
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
        }
        #endregion

        #region WriteScript
        private void WriteScript()
        {
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

            m.ScriptUpdated = false;
        }
        #endregion

        #region Window Event
        private void Window_Closed(object sender, EventArgs e)
        {
            Interlocked.Decrement(ref Count);
        }

        private void MainSaveButton_Click(object sender, RoutedEventArgs e)
        {
            WriteScript();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (m.ScriptUpdated)
            {
                // Make MainWindow to refresh script
                DialogResult = true;
                
            }
            else
            {
                DialogResult = false;
            }
                
            Tag = _sc;

            Close();
        }
        #endregion

        #region Button Event - General
        private void ScriptLogoAttachButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                InitialDirectory = App.BaseDir,
                Filter = "Supported Image (bmp, jpg, png, gif, ico, svg)|*.bmp;*.jpg;*.png;*.gif;*.ico;*.svg",
            };

            if (dialog.ShowDialog() == true)
            {
                string srcFile = dialog.FileName;
                try
                { 
                    string srcFileName = System.IO.Path.GetFileName(srcFile);
                    _sc = EncodedFile.AttachLogo(_sc, "AuthorEncoded", srcFileName, srcFile);
                    MessageBox.Show("Logo successfully attached.", "Attach Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    ReadScript();
                }
                catch (Exception ex)
                {
                    App.Logger.System_Write(new LogInfo(LogState.Error, ex));
                    MessageBox.Show("Attach failed.\r\nSee system log for details.", "Attach Failure", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ScriptLogoExtractButton_Click(object sender, RoutedEventArgs e)
        {
            if (EncodedFile.LogoExists(_sc))
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

                            MessageBox.Show("Logo successfully extracted.", "Extract Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        catch (Exception ex)
                        {
                            App.Logger.System_Write(new LogInfo(LogState.Error, ex));
                            MessageBox.Show("Extraction failed.\r\nSee system log for details.", "Extract Failure", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            else
            {
                MessageBox.Show($"Script [{_sc.Title}] does not have logo attached", "Extract Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ScriptLogoDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (EncodedFile.LogoExists(_sc))
            {
                _sc = EncodedFile.DeleteLogo(_sc, out string errorMsg);
                if (errorMsg == null)
                {
                    MessageBox.Show("Logo successfully deleted.", "Delete Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    ReadScript();
                }
                else
                {
                    App.Logger.System_Write(new LogInfo(LogState.Error, errorMsg));
                    MessageBox.Show("Delete of logo had some issues.\r\nSee system log for details.", "Delete Warning", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Delete of logo had some issues.\r\nSee system log for details.", "Delete Warning", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ScriptLevelNumberBox_ValueChanged(object sender, RoutedPropertyChangedEventArgs<decimal> e)
        {
            m.ScriptLevel = e.NewValue;
        }
        #endregion

        #region Event Handler - Attachment
        private void NewFolderButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void RemoveFolderButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ScriptAttachTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            
        }
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

                    WriteScript();
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
            ScriptLogoImageDefault.Foreground = new SolidColorBrush(Color.FromRgb(192, 192, 192));
        }
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

        #region Property - General - Script Logo
        public static readonly PackIconMaterial ScriptLogoImageDefault = ImageHelper.GetMaterialIcon(PackIconMaterialKind.BorderNone);
        private FrameworkElement scriptLogoImage = ScriptLogoImageDefault;
        public FrameworkElement ScriptLogoImage
        {
            get => scriptLogoImage;
            set
            {
                scriptLogoImage = value;
                OnPropertyUpdate(nameof(ScriptLogoImage));
            }
        }

        private EncodedFile.EncodedFileInfo scriptLogoInfo;
        public EncodedFile.EncodedFileInfo ScriptLogoInfo
        {
            get => scriptLogoInfo;
            set
            {
                scriptLogoInfo = value;
                OnPropertyUpdate(nameof(ScriptLogoInfoStr));
            }
        }

        public string ScriptLogoInfoStr
        {
            get
            {
                if (ScriptLogoInfo == null)
                    return "Logo not found";

                StringBuilder b = new StringBuilder();
                b.AppendLine($"File : {ScriptLogoInfo.FileName}");
                b.AppendLine($"Raw Size : {NumberHelper.ByteSizeToHumanReadableString(ScriptLogoInfo.RawSize, 1)}");
                b.AppendLine($"Compressed Size : {NumberHelper.ByteSizeToHumanReadableString(ScriptLogoInfo.CompressedSize, 1)}");
                b.Append($"Compression : {ScriptLogoInfo.EncodeMode}");
                return b.ToString();
            }
        }
        #endregion

        #region Property - General - Script Main
        public bool ScriptUpdated { get; set; } = false;

        private string scriptTitle = string.Empty;
        public string ScriptTitle
        {
            get => scriptTitle;
            set
            {
                scriptTitle = value;
                ScriptUpdated = true;
                OnPropertyUpdate(nameof(ScriptTitle));
            }
        }

        private string scriptAuthor = string.Empty;
        public string ScriptAuthor
        {
            get => scriptAuthor;
            set
            {
                scriptAuthor = value;
                OnPropertyUpdate(nameof(ScriptAuthor));
            }
        }

        private string scriptVersion = "0";
        public string ScriptVersion
        {
            get => scriptVersion;
            set
            {
                scriptVersion = value;
                ScriptUpdated = true;
                OnPropertyUpdate(nameof(ScriptVersion));
            }
        }

        private string scriptDate = string.Empty;
        public string ScriptDate
        {
            get => scriptDate;
            set
            {
                scriptDate = value;
                ScriptUpdated = true;
                OnPropertyUpdate(nameof(ScriptDate));
            }
        }

        private decimal scriptLevel = 0;
        public decimal ScriptLevel
        {
            get => scriptLevel;
            set
            {
                scriptLevel = value;
                ScriptUpdated = true;
                OnPropertyUpdate(nameof(ScriptLevel));
            }
        }

        private string scriptDescription = string.Empty;
        public string ScriptDescription
        {
            get => scriptDescription;
            set
            {
                scriptDescription = value;
                ScriptUpdated = true;
                OnPropertyUpdate(nameof(ScriptDescription));
            }
        }

        private SelectedState scriptSelectedState = SelectedState.False;
        public SelectedState ScriptSelectedState
        {
            get => scriptSelectedState;
            set
            {
                scriptSelectedState = value;
                ScriptUpdated = true;
                OnPropertyUpdate(nameof(ScriptSelected));
            }
        }

        [SuppressMessage("ReSharper", "RedundantCaseLabel")]
        public bool? ScriptSelected
        {
            get
            {
                switch (scriptSelectedState)
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
                        scriptSelectedState = SelectedState.True;
                        break;
                    default:
                    case false:
                        scriptSelectedState = SelectedState.False;
                        break;
                    case null:
                        scriptSelectedState = SelectedState.None;
                        break;                
                }
                ScriptUpdated = true;
            }
        }

        private bool scriptMandatory = false;
        public bool ScriptMandatory
        {
            get => scriptMandatory;
            set
            {
                scriptMandatory = value;
                ScriptUpdated = true;
                OnPropertyUpdate(nameof(ScriptMandatory));
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

    #region ScriptAttachTreeViewModel

    public class ScriptAttachTreeViewModel : INotifyPropertyChanged
    {
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
