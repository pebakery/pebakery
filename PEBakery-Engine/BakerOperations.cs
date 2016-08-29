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
    /// Implementation of commands
    /// </summary>
    public static class BakerOperations
    {
        public static bool OptionalOperandBitMask(OptionalOperand flags, OptionalOperand mask)
        {
            return ((flags & mask) != OptionalOperand.None);
        }

        // Start of Opertaions
        // FileCopy,<SrcFileName>,<DestFileName>[,PRESERVE][,NOWARN][,SHOW][,NOREC]
        public static int FileCopy(string srcFileName, string destFileName, OptionalOperand optional)
        {
            try
            {
                bool preserve = OptionalOperandBitMask(optional, OptionalOperand.PRESERVE);
                bool noWarn = OptionalOperandBitMask(optional, OptionalOperand.NOWARN);
                bool show = OptionalOperandBitMask(optional, OptionalOperand.SHOW);
                bool noRec = OptionalOperandBitMask(optional, OptionalOperand.NOREC);

                if (preserve)
                    File.Copy(srcFileName, destFileName, false);
                else
                    File.Copy(srcFileName, destFileName, true);

            }
            catch (Exception e)
            {
                Console.WriteLine("[ERR]\n{0}", e.ToString());
            }

            return 0;
        }

        // FileDelete,<FileName>,[,NOWARN][,NOREC]
        public static int FileDelete(string fileName, OptionalOperand optional)
        {
            try
            {
                bool noWarn = OptionalOperandBitMask(optional, OptionalOperand.NOWARN);
                bool noRec = OptionalOperandBitMask(optional, OptionalOperand.NOREC);

                File.Delete(fileName);
            }
            catch (Exception e)
            {
                Console.WriteLine("[ERR]\n{0}", e.ToString());
            }

            return 0;
        }

        // FileRename,<srcFileName>,<destFileName>
        public static int FileRename(string srcFileName, string destFileName)
        {
            try
            {
                File.Move(srcFileName, destFileName);
            }
            catch (Exception e)
            {
                Console.WriteLine("[ERR]\n{0}", e.ToString());
            }

            return 0;
        }

        // FileCreateBlank,<FileName>[,PRESERVE][,NOWARN]
        public static int FileCreateBlank(string fileName, OptionalOperand optional)
        {
            try
            {
                bool preserve = OptionalOperandBitMask(optional, OptionalOperand.PRESERVE);
                bool noWarn = OptionalOperandBitMask(optional, OptionalOperand.NOWARN);

                StreamWriter sw = File.CreateText(fileName);
                sw.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine("[ERR]\n{0}", e.ToString());
            }

            return 0;
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
        public static int TXTAddLine(string fileName, string line, string mode)
        { // TXTAddLine,<FileName>,<Line>,<Mode>
            
            return 0;
        }
    }
}