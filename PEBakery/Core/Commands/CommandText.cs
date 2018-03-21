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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PEBakery.Core.Commands
{
    [SuppressMessage("ReSharper", "LocalizableElement")]
    public static class CommandText
    {
        public static List<LogInfo> TXTAddLine(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_TXTAddLine), "Invalid CodeInfo");
            CodeInfo_TXTAddLine info = cmd.Info as CodeInfo_TXTAddLine;
            Debug.Assert(info != null, "Invalid CodeInfo");

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string line = StringEscaper.Preprocess(s, info.Line);
            string modeStr = StringEscaper.Preprocess(s, info.Mode);
            TXTAddLineMode mode;
            if (modeStr.Equals("Append", StringComparison.OrdinalIgnoreCase))
                mode = TXTAddLineMode.Append;
            else if (info.Mode.Equals("Prepend", StringComparison.OrdinalIgnoreCase))
                mode = TXTAddLineMode.Prepend;
            else
                throw new ExecuteException($"Mode [{modeStr}] must be one of [Append, Prepend]");

            if (!StringEscaper.PathSecurityCheck(fileName, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            // Detect encoding of text
            // If text does not exists, create blank file
            Encoding encoding = Encoding.Default;
            if (File.Exists(fileName))
                encoding = FileHelper.DetectTextEncoding(fileName);

            if (mode == TXTAddLineMode.Prepend)
            {
                string tempPath = Path.GetTempFileName();
                using (StreamReader reader = new StreamReader(fileName, encoding))
                using (StreamWriter writer = new StreamWriter(tempPath, false, encoding))
                {
                    writer.WriteLine(line);
                    string lineFromSrc;
                    while ((lineFromSrc = reader.ReadLine()) != null)
                        writer.WriteLine(lineFromSrc);
                }
                FileHelper.FileReplaceEx(tempPath, fileName);

                logs.Add(new LogInfo(LogState.Success, $"Prepened [{line}] to [{fileName}]", cmd));
            }
            else if (mode == TXTAddLineMode.Append)
            {
                bool newLineExist = true;
                if (File.Exists(fileName))
                {
                    using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        long bomLen = FileHelper.TextBOMLength(fs);
                        byte[] lastChar = new byte[2];
                        if (2 + bomLen <= fs.Length)
                        {
                            fs.Position = fs.Length - 2;
                            fs.Read(lastChar, 0, 2);
                            if (lastChar[0] != '\r' || lastChar[1] != '\n')
                                newLineExist = false;
                        }
                    }
                }

                if (newLineExist)
                    File.AppendAllText(fileName, line + "\r\n", encoding);
                else
                    File.AppendAllText(fileName, "\r\n" + line + "\r\n", encoding);
                logs.Add(new LogInfo(LogState.Success, $"Appended [{line}] to [{fileName}]", cmd));
            }

            return logs;
        }

        public static List<LogInfo> TXTAddLineOp(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_TXTAddLineOp), "Invalid CodeInfo");
            CodeInfo_TXTAddLineOp infoOp = cmd.Info as CodeInfo_TXTAddLineOp;
            Debug.Assert(infoOp != null, "Invalid CodeInfo");

            string fileName = StringEscaper.Preprocess(s, infoOp.InfoList[0].FileName);
            string modeStr = StringEscaper.Preprocess(s, infoOp.InfoList[0].Mode);
            TXTAddLineMode mode;
            if (modeStr.Equals("Append", StringComparison.OrdinalIgnoreCase))
                mode = TXTAddLineMode.Append;
            else if (modeStr.Equals("Prepend", StringComparison.OrdinalIgnoreCase))
                mode = TXTAddLineMode.Prepend;
            else
                throw new ExecuteException($"Mode [{modeStr}] must be one of [Append, Prepend]");

            if (!StringEscaper.PathSecurityCheck(fileName, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            // Detect encoding of text
            // If text does not exists, create blank file
            Encoding encoding = Encoding.Default;
            if (File.Exists(fileName))
                encoding = FileHelper.DetectTextEncoding(fileName);

            string linesToWrite;
            if (mode == TXTAddLineMode.Prepend)
            {
                string tempPath = Path.GetTempFileName();
                using (StreamReader reader = new StreamReader(fileName, encoding))
                using (StreamWriter writer = new StreamWriter(tempPath, false, encoding))
                {
                    StringBuilder b = new StringBuilder();
                    for (int i = infoOp.InfoList.Count - 1; 0 <= i; i--)
                        b.AppendLine(StringEscaper.Preprocess(s, infoOp.InfoList[i].Line));
                    linesToWrite = b.ToString();

                    writer.Write(linesToWrite);
                    writer.Write(reader.ReadToEnd());
                }
                FileHelper.FileReplaceEx(tempPath, fileName);

                logs.Add(new LogInfo(LogState.Success, $"Lines prepened to [{fileName}] : \r\n{linesToWrite}", cmd));
            }
            else if (mode == TXTAddLineMode.Append)
            {
                StringBuilder b = new StringBuilder();
                for (int i = 0; i < infoOp.InfoList.Count; i++)
                    b.AppendLine(StringEscaper.Preprocess(s, infoOp.InfoList[i].Line));
                linesToWrite = b.ToString();

                bool newLineExist = true;
                if (File.Exists(fileName))
                {
                    using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        long bomLen = FileHelper.TextBOMLength(fs);
                        byte[] lastChar = new byte[2];
                        if (2 + bomLen <= fs.Length)
                        {
                            fs.Position = fs.Length - 2;
                            fs.Read(lastChar, 0, 2);
                            if (lastChar[0] != '\r' || lastChar[1] != '\n')
                                newLineExist = false;
                        }
                    }
                }

                if (newLineExist)
                    File.AppendAllText(fileName, linesToWrite, encoding);
                else
                    File.AppendAllText(fileName, "\r\n" + linesToWrite, encoding);

                logs.Add(new LogInfo(LogState.Success, $"Lines appended to [{fileName}] : \r\n{linesToWrite}", cmd));
            }

            return logs;
        }

        public static List<LogInfo> TXTReplace(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_TXTReplace), "Invalid CodeInfo");
            CodeInfo_TXTReplace info = cmd.Info as CodeInfo_TXTReplace;
            Debug.Assert(info != null, "Invalid CodeInfo");

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string oldStr = StringEscaper.Preprocess(s, info.OldStr);
            string newStr = StringEscaper.Preprocess(s, info.NewStr);

            if (!StringEscaper.PathSecurityCheck(fileName, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            if (File.Exists(fileName) == false)
            {
                logs.Add(new LogInfo(LogState.Error, $"File [{fileName}] does not exist"));
                return logs;
            }

            Encoding encoding = FileHelper.DetectTextEncoding(fileName);

            string tempPath = Path.GetTempFileName();
            using (StreamReader reader = new StreamReader(fileName, encoding))
            using (StreamWriter writer = new StreamWriter(tempPath, false, encoding))
            {
                string str = reader.ReadToEnd();
                str = StringHelper.ReplaceEx(str, oldStr, newStr, StringComparison.OrdinalIgnoreCase);
                writer.Write(str);
            }
            FileHelper.FileReplaceEx(tempPath, fileName);

            logs.Add(new LogInfo(LogState.Success, $"Replaced [{oldStr}] with [{newStr}]", cmd));

            return logs;
        }

        public static List<LogInfo> TXTReplaceOp(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_TXTReplaceOp), "Invalid CodeInfo");
            CodeInfo_TXTReplaceOp infoOp = cmd.Info as CodeInfo_TXTReplaceOp;
            Debug.Assert(infoOp != null, "Invalid CodeInfo");

            string fileName = StringEscaper.Preprocess(s, infoOp.InfoList[0].FileName);

            if (!StringEscaper.PathSecurityCheck(fileName, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            if (!File.Exists(fileName))
                return LogInfo.LogErrorMessage(logs, $"File [{fileName}] does not exist");

            List<Tuple<string, string>> prepReplace = new List<Tuple<string, string>>();
            foreach (CodeInfo_TXTReplace info in infoOp.InfoList)
            {
                string oldStr = StringEscaper.Preprocess(s, info.OldStr);
                string newStr = StringEscaper.Preprocess(s, info.NewStr);
                prepReplace.Add(new Tuple<string, string>(oldStr, newStr));
            }

            Encoding encoding = FileHelper.DetectTextEncoding(fileName);

            string tempPath = Path.GetTempFileName();
            using (StreamReader reader = new StreamReader(fileName, encoding))
            using (StreamWriter writer = new StreamWriter(tempPath, false, encoding))
            {
                string str = reader.ReadToEnd();
                foreach (var tup in prepReplace)
                {
                    string oldStr = tup.Item1;
                    string newStr = tup.Item2;

                    str = StringHelper.ReplaceEx(str, oldStr, newStr, StringComparison.OrdinalIgnoreCase);
                    logs.Add(new LogInfo(LogState.Success, $"Replaced [{oldStr}] with [{newStr}]"));
                }
                writer.Write(str);
            }
            FileHelper.FileReplaceEx(tempPath, fileName);

            return logs;
        }

        public static List<LogInfo> TXTDelLine(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_TXTDelLine), "Invalid CodeInfo");
            CodeInfo_TXTDelLine info = cmd.Info as CodeInfo_TXTDelLine;
            Debug.Assert(info != null, "Invalid CodeInfo");

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string deleteLine = StringEscaper.Preprocess(s, info.DeleteLine);

            if (!StringEscaper.PathSecurityCheck(fileName, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            if (!File.Exists(fileName))
                return LogInfo.LogErrorMessage(logs, $"File [{fileName}] does not exist");

            Encoding encoding = FileHelper.DetectTextEncoding(fileName);

            int i = 0;
            string tempPath = Path.GetTempFileName();
            using (StreamReader reader = new StreamReader(fileName, encoding))
            using (StreamWriter writer = new StreamWriter(tempPath, false, encoding))
            {
                string srcLine;
                while ((srcLine = reader.ReadLine()) != null)
                {
                    // Strange enough, WB082 treat [deleteLine] as case sensitive string.
                    if (srcLine.StartsWith(deleteLine, StringComparison.Ordinal))
                    {
                        i++;
                        continue;
                    }                        
                    writer.WriteLine(srcLine);
                }
            }
            FileHelper.FileReplaceEx(tempPath, fileName);

            logs.Add(new LogInfo(LogState.Success, $"Deleted [{i}] lines from [{fileName}]"));

            return logs;
        }

        public static List<LogInfo> TXTDelLineOp(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_TXTDelLineOp), "Invalid CodeInfo");
            CodeInfo_TXTDelLineOp infoOp = cmd.Info as CodeInfo_TXTDelLineOp;
            Debug.Assert(infoOp != null, "Invalid CodeInfo");

            string fileName = StringEscaper.Preprocess(s, infoOp.InfoList[0].FileName);

            if (!StringEscaper.PathSecurityCheck(fileName, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            if (!File.Exists(fileName))
                return LogInfo.LogErrorMessage(logs, $"File [{fileName}] does not exist");

            List<string> prepDeleteLine = new List<string>();
            foreach (CodeInfo_TXTDelLine info in infoOp.InfoList)
            {
                string deleteLine = StringEscaper.Preprocess(s, info.DeleteLine);
                prepDeleteLine.Add(deleteLine);
            }

            Encoding encoding = FileHelper.DetectTextEncoding(fileName);
            
            int count = 0;
            string tempPath = Path.GetTempFileName();
            using (StreamReader reader = new StreamReader(fileName, encoding))
            using (StreamWriter writer = new StreamWriter(tempPath, false, encoding))
            {
                string srcLine;
                while ((srcLine = reader.ReadLine()) != null)
                {
                    bool writeLine = true;
                    foreach (string deleteLine in prepDeleteLine)
                    {
                        // Strange enough, WB082 treat [deleteLine] as case sensitive string.
                        if (srcLine.StartsWith(deleteLine, StringComparison.Ordinal))
                        {
                            writeLine = false;
                            count++;
                            break;
                        }
                    }
                    
                    if (writeLine)
                        writer.WriteLine(srcLine);
                }
            }
            FileHelper.FileReplaceEx(tempPath, fileName);

            logs.Add(new LogInfo(LogState.Success, $"Deleted [{count}] lines from [{fileName}]"));

            return logs;
        }

        public static List<LogInfo> TXTDelSpaces(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_TXTDelSpaces), "Invalid CodeInfo");
            CodeInfo_TXTDelSpaces info = cmd.Info as CodeInfo_TXTDelSpaces;
            Debug.Assert(info != null, "Invalid CodeInfo");

            string fileName = StringEscaper.Preprocess(s, info.FileName);

            if (!StringEscaper.PathSecurityCheck(fileName, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            if (!File.Exists(fileName))
                return LogInfo.LogErrorMessage(logs, $"File [{fileName}] does not exist");

            Encoding encoding = FileHelper.DetectTextEncoding(fileName);

            int i = 0;
            string tempPath = Path.GetTempFileName();
            using (StreamReader reader = new StreamReader(fileName, encoding))
            using (StreamWriter writer = new StreamWriter(tempPath, false, encoding))
            {
                string srcLine;
                while ((srcLine = reader.ReadLine()) != null)
                {
                    // WB082 delete spaces only if spaces are placed in front of line.
                    // Same with C#'s string.TrimStart().
                    int count = StringHelper.CountOccurrences(srcLine, " ");
                    if (0 < count)
                    {
                        i++;
                        srcLine = srcLine.TrimStart();
                    }
                    writer.WriteLine(srcLine);
                }
            }
            FileHelper.FileReplaceEx(tempPath, fileName);

            logs.Add(new LogInfo(LogState.Success, $"Deleted [{i}] spaces"));

            return logs;
        }

        public static List<LogInfo> TXTDelEmptyLines(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_TXTDelEmptyLines), "Invalid CodeInfo");
            CodeInfo_TXTDelEmptyLines info = cmd.Info as CodeInfo_TXTDelEmptyLines;
            Debug.Assert(info != null, "Invalid CodeInfo");

            string fileName = StringEscaper.Preprocess(s, info.FileName);

            if (!StringEscaper.PathSecurityCheck(fileName, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            if (!File.Exists(fileName))
                return LogInfo.LogErrorMessage(logs, $"File [{fileName}] does not exist");

            Encoding encoding = FileHelper.DetectTextEncoding(fileName);

            int i = 0;
            string tempPath = Path.GetTempFileName();
            using (StreamReader reader = new StreamReader(fileName, encoding))
            using (StreamWriter writer = new StreamWriter(tempPath, false, encoding))
            {
                string lineFromSrc;
                while ((lineFromSrc = reader.ReadLine()) != null)
                {
                    if (lineFromSrc.Equals(string.Empty, StringComparison.Ordinal))
                        i++;
                    else
                        writer.WriteLine(lineFromSrc);
                }
            }
            FileHelper.FileReplaceEx(tempPath, fileName);

            logs.Add(new LogInfo(LogState.Success, $"Deleted [{i}] empty lines"));

            return logs;
        }
    }
}
