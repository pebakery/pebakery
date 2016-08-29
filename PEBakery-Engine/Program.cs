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

            Plugin plugin = new Plugin(args[0]);
            Logger logger = new Logger("log.txt");
            BakerEngine engine = new BakerEngine(plugin, logger);
            engine.Debug();
            
            return 0;
        }

        
    }
}
