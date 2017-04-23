using PEBakery.Exceptions;
using PEBakery.Helper;
using System;
using System.Collections.Generic;
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

        public static string Unescape(string operand)
        {
            return unescapeSeqs.Keys.Aggregate(operand, (from, to) => from.Replace(to, unescapeSeqs[to]));
        }

        public static List<string> UnescapeList(List<string> operands)
        {
            for (int i = 0; i < operands.Count; i++)
                operands[i] = Unescape(operands[i]);
            return operands;
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

        public static string Escape(string operand, bool fullEscape = false)
        {
            Dictionary<string, string> dict;
            if (fullEscape)
                dict = fullEscapeSeqs;
            else
                dict = escapeSeqs;
            return dict.Keys.Aggregate(operand, (from, to) => from.Replace(to, dict[to]));
        }

        public static List<string> EscapeList(List<string> operands)
        {
            for (int i = 0; i < operands.Count; i++)
                operands[i] = Escape(operands[i]);
            return operands;
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
            if (str.Contains(' ') || str.Contains('%') || str.Contains(','))
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
            return s.Variables.Expand(ExpandSectionParams(s, str));
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
            MatchCollection matches = Regex.Matches(str, @"(#\d+)", RegexOptions.Compiled);
            StringBuilder builder = new StringBuilder();
            for (int x = 0; x < matches.Count; x++)
            {
                if (NumberHelper.ParseInt32(matches[x].Groups[1].ToString().Substring(1), out int pIdx) == false)
                    throw new InternalErrorException("ExpandVariables failure");
                if (x == 0)
                    builder.Append(str.Substring(0, matches[0].Index));
                else
                {
                    int startOffset = matches[x - 1].Index + matches[x - 1].Value.Length;
                    int endOffset = matches[x].Index - startOffset;
                    builder.Append(str.Substring(startOffset, endOffset));
                }

                string param;
                try
                {
                    param = s.CurSectionParams[pIdx];
                }
                catch (KeyNotFoundException)
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
            if (0 < matches.Count) // Only copy it if variable exists
            {
                str = builder.ToString();
            }

            if (s.LoopRunning)
            { // Escape #c
                if (str.IndexOf("#c", StringComparison.OrdinalIgnoreCase) != -1)
                {
                    str = str.Replace("#c", s.LoopCounter.ToString());
                    str = str.Replace("#C", s.LoopCounter.ToString());
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

        public static List<string> Preprocess(EngineState s, List<string> strs)
        {
            return UnescapeList(ExpandVariables(s, strs));
        }

        public static string Preprocess(Variables vars, string str)
        {
            return Unescape(ExpandVariables(vars, str));
        }

        public static List<string> Preprocess(Variables vars, List<string> strs)
        {
            return UnescapeList(ExpandVariables(vars, strs));
        }
        #endregion
    }
}
