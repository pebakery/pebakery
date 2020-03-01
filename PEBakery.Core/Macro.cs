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

using PEBakery.Ini;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace PEBakery.Core
{
    public enum MacroType
    {
        Global = 0,
        Local = 1,
    }

    public class Macro
    {
        #region Constants
        public class KnownVar
        {
            public const string APIVAR = "APIVAR";
            public const string API = "API";
        }
        #endregion

        #region Field and Property
        public bool MacroEnabled { get; }
        /// <summary>
        /// %API% of sciprt.project
        /// </summary>
        public Script MacroScript { get; }
        /// <summary>
        /// %APIVAR% of sciprt.project
        /// </summary>
        public ScriptSection MacroSection { get; }
        /// <summary>
        /// [ApiVar] of macro script
        /// </summary>
        public Dictionary<string, CodeCommand> GlobalDict { get; private set; }
            = new Dictionary<string, CodeCommand>(StringComparer.OrdinalIgnoreCase);
        /// <summary>
        /// Macro defined in current script's [Variables] 
        /// </summary>
        public Dictionary<string, CodeCommand> LocalDict { get; private set; }
            = new Dictionary<string, CodeCommand>(StringComparer.OrdinalIgnoreCase);

        public const string MacroNameRegex = @"^([a-zA-Z0-9_]+)$";
        #endregion

        #region Constructor
        public Macro(Project project, Variables variables, out List<LogInfo> logs)
        {
            logs = new List<LogInfo>();

            MacroEnabled = true;
            if (!project.MainScript.Sections.ContainsKey(ScriptSection.Names.Variables))
            {
                MacroEnabled = false;
                logs.Add(new LogInfo(LogState.Info, "Macro not defined"));
                return;
            }

            ScriptSection mainScriptVarSection = project.MainScript.Sections[ScriptSection.Names.Variables];

            Dictionary<string, string> varDict = IniReadWriter.ParseIniLinesVarStyle(mainScriptVarSection.Lines);
            if (!(varDict.ContainsKey(KnownVar.API) && varDict.ContainsKey(KnownVar.APIVAR)))
            {
                MacroEnabled = false;
                logs.Add(new LogInfo(LogState.Info, "Macro not defined"));
                return;
            }

            // Get macroScript
            string rawScriptPath = varDict[KnownVar.API];
            string macroScriptPath = variables.Expand(varDict[KnownVar.API]); // Need expansion
            MacroScript = project.AllScripts.Find(x => x.RealPath.Equals(macroScriptPath, StringComparison.OrdinalIgnoreCase));
            if (MacroScript == null)
            {
                MacroEnabled = false;
                logs.Add(new LogInfo(LogState.Error, $"Macro defined but unable to find macro script [{rawScriptPath}"));
                return;
            }

            // Get macroScript
            if (!MacroScript.Sections.ContainsKey(varDict[KnownVar.APIVAR]))
            {
                MacroEnabled = false;
                logs.Add(new LogInfo(LogState.Error, $"Macro defined but unable to find macro section [{varDict[KnownVar.APIVAR]}"));
                return;
            }
            MacroSection = MacroScript.Sections[varDict[KnownVar.APIVAR]];
            variables.SetValue(VarsType.Global, KnownVar.API, macroScriptPath);
            if (MacroScript.Sections.ContainsKey(ScriptSection.Names.Variables))
                logs.AddRange(variables.AddVariables(VarsType.Global, MacroScript.Sections[ScriptSection.Names.Variables]));

            // Import Section [APIVAR]'s variables, such as '%Shc_Mode%=0'
            logs.AddRange(variables.AddVariables(VarsType.Global, MacroSection));

            // Parse Section [APIVAR] into MacroDict
            {
                ScriptSection section = MacroSection;
                Dictionary<string, string> rawDict = IniReadWriter.ParseIniLinesIniStyle(MacroSection.Lines);
                foreach (var kv in rawDict)
                {
                    try
                    {
                        if (Regex.Match(kv.Key, MacroNameRegex, RegexOptions.Compiled | RegexOptions.CultureInvariant).Success)
                        { // Macro Name Validation
                            CodeParser parser = new CodeParser(section, Global.Setting, section.Project.Compat);
                            GlobalDict[kv.Key] = parser.ParseStatement(kv.Value);
                        }
                        else
                        {
                            logs.Add(new LogInfo(LogState.Error, $"Invalid macro name [{kv.Key}]"));
                        }
                    }
                    catch (Exception e)
                    {
                        logs.Add(new LogInfo(LogState.Error, e));
                    }
                }
            }

            // Parse MainScript's section [Variables] into MacroDict
            // (Written by SetMacro, ... ,PERMANENT
            if (project.MainScript.Sections.ContainsKey(ScriptSection.Names.Variables))
            {
                ScriptSection permaSection = project.MainScript.Sections[ScriptSection.Names.Variables];
                Dictionary<string, string> rawDict = IniReadWriter.ParseIniLinesIniStyle(permaSection.Lines);
                foreach (var kv in rawDict)
                {
                    try
                    {
                        if (Regex.Match(kv.Key, MacroNameRegex, RegexOptions.Compiled | RegexOptions.CultureInvariant).Success)
                        { // Macro Name Validation
                            CodeParser parser = new CodeParser(permaSection, Global.Setting, permaSection.Project.Compat);
                            GlobalDict[kv.Key] = parser.ParseStatement(kv.Value);
                        }
                        else
                        {
                            logs.Add(new LogInfo(LogState.Error, $"Invalid macro name [{kv.Key}]"));
                        }
                    }
                    catch (Exception e)
                    {
                        logs.Add(new LogInfo(LogState.Error, e));
                    }
                }
            }
        }
        #endregion

        #region Local Macro
        public List<LogInfo> LoadMacroDict(MacroType type, Script sc, bool append, string sectionName = ScriptSection.Names.Variables)
        {
            if (!sc.Sections.ContainsKey(sectionName))
                return new List<LogInfo>();
            
            ScriptSection section = sc.Sections[sectionName];

            // Pick key-value only if key is not wrapped by %
            Dictionary<string, string> dict = IniReadWriter.ParseIniLinesIniStyle(section.Lines);
            return LoadMacroDict(type, section, dict, append);
        }

        public List<LogInfo> LoadMacroDict(MacroType type, ScriptSection section, IEnumerable<string> lines, bool append)
        {
            Dictionary<string, string> dict = IniReadWriter.ParseIniLinesIniStyle(lines);
            return LoadMacroDict(type, section, dict, append);
        }

        [SuppressMessage("Design", "CA1031:Do not catch general exception types")]
        private List<LogInfo> LoadMacroDict(MacroType type, ScriptSection section, Dictionary<string, string> newDict, bool append)
        {
            List<LogInfo> logs = new List<LogInfo>();
            
            // Select proper macroDict
            Dictionary<string, CodeCommand> macroDict;
            switch (type)
            {
                case MacroType.Global:
                    macroDict = GlobalDict;
                    break;
                case MacroType.Local:
                    macroDict = LocalDict;
                    break;
                default:
                    throw new CriticalErrorException($"Invalid MacroType {type}");
            }

            // Do not clear macroDict in the append mode
            if (!append)
                macroDict.Clear();

            // If the newDict is empty, skip the rest
            if (newDict.Keys.Count == 0)
                return logs;
            
            // Parse and register commands to the macroDict
            int count = 0;
            CodeParser parser = new CodeParser(section, Global.Setting, section.Project.Compat);
            logs.Add(new LogInfo(LogState.Info, $"Import {type} Macro from [{section.Name}]", 0));
            foreach (var kv in newDict)
            {
                try
                {
                    // Macro Name Validation
                    if (!Regex.Match(kv.Key, MacroNameRegex, RegexOptions.Compiled | RegexOptions.CultureInvariant).Success)
                    {
                        logs.Add(new LogInfo(LogState.Error, $"Invalid macro name [{kv.Key}]"));
                        continue;
                    }
                    
                    CodeCommand macroCmd = parser.ParseStatement(kv.Value);
                    macroDict[kv.Key] = macroCmd;
                    logs.Add(new LogInfo(LogState.Success, $"{type} macro [{kv.Key}] set to [{kv.Value}]", 1));
                    count += 1;
                }
                catch (Exception e)
                {
                    logs.Add(new LogInfo(LogState.Error, e));
                }
            }
            logs.Add(new LogInfo(LogState.Info, $"Imported {count} {type} Macro", 0));
            logs.Add(new LogInfo(LogState.None, Logger.LogSeparator, 0));

            return logs;
        }

        public void ResetMacroDict(MacroType type)
        {
            switch (type)
            {
                case MacroType.Global:
                    GlobalDict.Clear();
                    break;
                case MacroType.Local:
                    LocalDict.Clear();
                    break;
                default:
                    throw new CriticalErrorException($"Invalid MacroType {type}");
            }
        }

        /// <summary>
        /// Local Macro from [Variables]
        /// </summary>
        /// <param name="newDict"></param>
        public void SetMacroDict(MacroType type, Dictionary<string, CodeCommand> newDict)
        {
            switch (type)
            {
                case MacroType.Global:
                    GlobalDict = new Dictionary<string, CodeCommand>(newDict, StringComparer.OrdinalIgnoreCase);
                    break;
                case MacroType.Local:
                    LocalDict = new Dictionary<string, CodeCommand>(newDict, StringComparer.OrdinalIgnoreCase);
                    break;
                default:
                    throw new CriticalErrorException($"Invalid MacroType {type}");
            }
        }

        public Dictionary<string, CodeCommand> GetMacroDict(MacroType type)
        {
            Dictionary<string, CodeCommand> macroDict;
            switch (type)
            {
                case MacroType.Global:
                    macroDict = new Dictionary<string, CodeCommand>(GlobalDict, StringComparer.OrdinalIgnoreCase);
                    break;
                case MacroType.Local:
                    macroDict = new Dictionary<string, CodeCommand>(LocalDict, StringComparer.OrdinalIgnoreCase);
                    break;
                default:
                    throw new CriticalErrorException($"Invalid MacroType {type}");
            }
            return macroDict;
        }
        #endregion

        #region SetMacro
        public LogInfo SetMacro(string macroName, string macroCommand, ScriptSection section, bool global, bool permanent)
        {
            // Macro Name Validation
            if (!Regex.Match(macroName, MacroNameRegex, RegexOptions.Compiled | RegexOptions.CultureInvariant).Success)
                return new LogInfo(LogState.Error, $"Invalid macro name [{macroName}]");

            if (macroCommand != null)
            { // Insert
                // Try parsing
                CodeParser parser = new CodeParser(section, Global.Setting, section.Project.Compat);
                CodeCommand cmd = parser.ParseStatement(macroCommand);
                if (cmd.Type == CodeType.Error)
                {
                    CodeInfo_Error info = cmd.Info.Cast<CodeInfo_Error>();
                    return new LogInfo(LogState.Error, info.ErrorMessage);
                }

                // Put into dictionary
                if (permanent) // MacroDict
                {
                    GlobalDict[macroName] = cmd;
                    if (IniReadWriter.WriteKey(section.Project.MainScript.RealPath, ScriptSection.Names.Variables, macroName, cmd.RawCode))
                        return new LogInfo(LogState.Success, $"Permanent Macro [{macroName}] set to [{cmd.RawCode}]");
                    else
                        return new LogInfo(LogState.Error, $"Could not write macro into [{section.Project.MainScript.RealPath}]");
                }

                if (global) // MacroDict
                {
                    GlobalDict[macroName] = cmd;
                    return new LogInfo(LogState.Success, $"Global Macro [{macroName}] set to [{cmd.RawCode}]");
                }

                LocalDict[macroName] = cmd;
                return new LogInfo(LogState.Success, $"Local Macro [{macroName}] set to [{cmd.RawCode}]");
            }
            else
            {
                // Delete
                // Put into dictionary
                if (permanent) // MacroDict
                {
                    if (GlobalDict.ContainsKey(macroName))
                    {
                        GlobalDict.Remove(macroName);
                        IniReadWriter.DeleteKey(section.Project.MainScript.RealPath, ScriptSection.Names.Variables, macroName);
                        return new LogInfo(LogState.Success, $"Permanent Macro [{macroName}] deleted");
                    }

                    return new LogInfo(LogState.Error, $"Permanent Macro [{macroName}] not found");
                }

                if (global) // MacroDict
                {
                    if (GlobalDict.ContainsKey(macroName))
                    {
                        GlobalDict.Remove(macroName);
                        return new LogInfo(LogState.Success, $"Global Macro [{macroName}] deleted");
                    }

                    return new LogInfo(LogState.Error, $"Global Macro [{macroName}] not found");
                }

                // LocalDict
                if (LocalDict.ContainsKey(macroName))
                {
                    LocalDict.Remove(macroName);
                    return new LogInfo(LogState.Success, $"Local Macro [{macroName}] deleted");
                }

                return new LogInfo(LogState.Error, $"Local Macro [{macroName}] not found");
            }
        }
        #endregion
    }
}
