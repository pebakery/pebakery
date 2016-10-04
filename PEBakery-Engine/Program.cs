using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;

namespace BakeryEngine
{
    class PEBakery
    {
        static int Main(string[] args)
        {
            // Project project = new Project("Win10PESE");
            Project project = new Project("Joveler");
            Logger logger = new Logger("log.txt", LogFormat.Text);
            BakeryEngine engine = new BakeryEngine(project, logger);
            Stopwatch stopwatch = Stopwatch.StartNew();
            Console.WriteLine("BakeryEngine start...");

            /*
            // Test of BakeryCodeParser
            project.MainPlugin.Sections["Process"] = BakeryCodeParser.ParsePluginSection(project.MainPlugin.Sections["Process"] as PluginLineSection);
            List<BakeryCommand> cmds = project.MainPlugin.Sections["Process"].Get() as List<BakeryCommand>;
            for (int i = 0; i < project.MainPlugin.Sections["Process"].Count; i++)
                Console.WriteLine(cmds[i]);
            */
            engine.Build();

            Console.WriteLine("BakeryEngine done");
            Console.WriteLine("Time elapsed: {0}\n", stopwatch.Elapsed);

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
