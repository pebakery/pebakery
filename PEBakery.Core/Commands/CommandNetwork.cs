﻿/*
    Copyright (C) 2016-2023 Hajin Jang
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
using System.Threading;
using System.Threading.Tasks;

namespace PEBakery.Core.Commands
{
    public static class CommandNetwork
    {
        public static List<LogInfo> WebGet(EngineState s, CodeCommand cmd)
        { // WebGet,<URL>,<DestPath>[<HashType>=<HashDigest>][,TimeOut=<Int>][,Referer=<URL>][,UserAgent=<Agent>][,NOERR]
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_WebGet info = (CodeInfo_WebGet)cmd.Info;

            string url = StringEscaper.Preprocess(s, info.URL);
            string destPath = StringEscaper.Preprocess(s, info.DestPath);
            int timeOut = 10;
            if (info.TimeOut != null)
            {
                string timeOutStr = StringEscaper.Preprocess(s, info.TimeOut);
                if (!NumberHelper.ParseInt32(timeOutStr, out timeOut))
                    return LogInfo.LogErrorMessage(logs, $"TimeOut [{timeOutStr}] is not a valid positive integer");
                if (timeOut <= 0)
                    return LogInfo.LogErrorMessage(logs, $"TimeOut [{timeOutStr}] is not a valid positive integer");
            }

            string? refererUrl = null;
            if (info.Referer != null)
                refererUrl = StringEscaper.Preprocess(s, info.Referer);

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
                // Set User-Agent to use
                // (1) Use Command's custom User-Agent
                // (2) Use EngineState's custom User-Agent
                // (3) Use PEBakery's default User-Agent
                string? userAgent = null;
                if (info.UserAgent != null)
                    userAgent = StringEscaper.Preprocess(s, info.UserAgent);
                else
                    userAgent = s.CustomUserAgent;

                if (info.HashType == HashType.None)
                { // Standard WebGet
                    string tempPath = FileHelper.GetTempFile(destFileExt);

                    HttpFileDownloader downloader = new HttpFileDownloader(s.MainViewModel, timeOut, userAgent, refererUrl);
                    HttpFileDownloader.Report report;
                    try
                    {
                        CancellationTokenSource ct = new CancellationTokenSource();
                        s.CancelWebGet = ct;

                        Task<HttpFileDownloader.Report> task = downloader.Download(url, tempPath, ct.Token);
                        task.Wait(ct.Token);

                        report = task.Result;
                    }
                    catch (Exception e)
                    {
                        report = new HttpFileDownloader.Report(false, 0, Logger.LogExceptionMessage(e));
                    }
                    finally
                    {
                        s.CancelWebGet = null;
                    }

                    int statusCode = report.StatusCode;
                    if (report.Result)
                    {
                        FileHelper.FileReplaceEx(tempPath, destFile);
                        logs.Add(new LogInfo(LogState.Success, $"[{destFile}] downloaded from [{url}]"));
                    }
                    else
                    {
                        LogState state = info.NoErrFlag ? LogState.Warning : LogState.Error;
                        logs.Add(new LogInfo(state, $"An error occurred while downloading [{url}]"));
                        if (report.ErrorMsg != null)
                            logs.Add(new LogInfo(LogState.Info, report.ErrorMsg));
                        if (statusCode == 0)
                            logs.Add(new LogInfo(LogState.Info, "Request failed, no response was received from the server."));
                        else
                            logs.Add(new LogInfo(LogState.Info, $"The server responded with HTTP status code [{statusCode}]"));
                    }

                    // PEBakery extension -> Report exit code via #r
                    if (!s.CompatDisableExtendedSectionParams)
                    {
                        s.ReturnValue = statusCode.ToString();
                        if (statusCode < 100)
                            logs.Add(new LogInfo(LogState.Success, $"Returned [{statusCode}] into [#r]"));
                        else
                            logs.Add(new LogInfo(LogState.Success, $"Returned HTTP status code [{statusCode}] into [#r]"));
                    }
                }
                else
                { // Validate downloaded file with hash
                    Debug.Assert(info.HashDigest != null);

                    string tempPath = FileHelper.GetTempFile(destFileExt);

                    HttpFileDownloader downloader = new HttpFileDownloader(s.MainViewModel, timeOut, userAgent, refererUrl);
                    HttpFileDownloader.Report report;
                    try
                    {
                        CancellationTokenSource ct = new CancellationTokenSource();
                        s.CancelWebGet = ct;

                        Task<HttpFileDownloader.Report> task = downloader.Download(url, tempPath, ct.Token);
                        task.Wait(ct.Token);

                        report = task.Result;
                    }
                    catch (Exception e)
                    {
                        report = new HttpFileDownloader.Report(false, 0, Logger.LogExceptionMessage(e));
                    }
                    finally
                    {
                        s.CancelWebGet = null;
                    }

                    int statusCode = report.StatusCode;
                    if (report.Result)
                    { // Success -> Check hash
                        string hashDigest = StringEscaper.Preprocess(s, info.HashDigest);
                        if (hashDigest.Length != 2 * HashHelper.GetHashByteLen(info.HashType))
                            return LogInfo.LogErrorMessage(logs, $"[{hashDigest}] is not a valid [{info.HashType}] hash digest");

                        string downDigest;
                        using (FileStream fs = new FileStream(tempPath, FileMode.Open, FileAccess.Read))
                        {
                            byte[] digest = HashHelper.GetHash(info.HashType, fs);
                            downDigest = StringHelper.ToHexStr(digest);
                        }

                        if (hashDigest.Equals(downDigest, StringComparison.OrdinalIgnoreCase)) // Success
                        {
                            FileHelper.FileReplaceEx(tempPath, destFile);
                            logs.Add(new LogInfo(LogState.Success, $"[{destFile}] downloaded from [{url}] and verified with a [{info.HashType}] hash digest"));
                        }
                        else
                        {
                            statusCode = 1; // 1 means hash mismatch
                            logs.Add(new LogInfo(LogState.Error, $"The [{info.HashType}] hash [{downDigest}] of file downloaded from [{url}] does not match [{hashDigest}]. The file may be corrupt."));
                        }
                    }
                    else
                    { // Failure -> Log error message
                        LogState state = info.NoErrFlag ? LogState.Warning : LogState.Error;
                        logs.Add(new LogInfo(state, $"An error occurred while downloading [{url}]"));
                        if (report.ErrorMsg != null)
                            logs.Add(new LogInfo(LogState.Info, report.ErrorMsg));
                        if (statusCode == 0)
                            logs.Add(new LogInfo(LogState.Info, "Request failed, no response was received from the server."));
                        else
                            logs.Add(new LogInfo(LogState.Info, $"The server responded with HTTP Status Code [{statusCode}]"));
                    }

                    // PEBakery extension -> Report exit code via #r
                    if (!s.CompatDisableExtendedSectionParams)
                    {
                        s.ReturnValue = statusCode.ToString();
                        if (statusCode < 100)
                            logs.Add(new LogInfo(LogState.Success, $"Returned [{statusCode}] into [#r]"));
                        else
                            logs.Add(new LogInfo(LogState.Success, $"Returned HTTP status code [{statusCode}] into [#r]"));
                    }
                }
            }
            finally
            {
                s.MainViewModel.ResetBuildCommandProgress();
            }

            return logs;
        }
    }
}
