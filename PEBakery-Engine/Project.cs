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
            ArrayList levelList = new ArrayList();
            Dictionary<int, ArrayList> pluginsPathByLevel = new Dictionary<int, ArrayList>();
            string[] files = Directory.GetFiles(projectRoot, "*.script", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                int level = int.Parse(IniFile.GetKey(file, "Main", "Level"));
                if (!levelList.Contains(level))
                    levelList.Add(level);
                if (!pluginsPathByLevel.ContainsKey(level))
                    pluginsPathByLevel[level] = new ArrayList();
                pluginsPathByLevel[level].Add(file);
            }

            levelList.Sort();
            levels = levelList.ToArray(typeof(int)) as int[];

            Thread[] parseThreads = new Thread[levels.Length];
            for (int i = 0; i < levels.Length; i++)
            {
                int level = levels[i];
                pluginsPathByLevel[level].Sort(StringComparer.OrdinalIgnoreCase); // Sort lexicographically                
                parseThreads[i] = new Thread(new ThreadStart(() => LoadPlugins(pluginsPathByLevel[level].ToArray(typeof(string)) as string[], level)));
                parseThreads[i].Start();
            }
            foreach (var thread in parseThreads)
                thread.Join();



            /*
                for (int i = 0; i < levels.Length; i++)
                {
                    parsePlugin = new Thread();
                    int level = levels[i];
                    pluginsPathByLevel[level].Sort(StringComparer.OrdinalIgnoreCase); // Sort lexicographically 
                    ArrayList pluginsByLevel = new ArrayList();
                    foreach (string file in pluginsPathByLevel[level].ToArray(typeof(string)) as string[])
                    {
                        Console.WriteLine(level + " " + file);
                        pluginsByLevel.Add(new Plugin(file, projectRoot));
                    }
                    plugins[level] = pluginsByLevel.ToArray(typeof(Plugin)) as Plugin[];
                }
                */

        }

        private void LoadPlugins(string[] pluginsPaths, int level)
        {
            foreach (string file in pluginsPaths)
            {
                ArrayList pluginsByLevel = new ArrayList();
                Console.WriteLine(level + " " + file);
                pluginsByLevel.Add(new Plugin(file, projectRoot));
                plugins[level] = pluginsByLevel.ToArray(typeof(Plugin)) as Plugin[];
            }
        }
    }
}
