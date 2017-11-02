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
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using PEBakery.Helper;
using PEBakery.Exceptions;
using System.IO;
using System.Globalization;

namespace PEBakery.Core.Commands
{
    public static class CommandRegistry
    {
        private static bool privilegesEnabled = false;

        public static List<LogInfo> RegHiveLoad(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_RegHiveLoad));
            CodeInfo_RegHiveLoad info = cmd.Info as CodeInfo_RegHiveLoad;

            string keyPath = StringEscaper.Preprocess(s, info.KeyPath);
            string hiveFile = StringEscaper.Preprocess(s, info.HiveFile);

            if (!File.Exists(hiveFile))
                throw new ExecuteException($"Hive file [{hiveFile}] does not exist");

            if (!privilegesEnabled)
            {
                RegistryHelper.GetAdminPrivileges();
                privilegesEnabled = true;
            }

            int result = RegistryHelper.RegLoadKey(RegistryHelper.HKLM, keyPath, hiveFile);
            if (result == (int)BetterWin32Errors.Win32Error.ERROR_SUCCESS)
                logs.Add(new LogInfo(LogState.Success, $"Loaded [{hiveFile}] into [HKLM\\{keyPath}]"));
            else
                logs.Add(new LogInfo(LogState.Success, $"Could not load [{hiveFile}] into [HKLM\\{keyPath}], error code = [{result}]"));

            return logs;
        }

        public static List<LogInfo> RegHiveUnload(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_RegHiveUnload));
            CodeInfo_RegHiveUnload info = cmd.Info as CodeInfo_RegHiveUnload;

            string keyPath = StringEscaper.Preprocess(s, info.KeyPath);

            if (!privilegesEnabled)
            {
                RegistryHelper.GetAdminPrivileges();
                privilegesEnabled = true;
            }

            int result = RegistryHelper.RegUnLoadKey(RegistryHelper.HKLM, keyPath);
            if (result == (int)BetterWin32Errors.Win32Error.ERROR_SUCCESS)
                logs.Add(new LogInfo(LogState.Success, $"[HKLM\\{keyPath}] Unloaded"));
            else
                logs.Add(new LogInfo(LogState.Success, $"Could not unload [HKLM\\{keyPath}], error code = [{result}]"));

            return logs;
        }

        public static List<LogInfo> RegImport(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_RegImport));
            CodeInfo_RegImport info = cmd.Info as CodeInfo_RegImport;

            string regFile = StringEscaper.Preprocess(s, info.RegFile);

            using (Process proc = new Process())
            {
                proc.StartInfo.FileName = "REG.exe";
                proc.StartInfo.Arguments = $"IMPORT \"{regFile}\"";
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.Verb = "Open";
                proc.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                proc.StartInfo.CreateNoWindow = true;
                proc.Start();

                s.MainViewModel.BuildCommandProgressBarValue = 500;

                proc.WaitForExit();

                if (proc.ExitCode == 0) // Success
                    logs.Add(new LogInfo(LogState.Success, $"Registry file [{regFile}] imported"));
                else // if (proc.ExitCode == 1) // Failure
                    logs.Add(new LogInfo(LogState.Error, $"Registry file [{regFile}] import failed"));
            }

            return logs;
        }

        public static List<LogInfo> RegExport(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_RegExport));
            CodeInfo_RegExport info = cmd.Info as CodeInfo_RegExport;

            string keyPath = StringEscaper.Preprocess(s, info.KeyPath);
            string regFile = StringEscaper.Preprocess(s, info.RegFile);

            string hKeyStr = RegistryHelper.RegKeyToString(info.HKey);
            if (hKeyStr == null)
                throw new InternalException("Internal Logic Error");
            string fullKeyPath = $"{hKeyStr}\\{keyPath}";

            if (File.Exists(regFile))
                logs.Add(new LogInfo(LogState.Warning, $"File [{regFile}] will be overwritten"));

            using (Process proc = new Process())
            {
                proc.StartInfo.FileName = "REG.exe";
                proc.StartInfo.Arguments = $"EXPORT \"{fullKeyPath}\" \"{regFile}\" /y";
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.Verb = "Open";
                proc.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                proc.StartInfo.CreateNoWindow = true;
                proc.Start();

                s.MainViewModel.BuildCommandProgressBarValue = 500;

                proc.WaitForExit();

                if (proc.ExitCode == 0) // Success
                    logs.Add(new LogInfo(LogState.Success, $"Registry key [{fullKeyPath}] exported to [{regFile}]"));
                else // if (proc.ExitCode == 1) // Failure
                    logs.Add(new LogInfo(LogState.Error, $"Registry key [{fullKeyPath}] cannot be exported"));
            }

            return logs;
        }

        public static List<LogInfo> RegRead(EngineState s, CodeCommand cmd)
        { // RegRead,<HKey>,<KeyPath>,<ValueName>,<DestVar>
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_RegRead));
            CodeInfo_RegRead info = cmd.Info as CodeInfo_RegRead;

            string keyPath = StringEscaper.Preprocess(s, info.KeyPath);
            string valueName = StringEscaper.Preprocess(s, info.ValueName);

            string hKeyStr = RegistryHelper.RegKeyToString(info.HKey);
            if (hKeyStr == null)
                throw new InternalException("Internal Logic Error");
            string fullKeyPath = $"{hKeyStr}\\{keyPath}";

            s.MainViewModel.BuildCommandProgressBarValue = 300;

            string valueDataStr;
            using (RegistryKey subKey = info.HKey.OpenSubKey(keyPath, false))
            {
                RegistryValueKind kind = subKey.GetValueKind(valueName);
                object valueData = subKey.GetValue(valueName, null);

                if (valueData == null)
                {
                    logs.Add(new LogInfo(LogState.Error, $"Cannot read registry key [{fullKeyPath}]"));
                    return logs;
                }

                switch (kind)
                { 
                    case RegistryValueKind.None:
                        valueDataStr = string.Empty;
                        break;
                    case RegistryValueKind.String:
                    case RegistryValueKind.ExpandString:
                        valueDataStr = (string)valueData;
                        break;
                    case RegistryValueKind.Binary:
                        valueDataStr = StringEscaper.PackRegBinary((byte[])valueData);
                        break;
                    case RegistryValueKind.DWord:
                        valueDataStr = ((uint)(int)valueData).ToString();
                        break;
                    case RegistryValueKind.MultiString:
                        valueDataStr = StringEscaper.PackRegMultiString((string[])valueData);
                        break;
                    case RegistryValueKind.QWord:
                        valueDataStr = ((ulong)(long)valueData).ToString();
                        break;
                    default:
                        logs.Add(new LogInfo(LogState.Error, $"Unsupported registry value type [0x{((int) kind).ToString("0:X")}]"));
                        return logs;
                }
            }

            s.MainViewModel.BuildCommandProgressBarValue = 700;

            logs.Add(new LogInfo(LogState.Success, $"Registry value [{fullKeyPath}\\{valueName}]'s data is [{valueDataStr}]"));
            List<LogInfo> varLogs = Variables.SetVariable(s, info.DestVar, valueDataStr);
            logs.AddRange(varLogs);

            return logs;
        }

        public static List<LogInfo> RegWrite(EngineState s, CodeCommand cmd)
        { // RegWrite,<HKey>,<ValueType>,<KeyPath>,<ValueName>,<ValueData>,[OptionalData]
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_RegWrite));
            CodeInfo_RegWrite info = cmd.Info as CodeInfo_RegWrite;

            string keyPath = StringEscaper.Preprocess(s, info.KeyPath);
            string valueName = StringEscaper.Preprocess(s, info.ValueName);
            
            string hKeyStr = RegistryHelper.RegKeyToString(info.HKey);
            if (hKeyStr == null)
                throw new InternalException("Internal Logic Error");
            string fullKeyPath = $"{hKeyStr}\\{keyPath}";

            s.MainViewModel.BuildCommandProgressBarValue = 500;

            string valueData;
            using (RegistryKey subKey = info.HKey.CreateSubKey(keyPath, true))
            {
                switch (info.ValueType)
                {
                    case RegistryValueKind.None:
                        subKey.SetValue(valueName, null, RegistryValueKind.None);
                        logs.Add(new LogInfo(LogState.Success, $"Registry value [{fullKeyPath}]\\{valueName}] set to none"));
                        return logs;
                    case RegistryValueKind.String:
                        valueData = StringEscaper.Preprocess(s, info.ValueData);
                        subKey.SetValue(valueName, valueData, RegistryValueKind.String);
                        break;
                    case RegistryValueKind.ExpandString:
                        valueData = info.ValueData; // Need Test
                        subKey.SetValue(valueName, valueData, RegistryValueKind.ExpandString);
                        break;
                    case RegistryValueKind.Binary:
                        valueData = StringEscaper.Preprocess(s, info.ValueData);
                        if (!StringEscaper.UnpackRegBinary(valueData, out byte[] bin))
                            throw new ExecuteException($"[{valueData}] is not valid binary representation");
                        subKey.SetValue(valueName, bin, RegistryValueKind.Binary);
                        break;
                    case RegistryValueKind.DWord:
                        valueData = StringEscaper.Preprocess(s, info.ValueData);
                        if (!NumberHelper.ParseUInt32(valueData, out uint valUInt32))
                            throw new ExecuteException($"[{valueData}] is not valid DWORD");
                        subKey.SetValue(valueName, valUInt32, RegistryValueKind.DWord);
                        break;
                    case RegistryValueKind.MultiString:
                        string[] multiStrs = StringEscaper.Preprocess(s, info.ValueDatas).ToArray();
                        subKey.SetValue(valueName, multiStrs, RegistryValueKind.MultiString);
                        valueData = StringEscaper.PackRegMultiString(multiStrs);
                        break;
                    case RegistryValueKind.QWord:
                        valueData = StringEscaper.Preprocess(s, info.ValueData);
                        if (!NumberHelper.ParseUInt64(valueData, out ulong valUInt64))
                            throw new ExecuteException($"[{valueData}] is not valid DWORD");
                        subKey.SetValue(valueName, valUInt64, RegistryValueKind.DWord);
                        break;
                    default:
                        throw new InternalException("Internal CodeParser Error");
                }
            }

            logs.Add(new LogInfo(LogState.Success, $"Registry value [{fullKeyPath}\\{valueName}] set to [{valueData}]"));
            return logs;
        }

        public static List<LogInfo> RegDelete(EngineState s, CodeCommand cmd)
        { // RegDelete,<HKey>,<KeyPath>,[ValueName]
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_RegDelete));
            CodeInfo_RegDelete info = cmd.Info as CodeInfo_RegDelete;

            string keyPath = StringEscaper.Preprocess(s, info.KeyPath);

            string hKeyStr = RegistryHelper.RegKeyToString(info.HKey);
            if (hKeyStr == null)
                throw new InternalException("Internal Logic Error");

            string fullKeyPath = $"{hKeyStr}\\{keyPath}";

            s.MainViewModel.BuildCommandProgressBarValue = 500;

            if (info.ValueName == null)
            { // Delete SubKey
                try
                {
                    info.HKey.DeleteSubKeyTree(keyPath, true);
                    logs.Add(new LogInfo(LogState.Success, $"Registry key [{fullKeyPath}] deleted"));
                }
                catch (ArgumentException)
                {
                    logs.Add(new LogInfo(LogState.Ignore, $"Registry key [{fullKeyPath}] does not exist"));
                }
            }
            else
            { // Delete Value
                string valueName = StringEscaper.Preprocess(s, info.ValueName);

                using (RegistryKey subKey = info.HKey.OpenSubKey(keyPath, true))
                {
                    try
                    {
                        subKey.DeleteValue(valueName, true);
                        logs.Add(new LogInfo(LogState.Success, $"Registry value [{fullKeyPath}\\{valueName}] deleted"));
                    }
                    catch (ArgumentException)
                    {
                        logs.Add(new LogInfo(LogState.Ignore, $"Registry value [{fullKeyPath}\\{valueName}] does not exist"));
                    }
                }
            }
            
            return logs;
        }

        public static List<LogInfo> RegMulti(EngineState s, CodeCommand cmd)
        { // RegMulti,<HKey>,<KeyPath>,<ValueName>,<Action>,<Arg1>,[Arg2]
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_RegMulti));
            CodeInfo_RegMulti info = cmd.Info as CodeInfo_RegMulti;

            string keyPath = StringEscaper.Preprocess(s, info.KeyPath);
            string valueName = StringEscaper.Preprocess(s, info.ValueName);
            string arg1 = StringEscaper.Preprocess(s, info.Arg1);
            string arg2 = null;
            if (info.Arg2 != null)
                arg2 = StringEscaper.Preprocess(s, info.Arg2);

            string hKeyStr = RegistryHelper.RegKeyToString(info.HKey);
            if (hKeyStr == null)
                throw new InternalException("Internal Logic Error");
            string fullKeyPath = $"{hKeyStr}\\{keyPath}";

            using (RegistryKey subKey = info.HKey.OpenSubKey(keyPath, true))
            {
                if (subKey == null)
                {
                    logs.Add(new LogInfo(LogState.Error, $"Registry key [{fullKeyPath}] does not exist"));
                    return logs;
                }

                object regRead = subKey.GetValue(valueName, null);
                if (regRead == null)
                {
                    logs.Add(new LogInfo(LogState.Error, $"Registry value [{fullKeyPath}\\{valueName}] does not exist"));
                    return logs;
                }

                RegistryValueKind kind = subKey.GetValueKind(valueName);
                if (kind != RegistryValueKind.MultiString)
                {
                    logs.Add(new LogInfo(LogState.Error, $"Registry value [{fullKeyPath}\\{valueName}] is not REG_MULTI_SZ"));
                    return logs;
                }

                List<string> multiStrs = ((string[])regRead).ToList();

                s.MainViewModel.BuildCommandProgressBarValue = 500;

                switch (info.ActionType)
                {
                    case RegMultiType.Append:
                        multiStrs.Add(arg1);
                        subKey.SetValue(valueName, multiStrs);
                        logs.Add(new LogInfo(LogState.Success, $"[{arg1}] appended to REG_MULTI_SZ [{fullKeyPath}]\\{valueName}]"));
                        break;
                    case RegMultiType.Prepend:
                        multiStrs.Insert(0, arg1);
                        subKey.SetValue(valueName, multiStrs);
                        logs.Add(new LogInfo(LogState.Success, $"[{arg1}] prepended to REG_MULTI_SZ [{fullKeyPath}]\\{valueName}]"));
                        break;
                    case RegMultiType.Before:
                        {
                            int idx = multiStrs.FindIndex(x => x.Equals(arg1, StringComparison.OrdinalIgnoreCase));
                            if (idx == -1) // Not Found
                            {
                                logs.Add(new LogInfo(LogState.Error, $"[{arg1}] not found in REG_MULTI_SZ [{fullKeyPath}]\\{valueName}]"));
                                break;
                            }

                            // Found
                            multiStrs.Insert(idx, arg2);
                            subKey.SetValue(valueName, multiStrs);
                            logs.Add(new LogInfo(LogState.Success, $"[{arg2}] placed at index [{idx + 1}] of REG_MULTI_SZ [{fullKeyPath}]\\{valueName}]"));
                        }
                        break;
                    case RegMultiType.Behind:
                        {
                            int idx = multiStrs.FindIndex(x => x.Equals(arg1, StringComparison.OrdinalIgnoreCase));
                            if (idx == -1) // Not Found
                            {
                                logs.Add(new LogInfo(LogState.Error, $"[{arg1}] not found in REG_MULTI_SZ [{fullKeyPath}]\\{valueName}]"));
                                break;
                            }

                            // Found
                            multiStrs.Insert(idx + 1, arg2);
                            subKey.SetValue(valueName, multiStrs);
                            logs.Add(new LogInfo(LogState.Success, $"[{arg2}] placed at index [{idx + 2}] of REG_MULTI_SZ [{fullKeyPath}]\\{valueName}]"));
                        }
                        break;
                    case RegMultiType.Place:
                        { // Different from WB082, it will just overwrite
                            if (!NumberHelper.ParseInt32(arg1, out int idx))
                                throw new ExecuteException($"[{arg1}] is not a valid integer");
                            if (idx <= 0)
                                throw new ExecuteException($"Index [{arg1}] must be positive integer");

                            if (0 <= idx && idx <= multiStrs.Count)
                            {
                                string beforeValue = multiStrs[idx];
                                multiStrs.RemoveAt(idx);
                                multiStrs.Insert(idx, arg2);
                                subKey.SetValue(valueName, multiStrs);
                                logs.Add(new LogInfo(LogState.Success, $"[{arg2}] placed at index [{idx + 1}] of REG_MULTI_SZ [{fullKeyPath}]\\{valueName}], overwriting [{beforeValue}]"));
                            }
                            else
                            {
                                logs.Add(new LogInfo(LogState.Error, $"Index [{idx + 1}] out of range, REG_MULTI_SZ [{fullKeyPath}]\\{valueName}] has [{multiStrs.Count}] strings"));
                            }
                        }
                        break;
                    case RegMultiType.Delete:
                        {
                            int idx = multiStrs.FindIndex(x => x.Equals(arg1, StringComparison.OrdinalIgnoreCase));
                            if (idx == -1) // Not Found
                            {
                                logs.Add(new LogInfo(LogState.Error, $"[{arg1}] not found in REG_MULTI_SZ [{fullKeyPath}]\\{valueName}]"));
                                break;
                            }

                            string beforeValue = multiStrs[idx];
                            multiStrs.RemoveAt(idx);
                            subKey.SetValue(valueName, multiStrs);
                            logs.Add(new LogInfo(LogState.Success, $"[{beforeValue}] (index [{idx + 1}]) deleted from REG_MULTI_SZ [{fullKeyPath}]\\{valueName}]"));
                        }
                        break;
                    case RegMultiType.Index:
                        {
                            if (Variables.DetermineType(info.Arg2) == Variables.VarKeyType.None)
                                throw new ExecuteException($"[{info.Arg2}] is not valid variable name");

                            string idxStr;
                            int idx = multiStrs.FindIndex(x => x.Equals(arg1, StringComparison.OrdinalIgnoreCase));
                            idxStr = (idx + 1).ToString();

                            if (idx == -1) // Not Found
                                logs.Add(new LogInfo(LogState.Error, $"[{arg1}] does not exists in REG_MULTI_SZ [{fullKeyPath}]\\{valueName}]"));
                            else // Found
                                logs.Add(new LogInfo(LogState.Success, $"[{arg1}] exists in REG_MULTI_SZ [{fullKeyPath}]\\{valueName}]'s index [{idxStr}]"));

                            List<LogInfo> varLogs = Variables.SetVariable(s, info.Arg2, idxStr);
                            logs.AddRange(varLogs);
                        }
                        break;
                }
            }

            return logs;
        }
    }
}
