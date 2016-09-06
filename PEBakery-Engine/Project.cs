using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;

namespace BakeryEngine
{
    using PluginDictionary = ConcurrentDictionary<int, Plugin[]>;

    public class PluginNotFoundException : Exception
    {
        public PluginNotFoundException() { }
        public PluginNotFoundException(string message) : base(message) { }
        public PluginNotFoundException(string message, Exception inner) : base(message, inner) { }
    }

    class Project
    {
        private string projectName;
        private string projectRoot;
        private Plugin mainPlugin;
        private PluginDictionary plugins;
        private int[] pluginLevels;

        public string ProjectName
        {
            get { return projectName; }
        }
        public string ProjectRoot
        {
            get { return projectRoot; }
        }
        public Plugin MainPlugin
        {
            get { return MainPlugin; }
        }
        public PluginDictionary Plugins
        {
            get { return plugins;}
        }
        public int[] PluginLevels
        {
            get { return pluginLevels; }
        }

        public Project(string projectName)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            this.projectName = projectName;
            this.projectRoot = Path.Combine(Helper.GetProgramAbsolutePath(), "Projects", projectName);
            this.mainPlugin = new Plugin(Path.Combine(projectRoot, "script.project"), projectRoot);
            this.plugins = new PluginDictionary();
            LoadPlugins(CollectPlugins());
            stopwatch.Stop();
            Console.WriteLine("Time elapsed: {0}", stopwatch.Elapsed);
        }

        private Dictionary<int, ArrayList> CollectPlugins()
        {
            // Search all *.script
            ArrayList levelList = new ArrayList();
            Dictionary<int, ArrayList> pluginsByLevel = new Dictionary<int, ArrayList>();
            string[] files = Directory.GetFiles(projectRoot, "*.script", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                int level = int.Parse(IniFile.GetKey(file, "Main", "Level"));
                if (!levelList.Contains(level))
                    levelList.Add(level);
                if (!pluginsByLevel.ContainsKey(level))
                    pluginsByLevel[level] = new ArrayList();
                pluginsByLevel[level].Add(file);
            }
            
            levelList.Sort();
            pluginLevels = levelList.ToArray(typeof(int)) as int[];
            foreach (int level in pluginLevels)
                pluginsByLevel[level].Sort(StringComparer.OrdinalIgnoreCase); // Sort lexicographically
            return pluginsByLevel;
        }

        private void LoadPlugins(Dictionary<int, ArrayList> pluginsByLevel)
        {
            Task[] parseTasks = new Task[pluginLevels.Length];
            for (int i = 0; i < pluginLevels.Length; i++)
            {
                int level = pluginLevels[i];
                pluginsByLevel[level].Sort(StringComparer.OrdinalIgnoreCase); // Sort lexicographically                
                parseTasks[i] = new Task(() => { InternalLoadPlugins(pluginsByLevel[level].ToArray(typeof(string)) as string[], level); });
                parseTasks[i].Start();
            }
            Task.WaitAll(parseTasks);
        }

        private void InternalLoadPlugins(string[] pluginsPaths, int level)
        {
            foreach (string file in pluginsPaths)
            {
                ArrayList pluginsByLevel = new ArrayList();
                Console.WriteLine(level + " " + file);
                pluginsByLevel.Add(new Plugin(file, projectRoot));
                plugins[level] = pluginsByLevel.ToArray(typeof(Plugin)) as Plugin[];
            }
        }

        public Plugin SearchPluginByName(string pluginName)
        {
            foreach (int level in plugins.Keys)
            {
                for (int i = 0; i < plugins[level].Length; i++)
                {
                    if (string.Equals(pluginName, plugins[level][i].PluginName, StringComparison.OrdinalIgnoreCase))
                        return plugins[level][i];
                }
            }
            // not found
            throw new PluginNotFoundException("Plugin [" + pluginName + "] not found");
        }
    }
}
