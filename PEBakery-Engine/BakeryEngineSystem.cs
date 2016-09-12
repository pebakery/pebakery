using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections;
using System.Windows.Forms;

namespace BakeryEngine
{
    using System.Globalization;
    using VariableDictionary = Dictionary<string, string>;

    public enum SystemSubOpcode
    {
        None = 0,
        Cursor,
        ErrorOff, Log, SaveLog,
        GetFreeDrive, GetFreeSpace,
        GetEnv, IsAdmin, 
        OnBuildExit, OnScriptExit, OnPluginExit,
        RefreshInterface, RescanScript,
        ShellExecute,
        // Deprecated
        Comp80, FileRedirect, HasUAC, IsTerminal, RebuildVars, RegRedirect, SplitParameters
    }

    public partial class BakeryEngine
    {
        /*
         * System with sub commands
         */

        /// <summary>
        /// FileCopy,<SrcFileName>,<DestPath>[,PRESERVE][,NOWARN][,NOREC]
        /// Wildcard supported in <SrcFileName>
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns>LogInfo[]</returns>
        public LogInfo[] SystemCommands(BakeryCommand cmd)
        {
            LogInfo[] logs;

            // Necessary operand : 1, optional operand : 0+
            const int necessaryOperandNum = 1;

            if (cmd.Operands.Length < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);

            string subOpcodeString = cmd.Operands[0];
            SystemSubOpcode subOpcode = SystemSubOpcode.None;
            BakerySubCommand subCmd;

            // Check if rawCode is Empty
            if (string.Equals(subOpcodeString, string.Empty))
                throw new InvalidSubOpcodeException($"Invalid sub command [System.{subOpcodeString}]", cmd);

            // Parse opcode
            try
            {
                subOpcode = (SystemSubOpcode)Enum.Parse(typeof(SystemSubOpcode), subOpcodeString, true);
                if (Enum.IsDefined(typeof(SystemSubOpcode), subOpcode))
                {
                    if (subOpcode == SystemSubOpcode.None)
                        throw new InvalidSubOpcodeException($"Invalid sub command [System.{subOpcodeString}]", cmd);
                    else
                        subCmd = new BakerySubCommand(SubCommandType.System, subOpcode, cmd.Operands.Skip(1).ToArray());
                }
                else
                    throw new InvalidSubOpcodeException($"Invalid sub command [System.{subOpcodeString}]", cmd);
            }
            catch (ArgumentException)
            {
                throw new InvalidSubOpcodeException($"Invalid sub command [System.{subOpcodeString}]", cmd);
            } // Do nothing

            // Call sub command methods
            switch (subOpcode)
            {
                case SystemSubOpcode.Cursor:
                    logs = SystemCursor(cmd, subCmd);
                    break;
                case SystemSubOpcode.ErrorOff:
                    logs = SystemErrorOff(cmd, subCmd);
                    break;
                case SystemSubOpcode.Log:
                    logs = SystemLog(cmd, subCmd);
                    break;
                case SystemSubOpcode.SaveLog:
                    logs = SystemSaveLog(cmd, subCmd);
                    break;
                default: // In fact, Enum.Parse logic must handle this. If this logic is called, it is definitely a BUG
                    throw new InvalidSubOpcodeException($"INTERNAL ERROR! Invalid sub command [System.{subOpcodeString}]", cmd);
            }
 
            return logs;
        }

        /// <summary>
        /// System,Cursor,<Wait|Normal>
        /// </summary>
        /// <param name="cmd">BakeryCommand</param>
        /// /// <param name="subCmd">BakerySubCommand</param>
        /// <returns></returns>
        public LogInfo[] SystemCursor(BakeryCommand cmd, BakerySubCommand subCmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 1, optional operand : 0
            const int necessaryOperandNum = 1;
            const int optionalOperandNum = 0;

            if (subCmd.Operands.Length < necessaryOperandNum)
                throw new InvalidSubOperandException("Necessary operands does not exist", cmd);
            else if (necessaryOperandNum + optionalOperandNum < subCmd.Operands.Length)
                throw new InvalidSubOperandException("Too many operands", cmd);

            string action = subCmd.Operands[0];

            if (string.Equals(action, "Wait", StringComparison.OrdinalIgnoreCase))
            {
                Cursor.Current = Cursors.WaitCursor;
                logs.Add(new LogInfo(cmd, LogState.Success, "Set mouse cursor to [Wait]"));
            }
            else if (string.Equals(action, "Normal", StringComparison.OrdinalIgnoreCase))
            {                
                Cursor.Current = Cursors.Default;
                logs.Add(new LogInfo(cmd, LogState.Success, "Set mouse cursor to [Normal]"));
            }
            else
            {
                throw new InvalidSubOperandException($"Invalid operand [{action}]", cmd);
            }

            return logs.ToArray();
        }

        /// <summary>
        /// System,ErrorOff[,Lines] 
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="subCmd"></param>
        /// <returns></returns>
        public LogInfo[] SystemErrorOff(BakeryCommand cmd, BakerySubCommand subCmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 0, optional operand : 1
            const int necessaryOperandNum = 0;
            const int optionalOperandNum = 1;

            if (subCmd.Operands.Length < necessaryOperandNum)
                throw new InvalidSubOperandException("Necessary operands does not exist", cmd);
            else if (necessaryOperandNum + optionalOperandNum < subCmd.Operands.Length)
                throw new InvalidSubOperandException("Too many operands", cmd);

            uint lines = 1; // must be (0 < lines)
            if (subCmd.Operands.Length == necessaryOperandNum + optionalOperandNum)
            {
                if (Helper.ParseUInt32(subCmd.Operands[0], out lines) == false)
                    throw new InvalidSubOperandException($"[{subCmd.Operands[0]}] is not valid number", cmd);
                if (lines <= 0)
                    throw new InvalidSubOperandException($"[{subCmd.Operands[0]}] must be greater than 0", cmd);
            }

            logger.ErrorOffCount = lines + 1; // 1 is for ErrorOff itself
            logs.Add(new LogInfo(cmd, LogState.Success, $"Error log is off for [{lines}] lines"));

            return logs.ToArray();
        }

        /// <summary>
        /// System,Log,<On|Off>
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="subCmd"></param>
        /// <returns></returns>
        public LogInfo[] SystemLog(BakeryCommand cmd, BakerySubCommand subCmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 1, optional operand : 0
            const int necessaryOperandNum = 1;
            const int optionalOperandNum = 0;

            if (subCmd.Operands.Length < necessaryOperandNum)
                throw new InvalidSubOperandException("Necessary operands does not exist", cmd, subCmd);
            else if (necessaryOperandNum + optionalOperandNum < subCmd.Operands.Length)
                throw new InvalidSubOperandException("Too many operands", cmd, subCmd);

            string action = subCmd.Operands[0];
            if (string.Equals(action, "On", StringComparison.OrdinalIgnoreCase))
            {
                logger.SuspendLog = true;
                logs.Add(new LogInfo(cmd, subCmd, LogState.Success, $"Logging turned on for plugin [{cmd.Address.plugin.ShortPath}]"));
            }
            else if (string.Equals(action, "Off", StringComparison.OrdinalIgnoreCase))
            {
                logger.SuspendLog = false;
                logs.Add(new LogInfo(cmd, subCmd, LogState.Success, $"Logging turned off for plugin [{cmd.Address.plugin.ShortPath}]"));
            }
            else
            {
                throw new InvalidSubOperandException($"Invalid operand [{action}]", cmd);
            }

            return logs.ToArray();
        }

        /// <summary>
        /// System,SaveLog,<File>
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="subCmd"></param>
        /// <returns></returns>
        public LogInfo[] SystemSaveLog(BakeryCommand cmd, BakerySubCommand subCmd)
        {
            // Necessary operand : 1, optional operand : 0
            const int necessaryOperandNum = 1;
            const int optionalOperandNum = 0;

            if (subCmd.Operands.Length < necessaryOperandNum)
                throw new InvalidSubOperandException("Necessary operands does not exist", cmd, subCmd);
            else if (necessaryOperandNum + optionalOperandNum < subCmd.Operands.Length)
                throw new InvalidSubOperandException("Too many operands", cmd, subCmd);

            string dest = EscapeString(variables.Expand(subCmd.Operands[0]));
            string rawDest = subCmd.Operands[0];
            logger.Flush();
            File.Copy(logger.LogFile, dest);

            return new LogInfo[0];
        }
    }
}