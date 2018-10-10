/*
    Copyright (C) 2016-2018 Hajin Jang
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

using PEBakery.Cab;
using PEBakery.Helper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using SevenZip;

namespace PEBakery.Core.Commands
{
    public class CommandArchive
    {
        public static List<LogInfo> Compress(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_Compress info = cmd.Info.Cast<CodeInfo_Compress>();

            #region Event Handlers
            void ReportCompressProgress(object sender, ProgressEventArgs e)
            {
                s.MainViewModel.BuildCommandProgressValue = e.PercentDone;
                s.MainViewModel.BuildCommandProgressText = $"Compressing... ({e.PercentDone}%)";
            }
            #endregion

            // Parse arguments / parameters
            string srcPath = StringEscaper.Preprocess(s, info.SrcPath);
            string destArchive = StringEscaper.Preprocess(s, info.DestArchive);
            SevenZip.OutArchiveFormat outFormat = ArchiveHelper.ToSevenZipOutFormat(info.Format);
            SevenZip.CompressionLevel compLevel = SevenZip.CompressionLevel.Normal;
            if (info.CompressLevel is ArchiveHelper.CompressLevel level)
            {
                try
                {
                    compLevel = ArchiveHelper.ToSevenZipLevel(level);
                }
                catch (ArgumentException)
                { // Should have been filtered by CodeParser
                    logs.Add(new LogInfo(LogState.CriticalError, $"Invalid ArchiveHelper.CompressLevel [{info.CompressLevel}]"));
                    return logs;
                }
            }
                
            // Path Security Check
            if (!StringEscaper.PathSecurityCheck(destArchive, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            // Check if a file or directory exist under name of destArchive 
            if (Directory.Exists(destArchive))
                return LogInfo.LogErrorMessage(logs, $"[{destArchive}] should be a file, not a directory");
            if (File.Exists(destArchive))
            {
                logs.Add(new LogInfo(LogState.Overwrite, $"File [{destArchive}] will be overwritten"));
                File.Delete(destArchive);
            }

            // If parent directory of destArchive does not exist, create it
            Directory.CreateDirectory(FileHelper.GetDirNameEx(destArchive));

            // Call SevenZipSharp
            SevenZipCompressor compressor = new SevenZipCompressor
            {
                ArchiveFormat = outFormat,
                CompressionMode = CompressionMode.Create,
                CompressionLevel = compLevel,
            };

            // Set filename encoding
            switch (outFormat)
            {
                case OutArchiveFormat.Zip:
                    compressor.CustomParameters["cu"] = "on"; // Force UTF-8 for filename
                    break;
            }

            if (File.Exists(srcPath))
            {
                // Compressor Options
                compressor.DirectoryStructure = false;

                // Compressor Callbacks
                compressor.Compressing += ReportCompressProgress;

                s.MainViewModel.SetBuildCommandProgress("Compress Progress");
                try
                {
                    using (FileStream fs = new FileStream(destArchive, FileMode.Create))
                    {
                        compressor.CompressFiles(fs, srcPath);
                    }
                }
                finally
                {
                    compressor.Compressing -= ReportCompressProgress;
                    s.MainViewModel.ResetBuildCommandProgress();
                }
                
            }
            else if (Directory.Exists(srcPath))
            {
                // Compressor Options
                compressor.DirectoryStructure = true;
                compressor.PreserveDirectoryRoot = true;
                compressor.IncludeEmptyDirectories = true;

                // Compressor Callbacks
                compressor.Compressing += ReportCompressProgress;

                s.MainViewModel.SetBuildCommandProgress("Compress Progress");
                try
                {
                    using (FileStream fs = new FileStream(destArchive, FileMode.Create))
                    {
                        compressor.CompressDirectory(srcPath, fs);
                    }
                }
                finally
                {
                    compressor.Compressing -= ReportCompressProgress;
                    s.MainViewModel.ResetBuildCommandProgress();
                }
            }
            else
            {
                return LogInfo.LogErrorMessage(logs, $"Cannot find [{srcPath}]");
            }

            if (File.Exists(destArchive))
                logs.Add(new LogInfo(LogState.Success, $"[{srcPath}] compressed to [{destArchive}]"));
            else
                logs.Add(new LogInfo(LogState.Error, $"Compressing to [{srcPath}] failed"));

            return logs;
        }

        public static List<LogInfo> Decompress(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_Decompress info = cmd.Info.Cast<CodeInfo_Decompress>();

            #region Event Handlers
            void ReportDecompressProgress(object sender, ProgressEventArgs e)
            {
                s.MainViewModel.BuildCommandProgressValue = e.PercentDone;
                s.MainViewModel.BuildCommandProgressText = $"Decompressing... ({e.PercentDone}%)";
            }
            #endregion

            string srcArchive = StringEscaper.Preprocess(s, info.SrcArchive);
            string destDir = StringEscaper.Preprocess(s, info.DestDir);

            // Path Security Check
            if (!StringEscaper.PathSecurityCheck(destDir, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            // Does SrcArchive exist?
            if (!File.Exists(srcArchive))
                return LogInfo.LogErrorMessage(logs, $"Cannot find [{srcArchive}]");

            // Check if file or directory exist under name of destDir
            if (!Directory.Exists(destDir))
            {
                if (File.Exists(destDir))
                    return LogInfo.LogErrorMessage(logs, $"[{destDir}] should be a directory, not a file");

                Directory.CreateDirectory(destDir);
            }

            using (SevenZipExtractor extractor = new SevenZipExtractor(srcArchive))
            {
                extractor.Extracting += ReportDecompressProgress;
                s.MainViewModel.SetBuildCommandProgress("Decompress Progress");
                try
                {
                    extractor.ExtractArchive(destDir);
                }
                finally
                {
                    extractor.Extracting -= ReportDecompressProgress;
                    s.MainViewModel.ResetBuildCommandProgress();
                }
            }

            logs.Add(new LogInfo(LogState.Success, $"[{srcArchive}] decompressed to [{destDir}]"));
            return logs;
        }

        public static List<LogInfo> Expand(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_Expand info = cmd.Info.Cast<CodeInfo_Expand>();
            List<string> extractedFiles = new List<string>();

            #region Event Handlers
            void ReportExpandProgress(object sender, ProgressEventArgs e)
            {
                s.MainViewModel.BuildCommandProgressValue = e.PercentDone;
                s.MainViewModel.BuildCommandProgressText = $"Expanding... ({e.PercentDone}%)";
            }

            object trackLock = new object();
            void TrackExtractedFile(object sender, FileInfoEventArgs e)
            {
                lock (trackLock)
                    extractedFiles.Add(e.FileInfo.FileName);
            }
            #endregion

            string srcCab = StringEscaper.Preprocess(s, info.SrcCab);
            string destDir = StringEscaper.Preprocess(s, info.DestDir);
            string singleFile = null;
            if (info.SingleFile != null)
                singleFile = StringEscaper.Preprocess(s, info.SingleFile);

            // Path Security Check
            if (!StringEscaper.PathSecurityCheck(destDir, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            if (!Directory.Exists(destDir))
            {
                if (File.Exists(destDir))
                    return LogInfo.LogErrorMessage(logs, $"Path [{destDir}] is a file, not a directory");
                Directory.CreateDirectory(destDir);
            }

            // Does srcCab exist?
            if (!File.Exists(srcCab))
                return LogInfo.LogErrorMessage(logs, $"Cannot find [{srcCab}]");

            // Turn on report progress only if file is larger than 1MB
            FileInfo fi = new FileInfo(srcCab);
            bool reportProgress = 1024 * 1024 <= fi.Length; 

            if (singleFile == null)
            { // No singleFile operand, extract all
                using (SevenZipExtractor extractor = new SevenZipExtractor(srcCab))
                {
                    if (extractor.Format != InArchiveFormat.Cab)
                        return LogInfo.LogErrorMessage(logs, "Expand command must be used with cabinet archive");

                    if (reportProgress)
                    {
                        extractor.FileExtractionFinished += TrackExtractedFile;
                        extractor.Extracting += ReportExpandProgress;
                        s.MainViewModel.SetBuildCommandProgress("Expand Progress");
                    }

                    try
                    {
                        extractor.ExtractArchive(destDir);
                        foreach (string file in extractedFiles)
                            logs.Add(new LogInfo(LogState.Success, $"[{file}] extracted"));
                        logs.Add(new LogInfo(LogState.Success, $"[{extractedFiles.Count}] files from [{srcCab}] extracted to [{destDir}]"));
                    }
                    finally
                    {
                        if (reportProgress)
                        {
                            extractor.FileExtractionFinished -= TrackExtractedFile;
                            extractor.Extracting -= ReportExpandProgress;
                            s.MainViewModel.ResetBuildCommandProgress();
                        }
                    }
                }
            }
            else
            { // singleFile specified, extract only that file
                string destPath = Path.Combine(destDir, singleFile);
                if (File.Exists(destPath))
                { // Check PRESERVE, NOWARN 
                    if (info.Preserve)
                    { // Do nothing
                        logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Overwrite, $"[{destPath}] already exists, skipping extract from [{srcCab}]"));
                        return logs;
                    }

                    logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Overwrite, $"[{destPath}] will be overwritten"));
                }

                using (SevenZipExtractor extractor = new SevenZipExtractor(srcCab))
                {
                    if (extractor.Format != InArchiveFormat.Cab)
                        return LogInfo.LogErrorMessage(logs, "Expand command must be used with cabinet archive");

                    if (reportProgress)
                    {
                        extractor.Extracting += ReportExpandProgress;
                        s.MainViewModel.SetBuildCommandProgress("Expand Progress");
                    }

                    try
                    {
                        string destFile = Path.Combine(destDir, singleFile);
                        using (FileStream fs = new FileStream(destFile, FileMode.Create))
                        {
                            if (extractor.ArchiveFileNames.Contains(singleFile))
                            {
                                extractor.ExtractFile(singleFile, fs);
                                logs.Add(new LogInfo(LogState.Success, $"[{singleFile}] from [{srcCab}] extracted to [{destPath}]"));
                            }
                            else
                            { // Unable to find specified file
                                logs.Add(new LogInfo(LogState.Error, $"Failed to extract [{singleFile}] from [{srcCab}]"));
                            }   
                        }
                    }
                    finally
                    {
                        if (reportProgress)
                        {
                            extractor.Extracting -= ReportExpandProgress;
                            s.MainViewModel.ResetBuildCommandProgress();
                        }
                    }
                }
            }

            return logs;
        }

        public static List<LogInfo> CopyOrExpand(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_CopyOrExpand info = cmd.Info.Cast<CodeInfo_CopyOrExpand>();

            string srcFile = StringEscaper.Preprocess(s, info.SrcFile);
            string destPath = StringEscaper.Preprocess(s, info.DestPath);
            Debug.Assert(srcFile != null, $"{nameof(srcFile)} != null");
            Debug.Assert(destPath != null, $"{nameof(destPath)} != null");

            // Path Security Check
            if (!StringEscaper.PathSecurityCheck(destPath, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            // Check srcFile contains wildcard
            if (srcFile.IndexOfAny(new char[] { '*', '?' }) == -1)
            { // No Wildcard
                InternalCopyOrExpand(logs, info, srcFile, destPath);
            }
            else
            { // With Wildcard
                string srcDirToFind = FileHelper.GetDirNameEx(srcFile);

                string[] files = FileHelper.GetFilesEx(srcDirToFind, Path.GetFileName(srcFile));

                if (0 < files.Length)
                { // One or more file will be copied
                    logs.Add(new LogInfo(LogState.Success, $"[{srcFile}] will be copied to [{destPath}]", cmd));

                    foreach (string file in files)
                        InternalCopyOrExpand(logs, info, file, destPath);

                    logs.Add(new LogInfo(LogState.Success, $"[{files.Length}] files copied", cmd));
                }
                else
                { // No file will be copied
                    logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Warning, $"No files matching wildcard [{srcFile}] were found", cmd));
                }
            }

            return logs;
        }

        private static void InternalCopyOrExpand(List<LogInfo> logs, CodeInfo_CopyOrExpand info, string srcFile, string destPath)
        {
            if (srcFile == null)
                throw new ArgumentNullException(nameof(srcFile));
            if (destPath == null)
                throw new ArgumentNullException(nameof(destPath));

            string srcFileName = Path.GetFileName(srcFile);
            bool destIsDir = Directory.Exists(destPath);
            bool destIsFile = File.Exists(destPath);
            if (!destIsDir)
            {
                if (destIsFile)
                {
                    if (info.Preserve)
                    {
                        logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Overwrite, $"[{destPath}] will not be overwritten"));
                        return;
                    }

                    logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Overwrite, $"[{destPath}] will be overwritten"));
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
                string destDir = destIsDir ? destPath : Path.GetDirectoryName(destPath);
                if (destDir == null)
                    throw new InternalException("Internal Logic Error at InternalCopyOrExpand");

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
                            if (2 < fileList.Count) // WB082 behavior : Expand/CopyOrExpand only supports single-file cabinet
                            {
                                logs.Add(new LogInfo(LogState.Error, $"Cabinet [{srcFileName}] should contain only a single file"));
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
                                    logs.Add(new LogInfo(LogState.Overwrite, $"File [{destFullPath}] already exists and will be overwritten"));

                                try
                                {
                                    string destParent = Path.GetDirectoryName(destFullPath);
                                    if (destParent == null)
                                        throw new InternalException("Internal Logic Error at InternalCopyOrExpand");
                                    if (!Directory.Exists(destParent))
                                        Directory.CreateDirectory(destParent);
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
                                logs.Add(new LogInfo(LogState.Error, $"Cabinet [{srcFileName}] does not contain [{Path.GetFileName(destPath)}]"));
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
                    logs.Add(new LogInfo(LogState.Error, $"The file [{srcFile}] or [{srcCab}] could not be found"));
                }
            }
        }
    }
}
