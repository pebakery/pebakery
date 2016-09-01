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
        public LogInfo[] Run(BakeryCommand cmd)
        {
            ArrayList logs = new ArrayList();

            // Necessary operand : 2, optional operand : variable length
            const int necessaryOperandNum = 2;
            if (cmd.Operands.Length < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);

            // Get necesssary operand
            string pluginFile = variables.Expand(cmd.Operands[0]);
            string rawPluginFile = cmd.Operands[0];
            string sectionName = variables.Expand(cmd.Operands[1]);
            string rawSectoinName = cmd.Operands[1];

            // Get optional operand 
            string[] parameters = new string[cmd.Operands.Length - necessaryOperandNum];
            if (necessaryOperandNum < cmd.Operands.Length)
                Array.Copy(cmd.Operands, 2, parameters, 0, cmd.Operands.Length - necessaryOperandNum);

            bool currentPlugin = false;
            if (String.Equals(rawPluginFile, "%PluginFile%", StringComparison.OrdinalIgnoreCase))
                currentPlugin = true;
            else if (String.Equals(rawPluginFile, "%ScriptFile%", StringComparison.OrdinalIgnoreCase))
                currentPlugin = true;

            if (currentPlugin)
            {
                if (!plugin.Sections.ContainsKey(sectionName))
                    throw new InvalidOperandException(string.Concat("'", Path.GetFileName(pluginFile), "' does not have section '", sectionName, "'"), cmd);

                // Branch to new section
                CommandAddress retCmdAddr = new CommandAddress(cmd.Address.section, cmd.Address.line + 1, cmd.Address.secLength);
                returnAddress.Push(new ReturnAddressInfo(retCmdAddr, cmd.Address.section.SecName));
                nextCommand = new CommandAddress(plugin.Sections[sectionName], 0, plugin.Sections[sectionName].SecCodes.Length);
            }

            cmd.SectionDepth++;
            logs.Add(new LogInfo(cmd, string.Concat("Running section '", sectionName, "'"), LogState.Success));

            return logs.ToArray(typeof(LogInfo)) as LogInfo[];
        }

    }
}