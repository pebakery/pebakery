using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    }
}
