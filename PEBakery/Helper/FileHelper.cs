/*
    Copyright (C) 2016-2018 Hajin Jang
    Licensed under MIT License.
 
    MIT License

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace PEBakery.Helper
{
    #region FileHelper
    /// <summary>
    /// Contains static helper methods.
    /// </summary>
    public static class FileHelper
    {
        public static Encoding DetectTextEncoding(string filePath)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return DetectTextEncoding(fs);
            }
        }

        public static Encoding DetectTextEncoding(Stream stream)
        {
            byte[] bom = new byte[3];

            long posBackup = stream.Position;
            stream.Position = 0;
            stream.Read(bom, 0, bom.Length);
            stream.Position = posBackup;

            if (bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
                return Encoding.UTF8;
            if (bom[0] == 0xFF && bom[1] == 0xFE)
                return Encoding.Unicode;
            if (bom[0] == 0xFE && bom[1] == 0xFF)
                return Encoding.BigEndianUnicode;
            return Encoding.Default;
        }

        public static void WriteTextBom(string path, Encoding encoding)
        {
            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                WriteTextBom(fs, encoding);
            }
        }

        public static void WriteTextBom(Stream stream, Encoding encoding)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            long posBackup = stream.Position;
            stream.Position = 0;

            if (encoding.Equals(Encoding.UTF8))
            {
                byte[] bom = { 0xEF, 0xBB, 0xBF };
                stream.Write(bom, 0, bom.Length);
            }
            else if (encoding.Equals(Encoding.Unicode))
            {
                byte[] bom = { 0xFF, 0xFE };
                stream.Write(bom, 0, bom.Length);
            }
            else if (encoding.Equals(Encoding.BigEndianUnicode))
            {
                byte[] bom = { 0xFE, 0xFF };
                stream.Write(bom, 0, bom.Length);
            }
            else if (!encoding.Equals(Encoding.Default))
            { // Unsupported Encoding
                throw new ArgumentException($"[{encoding}] is not supported");
            }

            stream.Position = posBackup;
        }

        public static long TextBomLength(string filePath)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return TextBomLength(fs);
            }
        }

        public static long TextBomLength(Stream stream)
        {
            byte[] bom = new byte[3];

            long posBackup = stream.Position;
            stream.Position = 0;
            stream.Read(bom, 0, bom.Length);
            stream.Position = posBackup;

            if (bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
                return 3;
            if (bom[0] == 0xFF && bom[1] == 0xFE)
                return 2;
            if (bom[0] == 0xFE && bom[1] == 0xFF)
                return 1;
            return 0;
        }

        public static void ConvertTextFileToEncoding(string srcFile, string destFile, Encoding destEnc)
        {
            Encoding srcEnc = FileHelper.DetectTextEncoding(srcFile);
            using (StreamReader r = new StreamReader(srcFile, srcEnc))
            using (StreamWriter w = new StreamWriter(destFile, false, destEnc))
            {
                w.Write(r.ReadToEnd());
            }
        }

        /// <summary>
        /// Read program's version from assembly
        /// </summary>
        /// <returns></returns>
        public static Version GetProgramVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version;
        }

        public static string GetProgramAbsolutePath()
        {
            return RemoveLastDirChar(AppDomain.CurrentDomain.BaseDirectory);
        }

        /// <summary>
        /// Remove last \ in the path.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string RemoveLastDirChar(string path)
        {
            if (Path.GetDirectoryName(path) != null)
                path = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return path;
        }

        /// <summary>
        /// Extends Path.GetDirectoryName().
        /// If returned dir path is empty, change it to "."
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string GetDirNameEx(string path)
        {
            path = FileHelper.RemoveLastDirChar(path);
            string dirName = Path.GetDirectoryName(path);
            if (dirName == string.Empty)
                dirName = ".";
            else if (dirName == null) // path denotes root directory
                dirName = path; // So return root itself
            return dirName;
        }

        /// <summary>
        /// Get Parent directory name, not full path.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string GetParentDirName(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            string dirName = Path.GetDirectoryName(path);
            if (dirName == null)
                return string.Empty;

            int idx = dirName.LastIndexOf(Path.DirectorySeparatorChar);
            if (idx != -1)
                dirName = dirName.Substring(idx + 1, dirName.Length - (idx + 1));
            else
                dirName = string.Empty;

            return dirName;
        }

        /// <summary>
        /// Replace src with dest. 
        /// </summary>
        /// <param name="src"></param>
        /// <param name="dest"></param>
        public static void FileReplaceEx(string src, string dest)
        {
            try
            {
                // File.Copy removes ACL and ADS.
                // Instead, use File.Replace.
                File.Replace(src, dest, null);
            }
            catch (IOException)
            {
                // However, File.Replace throws IOException if src and dest files are in different volume.
                // In this case, try File.Copy as fallback.
                File.Copy(src, dest, true);
                File.Delete(src);
            }
        }

        public static long GetFileSize(string srcFile)
        {
            FileInfo info = new FileInfo(srcFile);
            return info.Length;
        }

        public static bool FindByteSignature(string srcFile, byte[] signature, out long offset)
        {
            long size = FileHelper.GetFileSize(srcFile);

            bool found = false;
            using (MemoryMappedFile mmap = MemoryMappedFile.CreateFromFile(srcFile, FileMode.Open))
            using (MemoryMappedViewAccessor accessor = mmap.CreateViewAccessor())
            {
                byte[] buffer = new byte[signature.Length];
                offset = 0;

                for (long i = 0; i < size - signature.Length; i++)
                {
                    accessor.ReadArray(i, buffer, 0, buffer.Length);
                    if (signature.SequenceEqual(buffer))
                    {
                        found = true;
                        offset = i;
                        break;
                    }
                }
            }

            return found;
        }

        public static string GetTempFileNameEx()
        {
            string path = Path.GetTempFileName();
            File.Delete(path);
            return path;
        }

        public static void CopyOffset(string srcFile, string destFile, long offset, long length)
        {
            using (MemoryMappedFile mmap = MemoryMappedFile.CreateFromFile(srcFile, FileMode.Open))
            using (MemoryMappedViewAccessor accessor = mmap.CreateViewAccessor())
            using (FileStream stream = new FileStream(destFile, FileMode.Create, FileAccess.Write))
            {
                const int block = 4096; // Memory Page is 4KB!
                byte[] buffer = new byte[block];
                for (long i = offset - offset % block; i < offset + length; i += block)
                {
                    if (i == offset - offset % block) // First block
                    {
                        accessor.ReadArray(i, buffer, 0, block);
                        stream.Write(buffer, (int)(offset % block), block - (int)(offset % block));
                    }
                    else if (offset + length - block <= i) // Last block // i < offset + length + block - ((offset + length) % block)
                    {
                        accessor.ReadArray(i, buffer, 0, (int)((offset + length) % block));
                        stream.Write(buffer, 0, (int)((offset + length) % block));
                    }
                    else // Middle. Just copy whole block
                    {
                        accessor.ReadArray(i, buffer, 0, block);
                        stream.Write(buffer, 0, block);
                    }
                }
            }
        }

        /// <summary>
        /// Copy directory.
        /// </summary>
        public static void DirectoryCopy(string srcDir, string destDir, bool copySubDirs, bool overwrite, string fileWildcard = null)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dirInfo = new DirectoryInfo(srcDir);

            if (!dirInfo.Exists)
                throw new DirectoryNotFoundException($"Directory [{srcDir}] does not exist");

            // Get the files in the directory and copy them to the new location.
            try
            {
                FileInfo[] files;
                if (fileWildcard == null)
                    files = dirInfo.GetFiles();
                else
                    files = dirInfo.GetFiles(fileWildcard);

                // If the destination directory doesn't exist, create it.
                if (Directory.Exists(destDir) == false)
                    Directory.CreateDirectory(destDir);

                foreach (FileInfo file in files)
                {
                    string tempPath = Path.Combine(destDir, file.Name);
                    file.CopyTo(tempPath, overwrite);
                }
            }
            catch (UnauthorizedAccessException) { } // Ignore UnauthorizedAccessException

            // If copying subdirectories, copy them and their contents to new location.
            if (copySubDirs)
            {
                DirectoryInfo[] dirs;
                try { dirs = dirInfo.GetDirectories(); }
                catch (UnauthorizedAccessException) { return; } // Ignore UnauthorizedAccessException

                foreach (DirectoryInfo subDir in dirs)
                {
                    string tempPath = Path.Combine(destDir, subDir.Name);
                    DirectoryCopy(subDir.FullName, tempPath, true, overwrite, fileWildcard);
                }
            }
        }

        public static string[] GetDirectoriesEx(string dirPath, string searchPattern, SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            if (dirPath == null) throw new ArgumentNullException(nameof(dirPath));

            DirectoryInfo dirInfo = new DirectoryInfo(dirPath);
            if (!dirInfo.Exists)
                throw new DirectoryNotFoundException($"Source directory does not exist or could not be found: {dirPath}");

            List<string> foundDirs = new List<string>();
            return InternalGetDirectoriesEx(dirInfo, searchPattern, searchOption, foundDirs).ToArray();
        }

        private static List<string> InternalGetDirectoriesEx(DirectoryInfo dirInfo, string searchPattern, SearchOption searchOption, List<string> foundDirs)
        {
            if (dirInfo == null) throw new ArgumentNullException(nameof(dirInfo));
            if (searchPattern == null) throw new ArgumentNullException(nameof(searchPattern));

            try
            {
                var dirs = dirInfo.GetDirectories();
                foreach (DirectoryInfo dir in dirs)
                {
                    foundDirs.Add(dir.FullName);
                    if (searchOption == SearchOption.AllDirectories)
                        InternalGetDirectoriesEx(dir, searchPattern, searchOption, foundDirs);
                }
            }
            catch (UnauthorizedAccessException) { } // Ignore UnauthorizedAccessException

            return foundDirs;
        }

        public static string[] GetFilesEx(string dirPath, string searchPattern, SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            if (dirPath == null) throw new ArgumentNullException(nameof(dirPath));
            if (searchPattern == null) throw new ArgumentNullException(nameof(searchPattern));

            DirectoryInfo dirInfo = new DirectoryInfo(dirPath);
            if (!dirInfo.Exists)
                throw new DirectoryNotFoundException($"Directory [{dirPath}] does not exist");

            List<string> foundFiles = new List<string>();
            return InternalGetFilesEx(dirInfo, searchPattern, searchOption, foundFiles).ToArray();
        }

        private static List<string> InternalGetFilesEx(DirectoryInfo dirInfo, string searchPattern, SearchOption searchOption, List<string> foundFiles)
        {
            if (dirInfo == null) throw new ArgumentNullException(nameof(dirInfo));
            if (searchPattern == null) throw new ArgumentNullException(nameof(searchPattern));

            // Get the files in the directory and copy them to the new location.
            try
            {
                FileInfo[] files = dirInfo.GetFiles(searchPattern);
                foreach (FileInfo file in files)
                {
                    foundFiles.Add(file.FullName);
                }
            }
            catch (UnauthorizedAccessException) { } // Ignore UnauthorizedAccessException

            DirectoryInfo[] dirs;
            try { dirs = dirInfo.GetDirectories(); }
            catch (UnauthorizedAccessException) { return foundFiles; } // Ignore UnauthorizedAccessException

            // If copying subdirectories, copy them and their contents to new location.
            if (searchOption == SearchOption.AllDirectories)
            {
                foreach (DirectoryInfo subDirInfo in dirs)
                {
                    InternalGetFilesEx(subDirInfo, searchPattern, searchOption, foundFiles);
                }
            }

            return foundFiles;
        }

        public static (string Path, bool IsDir)[] GetFilesExWithDirs(string dirPath, string searchPattern, SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            if (dirPath == null) throw new ArgumentNullException(nameof(dirPath));
            if (searchPattern == null) throw new ArgumentNullException(nameof(searchPattern));

            DirectoryInfo dirInfo = new DirectoryInfo(dirPath);
            if (!dirInfo.Exists)
                throw new DirectoryNotFoundException($"Directory [{dirPath}] does not exist");

            List<(string Path, bool IsDir)> foundPaths = new List<(string Path, bool IsDir)>();
            bool InternalGetFilesExWithDirs(DirectoryInfo subDir)
            {
                bool fileFound = false;
                if (subDir == null) throw new ArgumentNullException(nameof(subDir));
                if (searchPattern == null) throw new ArgumentNullException(nameof(searchPattern));

                // Get files
                try
                {
                    var fileInfos = subDir.EnumerateFiles(searchPattern);
                    var files = fileInfos.Select(file => (Path: file.FullName, IsDir: false)).ToArray();
                    if (0 < files.Length)
                    {
                        fileFound = true;
                        foundPaths.AddRange(files);
                    }
                }
                catch (UnauthorizedAccessException) { } // Ignore UnauthorizedAccessException

                // Get subdirectories
                if (searchOption == SearchOption.AllDirectories)
                {
                    try
                    {
                        DirectoryInfo[] dirs = subDir.GetDirectories();
                        foreach (DirectoryInfo dir in dirs)
                            fileFound |= InternalGetFilesExWithDirs(dir);
                    }
                    catch (UnauthorizedAccessException) { } // Ignore UnauthorizedAccessException
                }

                if (fileFound)
                    foundPaths.Add((Path: subDir.FullName, IsDir: true));
                
                return fileFound;
            }

            InternalGetFilesExWithDirs(dirInfo);
            return foundPaths.ToArray();
        }

        public static void DirectoryDeleteEx(string path)
        {
            DirectoryInfo root = new DirectoryInfo(path);
            Stack<DirectoryInfo> fols = new Stack<DirectoryInfo>();
            fols.Push(root);
            while (fols.Count > 0)
            {
                DirectoryInfo fol = fols.Pop();
                fol.Attributes = fol.Attributes & ~(FileAttributes.Archive | FileAttributes.ReadOnly | FileAttributes.Hidden);
                foreach (DirectoryInfo d in fol.GetDirectories())
                {
                    fols.Push(d);
                }
                foreach (FileInfo f in fol.GetFiles())
                {
                    f.Attributes = f.Attributes & ~(FileAttributes.Archive | FileAttributes.ReadOnly | FileAttributes.Hidden);
                    f.Delete();
                }
            }
            root.Delete(true);
        }

        public static Process OpenUri(string uri)
        {
            try
            {
                string protocol = StringHelper.GetUriProtocol(uri);
                string exePath = RegistryHelper.GetDefaultWebBrowserPath(protocol, true);
                string quoteUri = uri.Contains(' ') ? $"\"{uri}\"" : uri;

                return UACHelper.UACHelper.StartWithShell(new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = quoteUri,
                });
            }
            catch
            {
                Process proc = new Process { StartInfo = new ProcessStartInfo(uri) };
                proc.Start();
                return proc;
            }
        }

        public static Process OpenPath(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            try
            {
                string ext = Path.GetExtension(path);
                string exePath = RegistryHelper.GetDefaultExecutablePath(ext, true);
                string quotePath = path.Contains(' ') ? $"\"{path}\"" : path;

                return UACHelper.UACHelper.StartWithShell(new ProcessStartInfo
                {
                    UseShellExecute = false,
                    FileName = exePath,
                    Arguments = quotePath,
                });
            }
            catch
            {
                Process proc = new Process { StartInfo = new ProcessStartInfo(path) };
                proc.Start();
                return proc;
            }
        }

        private const int MAX_LONG_PATH = 32767;
        private const string LONG_PATH_PREFIX = @"\\?\";

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetShortPathName(
            [MarshalAs(UnmanagedType.LPTStr)] string longPath,
            [MarshalAs(UnmanagedType.LPTStr)] StringBuilder shortPath,
            int cchBuffer
        );

        // Success of this depends on HKEY_LOCAL_MACHINE\System\CurrentControlSet\Control\FileSystem\NtfsDisable8dot3NameCreation
        public static string GetShortPath(string longPath)
        {
            // Is long path (~32768) support enabled in .Net?
            bool isLongPathDisabled;
            try
            {
                // false - 32767, true - 260
                AppContext.TryGetSwitch("Switch.System.IO.UseLegacyPathHandling", out isLongPathDisabled);
            }
            catch
            {
                isLongPathDisabled = true;
            }

            if (!isLongPathDisabled)
            {
                if (!longPath.StartsWith(LONG_PATH_PREFIX, StringComparison.Ordinal))
                    longPath = LONG_PATH_PREFIX + longPath;
            }

            StringBuilder shortPath = new StringBuilder(MAX_LONG_PATH);
            GetShortPathName(longPath, shortPath, MAX_LONG_PATH);

            string str = shortPath.ToString();
            if (!isLongPathDisabled)
            {
                if (str.StartsWith(LONG_PATH_PREFIX, StringComparison.Ordinal))
                    return str.Substring(LONG_PATH_PREFIX.Length);
            }
            return str;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetLongPathName(
            [MarshalAs(UnmanagedType.LPTStr)] string shortPath,
            [MarshalAs(UnmanagedType.LPTStr)] StringBuilder longPath,
            int cchBuffer
        );

        public static string GetLongPath(string shortPath)
        {
            // Is long path (~32768) support enabled in .Net?
            bool isLongPathDisabled;
            try
            {
                AppContext.TryGetSwitch("Switch.System.IO.UseLegacyPathHandling", out isLongPathDisabled);
            }
            catch
            {
                isLongPathDisabled = true;
            }
            if (isLongPathDisabled == false)
            {
                if (shortPath.StartsWith(LONG_PATH_PREFIX, StringComparison.Ordinal) == false)
                    shortPath = LONG_PATH_PREFIX + shortPath;
            }

            StringBuilder longPath = new StringBuilder(MAX_LONG_PATH);
            GetLongPathName(shortPath, longPath, MAX_LONG_PATH);

            string str = longPath.ToString();
            if (isLongPathDisabled == false)
            {
                if (str.StartsWith(LONG_PATH_PREFIX, StringComparison.Ordinal))
                    return str.Substring(LONG_PATH_PREFIX.Length);
            }
            return str;
        }
    }
    #endregion
}
