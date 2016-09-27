using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BakeryEngine
{
    /// <summary>
    /// Exception used in BakeryEngine::ParseCommand
    /// </summary>
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
    public static class BakeryCompiler
    {
        private enum CompileState
        {
            Normal, IfMultiLine, ElseMultiLine
        }

        private enum LastCommand
        {
            Normal, If, Else, Begin, End
        }

        public static void Compile(Plugin plugin)
        {
            foreach (PluginSection rawSection in plugin.Sections.Values)
            {
                // Select Code sections and compile
                if (rawSection.Type != SectionType.Code)
                    continue;
                PluginRawLineSection section = (PluginRawLineSection)rawSection;
                string[] rawCodes = section.Get() as string[];

                List<BakeryCommand> compiledList = CompileSection(rawCodes);
            }
        }

        public static List<BakeryCommand> CompileSection(string[] rawCodes)
        {
            CompileState state = CompileState.Normal;
            LastCommand last = LastCommand.Normal;
            BakeryCommand cmd;
            List<BakeryCommand> compiledList = new List<BakeryCommand>();
            List<BakeryCommand> codeBlockList = new List<BakeryCommand>();
            int nestIfCount = 0;
            int codeBlockLine = 0;
            for (int i = 0; i < rawCodes.Length; i++)
            {
                cmd = ParseCommand(rawCodes[i]);

                switch (state)
                {
                    case CompileState.Normal:
                        if (cmd.Opcode == Opcode.If) // SingleLine or MultiLine?
                        {
                            last = LastCommand.If;
                            CompileNestedIf(cmd, ref state, ref codeBlockList, ref nestIfCount);
                        }
                        else if (cmd.Opcode == Opcode.Else) // SingleLine or MultiLine?
                        {
                            if (last == LastCommand.If)
                            {
                                last = LastCommand.Else;
                            }
                            else
                                throw new InvalidGrammarException("Else must be used after If", cmd);
                        }
                        else if (cmd.Opcode == Opcode.Begin)
                            throw new InvalidGrammarException("Begin must be used with If or Else", cmd);
                        else if (cmd.Opcode == Opcode.End)
                            throw new InvalidGrammarException("End must be matched with Begin", cmd);
                        else
                        { // The other operands - just copy
                            if (last == LastCommand.Normal)
                                compiledList.Add(cmd);
                            else if (last == LastCommand.If)
                            { // The other operands - finalize single line if, by adding addr info to Jump
                                for (int x = 0; x < codeBlockList.Count; x++)
                                {
                                    if (codeBlockList[x].Opcode == Opcode.If)
                                    {
                                        codeBlockList[x].Operands.Add((nestIfCount + 1).ToString());
                                        nestIfCount--;
                                    }
                                    else if (x + 1 != codeBlockList.Count) // Last
                                        throw new InternalUnknownException("Check Compiler Logic, IfSingleLine");
                                }
                                compiledList.AddRange(codeBlockList.Select(x => x));
                                codeBlockList.Clear();
                                state = CompileState.Normal;
                            }
                        }
                        break;
                    case CompileState.IfMultiLine:
                        if (cmd.Opcode == Opcode.If) // SingleLine or MultiLine?
                        {
                            last = LastCommand.If;
                            CompileNestedIf(cmd, ref state, ref codeBlockList, ref nestIfCount);
                        }
                        else if (cmd.Opcode == Opcode.Else) // SingleLine or MultiLine?
                        {
                            if (last == LastCommand.End)
                            {
                                last = LastCommand.Else;

                            }
                            else
                                throw new InvalidGrammarException("Else must be used after If", cmd);
                        }
                        else if (cmd.Opcode == Opcode.Begin)
                            throw new InvalidGrammarException("Begin must be used with If or Else", cmd);
                        else if (cmd.Opcode == Opcode.End)
                        {
                            throw new InvalidGrammarException("End must be matched with Begin", cmd);
                        }
                        else
                        {
                            compiledList.Add(cmd);
                        }
                        break;
                    case CompileState.ElseMultiLine:
                        break;
                }
            }

            return compiledList;
        }
        private static void CompileNestedIf(BakeryCommand cmd, ref CompileState state, ref List<BakeryCommand> tempList, ref int nestIfCount)
        {
            nestIfCount = 0;
            BakeryCommand ifCmd = cmd;
            BakeryIfCommand subCmd;
            BakeryCommand embCmd;
            while (true)
            {
                nestIfCount++;
                subCmd = ForgeIfSubCommand(ifCmd);
                embCmd = ForgeIfEmbedCommand(ifCmd, subCmd, 0);
                int operandCount = GetIfSubCmdOperandNum(subCmd.SubOpcode);
                // Prepare If's conditions - Ex) If,%A%,Equal,B,Echo,Success
                List<string> operands = new List<string>();
                operands.AddRange(ifCmd.Operands.Take(operandCount + 1)); // +1 for subOpcode
                tempList.Add(new BakeryCommand(ifCmd.Origin, Opcode.IfCompact, operands)); // Ex) If,Not,%A%,Equal,B,Jump,Relative - N will be added in later

                if (embCmd.Opcode == Opcode.If)
                { // Nested If
                    ifCmd = embCmd;
                    continue;
                }
                else if (embCmd.Opcode == Opcode.Begin)
                { // Multiline If (Begin-End)
                    state = CompileState.IfMultiLine;
                    tempList.Add(embCmd); // Ex) Echo,Success
                }
                else
                { // Singleline If
                    // Add Jump,Relative - addr will be added in else or normal
                    tempList.Add(embCmd); // Ex) Echo,Success
                }
                break;
            }
        }

        private static void CompileNestedElse(BakeryCommand cmd, ref CompileState state, ref List<BakeryCommand> tempList, ref int nestIfCount)
        {
            // Ex) Else, Set,%A%,B
            // elseCmd = Set,%A%,B
            BakeryCommand emdCmd = BakeryEngine.ForgeEmbedCommand(cmd, 0, 0);
            if (emdCmd.Opcode == Opcode.If) // Nested If
            {

            }
            else if (emdCmd.Opcode == Opcode.Begin) // ElseMultiline
            {
                // Add Jump,Relative to tempList
                state = CompileState.ElseMultiLine;
                List<string> operands = new List<string>();
                operands.Add("Relative");
                BakeryCommand embCmd = new BakeryCommand(cmd.Origin, Opcode.Jump, operands);
            }
        }

        private static BakeryCommand ParseCommand(string rawCode)
        {
            Opcode opcode = Opcode.None;

            // Remove whitespace of rawCode's start and end
            rawCode = rawCode.Trim();

            // Check if rawCode is Empty
            if (string.Equals(rawCode, string.Empty))
                return new BakeryCommand(string.Empty, Opcode.None, new List<string>());

            // Comment Format : starts with '//' or '#', ';'
            if (rawCode.StartsWith("//") || rawCode.StartsWith("#") || rawCode.StartsWith(";"))
                return new BakeryCommand(rawCode, Opcode.Comment, new List<string>());

            // Splice with spaces
            string[] slices = rawCode.Split(',');

            // Parse opcode
            string opcodeStr = slices[0].Trim();
            try
            {
                opcode = (Opcode)Enum.Parse(typeof(Opcode), opcodeStr, true);
                if (!Enum.IsDefined(typeof(Opcode), opcode) || opcode == Opcode.None || opcode == Opcode.Comment)
                    throw new ArgumentException();
            }
            catch (ArgumentException)
            {
                throw new InvalidOpcodeException($"Unknown command [{opcodeStr}]", new BakeryCommand(rawCode, Opcode.Unknown, new List<string>()));
            }

            // Check doublequote's occurence - must be 2n
            if (Helper.CountStringOccurrences(rawCode, "\"") % 2 == 1)
                throw new InvalidCommandException("number of doublequotes must be times of 2");

            // forge BakeryCommand
            return new BakeryCommand(rawCode, opcode, ParseOperands(slices));
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
            BakeryIfCommand subCmd = ForgeIfSubCommand(cmd);
            BakeryCommand embCmd = ForgeIfEmbedCommand(cmd, subCmd, 0);
            int operandCount = GetIfSubCmdOperandNum(subCmd.SubOpcode);
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
                opcode = (Opcode)Enum.Parse(typeof(Opcode), opcodeStr, true);
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
    }
}
