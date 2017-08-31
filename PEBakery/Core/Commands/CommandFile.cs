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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PEBakery.Core.Commands
{
    public static class CommandFile
    {
        public static List<LogInfo> FileCopy(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_FileCopy));
            CodeInfo_FileCopy info = cmd.Info as CodeInfo_FileCopy;

            string srcFile = StringEscaper.Preprocess(s, info.SrcFile);
            string destPath = StringEscaper.Preprocess(s, info.DestPath);

            // Path Security Check
            if (StringEscaper.PathSecurityCheck(destPath, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            s.MainViewModel.BuildCommandProgressBarValue = 500;

            // Check destPath is directory
            bool destPathExists = false;
            bool destPathIsDir = false;
            if (Directory.Exists(destPath))
            {
                destPathExists = true;
                destPathIsDir = true;
            }
            else if (File.Exists(destPath))
            {
                destPathExists = true;
            }

            // Check srcFileName contains wildcard
            if (srcFile.IndexOfAny(new char[] { '*', '?' }) == -1)
            { // No Wildcard
                if (destPathIsDir) // DestPath exists, and it is directory
                {
                    Directory.CreateDirectory(destPath);
                    string destFullPath = Path.Combine(destPath, Path.GetFileName(srcFile));
                    if (File.Exists(destFullPath))
                    {
                        if (info.Preserve)
                        {
                            logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Warning, $"Cannot overwrite file [{destFullPath}]", cmd));
                            return logs;
                        }
                        else
                        {
                            logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Warning, $"File [{destFullPath}] will be overwritten", cmd));
                        }
                    }

                    File.Copy(srcFile, destFullPath, true);
                    logs.Add(new LogInfo(LogState.Success, $"[{srcFile}] copied to [{destFullPath}]", cmd));
                }
                else // DestPath not exist, or it is file
                {
                    Directory.CreateDirectory(FileHelper.GetDirNameEx(destPath));
                    if (destPathExists)
                    {
                        if (info.Preserve)
                        {
                            logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Warning, $"Cannot overwrite file [{destPath}]", cmd));
                            return logs;
                        }
                        else
                        {
                            logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Warning, $"File [{destPath}] will be overwritten", cmd));
                        }
                    }

                    File.Copy(srcFile, destPath, true);
                    logs.Add(new LogInfo(LogState.Success, $"[{srcFile}] copied to [{destPath}]", cmd));
                }
            }
            else
            { // With Wildcard
                // Use FileHelper.GetDirNameEx to prevent ArgumentException of Directory.GetFiles
                string srcDirToFind = FileHelper.GetDirNameEx(srcFile);

                string[] files;
                if (info.NoRec)
                    files = Directory.GetFiles(srcDirToFind, Path.GetFileName(srcFile));
                else
                    files = Directory.GetFiles(srcDirToFind, Path.GetFileName(srcFile), SearchOption.AllDirectories);

                if (0 < files.Length)
                { // One or more file will be copied
                    logs.Add(new LogInfo(LogState.Success, $"[{srcFile}] will be copied to [{destPath}]", cmd));

                    if (destPathIsDir || !destPathExists)
                    {
                        if (destPathExists == false)
                            Directory.CreateDirectory(destPath);

                        foreach (string f in files)
                        {
                            string destFullPath = Path.Combine(destPath, Path.GetFileName(f));
                            if (File.Exists(destFullPath))
                            {
                                if (info.Preserve)
                                {
                                    logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Warning, $"Cannot overwrite [{destFullPath}]", cmd));
                                    continue;
                                }
                                else
                                {
                                    logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Warning, $"[{destFullPath}] will be overwritten", cmd));
                                }
                            }

                            File.Copy(f, destFullPath, true);
                            logs.Add(new LogInfo(LogState.Success, $"[{f}] copied to [{destFullPath}]", cmd));
                        }

                        logs.Add(new LogInfo(LogState.Success, $"[{files.Length}] files copied", cmd));
                    }
                    else
                    {
                        logs.Add(new LogInfo(LogState.Error, "<DestPath> must be directory when using wildcard in <SrcFile>", cmd));
                        return logs;
                    }
                }
                else
                { // No file will be copied
                    logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Warning, $"Files match wildcard [{srcFile}] not found", cmd));
                }
            }

            return logs;
        }

        public static List<LogInfo> FileDelete(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_FileDelete));
            CodeInfo_FileDelete info = cmd.Info as CodeInfo_FileDelete;

            string filePath = StringEscaper.Preprocess(s, info.FilePath);

            // Path Security Check
            if (StringEscaper.PathSecurityCheck(filePath, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            s.MainViewModel.BuildCommandProgressBarValue = 500;

            // Check srcFileName contains wildcard
            if (filePath.IndexOfAny(new char[] { '*', '?' }) == -1)
            { // No Wildcard
                if (File.Exists(filePath))
                { // Delete File
                    File.Delete(filePath);

                    logs.Add(new LogInfo(LogState.Success, $"Deleted file [{filePath}]"));
                }
                else
                {
                    logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Warning, $"File [{filePath}] not exists"));
                }

            }
            else
            { // With Wildcard
                // Use FileHelper.GetDirNameEx to prevent ArgumentException of Directory.GetFiles
                string srcDirToFind = FileHelper.GetDirNameEx(filePath);

                string[] files;
                if (info.NoRec)
                    files = Directory.GetFiles(srcDirToFind, Path.GetFileName(filePath));
                else
                    files = Directory.GetFiles(srcDirToFind, Path.GetFileName(filePath), SearchOption.AllDirectories);

                if (0 < files.Length)
                { // One or more file will be deleted
                    foreach(string f in files)
                    {
                        File.Delete(f);
                        logs.Add(new LogInfo(LogState.Success, $"File [{f}] deleted"));
                    }

                    logs.Add(new LogInfo(LogState.Success, $"[{files.Length}] files copied"));
                }
                else
                { // No file will be deleted
                    logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Warning, $"Files match wildcard [{filePath}] not found"));
                }
            }

            return logs;
        }

        public static List<LogInfo> FileRename(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_FileRename));
            CodeInfo_FileRename info = cmd.Info as CodeInfo_FileRename;

            string srcPath = StringEscaper.Preprocess(s, info.SrcPath);
            string destPath = StringEscaper.Preprocess(s, info.DestPath);

            // Path Security Check
            if (StringEscaper.PathSecurityCheck(destPath, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            s.MainViewModel.BuildCommandProgressBarValue = 500;

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

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_FileCreateBlank));
            CodeInfo_FileCreateBlank info = cmd.Info as CodeInfo_FileCreateBlank;

            string filePath = StringEscaper.Preprocess(s, info.FilePath);

            // Default Encoding - UTF8
            Encoding encoding = Encoding.UTF8;
            if (info.Encoding != null)
                encoding = info.Encoding;

            if (File.Exists(filePath))
            {
                if (info.Preserve)
                {
                    logs.Add(new LogInfo(LogState.Success, $"Cannot overwrite [{filePath}]", cmd));
                    return logs;
                }
                else
                {
                    LogState state = LogState.Warning;
                    if (info.NoWarn)
                        state = LogState.Ignore;
                    logs.Add(new LogInfo(state, $"[{filePath}] will be overwritten", cmd));
                }
            }

            s.MainViewModel.BuildCommandProgressBarValue = 500;

            if (StringEscaper.PathSecurityCheck(filePath, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            Directory.CreateDirectory(FileHelper.GetDirNameEx(filePath));
            FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            FileHelper.WriteTextBOM(fs, encoding).Close();
            logs.Add(new LogInfo(LogState.Success, $"Created blank text file [{filePath}]", cmd));

            return logs;
        }

        public static List<LogInfo> FileSize(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_FileSize));
            CodeInfo_FileSize info = cmd.Info as CodeInfo_FileSize;

            s.MainViewModel.BuildCommandProgressBarValue = 300;

            string filePath = StringEscaper.Preprocess(s, info.FilePath);
            FileInfo fileInfo = new FileInfo(filePath);

            s.MainViewModel.BuildCommandProgressBarValue = 700;

            logs.Add(new LogInfo(LogState.Success, $"File [{filePath}] is [{fileInfo.Length}B]", cmd));

            List<LogInfo> varLogs = Variables.SetVariable(s, info.DestVar, fileInfo.Length.ToString());
            logs.AddRange(varLogs);

            return logs;
        }

        public static List<LogInfo> FileVersion(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_FileVersion));
            CodeInfo_FileVersion info = cmd.Info as CodeInfo_FileVersion;

            s.MainViewModel.BuildCommandProgressBarValue = 300;

            string filePath = StringEscaper.Preprocess(s, info.FilePath);
            FileVersionInfo v = FileVersionInfo.GetVersionInfo(filePath);

            s.MainViewModel.BuildCommandProgressBarValue = 700;

            string verStr = $"{v.FileMajorPart}.{v.FileMinorPart}.{v.FileBuildPart}.{v.FilePrivatePart}";
            logs.Add(new LogInfo(LogState.Success, $"File [{filePath}]'s version is [{verStr}]", cmd));

            List<LogInfo> varLogs = Variables.SetVariable(s, info.DestVar, verStr); 
            logs.AddRange(varLogs);

            return logs;
        }

        public static List<LogInfo> DirCopy(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_DirCopy));
            CodeInfo_DirCopy info = cmd.Info as CodeInfo_DirCopy;

            string srcDir = StringEscaper.Preprocess(s, info.SrcDir);
            string destPath = StringEscaper.Preprocess(s, info.DestPath);

            // Path Security Check
            if (StringEscaper.PathSecurityCheck(destPath, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            s.MainViewModel.BuildCommandProgressBarValue = 300;

            // DestPath must be directory 
            if (File.Exists(destPath))
            {
                logs.Add(new LogInfo(LogState.Error, $"Cannot overwrite file [{destPath}] with directory [{srcDir}]"));
                return logs;
            }

            if (Directory.Exists(destPath))
                logs.Add(new LogInfo(LogState.Ignore, $"Directory [{destPath}] will be overwritten with [{srcDir}]"));
            else
                Directory.CreateDirectory(destPath);

            s.MainViewModel.BuildCommandProgressBarValue = 700;

            // Check srcDir contains wildcard
            if (srcDir.IndexOfAny(new char[] { '*', '?' }) == -1)
            { // No Wildcard
                FileHelper.DirectoryCopy(srcDir, destPath, true, null);
                logs.Add(new LogInfo(LogState.Success, $"Directory [{srcDir}] copied to [{destPath}]", cmd));
            }
            else
            { // With Wildcard
                string wildcard = Path.GetFileName(srcDir);
                FileHelper.DirectoryCopy(srcDir, destPath, true, wildcard);
                logs.Add(new LogInfo(LogState.Success, $"Directory [{srcDir}] copied to [{destPath}]", cmd));
            }

            return logs;
        }

        public static List<LogInfo> DirDelete(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_DirDelete));
            CodeInfo_DirDelete info = cmd.Info as CodeInfo_DirDelete;

            string dirPath = StringEscaper.Preprocess(s, info.DirPath);

            // Path Security Check
            if (StringEscaper.PathSecurityCheck(dirPath, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            s.MainViewModel.BuildCommandProgressBarValue = 500;

            // Delete Directory
            Directory.Delete(dirPath, true);

            logs.Add(new LogInfo(LogState.Success, $"Deleted directory [{dirPath}]"));

            return logs;
        }

        public static List<LogInfo> DirMove(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_DirMove));
            CodeInfo_DirMove info = cmd.Info as CodeInfo_DirMove;

            string srcDir = StringEscaper.Preprocess(s, info.SrcDir);
            string destPath = StringEscaper.Preprocess(s, info.DestPath);

            // Path Security Check
            if (StringEscaper.PathSecurityCheck(destPath, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            s.MainViewModel.BuildCommandProgressBarValue = 500;

            // DestPath must be directory 
            if (File.Exists(destPath))
            {
                logs.Add(new LogInfo(LogState.Error, $"[{destPath}] is a file, not a directory"));
                return logs;
            }

            if (Directory.Exists(destPath))
            { // Cannot use Directory.Move, should copy and delete directory.
                logs.Add(new LogInfo(LogState.Ignore, $"Directory [{destPath}] will be overwritten with [{srcDir}]"));

                FileHelper.DirectoryCopy(srcDir, destPath, true, null);
                Directory.Delete(srcDir, true);

                logs.Add(new LogInfo(LogState.Success, $"Directory [{srcDir}] moved to [{destPath}]"));
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

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_DirMake));
            CodeInfo_DirMake info = cmd.Info as CodeInfo_DirMake;

            string destDir = StringEscaper.Preprocess(s, info.DestDir);

            s.MainViewModel.BuildCommandProgressBarValue = 500;

            // Path Security Check
            if (StringEscaper.PathSecurityCheck(destDir, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            Directory.CreateDirectory(destDir);
            logs.Add(new LogInfo(LogState.Success, $"Created Directory [{destDir}]", cmd));

            return logs;
        }

        public static List<LogInfo> DirSize(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_DirSize));
            CodeInfo_DirSize info = cmd.Info as CodeInfo_DirSize;

            s.MainViewModel.BuildCommandProgressBarValue = 200;

            string path = StringEscaper.Preprocess(s, info.Path);

            string[] files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
            long dirSize = 0;
            for (int i = 0; i < files.Length; i++)
            {
                FileInfo fileInfo = new FileInfo(files[i]);
                dirSize += fileInfo.Length;

                s.MainViewModel.BuildCommandProgressBarValue = 200 + (800 * (i + 1) / files.Length);
            }

            logs.Add(new LogInfo(LogState.Success, $"Directory [{path}] is [{dirSize}B]", cmd));

            List<LogInfo> varLogs = Variables.SetVariable(s, info.DestVar, dirSize.ToString());
            logs.AddRange(varLogs);

            return logs;
        }
    }
}
