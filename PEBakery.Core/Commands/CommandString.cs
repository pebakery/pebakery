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

using PEBakery.Helper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace PEBakery.Core.Commands
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public static class CommandString
    {
        private const long PB = 1024L * 1024L * 1024L * 1024L * 1024L;
        private const long TB = 1024L * 1024L * 1024L * 1024L;
        private const long GB = 1024L * 1024L * 1024L;
        private const long MB = 1024L * 1024L;
        private const long KB = 1024L;

        public static List<LogInfo> StrFormat(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_StrFormat info = cmd.Info.Cast<CodeInfo_StrFormat>();

            StrFormatType type = info.Type;
            switch (type)
            {
                case StrFormatType.IntToBytes:
                case StrFormatType.Bytes:
                    {
                        StrFormatInfo_IntToBytes subInfo = info.SubInfo.Cast<StrFormatInfo_IntToBytes>();

                        string byteSizeStr = StringEscaper.Preprocess(s, subInfo.ByteSize);
                        if (!NumberHelper.ParseInt64(byteSizeStr, out long byteSize))
                            return LogInfo.LogErrorMessage(logs, $"[{byteSizeStr}] is not a valid integer");

                        if (byteSize < 0)
                            return LogInfo.LogErrorMessage(logs, $"[{byteSize}] must be a positive integer");

                        string destStr = NumberHelper.ByteSizeToSIUnit(byteSize);

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, destStr);
                        logs.AddRange(varLogs);
                    }
                    break;
                case StrFormatType.BytesToInt:
                    {
                        StrFormatInfo_BytesToInt subInfo = info.SubInfo.Cast<StrFormatInfo_BytesToInt>();

                        string humanReadableByteSizeStr = StringEscaper.Preprocess(s, subInfo.HumanReadableByteSize);
                        decimal dest = NumberHelper.HumanReadableStringToByteSize(humanReadableByteSizeStr);

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, decimal.Ceiling(dest).ToString(CultureInfo.InvariantCulture));
                        logs.AddRange(varLogs);
                    }
                    break;
                case StrFormatType.Hex:
                    {
                        StrFormatInfo_Hex subInfo = info.SubInfo.Cast<StrFormatInfo_Hex>();

                        string intStr = StringEscaper.Preprocess(s, subInfo.Integer);
                        if (!NumberHelper.ParseInt32(intStr, out int intVal))
                            return LogInfo.LogErrorMessage(logs, $"[{intStr}] is not a valid integer");

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, intVal.ToString("X8"));
                        logs.AddRange(varLogs);
                    }
                    break;
                case StrFormatType.Ceil:
                case StrFormatType.Floor:
                case StrFormatType.Round:
                    {
                        StrFormatInfo_CeilFloorRound subInfo = info.SubInfo.Cast<StrFormatInfo_CeilFloorRound>();

                        string roundToStr = StringEscaper.Preprocess(s, subInfo.RoundTo);

                        // Is roundToStr number?
                        if (!NumberHelper.ParseInt64(roundToStr, out long roundTo))
                        { // Is roundToStr is one of K, M, G, T, P?
                            if (roundToStr.Equals("K", StringComparison.OrdinalIgnoreCase))
                                roundTo = KB;
                            else if (roundToStr.Equals("M", StringComparison.OrdinalIgnoreCase))
                                roundTo = MB;
                            else if (roundToStr.Equals("G", StringComparison.OrdinalIgnoreCase))
                                roundTo = GB;
                            else if (roundToStr.Equals("T", StringComparison.OrdinalIgnoreCase))
                                roundTo = TB;
                            else if (roundToStr.Equals("P", StringComparison.OrdinalIgnoreCase))
                                roundTo = PB;
                            else
                                return LogInfo.LogErrorMessage(logs, $"[{roundToStr}] is not a valid integer");
                        }

                        if (roundTo < 0)
                            return LogInfo.LogErrorMessage(logs, $"[{roundTo}] must be a positive integer");

                        string srcIntStr = StringEscaper.Preprocess(s, subInfo.SizeVar);
                        if (!NumberHelper.ParseInt64(srcIntStr, out long srcInt))
                            return LogInfo.LogErrorMessage(logs, $"[{srcIntStr}] is not a valid integer");
                        long destInt;
                        switch (type)
                        {
                            case StrFormatType.Ceil:
                                {
                                    long remainder = srcInt % roundTo;
                                    destInt = srcInt - remainder + roundTo;
                                    break;
                                }
                            case StrFormatType.Floor:
                                {
                                    long remainder = srcInt % roundTo;
                                    destInt = srcInt - remainder;
                                    break;
                                }
                            case StrFormatType.Round:
                                {
                                    long remainder = srcInt % roundTo;
                                    if ((roundTo - 1) / 2 < remainder)
                                        destInt = srcInt - remainder + roundTo;
                                    else
                                        destInt = srcInt - remainder;
                                    break;
                                }
                            default:
                                throw new InternalException($"Internal Logic Error at StrFormat,{type}");
                        }

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.SizeVar, destInt.ToString());
                        logs.AddRange(varLogs);
                    }
                    break;
                case StrFormatType.Date:
                    { // <yyyy-mmm-dd hh:nn am/pm> 
                        StrFormatInfo_Date subInfo = info.SubInfo.Cast<StrFormatInfo_Date>();

                        string formatStr = StringEscaper.Preprocess(s, subInfo.FormatString);

                        string destStr = DateTime.Now.ToString(formatStr, CultureInfo.InvariantCulture);

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, destStr);
                        logs.AddRange(varLogs);
                    }
                    break;
                case StrFormatType.FileName:
                case StrFormatType.DirPath:
                case StrFormatType.Path:
                case StrFormatType.Ext:
                    {
                        StrFormatInfo_Path subInfo = info.SubInfo.Cast<StrFormatInfo_Path>();

                        string srcStr = StringEscaper.Preprocess(s, subInfo.FilePath);
                        string destStr = string.Empty;

                        if (srcStr.Trim().Equals(string.Empty, StringComparison.Ordinal)) // Empty or Whitespace string
                        {
                            logs.Add(new LogInfo(LogState.Info, $"Source string [{srcStr}] is empty"));
                        }
                        else
                        {
                            switch (type)
                            {
                                case StrFormatType.FileName:
                                    destStr = Path.GetFileName(srcStr);
                                    logs.Add(new LogInfo(LogState.Success, $"Path [{srcStr}]'s file name is [{destStr}]"));
                                    break;
                                case StrFormatType.DirPath:
                                case StrFormatType.Path: // Includes Last Seperator - Default WB082 Behavior
                                    int bsIdx = srcStr.LastIndexOf('\\');
                                    int sIdx = srcStr.LastIndexOf('/');

                                    if (bsIdx != -1 && sIdx != -1)
                                    { // Slash and BackSlash cannot exist at same time
                                        logs.Add(new LogInfo(LogState.Error, $"Path [{srcStr}] is invalid"));
                                        return logs;
                                    }

                                    if (bsIdx != -1)
                                    { // Normal file path
                                        // destStr = Path.GetDirectoryName(srcStr) + '\\';
                                        destStr = srcStr.Substring(0, bsIdx + 1);
                                    }
                                    else
                                    { // URL
                                        if (sIdx == -1)
                                            destStr = string.Empty;
                                        else
                                            destStr = srcStr.Substring(0, sIdx + 1);
                                    }

                                    logs.Add(new LogInfo(LogState.Success, $"Path [{srcStr}]'s directory path is [{destStr}]"));
                                    break;
                                case StrFormatType.Ext:
                                    destStr = Path.GetExtension(srcStr);
                                    logs.Add(new LogInfo(LogState.Success, $"Path [{srcStr}]'s extension is [{destStr}]"));
                                    break;
                                default:
                                    throw new InternalException($"Internal Logic Error at StrFormat,{type}");
                            }
                        }

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, destStr);
                        logs.AddRange(varLogs);
                    }
                    break;
                case StrFormatType.PathCombine:
                    { // StrFormat,PathCombine,<DirPath>,<FileName>,<DestVar>
                        StrFormatInfo_PathCombine subInfo = info.SubInfo.Cast<StrFormatInfo_PathCombine>();

                        string dirPath = StringEscaper.Preprocess(s, subInfo.DirPath).Trim();
                        string fileName = StringEscaper.Preprocess(s, subInfo.FileName).Trim();

                        if (Regex.IsMatch(dirPath, @"^([a-zA-Z]:)$", RegexOptions.Compiled | RegexOptions.CultureInvariant))
                            dirPath = dirPath + @"\";

                        string destStr = Path.Combine(dirPath, fileName);

                        logs.Add(new LogInfo(LogState.Success, $"Path [{dirPath}] and [{fileName}] combined into [{destStr}]"));

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, destStr);
                        logs.AddRange(varLogs);
                    }
                    break;
                case StrFormatType.Inc:
                case StrFormatType.Dec:
                case StrFormatType.Mult:
                case StrFormatType.Div:
                    {
                        StrFormatInfo_Arithmetic subInfo = info.SubInfo.Cast<StrFormatInfo_Arithmetic>();

                        string operandStr = StringEscaper.Preprocess(s, subInfo.Integer);
                        if (!NumberHelper.ParseInt64(operandStr, out long operand))
                            return LogInfo.LogErrorMessage(logs, $"[{operandStr}] is not a valid integer");

                        string destStr;
                        string srcStr = StringEscaper.Preprocess(s, subInfo.DestVar);
                        if (NumberHelper.ParseInt64(srcStr, out long src))
                        { // Integer (Discouraged - Use Math,Add/Sub/Mul/Div/IntDiv instead)
                            long dest = src;
                            if (type == StrFormatType.Inc)
                                dest += operand;
                            else if (type == StrFormatType.Dec)
                                dest -= operand;
                            else if (type == StrFormatType.Mult)
                                dest *= operand;
                            else if (type == StrFormatType.Div)
                                dest /= operand;

                            destStr = dest.ToString();
                        }
                        else if (srcStr.Length == 1 && (type == StrFormatType.Inc || type == StrFormatType.Dec))
                        { // Letter
                            bool upper = StringHelper.IsUpperAlphabet(srcStr[0]);
                            bool lower = StringHelper.IsLowerAlphabet(srcStr[0]);
                            if (upper == false && lower == false)
                            {
                                logs.Add(new LogInfo(LogState.Error, $"[{srcStr}] is not a valid integer nor drive letter"));
                                return logs;
                            }

                            char dest = srcStr[0];
                            if (type == StrFormatType.Inc)
                                dest = (char)(dest + operand);
                            else if (type == StrFormatType.Dec)
                                dest = (char)(dest - operand);

                            if (upper && !StringHelper.IsUpperAlphabet(dest) ||
                                lower && !StringHelper.IsLowerAlphabet(dest))
                                return LogInfo.LogErrorMessage(logs, "Result is not a valid drive letter");

                            destStr = dest.ToString();
                        }
                        else
                        {
                            return LogInfo.LogErrorMessage(logs, $"[{srcStr}] is not a valid integer");
                        }

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, destStr);
                        logs.AddRange(varLogs);
                    }
                    break;
                case StrFormatType.Left:
                case StrFormatType.Right:
                    {
                        StrFormatInfo_LeftRight subInfo = info.SubInfo.Cast<StrFormatInfo_LeftRight>();

                        string srcStr = StringEscaper.Preprocess(s, subInfo.SrcStr);
                        string cutLenStr = StringEscaper.Preprocess(s, subInfo.Count);

                        if (!NumberHelper.ParseInt32(cutLenStr, out int cutLen))
                            return LogInfo.LogErrorMessage(logs, $"[{cutLenStr}] is not a valid integer");
                        if (cutLen < 0)
                            return LogInfo.LogErrorMessage(logs, $"[{cutLen}] must be a positive integer");

                        string destStr = string.Empty;
                        try
                        {
                            if (type == StrFormatType.Left)
                            {
                                if (cutLen <= srcStr.Length)
                                    destStr = srcStr.Substring(0, cutLen);
                                else
                                    destStr = srcStr;
                            }
                            else if (type == StrFormatType.Right)
                            {
                                if (cutLen <= srcStr.Length)
                                    destStr = srcStr.Substring(srcStr.Length - cutLen, cutLen);
                                else
                                    destStr = srcStr;
                            }

                            logs.AddRange(Variables.SetVariable(s, subInfo.DestVar, destStr));
                        }
                        catch (ArgumentOutOfRangeException)
                        { // Correct WB082 behavior : Not error, but just empty string
                            logs.Add(new LogInfo(LogState.Ignore, $"[{cutLen}] is not a valid index"));
                            logs.AddRange(Variables.SetVariable(s, subInfo.DestVar, string.Empty));
                        }
                    }
                    break;
                case StrFormatType.Mid:
                    { // Index start from 1, not 0!
                        StrFormatInfo_Mid subInfo = info.SubInfo.Cast<StrFormatInfo_Mid>();

                        string srcStr = StringEscaper.Preprocess(s, subInfo.SrcStr);
                        string startPosStr = StringEscaper.Preprocess(s, subInfo.StartPos);

                        if (!NumberHelper.ParseInt32(startPosStr, out int startPos))
                            return LogInfo.LogErrorMessage(logs, $"[{startPosStr}] is not a valid integer");
                        if (startPos <= 0)
                            return LogInfo.LogErrorMessage(logs, $"[{startPos}] must be a positive integer");
                        string lenStr = StringEscaper.Preprocess(s, subInfo.Length);
                        if (!NumberHelper.ParseInt32(lenStr, out int len))
                            return LogInfo.LogErrorMessage(logs, $"[{lenStr}] is not a valid integer");
                        if (len <= 0)
                            return LogInfo.LogErrorMessage(logs, $"[{len}] must be a positive integer");

                        // Error handling
                        if (srcStr.Length <= startPos - 1)
                            return LogInfo.LogErrorMessage(logs, $"Start position [{startPos}] cannot be bigger than source string's length [{srcStr.Length}]");
                        if (srcStr.Length - (startPos - 1) < len)
                            return LogInfo.LogErrorMessage(logs, $"Length [{len}] cannot be bigger than [{srcStr.Length - startPos}]");

                        string destStr = srcStr.Substring(startPos - 1, len);

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, destStr);
                        logs.AddRange(varLogs);
                    }
                    break;
                case StrFormatType.Len:
                    {
                        StrFormatInfo_Len subInfo = info.SubInfo.Cast<StrFormatInfo_Len>();

                        string srcStr = StringEscaper.Preprocess(s, subInfo.SrcStr);

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, srcStr.Length.ToString());
                        logs.AddRange(varLogs);
                    }
                    break;
                case StrFormatType.LTrim:
                case StrFormatType.RTrim:
                case StrFormatType.CTrim:
                    {
                        StrFormatInfo_Trim subInfo = info.SubInfo.Cast<StrFormatInfo_Trim>();

                        string srcStr = StringEscaper.Preprocess(s, subInfo.SrcStr);
                        string toTrim = StringEscaper.Preprocess(s, subInfo.ToTrim);

                        try
                        {
                            string destStr;
                            if (type == StrFormatType.LTrim) // string.Substring
                            {
                                if (!NumberHelper.ParseInt32(toTrim, out int cutLen))
                                    logs.Add(new LogInfo(LogState.Error, $"[{toTrim}] is not a valid integer"));

                                // Error handling
                                if (srcStr.Length < cutLen)
                                    cutLen = srcStr.Length;
                                else if (cutLen < 0)
                                    cutLen = 0;

                                destStr = srcStr.Substring(cutLen);
                            }
                            else if (type == StrFormatType.RTrim) // string.Substring
                            {
                                if (!NumberHelper.ParseInt32(toTrim, out int cutLen))
                                    logs.Add(new LogInfo(LogState.Error, $"[{toTrim}] is not a valid integer"));

                                // Error handling
                                if (srcStr.Length < cutLen)
                                    cutLen = srcStr.Length;
                                else if (cutLen < 0)
                                    cutLen = 0;

                                destStr = srcStr.Substring(0, srcStr.Length - cutLen);
                            }
                            else if (type == StrFormatType.CTrim) // string.Trim
                            {
                                if (toTrim.Length == 0)
                                    return LogInfo.LogErrorMessage(logs, "No characters to trim");

                                char[] chArr = toTrim.ToCharArray();
                                destStr = srcStr.Trim(chArr);
                            }
                            else
                            {
                                throw new InternalException("Internal Logic Error at StrFormat,Trim");
                            }

                            List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVarName, destStr);
                            logs.AddRange(varLogs);
                        }
                        catch (ArgumentOutOfRangeException)
                        {
                            logs.Add(new LogInfo(LogState.Error, $"[{toTrim}] is not a valid index"));
                        }
                    }
                    break;
                case StrFormatType.NTrim:
                    {
                        StrFormatInfo_NTrim subInfo = info.SubInfo.Cast<StrFormatInfo_NTrim>();

                        string srcStr = StringEscaper.Preprocess(s, subInfo.SrcStr);

                        Match m = Regex.Match(srcStr, @"([0-9]+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
                        var destStr = m.Success ? srcStr.Substring(0, m.Index) : srcStr;

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, destStr);
                        logs.AddRange(varLogs);
                    }
                    break;
                case StrFormatType.UCase:
                case StrFormatType.LCase:
                    {
                        StrFormatInfo_ULCase subInfo = info.SubInfo.Cast<StrFormatInfo_ULCase>();

                        string srcStr = StringEscaper.Preprocess(s, subInfo.SrcStr);

                        string destStr;
                        if (type == StrFormatType.UCase)
                            destStr = srcStr.ToUpper(CultureInfo.InvariantCulture);
                        else if (type == StrFormatType.LCase)
                            destStr = srcStr.ToLower(CultureInfo.InvariantCulture);
                        else
                            throw new InternalException("Internal Logic Error at StrFormat,ULCase");

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, destStr);
                        logs.AddRange(varLogs);
                    }
                    break;
                case StrFormatType.Pos:
                case StrFormatType.PosX:
                    {
                        StrFormatInfo_Pos subInfo = info.SubInfo.Cast<StrFormatInfo_Pos>();

                        string srcStr = StringEscaper.Preprocess(s, subInfo.SrcStr);
                        string subStr = StringEscaper.Preprocess(s, subInfo.SubStr);

                        StringComparison comp = StringComparison.OrdinalIgnoreCase;
                        if (type == StrFormatType.PosX)
                            comp = StringComparison.Ordinal;

                        // 0 if not found
                        int idx = 0;
                        if (!subStr.Equals(string.Empty, StringComparison.Ordinal))
                            idx = srcStr.IndexOf(subStr, comp) + 1;

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, idx.ToString());
                        logs.AddRange(varLogs);
                    }
                    break;
                case StrFormatType.Replace:
                case StrFormatType.ReplaceX:
                    {
                        StrFormatInfo_Replace subInfo = info.SubInfo.Cast<StrFormatInfo_Replace>();

                        string srcStr = StringEscaper.Preprocess(s, subInfo.SrcStr);
                        string subStr = StringEscaper.Preprocess(s, subInfo.SearchStr);
                        string newStr = StringEscaper.Preprocess(s, subInfo.ReplaceStr);

                        StringComparison comp = StringComparison.OrdinalIgnoreCase;
                        if (type == StrFormatType.ReplaceX)
                            comp = StringComparison.Ordinal;

                        string destStr = StringHelper.ReplaceEx(srcStr, subStr, newStr, comp);

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, destStr);
                        logs.AddRange(varLogs);
                    }
                    break;
                case StrFormatType.ShortPath:
                case StrFormatType.LongPath:
                    { // Will be deprecated
                        StrFormatInfo_ShortLongPath subInfo = info.SubInfo.Cast<StrFormatInfo_ShortLongPath>();

                        logs.Add(new LogInfo(LogState.Warning, $"Command [StrFormatType,{info.Type}] is deprecated"));

                        string srcStr = StringEscaper.Preprocess(s, subInfo.SrcStr);

                        string? destStr;
                        if (type == StrFormatType.ShortPath)
                            destStr = FileHelper.GetShortPath(srcStr);
                        else
                            destStr = FileHelper.GetLongPath(srcStr);

                        // GetShortPathName / GetLongPathName failed
                        if (destStr == null)
                            destStr = srcStr;

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, destStr);
                        logs.AddRange(varLogs);
                    }
                    break;
                case StrFormatType.Split:
                    {
                        StrFormatInfo_Split subInfo = info.SubInfo.Cast<StrFormatInfo_Split>();

                        string srcStr = StringEscaper.Preprocess(s, subInfo.SrcStr);
                        string delimStr = StringEscaper.Preprocess(s, subInfo.Delimiter);
                        string idxStr = StringEscaper.Preprocess(s, subInfo.Index);
                        if (!NumberHelper.ParseInt32(idxStr, out int idx))
                            return LogInfo.LogErrorMessage(logs, $"[{idxStr}] is not a valid integer");

                        char[] delim = delimStr.ToCharArray();

                        List<LogInfo> varLogs;
                        if (idx == 0)
                        {
                            int delimCount = srcStr.Split(delim).Length;
                            logs.Add(new LogInfo(LogState.Success, $"String [{srcStr}] is split to [{delimCount}] strings."));
                            varLogs = Variables.SetVariable(s, subInfo.DestVar, delimCount.ToString());
                            logs.AddRange(varLogs);
                        }
                        else
                        {
                            string[] slices = srcStr.Split(delim);
                            if (idx - 1 < slices.Length)
                            {
                                string destStr = slices[idx - 1];
                                logs.Add(new LogInfo(LogState.Success, $"String [{srcStr}]'s split index [{idx}] is [{destStr}]"));
                                varLogs = Variables.SetVariable(s, subInfo.DestVar, destStr);
                                logs.AddRange(varLogs);
                            }
                            else
                            {
                                logs.Add(new LogInfo(LogState.Info, $"Index [{idx}] out of bounds [{slices.Length}]"));
                            }
                        }
                    }
                    break;
                case StrFormatType.PadLeft:
                case StrFormatType.PadRight:
                    {
                        StrFormatInfo_Pad subInfo = info.SubInfo.Cast<StrFormatInfo_Pad>();

                        string srcStr = StringEscaper.Preprocess(s, subInfo.SrcStr);
                        string totalWidthStr = StringEscaper.Preprocess(s, subInfo.TotalWidth);
                        string padCharStr = StringEscaper.Preprocess(s, subInfo.PadChar);

                        if (!NumberHelper.ParseInt32(totalWidthStr, out int totalWidth))
                            return LogInfo.LogErrorMessage(logs, $"[{totalWidthStr}] is not a valid integer");
                        if (totalWidth < 0)
                            return LogInfo.LogErrorMessage(logs, $"[{totalWidth}] must be a positive integer");

                        if (padCharStr.Length != 1)
                            return LogInfo.LogErrorMessage(logs, $"Padding character [{padCharStr}] should be one character");
                        char padChar = padCharStr[0];

                        string destStr = type == StrFormatType.PadLeft ? srcStr.PadLeft(totalWidth, padChar) : srcStr.PadRight(totalWidth, padChar);

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, destStr);
                        logs.AddRange(varLogs);
                    }
                    break;
                // Error
                default:
                    throw new InternalException("Internal Logic Error at CommandString.StrFormat");
            }

            return logs;
        }
    }
}
