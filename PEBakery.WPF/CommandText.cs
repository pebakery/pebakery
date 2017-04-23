using PEBakery.Exceptions;
using PEBakery.Helper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PEBakery.Core
{
    public static class CommandText
    {
        public static List<LogInfo> TXTAddLine(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_TXTAddLine info = cmd.Info as CodeInfo_TXTAddLine;
            if (info == null)
                throw new InternalCodeInfoException();

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string line = StringEscaper.Preprocess(s, info.Line);

            // Detect encoding of text
            // If text does not exists, create blank file
            Encoding encoding = Encoding.UTF8;
            if (File.Exists(fileName))
                encoding = FileHelper.DetectTextEncoding(fileName);
            else
                FileHelper.WriteTextBOM(new FileStream(fileName, FileMode.Create, FileAccess.Write), Encoding.UTF8).Close();

            if (info.Mode == TXTAddLineMode.Prepend)
            {
                string temp = FileHelper.CreateTempFile();
                using (StreamReader reader = new StreamReader(new FileStream(fileName, FileMode.Open, FileAccess.Read), encoding))
                using (StreamWriter writer = new StreamWriter(new FileStream(temp, FileMode.Create, FileAccess.Write), encoding))
                {
                    writer.WriteLine(line);
                    string lineFromSrc;
                    while ((lineFromSrc = reader.ReadLine()) != null)
                        writer.WriteLine(lineFromSrc);
                    reader.Close();
                    writer.Close();
                }
                FileHelper.FileReplaceEx(temp, fileName);

                logs.Add(new LogInfo(LogState.Success, $"Prepened [{line}] to [{info.FileName}]", cmd));
            }
            else if (info.Mode == TXTAddLineMode.Append)
            {
                File.AppendAllText(fileName, line + "\r\n", encoding);
                logs.Add(new LogInfo(LogState.Success, $"Appended [{line}] to [{info.FileName}]", cmd));
            }
            else if (info.Mode == TXTAddLineMode.Place)
            { // In Place mode, placeLineNum starts from 1;
                int count = 1;
                string temp = FileHelper.CreateTempFile();
                using (StreamReader reader = new StreamReader(new FileStream(fileName, FileMode.Open, FileAccess.Read), encoding))
                using (StreamWriter writer = new StreamWriter(new FileStream(temp, FileMode.Create, FileAccess.Write), encoding))
                {
                    string lineFromSrc;
                    while ((lineFromSrc = reader.ReadLine()) != null)
                    {
                        if (count == info.LineNum)
                            writer.WriteLine(line);
                        writer.WriteLine(lineFromSrc);
                        count++;
                    }
                    reader.Close();
                    writer.Close();
                }
                FileHelper.FileReplaceEx(temp, fileName);

                logs.Add(new LogInfo(LogState.Success, $"Placed [{line}] to [{info.LineNum}]th row of [{info.FileName}]", cmd));
            }

            return logs;
        }

        public static List<LogInfo> TXTReplace(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_TXTReplace info = cmd.Info as CodeInfo_TXTReplace;
            if (info == null)
                throw new InternalCodeInfoException();

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string toBeReplaced = StringEscaper.Preprocess(s, info.ToBeReplaced);
            string replaceWith = StringEscaper.Preprocess(s, info.ReplaceWith);

            if (File.Exists(fileName) == false)
                throw new ExecuteErrorException($"File [{fileName}] not exists");
            Encoding encoding = FileHelper.DetectTextEncoding(fileName);

            string temp = FileHelper.CreateTempFile();
            int i = 0;
            using (StreamReader reader = new StreamReader(new FileStream(fileName, FileMode.Open, FileAccess.Read), encoding))
            using (StreamWriter writer = new StreamWriter(new FileStream(temp, FileMode.Create, FileAccess.Write), encoding))
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
            FileHelper.FileReplaceEx(temp, fileName);

            logs.Add(new LogInfo(LogState.Success, $"Replaced [{toBeReplaced}] with [{replaceWith}] [{i}] times", cmd));

            return logs;
        }

        public static List<LogInfo> TXTDelLine(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_TXTDelLine info = cmd.Info as CodeInfo_TXTDelLine;
            if (info == null)
                throw new InternalCodeInfoException();

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string deleteIfBeginWith = StringEscaper.Preprocess(s, info.DeleteIfBeginWith);
            if (File.Exists(fileName) == false)
                throw new ExecuteErrorException($"File [{fileName}] not exists");
            Encoding encoding = FileHelper.DetectTextEncoding(fileName);

            int i = 0;
            string temp = FileHelper.CreateTempFile();
            using (StreamReader reader = new StreamReader(new FileStream(fileName, FileMode.Open, FileAccess.Read), encoding))
            using (StreamWriter writer = new StreamWriter(new FileStream(temp, FileMode.Create, FileAccess.Write), encoding))
            {
                string lineFromSrc;
                while ((lineFromSrc = reader.ReadLine()) != null)
                {
                    if (lineFromSrc.StartsWith(deleteIfBeginWith))
                    {
                        i++;
                        continue;
                    }                        
                    writer.WriteLine(lineFromSrc);
                }
                reader.Close();
                writer.Close();
            }
            FileHelper.FileReplaceEx(temp, fileName);

            logs.Add(new LogInfo(LogState.Success, $"Deleted [{i}] lines", cmd));

            return logs;
        }

        public static List<LogInfo> TXTDelSpaces(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_TXTDelSpaces info = cmd.Info as CodeInfo_TXTDelSpaces;
            if (info == null)
                throw new InternalCodeInfoException();

            string fileName = StringEscaper.Preprocess(s, info.FileName);

            if (File.Exists(fileName) == false)
                throw new ExecuteErrorException($"File [{fileName}] not exists");
            Encoding encoding = FileHelper.DetectTextEncoding(fileName);

            int i = 0;
            string temp = FileHelper.CreateTempFile();
            using (StreamReader reader = new StreamReader(new FileStream(fileName, FileMode.Open, FileAccess.Read), encoding))
            using (StreamWriter writer = new StreamWriter(new FileStream(temp, FileMode.Create, FileAccess.Write), encoding))
            {
                string lineFromSrc;
                while ((lineFromSrc = reader.ReadLine()) != null)
                {
                    int count = FileHelper.CountStringOccurrences(lineFromSrc, " ");
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
            FileHelper.FileReplaceEx(temp, fileName);

            logs.Add(new LogInfo(LogState.Success, $"Deleted [{i}] spaces", cmd));

            return logs;
        }

        public static List<LogInfo> TXTDelEmptyLines(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_TXTDelEmptyLines info = cmd.Info as CodeInfo_TXTDelEmptyLines;
            if (info == null)
                throw new InternalCodeInfoException();

            string fileName = StringEscaper.Preprocess(s, info.FileName);

            if (File.Exists(fileName) == false)
                throw new ExecuteErrorException($"File [{fileName}] not exists");
            Encoding encoding = FileHelper.DetectTextEncoding(fileName);

            int i = 0;
            string temp = FileHelper.CreateTempFile();
            using (StreamReader reader = new StreamReader(new FileStream(fileName, FileMode.Open, FileAccess.Read), encoding))
            using (StreamWriter writer = new StreamWriter(new FileStream(temp, FileMode.Create, FileAccess.Write), encoding))
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
            FileHelper.FileReplaceEx(temp, fileName);

            logs.Add(new LogInfo(LogState.Success, $"Deleted [{i}] empty lines", cmd));

            return logs;
        }
    }
}
