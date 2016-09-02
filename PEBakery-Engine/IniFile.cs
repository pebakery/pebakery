using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace BakeryEngine
{
    public class IniFile
    {
        private string iniFile;
        

        public IniFile(string iniFile)
        {
            this.iniFile = iniFile;

            
        }
        
        public void ParseIni()
        {
            StreamReader sr = new StreamReader(iniFile);
        }
    }
}
