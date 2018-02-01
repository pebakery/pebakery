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
using PEBakery.Helper;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PEBakery.Core.Commands
{
    public static class CommandWim
    {
        #region Wimgapi - WimMount, WimUnmount
        public static List<LogInfo> WimMount(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>(1);

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_WimMount));
            CodeInfo_WimMount info = cmd.Info as CodeInfo_WimMount;

            string srcWim = StringEscaper.Preprocess(s, info.SrcWim);
            string imageIndexStr = StringEscaper.Preprocess(s, info.ImageIndex);
            string mountDir = StringEscaper.Preprocess(s, info.MountDir);
            string mountOptionStr = StringEscaper.Preprocess(s, info.MountOption);

            // Mount Option
            bool readwrite;
            if (mountOptionStr.Equals("READONLY", StringComparison.OrdinalIgnoreCase)) readwrite = false;
            else if (mountOptionStr.Equals("READWRITE", StringComparison.OrdinalIgnoreCase)) readwrite = true;
            else
            {
                logs.Add(new LogInfo(LogState.Error, $"Invalid mount option [{mountOptionStr}]"));
                return logs;
            }

            // Check srcWim
            if (!File.Exists(srcWim))
            {
                logs.Add(new LogInfo(LogState.Error, $"File [{srcWim}] does not exist"));
                return logs;
            }

            // Check MountDir 
            if (StringEscaper.PathSecurityCheck(mountDir, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            if (!Directory.Exists(mountDir))
            {
                logs.Add(new LogInfo(LogState.Error, $"Directory [{mountDir}] does not exist"));
                return logs;
            }

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
                logs.Add(CommandWim.LogWimgApiException(e, $"Unable to get information of [{srcWim}]"));
                return logs;
            }

            if (!NumberHelper.ParseInt32(imageIndexStr, out int imageIndex))
            {
                logs.Add(new LogInfo(LogState.Error, $"[{imageIndexStr}] is not a valid a positive integer"));
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

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_WimUnmount));
            CodeInfo_WimUnmount info = cmd.Info as CodeInfo_WimUnmount;

            string mountDir = StringEscaper.Preprocess(s, info.MountDir);
            string unmountOptionStr = StringEscaper.Preprocess(s, info.UnmountOption);

            bool commit;
            if (unmountOptionStr.Equals("DISCARD", StringComparison.OrdinalIgnoreCase))
                commit = false;
            else if (unmountOptionStr.Equals("COMMIT", StringComparison.OrdinalIgnoreCase))
                commit = true;
            else
            {
                logs.Add(new LogInfo(LogState.Error, $"Invalid unmount option [{unmountOptionStr}]"));
                return logs;
            }

            // Check MountDir 
            if (!Directory.Exists(mountDir))
            {
                logs.Add(new LogInfo(LogState.Error, $"Directory [{mountDir}] does not exist"));
                return logs;
            }

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

        #region WimLib - WimApply, WimExtract
        public static List<LogInfo> WimApply(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>(1);

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_WimApply));
            CodeInfo_WimApply info = cmd.Info as CodeInfo_WimApply;

            string srcWim = StringEscaper.Preprocess(s, info.SrcWim);
            string imageIndexStr = StringEscaper.Preprocess(s, info.ImageIndex);
            string destDir = StringEscaper.Preprocess(s, info.DestDir);

            // Check SrcWim
            if (!File.Exists(srcWim))
            {
                logs.Add(new LogInfo(LogState.Error, $"File [{srcWim}] does not exist"));
                return logs;
            }

            // Check DestDir
            if (StringEscaper.PathSecurityCheck(destDir, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            if (!Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            // Set Flags
            WimLibOpenFlags openFlags = WimLibOpenFlags.DEFAULT;
            WimLibExtractFlags extractFlags = WimLibExtractFlags.DEFAULT;
            if (info.CheckFlag)
                openFlags |= WimLibOpenFlags.CHECK_INTEGRITY;
            if (info.NoAclFlag)
                extractFlags |= WimLibExtractFlags.NO_ACLS;
            if (info.NoAttribFlag)
                extractFlags |= WimLibExtractFlags.NO_ATTRIBUTES;

            
            try
            {
                using (Wim wim = Wim.OpenWim(srcWim, openFlags, WimApplyExtractProgress, s))
                {
                    ManagedWimLib.WimInfo wimInfo = wim.GetWimInfo();

                    // Check imageIndex
                    if (!NumberHelper.ParseInt32(imageIndexStr, out int imageIndex))
                    {
                        logs.Add(new LogInfo(LogState.Error, $"[{imageIndexStr}] is not a valid a positive integer"));
                        return logs;
                    }

                    if (!(1 <= imageIndex && imageIndex <= wimInfo.ImageCount))
                    {
                        logs.Add(new LogInfo(LogState.Error, $"[{imageIndexStr}] must be [1] ~ [{wimInfo.ImageCount}]"));
                        return logs;
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

        private static WimLibProgressStatus WimApplyExtractProgress(WimLibProgressMsg msg, object info, object progctx)
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
                case WimLibProgressMsg.EXTRACT_FILE_STRUCTURE:
                    {
                        WimLibProgressInfo_Extract m = (WimLibProgressInfo_Extract)info;

                        if (0 < m.EndFileCount)
                        {
                            ulong percentComplete = (m.CurrentFileCount * 10 / m.EndFileCount);
                            s.MainViewModel.BuildCommandProgressValue = percentComplete;
                            s.MainViewModel.BuildCommandProgressText = $"[Stage 1] Creating files ({percentComplete}%)";
                        }
                    }
                    break;
                case WimLibProgressMsg.EXTRACT_STREAMS:
                    {
                        WimLibProgressInfo_Extract m = (WimLibProgressInfo_Extract)info;

                        if (0 < m.TotalBytes)
                        {
                            ulong percentComplete = 10 + (m.CompletedBytes * 80 / m.TotalBytes);
                            s.MainViewModel.BuildCommandProgressValue = percentComplete;
                            s.MainViewModel.BuildCommandProgressText = $"[Stage 2] Extracting file data ({percentComplete}%)";
                        }
                    }
                    break;
                case WimLibProgressMsg.EXTRACT_METADATA:
                    {
                        WimLibProgressInfo_Extract m = (WimLibProgressInfo_Extract)info;

                        if (0 < m.EndFileCount)
                        {
                            ulong percentComplete = 90 + (m.CurrentFileCount * 10 / m.EndFileCount);
                            s.MainViewModel.BuildCommandProgressValue = percentComplete;
                            s.MainViewModel.BuildCommandProgressText = $"[Stage 3] Applying metadata to files ({percentComplete}%)";
                        }
                    }
                    break;
            }
            return WimLibProgressStatus.CONTINUE;
        }
        #endregion

        #region WimExtract, WimExtractOp, WimExtractList
        public static List<LogInfo> WimExtract(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>(1);

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_WimExtract));
            CodeInfo_WimExtract info = cmd.Info as CodeInfo_WimExtract;

            string srcWim = StringEscaper.Preprocess(s, info.SrcWim);
            string imageIndexStr = StringEscaper.Preprocess(s, info.ImageIndex);
            string destDir = StringEscaper.Preprocess(s, info.DestDir);
            string extractPath = StringEscaper.Preprocess(s, info.ExtractPath);

            // Check SrcWim
            if (!File.Exists(srcWim))
            {
                logs.Add(new LogInfo(LogState.Error, $"File [{srcWim}] does not exist"));
                return logs;
            }

            // Check DestDir
            if (StringEscaper.PathSecurityCheck(destDir, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            if (!Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            // Set Flags
            WimLibOpenFlags openFlags = WimLibOpenFlags.DEFAULT;
            WimLibExtractFlags extractFlags = WimLibExtractFlags.NORPFIX |
                WimLibExtractFlags.GLOB_PATHS | WimLibExtractFlags.STRICT_GLOB |
                WimLibExtractFlags.NO_PRESERVE_DIR_STRUCTURE;
            if (info.CheckFlag)
                openFlags |= WimLibOpenFlags.CHECK_INTEGRITY;
            if (info.NoAclFlag)
                extractFlags |= WimLibExtractFlags.NO_ACLS;
            if (info.NoAttribFlag)
                extractFlags |= WimLibExtractFlags.NO_ATTRIBUTES;

            try
            {
                using (Wim wim = Wim.OpenWim(srcWim, openFlags, WimApplyExtractProgress, s))
                {
                    ManagedWimLib.WimInfo wimInfo = wim.GetWimInfo();

                    // Check imageIndex
                    if (!NumberHelper.ParseInt32(imageIndexStr, out int imageIndex))
                    {
                        logs.Add(new LogInfo(LogState.Error, $"[{imageIndexStr}] is not a valid a positive integer"));
                        return logs;
                    }
                    if (!(1 <= imageIndex && imageIndex <= wimInfo.ImageCount))
                    {
                        logs.Add(new LogInfo(LogState.Error, $"[{imageIndexStr}] must be [1] ~ [{wimInfo.ImageCount}]"));
                        return logs;
                    }

                    // Extract file(s)
                    s.MainViewModel.BuildCommandProgressTitle = "WimExtract Progress";
                    s.MainViewModel.BuildCommandProgressText = string.Empty;
                    s.MainViewModel.BuildCommandProgressMax = 100;
                    s.MainViewModel.BuildCommandProgressShow = true;

                    try
                    {
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

        public static List<LogInfo> WimExtractList(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>(1);

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_WimExtractList));
            CodeInfo_WimExtractList info = cmd.Info as CodeInfo_WimExtractList;

            string srcWim = StringEscaper.Preprocess(s, info.SrcWim);
            string imageIndexStr = StringEscaper.Preprocess(s, info.ImageIndex);
            string destDir = StringEscaper.Preprocess(s, info.DestDir);
            string listFilePath = StringEscaper.Preprocess(s, info.ListFile);

            // Check SrcWim
            if (!File.Exists(srcWim))
            {
                logs.Add(new LogInfo(LogState.Error, $"File [{srcWim}] does not exist"));
                return logs;
            }

            // Check DestDir
            if (StringEscaper.PathSecurityCheck(destDir, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            if (!Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            // Set Flags
            WimLibOpenFlags openFlags = WimLibOpenFlags.DEFAULT;
            WimLibExtractFlags extractFlags = WimLibExtractFlags.NORPFIX |
                WimLibExtractFlags.GLOB_PATHS | WimLibExtractFlags.STRICT_GLOB |
                WimLibExtractFlags.NO_PRESERVE_DIR_STRUCTURE;
            if (info.CheckFlag)
                openFlags |= WimLibOpenFlags.CHECK_INTEGRITY;
            if (info.NoAclFlag)
                extractFlags |= WimLibExtractFlags.NO_ACLS;
            if (info.NoAttribFlag)
                extractFlags |= WimLibExtractFlags.NO_ATTRIBUTES;

            // Check ListFile
            if (!File.Exists(listFilePath))
            {
                logs.Add(new LogInfo(LogState.Error, $"ListFile [{listFilePath}] does not exist"));
                return logs;
            }

            string unicodeListFile = Path.GetTempFileName();
            try
            {
                // Convert ListFile into UTF-16LE (wimlib only accepts UTF-8 or UTF-16LE)
                FileHelper.ConvertTextFileToEncoding(listFilePath, unicodeListFile, Encoding.Unicode);

                using (Wim wim = Wim.OpenWim(srcWim, openFlags, WimApplyExtractProgress, s))
                {
                    ManagedWimLib.WimInfo wimInfo = wim.GetWimInfo();

                    // Check imageIndex
                    if (!NumberHelper.ParseInt32(imageIndexStr, out int imageIndex))
                    {
                        logs.Add(new LogInfo(LogState.Error, $"[{imageIndexStr}] is not a valid a positive integer"));
                        return logs;
                    }
                    if (!(1 <= imageIndex && imageIndex <= wimInfo.ImageCount))
                    {
                        logs.Add(new LogInfo(LogState.Error, $"[{imageIndexStr}] must be [1] ~ [{wimInfo.ImageCount}]"));
                        return logs;
                    }

                    // Extract file(s)
                    s.MainViewModel.BuildCommandProgressTitle = "WimExtractList Progress";
                    s.MainViewModel.BuildCommandProgressText = string.Empty;
                    s.MainViewModel.BuildCommandProgressMax = 100;
                    s.MainViewModel.BuildCommandProgressShow = true;

                    try
                    {
                        wim.ExtractPathList(imageIndex, destDir, unicodeListFile, extractFlags);

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

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_WimCapture));
            CodeInfo_WimCapture info = cmd.Info as CodeInfo_WimCapture;

            string srcDir = StringEscaper.Preprocess(s, info.SrcDir);
            string destWim = StringEscaper.Preprocess(s, info.DestWim);
            string compStr = StringEscaper.Preprocess(s, info.Compress);

            // Check SrcDir
            if (!Directory.Exists(srcDir))
            {
                logs.Add(new LogInfo(LogState.Error, $"Directory [{srcDir}] does not exist"));
                return logs;
            }

            // Check DestWim
            if (StringEscaper.PathSecurityCheck(destWim, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            // Set Flags
            WimLibOpenFlags openFlags = WimLibOpenFlags.WRITE_ACCESS;
            WimLibWriteFlags writeFlags = WimLibWriteFlags.DEFAULT;
            WimLibAddFlags addFlags = WimLibAddFlags.WINCONFIG | WimLibAddFlags.FILE_PATHS_UNNEEDED;
            if (info.BootFlag)
                addFlags |= WimLibAddFlags.BOOT;
            if (info.NoAclFlag)
                addFlags |= WimLibAddFlags.NO_ACLS;
            if (info.CheckFlag)
            {
                openFlags |= WimLibOpenFlags.CHECK_INTEGRITY;
                writeFlags |= WimLibWriteFlags.CHECK_INTEGRITY;
            }

            // Set Compression Type
            WimLibCompressionType compType = WimLibCompressionType.NONE;
            if (compStr.Equals("NONE", StringComparison.OrdinalIgnoreCase))
                compType = WimLibCompressionType.NONE;
            else if (compStr.Equals("XPRESS", StringComparison.OrdinalIgnoreCase))
                compType = WimLibCompressionType.XPRESS;
            else if (compStr.Equals("LZX", StringComparison.OrdinalIgnoreCase))
                compType = WimLibCompressionType.LZX;
            else if (compStr.Equals("LZMS", StringComparison.OrdinalIgnoreCase))
            {
                writeFlags |= WimLibWriteFlags.SOLID;
                compType = WimLibCompressionType.LZMS;
            }
            else
            {
                logs.Add(new LogInfo(LogState.Error, $"Invalid Compression Type [{compStr}]"));
                return logs;
            }

            // Set ImageName
            string imageName;
            if (info.ImageName != null)
                imageName = StringEscaper.Preprocess(s, info.ImageName);
            else
            {
                imageName = Path.GetFileName(Path.GetFullPath(srcDir));
                if (string.IsNullOrWhiteSpace(imageName))
                {
                    logs.Add(new LogInfo(LogState.Error, $"Unable to set proper image name automatically"));
                    return logs;
                }
            }

            // Capture from disk
            try
            {
                using (Wim wim = Wim.CreateNewWim(compType))
                {
                    wim.RegisterCallback(WimCaptureAppendProgress, s);

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
                        wim.Write(destWim, WimLibNative.AllImages, writeFlags, (uint)Environment.ProcessorCount);

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

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_WimAppend));
            CodeInfo_WimAppend info = cmd.Info as CodeInfo_WimAppend;

            string srcDir = StringEscaper.Preprocess(s, info.SrcDir);
            string destWim = StringEscaper.Preprocess(s, info.DestWim);

            // Check SrcDir
            if (!Directory.Exists(srcDir))
            {
                logs.Add(new LogInfo(LogState.Error, $"Directory [{srcDir}] does not exist"));
                return logs;
            }

            // Check DestWim
            if (StringEscaper.PathSecurityCheck(destWim, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            // Set Flags
            WimLibOpenFlags openFlags = WimLibOpenFlags.WRITE_ACCESS;
            WimLibWriteFlags writeFlags = WimLibWriteFlags.DEFAULT;
            WimLibAddFlags addFlags = WimLibAddFlags.WINCONFIG | WimLibAddFlags.FILE_PATHS_UNNEEDED;
            if (info.BootFlag)
                addFlags |= WimLibAddFlags.BOOT;
            if (info.NoAclFlag)
                addFlags |= WimLibAddFlags.NO_ACLS;
            if (info.CheckFlag)
            {
                openFlags |= WimLibOpenFlags.CHECK_INTEGRITY;
                writeFlags |= WimLibWriteFlags.CHECK_INTEGRITY;
            }

            // Set ImageName
            string imageName;
            if (info.ImageName != null)
                imageName = StringEscaper.Preprocess(s, info.ImageName);
            else
            {
                imageName = Path.GetFileName(Path.GetFullPath(srcDir));
                if (string.IsNullOrWhiteSpace(imageName))
                {
                    logs.Add(new LogInfo(LogState.Error, $"Unable to set proper image name automatically"));
                    return logs;
                }
            }

            try
            {
                using (Wim wim = Wim.OpenWim(destWim, openFlags))
                {
                    wim.RegisterCallback(WimCaptureAppendProgress, s);

                    // Check if image name is duplicated
                    if (wim.IsImageNameInUse(imageName))
                    {
                        logs.Add(new LogInfo(LogState.Error, $"Image name [{imageName}] is already in use"));
                        return logs;
                    }

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
                        {
                            logs.Add(new LogInfo(LogState.Error, $"[{deltaIndexStr}] is not a valid a positive integer"));
                            return logs;
                        }
                        if (!(1 <= deltaIndex && deltaIndex <= imageCount))
                        {
                            logs.Add(new LogInfo(LogState.Error, $"[{deltaIndex}] must be [1] ~ [{imageCount}]"));
                            return logs;
                        }

                        wim.ReferenceTemplateImage((int)imageCount, deltaIndex);
                    }

                    // Appned to Wim
                    s.MainViewModel.BuildCommandProgressTitle = "WimAppend Progress";
                    s.MainViewModel.BuildCommandProgressText = string.Empty;
                    s.MainViewModel.BuildCommandProgressMax = 100;
                    s.MainViewModel.BuildCommandProgressShow = true;

                    try
                    {
                        wim.OverWrite(writeFlags, (uint)Environment.ProcessorCount);

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

        private static WimLibProgressStatus WimCaptureAppendProgress(WimLibProgressMsg msg, object info, object progctx)
        {
            EngineState s = progctx as EngineState;
            Debug.Assert(s != null);

            // SCAN_BEGIN
            // SCAN_DENTRY (Stage 1)
            // SCAN_END
            // WRITE_STREAMS (Stage 2)

            switch (msg)
            {
                case WimLibProgressMsg.SCAN_BEGIN:
                    {
                        WimLibProgressInfo_Scan m = (WimLibProgressInfo_Scan)info;

                        s.MainViewModel.BuildCommandProgressText = $"[Stage 1] Scanning {m.Source}...";
                    }
                    break;
                case WimLibProgressMsg.WRITE_STREAMS:
                    {
                        WimLibProgressInfo_WriteStreams m = (WimLibProgressInfo_WriteStreams)info;

                        if (0 < m.TotalBytes)
                        {
                            ulong percentComplete = m.CompletedBytes * 100 / m.TotalBytes;
                            s.MainViewModel.BuildCommandProgressValue = percentComplete;
                            s.MainViewModel.BuildCommandProgressText = $"[Stage 2] Archiving file data ({percentComplete}%)";
                        }
                    }
                    break;
            }
            return WimLibProgressStatus.CONTINUE;
        }

        private static LogInfo LogWimLibException(WimLibException e)
        {
            return new LogInfo(LogState.Error, $"[{e.ErrorCode}] {e.ErrorMsg}");
        }
        #endregion
    }
}
