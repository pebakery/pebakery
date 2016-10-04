using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections;
using Microsoft.Win32;

namespace BakeryEngine
{
    public enum IfSubOpcode
    {
        None = 0,
        ExistFile, ExistDir, ExistSection, ExistRegSection, ExistRegKey, ExistVar,
        Equal, Smaller, Bigger, SmallerEqual, BiggerEqual,
        Not,
        Ping, Online, Question, ExistMacro,
        // Deprecated
        EqualX, License, ExistRegMulti
    }

    public class BakeryIfCommand
    {
        // For IfCondition
        public IfSubOpcode SubOpcode;
        public string[] Operands;
        public bool NotFlag;

        public BakeryIfCommand(IfSubOpcode subOpcode, string[] operands, bool notFlag)
        {
            this.SubOpcode = subOpcode;
            this.Operands = operands;
            this.NotFlag = notFlag;
        }

        public List<string> ToOperands()
        {
            List<string> operands = new List<string>();
            if (NotFlag)
                operands.Add("Not");
            operands.Add(SubOpcode.ToString());
            operands.AddRange(Operands.Take(BakeryCodeParser.GetIfSubCmdOperandNum(SubOpcode)));
            return operands;
        }

        public List<string> ToOperands(bool prefix, string add)
        {
            List<string> operands = new List<string>();
            if (prefix)
                operands.Add(add);
            if (NotFlag)
                operands.Add("Not");
            operands.Add(SubOpcode.ToString());
            operands.AddRange(Operands.Take(BakeryCodeParser.GetIfSubCmdOperandNum(SubOpcode)));
            if (!prefix)
                operands.Add(add);
            return operands;
        }

        public List<string> ToOperands(bool prefix, List<string> add)
        {
            List<string> operands = new List<string>();
            if (prefix)
                operands.AddRange(add);
            if (NotFlag)
                operands.Add("Not");
            operands.Add(SubOpcode.ToString());
            operands.AddRange(Operands.Take(BakeryCodeParser.GetIfSubCmdOperandNum(SubOpcode)));
            if (!prefix)
                operands.AddRange(add);
            return operands;
        }
        public List<string> ToOperandsPrefix(string add)
        {
            List<string> operands = new List<string>();
            operands.Add(add);
            if (NotFlag)
                operands.Add("Not");
            operands.Add(SubOpcode.ToString());
            operands.AddRange(Operands.Take(BakeryCodeParser.GetIfSubCmdOperandNum(SubOpcode)));
            return operands;
        }

        public List<string> ToOperandsPrefix(List<string> add)
        {
            List<string> operands = new List<string>();
            operands.AddRange(add);
            if (NotFlag)
                operands.Add("Not");
            operands.Add(SubOpcode.ToString());
            operands.AddRange(Operands.Take(BakeryCodeParser.GetIfSubCmdOperandNum(SubOpcode)));
            return operands;
        }
        public List<string> ToOperandsPostfix(string add)
        {
            List<string> operands = new List<string>();
            if (NotFlag)
                operands.Add("Not");
            operands.Add(SubOpcode.ToString());
            operands.AddRange(Operands.Take(BakeryCodeParser.GetIfSubCmdOperandNum(SubOpcode)));
            operands.Add(add);
            return operands;
        }

        public List<string> ToOperandsPostfix(List<string> add)
        {
            List<string> operands = new List<string>();
            if (NotFlag)
                operands.Add("Not");
            operands.Add(SubOpcode.ToString());
            operands.AddRange(Operands.Take(BakeryCodeParser.GetIfSubCmdOperandNum(SubOpcode)));
            operands.AddRange(add);
            return operands;
        }
    }

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
            return InternalRunExec(cmd, false, 0);
        }

        private LogInfo[] RunExecCallback(BakeryCommand cmd, int depth)
        {
            return InternalRunExec(cmd, true, depth);
        }

        private LogInfo[] InternalRunExec(BakeryCommand cmd, bool callback, int depth)
        {
            List<LogInfo> logs = new List<LogInfo>();

            try
            {
                // Necessary operand : 2, optional operand : variable length
                const int necessaryOperandNum = 2;
                if (cmd.Operands.Count < necessaryOperandNum)
                    throw new InvalidOperandException("Necessary operands does not exist", cmd);

                // Get necesssary operand
                string pluginFile = UnescapeString(variables.Expand(cmd.Operands[0]));
                string sectionName = UnescapeString(variables.Expand(cmd.Operands[1]));
                string rawPluginFile = cmd.Operands[0];
                string rawSectoinName = cmd.Operands[1];

                // Get optional operand 
                List<string> parameters = new List<string>();//  string[cmd.Operands.Count - necessaryOperandNum];
                if (necessaryOperandNum < cmd.Operands.Count)
                    parameters.AddRange(cmd.Operands.Skip(2).Take(cmd.Operands.Count - necessaryOperandNum));

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
                SectionAddress nextAddr = new SectionAddress(); // Blank value
                if (callback)
                {
                    nextAddr = new SectionAddress(targetPlugin, targetPlugin.Sections[sectionName]);
                    if (depth != 0)
                    {
                        if (inCurrentPlugin)
                            logger.Write(new LogInfo(cmd, LogState.Success, $"Processing section [{sectionName}]"), true);
                        else
                            logger.Write(new LogInfo(cmd, LogState.Success, $"Processing [{rawPluginFile}]'s section [{sectionName}]"), true);
                    }
                }

                // Exec utilizes [Variables] section of the plugin
                if (cmd.Opcode == Opcode.Exec)
                    variables.AddVariables(VarsType.Local, targetPlugin.Sections["Variables"], depth, true);

                // Run Section
                RunSection(nextAddr, parameters, depth);
            }
            catch (Exception e)
            {
                if (callback)
                    logs.Add(new LogInfo(cmd, LogState.Error, e.GetType() + ": " + Helper.RemoveLastNewLine(e.Message)));
                else
                    throw e;
            }

            return logs.ToArray();
        }



        /// <summary>
        /// IfCompact,<Condition>,Link
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public LogInfo[] IfCompact(BakeryCommand cmd)
        {
            // elseFlag = 1;

            LogInfo[] logs = new LogInfo[0]; // TODO: Delete this init sentence if If command is implemented

            // Necessary operand : 3, 2 for condition and 1 for embeded command
            const int necessaryOperandNum = 3;
            if (cmd.Operands.Count < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);

            // Get Condition SubOpcode
            BakeryIfCommand subCmd = BakeryCodeParser.ForgeIfSubCommand(cmd, false);

            // Call sub command methods
            switch (subCmd.SubOpcode)
            {
                case IfSubOpcode.Equal:
                case IfSubOpcode.Smaller:
                case IfSubOpcode.Bigger:
                case IfSubOpcode.SmallerEqual:
                case IfSubOpcode.BiggerEqual:
                    logs = this.IfCompare(cmd, subCmd);
                    break;
                case IfSubOpcode.ExistFile:
                    logs = this.IfExistFile(cmd, subCmd);
                    break;
                case IfSubOpcode.ExistDir:
                    logs = this.IfExistDir(cmd, subCmd);
                    break;
                case IfSubOpcode.ExistSection:
                    logs = this.IfExistSection(cmd, subCmd);
                    break;
                case IfSubOpcode.ExistRegSection:
                    logs = this.IfExistRegSection(cmd, subCmd);
                    break;
                case IfSubOpcode.ExistRegKey:
                    logs = this.IfExistRegKey(cmd, subCmd);
                    break;
                case IfSubOpcode.ExistVar:
                    logs = this.IfExistVar(cmd, subCmd);
                    break;
                case IfSubOpcode.Ping:
                    break;
                case IfSubOpcode.Online:
                    break;
                default: // In fact, Enum.Parse logic must handle this. If this logic is called in production, it is definitely a BUG
                    throw new InvalidSubOpcodeException($"INTERNAL ERROR! Invalid sub command [If,{subCmd.SubOpcode}]", cmd);
            }

            return logs;
        }

        private void RunIfLink(ref List<LogInfo> logs, bool run, BakeryCommand cmd, BakeryIfCommand subCmd, string message)
        { // ref for performance
            int necessaryOperandNum = BakeryCodeParser.GetIfOperandNum(cmd, subCmd);
            BakeryCommand embCmd = BakeryCodeParser.ForgeEmbedCommand(cmd, necessaryOperandNum + 1, cmd.Depth);
            if (embCmd.Opcode != Opcode.Link)
                throw new CriticalErrorException($"[{cmd.Opcode.ToString()}] must be used with Link", cmd);

            if (run)
            {
                runElse = false;
                logs.Add(new LogInfo(cmd, LogState.Success, message));
                RunCommands(cmd.Link, new List<string>(), cmd.Depth);
            }
            else // Do not run
            {
                runElse = true;
                logs.Add(new LogInfo(cmd, LogState.Ignore, message));
            }
        }

        /// <summary>
        /// If,<%Variable%>,Equal,<Value><Command>
        /// </summary>
        /// <remarks>
        /// Equal can be substituded by Smaller, Bigger, SmallerEqual, BiggerEqual
        /// ==, <, >, <=, =>, != also supported
        /// </remarks>
        /// <param name="cmd"></param>
        /// <param name="subCmd"></param>
        /// <returns></returns>
        public LogInfo[] IfCompare(BakeryCommand cmd, BakeryIfCommand subCmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 2 for condition and 1 for command
            int necessaryOperandNum = BakeryCodeParser.GetIfSubCmdOperandNum((IfSubOpcode) subCmd.SubOpcode);
            if (cmd.Operands.Count < necessaryOperandNum + 1)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);

            string value1 = UnescapeString(variables.Expand(subCmd.Operands[0]));
            string value2 = UnescapeString(variables.Expand(subCmd.Operands[1]));

            string resMessage;
            CompareStringNumberResult comp = NumberHelper.CompareStringNumber(value1, value2);

            bool run = false;
            bool notEqual = ((comp & CompareStringNumberResult.NotEqual) == CompareStringNumberResult.NotEqual);
            if ((comp & CompareStringNumberResult.Equal) != 0)
            {
                if ((IfSubOpcode)subCmd.SubOpcode == IfSubOpcode.Equal
                    || (IfSubOpcode)subCmd.SubOpcode == IfSubOpcode.SmallerEqual
                    || (IfSubOpcode)subCmd.SubOpcode == IfSubOpcode.BiggerEqual)
                    run = true;
                resMessage = $"[{value1}] is equal to [{value2}]";
            }
            else if ((comp & CompareStringNumberResult.Smaller) != 0)
            {
                if ((IfSubOpcode)subCmd.SubOpcode == IfSubOpcode.Smaller
                    || (IfSubOpcode)subCmd.SubOpcode == IfSubOpcode.SmallerEqual
                    || (IfSubOpcode)subCmd.SubOpcode == IfSubOpcode.Bigger && subCmd.NotFlag
                    || (IfSubOpcode)subCmd.SubOpcode == IfSubOpcode.BiggerEqual && subCmd.NotFlag)
                    run = true;
                resMessage = $"[{value1}] is smaller than [{value2}]";
            }
            else if ((comp & CompareStringNumberResult.Bigger) != 0)
            {
                if ((IfSubOpcode)subCmd.SubOpcode == IfSubOpcode.Bigger
                    || (IfSubOpcode)subCmd.SubOpcode == IfSubOpcode.BiggerEqual
                    || (IfSubOpcode)subCmd.SubOpcode == IfSubOpcode.Smaller && subCmd.NotFlag
                    || (IfSubOpcode)subCmd.SubOpcode == IfSubOpcode.SmallerEqual && subCmd.NotFlag)
                    run = true;
                resMessage = $"[{value1}] is bigger than [{value2}]";
            }
            else if ((comp & CompareStringNumberResult.NotEqual) != 0)
            {
                if ((IfSubOpcode)subCmd.SubOpcode == IfSubOpcode.Equal && subCmd.NotFlag)
                    run = true;
                resMessage = $"[{value1}] is not equal to [{value2}]";
            }
            else
                throw new InternalUnknownException($"Cannot compare [{value1}] and [{value2}]");

            RunIfLink(ref logs, run, cmd, subCmd, resMessage);

            return logs.ToArray();
        }

        /// <summary>
        /// If,ExistFile,<FilePath>,<Command>
        /// </summary>
        /// <remarks>Support wildcard</remarks>
        /// <param name="cmd"></param>
        /// <param name="subCmd"></param>
        /// <returns></returns>
        public LogInfo[] IfExistFile(BakeryCommand cmd, BakeryIfCommand subCmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 1 for condition and 1 for command
            int necessaryOperandNum = BakeryCodeParser.GetIfSubCmdOperandNum((IfSubOpcode)subCmd.SubOpcode);
            if (cmd.Operands.Count < necessaryOperandNum + 1)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);

            string filePath = UnescapeString(variables.Expand(subCmd.Operands[0]));
            string rawFilePath = subCmd.Operands[0];

            // Check filePath contains wildcard
            bool filePathContainsWildcard = true;
            if (filePath.IndexOfAny(new char[] { '*', '?' }) == -1) // No wildcard
                filePathContainsWildcard = false;

            // Check if file exists
            bool run;
            if (filePathContainsWildcard)
            {
                string[] list = Directory.GetFiles(Helper.GetDirNameEx(filePath), Path.GetFileName(filePath));
                if (0 < list.Length)
                    run = true;
                else
                    run = false;
            }
            else
                run = File.Exists(filePath);

            string resMessage;
            if (run) // Exists
                resMessage = $"File [{rawFilePath}] exists";
            else
                resMessage = $"File [{rawFilePath}] does not exists";

            RunIfLink(ref logs, (run && !subCmd.NotFlag) || (!run && subCmd.NotFlag), cmd, subCmd, resMessage);

            return logs.ToArray();
        }

        /// <summary>
        /// If,ExistDir,<DirPath>,<Command>
        /// </summary>
        /// <remarks>Support wildcard</remarks>
        /// <param name="cmd"></param>
        /// <param name="subCmd"></param>
        /// <returns></returns>
        public LogInfo[] IfExistDir(BakeryCommand cmd, BakeryIfCommand subCmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 1 for condition and 1 for command
            int necessaryOperandNum = BakeryCodeParser.GetIfSubCmdOperandNum((IfSubOpcode)subCmd.SubOpcode);
            if (cmd.Operands.Count < necessaryOperandNum + 1)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);

            string dirPath = UnescapeString(variables.Expand(subCmd.Operands[0]));
            string rawFilePath = subCmd.Operands[0];

            // Check filePath contains wildcard
            bool dirPathContainsWildcard = true;
            if (dirPath.IndexOfAny(new char[] { '*', '?' }) == -1) // No wildcard
                dirPathContainsWildcard = false;

            // Check if file exists
            bool run;
            if (dirPathContainsWildcard)
            {
                string[] list = Directory.GetDirectories(Helper.GetDirNameEx(dirPath), Path.GetFileName(dirPath));
                if (0 < list.Length)
                    run = true;
                else
                    run = false;
            }
            else
                run = Directory.Exists(dirPath);

            string resMessage;
            if (run) // Exists
                resMessage = $"Directory [{rawFilePath}] exists";
            else
                resMessage = $"Directory [{rawFilePath}] does not exists";

            RunIfLink(ref logs, (run && !subCmd.NotFlag) || (!run && subCmd.NotFlag), cmd, subCmd, resMessage);

            return logs.ToArray();
        }

        /// <summary>
        /// If,ExistSection,<IniFile>,<Section>,<Command>
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="subCmd"></param>
        /// <returns></returns>
        public LogInfo[] IfExistSection(BakeryCommand cmd, BakeryIfCommand subCmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 2 for condition and 1 for command
            int necessaryOperandNum = BakeryCodeParser.GetIfSubCmdOperandNum((IfSubOpcode)subCmd.SubOpcode);
            if (cmd.Operands.Count < necessaryOperandNum + 1)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);

            string iniFile = UnescapeString(variables.Expand(subCmd.Operands[0]));
            string section = UnescapeString(variables.Expand(subCmd.Operands[1]));
            string rawIniFile = subCmd.Operands[0];
            string rawSection = subCmd.Operands[1];

            string resMessage;
            bool run = IniFile.CheckSectionExist(iniFile, section);
            if (run) // Exists
                resMessage = $"Section [{rawSection}] exists in [{rawIniFile}]";
            else
                resMessage = $"Section [{rawSection}] does not exists in [{rawIniFile}]";

            RunIfLink(ref logs, (run && !subCmd.NotFlag) || (!run && subCmd.NotFlag), cmd, subCmd, resMessage);

            return logs.ToArray();
        }

        /// <summary>
        /// If,ExistRegSection,<RegRootKey>,<SubKey>,<Command>
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="subCmd"></param>
        /// <returns></returns>
        public LogInfo[] IfExistRegSection(BakeryCommand cmd, BakeryIfCommand subCmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 2 for condition and 1 for command
            int necessaryOperandNum = BakeryCodeParser.GetIfSubCmdOperandNum((IfSubOpcode)subCmd.SubOpcode);
            if (cmd.Operands.Count < necessaryOperandNum + 1)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);

            string rootKey = UnescapeString(variables.Expand(subCmd.Operands[0]));
            string subKey = UnescapeString(variables.Expand(subCmd.Operands[1]));
            string rawRootKey = subCmd.Operands[0];
            string rawSubKey = subCmd.Operands[1];

            RegistryKey regRoot = RegistryHelper.ParseRootKeyToRegKey(rootKey);
            if (regRoot == null)
                throw new InvalidOperandException($"Invalid registry root key [{rawRootKey}]", cmd);
            RegistryKey regSubKey = regRoot.OpenSubKey(subKey);

            string resMessage;
            bool run = (regSubKey != null);
            if (run) // Exists
            {
                resMessage = $"Registry sub key [{rawRootKey}\\{rawSubKey}] exists";
                regSubKey.Close();
            }
            else
                resMessage = $"Registry sub key [{rawRootKey}\\{rawSubKey}] does not exists";

            RunIfLink(ref logs, (run && !subCmd.NotFlag) || (!run && subCmd.NotFlag), cmd, subCmd, resMessage);

            regRoot.Close();
            return logs.ToArray();
        }

        /// <summary>
        /// If,ExistRegKey,<RegRootKey>,<SubKey>,<ValueName>,<Command>
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="subCmd"></param>
        /// <returns></returns>
        public LogInfo[] IfExistRegKey(BakeryCommand cmd, BakeryIfCommand subCmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 3 for condition and 1 for command
            int necessaryOperandNum = BakeryCodeParser.GetIfSubCmdOperandNum((IfSubOpcode)subCmd.SubOpcode);
            if (cmd.Operands.Count < necessaryOperandNum + 1)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);

            string rootKey = UnescapeString(variables.Expand(subCmd.Operands[0]));
            string subKey = UnescapeString(variables.Expand(subCmd.Operands[1]));
            string valueName = UnescapeString(variables.Expand(subCmd.Operands[2]));
            string rawRootKey = subCmd.Operands[0];
            string rawSubKey = subCmd.Operands[1];
            string rawValueName = subCmd.Operands[2];

            RegistryKey regRoot = RegistryHelper.ParseRootKeyToRegKey(rootKey);
            if (regRoot == null)
                throw new InvalidOperandException($"Invalid registry root key [{rawRootKey}]", cmd);
            object value = regRoot.OpenSubKey(subKey).GetValue(valueName);

            string resMessage;
            bool run = (value != null);
            if (run) // Exists
            {
                resMessage = $"Registry value [{rootKey}\\{subKey}\\{valueName}] exists";
                regRoot.Close();
            }
            else
                resMessage = $"Registry value [{rootKey}\\{subKey}\\{valueName}] does not exists";

            RunIfLink(ref logs, (run && !subCmd.NotFlag) || (!run && subCmd.NotFlag), cmd, subCmd, resMessage);

            regRoot.Close();
            return logs.ToArray();
        }

        /// <summary>
        /// If,ExistVar,<%Variable%>,<Command>
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="subCmd"></param>
        /// <returns></returns>
        public LogInfo[] IfExistVar(BakeryCommand cmd, BakeryIfCommand subCmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 1 for condition and 1 for command
            const int necessaryOperandNum = 1;
            if (cmd.Operands.Count < necessaryOperandNum + 1)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);

            string varName = BakeryVariables.TrimPercentMark(subCmd.Operands[0]);

            string resMessage;
            bool run = variables.ContainsKey(varName);
            if (run) // Exists
                resMessage = $"Varaible [%{varName}%] exists";
            else
                resMessage = $"Varaible [%{varName}%] does not exists";

            RunIfLink(ref logs, (run && !subCmd.NotFlag) || (!run && subCmd.NotFlag), cmd, subCmd, resMessage);

            return logs.ToArray();
        }

        private LogInfo[] ElseCompact(BakeryCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            BakeryCommand embCmd = BakeryCodeParser.ForgeEmbedCommand(cmd, 0, 0);
            if (embCmd.Opcode != Opcode.Link)
                throw new CriticalErrorException($"[{cmd.Opcode.ToString()}] must be used with Link", cmd);

            if (runElse)
            {
                logs.Add(new LogInfo(cmd, LogState.Success, "Running Else"));
                RunCommands(cmd.Link, new List<string>(), cmd.Depth);
                runElse = false;
            }

            return logs.ToArray();
        }
    }
}