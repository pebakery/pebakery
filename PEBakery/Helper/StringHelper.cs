/*
    Copyright (C) 2016-2018 Hajin Jang
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
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace PEBakery.Helper
{
    #region StringHelper
    public static class StringHelper
    {
        #region RemoveLastLine
        /// <summary>
        /// Remove last newline in the string, removes whitespaces also.
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string RemoveLastNewLine(string str)
        {
            return str.Trim().TrimEnd(Environment.NewLine.ToCharArray()).Trim();
        }
        #endregion

        #region Is{Hex|Alphabet|...}
        public static bool IsHex(string str)
        {
            if (str.Length % 2 == 1)
                return false;

            return Regex.IsMatch(str, @"^[A-Fa-f0-9]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        }

        public static bool IsUpperAlphabet(string str)
        {
            foreach (char ch in str)
            {
                if (!IsUpperAlphabet(ch))
                    return false;
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsUpperAlphabet(char ch)
        {
            return 'A' <= ch && ch <= 'Z';
        }

        public static bool IsLowerAlphabet(string str)
        {
            foreach (char ch in str)
            {
                if (!IsLowerAlphabet(ch))
                    return false;
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsLowerAlphabet(char ch)
        {
            return 'a' <= ch && ch <= 'z';
        }

        public static bool IsAlphabet(string str)
        {
            foreach (char ch in str)
            {
                if (!IsAlphabet(ch))
                    return false;
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAlphabet(char ch)
        {
            return 'A' <= ch && ch <= 'Z' || 'a' <= ch && ch <= 'z';
        }

        public static bool IsInteger(string str)
        {
            foreach (char ch in str)
            {
                if (!IsInteger(ch))
                    return false;
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInteger(char ch)
        {
            return '0' <= ch && ch <= '9';
        }


        public static bool IsWildcard(string str)
        {
            return str.IndexOfAny(new[] { '*', '?' }) != -1;
        }
        #endregion

        #region CountSubStr
        public static int CountSubStr(string text, string pattern)
        {
            // Loop through all instances of the string 'text'.
            int count = 0;
            int i = 0;
            while ((i = text.IndexOf(pattern, i, StringComparison.Ordinal)) != -1)
            {
                i += pattern.Length;
                count++;
            }
            return count;
        }
        #endregion

        #region SplitEx
        public static List<string> SplitEx(string str, string seperator, StringComparison comp)
        {
            if (str.Length == 0)
                return new List<string>();
            if (seperator.Length == 0)
                return new List<string> { str };
            if (str.IndexOf(seperator, comp) == -1)
                return new List<string> { str };

            int idx = 0;
            List<string> split = new List<string>();
            while (idx < str.Length)
            {
                int vIdx = str.IndexOf(seperator, idx, comp);
                if (vIdx == -1)
                {
                    split.Add(str.Substring(idx));
                    break;
                }

                split.Add(str.Substring(idx, vIdx - idx));
                idx = vIdx + seperator.Length;
            }
            return split;
        }
        #endregion

        #region Replace Series
        public static string ReplaceEx(string str, string oldValue, string newValue, StringComparison comp)
        {
            if (oldValue.Length == 0)
                return str;
            if (str.IndexOf(oldValue, comp) == -1)
                return str;

            int idx = 0;
            StringBuilder b = new StringBuilder();
            while (idx < str.Length)
            {
                int vIdx = str.IndexOf(oldValue, idx, comp);
                if (vIdx == -1)
                {
                    b.Append(str.Substring(idx));
                    break;
                }

                b.Append(str.Substring(idx, vIdx - idx));
                b.Append(newValue);
                idx = vIdx + oldValue.Length;
            }
            return b.ToString();
        }

        public static string ReplaceRegex(string str, string regex, string newValue, StringComparison comp)
        {
            MatchCollection matches = Regex.Matches(str, regex, RegexOptions.Compiled | RegexOptions.CultureInvariant);
            if (matches.Count == 0)
                return str;

            StringBuilder b = new StringBuilder();
            for (int x = 0; x < matches.Count; x++)
            {
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

                b.Append(newValue);

                if (x + 1 == matches.Count)
                {
                    b.Append(str.Substring(matches[x].Index + matches[x].Value.Length));
                }
            }
            return b.ToString();
        }

        public static string ReplaceAt(string str, int index, int length, string newValue)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
            return str.Substring(0, index) + newValue + str.Substring(index + length);
        }
        #endregion

        #region GetUriProtocol, FormatOpenCommand
        public static string GetUriProtocol(string str)
        {
            int idx = str.IndexOf(@"://", StringComparison.Ordinal);
            if (0 <= idx && idx < str.Length)
                return str.Substring(0, idx);
            else
                return null;
        }

        public static (string, string) FormatOpenCommand(string str, string openFile)
        {
            string formated = StringHelper.ReplaceEx(str, "%1", openFile, StringComparison.Ordinal);
            int exeEndIdx = formated.LastIndexOf(".exe", StringComparison.OrdinalIgnoreCase) + 4;
            string exe = formated.Substring(0, exeEndIdx).Trim().Trim('\"').Trim();
            string arguments = formated.Substring(exeEndIdx).Trim().Trim('\"').Trim();

            return (exe, arguments);
        }
        #endregion

        #region Glob
        public static string GlobToRegex(string glob)
        {
            glob = Regex.Escape(glob);
            glob = ReplaceEx(glob, "\\*", ".*?", StringComparison.Ordinal);
            glob = ReplaceEx(glob, "\\?", ".?", StringComparison.Ordinal);
            return '^' + glob + '$';
        }

        public static IEnumerable<string> MatchGlob(string glob, IEnumerable<string> strs, StringComparison comp)
        {
            string pattern = GlobToRegex(glob);
            RegexOptions regexOptions = RegexOptions.Compiled;
            switch (comp)
            {
                case StringComparison.CurrentCulture:
                    break;
                case StringComparison.CurrentCultureIgnoreCase:
                    regexOptions |= RegexOptions.IgnoreCase;
                    break;
                case StringComparison.InvariantCulture:
                    regexOptions |= RegexOptions.CultureInvariant;
                    break;
                case StringComparison.InvariantCultureIgnoreCase:
                    regexOptions |= RegexOptions.CultureInvariant;
                    regexOptions |= RegexOptions.IgnoreCase;
                    break;
                case StringComparison.Ordinal:
                    regexOptions |= RegexOptions.CultureInvariant;
                    break;
                case StringComparison.OrdinalIgnoreCase:
                    regexOptions |= RegexOptions.CultureInvariant;
                    regexOptions |= RegexOptions.IgnoreCase;
                    break;
            }

            return strs.Where(x => Regex.IsMatch(x, pattern, regexOptions));
        }
        #endregion
    }
    #endregion
}
