using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections;

namespace BakeryEngine
{
    /// <summary>
    /// Implementation of commands
    /// </summary>
    public partial class BakeryEngine
    {
        /// <summary>
        /// Run,%PluginFile%,<Section>[,PARAMS]
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public LogInfo[] RunExec(BakeryCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 2, optional operand : variable length
            const int necessaryOperandNum = 2;
            if (cmd.Operands.Length < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);

            // Get necesssary operand
            string pluginFile = EscapeString(variables.Expand(cmd.Operands[0]));
            string sectionName = EscapeString(variables.Expand(cmd.Operands[1]));
            string rawPluginFile = cmd.Operands[0];
            string rawSectoinName = cmd.Operands[1];

            // Get optional operand 
            string[] parameters = new string[cmd.Operands.Length - necessaryOperandNum];
            if (necessaryOperandNum < cmd.Operands.Length)
                Array.Copy(cmd.Operands, 2, parameters, 0, cmd.Operands.Length - necessaryOperandNum);

            bool inCurrentPlugin = false;
            if (string.Equals(rawPluginFile, "%PluginFile%", StringComparison.OrdinalIgnoreCase))
                inCurrentPlugin = true;
            else if (string.Equals(rawPluginFile, "%ScriptFile%", StringComparison.OrdinalIgnoreCase))
                inCurrentPlugin = true;

            Plugin targetPlugin;
            if (inCurrentPlugin)
                targetPlugin = currentPlugin;
            else
                targetPlugin = project.ActivePlugins.SearchByFullPath(variables.Expand(pluginFile));

            // Does section exists?
            if (!targetPlugin.Sections.ContainsKey(sectionName))
                throw new InvalidOperandException($"[{rawPluginFile}] does not have section [{sectionName}]", cmd);

            // Branch to new section
            returnAddress.Push(new CommandAddress(cmd.Address.plugin, cmd.Address.section, cmd.Address.line + 1, cmd.Address.secLength));
            nextCommand = new CommandAddress(targetPlugin, targetPlugin.Sections[sectionName], 0, targetPlugin.Sections[sectionName].Count);
            currentSectionParams = parameters;

            cmd.SectionDepth += 1; // For proper log indentation
            if (inCurrentPlugin)
                logs.Add(new LogInfo(cmd, LogState.Success, $"Processing section [{sectionName}]"));
            else
                logs.Add(new LogInfo(cmd, LogState.Success, $"Processing [{rawPluginFile}]'s section [{sectionName}]"));

            // Exec utilizes [Variables] section of the plugin
            if (cmd.Opcode == Opcode.Exec)
                variables.AddVariables(VarsType.Local, targetPlugin.Sections[sectionName], returnAddress.Count, true);            

            return logs.ToArray();
        }
    }
}