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

        public bool MacroEnabled { get => macroEnabled; }
        public Plugin MacroPlugin { get => macroPlugin; }
        public PluginSection MacroSection { get => macroSection; }
        public Dictionary<string, CodeCommand> MacroDict { get => macroDict; } // Macro Library - [ApiVar]

        public Macro(Project project, Variables variables, out List<LogInfo> results)
        {
            macroEnabled = true;
            results = new List<LogInfo>();
            if (project.MainPlugin.Sections.ContainsKey("Variables") == false)
            { 
                macroEnabled = false;
                results.Add(new LogInfo(LogState.Info, "Macro not defined"));
                return;
            }

            Dictionary<string, string> varDict = project.MainPlugin.Sections["Variables"].GetIniDict();
            if (varDict.ContainsKey("API") && varDict.ContainsKey("APIVAR") == false)
            {
                macroEnabled = false;
                results.Add(new LogInfo(LogState.Info, "Macro not defined"));
                return;
            }

            // Get macroPlugin 
            string rawPluginPath = varDict["API"];
            string macroPluginPath = variables.Expand(varDict["API"]); // Need Expansion
            macroPlugin = project.AllPluginList.Find(x => string.Equals(x.FullPath, macroPluginPath, StringComparison.OrdinalIgnoreCase));
            if (macroPlugin == null)
            {
                macroEnabled = false;
                results.Add(new LogInfo(LogState.Error, $"Macro defined but unable to find macro plugin [{rawPluginPath}"));
                return;
            }

            // Get macroPlugin
            if (macroPlugin.Sections.ContainsKey(varDict["APIVAR"]) == false)
            {
                macroEnabled = false;
                results.Add(new LogInfo(LogState.Error, $"Macro defined but unable to find macro section [{varDict["APIVAR"]}"));
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
            Dictionary<string, string> macroRawDict = Ini.ParseIniLinesIniStyle(macroSection.GetLines());
            foreach (var kv in macroRawDict)
            {
                try
                {
                    SectionAddress addr = new SectionAddress(macroPlugin, macroSection);
                    if (kv.Key.StartsWith("%", StringComparison.Ordinal) == false
                        && kv.Key.EndsWith("%", StringComparison.Ordinal) == false)
                        macroDict[kv.Key] = CodeParser.ParseOneRawLine(kv.Value, addr);
                }
                catch (Exception e)
                {
                    results.Add(new LogInfo(LogState.Error, e));
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
            try
            {
                macroCmd = s.Macro.MacroDict[info.MacroType];
                macroCmd.RawCode = cmd.RawCode;
            }
            catch (KeyNotFoundException)
            {
                throw new CodeCommandException($"Invalid Command [{info.MacroType}]", cmd);
            }

            Dictionary<int, string> paramDict = new Dictionary<int, string>();
            for (int i = 0; i < info.Args.Count; i++)
                paramDict[i + 1] = StringEscaper.Preprocess(s, info.Args[i]);

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
