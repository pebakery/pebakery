using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections;

namespace PEBakery_Engine
{
    using VariableDictionary = Dictionary<string, string>;

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
    public partial class BakerEngine
    {
        /// <summary>
        /// FileCopy
        /// </summary>
        /// <param name="operand"></param>
        /// <returns></returns>
        public LogInfo FileCopy(BakerCommand cmd)
        { // FileCopy,<SrcFileName>,<DestFileName>[,PRESERVE][,NOWARN][,SHOW][,NOREC]
            try
            {
                // Must-have operand : 2
                if (!(2 <= cmd.Operands.Length))
                    throw new InvalidOperandException("Necessary operands does not exist");

                string srcFileName = cmd.Operands[0];
                string destFileName = cmd.Operands[1];

                // Check if srcFileName exists
                if (File.Exists(srcFileName) == false)
                    throw new FileDoesNotExistException(String.Format("{0} does not exist", srcFileName));

                bool preserve = false;
                bool noWarn = false;
                bool show = false;
                bool noRec = false;

                foreach (string operand in cmd.Operands)
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
                    File.Copy(srcFileName, destFileName, false);
                else
                    File.Copy(srcFileName, destFileName, true);
            }
            catch (Exception e)
            {
                Console.WriteLine("[ERR]\n{0}", e.ToString());
            }

            return new LogInfo(cmd.RawCode, "FileCopy", LogState.Success);
        }

        
        public LogInfo FileDelete(BakerCommand cmd)
        { // FileDelete,<FileName>,[,NOWARN][,NOREC]
            try
            {
                // Must-have operand : 1
                if (!(1 <= cmd.Operands.Length))
                    throw new InvalidOperandException("Necessary operands does not exist");

                string fileName = cmd.Operands[0];

                // Check if srcFileName exists
                if (File.Exists(fileName) == false)
                    throw new FileDoesNotExistException(String.Format("{0} does not exist", fileName));

                bool noWarn = false;
                bool noRec = false;

                foreach (string operand in cmd.Operands)
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

                File.Delete(fileName);
            }
            catch (Exception e)
            {
                Console.WriteLine("[ERR]\n{0}", e.ToString());
            }

            return new LogInfo(cmd.RawCode, "FileDelete", LogState.Success);
        }

        public LogInfo FileRename(BakerCommand cmd)
        { // FileRename,<srcFileName>,<destFileName>
            try
            {
                // Must-have operand : 2
                if (!(1 <= cmd.Operands.Length))
                    throw new InvalidOperandException("Necessary operands does not exist");

                string srcFileName = cmd.Operands[0];
                string destFileName = cmd.Operands[1];

                // Check if srcFileName exists
                if (File.Exists(srcFileName) == false)
                    throw new FileDoesNotExistException(String.Format("{0} does not exist", srcFileName));

                File.Move(srcFileName, destFileName);
            }
            catch (Exception e)
            {
                Console.WriteLine("[ERR]\n{0}", e.ToString());
            }

            return new LogInfo(cmd.RawCode, "FileRename", LogState.Success);
        }

        public LogInfo FileCreateBlank(BakerCommand cmd)
        { // FileCreateBlank,<FileName>[,PRESERVE][,NOWARN][,UTF8 | UTF16LE | UTF16BE | ANSI]
            try
            {
                // Must-have operand : 1
                if (!(1 <= cmd.Operands.Length))
                    throw new InvalidOperandException("Necessary operands does not exist");

                string fileName = cmd.Operands[0];
                bool preserve = false;
                bool noWarn = false;
                Encoding encoding = null;

                foreach (string operand in cmd.Operands)
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

                FileStream fs = new FileStream(fileName, preserve ? FileMode.CreateNew : FileMode.Create, FileAccess.Write, FileShare.Write);
                Helper.WriteTextBOM(fs, encoding).Close();
            }
            catch (Exception e)
            {
                Console.WriteLine("[ERR]\n{0}", e.ToString());
            }

            return new LogInfo(cmd.RawCode, "FileCreateBlank", LogState.Success);
        }

        /* 
         * Text Manipulation
         */

        /// <summary>
        /// Add line to text file
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public LogInfo TXTAddLine(BakerCommand cmd)
        { // TXTAddLine,<FileName>,<Line>,<Mode>
            // Mode : Prepend / Append / Place,LineNum
            try
            {
                // Must-have operand : 3-4
                if (!(3 <= cmd.Operands.Length || cmd.Operands.Length <= 4))
                    throw new InvalidOperandException("Necessary operands does not exist");

                string fileName = cmd.Operands[0];
                string line = cmd.Operands[1];
                int mode = 1;
                int placeLineNum = 0;

                if (string.Equals(cmd.Operands[2], "Prepend", StringComparison.OrdinalIgnoreCase))
                {
                    mode = 0;
                    if (4 <= cmd.Operands.Length)
                        throw new InvalidOperandException("Too many operands");
                }
                else if (string.Equals(cmd.Operands[2], "Append", StringComparison.OrdinalIgnoreCase))
                {
                    mode = 1;
                    if (4 <= cmd.Operands.Length)
                        throw new InvalidOperandException("Too many operands");
                }
                else if (string.Equals(cmd.Operands[2], "Place", StringComparison.OrdinalIgnoreCase))
                {
                    mode = 2;
                    if (5 <= cmd.Operands.Length)
                        throw new InvalidOperandException("Too many operands");
                    else if (cmd.Operands.Length == 3)
                        throw new InvalidOperandException("Not enough operands");
                    placeLineNum = int.Parse(cmd.Operands[3]);
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
                    Helper.WriteTextBOM(new FileStream(fileName, FileMode.Create, FileAccess.Write), Encoding.UTF8);

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
                    File.AppendAllText(fileName, line + "\r\n", encoding);
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

            return new LogInfo(cmd.RawCode, "TXTAddLine", LogState.Success);
        }

        /// <summary>
        /// Set variables
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public LogInfo Set(BakerCommand cmd)
        { // Set,<VarName>,<VarValue>[,GLOBAL | PERMANENT] 
            string varName;
            string varValue;
            bool global = false;
            bool permanent = false;
            VariableDictionary targetVar;

            // Must-have operand : 2-3
            if (cmd.Operands.Length == 3)
            {
                switch (cmd.Operands[2].ToUpper())
                {
                    case "GLOBAL":
                        global = true;
                        break;
                    case "PERMANENT":
                        permanent = true;
                        break;
                    default:
                        throw new InvalidOperandException("Invalid operand : " + cmd.Operands[2]);
                }
            }
            else if (cmd.Operands.Length != 2)
                throw new InvalidOperandException("Necessary operands does not exist");

            varName = cmd.Operands[0].Trim(new char[] { '%' });
            varValue = cmd.Operands[1];

            if (global || permanent)
                targetVar = this.globalVars;
            else
                targetVar = this.localVars;

            if (targetVar.ContainsKey(varName))
                targetVar[varName] = varValue;
            else
                targetVar.Add(varName, varValue);

            return new LogInfo(cmd.RawCode, "Set", LogState.Success);
        }
    }
}