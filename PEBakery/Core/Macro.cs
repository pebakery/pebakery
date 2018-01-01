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
        private Plugin macroPlugin; // sciprt.project - %API%
        private PluginSection macroSection; // sciprt.project - %APIVAR%
        private Dictionary<string, CodeCommand> macroDict = new Dictionary<string, CodeCommand>(StringComparer.OrdinalIgnoreCase); // Macro Library - [ApiVar]
        private Dictionary<string, CodeCommand> localDict = new Dictionary<string, CodeCommand>(StringComparer.OrdinalIgnoreCase); // Local Macro from [Variables]

        public bool MacroEnabled => macroEnabled;
        public Plugin MacroPlugin => macroPlugin;
        public PluginSection MacroSection => macroSection;
        public Dictionary<string, CodeCommand> MacroDict => macroDict; // Macro Library - [ApiVar]
        public Dictionary<string, CodeCommand> LocalDict => localDict;

        public const string MacroNameRegex = @"^([a-zA-Z0-9_]+)$";

        public Macro(Project project, Variables variables, out List<LogInfo> logs)
        { 
            macroEnabled = true;
            logs = new List<LogInfo>();
            if (project.MainPlugin.Sections.ContainsKey("Variables") == false)
            {
                macroEnabled = false;
                logs.Add(new LogInfo(LogState.Info, "Macro not defined"));
                return;
            }

            Dictionary<string, string> varDict = Ini.ParseIniLinesVarStyle(project.MainPlugin.Sections["Variables"].GetLines());
            if ((varDict.ContainsKey("API") && varDict.ContainsKey("APIVAR")) == false)
            {
                macroEnabled = false;
                logs.Add(new LogInfo(LogState.Info, "Macro not defined"));
                return;
            }

            // Get macroPlugin 
            string rawPluginPath = varDict["API"];
            string macroPluginPath = variables.Expand(varDict["API"]); // Need Expansion
            macroPlugin = project.AllPlugins.Find(x => string.Equals(x.FullPath, macroPluginPath, StringComparison.OrdinalIgnoreCase));
            if (macroPlugin == null)
            {
                macroEnabled = false;
                logs.Add(new LogInfo(LogState.Error, $"Macro defined but unable to find macro plugin [{rawPluginPath}"));
                return;
            }

            // Get macroPlugin
            if (macroPlugin.Sections.ContainsKey(varDict["APIVAR"]) == false)
            {
                macroEnabled = false;
                logs.Add(new LogInfo(LogState.Error, $"Macro defined but unable to find macro section [{varDict["APIVAR"]}"));
                return;
            }
            macroSection = macroPlugin.Sections[varDict["APIVAR"]];
            variables.SetValue(VarsType.Global, "API", macroPluginPath);
            if (macroPlugin.Sections.ContainsKey("Variables"))
                variables.AddVariables(VarsType.Global, macroPlugin.Sections["Variables"]);

            // Import Section [APIVAR]'s variables, such as '%Shc_Mode%=0'
            variables.AddVariables(VarsType.Global, macroSection);

            // Parse Section [APIVAR] into MacroDict
            {
                SectionAddress addr = new SectionAddress(macroPlugin, macroSection);
                Dictionary<string, string> rawDict = Ini.ParseIniLinesIniStyle(macroSection.GetLines());
                foreach (var kv in rawDict)
                {
                    try
                    {
                        if (Regex.Match(kv.Key, Macro.MacroNameRegex, RegexOptions.Compiled).Success) // Macro Name Validation
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
            
            // Parse MainPlugin's section [Variables] into MacroDict
            // (Written by SetMacro, ... ,PERMANENT
            if (project.MainPlugin.Sections.ContainsKey("Variables"))
            {
                PluginSection permaSection = project.MainPlugin.Sections["Variables"];
                SectionAddress addr = new SectionAddress(project.MainPlugin, permaSection);
                Dictionary<string, string> rawDict = Ini.ParseIniLinesIniStyle(permaSection.GetLines());
                foreach (var kv in rawDict)
                {
                    try
                    {
                        if (Regex.Match(kv.Key, Macro.MacroNameRegex, RegexOptions.Compiled).Success) // Macro Name Validation
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

        public List<LogInfo> LoadLocalMacroDict(Plugin p, bool append, string sectionName = "Variables")
        {
            if (p.Sections.ContainsKey(sectionName))
            {
                PluginSection section = p.Sections[sectionName];

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
                        if (Regex.Match(kv.Key, Macro.MacroNameRegex, RegexOptions.Compiled).Success == false) // Macro Name Validation
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
            if (Regex.Match(macroName, Macro.MacroNameRegex, RegexOptions.Compiled).Success == false) // Macro Name Validation
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
                    if (Ini.SetKey(addr.Project.MainPlugin.FullPath, "Variables", macroName, cmd.RawCode))
                        return new LogInfo(LogState.Success, $"Permanent Macro [{macroName}] set to [{cmd.RawCode}]");
                    else
                        return new LogInfo(LogState.Error, $"Could not write macro into [{addr.Project.MainPlugin.FullPath}]");
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
                        Ini.DeleteKey(addr.Project.MainPlugin.FullPath, "Variables", macroName);
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
