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
        /// Run,%ScriptFile%,<Section>[,PARAMS]
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
            string rawPluginFile = cmd.Operands[0];
            string sectionName = EscapeString(variables.Expand(cmd.Operands[1]));
            string rawSectoinName = cmd.Operands[1];

            // Get optional operand 
            string[] parameters = new string[cmd.Operands.Length - necessaryOperandNum];
            if (necessaryOperandNum < cmd.Operands.Length)
                Array.Copy(cmd.Operands, 2, parameters, 0, cmd.Operands.Length - necessaryOperandNum);

            bool inCurrentPlugin = false;
            if (String.Equals(rawPluginFile, "%PluginFile%", StringComparison.OrdinalIgnoreCase))
                inCurrentPlugin = true;
            else if (String.Equals(rawPluginFile, "%ScriptFile%", StringComparison.OrdinalIgnoreCase))
                inCurrentPlugin = true;

            if (inCurrentPlugin)
            {
                if (!currentPlugin.Sections.ContainsKey(sectionName))
                    throw new InvalidOperandException($"[{Path.GetFileName(pluginFile)}] does not have section [{sectionName}]", cmd);

                // Branch to new section
                returnAddress.Push(new CommandAddress(cmd.Address.plugin, cmd.Address.section, cmd.Address.line + 1, cmd.Address.secLength));
                nextCommand = new CommandAddress(currentPlugin, currentPlugin.Sections[sectionName], 0, currentPlugin.Sections[sectionName].Count);
                currentSectionParams = parameters;

                // Exec utilizes [Variables] section of the plugin
                if (cmd.Opcode == Opcode.Exec)
                {

                }
            }

            cmd.SectionDepth += 1; // For proper log indentation
            logs.Add(new LogInfo(cmd, LogState.Success, $"Processing section [{sectionName}]"));

            return logs.ToArray();
        }
    }
}