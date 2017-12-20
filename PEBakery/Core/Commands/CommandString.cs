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

using PEBakery.Exceptions;
using PEBakery.Helper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PEBakery.Core.Commands
{
    public static class CommandString
    {
        const long PB = 1024L * 1024L * 1024L * 1024L * 1024L;
        const long TB = 1024L * 1024L * 1024L * 1024L;
        const long GB = 1024L * 1024L * 1024L;
        const long MB = 1024L * 1024L;
        const long KB = 1024L;

        public static List<LogInfo> StrFormat(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_StrFormat));
            CodeInfo_StrFormat info = cmd.Info as CodeInfo_StrFormat;

            StrFormatType type = info.Type;
            switch (type)
            {
                case StrFormatType.IntToBytes:
                case StrFormatType.Bytes:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(StrFormatInfo_IntToBytes));
                        StrFormatInfo_IntToBytes subInfo = info.SubInfo as StrFormatInfo_IntToBytes;

                        string byteSizeStr = StringEscaper.Preprocess(s, subInfo.ByteSize);
                        if (!NumberHelper.ParseInt64(byteSizeStr, out long byteSize))
                            throw new ExecuteException($"[{byteSizeStr}] is not a valid integer");

                        if (byteSize < 0)
                            throw new ExecuteException($"[{byteSize}] must be positive integer");

                        string destStr = NumberHelper.ByteSizeToHumanReadableString(byteSize);

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, destStr);
                        logs.AddRange(varLogs);
                    }
                    break;
                case StrFormatType.BytesToInt:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(StrFormatInfo_BytesToInt));
                        StrFormatInfo_BytesToInt subInfo = info.SubInfo as StrFormatInfo_BytesToInt;

                        string humanReadableByteSizeStr = StringEscaper.Preprocess(s, subInfo.HumanReadableByteSize);
                        decimal dest = NumberHelper.HumanReadableStringToByteSize(humanReadableByteSizeStr);

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, decimal.Ceiling(dest).ToString());
                        logs.AddRange(varLogs);
                    }
                    break;
                case StrFormatType.Hex:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(StrFormatInfo_Hex));
                        StrFormatInfo_Hex subInfo = info.SubInfo as StrFormatInfo_Hex;

                        string intStr = StringEscaper.Preprocess(s, subInfo.Integer);
                        if (!NumberHelper.ParseInt32(intStr, out int intVal))
                            throw new ExecuteException($"[{intStr}] is not a valid integer");

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, intVal.ToString("X8"));
                        logs.AddRange(varLogs);
                    }
                    break;
                case StrFormatType.Ceil:
                case StrFormatType.Floor:
                case StrFormatType.Round:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(StrFormatInfo_CeilFloorRound));
                        StrFormatInfo_CeilFloorRound subInfo = info.SubInfo as StrFormatInfo_CeilFloorRound;

                        // subInfo.SizeVar;
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
                                throw new ExecuteException($"[{roundToStr}] is not a valid integer");
                        }

                        if (roundTo < 0)
                            throw new ExecuteException($"[{roundTo}] must be positive integer");

                        string srcIntStr = StringEscaper.Preprocess(s, subInfo.SizeVar);
                        if (!NumberHelper.ParseInt64(srcIntStr, out long srcInt))
                            throw new ExecuteException($"[{srcIntStr}] is not a valid integer");
                        long destInt;
                        if (type == StrFormatType.Ceil)
                        {
                            long remainder = srcInt % roundTo;
                            destInt = srcInt - remainder + roundTo;
                        }
                        else if (type == StrFormatType.Floor)
                        {
                            long remainder = srcInt % roundTo;
                            destInt = srcInt - remainder;
                        }
                        else // if (type == StrFormatType.Round)
                        {
                            long remainder = srcInt % roundTo;
                            if ((roundTo - 1) / 2 < remainder)
                                destInt = srcInt - remainder + roundTo;
                            else
                                destInt = srcInt - remainder;
                        }

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.SizeVar, destInt.ToString());
                        logs.AddRange(varLogs);
                    }
                    break;
                case StrFormatType.Date:
                    { // <yyyy-mmm-dd hh:nn am/pm> 
                        Debug.Assert(info.SubInfo.GetType() == typeof(StrFormatInfo_Date));
                        StrFormatInfo_Date subInfo = info.SubInfo as StrFormatInfo_Date;

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
                        Debug.Assert(info.SubInfo.GetType() == typeof(StrFormatInfo_Path));
                        StrFormatInfo_Path subInfo = info.SubInfo as StrFormatInfo_Path;

                        string srcStr = StringEscaper.Preprocess(s, subInfo.FilePath);
                        string destStr = string.Empty;

                        if (srcStr.Trim().Equals(string.Empty, StringComparison.Ordinal)) // Empty or Whitespace string
                        {
                            logs.Add(new LogInfo(LogState.Info, $"Source string [{srcStr}] is empty"));
                        }
                        else 
                        {
                            if (type == StrFormatType.FileName)
                            {
                                destStr = Path.GetFileName(srcStr);
                                logs.Add(new LogInfo(LogState.Success, $"Path [{srcStr}]'s file name is [{destStr}]"));
                            }
                            else if (type == StrFormatType.DirPath)
                            { // Does not includes Last Seperator
                                int bsIdx = srcStr.LastIndexOf('\\');
                                int sIdx = srcStr.LastIndexOf('/');

                                if (bsIdx != -1 && sIdx != -1)
                                { // Slash and BackSlash cannot exist at same time
                                    logs.Add(new LogInfo(LogState.Error, $"Path [{srcStr}] is invalid"));
                                    return logs;
                                }

                                if (bsIdx != -1)
                                { // Normal file path
                                    destStr = Path.GetDirectoryName(srcStr);
                                }
                                else
                                { // URL
                                    if (sIdx == -1)
                                        destStr = string.Empty;
                                    else
                                        destStr = srcStr.Substring(0, sIdx);
                                }

                                logs.Add(new LogInfo(LogState.Success, $"Path [{srcStr}]'s directory path is [{destStr}]"));
                            }
                            else if (type == StrFormatType.Path)
                            { // Includes Last Seperator - Default WB082 Behavior
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
                            }
                            else if (type == StrFormatType.Ext)
                            {
                                destStr = Path.GetExtension(srcStr);
                                logs.Add(new LogInfo(LogState.Success, $"Path [{srcStr}]'s extension is [{destStr}]"));
                            }
                        }

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, destStr);
                        logs.AddRange(varLogs);
                    }
                    break;
                case StrFormatType.PathCombine:
                    { // StrFormat,PathCombine,<DirPath>,<FileName>,<DestVar>
                        Debug.Assert(info.SubInfo.GetType() == typeof(StrFormatInfo_PathCombine));
                        StrFormatInfo_PathCombine subInfo = info.SubInfo as StrFormatInfo_PathCombine;

                        string dirPath = StringEscaper.Preprocess(s, subInfo.DirPath).Trim();
                        string fileName = StringEscaper.Preprocess(s, subInfo.FileName).Trim();

                        if (Regex.IsMatch(dirPath, @"^([a-zA-Z]:)$", RegexOptions.Compiled))
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
                    { // Why, why arithmetic is in StrFormat...
                        Debug.Assert(info.SubInfo.GetType() == typeof(StrFormatInfo_Arithmetic));
                        StrFormatInfo_Arithmetic subInfo = info.SubInfo as StrFormatInfo_Arithmetic;

                        string srcStr = StringEscaper.Preprocess(s, subInfo.DestVar);
                        if (!NumberHelper.ParseInt64(srcStr, out long src))
                            throw new ExecuteException($"[{srcStr}] is not a valid integer");

                        string operandStr = StringEscaper.Preprocess(s, subInfo.Integer);
                        if (!NumberHelper.ParseInt64(operandStr, out long operand))
                            throw new ExecuteException($"[{operandStr}] is not a valid integer");

                        long dest = src;
                        if (type == StrFormatType.Inc) // +
                            dest += operand;
                        else if (type == StrFormatType.Dec) // -
                            dest -= operand;
                        else if (type == StrFormatType.Mult) // *
                            dest *= operand;
                        else if (type == StrFormatType.Div) // /
                            dest /= operand;

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, dest.ToString());
                        logs.AddRange(varLogs);
                    }
                    break;
                case StrFormatType.Left:
                case StrFormatType.Right:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(StrFormatInfo_LeftRight));
                        StrFormatInfo_LeftRight subInfo = info.SubInfo as StrFormatInfo_LeftRight;

                        string srcStr = StringEscaper.Preprocess(s, subInfo.SrcStr);
                        string cutLenStr = StringEscaper.Preprocess(s, subInfo.CutLen);

                        if (!NumberHelper.ParseInt32(cutLenStr, out int cutLen))
                            throw new ExecuteException($"[{cutLenStr}] is not a valid integer");
                        if (cutLen < 0)
                            throw new ExecuteException($"[{cutLen}] must be positive integer");

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
                            logs.Add(new LogInfo(LogState.Ignore, $"[{cutLen}] is not valid index"));
                            logs.AddRange(Variables.SetVariable(s, subInfo.DestVar, string.Empty));
                        }
                    }
                    break;
                case StrFormatType.SubStr:
                    { // Index start from 1, not 0!
                        Debug.Assert(info.SubInfo.GetType() == typeof(StrFormatInfo_SubStr));
                        StrFormatInfo_SubStr subInfo = info.SubInfo as StrFormatInfo_SubStr;

                        string srcStr = StringEscaper.Preprocess(s, subInfo.SrcStr);
                        string startPosStr = StringEscaper.Preprocess(s, subInfo.StartPos);

                        if (!NumberHelper.ParseInt32(startPosStr, out int startPos))
                            throw new ExecuteException($"[{startPosStr}] is not a valid integer");
                        if (startPos <= 0)
                            throw new ExecuteException($"[{startPos}] must be positive integer");
                        string lenStr = StringEscaper.Preprocess(s, subInfo.Length);
                        if (!NumberHelper.ParseInt32(lenStr, out int len))
                            throw new ExecuteException($"[{lenStr}] is not a valid integer");
                        if (len <= 0)
                            throw new ExecuteException($"[{len}] must be positive integer");

                        // Error handling
                        if (srcStr.Length <= (startPos - 1))
                            throw new ExecuteException($"Start position [{startPos}] cannot be bigger than source string's length [{srcStr.Length}]");
                        if (srcStr.Length - (startPos - 1) < len)
                            throw new ExecuteException($"Length [{len}] cannot be bigger than [{srcStr.Length - startPos}]");

                        string destStr = srcStr.Substring(startPos - 1, len);

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, destStr);
                        logs.AddRange(varLogs);
                    }
                    break;
                case StrFormatType.Len:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(StrFormatInfo_Len));
                        StrFormatInfo_Len subInfo = info.SubInfo as StrFormatInfo_Len;

                        string srcStr = StringEscaper.Preprocess(s, subInfo.SrcStr);

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, srcStr.Length.ToString());
                        logs.AddRange(varLogs);
                    }
                    break;
                case StrFormatType.LTrim:
                case StrFormatType.RTrim:
                case StrFormatType.CTrim:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(StrFormatInfo_Trim));
                        StrFormatInfo_Trim subInfo = info.SubInfo as StrFormatInfo_Trim;

                        string srcStr = StringEscaper.Preprocess(s, subInfo.SrcStr);
                        string toTrim = StringEscaper.Preprocess(s, subInfo.ToTrim);

                        string destStr = string.Empty;
                        try
                        {
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
                                    throw new ExecuteException("No characters to trim");

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
                            logs.Add(new LogInfo(LogState.Error, $"[{toTrim}] is not valid index"));
                        }
                    }
                    break;
                case StrFormatType.NTrim:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(StrFormatInfo_NTrim));
                        StrFormatInfo_NTrim subInfo = info.SubInfo as StrFormatInfo_NTrim;

                        string srcStr = StringEscaper.Preprocess(s, subInfo.SrcStr);

                        Match match = Regex.Match(srcStr, @"([0-9]+)$", RegexOptions.Compiled);
                        string destStr;
                        if (match.Success)
                            destStr = srcStr.Substring(0, match.Index);
                        else
                            destStr = srcStr;

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, destStr);
                        logs.AddRange(varLogs);
                    }
                    break;
                case StrFormatType.UCase:
                case StrFormatType.LCase:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(StrFormatInfo_ULCase));
                        StrFormatInfo_ULCase subInfo = info.SubInfo as StrFormatInfo_ULCase;

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
                        Debug.Assert(info.SubInfo.GetType() == typeof(StrFormatInfo_Pos));
                        StrFormatInfo_Pos subInfo = info.SubInfo as StrFormatInfo_Pos;

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
                        Debug.Assert(info.SubInfo.GetType() == typeof(StrFormatInfo_Replace));
                        StrFormatInfo_Replace subInfo = info.SubInfo as StrFormatInfo_Replace;

                        string srcStr = StringEscaper.Preprocess(s, subInfo.SrcString);
                        string subStr = StringEscaper.Preprocess(s, subInfo.ToBeReplaced);
                        string newStr = StringEscaper.Preprocess(s, subInfo.ReplaceWith);

                        StringComparison comp = StringComparison.OrdinalIgnoreCase;
                        if (type == StrFormatType.ReplaceX)
                            comp = StringComparison.Ordinal;

                        string destStr = StringHelper.ReplaceEx(srcStr, subStr, newStr, comp);

                        /*
                        if (subStr.Equals(string.Empty, StringComparison.Ordinal))
                        {
                            destStr = srcStr;
                        }
                        else
                        {
                            StringBuilder b = new StringBuilder();
                            int startIdx = 0;
                            int newIdx = srcStr.Substring(startIdx).IndexOf(subStr, comp);
                            if (newIdx != -1)
                            {
                                while (newIdx != -1)
                                {
                                    string tmpStr = srcStr.Substring(startIdx, newIdx);
                                    b.Append(tmpStr);
                                    b.Append(newStr);

                                    startIdx += tmpStr.Length + subStr.Length;
                                    newIdx = srcStr.Substring(startIdx).IndexOf(subStr, comp);

                                    if (newIdx == -1)
                                    {
                                        b.Append(srcStr.Substring(startIdx));
                                        break;
                                    }
                                }

                                destStr = b.ToString();
                            }
                            else
                            {
                                destStr = srcStr;
                            }
                        }
                        */

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, destStr);
                        logs.AddRange(varLogs);
                    }
                    break;
                case StrFormatType.ShortPath:
                case StrFormatType.LongPath:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(StrFormatInfo_ShortLongPath));
                        StrFormatInfo_ShortLongPath subInfo = info.SubInfo as StrFormatInfo_ShortLongPath;

                        // Will be deprecated
                        logs.Add(new LogInfo(LogState.Warning, $"Command [StrFormatType,{info.Type}] is deprecated"));

                        string srcStr = StringEscaper.Preprocess(s, subInfo.SrcString);

                        string destStr;
                        if (type == StrFormatType.ShortPath)
                        {
                            destStr = FileHelper.GetShortPath(srcStr);
                        }
                        else
                        {
                            destStr = FileHelper.GetLongPath(srcStr);
                        }

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, destStr);
                        logs.AddRange(varLogs);
                    }
                    break;
                case StrFormatType.Split:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(StrFormatInfo_Split));
                        StrFormatInfo_Split subInfo = info.SubInfo as StrFormatInfo_Split;

                        string srcStr = StringEscaper.Preprocess(s, subInfo.SrcString);
                        string delimStr = StringEscaper.Preprocess(s, subInfo.Delimeter);
                        string idxStr = StringEscaper.Preprocess(s, subInfo.Index);
                        if (!NumberHelper.ParseInt32(idxStr, out int idx))
                            throw new ExecuteException($"[{idxStr}] is not a valid integer");

                        char[] delim = delimStr.ToCharArray();

                        List<LogInfo> varLogs = null;
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
                                logs.Add(new LogInfo(LogState.Info, $"Index [{idx}] out of bound [{slices.Length}]"));
                            }
                        }
                    }
                    break;
                // Error
                default:
                    throw new InvalidCodeCommandException($"Wrong StrFormatType [{type}]");
            }

            return logs;
        }
    }
}
