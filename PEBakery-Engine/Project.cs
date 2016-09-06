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
        private PluginDictionary plugins;
        private int[] pluginLevels;
        private int pluginCount;
        private int processedPlugins;
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
            Console.WriteLine("Parsing plugins start...");
            processedPlugins = 0;
            pluginCount = 0;
            this.projectName = projectName;
            this.projectRoot = Path.Combine(Helper.GetProgramAbsolutePath(), "Projects", projectName);
            CollectPlugins();
            Console.WriteLine("Parsing plugins done.");
            stopwatch.Stop();
            Console.WriteLine("Time elapsed: {0}\n", stopwatch.Elapsed);
        }

        private void CollectPlugins()
        {
            // Declare and init vars
            Dictionary<int, List<string>> pluginsByLevel = new Dictionary<int, List<string>>();
            this.plugins = new PluginDictionary();
            // Collect mainPlugin (script.project)
            pluginsByLevel.Add(mainLevel, new List<string>());
            pluginsByLevel[mainLevel].Add(Path.Combine(projectRoot, "script.project"));
            // Collect all *.script
            string[] files = Directory.GetFiles(projectRoot, "*.script", SearchOption.AllDirectories);
            pluginCount = files.Length;
            foreach (string file in files)
            {
                // level must be bigger than mainLevel
                int level = int.Parse(IniFile.GetKey(file, "Main", "Level"));
                if (!(mainLevel < level))
                    continue;
                if (!pluginsByLevel.ContainsKey(level))
                    pluginsByLevel.Add(level, new List<string>());
                pluginsByLevel[level].Add(file);
            }

            // Convert dict's key to int array
            pluginLevels = pluginsByLevel.Keys.OrderBy(i => i).ToArray();

            var parseTasks = pluginLevels.SelectMany(l => LoadPlugins(pluginsByLevel[l].OrderBy(p => p.ToLower()).ToArray(), l));
            Task.WaitAll(parseTasks.ToArray());

            // mainPlugin is mainLevel's first element, since mainLevel's element is always one.
            this.mainPlugin = plugins[mainLevel][0];
        }

        private IEnumerable<Task> LoadPlugins(string[] pluginsPaths, int level)
        {
            plugins[level] = new Plugin[pluginsPaths.Length];

            var i = 0;
            return pluginsPaths.Select(p =>
            {
                var t = i++;
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
                    {
                        Interlocked.Increment(ref pluginCount);
                        return plugins[level][i];
                    }
                }
            }
            // not found
            throw new PluginNotFoundException($"Plugin [{pluginName}] not found");
        }

        public PluginAddress GetPluginAddress(Plugin plugin)
        {
            int level = 0;
            int index = 0;
            bool found = false;

            for (int i = 0; i < PluginLevels.Length; i++)
            {
                level = PluginLevels[i];
                index = Array.IndexOf<Plugin>(Plugins[level], plugin);
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

        public PluginAddress GetNextPluginAddress(PluginAddress addr)
        {
            if (addr.index < Plugins[addr.level].Length - 1)
                addr.index++;
            else
            {
                // Increment level value
                int idx = Array.IndexOf<int>(PluginLevels, addr.level); // if fail, return -1
                if (PluginLevels.Length <= idx) // end of level
                    throw new EndOfPluginLevelException();
                addr.level = PluginLevels[idx + 1];
                addr.index = 0;
            }
            return addr;
        }

        public Plugin GetPluginFromAddress(PluginAddress addr)
        {
            return Plugins[addr.level][addr.index];
        }
    }
}
