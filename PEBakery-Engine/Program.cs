using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace BakeryEngine
{
    class PEBakery_Engine
    {
        static int Main(string[] args)
        {            
            if (args.Length < 1)
            {
                Console.WriteLine("[ERR] No arg specified");
                return 1;
            }

            Plugin plugin = new Plugin(args[0]);
            Logger logger = new Logger("log.txt", LogFormat.Text);
            BakeryEngine engine = new BakeryEngine(plugin, logger);
            engine.RunPlugin();

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

        public PEBakeryInfo()
        {
            this.ver = Helper.GetProgramVersion();
            this.build = Helper.GetBuildDate();
        } 
    }
}
