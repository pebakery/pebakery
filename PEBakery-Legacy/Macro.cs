using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace BakeryEngine_Legacy
{
    using StringDictionary = Dictionary<string, string>;
    using CommandDictionary = Dictionary<string, BakeryCommand>;

    public class Macro
    {
        private bool macroEnabled; // Can use macro or not?
        public bool MacroEnabled { get { return macroEnabled; } }
        private Plugin macroPlugin; // sciprt.project - %API%
        public Plugin MacroPlugin { get { return macroPlugin; } }
        private PluginSection macroSection; // sciprt.project - %APIVAR%
        public PluginSection MacroSection { get { return macroSection; } }
        private CommandDictionary macroDict; // Macro Library - [ApiVar]
        private CommandDictionary MacroDict { get { return macroDict; } } // Macro Library - [ApiVar]
        private Logger logger;

        public Macro(Project project, BakeryVariables variables, Logger logger)
        {
            this.logger = logger;

            macroEnabled = true;
            if (project.MainPlugin.Sections.ContainsKey("Variables") == false)
            { 
                macroEnabled = false;
                logger.Write(new LogInfo(LogState.Info, "Macro not defined"));
                return;
            }
            
            StringDictionary varDict = project.MainPlugin.Sections["Variables"].GetIniDict();
            if (varDict.ContainsKey("API") && varDict.ContainsKey("APIVAR") == false)
            {
                macroEnabled = false;
                logger.Write(new LogInfo(LogState.Info, "Macro not defined"));
                return;
            }

            // Get macroPlugin 
            string rawPluginPath = varDict["API"];
            string macroPluginPath = variables.Expand(varDict["API"]); // Need Expansion
            try
            {
                macroPlugin = project.AllPlugins.SearchByFullPath(macroPluginPath);
            }
            catch (PluginNotFoundException)
            {
                macroEnabled = false;
                logger.Write(new LogInfo(LogState.Error, $"Macro defined but unable to find macro plugin [{rawPluginPath}"));
                return;
            }

            // Get macroPlugin
            if (macroPlugin.Sections.ContainsKey(varDict["APIVAR"]) == false)
            {
                macroEnabled = false;
                logger.Write(new LogInfo(LogState.Error, $"Macro defined but unable to find macro section [{varDict["APIVAR"]}"));
                return;
            }
            macroSection = macroPlugin.Sections[varDict["APIVAR"]];

            macroDict = new CommandDictionary(StringComparer.OrdinalIgnoreCase);
            StringDictionary macroRawDict = IniFile.ParseLinesIniStyle(macroSection.GetLines());
            foreach (var kv in macroRawDict)
            {
                try
                {
                    BakeryCommand cmd = BakeryCodeParser.ParseOneCommand(kv.Value);
                    if (cmd.Opcode == Opcode.Macro)
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
