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
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace PEBakery.Core.Commands
{
    public static class CommandNetwork
    {
        public static List<LogInfo> WebGet(EngineState s, CodeCommand cmd)
        { // WebGet,<URL>,<DestPath>,[HashType],[HashDigest]
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_WebGet));
            CodeInfo_WebGet info = cmd.Info as CodeInfo_WebGet;

            string url = StringEscaper.Preprocess(s, info.URL);
            string downloadTo = StringEscaper.Preprocess(s, info.DestPath);

            if (StringEscaper.PathSecurityCheck(downloadTo, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            Uri uri = new Uri(url);
            string destPath;
            if (Directory.Exists(downloadTo)) // downloadTo is dir
            {
                destPath = Path.Combine(downloadTo, Path.GetFileName(uri.LocalPath));
            }
            else // downloadTo is file
            {
                if (File.Exists(downloadTo))
                {
                    if (cmd.Type == CodeType.WebGetIfNotExist)
                    {
                        logs.Add(new LogInfo(LogState.Ignore, $"File [{downloadTo}] already exists"));
                        return logs;
                    }
                    else
                    {
                        logs.Add(new LogInfo(LogState.Ignore, $"File [{downloadTo}] will be overwritten"));
                    }
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(downloadTo));
                }
                destPath = downloadTo;
            }

            s.MainViewModel.BuildCommandProgressBarValue = 200;

            if (info.HashType != null && info.HashDigest != null)
            { // Calculate Hash After Downloading
                string tempPath = Path.GetTempFileName();
                using (WebClient client = new WebClient())
                {
                    client.DownloadFile(url, tempPath);
                }

                s.MainViewModel.BuildCommandProgressBarValue = 500;

                string hashTypeStr = StringEscaper.Preprocess(s, info.HashType);
                string hashDigest = StringEscaper.Preprocess(s, info.HashDigest);

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

                int byteLen = 0;
                switch (hashType)
                {
                    case HashType.MD5:
                        byteLen = 32;
                        break;
                    case HashType.SHA1:
                        byteLen = 40;
                        break;
                    case HashType.SHA256:
                        byteLen = 64;
                        break;
                    case HashType.SHA384:
                        byteLen = 96;
                        break;
                    case HashType.SHA512:
                        byteLen = 128;
                        break;
                }

                if (hashDigest.Length != byteLen)
                    throw new ExecuteException($"Hash digest [{hashDigest}] is not [{hashTypeStr}]");

                string downDigest;
                using (FileStream fs = new FileStream(tempPath, FileMode.Open, FileAccess.Read))
                {
                    downDigest = HashHelper.CalcHashString(hashType, fs);
                }

                s.MainViewModel.BuildCommandProgressBarValue = 800;
                if (hashDigest.Equals(downDigest, StringComparison.OrdinalIgnoreCase)) // Success
                {
                    File.Move(tempPath, destPath);
                    logs.Add(new LogInfo(LogState.Success, $"[{url}] downloaded to [{downloadTo}] with integerity checked."));
                }
                else
                {
                    logs.Add(new LogInfo(LogState.Success, $"Downloaded [{url}], but it was corrupted"));
                }
            }
            else
            { // No Hash
                using (WebClient client = new WebClient())
                {
                    client.DownloadFile(url, destPath);
                }

                s.MainViewModel.BuildCommandProgressBarValue = 700;
                logs.Add(new LogInfo(LogState.Success, $"[{url}] downloaded to [{downloadTo}]"));
            }

            return logs;
        }
    }
}
