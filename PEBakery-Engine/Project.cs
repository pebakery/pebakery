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
    class Project
    {
        private string projectName;
        private string projectRoot;
        private Plugin mainPlugin;
        // private PluginDictionary plugins;
        private Plugin[] plugins;

        public Project(string projectName)
        {
            this.projectName = projectName;
            this.projectRoot = Path.Combine(Helper.GetProgramPath(), "Projects", projectName);
            this.mainPlugin = new Plugin(Path.Combine(projectRoot, "script.project"));
            SearchPlugins();
        }

        private void SearchPlugins()
        {
            ArrayList list = new ArrayList();
            Dictionary<string, string> parentsDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string[] files = Directory.GetFiles(projectRoot, "*.script", SearchOption.AllDirectories);
            foreach (string file in files)
                list.Add(new Plugin(file));
            plugins = list.ToArray(typeof(Plugin)) as Plugin[];
        }

        private void SortPlugins()
        {

        }
    }
}
