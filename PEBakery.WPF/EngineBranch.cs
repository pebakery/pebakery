/*
    Copyright (C) 2016-2017 Hajin Jang
    Licensed under GPL 3.0
 
    PEBakery is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Win32;
using PEBakery.Exceptions;

namespace PEBakery.Core
{
    public enum IfSubOpcode
    {
        None = 0,
        ExistFile, ExistDir, ExistSection, ExistRegSection, ExistRegKey, ExistVar, ExistMacro,
        Equal, Smaller, Bigger, SmallerEqual, BiggerEqual,
        Not,
        Ping, Online, Question,
        // Deprecated
        EqualX, License, ExistRegMulti
    }

    public class IfCommand
    {
        // For IfCondition
        public IfSubOpcode SubOpcode;
        public List<string> Operands;
        public bool NotFlag;

        public IfCommand(IfSubOpcode subOpcode, List<string> operands, bool notFlag)
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
            operands.AddRange(Operands.Take(CodeParser.GetIfSubCmdOperandNum(SubOpcode)));
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
            operands.AddRange(Operands.Take(CodeParser.GetIfSubCmdOperandNum(SubOpcode)));
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
            operands.AddRange(Operands.Take(CodeParser.GetIfSubCmdOperandNum(SubOpcode)));
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
            operands.AddRange(Operands.Take(CodeParser.GetIfSubCmdOperandNum(SubOpcode)));
            return operands;
        }

        public List<string> ToOperandsPrefix(List<string> add)
        {
            List<string> operands = new List<string>();
            operands.AddRange(add);
            if (NotFlag)
                operands.Add("Not");
            operands.Add(SubOpcode.ToString());
            operands.AddRange(Operands.Take(CodeParser.GetIfSubCmdOperandNum(SubOpcode)));
            return operands;
        }
        public List<string> ToOperandsPostfix(string add)
        {
            List<string> operands = new List<string>();
            if (NotFlag)
                operands.Add("Not");
            operands.Add(SubOpcode.ToString());
            operands.AddRange(Operands.Take(CodeParser.GetIfSubCmdOperandNum(SubOpcode)));
            operands.Add(add);
            return operands;
        }

        public List<string> ToOperandsPostfix(List<string> add)
        {
            List<string> operands = new List<string>();
            if (NotFlag)
                operands.Add("Not");
            operands.Add(SubOpcode.ToString());
            operands.AddRange(Operands.Take(CodeParser.GetIfSubCmdOperandNum(SubOpcode)));
            operands.AddRange(add);
            return operands;
        }
    }
    /*
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
        private void CmdRunExec(Command cmd)
        {
            RunExec(cmd, cmd.Depth + 1, false);
        }

        /// <summary>
        /// Run,%PluginFile%,<Section>[,PARAMS]
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        private void RunExec(Command cmd, int depth, bool callback)
        {
            // Necessary operand : 2, optional operand : variable length
            const int necessaryOperandNum = 2;
            if (cmd.Operands.Count < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);

            // Get necesssary operand
            string pluginFile = UnescapeString(ExpandVariables(cmd.Operands[0]));
            string sectionName = UnescapeString(ExpandVariables(cmd.Operands[1]));
            string rawPluginFile = cmd.Operands[0];
            string rawSectoinName = cmd.Operands[1];

            // Get parameters 
            List<string> parameters = new List<string>();
            if (necessaryOperandNum < cmd.Operands.Count)
                parameters.AddRange(cmd.Operands.Skip(2));
            for (int i = 0; i < parameters.Count; i++)
                parameters[i] = ExpandVariables(parameters[i]);

            bool inCurrentPlugin = false;
            if (string.Equals(rawPluginFile, "%PluginFile%", StringComparison.OrdinalIgnoreCase))
                inCurrentPlugin = true;
            else if (string.Equals(rawPluginFile, "%ScriptFile%", StringComparison.OrdinalIgnoreCase))
                inCurrentPlugin = true;

            Plugin targetPlugin;
            if (inCurrentPlugin)
                targetPlugin = currentPlugin;
            else
                targetPlugin = project.AllPlugins.SearchByFullPath(ExpandVariables(pluginFile));

            // Does section exists?
            if (!targetPlugin.Sections.ContainsKey(sectionName))
                throw new InvalidOperandException($"[{rawPluginFile}] does not have section [{sectionName}]", cmd);

            // Branch to new section
            SectionAddress nextAddr = new SectionAddress(); // Blank value
            nextAddr = new SectionAddress(targetPlugin, targetPlugin.Sections[sectionName]);

            if (inCurrentPlugin)
                logger.Write(new LogInfo(cmd, LogState.Success, $"Processing section [{sectionName}]"), true);
            else
                logger.Write(new LogInfo(cmd, LogState.Success, $"Processing [{rawPluginFile}]'s section [{sectionName}]"), true);

            // Exec utilizes [Variables] section of the plugin
            if (cmd.Opcode == Opcode.Exec)
                variables.AddVariables(VarsType.Local, targetPlugin.Sections["Variables"], depth, true);

            // Run Section
            RunSection(nextAddr, parameters, depth, callback);
        }

        /// <summary>
        /// IfCompact,<Condition>,Link
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public void CmdIfCompact(Command cmd)
        {
            // Necessary operand : 3, 2 for condition and 1 for Link
            const int necessaryOperandNum = 3;
            if (cmd.Operands.Count < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);

            // Get Condition SubOpcode
            IfCommand subCmd = CodeParser.ForgeIfSubCommand(cmd, false);

            // Call sub command methods
            switch (subCmd.SubOpcode)
            {
                case IfSubOpcode.Equal:
                case IfSubOpcode.Smaller:
                case IfSubOpcode.Bigger:
                case IfSubOpcode.SmallerEqual:
                case IfSubOpcode.BiggerEqual:
                    this.IfCompare(cmd, subCmd);
                    break;
                case IfSubOpcode.ExistFile:
                    this.IfExistFile(cmd, subCmd);
                    break;
                case IfSubOpcode.ExistDir:
                    this.IfExistDir(cmd, subCmd);
                    break;
                case IfSubOpcode.ExistSection:
                    this.IfExistSection(cmd, subCmd);
                    break;
                case IfSubOpcode.ExistRegSection:
                    this.IfExistRegSection(cmd, subCmd);
                    break;
                case IfSubOpcode.ExistRegKey:
                    this.IfExistRegKey(cmd, subCmd);
                    break;
                case IfSubOpcode.ExistVar:
                    this.IfExistVar(cmd, subCmd);
                    break;
                case IfSubOpcode.Ping:
                    break;
                case IfSubOpcode.Online:
                    break;
                default: // In fact, Enum.Parse logic must handle this. If this logic is called in production, it is definitely a BUG
                    throw new InvalidSubOpcodeException($"INTERNAL ERROR! Invalid sub command [If,{subCmd.SubOpcode}]", cmd);
            }
        }

        private void RunIfLink(bool run, Command cmd, IfCommand subCmd, string message)
        {
            int necessaryOperandNum = CodeParser.GetIfOperandNum(cmd, subCmd);
            Command embCmd = CodeParser.ForgeEmbedCommand(cmd, necessaryOperandNum + 1, cmd.Depth);
            if (embCmd.Opcode != Opcode.Link)
                throw new CriticalErrorException($"[{cmd.Opcode.ToString()}] must be used with Link", cmd);

            if (run)
            {
                runElse = false;
                logger.Write(new LogInfo(cmd, LogState.Success, message));
                RunCommands(cmd.Link, curSectionParams, cmd.Depth + 1);
            }
            else // Do not run
            {
                runElse = true;
                logger.Write(new LogInfo(cmd, LogState.Ignore, message));
            }
        }

        private void CmdElseCompact(Command cmd)
        {
            Command embCmd = CodeParser.ForgeEmbedCommand(cmd, 0, 0);
            if (embCmd.Opcode != Opcode.Link)
                throw new CriticalErrorException($"[{cmd.Opcode.ToString()}] must be used with Link", cmd);

            if (runElse)
            {
                logger.Write(new LogInfo(cmd, LogState.Success, "Else condition met"));
                RunCommands(cmd.Link, new List<string>(), cmd.Depth + 1);
                runElse = false;
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
        public void IfCompare(Command cmd, IfCommand subCmd)
        {
            // Necessary operand : 2 for condition and 1 for command
            int necessaryOperandNum = CodeParser.GetIfSubCmdOperandNum(subCmd.SubOpcode);
            if (cmd.Operands.Count < necessaryOperandNum + 1)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);

            string value1 = UnescapeString(ExpandVariables(subCmd.Operands[0]));
            string value2 = UnescapeString(ExpandVariables(subCmd.Operands[1]));

            string resMessage;
            CompareStringNumberResult comp = NumberHelper.CompareStringNumber(value1, value2);

            bool run = false;
            bool notEqual = ((comp & CompareStringNumberResult.NotEqual) == CompareStringNumberResult.NotEqual);
            if ((comp & CompareStringNumberResult.Equal) != 0)
            {
                if (subCmd.SubOpcode == IfSubOpcode.Equal
                    || subCmd.SubOpcode == IfSubOpcode.SmallerEqual
                    || subCmd.SubOpcode == IfSubOpcode.BiggerEqual)
                    run = true;
                resMessage = $"[{value1}] is equal to [{value2}]";
            }
            else if ((comp & CompareStringNumberResult.Smaller) != 0)
            {
                if (subCmd.SubOpcode == IfSubOpcode.Smaller
                    || subCmd.SubOpcode == IfSubOpcode.SmallerEqual
                    || subCmd.SubOpcode == IfSubOpcode.Bigger && subCmd.NotFlag
                    || subCmd.SubOpcode == IfSubOpcode.BiggerEqual && subCmd.NotFlag)
                    run = true;
                resMessage = $"[{value1}] is smaller than [{value2}]";
            }
            else if ((comp & CompareStringNumberResult.Bigger) != 0)
            {
                if (subCmd.SubOpcode == IfSubOpcode.Bigger
                    || subCmd.SubOpcode == IfSubOpcode.BiggerEqual
                    || subCmd.SubOpcode == IfSubOpcode.Smaller && subCmd.NotFlag
                    || subCmd.SubOpcode == IfSubOpcode.SmallerEqual && subCmd.NotFlag)
                    run = true;
                resMessage = $"[{value1}] is bigger than [{value2}]";
            }
            else if ((comp & CompareStringNumberResult.NotEqual) != 0)
            {
                if (subCmd.SubOpcode == IfSubOpcode.Equal && subCmd.NotFlag)
                    run = true;
                resMessage = $"[{value1}] is not equal to [{value2}]";
            }
            else
                throw new InternalUnknownException($"Cannot compare [{value1}] and [{value2}]");

            RunIfLink(run, cmd, subCmd, resMessage);
        }

        /// <summary>
        /// If,ExistFile,<FilePath>,<Command>
        /// </summary>
        /// <remarks>Support wildcard</remarks>
        /// <param name="cmd"></param>
        /// <param name="subCmd"></param>
        /// <returns></returns>
        public void IfExistFile(Command cmd, IfCommand subCmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 1 for condition and 1 for command
            int necessaryOperandNum = CodeParser.GetIfSubCmdOperandNum(subCmd.SubOpcode);
            if (cmd.Operands.Count < necessaryOperandNum + 1)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);

            string filePath = UnescapeString(ExpandVariables(subCmd.Operands[0]));
            string rawFilePath = subCmd.Operands[0];

            // Check filePath contains wildcard
            bool filePathContainsWildcard = true;
            if (filePath.IndexOfAny(new char[] { '*', '?' }) == -1) // No wildcard
                filePathContainsWildcard = false;

            // Check if file exists
            bool run;
            if (filePathContainsWildcard)
            {
                string[] list = Directory.GetFiles(FileHelper.GetDirNameEx(filePath), Path.GetFileName(filePath));
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

            RunIfLink((run && !subCmd.NotFlag) || (!run && subCmd.NotFlag), cmd, subCmd, resMessage);
        }

        /// <summary>
        /// If,ExistDir,<DirPath>,<Command>
        /// </summary>
        /// <remarks>Support wildcard</remarks>
        /// <param name="cmd"></param>
        /// <param name="subCmd"></param>
        /// <returns></returns>
        public void IfExistDir(Command cmd, IfCommand subCmd)
        {
            // Necessary operand : 1 for condition and 1 for command
            int necessaryOperandNum = CodeParser.GetIfSubCmdOperandNum(subCmd.SubOpcode);
            if (cmd.Operands.Count < necessaryOperandNum + 1)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);

            string dirPath = UnescapeString(ExpandVariables(subCmd.Operands[0]));
            string rawFilePath = subCmd.Operands[0];

            // Check filePath contains wildcard
            bool dirPathContainsWildcard = true;
            if (dirPath.IndexOfAny(new char[] { '*', '?' }) == -1) // No wildcard
                dirPathContainsWildcard = false;

            // Check if file exists
            bool run;
            if (dirPathContainsWildcard)
            {
                string[] list = Directory.GetDirectories(FileHelper.GetDirNameEx(dirPath), Path.GetFileName(dirPath));
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

            RunIfLink((run && !subCmd.NotFlag) || (!run && subCmd.NotFlag), cmd, subCmd, resMessage);
        }

        /// <summary>
        /// If,ExistSection,<IniFile>,<Section>,<Command>
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="subCmd"></param>
        /// <returns></returns>
        public void IfExistSection(Command cmd, IfCommand subCmd)
        {
            // Necessary operand : 2 for condition and 1 for command
            int necessaryOperandNum = CodeParser.GetIfSubCmdOperandNum(subCmd.SubOpcode);
            if (cmd.Operands.Count < necessaryOperandNum + 1)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);

            string iniFile = UnescapeString(ExpandVariables(subCmd.Operands[0]));
            string section = UnescapeString(ExpandVariables(subCmd.Operands[1]));
            string rawIniFile = subCmd.Operands[0];
            string rawSection = subCmd.Operands[1];

            string resMessage;
            bool run = IniFile.CheckSectionExist(iniFile, section);
            if (run) // Exists
                resMessage = $"Section [{rawSection}] exists in [{rawIniFile}]";
            else
                resMessage = $"Section [{rawSection}] does not exists in [{rawIniFile}]";

            RunIfLink((run && !subCmd.NotFlag) || (!run && subCmd.NotFlag), cmd, subCmd, resMessage);
        }

        /// <summary>
        /// If,ExistRegSection,<RegRootKey>,<SubKey>,<Command>
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="subCmd"></param>
        /// <returns></returns>
        public void IfExistRegSection(Command cmd, IfCommand subCmd)
        {
            // Necessary operand : 2 for condition and 1 for command
            int necessaryOperandNum = CodeParser.GetIfSubCmdOperandNum(subCmd.SubOpcode);
            if (cmd.Operands.Count < necessaryOperandNum + 1)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);

            string rootKey = UnescapeString(ExpandVariables(subCmd.Operands[0]));
            string subKey = UnescapeString(ExpandVariables(subCmd.Operands[1]));
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

            RunIfLink((run && !subCmd.NotFlag) || (!run && subCmd.NotFlag), cmd, subCmd, resMessage);

            regRoot.Close();
        }

        /// <summary>
        /// If,ExistRegKey,<RegRootKey>,<SubKey>,<ValueName>,<Command>
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="subCmd"></param>
        /// <returns></returns>
        public void IfExistRegKey(Command cmd, IfCommand subCmd)
        {
            // Necessary operand : 3 for condition and 1 for command
            int necessaryOperandNum = CodeParser.GetIfSubCmdOperandNum(subCmd.SubOpcode);
            if (cmd.Operands.Count < necessaryOperandNum + 1)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);

            string rootKey = UnescapeString(ExpandVariables(subCmd.Operands[0]));
            string subKey = UnescapeString(ExpandVariables(subCmd.Operands[1]));
            string valueName = UnescapeString(ExpandVariables(subCmd.Operands[2]));
            string rawRootKey = subCmd.Operands[0];
            string rawSubKey = subCmd.Operands[1];
            string rawValueName = subCmd.Operands[2];

            bool run = true;
            RegistryKey regRoot = RegistryHelper.ParseRootKeyToRegKey(rootKey);
            if (regRoot == null)
                throw new InvalidOperandException($"Invalid registry root key [{rawRootKey}]", cmd);
            else
            {
                RegistryKey regSubKey = regRoot.OpenSubKey(subKey);
                if (regSubKey == null)
                    run = false;
                else
                {
                    object value = regSubKey.GetValue(valueName);
                    if (value == null)
                        run = false;
                    regSubKey.Close();
                }
                regRoot.Close();
            }
            
            string resMessage;
            if (run) // Exists
            {
                resMessage = $"Registry value [{rootKey}\\{subKey}\\{valueName}] exists";
                regRoot.Close();
            }
            else
                resMessage = $"Registry value [{rootKey}\\{subKey}\\{valueName}] does not exists";

            RunIfLink((run && !subCmd.NotFlag) || (!run && subCmd.NotFlag), cmd, subCmd, resMessage);

            
        }

        /// <summary>
        /// If,ExistVar,<%Variable%>,<Command>
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="subCmd"></param>
        /// <returns></returns>
        public void IfExistVar(Command cmd, IfCommand subCmd)
        {
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

            RunIfLink((run && !subCmd.NotFlag) || (!run && subCmd.NotFlag), cmd, subCmd, resMessage);
        }
    }

    */
}