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
        /// Set,<VarName>,<VarValue>[,GLOBAL | PERMANENT] 
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public LogInfo[] Set(BakeryCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 2, optional operand : 1
            const int necessaryOperandNum = 2;
            const int optionalOperandNum = 1;
            if (cmd.Operands.Length < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);
            else if (necessaryOperandNum + optionalOperandNum < cmd.Operands.Length)
                throw new InvalidOperandException("Too many operands", cmd);

            string varKey = cmd.Operands[0].Trim(new char[] { '%' });
            string varValue = cmd.Operands[1];
            bool global = false;
            bool permanent = false;

            // Get optional operand
            if (cmd.Operands.Length == 3)
            {
                switch (cmd.Operands[2].ToUpper())
                {
                    case "GLOBAL":
                        global = true;
                        break;
                    case "PERMANENT":
                        permanent = true;
                        break;
                    default:
                        throw new InvalidOperandException($"Invalid operand [{cmd.Operands[2]}");
                }
            }

            // Logs are written in variables.SetValue method
            if (global)
            {
                variables.SetValue(VarsType.Global, varKey, varValue);
            }
            if (permanent)
            {
                variables.SetValue(VarsType.Global, varKey, varValue);
                IniFile.SetKey(project.MainPlugin.FullPath, "Variables", varKey, varValue);
            }
            else
            {
                variables.SetValue(VarsType.Local, varKey, varValue);
            }

            return logs.ToArray();
        }

        /// <summary>
        /// AddVariables,<%PluginFile%><Section>[,GLOBAL]
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public LogInfo[] AddVariables(BakeryCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 2, optional operand : 1
            const int necessaryOperandNum = 2;
            const int optionalOperandNum = 1;
            if (cmd.Operands.Length < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);
            else if (necessaryOperandNum + optionalOperandNum < cmd.Operands.Length)
                throw new InvalidOperandException("Too many operands", cmd);

            string plugin = cmd.Operands[0];
            string section = cmd.Operands[1];
            VarsType vars = VarsType.Local;

            // Get optional operand
            if (cmd.Operands.Length == 3)
            {
                switch (cmd.Operands[2].ToUpper())
                {
                    case "GLOBAL":
                        vars = VarsType.Global;
                        break;
                    default:
                        throw new InvalidOperandException($"Invalid operand [{cmd.Operands[2]}]");
                }
            }

            if (string.Equals(plugin, "%PluginFile%", StringComparison.OrdinalIgnoreCase)
                || string.Equals(plugin, "%ScriptFile%", StringComparison.OrdinalIgnoreCase))
                variables.AddVariables(vars, currentPlugin.Sections[section]);
            else
            {
                Plugin p = project.ActivePlugins.SearchByFullPath(variables.Expand(plugin));
                variables.AddVariables(vars, p.Sections[section]);
            }

            return logs.ToArray();
        }
    }
}