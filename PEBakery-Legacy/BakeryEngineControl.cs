using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections;
using System.Text.RegularExpressions;

namespace BakeryEngine_Legacy
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
        public List<LogInfo> CmdSet(BakeryCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 2, optional operand : 1
            const int necessaryOperandNum = 2;
            const int optionalOperandNum = 1;
            if (cmd.Operands.Count < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);
            else if (necessaryOperandNum + optionalOperandNum < cmd.Operands.Count)
                throw new InvalidOperandException("Too many operands", cmd);

            // Get operand
            string varValue = ExpandSectionParams(cmd.Operands[1]);
            bool global = false;
            bool permanent = false;

            // Get optional operand
            if (cmd.Operands.Count == 3)
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

            // Determine varKey's type - %A% vs #1
            string rawVarValue = cmd.Operands[0];
            if (rawVarValue.StartsWith("%") && rawVarValue.EndsWith("%")) // %A%
            {
                string varKey = BakeryVariables.TrimPercentMark(rawVarValue);
                // Logs are written in variables.SetValue method
                if (global)
                {
                    logs.Add(variables.SetValue(VarsType.Global, cmd, varKey, varValue));
                }
                if (permanent)
                {
                    LogInfo log = variables.SetValue(VarsType.Global, cmd, varKey, varValue);
                    if (log.State == LogState.Success)
                    { // SetValue success, write to IniFile
                        if (IniFile.SetKey(project.MainPlugin.FullPath, "Variables", varKey, varValue))
                            logs.Add(new LogInfo(cmd, LogState.Success, $"Permanent variable [%{varKey}%] set to [{varValue}]"));
                        else
                            logs.Add(new LogInfo(cmd, LogState.Error, $"Failed to write permanent variable [%{varKey}%] and its value [{varValue}] into script.project"));
                    }
                    else
                    { // SetValue failed
                        logs.Add(new LogInfo(cmd, LogState.Error, $"Variable [%{varKey}%] contains itself in [{varValue}]"));
                    }
                }
                else // Local
                {
                    logs.Add(variables.SetValue(VarsType.Local, cmd, varKey, varValue));
                }
            }
            else if (Regex.Match(rawVarValue, @"(#\d+)", RegexOptions.Compiled).Success) // #1
            {
                int paramIdx = BakeryVariables.GetSectionParamIndex(rawVarValue) - 1; // -1 for (#1 == curSectionParams[0])
                if (paramIdx < 0)
                    throw new InvalidOperandException($"[{rawVarValue}]'s index [{paramIdx + 1}] cannot be negative number", cmd);
                else if (paramIdx < curSectionParams.Count)
                    curSectionParams[paramIdx] = varValue;
                else
                {
                    for (int i = curSectionParams.Count; i < paramIdx; i++)
                        curSectionParams.Add("");
                    curSectionParams.Add(varValue);
                }
            }
            else
                throw new InvalidOperandException($"Invalid variable name [{rawVarValue}]", cmd);


            return logs;
        }

        /// <summary>
        /// AddVariables,<%PluginFile%><Section>[,GLOBAL]
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public List<LogInfo> CmdAddVariables(BakeryCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 2, optional operand : 1
            const int necessaryOperandNum = 2;
            const int optionalOperandNum = 1;
            if (cmd.Operands.Count < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);
            else if (necessaryOperandNum + optionalOperandNum < cmd.Operands.Count)
                throw new InvalidOperandException("Too many operands", cmd);

            string plugin = cmd.Operands[0];
            string section = cmd.Operands[1];
            VarsType vars = VarsType.Local;

            // Get optional operand
            if (cmd.Operands.Count == 3)
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
                logs.AddRange(variables.AddVariables(vars, currentPlugin.Sections[section], cmd.Depth));
            else
            {
                Plugin p = project.AllPlugins.SearchByFullPath(ExpandVariables(plugin));
                logs.AddRange(variables.AddVariables(vars, p.Sections[section], cmd.Depth));
            }

            return logs;
        }
    }
}