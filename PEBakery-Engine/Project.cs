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

    /// <summary>
    /// Struct to address PluginDictionary
    /// </summary>
    public struct PluginAddress
    { // Return address format = <Section>'s <n'th line>
        public int level;
        public int index;
        public PluginAddress(int level, int index)
        {
            this.level = level;
            this.index = index;
        }
    }

    public class PluginNotFoundException : Exception
    {
        public PluginNotFoundException() { }
        public PluginNotFoundException(string message) : base(message) { }
        public PluginNotFoundException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Reached end of plugin levels
    /// </summary>
    public class EndOfPluginLevelException : Exception
    {
        public EndOfPluginLevelException() { }
        public EndOfPluginLevelException(string message) : base(message) { }
        public EndOfPluginLevelException(string message, Exception inner) : base(message, inner) { }
    }

    public class Project
    {
        // Fiels
        private string projectName;
        private string projectRoot;
        private Plugin mainPlugin;
        private PluginDictionary allPlugins; // All plugins
        private int[] allLevels; // plugin's level array
        private int allPluginsCount;
        private PluginDictionary activePlugins; // Selected (will-be run) plugins
        private int[] activeLevels; // selected plugins's level array
        private int activePluginsCount;
        public const int mainLevel = -256; // Reserved level for script.project

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
            get { return mainPlugin; }
        }
        public PluginDictionary AllPlugins
        {
            get { return allPlugins;}
        }
        public int[] AllLevels
        {
            get { return allLevels; }
        }
        public int AllPluginCount
        {
            get { return allPluginsCount; }
        }
        public PluginDictionary ActivePlugins
        {
            get { return activePlugins; }
        }
        public int[] ActiveLevels
        {
            get { return activeLevels; }
        }
        public int ActivePluginCount
        {
            get { return activePluginsCount; }
        }

        public Project(string projectName)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            Console.WriteLine("Parsing plugins start...");
            allPluginsCount = 0;
            this.projectName = projectName;
            this.projectRoot = Path.Combine(Helper.GetProgramAbsolutePath(), "Projects", projectName);
            CollectAllPlugins();
            Console.WriteLine("Parsing plugins done.");
            Console.WriteLine($"All Plugins : {allPluginsCount}");
            Console.WriteLine("Time elapsed: {0}\n", stopwatch.Elapsed);
            stopwatch.Stop();
            CollectActivePlugins();
            Console.WriteLine("Selected active plugins.");
            Console.WriteLine($"Active Plugins : {activePluginsCount}");
            Console.WriteLine("Time elapsed: {0}\n", stopwatch.Elapsed);
        }

        private void CollectAllPlugins()
        {
            // Declare and init vars
            Dictionary<int, List<string>> pluginsByLevel = new Dictionary<int, List<string>>();
            allPlugins = new PluginDictionary();
            
            // Collect mainPlugin (script.project)
            pluginsByLevel.Add(mainLevel, new List<string>());
            pluginsByLevel[mainLevel].Add(Path.Combine(projectRoot, "script.project"));
            // Collect all *.script
            string[] files = Directory.GetFiles(projectRoot, "*.script", SearchOption.AllDirectories);
            allPluginsCount = files.Length;
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
            allLevels = pluginsByLevel.Keys.OrderBy(i => i).ToArray();

            var parseTasks = allLevels.SelectMany(l => LoadPlugins(pluginsByLevel[l].OrderBy(p => p.ToLower()).ToArray(), l));
            Task.WaitAll(parseTasks.ToArray());

            // mainPlugin is mainLevel's first element, since mainLevel's element is always one.
            this.mainPlugin = allPlugins[mainLevel][0];
        }

        private IEnumerable<Task> LoadPlugins(string[] pluginsPaths, int level)
        {
            allPlugins[level] = new Plugin[pluginsPaths.Length];

            var i = 0;
            return pluginsPaths.Select(p =>
            {
                var t = i++;
                return Task.Run(() => allPlugins[level][t] = new Plugin(p, projectRoot));
            });
        }

        /// <summary>
        /// Filter non-active plugins from allPlugins dict
        /// </summary>
        /// <remarks>
        /// Took 1ms, Tested on i5-6200U, 151 plugins (Win10PESE 2016-09-01 default)
        /// Time elapsed: 00:00:13.2500294 -> 00:00:13.2501122
        /// </remarks>
        private void CollectActivePlugins()
        {
            activePluginsCount = 0;
            activePlugins = new PluginDictionary();
            List<int> activeLevelsList = new List<int>();
            foreach (int level in allPlugins.Keys)
            {
                bool levelSelected = false;
                List<Plugin> activePluginsList = new List<Plugin>();
                foreach (Plugin plugin in allPlugins[level])
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
                        activePluginsCount++;
                    }
                }

                if (levelSelected)
                {
                    activeLevelsList.Add(level);
                    activePlugins[level] = activePluginsList.ToArray();
                }
            }
            activeLevels = activeLevelsList.OrderBy(i => i).ToArray();
        }

        public Plugin SearchPluginByName(string pluginName)
        {
            foreach (int level in allPlugins.Keys)
            {
                for (int i = 0; i < allPlugins[level].Length; i++)
                {
                    if (string.Equals(pluginName, allPlugins[level][i].PluginName, StringComparison.OrdinalIgnoreCase))
                    {
                        // Interlocked.Increment(ref allPluginsCount);
                        return allPlugins[level][i];
                    }
                }
            }
            // not found
            throw new PluginNotFoundException($"Plugin [{pluginName}] not found");
        }



        public PluginAddress GetActivePluginAddress(Plugin plugin)
        {
            int level = 0;
            int index = 0;
            bool found = false;

            for (int i = 0; i < activeLevels.Length; i++)
            {
                level = activeLevels[i];
                if (level == 0) // Level 0 is usually script
                    continue;
                index = Array.IndexOf<Plugin>(ActivePlugins[level], plugin);
                if (index != -1) // found!
                {
                    found = true;
                    break;
                }
            }

            if (found)
                return new PluginAddress(level, index);
            else
                throw new PluginNotFoundException();
        }

        public PluginAddress GetNextActivePluginAddress(PluginAddress addr)
        {
            {// Find just next plugin
                if (addr.index < activePlugins[addr.level].Length - 1)
                    addr.index++;
                else
                {
                    // Increment level value
                    int idx = Array.IndexOf<int>(activeLevels, addr.level); // if fail, return -1
                    if (activeLevels.Length <= idx + 1) // end of level
                        throw new EndOfPluginLevelException();
                    addr.level = activeLevels[idx + 1];
                    addr.index = 0;
                }
            }


            return addr;
        }

        public Plugin GetActivePluginFromAddress(PluginAddress addr)
        {
            return activePlugins[addr.level][addr.index];
        }

        public int GetFullIndexOfActivePlugin(PluginAddress addr)
        {
            int fullIndex = addr.index + 1; // Human-readable index starts from 1
            for (int i = 0; i < activeLevels.Length; i++)
            {
                if (activeLevels[i] < addr.level)
                    fullIndex += activePlugins[activeLevels[i]].Length;
                else
                    break;
            }
            return fullIndex;
        }
    }
}
