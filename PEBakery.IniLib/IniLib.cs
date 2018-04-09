/*
    Copyright (C) 2016-2018 Hajin Jang
 
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections;
using System.Text.RegularExpressions;
using System.Threading;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace PEBakery.IniLib
{
    #region IniKey
    public struct IniKey
    {
        public string Section;
        public string Key;
        public string Value; // In GetKeys, this record is not used, set to null

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
    }
    #endregion

    #region Ini Class
    public static class Ini
    {
        #region Lock
        private static readonly ConcurrentDictionary<string, ReaderWriterLockSlim> lockDict =
            new ConcurrentDictionary<string, ReaderWriterLockSlim>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Must be called when Ini class is not used
        /// </summary>
        public static void ClearLockDict()
        {
            lockDict.Clear();
        }
        #endregion

        #region GetKey
        /// <summary>
        /// Get key's value from ini file.
        /// </summary>
        public static string GetKey(string file, IniKey iniKey)
        {
            IniKey[] iniKeys = InternalGetKeys(file, new IniKey[] { iniKey });
            return iniKeys[0].Value;
        }
        /// <summary>
        /// Get key's value from ini file.
        /// </summary>
        public static string GetKey(string file, string section, string key)
        {
            IniKey[] iniKeys = InternalGetKeys(file, new IniKey[] { new IniKey(section, key) });
            return iniKeys[0].Value;
        }
        /// <summary>
        /// Get key's value from ini file.
        /// </summary>
        public static IniKey[] GetKeys(string file, IEnumerable<IniKey> iniKeys)
        {
            return InternalGetKeys(file, iniKeys.ToArray());
        }
        private static IniKey[] InternalGetKeys(string file, IniKey[] iniKeys)
        {
            ReaderWriterLockSlim rwLock;
            if (lockDict.ContainsKey(file))
            {
                rwLock = lockDict[file];
            }
            else
            {
                rwLock = new ReaderWriterLockSlim();
                lockDict[file] = rwLock;
            }

            rwLock.EnterReadLock();
            try
            {
                List<int> processedKeyIdxs = new List<int>(iniKeys.Length);

                Encoding encoding = IniHelper.DetectTextEncoding(file);
                using (StreamReader reader = new StreamReader(file, encoding, true))
                {
                    // int len = iniKeys.Count;
                    string line;
                    bool inTargetSection = false;
                    string currentSection = null;

                    while ((line = reader.ReadLine()) != null)
                    { // Read text line by line
                        if (processedKeyIdxs.Count == iniKeys.Length) // Work Done
                            break;

                        line = line.Trim(); // Remove whitespace
                        if (line.StartsWith("#", StringComparison.Ordinal) ||
                            line.StartsWith(";", StringComparison.Ordinal) ||
                            line.StartsWith("//", StringComparison.Ordinal)) // Ignore comment
                            continue;

                        if (inTargetSection)
                        {
                            int idx = line.IndexOf('=');
                            if (idx != -1 && idx != 0) // there is key, and key name is not empty
                            {
                                string keyName = line.Substring(0, idx).Trim();
                                for (int i = 0; i < iniKeys.Length; i++)
                                {
                                    if (processedKeyIdxs.Contains(i))
                                        continue;

                                    // Only if <section, key> is same, copy value;
                                    IniKey iniKey = iniKeys[i];
                                    if (currentSection.Equals(iniKey.Section, StringComparison.OrdinalIgnoreCase) &&
                                        keyName.Equals(iniKey.Key, StringComparison.OrdinalIgnoreCase))
                                    {
                                        iniKey.Value = line.Substring(idx + 1).Trim();
                                        iniKeys[i] = iniKey;
                                        processedKeyIdxs.Add(i);
                                    }
                                }
                            }
                            else
                            {
                                // search if current section has end
                                if (line.StartsWith("[", StringComparison.Ordinal) &&
                                    line.EndsWith("]", StringComparison.Ordinal))
                                {
                                    // Only sections contained in iniKeys will be targeted
                                    inTargetSection = false;
                                    currentSection = null;
                                    string foundSection = line.Substring(1, line.Length - 2);
                                    for (int i = 0; i < iniKeys.Length; i++)
                                    {
                                        if (processedKeyIdxs.Contains(i))
                                            continue;

                                        if (foundSection.Equals(iniKeys[i].Section, StringComparison.OrdinalIgnoreCase))
                                        {
                                            inTargetSection = true;
                                            currentSection = foundSection;
                                            break; // for shorter O(n)
                                        }
                                    }
                                }
                            }
                        }
                        else
                        { // not in section
                          // Check if encountered section head Ex) [Process]
                            if (line.StartsWith("[", StringComparison.Ordinal) &&
                                line.EndsWith("]", StringComparison.Ordinal))
                            {
                                // Only sections contained in iniKeys will be targeted
                                string foundSection = line.Substring(1, line.Length - 2);
                                for (int i = 0; i < iniKeys.Length; i++)
                                {
                                    if (processedKeyIdxs.Contains(i))
                                        continue;

                                    if (foundSection.Equals(iniKeys[i].Section, StringComparison.OrdinalIgnoreCase))
                                    {
                                        inTargetSection = true;
                                        currentSection = foundSection;
                                        break; // for shorter O(n)
                                    }
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                rwLock.ExitReadLock();
            }

            return iniKeys;
        }
        #endregion

        #region SetKey
        public static bool SetKey(string file, string section, string key, string value)
        {
            return InternalSetKeys(file, new List<IniKey> { new IniKey(section, key, value) });
        }

        public static bool SetKey(string file, IniKey iniKey)
        {
            return InternalSetKeys(file, new List<IniKey> { iniKey });
        }

        public static bool SetKeys(string file, IEnumerable<IniKey> iniKeys)
        {
            return InternalSetKeys(file, iniKeys.ToList());
        }

        private static bool InternalSetKeys(string file, List<IniKey> inputKeys)
        {
            ReaderWriterLockSlim rwLock;
            if (lockDict.ContainsKey(file))
            {
                rwLock = lockDict[file];
            }
            else
            {
                rwLock = new ReaderWriterLockSlim();
                lockDict[file] = rwLock;
            }

            #region FinalizeFile
            void FinalizeFile(StreamWriter w, string lastLine, bool firstEmptyLine)
            {
                bool firstSection = true;
                if (0 < inputKeys.Count)
                {
                    // Call ToArray() to make a copy of inputKeys
                    string[] unprocessedSections = inputKeys
                        .Select(x => x.Section)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    foreach (string section in unprocessedSections)
                    {
                        IniKey[] secKeys = inputKeys
                            .Where(x => x.Section.Equals(section))
                            .ToArray();

                        if ((lastLine == null || lastLine.Length != 0) && (!firstSection || firstEmptyLine))
                            w.WriteLine();
                        w.WriteLine($"[{section}]");

                        foreach (var secKey in secKeys)
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

            rwLock.EnterWriteLock();
            try
            {
                // If file does not exist just create new file and insert keys.
                if (!File.Exists(file))
                {
                    using (StreamWriter w = new StreamWriter(file, false, Encoding.UTF8))
                    {
                        FinalizeFile(w, null, false);
                    }
                    return true;
                }

                // Append IniKey into existing file
                string tempPath = Path.GetTempFileName();
                Encoding encoding = IniHelper.DetectTextEncoding(file);
                using (StreamReader r = new StreamReader(file, encoding, true))
                using (StreamWriter w = new StreamWriter(tempPath, false, encoding))
                {
                    bool inTargetSection = false;
                    string currentSection = null;
                    List<Tuple<string, string>> lineBuffer = new List<Tuple<string, string>>(32);

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
                            string targetLine = lineBuffer[i].Item1;
                            if (targetLine.Length == 0)
                                lineBuffer.RemoveAt(i);
                            else
                                break;
                        }

                        // lineBuffer 내에 있는 key들을 검사해서 덮어 쓸 것들을 확인한다.
                        foreach (var tup in lineBuffer)
                        {
                            string targetLine = tup.Item1;
                            string targetRawLine = tup.Item2;

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
                                string targetKey = targetLine.Substring(0, eIdx).Trim();

                                // Call ToArray() to make copy of secKeys
                                foreach (IniKey secKey in secKeys.ToArray())
                                {
                                    if (targetKey.Equals(secKey.Key, StringComparison.OrdinalIgnoreCase))
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
                    string lastLine = null;
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
                        
                        string line = rawLine.Trim();
                            
                        // Section head like [Process] encountered
                        if (line.StartsWith("[", StringComparison.Ordinal) &&
                            line.EndsWith("]", StringComparison.Ordinal))
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
                                string foundSection = line.Substring(1, line.Length - 2);
                                if (0 < inputKeys.Count(x => x.Section.Equals(foundSection, StringComparison.OrdinalIgnoreCase)))
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
                                lineBuffer.Add(new Tuple<string, string>(line, rawLine));
                            else // Passthrough
                                w.WriteLine(rawLine);    
                        }

                        firstLine = false;
                        lastLine = line;
                    }
                }

                if (inputKeys.Count == 0)
                { // Success
                    IniHelper.FileReplaceEx(tempPath, file);
                    return true;
                }
                else
                { // Internal Error
                    return false;
                }
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }
        #endregion

        #region WriteRawLine
        public static bool WriteRawLine(string file, string section, string line, bool append = true)
        {
            return InternalWriteRawLine(file, new List<IniKey> { new IniKey(section, line) }, append);
        }

        public static bool WriteRawLine(string file, IniKey iniKey, bool append = true)
        {
            return InternalWriteRawLine(file, new List<IniKey> { iniKey }, append);
        }

        public static bool WriteRawLines(string file, IEnumerable<IniKey> iniKeys, bool append = true)
        {
            return InternalWriteRawLine(file, iniKeys.ToList(), append);
        }

        private static bool InternalWriteRawLine(string file, List<IniKey> iniKeys, bool append = true)
        {
            ReaderWriterLockSlim rwLock;
            if (lockDict.ContainsKey(file))
            {
                rwLock = lockDict[file];
            }
            else
            {
                rwLock = new ReaderWriterLockSlim();
                lockDict[file] = rwLock;
            }

            rwLock.EnterWriteLock();
            try
            {
                bool fileExist = File.Exists(file);

                // If file do not exists or blank, just create new file and insert keys.
                if (fileExist == false)
                {
                    using (StreamWriter writer = new StreamWriter(file, false, Encoding.UTF8))
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

                List<int> processedKeys = new List<int>(iniKeys.Count);
                string tempPath = Path.GetTempFileName();
                Encoding encoding = IniHelper.DetectTextEncoding(file);
                using (StreamReader reader = new StreamReader(file, encoding, true))
                using (StreamWriter writer = new StreamWriter(tempPath, false, encoding))
                {
                    string rawLine;
                    string line;
                    bool inTargetSection = false;
                    string currentSection = null;
                    List<string> processedSections = new List<string>(iniKeys.Count);

                    // Is Original File Empty?
                    if (reader.Peek() == -1)
                    {
                        reader.Close();

                        // Write all and exit
                        string beforeSection = string.Empty;
                        for (int i = 0; i < iniKeys.Count; i++)
                        {
                            if (beforeSection.Equals(iniKeys[i].Section, StringComparison.OrdinalIgnoreCase) == false)
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
                        IniHelper.FileReplaceEx(tempPath, file);
                        return true;
                    }

                    // Main Logic
                    while ((rawLine = reader.ReadLine()) != null)
                    { // Read text line by line
                        bool thisLineWritten = false;
                        line = rawLine.Trim(); // Remove whitespace

                        // Ignore comments. If you wrote all keys successfully, also skip.
                        if (iniKeys.Count == 0 ||
                            line.StartsWith("#", StringComparison.Ordinal) ||
                            line.StartsWith(";", StringComparison.Ordinal) ||
                            line.StartsWith("//", StringComparison.Ordinal))
                        {
                            thisLineWritten = true;
                            writer.WriteLine(rawLine);
                        }
                        else
                        {
                            // Check if encountered section head Ex) [Process]
                            if (line.StartsWith("[", StringComparison.Ordinal) &&
                                line.EndsWith("]", StringComparison.Ordinal))
                            {
                                string foundSection = line.Substring(1, line.Length - 2);

                                // Append Mode : Add to last line of section
                                if (append && inTargetSection)
                                { // End of targetSeciton and start of foundSection
                                    for (int i = 0; i < iniKeys.Count; i++)
                                    {
                                        if (processedKeys.Contains(i))
                                            continue;

                                        // Add to last line of foundSection
                                        if (currentSection.Equals(iniKeys[i].Section, StringComparison.OrdinalIgnoreCase))
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

                                    if (foundSection.Equals(iniKeys[i].Section, StringComparison.OrdinalIgnoreCase))
                                    {
                                        inTargetSection = true;
                                        currentSection = foundSection;
                                        processedSections.Add(currentSection);
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
                                        if (currentSection.Equals(iniKeys[i].Section, StringComparison.OrdinalIgnoreCase))
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

                                        if (currentSection.Equals(iniKeys[i].Section, StringComparison.OrdinalIgnoreCase))
                                        { // append key to section
                                            processedKeys.Add(i);
                                            thisLineWritten = true;

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
                            { // End of targetSeciton and start of foundSection
                                for (int i = 0; i < iniKeys.Count; i++)
                                {
                                    if (processedKeys.Contains(i))
                                        continue;

                                    // Add to last line of foundSection
                                    if (currentSection.Equals(iniKeys[i].Section, StringComparison.OrdinalIgnoreCase))
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
                            { // At this time, only unfound section remains in iniKeys
                                if (processedKeys.Contains(i))
                                    continue;

                                if (processedSections.Any(s => s.Equals(iniKeys[i].Section, StringComparison.OrdinalIgnoreCase)) == false)
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
                    IniHelper.FileReplaceEx(tempPath, file);
                    return true;
                }
                else
                {
                    return false;
                }
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }
        #endregion

        #region DeleteKey
        public static bool DeleteKey(string file, IniKey iniKey)
        {
            return InternalDeleteKeys(file, new IniKey[] { iniKey })[0];
        }
        public static bool DeleteKey(string file, string section, string key)
        {
            return InternalDeleteKeys(file, new IniKey[] { new IniKey(section, key) })[0];
        }
        public static bool[] DeleteKeys(string file, IEnumerable<IniKey> iniKeys)
        {
            return InternalDeleteKeys(file, iniKeys);
        }
        private static bool[] InternalDeleteKeys(string file, IEnumerable<IniKey> iniKeys)
        {
            IniKey[] keys = iniKeys.ToArray();
            bool[] processed = new bool[keys.Length];
            for (int i = 0; i < processed.Length; i++)
                processed[i] = false;

            ReaderWriterLockSlim rwLock;
            if (lockDict.ContainsKey(file))
            {
                rwLock = lockDict[file];
            }
            else
            {
                rwLock = new ReaderWriterLockSlim();
                lockDict[file] = rwLock;
            }

            rwLock.EnterWriteLock();
            try
            {
                if (File.Exists(file) == false)
                    return processed; // All False

                string tempPath = Path.GetTempFileName();
                Encoding encoding = IniHelper.DetectTextEncoding(file);
                using (StreamReader reader = new StreamReader(file, encoding, true))
                using (StreamWriter writer = new StreamWriter(tempPath, false, encoding))
                {
                    if (reader.Peek() == -1)
                    {
                        reader.Close();
                        return processed; // All False
                    }

                    string rawLine = string.Empty;
                    string line = string.Empty;
                    bool inTargetSection = false;
                    string currentSection = null;

                    while ((rawLine = reader.ReadLine()) != null)
                    { // Read text line by linev
                        bool thisLineProcessed = false;
                        line = rawLine.Trim(); // Remove whitespace

                        // Ignore comments. If you deleted all keys successfully, also skip.
                        if (processed.Where(x => x == false).Count() == 0
                            || line.StartsWith("#", StringComparison.Ordinal)
                            || line.StartsWith(";", StringComparison.Ordinal)
                            || line.StartsWith("//", StringComparison.Ordinal))
                        {
                            thisLineProcessed = true;
                            writer.WriteLine(rawLine);
                            continue;
                        }

                        // Check if encountered section head Ex) [Process]
                        if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
                        {
                            string foundSection = line.Substring(1, line.Length - 2);

                            // Start of the section
                            inTargetSection = false;
                            // Only sections contained in iniKeys will be targeted
                            for (int i = 0; i < keys.Length; i++)
                            {
                                if (processed[i])
                                    continue;

                                if (foundSection.Equals(keys[i].Section, StringComparison.OrdinalIgnoreCase))
                                {
                                    inTargetSection = true;
                                    currentSection = foundSection;
                                    break; // for shorter O(n)
                                }
                            }
                            thisLineProcessed = true;
                            writer.WriteLine(rawLine);
                        }

                        // key=value
                        int idx = line.IndexOf('=');
                        if (idx != -1 && idx != 0) // Key exists
                        {
                            if (inTargetSection) // process here only if we are in target section
                            {
                                string keyOfLine = line.Substring(0, idx).Trim();
                                for (int i = 0; i < keys.Length; i++)
                                {
                                    if (processed[i])
                                        continue;

                                    if (currentSection.Equals(keys[i].Section, StringComparison.OrdinalIgnoreCase)
                                        && keyOfLine.Equals(keys[i].Key, StringComparison.OrdinalIgnoreCase))
                                    { // key exists, so do not write this line, which lead to 'deletion'
                                        thisLineProcessed = true;
                                        processed[i] = true;
                                    }
                                }
                            }
                        }

                        if (thisLineProcessed == false)
                            writer.WriteLine(rawLine);
                    }
                    reader.Close();
                    writer.Close();
                }

                if (0 < processed.Where(x => x).Count())
                    IniHelper.FileReplaceEx(tempPath, file);

                return processed;
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }
        #endregion

        #region ReadSection
        public static IniKey[] ReadSection(string file, IniKey iniKey)
        {
            return InternalReadSection(file, new string[] { iniKey.Section }).Select(x => x.Value).First();
        }
        public static IniKey[] ReadSection(string file, string section)
        {
            return InternalReadSection(file, new string[] { section }).Select(x => x.Value).First();
        }
        public static Dictionary<string, IniKey[]> ReadSections(string file, IEnumerable<IniKey> iniKeys)
        {
            return InternalReadSection(file, iniKeys.Select(x => x.Section));
        }
        public static Dictionary<string, IniKey[]> ReadSections(string file, IEnumerable<string> sections)
        {
            return InternalReadSection(file, sections);
        }
        private static Dictionary<string, IniKey[]> InternalReadSection(string file, IEnumerable<string> sections)
        {
            string[] sectionNames = sections.ToArray();
            Dictionary<string, List<IniKey>> secDict = new Dictionary<string, List<IniKey>>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < sectionNames.Length; i++)
                secDict[sectionNames[i]] = null;

            ReaderWriterLockSlim rwLock;
            if (lockDict.ContainsKey(file))
            {
                rwLock = lockDict[file];
            }
            else
            {
                rwLock = new ReaderWriterLockSlim();
                lockDict[file] = rwLock;
            }

            rwLock.EnterReadLock();
            try
            {
                Encoding encoding = IniHelper.DetectTextEncoding(file);
                using (StreamReader reader = new StreamReader(file, encoding, true))
                {
                    // int len = iniKeys.Count;
                    string line = string.Empty;
                    bool inTargetSection = false;
                    string currentSection = null;
                    List<IniKey> currentIniKeys = null;

                    while ((line = reader.ReadLine()) != null)
                    { // Read text line by line
                        line = line.Trim(); // Remove whitespace
                        if (line.StartsWith("#", StringComparison.Ordinal) ||
                            line.StartsWith(";", StringComparison.Ordinal) ||
                            line.StartsWith("//", StringComparison.Ordinal)) // Ignore comment
                            continue;

                        if (inTargetSection)
                        {
                            Debug.Assert(currentSection != null);

                            int idx = line.IndexOf('=');
                            if (idx != -1 && idx != 0) // there is key, and key name is not empty
                            {
                                string key = line.Substring(0, idx).Trim();
                                string value = line.Substring(idx + 1).Trim();
                                currentIniKeys.Add(new IniKey(currentSection, key, value));
                            }
                            else
                            {
                                // Search if current section ended
                                if (line.StartsWith("[", StringComparison.Ordinal) &&
                                    line.EndsWith("]", StringComparison.Ordinal))
                                {
                                    // Only sections contained in sectionNames will be targeted
                                    inTargetSection = false;
                                    currentSection = null;

                                    string foundSection = line.Substring(1, line.Length - 2);
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
                            if (line.StartsWith("[", StringComparison.Ordinal) &&
                                line.EndsWith("]", StringComparison.Ordinal))
                            {
                                // Only sections contained in iniKeys will be targeted
                                string foundSection = line.Substring(1, line.Length - 2);
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
            }
            finally
            {
                rwLock.ExitReadLock();
            }

            return secDict.ToDictionary(x => x.Key, x => x.Value?.ToArray(), StringComparer.OrdinalIgnoreCase);
        }
        #endregion

        #region AddSection
        public static bool AddSection(string file, IniKey iniKey)
        {
            return InternalAddSection(file, new List<string> { iniKey.Section });
        }
        public static bool AddSection(string file, string section)
        {
            return InternalAddSection(file, new List<string> { section });
        }
        public static bool AddSections(string file, IEnumerable<IniKey> iniKeys)
        {
            return InternalAddSection(file, iniKeys.Select(x => x.Section).ToList());
        }
        public static bool AddSections(string file, IEnumerable<string> sections)
        {
            return InternalAddSection(file, sections);
        }
        /// <summary>
        /// Return true if success
        /// </summary>
        private static bool InternalAddSection(string file, IEnumerable<string> sections)
        {
            List<string> sectionList = sections.ToList();
            ReaderWriterLockSlim rwLock;
            if (lockDict.ContainsKey(file))
            {
                rwLock = lockDict[file];
            }
            else
            {
                rwLock = new ReaderWriterLockSlim();
                lockDict[file] = rwLock;
            }

            rwLock.EnterWriteLock();
            try
            {
                // Not exist -> Create and exit
                if (File.Exists(file) == false)
                {
                    using (StreamWriter writer = new StreamWriter(file, false, Encoding.UTF8))
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

                string tempPath = Path.GetTempFileName();
                Encoding encoding = IniHelper.DetectTextEncoding(file);
                using (StreamReader reader = new StreamReader(file, encoding, true))
                using (StreamWriter writer = new StreamWriter(tempPath, false, encoding))
                {
                    // Is Original File Empty?
                    if (reader.Peek() == -1)
                    {
                        reader.Close();

                        // Write all and exit
                        for (int i = 0; i < sectionList.Count; i++)
                        {
                            if (0 < i)
                                writer.WriteLine();
                            writer.WriteLine($"[{sectionList[i]}]");
                        }

                        writer.Close();
                        IniHelper.FileReplaceEx(tempPath, file);
                        return true;
                    }

                    string rawLine;
                    List<string> processedSections = new List<string>(sectionList.Count);

                    while ((rawLine = reader.ReadLine()) != null)
                    { // Read text line by line
                        bool thisLineProcessed = false;
                        string line = rawLine.Trim();

                        // Check if encountered section head Ex) [Process]
                        if (line.StartsWith("[", StringComparison.Ordinal) &&
                            line.EndsWith("]", StringComparison.Ordinal))
                        {
                            string foundSection = line.Substring(1, line.Length - 2);

                            // Start of the section;
                            // Only sections contained in iniKeys will be targeted
                            for (int i = 0; i < sectionList.Count; i++)
                            {
                                if (foundSection.Equals(sectionList[i], StringComparison.OrdinalIgnoreCase))
                                { // Delete this section!
                                    processedSections.Add(foundSection);
                                    sectionList.RemoveAt(i);
                                    break; // for shorter O(n)
                                }
                            }
                            thisLineProcessed = true;
                            writer.WriteLine(rawLine);
                        }

                        if (thisLineProcessed == false)
                            writer.WriteLine(rawLine);

                        // End of file
                        if (reader.Peek() == -1)
                        { // If there are sections not added, add it now
                            List<int> processedIdxs = new List<int>(sectionList.Count);
                            for (int i = 0; i < sectionList.Count; i++)
                            { // At this time, only unfound section remains in iniKeys
                                if (processedSections.Any(s => s.Equals(sectionList[i], StringComparison.OrdinalIgnoreCase)) == false)
                                {
                                    processedSections.Add(sectionList[i]);
                                    writer.WriteLine($"\r\n[{sectionList[i]}]");
                                }
                                processedIdxs.Add(i);
                            }
                            foreach (int i in processedIdxs.OrderByDescending(x => x))
                                sectionList.RemoveAt(i);
                        }
                    }
                    reader.Close();
                    writer.Close();
                }

                if (sectionList.Count == 0)
                {
                    IniHelper.FileReplaceEx(tempPath, file);
                    return true;
                }
                else
                {
                    return false;
                }
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }
        #endregion

        #region DeleteSection
        public static bool DeleteSection(string file, IniKey iniKey)
        {
            return InternalDeleteSection(file, new string[] { iniKey.Section });
        }
        public static bool DeleteSection(string file, string section)
        {
            return InternalDeleteSection(file, new string[] { section });
        }
        public static bool DeleteSections(string file, IEnumerable<IniKey> iniKeys)
        {
            return InternalDeleteSection(file, iniKeys.Select(x => x.Section));
        }
        public static bool DeleteSections(string file, IEnumerable<string> sections)
        {
            return InternalDeleteSection(file, sections);
        }
        /// <summary>
        /// Return true if success
        /// </summary>
        /// <param name="file"></param>
        /// <param name="sections"></param>
        /// <returns></returns>
        private static bool InternalDeleteSection(string file, IEnumerable<string> sections)
        {
            List<string> sectionList = sections.ToList();

            ReaderWriterLockSlim rwLock;
            if (lockDict.ContainsKey(file))
            {
                rwLock = lockDict[file];
            }
            else
            {
                rwLock = new ReaderWriterLockSlim();
                lockDict[file] = rwLock;
            }

            rwLock.EnterWriteLock();
            try
            {
                if (File.Exists(file) == false)
                    return false;

                string tempPath = Path.GetTempFileName();
                Encoding encoding = IniHelper.DetectTextEncoding(file);
                using (StreamReader r = new StreamReader(file, encoding, true))
                using (StreamWriter w = new StreamWriter(tempPath, false, encoding))
                {
                    if (r.Peek() == -1)
                    {
                        r.Close();
                        return false;
                    }

                    string rawLine;
                    bool ignoreCurrentSection = false;

                    // Main Logic
                    while ((rawLine = r.ReadLine()) != null)
                    { // Read text line by line
                        string line = rawLine.Trim();

                        // Check if encountered section head Ex) [Process]
                        if (line.StartsWith("[", StringComparison.Ordinal) &&
                            line.EndsWith("]", StringComparison.Ordinal))
                        {
                            string foundSection = line.Substring(1, line.Length - 2);
                            ignoreCurrentSection = false;

                            // Start of the section;
                            // Only sections contained in iniKeys will be targeted
                            for (int i = 0; i < sectionList.Count; i++)
                            {
                                if (foundSection.Equals(sectionList[i], StringComparison.OrdinalIgnoreCase))
                                { // Delete this section!
                                    ignoreCurrentSection = true;
                                    sectionList.RemoveAt(i);
                                    break; // for shorter O(n)
                                }
                            }
                        }

                        if (!ignoreCurrentSection)
                            w.WriteLine(rawLine);
                    }
                }

                if (sectionList.Count == 0)
                {
                    IniHelper.FileReplaceEx(tempPath, file);
                    return true;
                }
                else
                {
                    return false;
                }
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }
        #endregion

        #region Merge
        public static bool Merge(string srcFile, string destFile)
        {
            IniFile srcIniFile = new IniFile(srcFile);

            bool result = true;
            // kvSec => Key: Section Value:<Key-Value>
            foreach (var kvSec in srcIniFile.Sections)
            {
                List<IniKey> keys = new List<IniKey>();

                // kvKey => Key:Key Value:Value
                foreach (var kvKey in kvSec.Value)
                    keys.Add(new IniKey(kvSec.Key, kvKey.Key, kvKey.Value));

                result = result & InternalSetKeys(destFile, keys);
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

                    result = result & InternalSetKeys(destFile, keys);
                }
            }

            return result;
        }
        #endregion

        #region Utility
        private static bool IsLineComment(string line)
        {
            return line.StartsWith("#", StringComparison.Ordinal) ||
                   line.StartsWith(";", StringComparison.Ordinal) ||
                   line.StartsWith("//", StringComparison.Ordinal);
        }

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


        /// <summary>
        /// Parse section to dictionary.
        /// </summary>
        public static Dictionary<string, string> ParseIniSectionToDict(string file, string section)
        {
            List<string> lines = ParseIniSection(file, section);
            return lines == null ? null : ParseIniLinesIniStyle(lines);
        }

        public static List<string> ParseIniSection(string file, string section)
        {
            List<string> lines = new List<string>();

            Encoding encoding = IniHelper.DetectTextEncoding(file);
            using (StreamReader reader = new StreamReader(file, encoding, true))
            {
                string line;
                bool appendState = false;
                while ((line = reader.ReadLine()) != null)
                { // Read text line by line
                    line = line.Trim();

                    // Ignore comment
                    if (line.StartsWith("#", StringComparison.Ordinal) ||
                        line.StartsWith(";", StringComparison.Ordinal) ||
                        line.StartsWith("//", StringComparison.Ordinal)) 
                        continue;

                    if (line.StartsWith("[", StringComparison.Ordinal) &&
                        line.EndsWith("]", StringComparison.Ordinal))
                    { // Start of section
                        if (appendState)
                            break;

                        string foundSection = line.Substring(1, line.Length - 2);
                        if (section.Equals(foundSection, StringComparison.OrdinalIgnoreCase))
                            appendState = true;
                    }
                    else
                    {
                        int idx;
                        if ((idx = line.IndexOf('=')) != -1 && idx != 0)
                        { // valid ini key, and not empty
                            if (appendState)
                                lines.Add(line);
                        }
                    }
                }

                if (!appendState) // Section not found
                    return null;
            }
            return lines;
        }

        public static List<string> ParseRawSection(string file, string section)
        {
            List<string> lines = new List<string>();

            Encoding encoding = IniHelper.DetectTextEncoding(file);
            using (StreamReader reader = new StreamReader(file, encoding, true))
            {
                string line;
                bool appendState = false;
                while ((line = reader.ReadLine()) != null)
                { // Read text line by line
                    line = line.Trim();

                    // Ignore comment
                    if (line.StartsWith("#", StringComparison.Ordinal) ||
                        line.StartsWith(";", StringComparison.Ordinal) ||
                        line.StartsWith("//", StringComparison.Ordinal))
                        continue;

                    if (line.StartsWith("[", StringComparison.Ordinal) &&
                        line.EndsWith("]", StringComparison.Ordinal))
                    { // Start of section
                        if (appendState)
                            break;

                        string foundSection = line.Substring(1, line.Length - 2);
                        if (section.Equals(foundSection, StringComparison.OrdinalIgnoreCase))
                            appendState = true;
                    }
                    else if (appendState)
                    {
                        if (line.Length != 0)
                            lines.Add(line);
                    }
                }

                if (!appendState) // Section not found
                    return null;
            }
            return lines;
        }

        /// <summary>
        /// Parse section to dictionary array.
        /// </summary>
        public static Dictionary<string, string>[] ParseSectionsToDicts(string file, string[] sections)
        {
            List<string>[] lines = ParseIniSections(file, sections);
            Dictionary<string, string>[] dicts = new Dictionary<string, string>[lines.Length];
            for (int i = 0; i < lines.Length; i++)
                dicts[i] = ParseIniLinesIniStyle(lines[i]);
            return dicts;
        }
        /// <summary>
        /// Parse sections to string 2D array.
        /// </summary>
        public static List<string>[] ParseIniSections(string file, IEnumerable<string> sectionList)
        {
            string[] sections = sectionList.Distinct().ToArray(); // Remove duplicate

            List<string>[] lines = new List<string>[sections.Length];
            for (int i = 0; i < sections.Length; i++)
                lines[i] = new List<string>();

            Encoding encoding = IniHelper.DetectTextEncoding(file);
            using (StreamReader reader = new StreamReader(file, encoding, true))
            {
                string line;
                int currentSection = -1; // -1 == empty, 0, 1, ... == index value of sections array
                List<int> processedSectionIdxs = new List<int>();

                while ((line = reader.ReadLine()) != null)
                { // Read text line by line
                    if (sections.Length < processedSectionIdxs.Count)
                        break;

                    line = line.Trim();
                    if (line.StartsWith("[", StringComparison.Ordinal) &&
                        line.EndsWith("]", StringComparison.Ordinal))
                    { // Start of section
                        bool isSectionFound = false;
                        string foundSection = line.Substring(1, line.Length - 2);
                        for (int i = 0; i < sections.Length; i++)
                        {
                            if (processedSectionIdxs.Contains(i))
                                continue;

                            if (foundSection.Equals(sections[i], StringComparison.Ordinal))
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
                                lines[currentSection].Add(line);
                        }
                    }
                }

                //if (sections.Length != processedSectionIdxs.Count) // Section not found
                //    return null;

                reader.Close();
            }

            return lines;
        }

        public static Dictionary<string, Dictionary<string, string>> ParseToDict(string srcFile)
        {
            ReaderWriterLockSlim rwLock;

            if (lockDict.ContainsKey(srcFile))
            {
                rwLock = lockDict[srcFile];
            }
            else
            {
                rwLock = new ReaderWriterLockSlim();
                lockDict[srcFile] = rwLock;
            }

            Dictionary<string, Dictionary<string, string>> dict = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            rwLock.EnterReadLock();
            try
            {
                if (!File.Exists(srcFile))
                    return dict; // Return Empty dict if srcFile does not exist

                Encoding encoding = IniHelper.DetectTextEncoding(srcFile);
                using (StreamReader reader = new StreamReader(srcFile, encoding))
                {
                    // Is Original File Empty?
                    if (reader.Peek() == -1)
                    {
                        reader.Close();

                        // Return Empty Dict
                        return dict;
                    }

                    string line;
                    string section = null;

                    while ((line = reader.ReadLine()) != null)
                    { // Read text line by line
                        line = line.Trim(); // Remove whitespace
                        if (line.StartsWith("#", StringComparison.Ordinal) ||
                            line.StartsWith(";", StringComparison.Ordinal) ||
                            line.StartsWith("//", StringComparison.Ordinal)) // Ignore comment
                            continue;

                        // Check if encountered section head Ex) [Process]
                        if (line.StartsWith("[", StringComparison.Ordinal) &&
                            line.EndsWith("]", StringComparison.Ordinal))
                        {
                            section = line.Substring(1, line.Length - 2);
                            dict[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            continue;
                        }

                        // Read Keys
                        if (section != null)
                        {
                            int idx = line.IndexOf('=');
                            if (idx != -1 && idx != 0) // there is key, and key name is not empty
                            {
                                string key = line.Substring(0, idx);
                                string value = line.Substring(idx + 1);

                                dict[section][key] = value;
                            }
                        }
                    }
                }
            }
            finally
            {
                rwLock.ExitReadLock();
            }

            return dict;
        }

        /// <summary>
        /// Get name of sections from INI file
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static List<string> GetSectionNames(string file)
        {
            List<string> sections = new List<string>();

            Encoding encoding = IniHelper.DetectTextEncoding(file);
            using (StreamReader reader = new StreamReader(file, encoding, true))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                { // Read text line by line
                    line = line.Trim();
                    if (line.StartsWith("[", StringComparison.Ordinal) &&
                        line.EndsWith("]", StringComparison.Ordinal)) // Count sections
                        sections.Add(line.Substring(1, line.Length - 2));
                }

                reader.Close();
            }

            return sections;
        }

        /// <summary>
        /// Check if INI file has specified section
        /// </summary>
        /// <param name="file"></param>
        /// <param name="section"></param>
        /// <returns></returns>
        public static bool SectionExists(string file, string section)
        {
            bool result = false;

            Encoding encoding = IniHelper.DetectTextEncoding(file);
            using (StreamReader reader = new StreamReader(file, encoding, true))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                { // Read text line by line
                    line = line.Trim();
                    if (line.StartsWith("[", StringComparison.Ordinal) &&
                        line.EndsWith("]", StringComparison.Ordinal))
                    {
                        if (section.Equals(line.Substring(1, line.Length - 2), StringComparison.OrdinalIgnoreCase))
                        {
                            result = true;
                            break;
                        }
                    }
                }

                reader.Close();
            }

            return result;
        }


        /// <summary>
        /// Return true if failed
        /// </summary>
        /// <param name="rawLine"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool GetKeyValueFromLine(string rawLine, out string key, out string value)
        {
            int idx = rawLine.IndexOf('=');
            if (idx != -1) // there is key
            {
                key = rawLine.Substring(0, idx);
                value = rawLine.Substring(idx + 1);
                return false;
            }
            else // No Ini Format!
            {
                key = string.Empty;
                value = string.Empty;
                return true;
            }
        }

        /// <summary>
        /// Return true if failed
        /// </summary>
        /// <param name="rawLines"></param>
        /// <param name="keys"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        public static bool GetKeyValueFromLines(List<string> rawLines, out List<string> keys, out List<string> values)
        {
            keys = new List<string>();
            values = new List<string>();
            foreach (string rawLine in rawLines)
            {
                if (GetKeyValueFromLine(rawLine, out string key, out string value))
                    return true;
                keys.Add(key);
                values.Add(value);
            }

            return false;
        }
        #endregion
    }
    #endregion

    #region IniFile
    public class IniFile
    {
        public string FilePath { get; set; }
        public Dictionary<string, Dictionary<string, string>> Sections { get; set; }

        public IniFile(string filePath)
        {
            FilePath = filePath;
            Sections = Ini.ParseToDict(filePath);
        }
    }
    #endregion
}
