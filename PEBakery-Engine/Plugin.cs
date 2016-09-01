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
        private PluginSection[] sections;

        // Constructor
        public Plugin(string fileName)
        {
            this.fileName = fileName;
            this.rawData = null;
            this.sections = null;
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
                sections = new PluginSection[matches.Count];
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
                    sections[i] = new PluginSection(secName, secData, i);
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
                foreach (PluginSection section in sections)
                {
                    Console.WriteLine(section.Index);
                    Console.WriteLine(section.SectionName);
                    Console.WriteLine(section.SectionData);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("[ERR]\n{0}", e.ToString());
            }

        }

        public PluginSection FindSection(string sectionName)
        {
            foreach (PluginSection section in this.sections)
            {
                if (string.Equals(section.SectionName, sectionName, StringComparison.OrdinalIgnoreCase))
                    return section;
            }
            throw new PluginSectionNotFoundException();
        }


    }

    public class PluginSection
    {
        // Fields
        private string sectionName;
        public string SectionName
        {
            get
            {
                return sectionName;
            }
        }

        private string sectionData;
        public string SectionData
        {
            get
            {
                return sectionData;
            }
        }


        private int index;
        public int Index
        {
            get
            {
                return index;
            }
        }

        // Constructor
        public PluginSection(string sectionName, string sectionData, int index)
        {
            this.sectionName = sectionName;
            this.sectionData = sectionData;
            this.index = index;
        }
    }

    public class PluginSectionNotFoundException : Exception
    {
        public PluginSectionNotFoundException() { }
        public PluginSectionNotFoundException(string message) : base(message) { }
        public PluginSectionNotFoundException(string message, Exception inner) : base(message, inner) { }
    }
}

