﻿/*
    Copyright (C) 2017-2023 Hajin Jang
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

using ManagedWimLib;
using Microsoft.Wim;
using PEBakery.Helper;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using WimInfo = ManagedWimLib.WimInfo;

namespace PEBakery.Core.Commands
{
    public static class CommandWim
    {
        #region Wimgapi - WimMount, WimUnmount
        public static List<LogInfo> WimMount(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>(1);

            CodeInfo_WimMount info = (CodeInfo_WimMount)cmd.Info;

            string srcWim = StringEscaper.Preprocess(s, info.SrcWim);
            string imageIndexStr = StringEscaper.Preprocess(s, info.ImageIndex);
            string mountDir = StringEscaper.Preprocess(s, info.MountDir);
            string mountOptionStr = StringEscaper.Preprocess(s, info.MountOption);

            // Mount Option
            bool readwrite;
            if (mountOptionStr.Equals("READONLY", StringComparison.OrdinalIgnoreCase))
                readwrite = false;
            else if (mountOptionStr.Equals("READWRITE", StringComparison.OrdinalIgnoreCase))
                readwrite = true;
            else
                return LogInfo.LogErrorMessage(logs, $"Invalid mount option [{mountOptionStr}]");

            // Check srcWim
            if (!File.Exists(srcWim))
                return LogInfo.LogErrorMessage(logs, $"File [{srcWim}] does not exist");

            // Check MountDir 
            if (StringEscaper.PathSecurityCheck(mountDir, out string errorMsg) == false)
                return LogInfo.LogErrorMessage(logs, errorMsg);

            if (!Directory.Exists(mountDir))
                return LogInfo.LogErrorMessage(logs, $"Directory [{mountDir}] does not exist");

            // Check imageIndex
            int imageCount;
            string tempDir = FileHelper.GetTempDir();
            try
            {
                using (WimHandle hWim = WimgApi.CreateFile(srcWim,
                    WimFileAccess.Query,
                    WimCreationDisposition.OpenExisting,
                    WimCreateFileOptions.None,
                    WimCompressionType.None))
                {
                    WimgApi.SetTemporaryPath(hWim, tempDir);
                    imageCount = WimgApi.GetImageCount(hWim);
                }
            }
            catch (Win32Exception e)
            {
                logs.Add(LogWimgApiException(e, $"Unable to get information from [{srcWim}]"));
                return logs;
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }

            if (!NumberHelper.ParseInt32(imageIndexStr, out int imageIndex))
            {
                logs.Add(new LogInfo(LogState.Error, $"[{imageIndexStr}] is not a valid positive integer"));
                return logs;
            }

            if (!(1 <= imageIndex && imageIndex <= imageCount))
            {
                logs.Add(new LogInfo(LogState.Error, $"[{imageIndexStr}] must be [1] ~ [{imageCount}]"));
                return logs;
            }

            // Mount Wim
            WimFileAccess accessFlag = WimFileAccess.Mount | WimFileAccess.Read;
            WimMountImageOptions mountFlag = WimMountImageOptions.ReadOnly;
            if (readwrite)
            {
                accessFlag |= WimFileAccess.Write;
                mountFlag = WimMountImageOptions.None;
            }

            tempDir = FileHelper.GetTempDir();
            try
            {
                using (WimHandle hWim = WimgApi.CreateFile(srcWim,
                    accessFlag,
                    WimCreationDisposition.OpenExisting,
                    WimCreateFileOptions.None,
                    WimCompressionType.None))
                {

                    WimgApi.SetTemporaryPath(hWim, tempDir);

                    try
                    {
                        // Prepare Command Progress Report
                        WimgApi.RegisterMessageCallback(hWim, WimgApiMountCallback);

                        using (WimHandle hImage = WimgApi.LoadImage(hWim, imageIndex))
                        {
                            s.MainViewModel.SetBuildCommandProgress("WimMount Progress");

                            // Mount Wim
                            WimgApi.MountImage(hImage, mountDir, mountFlag);
                        }
                    }
                    catch (Win32Exception e)
                    {
                        logs.Add(LogWimgApiException(e, $"Unable to mount [{srcWim}]"));
                        return logs;
                    }
                    finally
                    {
                        s.MainViewModel.ResetBuildCommandProgress();
                        WimgApi.UnregisterMessageCallback(hWim, WimgApiMountCallback);
                    }
                }
            }
            catch (Win32Exception e)
            {
                logs.Add(LogWimgApiException(e, $"Unable to open [{srcWim}]"));
                return logs;
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }

            logs.Add(new LogInfo(LogState.Success, $"[{srcWim}]'s image [{imageIndex}] mounted to [{mountDir}]"));
            return logs;
        }

        public static List<LogInfo> WimUnmount(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>(1);

            CodeInfo_WimUnmount info = (CodeInfo_WimUnmount)cmd.Info;

            string mountDir = StringEscaper.Preprocess(s, info.MountDir);
            string unmountOptionStr = StringEscaper.Preprocess(s, info.UnmountOption);

            bool commit;
            if (unmountOptionStr.Equals("DISCARD", StringComparison.OrdinalIgnoreCase))
                commit = false;
            else if (unmountOptionStr.Equals("COMMIT", StringComparison.OrdinalIgnoreCase))
                commit = true;
            else
                return LogInfo.LogErrorMessage(logs, $"Invalid unmount option [{unmountOptionStr}]");

            // Check MountDir 
            if (!Directory.Exists(mountDir))
                return LogInfo.LogErrorMessage(logs, $"Directory [{mountDir}] does not exist");

            // Unmount Wim
            // https://msdn.microsoft.com/ko-kr/library/windows/desktop/dd834953.aspx
            WimHandle? hWim = null;
            WimHandle? hImage = null;
            try
            {
                hWim = WimgApi.GetMountedImageHandle(mountDir, !commit, out hImage);

                WimMountInfo wimInfo = WimgApi.GetMountedImageInfoFromHandle(hImage);
                Debug.Assert(wimInfo.MountPath.Equals(mountDir, StringComparison.OrdinalIgnoreCase));

                // Prepare Command Progress Report
                WimgApi.RegisterMessageCallback(hWim, WimgApiUnmountCallback);
                s.MainViewModel.SetBuildCommandProgress("WimUnmount Progress");

                try
                {
                    // Commit 
                    if (commit)
                    {
                        try
                        {
                            WimgApi.CommitImageHandle(hImage, false, WimCommitImageOptions.None);
                        }
                        catch (Win32Exception e)
                        {
                            logs.Add(LogWimgApiException(e, $"Unable to commit [{mountDir}] into [{wimInfo.Path}]"));
                            return logs;
                        }

                        logs.Add(new LogInfo(LogState.Success, $"Commited [{mountDir}] into [{wimInfo.Path}]'s index [{wimInfo.ImageIndex}]"));
                    }

                    // Unmount
                    try
                    {
                        WimgApi.UnmountImage(hImage);
                        logs.Add(new LogInfo(LogState.Success, $"Unmounted [{wimInfo.Path}]'s image [{wimInfo.ImageIndex}] from [{mountDir}]"));
                    }
                    catch (Win32Exception e)
                    {
                        logs.Add(LogWimgApiException(e, $"Unable to unmount [{mountDir}]"));
                        return logs;
                    }
                }
                finally
                { // Finalize Command Progress Report
                    s.MainViewModel.ResetBuildCommandProgress();
                    WimgApi.UnregisterMessageCallback(hWim, WimgApiUnmountCallback);
                }
            }
            catch (Win32Exception e)
            {
                logs.Add(LogWimgApiException(e, $"Unable to get mounted wim information from [{mountDir}]"));
                return logs;
            }
            finally
            {
                hImage?.Close();
                hWim?.Close();
            }

            return logs;
        }

        private static WimMessageResult WimgApiMountCallback(WimMessageType msgType, object msg, object userData)
        { // https://github.com/josemesona/ManagedWimgApi/wiki/Message-Callbacks
            Debug.Assert(Engine.WorkingEngine != null);
            EngineState s = Engine.WorkingEngine.State;

            switch (msgType)
            {
                case WimMessageType.Progress:
                    { // For Mount
                        WimMessageProgress wMsg = (WimMessageProgress)msg;

                        s.MainViewModel.BuildCommandProgressValue = wMsg.PercentComplete;

                        if (0 < wMsg.EstimatedTimeRemaining.TotalSeconds)
                        {
                            int min = (int)wMsg.EstimatedTimeRemaining.TotalMinutes;
                            int sec = wMsg.EstimatedTimeRemaining.Seconds;
                            s.MainViewModel.BuildCommandProgressText = $"Mounting image... ({wMsg.PercentComplete}%)\r\nRemaining Time: {min}m {sec}s";
                        }
                        else
                        {
                            s.MainViewModel.BuildCommandProgressText = $"Mounting image... ({wMsg.PercentComplete}%)";
                        }
                    }
                    break;
            }

            return WimMessageResult.Success;
        }

        private static WimMessageResult WimgApiUnmountCallback(WimMessageType msgType, object msg, object userData)
        { // https://github.com/josemesona/ManagedWimgApi/wiki/Message-Callbacks
            Debug.Assert(Engine.WorkingEngine != null);
            EngineState s = Engine.WorkingEngine.State;

            switch (msgType)
            {
                case WimMessageType.Progress:
                    { // For Commit
                        WimMessageProgress wMsg = (WimMessageProgress)msg;

                        s.MainViewModel.BuildCommandProgressValue = wMsg.PercentComplete;

                        if (0 < wMsg.EstimatedTimeRemaining.TotalSeconds)
                        {
                            int min = (int)wMsg.EstimatedTimeRemaining.TotalMinutes;
                            int sec = wMsg.EstimatedTimeRemaining.Seconds;
                            s.MainViewModel.BuildCommandProgressText = $"Saving image... ({wMsg.PercentComplete}%)\r\nRemaining Time: {min}m {sec}s";
                        }
                        else
                        {
                            s.MainViewModel.BuildCommandProgressText = $"Saving image... ({wMsg.PercentComplete}%)";
                        }
                    }
                    break;
                case WimMessageType.MountCleanupProgress:
                    { // For Unmount
                        WimMessageMountCleanupProgress wMsg = (WimMessageMountCleanupProgress)msg;

                        s.MainViewModel.BuildCommandProgressValue = wMsg.PercentComplete;

                        if (0 < wMsg.EstimatedTimeRemaining.TotalSeconds)
                        {
                            int min = (int)wMsg.EstimatedTimeRemaining.TotalMinutes;
                            int sec = wMsg.EstimatedTimeRemaining.Seconds;
                            s.MainViewModel.BuildCommandProgressText = $"Unmounting image... ({wMsg.PercentComplete}%)\r\nRemaining Time: {min}m {sec}s";
                        }
                        else
                        {
                            s.MainViewModel.BuildCommandProgressText = $"Unmounting image... ({wMsg.PercentComplete}%)";
                        }
                    }
                    break;
            }

            return WimMessageResult.Success;
        }

        private static LogInfo LogWimgApiException(Win32Exception e, string msg)
        {
            return new LogInfo(LogState.Error, $"{msg}\r\nError Code [0x{e.ErrorCode:X8}]\r\nNative Error Code [0x{e.NativeErrorCode:X8}]\r\n");
        }
        #endregion

        #region WimLib - WimInfo
        public static List<LogInfo> WimInfo(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>(1);

            CodeInfo_WimInfo info = (CodeInfo_WimInfo)cmd.Info;

            string srcWim = StringEscaper.Preprocess(s, info.SrcWim);
            string imageIndexStr = StringEscaper.Preprocess(s, info.ImageIndex);
            string key = StringEscaper.Preprocess(s, info.Key);

            // Check SrcWim
            if (!File.Exists(srcWim))
                return LogInfo.LogErrorMessage(logs, $"File [{srcWim}] does not exist");

            try
            {
                using (Wim wim = Wim.OpenWim(srcWim, OpenFlags.None))
                {
                    WimInfo wi = wim.GetWimInfo();

                    // Check imageIndex
                    if (!NumberHelper.ParseInt32(imageIndexStr, out int imageIndex))
                        return LogInfo.LogErrorMessage(logs, $"[{imageIndexStr}] is not a valid positive integer");
                    if (!(0 <= imageIndex && imageIndex <= wi.ImageCount))
                        return LogInfo.LogErrorMessage(logs, $"[{imageIndexStr}] must be [0] or [1] ~ [{wi.ImageCount}]");

                    string dest;
                    if (imageIndex == 0)
                    { // Generic wim file information
                        if (key.Equals("ImageCount", StringComparison.OrdinalIgnoreCase))
                            dest = wi.ImageCount.ToString();
                        else if (key.Equals("BootIndex", StringComparison.OrdinalIgnoreCase))
                            dest = wi.BootIndex.ToString(); // 0 -> No Boot Index (follow wimlib convention)
                        else if (key.Equals("Compression", StringComparison.OrdinalIgnoreCase))
                            dest = wi.CompressionType.ToString(); // NONE, LZX, XPRESS, LZMS
                        else
                            return LogInfo.LogErrorMessage(logs, $"Invalid property key [{key}]");
                    }
                    else
                    { // Per image information
                        // Arg <Key> follows wimlib conventetion, more precisely wimlib_get_image_property().
                        // wimlib_get_image_property() is case sensitive, so use ToUpper() since most property key is uppercase.
                        // Ex) Name, Description
                        // To query non-standard property such as "Major Version", extract xml with wimlib-imagex first and inspect hierarchy.
                        // Ex) Major Version => WINDOWS/VERSION/MAJOR
                        dest = wim.GetImageProperty(imageIndex, key.ToUpper());
                        if (dest == null)
                        {
                            if (info.NoErrFlag)
                            {
                                logs.Add(new LogInfo(LogState.Ignore, $"Invalid property key [{key}]"));
                                return logs;
                            }

                            return LogInfo.LogErrorMessage(logs, $"Invalid property key [{key}]");
                        }
                    }

                    logs.AddRange(Variables.SetVariable(s, info.DestVar, dest));
                }
            }
            catch (WimLibException e)
            {
                logs.Add(LogWimLibException(e));
                return logs;
            }

            return logs;
        }
        #endregion

        #region WimLib - WimApply
        public static List<LogInfo> WimApply(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>(1);

            CodeInfo_WimApply info = (CodeInfo_WimApply)cmd.Info;

            string srcWim = StringEscaper.Preprocess(s, info.SrcWim);
            string imageIndexStr = StringEscaper.Preprocess(s, info.ImageIndex);
            string destDir = StringEscaper.Preprocess(s, info.DestDir);

            // Check SrcWim
            if (!File.Exists(srcWim))
                return LogInfo.LogErrorMessage(logs, $"File [{srcWim}] does not exist");

            // Check DestDir
            if (StringEscaper.PathSecurityCheck(destDir, out string errorMsg) == false)
                return LogInfo.LogErrorMessage(logs, errorMsg);

            if (!Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            // Set Flags
            OpenFlags openFlags = OpenFlags.None;
            ExtractFlags extractFlags = ExtractFlags.None;
            if (info.CheckFlag)
                openFlags |= OpenFlags.CheckIntegrity;
            if (info.NoAclFlag)
                extractFlags |= ExtractFlags.NoAcls;
            if (info.NoAttribFlag)
                extractFlags |= ExtractFlags.NoAttributes;

            try
            {
                using (Wim wim = Wim.OpenWim(srcWim, openFlags, WimApplyExtractProgress, s))
                {
                    WimInfo wimInfo = wim.GetWimInfo();

                    // Check imageIndex
                    if (!NumberHelper.ParseInt32(imageIndexStr, out int imageIndex))
                        return LogInfo.LogErrorMessage(logs, $"[{imageIndexStr}] is not a valid positive integer");
                    if (!(1 <= imageIndex && imageIndex <= wimInfo.ImageCount))
                        return LogInfo.LogErrorMessage(logs, $"[{imageIndexStr}] must be [1] ~ [{wimInfo.ImageCount}]");

                    // Process split wim
                    if (info.Split != null)
                    {
                        string splitWim = StringEscaper.Preprocess(s, info.Split);

                        try
                        {
                            const RefFlags refFlags = RefFlags.GlobEnable | RefFlags.GlobErrOnNoMatch;
                            wim.ReferenceResourceFile(splitWim, refFlags, openFlags);
                        }
                        catch (WimLibException e) when (e.ErrorCode == ErrorCode.GlobHadNoMatches)
                        {
                            return LogInfo.LogErrorMessage(logs, $"Unable to find a match to [{splitWim}]");
                        }
                    }

                    // Apply to disk
                    s.MainViewModel.SetBuildCommandProgress("WimApply Progress");
                    try
                    {
                        wim.ExtractImage(imageIndex, destDir, extractFlags);

                        logs.Add(new LogInfo(LogState.Success, $"Applied [{srcWim}:{imageIndex}] to [{destDir}]"));
                    }
                    finally
                    { // Finalize Command Progress Report
                        s.MainViewModel.ResetBuildCommandProgress();
                    }
                }
            }
            catch (WimLibException e)
            {
                logs.Add(LogWimLibException(e));
                return logs;
            }

            return logs;
        }

        private static CallbackStatus WimApplyExtractProgress(ProgressMsg msg, object info, object progctx)
        {
            if (progctx is not EngineState s)
                return CallbackStatus.Continue;

            // EXTRACT_IMAGE_BEGIN
            // EXTRACT_FILE_STRUCTURE (Stage 1)
            // EXTRACT_STREAMS (Stage 2)
            // EXTRACT_METADATA (Stage 3)
            // EXTRACT_IMAGE_END
            switch (msg)
            {
                case ProgressMsg.ExtractFileStructure:
                    {
                        ExtractProgress m = (ExtractProgress)info;

                        if (0 < m.EndFileCount)
                        {
                            ulong percentComplete = m.CurrentFileCount * 10 / m.EndFileCount;
                            s.MainViewModel.BuildCommandProgressValue = percentComplete;
                            s.MainViewModel.BuildCommandProgressText = $"[Stage 1] Creating files... ({percentComplete}%)";
                        }
                    }
                    break;
                case ProgressMsg.ExtractStreams:
                    {
                        ExtractProgress m = (ExtractProgress)info;

                        if (0 < m.TotalBytes)
                        {
                            ulong percentComplete = 10 + m.CompletedBytes * 80 / m.TotalBytes;
                            s.MainViewModel.BuildCommandProgressValue = percentComplete;
                            s.MainViewModel.BuildCommandProgressText = $"[Stage 2] Extracting file data... ({percentComplete}%)";
                        }
                    }
                    break;
                case ProgressMsg.ExtractMetadata:
                    {
                        ExtractProgress m = (ExtractProgress)info;

                        if (0 < m.EndFileCount)
                        {
                            ulong percentComplete = 90 + m.CurrentFileCount * 10 / m.EndFileCount;
                            s.MainViewModel.BuildCommandProgressValue = percentComplete;
                            s.MainViewModel.BuildCommandProgressText = $"[Stage 3] Applying metadata to files... ({percentComplete}%)";
                        }
                    }
                    break;
                case ProgressMsg.CalcIntegrity:
                    {
                        IntegrityProgress m = (IntegrityProgress)info;

                        if (0 < m.TotalBytes)
                        {
                            ulong percentComplete = m.CompletedBytes * 100 / m.TotalBytes;
                            s.MainViewModel.BuildCommandProgressText = $"Calculating integrity... ({percentComplete}%)";
                        }
                    }
                    break;
            }
            return CallbackStatus.Continue;
        }
        #endregion

        #region WimLib - WimExtract, WimExtractOp, WimExtractBulk
        public static List<LogInfo> WimExtract(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>(1);

            CodeInfo_WimExtract info = (CodeInfo_WimExtract)cmd.Info;

            string srcWim = StringEscaper.Preprocess(s, info.SrcWim);
            string imageIndexStr = StringEscaper.Preprocess(s, info.ImageIndex);
            string destDir = StringEscaper.Preprocess(s, info.DestDir);
            string extractPath = StringEscaper.Preprocess(s, info.ExtractPath);

            // Check SrcWim
            if (!File.Exists(srcWim))
                return LogInfo.LogErrorMessage(logs, $"File [{srcWim}] does not exist");

            // Check DestDir
            if (!StringEscaper.PathSecurityCheck(destDir, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            if (!Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            // Set Flags
            OpenFlags openFlags = OpenFlags.None;
            ExtractFlags extractFlags = ExtractFlags.NoRpFix | ExtractFlags.NoPreserveDirStructure;
            if (info.CheckFlag)
                openFlags |= OpenFlags.CheckIntegrity;
            if (info.NoAclFlag)
                extractFlags |= ExtractFlags.NoAcls;
            if (info.NoAttribFlag)
                extractFlags |= ExtractFlags.NoAttributes;

            // Flags for globbing
            if (StringHelper.IsWildcard(extractPath))
                extractFlags |= ExtractFlags.GlobPaths;

            try
            {
                using (Wim wim = Wim.OpenWim(srcWim, openFlags, WimApplyExtractProgress, s))
                {
                    WimInfo wimInfo = wim.GetWimInfo();

                    // Check imageIndex
                    if (!NumberHelper.ParseInt32(imageIndexStr, out int imageIndex))
                        return LogInfo.LogErrorMessage(logs, $"[{imageIndexStr}] is not a valid positive integer");
                    if (!(1 <= imageIndex && imageIndex <= wimInfo.ImageCount))
                        return LogInfo.LogErrorMessage(logs, $"[{imageIndexStr}] must be [1] ~ [{wimInfo.ImageCount}]");

                    // Process split wim
                    if (info.Split != null)
                    {
                        string splitWim = StringEscaper.Preprocess(s, info.Split);

                        try
                        {
                            const RefFlags refFlags = RefFlags.GlobEnable | RefFlags.GlobErrOnNoMatch;
                            wim.ReferenceResourceFile(splitWim, refFlags, openFlags);
                        }
                        catch (WimLibException e) when (e.ErrorCode == ErrorCode.GlobHadNoMatches)
                        {
                            return LogInfo.LogErrorMessage(logs, $"Unable to find a match to [{splitWim}]");
                        }
                    }

                    // Extract file(s)
                    s.MainViewModel.SetBuildCommandProgress("WimExtract Progress");
                    try
                    {
                        // Ignore GLOB_HAD_NO_MATCHES
                        wim.ExtractPath(imageIndex, destDir, extractPath, extractFlags);

                        logs.Add(new LogInfo(LogState.Success, $"Extracted [{extractPath}] to [{destDir}] from [{srcWim}:{imageIndex}]"));
                    }
                    finally
                    { // Finalize Command Progress Report
                        s.MainViewModel.ResetBuildCommandProgress();
                    }
                }
            }
            catch (WimLibException e)
            {
                logs.Add(LogWimLibException(e));
                return logs;
            }

            return logs;
        }

        public static List<LogInfo> WimExtractOp(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeOptInfo infoOp = (CodeOptInfo)cmd.Info;
            CodeInfo_WimExtract[] optInfos = infoOp.Infos<CodeInfo_WimExtract>().ToArray(); ;

            CodeInfo_WimExtract firstInfo = optInfos[0];
            string srcWim = StringEscaper.Preprocess(s, firstInfo.SrcWim);
            string imageIndexStr = StringEscaper.Preprocess(s, firstInfo.ImageIndex);
            string destDir = StringEscaper.Preprocess(s, firstInfo.DestDir);

            // Check SrcWim
            if (!File.Exists(srcWim))
                return LogInfo.LogErrorMessage(logs, $"File [{srcWim}] does not exist");

            // Check DestDir
            if (!StringEscaper.PathSecurityCheck(destDir, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            if (!Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            // Set Flags
            OpenFlags openFlags = OpenFlags.None;
            ExtractFlags extractFlags = ExtractFlags.NoRpFix | ExtractFlags.NoPreserveDirStructure;
            if (firstInfo.CheckFlag)
                openFlags |= OpenFlags.CheckIntegrity;
            if (firstInfo.NoAclFlag)
                extractFlags |= ExtractFlags.NoAcls;
            if (firstInfo.NoAttribFlag)
                extractFlags |= ExtractFlags.NoAttributes;

            List<string> extractPaths = new List<string>(infoOp.Cmds.Count);
            foreach (CodeInfo_WimExtract info in optInfos)
            {
                string extractPath = StringEscaper.Preprocess(s, info.ExtractPath);
                extractPaths.Add(extractPath);

                // Flags for globbing
                if (StringHelper.IsWildcard(extractPath))
                    extractFlags |= ExtractFlags.GlobPaths;
            }

            try
            {
                using (Wim wim = Wim.OpenWim(srcWim, openFlags, WimApplyExtractProgress, s))
                {
                    WimInfo wimInfo = wim.GetWimInfo();

                    // Check imageIndex
                    if (!NumberHelper.ParseInt32(imageIndexStr, out int imageIndex))
                        return LogInfo.LogErrorMessage(logs, $"[{imageIndexStr}] is not a valid positive integer");
                    if (!(1 <= imageIndex && imageIndex <= wimInfo.ImageCount))
                        return LogInfo.LogErrorMessage(logs, $"[{imageIndexStr}] must be [1] ~ [{wimInfo.ImageCount}]");

                    // Process split wim
                    if (firstInfo.Split != null)
                    {
                        string splitWim = StringEscaper.Preprocess(s, firstInfo.Split);

                        try
                        {
                            const RefFlags refFlags = RefFlags.GlobEnable | RefFlags.GlobErrOnNoMatch;
                            wim.ReferenceResourceFile(splitWim, refFlags, openFlags);
                        }
                        catch (WimLibException e) when (e.ErrorCode == ErrorCode.GlobHadNoMatches)
                        {
                            return LogInfo.LogErrorMessage(logs, $"Unable to find a match to [{splitWim}]");
                        }
                    }

                    // Extract files
                    s.MainViewModel.SetBuildCommandProgress("WimExtract Progress");
                    try
                    {
                        Debug.Assert(extractPaths.Count == infoOp.Cmds.Count);

                        // Ignore GLOB_HAD_NO_MATCHES
                        wim.ExtractPaths(imageIndex, destDir, extractPaths, extractFlags);

                        logs.AddRange(infoOp.Cmds.Select((subCmd, i) => new LogInfo(LogState.Success, $"Extracted [{extractPaths[i]}]", subCmd)));
                        logs.Add(new LogInfo(LogState.Success, $"Extracted [{extractPaths.Count}] files from [{srcWim}:{imageIndex}]"));
                    }
                    finally
                    { // Finalize Command Progress Report
                        s.MainViewModel.ResetBuildCommandProgress();
                    }
                }
            }
            catch (WimLibException e)
            {
                logs.Add(LogWimLibException(e));
                return logs;
            }

            return logs;
        }

        public static List<LogInfo> WimExtractBulk(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>(1);

            CodeInfo_WimExtractBulk info = (CodeInfo_WimExtractBulk)cmd.Info;

            string srcWim = StringEscaper.Preprocess(s, info.SrcWim);
            string imageIndexStr = StringEscaper.Preprocess(s, info.ImageIndex);
            string destDir = StringEscaper.Preprocess(s, info.DestDir);
            string listFilePath = StringEscaper.Preprocess(s, info.ListFile);

            // Check SrcWim
            if (!File.Exists(srcWim))
                return LogInfo.LogErrorMessage(logs, $"File [{srcWim}] does not exist");

            // Check DestDir
            if (StringEscaper.PathSecurityCheck(destDir, out string errorMsg) == false)
                return LogInfo.LogErrorMessage(logs, errorMsg);

            if (!Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            // Set Flags
            OpenFlags openFlags = OpenFlags.None;
            ExtractFlags extractFlags = ExtractFlags.NoRpFix;
            if (info.CheckFlag)
                openFlags |= OpenFlags.CheckIntegrity;
            if (info.NoAclFlag)
                extractFlags |= ExtractFlags.NoAcls;
            if (info.NoAttribFlag)
                extractFlags |= ExtractFlags.NoAttributes;
            ExtractFlags extractGlobFlags = extractFlags | ExtractFlags.GlobPaths;

            // Check ListFile
            if (!File.Exists(listFilePath))
                return LogInfo.LogErrorMessage(logs, $"ListFile [{listFilePath}] does not exist");

            string unicodeListFile = FileHelper.GetTempFile("txt");
            try
            {
                List<string> extractNormalPaths = new List<string>();
                List<string> extractGlobPaths = new List<string>();

                // Read listfile
                Encoding encoding = EncodingHelper.DetectEncoding(listFilePath);
                using (StreamReader r = new StreamReader(listFilePath, encoding, false))
                {
                    var extractPaths = r.ReadToEnd().Split('\n').Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim());
                    foreach (string path in extractPaths)
                    {
                        Debug.Assert(0 < path.Length, "Internal Logic Error at CommandWim.WimExtractBulk"); // It should be, because of string.IsNullOrWhiteSpace()
                        if (path[0] == ';' || path[0] == '#')
                            continue;

                        if (StringHelper.IsWildcard(path))
                            extractGlobPaths.Add(path);
                        else
                            extractNormalPaths.Add(path);
                    }
                }

                using (Wim wim = Wim.OpenWim(srcWim, openFlags, WimApplyExtractProgress, s))
                {
                    WimInfo wimInfo = wim.GetWimInfo();

                    // Check imageIndex
                    if (!NumberHelper.ParseInt32(imageIndexStr, out int imageIndex))
                    {
                        logs.Add(new LogInfo(LogState.Error, $"[{imageIndexStr}] is not a valid positive integer"));
                        return logs;
                    }
                    if (!(1 <= imageIndex && imageIndex <= wimInfo.ImageCount))
                    {
                        logs.Add(new LogInfo(LogState.Error, $"[{imageIndexStr}] must be [1] ~ [{wimInfo.ImageCount}]"));
                        return logs;
                    }

                    // Process split wim
                    if (info.Split != null)
                    {
                        string splitWim = StringEscaper.Preprocess(s, info.Split);

                        try
                        {
                            const RefFlags refFlags = RefFlags.GlobEnable | RefFlags.GlobErrOnNoMatch;
                            wim.ReferenceResourceFile(splitWim, refFlags, openFlags);
                        }
                        catch (WimLibException e) when (e.ErrorCode == ErrorCode.GlobHadNoMatches)
                        {
                            return LogInfo.LogErrorMessage(logs, $"Unable to find a match to [{splitWim}]");
                        }
                    }

                    // Log ListFile
                    string globPaths = string.Empty;
                    string normalPaths = string.Empty;
                    if (0 < extractGlobPaths.Count)
                        globPaths = string.Join(Environment.NewLine, extractGlobPaths);
                    if (0 < extractNormalPaths.Count)
                        normalPaths = string.Join(Environment.NewLine, extractNormalPaths);

                    string listFileContent;
                    if (0 < globPaths.Length)
                    {
                        if (0 < normalPaths.Length) // GlobPaths - O, NormalPath - O
                            listFileContent = globPaths + Environment.NewLine + normalPaths;
                        else // GlobPaths - O, NormalPath - X
                            listFileContent = globPaths;
                    }
                    else
                    {
                        if (0 < normalPaths.Length) // GlobPaths - X, NormalPath - O
                            listFileContent = normalPaths;
                        else // GlobPaths - X, NormalPath - X
                            listFileContent = string.Empty;
                    }

                    if (0 == listFileContent.Length)
                        logs.Add(new LogInfo(info.NoWarnFlag ? LogState.Ignore : LogState.Warning, $"Listfile [{listFilePath}] is empty"));
                    else
                        logs.Add(new LogInfo(LogState.Info, $"Extract files based on listfile [{listFilePath}] :\r\n{listFileContent}"));

                    // Extract file(s)
                    s.MainViewModel.SetBuildCommandProgress("WimExtractBulk Progress");
                    try
                    {
                        if (info.NoErrFlag)
                        {
                            Wim.ResetErrorFile();
                            wim.ExtractPaths(imageIndex, destDir, extractNormalPaths, extractGlobFlags);
                            logs.AddRange(Wim.GetErrors().Select(x => new LogInfo(info.NoWarnFlag ? LogState.Ignore : LogState.Warning, x)));

                            wim.ExtractPaths(imageIndex, destDir, extractGlobPaths, extractGlobFlags);
                        }
                        else
                        {
                            wim.ExtractPaths(imageIndex, destDir, extractNormalPaths, extractFlags);
                            wim.ExtractPaths(imageIndex, destDir, extractGlobPaths, extractGlobFlags);
                        }

                        logs.Add(new LogInfo(LogState.Success, $"Extracted files to [{destDir}] from [{srcWim}:{imageIndex}]"));
                    }
                    finally
                    { // Finalize Command Progress Report
                        s.MainViewModel.ResetBuildCommandProgress();
                    }
                }
            }
            catch (WimLibException e)
            {
                logs.Add(LogWimLibException(e));
                return logs;
            }
            finally
            {
                File.Delete(unicodeListFile);
            }

            return logs;
        }
        #endregion

        #region WimLib - WimCapture, WimAppend
        public static List<LogInfo> WimCapture(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>(1);

            CodeInfo_WimCapture info = (CodeInfo_WimCapture)cmd.Info;

            string srcDir = StringEscaper.Preprocess(s, info.SrcDir);
            string destWim = StringEscaper.Preprocess(s, info.DestWim);
            string compStr = StringEscaper.Preprocess(s, info.Compress);

            // Check SrcDir
            if (!Directory.Exists(srcDir))
                return LogInfo.LogErrorMessage(logs, $"Directory [{srcDir}] does not exist");

            // Check DestWim
            if (!StringEscaper.PathSecurityCheck(destWim, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            // Set Flags
            WriteFlags writeFlags = WriteFlags.None;
            AddFlags addFlags = AddFlags.WinConfig | AddFlags.FilePathsUnneeded;
            if (info.BootFlag)
                addFlags |= AddFlags.Boot;
            if (info.NoAclFlag)
                addFlags |= AddFlags.NoAcls;
            if (info.CheckFlag)
                writeFlags |= WriteFlags.CheckIntegrity;

            // Set Compression Type
            CompressionType compType;
            if (compStr.Equals("NONE", StringComparison.OrdinalIgnoreCase))
                compType = CompressionType.None;
            else if (compStr.Equals("XPRESS", StringComparison.OrdinalIgnoreCase))
                compType = CompressionType.XPRESS;
            else if (compStr.Equals("LZX", StringComparison.OrdinalIgnoreCase))
                compType = CompressionType.LZX;
            else if (compStr.Equals("LZMS", StringComparison.OrdinalIgnoreCase))
            {
                writeFlags |= WriteFlags.Solid;
                compType = CompressionType.LZMS;
            }
            else
                return LogInfo.LogErrorMessage(logs, $"Invalid Compression Type [{compStr}]");

            // Set ImageName
            string imageName;
            if (info.ImageName != null)
                imageName = StringEscaper.Preprocess(s, info.ImageName);
            else
            {
                imageName = Path.GetFileName(Path.GetFullPath(srcDir));
                if (string.IsNullOrWhiteSpace(imageName))
                    return LogInfo.LogErrorMessage(logs, "Unable to automatically set the image name");
            }

            // Capture from disk
            try
            {
                using (Wim wim = Wim.CreateNewWim(compType))
                {
                    wim.RegisterCallback(WimWriteProgress, s);

                    wim.AddImage(srcDir, imageName, null, addFlags);
                    if (info.ImageDesc != null)
                    {
                        string imageDesc = StringEscaper.Preprocess(s, info.ImageDesc);
                        wim.SetImageDescription(1, imageDesc);
                    }

                    if (info.WimFlags != null)
                    {
                        string wimFlags = StringEscaper.Preprocess(s, info.WimFlags);
                        wim.SetImageFlags(1, wimFlags);
                    }

                    s.MainViewModel.SetBuildCommandProgress("WimCapture Progress");
                    try
                    {
                        wim.Write(destWim, Wim.AllImages, writeFlags, (uint)Environment.ProcessorCount);

                        logs.Add(new LogInfo(LogState.Success, $"Captured [{srcDir}] into [{destWim}]"));
                    }
                    finally
                    { // Finalize Command Progress Report
                        s.MainViewModel.ResetBuildCommandProgress();
                    }
                }
            }
            catch (WimLibException e)
            {
                logs.Add(LogWimLibException(e));
                return logs;
            }

            return logs;
        }

        public static List<LogInfo> WimAppend(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>(1);

            CodeInfo_WimAppend info = (CodeInfo_WimAppend)cmd.Info;

            string srcDir = StringEscaper.Preprocess(s, info.SrcDir);
            string destWim = StringEscaper.Preprocess(s, info.DestWim);

            // Check SrcDir
            if (!Directory.Exists(srcDir))
                return LogInfo.LogErrorMessage(logs, $"Directory [{srcDir}] does not exist");

            // Check DestWim
            if (!StringEscaper.PathSecurityCheck(destWim, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            // Set Flags
            OpenFlags openFlags = OpenFlags.WriteAccess;
            WriteFlags writeFlags = WriteFlags.None;
            AddFlags addFlags = AddFlags.WinConfig | AddFlags.FilePathsUnneeded;
            if (info.BootFlag)
                addFlags |= AddFlags.Boot;
            if (info.NoAclFlag)
                addFlags |= AddFlags.NoAcls;
            if (info.CheckFlag)
                writeFlags |= WriteFlags.CheckIntegrity;

            // Set ImageName
            string imageName;
            if (info.ImageName != null)
                imageName = StringEscaper.Preprocess(s, info.ImageName);
            else
            {
                imageName = Path.GetFileName(Path.GetFullPath(srcDir));
                if (string.IsNullOrWhiteSpace(imageName))
                    return LogInfo.LogErrorMessage(logs, "Unable to automatically set the image name");
            }

            try
            {
                using (Wim wim = Wim.OpenWim(destWim, openFlags))
                {
                    wim.RegisterCallback(WimWriteProgress, s);

                    // Check if image name is duplicated
                    if (wim.IsImageNameInUse(imageName))
                        return LogInfo.LogErrorMessage(logs, $"Image name [{imageName}] is already in use");

                    // Add Image
                    wim.AddImage(srcDir, imageName, null, addFlags);
                    if (info.ImageDesc != null)
                    {
                        string imageDesc = StringEscaper.Preprocess(s, info.ImageDesc);
                        wim.SetImageDescription(1, imageDesc);
                    }
                    if (info.WimFlags != null)
                    {
                        string wimFlags = StringEscaper.Preprocess(s, info.WimFlags);
                        wim.SetImageFlags(1, wimFlags);
                    }

                    // Set Delta Wim Append (Optional)
                    if (info.DeltaIndex != null)
                    {
                        // Get ImageCount
                        WimInfo wInfo = wim.GetWimInfo();
                        uint imageCount = wInfo.ImageCount;

                        string deltaIndexStr = StringEscaper.Preprocess(s, info.DeltaIndex);
                        if (!NumberHelper.ParseInt32(deltaIndexStr, out int deltaIndex))
                            return LogInfo.LogErrorMessage(logs, $"[{deltaIndexStr}] is not a valid positive integer");
                        if (!(1 <= deltaIndex && deltaIndex <= imageCount))
                            return LogInfo.LogErrorMessage(logs, $"[{deltaIndex}] must be [1] ~ [{imageCount}]");

                        wim.ReferenceTemplateImage((int)imageCount, deltaIndex);
                    }

                    // Appned to Wim
                    s.MainViewModel.SetBuildCommandProgress("WimAppend Progress");
                    try
                    {
                        wim.Overwrite(writeFlags, (uint)Environment.ProcessorCount);

                        logs.Add(new LogInfo(LogState.Success, $"Appended [{srcDir}] into [{destWim}]"));
                    }
                    finally
                    { // Finalize Command Progress Report
                        s.MainViewModel.ResetBuildCommandProgress();
                    }
                }
            }
            catch (WimLibException e)
            {
                logs.Add(LogWimLibException(e));
                return logs;
            }

            return logs;
        }

        private static CallbackStatus WimWriteProgress(ProgressMsg msg, object info, object progctx)
        {
            if (progctx is not EngineState s)
                return CallbackStatus.Continue;

            // SCAN_BEGIN
            // SCAN_DENTRY (Stage 1)
            // SCAN_END
            // WRITE_STREAMS (Stage 2)

            switch (msg)
            {
                case ProgressMsg.ScanBegin:
                    {
                        ScanProgress m = (ScanProgress)info;

                        s.MainViewModel.BuildCommandProgressText = $"[Stage 1] Scanning {m.Source}...";
                    }
                    break;
                case ProgressMsg.WriteStreams:
                    {
                        WriteStreamsProgress m = (WriteStreamsProgress)info;

                        if (0 < m.TotalBytes)
                        {
                            ulong percentComplete = m.CompletedBytes * 100 / m.TotalBytes;
                            s.MainViewModel.BuildCommandProgressValue = percentComplete;
                            s.MainViewModel.BuildCommandProgressText = $"[Stage 2] Writing... ({percentComplete}%)";
                        }
                    }
                    break;
                case ProgressMsg.CalcIntegrity:
                    {
                        IntegrityProgress m = (IntegrityProgress)info;

                        if (0 < m.TotalBytes)
                        {
                            ulong percentComplete = m.CompletedBytes * 100 / m.TotalBytes;
                            s.MainViewModel.BuildCommandProgressText = $"Calculating integrity... ({percentComplete}%)";
                        }
                    }
                    break;
            }
            return CallbackStatus.Continue;
        }

        private static LogInfo LogWimLibException(WimLibException e)
        {
            return new LogInfo(LogState.Error, e.Message);
        }
        #endregion

        #region WimLib - WimDelete
        public static List<LogInfo> WimDelete(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>(1);

            CodeInfo_WimDelete info = (CodeInfo_WimDelete)cmd.Info;

            string srcWim = StringEscaper.Preprocess(s, info.SrcWim);
            string imageIndexStr = StringEscaper.Preprocess(s, info.ImageIndex);

            // Check SrcWim
            if (!File.Exists(srcWim))
                return LogInfo.LogErrorMessage(logs, $"File [{srcWim}] does not exist");

            // Set Flags
            OpenFlags openFlags = OpenFlags.WriteAccess;
            WriteFlags writeFlags = WriteFlags.None;
            if (info.CheckFlag)
                writeFlags |= WriteFlags.CheckIntegrity;

            try
            {
                using (Wim wim = Wim.OpenWim(srcWim, openFlags))
                {
                    wim.RegisterCallback(WimDeleteProgress, s);

                    WimInfo wi = wim.GetWimInfo();

                    // Check imageIndex
                    if (!NumberHelper.ParseInt32(imageIndexStr, out int imageIndex))
                        return LogInfo.LogErrorMessage(logs, $"[{imageIndexStr}] is not a valid positive integer");
                    if (!(1 <= imageIndex && imageIndex <= wi.ImageCount))
                        return LogInfo.LogErrorMessage(logs, $"[{imageIndexStr}] must be [1] ~ [{wi.ImageCount}]");

                    wim.DeleteImage(imageIndex);

                    s.MainViewModel.SetBuildCommandProgress("WimDelete Progress");
                    try
                    {
                        wim.Overwrite(writeFlags, (uint)Environment.ProcessorCount);

                        logs.Add(new LogInfo(LogState.Success, $"Deleted index [{imageIndex}] from [{srcWim}]"));
                    }
                    finally
                    { // Finalize Command Progress Report
                        s.MainViewModel.ResetBuildCommandProgress();
                    }
                }
            }
            catch (WimLibException e)
            {
                logs.Add(LogWimLibException(e));
                return logs;
            }

            return logs;
        }

        private static CallbackStatus WimDeleteProgress(ProgressMsg msg, object info, object progctx)
        {
            if (progctx is not EngineState s)
                return CallbackStatus.Continue;

            // WRITE_STREAMS 
            switch (msg)
            {
                case ProgressMsg.WriteStreams:
                    {
                        WriteStreamsProgress m = (WriteStreamsProgress)info;

                        if (0 < m.TotalBytes)
                        {
                            ulong percentComplete = m.CompletedBytes * 100 / m.TotalBytes;
                            s.MainViewModel.BuildCommandProgressValue = percentComplete;
                            s.MainViewModel.BuildCommandProgressText = $"Writing... ({percentComplete}%)";
                        }
                    }
                    break;
                case ProgressMsg.CalcIntegrity:
                    {
                        IntegrityProgress m = (IntegrityProgress)info;

                        if (0 < m.TotalBytes)
                        {
                            ulong percentComplete = m.CompletedBytes * 100 / m.TotalBytes;
                            s.MainViewModel.BuildCommandProgressText = $"Calculating integrity... ({percentComplete}%)";
                        }
                    }
                    break;
            }
            return CallbackStatus.Continue;
        }
        #endregion

        #region WimLib - WimPathAdd, WimPathDelete, WimPathRemove, WimPathOp
        public static List<LogInfo> WimPathAdd(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>(1);

            CodeInfo_WimPathAdd info = (CodeInfo_WimPathAdd)cmd.Info;

            string wimFile = StringEscaper.Preprocess(s, info.WimFile);
            string imageIndexStr = StringEscaper.Preprocess(s, info.ImageIndex);
            string srcPath = StringEscaper.Preprocess(s, info.SrcPath);
            string destPath = StringEscaper.Preprocess(s, info.DestPath);

            // Check wimFile
            if (!File.Exists(wimFile))
                return LogInfo.LogErrorMessage(logs, $"File [{wimFile}] does not exist");
            if (!StringEscaper.PathSecurityCheck(wimFile, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            // Set Flags
            const OpenFlags openFlags = OpenFlags.WriteAccess;
            const UpdateFlags updateFlags = UpdateFlags.SendProgress;
            WriteFlags writeFlags = WriteFlags.None;
            AddFlags addFlags = AddFlags.WinConfig | AddFlags.Verbose | AddFlags.ExcludeVerbose;
            if (info.CheckFlag)
                writeFlags |= WriteFlags.CheckIntegrity;
            if (info.RebuildFlag)
                writeFlags |= WriteFlags.Rebuild;
            if (info.NoAclFlag)
                addFlags |= AddFlags.NoAcls;
            if (info.PreserveFlag)
                addFlags |= AddFlags.NoReplace;

            try
            {
                using (Wim wim = Wim.OpenWim(wimFile, openFlags))
                {
                    wim.RegisterCallback(WimPathProgress, s);

                    WimInfo wi = wim.GetWimInfo();
                    if (!NumberHelper.ParseInt32(imageIndexStr, out int imageIndex))
                        return LogInfo.LogErrorMessage(logs, $"[{imageIndexStr}] is not a valid positive integer");
                    if (!(1 <= imageIndex && imageIndex <= wi.ImageCount))
                        return LogInfo.LogErrorMessage(logs, $"[{imageIndexStr}] must be [1] ~ [{wi.ImageCount}]");

                    UpdateCommand addCmd = UpdateCommand.SetAdd(srcPath, destPath, null, addFlags);
                    wim.UpdateImage(imageIndex, addCmd, updateFlags);

                    s.MainViewModel.SetBuildCommandProgress("WimPathAdd Progress");
                    try
                    {
                        wim.Overwrite(writeFlags, (uint)Environment.ProcessorCount);

                        logs.Add(new LogInfo(LogState.Success, $"Added [{srcPath}] into [{wimFile}]"));
                    }
                    finally
                    { // Finalize Command Progress Report
                        s.MainViewModel.ResetBuildCommandProgress();
                    }
                }
            }
            catch (WimLibException e)
            {
                logs.Add(LogWimLibException(e));
                return logs;
            }

            return logs;
        }

        public static List<LogInfo> WimPathDelete(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>(1);

            CodeInfo_WimPathDelete info = (CodeInfo_WimPathDelete)cmd.Info;

            string wimFile = StringEscaper.Preprocess(s, info.WimFile);
            string imageIndexStr = StringEscaper.Preprocess(s, info.ImageIndex);
            string path = StringEscaper.Preprocess(s, info.Path);

            // Check wimFile
            if (!File.Exists(wimFile))
                return LogInfo.LogErrorMessage(logs, $"File [{wimFile}] does not exist");
            if (StringEscaper.PathSecurityCheck(wimFile, out string errorMsg) == false)
                return LogInfo.LogErrorMessage(logs, errorMsg);

            // Set Flags
            const OpenFlags openFlags = OpenFlags.WriteAccess;
            const UpdateFlags updateFlags = UpdateFlags.SendProgress;
            WriteFlags writeFlags = WriteFlags.None;
            const DeleteFlags deleteFlags = DeleteFlags.Recursive;
            if (info.CheckFlag)
                writeFlags |= WriteFlags.CheckIntegrity;
            if (info.RebuildFlag)
                writeFlags |= WriteFlags.Rebuild;

            try
            {
                using (Wim wim = Wim.OpenWim(wimFile, openFlags, WimPathProgress, s))
                {
                    WimInfo wi = wim.GetWimInfo();
                    if (!NumberHelper.ParseInt32(imageIndexStr, out int imageIndex))
                        return LogInfo.LogErrorMessage(logs, $"[{imageIndexStr}] is not a valid positive integer");
                    if (!(1 <= imageIndex && imageIndex <= wi.ImageCount))
                        return LogInfo.LogErrorMessage(logs, $"[{imageIndexStr}] must be [1] ~ [{wi.ImageCount}]");

                    UpdateCommand deleteCmd = UpdateCommand.SetDelete(path, deleteFlags);
                    wim.UpdateImage(imageIndex, deleteCmd, updateFlags);

                    s.MainViewModel.SetBuildCommandProgress("WimPathDelete Progress");
                    try
                    {
                        wim.Overwrite(writeFlags, (uint)Environment.ProcessorCount);

                        logs.Add(new LogInfo(LogState.Success, $"[{path}] deleted from [{wimFile}]"));
                    }
                    finally
                    { // Finalize Command Progress Report
                        s.MainViewModel.ResetBuildCommandProgress();
                    }
                }
            }
            catch (WimLibException e)
            {
                logs.Add(LogWimLibException(e));
                return logs;
            }

            return logs;
        }

        public static List<LogInfo> WimPathRename(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>(1);

            CodeInfo_WimPathRename info = (CodeInfo_WimPathRename)cmd.Info;

            string wimFile = StringEscaper.Preprocess(s, info.WimFile);
            string imageIndexStr = StringEscaper.Preprocess(s, info.ImageIndex);
            string srcPath = StringEscaper.Preprocess(s, info.SrcPath);
            string destPath = StringEscaper.Preprocess(s, info.DestPath);

            // Check wimFile
            if (!File.Exists(wimFile))
                return LogInfo.LogErrorMessage(logs, $"File [{wimFile}] does not exist");
            if (StringEscaper.PathSecurityCheck(wimFile, out string errorMsg) == false)
                return LogInfo.LogErrorMessage(logs, errorMsg);

            // Set Flags
            const OpenFlags openFlags = OpenFlags.WriteAccess;
            const UpdateFlags updateFlags = UpdateFlags.SendProgress;
            WriteFlags writeFlags = WriteFlags.None;
            if (info.CheckFlag)
                writeFlags |= WriteFlags.CheckIntegrity;
            if (info.RebuildFlag)
                writeFlags |= WriteFlags.Rebuild;

            try
            {
                using (Wim wim = Wim.OpenWim(wimFile, openFlags, WimPathProgress, s))
                {
                    WimInfo wi = wim.GetWimInfo();
                    if (!NumberHelper.ParseInt32(imageIndexStr, out int imageIndex))
                        return LogInfo.LogErrorMessage(logs, $"[{imageIndexStr}] is not a valid positive integer");
                    if (!(1 <= imageIndex && imageIndex <= wi.ImageCount))
                        return LogInfo.LogErrorMessage(logs, $"[{imageIndexStr}] must be [1] ~ [{wi.ImageCount}]");

                    UpdateCommand renCmd = UpdateCommand.SetRename(srcPath, destPath);
                    wim.UpdateImage(imageIndex, renCmd, updateFlags);

                    s.MainViewModel.SetBuildCommandProgress("WimPathRename Progress");
                    try
                    {
                        wim.Overwrite(writeFlags, (uint)Environment.ProcessorCount);

                        logs.Add(new LogInfo(LogState.Success, $"Renamed [{srcPath}] to [{destPath}] in [{wimFile}]"));
                    }
                    finally
                    { // Finalize Command Progress Report
                        s.MainViewModel.ResetBuildCommandProgress();
                    }
                }
            }
            catch (WimLibException e)
            {
                logs.Add(LogWimLibException(e));
                return logs;
            }

            return logs;
        }

        public static List<LogInfo> WimPathOp(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeOptInfo infoOp = (CodeOptInfo)cmd.Info;

            string wimFile;
            string imageIndexStr;
            bool checkFlag;
            bool rebuildFlag;

            CodeCommand firstCmd = infoOp.Cmds[0];
            switch (firstCmd.Type)
            {
                case CodeType.WimPathAdd:
                    {
                        CodeInfo_WimPathAdd firstInfo = (CodeInfo_WimPathAdd)firstCmd.Info;
                        wimFile = StringEscaper.Preprocess(s, firstInfo.WimFile);
                        imageIndexStr = StringEscaper.Preprocess(s, firstInfo.ImageIndex);
                        checkFlag = firstInfo.CheckFlag;
                        rebuildFlag = firstInfo.RebuildFlag;
                        break;
                    }
                case CodeType.WimPathDelete:
                    {
                        CodeInfo_WimPathDelete firstInfo = (CodeInfo_WimPathDelete)firstCmd.Info;
                        wimFile = StringEscaper.Preprocess(s, firstInfo.WimFile);
                        imageIndexStr = StringEscaper.Preprocess(s, firstInfo.ImageIndex);
                        checkFlag = firstInfo.CheckFlag;
                        rebuildFlag = firstInfo.RebuildFlag;
                        break;
                    }
                case CodeType.WimPathRename:
                    {
                        CodeInfo_WimPathRename firstInfo = (CodeInfo_WimPathRename)firstCmd.Info;
                        wimFile = StringEscaper.Preprocess(s, firstInfo.WimFile);
                        imageIndexStr = StringEscaper.Preprocess(s, firstInfo.ImageIndex);
                        checkFlag = firstInfo.CheckFlag;
                        rebuildFlag = firstInfo.RebuildFlag;
                        break;
                    }
                default:
                    throw new InternalException("Internal Logic Error at CommandWim.WimPathOp");
            }

            // Check WimFile
            if (!File.Exists(wimFile))
                return LogInfo.LogErrorMessage(logs, $"File [{wimFile}] does not exist");
            if (!StringEscaper.PathSecurityCheck(wimFile, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            // Set Common Flags
            const OpenFlags openFlags = OpenFlags.WriteAccess;
            const UpdateFlags updateFlags = UpdateFlags.SendProgress;
            WriteFlags writeFlags = WriteFlags.None;
            if (checkFlag)
                writeFlags |= WriteFlags.CheckIntegrity;
            if (rebuildFlag)
                writeFlags |= WriteFlags.Rebuild;

            // Make list of UpdateCommand
            List<LogInfo> wimLogs = new List<LogInfo>(infoOp.Cmds.Count);
            List<UpdateCommand> wimUpdateCmds = new List<UpdateCommand>(infoOp.Cmds.Count);
            foreach (CodeCommand subCmd in infoOp.Cmds)
            {
                switch (subCmd.Type)
                {
                    case CodeType.WimPathAdd:
                        {
                            CodeInfo_WimPathAdd info = (CodeInfo_WimPathAdd)subCmd.Info;

                            string srcPath = StringEscaper.Preprocess(s, info.SrcPath);
                            string destPath = StringEscaper.Preprocess(s, info.DestPath);

                            AddFlags addFlags = AddFlags.WinConfig | AddFlags.Verbose | AddFlags.ExcludeVerbose;
                            if (info.NoAclFlag)
                                addFlags |= AddFlags.NoAcls;
                            if (info.PreserveFlag)
                                addFlags |= AddFlags.NoReplace;

                            UpdateCommand addCmd = UpdateCommand.SetAdd(srcPath, destPath, null, addFlags);
                            wimUpdateCmds.Add(addCmd);

                            wimLogs.Add(new LogInfo(LogState.Success, $"Added [{srcPath}] into [{wimFile}]", subCmd));
                            break;
                        }
                    case CodeType.WimPathDelete:
                        {
                            CodeInfo_WimPathDelete info = (CodeInfo_WimPathDelete)subCmd.Info;

                            string path = StringEscaper.Preprocess(s, info.Path);

                            const DeleteFlags deleteFlags = DeleteFlags.Recursive;

                            UpdateCommand deleteCmd = UpdateCommand.SetDelete(path, deleteFlags);
                            wimUpdateCmds.Add(deleteCmd);

                            wimLogs.Add(new LogInfo(LogState.Success, $"[{path}] deleted from [{wimFile}]", subCmd));
                            break;
                        }
                    case CodeType.WimPathRename:
                        {
                            CodeInfo_WimPathRename info = (CodeInfo_WimPathRename)subCmd.Info;

                            string srcPath = StringEscaper.Preprocess(s, info.SrcPath);
                            string destPath = StringEscaper.Preprocess(s, info.DestPath);

                            UpdateCommand renCmd = UpdateCommand.SetRename(srcPath, destPath);
                            wimUpdateCmds.Add(renCmd);

                            wimLogs.Add(new LogInfo(LogState.Success, $"Renamed [{srcPath}] to [{destPath}] in [{wimFile}]", subCmd));
                            break;
                        }
                    default:
                        throw new InternalException("Internal Logic Error at CommandWim.WimPathOp");
                }
            }

            try
            {
                using (Wim wim = Wim.OpenWim(wimFile, openFlags, WimPathProgress, s))
                {
                    WimInfo wi = wim.GetWimInfo();
                    if (!NumberHelper.ParseInt32(imageIndexStr, out int imageIndex))
                        return LogInfo.LogErrorMessage(logs, $"[{imageIndexStr}] is not a valid positive integer");
                    if (!(1 <= imageIndex && imageIndex <= wi.ImageCount))
                        return LogInfo.LogErrorMessage(logs, $"[{imageIndexStr}] must be [1] ~ [{wi.ImageCount}]");

                    wim.UpdateImage(imageIndex, wimUpdateCmds, updateFlags);

                    s.MainViewModel.SetBuildCommandProgress("WimPath Progress");
                    try
                    {
                        wim.Overwrite(writeFlags, (uint)Environment.ProcessorCount);

                        logs.AddRange(wimLogs);
                        logs.Add(new LogInfo(LogState.Success, $"Updated [{infoOp.Cmds.Count}] files from [{wimFile}:{imageIndex}]"));
                    }
                    finally
                    { // Finalize Command Progress Report
                        s.MainViewModel.ResetBuildCommandProgress();
                    }
                }
            }
            catch (WimLibException e)
            {
                logs.Add(LogWimLibException(e));
                return logs;
            }

            return logs;
        }

        private static CallbackStatus WimPathProgress(ProgressMsg msg, object info, object progctx)
        {
            if (progctx is not EngineState s)
                return CallbackStatus.Continue;

            // UPDATE_BEGIN_COMMAND
            // SCAN_BEGIN
            // SCAN_END
            // UPDATE_END_COMMAND
            // WRITE_STREAMS

            switch (msg)
            {
                case ProgressMsg.UpdateEndCommand:
                    {
                        UpdateProgress m = (UpdateProgress)info;

                        UpdateCommand upCmd = m.Command;
                        string str;
                        switch (upCmd.Op)
                        {
                            case UpdateOp.Add:
                                var add = upCmd.Add;
                                str = $"[Stage 1] Adding {add.FsSourcePath}... ({m.CompletedCommands}/{m.TotalCommands})";
                                break;
                            case UpdateOp.Delete:
                                var del = upCmd.Delete;
                                str = $"[Stage 1] Deleting {del.WimPath}... ({m.CompletedCommands}/{m.TotalCommands})";
                                break;
                            case UpdateOp.Rename:
                                var ren = upCmd.Rename;
                                str = $"[Stage 1] Renaming {ren.WimSourcePath} to {ren.WimTargetPath}... ({m.CompletedCommands}/{m.TotalCommands})";
                                break;
                            default:
                                throw new InternalException("Internal Logic Error at WimPathProgress");
                        }

                        s.MainViewModel.BuildCommandProgressText = str;
                    }
                    break;
                case ProgressMsg.WriteStreams:
                    {
                        WriteStreamsProgress m = (WriteStreamsProgress)info;

                        if (0 < m.TotalBytes)
                        {
                            ulong percentComplete = m.CompletedBytes * 100 / m.TotalBytes;
                            s.MainViewModel.BuildCommandProgressValue = percentComplete;
                            s.MainViewModel.BuildCommandProgressText = $"[Stage 2] Writing... ({percentComplete}%)";
                        }
                    }
                    break;
                case ProgressMsg.CalcIntegrity:
                    {
                        IntegrityProgress m = (IntegrityProgress)info;

                        if (0 < m.TotalBytes)
                        {
                            ulong percentComplete = m.CompletedBytes * 100 / m.TotalBytes;
                            s.MainViewModel.BuildCommandProgressText = $"Calculating integrity... ({percentComplete}%)";
                        }
                    }
                    break;
            }
            return CallbackStatus.Continue;
        }
        #endregion

        #region WimLib - WimOptimize
        public static List<LogInfo> WimOptimize(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>(1);

            CodeInfo_WimOptimize info = (CodeInfo_WimOptimize)cmd.Info;

            string wimFile = StringEscaper.Preprocess(s, info.WimFile);

            // Check SrcWim
            if (!File.Exists(wimFile))
                return LogInfo.LogErrorMessage(logs, $"File [{wimFile}] does not exist");
            if (!StringEscaper.PathSecurityCheck(wimFile, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            // Set Flags
            OpenFlags openFlags = OpenFlags.WriteAccess;
            WriteFlags writeFlags = WriteFlags.Rebuild;
            CompressionType? compType = null;
            if (info.Recompress != null)
            {
                string recompStr = StringEscaper.Preprocess(s, info.Recompress);

                writeFlags |= WriteFlags.Recompress;

                // Set Compression Type
                // NONE, XPRESS, LZX, LZMS : Recompress file with specified algorithm
                // KEEP : Recompress file with current compresssoin algorithm
                if (recompStr.Equals("NONE", StringComparison.OrdinalIgnoreCase))
                    compType = CompressionType.None;
                else if (recompStr.Equals("XPRESS", StringComparison.OrdinalIgnoreCase))
                    compType = CompressionType.XPRESS;
                else if (recompStr.Equals("LZX", StringComparison.OrdinalIgnoreCase))
                    compType = CompressionType.LZX;
                else if (recompStr.Equals("LZMS", StringComparison.OrdinalIgnoreCase))
                {
                    writeFlags |= WriteFlags.Solid;
                    compType = CompressionType.LZMS;
                }
                else if (!recompStr.Equals("KEEP", StringComparison.OrdinalIgnoreCase))
                    return LogInfo.LogErrorMessage(logs, $"Invalid compression type [{recompStr}].");
            }

            if (info.CheckFlag == true)
                writeFlags |= WriteFlags.CheckIntegrity;
            else if (info.CheckFlag == false)
                writeFlags |= WriteFlags.NoCheckIntegrity;

            try
            {
                using (Wim wim = Wim.OpenWim(wimFile, openFlags))
                {
                    wim.RegisterCallback(WimSimpleWriteProgress, s);

                    if (compType != null)
                        wim.SetOutputCompressionType((CompressionType)compType);

                    s.MainViewModel.SetBuildCommandProgress("WimOptimize Progress");
                    try
                    {
                        long before = new FileInfo(wimFile).Length;
                        wim.Overwrite(writeFlags, (uint)Environment.ProcessorCount);
                        long after = new FileInfo(wimFile).Length;

                        string beforeStr = NumberHelper.ByteSizeToSIUnit(before);
                        string afterStr = NumberHelper.ByteSizeToSIUnit(after);
                        logs.Add(new LogInfo(LogState.Success, $"Optimized [{wimFile}] from [{beforeStr}] to [{afterStr}]"));
                    }
                    finally
                    { // Finalize Command Progress Report
                        s.MainViewModel.ResetBuildCommandProgress();
                    }
                }
            }
            catch (WimLibException e)
            {
                logs.Add(LogWimLibException(e));
                return logs;
            }

            return logs;
        }
        #endregion

        #region WimLib - WimExport
        public static List<LogInfo> WimExport(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>(1);

            CodeInfo_WimExport info = (CodeInfo_WimExport)cmd.Info;

            string srcWimPath = StringEscaper.Preprocess(s, info.SrcWim);
            string imageIndexStr = StringEscaper.Preprocess(s, info.ImageIndex);
            string destWimPath = StringEscaper.Preprocess(s, info.DestWim);
            string? imageName = null;
            if (info.ImageName != null)
                imageName = StringEscaper.Preprocess(s, info.ImageName);
            string? imageDesc = null;
            if (info.ImageDesc != null)
                imageDesc = StringEscaper.Preprocess(s, info.ImageDesc);

            // Check SrcWim
            if (!File.Exists(srcWimPath))
                return LogInfo.LogErrorMessage(logs, $"File [{srcWimPath}] does not exist");

            // Check DestWim
            if (!StringEscaper.PathSecurityCheck(destWimPath, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            // Set Flags
            WriteFlags writeFlags = WriteFlags.Rebuild;
            ExportFlags exportFlags = ExportFlags.Gift;

            if (info.BootFlag)
                exportFlags |= ExportFlags.Boot;
            if (info.CheckFlag == true)
                writeFlags |= WriteFlags.CheckIntegrity;
            else if (info.CheckFlag == false)
                writeFlags |= WriteFlags.NoCheckIntegrity;

            try
            {
                using (Wim srcWim = Wim.OpenWim(srcWimPath, OpenFlags.None))
                {
                    WimInfo wi = srcWim.GetWimInfo();

                    // Check imageIndex
                    if (!NumberHelper.ParseInt32(imageIndexStr, out int imageIndex))
                        return LogInfo.LogErrorMessage(logs, $"[{imageIndexStr}] is not a valid positive integer");
                    if (!(1 <= imageIndex && imageIndex <= wi.ImageCount))
                        return LogInfo.LogErrorMessage(logs, $"[{imageIndexStr}] must be [1] ~ [{wi.ImageCount}]");

                    // Process split wim
                    if (info.Split != null)
                    {
                        string splitWim = StringEscaper.Preprocess(s, info.Split);

                        try
                        {
                            const RefFlags refFlags = RefFlags.GlobEnable | RefFlags.GlobErrOnNoMatch;
                            srcWim.ReferenceResourceFile(splitWim, refFlags, OpenFlags.None);
                        }
                        catch (WimLibException e) when (e.ErrorCode == ErrorCode.GlobHadNoMatches)
                        {
                            return LogInfo.LogErrorMessage(logs, $"Unable to find a match to [{splitWim}]");
                        }
                    }

                    s.MainViewModel.SetBuildCommandProgress("WimExport Progress");
                    try
                    {
                        if (File.Exists(destWimPath))
                        { // Append to existing wim file
                            // Set Compression Type
                            // Use of compress argument [NONE|XPRESS|LZX|LZMS] is prohibited
                            if (info.Recompress != null)
                            {
                                string compStr = StringEscaper.Preprocess(s, info.Recompress);
                                if (!compStr.Equals("KEEP", StringComparison.OrdinalIgnoreCase))
                                    return LogInfo.LogErrorMessage(logs, $"Invalid compression type [{compStr}]. You must use [KEEP] when exporting to an existing wim file");

                                writeFlags |= WriteFlags.Recompress;
                            }

                            uint destWimCount;
                            using (Wim destWim = Wim.OpenWim(destWimPath, OpenFlags.WriteAccess))
                            {
                                destWim.RegisterCallback(WimSimpleWriteProgress, s);

                                // Get destWim's imageCount
                                WimInfo dwi = destWim.GetWimInfo();
                                destWimCount = dwi.ImageCount;

                                srcWim.ExportImage(imageIndex, destWim, imageName, imageDesc, exportFlags);

                                destWim.Overwrite(writeFlags, (uint)Environment.ProcessorCount);
                            }


                            logs.Add(new LogInfo(LogState.Success, $"Exported [{srcWimPath}:{imageIndex}] into wim [{destWimPath}:{destWimCount + 1}]"));
                        }
                        else
                        { // Create new wim file
                            CompressionType compType = wi.CompressionType;
                            if (info.Recompress != null)
                            {
                                string compStr = StringEscaper.Preprocess(s, info.Recompress);

                                // Set Compression Type
                                // Use of compress argument [KEEP] is prohibitted
                                if (compStr.Equals("NONE", StringComparison.OrdinalIgnoreCase))
                                    compType = CompressionType.None;
                                else if (compStr.Equals("XPRESS", StringComparison.OrdinalIgnoreCase))
                                    compType = CompressionType.XPRESS;
                                else if (compStr.Equals("LZX", StringComparison.OrdinalIgnoreCase))
                                    compType = CompressionType.LZX;
                                else if (compStr.Equals("LZMS", StringComparison.OrdinalIgnoreCase))
                                    compType = CompressionType.LZMS;
                                else if (compStr.Equals("KEEP", StringComparison.OrdinalIgnoreCase))
                                    return LogInfo.LogErrorMessage(logs, $"Cannot use [{compStr}] compression with a new wim file");
                                else
                                    return LogInfo.LogErrorMessage(logs, $"Invalid compression type [{compStr}]");

                                if (compType == CompressionType.LZMS)
                                    writeFlags |= WriteFlags.Solid;
                                writeFlags |= WriteFlags.Recompress;
                            }

                            using (Wim destWim = Wim.CreateNewWim(compType))
                            {
                                destWim.RegisterCallback(WimSimpleWriteProgress, s);

                                srcWim.ExportImage(imageIndex, destWim, imageName, imageDesc, exportFlags);

                                destWim.Write(destWimPath, Wim.AllImages, writeFlags, (uint)Environment.ProcessorCount);
                            }

                            logs.Add(new LogInfo(LogState.Success, $"Exported [{srcWimPath}:{imageIndex}] into new wim file [{destWimPath}]"));
                        }
                    }
                    finally
                    { // Finalize Command Progress Report
                        s.MainViewModel.SetBuildCommandProgress("Progress");
                    }
                }
            }
            catch (WimLibException e)
            {
                logs.Add(LogWimLibException(e));
                return logs;
            }

            return logs;
        }
        #endregion

        #region WimLib - WimSimpleWriteProgress
        private static CallbackStatus WimSimpleWriteProgress(ProgressMsg msg, object info, object progctx)
        {
            if (progctx is not EngineState s)
                return CallbackStatus.Continue;

            // WRITE_STREAMS 
            switch (msg)
            {
                case ProgressMsg.WriteStreams:
                    {
                        WriteStreamsProgress m = (WriteStreamsProgress)info;

                        if (0 < m.TotalBytes)
                        {
                            ulong percentComplete = m.CompletedBytes * 100 / m.TotalBytes;
                            s.MainViewModel.BuildCommandProgressValue = percentComplete;
                            s.MainViewModel.BuildCommandProgressText = $"Writing... ({percentComplete}%)";
                        }
                    }
                    break;
                case ProgressMsg.CalcIntegrity:
                    {
                        IntegrityProgress m = (IntegrityProgress)info;

                        if (0 < m.TotalBytes)
                        {
                            ulong percentComplete = m.CompletedBytes * 100 / m.TotalBytes;
                            s.MainViewModel.BuildCommandProgressText = $"Calculating integrity... ({percentComplete}%)";
                        }
                    }
                    break;
            }
            return CallbackStatus.Continue;
        }
        #endregion
    }
}
