using PEBakery.Core;
using PEBakery.Exceptions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PEBakery.Core
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

            CodeInfo_StrFormat info = cmd.Info as CodeInfo_StrFormat;
            if (info == null)
                throw new InvalidCodeCommandException("Command [StrFormat] should have [CodeInfo_StrFormat]", cmd);

            StrFormatType type = info.Type;

            switch (type)
            {
                case StrFormatType.Bytes:
                    {
                        StrFormatInfo_Bytes subInfo = info.SubInfo as StrFormatInfo_Bytes;
                        if (subInfo == null)
                            throw new InvalidCodeCommandException($"Command [StrFormat,{info.Type}] should have [StrFormatInfo_Bytes]", cmd);

                        string byteSizeStr = StringEscaper.Preprocess(s, subInfo.ByteSize);
                        if (long.TryParse(byteSizeStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out long byteSize) == false)
                            throw new InvalidCodeCommandException($"[{byteSizeStr}] is not valid integer", cmd);

                        if (byteSize < 0)
                            throw new InvalidCodeCommandException($"[{byteSize}] must be positive integer", cmd);

                        string destStr = string.Empty;
                        if (PB <= byteSize)
                            destStr = $"{((decimal)byteSize / PB):0.###}PB";
                        else if (TB <= byteSize)
                            destStr = $"{((decimal)byteSize / TB):0.###}TB";
                        else if (GB <= byteSize)
                            destStr = $"{((decimal)byteSize / GB):0.###}GB";
                        else if (MB <= byteSize)
                            destStr = $"{((decimal)byteSize / MB):0.###}MB";
                        else
                            destStr = $"{((decimal)byteSize / KB):0.###}KB";

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVarName, destStr);
                        logs.AddRange(LogInfo.AddCommand(varLogs, cmd));
                    }
                    break;
                case StrFormatType.Ceil:
                case StrFormatType.Floor:
                case StrFormatType.Round:
                    {
                        StrFormatInfo_CeilFloorRound subInfo = info.SubInfo as StrFormatInfo_CeilFloorRound;
                        if (subInfo == null)
                            throw new InvalidCodeCommandException($"Command [StrFormat,{info.Type}] should have [StrFormatInfo_CeilFloorRound]", cmd);

                        // subInfo.SizeVar;
                        string roundToStr = StringEscaper.Preprocess(s, subInfo.RoundTo);
                        // Is roundToStr number?
                        if (long.TryParse(roundToStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out long roundTo) == false)
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
                                throw new InvalidCodeCommandException($"[{roundToStr}] is not valid integer", cmd);
                        }
                            
                        if (roundTo < 0)
                            throw new InvalidCodeCommandException($"[{roundTo}] must be positive integer", cmd);

                        string srcIntStr = StringEscaper.Preprocess(s, subInfo.SizeVar);
                        if (long.TryParse(srcIntStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out long srcInt) == false)
                            throw new InvalidCodeCommandException($"[{srcIntStr}] is not valid integer", cmd);
                        long destInt;
                        if (type == StrFormatType.Ceil)
                        {
                            long remainder = srcInt % roundTo;
                            destInt = srcInt - remainder;
                        }
                        else if (type == StrFormatType.Floor)
                        {
                            long remainder = srcInt % roundTo;
                            destInt = srcInt - remainder + roundTo;
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
                        logs.AddRange(LogInfo.AddCommand(varLogs, cmd));
                    }
                    break;
                case StrFormatType.Date:
                    break;
                case StrFormatType.FileName:
                case StrFormatType.DirPath:
                case StrFormatType.Path:
                case StrFormatType.Ext:
                    {
                        StrFormatInfo_Path subInfo = info.SubInfo as StrFormatInfo_Path;
                        if (subInfo == null)
                            throw new InvalidCodeCommandException($"Command [StrFormat,{info.Type}] should have [StrFormatInfo_Path]", cmd);

                        string srcStr = StringEscaper.Preprocess(s, subInfo.FilePath);

                        string destStr = string.Empty;
                        if (type == StrFormatType.FileName)
                        {
                            destStr = Path.GetFileName(srcStr);
                        }
                        else if (type == StrFormatType.DirPath || type == StrFormatType.Path)
                        {
                            destStr = Path.GetDirectoryName(srcStr);
                        }
                        else if (type == StrFormatType.Ext)
                        {
                            destStr = Path.GetExtension(srcStr);
                        }

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVarName, destStr);
                        logs.AddRange(LogInfo.AddCommand(varLogs, cmd));
                    }
                    break;
                case StrFormatType.Inc:
                case StrFormatType.Dec:
                case StrFormatType.Mult:
                case StrFormatType.Div:
                    break;
                case StrFormatType.Left:
                case StrFormatType.Right:
                    break;
                case StrFormatType.SubStr:
                    break;
                case StrFormatType.Len:
                    break;
                case StrFormatType.LTrim:
                case StrFormatType.RTrim:
                case StrFormatType.CTrim:
                    {
                        StrFormatInfo_Trim subInfo = info.SubInfo as StrFormatInfo_Trim;
                        if (subInfo == null)
                            throw new InvalidCodeCommandException($"Command [StrFormat,{info.Type}] should have [StrFormatInfo_Trim]", cmd);

                        string srcStr = StringEscaper.Preprocess(s, subInfo.SrcString);
                        string toTrim = StringEscaper.Preprocess(s, subInfo.ToTrim);

                        string destStr = string.Empty;
                        try
                        {
                            if (type == StrFormatType.LTrim) // string.Substring
                            {
                                if (int.TryParse(toTrim, NumberStyles.Integer, CultureInfo.InvariantCulture, out int cutLen) == false)
                                    throw new InvalidCodeCommandException($"[{toTrim}] is not valid integer", cmd);

                                destStr = srcStr.Substring(cutLen);
                            }
                            else if (type == StrFormatType.RTrim) // string.Substring
                            {
                                if (int.TryParse(toTrim, NumberStyles.Integer, CultureInfo.InvariantCulture, out int cutLen) == false)
                                    throw new InvalidCodeCommandException($"[{toTrim}] is not valid integer", cmd);

                                destStr = srcStr.Substring(0, srcStr.Length - cutLen);
                            }
                            else if (type == StrFormatType.CTrim) // string.Trim
                            {
                                char[] chArr = toTrim.ToCharArray();
                                destStr = srcStr.Trim(chArr);
                            }
                            else if (type == StrFormatType.NTrim) // string.Substring
                            {
                                if (int.TryParse(toTrim, NumberStyles.Integer, CultureInfo.InvariantCulture, out int cutLen) == false)
                                    throw new InvalidCodeCommandException($"[{toTrim}] is not valid integer", cmd);

                                Match match = Regex.Match(srcStr, @"([0-9]+)$", RegexOptions.Compiled);
                                if (match.Success)
                                    destStr = srcStr.Substring(0, match.Index);
                                else
                                    destStr = srcStr;
                            }

                            List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVarName, destStr);
                            logs.AddRange(LogInfo.AddCommand(varLogs, cmd));
                        }
                        catch (ArgumentOutOfRangeException)
                        {
                            throw new InvalidCodeCommandException($"[{toTrim}] is not valid index", cmd);
                        }
                    }
                    break;
                case StrFormatType.NTrim:
                    {
                        StrFormatInfo_NTrim subInfo = info.SubInfo as StrFormatInfo_NTrim;
                        if (subInfo == null)
                            throw new InvalidCodeCommandException($"Command [StrFormat,{info.Type}] should have [StrFormatInfo_NTrim]", cmd);

                        string srcStr = StringEscaper.Preprocess(s, subInfo.SrcString);

                        Match match = Regex.Match(srcStr, @"([0-9]+)$", RegexOptions.Compiled);
                        string destStr = srcStr.Substring(0, match.Index);

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVarName, destStr);
                        logs.AddRange(LogInfo.AddCommand(varLogs, cmd));
                    }
                    break;
                case StrFormatType.Pos:
                    break;
                case StrFormatType.Replace:
                case StrFormatType.ReplaceX:
                    break;
                case StrFormatType.ShortPath:
                case StrFormatType.LongPath:
                    break;
                case StrFormatType.Split:
                    break;
                // Error
                default:
                    throw new InvalidCodeCommandException($"Wrong StrFormatType [{info.Type}]");
            }

            return logs;
        }
    }
}
