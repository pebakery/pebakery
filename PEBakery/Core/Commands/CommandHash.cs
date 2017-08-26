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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PEBakery.Core.Commands
{
    public static class CommandHash
    {
        public static List<LogInfo> Hash(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Hash));
            CodeInfo_Hash info = cmd.Info as CodeInfo_Hash;

            string hashTypeStr = StringEscaper.Preprocess(s, info.HashType);
            string filePath = StringEscaper.Preprocess(s, info.FilePath);

            HashType hashType;
            if (hashTypeStr.Equals("MD5", StringComparison.OrdinalIgnoreCase))
                hashType = HashType.MD5;
            else if (hashTypeStr.Equals("SHA1", StringComparison.OrdinalIgnoreCase))
                hashType = HashType.SHA1;
            else if (hashTypeStr.Equals("SHA256", StringComparison.OrdinalIgnoreCase))
                hashType = HashType.SHA256;
            else if (hashTypeStr.Equals("SHA384", StringComparison.OrdinalIgnoreCase))
                hashType = HashType.SHA384;
            else if (hashTypeStr.Equals("SHA512", StringComparison.OrdinalIgnoreCase))
                hashType = HashType.SHA512;
            else
                throw new ExecuteException($"Invalid hash type [{hashTypeStr}]");

            s.MainViewModel.BuildCommandProgressBarValue = 300;

            byte[] digest;
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                digest = HashHelper.CalcHash(hashType, fs);
            }

            s.MainViewModel.BuildCommandProgressBarValue = 700;

            StringBuilder b = new StringBuilder();
            foreach (byte d in digest)
                b.AppendFormat("{0:x2}", d);

            logs.Add(new LogInfo(LogState.Success, $"Hash [{hashType}] digest of [{filePath}] is [{b}]"));
            List<LogInfo> varLogs = Variables.SetVariable(s, info.DestVar, b.ToString());
            logs.AddRange(varLogs);

            return logs;
        }
    }
}
