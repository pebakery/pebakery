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
using PEBakery.Exceptions;
using PEBakery.Helper;
using System.Globalization;

namespace PEBakery.Core
{
    public static class CodeParser
    {
        #region ParseOneRawLine, ParseRawLines
        public static CodeCommand ParseOneRawLine(string rawCode, SectionAddress addr)
        {
            List<string> list = new List<string>();
            int idx = 0;
            list.Add(rawCode);

            return ParseCommand(list, addr, ref idx);
        }

        public static List<CodeCommand> ParseRawLines(List<string> lines, SectionAddress addr, out List<LogInfo> errorLogs)
        {
            // Select Code sections and compile
            errorLogs = new List<LogInfo>();
            List<CodeCommand> codeList = new List<CodeCommand>();
            for (int i = 0; i < lines.Count; i++)
            {
                try
                {
                    codeList.Add(ParseCommand(lines, addr, ref i));
                }
                catch (EmptyLineException) { } // Do nothing
                catch (InvalidCodeCommandException e)
                {
                    codeList.Add(e.Cmd);
                    errorLogs.Add(new LogInfo(LogState.Error, e, e.Cmd));
                }
                catch (Exception e)
                {
                    CodeCommand error = new CodeCommand(lines[i].Trim(), addr, CodeType.Error, new CodeInfo());
                    codeList.Add(error);
                    errorLogs.Add(new LogInfo(LogState.Error, e.Message, error));
                }
            }

            List<CodeCommand> compiledList = codeList;
            try
            {
                CompileBranchCodeBlock(compiledList, out compiledList);
            }
            catch (InvalidCodeCommandException e)
            {
                errorLogs.Add(new LogInfo(LogState.Error, e.Message, e.Cmd));
            }
            return compiledList;
        }
        #endregion

        #region ParseCommand, ParseCommandFromSlicedArgs, ParseCodeType, ParseArguments
        private static CodeCommand ParseCommand(List<string> rawCodes, SectionAddress addr, ref int idx)
        {
            CodeType type = CodeType.None;

            // Remove whitespace of rawCode's start and end
            string rawCode = rawCodes[idx].Trim();

            // Check if rawCode is Empty
            if (string.Equals(rawCode, string.Empty))
                throw new EmptyLineException();

            // Comment Format : starts with '//' or '#', ';'
            if (rawCode.StartsWith("//") || rawCode.StartsWith("#") || rawCode.StartsWith(";"))
                return new CodeCommand(rawCode, addr, CodeType.Comment, new CodeInfo());

            // Splice with spaces
            List<string> rawArgs = rawCode.Split(',').ToList();

            // Parse opcode
            string macroType;
            try
            {
                type = ParseCodeType(rawArgs[0].Trim(), out macroType);
            }
            catch (InvalidCommandException e)
            {
                CodeCommand error = new CodeCommand(rawCode, addr, CodeType.Error, new CodeInfo());
                throw new InvalidCodeCommandException(e.Message, error);
            }

            // Check doublequote's occurence - must be 2n
            if (FileHelper.CountStringOccurrences(rawCode, "\"") % 2 == 1)
            {
                CodeCommand error = new CodeCommand(rawCode, addr, CodeType.Error, new CodeInfo());
                throw new InvalidCodeCommandException("Doublequote's number should be even number", error);
            }
                
            // Parse Arguments
            List<string> args = new List<string>();
            try
            {
                args = ParseArguments(rawArgs, 1);
            }
            catch (InvalidCommandException e)
            {
                CodeCommand error = new CodeCommand(rawCode, addr, CodeType.Error, new CodeInfo());
                throw new InvalidCodeCommandException(e.Message, error);
            }

            // Check if last operand is \ - MultiLine check - only if one or more operands exists
            if (0 < args.Count)
            {
                while (string.Equals(args.Last(), @"\", StringComparison.OrdinalIgnoreCase))
                { // Split next line and append to List<string> operands
                    if (rawCodes.Count <= idx) // Section ended with \, invalid grammar!
                    {
                        CodeCommand error = new CodeCommand(rawCode, addr, CodeType.Error, new CodeInfo());
                        throw new InvalidCodeCommandException(@"Last command of a section cannot end with '\'", error);
                    }
                    idx++;
                    args.AddRange(rawCodes[idx].Trim().Split(','));
                }
            }

            CodeInfo info;
            try
            {
                info = ParseCodeCommandInfo(rawCode, type, macroType, args, addr);
                return new CodeCommand(rawCode, addr, type, info);
            }
            catch (InvalidCommandException e)
            {
                CodeCommand error = new CodeCommand(rawCode, addr, CodeType.Error, new CodeInfo());
                throw new InvalidCodeCommandException(e.Message, error);
            }
        }

        /// <summary>
        /// Used to get Embedded Command from If, Else
        /// </summary>
        /// <param name="rawCodes"></param>
        /// <param name="addr"></param>
        /// <param name="idx"></param>
        /// <param name="preprocessed"></param>
        /// <returns></returns>
        private static CodeCommand ParseCommandFromSlicedArgs(string rawCode, List<string> args, SectionAddress addr)
        {
            CodeType type = CodeType.None;

            // Parse opcode
            string macroType;
            try
            {
                type = ParseCodeType(args[0], out macroType);
            }
            catch (InvalidCommandException e)
            {
                throw new InvalidCommandException(e.Message, rawCode);
            }

            CodeInfo info;
            try
            {
                info = ParseCodeCommandInfo(rawCode, type, macroType, args.Skip(1).ToList(), addr);
                return new CodeCommand(rawCode, addr, type, info);
            }
            catch (InvalidCommandException e)
            {
                CodeCommand error = new CodeCommand(rawCode, addr, CodeType.Error, new CodeInfo());
                throw new InvalidCodeCommandException(e.Message, error);
            }
        }

        public static CodeType ParseCodeType(string typeStr, out string macroType)
        {
            macroType = null;

            // There must be no number in yypeStr
            if (!Regex.IsMatch(typeStr, @"^[A-Za-z_]+$", RegexOptions.Compiled))
                throw new InvalidCommandException($"Wrong CodeType [{typeStr}], Only alphabet and underscore can be used as opcode");

            bool isMacro = false;
            if (Enum.TryParse(typeStr, true, out CodeType type) == false)
                isMacro = true;
            if (Enum.IsDefined(typeof(CodeType), type) == false || type == CodeType.None || type == CodeType.Macro)
                isMacro = true;

            if (isMacro)
            {
                type = CodeType.Macro;
                macroType = typeStr;
            }

            return type;
        }

        private enum ParseState { Normal, Merge }
        public static List<string> ParseArguments(List<string> slices, int start)
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
                            throw new InternalParserException("Internal parser error");
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
                        default:
                            throw new InternalParserException("Internal parser error");
                    }
                }
                else if (idx == slices[i].Length - 1) // Endes with doublequote
                {
                    switch (state)
                    {
                        case ParseState.Merge:
                            state = ParseState.Normal;
                            builder.Append(",");
                            builder.Append(slices[i], 0, slices[i].Length - 1); // Remove doublequote
                            operandList.Add(builder.ToString());
                            builder.Clear();
                            break;
                        default:
                            throw new InternalParserException("Internal parser error");
                    }
                }
                else // doublequote is in the middle - Error
                {
                    throw new InvalidCommandException("Wrong doublequote usage");
                }
            }

            // doublequote is not matched by two!
            if (state == ParseState.Merge)
                throw new InternalParserException("Internal parser error");

            return operandList;
        }
        #endregion

        #region ParseCodeCommandInfo, CheckInfoArgumentCount
        public static CodeInfo ParseCodeCommandInfo(string rawCode, CodeType type, string macroType, List<string> args, SectionAddress addr)
        {
            switch (type)
            {
                #region 00 Misc
                // 00 Misc
                case CodeType.None:
                    break;
                case CodeType.Comment:
                    break;
                case CodeType.Error:
                    break;
                case CodeType.Unknown:
                    break;
                #endregion
                #region 01 File
                // 01 File
                case CodeType.CopyOrExpand:
                    break;
                case CodeType.DirCopy:
                    break;
                case CodeType.DirDelete:
                    break;
                case CodeType.DirMove:
                    break;
                case CodeType.DirMake:
                    break;
                case CodeType.Expand:
                    break;
                case CodeType.FileCopy:
                    { // FileCopy,<SrcFile>,<DestPath>[,PRESERVE][,NOWARN][,NOREC][,SHOW]
                        const int minArgCount = 2;
                        const int maxArgCount = 6;
                        if (CodeParser.CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        string srcFile = args[0];
                        string destPath = args[1];
                        bool preserve = false;
                        bool noWarn = false;
                        bool noRec = false;
                        bool show = false;

                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            string arg = args[i];
                            if (arg.Equals("PRESERVE", StringComparison.OrdinalIgnoreCase))
                                preserve = true;
                            else if (arg.Equals("NOWARN", StringComparison.OrdinalIgnoreCase))
                                noWarn = true;
                            else if (arg.Equals("SHOW", StringComparison.OrdinalIgnoreCase)) // for compability with WB082
                                show = true;
                            else if (arg.Equals("NOREC", StringComparison.OrdinalIgnoreCase)) // no recursive wildcard copy
                                noRec = true;
                            else
                                throw new InvalidCommandException($"Invalid argument [{arg}]", rawCode);
                        }

                        return new CodeInfo_FileCopy(srcFile, destPath, preserve, noWarn, noRec, show);
                    }
                case CodeType.FileDelete:
                    break;
                case CodeType.FileRename:
                    break;
                case CodeType.FileMove:
                    break;
                case CodeType.FileCreateBlank:
                    { // FileCreateBlank,<FilePath>[,PRESERVE][,NOWARN][,UTF8 | UTF16LE | UTF16BE | ANSI]
                        const int minArgCount = 1;
                        const int maxArgCount = 4;
                        if (CodeParser.CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        string filePath = args[0];
                        bool preserve = false;
                        bool noWarn = false;
                        Encoding encoding = null;

                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            string arg = args[i];
                            if (arg.Equals("PRESERVE", StringComparison.OrdinalIgnoreCase))
                                preserve = true;
                            else if (arg.Equals("NOWARN", StringComparison.OrdinalIgnoreCase))
                                noWarn = true;
                            else if (arg.Equals("UTF8", StringComparison.OrdinalIgnoreCase))
                            {
                                if (encoding != null)
                                    throw new InvalidCommandException($"Encoding cannot be duplicated", rawCode);
                                encoding = Encoding.UTF8;
                            }
                            else if (arg.Equals("UTF16", StringComparison.OrdinalIgnoreCase))
                            {
                                if (encoding != null)
                                    throw new InvalidCommandException($"Encoding cannot be duplicated", rawCode);
                                encoding = Encoding.Unicode;
                            }
                            else if (arg.Equals("UTF16", StringComparison.OrdinalIgnoreCase) || arg.Equals("UTF16LE", StringComparison.OrdinalIgnoreCase))
                            {
                                if (encoding != null)
                                    throw new InvalidCommandException($"Encoding cannot be duplicated", rawCode);
                                encoding = Encoding.Unicode;
                            }
                            else if (arg.Equals("UTF16", StringComparison.OrdinalIgnoreCase))
                            {
                                if (encoding != null)
                                    throw new InvalidCommandException($"Encoding cannot be duplicated", rawCode);
                                encoding = Encoding.BigEndianUnicode;
                            }
                            else if (arg.Equals("ANSI", StringComparison.OrdinalIgnoreCase))
                            {
                                if (encoding != null)
                                    throw new InvalidCommandException($"Encoding cannot be duplicated", rawCode);
                                encoding = Encoding.ASCII;
                            }
                            else
                                throw new InvalidCommandException($"Invalid argument [{arg}]", rawCode);
                        }

                        return new CodeInfo_FileCreateBlank(filePath, preserve, noWarn, encoding);
                    }
                case CodeType.FileByteExtract:
                    break;
                #endregion
                #region 02 Registry
                // 02 Registry
                case CodeType.RegHiveLoad:
                    break;
                case CodeType.RegHiveUnload:
                    break;
                case CodeType.RegImport:
                    break;
                case CodeType.RegWrite:
                    break;
                case CodeType.RegRead:
                    break;
                case CodeType.RegDelete:
                    break;
                case CodeType.RegWriteBin:
                    break;
                case CodeType.RegReadBin:
                    break;
                case CodeType.RegMulti:
                    break;
                #endregion
                #region 03 Text
                // 03 Text
                case CodeType.TXTAddLine:
                    { // TXTAddLine,<FileName>,<Line>,<Mode>[,LineNum]
                        const int minArgCount = 3;
                        const int maxArgCount = 4;
                        if (CodeParser.CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        string fileName = args[0];
                        string line = args[1];
                        TXTAddLineMode mode;
                        if (args[2].Equals("Append", StringComparison.OrdinalIgnoreCase))
                            mode = TXTAddLineMode.Append;
                        else if (args[2].Equals("Prepend", StringComparison.OrdinalIgnoreCase))
                            mode = TXTAddLineMode.Prepend;
                        else if (args[2].Equals("Place", StringComparison.OrdinalIgnoreCase))
                            mode = TXTAddLineMode.Place;
                        else
                            throw new InvalidCommandException($"Invalid argument [{args[2]}]", rawCode);

                        int lineNum = -1;
                        if (mode == TXTAddLineMode.Place)
                        {
                            if (args.Count != maxArgCount)
                                throw new InvalidCommandException($"In [Place] mode, line number argument is necessary", rawCode);
                            if (int.TryParse(args[maxArgCount], out lineNum) == false)
                                lineNum = -1;
                            if (lineNum <= 0)
                                throw new InvalidCommandException($"Line number must be positive integer", rawCode);
                        }

                        return new CodeInfo_TXTAddLine(fileName, line, mode, lineNum);
                    }
                case CodeType.TXTReplace:
                    { // TXTReplace,<FileName>,<ToBeReplaced>,<ReplaceWith>
                        const int minArgCount = 3;
                        const int maxArgCount = 3;
                        if (CodeParser.CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        if (args[1].Contains("#$x"))
                            throw new InvalidCommandException($"String to be replaced or replace with cannot include line feed", rawCode);
                        if (args[2].Contains("#$x"))
                            throw new InvalidCommandException($"String to be replaced or replace with cannot include line feed", rawCode);

                        return new CodeInfo_TXTReplace(args[0], args[1], args[2]);
                    }
                case CodeType.TXTDelLine:
                    { // TXTDelLine,<FileName>,<DeleteIfBeginWith>
                        const int minArgCount = 2;
                        const int maxArgCount = 2;
                        if (CodeParser.CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        if (args[1].Contains("#$x"))
                            throw new InvalidCommandException($"Keyword cannot include line feed", rawCode);

                        return new CodeInfo_TXTDelLine(args[0], args[1]);
                    }
                case CodeType.TXTDelSpaces:
                    { // TXTDelSpaces,<FileName>
                        const int minArgCount = 1;
                        const int maxArgCount = 1;
                        if (CodeParser.CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        return new CodeInfo_TXTDelSpaces(args[0]);
                    }
                case CodeType.TXTDelEmptyLines:
                    { // TXTDelEmptyLines,<FileName>
                        const int minArgCount = 1;
                        const int maxArgCount = 1;
                        if (CodeParser.CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        return new CodeInfo_TXTDelEmptyLines(args[0]);
                    }
                #endregion
                #region 04 INI
                // 04 INI
                case CodeType.INIWrite:
                    { // INIWrite,<FileName>,<SectionName>,<Key>,<Value>
                        const int minArgCount = 4;
                        const int maxArgCount = 4;
                        if (CodeParser.CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        return new CodeInfo_INIWrite(args[0], args[1], args[2], args[3]);
                    }
                case CodeType.INIRead:
                    { // INIWrite,<FileName>,<SectionName>,<Key>,<VarName>
                        const int minArgCount = 4;
                        const int maxArgCount = 4;
                        if (CodeParser.CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        string varName = args[3];
                        if (varName == null)
                            throw new InvalidCommandException($"Variable name [{args[3]}] must start and end with %", rawCode);

                        return new CodeInfo_INIRead(args[0], args[1], args[2], varName);
                    }
                case CodeType.INIDelete:
                    { // INIDelete,<FileName>,<SectionName>,<Key>
                        const int minArgCount = 3;
                        const int maxArgCount = 3;
                        if (CodeParser.CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        return new CodeInfo_INIDelete(args[0], args[1], args[2]);
                    }
                case CodeType.INIAddSection:
                    { // INIAddSection,<FileName>,<SectionName>
                        const int minArgCount = 2;
                        const int maxArgCount = 2;
                        if (CodeParser.CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        return new CodeInfo_INIAddSection(args[0], args[1]);
                    }
                case CodeType.INIDeleteSection:
                    { // INIDeleteSection,<FileName>,<SectionName>
                        const int minArgCount = 2;
                        const int maxArgCount = 2;
                        if (CodeParser.CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        return new CodeInfo_INIDeleteSection(args[0], args[1]);
                    }
                case CodeType.INIWriteTextLine:
                    { // INIDelete,<FileName>,<SectionName>,<Line>[,APPEND]
                        const int minArgCount = 3;
                        const int maxArgCount = 4;
                        if (CodeParser.CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        bool append = false;
                        if (maxArgCount == args.Count)
                        {
                            if (args[3].Equals("APPEND", StringComparison.OrdinalIgnoreCase))
                                append = true;
                            else
                                throw new InvalidCommandException($"Wrong argument [{args[3]}]", rawCode);
                        }

                        return new CodeInfo_INIWriteTextLine(args[0], args[1], args[2], append);
                    }
                case CodeType.INIMerge:
                    {
                        // INIMerge,<SrcFileName>,<DestFileName>
                        // INIMerge,<SrcFileName>,<SectionName>,<DestFileName>
                        const int minArgCount = 2;
                        const int maxArgCount = 3;
                        if (CodeParser.CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        string srcFileName = args[0];
                        string destFileName;
                        string sectionName = null;
                        if (args.Count == minArgCount)
                        {
                            destFileName = args[1];
                        }
                        else if (args.Count == maxArgCount)
                        {
                            destFileName = args[2];
                            sectionName = args[1];
                        }
                        else
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        return new CodeInfo_INIMerge(srcFileName, destFileName, sectionName);
                    }
                #endregion
                #region 05 Network
                // 05 Network
                case CodeType.WebGet:
                    break;
                case CodeType.WebGetIfNotExist:
                    break;
                #endregion
                #region 06 Attach, Interface
                // 06 Attach, Interface
                case CodeType.ExtractFile:
                    break;
                case CodeType.ExtractAndRun:
                    break;
                case CodeType.ExtractAllFiles:
                    break;
                case CodeType.ExtractAllFilesIfNotExist:
                    break;
                case CodeType.Encode:
                    break;
                #endregion
                #region 07 UI
                // 07 UI
                case CodeType.Message:
                    { // Message,<Message>,<Icon>,[TIMEOUT]
                        const int minArgCount = 2;
                        const int maxArgCount = 3;
                        if (CodeParser.CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        string message = args[0];
                        CodeMessageAction action;
                        if (args[1].Equals("Information", StringComparison.OrdinalIgnoreCase))
                            action = CodeMessageAction.Information;
                        else if (args[1].Equals("Confirmation", StringComparison.OrdinalIgnoreCase))
                            action = CodeMessageAction.Confirmation;
                        else if (args[1].Equals("Error", StringComparison.OrdinalIgnoreCase))
                            action = CodeMessageAction.Error;
                        else if (args[1].Equals("Warning", StringComparison.OrdinalIgnoreCase))
                            action = CodeMessageAction.Warning;
                        else
                            throw new InvalidCommandException($"Second argument [{args[1]}] must be one of \'Information\', \'Confirmation\', \'Error\' and \'Warning\'", rawCode);

                        string timeout = null;
                        if (minArgCount < args.Count)
                            timeout = args[minArgCount];

                        return new CodeInfo_Message(message, action, timeout);
                    }
                case CodeType.Echo:
                    { // Echo,<Message>[,WARN]
                        const int minArgCount = 1;
                        const int maxArgCount = 2;
                        if (CodeParser.CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        bool warn = false;
                        if (args.Count == maxArgCount)
                        {
                            if (args[1].Equals("WARN", StringComparison.OrdinalIgnoreCase))
                                warn = true;
                        }

                        return new CodeInfo_Echo(args[0], warn);
                    }
                case CodeType.Retrieve:
                    break;
                case CodeType.Visible:
                    break;
                #endregion
                #region 08 StringFormat
                // 08 StringFormat
                case CodeType.StrFormat:
                    return ParseCodeInfoStrFormat(rawCode, args);
                #endregion
                #region 09 System
                // 09 System
                case CodeType.System:
                    break;
                case CodeType.ShellExecute:
                case CodeType.ShellExecuteEx:
                case CodeType.ShellExecuteDelete:
                    {
                        // ShellExecute,<Action>,<FilePath>[,Params][,WorkDir][,%ExitOutVar%]
                        // ShellExecuteEx,<Action>,<FilePath>[,Params][,WorkDir]
                        // ShellExecuteDelete,<Action>,<FilePath>[,Params][,WorkDir][,%ExitOutVar%]

                        const int minArgCount = 2;
                        const int maxArgCount = 5;
                        if (CodeParser.CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        if (type == CodeType.ShellExecuteEx && args.Count == 5)
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        string parameters = null;
                        string workDir = null;
                        string exitOutVar = null;
                        switch (args.Count)
                        {
                            case 3:
                                parameters = args[2];
                                break;
                            case 4:
                                parameters = args[2];
                                workDir = args[3];
                                break;
                            case 5:
                                parameters = args[2];
                                workDir = args[3];
                                exitOutVar = args[4];
                                break;
                        }

                        return new CodeInfo_ShellExecute(args[0], args[1], parameters, workDir, exitOutVar);
                    }
                #endregion
                #region 10 Branch
                // 10 Branch
                case CodeType.Run:
                case CodeType.Exec:
                    { // Run,%PluginFile%,<Section>[,PARAMS]
                        const int minArgCount = 2;
                        if (CodeParser.CheckInfoArgumentCount(args, minArgCount, -1))
                            throw new InvalidCommandException($"Command [{type}] must have at least [{minArgCount}] arguments", rawCode);

                        string pluginFile = args[0];
                        string sectionName = args[1];

                        // Get parameters 
                        List<string> parameters = new List<string>();
                        if (minArgCount < args.Count)
                            parameters.AddRange(args.Skip(minArgCount));

                        return new CodeInfo_RunExec(pluginFile, sectionName, parameters);
                    }
                case CodeType.Loop:
                    {
                        if (args.Count == 1)
                        { // Loop,BREAK
                            if (string.Equals(args[0], "BREAK", StringComparison.OrdinalIgnoreCase))
                                return new CodeInfo_Loop(true);
                            else
                                throw new InvalidCommandException("Invalid form of Command [Loop]", rawCode);
                        }
                        else
                        { // Loop,%PluginFile%,<Section>,<StartIndex>,<EndIndex>[,PARAMS]
                            const int minArgCount = 4;
                            if (CodeParser.CheckInfoArgumentCount(args, minArgCount, -1))
                                throw new InvalidCommandException($"Command [Loop] must have at least [{minArgCount}] arguments", rawCode);

                            // Get parameters 
                            List<string> parameters = new List<string>();
                            if (minArgCount < args.Count)
                                parameters.AddRange(args.Skip(minArgCount));

                            return new CodeInfo_Loop(args[0], args[1], args[2], args[3], parameters);
                        }
                    }
                case CodeType.If:
                    return ParseCodeInfoIf(rawCode, args, addr);
                case CodeType.Else:
                    return ParseCodeInfoElse(rawCode, args, addr);
                case CodeType.Begin:
                case CodeType.End:
                    return new CodeInfo();
                #endregion
                #region 11 Control
                // 11 Control
                case CodeType.Set:
                    { // Set,<VarName>,<VarValue>[,GLOBAL | PERMANENT]
                        const int minArgCount = 2;
                        const int maxArgCount = 3;
                        if (CodeParser.CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        string varName = args[0];
                        // if (varName == null)
                        //    throw new InvalidCommandException($"Variable name [{args[0]}] must start and end with %", rawCode);
                        string varValue = args[1];
                        bool global = false;
                        bool permanent = false;

                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            string arg = args[i];
                            if (string.Equals(arg, "GLOBAL", StringComparison.OrdinalIgnoreCase))
                                global = true;
                            else if (string.Equals(arg, "PREMENENT", StringComparison.OrdinalIgnoreCase))
                                permanent = true;
                            else
                                throw new InvalidCommandException($"Invalid argument [{arg}]", rawCode);
                        }

                        return new CodeInfo_Set(varName, varValue, global, permanent);
                    }
                case CodeType.GetParam:
                    { // GetParam,<Index>,<VarName>
                        const int minArgCount = 2;
                        const int maxArgCount = 2;
                        if (CodeParser.CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        if (int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) == false)
                            throw new InvalidCommandException($"Argument [{args[2]}] is not valid number", rawCode);

                        string varName = args[1];
                        if (varName == null)
                            throw new InvalidCommandException($"Variable name [{args[1]}] must start and end with %", rawCode);

                        return new CodeInfo_GetParam(index, varName);
                    }
                case CodeType.PackParam:
                    { // PackParam,<StartIndex>,<VarName>[,VarNum] -- Cannot figure out how it works
                        const int minArgCount = 2;
                        const int maxArgCount = 3;
                        if (CodeParser.CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        if (int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int startIdx) == false)
                            throw new InvalidCommandException($"Argument [{args[2]}] is not valid number", rawCode);

                        string varName = args[1];
                        if (varName == null)
                            throw new InvalidCommandException($"Variable name [{args[1]}] must start and end with %", rawCode);

                        string varNum = null;
                        if (2 < args.Count)
                        {
                            varNum = args[2];
                            if (varNum == null)
                                throw new InvalidCommandException($"Variable name [{args[2]}] must start and end with %", rawCode);
                        }

                        return new CodeInfo_PackParam(startIdx, varName, varNum);
                    }
                case CodeType.AddVariables:
                    break;
                case CodeType.Exit:
                    break;
                case CodeType.Halt:
                    break;
                case CodeType.Wait:
                    break;
                case CodeType.Beep:
                    break;
                #endregion
                #region 12 External Macro
                // 12 External Macro
                case CodeType.Macro:
                    return new CodeInfo_Macro(macroType, args);
                #endregion
                #region Error
                // Error
                default:
                    throw new InternalParserException($"Wrong CodeType [{type}]");
                #endregion
            }

            // Temp Measure
            return new CodeInfo();
        }

        /// <summary>
        /// Check CodeCommand's argument count
        /// </summary>
        /// <param name="op"></param>
        /// <param name="min"></param>
        /// <param name="max">-1 if unlimited argument</param>
        /// <returns>Return true if invalid</returns>
        public static bool CheckInfoArgumentCount(List<string> op, int min, int max)
        {
            if (max == -1)
            { // Unlimited argument count
                if (op.Count < min)
                    return true;
                else
                    return false;
            }
            else
            {
                if (op.Count < min || max < op.Count)
                    return true;
                else
                    return false;
            }
            
        }
        #endregion

        #region ParseCodeInfoStrFormat, ParseStrFormatType
        public static CodeInfo_StrFormat ParseCodeInfoStrFormat(string rawCode, List<string> args)
        {
            const int minArgCount = 3;
            if (CodeParser.CheckInfoArgumentCount(args, minArgCount, -1))
                throw new InvalidCommandException($"Command [StrFormat] must have at least [{minArgCount}] arguments", rawCode);

            StrFormatType type = ParseStrFormatType(args[0]);
            StrFormatInfo info = new StrFormatInfo(); // Temp Measure

            // Remove StrFormatType
            args.RemoveAt(0);

            switch (type)
            {
                case StrFormatType.Bytes:
                    { // StrFormat,Bytes,<Integer>,<DestVarName>
                        const int argCount = 2;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [StrFormat,{type}] must have [{argCount}] arguments", rawCode);

                        if (Variables.DetermineType(args[1]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[1]}] is not valid variable name", rawCode);
                        else
                            info = new StrFormatInfo_Bytes(args[0], args[1]);
                    }
                    break;
                case StrFormatType.Ceil:
                case StrFormatType.Floor:
                case StrFormatType.Round:
                    {
                        // StrFormat,Ceil,<SizeVar>,<CeilTo>
                        // StrFormat,Floor,<SizeVar>,<FloorTo>
                        // StrFormat,Round,<SizeVar>,<RoundTo>

                        const int argCount = 2;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [StrFormat,{type}] must have [{argCount}] arguments", rawCode);

                        if (Variables.DetermineType(args[0]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[0]}] is not valid variable name", rawCode);
                        else
                            info = new StrFormatInfo_CeilFloorRound(args[0], args[1]);
                    }
                    break;
                case StrFormatType.Date:
                    { // StrFormat,Date,<DestVarName>,<FormatString>
                        const int argCount = 2;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [StrFormat,{type}] must have [{argCount}] arguments", rawCode);

                        if (Variables.DetermineType(args[0]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[0]}] is not valid variable name", rawCode);
                        else
                            info = new StrFormatInfo_Date(args[0], args[1]);
                    }
                    break;
                case StrFormatType.FileName:
                case StrFormatType.DirPath:
                case StrFormatType.Path:
                case StrFormatType.Ext:
                    {
                        // StrFormat,FileName,<FilePath>,<DestVarName>
                        // StrFormat,DirPath,<FilePath>,<DestVarName> -- Same with StrFormat,Path
                        // StrFormat,Ext,<FilePath>,<DestVarName>

                        const int argCount = 2;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [StrFormat,{type}] must have [{argCount}] arguments", rawCode);

                        if (Variables.DetermineType(args[1]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[1]}] is not valid variable name", rawCode); 
                        else
                            info = new StrFormatInfo_Path(args[0], args[1]);
                    }
                    break;
                case StrFormatType.Inc:
                case StrFormatType.Dec:
                case StrFormatType.Mult:
                case StrFormatType.Div:
                    {
                        // StrFormat,Inc,<DestVarName>,<Integer>
                        // StrFormat,Dec,<DestVarName>,<Integer>
                        // StrFormat,Mult,<DestVarName>,<Integer>
                        // StrFormat,Div,<DestVarName>,<Integer>

                        const int argCount = 2;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [StrFormat,{type}] must have [{argCount}] arguments", rawCode);

                        if (Variables.DetermineType(args[0]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[0]}] is not valid variable name", rawCode);
                        else
                            info = new StrFormatInfo_Arithmetic(args[0], args[1]);
                    }
                    break;
                case StrFormatType.Left:
                case StrFormatType.Right:
                    {
                        // StrFormat,Left,<SrcString>,<Integer>,<DestVarName>
                        // StrFormat,Right,<SrcString>,<Integer>,<DestVarName>

                        const int argCount = 3;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [StrFormat,{type}] must have [{argCount}] arguments", rawCode);

                        if (Variables.DetermineType(args[2]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[2]}] is not valid variable name", rawCode);
                        
                        info = new StrFormatInfo_LeftRight(args[0], args[1], args[2]);
                    }
                    break;
                case StrFormatType.SubStr:
                    { // StrFormat,SubStr,<SrcString>,<StartPos>,<Length>,<DestVarName>
                        const int argCount = 4;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [StrFormat,{type}] must have [{argCount}] arguments", rawCode);

                        if (Variables.DetermineType(args[3]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[3]}] is not valid variable name", rawCode);

                        info = new StrFormatInfo_SubStr(args[0], args[1], args[2], args[3]);
                    }
                    break;
                case StrFormatType.Len:
                    { // StrFormat,Len,<SrcString>,<DestVarName>
                        const int argCount = 2;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [StrFormat,{type}] must have [{argCount}] arguments", rawCode);

                        if (Variables.DetermineType(args[1]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[1]}] is not valid variable name", rawCode);

                        info = new StrFormatInfo_Len(args[0], args[1]);
                    }
                    break;
                case StrFormatType.LTrim:
                case StrFormatType.RTrim:
                case StrFormatType.CTrim:
                    {
                        // StrFormat,LTrim,<SrcString>,<Integer>,<DestVarName>
                        // StrFormat,RTrim,<SrcString>,<Integer>,<DestVarName>
                        // StrFormat,CTrim,<SrcString>,<Chars>,<DestVarName>

                        const int argCount = 3;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [StrFormat,{type}] must have [{argCount}] arguments", rawCode);

                        if (Variables.DetermineType(args[2]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[2]}] is not valid variable name", rawCode);

                        info = new StrFormatInfo_Trim(args[0], args[1], args[2]);
                    }
                    break;
                case StrFormatType.NTrim:
                    { // StrFormat,NTrim,<SrcString>,<DestVarName>

                        const int argCount = 2;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [StrFormat,{type}] must have [{argCount}] arguments", rawCode);

                        if (Variables.DetermineType(args[1]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[1]}] is not valid variable name", rawCode);

                        info = new StrFormatInfo_NTrim(args[0], args[1]);
                    }
                    break;
                case StrFormatType.Pos:
                    { // StrFormat,Pos,<SrcString>,<SubString>,<DestVarName>
                        const int argCount = 3;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [StrFormat,{type}] must have [{argCount}] arguments", rawCode);

                        if (Variables.DetermineType(args[2]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[2]}] is not valid variable name", rawCode);
                        
                        info = new StrFormatInfo_Pos(args[0], args[1], args[2]);
                    }
                    break;
                case StrFormatType.Replace:
                case StrFormatType.ReplaceX:
                    {
                        // StrFormat,Replace,<SrcString>,<ToBeReplaced>,<ReplaceWith>,<DestVarName>
                        // StrFormat,ReplaceX,<SrcString>,<ToBeReplaced>,<ReplaceWith>,<DestVarName>

                        const int argCount = 4;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [StrFormat,{type}] must have [{argCount}] arguments", rawCode);

                        if (Variables.DetermineType(args[3]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[3]}] is not valid variable name", rawCode);
                        else
                            info = new StrFormatInfo_Replace(args[0], args[1], args[2], args[3]);
                    }
                    break;
                case StrFormatType.ShortPath:
                case StrFormatType.LongPath:
                    {
                        // StrFormat,ShortPath,<SrcString>,<DestVarName>
                        // StrFormat,LongPath,<SrcString>,<DestVarName>

                        const int argCount = 2;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [StrFormat,{type}] must have [{argCount}] arguments", rawCode);

                        if (Variables.DetermineType(args[1]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[1]}] is not valid variable name", rawCode);

                        info = new StrFormatInfo_ShortLongPath(args[0], args[1]);
                    }
                    break;
                case StrFormatType.Split:
                    { // StrFormat,Split,<SrcString>,<Delimeter>,<Index>,<DestVarName>
                        const int argCount = 4;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [StrFormat,{type}] must have [{argCount}] arguments", rawCode);

                        if (Variables.DetermineType(args[3]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[3]}] is not valid variable name", rawCode);

                        info = new StrFormatInfo_Split(args[0], args[1], args[2], args[3]);
                    }
                    break;
                // Error
                default:
                    throw new InternalParserException($"Wrong StrFormatType [{type}]");
            }

            return new CodeInfo_StrFormat(type, info);
        }

        public static StrFormatType ParseStrFormatType(string typeStr)
        {
            // There must be no number in typeStr
            if (!Regex.IsMatch(typeStr, @"^[A-Za-z_]+$", RegexOptions.Compiled))
                throw new InvalidCommandException($"Wrong CodeType [{typeStr}], Only alphabet and underscore can be used as opcode");

            bool invalid = false;
            if (Enum.TryParse(typeStr, true, out StrFormatType type) == false)
                invalid = true;
            if (Enum.IsDefined(typeof(StrFormatType), type) == false)
                invalid = true;

            if (invalid)
                throw new InvalidCommandException($"Invalid StrFormatType [{typeStr}]");

            return type;
        }
        #endregion

        #region ParseCodeInfoIf, ForgeIfEmbedCommand
        public static CodeInfo_If ParseCodeInfoIf(string rawCode, List<string> args, SectionAddress addr)
        {
            if (args.Count < 2)
                throw new InvalidCommandException("[If] must have form of [If],<Condition>,<Command>", rawCode);

            int cIdx = 0;
            bool notFlag = false;
            if (string.Equals(args[0], "Not", StringComparison.OrdinalIgnoreCase))
            {
                notFlag = true;
                cIdx++;
            }

            BranchCondition cond;
            CodeCommand embCmd;
            int occurence = FileHelper.CountStringOccurrences(args[cIdx], "%"); // %Joveler%
            bool match = Regex.IsMatch(args[cIdx], @"(#\d+)", RegexOptions.Compiled); // #1
            if ((occurence != 0 && occurence % 2 == 0) || match) // BranchCondition - Compare series
            {
                string condStr = args[cIdx + 1];
                BranchConditionType condType;

                if (string.Equals(condStr, "Equal", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(condStr, "==", StringComparison.OrdinalIgnoreCase))
                    condType = BranchConditionType.Equal;
                else if (string.Equals(condStr, "EqualX", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(condStr, "===", StringComparison.OrdinalIgnoreCase))
                    condType = BranchConditionType.EqualX;
                else if (string.Equals(condStr, "Smaller", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(condStr, "<", StringComparison.OrdinalIgnoreCase))
                    condType = BranchConditionType.Smaller;
                else if (string.Equals(condStr, "Bigger", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(condStr, ">", StringComparison.OrdinalIgnoreCase))
                    condType = BranchConditionType.Bigger;
                else if (string.Equals(condStr, "SmallerEqual", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(condStr, "<=", StringComparison.OrdinalIgnoreCase))
                    condType = BranchConditionType.SmallerEqual;
                else if (string.Equals(condStr, "BiggerEqual", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(condStr, ">=", StringComparison.OrdinalIgnoreCase))
                    condType = BranchConditionType.BiggerEqual;
                else if (string.Equals(condStr, "NotEqual", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(condStr, "!=", StringComparison.OrdinalIgnoreCase))
                {
                    if (notFlag)
                        throw new InvalidCommandException("Branch condition [Not] cannot be duplicated", rawCode);
                    notFlag = true;
                    condType = BranchConditionType.Equal;
                }
                else
                    throw new InvalidCommandException($"Wrong branch condition [{condStr}]", rawCode);

                string compArg1 = args[cIdx];
                string compArg2 = args[cIdx + 2];
                cond = new BranchCondition(condType, notFlag, compArg1, compArg2);
                embCmd = ForgeIfEmbedCommand(rawCode, args.Skip(cIdx + 3).ToList(), addr);

            }
            else // IfSubOpcode - Non-Compare series
            {
                int embIdx;
                string condStr = args[cIdx];
                if (string.Equals(condStr, "ExistFile", StringComparison.OrdinalIgnoreCase))
                {
                    cond = new BranchCondition(BranchConditionType.ExistFile, notFlag, args[cIdx + 1]);
                    embIdx = cIdx + 2;
                }
                else if (string.Equals(condStr, "NotExistFile", StringComparison.OrdinalIgnoreCase)) // Deprecated
                {
                    if (notFlag)
                        throw new InvalidCommandException("Branch condition [Not] cannot be duplicated", rawCode);
                    cond = new BranchCondition(BranchConditionType.ExistFile, true, args[cIdx + 1]);
                    embIdx = cIdx + 2;
                }
                else if (string.Equals(condStr, "ExistDir", StringComparison.OrdinalIgnoreCase))
                {
                    cond = new BranchCondition(BranchConditionType.ExistDir, notFlag, args[cIdx + 1]);
                    embIdx = cIdx + 2;
                }
                else if (string.Equals(condStr, "NotExistDir", StringComparison.OrdinalIgnoreCase)) // Deprecated
                {
                    if (notFlag)
                        throw new InvalidCommandException("Branch condition [Not] cannot be duplicated", rawCode);
                    cond = new BranchCondition(BranchConditionType.ExistDir, true, args[cIdx + 1]);
                    embIdx = cIdx + 2;
                }
                else if (string.Equals(condStr, "ExistSection", StringComparison.OrdinalIgnoreCase))
                {
                    cond = new BranchCondition(BranchConditionType.ExistSection, true, args[cIdx + 1], args[cIdx + 2]);
                    embIdx = cIdx + 3;
                }
                else if (string.Equals(condStr, "NotExistSection", StringComparison.OrdinalIgnoreCase)) // Deprecated
                {
                    if (notFlag)
                        throw new InvalidCommandException("Branch condition [Not] cannot be duplicated", rawCode);
                    cond = new BranchCondition(BranchConditionType.ExistSection, true, args[cIdx + 1], args[cIdx + 2]);
                    embIdx = cIdx + 3;
                }
                else if (string.Equals(condStr, "ExistRegSection", StringComparison.OrdinalIgnoreCase))
                {
                    cond = new BranchCondition(BranchConditionType.ExistRegSection, notFlag, args[cIdx + 1], args[cIdx + 2]);
                    embIdx = cIdx + 3;
                }
                else if (string.Equals(condStr, "NotExistRegSection", StringComparison.OrdinalIgnoreCase)) // deprecated
                {
                    if (notFlag)
                        throw new InvalidCommandException("Branch condition [Not] cannot be duplicated", rawCode);
                    cond = new BranchCondition(BranchConditionType.ExistRegSection, true, args[cIdx + 1], args[cIdx + 2]);
                    embIdx = cIdx + 3;
                }
                else if (string.Equals(condStr, "ExistRegKey", StringComparison.OrdinalIgnoreCase))
                {
                    cond = new BranchCondition(BranchConditionType.ExistRegKey, notFlag, args[cIdx + 1], args[cIdx + 2], args[cIdx + 3]);
                    embIdx = cIdx + 4;
                }
                else if (string.Equals(condStr, "NotExistRegKey", StringComparison.OrdinalIgnoreCase)) // deprecated 
                {
                    if (notFlag)
                        throw new InvalidCommandException("Branch condition [Not] cannot be duplicated", rawCode);
                    cond = new BranchCondition(BranchConditionType.ExistRegKey, true, args[cIdx + 1], args[cIdx + 2], args[cIdx + 3]);
                    embIdx = cIdx + 4;
                }
                else if (string.Equals(condStr, "ExistVar", StringComparison.OrdinalIgnoreCase))
                {
                    cond = new BranchCondition(BranchConditionType.ExistVar, notFlag, args[cIdx + 1]);
                    embIdx = cIdx + 2;
                }
                else if (string.Equals(condStr, "NotExistVar", StringComparison.OrdinalIgnoreCase))  // deprecated 
                {
                    if (notFlag)
                        throw new InvalidCommandException("Branch condition [Not] cannot be duplicated", rawCode);
                    cond = new BranchCondition(BranchConditionType.ExistVar, true, args[cIdx + 1]);
                    embIdx = cIdx + 2;
                }
                else if (string.Equals(condStr, "ExistMacro", StringComparison.OrdinalIgnoreCase))
                {
                    cond = new BranchCondition(BranchConditionType.ExistMacro, notFlag, args[cIdx + 1]);
                    embIdx = cIdx + 2;
                }
                else if (string.Equals(condStr, "NotExistMacro", StringComparison.OrdinalIgnoreCase))  // deprecated 
                {
                    if (notFlag)
                        throw new InvalidCommandException("Branch condition [Not] cannot be duplicated", rawCode);
                    cond = new BranchCondition(BranchConditionType.ExistMacro, true, args[cIdx + 1]);
                    embIdx = cIdx + 2;
                }
                else if (string.Equals(condStr, "Ping", StringComparison.OrdinalIgnoreCase))
                {
                    cond = new BranchCondition(BranchConditionType.Ping, true, args[cIdx + 1]);
                    embIdx = cIdx + 2;
                }
                else if (string.Equals(condStr, "Online", StringComparison.OrdinalIgnoreCase))
                {
                    cond = new BranchCondition(BranchConditionType.Online, true);
                    embIdx = cIdx + 1;
                }
                else if (string.Equals(condStr, "Question", StringComparison.OrdinalIgnoreCase))
                {
                    Match m = Regex.Match(args[cIdx + 2], @"([0-9]+)$", RegexOptions.Compiled);
                    if (m.Success)
                    {
                        cond = new BranchCondition(BranchConditionType.Question, true, args[cIdx + 1], args[cIdx + 2], args[cIdx + 3]);
                        embIdx = cIdx + 4;
                    }
                    else
                    {
                        cond = new BranchCondition(BranchConditionType.Question, true, args[cIdx + 1]);
                        embIdx = cIdx + 2;
                    }
                }
                else
                    throw new InvalidCommandException($"Wrong branch condition [{condStr}]", rawCode);
                embCmd = ForgeIfEmbedCommand(rawCode, args.Skip(embIdx).ToList(), addr);
            }

            return new CodeInfo_If(cond, embCmd);
        }

        public static CodeInfo_Else ParseCodeInfoElse(string rawCode, List<string> args, SectionAddress addr)
        {
            CodeCommand embCmd = ForgeIfEmbedCommand(rawCode, args, addr); // Skip Else
            return new CodeInfo_Else(embCmd);
        }

        public static CodeCommand ForgeIfEmbedCommand(string rawCode, List<string> args, SectionAddress addr)
        {
            CodeCommand embed = ParseCommandFromSlicedArgs(rawCode, args, addr);
            return embed;
        }
        #endregion

        #region CompileBranchCodeBlock
        public static void CompileBranchCodeBlock(List<CodeCommand> codeList, out List<CodeCommand> compiledList)
        {
            bool elseFlag = false;
            compiledList = new List<CodeCommand>();

            for (int i = 0; i < codeList.Count; i++)
            {
                CodeCommand cmd = codeList[i];
                if (cmd.Type == CodeType.If)
                { // Change it to IfCompact, and parse Begin - End
                    CodeInfo_If info = cmd.Info as CodeInfo_If;
                    if (info == null)
                        throw new InternalParserException($"Error while parsing command [{cmd.RawCode}]");

                    i = ParseNestedIf(cmd, codeList, i, compiledList);
                    elseFlag = true;

                    CompileBranchCodeBlock(info.Link, out List<CodeCommand> newLinkList);
                    info.Link = newLinkList;
                }
                else if (cmd.Type == CodeType.Else) // SingleLine or MultiLine?
                { // Compile to ElseCompact
                    CodeInfo_Else info = cmd.Info as CodeInfo_Else;
                    if (info == null)
                        throw new InternalParserException($"Error while parsing command [{cmd.RawCode}]");

                    if (elseFlag)
                    {
                        compiledList.Add(cmd);
                        i = ParseNestedElse(cmd, codeList, i, compiledList, out elseFlag);

                        CompileBranchCodeBlock(info.Link, out List<CodeCommand> newLinkList);
                        info.Link = newLinkList;
                    }
                    else
                        throw new InvalidCodeCommandException("Else must be used after If", cmd);
                        
                }
                //else if (cmd.Type == CodeType.End)
                //{
                //    // elseFlag = true;
                //    // throw new InvalidCodeCommandException("End must be matched with Begin", cmd);
                //}
                else if (cmd.Type != CodeType.Begin && cmd.Type != CodeType.End) // The other operands - just copy
                {
                    elseFlag = false;
                    compiledList.Add(cmd);
                }
            }
        }
        #endregion

        #region ParseNestedIf, ParsedNestedElse, MatchBeginWithEnd
        /// <summary>
        /// 
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="codeList"></param>
        /// <param name="codeListIdx"></param>
        /// <param name="newList"></param>
        /// <returns>Next codeListIdx</returns>
        private static int ParseNestedIf(CodeCommand cmd, List<CodeCommand> codeList, int codeListIdx, List<CodeCommand> newList)
        {
            // RawCode : If,%A%,Equal,B,Echo,Success
            // Condition : Equal,%A%,B,Echo,Success
            // Run if condition is met : Echo,Success
            // BakeryCommand compiledCmd; // Compiled If : IfCompact,Equal,%A%,B

            CodeCommand ifCmd = cmd;

            // <Raw>
            // If,%A%,Equal,B,Echo,Success
            while (true)
            {
                CodeInfo_If info = ifCmd.Info as CodeInfo_If;
                if (info == null)
                    throw new InternalParserException("Invalid CodeInfo_If while processing nested [If]");

                newList.Add(ifCmd);
                info.LinkParsed = true;
                info.Link.Add(info.Embed);
                if (info.Embed.Type == CodeType.If) // Nested If
                {
                    ifCmd = info.Embed;
                    newList = info.Link;
                }
                else if (info.Embed.Type == CodeType.Begin) // Multiline If (Begin-End)
                {
                    // Find proper End
                    int endIdx = MatchBeginWithEnd(codeList, codeListIdx + 1);
                    if (endIdx == -1)
                        throw new InvalidCodeCommandException("Begin must be matched with End", cmd);
                    info.Link.AddRange(codeList.Skip(codeListIdx + 1).Take(endIdx - codeListIdx - 1));
                    return endIdx;
                }
                else if (info.Embed.Type == CodeType.Else || info.Embed.Type == CodeType.End) // Cannot come here!
                {
                    throw new InvalidCodeCommandException($"{info.Embed.Type} cannot be used with If", cmd);
                }
                else // Singleline If
                {
                    return codeListIdx;
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
        private static int ParseNestedElse(CodeCommand cmd, List<CodeCommand> codeList, int codeListIdx, List<CodeCommand> newList, out bool elseFlag)
        {
            CodeInfo_Else info = cmd.Info as CodeInfo_Else;
            if (info == null)
                throw new InternalParserException("Invalid CodeInfo_Else while processing nested [Else]");

            newList.Add(cmd);
            info.LinkParsed = true;
            info.Link.Add(info.Embed);

            CodeCommand elseEmbCmd = info.Embed;
            if (elseEmbCmd.Type == CodeType.If) // Nested If
            {
                CodeCommand ifCmd = elseEmbCmd;
                List<CodeCommand> nestList = info.Link;
                while (true)
                {
                    CodeInfo_If ifInfo = ifCmd.Info as CodeInfo_If;
                    if (info == null)
                        throw new InternalParserException("Invalid CodeInfo_If while processing nested [If]");

                    nestList.Add(ifCmd);
                    ifInfo.LinkParsed = true;
                    ifInfo.Link.Add(info.Embed);
                    if (info.Embed.Type == CodeType.If) // Nested If
                    {
                        ifCmd = ifInfo.Embed;
                        nestList = ifInfo.Link;
                    }
                    else if (info.Embed.Type == CodeType.Begin) // Multiline If (Begin-End)
                    {
                        // Find proper End
                        int endIdx = MatchBeginWithEnd(codeList, codeListIdx + 1);
                        if (endIdx == -1)
                            throw new InvalidCodeCommandException("Begin must be matched with End", ifCmd);
                        info.Link.AddRange(codeList.Skip(codeListIdx + 1).Take(endIdx - codeListIdx - 1));
                        elseFlag = true;
                        return endIdx;
                    }
                    else if (info.Embed.Type == CodeType.Else || info.Embed.Type == CodeType.End) // Cannot come here!
                    {
                        throw new InvalidCodeCommandException($"{info.Embed.Type} cannot be used with If", cmd);
                    }
                    else // Singleline If
                    {
                        elseFlag = true;
                        return codeListIdx;
                    }
                }
            }
            else if (elseEmbCmd.Type == CodeType.Begin)
            {
                // Find proper End
                int endIdx = MatchBeginWithEnd(codeList, codeListIdx + 1);
                if (endIdx == -1)
                    throw new InvalidCodeCommandException("Begin must be matched with End", cmd);
                info.Link.AddRange(codeList.Skip(codeListIdx + 1).Take(endIdx - codeListIdx - 1)); // Remove Begin and End
                elseFlag = true;
                return endIdx;
            }
            else if (elseEmbCmd.Type == CodeType.Else || elseEmbCmd.Type == CodeType.End)
            {
                throw new InvalidCodeCommandException($"{elseEmbCmd.Type} cannot be used with Else", cmd);
            }
            else // Normal codes
            {
                elseFlag = false;
                return codeListIdx;
            }
        }

        // Process nested Begin ~ End block
        private static int MatchBeginWithEnd(List<CodeCommand> codeList, int codeListIdx)
        {
            int nestedBeginEnd = 1;
            // bool beginExist = false;
            bool finalizedWithEnd = false;

            for (; codeListIdx < codeList.Count; codeListIdx++)
            {
                CodeCommand cmd = codeList[codeListIdx];
                if (cmd.Type == CodeType.If) // To check If,<Condition>,Begin
                {
                    while (true)
                    {
                        CodeInfo_If info = cmd.Info as CodeInfo_If;
                        if (info == null)
                            throw new InternalParserException("Invalid CodeInfo_If while matching [Begin] with [End]");

                        if (info.Embed.Type == CodeType.If) // Nested If
                        {
                            cmd = info.Embed;
                        }
                        else if (info.Embed.Type == CodeType.Begin)
                        {
                            // beginExist = true;
                            nestedBeginEnd++;
                            break;
                        }
                        else
                            break;
                    }
                }
                else if (cmd.Type == CodeType.Else)
                {
                    CodeInfo_Else info = cmd.Info as CodeInfo_Else;
                    if (info == null)
                        throw new InternalParserException("Invalid CodeInfo_Else while matching [Begin] with [End]");

                    CodeCommand ifCmd = info.Embed;
                    if (ifCmd.Type == CodeType.If) // Nested If
                    {
                        while (true)
                        {
                            CodeInfo_If embedInfo = ifCmd.Info as CodeInfo_If;
                            if (embedInfo == null)
                                throw new InternalParserException("Invalid CodeInfo_If while matching [Begin] with [End]");

                            if (embedInfo.Embed.Type == CodeType.If) // Nested If
                            {
                                ifCmd = embedInfo.Embed;
                            }
                            else if (embedInfo.Embed.Type == CodeType.Begin)
                            {
                                // beginExist = true;
                                nestedBeginEnd++;
                                break;
                            }
                            else
                                break;
                        }
                    }
                    else if (ifCmd.Type == CodeType.Begin)
                    {
                        // beginExist = true;
                        nestedBeginEnd++;
                    }
                }
                else if (cmd.Type == CodeType.End)
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
            // if (beginExist && finalizedWithEnd && nestedBeginEnd == 0)
            if (finalizedWithEnd && nestedBeginEnd == 0)
                return codeListIdx;
            else
                return -1;
        }
        #endregion
    }
}
