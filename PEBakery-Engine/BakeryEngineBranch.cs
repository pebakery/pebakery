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
        public enum IfConditionSubOpcode
        {
            None = 0,
            ExistFile, ExistDir, ExistSection, ExistRegSection, ExistRegKey, ExistVar,
            NotExistFile, NotExistDir, NotExistSection, NotExistRegSection, NotExistRegKey, NotExistVar,
            Equal, NotEqual, Smaller, Bigger,
            Not, 
            Online, Question, ExistMacro,
            // Deprecated
            EqualX, Ping, License, ExistRegMulti
        }

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

        /// <summary>
        /// If,<Condition>,<Command>
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public LogInfo[] IfCondition(BakeryCommand cmd)
        {
            LogInfo[] logs = new LogInfo[0]; // TODO: Delete this init sentence if If command is implemented

            // Necessary operand : 2, optional operand : variable length
            const int necessaryOperandNum = 2;
            if (cmd.Operands.Length < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);

            // Get Condition SubOpcode
            string subOpcodeString = cmd.Operands[0];
            IfConditionSubOpcode subOpcode = IfConditionSubOpcode.None;
            BakerySubCommand subCmd;

            // Check if rawCode is Empty
            if (string.Equals(subOpcodeString, string.Empty))
                throw new InvalidSubOpcodeException($"Invalid sub command [System.{subOpcodeString}]", cmd);

            // Parse opcode
            int subOpcodeIdx = 1;
            bool notFlag = false;
            do
            {
                try
                {
                    subOpcode = (IfConditionSubOpcode)Enum.Parse(typeof(IfConditionSubOpcode), subOpcodeString, true);
                    if (!Enum.IsDefined(typeof(IfConditionSubOpcode), subOpcode) || subOpcode == IfConditionSubOpcode.None)
                        throw new ArgumentException();
                }
                catch (ArgumentException)
                {
                    throw new InvalidSubOpcodeException($"Invalid sub command [IfConditionSubOpcode.{subOpcodeString}]", cmd);
                }

                if (subOpcode == IfConditionSubOpcode.Not)
                {
                    subOpcodeIdx++;
                    if (notFlag)
                        throw new InvalidSubOpcodeException("Not condition cannot be duplicated", cmd);
                    notFlag = true;
                }
            }
            while (subOpcode == IfConditionSubOpcode.Not);
            subCmd = new BakerySubCommand(SubCommandType.IfCondition, subOpcode, cmd.Operands.Skip(subOpcodeIdx).ToArray(), notFlag);

            // Call sub command methods
            switch (subOpcode)
            {
                case IfConditionSubOpcode.ExistFile:
                    logs = this.IfExistFile(cmd, subCmd);
                    break;
                case IfConditionSubOpcode.ExistDir:
                    logs = this.IfExistDir(cmd, subCmd);
                    break;
                case IfConditionSubOpcode.ExistSection:
                    break;
                case IfConditionSubOpcode.ExistRegSection:
                    break;
                case IfConditionSubOpcode.ExistRegKey:
                    break;
                case IfConditionSubOpcode.ExistVar:
                    break;
                case IfConditionSubOpcode.NotExistFile:
                    subCmd.NotFlag = true;
                    logs = this.IfExistFile(cmd, subCmd);
                    break;
                case IfConditionSubOpcode.NotExistDir:
                    subCmd.NotFlag = true;
                    logs = this.IfExistDir(cmd, subCmd);
                    break;
                case IfConditionSubOpcode.NotExistSection:
                    subCmd.NotFlag = true;
                    break;
                case IfConditionSubOpcode.NotExistRegSection:
                    subCmd.NotFlag = true;
                    break;
                case IfConditionSubOpcode.NotExistRegKey:
                    subCmd.NotFlag = true;
                    break;
                case IfConditionSubOpcode.NotExistVar:
                    subCmd.NotFlag = true;
                    break;
                case IfConditionSubOpcode.Equal:
                    break;
                case IfConditionSubOpcode.NotEqual:
                    subCmd.NotFlag = true;
                    break;
                case IfConditionSubOpcode.Smaller:
                    break;
                case IfConditionSubOpcode.Bigger:
                    break;
                case IfConditionSubOpcode.Online:
                    break;
                default: // In fact, Enum.Parse logic must handle this. If this logic is called, it is definitely a BUG
                    throw new InvalidSubOpcodeException($"INTERNAL ERROR! Invalid sub command [System.{subOpcodeString}]", cmd);
            }

            return logs;
        }

        public BakeryCommand IfInternalCommand(BakeryCommand cmd, BakerySubCommand subCmd, int opcodeIdx)
        {
            // If ExistFile,Joveler.txt,Echo,ied206
            // [subCmd]   0,           1,  2,     3 -> opcodeIdx must be 2 

            // Parse opcode
            Opcode opcode = Opcode.None;
            string opcodeStr = subCmd.Operands[opcodeIdx];
            try
            {
                opcode = (Opcode) Enum.Parse(typeof(Opcode), opcodeStr, true);
                if (!Enum.IsDefined(typeof(Opcode), opcode) || opcode == Opcode.None)
                    throw new ArgumentException();
            }
            catch (ArgumentException)
            {
                throw new InvalidOpcodeException($"Unknown command [{opcodeStr}]", cmd);
            }

            return new BakeryCommand(cmd.RawCode, opcode, subCmd.Operands.Skip(opcodeIdx + 1).ToArray(), returnAddress.Count + 1);
        }

        /// <summary>
        /// If,ExistFile,<FilePath>,<Command>
        /// </summary>
        /// <remarks>Support wildcard</remarks>
        /// <param name="cmd"></param>
        /// <param name="subCmd"></param>
        /// <returns></returns>
        public LogInfo[] IfExistFile(BakeryCommand cmd, BakerySubCommand subCmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 1, optional operand : 0+
            const int necessaryOperandNum = 1;
            if (cmd.Operands.Length < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);

            string filePath = EscapeString(variables.Expand(subCmd.Operands[0]));
            string rawFilePath = subCmd.Operands[0];
            BakeryCommand ifCmd = IfInternalCommand(cmd, subCmd, necessaryOperandNum);

            // Check filePath contains wildcard
            bool filePathContainsWildcard = true;
            if (filePath.IndexOfAny(new char[] { '*', '?' }) == -1) // No wildcard
                filePathContainsWildcard = false;

            // Check if file exists
            bool exist;
            if (filePathContainsWildcard)
            {
                string[] list = Directory.GetFiles(Helper.GetDirNameEx(filePath), Path.GetFileName(filePath));
                if (0 < list.Length)
                    exist = true;
                else
                    exist = false;
            }
            else
                exist = File.Exists(filePath);
            if (subCmd.NotFlag)
                exist = !exist;

            if (exist)
            {
                logs.Add(new LogInfo(cmd, subCmd, LogState.Success, $"File [{rawFilePath}] exists"));
                logs.AddRange(ExecuteCommand(ifCmd));
            }
            else
            {
                logs.Add(new LogInfo(cmd, subCmd, LogState.Ignore, $"File [{rawFilePath}] does not exists"));
            }
            
            return logs.ToArray();
        }

        /// <summary>
        /// If,ExistDir,<DirPath>,<Command>
        /// </summary>
        /// <remarks>Support wildcard</remarks>
        /// <param name="cmd"></param>
        /// <param name="subCmd"></param>
        /// <returns></returns>
        public LogInfo[] IfExistDir(BakeryCommand cmd, BakerySubCommand subCmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 1, optional operand : 0+
            const int necessaryOperandNum = 1;
            if (cmd.Operands.Length < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);

            string dirPath = EscapeString(variables.Expand(subCmd.Operands[0]));
            string rawFilePath = subCmd.Operands[0];
            BakeryCommand ifCmd = IfInternalCommand(cmd, subCmd, necessaryOperandNum);

            // Check filePath contains wildcard
            bool dirPathContainsWildcard = true;
            if (dirPath.IndexOfAny(new char[] { '*', '?' }) == -1) // No wildcard
                dirPathContainsWildcard = false;

            // Check if file exists
            bool exist;
            if (dirPathContainsWildcard)
            {
                string[] list = Directory.GetDirectories(Helper.GetDirNameEx(dirPath), Path.GetFileName(dirPath));
                if (0 < list.Length)
                    exist = true;
                else
                    exist = false;
            }
            else
                exist = Directory.Exists(dirPath);
            if (subCmd.NotFlag)
                exist = !exist;

            if (exist)
            {
                logs.Add(new LogInfo(cmd, subCmd, LogState.Success, $"Directory [{rawFilePath}] exists"));
                logs.AddRange(ExecuteCommand(ifCmd));
            }
            else
            {
                logs.Add(new LogInfo(cmd, subCmd, LogState.Ignore, $"Directory [{rawFilePath}] does not exists"));
            }

            return logs.ToArray();
        }
    }
}