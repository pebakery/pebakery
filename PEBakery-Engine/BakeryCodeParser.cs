using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BakeryEngine
{
    /// <summary>
    /// BakeryCodeParser cannot continue parsing due to malformed command
    /// </summary>
    /// <remarks>
    /// Throw this if BakeryCommand is not forged
    /// </remarks>
    public class InvalidCommandException : Exception
    {
        public InvalidCommandException() { }
        public InvalidCommandException(string message) : base(message) { }
        public InvalidCommandException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// BakeryCodeParser cannot continue parsing due to malformed grammar of command, especially If and Else
    /// </summary>
    /// <remarks>
    /// Throw this if BakeryCommand is already forged
    /// </remarks>
    public class InvalidGrammarException : Exception
    {
        private BakeryCommand cmd;
        public BakeryCommand Cmd { get { return cmd; } }
        public InvalidGrammarException() { }
        public InvalidGrammarException(string message) : base(message) { }
        public InvalidGrammarException(BakeryCommand cmd) { this.cmd = cmd; }
        public InvalidGrammarException(string message, BakeryCommand cmd) : base(message) { this.cmd = cmd; }
        public InvalidGrammarException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// The compiler to convert If~Else, Begin~End to If+Jump (assembly) style
    /// </summary>
    public static class BakeryCodeParser
    {
        public static List<BakeryCommand> ParseRawLines(List<string> lines, SectionAddress addr)
        {
            // Select Code sections and compile
            List<BakeryCommand> rawCodeList = new List<BakeryCommand>();
            for (int i = 0; i < lines.Count; i++)
                rawCodeList.Add(ParseCommand(lines, ref i, addr));

            List<BakeryCommand> compiledList = rawCodeList;
            while (ParseRawLinesOnce(compiledList, out compiledList, addr));
            return compiledList;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="rawCmdList"></param>
        /// <param name="compiledList"></param>
        /// <returns>Return true if this section need more iterate</returns>
        public static bool ParseRawLinesOnce(List<BakeryCommand> rawCmdList, out List<BakeryCommand> compiledList, SectionAddress addr)
        {
            compiledList = new List<BakeryCommand>();
            bool elseFlag = false;
            bool iterate = false;

            for (int i = 0; i < rawCmdList.Count; i++)
            {
                BakeryCommand cmd = rawCmdList[i];
                if (cmd.Opcode == Opcode.If)
                {
                    int dest = CompileNestedIf(cmd, out elseFlag, ref rawCmdList, i, ref compiledList, addr);
                    i = dest;
                    iterate = true;
                }
                else if (cmd.Opcode == Opcode.Else) // SingleLine or MultiLine?
                {
                    if (elseFlag)
                    {
                        int dest = CompileNestedElse(cmd, out elseFlag, ref rawCmdList, i, ref compiledList, addr);
                        i = dest;
                        iterate = true;
                    }
                    else
                        throw new InvalidGrammarException("Else must be used after If", cmd);
                }
                else if (cmd.Opcode == Opcode.IfCompact || cmd.Opcode == Opcode.ElseCompact)
                { // Follow Link
                    if (ParseRawLinesOnce(cmd.Link, out cmd.Link, addr))
                        iterate = true;
                    compiledList.Add(cmd);
                }
                else if (cmd.Opcode == Opcode.Begin)
                    throw new InvalidGrammarException("Begin must be used with If or Else", cmd);
                else if (cmd.Opcode == Opcode.End)
                    throw new InvalidGrammarException("End must be matched with Begin", cmd);
                else
                { // The other operands - just copy
                    compiledList.Add(cmd);
                }
            }
            return iterate;
        }

        private static int CompileNestedIf(BakeryCommand cmd, out bool elseFlag, ref List<BakeryCommand> cmdList, int cmdListIdx, ref List<BakeryCommand> compiledList, SectionAddress addr)
        {
            BakeryCommand ifCmd = cmd; // RawCode : If,%A%,Equal,B,Echo,Success
            BakeryIfCommand ifSubCmd; // Condition : Equal,%A%,B,Echo,Success
            BakeryCommand ifEmbCmd; // Run if condition is met : Echo,Success
            // BakeryCommand compiledCmd; // Compiled If : IfCompact,Equal,%A%,B
            List<BakeryCommand> ifCmdList = compiledList;
            elseFlag = false;

            // <Raw>
            // If,%A%,Equal,B,Echo,Success
            int depth = 0;
            while (true)
            {
                ifSubCmd = ForgeIfSubCommand(ifCmd, true); 
                ifEmbCmd = ForgeIfEmbedCommand(ifCmd, ifSubCmd, 0);

                // Ex) IfCompact,Equal,%A%,B
                BakeryCommand ifCompiledCmd = new BakeryCommand(cmd.Origin, Opcode.IfCompact, ifSubCmd.ToOperandsPostfix(Opcode.Link.ToString()), addr, depth, new List<BakeryCommand>());
                ifCmdList.Add(ifCompiledCmd);

                if (ifEmbCmd.Opcode == Opcode.If) // Nested If
                {
                    ifCmd = ifEmbCmd;
                    ifCmdList = ifCompiledCmd.Link;
                    depth++;
                    continue;
                }
                else if (ifEmbCmd.Opcode == Opcode.Begin) // Multiline If (Begin-End)
                {
                    // Find proper End
                    int endIdx = MatchBeginWithEnd(ref cmdList, cmdListIdx);
                    if (endIdx == -1)
                        throw new InvalidGrammarException("End must be matched with Begin", cmd);
                    for (int i = cmdListIdx + 1; i < endIdx; i++)
                        ifCompiledCmd.Link.Add(cmdList[i]);
                    elseFlag = true; // Enable Else
                    return endIdx;
                }
                else // Singleline If
                {
                    ifCompiledCmd.Link.Add(ifEmbCmd);
                    elseFlag = true; // Enable Else
                    return cmdListIdx;
                }
            }
        }

        private static int CompileNestedElse(BakeryCommand cmd, out bool elseFlag, ref List<BakeryCommand> cmdList, int cmdListIdx, ref List<BakeryCommand> compiledList, SectionAddress addr)
        {
            BakeryCommand elseEmbCmd = ForgeEmbedCommand(cmd, 0, 0);
            BakeryCommand compiledCmd = new BakeryCommand(cmd.Origin, Opcode.ElseCompact, new List<string>(), addr, 0, new List<BakeryCommand>());
            compiledCmd.Operands.Add(Opcode.Link.ToString());
            compiledList.Add(compiledCmd);

            if (elseEmbCmd.Opcode == Opcode.If) // Nested If
            {
                int depth = 0;
                
                BakeryCommand ifCmd = elseEmbCmd; // RawCode : If,%A%,Equal,B,Echo,Success
                BakeryIfCommand ifSubCmd; // Condition : Equal,%A%,B,Echo,Success
                BakeryCommand ifEmbCmd; // Run if condition is met : Echo,Success
                List<BakeryCommand> ifCmdList = compiledCmd.Link;
                while (true)
                {
                    ifSubCmd = ForgeIfSubCommand(ifCmd, true);
                    ifEmbCmd = ForgeIfEmbedCommand(ifCmd, ifSubCmd, 0);

                    // Ex) IfCompact,Equal,%A%,B
                    BakeryCommand ifCompiledCmd = new BakeryCommand(cmd.Origin, Opcode.IfCompact, ifSubCmd.ToOperandsPostfix(Opcode.Link.ToString()), addr, depth, new List<BakeryCommand>());
                    ifCmdList.Add(ifCompiledCmd);

                    if (ifEmbCmd.Opcode == Opcode.If) // Nested If
                    {
                        ifCmd = ifEmbCmd;
                        ifCompiledCmd.Operands.Add(Opcode.Link.ToString());
                        ifCmdList = ifCompiledCmd.Link;
                        depth++;
                        continue;
                    }
                    else if (ifEmbCmd.Opcode == Opcode.Begin) // Multiline If (Begin-End)
                    {
                        // Find proper End
                        int endIdx = MatchBeginWithEnd(ref cmdList, cmdListIdx);
                        if (endIdx == -1)
                            throw new InvalidGrammarException("End must be matched with Begin", cmd);
                        ifCompiledCmd.Operands.Add(Opcode.Link.ToString());
                        for (int i = cmdListIdx + 1; i < endIdx; i++)
                            ifCompiledCmd.Link.Add(cmdList[i]);
                        elseFlag = true; // Enable Else
                        return endIdx;
                    }
                    else // Singleline If
                    {
                        ifCompiledCmd.Link.Add(ifEmbCmd);
                        elseFlag = true; // Enable Else
                        return cmdListIdx;
                    }
                }
            }
            else if (elseEmbCmd.Opcode == Opcode.Begin)
            {
                // Find proper End
                int endIdx = MatchBeginWithEnd(ref cmdList, cmdListIdx);
                if (endIdx == -1)
                    throw new InvalidGrammarException("End must be matched with Begin", cmd);
                for (int i = cmdListIdx + 1; i < endIdx; i++)
                    compiledCmd.Link.Add(cmdList[i]);
                elseFlag = false;
                return endIdx;
            }
            else if (elseEmbCmd.Opcode == Opcode.Else || elseEmbCmd.Opcode == Opcode.End)
            {
                throw new InvalidGrammarException($"{elseEmbCmd.Opcode} cannot be used with Else", cmd);
            }
            else // Normal opcodes
            {
                compiledCmd.Link.Add(elseEmbCmd);
                elseFlag = false;
                return cmdListIdx;
            }
        }

        private static int MatchBeginWithEnd(ref List<BakeryCommand> cmdList, int cmdListIdx)
        { // To process nested Begin~End block
            int nestedBeginEnd = 0;
            bool beginExist = false;
            bool finalizedWithEnd = false;

            // start command must be If or Begin, and its last embCmd must be Begin
            if (!(cmdList[cmdListIdx].Opcode == Opcode.If || cmdList[cmdListIdx].Opcode == Opcode.Else))
                return -1;

            for (; cmdListIdx < cmdList.Count; cmdListIdx++)
            {
                BakeryCommand cmd = cmdList[cmdListIdx];
                if (cmd.Opcode == Opcode.If) // To check If,<Condition>,Begin
                {
                    while (true)
                    {
                        BakeryIfCommand subCmd = ForgeIfSubCommand(cmd, true);
                        BakeryCommand embCmd = ForgeIfEmbedCommand(cmd, subCmd, 0);
                        if (embCmd.Opcode == Opcode.If) // Nested If
                        {
                            cmd = embCmd;
                            continue;
                        }
                        else if (embCmd.Opcode == Opcode.Begin)
                        {
                            beginExist = true;
                            nestedBeginEnd++;
                        }
                        break;
                    }
                }
                else if (cmd.Opcode == Opcode.Else)
                {
                    BakeryCommand embCmd = ForgeEmbedCommand(cmd, 0, 0);
                    if (embCmd.Opcode == Opcode.If) // Nested If
                    {
                        while (true)
                        {
                            BakeryIfCommand ifSubCmd = ForgeIfSubCommand(embCmd, true);
                            BakeryCommand ifEmbCmd = ForgeIfEmbedCommand(embCmd, ifSubCmd, 0);
                            if (ifEmbCmd.Opcode == Opcode.If) // Nested If
                            {
                                cmd = embCmd;
                                continue;
                            }
                            else if (ifEmbCmd.Opcode == Opcode.Begin)
                            {
                                beginExist = true;
                                nestedBeginEnd++;
                            }
                            break;
                        }
                    }
                    else if (embCmd.Opcode == Opcode.Begin)
                    {
                        beginExist = true;
                        nestedBeginEnd++;
                    }
                }
                else if (cmd.Opcode == Opcode.End)
                {
                    nestedBeginEnd--;
                    if (nestedBeginEnd == 0)
                    {
                        finalizedWithEnd = true;
                        break;
                    }
                }
            }

            // Met Begin, End and returned, success
            if (beginExist && finalizedWithEnd && nestedBeginEnd == 0)
                return cmdListIdx;                
            else
                return -1;
        }

        private static BakeryCommand ParseCommand(List<string> rawCodes, ref int idx, SectionAddress addr)
        {
            Opcode opcode = Opcode.None;
            string externalOpcode = null;
 
            // Remove whitespace of rawCode's start and end
            string rawCode = rawCodes[idx].Trim();

            // Check if rawCode is Empty
            if (string.Equals(rawCode, string.Empty))
                return new BakeryCommand(string.Empty, Opcode.None, new List<string>(), addr);

            // Comment Format : starts with '//' or '#', ';'
            if (rawCode.StartsWith("//") || rawCode.StartsWith("#") || rawCode.StartsWith(";"))
                return new BakeryCommand(rawCode, Opcode.Comment, new List<string>(), addr);

            // Splice with spaces
            string[] slices = rawCode.Split(',');

            // Parse opcode
            opcode = ParseOpcode(slices[0].Trim(), out externalOpcode);
            
            // Check doublequote's occurence - must be 2n
            if (FileHelper.CountStringOccurrences(rawCode, "\"") % 2 == 1)
                throw new InvalidCommandException("number of doublequotes must be times of 2");

            // Parse Operands
            List<string> operands = ParseOperands(slices);

            // Check if last operand is \ - MultiLine check - only if one or more operands exists
            if (0 < operands.Count)
            {
                while (string.Equals(operands.Last(), @"\", StringComparison.OrdinalIgnoreCase))
                { // Split next line and append to List<string> operands
                    if (rawCodes.Count <= idx) // Section ended with \, invalid grammar!
                        throw new InvalidCommandException(@"A section's last command cannot end with '\'");
                    idx++;
                    operands.AddRange(rawCodes[idx].Trim().Split(','));
                }
            }

            // Forge BakeryCommand
            if (opcode == Opcode.Macro)
               return new BakeryCommand(rawCode, externalOpcode, operands, addr);
            else
                return new BakeryCommand(rawCode, opcode, operands, addr);
        }

        public static Opcode ParseOpcode(string opcodeStr, out string externalOpcode)
        {
            Opcode opcode = Opcode.None;
            externalOpcode = null;
            
            // There must be no number in opcodeStr
            if (!Regex.IsMatch(opcodeStr, @"^[A-Za-z_]+$", RegexOptions.Compiled))
                throw new InvalidCommandException("Only alphabet and underscore can be used as opcode");

            try
            {
                opcode = (Opcode) Enum.Parse(typeof(Opcode), opcodeStr, true);
                if (!Enum.IsDefined(typeof(Opcode), opcode) || opcode == Opcode.None || opcode == Opcode.Macro)
                    throw new ArgumentException();
            }
            catch (ArgumentException)
            {
                // Assume this command is Macro
                // Checking if this command is Macro or not will be determined in BakeryEngine.ExecuteCommand
                opcode = Opcode.Macro;
                externalOpcode = opcodeStr;
            }
            return opcode;
        }

        /// <summary>
        /// ParseState enum
        /// </summary>
        private enum ParseState { Normal, Merge }
        /// <summary>
        /// Parse operands, especially with doublequote
        /// </summary>
        /// <param name="slices"></param>
        /// <returns></returns>
        public static List<string> ParseOperands(string[] slices)
        {
            List<string> operandList = new List<string>();
            ParseState state = ParseState.Normal;
            StringBuilder builder = new StringBuilder();

            for (int i = 1; i < slices.Length; i++)
            {
                // Remove whitespace
                slices[i] = slices[i].Trim();

                // Check if operand is doublequoted
                int idx = slices[i].IndexOf("\"");
                if (idx == -1) // Do not have doublequote
                {
                    switch (state)
                    {
                        case ParseState.Normal: // Add to operand
                            operandList.Add(slices[i]);
                            break;
                        case ParseState.Merge:
                            builder.Append(",");
                            builder.Append(slices[i]);
                            break;
                        default:
                            throw new InternalParseException();
                    }
                }
                else if (idx == 0) // Startes with doublequote
                { // Merge this operand with next operand
                    switch (state)
                    {
                        case ParseState.Normal: // Add to operand
                            if (slices[i].IndexOf("\"", idx + 1) != -1) // This operand starts and end with doublequote
                            { // Ex) FileCopy,"1 2.dll",34.dll
                                operandList.Add(slices[i].Substring(1, slices[i].Length - 2)); // Remove doublequote
                            }
                            else
                            {
                                state = ParseState.Merge;
                                builder.Clear();
                                builder.Append(slices[i].Substring(1)); // Remove doublequote
                            }
                            break;
                        case ParseState.Merge:
                            throw new InvalidOperandException();
                        default:
                            throw new InternalParseException();
                    }
                }
                else if (idx == slices[i].Length - 1) // Endes with doublequote
                {
                    switch (state)
                    {
                        case ParseState.Normal: // Add to operand
                            throw new InvalidOperandException();
                        case ParseState.Merge:
                            state = ParseState.Normal;
                            builder.Append(",");
                            builder.Append(slices[i], 0, slices[i].Length - 1); // Remove doublequote
                            operandList.Add(builder.ToString());
                            builder.Clear();
                            break;
                        default:
                            throw new InternalParseException();
                    }
                }
                else // doublequote is in the middle
                {
                    throw new InvalidOperandException();
                }
            }

            // doublequote is not matched by two!
            if (state == ParseState.Merge)
                throw new InvalidOperandException("When parsing ends, ParseState must not be in state of Merge");

            return operandList;
        }

        /// <summary>
        /// Forge an BakeryIfSubCommand from BakeryCommand
        /// </summary>
        /// <param name="cmd">Source BakeryConmand to extract BakeryIfSubCommand</param>
        /// <param name="rawComparePosition">true - If grammar (%A%,Equal,A), false - IfCompact grammar (Equal,%A%,A)</param>
        /// <returns></returns>
        public static BakeryIfCommand ForgeIfSubCommand(BakeryCommand cmd, bool rawComparePosition)
        {
            // Get Condition SubOpcode
            IfSubOpcode subOpcode = IfSubOpcode.None;
            BakeryIfCommand subCmd;

            // Parse opcode
            int subOpcodeIdx = 0;
            bool notFlag = false;

            if (string.Equals(cmd.Operands[0], "Not", StringComparison.OrdinalIgnoreCase))
            {
                notFlag = true;
                subOpcodeIdx++;
            }

            if (rawComparePosition)
            { // 컴파일되기 전의 If 문법 (%A%,Equal,A)
                int occurence = FileHelper.CountStringOccurrences(cmd.Operands[subOpcodeIdx], "%"); // %Joveler%
                bool match = Regex.IsMatch(cmd.Operands[subOpcodeIdx], @"(#\d+)", RegexOptions.Compiled); // #1
                if ((occurence != 0 && occurence % 2 == 0) || match) // IfSubOpcode - Compare series
                {
                    string subOpcodeString = cmd.Operands[subOpcodeIdx + 1];
                    if (ParseCompareIfSubOpcode(cmd, subOpcodeString, ref subOpcode, ref notFlag) == false)
                        throw new InvalidSubOpcodeException($"Invalid sub command [If,{subOpcodeString}]", cmd);
                    List<string> operandList = new List<string>();
                    operandList.Add(cmd.Operands[subOpcodeIdx]);
                    operandList.AddRange(cmd.Operands.Skip(subOpcodeIdx + 2));
                    subCmd = new BakeryIfCommand(subOpcode, operandList, notFlag);
                }
                else // IfSubOpcode - Non-Compare series
                {
                    string subOpcodeString = cmd.Operands[subOpcodeIdx];
                    if (ParseNonCompareIfSubOpcode(cmd, subOpcodeString, ref subOpcode, ref notFlag) == false)
                        throw new InvalidSubOpcodeException($"Invalid sub command [If,{subOpcodeString}]", cmd);
                    subCmd = new BakeryIfCommand(subOpcode, cmd.Operands.Skip(subOpcodeIdx + 1).ToList(), notFlag);
                }
            }
            else
            { // 한번 컴파일된 후의 IfCompact 문법 (Equal,%A%,A)
                string subOpcodeString = cmd.Operands[subOpcodeIdx];
                if (ParseCompareIfSubOpcode(cmd, subOpcodeString, ref subOpcode, ref notFlag) == false)
                {
                    if (ParseNonCompareIfSubOpcode(cmd, subOpcodeString, ref subOpcode, ref notFlag) == false)
                        throw new InvalidSubOpcodeException($"Invalid sub command [If,{subOpcodeString}]", cmd);
                }
                subCmd = new BakeryIfCommand(subOpcode, cmd.Operands.Skip(subOpcodeIdx + 1).ToList(), notFlag);
            }

            return subCmd;
        }
        public static bool ParseCompareIfSubOpcode(BakeryCommand cmd, string subOpcodeString, ref IfSubOpcode subOpcode, ref bool notFlag)
        {
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
                return false;
            return true;
        }
        public static bool ParseNonCompareIfSubOpcode(BakeryCommand cmd, string subOpcodeString, ref IfSubOpcode subOpcode, ref bool notFlag)
        {
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
            else if (string.Equals(subOpcodeString, "ExistMacro", StringComparison.OrdinalIgnoreCase))
                subOpcode = IfSubOpcode.ExistMacro;
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
            else if (string.Equals(subOpcodeString, "NotExistMacro", StringComparison.OrdinalIgnoreCase))  // deprecated 
            {
                if (notFlag)
                    throw new InvalidSubOpcodeException("Condition [Not] cannot be duplicated", cmd);
                notFlag = true;
                subOpcode = IfSubOpcode.ExistMacro;
            }
            else if (string.Equals(subOpcodeString, "Ping", StringComparison.OrdinalIgnoreCase))
                subOpcode = IfSubOpcode.Ping;
            else if (string.Equals(subOpcodeString, "Online", StringComparison.OrdinalIgnoreCase))
                subOpcode = IfSubOpcode.Online;
            else
                return false;
            return true;
        }
        public static BakeryCommand ForgeIfEmbedCommand(BakeryCommand cmd, int depth)
        {
            BakeryIfCommand subCmd = ForgeIfSubCommand(cmd, true);
            int necessaryOperandNum = GetIfOperandNum(cmd, subCmd);
            return ForgeEmbedCommand(cmd, necessaryOperandNum + 1, depth);
        }
        public static BakeryCommand ForgeIfEmbedCommand(BakeryCommand cmd, BakeryIfCommand subCmd, int depth)
        {
            int necessaryOperandNum = GetIfOperandNum(cmd, subCmd);
            return ForgeEmbedCommand(cmd, necessaryOperandNum + 1, depth);
        }
        public static BakeryCommand ForgeIfConditionCommand(BakeryCommand cmd)
        {
            BakeryIfCommand subCmd = ForgeIfSubCommand(cmd, true);
            BakeryCommand embCmd = ForgeIfEmbedCommand(cmd, subCmd, 0);
            int operandCount = GetIfSubCmdOperandNum(subCmd.SubOpcode);
            return new BakeryCommand(Opcode.If, cmd.Operands.Take(operandCount + 1).ToList()); // 1 for sub opcode itself
        }
       
        public static BakeryCommand ForgeEmbedCommand(BakeryCommand cmd, int opcodeIdx, int depth)
        {
            // If,   ExistFile,Joveler.txt,Echo,ied206
            // [cmd] 0,        1,          2,   3 -> opcodeIdx must be 2 

            // Parse opcode
            string externalOpcode = null;
            Opcode opcode = ParseOpcode(cmd.Operands[opcodeIdx], out externalOpcode);

            int cmdDepth = depth + 1;
            if (opcode == Opcode.Run)
                cmdDepth -= 1;

            List<string> operands;
            if (opcodeIdx == 0 && cmd.Operands.Count == 1) // Ex) Begin
                operands = new List<string>();
            else // Ex) Set,%A%,B
                operands = cmd.Operands.Skip(opcodeIdx + 1).ToList();

            if (opcode == Opcode.Macro)
                return new BakeryCommand(cmd.Origin, externalOpcode, operands, cmd.Address, cmdDepth);
            else
                return new BakeryCommand(cmd.Origin, opcode, operands, cmd.Address, cmdDepth);
        }
        public static int GetIfOperandNum(BakeryCommand cmd, BakeryIfCommand subCmd)
        {
            int necessaryOperandNum = GetIfSubCmdOperandNum(subCmd.SubOpcode);
            if (1 <= cmd.Operands.Count && string.Equals(cmd.Operands[0], "Not", StringComparison.OrdinalIgnoreCase))
                necessaryOperandNum += 1;
            return necessaryOperandNum;
        }
        public static int GetIfSubCmdOperandNum(IfSubOpcode subOpcode)
        {
            switch (subOpcode)
            {
                case IfSubOpcode.Equal:
                case IfSubOpcode.Smaller:
                case IfSubOpcode.Bigger:
                case IfSubOpcode.SmallerEqual:
                case IfSubOpcode.BiggerEqual:
                    return 2;
                case IfSubOpcode.ExistFile:
                case IfSubOpcode.ExistDir:
                    return 1;
                case IfSubOpcode.ExistSection:
                case IfSubOpcode.ExistRegSection:
                    return 2;
                case IfSubOpcode.ExistRegKey:
                    return 3;
                case IfSubOpcode.ExistVar:
                case IfSubOpcode.ExistMacro:
                    return 1;
                case IfSubOpcode.Ping: // Not implemented
                case IfSubOpcode.Online: // Not implemented
                    return 0; // Not implemented
                default: // If this logic is called in production, it is definitely a BUG
                    return -1;
            }
        }
    }
}
