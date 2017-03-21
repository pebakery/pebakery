using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using PEBakery.Lib;
using PEBakery.Exceptions;

namespace PEBakery.Object
{
    public class Project
    {
        // Fields
        private string projectName;
        private string projectRoot;
        private Plugin mainPlugin;
        private Tree<Plugin> allPlugins;
        private Tree<Plugin> visiblePlugins;
        private Tree<Plugin> activePlugins;
        public const int MainLevel = -256;  // Reserved level for script.project

        private int loadedPluginCount;
        private int allPluginCount;
        private string baseDir;
        private BackgroundWorker worker;

        // Properties
        public string ProjectName { get { return projectName; } }
        public string ProjectRoot { get { return projectRoot; } }
        public Plugin MainPlugin { get { return mainPlugin; } }
        public Tree<Plugin> AllPlugins { get { return allPlugins; } }
        public Tree<Plugin> VisiblePlugins { get => visiblePlugins; }
        public Tree<Plugin> ActivePlugins { get { return activePlugins; } }
        public int LoadedPluginCount { get => loadedPluginCount; }
        public int AllPluginCount { get => allPluginCount; }


        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="projectName"></param>
        public Project(string baseDir, string projectName, BackgroundWorker worker)
        {
            this.loadedPluginCount = 0;
            this.allPluginCount = 0;
            this.worker = worker;
            this.projectName = projectName;
            this.projectRoot = Path.Combine(baseDir, "Projects", projectName);
            this.baseDir = baseDir;
        }

        public void Load()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            Console.WriteLine("Parsing plugins start...");
            List<Plugin> allPluginList = CollectAllPlugins();
            Console.WriteLine("Parsing plugins done.");
            Console.WriteLine($"All Plugins : {allPluginList.Count}");
            Console.WriteLine("Time elapsed : {0}\r\n", stopwatch.Elapsed);
            List<Plugin> visiblePluginList = CollectVisiblePlugins(allPluginList);
            Console.WriteLine("Selected visible plugins.");
            Console.WriteLine($"Active Plugins : {visiblePluginList.Count}");
            Console.WriteLine("Time elapsed : {0}\r\n", stopwatch.Elapsed);
            List<Plugin> activePluginList = CollectActivePlugins(allPluginList);
            Console.WriteLine("Selected active plugins.");
            Console.WriteLine($"Active Plugins : {activePluginList.Count}");
            Console.WriteLine("Time elapsed : {0}\r\n", stopwatch.Elapsed);
            this.allPlugins = PluginListToTree(allPluginList);
            this.visiblePlugins = PluginListToTree(visiblePluginList);
            this.activePlugins = PluginListToTree(activePluginList);
            Console.WriteLine("Converted to Tree.");
            Console.WriteLine("Time elapsed : {0}\r\n", stopwatch.Elapsed);
            stopwatch.Stop();
        }

        private List<Plugin> CollectAllPlugins()
        {
            List<string> pPathList = new List<string>();

            // Collect mainPlugin (script.project)
            pPathList.Add(Path.Combine(projectRoot, "script.project"));

            // Collect all *.script
            string[] files = Directory.GetFiles(projectRoot, "*.script", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                // level must be bigger than mainLevel, and not level 0
                int level;
                if (int.TryParse(Ini.GetKey(file, "Main", "Level"), out level) == false)
                    level = 0;
                string directory = new DirectoryInfo(Path.GetDirectoryName(file)).Name;

                if (!(MainLevel < level))
                    continue;
                pPathList.Add(file);
                allPluginCount++;
            }

            List<Plugin> pList = new List<Plugin>();
            var parseTasks = LoadPlugins(pPathList, pList);
            Task.WaitAll(parseTasks.ToArray());

            // mainPlugin is the first element.
            this.mainPlugin = pList.Single(p => p.Level == MainLevel);
            // Sort by level and filename (lexicographic) - Well, this is done by PluginListToTree() later
            // return pList.OrderBy(p => p.Level).ThenBy(p => p.ShortPath).ToList();
            return pList;
        }

        private IEnumerable<Task> LoadPlugins(List<string> pPathList, List<Plugin> pList)
        {
            ReaderWriterLockSlim listLock = new ReaderWriterLockSlim();

            int i = 0;
            return pPathList.Select(pPath =>
            {
                int t = i++;
                return Task.Run(() =>
                {
                    Plugin p;
                    if (string.Equals(pPath, Path.Combine(projectRoot, "script.project"), StringComparison.OrdinalIgnoreCase))
                        p = new Plugin(PluginType.Plugin, pPath, projectRoot, MainLevel);
                    else
                        p = new Plugin(PluginType.Plugin, pPath, projectRoot, null);

                    listLock.EnterWriteLock();
                    try
                    {
                        pList.Add(p);
                    }
                    finally
                    {
                        listLock.ExitWriteLock();
                    }
#if DEBUG
                    Console.WriteLine(pPath);
#endif
                    Interlocked.Increment(ref loadedPluginCount);
                    worker.ReportProgress((loadedPluginCount * 100) / allPluginCount);
                });
            });
        }

        private List<Plugin> CollectVisiblePlugins(List<Plugin> allPluginList)
        {
            List<Plugin> visiblePluginList = new List<Plugin>();
            foreach (Plugin p in allPluginList)
            {
                if (p.Level != 0)
                    visiblePluginList.Add(p);
            }
            return visiblePluginList;
        }

        /// <summary>
        /// Filter active plugins from allPlugins dict
        /// Active Plugins : Will-be-processed plugins
        /// </summary>
        /// <remarks>
        /// Took 1ms, Tested on i5-6200U, 151 plugins (Win10PESE 2016-09-01 default)
        /// Time elapsed: 00:00:14.0766582 -> 00:00:14.0767675
        /// 
        /// All Plugins : 155
        /// </remarks>
        private List<Plugin> CollectActivePlugins(List<Plugin> allPluginList)
        {
            List<Plugin> activePluginList = new List<Plugin>();
            foreach (Plugin p in allPluginList)
            {
                bool active = false;
                if (!(p.MainInfo.ContainsKey("Selected") && string.Equals(p.MainInfo["Selected"], "None", StringComparison.OrdinalIgnoreCase)))
                {
                    if (p.MainInfo.ContainsKey("Selected") && string.Equals(p.MainInfo["Selected"], "True", StringComparison.OrdinalIgnoreCase))
                        active = true;
                    if (p.MainInfo.ContainsKey("Mandatory") && string.Equals(p.MainInfo["Mandatory"], "True", StringComparison.OrdinalIgnoreCase))
                        active = true;
                }

                if (active)
                {
                    activePluginList.Add(p);
                }
            }
            return activePluginList;
        }

        private string PathKeyGenerator(string[] paths, int last)
        { // last - start entry is 0
            StringBuilder builder = new StringBuilder();
            builder.Append(paths[0]);
            for (int i = 1; i <= last; i++)
            {
                builder.Append(Path.DirectorySeparatorChar);
                builder.Append(paths[i]);
            }
            return builder.ToString();
        }

        private Tree<Plugin> PluginListToTree(List<Plugin> pList)
        {
            Tree<Plugin> pTree = new Tree<Plugin>();
            Dictionary<string, int> dirDict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            int rootId = pTree.AddNode(0, this.MainPlugin); // Root is script.project

            foreach (Plugin p in pList)
            {
                if (p == this.MainPlugin)
                    continue;

                int nodeId = rootId;
                string[] paths = p.ShortPath.Split(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });

                // Ex) Apps\Network\Mozilla_Firefox_CR.script
                for (int i = 0; i < paths.Length - 1; i++)
                {
                    string pathKey = PathKeyGenerator(paths, i);
                    string key = p.Level.ToString() + pathKey;
                    if (dirDict.ContainsKey(key))
                    {
                        nodeId = dirDict[key];
                    }
                    else
                    {
                        Plugin dirPlugin = new Plugin(PluginType.Directory, Path.Combine(projectRoot, pathKey), baseDir, p.Level);
                        nodeId = pTree.AddNode(nodeId, dirPlugin);
                        dirDict[key] = nodeId;
                    }
                }
                pTree.AddNode(nodeId, p);
            }

            // Sort - Plugin first, Directory last
            pTree.Sort((x, y) => {
                if (x.Data.Level == y.Data.Level)
                {
                    if (x.Data.Type == y.Data.Type)
                        return x.Data.FullPath.CompareTo(y.Data.FullPath);
                    else
                        return x.Data.Type - y.Data.Type;
                }
                else
                    return x.Data.Level - y.Data.Level;
            });

            // Reflect Directory's Selected value
            RecursiveDecideDirectorySelectedValue(pTree.Root);

            return pTree;
        }

        // TODO: It violates Tree<T>'s abstraction...
        private SelectedState RecursiveDecideDirectorySelectedValue(List<Node<Plugin>> list)
        {
            SelectedState final = SelectedState.None;
            foreach (Node<Plugin> node in list)
            {
                if (0 < node.Child.Count)
                { // Has child plugins
                    SelectedState state = RecursiveDecideDirectorySelectedValue(node.Child);
                    if (state == SelectedState.True)
                        final = node.Data.Selected = SelectedState.True;
                    else if (state == SelectedState.False)
                    {
                        if (final != SelectedState.True)
                            final = SelectedState.False;
                        if (node.Data.Selected != SelectedState.True)
                            node.Data.Selected = SelectedState.False;
                    }
                }
                else // Does not have child plugin
                {
                    switch (node.Data.Selected)
                    {
                        case SelectedState.True:
                            final = SelectedState.True;
                            break;
                        case SelectedState.False:
                            if (final == SelectedState.None)
                                final = SelectedState.False;
                            break;
                    }
                }
            }

            return final;
        }
    }
}
