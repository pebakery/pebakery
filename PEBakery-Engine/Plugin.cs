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

    public class PluginParseException : Exception
    {
        public PluginParseException() { }
        public PluginParseException(string message) : base(message) { }
        public PluginParseException(string message, Exception inner) : base(message, inner) { }
    }

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
        public string PluginName
        {
            get { return mainInfo["Title"]; }
        }
        private StringDictionary mainInfo;
        public StringDictionary MainInfo
        {
            get { return mainInfo; }
        }
        private Dictionary<string, PluginSection> sections;
        public Dictionary<string, PluginSection> Sections
        {
            get { return sections; }
        }

        // Constructor
        public Plugin(string fileName)
        {
            this.fileName = fileName;
            this.sections = new Dictionary<string, PluginSection>(StringComparer.OrdinalIgnoreCase);
            this.mainInfo = new StringDictionary(StringComparer.OrdinalIgnoreCase);
            this.ReadFile();
        }

        // Methods
        public void ReadFile()
        {
            try
            {
                // Read and parse plugin file
                ParseSection(Helper.ReadTextFile(this.fileName));
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
            mainInfo = Helper.ParseIniStyle(sections["Main"].Lines);
            if (Path.GetFileName(fileName) == "Macro_Library.script")
            {
                if (!(mainInfo.ContainsKey("Title") && mainInfo.ContainsKey("Description") && mainInfo.ContainsKey("Level")))
                {
                    Console.Write(mainInfo["Description"]);
                    throw new PluginParseException(fileName + " is invalid, check [Main] Section");
                }
            }
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

        private SectionStyle DetectSectionStyle(PluginSection section)
        {
            SectionStyle style;
            if (string.Equals(section.Name, "Main", StringComparison.OrdinalIgnoreCase))
                style = SectionStyle.Ini;
            else if (string.Equals(section.Name, "Variables", StringComparison.OrdinalIgnoreCase))
                style = SectionStyle.Variables;
            else if (string.Equals(section.Name, "EncodedFolders", StringComparison.OrdinalIgnoreCase))
                style = SectionStyle.AttachFolderList;
            else if (string.Equals(section.Name, "AuthorEncoded", StringComparison.OrdinalIgnoreCase)
                || string.Equals(section.Name, "InterfaceEncoded", StringComparison.OrdinalIgnoreCase))
                style = SectionStyle.AttachFileList;
            else if (IsSectionEncodedFolders(section))
                style = SectionStyle.AttachFileList;
            else if (string.Compare(section.Name, 0, "EncodedFile-", 0, 11, StringComparison.OrdinalIgnoreCase) == 0)
                style = SectionStyle.AttachEncode;
            else
                style = SectionStyle.Code;
            section.Style = style;
            return style;
        }
    }

    public enum SectionStyle
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
        private SectionStyle style;
        public SectionStyle Style
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
            this.style = SectionStyle.None;
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

