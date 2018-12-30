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

using PEBakery.Helper;
using SevenZip;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

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
            SevenZip.OutArchiveFormat outFormat = ArchiveFile.ToSevenZipOutFormat(info.Format);
            SevenZip.CompressionLevel compLevel = SevenZip.CompressionLevel.Normal;
            if (info.CompressLevel is ArchiveFile.CompressLevel level)
            {
                try
                {
                    compLevel = ArchiveFile.ToSevenZipLevel(level);
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
            bool appendMode = false;
            if (Directory.Exists(destArchive))
                return LogInfo.LogErrorMessage(logs, $"[{destArchive}] should be a file, not a directory");
            if (File.Exists(destArchive))
            {
                if (info.Format == ArchiveFile.ArchiveCompressFormat.Zip)
                {
                    logs.Add(new LogInfo(LogState.Overwrite, $"Archive [{destArchive}] will be appended"));
                    appendMode = true;
                }
                else
                {
                    logs.Add(new LogInfo(LogState.Overwrite, $"File [{destArchive}] will be overwritten"));
                    File.Delete(destArchive);
                }
            }

            // If parent directory of destArchive does not exist, create it
            Directory.CreateDirectory(FileHelper.GetDirNameEx(destArchive));

            // Call SevenZipSharp
            SevenZipCompressor compressor = new SevenZipCompressor
            {
                ArchiveFormat = outFormat,
                CompressionMode = appendMode ? CompressionMode.Append : CompressionMode.Create,
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
                    compressor.CompressFiles(destArchive, srcPath);
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
                    compressor.CompressDirectory(srcPath, destArchive);
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

                    string[] archiveFileNames = extractor.ArchiveFileNames.ToArray();
                    if (archiveFileNames.Contains(singleFile, StringComparer.OrdinalIgnoreCase))
                    {
                        try
                        {
                            string destFile = Path.Combine(destDir, singleFile);
                            using (FileStream fs = new FileStream(destFile, FileMode.Create))
                            {
                                extractor.ExtractFile(singleFile, fs);
                            }
                            logs.Add(new LogInfo(LogState.Success, $"[{singleFile}] from [{srcCab}] extracted to [{destPath}]"));
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
                    else
                    { // Unable to find specified file
                        logs.Add(new LogInfo(LogState.Error, $"Failed to extract [{singleFile}] from [{srcCab}]"));
                    }
                    
                }
            }

            return logs;
        }

        public static List<LogInfo> CopyOrExpand(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();
            CodeInfo_CopyOrExpand info = cmd.Info.Cast<CodeInfo_CopyOrExpand>();

            #region Event Handlers
            void ReportExpandProgress(object sender, ProgressEventArgs e)
            {
                s.MainViewModel.BuildCommandProgressValue = e.PercentDone;
                s.MainViewModel.BuildCommandProgressText = $"Expanding... ({e.PercentDone}%)";
            }
            #endregion

            string srcFile = StringEscaper.Preprocess(s, info.SrcFile);
            string destPath = StringEscaper.Preprocess(s, info.DestPath);
            Debug.Assert(srcFile != null, $"{nameof(srcFile)} != null");
            Debug.Assert(destPath != null, $"{nameof(destPath)} != null");

            // Path Security Check
            if (!StringEscaper.PathSecurityCheck(destPath, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            string srcFileName = Path.GetFileName(srcFile);
            string srcFileExt = Path.GetExtension(srcFile);
            if (!Directory.Exists(destPath))
            {
                if (File.Exists(destPath))
                {
                    if (info.Preserve)
                    {
                        logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Overwrite, $"File [{destPath}] already exists and will not be overwritten"));
                        return logs;
                    }

                    logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Overwrite, $"File [{destPath}] already exists and will be overwritten"));
                }
            }

            // Get destFullPath. It should be a file.
            string destDir;
            string destFullPath;
            if (Directory.Exists(destPath))
            {
                destDir = destPath;
                destFullPath = Path.Combine(destPath, srcFileName);
            }
            else // Move to new name
            {
                destDir = FileHelper.GetDirNameEx(destPath);
                destFullPath = destPath;
            }
            Directory.CreateDirectory(destDir);

            // Check wildcard
            // WinBuilder082 behavior
            // - Some files are matched with wildcard : Reports success, but no files are copied.
            // - No files are matched with wildcard   : Reports error.
            string wildcard = Path.GetFileName(srcFile);
            if (wildcard.IndexOfAny(new char[] {'*', '?'}) != -1)
            {
                logs.Add(new LogInfo(LogState.Warning, "CopyOrExpand does not support filename with wildcard"));
                return logs;
            }

            // Copy or Expand srcFile.
            if (File.Exists(srcFile))
            { // SrcFile is uncompressed, just copy!
                File.Copy(srcFile, destFullPath, !info.Preserve);
                logs.Add(new LogInfo(LogState.Success, $"[{srcFile}] copied to [{destPath}]"));
            }
            else
            { // Extract Cabinet from _ (Ex) EXPLORER.EX_ -> EXPLORER.EXE
              // Terminate if a file does not have equivalent cabinet file
                if (srcFileExt.Length == ".".Length)
                    return logs;
                string srcCabExt = srcFileExt.Substring(0, srcFileExt.Length - 1) + "_";
                string srcCab = srcFile.Substring(0, srcFile.Length - srcCabExt.Length) + srcCabExt;

                if (File.Exists(srcCab))
                {
                    // Turn on report progress only if file is larger than 1MB
                    FileInfo fi = new FileInfo(srcCab);
                    bool reportProgress = 1024 * 1024 <= fi.Length;

                    using (SevenZipExtractor extractor = new SevenZipExtractor(srcCab))
                    {
                        if (extractor.Format != InArchiveFormat.Cab)
                            return LogInfo.LogErrorMessage(logs, $"[{srcCab}] is not a cabinet archive");

                        string[] archiveFileNames = extractor.ArchiveFileNames.ToArray();
                        if (archiveFileNames.Length != 1)
                            return LogInfo.LogErrorMessage(logs, $"Cabinet [{srcCab}] should contain only a single file");

                        if (!archiveFileNames.Contains(srcFileName, StringComparer.OrdinalIgnoreCase))
                            return LogInfo.LogErrorMessage(logs, $"Failed to extract [{srcFileName}] from [{srcCab}]");

                        if (reportProgress)
                        {
                            extractor.Extracting += ReportExpandProgress; // Use "Expanding" instead of "CopyOrExpanding"
                            s.MainViewModel.SetBuildCommandProgress("CopyOrExpand Progress");
                        }
                        try
                        {
                            using (FileStream fs = new FileStream(destFullPath, FileMode.Create))
                            {
                                extractor.ExtractFile(srcFileName, fs);
                            }
                            logs.Add(new LogInfo(LogState.Success, $"[{srcCab}] extracted to [{destFullPath}]"));
                        }
                        finally
                        {
                            extractor.Extracting -= ReportExpandProgress; // Use "Expanding" instead of "CopyOrExpanding"
                            s.MainViewModel.ResetBuildCommandProgress();
                        }
                    }
                }
                else
                { // Error
                    logs.Add(new LogInfo(LogState.Error, $"The file [{srcFile}] or [{Path.GetFileName(srcCab)}] could not be found"));
                }
            }

            return logs;
        }
    }
}
