/*
    Copyright (C) 2016-2017 Hajin Jang
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
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PEBakery.Lib;

namespace PEBakery.Core
{
    /// <summary>
    /// How much information will be logged if an Exception is catched in ExecuteCommand?
    /// </summary>
    public enum DebugLevel
    {
        Production = 0, // Only Exception message
        PrintExceptionType = 1, // Print Exception message with Exception type
        PrintExceptionStackTrace = 2, // Print Exception message, type, and stack trace
    }

    public class Engine
    {
        public EngineState state;

        public Engine()
        {

        }

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

        public static string UnescapeStr(string operand)
        {
            return unescapeSeqs.Keys.Aggregate(operand, (from, to) => from.Replace(to, unescapeSeqs[to]));
        }

        public static List<string> UnescapeStrs(List<string> operands)
        {
            for (int i = 0; i < operands.Count; i++)
                operands[i] = UnescapeStr(operands[i]);
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

        public static string EscapeStr(string operand, bool fullEscape = false)
        {
            Dictionary<string, string> dict;
            if (fullEscape)
                dict = fullEscapeSeqs;
            else
                dict = escapeSeqs;
            return dict.Keys.Aggregate(operand, (from, to) => from.Replace(to, dict[to]));
        }

        public static List<string> EscapeStrs(List<string> operands)
        {
            for (int i = 0; i < operands.Count; i++)
                operands[i] = EscapeStr(operands[i]);
            return operands;
        }

        public static string DoublequoteStr(string str)
        {
            if (str.Contains(' '))
                return "\"" + str + "\"";
            else
                return str;
        }

        public static string QuoteEscapeStr(string str)
        {
            bool needQoute = false;

            // Check if str need doublequote escaping
            if (str.Contains(' ') || str.Contains('%') || str.Contains(','))
                needQoute = true;

            // Let's escape characters
            str = EscapeStr(str, false); // WB082 escape sequence
            if (needQoute)
                str = DoublequoteStr(str); // Doublequote escape
            return str;
        }
        #endregion
    }

    public class EngineState
    {
        // Fields used globally
        public Project Project;
        public Tree<Plugin> Plugins;
        // public Variables Variables;
        // public Logger Logger;
        public bool RunOnePlugin;
        public DebugLevel DebugLevel;
        // public Macro macro;

        // Fields : Engine's state
        public Node<Plugin> CurrentNode;
        public Plugin CurrentPlugin { get => CurrentNode.Data; }
        public List<string> curSectionParams;
        public bool runElse;

        // Fields : System Commands
        //private CodeCommand onBuildExit;
        //private CodeCommand onPluginExit;

        public EngineState(Project project, DebugLevel debugLevel)
        {
            this.Project = project;
            this.Plugins = project.GetActivePlugin();
            this.RunOnePlugin = false;
            this.DebugLevel = debugLevel;
        }
    }
}