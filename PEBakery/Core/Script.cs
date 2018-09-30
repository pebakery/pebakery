/*
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

using PEBakery.Helper;
using PEBakery.Ini;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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
    [Serializable]
    public class Script : IEquatable<Script>
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
        [NonSerialized]
        private bool _isDirLink;
        private readonly string _title = string.Empty;
        private readonly string _author = string.Empty;
        private readonly string _description = string.Empty;
        private readonly string _version = "0";
        private readonly int _level;
        private SelectedState _selected = SelectedState.None;
        private readonly bool _mandatory = false;
        private readonly List<string> _interfaceList = new List<string>();
        #endregion

        #region Properties
        public string FullIdentifier => $"{_level}_{_realPath}_{_treePath}";
        public string RealIdentifier => $"{_level}_{_realPath}";
        public string TreeIdentifier => $"{_level}_{_treePath}";
        public string RealPath
        {
            get
            {
                if (_type == ScriptType.Link && _linkLoaded)
                    return _link.RealPath;
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
                return _sections;
            }
        }
        public Dictionary<string, string> MainInfo
        {
            get
            {
                if (_type == ScriptType.Link && _linkLoaded)
                    return _link.MainInfo;
                if (_sections.ContainsKey("Main"))
                    return _sections["Main"].IniDict;

                // Section not found, Just return empty dictionary
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        public bool IsMainScript => _isMainScript;
        public ScriptType Type => _type;
        public Script Link { get => _link; set => _link = value; }
        public bool LinkLoaded { get => _linkLoaded; set => _linkLoaded = value; }
        public bool IsDirLink { get => _isDirLink; set => _isDirLink = value; }
        public Project Project
        {
            get
            {
                if (_type == ScriptType.Link && _linkLoaded)
                    return _link.Project;
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
                return _title;
            }
        }
        public string Author
        {
            get
            {
                if (_type == ScriptType.Link && _linkLoaded)
                    return _link.Author;
                return _author;
            }
        }
        public string Description
        {
            get
            {
                if (_type == ScriptType.Link && _linkLoaded)
                    return _link.Description;
                return _description;
            }
        }
        public string Version
        {
            get
            {
                if (_type == ScriptType.Link && _linkLoaded)
                    return _link.Version;
                return _version;
            }
        }
        public int Level
        {
            get
            {
                if (_type == ScriptType.Link && _linkLoaded)
                    return _link.Level;
                return _level;
            }
        }
        public bool Mandatory
        {
            get
            {
                if (_type == ScriptType.Link && _linkLoaded)
                    return _link.Mandatory;
                return _mandatory;
            }
        }
        public SelectedState Selected
        {
            get => _selected;
            set
            {
                if (_selected == value)
                    return;

                _selected = value;
                string valStr = value.ToString();
                if (_type != ScriptType.Directory && _sections.ContainsKey("Main"))
                {
                    _sections["Main"].IniDict["Selected"] = valStr;
                    IniUtil.WriteKey(_realPath, new IniKey("Main", "Selected", valStr));
                }
            }
        }

        public ScriptSection this[string key] => Sections[key];
        #endregion

        #region Constructor
        public Script(
            ScriptType type, string realPath, string treePath, Project project,
            int? level, bool isMainScript, bool ignoreMain, bool isDirLink)
        {
            Debug.Assert(realPath != null, $"{nameof(realPath)} is null");
            Debug.Assert(treePath != null, $"{nameof(treePath)} is null");
            Debug.Assert(project != null, $"{nameof(project)} is null");

            Debug.Assert(!isDirLink || type != ScriptType.Link, "Script cannot be both Link and DirLink at the same time");
            Debug.Assert(treePath.Length == 0 || !Path.IsPathRooted(treePath), $"{nameof(treePath)} must be empty or rooted path");

            _realPath = realPath;
            _treePath = treePath;
            _type = type;
            _project = project;
            _isMainScript = isMainScript;
            _linkLoaded = false;
            _isDirLink = isDirLink;

            switch (type)
            {
                case ScriptType.Directory:
                    {
                        int dirLevel;
                        if (level is int lv)
                            dirLevel = lv;
                        else
                            dirLevel = 0;

                        // Mandatory Entries
                        _title = Path.GetFileName(treePath);
                        _description = $"[Directory] {_title}";
                        _level = dirLevel;

                        // Optional Entries
                        _author = string.Empty;
                        _version = "0";
                        _selected = SelectedState.None; // This value should be adjusted later!
                        _mandatory = false;
                        _link = null;

                        string[] mainSectionLines = new string[]
                        {
                            $"Title={_title}",
                            $"Description={_description}",
                            $"Level={_level}",
                        };

                        _sections = new Dictionary<string, ScriptSection>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["Main"] = CreateScriptSectionInstance("Main", SectionType.Main, mainSectionLines, 1)
                        };
                    }
                    break;
                case ScriptType.Link:
                    { // Parse only [Main] Section
                        _sections = ParseScript();
                        CheckMainSection(ScriptType.Link);
                        ScriptSection mainSection = _sections["Main"];

                        if (!mainSection.IniDict.ContainsKey("Link"))
                            throw new ScriptParseException($"Invalid link path in script {realPath}");

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
                                _version = mainSection.IniDict["Version"];
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
                                            string next;
                                            (next, remainder) = CodeParser.GetNextArgument(remainder);
                                            _interfaceList.Add(next);
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

        #region ParseScript
        public Dictionary<string, ScriptSection> ParseScript()
        {
            Dictionary<string, ScriptSection> dict = new Dictionary<string, ScriptSection>(StringComparer.OrdinalIgnoreCase);

            Encoding encoding = FileHelper.DetectTextEncoding(_realPath);
            using (StreamReader r = new StreamReader(_realPath, encoding))
            {
                int idx = 0;
                int sectionIdx = 0;
                string line;
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

                while ((line = r.ReadLine()) != null)
                { // Read text line by line
                    idx++;
                    line = line.Trim();

                    if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
                    { // Start of section
                        FinalizeSection();

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

                    if (r.Peek() == -1) // End of .script
                        FinalizeSection();
                }
            }

            _fullyParsed = true;

            return dict;
        }
        #endregion

        #region Detect Section Type
        private bool IsSectionEncodedFolders(string sectionName)
        {
            IList<string> encodedFolders;
            if (_fullyParsed)
            {
                if (_sections.ContainsKey(ScriptSection.Names.EncodedFolders))
                    encodedFolders = _sections[ScriptSection.Names.EncodedFolders].Lines;
                else
                    return false;
            }
            else
            {
                encodedFolders = IniUtil.ParseIniSection(_realPath, ScriptSection.Names.EncodedFolders);
                if (encodedFolders == null)  // No EncodedFolders section, exit
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
            else // Can be SectionType.Code or SectionType.AttachFileList
                type = inspectCode ? DetectTypeOfUninspectedSection(sectionName) : SectionType.Uninspected;
            return type;
        }

        private void InspectTypeOfUninspectedCodeSection()
        {
            // Dictionary<string, ScriptSection>
            foreach (var key in _sections.Keys)
            {
                if (_sections[key].Type == SectionType.Uninspected)
                    _sections[key].Type = DetectTypeOfUninspectedSection(_sections[key].Name);
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
                case SectionType.Interface:
                case SectionType.Code:
                case SectionType.Uninspected:
                case SectionType.AttachFolderList:
                case SectionType.AttachFileList:
                case SectionType.AttachEncodeNow:
                    return true;
                default:
                    return false;
            }
        }
        #endregion

        #region ScriptSection
        private ScriptSection CreateScriptSectionInstance(string sectionName, SectionType type, string[] lines, int lineIdx)
        {
            switch (type)
            {
                case SectionType.Main:
                case SectionType.Variables:
                case SectionType.Interface:
                case SectionType.Code:
                case SectionType.Uninspected:
                case SectionType.AttachFolderList:
                case SectionType.AttachFileList:
                case SectionType.AttachEncodeNow:
                    return new ScriptSection(this, sectionName, type, lines, lineIdx);
                case SectionType.AttachEncodeLazy: // do not load now
                    return new ScriptSection(this, sectionName, type, false, lineIdx);
                default:
                    throw new ScriptParseException($"Invalid SectionType [{type}]");
            }
        }

        public ScriptSection RefreshSection(string sectionName)
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
                List<string> lineList = IniUtil.ParseRawSection(_realPath, sectionName);
                if (lineList == null)
                    return null;

                SectionType type = DetectTypeOfSection(sectionName, true);
                // lineIdx information is not provided, use with caution!
                section = CreateScriptSectionInstance(sectionName, type, lineList.ToArray(), 0);
                Sections[sectionName] = section;
            }

            return section;
        }
        #endregion

        #region CheckMainSection
        private void CheckMainSection(ScriptType type)
        {
            if (!_sections.ContainsKey("Main"))
                throw new ScriptParseException($"[{_realPath}] is invalid, please Add [Main] Section");
            Dictionary<string, string> mainDict = _sections["Main"].IniDict;

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
        public static string[] GetDisableScriptPaths(Script sc, out List<LogInfo> errorLogs)
        {
            errorLogs = new List<LogInfo>();

            if (sc.Type == ScriptType.Directory || sc._isMainScript)
                return null;

            if (!sc.MainInfo.ContainsKey("Disable"))
                return null;

            sc.Project.Variables.ResetVariables(VarsType.Local);
            sc.Project.Variables.LoadDefaultScriptVariables(sc);

            string rawLine = sc.MainInfo["Disable"];

            // Check if rawCode is Empty
            if (rawLine.Equals(string.Empty, StringComparison.Ordinal))
                return null;

            // Check doublequote's occurence - must be 2n
            if (StringHelper.CountSubStr(rawLine, "\"") % 2 == 1)
                throw new ExecuteException("Doublequote's number should be even number");

            // Parse Arguments
            List<string> paths = new List<string>();
            try
            {
                string remainder = rawLine;
                while (remainder != null)
                {
                    string next;
                    (next, remainder) = CodeParser.GetNextArgument(remainder);
                    paths.Add(next);
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
        #endregion

        #region Interface Methods - Get, Apply
        public ScriptSection GetInterfaceSection(out string sectionName)
        {
            sectionName = "Interface";
            if (MainInfo.ContainsKey("Interface"))
                sectionName = MainInfo["Interface"];

            return Sections.ContainsKey(sectionName) ? Sections[sectionName] : null;
        }

        public (string sectionName, List<UIControl> uiCtrls, List<LogInfo> errLogs) GetInterfaceControls()
        {
            ScriptSection ifaceSection = GetInterfaceSection(out string sectionName);
            if (ifaceSection == null)
                return (null, null, null);

            string[] lines = ifaceSection.Lines;
            (List<UIControl> uiCtrls, List<LogInfo> errLogs) = UIParser.ParseStatements(lines, ifaceSection);
            return (sectionName, uiCtrls, errLogs);
        }

        public (List<UIControl> uiCtrls, List<LogInfo> errLogs) GetInterfaceControls(string srcSection)
        {
            if (!Sections.ContainsKey(srcSection))
                return (null, null);
            ScriptSection ifaceSection = Sections[srcSection];

            string[] lines = ifaceSection.Lines;
            (List<UIControl> uiCtrls, List<LogInfo> errLogs) = UIParser.ParseStatements(lines, ifaceSection);
            return (uiCtrls, errLogs);
        }

        public bool ApplyInterfaceControls(List<UIControl> newCtrls, string destSection = "Interface")
        {
            if (!Sections.ContainsKey(destSection))
                return false; // Section [destSection] not found

            (List<UIControl> oldCtrls, _) = GetInterfaceControls(destSection);
            List<UIControl> updatedCtrls = new List<UIControl>();
            foreach (UIControl newCtrl in newCtrls)
            {
                UIControl oldCtrl = oldCtrls.Find(x => x.Key.Equals(newCtrl.Key, StringComparison.OrdinalIgnoreCase));
                if (oldCtrl == null)
                { // newCtrl not exist in oldCtrls, append it.
                    updatedCtrls.Add(newCtrl);
                    continue;
                }

                // newCtrl exist in oldCtrls
                oldCtrls.Remove(oldCtrl);
                if (oldCtrl.Type != newCtrl.Type)
                { // Keep oldCtrl. They are different uiCtrls even though they have same key.
                    updatedCtrls.Add(oldCtrl);
                    continue;
                }

                string val = newCtrl.GetValue(false);
                if (val == null)
                { // This ctrl does not have 'value'. Keep oldCtrl.
                    updatedCtrls.Add(oldCtrl);
                    continue;
                }

                if (!oldCtrl.SetValue(val, false, out _))
                { // Unable to write value to oldCtrl
                    updatedCtrls.Add(oldCtrl);
                    continue;
                }

                // Apply newCtrl
                updatedCtrls.Add(newCtrl);
            }

            // Append leftover oldCtrls
            updatedCtrls.AddRange(oldCtrls);

            // Write to file
            return UIControl.Update(updatedCtrls);
        }
        #endregion

        #region Virtual, Interface Methods
        public override string ToString()
        {
            switch (_type)
            {
                case ScriptType.Script:
                    return $"[S_{_level}] {_title}";
                case ScriptType.Link:
                    return $"[L_{_level}] {MainInfo["Link"]}";
                case ScriptType.Directory:
                    return $"[D_{_level}] {_title}";
            }
            return _title;
        }

        public override bool Equals(object obj)
        {
            if (obj is Script sc)
                return RealPath.Equals(sc.RealPath, StringComparison.OrdinalIgnoreCase) &&
                       TreePath.Equals(sc.TreePath, StringComparison.OrdinalIgnoreCase);
            return false;
        }

        public bool Equals(Script sc)
        {
            if (sc == null)
                return false;
            return RealPath.Equals(sc.RealPath, StringComparison.OrdinalIgnoreCase) &&
                   TreePath.Equals(sc.TreePath, StringComparison.OrdinalIgnoreCase);
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
        None = 0,
        // [Main]
        Main = 10,
        // [Variables]
        Variables = 20,
        // [Interface]
        Interface = 30,
        // [Process], ...
        Code = 40,
        // Code or AttachFileList
        Uninspected = 90,
        // [EncodedFolders]
        AttachFolderList = 100,
        // [AuthorEncoded], [InterfaceEncoded], and other folders
        AttachFileList = 101,
        // [EncodedFile-InterfaceEncoded-*], [EncodedFile-AuthorEncoded-*]
        AttachEncodeNow = 102,
        // [EncodedFile-*]
        AttachEncodeLazy = 103,
    }
    #endregion

    #region Comaparer
    public class ScriptComparer : IEqualityComparer<Script>
    {
        public bool Equals(Script x, Script y)
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

    #region ScriptSection
    [Serializable]
    public class ScriptSection : IEquatable<ScriptSection>
    {
        #region (Const) Known Section Names
        public static class Names
        {
            public const string Main = "Main";
            public const string Variables = "Variables";
            public const string Interface = "Interface";
            public const string Process = "Process";
            public const string EncodedFolders = "EncodedFolders";
            public const string AuthorEncoded = "AuthorEncoded";
            public const string InterfaceEncoded = "InterfaceEncoded";
            public const string EncodedFileInterfaceEncodedPrefix = "EncodedFile-InterfaceEncoded-";
            public const string EncodedFileAuthorEncodedPrefix = "EncodedFile-AuthorEncoded-";
            public const string EncodedFilePrefix = "EncodedFile-";
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static string GetEncodedSectionName(string folderName, string fileName) => $"EncodedFile-{folderName}-{fileName}";
        }

        #endregion

        #region Fields and Properties
        public Script Script { get; }
        public Project Project => Script.Project;
        public string Name { get; }
        public SectionType Type { get; set; }
        public int LineIdx { get; }
        private string[] _lines;

        /// <summary>
        /// Get lines of this section (Cached)
        /// </summary>
        public string[] Lines
        {
            get
            {
                // Already loaded, return directly
                if (_lines != null)
                    return _lines;

                // Load from file, do not keep in memory. AttachEncodeLazy sections are too large.
                if (Type == SectionType.AttachEncodeLazy)
                {
                    List<string> lineList = IniUtil.ParseRawSection(Script.RealPath, Name);
                    return lineList?.ToArray();
                }

                // Load from file, keep in memory.
                if (LoadLines())
                    return _lines;

                return null;
            }
        }

        private Dictionary<string, string> _iniDict;
        public Dictionary<string, string> IniDict
        {
            get
            {
                // Already loaded, return directly
                if (_iniDict != null)
                    return _iniDict;

                // Load from file, do not keep in memory. AttachEncodeLazy sections are too large.
                if (Type == SectionType.AttachEncodeLazy)
                    return IniUtil.ParseIniSectionToDict(Script.RealPath, Name);

                // Load from file, keep in memory.
                if (LoadIniDict())
                    return _iniDict;
                return null;
            }
        }
        #endregion

        #region Constructor
        public ScriptSection(Script script, string sectionName, SectionType type, bool load, int lineIdx)
        {
            Script = script;
            Name = sectionName;
            Type = type;
            LineIdx = lineIdx;
            if (load)
                LoadLines();
        }

        public ScriptSection(Script script, string sectionName, SectionType type, string[] lines, int lineIdx)
        {
            Script = script;
            Name = sectionName;
            Type = type;
            LineIdx = lineIdx;
            _lines = lines;
        }
        #endregion

        #region Load, Unload, Reload
        /// <summary>
        /// If _lines is not loaded from file, load it to memory.
        /// </summary>
        /// <returns>
        /// true if _lines is valid
        /// </returns>
        public bool LoadLines()
        {
            if (_lines != null)
                return true;

            List<string> lineList = IniUtil.ParseRawSection(Script.RealPath, Name);
            if (lineList == null)
                return false;
            _lines = lineList.ToArray();
            return true;
        }

        /// <summary>
        /// If _lines is not loaded from file, load it to memory.
        /// </summary>
        /// <returns>
        /// true if _lines is valid
        /// </returns>
        public bool LoadIniDict()
        {
            bool result = true;
            if (_lines == null)
                result = LoadLines();
            if (!result) // LoadLines failed
                return false;

            if (_iniDict != null)
                return true;

            _iniDict = IniUtil.ParseIniLinesIniStyle(_lines);
            return true;
        }

        /// <summary>
        /// Discard loaded _lines.
        /// </summary>
        /// <remarks>
        /// Useful to reduce memory usage.
        /// </remarks>
        public void Unload()
        {
            _lines = null;
            _iniDict = null;
        }

        /// <summary>
        /// Reload _lines from file.
        /// </summary>
        public bool Reload()
        {
            Unload();
            return LoadLines();
        }
        #endregion

        #region Equals, GetHashCode
        public override bool Equals(object obj)
        {
            if (obj is ScriptSection section)
                return Equals(section);
            return false;
        }

        public bool Equals(ScriptSection section)
        {
            if (section == null) throw new ArgumentNullException(nameof(section));

            return Script.Equals(section.Script) && Name.Equals(section.Name, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            return Script.GetHashCode() ^ Name.GetHashCode() ^ LineIdx.GetHashCode();
        }
        #endregion

        #region Override Methods
        public override string ToString()
        {
            return Name;
        }
        #endregion
    }
    #endregion

    #region Struct ScriptParseInfo
    public struct ScriptParseInfo
    {
        public string RealPath;
        public string TreePath;
        public bool IsDir;
        public bool IsDirLink;

        public override string ToString() => IsDir ? $"[D] {TreePath}" : $"[S] {TreePath}";
    }

    public class ScriptParseInfoComparer : IEqualityComparer<ScriptParseInfo>
    {
        public bool Equals(ScriptParseInfo x, ScriptParseInfo y)
        {
            return x.RealPath.Equals(y.RealPath, StringComparison.OrdinalIgnoreCase) &&
                   x.TreePath.Equals(y.TreePath, StringComparison.OrdinalIgnoreCase) &&
                   x.IsDir == y.IsDir &&
                   x.IsDirLink == y.IsDirLink;
        }

        public int GetHashCode(ScriptParseInfo x)
        {
            return x.RealPath.GetHashCode() ^ x.TreePath.GetHashCode() ^ x.IsDir.GetHashCode() ^ x.IsDirLink.GetHashCode();
        }
    }
    #endregion
}

