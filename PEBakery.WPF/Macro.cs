using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using PEBakery.Lib;
using PEBakery.Exceptions;

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

            macroDict = new Dictionary<string, CodeCommand>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> macroRawDict = Ini.ParseLinesIniStyle(macroSection.GetLines());
            foreach (var kv in macroRawDict)
            {
                try
                {
                    SectionAddress addr = new SectionAddress(macroPlugin, macroSection);
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

            CodeCommand macroCmd = s.Macro.MacroDict[info.MacroType];
            s.CurSectionParams = info.Args;
            CommandBranch.RunExec(s, macroCmd, true);
            // List<CodeCommand> codeList = new List<CodeCommand>() {  };
            // Engine.RunCommands(s, codeList, info.Args, info.Depth + 1, false, false);
        }
    }
}
