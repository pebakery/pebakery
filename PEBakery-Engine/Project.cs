using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace BakeryEngine
{
    using PluginDictionary = ConcurrentDictionary<int, Plugin[]>;

    public class Project
    {
        // Fiels
        private string projectName;
        private string projectRoot;
        private Plugin mainPlugin;
        private PluginCollection allPlugins;
        private PluginCollection activePlugins;
        public const int mainLevel = -256; // Reserved level for script.project

        // Properties
        public string ProjectName { get { return projectName; } }
        public string ProjectRoot { get { return projectRoot; } }
        public Plugin MainPlugin { get { return mainPlugin; } }
        public PluginCollection AllPlugins { get { return allPlugins; } }
        public PluginCollection ActivePlugins { get { return activePlugins; } }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="projectName"></param>
        public Project(string projectName)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            Console.WriteLine("Parsing plugins start...");
            this.projectName = projectName;
            this.projectRoot = Path.Combine(Helper.GetProgramAbsolutePath(), "Projects", projectName);
            this.allPlugins = CollectAllPlugins();
            Console.WriteLine("Parsing plugins done.");
            Console.WriteLine($"All Plugins : {allPlugins.Count}");
            Console.WriteLine("Time elapsed: {0}\n", stopwatch.Elapsed);
            stopwatch.Stop();
            this.activePlugins = CollectActivePlugins(this.allPlugins);
            Console.WriteLine("Selected active plugins.");
            Console.WriteLine($"Active Plugins : {activePlugins.Count}");
            Console.WriteLine("Time elapsed: {0}\n", stopwatch.Elapsed);
        }

        private PluginCollection CollectAllPlugins()
        {
            // Declare and init vars
            Dictionary<int, List<string>> pluginsByLevel = new Dictionary<int, List<string>>();
            PluginDictionary plugins = new PluginDictionary();
            
            // Collect mainPlugin (script.project)
            pluginsByLevel.Add(mainLevel, new List<string>());
            pluginsByLevel[mainLevel].Add(Path.Combine(projectRoot, "script.project"));
            // Collect all *.script
            string[] files = Directory.GetFiles(projectRoot, "*.script", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                // level must be bigger than mainLevel, and not level 0
                int level = int.Parse(IniFile.GetKey(file, "Main", "Level"));
                if (!(mainLevel < level))
                    continue; 
                if (!pluginsByLevel.ContainsKey(level))
                    pluginsByLevel.Add(level, new List<string>());
                pluginsByLevel[level].Add(file);
            }

            // Convert dict's key to int array
            int[] levels = pluginsByLevel.Keys.OrderBy(level => level).ToArray();

            var parseTasks = levels.SelectMany(level => LoadPlugins(plugins, pluginsByLevel[level].OrderBy(p => p.ToLower()).ToArray(), level));
            Task.WaitAll(parseTasks.ToArray());

            // mainPlugin is mainLevel's first element, since mainLevel's element is always one.
            this.mainPlugin = plugins[mainLevel][0];
            return new PluginCollection(plugins, levels, files.Length);
        }

        private IEnumerable<Task> LoadPlugins(PluginDictionary plugins, string[] pluginsPaths, int level)
        {
            plugins[level] = new Plugin[pluginsPaths.Length];

            var i = 0;
            return pluginsPaths.Select(p =>
            {
                var t = i++;
                return Task.Run(() => plugins[level][t] = new Plugin(p, projectRoot));
            });
        }

        /// <summary>
        /// Filter non-active plugins from allPlugins dict
        /// </summary>
        /// <remarks>
        /// Took 1ms, Tested on i5-6200U, 151 plugins (Win10PESE 2016-09-01 default)
        /// Time elapsed: 00:00:14.0766582 -> 00:00:14.0767675
        /// 
        /// All Plugins : 155
        /// </remarks>
        private PluginCollection CollectActivePlugins(PluginCollection allPlugins)
        {
            int count = 0;
            PluginDictionary plugins = new PluginDictionary();
            List<int> activeLevelsList = new List<int>();
            foreach (int level in allPlugins.Dict.Keys)
            {
                bool levelSelected = false;
                List<Plugin> activePluginsList = new List<Plugin>();
                foreach (Plugin plugin in allPlugins.Dict[level])
                {
                    bool active = false;
                    if (!(plugin.MainInfo.ContainsKey("Selected") && string.Equals(plugin.MainInfo["Selected"], "None", StringComparison.OrdinalIgnoreCase)))
                    {
                        if (plugin.MainInfo.ContainsKey("Selected") && string.Equals(plugin.MainInfo["Selected"], "True", StringComparison.OrdinalIgnoreCase))
                            active = true;
                        if (plugin.MainInfo.ContainsKey("Mandatory") && string.Equals(plugin.MainInfo["Mandatory"], "True", StringComparison.OrdinalIgnoreCase))
                            active = true;
                    }

                    if (active)
                    {
                        levelSelected = true;
                        activePluginsList.Add(plugin);
                        count++;
                    }
                }

                if (levelSelected)
                {
                    activeLevelsList.Add(level);
                    plugins[level] = activePluginsList.ToArray();
                }
            }
            int[] levels = activeLevelsList.OrderBy(i => i).ToArray();
            return new PluginCollection(plugins, levels, count);
        }

    }
}
