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

    Additional permission under GNU GPL version 3 section 7

    If you modify this program, or any covered work, by linking
    or combining it with external libraries, containing parts
    covered by the terms of various license, the licensors of
    this program grant you additional permission to convey the
    resulting work. An external library is a library which is
    not derived from or based on this program. 
*/

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections;
using PEBakery.Helper;
using PEBakery.Exceptions;
using PEBakery.IniLib;
using System.Diagnostics;
using PEBakery.Core.Commands;

namespace PEBakery.Core
{
    #region Script
    [Serializable]
    public class Script
    {
        #region Fields
        private string realPath;
        private string treePath;

        private bool fullyParsed;
        private bool isMainScript;

        private Dictionary<string, ScriptSection> sections;
        private ScriptType type;
        [NonSerialized]
        private Project project;
        [NonSerialized]
        private Script link;
        [NonSerialized]
        private bool linkLoaded;
        private bool isDirLink;
        private string dirLinkRoot;
        private string title = string.Empty;
        private string author = string.Empty;
        private string description = string.Empty;
        private int version;
        private int level;
        private SelectedState selected = SelectedState.None;
        private bool mandatory = false;
        private List<string> interfaceList = new List<string>();
        #endregion

        #region Properties
        public string RealPath
        {
            get
            {
                if (type == ScriptType.Link && linkLoaded)
                    return link.RealPath;
                else
                    return realPath;
            }
        }
        public string DirectRealPath => realPath;
        public string TreePath => treePath;
        public Dictionary<string, ScriptSection> Sections
        {
            get
            {
                if (type == ScriptType.Link && linkLoaded)
                    return link.Sections;
                else
                    return sections;
            }
        }
        public Dictionary<string, string> MainInfo
        {
            get
            {
                if (type == ScriptType.Link && linkLoaded)
                    return link.MainInfo;
                else
                {
                    if (sections.ContainsKey("Main"))
                        return sections["Main"].GetIniDict();
                    else // Just return empty dictionary
                        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }
            }
        }

        public bool IsMainScript => isMainScript;
        public ScriptType Type => type;
        public Script Link { get => link; set => link = value; }
        public bool LinkLoaded { get => linkLoaded; set => linkLoaded = value; }
        public bool IsDirLink { get => isDirLink; set => isDirLink = value; }
        public string DirLinkRoot { get => dirLinkRoot; set => dirLinkRoot = value; }
        public Project Project
        {
            get
            {
                if (type == ScriptType.Link && linkLoaded)
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
                if (type == ScriptType.Link && linkLoaded)
                    return link.Title;
                else
                    return title;
            }
        }
        public string Author
        {
            get
            {
                if (type == ScriptType.Link && linkLoaded)
                    return link.Author;
                else
                    return author;
            }
        }
        public string Description
        {
            get
            {
                if (type == ScriptType.Link && linkLoaded)
                    return link.Description;
                else
                    return description;
            }
        }
        public int Version
        {
            get
            {
                if (type == ScriptType.Link && linkLoaded)
                    return link.Version;
                else
                    return version;
            }
        }
        public int Level
        {
            get
            {
                if (type == ScriptType.Link && linkLoaded)
                    return link.Level;
                else
                    return level;
            }
        }
        public bool Mandatory
        {
            get
            {
                if (type == ScriptType.Link && linkLoaded)
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
                if (selected != value)
                {
                    selected = value;
                    string valStr = value.ToString();
                    if (type != ScriptType.Directory)
                    {
                        if (sections.ContainsKey("Main"))
                        {
                            sections["Main"].IniDict["Selected"] = valStr;
                            Ini.SetKey(realPath, new IniKey("Main", "Selected", valStr));
                        }
                    }
                }
            }
        }
        #endregion

        #region Constructor
        public Script(
            ScriptType type,
            string realPath, string treePath, 
            Project project, string projectRoot,
            int? level, bool isMainScript, bool ignoreMain, string dirLinkRoot)
        {
            if (projectRoot == null) throw new ArgumentNullException(nameof(projectRoot));

            this.realPath = realPath ?? throw new ArgumentNullException(nameof(realPath));
            if (treePath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                this.treePath = treePath.Remove(0, projectRoot.Length + 1);
            else
                this.treePath = treePath;
            Debug.Assert(this.treePath != null);

            this.type = type;
            this.project = project ?? throw new ArgumentNullException(nameof(project));
            this.isMainScript = isMainScript;
            this.linkLoaded = false;
            if (dirLinkRoot != null)
            {
                this.isDirLink = true;
                this.dirLinkRoot = dirLinkRoot;
            }
            else
            {
                this.isDirLink = false;
                this.dirLinkRoot = null;
            }

            Debug.Assert(!isDirLink || type != ScriptType.Link);

            switch (type)
            {
                case ScriptType.Directory:
                    {
                        if (level == null)
                            level = 0;
                        List<string> dirInfo = new List<string>();
                        sections = new Dictionary<string, ScriptSection>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["Main"] = CreateScriptSectionInstance("Main", SectionType.Main, new List<string>(), 1)
                        };

                        // Mandatory Entries
                        sections["Main"].IniDict["Title"] = this.title = Path.GetFileName(treePath);
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
                case ScriptType.Link:
                    { // Parse only [Main] Section
                        sections = ParseScript();
                        CheckMainSection(ScriptType.Link);
                        ScriptSection mainSection = sections["Main"];

                        if (mainSection.IniDict.ContainsKey("Link") == false)
                        {
                            throw new ScriptParseException($"Invalid link path in script {realPath}");
                        }

                        if (mainSection.IniDict.ContainsKey("Selected"))
                        {
                            string _value = mainSection.IniDict["Selected"];
                            if (_value.Equals("True", StringComparison.OrdinalIgnoreCase))
                                this.selected = SelectedState.True;
                            else if (_value.Equals("False", StringComparison.OrdinalIgnoreCase))
                                this.selected = SelectedState.False;
                            else
                                this.selected = SelectedState.None;
                        }
                    }
                    break;
                case ScriptType.Script:
                    {
                        sections = ParseScript();
                        InspectTypeOfUninspectedCodeSection();
                        if (!ignoreMain)
                        {
                            CheckMainSection(ScriptType.Script);
                            ScriptSection mainSection = sections["Main"];

                            // Mandatory Entry
                            this.title = mainSection.IniDict["Title"];
                            if (mainSection.IniDict.ContainsKey("Description"))
                                this.description = mainSection.IniDict["Description"];
                            else
                                this.description = string.Empty;
                            if (level == null)
                            {
                                if (mainSection.IniDict.ContainsKey("Level"))
                                {
                                    if (!int.TryParse(mainSection.IniDict["Level"], out this.level))
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

                            if (mainSection.IniDict.ContainsKey("Author"))
                                this.author = mainSection.IniDict["Author"];
                            if (mainSection.IniDict.ContainsKey("Version"))
                                this.version = int.Parse(mainSection.IniDict["Version"]);
                            if (mainSection.IniDict.ContainsKey("Selected"))
                            {
                                string src = mainSection.IniDict["Selected"];
                                if (src.Equals("True", StringComparison.OrdinalIgnoreCase))
                                    this.selected = SelectedState.True;
                                else if (src.Equals("False", StringComparison.OrdinalIgnoreCase))
                                    this.selected = SelectedState.False;
                                else
                                    this.selected = SelectedState.None;
                            }
                            if (mainSection.IniDict.ContainsKey("Mandatory"))
                            {
                                if (mainSection.IniDict["Mandatory"].Equals("True", StringComparison.OrdinalIgnoreCase))
                                    this.mandatory = true;
                                else
                                    this.mandatory = false;
                            }
                            if (mainSection.IniDict.ContainsKey("InterfaceList"))
                            {
                                string rawList = mainSection.IniDict["InterfaceList"];
                                if (rawList.Equals("True", StringComparison.OrdinalIgnoreCase))
                                {
                                    try
                                    {
                                        string remainder = rawList;
                                        while (remainder != null)
                                        {
                                            Tuple<string, string> tuple = CodeParser.GetNextArgument(remainder);
                                            this.interfaceList.Add(tuple.Item1);
                                            remainder = tuple.Item2;
                                        }
                                    }
                                    catch (InvalidCommandException) { } // Just Ignore
                                }
                            } // InterfaceList
                            this.link = null;
                        }
                        else
                        {
                            this.title = Path.GetFileName(realPath);
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
        #endregion

        #region Methods
        public Dictionary<string, ScriptSection> ParseScript()
        {
            Dictionary<string, ScriptSection> dict = new Dictionary<string, ScriptSection>(StringComparer.OrdinalIgnoreCase);

            Encoding encoding = FileHelper.DetectTextEncoding(realPath);
            using (StreamReader reader = new StreamReader(realPath, encoding))
            {
                int idx = 0;
                int sectionIdx = 0;
                string line;
                string currentSection = string.Empty;
                bool inSection = false;
                bool loadSection = false;
                SectionType type = SectionType.None;
                List<string> lines = new List<string>();
                while ((line = reader.ReadLine()) != null)
                { // Read text line by line
                    idx++;
                    line = line.Trim();
                    if (line.StartsWith("[", StringComparison.Ordinal) &&
                        line.EndsWith("]", StringComparison.Ordinal))
                    { // Start of section
                        if (inSection)
                        { // End of section
                            dict[currentSection] = CreateScriptSectionInstance(currentSection, type, lines, sectionIdx);
                            lines = new List<string>();
                        }

                        sectionIdx = idx;
                        currentSection = line.Substring(1, line.Length - 2);
                        type = DetectTypeOfSection(currentSection, false);
                        if (LoadSectionAtScriptLoadTime(type))
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
                            dict[currentSection] = CreateScriptSectionInstance(currentSection, type, lines, sectionIdx);
                            lines = new List<string>();
                        }
                    }
                }
            }

            fullyParsed = true;

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
                    encodedFolders = Ini.ParseIniSection(realPath, "EncodedFolders");
            }
            catch (SectionNotFoundException) // No EncodedFolders section, exit
            {
                return false;
            }

            foreach (string folder in encodedFolders)
            {
                if (folder.Equals(sectionName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private SectionType DetectTypeOfSection(string sectionName, bool inspectCode)
        {
            // OnProcessEntry, OnProcessExit : deprecated, it is not used in WinPESE
            SectionType type;
            if (sectionName.Equals("Main", StringComparison.OrdinalIgnoreCase))
                type = SectionType.Main;
            else if (sectionName.Equals("Variables", StringComparison.OrdinalIgnoreCase))
                type = SectionType.Variables;
            else if (sectionName.Equals("Interface", StringComparison.OrdinalIgnoreCase))
                type = SectionType.Interface;
            else if (sectionName.Equals("EncodedFolders", StringComparison.OrdinalIgnoreCase))
                type = SectionType.AttachFolderList;
            else if (sectionName.Equals("AuthorEncoded", StringComparison.OrdinalIgnoreCase)
                || sectionName.Equals("InterfaceEncoded", StringComparison.OrdinalIgnoreCase))
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
            // Dictionary<string, ScriptSection>
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
            else if (interfaceList.FirstOrDefault(x => x.Equals(sectionName, StringComparison.OrdinalIgnoreCase)) != null)
                type = SectionType.Interface;
            else // Load it!
                type = SectionType.Code;
            return type;
        }

        private static bool LoadSectionAtScriptLoadTime(SectionType type)
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

        private ScriptSection CreateScriptSectionInstance(string sectionName, SectionType type, List<string> lines, int lineIdx)
        {
            Dictionary<string, string> sectionKeys;
            switch (type)
            {
                case SectionType.Main:
                case SectionType.Ini:
                case SectionType.AttachFileList:
                    sectionKeys = Ini.ParseIniLinesIniStyle(lines);
                    return new ScriptSection(this, sectionName, type, sectionKeys, lineIdx); // SectionDataType.IniDict
                case SectionType.Variables:
                case SectionType.Code:
                case SectionType.AttachFolderList:
                case SectionType.Uninspected:
                case SectionType.Interface:
                    return new ScriptSection(this, sectionName, type, lines, lineIdx); // SectionDataType.Lines
                case SectionType.AttachEncode: // do not load now
                    return new ScriptSection(this, sectionName, type, false, lineIdx);
                default:
                    throw new ScriptParseException($"Invalid SectionType [{type}]");
            }
        }

        private void CheckMainSection(ScriptType type)
        {
            if (sections.ContainsKey("Main") == false)
                throw new ScriptParseException($"[{realPath}] is invalid, please Add [Main] Section");

            bool fail = true;
            if (sections["Main"].DataType == SectionDataType.IniDict)
            {
                if (type == ScriptType.Script)
                {
                    if (sections["Main"].IniDict.ContainsKey("Title"))
                        fail = false;
                }
                else if (type == ScriptType.Link)
                {
                    if (sections["Main"].IniDict.ContainsKey("Link"))
                        fail = false;
                }
            }

            if (fail)
                throw new ScriptParseException($"[{realPath}] is invalid, check [Main] Section");
        }

        public static string[] GetDisableScriptPaths(Script p, out List<LogInfo> errorLogs)
        {
            errorLogs = new List<LogInfo>();

            if (p.Type == ScriptType.Directory || p.isMainScript)
                return null;

            if (p.MainInfo.ContainsKey("Disable") == false)
                return null;

            p.Project.Variables.ResetVariables(VarsType.Local);
            p.Project.Variables.LoadDefaultScriptVariables(p);

            string rawLine = p.MainInfo["Disable"];

            // Check if rawCode is Empty
            if (rawLine.Equals(string.Empty, StringComparison.Ordinal))
                return null;

            // Check doublequote's occurence - must be 2n
            if (StringHelper.CountOccurrences(rawLine, "\"") % 2 == 1)
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

            // Filter out script itself
            List<string> filteredPaths = new List<string>(paths.Count);
            foreach (string path in paths)
            {
                try
                {
                    string pPath = p.Project.Variables.Expand(path);
                    if (pPath.Equals(p.DirectRealPath, StringComparison.OrdinalIgnoreCase) == false)
                        filteredPaths.Add(p.Project.Variables.Expand(path));
                }
                catch (Exception e) { errorLogs.Add(new LogInfo(LogState.Success, Logger.LogExceptionMessage(e))); }
            }

            return filteredPaths.ToArray();
        }

        public ScriptSection GetInterface(out string sectionName)
        {
            sectionName = "Interface";
            if (MainInfo.ContainsKey("Interface"))
                sectionName = MainInfo["Interface"];

            if (Sections.ContainsKey(sectionName))
                return Sections[sectionName];
            else
                return null;
        }
        #endregion

        #region Virtual Methods
        public override string ToString()
        {
            if (type == ScriptType.Link)
                return sections["Main"].IniDict["Link"];
            else
                return this.title;
        }

        public override bool Equals(object obj)
        {
            if (obj is Script p)
            {
                if (this.RealPath.Equals(p.RealPath, StringComparison.OrdinalIgnoreCase))
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
            return realPath.GetHashCode() ^ treePath.GetHashCode();
        }
        #endregion
    }
    #endregion

    #region Enums
    public enum ScriptType
    {
        Script, Link, Directory
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
        Interfaces = 2, // List<UIControl>
    }
    #endregion

    #region ScriptSection
    [Serializable]
    public class ScriptSection
    {
        #region Fields and Properties
        // Common Fields
        private Script script;
        private string sectionName;
        private SectionType type;
        private SectionDataType dataType;
        [NonSerialized]
        private SectionDataConverted convDataType = SectionDataConverted.None;
        private bool loaded;
        private int lineIdx;

        public Script Script => script;
        public string SectionName => sectionName;
        public SectionType Type { get => type; set => type = value; }
        public SectionDataType DataType { get => dataType; set => dataType = value; }
        public SectionDataConverted ConvertedType => convDataType; 
        public bool Loaded => loaded;
        public int LineIdx => lineIdx;

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
        private Dictionary<string, string> iniDict;
        public Dictionary<string, string> IniDict
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
        private List<UIControl> uiCtrls;
        public List<UIControl> UICtrls
        {
            get
            {
                if (!loaded)
                    Load();
                return uiCtrls;
            }
        }
        #endregion

        #region Constructor
        public ScriptSection(Script script, string sectionName, SectionType type)
        {
            this.script = script;
            this.sectionName = sectionName;
            this.type = type;
            this.dataType = SelectDataType(type);
            this.loaded = false;
        }

        public ScriptSection(Script script, string sectionName, SectionType type, bool load, int lineIdx)
        {
            this.script = script;
            this.sectionName = sectionName;
            this.type = type;
            this.dataType = SelectDataType(type);
            this.loaded = false;
            this.lineIdx = lineIdx;
            if (load)
                Load();
        }

        public ScriptSection(Script script, string sectionName, SectionType type, SectionDataType dataType, bool load, int lineIdx)
        {
            this.script = script;
            this.sectionName = sectionName;
            this.type = type;
            this.dataType = dataType;
            this.loaded = false;
            this.lineIdx = lineIdx;
            if (load)
                Load();
        }

        public ScriptSection(Script script, string sectionName, SectionType type, Dictionary<string, string> iniDict, int lineIdx)
        {
            this.script = script;
            this.sectionName = sectionName;
            this.type = type;
            this.dataType = SectionDataType.IniDict;
            this.loaded = true;
            this.iniDict = iniDict;
            this.lineIdx = lineIdx;
        }

        public ScriptSection(Script script, string sectionName, SectionType type, List<string> lines, int lineIdx)
        {
            this.script = script;
            this.sectionName = sectionName;
            this.type = type;
            this.dataType = SectionDataType.Lines;
            this.loaded = true;
            this.lines = lines;
            this.lineIdx = lineIdx;
        }
        #endregion

        #region Equals, GetHashCode
        public override bool Equals(object obj)
        {
            ScriptSection section = obj as ScriptSection;
            return this.Equals(section);
        }

        public bool Equals(ScriptSection section)
        {
            if (section == null) throw new ArgumentNullException(nameof(section));

            if (script.Equals(section.Script) && sectionName.Equals(section.SectionName, StringComparison.OrdinalIgnoreCase))
                return true;
            else
                return false;
        }

        public override int GetHashCode()
        {
            return script.GetHashCode() ^ sectionName.GetHashCode();
        }
        #endregion

        #region Methods
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
                        iniDict = Ini.ParseIniSectionToDict(script.RealPath, SectionName);
                        break;
                    case SectionDataType.Lines:
                        {
                            lines = Ini.ParseIniSection(script.RealPath, sectionName);
                            if (convDataType == SectionDataConverted.Codes)
                            {
                                SectionAddress addr = new SectionAddress(script, this);
                                codes = CodeParser.ParseStatements(lines, addr, out List<LogInfo> logList);
                                logInfos.AddRange(logList);
                            }
                            else if (convDataType == SectionDataConverted.Interfaces)
                            {
                                SectionAddress addr = new SectionAddress(script, this);
                                uiCtrls = UIParser.ParseRawLines(lines, addr, out List<LogInfo> logList);
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
                            uiCtrls = null;
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
                SectionAddress addr = new SectionAddress(script, this);
                codes = CodeParser.ParseStatements(lines, addr, out List<LogInfo> logList);
                logInfos.AddRange(logList);

                convDataType = SectionDataConverted.Codes;
            }
            else
            {
                throw new InternalException($"Section [{sectionName}] is not a Line section");
            }
        }

        public void ConvertLineToUICtrlSection(List<string> lines)
        {
            if ((type == SectionType.Interface || type == SectionType.Code) &&
                dataType == SectionDataType.Lines)
            {
                SectionAddress addr = new SectionAddress(script, this);
                uiCtrls = UIParser.ParseRawLines(lines, addr, out List<LogInfo> logList);
                logInfos.AddRange(logList);

                convDataType = SectionDataConverted.Interfaces;
            }
            else
            {
                throw new InternalException($"Section [{sectionName}] is not a Line section");
            }
        }
 
        public Dictionary<string, string> GetIniDict()
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
                    return Ini.ParseIniSection(script.RealPath, sectionName);
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

        public List<CodeCommand> GetCodesForce(bool convert)
        {
            if (dataType == SectionDataType.Lines &&
                convDataType == SectionDataConverted.Codes)
            {
                return Codes; // this.Codes for Load()
            }
            else if (dataType == SectionDataType.Lines)
            {
                ConvertLineToCodeSection(Lines); // this.Lines for Load()
                return codes;
            }
            else
            {
                throw new InternalException("GetCodes must be used with SectionDataType.Codes");
            }
        }

        public List<UIControl> GetUICtrls()
        {
            if (dataType == SectionDataType.Lines &&
                convDataType == SectionDataConverted.Interfaces)
            {
                return UICtrls; // this.UICtrls for Load()
            }
            else
            {
                throw new InternalException("GetUICtrls must be used with SectionDataType.Interfaces");
            }
        }

        /// <summary>
        /// Convert to Interfaces if SectionDataType is Lines
        /// </summary>
        /// <param name="convert"></param>
        /// <returns></returns>
        public List<UIControl> GetUICtrls(bool convert)
        {
            if (dataType == SectionDataType.Lines &&
                convDataType == SectionDataConverted.Interfaces)
            {
                return UICtrls; // this.UICtrls for Load()
            }
            else if (convert && dataType == SectionDataType.Lines)
            { // SectionDataType.Codes for custom interface section
                ConvertLineToUICtrlSection(Lines); // this.Lines for Load()
                return uiCtrls;
            }
            else
            {
                throw new InternalException("GetUICtrls must be used with SectionDataType.Interfaces");
            }
        }
        #endregion
    }
    #endregion
}

