using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;

namespace BakeryEngine
{
    using StringDictionary = Dictionary<string, string>;
    using SectionDictionary = Dictionary<string, PluginSection>;

    public class PluginParseException : Exception
    {
        public PluginParseException() { }
        public PluginParseException(string message) : base(message) { }
        public PluginParseException(string message, Exception inner) : base(message, inner) { }
    }

    public class Plugin
    {
        // Fields, Properties
        private string fullPath;
        public string FullPath
        {
            get
            {
                return fullPath;
            }
        }
        private string shortPath;
        public string ShortPath
        {
            get
            {
                return shortPath;
            }
        }
        private StringDictionary mainInfo;
        public StringDictionary MainInfo
        {
            get { return mainInfo; }
        }
        private SectionDictionary sections;
        public SectionDictionary Sections
        {
            get { return sections; }
        }
        public string PluginName
        {
            get { return mainInfo["Title"]; }
        }

        // Constructor
        public Plugin(string fullPath, string baseDir)
        {
            this.fullPath = fullPath;
            this.shortPath = fullPath.Remove(0, baseDir.Length + 1);
            this.sections = new SectionDictionary(StringComparer.OrdinalIgnoreCase);
            this.mainInfo = new StringDictionary(StringComparer.OrdinalIgnoreCase);
            this.ReadFile();
        }

        // Methods
        public void ReadFile()
        {
            try
            {
                // Read and parse plugin file
                ParseSection(Helper.ReadTextFile(this.fullPath));
                foreach (var kv in sections)
                    DetectSectionStyle(kv.Value);
                ParseMainSection();
            }
            catch (Exception e)
            {
                Console.WriteLine(string.Concat(e.GetType(), ": ", Helper.RemoveLastNewLine(e.Message)));
            }
        }

        private void ParseSection(string rawData)
        {
            // Match sections using regex
            MatchCollection matches = Regex.Matches(rawData, @"^\[(.+)\]\r?$", RegexOptions.Compiled | RegexOptions.Multiline);

            // Make instances of sections
            for (int i = 0; i < matches.Count; i++)
            {
                int secDataOffset = 0;
                int secDataLen = 0;

                secDataOffset = matches[i].Index + matches[i].Length + 1;
                if (i + 1 < matches.Count)
                    secDataLen = matches[i + 1].Index - secDataOffset;
                else
                    secDataLen = rawData.Length - secDataOffset;

                string name = matches[i].Value.Substring(1, matches[i].Length - 3);
                string[] lines = rawData.Substring(secDataOffset, secDataLen).Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                sections[name] = new PluginSection(name, lines);
            }
        }

        private void ParseMainSection()
        {
            if (!sections.ContainsKey("Main"))
                throw new PluginParseException(fullPath + " is invalid, please Add [Main] Section");
            mainInfo = IniFile.ParseLinesIniStyle(sections["Main"].Lines);
            if (!(mainInfo.ContainsKey("Title") && mainInfo.ContainsKey("Description") && mainInfo.ContainsKey("Level")))
                throw new PluginParseException(fullPath + " is invalid, check [Main] Section");
        }

        private bool IsSectionEncodedFolders(PluginSection section)
        {
            bool ret = false;
            if (sections.ContainsKey("EncodedFolders"))
            {
                foreach (string folder in sections["EncodedFolders"].Lines)
                {
                    if (string.Equals(folder, section.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        ret = true;
                        break;
                    }
                }
            }
            return ret;
        }

        private SectionType DetectSectionStyle(PluginSection section)
        {
            SectionType style;
            if (string.Equals(section.Name, "Main", StringComparison.OrdinalIgnoreCase))
                style = SectionType.Ini;
            else if (string.Equals(section.Name, "Variables", StringComparison.OrdinalIgnoreCase))
                style = SectionType.Variables;
            else if (string.Equals(section.Name, "EncodedFolders", StringComparison.OrdinalIgnoreCase))
                style = SectionType.AttachFolderList;
            else if (string.Equals(section.Name, "AuthorEncoded", StringComparison.OrdinalIgnoreCase)
                || string.Equals(section.Name, "InterfaceEncoded", StringComparison.OrdinalIgnoreCase))
                style = SectionType.AttachFileList;
            else if (IsSectionEncodedFolders(section))
                style = SectionType.AttachFileList;
            else if (string.Compare(section.Name, 0, "EncodedFile-", 0, 11, StringComparison.OrdinalIgnoreCase) == 0)
                style = SectionType.AttachEncode;
            else
                style = SectionType.Code;
            section.Style = style;
            return style;
        }
    }

    public enum SectionType
    {
        None = 0, Ini, Variables, Code, AttachFolderList, AttachFileList, AttachEncode
    }

    public class PluginSection
    {
        // Fields
        private string name;
        public string Name
        {
            get
            {
                return name;
            }
        }
        private SectionType style;
        public SectionType Style
        {
            get { return style; }
            set { style = value; }
        }
        private string[] lines;
        public string[] Lines
        {
            get { return lines; }
        }

        // Constructor
        public PluginSection(string name, string[] lines)
        {
            this.name = name;
            this.style = SectionType.None;
            this.lines = lines;
        }
    }

    public class PluginSectionNotFoundException : Exception
    {
        public PluginSectionNotFoundException() { }
        public PluginSectionNotFoundException(string message) : base(message) { }
        public PluginSectionNotFoundException(string message, Exception inner) : base(message, inner) { }
    }
}

