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
using PEBakery.Core.Commands;

namespace PEBakery.Core
{
    using StringDictionary = Dictionary<string, string>;
    using SectionDictionary = Dictionary<string, PluginSection>;

    #region Plugin
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
                    return sections["Main"].GetIniDict();
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
            get
            {
                if (type == PluginType.Link && linkLoaded)
                    return link.Selected;
                else
                    return selected;
            }
            set
            {
                if (selected != value)
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
                }
            }
        }

        public Plugin(PluginType type, string fullPath, Project project, string projectRoot, int? level, bool ignoreMain)
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

                        if (sections["Main"].IniDict.ContainsKey("Link") == false)
                        {
                            throw new PluginParseException($"Invalid link path in plugin {fullPath}");
                        }
                    }
                    break;
                case PluginType.Plugin:
                    {
                        sections = ParsePlugin();
                        InspectTypeOfUninspectedCodeSection();
                        if (!ignoreMain)
                        {
                            CheckMainSection(PluginType.Plugin);
                            // Mandatory Entry
                            this.title = sections["Main"].IniDict["Title"];
                            this.description = sections["Main"].IniDict["Description"];
                            if (level == null)
                            {
                                if (sections["Main"].IniDict.ContainsKey("Level"))
                                {
                                    if (!int.TryParse(sections["Main"].IniDict["Level"], out this.level))
                                        this.level = 0;
                                }
                                else
                                {
                                    this.level = 0;
                                }

                            }
                            else
                            {
                                this.level = (int)level;
                            }

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
                        else
                        {
                            this.title = Path.GetFileName(fullPath);
                            this.description = string.Empty;
                            this.level = 0;
                        }

                        
                    }
                    break;
                default:
                    Debug.Assert(false); // Internal Error
                    break;
            }
        }

        // Methods
        public SectionDictionary ParsePlugin()
        {
            SectionDictionary dict = new SectionDictionary(StringComparer.OrdinalIgnoreCase);

            Encoding encoding = FileHelper.DetectTextEncoding(fullPath);
            // using (StreamReader reader = new StreamReader(fullPath, encoding, true, Ini.BigBufSize))
            using (StreamReader reader = new StreamReader(fullPath, encoding))
            {
                string line;
                string currentSection = string.Empty; // -1 == empty, 0, 1, ... == index value of sections array
                bool inSection = false;
                bool loadSection = false;
                SectionType type = SectionType.None;
                List<string> lines = new List<string>();
                while ((line = reader.ReadLine()) != null)
                { // Read text line by line
                    line = line.Trim();
                    if (line.StartsWith("[", StringComparison.Ordinal) &&
                        line.EndsWith("]", StringComparison.Ordinal))
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
            }

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
                    encodedFolders = Ini.ParseIniSection(fullPath, "EncodedFolders");
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
                    type = DetectTypeOfUninspectedSection(sectionName);
                else
                    type = SectionType.Uninspected;
            }
            return type;
        }

        private void InspectTypeOfUninspectedCodeSection()
        {
            // SectionDictionary
            foreach (var key in sections.Keys)
            {
                if (sections[key].Type == SectionType.Uninspected)
                    sections[key].Type = DetectTypeOfUninspectedSection(sections[key].SectionName);
            }
        }

        private SectionType DetectTypeOfUninspectedSection(string sectionName)
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
                case SectionType.Uninspected:
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
                    sectionKeys = Ini.ParseIniLinesIniStyle(lines);
                    return new PluginSection(this, sectionName, type, sectionKeys); // SectionDataType.IniDict
                // case SectionType.Variables:
                //     sectionKeys = Ini.ParseIniLinesVarStyle(lines);
                //     return new PluginSection(this, sectionName, type, sectionKeys); // SectionDataType.IniDict
                case SectionType.Variables:
                case SectionType.Code:
                case SectionType.AttachFolderList:
                case SectionType.Uninspected:
                case SectionType.Interface:
                    return new PluginSection(this, sectionName, type, lines); // SectionDataType.Lines
                case SectionType.AttachEncode: // do not load now
                    return new PluginSection(this, sectionName, type, false);
                default:
                    throw new PluginParseException("Invalid SectionType " + type.ToString());
            }
        }

        private void CheckMainSection(PluginType type)
        {
            if (sections.ContainsKey("Main") == false)
                throw new PluginParseException($"[{fullPath}] is invalid, please Add [Main] Section");

            bool fail = true;
            if (sections["Main"].DataType == SectionDataType.IniDict)
            {
                if (type == PluginType.Plugin)
                {
                    /*
                    if (sections["Main"].IniDict.ContainsKey("Title")
                        && sections["Main"].IniDict.ContainsKey("Description")
                        && sections["Main"].IniDict.ContainsKey("Level"))
                        fail = false;
                        */

                    if (sections["Main"].IniDict.ContainsKey("Title")
                        && sections["Main"].IniDict.ContainsKey("Description"))
                        fail = false;
                }
                else if (type == PluginType.Link)
                {
                    if (sections["Main"].IniDict.ContainsKey("Link"))
                        fail = false;
                }
            }

            if (fail)
                throw new PluginParseException($"[{fullPath}] is invalid, check [Main] Section");
        }

        public static List<string> GetDisablePluginPaths(Plugin p)
        {
            if (p.Type == PluginType.Directory || p.Level == Project.MainLevel)
                return null;

            if (p.MainInfo.ContainsKey("Disable") == false)
                return null;

            p.Project.Variables.ResetVariables(VarsType.Local);
            p.Project.Variables.LoadDefaultPluginVariables(p);

            string rawLine = p.MainInfo["Disable"];

            // Check if rawCode is Empty
            if (rawLine.Equals(string.Empty))
                return null;

            // Splice with spaces
            List<string> rawPaths = rawLine.Split(',').ToList();

            // Check doublequote's occurence - must be 2n
            if (FileHelper.CountStringOccurrences(rawLine, "\"") % 2 == 1)
                throw new ExecuteException("Doublequote's number should be even number");

            // Parse Arguments
            List<string> paths = new List<string>();
            try
            {
                string remainder = rawLine;
                while (remainder != null)
                {
                    Tuple<string, string> tuple = CodeParser.GetNextArgument(remainder);
                    paths.Add(tuple.Item1);
                    remainder = tuple.Item2;
                }
            }
            catch (InvalidCommandException e) { throw new InvalidCommandException(e.Message, rawLine); }

            for (int i = 0; i < paths.Count; i++)
                paths[i] = p.Project.Variables.Expand(paths[i]);
            return paths;
        }

        public override string ToString()
        {
            if (type == PluginType.Link)
                return sections["Main"].IniDict["Link"];
            else
                return this.title;
        }

        public override bool Equals(object obj)
        {
            if (obj is Plugin p)
            {
                if (this.FullPath.Equals(p.FullPath, StringComparison.OrdinalIgnoreCase))
                    return true;
                else
                    return false;
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return fullPath.GetHashCode() ^ shortPath.GetHashCode();
        }
    }
    #endregion


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
        // Uninspected == It can be Code or AttachFileList
        None = 0,
        Main = 10,
        Ini = 20,
        Variables = 30,
        Uninspected = 40,
        Code = 50,
        Interface = 60,
        AttachFolderList = 100,
        AttachFileList = 101,
        AttachEncode = 102,
    }

    public enum SectionDataType
    {
        // First, only IniDict and Lines can be set.
        // They only have [IniDict] or [Lines] as data.
        IniDict = 1, // Dictionary<string, string>
        Lines = 2, // List<string>
    }

    public enum SectionDataConverted
    {
        // SectionDataType.Lines can be converted to SectionDataConverted.Codes and SectionDataConverted.Interfaces
        // They have [Lines] & [Codes], or [Lines] & [Interfaces] as data.
        None = 0,
        Codes = 1, // List<Command>
        Interfaces = 2, // List<UICommand>
    }
    #endregion

    #region PluginSection
    [Serializable]
    public class PluginSection
    {
        // Common Fields
        private Plugin plugin;
        private string sectionName;
        private SectionType type;
        private SectionDataType dataType;
        [NonSerialized]
        private SectionDataConverted convDataType = SectionDataConverted.None;
        private bool loaded;

        public Plugin Plugin { get => plugin; }
        public string SectionName { get => sectionName; }
        public SectionType Type { get => type; set => type = value; }
        public SectionDataType DataType { get => dataType; set => dataType = value; }
        public SectionDataConverted ConvertedType { get => convDataType; }
        public bool Loaded { get => loaded; }

        // Logs
        private List<LogInfo> logInfos = new List<LogInfo>();
        public List<LogInfo> LogInfos
        {
            get
            { // Call .ToList to get logInfo's copy 
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
        [NonSerialized]
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
        [NonSerialized]
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

        public SectionDataType SelectDataType(SectionType type)
        {
            switch (type)
            {
                // Ini-Style
                case SectionType.Main:
                case SectionType.Ini:
                case SectionType.AttachFileList:
                    return SectionDataType.IniDict;
                case SectionType.Variables: // Because of Local Macros, cannot set to IniDict
                case SectionType.Interface:
                case SectionType.Code:
                case SectionType.AttachFolderList:
                case SectionType.Uninspected:
                case SectionType.AttachEncode:
                    return SectionDataType.Lines;
                default:
                    throw new InternalException($"Invalid SectionType {type}");
            }
        }

        public void Load()
        {
            if (loaded == false)
            {
                switch (dataType)
                {
                    case SectionDataType.IniDict:
                        iniDict = Ini.ParseIniSectionToDict(plugin.FullPath, SectionName);
                        break;
                    case SectionDataType.Lines:
                        {
                            lines = Ini.ParseIniSection(plugin.FullPath, sectionName);
                            if (convDataType == SectionDataConverted.Codes)
                            {
                                SectionAddress addr = new SectionAddress(plugin, this);
                                codes = CodeParser.ParseRawLines(lines, addr, out List<LogInfo> logList);
                                logInfos.AddRange(logList);
                            }
                            else if (convDataType == SectionDataConverted.Interfaces)
                            {
                                SectionAddress addr = new SectionAddress(plugin, this);
                                uiCodes = UIParser.ParseRawLines(lines, addr, out List<LogInfo> logList);
                                logInfos.AddRange(logList);
                            }
                        }
                        break;
                    default:
                        throw new InternalException($"Invalid SectionType {type}");
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
                        if (convDataType == SectionDataConverted.Codes)
                            codes = null;
                        else if (convDataType == SectionDataConverted.Interfaces)
                            uiCodes = null;
                        break;
                    default:
                        throw new InternalException($"Invalid SectionType {type}");
                }
                loaded = false;
            }
        }

        public void ConvertLineToCodeSection(List<string> lines)
        {
            if (type == SectionType.Code && dataType == SectionDataType.Lines)
            {
                SectionAddress addr = new SectionAddress(plugin, this);
                codes = CodeParser.ParseRawLines(lines, addr, out List<LogInfo> logList);
                logInfos.AddRange(logList);

                convDataType = SectionDataConverted.Codes;
            }
            else
            {
                throw new InternalException($"Section [{sectionName}] is not a Line section");
            }
        }

        public void ConvertLineToUICodeSection(List<string> lines)
        {
            if ((type == SectionType.Interface || type == SectionType.Code) &&
                dataType == SectionDataType.Lines)
            {
                SectionAddress addr = new SectionAddress(plugin, this);
                uiCodes = UIParser.ParseRawLines(lines, addr, out List<LogInfo> logList);
                logInfos.AddRange(logList);

                convDataType = SectionDataConverted.Interfaces;
            }
            else
            {
                throw new InternalException($"Section [{sectionName}] is not a Line section");
            }
        }
 
        public StringDictionary GetIniDict()
        {
            if (dataType == SectionDataType.IniDict)
                return IniDict; // this.IniDict for Load()
            else
                throw new InternalException("GetIniDict must be used with [SectionDataType.IniDict]");
        }

        public List<string> GetLines()
        {
            if (dataType == SectionDataType.Lines)
                return Lines; // this.Lines for Load()
            else
                throw new InternalException("GetLines must be used with [SectionDataType.Lines]");
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
                    return Ini.ParseIniSection(plugin.FullPath, sectionName);
            }
            else
            {
                throw new InternalException("GetLinesOnce must be used with [SectionDataType.Lines]");
            }
        }

        public List<CodeCommand> GetCodes()
        {
            if (dataType == SectionDataType.Lines &&
                convDataType == SectionDataConverted.Codes)
                return Codes; // this.Codes for Load()
            else
                throw new InternalException("GetCodes must be used with SectionDataType.Codes");
        }

        /// <summary>
        /// Convert to Codes if SectionDataType is Lines
        /// </summary>
        /// <param name="convert"></param>
        /// <returns></returns>
        public List<CodeCommand> GetCodes(bool convert)
        {
            if (dataType == SectionDataType.Lines &&
                convDataType == SectionDataConverted.Codes)
            {
                return Codes; // this.Codes for Load()
            }
            else if (convert && dataType == SectionDataType.Lines)
            {
                ConvertLineToCodeSection(Lines); // this.Lines for Load()
                return codes;
            }
            else
            {
                throw new InternalException("GetCodes must be used with SectionDataType.Codes");
            }
        }

        public List<UICommand> GetUICodes()
        {
            if (dataType == SectionDataType.Lines &&
                convDataType == SectionDataConverted.Interfaces)
            {
                return UICodes; // this.UICodes for Load()
            }
            else
            {
                throw new InternalException("GetUICodes must be used with SectionDataType.Interfaces");
            }
        }

        /// <summary>
        /// Convert to Interfaces if SectionDataType is Lines
        /// </summary>
        /// <param name="convert"></param>
        /// <returns></returns>
        public List<UICommand> GetUICodes(bool convert)
        {
            if (dataType == SectionDataType.Lines &&
                convDataType == SectionDataConverted.Interfaces)
            {
                return UICodes; // this.UICodes for Load()
            }
            else if (convert && dataType == SectionDataType.Lines)
            { // SectionDataType.Codes for custom interface section
                ConvertLineToUICodeSection(Lines); // this.Lines for Load()
                return uiCodes;
            }
            else
            {
                throw new InternalException("GetUICodes must be used with SectionDataType.Interfaces");
            }
        }
    }
    #endregion
}

