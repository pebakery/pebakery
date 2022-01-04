/*
    Copyright (C) 2016-2022 Hajin Jang
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
using System.Linq;
using System.Text;

#nullable enable

namespace PEBakery.Core.Commands
{
    public static class CommandFile
    {
        public static List<LogInfo> FileCopy(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_FileCopy info = cmd.Info.Cast<CodeInfo_FileCopy>();

            string srcFile = StringEscaper.Preprocess(s, info.SrcFile);
            string destPath = StringEscaper.Preprocess(s, info.DestPath);
            Debug.Assert(srcFile != null, $"{nameof(srcFile)} != null");
            Debug.Assert(destPath != null, $"{nameof(destPath)} != null");

            // Path Security Check
            if (!StringEscaper.PathSecurityCheck(destPath, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            // Check srcFileName contains wildcard
            string wildcard = Path.GetFileName(srcFile);
            if (!StringHelper.IsWildcard(wildcard))
            { // No Wildcard
                if (Directory.Exists(destPath))
                { // DestPath exists, and it is directory
                    Directory.CreateDirectory(destPath);
                    string destFullPath = Path.Combine(destPath, Path.GetFileName(srcFile));
                    if (File.Exists(destFullPath))
                    {
                        if (info.Preserve)
                        {
                            logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Overwrite, $"[{destFullPath}] will not be overwritten"));
                            return logs;
                        }

                        logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Overwrite, $"File [{destFullPath}] will be overwritten"));
                    }

                    File.Copy(srcFile, destFullPath, true);
                    logs.Add(new LogInfo(LogState.Success, $"[{srcFile}] copied to [{destFullPath}]"));
                }
                else if (FileHelper.IsPathNonExistDir(destPath))
                { // DestPath ends with \, warn user
                    logs.Add(new LogInfo(LogState.Warning, $"Unable to copy a file, directory [{destPath}] does not exist"));
                }
                else
                { // DestPath does not exist, or it is a file
                    string destDir = FileHelper.GetDirNameEx(destPath);
                    Directory.CreateDirectory(destDir);
                    if (File.Exists(destPath))
                    {
                        if (info.Preserve)
                        {
                            logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Overwrite, $"[{destPath}] will not be overwritten"));
                            return logs;
                        }

                        logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Overwrite, $"File [{destPath}] will be overwritten"));
                    }

                    File.Copy(srcFile, destPath, true);
                    logs.Add(new LogInfo(LogState.Success, $"[{srcFile}] copied to [{destPath}]"));
                }
            }
            else
            { // With Wildcard
                // Use FileHelper.GetDirNameEx to prevent ArgumentException of Directory.GetFiles
                string srcDirToFind = Path.GetFullPath(FileHelper.GetDirNameEx(srcFile));
                string[] files = FileHelper.GetFilesEx(srcDirToFind, wildcard, info.NoRec ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories);

                if (files.Length == 0)
                {
                    logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Warning, $"No files were found in [{srcDirToFind}]"));
                    return logs;
                }

                if (!Directory.Exists(destPath) && File.Exists(destPath))
                {
                    logs.Add(new LogInfo(LogState.Success, $"Cannot copy [{srcFile}] into [{destPath}]"));
                    logs.Add(new LogInfo(LogState.Error, "The file destination must be directory when using wildcards (? *) in the <SrcFile> name"));
                    return logs;
                }

                // One or more file will be copied
                logs.Add(new LogInfo(LogState.Success, $"[{srcFile}] will be copied to [{destPath}]"));
                s.MainViewModel.SetBuildCommandProgress("FileCopy Progress", files.Length);
                try
                {
                    for (int i = 0; i < files.Length; i++)
                    {
                        string f = files[i];
                        string destFullPath = Path.Combine(destPath, FileHelper.SubRootDirPath(f, srcDirToFind));

                        if (File.Exists(destFullPath))
                        {
                            if (info.Preserve)
                            {
                                logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Overwrite, $"[{destFullPath}] will not be overwritten"));
                                continue;
                            }

                            logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Overwrite, $"[{destFullPath}] will be overwritten"));
                        }

                        string? destFullParent = Path.GetDirectoryName(destFullPath);
                        if (destFullParent is null)
                            throw new InternalException("Internal Logic Error at FileCopy");

                        s.MainViewModel.BuildCommandProgressText = $"{f}\r\n({(double)(i + 1) / files.Length * 100:0.0}%)";

                        Directory.CreateDirectory(destFullParent);
                        File.Copy(f, destFullPath, true);

                        s.MainViewModel.BuildCommandProgressValue = i + 1;

                        logs.Add(new LogInfo(LogState.Success, $"[{f}] copied to [{destFullPath}]"));
                    }
                }
                finally
                {
                    s.MainViewModel.ResetBuildCommandProgress();
                }

                logs.Add(new LogInfo(LogState.Success, $"[{files.Length}] files copied"));
            }

            return logs;
        }

        public static List<LogInfo> FileDelete(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_FileDelete info = cmd.Info.Cast<CodeInfo_FileDelete>();

            string filePath = StringEscaper.Preprocess(s, info.FilePath);
            Debug.Assert(filePath != null, $"{nameof(filePath)} != null");

            // Path Security Check
            if (!StringEscaper.PathSecurityCheck(filePath, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            // Check srcFileName contains wildcard
            string wildcard = Path.GetFileName(filePath);
            if (!StringHelper.IsWildcard(wildcard))
            { // No Wildcard
                if (File.Exists(filePath))
                { // Delete File
                    File.SetAttributes(filePath, FileAttributes.Normal);
                    File.Delete(filePath);

                    logs.Add(new LogInfo(LogState.Success, $"Deleted file [{filePath}]"));
                }
                else
                {
                    logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Warning, $"File [{filePath}] does not exist"));
                }
            }
            else
            { // With Wildcard
                // Use FileHelper.GetDirNameEx to prevent ArgumentException of Directory.GetFiles
                string srcDirToFind = FileHelper.GetDirNameEx(filePath);
                if (!Directory.Exists(srcDirToFind))
                    return LogInfo.LogErrorMessage(logs, $"Cannot find path [{srcDirToFind}]");

                string[] files = FileHelper.GetFilesEx(srcDirToFind, wildcard, info.NoRec ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories);

                if (files.Length == 0)
                {
                    logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Warning, $"Files matching wildcard [{filePath}] were not found"));
                    return logs;
                }

                s.MainViewModel.SetBuildCommandProgress("FileDelete Progress", files.Length);
                try
                {
                    for (int i = 0; i < files.Length; i++)
                    {
                        string f = files[i];
                        s.MainViewModel.BuildCommandProgressText = $"{f}\r\n({(double)(i + 1) / files.Length * 100:0.0}%)";

                        // Prevent exception from readonly attribute
                        File.SetAttributes(f, FileAttributes.Normal);
                        File.Delete(f);

                        s.MainViewModel.BuildCommandProgressValue = i + 1;

                        logs.Add(new LogInfo(LogState.Success, $"File [{f}] deleted"));
                    }

                    logs.Add(new LogInfo(LogState.Success, $"[{files.Length}] files deleted"));
                }
                finally
                {
                    s.MainViewModel.ResetBuildCommandProgress();
                }
            }

            return logs;
        }

        public static List<LogInfo> FileRename(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_FileRename info = cmd.Info.Cast<CodeInfo_FileRename>();

            string srcPath = StringEscaper.Preprocess(s, info.SrcPath);
            string destPath = StringEscaper.Preprocess(s, info.DestPath);

            // Path Security Check
            if (!StringEscaper.PathSecurityCheck(destPath, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            if (!File.Exists(srcPath))
            {
                // Check if srcPath is directory
                if (!Directory.Exists(srcPath))
                    return LogInfo.LogErrorMessage(logs, $"File [{srcPath}] does not exist");

                if (!s.CompatFileRenameCanMoveDir)
                    return LogInfo.LogErrorMessage(logs, $"[{srcPath}] is a directory, not a file");

                if (Directory.Exists(destPath))
                {
                    string destFullPath = Path.Combine(destPath, Path.GetFileName(srcPath));

                    Directory.Move(srcPath, destFullPath);
                    logs.Add(new LogInfo(LogState.Success, $"Directory [{srcPath}] moved to [{destFullPath}]"));
                    return logs;
                }

                Directory.Move(srcPath, destPath);
                logs.Add(new LogInfo(LogState.Success, $"Directory [{srcPath}] moved to [{destPath}]"));
                return logs;
            }

            File.SetAttributes(srcPath, FileAttributes.Normal);
            File.Move(srcPath, destPath);

            if (cmd.Type == CodeType.FileRename)
                logs.Add(new LogInfo(LogState.Success, $"File [{srcPath}] renamed to [{destPath}]"));
            else
                logs.Add(new LogInfo(LogState.Success, $"File [{srcPath}] moved to [{destPath}]"));

            return logs;
        }

        public static List<LogInfo> FileCreateBlank(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_FileCreateBlank info = cmd.Info.Cast<CodeInfo_FileCreateBlank>();

            string filePath = StringEscaper.Preprocess(s, info.FilePath);

            Encoding encoding = EncodingHelper.DefaultAnsi;
            if (info.Encoding != null)
            {
                string encodingStr = StringEscaper.Preprocess(s, info.Encoding);
                if (StringEscaper.ParseEncoding(encodingStr) is Encoding enc)
                    encoding = enc;
                else
                    return LogInfo.LogErrorMessage(logs, $"Encoding [{encodingStr}] is invalid");
            }

            if (File.Exists(filePath))
            {
                if (info.Preserve)
                {
                    logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Overwrite, $"[{filePath}] will not be overwritten", cmd));
                    return logs;
                }

                logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Overwrite, $"[{filePath}] will be overwritten", cmd));
            }

            if (!StringEscaper.PathSecurityCheck(filePath, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            Directory.CreateDirectory(FileHelper.GetDirNameEx(filePath));
            EncodingHelper.WriteTextBom(filePath, encoding);
            logs.Add(new LogInfo(LogState.Success, $"Created blank text file [{filePath}]", cmd));

            return logs;
        }

        public static List<LogInfo> FileSize(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_FileSize info = cmd.Info.Cast<CodeInfo_FileSize>();

            string filePath = StringEscaper.Preprocess(s, info.FilePath);

            if (!File.Exists(filePath))
                return LogInfo.LogErrorMessage(logs, $"File [{filePath}] does not exist");

            FileInfo fileInfo = new FileInfo(filePath);

            logs.Add(new LogInfo(LogState.Success, $"File [{filePath}] is [{fileInfo.Length}B]", cmd));

            List<LogInfo> varLogs = Variables.SetVariable(s, info.DestVar, fileInfo.Length.ToString());
            logs.AddRange(varLogs);

            return logs;
        }

        public static List<LogInfo> FileVersion(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_FileVersion info = cmd.Info.Cast<CodeInfo_FileVersion>();

            string filePath = StringEscaper.Preprocess(s, info.FilePath);
            FileVersionInfo v = FileVersionInfo.GetVersionInfo(filePath);

            string verStr = $"{v.FileMajorPart}.{v.FileMinorPart}.{v.FileBuildPart}.{v.FilePrivatePart}";
            logs.Add(new LogInfo(LogState.Success, $"File [{filePath}]'s version is [{verStr}]", cmd));

            List<LogInfo> varLogs = Variables.SetVariable(s, info.DestVar, verStr);
            logs.AddRange(varLogs);

            return logs;
        }

        public static List<LogInfo> DirCopy(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_DirCopy info = cmd.Info.Cast<CodeInfo_DirCopy>();

            string srcDir = StringEscaper.Preprocess(s, info.SrcDir);
            string destDir = StringEscaper.Preprocess(s, info.DestDir);
            Debug.Assert(srcDir != null, $"{nameof(srcDir)} != null");
            Debug.Assert(destDir != null, $"{nameof(destDir)} != null");

            // Path Security Check
            if (!StringEscaper.PathSecurityCheck(destDir, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            // DestPath must be directory 
            if (File.Exists(destDir))
                return LogInfo.LogErrorMessage(logs, $"Cannot overwrite file [{destDir}] with directory [{srcDir}]");

            // Prepare progress report
            int progressCount = 0;
            object progressLock = new object();
            IProgress<string> progress = new Progress<string>(f =>
            {
                lock (progressLock)
                {
                    progressCount += 1;
                    double percent;
                    if (NumberHelper.DoubleEquals(s.MainViewModel.BuildCommandProgressMax, 0))
                        percent = 0;
                    else
                        percent = progressCount / s.MainViewModel.BuildCommandProgressMax * 100;
                    s.MainViewModel.BuildCommandProgressText = $"{f}\r\n({percent:0.0}%)";
                    s.MainViewModel.BuildCommandProgressValue = progressCount;
                }
            });

            // Check if srcDir contains wildcard
            string wildcard = Path.GetFileName(srcDir);
            if (!StringHelper.IsWildcard(wildcard))
            { // No Wildcard
                string destFullPath = Path.Combine(destDir, Path.GetFileName(srcDir));
                if (Directory.Exists(destFullPath))
                    logs.Add(new LogInfo(LogState.Overwrite, $"Directory [{destFullPath}] will be overwritten with [{srcDir}]"));
                else
                    Directory.CreateDirectory(destFullPath);

                // Get total file count
                int filesCount = FileHelper.GetFilesEx(srcDir, "*", SearchOption.AllDirectories).Length;

                // Copy directory
                s.MainViewModel.SetBuildCommandProgress("DirCopy Progress", filesCount);
                try
                {
                    FileHelper.DirCopy(srcDir, destFullPath, new DirCopyOptions
                    {
                        CopySubDirs = true,
                        Overwrite = true,
                        Progress = progress,
                    });
                    logs.Add(new LogInfo(LogState.Success, $"Directory [{srcDir}] copied to [{destFullPath}]", cmd));
                }
                finally
                {
                    s.MainViewModel.ResetBuildCommandProgress();
                }
            }
            else
            { // With Wildcard
                if (Directory.Exists(destDir))
                    logs.Add(new LogInfo(LogState.Overwrite, $"Directory [{destDir}] will be overwritten with [{srcDir}]"));
                else
                    Directory.CreateDirectory(destDir);

                string? srcParentDir = Path.GetDirectoryName(srcDir);
                if (srcParentDir == null)
                    throw new InternalException("Internal Logic Error at DirCopy");
                DirectoryInfo dirInfo = new DirectoryInfo(srcParentDir);
                if (!dirInfo.Exists)
                    throw new DirectoryNotFoundException($"Source directory does not exist or cannot be found: {srcDir}");

                // Get total file count
                int filesCount = 0;
                FileInfo[]? compatFiles = null;
                if (s.CompatDirCopyBug)
                {
                    compatFiles = dirInfo.GetFiles(wildcard);
                    filesCount += compatFiles.Length;
                }

                DirectoryInfo[] subDirs = dirInfo.GetDirectories(wildcard);
                foreach (DirectoryInfo d in subDirs)
                {
                    string[] files = FileHelper.GetFilesEx(d.FullName, "*", SearchOption.AllDirectories);
                    filesCount += files.Length;
                }

                // Copy directory
                s.MainViewModel.SetBuildCommandProgress("DirCopy Progress", filesCount);
                try
                {
                    if (s.CompatDirCopyBug && compatFiles != null)
                    { // Simulate WB082's [DirCopy,%SrcDir%\*,%DestDir%] FileCopy _bug_
                        foreach (FileInfo f in compatFiles)
                        {
                            progress.Report(f.FullName);
                            File.Copy(f.FullName, Path.Combine(destDir, f.Name), true);
                        }
                    }

                    // Copy first sub-level directory with wildcard
                    // Note wildcard will not be applied to sub-directory copy
                    foreach (DirectoryInfo d in subDirs)
                    {
                        FileHelper.DirCopy(d.FullName, Path.Combine(destDir, d.Name), new DirCopyOptions
                        {
                            CopySubDirs = true,
                            Overwrite = true,
                            Progress = progress,
                        });
                    }
                }
                finally
                {
                    s.MainViewModel.ResetBuildCommandProgress();
                }

                logs.Add(new LogInfo(LogState.Success, $"Directory [{srcDir}] copied to [{destDir}]", cmd));
            }

            return logs;
        }

        public static List<LogInfo> DirDelete(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_DirDelete info = cmd.Info.Cast<CodeInfo_DirDelete>();

            string dirPath = StringEscaper.Preprocess(s, info.DirPath);

            // Path Security Check
            if (!StringEscaper.PathSecurityCheck(dirPath, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            // Delete Directory
            FileHelper.DirDeleteEx(dirPath);

            logs.Add(new LogInfo(LogState.Success, $"Deleted directory [{dirPath}]"));

            return logs;
        }

        public static List<LogInfo> DirMove(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_DirMove info = cmd.Info.Cast<CodeInfo_DirMove>();

            string srcDir = StringEscaper.Preprocess(s, info.SrcDir);
            string destPath = StringEscaper.Preprocess(s, info.DestPath);

            // Path Security Check
            if (!StringEscaper.PathSecurityCheck(destPath, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            // SrcPath must be directory 
            // WB082 does not check this, so file can be moved with DirMove
            if (File.Exists(srcDir))
                return LogInfo.LogErrorMessage(logs, $"[{srcDir}] is a file, not a directory");

            // DestPath must be directory 
            if (File.Exists(destPath))
                return LogInfo.LogErrorMessage(logs, $"[{destPath}] is a file, not a directory");

            if (Directory.Exists(destPath))
            {
                string srcDirName = Path.GetFileName(srcDir);
                if (srcDirName == null)
                    throw new InternalException("Internal Logic Error at DirMove");
                string destFullPath = Path.Combine(destPath, srcDirName);

                Directory.Move(srcDir, destFullPath);

                logs.Add(new LogInfo(LogState.Success, $"Directory [{srcDir}] moved to [{destFullPath}]"));
            }
            else
            {
                Directory.Move(srcDir, destPath);

                logs.Add(new LogInfo(LogState.Success, $"Directory [{srcDir}] moved to [{destPath}]"));
            }

            return logs;
        }

        public static List<LogInfo> DirMake(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_DirMake info = cmd.Info.Cast<CodeInfo_DirMake>();

            string destDir = StringEscaper.Preprocess(s, info.DestDir);

            // Path Security Check
            if (!StringEscaper.PathSecurityCheck(destDir, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            // DestPath cannot be file
            if (File.Exists(destDir))
                return LogInfo.LogErrorMessage(logs, $"File [{destDir}] already exists");

            if (Directory.Exists(destDir))
            {
                logs.Add(new LogInfo(LogState.Ignore, $"Directory [{destDir}] already exists"));
            }
            else
            {
                Directory.CreateDirectory(destDir);
                logs.Add(new LogInfo(LogState.Success, $"Created Directory [{destDir}]"));
            }

            return logs;
        }

        public static List<LogInfo> DirSize(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_DirSize info = cmd.Info.Cast<CodeInfo_DirSize>();

            string dirPath = StringEscaper.Preprocess(s, info.DirPath);

            if (!Directory.Exists(dirPath))
                return LogInfo.LogErrorMessage(logs, $"Directory [{dirPath}] does not exist");

            string[] files = FileHelper.GetFilesEx(dirPath, "*", SearchOption.AllDirectories);
            long dirSize = files.Sum(f => new FileInfo(f).Length);

            logs.Add(new LogInfo(LogState.Success, $"Directory [{dirPath}] is [{dirSize}B]", cmd));

            List<LogInfo> varLogs = Variables.SetVariable(s, info.DestVar, dirSize.ToString());
            logs.AddRange(varLogs);

            return logs;
        }

        public static List<LogInfo> PathMove(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_PathMove info = cmd.Info.Cast<CodeInfo_PathMove>();

            string srcPath = StringEscaper.Preprocess(s, info.SrcPath);
            string destPath = StringEscaper.Preprocess(s, info.DestPath);

            // Path Security Check
            if (!StringEscaper.PathSecurityCheck(destPath, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            // SrcPath must be directory
            if (File.Exists(srcPath))
            {
                File.SetAttributes(srcPath, FileAttributes.Normal);
                File.Move(srcPath, destPath);
                logs.Add(new LogInfo(LogState.Success, $"File [{srcPath}] moved to [{destPath}]"));
            }
            else if (Directory.Exists(srcPath))
            {
                // DestPath must be directory 
                if (File.Exists(destPath))
                    return LogInfo.LogErrorMessage(logs, $"[{destPath}] is a file, not a directory");

                if (Directory.Exists(destPath))
                {
                    string destFullPath = Path.Combine(destPath, Path.GetFileName(srcPath));
                    Directory.Move(srcPath, destFullPath);
                    logs.Add(new LogInfo(LogState.Success, $"Directory [{srcPath}] moved to [{destFullPath}]"));
                }
                else
                {
                    Directory.Move(srcPath, destPath);
                    logs.Add(new LogInfo(LogState.Success, $"Directory [{srcPath}] moved to [{destPath}]"));
                }
            }
            else
            {
                logs.Add(new LogInfo(LogState.Success, $"Path [{srcPath}] does not exist"));
            }

            return logs;
        }
    }
}
