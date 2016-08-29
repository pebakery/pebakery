using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections;

namespace PEBakery_Engine
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
            set
            {
                // Path.GetInvalidPathChars
                if (File.Exists(value))
                    fileName = value;
                else
                    Console.WriteLine("Invalid Path");
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
                FileStream fs = null;
                StreamReader sr = null;
                char[] buffer = null;

                fs = new FileStream(this.fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
                sr = new StreamReader(fs, true);
                buffer = new char[fs.Length];
                sr.Read(buffer, 0, buffer.Length);
                this.rawData = new string(buffer);

                sr.Close();
                fs.Close();
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
                MatchCollection matches = Regex.Matches(rawData, @"^\[(.)+\]\r?$", RegexOptions.Multiline);

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
                // Console.WriteLine("Data :\n" + this.rawData);
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
            set
            {
                sectionName = value;
            }
        }

        private string sectionData;
        public string SectionData
        {
            get
            {
                return sectionData;
            }
            set
            {
                sectionData = value;
            }
        }


        private int index;
        public int Index
        {
            get
            {
                return index;
            }
            set
            {
                index = value;
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
        public PluginSectionNotFoundException()
        {
        }

        public PluginSectionNotFoundException(string message)
            : base(message)
        {
        }

        public PluginSectionNotFoundException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}




/*
    public static Encoding DetectTextEncoding(string fileName)
        {
            byte[] bom = new byte[4];
            FileStream fs = null;

            try
            {
                fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
                fs.Read(bom, 0, bom.Length);
                fs.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine("The process failed: {0}", e.ToString());
            }

            if (bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
                return Encoding.UTF8;
            else if (bom[0] == 0xFF && bom[1] == 0xF)
                return Encoding.Unicode;
            else if (bom[0] == 0xFE && bom[1] == 0xFF)
                return Encoding.BigEndianUnicode;
            return Encoding.Default;
        }
*/
