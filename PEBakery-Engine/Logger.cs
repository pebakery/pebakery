using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace PEBakery_Engine
{
    public class Logger
    {
        private string logFileName;
        private FileStream fs;
        private StreamWriter sw;

        public Logger(string logFileName)
        {
            try
            {
                this.logFileName = logFileName;

                fs = new FileStream(this.logFileName, FileMode.Create, FileAccess.Write, FileShare.Write);
                sw = new StreamWriter(fs, Encoding.UTF8);
            }
            catch (Exception e)
            {
                Console.WriteLine("[ERR]\n{0}", e.ToString());
            }
        }

        public void LogRaw(string log)
        {
            sw.Write(log);
        }

        public void Close()
        {
            sw.Close();
            fs.Close();
        }
    }
}
