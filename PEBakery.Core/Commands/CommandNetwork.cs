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
            int timeOut = 10;
            if (info.TimeOut != null)
            {
                string timeOutStr = StringEscaper.Preprocess(s, info.TimeOut);
                if (!NumberHelper.ParseInt32(timeOutStr, out timeOut))
                    return LogInfo.LogErrorMessage(logs, $"TimeOut [{timeOutStr}] is not a valid positive integer");
                if (timeOut <= 0)
                    return LogInfo.LogErrorMessage(logs, $"TimeOut [{timeOutStr}] is not a valid positive integer");
            }

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

                    HttpFileDownloader downloader = new HttpFileDownloader(s.MainViewModel, timeOut, s.CustomUserAgent);
                    HttpFileDownloader.Report report;
                    try
                    {
                        CancellationTokenSource ct = new CancellationTokenSource();
                        s.CancelWebGet = ct;

                        Task<HttpFileDownloader.Report> task = downloader.Download(url, tempPath, ct.Token);
                        task.Wait();

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
                        logs.Add(new LogInfo(state, $"Error occured while downloading [{url}]"));
                        logs.Add(new LogInfo(LogState.Info, report.ErrorMsg));
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

                    HttpFileDownloader downloader = new HttpFileDownloader(s.MainViewModel, timeOut, s.CustomUserAgent);
                    HttpFileDownloader.Report report;
                    try
                    {
                        CancellationTokenSource ct = new CancellationTokenSource();
                        s.CancelWebGet = ct;

                        Task<HttpFileDownloader.Report> task = downloader.Download(url, tempPath, ct.Token);
                        task.Wait();

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
                        logs.Add(new LogInfo(LogState.Info, report.ErrorMsg));
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
    }
}
