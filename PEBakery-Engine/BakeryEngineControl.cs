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
            ArrayList logs = new ArrayList();

            // Necessary operand : 2, optional operand : 1
            const int necessaryOperandNum = 2;
            const int optionalOperandNum = 1;
            if (cmd.Operands.Length < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);
            else if (necessaryOperandNum + optionalOperandNum < cmd.Operands.Length)
                throw new InvalidOperandException("Too many operands", cmd);

            string varKey;
            string varValue;
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
                        throw new InvalidOperandException("Invalid operand : " + cmd.Operands[2]);
                }
            }

            varKey = cmd.Operands[0].Trim(new char[] { '%' });
            varValue = cmd.Operands[1];

            bool isVarCreated = false;
            if (global || permanent)
            {
                isVarCreated = variables.ContainsKey(VarsType.Global, varKey);
                variables.SetValue(VarsType.Global, varKey, varValue);
            }
            else
            {
                isVarCreated = variables.ContainsKey(VarsType.Local, varKey);
                variables.SetValue(VarsType.Local, varKey, varValue);
            }

            if (isVarCreated)
            {
                logs.Add(new LogInfo(cmd, string.Concat("Var %", varKey, "% set to ", varValue), LogState.Success));
            }
            else
            {
                logs.Add(new LogInfo(cmd, string.Concat("Var %", varKey, "% created, set to ", varValue), LogState.Success));
            }

            return logs.ToArray(typeof(LogInfo)) as LogInfo[];
        }

        public LogInfo[] AddVariables(BakeryCommand cmd)
        {
            ArrayList logs = new ArrayList();

            // Necessary operand : 2, optional operand : 1
            const int necessaryOperandNum = 2;
            const int optionalOperandNum = 1;
            if (cmd.Operands.Length < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);
            else if (necessaryOperandNum + optionalOperandNum < cmd.Operands.Length)
                throw new InvalidOperandException("Too many operands", cmd);

            string varKey;
            string varValue;
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
                        throw new InvalidOperandException("Invalid operand : " + cmd.Operands[2]);
                }
            }

            varKey = cmd.Operands[0].Trim(new char[] { '%' });
            varValue = cmd.Operands[1];

            bool isVarCreated = false;
            if (global || permanent)
            {
                isVarCreated = variables.ContainsKey(VarsType.Global, varKey);
                variables.SetValue(VarsType.Global, varKey, varValue);
            }
            else
            {
                isVarCreated = variables.ContainsKey(VarsType.Local, varKey);
                variables.SetValue(VarsType.Local, varKey, varValue);
            }

            if (isVarCreated)
            {
                logs.Add(new LogInfo(cmd, string.Concat("Var [%", varKey, "%] set to [", varValue, "]"), LogState.Success));
            }
            else
            {
                logs.Add(new LogInfo(cmd, string.Concat("Var [%", varKey, "%] created, set to [", varValue, "]"), LogState.Success));
            }

            return logs.ToArray(typeof(LogInfo)) as LogInfo[];
        }
    }
}