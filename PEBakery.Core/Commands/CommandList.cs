/*
    Copyright (C) 2018-2022 Hajin Jang
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

using NaturalSort.Extension;
using PEBakery.Helper;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PEBakery.Core.Commands
{
    public static class CommandList
    {
        /// <summary>
        /// PEBakery uses 1-based index for concated list
        /// </summary>
        /// <param name="s"></param>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public static List<LogInfo> List(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();
            CodeInfo_List info = (CodeInfo_List)cmd.Info;

            string listStr = string.Empty;
            string listVar = info.SubInfo.ListVar;
            if (Variables.ContainsKey(s, listVar) == true)
                listStr = StringEscaper.Preprocess(s, listVar);

            ListType type = info.Type;
            string delimiter = "|";
            switch (type)
            {
                case ListType.Get:
                    {
                        ListInfo_Get subInfo = (ListInfo_Get)info.SubInfo;

                        string indexStr = StringEscaper.Preprocess(s, subInfo.Index);

                        if (!NumberHelper.ParseInt32(indexStr, out int index))
                            return LogInfo.LogErrorMessage(logs, $"[{indexStr}] is not a valid positive integer");
                        if (index <= 0)
                            return LogInfo.LogErrorMessage(logs, $"[{indexStr}] is not a valid positive integer");

                        if (subInfo.Delim != null)
                            delimiter = StringEscaper.Preprocess(s, subInfo.Delim);

                        List<string> list = StringEscaper.UnpackListStr(listStr, delimiter);
                        string destStr = list[index - 1];

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, destStr);
                        logs.AddRange(varLogs);
                    }
                    break;
                case ListType.Set:
                    {
                        ListInfo_Set subInfo = (ListInfo_Set)info.SubInfo;

                        string indexStr = StringEscaper.Preprocess(s, subInfo.Index);
                        string item = StringEscaper.Preprocess(s, subInfo.Item);

                        if (!NumberHelper.ParseInt32(indexStr, out int index))
                            return LogInfo.LogErrorMessage(logs, $"[{indexStr}] is not a valid positive integer");
                        if (index <= 0)
                            return LogInfo.LogErrorMessage(logs, $"[{indexStr}] is not a valid positive integer");

                        if (subInfo.Delim != null)
                            delimiter = StringEscaper.Preprocess(s, subInfo.Delim);

                        List<string> list = StringEscaper.UnpackListStr(listStr, delimiter);
                        list[index - 1] = item;

                        listStr = StringEscaper.PackListStr(list, delimiter);
                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.ListVar, listStr);
                        logs.AddRange(varLogs);
                    }
                    break;
                case ListType.Append:
                    {
                        ListInfo_Append subInfo = (ListInfo_Append)info.SubInfo;

                        string item = StringEscaper.Preprocess(s, subInfo.Item);

                        if (subInfo.Delim != null)
                            delimiter = StringEscaper.Preprocess(s, subInfo.Delim);

                        List<string> list = StringEscaper.UnpackListStr(listStr, delimiter);
                        list.Add(item);

                        listStr = StringEscaper.PackListStr(list, delimiter);
                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.ListVar, listStr);
                        logs.AddRange(varLogs);
                    }
                    break;
                case ListType.Insert:
                    {
                        ListInfo_Insert subInfo = (ListInfo_Insert)info.SubInfo;

                        string indexStr = StringEscaper.Preprocess(s, subInfo.Index);
                        string item = StringEscaper.Preprocess(s, subInfo.Item);

                        if (!NumberHelper.ParseInt32(indexStr, out int index))
                            return LogInfo.LogErrorMessage(logs, $"[{indexStr}] is not a valid positive integer");
                        if (index <= 0)
                            return LogInfo.LogErrorMessage(logs, $"[{indexStr}] is not a valid positive integer");

                        if (subInfo.Delim != null)
                            delimiter = StringEscaper.Preprocess(s, subInfo.Delim);

                        List<string> list = StringEscaper.UnpackListStr(listStr, delimiter);
                        list.Insert(index - 1, item);

                        listStr = StringEscaper.PackListStr(list, delimiter);
                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.ListVar, listStr);
                        logs.AddRange(varLogs);
                    }
                    break;
                case ListType.Remove:
                case ListType.RemoveX:
                    {
                        ListInfo_Remove subInfo = (ListInfo_Remove)info.SubInfo;

                        string item = StringEscaper.Preprocess(s, subInfo.Item);

                        if (subInfo.Delim != null)
                            delimiter = StringEscaper.Preprocess(s, subInfo.Delim);

                        List<string> list = StringEscaper.UnpackListStr(listStr, delimiter);
                        StringComparison comp;
                        switch (type)
                        {
                            case ListType.Remove:
                                comp = StringComparison.OrdinalIgnoreCase;
                                break;
                            case ListType.RemoveX:
                                comp = StringComparison.Ordinal;
                                break;
                            default:
                                throw new InternalException("Internal Logic Error at CommandList");
                        }

                        int deletedItemCount = list.RemoveAll(x => x.Equals(item, comp));
                        if (0 < deletedItemCount)
                        {
                            logs.Add(new LogInfo(LogState.Success, $"[{deletedItemCount}] items were deleted"));
                            listStr = StringEscaper.PackListStr(list, delimiter);
                            List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.ListVar, listStr);
                            logs.AddRange(varLogs);
                        }
                        else
                        {
                            logs.Add(new LogInfo(LogState.Ignore, "No items were deleted"));
                        }
                    }
                    break;
                case ListType.RemoveAt:
                    {
                        ListInfo_RemoveAt subInfo = (ListInfo_RemoveAt)info.SubInfo;

                        string indexStr = StringEscaper.Preprocess(s, subInfo.Index);

                        if (subInfo.Delim != null)
                            delimiter = StringEscaper.Preprocess(s, subInfo.Delim);

                        if (!NumberHelper.ParseInt32(indexStr, out int index))
                            return LogInfo.LogErrorMessage(logs, $"[{indexStr}] is not a valid positive integer");
                        if (index <= 0)
                            return LogInfo.LogErrorMessage(logs, $"[{indexStr}] is not a valid positive integer");

                        List<string> list = StringEscaper.UnpackListStr(listStr, delimiter);
                        list.RemoveAt(index - 1);

                        listStr = StringEscaper.PackListStr(list, delimiter);
                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.ListVar, listStr);
                        logs.AddRange(varLogs);
                    }
                    break;
                case ListType.Count:
                    {
                        ListInfo_Count subInfo = (ListInfo_Count)info.SubInfo;

                        if (subInfo.Delim != null)
                            delimiter = StringEscaper.Preprocess(s, subInfo.Delim);

                        List<string> list = StringEscaper.UnpackListStr(listStr, delimiter);
                        int destInt = list.Count;

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, destInt.ToString());
                        logs.AddRange(varLogs);
                    }
                    break;
                case ListType.Pos:
                case ListType.PosX:
                case ListType.LastPos:
                case ListType.LastPosX:
                    {
                        ListInfo_Pos subInfo = (ListInfo_Pos)info.SubInfo;

                        string item = StringEscaper.Preprocess(s, subInfo.Item);

                        if (subInfo.Delim != null)
                            delimiter = StringEscaper.Preprocess(s, subInfo.Delim);

                        List<string> list = StringEscaper.UnpackListStr(listStr, delimiter);
                        int destInt;
                        switch (type)
                        {
                            case ListType.Pos:
                                destInt = list.FindIndex(x => x.Equals(item, StringComparison.OrdinalIgnoreCase));
                                break;
                            case ListType.PosX:
                                destInt = list.FindIndex(x => x.Equals(item, StringComparison.Ordinal));
                                break;
                            case ListType.LastPos:
                                destInt = list.FindLastIndex(x => x.Equals(item, StringComparison.OrdinalIgnoreCase));
                                break;
                            case ListType.LastPosX:
                                destInt = list.FindLastIndex(x => x.Equals(item, StringComparison.Ordinal));
                                break;
                            default:
                                throw new InternalException("Internal Logic Error at CommandList");
                        }
                        destInt += 1;

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, destInt.ToString());
                        logs.AddRange(varLogs);
                    }
                    break;
                case ListType.Sort:
                case ListType.SortX:
                case ListType.SortN:
                case ListType.SortNX:
                    {
                        ListInfo_Sort subInfo = (ListInfo_Sort)info.SubInfo;

                        string order = StringEscaper.Preprocess(s, subInfo.Order);

                        if (subInfo.Delim != null)
                            delimiter = StringEscaper.Preprocess(s, subInfo.Delim);

                        bool reverse;
                        if (order.Equals("ASC", StringComparison.OrdinalIgnoreCase))
                            reverse = false;
                        else if (order.Equals("DESC", StringComparison.OrdinalIgnoreCase))
                            reverse = true;
                        else
                            return LogInfo.LogErrorMessage(logs, "Order must be [ASC] or [DESC]");

                        List<string> list = StringEscaper.UnpackListStr(listStr, delimiter);
                        switch (type)
                        {
                            case ListType.Sort:
                                list = list
                                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                                    .ThenBy(x => x, StringComparer.Ordinal)
                                    .ToList();
                                break;
                            case ListType.SortX:
                                list.Sort(StringComparer.Ordinal);
                                break;
                            case ListType.SortN:
                                list = list
                                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase.WithNaturalSort())
                                    .ThenBy(x => x, StringComparer.Ordinal.WithNaturalSort())
                                    .ToList();
                                break;
                            case ListType.SortNX:
                                list.Sort(StringComparer.Ordinal.WithNaturalSort());
                                break;
                            default:
                                throw new InternalException("Internal Logic Error at CommandList");
                        }

                        if (reverse)
                            list.Reverse();

                        listStr = StringEscaper.PackListStr(list, delimiter);
                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.ListVar, listStr);
                        logs.AddRange(varLogs);
                    }
                    break;
                case ListType.Range:
                    {
                        ListInfo_Range subInfo = (ListInfo_Range)info.SubInfo;

                        string startStr = StringEscaper.Preprocess(s, subInfo.Start);
                        string endStr = StringEscaper.Preprocess(s, subInfo.End);

                        // TODO: A-Z, a-z 시나리오도 준비
                        if (!NumberHelper.ParseInt64(startStr, out long startVal))
                            return LogInfo.LogErrorMessage(logs, $"[{startVal}] is not a valid integer");
                        if (!NumberHelper.ParseInt64(endStr, out long endVal))
                            return LogInfo.LogErrorMessage(logs, $"[{endVal}] is not a valid integer");

                        long stepVal = startVal <= endVal ? 1 : -1;
                        if (subInfo.Step != null)
                        {
                            string stepStr = StringEscaper.Preprocess(s, subInfo.Step);
                            if (!NumberHelper.ParseInt64(stepStr, out stepVal))
                                return LogInfo.LogErrorMessage(logs, $"[{stepVal}] is not a valid integer");
                        }

                        if (subInfo.Delim != null)
                            delimiter = StringEscaper.Preprocess(s, subInfo.Delim);

                        List<string> list;
                        if (startVal < endVal)
                        { 
                            if (stepVal <= 0)
                                return LogInfo.LogErrorMessage(logs, $"[{startVal}] is larger than [{endVal}], step [{stepVal}] must be positive integer");

                            list = new List<string>();
                            for (long i = startVal; i < endVal; i += stepVal)
                                list.Add(i.ToString());
                        }
                        else if (endVal < startVal)
                        {
                            if (0 <= stepVal)
                                return LogInfo.LogErrorMessage(logs, $"[{startVal}] is smaller than [{endVal}], step [{stepVal}] must be negative integer");

                            list = new List<string>();
                            for (long i = startVal; endVal < i; i += stepVal)
                                list.Add(i.ToString());
                        }
                        else
                        {
                            return LogInfo.LogErrorMessage(logs, $"Step [{stepVal}] canmnot be 0");
                        }
                        

                        listStr = StringEscaper.PackListStr(list, delimiter);
                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.ListVar, listStr);
                        logs.AddRange(varLogs);
                    }
                    break;
                default: // Error
                    logs.Add(new LogInfo(LogState.CriticalError, $"[List,{info.Type}] is not yet implemented."));
                    break;
            }

            return logs;
        }
    }
}
