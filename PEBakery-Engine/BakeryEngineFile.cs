using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections;

namespace BakeryEngine
{
    using VariableDictionary = Dictionary<string, string>;

    public partial class BakeryEngine
    {
        /// <summary>
        /// Exception used in BakeryEngine file commands
        /// </summary>
        public class PathNotFileException : Exception
        {
            private BakeryCommand command = null;
            public BakeryCommand Command
            {
                get { return command; }
            }
            public PathNotFileException() { }
            public PathNotFileException(string message) : base(message) { }
            public PathNotFileException(BakeryCommand command) { }
            public PathNotFileException(string message, BakeryCommand command) : base(message) { this.command = command; }
            public PathNotFileException(string message, Exception inner) : base(message, inner) { }
        }

        /// <summary>
        /// Exception used in BakeryEngine file commands
        /// </summary>
        public class PathNotDirException : Exception
        {
            private BakeryCommand command = null;
            public BakeryCommand Command
            {
                get { return command; }
            }
            public PathNotDirException() { }
            public PathNotDirException(string message) : base(message) { }
            public PathNotDirException(BakeryCommand command) { }
            public PathNotDirException(string message, BakeryCommand command) : base(message) { this.command = command; }
            public PathNotDirException(string message, Exception inner) : base(message, inner) { }
        }


        /*
         * File Commands
         * Note) Need refactor to support file name longer than 260 length.
         * http://bcl.codeplex.com/releases/view/42783
         * http://alphafs.alphaleonis.com/
         */

        /// <summary>
        /// FileCopy,<SrcFileName>,<DestPath>[,PRESERVE][,NOWARN][,NOREC]
        /// Wildcard supported in <SrcFileName>
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns>LogInfo[]</returns>
        public LogInfo[] FileCopy(BakeryCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 2, optional operand : 3
            const int necessaryOperandNum = 2;
            const int optionalOperandNum = 3;

            if (cmd.Operands.Length < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);
            else if (necessaryOperandNum + optionalOperandNum < cmd.Operands.Length)
                throw new InvalidOperandException("Too many operands", cmd);

            string srcFileName = EscapeString(variables.Expand(cmd.Operands[0]));
            string rawSrcFileName = cmd.Operands[0];
            string destPath = EscapeString(variables.Expand(cmd.Operands[1]));
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

            for (int i = necessaryOperandNum; i < cmd.Operands.Length; i++)
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
                        throw new InvalidOperandException($"Invalid operand [{operand}]", cmd);
                }
            }

            try
            {
                if (srcContainWildcard)
                {
                    string srcDirToFind = Helper.GetDirNameEx(srcFileName);
                    string rawSrcDirToFind = Helper.GetDirNameEx(rawSrcFileName);
                    string[] listToCopy;
                    if (noRec)
                        listToCopy = Directory.GetFiles(srcDirToFind, Path.GetFileName(srcFileName));
                    else
                        listToCopy = Directory.GetFiles(srcDirToFind, Path.GetFileName(srcFileName), SearchOption.AllDirectories);
                    foreach (string searchedFilePath in listToCopy)
                    {
                        if (destPathIsDir || !destPathExists)
                        {
                            string rawDestPathDir = Helper.GetDirNameEx(rawDestPath);
                            string destPathTail = searchedFilePath.Remove(0, srcDirToFind.Length+1); // 1 for \\
                            string destFullPath = Path.Combine(Helper.RemoveLastDirChar(destPath), destPathTail);
                            Directory.CreateDirectory(Path.GetDirectoryName(destFullPath));
                            if (File.Exists(destFullPath) && !noWarn)
                                logs.Add(new LogInfo(cmd, $"[{Path.Combine(rawSrcDirToFind, destPathTail)}] will be overwritten", LogState.Warning));
                            File.Copy(searchedFilePath, destFullPath, !preserve);
                            logs.Add(new LogInfo(cmd, $"[{Path.Combine(rawSrcDirToFind, destPathTail)}] copied to [{Path.Combine(rawDestPathDir, destPathTail)}]", LogState.Success));
                        }
                        else
                            throw new PathNotDirException("<DestPath> must be directory when using wildcard in <SrcFileName>", cmd);
                    }
                    if (listToCopy.Length == 0)
                        logs.Add(new LogInfo(cmd, $"[{rawDestPath}] not found", noWarn ? LogState.Ignore : LogState.Warning));
                }
                else
                {
                    if (destPathIsDir)
                    {
                        Directory.CreateDirectory(destPath);
                        string rawDestPathDir = Helper.GetDirNameEx(rawDestPath);
                        string destPathTail = srcFileName.Remove(0, Helper.GetDirNameEx(srcFileName).Length + 1); // 1 for \\
                        string destFullPath = string.Concat(Helper.RemoveLastDirChar(destPath), Path.DirectorySeparatorChar, destPathTail);
                        if (File.Exists(destFullPath))
                            logs.Add(new LogInfo(cmd, $"[{Path.Combine(rawDestPathDir, destPathTail)}] will be overwritten", noWarn ? LogState.Ignore : LogState.Warning));
                            
                        File.Copy(srcFileName, destFullPath, !preserve);
                        logs.Add(new LogInfo(cmd, $"[{rawSrcFileName}] copied to [{rawDestPath}]", LogState.Success));
                    }
                    else
                    {
                        Directory.CreateDirectory(Helper.GetDirNameEx(destPath));
                        if (destPathExists)
                            logs.Add(new LogInfo(cmd, $"[{rawDestPath}] will be overwritten", noWarn ? LogState.Ignore : LogState.Warning));
                        File.Copy(srcFileName, destPath, !preserve);
                        logs.Add(new LogInfo(cmd, $"[{rawSrcFileName}] copied to [{rawDestPath}]", LogState.Success));                        
                    }
                }
                
            }
            catch (IOException e)
            {
                if (preserve && noWarn)
                {
                    logs.Add(new LogInfo(cmd, $"Cannot overwrite [{destPath}]", LogState.Ignore));
                }
                else
                {
                    throw new IOException(e.Message, e);
                }
            }

            return logs.ToArray();
        }

        /// <summary>
        /// FileDelete,<FileName>,[,NOWARN][,NOREC]
        /// Wildcard supported in <FileName>
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public LogInfo[] FileDelete(BakeryCommand cmd)
        { 
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 1, optional operand : 2
            const int necessaryOperandNum = 1;
            const int optionalOperandNum = 2;

            if (cmd.Operands.Length < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);
            else if (necessaryOperandNum + optionalOperandNum < cmd.Operands.Length)
                throw new InvalidOperandException("Too many operands", cmd);

            string filePath = EscapeString(variables.Expand(cmd.Operands[0]));
            string rawFilePath = cmd.Operands[0];

            // Check srcFileName contains wildcard
            bool filePathContainsWildcard = true;
            if (filePath.IndexOfAny(new char[] { '*', '?' }) == -1) // No wildcard
                filePathContainsWildcard = false;
            // Check destPath is directory
            if (Directory.Exists(filePath))
                throw new PathNotFileException($"[{filePath}] cannot be directory", cmd);

            bool noWarn = false;
            bool noRec = false;

            for (int i = necessaryOperandNum; i < cmd.Operands.Length; i++)
            {
                string operand = cmd.Operands[i];
                switch (operand.ToUpper())
                {
                    case "NOWARN": // no warning when if the file does not exists
                        noWarn = true;
                        break;
                    case "NOREC": // no recursive wildcard copy
                        noRec = true;
                        break;
                    default:
                        throw new InvalidOperandException($"Invalid operand [{operand}]", cmd);
                }
            }

            if (filePathContainsWildcard) // wildcard exists
            {                   
                string srcDirToFind = Helper.GetDirNameEx(filePath);
                string rawSrcDirToFind = Helper.GetDirNameEx(rawFilePath);
                string[] listToDelete;
                if (noRec)
                    listToDelete = Directory.GetFiles(srcDirToFind, Path.GetFileName(filePath));
                else
                    listToDelete = Directory.GetFiles(srcDirToFind, Path.GetFileName(filePath), SearchOption.AllDirectories);
                foreach (string searchedFilePath in listToDelete)
                {
                    File.Delete(searchedFilePath);
                    string searchedFileName = searchedFilePath.Remove(0, srcDirToFind.Length + 1); // 1 for \\
                    logs.Add(new LogInfo(cmd, $"[{Path.Combine(rawSrcDirToFind, searchedFileName)}] deleted", LogState.Success));
                }
                if (listToDelete.Length == 0)
                {
                    if (!noWarn) // file is not found
                        logs.Add(new LogInfo(cmd, $"[{rawFilePath}] not found", LogState.Warning));
                }
            }
            else // No wildcard
            {
                if (!noWarn && !File.Exists(filePath)) // File.Delete does not throw exception when file is not found
                    logs.Add(new LogInfo(cmd, $"[{rawFilePath}] not found", LogState.Warning));
                File.Delete(filePath); 
                logs.Add(new LogInfo(cmd, $"[{rawFilePath}] deleted", LogState.Success));
            }

            return logs.ToArray();
        }

        /// <summary>
        /// FileRename,<srcFileName>,<destFileName>
        /// Wildcard not supported
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public LogInfo[] FileMove(BakeryCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 2, optional operand : 0
            const int necessaryOperandNum = 2;
            const int optionalOperandNum = 0;

            if (cmd.Operands.Length < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);
            else if (necessaryOperandNum + optionalOperandNum < cmd.Operands.Length)
                throw new InvalidOperandException("Too many operands", cmd);

            string srcFileName = EscapeString(variables.Expand(cmd.Operands[0]));
            string rawSrcFileName = cmd.Operands[0];
            string destFileName = EscapeString(variables.Expand(cmd.Operands[1]));
            string rawDestFileName = cmd.Operands[1];

            // Check if srcFileName exists
            if (File.Exists(srcFileName) == false)
                throw new FileNotFoundException($"[{rawSrcFileName}] does not exist");

            // src and dest filename is same, so log it
            if (string.Equals(Helper.RemoveLastDirChar(srcFileName), Helper.RemoveLastDirChar(destFileName), StringComparison.OrdinalIgnoreCase))
                logs.Add(new LogInfo(cmd, "Cannot rename to same filename", LogState.Warning));
            else
            {
                // File.Move cannot move file if volume is different.
                string srcFileDrive = Path.GetPathRoot(Path.GetFullPath(srcFileName));
                string destFileDrive = Path.GetPathRoot(Path.GetFullPath(destFileName));
                if (string.Equals(srcFileDrive, destFileDrive, StringComparison.OrdinalIgnoreCase))
                { // Same volume. Just use File.Move.
                    File.Move(srcFileName, destFileName);
                    logs.Add(new LogInfo(cmd, $"[{rawSrcFileName}] moved to [{rawDestFileName}]", LogState.Success));
                }
                else
                { // Use File.Copy and File.Delete instead.
                    try
                    {
                        File.Copy(srcFileName, destFileName, false);
                        File.Delete(srcFileName);
                        logs.Add(new LogInfo(cmd, $"[{rawSrcFileName}] moved to [{rawDestFileName}]", LogState.Success));
                    }
                    catch (IOException)
                    {
                        logs.Add(new LogInfo(cmd, $"Cannot overwrite [{rawDestFileName}]", LogState.Warning));
                    }
                }
            }

            
            return logs.ToArray();
        }

        /// <summary>
        /// FileCreateBlank,<FileName>[,PRESERVE][,NOWARN][,UTF8 | UTF16LE | UTF16BE | ANSI]
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public LogInfo[] FileCreateBlank(BakeryCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();
            // Necessary operand : 1, optional operand : 3
            const int necessaryOperandNum = 1;
            const int optionalOperandNum = 3;

            if (cmd.Operands.Length < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);
            else if (necessaryOperandNum + optionalOperandNum < cmd.Operands.Length)
                throw new InvalidOperandException("Too many operands", cmd);

            string fileName = EscapeString(variables.Expand(cmd.Operands[0]));
            string rawFileName = cmd.Operands[0];

            bool preserve = false;
            bool noWarn = false;
            Encoding encoding = null;

            for (int i = necessaryOperandNum; i < cmd.Operands.Length; i++)
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
                    default:
                        throw new InvalidOperandException($"Invalid operand [{operand}]", cmd);
                }
            }

            // Default Encoding - UTF8
            if (encoding == null)
                encoding = Encoding.UTF8;

            // If file already exists, 
            if (File.Exists(fileName))
            {
                if (!preserve)
                    logs.Add(new LogInfo(cmd, $"[{rawFileName}] will be overwritten", noWarn ? LogState.Ignore : LogState.Warning));
            }

            try
            {
                FileStream fs = new FileStream(fileName, preserve ? FileMode.CreateNew : FileMode.Create, FileAccess.Write, FileShare.Write);
                Helper.WriteTextBOM(fs, encoding).Close();
                logs.Add(new LogInfo(cmd, $"Created blank text file [{rawFileName}]", LogState.Success));
            }
            catch (IOException)
            {
                if (preserve)
                    logs.Add(new LogInfo(cmd, $"Cannot overwrite [{rawFileName}]", noWarn ? LogState.Ignore : LogState.Warning));
            }

            return logs.ToArray();
        }
    }
}