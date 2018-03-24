using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
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

namespace PEBakery.WPF
{
    #region ScriptEditWindow
    public partial class ScriptEditWindow : Window
    {
        #region Field
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

                PopulateInformation();
            }
            catch
            { // Rollback Count to 0
                Interlocked.Decrement(ref Count);
                throw;
            }
            
            /*
            Application.Current.Dispatcher.Invoke(() =>
            {
                MainWindow w = Application.Current.MainWindow as MainWindow;
                Debug.Assert(w != null, "MainWindow != null");

                List<Project> projList = w.Projects.Projects;
                for (int i = 0; i < projList.Count; i++)
                {
                    Project proj = projList[i];

                    m.CodeBox_Projects.Add(new Tuple<string, Project>(proj.ProjectName, proj));

                    if (proj.ProjectName.Equals(w.CurMainTree.Script.Project.ProjectName, StringComparison.Ordinal))
                        m.CodeBox_SelectedProjectIndex = i;
                }
            });
            */
        }
        #endregion

        #region PopulateInformation
        private void PopulateInformation()
        {
            // General
            if (EncodedFile.LogoExists(_sc))
                m.ScriptLogo = EncodedFile.ExtractLogoImage(_sc, ScriptLogo.ActualWidth);
            else
                m.ScriptLogo = ScriptEditViewModel.ScriptLogoDefault;
            m.ScriptTitle = _sc.MainInfo["Title"];
        }
        #endregion

        #region Window Event
        private void Window_Closed(object sender, EventArgs e)
        {
            Interlocked.Decrement(ref Count);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {

            Close();
        }
        #endregion

        #region Button Event - Attachment
        private void NewFolderButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void RemoveFolderButton_Click(object sender, RoutedEventArgs e)
        {

        }
        #endregion

        #region Button Event - General
        private void ScriptLogoAttachButton_Click(object sender, RoutedEventArgs e)
        {
            /*
            if (EncodedFile.LogoExists(_sc))
            {
                using (MemoryStream ms = EncodedFile.ExtractLogo(_sc, out ImageHelper.ImageType type))
                {
                    SaveFileDialog dialog = new SaveFileDialog
                    {
                        InitialDirectory = App.BaseDir,
                        OverwritePrompt = true,
                        DefaultExt = $".{type}",
                        AddExtension = true,
                    };

                    if (dialog.ShowDialog() == true)
                    {
                        string destPath = dialog.FileName;
                        using (FileStream fs = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            ms.CopyTo(fs);
                        }

                        MessageBox.Show($"Logo of script extracted to [{destPath}]", "Extract Success", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show($"Script [{_sc.Title}] does not have logo attached", "Extract Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
                */
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
                        Filter = $"Image (.{type.ToString().ToLower()})|*.{type}",
                        DefaultExt = $".{type}",
                        AddExtension = true,
                    };

                    if (dialog.ShowDialog() == true)
                    {
                        string destPath = dialog.FileName;
                        try
                        {
                            using (FileStream fs = new FileStream(destPath, FileMode.Create, FileAccess.Write,
                                FileShare.None))
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

        }
        #endregion
    }
    #endregion

    #region ScriptEditViewModel
    public class ScriptEditViewModel : INotifyPropertyChanged
    {
        #region Field and Property - General
        public static readonly PackIconMaterial ScriptLogoDefault = ImageHelper.GetMaterialIcon(PackIconMaterialKind.BorderNone);
        private FrameworkElement scriptLogo = ScriptLogoDefault;
        public FrameworkElement ScriptLogo
        {
            get => scriptLogo;
            set
            {
                scriptLogo = value;
                OnPropertyUpdate(nameof(ScriptLogo));
            }
        }

        private string scriptTitle = string.Empty;
        public string ScriptTitle
        {
            get => scriptTitle;
            set
            {
                scriptTitle = value;
                OnPropertyUpdate(nameof(ScriptTitle));
            }
        }
        #endregion

        #region Utility
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyUpdate(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
    #endregion
}
