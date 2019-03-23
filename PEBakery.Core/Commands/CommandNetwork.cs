/*
    Copyright (C) 2016-2019 Hajin Jang
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
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PEBakery.Core.Commands
{
    public static class CommandNetwork
    {
        public static List<LogInfo> WebGet(EngineState s, CodeCommand cmd)
        { // WebGet,<URL>,<DestPath>,[HashType],[HashDigest]
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_WebGet info = cmd.Info.Cast<CodeInfo_WebGet>();

            string url = StringEscaper.Preprocess(s, info.URL);
            string destPath = StringEscaper.Preprocess(s, info.DestPath);

            // Check PathSecurity in destPath
            if (!StringEscaper.PathSecurityCheck(destPath, out string pathErrorMsg))
                return LogInfo.LogErrorMessage(logs, pathErrorMsg);

            Uri uri = new Uri(url);
            string destFile;
            if (Directory.Exists(destPath))
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

            string destFileExt = Path.GetExtension(destFile);

            s.MainViewModel.SetBuildCommandProgress("WebGet Progress");
            try
            {
                if (info.HashType == HashHelper.HashType.None)
                { // Standard WebGet
                    string tempPath = FileHelper.GetTempFile(destFileExt);
                    var task = DownloadHttpFile(s, url, tempPath);
                    task.Wait();
                    (bool result, int statusCode, string errorMsg) = task.Result;
                    if (result)
                    {
                        FileHelper.FileReplaceEx(tempPath, destFile);
                        logs.Add(new LogInfo(LogState.Success, $"[{destFile}] downloaded from [{url}]"));
                    }
                    else
                    {
                        LogState state = info.NoErrFlag ? LogState.Warning : LogState.Error;
                        logs.Add(new LogInfo(state, $"Error occured while downloading [{url}]"));
                        logs.Add(new LogInfo(LogState.Info, errorMsg));
                        if (statusCode == 0)
                            logs.Add(new LogInfo(LogState.Info, "Request failed, no response received."));
                        else
                            logs.Add(new LogInfo(LogState.Info, $"Response returned HTTP status code [{statusCode}]"));
                    }

                    // PEBakery extension -> Report exit code via #r
                    if (!s.CompatDisableExtendedSectionParams)
                    {
                        s.ReturnValue = statusCode.ToString();
                        if (statusCode < 100)
                            logs.Add(new LogInfo(LogState.Success, $"Returned [{statusCode}] into [#r]"));
                        else
                            logs.Add(new LogInfo(LogState.Success, $"Returned HTTP status code [{statusCode}] to [#r]"));
                    }
                }
                else
                { // Validate downloaded file with hash
                    Debug.Assert(info.HashDigest != null);

                    string tempPath = FileHelper.GetTempFile(destFileExt);
                    var task = DownloadHttpFile(s, url, tempPath);
                    task.Wait();
                    (bool result, int statusCode, string errorMsg) = task.Result;
                    if (result)
                    { // Success -> Check hash
                        string hashDigest = StringEscaper.Preprocess(s, info.HashDigest);
                        if (hashDigest.Length != 2 * HashHelper.GetHashByteLen(info.HashType))
                            return LogInfo.LogErrorMessage(logs, $"Hash digest [{hashDigest}] is not [{info.HashType}]");

                        string downDigest;
                        using (FileStream fs = new FileStream(tempPath, FileMode.Open, FileAccess.Read))
                        {
                            byte[] digest = HashHelper.GetHash(info.HashType, fs);
                            downDigest = StringHelper.ToHexStr(digest);
                        }

                        if (hashDigest.Equals(downDigest, StringComparison.OrdinalIgnoreCase)) // Success
                        {
                            FileHelper.FileReplaceEx(tempPath, destFile);
                            logs.Add(new LogInfo(LogState.Success, $"[{destFile}] downloaded from [{url}] and verified "));
                        }
                        else
                        {
                            statusCode = 1; // 1 means hash mismatch
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
                    
                    // PEBakery extension -> Report exit code via #r
                    if (!s.CompatDisableExtendedSectionParams)
                    {
                        s.ReturnValue = statusCode.ToString();
                        if (statusCode < 100)
                            logs.Add(new LogInfo(LogState.Success, $"Returned [{statusCode}] into [#r]"));
                        else
                            logs.Add(new LogInfo(LogState.Success, $"Returned HTTP status code [{statusCode}] to [#r]"));
                    }
                }
            }
            finally
            {
                s.MainViewModel.ResetBuildCommandProgress();
            }

            return logs;
        }

        #region Utility
        /// <summary>
        /// Download a file with HttpClient.
        /// </summary>
        /// <returns>true in case of success.</returns>
        private static async Task<(bool Result, int StatusCode, string ErrorMsg)> DownloadHttpFile(EngineState s, string url, string destPath)
        {
            Uri uri = new Uri(url);

            bool result;
            HttpStatusCode statusCode;
            string errorMsg = null;
            using (HttpClientHandler handler = new HttpClientHandler())
            {
                handler.AllowAutoRedirect = true;
                handler.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;

                using (HttpClient client = new HttpClient(handler))
                {
                    // Set Timeout
                    client.Timeout = TimeSpan.FromSeconds(10);

                    // User Agent
                    string userAgent = s.CustomUserAgent ?? Engine.DefaultUserAgent;
                    client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);

                    // Progress Report
                    Progress<(long Position, long ContentLength, TimeSpan Elapsed)> progress = new Progress<(long, long, TimeSpan)>(x =>
                    {
                        (long position, long contentLength, TimeSpan t) = x;
                        string elapsedStr = $"Elapsed Time: {(int)t.TotalHours}h {t.Minutes}m {t.Seconds}s";

                        if (0 < contentLength)
                        { // Server returned proper content length.
                            Debug.Assert(position <= contentLength);
                            double percent = position * 100.0 / contentLength;
                            s.MainViewModel.BuildCommandProgressValue = percent;

                            string receivedStr = $"Received : {NumberHelper.ByteSizeToSIUnit(position, 1)} ({percent:0.0}%)";

                            int totalSec = (int)t.TotalSeconds;
                            string total = NumberHelper.ByteSizeToSIUnit(contentLength, 1);
                            if (totalSec == 0)
                            {
                                s.MainViewModel.BuildCommandProgressText = $"{url}\r\nTotal : {total}\r\n{receivedStr}\r\n{elapsedStr}";
                            }
                            else
                            {
                                long bytePerSec = position / totalSec; // Byte per sec
                                string speedStr = NumberHelper.ByteSizeToSIUnit(bytePerSec, 1) + "/s"; // KB/s, MB/s, ...

                                // ReSharper disable once PossibleLossOfFraction
                                TimeSpan r = TimeSpan.FromSeconds((contentLength - position) / bytePerSec);
                                string remainStr = $"Remaining Time : {(int)r.TotalHours}h {r.Minutes}m {r.Seconds}s";
                                s.MainViewModel.BuildCommandProgressText = $"{url}\r\nTotal : {total}\r\n{receivedStr}\r\nSpeed : {speedStr}\r\n{elapsedStr}\r\n{remainStr}";
                            }
                        }
                        else
                        { // Ex) Response do not have content length info. Ex) Google Drive
                            if (!s.MainViewModel.BuildCommandProgressIndeterminate)
                                s.MainViewModel.BuildCommandProgressIndeterminate = true;

                            string receivedStr = $"Received : {NumberHelper.ByteSizeToSIUnit(position, 1)}";

                            int totalSec = (int)t.TotalSeconds;
                            if (totalSec == 0)
                            {
                                s.MainViewModel.BuildCommandProgressText = $"{url}\r\n{receivedStr}\r\n{elapsedStr}";
                            }
                            else
                            {
                                long bytePerSec = position / totalSec; // Byte per sec
                                string speedStr = NumberHelper.ByteSizeToSIUnit(bytePerSec, 1) + "/s"; // KB/s, MB/s, ...
                                s.MainViewModel.BuildCommandProgressText = $"{url}\r\n{receivedStr}\r\nSpeed : {speedStr}\r\n{elapsedStr}";
                            }
                        }
                    });

                    // Cancel Token
                    CancellationTokenSource ct = new CancellationTokenSource();
                    s.CancelWebGet = ct;

                    // Download file from uri
                    using (FileStream fs = new FileStream(destPath, FileMode.Create, FileAccess.Write))
                    {
                        TimeSpan reportInterval = TimeSpan.FromSeconds(1);
                        HttpClientDownloader downloader = new HttpClientDownloader(client, uri, fs, progress, reportInterval, ct.Token);
                        try
                        {
                            await downloader.DownloadAsync();

                            Debug.Assert(downloader.StatusCode != null, "Successful HTTP response must have status code.");
                            statusCode = (HttpStatusCode)downloader.StatusCode;

                            result = true;
                        }
                        catch (HttpRequestException e)
                        {
                            if (downloader.StatusCode == null)
                                statusCode = 0; // Unable to send a request. Ex) Network not available
                            else
                                statusCode = (HttpStatusCode)downloader.StatusCode;

                            result = false;
                            errorMsg = $"[{(int)statusCode}] {e.Message}";
                        }
                    }
                }

            }

            if (!result)
            { // Download failed, delete file
                if (File.Exists(destPath))
                    File.Delete(destPath);
            }

            s.CancelWebGet = null;
            return (result, (int)statusCode, errorMsg);
        }
        #endregion
    }
}
