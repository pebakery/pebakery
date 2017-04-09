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

            macroDict = new Dictionary<string, CodeCommand>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> macroRawDict = Ini.ParseLinesIniStyle(macroSection.GetLines());
            foreach (var kv in macroRawDict)
            {
                try
                {
                    SectionAddress addr = new SectionAddress(macroPlugin, macroSection);
                    CodeCommand cmd = CodeParser.ParseOneRawLine(kv.Value, addr);
                    if (cmd.Type == CodeType.Macro)
                    { // Cannot use Macro in Macro!
                    }
                    macroDict[kv.Key] = cmd;
                }
                catch
                {
                    // Do nothing
                    // TODO: leave error to log
                }
            }
            
        }
    }
}
