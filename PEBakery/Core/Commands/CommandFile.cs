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
            string wildcard = Path.GetFileName(srcFile);
            if (wildcard.IndexOfAny(new char[] { '*', '?' }) == -1)
            { // No Wildcard
                if (destPathIsDir) // DestPath exists, and it is directory
                {
                    Directory.CreateDirectory(destPath);
                    string destFullPath = Path.Combine(destPath, Path.GetFileName(srcFile));
                    if (File.Exists(destFullPath))
                    {
                        if (info.Preserve)
                        {
                            logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Overwrite, $"Cannot overwrite file [{destFullPath}]", cmd));
                            return logs;
                        }
                        else
                        {
                            logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Overwrite, $"File [{destFullPath}] will be overwritten", cmd));
                        }
                    }

                    File.Copy(srcFile, destFullPath, true);
                    logs.Add(new LogInfo(LogState.Success, $"[{srcFile}] copied to [{destFullPath}]", cmd));
                }
                else // DestPath not exist, or it is a file
                {
                    Directory.CreateDirectory(FileHelper.GetDirNameEx(destPath));
                    if (destPathExists)
                    {
                        if (info.Preserve)
                        {
                            logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Overwrite, $"Cannot overwrite file [{destPath}]", cmd));
                            return logs;
                        }
                        else
                        {
                            logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Overwrite, $"File [{destPath}] will be overwritten", cmd));
                        }
                    }

                    File.Copy(srcFile, destPath, true);
                    logs.Add(new LogInfo(LogState.Success, $"[{srcFile}] copied to [{destPath}]", cmd));
                }
            }
            else
            { // With Wildcard
                // Use FileHelper.GetDirNameEx to prevent ArgumentException of Directory.GetFiles
                string srcDirToFind = Path.GetFullPath(FileHelper.GetDirNameEx(srcFile));

                string[] files;
                if (info.NoRec)
                    files = FileHelper.GetFilesEx(srcDirToFind, wildcard);
                else
                    files = FileHelper.GetFilesEx(srcDirToFind, wildcard, SearchOption.AllDirectories);

                if (0 < files.Length)
                { // One or more file will be copied
                    logs.Add(new LogInfo(LogState.Success, $"[{srcFile}] will be copied to [{destPath}]", cmd));

                    if (destPathIsDir || !destPathExists)
                    {
                        for (int i = 0; i < files.Length; i++)
                        {
                            string f = files[i];
                            string destFullPath = Path.Combine(destPath, f.Substring(srcDirToFind.Length + 1));
                            
                            if (File.Exists(destFullPath))
                            {
                                if (info.Preserve)
                                {
                                    logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Overwrite, $"Cannot overwrite [{destFullPath}]", cmd));
                                    continue;
                                }
                                else
                                {
                                    logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Overwrite, $"[{destFullPath}] will be overwritten", cmd));
                                }
                            }

                            Directory.CreateDirectory(Path.GetDirectoryName(destFullPath));
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
                    logs.Add(new LogInfo(LogState.Ignore, $"Files matching wildcard [{srcFile}] not found", cmd));
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

            // Check srcFileName contains wildcard
            string wildcard = Path.GetFileName(filePath);
            if (wildcard.IndexOfAny(new char[] { '*', '?' }) == -1)
            { // No Wildcard
                if (File.Exists(filePath))
                { // Delete File
                    File.SetAttributes(filePath, FileAttributes.Normal);
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
                if (Directory.Exists(srcDirToFind) == false)
                {
                    logs.Add(new LogInfo(LogState.Error, $"Cannot find path [{srcDirToFind}]"));
                    return logs;
                }
                
                string[] files;
                if (info.NoRec)
                    files = FileHelper.GetFilesEx(srcDirToFind, wildcard);
                else
                    files = FileHelper.GetFilesEx(srcDirToFind, wildcard, SearchOption.AllDirectories);

                if (0 < files.Length)
                { // One or more file will be deleted
                    foreach (string f in files)
                    {
                        File.SetAttributes(f, FileAttributes.Normal);
                        File.Delete(f);
                        logs.Add(new LogInfo(LogState.Success, $"File [{f}] deleted"));
                    }

                    logs.Add(new LogInfo(LogState.Success, $"[{files.Length}] files deleted"));
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

            if (File.Exists(srcPath) == false)
            {
                // Check if srcPath is directory
                if (Directory.Exists(srcPath))
                {
                    if (s.CompatFileRenameCanMoveDir)
                    {
                        if (Directory.Exists(destPath))
                        {
                            string destFullPath = Path.Combine(destPath, Path.GetFileName(srcPath));

                            Directory.Move(srcPath, destFullPath);
                            logs.Add(new LogInfo(LogState.Success, $"Directory [{srcPath}] moved to [{destFullPath}]"));
                            return logs;
                        }
                        else
                        {
                            Directory.Move(srcPath, destPath);
                            logs.Add(new LogInfo(LogState.Success, $"Directory [{srcPath}] moved to [{destPath}]"));
                            return logs;
                        }
                    }
                    else
                    {
                        logs.Add(new LogInfo(LogState.Error, $"[{srcPath}] is a directory, not a file"));
                        return logs;
                    }
                }
                else
                {
                    logs.Add(new LogInfo(LogState.Error, $"File [{srcPath}] does not exist"));
                    return logs;
                }
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

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_FileCreateBlank));
            CodeInfo_FileCreateBlank info = cmd.Info as CodeInfo_FileCreateBlank;

            string filePath = StringEscaper.Preprocess(s, info.FilePath);

            Encoding encoding = Encoding.Default;
            if (info.Encoding != null)
                encoding = info.Encoding;

            if (File.Exists(filePath))
            {
                if (info.Preserve)
                {
                    logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Warning, $"Cannot overwrite [{filePath}]", cmd));
                    return logs;
                }
                else
                {
                    logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Warning, $"[{filePath}] will be overwritten", cmd));
                }
            }

            if (StringEscaper.PathSecurityCheck(filePath, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            Directory.CreateDirectory(FileHelper.GetDirNameEx(filePath));
            FileHelper.WriteTextBOM(filePath, encoding);
            logs.Add(new LogInfo(LogState.Success, $"Created blank text file [{filePath}]", cmd));

            return logs;
        }

        public static List<LogInfo> FileSize(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_FileSize));
            CodeInfo_FileSize info = cmd.Info as CodeInfo_FileSize;

            string filePath = StringEscaper.Preprocess(s, info.FilePath);

            if (File.Exists(filePath) == false)
            {
                logs.Add(new LogInfo(LogState.Error, $"File [{filePath}] does not exist"));
                return logs;
            }

            FileInfo fileInfo = new FileInfo(filePath);

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

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_DirCopy));
            CodeInfo_DirCopy info = cmd.Info as CodeInfo_DirCopy;

            string srcDir = StringEscaper.Preprocess(s, info.SrcDir);
            string destDir = StringEscaper.Preprocess(s, info.DestDir);

            // Path Security Check
            if (StringEscaper.PathSecurityCheck(destDir, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            // DestPath must be directory 
            if (File.Exists(destDir))
            {
                logs.Add(new LogInfo(LogState.Error, $"Cannot overwrite file [{destDir}] with directory [{srcDir}]"));
                return logs;
            }

            // Check srcDir contains wildcard
            string wildcard = Path.GetFileName(srcDir);
            if (wildcard.IndexOfAny(new char[] { '*', '?' }) == -1)
            { // No Wildcard
                string destFullPath = Path.Combine(destDir, Path.GetFileName(srcDir));
                if (Directory.Exists(destFullPath))
                    logs.Add(new LogInfo(LogState.Ignore, $"Directory [{destFullPath}] will be overwritten with [{srcDir}]"));
                else
                    Directory.CreateDirectory(destFullPath);

                FileHelper.DirectoryCopy(srcDir, destFullPath, true, true, null);
                logs.Add(new LogInfo(LogState.Success, $"Directory [{srcDir}] copied to [{destFullPath}]", cmd));
            }
            else
            { // With Wildcard
                if (Directory.Exists(destDir))
                    logs.Add(new LogInfo(LogState.Ignore, $"Directory [{destDir}] will be overwritten with [{srcDir}]"));
                else
                    Directory.CreateDirectory(destDir);

                string srcParentDir = Path.GetDirectoryName(srcDir);

                DirectoryInfo dirInfo = new DirectoryInfo(srcParentDir);
                if (!dirInfo.Exists)
                    throw new DirectoryNotFoundException($"Source directory does not exist or cannot be found: {srcDir}");
                
                if (s.CompatDirCopyBug)
                { // Simulate WB082's [DirCopy,%SrcDir%\*,%DestDir%] filecopy bug
                    foreach (FileInfo f in dirInfo.GetFiles(wildcard))
                        File.Copy(f.FullName, Path.Combine(destDir, f.Name), true);
                }

                // Copy first sublevel directory with wildcard
                // Note that wildcard will not be applied to subdirectory copy
                foreach (DirectoryInfo subDir in dirInfo.GetDirectories(wildcard))
                    FileHelper.DirectoryCopy(subDir.FullName, Path.Combine(destDir, subDir.Name), true, true, null);

                logs.Add(new LogInfo(LogState.Success, $"Directory [{srcDir}] copied to [{destDir}]", cmd));
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

            // Delete Directory
            FileHelper.DirectoryDeleteEx(dirPath);

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

            // SrcPath must be directory 
            // WB082 does not check this, so file can be moved with DirMove
            if (File.Exists(srcDir))
            {
                logs.Add(new LogInfo(LogState.Error, $"[{srcDir}] is a file, not a directory"));
                return logs;
            }

            // DestPath must be directory 
            if (File.Exists(destPath))
            {
                logs.Add(new LogInfo(LogState.Error, $"[{destPath}] is a file, not a directory"));
                return logs;
            }

            if (Directory.Exists(destPath))
            {
                string destFullPath = Path.Combine(destPath, Path.GetFileName(srcDir));

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

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_DirMake));
            CodeInfo_DirMake info = cmd.Info as CodeInfo_DirMake;

            string destDir = StringEscaper.Preprocess(s, info.DestDir);

            // Path Security Check
            if (StringEscaper.PathSecurityCheck(destDir, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            // DestPath cannot be file
            if (File.Exists(destDir))
            {
                logs.Add(new LogInfo(LogState.Error, $"File [{destDir}] already exists"));
                return logs;
            }

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

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_DirSize));
            CodeInfo_DirSize info = cmd.Info as CodeInfo_DirSize;

            string dirPath = StringEscaper.Preprocess(s, info.DirPath);

            if (Directory.Exists(dirPath) == false)
            {
                logs.Add(new LogInfo(LogState.Error, $"Directory [{dirPath}] does not exist"));
                return logs;
            }

            string[] files = FileHelper.GetFilesEx(dirPath, "*", SearchOption.AllDirectories);
            long dirSize = 0;
            for (int i = 0; i < files.Length; i++)
            {
                FileInfo fileInfo = new FileInfo(files[i]);
                dirSize += fileInfo.Length;
            }

            logs.Add(new LogInfo(LogState.Success, $"Directory [{dirPath}] is [{dirSize}B]", cmd));

            List<LogInfo> varLogs = Variables.SetVariable(s, info.DestVar, dirSize.ToString());
            logs.AddRange(varLogs);

            return logs;
        }

        public static List<LogInfo> PathMove(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_PathMove));
            CodeInfo_PathMove info = cmd.Info as CodeInfo_PathMove;

            string srcPath = StringEscaper.Preprocess(s, info.SrcPath);
            string destPath = StringEscaper.Preprocess(s, info.DestPath);

            // Path Security Check
            if (StringEscaper.PathSecurityCheck(destPath, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

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
                {
                    logs.Add(new LogInfo(LogState.Error, $"[{destPath}] is a file, not a directory"));
                    return logs;
                }

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
