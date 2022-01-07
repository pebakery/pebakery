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

namespace PEBakery.Core
{
    #region (enum) SectionType
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
        NonInspected = 90,
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
                    SectionType.NonInspected => value,
                    _ => throw new InvalidOperationException($"Overwriting a type of a ScriptSection is only allowed to SectionType.NonInstpected."),
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
#pragma warning disable CA1819 // Properties should not return arrays
        public string[] Lines
#pragma warning restore CA1819 // Properties should not return arrays
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
        /// If _lines is not loaded from file, load it to memory.
        /// </summary>
        /// <returns>
        /// true if _lines is valid
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
                case SectionType.Code:
                case SectionType.NonInspected:
                case SectionType.AttachFolderList:
                case SectionType.AttachFileList:
                case SectionType.AttachEncodeNow:
                    return true;
                default:
                    return false;
            }
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
            return Name;
        }
        #endregion
    }
    #endregion
}
