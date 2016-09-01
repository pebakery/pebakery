using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections;

namespace BakeryEngine
{
    using VariableDictionary = Dictionary<string, string>;

    /// <summary>
    /// Implementation of commands
    /// </summary>
    public partial class BakeryEngine
    {
        /// <summary>
        /// Set variables
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public LogInfo Set(BakeryCommand cmd)
        { // Set,<VarName>,<VarValue>[,GLOBAL | PERMANENT] 
            string logResult = string.Empty;
            LogState resState = LogState.Success;

            string varName;
            string varValue;
            bool global = false;
            bool permanent = false;
            VariableDictionary targetVar;

            // Must-have operand : 2-3
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
            else if (cmd.Operands.Length != 2)
                throw new InvalidOperandException("Necessary operands does not exist");

            varName = cmd.Operands[0].Trim(new char[] { '%' });
            varValue = cmd.Operands[1];

            if (global || permanent)
                targetVar = this.globalVars;
            else
                targetVar = this.localVars;

            if (targetVar.ContainsKey(varName))
            {
                targetVar[varName] = varValue;
                logResult = "Var %" + varName + "% set to " + varValue;
            }
            else
            {
                targetVar.Add(varName, varValue);
                logResult = "Var %" + varName + "% created, set to " + varValue; 
            }

            return new LogInfo(cmd, logResult, resState);
        }
    }
}