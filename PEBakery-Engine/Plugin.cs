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
    public class PluginNewSection
    {
        // Common Fields
        protected string pluginPath;
        protected string sectionName;
        protected SectionType type;
        protected SectionDataType dataType;
        protected bool loaded;

        public string PluginPath { get { return pluginPath; } }
        public string SectionName { get { return sectionName; } }
        public SectionType Type { get { return type; } set { type = value; } }
        protected SectionDataType DataType { get { return dataType; } set { dataType = value; } }
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
        private string[] lines;
        public string[] Lines
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
        public List<BakeryCommand> Codes { get { return codes; } }

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
                        return lines.Length;
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
        public PluginNewSection(string pluginPath, string sectionName, SectionType type)
        {
            this.pluginPath = pluginPath;
            this.sectionName = sectionName;
            this.type = type;
        }

        public void NewIniSection()
        {

        }

        public void NewLineSection()
        {

        }

        public void NewCodesSection()
        {

        }

        public SectionDataType SelectDataType()
        {
            switch (type)
            {
                // Ini-Style
                case SectionType.Main:
                case SectionType.Ini:
                case SectionType.AttachFileList:
                case SectionType.Variables:
                case SectionType.AttachEncode:
                    return SectionDataType.IniDict;
                case SectionType.Code:
                case SectionType.AttachFolderList:
                case SectionType.UninspectedCode:
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
                        lines = IniFile.ParseSectionToStrings(pluginPath, sectionName);
                        break;
                    case SectionDataType.Codes:
                        throw new InternalUnknownException($"SectionDataType.Codes must be always loaded");
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
                        throw new InternalUnknownException($"SectionDataType.Codes must be always loaded");
                    default:
                        throw new InternalUnknownException($"Invalid SectionType {type}");
                }
                loaded = false;
            }
        }

        public object Get()
        {
            return null;
        }
    }

    public class PluginSection
    {
        // Fields
        protected string pluginPath;
        protected string sectionName;
        protected SectionType type;
        protected bool loaded;

        public string PluginPath { get { return pluginPath; } }
        public string SectionName { get { return sectionName; } }
        public SectionType Type { get { return type; } set { type = value; } }
        public bool Loaded { get { return loaded; } }
        public virtual int Count { get { return 0; } }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="pluginPath"></param>
        /// <param name="sectionName"></param>
        /// <param name="type"></param>
        public PluginSection(string pluginPath, string sectionName, SectionType type)
        {
            this.pluginPath = pluginPath;
            this.sectionName = sectionName;
            this.type = type;
        }

        public virtual void Load()
        {
        }

        public virtual void Unload()
        {
        }

        public virtual object Get()
        {
            return null;
        }
    }

    public class PluginIniSection : PluginSection
    {
        // Fields
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
        public override int Count
        {
            get { return iniDict.Count; }
        }

        /// <summary>
        /// Constructor for non-code sections
        /// </summary>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <param name="codes"></param>
        public PluginIniSection(string pluginPath, string sectionName, SectionType type, StringDictionary dict) : base(pluginPath, sectionName, type)
        {
            this.loaded = true;
            this.iniDict = dict;
        }

        /// <summary>
        /// Constructor for non-code sections
        /// </summary>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <param name="codes"></param>
        public PluginIniSection(string pluginPath, string sectionName, SectionType type) : base(pluginPath, sectionName, type)
        {
            this.loaded = false;
            this.iniDict = null;
        }

        public override void Load()
        {
            if (!loaded)
            {
                iniDict = IniFile.ParseSectionToDict(PluginPath, SectionName);
                loaded = true;
            }
        }

        public override void Unload()
        {
            if (loaded)
            {
                iniDict = null; // dict will be deleted at next gc run
                loaded = false;
            }
        }

        public override object Get()
        {
            return IniDict;
        }
    }

    public class PluginLineSection : PluginSection
    {
        // Fields
        private string[] lines;
        public string[] Lines
        {
            get
            {
                if (!loaded)
                    Load();
                return lines;
            }
        }
        public override int Count { get { return lines.Length; } }

        /// <summary>
        /// Constructor for code sections, loaded
        /// </summary>
        /// <param name="pluginPath"></param>
        /// <param name="sectionName"></param>
        /// <param name="codes"></param>
        public PluginLineSection(string pluginPath, string sectionName, string[] codes) : base(pluginPath, sectionName, SectionType.Code)
        {
            this.lines = codes;
            loaded = true;
        }

        /// <summary>
        /// Constructor for code sections, unloaded 
        /// </summary>
        /// <param name="pluginPath"></param>
        /// <param name="sectionName"></param>
        /// <param name="codes"></param>
        public PluginLineSection(string pluginPath, string sectionName) : base(pluginPath, sectionName, SectionType.Code)
        {
            this.lines = null;
            loaded = false;
        }

        public override void Load()
        {
            if (!loaded)
            {
                lines = IniFile.ParseSectionToStrings(pluginPath, sectionName);
                loaded = true;
            }
        }

        public override void Unload()
        {
            if (loaded)
            {
                lines = null; // Delete this at next gc run
                loaded = false;
            }
        }

        public override object Get()
        {
            return Lines;
        }
    }

    public class PluginCodeSection : PluginSection
    {
        // Fields
        private List<BakeryCommand> codes;
        public List<BakeryCommand> Codes
        {
            get
            {
                return codes;
            }
        }
        public override int Count { get { return codes.Count; } }

        /// <summary>
        /// Constructor for code sections, compiled
        /// </summary>
        public PluginCodeSection(PluginLineSection section, List<BakeryCommand> codes) : base(section.PluginPath, section.SectionName, SectionType.Code)
        {
            this.loaded = section.Loaded;
            this.codes = codes;
        }

        public override object Get()
        {
            return codes;
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
            try
            {
                sections = ParsePlugin();
                InspectTypeOfUninspectedCodeSection();
                CheckMainSection();
            }
            catch (Exception e)
            {
                Console.WriteLine(string.Concat(e.GetType(), ": ", Helper.RemoveLastNewLine(e.Message)));
            }
        }

        // Methods
        public SectionDictionary ParsePlugin()
        {
            const StringComparison stricmp = StringComparison.OrdinalIgnoreCase;
            SectionDictionary dict = new SectionDictionary(StringComparer.OrdinalIgnoreCase);
            StreamReader reader = new StreamReader(new FileStream(fullPath, FileMode.Open, FileAccess.Read), Helper.DetectTextEncoding(fullPath));

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
                if (line.StartsWith("[", stricmp) && line.EndsWith("]", stricmp))
                { // Start of section
                    if (inSection)
                    { // End of section
                        dict[currentSection] = CreatePluginSectionInstance(fullPath, currentSection, type, lines);
                        lines = new List<string>(); // original List<string> will be deleted after GC
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
                        lines = new List<string>(); // original List<string> will be deleted after GC
                    }
                }
            }

            fullyParsed = true;
            reader.Close();
            return dict;
        }

        private bool IsSectionEncodedFolders(string sectionName)
        {
            string[] encodedFolders;
            try
            {
                if (fullyParsed)
                {
                    if (sections.ContainsKey("EncodedFolders"))
                        encodedFolders = sections["EncodedFolders"].Get() as string[];
                    else
                        return false;
                }
                else
                    encodedFolders = IniFile.ParseSectionToStrings(fullPath, "EncodedFolders");
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
            /*
             * OnProcessEntry, OnProcessExit : deprecated, it is not used in WinPESE
             */
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
                    sectionKeys = IniFile.ParseLinesIniStyle(lines.ToArray());
                    return new PluginIniSection(fullPath, sectionName, type, sectionKeys);
                case SectionType.Variables:
                    sectionKeys = IniFile.ParseLinesVarStyle(lines.ToArray());
                    return new PluginIniSection(fullPath, sectionName, type, sectionKeys);
                case SectionType.Code:
                case SectionType.AttachFolderList:
                case SectionType.UninspectedCode:
                    return new PluginLineSection(fullPath, sectionName, lines.ToArray());
                case SectionType.AttachEncode: // do not load now
                    return new PluginIniSection(fullPath, sectionName, type);
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
            if (!((sections["Main"] as PluginIniSection).IniDict.ContainsKey("Title")
                && (sections["Main"] as PluginIniSection).IniDict.ContainsKey("Description")
                && (sections["Main"] as PluginIniSection).IniDict.ContainsKey("Level")))
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

