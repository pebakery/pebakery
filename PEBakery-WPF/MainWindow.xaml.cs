using System;
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
using PEBakery.Helper;
using PEBakery.Lib;
using PEBakery.Object;
using System.IO;

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

            loadWorker.WorkerReportsProgress = true;
            loadWorker.DoWork += new DoWorkEventHandler(bgWorker_LoadProject);
            loadWorker.ProgressChanged += new ProgressChangedEventHandler(loadWorker_ProgressChanged);
            loadWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(loadWorker_RunWorkerCompleted);

            loadWorker.RunWorkerAsync(baseDir);
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
            // this.loadProgressBar.Value = e.ProgressPercentage;
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
                RecursivePopulateMainTreeView(plugins, this.mainTreeView.Items);
            }   
        }

        private void RecursivePopulateMainTreeView(List<Node<Plugin>> plugins, ItemCollection treeParent)
        {
            foreach (Node<Plugin> node in plugins)
            {
                Plugin p = node.Data;
                TreeViewItem item = new TreeViewItem();
                treeParent.Add(item);
                item.Header = p.Title;
                item.Tag = p;
                // if (p.Selected)
                    // item.CheckBox
                if (0 < node.Child.Count)
                    RecursivePopulateMainTreeView(node.Child, item.Items);
            }
        }

        private void mainTreeView_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void mainTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var tree = sender as TreeView;

            if (tree.SelectedItem is TreeViewItem)
            {
                TreeViewItem item = tree.SelectedItem as TreeViewItem;
                Plugin p = item.Tag as Plugin;
                this.mainContainer.Text = $"Selected: {p.Title}, Level = {p.Level}";

            }
            else
            {
                Debug.Assert(false);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            mainTreeView.Items.Clear();
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
}
