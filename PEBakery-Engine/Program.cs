using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace BakeryEngine
{
    class PEBakery
    {
        static int Main(string[] args)
        {
            Project project = new Project("Win10PESE");
            Logger logger = new Logger("log.txt", LogFormat.Text);
            BakeryEngine engine = new BakeryEngine(project, logger);
            // engine.RunPlugin();

            return 0;
        }
    }

    public class PEBakeryInfo
    {
        private Version ver;
        public Version Ver
        {
            get { return ver;  }
        }
        private DateTime build;
        public DateTime Build
        {
            get { return build; }
        }
        public string baseDir;
        public string BaseDir
        {
            get { return baseDir; }
        }

        public PEBakeryInfo()
        {
            this.baseDir = Helper.GetProgramAbsolutePath();
            this.ver = Helper.GetProgramVersion();
            this.build = Helper.GetBuildDate();
        } 
    }
}
