using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections;

namespace PEBakery_Engine
{
    /// <summary>
    /// Exception used in BakerOperations
    /// </summary>
    public class FileDoesNotExistException : Exception
    {
        public FileDoesNotExistException() { }
        public FileDoesNotExistException(string message) : base(message) { }
        public FileDoesNotExistException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Exception used in BakerOperations
    /// </summary>
    public class InternalUnknownException : Exception
    {
        public InternalUnknownException() { }
        public InternalUnknownException(string message) : base(message) { }
        public InternalUnknownException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Implementation of commands
    /// </summary>
    public static class BakerOperations
    {
        /// <summary>
        /// FileCopy
        /// </summary>
        /// <param name="operand"></param>
        /// <returns></returns>
        public static LogInfo FileCopy(string[] operands)
        { // FileCopy,<SrcFileName>,<DestFileName>[,PRESERVE][,NOWARN][,SHOW][,NOREC]
            try
            {
                // Must-have operand : 2
                if (!(2 <= operands.Length))
                    throw new InvalidOperandException("Necessary operands does not exist");

                // Check if srcFileName exists
                if (File.Exists(operands[0]) == false)
                    throw new FileDoesNotExistException(String.Format("{0} does not exist", operands[0]));

                bool preserve = false;
                bool noWarn = false;
                bool show = false;
                bool noRec = false;

                foreach (string operand in operands)
                {
                    switch (operand.ToUpper())
                    {
                        case "PRESERVE":
                            preserve = true;
                            break;
                        case "NOWARN":
                            noWarn = true;
                            break;
                        case "SHOW":
                            show = true;
                            break;
                        case "NOREC":
                            noRec = true;
                            break;
                    }
                }
                
                if (preserve)
                    File.Copy(operands[0], operands[1], false);
                else
                    File.Copy(operands[0], operands[1], true);
            }
            catch (Exception e)
            {
                Console.WriteLine("[ERR]\n{0}", e.ToString());
            }

            return new LogInfo(null, "FileCopy", LogState.Success);
        }

        
        public static LogInfo FileDelete(string[] operands)
        { // FileDelete,<FileName>,[,NOWARN][,NOREC]
            try
            {
                // Must-have operand : 1
                if (!(1 <= operands.Length))
                    throw new InvalidOperandException("Necessary operands does not exist");

                // Check if srcFileName exists
                if (File.Exists(operands[0]) == false)
                    throw new FileDoesNotExistException(String.Format("{0} does not exist", operands[0]));

                bool noWarn = false;
                bool noRec = false;

                foreach (string operand in operands)
                {
                    switch (operand.ToUpper())
                    {
                        case "NOWARN":
                            noWarn = true;
                            break;
                        case "NOREC":
                            noRec = true;
                            break;
                    }
                }

                File.Delete(operands[0]);
            }
            catch (Exception e)
            {
                Console.WriteLine("[ERR]\n{0}", e.ToString());
            }

            return new LogInfo(null, "FileDelete", LogState.Success);
        }

        public static LogInfo FileRename(string[] operands)
        { // FileRename,<srcFileName>,<destFileName>
            try
            {
                // Must-have operand : 2
                if (!(1 <= operands.Length))
                    throw new InvalidOperandException("Necessary operands does not exist");

                // Check if srcFileName exists
                if (File.Exists(operands[0]) == false)
                    throw new FileDoesNotExistException(String.Format("{0} does not exist", operands[0]));

                File.Move(operands[0], operands[1]);
            }
            catch (Exception e)
            {
                Console.WriteLine("[ERR]\n{0}", e.ToString());
            }

            return new LogInfo(null, "FileRename", LogState.Success);
        }

        public static LogInfo FileCreateBlank(string[] operands)
        { // FileCreateBlank,<FileName>[,PRESERVE][,NOWARN][,UTF8 | UTF16LE | UTF16BE | ANSI]
            try
            {
                // Must-have operand : 1
                if (!(1 <= operands.Length))
                    throw new InvalidOperandException("Necessary operands does not exist");

                string fileName = operands[0];
                bool preserve = false;
                bool noWarn = false;
                Encoding encoding = null;

                foreach (string operand in operands)
                {
                    switch (operand.ToUpper())
                    {
                        case "PRESERVE":
                            preserve = true;
                            break;
                        case "NOWARN":
                            noWarn = true;
                            break;
                        case "UTF8":
                            if (encoding == null)
                                encoding = Encoding.UTF8;
                            else
                                throw new InvalidOperandException("Encoding operand only can be used once");
                            break;
                        case "UTF16":
                            if (encoding == null)
                                encoding = Encoding.Unicode;
                            else
                                throw new InvalidOperandException("Encoding operand only can be used once");
                            break;
                        case "UTF16LE":
                            if (encoding == null)
                                encoding = Encoding.Unicode;
                            else
                                throw new InvalidOperandException("Encoding operand only can be used once");
                            break;
                        case "UTF16BE":
                            if (encoding == null)
                                encoding = Encoding.BigEndianUnicode;
                            else
                                throw new InvalidOperandException("Encoding operand only can be used once");
                            break;
                        case "ANSI":
                            if (encoding == null)
                                encoding = Encoding.Default;
                            else
                                throw new InvalidOperandException("Encoding operand only can be used once");
                            break;
                    }
                }

                // Default Encoding
                if (encoding == null)
                    encoding = Encoding.UTF8;

                FileStream fs = new FileStream(operands[0], preserve ? FileMode.CreateNew : FileMode.Create, FileAccess.Write, FileShare.Write);
                Helper.WriteTextBOM(fs, encoding).Close();
            }
            catch (Exception e)
            {
                Console.WriteLine("[ERR]\n{0}", e.ToString());
            }

            return new LogInfo(null, "FileCreateBlank", LogState.Success);
        }

        /* 
         * Text Manipulation
         */

        /// <summary>
        /// Add line to text file
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="line"></param>
        /// <param name="mode"></param>
        /// <returns></returns>
        public static LogInfo TXTAddLine(string[] operands)
        { // TXTAddLine,<FileName>,<Line>,<Mode>
            // Mode : Prepend / Append / Place,LineNum
            try
            {
                // Must-have operand : 3-4
                if (!(3 <= operands.Length || operands.Length <= 4))
                    throw new InvalidOperandException("Necessary operands does not exist");

                string fileName = operands[0];
                string line = operands[1];
                int mode = 1;
                int placeLineNum = 0;

                if (string.Equals(operands[2], "Prepend", StringComparison.OrdinalIgnoreCase))
                {
                    mode = 0;
                    if (4 <= operands.Length)
                        throw new InvalidOperandException("Too many operands");
                }
                else if (string.Equals(operands[2], "Append", StringComparison.OrdinalIgnoreCase))
                {
                    mode = 1;
                    if (4 <= operands.Length)
                        throw new InvalidOperandException("Too many operands");
                }
                else if (string.Equals(operands[2], "Place", StringComparison.OrdinalIgnoreCase))
                {
                    mode = 2;
                    if (5 <= operands.Length)
                        throw new InvalidOperandException("Too many operands");
                    else if (operands.Length == 3)
                        throw new InvalidOperandException("Not enough operands");
                    placeLineNum = int.Parse(operands[3]);
                    if (placeLineNum <= 0) // In Place mode, placeLineNum starts from 1;
                        throw new InvalidOperandException("Invalid LineNum value. LineNum starts from 1.");
                }
                else
                {
                    throw new InvalidOperandException("Invalid mode of TXTADDLine");
                }

                // Detect encoding of text
                // If text does not exists, create blank file
                Encoding encoding = Encoding.UTF8;
                if (File.Exists(fileName))
                    encoding = Helper.DetectTextEncoding(fileName);
                else
                    FileCreateBlank(new string[] { fileName, "UTF8" });

                if (mode == 0) // Prepend
                {
                    string rawText = Helper.ReadTextFile(fileName);
                    StreamWriter sw = new StreamWriter(new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.Write), encoding);
                    sw.WriteLine(line);
                    sw.Write(rawText);
                    sw.Close();
                }
                else if (mode == 1) // Append
                {
                    File.AppendAllText(fileName, line, encoding);
                }
                else if (mode == 2) // Place
                { // In Place mode, placeLineNum starts from 1;
                    int count = 1;
                    int offset = 0;
                    string rawText = Helper.ReadTextFile(fileName);
                    // Get offset of start of (placeLineNum)'th line
                    while ((count < placeLineNum) && (offset = rawText.IndexOf("\r\n", offset)) != -1)
                    {
                        offset += 2;
                        count++;
                    }
                    if (offset == -1) // placeLineNum is bigger than text file's line num, so works as 'Append'
                        offset = rawText.Length;
                    // Write to file
                    StreamWriter sw = new StreamWriter(new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.Write), encoding);
                    sw.Write(rawText.Substring(0, offset));
                    sw.WriteLine(line);
                    sw.Write(rawText.Substring(offset));
                    sw.Close();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("[ERR]\n{0}", e.ToString());
            }

            return new LogInfo(null, "TXTAddLine", LogState.Success);
        }
    }
}