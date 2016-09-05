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
            Plugin[] plugins = new Plugin[] { new Plugin(Helper.GetProgramAbsolutePath() + "\\test.script", Helper.GetProgramAbsolutePath()) };
            Logger logger = new Logger("log.txt", LogFormat.Text);
            BakeryEngine engine = new BakeryEngine(plugins, logger);
            engine.RunPlugin();

            IniKey[] ini = new IniKey[] { new IniKey("Main", "A", "1"), new IniKey("Main", "B", "2"), new IniKey("MainC", "C", "3") };
            IniFile.SetIniKeys("new.ini", ini);
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
