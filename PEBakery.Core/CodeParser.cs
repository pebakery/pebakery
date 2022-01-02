/*
    Copyright (C) 2016-2022 Hajin Jang
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

    Additional permission under GNU GPL version 3 section 7

    If you modify this program, or any covered work, by linking
    or combining it with external libraries, containing parts
    covered by the terms of various license, the licensors of
    this program grant you additional permission to convey the
    resulting work. An external library is a library which is
    not derived from or based on this program. 
*/

using Microsoft.Win32;
using PEBakery.Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PEBakery.Core
{
    public class CodeParser
    {
        #region Fields and Properties
        private readonly ScriptSection _section;
        private readonly Options _opts;
        #endregion

        #region Options
        public struct Options
        {
            // Optimization
            public bool OptimizeCode;
            // Compatibility
            public bool AllowLegacyBranchCondition;
            public bool AllowLegacyRegWrite;
            public bool AllowLegacyInterfaceCommand;
            public bool AllowLegacySectionParamCommand;

            public static Options CreateOptions(Setting setting, CompatOption compat)
            {
                return new Options
                {
                    OptimizeCode = setting.General.OptimizeCode,
                    AllowLegacyBranchCondition = compat.LegacyBranchCondition,
                    AllowLegacyRegWrite = compat.LegacyRegWrite,
                    AllowLegacyInterfaceCommand = compat.LegacyInterfaceCommand,
                    AllowLegacySectionParamCommand = compat.LegacySectionParamCommand,
                };
            }
        }
        #endregion

        #region Constructor
        public CodeParser(ScriptSection section, Options options)
        {
            _section = section;
            _opts = options;
        }

        public CodeParser(ScriptSection section, Setting setting, CompatOption compat)
        {
            _section = section;
            _opts = Options.CreateOptions(setting, compat);
        }
        #endregion

        #region ParseStatement, ParseStatements
        public CodeCommand ParseStatement(string rawCode)
        {
            int idx = 0;
            List<string> list = new List<string> { rawCode };

            try
            {
                return ParseCommand(list, ref idx);
            }
            catch (Exception e)
            {
                return new CodeCommand(rawCode.Trim(), _section, CodeType.Error, new CodeInfo_Error(e), _section.LineIdx + idx + 1);
            }
        }

        public Task<(CodeCommand[] cmds, List<LogInfo> errLogs)> ParseStatementsAsync()
        {
            return Task.Run(ParseStatements);
        }

        public Task<(CodeCommand[] cmds, List<LogInfo> errLogs)> ParseStatementsAsync(IReadOnlyList<string> lines)
        {
            return Task.Run(() => ParseStatements(lines));
        }

        public (CodeCommand[] cmds, List<LogInfo> errLogs) ParseStatements() => ParseStatements(_section.Lines);

        public (CodeCommand[] cmds, List<LogInfo> errLogs) ParseStatements(IReadOnlyList<string> lines)
        {
            // Select Code sections and compile
            List<CodeCommand> cmds = new List<CodeCommand>();
            for (int i = 0; i < lines.Count; i++)
            {
                try
                {
                    CodeCommand cmd = ParseCommand(lines, ref i);
                    cmds.Add(cmd);
                }
                catch (InvalidCommandException e)
                {
                    CodeCommand error = new CodeCommand(e.RawLine, _section, CodeType.Error, new CodeInfo_Error(e), _section.LineIdx + i + 1);
                    cmds.Add(error);
                }
                catch (Exception e)
                {
                    CodeCommand error = new CodeCommand(lines[i].Trim(), _section, CodeType.Error, new CodeInfo_Error(e), _section.LineIdx + i + 1);
                    cmds.Add(error);
                }
            }

            List<LogInfo> errLogs = cmds
                .Where(x => x.Type == CodeType.Error)
                .Select(x => new LogInfo(LogState.Error, x.Info.Cast<CodeInfo_Error>().ErrorMessage, x))
                .ToList();

            List<CodeCommand> foldedList = cmds.Where(x => x.Type != CodeType.None).ToList();
            try
            {
                FoldBranchCodeBlock(foldedList, out foldedList);
            }
            catch (InvalidCodeCommandException e)
            {
                errLogs.Add(new LogInfo(LogState.Error, $"Cannot parse section [{_section.Name}] : {Logger.LogExceptionMessage(e)}", e.Cmd));
                CodeCommand error = new CodeCommand(e.Cmd.RawCode, e.Cmd.Section, CodeType.Error, new CodeInfo_Error(e), e.Cmd.LineIdx);
                foldedList.Add(error);
            }

            if (_opts.OptimizeCode)
                foldedList = CodeOptimizer.Optimize(foldedList);

            return (foldedList.ToArray(), errLogs);
        }
        #endregion

        #region GetNextArgument
        public static (string Next, string Remainder) GetNextArgument(string str)
        {
            str = str.Trim();

            int dqIdx = str.IndexOf("\"", StringComparison.Ordinal);

            if (dqIdx == 0) // With DoubleQuote, dqIdx should be 0
            { // Ex) "   Return SetError(@error,0,0)",Append
                // [   Return SetError(@error,0,0)], [Append]
                int nextIdx = str.IndexOf('\"', 1);
                while (true)
                {
                    if (nextIdx == -1) // Error, double-quote must be multiple of 2
                        throw new InvalidCommandException("Double-quote's number should be an even number");

                    // Ignore ""
                    // Ex) Echo,"Hello""World"
                    if (nextIdx + 1 < str.Length && str[nextIdx + 1] == '\"') // Matched ""
                    {
                        if (nextIdx + 2 < str.Length)
                            nextIdx = str.IndexOf('\"', nextIdx + 2);
                        else
                            nextIdx = -1;
                    }
                    else
                    {
                        break;
                    }
                }

                int pIdx = str.IndexOf(",", nextIdx, StringComparison.Ordinal);

                // There should be only whitespace in between ["] and [,]
                if (pIdx == -1) // Last one
                {
                    string whitespace = str[(nextIdx + 1)..].Trim();
                    if (0 < whitespace.Length)
                        throw new InvalidCommandException("Syntax error");

                    string preNext = str[..(nextIdx + 1)].Trim();  // ["   Return SetError(@error,0,0)"]
                    string next = preNext[1..^1]; // [   Return SetError(@error,0,0)]
                    return (next, null);
                }
                else // [   Return SetError(@error,0,0)], [Append]
                {
                    string whitespace = str[(nextIdx + 1)..pIdx].Trim();
                    if (0 < whitespace.Length)
                        throw new InvalidCommandException("Syntax error");

                    string preNext = str[..(nextIdx + 1)].Trim();
                    string next = preNext[1..^1];
                    string remainder = str[(pIdx + 1)..].Trim();
                    return (next, remainder);
                }
            }
            else // No double-quote for now
            { // Ex) FileCreateBlank,#3.au3
                int pIdx = str.IndexOf(",", StringComparison.Ordinal);
                if (pIdx == -1) // Last one
                {
                    return (str, null);
                }
                else // [FileCreateBlank], [#3.au3]
                {
                    string next = str[..pIdx].Trim();
                    string remainder = str[(pIdx + 1)..].Trim();
                    return (next, remainder);
                }
            }
        }
        #endregion

        #region ParseCommand, ParseCommandFromSlicedArgs, ParseCodeType, ParseArguments
        private CodeCommand ParseCommand(IReadOnlyList<string> rawCodes, ref int idx)
        {
            // Command's line number in physical file
            int lineIdx = _section.LineIdx + 1 + idx;

            // Remove whitespace of rawCode's from start and end
            string rawCode = rawCodes[idx].Trim();

            // Check if rawCode is empty
            if (rawCode.Length == 0)
                return new CodeCommand(string.Empty, _section, CodeType.None, null, lineIdx);

            // Line Comment Identifier : '//', '#', ';'
            if (rawCode[0] == '/' || rawCode[0] == '#' || rawCode[0] == ';')
                return new CodeCommand(rawCode, _section, CodeType.Comment, null, lineIdx);

            // Split with period
            (string codeTypeStr, string remainder) = GetNextArgument(rawCode);

            // Parse CodeType
            CodeType type = ParseCodeType(codeTypeStr, out string macroType);

            // Check double-quote's occurence - must be 2n
            if (StringHelper.CountSubStr(rawCode, "\"") % 2 == 1)
                throw new InvalidCommandException("Double-quote's number should be even", rawCode);

            // Parse Arguments
            List<string> args = new List<string>();
            while (remainder != null)
            {
                string nextArg;
                (nextArg, remainder) = GetNextArgument(remainder);
                args.Add(nextArg);
            }

            // Check if last operand is \ - MultiLine check - only if one or more operands exists
            if (0 < args.Count)
            {
                while (args.Last().Equals(@"\", StringComparison.Ordinal) &&
                    2 < rawCode.Length && rawCode.Substring(rawCode.Length - 2, 2).Equals(@",\", StringComparison.Ordinal))
                { // Split next line and append to List<string> operands
                    if (rawCodes.Count <= idx) // Section ended with \, invalid grammar!
                        throw new InvalidCommandException(@"Last command of a section cannot end with '\'", rawCode);

                    // Get next raw code
                    string nextRawCode = rawCodes[idx + 1].Trim();

                    // Check if nextRawCode is Empty / Comment
                    if (nextRawCode.Length == 0 ||
                        rawCode.StartsWith("//", StringComparison.Ordinal) ||
                        rawCode.StartsWith("#", StringComparison.Ordinal) ||
                        rawCode.StartsWith(";", StringComparison.Ordinal))
                        throw new InvalidCommandException(@"Valid command should be placed after '\'", rawCode);

                    // Parse next raw code
                    rawCode += Environment.NewLine + nextRawCode;
                    args.RemoveAt(args.Count - 1); // Remove Last '\'
                    remainder = nextRawCode;
                    do
                    {
                        string nextArg;
                        (nextArg, remainder) = GetNextArgument(remainder);
                        args.Add(nextArg);
                    }
                    while (remainder != null);

                    // Increase index
                    idx++;
                }
            }

            // Create instance of command
            CodeInfo info = ParseCodeInfo(rawCode, ref type, macroType, args, lineIdx);
            return new CodeCommand(rawCode, _section, type, info, lineIdx);

            // [Process] <- LineIdx
            // Echo,A <- if idx is 0, should point here. Add 1 to solve this.
            // Echo,B
        }

        /// <summary>
        /// Used to get Embedded Command from If, Else
        /// </summary>
        /// <returns></returns>
        private CodeCommand ParseStatementFromSlicedArgs(string rawCode, List<string> args, int lineIdx)
        {
            CodeType type;

            // Parse type
            string macroType;
            try
            {
                type = ParseCodeType(args[0], out macroType);
            }
            catch (InvalidCommandException e)
            {
                throw new InvalidCommandException(e.Message, rawCode);
            }

            try
            {
                CodeInfo info = ParseCodeInfo(rawCode, ref type, macroType, args.Skip(1).ToList(), lineIdx);
                return new CodeCommand(rawCode, _section, type, info, lineIdx);
            }
            catch (InvalidCommandException e)
            {
                CodeCommand error = new CodeCommand(rawCode, _section, CodeType.Error, new CodeInfo_Error(Logger.LogExceptionMessage(e)), lineIdx);
                throw new InvalidCodeCommandException(e.Message, error);
            }
        }

        public CodeType ParseCodeType(string typeStr, out string macroType)
        {
            macroType = null;

            // There must be no number in typeStr
            if (!Regex.IsMatch(typeStr, @"^[A-Za-z0-9_]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant))
                throw new InvalidCommandException($"Invalid CodeType [{typeStr}], Only alphabet, number and underscore can be used as CodeType");

            bool isMacro = !Enum.TryParse(typeStr, true, out CodeType type) ||
                           !Enum.IsDefined(type) ||
                           type == CodeType.None ||
                           type == CodeType.Error ||
                           type == CodeType.Comment ||
                           !_opts.AllowLegacyInterfaceCommand && type == CodeType.Visible ||
                           !_opts.AllowLegacySectionParamCommand && type == CodeType.PackParam ||
                           type == CodeType.Macro ||
                           CodeCommand.OptimizedCodeType.Contains(type);

            if (isMacro)
            {
                type = CodeType.Macro;
                macroType = typeStr;
            }

            return type;
        }
        #endregion

        #region ParseCodeInfo
        public CodeInfo ParseCodeInfo(string rawCode, ref CodeType type, string macroType, List<string> args, int lineIdx)
        {
            switch (type)
            {
                #region 00 Misc
                case CodeType.None:
                case CodeType.Comment:
                case CodeType.Error:
                    return null;
                #endregion
                #region 01 File
                case CodeType.FileCopy:
                    { // FileCopy,<SrcFile>,<DestPath>[,PRESERVE][,NOWARN][,NOREC]
                        const int minArgCount = 2;
                        const int maxArgCount = 5;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        bool preserve = false;
                        bool noWarn = false;
                        bool noRec = false;

                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            string arg = args[i];
                            if (arg.Equals("PRESERVE", StringComparison.OrdinalIgnoreCase))
                            {
                                if (preserve)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                preserve = true;
                            }
                            else if (arg.Equals("NOWARN", StringComparison.OrdinalIgnoreCase))
                            {
                                if (noWarn)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                noWarn = true;
                            }
                            else if (arg.Equals("NOREC", StringComparison.OrdinalIgnoreCase)) // no recursive wildcard copy
                            {
                                if (noRec)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                noRec = true;
                            }
                            else
                            {
                                throw new InvalidCommandException($"Invalid optional argument or flag [{arg}]", rawCode);
                            }
                        }

                        return new CodeInfo_FileCopy(args[0], args[1], preserve, noWarn, noRec);
                    }
                case CodeType.FileDelete:
                    { // FileDelete,<FilePath>[,NOWARN][,NOREC]
                        const int minArgCount = 1;
                        const int maxArgCount = 3;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        string filePath = args[0];
                        bool noWarn = false;
                        bool noRec = false;

                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            string arg = args[i];
                            if (arg.Equals("NOWARN", StringComparison.OrdinalIgnoreCase))
                            {
                                if (noWarn)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                noWarn = true;
                            }
                            else if (arg.Equals("NOREC", StringComparison.OrdinalIgnoreCase)) // no recursive wildcard copy
                            {
                                if (noRec)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                noRec = true;
                            }
                            else
                            {
                                throw new InvalidCommandException($"Invalid optional argument or flag [{arg}]", rawCode);
                            }
                        }

                        return new CodeInfo_FileDelete(filePath, noWarn, noRec);
                    }
                case CodeType.FileRename:
                case CodeType.FileMove:
                    { // FileRename,<SrcPath>,<DestPath>
                        const int argCount = 2;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [{type}] must have [{argCount}] arguments", rawCode);

                        return new CodeInfo_FileRename(args[0], args[1]);
                    }
                case CodeType.FileCreateBlank:
                    {
                        // Legacy: FileCreateBlank,<FilePath>,[UTF8|UTF16|UTF16BE|ANSI],[PRESERVE],[NOWARN]
                        // NEW   : FileCreateBlank,<FilePath>,[Encoding=<ENC>],[PRESERVE],[NOWARN]
                        const int minArgCount = 1;
                        const int maxArgCount = 4;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        string filePath = args[0];
                        bool preserve = false;
                        bool noWarn = false;
                        string encodingValue = null;

                        bool isDeprecated = false;
                        const string encodingKey = "Encoding=";
                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            string arg = args[i];
                            if (arg.Equals("PRESERVE", StringComparison.OrdinalIgnoreCase))
                            {
                                if (preserve)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                preserve = true;
                            }
                            else if (arg.Equals("NOWARN", StringComparison.OrdinalIgnoreCase))
                            {
                                if (noWarn)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                noWarn = true;
                            }
                            else if (arg.StartsWith(encodingKey, StringComparison.OrdinalIgnoreCase))
                            {
                                if (encodingValue != null)
                                    throw new InvalidCommandException($"Argument <{encodingKey}> cannot be duplicated", rawCode);
                                encodingValue = arg[encodingKey.Length..];
                            }
                            else if (arg.Equals("UTF8", StringComparison.OrdinalIgnoreCase))
                            {
                                if (encodingValue != null)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                isDeprecated = true;
                                encodingValue = "UTF8";
                            }
                            else if (arg.Equals("UTF16", StringComparison.OrdinalIgnoreCase) || arg.Equals("UTF16LE", StringComparison.OrdinalIgnoreCase))
                            {
                                if (encodingValue != null)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                isDeprecated = true;
                                encodingValue = "UTF16";
                            }
                            else if (arg.Equals("UTF16BE", StringComparison.OrdinalIgnoreCase))
                            {
                                if (encodingValue != null)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                isDeprecated = true;
                                encodingValue = "UTF16BE";
                            }
                            else if (arg.Equals("ANSI", StringComparison.OrdinalIgnoreCase))
                            {
                                isDeprecated = true;
                                encodingValue = "ANSI";
                            }
                            else
                            {
                                throw new InvalidCommandException($"Invalid optional argument or flag [{arg}]", rawCode);
                            }
                        }

                        return new CodeInfo_FileCreateBlank(filePath, preserve, noWarn, encodingValue, isDeprecated);
                    }
                case CodeType.FileSize:
                    { // FileSize,<FileName>,<DestVar>
                        const int argCount = 2;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [{type}] must have [{argCount}] arguments", rawCode);

                        if (Variables.DetectType(args[1]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[1]}] is not a valid variable name", rawCode);

                        return new CodeInfo_FileSize(args[0], args[1]);
                    }
                case CodeType.FileVersion:
                    { // FileVersion,<FileName>,<DestVar>
                        const int argCount = 2;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [{type}] must have [{argCount}] arguments", rawCode);

                        if (Variables.DetectType(args[1]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[1]}] is not a valid variable name", rawCode);

                        return new CodeInfo_FileVersion(args[0], args[1]);
                    }
                case CodeType.DirCopy:
                    { // DirCopy,<SrcFile>,<DestPath>
                        const int argCount = 2;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [{type}] must have [{argCount}] arguments", rawCode);

                        return new CodeInfo_DirCopy(args[0], args[1]);
                    }
                case CodeType.DirDelete:
                    { // DirDelete,<DirPath>
                        const int minArgCount = 1;
                        const int maxArgCount = 2; // For deprecated [FAST] argument
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        return new CodeInfo_DirDelete(args[0]);
                    }
                case CodeType.DirMove:
                    { // DirMove,<SrcDir>,<DestPath>
                        const int argCount = 2;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [{type}] must have [{argCount}] arguments", rawCode);

                        return new CodeInfo_DirMove(args[0], args[1]);
                    }
                case CodeType.DirMake:
                    { // DirMake,<DestDir>
                        const int argCount = 1;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [{type}] must have [{argCount}] arguments", rawCode);

                        return new CodeInfo_DirMake(args[0]);
                    }
                case CodeType.DirSize:
                    { // DirSize,<FileName>,<DestVar>
                        const int argCount = 2;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [{type}] must have [{argCount}] arguments", rawCode);

                        if (Variables.DetectType(args[1]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[1]}] is not a valid variable name", rawCode);

                        return new CodeInfo_DirSize(args[0], args[1]);
                    }
                case CodeType.PathMove:
                    { // PathMove,<SrcPath>,<DestPath>
                        const int argCount = 2;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [{type}] must have [{argCount}] arguments", rawCode);

                        return new CodeInfo_PathMove(args[0], args[1]);
                    }
                #endregion
                #region 02 Registry
                case CodeType.RegHiveLoad:
                    { // RegHiveLoad,<KeyPath>,<HiveFile>
                        const int argCount = 2;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [{type}] must have [{argCount}] arguments", rawCode);

                        return new CodeInfo_RegHiveLoad(args[0], args[1]);
                    }
                case CodeType.RegHiveUnload:
                    { // RegHiveUnload,<KeyPath>
                        const int argCount = 1;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [{type}] must have [{argCount}] arguments", rawCode);

                        return new CodeInfo_RegHiveUnload(args[0]);
                    }
                case CodeType.RegRead:
                    { // RegRead,<HKey>,<KeyPath>,<ValueName>,<DestVar>
                        const int argCount = 4;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [{type}] must have [{argCount}] arguments", rawCode);

                        RegistryKey hKey = RegistryHelper.ParseStringToRegKey(args[0]);

                        string destVar = args[3];
                        if (Variables.DetectType(destVar) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{destVar}] is not a valid variable name", rawCode);

                        return new CodeInfo_RegRead(hKey, args[1], args[2], destVar);
                    }
                case CodeType.RegWrite:
                case CodeType.RegWriteEx:
                    {
                        // RegWrite,<HKey>,<ValueType>,<KeyPath>,<ValueName>,<Empty | ValueData | ValueDataList>,[NOWARN]
                        // RegWriteEx,<HKey>,<ValueType>,<KeyPath>,<ValueName>,<Empty | ValueData | ValueDataList>,[NOWARN]

                        const int minArgCount = 3;
                        if (CheckInfoArgumentCount(args, minArgCount, -1))
                            throw new InvalidCommandException($"Command [{type}] must have at least [{minArgCount}] arguments", rawCode);

                        // Compatibility shim for Win10PESE : RegWrite,#5,#6,#7,#8,%_ML_T8_RegWriteBinaryBit%
                        // It will be parsed in RegWriteLegacy
                        RegistryKey hKey = RegistryHelper.ParseStringToRegKey(args[0]);
                        if (hKey == null)
                        {
                            type = CodeType.RegWriteLegacy;
                            goto case CodeType.RegWriteLegacy;
                        }

                        int cnt = args.Count;
                        bool noWarn = false;
                        if (args[cnt - 1].Equals("NOWARN", StringComparison.OrdinalIgnoreCase))
                        {
                            noWarn = true;
                            cnt -= 1;
                        }

                        // Parse RegistryValueKind
                        RegistryValueKind valueType;
                        if (!NumberHelper.ParseUInt32(args[1], out uint valueTypeInt))
                            throw new InvalidCommandException($"[{args[1]}] is not a valid number");
                        switch (valueTypeInt)
                        {
                            case 0:
                                valueType = RegistryValueKind.None;
                                break;
                            case 1:
                                valueType = RegistryValueKind.String;
                                break;
                            case 2:
                                valueType = RegistryValueKind.ExpandString;
                                break;
                            case 3:
                                valueType = RegistryValueKind.Binary;
                                break;
                            case 4:
                                valueType = RegistryValueKind.DWord;
                                break;
                            case 7:
                                valueType = RegistryValueKind.MultiString;
                                break;
                            case 11:
                                valueType = RegistryValueKind.QWord;
                                break;
                            default:
                                if (type == CodeType.RegWriteEx)
                                    valueType = RegistryValueKind.Unknown;
                                else
                                    throw new InvalidCommandException($"Invalid registry value type [0x{valueTypeInt:X}]");
                                break;
                        }

                        // Create CodeInfo_RegWrite instance
                        switch (valueType)
                        {
                            case RegistryValueKind.Unknown:
                                // RegWriteEx only, it bypass RegistryValueKind checking
                                // Data would be treated as binary
                                if (cnt == 4)
                                    return new CodeInfo_RegWrite(hKey, valueType, valueTypeInt, args[2], args[3], string.Empty, null, noWarn);
                                else if (5 == cnt)
                                    return new CodeInfo_RegWrite(hKey, valueType, valueTypeInt, args[2], args[3], args[4], null, noWarn);
                                else if (6 <= cnt)
                                {
                                    string[] valueDataList = args.Skip(4).Take(cnt - 4).ToArray();
                                    return new CodeInfo_RegWrite(hKey, valueType, valueTypeInt, args[2], args[3], null, valueDataList, noWarn);
                                }
                                break;
                            case RegistryValueKind.None:
                                // RegWrite,HKCU,0x0,Software\PEBakery
                                // RegWrite,HKCU,0x0,Software\PEBakery,Hello
                                switch (cnt)
                                {
                                    case 3:
                                        return new CodeInfo_RegWrite(hKey, valueType, valueTypeInt, args[2], null, null, null, noWarn);
                                    case 4:
                                        return new CodeInfo_RegWrite(hKey, valueType, valueTypeInt, args[2], args[3], null, null, noWarn);
                                }
                                break;
                            case RegistryValueKind.String:
                            case RegistryValueKind.ExpandString:
                                switch (cnt)
                                {
                                    case 3:
                                        return new CodeInfo_RegWrite(hKey, valueType, valueTypeInt, args[2], null, null, null, noWarn);
                                    case 4:
                                        return new CodeInfo_RegWrite(hKey, valueType, valueTypeInt, args[2], args[3], string.Empty, null, noWarn);
                                    case 5:
                                        return new CodeInfo_RegWrite(hKey, valueType, valueTypeInt, args[2], args[3], args[4], null, noWarn);
                                }
                                break;
                            case RegistryValueKind.MultiString:
                                if (4 == cnt)
                                { // RegWrite,HKLM,0x7,"Tmp_Software\PEBakery","Download Directories" 
                                    return new CodeInfo_RegWrite(hKey, valueType, valueTypeInt, args[2], args[3], null, Array.Empty<string>(), noWarn);
                                }
                                else if (5 <= cnt)
                                { // RegWrite,HKLM,0x7,"Tmp_Software\Microsoft\Windows NT\CurrentVersion\FontLink\SystemLink","Lucida Console","MALGUN.TTF,Malgun Gothic","GULIM.TTC,Gulim"
                                    string[] valueDataList = args.Skip(4).Take(cnt - 4).ToArray();
                                    if (valueDataList.Length == 1 && valueDataList[0].Equals(string.Empty, StringComparison.Ordinal))
                                        return new CodeInfo_RegWrite(hKey, valueType, valueTypeInt, args[2], args[3], null, Array.Empty<string>(), noWarn);
                                    else
                                        return new CodeInfo_RegWrite(hKey, valueType, valueTypeInt, args[2], args[3], null, valueDataList, noWarn);
                                }
                                break;
                            case RegistryValueKind.Binary:
                                if (cnt == 4)
                                    return new CodeInfo_RegWrite(hKey, valueType, valueTypeInt, args[2], args[3], string.Empty, null, noWarn);
                                else if (5 == cnt)
                                    return new CodeInfo_RegWrite(hKey, valueType, valueTypeInt, args[2], args[3], args[4], null, noWarn);
                                else if (6 <= cnt)
                                {
                                    string[] valueDataList = args.Skip(4).Take(cnt - 4).ToArray();
                                    return new CodeInfo_RegWrite(hKey, valueType, valueTypeInt, args[2], args[3], null, valueDataList, noWarn);
                                }
                                break;
                            case RegistryValueKind.DWord:
                            case RegistryValueKind.QWord:
                                if (cnt == 5)
                                    return new CodeInfo_RegWrite(hKey, valueType, valueTypeInt, args[2], args[3], args[4], null, noWarn);
                                break;
                            default:
                                throw new InvalidCommandException($"Invalid ValueType [{valueTypeInt}]", rawCode);
                        }

                        throw new InvalidCommandException("Invalid RegWrite Syntax", rawCode);
                    }
                case CodeType.RegWriteLegacy:
                    { // RegWrite,<HKey>,<ValueType>,<KeyPath>,<ValueName>,<Empty | ValueData | ValueDataList>
                        // Compatibility shim for Macro Library of Win10PESE
                        // Ex) RegWrite,#5,#6,#7,#8,%_ML_T8_RegWriteBinaryBit%
                        //     ValueType cannot be parsed as normal RegWrite in CodeParser.

                        // Check for compat option
                        if (!_opts.AllowLegacyRegWrite)
                            throw new InvalidCommandException("<HKey> must be constant string", rawCode);

                        const int minArgCount = 3;
                        if (CheckInfoArgumentCount(args, minArgCount, -1))
                            throw new InvalidCommandException($"Command [{type}] must have at least [{minArgCount}] arguments", rawCode);

                        int cnt = args.Count;
                        bool noWarn = false;
                        if (args[cnt - 1].Equals("NOWARN", StringComparison.OrdinalIgnoreCase))
                        {
                            noWarn = true;
                            cnt -= 1;
                        }

                        string valueName = null;
                        if (4 <= cnt)
                            valueName = args[3];

                        string[] valueDataList = null;
                        if (5 <= cnt)
                            valueDataList = args.Skip(4).Take(cnt - 4).ToArray();

                        return new CodeInfo_RegWriteLegacy(args[0], args[1], args[2], valueName, valueDataList, noWarn);
                    }
                case CodeType.RegDelete:
                    { // RegDelete,<HKey>,<KeyPath>,[ValueName]
                        const int minArgCount = 2;
                        const int maxArgCount = 3;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        RegistryKey hKey = RegistryHelper.ParseStringToRegKey(args[0]);
                        string keyPath = args[1];
                        string valueName = null;
                        if (args.Count == maxArgCount)
                            valueName = args[2];

                        return new CodeInfo_RegDelete(hKey, keyPath, valueName);
                    }
                case CodeType.RegMulti:
                    { // RegMulti,<HKey>,<KeyPath>,<ValueName>,<Type>,<Arg1>,[Arg2]
                        const int minArgCount = 5;
                        const int maxArgCount = 6;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        RegistryKey hKey = RegistryHelper.ParseStringToRegKey(args[0]);
                        string keyPath = args[1];
                        string valueName = args[2];

                        string valTypeStr = args[3];
                        RegMultiType valType;
                        try { valType = ParseRegMultiType(valTypeStr); }
                        catch (InvalidCommandException e) { throw new InvalidCommandException(e.Message, rawCode); }

                        string arg1 = args[4];
                        string arg2 = null;
                        if (args.Count == maxArgCount)
                            arg2 = args[5];

                        return new CodeInfo_RegMulti(hKey, keyPath, valueName, valType, arg1, arg2);
                    }
                case CodeType.RegImport:
                    { // RegImport,<RegFile>
                        const int argCount = 1;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [{type}] must have [{argCount}] arguments", rawCode);

                        return new CodeInfo_RegImport(args[0]);
                    }
                case CodeType.RegExport:
                    { // RegExport,<HKey>,<KeyPath>,<RegFile>
                        const int argCount = 3;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [{type}] must have [{argCount}] arguments", rawCode);

                        RegistryKey hKey = RegistryHelper.ParseStringToRegKey(args[0]);

                        return new CodeInfo_RegExport(hKey, args[1], args[2]);
                    }
                case CodeType.RegCopy:
                    { // RegCopy,<SrcKey>,<SrcKeyPath>,<DestKey>,<DestKeyPath>,[WILDCARD]
                        const int minArgCount = 4;
                        const int maxArgCount = 5;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        RegistryKey hSrcKey = RegistryHelper.ParseStringToRegKey(args[0]);
                        RegistryKey hDestKey = RegistryHelper.ParseStringToRegKey(args[2]);

                        bool wildcard = false;
                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            string arg = args[i];
                            if (arg.Equals("WILDCARD", StringComparison.OrdinalIgnoreCase))
                            {
                                if (wildcard)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                wildcard = true;
                            }
                            else
                            {
                                throw new InvalidCommandException($"Invalid optional argument or flag [{arg}]", rawCode);
                            }
                        }

                        return new CodeInfo_RegCopy(hSrcKey, args[1], hDestKey, args[3], wildcard);
                    }
                #endregion
                #region 03 Text
                case CodeType.TXTAddLine:
                    { // TXTAddLine,<FileName>,<Line>,<Mode>
                        const int argCount = 3;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [{type}] must have [{argCount}] arguments", rawCode);


                        string fileName = args[0];
                        string line = args[1];
                        string mode;
                        if (args[2].Equals("Prepend", StringComparison.OrdinalIgnoreCase) ||
                            args[2].Equals("Append", StringComparison.OrdinalIgnoreCase) ||
                            Variables.DetectType(args[1]) != Variables.VarKeyType.None)
                            mode = args[2];
                        else
                            throw new InvalidCommandException("Mode must be one of Prepend, Append, or variable.", rawCode);

                        return new CodeInfo_TXTAddLine(fileName, line, mode);
                    }
                case CodeType.TXTReplace:
                    { // TXTReplace,<FileName>,<ToBeReplaced>,<ReplaceWith>
                        const int argCount = 3;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [{type}] must have [{argCount}] arguments", rawCode);

                        return new CodeInfo_TXTReplace(args[0], args[1], args[2]);
                    }
                case CodeType.TXTDelLine:
                    { // TXTDelLine,<FileName>,<DeleteIfBeginWith>
                        const int argCount = 2;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [{type}] must have [{argCount}] arguments", rawCode);

                        if (args[1].Contains("#$x"))
                            throw new InvalidCommandException("Keyword cannot include line feed", rawCode);

                        return new CodeInfo_TXTDelLine(args[0], args[1]);
                    }
                case CodeType.TXTDelSpaces:
                    { // TXTDelSpaces,<FileName>
                        const int argCount = 1;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [{type}] must have [{argCount}] arguments", rawCode);

                        return new CodeInfo_TXTDelSpaces(args[0]);
                    }
                case CodeType.TXTDelEmptyLines:
                    { // TXTDelEmptyLines,<FileName>
                        const int argCount = 1;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [{type}] must have [{argCount}] arguments", rawCode);

                        return new CodeInfo_TXTDelEmptyLines(args[0]);
                    }
                #endregion
                #region 04 Ini
                case CodeType.IniRead:
                    { // INIRead,<FileName>,<Section>,<Key>,<DestVar>[,<Default=[Value]>]
                        const int minArgCount = 4;
                        const int maxArgCount = 5;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        string destVar = args[3];
                        if (Variables.DetectType(destVar) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{destVar}] is not a valid variable name", rawCode);

                        string defaultValue = null;
                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            string arg = args[i];

                            const string splitKey = "Default=";
                            if (arg.StartsWith(splitKey, StringComparison.OrdinalIgnoreCase))
                            {
                                if (defaultValue != null)
                                    throw new InvalidCommandException("Argument <Default> cannot be duplicated", rawCode);
                                defaultValue = arg[splitKey.Length..];
                            }
                            else
                            {
                                throw new InvalidCommandException($"Invalid optional argument [{arg}]", rawCode);
                            }
                        }

                        return new CodeInfo_IniRead(args[0], args[1], args[2], destVar, defaultValue);
                    }
                case CodeType.IniWrite:
                    { // INIWrite,<FileName>,<Section>,<Key>,<Value>
                        const int argCount = 4;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [{type}] must have [{argCount}] arguments", rawCode);

                        return new CodeInfo_IniWrite(args[0], args[1], args[2], args[3]);
                    }
                case CodeType.IniDelete:
                    { // INIDelete,<FileName>,<Section>,<Key>
                        const int argCount = 3;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [{type}] must have [{argCount}] arguments", rawCode);

                        return new CodeInfo_IniDelete(args[0], args[1], args[2]);
                    }
                case CodeType.IniReadSection:
                    { // INIReadSection,<FileName>,<Section>,<DestVar>
                        const int minArgCount = 3;
                        const int maxArgCount = 4;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        string destVar = args[2];
                        if (Variables.DetectType(destVar) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{destVar}] is not a valid variable name", rawCode);

                        string delim = null;
                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            string arg = args[i];

                            const string delimKey = "Delim=";
                            if (arg.StartsWith(delimKey, StringComparison.OrdinalIgnoreCase))
                            {
                                if (delim != null)
                                    throw new InvalidCommandException("Argument <Delim> cannot be duplicated", rawCode);
                                delim = arg[delimKey.Length..];
                            }
                            else
                            {
                                throw new InvalidCommandException($"Invalid optional argument or flag [{arg}]", rawCode);
                            }
                        }

                        return new CodeInfo_IniReadSection(args[0], args[1], args[2], delim);
                    }
                case CodeType.IniAddSection:
                    { // INIAddSection,<FileName>,<Section>
                        const int argCount = 2;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [{type}] must have [{argCount}] arguments", rawCode);

                        return new CodeInfo_IniAddSection(args[0], args[1]);
                    }
                case CodeType.IniDeleteSection:
                    { // INIDeleteSection,<FileName>,<Section>
                        const int argCount = 2;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [{type}] must have [{argCount}] arguments", rawCode);

                        return new CodeInfo_IniDeleteSection(args[0], args[1]);
                    }
                case CodeType.IniWriteTextLine:
                    {  // IniWriteTextLine,<FileName>,<Section>,<Line>,[APPEND] 
                        const int minArgCount = 3;
                        const int maxArgCount = 4;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        bool append = false;
                        if (maxArgCount == args.Count)
                        {
                            if (args[3].Equals("APPEND", StringComparison.OrdinalIgnoreCase))
                                append = true;
                            else
                                throw new InvalidCommandException($"Wrong argument [{args[3]}]", rawCode);
                        }

                        return new CodeInfo_IniWriteTextLine(args[0], args[1], args[2], append);
                    }
                case CodeType.IniMerge:
                    { // INIMerge,<SrcFile>,<DestFile>
                        const int argCount = 2;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [{type}] must have [{argCount}] arguments", rawCode);

                        return new CodeInfo_IniMerge(args[0], args[1]);
                    }
                case CodeType.IniCompact:
                    { // IniCompact,<FilePath>
                        const int argCount = 1;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [{type}] must have [{argCount}] arguments", rawCode);

                        return new CodeInfo_IniCompact(args[0]);
                    }
                #endregion
                #region 05 Wim
                case CodeType.WimMount:
                    { // WimMount,<SrcWim>,<ImageIndex>,<MountDir>,<READONLY|READWRITE>
                        const int argCount = 4;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [{type}] must have [{argCount}] arguments", rawCode);

                        return new CodeInfo_WimMount(args[0], args[1], args[2], args[3]);
                    }
                case CodeType.WimUnmount:
                    { // WimUnmount,<MountDir>,<DISCARD|COMMIT>
                        const int argCount = 2;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [{type}] must have [{argCount}] arguments", rawCode);

                        return new CodeInfo_WimUnmount(args[0], args[1]);
                    }
                case CodeType.WimInfo:
                    { // WimInfo,<SrcWim>,<ImageIndex>,<Key>,<DestVar>,[NOERR]
                        const int minArgCount = 4;
                        const int maxArgCount = 5;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        // Check DestVar
                        string destVar = args[3];
                        if (Variables.DetectType(destVar) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{destVar}] is not a valid variable name", rawCode);

                        bool noErr = false;
                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            string arg = args[i];

                            if (arg.Equals("NOERR", StringComparison.OrdinalIgnoreCase))
                            {
                                if (noErr)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                noErr = true;
                            }
                            else
                            {
                                throw new InvalidCommandException($"Invalid optional argument or flag [{arg}]", rawCode);
                            }
                        }

                        return new CodeInfo_WimInfo(args[0], args[1], args[2], destVar, noErr);
                    }
                case CodeType.WimApply:
                    { // WimApply,<SrcWim>,<ImageIndex>,<DestDir>,[Split=STR],[CHECK],[NOACL],[NOATTRIB]
                        const int minArgCount = 3;
                        const int maxArgCount = 7;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        string split = null;
                        bool check = false;
                        bool noAcl = false;
                        bool noAttrib = false;

                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            string arg = args[i];

                            const string splitKey = "Split=";
                            if (arg.StartsWith(splitKey, StringComparison.OrdinalIgnoreCase))
                            {
                                if (split != null)
                                    throw new InvalidCommandException("Argument <Split> cannot be duplicated", rawCode);
                                split = arg[splitKey.Length..];
                            }
                            else if (arg.Equals("CHECK", StringComparison.OrdinalIgnoreCase))
                            {
                                if (check)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                check = true;
                            }
                            else if (arg.Equals("NOACL", StringComparison.OrdinalIgnoreCase))
                            {
                                if (noAcl)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                noAcl = true;
                            }
                            else if (arg.Equals("NOATTRIB", StringComparison.OrdinalIgnoreCase))
                            {
                                if (noAttrib)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                noAttrib = true;
                            }
                            else
                                throw new InvalidCommandException($"Invalid optional argument or flag [{arg}]", rawCode);
                        }

                        return new CodeInfo_WimApply(args[0], args[1], args[2], split, check, noAcl, noAttrib);
                    }
                case CodeType.WimExtract:
                    { // WimExtract,<SrcWim>,<ImageIndex>,<ExtractPath>,<DestDir>,[Split=],[CHECK],[NOACL],[NOATTRIB]
                        const int minArgCount = 4;
                        const int maxArgCount = 7;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        string split = null;
                        bool check = false;
                        bool noAcl = false;
                        bool noAttrib = false;

                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            string arg = args[i];

                            const string splitKey = "Split=";
                            if (arg.StartsWith(splitKey, StringComparison.OrdinalIgnoreCase))
                            {
                                if (split != null)
                                    throw new InvalidCommandException("Argument <Split> cannot be duplicated", rawCode);
                                split = arg[splitKey.Length..];
                            }
                            else if (arg.Equals("CHECK", StringComparison.OrdinalIgnoreCase))
                            {
                                if (check)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                check = true;
                            }
                            else if (arg.Equals("NOACL", StringComparison.OrdinalIgnoreCase))
                            {
                                if (noAcl)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                noAcl = true;
                            }
                            else if (arg.Equals("NOATTRIB", StringComparison.OrdinalIgnoreCase))
                            {
                                if (noAttrib)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                noAttrib = true;
                            }
                            else
                            {
                                throw new InvalidCommandException($"Invalid optional argument or flag [{arg}]", rawCode);
                            }
                        }

                        return new CodeInfo_WimExtract(args[0], args[1], args[2], args[3], split, check, noAcl, noAttrib);
                    }
                case CodeType.WimExtractBulk:
                    { // WimExtractBulk,<SrcWim>,<ImageIndex>,<ListFile>,<DestDir>,[Split=],[CHECK],[NOACL],[NOATTRIB],[NOERR],[NOWARN]
                        const int minArgCount = 4;
                        const int maxArgCount = 10;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        string split = null;
                        bool check = false;
                        bool noAcl = false;
                        bool noAttrib = false;
                        bool noErr = false;
                        bool noWarn = false;
                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            string arg = args[i];

                            const string splitKey = "Split=";
                            if (arg.StartsWith(splitKey, StringComparison.OrdinalIgnoreCase))
                            {
                                if (split != null)
                                    throw new InvalidCommandException("Argument <Split> cannot be duplicated", rawCode);
                                split = arg[splitKey.Length..];
                            }
                            else if (arg.Equals("CHECK", StringComparison.OrdinalIgnoreCase))
                            {
                                if (check)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                check = true;
                            }
                            else if (arg.Equals("NOACL", StringComparison.OrdinalIgnoreCase))
                            {
                                if (noAcl)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                noAcl = true;
                            }
                            else if (arg.Equals("NOATTRIB", StringComparison.OrdinalIgnoreCase))
                            {
                                if (noAttrib)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                noAttrib = true;
                            }
                            else if (arg.Equals("NOERR", StringComparison.OrdinalIgnoreCase))
                            {
                                if (noErr)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                noErr = true;
                            }
                            else if (arg.Equals("NOWARN", StringComparison.OrdinalIgnoreCase))
                            {
                                if (noWarn)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                noWarn = true;
                            }
                            else
                            {
                                throw new InvalidCommandException($"Invalid optional argument or flag [{arg}]", rawCode);
                            }
                        }

                        return new CodeInfo_WimExtractBulk(args[0], args[1], args[2], args[3], split, check, noAcl, noAttrib, noErr, noWarn);
                    }
                case CodeType.WimCapture:
                    { // WimCapture,<SrcDir>,<DestWim>,<Compress>,[IMAGENAME=STR],[IMAGEDESC=STR],[FLAGS=STR],[BOOT],[CHECK],[NOACL]
                        const int minArgCount = 3;
                        const int maxArgCount = 9;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        string imageName = null;
                        string imageDesc = null;
                        string wimFlags = null;
                        bool boot = false;
                        bool check = false;
                        bool noAcl = false;

                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            const string imageNameKey = "ImageName=";
                            const string imageDescKey = "ImageDesc=";
                            const string wimFlagsKey = "Flags=";

                            string arg = args[i];
                            if (arg.StartsWith(imageNameKey, StringComparison.OrdinalIgnoreCase))
                            {
                                if (imageName != null)
                                    throw new InvalidCommandException("Argument <ImageName> cannot be duplicated", rawCode);
                                imageName = arg[imageNameKey.Length..];
                            }
                            else if (arg.StartsWith(imageDescKey, StringComparison.OrdinalIgnoreCase))
                            {
                                if (imageDesc != null)
                                    throw new InvalidCommandException("Argument <ImageDesc> cannot be duplicated", rawCode);
                                imageDesc = arg[imageDescKey.Length..];
                            }
                            else if (arg.StartsWith(wimFlagsKey, StringComparison.OrdinalIgnoreCase))
                            {
                                if (wimFlags != null)
                                    throw new InvalidCommandException("Argument <Flags> cannot be duplicated", rawCode);
                                wimFlags = arg[wimFlagsKey.Length..];
                            }
                            else if (arg.Equals("BOOT", StringComparison.OrdinalIgnoreCase))
                            {
                                if (boot)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                boot = true;
                            }
                            else if (arg.Equals("CHECK", StringComparison.OrdinalIgnoreCase))
                            {
                                if (check)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                check = true;
                            }
                            else if (arg.Equals("NOACL", StringComparison.OrdinalIgnoreCase))
                            {
                                if (noAcl)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                noAcl = true;
                            }
                            else
                                throw new InvalidCommandException($"Invalid optional argument or flag [{arg}]", rawCode);
                        }

                        return new CodeInfo_WimCapture(args[0], args[1], args[2], imageName, imageDesc, wimFlags, boot, check, noAcl);
                    }
                case CodeType.WimAppend:
                    { // WimAppend,<SrcDir>,<DestWim>,[ImageName=STR],[ImageDesc=STR],[Flags=STR],[DeltaIndex=INT],[BOOT],[CHECK],[NOACL]
                        const int minArgCount = 2;
                        const int maxArgCount = 9;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        string imageName = null;
                        string imageDesc = null;
                        string wimFlags = null;
                        string deltaFrom = null;
                        bool boot = false;
                        bool check = false;
                        bool noAcl = false;

                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            const string imageNameKey = "ImageName=";
                            const string imageDescKey = "ImageDesc=";
                            const string wimFlagsKey = "Flags=";
                            const string deltaIndexKey = "DeltaIndex=";

                            string arg = args[i];
                            if (arg.StartsWith(imageNameKey, StringComparison.OrdinalIgnoreCase))
                            {
                                if (imageName != null)
                                    throw new InvalidCommandException("Argument <ImageName> cannot be duplicated", rawCode);
                                imageName = arg[imageNameKey.Length..];
                            }
                            else if (arg.StartsWith(imageDescKey, StringComparison.OrdinalIgnoreCase))
                            {
                                if (imageDesc != null)
                                    throw new InvalidCommandException("Argument <ImageDesc> cannot be duplicated", rawCode);
                                imageDesc = arg[imageDescKey.Length..];
                            }
                            else if (arg.StartsWith(wimFlagsKey, StringComparison.OrdinalIgnoreCase))
                            {
                                if (wimFlags != null)
                                    throw new InvalidCommandException("Argument <Flags> cannot be duplicated", rawCode);
                                wimFlags = arg[wimFlagsKey.Length..];
                            }
                            else if (arg.StartsWith(deltaIndexKey, StringComparison.OrdinalIgnoreCase))
                            {
                                if (deltaFrom != null)
                                    throw new InvalidCommandException("Argument <DeltaFrom> cannot be duplicated", rawCode);
                                deltaFrom = arg[deltaIndexKey.Length..];
                            }
                            else if (arg.Equals("BOOT", StringComparison.OrdinalIgnoreCase))
                            {
                                if (boot)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                boot = true;
                            }
                            else if (arg.Equals("CHECK", StringComparison.OrdinalIgnoreCase))
                            {
                                if (check)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                check = true;
                            }
                            else if (arg.Equals("NOACL", StringComparison.OrdinalIgnoreCase))
                            {
                                if (noAcl)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                noAcl = true;
                            }
                            else
                                throw new InvalidCommandException($"Invalid optional argument or flag [{arg}]", rawCode);
                        }

                        return new CodeInfo_WimAppend(args[0], args[1], imageName, imageDesc, wimFlags, deltaFrom, boot, check, noAcl);
                    }
                case CodeType.WimDelete:
                    { // WimDelete,<SrcWim>,<ImageIndex>,[CHECK]
                        const int minArgCount = 2;
                        const int maxArgCount = 3;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        bool check = false;

                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            string arg = args[i];

                            if (arg.Equals("CHECK", StringComparison.OrdinalIgnoreCase))
                            {
                                if (check)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                check = true;
                            }
                            else
                            {
                                throw new InvalidCommandException($"Invalid optional argument or flag [{arg}]", rawCode);
                            }
                        }

                        return new CodeInfo_WimDelete(args[0], args[1], check);
                    }
                case CodeType.WimPathAdd:
                    { // WimPathAdd,<WimFile>,<ImageIndex>,<SrcPath>,<DestPath>,[CHECK],[NOACL],[PRESERVE],[REBUILD]
                        const int minArgCount = 4;
                        const int maxArgCount = 8;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        bool check = false;
                        bool noAcl = false;
                        bool preserve = false;
                        bool rebuild = false;

                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            string arg = args[i];
                            if (arg.Equals("CHECK", StringComparison.OrdinalIgnoreCase))
                            {
                                if (check)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                check = true;
                            }
                            else if (arg.Equals("NOACL", StringComparison.OrdinalIgnoreCase))
                            {
                                if (noAcl)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                noAcl = true;
                            }
                            else if (arg.Equals("PRESERVE", StringComparison.OrdinalIgnoreCase))
                            {
                                if (preserve)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                preserve = true;
                            }
                            else if (arg.Equals("REBUILD", StringComparison.OrdinalIgnoreCase))
                            {
                                if (rebuild)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                rebuild = true;
                            }
                            else
                            {
                                throw new InvalidCommandException($"Invalid optional argument or flag [{arg}]", rawCode);
                            }
                        }

                        return new CodeInfo_WimPathAdd(args[0], args[1], args[2], args[3], check, noAcl, preserve, rebuild);
                    }
                case CodeType.WimPathDelete:
                    { // WimPathDelete,<WimFile>,<ImageIndex>,<Path>,[CHECK],[REBUILD]
                        const int minArgCount = 3;
                        const int maxArgCount = 5;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        bool check = false;
                        bool rebuild = false;

                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            string arg = args[i];
                            if (arg.Equals("CHECK", StringComparison.OrdinalIgnoreCase))
                            {
                                if (check)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                check = true;
                            }
                            else if (arg.Equals("REBUILD", StringComparison.OrdinalIgnoreCase))
                            {
                                if (rebuild)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                rebuild = true;
                            }
                            else
                            {
                                throw new InvalidCommandException($"Invalid optional argument or flag [{arg}]", rawCode);
                            }
                        }

                        return new CodeInfo_WimPathDelete(args[0], args[1], args[2], check, rebuild);
                    }
                case CodeType.WimPathRename:
                    { // WimPathRename,<WimFile>,<ImageIndex>,<SrcPath>,<DestPath>,[CHECK],[REBUILD]
                        const int minArgCount = 4;
                        const int maxArgCount = 6;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        bool check = false;
                        bool rebuild = false;

                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            string arg = args[i];
                            if (arg.Equals("CHECK", StringComparison.OrdinalIgnoreCase))
                            {
                                if (check)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                check = true;
                            }
                            else if (arg.Equals("REBUILD", StringComparison.OrdinalIgnoreCase))
                            {
                                if (rebuild)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                rebuild = true;
                            }
                            else
                            {
                                throw new InvalidCommandException($"Invalid optional argument or flag [{arg}]", rawCode);
                            }
                        }

                        return new CodeInfo_WimPathRename(args[0], args[1], args[2], args[3], check, rebuild);
                    }
                case CodeType.WimOptimize:
                    { // WimOptimize,<WimFile>,[Recomp=STR],[CHECK|NOCHECK]
                        const int minArgCount = 1;
                        const int maxArgCount = 3;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        string recompress = null;
                        bool? check = null;

                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            const string recompKey = "Recomp=";

                            string arg = args[i];
                            if (arg.StartsWith(recompKey, StringComparison.OrdinalIgnoreCase))
                            {
                                if (recompress != null)
                                    throw new InvalidCommandException("Argument <Recomp> cannot be duplicated", rawCode);
                                recompress = arg[recompKey.Length..];
                            }
                            else if (arg.Equals("CHECK", StringComparison.OrdinalIgnoreCase))
                            {
                                if (check != null)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                check = true;
                            }
                            else if (arg.Equals("NOCHECK", StringComparison.OrdinalIgnoreCase))
                            {
                                if (check != null)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                check = false;
                            }
                            else
                                throw new InvalidCommandException($"Invalid optional argument or flag [{arg}]", rawCode);
                        }

                        return new CodeInfo_WimOptimize(args[0], recompress, check);
                    }
                case CodeType.WimExport:
                    { // WimExport,<SrcWim>,<ImageIndex>,<DestWim>,[ImageName=STR],[ImageDesc=STR],[Split=STR],[Recomp=STR],[BOOT],[CHECK|NOCHECK]
                        const int minArgCount = 3;
                        const int maxArgCount = 9;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        string imageName = null;
                        string imageDesc = null;
                        string split = null;
                        string recompress = null;
                        bool boot = false;
                        bool? check = null;

                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            const string imageNameKey = "ImageName=";
                            const string imageDescKey = "ImageDesc=";
                            const string splitKey = "Split=";
                            const string recompKey = "Recomp=";

                            string arg = args[i];
                            if (arg.StartsWith(imageNameKey, StringComparison.OrdinalIgnoreCase))
                            {
                                if (imageName != null)
                                    throw new InvalidCommandException("Argument <ImageName> cannot be duplicated", rawCode);
                                imageName = arg[imageNameKey.Length..];
                            }
                            else if (arg.StartsWith(imageDescKey, StringComparison.OrdinalIgnoreCase))
                            {
                                if (imageDesc != null)
                                    throw new InvalidCommandException("Argument <ImageDesc> cannot be duplicated", rawCode);
                                imageDesc = arg[imageDescKey.Length..];
                            }
                            else if (arg.StartsWith(splitKey, StringComparison.OrdinalIgnoreCase))
                            {
                                if (split != null)
                                    throw new InvalidCommandException("Argument <Split> cannot be duplicated", rawCode);
                                split = arg[splitKey.Length..];
                            }
                            else if (arg.StartsWith(recompKey, StringComparison.OrdinalIgnoreCase))
                            {
                                if (recompress != null)
                                    throw new InvalidCommandException("Argument <Recomp> cannot be duplicated", rawCode);
                                recompress = arg[recompKey.Length..];
                            }
                            else if (arg.Equals("BOOT", StringComparison.OrdinalIgnoreCase))
                            {
                                if (boot)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                boot = true;
                            }
                            else if (arg.Equals("CHECK", StringComparison.OrdinalIgnoreCase))
                            {
                                if (check != null)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                check = true;
                            }
                            else if (arg.Equals("NOCHECK", StringComparison.OrdinalIgnoreCase))
                            {
                                if (check != null)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                check = false;
                            }
                            else
                                throw new InvalidCommandException($"Invalid optional argument or flag [{arg}]", rawCode);
                        }

                        return new CodeInfo_WimExport(args[0], args[1], args[2], imageName, imageDesc, split, recompress, boot, check);
                    }
                #endregion
                #region 06 Archive
                case CodeType.Compress:
                    { // Compress,<Format>,<SrcPath>,<DestArchive>[,CompressLevel]
                        const int minArgCount = 3;
                        const int maxArgCount = 4;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        ArchiveFile.ArchiveCompressFormat format;
                        string formatStr = args[0];
                        if (formatStr.Equals("Zip", StringComparison.OrdinalIgnoreCase))
                            format = ArchiveFile.ArchiveCompressFormat.Zip;
                        else if (formatStr.Equals("7z", StringComparison.OrdinalIgnoreCase))
                            format = ArchiveFile.ArchiveCompressFormat.SevenZip;
                        else
                            throw new InvalidCommandException($"Cannot compress to [{formatStr}] file format", rawCode);

                        ArchiveFile.CompressLevel? compLevel = null;
                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            string arg = args[i];
                            if (arg.Equals("STORE", StringComparison.OrdinalIgnoreCase))
                            {
                                if (compLevel != null)
                                    throw new InvalidCommandException("CompressLevel cannot be duplicated", rawCode);
                                compLevel = ArchiveFile.CompressLevel.Store;
                            }
                            else if (arg.Equals("FASTEST", StringComparison.OrdinalIgnoreCase))
                            {
                                if (compLevel != null)
                                    throw new InvalidCommandException("CompressLevel cannot be duplicated", rawCode);
                                compLevel = ArchiveFile.CompressLevel.Fastest;
                            }
                            else if (arg.Equals("NORMAL", StringComparison.OrdinalIgnoreCase))
                            {
                                if (compLevel != null)
                                    throw new InvalidCommandException("CompressLevel cannot be duplicated", rawCode);
                                compLevel = ArchiveFile.CompressLevel.Normal;
                            }
                            else if (arg.Equals("BEST", StringComparison.OrdinalIgnoreCase))
                            {
                                if (compLevel != null)
                                    throw new InvalidCommandException("CompressLevel cannot be duplicated", rawCode);
                                compLevel = ArchiveFile.CompressLevel.Best;
                            }
                            else
                            {
                                throw new InvalidCommandException($"Invalid optional argument or flag [{arg}]", rawCode);
                            }
                        }

                        return new CodeInfo_Compress(format, args[1], args[2], compLevel);
                    }
                case CodeType.Decompress:
                    { // Decompress,<SrcArchive>,<DestDir>[,Password=<Str>]
                        const int minArgCount = 2;
                        const int maxArgCount = 3;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        string password = null;
                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            string arg = args[i];

                            const string passwordKey = "Password=";
                            if (arg.StartsWith(passwordKey, StringComparison.OrdinalIgnoreCase))
                            {
                                if (password != null)
                                    throw new InvalidCommandException("Argument <Password> cannot be duplicated", rawCode);
                                password = arg[passwordKey.Length..];
                            }
                            else
                            {
                                throw new InvalidCommandException($"Invalid optional argument [{arg}]", rawCode);
                            }
                        }

                        return new CodeInfo_Decompress(args[0], args[1], password);
                    }
                case CodeType.Expand:
                    { // Expand,<SrcCab>,<DestDir>,[SingleFile],[PRESERVE],[NOWARN]
                        const int minArgCount = 2;
                        const int maxArgCount = 5;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        string srcCab = args[0];
                        string destDir = args[1];
                        string singleFile = null;
                        bool preserve = false;
                        bool noWarn = false;

                        if (3 <= args.Count)
                            singleFile = args[2];

                        for (int i = 3; i < args.Count; i++)
                        {
                            string arg = args[i];
                            if (arg.Equals("PRESERVE", StringComparison.OrdinalIgnoreCase))
                            {
                                if (preserve)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                preserve = true;
                            }
                            else if (arg.Equals("NOWARN", StringComparison.OrdinalIgnoreCase))
                            {
                                if (noWarn)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                noWarn = true;
                            }
                            else
                                throw new InvalidCommandException($"Invalid optional argument or flag [{arg}]", rawCode);
                        }

                        return new CodeInfo_Expand(srcCab, destDir, singleFile, preserve, noWarn);
                    }
                case CodeType.CopyOrExpand:
                    { // CopyOrExpand,<SrcFile>,<DestPath>,[PRESERVE],[NOWARN]
                        const int minArgCount = 2;
                        const int maxArgCount = 4;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        string srcFile = args[0];
                        string destPath = args[1];
                        bool preserve = false;
                        bool noWarn = false;

                        for (int i = 2; i < args.Count; i++)
                        {
                            string arg = args[i];
                            if (arg.Equals("PRESERVE", StringComparison.OrdinalIgnoreCase))
                            {
                                if (preserve)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                preserve = true;
                            }
                            else if (arg.Equals("NOWARN", StringComparison.OrdinalIgnoreCase))
                            {
                                if (noWarn)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                noWarn = true;
                            }
                            else
                                throw new InvalidCommandException($"Invalid optional argument or flag [{arg}]", rawCode);
                        }

                        return new CodeInfo_CopyOrExpand(srcFile, destPath, preserve, noWarn);
                    }
                #endregion
                #region 07 Network
                case CodeType.WebGet:
                case CodeType.WebGetIfNotExist: // Will be deprecated
                    { // WebGet,<URL>,<DestPath>[,<HashType>=<HashDigest>][,TimeOut=<Int>][,Referer=<URL>][,NOERR]
                        const int minArgCount = 2;
                        const int maxArgCount = 5;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        HashHelper.HashType hashType = HashHelper.HashType.None;
                        string hashDigest = null;
                        string timeOut = null;
                        string referer = null;
                        string userAgent = null;
                        bool noErr = false;

                        const string md5Key = "MD5=";
                        const string sha1Key = "SHA1=";
                        const string sha256Key = "SHA256=";
                        const string sha384Key = "SHA384=";
                        const string sha512Key = "SHA512=";
                        const string timeOutKey = "TimeOut=";
                        const string refererKey = "Referer=";
                        const string userAgentKey = "UserAgent=";
                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            string arg = args[i];
                            if (arg.StartsWith(md5Key, StringComparison.OrdinalIgnoreCase))
                            {
                                if (hashType != HashHelper.HashType.None || hashDigest != null)
                                    throw new InvalidCommandException("Argument <MD5> cannot be duplicated", rawCode);
                                hashType = HashHelper.HashType.MD5;
                                hashDigest = arg[md5Key.Length..];
                            }
                            else if (arg.StartsWith(sha1Key, StringComparison.OrdinalIgnoreCase))
                            {
                                if (hashType != HashHelper.HashType.None || hashDigest != null)
                                    throw new InvalidCommandException("Argument <SHA1> cannot be duplicated", rawCode);
                                hashType = HashHelper.HashType.SHA1;
                                hashDigest = arg[sha1Key.Length..];
                            }
                            else if (arg.StartsWith(sha256Key, StringComparison.OrdinalIgnoreCase))
                            {
                                if (hashType != HashHelper.HashType.None || hashDigest != null)
                                    throw new InvalidCommandException("Argument <SHA256> cannot be duplicated", rawCode);
                                hashType = HashHelper.HashType.SHA256;
                                hashDigest = arg[sha256Key.Length..];
                            }
                            else if (arg.StartsWith(sha384Key, StringComparison.OrdinalIgnoreCase))
                            {
                                if (hashType != HashHelper.HashType.None || hashDigest != null)
                                    throw new InvalidCommandException("Argument <SHA384> cannot be duplicated", rawCode);
                                hashType = HashHelper.HashType.SHA384;
                                hashDigest = arg[sha384Key.Length..];
                            }
                            else if (arg.StartsWith(sha512Key, StringComparison.OrdinalIgnoreCase))
                            {
                                if (hashType != HashHelper.HashType.None || hashDigest != null)
                                    throw new InvalidCommandException("Argument <SHA512> cannot be duplicated", rawCode);
                                hashType = HashHelper.HashType.SHA512;
                                hashDigest = arg[sha512Key.Length..];
                            }
                            else if (arg.StartsWith(timeOutKey, StringComparison.OrdinalIgnoreCase))
                            {
                                if (timeOut != null)
                                    throw new InvalidCommandException("Argument <TimeOut> cannot be duplicated", rawCode);
                                timeOut = arg[timeOutKey.Length..];
                            }
                            else if (arg.StartsWith(refererKey, StringComparison.OrdinalIgnoreCase))
                            {
                                if (referer != null)
                                    throw new InvalidCommandException("Argument <Referer> cannot be duplicated", rawCode);
                                referer = arg[refererKey.Length..];
                            }
                            else if (arg.StartsWith(userAgentKey, StringComparison.OrdinalIgnoreCase))
                            {
                                if (referer != null)
                                    throw new InvalidCommandException("Argument <UserAgent> cannot be duplicated", rawCode);
                                userAgent = arg[userAgentKey.Length..];
                            }
                            else if (arg.Equals("NOERR", StringComparison.OrdinalIgnoreCase))
                            {
                                if (noErr)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                noErr = true;
                            }
                            else
                            {
                                throw new InvalidCommandException($"Invalid optional argument or flag [{arg}]", rawCode);
                            }
                        }

                        return new CodeInfo_WebGet(args[0], args[1], hashType, hashDigest, timeOut, referer, userAgent, noErr);
                    }
                #endregion
                #region 08 Hash
                case CodeType.Hash:
                    { // Hash,<HashHelper.HashType>,<FilePath>,<DestVar>
                        const int argCount = 3;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [{type}] must have [{argCount}] arguments", rawCode);

                        if (Variables.DetectType(args[2]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[2]}] is not valid variable name", rawCode);

                        return new CodeInfo_Hash(args[0], args[1], args[2]);
                    }
                #endregion
                #region 09 Script
                case CodeType.ExtractFile:
                    { // ExtractFile,%ScriptFile%,<DirName>,<FileName>,<ExtractTo>
                        const int argCount = 4;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [{type}] must have [{argCount}] arguments", rawCode);

                        return new CodeInfo_ExtractFile(args[0], args[1], args[2], args[3]);
                    }
                case CodeType.ExtractAndRun:
                    { // ExtractAndRun,%ScriptFile%,<DirName>,<FileName>,[Params]
                        const int minArgCount = 3;
                        const int maxArgCount = 4;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        string _params = null;
                        if (4 <= args.Count)
                            _params = args[3];

                        return new CodeInfo_ExtractAndRun(args[0], args[1], args[2], _params);
                    }
                case CodeType.ExtractAllFiles:
                    { // ExtractAllFiles,%ScriptFile%,<DirName>,<ExtractTo>
                        const int argCount = 3;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [{type}] must have [{argCount}] arguments", rawCode);

                        return new CodeInfo_ExtractAllFiles(args[0], args[1], args[2]);
                    }
                case CodeType.Encode:
                    { // Encode,%ScriptFile%,<DirName>,<FileName>,[Compression]
                        const int minArgCount = 3;
                        const int maxArgCount = 4;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        string compression = null;
                        if (3 < args.Count)
                            compression = args[3];

                        return new CodeInfo_Encode(args[0], args[1], args[2], compression);
                    }
                #endregion
                #region 10 Interface
                case CodeType.Visible:
                    { // Visible,<%InterfaceKey%>,<Visibility>
                        // [,PERMANENT] - for compability of WB082
                        const int minArgCount = 2;
                        const int maxArgCount = 3;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        string interfaceKey = Variables.TrimPercentMark(args[0]);
                        if (interfaceKey == null)
                            throw new InvalidCommandException($"Invalid InterfaceKey [{interfaceKey}]", rawCode);

                        string visibility;
                        if (args[1].Equals("True", StringComparison.OrdinalIgnoreCase) ||
                            args[1].Equals("False", StringComparison.OrdinalIgnoreCase) ||
                            Variables.DetectType(args[1]) != Variables.VarKeyType.None)
                            visibility = args[1];
                        else
                            throw new InvalidCommandException("Visibility must be one of True, False, or variable key.", rawCode);

                        if (2 < args.Count)
                        {
                            if (!args[2].Equals("PERMANENT", StringComparison.OrdinalIgnoreCase))
                                throw new InvalidCommandException($"Invalid optional argument or flag [{args[2]}]", rawCode);
                        }

                        return new CodeInfo_Visible(interfaceKey, visibility);
                    }
                case CodeType.ReadInterface:
                    { // ReadInterface,<Element>,<ScriptFile>,<Section>,<Key>,<DestVar>,[Delim=<Str>]
                        const int minArgCount = 5;
                        const int maxArgCount = 6;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        InterfaceElement element = ParseInterfaceElement(args[0]);

                        string destVar = args[4];
                        if (Variables.DetectType(destVar) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{destVar}] is not a valid variable name", rawCode);

                        string delim = null;
                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            string arg = args[i];

                            const string delimKey = "Delim=";
                            if (arg.StartsWith(delimKey, StringComparison.OrdinalIgnoreCase))
                            {
                                if (delim != null)
                                    throw new InvalidCommandException("Argument <Delim> cannot be duplicated", rawCode);
                                delim = arg[delimKey.Length..];
                            }
                            else
                            {
                                throw new InvalidCommandException($"Invalid optional argument or flag [{arg}]", rawCode);
                            }
                        }

                        if (delim != null && element != InterfaceElement.Items)
                            throw new InvalidCommandException($"Argument [Delim] can be only used with [{element}]", rawCode);

                        return new CodeInfo_ReadInterface(element, args[1], args[2], args[3], destVar, delim);
                    }
                case CodeType.WriteInterface:
                    { // WriteInterface,<Element>,<ScriptFile>,<Section>,<Key>,<Value>,[Delim=<Str>]
                        const int minArgCount = 5;
                        const int maxArgCount = 6;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        InterfaceElement element = ParseInterfaceElement(args[0]);

                        string delim = null;
                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            string arg = args[i];

                            const string delimKey = "Delim=";
                            if (arg.StartsWith(delimKey, StringComparison.OrdinalIgnoreCase))
                            {
                                if (delim != null)
                                    throw new InvalidCommandException("Argument <Delim> cannot be duplicated", rawCode);
                                delim = arg[delimKey.Length..];
                            }
                            else
                            {
                                throw new InvalidCommandException($"Invalid optional argument or flag [{arg}]", rawCode);
                            }
                        }

                        if (delim != null && element != InterfaceElement.Items)
                            throw new InvalidCommandException($"Argument [Delim] can be only used with [{element}]", rawCode);

                        return new CodeInfo_WriteInterface(element, args[1], args[2], args[3], args[4], delim);
                    }
                case CodeType.Message:
                    { // Message,<Message>[,ICON][,TIMEOUT]
                        const int minArgCount = 1;
                        const int maxArgCount = 3;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        string message = args[0];
                        CodeMessageAction action = CodeMessageAction.None;
                        string timeout = null;

                        if (2 <= args.Count)
                        {
                            string arg = args[1];
                            if (arg.Equals("Information", StringComparison.OrdinalIgnoreCase) || arg.Equals("Info", StringComparison.OrdinalIgnoreCase))
                                action = CodeMessageAction.Information;
                            else if (arg.Equals("Confirmation", StringComparison.OrdinalIgnoreCase) || arg.Equals("Confirm", StringComparison.OrdinalIgnoreCase))
                                action = CodeMessageAction.Confirmation;
                            else if (arg.Equals("Error", StringComparison.OrdinalIgnoreCase))
                                action = CodeMessageAction.Error;
                            else if (arg.Equals("Warning", StringComparison.OrdinalIgnoreCase) || arg.Equals("Warn", StringComparison.OrdinalIgnoreCase))
                                action = CodeMessageAction.Warning;
                            else
                                throw new InvalidCommandException($"Second argument [{args[1]}] must be one of \'Information\', \'Confirmation\', \'Error\' and \'Warning\'", rawCode);
                        }

                        if (3 <= args.Count)
                            timeout = args[2];

                        return new CodeInfo_Message(message, action, timeout);
                    }
                case CodeType.Echo:
                    { // Echo,<Message>,[WARN]
                        const int minArgCount = 1;
                        const int maxArgCount = 2;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        bool warn = false;
                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            string arg = args[i];
                            if (arg.Equals("WARN", StringComparison.OrdinalIgnoreCase))
                            {
                                if (warn)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                warn = true;
                            }
                            else
                                throw new InvalidCommandException($"Invalid optional argument or flag [{arg}]", rawCode);
                        }
                        return new CodeInfo_Echo(args[0], warn);
                    }
                case CodeType.EchoFile:
                    { // EchoFile,<SrcFile>[,WARN]
                        const int minArgCount = 1;
                        const int maxArgCount = 2;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        bool warn = false;
                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            string arg = args[i];
                            if (arg.Equals("WARN", StringComparison.OrdinalIgnoreCase))
                            {
                                if (warn)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                warn = true;
                            }
                            else
                                throw new InvalidCommandException($"Invalid optional argument or flag [{arg}]", rawCode);
                        }

                        return new CodeInfo_EchoFile(args[0], warn);
                    }
                case CodeType.UserInput:
                    return ParseCodeInfoUserInput(rawCode, args);
                case CodeType.AddInterface:
                    { // AddInterface,<ScriptFile>,<Interface>,<Prefix>
                        const int argCount = 3;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [{type}] must have [{argCount}] arguments", rawCode);

                        return new CodeInfo_AddInterface(args[0], args[1], args[2]);
                    }
                case CodeType.Retrieve:
                    { // Compability Shim for WinBuilder 082
                        const int argCount = 3;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [{type}] must have [{argCount}] arguments", rawCode);

                        if (Variables.DetectType(args[2]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[2]}] is not a valid variable name", rawCode);
                        else if (args[0].Equals("Dir", StringComparison.OrdinalIgnoreCase))
                        { // Retrieve.Dir -> UserInput.DirPath
                            type = CodeType.UserInput;
                            args[0] = "DirPath";
                            return ParseCodeInfo(rawCode, ref type, macroType, args, lineIdx);
                        }
                        else if (args[0].Equals("File", StringComparison.OrdinalIgnoreCase))
                        { // Retrieve.File -> UserInput.FilePath
                            type = CodeType.UserInput;
                            args[0] = "FilePath";
                            return ParseCodeInfo(rawCode, ref type, macroType, args, lineIdx);
                        }
                        else if (args[0].Equals("FileSize", StringComparison.OrdinalIgnoreCase))
                        { // Retrieve.FileSize -> FileSize
                            type = CodeType.FileSize;
                            args.RemoveAt(0);
                            return ParseCodeInfo(rawCode, ref type, macroType, args, lineIdx);
                        }
                        else if (args[0].Equals("FileVersion", StringComparison.OrdinalIgnoreCase))
                        { // Retrieve.FileVersion -> FileVersion
                            type = CodeType.FileVersion;
                            args.RemoveAt(0);
                            return ParseCodeInfo(rawCode, ref type, macroType, args, lineIdx);
                        }
                        else if (args[0].Equals("FolderSize", StringComparison.OrdinalIgnoreCase))
                        { // Retrieve.FolderSize -> DirSize
                            type = CodeType.DirSize;
                            args.RemoveAt(0);
                            return ParseCodeInfo(rawCode, ref type, macroType, args, lineIdx);
                        }
                        else if (args[0].Equals("MD5", StringComparison.OrdinalIgnoreCase))
                        { // Retrieve.MD5 -> Hash.MD5
                            type = CodeType.Hash;
                            return ParseCodeInfo(rawCode, ref type, macroType, args, lineIdx);
                        }

                        throw new InvalidCommandException($"Invalid command [Retrieve,{args[0]}]", rawCode);
                    }
                #endregion
                #region 20 String
                case CodeType.StrFormat:
                    return ParseCodeInfoStrFormat(rawCode, args);
                #endregion
                #region 21 Math
                case CodeType.Math:
                    return ParseCodeInfoMath(rawCode, args);
                #endregion
                #region 22 List
                case CodeType.List:
                    return ParseCodeInfoList(rawCode, args);
                #endregion
                #region 80 Branch
                case CodeType.Run:
                case CodeType.Exec:
                    {
                        // Run,<ScriptFile>,<Section>,[Params]
                        // Exec,<ScriptFile>,<Section>,[Params]
                        const int minArgCount = 2;
                        if (CheckInfoArgumentCount(args, minArgCount, -1))
                            throw new InvalidCommandException($"Command [{type}] must have at least [{minArgCount}] arguments", rawCode);

                        string scriptFile = args[0];
                        string sectionName = args[1];

                        // Get inParams
                        List<string> inParams = new List<string>();
                        if (minArgCount < args.Count)
                            inParams.AddRange(args.Skip(minArgCount));

                        return new CodeInfo_RunExec(scriptFile, sectionName, inParams, null);
                    }
                case CodeType.RunEx:
                    { // RunEx,<ScriptFile>,<Section>,[InOutParams]
                        const int minArgCount = 2;
                        if (CheckInfoArgumentCount(args, minArgCount, -1))
                            throw new InvalidCommandException($"Command [{type}] must have at least [{minArgCount}] arguments", rawCode);

                        string scriptFile = args[0];
                        string sectionName = args[1];

                        // Get parameters
                        const string inKey = "In=";
                        const string outKey = "Out=";
                        List<string> inParams = new List<string>();
                        List<string> outParams = new List<string>();
                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            string arg = args[i];
                            if (arg.StartsWith(inKey, StringComparison.OrdinalIgnoreCase))
                            {
                                inParams.Add(arg[inKey.Length..]);
                            }
                            else if (arg.StartsWith(outKey, StringComparison.OrdinalIgnoreCase))
                            {
                                string varKey = arg[outKey.Length..];
                                if (Variables.DetectType(varKey) != Variables.VarKeyType.Variable)
                                    throw new InvalidCommandException($"Out parameter [{varKey}] must be a normal variable enclosed in % characters", rawCode);
                                outParams.Add(varKey);
                            }
                            else
                            {
                                throw new InvalidCommandException($"Parameter of [{type}] must start with [In=] or [Out=]", rawCode);
                            }
                        }

                        return new CodeInfo_RunExec(scriptFile, sectionName, inParams, outParams);
                    }
                case CodeType.Loop:
                case CodeType.LoopLetter:
                    {
                        if (args.Count == 1)
                        { // Loop,BREAK
                            if (args[0].Equals("BREAK", StringComparison.OrdinalIgnoreCase))
                                return new CodeInfo_Loop();

                            throw new InvalidCommandException($"Invalid form of Command [{type}]", rawCode);
                        }

                        // Loop,%ScriptFile%,<Section>,<StartIndex>,<EndIndex>[,PARAMS]
                        const int minArgCount = 4;
                        if (CheckInfoArgumentCount(args, minArgCount, -1))
                            throw new InvalidCommandException($"Command [{type}] must have at least [{minArgCount}] arguments", rawCode);

                        // Get parameters 
                        List<string> inParams = new List<string>();
                        if (minArgCount < args.Count)
                            inParams.AddRange(args.Skip(minArgCount));

                        return new CodeInfo_Loop(args[0], args[1], args[2], args[3], inParams, null);
                    }
                case CodeType.LoopEx:
                case CodeType.LoopLetterEx:
                    {
                        // LoopEx,<ScriptFile>,<Section>,<StartIndex>,<EndIndex>[,InOutParams]
                        // LoopEx,BREAK
                        // LoopLetterEx,<ScriptFile>,<Section>,<StartLetter>,<EndLetter>[,InOutParams]
                        // LoopLetterEx,BREAK

                        if (args.Count == 1)
                        { // LoopEx,BREAK
                            if (args[0].Equals("BREAK", StringComparison.OrdinalIgnoreCase))
                                return new CodeInfo_Loop();

                            throw new InvalidCommandException($"Invalid form of Command [{type}]", rawCode);
                        }

                        // LoopEx,%ScriptFile%,<Section>,<StartIndex>,<EndIndex>[,PARAMS]
                        const int minArgCount = 4;
                        if (CheckInfoArgumentCount(args, minArgCount, -1))
                            throw new InvalidCommandException($"Command [{type}] must have at least [{minArgCount}] arguments", rawCode);

                        // Get parameters
                        const string inKey = "In=";
                        const string outKey = "Out=";
                        List<string> inParams = new List<string>();
                        List<string> outParams = new List<string>();
                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            string arg = args[i];
                            if (arg.StartsWith(inKey, StringComparison.OrdinalIgnoreCase))
                                inParams.Add(arg[inKey.Length..]);
                            else if (arg.StartsWith(outKey, StringComparison.OrdinalIgnoreCase))
                            {
                                string varKey = arg[outKey.Length..];
                                if (Variables.DetectType(varKey) != Variables.VarKeyType.Variable)
                                    throw new InvalidCommandException($"Out parameter [{varKey}] must be a normal variable enclosed in % characters", rawCode);
                                outParams.Add(varKey);
                            }
                            else
                                throw new InvalidCommandException($"Parameter of [{type}] must start with [In=] or [Out=]", rawCode);
                        }

                        return new CodeInfo_Loop(args[0], args[1], args[2], args[3], inParams, outParams);
                    }
                case CodeType.If:
                    return ParseCodeInfoIf(rawCode, args, lineIdx);
                case CodeType.Else:
                    return ParseCodeInfoElse(rawCode, args, lineIdx);
                case CodeType.Begin:
                case CodeType.End:
                    return new CodeInfo();
                #endregion
                #region 81 Control
                case CodeType.Set:
                    { // Set,<VarName>,<VarValue>[,GLOBAL | PERMANENT]
                        const int minArgCount = 2;
                        const int maxArgCount = 3;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        string varName = args[0];
                        string varValue = args[1];
                        bool global = false;
                        bool permanent = false;

                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            string arg = args[i];
                            if (arg.Equals("GLOBAL", StringComparison.OrdinalIgnoreCase))
                            {
                                if (global)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                global = true;
                            }
                            else if (arg.Equals("PERMANENT", StringComparison.OrdinalIgnoreCase))
                            {
                                if (permanent)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                permanent = true;
                            }
                            else
                                throw new InvalidCommandException($"Invalid optional argument or flag [{arg}]", rawCode);
                        }

                        return new CodeInfo_Set(varName, varValue, global, permanent);
                    }
                case CodeType.SetMacro:
                    { // SetMacro,<MacroName>,<MacroCommand>,[GLOBAL|PERMANENT]
                        const int minArgCount = 2;
                        const int maxArgCount = 3;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        string macroName = args[0];
                        string macroCommand = args[1];
                        bool global = false;
                        bool permanent = false;

                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            string arg = args[i];
                            if (arg.Equals("GLOBAL", StringComparison.OrdinalIgnoreCase))
                            {
                                if (global)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                global = true;
                            }
                            else if (arg.Equals("PERMANENT", StringComparison.OrdinalIgnoreCase))
                            {
                                if (permanent)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                permanent = true;
                            }
                            else
                                throw new InvalidCommandException($"Invalid optional argument or flag [{arg}]", rawCode);
                        }

                        return new CodeInfo_SetMacro(macroName, macroCommand, global, permanent);
                    }
                case CodeType.AddVariables:
                    { // AddVariables,%ScriptFile%,<Section>[,GLOBAL]
                        const int minArgCount = 2;
                        const int maxArgCount = 3;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        string varName = args[0];
                        string varValue = args[1];
                        bool global = false;

                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            string arg = args[i];
                            if (arg.Equals("GLOBAL", StringComparison.OrdinalIgnoreCase))
                            {
                                if (global)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                global = true;
                            }
                            else
                                throw new InvalidCommandException($"Invalid optional argument or flag [{arg}]", rawCode);
                        }

                        return new CodeInfo_AddVariables(varName, varValue, global);
                    }
                case CodeType.Exit:
                    { // Exit,[Message],[NOWARN]
                        const int minArgCount = 0;
                        const int maxArgCount = 2;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        string msg = string.Empty;
                        if (0 < args.Count)
                            msg = args[0];

                        bool noWarn = false;
                        if (1 < args.Count)
                        {
                            if (args[1].Equals("NOWARN", StringComparison.OrdinalIgnoreCase))
                                noWarn = true;
                        }

                        return new CodeInfo_Exit(msg, noWarn);
                    }
                case CodeType.Halt:
                    { // Halt,<Message>
                        const int minArgCount = 0;
                        const int maxArgCount = 1;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        string message = string.Empty;
                        if (1 <= args.Count)
                            message = args[0];
                        return new CodeInfo_Halt(message);
                    }
                case CodeType.Wait:
                    { // Wait,<Seconds>
                        const int argCount = 1;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [{type}] must have [{argCount}] arguments", rawCode);

                        return new CodeInfo_Wait(args[0]);
                    }
                case CodeType.Beep:
                    { // Beep,<Type>
                        const int argCount = 1;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [{type}] must have [{argCount}] arguments", rawCode);

                        string beepTypeStr = args[0];
                        if (!Enum.TryParse(beepTypeStr, true, out BeepType beepType))
                            throw new InvalidCommandException($"Invalid BeepType [{beepTypeStr}]");
                        if (Enum.IsDefined(typeof(BeepType), beepType) == false)
                            throw new InvalidCommandException($"Invalid BeepType [{beepTypeStr}]");

                        return new CodeInfo_Beep(beepType);
                    }
                case CodeType.GetParam:
                    { // GetParam,<Index>,<DestVar>
                        const int argCount = 2;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [{type}] must have [{argCount}] arguments", rawCode);

                        Variables.VarKeyType varKeyType = Variables.DetectType(args[1]);
                        switch (varKeyType)
                        {
                            case Variables.VarKeyType.Variable:
                                break;
                            case Variables.VarKeyType.SectionInParams:
                                throw new InvalidCommandException($"Section parameter [{args[1]}] cannot be used in GetParam", rawCode);
                            default:
                                throw new InvalidCommandException($"[{args[1]}] is not a valid variable name", rawCode);
                        }

                        return new CodeInfo_GetParam(args[0], args[1]);
                    }
                case CodeType.PackParam:
                    { // PackParam,<StartIndex>,<DestVar>,[VarCount]
                        const int minArgCount = 2;
                        const int maxArgCount = 3;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        Variables.VarKeyType varKeyType = Variables.DetectType(args[1]);
                        switch (varKeyType)
                        {
                            case Variables.VarKeyType.Variable:
                                break;
                            case Variables.VarKeyType.SectionInParams:
                                throw new InvalidCommandException($"Section parameter [{args[1]}] cannot be used in GetParam", rawCode);
                            default:
                                throw new InvalidCommandException($"[{args[1]}] is not a valid variable name", rawCode);
                        }

                        string varCount = null;
                        if (args.Count == 3)
                        {
                            varKeyType = Variables.DetectType(args[2]);
                            switch (varKeyType)
                            {
                                case Variables.VarKeyType.Variable:
                                    varCount = args[2];
                                    break;
                                case Variables.VarKeyType.SectionInParams:
                                    throw new InvalidCommandException($"Section parameter [{args[2]}] cannot be used in GetParam", rawCode);
                                default:
                                    throw new InvalidCommandException($"[{args[2]}] is not a valid variable name", rawCode);
                            }
                        }

                        return new CodeInfo_PackParam(args[0], args[1], varCount);
                    }
                #endregion
                #region 82 System
                case CodeType.System:
                    return ParseCodeInfoSystem(rawCode, args, lineIdx);
                case CodeType.ShellExecute:
                case CodeType.ShellExecuteEx:
                case CodeType.ShellExecuteDelete:
                    {
                        // ShellExecute,<Action>,<FilePath>[,Params][,WorkDir][,%ExitOutVar%]
                        // ShellExecuteEx,<Action>,<FilePath>[,Params][,WorkDir]
                        // ShellExecuteDelete,<Action>,<FilePath>[,Params][,WorkDir][,%ExitOutVar%]
                        const int minArgCount = 2;
                        const int maxArgCount = 5;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
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
                #region 98 Debug
                case CodeType.Debug:
                    return ParseCodeInfoDebug(rawCode, args);
                #endregion
                #region 99 External Macro
                case CodeType.Macro:
                    return new CodeInfo_Macro(macroType, args);
                #endregion
                #region Error
                default: // Error
                    throw new InternalParserException($"Wrong CodeType [{type}]");
                    #endregion
            }
        }
        #endregion

        #region CheckInfoArgumentCount
        /// <summary>
        /// Check CodeCommand's argument count
        /// </summary>
        /// <param name="op"></param>
        /// <param name="min"></param>
        /// <param name="max">-1 if unlimited argument</param>
        /// <returns>Return true if invalid</returns>
        public static bool CheckInfoArgumentCount(List<string> op, int min, int max)
        {
            if (max == -1) // Unlimited argument count
                return op.Count < min;
            return op.Count < min || max < op.Count;
        }
        #endregion

        #region ParseRegValueType, ParseRegMultiType
        public static uint ParseRegistryValueKind(string typeStr, bool unsafeMode)
        {
            // typeStr must be valid number
            if (!NumberHelper.ParseUInt32(typeStr, out uint valueType))
                throw new InvalidCommandException($"[{typeStr}] is not a valid number");

            switch (valueType)
            {
                case 0:
                case 1:
                case 2:
                case 3:
                case 4:
                case 7:
                case 11:
                    return valueType;
                default:
                    if (unsafeMode)
                        return valueType;
                    else
                        throw new InvalidCommandException($"Invalid registry value type [0x{valueType:X}]");
            }
        }

        public static RegMultiType ParseRegMultiType(string typeStr)
        {
            // There must be no number in typeStr
            if (!Regex.IsMatch(typeStr, @"^[A-Za-z_]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant))
                throw new InvalidCommandException($"Wrong RegMultiType [{typeStr}], Only alphabet and underscore can be used as RegMultiType");

            bool invalid = !Enum.TryParse(typeStr, true, out RegMultiType type) ||
                           !Enum.IsDefined(typeof(RegMultiType), type);

            if (invalid)
                throw new InvalidCommandException($"Invalid RegMultiType [{typeStr}]");

            return type;
        }
        #endregion

        #region ParseInterfaceElement
        public static InterfaceElement ParseInterfaceElement(string str)
        {
            if (!Regex.IsMatch(str, @"^[A-Za-z_]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant))
                throw new InvalidCommandException($"Wrong InterfaceElement [{str}], Only alphabet and underscore can be used");

            bool invalid = !Enum.TryParse(str, true, out InterfaceElement e) ||
                           !Enum.IsDefined(typeof(InterfaceElement), e);

            if (invalid)
                throw new InvalidCommandException($"Invalid InterfaceElement [{str}]");

            return e;
        }
        #endregion

        #region ParseCodeInfoUserInput, ParseUserInputType
        public static CodeInfo_UserInput ParseCodeInfoUserInput(string rawCode, List<string> args)
        {
            const int minArgCount = 3;
            const int maxArgCount = 5;
            if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                throw new InvalidCommandException($"Command [UserInput] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

            UserInputType type = ParseUserInputType(args[0]);
            UserInputInfo info;

            // Remove UserInputType
            args.RemoveAt(0);
            int argCount = minArgCount - 1; // subtract 1 from minArgCount to ignore UserInputType

            switch (type)
            {
                case UserInputType.DirPath:
                case UserInputType.FilePath:
                    {
                        // UserInput,DirPath,<Path>,<DestVar>,[Title=<Str>]
                        // UserInput,FilePath,<Path>,<DestVar>,[Title=<Str>][Filter=<Str>]

                        if (Variables.DetectType(args[1]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[1]}] is not valid variable name", rawCode);

                        string title = null;
                        string filter = null;

                        const string titleKey = "Title=";
                        const string filterKey = "Filter=";

                        for (int i = argCount; i < args.Count; i++)
                        {
                            string arg = args[i];

                            if (arg.StartsWith(titleKey, StringComparison.OrdinalIgnoreCase))
                            {
                                if (title != null)
                                    throw new InvalidCommandException("Argument <Title> cannot be duplicated", rawCode);
                                title = arg[titleKey.Length..];
                            }
                            else if (arg.StartsWith(filterKey, StringComparison.OrdinalIgnoreCase))
                            {
                                if (type == UserInputType.DirPath)
                                    throw new InvalidCommandException("Argument <Filter> can only be used for file selection", rawCode);
                                if (filter != null)
                                    throw new InvalidCommandException("Argument <Filter> cannot be duplicated", rawCode);
                                filter = arg[filterKey.Length..];
                            }
                            else
                            {
                                throw new InvalidCommandException($"Invalid optional argument [{arg}]", rawCode);
                            }
                        }

                        info = new UserInputInfo_DirFile(args[0], args[1], title, filter);
                    }
                    break;
                default: // Error
                    throw new InternalParserException($"Wrong UserInputType [{type}]");
            }

            return new CodeInfo_UserInput(type, info);
        }

        public static UserInputType ParseUserInputType(string typeStr)
        {
            // There must be no number in typeStr
            if (!Regex.IsMatch(typeStr, @"^[A-Za-z_]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant))
                throw new InvalidCommandException($"Wrong CodeType [{typeStr}], Only alphabet and underscore can be used as UserInputType");

            bool invalid = !Enum.TryParse(typeStr, true, out UserInputType type) ||
                           !Enum.IsDefined(typeof(UserInputType), type);

            if (invalid)
                throw new InvalidCommandException($"Invalid UserInputType [{typeStr}]");

            return type;
        }
        #endregion

        #region ParseCodeInfoStrFormat, ParseStrFormatType
        public static CodeInfo_StrFormat ParseCodeInfoStrFormat(string rawCode, List<string> args)
        {
            if (CheckInfoArgumentCount(args, 2, -1))
                throw new InvalidCommandException("Command [StrFormat] must have at least [2] arguments", rawCode);

            StrFormatType type = ParseStrFormatType(args[0]);
            StrFormatInfo info;

            // Remove StrFormatType
            args.RemoveAt(0);

            switch (type)
            {
                case StrFormatType.Bytes:
                case StrFormatType.IntToBytes:
                    {
                        // StrFormat,IntToBytes,<Integer>,<DestVar>
                        // StrFormat,IntToBytes,<SrcDestVar>
                        const int minArgCount = 1;
                        const int maxArgCount = 2;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [StrFormat,{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        string destVar = args[0];
                        if (args.Count == 2)
                            destVar = args[1];

                        if (Variables.DetectType(destVar) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{destVar}] is not a valid variable name", rawCode);

                        info = new StrFormatInfo_IntToBytes(args[0], destVar);
                    }
                    break;
                case StrFormatType.BytesToInt:
                    { // StrFormat,BytesToInt,<Bytes>,<DestVar>
                        const int argCount = 2;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [StrFormat,{type}] must have [{argCount}] arguments", rawCode);

                        if (Variables.DetectType(args[1]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[1]}] is not a valid variable name", rawCode);
                        info = new StrFormatInfo_BytesToInt(args[0], args[1]);
                    }
                    break;
                case StrFormatType.Hex:
                    { // StrFormat,Hex,<Integer>,<DestVar>
                        const int argCount = 2;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [StrFormat,{type}] must have [{argCount}] arguments", rawCode);

                        if (Variables.DetectType(args[1]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[1]}] is not a valid variable name", rawCode);

                        info = new StrFormatInfo_Hex(args[0], args[1]);
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

                        if (Variables.DetectType(args[0]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[0]}] is not a valid variable name", rawCode);
                        info = new StrFormatInfo_CeilFloorRound(args[0], args[1]);
                    }
                    break;
                case StrFormatType.Date:
                    { // StrFormat,Date,<DestVar>,<FormatString>
                        const int argCount = 2;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [StrFormat,{type}] must have [{argCount}] arguments", rawCode);

                        string destVar = args[0];
                        if (Variables.DetectType(destVar) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{destVar}] is not a valid variable name", rawCode);

                        // Convert WB Date Format String to .Net Date Format String
                        string formatStr = StrFormat_Date_FormatString(args[1]);
                        if (formatStr == null)
                            throw new InvalidCommandException($"Invalid date format string [{args[1]}]", rawCode);

                        info = new StrFormatInfo_Date(destVar, formatStr);
                    }
                    break;
                case StrFormatType.FileName:
                case StrFormatType.DirPath:
                case StrFormatType.Path:
                case StrFormatType.Ext:
                    {
                        // StrFormat,FileName,<FilePath>,<DestVar>
                        // StrFormat,DirPath,<FilePath>,<DestVar> -- Same with StrFormat,Path
                        // StrFormat,Ext,<FilePath>,<DestVar>

                        const int argCount = 2;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [StrFormat,{type}] must have [{argCount}] arguments", rawCode);

                        string destVar = args[1];
                        if (Variables.DetectType(destVar) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{destVar}] is not a valid variable name", rawCode);

                        info = new StrFormatInfo_Path(args[0], destVar);
                    }
                    break;
                case StrFormatType.PathCombine:
                    { // StrFormat,PathCombine,<DirPath>,<FileName>,<DestVar>
                        const int argCount = 3;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [StrFormat,{type}] must have [{argCount}] arguments", rawCode);

                        string destVar = args[2];
                        if (Variables.DetectType(destVar) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{destVar}] is not a valid variable name", rawCode);

                        info = new StrFormatInfo_PathCombine(args[0], args[1], destVar);
                    }
                    break;
                case StrFormatType.Inc:
                case StrFormatType.Dec:
                case StrFormatType.Mult:
                case StrFormatType.Div:
                    {
                        // StrFormat,Inc,<DestVar>,<Integer>
                        // StrFormat,Dec,<DestVar>,<Integer>
                        // StrFormat,Mult,<DestVar>,<Integer>
                        // StrFormat,Div,<DestVar>,<Integer>

                        const int argCount = 2;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [StrFormat,{type}] must have [{argCount}] arguments", rawCode);

                        if (Variables.DetectType(args[0]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[0]}] is not a valid variable name", rawCode);

                        info = new StrFormatInfo_Arithmetic(args[0], args[1]);
                    }
                    break;
                case StrFormatType.Left:
                case StrFormatType.Right:
                    {
                        // StrFormat,Left,<SrcString>,<Integer>,<DestVar>
                        // StrFormat,Right,<SrcString>,<Integer>,<DestVar>

                        const int argCount = 3;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [StrFormat,{type}] must have [{argCount}] arguments", rawCode);

                        if (Variables.DetectType(args[2]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[2]}] is not a valid variable name", rawCode);

                        info = new StrFormatInfo_LeftRight(args[0], args[1], args[2]);
                    }
                    break;
                case StrFormatType.Mid:
                    { // StrFormat,Mid,<SrcStr>,<StartPos>,<Length>,<DestVar>
                        const int argCount = 4;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [StrFormat,{type}] must have [{argCount}] arguments", rawCode);

                        if (Variables.DetectType(args[3]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[3]}] is not a valid variable name", rawCode);

                        info = new StrFormatInfo_Mid(args[0], args[1], args[2], args[3]);
                    }
                    break;
                case StrFormatType.Len:
                    { // StrFormat,Len,<SrcStr>,<DestVar>
                        const int argCount = 2;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [StrFormat,{type}] must have [{argCount}] arguments", rawCode);

                        if (Variables.DetectType(args[1]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[1]}] is not a valid variable name", rawCode);

                        info = new StrFormatInfo_Len(args[0], args[1]);
                    }
                    break;
                case StrFormatType.LTrim:
                case StrFormatType.RTrim:
                case StrFormatType.CTrim:
                    {
                        // StrFormat,LTrim,<SrcString>,<Integer>,<DestVar>
                        // StrFormat,RTrim,<SrcString>,<Integer>,<DestVar>
                        // StrFormat,CTrim,<SrcString>,<Chars>,<DestVar>

                        const int argCount = 3;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [StrFormat,{type}] must have [{argCount}] arguments", rawCode);

                        if (Variables.DetectType(args[2]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[2]}] is not a valid variable name", rawCode);

                        info = new StrFormatInfo_Trim(args[0], args[1], args[2]);
                    }
                    break;
                case StrFormatType.NTrim:
                    { // StrFormat,NTrim,<SrcString>,<DestVar>
                        const int argCount = 2;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [StrFormat,{type}] must have [{argCount}] arguments", rawCode);

                        if (Variables.DetectType(args[1]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[1]}] is not a valid variable name", rawCode);

                        info = new StrFormatInfo_NTrim(args[0], args[1]);
                    }
                    break;
                case StrFormatType.UCase:
                case StrFormatType.LCase:
                    {
                        // StrFormat,UCase,<SrcString>,<DestVar>
                        // StrFormat,LCase,<SrcString>,<DestVar>

                        const int argCount = 2;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [StrFormat,{type}] must have [{argCount}] arguments", rawCode);

                        if (Variables.DetectType(args[1]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[1]}] is not a valid variable name", rawCode);

                        info = new StrFormatInfo_ULCase(args[0], args[1]);
                    }
                    break;
                case StrFormatType.Pos:
                case StrFormatType.PosX:
                    { // StrFormat,Pos,<SrcString>,<SubString>,<DestVar>
                        const int argCount = 3;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [StrFormat,{type}] must have [{argCount}] arguments", rawCode);

                        if (Variables.DetectType(args[2]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[2]}] is not a valid variable name", rawCode);

                        info = new StrFormatInfo_Pos(args[0], args[1], args[2]);
                    }
                    break;
                case StrFormatType.Replace:
                case StrFormatType.ReplaceX:
                    {
                        // StrFormat,Replace,<SrcString>,<ToBeReplaced>,<ReplaceWith>,<DestVar>
                        // StrFormat,ReplaceX,<SrcString>,<ToBeReplaced>,<ReplaceWith>,<DestVar>

                        const int argCount = 4;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [StrFormat,{type}] must have [{argCount}] arguments", rawCode);

                        if (Variables.DetectType(args[3]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[3]}] is not a valid variable name", rawCode);

                        info = new StrFormatInfo_Replace(args[0], args[1], args[2], args[3]);
                    }
                    break;
                case StrFormatType.ShortPath:
                case StrFormatType.LongPath:
                    {
                        // StrFormat,ShortPath,<SrcString>,<DestVar>
                        // StrFormat,LongPath,<SrcString>,<DestVar>

                        const int argCount = 2;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [StrFormat,{type}] must have [{argCount}] arguments", rawCode);

                        if (Variables.DetectType(args[1]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[1]}] is not a valid variable name", rawCode);

                        info = new StrFormatInfo_ShortLongPath(args[0], args[1]);
                    }
                    break;
                case StrFormatType.Split:
                    { // StrFormat,Split,<SrcString>,<Delimiter>,<Index>,<DestVar>
                        const int argCount = 4;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [StrFormat,{type}] must have [{argCount}] arguments", rawCode);

                        if (Variables.DetectType(args[3]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[3]}] is not a valid variable name", rawCode);

                        info = new StrFormatInfo_Split(args[0], args[1], args[2], args[3]);
                    }
                    break;
                case StrFormatType.PadLeft:
                case StrFormatType.PadRight:
                    {
                        // StrFormat,PadLeft,<SrcStr>,<Count>,<PadChar>,<%DestVar%>
                        // StrFormat,PadRight,<SrcStr>,<Count>,<PadChar>,<%DestVar%>
                        const int argCount = 4;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [StrFormat,{type}] must have [{argCount}] arguments", rawCode);

                        string destVar = args[3];
                        if (Variables.DetectType(destVar) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{destVar}] is not a valid variable name", rawCode);

                        info = new StrFormatInfo_Pad(args[0], args[1], args[2], destVar);
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
            if (!Regex.IsMatch(typeStr, @"^[A-Za-z_]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant))
                throw new InvalidCommandException($"Wrong CodeType [{typeStr}], Only alphabet and underscore can be used as StrFormatType");

            bool invalid = !Enum.TryParse(typeStr, true, out StrFormatType type) ||
                           !Enum.IsDefined(typeof(StrFormatType), type);

            if (invalid)
                throw new InvalidCommandException($"Invalid StrFormatType [{typeStr}]");

            return type;
        }

        private static readonly Dictionary<string, string> DateFormatStringMap = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // Year
            [@"yyyy"] = @"yyyy",
            [@"yy"] = @"yy",
            [@"y"] = @"y",
            // Month
            [@"mmmm"] = @"MMMM",
            [@"mmm"] = @"MMM",
            [@"mm"] = @"MM",
            [@"m"] = @"M",
            // Date
            [@"dddd"] = @"dddd",
            [@"ddd"] = @"ddd",
            [@"dd"] = @"dd",
            [@"d"] = @"d",
            // Hour
            [@"hh"] = @"HH",
            [@"h"] = @"H",
            // Minute
            [@"nn"] = @"mm",
            [@"n"] = @"m",
            // Second
            [@"ss"] = @"ss",
            [@"s"] = @"s",
            // Millisecond
            [@"zzz"] = @"fff",
            [@"zz"] = @"fff",
            [@"z"] = @"fff",
            // AM/PM
            [@"am/pm"] = @"tt", // C# only supports uppercase AM/PM in CultureInfo.InvariantCulture
            // WB uses 12hr short time for t, 12hr long for tt
            [@"tt"] = @"h:mm:ss tt",
            [@"t"] = @"h:mm tt",
            // Gregorian Era (B.C./A.D.)
            [@"gg"] = @"gg",
            [@"g"] = @"gg"
        };

        // Year, Month, Date, Hour, Minute, Second, Millisecond, AM, PM, 12 hr Time, Era
        private static readonly char[] FormatStringAllowedChars = { 'y', 'm', 'd', 'h', 'n', 's', 'z', 'a', 'p', 't', 'g' };

        private static string StrFormat_Date_FormatString(string str)
        {
            // dd-mmm-yyyy-hh.nn
            // 02-11-2017-13.49

            // Check if there are only characters which are allowed
            string wbFormatStr = str.ToLower();
            foreach (char ch in wbFormatStr)
            {
                if ('a' <= ch && ch <= 'z')
                {
                    if (!FormatStringAllowedChars.Contains(ch))
                        return null;
                }
            }

            Dictionary<string, string>[] partialMaps = new Dictionary<string, string>[5];
            for (int i = 0; i < 5; i++)
                partialMaps[i] = DateFormatStringMap.Where(kv => kv.Key.Length == i + 1).ToDictionary(kv => kv.Key, kv => kv.Value);

            int idx = 0;
            bool hour12 = false;
            List<(int, int, string)> hourIdxs = new List<(int, int, string)>(2);
            bool processed = false;
            StringBuilder b = new StringBuilder();
            while (idx < wbFormatStr.Length)
            {
                for (int i = 5; 1 <= i; i--)
                {
                    processed = false;
                    if (idx + i <= wbFormatStr.Length)
                    {
                        string token = wbFormatStr.Substring(idx, i);
                        foreach (var kv in partialMaps[i - 1])
                        {
                            if (token.Equals(kv.Key, StringComparison.OrdinalIgnoreCase))
                            {
                                b.Append(kv.Value);
                                processed = true;

                                if (kv.Key.Equals("am/pm", StringComparison.OrdinalIgnoreCase))
                                    hour12 = true;
                                else if (kv.Key.Equals("hh", StringComparison.OrdinalIgnoreCase))
                                    hourIdxs.Add((idx, 2, "hh"));
                                else if (kv.Key.Equals("h", StringComparison.OrdinalIgnoreCase))
                                    hourIdxs.Add((idx, 1, "h"));

                                idx += i;
                                break;
                            }
                        }
                    }

                    if (processed)
                        break;
                }

                if (!processed && idx < wbFormatStr.Length)
                {
                    char ch = wbFormatStr[idx];
                    if ('a' <= ch && ch <= 'z') // Error
                        return null;

                    // Only if token is not alphabet
                    b.Append(ch);
                    idx += 1;
                }
            }

            string formatStr = b.ToString();
            if (hour12)
            {
                foreach ((int hIdx, int len, string replace) in hourIdxs)
                {
                    formatStr = StringHelper.ReplaceAt(formatStr, hIdx, len, replace);
                }
            }

            return formatStr;
        }
        #endregion

        #region ParseCodeInfoMath, ParseMathType
        public static CodeInfo_Math ParseCodeInfoMath(string rawCode, List<string> args)
        {
            MathType type = ParseMathType(args[0]);
            MathInfo info;

            // Remove MathType
            args.RemoveAt(0);

            switch (type)
            {
                case MathType.Add:
                case MathType.Sub:
                case MathType.Mul:
                case MathType.Div:
                    {
                        // Math,Add,<DestVar>,<Src1>,<Src2>
                        // Math,Sub,<DestVar>,<Src1>,<Src2>
                        // Math,Mul,<DestVar>,<Src1>,<Src2>
                        // Math,Div,<DestVar>,<Src1>,<Src2>

                        const int argCount = 3;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [Math,{type}] must have [{argCount}] arguments", rawCode);

                        // Check DestVar
                        if (Variables.DetectType(args[0]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[0]}] is not a valid variable name", rawCode);

                        info = new MathInfo_Arithmetic(args[0], args[1], args[2]);
                    }
                    break;
                case MathType.IntDiv:
                    { // Math,IntDiv,<QuotientVar>,<RemainderVar>,<Src1>,<Src2>
                        const int argCount = 4;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [Math,{type}] must have [{argCount}] arguments", rawCode);

                        // Check DestVar
                        if (Variables.DetectType(args[0]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[0]}] is not a valid variable name", rawCode);
                        if (Variables.DetectType(args[1]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[1]}] is not a valid variable name", rawCode);

                        info = new MathInfo_IntDiv(args[0], args[1], args[2], args[3]);
                    }
                    break;
                case MathType.Neg:
                    { // Math,Neg,<DestVar>,<Src>
                        const int argCount = 2;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [Math,{type}] must have [{argCount}] arguments", rawCode);

                        // Check DestVar
                        if (Variables.DetectType(args[0]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[0]}] is not a valid variable name", rawCode);

                        info = new MathInfo_Neg(args[0], args[1]);
                    }
                    break;
                case MathType.ToSign:
                case MathType.ToUnsign:
                    {
                        // Math,ToSign,<DestVar>,<Src>,<BitSize>
                        // Math,ToUnsign,<DestVar>,<Src>,<BitSize>
                        const int argCount = 3;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [Math,{type}] must have [{argCount}] arguments", rawCode);

                        // Check DestVar
                        string destVar = args[0];
                        if (Variables.DetectType(args[0]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[0]}] is not a valid variable name", rawCode);

                        // Check BitSize
                        string bitSize = args[2];
                        if (!CheckMathBitSizeStr(bitSize))
                            throw new InvalidCommandException($"[{bitSize} is not a valid bit size", rawCode);

                        info = new MathInfo_IntegerSignedness(destVar, args[1], bitSize);
                    }
                    break;
                case MathType.BoolAnd:
                case MathType.BoolOr:
                case MathType.BoolXor:
                    {
                        // Math,BoolAnd,<DestVar>,<Src1>,<Src2>
                        // Math,BoolOr,<DestVar>,<Src1>,<Src2>
                        // Math,BoolXor,<DestVar>,<Src1>,<Src2>

                        const int argCount = 3;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [Math,{type}] must have [{argCount}] arguments", rawCode);

                        // Check DestVar
                        if (Variables.DetectType(args[0]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[0]}] is not a valid variable name", rawCode);

                        info = new MathInfo_BoolLogicOperation(args[0], args[1], args[2]);
                    }
                    break;
                case MathType.BoolNot:
                    { // Math,BoolNot,<DestVar>,<Src>
                        const int argCount = 2;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [Math,{type}] must have [{argCount}] arguments", rawCode);

                        // Check DestVar
                        if (Variables.DetectType(args[0]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[0]}] is not a valid variable name", rawCode);

                        info = new MathInfo_BoolNot(args[0], args[1]);
                    }
                    break;
                case MathType.BitAnd:
                case MathType.BitOr:
                case MathType.BitXor:
                    {
                        // Math,BitAnd,<DestVar>,<Src1>,<Src2>
                        // Math,BitOr,<DestVar>,<Src1>,<Src2>
                        // Math,BitXor,<DestVar>,<Src1>,<Src2>

                        const int argCount = 3;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [Math,{type}] must have [{argCount}] arguments", rawCode);

                        // Check DestVar
                        if (Variables.DetectType(args[0]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[0]}] is not a valid variable name", rawCode);

                        info = new MathInfo_BitLogicOperation(args[0], args[1], args[2]);
                    }
                    break;
                case MathType.BitNot:
                    {  // Math,BitNot,<DestVar>,<Src>,<BitSize>
                        const int argCount = 3;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [Math,{type}] must have [{argCount}] arguments", rawCode);

                        // Check DestVar
                        string destVar = args[0];
                        if (Variables.DetectType(destVar) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{destVar}] is not a valid variable name", rawCode);

                        // Check BitSize
                        string bitSize = args[2];
                        if (!CheckMathBitSizeStr(bitSize))
                            throw new InvalidCommandException($"[{bitSize} is not a valid bit size", rawCode);

                        info = new MathInfo_BitNot(destVar, args[1], bitSize);
                    }
                    break;
                case MathType.BitShift:
                    { // Math,BitShift,<DestVar>,<Src>,<LEFT|RIGHT>,<Shift>,<BitSize>,[UNSIGNED]
                        const int minArgCount = 5;
                        const int maxArgCount = 6;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [Math,{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        // Check DestVar
                        string destVar = args[0];
                        if (Variables.DetectType(destVar) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{destVar}] is not a valid variable name", rawCode);

                        // Check BitSize
                        string bitSize = args[4];
                        if (!CheckMathBitSizeStr(bitSize))
                            throw new InvalidCommandException($"[{bitSize} is not a valid bit size", rawCode);

                        bool unsigned = false;
                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            string arg = args[i];
                            if (arg.Equals("UNSIGNED", StringComparison.OrdinalIgnoreCase))
                                unsigned = true;
                            else
                                throw new InvalidCommandException($"Invalid argument [{arg}]", rawCode);
                        }

                        info = new MathInfo_BitShift(destVar, args[1], args[2], args[3], bitSize, unsigned);
                    }
                    break;
                case MathType.Ceil:
                case MathType.Floor:
                case MathType.Round:
                    {
                        // Math,Ceil,<DestVar>,<Src>,<Unit>
                        // Math,Floor,<DestVar>,<Src>,<Unit>
                        // Math,Round,<DestVar>,<Src>,<Unit>

                        const int argCount = 3;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [Math,{type}] must have [{argCount}] arguments", rawCode);

                        if (Variables.DetectType(args[0]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[0]}] is not a valid variable name", rawCode);
                        info = new MathInfo_CeilFloorRound(args[0], args[1], args[2]);
                    }
                    break;
                case MathType.Abs:
                    { // Math,Abs,<DestVar>,<Src>
                        const int argCount = 2;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [Math,{type}] must have [{argCount}] arguments", rawCode);

                        if (Variables.DetectType(args[0]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[0]}] is not a valid variable name", rawCode);
                        info = new MathInfo_Abs(args[0], args[1]);
                    }
                    break;
                case MathType.Pow:
                    { // Math,Pow,<DestVar>,<Base>,<Power>
                        const int argCount = 3;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [Math,{type}] must have [{argCount}] arguments", rawCode);

                        if (Variables.DetectType(args[0]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[0]}] is not a valid variable name", rawCode);
                        info = new MathInfo_Pow(args[0], args[1], args[2]);
                    }
                    break;
                case MathType.Hex:
                case MathType.Dec:
                    {
                        // Math,Hex,<DestVar>,<Integer>,<BitSize>
                        // Math,Dec,<DestVar>,<Integer>,<BitSize>
                        const int argCount = 3;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [Math,{type}] must have [{argCount}] arguments", rawCode);

                        // Check DestVar
                        string destVar = args[0];
                        if (Variables.DetectType(destVar) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{destVar}] is not a valid variable name", rawCode);

                        // Check BitSize
                        string bitSize = args[2];
                        if (!CheckMathBitSizeStr(bitSize))
                            throw new InvalidCommandException($"[{bitSize} is not a valid bit size", rawCode);

                        info = new MathInfo_HexDec(destVar, args[1], bitSize);
                    }
                    break;
                case MathType.Rand:
                    { // Math,Rand,<DestVar>[,Min,Max]
                        // Must have 1 or 3 arguments
                        if (args.Count != 1 && args.Count != 3)
                            throw new InvalidCommandException($"Command [Math,{type}] must have [1] or [3] arguments", rawCode);

                        string destVar = args[0];
                        if (Variables.DetectType(destVar) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{destVar}] is not a valid variable name", rawCode);

                        string min = null;
                        string max = null;
                        if (3 == args.Count)
                        {
                            min = args[1];
                            max = args[2];
                        }

                        info = new MathInfo_Rand(destVar, min, max);
                    }
                    break;
                // Error
                default:
                    throw new InternalParserException($"Wrong MathType [{type}]");
            }

            return new CodeInfo_Math(type, info);
        }

        public static MathType ParseMathType(string typeStr)
        {
            // There must be no number in typeStr
            if (!Regex.IsMatch(typeStr, @"^[A-Za-z_]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant))
                throw new InvalidCommandException($"Wrong CodeType [{typeStr}], Only alphabet and underscore can be used as MathType");

            bool invalid = !Enum.TryParse(typeStr, true, out MathType type) ||
                           !Enum.IsDefined(typeof(MathType), type);

            if (invalid)
                throw new InvalidCommandException($"Invalid MathType [{typeStr}]");

            return type;
        }
        #endregion

        #region ParseCodeInfoList, ParseListType
        public static CodeInfo_List ParseCodeInfoList(string rawCode, List<string> args)
        {
            ListType type = ParseListType(args[0]);
            ListInfo info;

            // Remove MathType
            args.RemoveAt(0);

            switch (type)
            {
                case ListType.Get:
                    {
                        const int minArgCount = 3;
                        const int maxArgCount = 4;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        // Check ListVar
                        string listVar = args[0];
                        if (Variables.DetectType(listVar) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{listVar}] is not a valid variable name", rawCode);

                        // Check DestVar
                        string destVar = args[2];
                        if (Variables.DetectType(destVar) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{destVar}] is not a valid variable name", rawCode);

                        string delim = null;
                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            string arg = args[i];

                            const string delimKey = "Delim=";
                            if (arg.StartsWith(delimKey, StringComparison.OrdinalIgnoreCase))
                            {
                                if (delim != null)
                                    throw new InvalidCommandException("Argument <Delim> cannot be duplicated", rawCode);
                                delim = arg[delimKey.Length..];
                            }
                            else
                            {
                                throw new InvalidCommandException($"Invalid optional argument or flag [{arg}]", rawCode);
                            }
                        }

                        info = new ListInfo_Get(listVar, args[1], destVar, delim);
                    }
                    break;
                case ListType.Set:
                    {
                        const int minArgCount = 3;
                        const int maxArgCount = 4;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        // Check ListVar
                        string listVar = args[0];
                        if (Variables.DetectType(listVar) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{listVar}] is not a valid variable name", rawCode);

                        string delim = null;
                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            string arg = args[i];

                            const string delimKey = "Delim=";
                            if (arg.StartsWith(delimKey, StringComparison.OrdinalIgnoreCase))
                            {
                                if (delim != null)
                                    throw new InvalidCommandException("Argument <Delim> cannot be duplicated", rawCode);
                                delim = arg[delimKey.Length..];
                            }
                            else
                            {
                                throw new InvalidCommandException($"Invalid optional argument or flag [{arg}]", rawCode);
                            }
                        }

                        info = new ListInfo_Set(listVar, args[1], args[2], delim);
                    }
                    break;
                case ListType.Append:
                    {
                        const int minArgCount = 2;
                        const int maxArgCount = 3;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        // Check ListVar
                        string listVar = args[0];
                        if (Variables.DetectType(listVar) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{listVar}] is not a valid variable name", rawCode);

                        string delim = null;
                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            string arg = args[i];

                            const string delimKey = "Delim=";
                            if (arg.StartsWith(delimKey, StringComparison.OrdinalIgnoreCase))
                            {
                                if (delim != null)
                                    throw new InvalidCommandException("Argument <Delim> cannot be duplicated", rawCode);
                                delim = arg[delimKey.Length..];
                            }
                            else
                            {
                                throw new InvalidCommandException($"Invalid optional argument or flag [{arg}]", rawCode);
                            }
                        }

                        info = new ListInfo_Append(listVar, args[1], delim);
                    }
                    break;
                case ListType.Insert:
                    {
                        const int minArgCount = 3;
                        const int maxArgCount = 4;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        // Check ListVar
                        string listVar = args[0];
                        if (Variables.DetectType(listVar) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{listVar}] is not a valid variable name", rawCode);

                        string delim = null;
                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            string arg = args[i];
                            const string delimKey = "Delim=";
                            if (arg.StartsWith(delimKey, StringComparison.OrdinalIgnoreCase))
                            {
                                if (delim != null)
                                    throw new InvalidCommandException("Argument <Delim> cannot be duplicated", rawCode);
                                delim = arg[delimKey.Length..];
                            }
                            else
                            {
                                throw new InvalidCommandException($"Invalid optional argument or flag [{arg}]", rawCode);
                            }
                        }

                        info = new ListInfo_Insert(listVar, args[1], args[2], delim);
                    }
                    break;
                case ListType.Remove:
                case ListType.RemoveX:
                    {
                        const int minArgCount = 2;
                        const int maxArgCount = 3;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        // Check ListVar
                        string listVar = args[0];
                        if (Variables.DetectType(listVar) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{listVar}] is not a valid variable name", rawCode);

                        string delim = null;
                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            string arg = args[i];
                            const string delimKey = "Delim=";
                            if (arg.StartsWith(delimKey, StringComparison.OrdinalIgnoreCase))
                            {
                                if (delim != null)
                                    throw new InvalidCommandException("Argument <Delim> cannot be duplicated", rawCode);
                                delim = arg[delimKey.Length..];
                            }
                            else
                            {
                                throw new InvalidCommandException($"Invalid optional argument or flag [{arg}]", rawCode);
                            }
                        }

                        info = new ListInfo_Remove(listVar, args[1], delim);
                    }
                    break;
                case ListType.RemoveAt:
                    {
                        const int minArgCount = 2;
                        const int maxArgCount = 3;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        // Check ListVar
                        string listVar = args[0];
                        if (Variables.DetectType(listVar) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{listVar}] is not a valid variable name", rawCode);

                        string delim = null;
                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            string arg = args[i];
                            const string delimKey = "Delim=";
                            if (arg.StartsWith(delimKey, StringComparison.OrdinalIgnoreCase))
                            {
                                if (delim != null)
                                    throw new InvalidCommandException("Argument <Delim> cannot be duplicated", rawCode);
                                delim = arg[delimKey.Length..];
                            }
                            else
                            {
                                throw new InvalidCommandException($"Invalid optional argument or flag [{arg}]", rawCode);
                            }
                        }

                        info = new ListInfo_RemoveAt(listVar, args[1], delim);
                    }
                    break;
                case ListType.Count:
                    {
                        const int minArgCount = 2;
                        const int maxArgCount = 3;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        // Check ListVar
                        string listVar = args[0];
                        if (Variables.DetectType(listVar) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{listVar}] is not a valid variable name", rawCode);

                        // Check DestVar
                        string destVar = args[1];
                        if (Variables.DetectType(destVar) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{destVar}] is not a valid variable name", rawCode);

                        string delim = null;
                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            string arg = args[i];

                            const string delimKey = "Delim=";
                            if (arg.StartsWith(delimKey, StringComparison.OrdinalIgnoreCase))
                            {
                                if (delim != null)
                                    throw new InvalidCommandException("Argument <Delim> cannot be duplicated", rawCode);
                                delim = arg[delimKey.Length..];
                            }
                            else
                            {
                                throw new InvalidCommandException($"Invalid optional argument or flag [{arg}]", rawCode);
                            }
                        }

                        info = new ListInfo_Count(listVar, destVar, delim);
                    }
                    break;
                case ListType.Pos:
                case ListType.PosX:
                case ListType.LastPos:
                case ListType.LastPosX:
                    {
                        const int minArgCount = 3;
                        const int maxArgCount = 4;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        // Check ListVar
                        string listVar = args[0];
                        if (Variables.DetectType(listVar) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{listVar}] is not a valid variable name", rawCode);

                        // Check DestVar
                        string destVar = args[2];
                        if (Variables.DetectType(destVar) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{destVar}] is not a valid variable name", rawCode);

                        string delim = null;
                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            string arg = args[i];

                            const string delimKey = "Delim=";
                            if (arg.StartsWith(delimKey, StringComparison.OrdinalIgnoreCase))
                            {
                                if (delim != null)
                                    throw new InvalidCommandException("Argument <Delim> cannot be duplicated", rawCode);
                                delim = arg[delimKey.Length..];
                            }
                            else
                            {
                                throw new InvalidCommandException($"Invalid optional argument or flag [{arg}]", rawCode);
                            }
                        }

                        info = new ListInfo_Pos(listVar, args[1], destVar, delim);
                    }
                    break;
                case ListType.Sort:
                case ListType.SortX:
                case ListType.SortN:
                case ListType.SortNX:
                    { // List,Sort,<%ListVar%>,<Asc|Desc>,[Delim=<Str>]
                        const int minArgCount = 2;
                        const int maxArgCount = 3;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        // Check ListVar
                        string listVar = args[0];
                        if (Variables.DetectType(listVar) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{listVar}] is not a valid variable name", rawCode);

                        string delim = null;
                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            string arg = args[i];

                            const string delimKey = "Delim=";
                            if (arg.StartsWith(delimKey, StringComparison.OrdinalIgnoreCase))
                            {
                                if (delim != null)
                                    throw new InvalidCommandException("Argument <Delim> cannot be duplicated", rawCode);
                                delim = arg[delimKey.Length..];
                            }
                            else
                            {
                                throw new InvalidCommandException($"Invalid optional argument or flag [{arg}]", rawCode);
                            }
                        }

                        info = new ListInfo_Sort(listVar, args[1], delim);
                    }
                    break;
                default: // Error
                    throw new InternalParserException($"Wrong ListType [{type}]");
            }

            return new CodeInfo_List(type, info);
        }

        public static ListType ParseListType(string typeStr)
        {
            // There must be no number in typeStr
            if (!Regex.IsMatch(typeStr, @"^[A-Za-z_]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant))
                throw new InvalidCommandException($"Wrong CodeType [{typeStr}], Only alphabet and underscore can be used as ListType");

            bool invalid = !Enum.TryParse(typeStr, true, out ListType type) ||
                           !Enum.IsDefined(typeof(ListType), type);

            if (invalid)
                throw new InvalidCommandException($"Invalid MathType [{typeStr}]");

            return type;
        }

        /// <summary>
        /// Return true if valid
        /// </summary>
        /// <param name="sizeStr"></param>
        /// <returns>Return true if valid</returns>
        public static bool CheckMathBitSizeStr(string sizeStr)
        {
            // If a str is a variable, BitSizeStr is always valid
            if (Variables.DetectType(sizeStr) != Variables.VarKeyType.None)
                return true;

            // If a str is a const string, it must be one of 8, 16, 32 and 64
            if (!NumberHelper.ParseInt32(sizeStr, out int sizeVal))
                return false;
            return sizeVal == 8 || sizeVal == 16 || sizeVal == 32 || sizeVal == 64;
        }
        #endregion

        #region ParseCodeInfoSystem, ParseSystemType
        public CodeInfo_System ParseCodeInfoSystem(string rawCode, List<string> args, int lineIdx)
        {
            SystemType type = ParseSystemType(args[0]);
            SystemInfo info;

            // Remove SystemType
            args.RemoveAt(0);

            switch (type)
            {
                case SystemType.Cursor:
                    { // System,Cursor,<IconKind>
                        const int argCount = 1;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [System,{type}] must have [{argCount}] arguments", rawCode);

                        info = new SystemInfo_Cursor(args[0]);
                    }
                    break;
                case SystemType.ErrorOff:
                    { // System,ErrorOff,[Lines]
                        const int minArgCount = 0;
                        const int maxArgCount = 1;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [System,{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        if (args.Count == 0) // No args
                            info = new SystemInfo_ErrorOff();
                        else
                            info = new SystemInfo_ErrorOff(args[0]);
                    }
                    break;
                case SystemType.GetEnv:
                    { // System,GetEnv,<EnvVarName>,<DestVar>
                        const int argCount = 2;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [System,{type}] must have [{argCount}] arguments", rawCode);

                        if (Variables.DetectType(args[1]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[1]}] is not a valid variable name", rawCode);

                        info = new SystemInfo_GetEnv(args[0], args[1]);
                    }
                    break;
                case SystemType.GetFreeDrive:
                    { // System,GetFreeDrive,<DestVar>
                        const int argCount = 1;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [System,{type}] must have [{argCount}] arguments", rawCode);

                        if (Variables.DetectType(args[0]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[0]}] is not a valid variable name", rawCode);

                        info = new SystemInfo_GetFreeDrive(args[0]);
                    }
                    break;
                case SystemType.GetFreeSpace:
                    { // System,GetFreeSpace,<Path>,<DestVar>
                        const int argCount = 2;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [System,{type}] must have [{argCount}] arguments", rawCode);

                        if (Variables.DetectType(args[1]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[1]}] is not a valid variable name", rawCode);

                        info = new SystemInfo_GetFreeSpace(args[0], args[1]);
                    }
                    break;
                case SystemType.IsAdmin:
                    { // System,IsAdmin,<DestVar>
                        const int argCount = 1;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [System,{type}] must have [{argCount}] arguments", rawCode);

                        if (Variables.DetectType(args[0]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[0]}] is not a valid variable name", rawCode);

                        info = new SystemInfo_IsAdmin(args[0]);
                    }
                    break;
                case SystemType.OnBuildExit:
                    { // System,OnBuildExit,<Command>
                        const int minArgCount = 1;
                        if (CheckInfoArgumentCount(args, minArgCount, -1))
                            throw new InvalidCommandException($"Command [{type}] must have at least [{minArgCount}] arguments", rawCode);

                        CodeCommand embed = ParseStatementFromSlicedArgs(rawCode, args, lineIdx);

                        info = new SystemInfo_OnBuildExit(embed);
                    }
                    break;
                case SystemType.OnScriptExit:
                    { // System,OnScriptExit,<Command>
                        const int minArgCount = 1;
                        if (CheckInfoArgumentCount(args, minArgCount, -1))
                            throw new InvalidCommandException($"Command [{type}] must have at least [{minArgCount}] arguments", rawCode);

                        CodeCommand embed = ParseStatementFromSlicedArgs(rawCode, args, lineIdx);

                        info = new SystemInfo_OnScriptExit(embed);
                    }
                    break;
                case SystemType.RefreshInterface:
                    { // System,RefreshInterface
                        const int argCount = 0;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [System,{type}] must have [{argCount}] arguments", rawCode);

                        info = new SystemInfo();
                    }
                    break;
                case SystemType.RefreshAllScripts:
                case SystemType.RescanScripts:
                    { // System,RefreshAllScripts
                        const int argCount = 0;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [System,{type}] must have [{argCount}] arguments", rawCode);

                        info = new SystemInfo();
                    }
                    break;
                case SystemType.LoadNewScript:
                    { // System,LoadNewScript,<SrcFilePath>,<DestTreeDir>,[PRESERVE],[NOWARN],[NOREC]
                        const int minArgCount = 2;
                        const int maxArgCount = 5;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [System,{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        bool preserve = false;
                        bool noWarn = false;
                        bool noRec = false;
                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            string arg = args[i];
                            if (arg.Equals("PRESERVE", StringComparison.OrdinalIgnoreCase))
                            {
                                if (preserve)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                preserve = true;
                            }
                            else if (arg.Equals("NOWARN", StringComparison.OrdinalIgnoreCase))
                            {
                                if (noWarn)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                noWarn = true;
                            }
                            else if (arg.Equals("NOREC", StringComparison.OrdinalIgnoreCase))
                            {
                                if (noRec)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                noRec = true;
                            }
                            else
                                throw new InvalidCommandException($"Invalid optional argument or flag [{arg}]", rawCode);
                        }

                        info = new SystemInfo_LoadNewScript(args[0], args[1], preserve, noWarn, noRec);
                    }
                    break;
                case SystemType.RefreshScript:
                    { // System,RefreshScript,<FilePath>,[NOREC]
                        const int minArgCount = 1;
                        const int maxArgCount = 2;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [System,{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        bool noRec = false;
                        for (int i = minArgCount; i < args.Count; i++)
                        {
                            string arg = args[i];
                            if (arg.Equals("NOREC", StringComparison.OrdinalIgnoreCase))
                            {
                                if (noRec)
                                    throw new InvalidCommandException("Flag cannot be duplicated", rawCode);
                                noRec = true;
                            }
                            else
                                throw new InvalidCommandException($"Invalid optional argument or flag [{arg}]", rawCode);
                        }

                        info = new SystemInfo_RefreshScript(args[0], noRec);
                    }
                    break;
                case SystemType.SaveLog:
                    { // System,SaveLog,<DestPath>,[LogFormat]
                        const int minArgCount = 1;
                        const int maxArgCount = 2;
                        if (CheckInfoArgumentCount(args, minArgCount, maxArgCount))
                            throw new InvalidCommandException($"Command [System,{type}] can have [{minArgCount}] ~ [{maxArgCount}] arguments", rawCode);

                        string logFormat = null;
                        if (minArgCount < args.Count)
                            logFormat = args[1];

                        info = new SystemInfo_SaveLog(args[0], logFormat);
                    }
                    break;
                case SystemType.SetLocal:
                    { // System,SetLocal
                        const int argCount = 0;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [System,{type}] must have [{argCount}] arguments", rawCode);

                        // Return empty SystemInfo
                        info = new SystemInfo();
                    }
                    break;
                case SystemType.EndLocal:
                    { // System,EndLocal
                        const int argCount = 0;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [System,{type}] must have [{argCount}] arguments", rawCode);

                        // Return empty SystemInfo
                        info = new SystemInfo();
                    }
                    break;
                // Compability Shim
                case SystemType.HasUAC:
                    { // System,HasUAC,<Command>
                        const int argCount = 1;
                        if (args.Count != argCount)
                            throw new InvalidCommandException($"Command [System,{type}] must have [{argCount}] arguments", rawCode);

                        if (Variables.DetectType(args[0]) == Variables.VarKeyType.None)
                            throw new InvalidCommandException($"[{args[0]}] is not a valid variable name", rawCode);

                        info = new SystemInfo_HasUAC(args[0]);
                    }
                    break;
                case SystemType.FileRedirect:
                    info = new SystemInfo();
                    break;
                case SystemType.RegRedirect:
                    info = new SystemInfo();
                    break;
                case SystemType.RebuildVars:
                    info = new SystemInfo();
                    break;
                default: // Error
                    throw new InternalParserException($"Wrong SystemType [{type}]");
            }

            return new CodeInfo_System(type, info);
        }

        public static SystemType ParseSystemType(string typeStr)
        {
            // There must be no number in typeStr
            if (!Regex.IsMatch(typeStr, @"^[A-Za-z_]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant))
                throw new InvalidCommandException($"Wrong CodeType [{typeStr}], Only alphabet and underscore can be used as SystemType");

            bool invalid = !Enum.TryParse(typeStr, true, out SystemType type) ||
                           !Enum.IsDefined(typeof(SystemType), type);

            if (invalid)
                throw new InvalidCommandException($"Invalid SystemType [{typeStr}]");

            return type;
        }
        #endregion

        #region ParseCodeInfoDebug, ParseDebugType
        public CodeInfo_Debug ParseCodeInfoDebug(string rawCode, List<string> args)
        {
            DebugType type = ParseDebugType(args[0]);
            DebugInfo info;

            // Remove DebugType
            args.RemoveAt(0);

            switch (type)
            {
                case DebugType.Breakpoint:
                    { // Debug,Breakpoint,[BranchCondition]
                        BranchCondition cond = null;

                        // BranchCondition was written
                        if (0 < args.Count)
                        {
                            int skipArgs;
                            (cond, skipArgs) = ParseBranchCondition(rawCode, args);
                            if (args.Count != skipArgs)
                                throw new InvalidCommandException("Command [Debug,Breakpoint] cannot have additional arguments after [BranchCondition]", rawCode);
                        }

                        info = new DebugInfo_Breakpoint(cond);
                    }
                    break;
                // Error
                default:
                    throw new InternalParserException($"Wrong DebugType [{type}]");
            }

            return new CodeInfo_Debug(type, info);
        }

        public static DebugType ParseDebugType(string typeStr)
        {
            // There must be no number in typeStr
            if (!Regex.IsMatch(typeStr, @"^[A-Za-z_]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant))
                throw new InvalidCommandException($"Wrong CodeType [{typeStr}], Only alphabet and underscore can be used as DebugType");

            bool invalid = !Enum.TryParse(typeStr, true, out DebugType type) ||
                           !Enum.IsDefined(typeof(DebugType), type);

            if (invalid)
                throw new InvalidCommandException($"Invalid DebugType [{typeStr}]");

            return type;
        }
        #endregion

        #region ParseBranchCondition
        public (BranchCondition Cond, int SkipArgs) ParseBranchCondition(string rawCode, List<string> args)
        {
            if (args.Count < 1)
                throw new InvalidCommandException("Unable to parse BranchCondition from empty arguments", rawCode);

            int cIdx = 0;
            bool notFlag = false;
            if (args[0].Equals("Not", StringComparison.OrdinalIgnoreCase))
            {
                notFlag = true;
                cIdx++;
            }

            void CheckArgumentCount(BranchConditionType condType, int minArgCount)
            {
                if (args.Count < minArgCount)
                    throw new InvalidCommandException($"BranchCondition [{condType}] must have at least [{minArgCount}] arguments", rawCode);
            }

            // BranchCondition - Non-Compare series
            {
                int embIdx = -1;
                string condStr = args[cIdx];
                BranchCondition cond = null;
                if (condStr.Equals("ExistFile", StringComparison.OrdinalIgnoreCase))
                {
                    embIdx = cIdx + 2;
                    CheckArgumentCount(BranchConditionType.ExistFile, embIdx);
                    cond = new BranchCondition(BranchConditionType.ExistFile, notFlag, args[cIdx + 1]);
                }
                else if (condStr.Equals("ExistDir", StringComparison.OrdinalIgnoreCase))
                {
                    embIdx = cIdx + 2;
                    CheckArgumentCount(BranchConditionType.ExistDir, embIdx);
                    cond = new BranchCondition(BranchConditionType.ExistDir, notFlag, args[cIdx + 1]);
                }
                else if (condStr.Equals("ExistSection", StringComparison.OrdinalIgnoreCase))
                {
                    embIdx = cIdx + 3;
                    CheckArgumentCount(BranchConditionType.ExistSection, embIdx);
                    cond = new BranchCondition(BranchConditionType.ExistSection, notFlag, args[cIdx + 1], args[cIdx + 2]);
                }
                else if (condStr.Equals("ExistRegSection", StringComparison.OrdinalIgnoreCase))
                { // Will-be-deprecated
                    embIdx = cIdx + 3;
                    CheckArgumentCount(BranchConditionType.ExistRegSection, embIdx);
                    cond = new BranchCondition(BranchConditionType.ExistRegSection, notFlag, args[cIdx + 1], args[cIdx + 2]);
                }
                else if (condStr.Equals("ExistRegSubKey", StringComparison.OrdinalIgnoreCase))
                {
                    embIdx = cIdx + 3;
                    CheckArgumentCount(BranchConditionType.ExistRegSubKey, embIdx);
                    cond = new BranchCondition(BranchConditionType.ExistRegSubKey, notFlag, args[cIdx + 1], args[cIdx + 2]);
                }
                else if (condStr.Equals("ExistRegKey", StringComparison.OrdinalIgnoreCase))
                { // Will-be-deprecated
                    embIdx = cIdx + 4;
                    CheckArgumentCount(BranchConditionType.ExistRegKey, embIdx);
                    cond = new BranchCondition(BranchConditionType.ExistRegKey, notFlag, args[cIdx + 1], args[cIdx + 2], args[cIdx + 3]);
                }
                else if (condStr.Equals("ExistRegValue", StringComparison.OrdinalIgnoreCase))
                {
                    embIdx = cIdx + 4;
                    CheckArgumentCount(BranchConditionType.ExistRegValue, embIdx);
                    cond = new BranchCondition(BranchConditionType.ExistRegValue, notFlag, args[cIdx + 1], args[cIdx + 2], args[cIdx + 3]);
                }
                else if (condStr.Equals("ExistRegMulti", StringComparison.OrdinalIgnoreCase))
                {
                    embIdx = cIdx + 5;
                    CheckArgumentCount(BranchConditionType.ExistRegMulti, embIdx);
                    cond = new BranchCondition(BranchConditionType.ExistRegMulti, notFlag, args[cIdx + 1], args[cIdx + 2], args[cIdx + 3], args[cIdx + 4]);
                }
                else if (condStr.Equals("ExistVar", StringComparison.OrdinalIgnoreCase))
                {
                    embIdx = cIdx + 2;
                    CheckArgumentCount(BranchConditionType.ExistVar, embIdx);
                    cond = new BranchCondition(BranchConditionType.ExistVar, notFlag, args[cIdx + 1]);
                }
                else if (condStr.Equals("ExistMacro", StringComparison.OrdinalIgnoreCase))
                {
                    embIdx = cIdx + 2;
                    CheckArgumentCount(BranchConditionType.ExistMacro, embIdx);
                    cond = new BranchCondition(BranchConditionType.ExistMacro, notFlag, args[cIdx + 1]);
                }
                else if (condStr.Equals("WimExistIndex", StringComparison.OrdinalIgnoreCase))
                {
                    embIdx = cIdx + 3;
                    CheckArgumentCount(BranchConditionType.WimExistIndex, embIdx);
                    cond = new BranchCondition(BranchConditionType.WimExistIndex, notFlag, args[cIdx + 1], args[cIdx + 2]);
                }
                else if (condStr.Equals("WimExistFile", StringComparison.OrdinalIgnoreCase))
                {
                    embIdx = cIdx + 4;
                    CheckArgumentCount(BranchConditionType.WimExistFile, embIdx);
                    cond = new BranchCondition(BranchConditionType.WimExistFile, notFlag, args[cIdx + 1], args[cIdx + 2], args[cIdx + 3]);
                }
                else if (condStr.Equals("WimExistDir", StringComparison.OrdinalIgnoreCase))
                {
                    embIdx = cIdx + 4;
                    CheckArgumentCount(BranchConditionType.WimExistDir, embIdx);
                    cond = new BranchCondition(BranchConditionType.WimExistDir, notFlag, args[cIdx + 1], args[cIdx + 2], args[cIdx + 3]);
                }
                else if (condStr.Equals("WimExistImageInfo", StringComparison.OrdinalIgnoreCase))
                {
                    embIdx = cIdx + 4;
                    CheckArgumentCount(BranchConditionType.WimExistImageInfo, embIdx);
                    cond = new BranchCondition(BranchConditionType.WimExistImageInfo, notFlag, args[cIdx + 1], args[cIdx + 2], args[cIdx + 3]);

                }
                else if (condStr.Equals("Ping", StringComparison.OrdinalIgnoreCase))
                {
                    embIdx = cIdx + 2;
                    CheckArgumentCount(BranchConditionType.Ping, embIdx);
                    cond = new BranchCondition(BranchConditionType.Ping, notFlag, args[cIdx + 1]);
                }
                else if (condStr.Equals("Online", StringComparison.OrdinalIgnoreCase))
                {
                    embIdx = cIdx + 1;
                    CheckArgumentCount(BranchConditionType.Online, embIdx);
                    cond = new BranchCondition(BranchConditionType.Online, notFlag);
                }
                else if (condStr.Equals("Question", StringComparison.OrdinalIgnoreCase))
                {
                    Match m = Regex.Match(args[cIdx + 2], @"([0-9]+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
                    if (m.Success)
                    {
                        embIdx = cIdx + 4;
                        CheckArgumentCount(BranchConditionType.Question, embIdx);
                        cond = new BranchCondition(BranchConditionType.Question, notFlag, args[cIdx + 1], args[cIdx + 2], args[cIdx + 3]);
                    }
                    else
                    {
                        embIdx = cIdx + 2;
                        CheckArgumentCount(BranchConditionType.Question, embIdx);
                        cond = new BranchCondition(BranchConditionType.Question, notFlag, args[cIdx + 1]);
                    }
                }
                else
                {
                    if (_opts.AllowLegacyBranchCondition)
                    { // Deprecated BranchConditions
                        if (condStr.Equals("NotExistFile", StringComparison.OrdinalIgnoreCase))
                        {
                            if (notFlag)
                                throw new InvalidCommandException("Branch condition [Not] cannot be duplicated", rawCode);
                            embIdx = cIdx + 2;
                            CheckArgumentCount(BranchConditionType.ExistFile, embIdx);
                            cond = new BranchCondition(BranchConditionType.ExistFile, true, args[cIdx + 1]);

                        }
                        else if (condStr.Equals("NotExistDir", StringComparison.OrdinalIgnoreCase))
                        {
                            if (notFlag)
                                throw new InvalidCommandException("Branch condition [Not] cannot be duplicated", rawCode);
                            embIdx = cIdx + 2;
                            CheckArgumentCount(BranchConditionType.ExistDir, embIdx);
                            cond = new BranchCondition(BranchConditionType.ExistDir, true, args[cIdx + 1]);
                        }
                        else if (condStr.Equals("NotExistSection", StringComparison.OrdinalIgnoreCase))
                        {
                            if (notFlag)
                                throw new InvalidCommandException("Branch condition [Not] cannot be duplicated", rawCode);
                            embIdx = cIdx + 3;
                            CheckArgumentCount(BranchConditionType.ExistSection, embIdx);
                            cond = new BranchCondition(BranchConditionType.ExistSection, true, args[cIdx + 1], args[cIdx + 2]);
                        }
                        else if (condStr.Equals("NotExistRegSection", StringComparison.OrdinalIgnoreCase))
                        {
                            if (notFlag)
                                throw new InvalidCommandException("Branch condition [Not] cannot be duplicated", rawCode);
                            embIdx = cIdx + 3;
                            CheckArgumentCount(BranchConditionType.ExistRegSection, embIdx);
                            cond = new BranchCondition(BranchConditionType.ExistRegSection, true, args[cIdx + 1], args[cIdx + 2]);
                        }
                        else if (condStr.Equals("NotExistRegKey", StringComparison.OrdinalIgnoreCase))
                        {
                            if (notFlag)
                                throw new InvalidCommandException("Branch condition [Not] cannot be duplicated", rawCode);
                            embIdx = cIdx + 4;
                            CheckArgumentCount(BranchConditionType.ExistRegKey, embIdx);
                            cond = new BranchCondition(BranchConditionType.ExistRegKey, true, args[cIdx + 1], args[cIdx + 2], args[cIdx + 3]);
                        }
                        else if (condStr.Equals("NotExistVar", StringComparison.OrdinalIgnoreCase))
                        {
                            if (notFlag)
                                throw new InvalidCommandException("Branch condition [Not] cannot be duplicated", rawCode);
                            embIdx = cIdx + 2;
                            CheckArgumentCount(BranchConditionType.ExistVar, embIdx);
                            cond = new BranchCondition(BranchConditionType.ExistVar, true, args[cIdx + 1]);
                        }
                    }
                }

                if (embIdx != -1 && cond != null)
                {
                    return (cond, embIdx);
                }
            }

            // BranchCondition - Compare series
            // <a1>,<Compare>,<a2>,<EmbCmd...> - at least 4 args is required
            if (cIdx + 3 < args.Count)
            {
                string condStr = args[cIdx + 1];
                BranchConditionType condType;

                if (condStr.Equals("Equal", StringComparison.OrdinalIgnoreCase) || condStr.Equals("==", StringComparison.OrdinalIgnoreCase))
                    condType = BranchConditionType.Equal;
                else if (condStr.Equals("EqualX", StringComparison.OrdinalIgnoreCase) || condStr.Equals("===", StringComparison.OrdinalIgnoreCase))
                    condType = BranchConditionType.EqualX;
                else if (condStr.Equals("Smaller", StringComparison.OrdinalIgnoreCase) || condStr.Equals("<", StringComparison.OrdinalIgnoreCase))
                    condType = BranchConditionType.Smaller;
                else if (condStr.Equals("Bigger", StringComparison.OrdinalIgnoreCase) || condStr.Equals(">", StringComparison.OrdinalIgnoreCase))
                    condType = BranchConditionType.Bigger;
                else if (condStr.Equals("SmallerEqual", StringComparison.OrdinalIgnoreCase) || condStr.Equals("<=", StringComparison.OrdinalIgnoreCase))
                    condType = BranchConditionType.SmallerEqual;
                else if (condStr.Equals("BiggerEqual", StringComparison.OrdinalIgnoreCase) || condStr.Equals(">=", StringComparison.OrdinalIgnoreCase))
                    condType = BranchConditionType.BiggerEqual;
                else if (condStr.Equals("NotEqual", StringComparison.OrdinalIgnoreCase) || // Deprecated
                    condStr.Equals("!=", StringComparison.OrdinalIgnoreCase)) // Keep != 
                {
                    if (notFlag)
                        throw new InvalidCommandException("Branch condition [Not] cannot be duplicated", rawCode);
                    notFlag = true;
                    condType = BranchConditionType.Equal;
                }
                else
                {
                    throw new InvalidCommandException($"Incorrect branch condition [{condStr}]", rawCode);
                }

                string compArg1 = args[cIdx];
                string compArg2 = args[cIdx + 2];
                BranchCondition cond = new BranchCondition(condType, notFlag, compArg1, compArg2);
                return (cond, cIdx + 3);
            }

            throw new InvalidCommandException("Incorrect branch condition", rawCode);
        }
        #endregion

        #region ParseCodeInfoIf, ForgeIfEmbedCommand
        public static bool StringContainsVariable(string str)
        {
            MatchCollection matches = Regex.Matches(str, Variables.VarKeyRegexContainsVariable, RegexOptions.Compiled | RegexOptions.CultureInvariant); // ABC%Joveler%
            bool sectionInParamMatch = Regex.IsMatch(str, Variables.VarKeyRegexContainsSectionInParams, RegexOptions.Compiled | RegexOptions.CultureInvariant); // #1
            bool sectionOutParamMatch = Regex.IsMatch(str, Variables.VarKeyRegexContainsSectionOutParams, RegexOptions.Compiled | RegexOptions.CultureInvariant); // #o1
            bool sectionLoopMatch = str.IndexOf("#c", StringComparison.OrdinalIgnoreCase) != -1; // #c
            bool sectionInParamCountMatch = str.IndexOf("#a", StringComparison.OrdinalIgnoreCase) != -1; // #a
            bool sectionOutParamCountMatch = str.IndexOf("#oa", StringComparison.OrdinalIgnoreCase) != -1; // #oa
            bool sectionReturnValueMatch = str.IndexOf("#r", StringComparison.OrdinalIgnoreCase) != -1; // #r

            return 0 < matches.Count ||
                   sectionInParamMatch || sectionOutParamMatch ||
                   sectionLoopMatch ||
                   sectionInParamCountMatch || sectionOutParamCountMatch ||
                   sectionReturnValueMatch;
        }

        public CodeInfo_If ParseCodeInfoIf(string rawCode, List<string> args, int lineIdx)
        {
            if (args.Count < 2)
                throw new InvalidCommandException("[If] must have form of [If],<Condition>,<Command>", rawCode);

            (BranchCondition cond, int skipArgs) = ParseBranchCondition(rawCode, args);
            CodeCommand embCmd = ForgeIfEmbedCommand(rawCode, args.Skip(skipArgs).ToList(), lineIdx);
            return new CodeInfo_If(cond, embCmd);
        }

        public CodeInfo_Else ParseCodeInfoElse(string rawCode, List<string> args, int lineIdx)
        {
            CodeCommand embCmd = ForgeIfEmbedCommand(rawCode, args, lineIdx); // Skip Else
            return new CodeInfo_Else(embCmd);
        }

        public CodeCommand ForgeIfEmbedCommand(string rawCode, List<string> args, int lineIdx)
        {
            CodeCommand embed = ParseStatementFromSlicedArgs(rawCode, args, lineIdx);
            return embed;
        }
        #endregion

        #region FoldBranchCodeBlock
        public static void FoldBranchCodeBlock(List<CodeCommand> codeList, out List<CodeCommand> foldedList)
        {
            bool elseFlag = false;
            foldedList = new List<CodeCommand>();

            for (int i = 0; i < codeList.Count; i++)
            {
                CodeCommand cmd = codeList[i];
                if (cmd.Type == CodeType.If)
                { // Change it to IfCompact, and parse Begin - End
                    if (cmd.Info is not CodeInfo_If info)
                        throw new InternalParserException($"Error while parsing command [{cmd.RawCode}]");

                    if (info.LinkParsed)
                        foldedList.Add(cmd);
                    else
                        i = ParseNestedIf(cmd, codeList, i, foldedList);

                    elseFlag = true;

                    FoldBranchCodeBlock(info.Link, out List<CodeCommand> newLinkList);
                    info.Link = newLinkList;
                }
                else if (cmd.Type == CodeType.Else) // SingleLine or MultiLine?
                { // Compile to ElseCompact
                    if (cmd.Info is not CodeInfo_Else info)
                        throw new InternalParserException($"Error while parsing command [{cmd.RawCode}]");

                    if (elseFlag)
                    {
                        if (info.LinkParsed)
                            foldedList.Add(cmd);
                        else
                            i = ParseNestedElse(cmd, codeList, i, foldedList, out elseFlag);

                        FoldBranchCodeBlock(info.Link, out List<CodeCommand> newLinkList);
                        info.Link = newLinkList;
                    }
                    else
                    {
                        throw new InvalidCodeCommandException("[Else] must be used after [If]", cmd);
                    }
                }
                else if (cmd.Type == CodeType.Begin || cmd.Type == CodeType.End)
                {
                    // Begin and End command must not affect or reset elseFlag
                    // And it must not be added to foldedList
                }
                else if (cmd.Type == CodeType.Comment)
                {
                    // Comment command must not reset elseFlag
                    // But it must be added to foldedList
                    foldedList.Add(cmd);
                }
                else
                {
                    // The other operands, reset elseFlag and just add them to foldedList.
                    elseFlag = false;
                    foldedList.Add(cmd);
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
            // Command compiledCmd; // Compiled If : IfCompact,Equal,%A%,B

            if (cmd.Info is not CodeInfo_If info)
                throw new InternalParserException("Invalid CodeInfo_If while processing nested [If]");

            newList.Add(cmd);

            // <Raw>
            // If,%A%,Equal,B,Echo,Success
            while (true)
            {
                if (info.Embed.Type == CodeType.If) // Nested If
                {
                    info.Link.Add(info.Embed);
                    info.LinkParsed = true;

                    info = info.Embed.Info as CodeInfo_If;
                    if (info == null)
                        throw new InternalParserException("Invalid CodeInfo_If while processing nested [If]");
                }
                else if (info.Embed.Type == CodeType.Begin) // Multiline If (Begin-End)
                {
                    // Find proper End
                    int endIdx = MatchBeginWithEnd(codeList, codeListIdx + 1);
                    if (endIdx == -1)
                        throw new InvalidCodeCommandException("[Begin] must be matched with [End]", cmd);

                    info.Link.AddRange(codeList.Skip(codeListIdx + 1).Take(endIdx - (codeListIdx + 1)));
                    info.LinkParsed = true;

                    return endIdx;
                }
                else if (info.Embed.Type == CodeType.Else || info.Embed.Type == CodeType.End || info.Embed.Type == CodeType.Comment) // Cannot come here!
                {
                    throw new InvalidCodeCommandException($"{info.Embed.Type} cannot be used with [If]", cmd);
                }
                else // Single-line If
                {
                    info.Link.Add(info.Embed);
                    info.LinkParsed = true;

                    return codeListIdx;
                }
            }
        }

        /// <summary>
        /// Parsed nested Else
        /// </summary>
        /// <returns>Return next command index</returns>
        private static int ParseNestedElse(CodeCommand cmd, List<CodeCommand> codeList, int codeListIdx, List<CodeCommand> newList, out bool elseFlag)
        {
            if (cmd.Info is not CodeInfo_Else info)
                throw new InternalParserException("Invalid CodeInfo_Else while processing nested [Else]");

            newList.Add(cmd);

            CodeCommand elseEmbCmd = info.Embed;
            if (elseEmbCmd.Type == CodeType.If) // Nested If
            {
                info.Link.Add(elseEmbCmd);
                info.LinkParsed = true;

                if (info.Embed.Info is not CodeInfo_If ifInfo)
                    throw new InternalParserException("Invalid CodeInfo_If while processing nested [If]");

                while (true)
                {
                    if (ifInfo.Embed.Type == CodeType.If) // Nested If
                    {
                        ifInfo.Link.Add(ifInfo.Embed);
                        ifInfo.LinkParsed = true;

                        ifInfo = ifInfo.Embed.Info as CodeInfo_If;
                        if (ifInfo == null)
                            throw new InternalParserException("Invalid CodeInfo_If while processing nested [If]");

                        ifInfo.LinkParsed = true;
                    }
                    else if (ifInfo.Embed.Type == CodeType.Begin) // Multiline If (Begin-End)
                    {
                        // Find proper End
                        int endIdx = MatchBeginWithEnd(codeList, codeListIdx + 1);
                        if (endIdx == -1)
                            throw new InvalidCodeCommandException("[Begin] must be matched with [End]", ifInfo.Embed);

                        ifInfo.Link.AddRange(codeList.Skip(codeListIdx + 1).Take(endIdx - (codeListIdx + 1)));
                        ifInfo.LinkParsed = true;

                        elseFlag = true;
                        return endIdx;
                    }
                    else if (ifInfo.Embed.Type == CodeType.Else || ifInfo.Embed.Type == CodeType.End) // Cannot come here!
                    {
                        ifInfo.Link.Add(ifInfo.Embed);
                        throw new InvalidCodeCommandException($"{info.Embed.Type} cannot be used with [If]", cmd);
                    }
                    else // Single-line If
                    {
                        ifInfo.Link.Add(ifInfo.Embed);
                        ifInfo.LinkParsed = true;

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
                    throw new InvalidCodeCommandException("[Begin] must be matched with [End]", cmd);

                info.Link.AddRange(codeList.Skip(codeListIdx + 1).Take(endIdx - codeListIdx - 1)); // Remove Begin and End
                info.LinkParsed = true;

                elseFlag = true;
                return endIdx;
            }
            else if (elseEmbCmd.Type == CodeType.Else || elseEmbCmd.Type == CodeType.End || elseEmbCmd.Type == CodeType.Comment)
            {
                info.Link.Add(info.Embed);
                throw new InvalidCodeCommandException($"{elseEmbCmd.Type} cannot be used with [Else]", cmd);
            }
            else // Normal codes
            {
                info.Link.Add(info.Embed);
                info.LinkParsed = true;

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
                        if (cmd.Info is not CodeInfo_If info)
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
                    if (cmd.Info is not CodeInfo_Else info)
                        throw new InternalParserException("Invalid CodeInfo_Else while matching [Begin] with [End]");

                    CodeCommand ifCmd = info.Embed;
                    if (ifCmd.Type == CodeType.If) // Nested If
                    {
                        while (true)
                        {
                            if (ifCmd.Info is not CodeInfo_If embedInfo)
                                throw new InternalParserException("Invalid CodeInfo_If while matching [Begin] with [End]");

                            if (embedInfo.Embed.Type == CodeType.If) // Nested If
                            {
                                // ifCmd = embedInfo.Embed;
                            }
                            else if (embedInfo.Embed.Type == CodeType.Begin)
                            {
                                // beginExist = true;
                                nestedBeginEnd++;
                                break;
                            }
                            else
                            {
                                break;
                            }
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
            if (finalizedWithEnd && nestedBeginEnd == 0)
                return codeListIdx;
            return -1;
        }
        #endregion
    }
}
