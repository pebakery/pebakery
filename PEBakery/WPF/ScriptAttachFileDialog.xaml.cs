using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using PEBakery.Core;

namespace PEBakery.WPF
{
    /// <summary>
    /// ScriptAttachFileDialog.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ScriptAttachFileDialog : Window
    {
        #region Properties
        public string FilePath
        {
            get => FilePathTextBox.Text;
            set => FilePathTextBox.Text = value;
        }

        public string FileName
        {
            get => FileNameTextBox.Text;
            set => FileNameTextBox.Text = value;
        }

        public EncodedFile.EncodeMode EncodeMode
        {
            get
            {
                int idx = CompressionComboBox.SelectedIndex;
                switch (idx)
                {
                    case 0:
                        return EncodedFile.EncodeMode.Raw;
                    case 1:
                        return EncodedFile.EncodeMode.ZLib;
                    case 2:
                        return EncodedFile.EncodeMode.XZ;
                    default:
                        string msg = $"Invalid CompressionComboBox index [{idx}]";
                        MessageBox.Show(this, msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        throw new InvalidOperationException(msg);
                }
            }
            set
            {
                switch (value)
                {
                    case EncodedFile.EncodeMode.Raw:
                        CompressionComboBox.SelectedIndex = 0;
                        break;
                    case EncodedFile.EncodeMode.ZLib:
                        CompressionComboBox.SelectedIndex = 1;
                        break;
                    case EncodedFile.EncodeMode.XZ:
                        CompressionComboBox.SelectedIndex = 2;
                        break;
                    default:
                        string msg = $"Invalid EncodeMode [{value}]";
                        MessageBox.Show(this, msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        throw new InvalidOperationException(msg);
                }
            }
        }
        #endregion

        #region Constructor
        public ScriptAttachFileDialog()
        {
            InitializeComponent();

            EncodeMode = EncodedFile.EncodeMode.ZLib;
        }
        #endregion

        #region Event Handlers
        private void FilePathSelectButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "All Files|*.*",
            };

            if (dialog.ShowDialog() != true)
                return;

            FilePath = dialog.FileName;
            FileName = Path.GetFileName(dialog.FileName);
        }

        private void AttachButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
        #endregion
    }
}
