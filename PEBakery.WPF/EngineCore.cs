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
using System.IO;
using PEBakery.Helper;

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
        public static DebugLevel DebugLevel = DebugLevel.PrintExceptionStackTrace;
        public EngineState s;

        public Engine(EngineState state)
        {
            s = state;
            LoadDefaultFixedVariables();
            LoadDefaultPluginVariables(s, s.CurrentPlugin);
        }

        private void LoadDefaultFixedVariables()
        {
            // BaseDir
            s.Variables.SetFixedValue("BaseDir", s.BaseDir);
            // Tools
            s.Variables.SetFixedValue("Tools", Path.Combine("%BaseDir%", "Projects", "Tools"));

            // Version
            Version version = FileHelper.GetProgramVersion();
            s.Variables.SetFixedValue("Version", version.Build.ToString());
            // ProjectDir
            s.Variables.SetFixedValue("ProjectDir", Path.Combine("%BaseDir%", "Projects", s.Project.ProjectName));
            // TargetDir
            s.Variables.SetFixedValue("TargetDir", Path.Combine("%BaseDir%", "Target", s.Project.ProjectName));
        }

        public static void LoadDefaultPluginVariables(EngineState s, Plugin p)
        {
            // ScriptFile, PluginFile
            s.Variables.SetValue(VarsType.Local, "PluginFile", p.FullPath);
            s.Variables.SetValue(VarsType.Local, "ScriptFile", p.FullPath);

            // [Variables]
            if (p.Sections.ContainsKey("Variables"))
            {
                VarsType type = VarsType.Local;
                if (string.Equals(p.FullPath, s.MainPlugin.FullPath, StringComparison.OrdinalIgnoreCase))
                    type = VarsType.Global;
                List<SimpleLog> logs = s.Variables.AddVariables(type, p.Sections["Variables"]);
            }
        }
    }

    public class EngineState
    {
        // Fields used globally
        public Project Project;
        public Tree<Plugin> Plugins;
        public Variables Variables;
        public Macro Macro;
        public Logger Logger;
        public bool RunOnePlugin;
        public DebugLevel DebugLevel;

        // Properties
        public string BaseDir { get => Project.BaseDir; }
        public Plugin MainPlugin { get => Project.MainPlugin; }

        // Fields : Engine's state
        public Node<Plugin> CurrentNode;
        public Plugin CurrentPlugin { get => CurrentNode.Data; }
        public List<string> CurSectionParams;
        public bool RunElse;

        // Fields : System Commands
        public CodeCommand OnBuildExit;
        public CodeCommand OnPluginExit;

        public EngineState(DebugLevel debugLevel, Project project, Logger logger, Node<Plugin> pluginToRun = null, bool runOnePlugin = false)
        {
            this.DebugLevel = debugLevel;
            this.Project = project;
            this.Plugins = project.GetActivePlugin();
            this.Logger = logger;
            this.RunOnePlugin = runOnePlugin;

            this.Variables = new Variables();
            Macro = new Macro(Project, Variables, out List<SimpleLog> logs);
            // TODO: logger.Write(logs);

            if (runOnePlugin)
                CurrentNode = Plugins.Root[0]; // Main Plugin
            else
                CurrentNode = pluginToRun;
            this.CurSectionParams = new List<string>();
            this.RunElse = false;
            this.OnBuildExit = null;
            this.OnPluginExit = null;
        }
    }
}