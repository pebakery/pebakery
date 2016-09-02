using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace BakeryEngine
{
    using IniDictionary = Dictionary<string, Dictionary<string, string>>;
    public class IniFile
    {
        private string iniFile;
        private IniDictionary key;

        public IniFile(string iniFile)
        {
            this.iniFile = iniFile;
            key = new IniDictionary();
        }
        
        public void ParseIni()
        {
            StreamReader sr = new StreamReader(iniFile);
        }
    }
}
