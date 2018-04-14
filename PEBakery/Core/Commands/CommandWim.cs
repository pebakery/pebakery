/*
    Copyright (C) 2017-2018 Hajin Jang
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
using PEBakery.Exceptions;
using PEBakery.Helper;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace PEBakery.Core.Commands
{
    public static class CommandWim
    {
        #region Wimgapi - WimMount, WimUnmount
        public static List<LogInfo> WimMount(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>(1);

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_WimMount), "Invalid CodeInfo");
            CodeInfo_WimMount info = cmd.Info as CodeInfo_WimMount;
            Debug.Assert(info != null, "Invalid CodeInfo");

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
            int imageCount = 0;
            try
            {
                using (WimHandle hWim = WimgApi.CreateFile(srcWim,
                    WimFileAccess.Query,
                    WimCreationDisposition.OpenExisting,
                    WimCreateFileOptions.None,
                    WimCompressionType.None))
                {
                    WimgApi.SetTemporaryPath(hWim, Path.GetTempPath());
                    imageCount = WimgApi.GetImageCount(hWim);
                }
            }
            catch (Win32Exception e)
            {
                logs.Add(CommandWim.LogWimgApiException(e, $"Unable to get information from [{srcWim}]"));
                return logs;
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

            try
            {
                using (WimHandle hWim = WimgApi.CreateFile(srcWim,
                    accessFlag,
                    WimCreationDisposition.OpenExisting,
                    WimCreateFileOptions.None,
                    WimCompressionType.None))
                {

                    WimgApi.SetTemporaryPath(hWim, Path.GetTempPath());

                    try
                    {
                        WimgApi.RegisterMessageCallback(hWim, WimgApiCallback);

                        using (WimHandle hImage = WimgApi.LoadImage(hWim, imageIndex))
                        {
                            s.MainViewModel.BuildCommandProgressTitle = "WimMount Progress";
                            s.MainViewModel.BuildCommandProgressText = string.Empty;
                            s.MainViewModel.BuildCommandProgressMax = 100;
                            s.MainViewModel.BuildCommandProgressShow = true;

                            // Mount Wim
                            WimgApi.MountImage(hImage, mountDir, mountFlag);
                        }
                    }
                    catch (Win32Exception e)
                    {
                        logs.Add(CommandWim.LogWimgApiException(e, $"Unable to mount [{srcWim}]"));
                        return logs;
                    }
                    finally
                    {
                        s.MainViewModel.BuildCommandProgressShow = false;
                        s.MainViewModel.BuildCommandProgressTitle = "Progress";
                        s.MainViewModel.BuildCommandProgressText = string.Empty;
                        s.MainViewModel.BuildCommandProgressValue = 0;
                        WimgApi.UnregisterMessageCallback(hWim, WimgApiCallback);
                    }
                }
            }
            catch (Win32Exception e)
            {
                logs.Add(CommandWim.LogWimgApiException(e, $"Unable to open [{srcWim}]"));
                return logs;
            }

            logs.Add(new LogInfo(LogState.Success, $"[{srcWim}]'s image [{imageIndex}] mounted to [{mountDir}]"));
            return logs;
        }

        public static List<LogInfo> WimUnmount(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>(1);

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_WimUnmount), "Invalid CodeInfo");
            CodeInfo_WimUnmount info = cmd.Info as CodeInfo_WimUnmount;
            Debug.Assert(info != null, "Invalid CodeInfo");

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
            WimHandle hWim = null;
            WimHandle hImage = null;
            try
            {
                hWim = WimgApi.GetMountedImageHandle(mountDir, !commit, out hImage);

                WimMountInfo wimInfo = WimgApi.GetMountedImageInfoFromHandle(hImage);
                Debug.Assert(wimInfo.MountPath.Equals(mountDir, StringComparison.OrdinalIgnoreCase));

                // Prepare Command Progress Report
                WimgApi.RegisterMessageCallback(hWim, WimgApiCallback);
                s.MainViewModel.BuildCommandProgressTitle = "WimUnmount Progress";
                s.MainViewModel.BuildCommandProgressText = string.Empty;
                s.MainViewModel.BuildCommandProgressMax = 100;
                s.MainViewModel.BuildCommandProgressShow = true;

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
                            logs.Add(CommandWim.LogWimgApiException(e, $"Unable to commit [{mountDir}] into [{wimInfo.Path}]"));
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
                        logs.Add(CommandWim.LogWimgApiException(e, $"Unable to unmount [{mountDir}]"));
                        return logs;
                    }
                }
                finally
                { // Finalize Command Progress Report
                    s.MainViewModel.BuildCommandProgressShow = false;
                    s.MainViewModel.BuildCommandProgressTitle = "Progress";
                    s.MainViewModel.BuildCommandProgressText = string.Empty;
                    s.MainViewModel.BuildCommandProgressValue = 0;
                    WimgApi.UnregisterMessageCallback(hWim, WimgApiCallback);
                }
            }
            catch (Win32Exception e)
            {
                logs.Add(CommandWim.LogWimgApiException(e, $"Unable to get mounted wim information from [{mountDir}]"));
                return logs;
            }
            finally
            {
                hImage?.Close();
                hWim?.Close();
            }

            return logs;
        }

        private static WimMessageResult WimgApiCallback(WimMessageType msgType, object msg, object userData)
        { // https://github.com/josemesona/ManagedWimgApi/wiki/Message-Callbacks
            Debug.Assert(Engine.WorkingEngine != null);
            EngineState s = Engine.WorkingEngine.s;

            switch (msgType)
            {
                case WimMessageType.Progress:
                    { // For WimMount
                        WimMessageProgress wMsg = (WimMessageProgress)msg;

                        s.MainViewModel.BuildCommandProgressValue = wMsg.PercentComplete;

                        if (0 < wMsg.EstimatedTimeRemaining.TotalSeconds)
                        {
                            int min = (int)wMsg.EstimatedTimeRemaining.TotalMinutes;
                            int sec = wMsg.EstimatedTimeRemaining.Seconds;
                            s.MainViewModel.BuildCommandProgressText = $"{wMsg.PercentComplete}%, Remaing Time : {min}m {sec}s";
                        }
                        else
                        {
                            s.MainViewModel.BuildCommandProgressText = $"{wMsg.PercentComplete}%";
                        }
                    }
                    break;
                case WimMessageType.MountCleanupProgress:
                    { // For WimUnmount
                        WimMessageMountCleanupProgress wMsg = (WimMessageMountCleanupProgress)msg;

                        s.MainViewModel.BuildCommandProgressValue = wMsg.PercentComplete;

                        if (0 < wMsg.EstimatedTimeRemaining.TotalSeconds)
                        {
                            int min = (int)wMsg.EstimatedTimeRemaining.TotalMinutes;
                            int sec = wMsg.EstimatedTimeRemaining.Seconds;
                            s.MainViewModel.BuildCommandProgressText = $"{wMsg.PercentComplete}%, Remaing Time : {min}m {sec}s";
                        }
                        else
                        {
                            s.MainViewModel.BuildCommandProgressText = $"{wMsg.PercentComplete}%";
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

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_WimInfo), "Invalid CodeInfo");
            CodeInfo_WimInfo info = cmd.Info as CodeInfo_WimInfo;
            Debug.Assert(info != null, "Invalid CodeInfo");

            string srcWim = StringEscaper.Preprocess(s, info.SrcWim);
            string imageIndexStr = StringEscaper.Preprocess(s, info.ImageIndex);
            string key = StringEscaper.Preprocess(s, info.Key);

            // Check SrcWim
            if (!File.Exists(srcWim))
                return LogInfo.LogErrorMessage(logs, $"File [{srcWim}] does not exist");

            try
            {
                using (Wim wim = Wim.OpenWim(srcWim, OpenFlags.DEFAULT))
                {
                    ManagedWimLib.WimInfo wi = wim.GetWimInfo();

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
                logs.Add(CommandWim.LogWimLibException(e));
                return logs;
            }

            return logs;
        }
        #endregion

        #region WimLib - WimApply
        public static List<LogInfo> WimApply(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>(1);

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_WimApply), "Invalid CodeInfo");
            CodeInfo_WimApply info = cmd.Info as CodeInfo_WimApply;
            Debug.Assert(info != null, "Invalid CodeInfo");

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
            OpenFlags openFlags = OpenFlags.DEFAULT;
            ExtractFlags extractFlags = ExtractFlags.DEFAULT;
            if (info.CheckFlag)
                openFlags |= OpenFlags.CHECK_INTEGRITY;
            if (info.NoAclFlag)
                extractFlags |= ExtractFlags.NO_ACLS;
            if (info.NoAttribFlag)
                extractFlags |= ExtractFlags.NO_ATTRIBUTES;

            try
            {
                using (Wim wim = Wim.OpenWim(srcWim, openFlags, WimApplyExtractProgress, s))
                {
                    ManagedWimLib.WimInfo wimInfo = wim.GetWimInfo();

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
                            const RefFlags refFlags = RefFlags.GLOB_ENABLE | RefFlags.GLOB_ERR_ON_NOMATCH;
                            wim.ReferenceResourceFile(splitWim, refFlags, openFlags);
                        }
                        catch (WimLibException e) when (e.ErrorCode == ErrorCode.GLOB_HAD_NO_MATCHES)
                        {
                            return LogInfo.LogErrorMessage(logs, $"Unable to find a match to [{splitWim}]");
                        }
                    }

                    // Apply to disk
                    s.MainViewModel.BuildCommandProgressTitle = "WimApply Progress";
                    s.MainViewModel.BuildCommandProgressText = string.Empty;
                    s.MainViewModel.BuildCommandProgressMax = 100;
                    s.MainViewModel.BuildCommandProgressShow = true;

                    try
                    {
                        wim.ExtractImage(imageIndex, destDir, extractFlags);

                        logs.Add(new LogInfo(LogState.Success, $"Applied [{srcWim}:{imageIndex}] to [{destDir}]"));
                    }
                    finally
                    { // Finalize Command Progress Report
                        s.MainViewModel.BuildCommandProgressShow = false;
                        s.MainViewModel.BuildCommandProgressTitle = "Progress";
                        s.MainViewModel.BuildCommandProgressText = string.Empty;
                        s.MainViewModel.BuildCommandProgressValue = 0;
                    }
                }
            }
            catch (WimLibException e)
            {
                logs.Add(CommandWim.LogWimLibException(e));
                return logs;
            }

            return logs;
        }

        private static CallbackStatus WimApplyExtractProgress(ProgressMsg msg, object info, object progctx)
        {
            EngineState s = progctx as EngineState;
            Debug.Assert(s != null);

            // EXTRACT_IMAGE_BEGIN
            // EXTRACT_FILE_STRUCTURE (Stage 1)
            // EXTRACT_STREAMS (Stage 2)
            // EXTRACT_METADATA (Stage 3)
            // EXTRACT_IMAGE_END
            switch (msg)
            {
                case ProgressMsg.EXTRACT_FILE_STRUCTURE:
                    {
                        ProgressInfo_Extract m = (ProgressInfo_Extract)info;

                        if (0 < m.EndFileCount)
                        {
                            ulong percentComplete = m.CurrentFileCount * 10 / m.EndFileCount;
                            s.MainViewModel.BuildCommandProgressValue = percentComplete;
                            s.MainViewModel.BuildCommandProgressText = $"[Stage 1] Creating files... ({percentComplete}%)";
                        }
                    }
                    break;
                case ProgressMsg.EXTRACT_STREAMS:
                    {
                        ProgressInfo_Extract m = (ProgressInfo_Extract)info;

                        if (0 < m.TotalBytes)
                        {
                            ulong percentComplete = 10 + m.CompletedBytes * 80 / m.TotalBytes;
                            s.MainViewModel.BuildCommandProgressValue = percentComplete;
                            s.MainViewModel.BuildCommandProgressText = $"[Stage 2] Extracting file data... ({percentComplete}%)";
                        }
                    }
                    break;
                case ProgressMsg.EXTRACT_METADATA:
                    {
                        ProgressInfo_Extract m = (ProgressInfo_Extract)info;

                        if (0 < m.EndFileCount)
                        {
                            ulong percentComplete = 90 + m.CurrentFileCount * 10 / m.EndFileCount;
                            s.MainViewModel.BuildCommandProgressValue = percentComplete;
                            s.MainViewModel.BuildCommandProgressText = $"[Stage 3] Applying metadata to files... ({percentComplete}%)";
                        }
                    }
                    break;
                case ProgressMsg.CALC_INTEGRITY:
                    {
                        ProgressInfo_Integrity m = (ProgressInfo_Integrity)info;

                        if (0 < m.TotalBytes)
                        {
                            ulong percentComplete = m.CompletedBytes * 100 / m.TotalBytes;
                            s.MainViewModel.BuildCommandProgressText = $"Calculating integrity... ({percentComplete}%)";
                        }
                    }
                    break;
            }
            return CallbackStatus.CONTINUE;
        }
        #endregion

        #region WimLib - WimExtract, WimExtractOp, WimExtractBulk
        public static List<LogInfo> WimExtract(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>(1);

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_WimExtract), "Invalid CodeInfo");
            CodeInfo_WimExtract info = cmd.Info as CodeInfo_WimExtract;
            Debug.Assert(info != null, "Invalid CodeInfo");

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
            OpenFlags openFlags = OpenFlags.DEFAULT;
            ExtractFlags extractFlags = ExtractFlags.NORPFIX | ExtractFlags.NO_PRESERVE_DIR_STRUCTURE;
            if (info.CheckFlag)
                openFlags |= OpenFlags.CHECK_INTEGRITY;
            if (info.NoAclFlag)
                extractFlags |= ExtractFlags.NO_ACLS;
            if (info.NoAttribFlag)
                extractFlags |= ExtractFlags.NO_ATTRIBUTES;

            // Flags for globbing
            if (StringHelper.IsWildcard(extractPath))
                extractFlags |= ExtractFlags.GLOB_PATHS;

            try
            {
                using (Wim wim = Wim.OpenWim(srcWim, openFlags, WimApplyExtractProgress, s))
                {
                    ManagedWimLib.WimInfo wimInfo = wim.GetWimInfo();

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
                            const RefFlags refFlags = RefFlags.GLOB_ENABLE | RefFlags.GLOB_ERR_ON_NOMATCH;
                            wim.ReferenceResourceFile(splitWim, refFlags, openFlags);
                        }
                        catch (WimLibException e) when (e.ErrorCode == ErrorCode.GLOB_HAD_NO_MATCHES)
                        {
                            return LogInfo.LogErrorMessage(logs, $"Unable to find a match to [{splitWim}]");
                        }
                    }

                    // Extract file(s)
                    s.MainViewModel.BuildCommandProgressTitle = "WimExtract Progress";
                    s.MainViewModel.BuildCommandProgressText = string.Empty;
                    s.MainViewModel.BuildCommandProgressMax = 100;
                    s.MainViewModel.BuildCommandProgressShow = true;

                    try
                    {
                        // Ignore GLOB_HAD_NO_MATCHES
                        wim.ExtractPath(imageIndex, destDir, extractPath, extractFlags);

                        logs.Add(new LogInfo(LogState.Success, $"Extracted [{extractPath}] to [{destDir}] from [{srcWim}:{imageIndex}]"));
                    }
                    finally
                    { // Finalize Command Progress Report
                        s.MainViewModel.BuildCommandProgressShow = false;
                        s.MainViewModel.BuildCommandProgressTitle = "Progress";
                        s.MainViewModel.BuildCommandProgressText = string.Empty;
                        s.MainViewModel.BuildCommandProgressValue = 0;
                    }
                }
            }
            catch (WimLibException e)
            {
                logs.Add(CommandWim.LogWimLibException(e));
                return logs;
            }

            return logs;
        }

        public static List<LogInfo> WimExtractOp(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_WimExtractOp), "Invalid CodeInfo");
            CodeInfo_WimExtractOp infoOp = cmd.Info as CodeInfo_WimExtractOp;
            Debug.Assert(infoOp != null, "Invalid CodeInfo");

            CodeInfo_WimExtract firstInfo = infoOp.Infos[0];
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
            OpenFlags openFlags = OpenFlags.DEFAULT;
            ExtractFlags extractFlags = ExtractFlags.NORPFIX | ExtractFlags.NO_PRESERVE_DIR_STRUCTURE;
            if (firstInfo.CheckFlag)
                openFlags |= OpenFlags.CHECK_INTEGRITY;
            if (firstInfo.NoAclFlag)
                extractFlags |= ExtractFlags.NO_ACLS;
            if (firstInfo.NoAttribFlag)
                extractFlags |= ExtractFlags.NO_ATTRIBUTES;

            List<string> extractPaths = new List<string>(infoOp.Cmds.Count);
            foreach (CodeInfo_WimExtract info in infoOp.Infos)
            {
                string extractPath = StringEscaper.Preprocess(s, info.ExtractPath);
                extractPaths.Add(extractPath);

                // Flags for globbing
                if (StringHelper.IsWildcard(extractPath))
                    extractFlags |= ExtractFlags.GLOB_PATHS;
            }

            try
            {
                using (Wim wim = Wim.OpenWim(srcWim, openFlags, WimApplyExtractProgress, s))
                {
                    ManagedWimLib.WimInfo wimInfo = wim.GetWimInfo();

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
                            const RefFlags refFlags = RefFlags.GLOB_ENABLE | RefFlags.GLOB_ERR_ON_NOMATCH;
                            wim.ReferenceResourceFile(splitWim, refFlags, openFlags);
                        }
                        catch (WimLibException e) when (e.ErrorCode == ErrorCode.GLOB_HAD_NO_MATCHES)
                        {
                            return LogInfo.LogErrorMessage(logs, $"Unable to find a match to [{splitWim}]");
                        }
                    }

                    // Extract files
                    s.MainViewModel.EnableBuildCommandProgress("WimExtract Progress", string.Empty, 100);

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
                        s.MainViewModel.DisableBuildCommandProgress();
                    }
                }
            }
            catch (WimLibException e)
            {
                logs.Add(CommandWim.LogWimLibException(e));
                return logs;
            }

            return logs;
        }

        public static List<LogInfo> WimExtractBulk(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>(1);

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_WimExtractBulk), "Invalid CodeInfo");
            CodeInfo_WimExtractBulk info = cmd.Info as CodeInfo_WimExtractBulk;
            Debug.Assert(info != null, "Invalid CodeInfo");

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
            OpenFlags openFlags = OpenFlags.DEFAULT;
            ExtractFlags extractFlags = ExtractFlags.NORPFIX;
            if (info.CheckFlag)
                openFlags |= OpenFlags.CHECK_INTEGRITY;
            if (info.NoAclFlag)
                extractFlags |= ExtractFlags.NO_ACLS;
            if (info.NoAttribFlag)
                extractFlags |= ExtractFlags.NO_ATTRIBUTES;
            ExtractFlags extractGlobFlags = extractFlags | ExtractFlags.GLOB_PATHS;

            // Check ListFile
            if (!File.Exists(listFilePath))
                return LogInfo.LogErrorMessage(logs, $"ListFile [{listFilePath}] does not exist");

            string unicodeListFile = Path.GetTempFileName();
            try
            {
                List<string> extractNormalPaths = new List<string>();
                List<string> extractGlobPaths = new List<string>();

                Encoding encoding = FileHelper.DetectTextEncoding(listFilePath);
                using (StreamReader r = new StreamReader(listFilePath, encoding, false))
                {
                    var extractPaths = r.ReadToEnd().Split('\n').Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim());
                    foreach (string path in extractPaths)
                    {
                        if (path[0] == ';' || path[0] == '#')
                            continue;

                        if (StringHelper.IsWildcard(path))
                            extractGlobPaths.Add(path);
                        else
                            extractNormalPaths.Add(path);
                    }
                }

                // Convert ListFile into UTF-16LE (wimlib only accepts UTF-8 or UTF-16LE ListFile)
                // FileHelper.ConvertTextFileToEncoding(listFilePath, unicodeListFile, Encoding.Unicode);

                using (Wim wim = Wim.OpenWim(srcWim, openFlags, WimApplyExtractProgress, s))
                {
                    ManagedWimLib.WimInfo wimInfo = wim.GetWimInfo();

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
                            const RefFlags refFlags = RefFlags.GLOB_ENABLE | RefFlags.GLOB_ERR_ON_NOMATCH;
                            wim.ReferenceResourceFile(splitWim, refFlags, openFlags);
                        }
                        catch (WimLibException e) when (e.ErrorCode == ErrorCode.GLOB_HAD_NO_MATCHES)
                        {
                            return LogInfo.LogErrorMessage(logs, $"Unable to find a match to [{splitWim}]");
                        }
                    }

                    // Extract file(s)
                    s.MainViewModel.BuildCommandProgressTitle = "WimExtractList Progress";
                    s.MainViewModel.BuildCommandProgressText = string.Empty;
                    s.MainViewModel.BuildCommandProgressMax = 100;
                    s.MainViewModel.BuildCommandProgressShow = true;

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
                            
                        logs.Add(new LogInfo(LogState.Success, $"Extracted files to [{destDir}] from [{srcWim}:{imageIndex}], based on [{listFilePath}]"));
                    }
                    finally
                    { // Finalize Command Progress Report
                        s.MainViewModel.BuildCommandProgressShow = false;
                        s.MainViewModel.BuildCommandProgressTitle = "Progress";
                        s.MainViewModel.BuildCommandProgressText = string.Empty;
                        s.MainViewModel.BuildCommandProgressValue = 0;
                    }
                }
            }
            catch (WimLibException e)
            {
                logs.Add(CommandWim.LogWimLibException(e));
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

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_WimCapture), "Invalid CodeInfo");
            CodeInfo_WimCapture info = cmd.Info as CodeInfo_WimCapture;
            Debug.Assert(info != null, "Invalid CodeInfo");

            string srcDir = StringEscaper.Preprocess(s, info.SrcDir);
            string destWim = StringEscaper.Preprocess(s, info.DestWim);
            string compStr = StringEscaper.Preprocess(s, info.Compress);

            // Check SrcDir
            if (!Directory.Exists(srcDir))
                return LogInfo.LogErrorMessage(logs, $"Directory [{srcDir}] does not exist");

            // Check DestWim
            if (StringEscaper.PathSecurityCheck(destWim, out string errorMsg) == false)
                return LogInfo.LogErrorMessage(logs, errorMsg);

            // Set Flags
            WriteFlags writeFlags = WriteFlags.DEFAULT;
            AddFlags addFlags = AddFlags.WINCONFIG | AddFlags.FILE_PATHS_UNNEEDED;
            if (info.BootFlag)
                addFlags |= AddFlags.BOOT;
            if (info.NoAclFlag)
                addFlags |= AddFlags.NO_ACLS;
            if (info.CheckFlag)
                writeFlags |= WriteFlags.CHECK_INTEGRITY;

            // Set Compression Type
            CompressionType compType = CompressionType.NONE;
            if (compStr.Equals("NONE", StringComparison.OrdinalIgnoreCase))
                compType = CompressionType.NONE;
            else if (compStr.Equals("XPRESS", StringComparison.OrdinalIgnoreCase))
                compType = CompressionType.XPRESS;
            else if (compStr.Equals("LZX", StringComparison.OrdinalIgnoreCase))
                compType = CompressionType.LZX;
            else if (compStr.Equals("LZMS", StringComparison.OrdinalIgnoreCase))
            {
                writeFlags |= WriteFlags.SOLID;
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

                    s.MainViewModel.BuildCommandProgressTitle = "WimCapture Progress";
                    s.MainViewModel.BuildCommandProgressText = string.Empty;
                    s.MainViewModel.BuildCommandProgressMax = 100;
                    s.MainViewModel.BuildCommandProgressShow = true;

                    try
                    {
                        wim.Write(destWim, Wim.AllImages, writeFlags, (uint)Environment.ProcessorCount);

                        logs.Add(new LogInfo(LogState.Success, $"Captured [{srcDir}] into [{destWim}]"));
                    }
                    finally
                    { // Finalize Command Progress Report
                        s.MainViewModel.BuildCommandProgressShow = false;
                        s.MainViewModel.BuildCommandProgressTitle = "Progress";
                        s.MainViewModel.BuildCommandProgressText = string.Empty;
                        s.MainViewModel.BuildCommandProgressValue = 0;
                    }
                }
            }
            catch (WimLibException e)
            {
                logs.Add(CommandWim.LogWimLibException(e));
                return logs;
            }

            return logs;
        }

        public static List<LogInfo> WimAppend(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>(1);

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_WimAppend), "Invalid CodeInfo");
            CodeInfo_WimAppend info = cmd.Info as CodeInfo_WimAppend;
            Debug.Assert(info != null, "Invalid CodeInfo");

            string srcDir = StringEscaper.Preprocess(s, info.SrcDir);
            string destWim = StringEscaper.Preprocess(s, info.DestWim);

            // Check SrcDir
            if (!Directory.Exists(srcDir))
                return LogInfo.LogErrorMessage(logs, $"Directory [{srcDir}] does not exist");

            // Check DestWim
            if (StringEscaper.PathSecurityCheck(destWim, out string errorMsg) == false)
                return LogInfo.LogErrorMessage(logs, errorMsg);

            // Set Flags
            OpenFlags openFlags = OpenFlags.WRITE_ACCESS;
            WriteFlags writeFlags = WriteFlags.DEFAULT;
            AddFlags addFlags = AddFlags.WINCONFIG | AddFlags.FILE_PATHS_UNNEEDED;
            if (info.BootFlag)
                addFlags |= AddFlags.BOOT;
            if (info.NoAclFlag)
                addFlags |= AddFlags.NO_ACLS;
            if (info.CheckFlag)
                writeFlags |= WriteFlags.CHECK_INTEGRITY;

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
                        ManagedWimLib.WimInfo wInfo = wim.GetWimInfo();
                        uint imageCount = wInfo.ImageCount;

                        string deltaIndexStr = StringEscaper.Preprocess(s, info.DeltaIndex);
                        if (!NumberHelper.ParseInt32(deltaIndexStr, out int deltaIndex))
                            return LogInfo.LogErrorMessage(logs, $"[{deltaIndexStr}] is not a valid positive integer");
                        if (!(1 <= deltaIndex && deltaIndex <= imageCount))
                            return LogInfo.LogErrorMessage(logs, $"[{deltaIndex}] must be [1] ~ [{imageCount}]");

                        wim.ReferenceTemplateImage((int)imageCount, deltaIndex);
                    }

                    // Appned to Wim
                    s.MainViewModel.BuildCommandProgressTitle = "WimAppend Progress";
                    s.MainViewModel.BuildCommandProgressText = string.Empty;
                    s.MainViewModel.BuildCommandProgressMax = 100;
                    s.MainViewModel.BuildCommandProgressShow = true;

                    try
                    {
                        wim.Overwrite(writeFlags, (uint)Environment.ProcessorCount);

                        logs.Add(new LogInfo(LogState.Success, $"Appended [{srcDir}] into [{destWim}]"));
                    }
                    finally
                    { // Finalize Command Progress Report
                        s.MainViewModel.BuildCommandProgressShow = false;
                        s.MainViewModel.BuildCommandProgressTitle = "Progress";
                        s.MainViewModel.BuildCommandProgressText = string.Empty;
                        s.MainViewModel.BuildCommandProgressValue = 0;
                    }
                }
            }
            catch (WimLibException e)
            {
                logs.Add(CommandWim.LogWimLibException(e));
                return logs;
            }

            return logs;
        }

        private static CallbackStatus WimWriteProgress(ProgressMsg msg, object info, object progctx)
        {
            EngineState s = progctx as EngineState;
            Debug.Assert(s != null);

            // SCAN_BEGIN
            // SCAN_DENTRY (Stage 1)
            // SCAN_END
            // WRITE_STREAMS (Stage 2)

            switch (msg)
            {
                case ProgressMsg.SCAN_BEGIN:
                    {
                        ProgressInfo_Scan m = (ProgressInfo_Scan)info;

                        s.MainViewModel.BuildCommandProgressText = $"[Stage 1] Scanning {m.Source}...";
                    }
                    break;
                case ProgressMsg.WRITE_STREAMS:
                    {
                        ProgressInfo_WriteStreams m = (ProgressInfo_WriteStreams)info;

                        if (0 < m.TotalBytes)
                        {
                            ulong percentComplete = m.CompletedBytes * 100 / m.TotalBytes;
                            s.MainViewModel.BuildCommandProgressValue = percentComplete;
                            s.MainViewModel.BuildCommandProgressText = $"[Stage 2] Writing... ({percentComplete}%)";
                        }
                    }
                    break;
                case ProgressMsg.CALC_INTEGRITY:
                    {
                        ProgressInfo_Integrity m = (ProgressInfo_Integrity)info;

                        if (0 < m.TotalBytes)
                        {
                            ulong percentComplete = m.CompletedBytes * 100 / m.TotalBytes;
                            s.MainViewModel.BuildCommandProgressText = $"Calculating integrity... ({percentComplete}%)";
                        }
                    }
                    break;
            }
            return CallbackStatus.CONTINUE;
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

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_WimDelete), "Invalid CodeInfo");
            CodeInfo_WimDelete info = cmd.Info as CodeInfo_WimDelete;
            Debug.Assert(info != null, "Invalid CodeInfo");

            string srcWim = StringEscaper.Preprocess(s, info.SrcWim);
            string imageIndexStr = StringEscaper.Preprocess(s, info.ImageIndex);

            // Check SrcWim
            if (!File.Exists(srcWim))
                return LogInfo.LogErrorMessage(logs, $"File [{srcWim}] does not exist");

            // Set Flags
            OpenFlags openFlags = OpenFlags.WRITE_ACCESS;
            WriteFlags writeFlags = WriteFlags.DEFAULT;
            if (info.CheckFlag)
                writeFlags |= WriteFlags.CHECK_INTEGRITY;

            try
            {
                using (Wim wim = Wim.OpenWim(srcWim, openFlags))
                {
                    wim.RegisterCallback(WimDeleteProgress, s);

                    ManagedWimLib.WimInfo wi = wim.GetWimInfo();

                    // Check imageIndex
                    if (!NumberHelper.ParseInt32(imageIndexStr, out int imageIndex))
                        return LogInfo.LogErrorMessage(logs, $"[{imageIndexStr}] is not a valid positive integer");
                    if (!(1 <= imageIndex && imageIndex <= wi.ImageCount))
                        return LogInfo.LogErrorMessage(logs, $"[{imageIndexStr}] must be [1] ~ [{wi.ImageCount}]");

                    wim.DeleteImage(imageIndex);

                    s.MainViewModel.BuildCommandProgressTitle = "WimDelete Progress";
                    s.MainViewModel.BuildCommandProgressText = string.Empty;
                    s.MainViewModel.BuildCommandProgressMax = 100;
                    s.MainViewModel.BuildCommandProgressShow = true;

                    try
                    {
                        wim.Overwrite(writeFlags, (uint)Environment.ProcessorCount);

                        logs.Add(new LogInfo(LogState.Success, $"Deleted index [{imageIndex}] from [{srcWim}]"));
                    }
                    finally
                    { // Finalize Command Progress Report
                        s.MainViewModel.BuildCommandProgressShow = false;
                        s.MainViewModel.BuildCommandProgressTitle = "Progress";
                        s.MainViewModel.BuildCommandProgressText = string.Empty;
                        s.MainViewModel.BuildCommandProgressValue = 0;
                    }
                }
            }
            catch (WimLibException e)
            {
                logs.Add(CommandWim.LogWimLibException(e));
                return logs;
            }

            return logs;
        }

        private static CallbackStatus WimDeleteProgress(ProgressMsg msg, object info, object progctx)
        {
            EngineState s = progctx as EngineState;
            Debug.Assert(s != null);

            // WRITE_STREAMS 
            switch (msg)
            {
                case ProgressMsg.WRITE_STREAMS:
                    {
                        ProgressInfo_WriteStreams m = (ProgressInfo_WriteStreams)info;

                        if (0 < m.TotalBytes)
                        {
                            ulong percentComplete = m.CompletedBytes * 100 / m.TotalBytes;
                            s.MainViewModel.BuildCommandProgressValue = percentComplete;
                            s.MainViewModel.BuildCommandProgressText = $"Writing... ({percentComplete}%)";
                        }
                    }
                    break;
                case ProgressMsg.CALC_INTEGRITY:
                    {
                        ProgressInfo_Integrity m = (ProgressInfo_Integrity)info;

                        if (0 < m.TotalBytes)
                        {
                            ulong percentComplete = m.CompletedBytes * 100 / m.TotalBytes;
                            s.MainViewModel.BuildCommandProgressText = $"Calculating integrity... ({percentComplete}%)";
                        }
                    }
                    break;
            }
            return CallbackStatus.CONTINUE;
        }
        #endregion

        #region WimLib - WimPathAdd, WimPathDelete, WimPathRemove, WimPathOp
        public static List<LogInfo> WimPathAdd(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>(1);

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_WimPathAdd), "Invalid CodeInfo");
            CodeInfo_WimPathAdd info = cmd.Info as CodeInfo_WimPathAdd;
            Debug.Assert(info != null, "Invalid CodeInfo");

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
            const OpenFlags openFlags = OpenFlags.WRITE_ACCESS;
            const UpdateFlags updateFlags = UpdateFlags.SEND_PROGRESS;
            WriteFlags writeFlags = WriteFlags.DEFAULT;
            AddFlags addFlags = AddFlags.WINCONFIG | AddFlags.VERBOSE | AddFlags.EXCLUDE_VERBOSE;
            if (info.CheckFlag)
                writeFlags |= WriteFlags.CHECK_INTEGRITY;
            if (info.RebuildFlag)
                writeFlags |= WriteFlags.REBUILD;
            if (info.NoAclFlag)
                addFlags |= AddFlags.NO_ACLS;
            if (info.PreserveFlag)
                addFlags |= AddFlags.NO_REPLACE;
            
            try
            {
                using (Wim wim = Wim.OpenWim(wimFile, openFlags))
                {
                    wim.RegisterCallback(WimPathProgress, s);

                    ManagedWimLib.WimInfo wi = wim.GetWimInfo();
                    if (!NumberHelper.ParseInt32(imageIndexStr, out int imageIndex))
                        return LogInfo.LogErrorMessage(logs, $"[{imageIndexStr}] is not a valid positive integer");
                    if (!(1 <= imageIndex && imageIndex <= wi.ImageCount))
                        return LogInfo.LogErrorMessage(logs, $"[{imageIndexStr}] must be [1] ~ [{wi.ImageCount}]");

                    UpdateCommand addCmd = UpdateCommand.SetAdd(srcPath, destPath, null, addFlags);
                    wim.UpdateImage(imageIndex, addCmd, updateFlags);

                    s.MainViewModel.EnableBuildCommandProgress("WimPathAdd Progress", string.Empty, 100);
                    try
                    {
                        wim.Overwrite(writeFlags, (uint)Environment.ProcessorCount);

                        logs.Add(new LogInfo(LogState.Success, $"Added [{srcPath}] into [{wimFile}]"));
                    }
                    finally
                    { // Finalize Command Progress Report
                        s.MainViewModel.DisableBuildCommandProgress();
                    }
                }
            }
            catch (WimLibException e)
            {
                logs.Add(CommandWim.LogWimLibException(e));
                return logs;
            }

            return logs;
        }

        public static List<LogInfo> WimPathDelete(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>(1);

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_WimPathDelete), "Invalid CodeInfo");
            CodeInfo_WimPathDelete info = cmd.Info as CodeInfo_WimPathDelete;
            Debug.Assert(info != null, "Invalid CodeInfo");

            string wimFile = StringEscaper.Preprocess(s, info.WimFile);
            string imageIndexStr = StringEscaper.Preprocess(s, info.ImageIndex);
            string path = StringEscaper.Preprocess(s, info.Path);

            // Check wimFile
            if (!File.Exists(wimFile))
                return LogInfo.LogErrorMessage(logs, $"File [{wimFile}] does not exist");
            if (StringEscaper.PathSecurityCheck(wimFile, out string errorMsg) == false)
                return LogInfo.LogErrorMessage(logs, errorMsg);

            // Set Flags
            const OpenFlags openFlags = OpenFlags.WRITE_ACCESS;
            const UpdateFlags updateFlags = UpdateFlags.SEND_PROGRESS;
            WriteFlags writeFlags = WriteFlags.DEFAULT;
            const DeleteFlags deleteFlags = DeleteFlags.RECURSIVE;
            if (info.CheckFlag)
                writeFlags |= WriteFlags.CHECK_INTEGRITY;
            if (info.RebuildFlag)
                writeFlags |= WriteFlags.REBUILD;

            try
            {
                using (Wim wim = Wim.OpenWim(wimFile, openFlags, WimPathProgress, s))
                {
                    ManagedWimLib.WimInfo wi = wim.GetWimInfo();
                    if (!NumberHelper.ParseInt32(imageIndexStr, out int imageIndex))
                        return LogInfo.LogErrorMessage(logs, $"[{imageIndexStr}] is not a valid positive integer");
                    if (!(1 <= imageIndex && imageIndex <= wi.ImageCount))
                        return LogInfo.LogErrorMessage(logs, $"[{imageIndexStr}] must be [1] ~ [{wi.ImageCount}]");

                    UpdateCommand deleteCmd = UpdateCommand.SetDelete(path, deleteFlags);
                    wim.UpdateImage(imageIndex, deleteCmd, updateFlags);

                    s.MainViewModel.EnableBuildCommandProgress("WimPathDelete Progress", string.Empty, 100);
                    try
                    {
                        wim.Overwrite(writeFlags, (uint)Environment.ProcessorCount);

                        logs.Add(new LogInfo(LogState.Success, $"[{path}] deleted from [{wimFile}]"));
                    }
                    finally
                    { // Finalize Command Progress Report
                        s.MainViewModel.DisableBuildCommandProgress();
                    }
                }
            }
            catch (WimLibException e)
            {
                logs.Add(CommandWim.LogWimLibException(e));
                return logs;
            }

            return logs;
        }

        public static List<LogInfo> WimPathRename(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>(1);

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_WimPathRename), "Invalid CodeInfo");
            CodeInfo_WimPathRename info = cmd.Info as CodeInfo_WimPathRename;
            Debug.Assert(info != null, "Invalid CodeInfo");

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
            const OpenFlags openFlags = OpenFlags.WRITE_ACCESS;
            const UpdateFlags updateFlags = UpdateFlags.SEND_PROGRESS;
            WriteFlags writeFlags = WriteFlags.DEFAULT;
            if (info.CheckFlag)
                writeFlags |= WriteFlags.CHECK_INTEGRITY;
            if (info.RebuildFlag)
                writeFlags |= WriteFlags.REBUILD;

            try
            {
                using (Wim wim = Wim.OpenWim(wimFile, openFlags, WimPathProgress, s))
                {
                    ManagedWimLib.WimInfo wi = wim.GetWimInfo();
                    if (!NumberHelper.ParseInt32(imageIndexStr, out int imageIndex))
                        return LogInfo.LogErrorMessage(logs, $"[{imageIndexStr}] is not a valid positive integer");
                    if (!(1 <= imageIndex && imageIndex <= wi.ImageCount))
                        return LogInfo.LogErrorMessage(logs, $"[{imageIndexStr}] must be [1] ~ [{wi.ImageCount}]");

                    UpdateCommand renCmd = UpdateCommand.SetRename(srcPath, destPath);
                    wim.UpdateImage(imageIndex, renCmd, updateFlags);

                    s.MainViewModel.EnableBuildCommandProgress("WimPathRename Progress", string.Empty, 100);
                    try
                    {
                        wim.Overwrite(writeFlags, (uint)Environment.ProcessorCount);

                        logs.Add(new LogInfo(LogState.Success, $"Renamed [{srcPath}] to [{destPath}] in [{wimFile}]"));
                    }
                    finally
                    { // Finalize Command Progress Report
                        s.MainViewModel.DisableBuildCommandProgress();
                    }
                }
            }
            catch (WimLibException e)
            {
                logs.Add(CommandWim.LogWimLibException(e));
                return logs;
            }

            return logs;
        }

        public static List<LogInfo> WimPathOp(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_WimPathOp infoOp = cmd.Info.Cast<CodeInfo_WimPathOp>();

            string wimFile;
            string imageIndexStr;
            bool checkFlag;
            bool rebuildFlag;

            CodeCommand firstCmd = infoOp.Cmds[0];
            switch (firstCmd.Type)
            {
                case CodeType.WimPathAdd:
                {
                    CodeInfo_WimPathAdd firstInfo = firstCmd.Info.Cast<CodeInfo_WimPathAdd>();
                    wimFile = StringEscaper.Preprocess(s, firstInfo.WimFile);
                    imageIndexStr = StringEscaper.Preprocess(s, firstInfo.ImageIndex);
                    checkFlag = firstInfo.CheckFlag;
                    rebuildFlag = firstInfo.RebuildFlag;
                    break;
                }
                case CodeType.WimPathDelete:
                {
                    CodeInfo_WimPathDelete firstInfo = firstCmd.Info.Cast<CodeInfo_WimPathDelete>();
                    wimFile = StringEscaper.Preprocess(s, firstInfo.WimFile);
                    imageIndexStr = StringEscaper.Preprocess(s, firstInfo.ImageIndex);
                    checkFlag = firstInfo.CheckFlag;
                    rebuildFlag = firstInfo.RebuildFlag;
                    break;
                }
                case CodeType.WimPathRename:
                {
                    CodeInfo_WimPathRename firstInfo = firstCmd.Info.Cast<CodeInfo_WimPathRename>();
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
            const OpenFlags openFlags = OpenFlags.WRITE_ACCESS;
            const UpdateFlags updateFlags = UpdateFlags.SEND_PROGRESS;
            WriteFlags writeFlags = WriteFlags.DEFAULT;
            if (checkFlag)
                writeFlags |= WriteFlags.CHECK_INTEGRITY;
            if (rebuildFlag)
                writeFlags |= WriteFlags.REBUILD;

            // Make list of UpdateCommand
            List<LogInfo> wimLogs = new List<LogInfo>(infoOp.Cmds.Count);
            List<UpdateCommand> wimUpdateCmds = new List<UpdateCommand>(infoOp.Cmds.Count);
            foreach (CodeCommand subCmd in infoOp.Cmds)
            {
                switch (subCmd.Type)
                {
                    case CodeType.WimPathAdd:
                    {
                        CodeInfo_WimPathAdd info = subCmd.Info.Cast<CodeInfo_WimPathAdd>();

                        string srcPath = StringEscaper.Preprocess(s, info.SrcPath);
                        string destPath = StringEscaper.Preprocess(s, info.DestPath);

                        AddFlags addFlags = AddFlags.WINCONFIG | AddFlags.VERBOSE | AddFlags.EXCLUDE_VERBOSE;
                        if (info.NoAclFlag)
                            addFlags |= AddFlags.NO_ACLS;
                        if (info.PreserveFlag)
                            addFlags |= AddFlags.NO_REPLACE;

                        UpdateCommand addCmd = UpdateCommand.SetAdd(srcPath, destPath, null, addFlags);
                        wimUpdateCmds.Add(addCmd);

                        wimLogs.Add(new LogInfo(LogState.Success, $"Added [{srcPath}] into [{wimFile}]", subCmd));
                        break;
                    }
                    case CodeType.WimPathDelete:
                    {
                        CodeInfo_WimPathDelete info = subCmd.Info.Cast<CodeInfo_WimPathDelete>();

                        string path = StringEscaper.Preprocess(s, info.Path);

                        const DeleteFlags deleteFlags = DeleteFlags.RECURSIVE;

                        UpdateCommand deleteCmd = UpdateCommand.SetDelete(path, deleteFlags);
                        wimUpdateCmds.Add(deleteCmd);

                        wimLogs.Add(new LogInfo(LogState.Success, $"[{path}] deleted from [{wimFile}]", subCmd));
                        break;
                    }
                    case CodeType.WimPathRename:
                    {
                        CodeInfo_WimPathRename info = subCmd.Info.Cast<CodeInfo_WimPathRename>();

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
                    ManagedWimLib.WimInfo wi = wim.GetWimInfo();
                    if (!NumberHelper.ParseInt32(imageIndexStr, out int imageIndex))
                        return LogInfo.LogErrorMessage(logs, $"[{imageIndexStr}] is not a valid positive integer");
                    if (!(1 <= imageIndex && imageIndex <= wi.ImageCount))
                        return LogInfo.LogErrorMessage(logs, $"[{imageIndexStr}] must be [1] ~ [{wi.ImageCount}]");

                    wim.UpdateImage(imageIndex, wimUpdateCmds, updateFlags);

                    s.MainViewModel.EnableBuildCommandProgress("WimPath Progress", string.Empty, 100);
                    try
                    {
                        wim.Overwrite(writeFlags, (uint)Environment.ProcessorCount);

                        logs.AddRange(wimLogs);
                        logs.Add(new LogInfo(LogState.Success, $"Updated [{infoOp.Cmds.Count}] files from [{wimFile}:{imageIndex}]"));
                    }
                    finally
                    { // Finalize Command Progress Report
                        s.MainViewModel.DisableBuildCommandProgress();
                    }
                }
            }
            catch (WimLibException e)
            {
                logs.Add(CommandWim.LogWimLibException(e));
                return logs;
            }

            return logs;
        }

        private static CallbackStatus WimPathProgress(ProgressMsg msg, object info, object progctx)
        {
            EngineState s = progctx as EngineState;
            Debug.Assert(s != null);

            // UPDATE_BEGIN_COMMAND
            // SCAN_BEGIN
            // SCAN_END
            // UPDATE_END_COMMAND
            // WRITE_STREAMS

            switch (msg)
            {
                case ProgressMsg.UPDATE_END_COMMAND:
                    {
                        ProgressInfo_Update m = (ProgressInfo_Update)info;

                        UpdateCommand upCmd = m.Command;
                        string str;
                        switch (upCmd.Op)
                        {
                            case UpdateOp.ADD:
                                var add = upCmd.Add;
                                str = $"[Stage 1] Adding {add.FsSourcePath}... ({m.CompletedCommands}/{m.TotalCommands})";
                                break;
                            case UpdateOp.DELETE:
                                var del = upCmd.Delete;
                                str = $"[Stage 1] Deleting {del.WimPath}... ({m.CompletedCommands}/{m.TotalCommands})";
                                break;
                            case UpdateOp.RENAME:
                                var ren = upCmd.Rename;
                                str = $"[Stage 1] Renaming {ren.WimSourcePath} to {ren.WimTargetPath}... ({m.CompletedCommands}/{m.TotalCommands})";
                                break;
                            default:
                                throw new InternalException("Internal Logic Error at WimPathProgress");
                        }

                        s.MainViewModel.BuildCommandProgressText = str;
                    }
                    break;
                case ProgressMsg.WRITE_STREAMS:
                    {
                        ProgressInfo_WriteStreams m = (ProgressInfo_WriteStreams)info;

                        if (0 < m.TotalBytes)
                        {
                            ulong percentComplete = m.CompletedBytes * 100 / m.TotalBytes;
                            s.MainViewModel.BuildCommandProgressValue = percentComplete;
                            s.MainViewModel.BuildCommandProgressText = $"[Stage 2] Writing... ({percentComplete}%)";
                        }
                    }
                    break;
                case ProgressMsg.CALC_INTEGRITY:
                    {
                        ProgressInfo_Integrity m = (ProgressInfo_Integrity)info;

                        if (0 < m.TotalBytes)
                        {
                            ulong percentComplete = m.CompletedBytes * 100 / m.TotalBytes;
                            s.MainViewModel.BuildCommandProgressText = $"Calculating integrity... ({percentComplete}%)";
                        }
                    }
                    break;
            }
            return CallbackStatus.CONTINUE;
        }
        #endregion

        #region WimLib - WimOptimize
        public static List<LogInfo> WimOptimize(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>(1);

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_WimOptimize), "Invalid CodeInfo");
            CodeInfo_WimOptimize info = cmd.Info as CodeInfo_WimOptimize;
            Debug.Assert(info != null, "Invalid CodeInfo");

            string wimFile = StringEscaper.Preprocess(s, info.WimFile);

            // Check SrcWim
            if (!File.Exists(wimFile))
                return LogInfo.LogErrorMessage(logs, $"File [{wimFile}] does not exist");
            if (StringEscaper.PathSecurityCheck(wimFile, out string errorMsg) == false)
                return LogInfo.LogErrorMessage(logs, errorMsg);

            // Set Flags
            OpenFlags openFlags = OpenFlags.WRITE_ACCESS;
            WriteFlags writeFlags = WriteFlags.REBUILD;
            CompressionType? compType = null;
            if (info.Recompress != null)
            {
                string recompStr = StringEscaper.Preprocess(s, info.Recompress);

                writeFlags |= WriteFlags.RECOMPRESS;

                // Set Compression Type
                // NONE, XPRESS, LZX, LZMS : Recompress file with specified algorithm
                // KEEP : Recompress file with current compresssoin algorithm
                if (recompStr.Equals("NONE", StringComparison.OrdinalIgnoreCase))
                    compType = CompressionType.NONE;
                else if (recompStr.Equals("XPRESS", StringComparison.OrdinalIgnoreCase))
                    compType = CompressionType.XPRESS;
                else if (recompStr.Equals("LZX", StringComparison.OrdinalIgnoreCase))
                    compType = CompressionType.LZX;
                else if (recompStr.Equals("LZMS", StringComparison.OrdinalIgnoreCase))
                {
                    writeFlags |= WriteFlags.SOLID;
                    compType = CompressionType.LZMS;
                }
                else if (!recompStr.Equals("KEEP", StringComparison.OrdinalIgnoreCase)) 
                    return LogInfo.LogErrorMessage(logs, $"Invalid compression type [{recompStr}].");
            }

            if (info.CheckFlag == true)
                writeFlags |= WriteFlags.CHECK_INTEGRITY;
            else if (info.CheckFlag == false)
                writeFlags |= WriteFlags.NO_CHECK_INTEGRITY;

            try
            {
                using (Wim wim = Wim.OpenWim(wimFile, openFlags))
                {
                    wim.RegisterCallback(WimSimpleWriteProgress, s);

                    if (compType != null)
                        wim.SetOutputCompressionType((CompressionType)compType);

                    s.MainViewModel.BuildCommandProgressTitle = "WimOptimize Progress";
                    s.MainViewModel.BuildCommandProgressText = string.Empty;
                    s.MainViewModel.BuildCommandProgressMax = 100;
                    s.MainViewModel.BuildCommandProgressShow = true;

                    try
                    {
                        long before = new FileInfo(wimFile).Length;
                        wim.Overwrite(writeFlags, (uint)Environment.ProcessorCount);
                        long after = new FileInfo(wimFile).Length;

                        string beforeStr = NumberHelper.ByteSizeToHumanReadableString(before);
                        string afterStr = NumberHelper.ByteSizeToHumanReadableString(after);
                        logs.Add(new LogInfo(LogState.Success, $"Optimized [{wimFile}] from [{beforeStr}] to [{afterStr}]"));
                    }
                    finally
                    { // Finalize Command Progress Report
                        s.MainViewModel.BuildCommandProgressShow = false;
                        s.MainViewModel.BuildCommandProgressTitle = "Progress";
                        s.MainViewModel.BuildCommandProgressText = string.Empty;
                        s.MainViewModel.BuildCommandProgressValue = 0;
                    }
                }
            }
            catch (WimLibException e)
            {
                logs.Add(CommandWim.LogWimLibException(e));
                return logs;
            }

            return logs;
        }
        #endregion

        #region WimLib - WimExport
        public static List<LogInfo> WimExport(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>(1);

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_WimExport), "Invalid CodeInfo");
            CodeInfo_WimExport info = cmd.Info as CodeInfo_WimExport;
            Debug.Assert(info != null, "Invalid CodeInfo");

            string srcWimPath = StringEscaper.Preprocess(s, info.SrcWim);
            string imageIndexStr = StringEscaper.Preprocess(s, info.ImageIndex);
            string destWimPath = StringEscaper.Preprocess(s, info.DestWim);
            string imageName = null;
            if (info.ImageName != null)
                imageName = StringEscaper.Preprocess(s, info.ImageName);
            string imageDesc = null;
            if (info.ImageDesc != null)
                imageDesc = StringEscaper.Preprocess(s, info.ImageDesc);

            // Check SrcWim
            if (!File.Exists(srcWimPath))
                return LogInfo.LogErrorMessage(logs, $"File [{srcWimPath}] does not exist");

            // Check DestWim
            if (StringEscaper.PathSecurityCheck(destWimPath, out string errorMsg) == false)
                return LogInfo.LogErrorMessage(logs, errorMsg);

            // Set Flags
            WriteFlags writeFlags = WriteFlags.REBUILD;
            ExportFlags exportFlags = ExportFlags.GIFT;

            if (info.BootFlag)
                exportFlags |= ExportFlags.BOOT;
            if (info.CheckFlag == true)
                writeFlags |= WriteFlags.CHECK_INTEGRITY;
            else if (info.CheckFlag == false)
                writeFlags |= WriteFlags.NO_CHECK_INTEGRITY;

            try
            {
                using (Wim srcWim = Wim.OpenWim(srcWimPath, OpenFlags.DEFAULT))
                {
                    ManagedWimLib.WimInfo wi = srcWim.GetWimInfo();

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
                            const RefFlags refFlags = RefFlags.GLOB_ENABLE | RefFlags.GLOB_ERR_ON_NOMATCH;
                            srcWim.ReferenceResourceFile(splitWim, refFlags, OpenFlags.DEFAULT);
                        }
                        catch (WimLibException e) when (e.ErrorCode == ErrorCode.GLOB_HAD_NO_MATCHES)
                        {
                            return LogInfo.LogErrorMessage(logs, $"Unable to find a match to [{splitWim}]");
                        }
                    }

                    s.MainViewModel.BuildCommandProgressTitle = "WimExport Progress";
                    s.MainViewModel.BuildCommandProgressText = string.Empty;
                    s.MainViewModel.BuildCommandProgressMax = 100;
                    s.MainViewModel.BuildCommandProgressShow = true;

                    try
                    {
                        if (File.Exists(destWimPath))
                        { // Append to existing wim file
                            // Set Compression Type
                            // Use of compress argument [NONE|XPRESS|LZX|LZMS] is prohibitted
                            if (info.Recompress != null)
                            {
                                string compStr = StringEscaper.Preprocess(s, info.Recompress);
                                if (!compStr.Equals("KEEP", StringComparison.OrdinalIgnoreCase))
                                    return LogInfo.LogErrorMessage(logs, $"Invalid compression type [{compStr}]. You must use [KEEP] when exporting to an existing wim file");

                                writeFlags |= WriteFlags.RECOMPRESS;
                            }

                            uint destWimCount;
                            using (Wim destWim = Wim.OpenWim(destWimPath, OpenFlags.WRITE_ACCESS))
                            {
                                destWim.RegisterCallback(WimSimpleWriteProgress, s);

                                // Get destWim's imageCount
                                ManagedWimLib.WimInfo dwi = destWim.GetWimInfo();
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
                                    compType = CompressionType.NONE;
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
                                    writeFlags |= WriteFlags.SOLID;
                                writeFlags |= WriteFlags.RECOMPRESS;
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
                        s.MainViewModel.BuildCommandProgressShow = false;
                        s.MainViewModel.BuildCommandProgressTitle = "Progress";
                        s.MainViewModel.BuildCommandProgressText = string.Empty;
                        s.MainViewModel.BuildCommandProgressValue = 0;
                    }
                }
            }
            catch (WimLibException e)
            {
                logs.Add(CommandWim.LogWimLibException(e));
                return logs;
            }

            return logs;
        }
        #endregion

        #region WimLib - WimSimpleWriteProgress
        private static CallbackStatus WimSimpleWriteProgress(ProgressMsg msg, object info, object progctx)
        {
            EngineState s = progctx as EngineState;
            Debug.Assert(s != null);

            // WRITE_STREAMS 
            switch (msg)
            {
                case ProgressMsg.WRITE_STREAMS:
                    {
                        ProgressInfo_WriteStreams m = (ProgressInfo_WriteStreams)info;

                        if (0 < m.TotalBytes)
                        {
                            ulong percentComplete = m.CompletedBytes * 100 / m.TotalBytes;
                            s.MainViewModel.BuildCommandProgressValue = percentComplete;
                            s.MainViewModel.BuildCommandProgressText = $"Writing... ({percentComplete}%)";
                        }
                    }
                    break;
                case ProgressMsg.CALC_INTEGRITY:
                    {
                        ProgressInfo_Integrity m = (ProgressInfo_Integrity)info;

                        if (0 < m.TotalBytes)
                        {
                            ulong percentComplete = m.CompletedBytes * 100 / m.TotalBytes;
                            s.MainViewModel.BuildCommandProgressText = $"Calculating integrity... ({percentComplete}%)";
                        }
                    }
                    break;
            }
            return CallbackStatus.CONTINUE;
        }
        #endregion
    }
}
