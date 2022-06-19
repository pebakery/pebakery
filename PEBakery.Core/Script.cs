/*
    Copyright (C) 2016-2022 Hajin Jang
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

using MessagePack;
using PEBakery.Helper;
using PEBakery.Ini;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace PEBakery.Core
{
    #region Internals Documentation
    /*
    RealPath : Where can script found in the disk.
    TreePath : Where should script be put in script tree.
               Can be obtained by stripping "%BaseDir%\Projects\" from RealPath.
               Must be empty for temporary scripts (scripts will not be put in script tree).
    */
    #endregion

    #region Script
    [MessagePackObject]
    public class Script : IEquatable<Script>
    {
        #region Const
        public static class Const
        {
            // %ScriptFile%
            public const string ScriptFile = "%ScriptFile%";
            // MainInfo
            public const string Selected = "Selected";
            public const string Interface = "Interface";
            public const string InterfaceList = "InterfaceList";
            // PathSetting
            public const string PathSetting = "PathSetting";
            public const string SourceDir = "SourceDir";
            public const string TargetDir = "TargetDir";
            public const string IsoFile = "ISOFile";
            // Update
            public const string UpdateType = @"UpdateType";
            public const string Url = @"Url";
        }
        #endregion

        #region Fields
        [IgnoreMember]
        private bool _fullyParsed;
        [Key(0)]
        private readonly ScriptType _type;
        [Key(1)]
        private readonly string _realPath = string.Empty;
        [IgnoreMember]
        private string _treePath = string.Empty;
        [Key(2)]
        private readonly bool _isMainScript;
        [Key(3)]
        private readonly bool _ignoreMain;
        [Key(4)]
        [MessagePackFormatter(typeof(ScriptStringDictionaryFormatter<ScriptSection>))]
        private Dictionary<string, ScriptSection> _sections = new Dictionary<string, ScriptSection>(StringComparer.OrdinalIgnoreCase);
        [Key(5)]
        private readonly List<string> _interfaceList = new List<string>();
        [IgnoreMember]
        private Project? _project;
        [IgnoreMember]
        private Script? _link;
        [IgnoreMember]
        private bool _linkLoaded;
        [IgnoreMember]
        private bool _isDirLink; // No readonly for script caching
        [Key(6)]
        private string _title = string.Empty;
        [Key(7)]
        private string _author = string.Empty;
        [Key(8)]
        private string _description = string.Empty;
        [Key(9)]
        private string _version = "0";
        [Key(10)]
        private VersionEx _parsedVersion = new VersionEx(0);
        [Key(11)]
        private int _level;
        [Key(12)]
        private SelectedState _selected = SelectedState.None;
        [Key(13)]
        private bool _mandatory = false;
        [Key(14)]
        private string? _updateUrl;
        #endregion

        #region Properties
        [IgnoreMember]
        public string FullIdentifier => $"{_level}_{_realPath}_{_treePath}";
        [IgnoreMember]
        public string RealIdentifier => $"{_level}_{_realPath}";
        [IgnoreMember]
        public string TreeIdentifier => $"{_level}_{_treePath}";
        /// <summary>
        /// Full path of the script file. In case of the linked script, this property return the real path of the linked .script file.
        /// </summary>
        [IgnoreMember]
        public string RealPath
        {
            get
            {
                if (_type == ScriptType.Link && _linkLoaded && _link != null)
                    return _link.RealPath;
                return _realPath;
            }
        }
        /// <summary>
        /// 'Direct' full path of the script file. In case of the linked script, this property return the real path of the .link file.
        /// </summary>
        [IgnoreMember]
        public string DirectRealPath => _realPath;
        /// <summary>
        /// The location script should be displayed in project script tree. It determines the build order. TreePath is empty in linked script.
        /// </summary>
        [IgnoreMember]
        public string TreePath => _treePath;
        [IgnoreMember]
        public Dictionary<string, ScriptSection> Sections
        {
            get
            {
                if (_type == ScriptType.Link && _linkLoaded && _link != null)
                    return _link.Sections;
                return _sections;
            }
        }
        [IgnoreMember]
        public Dictionary<string, string> MainInfo
        {
            get
            {
                if (_type == ScriptType.Link && _linkLoaded && _link != null)
                {
                    return _link.MainInfo;
                }

                if (_sections.ContainsKey(ScriptSection.Names.Main))
                {
                    Dictionary<string, string>? iniDict = _sections[ScriptSection.Names.Main].IniDict;
                    if (iniDict != null)
                        return iniDict;
                }

                // Section not found, just return empty dictionary
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }
        [IgnoreMember]
        public bool IsMainScript => _isMainScript;
        [IgnoreMember]
        public bool IgnoreMain => _ignoreMain;
        [IgnoreMember]
        public ScriptType Type => _type;
        [IgnoreMember]
        public Script? Link => _link;
        [IgnoreMember]
        public bool LinkLoaded => _linkLoaded;
        [IgnoreMember]
        public bool IsDirLink => _isDirLink;
        [IgnoreMember]
        public Project Project
        {
            get
            {
                if (_type == ScriptType.Link && _linkLoaded && _link != null)
                    return _link.Project;
                if (_project != null)
                    return _project;
                throw new CriticalErrorException($"{nameof(_project)} is null");
            }
        }
        /// <summary>
        /// Title of the script, displayed in the PEBakery UI.
        /// </summary>
        [IgnoreMember]
        public string Title
        {
            get
            {
                if (_type == ScriptType.Link && _linkLoaded && _link != null)
                    return _link.Title;
                return _title;
            }
        }
        [IgnoreMember]
        public string Author
        {
            get
            {
                if (_type == ScriptType.Link && _linkLoaded && _link != null)
                    return _link.Author;
                return _author;
            }
        }
        [IgnoreMember]
        public string Description
        {
            get
            {
                if (_type == ScriptType.Link && _linkLoaded && _link != null)
                    return _link.Description;
                return _description;
            }
        }
        [IgnoreMember]
        public string RawVersion
        {
            get
            {
                if (_type == ScriptType.Link && _linkLoaded && _link != null)
                    return _link.RawVersion;
                return _version;
            }
        }

        [IgnoreMember]
        public string TidyVersion => StringEscaper.ProcessVersionString(RawVersion) ?? RawVersion;
        [IgnoreMember]
        public VersionEx ParsedVersion => _parsedVersion;
        [IgnoreMember]
        public int Level
        {
            get
            {
                if (_type == ScriptType.Link && _linkLoaded && _link != null)
                    return _link.Level;
                return _level;
            }
        }
        [IgnoreMember]
        public bool Mandatory
        {
            get
            {
                if (_type == ScriptType.Link && _linkLoaded && _link != null)
                    return _link.Mandatory;
                return _mandatory;
            }
        }
        [IgnoreMember]
        public SelectedState Selected
        {
            get => _selected;
            set
            {
                if (_selected == value)
                    return;

                _selected = value;
                string valStr = value.ToString();
                if (_type != ScriptType.Directory && _sections.ContainsKey(ScriptSection.Names.Main))
                {
                    MainInfo[Const.Selected] = valStr;
                    IniReadWriter.WriteKey(_realPath, new IniKey(ScriptSection.Names.Main, Const.Selected, valStr));
                }
            }
        }

        [IgnoreMember]
        public ScriptSection this[string key] => Sections[key];

        [IgnoreMember]
        public string InterfaceSectionName
        {
            get
            {
                // Check if script has custom interface section
                if (MainInfo.ContainsKey(ScriptSection.Names.Interface))
                    return MainInfo[ScriptSection.Names.Interface];
                return ScriptSection.Names.Interface;
            }
        }

        [IgnoreMember]
        public string? UpdateUrl => _updateUrl;
        [IgnoreMember]
        public bool IsUpdateable => _updateUrl != null || Type == ScriptType.Directory;
        #endregion

        #region Constructor
        // For MessagePack
        [SerializationConstructor]
        private Script() { /* Do Nothing */ }

        public Script(ScriptType type, string realPath, string treePath, Project project,
                      int? level, bool isMainScript, bool ignoreMain, bool isDirLink)
        {
            Debug.Assert(realPath != null, $"{nameof(realPath)} is null");
            Debug.Assert(treePath != null, $"{nameof(treePath)} is null");
            Debug.Assert(project != null, $"{nameof(project)} is null");
            Debug.Assert(treePath.Length == 0 || !Path.IsPathRooted(treePath), $"{nameof(treePath)} must be empty or rooted path");

            _realPath = realPath;
            _treePath = treePath;
            _type = type;
            _project = project;
            _isMainScript = isMainScript;
            _linkLoaded = false;
            _isDirLink = isDirLink;
            _ignoreMain = ignoreMain;
            _updateUrl = null;

            if (level is int lv)
                _level = lv;
            else
                _level = 0;

            // Read from file
            switch (Type)
            {
                case ScriptType.Link:
                case ScriptType.Script:
                    _sections = ParseSections();
                    break;
            }

            ReadMainSection(true);
        }
        #endregion

        #region RefreshSections
        /// <summary>
        /// Refresh sections of a script.
        /// While Project.RefreshScript create new script instance, Script.RefreshSections refresh only sections.
        /// </summary>
        public void RefreshSections()
        {
            // If script a directory entry, do nothing.
            if (Type == ScriptType.Directory)
                return;

            // Reload sections
            _sections = ParseSections();

            // Recheck [Main]
            ReadMainSection(true);
        }
        #endregion

        #region ParseSections
        private Dictionary<string, ScriptSection> ParseSections()
        {
            Dictionary<string, ScriptSection> dict = new Dictionary<string, ScriptSection>(StringComparer.OrdinalIgnoreCase);

            Encoding encoding = EncodingHelper.DetectEncoding(_realPath);
            using (StreamReader r = new StreamReader(_realPath, encoding))
            {
                int idx = 0;
                int sectionIdx = 0;
                string currentSection = string.Empty;
                bool inSection = false;
                bool loadSection = false;
                SectionType type = SectionType.None;
                List<string> lines = new List<string>();

                void FinalizeSection()
                {
                    if (inSection)
                    {
                        dict[currentSection] = CreateScriptSectionInstance(currentSection, type, lines.ToArray(), sectionIdx);
                        lines = new List<string>();
                    }
                }

                string? rawLine;
                while ((rawLine = r.ReadLine()) != null)
                { // Read text line by line
                    idx++;
                    ReadOnlySpan<char> line = rawLine.AsSpan().Trim();

                    if (IniReadWriter.IsLineSection(line, out ReadOnlySpan<char> sectionName))
                    { // Start of section
                        FinalizeSection();

                        sectionIdx = idx;
                        currentSection = sectionName.ToString();
                        type = DetectSectionType(currentSection, false);
                        if (ScriptSection.LoadSectionAtScriptLoadTime(type))
                            loadSection = true;
                        inSection = true;
                    }
                    else if (inSection && loadSection)
                    { // line of section
                        lines.Add(line.ToString());
                    }

                    if (r.Peek() == -1) // End of .script
                        FinalizeSection();
                }
            }

            _fullyParsed = true;

            return dict;
        }
        #endregion

        #region Detect Section Type
        private SectionType DetectSectionType(string sectionName, bool inspectCode)
        {
            SectionType type;
            if (sectionName.Equals(ScriptSection.Names.Main, StringComparison.OrdinalIgnoreCase))
                type = SectionType.Main;
            else if (sectionName.Equals(ScriptSection.Names.Variables, StringComparison.OrdinalIgnoreCase))
                type = SectionType.Variables;
            else if (sectionName.Equals(ScriptSection.Names.Interface, StringComparison.OrdinalIgnoreCase))
                type = SectionType.Interface;
            else if (sectionName.Equals(ScriptSection.Names.EncodedFolders, StringComparison.OrdinalIgnoreCase))
                type = SectionType.AttachFolderList;
            else if (sectionName.Equals(ScriptSection.Names.AuthorEncoded, StringComparison.OrdinalIgnoreCase) ||
                     sectionName.Equals(ScriptSection.Names.InterfaceEncoded, StringComparison.OrdinalIgnoreCase))
                type = SectionType.AttachFileList;
            else if (sectionName.StartsWith(ScriptSection.Names.EncodedFileAuthorEncodedPrefix, StringComparison.OrdinalIgnoreCase) ||
                     sectionName.StartsWith(ScriptSection.Names.EncodedFileInterfaceEncodedPrefix, StringComparison.OrdinalIgnoreCase))
                type = SectionType.AttachEncodeNow;
            else if (sectionName.StartsWith(ScriptSection.Names.EncodedFilePrefix, StringComparison.OrdinalIgnoreCase)) // lazy loading
                type = SectionType.AttachEncodeLazy;
            else if (sectionName.Equals(ScriptSection.Names.Process, StringComparison.OrdinalIgnoreCase))
                type = SectionType.Code;
            else if (sectionName.StartsWith('#') && sectionName.EndsWith('#'))
                type = SectionType.Commentary;
            else // Can be a SectionType.Code, SectionType.Interface or SectionType.AttachFileList
                type = inspectCode ? DetectNonInspectedSectionType(sectionName) : SectionType.NotInspected;

            // SyntaxChecker will deep inspect NotInspected or CodeOrUnknown sections later.
            // Until then, treat them as a maybe-Code secction.
            return type;
        }

        private void DetectNonInspectedCodeSectionsType()
        {
            foreach (var kv in _sections.Where(x => x.Value.Type == SectionType.NotInspected))
            {
                ScriptSection section = kv.Value;
                section.Type = DetectNonInspectedSectionType(section.Name);
            }
        }

        private SectionType DetectNonInspectedSectionType(string sectionName)
        {
            SectionType type;
            if (IsSectionEncodedFolders(sectionName))
                type = SectionType.AttachFileList;
            else if (_interfaceList.Any(x => x.Equals(sectionName, StringComparison.OrdinalIgnoreCase)))
                type = SectionType.Interface;
            else // Unknown section
                type = SectionType.CodeOrUnknown;
            return type;
        }

        private bool IsSectionEncodedFolders(string sectionName)
        {
            IReadOnlyList<string> encodedFolders;
            if (_fullyParsed)
            {
                if (_sections.ContainsKey(ScriptSection.Names.EncodedFolders))
                    encodedFolders = _sections[ScriptSection.Names.EncodedFolders].Lines;
                else
                    return false;
            }
            else
            {
                List<string>? encodedFolderLists = IniReadWriter.ParseIniSection(_realPath, ScriptSection.Names.EncodedFolders);
                if (encodedFolderLists == null)  // No EncodedFolders section, exit
                    return false;
                encodedFolders = encodedFolderLists;
            }

            return encodedFolders.Any(folder => folder.Equals(sectionName, StringComparison.OrdinalIgnoreCase));
        }
        #endregion

        #region CreateScriptSectionInstance, RefreshSection
        private ScriptSection CreateScriptSectionInstance(string sectionName, SectionType type, string[] lines, int lineIdx)
        {
            switch (type)
            {
                case SectionType.Main:
                case SectionType.Variables:
                case SectionType.SimpleIni:
                case SectionType.Interface:
                case SectionType.CodeOrUnknown:
                case SectionType.Code:
                case SectionType.Commentary:
                case SectionType.NotInspected:
                case SectionType.AttachFolderList:
                case SectionType.AttachFileList:
                case SectionType.AttachEncodeNow:
                default:
                    return new ScriptSection(this, sectionName, type, lines, lineIdx);
                case SectionType.AttachEncodeLazy: // do not load now
                    return new ScriptSection(this, sectionName, type, false, lineIdx);
            }
        }

        public ScriptSection? RefreshSection(string sectionName)
        {
            ScriptSection section;
            if (Sections.ContainsKey(sectionName))
            { // Force refresh by invalidating section
                section = Sections[sectionName];
                section.Unload();
                section.LoadLines();
            }
            else
            {
                List<string>? lineList = IniReadWriter.ParseRawSection(_realPath, sectionName);
                if (lineList == null)
                    return null;

                SectionType type = DetectSectionType(sectionName, true);
                // lineIdx information is not provided, use with caution!
                section = CreateScriptSectionInstance(sectionName, type, lineList.ToArray(), 0);
                Sections[sectionName] = section;
            }

            return section;
        }
        #endregion

        #region ReadMainSection, CheckMainSection
        private void ReadMainSection(bool autoDetectLevel)
        {
            switch (Type)
            {
                case ScriptType.Directory:
                    {
                        // Mandatory Entries
                        _title = Path.GetFileName(TreePath);
                        _description = $"[Directory] {_title}";

                        // Optional Entries
                        _author = string.Empty;
                        _version = "0";
                        _parsedVersion = new VersionEx(0);
                        _selected = SelectedState.None; // This value should be adjusted later!
                        _mandatory = false;
                        _link = null;

                        string[] mainSectionLines =
                        {
                            $"Title={_title}",
                            $"Description={_description}",
                            $"Level={_level}",
                        };

                        _sections = new Dictionary<string, ScriptSection>(StringComparer.OrdinalIgnoreCase)
                        {
                            [ScriptSection.Names.Main] = CreateScriptSectionInstance(ScriptSection.Names.Main, SectionType.Main, mainSectionLines, 1)
                        };
                    }
                    break;
                case ScriptType.Link:
                    { // Parse only [Main] Section
                        CheckMainSection(ScriptType.Link);
                        ScriptSection mainSection = _sections[ScriptSection.Names.Main];

                        if (!mainSection.IniDict.ContainsKey("Link"))
                            throw new ScriptParseException($"Invalid link path in script {RealPath}");

                        if (mainSection.IniDict.ContainsKey(Const.Selected))
                        {
                            string value = mainSection.IniDict[Const.Selected];
                            if (value.Equals("True", StringComparison.OrdinalIgnoreCase))
                                _selected = SelectedState.True;
                            else if (value.Equals("False", StringComparison.OrdinalIgnoreCase))
                                _selected = SelectedState.False;
                            else
                                _selected = SelectedState.None;
                        }
                    }
                    break;
                case ScriptType.Script:
                    {
                        _interfaceList.Clear();
                        if (!_ignoreMain)
                        {
                            CheckMainSection(ScriptType.Script);
                            ScriptSection mainSection = _sections[ScriptSection.Names.Main];

                            // Mandatory Entry
                            _title = mainSection.IniDict["Title"];
                            if (mainSection.IniDict.ContainsKey("Description"))
                                _description = mainSection.IniDict["Description"];
                            else
                                _description = string.Empty;
                            if (autoDetectLevel)
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

                            if (mainSection.IniDict.ContainsKey("Author"))
                                _author = mainSection.IniDict["Author"];
                            if (mainSection.IniDict.ContainsKey("Version"))
                            {
                                _version = mainSection.IniDict["Version"];
                                _parsedVersion = VersionEx.Parse(_version) ?? new VersionEx(0);
                            }
                            if (mainSection.IniDict.ContainsKey(Const.Selected))
                            {
                                string src = mainSection.IniDict[Const.Selected];
                                if (src.Equals("True", StringComparison.OrdinalIgnoreCase))
                                    _selected = SelectedState.True;
                                else if (src.Equals("False", StringComparison.OrdinalIgnoreCase))
                                    _selected = SelectedState.False;
                                else
                                    _selected = SelectedState.None;
                            }
                            if (mainSection.IniDict.ContainsKey("Mandatory"))
                            {
                                _mandatory = mainSection.IniDict["Mandatory"].Equals("True", StringComparison.OrdinalIgnoreCase);
                            }
                            if (mainSection.IniDict.ContainsKey(Const.InterfaceList))
                            { // Hint for multiple Interface. Useful when supporting multi-interface editing.
                                string rawList = mainSection.IniDict[Const.InterfaceList];
                                try
                                {
                                    string? remainder = rawList;
                                    while (remainder != null)
                                    {
                                        string next;
                                        (next, remainder) = CodeParser.GetNextArgument(remainder);

                                        // Avoid duplicate, only add if section exists
                                        if (_sections.ContainsKey(next))
                                        {
                                            if (!_interfaceList.Contains(next, StringComparer.OrdinalIgnoreCase))
                                                _interfaceList.Add(next);
                                        }
                                        else
                                        {
                                            Global.Logger.SystemWrite(new LogInfo(LogState.Error, $"Section [{next}] does not exist ({rawList}"));
                                        }
                                    }
                                }
                                catch (InvalidCommandException e)
                                {
                                    Global.Logger.SystemWrite(new LogInfo(LogState.Error, e));
                                }
                            } // InterfaceList
                            _link = null;
                            ParseUpdateUrl();
                        }
                        else // _ignoreMain is true
                        {
                            if (_sections.ContainsKey(ScriptSection.Names.Main))
                            { // Try to parse basic information if it is available
                                ScriptSection mainSection = _sections[ScriptSection.Names.Main];
                                Dictionary<string, string> mainIniDict = mainSection.IniDict;

                                _title = SilentDictParser.ParseString(mainIniDict, "Title", Path.GetFileName(RealPath));
                                _description = SilentDictParser.ParseString(mainIniDict, "Description", string.Empty);
                                _level = SilentDictParser.ParseInteger(mainIniDict, "Level", 0, null, null);
                            }
                            else
                            {
                                _title = Path.GetFileName(RealPath);
                                _description = string.Empty;
                                _level = 0;
                            }
                        }

                        // Add default interface section if not added
                        if (!_interfaceList.Contains(InterfaceSectionName, StringComparer.OrdinalIgnoreCase))
                            _interfaceList.Add(InterfaceSectionName);

                        // Inspect previously not inspected sections
                        DetectNonInspectedCodeSectionsType();
                    }
                    break;
                default:
                    Debug.Assert(false, "Internal Error at Script constructor"); // Internal Error
                    break;
            }
        }

        private void CheckMainSection(ScriptType type)
        {
            if (!_sections.ContainsKey(ScriptSection.Names.Main))
                throw new ScriptParseException($"[{_realPath}] is invalid, please Add [Main] Section");
            Dictionary<string, string> mainDict = _sections[ScriptSection.Names.Main].IniDict;

            bool invalid = false;
            switch (type)
            {
                case ScriptType.Script:
                    invalid |= !mainDict.ContainsKey("Title");
                    break;
                case ScriptType.Link:
                    invalid |= !mainDict.ContainsKey("Link");
                    break;
            }

            if (invalid)
                throw new ScriptParseException($"[{_realPath}] is invalid, check [Main] Section");
        }
        #endregion

        #region GetDisableScriptPaths
        public static string[]? GetDisableScriptPaths(Script sc, out List<LogInfo> errorLogs)
        {
            errorLogs = new List<LogInfo>();

            if (sc.Type == ScriptType.Directory || sc._isMainScript)
                return null;

            if (!sc.MainInfo.ContainsKey("Disable"))
                return null;

            if (sc.Project.Variables == null)
                throw new InvalidOperationException($"Variables of Project [{sc.Project.ProjectName}] is not initialized");

            sc.Project.Variables.ResetVariables(VarsType.Local);
            sc.Project.Variables.LoadDefaultScriptVariables(sc);

            string rawLine = sc.MainInfo["Disable"];

            // Check if rawCode is Empty
            if (string.IsNullOrEmpty(rawLine))
                return null;

            // Check double-quote's occurence - must be 2n
            if (StringHelper.CountSubStr(rawLine, "\"") % 2 == 1)
                throw new ExecuteException("Double-quote's number should be even number");

            // Parse Arguments
            List<string> paths = new List<string>();
            try
            {
                string? remainder = rawLine;
                while (remainder != null)
                {
                    string next;
                    (next, remainder) = CodeParser.GetNextArgument(remainder);
                    paths.Add(next);
                }
            }
            catch (InvalidCommandException e)
            {
                LogInfo log = new LogInfo(LogState.Warning, $"{Logger.LogExceptionMessage(e)} ({rawLine})");
                errorLogs.Add(log);
            }

            // Filter out script itself
            List<string> filteredPaths = new List<string>(paths.Count);
            foreach (string path in paths)
            {
                try
                {
                    string pPath = sc.Project.Variables.Expand(path);
                    if (!pPath.Equals(sc.DirectRealPath, StringComparison.OrdinalIgnoreCase))
                        filteredPaths.Add(sc.Project.Variables.Expand(path));
                }
                catch (Exception e)
                {
                    LogInfo log = new LogInfo(LogState.Warning, Logger.LogExceptionMessage(e));
                    errorLogs.Add(log);
                }
            }

            return filteredPaths.ToArray();
        }
        #endregion

        #region GetUpdateUrl
        private enum ScriptUpdateType
        {
            None,
            Project,
            Standalone,
        }

        private static ScriptUpdateType ParseScriptUpdateType(string str)
        {
            if (str.Equals("Project", StringComparison.OrdinalIgnoreCase))
                return ScriptUpdateType.Project;
            if (str.Equals("Standalone", StringComparison.OrdinalIgnoreCase))
                return ScriptUpdateType.Standalone;
            return ScriptUpdateType.None;
        }

        private void ParseUpdateUrl()
        {
            // Check local script's [ScriptUpdate] section
            if (!Sections.ContainsKey(ScriptSection.Names.ScriptUpdate))
                return;

            Dictionary<string, string> scUpdateDict = Sections[ScriptSection.Names.ScriptUpdate].IniDict;

            // Parse ScriptUpdateType
            if (!scUpdateDict.ContainsKey(Const.UpdateType))
                return;

            ScriptUpdateType scType = ParseScriptUpdateType(scUpdateDict[Const.UpdateType]);
            if (scType == ScriptUpdateType.None)
                return;

            // Get ScriptUrl (.script and .meta.json)
            if (!scUpdateDict.ContainsKey(Const.Url))
                return;
            string updateUrl = scUpdateDict[Const.Url].TrimStart('/');

            // ScriptUpdateType is a project
            if (scType == ScriptUpdateType.Project)
            {
                if (Project.UpdateInfo.IsUpdateable)
                    return;

                string baseUrl = Project.UpdateInfo.BaseUrl;
                string channel = Project.UpdateInfo.SelectedChannel;
                updateUrl = $"{baseUrl}/{channel}/{updateUrl}";
            }

            _updateUrl = updateUrl;
        }
        #endregion

        #region User Interface Methods - Get, Apply
        public ScriptSection? GetInterfaceSection(out string sectionName)
        {
            sectionName = ScriptSection.Names.Interface;
            if (MainInfo.ContainsKey(ScriptSection.Names.Interface))
                sectionName = MainInfo[ScriptSection.Names.Interface];

            return Sections.ContainsKey(sectionName) ? Sections[sectionName] : null;
        }

        public List<string> GetInterfaceSectionNames(bool deepScan)
        {
            // Basic scan : Refer only _interfaceList
            List<string> interfaceSections = _interfaceList.ToList();
            if (!deepScan)
                return interfaceSections;

            // Deep scan : Inspect pattern `IniWrite,%ScriptFile%,Main,Interface,<NewInterfaceSection>`
            List<string> visitedSections = new List<string>();

            // Parse interface controls.
            ScriptSection? section = GetInterfaceSection(out string defaultInterfaceSection);
            if (section == null) // No interface section -> return empty list
                return interfaceSections;

            // Add defaultInterfaceSection to visited sections.
            Debug.Assert(!visitedSections.Contains(defaultInterfaceSection, StringComparer.OrdinalIgnoreCase));
            visitedSections.Add(defaultInterfaceSection);

            // Queue for checking sections.
            Queue<ScriptSection> sectionQueue = new Queue<ScriptSection>();

            // Parse interface controls, and get a queue of code sections to inspect.
            // Inspect only default interface section for performance
            // 전제) The multi-interface script should have buttons for switching to another interface, in current interface.
            (List<UIControl> uiCtrls, _) = UIParser.ParseStatements(section.Lines, section);
            foreach (UIControl uiCtrl in uiCtrls)
            {
                string? sectionToRun = null;
                switch (uiCtrl.Type)
                {
                    case UIControlType.CheckBox:
                        {
                            UIInfo_CheckBox info = (UIInfo_CheckBox)uiCtrl.Info;
                            sectionToRun = info.SectionName;
                        }
                        break;
                    case UIControlType.Button:
                        {
                            UIInfo_Button info = (UIInfo_Button)uiCtrl.Info;
                            sectionToRun = info.SectionName;
                        }
                        break;
                    case UIControlType.RadioButton:
                        {
                            UIInfo_RadioButton info = (UIInfo_RadioButton)uiCtrl.Info;
                            sectionToRun = info.SectionName;
                        }
                        break;
                    case UIControlType.RadioGroup:
                        {
                            UIInfo_RadioGroup info = (UIInfo_RadioGroup)uiCtrl.Info;
                            sectionToRun = info.SectionName;
                        }
                        break;
                }

                if (sectionToRun != null && Sections.ContainsKey(sectionToRun))
                    sectionQueue.Enqueue(Sections[sectionToRun]);
            }

            // Run section queue
            while (0 < sectionQueue.Count)
            {
                // Dequeue targetSection
                ScriptSection targetSection = sectionQueue.Dequeue();

                // Check if targetSection was already visited
                if (visitedSections.Contains(targetSection.Name, StringComparer.OrdinalIgnoreCase))
                    continue;
                visitedSections.Add(targetSection.Name);

                // Parse commands (depth 0)
                CodeParser parser = new CodeParser(targetSection, Global.Setting, targetSection.Project.Compat);
                (CodeCommand[] cmds, _) = parser.ParseStatements();

                // Queue for checking commands
                Queue<CodeCommand> commandQueue = new Queue<CodeCommand>(cmds);

                // Run command queue
                while (0 < commandQueue.Count)
                {
                    CodeCommand cmd = commandQueue.Dequeue();
                    switch (cmd.Type)
                    {
                        case CodeType.If:
                            {
                                CodeInfo_If info = (CodeInfo_If)cmd.Info;
                                foreach (CodeCommand nextCmd in info.Link)
                                    commandQueue.Enqueue(nextCmd);
                            }
                            break;
                        case CodeType.Else:
                            {
                                CodeInfo_Else info = (CodeInfo_Else)cmd.Info;
                                foreach (CodeCommand nextCmd in info.Link)
                                    commandQueue.Enqueue(nextCmd);
                            }
                            break;
                        case CodeType.Run:
                        case CodeType.Exec:
                        case CodeType.RunEx:
                            {
                                CodeInfo_RunExec info = (CodeInfo_RunExec)cmd.Info;

                                if (info.ScriptFile.Equals(Const.ScriptFile, StringComparison.OrdinalIgnoreCase) &&
                                    !CodeParser.StringContainsVariable(info.SectionName) &&
                                    Sections.ContainsKey(info.SectionName))
                                    sectionQueue.Enqueue(Sections[info.SectionName]);
                            }
                            break;
                        case CodeType.Loop:
                        case CodeType.LoopLetter:
                        case CodeType.LoopEx:
                        case CodeType.LoopLetterEx:
                            {
                                CodeInfo_Loop info = (CodeInfo_Loop)cmd.Info;

                                if (info.Break)
                                    continue;
                                if (info.ScriptFile == null)
                                    throw new InvalidOperationException($"[{nameof(info.ScriptFile)}] is null");
                                if (info.SectionName == null)
                                    throw new InvalidOperationException($"[{nameof(info.SectionName)}] is null");

                                if (info.ScriptFile.Equals(Const.ScriptFile, StringComparison.OrdinalIgnoreCase) &&
                                    !CodeParser.StringContainsVariable(info.SectionName) &&
                                    Sections.ContainsKey(info.SectionName))
                                    sectionQueue.Enqueue(Sections[info.SectionName]);
                            }
                            break;
                        case CodeType.IniWrite:
                            {
                                CodeInfo_IniWrite info = (CodeInfo_IniWrite)cmd.Info;

                                // To detect multi-interface without `InterfaceList=`,
                                // Inspect pattern `IniWrite,%ScriptFile%,Main,Interface,<NewInterfaceSection>`
                                if (info.FileName.Equals(Const.ScriptFile, StringComparison.OrdinalIgnoreCase) &&
                                    info.Section.Equals(ScriptSection.Names.Main, StringComparison.OrdinalIgnoreCase) &&
                                    info.Key.Equals(ScriptSection.Names.Interface, StringComparison.OrdinalIgnoreCase) &&
                                    !CodeParser.StringContainsVariable(info.Value))
                                {
                                    if (!interfaceSections.Contains(info.Value, StringComparer.OrdinalIgnoreCase))
                                        interfaceSections.Add(info.Value);
                                }
                            }
                            break;
                    }
                }
            }

            return interfaceSections;
        }

        public (string? sectionName, List<UIControl>? uiCtrls, List<LogInfo>? errLogs) GetInterfaceControls()
        {
            ScriptSection? ifaceSection = GetInterfaceSection(out string sectionName);
            if (ifaceSection == null)
                return (sectionName, null, null);

            string[] lines = ifaceSection.Lines;
            (List<UIControl> uiCtrls, List<LogInfo> errLogs) = UIParser.ParseStatements(lines, ifaceSection);
            return (sectionName, uiCtrls, errLogs);
        }

        public (List<UIControl>? uiCtrls, List<LogInfo>? errLogs) GetInterfaceControls(string srcSection)
        {
            if (!Sections.ContainsKey(srcSection))
                return (null, null);
            ScriptSection ifaceSection = Sections[srcSection];

            string[] lines = ifaceSection.Lines;
            (List<UIControl> uiCtrls, List<LogInfo> errLogs) = UIParser.ParseStatements(lines, ifaceSection);
            return (uiCtrls, errLogs);
        }
        #endregion

        #region Script Caching - PostDeserialization
        /// <summary>
        /// Must run the method after deserializing cached script.
        /// </summary>
        /// <param name="treePath">New treePath of the script.</param>
        /// <param name="project">New project of the script.</param>
        /// <param name="isDirLink">New isDirLink of the script.</param>
        internal void PostDeserialization(string treePath, Project project, bool isDirLink)
        {
            // Overwrite non-serialized fields.
            // Link have to be settled with SetLink().
            _fullyParsed = true;
            _treePath = treePath;
            _project = project;
            _isDirLink = isDirLink;

            // Currently MessagePack-CSharp does not support cyclic reference.
            // As a workaround, give a reference to the Script instance after the deserialization.
            foreach (ScriptSection section in _sections.Values)
                section.PostDeserialization(this);
        }
        #endregion

        #region SetLink
        public void SetLink(Script linkTarget)
        {
            if (Type == ScriptType.Directory)
                throw new InvalidOperationException($"Directory script cannot have a Link");
            if (linkTarget.Type == ScriptType.Directory)
                throw new ArgumentException($"linkTarget [{nameof(linkTarget)}] cannot be a Directory script");

            _link = linkTarget;
            _linkLoaded = true;
        }
        #endregion

        #region DeepInspectSections
        public void DeepInspectSections()
        {
            foreach (var kv in Sections.Where(x => x.Value.Type == SectionType.CodeOrUnknown || x.Value.Type == SectionType.NotInspected))
            {
                ScriptSection section = kv.Value;
                section.DeepInspect();
            }
        }
        #endregion

        #region Virtual, Overriden Methods
        public override string ToString()
        {
            return _type switch
            {
                ScriptType.Script => $"[S_{_level}] {_title}",
                ScriptType.Link => $"[L_{_level}] {MainInfo["Link"]}",
                ScriptType.Directory => $"[D_{_level}] {_title}",
                _ => _title,
            };
        }

        public override bool Equals(object? obj)
        {
            if (obj is Script sc)
                return RealPath.Equals(sc.RealPath, StringComparison.OrdinalIgnoreCase) &&
                       TreePath.Equals(sc.TreePath, StringComparison.OrdinalIgnoreCase);
            return false;
        }

        public bool Equals(Script? sc)
        {
            if (sc == null)
                return false;
            return RealPath.Equals(sc.RealPath, StringComparison.OrdinalIgnoreCase) &&
                   TreePath.Equals(sc.TreePath, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            // ReSharper disable once NonReadonlyMemberInGetHashCode
            // _treePath can be changed only right after deserialized from cache.
            return _realPath.GetHashCode() ^ _treePath.GetHashCode();
        }
        #endregion
    }
    #endregion

    #region Enums
    public enum ScriptType
    {
        Script = 0,
        Link = 1,
        Directory = 2,
    }

    public enum SelectedState
    {
        True, False, None
    }
    #endregion

    #region ScriptComaparer
    public class ScriptComparer : IEqualityComparer<Script>
    {
        public static ScriptComparer Instance => new ScriptComparer();

        public bool Equals(Script? x, Script? y)
        {
            Debug.Assert(x != null, "Script must not be null");
            Debug.Assert(y != null, "Script must not be null");
            return x.Equals(y);
        }

        public int GetHashCode(Script x)
        {
            return x.GetHashCode();
        }
    }
    #endregion

    #region ScriptParseInfo
    public class ScriptParseInfo : IEquatable<ScriptParseInfo>
    {
        public string RealPath { get; private set; }
        public string TreePath { get; private set; }
        public bool IsDir { get; private set; }
        public bool IsDirLink { get; private set; }

        public ScriptParseInfo(string realPath, string treePath, bool isDir, bool isDirLink)
        {
            RealPath = realPath;
            TreePath = treePath;
            IsDir = isDir;
            IsDirLink = isDirLink;
        }

        public override string ToString() => IsDir ? $"[D] {TreePath}" : $"[S] {TreePath}";

        public bool Equals(ScriptParseInfo? y)
        {
            if (y == null)
                return false;

            // IsDir, IsDirLink
            if (IsDir != y.IsDir || IsDirLink != y.IsDirLink)
                return false;

            // RealPath
            if (RealPath != null && y.RealPath == null ||
                RealPath == null && y.RealPath != null)
                return false;
            if (RealPath != null && y.RealPath != null &&
                !RealPath.Equals(y.RealPath, StringComparison.OrdinalIgnoreCase))
                return false;

            // TreePath
            if (TreePath != null && y.TreePath == null ||
                TreePath == null && y.TreePath != null)
                return false;
            if (TreePath != null && y.TreePath != null &&
                !TreePath.Equals(y.TreePath, StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        public override bool Equals(object? obj)
        {
            if (obj is ScriptParseInfo other)
                return Equals(other);
            else
                return false;
        }

        public override int GetHashCode()
        {
            int hash = IsDir.GetHashCode() ^ IsDirLink.GetHashCode();
            if (RealPath != null)
                hash ^= RealPath.GetHashCode();
            if (TreePath != null)
                hash ^= TreePath.GetHashCode();
            return hash;
        }
    }

    public class ScriptParseInfoComparer : IEqualityComparer<ScriptParseInfo>
    {
        public bool Equals(ScriptParseInfo? x, ScriptParseInfo? y)
        {
            if (x == null)
            {
                if (y == null)
                    return true;
                else
                    return false;
            }
            else
            {
                if (y == null)
                    return false;
                else
                    return x.Equals(y);
            }
        }

        public int GetHashCode(ScriptParseInfo x)
        {
            return x.RealPath.GetHashCode() ^ x.TreePath.GetHashCode() ^ x.IsDir.GetHashCode() ^ x.IsDirLink.GetHashCode();
        }

        public static ScriptParseInfoComparer Instance => new ScriptParseInfoComparer();
    }
    #endregion
}
