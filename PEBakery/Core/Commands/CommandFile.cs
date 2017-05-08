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
    public static class CommandFile
    {
        public static List<LogInfo> FileCreateBlank(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_FileCreateBlank));
            CodeInfo_FileCreateBlank info = cmd.Info as CodeInfo_FileCreateBlank;

            string filePath = StringEscaper.Preprocess(s, info.FilePath);

            // Default Encoding - UTF8
            Encoding encoding = Encoding.UTF8;
            if (info.Encoding != null)
                encoding = info.Encoding;

            if (File.Exists(filePath))
            {
                if (info.Preserve)
                {
                    logs.Add(new LogInfo(LogState.Success, $"Cannot overwrite [{filePath}]", cmd));
                    return logs;
                }
                else
                {
                    logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Warning, $"[{filePath}] will be overwritten", cmd));
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            FileHelper.WriteTextBOM(fs, encoding).Close();
            logs.Add(new LogInfo(LogState.Success, $"Created blank text file [{filePath}]", cmd));

            return logs;
        }
    }
}
