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
    }

    /*
     * Warning!!!
     *   This shit of codes needs a looooooooooooot of refactoring!
     *   TODO: Compiler to convert Begin~End into goto-style simplified script (like babel)
     */

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
                CommandAddress nextAddr = new CommandAddress(); // Blank value
                if (callback)
                {
                    nextAddr = new CommandAddress(targetPlugin, targetPlugin.Sections[sectionName], 0, targetPlugin.Sections[sectionName].Count);
                    if (depth != 0)
                    {
                        if (inCurrentPlugin)
                            logger.Write(new LogInfo(cmd, LogState.Success, $"Processing section [{sectionName}]"), true);
                        else
                            logger.Write(new LogInfo(cmd, LogState.Success, $"Processing [{rawPluginFile}]'s section [{sectionName}]"), true);
                    }
                }
                else
                {
                    returnAddress.Push(new CommandAddress(cmd.Address.plugin, cmd.Address.section, cmd.Address.line + 1, cmd.Address.secLength));
                    nextCommand = new CommandAddress(targetPlugin, targetPlugin.Sections[sectionName], -1, targetPlugin.Sections[sectionName].Count);
                    currentSectionParams = parameters;
                    if (inCurrentPlugin)
                        logs.Add(new LogInfo(cmd, LogState.Success, $"Processing section [{sectionName}]"));
                    else
                        logs.Add(new LogInfo(cmd, LogState.Success, $"Processing [{rawPluginFile}]'s section [{sectionName}]"));
                }

                // Exec utilizes [Variables] section of the plugin
                if (cmd.Opcode == Opcode.Exec)
                    variables.AddVariables(VarsType.Local, targetPlugin.Sections["Variables"], returnAddress.Count, true);

                if (callback)
                    RunCallbackSection(nextAddr, parameters, depth);
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
        
        /*
        /// <summary>
        /// Begin
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public LogInfo[] Begin(BakeryCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 0, optional operand : 0
            const int necessaryOperandNum = 0;
            const int optionalOperandNum = 0;
            if (cmd.Operands.Count < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);
            else if (necessaryOperandNum + optionalOperandNum < cmd.Operands.Count)
                throw new InvalidOperandException("Too many operands", cmd);

            beginEndBlockCount++;
            returnAddress.Push(new CommandAddress()); // Push blank address to correct log's indent
            logs.Add(new LogInfo(cmd, LogState.Info, "Begin of code block"));

            return logs.ToArray();
        }

        /// <summary>
        /// End
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public LogInfo[] End(BakeryCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 0, optional operand : 0
            const int necessaryOperandNum = 0;
            const int optionalOperandNum = 0;
            if (cmd.Operands.Count < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);
            else if (necessaryOperandNum + optionalOperandNum < cmd.Operands.Count)
                throw new InvalidOperandException("Too many operands", cmd);

            if (0 < beginEndBlockCount)
                beginEndBlockCount--;
            else
                throw new CriticalErrorException("End must match with Begin", cmd);

            elseFlag = 1;

            returnAddress.Pop(); // Remove blank address to restore log's indent
            logs.Add(new LogInfo(cmd, LogState.Info, "End of code block"));

            return logs.ToArray();
        }

        /// <summary>
        /// Else,<Command>
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public LogInfo[] Else(BakeryCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 0, optional operand : 0+
            const int necessaryOperandNum = 0;
            if (cmd.Operands.Count < necessaryOperandNum + 1)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);

            if (0 <= elseFlag)
            {
                BakeryCommand elseCmd = ForgeEmbedCommand(cmd, 0, returnAddress.Count);
                logs.Add(new LogInfo(cmd, LogState.Success, "Else condition"));
                logs.AddRange(ExecuteCommand(elseCmd));
            }
            else
            {
                throw new CriticalErrorException("Else must be used with If properly", cmd);
            }

            return logs.ToArray();
        }

        */

        /// <summary>
        /// If,<Condition>,<Command>
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public LogInfo[] IfCondition(BakeryCommand cmd)
        {
            // elseFlag = 1;

            LogInfo[] logs = new LogInfo[0]; // TODO: Delete this init sentence if If command is implemented

            // Necessary operand : 3, 2 for condition and 1 for embeded command
            const int necessaryOperandNum = 3;
            if (cmd.Operands.Count < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);

            // Get Condition SubOpcode
            BakeryIfCommand subCmd = ForgeIfSubCommand(cmd);

            // Call sub command methods
            switch ((IfSubOpcode) subCmd.SubOpcode)
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

        public static BakeryIfCommand ForgeIfSubCommand(BakeryCommand cmd)
        {
            // Get Condition SubOpcode
            IfSubOpcode subOpcode = IfSubOpcode.None;
            BakeryIfCommand subCmd;

            // Parse opcode
            int subOpcodeIdx = 0;
            bool notFlag = false;
            string subOpcodeString = cmd.Operands[subOpcodeIdx];

            if (string.Equals(cmd.Operands[0], "Not", StringComparison.OrdinalIgnoreCase))
            {
                notFlag = true;
                subOpcodeIdx++;
            }

            // Check if subOpcodeString starts and ends with % -> Equal, Smaller, Bigger
            if (cmd.Operands[subOpcodeIdx].StartsWith("%") && cmd.Operands[subOpcodeIdx].EndsWith("%"))
            {
                if (cmd.Operands.Count < 4) // 3 for %A%,Equal,%B% and 1 for embeded command
                    throw new InvalidOperandException("Necessary operands does not exist", cmd);

                subOpcodeString = cmd.Operands[subOpcodeIdx + 1];
                if (string.Equals(subOpcodeString, "Equal", StringComparison.OrdinalIgnoreCase) || string.Equals(subOpcodeString, "==", StringComparison.OrdinalIgnoreCase))
                    subOpcode = IfSubOpcode.Equal;
                else if (string.Equals(subOpcodeString, "Smaller", StringComparison.OrdinalIgnoreCase) || string.Equals(subOpcodeString, "<", StringComparison.OrdinalIgnoreCase))
                    subOpcode = IfSubOpcode.Smaller;
                else if (string.Equals(subOpcodeString, "Bigger", StringComparison.OrdinalIgnoreCase) || string.Equals(subOpcodeString, ">", StringComparison.OrdinalIgnoreCase))
                    subOpcode = IfSubOpcode.Bigger;
                else if (string.Equals(subOpcodeString, "SmallerEqual", StringComparison.OrdinalIgnoreCase) || string.Equals(subOpcodeString, "<=", StringComparison.OrdinalIgnoreCase))
                    subOpcode = IfSubOpcode.SmallerEqual;
                else if (string.Equals(subOpcodeString, "BiggerEqual", StringComparison.OrdinalIgnoreCase) || string.Equals(subOpcodeString, ">=", StringComparison.OrdinalIgnoreCase))
                    subOpcode = IfSubOpcode.BiggerEqual;
                else if (string.Equals(subOpcodeString, "NotEqual", StringComparison.OrdinalIgnoreCase) || string.Equals(subOpcodeString, "!=", StringComparison.OrdinalIgnoreCase))
                {
                    if (notFlag)
                        throw new InvalidSubOpcodeException("Condition [Not] cannot be duplicated", cmd);
                    notFlag = true;
                    subOpcode = IfSubOpcode.Equal;
                }
                else if (string.Equals(subOpcodeString, "EqualX", StringComparison.OrdinalIgnoreCase)) // deprecated 
                    subOpcode = IfSubOpcode.EqualX;
                else
                    throw new InvalidSubOpcodeException($"Invalid sub command [If,{subOpcodeString}]", cmd);

                // Ex) If,%Joveler%,Equal,ied206,Set,%A%,True
                // -> new string[] { "%Joveler%",ied206,Set,%A%,True}
                List<string> operandList = new List<string>();
                operandList.Add(cmd.Operands[subOpcodeIdx]);
                operandList.AddRange(cmd.Operands.Skip(subOpcodeIdx + 2));
                subCmd = new BakeryIfCommand(subOpcode, operandList.ToArray(), notFlag);
            }
            else
            {
                // Get condition SubOpcode string
                subOpcodeString = cmd.Operands[subOpcodeIdx];
                if (string.Equals(subOpcodeString, "ExistFile", StringComparison.OrdinalIgnoreCase))
                    subOpcode = IfSubOpcode.ExistFile;
                else if (string.Equals(subOpcodeString, "ExistDir", StringComparison.OrdinalIgnoreCase))
                    subOpcode = IfSubOpcode.ExistDir;
                else if (string.Equals(subOpcodeString, "ExistSection", StringComparison.OrdinalIgnoreCase))
                    subOpcode = IfSubOpcode.ExistSection;
                else if (string.Equals(subOpcodeString, "ExistRegSection", StringComparison.OrdinalIgnoreCase))
                    subOpcode = IfSubOpcode.ExistRegSection;
                else if (string.Equals(subOpcodeString, "ExistRegKey", StringComparison.OrdinalIgnoreCase))
                    subOpcode = IfSubOpcode.ExistRegKey;
                else if (string.Equals(subOpcodeString, "ExistVar", StringComparison.OrdinalIgnoreCase))
                    subOpcode = IfSubOpcode.ExistVar;
                else if (string.Equals(subOpcodeString, "NotExistFile", StringComparison.OrdinalIgnoreCase))
                {
                    if (notFlag)
                        throw new InvalidSubOpcodeException("Condition [Not] cannot be duplicated", cmd); // deprecated 
                    notFlag = true;
                    subOpcode = IfSubOpcode.ExistFile;
                }
                else if (string.Equals(subOpcodeString, "NotExistDir", StringComparison.OrdinalIgnoreCase))
                {
                    if (notFlag)
                        throw new InvalidSubOpcodeException("Condition [Not] cannot be duplicated", cmd); // deprecated 
                    notFlag = true;
                    subOpcode = IfSubOpcode.ExistDir;
                }
                else if (string.Equals(subOpcodeString, "NotExistSection", StringComparison.OrdinalIgnoreCase))
                {
                    if (notFlag)
                        throw new InvalidSubOpcodeException("Condition [Not] cannot be duplicated", cmd); // deprecated 
                    notFlag = true;
                    subOpcode = IfSubOpcode.ExistSection;
                }
                else if (string.Equals(subOpcodeString, "NotExistRegSection", StringComparison.OrdinalIgnoreCase)) // deprecated
                {
                    if (notFlag)
                        throw new InvalidSubOpcodeException("Condition [Not] cannot be duplicated", cmd);
                    notFlag = true;
                    subOpcode = IfSubOpcode.ExistRegSection;
                }
                else if (string.Equals(subOpcodeString, "NotExistRegKey", StringComparison.OrdinalIgnoreCase)) // deprecated 
                {
                    if (notFlag)
                        throw new InvalidSubOpcodeException("Condition [Not] cannot be duplicated", cmd);
                    notFlag = true;
                    subOpcode = IfSubOpcode.ExistRegKey;
                }
                else if (string.Equals(subOpcodeString, "NotExistVar", StringComparison.OrdinalIgnoreCase))  // deprecated 
                {
                    if (notFlag)
                        throw new InvalidSubOpcodeException("Condition [Not] cannot be duplicated", cmd);
                    notFlag = true;
                    subOpcode = IfSubOpcode.ExistVar;
                }
                else if (string.Equals(subOpcodeString, "Ping", StringComparison.OrdinalIgnoreCase))
                    subOpcode = IfSubOpcode.Ping;
                else if (string.Equals(subOpcodeString, "Online", StringComparison.OrdinalIgnoreCase))
                    subOpcode = IfSubOpcode.Online;
                else
                    throw new InvalidSubOpcodeException($"Invalid sub command [If,{subOpcodeString}]", cmd);
                subCmd = new BakeryIfCommand(subOpcode, cmd.Operands.Skip(subOpcodeIdx + 1).ToArray(), notFlag);
            }

            return subCmd;
        }

        public static BakeryCommand ForgeIfEmbedCommand(BakeryCommand cmd, int depth)
        {
            BakeryIfCommand subCmd = ForgeIfSubCommand(cmd);
            int necessaryOperandNum = GetIfOperandNum(cmd, subCmd);
            return BakeryEngine.ForgeEmbedCommand(cmd, necessaryOperandNum + 1, depth);
        }

        public static BakeryCommand ForgeIfEmbedCommand(BakeryCommand cmd, BakeryIfCommand subCmd, int depth)
        {
            int necessaryOperandNum = GetIfOperandNum(cmd, subCmd);
            return BakeryEngine.ForgeEmbedCommand(cmd, necessaryOperandNum + 1, depth);
        }

        public static BakeryCommand ForgeIfConditionCommand(BakeryCommand cmd)
        {
            BakeryIfCommand subCmd = BakeryEngine.ForgeIfSubCommand(cmd);
            BakeryCommand embCmd = BakeryEngine.ForgeIfEmbedCommand(cmd, subCmd, 0);
            int operandCount = BakeryEngine.GetIfSubCmdOperandNum(subCmd.SubOpcode);
            return new BakeryCommand(Opcode.If, cmd.Operands.Take(operandCount + 1).ToList()); // 1 for sub opcode itself
        }

        public static BakeryCommand ForgeEmbedCommand(BakeryCommand cmd, int opcodeIdx, int depth)
        {
            // If,   ExistFile,Joveler.txt,Echo,ied206
            // [cmd] 0,        1,          2,   3 -> opcodeIdx must be 2 

            // Parse opcode
            Opcode opcode = Opcode.None;
            string opcodeStr = cmd.Operands[opcodeIdx];
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

            int cmdDepth = depth + 1;
            if (opcode == Opcode.Run)
                cmdDepth -= 1;

            List<string> operands;
            if (opcodeIdx == 0 && cmd.Operands.Count == 1) // Ex) Begin
                operands = new List<string>();
            else // Ex) Set,%A%,B
                operands = cmd.Operands.Skip(opcodeIdx + 1).ToList();
            return new BakeryCommand(cmd.Origin, opcode, operands, cmd.Address, cmdDepth);
        }

        private void ProcessIfEmbeddedCommand(ref List<LogInfo> logs, bool run, BakeryCommand cmd, BakeryIfCommand subCmd, string message)
        { // ref for performance
            int necessaryOperandNum = GetIfOperandNum(cmd, subCmd);
            BakeryCommand ifCmd = ForgeEmbedCommand(cmd, necessaryOperandNum + 1, returnAddress.Count);
            if (ifCmd.Opcode == Opcode.End)
                throw new CriticalErrorException("End cannot be used with If", cmd);

            if (run)
            {
                logs.Add(new LogInfo(cmd, LogState.Success, message));
                logs.AddRange(ExecuteCommand(ifCmd));
            }
            else // Do not run
            {
                logs.Add(new LogInfo(cmd, LogState.Ignore, message));
                if (ifCmd.Opcode == Opcode.If || ifCmd.Opcode == Opcode.Begin)
                { // Find End
                    nextCommand = MatchBeginWithProperEnd(cmd.Address);
                }
            }
        }

        /// <summary>
        /// Inspect codes to be processed to find out proper End's address
        /// </summary>
        /// <param name="addr"></param>
        /// <returns></returns>
        private CommandAddress MatchBeginWithProperEnd(CommandAddress addr)
        { // To process nested Begin~End block
            int nestedBeginEnd = 0;
            int i = addr.line;
            for (; i < addr.secLength; i++)
            {
                string rawCode = (addr.section.Get() as string[])[i].Trim();
                try
                {
                    BakeryCommand cmd = ParseCommand(rawCode, new CommandAddress(addr.plugin, addr.section, i, addr.secLength));
                    if (cmd.Opcode == Opcode.If) // To check If,<Condition>,Begin
                    {
                        while (true)
                        {
                            BakeryIfCommand subCmd = ForgeIfSubCommand(cmd);
                            int necessaryOperandNum = GetIfOperandNum(cmd, subCmd);
                            BakeryCommand embedded = ForgeEmbedCommand(cmd, necessaryOperandNum + 1, returnAddress.Count);
                            if (embedded.Opcode == Opcode.If) // Nested If
                            {
                                cmd = embedded;
                                continue;
                            }
                            else if (embedded.Opcode == Opcode.Begin)
                                nestedBeginEnd++;
                            break;
                        }
                    }
                    else if (cmd.Opcode == Opcode.Else)
                    {
                        BakeryCommand embedded = ForgeEmbedCommand(cmd, 0, returnAddress.Count);
                        if (embedded.Opcode == Opcode.If) // Nested If
                        {
                            while (true)
                            {
                                BakeryIfCommand subCmd = ForgeIfSubCommand(cmd);
                                int necessaryOperandNum = GetIfOperandNum(cmd, subCmd);
                                embedded = ForgeEmbedCommand(cmd, necessaryOperandNum + 1, returnAddress.Count);
                                if (embedded.Opcode == Opcode.If) // Nested If
                                {
                                    cmd = embedded;
                                    continue;
                                }
                                else if (embedded.Opcode == Opcode.Begin)
                                    nestedBeginEnd++;
                                break;
                            }
                        }
                        else if (embedded.Opcode == Opcode.Begin)
                            nestedBeginEnd++;
                    }
                    else if (cmd.Opcode == Opcode.Begin)
                    {
                        throw new CriticalErrorException("Begin must be used with If, Else", cmd);
                    }
                    else if (cmd.Opcode == Opcode.End)
                    {
                        nestedBeginEnd--;
                        if (nestedBeginEnd == 0)
                            break;
                    }
                }
                catch (Exception)
                {
                    // Tempororay measure
                    // throw new CriticalErrorException(e.GetType() + ": " + Helper.RemoveLastNewLine(e.Message));
                }
            }

            if (nestedBeginEnd == 0) // Success
            {
                // elseFlag = 1;
                return new CommandAddress(addr.plugin, addr.section, i, addr.secLength);
            }
            else
                throw new CriticalErrorException("End must match with Begin");
        }

        public static int GetIfOperandNum(BakeryCommand cmd, BakeryIfCommand subCmd)
        {
            int necessaryOperandNum = GetIfSubCmdOperandNum((IfSubOpcode) subCmd.SubOpcode);
            if (1 <= cmd.Operands.Count && string.Equals(cmd.Operands[0], "Not", StringComparison.OrdinalIgnoreCase))
                necessaryOperandNum += 1;
            return necessaryOperandNum;
        }

        public static int GetIfSubCmdOperandNum(IfSubOpcode subOpcode)
        {
            switch(subOpcode)
            {
                case IfSubOpcode.Equal:
                case IfSubOpcode.Smaller:
                case IfSubOpcode.Bigger:
                case IfSubOpcode.SmallerEqual:
                case IfSubOpcode.BiggerEqual:
                    return 2;
                case IfSubOpcode.ExistFile:
                    return 1;
                case IfSubOpcode.ExistDir:
                    return 1;
                case IfSubOpcode.ExistSection:
                    return 2;
                case IfSubOpcode.ExistRegSection:
                    return 2;
                case IfSubOpcode.ExistRegKey:
                    return 3;
                case IfSubOpcode.ExistVar:
                    return 1;
                case IfSubOpcode.Ping:
                    return 0; // Not implemented
                case IfSubOpcode.Online:
                    return 0; // Not implemented
                default: // If this logic is called in production, it is definitely a BUG
                    return -1;
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
            int necessaryOperandNum = GetIfSubCmdOperandNum((IfSubOpcode) subCmd.SubOpcode);
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

            ProcessIfEmbeddedCommand(ref logs, run, cmd, subCmd, resMessage);

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
            int necessaryOperandNum = GetIfSubCmdOperandNum((IfSubOpcode)subCmd.SubOpcode);
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

            ProcessIfEmbeddedCommand(ref logs, (run && !subCmd.NotFlag) || (!run && subCmd.NotFlag), cmd, subCmd, resMessage);

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
            int necessaryOperandNum = GetIfSubCmdOperandNum((IfSubOpcode)subCmd.SubOpcode);
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

            ProcessIfEmbeddedCommand(ref logs, (run && !subCmd.NotFlag) || (!run && subCmd.NotFlag), cmd, subCmd, resMessage);

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
            int necessaryOperandNum = GetIfSubCmdOperandNum((IfSubOpcode)subCmd.SubOpcode);
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

            ProcessIfEmbeddedCommand(ref logs, (run && !subCmd.NotFlag) || (!run && subCmd.NotFlag), cmd, subCmd, resMessage);

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
            int necessaryOperandNum = GetIfSubCmdOperandNum((IfSubOpcode)subCmd.SubOpcode);
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

            ProcessIfEmbeddedCommand(ref logs, (run && !subCmd.NotFlag) || (!run && subCmd.NotFlag), cmd, subCmd, resMessage);

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
            int necessaryOperandNum = GetIfSubCmdOperandNum((IfSubOpcode)subCmd.SubOpcode);
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

            ProcessIfEmbeddedCommand(ref logs, (run && !subCmd.NotFlag) || (!run && subCmd.NotFlag), cmd, subCmd, resMessage);

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

            ProcessIfEmbeddedCommand(ref logs, (run && !subCmd.NotFlag) || (!run && subCmd.NotFlag), cmd, subCmd, resMessage);

            return logs.ToArray();
        }
    }
}