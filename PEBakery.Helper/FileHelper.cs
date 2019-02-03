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
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace PEBakery.Helper
{
    #region FileHelper
    /// <summary>
    /// Contains static helper methods.
    /// </summary>
    public static class FileHelper
    {
        #region Get Program's Property
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
        #endregion

        #region RemoveLastDirChar
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
        #endregion

        #region Temp Path
        private static readonly object _tempPathLock = new object();
        private static readonly RNGCryptoServiceProvider _secureRandom = new RNGCryptoServiceProvider();
        private static int _tempCounter = 0;

        private static string _baseTempDir = null;
        public static string BaseTempDir()
        {
            lock (_tempPathLock)
            {
                if (_baseTempDir == null)
                {
                    byte[] randBytes = new byte[4];
                    string systemTempDir = Path.GetTempPath();

                    do
                    {
                        // Get 4B of random 
                        _secureRandom.GetBytes(randBytes);
                        uint randInt = BitConverter.ToUInt32(randBytes, 0);

                        _baseTempDir = Path.Combine(systemTempDir, $"PEBakery_{randInt:X8}");
                    }
                    while (Directory.Exists(_baseTempDir) || File.Exists(_baseTempDir));

                    Directory.CreateDirectory(_baseTempDir);
                }

                return _baseTempDir;
            }
        }

        /// <summary>
        /// Delete BaseTempDir from disk. Call this method before termination of an application.
        /// </summary>
        public static void CleanBaseTempDir()
        {
            lock (_tempPathLock)
            {
                if (_baseTempDir == null)
                    return;

                if (Directory.Exists(_baseTempDir))
                    Directory.Delete(_baseTempDir, true);
                _baseTempDir = null;
            }
        }

        public static string GetTempDir()
        {
            // Never call BaseTempDir in the _tempPathLock, it would cause a deadlock!
            string baseTempDir = BaseTempDir();

            lock (_tempPathLock)
            {
                string tempDir;
                do
                {
                    int counter = Interlocked.Increment(ref _tempCounter);
                    tempDir = Path.Combine(baseTempDir, $"d{counter:X8}");
                }
                while (Directory.Exists(tempDir) || File.Exists(tempDir));

                Directory.CreateDirectory(tempDir);
                return tempDir;
            }
        }

        /// <summary>
        /// Create temp file with synchronization
        /// </summary>
        public static string GetTempFile(string ext = null)
        {
            // Never call BaseTempDir in the _tempPathLock, it would cause a deadlock!
            string baseTempDir = BaseTempDir();

            // Use tmp by default / Remove '.' from ext
            ext = ext == null ? "tmp" : ext.Trim('.');

            lock (_tempPathLock)
            {
                string tempFile;
                do
                {
                    int counter = Interlocked.Increment(ref _tempCounter);
                    tempFile = Path.Combine(baseTempDir, ext.Length == 0 ? $"f{counter:X8}" : $"f{counter:X8}.{ext}");
                }
                while (Directory.Exists(tempFile) || File.Exists(tempFile));

                File.Create(tempFile).Dispose();
                return tempFile;
            }
        }
        #endregion

        #region Path Operations
        /// <summary>
        /// Extends Path.GetDirectoryName().
        /// If returned dir path is empty, change it to "."
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string GetDirNameEx(string path)
        {
            string dirName = Path.GetDirectoryName(path);
            if (dirName == null)
                return null;
            if (dirName.Length == 0) // e.g. Hello.txt
                return "."; // Consider path as [.\Hello.txt].
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

        public static string RemoveFirstDir(string src, int removeNumber)
        {
            int idx = src.IndexOf(Path.DirectorySeparatorChar);
            return idx == -1 ? null : src.Substring(idx + 1);
        }

        public static string RemoveLastDir(string src, int removeNumber)
        {
            int idx = src.LastIndexOf(Path.DirectorySeparatorChar);
            return idx == -1 ? null : src.Substring(0, idx);
        }
        #endregion

        #region GetFileSize
        public static long GetFileSize(string srcFile)
        {
            FileInfo info = new FileInfo(srcFile);
            return info.Length;
        }
        #endregion

        #region File Byte Operations
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
        #endregion

        #region DirectoryCopy
        public struct DirCopyOptions
        {
            public bool CopySubDirs;
            public bool Overwrite;
            public string FileWildcard;
            public IProgress<string> Progress;
        }

        /// <summary>
        /// Copy directory.
        /// </summary>
        public static void DirCopy(string srcDir, string destDir, DirCopyOptions opts)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dirInfo = new DirectoryInfo(srcDir);

            if (!dirInfo.Exists)
                throw new DirectoryNotFoundException($"Directory [{srcDir}] does not exist");

            // Get the files in the directory and copy them to the new location.
            try
            {
                FileInfo[] files;
                if (opts.FileWildcard == null)
                    files = dirInfo.GetFiles();
                else
                    files = dirInfo.GetFiles(opts.FileWildcard);

                // If the destination directory doesn't exist, create it.
                if (!Directory.Exists(destDir))
                    Directory.CreateDirectory(destDir);

                foreach (FileInfo f in files)
                {
                    opts.Progress?.Report(f.FullName);

                    string destPath = Path.Combine(destDir, f.Name);
                    f.CopyTo(destPath, opts.Overwrite);
                }
            }
            catch (UnauthorizedAccessException) { } // Ignore UnauthorizedAccessException

            // If copying subdirectories, copy them and their contents to new location.
            if (opts.CopySubDirs)
            {
                DirectoryInfo[] dirs;
                try { dirs = dirInfo.GetDirectories(); }
                catch (UnauthorizedAccessException) { return; } // Ignore UnauthorizedAccessException

                foreach (DirectoryInfo d in dirs)
                {
                    string tempPath = Path.Combine(destDir, d.Name);
                    DirCopy(d.FullName, tempPath, opts);
                }
            }
        }
        #endregion

        #region GetDirectoriesEx
        public static string[] GetDirsEx(string dirPath, string searchPattern, SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            if (dirPath == null) throw new ArgumentNullException(nameof(dirPath));

            DirectoryInfo dirInfo = new DirectoryInfo(dirPath);
            if (!dirInfo.Exists)
                throw new DirectoryNotFoundException($"Source directory does not exist or could not be found: {dirPath}");

            List<string> foundDirs = new List<string>();
            return InternalGetDirsEx(dirInfo, searchPattern, searchOption, foundDirs).ToArray();
        }

        private static List<string> InternalGetDirsEx(DirectoryInfo dirInfo, string searchPattern, SearchOption searchOption, List<string> foundDirs)
        {
            if (dirInfo == null) throw new ArgumentNullException(nameof(dirInfo));
            if (searchPattern == null) throw new ArgumentNullException(nameof(searchPattern));

            try
            {
                DirectoryInfo[] dirs = dirInfo.GetDirectories();
                foreach (DirectoryInfo dir in dirs)
                {
                    foundDirs.Add(dir.FullName);
                    if (searchOption == SearchOption.AllDirectories)
                        InternalGetDirsEx(dir, searchPattern, searchOption, foundDirs);
                }
            }
            catch (UnauthorizedAccessException) { } // Ignore UnauthorizedAccessException

            return foundDirs;
        }
        #endregion

        #region GetFilesEx
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
        #endregion

        #region DirectoryDeleteEx
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
        #endregion

        #region DOS 8.3 Path
        private const int MaxLongPath = 32767;
        private const string LongPathPrefix = @"\\?\";
        private const string UseLegacyPathHandling = @"Switch.System.IO.UseLegacyPathHandling";

        // Success of this depends on HKEY_LOCAL_MACHINE\System\CurrentControlSet\Control\FileSystem\NtfsDisable8dot3NameCreation
        public static string GetShortPath(string longPath)
        {
            // Is long path (~32768) support enabled in .Net?
            bool isLongPathDisabled;
            try
            {
                // false - 32767, true - 260
                AppContext.TryGetSwitch(UseLegacyPathHandling, out isLongPathDisabled);
            }
            catch
            {
                isLongPathDisabled = true;
            }

            if (!isLongPathDisabled)
            {
                if (!longPath.StartsWith(LongPathPrefix, StringComparison.Ordinal))
                    longPath = LongPathPrefix + longPath;
            }

            StringBuilder shortPath = new StringBuilder(MaxLongPath);
            NativeMethods.GetShortPathName(longPath, shortPath, MaxLongPath);

            string str = shortPath.ToString();
            if (!isLongPathDisabled)
            {
                if (str.StartsWith(LongPathPrefix, StringComparison.Ordinal))
                    return str.Substring(LongPathPrefix.Length);
            }
            return str;
        }

        public static string GetLongPath(string shortPath)
        {
            // Is long path (~32768) support enabled in .Net?
            bool isLongPathDisabled;
            try
            {
                AppContext.TryGetSwitch(UseLegacyPathHandling, out isLongPathDisabled);
            }
            catch
            {
                isLongPathDisabled = true;
            }
            if (!isLongPathDisabled)
            {
                if (!shortPath.StartsWith(LongPathPrefix, StringComparison.Ordinal))
                    shortPath = LongPathPrefix + shortPath;
            }

            StringBuilder longPath = new StringBuilder(MaxLongPath);
            NativeMethods.GetLongPathName(shortPath, longPath, MaxLongPath);

            string str = longPath.ToString();
            if (!isLongPathDisabled)
            {
                if (str.StartsWith(LongPathPrefix, StringComparison.Ordinal))
                    return str.Substring(LongPathPrefix.Length);
            }
            return str;
        }
        #endregion

        #region Hyperlink and ShellExecute Alternative
        /// <summary>
        /// Open URI with default browser without Administrator privilege.
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
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

        /// <summary>
        /// ShellExecute without Administrator privilege.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
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
        #endregion
    }
    #endregion
}
