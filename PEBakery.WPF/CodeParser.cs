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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PEBakery.Exceptions;
using PEBakery.Helper;
using System.Windows;

namespace PEBakery.Core
{
    /// <summary>
    /// The compiler to convert If~Else, Begin~End to If+Jump (assembly) style
    /// </summary>
    public static class CodeParser
    {
        public static List<CodeCommand> ParseRawLines(List<string> lines, SectionAddress addr)
        {
            // Select Code sections and compile
            List<CodeCommand> rawCodeList = new List<CodeCommand>();
            for (int i = 0; i < lines.Count; i++)
                rawCodeList.Add(ParseCommand(lines, ref i, addr));

            List<CodeCommand> compiledList = rawCodeList;
            while (ParseRawLinesOnce(compiledList, out compiledList, addr));
            return compiledList;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="rawCmdList"></param>
        /// <param name="compiledList"></param>
        /// <returns>Return true if this section need more iterate</returns>
        public static bool ParseRawLinesOnce(List<CodeCommand> rawCmdList, out List<CodeCommand> compiledList, SectionAddress addr)
        {
            compiledList = new List<CodeCommand>();
            bool elseFlag = false;
            bool iterate = false;

            for (int i = 0; i < rawCmdList.Count; i++)
            {
                CodeCommand cmd = rawCmdList[i];
                if (cmd.Opcode == Opcode.If)
                {
                    int dest = ParseNestedIf(cmd, out elseFlag, ref rawCmdList, i, ref compiledList, addr);
                    i = dest;
                    iterate = true;
                }
                else if (cmd.Opcode == Opcode.Else) // SingleLine or MultiLine?
                {
                    if (elseFlag)
                    {
                        int dest = ParseNestedElse(cmd, out elseFlag, ref rawCmdList, i, ref compiledList, addr);
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

        /// <summary>
        /// Parsed nested if
        /// </summary>
        /// <param name="cmd">BakeryCommand</param>
        /// <param name="elseFlag">Can I use else right after this?</param>
        /// <param name="cmdList">raw command list</param>
        /// <param name="cmdListIdx">raw command index of list</param>
        /// <param name="parsedList">parsed command list</param>
        /// <param name="addr">section address addr</param>
        /// <returns>Return next command index</returns>
        private static int ParseNestedIf(CodeCommand cmd, out bool elseFlag, ref List<CodeCommand> cmdList, int cmdListIdx, ref List<CodeCommand> parsedList, SectionAddress addr)
        {
            CodeCommand ifCmd = cmd; // RawCode : If,%A%,Equal,B,Echo,Success
            IfCommand ifSubCmd; // Condition : Equal,%A%,B,Echo,Success
            CodeCommand ifEmbCmd; // Run if condition is met : Echo,Success
            // BakeryCommand compiledCmd; // Compiled If : IfCompact,Equal,%A%,B
            List<CodeCommand> ifCmdList = parsedList;
            elseFlag = false;

            // <Raw>
            // If,%A%,Equal,B,Echo,Success
            int depth = 0;
            while (true)
            {
                ifSubCmd = ForgeIfSubCommand(ifCmd, true); 
                ifEmbCmd = ForgeIfEmbedCommand(ifCmd, ifSubCmd, 0);

                // Ex) IfCompact,Equal,%A%,B
                CodeCommand ifCompiledCmd = new CodeCommand(cmd.RawCode, Opcode.IfCompact, ifSubCmd.ToOperandsPostfix(Opcode.Link.ToString()), addr, depth, new List<CodeCommand>());
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

        /// <summary>
        /// Parsed nested Else
        /// </summary>
        /// <param name="cmd">BakeryCommand else</param>
        /// <param name="elseFlag">Reset else flag</param>
        /// <param name="cmdList">raw command list</param>
        /// <param name="cmdListIdx">raw command index of list</param>
        /// <param name="parsedList">parsed command list</param>
        /// <param name="addr">section address addr</param>
        /// <returns>Return next command index</returns>
        private static int ParseNestedElse(CodeCommand cmd, out bool elseFlag, ref List<CodeCommand> cmdList, int cmdListIdx, ref List<CodeCommand> compiledList, SectionAddress addr)
        {
            CodeCommand elseEmbCmd = ForgeEmbedCommand(cmd, 0, 0);
            CodeCommand compiledCmd = new CodeCommand(cmd.RawCode, Opcode.ElseCompact, new List<string>(), addr, 0, new List<CodeCommand>());
            compiledCmd.Operands.Add(Opcode.Link.ToString());
            compiledList.Add(compiledCmd);

            if (elseEmbCmd.Opcode == Opcode.If) // Nested If
            {
                int depth = 0;
                
                CodeCommand ifCmd = elseEmbCmd; // RawCode : If,%A%,Equal,B,Echo,Success
                IfCommand ifSubCmd; // Condition : Equal,%A%,B,Echo,Success
                CodeCommand ifEmbCmd; // Run if condition is met : Echo,Success
                List<CodeCommand> ifCmdList = compiledCmd.Link;
                while (true)
                {
                    ifSubCmd = ForgeIfSubCommand(ifCmd, true);
                    ifEmbCmd = ForgeIfEmbedCommand(ifCmd, ifSubCmd, 0);

                    // Ex) IfCompact,Equal,%A%,B
                    CodeCommand ifCompiledCmd = new CodeCommand(cmd.RawCode, Opcode.IfCompact, ifSubCmd.ToOperandsPostfix(Opcode.Link.ToString()), addr, depth, new List<CodeCommand>());
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

        private static int MatchBeginWithEnd(ref List<CodeCommand> cmdList, int cmdListIdx)
        { // To process nested Begin~End block
            int nestedBeginEnd = 0;
            bool beginExist = false;
            bool finalizedWithEnd = false;

            // start command must be If or Begin, and its last embCmd must be Begin
            if (!(cmdList[cmdListIdx].Opcode == Opcode.If || cmdList[cmdListIdx].Opcode == Opcode.Else))
                return -1;

            for (; cmdListIdx < cmdList.Count; cmdListIdx++)
            {
                CodeCommand cmd = cmdList[cmdListIdx];
                if (cmd.Opcode == Opcode.If) // To check If,<Condition>,Begin
                {
                    while (true)
                    {
                        IfCommand subCmd = ForgeIfSubCommand(cmd, true);
                        CodeCommand embCmd = ForgeIfEmbedCommand(cmd, subCmd, 0);
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
                    CodeCommand embCmd = ForgeEmbedCommand(cmd, 0, 0);
                    if (embCmd.Opcode == Opcode.If) // Nested If
                    {
                        while (true)
                        {
                            IfCommand ifSubCmd = ForgeIfSubCommand(embCmd, true);
                            CodeCommand ifEmbCmd = ForgeIfEmbedCommand(embCmd, ifSubCmd, 0);
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

        /// <summary>
        /// Parse command raw string into BakeryCommand, wrapper of ParseCommand
        /// </summary>
        /// <param name="rawCode"></param>
        /// <returns></returns>
        public static CodeCommand ParseOneCommand(string rawCode)
        {
            List<string> list = new List<string>();
            list.Add(rawCode);
            int idx = 0;
            SectionAddress addr = new SectionAddress();

            return ParseCommand(list, ref idx, addr);
        }

        /// <summary>
        /// Parse command raw string (from raw code list) into BakeryCommand
        /// </summary>
        /// <param name="rawCodes"></param>
        /// <param name="idx"></param>
        /// <param name="addr"></param>
        /// <returns></returns>
        private static CodeCommand ParseCommand(List<string> rawCodes, ref int idx, SectionAddress addr)
        {
            Opcode opcode = Opcode.None;
            string externalOpcode = null;
 
            // Remove whitespace of rawCode's start and end
            string rawCode = rawCodes[idx].Trim();

            // Check if rawCode is Empty
            if (string.Equals(rawCode, string.Empty))
                return new CodeCommand(string.Empty, Opcode.None, new List<string>(), addr);

            // Comment Format : starts with '//' or '#', ';'
            if (rawCode.StartsWith("//") || rawCode.StartsWith("#") || rawCode.StartsWith(";"))
                return new CodeCommand(rawCode, Opcode.Comment, new List<string>(), addr);

            // Splice with spaces
            List<string> slices = rawCode.Split(',').ToList();

            // Parse opcode
            opcode = ParseOpcode(slices[0].Trim(), out externalOpcode);
            
            // Check doublequote's occurence - must be 2n
            if (FileHelper.CountStringOccurrences(rawCode, "\"") % 2 == 1)
                throw new InvalidCommandException("number of doublequotes must be times of 2");

            // Parse Operands
            List<string> operands = new List<string>();
            try
            {
                operands = ParseOperands(slices, 1);
            }
            catch
            {
                return new CodeCommand(string.Empty, Opcode.Error, new List<string>(), addr);
            }

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
               return new CodeCommand(rawCode, externalOpcode, operands, addr);
            else
                return new CodeCommand(rawCode, opcode, operands, addr);
        }

        /// <summary>
        /// Parse BakeryCommand opcode
        /// </summary>
        /// <param name="opcodeStr"></param>
        /// <param name="externalOpcode"></param>
        /// <returns></returns>
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
        /// Parse BakeryCommand operands, especially with doublequote
        /// </summary>
        /// <param name="slices"></param>
        /// <returns></returns>
        public static List<string> ParseOperands(List<string> slices, int start)
        {
            List<string> operandList = new List<string>();
            ParseState state = ParseState.Normal;
            StringBuilder builder = new StringBuilder();

            for (int i = start; i < slices.Count; i++)
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
                else // doublequote is in the middle - Error
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
        public static IfCommand ForgeIfSubCommand(CodeCommand cmd, bool rawComparePosition)
        {
            // Get Condition SubOpcode
            IfSubOpcode subOpcode = IfSubOpcode.None;
            IfCommand subCmd;

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
                    if (ParseIfSubOpcodeIfCompare(cmd, subOpcodeString, ref subOpcode, ref notFlag) == false)
                        throw new InvalidSubOpcodeException($"Invalid sub command [If,{subOpcodeString}]", cmd);
                    List<string> operandList = new List<string>();
                    operandList.Add(cmd.Operands[subOpcodeIdx]);
                    operandList.AddRange(cmd.Operands.Skip(subOpcodeIdx + 2));
                    subCmd = new IfCommand(subOpcode, operandList, notFlag);
                }
                else // IfSubOpcode - Non-Compare series
                {
                    string subOpcodeString = cmd.Operands[subOpcodeIdx];
                    if (ParseIfSubOpcodeIfNonCompare(cmd, subOpcodeString, ref subOpcode, ref notFlag) == false)
                        throw new InvalidSubOpcodeException($"Invalid sub command [If,{subOpcodeString}]", cmd);
                    subCmd = new IfCommand(subOpcode, cmd.Operands.Skip(subOpcodeIdx + 1).ToList(), notFlag);
                }
            }
            else
            { // 한번 컴파일된 후의 IfCompact 문법 (Equal,%A%,A)
                string subOpcodeString = cmd.Operands[subOpcodeIdx];
                if (ParseIfSubOpcodeIfCompare(cmd, subOpcodeString, ref subOpcode, ref notFlag) == false)
                {
                    if (ParseIfSubOpcodeIfNonCompare(cmd, subOpcodeString, ref subOpcode, ref notFlag) == false)
                        throw new InvalidSubOpcodeException($"Invalid sub command [If,{subOpcodeString}]", cmd);
                }
                subCmd = new IfCommand(subOpcode, cmd.Operands.Skip(subOpcodeIdx + 1).ToList(), notFlag);
            }

            return subCmd;
        }
        /// <summary>
        /// Parse If/IfCompact's comparing subOpcode
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="subOpcodeString"></param>
        /// <param name="subOpcode"></param>
        /// <param name="notFlag"></param>
        /// <returns></returns>
        public static bool ParseIfSubOpcodeIfCompare(CodeCommand cmd, string subOpcodeString, ref IfSubOpcode subOpcode, ref bool notFlag)
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
        /// <summary>
        /// Parse If/IfCompact's non-comparing subOpcode
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="subOpcodeString"></param>
        /// <param name="subOpcode"></param>
        /// <param name="notFlag"></param>
        /// <returns></returns>
        public static bool ParseIfSubOpcodeIfNonCompare(CodeCommand cmd, string subOpcodeString, ref IfSubOpcode subOpcode, ref bool notFlag)
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
        public static CodeCommand ForgeIfEmbedCommand(CodeCommand cmd, int depth)
        {
            IfCommand subCmd = ForgeIfSubCommand(cmd, true);
            int necessaryOperandNum = GetIfOperandNum(cmd, subCmd);
            return ForgeEmbedCommand(cmd, necessaryOperandNum + 1, depth);
        }
        public static CodeCommand ForgeIfEmbedCommand(CodeCommand cmd, IfCommand subCmd, int depth)
        {
            int necessaryOperandNum = GetIfOperandNum(cmd, subCmd);
            return ForgeEmbedCommand(cmd, necessaryOperandNum + 1, depth);
        }
        public static CodeCommand ForgeIfConditionCommand(CodeCommand cmd)
        {
            IfCommand subCmd = ForgeIfSubCommand(cmd, true);
            CodeCommand embCmd = ForgeIfEmbedCommand(cmd, subCmd, 0);
            int operandCount = GetIfSubCmdOperandNum(subCmd.SubOpcode);
            return new CodeCommand(Opcode.If, cmd.Operands.Take(operandCount + 1).ToList()); // 1 for sub opcode itself
        }
       
        public static CodeCommand ForgeEmbedCommand(CodeCommand cmd, int opcodeIdx, int depth)
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
                return new CodeCommand(cmd.RawCode, externalOpcode, operands, cmd.Address, cmdDepth);
            else
                return new CodeCommand(cmd.RawCode, opcode, operands, cmd.Address, cmdDepth);
        }
        public static int GetIfOperandNum(CodeCommand cmd, IfCommand subCmd)
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
                case IfSubOpcode.ExistVar:
                case IfSubOpcode.ExistMacro:
                    return 1;
                case IfSubOpcode.ExistSection:
                case IfSubOpcode.ExistRegSection:
                    return 2;
                case IfSubOpcode.ExistRegKey:
                    return 3;
                case IfSubOpcode.Ping: // Not implemented
                case IfSubOpcode.Online: // Not implemented
                    return 0; // Not implemented
                default: // If this logic is called in production, it is definitely a BUG
                    return -1;
            }
        }
    }
}
