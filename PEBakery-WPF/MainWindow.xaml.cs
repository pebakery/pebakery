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

namespace PEBakery.WPF
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        private Project project;
        private ProgressBar loadProgressBar;
        private TextBlock statusBar;
        private Stopwatch stopwatch;

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

            loadWorker.WorkerReportsProgress = true;
            loadWorker.DoWork += new DoWorkEventHandler(bgWorker_LoadProject);
            loadWorker.ProgressChanged += new ProgressChangedEventHandler(loadWorker_ProgressChanged);
            loadWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(loadWorker_RunWorkerCompleted);

            stopwatch = Stopwatch.StartNew();
            loadWorker.RunWorkerAsync(argBaseDir);
        }

        private BackgroundWorker loadWorker = new BackgroundWorker();

        void bgWorker_LoadProject(object sender, DoWorkEventArgs e)
        {
            string baseDir = (string) e.Argument;

            BackgroundWorker worker = sender as BackgroundWorker;

            project = new Project(baseDir, "Win10PESE", worker);
            project.Load();
        }

        private void loadWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            this.loadProgressBar.Value = e.ProgressPercentage;
        }

        private void loadWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            stopwatch.Stop();
            this.statusBar.Text = $"Scan took {stopwatch.Elapsed}";
            this.bottomDock.Child = statusBar;

            List<Node<Plugin>> plugins = project.VisiblePlugins.Root;
            RecursivePopulateMainTreeView(plugins, this.mainTreeView.Items);
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

            // ... Determine type of SelectedItem.
            if (tree.SelectedItem is TreeViewItem)
            {
                // ... Handle a TreeViewItem.
                var item = tree.SelectedItem as TreeViewItem;
                this.mainContainer.Text = "Selected header: " + item.Header.ToString();
            }
            else if (tree.SelectedItem is string)
            {
                // ... Handle a string.
                this.mainContainer.Text = "Selected: " + tree.SelectedItem.ToString();
            }
        }
    }

    class BakeryTreeViewItem : TreeViewItem
    {

    }
}
