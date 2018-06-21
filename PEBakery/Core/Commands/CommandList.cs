/*
    Copyright (C) 2018 Hajin Jang
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

using System;
using System.Linq;
using System.Collections.Generic;
using PEBakery.Helper;

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
            CodeInfo_List info = cmd.Info.Cast<CodeInfo_List>();

            ListType type = info.Type;
            string delimiter = "|";
            switch (type)
            {
                case ListType.Get:
                    {
                        ListInfo_Get subInfo = info.SubInfo.Cast<ListInfo_Get>();

                        string listStr = StringEscaper.Preprocess(s, subInfo.ListVar);
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
                        ListInfo_Set subInfo = info.SubInfo.Cast<ListInfo_Set>();

                        string listStr = StringEscaper.Preprocess(s, subInfo.ListVar);
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
                        ListInfo_Append subInfo = info.SubInfo.Cast<ListInfo_Append>();

                        string listStr = StringEscaper.Preprocess(s, subInfo.ListVar);
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
                        ListInfo_Insert subInfo = info.SubInfo.Cast<ListInfo_Insert>();

                        string listStr = StringEscaper.Preprocess(s, subInfo.ListVar);
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
                        ListInfo_Remove subInfo = info.SubInfo.Cast<ListInfo_Remove>();

                        string listStr = StringEscaper.Preprocess(s, subInfo.ListVar);
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
                            logs.Add(new LogInfo(LogState.Ignore, "No item was deleted"));
                        }
                    }
                    break;
                case ListType.RemoveAt:
                    {
                        ListInfo_RemoveAt subInfo = info.SubInfo.Cast<ListInfo_RemoveAt>();

                        string listStr = StringEscaper.Preprocess(s, subInfo.ListVar);
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
                        ListInfo_Count subInfo = info.SubInfo.Cast<ListInfo_Count>();

                        string listStr = StringEscaper.Preprocess(s, subInfo.ListVar);

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
                        ListInfo_Pos subInfo = info.SubInfo.Cast<ListInfo_Pos>();

                        string listStr = StringEscaper.Preprocess(s, subInfo.ListVar);
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
                default: // Error
                    throw new InternalException("Internal Logic Error at CommandList");
            }

            return logs;
        }
    }
}
