using System;
using System.IO;
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
using PEBakery.Core;

// Used OpenSource
// Svg.Net (Microsoft Public License)
// Google's Material Icons (Apache License)
// Microsoft's Per-Monitor-DPI Images Example (MIT)
// Main Icon from pixelkit.com, CC BY-NC 3.0 (https://www.iconfinder.com/icons/208267/desert_donut_icon)

namespace PEBakery.WPF
{
    #region MainWindow
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

        const int maxDpiScale = 4;

        private TreeViewModel treeModel;
        public TreeViewModel TreeModel { get => treeModel; }

        public MainWindow()
        {
            InitializeComponent();

            string[] args = App.Args;

            string argBaseDir = FileHelper.GetProgramAbsolutePath();
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
            this.treeModel = new TreeViewModel(null);
            this.DataContext = treeModel;

            LoadButtonsImage();

            StartLoadWorkder();
        }

        void LoadButtonsImage()
        {
            double width = 300;
            double height = 300;
            buildButton.Background = ImageHelper.SvgByteToImageBrush(Properties.Resources.SvgBuild, width, height);
            refreshButton.Background = ImageHelper.SvgByteToImageBrush(Properties.Resources.SvgRefresh, width, height);
            settingButton.Background = ImageHelper.SvgByteToImageBrush(Properties.Resources.SvgSetting, width, height);
            updateButton.Background = ImageHelper.SvgByteToImageBrush(Properties.Resources.SvgUpdate, width, height);

            width = 120;
            height = 120;
            pluginRunButton.Background = ImageHelper.SvgByteToImageBrush(Properties.Resources.SvgBuild, width, height);
            pluginEditButton.Background = ImageHelper.SvgByteToImageBrush(Properties.Resources.SvgEdit, width, height);
        }

        private void StartLoadWorkder()
        {
            loadWorker = new BackgroundWorker();
            loadWorker.WorkerReportsProgress = true;
            loadWorker.DoWork += new DoWorkEventHandler(LoadWorker_Work);
            loadWorker.ProgressChanged += new ProgressChangedEventHandler(LoadWorker_ProgressChanged);
            loadWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(LoadWorker_RunWorkerCompleted);
            loadWorker.RunWorkerAsync(baseDir);
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (loadWorker.IsBusy == false)
            {
                (mainTreeView.DataContext as TreeViewModel).Child.Clear();
                loadProgressBar.Value = 0;
                this.bottomDock.Child = loadProgressBar;

                StartLoadWorkder();
            }
        }

        void LoadWorker_Work(object sender, DoWorkEventArgs e)
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

        private void LoadWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            this.loadProgressBar.Value = (e.ProgressPercentage / allProjectCount) + (loadedProjectCount * 100 / allProjectCount);
        }

        private void LoadWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            stopwatch.Stop();
            TimeSpan t = stopwatch.Elapsed;
            this.statusBar.Text = $"{allProjectCount} projects loaded, took {t:hh\\:mm\\:ss}";
            this.bottomDock.Child = statusBar;

            foreach (Project project in this.projects)
            {
                List<Node<Plugin>> plugins = project.VisiblePlugins.Root;
                RecursivePopulateMainTreeView(plugins, this.treeModel);
            };
            mainTreeView.DataContext = treeModel;
        }

        private void RecursivePopulateMainTreeView(List<Node<Plugin>> plugins, TreeViewModel treeParent)
        {
            double size = mainTreeView.FontSize * maxDpiScale;

            foreach (Node<Plugin> node in plugins)
            {
                Plugin p = node.Data;

                TreeViewModel item = new TreeViewModel(treeParent);
                treeParent.Child.Add(item);
                item.Node = node;

                if (p.Type == PluginType.Directory)
                {
                    item.SetSvgImage(Properties.Resources.SvgFolder, size, size);
                }
                else if (p.Type == PluginType.Plugin)
                {
                    if (p.Level == Project.MainLevel)
                        item.SetSvgImage(Properties.Resources.SvgProject, size, size);
                    else if (p.Mandatory)
                        item.SetSvgImage(Properties.Resources.SvgLock, size, size);
                    else
                        item.SetSvgImage(Properties.Resources.SvgFile, size, size);
                }
                else if (p.Type == PluginType.Link)
                {
                    item.SetSvgImage(Properties.Resources.SvgLink, size, size);
                }

                if (0 < node.Child.Count)
                    RecursivePopulateMainTreeView(node.Child, item);
            }
        }

        private void MainTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var tree = sender as TreeView;

            if (tree.SelectedItem is TreeViewModel)
            {
                TreeViewModel item = tree.SelectedItem as TreeViewModel;
                Plugin p = item.Node.Data;
                double size = 400; // pluginLogo.Width * maxDpiScale;
                switch(p.Type)
                {
                    case PluginType.Directory:
                        pluginLogo.Source = ImageHelper.SvgByteToBitmapImage(Properties.Resources.SvgFolder, size, size);
                        break;
                    case PluginType.Plugin:
                        pluginLogo.Source = ImageHelper.SvgByteToBitmapImage(Properties.Resources.SvgPlugin, size, size);
                        break;
                    case PluginType.Link:
                        pluginLogo.Source = ImageHelper.SvgByteToBitmapImage(Properties.Resources.SvgLink, size, size);
                        break;
                }
                pluginTitle.Text = p.Title;
                pluginDescription.Text = p.Description;
                pluginVersion.Text = $"v{p.Version}";
            }
            else
            {
                Debug.Assert(false);
            }
        }

        private void MainTreeView_Loaded(object sender, RoutedEventArgs e)
        {
            Window window = Window.GetWindow(this);
            window.KeyDown += MainTreeView_KeyDown;
        }

        /// <summary>
        /// Used to ensure pressing 'Space' to toggle TreeView's checkbox.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainTreeView_KeyDown(object sender, KeyEventArgs e)
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
    }
    #endregion

    #region TreeViewModel
    public class TreeViewModel : INotifyPropertyChanged
    {
        public TreeViewModel(TreeViewModel parent)
        {
            this.parent = parent;
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

                    if (0 < this.Child.Count)
                    { // Set child plugins, too -> Top-down propagation
                        foreach (TreeViewModel childModel in this.Child)
                        {
                            if (value)
                                childModel.Checked = true;
                            else
                                childModel.Checked = false;
                        }
                    }

                    ParentCheckedPropagation();
                    OnPropertyUpdate("Checked");
                }
            }
        }

        public void ParentCheckedPropagation()
        { // Bottom-up propagation of Checked property
            if (parent == null)
                return;

            bool setParentChecked = false;

            foreach (TreeViewModel sibling in parent.Child)
            { // Siblings
                if (sibling.Checked)
                    setParentChecked = true;
            }

            parent.SetParentChecked(setParentChecked);
        }

        public void SetParentChecked(bool value)
        {
            if (parent == null)
                return;

            if (node.Data.Mandatory == false && node.Data.Selected != SelectedState.None)
            {
                if (value)
                    node.Data.Selected = SelectedState.True;
                else
                    node.Data.Selected = SelectedState.False;
            }

            OnPropertyUpdate("Checked");
            ParentCheckedPropagation();
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

        private TreeViewModel parent;
        public TreeViewModel Parent { get => parent; }

        private ObservableCollection<TreeViewModel> child = new ObservableCollection<TreeViewModel>();
        public ObservableCollection<TreeViewModel> Child { get => child; }
        
        public void SetSvgImage(byte[] src, double width, double height)
        {
            imageWidth = width;
            imageHeight = height;
            imageSrc = src;
            ImageSvg = ImageHelper.SvgByteToBitmapImage(src, width, height);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyUpdate(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    #endregion

}
