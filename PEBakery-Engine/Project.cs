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

    public class Project
    {
        // Fiels
        private string projectName;
        private string projectRoot;
        private Plugin mainPlugin;
        private PluginDictionary plugins;
        private int[] pluginLevels;

        // Properties
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
            CollectPlugins();
            stopwatch.Stop();
            Console.WriteLine("Time elapsed: {0}", stopwatch.Elapsed);
        }

        private void CollectPlugins()
        {
            // Colect all *.script
            Dictionary<int, List<string>> pluginsByLevel = new Dictionary<int, List<string>>();
            string[] files = Directory.GetFiles(projectRoot, "*.script", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                int level = int.Parse(IniFile.GetKey(file, "Main", "Level"));

                if (!pluginsByLevel.ContainsKey(level))
                    pluginsByLevel.Add(level, new List<string>());
                pluginsByLevel[level].Add(file);
            }

            pluginLevels = pluginsByLevel.Keys.OrderBy(i => i).ToArray();

            var parseTasks = pluginLevels.SelectMany(l => LoadPlugins(pluginsByLevel[l].OrderBy(p => p.ToLower()).ToArray(), l));
            Task.WaitAll(parseTasks.ToArray());
        }

        private IEnumerable<Task> LoadPlugins(string[] pluginsPaths, int level)
        {
            plugins[level] = new Plugin[pluginsPaths.Length];

            var i = 0;
            return pluginsPaths.Select(p =>
            {
                var t = i++;
                Console.WriteLine($"{level} {p}");
                return Task.Run(() => plugins[level][t] = new Plugin(p, projectRoot));
            });
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
            throw new PluginNotFoundException($"Plugin [{pluginName}] not found");
        }
    }
}
