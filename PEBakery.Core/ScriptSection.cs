/*
    Copyright (C) 2018-2022 Hajin Jang
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
using PEBakery.Ini;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace PEBakery.Core
{
    #region (enum) SectionType
    /// <summary>
    /// Type of a PEBakery script section.
    /// Used to determine (1) whether a section content should be loaded into a memory, and (2) whether a section should be syntax-checked.
    /// PEBakery Engine will treat a section always as a Code, regardless of actual SectionType value.
    /// </summary>
    public enum SectionType
    {
        None = 0,
        /// <summary>
        /// [Main]
        /// </summary>
        Main = 10,
        /// <summary>
        /// [Variables]
        /// </summary>
        Variables = 20,
        /// <summary>
        /// Simpler .ini style, detected with Deep Inspection.
        /// </summary>
        SimpleIni = 21,
        /// <summary>
        /// [Interface] 
        /// </summary>
        Interface = 30,
        /// <summary>
        /// [Process], ...
        /// </summary>
        Code = 40,
        /// <summary>
        /// Assumed as a Code section.
        /// </summary>
        CodeOrUnknown = 41,
        /// <summary>
        /// Comments - [#...#], or detected with Deep Inspection. 
        /// </summary>
        Commentary = 50,
        /// <summary>
        /// Would be a Code, a SimpleIni, a Interface or an AttachFileList section.
        /// AttachFileList - detectable with simple AttachFileList section inspection.
        /// Code/SimpleIni/Interface - requires deep inspection.
        /// </summary>
        NotInspected = 90,
        /// <summary>
        /// [EncodedFolders]
        /// </summary>
        AttachFolderList = 100,
        /// <summary>
        /// [AuthorEncoded], [InterfaceEncoded], and other folders
        /// </summary>
        AttachFileList = 101,
        /// <summary>
        /// [EncodedFile-InterfaceEncoded-*], [EncodedFile-AuthorEncoded-*]
        /// </summary>
        AttachEncodeNow = 102,
        /// <summary>
        /// [EncodedFile-*]
        /// </summary>
        AttachEncodeLazy = 103,
    }
    #endregion

    #region ScriptSection
    [MessagePackObject]
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
            public const string ScriptUpdate = @"ScriptUpdate";
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static string GetEncodedSectionName(string folderName, string fileName) => $"EncodedFile-{folderName}-{fileName}";
        }
        #endregion

        #region Fields and Properties
        [IgnoreMember]
        private Script? _script;
        [IgnoreMember]
        public Script Script
        {
            get
            {
                if (_script == null)
                    throw new InvalidOperationException($"{nameof(_script)} is null");
                return _script;
            }
            private set => _script = value;
        }
        [IgnoreMember]
        public Project Project => Script.Project;
        [Key(0)] // "private set" for Deserialization
        public string Name { get; private set; } = string.Empty;
        [Key(1)]
        private SectionType _type;
        [IgnoreMember]
        public SectionType Type
        {
            get => _type;
            set
            {
                _type = Type switch
                {
                    SectionType.NotInspected => value,
                    SectionType.CodeOrUnknown => value,
                    _ => throw new InvalidOperationException($"Overwriting a type of a ScriptSection is only allowed to [{nameof(SectionType.NotInspected)}] or [{nameof(SectionType.CodeOrUnknown)}]."),
                };
            }
        }
        [Key(2)] // "private set" for Deserialization
        public int LineIdx { get; private set; }

        /// <summary>
        /// Get lines of this section (Cached)
        /// </summary>
        [Key(3)]
        private string[]? _lines;
        [IgnoreMember]
        public string[] Lines
        {
            get
            {
                // Return cached line array.
                if (_lines != null)
                    return _lines;

                // Load from file, do not keep in memory. AttachEncodeLazy sections are too large.
                if (Type == SectionType.AttachEncodeLazy)
                {
                    List<string>? lineList = IniReadWriter.ParseRawSection(Script.RealPath, Name);
                    if (lineList != null)
                        return lineList.ToArray();
                    else
                        throw new InvalidOperationException($"Section [{Name}] is not a line-type section");
                }

                // Load from file, cache them in the memory.
                if (LoadLines() && _lines != null)
                    return _lines;

                throw new InvalidOperationException($"Section [{Name}] is not a line-type section");
            }
        }

        [IgnoreMember]
        private Dictionary<string, string>? _iniDict;
        [IgnoreMember]
        public Dictionary<string, string> IniDict
        {
            get
            {
                // Return cached dictionary.
                if (_iniDict != null)
                    return _iniDict;

                // Load from file, do not keep in memory. AttachEncodeLazy sections are too large.
                if (Type == SectionType.AttachEncodeLazy)
                {
                    Dictionary<string, string>? iniDict = IniReadWriter.ParseIniSectionToDict(Script.RealPath, Name);
                    if (iniDict != null)
                        return iniDict;
                    else
                        throw new InvalidOperationException($"Section [{Name}] is not an ini-type section");

                }

                // Load from file, cache them in the memory.
                if (LoadIniDict() && _iniDict != null)
                    return _iniDict;

                throw new InvalidOperationException($"Section [{Name}] is not an ini-type section");
            }
        }
        #endregion

        #region Constructor
        public ScriptSection(Script script, string sectionName, SectionType type, bool load, int lineIdx)
        {
            Script = script;
            Name = sectionName;
            _type = type;
            LineIdx = lineIdx;
            if (load)
                LoadLines();
        }

        public ScriptSection(Script script, string sectionName, SectionType type, string[] lines, int lineIdx)
        {
            Script = script;
            Name = sectionName;
            _type = type;
            LineIdx = lineIdx;
            _lines = lines;
        }

        [SerializationConstructor]
        private ScriptSection() { /* Do Nothing */ }
        #endregion

        #region Load, Unload, Reload
        /// <summary>
        /// If _lines was not loaded from file, load it to memory.
        /// </summary>
        /// <returns>
        /// Return true if _lines is valid.
        /// </returns>
        public bool LoadLines()
        {
            if (_lines != null)
                return true;

            List<string>? lineList = IniReadWriter.ParseRawSection(Script.RealPath, Name);
            if (lineList == null)
                return false;
            _lines = lineList.ToArray();
            return true;
        }

        /// <summary>
        /// If _lines was not loaded from file, load it to memory.
        /// </summary>
        /// <returns>
        /// true if _lines is valid
        /// </returns>
        public bool LoadIniDict()
        {
            bool result = true;
            if (_lines == null)
                result = LoadLines();
            if (!result || _lines == null) // LoadLines failed
                return false;

            if (_iniDict != null)
                return true;

            _iniDict = IniReadWriter.ParseIniLinesIniStyle(_lines);
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

        #region UpdateIniKey, DeleteIniKey
        /// <summary>
        /// Update Lines property.
        /// ScriptSection must not be SectionType.AttachEncodeLazy
        /// </summary>
        /// <returns>true if succeeded</returns>
        public bool UpdateIniKey(string key, string value)
        {
            // AttachEncodeLazy cannot be updated 
            if (Type == SectionType.AttachEncodeLazy)
                return false;
            if (_lines == null)
                return false;
            _iniDict = null;

            bool updated = false;
            for (int i = 0; i < _lines.Length; i++)
            {
                // 'line' was already trimmed at the loading time. Do not call Trim() again to avoid new heap allocation.
                string line = _lines[i];

                int eIdx = line.IndexOf('=');
                if (eIdx != -1 && eIdx != 0)
                { // Key Found
                    ReadOnlySpan<char> keyName = line.AsSpan(0, eIdx).TrimEnd(); // Do not need to trim start of the line
                    if (keyName.Equals(key.AsSpan(), StringComparison.OrdinalIgnoreCase))
                    {
                        _lines[i] = $"{key}={value}";
                        updated = true;
                        break;
                    }
                }
            }

            if (!updated)
            { // Append to last line
                Array.Resize(ref _lines, _lines.Length + 1);
                _lines[^1] = $"{key}={value}";
            }

            return true;
        }

        public bool DeleteIniKey(string key)
        {
            // AttachEncodeLazy cannot be updated 
            if (Type == SectionType.AttachEncodeLazy)
                return false;
            if (_lines == null)
                return false;
            _iniDict = null;

            int targetIdx = -1;
            for (int i = 0; i < _lines.Length; i++)
            {
                // 'line' was already trimmed at the loading time. Do not call Trim() again to avoid new heap allocation.
                string line = _lines[i];

                int eIdx = line.IndexOf('=');
                if (eIdx != -1 && eIdx != 0)
                { // Key Found
                    ReadOnlySpan<char> keyName = line.AsSpan(0, eIdx).TrimEnd(); // Do not need to trim start of the line
                    if (keyName.Equals(key.AsSpan(), StringComparison.OrdinalIgnoreCase))
                    {
                        targetIdx = i;
                        break;
                    }
                }
            }

            if (targetIdx != -1)
            { // Delete target line
                List<string> newLines = _lines.ToList();
                newLines.RemoveAt(targetIdx);
                _lines = newLines.ToArray();
            }

            return true;
        }
        #endregion

        #region Script Caching - PostDeserialization
        public void PostDeserialization(Script parent)
        {
            Script = parent;
        }
        #endregion

        #region LoadSectionAtScriptLoadTime
        public static bool LoadSectionAtScriptLoadTime(SectionType type)
        {
            switch (type)
            {
                case SectionType.Main:
                case SectionType.Variables:
                case SectionType.Interface:
                case SectionType.CodeOrUnknown:
                case SectionType.Code:
                case SectionType.NotInspected:
                case SectionType.AttachFolderList:
                case SectionType.AttachFileList:
                case SectionType.AttachEncodeNow:
                    return true;
                case SectionType.SimpleIni:
                default:
                    return false;
            }
        }
        #endregion

        #region DeepInspectSection
        /// <summary>
        /// Deep inspect a section by analyzing its content.
        /// Only runs on NotInspected/CodeOrUnknown sections.
        /// </summary>
        public void DeepInspect()
        {
            // Check only Code/NonInspected sections.
            switch (Type)
            {
                case SectionType.NotInspected:
                case SectionType.CodeOrUnknown:
                    break;
                default:
                    return;
            }

            int ifaceMatchCount = 0;
            int iniMatchCount = 0;
            int varMatchCount = 0;
            int codeMatchCount = 0;
            int totalLineCount = 0;

            // Check the type of a line using regexes.
            // Even though regex does have some errors, regexes are used for its simplexity and speed advantage.
            // TODO: How many times running a precise parsers like CodeParser/UIParser/IniReadWriter are slower than regexes?
            foreach (string line in Lines.Where(x => 0 < x.Length))
            {
                // Exclude comment lines
                if (IniReadWriter.IsLineComment(line))
                    continue;

                totalLineCount += 1;

                // Is this line a code?
                if (Regex.IsMatch(line, "^(([A-Za-z0-9_]+[ ]*(,.+)*)|End|Break|Continue)$", RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase))
                    codeMatchCount += 1;

                // Is this line an interface Control?
                if (Regex.IsMatch(line, "^([^%=\r\n]+)=(.*,[0-9]+,[0-9]+,[0-9]+,[0-9]+,[0-9]+,[0-9]+.*)$", RegexOptions.CultureInvariant | RegexOptions.Compiled))
                    ifaceMatchCount += 1;

                // Is this line a var-style line?
                if (Regex.IsMatch(line, "^(%[^=\r\n]+%)=(.*)$", RegexOptions.CultureInvariant | RegexOptions.Compiled))
                    varMatchCount += 1;

                // Is this line a simple ini-style line?
                if (Regex.IsMatch(line, "^([^=\r\n]+)=(.*)$", RegexOptions.CultureInvariant | RegexOptions.Compiled))
                    iniMatchCount += 1;
            }

            // If more than {threshold}% of sections lines are analyzed as a single type, 
            // Convert the type of this section to a inferred section type.
            const double threshold = 0.75;

            // Infer the type of this section, using the line count data.
            // Keep the order, as SimpleIni detection are mostly prone to false-positive error.
            SectionType newType;
            if (totalLineCount == 0)
                newType = SectionType.Commentary;
            else if (totalLineCount * threshold <= codeMatchCount)
                newType = SectionType.Code;
            else if (totalLineCount * threshold <= ifaceMatchCount)
                newType = SectionType.Interface;
            else if (totalLineCount * threshold <= varMatchCount)
                newType = SectionType.Variables;
            else if (totalLineCount * threshold <= iniMatchCount)
                newType = SectionType.SimpleIni;
            else
                newType = SectionType.Commentary;
            Type = newType;
        }
        #endregion

        #region Equals, GetHashCode
        public override bool Equals(object? obj)
        {
            if (obj is ScriptSection section)
                return Equals(section);
            return false;
        }

        public bool Equals(ScriptSection? section)
        {
            if (section == null)
                return false;

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
            return $"{Name} ({Type})";
        }
        #endregion
    }
    #endregion
}
