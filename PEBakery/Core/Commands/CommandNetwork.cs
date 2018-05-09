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

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_WebGet), "Invalid CodeInfo");
            CodeInfo_WebGet info = cmd.Info as CodeInfo_WebGet;
            Debug.Assert(info != null, "Invalid CodeInfo");

            string url = StringEscaper.Preprocess(s, info.URL);
            string destPath = StringEscaper.Preprocess(s, info.DestPath);
            const string destVar = @"%StatusCode%";

            // Check PathSecurity in destPath
            if (!StringEscaper.PathSecurityCheck(destPath, out string pathErrorMsg))
                return LogInfo.LogErrorMessage(logs, pathErrorMsg);

            Uri uri = new Uri(url);
            string destFile;
            if (Directory.Exists(destPath)) // downloadTo is dir
            {
                destFile = Path.Combine(destPath, Path.GetFileName(uri.LocalPath));
            }
            else // downloadTo is file
            {
                if (File.Exists(destPath))
                {
                    if (cmd.Type == CodeType.WebGetIfNotExist)
                    {
                        logs.Add(new LogInfo(LogState.Ignore, $"File [{destPath}] already exists"));
                        return logs;
                    }

                    logs.Add(new LogInfo(LogState.Overwrite, $"File [{destPath}] will be overwritten"));
                }
                else
                {
                    Directory.CreateDirectory(FileHelper.GetDirNameEx(destPath));
                }
                destFile = destPath;
            }

            s.MainViewModel.BuildCommandProgressTitle = "WebGet Progress";
            s.MainViewModel.BuildCommandProgressText = string.Empty;
            s.MainViewModel.BuildCommandProgressMax = 100;
            s.MainViewModel.BuildCommandProgressShow = true;
            try
            {
                if (info.HashType == HashHelper.HashType.None)
                { // Standard WebGet
                    (bool result, int statusCode, string errorMsg) = DownloadFile(s, url, destFile);
                    if (result)
                    {
                        logs.Add(new LogInfo(LogState.Success, $"[{destPath}] downloaded from [{url}]"));
                    }
                    else
                    {
                        LogState state = info.NoErrFlag ? LogState.Warning : LogState.Error;
                        logs.Add(new LogInfo(state, $"Error occured while downloading [{url}]"));
                        logs.Add(new LogInfo(LogState.Info, errorMsg));
                        if (statusCode == 0)
                            logs.Add(new LogInfo(LogState.Info, "Request failed, no response received."));
                        else
                            logs.Add(new LogInfo(LogState.Info, $"Response returned HTTP Status Code [{statusCode}]"));
                    }

                    List<LogInfo> varLogs = Variables.SetVariable(s, destVar, statusCode.ToString());
                    logs.AddRange(varLogs);
                }
                else
                { // Validate downloaded file with hash
                    Debug.Assert(info.HashDigest != null);

                    string tempPath = Path.GetTempFileName();
                    (bool result, int statusCode, string errorMsg) = DownloadFile(s, url, tempPath);
                    if (result)
                    { // Success -> Check hash
                        string hashDigest = StringEscaper.Preprocess(s, info.HashDigest);
                        int byteLen = HashHelper.GetHashByteLen(info.HashType);
                        if (hashDigest.Length != byteLen)
                            return LogInfo.LogErrorMessage(logs, $"Hash digest [{hashDigest}] is not [{info.HashType}]");

                        string downDigest;
                        using (FileStream fs = new FileStream(tempPath, FileMode.Open, FileAccess.Read))
                        {
                            downDigest = HashHelper.CalcHashString(info.HashType, fs);
                        }

                        if (hashDigest.Equals(downDigest, StringComparison.OrdinalIgnoreCase)) // Success
                        {
                            File.Move(tempPath, destFile);
                            logs.Add(new LogInfo(LogState.Success, $"[{destPath}] downloaded from [{url}] and verified "));
                        }
                        else
                        {
                            statusCode = 1;
                            logs.Add(new LogInfo(LogState.Error, $"Downloaded file from [{url}] was corrupted"));
                        }
                    }
                    else
                    { // Failure -> Log error message
                        LogState state = info.NoErrFlag ? LogState.Warning : LogState.Error;
                        logs.Add(new LogInfo(state, $"Error occured while downloading [{url}]"));
                        logs.Add(new LogInfo(LogState.Info, errorMsg));
                        if (statusCode == 0)
                            logs.Add(new LogInfo(LogState.Info, "Request failed, no response received."));
                        else
                            logs.Add(new LogInfo(LogState.Info, $"Response returned HTTP Status Code [{statusCode}]"));
                    }

                    List<LogInfo> varLogs = Variables.SetVariable(s, destVar, statusCode.ToString());
                    logs.AddRange(varLogs);
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
        private static (bool, int, string) DownloadFile(EngineState s, string url, string destPath)
        {
            Uri uri = new Uri(url);

            bool result = true;
            int statusCode = 200;
            string errorMsg = null;
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
                    string downloaded = NumberHelper.ByteSizeToSIUnit(e.BytesReceived, 1);
                    string total = NumberHelper.ByteSizeToSIUnit(e.TotalBytesToReceive, 1);
                    if (Math.Abs(totalSec) < double.Epsilon)
                    {
                        s.MainViewModel.BuildCommandProgressText = $"{url}\r\nTotal : {total}\r\nReceived : {downloaded}";
                    }
                    else
                    {
                        long bytePerSec = (long)(e.BytesReceived / totalSec); // Byte per sec
                        string speedStr = NumberHelper.ByteSizeToSIUnit((long)(e.BytesReceived / totalSec), 1) + "/s"; // KB/s, MB/s, ...

                        // ReSharper disable once PossibleLossOfFraction
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
                        statusCode = 0; // Unknown Error
                        if (e.Error is WebException webEx)
                        {
                            errorMsg = $"[{webEx.Status}] {webEx.Message}";
                            if (webEx.Response is HttpWebResponse res)
                                statusCode = (int)res.StatusCode;
                        }

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

            return (result, statusCode, errorMsg);
        }
        #endregion
    }
}
