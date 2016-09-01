using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections;

namespace BakeryEngine
{
    public class Plugin
    {
        // Fields
        private string fileName;
        public string FileName
        {
            get
            {
                return fileName;
            }
        }
        private string rawData;
        private Dictionary<string, PluginSection> sections;
        public Dictionary<string, PluginSection> Sections
        {
            get { return sections; }
        }

        // Constructor
        public Plugin(string fileName)
        {
            this.fileName = fileName;
            this.rawData = null;
            this.sections = new Dictionary<string, PluginSection>(StringComparer.OrdinalIgnoreCase);
            this.ReadFile();
        }

        // Methods
        public void ReadFile()
        {
            try
            {
                this.rawData = Helper.ReadTextFile(this.fileName);
            }
            catch (Exception e)
            {
                Console.WriteLine("[ERR]\n{0}", e.ToString());
            }

            // Parse Plugin's section
            this.ParseSection();
        }

        private void ParseSection()
        {
            try
            {
                // Match sections using regex
                MatchCollection matches = Regex.Matches(rawData, @"^\[(.+)\]\r?$", RegexOptions.Multiline);

                // Make instances of sections
                // sections = new PluginSection[matches.Count];
                for (int i = 0; i < matches.Count; i++)
                {
                    int secDataOffset = 0;
                    int secDataLen = 0;

                    secDataOffset = matches[i].Index + matches[i].Length + 1;
                    if (i + 1 < matches.Count)
                        secDataLen = matches[i + 1].Index - secDataOffset;
                    else
                        secDataLen = rawData.Length - secDataOffset;

                    string secName = matches[i].Value.Substring(1, matches[i].Length - 3);
                    string secData = rawData.Substring(secDataOffset, secDataLen);
                    string[] secCodes = secData.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                    sections[secName] = new PluginSection(secName, secData, secCodes);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("[ERR]\n{0}", e.ToString());
            }
        }

        public void Debug()
        {
            try
            {
                Console.WriteLine("FileName = " + this.fileName);
                foreach (var section in sections)
                {
                    Console.WriteLine(section.Value.SecName);
                    Console.WriteLine(section.Value.SecData);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("[ERR]\n{0}", e.ToString());
            }

        }
    }

    public class PluginSection
    {
        // Fields
        private string secName;
        public string SecName
        {
            get
            {
                return secName;
            }
        }
        private string secData;
        public string SecData
        {
            get
            {
                return secData;
            }
        }
        private string[] secCodes;
        public string[] SecCodes
        {
            get { return secCodes; }
        }

        // Constructor
        public PluginSection(string sectionName, string sectionData, string[] sectionCodes)
        {
            this.secName = sectionName;
            this.secData = sectionData;
            this.secCodes = sectionCodes;
        }
    }

    public class PluginSectionNotFoundException : Exception
    {
        public PluginSectionNotFoundException() { }
        public PluginSectionNotFoundException(string message) : base(message) { }
        public PluginSectionNotFoundException(string message, Exception inner) : base(message, inner) { }
    }
}

