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
    /// Implementation of commands
    /// </summary>
    public partial class BakerEngine
    {
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

            return new LogInfo(cmd, "TXTAddLine", LogState.Success);
        }
    }
}