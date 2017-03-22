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
        private Command onBuildExit;
        private Command onPluginExit;

        public EngineState(Project project, DebugLevel debugLevel)
        {
            this.Project = project;
            this.Plugins = project.GetActivePlugin();
            this.RunOnePlugin = false;
            this.DebugLevel = debugLevel;
        }
    }
}