using PEBakery.Exceptions;
using PEBakery.Helper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PEBakery.Core
{
    public static class StringEscaper
    {
        #region EscapeString
        private static readonly Dictionary<string, string> unescapeSeqs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { @"#$c", @"," },
            { @"#$p", @"%" },
            { @"#$q", "\"" },
            { @"#$s", @" " },
            { @"#$t", "\t"},
            { @"#$x", "\r\n"},
            // { @"#$z", "\x00\x00"} -> This should go to EngineRegistry
        };

        public static readonly string Legend = "#$c = Comma [,]\r\n#$p = Percent [%]\r\n#$q = DoubleQuote [\"]\r\n#$s = Space [ ]\r\n#$t = Tab [\t]\r\n#$x = NewLine";

        public static string Unescape(string str)
        {
            return unescapeSeqs.Keys.Aggregate(str, (from, to) => from.Replace(to, unescapeSeqs[to]));
        }

        public static List<string> Unescape(IEnumerable<string> strs)
        {
            List<string> unescaped = new List<string>();
            foreach (string str in strs)
                unescaped.Add(Unescape(str));
            return unescaped;
        }

        public static string QuoteUnescape(string str)
        {
            return unescapeSeqs.Keys.Aggregate(str.Trim('\"'), (from, to) => from.Replace(to, unescapeSeqs[to]));
        }

        public static List<string> QuoteUnescape(IEnumerable<string> strs)
        {
            List<string> unescaped = new List<string>();
            foreach (string str in strs)
                unescaped.Add(QuoteUnescape(str));
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

        private static readonly Dictionary<string, string> fullEscapeSeqs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { @",", @"#$c" },
            // { @"%", @"#$p" }, // Seems even WB082 ignore this escape seqeunce?
            { "\"", @"#$q" },
            { @" ", @"#$s" },
            { "\t", @"#$t" },
            { "\r\n", @"#$x" },
        };

        private static readonly Dictionary<string, string> escapeSeqs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "\"", @"#$q" },
            { "\t", @"#$t" },
            { "\r\n", @"#$x" },
        };

        public static string Escape(string str, bool fullEscape = false, bool escapePercent = false)
        {
            Dictionary<string, string> dict;
            if (fullEscape)
                dict = fullEscapeSeqs;
            else
                dict = escapeSeqs;

            str = dict.Keys.Aggregate(str, (from, to) => from.Replace(to, dict[to]));

            if (escapePercent)
                return EscapePercent(str);
            else
                return str;
        }

        public static List<string> Escape(IEnumerable<string> strs, bool fullEscape = false, bool escapePercent = false)
        {
            List<string> escaped = new List<string>();
            foreach (string str in strs)
                escaped.Add(Escape(str, fullEscape, escapePercent));
            return escaped;
        }

        public static string EscapePercent(string str)
        {
            return str.Replace(@"%", @"#$p");
        }

        public static List<string> EscapePercent(IEnumerable<string> strs)
        {
            List<string> unescaped = new List<string>();
            foreach (string str in strs)
                unescaped.Add(EscapePercent(str));
            return unescaped;
        }

        public static string Doublequote(string str)
        {
            if (str.Contains(' '))
                return "\"" + str + "\"";
            else
                return str;
        }

        public static string QuoteEscape(string str)
        {
            bool needQoute = false;

            // Check if str need doublequote escaping
            // if (str.Contains(' ') || str.Contains('%') || str.Contains(','))
            if (str.Contains(' ') || str.Contains(','))
                needQoute = true;

            // Let's escape characters
            str = Escape(str, false); // WB082 escape sequence
            if (needQoute)
                str = Doublequote(str); // Doublequote escape
            return str;
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
            do
            { // TODO: Prevent Infinite loop
                str = s.Variables.Expand(ExpandSectionParams(s, str));
            }
            while (Variables.DetermineType(str) != Variables.VarKeyType.None);

            return str;
        }

        public static List<string> ExpandVariables(EngineState s, IEnumerable<string> strs)
        {
            List<string> list = new List<string>();
            foreach (string str in strs)
                list.Add(s.Variables.Expand(ExpandSectionParams(s, str)));
            return list;
        }

        public static string ExpandVariables(Variables vars, string str)
        {
            return vars.Expand(str);
        }

        public static List<string> ExpandVariables(Variables vars, IEnumerable<string> strs)
        {
            List<string> list = new List<string>();
            foreach (string str in strs)
                list.Add(vars.Expand(str));
            return list;
        }

        /// <summary>
        /// Expand #1, #2, #3, etc...
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string ExpandSectionParams(EngineState s, string str)
        {
            // Expand #1 into its value
            Regex regex = new Regex(@"(#\d+)", RegexOptions.Compiled);
            MatchCollection matches = regex.Matches(str);

            while (0 < matches.Count)
            {
                StringBuilder builder = new StringBuilder();
                for (int x = 0; x < matches.Count; x++)
                {
                    if (NumberHelper.ParseInt32(matches[x].Groups[1].ToString().Substring(1), out int pIdx) == false)
                        throw new InternalException("ExpandVariables failure");
                    if (x == 0)
                        builder.Append(str.Substring(0, matches[0].Index));
                    else
                    {
                        int startOffset = matches[x - 1].Index + matches[x - 1].Value.Length;
                        int endOffset = matches[x].Index - startOffset;
                        builder.Append(str.Substring(startOffset, endOffset));
                    }

                    string param;
                    if (s.CurSectionParams.ContainsKey(pIdx))
                    {
                        //if (s.CurSectionParams[pIdx].Equals($"#{pIdx}", StringComparison.Ordinal))
                        //    param = string.Empty; // TODO: Really, this code should not be reached, but being readched (....)
                        //else
                        //    param = s.CurSectionParams[pIdx];
                        param = s.CurSectionParams[pIdx];
                    }
                    else
                    {
                        /*
                        TODO: What is the internal logic of WB082?

                        Test Result
                            In [Process]
                                Message,#3
                            Printed "#3"

                            In [Process2] 
                                Run,%ScriptFile%,Process2,Test)
                                [Process2]
                                Message,#3
                            Printed ""
                        */

                        // param = matches[x].Value;
                        param = string.Empty;
                    }
                    builder.Append(param);

                    if (x + 1 == matches.Count) // Last iteration
                        builder.Append(str.Substring(matches[x].Index + matches[x].Value.Length));
                }
                str = builder.ToString();

                matches = regex.Matches(str);
            }

            if (s.LoopRunning)
            { // Escape #c
                int idx = str.IndexOf("#c", StringComparison.OrdinalIgnoreCase);
                if (idx != -1)
                {
                    StringBuilder b = new StringBuilder();
                    b.Append(str.Substring(0, idx));
                    b.Append(s.LoopCounter);
                    b.Append(str.Substring(idx + 2)); // +2 for removing #c
                    str = b.ToString();
                }
            }

            return str;
        }
        #endregion

        #region Preprocess
        public static string Preprocess(EngineState s, string str)
        {
            return Unescape(ExpandVariables(s, str));
        }

        public static List<string> Preprocess(EngineState s, IEnumerable<string> strs)
        {
            return Unescape(ExpandVariables(s, strs));
        }

        public static string Preprocess(Variables vars, string str)
        {
            return Unescape(ExpandVariables(vars, str));
        }

        public static List<string> Preprocess(Variables vars, IEnumerable<string> strs)
        {
            return Unescape(ExpandVariables(vars, strs));
        }
        #endregion

        #region PathSecurity
        private static readonly List<string> forbiddenPaths = new List<string>
        {
            Environment.GetEnvironmentVariable("WinDir"),
            Environment.GetEnvironmentVariable("ProgramFiles"),
            Environment.GetEnvironmentVariable("ProgramFiles(x86)")
        };

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <returns>Return false if path is forbidden</returns>
        public static bool PathSecurityCheck(string path, out string errorMsg)
        {
            string fullPath = Path.GetFullPath(path);
            foreach (string f in forbiddenPaths)
            {
                if (fullPath.StartsWith(f, StringComparison.OrdinalIgnoreCase))
                {
                    errorMsg = $"Cannot write into [{path}], [{f}] is write protected directory";
                    return false;
                }
            }
            errorMsg = string.Empty;
            return true;
        }
        #endregion
    }
}
