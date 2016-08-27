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
            plugin.Debug();
            BakerEngine engine = new BakerEngine(plugin);
            engine.Debug();
            
            return 0;
        }

        
    }
}
