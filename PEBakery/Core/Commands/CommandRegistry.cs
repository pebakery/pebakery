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

namespace PEBakery.Core.Commands
{
    public static class CommandRegistry
    {
        public static List<LogInfo> RegRead(EngineState s, CodeCommand cmd)
        { // RegRead,<HKey>,<Key>,<ValueName>,<DestVar>
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_RegRead));
            CodeInfo_RegRead info = cmd.Info as CodeInfo_RegRead;

            string keyPath = StringEscaper.Preprocess(s, info.KeyPath);
            string valueName = StringEscaper.Preprocess(s, info.ValueName);

            string hKeyStr = RegistryHelper.RegKeyToFullString(info.HKey);
            if (hKeyStr == null)
                throw new InternalException("Internal Logic Error at RegRead");
            string fullKeyPath = $"{hKeyStr}\\{keyPath}";

            object valueData = Registry.GetValue(fullKeyPath, valueName, null);

            s.MainViewModel.BuildCommandProgressBarValue = 500;

            if (valueData == null)
            {
                logs.Add(new LogInfo(LogState.Error, $"Cannot read registry key [{fullKeyPath}]"));
            }
            else
            {
                logs.Add(new LogInfo(LogState.Success, $"Rgistry key [{fullKeyPath}]'s value is [{valueData}]"));
                List<LogInfo> varLogs = Variables.SetVariable(s, info.DestVar, valueData.ToString());
                logs.AddRange(varLogs);
            }

            return logs;
        }
    }
}
