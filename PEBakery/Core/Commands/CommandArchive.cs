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

using PEBakery.CabLib;
using PEBakery.Exceptions;
using PEBakery.Helper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PEBakery.Core.Commands
{
    public class CommandArchive
    {
        public static List<LogInfo> Compress(EngineState s, CodeCommand cmd)
        { // Compress,<ArchiveType>,<SrcPath>,<DestArchive>,[CompressLevel],[UTF8|UTF16|UTF16BE|ANSI]
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Compress));
            CodeInfo_Compress info = cmd.Info as CodeInfo_Compress;

            ArchiveCompressFormat arcType = info.Format;
            string srcPath = StringEscaper.Preprocess(s, info.SrcPath);
            string destArchive = StringEscaper.Preprocess(s, info.DestArchive);

            ArchiveHelper.CompressLevel compLevel = ArchiveHelper.CompressLevel.Normal;
            if (info.CompressLevel != null)
                compLevel = (ArchiveHelper.CompressLevel) info.CompressLevel;

            Encoding encoding = info.Encoding;
            if (info.Encoding == null)
                encoding = Encoding.UTF8;

            // Path Security Check
            if (StringEscaper.PathSecurityCheck(destArchive, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            if (Directory.Exists(destArchive))
            {
                logs.Add(new LogInfo(LogState.Error, $"[{destArchive}] should be a file, not a directory"));
                return logs;
            }
            else
            {
                if (File.Exists(destArchive))
                    logs.Add(new LogInfo(LogState.Warning, $"File [{destArchive}] will be overwritten"));
            }

            if (!Directory.Exists(srcPath) && !File.Exists(srcPath))
            {
                logs.Add(new LogInfo(LogState.Error, $"Cannot find [{srcPath}]"));
                return logs;
            }

            bool success;
            switch (arcType)
            {
                case ArchiveCompressFormat.Zip:
                    success = ArchiveHelper.CompressManagedZip(srcPath, destArchive, compLevel, encoding);
                    break;
                default:
                    logs.Add(new LogInfo(LogState.Error, $"Compressing to [{arcType}] format is not supported"));
                    return logs;
            }
            if (success)
                logs.Add(new LogInfo(LogState.Success, $"[{srcPath}] compressed to [{destArchive}]"));
            else
                logs.Add(new LogInfo(LogState.Error, $"Compressing [{srcPath}] failed"));

            return logs;
        }

        public static List<LogInfo> Decompress(EngineState s, CodeCommand cmd)
        { // Decompress,<SrcArchive>,<DestDir>,[UTF8|UTF16|UTF16BE|ANSI]
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Decompress));
            CodeInfo_Decompress info = cmd.Info as CodeInfo_Decompress;

            string srcArchive = StringEscaper.Preprocess(s, info.SrcArchive);
            string destDir = StringEscaper.Preprocess(s, info.DestDir);

            // Path Security Check
            if (StringEscaper.PathSecurityCheck(destDir, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            if (!File.Exists(srcArchive))
            {
                logs.Add(new LogInfo(LogState.Error, $"Cannot find [{srcArchive}]"));
                return logs;
            }

            if (!Directory.Exists(destDir))
            {
                if (File.Exists(destDir))
                {
                    logs.Add(new LogInfo(LogState.Error, $"[{destDir}] should be a directory, not a file"));
                    return logs;
                }
                Directory.CreateDirectory(destDir);
            }

            if (info.Encoding == null)
                ArchiveHelper.DecompressNative(srcArchive, destDir, true);
            else
                ArchiveHelper.DecompressManaged(srcArchive, destDir, true, info.Encoding); // Can handle null value of Encoding 

            logs.Add(new LogInfo(LogState.Success, $"[{srcArchive}] compressed to [{destDir}]"));

            return logs;
        }

        public static List<LogInfo> Expand(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Expand));
            CodeInfo_Expand info = cmd.Info as CodeInfo_Expand;

            string srcCab = StringEscaper.Preprocess(s, info.SrcCab);
            string destDir = StringEscaper.Preprocess(s, info.DestDir);
            string singleFile = null;
            if (info.SingleFile != null)
                singleFile = StringEscaper.Preprocess(s, info.SingleFile);

            // Path Security Check
            if (StringEscaper.PathSecurityCheck(destDir, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            if (!Directory.Exists(destDir))
            {
                if (File.Exists(destDir))
                {
                    logs.Add(new LogInfo(LogState.Error, $"Path [{destDir}] is file, not a directory"));
                    return logs;
                }
                Directory.CreateDirectory(destDir);
            }

            if (singleFile == null)
            { // No singleFile operand, extract all
                if (ArchiveHelper.ExtractCab(srcCab, destDir, out List<string> doneList)) // Success
                {
                    foreach (string done in doneList)
                        logs.Add(new LogInfo(LogState.Success, $"[{done}] extracted"));
                    logs.Add(new LogInfo(LogState.Success, $"[{doneList.Count}] files from [{srcCab}] extracted to [{destDir}]"));
                }
                else // Failure
                {
                    logs.Add(new LogInfo(LogState.Error, $"Failed to extract [{srcCab}]"));
                }
            }
            else
            { // singleFile specified, extract only that singleFile
                string destPath = Path.Combine(destDir, singleFile);
                if (File.Exists(destPath))
                { // Check PRESERVE, NOWARN 
                    if (info.Preserve)
                    { // Do nothing
                        logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Warning, $"[{destPath}] already exists, cannot extract from [{srcCab}]"));
                        return logs;
                    }
                    else
                    {
                        logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Warning, $"[{destPath}] will be overwritten"));
                    }
                }

                if (ArchiveHelper.ExtractCab(srcCab, destDir, singleFile)) // Success
                    logs.Add(new LogInfo(LogState.Success, $"[{singleFile}] from [{srcCab}] extracted to [{destPath}]"));
                else // Failure
                    logs.Add(new LogInfo(LogState.Error, $"Failed to extract [{singleFile}] from [{srcCab}]"));
            }

            return logs;
        }

        public static List<LogInfo> CopyOrExpand(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_CopyOrExpand));
            CodeInfo_CopyOrExpand info = cmd.Info as CodeInfo_CopyOrExpand;

            string srcFile = StringEscaper.Preprocess(s, info.SrcFile);
            string destPath = StringEscaper.Preprocess(s, info.DestPath);

            // Path Security Check
            if (StringEscaper.PathSecurityCheck(destPath, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            // Check srcFile contains wildcard
            if (srcFile.IndexOfAny(new char[] { '*', '?' }) == -1)
            { // No Wildcard
                InternalCopyOrExpand(s, logs, info, srcFile, destPath);
            }
            else
            { // With Wildcard
                string srcDirToFind = FileHelper.GetDirNameEx(srcFile);

                string[] files = FileHelper.GetFilesEx(srcDirToFind, Path.GetFileName(srcFile));

                if (0 < files.Length)
                { // One or more file will be copied
                    logs.Add(new LogInfo(LogState.Success, $"[{srcFile}] will be copied to [{destPath}]", cmd));

                    for (int i = 0; i < files.Length; i++)
                    {
                        string f = files[i];
                        InternalCopyOrExpand(s, logs, info, f, destPath);
                    }

                    logs.Add(new LogInfo(LogState.Success, $"[{files.Length}] files copied", cmd));
                }
                else
                { // No file will be copied
                    logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Warning, $"Files match wildcard [{srcFile}] not found", cmd));
                }
            }

            return logs;
        }

        private static void InternalCopyOrExpand(EngineState s, List<LogInfo> logs, CodeInfo_CopyOrExpand info, string srcFile, string destPath)
        {
            string srcFileName = Path.GetFileName(srcFile);
            bool destIsDir = Directory.Exists(destPath);
            bool destIsFile = File.Exists(destPath);
            if (!destIsDir)
            {
                if (destIsFile)
                {
                    if (info.Preserve)
                    {
                        logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Warning, $"Cannot overwrite [{destPath}]"));
                        return;
                    }
                    else
                    {
                        logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Warning, $"[{destPath}] will be overwritten"));
                    }
                }
            }

            if (File.Exists(srcFile))
            { // SrcFile is uncompressed, just copy!
                string destFullPath = destPath;
                if (destIsDir)
                    destFullPath = Path.Combine(destPath, srcFileName);
                else if (!destIsFile)
                    Directory.CreateDirectory(FileHelper.GetDirNameEx(destPath));

                File.Copy(srcFile, destFullPath, !info.Preserve);
                logs.Add(new LogInfo(LogState.Success, $"[{srcFile}] copied to [{destPath}]"));
            }
            else
            { // Extract Cabinet from _ (Ex) EXPLORER.EX_ -> EXPLORER.EXE
                string destDir;
                if (destIsDir)
                    destDir = destPath;
                else
                    destDir = Path.GetDirectoryName(destPath);

                string srcCab = srcFile.Substring(0, srcFile.Length - 1) + "_";
                if (File.Exists(srcCab))
                {
                    // Get Temp Dir
                    string tempDir;
                    {
                        string tempDirName = Path.GetTempFileName();
                        File.Delete(tempDirName);
                        tempDir = Path.Combine(Path.GetTempPath(), tempDirName);
                    }
                    Directory.CreateDirectory(tempDir);

                    try
                    {
                        bool result;
                        using (FileStream fs = new FileStream(srcCab, FileMode.Open, FileAccess.Read, FileShare.Read))
                        using (CabExtract cab = new CabExtract(fs))
                        {
                            result = cab.ExtractAll(tempDir, out List<string> fileList);
                            if (2 < fileList.Count)
                            { // WB082 behavior : Expand/CopyOrExpand only supports single-file cabinet
                                logs.Add(new LogInfo(LogState.Error, $"Cabinet [{srcFileName}] should contain single file"));
                                return;
                            }
                        }

                        if (result)
                        { // Extract Success
                            string tempFullPath = Path.Combine(tempDir, srcFileName);
                            if (File.Exists(tempFullPath))
                            {
                                string destFullPath;
                                if (destIsDir)
                                    destFullPath = Path.Combine(destDir, srcFileName);
                                else // Move to new name
                                    destFullPath = Path.Combine(destDir, Path.GetFileName(destPath));

                                if (File.Exists(destFullPath))
                                    logs.Add(new LogInfo(LogState.Warning, $"File [{destFullPath}] already exists, will be overwritten"));

                                try
                                {
                                    if (!Directory.Exists(Path.GetDirectoryName(destFullPath)))
                                        Directory.CreateDirectory(Path.GetDirectoryName(destFullPath));
                                    File.Copy(tempFullPath, destFullPath, true);
                                    logs.Add(new LogInfo(LogState.Success, $"[{srcFileName}] from [{srcCab}] extracted to [{destFullPath}]"));
                                }
                                finally
                                {
                                    File.Delete(tempFullPath);
                                }
                            }
                            else
                            { // Unable to find srcFile
                                logs.Add(new LogInfo(LogState.Error, $"Cabinet [{srcFileName}] does not contains [{Path.GetFileName(destPath)}]"));
                            }
                        }
                        else
                        { // Extract Fail
                            logs.Add(new LogInfo(LogState.Error, $"Failed to extract [{srcCab}]"));
                        }
                    }
                    finally
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                else
                { // Error
                    logs.Add(new LogInfo(LogState.Error, $"[{srcFile}] nor [{srcCab}] not found"));
                }
            }
        }
    }
}
