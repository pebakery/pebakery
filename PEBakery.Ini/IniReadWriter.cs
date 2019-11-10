/*
    Copyright (C) 2016-2019 Hajin Jang
 
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
// ReSharper disable UnusedMember.Global

namespace PEBakery.Ini
{
    #region IniKey
    public class IniKey : IEquatable<IniKey>
    {
        #region Properties
        public string Section { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }
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
        public bool Equals(IniKey other)
        {
            bool StringEqual(string x, string y)
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

        public override bool Equals(object obj)
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
        public static string ReadKey(string filePath, string section, string key)
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
        public static string ReadKey(string filePath, IniKey iniKey)
        {
            IniKey[] iniKeys = InternalReadKeys(filePath, new IniKey[] { iniKey });
            return iniKeys[0].Value;
        }

        /// <summary>
        /// Read values of target keys from an ini file.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="iniKey">Enumerable tuples of Section and Key.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IniKey[] ReadKeys(string filePath, IEnumerable<IniKey> iniKeys)
        {
            return InternalReadKeys(filePath, iniKeys.ToArray());
        }

        private static IniKey[] InternalReadKeys(string filePath, IniKey[] iniKeys)
        {
            List<int> processedKeyIdxs = new List<int>(iniKeys.Length);

            Encoding encoding = EncodingHelper.DetectBom(filePath);
            using (StreamReader reader = new StreamReader(filePath, encoding, true))
            {
                string rawLine;
                bool inTargetSection = false;
                string currentSection = null;

                while ((rawLine = reader.ReadLine()) != null)
                { // Read text line by line
                    if (processedKeyIdxs.Count == iniKeys.Length) // Work Done
                        break;

                    ReadOnlySpan<char> line = rawLine.AsSpan().Trim(); // Remove whitespace
                    if (line.StartsWith("#".AsSpan(), StringComparison.Ordinal) ||
                        line.StartsWith(";".AsSpan(), StringComparison.Ordinal) ||
                        line.StartsWith("//".AsSpan(), StringComparison.Ordinal)) // Ignore comment
                        continue;

                    if (inTargetSection)
                    {
                        int idx = line.IndexOf('=');
                        if (idx != -1 && idx != 0) // there is key, and key name is not empty
                        {
                            ReadOnlySpan<char> keyName = line.Slice(0, idx).Trim();
                            for (int i = 0; i < iniKeys.Length; i++)
                            {
                                if (processedKeyIdxs.Contains(i))
                                    continue;

                                // Only if <section, key> is same, copy value;
                                IniKey iniKey = iniKeys[i];
                                if (currentSection.Equals(iniKey.Section, StringComparison.OrdinalIgnoreCase) &&
                                    keyName.Equals(iniKey.Key.AsSpan(), StringComparison.OrdinalIgnoreCase))
                                {
                                    iniKey.Value = line.Slice(idx + 1).Trim().ToString();
                                    iniKeys[i] = iniKey;
                                    processedKeyIdxs.Add(i);
                                }
                            }
                        }
                        else
                        {
                            // search if current section reached its end
                            if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                                line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
                            {
                                // Only sections contained in iniKeys will be targeted
                                inTargetSection = false;
                                currentSection = null;
                                ReadOnlySpan<char> foundSection = line.Slice(1, line.Length - 2);
                                for (int i = 0; i < iniKeys.Length; i++)
                                {
                                    if (processedKeyIdxs.Contains(i))
                                        continue;

                                    if (foundSection.Equals(iniKeys[i].Section.AsSpan(), StringComparison.OrdinalIgnoreCase))
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
                        if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                            line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
                        {
                            // Only sections contained in iniKeys will be targeted
                            ReadOnlySpan<char> foundSection = line.Slice(1, line.Length - 2);
                            for (int i = 0; i < iniKeys.Length; i++)
                            {
                                if (processedKeyIdxs.Contains(i))
                                    continue;

                                if (foundSection.Equals(iniKeys[i].Section.AsSpan(), StringComparison.OrdinalIgnoreCase))
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
                                x.Key.Equals(secKey.Key, StringComparison.OrdinalIgnoreCase));
                        }

                        firstSection = false;
                    }
                }
            }
            #endregion

            // If file does not exist just create new file and insert keys.
            if (!File.Exists(filePath))
            {
                using (StreamWriter w = new StreamWriter(filePath, false, Encoding.UTF8))
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
                Encoding encoding = EncodingHelper.DetectBom(filePath);
                using (StreamReader r = new StreamReader(filePath, encoding, false))
                using (StreamWriter w = new StreamWriter(tempPath, false, encoding))
                {
                    bool inTargetSection = false;
                    string currentSection = null;
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
                                            x.Key.Equals(secKey.Key, StringComparison.OrdinalIgnoreCase));
                                        secKeys.RemoveAll(x =>
                                            x.Key.Equals(secKey.Key, StringComparison.OrdinalIgnoreCase));

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
                                x.Key.Equals(secKey.Key, StringComparison.OrdinalIgnoreCase));
                            secKeys.RemoveAll(x =>
                                x.Key.Equals(secKey.Key, StringComparison.OrdinalIgnoreCase));
                        }
                        w.WriteLine();

                        Debug.Assert(secKeys.Count == 0);
                    }
                    #endregion

                    bool firstLine = true;
                    ReadOnlySpan<char> lastLine = null;
                    while (true)
                    {
                        string rawLine = r.ReadLine();
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
                        if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                            line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
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
                                string foundSection = line.Slice(1, line.Length - 2).ToString();
                                if (0 < inputKeys.Count(x => foundSection.Equals(x.Section, StringComparison.OrdinalIgnoreCase)))
                                {
                                    inTargetSection = true;
                                    currentSection = foundSection;
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
        public static bool WriteCompactKey(string filePath, IniKey iniKey)
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

        private static bool InternalWriteCompactKeys(string filePath, List<IniKey> inputKeys)
        {
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
                                x.Key.Equals(secKey.Key, StringComparison.OrdinalIgnoreCase));
                        }

                        firstSection = false;
                    }
                }
            }
            #endregion

            // If file does not exist just create new file and insert keys.
            if (!File.Exists(filePath))
            {
                using (StreamWriter w = new StreamWriter(filePath, false, Encoding.UTF8))
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
                Encoding encoding = EncodingHelper.DetectBom(filePath);
                using (StreamReader r = new StreamReader(filePath, encoding, false))
                using (StreamWriter w = new StreamWriter(tempPath, false, encoding))
                {
                    bool inTargetSection = false;
                    string currentSection = null;
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
                                ReadOnlySpan<char> targetKey = line.AsSpan().Slice(0, eIdx).Trim();

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
                                            x.Key.Equals(secKey.Key, StringComparison.OrdinalIgnoreCase));
                                        secKeys.RemoveAll(x =>
                                            x.Key.Equals(secKey.Key, StringComparison.OrdinalIgnoreCase));

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
                                x.Key.Equals(secKey.Key, StringComparison.OrdinalIgnoreCase));
                            secKeys.RemoveAll(x =>
                                x.Key.Equals(secKey.Key, StringComparison.OrdinalIgnoreCase));
                        }
                        w.WriteLine();

                        Debug.Assert(secKeys.Count == 0);
                    }
                    #endregion

                    bool firstLine = true;
                    ReadOnlySpan<char> lastLine = null;
                    while (true)
                    {
                        string rawLine = r.ReadLine();
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
                        if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                            line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
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
                                string foundSection = line.Slice(1, line.Length - 2).ToString();
                                if (0 < inputKeys.Count(x => foundSection.Equals(x.Section, StringComparison.OrdinalIgnoreCase)))
                                {
                                    inTargetSection = true;
                                    currentSection = foundSection;
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
            return InternalWriteRawLine(filePath, iniKeys, true);
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
            return InternalWriteRawLine(filePath, iniKeys, append);
        }

        private static bool InternalWriteRawLine(string filePath, IEnumerable<IniKey> iniKeys, bool append)
        {
            List<IniKey> keys = iniKeys.ToList();

            // If file do not exists or blank, just create new file and insert keys.
            if (!File.Exists(filePath))
            {
                using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    string beforeSection = string.Empty;
                    for (int i = 0; i < keys.Count; i++)
                    {
                        if (beforeSection.Equals(keys[i].Section, StringComparison.OrdinalIgnoreCase) == false)
                        {
                            if (0 < i)
                                writer.WriteLine();
                            writer.WriteLine($"[{keys[i].Section}]");
                        }

                        // File does not exists, so we don't have to consider "append"
                        writer.WriteLine(keys[i].Key);

                        beforeSection = keys[i].Section;
                    }
                    writer.Close();
                }
                return true;
            }

            bool result = false;
            List<int> processedKeys = new List<int>(keys.Count);
            string tempPath = FileHelper.GetTempFile();
            try
            {
                Encoding encoding = EncodingHelper.DetectBom(filePath);
                using (StreamReader reader = new StreamReader(filePath, encoding, true))
                using (StreamWriter writer = new StreamWriter(tempPath, false, encoding))
                {
                    string rawLine;
                    bool inTargetSection = false;
                    ReadOnlySpan<char> currentSection = null;
                    List<string> processedSections = new List<string>(keys.Count);

                    // Is Original File Empty?
                    if (reader.Peek() == -1)
                    {
                        reader.Close();

                        // Write all and exit
                        string beforeSection = string.Empty;
                        for (int i = 0; i < keys.Count; i++)
                        {
                            if (!beforeSection.Equals(keys[i].Section, StringComparison.OrdinalIgnoreCase))
                            {
                                if (0 < i)
                                    writer.WriteLine();
                                writer.WriteLine($"[{keys[i].Section}]");
                            }

                            // File is blank, so we don't have to consider "append"
                            writer.WriteLine(keys[i].Key);

                            beforeSection = keys[i].Section;
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
                        if (keys.Count == 0 || IsLineComment(line))
                        {
                            thisLineWritten = true;
                            writer.WriteLine(rawLine);
                        }
                        else
                        {
                            // Check if encountered section head Ex) [Process]
                            if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                                line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
                            {
                                ReadOnlySpan<char> foundSection = line.Slice(1, line.Length - 2);

                                // Append Mode : Add to last line of section
                                if (append && inTargetSection)
                                { // End of targetSection and start of foundSection
                                    for (int i = 0; i < keys.Count; i++)
                                    {
                                        if (processedKeys.Contains(i))
                                            continue;

                                        // Add to last line of foundSection
                                        if (currentSection.Equals(keys[i].Section.AsSpan(), StringComparison.OrdinalIgnoreCase))
                                        {
                                            processedKeys.Add(i);
                                            writer.WriteLine(keys[i].Key);
                                        }
                                    }
                                }

                                // Start of the section
                                inTargetSection = false;
                                // Only sections contained in iniKeys will be targeted
                                for (int i = 0; i < keys.Count; i++)
                                {
                                    if (processedKeys.Contains(i))
                                        continue;

                                    if (foundSection.Equals(keys[i].Section.AsSpan(), StringComparison.OrdinalIgnoreCase))
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
                                    for (int i = 0; i < keys.Count; i++)
                                    {
                                        if (processedKeys.Contains(i))
                                            continue;

                                        // Add to last line of foundSection
                                        if (currentSection.Equals(keys[i].Section.AsSpan(), StringComparison.OrdinalIgnoreCase))
                                        {
                                            if (!thisLineWritten)
                                                writer.WriteLine(rawLine);
                                            thisLineWritten = true;

                                            processedKeys.Add(i);
                                            writer.WriteLine(keys[i].Key);
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
                                    for (int i = 0; i < keys.Count; i++)
                                    {
                                        if (processedKeys.Contains(i))
                                            continue;

                                        if (currentSection.Equals(keys[i].Section.AsSpan(), StringComparison.OrdinalIgnoreCase))
                                        { // append key to section
                                            processedKeys.Add(i);
                                            writer.WriteLine(keys[i].Key);
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
                                for (int i = 0; i < keys.Count; i++)
                                {
                                    if (processedKeys.Contains(i))
                                        continue;

                                    // Add to last line of foundSection
                                    if (currentSection.Equals(keys[i].Section.AsSpan(), StringComparison.OrdinalIgnoreCase))
                                    {
                                        processedKeys.Add(i);

                                        if (!thisLineWritten)
                                            writer.WriteLine(rawLine);
                                        thisLineWritten = true;

                                        writer.WriteLine(keys[i].Key);
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
                            for (int i = 0; i < keys.Count; i++)
                            { // At this time, only not found section remains in iniKeys
                                if (processedKeys.Contains(i))
                                    continue;

                                if (!processedSections.Any(s => s.Equals(keys[i].Section, StringComparison.OrdinalIgnoreCase)))
                                {
                                    if (!thisLineWritten)
                                        writer.WriteLine(rawLine);
                                    thisLineWritten = true;

                                    processedSections.Add(keys[i].Section);
                                    writer.WriteLine($"\r\n[{keys[i].Section}]");
                                }

                                processedKeys.Add(i);

                                writer.WriteLine(keys[i].Key);
                            }
                        }

                        if (!thisLineWritten)
                            writer.WriteLine(rawLine);
                    }
                }

                if (processedKeys.Count == keys.Count)
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
        /// <param name="iniKey">Enumarable tuples of Section, OldKey, and NewKey. Value is treated as NewKey.</param>
        /// <returns>Returns true if the operation was successful.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool[] RenameKeys(string filePath, IEnumerable<IniKey> iniKeys)
        {
            return InternalRenameKeys(filePath, iniKeys.ToArray());
        }

        /// <summary>
        /// Internal method for RenameKeys
        /// </summary>
        /// <param name="file"></param>
        /// <param name="iniKeys">Use Value for new names of Key</param>
        /// <returns></returns>
        private static bool[] InternalRenameKeys(string file, IEnumerable<IniKey> iniKeys)
        {
            IniKey[] keys = iniKeys.ToArray();
            bool[] processed = new bool[keys.Length];

            if (!File.Exists(file))
                return processed; // All False

            string tempPath = FileHelper.GetTempFile();
            try
            {
                Encoding encoding = EncodingHelper.DetectBom(file);
                using (StreamReader r = new StreamReader(file, encoding, false))
                using (StreamWriter w = new StreamWriter(tempPath, false, encoding))
                {
                    if (r.Peek() == -1)
                    {
                        r.Close();
                        return processed; // All False
                    }

                    string rawLine;
                    bool inTargetSection = false;
                    ReadOnlySpan<char> currentSection = null;

                    while ((rawLine = r.ReadLine()) != null)
                    { // Read text line by line
                        bool thisLineProcessed = false;
                        ReadOnlySpan<char> line = rawLine.AsSpan().Trim();

                        // Ignore comments. If you deleted all keys successfully, also skip.
                        if (processed.Count(x => !x) == 0 || IsLineComment(line))
                        {
                            w.WriteLine(rawLine);
                            continue;
                        }

                        // Check if encountered section head Ex) [Process]
                        if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                            line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
                        {
                            ReadOnlySpan<char> foundSection = line.Slice(1, line.Length - 2);

                            // Start of the section
                            inTargetSection = false;
                            // Only sections contained in iniKeys will be targeted
                            for (int i = 0; i < keys.Length; i++)
                            {
                                if (processed[i])
                                    continue;

                                if (foundSection.Equals(keys[i].Section.AsSpan(), StringComparison.OrdinalIgnoreCase))
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
                                ReadOnlySpan<char> lineKey = line.Slice(0, idx).Trim();
                                ReadOnlySpan<char> lineValue = line.Slice(idx + 1).Trim();
                                for (int i = 0; i < keys.Length; i++)
                                {
                                    if (processed[i])
                                        continue;

                                    IniKey key = keys[i];
                                    if (currentSection.Equals(key.Section.AsSpan(), StringComparison.OrdinalIgnoreCase)
                                        && lineKey.Equals(key.Key.AsSpan(), StringComparison.OrdinalIgnoreCase))
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
                    FileHelper.FileReplaceEx(tempPath, file);
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
        /// <returns>An array of return value for each IniKey. Returns true if the operation of an iniKey was successful.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool[] DeleteKeys(string filePath, IEnumerable<IniKey> iniKeys)
        {
            return InternalDeleteKeys(filePath, iniKeys);
        }

        private static bool[] InternalDeleteKeys(string filePath, IEnumerable<IniKey> iniKeys)
        {
            IniKey[] keys = iniKeys.ToArray();
            bool[] processed = new bool[keys.Length];
            for (int i = 0; i < processed.Length; i++)
                processed[i] = false;

            if (!File.Exists(filePath))
                return processed; // All False

            string tempPath = FileHelper.GetTempFile();
            try
            {
                Encoding encoding = EncodingHelper.DetectBom(filePath);
                using (StreamReader r = new StreamReader(filePath, encoding, false))
                using (StreamWriter w = new StreamWriter(tempPath, false, encoding))
                {
                    if (r.Peek() == -1)
                    {
                        r.Close();
                        return processed; // All False
                    }

                    string rawLine;
                    bool inTargetSection = false;
                    ReadOnlySpan<char> currentSection = null;

                    while ((rawLine = r.ReadLine()) != null)
                    { // Read text line by line
                        bool thisLineProcessed = false;
                        ReadOnlySpan<char> line = rawLine.AsSpan().Trim();

                        // Ignore comments. If you deleted all keys successfully, also skip.
                        if (processed.Count(x => !x) == 0 || IsLineComment(line))
                        {
                            w.WriteLine(rawLine);
                            continue;
                        }

                        // Check if encountered section head Ex) [Process]
                        if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                            line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
                        {
                            ReadOnlySpan<char> foundSection = line.Slice(1, line.Length - 2);

                            // Start of the section
                            inTargetSection = false;
                            // Only sections contained in iniKeys will be targeted
                            for (int i = 0; i < keys.Length; i++)
                            {
                                if (processed[i])
                                    continue;

                                if (foundSection.Equals(keys[i].Section.AsSpan(), StringComparison.OrdinalIgnoreCase))
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
                                ReadOnlySpan<char> lineKey = line.Slice(0, idx).Trim();
                                for (int i = 0; i < keys.Length; i++)
                                {
                                    if (processed[i])
                                        continue;

                                    if (currentSection.Equals(keys[i].Section.AsSpan(), StringComparison.OrdinalIgnoreCase) &&
                                        lineKey.Equals(keys[i].Key.AsSpan(), StringComparison.OrdinalIgnoreCase))
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

                if (0 < processed.Count(x => x))
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
            return InternalDeleteCompactKeys(filePath, iniKeys);
        }

        private static bool[] InternalDeleteCompactKeys(string filePath, IEnumerable<IniKey> iniKeys)
        {
            IniKey[] keys = iniKeys.ToArray();
            bool[] processed = new bool[keys.Length];
            for (int i = 0; i < processed.Length; i++)
                processed[i] = false;

            if (!File.Exists(filePath))
                return processed; // All False

            string tempPath = FileHelper.GetTempFile();
            try
            {
                Encoding encoding = EncodingHelper.DetectBom(filePath);
                using (StreamReader r = new StreamReader(filePath, encoding, false))
                using (StreamWriter w = new StreamWriter(tempPath, false, encoding))
                {
                    if (r.Peek() == -1)
                    {
                        r.Close();
                        return processed; // All False
                    }

                    string rawLine;
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
                        if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                            line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
                        {
                            ReadOnlySpan<char> foundSection = line.Slice(1, line.Length - 2);

                            // Start of the section
                            inTargetSection = false;
                            // Only sections contained in iniKeys will be targeted
                            for (int i = 0; i < keys.Length; i++)
                            {
                                if (processed[i])
                                    continue;

                                if (foundSection.Equals(keys[i].Section.AsSpan(), StringComparison.OrdinalIgnoreCase))
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
                                ReadOnlySpan<char> lineKey = line.Slice(0, idx).Trim();
                                for (int i = 0; i < keys.Length; i++)
                                {
                                    if (processed[i])
                                        continue;

                                    if (currentSection.Equals(keys[i].Section.AsSpan(), StringComparison.OrdinalIgnoreCase) &&
                                        lineKey.Equals(keys[i].Key.AsSpan(), StringComparison.OrdinalIgnoreCase))
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

                if (0 < processed.Count(x => x))
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
        /// <returns>An array of IniKey. Each IniKey represents a pair of key and value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IniKey[] ReadSection(string filePath, string section)
        {
            return InternalReadSection(filePath, new string[] { section }).Select(x => x.Value).First();
        }

        /// <summary>
        /// Read entire pairs of key and value from an ini file section.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="iniKey">Name of the section to read. Key and Value is ignored.</param>
        /// <returns>An array of IniKey. Each IniKey represents a pair of key and value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IniKey[] ReadSection(string filePath, IniKey iniKey)
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
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Dictionary<string, IniKey[]> ReadSections(string filePath, IEnumerable<string> sections)
        {
            return InternalReadSection(filePath, sections);
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
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Dictionary<string, IniKey[]> ReadSections(string filePath, IEnumerable<IniKey> iniKeys)
        {
            return InternalReadSection(filePath, iniKeys.Select(x => x.Section));
        }

        private static Dictionary<string, IniKey[]> InternalReadSection(string filePath, IEnumerable<string> sections)
        {
            string[] sectionNames = sections.ToArray();
            Dictionary<string, List<IniKey>> secDict = new Dictionary<string, List<IniKey>>(StringComparer.OrdinalIgnoreCase);
            foreach (string section in sectionNames)
                secDict[section] = null;

            Encoding encoding = EncodingHelper.DetectBom(filePath);
            using (StreamReader reader = new StreamReader(filePath, encoding, true))
            {
                string rawLine;
                bool inTargetSection = false;
                string currentSection = null;
                List<IniKey> currentIniKeys = null;

                while ((rawLine = reader.ReadLine()) != null)
                { // Read text line by line
                    ReadOnlySpan<char> line = rawLine.AsSpan().Trim(); // Remove whitespace
                    if (line.StartsWith("#".AsSpan(), StringComparison.Ordinal) ||
                        line.StartsWith(";".AsSpan(), StringComparison.Ordinal) ||
                        line.StartsWith("//".AsSpan(), StringComparison.Ordinal)) // Ignore comment
                        continue;

                    if (inTargetSection)
                    {
                        Debug.Assert(currentSection != null);

                        int idx = line.IndexOf('=');
                        if (idx != -1 && idx != 0) // there is key, and key name is not empty
                        {
                            string key = line.Slice(0, idx).Trim().ToString();
                            string value = line.Slice(idx + 1).Trim().ToString();
                            currentIniKeys.Add(new IniKey(currentSection, key, value));
                        }
                        else
                        {
                            // Search if current section ended
                            if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                                line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
                            {
                                // Only sections contained in sectionNames will be targeted
                                inTargetSection = false;
                                currentSection = null;

                                string foundSection = line.Slice(1, line.Length - 2).ToString();
                                int sIdx = Array.FindIndex(sectionNames, x => x.Equals(foundSection, StringComparison.OrdinalIgnoreCase));
                                if (sIdx != -1)
                                {
                                    inTargetSection = true;
                                    currentSection = foundSection;
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
                        if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                            line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
                        {
                            // Only sections contained in iniKeys will be targeted
                            string foundSection = line.Slice(1, line.Length - 2).ToString();
                            int sIdx = Array.FindIndex(sectionNames, x => x.Equals(foundSection, StringComparison.OrdinalIgnoreCase));
                            if (sIdx != -1)
                            {
                                inTargetSection = true;
                                currentSection = foundSection;
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
            return InternalAddSection(filePath, sections);
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

        private static bool InternalAddSection(string filePath, IEnumerable<string> sections)
        {
            List<string> sectionList = sections.ToList();

            // Not exist -> Create and exit
            if (!File.Exists(filePath))
            {
                using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    for (int i = 0; i < sectionList.Count; i++)
                    {
                        if (0 < i)
                            writer.WriteLine();
                        writer.WriteLine($"[{sectionList[i]}]");
                    }

                    writer.Close();
                }
                return true;
            }

            bool result = true;
            string tempPath = FileHelper.GetTempFile();
            try
            {
                Encoding encoding = EncodingHelper.DetectBom(filePath);
                using (StreamReader r = new StreamReader(filePath, encoding, false))
                using (StreamWriter w = new StreamWriter(tempPath, false, encoding))
                {
                    // Is Original File Empty?
                    if (r.Peek() == -1)
                    {
                        r.Close();

                        // Write all and exit
                        for (int i = 0; i < sectionList.Count; i++)
                        {
                            if (0 < i)
                                w.WriteLine();
                            w.WriteLine($"[{sectionList[i]}]");
                        }

                        w.Close();

                        FileHelper.FileReplaceEx(tempPath, filePath);
                        return true;
                    }

                    string rawLine;
                    List<string> processedSections = new List<string>(sectionList.Count);

                    while ((rawLine = r.ReadLine()) != null)
                    { // Read text line by line
                        bool thisLineProcessed = false;
                        ReadOnlySpan<char> line = rawLine.AsSpan().Trim();

                        // Check if encountered section head Ex) [Process]
                        if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                            line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
                        {
                            ReadOnlySpan<char> foundSection = line.Slice(1, line.Length - 2);

                            // Start of the section;
                            // Only sections contained in iniKeys will be targeted
                            for (int i = 0; i < sectionList.Count; i++)
                            {
                                if (foundSection.Equals(sectionList[i].AsSpan(), StringComparison.OrdinalIgnoreCase))
                                {
                                    processedSections.Add(foundSection.ToString());
                                    sectionList.RemoveAt(i);
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
                            List<int> processedIdxs = new List<int>(sectionList.Count);
                            for (int i = 0; i < sectionList.Count; i++)
                            { // At this time, only not found section remains in iniKeys
                                if (processedSections.Any(s => s.Equals(sectionList[i], StringComparison.OrdinalIgnoreCase)) == false)
                                {
                                    processedSections.Add(sectionList[i]);
                                    w.WriteLine($"\r\n[{sectionList[i]}]");
                                }
                                processedIdxs.Add(i);
                            }
                            foreach (int i in processedIdxs.OrderByDescending(x => x))
                                sectionList.RemoveAt(i);
                        }
                    }
                }

                if (sectionList.Count == 0)
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

        private static bool InternalWriteSectionFast(string file, string section, object content)
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
                        string readLine;
                        while ((readLine = tr.ReadLine()) != null)
                            w.WriteLine(readLine);
                        break;
                    default:
                        throw new ArgumentException("Invalid content");
                }
            }

            // If file does not exist or blank, just create new file and insert keys.
            if (!File.Exists(file))
            {
                using (StreamWriter w = new StreamWriter(file, false, Encoding.UTF8))
                {
                    WriteContent(w);
                }

                return true;
            }

            string tempPath = FileHelper.GetTempFile();
            try
            {
                bool finished = false;
                Encoding encoding = EncodingHelper.DetectBom(file);
                using (StreamReader r = new StreamReader(file, encoding, false))
                using (StreamWriter w = new StreamWriter(tempPath, false, encoding))
                {
                    string rawLine;
                    bool passThisSection = false;

                    // Main Logic
                    while ((rawLine = r.ReadLine()) != null)
                    { // Read text line by line
                        ReadOnlySpan<char> line = rawLine.AsSpan().Trim();

                        // Check if encountered section head Ex) [Process]
                        if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                            line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
                        {
                            passThisSection = false;

                            ReadOnlySpan<char> foundSection = line.Slice(1, line.Length - 2);
                            if (foundSection.Equals(section.AsSpan(), StringComparison.OrdinalIgnoreCase))
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

                FileHelper.FileReplaceEx(tempPath, file);
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
            return InternalRenameSection(filePath, iniKeys);
        }

        private static bool[] InternalRenameSection(string filePath, IEnumerable<IniKey> iniKeys)
        {
            IniKey[] keys = iniKeys.ToArray();
            bool[] processed = new bool[keys.Length];

            if (!File.Exists(filePath))
                return processed;

            string tempPath = FileHelper.GetTempFile();
            try
            {
                Encoding encoding = EncodingHelper.DetectBom(filePath);
                using (StreamReader r = new StreamReader(filePath, encoding, false))
                using (StreamWriter w = new StreamWriter(tempPath, false, encoding))
                {
                    if (r.Peek() == -1)
                    {
                        r.Close();
                        return processed;
                    }

                    string rawLine;

                    // Main Logic
                    while ((rawLine = r.ReadLine()) != null)
                    { // Read text line by line
                        ReadOnlySpan<char> line = rawLine.AsSpan().Trim();
                        bool thisLineProcessed = false;

                        // Check if encountered section head Ex) [Process]
                        if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                            line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
                        {
                            ReadOnlySpan<char> foundSection = line.Slice(1, line.Length - 2);

                            // Start of the section;
                            // Only sections contained in iniKeys will be targeted
                            for (int i = 0; i < keys.Length; i++)
                            {
                                if (processed[i])
                                    continue;

                                IniKey key = keys[i];
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
            return InternalDeleteSection(filePath, new string[] { section })[0];
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
            return InternalDeleteSection(filePath, new string[] { iniKey.Section })[0];
        }

        /// <summary>
        /// Delete sections from an .ini file.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="sections">Enumerable names of the target section.</param>
        /// <returns>An array of return value for each IniKey. Returns true if the operation of an iniKey was successful.</returns>
        public static bool[] DeleteSections(string filePath, IEnumerable<string> sections)
        {
            return InternalDeleteSection(filePath, sections);
        }

        /// <summary>
        /// Delete sections from an .ini file.
        /// </summary>
        /// <param name="filePath">An ini file to manipulate.</param>
        /// <param name="iniKeys">Enumerable names of the target section. Key and Value is ignored.</param>
        /// <returns>An array of return value for each IniKey. Returns true if the operation of an iniKey was successful.</returns>
        public static bool[] DeleteSections(string filePath, IEnumerable<IniKey> iniKeys)
        {
            return InternalDeleteSection(filePath, iniKeys.Select(x => x.Section));
        }

        private static bool[] InternalDeleteSection(string filePath, IEnumerable<string> sections)
        {
            List<string> sectionList = sections.ToList();
            bool[] processed = new bool[sectionList.Count];

            if (!File.Exists(filePath))
                return processed;

            string tempPath = FileHelper.GetTempFile();
            try
            {
                Encoding encoding = EncodingHelper.DetectBom(filePath);
                using (StreamReader r = new StreamReader(filePath, encoding, false))
                using (StreamWriter w = new StreamWriter(tempPath, false, encoding))
                {
                    if (r.Peek() == -1)
                    {
                        r.Close();
                        return processed;
                    }

                    string rawLine;
                    bool ignoreCurrentSection = false;

                    // Main Logic
                    while ((rawLine = r.ReadLine()) != null)
                    { // Read text line by line
                        ReadOnlySpan<char> line = rawLine.AsSpan().Trim();

                        // Check if encountered section head Ex) [Process]
                        if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                            line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
                        {
                            ReadOnlySpan<char> foundSection = line.Slice(1, line.Length - 2);
                            ignoreCurrentSection = false;

                            // Start of the section;
                            // Only sections contained in iniKeys will be targeted
                            for (int i = 0; i < sectionList.Count; i++)
                            {
                                if (processed[i])
                                    continue;

                                if (foundSection.Equals(sectionList[i].AsSpan(), StringComparison.OrdinalIgnoreCase))
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
            return InternalReadRawSection(filePath, new string[] { section }, false).Select(x => x.Value).First();
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
            return InternalReadRawSection(filePath, new string[] { section }, includeEmptyLines).Select(x => x.Value).First();
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
            return InternalReadRawSection(filePath, new string[] { iniKey.Section }, false).Select(x => x.Value).First();
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
            return InternalReadRawSection(filePath, new string[] { iniKey.Section }, includeEmptyLines).Select(x => x.Value).First();
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
            return InternalReadRawSection(filePath, sections, false);
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
            return InternalReadRawSection(filePath, sections, includeEmptyLines);
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
            return InternalReadRawSection(filePath, iniKeys.Select(x => x.Section), false);
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
            return InternalReadRawSection(filePath, iniKeys.Select(x => x.Section), includeEmptyLines);
        }

        private static Dictionary<string, List<string>> InternalReadRawSection(string filePath, IEnumerable<string> sections, bool includeEmptyLines)
        {
            List<string> sectionNames = sections.ToList();
            Dictionary<string, List<string>> secDict = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            Encoding encoding = EncodingHelper.DetectBom(filePath);
            using (StreamReader r = new StreamReader(filePath, encoding, false))
            {
                string rawLine;
                bool inTargetSection = false;
                string currentSection = null;
                List<string> currentContents = null;

                while ((rawLine = r.ReadLine()) != null)
                { // Read text line by line
                    ReadOnlySpan<char> line = rawLine.AsSpan().Trim(); // Remove whitespace
                    if (line.StartsWith("#".AsSpan(), StringComparison.Ordinal) ||
                        line.StartsWith(";".AsSpan(), StringComparison.Ordinal) ||
                        line.StartsWith("//".AsSpan(), StringComparison.Ordinal)) // Ignore comment
                        continue;

                    if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                        line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
                    {
                        inTargetSection = false;

                        string foundSection = line.Slice(1, line.Length - 2).ToString();
                        int sIdx = sectionNames.FindIndex(x => x.Equals(foundSection, StringComparison.OrdinalIgnoreCase));
                        if (sIdx != -1)
                        {
                            inTargetSection = true;
                            currentSection = foundSection;

                            secDict[currentSection] = new List<string>(16);
                            currentContents = secDict[currentSection];

                            sectionNames.RemoveAt(sIdx);
                            continue;
                        }
                    }

                    if (inTargetSection)
                    {
                        Debug.Assert(currentSection != null);

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
                Encoding encoding = EncodingHelper.DetectBom(filePath);
                using (StreamReader r = new StreamReader(filePath, encoding, false))
                using (StreamWriter w = new StreamWriter(tempPath, false, encoding))
                {
                    while (true)
                    {
                        string rawLine = r.ReadLine();
                        if (rawLine == null) // End of file
                            break;

                        ReadOnlySpan<char> line = rawLine.AsSpan().Trim();
                        if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) && line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
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

            Encoding encoding = EncodingHelper.DetectBom(filePath);
            using (StreamReader r = new StreamReader(filePath, encoding, true))
            {
                string rawLine;
                while ((rawLine = r.ReadLine()) != null)
                { // Read text line by line
                    ReadOnlySpan<char> line = rawLine.AsSpan().Trim();
                    if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                        line.EndsWith("]".AsSpan(), StringComparison.Ordinal)) // Count sections
                        sections.Add(line.Slice(1, line.Length - 2).ToString());
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

            Encoding encoding = EncodingHelper.DetectBom(filePath);
            using (StreamReader r = new StreamReader(filePath, encoding, false))
            {
                string rawLine;
                while ((rawLine = r.ReadLine()) != null)
                { // Read text line by line
                    ReadOnlySpan<char> line = rawLine.AsSpan().Trim();
                    if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                        line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
                    {
                        ReadOnlySpan<char> foundSection = line.Slice(1, line.Length - 2);
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
        /// <returns>A dictionary of keys and values.</returns>
        public static Dictionary<string, string> ParseIniSectionToDict(string filePath, string section)
        {
            List<string> lines = ParseIniSection(filePath, section);
            return lines == null ? null : ParseIniLinesIniStyle(lines);
        }

        /// <summary>
        /// Faster, specialized version of InternalReadRawSection.
        /// </summary>
        /// <param name="filePath">An .ini file to read.</param>
        /// <param name="section">Name of a target section.</param>
        /// <returns>A list of section content text lines.</returns>
        public static List<string> ParseIniSection(string filePath, string section)
        {
            List<string> lines = new List<string>();

            Encoding encoding = EncodingHelper.DetectBom(filePath);
            using (StreamReader r = new StreamReader(filePath, encoding, false))
            {
                string rawLine;
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
                        ReadOnlySpan<char> foundSection = line.Slice(1, line.Length - 2);
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
        /// <returns>A list of section content text lines.</returns>
        public static List<string> ParseRawSection(string filePath, string section)
        {
            List<string> lines = new List<string>();

            Encoding encoding = EncodingHelper.DetectBom(filePath);
            using (StreamReader r = new StreamReader(filePath, encoding, false))
            {
                string rawLine;
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
                        ReadOnlySpan<char> foundSection = line.Slice(1, line.Length - 2);
                        if (foundSection.Equals(section.AsSpan(), StringComparison.OrdinalIgnoreCase))
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

            Encoding encoding = EncodingHelper.DetectBom(filePath);
            using (StreamReader r = new StreamReader(filePath, encoding, true))
            {
                string rawLine;
                int currentSection = -1; // -1 == empty, 0, 1, ... == index value of sections array
                List<int> processedSectionIdxs = new List<int>();

                while ((rawLine = r.ReadLine()) != null)
                { // Read text line by line
                    if (sectionNames.Length < processedSectionIdxs.Count)
                        break;

                    ReadOnlySpan<char> line = rawLine.AsSpan().Trim();
                    if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                        line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
                    { // Start of section
                        bool isSectionFound = false;
                        ReadOnlySpan<char> foundSection = line.Slice(1, line.Length - 2);
                        for (int i = 0; i < sectionNames.Length; i++)
                        {
                            if (processedSectionIdxs.Contains(i))
                                continue;

                            if (foundSection.Equals(sectionNames[i].AsSpan(), StringComparison.Ordinal))
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

            Encoding encoding = EncodingHelper.DetectBom(srcFile);
            using (StreamReader r = new StreamReader(srcFile, encoding))
            {
                // Is Original File Empty?
                if (r.Peek() == -1) // Return Empty Dict
                    return dict;

                string rawLine;
                string section = null;

                while ((rawLine = r.ReadLine()) != null)
                { // Read text line by line
                    ReadOnlySpan<char> line = rawLine.AsSpan().Trim(); // Remove whitespace
                    if (line.StartsWith("#".AsSpan(), StringComparison.Ordinal) ||
                        line.StartsWith(";".AsSpan(), StringComparison.Ordinal) ||
                        line.StartsWith("//".AsSpan(), StringComparison.Ordinal)) // Ignore comment
                        continue;

                    // Check if encountered section head Ex) [Process]
                    if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                        line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
                    {
                        section = line.Slice(1, line.Length - 2).ToString();
                        dict[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        continue;
                    }

                    // Read Keys
                    if (section != null)
                    {
                        int idx = line.IndexOf('=');
                        if (idx != -1 && idx != 0) // there is key, and key name is not empty
                        {
                            string key = line.Slice(0, idx).Trim().ToString();
                            string value = line.Slice(idx + 1).Trim().ToString();

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
            string rawLine;
            while ((rawLine = tr.ReadLine()) != null)
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
                    ReadOnlySpan<char> foundSection = line.Slice(1, line.Length - 2); // Remove [ and ]

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
        public static void FastForwardTextWriter(TextReader tr, TextWriter tw, string section, bool copyFromNewSection)
        {
            bool enableCopy = !copyFromNewSection;

            string rawLine;
            while ((rawLine = tr.ReadLine()) != null)
            { // Read text line by line
                if (enableCopy)
                    tw.WriteLine(rawLine);

                if (section == null && !enableCopy)
                {
                    ReadOnlySpan<char> line = rawLine.AsSpan().Trim();
                    if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                        line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
                    { // Start of section
                        tw.WriteLine(rawLine);
                        enableCopy = true;
                    }
                }
                else if (section != null)
                {
                    ReadOnlySpan<char> line = rawLine.AsSpan().Trim();
                    if (line.StartsWith("[".AsSpan(), StringComparison.Ordinal) &&
                        line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
                    { // Start of section
                        if (!enableCopy)
                            tw.WriteLine(rawLine);
                        enableCopy = true;

                        // Found target section, so return
                        ReadOnlySpan<char> foundSection = line.Slice(1, line.Length - 2); // Remove [ and ]
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

        #region (Utility) IsLineComment
        /// <summary>
        /// Check whether a text line is a comment or not.
        /// </summary>
        /// <param name="line">A text line to check.</param>
        /// <returns>Returns true if a text line is a comment.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsLineComment(string line)
        {
            return IsLineComment(line.AsSpan());
        }

        /// <summary>
        /// Check whether a text line is a comment or not.
        /// </summary>
        /// <param name="lineSpan">A text line to check.</param>
        /// <returns>Returns true if a text line is a comment.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsLineComment(ReadOnlySpan<char> lineSpan)
        {
            return lineSpan.StartsWith("#".AsSpan(), StringComparison.Ordinal) ||
                   lineSpan.StartsWith(";".AsSpan(), StringComparison.Ordinal) ||
                   lineSpan.StartsWith("//".AsSpan(), StringComparison.Ordinal);
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
            Dictionary<string, string> dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in lines)
            {
                Regex regexInstance = new Regex(regex, RegexOptions.Compiled);
                MatchCollection matches = regexInstance.Matches(line);

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
        public static (string key, string value) GetKeyValueFromLine(string rawLine)
        {
            int idx = rawLine.IndexOf('=');
            if (idx == -1) // Unable to find key and value
                return (null, null);

            string key = rawLine.AsSpan().Slice(0, idx).Trim().ToString();
            string value = rawLine.AsSpan().Slice(idx + 1).Trim().ToString();
            return (key, value);
        }

        /// <summary>
        /// Used to handle [EncodedFile-InterfaceEncoded-*] section.
        /// </summary>
        /// <returns>
        /// Null is returned if failed.
        /// </returns>
        public static (List<string> Keys, List<string> Values) GetKeyValueFromLines(IReadOnlyList<string> rawLines)
        {
            List<string> keys = new List<string>();
            List<string> values = new List<string>();
            foreach (string rawLine in rawLines)
            {
                (string key, string value) = GetKeyValueFromLine(rawLine);
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string CompactKeyValuePairLine(ReadOnlySpan<char> lineSpan)
        {
            int idx = lineSpan.IndexOf('=');
            if (idx != -1 && idx != 0) // there is key, and key name is not empty
            { // Ini style entry, reformat into [Key=Value] template
                string key = lineSpan.Slice(0, idx).Trim().ToString();
                string value = lineSpan.Slice(idx + 1).Trim().ToString();
                return $"{key}={value}";
            }
            else
            { // Non-ini style entry, trim only end of it (Commands are often indented)
                return lineSpan.ToString();
            }
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
