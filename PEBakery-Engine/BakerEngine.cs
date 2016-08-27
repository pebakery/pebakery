using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PEBakery_Engine
{
    class BakerEngine
    {
        // Field
        private PluginSection entryPoint;

        // Constructor
        public BakerEngine(Plugin plugin)
        {
            try
            {
                this.entryPoint = plugin.FindSection("Process");
            }
            catch (Exception e)
            {
                Console.WriteLine("[ERR]\n{0}", e.ToString());
            }
        }

        // Methods
        public void Debug()
        {
            try
            {
                Console.WriteLine(entryPoint.SectionData);
            }
            catch (Exception e)
            {
                Console.WriteLine("[ERR]\n{0}", e.ToString());
            }

        }
    }
}
