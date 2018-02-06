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

    Additional permission under GNU GPL version 3 section 7

    If you modify this program, or any covered work, by linking
    or combining it with external libraries, containing parts
    covered by the terms of various license, the licensors of
    this program grant you additional permission to convey the
    resulting work. An external library is a library which is
    not derived from or based on this program. 
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using PEBakery.IniLib;
using PEBakery.Exceptions;
using PEBakery.Core.Commands;
using System.Windows;
using PEBakery.WPF;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace PEBakery.Core
{
    public class Macro
    {
        private bool macroEnabled; // Can use macro or not?
        private Script macroScript; // sciprt.project - %API%
        private ScriptSection macroSection; // sciprt.project - %APIVAR%
        private Dictionary<string, CodeCommand> macroDict = new Dictionary<string, CodeCommand>(StringComparer.OrdinalIgnoreCase); // Macro Library - [ApiVar]
        private Dictionary<string, CodeCommand> localDict = new Dictionary<string, CodeCommand>(StringComparer.OrdinalIgnoreCase); // Local Macro from [Variables]

        public bool MacroEnabled => macroEnabled;
        public Script MacroScript => macroScript;
        public ScriptSection MacroSection => macroSection;
        public Dictionary<string, CodeCommand> MacroDict => macroDict; // Macro Library - [ApiVar]
        public Dictionary<string, CodeCommand> LocalDict => localDict;

        public const string MacroNameRegex = @"^([a-zA-Z0-9_]+)$";

        public Macro(Project project, Variables variables, out List<LogInfo> logs)
        { 
            macroEnabled = true;
            logs = new List<LogInfo>();
            if (project.MainScript.Sections.ContainsKey("Variables") == false)
            {
                macroEnabled = false;
                logs.Add(new LogInfo(LogState.Info, "Macro not defined"));
                return;
            }

            Dictionary<string, string> varDict = Ini.ParseIniLinesVarStyle(project.MainScript.Sections["Variables"].GetLines());
            if ((varDict.ContainsKey("API") && varDict.ContainsKey("APIVAR")) == false)
            {
                macroEnabled = false;
                logs.Add(new LogInfo(LogState.Info, "Macro not defined"));
                return;
            }

            // Get macroScript
            string rawScriptPath = varDict["API"];
            string macroScriptPath = variables.Expand(varDict["API"]); // Need Expansion
            macroScript = project.AllScripts.Find(x => x.FullPath.Equals(macroScriptPath, StringComparison.OrdinalIgnoreCase));
            if (macroScript == null)
            {
                macroEnabled = false;
                logs.Add(new LogInfo(LogState.Error, $"Macro defined but unable to find macro script [{rawScriptPath}"));
                return;
            }

            // Get macroScript
            if (macroScript.Sections.ContainsKey(varDict["APIVAR"]) == false)
            {
                macroEnabled = false;
                logs.Add(new LogInfo(LogState.Error, $"Macro defined but unable to find macro section [{varDict["APIVAR"]}"));
                return;
            }
            macroSection = macroScript.Sections[varDict["APIVAR"]];
            variables.SetValue(VarsType.Global, "API", macroScriptPath);
            if (macroScript.Sections.ContainsKey("Variables"))
                variables.AddVariables(VarsType.Global, macroScript.Sections["Variables"]);

            // Import Section [APIVAR]'s variables, such as '%Shc_Mode%=0'
            variables.AddVariables(VarsType.Global, macroSection);

            // Parse Section [APIVAR] into MacroDict
            {
                SectionAddress addr = new SectionAddress(macroScript, macroSection);
                Dictionary<string, string> rawDict = Ini.ParseIniLinesIniStyle(macroSection.GetLines());
                foreach (var kv in rawDict)
                {
                    try
                    {
                        if (Regex.Match(kv.Key, Macro.MacroNameRegex, RegexOptions.Compiled | RegexOptions.CultureInvariant).Success) // Macro Name Validation
                            macroDict[kv.Key] = CodeParser.ParseStatement(kv.Value, addr);
                        else
                            logs.Add(new LogInfo(LogState.Error, $"Invalid macro name [{kv.Key}]"));
                    }
                    catch (Exception e)
                    {
                        logs.Add(new LogInfo(LogState.Error, e));
                    }
                }
            }
            
            // Parse MainScript's section [Variables] into MacroDict
            // (Written by SetMacro, ... ,PERMANENT
            if (project.MainScript.Sections.ContainsKey("Variables"))
            {
                ScriptSection permaSection = project.MainScript.Sections["Variables"];
                SectionAddress addr = new SectionAddress(project.MainScript, permaSection);
                Dictionary<string, string> rawDict = Ini.ParseIniLinesIniStyle(permaSection.GetLines());
                foreach (var kv in rawDict)
                {
                    try
                    {
                        if (Regex.Match(kv.Key, Macro.MacroNameRegex, RegexOptions.Compiled | RegexOptions.CultureInvariant).Success) // Macro Name Validation
                            macroDict[kv.Key] = CodeParser.ParseStatement(kv.Value, addr);
                        else
                            logs.Add(new LogInfo(LogState.Error, $"Invalid macro name [{kv.Key}]"));
                    }
                    catch (Exception e)
                    {
                        logs.Add(new LogInfo(LogState.Error, e));
                    }
                }
            }
            
        }

        public List<LogInfo> LoadLocalMacroDict(Script p, bool append, string sectionName = "Variables")
        {
            if (p.Sections.ContainsKey(sectionName))
            {
                ScriptSection section = p.Sections[sectionName];

                // [Variables]'s type is SectionDataType.Lines
                // Pick key-value only if key is not wrapped by %
                SectionAddress addr = new SectionAddress(p, section);
                Dictionary<string, string> dict = Ini.ParseIniLinesIniStyle(section.GetLines());
                return LoadLocalMacroDict(addr, dict, append);
            }
            else
            {
                return new List<LogInfo>();
            }
        }

        public List<LogInfo> LoadLocalMacroDict(SectionAddress addr, IEnumerable<string> lines, bool append)
        {
            Dictionary<string, string> dict = Ini.ParseIniLinesIniStyle(lines);
            return LoadLocalMacroDict(addr, dict, append);
        }

        private List<LogInfo> LoadLocalMacroDict(SectionAddress addr, Dictionary<string, string> dict, bool append)
        {
            List<LogInfo> logs = new List<LogInfo>();
            if (append == false)
                localDict.Clear();

            if (0 < dict.Keys.Count)
            {
                int count = 0;
                logs.Add(new LogInfo(LogState.Info, $"Import Local Macro from [{addr.Section.SectionName}]", 0));
                foreach (var kv in dict)
                {
                    try
                    {
                        if (Regex.Match(kv.Key, Macro.MacroNameRegex, RegexOptions.Compiled | RegexOptions.CultureInvariant).Success == false) // Macro Name Validation
                        {
                            logs.Add(new LogInfo(LogState.Error, $"Invalid local macro name [{kv.Key}]"));
                            continue;
                        }

                        localDict[kv.Key] = CodeParser.ParseStatement(kv.Value, addr);
                        logs.Add(new LogInfo(LogState.Success, $"Local macro [{kv.Key}] set to [{kv.Value}]", 1));
                        count += 1;
                    }
                    catch (Exception e)
                    {
                        logs.Add(new LogInfo(LogState.Error, e));
                    }
                }
                logs.Add(new LogInfo(LogState.Info, $"Imported {count} Local Macro", 0));
                logs.Add(new LogInfo(LogState.None, Logger.LogSeperator, 0));
            }

            return logs;
        }

        public void ResetLocalMacros()
        {
            localDict = new Dictionary<string, CodeCommand>(StringComparer.OrdinalIgnoreCase);
        }

        public void SetLocalMacros(Dictionary<string, CodeCommand> newDict)
        { // Local Macro from [Variables]
            localDict = new Dictionary<string, CodeCommand>(newDict, StringComparer.OrdinalIgnoreCase);
        }

        public LogInfo SetMacro(string macroName, string macroCommand, SectionAddress addr, bool global, bool permanent)
        {
            if (Regex.Match(macroName, Macro.MacroNameRegex, RegexOptions.Compiled | RegexOptions.CultureInvariant).Success == false) // Macro Name Validation
                return new LogInfo(LogState.Error, $"Invalid macro name [{macroName}]");

            if (macroCommand != null)
            { // Insert
                // Try parsing
                CodeCommand cmd = CodeParser.ParseStatement(macroCommand, addr);
                if (cmd.Type == CodeType.Error)
                {
                    Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Error));
                    CodeInfo_Error info = cmd.Info as CodeInfo_Error;

                    return new LogInfo(LogState.Error, info.ErrorMessage);
                }

                // Put into dictionary
                if (permanent) // MacroDict
                {
                    MacroDict[macroName] = cmd;
                    if (Ini.SetKey(addr.Project.MainScript.FullPath, "Variables", macroName, cmd.RawCode))
                        return new LogInfo(LogState.Success, $"Permanent Macro [{macroName}] set to [{cmd.RawCode}]");
                    else
                        return new LogInfo(LogState.Error, $"Could not write macro into [{addr.Project.MainScript.FullPath}]");
                }
                else if (global) // MacroDict
                {
                    MacroDict[macroName] = cmd;
                    return new LogInfo(LogState.Success, $"Global Macro [{macroName}] set to [{cmd.RawCode}]");
                }
                else
                {
                    LocalDict[macroName] = cmd;
                    return new LogInfo(LogState.Success, $"Local Macro [{macroName}] set to [{cmd.RawCode}]");
                }
            }
            else
            { // Delete
                // Put into dictionary
                if (permanent) // MacroDict
                {
                    if (MacroDict.ContainsKey(macroName))
                    {
                        MacroDict.Remove(macroName);
                        Ini.DeleteKey(addr.Project.MainScript.FullPath, "Variables", macroName);
                        return new LogInfo(LogState.Success, $"Permanent Macro [{macroName}] deleted");
                    }
                    else
                    {
                        return new LogInfo(LogState.Error, $"Permanent Macro [{macroName}] not found");
                    }                   
                }
                else if (global) // MacroDict
                {
                    if (MacroDict.ContainsKey(macroName))
                    {
                        MacroDict.Remove(macroName);
                        return new LogInfo(LogState.Success, $"Global Macro [{macroName}] deleted");
                    }
                    else
                    {
                        return new LogInfo(LogState.Error, $"Global Macro [{macroName}] not found");
                    }
                }
                else // LocalDict
                {
                    if (LocalDict.ContainsKey(macroName))
                    {
                        LocalDict.Remove(macroName);
                        return new LogInfo(LogState.Success, $"Local Macro [{macroName}] deleted");
                    }
                    else
                    {
                        return new LogInfo(LogState.Error, $"Local Macro [{macroName}] not found");
                    }
                }
            }
        }
    }

    public static class CommandMacro
    {
        public static void Macro(EngineState s, CodeCommand cmd)
        {
            CodeInfo_Macro info = cmd.Info as CodeInfo_Macro;
            if (info == null)
                throw new InvalidCodeCommandException("Command [Macro] should have [CodeInfo_Macro]", cmd);

            CodeCommand macroCmd;
            if (s.Macro.MacroDict.ContainsKey(info.MacroType))
            {
                macroCmd = s.Macro.MacroDict[info.MacroType];
                macroCmd.RawCode = cmd.RawCode;
            }
            else if (s.Macro.LocalDict.ContainsKey(info.MacroType))
            { // Try to find [infoMacroType] in [Variables] <- I hate undocumented behaviors!
                macroCmd = s.Macro.LocalDict[info.MacroType];
                macroCmd.RawCode = cmd.RawCode;
            }
            else 
            { 
                throw new CodeCommandException($"Invalid Command [{info.MacroType}]", cmd);
            }

            Dictionary<int, string> paramDict = new Dictionary<int, string>();
            for (int i = 0; i < info.Args.Count; i++)
                paramDict[i + 1] = StringEscaper.ExpandSectionParams(s, info.Args[i]);

            s.CurSectionParams = paramDict;

            if (s.LogMacro)
            {
                s.InMacro = true;
                CommandBranch.RunExec(s, macroCmd, true);
                s.InMacro = false;
            }
            else // Do not log macro
            {
                s.Logger.Build_Write(s, new LogInfo(LogState.Info, $"Macro [{info.MacroType}] ({cmd.RawCode})", s.CurDepth + 1));
                s.Logger.TurnOff.Push(true);
                CommandBranch.RunExec(s, macroCmd, true);
                s.Logger.TurnOff.TryPop(out bool dummy);
            }
        }
    }
}
