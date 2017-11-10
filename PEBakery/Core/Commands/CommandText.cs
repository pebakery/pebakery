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

namespace PEBakery.Core.Commands
{
    public static class CommandText
    {
        public static List<LogInfo> TXTAddLine(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_TXTAddLine));
            CodeInfo_TXTAddLine info = cmd.Info as CodeInfo_TXTAddLine;

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

            s.MainViewModel.BuildCommandProgressBarValue = 100;

            if (StringEscaper.PathSecurityCheck(fileName, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            s.MainViewModel.BuildCommandProgressBarValue = 200;

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
                    reader.Close();
                    writer.Close();
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
                        byte[] lastChar = new byte[2];
                        if (2 <= fs.Length)
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

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_TXTAddLineOp));
            CodeInfo_TXTAddLineOp infoOp = cmd.Info as CodeInfo_TXTAddLineOp;

            string fileName = StringEscaper.Preprocess(s, infoOp.InfoList[0].FileName);
            string modeStr = StringEscaper.Preprocess(s, infoOp.InfoList[0].Mode);
            TXTAddLineMode mode;
            if (modeStr.Equals("Append", StringComparison.OrdinalIgnoreCase))
                mode = TXTAddLineMode.Append;
            else if (modeStr.Equals("Prepend", StringComparison.OrdinalIgnoreCase))
                mode = TXTAddLineMode.Prepend;
            else
                throw new ExecuteException($"Mode [{modeStr}] must be one of [Append, Prepend]");

            s.MainViewModel.BuildCommandProgressBarValue = 100;

            if (StringEscaper.PathSecurityCheck(fileName, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            s.MainViewModel.BuildCommandProgressBarValue = 300;

            // Detect encoding of text
            // If text does not exists, create blank file
            Encoding encoding = Encoding.Default;
            if (File.Exists(fileName))
                encoding = FileHelper.DetectTextEncoding(fileName);

            s.MainViewModel.BuildCommandProgressBarValue = 500;

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
                        byte[] lastChar = new byte[2];
                        if (2 <= fs.Length)
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

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_TXTReplace));
            CodeInfo_TXTReplace info = cmd.Info as CodeInfo_TXTReplace;

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string toBeReplaced = StringEscaper.Preprocess(s, info.ToBeReplaced);
            string replaceWith = StringEscaper.Preprocess(s, info.ReplaceWith);

            if (StringEscaper.PathSecurityCheck(fileName, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            if (File.Exists(fileName) == false)
                throw new ExecuteException($"File [{fileName}] not exists");
            Encoding encoding = FileHelper.DetectTextEncoding(fileName);

            int i = 0;
            string tempPath = Path.GetTempFileName();
            using (StreamReader reader = new StreamReader(fileName, encoding))
            using (StreamWriter writer = new StreamWriter(tempPath, false, encoding))
            {
                string lineFromSrc;
                while ((lineFromSrc = reader.ReadLine()) != null)
                {
                    lineFromSrc = lineFromSrc.Replace(toBeReplaced, replaceWith);
                    writer.WriteLine(lineFromSrc);
                    i++;
                }
                reader.Close();
                writer.Close();
            }
            FileHelper.FileReplaceEx(tempPath, fileName);

            logs.Add(new LogInfo(LogState.Success, $"Replaced [{toBeReplaced}] with [{replaceWith}] [{i}] times", cmd));

            return logs;
        }

        public static List<LogInfo> TXTDelLine(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_TXTDelLine));
            CodeInfo_TXTDelLine info = cmd.Info as CodeInfo_TXTDelLine;

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string deleteIfBeginWith = StringEscaper.Preprocess(s, info.DeleteIfBeginWith);

            if (StringEscaper.PathSecurityCheck(fileName, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            if (File.Exists(fileName) == false)
                throw new ExecuteException($"File [{fileName}] not exists");
            Encoding encoding = FileHelper.DetectTextEncoding(fileName);

            int i = 0;
            string tempPath = Path.GetTempFileName();
            using (StreamReader reader = new StreamReader(fileName, encoding))
            using (StreamWriter writer = new StreamWriter(tempPath, false, encoding))
            {
                string lineFromSrc;
                while ((lineFromSrc = reader.ReadLine()) != null)
                {
                    if (lineFromSrc.StartsWith(deleteIfBeginWith, StringComparison.OrdinalIgnoreCase))
                    {
                        i++;
                        continue;
                    }                        
                    writer.WriteLine(lineFromSrc);
                }
                reader.Close();
                writer.Close();
            }
            FileHelper.FileReplaceEx(tempPath, fileName);

            logs.Add(new LogInfo(LogState.Success, $"Deleted [{i}] lines from [{fileName}]", cmd));

            return logs;
        }

        public static List<LogInfo> TXTDelLineOp(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_TXTDelLineOp));
            CodeInfo_TXTDelLineOp infoOp = cmd.Info as CodeInfo_TXTDelLineOp;

            string fileName = StringEscaper.Preprocess(s, infoOp.InfoList[0].FileName);

            if (StringEscaper.PathSecurityCheck(fileName, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            if (File.Exists(fileName) == false)
                throw new ExecuteException($"File [{fileName}] not exists");

            List<string> prepDeleteIfBeginWith = new List<string>();
            foreach (CodeInfo_TXTDelLine info in infoOp.InfoList)
            {
                string deleteIfBeginWith = StringEscaper.Preprocess(s, info.DeleteIfBeginWith);
                prepDeleteIfBeginWith.Add(deleteIfBeginWith);
            }

            Encoding encoding = FileHelper.DetectTextEncoding(fileName);
            
            int count = 0;
            string tempPath = Path.GetTempFileName();
            using (StreamReader reader = new StreamReader(fileName, encoding))
            using (StreamWriter writer = new StreamWriter(tempPath, false, encoding))
            {
                string lineFromSrc;
                while ((lineFromSrc = reader.ReadLine()) != null)
                {
                    bool writeLine = true;
                    foreach (string deleteIfBeginWith in prepDeleteIfBeginWith)
                    {
                        if (lineFromSrc.StartsWith(deleteIfBeginWith, StringComparison.OrdinalIgnoreCase))
                        {
                            writeLine = false;
                            count++;
                            break;
                        }
                    }
                    
                    if (writeLine)
                        writer.WriteLine(lineFromSrc);
                }
                reader.Close();
                writer.Close();
            }
            FileHelper.FileReplaceEx(tempPath, fileName);

            logs.Add(new LogInfo(LogState.Success, $"Deleted [{count}] lines from [{fileName}]", cmd));

            return logs;
        }

        public static List<LogInfo> TXTDelSpaces(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_TXTDelSpaces));
            CodeInfo_TXTDelSpaces info = cmd.Info as CodeInfo_TXTDelSpaces;

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            if (StringEscaper.PathSecurityCheck(fileName, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            if (File.Exists(fileName) == false)
                throw new ExecuteException($"File [{fileName}] not exists");
            Encoding encoding = FileHelper.DetectTextEncoding(fileName);

            int i = 0;
            string tempPath = Path.GetTempFileName();
            using (StreamReader reader = new StreamReader(fileName, encoding))
            using (StreamWriter writer = new StreamWriter(tempPath, false, encoding))
            {
                string lineFromSrc;
                while ((lineFromSrc = reader.ReadLine()) != null)
                {
                    int count = StringHelper.CountOccurrences(lineFromSrc, " ");
                    if (0 < count)
                    {
                        i++;
                        lineFromSrc = lineFromSrc.Replace(" ", string.Empty);
                    }
                    writer.WriteLine(lineFromSrc);
                }
                reader.Close();
                writer.Close();
            }
            FileHelper.FileReplaceEx(tempPath, fileName);

            logs.Add(new LogInfo(LogState.Success, $"Deleted [{i}] spaces", cmd));

            return logs;
        }

        public static List<LogInfo> TXTDelEmptyLines(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_TXTDelEmptyLines));
            CodeInfo_TXTDelEmptyLines info = cmd.Info as CodeInfo_TXTDelEmptyLines;

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            if (StringEscaper.PathSecurityCheck(fileName, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            if (File.Exists(fileName) == false)
                throw new ExecuteException($"File [{fileName}] not exists");
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
                reader.Close();
                writer.Close();
            }
            FileHelper.FileReplaceEx(tempPath, fileName);

            logs.Add(new LogInfo(LogState.Success, $"Deleted [{i}] empty lines", cmd));

            return logs;
        }
    }
}
