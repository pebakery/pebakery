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
        private static readonly Dictionary<string, string> unescapeChars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { @"#$c", @"," },
            { @"#$p", @"%" },
            { @"#$q", @""""},
            { @"#$s", @" " },
            { @"#$t", "\t"},
            { @"#$x", "\r\n"},
            { @"#$h", @"#" }, // Extended
            //{ @"#$z", "\x00\x00"},
        };

        public static string UnescapeString(string operand)
        {
            return unescapeChars.Keys.Aggregate(operand, (from, to) => from.Replace(to, unescapeChars[to]));
        }

        public static List<string> UnescapeStrings(List<string> operands)
        {
            for (int i = 0; i < operands.Count; i++)
                operands[i] = UnescapeString(operands[i]);
            return operands;
        }

        public static string EscapeString(string operand)
        {
            Dictionary<string, string> escapeChars = unescapeChars.ToDictionary(kp => kp.Value, kp => kp.Key, StringComparer.OrdinalIgnoreCase);
            return escapeChars.Keys.Aggregate(operand, (from, to) => from.Replace(to, escapeChars[to]));
        }

        public static List<string> EscapeStrings(List<string> operands)
        {
            for (int i = 0; i < operands.Count; i++)
                operands[i] = EscapeString(operands[i]);
            return operands;
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