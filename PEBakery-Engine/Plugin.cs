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
        public PluginParseException() { }script.project
        public PluginParseException(string message) : base(message) { }
        public PluginParseException(string message, Exception inner) : base(message, inner) { }
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
            StreamReader sr = new StreamReader(new FileStream(fullPath, FileMode.Open, FileAccess.Read), Helper.DetectTextEncoding(fullPath));

            // If file is blank
            if (sr.Peek() == -1)
            {
                sr.Close();
                throw new SectionNotFoundException(string.Concat("Unable to find section, file is empty"));
            }

            string line;
            string currentSection = string.Empty; // -1 == empty, 0, 1, ... == index value of sections array
            bool inSection = false;
            bool loadSection = false;
            SectionType type = SectionType.None;
            ArrayList lines = new ArrayList();
            while ((line = sr.ReadLine()) != null)
            { // Read text line by line
                line = line.Trim();
                if (line.StartsWith("[", stricmp) && line.EndsWith("]", stricmp))
                { // Start of section
                    if (inSection)
                    { // End of section
                        dict[currentSection] = CreatePluginSectionInstance(fullPath, currentSection, type, lines);
                        lines = new ArrayList(); // original ArrayList will be deleted after GC
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
            }

            fullyParsed = true;
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
            SectionType type;
            if (string.Equals(sectionName, "Main", StringComparison.OrdinalIgnoreCase))
                type = SectionType.Main;
            else if (string.Equals(sectionName, "Variables", StringComparison.OrdinalIgnoreCase))
                type = SectionType.Variables;
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
        private PluginSection CreatePluginSectionInstance(string fullPath, string sectionName, SectionType type, ArrayList lines)
        {
            StringDictionary sectionKeys;
            switch (type)
            {
                case SectionType.Main:
                case SectionType.Ini:
                case SectionType.AttachFileList:
                    sectionKeys = IniFile.ParseLinesIniStyle(lines.ToArray(typeof(string)) as string[]);
                    return new PluginIniSection(fullPath, sectionName, type, sectionKeys);
                case SectionType.Variables:
                    sectionKeys = IniFile.ParseLinesVarStyle(lines.ToArray(typeof(string)) as string[]);
                    return new PluginIniSection(fullPath, sectionName, type, sectionKeys);
                case SectionType.Code:
                case SectionType.AttachFolderList:
                case SectionType.UninspectedCode:
                    return new PluginRawLineSection(fullPath, sectionName, lines.ToArray(typeof(string)) as string[]);
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
            if (!((sections["Main"] as PluginIniSection).Dict.ContainsKey("Title")
                && (sections["Main"] as PluginIniSection).Dict.ContainsKey("Description")
                && (sections["Main"] as PluginIniSection).Dict.ContainsKey("Level")))
            {
                throw new PluginParseException(fullPath + " is invalid, check [Main] Section");
            }
        }

        
    }

    public enum SectionType
    {
        // UninspectedCode == It can be Code or AttachFileList
        None = 0, Main, Ini, Variables, Code, UninspectedCode, AttachFolderList, AttachFileList, AttachEncode
    }

    public class PluginSection
    {
        // Fields
        protected string pluginPath;
        public string PluginPath
        {
            get { return pluginPath; }
        }
        protected string sectionName;
        public string SectionName
        {
            get
            {
                return sectionName;
            }
        }
        protected SectionType type;
        public SectionType Type
        {
            get { return type; }
            set { type = value; }
        }
        protected bool loaded;
        public bool Loaded
        {
            get { return loaded; }
        }
        public virtual int Count
        {
            get { return 0; }
        }

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
        private StringDictionary dict;
        public StringDictionary Dict
        {
            get
            {
                if (!loaded)
                    Load();
                return dict;
            }
        }
        public override int Count
        {
            get { return dict.Count; }
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
            this.dict = dict;
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
            this.dict = null;
        }

        public override void Load()
        {
            if (!loaded)
            {
                dict = IniFile.ParseSectionToDict(PluginPath, SectionName);
                loaded = true;
            }
        }

        public override void Unload()
        {
            if (loaded)
            {
                dict = null; // dict will be deleted at next gc run
                loaded = false;
            }
        }

        public override object Get()
        {
            return Dict;
        }
    }

    public class PluginRawLineSection : PluginSection
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
        public override int Count
        {
            get { return lines.Length; }
        }

        /// <summary>
        /// Constructor for code sections, loaded
        /// </summary>
        /// <param name="pluginPath"></param>
        /// <param name="sectionName"></param>
        /// <param name="codes"></param>
        public PluginRawLineSection(string pluginPath, string sectionName, string[] codes) : base(pluginPath, sectionName, SectionType.Code)
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
        public PluginRawLineSection(string pluginPath, string sectionName) : base(pluginPath, sectionName, SectionType.Code)
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

    public class PluginSectionNotFoundException : Exception
    {
        public PluginSectionNotFoundException() { }
        public PluginSectionNotFoundException(string message) : base(message) { }
        public PluginSectionNotFoundException(string message, Exception inner) : base(message, inner) { }
    }
}

