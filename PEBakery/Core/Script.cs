﻿/*
    Copyright (C) 2016-2018 Hajin Jang
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
        private readonly string _realPath;
        private readonly string _treePath;
        private bool _fullyParsed;
        private readonly bool _isMainScript;
        private readonly Dictionary<string, ScriptSection> _sections;
        private readonly ScriptType _type;
        [NonSerialized]
        private Project _project;
        [NonSerialized]
        private Script _link;
        [NonSerialized]
        private bool _linkLoaded;
        private bool _isDirLink;
        private string _dirLinkRoot;
        private readonly string _title = string.Empty;
        private readonly string _author = string.Empty;
        private readonly string _description = string.Empty;
        private readonly int _version;
        private readonly int _level;
        private SelectedState _selected = SelectedState.None;
        private readonly bool _mandatory = false;
        private readonly List<string> _interfaceList = new List<string>();
        #endregion

        #region Properties
        public string RealPath
        {
            get
            {
                if (_type == ScriptType.Link && _linkLoaded)
                    return _link.RealPath;
                else
                    return _realPath;
            }
        }
        public string DirectRealPath => _realPath;
        public string TreePath => _treePath;
        public Dictionary<string, ScriptSection> Sections
        {
            get
            {
                if (_type == ScriptType.Link && _linkLoaded)
                    return _link.Sections;
                else
                    return _sections;
            }
        }
        public Dictionary<string, string> MainInfo
        {
            get
            {
                if (_type == ScriptType.Link && _linkLoaded)
                    return _link.MainInfo;
                else
                {
                    if (_sections.ContainsKey("Main"))
                        return _sections["Main"].GetIniDict();
                    else // Just return empty dictionary
                        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }
            }
        }

        public bool IsMainScript => _isMainScript;
        public ScriptType Type => _type;
        public Script Link { get => _link; set => _link = value; }
        public bool LinkLoaded { get => _linkLoaded; set => _linkLoaded = value; }
        public bool IsDirLink { get => _isDirLink; set => _isDirLink = value; }
        public string DirLinkRoot { get => _dirLinkRoot; set => _dirLinkRoot = value; }
        public Project Project
        {
            get
            {
                if (_type == ScriptType.Link && _linkLoaded)
                    return _link.Project;
                else
                    return _project;
            }
            set => _project = value;
        }
        public string Title
        {
            get
            {
                if (_type == ScriptType.Link && _linkLoaded)
                    return _link.Title;
                else
                    return _title;
            }
        }
        public string Author
        {
            get
            {
                if (_type == ScriptType.Link && _linkLoaded)
                    return _link.Author;
                else
                    return _author;
            }
        }
        public string Description
        {
            get
            {
                if (_type == ScriptType.Link && _linkLoaded)
                    return _link.Description;
                else
                    return _description;
            }
        }
        public int Version
        {
            get
            {
                if (_type == ScriptType.Link && _linkLoaded)
                    return _link.Version;
                else
                    return _version;
            }
        }
        public int Level
        {
            get
            {
                if (_type == ScriptType.Link && _linkLoaded)
                    return _link.Level;
                else
                    return _level;
            }
        }
        public bool Mandatory
        {
            get
            {
                if (_type == ScriptType.Link && _linkLoaded)
                    return _link.Mandatory;
                else
                    return _mandatory;
            }
        }
        public SelectedState Selected
        {
            get => _selected;
            set
            {
                if (_selected != value)
                {
                    _selected = value;
                    string valStr = value.ToString();
                    if (_type != ScriptType.Directory)
                    {
                        if (_sections.ContainsKey("Main"))
                        {
                            _sections["Main"].IniDict["Selected"] = valStr;
                            Ini.SetKey(_realPath, new IniKey("Main", "Selected", valStr));
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

            _realPath = realPath ?? throw new ArgumentNullException(nameof(realPath));
            if (treePath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                _treePath = treePath.Remove(0, projectRoot.Length + 1);
            else
                _treePath = treePath;
            Debug.Assert(_treePath != null);

            _type = type;
            _project = project ?? throw new ArgumentNullException(nameof(project));
            _isMainScript = isMainScript;
            _linkLoaded = false;
            if (dirLinkRoot != null)
            {
                _isDirLink = true;
                _dirLinkRoot = dirLinkRoot;
            }
            else
            {
                _isDirLink = false;
                _dirLinkRoot = null;
            }

            Debug.Assert(!_isDirLink || type != ScriptType.Link);

            switch (type)
            {
                case ScriptType.Directory:
                    {
                        if (level == null)
                            level = 0;
                        _sections = new Dictionary<string, ScriptSection>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["Main"] = CreateScriptSectionInstance("Main", SectionType.Main, new List<string>(), 1)
                        };

                        // Mandatory Entries
                        _sections["Main"].IniDict["Title"] = _title = Path.GetFileName(treePath);
                        _sections["Main"].IniDict["Description"] = _description = $"Directory {_title}";
                        _level = (int)level;
                        _sections["Main"].IniDict["Level"] = _level.ToString();

                        // Optional Entries
                        _author = string.Empty;
                        _version = 0;
                        _selected = SelectedState.None; // This Value should be adjusted later!
                        _mandatory = false;
                        _link = null;
                    }
                    break;
                case ScriptType.Link:
                    { // Parse only [Main] Section
                        _sections = ParseScript();
                        CheckMainSection(ScriptType.Link);
                        ScriptSection mainSection = _sections["Main"];

                        if (mainSection.IniDict.ContainsKey("Link") == false)
                        {
                            throw new ScriptParseException($"Invalid link path in script {realPath}");
                        }

                        if (mainSection.IniDict.ContainsKey("Selected"))
                        {
                            string _value = mainSection.IniDict["Selected"];
                            if (_value.Equals("True", StringComparison.OrdinalIgnoreCase))
                                _selected = SelectedState.True;
                            else if (_value.Equals("False", StringComparison.OrdinalIgnoreCase))
                                _selected = SelectedState.False;
                            else
                                _selected = SelectedState.None;
                        }
                    }
                    break;
                case ScriptType.Script:
                    {
                        _sections = ParseScript();
                        InspectTypeOfUninspectedCodeSection();
                        if (!ignoreMain)
                        {
                            CheckMainSection(ScriptType.Script);
                            ScriptSection mainSection = _sections["Main"];

                            // Mandatory Entry
                            _title = mainSection.IniDict["Title"];
                            if (mainSection.IniDict.ContainsKey("Description"))
                                _description = mainSection.IniDict["Description"];
                            else
                                _description = string.Empty;
                            if (level == null)
                            {
                                if (mainSection.IniDict.ContainsKey("Level"))
                                {
                                    if (!int.TryParse(mainSection.IniDict["Level"], out _level))
                                        _level = 0;
                                }
                                else
                                {
                                    _level = 0;
                                }
                            }
                            else
                            {
                                _level = (int)level;
                            }

                            if (mainSection.IniDict.ContainsKey("Author"))
                                _author = mainSection.IniDict["Author"];
                            if (mainSection.IniDict.ContainsKey("Version"))
                                _version = int.Parse(mainSection.IniDict["Version"]);
                            if (mainSection.IniDict.ContainsKey("Selected"))
                            {
                                string src = mainSection.IniDict["Selected"];
                                if (src.Equals("True", StringComparison.OrdinalIgnoreCase))
                                    _selected = SelectedState.True;
                                else if (src.Equals("False", StringComparison.OrdinalIgnoreCase))
                                    _selected = SelectedState.False;
                                else
                                    _selected = SelectedState.None;
                            }
                            if (mainSection.IniDict.ContainsKey("Mandatory"))
                            {
                                if (mainSection.IniDict["Mandatory"].Equals("True", StringComparison.OrdinalIgnoreCase))
                                    _mandatory = true;
                                else
                                    _mandatory = false;
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
                                            _interfaceList.Add(tuple.Item1);
                                            remainder = tuple.Item2;
                                        }
                                    }
                                    catch (InvalidCommandException) { } // Just Ignore
                                }
                            } // InterfaceList
                            _link = null;
                        }
                        else
                        {
                            _title = Path.GetFileName(realPath);
                            _description = string.Empty;
                            _level = 0;
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

            Encoding encoding = FileHelper.DetectTextEncoding(_realPath);
            using (StreamReader reader = new StreamReader(_realPath, encoding))
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

            _fullyParsed = true;

            return dict;
        }

        private bool IsSectionEncodedFolders(string sectionName)
        {
            List<string> encodedFolders;
            try
            {
                if (_fullyParsed)
                {
                    if (_sections.ContainsKey("EncodedFolders"))
                        encodedFolders = _sections["EncodedFolders"].GetLines();
                    else
                        return false;
                }
                else
                    encodedFolders = Ini.ParseIniSection(_realPath, "EncodedFolders");
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
            foreach (var key in _sections.Keys)
            {
                if (_sections[key].Type == SectionType.Uninspected)
                    _sections[key].Type = DetectTypeOfUninspectedSection(_sections[key].SectionName);
            }
        }

        private SectionType DetectTypeOfUninspectedSection(string sectionName)
        {
            SectionType type;
            if (IsSectionEncodedFolders(sectionName))
                type = SectionType.AttachFileList;
            else if (_interfaceList.FirstOrDefault(x => x.Equals(sectionName, StringComparison.OrdinalIgnoreCase)) != null)
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
            switch (type)
            {
                case SectionType.Main:
                case SectionType.Ini:
                case SectionType.AttachFileList:
                    Dictionary<string, string> sectionKeys = Ini.ParseIniLinesIniStyle(lines);
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
            if (_sections.ContainsKey("Main") == false)
                throw new ScriptParseException($"[{_realPath}] is invalid, please Add [Main] Section");

            bool fail = true;
            if (_sections["Main"].DataType == SectionDataType.IniDict)
            {
                if (type == ScriptType.Script)
                {
                    if (_sections["Main"].IniDict.ContainsKey("Title"))
                        fail = false;
                }
                else if (type == ScriptType.Link)
                {
                    if (_sections["Main"].IniDict.ContainsKey("Link"))
                        fail = false;
                }
            }

            if (fail)
                throw new ScriptParseException($"[{_realPath}] is invalid, check [Main] Section");
        }

        public static string[] GetDisableScriptPaths(Script sc, out List<LogInfo> errorLogs)
        {
            errorLogs = new List<LogInfo>();

            if (sc.Type == ScriptType.Directory || sc._isMainScript)
                return null;

            if (sc.MainInfo.ContainsKey("Disable") == false)
                return null;

            sc.Project.Variables.ResetVariables(VarsType.Local);
            sc.Project.Variables.LoadDefaultScriptVariables(sc);

            string rawLine = sc.MainInfo["Disable"];

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
                    string pPath = sc.Project.Variables.Expand(path);
                    if (pPath.Equals(sc.DirectRealPath, StringComparison.OrdinalIgnoreCase) == false)
                        filteredPaths.Add(sc.Project.Variables.Expand(path));
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
            if (_type == ScriptType.Link)
                return _sections["Main"].IniDict["Link"];
            else
                return _title;
        }

        public override bool Equals(object obj)
        {
            if (obj is Script sc)
                return RealPath.Equals(sc.RealPath, StringComparison.OrdinalIgnoreCase);
            else
                return false;
        }

        public override int GetHashCode()
        {
            return _realPath.GetHashCode() ^ _treePath.GetHashCode();
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
        private readonly Script _script;
        private readonly string _sectionName;
        private SectionType _type;
        private SectionDataType _dataType;
        [NonSerialized]
        private SectionDataConverted _convDataType = SectionDataConverted.None;
        private bool _loaded;
        private readonly int _lineIdx;

        public Script Script => _script;
        public string SectionName => _sectionName;
        public SectionType Type { get => _type; set => _type = value; }
        public SectionDataType DataType { get => _dataType; set => _dataType = value; }
        public SectionDataConverted ConvertedType => _convDataType; 
        public bool Loaded => _loaded;
        public int LineIdx => _lineIdx;

        // Logs
        private readonly List<LogInfo> _logInfos = new List<LogInfo>();
        public List<LogInfo> LogInfos
        {
            get
            { // Call .ToList to get logInfo's copy 
                List<LogInfo> list = _logInfos.ToList();
                _logInfos.Clear();
                return list;
            }
        }

        // Ini-Type Section
        private Dictionary<string, string> _iniDict;
        public Dictionary<string, string> IniDict
        {
            get
            {
                if (!_loaded)
                    Load();
                return _iniDict;
            }
        }

        // RawLine-Type Section
        private List<string> _lines;
        public List<string> Lines
        {
            get
            {
                if (!_loaded)
                    Load();
                return _lines;
            }
        }

        // Code-Type Section
        [NonSerialized]
        private List<CodeCommand> _codes;
        public List<CodeCommand> Codes
        {
            get
            {
                if (!_loaded)
                    Load();
                return _codes;
            }
        }

        // Interface-Type Section
        [NonSerialized]
        private List<UIControl> _uiCtrls;
        public List<UIControl> UICtrls
        {
            get
            {
                if (!_loaded)
                    Load();
                return _uiCtrls;
            }
        }
        #endregion

        #region Constructor
        public ScriptSection(Script script, string sectionName, SectionType type)
        {
            _script = script;
            _sectionName = sectionName;
            _type = type;
            _dataType = SelectDataType(type);
            _loaded = false;
        }

        public ScriptSection(Script script, string sectionName, SectionType type, bool load, int lineIdx)
        {
            _script = script;
            _sectionName = sectionName;
            _type = type;
            _dataType = SelectDataType(type);
            _loaded = false;
            _lineIdx = lineIdx;
            if (load)
                Load();
        }

        public ScriptSection(Script script, string sectionName, SectionType type, SectionDataType dataType, bool load, int lineIdx)
        {
            _script = script;
            _sectionName = sectionName;
            _type = type;
            _dataType = dataType;
            _loaded = false;
            _lineIdx = lineIdx;
            if (load)
                Load();
        }

        public ScriptSection(Script script, string sectionName, SectionType type, Dictionary<string, string> iniDict, int lineIdx)
        {
            _script = script;
            _sectionName = sectionName;
            _type = type;
            _dataType = SectionDataType.IniDict;
            _loaded = true;
            _iniDict = iniDict;
            _lineIdx = lineIdx;
        }

        public ScriptSection(Script script, string sectionName, SectionType type, List<string> lines, int lineIdx)
        {
            _script = script;
            _sectionName = sectionName;
            _type = type;
            _dataType = SectionDataType.Lines;
            _loaded = true;
            _lines = lines;
            _lineIdx = lineIdx;
        }
        #endregion

        #region Equals, GetHashCode
        public override bool Equals(object obj)
        {
            ScriptSection section = obj as ScriptSection;
            return Equals(section);
        }

        public bool Equals(ScriptSection section)
        {
            if (section == null) throw new ArgumentNullException(nameof(section));

            return _script.Equals(section.Script) && _sectionName.Equals(section.SectionName, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            return _script.GetHashCode() ^ _sectionName.GetHashCode();
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
            if (_loaded == false)
            {
                switch (_dataType)
                {
                    case SectionDataType.IniDict:
                        _iniDict = Ini.ParseIniSectionToDict(_script.RealPath, SectionName);
                        break;
                    case SectionDataType.Lines:
                        {
                            _lines = Ini.ParseIniSection(_script.RealPath, _sectionName);
                            if (_convDataType == SectionDataConverted.Codes)
                            {
                                SectionAddress addr = new SectionAddress(_script, this);
                                _codes = CodeParser.ParseStatements(_lines, addr, out List<LogInfo> logList);
                                _logInfos.AddRange(logList);
                            }
                            else if (_convDataType == SectionDataConverted.Interfaces)
                            {
                                SectionAddress addr = new SectionAddress(_script, this);
                                _uiCtrls = UIParser.ParseRawLines(_lines, addr, out List<LogInfo> logList);
                                _logInfos.AddRange(logList);
                            }
                        }
                        break;
                    default:
                        throw new InternalException($"Invalid SectionType {_type}");
                }
                _loaded = true;
            }
        }

        public void Unload()
        {
            if (_loaded)
            {
                switch (_dataType)
                {
                    case SectionDataType.IniDict:
                        _iniDict = null;
                        break;
                    case SectionDataType.Lines:
                        _lines = null;
                        if (_convDataType == SectionDataConverted.Codes)
                            _codes = null;
                        else if (_convDataType == SectionDataConverted.Interfaces)
                            _uiCtrls = null;
                        break;
                    default:
                        throw new InternalException($"Invalid SectionType {_type}");
                }
                _loaded = false;
            }
        }

        public void ConvertLineToCodeSection(List<string> lines)
        {
            if (_type == SectionType.Code && _dataType == SectionDataType.Lines)
            {
                SectionAddress addr = new SectionAddress(_script, this);
                _codes = CodeParser.ParseStatements(lines, addr, out List<LogInfo> logList);
                _logInfos.AddRange(logList);

                _convDataType = SectionDataConverted.Codes;
            }
            else
            {
                throw new InternalException($"Section [{_sectionName}] is not a Line section");
            }
        }

        public void ConvertLineToUICtrlSection(List<string> lines)
        {
            if ((_type == SectionType.Interface || _type == SectionType.Code) &&
                _dataType == SectionDataType.Lines)
            {
                SectionAddress addr = new SectionAddress(_script, this);
                _uiCtrls = UIParser.ParseRawLines(lines, addr, out List<LogInfo> logList);
                _logInfos.AddRange(logList);

                _convDataType = SectionDataConverted.Interfaces;
            }
            else
            {
                throw new InternalException($"Section [{_sectionName}] is not a Line section");
            }
        }
 
        public Dictionary<string, string> GetIniDict()
        {
            if (_dataType == SectionDataType.IniDict)
                return IniDict; // IniDict for Load()
            else
                throw new InternalException("GetIniDict must be used with [SectionDataType.IniDict]");
        }

        public List<string> GetLines()
        {
            if (_dataType == SectionDataType.Lines)
                return Lines; // Lines for Load()
            else
                throw new InternalException("GetLines must be used with [SectionDataType.Lines]");
        }

        /// <summary>
        /// Get Lines without permanently loaded, saving memory
        /// </summary>
        /// <returns></returns>
        public List<string> GetLinesOnce()
        {
            if (_dataType == SectionDataType.Lines)
            {
                if (_loaded)
                    return _lines;
                else
                    return Ini.ParseIniSection(_script.RealPath, _sectionName);
            }
            else
            {
                throw new InternalException("GetLinesOnce must be used with [SectionDataType.Lines]");
            }
        }

        public List<CodeCommand> GetCodes()
        {
            if (_dataType == SectionDataType.Lines &&
                _convDataType == SectionDataConverted.Codes)
                return Codes; // Codes for Load()
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
            if (_dataType == SectionDataType.Lines &&
                _convDataType == SectionDataConverted.Codes)
            {
                return Codes; // Codes for Load()
            }
            else if (convert && _dataType == SectionDataType.Lines)
            {
                ConvertLineToCodeSection(Lines); // Lines for Load()
                return _codes;
            }
            else
            {
                throw new InternalException("GetCodes must be used with SectionDataType.Codes");
            }
        }

        public List<CodeCommand> GetCodesForce(bool convert)
        {
            if (_dataType == SectionDataType.Lines &&
                _convDataType == SectionDataConverted.Codes)
            {
                return Codes; // Codes for Load()
            }
            else if (_dataType == SectionDataType.Lines)
            {
                ConvertLineToCodeSection(Lines); // Lines for Load()
                return _codes;
            }
            else
            {
                throw new InternalException("GetCodes must be used with SectionDataType.Codes");
            }
        }

        public List<UIControl> GetUICtrls()
        {
            if (_dataType == SectionDataType.Lines &&
                _convDataType == SectionDataConverted.Interfaces)
            {
                return UICtrls; // UICtrls for Load()
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
            if (_dataType == SectionDataType.Lines &&
                _convDataType == SectionDataConverted.Interfaces)
            {
                return UICtrls; // UICtrls for Load()
            }
            else if (convert && _dataType == SectionDataType.Lines)
            { // SectionDataType.Codes for custom interface section
                ConvertLineToUICtrlSection(Lines); // Lines for Load()
                return _uiCtrls;
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

