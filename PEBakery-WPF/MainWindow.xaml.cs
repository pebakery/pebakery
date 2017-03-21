using System;
using System.IO;
using System.Reflection;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Collections.ObjectModel;
using PEBakery.Helper;
using PEBakery.Lib;
using PEBakery.Object;
using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace PEBakery.WPF
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<Project> projects;
        private int loadedProjectCount;
        private int allProjectCount;
        private string baseDir;
        private ProgressBar loadProgressBar;
        private TextBlock statusBar;
        private Stopwatch stopwatch;
        private BackgroundWorker loadWorker = new BackgroundWorker();

        private TreeViewModel treeModel;
        public TreeViewModel TreeModel { get => treeModel; }

        public MainWindow()
        {
            InitializeComponent();

            string[] args = App.Args;

            string argBaseDir = new DirectoryInfo(FileHelper.GetProgramAbsolutePath()).Parent.FullName;
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "/basedir", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length)
                        argBaseDir = System.IO.Path.GetFullPath(args[i + 1]);
                    else
                        Console.WriteLine("\'/basedir\' must be used with path\r\n");
                }
                else if (string.Equals(args[i], "/?", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(args[i], "/help", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(args[i], "/h", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Sorry, help message not implemented\r\n");
                }
            }

            this.statusBar = new TextBlock();
            loadProgressBar = new ProgressBar()
            {
                IsIndeterminate = false,
                Minimum = 0,
                Maximum = 100,
                Value = 0
            };
            this.bottomDock.Child = loadProgressBar;

            this.projects = new List<Project>();
            this.loadedProjectCount = 0;
            this.allProjectCount = 0;

            this.baseDir = argBaseDir;
            this.treeModel = new TreeViewModel();
            this.DataContext = treeModel;

            LoadButtonsImage();

            loadWorker.WorkerReportsProgress = true;
            loadWorker.DoWork += new DoWorkEventHandler(bgWorker_LoadProject);
            loadWorker.ProgressChanged += new ProgressChangedEventHandler(loadWorker_ProgressChanged);
            loadWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(loadWorker_RunWorkerCompleted);

            loadWorker.RunWorkerAsync(baseDir);
        }

        void LoadButtonsImage()
        {
            double width = 150;
            double height = 150;
            buildButton.Background = ImageHelper.SvgByteToImageBrush(Properties.Resources.SvgBuild, width, height);
            refreshButton.Background = ImageHelper.SvgByteToImageBrush(Properties.Resources.SvgRefresh, width, height);
            settingButton.Background = ImageHelper.SvgByteToImageBrush(Properties.Resources.SvgSetting, width, height);
            updateButton.Background = ImageHelper.SvgByteToImageBrush(Properties.Resources.SvgUpdate, width, height);
        }

        void bgWorker_LoadProject(object sender, DoWorkEventArgs e)
        {
            string baseDir = (string) e.Argument;
            BackgroundWorker worker = sender as BackgroundWorker;

            stopwatch = Stopwatch.StartNew();
            this.projects = new List<Project>();
            this.loadedProjectCount = 0;
            this.allProjectCount = 0;

            string[] projArray = Directory.GetDirectories(System.IO.Path.Combine(baseDir, "Projects"));
            List<string> projList = new List<string>();
            foreach (string dir in projArray)
            {
                if (File.Exists(System.IO.Path.Combine(baseDir, "Projects", dir, "script.project")))
                    projList.Add(dir);
            }

            allProjectCount = projList.Count;
            foreach (string dir in projList)
            {
                Project project = new Project(baseDir, System.IO.Path.GetFileName(dir), worker);
                project.Load();
                projects.Add(project);
                loadedProjectCount++;
            }
        }

        private void loadWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            this.loadProgressBar.Value = (e.ProgressPercentage / allProjectCount) + (loadedProjectCount * 100 / allProjectCount);
        }

        private void loadWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            stopwatch.Stop();
            TimeSpan t = stopwatch.Elapsed;
            this.statusBar.Text = $"{allProjectCount} projects loaded, took {t:hh\\:mm\\:ss}";
            this.bottomDock.Child = statusBar;

            foreach (Project project in this.projects)
            {
                List<Node<Plugin>> plugins = project.VisiblePlugins.Root;
                RecursivePopulateMainTreeView(plugins, this.treeModel.Child);
            };
            mainTreeView.DataContext = treeModel;
        }

        private void RecursivePopulateMainTreeView(List<Node<Plugin>> plugins, ObservableCollection<TreeViewModel> treeParent)
        {
            double size = mainTreeView.FontSize;
            DpiScale scale = VisualTreeHelper.GetDpi(this as Visual);

            foreach (Node<Plugin> node in plugins)
            {
                Plugin p = node.Data;

                TreeViewModel item = new TreeViewModel();
                treeParent.Add(item);
                item.Node = node;
                
                if (p.Type == PluginType.Directory)
                {
                    item.SetSvgImage(Properties.Resources.SvgFolder, size, size, scale);
                }
                else if (p.Type == PluginType.Plugin)
                {
                    if (p.Level == Project.MainLevel)
                        item.SetSvgImage(Properties.Resources.SvgProject, size, size, scale);
                    else if (p.Mandatory)
                        item.SetSvgImage(Properties.Resources.SvgLock, size, size, scale);
                    else
                        item.SetSvgImage(Properties.Resources.SvgFile, size, size, scale);
                }

                if (0 < node.Child.Count)
                    RecursivePopulateMainTreeView(node.Child, item.Child);
            }
        }

        private void mainTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var tree = sender as TreeView;

            if (tree.SelectedItem is TreeViewModel)
            {
                TreeViewModel item = tree.SelectedItem as TreeViewModel;
                Plugin p = item.Node.Data;
                this.mainContainer.Text = $"Selected: {p.Title}, Level = {p.Level}";
            }
            else
            {
                Debug.Assert(false);
            }
        }

        private void refreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (loadWorker.IsBusy == false)
            {
                (mainTreeView.DataContext as TreeViewModel).Child.Clear();
                loadProgressBar.Value = 0;
                this.bottomDock.Child = loadProgressBar;

                loadWorker = new BackgroundWorker();
                loadWorker.WorkerReportsProgress = true;
                loadWorker.DoWork += new DoWorkEventHandler(bgWorker_LoadProject);
                loadWorker.ProgressChanged += new ProgressChangedEventHandler(loadWorker_ProgressChanged);
                loadWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(loadWorker_RunWorkerCompleted);
                loadWorker.RunWorkerAsync(baseDir);
            }
        }

        private void JumpyStackPanel_KeyDown(object sender, KeyEventArgs e)
        {
            StackPanel panel = sender as StackPanel;

            base.OnPreviewKeyDown(e);
            if (e.Key == Key.Space)
            {
                CheckBox checkBox = panel.Children[0] as CheckBox;
                if (checkBox.IsChecked == true)
                    checkBox.IsChecked = false;
                else if (checkBox.IsChecked == false)
                    checkBox.IsChecked = true;
            }
        }

        private void mainTreeView_Loaded(object sender, RoutedEventArgs e)
        {
            Window window = Window.GetWindow(this);
            window.KeyDown += mainTreeView_KeyDown;
        }

        /// <summary>
        /// Used to ensure pressing 'Space' to toggle TreeView's checkbox.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void mainTreeView_KeyDown(object sender, KeyEventArgs e)
        {
            // Window window = sender as Window;
            base.OnKeyDown(e);

            if (e.Key == Key.Space)
            {
                if (Keyboard.FocusedElement is FrameworkElement focusedElement)
                {
                    if (focusedElement.DataContext is TreeViewModel node)
                    {
                        if (node.Checked == true)
                            node.Checked = false;
                        else if (node.Checked == false)
                            node.Checked = true;
                        e.Handled = true;
                    }
                }
            }
        }

        private void TreeNodeImage_Initialized(object sender, EventArgs e)
        {
            FrameworkElement element = sender as FrameworkElement;
            if (element != null)
            {
                TreeViewModel model = element.DataContext as TreeViewModel;
                DpiChanged += model.OnDpiChanged;
            }
        }
    }

    public class TreeViewModel : INotifyPropertyChanged
    {
        public void OnDpiChanged(object sender, DpiChangedEventArgs e)
        {
            // TODO: Not working! 
            DpiScale scale = e.NewDpi;
            ImageSvg = ImageHelper.SvgByteToBitmapImage(imageSrc, imageWidth * scale.DpiScaleX, imageHeight * scale.DpiScaleY);
        }

        public bool Checked
        {
            get
            {
                switch (node.Data.Selected)
                {
                    case SelectedState.True:
                        return true;
                    default:
                        return false;
                }
            }
            set
            {
                if (node.Data.Mandatory == false && node.Data.Selected != SelectedState.None)
                {
                    if (value)
                        node.Data.Selected = SelectedState.True;
                    else
                        node.Data.Selected = SelectedState.False;
                    OnPropertyUpdate("Checked");
                }
            }
        }

        public Visibility CheckBoxVisible
        {
            get
            {
                if (node.Data.Selected == SelectedState.None)
                    return Visibility.Hidden;
                else
                    return Visibility.Visible;
            }
        }

        public string Text { get => node.Data.Title; }

        private Node<Plugin> node;
        public Node<Plugin> Node
        {
            get => node;
            set
            {
                node = value;
                OnPropertyUpdate("Node");
            }
        }

        private double imageWidth;
        public double ImageWidth { get => imageWidth; }
        private double imageHeight;
        public double ImageHeight { get => imageHeight; }
        private byte[] imageSrc;
        public byte[] ImageSrc { get => imageSrc; }
        private BitmapImage imageSvg;
        public BitmapImage ImageSvg
        {
            get => imageSvg;
            set
            {
                imageSvg = value;
                OnPropertyUpdate("Image");
            }
        }

        private ObservableCollection<TreeViewModel> child = new ObservableCollection<TreeViewModel>();
        public ObservableCollection<TreeViewModel> Child { get => child; }
        
        public void SetSvgImage(byte[] src, double width, double height, DpiScale scale)
        {
            imageWidth = width;
            imageHeight = height;
            imageSrc = src;
            ImageSvg = ImageHelper.SvgByteToBitmapImage(src, width * scale.DpiScaleX, height * scale.DpiScaleY);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyUpdate(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
