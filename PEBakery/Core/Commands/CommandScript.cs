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

    Additional permission under GNU GPL version 3 section 7

    If you modify this program, or any covered work, by linking
    or combining it with external libraries, containing parts
    covered by the terms of various license, the licensors of
    this program grant you additional permission to convey the
    resulting work. An external library is a library which is
    not derived from or based on this program. 
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
    public static class CommandScript
    {
        /*
         * WB082 Behavior
         * ExtractFile : DestDir must be Directory, create if not exists.
         * Ex) (...),README.txt,%BaseDir%\Temp\Hello
         *   -> No Hello : Create direcotry "Hello" and extract files into new directory.
         *   -> Hello is a file : Failure
         *   -> Hello is a directory : Extract files into directory.
         * 
         * ExtractAllFiles
         * Ex) (...),Fonts,%BaseDir%\Temp\Hello
         *   -> No Hello : Failure
         *   -> Hello is a file : Failure
         *   -> Hello is a direcotry : Extract files into directory.
         * 
         */

        public static List<LogInfo> ExtractFile(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_ExtractFile));
            CodeInfo_ExtractFile info = cmd.Info as CodeInfo_ExtractFile;

            string scriptFile = StringEscaper.Preprocess(s, info.ScriptFile);
            string dirName = StringEscaper.Preprocess(s, info.DirName);
            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string destDir = StringEscaper.Preprocess(s, info.DestDir); // Should be directory name

            Script p = Engine.GetScriptInstance(s, cmd, s.CurrentScript.FullPath, scriptFile, out bool inCurrentScript);

            if (StringEscaper.PathSecurityCheck(destDir, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

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
            }

            logs.Add(new LogInfo(LogState.Success, $"Encoded file [{fileName}] was extracted to [{destDir}]"));

            return logs;
        }

        public static List<LogInfo> ExtractAndRun(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_ExtractAndRun));
            CodeInfo_ExtractAndRun info = cmd.Info as CodeInfo_ExtractAndRun;

            string scriptFile = StringEscaper.Preprocess(s, info.ScriptFile);
            string dirName = StringEscaper.Preprocess(s, info.DirName);
            string fileName = StringEscaper.Preprocess(s, info.FileName);
            List<string> parameters = StringEscaper.Preprocess(s, info.Params);

            Script p = Engine.GetScriptInstance(s, cmd, s.CurrentScript.FullPath, scriptFile, out bool inCurrentScript);

            string destPath = Path.GetTempFileName();
            if (StringEscaper.PathSecurityCheck(destPath, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            using (MemoryStream ms = EncodedFile.ExtractFile(p, dirName, info.FileName))
            using (FileStream fs = new FileStream(destPath, FileMode.Create, FileAccess.Write))
            {
                ms.Position = 0;
                ms.CopyTo(fs);
            }

            Process proc = new Process();
            proc.StartInfo.FileName = destPath;
            proc.StartInfo.UseShellExecute = true;
            proc.StartInfo.Verb = "Open";
            proc.Start();

            logs.Add(new LogInfo(LogState.Success, $"Encoded file [{fileName}] was extracted and executed"));

            return logs;
        }

        public static List<LogInfo> ExtractAllFiles(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_ExtractAllFiles));
            CodeInfo_ExtractAllFiles info = cmd.Info as CodeInfo_ExtractAllFiles;

            string scriptFile = StringEscaper.Preprocess(s, info.ScriptFile);
            string dirName = StringEscaper.Preprocess(s, info.DirName);
            string destDir = StringEscaper.Preprocess(s, info.DestDir);

            Script p = Engine.GetScriptInstance(s, cmd, s.CurrentScript.FullPath, scriptFile, out bool inCurrentScript);

            if (StringEscaper.PathSecurityCheck(destDir, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            List<string> dirs = p.Sections["EncodedFolders"].Lines;
            bool dirNameValid = dirs.Any(d => d.Equals(dirName, StringComparison.OrdinalIgnoreCase));
            if (dirNameValid == false)
                throw new ExecuteException($"Directory [{dirName}] not exists in [{scriptFile}]");

            if (!Directory.Exists(destDir))
            {
                if (File.Exists(destDir))
                {
                    logs.Add(new LogInfo(LogState.Error, $"File [{destDir}] is not a directory"));
                    return logs;
                }
                else
                {
                    logs.Add(new LogInfo(LogState.Error, $"Directory [{destDir}] does not exist"));
                    return logs;
                }
            }

            List<string> lines = p.Sections[dirName].Lines;
            Dictionary<string, string> fileDict = Ini.ParseIniLinesIniStyle(lines);
            foreach (string file in fileDict.Keys)
            {
                using (MemoryStream ms = EncodedFile.ExtractFile(p, dirName, file))
                using (FileStream fs = new FileStream(Path.Combine(destDir, file), FileMode.Create, FileAccess.Write))
                {
                    ms.Position = 0;
                    ms.CopyTo(fs);
                }
            }

            logs.Add(new LogInfo(LogState.Success, $"Encoded folder [{dirName}] was extracted to [{destDir}]"));

            return logs;
        }

        public static List<LogInfo> Encode(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Encode));
            CodeInfo_Encode info = cmd.Info as CodeInfo_Encode;

            string scriptFile = StringEscaper.Preprocess(s, info.ScriptFile);
            string dirName = StringEscaper.Preprocess(s, info.DirName);
            string filePath = StringEscaper.Preprocess(s, info.FilePath);

            Script p = Engine.GetScriptInstance(s, cmd, s.CurrentScript.FullPath, scriptFile, out bool inCurrentScript);

            // Check srcFileName contains wildcard
            if (filePath.IndexOfAny(new char[] { '*', '?' }) == -1)
            { // No Wildcard
                EncodedFile.AttachFile(p, dirName, Path.GetFileName(filePath), filePath);
                logs.Add(new LogInfo(LogState.Success, $"[{filePath}] was encoded into [{p.FullPath}]", cmd));
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
                        logs.Add(new LogInfo(LogState.Success, $"[{files[i]}] encoded ({i + 1}/{files.Length})", cmd));
                    }

                    logs.Add(new LogInfo(LogState.Success, $"[{files.Length}] files copied", cmd));
                }
                else
                { // No file will be copied
                    logs.Add(new LogInfo(LogState.Warning, $"Files matching wildcard [{filePath}] were not found", cmd));
                }
            }

            return logs;
        }
    }
}
