using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using PEBakery.Lib;
using PEBakery.Exceptions;
using PEBakery.Core.Commands;
using System.Windows;
using PEBakery.WPF;

namespace PEBakery.Core
{
    public class Macro
    {
        private bool macroEnabled; // Can use macro or not?
        private Plugin macroPlugin; // sciprt.project - %API%
        private PluginSection macroSection; // sciprt.project - %APIVAR%
        private Dictionary<string, CodeCommand> macroDict; // Macro Library - [ApiVar]
        private Dictionary<string, CodeCommand> localDict; // Local Macro from [Variables]

        public bool MacroEnabled { get => macroEnabled; }
        public Plugin MacroPlugin { get => macroPlugin; }
        public PluginSection MacroSection { get => macroSection; }
        public Dictionary<string, CodeCommand> MacroDict { get => macroDict; } // Macro Library - [ApiVar]
        public Dictionary<string, CodeCommand> LocalDict { get => localDict; } // Local Macro from [Variables]

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
            macroPlugin = project.AllPluginList.Find(x => string.Equals(x.FullPath, macroPluginPath, StringComparison.OrdinalIgnoreCase));
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
            if (macroPlugin.Sections.ContainsKey("Varaibles"))
                variables.AddVariables(VarsType.Global, macroPlugin.Sections["Varaibles"]);

            // Import Section [APIVAR]'s variables, such as '%Shc_Mode%=0'
            variables.AddVariables(VarsType.Global, macroSection);

            // Parse Section [APIVAR] into dictionary of CodeCommand
            macroDict = new Dictionary<string, CodeCommand>(StringComparer.OrdinalIgnoreCase);
            SectionAddress addr = new SectionAddress(macroPlugin, macroSection);
            Dictionary<string, string> macroRawDict = Ini.ParseIniLinesIniStyle(macroSection.GetLines());
            foreach (var kv in macroRawDict)
            {
                try
                {
                    if (kv.Key.StartsWith("%", StringComparison.Ordinal) == false
                        && kv.Key.EndsWith("%", StringComparison.Ordinal) == false)
                        macroDict[kv.Key] = CodeParser.ParseOneRawLine(kv.Value, addr);
                }
                catch (Exception e)
                {
                    logs.Add(new LogInfo(LogState.Error, e));
                }
            }

            // Prepare Local Macro Dict
            localDict = new Dictionary<string, CodeCommand>(StringComparer.OrdinalIgnoreCase);
        }

        public List<LogInfo> LoadLocalMacroDict(Plugin p)
        {
            List<LogInfo> logs = new List<LogInfo>();
            localDict.Clear();

            if (p.Sections.ContainsKey("Variables"))
            {
                PluginSection section = p.Sections["Variables"];
                
                // [Variables]'s type is SectionDataType.Lines
                // Pick key-value only if key is not wrapped by %
                Dictionary<string, string> dict =
                    Ini.ParseIniLinesIniStyle(section.GetLines())
                    .Where(x => !(x.Key.StartsWith("%", StringComparison.Ordinal) && x.Key.EndsWith("%", StringComparison.Ordinal)))
                    .ToDictionary(x => x.Key, x => x.Value);

                if (0 < dict.Keys.Count)
                {
                    SectionAddress addr = new SectionAddress(p, section);

                    int count = 0;
                    logs.Add(new LogInfo(LogState.Info, "Import Macro from [Variables]", 0));
                    foreach (var kv in dict)
                    {
                        try
                        {
                            localDict[kv.Key] = CodeParser.ParseOneRawLine(kv.Value, addr);
                            logs.Add(new LogInfo(LogState.Success, $"Macro [{kv.Key}] set to [{kv.Value}]", 1));
                            count += 1;
                        }
                        catch (Exception e)
                        {
                            logs.Add(new LogInfo(LogState.Error, e));
                        }
                    }
                    logs.Add(new LogInfo(LogState.Info, $"Imported {count} Macro", 0));
                    logs.Add(new LogInfo(LogState.None, Logger.LogSeperator, 0));
                }
            }

            return logs;
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
                CommandBranch.RunExec(s, macroCmd, true);
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
