using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading;

namespace BakeryEngine
{
    using System.Threading.Tasks;
    using PluginDictionary = ConcurrentDictionary<int, Plugin[]>;
    class Project
    {
        private string projectName;
        private string projectRoot;
        private Plugin mainPlugin;
        private PluginDictionary plugins;
        private int[] levels;

        public Project(string projectName)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            this.projectName = projectName;
            this.projectRoot = Path.Combine(Helper.GetProgramAbsolutePath(), "Projects", projectName);
            this.mainPlugin = new Plugin(Path.Combine(projectRoot, "script.project"), projectRoot);
            this.plugins = new PluginDictionary();
            SearchAndLoadPlugins();
            stopwatch.Stop();
            Console.WriteLine("Time elapsed: {0}", stopwatch.Elapsed);

        }

        private void SearchAndLoadPlugins()
        {
            // Search all *.script
            Dictionary<int, List<string>> pluginsPathByLevel = new Dictionary<int, List<string>>();
            string[] files = Directory.GetFiles(projectRoot, "*.script", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                int level = int.Parse(IniFile.GetKey(file, "Main", "Level"));
                if (!pluginsPathByLevel.ContainsKey(level))
                {
                    pluginsPathByLevel.Add(level, new List<string>());
                }
                pluginsPathByLevel[level].Add(file);
            }
            
            levels = pluginsPathByLevel.Keys.OrderBy(i => i).ToArray();

            var parseTasks = levels.SelectMany(l => LoadPlugins(pluginsPathByLevel[l].OrderBy(p => p.ToLower()).ToArray(), l));
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
    }
}
