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
using SharpCompress.Common;
using SharpCompress.Writers;
// using SharpCompress.Archives;
// using SharpCompress.Archives.Zip;

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

            s.MainViewModel.BuildCommandProgressBarValue = 500;

            if (Directory.Exists(destArchive))
            {
                throw new ExecuteException($"[{destArchive}] should be a file, not a directory");
            }
            else
            {
                if (File.Exists(destArchive))
                    logs.Add(new LogInfo(LogState.Warning, $"File [{destArchive}] will be overwritten"));
            }

            if (!Directory.Exists(srcPath) && !File.Exists(srcPath))
                throw new ExecuteException($"Cannot find [{srcPath}]");

            switch (arcType)
            {
                case ArchiveCompressFormat.Zip:
                    ArchiveHelper.CompressZip(srcPath, destArchive, compLevel, encoding);
                    break;
                default:
                    throw new ExecuteException($"Compressing to [{arcType}] format is not supported");
            }
            logs.Add(new LogInfo(LogState.Success, $"[{srcPath}] compressed to [{destArchive}]"));
            
            return logs;
        }

        public static List<LogInfo> Decompress(EngineState s, CodeCommand cmd)
        { // Decompress,<ArchiveType>,<SrcArchive>,<DestDir>,[UTF8|UTF16|UTF16BE|ANSI]
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Decompress));
            CodeInfo_Decompress info = cmd.Info as CodeInfo_Decompress;

            ArchiveDecompressFormat arcType = info.Format;
            string srcArchive = StringEscaper.Preprocess(s, info.SrcArchive);
            string destDir = StringEscaper.Preprocess(s, info.DestDir);

            // Path Security Check
            if (StringEscaper.PathSecurityCheck(destDir, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            s.MainViewModel.BuildCommandProgressBarValue = 500;

            if (!File.Exists(srcArchive))
                throw new ExecuteException($"Cannot find [{srcArchive}]");

            if (!Directory.Exists(destDir))
            {
                if (File.Exists(destDir))
                    throw new ExecuteException($"[{destDir}] should be a directory, not a file");
                Directory.CreateDirectory(destDir);
            }

            switch (arcType)
            {
                case ArchiveDecompressFormat.Auto:
                    ArchiveHelper.DecompressAuto(srcArchive, destDir, true, info.Encoding); // Can handle null value of Encoding 
                    break;
                case ArchiveDecompressFormat.Zip: 
                    ArchiveHelper.DecompressZip(srcArchive, destDir, true, info.Encoding); // Can handle null value of Encoding 
                    break;
                case ArchiveDecompressFormat.Rar:
                    ArchiveHelper.DecompressRar(srcArchive, destDir, true, info.Encoding); // Can handle null value of Encoding 
                    break;
                case ArchiveDecompressFormat.SevenZip:
                    ArchiveHelper.Decompress7z(srcArchive, destDir, true, info.Encoding); // Can handle null value of Encoding 
                    break;
                default:
                    throw new ExecuteException($"Decompressing from [{arcType}] format is not supported");
            }

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

            s.MainViewModel.BuildCommandProgressBarValue = 500;

            if (!Directory.Exists(destDir))
            {
                if (File.Exists(destDir))
                    throw new ExecuteException($"Path [{destDir}] is file, not a directory");
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

            // TODO: Wildcard support
            string srcFileName = Path.GetFileName(srcFile);
            bool destIsDir = Directory.Exists(destPath);
            if (!destIsDir)
            {
                if (File.Exists(destPath))
                {
                    if (info.Preserve)
                    {
                        logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Warning, $"Cannot overwrite [{destPath}]"));
                        return logs;
                    }
                    else
                    {
                        logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Warning, $"[{destPath}] will be overwritten"));
                    }
                }
            }

            s.MainViewModel.BuildCommandProgressBarValue = 500;

            // Check srcFileName contains wildcard
            if (srcFileName.IndexOfAny(new char[] { '*', '?' }) == -1)
            { // No Wildcard
                if (srcFile.EndsWith("_", StringComparison.Ordinal))
                { // Extract Cabinet from _ (Ex) EXPLORER.EX_ -> EXPLORER.EXE
                    string destDir = destPath;
                    if (!destIsDir)
                        destDir = Path.Combine(destPath, srcFileName);

                    string srcCab = srcFile.Substring(0, srcFile.Length - 1) + "_";
                    if (File.Exists(srcCab))
                    {
                        string destFullPath = Path.Combine(destDir, srcFileName);
                        if (ArchiveHelper.ExtractCab(srcCab, destDir))
                        { // Extract Success
                            if (File.Exists(destFullPath)) // destFileName == srcFileName?
                            { // dest filename not specified
                                logs.Add(new LogInfo(LogState.Success, $"[{srcFileName}] from [{srcCab}] extracted to [{destFullPath}]"));
                            }
                            else // destFileName != srcFileName
                            { // Dest filename specified
                                File.Move(Path.Combine(destDir, srcFileName), destFullPath);
                                logs.Add(new LogInfo(LogState.Success, $"[{srcFileName}] from [{srcCab}] extracted to [{destFullPath}]"));
                            }
                        }
                        else
                        { // Extract Fail
                            logs.Add(new LogInfo(LogState.Error, $"Failed to extract [{srcCab}]"));
                        }
                    }
                    else
                    { // Error
                        logs.Add(new LogInfo(LogState.Error, $"[{srcFile}] nor [{srcCab}] not found"));
                    }
                }
                else
                { // SrcFile is uncompressed, just copy!  
                    string destFullPath = destPath;
                    if (destIsDir)
                        destFullPath = Path.Combine(destPath, srcFileName);
                    File.Copy(srcFile, destFullPath, !info.Preserve);
                    logs.Add(new LogInfo(LogState.Success, $"[{srcFile}] copied to [{destFullPath}]"));
                }

                return logs;
            }
            else
            { // Wildcard
                string srcDirToFind = FileHelper.GetDirNameEx(srcFile);
                string[] files = Directory.GetFiles(srcDirToFind, srcFileName);

                if (0 < files.Length)
                { // One or more file will be copied
                    foreach (string f in files)
                    {
                        if (f.EndsWith("_", StringComparison.Ordinal))
                        { // Extract Cabinet from _ (Ex) EXPLORER.EX_ -> EXPLORER.EXE
                            string destDir = destPath;
                            if (!destIsDir)
                                destDir = Path.Combine(destPath, srcFileName);

                            string cab = f.Substring(0, srcFile.Length - 1) + "_";
                            if (File.Exists(cab))
                            {
                                string destFullPath = Path.Combine(destDir, srcFileName);
                                if (ArchiveHelper.ExtractCab(cab, destDir))
                                { // Extract Success
                                    if (File.Exists(destFullPath)) // destFileName == srcFileName?
                                    { // dest filename not specified
                                        logs.Add(new LogInfo(LogState.Success, $"[{srcFileName}] from [{cab}] extracted to [{destFullPath}]"));
                                    }
                                    else // destFileName != srcFileName
                                    { // Dest filename specified
                                        File.Move(Path.Combine(destDir, srcFileName), destFullPath);
                                        logs.Add(new LogInfo(LogState.Success, $"[{srcFileName}] from [{cab}] extracted to [{destFullPath}]"));
                                    }
                                }
                                else
                                { // Extract Fail
                                    logs.Add(new LogInfo(LogState.Error, $"Failed to extract [{cab}]"));
                                }
                            }
                            else
                            { // Error
                                logs.Add(new LogInfo(LogState.Error, $"[{f}] nor [{cab}] not found"));
                            }
                        }
                        else
                        { // SrcFile is uncompressed, just copy!  
                            string destFullPath = destPath;
                            if (destIsDir)
                                destFullPath = Path.Combine(destPath, srcFileName);
                            File.Copy(srcFile, destFullPath, !info.Preserve);
                            logs.Add(new LogInfo(LogState.Success, $"[{f}] copied to [{destFullPath}]"));
                        }
                    }

                    logs.Add(new LogInfo(LogState.Success, $"[{files.Length}] files copied"));
                }
                else
                { // No file will be deleted
                    logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Warning, $"Files match wildcard [{srcFile}] not found"));
                }
            }

            return logs;
        }
    }
}
