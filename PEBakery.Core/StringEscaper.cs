/*
    Copyright (C) 2016-2019 Hajin Jang
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
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace PEBakery.Core
{
    public static class StringEscaper
    {
        #region Static Variables and Constructor
        private static readonly string[] ForbiddenPaths =
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            // Equal to Environment.SpecialFolder.ProgramFiles in x86 Windows (32bit)
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        };
        #endregion

        #region PathSecurityCheck
        /// <summary>
        /// 
        /// </summary>
        /// <returns>Return false if path is forbidden</returns>
        public static bool PathSecurityCheck(string path, out string errorMsg)
        {
            errorMsg = string.Empty;
            if (path.Length == 0) // Path.GetFullPath(string.Empty) throws ArgumentException
                return true;

            // PathSecurityCheck should be able to process paths like [*.exe]
            // So remove filename if necessary.
            string fullPath;
            int lastWildcardIdx = path.IndexOfAny(new char[] { '*', '?' });
            if (lastWildcardIdx != -1 && path.LastIndexOf('\\') < lastWildcardIdx)
                fullPath = Path.GetFullPath(FileHelper.GetDirNameEx(path));
            else
                fullPath = Path.GetFullPath(path);

            foreach (string f in ForbiddenPaths)
            {
                if (fullPath.StartsWith(f, StringComparison.OrdinalIgnoreCase))
                {
                    errorMsg = $"Cannot write into [{path}], [{f}] is a write protected directory";
                    return false;
                }
            }
            return true;
        }
        #endregion

        #region IsValid Series
        public static bool IsPathValid(string path, IEnumerable<char> more = null)
        {
            // Windows Reserved Characters
            // https://msdn.microsoft.com/en-us/library/windows/desktop/aa365247(v=vs.85).aspx
            // Exclude backslash, because this function will receive 
            char[] invalidChars = Path.GetInvalidFileNameChars().Where(x => x != '\\').ToArray();

            // Ex) "C:\Program Files"
            Match m = Regex.Match(path, "^[A-Za-z]:", RegexOptions.Compiled | RegexOptions.CultureInvariant);
            if (m.Success)
            {
                for (int i = 0; i < path.Length; i++)
                {
                    char ch = path[i];
                    if (invalidChars.Contains(ch))
                    {
                        if (!(ch == ':' && i == 1)) // Ex) C:\Users -> ':' should be ignored
                            return false;
                    }
                }
            }
            else
            {
                foreach (char ch in path)
                {
                    if (invalidChars.Contains(ch))
                        return false;
                }
            }

            if (more != null)
            {
                foreach (char ch in path)
                {
                    // ReSharper disable once PossibleMultipleEnumeration
                    if (more.Contains(ch))
                        return false;
                }
            }

            return true;
        }

        public static bool IsFileNameValid(string path, IEnumerable<char> more = null)
        {
            // Windows Reserved Characters
            // https://msdn.microsoft.com/en-us/library/windows/desktop/aa365247(v=vs.85).aspx
            // Exclude backslash, because this function will receive 
            char[] invalidChars = Path.GetInvalidFileNameChars();

            foreach (char ch in path)
            {
                if (invalidChars.Contains(ch))
                    return false;
            }

            if (more != null)
            {
                foreach (char ch in path)
                {
                    // ReSharper disable once PossibleMultipleEnumeration
                    if (more.Contains(ch))
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Check if given url is valid http or https url.
        /// </summary>
        /// <param name="url"></param>
        /// <returns>True if valid</returns>
        public static bool IsUrlValid(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out _);
        }
        #endregion

        #region EscapeString
        /*
        private static readonly Dictionary<string, string> unescapeSeqs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { @"#$c", @"," },
            { @"#$p", @"%" },
            { @"#$q", "\"" },
            { @"#$s", @" " },
            { @"#$t", "\t"},
            { @"#$x", Environment.NewLine},
        };
        */
        public const string Legend = "#$c = Comma [,]\r\n#$p = Percent [%]\r\n#$q = DoubleQuote [\"]\r\n#$s = Space [ ]\r\n#$t = Tab [\t]\r\n#$x = NewLine\r\n## = Sharp [#]";

        public static string Unescape(string str, bool escapePercent = false)
        {
            int idx = 0;
            StringBuilder b = new StringBuilder();
            while (idx < str.Length)
            {
                int hIdx = str.IndexOf('#', idx);
                if (hIdx == -1)
                { // # (X)
                    b.Append(str.Substring(idx));
                    break;
                }
                else
                { // # (O)
                    b.Append(str.Substring(idx, hIdx - idx));
                    if (hIdx + 1 < str.Length)
                    {
                        char ch1 = str[hIdx + 1];
                        if (ch1 == '#')
                        { // ## -> [#]
                            b.Append('#');
                            idx = hIdx + 2;
                        }
                        else if (ch1 == '$')
                        {
                            if (hIdx + 2 < str.Length)
                            {
                                char ch2 = str[hIdx + 2];
                                switch (ch2)
                                {
                                    case 'c': // #$c -> [,]
                                    case 'C':
                                        b.Append(',');
                                        break;
                                    case 'p': // #$p -> [%]
                                    case 'P':
                                        b.Append('%');
                                        break;
                                    case 'q': // #$q -> ["]
                                    case 'Q':
                                        b.Append('"');
                                        break;
                                    case 's': // #$s -> [ ]
                                    case 'S':
                                        b.Append(' ');
                                        break;
                                    case 't': // #$t -> [\t]
                                    case 'T':
                                        b.Append('\t');
                                        break;
                                    case 'x': // #$x -> [\r\n]
                                    case 'X':
                                        b.Append("\r\n");
                                        break;
                                    default: // No escape
                                        b.Append(@"#$");
                                        idx = hIdx + 2;
                                        continue;
                                }
                                idx = hIdx + 3;
                            }
                            else
                            { // Last 2 characters of string
                                b.Append("#$");
                                idx = hIdx + 2;
                            }
                        }
                        else
                        {
                            b.Append('#');
                            idx = hIdx + 1;
                        }
                    }
                    else
                    { // Last character of string
                        b.Append('#');
                        idx = hIdx + 1;
                    }
                }
            }
            str = b.ToString();

            if (escapePercent)
                str = UnescapePercent(str);

            return str;
        }

        public static List<string> Unescape(IEnumerable<string> strs, bool escapePercent = false)
        {
            return strs.Select(str => Unescape(str, escapePercent)).ToList();
        }

        public static string QuoteUnescape(string str, bool escapePercent = false)
        {
            return Unescape(str.Trim('\"'), escapePercent);
        }

        public static List<string> QuoteUnescape(IEnumerable<string> strs, bool escapePercent = false)
        {
            List<string> unescaped = new List<string>();
            foreach (string str in strs)
                unescaped.Add(QuoteUnescape(str, escapePercent));
            return unescaped;
        }

        public static string UnescapePercent(string str)
        {
            return str.Replace(@"#$p", @"%");
        }

        public static List<string> UnescapePercent(IEnumerable<string> strs)
        {
            List<string> unescaped = new List<string>();
            foreach (string str in strs)
                unescaped.Add(UnescapePercent(str));
            return unescaped;
        }

        private static readonly Dictionary<string, string> _fullEscapeSeqs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { @",", @"#$c" },
            { "\"", @"#$q" },
            { @" ", @"#$s" },
            { "\t", @"#$t" },
            { Environment.NewLine, @"#$x" },
        };

        private static readonly Dictionary<string, string> _escapeSeqs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "\"", @"#$q" },
            { "\t", @"#$t" },
            { Environment.NewLine, @"#$x" },
        };

        public static string Escape(string str, bool fullEscape = false, bool escapePercent = false)
        {
            // Escape # first
            if (str.IndexOf('#') != -1)
            {
                int idx = 0;
                StringBuilder b = new StringBuilder();
                while (idx < str.Length)
                {
                    int hIdx = str.IndexOf('#', idx);

                    if (hIdx == -1)
                    { // # (X)
                        b.Append(str.Substring(idx));
                        break;
                    }

                    // # (O)
                    b.Append(str.Substring(idx, hIdx - idx));
                    b.Append(@"##");
                    idx = hIdx + 1;
                }
                str = b.ToString();
            }

            Dictionary<string, string> dict = fullEscape ? _fullEscapeSeqs : _escapeSeqs;
            str = dict.Keys.Aggregate(str, (from, to) => from.Replace(to, dict[to]));

            if (escapePercent)
                str = EscapePercent(str);

            return str;
        }

        public static List<string> Escape(IEnumerable<string> strs, bool fullEscape = false, bool escapePercent = false)
        {
            return strs.Select(str => Escape(str, fullEscape, escapePercent)).ToList();
        }

        public static string EscapePercent(string str)
        {
            return StringHelper.ReplaceEx(str, @"%", @"#$p", StringComparison.Ordinal);
        }

        public static List<string> EscapePercent(IEnumerable<string> strs)
        {
            return strs.Select(EscapePercent).ToList();
        }

        public static string DoubleQuote(string str)
        {
            if (str.StartsWith("\"") && str.EndsWith("\""))
                return str;
            if (str.Contains(' ') || str.Contains(','))
                return $"\"{str.Trim('\"')}\"";
            return str;
        }

        public static string QuoteEscape(string str, bool fullEscape = false, bool escapePercent = false)
        {
            // Escape characters
            str = Escape(str, fullEscape, escapePercent); // WB082 escape sequence
            // DoubleQuote escape
            str = DoubleQuote(str);

            return str;
        }

        public static List<string> QuoteEscape(IEnumerable<string> strs, bool fullEscape = false, bool escapePercent = false)
        {
            return strs.Select(str => QuoteEscape(str, fullEscape, escapePercent)).ToList();
        }
        #endregion

        #region Variables
        /// <summary>
        /// Expand #n and %Var% variables.
        /// </summary>
        /// <param name="s"></param>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string ExpandVariables(EngineState s, string str)
        {
            return s.Variables.Expand(ExpandSectionParams(s, str));
        }

        public static List<string> ExpandVariables(EngineState s, IEnumerable<string> strs)
        {
            return strs.Select(str => s.Variables.Expand(ExpandSectionParams(s, str))).ToList();
        }

        public static string ExpandVariables(Variables vars, string str)
        {
            return vars.Expand(str);
        }

        public static List<string> ExpandVariables(Variables vars, IEnumerable<string> strs)
        {
            return strs.Select(vars.Expand).ToList();
        }

        /// <summary>
        /// Expand #1, #2, #3, etc...
        /// </summary>
        public static string ExpandSectionParams(EngineState s, string str)
        {
            // Expand #1 into its value
            // string inParamRegex = s.CompatDisableExtendedSectionParams ? @"(?<!#)(#[1-9])" : @"(?<!#)(#[1-9][0-9]*)";
            // Regex inRegex = new Regex(inParamRegex, RegexOptions.Compiled | RegexOptions.CultureInvariant);
            Regex inRegex = new Regex(@"(?<!#)(#[1-9])", RegexOptions.Compiled | RegexOptions.CultureInvariant);
            MatchCollection matches = inRegex.Matches(str);
            while (0 < matches.Count)
            {
                StringBuilder b = new StringBuilder();
                for (int x = 0; x < matches.Count; x++)
                {
                    string pIdxStr = matches[x].Groups[1].ToString().Substring(1);
                    if (!int.TryParse(pIdxStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int pIdx))
                        throw new InternalException("ExpandSectionParams failure");

                    if (x == 0)
                    {
                        b.Append(str.Substring(0, matches[0].Index));
                    }
                    else
                    {
                        int startOffset = matches[x - 1].Index + matches[x - 1].Value.Length;
                        int endOffset = matches[x].Index - startOffset;
                        b.Append(str.Substring(startOffset, endOffset));
                    }

                    string param;
                    if (s.CurSectionInParams.ContainsKey(pIdx))
                    {
                        param = s.CurSectionInParams[pIdx];
                    }
                    else
                    {
                        if (s.PeekDepth == 1) // Dirty Hack for WB082 compatibility
                            param = $"##{pIdx}"; // [Process] -> Should return #{pIdx} even it was not found
                        else
                            param = string.Empty; // Not in entry section -> return string.Empty;
                    }
                    b.Append(param);

                    if (x + 1 == matches.Count) // Last iteration
                    {
                        b.Append(str.Substring(matches[x].Index + matches[x].Value.Length));
                    }
                }
                str = b.ToString();

                matches = inRegex.Matches(str);
            }

            if (!s.CompatDisableExtendedSectionParams)
            {
                // Escape #o1, #o2, ... (Section Out Parameter)
                // string outParamRegex = s.CompatDisableExtendedSectionParams ? @"(?<!#)(#[oO][1-9])" : @"(?<!#)(#[oO][1-9][0-9]*)";
                // Regex outRegex = new Regex(outParamRegex, RegexOptions.Compiled | RegexOptions.CultureInvariant);
                Regex outRegex = new Regex(@"(?<!#)(#[oO][1-9])", RegexOptions.Compiled | RegexOptions.CultureInvariant);
                matches = outRegex.Matches(str);
                while (0 < matches.Count)
                {
                    StringBuilder b = new StringBuilder();
                    for (int x = 0; x < matches.Count; x++)
                    {
                        string pIdxStr = matches[x].Groups[1].ToString().Substring(2);
                        if (!int.TryParse(pIdxStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int pIdx))
                            throw new InternalException("ExpandSectionParams failure");

                        if (x == 0)
                        {
                            b.Append(str.Substring(0, matches[0].Index));
                        }
                        else
                        {
                            int startOffset = matches[x - 1].Index + matches[x - 1].Value.Length;
                            int endOffset = matches[x].Index - startOffset;
                            b.Append(str.Substring(startOffset, endOffset));
                        }

                        string param;
                        if (1 <= pIdx && pIdx <= s.CurSectionOutParams.Count)
                        {
                            string varKey = s.CurSectionOutParams[pIdx - 1];
                            param = s.Variables.Expand(varKey);
                        }
                        else
                        {
                            param = string.Empty;
                        }
                        b.Append(param);

                        if (x + 1 == matches.Count) // Last iteration
                        {
                            b.Append(str.Substring(matches[x].Index + matches[x].Value.Length));
                        }
                    }
                    str = b.ToString();

                    matches = inRegex.Matches(str);
                }

                // Escape #a (Section In Params Count)
                if (str.IndexOf("#a", StringComparison.OrdinalIgnoreCase) != -1)
                    str = StringHelper.ReplaceRegex(str, @"(?<!#)(#[aA])", s.CurSectionInParamsCount.ToString());

                // Escape #oa (Section Out Params Count)
                if (str.IndexOf("#oa", StringComparison.OrdinalIgnoreCase) != -1)
                    str = StringHelper.ReplaceRegex(str, @"(?<!#)(#[oO][aA])", s.CurSectionInParamsCount.ToString());

                // Escape #r (Return Value)
                if (str.IndexOf("#r", StringComparison.OrdinalIgnoreCase) != -1)
                    str = StringHelper.ReplaceRegex(str, @"(?<!#)(#[rR])", s.SectionReturnValue);
            }

            // Escape #c (Loop Counter)
            switch (s.LoopState)
            {
                case LoopState.OnIndex:
                    str = StringHelper.ReplaceRegex(str, @"(?<!#)(#[cC])", s.LoopCounter.ToString());
                    break;
                case LoopState.OnDriveLetter:
                    str = StringHelper.ReplaceRegex(str, @"(?<!#)(#[cC])", s.LoopLetter.ToString());
                    break;
            }

            return str;
        }

        public static List<string> ExpandSectionParams(EngineState s, IEnumerable<string> strs)
        {
            return strs.Select(str => ExpandSectionParams(s, str)).ToList();
        }
        #endregion

        #region Preprocess
        public static string Preprocess(EngineState s, string str, bool escapePercent = true)
        {
            return Unescape(ExpandVariables(s, str), escapePercent);
        }

        public static List<string> Preprocess(EngineState s, IEnumerable<string> strs, bool escapePercent = true)
        {
            return Unescape(ExpandVariables(s, strs), escapePercent);
        }

        public static string Preprocess(Variables vars, string str, bool escapePercent = true)
        {
            return Unescape(ExpandVariables(vars, str), escapePercent);
        }

        public static List<string> Preprocess(Variables vars, IEnumerable<string> strs, bool escapePercent = true)
        {
            return Unescape(ExpandVariables(vars, strs), escapePercent);
        }
        #endregion

        #region GetUniqueKey, GetUniqueFileName
        public static string GetUniqueKey(string srcKey, IEnumerable<string> keys)
        {
            int idx = 0;
            string key;
            bool duplicate;
            string[] keyArr = keys.ToArray();
            do
            {
                idx++;
                duplicate = false;

                key = $"{srcKey}{idx:D2}";

                if (keyArr.Contains(key, StringComparer.OrdinalIgnoreCase))
                    duplicate = true;
            } while (duplicate);

            return key;
        }

        public static string GetUniqueFileName(string srcKey, IEnumerable<string> keys)
        {
            string fileName = Path.GetFileNameWithoutExtension(srcKey);
            string ext = Path.GetExtension(srcKey);

            int idx = 0;
            string key;
            bool duplicate;
            string[] keyArr = keys.ToArray();
            do
            {
                idx++;
                duplicate = false;

                key = $"{fileName}{idx:D2}{ext}";

                if (keyArr.Contains(key, StringComparer.OrdinalIgnoreCase))
                    duplicate = true;
            } while (duplicate);

            return key;
        }
        #endregion

        #region Registry
        public static string PackRegBinary(byte[] bin, bool escape = false)
        { // Ex) 43,00,3A,00,5C,00,55,00,73,00,65,00,72,00,73,00,5C,00,4A,00,6F,00,76,00,65,00,6C,00,65,00,72,00,5C,00,4F,00,6E,00,65,00,44,00,72,00,69,00,76,00,65,00,00,00
            string seperator = ",";
            if (escape)
                seperator = "#$c";

            StringBuilder b = new StringBuilder();
            for (int i = 0; i < bin.Length; i++)
            {
                b.Append(bin[i].ToString("X2"));
                if (i + 1 < bin.Length)
                    b.Append(seperator);
            }

            return b.ToString();
        }

        public static string PackRegBinary(string[] strs, bool escape = false)
        { // Ex) 43,00,3A,00,5C,00,55,00,73,00,65,00,72,00,73,00,5C,00,4A,00,6F,00,76,00,65,00,6C,00,65,00,72,00,5C,00,4F,00,6E,00,65,00,44,00,72,00,69,00,76,00,65,00,00,00
            string seperator = ",";
            if (escape)
                seperator = "#$c";

            StringBuilder b = new StringBuilder();
            for (int i = 0; i < strs.Length; i++)
            {
                b.Append(strs[i]);
                if (i + 1 < strs.Length)
                    b.Append(seperator);
            }

            return b.ToString();
        }

        public static bool UnpackRegBinary(string packStr, out byte[] bin)
        { // Ex) 43,00,3A,00,5C,00,55,00,73,00,65,00,72,00,73,00,5C,00,4A,00,6F,00,76,00,65,00,6C,00,65,00,72,00,5C,00,4F,00,6E,00,65,00,44,00,72,00,69,00,76,00,65,00,00,00
            int count = (packStr.Length + 1) / 3;
            bin = new byte[count]; // 3n-1

            for (int i = 0; i < count; i++)
            {
                if (!byte.TryParse(packStr.Substring(i * 3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out bin[i]))
                    return false;
            }

            return true;
        }

        public static bool UnpackRegBinary(string[] packStrs, out byte[] bin)
        { // Ex) 43,00,3A,00,5C,00,55,00,73,00,65,00,72,00,73,00,5C,00,4A,00,6F,00,76,00,65,00,6C,00,65,00,72,00,5C,00,4F,00,6E,00,65,00,44,00,72,00,69,00,76,00,65,00,00,00
            bin = new byte[packStrs.Length];

            for (int i = 0; i < packStrs.Length; i++)
            {
                if (!byte.TryParse(packStrs[i], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out bin[i]))
                    return false;
            }

            return true;
        }

        public static string PackRegMultiBinary(IEnumerable<string> multiStrs)
        {
            StringBuilder b = new StringBuilder();

            string[] list = multiStrs.ToArray();
            for (int i = 0; i < list.Length; i++)
            {
                byte[] bin = Encoding.Unicode.GetBytes(list[i]);
                b.Append(StringEscaper.PackRegBinary(bin));
                if (i + 1 < list.Length)
                    b.Append(",00,00,");
            }

            return b.ToString();
        }

        public static string PackRegMultiString(IEnumerable<string> multiStrs)
        {
            // RegRead,HKLM,SOFTWARE\Microsoft\Windows NT\CurrentVersion\FontLink\SystemLink,Batang,%A%
            // MSMINCHO.TTC,MS PMincho#$zMINGLIU.TTC,PMingLiU#$zSIMSUN.TTC,SimSun#$zMALGUN.TTF,Malgun Gothic#$zYUGOTHM.TTC,Yu Gothic UI#$zMSJH.TTC,Microsoft JhengHei UI#$zMSYH.TTC,Microsoft YaHei UI#$zSEGUISYM.TTF,Segoe UI Symbol

            string[] list = multiStrs.ToArray();

            StringBuilder b = new StringBuilder();
            for (int i = 0; i < list.Length; i++)
            {
                b.Append(list[i]);
                if (i + 1 < list.Length)
                    b.Append("#$z");
            }
            return b.ToString();
        }

        public static List<string> UnpackRegMultiString(string packStr)
        {
            // RegRead,HKLM,SOFTWARE\Microsoft\Windows NT\CurrentVersion\FontLink\SystemLink,Batang,%A%
            // MSMINCHO.TTC,MS PMincho#$zMINGLIU.TTC,PMingLiU#$zSIMSUN.TTC,SimSun#$zMALGUN.TTF,Malgun Gothic#$zYUGOTHM.TTC,Yu Gothic UI#$zMSJH.TTC,Microsoft JhengHei UI#$zMSYH.TTC,Microsoft YaHei UI#$zSEGUISYM.TTF,Segoe UI Symbol

            List<string> list = new List<string>();

            string next = packStr;
            while (next != null)
            {
                int pIdx = next.IndexOf("#$z", StringComparison.Ordinal);
                if (pIdx != -1)
                { // Not Last One
                    string now = next.Substring(0, pIdx);
                    next = next.Substring(pIdx + 3);

                    list.Add(now);
                }
                else
                { // Last One
                    list.Add(next);

                    next = null;
                }
            }

            return list;
        }
        #endregion

        #region ProcessScriptVersionString
        public static string ProcessVersionString(string str)
        {
            // Integer - Ex) 001 -> 1
            if (NumberHelper.ParseInt32(str, out int intVal))
                return intVal.ToString();

            // Semantic versioning - Ex) 5.1.2600 
            // If does not conform to semantic versioning, return null
            NumberHelper.VersionEx semVer = NumberHelper.VersionEx.Parse(str);
            return semVer?.ToString();
        }
        #endregion

        #region List as Concatinated String
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static List<string> UnpackListStr(string listStr, string seperator)
        {
            return StringHelper.SplitEx(listStr, seperator, StringComparison.OrdinalIgnoreCase);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string PackListStr(IList<string> list, string seperator)
        {
            return string.Join(seperator, list);
        }
        #endregion
    }
}
