/*
    Copyright (C) 2016-2018 Hajin Jang
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

            if (!StringEscaper.PathSecurityCheck(downloadTo, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

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

                    logs.Add(new LogInfo(LogState.Overwrite, $"File [{downloadTo}] will be overwritten"));
                }
                else
                {
                    Directory.CreateDirectory(FileHelper.GetDirNameEx(downloadTo));
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

                    (bool result, WebException e) = DownloadFile(s, url, tempPath);
                    if (result == false)
                    {
                        logs.Add(new LogInfo(LogState.Error, $"Error occured while downloading [{url}]"));
                        if (e.Response is HttpWebResponse res)
                        {
                            logs.Add(new LogInfo(LogState.Info, $"Response returned HTTP Status Code [{(int)res.StatusCode}]"));
                            /*
                            int statusCode = (int)res.StatusCode;
                            LogInfo log = Variables.SetVariable(s, "%StatusCode%", statusCode.ToString()).First();
                            if (log.State == LogState.Success)
                                logs.Add(new LogInfo(LogState.Info, $"HTTP Status Code [{statusCode}] saved into variable [%StatusCode%]"));
                            else
                                logs.Add(log);
                            */
                        }

                        return logs;
                    }

                    string hashTypeStr = StringEscaper.Preprocess(s, info.HashType);
                    string hashDigest = StringEscaper.Preprocess(s, info.HashDigest);

                    HashHelper.HashType hashType = HashHelper.ParseHashType(hashTypeStr);
                    int byteLen = HashHelper.GetHashByteLen(hashType);
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
                        logs.Add(new LogInfo(LogState.Success, $"[{url}] was downloaded to [{downloadTo}] and it's integerity was verified."));
                    }
                    else
                    {
                        logs.Add(new LogInfo(LogState.Error, $"Downloaded [{url}], but the file was corrupted"));
                    }
                }
                else
                { // No Hash
                    (bool result, WebException e) = DownloadFile(s, url, destPath);
                    if (result)
                    {
                        logs.Add(new LogInfo(LogState.Success, $"[{url}] downloaded to [{downloadTo}]"));
                    }
                    else
                    {
                        logs.Add(new LogInfo(LogState.Error, $"Error occured while downloading [{url}]"));
                        if (e.Response is HttpWebResponse res)
                        {
                            logs.Add(new LogInfo(LogState.Info, $"Response returned HTTP Status Code [{(int)res.StatusCode}]"));
                            /*
                            int statusCode = (int) res.StatusCode;
                            LogInfo log = Variables.SetVariable(s, "%StatusCode%", statusCode.ToString()).First();
                            if (log.State == LogState.Success)
                                logs.Add(new LogInfo(LogState.Success, $"HTTP Status Code [{statusCode}] saved into variable [%StatusCode%]"));
                            else
                                logs.Add(log);
                            */
                        }
                    }
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
        /// <returns></returns>
        private static (bool, WebException) DownloadFile(EngineState s, string url, string destPath)
        {
            Uri uri = new Uri(url);

            bool result = true;
            WebException webEx = null;
            Stopwatch watch = Stopwatch.StartNew();
            using (WebClient client = new WebClient())
            {
                string userAgent = s.CustomUserAgent ?? $"PEBakery/{Properties.Resources.EngineVersion}";
                client.Headers.Add("User-Agent", userAgent);

                client.DownloadProgressChanged += (object sender, DownloadProgressChangedEventArgs e) =>
                {
                    s.MainViewModel.BuildCommandProgressValue = e.ProgressPercentage;

                    TimeSpan t = watch.Elapsed;
                    double totalSec = t.TotalSeconds;
                    string downloaded = NumberHelper.ByteSizeToHumanReadableString(e.BytesReceived, 1);
                    string total = NumberHelper.ByteSizeToHumanReadableString(e.TotalBytesToReceive, 1);
                    if (Math.Abs(totalSec) < double.Epsilon)
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
                        webEx = e.Error as WebException;

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

            return (result, webEx);
        }
        #endregion
    }
}
