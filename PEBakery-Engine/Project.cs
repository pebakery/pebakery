using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace BakeryEngine
{
    using PluginDictionary = Dictionary<int, Plugin[]>;
    class Project
    {
        private string projectName;
        private string projectRoot;
        private Plugin mainPlugin;
        private PluginDictionary plugins;

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
            ArrayList list = new ArrayList();
            Dictionary<int, ArrayList> pluginsPathByLevel = new Dictionary<int, ArrayList>();
            string[] files = Directory.GetFiles(projectRoot, "*.script", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                int level = int.Parse(IniFile.GetKey(file, "Main", "Level"));
                if (!pluginsPathByLevel.ContainsKey(level))
                    pluginsPathByLevel[level] = new ArrayList();
                pluginsPathByLevel[level].Add(file);
            }

            foreach (int level in pluginsPathByLevel.Keys)
            {
                pluginsPathByLevel[level].Sort(StringComparer.OrdinalIgnoreCase); // Sort lexicographically 
                ArrayList pluginsByLevel = new ArrayList();
                foreach (string file in pluginsPathByLevel[level].ToArray(typeof(string)) as string[])
                    pluginsByLevel.Add(new Plugin(file, projectRoot));
                plugins[level] = pluginsByLevel.ToArray(typeof(Plugin)) as Plugin[];
            }
        }
    }
}
