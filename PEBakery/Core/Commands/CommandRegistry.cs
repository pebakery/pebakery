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

        public static List<LogInfo> RegRead(EngineState s, CodeCommand cmd)
        { // RegRead,<HKey>,<KeyPath>,<ValueName>,<DestVar>
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_RegRead));
            CodeInfo_RegRead info = cmd.Info as CodeInfo_RegRead;

            string keyPath = StringEscaper.Preprocess(s, info.KeyPath);
            string valueName = StringEscaper.Preprocess(s, info.ValueName);

            string hKeyStr = RegistryHelper.RegKeyToString(info.HKey);
            if (hKeyStr == null)
                throw new InternalException("Internal Logic Error at RegRead");
            string fullKeyPath = $"{hKeyStr}\\{keyPath}";

            s.MainViewModel.BuildCommandProgressBarValue = 300;

            object valueData;
            using (RegistryKey subKey = info.HKey.OpenSubKey(keyPath, false))
            {
                RegistryValueKind kind = subKey.GetValueKind(valueName);
                switch (kind)
                { 
                    case RegistryValueKind.None:
                    case RegistryValueKind.String:
                    case RegistryValueKind.ExpandString:
                    case RegistryValueKind.Binary:
                    case RegistryValueKind.DWord:
                    case RegistryValueKind.MultiString:
                    case RegistryValueKind.QWord:
                        break;
                    default:
                        logs.Add(new LogInfo(LogState.Error, $"Unsupported registry value type [0x{((int) kind).ToString("0:X")}]"));
                        break;
                }

                valueData = subKey.GetValue(valueName, null);
            }

            s.MainViewModel.BuildCommandProgressBarValue = 700;

            if (valueData == null)
            {
                logs.Add(new LogInfo(LogState.Error, $"Cannot read registry key [{fullKeyPath}]"));
            }
            else
            {
                logs.Add(new LogInfo(LogState.Success, $"Registry key [{fullKeyPath}]'s value is [{valueData}]"));
                List<LogInfo> varLogs = Variables.SetVariable(s, info.DestVar, valueData.ToString());
                logs.AddRange(varLogs);
            }

            return logs;
        }

        public static List<LogInfo> RegReadBin(EngineState s, CodeCommand cmd)
        { // RegReadBin,<HKey>,<KeyPath>,<ValueName>,<DestVar>
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_RegReadBin));
            CodeInfo_RegReadBin info = cmd.Info as CodeInfo_RegReadBin;

            string keyPath = StringEscaper.Preprocess(s, info.KeyPath);
            string valueName = StringEscaper.Preprocess(s, info.ValueName);

            string hKeyStr = RegistryHelper.RegKeyToFullString(info.HKey);
            if (hKeyStr == null)
                throw new InternalException("Internal Logic Error at RegRead");
            string fullKeyPath = $"{hKeyStr}\\{keyPath}";

            s.MainViewModel.BuildCommandProgressBarValue = 300;

            StringBuilder b = new StringBuilder();
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
                    case RegistryValueKind.String:
                    case RegistryValueKind.ExpandString:
                        {
                            byte[] bin = Encoding.Unicode.GetBytes((string)valueData);
                            for (int i = 0; i < bin.Length; i++)
                            {
                                b.Append(bin[i].ToString("0:2X"));
                                if (i + 1 < bin.Length)
                                    b.Append(",");
                            }
                        }
                        break;
                    case RegistryValueKind.MultiString:
                        { // Need Test
                            string[] strs = (string[])valueData;
                            for (int x = 0; x < strs.Length; x++)
                            {
                                string str = strs[x];
                                for (int i = 0; i < str.Length; i++)
                                {
                                    byte[] bin = Encoding.Unicode.GetBytes(str);
                                    b.Append(bin[i].ToString("0:2X"));
                                    if (i + 1 < bin.Length)
                                        b.Append(",");
                                }

                                if (x + 1 < strs.Length)
                                    b.Append("00,00,");
                            }
                        }
                        break;
                    case RegistryValueKind.Binary:
                        {
                            byte[] bin = (byte[]) valueData;
                            for (int i = 0; i < bin.Length; i++)
                            {
                                b.Append(bin[i].ToString("0:2X"));
                                if (i + 1 < bin.Length)
                                    b.Append(",");
                            }
                        }
                        break;
                    default:
                        logs.Add(new LogInfo(LogState.Error, $"Unsupported registry value type [0x{((int)kind).ToString("0:X")}]"));
                        break;
                }
            }

            s.MainViewModel.BuildCommandProgressBarValue = 700;

            logs.Add(new LogInfo(LogState.Success, $"Rgistry key [{fullKeyPath}]'s value is [{b}]"));
            List<LogInfo> varLogs = Variables.SetVariable(s, info.DestVar, b.ToString());
            logs.AddRange(varLogs);

            return logs;
        }
    }
}
