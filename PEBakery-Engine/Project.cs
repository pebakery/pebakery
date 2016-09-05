using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tree;

namespace BakeryEngine
{
    using PluginDictionary = Dictionary<int, Plugin[]>;
    class Project
    {
        private string projectName;
        private string projectRoot;
        private Plugin mainPlugin;
        private PluginDictionary plugins;
        // private Plugin[] plugins;

        public Project(string projectName)
        {
            this.projectName = projectName;
            this.projectRoot = Path.Combine(Helper.GetProgramAbsolutePath(), "Projects", projectName);
            this.mainPlugin = new Plugin(Path.Combine(projectRoot, "script.project"), projectRoot);
            SearchPlugins();
        }

        private void SearchPlugins()
        {
            
            // Search all *.script
            ArrayList list = new ArrayList();
            // Dictionary<string, string> parentsDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string[] files = Directory.GetFiles(projectRoot, "*.script", SearchOption.AllDirectories);
            foreach (string file in files)
            {

            }
            // list.Add(new Plugin(file, projectRoot));
            // plugins = list.ToArray(typeof(Plugin)) as Plugin[];

            /*
            // Now, sort it
            // PluginDictionary dict = new PluginDictionary();
            int maxLevel = 0;
            Dictionary<int, ArrayList> dict = new Dictionary<int, ArrayList>();
            // First sort by Level
            foreach (Plugin p in list)
            {
                int level = int.Parse(p.MainInfo["Level"]);
                if (maxLevel < level)
                    maxLevel = level; 
                if (!dict.ContainsKey(level))
                    dict[level] = new ArrayList();
                dict[level].Add(p.ShortPath);
            }

            // Second sort by ordinal lexicographic order
            this.plugins = new PluginDictionary();
            for (int i = 0; i < maxLevel; i++)
            {
                if (dict.ContainsKey(i))
                {
                    string[] levelPlugins = dict[i].ToArray(typeof(string)) as string[];
                    Array.Sort<string>(levelPlugins, StringComparer.OrdinalIgnoreCase);
                }
            }
            */
        }

        private void SortPlugins(ArrayList list)
        {
            
        }
    }
}
