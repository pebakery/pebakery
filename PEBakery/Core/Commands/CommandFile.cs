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
                            logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Warning, $"Cannot overwrite [{destFullPath}]", cmd));
                            return logs;
                        }
                        else
                        {
                            logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Warning, $"[{destFullPath}] will be overwritten", cmd));
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
                            logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Warning, $"Cannot overwrite [{destPath}]", cmd));
                            return logs;
                        }
                        else
                        {
                            logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Warning, $"[{destPath}] will be overwritten", cmd));
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
    }
}
