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
using PEBakery.Ini;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace PEBakery.Core.Commands
{
    public static class CommandScript
    {
        /*
         * WB082 Behavior
         * ExtractFile : DestDir must be Directory, create if not exists.
         * Ex) (...),README.txt,%BaseDir%\Temp\Hello
         *   -> No Hello : Create directory "Hello" and extract files into new directory.
         *   -> Hello is a file : Failure
         *   -> Hello is a directory : Extract files into directory.
         * 
         * ExtractAllFiles
         * Ex) (...),Fonts,%BaseDir%\Temp\Hello
         *   -> No Hello : Failure
         *   -> Hello is a file : Failure
         *   -> Hello is a directory : Extract files into directory.
         * 
         * PEBakery Behavior
         * ExtractFile/ExtractAllFiles : DestDir must be Directory, create if not exists.
         * Ex) (...),README.txt,%BaseDir%\Temp\Hello
         *   -> No Hello : Create directory "Hello" and extract files into new directory.
         *   -> Hello is a file : Failure
         *   -> Hello is a directory : Extract files into directory.
         */

        public static List<LogInfo> ExtractFile(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_ExtractFile info = cmd.Info.Cast<CodeInfo_ExtractFile>();

            string scriptFile = StringEscaper.Preprocess(s, info.ScriptFile);
            string dirName = StringEscaper.Preprocess(s, info.DirName);
            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string destDir = StringEscaper.Preprocess(s, info.DestDir); // Should be directory name

            Script sc = Engine.GetScriptInstance(s, s.CurrentScript.RealPath, scriptFile, out _);

            // Check if encoded file exist
            if (!EncodedFile.ContainsFile(sc, dirName, fileName))
                return LogInfo.LogErrorMessage(logs, $"Encoded file [{dirName}\\{fileName}] not found in script [{sc.RealPath}].");

            // Filter dest path
            if (!StringEscaper.PathSecurityCheck(destDir, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            if (!Directory.Exists(destDir)) // DestDir already exists
            {
                if (File.Exists(destDir)) // Error, cannot proceed
                    return LogInfo.LogErrorMessage(logs, $"File [{destDir}] is not a directory.");

                Directory.CreateDirectory(destDir);
            }

            s.MainViewModel.SetBuildCommandProgress("ExtractFile Progress", 1);
            try
            {
                object progressLock = new object();
                IProgress<double> progress = new Progress<double>(x =>
                {
                    lock (progressLock)
                    {
                        s.MainViewModel.BuildCommandProgressValue = x;
                        if (x < EncodedFile.Base64ReportFactor)
                        { // [Stage 1] Base64
                            s.MainViewModel.BuildCommandProgressText = $"Reading \"{fileName}\" from script\r\n({x * 100:0.0}%)";
                        }
                        else
                        { // [Stage 2] Decompress
                            s.MainViewModel.BuildCommandProgressText = $"Decompressing \"{fileName}\"\r\n({x * 100:0.0}%)";
                        }
                    }
                });

                string destPath = Path.Combine(destDir, fileName);
                using (FileStream fs = new FileStream(destPath, FileMode.Create, FileAccess.Write))
                {
                    EncodedFile.ExtractFile(sc, dirName, fileName, fs, progress);
                }
            }
            finally
            {
                s.MainViewModel.ResetBuildCommandProgress();
            }

            logs.Add(new LogInfo(LogState.Success, $"Encoded file [{fileName}] was extracted to [{destDir}]"));

            return logs;
        }

        public static List<LogInfo> ExtractAndRun(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_ExtractAndRun info = cmd.Info.Cast<CodeInfo_ExtractAndRun>();

            string scriptFile = StringEscaper.Preprocess(s, info.ScriptFile);
            string dirName = StringEscaper.Preprocess(s, info.DirName);
            string fileName = StringEscaper.Preprocess(s, info.FileName);

            Script sc = Engine.GetScriptInstance(s, s.CurrentScript.RealPath, scriptFile, out _);

            // Check if encoded file exist
            if (!EncodedFile.ContainsFile(sc, dirName, fileName))
                return LogInfo.LogErrorMessage(logs, $"Encoded file [{dirName}\\{fileName}] not found in script [{sc.RealPath}].");

            string tempDir = FileHelper.GetTempDir();
            string tempPath = Path.Combine(tempDir, fileName);

            s.MainViewModel.SetBuildCommandProgress("ExtractAndRun Progress", 1);
            try
            {
                object progressLock = new object();
                IProgress<double> progress = new Progress<double>(x =>
                {
                    lock (progressLock)
                    {
                        s.MainViewModel.BuildCommandProgressValue = x;
                        if (x < EncodedFile.Base64ReportFactor)
                        { // [Stage 1] Base64
                            s.MainViewModel.BuildCommandProgressText = $"Reading \"{fileName}\" from script\r\n({x * 100:0.0}%)";
                        }
                        else
                        { // [Stage 2] Decompress
                            s.MainViewModel.BuildCommandProgressText = $"Decompressing \"{fileName}\"\r\n({x * 100:0.0}%)";
                        }
                    }
                });

                using (FileStream fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
                {
                    EncodedFile.ExtractFile(sc, dirName, info.FileName, fs, progress);
                }
            }
            finally
            {
                s.MainViewModel.ResetBuildCommandProgress();
            }

            string? _params = null;
            using (Process proc = new Process())
            {
                proc.EnableRaisingEvents = true;
                proc.StartInfo = new ProcessStartInfo
                {
                    FileName = tempPath,
                    UseShellExecute = true,
                };

                if (!string.IsNullOrEmpty(info.Params))
                {
                    _params = StringEscaper.Preprocess(s, info.Params);
                    proc.StartInfo.Arguments = _params;
                }

                proc.Exited += (object? sender, EventArgs e) =>
                {
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, true);

                    // ReSharper disable once AccessToDisposedClosure
                    proc.Dispose();
                };

                proc.Start();
            }

            if (_params == null)
                logs.Add(new LogInfo(LogState.Success, $"Extracted and executed [{fileName}]"));
            else
                logs.Add(new LogInfo(LogState.Success, $"Extracted and executed [{fileName} {_params}]"));

            return logs;
        }

        public static List<LogInfo> ExtractAllFiles(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_ExtractAllFiles info = cmd.Info.Cast<CodeInfo_ExtractAllFiles>();

            string scriptFile = StringEscaper.Preprocess(s, info.ScriptFile);
            string dirName = StringEscaper.Preprocess(s, info.DirName);
            string destDir = StringEscaper.Preprocess(s, info.DestDir);

            Script sc = Engine.GetScriptInstance(s, s.CurrentScript.RealPath, scriptFile, out _);

            // Check if encoded file exist
            if (!EncodedFile.ContainsFolder(sc, dirName))
                return LogInfo.LogErrorMessage(logs, $"Encoded folder [{dirName}] not found in script [{sc.RealPath}].");

            // Filter dest path
            if (!StringEscaper.PathSecurityCheck(destDir, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            string[] dirs = sc.Sections[ScriptSection.Names.EncodedFolders].Lines;
            if (!dirs.Any(d => d.Equals(dirName, StringComparison.OrdinalIgnoreCase)))
                return LogInfo.LogErrorMessage(logs, $"Directory [{dirName}] not exists in [{scriptFile}]");

            if (!Directory.Exists(destDir))
            {
                if (File.Exists(destDir))
                    return LogInfo.LogErrorMessage(logs, $"File [{destDir}] is not a directory");
                Directory.CreateDirectory(destDir);
            }

            string[] lines = sc.Sections[dirName].Lines;
            Dictionary<string, string> fileDict = IniReadWriter.ParseIniLinesIniStyle(lines);
            int fileCount = fileDict.Count;

            s.MainViewModel.SetBuildCommandProgress("ExtractAndRun Progress", fileCount);
            try
            {
                int i = 0;
                foreach (string file in fileDict.Keys)
                {
                    object progressLock = new object();
                    IProgress<double> progress = new Progress<double>(x =>
                    {
                        lock (progressLock)
                        {
                            s.MainViewModel.BuildCommandProgressText = $"Decompressing \"{file}\"\r\n({(x + i) * 100 / fileCount:0.0}%)";
                            s.MainViewModel.BuildCommandProgressValue = x + i;
                        }
                    });

                    using (FileStream fs = new FileStream(Path.Combine(destDir, file), FileMode.Create, FileAccess.Write))
                    {
                        EncodedFile.ExtractFile(sc, dirName, file, fs, progress);
                    }

                    i += 1;
                }
            }
            finally
            {
                s.MainViewModel.ResetBuildCommandProgress();
            }

            logs.Add(new LogInfo(LogState.Success, $"Encoded folder [{dirName}] was extracted to [{destDir}]"));

            return logs;
        }

        public static List<LogInfo> Encode(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_Encode info = cmd.Info.Cast<CodeInfo_Encode>();

            string scriptFile = StringEscaper.Preprocess(s, info.ScriptFile);
            string dirName = StringEscaper.Preprocess(s, info.DirName);
            string filePath = StringEscaper.Preprocess(s, info.FilePath);

            EncodeMode mode = EncodeMode.ZLib;
            if (info.Compression != null)
            {
                string encodeModeStr = StringEscaper.Preprocess(s, info.Compression);
                if (encodeModeStr.Equals("None", StringComparison.OrdinalIgnoreCase))
                    mode = EncodeMode.Raw;
                else if (encodeModeStr.Equals("Deflate", StringComparison.OrdinalIgnoreCase))
                    mode = EncodeMode.ZLib;
                else if (encodeModeStr.Equals("LZMA2", StringComparison.OrdinalIgnoreCase))
                    mode = EncodeMode.XZ;
                else
                    return LogInfo.LogErrorMessage(logs, $"[{encodeModeStr}] is invalid compression");
            }

            Script sc = Engine.GetScriptInstance(s, s.CurrentScript.RealPath, scriptFile, out _);

            // Check srcFileName contains wildcard
            if (filePath.IndexOfAny(new char[] { '*', '?' }) == -1)
            { // No Wildcard
                s.MainViewModel.SetBuildCommandProgress("Encode Progress", 1);
                try
                {
                    object progressLock = new object();
                    IProgress<double> progress = new Progress<double>(x =>
                    {
                        lock (progressLock)
                        {
                            s.MainViewModel.BuildCommandProgressValue = x;
                            if (x < EncodedFile.CompReportFactor) // [Stage 1] Compress
                                s.MainViewModel.BuildCommandProgressText = $"Compressing \"{filePath}\"\r\n({x * 100:0.0}%)";
                            else // [Stage 2] Base64
                                s.MainViewModel.BuildCommandProgressText = $"Writing \"{filePath}\" to script\r\n({x * 100:0.0}%)";
                        }
                    });

                    EncodedFile.AttachFile(sc, dirName, Path.GetFileName(filePath), filePath, mode, progress);

                    logs.Add(new LogInfo(LogState.Success, $"[{filePath}] was encoded into [{sc.RealPath}]", cmd));
                }
                finally
                {
                    s.MainViewModel.ResetBuildCommandProgress();
                }
            }
            else
            { // With Wildcard
                // Use FileHelper.GetDirNameEx to prevent ArgumentException of Directory.GetFiles
                string srcDirToFind = FileHelper.GetDirNameEx(filePath);
                string[] files = Directory.GetFiles(srcDirToFind, Path.GetFileName(filePath));

                // No file will be compressed
                if (files.Length == 0)
                {
                    logs.Add(new LogInfo(LogState.Warning, $"Files matching wildcard [{filePath}] were not found", cmd));
                    return logs;
                }

                s.MainViewModel.SetBuildCommandProgress("Encode Progress", files.Length);
                try
                {
                    int i = 0;
                    object progressLock = new object();
                    IProgress<double> progress = new Progress<double>(x =>
                    {
                        lock (progressLock)
                        {
                            s.MainViewModel.BuildCommandProgressText = $"Attaching {filePath}...\r\n({(x + i) * 100:0.0}%)";
                            s.MainViewModel.BuildCommandProgressValue = x + i;
                        }
                    });

                    // One or more file will be copied
                    logs.Add(new LogInfo(LogState.Success, $"[{filePath}] will be encoded into [{sc.RealPath}]", cmd));
                    foreach (string f in files)
                    {
                        EncodedFile.AttachFile(sc, dirName, Path.GetFileName(f), f, mode, progress);
                        logs.Add(new LogInfo(LogState.Success, $"[{f}] encoded ({i + 1}/{files.Length})", cmd));

                        i += 1;
                    }

                    logs.Add(new LogInfo(LogState.Success, $"[{files.Length}] files copied", cmd));
                }
                finally
                {
                    s.MainViewModel.ResetBuildCommandProgress();
                }
            }

            return logs;
        }
    }
}
