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
                    break;
                case StrFormatType.Ceil:
                case StrFormatType.Floor:
                case StrFormatType.Round:
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
                            foreach (LogInfo log in varLogs)
                                logs.Add(LogInfo.AddCommand(log, cmd));
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
                                destStr = srcStr.Substring(0, match.Index);
                            }

                            List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVarName, destStr);
                            foreach (LogInfo log in varLogs)
                                logs.Add(LogInfo.AddCommand(log, cmd));
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
                        foreach (LogInfo log in varLogs)
                            logs.Add(LogInfo.AddCommand(log, cmd));
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
