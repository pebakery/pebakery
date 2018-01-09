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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
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

            s.MainViewModel.BuildCommandProgressTitle = "WebGet Progress";
            s.MainViewModel.BuildCommandProgressText = string.Empty;
            s.MainViewModel.BuildCommandProgressMax = 100;
            s.MainViewModel.BuildCommandProgressShow = true;
            try
            {
                if (info.HashType != null && info.HashDigest != null)
                { // Calculate Hash After Downloading
                    string tempPath = Path.GetTempFileName();
                    
                    if (!DownloadFile(s, url, tempPath))
                        logs.Add(new LogInfo(LogState.Error, $"Error occured while downloading [{url}]"));

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

                    if (hashDigest.Equals(downDigest, StringComparison.OrdinalIgnoreCase)) // Success
                    {
                        File.Move(tempPath, destPath);
                        logs.Add(new LogInfo(LogState.Success, $"[{url}] downloaded to [{downloadTo}], and its integerity is checked."));
                    }
                    else
                    {
                        logs.Add(new LogInfo(LogState.Error, $"Downloaded [{url}], but it was corrupted"));
                    }
                }
                else
                { // No Hash
                    bool result = DownloadFile(s, url, destPath);
                    if (result)
                        logs.Add(new LogInfo(LogState.Success, $"[{url}] downloaded to [{downloadTo}]"));
                    else
                        logs.Add(new LogInfo(LogState.Error, $"Error occured while downloading [{url}]"));
                }
            }
            finally
            {
                s.MainViewModel.BuildCommandProgressShow = false;
                s.MainViewModel.BuildCommandProgressTitle = "Progress";
                s.MainViewModel.BuildCommandProgressText = string.Empty;
                s.MainViewModel.BuildCommandProgressValue = 0;
            }

            return logs;
        }

        #region Utility
        /// <summary>
        /// Return true if success
        /// </summary>
        /// <param name="s"></param>
        /// <param name="url"></param>
        /// <param name="destPath"></param>
        /// <returns></returns>
        private static bool DownloadFile(EngineState s, string url, string destPath)
        {
            Uri uri = new Uri(url);

            bool result = true;
            Stopwatch watch = Stopwatch.StartNew();
            using (WebClient client = new WebClient())
            {
                client.DownloadProgressChanged += (object sender, DownloadProgressChangedEventArgs e) =>
                {
                    s.MainViewModel.BuildCommandProgressValue = e.ProgressPercentage;

                    TimeSpan t = watch.Elapsed;
                    double totalSec = t.TotalSeconds;
                    string downloaded = NumberHelper.ByteSizeToHumanReadableString(e.BytesReceived, 1);
                    string total = NumberHelper.ByteSizeToHumanReadableString(e.TotalBytesToReceive, 1);
                    if (totalSec == 0)
                    {
                        s.MainViewModel.BuildCommandProgressText = $"{url}\r\nTotal : {total}\r\nReceived : {downloaded}";
                    }
                    else
                    {
                        long bytePerSec = (long)(e.BytesReceived / totalSec); // Byte per sec
                        string speedStr = NumberHelper.ByteSizeToHumanReadableString((long)(e.BytesReceived / totalSec), 1) + "/s"; // KB/s, MB/s, ...

                        TimeSpan r = TimeSpan.FromSeconds((e.TotalBytesToReceive - e.BytesReceived) / bytePerSec);
                        int hour = (int)r.TotalHours;
                        int min = r.Minutes;
                        int sec = r.Seconds;
                        s.MainViewModel.BuildCommandProgressText = $"{url}\r\nTotal : {total}\r\nReceived : {downloaded}\r\nSpeed : {speedStr}\r\nRemaining Time : {hour}h {min}m {sec}s";
                    }
                };

                AutoResetEvent resetEvent = new AutoResetEvent(false);
                client.DownloadFileCompleted += (object sender, AsyncCompletedEventArgs e) =>
                {
                    s.RunningWebClient = null;

                    // Check if error occured
                    if (e.Cancelled || e.Error != null)
                    {
                        result = false;

                        if (File.Exists(destPath))
                            File.Delete(destPath);
                    }

                    resetEvent.Set();
                };

                s.RunningWebClient = client;
                client.DownloadFileAsync(uri, destPath);

                resetEvent.WaitOne();
            }
            watch.Stop();

            return result;
        }
        #endregion
    }
}
