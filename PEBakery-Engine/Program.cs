using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace PEBakery_Engine
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

            PEBakeryInfo info = new PEBakeryInfo(new Version(1, 0, 0), Helper.GetBuildDate());
            Plugin plugin = new Plugin(args[0]);
            Logger logger = new Logger("log.txt", LogFormat.Text, info);
            BakerEngine engine = new BakerEngine(plugin, logger);
            engine.Run();
            engine.Debug();

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

        public PEBakeryInfo(Version ver, DateTime build)
        {
            this.ver = ver;
            this.build = build;
        } 
    }
}
