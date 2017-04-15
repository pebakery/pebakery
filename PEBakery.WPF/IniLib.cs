/*
    Copyright (C) 2016-2017 Hajin Jang
    Licensed under MIT License.
 
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
using PEBakery.Helper;

namespace PEBakery.Lib
{
    using StringDictionary = Dictionary<string, string>;

    #region Exceptions
    /// <summary>
    /// When parsing ini file, specified key not found.
    /// </summary>
    public class IniKeyNotFoundException : Exception
    {
        public IniKeyNotFoundException() { }
        public IniKeyNotFoundException(string message) : base(message) { }
        public IniKeyNotFoundException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// When parsing ini file, specified section is not found.
    /// </summary>
    public class SectionNotFoundException : Exception
    {
        public SectionNotFoundException() { }
        public SectionNotFoundException(string message) : base(message) { }
        public SectionNotFoundException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// INI file is invalid
    /// </summary>
    public class InvalidIniFormatException : Exception
    {
        public InvalidIniFormatException() { }
        public InvalidIniFormatException(string message) : base(message) { }
        public InvalidIniFormatException(string message, Exception inner) : base(message, inner) { }
    }
    #endregion

    #region IniKey
    public struct IniKey
    {
        public string Section;
        public string Key;
        public string Value; // In GetKeys, this record is not used, set to null

        public IniKey(string section)
        {
            this.Section = section;
            this.Key = null;
            this.Value = null;
        }
        public IniKey(string section, string key)
        {
            this.Section = section;
            this.Key = key;
            this.Value = null;
        }
        public IniKey(string section, string key, string value)
        {
            this.Section = section;
            this.Key = key;
            this.Value = value;
        }
    }

    public class IniKeyComparer : IComparer
    {
        public int Compare(System.Object x, System.Object y)
        {
            string strX = ((IniKey)x).Section;
            string strY = ((IniKey)y).Section;
            return (new CaseInsensitiveComparer()).Compare(strX, strY);
        }
    }
    #endregion

    public static class Ini
    {
        #region GetKey
        /// <summary>
        /// Get key's value from ini file.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="section"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static string GetKey(string file, IniKey iniKey)
        {
            return InternalGetKey(file, iniKey.Section, iniKey.Key);
        }
        /// <summary>
        /// Get key's value from ini file.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="section"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static string GetKey(string file, string section, string key)
        {
            return InternalGetKey(file, section, key);
        }
        private static string InternalGetKey(string file, string section, string key)
        {
            const StringComparison stricmp = StringComparison.OrdinalIgnoreCase;
            StreamReader reader = new StreamReader(new FileStream(file, FileMode.Open, FileAccess.Read), FileHelper.DetectTextEncoding(file));
            string line = string.Empty;
            string value = null;
            bool inSection = false;

            // If file is blank
            if (reader.Peek() == -1)
            {
                reader.Close();
                throw new IniKeyNotFoundException(string.Concat("Unable to find key [", key, "], file is empty"));
            }

            while ((line = reader.ReadLine()) != null)
            { // Read text line by line
                line = line.Trim(); // Remove whitespace
                if (line.StartsWith("#", stricmp) || line.StartsWith(";", stricmp) || line.StartsWith("//", stricmp)) // Ignore comment
                    continue;

                if (inSection)
                {
                    int idx = line.IndexOf('=');
                    if (idx != -1 && idx != 0) // there is key, and key name is not empty
                    {
                        if (string.Equals(line.Substring(0, idx), key, stricmp))
                        {
                            value = line.Substring(idx + 1);
                            break;
                        }
                    }
                    else
                    {
                        // current section end
                        if (line.StartsWith("[", stricmp) && line.EndsWith("]", stricmp))
                            break;
                    }
                }
                else
                { // not in correct section
                    // Check if encountered section head Ex) [Process]
                    if (line.StartsWith("[", StringComparison.OrdinalIgnoreCase) && line.EndsWith("]", stricmp)
                        && string.Equals(section, line.Substring(1, line.Length - 2), stricmp))
                        inSection = true; // Found correct section
                }
            }
            reader.Close();
            if (value == null)
                throw new IniKeyNotFoundException(string.Concat("Unable to find key [", key, "]"));
            return value;
        }

        /// <summary>
        /// Get key's value from ini file.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="iniKeys"></param>
        /// <returns>
        /// Found values are stroed in returned IniKey.
        /// </returns>
        public static IniKey[] GetKeys(string file, IniKey[] iniKeys)
        {
            return InternalGetKeys(file, iniKeys);
        }
        private static IniKey[] InternalGetKeys(string file, IniKey[] iniKeys)
        {
            const StringComparison stricmp = StringComparison.OrdinalIgnoreCase;
            StreamReader reader = new StreamReader(new FileStream(file, FileMode.Open, FileAccess.Read), FileHelper.DetectTextEncoding(file));

            int len = iniKeys.Length;
            string line = string.Empty;
            bool inTargetSection = false;
            string currentSection = null;
            int foundKeyCount = 0;

            // If file is blank
            if (reader.Peek() == -1)
            {
                reader.Close();
                throw new IniKeyNotFoundException(string.Concat("Unable to find keys, file is empty"));
            }

            while ((line = reader.ReadLine()) != null)
            { // Read text line by line
                if (foundKeyCount == len)
                    break;

                line = line.Trim(); // Remove whitespace
                if (line.StartsWith("#", stricmp) || line.StartsWith(";", stricmp) || line.StartsWith("//", stricmp)) // Ignore comment
                    continue;

                if (inTargetSection)
                {
                    int idx = line.IndexOf('=');
                    if (idx != -1 && idx != 0) // there is key, and key name is not empty
                    {
                        for (int i = 0; i < len; i++)
                        {
                            // Only if <section, key> is same, copy value;
                            if (string.Equals(currentSection, iniKeys[i].Section, stricmp)
                                && string.Equals(line.Substring(0, idx), iniKeys[i].Key, stricmp))
                            {
                                iniKeys[foundKeyCount].Value = line.Substring(idx + 1);
                                foundKeyCount++;
                            }
                        }
                    }
                    else
                    {
                        // search if current section has end
                        if (line.StartsWith("[", stricmp) && line.EndsWith("]", stricmp))
                        {
                            // Only sections contained in iniKeys will be targeted
                            inTargetSection = false;
                            currentSection = null;
                            string foundSection = line.Substring(1, line.Length - 2);
                            for (int i = 0; i < len; i++)
                            {
                                if (string.Equals(iniKeys[i].Section, foundSection, stricmp))
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
                    if (line.StartsWith("[", stricmp) && line.EndsWith("]", stricmp))
                    {
                        // Only sections contained in iniKeys will be targeted
                        string foundSection = line.Substring(1, line.Length - 2);
                        for (int i = 0; i < len; i++)
                        {
                            if (string.Equals(iniKeys[i].Section, foundSection, stricmp))
                            {
                                inTargetSection = true;
                                currentSection = foundSection;
                                break; // for shorter O(n)
                            }
                        }
                    }
                }
            }
            reader.Close();
            return iniKeys;
        }
        #endregion

        #region SetKey - Need Test
        /// <summary>
        /// Add key into ini file. Return true if success.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="section"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns>
        /// Found values are stroed in returned IniKey.
        /// </returns>
        public static bool SetKey(string file, string section, string key, string value)
        {
            return InternalSetKeys(file, new List<IniKey> { new IniKey(section, key, value) });
        }
        /// <summary>
        /// Add key into ini file. Return true if success.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="section"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns>
        /// Found values are stroed in returned IniKey.
        /// </returns>
        public static bool SetKey(string file, IniKey iniKey)
        {
            return InternalSetKeys(file, new List<IniKey> { iniKey });
        }
        /// <summary>
        /// Add key into ini file.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="section"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns>
        /// Found values are stroed in returned IniKey.
        /// </returns>
        public static bool SetKeys(string file, IniKey[] iniKeys)
        {
            return InternalSetKeys(file, iniKeys.ToList());
        }
        public static bool SetKeys(string file, List<IniKey> iniKeys)
        {
            return InternalSetKeys(file, iniKeys);
        }
        private static bool InternalSetKeys(string file, List<IniKey> iniKeys) 
        { 
            bool fileExist = File.Exists(file);

            Encoding encoding = null;
            StreamReader reader = null;
            StreamWriter writer = null;
            if (fileExist)
            {
                encoding = FileHelper.DetectTextEncoding(file);
                reader = new StreamReader(new FileStream(file, FileMode.Open, FileAccess.Read), encoding);

                if (reader.Peek() == -1)
                {
                    reader.Close();
                    fileExist = false;
                }
            }

            // If file do not exists or blank, just create new file and insert keys
            if (!fileExist)
            {
                writer = new StreamWriter(new FileStream(file, FileMode.Create, FileAccess.Write), Encoding.UTF8);
                // Array.Sort(iniKeys, new IniKeyComparer());
                string beforeSection = string.Empty;
                for (int i = 0; i < iniKeys.Count; i++)
                {
                    if (beforeSection.Equals(iniKeys[i].Section, StringComparison.OrdinalIgnoreCase) == false)
                    {
                        if (0 < i)
                            writer.WriteLine();
                        writer.WriteLine($"[{iniKeys[i].Section}]");
                    }
                    writer.WriteLine($"{iniKeys[i].Key}={iniKeys[i].Value}");
                    beforeSection = iniKeys[i].Section;
                }
                writer.Close();
                return true;
            }

            string temp = FileHelper.CreateTempFile();
            writer = new StreamWriter(new FileStream(temp, FileMode.Create, FileAccess.Write), encoding);
            string rawLine = string.Empty;
            string line = string.Empty;
            bool inTargetSection = false;
            string currentSection = null;
            List<string> processedSections = new List<string>();

            while ((rawLine = reader.ReadLine()) != null)
            { // Read text line by line
                bool thisLineWritten = false;
                line = rawLine.Trim(); // Remove whitespace

                // Ignore comments. If you wrote all keys successfully, also skip.
                if (iniKeys.Count == 0
                    ||line.StartsWith("#", StringComparison.Ordinal)
                    || line.StartsWith(";", StringComparison.Ordinal)
                    || line.StartsWith("//", StringComparison.Ordinal))
                {
                    thisLineWritten = true;
                    writer.WriteLine(rawLine);
                    continue;
                }

                // Check if encountered section head Ex) [Process]
                if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
                {
                    string foundSection = line.Substring(1, line.Length - 2);

                    if (inTargetSection)
                    { // End and start of the section
                        List<int> processedKeys = new List<int>();
                        for (int i = 0; i < iniKeys.Count; i++)
                        {
                            if (currentSection.Equals(iniKeys[i].Section, StringComparison.OrdinalIgnoreCase))
                            {
                                processedKeys.Add(i);
                                writer.WriteLine($"{iniKeys[i].Key}={iniKeys[i].Value}");
                            }
                        }
                        foreach (int i in processedKeys)
                            iniKeys.RemoveAt(i);
                    }
                    
                    // Start of the section
                    inTargetSection = false;
                    // Only sections contained in iniKeys will be targeted
                    for (int i = 0; i < iniKeys.Count; i++)
                    {
                        if (foundSection.Equals(iniKeys[i].Section, StringComparison.OrdinalIgnoreCase))
                        {
                            inTargetSection = true;
                            currentSection = foundSection;
                            processedSections.Add(currentSection);
                            break; // for shorter O(n)
                        }
                    }
                    thisLineWritten = true;
                    writer.WriteLine(rawLine);
                }

                // key=value
                int idx = line.IndexOf('=');
                if (idx != -1 && idx != 0)
                {
                    if (inTargetSection) // process here only if we are in target section
                    {
                        string keyOfLine = line.Substring(0, idx);
                        List<int> processedKeys = new List<int>();
                        for (int i = 0; i < iniKeys.Count; i++)
                        {
                            if (currentSection.Equals(iniKeys[i].Section, StringComparison.OrdinalIgnoreCase)
                                && keyOfLine.Equals(iniKeys[i].Key, StringComparison.OrdinalIgnoreCase))
                            { // key exists, so overwrite
                                processedKeys.Add(i);
                                thisLineWritten = true;
                                writer.WriteLine($"{keyOfLine}={iniKeys[i].Value}");
                            }
                        }
                        foreach (int i in processedKeys)
                            iniKeys.RemoveAt(i);

                        if (!thisLineWritten)
                        {
                            thisLineWritten = true;
                            writer.WriteLine(rawLine);
                        }
                    }
                    else
                    {
                        thisLineWritten = true;
                        writer.WriteLine(rawLine);
                    }
                }

                // Blank line
                if (line.Equals(string.Empty, StringComparison.Ordinal))
                {
                    if (inTargetSection)
                    {
                        List<int> processedKeys = new List<int>();
                        for (int i = 0; i < iniKeys.Count; i++)
                        {
                            if (currentSection.Equals(iniKeys[i].Section, StringComparison.OrdinalIgnoreCase))
                            { // append key to section
                                processedKeys.Add(i);
                                thisLineWritten = true;
                                writer.WriteLine($"{iniKeys[i].Key}={iniKeys[i].Value}");
                            }
                        }
                        foreach (int i in processedKeys)
                            iniKeys.RemoveAt(i);
                    }
                    thisLineWritten = true;
                    writer.WriteLine();
                }

                // End of file
                if (reader.Peek() == -1)
                {
                    List<int> processedKeys = new List<int>();
                    if (inTargetSection)
                    { // Currently in section? check currentSection
                        for (int i = 0; i < iniKeys.Count; i++)
                        {
                            if (currentSection.Equals(iniKeys[i].Section, StringComparison.OrdinalIgnoreCase))
                            {
                                processedKeys.Add(i);
                                if (thisLineWritten == false)
                                    writer.WriteLine(rawLine);
                                thisLineWritten = true;
                                writer.WriteLine($"{iniKeys[i].Key}={iniKeys[i].Value}");
                            }
                        }
                        foreach (int i in processedKeys)
                            iniKeys.RemoveAt(i);
                    }

                    // Not in section, so create new section
                    processedKeys.Clear();
                    for (int i = 0; i < iniKeys.Count; i++)
                    { // At this time, only unfound section remains in iniKeys
                        if (processedSections.Any(s => s.Equals(iniKeys[i].Section, StringComparison.OrdinalIgnoreCase)) == false)
                        {
                            processedSections.Add(iniKeys[i].Section);
                            writer.WriteLine($"{Environment.NewLine}[{iniKeys[i].Section}]");
                        }
                        processedKeys.Add(i);
                        writer.WriteLine($"{iniKeys[i].Key}={iniKeys[i].Value}");
                    }
                    foreach (int i in processedKeys)
                        iniKeys.RemoveAt(i);
                }

                if (!thisLineWritten)
                    writer.WriteLine(rawLine);
            }
            reader.Close();
            writer.Close();

            if (iniKeys.Count == 0)
            {
                FileHelper.FileReplaceEx(temp, file);
                return true;
            }
            else
                return false;
        }
        #endregion

        #region DeleteKey - need test
        public static bool DeleteKey(string file, IniKey iniKey)
        {
            return InternalDeleteKeys(file, new List<IniKey> { iniKey });
        }
        public static bool DeleteKey(string file, string section, string key)
        {
            return InternalDeleteKeys(file, new List<IniKey> { new IniKey(section, key) });
        }
        public static bool DeleteKeys(string file, IniKey[] iniKeys)
        {
            return InternalDeleteKeys(file, iniKeys.ToList());
        }
        public static bool DeleteKeys(string file, List<IniKey> iniKeys)
        {
            return InternalDeleteKeys(file, iniKeys);
        }
        private static bool InternalDeleteKeys(string file, List<IniKey> iniKeys)
        {
            if (File.Exists(file) == false)
                return false;

            string temp = FileHelper.CreateTempFile();
            Encoding encoding = FileHelper.DetectTextEncoding(file);
            using (StreamReader reader = new StreamReader(new FileStream(file, FileMode.Open, FileAccess.Read), encoding))
            using (StreamWriter writer = new StreamWriter(new FileStream(temp, FileMode.Create, FileAccess.Write), encoding))
            {
                if (reader.Peek() == -1)
                {
                    reader.Close();
                    return false;
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
                    if (iniKeys.Count == 0
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
                        for (int i = 0; i < iniKeys.Count; i++)
                        {
                            if (foundSection.Equals(iniKeys[i].Section, StringComparison.OrdinalIgnoreCase))
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
                            string keyOfLine = line.Substring(0, idx);
                            for (int i = 0; i < iniKeys.Count; i++)
                            {
                                if (currentSection.Equals(iniKeys[i].Section, StringComparison.OrdinalIgnoreCase)
                                    && keyOfLine.Equals(iniKeys[i].Key, StringComparison.OrdinalIgnoreCase))
                                { // key exists, so do not write this line, which lead to 'deletion'
                                    iniKeys.RemoveAt(i);
                                    thisLineProcessed = true;
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

            if (iniKeys.Count == 0)
            {
                FileHelper.FileReplaceEx(temp, file);
                return true;
            }
            else
                return false;

        }
        #endregion

        #region AddSection - need test
        public static bool AddSection(string file, IniKey iniKey)
        {
            return InternalAddSection(file, new List<string> { iniKey.Section });
        }
        public static bool AddSection(string file, string section)
        {
            return InternalAddSection(file, new List<string> { section });
        }
        public static bool AddSections(string file, IniKey[] iniKeys)
        {
            List<string> sections = new List<string>();
            foreach (IniKey key in iniKeys)
                sections.Add(key.Section);
            return InternalDeleteSection(file, sections);
        }
        public static bool AddSections(string file, List<IniKey> iniKeys)
        {
            List<string> sections = new List<string>();
            foreach (IniKey key in iniKeys)
                sections.Add(key.Section);
            return InternalAddSection(file, sections);
        }
        public static bool AddSections(string file, string[] sections)
        {
            return InternalAddSection(file, sections.ToList());
        }
        public static bool AddSections(string file, List<string> sections)
        {
            return InternalAddSection(file, sections);
        }
        private static bool InternalAddSection(string file, List<string> sections)
        {
            if (File.Exists(file) == false)
            {
                using (StreamWriter writer = new StreamWriter(new FileStream(file, FileMode.Create, FileAccess.Write), Encoding.UTF8))
                {
                    foreach (string section in sections)
                        writer.WriteLine($"{Environment.NewLine}[{section}]");
                    writer.Close();
                }
                return true;
            }

            string temp = FileHelper.CreateTempFile();
            Encoding encoding = FileHelper.DetectTextEncoding(file);
            using (StreamReader reader = new StreamReader(new FileStream(file, FileMode.Open, FileAccess.Read), encoding))
            using (StreamWriter writer = new StreamWriter(new FileStream(temp, FileMode.Create, FileAccess.Write), encoding))
            {
                if (reader.Peek() == -1)
                {
                    reader.Close();
                    return false;
                }

                string rawLine = string.Empty;
                string line = string.Empty;
                List<string> processedSections = new List<string>();

                while ((rawLine = reader.ReadLine()) != null)
                { // Read text line by line
                    bool thisLineProcessed = false;
                    line = rawLine.Trim(); // Remove whitespace

                    // Check if encountered section head Ex) [Process]
                    if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
                    {
                        string foundSection = line.Substring(1, line.Length - 2);

                        // Start of the section;
                        // Only sections contained in iniKeys will be targeted
                        for (int i = 0; i < sections.Count; i++)
                        {
                            if (foundSection.Equals(sections[i], StringComparison.OrdinalIgnoreCase))
                            { // Delete this section!
                                processedSections.Add(foundSection);
                                sections.RemoveAt(i);
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
                        List<int> processedIdxs = new List<int>();
                        for (int i = 0; i < sections.Count; i++)
                        { // At this time, only unfound section remains in iniKeys
                            if (processedSections.Any(s => s.Equals(sections[i], StringComparison.OrdinalIgnoreCase)) == false)
                            {
                                processedSections.Add(sections[i]);
                                writer.WriteLine($"{Environment.NewLine}[{sections[i]}]");
                            }
                            processedIdxs.Add(i);
                        }
                        foreach (int i in processedIdxs)
                            sections.RemoveAt(i);
                    }
                }
                reader.Close();
                writer.Close();
            }

            if (sections.Count == 0)
            {
                FileHelper.FileReplaceEx(temp, file);
                return true;
            }
            else
                return false;
        }
        #endregion

        #region DeleteSection - need test
        public static bool DeleteSection(string file, IniKey iniKey)
        {
            return InternalDeleteSection(file, new List<string> { iniKey.Section });
        }
        public static bool DeleteSection(string file, string section)
        {
            return InternalDeleteSection(file, new List<string> { section });
        }
        public static bool DeleteSections(string file, IniKey[] iniKeys)
        {
            List<string> sections = new List<string>();
            foreach (IniKey key in iniKeys)
                sections.Add(key.Section);
            return InternalDeleteSection(file, sections);
        }
        public static bool DeleteSections(string file, List<IniKey> iniKeys)
        {
            List<string> sections = new List<string>();
            foreach (IniKey key in iniKeys)
                sections.Add(key.Section);
            return InternalDeleteSection(file, sections);
        }
        public static bool DeleteSections(string file, string[] sections)
        {
            return InternalDeleteSection(file, sections.ToList());
        }
        public static bool DeleteSections(string file, List<string> sections)
        {
            return InternalDeleteSection(file, sections);
        }
        private static bool InternalDeleteSection(string file, List<string> sections)
        {
            if (File.Exists(file) == false)
                return false;

            string temp = FileHelper.CreateTempFile();
            Encoding encoding = FileHelper.DetectTextEncoding(file);
            using (StreamReader reader = new StreamReader(new FileStream(file, FileMode.Open, FileAccess.Read), encoding))
            using (StreamWriter writer = new StreamWriter(new FileStream(temp, FileMode.Create, FileAccess.Write), encoding))
            {
                if (reader.Peek() == -1)
                {
                    reader.Close();
                    return false;
                }

                string rawLine = string.Empty;
                string line = string.Empty;
                bool ignoreCurrentSection = false;

                while ((rawLine = reader.ReadLine()) != null)
                { // Read text line by line
                    bool thisLineProcessed = false;
                    line = rawLine.Trim(); // Remove whitespace

                    // Check if encountered section head Ex) [Process]
                    if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
                    {
                        string foundSection = line.Substring(1, line.Length - 2);
                        ignoreCurrentSection = false;

                        // Start of the section;
                        // Only sections contained in iniKeys will be targeted
                        for (int i = 0; i < sections.Count; i++)
                        {
                            if (foundSection.Equals(sections[i], StringComparison.OrdinalIgnoreCase))
                            { // Delete this section!
                                ignoreCurrentSection = true;
                                sections.RemoveAt(i);
                                break; // for shorter O(n)
                            }
                        }
                        thisLineProcessed = true;
                        writer.WriteLine(rawLine);
                    }

                    if (thisLineProcessed == false && ignoreCurrentSection == false)
                        writer.WriteLine(rawLine);
                }
                reader.Close();
                writer.Close();
            }

            if (sections.Count == 0)
            {
                FileHelper.FileReplaceEx(temp, file);
                return true;
            }
            else
                return false;
        }
        #endregion

        #region Utility
        /// <summary>
        /// Parse INI style strings into dictionary
        /// </summary>
        /// <param name="lines"></param>
        /// <returns></returns>
        public static StringDictionary ParseLinesIniStyle(string[] lines)
        {
            return InternalParseLinesRegex(@"^([^=]+)=(.*)$", lines.ToList());
        }
        /// <summary>
        /// Parse INI style strings into dictionary
        /// </summary>
        /// <param name="lines"></param>
        /// <returns></returns>
        public static StringDictionary ParseLinesIniStyle(List<string> lines)
        {
            return InternalParseLinesRegex(@"^([^=]+)=(.*)$", lines);
        }
        /// <summary>
        /// Parse PEBakery-Variable style strings into dictionary
        /// </summary>
        /// There in format of %VarKey%=VarValue
        /// <param name="lines"></param>
        /// <returns></returns>
        public static StringDictionary ParseLinesVarStyle(string[] lines)
        {
            return InternalParseLinesRegex(@"^%([^=]+)%=(.*)$", lines.ToList());
        }
        /// <summary>
        /// Parse PEBakery-Variable style strings into dictionary
        /// </summary>
        /// There in format of %VarKey%=VarValue
        /// <param name="lines"></param>
        /// <returns></returns>
        public static StringDictionary ParseLinesVarStyle(List<string> lines)
        {
            return InternalParseLinesRegex(@"^%([^=]+)%=(.*)$", lines);
        }
        /// <summary>
        /// Parse strings with regex.
        /// </summary>
        /// <param name="regex"></param>
        /// <param name="lines"></param>
        /// <returns></returns>
        private static StringDictionary InternalParseLinesRegex(string regex, List<string> lines)
        {
            StringDictionary dict = new StringDictionary(StringComparer.OrdinalIgnoreCase);
            foreach (string line in lines)
            {
                try
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
                catch (Exception e)
                {
                    Console.WriteLine(string.Concat(e.GetType(), ": ", StringHelper.RemoveLastNewLine(e.Message)));
                }
            }
            return dict;
        }


        /// <summary>
        /// Parse section to dictionary.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="section"></param>
        /// <returns></returns>
        public static StringDictionary ParseSectionToDict(string file, string section)
        {
            string[] lines = ParseSectionToStringArray(file, section);
            return ParseLinesIniStyle(lines);
        }
        /// <summary>
        /// Parse section to string array.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="section"></param>
        /// <returns></returns>
        public static string[] ParseSectionToStringArray(string file, string section)
        {
            return ParseSectionToStringList(file, section).ToArray();
        }

        public static List<string> ParseSectionToStringList(string file, string section)
        {
            StreamReader reader = new StreamReader(new FileStream(file, FileMode.Open, FileAccess.Read), FileHelper.DetectTextEncoding(file));

            // If file is blank
            if (reader.Peek() == -1)
            {
                reader.Close();
                throw new SectionNotFoundException(string.Concat("Unable to find section, file is empty"));
            }

            string line = string.Empty;
            bool appendState = false;
            int idx = 0;
            List<string> lines = new List<string>();
            while ((line = reader.ReadLine()) != null)
            { // Read text line by line
                line = line.Trim();
                if (line.StartsWith("[", StringComparison.OrdinalIgnoreCase) && line.EndsWith("]", StringComparison.OrdinalIgnoreCase))
                { // Start of section
                    if (appendState)
                        break;
                    else
                    {
                        string foundSection = line.Substring(1, line.Length - 2);
                        if (string.Equals(section, foundSection, StringComparison.OrdinalIgnoreCase))
                            appendState = true;
                    }
                }
                else if ((idx = line.IndexOf('=')) != -1)
                { // valid ini key
                    if (idx == 0) // key is empty
                        throw new InvalidIniFormatException($"[{line}] has invalid format");
                    if (appendState)
                        lines.Add(line);
                }

            }

            reader.Close();
            return lines;
        }

        /// <summary>
        /// Parse section to dictionary array.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="section"></param>
        /// <returns></returns>
        public static StringDictionary[] ParseSectionsToDicts(string file, string[] sections)
        {
            string[][] lines = ParseSectionsToStrings(file, sections);
            StringDictionary[] dicts = new StringDictionary[lines.Length];
            for (int i = 0; i < lines.Length; i++)
                 dicts[i] = ParseLinesIniStyle(lines[i]);
            return dicts;
        }
        /// <summary>
        /// Parse sections to string 2D array.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="section"></param>
        /// <returns></returns>
        public static string[][] ParseSectionsToStrings(string file, string[] sections)
        {
            const StringComparison stricmp = StringComparison.OrdinalIgnoreCase;
            StreamReader reader = new StreamReader(new FileStream(file, FileMode.Open, FileAccess.Read), FileHelper.DetectTextEncoding(file));
            sections = sections.Distinct().ToArray(); // Remove duplicate

            // If file is blank
            if (reader.Peek() == -1)
            {
                reader.Close();
                throw new SectionNotFoundException(string.Concat("Unable to find section, file is empty"));
            }

            int len = sections.Length;
            string line = string.Empty;
            int currentSection = -1; // -1 == empty, 0, 1, ... == index value of sections array
            int parsedCount = 0;
            int idx = 0;
            List<string>[] lines = new List<string>[len];
            for (int i = 0; i < len; i++)
                lines[i] = new List<string>();
            
            while ((line = reader.ReadLine()) != null)
            { // Read text line by line
                if (len < parsedCount)
                    break;

                line = line.Trim();
                if (line.StartsWith("[", stricmp) && line.EndsWith("]", stricmp))
                { // Start of section
                    bool isSectionFound = false;
                    string foundSection = line.Substring(1, line.Length - 2);
                    for (int i = 0; i < len; i++)
                    {
                        if (string.Equals(sections[i], foundSection, stricmp))
                        {
                            isSectionFound = true;
                            parsedCount++;
                            currentSection = i;
                            break;
                        }
                    }
                    if (!isSectionFound)
                        currentSection = -1;
                }
                else if ((idx = line.IndexOf('=')) != -1)
                { // valid ini key
                    if (idx == 0) // current section is target, and key is empty
                        throw new InvalidIniFormatException("[" + line + "] has invalid format");
                    if (currentSection != -1)
                        lines[currentSection].Add(line);
                }

            }

            reader.Close();
            string[][] strArrays = new string[len][];
            for (int i = 0; i < len; i++)
                strArrays[i] = lines[i].ToArray();
            return strArrays;
        }

        /// <summary>
        /// Get name of sections from INI file
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static string[] GetSectionNames(string file)
        {
            const StringComparison stricmp = StringComparison.OrdinalIgnoreCase;
            StreamReader reader = new StreamReader(new FileStream(file, FileMode.Open, FileAccess.Read), FileHelper.DetectTextEncoding(file));

            // If file is blank
            if (reader.Peek() == -1)
            {
                reader.Close();
                return new string[0]; // No section, empty file
            }

            string line = string.Empty;
            List<string> sections = new List<string>();

            while ((line = reader.ReadLine()) != null)
            { // Read text line by line
                line = line.Trim();
                if (line.StartsWith("[", stricmp) && line.EndsWith("]", stricmp)) // Count sections
                    sections.Add(line.Substring(1, line.Length - 2));
            }

            reader.Close();
            return sections.ToArray();
        }

        /// <summary>
        /// Check if INI file has specified section
        /// </summary>
        /// <param name="file"></param>
        /// <param name="section"></param>
        /// <returns></returns>
        public static bool CheckSectionExist(string file, string section)
        {
            const StringComparison stricmp = StringComparison.OrdinalIgnoreCase;
            StreamReader reader = new StreamReader(new FileStream(file, FileMode.Open, FileAccess.Read), FileHelper.DetectTextEncoding(file));

            // If file is blank
            if (reader.Peek() == -1)
            {
                reader.Close();
                return false; // No section, empty file
            }

            string line;
            bool result = false;
            while ((line = reader.ReadLine()) != null)
            { // Read text line by line
                line = line.Trim();
                if (line.StartsWith("[", stricmp) && line.EndsWith("]", stricmp)) // Count sections
                {
                    if (string.Equals(line.Substring(1, line.Length - 2), section, stricmp))
                    {
                        result = true;
                        break;
                    }
                }
            }

            reader.Close();
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
            for (int i = 0; i < rawLines.Count; i++)
            {
                if (GetKeyValueFromLine(rawLines[i], out string key, out string value))
                    return true;
                keys.Add(key);
                values.Add(value);
            }

            return false;
        }
        #endregion
    }
}
