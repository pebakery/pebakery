/*
    Copyright (C) 2016-2022 Hajin Jang
 
    MIT License

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.
*/

using PEBakery.Helper;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace PEBakery.Ini
{
    #region IniKey
    public class IniKey : IEquatable<IniKey>
    {
        #region Properties
        public string Section { get; set; }
        public string? Key { get; set; }
        public string? Value { get; set; }
        #endregion

        #region Constructor
        public IniKey(string section)
        {
            Section = section;
            Key = null;
            Value = null;
        }

        public IniKey(string section, string key)
        {
            Section = section;
            Key = key;
            Value = null;
        }

        public IniKey(string section, string key, string value)
        {
            Section = section;
            Key = key;
            Value = value;
        }
        #endregion

        #region Interface and Override Methods
        public bool Equals(IniKey? other)
        {
            if (other is null)
                return false;

            static bool StringEqual(string? x, string? y)
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
                        return true;
                }
            }

            return StringEqual(Section, other.Section) && StringEqual(Key, other.Key) && StringEqual(Value, other.Value);
        }

        public override bool Equals(object? obj)
        {
            if (obj is IniKey iniKey)
                return Equals(iniKey);
            else
                return false;
        }

        public override int GetHashCode()
        {
            int hashCode = Section.GetHashCode();
            if (Key != null)
                hashCode ^= Key.GetHashCode();
            if (Value != null)
                hashCode ^= Value.GetHashCode();
            return hashCode;
        }

        public static bool operator ==(IniKey left, IniKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(IniKey left, IniKey right)
        {
            return !(left == right);
        }
        #endregion
    }
    #endregion

    #region IniReadWriter
    public static class IniReadWriter
    {
        #region (Read) ReadKey
        /// <summary>
        /// Read a value of target key from an ini file.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="section">Name of the target section.</param>
        /// <param name="key">Key to read its value.</param>
        /// <returns>A value of target key.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string? ReadKey(string filePath, string section, string key)
        {
            IniKey[] iniKeys = InternalReadKeys(filePath, new IniKey[] { new IniKey(section, key) });
            return iniKeys[0].Value;
        }

        /// <summary>
        /// Read a value of target key from an ini file.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="iniKey">A tuple of Section and Key. Value is ignored.</param>
        /// <returns>A value of target key.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string? ReadKey(string filePath, IniKey iniKey)
        {
            IniKey[] iniKeys = InternalReadKeys(filePath, new IniKey[] { iniKey });
            return iniKeys[0].Value;
        }

        /// <summary>
        /// Read values of target keys from an ini file.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="iniKeys">Enumerable tuples of Section and Key.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IniKey[] ReadKeys(string filePath, IEnumerable<IniKey> iniKeys)
        {
            return InternalReadKeys(filePath, iniKeys.ToArray());
        }

        /// <summary>
        /// Read value of specified key from .ini files.
        /// Values read will be written into iniKeys.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="iniKeys">Enumerable tuples of Section and Key.</param>
        /// <returns>Instance of iniKeys which contain </returns>
        private static IniKey[] InternalReadKeys(string filePath, IniKey[] iniKeys)
        {
            List<int> processedKeyIdxs = new List<int>(iniKeys.Length);

            Encoding encoding = SmarterDetectEncoding(filePath, iniKeys);
            using (StreamReader reader = new StreamReader(filePath, encoding, true))
            {
                string? rawLine;
                bool inTargetSection = false;
                string? currentSection = null;

                while ((rawLine = reader.ReadLine()) != null)
                { // Read text line by line
                    if (processedKeyIdxs.Count == iniKeys.Length) // Work Done
                        break;

                    ReadOnlySpan<char> line = rawLine.AsSpan().Trim(); // Remove whitespace
                    if (IsLineComment(line)) // Ignore comment
                        continue;

                    if (inTargetSection && currentSection != null)
                    {
                        int idx = line.IndexOf('=');
                        if (idx != -1 && idx != 0) // there is key, and key name is not empty
                        {
                            ReadOnlySpan<char> keyName = line[..idx].Trim();
                            for (int i = 0; i < iniKeys.Length; i++)
                            {
                                if (processedKeyIdxs.Contains(i))
                                    continue;

                                // Only if <section, key> is same, copy value;
                                IniKey iniKey = iniKeys[i];
                                if (currentSection.Equals(iniKey.Section, StringComparison.OrdinalIgnoreCase) &&
                                    keyName.Equals(iniKey.Key.AsSpan(), StringComparison.OrdinalIgnoreCase))
                                {
                                    iniKey.Value = line[(idx + 1)..].Trim().ToString();
                                    iniKeys[i] = iniKey;
                                    processedKeyIdxs.Add(i);
                                }
                            }
                        }
                        else
                        {
                            // search if current section reached its end
                            if (IsLineSection(line, out ReadOnlySpan<char> foundSection))
                            {
                                // Only sections contained in iniKeys will be targeted
                                inTargetSection = false;
                                currentSection = null;
                                for (int i = 0; i < iniKeys.Length; i++)
                                {
                                    if (processedKeyIdxs.Contains(i))
                                        continue;

                                    if (foundSection.Equals(iniKeys[i].Section, StringComparison.OrdinalIgnoreCase))
                                    {
                                        inTargetSection = true;
                                        currentSection = foundSection.ToString();
                                        break; // for shorter O(n)
                                    }
                                }
                            }
                        }
                    }
                    else
                    { // not in section
                      // Check if encountered section head Ex) [Process]
                        if (IsLineSection(line, out ReadOnlySpan<char> foundSection))
                        {
                            // Only sections contained in iniKeys will be targeted
                            for (int i = 0; i < iniKeys.Length; i++)
                            {
                                if (processedKeyIdxs.Contains(i))
                                    continue;

                                if (foundSection.Equals(iniKeys[i].Section, StringComparison.OrdinalIgnoreCase))
                                {
                                    inTargetSection = true;
                                    currentSection = foundSection.ToString();
                                    break; // for shorter O(n)
                                }
                            }
                        }
                    }
                }
            }

            return iniKeys;
        }
        #endregion

        #region (Write) WriteKey
        /// <summary>
        /// Write a pair of key and value into an .ini file.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="section">Name of the target section.</param>
        /// <param name="key">Key to insert/overwrite.</param>
        /// <param name="value">New value to insert/overwrite.</param>
        /// <returns>Returns true if the operation was successful.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteKey(string filePath, string section, string key, string value)
        {
            return InternalWriteKeys(filePath, new List<IniKey> { new IniKey(section, key, value) });
        }

        /// <summary>
        /// Write a pair of key and value into an .ini file.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="iniKey">Tuple of Section, Key, and Value.</param>
        /// <returns>Returns true if the operation was successful.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteKey(string filePath, IniKey iniKey)
        {
            return InternalWriteKeys(filePath, new List<IniKey> { iniKey });
        }

        /// <summary>
        /// Write multiple pairs of key and value into an .ini file.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="iniKeys">Enumberable tuples of Section, Key, and Value.</param>
        /// <returns>An array of return value for each IniKey. Returns true if the operation of an iniKey was successful.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteKeys(string filePath, IEnumerable<IniKey> iniKeys)
        {
            return InternalWriteKeys(filePath, iniKeys.ToList());
        }

        private static bool InternalWriteKeys(string filePath, List<IniKey> inputKeys)
        {
            // Input null check 
            if (inputKeys.Any(x => x.Key == null || x.Value == null))
                return false;

            #region FinalizeFile
            void FinalizeFile(StreamWriter w, ReadOnlySpan<char> lastLine, bool firstEmptyLine)
            {
                bool firstSection = true;
                if (0 < inputKeys.Count)
                {
                    // inputKeys are modified in the loop. Call ToArray() to clone inputKeys.
                    string[] unprocessedSections = inputKeys
                        .Select(x => x.Section)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    foreach (string section in unprocessedSections)
                    {
                        // inputKeys are modified in the loop. Call ToArray() to clone inputKeys.
                        IniKey[] secKeys = inputKeys
                            .Where(x => x.Section.Equals(section))
                            .ToArray();

                        if ((lastLine == null || lastLine.Length != 0) && (!firstSection || firstEmptyLine))
                            w.WriteLine();
                        w.WriteLine($"[{section}]");

                        foreach (IniKey secKey in secKeys)
                        {
                            w.WriteLine($"{secKey.Key}={secKey.Value}");
                            inputKeys.RemoveAll(x =>
                                x.Section.Equals(section, StringComparison.OrdinalIgnoreCase) &&
                                x.Key != null && x.Key.Equals(secKey.Key, StringComparison.OrdinalIgnoreCase));
                        }

                        firstSection = false;
                    }
                }
            }
            #endregion

            // If file does not exist just create new file and insert keys.
            if (!File.Exists(filePath))
            {
                using (StreamWriter w = new StreamWriter(filePath, false, new UTF8Encoding(false)))
                {
                    FinalizeFile(w, null, false);
                }
                return true;
            }

            // Append IniKey into existing file
            bool result = false;
            string ext = Path.GetExtension(filePath);
            string tempPath = FileHelper.GetTempFile(ext);
            try
            {
                Encoding encoding = SmarterDetectEncoding(filePath, inputKeys);
                using (StreamReader r = new StreamReader(filePath, encoding, false))
                using (StreamWriter w = new StreamWriter(tempPath, false, encoding))
                {
                    bool inTargetSection = false;
                    string? currentSection = null;
                    List<(string Line, string RawLine)> lineBuffer = new List<(string, string)>(32);

                    #region FinalizeSection
                    void FinalizeSection()
                    {
                        Debug.Assert(currentSection != null);
                        List<IniKey> secKeys = inputKeys
                            .Where(x => x.Section.Equals(currentSection, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                        Debug.Assert(0 < secKeys.Count);

                        // Remove tailing empty lines 
                        for (int i = lineBuffer.Count - 1; 0 <= i; i--)
                        {
                            string targetLine = lineBuffer[i].Line;
                            if (targetLine.Length == 0)
                                lineBuffer.RemoveAt(i);
                            else
                                break;
                        }

                        // Check keys inside of lineBuffer, and find out which value should be overwritten.
                        foreach ((string targetLine, string targetRawLine) in lineBuffer)
                        {
                            int eIdx = targetLine.IndexOf('=');
                            if (eIdx == -1 || // No identifier '='
                                eIdx == 0 || // Key length is 0
                                IsLineComment(targetLine)) // Does line start with { ";", "#", "//" }?
                            {
                                w.WriteLine(targetRawLine);
                            }
                            else
                            {
                                // Overwrite if key=line already exists
                                bool processed = false;
                                ReadOnlySpan<char> targetKey = targetLine.AsSpan(0, eIdx).Trim();

                                // Call ToArray() to make copy of secKeys
                                foreach (IniKey secKey in secKeys.ToArray())
                                {
                                    if (targetKey.Equals(secKey.Key.AsSpan(), StringComparison.OrdinalIgnoreCase))
                                    {
                                        processed = true;
                                        w.WriteLine($"{secKey.Key}={secKey.Value}");

                                        // Remove processed keys
                                        inputKeys.RemoveAll(x =>
                                            x.Section.Equals(currentSection, StringComparison.OrdinalIgnoreCase) &&
                                            x.Key != null && x.Key.Equals(secKey.Key, StringComparison.OrdinalIgnoreCase));
                                        secKeys.RemoveAll(x => x.Key != null && x.Key.Equals(secKey.Key, StringComparison.OrdinalIgnoreCase));

                                        break;
                                    }
                                }

                                if (!processed)
                                    w.WriteLine(targetRawLine);
                            }
                        }

                        lineBuffer.Clear();

                        // Process remaining keys
                        // Call ToArray() to make copy of secKeys
                        foreach (IniKey secKey in secKeys.ToArray())
                        {
                            w.WriteLine($"{secKey.Key}={secKey.Value}");

                            // Remove processed keys
                            inputKeys.RemoveAll(x =>
                                x.Section.Equals(currentSection, StringComparison.OrdinalIgnoreCase) &&
                                x.Key != null && x.Key.Equals(secKey.Key, StringComparison.OrdinalIgnoreCase));
                            secKeys.RemoveAll(x =>
                                x.Key != null && x.Key.Equals(secKey.Key, StringComparison.OrdinalIgnoreCase));
                        }
                        w.WriteLine();

                        Debug.Assert(secKeys.Count == 0);
                    }
                    #endregion

                    bool firstLine = true;
                    ReadOnlySpan<char> lastLine = null;
                    while (true)
                    {
                        string? rawLine = r.ReadLine();
                        if (rawLine == null)
                        { // Last line!
                            if (inTargetSection)
                                FinalizeSection();

                            // Finalize file
                            FinalizeFile(w, lastLine, !firstLine);
                            break;
                        }

                        ReadOnlySpan<char> line = rawLine.AsSpan().Trim();

                        // Section head like [Process] encountered
                        if (IsLineSection(line, out ReadOnlySpan<char> foundSection))
                        {
                            // Finalize section
                            if (inTargetSection)
                                FinalizeSection();

                            // Write section name
                            w.WriteLine(rawLine);

                            if (inputKeys.Count == 0)
                            { // Job done, no more section parsing
                                inTargetSection = false;
                            }
                            else
                            {
                                string foundSectionStr = foundSection.ToString();
                                if (inputKeys.Any(x => foundSectionStr.Equals(x.Section, StringComparison.OrdinalIgnoreCase)))
                                {
                                    inTargetSection = true;
                                    currentSection = foundSectionStr;
                                }
                                else
                                {
                                    inTargetSection = false;
                                }
                            }
                        }
                        else
                        {
                            // Parse section only if corresponding iniKeys exist
                            if (inTargetSection)
                                lineBuffer.Add((line.ToString(), rawLine));
                            else // Pass-through
                                w.WriteLine(rawLine);
                        }

                        firstLine = false;
                        lastLine = line;
                    }
                }

                if (inputKeys.Count == 0)
                { // Success
                    FileHelper.FileReplaceEx(tempPath, filePath);
                    result = true;
                }
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }

            return result;
        }
        #endregion

        #region (Write) WriteCompactKey
        /// <summary>
        /// Write a pair of key and value into an .ini file, and also compact the file.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="section">Name of the target section.</param>
        /// <param name="key">Key to insert/overwrite.</param>
        /// <param name="value">New value to insert/overwrite.</param>
        /// <returns>Returns true if the operation was successful.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteCompactKey(string filePath, string section, string key, string value)
        {
            return InternalWriteCompactKeys(filePath, new List<IniKey> { new IniKey(section, key, value) });
        }

        /// <summary>
        /// Write a pair of key and value into an .ini file, and also compact the file.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="iniKey">Tuple of Section, Key, and Value.</param>
        /// <returns>Returns true if the operation was successful.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteKeyCompact(string filePath, IniKey iniKey)
        {
            return InternalWriteCompactKeys(filePath, new List<IniKey> { iniKey });
        }

        /// <summary>
        /// Write multiple pairs of key and value into an .ini file, and also compact the file.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="iniKeys">Enumberable tuples of Section, Key, and Value.</param>
        /// <returns>An array of return value for each IniKey. Returns true if the operation of an iniKey was successful.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteCompactKeys(string filePath, IEnumerable<IniKey> iniKeys)
        {
            return InternalWriteCompactKeys(filePath, iniKeys.ToList());
        }

        /// <summary>
        /// Write multiple pairs of key and value into an .ini file, and also compact the file.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="inputKeys">Enumberable tuples of Section, Key, and Value.</param>
        /// <returns>An array of return value for each IniKey. Returns true if the operation of an iniKey was successful.</returns>
        private static bool InternalWriteCompactKeys(string filePath, List<IniKey> inputKeys)
        {
            // null check
            if (inputKeys.Any(x => x.Key == null || x.Value == null))
                return false;

            #region FinalizeFile
            void FinalizeFile(StreamWriter w, ReadOnlySpan<char> lastLine, bool firstEmptyLine)
            {
                bool firstSection = true;
                if (0 < inputKeys.Count)
                {
                    // inputKeys are modified in the loop. Call ToArray() to clone inputKeys.
                    string[] unprocessedSections = inputKeys
                        .Select(x => x.Section)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    foreach (string section in unprocessedSections)
                    {
                        // inputKeys are modified in the loop. Call ToArray() to clone inputKeys.
                        IniKey[] secKeys = inputKeys
                            .Where(x => x.Section.Equals(section))
                            .ToArray();

                        if ((lastLine == null || lastLine.Length != 0) && (!firstSection || firstEmptyLine))
                            w.WriteLine();
                        w.WriteLine($"[{section}]");

                        foreach (IniKey secKey in secKeys)
                        {
                            w.WriteLine($"{secKey.Key}={secKey.Value}");
                            inputKeys.RemoveAll(x =>
                                x.Section.Equals(section, StringComparison.OrdinalIgnoreCase) &&
                                x.Key != null && x.Key.Equals(secKey.Key, StringComparison.OrdinalIgnoreCase));
                        }

                        firstSection = false;
                    }
                }
            }
            #endregion

            // If file does not exist just create new file and insert keys.
            if (!File.Exists(filePath))
            {
                using (StreamWriter w = new StreamWriter(filePath, false, new UTF8Encoding(false)))
                {
                    FinalizeFile(w, null, false);
                }
                return true;
            }

            // Append IniKey into existing file
            bool result = false;
            string tempPath = FileHelper.GetTempFile();
            try
            {
                Encoding encoding = SmarterDetectEncoding(filePath, inputKeys);
                using (StreamReader r = new StreamReader(filePath, encoding, false))
                using (StreamWriter w = new StreamWriter(tempPath, false, encoding))
                {
                    bool inTargetSection = false;
                    string? currentSection = null;
                    List<string> lineBuffers = new List<string>(32);

                    #region FinalizeSection
                    void FinalizeSection()
                    {
                        Debug.Assert(currentSection != null);
                        List<IniKey> secKeys = inputKeys
                            .Where(x => x.Section.Equals(currentSection, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                        Debug.Assert(0 < secKeys.Count);

                        // Remove tailing empty lines 
                        for (int i = lineBuffers.Count - 1; 0 <= i; i--)
                        {
                            string targetLine = lineBuffers[i];
                            if (targetLine.Length == 0)
                                lineBuffers.RemoveAt(i);
                            else
                                break;
                        }

                        // Check keys inside of lineBuffer, and find out which value should be overwritten.
                        foreach (string line in lineBuffers)
                        {
                            int eIdx = line.IndexOf('=');
                            if (eIdx == -1 || // No identifier '='
                                eIdx == 0 || // Key length is 0
                                IsLineComment(line)) // Does line start with { ";", "#", "//" }?
                            {
                                w.WriteLine(line.ToArray());
                            }
                            else
                            {
                                // Overwrite if key=line already exists
                                bool processed = false;
                                ReadOnlySpan<char> targetKey = line.AsSpan()[..eIdx].Trim();

                                // Call ToArray() to make copy of secKeys
                                foreach (IniKey secKey in secKeys.ToArray())
                                {
                                    if (targetKey.Equals(secKey.Key.AsSpan(), StringComparison.OrdinalIgnoreCase))
                                    {
                                        processed = true;
                                        w.WriteLine($"{secKey.Key}={secKey.Value}");

                                        // Remove processed keys
                                        inputKeys.RemoveAll(x =>
                                            x.Section.Equals(currentSection, StringComparison.OrdinalIgnoreCase) &&
                                            x.Key != null && x.Key.Equals(secKey.Key, StringComparison.OrdinalIgnoreCase));
                                        secKeys.RemoveAll(x =>
                                            x.Key != null && x.Key.Equals(secKey.Key, StringComparison.OrdinalIgnoreCase));

                                        break;
                                    }
                                }

                                if (!processed)
                                {
                                    string compacted = CompactKeyValuePairLine(line);
                                    w.WriteLine(compacted);
                                }

                            }
                        }

                        lineBuffers.Clear();

                        // Process remaining keys
                        // Call ToArray() to make copy of secKeys
                        foreach (IniKey secKey in secKeys.ToArray())
                        {
                            w.WriteLine($"{secKey.Key}={secKey.Value}");

                            // Remove processed keys
                            inputKeys.RemoveAll(x =>
                                x.Section.Equals(currentSection, StringComparison.OrdinalIgnoreCase) &&
                                x.Key != null && x.Key.Equals(secKey.Key, StringComparison.OrdinalIgnoreCase));
                            secKeys.RemoveAll(x =>
                                x.Key != null && x.Key.Equals(secKey.Key, StringComparison.OrdinalIgnoreCase));
                        }
                        w.WriteLine();

                        Debug.Assert(secKeys.Count == 0);
                    }
                    #endregion

                    bool firstLine = true;
                    ReadOnlySpan<char> lastLine = null;
                    while (true)
                    {
                        string? rawLine = r.ReadLine();
                        if (rawLine == null)
                        { // Last line!
                            if (inTargetSection)
                                FinalizeSection();

                            // Finalize file
                            FinalizeFile(w, lastLine, !firstLine);
                            break;
                        }

                        ReadOnlySpan<char> line = rawLine.AsSpan().Trim();

                        // Section head like [Process] encountered
                        if (IsLineSection(line, out ReadOnlySpan<char> foundSection))
                        {
                            // Finalize section
                            if (inTargetSection)
                                FinalizeSection();

                            // Write section name
                            w.WriteLine(line.ToString());

                            if (inputKeys.Count == 0)
                            { // Job done, no more section parsing
                                inTargetSection = false;
                            }
                            else
                            {
                                string foundSectionStr = foundSection.ToString();
                                if (inputKeys.Any(x => foundSectionStr.Equals(x.Section, StringComparison.OrdinalIgnoreCase)))
                                {
                                    inTargetSection = true;
                                    currentSection = foundSectionStr;
                                }
                                else
                                {
                                    inTargetSection = false;
                                }
                            }
                        }
                        else
                        {
                            if (inTargetSection)
                            { // Save section lines into a buffer, which will be flushed in FinalizeSection().
                                lineBuffers.Add(line.ToString());
                            }
                            else
                            { // Write compact or trimmed line
                                string compacted = CompactKeyValuePairLine(line);
                                w.WriteLine(compacted);
                            }
                        }

                        firstLine = false;
                        lastLine = line;
                    }
                }

                if (inputKeys.Count == 0)
                { // Success
                    FileHelper.FileReplaceEx(tempPath, filePath);
                    result = true;
                }
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }

            return result;
        }
        #endregion

        #region (Write) WriteRawLine
        /// <summary>
        /// Write a text line into the section.
        /// </summary>
        /// <param name="filePath">Ini file to manipulate</param>
        /// <param name="section">Name of the target section</param>
        /// <param name="rawLine">A text line to insert</param>
        /// <returns>If the operation was successful, returns true.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteRawLine(string filePath, string section, string rawLine)
        {
            return InternalWriteRawLine(filePath, new List<IniKey> { new IniKey(section, rawLine) }, true);
        }

        /// <summary>
        /// Write a text line into the section.
        /// </summary>
        /// <param name="filePath">Ini file to manipulate</param>
        /// <param name="section">Name of the target section</param>
        /// <param name="rawLine">A text line to insert</param>
        /// <param name="append">
        /// If set to true, a text line is inserted into the last line of the section. 
        /// If false, a text line is inserted into the first line of the section.
        /// </param>
        /// <returns>If the operation was successful, returns true.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteRawLine(string filePath, string section, string rawLine, bool append)
        {
            return InternalWriteRawLine(filePath, new List<IniKey> { new IniKey(section, rawLine) }, append);
        }

        /// <summary>
        /// Write a text line into the section.
        /// </summary>
        /// <param name="filePath">Ini file to manipulate</param>
        /// <param name="iniKey">Tuple of Section and RawLine. Key is treated as a text line.</param>
        /// <returns>If the operation was successful, returns true.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteRawLine(string filePath, IniKey iniKey)
        {
            return InternalWriteRawLine(filePath, new List<IniKey> { iniKey }, true);
        }

        /// <summary>
        /// Write a text line into the section.
        /// </summary>
        /// <param name="filePath">Ini file to manipulate</param>
        /// <param name="iniKey">Tuple of Section and RawLine. Key is treated as a text line.</param>
        /// <param name="append">
        /// If set to true, a text line is inserted into the last line of the section. 
        /// If false, a text line is inserted into the first line of the section.
        /// </param>
        /// <returns>If the operation was successful, returns true.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteRawLine(string filePath, IniKey iniKey, bool append)
        {
            return InternalWriteRawLine(filePath, new List<IniKey> { iniKey }, append);
        }

        /// <summary>
        /// Write text lines into the section.
        /// </summary>
        /// <param name="filePath">Ini file to manipulate</param>
        /// <param name="iniKeys">Enumarable tuples of Section and RawLine. Key is treated as a text line.</param>
        /// <returns>An array of return value for each IniKey. Returns true if the operation of an iniKey was successful.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteRawLines(string filePath, IEnumerable<IniKey> iniKeys)
        {
            return InternalWriteRawLine(filePath, iniKeys.ToList(), true);
        }

        /// <summary>
        /// Write text lines into the section.
        /// </summary>
        /// <param name="filePath">Ini file to manipulate</param>
        /// <param name="iniKeys">List of tuples of Section and RawLine. Key is treated as a text line.</param>
        /// <param name="append">
        /// If set to true, a text line is inserted into the last line of the section. 
        /// If false, a text line is inserted into the first line of the section.
        /// </param>
        /// <returns>An array of return value for each IniKey. Returns true if the operation of an iniKey was successful.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteRawLines(string filePath, IEnumerable<IniKey> iniKeys, bool append)
        {
            return InternalWriteRawLine(filePath, iniKeys.ToList(), append);
        }

        /// <summary>
        /// Write text lines into the section.
        /// </summary>
        /// <param name="filePath">Ini file to manipulate</param>
        /// <param name="iniKeys">List of tuples of Section and RawLine. Key is treated as a text line.</param>
        /// <param name="append">
        /// If set to true, a text line is inserted into the last line of the section. 
        /// If false, a text line is inserted into the first line of the section.
        /// </param>
        /// <returns>An array of return value for each IniKey. Returns true if the operation of an iniKey was successful.</returns>
        private static bool InternalWriteRawLine(string filePath, List<IniKey> iniKeys, bool append)
        {
            // null check
            if (iniKeys.Any(x => x.Key == null))
                return false;

            // If file do not exists or blank, just create new file and insert keys.
            if (!File.Exists(filePath))
            {
                using (StreamWriter writer = new StreamWriter(filePath, false, new UTF8Encoding(false)))
                {
                    string beforeSection = string.Empty;
                    for (int i = 0; i < iniKeys.Count; i++)
                    {
                        if (beforeSection.Equals(iniKeys[i].Section, StringComparison.OrdinalIgnoreCase) == false)
                        {
                            if (0 < i)
                                writer.WriteLine();
                            writer.WriteLine($"[{iniKeys[i].Section}]");
                        }

                        // File does not exists, so we don't have to consider "append"
                        writer.WriteLine(iniKeys[i].Key);

                        beforeSection = iniKeys[i].Section;
                    }
                    writer.Close();
                }
                return true;
            }

            bool result = false;
            List<int> processedKeys = new List<int>(iniKeys.Count);
            string tempPath = FileHelper.GetTempFile();
            try
            {
                Encoding encoding = SmarterDetectEncoding(filePath, iniKeys);
                using (StreamReader reader = new StreamReader(filePath, encoding, true))
                using (StreamWriter writer = new StreamWriter(tempPath, false, encoding))
                {
                    string? rawLine = null;
                    bool inTargetSection = false;
                    ReadOnlySpan<char> currentSection = null;
                    List<string> processedSections = new List<string>(iniKeys.Count);

                    // Is Original File Empty?
                    if (reader.Peek() == -1)
                    {
                        reader.Close();

                        // Write all and exit
                        string beforeSection = string.Empty;
                        for (int i = 0; i < iniKeys.Count; i++)
                        {
                            if (!beforeSection.Equals(iniKeys[i].Section, StringComparison.OrdinalIgnoreCase))
                            {
                                if (0 < i)
                                    writer.WriteLine();
                                writer.WriteLine($"[{iniKeys[i].Section}]");
                            }

                            // File is blank, so we don't have to consider "append"
                            writer.WriteLine(iniKeys[i].Key);

                            beforeSection = iniKeys[i].Section;
                        }
                        writer.Close();
                        FileHelper.FileReplaceEx(tempPath, filePath);
                        return true;
                    }

                    // Main Logic
                    while ((rawLine = reader.ReadLine()) != null)
                    { // Read text line by line
                        bool thisLineWritten = false;
                        ReadOnlySpan<char> line = rawLine.AsSpan().Trim();

                        // Ignore comments. If you wrote all keys successfully, also skip.
                        if (iniKeys.Count == 0 || IsLineComment(line))
                        {
                            thisLineWritten = true;
                            writer.WriteLine(rawLine);
                        }
                        else
                        {
                            // Check if encountered section head Ex) [Process]
                            if (IsLineSection(line, out ReadOnlySpan<char> foundSection))
                            {
                                // Append Mode : Add to last line of section
                                if (append && inTargetSection)
                                { // End of targetSection and start of foundSection
                                    for (int i = 0; i < iniKeys.Count; i++)
                                    {
                                        if (processedKeys.Contains(i))
                                            continue;

                                        // Add to last line of foundSection
                                        if (currentSection.Equals(iniKeys[i].Section.AsSpan(), StringComparison.OrdinalIgnoreCase))
                                        {
                                            processedKeys.Add(i);
                                            writer.WriteLine(iniKeys[i].Key);
                                        }
                                    }
                                }

                                // Start of the section
                                inTargetSection = false;
                                // Only sections contained in iniKeys will be targeted
                                for (int i = 0; i < iniKeys.Count; i++)
                                {
                                    if (processedKeys.Contains(i))
                                        continue;

                                    if (foundSection.Equals(iniKeys[i].Section.AsSpan(), StringComparison.OrdinalIgnoreCase))
                                    {
                                        inTargetSection = true;
                                        currentSection = foundSection;
                                        processedSections.Add(currentSection.ToString());
                                        break; // for shorter O(n)
                                    }
                                }

                                // Non-Append Mode : Add to first line of section
                                if (!append && inTargetSection)
                                {
                                    for (int i = 0; i < iniKeys.Count; i++)
                                    {
                                        if (processedKeys.Contains(i))
                                            continue;

                                        // Add to last line of foundSection
                                        if (currentSection.Equals(iniKeys[i].Section.AsSpan(), StringComparison.OrdinalIgnoreCase))
                                        {
                                            if (!thisLineWritten)
                                                writer.WriteLine(rawLine);
                                            thisLineWritten = true;

                                            processedKeys.Add(i);
                                            writer.WriteLine(iniKeys[i].Key);
                                        }
                                    }

                                    inTargetSection = false;
                                }
                            }

                            // Blank line - for Append Mode
                            if (line.Length == 0)
                            {
                                if (append && inTargetSection)
                                {
                                    for (int i = 0; i < iniKeys.Count; i++)
                                    {
                                        if (processedKeys.Contains(i))
                                            continue;

                                        if (currentSection.Equals(iniKeys[i].Section.AsSpan(), StringComparison.OrdinalIgnoreCase))
                                        { // append key to section
                                            processedKeys.Add(i);
                                            writer.WriteLine(iniKeys[i].Key);
                                        }
                                    }
                                }
                                thisLineWritten = true;
                                writer.WriteLine();
                            }
                        }

                        // End of file
                        if (reader.Peek() == -1)
                        {
                            // Append Mode : Add to last line of section
                            if (append && inTargetSection)
                            { // End of targetSection and start of foundSection
                                for (int i = 0; i < iniKeys.Count; i++)
                                {
                                    if (processedKeys.Contains(i))
                                        continue;

                                    // Add to last line of foundSection
                                    if (currentSection.Equals(iniKeys[i].Section.AsSpan(), StringComparison.OrdinalIgnoreCase))
                                    {
                                        processedKeys.Add(i);

                                        if (!thisLineWritten)
                                            writer.WriteLine(rawLine);
                                        thisLineWritten = true;

                                        writer.WriteLine(iniKeys[i].Key);
                                    }
                                }
                            }

                            if (!append)
                            {
                                if (!thisLineWritten)
                                    writer.WriteLine(rawLine);
                                thisLineWritten = true;
                            }

                            // Not in section, so create new section
                            for (int i = 0; i < iniKeys.Count; i++)
                            { // At this time, only not found section remains in iniKeys
                                if (processedKeys.Contains(i))
                                    continue;

                                if (!processedSections.Any(s => s.Equals(iniKeys[i].Section, StringComparison.OrdinalIgnoreCase)))
                                {
                                    if (!thisLineWritten)
                                        writer.WriteLine(rawLine);
                                    thisLineWritten = true;

                                    processedSections.Add(iniKeys[i].Section);
                                    writer.WriteLine($"\r\n[{iniKeys[i].Section}]");
                                }

                                processedKeys.Add(i);

                                writer.WriteLine(iniKeys[i].Key);
                            }
                        }

                        if (!thisLineWritten)
                            writer.WriteLine(rawLine);
                    }
                }

                if (processedKeys.Count == iniKeys.Count)
                {
                    result = true;
                    FileHelper.FileReplaceEx(tempPath, filePath);
                }
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }

            return result;
        }
        #endregion

        #region (Write) RenameKey
        /// <summary>
        /// Rename oldKey into newKey when a pair of key and value is found from an .ini file.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="section">Name of the target section.</param>
        /// <returns>Returns true if the operation was successful.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RenameKey(string filePath, string section, string oldKey, string newKey)
        {
            return InternalRenameKeys(filePath, new IniKey[] { new IniKey(section, oldKey, newKey) })[0];
        }

        /// <summary>
        /// Rename oldKey into newKey when a pair of key and value is found from an .ini file.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="iniKey">Tuple of Section, OldKey, and NewKey. Value is treated as NewKey.</param>
        /// <returns>Returns true if the operation was successful.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RenameKey(string filePath, IniKey iniKey)
        {
            return InternalRenameKeys(filePath, new IniKey[] { iniKey })[0];
        }

        /// <summary>
        /// Rename oldKey into newKey, when a pair of key and value is found from an .ini file.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="iniKeys">Enumarable tuples of Section, OldKey, and NewKey. Value is treated as NewKey.</param>
        /// <returns>Returns true if the operation was successful.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool[] RenameKeys(string filePath, IEnumerable<IniKey> iniKeys)
        {
            return InternalRenameKeys(filePath, iniKeys.ToArray());
        }

        /// <summary>
        /// Internal method for RenameKeys
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="iniKeys">Use Value for new names of Key</param>
        /// <returns>Returns true if the operation was successful.</returns>
        private static bool[] InternalRenameKeys(string filePath, IniKey[] iniKeys)
        {
            bool[] processed = new bool[iniKeys.Length];
            for (int i = 0; i < iniKeys.Length; i++)
                processed[i] = false;

            // null check
            if (iniKeys.Any(x => x.Key == null || x.Value == null))
                return processed;

            // file check
            if (!File.Exists(filePath))
                return processed; // All False

            string tempPath = FileHelper.GetTempFile();
            try
            {
                Encoding encoding = SmarterDetectEncoding(filePath, iniKeys);
                using (StreamReader r = new StreamReader(filePath, encoding, false))
                using (StreamWriter w = new StreamWriter(tempPath, false, encoding))
                {
                    if (r.Peek() == -1)
                    {
                        r.Close();
                        return processed; // All False
                    }

                    string? rawLine;
                    bool inTargetSection = false;
                    ReadOnlySpan<char> currentSection = null;

                    while ((rawLine = r.ReadLine()) != null)
                    { // Read text line by line
                        bool thisLineProcessed = false;
                        ReadOnlySpan<char> line = rawLine.AsSpan().Trim();

                        // Ignore comments. If you deleted all keys successfully, also skip.
                        if (!processed.Any(x => !x) || IsLineComment(line))
                        {
                            w.WriteLine(rawLine);
                            continue;
                        }

                        // Check if encountered section head Ex) [Process]
                        if (IsLineSection(line, out ReadOnlySpan<char> foundSection))
                        {
                            // Start of the section
                            inTargetSection = false;
                            // Only sections contained in iniKeys will be targeted
                            for (int i = 0; i < iniKeys.Length; i++)
                            {
                                if (processed[i])
                                    continue;

                                if (foundSection.Equals(iniKeys[i].Section, StringComparison.OrdinalIgnoreCase))
                                {
                                    inTargetSection = true;
                                    currentSection = foundSection;
                                    break; // for shorter O(n)
                                }
                            }
                            thisLineProcessed = true;
                            w.WriteLine(rawLine);
                        }

                        // key=value
                        int idx = line.IndexOf('=');
                        if (idx != -1 && idx != 0) // Key exists
                        {
                            if (inTargetSection) // process here only if we are in target section
                            {
                                ReadOnlySpan<char> lineKey = line[..idx].Trim();
                                ReadOnlySpan<char> lineValue = line[(idx + 1)..].Trim();
                                for (int i = 0; i < iniKeys.Length; i++)
                                {
                                    if (processed[i])
                                        continue;

                                    IniKey key = iniKeys[i];
                                    if (currentSection.Equals(key.Section, StringComparison.OrdinalIgnoreCase)
                                        && lineKey.Equals(key.Key, StringComparison.OrdinalIgnoreCase))
                                    { // key exists, so do not write this line, which lead to 'deletion'
                                        w.WriteLine($"{key.Value}={lineValue.ToString()}");
                                        thisLineProcessed = true;
                                        processed[i] = true;
                                    }
                                }
                            }
                        }

                        if (!thisLineProcessed)
                            w.WriteLine(rawLine);
                    }
                }

                if (processed.Any(x => x))
                    FileHelper.FileReplaceEx(tempPath, filePath);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }

            return processed;
        }
        #endregion

        #region (Write) DeleteKey
        /// <summary>
        /// Delete target key and its value from an .ini file.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="section">Name of the target section.</param>
        /// <returns>Returns true if the operation was successful.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool DeleteKey(string filePath, string section, string key)
        {
            return InternalDeleteKeys(filePath, new IniKey[] { new IniKey(section, key) })[0];
        }

        /// <summary>
        /// Delete target key and its value from an .ini file.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="iniKey">Tuple of Section and Key.</param>
        /// <returns>Returns true if the operation was successful.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool DeleteKey(string filePath, IniKey iniKey)
        {
            return InternalDeleteKeys(filePath, new IniKey[] { iniKey })[0];
        }

        /// <summary>
        /// Delete target keys and their value from an .ini file.
        /// </summary>
        /// <param name="iniKeys">Enumerable tuples of Section and Key.</param>
        /// <returns>
        /// An array of return value for each IniKey.
        /// Returns true if the operation of an iniKey was successful.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool[] DeleteKeys(string filePath, IEnumerable<IniKey> iniKeys)
        {
            return InternalDeleteKeys(filePath, iniKeys.ToArray());
        }

        /// <summary>
        /// Delete target keys and their value from an .ini file.
        /// </summary>
        /// <param name="iniKeys">Enumerable tuples of Section and Key.</param>
        /// <returns>
        /// An array of return value for each IniKey.
        /// Returns true if the operation of an iniKey was successful.
        /// </returns>
        private static bool[] InternalDeleteKeys(string filePath, IniKey[] iniKeys)
        {
            bool[] processed = new bool[iniKeys.Length];
            for (int i = 0; i < processed.Length; i++)
                processed[i] = false;

            // null check
            if (iniKeys.Any(x => x.Key == null))
                return processed;

            // file check
            if (!File.Exists(filePath))
                return processed;

            string ext = Path.GetExtension(filePath);
            string tempPath = FileHelper.GetTempFile(ext);
            try
            {
                Encoding encoding = SmarterDetectEncoding(filePath, iniKeys);
                using (StreamReader r = new StreamReader(filePath, encoding, false))
                using (StreamWriter w = new StreamWriter(tempPath, false, encoding))
                {
                    if (r.Peek() == -1)
                    { // All False
                        return processed;
                    }

                    string? rawLine;
                    bool inTargetSection = false;
                    ReadOnlySpan<char> currentSection = null;

                    while ((rawLine = r.ReadLine()) != null)
                    { // Read text line by line
                        bool thisLineProcessed = false;
                        ReadOnlySpan<char> line = rawLine.AsSpan().Trim();

                        // Ignore comments. If you deleted all keys successfully, also skip.
                        if (!processed.Any(x => !x) || IsLineComment(line))
                        {
                            w.WriteLine(rawLine);
                            continue;
                        }

                        // Check if encountered section head Ex) [Process]
                        if (IsLineSection(line, out ReadOnlySpan<char> foundSection))
                        {
                            // Start of the section
                            inTargetSection = false;
                            // Only sections contained in iniKeys will be targeted
                            for (int i = 0; i < iniKeys.Length; i++)
                            {
                                if (processed[i])
                                    continue;

                                if (foundSection.Equals(iniKeys[i].Section, StringComparison.OrdinalIgnoreCase))
                                {
                                    inTargetSection = true;
                                    currentSection = foundSection;
                                    break; // for shorter O(n)
                                }
                            }
                            thisLineProcessed = true;
                            w.WriteLine(rawLine);
                        }

                        // key=value
                        int idx = line.IndexOf('=');
                        if (idx != -1 && idx != 0) // Key exists
                        {
                            if (inTargetSection) // process here only if we are in target section
                            {
                                ReadOnlySpan<char> lineKey = line[..idx].Trim();
                                for (int i = 0; i < iniKeys.Length; i++)
                                {
                                    if (processed[i])
                                        continue;

                                    if (currentSection.Equals(iniKeys[i].Section, StringComparison.OrdinalIgnoreCase) &&
                                        lineKey.Equals(iniKeys[i].Key, StringComparison.OrdinalIgnoreCase))
                                    { // key exists, so do not write this line, which lead to 'deletion'
                                        thisLineProcessed = true;
                                        processed[i] = true;
                                    }
                                }
                            }
                        }

                        if (!thisLineProcessed)
                            w.WriteLine(rawLine);
                    }
                }

                if (processed.Any(x => x))
                    FileHelper.FileReplaceEx(tempPath, filePath);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }

            return processed;
        }
        #endregion

        #region (Write) DeleteCompactKey
        /// <summary>
        /// Delete target key and its value from an .ini file, and also compact the file.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="section">Name of the target section.</param>
        /// <returns>Returns true if the operation was successful.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool DeleteCompactKey(string filePath, string section, string key)
        {
            return InternalDeleteCompactKeys(filePath, new IniKey[] { new IniKey(section, key) })[0];
        }

        /// <summary>
        /// Delete target key and its value from an .ini file, and also compact the file.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="iniKey">Tuple of Section and Key.</param>
        /// <returns>Returns true if the operation was successful.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool DeleteCompactKey(string filePath, IniKey iniKey)
        {
            return InternalDeleteCompactKeys(filePath, new IniKey[] { iniKey })[0];
        }

        /// <summary>
        /// Delete target keys and their value from an .ini file, and also compact the file.
        /// </summary>
        /// <param name="iniKeys">Enumerable tuples of Section and Key.</param>
        /// <returns>An array of return value for each IniKey. Returns true if the operation of an iniKey was successful.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool[] DeleteCompactKeys(string filePath, IEnumerable<IniKey> iniKeys)
        {
            return InternalDeleteCompactKeys(filePath, iniKeys.ToArray());
        }

        /// <summary>
        /// Delete target keys and their value from an .ini file, and also compact the file.
        /// </summary>
        /// <param name="iniKeys">Enumerable tuples of Section and Key.</param>
        /// <returns>An array of return value for each IniKey. Returns true if the operation of an iniKey was successful.</returns>
        private static bool[] InternalDeleteCompactKeys(string filePath, IniKey[] iniKeys)
        {
            bool[] processed = new bool[iniKeys.Length];
            for (int i = 0; i < processed.Length; i++)
                processed[i] = false;

            // Null check
            if (iniKeys.Any(x => x.Key == null))
                return processed;

            // File check
            if (!File.Exists(filePath))
                return processed; // All False

            string tempPath = FileHelper.GetTempFile();
            try
            {
                Encoding encoding = SmarterDetectEncoding(filePath, iniKeys);
                using (StreamReader r = new StreamReader(filePath, encoding, false))
                using (StreamWriter w = new StreamWriter(tempPath, false, encoding))
                {
                    if (r.Peek() == -1)
                    {
                        r.Close();
                        return processed; // All False
                    }

                    string? rawLine;
                    bool inTargetSection = false;
                    ReadOnlySpan<char> currentSection = null;

                    while ((rawLine = r.ReadLine()) != null)
                    { // Read text line by line
                        bool thisLineProcessed = false;
                        ReadOnlySpan<char> line = rawLine.AsSpan().Trim();

                        // Ignore comments. If you deleted all keys successfully, also skip.
                        if (IsLineComment(line))
                        {
                            w.WriteLine(line.ToString());
                            continue;
                        }

                        // Check if encountered section head Ex) [Process]
                        if (IsLineSection(line, out ReadOnlySpan<char> foundSection))
                        {
                            // Start of the section
                            inTargetSection = false;
                            // Only sections contained in iniKeys will be targeted
                            for (int i = 0; i < iniKeys.Length; i++)
                            {
                                if (processed[i])
                                    continue;

                                if (foundSection.Equals(iniKeys[i].Section, StringComparison.OrdinalIgnoreCase))
                                {
                                    inTargetSection = true;
                                    currentSection = foundSection;
                                    break; // for shorter O(n)
                                }
                            }
                            thisLineProcessed = true;
                            w.WriteLine(line.ToString());
                        }

                        // key=value
                        int idx = line.IndexOf('=');
                        if (idx != -1 && idx != 0) // Key exists
                        {
                            if (inTargetSection) // process here only if we are in target section
                            {
                                ReadOnlySpan<char> lineKey = line[..idx].Trim();
                                for (int i = 0; i < iniKeys.Length; i++)
                                {
                                    if (processed[i])
                                        continue;

                                    if (currentSection.Equals(iniKeys[i].Section, StringComparison.OrdinalIgnoreCase) &&
                                        lineKey.Equals(iniKeys[i].Key, StringComparison.OrdinalIgnoreCase))
                                    { // key exists, do not write this line to 'delete' them.
                                        thisLineProcessed = true;
                                        processed[i] = true;
                                    }
                                }
                            }
                        }

                        if (!thisLineProcessed)
                        {
                            string compacted = CompactKeyValuePairLine(line);
                            w.WriteLine(compacted);
                        }
                    }
                }

                if (processed.Any(x => x))
                    FileHelper.FileReplaceEx(tempPath, filePath);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }

            return processed;
        }
        #endregion

        #region (Read) ReadSection
        /// <summary>
        /// Read entire pairs of key and value from an ini file section.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="section">Name of the section to read.</param>
        /// <returns>
        /// An array of IniKey. Each IniKey represents a pair of key and value.
        /// Return null if section was not found.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IniKey[]? ReadSection(string filePath, string section)
        {
            return InternalReadSection(filePath, new string[] { section }).Select(x => x.Value).First();
        }

        /// <summary>
        /// Read entire pairs of key and value from an ini file section.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="iniKey">Name of the section to read. Key and Value is ignored.</param>
        /// <returns>
        /// An array of IniKey. Each IniKey represents a pair of key and value.
        /// Return null if section was not found.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IniKey[]? ReadSection(string filePath, IniKey iniKey)
        {
            return InternalReadSection(filePath, new string[] { iniKey.Section }).Select(x => x.Value).First();
        }

        /// <summary>
        /// Read entire pairs of key and value from multiple ini file sections.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="sections">Enumerable names of the section to read.</param>
        /// <returns>
        /// Dictionary of section name (key) and section body (value). 
        /// Value of Dictionary is an array of IniKey. 
        /// Each IniKey represents a pair of key and value.
        /// Dictionary value is null if section was not found.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Dictionary<string, IniKey[]?> ReadSections(string filePath, IEnumerable<string> sections)
        {
            return InternalReadSection(filePath, sections.ToArray());
        }

        /// <summary>
        /// Read entire pairs of key and value from multiple ini file sections.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="iniKeys">Enumerable names of the section to read. Key and Value is ignored.</param>
        /// <returns>
        /// Dictionary of section name (key) and section body (value). 
        /// Value of Dictionary is an array of IniKey. 
        /// Each IniKey represents a pair of key and value.
        /// Dictionary value is null if section was not found.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Dictionary<string, IniKey[]?> ReadSections(string filePath, IEnumerable<IniKey> iniKeys)
        {
            return InternalReadSection(filePath, iniKeys.Select(x => x.Section).ToArray());
        }

        /// <summary>
        /// Read entire pairs of key and value from multiple ini file sections.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="sections">Enumerable names of the section to read.</param>
        /// <returns>
        /// Dictionary of section name (key) and section body (value). 
        /// Value of Dictionary is an array of IniKey. 
        /// Each IniKey represents a pair of key and value.
        /// Dictionary value is null if section was not found.
        /// </returns>
        private static Dictionary<string, IniKey[]?> InternalReadSection(string filePath, string[] sections)
        {
            Dictionary<string, List<IniKey>?> secDict = new Dictionary<string, List<IniKey>?>(StringComparer.OrdinalIgnoreCase);
            foreach (string section in sections)
                secDict[section] = null;

            Encoding encoding = SmarterDetectEncoding(filePath, sections);
            using (StreamReader reader = new StreamReader(filePath, encoding, true))
            {
                string? rawLine;
                bool inTargetSection = false;
                string? currentSection = null;
                List<IniKey>? currentIniKeys = null;

                while ((rawLine = reader.ReadLine()) != null)
                { // Read text line by line
                    ReadOnlySpan<char> line = rawLine.AsSpan().Trim(); // Remove whitespace
                    if (IsLineComment(line)) // Ignore comment
                        continue;

                    if (inTargetSection && currentIniKeys != null)
                    {
                        Debug.Assert(currentSection != null);

                        int idx = line.IndexOf('=');
                        if (idx != -1 && idx != 0) // there is key, and key name is not empty
                        {
                            string key = line[..idx].Trim().ToString();
                            string value = line[(idx + 1)..].Trim().ToString();
                            currentIniKeys.Add(new IniKey(currentSection, key, value));
                        }
                        else
                        {
                            // Search if current section ended
                            if (IsLineSection(line, out ReadOnlySpan<char> foundSection))
                            {
                                // Only sections contained in sectionNames will be targeted
                                inTargetSection = false;
                                currentSection = null;

                                string foundSectionStr = foundSection.ToString();
                                int sIdx = Array.FindIndex(sections, x => x.Equals(foundSectionStr, StringComparison.OrdinalIgnoreCase));
                                if (sIdx != -1)
                                {
                                    inTargetSection = true;
                                    currentSection = foundSectionStr;
                                    if (secDict[currentSection] == null)
                                        secDict[currentSection] = new List<IniKey>(16);
                                    currentIniKeys = secDict[currentSection];
                                }
                            }
                        }
                    }
                    else
                    { // not in section
                        // Check if encountered section head Ex) [Process]
                        if (IsLineSection(line, out ReadOnlySpan<char> foundSection))
                        {
                            // Only sections contained in iniKeys will be targeted
                            string foundSectionStr = foundSection.ToString();
                            int sIdx = Array.FindIndex(sections, x => x.Equals(foundSectionStr, StringComparison.OrdinalIgnoreCase));
                            if (sIdx != -1)
                            {
                                inTargetSection = true;
                                currentSection = foundSectionStr;
                                if (secDict[currentSection] == null)
                                    secDict[currentSection] = new List<IniKey>(16);
                                currentIniKeys = secDict[currentSection];
                            }
                        }
                    }
                }
            }

            return secDict.ToDictionary(x => x.Key, x => x.Value?.ToArray(), StringComparer.OrdinalIgnoreCase);
        }
        #endregion

        #region (Write) AddSection
        /// <summary>
        /// Add a section into an .ini file.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="section">Name of the section to create.</param>
        /// <returns>Returns true if the operation was successful.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AddSection(string filePath, string section)
        {
            return InternalAddSection(filePath, new List<string> { section });
        }

        /// <summary>
        /// Write a pair of key and value into an .ini file.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="iniKey">Name of the section to create. Key and Value is ignored.</param>
        /// <returns>Returns true if the operation was successful.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AddSection(string filePath, IniKey iniKey)
        {
            return InternalAddSection(filePath, new List<string> { iniKey.Section });
        }

        /// <summary>
        /// Write a pair of key and value into an .ini file.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="sections">Enumerable names of the section to create.</param>
        /// <returns>Returns true if the operation was successful.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AddSections(string filePath, IEnumerable<string> sections)
        {
            return InternalAddSection(filePath, sections.ToList());
        }

        /// <summary>
        /// Write a pair of key and value into an .ini file.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="iniKeys">Enumerable names of the section to create. Key and Value is ignored.</param>
        /// <returns>Returns true if the operation was successful.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AddSections(string filePath, IEnumerable<IniKey> iniKeys)
        {
            return InternalAddSection(filePath, iniKeys.Select(x => x.Section).ToList());
        }

        private static bool InternalAddSection(string filePath, List<string> sections)
        {
            // Not exist -> Create and exit
            if (!File.Exists(filePath))
            {
                using (StreamWriter writer = new StreamWriter(filePath, false, new UTF8Encoding(false)))
                {
                    for (int i = 0; i < sections.Count; i++)
                    {
                        if (0 < i)
                            writer.WriteLine();
                        writer.WriteLine($"[{sections[i]}]");
                    }

                    writer.Close();
                }
                return true;
            }

            bool result = true;
            string tempPath = FileHelper.GetTempFile();
            try
            {
                Encoding encoding = SmarterDetectEncoding(filePath, sections);
                using (StreamReader r = new StreamReader(filePath, encoding, false))
                using (StreamWriter w = new StreamWriter(tempPath, false, encoding))
                {
                    // Is Original File Empty?
                    if (r.Peek() == -1)
                    {
                        r.Close();

                        // Write all and exit
                        for (int i = 0; i < sections.Count; i++)
                        {
                            if (0 < i)
                                w.WriteLine();
                            w.WriteLine($"[{sections[i]}]");
                        }

                        w.Close();

                        FileHelper.FileReplaceEx(tempPath, filePath);
                        return true;
                    }

                    List<string> processedSections = new List<string>(sections.Count);

                    string? rawLine;
                    while ((rawLine = r.ReadLine()) != null)
                    { // Read text line by line
                        bool thisLineProcessed = false;
                        ReadOnlySpan<char> line = rawLine.AsSpan().Trim();

                        // Check if encountered section head Ex) [Process]
                        if (IsLineSection(line, out ReadOnlySpan<char> foundSection))
                        {
                            // Start of the section;
                            // Only sections contained in iniKeys will be targeted
                            for (int i = 0; i < sections.Count; i++)
                            {
                                if (foundSection.Equals(sections[i].AsSpan(), StringComparison.OrdinalIgnoreCase))
                                {
                                    processedSections.Add(foundSection.ToString());
                                    sections.RemoveAt(i);
                                    break; // for shorter O(n)
                                }
                            }
                            thisLineProcessed = true;
                            w.WriteLine(rawLine);
                        }

                        if (!thisLineProcessed)
                            w.WriteLine(rawLine);

                        // End of file
                        if (r.Peek() == -1)
                        { // If sections were not added, add it now
                            List<int> processedIdxs = new List<int>(sections.Count);
                            for (int i = 0; i < sections.Count; i++)
                            { // At this time, only not found section remains in iniKeys
                                if (processedSections.Any(s => s.Equals(sections[i], StringComparison.OrdinalIgnoreCase)) == false)
                                {
                                    processedSections.Add(sections[i]);
                                    w.WriteLine($"\r\n[{sections[i]}]");
                                }
                                processedIdxs.Add(i);
                            }
                            foreach (int i in processedIdxs.OrderByDescending(x => x))
                                sections.RemoveAt(i);
                        }
                    }
                }

                if (sections.Count == 0)
                {
                    FileHelper.FileReplaceEx(tempPath, filePath);
                    result = true;
                }
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }

            return result;
        }
        #endregion

        #region (Write) WriteSectionFast
        /// <summary>
        /// Write one section with fast speed.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="section">Name of the target section.</param>
        /// <param name="lines">Content of the target section to write.</param>
        /// <remarks>
        /// The method acheives its fast performance by caring only one section.
        /// Designed to be used with EncodedFile.Encode().
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteSectionFast(string filePath, string section, IList<string> lines)
        {
            return InternalWriteSectionFast(filePath, section, lines);
        }

        /// <summary>
        /// Write one section with fast speed.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="section">Name of the target section.</param>
        /// <param name="str">Content of the target section to write.</param>
        /// <remarks>
        /// The method acheives its fast performance by caring only one section.
        /// Designed to be used with EncodedFile.Encode().
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteSectionFast(string filePath, string section, string str)
        {
            return InternalWriteSectionFast(filePath, section, str);
        }

        /// <summary>
        /// Write one section with fast speed.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="section">Name of the target section.</param>
        /// <param name="tr">TextReader to copy its content.</param>
        /// <remarks>
        /// The method acheives its fast performance by caring only one section.
        /// Designed to be used with EncodedFile.Encode().
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteSectionFast(string filePath, string section, TextReader tr)
        {
            return InternalWriteSectionFast(filePath, section, tr);
        }

        private static bool InternalWriteSectionFast(string filePath, string section, object content)
        {
            void WriteContent(TextWriter w)
            {
                w.WriteLine($"[{section}]");
                switch (content)
                {
                    case string str:
                        w.WriteLine(str);
                        break;
                    case IList<string> strs:
                        foreach (string str in strs)
                            w.WriteLine(str);
                        break;
                    case IReadOnlyList<string> strs:
                        foreach (string str in strs)
                            w.WriteLine(str);
                        break;
                    case TextReader tr:
                        string? readLine;
                        while ((readLine = tr.ReadLine()) != null)
                            w.WriteLine(readLine);
                        break;
                    default:
                        throw new ArgumentException("Invalid content", nameof(content));
                }
            }

            // If file does not exist or blank, just create new file and insert keys.
            if (!File.Exists(filePath))
            {
                using (StreamWriter w = new StreamWriter(filePath, false, new UTF8Encoding(false)))
                {
                    WriteContent(w);
                }

                return true;
            }

            string tempPath = FileHelper.GetTempFile();
            try
            {
                bool finished = false;
                Encoding encoding = EncodingHelper.DetectEncoding(filePath);
                using (StreamReader r = new StreamReader(filePath, encoding, false))
                using (StreamWriter w = new StreamWriter(tempPath, false, encoding))
                {
                    string? rawLine;
                    bool passThisSection = false;

                    // Main Logic
                    while ((rawLine = r.ReadLine()) != null)
                    { // Read text line by line
                        ReadOnlySpan<char> line = rawLine.AsSpan().Trim();

                        // Check if encountered section head Ex) [Process]
                        if (IsLineSection(line, out ReadOnlySpan<char> foundSection))
                        {
                            passThisSection = false;

                            if (foundSection.Equals(section, StringComparison.OrdinalIgnoreCase))
                            {
                                WriteContent(w);

                                passThisSection = true;
                                finished = true;
                            }
                        }

                        if (!passThisSection)
                            w.WriteLine(rawLine);
                    }

                    // End of file
                    if (!finished)
                    {
                        WriteContent(w);
                    }
                }

                FileHelper.FileReplaceEx(tempPath, filePath);
                return true;
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }
        #endregion

        #region (Write) RenameSection
        /// <summary>
        /// Rename specified section name into the new name.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="srcSection">Old name of the target section.</param>
        /// <param name="destSection">New name of the target section.</param>
        /// <returns>Returns true if the operation was successful.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RenameSection(string filePath, string srcSection, string destSection)
        {
            return InternalRenameSection(filePath, new IniKey[] { new IniKey(srcSection, destSection) })[0];
        }

        /// <summary>
        /// Rename specified section name into the new name.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="iniKey">
        /// A tuple of old section name and new section name. 
        /// Section is treated as old name, and Key is treated as new name.
        /// </param>
        /// <returns>Returns true if the operation was successful.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RenameSection(string filePath, IniKey iniKey)
        {
            return InternalRenameSection(filePath, new IniKey[] { iniKey })[0];
        }

        /// <summary>
        /// Rename specified section names into the new names.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="iniKeys">
        /// Enumerable tuples of old section name and new section name. 
        /// Section is treated as old name, and Key is treated as new name.
        /// </param>
        /// <returns>An array of return value for each IniKey. Returns true if the operation of an iniKey was successful.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool[] RenameSections(string filePath, IEnumerable<IniKey> iniKeys)
        {
            return InternalRenameSection(filePath, iniKeys.ToArray());
        }

        /// <summary>
        /// Rename specified section names into the new names.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="iniKeys">
        /// Enumerable tuples of old section name and new section name. 
        /// Section is treated as old name, and Key is treated as new name.
        /// </param>
        /// <returns>An array of return value for each IniKey. Returns true if the operation of an iniKey was successful.</returns>
        private static bool[] InternalRenameSection(string filePath, IniKey[] iniKeys)
        {
            bool[] processed = new bool[iniKeys.Length];
            for (int i = 0; i < processed.Length; i++)
                processed[i] = false;

            // Null check
            if (iniKeys.Any(x => x.Key == null))
                return processed;

            // File check
            if (!File.Exists(filePath))
                return processed;

            string tempPath = FileHelper.GetTempFile();
            try
            {
                Encoding encoding = SmarterDetectEncoding(filePath, iniKeys);
                using (StreamReader r = new StreamReader(filePath, encoding, false))
                using (StreamWriter w = new StreamWriter(tempPath, false, encoding))
                {
                    if (r.Peek() == -1)
                    {
                        r.Close();
                        return processed;
                    }

                    string? rawLine = null;

                    // Main Logic
                    while ((rawLine = r.ReadLine()) != null)
                    { // Read text line by line
                        ReadOnlySpan<char> line = rawLine.AsSpan().Trim();
                        bool thisLineProcessed = false;

                        // Check if encountered section head Ex) [Process]
                        if (IsLineSection(line, out ReadOnlySpan<char> foundSection))
                        {
                            // Start of the section;
                            // Only sections contained in iniKeys will be targeted
                            for (int i = 0; i < iniKeys.Length; i++)
                            {
                                if (processed[i])
                                    continue;

                                IniKey key = iniKeys[i];
                                if (foundSection.Equals(key.Section.AsSpan(), StringComparison.OrdinalIgnoreCase))
                                { // Rename this section
                                    w.WriteLine($"[{key.Key}]");
                                    thisLineProcessed = true;
                                    processed[i] = true;
                                    break; // for shorter O(n)
                                }
                            }
                        }

                        if (!thisLineProcessed)
                            w.WriteLine(rawLine);
                    }
                }

                if (processed.Any(x => x))
                    FileHelper.FileReplaceEx(tempPath, filePath);

                return processed;
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }
        #endregion

        #region (Write) DeleteSection
        /// <summary>
        /// Delete a section from an .ini file.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="section">Name of the target section.</param>
        /// <returns>Returns true if the operation was successful.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool DeleteSection(string filePath, string section)
        {
            return InternalDeleteSection(filePath, new List<string> { section })[0];
        }

        /// <summary>
        /// Delete a section from an .ini file.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="iniKey">Name of the target section. Key and Value is ignored.</param>
        /// <returns>Returns true if the operation was successful.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool DeleteSection(string filePath, IniKey iniKey)
        {
            return InternalDeleteSection(filePath, new List<string> { iniKey.Section })[0];
        }

        /// <summary>
        /// Delete sections from an .ini file.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="sections">Enumerable names of the target section.</param>
        /// <returns>An array of return value for each IniKey. Returns true if the operation of an iniKey was successful.</returns>
        public static bool[] DeleteSections(string filePath, IEnumerable<string> sections)
        {
            return InternalDeleteSection(filePath, sections.ToList());
        }

        /// <summary>
        /// Delete sections from an .ini file.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="iniKeys">Enumerable names of the target section. Key and Value is ignored.</param>
        /// <returns>An array of return value for each IniKey. Returns true if the operation of an iniKey was successful.</returns>
        public static bool[] DeleteSections(string filePath, IEnumerable<IniKey> iniKeys)
        {
            return InternalDeleteSection(filePath, iniKeys.Select(x => x.Section).ToList());
        }

        /// <summary>
        /// Delete sections from an .ini file.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="sections">List of target section names.</param>
        /// <returns>An array of return value for each IniKey. Returns true if the operation of an iniKey was successful.</returns>
        private static bool[] InternalDeleteSection(string filePath, List<string> sections)
        {
            bool[] processed = new bool[sections.Count];
            for (int i = 0; i < processed.Length; i++)
                processed[i] = false;

            // File check
            if (!File.Exists(filePath))
                return processed;

            string tempPath = FileHelper.GetTempFile();
            try
            {
                Encoding encoding = SmarterDetectEncoding(filePath, sections);
                using (StreamReader r = new StreamReader(filePath, encoding, false))
                using (StreamWriter w = new StreamWriter(tempPath, false, encoding))
                {
                    if (r.Peek() == -1)
                    {
                        r.Close();
                        return processed;
                    }

                    string? rawLine;
                    bool ignoreCurrentSection = false;

                    // Main Logic
                    while ((rawLine = r.ReadLine()) != null)
                    { // Read text line by line
                        ReadOnlySpan<char> line = rawLine.AsSpan().Trim();

                        // Check if encountered section head Ex) [Process]
                        if (IsLineSection(line, out ReadOnlySpan<char> foundSection))
                        {
                            ignoreCurrentSection = false;

                            // Start of the section;
                            // Only sections contained in iniKeys will be targeted
                            for (int i = 0; i < sections.Count; i++)
                            {
                                if (processed[i])
                                    continue;

                                if (foundSection.Equals(sections[i].AsSpan(), StringComparison.OrdinalIgnoreCase))
                                { // Delete this section!
                                    ignoreCurrentSection = true;
                                    processed[i] = true;
                                    break; // for shorter O(n)
                                }
                            }
                        }

                        if (!ignoreCurrentSection)
                            w.WriteLine(rawLine);
                    }
                }

                if (processed.Any(x => x))
                    FileHelper.FileReplaceEx(tempPath, filePath);

                return processed;
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }
        #endregion

        #region (Read) ReadRawSection
        /// <summary>
        /// Read contents of a section from an .ini file.
        /// </summary>
        /// <param name="filePath">An ini file to read.</param>
        /// <param name="section">Name of a target section.</param>
        /// <returns>A list of the text lines of a section.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static List<string> ReadRawSection(string filePath, string section)
        {
            return InternalReadRawSection(filePath, new List<string> { section }, false).Select(x => x.Value).First();
        }

        /// <summary>
        /// Read contents of a section from an .ini file.
        /// </summary>
        /// <param name="filePath">An ini file to read.</param>
        /// <param name="section">Name of a target section.</param>
        /// <param name="includeEmptyLines">Whether to include empty lines from section contents.</param>
        /// <returns>A list of the text lines of a section.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static List<string> ReadRawSection(string filePath, string section, bool includeEmptyLines)
        {
            return InternalReadRawSection(filePath, new List<string> { section }, includeEmptyLines).Select(x => x.Value).First();
        }

        /// <summary>
        /// Read contents of a section from an .ini file.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="iniKey">Name of a target section. Key and Value is ignored.</param>
        /// <returns>A list of the text lines of a section.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static List<string> ReadRawSection(string filePath, IniKey iniKey)
        {
            return InternalReadRawSection(filePath, new List<string> { iniKey.Section }, false).Select(x => x.Value).First();
        }

        /// <summary>
        /// Read contents of a section from an .ini file.
        /// </summary>
        /// <param name="filePath">An ini file to read.</param>
        /// <param name="iniKey">Name of a target section. Key and Value is ignored.</param>
        /// <param name="includeEmptyLines">Whether to include empty lines from section contents.</param>
        /// <returns>A list of the text lines of a section.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static List<string> ReadRawSection(string filePath, IniKey iniKey, bool includeEmptyLines)
        {
            return InternalReadRawSection(filePath, new List<string> { iniKey.Section }, includeEmptyLines).Select(x => x.Value).First();
        }

        /// <summary>
        /// Read contents of a section from an .ini file.
        /// </summary>
        /// <param name="filePath">An ini file to read.</param>
        /// <param name="sections">Enumerable names of the target section.</param>
        /// <param name="includeEmptyLines">Whether to include empty lines from section contents.</param>
        /// <returns>A list of the text lines of a section.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Dictionary<string, List<string>> ReadRawSections(string filePath, IEnumerable<string> sections)
        {
            return InternalReadRawSection(filePath, sections.ToList(), false);
        }

        /// <summary>
        /// Read contents of a section from an .ini file.
        /// </summary>
        /// <param name="filePath">An ini file to read.</param>
        /// <param name="sections">Enumerable names of target sections.</param>
        /// <param name="includeEmptyLines">Whether to include empty lines from section contents.</param>
        /// <returns>A list of the text lines of a section.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Dictionary<string, List<string>> ReadRawSections(string filePath, IEnumerable<string> sections, bool includeEmptyLines)
        {
            return InternalReadRawSection(filePath, sections.ToList(), includeEmptyLines);
        }

        /// <summary>
        /// Read contents of a section from an .ini file.
        /// </summary>
        /// <param name="filePath">An ini file to read.</param>
        /// <param name="iniKeys">Enumerable names of target sections. Key and Value is ignored.</param>
        /// <returns>A list of the text lines of a section.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Dictionary<string, List<string>> ReadRawSections(string filePath, IEnumerable<IniKey> iniKeys)
        {
            return InternalReadRawSection(filePath, iniKeys.Select(x => x.Section).ToList(), false);
        }

        /// <summary>
        /// Read contents of a section from an .ini file.
        /// </summary>
        /// <param name="filePath">An ini file to read.</param>
        /// <param name="iniKeys">Enumerable names of target sections. Key and Value is ignored.</param>
        /// <param name="includeEmptyLines">Whether to include empty lines from section contents.</param>
        /// <returns>A list of the text lines of a section.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Dictionary<string, List<string>> ReadRawSections(string filePath, IEnumerable<IniKey> iniKeys, bool includeEmptyLines)
        {
            return InternalReadRawSection(filePath, iniKeys.Select(x => x.Section).ToList(), includeEmptyLines);
        }

        /// <summary>
        /// Read contents of a section from an .ini file.
        /// </summary>
        /// <param name="filePath">An ini file to read.</param>
        /// <param name="sections">List of target section names.</param>
        /// <param name="includeEmptyLines">Whether to include empty lines from section contents.</param>
        /// <returns>A list of the text lines of a section.</returns>
        private static Dictionary<string, List<string>> InternalReadRawSection(string filePath, List<string> sections, bool includeEmptyLines)
        {
            Dictionary<string, List<string>> secDict = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            Encoding encoding = SmarterDetectEncoding(filePath, sections);
            using (StreamReader r = new StreamReader(filePath, encoding, false))
            {
                string? rawLine = null;
                bool inTargetSection = false;
                string? currentSection = null;
                List<string>? currentContents = null;

                while ((rawLine = r.ReadLine()) != null)
                { // Read text line by line
                    ReadOnlySpan<char> line = rawLine.AsSpan().Trim(); // Remove whitespace
                    if (IsLineComment(line)) // Ignore comment
                        continue;

                    if (IsLineSection(line, out ReadOnlySpan<char> foundSection))
                    {
                        inTargetSection = false;

                        string foundSectionStr = foundSection.ToString();
                        int sIdx = sections.FindIndex(x => x.Equals(foundSectionStr, StringComparison.OrdinalIgnoreCase));
                        if (sIdx != -1)
                        {
                            inTargetSection = true;
                            currentSection = foundSectionStr;

                            secDict[currentSection] = new List<string>(16);
                            currentContents = secDict[currentSection];

                            sections.RemoveAt(sIdx);
                            continue;
                        }
                    }

                    if (inTargetSection && currentContents != null)
                    {
                        if (includeEmptyLines)
                            currentContents.Add(line.ToString());
                        else if (0 < line.Length)
                            currentContents.Add(line.ToString());
                    }
                }
            }

            return secDict;
        }
        #endregion

        #region (Write) Merge
        public static bool Merge(string srcFile, string destFile)
        {
            IniFile srcIniFile = new IniFile(srcFile);

            bool result = true;
            // kvSec => Key: Section Value:<Key-Value>
            foreach (var kvSec in srcIniFile.Sections)
            {
                string section = kvSec.Key;
                Dictionary<string, string> kvPairs = kvSec.Value;

                List<IniKey> keys = new List<IniKey>();

                // kvKey => Key:Key Value:Value
                foreach (var kvKey in kvPairs)
                    keys.Add(new IniKey(section, kvKey.Key, kvKey.Value));

                result &= InternalWriteKeys(destFile, keys);
            }

            return result;
        }

        public static bool MergeCompact(string srcFile, string destFile)
        {
            IniFile srcIniFile = new IniFile(srcFile);

            bool result = true;
            // kvSec => Key: Section Value:<Key-Value>
            foreach (var kvSec in srcIniFile.Sections)
            {
                string section = kvSec.Key;
                Dictionary<string, string> kvPairs = kvSec.Value;

                List<IniKey> keys = new List<IniKey>();

                // kvKey => Key:Key Value:Value
                foreach (var kvKey in kvPairs)
                    keys.Add(new IniKey(section, kvKey.Key, kvKey.Value));

                result &= InternalWriteCompactKeys(destFile, keys);
            }

            return result;
        }

        public static bool Merge(string srcFile1, string srcFile2, string destFile)
        {
            IniFile[] srcIniFiles =
            {
                new IniFile(srcFile1),
                new IniFile(srcFile2),
            };

            bool result = true;
            foreach (IniFile srcIniFile in srcIniFiles)
            {
                // kvSec => Key: Section Value:<Key-Value>
                foreach (var kvSec in srcIniFile.Sections)
                {
                    List<IniKey> keys = new List<IniKey>();

                    // kvKey => Key:Key Value:Value
                    foreach (var kvKey in kvSec.Value)
                        keys.Add(new IniKey(kvSec.Key, kvKey.Key, kvKey.Value));

                    result &= InternalWriteKeys(destFile, keys);
                }
            }

            return result;
        }
        #endregion

        #region (Write) Compact
        /// <summary>
        /// Compact an ini file. 
        /// Every text line will have its start and end trimmed, 
        /// and each pairs of key and value will be reformatted to {K}={V}.
        /// </summary>
        /// <param name="filePath">An ini file to compact.</param>
        public static void Compact(string filePath)
        {
            string tempPath = FileHelper.GetTempFile();
            try
            {
                // Append IniKey into existing file
                Encoding encoding = EncodingHelper.DetectEncoding(filePath);
                using (StreamReader r = new StreamReader(filePath, encoding, false))
                using (StreamWriter w = new StreamWriter(tempPath, false, encoding))
                {
                    while (true)
                    {
                        string? rawLine = r.ReadLine();
                        if (rawLine == null) // End of file
                            break;

                        ReadOnlySpan<char> line = rawLine.AsSpan().Trim();
                        if (IsLineSection(line))
                        { // Section head encountered (e.g. [DestinationDirs]), just trim it
                            w.WriteLine(line.ToString());
                        }
                        else
                        {
                            string compacted = CompactKeyValuePairLine(line);
                            w.WriteLine(compacted);
                        }
                    }
                }

                FileHelper.FileReplaceEx(tempPath, filePath);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }
        #endregion

        #region (Read) ReadSectionNames, ContainsSection
        /// <summary>
        /// Get name of sections from INI file
        /// </summary>
        /// <param name="filePath">An .ini file to read.</param>
        /// <returns>List of section names.</returns>
        public static List<string> ReadSectionNames(string filePath)
        {
            List<string> sections = new List<string>();

            Encoding encoding = EncodingHelper.DetectEncoding(filePath);
            using (StreamReader r = new StreamReader(filePath, encoding, true))
            {
                string? rawLine;
                while ((rawLine = r.ReadLine()) != null)
                { // Read text line by line
                    ReadOnlySpan<char> line = rawLine.AsSpan().Trim();
                    if (IsLineSection(line, out ReadOnlySpan<char> foundSection)) // Count sections
                        sections.Add(foundSection.ToString());
                }
            }

            return sections;
        }

        /// <summary>
        /// Check if INI file has specified section.
        /// </summary>
        /// <param name="filePath">An .ini file to read.</param>
        /// <param name="section">Name of a section to check its existance.</param>
        /// <returns>Returns true if a section name exists in an .ini file.</returns>
        public static bool ContainsSection(string filePath, string section)
        {
            bool result = false;

            Encoding encoding = SmarterDetectEncoding(filePath, section);
            using (StreamReader r = new StreamReader(filePath, encoding, false))
            {
                string? rawLine;
                while ((rawLine = r.ReadLine()) != null)
                { // Read text line by line
                    ReadOnlySpan<char> line = rawLine.AsSpan().Trim();
                    if (IsLineSection(line, out ReadOnlySpan<char> foundSection))
                    {
                        if (foundSection.Equals(section.AsSpan(), StringComparison.OrdinalIgnoreCase))
                        {
                            result = true;
                            break;
                        }
                    }
                }
            }

            return result;
        }
        #endregion

        #region (Read) ParseIniSection
        /*
         * TODO: Methods are a bit messy, need some refactoring
         */

        /// <summary>
        /// Parse section to dictionary.
        /// </summary>
        /// <param name="filePath">An .ini file to read.</param>
        /// <param name="section">Name of a target section.</param>
        /// <returns>A dictionary of keys and values. Returns null if section was not found.</returns>
        public static Dictionary<string, string>? ParseIniSectionToDict(string filePath, string section)
        {
            List<string>? lines = ParseIniSection(filePath, section);
            return lines == null ? null : ParseIniLinesIniStyle(lines);
        }

        /// <summary>
        /// Faster, specialized version of InternalReadRawSection.
        /// </summary>
        /// <param name="filePath">An .ini file to read.</param>
        /// <param name="section">Name of a target section.</param>
        /// <returns>A list of section content text lines. Returns null if section was not found.</returns>
        public static List<string>? ParseIniSection(string filePath, string section)
        {
            List<string> lines = new List<string>();

            Encoding encoding = SmarterDetectEncoding(filePath, section);
            using (StreamReader r = new StreamReader(filePath, encoding, false))
            {
                string? rawLine;
                bool appendState = false;
                while ((rawLine = r.ReadLine()) != null)
                { // Read text line by line
                    ReadOnlySpan<char> line = rawLine.AsSpan().Trim();

                    // Ignore comment
                    if (line.StartsWith("#".AsSpan(), StringComparison.Ordinal) ||
                        line.StartsWith(";".AsSpan(), StringComparison.Ordinal) ||
                        line.StartsWith("//".AsSpan(), StringComparison.Ordinal))
                        continue;

                    if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                        line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
                    { // Start of section
                        if (appendState)
                            break;

                        // Remove [ and ]
                        ReadOnlySpan<char> foundSection = line[1..^1];
                        if (foundSection.Equals(section.AsSpan(), StringComparison.OrdinalIgnoreCase))
                            appendState = true;
                    }
                    else
                    {
                        int idx;
                        if ((idx = line.IndexOf('=')) != -1 && idx != 0)
                        { // Valid ini key/value pair, and not empty
                            if (appendState)
                                lines.Add(line.ToString());
                        }
                    }
                }

                if (!appendState) // Section not found
                    return null;
            }
            return lines;
        }

        /// <summary>
        /// Faster version of InternalReadRawSection.
        /// </summary>
        /// <param name="filePath">An .ini file to read.</param>
        /// <param name="section">Name of a target section.</param>
        /// <returns>A list of section content text lines. Returns null if section was not found.</returns>
        public static List<string>? ParseRawSection(string filePath, string section)
        {
            List<string> lines = new List<string>();

            Encoding encoding = SmarterDetectEncoding(filePath, section);
            using (StreamReader r = new StreamReader(filePath, encoding, false))
            {
                string? rawLine;
                bool appendState = false;
                while ((rawLine = r.ReadLine()) != null)
                { // Read text line by line
                    ReadOnlySpan<char> line = rawLine.AsSpan().Trim();

                    // Ignore comment
                    if (IsLineComment(line))
                        continue;

                    if (IsLineSection(line, out ReadOnlySpan<char> foundSection))
                    { // Start of section
                        if (appendState)
                            break;

                        // Remove [ and ]
                        if (foundSection.Equals(section, StringComparison.OrdinalIgnoreCase))
                            appendState = true;
                    }
                    else if (appendState)
                    {
                        if (line.Length != 0)
                            lines.Add(line.ToString());
                    }
                }

                if (!appendState) // Section not found
                    return null;
            }
            return lines;
        }

        /// <summary>
        /// Parse section to array of dictionary.
        /// </summary>
        /// <param name="filePath">An .ini file to read.</param>
        /// <param name="sections">Enumberable names of target sections.</param>
        /// <returns>An array of Dictionary of keys and values.</returns>
        public static Dictionary<string, string>[] ParseSectionsToDicts(string filePath, IEnumerable<string> sections)
        {
            List<string>[] lines = ParseIniSections(filePath, sections);
            Dictionary<string, string>[] dict = new Dictionary<string, string>[lines.Length];
            for (int i = 0; i < lines.Length; i++)
                dict[i] = ParseIniLinesIniStyle(lines[i]);
            return dict;
        }

        /// <summary>
        /// Parse sections to array of a list of strings.
        /// </summary>
        /// <param name="filePath">An .ini file to read.</param>
        /// <param name="sections">Enumberable names of target sections.</param>
        /// <returns>An array of Dictionary of keys and values.</returns>
        public static List<string>[] ParseIniSections(string filePath, IEnumerable<string> sections)
        {
            string[] sectionNames = sections.Distinct().ToArray(); // Remove duplicate

            List<string>[] lines = new List<string>[sectionNames.Length];
            for (int i = 0; i < sectionNames.Length; i++)
                lines[i] = new List<string>();

            Encoding encoding = SmarterDetectEncoding(filePath, sectionNames);
            using (StreamReader r = new StreamReader(filePath, encoding, true))
            {
                string? rawLine;
                int currentSection = -1; // -1 == empty, 0, 1, ... == index value of sections array
                List<int> processedSectionIdxs = new List<int>();

                while ((rawLine = r.ReadLine()) != null)
                { // Read text line by line
                    if (sectionNames.Length < processedSectionIdxs.Count)
                        break;

                    ReadOnlySpan<char> line = rawLine.AsSpan().Trim();
                    if (IsLineSection(line, out ReadOnlySpan<char> foundSection))
                    { // Start of section
                        bool isSectionFound = false;
                        for (int i = 0; i < sectionNames.Length; i++)
                        {
                            if (processedSectionIdxs.Contains(i))
                                continue;

                            if (foundSection.Equals(sectionNames[i], StringComparison.Ordinal))
                            {
                                isSectionFound = true;
                                processedSectionIdxs.Add(i);
                                currentSection = i;
                                break;
                            }
                        }
                        if (!isSectionFound)
                            currentSection = -1;
                    }
                    else
                    {
                        int idx;
                        if ((idx = line.IndexOf('=')) != -1 && idx != 0)
                        { // valid ini key
                            if (currentSection != -1) // current section is target, and key is empty
                                lines[currentSection].Add(line.ToString());
                        }
                    }
                }
            }

            return lines;
        }

        /// <summary>
        /// Parse entire content of an .ini file.
        /// </summary>
        /// <param name="srcFile">An .ini file to read.</param>
        public static Dictionary<string, Dictionary<string, string>> ParseFileToDict(string srcFile)
        {
            Dictionary<string, Dictionary<string, string>> dict = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            if (!File.Exists(srcFile))
                return dict; // Return Empty dict if srcFile does not exist

            Encoding encoding = EncodingHelper.DetectEncoding(srcFile);
            using (StreamReader r = new StreamReader(srcFile, encoding))
            {
                // Is Original File Empty?
                if (r.Peek() == -1) // Return Empty Dict
                    return dict;

                string? rawLine;
                string? section = null;

                while ((rawLine = r.ReadLine()) != null)
                { // Read text line by line
                    ReadOnlySpan<char> line = rawLine.AsSpan().Trim(); // Remove whitespace
                    if (IsLineComment(line)) // Ignore comment
                        continue;

                    // Check if encountered section head Ex) [Process]
                    if (IsLineSection(line, out ReadOnlySpan<char> foundSection))
                    {
                        section = foundSection.ToString();
                        dict[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        continue;
                    }

                    // Read Keys
                    if (section != null)
                    {
                        int idx = line.IndexOf('=');
                        if (idx != -1 && idx != 0) // there is key, and key name is not empty
                        {
                            string key = line[..idx].Trim().ToString();
                            string value = line[(idx + 1)..].Trim().ToString();

                            dict[section][key] = value;
                        }
                    }
                }
            }

            return dict;
        }
        #endregion

        #region (TextReader, TextWriter) Fast Forward
        /// <summary>
        /// Move position of TextReader to read content of specific .ini section.
        /// </summary>
        /// <remarks>
        /// Designed for use in EncodedFile class.
        /// </remarks>
        /// <param name="tr">TextReader to fast-forward.</param>
        /// <param name="section">Section to find.</param>
        public static void FastForwardTextReader(TextReader tr, string section)
        {
            if (section == null)
                throw new ArgumentNullException(nameof(section));

            // Read base64 block directly from file
            string? rawLine;
            while ((rawLine = tr.ReadLine()) != null)
            { // Read text line by line
                ReadOnlySpan<char> line = rawLine.AsSpan().Trim();

                // Ignore comment
                if (IsLineComment(line))
                    continue;

                if (IsLineSection(line, out ReadOnlySpan<char> foundSection))
                { // Start of section
                    // Found target section, so return
                    if (foundSection.Equals(section.AsSpan(), StringComparison.OrdinalIgnoreCase))
                        return;
                }
            }
        }

        /// <summary>
        /// Copy from TextReader to TextWriter until specific .ini section is found.
        /// </summary>
        /// <remarks>
        /// Designed for use in EncodedFile class.
        /// </remarks>
        /// <param name="tr">Source TextReader</param>
        /// <param name="tw">Destination TextWriter</param>
        /// <param name="section">Section to find. Specify null for full copy.</param>
        /// <param name="copyFromNewSection">If set to true, start copy after finding new section when tr was originally in the middle of a section.</param>
        public static void FastForwardTextWriter(TextReader tr, TextWriter tw, string? section, bool copyFromNewSection)
        {
            bool enableCopy = !copyFromNewSection;

            string? rawLine;
            while ((rawLine = tr.ReadLine()) != null)
            { // Read text line by line
                if (enableCopy)
                    tw.WriteLine(rawLine);

                if (section == null && !enableCopy)
                {
                    ReadOnlySpan<char> line = rawLine.AsSpan().Trim();
                    if (IsLineSection(line))
                    { // Start of section
                        tw.WriteLine(rawLine);
                        enableCopy = true;
                    }
                }
                else if (section != null)
                {
                    ReadOnlySpan<char> line = rawLine.AsSpan().Trim();
                    if (IsLineSection(line, out ReadOnlySpan<char> foundSection))
                    { // Start of section
                        if (!enableCopy)
                            tw.WriteLine(rawLine);
                        enableCopy = true;

                        // Found target section, so return
                        if (foundSection.Equals(section.AsSpan(), StringComparison.OrdinalIgnoreCase))
                            return;
                    }
                }
            }

            // Target section not found, insert section header at the end of TextWriter
            if (section != null)
            {
                tw.WriteLine();
                tw.WriteLine($"[{section}]");
            }
        }
        #endregion

        #region (Utility) IsLine Check
        /// <summary>
        /// Check whether a text line is a comment or not.
        /// </summary>
        /// <param name="lineSpan">A text line to check.</param>
        /// <returns>Returns true if a text line is a comment.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsLineComment(ReadOnlySpan<char> lineSpan)
        {
            return lineSpan.StartsWith("#", StringComparison.Ordinal) ||
                   lineSpan.StartsWith(";", StringComparison.Ordinal) ||
                   lineSpan.StartsWith("//", StringComparison.Ordinal);
        }

        /// <summary>
        /// Check if a line is a valid section.
        /// </summary>
        /// <param name="line">A line to check.</param>
        /// <returns>Returns true if a line is a valid section.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsLineSection(ReadOnlySpan<char> line)
        {
            return IsLineSection(line, out _);
        }

        /// <summary>
        /// Check if a line is a valid section.
        /// </summary>
        /// <param name="lineSpan">A line to check.</param>
        /// <returns>Returns true if a line is a valid section.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsLineSection(ReadOnlySpan<char> lineSpan, out ReadOnlySpan<char> sectionName)
        {
            if (lineSpan.StartsWith("[", StringComparison.Ordinal) &&
                lineSpan.EndsWith("]", StringComparison.Ordinal))
            {
                sectionName = lineSpan[1..^1];
                return true;
            }

            sectionName = string.Empty;
            return false;
        }
        #endregion

        #region (Utility) Filter Lines
        /// <summary>
        /// Filter out comment lines from a list of lines.
        /// </summary>
        /// <param name="lines">List of text lines to filter.</param>
        /// <returns>Returns a filtered out list of text lines.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static List<string> FilterCommentLines(IReadOnlyList<string> lines)
        {
            List<string> filtered = new List<string>(lines.Count);
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                if (0 < line.Length && !IsLineComment(line))
                    filtered.Add(line);
            }
            return filtered;
        }

        /// <summary>
        /// Filter out non-ini-style lines from a list of lines.
        /// </summary>
        /// <param name="lines">List of text lines to filter.</param>
        /// <returns>Returns a filtered out list of text lines.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static List<string> FilterNonIniLines(IReadOnlyList<string> lines)
        {
            List<string> filtered = new List<string>(lines.Count);
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                if (line.Length == 0)
                    continue;

                int idx = line.IndexOf('=');
                if (idx != 0 && idx != -1)
                    filtered.Add(line);
            }
            return filtered;
        }
        #endregion

        #region (Utility) Parse IEnumerable<string>
        /// <summary>
        /// Parse INI style strings into dictionary
        /// </summary>
        public static Dictionary<string, string> ParseIniLinesIniStyle(IEnumerable<string> lines)
        {
            // This regex exclude %A%=BCD form.
            // Used [^=] to prevent '=' in key.
            return InternalParseIniLinesRegex(@"^(?<!\/\/|#|;)([^%=\r\n]+)=(.*)$", lines);
        }

        /// <summary>
        /// Parse PEBakery-Variable style strings into dictionary, format of %VarKey%=VarValue. 
        /// </summary>
        public static Dictionary<string, string> ParseIniLinesVarStyle(IEnumerable<string> lines)
        {
            // Used [^=] to prevent '=' in key.
            return InternalParseIniLinesRegex(@"^%([^=]+)%=(.*)$", lines);
        }

        /// <summary>
        /// Parse strings with regex.
        /// </summary>
        private static Dictionary<string, string> InternalParseIniLinesRegex(string regex, IEnumerable<string> lines)
        {
            Regex regexInst = new Regex(regex, RegexOptions.Compiled);

            Dictionary<string, string> dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in lines)
            {
                MatchCollection matches = regexInst.Matches(line);

                // Make instances of sections
                for (int i = 0; i < matches.Count; i++)
                {
                    string key = matches[i].Groups[1].Value.Trim();
                    string value = matches[i].Groups[2].Value.Trim();
                    dict[key] = value;
                }
            }
            return dict;
        }
        #endregion

        #region (Utility) GetKeyValueFromLine
        /// <summary>
        /// Used to handle [EncodedFile-InterfaceEncoded-*] section
        /// Return null if failed
        /// </summary>
        public static (string? Key, string? Value) GetKeyValueFromLine(string rawLine)
        {
            int idx = rawLine.IndexOf('=');
            if (idx == -1) // Unable to find key and value
                return (null, null);

            string key = rawLine.AsSpan()[..idx].Trim().ToString();
            string value = rawLine.AsSpan()[(idx + 1)..].Trim().ToString();
            return (key, value);
        }

        /// <summary>
        /// Used to handle [EncodedFile-InterfaceEncoded-*] section.
        /// </summary>
        /// <returns>
        /// Null is returned if failed.
        /// </returns>
        public static (List<string>? Keys, List<string>? Values) GetKeyValueFromLines(IReadOnlyList<string> rawLines)
        {
            List<string> keys = new List<string>();
            List<string> values = new List<string>();
            foreach (string rawLine in rawLines)
            {
                (string? key, string? value) = GetKeyValueFromLine(rawLine);
                if (key == null || value == null)
                    return (null, null);
                keys.Add(key);
                values.Add(value);
            }
            return (keys, values);
        }
        #endregion

        #region (Utility) CompactKeyValuePairString
        /// <summary>
        /// Compacts ini-style key-value pair lines.
        /// </summary>
        /// <remarks>
        /// The method also handles non-ini style entry by just trimming its start and end.
        /// </remarks>
        /// <param name="line">A text line to compact.</param>
        /// <returns>Compacted string</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string CompactKeyValuePairLine(string line)
        {
            return CompactKeyValuePairLine(line.AsSpan());
        }

        /// <summary>
        /// Compacts ini-style key-value pair lines.
        /// </summary>
        /// <remarks>
        /// The method also handles non-ini style entry by just trimming its start and end.
        /// </remarks>
        /// <param name="lineSpan">A text line to compact.</param>
        /// <returns>Compacted string</returns>
        private static string CompactKeyValuePairLine(ReadOnlySpan<char> lineSpan)
        {
            int idx = lineSpan.IndexOf('=');
            if (idx != -1 && idx != 0) // there is key, and key name is not empty
            { // Ini style entry, reformat into [Key=Value] template
                ReadOnlySpan<char> key = lineSpan[..idx].Trim();
                ReadOnlySpan<char> value = lineSpan[(idx + 1)..].Trim();
                return string.Concat(key, "=", value);
            }
            else
            { // Non-ini style entry, trim only end of it (Commands are often indented)
                return lineSpan.ToString();
            }
        }
        #endregion

        #region (Internal) SmarterAnsiDetect
        private static Encoding SmarterDetectEncoding(string filePath, IReadOnlyList<IniKey> iniKeys)
        {
            // Test if content to write is ANSI-compatible.
            bool IsContentAnsiCompat()
            {
                bool isCompat = true;
                for (int i = 0; i < iniKeys.Count && isCompat; i++)
                {
                    IniKey key = iniKeys[i];
                    if (key.Section != null)
                        isCompat &= EncodingHelper.IsActiveCodePageCompatible(key.Section);
                    if (key.Key != null)
                        isCompat &= EncodingHelper.IsActiveCodePageCompatible(key.Key);
                    if (key.Value != null)
                        isCompat &= EncodingHelper.IsActiveCodePageCompatible(key.Value);
                }
                return isCompat;
            }

            return EncodingHelper.SmartDetectEncoding(filePath, IsContentAnsiCompat);
        }

        private static Encoding SmarterDetectEncoding(string filePath, IReadOnlyList<string> contents)
        {
            return EncodingHelper.SmartDetectEncoding(filePath, contents);
        }

        private static Encoding SmarterDetectEncoding(string filePath, string content)
        {
            return EncodingHelper.SmartDetectEncoding(filePath, content);
        }
        #endregion
    }
    #endregion

    #region IniFile
    public sealed class IniFile
    {
        /// <summary>
        /// Path of the .ini file.
        /// </summary>
        public string FilePath { get; set; }
        /// <summary>
        /// Key is section name, and value is a Dictionary of keys and value.
        /// </summary>
        public Dictionary<string, Dictionary<string, string>> Sections { get; set; }

        public IniFile(string filePath)
        {
            FilePath = filePath;
            Sections = IniReadWriter.ParseFileToDict(filePath);
        }
    }
    #endregion
}
