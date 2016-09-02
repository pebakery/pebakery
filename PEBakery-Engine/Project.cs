using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BakeryEngine
{
    class Project
    {
        private string projectName;
        public Project()
        {
            string projectBase = Path.Combine(Helper.GetProgramPath(), "Projects");
            string bakerySetting = Path.Combine(projectBase, "PEBakery.ini");
            string projectInfo = Path.Combine(projectBase, "script.project");


        }
    }
}
