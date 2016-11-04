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
    using StringDictionary = Dictionary<string, string>;
    using SectionDictionary = Dictionary<string, PluginSection>;

    public class PluginParseException : Exception
    {
        public PluginParseException() { }
        public PluginParseException(string message) : base(message) { }
        public PluginParseException(string message, Exception inner) : base(message, inner) { }
    }

    // Preparing to use
    public class PluginSection
    {
        // Common Fields
        protected Plugin plugin;
        protected string sectionName;
        protected SectionType type;
        protected SectionDataType dataType;
        protected bool loaded;

        public string PluginPath { get { return plugin.FullPath; } }
        public Plugin Plugin { get { return plugin; } }
        public string SectionName { get { return sectionName; } }
        public SectionType Type { get { return type; } set { type = value; } }
        public SectionDataType DataType { get { return dataType; } set { dataType = value; } }
        public bool Loaded { get { return loaded; } }

        // Ini-Type Section
        private StringDictionary iniDict;
        public StringDictionary IniDict
        {
            get
            {
                if (!loaded)
                    Load();
                return iniDict;
            }
        }
        // RawLine-Type Section
        private List<string> lines;
        public List<string> Lines
        {
            get
            {
                if (!loaded)
                    Load();
                return lines;
            }
        }

        // Code-Type Section
        private List<BakeryCommand> codes;
        public List<BakeryCommand> Codes
        {
            get
            {
                if (!loaded)
                    Load();
                return codes;
            }
        }

        // Count
        public int Count
        {
            get
            {
                switch (dataType)
                {
                    case SectionDataType.IniDict:
                        return iniDict.Count;
                    case SectionDataType.Lines:
                        return lines.Count;
                    case SectionDataType.Codes:
                        return codes.Count;
                    default:
                        throw new InternalUnknownException($"Invalid SectionType {type}");
                }
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="pluginPath"></param>
        /// <param name="sectionName"></param>
        /// <param name="type"></param>
        public PluginSection(Plugin plugin, string sectionName, SectionType type)
        {
            this.plugin = plugin;
            this.sectionName = sectionName;
            this.type = type;
            this.dataType = SelectDataType(type);
            this.loaded = false;
        }

        public PluginSection(Plugin plugin, string sectionName, SectionType type, bool load)
        {
            this.plugin = plugin;
            this.sectionName = sectionName;
            this.type = type;
            this.dataType = SelectDataType(type);
            this.loaded = false;
            if (load)
                Load();
        }

        public PluginSection(Plugin plugin, string sectionName, SectionType type, SectionDataType dataType, bool load)
        {
            this.plugin = plugin;
            this.sectionName = sectionName;
            this.type = type;
            this.dataType = dataType;
            this.loaded = false;
            if (load)
                Load();
        }

        public PluginSection(Plugin plugin, string sectionName, SectionType type, StringDictionary iniDict)
        {
            this.plugin = plugin;
            this.sectionName = sectionName;
            this.type = type;
            this.dataType = SectionDataType.IniDict;
            this.loaded = true;
            this.iniDict = iniDict;
        }

        public PluginSection(Plugin plugin, string sectionName, SectionType type, List<string> lines)
        {
            this.plugin = plugin;
            this.sectionName = sectionName;
            this.type = type;
            this.dataType = SectionDataType.Lines;
            this.loaded = true;
            this.lines = lines;
        }

        public PluginSection(Plugin plugin, string sectionName, SectionType type, List<BakeryCommand> codes)
        {
            this.plugin = plugin;
            this.sectionName = sectionName;
            this.type = type;
            this.dataType = SectionDataType.Codes;
            this.loaded = true;
            this.codes = codes;
        }

        public SectionDataType SelectDataType(SectionType type)
        {
            switch (type)
            {
                // Ini-Style
                case SectionType.Main:
                case SectionType.Ini:
                case SectionType.AttachFileList:
                case SectionType.Variables:
                    return SectionDataType.IniDict;
                case SectionType.Code:
                case SectionType.AttachFolderList:
                case SectionType.UninspectedCode:
                case SectionType.AttachEncode:
                    return SectionDataType.Lines;
                case SectionType.CompiledCode:
                    return SectionDataType.Codes;
                default:
                    throw new InternalUnknownException($"Invalid SectionType {type}");
            }
        }

        public void Load()
        {
            if (!loaded)
            {
                switch (dataType)
                {
                    case SectionDataType.IniDict:
                        iniDict = IniFile.ParseSectionToDict(PluginPath, SectionName);
                        break;
                    case SectionDataType.Lines:
                        lines = IniFile.ParseSectionToStringList(PluginPath, sectionName);
                        break;
                    case SectionDataType.Codes:
                        codes = BakeryCodeParser.ParseRawLines(IniFile.ParseSectionToStringList(PluginPath, sectionName), new SectionAddress(plugin, this));
                        break;
                    default:
                        throw new InternalUnknownException($"Invalid SectionType {type}");
                }
                loaded = true;
            }
        }

        public void Unload()
        {
            if (loaded)
            {
                switch (dataType)
                {
                    case SectionDataType.IniDict:
                        iniDict = null;
                        break;
                    case SectionDataType.Lines:
                        lines = null;
                        break;
                    case SectionDataType.Codes:
                        codes = null;
                        break;
                    default:
                        throw new InternalUnknownException($"Invalid SectionType {type}");
                }
                loaded = false;
            }
        }

        public void ConvertLineToCodeSection(List<string> lines)
        {
            if (dataType == SectionDataType.Lines)
            {
                Load();
                codes = BakeryCodeParser.ParseRawLines(lines, new SectionAddress(plugin, this));
                
                lines = null;
                dataType = SectionDataType.Codes;
            }
            else
                throw new InternalUnknownException($"Section [{sectionName}] is not a Line section");
        }

        public dynamic Get()
        {
            switch (dataType)
            {
                case SectionDataType.IniDict:
                    return iniDict;
                case SectionDataType.Lines:
                    return lines;
                case SectionDataType.Codes:
                    return codes;
                default:
                    throw new InternalUnknownException($"Invalid SectionType {type}");
            }
        }

        public StringDictionary GetIniDict()
        {
            if (dataType == SectionDataType.IniDict)
                return iniDict;
            else
                throw new InternalUnknownException("GetIniDict must be used with SectionDataType.IniDict");
        }

        public List<string> GetLines()
        {
            if (dataType == SectionDataType.Lines)
                return lines;
            else
                throw new InternalUnknownException("GetLines must be used with SectionDataType.Lines");
        }

        public List<BakeryCommand> GetCodes()
        {
            if (dataType == SectionDataType.Codes)
                return codes;
            else
                throw new InternalUnknownException("GetCodes must be used with SectionDataType.Codes");
        }

        public List<BakeryCommand> GetCodes(bool convertIfLine)
        {
            if (dataType == SectionDataType.Codes)
                return codes;
            else if (dataType == SectionDataType.Lines)
            {
                ConvertLineToCodeSection(Lines); // this.Lines for Load()
                return codes;
            }
            else
                throw new InternalUnknownException("GetCodes must be used with SectionDataType.Codes");
        }
    }

    public class PluginSectionNotFoundException : Exception
    {
        public PluginSectionNotFoundException() { }
        public PluginSectionNotFoundException(string message) : base(message) { }
        public PluginSectionNotFoundException(string message, Exception inner) : base(message, inner) { }
    }

    public class Plugin
    {
        // Fields
        private string fullPath;
        private string shortPath;
        private SectionDictionary sections;
        private bool fullyParsed;

        // Properties
        public string FullPath
        {
            get
            {
                return fullPath;
            }
        }
        public string ShortPath
        {
            get
            {
                return shortPath;
            }
        }
        public SectionDictionary Sections
        {
            get { return sections; }
        }
        public StringDictionary MainInfo
        {
            get { return (sections["Main"].Get() as StringDictionary); }
        }
        public string PluginName
        {
            get { return MainInfo["Title"]; }
        }

        // Constructor
        public Plugin(string fullPath, string baseDir)
        {
            this.fullPath = fullPath;
            this.shortPath = fullPath.Remove(0, baseDir.Length + 1);
            sections = ParsePlugin();
            InspectTypeOfUninspectedCodeSection();
            CheckMainSection();
        }

        // Methods
        public SectionDictionary ParsePlugin()
        {
            SectionDictionary dict = new SectionDictionary(StringComparer.OrdinalIgnoreCase);
            StreamReader reader = new StreamReader(new FileStream(fullPath, FileMode.Open, FileAccess.Read), FileHelper.DetectTextEncoding(fullPath));

            // If file is blank
            if (reader.Peek() == -1)
            {
                reader.Close();
                throw new SectionNotFoundException(string.Concat("Unable to find section, file is empty"));
            }

            string line;
            string currentSection = string.Empty; // -1 == empty, 0, 1, ... == index value of sections array
            bool inSection = false;
            bool loadSection = false;
            SectionType type = SectionType.None;
            List<string> lines = new List<string>();
            while ((line = reader.ReadLine()) != null)
            { // Read text line by line
                line = line.Trim();
                if (line.StartsWith("[", StringComparison.OrdinalIgnoreCase) && line.EndsWith("]", StringComparison.OrdinalIgnoreCase))
                { // Start of section
                    if (inSection)
                    { // End of section
                        dict[currentSection] = CreatePluginSectionInstance(fullPath, currentSection, type, lines);
                        lines = new List<string>();
                    }

                    currentSection = line.Substring(1, line.Length - 2);
                    type = DetectTypeOfSection(currentSection, false);
                    if (LoadSectionAtPluginLoadTime(type))
                        loadSection = true;
                    inSection = true;
                }
                else if (inSection && loadSection)
                { // line of section
                    lines.Add(line);
                }

                if (reader.Peek() == -1)
                { // End of .script
                    if (inSection)
                    {
                        dict[currentSection] = CreatePluginSectionInstance(fullPath, currentSection, type, lines);
                        lines = new List<string>();
                    }
                }
            }

            fullyParsed = true;
            reader.Close();
            return dict;
        }

        private bool IsSectionEncodedFolders(string sectionName)
        {
            List<string> encodedFolders;
            try
            {
                if (fullyParsed)
                {
                    if (sections.ContainsKey("EncodedFolders"))
                        encodedFolders = sections["EncodedFolders"].GetLines();
                    else
                        return false;
                }
                else
                    encodedFolders = IniFile.ParseSectionToStringList(fullPath, "EncodedFolders");
            }
            catch (SectionNotFoundException) // No EncodedFolders section, exit
            {
                return false;
            }

            foreach (string folder in encodedFolders)
            {
                if (string.Equals(folder, sectionName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private SectionType DetectTypeOfSection(string sectionName, bool inspectCode)
        {
            // OnProcessEntry, OnProcessExit : deprecated, it is not used in WinPESE
            SectionType type;
            if (string.Equals(sectionName, "Main", StringComparison.OrdinalIgnoreCase))
                type = SectionType.Main;
            else if (string.Equals(sectionName, "Variables", StringComparison.OrdinalIgnoreCase))
                type = SectionType.Variables;
            else if (string.Equals(sectionName, "Interface", StringComparison.OrdinalIgnoreCase))
                type = SectionType.Ini;
            else if (string.Equals(sectionName, "EncodedFolders", StringComparison.OrdinalIgnoreCase))
                type = SectionType.AttachFolderList;
            else if (string.Equals(sectionName, "AuthorEncoded", StringComparison.OrdinalIgnoreCase)
                || string.Equals(sectionName, "InterfaceEncoded", StringComparison.OrdinalIgnoreCase))
                type = SectionType.AttachFileList;
            else if (string.Compare(sectionName, 0, "EncodedFile-", 0, 11, StringComparison.OrdinalIgnoreCase) == 0) // lazy loading
                type = SectionType.AttachEncode;
            else
            {
                if (inspectCode)
                    type = DetectTypeOfUninspectedCodeSection(sectionName);
                else
                    type = SectionType.UninspectedCode;
            }
            return type;
        }

        private void InspectTypeOfUninspectedCodeSection()
        {
            // SectionDictionary
            foreach (var key in sections.Keys)
            {
                if (sections[key].Type == SectionType.UninspectedCode)
                    sections[key].Type = DetectTypeOfUninspectedCodeSection(sections[key].SectionName);
            }
        }

        private SectionType DetectTypeOfUninspectedCodeSection(string sectionName)
        {
            SectionType type;
            if (IsSectionEncodedFolders(sectionName))
                type = SectionType.AttachFileList;
            else // Load it!
                type = SectionType.Code;
            return type;
        }
        private static bool LoadSectionAtPluginLoadTime(SectionType type)
        {
            switch (type)
            {
                case SectionType.Main:
                case SectionType.Variables:
                case SectionType.Code:
                case SectionType.UninspectedCode:
                case SectionType.AttachFolderList:
                case SectionType.AttachFileList:
                    return true;
                default:
                    return false;
            }
        }
        private PluginSection CreatePluginSectionInstance(string fullPath, string sectionName, SectionType type, List<string> lines)
        {
            StringDictionary sectionKeys;
            switch (type)
            {
                case SectionType.Main:
                case SectionType.Ini:
                case SectionType.AttachFileList:
                    sectionKeys = IniFile.ParseLinesIniStyle(lines);
                    return new PluginSection(this, sectionName, type, sectionKeys);
                case SectionType.Variables:
                    sectionKeys = IniFile.ParseLinesVarStyle(lines);
                    return new PluginSection(this, sectionName, type, sectionKeys);
                case SectionType.Code:
                case SectionType.AttachFolderList:
                case SectionType.UninspectedCode:
                    return new PluginSection(this, sectionName, type, lines);
                case SectionType.AttachEncode: // do not load now
                    return new PluginSection(this, sectionName, type, false);
                default:
                    throw new PluginParseException("Invalid SectionType " + type.ToString());
            }
        }
        private void CheckMainSection()
        {
            if (!sections.ContainsKey("Main"))
            {
                throw new PluginParseException(fullPath + " is invalid, please Add [Main] Section");
            }
            if (!(sections["Main"].DataType == SectionDataType.IniDict
                && sections["Main"].IniDict.ContainsKey("Title")
                && sections["Main"].IniDict.ContainsKey("Description")
                && sections["Main"].IniDict.ContainsKey("Level")))
            {
                throw new PluginParseException(fullPath + " is invalid, check [Main] Section");
            }
        }   
    }

    public enum SectionType
    {
        // UninspectedCode == It can be Code or AttachFileList
        None = 0, Main, Ini, Variables, Code, CompiledCode, UninspectedCode, AttachFolderList, AttachFileList, AttachEncode
    }

    public enum SectionDataType
    {
        IniDict, Lines, Codes
    }
}

