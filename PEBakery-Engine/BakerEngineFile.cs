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

    public partial class BakerEngine
    {
        /// <summary>
        /// Exception used in BakerEngine::ParseCommand
        /// </summary>
        public class DestPathNotDirException : Exception
        {
            public DestPathNotDirException() { }
            public DestPathNotDirException(string message) : base(message) { }
            public DestPathNotDirException(string message, Exception inner) : base(message, inner) { }
        }


        /*
         * File Commands
         * Note) Need refactor to support file name longer than 260 length.
         * http://bcl.codeplex.com/releases/view/42783
         * http://alphafs.alphaleonis.com/
         */

        /// <summary>
        /// FileCopy,<SrcFileName>,<DestPath>[,PRESERVE][,NOWARN][,NOREC]
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns>LogInfo[]</returns>
        public LogInfo[] FileCopy(BakerCommand cmd)
        {
            ArrayList logs = new ArrayList();

            // Must-have operand : 2
            if (!(2 <= cmd.Operands.Length))
                throw new InvalidOperandException("Necessary operands does not exist");

            string srcFileName = ExpandVariables(cmd.Operands[0]);
            string rawSrcFileName = cmd.Operands[0];
            string destPath = ExpandVariables(cmd.Operands[1]);
            string rawDestPath = cmd.Operands[1];

            // Check srcFileName contains wildcard
            bool srcContainWildcard = true;
            if (srcFileName.IndexOfAny(new char[] { '*', '?' }) == -1) // No wildcard
                srcContainWildcard = false;
            // Check destPath is directory
            bool destPathExists = false;
            bool destPathIsDir = false;
            if (Directory.Exists(destPath))
            {
                destPathExists = true;
                destPathIsDir = true;
            }
            else if (File.Exists(destPath))
                destPathExists = true;

            bool preserve = false;
            bool noWarn = false;
            bool noRec = false;

            for (int i = 2; i < cmd.Operands.Length; i++)
            {
                string operand = cmd.Operands[i];
                switch (operand.ToUpper())
                {
                    case "PRESERVE":
                        preserve = true;
                        break;
                    case "NOWARN":
                        noWarn = true;
                        break;
                    case "SHOW": // for compability with WB082
                        break;
                    case "NOREC": // no recursive wildcard copy
                        noRec = true;
                        break;
                    default:
                        throw new InvalidOperandException(String.Concat("Invalid operand ", operand));
                }
            }

            try
            {
                if (srcContainWildcard)
                {
                    string[] listToCopy;
                    string srcDirToFind = Helper.GetDirNameEx(srcFileName);
                    if (noRec)
                        listToCopy = Directory.GetFiles(srcDirToFind, Path.GetFileName(srcFileName));
                    else
                        listToCopy = Directory.GetFiles(srcDirToFind, Path.GetFileName(srcFileName), SearchOption.AllDirectories);
                    foreach (string srcWCFileName in listToCopy)
                    {
                        if (destPathIsDir || !destPathExists)
                        {
                            string destPathTail = srcWCFileName.Remove(0, srcDirToFind.Length+1); // 1 for \\
                            string destFullPath = String.Concat(Helper.RemoveLastDirChar(destPath), Path.DirectorySeparatorChar, destPathTail);
                            Directory.CreateDirectory(Path.GetDirectoryName(destFullPath));
                            File.Copy(srcWCFileName, destFullPath, !preserve);
                            logs.Add(new LogInfo(cmd, String.Concat(@"'", srcWCFileName, @"' copied to '", destFullPath, @"'"), LogState.Success));
                        }
                        else
                            throw new DestPathNotDirException("<DestPath> must be directory when using wildcard in <SrcFileName>");
                    }                     
                }
                else
                {
                    if (destPathIsDir)
                    {
                        Directory.CreateDirectory(destPath);
                        string destPathTail = srcFileName.Remove(0, Helper.GetDirNameEx(srcFileName).Length + 1); // 1 for \\
                        string destFullPath = String.Concat(Helper.RemoveLastDirChar(destPath), Path.DirectorySeparatorChar, destPathTail);
                        File.Copy(srcFileName, destFullPath, !preserve);
                        logs.Add(new LogInfo(cmd, String.Concat(@"'", rawSrcFileName, @"' copied to '", rawDestPath, @"'"), LogState.Success));
                    }
                    else
                    {
                        Directory.CreateDirectory(Helper.GetDirNameEx(destPath));
                        File.Copy(srcFileName, destPath, !preserve);
                        logs.Add(new LogInfo(cmd, String.Concat(@"'", rawSrcFileName, @"' copied to '", rawDestPath, @"'"), LogState.Success));                        
                    }
                }
                
            }
            catch (IOException e)
            {
                if (preserve)
                {
                    if (noWarn)
                    {
                        logs.Add(new LogInfo(cmd, String.Concat("Cannot overwrite ", destPath), LogState.Success));
                    }
                    else
                    {
                        logs.Add(new LogInfo(cmd, String.Concat(e.GetType(), ": ", e.Message), LogState.Error));
                    }
                }
                else
                {
                    logs.Add(new LogInfo(cmd, String.Concat(e.GetType(), ": ", e.Message), LogState.Error));
                }
            }
            catch (Exception e)
            {
                logs.Add(new LogInfo(cmd, String.Concat(e.GetType(), ": ", e.Message), LogState.Error));
            }

            return logs.ToArray(typeof(LogInfo)) as LogInfo[];
        }

        
        public LogInfo FileDelete(BakerCommand cmd)
        { // FileDelete,<FileName>,[,NOWARN][,NOREC]
            try
            {
                // Must-have operand : 1
                if (!(1 <= cmd.Operands.Length))
                    throw new InvalidOperandException("Necessary operands does not exist");

                string fileName = ExpandVariables(cmd.Operands[0]);

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

            return new LogInfo(cmd, "FileDelete", LogState.Success);
        }

        public LogInfo FileRename(BakerCommand cmd)
        { // FileRename,<srcFileName>,<destFileName>
            try
            {
                // Must-have operand : 2
                if (!(1 <= cmd.Operands.Length))
                    throw new InvalidOperandException("Necessary operands does not exist");

                string srcFileName = ExpandVariables(cmd.Operands[0]);
                string destFileName = ExpandVariables(cmd.Operands[1]);

                // Check if srcFileName exists
                if (File.Exists(srcFileName) == false)
                    throw new FileDoesNotExistException(String.Format("{0} does not exist", srcFileName));

                File.Move(srcFileName, destFileName);
            }
            catch (Exception e)
            {
                Console.WriteLine("[ERR]\n{0}", e.ToString());
            }

            return new LogInfo(cmd, "FileRename", LogState.Success);
        }

        public LogInfo FileCreateBlank(BakerCommand cmd)
        { // FileCreateBlank,<FileName>[,PRESERVE][,NOWARN][,UTF8 | UTF16LE | UTF16BE | ANSI]
            try
            {
                // Must-have operand : 1
                if (!(1 <= cmd.Operands.Length))
                    throw new InvalidOperandException("Necessary operands does not exist");

                string fileName = ExpandVariables(cmd.Operands[0]);
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

            return new LogInfo(cmd, "FileCreateBlank", LogState.Success);
        }
    }
}