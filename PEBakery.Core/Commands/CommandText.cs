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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PEBakery.Core.Commands
{
    public static class CommandText
    {
        public static List<LogInfo> TXTAddLine(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_TXTAddLine info = (CodeInfo_TXTAddLine)cmd.Info;

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

            // Detect encoding of text. If text does not exists, create blank file (ANSI)
            Encoding encoding;
            if (File.Exists(fileName))
                encoding = EncodingHelper.SmartDetectEncoding(fileName, line);
            else
                encoding = EncodingHelper.DefaultAnsi;

            if (mode == TXTAddLineMode.Prepend)
            {
                string tempPath = FileHelper.GetTempFile();
                using (StreamReader r = new StreamReader(fileName, encoding, false))
                using (StreamWriter w = new StreamWriter(tempPath, false, encoding))
                {
                    w.WriteLine(line);

                    string? lineFromSrc;
                    while ((lineFromSrc = r.ReadLine()) != null)
                        w.WriteLine(lineFromSrc);
                }
                FileHelper.FileReplaceEx(tempPath, fileName);

                logs.Add(new LogInfo(LogState.Success, $"Prepended [{line}] to [{fileName}]", cmd));
            }
            else if (mode == TXTAddLineMode.Append)
            {
                bool newLineExist = true;
                if (File.Exists(fileName))
                {
                    using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        long bomLen = EncodingHelper.TextBomLength(fs);
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
            else
            {
                throw new InvalidOperationException("Internal Logic Error at TXTAddLine");
            }

            return logs;
        }

        public static List<LogInfo> TXTAddLineOp(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>(8);

            CodeInfo_TXTAddLineOp infoOp = (CodeInfo_TXTAddLineOp)cmd.Info;

            CodeInfo_TXTAddLine firstInfo = infoOp.Infos[0];
            string fileName = StringEscaper.Preprocess(s, firstInfo.FileName);
            string modeStr = StringEscaper.Preprocess(s, firstInfo.Mode);
            TXTAddLineMode mode;
            if (modeStr.Equals("Append", StringComparison.OrdinalIgnoreCase))
                mode = TXTAddLineMode.Append;
            else if (modeStr.Equals("Prepend", StringComparison.OrdinalIgnoreCase))
                mode = TXTAddLineMode.Prepend;
            else
                throw new ExecuteException($"Mode [{modeStr}] must be one of [Append, Prepend]");

            if (!StringEscaper.PathSecurityCheck(fileName, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            // Detect encoding of text. If text does not exists, create blank file (ANSI)
            Encoding encoding;
            if (File.Exists(fileName))
                encoding = EncodingHelper.SmartDetectEncoding(fileName, infoOp.Infos.Select(x => x.Line));
            else
                encoding = EncodingHelper.DefaultAnsi;

            string linesToWrite;
            if (mode == TXTAddLineMode.Prepend)
            {
                string tempPath = FileHelper.GetTempFile();
                using (StreamReader r = new StreamReader(fileName, encoding, false))
                using (StreamWriter w = new StreamWriter(tempPath, false, encoding))
                {
                    StringBuilder b = new StringBuilder();
                    List<CodeInfo_TXTAddLine> infos = infoOp.Infos;
                    infos.Reverse();
                    foreach (CodeInfo_TXTAddLine subInfo in infos)
                        b.AppendLine(StringEscaper.Preprocess(s, subInfo.Line));
                    linesToWrite = b.ToString();

                    w.Write(linesToWrite);
                    w.Write(r.ReadToEnd());
                }
                FileHelper.FileReplaceEx(tempPath, fileName);

                logs.Add(new LogInfo(LogState.Success, $"Lines prepended to [{fileName}] : \r\n{linesToWrite}", cmd));
            }
            else if (mode == TXTAddLineMode.Append)
            {
                StringBuilder b = new StringBuilder();
                foreach (CodeInfo_TXTAddLine subInfo in infoOp.Infos)
                    b.AppendLine(StringEscaper.Preprocess(s, subInfo.Line));

                linesToWrite = b.ToString();

                bool newLineExist = true;
                if (File.Exists(fileName))
                {
                    using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        long bomLen = EncodingHelper.TextBomLength(fs);
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
            else
            {
                throw new InvalidOperationException("Internal Logic Error at TXTAddLine");
            }

            return logs;
        }

        public static List<LogInfo> TXTReplace(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_TXTReplace info = (CodeInfo_TXTReplace)cmd.Info;

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string oldStr = StringEscaper.Preprocess(s, info.OldStr);
            string newStr = StringEscaper.Preprocess(s, info.NewStr);

            if (!StringEscaper.PathSecurityCheck(fileName, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            if (!File.Exists(fileName))
                return LogInfo.LogErrorMessage(logs, $"File [{fileName}] does not exist");

            // Detect encoding of text. If text does not exists, create blank file (ANSI)
            Encoding encoding;
            if (File.Exists(fileName))
            {
                encoding = EncodingHelper.SmartDetectEncoding(fileName, () =>
                {
                    return EncodingHelper.IsActiveCodePageCompatible(info.OldStr) &&
                        EncodingHelper.IsActiveCodePageCompatible(info.NewStr);
                });
            }
            else
            {
                encoding = EncodingHelper.DefaultAnsi;
            }

            string tempPath = FileHelper.GetTempFile();
            string txtStr;
            using (StreamReader r = new StreamReader(fileName, encoding, false))
            {
                txtStr = r.ReadToEnd();
            }

            using (StreamWriter w = new StreamWriter(tempPath, false, encoding))
            {
                txtStr = StringHelper.ReplaceEx(txtStr, oldStr, newStr, StringComparison.OrdinalIgnoreCase);
                w.Write(txtStr);
            }
            FileHelper.FileReplaceEx(tempPath, fileName);

            logs.Add(new LogInfo(LogState.Success, $"Replaced [{oldStr}] with [{newStr}]", cmd));

            return logs;
        }

        public static List<LogInfo> TXTReplaceOp(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>(8);

            CodeInfo_TXTReplaceOp infoOp = (CodeInfo_TXTReplaceOp)cmd.Info;

            CodeInfo_TXTReplace firstInfo = infoOp.Infos[0];
            string fileName = StringEscaper.Preprocess(s, firstInfo.FileName);

            if (!StringEscaper.PathSecurityCheck(fileName, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            if (!File.Exists(fileName))
                return LogInfo.LogErrorMessage(logs, $"File [{fileName}] does not exist");

            List<(CodeCommand, string, string)> prepReplace = new List<(CodeCommand, string, string)>();
            // foreach (CodeInfo_TXTReplace info in infoOp.Infos)
            foreach (CodeCommand subCmd in infoOp.Cmds)
            {
                CodeInfo_TXTReplace info = (CodeInfo_TXTReplace)subCmd.Info;

                string oldStr = StringEscaper.Preprocess(s, info.OldStr);
                string newStr = StringEscaper.Preprocess(s, info.NewStr);

                prepReplace.Add((subCmd, oldStr, newStr));
            }

            // Detect encoding of text. If text does not exists, create blank file (ANSI)
            Encoding encoding;
            if (File.Exists(fileName))
            {
                encoding = EncodingHelper.SmartDetectEncoding(fileName, () =>
                {
                    return infoOp.Infos.All(x => EncodingHelper.IsActiveCodePageCompatible(x.OldStr)) &&
                        infoOp.Infos.All(x => EncodingHelper.IsActiveCodePageCompatible(x.NewStr));
                });
            }
            else
            {
                encoding = EncodingHelper.DefaultAnsi;
            }

            string tempPath = FileHelper.GetTempFile();
            string txtStr;
            using (StreamReader r = new StreamReader(fileName, encoding, false))
            {
                txtStr = r.ReadToEnd();
            }

            foreach ((CodeCommand subCmd, string oldStr, string newStr) in prepReplace)
            {
                txtStr = StringHelper.ReplaceEx(txtStr, oldStr, newStr, StringComparison.OrdinalIgnoreCase);
                logs.Add(new LogInfo(LogState.Success, $"Replaced [{oldStr}] with [{newStr}]", subCmd));
            }

            using (StreamWriter w = new StreamWriter(tempPath, false, encoding))
            {
                w.Write(txtStr);
            }
            logs.Add(new LogInfo(LogState.Success, $"Replaced [{prepReplace.Count}] strings from [{fileName}]"));

            FileHelper.FileReplaceEx(tempPath, fileName);

            return logs;
        }

        public static List<LogInfo> TXTDelLine(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_TXTDelLine info = (CodeInfo_TXTDelLine)cmd.Info;

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string deleteLine = StringEscaper.Preprocess(s, info.DeleteLine);

            if (!StringEscaper.PathSecurityCheck(fileName, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            if (!File.Exists(fileName))
                return LogInfo.LogErrorMessage(logs, $"File [{fileName}] does not exist");

            // Detect encoding of text. 
            Encoding encoding = EncodingHelper.SmartDetectEncoding(fileName, deleteLine);

            int count = 0;
            string tempPath = FileHelper.GetTempFile();
            using (StreamReader r = new StreamReader(fileName, encoding, false))
            using (StreamWriter w = new StreamWriter(tempPath, false, encoding))
            {
                string? srcLine;
                while ((srcLine = r.ReadLine()) != null)
                {
                    // Strange enough, WB082 treat [deleteLine] as case sensitive string.
                    if (srcLine.StartsWith(deleteLine, StringComparison.Ordinal))
                        count++;
                    else
                        w.WriteLine(srcLine);
                }
            }
            FileHelper.FileReplaceEx(tempPath, fileName);

            logs.Add(new LogInfo(LogState.Success, $"Line [{deleteLine}] deleted from [{fileName}]"));
            logs.Add(new LogInfo(LogState.Success, $"Deleted [{count}] lines"));

            return logs;
        }

        public static List<LogInfo> TXTDelLineOp(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_TXTDelLineOp infoOp = (CodeInfo_TXTDelLineOp)cmd.Info;

            CodeInfo_TXTDelLine firstInfo = infoOp.Infos[0];
            string fileName = StringEscaper.Preprocess(s, firstInfo.FileName);

            if (!StringEscaper.PathSecurityCheck(fileName, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            if (!File.Exists(fileName))
                return LogInfo.LogErrorMessage(logs, $"File [{fileName}] does not exist");

            List<(CodeCommand, string)> prepDeleteLine = new List<(CodeCommand, string)>(infoOp.Cmds.Count);
            foreach (CodeCommand subCmd in infoOp.Cmds)
            {
                CodeInfo_TXTDelLine info = (CodeInfo_TXTDelLine)subCmd.Info;

                string deleteLine = StringEscaper.Preprocess(s, info.DeleteLine);
                prepDeleteLine.Add((subCmd, deleteLine));
            }

            // Detect encoding of text. 
            Encoding encoding = EncodingHelper.SmartDetectEncoding(fileName, prepDeleteLine.Select(t => t.Item2));

            int count = 0;
            string tempPath = FileHelper.GetTempFile();
            using (StreamReader r = new StreamReader(fileName, encoding, false))
            using (StreamWriter w = new StreamWriter(tempPath, false, encoding))
            {
                string? srcLine;
                while ((srcLine = r.ReadLine()) != null)
                {
                    bool writeLine = true;
                    foreach ((CodeCommand _, string deleteLine) in prepDeleteLine)
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
                        w.WriteLine(srcLine);
                }
            }
            FileHelper.FileReplaceEx(tempPath, fileName);

            foreach ((CodeCommand subCmd, string deleteLine) in prepDeleteLine)
                logs.Add(new LogInfo(LogState.Success, $"Line [{deleteLine}] deleted from [{fileName}]", subCmd));
            logs.Add(new LogInfo(LogState.Success, $"Deleted [{count}] lines from [{fileName}]"));

            return logs;
        }

        public static List<LogInfo> TXTDelSpaces(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_TXTDelSpaces info = (CodeInfo_TXTDelSpaces)cmd.Info;

            string fileName = StringEscaper.Preprocess(s, info.FileName);

            if (!StringEscaper.PathSecurityCheck(fileName, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            if (!File.Exists(fileName))
                return LogInfo.LogErrorMessage(logs, $"File [{fileName}] does not exist");

            Encoding encoding = EncodingHelper.DetectEncoding(fileName);

            int linesTrimmed = 0;
            string tempPath = FileHelper.GetTempFile();
            using (StreamReader sr = new StreamReader(fileName, encoding, false))
            using (StreamWriter sw = new StreamWriter(tempPath, false, encoding))
            {
                string? srcLine;
                while ((srcLine = sr.ReadLine()) != null)
                {
                    int count = StringHelper.CountSubStr(srcLine, " ");
                    if (0 < count)
                    {
                        srcLine = srcLine.Trim();
                        if (!StringHelper.CountSubStr(srcLine, " ").Equals(count)) //only count lines that we actually trimmed
                            linesTrimmed++;
                    }
                    sw.WriteLine(srcLine);
                }
            }
            FileHelper.FileReplaceEx(tempPath, fileName);

            logs.Add(new LogInfo(LogState.Success, $"Deleted leading and trailing spaces from [{linesTrimmed}] lines"));

            return logs;
        }

        public static List<LogInfo> TXTDelEmptyLines(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_TXTDelEmptyLines info = (CodeInfo_TXTDelEmptyLines)cmd.Info;

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            if (!StringEscaper.PathSecurityCheck(fileName, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            if (!File.Exists(fileName))
                return LogInfo.LogErrorMessage(logs, $"File [{fileName}] does not exist");

            Encoding encoding = EncodingHelper.DetectEncoding(fileName);

            int i = 0;
            string tempPath = FileHelper.GetTempFile();
            using (StreamReader r = new StreamReader(fileName, encoding, false))
            using (StreamWriter w = new StreamWriter(tempPath, false, encoding))
            {
                string? lineFromSrc;
                while ((lineFromSrc = r.ReadLine()) != null)
                {
                    if (lineFromSrc.Length == 0)
                        i++;
                    else
                        w.WriteLine(lineFromSrc);
                }
            }
            FileHelper.FileReplaceEx(tempPath, fileName);

            logs.Add(new LogInfo(LogState.Success, $"Deleted [{i}] empty lines"));

            return logs;
        }
    }
}
