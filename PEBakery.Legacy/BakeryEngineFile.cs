using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections;
using System.Net.Http;

namespace BakeryEngine_Legacy
{
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
        /// Expand,<SrcCab>,<DestDir>,[SingleFileName],[PRESERVE],[NOWARN]
        /// </summary>
        /// <remarks>
        /// SingleFileName to extract must come as third parameter
        /// </remarks>
        /// <param name="cmd"></param>
        /// <returns></returns>
        private List<LogInfo> CmdExpand(BakeryCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 2, optional operand : 3
            const int necessaryOperandNum = 2;
            const int optionalOperandNum = 3;
            if (cmd.Operands.Count < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);
            else if (necessaryOperandNum + optionalOperandNum < cmd.Operands.Count)
                throw new InvalidOperandException("Too many operands", cmd);

            string srcCabFile = UnescapeString(ExpandVariables(cmd.Operands[0]));
            string rawSrcCabFile = cmd.Operands[0];
            string destDir = UnescapeString(ExpandVariables(cmd.Operands[1]));
            string rawDestDir = cmd.Operands[1];

            // Check destDir is directory
            bool destExists = false;
            bool destIsDir = false;
            if (Directory.Exists(destDir))
            {
                destExists = true;
                destIsDir = true;
            }
            else if (File.Exists(destDir))
                destExists = true;

            string singleFile = string.Empty;
            string rawSingleFile = string.Empty;

            if (necessaryOperandNum + 1 <= cmd.Operands.Count)
            {
                string operand = cmd.Operands[necessaryOperandNum];
                singleFile = UnescapeString(ExpandVariables(operand));
                rawSingleFile = operand;
            }

            bool preserve = false;
            bool noWarn = false;

            for (int i = necessaryOperandNum + 1; i < cmd.Operands.Count; i++)
            {
                string operand = cmd.Operands[i];
                if (string.Equals(operand, "PRESERVE", StringComparison.OrdinalIgnoreCase))
                    preserve = true;
                else if (string.Equals(operand, "NOWARN", StringComparison.OrdinalIgnoreCase))
                    noWarn = true;
            }

            if (destExists && !destIsDir)
            { // Cannot make an directory, since destination is file
                throw new PathNotDirException($"[{rawDestDir}] must be directory", cmd);
            }
            else
            {
                if (!destExists) // Destination not exists, make an dir
                    Directory.CreateDirectory(destDir);
                if (string.Equals(singleFile, string.Empty, StringComparison.Ordinal))
                { // No singleFile operand, Extract all
                    List<string> extractedList;
                    if (CompressHelper.ExtractCab(srcCabFile, destDir, out extractedList)) // Success
                    {
                        logs.Add(new LogInfo(cmd, LogState.Success, $"[{extractedList.Count} files] extracted from [{rawSrcCabFile}]"));
                        foreach (string extracted in extractedList)
                            logs.Add(new LogInfo(cmd, LogState.Success, $"[{extracted}] extracted", cmd.Depth + 1));
                        logs.Add(new LogInfo(cmd, LogState.Success, $"End of the list"));
                    }
                    else // Failure
                        logs.Add(new LogInfo(cmd, LogState.Success, $"Failed to extract [{rawSrcCabFile}]"));
                }
                else
                { // singleFile specified, Extract only that file
                    string destSingleFile = Path.Combine(destDir, singleFile);
                    bool destSingleFileExists = File.Exists(destSingleFile);
                    if (destSingleFileExists)
                    { // Check PRESERVE, NOWARN 
                        if (preserve)
                        { // Do nothing
                            if (noWarn)
                                logs.Add(new LogInfo(cmd, LogState.Ignore, $"[{Path.Combine(rawDestDir, rawSingleFile)}] already exists, cannot extract from [{rawSrcCabFile}]"));
                            else
                                logs.Add(new LogInfo(cmd, LogState.Warning, $"[{Path.Combine(rawDestDir, rawSingleFile)}] already exists, cannot extract from [{rawSrcCabFile}]"));
                            return logs;
                        }
                    }

                    if (CompressHelper.ExtractCab(srcCabFile, destDir, singleFile)) // Success
                    {
                        logs.Add(new LogInfo(cmd, LogState.Success, $"[{rawSingleFile}] extracted from [{rawSrcCabFile}]"));
                        if (destSingleFileExists)
                        {
                            if (noWarn)
                                logs.Add(new LogInfo(cmd, LogState.Ignore, $"[{rawSingleFile}] overwritten"));
                            else
                                logs.Add(new LogInfo(cmd, LogState.Warning, $"[{rawSingleFile}] overwritten"));
                        }
                    }
                    else // Failure
                    {
                        logs.Add(new LogInfo(cmd, LogState.Error, $"Failed to extract [{rawSingleFile}] from [{rawSrcCabFile}]"));
                    }
                }
            }

            return logs;
        }

        /// <summary>
        /// CopyOrExpand,<SrcFile><DestPath>,[PRESERVE],[NOWARN]
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <param name="cmd"></param>
        /// <returns></returns>
        private List<LogInfo> CmdCopyOrExpand(BakeryCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 2, optional operand : 2
            const int necessaryOperandNum = 2;
            const int optionalOperandNum = 2;
            if (cmd.Operands.Count < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);
            else if (necessaryOperandNum + optionalOperandNum < cmd.Operands.Count)
                throw new InvalidOperandException("Too many operands", cmd);

            string srcFile = UnescapeString(ExpandVariables(cmd.Operands[0]));
            string rawSrcFile = cmd.Operands[0];
            string destPath = UnescapeString(ExpandVariables(cmd.Operands[1]));
            string rawDestPath = cmd.Operands[1];

            // Check destDir is directory
            bool destExists = false;
            bool destIsDir = false;
            if (Directory.Exists(destPath))
            {
                destExists = true;
                destIsDir = true;
            }
            else if (File.Exists(destPath))
                destExists = true;

            bool preserve = false;
            bool noWarn = false;

            for (int i = necessaryOperandNum; i < cmd.Operands.Count; i++)
            {
                string operand = cmd.Operands[i];
                if (string.Equals(operand, "PRESERVE", StringComparison.OrdinalIgnoreCase))
                    preserve = true;
                else if (string.Equals(operand, "NOWARN", StringComparison.OrdinalIgnoreCase))
                    noWarn = true;
            }

            string srcFileName = Path.GetFileName(srcFile);
            string destNewPath; // TODO : Need more clearer name...
            string destFileName;
            string destDir;
            if (destIsDir)
            {
                destNewPath = Path.Combine(destPath, srcFileName);
                destFileName = srcFileName;
                destDir = destPath;
            }
            else
            {
                destNewPath = Path.Combine(Path.GetDirectoryName(destPath), srcFileName);
                destFileName = Path.GetFileName(destPath);
                destDir = Path.GetDirectoryName(destPath);
            }

            // Filter overwrite
            if (destExists && !destIsDir) // Check if destPath is file and already exists
            {
                if (preserve)
                {
                    logs.Add(new LogInfo(cmd, noWarn ? LogState.Ignore : LogState.Warning, $"Cannot overwrite [{destPath}]"));
                    return logs;
                }
                else
                {
                    logs.Add(new LogInfo(cmd, noWarn ? LogState.Ignore : LogState.Warning, $"[{destPath}] will be overwritten"));
                }
            }
            if (destIsDir && File.Exists(destNewPath) && !preserve) // Check if "destDir\srcFileName" already exists
            {
                if (preserve)
                {
                    logs.Add(new LogInfo(cmd, noWarn ? LogState.Ignore : LogState.Warning, $"Cannot overwrite [{destNewPath}]"));
                    return logs;
                }
                else
                {
                    logs.Add(new LogInfo(cmd, noWarn ? LogState.Ignore : LogState.Warning, $"[{destNewPath}] will be overwritten"));
                }
            }

            if (File.Exists(srcFile))
            { // SrcFile is uncompressed, just copy!  
                try
                {
                    if (destIsDir)
                        File.Copy(srcFile, Path.Combine(destPath, srcFileName), !preserve);
                    else
                        File.Copy(srcFile, destPath, !preserve);
                    logs.Add(new LogInfo(cmd, LogState.Success, $"[{rawSrcFile}] copied to [{rawDestPath}]"));
                }
                catch (IOException) when (preserve)
                {
                    if (noWarn)
                        logs.Add(new LogInfo(cmd, LogState.Ignore, $"Cannot overwrite [{destPath}]"));
                    else
                        logs.Add(new LogInfo(cmd, LogState.Warning, $"Cannot overwrite [{destPath}]"));
                }
            }
            else
            {
                string srcCab = srcFile.Substring(0, srcFile.Length - 1) + "_";
                string rawSrcCab = rawSrcFile.Substring(0, rawSrcFile.Length - 1) + "_";
                if (File.Exists(srcCab))
                { // Expand SrcCab
                    if (CompressHelper.ExtractCab(srcCab, destDir))
                    { // Decompress Success
                        if (File.Exists(Path.Combine(destDir, srcFileName))) // destFileName == srcFileName?
                        { // dest filename not specified
                            logs.Add(new LogInfo(cmd, LogState.Success, $"[{srcFileName}] extracted from [{rawSrcCab}]"));
                        }
                        else // destFileName != srcFileName
                        { // dest filename specified
                            File.Move(Path.Combine(destDir, srcFileName), Path.Combine(destDir, destFileName));
                            logs.Add(new LogInfo(cmd, LogState.Success, $"[{destFileName}] extracted from [{rawSrcCab}] and renamed from [{srcFileName}]"));
                        }
                    }
                    else
                    { // Decompress Failure
                        logs.Add(new LogInfo(cmd, LogState.Error, $"Failed to extract [{destFileName}] from [{rawSrcCab}]"));
                    }

                }
                else
                { // Error
                    logs.Add(new LogInfo(cmd, LogState.Error, $"Unable to find [{rawSrcFile}] nor [{rawSrcCab}]"));
                }

            }

            return logs;
        }

        /// <summary>
        /// DirCopy,<SrcDir>,<DestDir>
        /// </summary>
        /// <remarks>
        /// Wildcard supported in <SrcDir>
        /// Option [SHOW] deprecated, not used in WinPESE
        /// </remarks>
        /// <param name="cmd"></param>
        /// <returns></returns>
        private List<LogInfo> CmdDirCopy(BakeryCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 2, optional operand : 1
            const int necessaryOperandNum = 2;
            const int optionalOperandNum = 1;
            if (cmd.Operands.Count < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);
            else if (necessaryOperandNum + optionalOperandNum < cmd.Operands.Count)
                throw new InvalidOperandException("Too many operands", cmd);

            string srcDir = UnescapeString(ExpandVariables(cmd.Operands[0]));
            string rawSrcDir = cmd.Operands[0];
            string destPath = UnescapeString(ExpandVariables(cmd.Operands[1]));
            string rawDestPath = cmd.Operands[1];

            for (int i = necessaryOperandNum; i < cmd.Operands.Count; i++)
            {
                string operand = cmd.Operands[i];
                if (string.Equals(operand, "SHOW", StringComparison.OrdinalIgnoreCase)) // for compability with WB082
                { }
                else
                    throw new InvalidOperandException($"Invalid operand [{operand}]", cmd);
            }

            // Check destPath is directory
            if (File.Exists(destPath))
                throw new PathNotDirException($"Destination [{rawDestPath}] must be directory", cmd);
            else if (!Directory.Exists(destPath))
                Directory.CreateDirectory(destPath);

            // Check srcFileName contains wildcard
            if (srcDir.IndexOfAny(new char[] { '*', '?' }) == -1) // No wildcard
                srcDir = Path.Combine(FileHelper.RemoveLastDirChar(srcDir), "*");

            string srcDirToFind = FileHelper.GetDirNameEx(srcDir);
            string rawSrcDirToFind = FileHelper.GetDirNameEx(rawSrcDir);
                
            string[] dirListToCreate = Directory.GetDirectories(srcDirToFind, Path.GetFileName(srcDir), SearchOption.AllDirectories);
            string[] fileListToCopy = Directory.GetFiles(srcDirToFind, Path.GetFileName(srcDir), SearchOption.AllDirectories);

            if (0 < dirListToCreate.Length || 0 < fileListToCopy.Length)
                logs.Add(new LogInfo(cmd, LogState.Success, $"[{rawSrcDir}] will be copied to [{rawDestPath}]"));

            foreach (string searchedDir in dirListToCreate)
            {
                string rawSearchedFile = searchedDir.Replace(srcDirToFind, FileHelper.RemoveLastDirChar(rawSrcDirToFind));
                string destFullPath = searchedDir.Replace(srcDirToFind, FileHelper.RemoveLastDirChar(destPath));
                string rawDestFullPath = searchedDir.Replace(srcDirToFind, FileHelper.RemoveLastDirChar(rawDestPath));
                if (Directory.Exists(destFullPath))
                {
                    logs.Add(new LogInfo(cmd, LogState.Warning, $"Directory [{rawDestFullPath}] already exists", cmd.Depth + 1));
                }
                else
                {
                    Directory.CreateDirectory(destFullPath);
                    logs.Add(new LogInfo(cmd, LogState.Success, $"Directory [{rawDestFullPath}] created", cmd.Depth + 1));
                }
            }

            foreach (string searchedFile in fileListToCopy)
            {
                string rawSearchedFile = searchedFile.Replace(srcDirToFind, FileHelper.RemoveLastDirChar(rawSrcDirToFind));
                string destFullPath = searchedFile.Replace(srcDirToFind, FileHelper.RemoveLastDirChar(destPath));
                string rawDestFullPath = searchedFile.Replace(srcDirToFind, FileHelper.RemoveLastDirChar(rawDestPath));

                if (!Directory.Exists(FileHelper.GetDirNameEx(destFullPath)))
                    Directory.CreateDirectory(FileHelper.GetDirNameEx(destFullPath));

                if (File.Exists(destFullPath))
                    logs.Add(new LogInfo(cmd, LogState.Warning, $"[{rawDestFullPath}] will be overwritten", cmd.Depth + 1));
                File.Copy(searchedFile, destFullPath, true);
                logs.Add(new LogInfo(cmd, LogState.Success, $"[{rawSearchedFile}] copied to [{rawDestFullPath}]", cmd.Depth + 1));
            }

            if (0 < dirListToCreate.Length)
                logs.Add(new LogInfo(cmd, LogState.Success, $"[{dirListToCreate.Length}] directories created"));

            if (0 < fileListToCopy.Length)
                logs.Add(new LogInfo(cmd, LogState.Success, $"[{fileListToCopy.Length}] files copied"));

            if (dirListToCreate.Length == 0 && fileListToCopy.Length == 0)
            {
                logs.Add(new LogInfo(cmd, LogState.Warning, $"Directory matches wildcard [{rawSrcDir}] not found"));
                logs.Add(new LogInfo(cmd, LogState.Warning, $"File matches wildcard [{rawSrcDir}] not found"));
            }

            return logs;
        }

        /// <summary>
        /// DirDelete,<DirPath>
        /// </summary>
        /// <remarks>
        /// [FAST] optional command deprecated
        /// Wildcard supported in <DirPath>
        /// This command only deletes directory, not file
        /// </remarks>
        /// <param name="cmd"></param>
        /// <returns></returns>
        private List<LogInfo> CmdDirDelete(BakeryCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 1, optional operand : 0
            const int necessaryOperandNum = 1;
            const int optionalOperandNum = 0;

            if (cmd.Operands.Count < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);
            else if (necessaryOperandNum + optionalOperandNum < cmd.Operands.Count)
                throw new InvalidOperandException("Too many operands", cmd);

            string dirPath = UnescapeString(ExpandVariables(cmd.Operands[0]));
            string rawDirPath = cmd.Operands[0];

            // Check srcFileName contains wildcard
            bool dirPathContainsWildcard = true;
            if (dirPath.IndexOfAny(new char[] { '*', '?' }) == -1) // No wildcard
                dirPathContainsWildcard = false;
            // Check destPath is directory
            if (File.Exists(dirPath))
                throw new PathNotDirException($"[{dirPath}] must be directory", cmd);

            for (int i = necessaryOperandNum; i < cmd.Operands.Count; i++)
            {
                string operand = cmd.Operands[i];
                if (string.Equals(operand, "FAST", StringComparison.OrdinalIgnoreCase))
                { // Do nothing. FAST option is deprecated in PEBakery
                }
                else
                    throw new InvalidOperandException($"Invalid operand [{operand}]", cmd);
            }

            if (dirPathContainsWildcard) // wildcard exists
            {
                string srcDirToFind = FileHelper.GetDirNameEx(dirPath);
                string rawSrcDirToFind = FileHelper.GetDirNameEx(rawDirPath);
                string[] listToDelete = Directory.GetDirectories(srcDirToFind, Path.GetFileName(dirPath), SearchOption.AllDirectories);
                if (0 < listToDelete.Length)
                    logs.Add(new LogInfo(cmd, LogState.Success, $"[{rawDirPath}] will be deleted"));
                foreach (string searchedPath in listToDelete)
                {
                    FileHelper.DirectoryDeleteEx(searchedPath, true);
                    string searchedDirName = searchedPath.Remove(0, srcDirToFind.Length + 1); // 1 for \\
                    logs.Add(new LogInfo(cmd, LogState.Success, $"[{Path.Combine(rawSrcDirToFind, searchedDirName)}] deleted", cmd.Depth + 1));
                }
                if (0 < listToDelete.Length)
                    logs.Add(new LogInfo(cmd, LogState.Success, $"[{listToDelete.Length}] directories deleted"));
                else if (listToDelete.Length == 0) // Directory not found
                    logs.Add(new LogInfo(cmd, LogState.Warning, $"Directory matches [{rawDirPath}] not found"));
            }
            else // No wildcard
            {
                if (!Directory.Exists(dirPath))
                    logs.Add(new LogInfo(cmd, LogState.Warning, $"Directory [{rawDirPath}] not found"));
                else
                {
                    FileHelper.DirectoryDeleteEx(dirPath, true);
                    logs.Add(new LogInfo(cmd, LogState.Success, $"[{rawDirPath}] deleted"));
                }
            }

            return logs;
        }

        /// <summary>
        /// DirMove,<SrcDir>,<DestDir>
        /// </summary>
        /// <remarks>
        /// Wildcard not supported
        /// </remarks>
        /// <param name="cmd"></param>
        /// <returns></returns>
        private List<LogInfo> CmdDirMove(BakeryCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 2, optional operand : 0
            const int necessaryOperandNum = 2;
            const int optionalOperandNum = 0;

            if (cmd.Operands.Count < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);
            else if (necessaryOperandNum + optionalOperandNum < cmd.Operands.Count)
                throw new InvalidOperandException("Too many operands", cmd);

            string srcDir = UnescapeString(ExpandVariables(cmd.Operands[0]));
            string rawSrcDir = cmd.Operands[0];
            string destDir = UnescapeString(ExpandVariables(cmd.Operands[1]));
            string rawDestDir = cmd.Operands[1];

            // Check if srcDir exists
            if (!Directory.Exists(srcDir))
                throw new InvalidOperandException($"Directory [{rawSrcDir}] does not exist");
            // Check if destDir exists
            if (Directory.Exists(destDir))
                throw new InvalidOperandException($"Directory [{rawDestDir}] already exists");
            else if (File.Exists(destDir))
                throw new InvalidOperandException($"File [{rawDestDir}] already exists");

            // src and dest directory name is same, so log it
            if (string.Equals(FileHelper.RemoveLastDirChar(srcDir), FileHelper.RemoveLastDirChar(destDir), StringComparison.OrdinalIgnoreCase))
                logs.Add(new LogInfo(cmd, LogState.Warning, "Cannot move to same directory"));
            else
            {
                Directory.Move(srcDir, destDir);
                logs.Add(new LogInfo(cmd, LogState.Success, $"[{rawSrcDir}] moved to [{rawDestDir}]"));
            }

            return logs;
        }

        /// <summary>
        /// DirMake,<DestDir> 
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        private List<LogInfo> CmdDirMake(BakeryCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();
            // Necessary operand : 1, optional operand : 0
            const int necessaryOperandNum = 1;
            const int optionalOperandNum = 0;

            if (cmd.Operands.Count < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);
            else if (necessaryOperandNum + optionalOperandNum < cmd.Operands.Count)
                throw new InvalidOperandException("Too many operands", cmd);

            string destDir = UnescapeString(ExpandVariables(cmd.Operands[0]));
            string rawDestDir = cmd.Operands[0];

            Directory.CreateDirectory(destDir);
            logs.Add(new LogInfo(cmd, LogState.Success, $"Created directory [{rawDestDir}]"));

            return logs;
        }

        /// <summary>
        /// FileCopy,<SrcFile>,<DestPath>[,PRESERVE][,NOWARN][,NOREC]
        /// </summary>
        /// <remarks>
        /// Wildcard supported in <SrcFile>
        /// </remarks>
        /// <param name="cmd"></param>
        /// <returns>LogInfo[]</returns>
        private List<LogInfo> CmdFileCopy(BakeryCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 2, optional operand : 3
            const int necessaryOperandNum = 2;
            const int optionalOperandNum = 3;
            if (cmd.Operands.Count < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);
            else if (necessaryOperandNum + optionalOperandNum < cmd.Operands.Count)
                throw new InvalidOperandException("Too many operands", cmd);

            string srcFile = UnescapeString(ExpandVariables(cmd.Operands[0]));
            string rawSrcFile = cmd.Operands[0];
            string destPath = UnescapeString(ExpandVariables(cmd.Operands[1]));
            string rawDestPath = cmd.Operands[1];

            // Check srcFileName contains wildcard
            bool srcContainWildcard = true;
            if (srcFile.IndexOfAny(new char[] { '*', '?' }) == -1) // No wildcard
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

            for (int i = necessaryOperandNum; i < cmd.Operands.Count; i++)
            {
                string operand = cmd.Operands[i];
                if (string.Equals(operand, "PRESERVE", StringComparison.OrdinalIgnoreCase))
                    preserve = true;
                else if (string.Equals(operand, "NOWARN", StringComparison.OrdinalIgnoreCase))
                    noWarn = true;
                else if (string.Equals(operand, "SHOW", StringComparison.OrdinalIgnoreCase)) // for compability with WB082
                { }
                else if (string.Equals(operand, "NOREC", StringComparison.OrdinalIgnoreCase)) // no recursive wildcard copy
                    noRec = true;
                else
                    throw new InvalidOperandException($"Invalid operand [{operand}]", cmd);
            }

            try
            {
                if (srcContainWildcard)
                {
                    string srcDirToFind = FileHelper.GetDirNameEx(srcFile);
                    string rawSrcDirToFind = FileHelper.GetDirNameEx(rawSrcFile);
                    string[] listToCopy;
                    if (noRec)
                        listToCopy = Directory.GetFiles(srcDirToFind, Path.GetFileName(srcFile));
                    else
                        listToCopy = Directory.GetFiles(srcDirToFind, Path.GetFileName(srcFile), SearchOption.AllDirectories);

                    if (0 < listToCopy.Length)
                        logs.Add(new LogInfo(cmd, LogState.Success, $"[{srcFile}] will be copied to [{destPath}]"));
                    foreach (string searchedFilePath in listToCopy)
                    {
                        if (destPathIsDir || !destPathExists)
                        {
                            string rawDestPathDir = FileHelper.GetDirNameEx(rawDestPath);
                            string destPathTail = searchedFilePath.Remove(0, srcDirToFind.Length+1); // 1 for \\
                            string destFullPath = Path.Combine(FileHelper.RemoveLastDirChar(destPath), destPathTail);
                            Directory.CreateDirectory(Path.GetDirectoryName(destFullPath));
                            if (File.Exists(destFullPath) && !noWarn)
                                logs.Add(new LogInfo(cmd, LogState.Warning, $"[{Path.Combine(rawSrcDirToFind, destPathTail)}] will be overwritten", cmd.Depth + 1));
                            File.Copy(searchedFilePath, destFullPath, !preserve);
                            logs.Add(new LogInfo(cmd, LogState.Success, $"[{Path.Combine(rawSrcDirToFind, destPathTail)}] copied to [{Path.Combine(rawDestPathDir, destPathTail)}]", cmd.Depth + 1));
                        }
                        else
                            throw new PathNotDirException("<DestPath> must be directory when using wildcard in <SrcFileName>", cmd);
                    }
                    if (0 < listToCopy.Length)
                        logs.Add(new LogInfo(cmd, LogState.Success, $"[{listToCopy.Length}] files copied"));
                    else if (listToCopy.Length == 0)
                        logs.Add(new LogInfo(cmd, noWarn ? LogState.Ignore : LogState.Warning, $"Files matches wildcard [{rawSrcFile}] not found"));
                }
                else
                {
                    if (destPathIsDir)
                    {
                        Directory.CreateDirectory(destPath);
                        string rawDestPathDir = FileHelper.GetDirNameEx(rawDestPath);
                        string destPathTail = srcFile.Remove(0, FileHelper.GetDirNameEx(srcFile).Length + 1); // 1 for \\
                        string destFullPath = string.Concat(FileHelper.RemoveLastDirChar(destPath), Path.DirectorySeparatorChar, destPathTail);
                        if (File.Exists(destFullPath))
                            logs.Add(new LogInfo(cmd, noWarn ? LogState.Ignore : LogState.Warning, $"[{Path.Combine(rawDestPathDir, destPathTail)}] will be overwritten"));
                            
                        File.Copy(srcFile, destFullPath, !preserve);
                        logs.Add(new LogInfo(cmd, LogState.Success, $"[{rawSrcFile}] copied to [{rawDestPath}]"));
                    }
                    else
                    {
                        Directory.CreateDirectory(FileHelper.GetDirNameEx(destPath));
                        if (destPathExists)
                            logs.Add(new LogInfo(cmd, noWarn ? LogState.Ignore : LogState.Warning, $"[{rawDestPath}] will be overwritten"));
                        File.Copy(srcFile, destPath, !preserve);
                        logs.Add(new LogInfo(cmd, LogState.Success, $"[{rawSrcFile}] copied to [{rawDestPath}]"));                        
                    }
                }
                
            }
            catch (IOException) when (preserve)
            {
                if (noWarn)
                    logs.Add(new LogInfo(cmd, LogState.Ignore, $"Cannot overwrite [{destPath}]"));
                else
                    logs.Add(new LogInfo(cmd, LogState.Warning, $"Cannot overwrite [{destPath}]"));
            }

            return logs;
        }

        /// <summary>
        /// FileDelete,<FilePath>,[,NOWARN][,NOREC]
        /// </summary>
        /// <remarks>
        /// Wildcard supported in <FilePath>
        /// </remarks>
        /// <param name="cmd"></param>
        /// <returns></returns>
        private List<LogInfo> CmdFileDelete(BakeryCommand cmd)
        { 
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 1, optional operand : 2
            const int necessaryOperandNum = 1;
            const int optionalOperandNum = 2;

            if (cmd.Operands.Count < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);
            else if (necessaryOperandNum + optionalOperandNum < cmd.Operands.Count)
                throw new InvalidOperandException("Too many operands", cmd);

            string filePath = UnescapeString(ExpandVariables(cmd.Operands[0]));
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

            for (int i = necessaryOperandNum; i < cmd.Operands.Count; i++)
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
                string srcDirToFind = FileHelper.GetDirNameEx(filePath);
                string rawSrcDirToFind = FileHelper.GetDirNameEx(rawFilePath);
                string[] listToDelete;
                if (noRec)
                    listToDelete = Directory.GetFiles(srcDirToFind, Path.GetFileName(filePath));
                else
                    listToDelete = Directory.GetFiles(srcDirToFind, Path.GetFileName(filePath), SearchOption.AllDirectories);

                if (0 < listToDelete.Length)
                    logs.Add(new LogInfo(cmd, LogState.Success, $"[{rawFilePath}] will be deleted"));
                foreach (string searchedFilePath in listToDelete)
                {
                    File.Delete(searchedFilePath);
                    string searchedFileName = searchedFilePath.Remove(0, srcDirToFind.Length + 1); // 1 for \\
                    logs.Add(new LogInfo(cmd, LogState.Success, $"[{Path.Combine(rawSrcDirToFind, searchedFileName)}] deleted", cmd.Depth + 1));
                }
                if (0 < listToDelete.Length)
                    logs.Add(new LogInfo(cmd, LogState.Success, $"[{listToDelete.Length}] files deleted"));
                else if (listToDelete.Length == 0)
                {
                    if (!noWarn) // file is not found
                        logs.Add(new LogInfo(cmd, LogState.Warning, $"[{rawFilePath}] not found"));
                }
            }
            else // No wildcard
            {
                if (!noWarn && !File.Exists(filePath)) // File.Delete does not throw exception when file is not found
                    logs.Add(new LogInfo(cmd, LogState.Warning, $"[{rawFilePath}] not found"));
                File.Delete(filePath); 
                logs.Add(new LogInfo(cmd, LogState.Success, $"[{rawFilePath}] deleted"));
            }

            return logs;
        }

        /// <summary>
        /// FileRename,<srcFile>,<destFile>
        /// </summary>
        /// <remarks>
        /// Wildcard not supported
        /// </remarks>
        /// <param name="cmd"></param>
        /// <returns></returns>
        private List<LogInfo> CmdFileMove(BakeryCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 2, optional operand : 0
            const int necessaryOperandNum = 2;
            const int optionalOperandNum = 0;

            if (cmd.Operands.Count < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);
            else if (necessaryOperandNum + optionalOperandNum < cmd.Operands.Count)
                throw new InvalidOperandException("Too many operands", cmd);

            string srcFile = UnescapeString(ExpandVariables(cmd.Operands[0]));
            string rawSrcFile = cmd.Operands[0];
            string destFile = UnescapeString(ExpandVariables(cmd.Operands[1]));
            string rawDestFile = cmd.Operands[1];

            // Check if srcFileName exists
            if (File.Exists(srcFile) == false)
                throw new FileNotFoundException($"[{rawSrcFile}] does not exist");
            // Check if destFileName exists
            if (Directory.Exists(destFile))
                throw new InvalidOperandException($"Directory [{rawDestFile}] already exists");
            else if (File.Exists(destFile))
                throw new InvalidOperandException($"File [{rawDestFile}] already exists");

            // src and dest filename is same, so log it
            if (string.Equals(FileHelper.RemoveLastDirChar(srcFile), FileHelper.RemoveLastDirChar(destFile), StringComparison.OrdinalIgnoreCase))
                logs.Add(new LogInfo(cmd, LogState.Warning, "Cannot rename to same filename"));
            else
            {
                File.Move(srcFile, destFile);
                logs.Add(new LogInfo(cmd, LogState.Success, $"[{rawSrcFile}] moved to [{rawDestFile}]"));
            }

            return logs;
        }

        /// <summary>
        /// FileCreateBlank,<FileName>[,PRESERVE][,NOWARN][,UTF8 | UTF16LE | UTF16BE | ANSI]
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        private List<LogInfo> CmdFileCreateBlank(BakeryCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();
            // Necessary operand : 1, optional operand : 3
            const int necessaryOperandNum = 1;
            const int optionalOperandNum = 3;

            if (cmd.Operands.Count < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);
            else if (necessaryOperandNum + optionalOperandNum < cmd.Operands.Count)
                throw new InvalidOperandException("Too many operands", cmd);

            string fileName = UnescapeString(ExpandVariables(cmd.Operands[0]));
            string rawFileName = cmd.Operands[0];

            bool preserve = false;
            bool noWarn = false;
            Encoding encoding = null;

            for (int i = necessaryOperandNum; i < cmd.Operands.Count; i++)
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
                    logs.Add(new LogInfo(cmd, noWarn ? LogState.Ignore : LogState.Warning, $"[{rawFileName}] will be overwritten"));
            }

            try
            {
                FileStream fs = new FileStream(fileName, preserve ? FileMode.CreateNew : FileMode.Create, FileAccess.Write, FileShare.Write);
                FileHelper.WriteTextBOM(fs, encoding).Close();
                logs.Add(new LogInfo(cmd, LogState.Success, $"Created blank text file [{rawFileName}]"));
            }
            catch (IOException)
            {
                if (preserve)
                    logs.Add(new LogInfo(cmd, noWarn ? LogState.Ignore : LogState.Warning, $"Cannot overwrite [{rawFileName}]"));
            }

            return logs;
        }

        /// <summary>
        /// FileByteExtract,<SrcFiles>,<DestFile>,<Signature>,<CopyLength> 
        /// </summary>
        /// <remarks>
        /// Wildcard supported in <SrcFiles>
        /// <DestFile> will regarded as file
        /// </remarks>
        /// <param name="cmd"></param>
        /// <returns></returns>
        private List<LogInfo> CmdFileByteExtract(BakeryCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();
            // Necessary operand : 4, optional operand : 0
            const int necessaryOperandNum = 4;
            const int optionalOperandNum = 0;

            if (cmd.Operands.Count < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);
            else if (necessaryOperandNum + optionalOperandNum < cmd.Operands.Count)
                throw new InvalidOperandException("Too many operands", cmd);

            string srcFiles = UnescapeString(ExpandVariables(cmd.Operands[0]));
            string rawSrcFiles = cmd.Operands[0];
            string destFile = UnescapeString(ExpandVariables(cmd.Operands[1]));
            string rawDestFile = cmd.Operands[1];
            byte[] signature;
            if (!NumberHelper.ParseHexStringToBytes(cmd.Operands[2], out signature))
                throw new InvalidOperandException($"[CopyLength] must be valid hex string, Ex) A0B1C2", cmd);
            long copyLength;
            if (!NumberHelper.ParseInt64(cmd.Operands[3], out copyLength))
                throw new InvalidOperandException($"[CopyLength] must be valid integer", cmd);

            // Check srcFileName contains wildcard
            bool srcContainWildcard = true;
            if (srcFiles.IndexOfAny(new char[] { '*', '?' }) == -1) // No wildcard
                srcContainWildcard = false;

            // Check destPath already exists
            if (Directory.Exists(destFile))
                throw new InvalidOperandException($"[{destFile}] is existing directory", cmd);
            else if (File.Exists(destFile))
                logs.Add(new LogInfo(cmd, LogState.Warning, $"[{destFile}] can be overwritten"));

            string parentDir = Path.GetDirectoryName(destFile);
            if (!Directory.Exists(parentDir) && !string.Equals(parentDir, string.Empty, StringComparison.Ordinal))
                Directory.CreateDirectory(parentDir);

            if (srcContainWildcard)
            {               
                string srcDirToFind = FileHelper.GetDirNameEx(srcFiles);
                string rawSrcDirToFind = FileHelper.GetDirNameEx(rawSrcFiles);
                string[] listToCopy = Directory.GetFiles(srcDirToFind, Path.GetFileName(srcFiles));

                if (0 < listToCopy.Length)
                    logs.Add(new LogInfo(cmd, LogState.Success, $"[{srcFiles}] will be searched"));
                int idx = 0;
                foreach (string searchedFilePath in listToCopy)
                {
                    idx++;
                    long offset;
                    bool result = FileHelper.FindByteSignature(searchedFilePath, signature, out offset);
                    if (result)
                    {
                        logs.Add(new LogInfo(cmd, LogState.Success, $"Signature found at [{Path.Combine(rawSrcDirToFind, Path.GetFileName(searchedFilePath))}]'s offset [{offset}]", cmd.Depth + 1));
                        FileHelper.CopyOffset(searchedFilePath, destFile, offset, copyLength);
                        logs.Add(new LogInfo(cmd, LogState.Success, $"Sucessfully copied [{copyLength}] bytes to [{rawDestFile}]", cmd.Depth + 1));
                        break;
                    }
                    else
                    {
                        logs.Add(new LogInfo(cmd, LogState.Ignore, $"Signature not found from [{Path.Combine(rawSrcDirToFind, Path.GetFileName(searchedFilePath))}]", cmd.Depth + 1));
                    }
                }
                if (0 < listToCopy.Length)
                    logs.Add(new LogInfo(cmd, LogState.Success, $"[{idx}] of [{listToCopy.Length}] files searched"));
                else if (listToCopy.Length == 0)
                    logs.Add(new LogInfo(cmd, LogState.Warning, $"Files matches wildcard [{rawSrcFiles}] not found"));
            }
            else
            {
                long offset;
                bool result = FileHelper.FindByteSignature(srcFiles, signature, out offset);
                if (result)
                {
                    logs.Add(new LogInfo(cmd, LogState.Success, $"Signature found at [{rawSrcFiles}]'s offset [{offset}]"));
                    FileHelper.CopyOffset(srcFiles, destFile, offset, copyLength);
                    logs.Add(new LogInfo(cmd, LogState.Success, $"Sucessfully copied [{copyLength}] bytes to [{rawDestFile}]"));
                }
                else
                {
                    logs.Add(new LogInfo(cmd, LogState.Ignore, $"Signature not found from [{rawSrcFiles}]"));
                }
            }

            return logs;
        }

        /// <summary>
        /// WebGet,<URL>,<DestPath>,[HASH]
        /// </summary>
        /// <remarks>
        /// [ASK], [TIMEOUT] deprecated
        /// [HASH] : <HashType><HashHex>
        ///   HashType = MD5, SHA1, SHA256, SHA512
        ///   Ex) SHA256,f23530b86b6cfa1f41f46fc4352951fe2773b25791a347383e851c720431ef9a
        /// </remarks>
        /// <param name="cmd"></param>
        /// <returns></returns>
        private List<LogInfo> CmdWebGet(BakeryCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();
            // Necessary operand : 2, optional operand : 1
            const int necessaryOperandNum = 2;
            const int optionalOperandNum = 2;

            if (cmd.Operands.Count < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);
            else if (necessaryOperandNum + optionalOperandNum < cmd.Operands.Count)
                throw new InvalidOperandException("Too many operands", cmd);

            string urlString = UnescapeString(ExpandVariables(cmd.Operands[0]));
            string rawUrlString = cmd.Operands[0];
            string destPath = UnescapeString(ExpandVariables(cmd.Operands[1]));
            string rawDestPath = cmd.Operands[1];

            Uri uri = new Uri(urlString);

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

            HashType hashType = HashType.None;
            string hashString = string.Empty;
            for (int i = necessaryOperandNum; i < cmd.Operands.Count; i++)
            {
                string operand = cmd.Operands[i];
                if (string.Equals(operand, "ASK", StringComparison.OrdinalIgnoreCase)) // for compability with WB082
                { }
                else if (string.Equals(operand, "TIMEOUT", StringComparison.OrdinalIgnoreCase)) // for compability with WB082
                { }
                else if (string.Equals(operand, "MD5", StringComparison.OrdinalIgnoreCase))
                {
                    if (!(i + 1 < cmd.Operands.Count))
                        throw new InvalidOperandException($"Hash string is not provided", cmd);
                    hashString = cmd.Operands[i + 1];
                    if (!StringHelper.IsHex(hashString))
                        throw new InvalidOperandException($"[{hashString}] is not a valid hash string", cmd);
                    if (hashString.Length != HashHelper.MD5Len * 2) // * 2 for Hexadecimal
                        throw new InvalidOperandException($"[{hashString}] is not a valid MD5 hash string", cmd);
                    hashType = HashType.MD5;
                    i++;
                }
                else if (string.Equals(operand, "SHA1", StringComparison.OrdinalIgnoreCase))
                {
                    if (!(i + 1 < cmd.Operands.Count))
                        throw new InvalidOperandException($"Hash string is not provided", cmd);
                    hashString = cmd.Operands[i + 1];
                    if (!StringHelper.IsHex(hashString))
                        throw new InvalidOperandException($"[{hashString}] is not a valid hash string", cmd);
                    if (hashString.Length != HashHelper.SHA1Len * 2) // * 2 for Hexadecimal
                        throw new InvalidOperandException($"[{hashString}] is not a valid SHA1 hash string", cmd);
                    hashType = HashType.SHA1;
                    i++;
                }
                else if (string.Equals(operand, "SHA256", StringComparison.OrdinalIgnoreCase))
                {
                    if (!(i + 1 < cmd.Operands.Count))
                        throw new InvalidOperandException($"Hash string is not provided", cmd);
                    hashString = cmd.Operands[i + 1];
                    if (!StringHelper.IsHex(hashString))
                        throw new InvalidOperandException($"[{hashString}] is not a valid hash string", cmd);
                    if (hashString.Length != HashHelper.SHA256Len * 2) // * 2 for Hexadecimal
                        throw new InvalidOperandException($"[{hashString}] is not a valid SHA256 hash string", cmd);
                    hashType = HashType.SHA256;
                    i++;
                }
                else if (string.Equals(operand, "SHA384", StringComparison.OrdinalIgnoreCase))
                {
                    if (!(i + 1 < cmd.Operands.Count))
                        throw new InvalidOperandException($"Hash string is not provided", cmd);
                    hashString = cmd.Operands[i + 1];
                    if (!StringHelper.IsHex(hashString))
                        throw new InvalidOperandException($"[{hashString}] is not a valid hash string", cmd);
                    if (hashString.Length != HashHelper.SHA384Len * 2) // * 2 for Hexadecimal
                        throw new InvalidOperandException($"[{hashString}] is not a valid SHA384 hash string", cmd);
                    hashType = HashType.SHA384;
                    i++;
                }
                else if (string.Equals(operand, "SHA512", StringComparison.OrdinalIgnoreCase))
                {
                    if (!(i + 1 < cmd.Operands.Count))
                        throw new InvalidOperandException($"Hash string is not provided", cmd);
                    hashString = cmd.Operands[i + 1];
                    if (!StringHelper.IsHex(hashString))
                        throw new InvalidOperandException($"[{hashString}] is not a valid hash string", cmd);
                    if (hashString.Length != HashHelper.SHA512Len * 2) // * 2 for Hexadecimal
                        throw new InvalidOperandException($"[{hashString}] is not a valid SHA512 hash string", cmd);
                    hashType = HashType.SHA512;
                    i++;
                }
                else
                    throw new InvalidOperandException($"Invalid operand [{operand}]", cmd);
            }

            if (destPathExists)
            {
                if (destPathIsDir)
                {
                    string urlFileName = uri.Segments[uri.Segments.Length - 1];
                    if (string.Equals(urlFileName, string.Empty, StringComparison.OrdinalIgnoreCase))
                        uri = new Uri(uri, "index");
                    destPath = Path.Combine(destPath, urlFileName);
                    rawDestPath = Path.Combine(rawDestPath, urlFileName);
                }
                else
                {
                    logs.Add(new LogInfo(cmd, LogState.Success, $"[{rawDestPath}] will be overwritten"));
                }
            }

            string parentDir = Path.GetDirectoryName(destPath);
            if (!Directory.Exists(parentDir) && !string.Equals(parentDir, string.Empty, StringComparison.Ordinal))
                Directory.CreateDirectory(parentDir);

            Task<Stream> t = WebHelper.GetStreamAsync(urlString);
            Stream read = t.Result;
            FileHelper.CopyStream(read, destPath);
            read.Close();

            if (hashType == HashType.None)
            {
                logs.Add(new LogInfo(cmd, LogState.Success, $"Sucessfully downloaded [{urlString}] into [{rawDestPath}]"));
            }
            else
            {
                FileStream file = new FileStream(destPath, FileMode.Open, FileAccess.Read);
                string compareHash = HashHelper.CalcHashString(hashType, file);
                file.Close();
                if (string.Equals(compareHash, hashString, StringComparison.OrdinalIgnoreCase)) // Success
                    logs.Add(new LogInfo(cmd, LogState.Success, $"Sucessfully downloaded [{urlString}] into [{rawDestPath}], integrity checked"));
                else // Failure
                {
                    File.Delete(destPath);
                    logs.Add(new LogInfo(cmd, LogState.Success, $"Downloaded [{urlString}], but integrity is broken"));
                }
            }

            return logs;
        }
    }
}