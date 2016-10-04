using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace BakeryEngine
{
    using System.Collections;
    using System.Text.RegularExpressions;
    using StringDictionary = Dictionary<string, string>;

    /// <summary>
    /// When parsing ini file, specified key not found.
    /// </summary>
    public class KeyNotFoundException : Exception
    {
        public KeyNotFoundException() { }
        public KeyNotFoundException(string message) : base(message) { }
        public KeyNotFoundException(string message, Exception inner) : base(message, inner) { }
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

    public struct IniKey
    {
        public string section;
        public string key;
        public string value; // In GetKeys, this record is not used, set to null
        public IniKey(string section, string key)
        {
            this.section = section;
            this.key = key;
            this.value = null;
        }
        public IniKey(string section, string key, string value)
        {
            this.section = section;
            this.key = key;
            this.value = value;
        }
    }
    public class IniKeyComparer : IComparer
    {
        public int Compare(Object x, Object y)
        {
            string strX = ((IniKey)x).section;
            string strY = ((IniKey)y).section;
            return (new CaseInsensitiveComparer()).Compare(strX, strY);
        }
    }


    public static class IniFile
    {
        // TODO Start : The codes below are too nasty. Needs refactoring.
        
        /// <summary>
        /// Get key's value from ini file.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="section"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static string GetKey(string file, IniKey iniKey)
        {
            return InternalGetKey(file, iniKey.section, iniKey.key);
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
            StreamReader sr = new StreamReader(new FileStream(file, FileMode.Open, FileAccess.Read), Helper.DetectTextEncoding(file));
            string line = string.Empty;
            string value = null;
            bool inSection = false;

            // If file is blank
            if (sr.Peek() == -1)
            {
                sr.Close();
                throw new KeyNotFoundException(string.Concat("Unable to find key [", key, "], file is empty"));
            }

            while ((line = sr.ReadLine().Trim()) != null)
            { // Read text line by line
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
            sr.Close();
            if (value == null)
                throw new KeyNotFoundException(string.Concat("Unable to find key [", key, "]"));
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
            StreamReader sr = new StreamReader(new FileStream(file, FileMode.Open, FileAccess.Read), Helper.DetectTextEncoding(file));

            int len = iniKeys.Length;
            string line = string.Empty;
            bool inTargetSection = false;
            string currentSection = null;
            int foundKeyCount = 0;

            // If file is blank
            if (sr.Peek() == -1)
            {
                sr.Close();
                throw new KeyNotFoundException(string.Concat("Unable to find keys, file is empty"));
            }

            while ((line = sr.ReadLine()) != null)
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
                            if (string.Equals(currentSection, iniKeys[i].section, stricmp)
                                && string.Equals(line.Substring(0, idx), iniKeys[i].key, stricmp))
                            {
                                iniKeys[foundKeyCount].value = line.Substring(idx + 1);
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
                                if (string.Equals(iniKeys[i].section, foundSection, stricmp))
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
                            if (string.Equals(iniKeys[i].section, foundSection, stricmp))
                            {
                                inTargetSection = true;
                                currentSection = foundSection;
                                break; // for shorter O(n)
                            }
                        }
                    }
                }
            }
            sr.Close();
            return iniKeys;
        }

        // TODO End : Need-refactor block end


        // Refactored
        /// <summary>
        /// Add key into ini file. Return true if success.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="section"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public static bool SetKey(string file, string section, string key, string value)
        {
            return InternalSetKeys(file, new IniKey[] { new IniKey(section, key, value) });
        }
        /// <summary>
        /// Add key into ini file. Return true if success.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="section"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public static bool SetKey(string file, IniKey iniKey)
        {
            return InternalSetKeys(file, new IniKey[] { iniKey });
        }
        /// <summary>
        /// Add key into ini file.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="section"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// /// <returns>
        /// Found values are stroed in returned IniKey.
        /// </returns>
        public static bool SetKeys(string file, IniKey[] iniKeys)
        {
            return InternalSetKeys(file, iniKeys);
        }
        private static bool InternalSetKeys(string file, IniKey[] iniKeys) 
        { 
            const StringComparison stricmp = StringComparison.OrdinalIgnoreCase;
            bool fileExist = File.Exists(file);

            int len = iniKeys.Length;
            Encoding encoding = null;
            StreamReader reader = null;
            StreamWriter writer = null;
            if (fileExist)
            {
                encoding = Helper.DetectTextEncoding(file);
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
                Array.Sort(iniKeys, new IniKeyComparer());
                string beforeSection = string.Empty;
                for (int i = 0; i < len; i++)
                {
                    if (!string.Equals(beforeSection, iniKeys[i].section, stricmp))
                    {
                        if (0 < i)
                            writer.WriteLine();
                        writer.WriteLine(string.Concat("[", iniKeys[i].section, "]"));
                    }
                    writer.WriteLine(string.Concat(iniKeys[i].key, "=", iniKeys[i].value));
                    beforeSection = iniKeys[i].section;
                }
                writer.Close();
                return true;
            }

            string temp = Helper.CreateTempFile();
            writer = new StreamWriter(new FileStream(temp, FileMode.Create, FileAccess.Write), encoding);
            string rawLine = string.Empty;
            string line = string.Empty;
            bool inTargetSection = false;
            string currentSection = null;
            List<string> processedSection = new List<string>();
            int wroteKeyCount = 0;
            bool[] wroteKey = new bool[len];
            for (int i = 0; i < len; i++)
                wroteKey[i] = false;

            while ((rawLine = reader.ReadLine()) != null)
            { // Read text line by line
                bool thisLineWritten = false;
                line = rawLine.Trim(); // Remove whitespace

                // Ignore comments. If you wrote all keys successfully, also skip.
                if (len == wroteKeyCount || line.StartsWith("#", stricmp) || line.StartsWith(";", stricmp) || line.StartsWith("//", stricmp))
                {
                    thisLineWritten = true;
                    writer.WriteLine(rawLine);
                    continue;
                }

                // Check if encountered section head Ex) [Process]
                if (line.StartsWith("[", stricmp) && line.EndsWith("]", stricmp))
                {
                    string foundSection = line.Substring(1, line.Length - 2);

                    if (inTargetSection)
                    { // End and start of the section
                        for (int i = 0; i < len; i++)
                        {
                            if (!wroteKey[i] && string.Equals(currentSection, iniKeys[i].section, stricmp))
                            {
                                wroteKey[i] = true;
                                wroteKeyCount++;
                                writer.WriteLine(string.Concat(iniKeys[i].key, "=", iniKeys[i].value));
                            }
                        }
                    }
                    
                    // Start of the section
                    inTargetSection = false;
                    // Only sections contained in iniKeys will be targeted
                    for (int i = 0; i < len; i++)
                    {
                        if (!wroteKey[i] && string.Equals(iniKeys[i].section, foundSection, stricmp))
                        {
                            inTargetSection = true;
                            currentSection = foundSection;
                            processedSection.Add(currentSection);
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
                        for (int i = 0; i < len; i++)
                        {
                            if (!wroteKey[i] && string.Equals(currentSection, iniKeys[i].section, stricmp) && string.Equals(keyOfLine, iniKeys[i].key, stricmp))
                            { // key exists, so overwrite
                                wroteKey[i] = true;
                                wroteKeyCount++;
                                thisLineWritten = true;
                                writer.WriteLine(string.Concat(keyOfLine, "=", iniKeys[i].value));
                            }
                        }
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
                if (string.Equals(line, string.Empty, stricmp))
                {
                    if (inTargetSection)
                    {
                        for (int i = 0; i < len; i++)
                        {
                            if (!wroteKey[i] && string.Equals(currentSection, iniKeys[i].section, stricmp))
                            { // append key to section
                                wroteKey[i] = true;
                                wroteKeyCount++;
                                thisLineWritten = true;
                                writer.WriteLine(string.Concat(iniKeys[i].key, "=", iniKeys[i].value));
                            }
                        }
                    }
                    thisLineWritten = true;
                    writer.WriteLine();
                }

                // End of file
                if (reader.Peek() == -1)
                {
                    if (inTargetSection)
                    { // Currently in section? check currentSection
                        for (int i = 0; i < len; i++)
                        {
                            if (!wroteKey[i] && string.Equals(currentSection, iniKeys[i].section, stricmp))
                            {                            
                                wroteKey[i] = true;
                                wroteKeyCount++;
                                if (!thisLineWritten)
                                    writer.WriteLine(rawLine);
                                thisLineWritten = true;
                                writer.WriteLine(string.Concat(iniKeys[i].key, "=", iniKeys[i].value));
                            }
                        }
                    }

                    // Not in section, so create new section
                    for (int i = 0; i < len; i++)
                    { // At this time, only unfound section remains in wroteKey[i] == false
                        if (!wroteKey[i])
                        {
                            wroteKey[i] = true;
                            wroteKeyCount++;
                            if (!processedSection.Contains(iniKeys[i].section))
                            {
                                processedSection.Add(iniKeys[i].section);
                                writer.WriteLine(string.Concat(Environment.NewLine, "[", iniKeys[i].section, "]"));
                            }
                            writer.WriteLine(string.Concat(iniKeys[i].key, "=", iniKeys[i].value));
                            inTargetSection = true;
                            currentSection = iniKeys[i].section;
                            thisLineWritten = true;
                        }
                    }
                }

                if (!thisLineWritten)
                    writer.WriteLine(rawLine);
            }
            reader.Close();
            writer.Close();

            if (wroteKeyCount == len)
            {
                Helper.FileReplaceEx(temp, file);
                return true;
            }
            else
                return false;
        }


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
                    Console.WriteLine(string.Concat(e.GetType(), ": ", Helper.RemoveLastNewLine(e.Message)));
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
            const StringComparison stricmp = StringComparison.OrdinalIgnoreCase;
            StreamReader reader = new StreamReader(new FileStream(file, FileMode.Open, FileAccess.Read), Helper.DetectTextEncoding(file));

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
                if (line.StartsWith("[", stricmp) && line.EndsWith("]", stricmp))
                { // Start of section
                    if (appendState)
                        break;
                    else
                    {
                        string foundSection = line.Substring(1, line.Length - 2);
                        if (string.Equals(section, foundSection, stricmp))
                            appendState = true;
                    }
                }
                else if ((idx = line.IndexOf('=')) != -1)
                { // valid ini key
                    if (idx == 0) // key is empty
                        throw new InvalidIniFormatException("[" + line + "] has invalid format");
                    lines.Add(line);
                }

            }

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
            StreamReader sr = new StreamReader(new FileStream(file, FileMode.Open, FileAccess.Read), Helper.DetectTextEncoding(file));
            sections = sections.Distinct().ToArray(); // Remove duplicate

            // If file is blank
            if (sr.Peek() == -1)
            {
                sr.Close();
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
            
            while ((line = sr.ReadLine()) != null)
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
            StreamReader reader = new StreamReader(new FileStream(file, FileMode.Open, FileAccess.Read), Helper.DetectTextEncoding(file));

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
            StreamReader reader = new StreamReader(new FileStream(file, FileMode.Open, FileAccess.Read), Helper.DetectTextEncoding(file));

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
    }
}
