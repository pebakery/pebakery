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
using PEBakery.IniLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PEBakery.Core.Commands
{
    public static class CommandPlugin
    {
        /*
         * WB082 Behavior
         * ExtractFile : DestDir는 반드시 Directory 이름으로 간주한다. 없으면 생성한다.
         * Ex) (...),README.txt,%BaseDir%\Temp\Hello
         *   -> Hello가 존재하지 않을 경우 : Hello 디렉토리를 만들고 그 밑에 압축해제
         *   -> Hello가 파일일 경우 : 실패
         *   -> Hello가 디렉토리일 경우 : 디렉터리 밑에 압축해제
         * 
         * ExtractAllFiles
         * Ex) (...),Fonts,%BaseDir%\Temp\Hello
         *   -> Hello가 존재하지 않을 경우 : 실패
         *   -> Hello가 파일일 경우 : 실패
         *   -> Hello가 디렉토리일 경우 : 디렉터리 밑에 압축해제
         * 
         */

        public static List<LogInfo> ExtractFile(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_ExtractFile));
            CodeInfo_ExtractFile info = cmd.Info as CodeInfo_ExtractFile;

            string pluginFile = StringEscaper.Preprocess(s, info.PluginFile);
            string dirName = StringEscaper.Preprocess(s, info.DirName);
            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string destDir = StringEscaper.Preprocess(s, info.DestDir); // Should be directory name

            s.MainViewModel.BuildCommandProgressBarValue = 300;

            Plugin p = Engine.GetPluginInstance(s, cmd, s.CurrentPlugin.FullPath, pluginFile, out bool inCurrentPlugin);

            if (StringEscaper.PathSecurityCheck(destDir, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            s.MainViewModel.BuildCommandProgressBarValue = 600;

            if (!Directory.Exists(destDir)) // DestDir already exists
            {
                if (File.Exists(destDir)) // Error, cannot proceed
                {
                    logs.Add(new LogInfo(LogState.Error, $"File [{destDir}] is not a directory."));
                    return logs;
                }
                else
                {
                    Directory.CreateDirectory(destDir);
                }
            }

            string destPath = Path.Combine(destDir, fileName);
            using (MemoryStream ms = EncodedFile.ExtractFile(p, dirName, fileName))
            using (FileStream fs = new FileStream(destPath, FileMode.Create, FileAccess.Write))
            {
                ms.Position = 0;
                ms.CopyTo(fs);
                ms.Close();
                fs.Close();
            }

            s.MainViewModel.BuildCommandProgressBarValue = 900;

            logs.Add(new LogInfo(LogState.Success, $"Encoded file [{fileName}] extracted to [{destDir}]"));

            return logs;
        }

        public static List<LogInfo> ExtractAndRun(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_ExtractAndRun));
            CodeInfo_ExtractAndRun info = cmd.Info as CodeInfo_ExtractAndRun;

            string pluginFile = StringEscaper.Preprocess(s, info.PluginFile);
            string dirName = StringEscaper.Preprocess(s, info.DirName);
            string fileName = StringEscaper.Preprocess(s, info.FileName);
            List<string> parameters = StringEscaper.Preprocess(s, info.Params);


            s.MainViewModel.BuildCommandProgressBarValue = 200;

            Plugin p = Engine.GetPluginInstance(s, cmd, s.CurrentPlugin.FullPath, pluginFile, out bool inCurrentPlugin);

            string destPath = Path.GetTempFileName();
            if (StringEscaper.PathSecurityCheck(destPath, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            s.MainViewModel.BuildCommandProgressBarValue = 400;

            using (MemoryStream ms = EncodedFile.ExtractFile(p, dirName, info.FileName))
            using (FileStream fs = new FileStream(destPath, FileMode.Create, FileAccess.Write))
            {
                ms.Position = 0;
                ms.CopyTo(fs);
                ms.Close();
                fs.Close();
            }

            s.MainViewModel.BuildCommandProgressBarValue = 600;

            Process proc = new Process();
            proc.StartInfo.FileName = destPath;
            proc.StartInfo.UseShellExecute = true;
            proc.StartInfo.Verb = "Open";
            proc.Start();

            s.MainViewModel.BuildCommandProgressBarValue = 800;

            logs.Add(new LogInfo(LogState.Success, $"Encoded file [{fileName}] extracted and executed"));

            return logs;
        }

        public static List<LogInfo> ExtractAllFiles(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_ExtractAllFiles));
            CodeInfo_ExtractAllFiles info = cmd.Info as CodeInfo_ExtractAllFiles;

            string pluginFile = StringEscaper.Preprocess(s, info.PluginFile);
            string dirName = StringEscaper.Preprocess(s, info.DirName);
            string destDir = StringEscaper.Preprocess(s, info.DestDir);

            s.MainViewModel.BuildCommandProgressBarValue = 100;

            Plugin p = Engine.GetPluginInstance(s, cmd, s.CurrentPlugin.FullPath, pluginFile, out bool inCurrentPlugin);

            if (StringEscaper.PathSecurityCheck(destDir, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            s.MainViewModel.BuildCommandProgressBarValue = 200;

            List<string> dirs = p.Sections["EncodedFolders"].Lines;
            bool dirNameValid = dirs.Any(d => d.Equals(dirName, StringComparison.OrdinalIgnoreCase));
            if (dirNameValid == false)
                throw new ExecuteException($"Directory [{dirName}] not exists in [{pluginFile}]");

            if (!Directory.Exists(destDir))
            {
                if (File.Exists(destDir))
                {
                    logs.Add(new LogInfo(LogState.Error, $"File [{destDir}] is not directory, DestDir must be directory"));
                    return logs;
                }
                else
                {
                    logs.Add(new LogInfo(LogState.Error, $"Directory [{destDir}] does not exists, DestDir must exists"));
                    return logs;
                }
            }

            int i = 0;
            List<string> lines = p.Sections[dirName].Lines;
            Dictionary<string, string> fileDict = Ini.ParseIniLinesIniStyle(lines);
            foreach (string file in fileDict.Keys)
            {
                using (MemoryStream ms = EncodedFile.ExtractFile(p, dirName, file))
                using (FileStream fs = new FileStream(Path.Combine(destDir, file), FileMode.Create, FileAccess.Write))
                {
                    ms.Position = 0;
                    ms.CopyTo(fs);
                    ms.Close();
                    fs.Close();
                }

                s.MainViewModel.BuildCommandProgressBarValue = 200 + ((fileDict.Count * i / fileDict.Count) * 800);
            }

            logs.Add(new LogInfo(LogState.Success, $"Encoded folder [{dirName}] extracted to [{destDir}]"));

            return logs;
        }

        public static List<LogInfo> Encode(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Encode));
            CodeInfo_Encode info = cmd.Info as CodeInfo_Encode;

            string pluginFile = StringEscaper.Preprocess(s, info.PluginFile);
            string dirName = StringEscaper.Preprocess(s, info.DirName);
            string filePath = StringEscaper.Preprocess(s, info.FilePath);

            Plugin p = Engine.GetPluginInstance(s, cmd, s.CurrentPlugin.FullPath, pluginFile, out bool inCurrentPlugin);

            s.MainViewModel.BuildCommandProgressBarValue = 200;

            // Check srcFileName contains wildcard
            if (filePath.IndexOfAny(new char[] { '*', '?' }) == -1)
            { // No Wildcard
                EncodedFile.AttachFile(p, dirName, Path.GetFileName(filePath), filePath);
                s.MainViewModel.BuildCommandProgressBarValue = 600;
                logs.Add(new LogInfo(LogState.Success, $"[{filePath}] encoded into [{p.FullPath}]", cmd));
            }
            else
            { // With Wildcard
                // Use FileHelper.GetDirNameEx to prevent ArgumentException of Directory.GetFiles
                string srcDirToFind = FileHelper.GetDirNameEx(filePath);
                string[] files = Directory.GetFiles(srcDirToFind, Path.GetFileName(filePath));

                if (0 < files.Length)
                { // One or more file will be copied
                    logs.Add(new LogInfo(LogState.Success, $"[{filePath}] will be encoded into [{p.FullPath}]", cmd));
                    for (int i = 0; i < files.Length; i++)
                    {
                        EncodedFile.AttachFile(p, dirName, Path.GetFileName(files[i]), files[i]);
                        s.MainViewModel.BuildCommandProgressBarValue = 200 + (800 * (i + 1) / files.Length);
                        logs.Add(new LogInfo(LogState.Success, $"[{files[i]}] encoded ({i + 1}/{files.Length})", cmd));
                    }

                    logs.Add(new LogInfo(LogState.Success, $"[{files.Length}] files copied", cmd));
                }
                else
                { // No file will be copied
                    s.MainViewModel.BuildCommandProgressBarValue = 600;
                    logs.Add(new LogInfo(LogState.Warning, $"Files match wildcard [{filePath}] not found", cmd));
                }
            }

            return logs;
        }
    }
}
