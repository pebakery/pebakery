/*
    Copyright (C) 2016-2017 Hajin Jang
    Licensed under GPL 3.0
 
    PEBakery is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections;
using PEBakery.Helper;
using PEBakery.Exceptions;
using PEBakery.Lib;
using System.Diagnostics;

namespace PEBakery.Core
{
    using StringDictionary = Dictionary<string, string>;
    using SectionDictionary = Dictionary<string, PluginSection>;

    [Serializable]
    public class Plugin
    {
        // Fields
        private string fullPath;
        private string shortPath;
        private bool fullyParsed;

        private SectionDictionary sections;

        private PluginType type;
        [NonSerialized]
        private Project project;
        [NonSerialized]
        private Plugin link;
        [NonSerialized]
        private bool linkLoaded;
        private string title;
        private string author;
        private string description;
        private int version;
        private int level;
        private SelectedState selected;
        private bool mandatory;

        // Properties
        public string FullPath
        {
            get
            {
                if (type == PluginType.Link && linkLoaded)
                    return link.FullPath;
                else
                    return fullPath;
            }
        }
        public string DirectFullPath { get => fullPath; }
        public string ShortPath { get => shortPath; }
        public SectionDictionary Sections
        {
            get
            {
                if (type == PluginType.Link && linkLoaded)
                    return link.Sections;
                else
                    return sections;
            }
        }
        public StringDictionary MainInfo
        {
            get
            {
                if (type == PluginType.Link && linkLoaded)
                    return link.MainInfo;
                else
                    return sections["Main"].Get() as StringDictionary;
            }
        }
        
        public PluginType Type { get => type; }
        public Plugin Link { get => link; set => link = value; }
        public bool LinkLoaded { get => linkLoaded; set => linkLoaded = value; }
        public Project Project
        {
            get
            {
                if (type == PluginType.Link && linkLoaded)
                    return link.Project;
                else
                    return project;
            }
            set
            {
                project = value;
            }
        }
        public string Title
        {
            get
            {
                if (type == PluginType.Link && linkLoaded)
                    return link.Title;
                else
                    return title;
            }
        }
        public string Author
        {
            get
            {
                if (type == PluginType.Link && linkLoaded)
                    return link.Author;
                else
                    return author;
            }
        }
        public string Description
        {
            get
            {
                if (type == PluginType.Link && linkLoaded)
                    return link.Description;
                else
                    return description;
            }
        }
        public int Version
        {
            get
            {
                if (type == PluginType.Link && linkLoaded)
                    return link.Version;
                else
                    return version;
            }
        }
        public int Level
        {
            get
            {
                if (type == PluginType.Link && linkLoaded)
                    return link.Level;
                else
                    return level;
            }
        }
        public bool Mandatory
        {
            get
            {
                if (type == PluginType.Link && linkLoaded)
                    return link.Mandatory;
                else
                    return mandatory;
            }
        }
        public SelectedState Selected
        {
            get => selected;
            set
            {
                selected = value;

                string str = value.ToString();
                if (type == PluginType.Plugin)
                {
                    sections["Main"].IniDict["Selected"] = str;
                    Ini.SetKey(fullPath, new IniKey("Main", "Selected", str));
                }
                else if (type == PluginType.Link && linkLoaded)
                {
                    sections["Main"].IniDict["Selected"] = str;
                    Ini.SetKey(fullPath, new IniKey("Main", "Selected", str));
                    link.sections["Main"].IniDict["Selected"] = str;
                    Ini.SetKey(link.FullPath, new IniKey("Main", "Selected", str));
                    link.selected = value;
                }

                // TODO : Plugin Disable
                // Need Engine's Variable system to be implemented
            }
        }

        public Plugin(PluginType type, string fullPath, Project project, string projectRoot, int? level)
        {
            this.fullPath = fullPath;
            this.shortPath = fullPath.Remove(0, projectRoot.Length + 1);
            this.type = type;
            this.project = project;
            this.linkLoaded = false;

            switch (type)
            {
                case PluginType.Directory:
                    {
                        if (level == null)
                            level = 0;
                        List<string> dirInfo = new List<string>();
                        sections = new SectionDictionary(StringComparer.OrdinalIgnoreCase)
                        {
                            ["Main"] = CreatePluginSectionInstance(fullPath, "Main", SectionType.Main, new List<string>())
                        };

                        // Mandatory Entries
                        sections["Main"].IniDict["Title"] = this.title = Path.GetFileName(fullPath);
                        sections["Main"].IniDict["Description"] = this.description = $"Directory {this.title}";
                        this.level = (int)level;
                        sections["Main"].IniDict["Level"] = this.level.ToString();

                        // Optional Entries
                        this.author = string.Empty;
                        this.version = 0;
                        this.selected = SelectedState.None; // This Value should be adjusted later!
                        this.mandatory = false;
                        this.link = null;
                    }
                    break;
                case PluginType.Link:
                    { // Parse only [Main] Section
                        sections = ParsePlugin();
                        CheckMainSection(PluginType.Link);

                        if (sections["Main"].IniDict.ContainsKey("Link"))
                        {
                            /*
                            string linkPath = Path.Combine(baseDir, sections["Main"].IniDict["Link"]);
                            if (File.Exists(linkPath) == false) // Invalid link
                                throw new PluginParseException($"Invalid link path in plugin {fullPath}");

                            try
                            {
                                string ext = Path.GetExtension(linkPath);
                                if (string.Equals(ext, ".link", StringComparison.OrdinalIgnoreCase))
                                    this.link = new Plugin(PluginType.Link, Path.Combine(baseDir, linkPath), project, projectRoot, baseDir, null);
                                else
                                    this.link = new Plugin(PluginType.Plugin, Path.Combine(baseDir, linkPath), project, projectRoot, baseDir, null);
                            }
                            catch (Exception)
                            {
                                throw new PluginParseException($"Linked plugin {linkPath} is invalid");
                            }
                            */
                        }
                        else
                        {
                            throw new PluginParseException($"Invalid link path in plugin {fullPath}");
                        }
                    }
                    break;
                case PluginType.Plugin:
                    {
                        sections = ParsePlugin();
                        InspectTypeOfUninspectedCodeSection();
                        CheckMainSection(PluginType.Plugin);

                        // Mandatory Entry
                        this.title = sections["Main"].IniDict["Title"];
                        this.description = sections["Main"].IniDict["Description"];
                        if (level == null)
                        {
                            if (int.TryParse(sections["Main"].IniDict["Level"], out this.level) == false)
                                this.level = 0;
                        }
                        else
                            this.level = (int) level;

                        // Optional Entry
                        if (sections["Main"].IniDict.ContainsKey("Author"))
                            this.author = sections["Main"].IniDict["Author"];
                        if (sections["Main"].IniDict.ContainsKey("Version"))
                            this.version = int.Parse(sections["Main"].IniDict["Version"]);
                        if (sections["Main"].IniDict.ContainsKey("Selected"))
                        {
                            string src = sections["Main"].IniDict["Selected"];
                            if (string.Equals(src, "True", StringComparison.OrdinalIgnoreCase))
                                this.selected = SelectedState.True;
                            else if (string.Equals(src, "None", StringComparison.OrdinalIgnoreCase))
                                this.selected = SelectedState.None;
                            else
                                this.selected = SelectedState.False;
                        }
                        if (sections["Main"].IniDict.ContainsKey("Mandatory"))
                        {
                            if (string.Equals(sections["Main"].IniDict["Mandatory"], "True", StringComparison.OrdinalIgnoreCase))
                                this.mandatory = true;
                            else
                                this.mandatory = false;
                        }
                        this.link = null;
                    }
                    break;
                default:
                    Debug.Assert(false); // Not implemented
                    break;
            }
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
                    encodedFolders = Ini.ParseSectionToStringList(fullPath, "EncodedFolders");
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
                type = SectionType.Interface;
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
                    sectionKeys = Ini.ParseLinesIniStyle(lines);
                    return new PluginSection(this, sectionName, type, sectionKeys);
                case SectionType.Variables:
                    sectionKeys = Ini.ParseLinesVarStyle(lines);
                    return new PluginSection(this, sectionName, type, sectionKeys);
                case SectionType.Code:
                case SectionType.AttachFolderList:
                case SectionType.UninspectedCode:
                case SectionType.Interface:
                    return new PluginSection(this, sectionName, type, lines);
                case SectionType.AttachEncode: // do not load now
                    return new PluginSection(this, sectionName, type, false);
                default:
                    throw new PluginParseException("Invalid SectionType " + type.ToString());
            }
        }

        private void CheckMainSection(PluginType type)
        {
            if (!sections.ContainsKey("Main"))
            {
                throw new PluginParseException(fullPath + " is invalid, please Add [Main] Section");
            }
            bool fail = true;
            if (sections["Main"].DataType == SectionDataType.IniDict)
            {
                if (type == PluginType.Plugin)
                {
                    if (sections["Main"].IniDict.ContainsKey("Title")
                        && sections["Main"].IniDict.ContainsKey("Description")
                        && sections["Main"].IniDict.ContainsKey("Level"))
                        fail = false;
                }
                else if (type == PluginType.Link)
                {
                    if (sections["Main"].IniDict.ContainsKey("Link"))
                        fail = false;
                }
            }

            if (fail)
                throw new PluginParseException(fullPath + " is invalid, check [Main] Section");
        }

        public override string ToString()
        {
            return this.title;
        }
    }

    #region Enums
    public enum PluginType
    {
        Plugin, Link, Directory
    }

    public enum SelectedState
    {
        True, False, None
    }

    public enum SectionType
    {
        // UninspectedCode == It can be Code or AttachFileList
        None = 0, Main, Interface, CompiledInterface, Ini, Variables, Code, CompiledCode, UninspectedCode, AttachFolderList, AttachFileList, AttachEncode
    }

    public enum SectionDataType
    {
        IniDict, // Dictionary<string, string>
        Lines, // List<string>
        Codes, // List<Command>
        Interfaces // List<UIDirective>
    }
    #endregion

    #region PluginSection
    [Serializable]
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

        // Logs
        private Queue<LogInfo> logInfos;
        public List<LogInfo> LogInfos
        {
            get
            {
                List<LogInfo> list = logInfos.ToList();
                logInfos.Clear();
                return list;
            }
        }

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
        private List<CodeCommand> codes;
        public List<CodeCommand> Codes
        {
            get
            {
                if (!loaded)
                    Load();
                return codes;
            }
        }

        // Interface-Type Section
        private List<UICommand> uiCodes;
        public List<UICommand> UICodes
        {
            get
            {
                if (!loaded)
                    Load();
                return uiCodes;
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
                    case SectionDataType.Interfaces:
                        return uiCodes.Count;
                    default:
                        throw new InternalErrorException($"Invalid SectionType {type}");
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
            this.logInfos = new Queue<LogInfo>();
            this.loaded = false;
        }

        public PluginSection(Plugin plugin, string sectionName, SectionType type, bool load)
        {
            this.plugin = plugin;
            this.sectionName = sectionName;
            this.type = type;
            this.dataType = SelectDataType(type);
            this.logInfos = new Queue<LogInfo>();
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
            this.logInfos = new Queue<LogInfo>();
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
            this.logInfos = new Queue<LogInfo>();
            this.loaded = true;
            this.iniDict = iniDict;
        }

        public PluginSection(Plugin plugin, string sectionName, SectionType type, List<string> lines)
        {
            this.plugin = plugin;
            this.sectionName = sectionName;
            this.type = type;
            this.dataType = SectionDataType.Lines;
            this.logInfos = new Queue<LogInfo>();
            this.loaded = true;
            this.lines = lines;
        }

        public PluginSection(Plugin plugin, string sectionName, SectionType type, List<CodeCommand> codes)
        {
            this.plugin = plugin;
            this.sectionName = sectionName;
            this.type = type;
            this.dataType = SectionDataType.Codes;
            this.logInfos = new Queue<LogInfo>();
            this.loaded = true;
            this.codes = codes;
        }

        public PluginSection(Plugin plugin, string sectionName, SectionType type, List<UICommand> uiCodes)
        {
            this.plugin = plugin;
            this.sectionName = sectionName;
            this.type = type;
            this.dataType = SectionDataType.Interfaces;
            this.logInfos = new Queue<LogInfo>();
            this.loaded = true;
            this.uiCodes = uiCodes;
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
                case SectionType.Interface:
                case SectionType.Code:
                case SectionType.AttachFolderList:
                case SectionType.UninspectedCode:
                case SectionType.AttachEncode:
                    return SectionDataType.Lines;
                case SectionType.CompiledCode:
                    return SectionDataType.Codes;
                case SectionType.CompiledInterface:
                    return SectionDataType.Interfaces;
                default:
                    throw new InternalErrorException($"Invalid SectionType {type}");
            }
        }

        public void Load()
        {
            if (!loaded)
            {
                switch (dataType)
                {
                    case SectionDataType.IniDict:
                        iniDict = Ini.ParseSectionToDict(PluginPath, SectionName);
                        break;
                    case SectionDataType.Lines:
                        lines = Ini.ParseSectionToStringList(PluginPath, sectionName);
                        break;
                    case SectionDataType.Codes:
                        {
                            codes = CodeParser.ParseRawLines(Ini.ParseSectionToStringList(PluginPath, sectionName), new SectionAddress(plugin, this), out List<LogInfo> logList);
                            foreach (LogInfo log in logList)
                                logInfos.Enqueue(log);
                        }
                        break;
                    case SectionDataType.Interfaces:
                        {
                            uiCodes = UIParser.ParseRawLines(Ini.ParseSectionToStringList(PluginPath, sectionName), new SectionAddress(plugin, this), out List<LogInfo> logList);
                            foreach (LogInfo log in logList)
                                logInfos.Enqueue(log);
                        }
                        break;
                    default:
                        throw new InternalErrorException($"Invalid SectionType {type}");
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
                    case SectionDataType.Interfaces:
                        uiCodes = null;
                        break;
                    default:
                        throw new InternalErrorException($"Invalid SectionType {type}");
                }
                loaded = false;
            }
        }

        public void ConvertLineToCodeSection(List<string> lines)
        {
            if (type == SectionType.Code && dataType == SectionDataType.Lines)
            {
                codes = CodeParser.ParseRawLines(lines, new SectionAddress(plugin, this), out List<LogInfo> logList);
                foreach (LogInfo log in logList)
                    logInfos.Enqueue(log);

                lines = null;
                type = SectionType.CompiledCode;
                dataType = SectionDataType.Codes;
            }
            else
                throw new InternalErrorException($"Section [{sectionName}] is not a Line section");
        }

        public void ConvertLineToUICodeSection(List<string> lines)
        {
            if (type == SectionType.Interface && dataType == SectionDataType.Lines)
            {
                uiCodes = UIParser.ParseRawLines(lines, new SectionAddress(plugin, this), out List<LogInfo> logList);
                foreach (LogInfo log in logList)
                    logInfos.Enqueue(log);

                lines = null;
                type = SectionType.CompiledInterface;
                dataType = SectionDataType.Interfaces;
            }
            else
                throw new InternalErrorException($"Section [{sectionName}] is not a Line section");
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
                case SectionDataType.Interfaces:
                    return uiCodes;
                default:
                    throw new InternalErrorException($"Invalid SectionType {type}");
            }
        }

        public StringDictionary GetIniDict()
        {
            if (dataType == SectionDataType.IniDict)
                return IniDict; // this.IniDict for Load()
            else
                throw new InternalErrorException("GetIniDict must be used with SectionDataType.IniDict");
        }

        public List<string> GetLines()
        {
            if (dataType == SectionDataType.Lines)
                return Lines; // this.Lines for Load()
            else
                throw new InternalErrorException("GetLines must be used with SectionDataType.Lines");
        }

        /// <summary>
        /// Get Lines without permanently loaded, saving memory
        /// </summary>
        /// <returns></returns>
        public List<string> GetLinesOnce()
        {
            if (dataType == SectionDataType.Lines)
            {
                if (loaded)
                    return lines;
                else
                    return Ini.ParseSectionToStringList(PluginPath, sectionName);
            }
            else
                throw new InternalErrorException("GetLines must be used with SectionDataType.Lines");
        }

        public List<CodeCommand> GetCodes()
        {
            if (dataType == SectionDataType.Codes)
                return Codes; // this.Codes for Load()
            else
                throw new InternalErrorException("GetCodes must be used with SectionDataType.Codes");
        }

        public List<CodeCommand> GetCodes(bool convertIfLine)
        {
            if (dataType == SectionDataType.Codes)
                return Codes; // this.Codes for Load()
            else if (convertIfLine && dataType == SectionDataType.Lines)
            {
                ConvertLineToCodeSection(this.Lines); // this.Lines for Load()
                return codes;
            }
            else
                throw new InternalErrorException("GetCodes must be used with SectionDataType.Codes");
        }

        public List<UICommand> GetUICodes()
        {
            if (dataType == SectionDataType.Interfaces)
                return UICodes; // this.UICodes for Load()
            else
                throw new InternalErrorException("GetUIDirectives must be used with SectionDataType.Interfaces");
        }

        public List<UICommand> GetUICodes(bool convertIfLine)
        {
            if (dataType == SectionDataType.Interfaces)
                return UICodes; // this.UICodes for Load()
            else if (convertIfLine && dataType == SectionDataType.Lines)
            {
                ConvertLineToUICodeSection(this.Lines); // this.Lines for Load()
                return uiCodes;
            }
            else
                throw new InternalErrorException("GetUIDirectives must be used with SectionDataType.Interfaces");
        }
    }
    #endregion
}

