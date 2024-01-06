﻿/*
    Copyright (C) 2016-2023 Hajin Jang
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
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
            if (Assembly.GetExecutingAssembly().GetName().Version is Version ver)
                return ver;
            else
                return new Version(0, 0); // Error
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
        private static int _tempPathCounter = 0;
        private static readonly object TempPathLock = new();
        private static readonly RandomNumberGenerator SecureRandom = RandomNumberGenerator.Create();

        private static FileStream? _lockFileStream = null;
        private static string? _baseTempDir = null;
        public static string BaseTempDir()
        {
            lock (TempPathLock)
            {
                if (_baseTempDir != null)
                    return _baseTempDir;

                byte[] randBytes = new byte[4];
                string systemTempDir = Path.GetTempPath();

                do
                {
                    // Get 4B of random 
                    SecureRandom.GetBytes(randBytes);
                    uint randInt = BitConverter.ToUInt32(randBytes, 0);

                    _baseTempDir = Path.Combine(systemTempDir, $"PEBakery_{randInt:X8}");
                }
                while (Directory.Exists(_baseTempDir) || File.Exists(_baseTempDir));

                // Create base temp directory
                Directory.CreateDirectory(_baseTempDir);

                // Lock base temp directory
                string lockFilePath = Path.Combine(_baseTempDir, "f.lock");
                _lockFileStream = new FileStream(lockFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

                return _baseTempDir;
            }
        }

        /// <summary>
        /// Delete BaseTempDir from disk. Call this method before termination of an application.
        /// </summary>
        public static void CleanBaseTempDir()
        {
            lock (TempPathLock)
            {
                if (_baseTempDir == null)
                    return;

                _lockFileStream?.Dispose();

                if (Directory.Exists(_baseTempDir))
                    Directory.Delete(_baseTempDir, true);
                _baseTempDir = null;
            }
        }

        /// <summary>
        /// Create temp directory with synchronization.
        /// Returned temp directory path is virtually unique per call.
        /// </summary>
        /// <remarks>
        /// Returned temp file path is unique per call unless this method is called uint.MaxValue times.
        /// </remarks>
        public static string GetTempDir()
        {
            // Never call BaseTempDir in the _tempPathLock, it would cause a deadlock!
            string baseTempDir = BaseTempDir();

            lock (TempPathLock)
            {
                string tempDir;
                do
                {
                    int counter = Interlocked.Increment(ref _tempPathCounter);
                    tempDir = Path.Combine(baseTempDir, $"d{counter:X8}");
                }
                while (Directory.Exists(tempDir) || File.Exists(tempDir));

                Directory.CreateDirectory(tempDir);
                return tempDir;
            }
        }

        /// <summary>
        /// Create temp file with synchronization.
        /// Returned temp file path is virtually unique per call.
        /// </summary>
        /// <remarks>
        /// Returned temp file path is unique per call unless this method is called uint.MaxValue times.
        /// </remarks>
        public static string GetTempFile(string? ext = null)
        {
            return GetTempFile(null, ext);
        }

        /// <summary>
        /// Create temp file with synchronization.
        /// Returned temp file path is virtually unique per call.
        /// </summary>
        /// <remarks>
        /// Returned temp file path is unique per call unless this method is called uint.MaxValue times.
        /// </remarks>
        public static string GetTempFile(string? baseName, string? ext)
        {
            // Never call BaseTempDir in the _tempPathLock, it would cause a deadlock!
            string baseTempDir = BaseTempDir();

            // Use tmp by default / Remove '.' from ext
            if (ext == null)
                ext = "tmp";
            else
                ext = ext.Trim('.');

            lock (TempPathLock)
            {
                string tempFile;
                do
                {
                    int counter = Interlocked.Increment(ref _tempPathCounter);
                    string fileName;
                    if (baseName == null)
                        fileName = $"f{counter:X8}";
                    else
                        fileName = $"{baseName}_f{counter:X8}";
                    if (0 < ext.Length) // Not empty
                        fileName += $".{ext}";

                    tempFile = Path.Combine(baseTempDir, fileName);

                }
                while (Directory.Exists(tempFile) || File.Exists(tempFile));

                File.Create(tempFile).Dispose();
                return tempFile;
            }
        }

        /// <summary>
        /// Reserve temp file path with synchronization.
        /// Returned temp file path is virtually unique per call.
        /// </summary>
        /// <remarks>
        /// Returned temp file path is unique per call unless this method is called uint.MaxValue times.
        /// </remarks>
        public static string ReserveTempFile(string? ext = null)
        {
            // Never call BaseTempDir in the _tempPathLock, it would cause a deadlock!
            string baseTempDir = BaseTempDir();

            // Use tmp by default / Remove '.' from ext
            ext = ext == null ? "tmp" : ext.Trim('.');

            lock (TempPathLock)
            {
                string tempFile;
                do
                {
                    int counter = Interlocked.Increment(ref _tempPathCounter);
                    tempFile = Path.Combine(baseTempDir, ext.Length == 0 ? $"f{counter:X8}" : $"f{counter:X8}.{ext}");
                }
                while (Directory.Exists(tempFile) || File.Exists(tempFile));
                return tempFile;
            }
        }
        #endregion

        #region Path Operations
        /// <summary>
        /// Extends Path.GetDirectoryName().
        /// Prevents returning of null, and also allows wildcard in filename.
        /// </summary>
        /// <returns></returns>
        public static string GetDirNameEx(string path)
        {
            int lastDirSepIdx = path.LastIndexOf('\\');
            if (lastDirSepIdx == -1)
            { // Ex) ABC.*, ABC.so -> No directory separator
                return ".";
            }
            else
            { // Ex) AB\CD\EF.so, AB\?.exe
                return path[..lastDirSepIdx];
            }
        }

        /// <summary>
        /// Replace src with dest. 
        /// </summary>
        /// <param name="srcPath"></param>
        /// <param name="destPath"></param>
        public static void FileReplaceEx(string srcPath, string destPath)
        {
            // File.Replace throws IOException if src and dest are located in different volume.
            // To decreate amount of exception throwen, check drive by ourself and use File.Copy as fallback.
            string fullSrcPath = Path.GetFullPath(srcPath);
            string fullDestPath = Path.GetFullPath(destPath);

            string? srcDrive = Path.GetPathRoot(fullSrcPath);
            string? destDrive = Path.GetPathRoot(fullDestPath);
            if (srcDrive is not null && destDrive is not null &&
                srcDrive.Equals(destDrive, StringComparison.Ordinal))
            {
                try
                {
                    // File.Copy removes ACL and ADS.
                    // Instead, use File.Replace.
                    File.Replace(srcPath, destPath, null);
                }
                catch (IOException)
                { // Failsafe
                    // File.Replace throws IOException if src and dest files are in different volume.
                    // In this case, try File.Copy as fallback.
                    File.Copy(srcPath, destPath, true);
                    File.Delete(srcPath);
                }
            }
            else
            {
                File.Copy(srcPath, destPath, true);
                File.Delete(srcPath);
            }
        }
        #endregion

        #region GetFileSize
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long GetFileSize(string srcFile) => new FileInfo(srcFile).Length;
        #endregion

        #region IsPathNonExistDir
        /// <summary>
        /// Is the given path is really non-existing directory path? 
        /// Ex) D:\Test\
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPathNonExistDir(string path)
        {
            return !Directory.Exists(path) && path.EndsWith("\\", StringComparison.Ordinal) && Path.GetFileName(path).Length == 0;
        }
        #endregion

        #region File Byte Operations
        public static bool FindByteSignature(string srcFile, byte[] signature, out long offset)
        {
            long size = GetFileSize(srcFile);

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

        #region DirCopy
        /// <summary>
        /// Copy directory.
        /// </summary>
        public static void DirCopy(string srcDir, string destDir, DirCopyOptions opts)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo root = new DirectoryInfo(srcDir);

            if (!root.Exists)
                throw new DirectoryNotFoundException($"Directory [{srcDir}] does not exist");

            Queue<(DirectoryInfo SrcDir, string DestDir)> q = new Queue<(DirectoryInfo, string)>();
            q.Enqueue((root, destDir));

            while (0 < q.Count)
            {
                (DirectoryInfo di, string subDestDir) = q.Dequeue();

                try
                {
                    FileInfo[] files;
                    if (opts.FileWildcard == null)
                        files = di.GetFiles();
                    else
                        files = di.GetFiles(opts.FileWildcard);

                    // If the destination directory doesn't exist, create it.
                    if (!Directory.Exists(subDestDir))
                        Directory.CreateDirectory(subDestDir);

                    foreach (FileInfo f in files)
                    {
                        opts.Progress?.Report(f.FullName);

                        string destPath = Path.Combine(subDestDir, f.Name);
                        f.CopyTo(destPath, opts.Overwrite);
                    }
                }
                catch (UnauthorizedAccessException) { /* Ignore UnauthorizedAccessException */ }

                // If copying subdirectories, copy them and their contents to new location.
                if (opts.CopySubDirs)
                {
                    DirectoryInfo[] dirs;
                    try
                    {
                        dirs = di.GetDirectories();
                    }
                    catch (UnauthorizedAccessException)
                    { // Ignore UnauthorizedAccessException
                        continue;
                    }

                    foreach (DirectoryInfo d in dirs)
                    {
                        string newDestDir = Path.Combine(subDestDir, d.Name);
                        q.Enqueue((d, newDestDir));
                    }
                }
            }
        }
        #endregion

        #region GetDirsEx
        /// <summary>
        /// Search for directories while ignoring UnauthorizedAccessException
        /// </summary>
        public static string[] GetDirsEx(string dirPath, string searchPattern)
        {
            return GetDirsEx(dirPath, searchPattern, SearchOption.TopDirectoryOnly);
        }

        /// <summary>
        /// Search for directories while ignoring UnauthorizedAccessException
        /// </summary>
        public static string[] GetDirsEx(string dirPath, string searchPattern, SearchOption searchOption)
        {
            if (dirPath == null) throw new ArgumentNullException(nameof(dirPath));

            DirectoryInfo root = new DirectoryInfo(dirPath);
            if (!root.Exists)
                throw new DirectoryNotFoundException($"Source directory does not exist or could not be found: {dirPath}");

            List<string> foundDirs = new List<string>();
            Stack<DirectoryInfo> stack = new Stack<DirectoryInfo>();
            stack.Push(root);
            while (0 < stack.Count)
            {
                DirectoryInfo di = stack.Pop();
                try
                {
                    DirectoryInfo[] subDirs = di.GetDirectories(searchPattern);
                    foreach (DirectoryInfo subDir in subDirs)
                    {
                        foundDirs.Add(subDir.FullName);
                        if (searchOption == SearchOption.AllDirectories)
                            stack.Push(subDir);
                    }
                }
                catch (UnauthorizedAccessException) { } /* Ignore UnauthorizedAccessException */
            }

            return foundDirs.ToArray();
        }
        #endregion

        #region GetFilesEx
        /// <summary>
        /// Search for files while ignoring UnauthorizedAccessException
        /// </summary>
        public static string[] GetFilesEx(string dirPath, string searchPattern)
        {
            return GetFilesEx(dirPath, searchPattern, SearchOption.TopDirectoryOnly);
        }

        /// <summary>
        /// Search for files while ignoring UnauthorizedAccessException
        /// </summary>
        public static string[] GetFilesEx(string dirPath, string searchPattern, SearchOption searchOption)
        {
            if (dirPath == null)
                throw new ArgumentNullException(nameof(dirPath));
            if (searchPattern == null)
                throw new ArgumentNullException(nameof(searchPattern));

            DirectoryInfo root = new DirectoryInfo(dirPath);
            if (!root.Exists)
                throw new DirectoryNotFoundException($"Directory [{dirPath}] does not exist");

            List<string> foundFiles = new List<string>();
            Stack<DirectoryInfo> stack = new Stack<DirectoryInfo>();
            stack.Push(root);

            while (0 < stack.Count)
            {
                // Get the files in the directory and copy them to the new location.
                DirectoryInfo di = stack.Pop();
                try
                {
                    FileInfo[] files = di.GetFiles(searchPattern);
                    foreach (FileInfo file in files)
                    {
                        foundFiles.Add(file.FullName);
                    }
                }
                catch (UnauthorizedAccessException) { /* Ignore UnauthorizedAccessException */ }

                DirectoryInfo[] dirs;
                try
                {
                    dirs = di.GetDirectories();
                }
                catch (UnauthorizedAccessException)
                { // Ignore UnauthorizedAccessException 
                    continue;
                }

                // If copying subdirectories, copy them and their contents to new location.
                if (searchOption == SearchOption.AllDirectories)
                {
                    Array.ForEach(dirs, d => stack.Push(d));
                }
            }

            return foundFiles.ToArray();
        }

        /// <summary>
        /// Search for files and directories while ignoring UnauthorizedAccessException
        /// </summary>
        /// <returns>
        /// An array of ValueTuple of string Path, bool IsDir
        /// </returns>
        public static (string Path, bool IsDir)[] GetDirFilesEx(string dirPath, string searchPattern)
        {
            return GetDirFilesEx(dirPath, searchPattern, SearchOption.TopDirectoryOnly);
        }

        /// <summary>
        /// Search for files and directories while ignoring UnauthorizedAccessException
        /// </summary>
        /// <returns>
        /// An array of ValueTuple of string Path, bool IsDir
        /// </returns>
        public static (string Path, bool IsDir)[] GetDirFilesEx(string dirPath, string searchPattern, SearchOption searchOption)
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
                    // Prevent multiple LINQ evaluation
                    int beforeCount = foundPaths.Count;
                    foundPaths.AddRange(subDir.EnumerateFiles(searchPattern).Select(file => (Path: file.FullName, IsDir: false)));
                    if (beforeCount < foundPaths.Count)
                        fileFound = true;
                }
                catch (UnauthorizedAccessException) { } // Ignore UnauthorizedAccessException

                // Get subdirectories
                if (searchOption == SearchOption.AllDirectories)
                {
                    try
                    {
                        foreach (DirectoryInfo dir in subDir.EnumerateDirectories())
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

        #region DirDeleteEx
        /// <summary>
        /// Delete a directory recursively, including readonly and hidden file/dirs.
        /// </summary>
        /// <param name="path">The path of a directory to delete.</param>
        public static void DirDeleteEx(string path)
        {
            DirectoryInfo root = new DirectoryInfo(path);
            Stack<DirectoryInfo> stack = new Stack<DirectoryInfo>();
            stack.Push(root);
            while (0 < stack.Count)
            {
                DirectoryInfo fol = stack.Pop();
                fol.Attributes &= ~(FileAttributes.Archive | FileAttributes.ReadOnly | FileAttributes.Hidden);
                foreach (DirectoryInfo d in fol.GetDirectories())
                {
                    stack.Push(d);
                }
                foreach (FileInfo f in fol.GetFiles())
                {
                    f.Attributes &= ~(FileAttributes.Archive | FileAttributes.ReadOnly | FileAttributes.Hidden);
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
        public static string? GetShortPath(string longPath)
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

            if (isLongPathDisabled == false)
            {
                if (longPath.StartsWith(LongPathPrefix, StringComparison.Ordinal) == false)
                    longPath = LongPathPrefix + longPath;
            }

            StringBuilder shortPath = new StringBuilder(MaxLongPath);
            int ret = NativeMethods.GetShortPathName(longPath, shortPath, MaxLongPath);
            if (ret == 0) // GetShortPathNameW() failed
                return null;

            string str = shortPath.ToString();
            if (!isLongPathDisabled)
            {
                if (str.StartsWith(LongPathPrefix, StringComparison.Ordinal))
                    return str[LongPathPrefix.Length..];
            }
            return str;
        }

        public static string? GetLongPath(string shortPath)
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
            int ret = NativeMethods.GetLongPathName(shortPath, longPath, MaxLongPath);
            if (ret == 0) // GetLongPathNameW() failed
                return null;

            string str = longPath.ToString();
            if (isLongPathDisabled == false)
            {
                if (str.StartsWith(LongPathPrefix, StringComparison.Ordinal))
                    return str[LongPathPrefix.Length..];
            }
            return str;
        }
        #endregion

        #region Hyperlink and ShellExecute Alternative
        /// <summary>
        /// Open URI with default browser without Administrator privilege.
        /// </summary>
        /// <param name="uri">
        /// An URI to open on system default browser.<br/>
        /// Also supports the local .html file.
        /// </param>
        /// <returns>An instance of ResultReport.</returns>
        public static ResultReport OpenUri(string uri)
        {
            Process? proc = null;
            try
            {
                string quoteUri = uri.Contains(' ') ? $"\"{uri}\"" : uri;
                string? exePath = RegistryHelper.GetDefaultWebBrowserPath(true);
                if (exePath == null)
                {
                    proc = Process.Start(new ProcessStartInfo
                    {
                        UseShellExecute = true,
                        FileName = uri,
                    });
                }
                else
                {
                    Debug.Assert(exePath != null, $"{nameof(exePath)} is null but fallback was not enabled");
                    proc = UACHelper.UACHelper.StartWithShell(new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = quoteUri,
                    });
                }
            }
            catch (Exception e)
            {
                return new ResultReport(e);
            }
            finally
            {
                if (proc != null)
                    proc.Dispose();
            }

            return new ResultReport(true);
        }

        /// <summary>
        /// ShellExecute without Administrator privilege.
        /// </summary>
        /// <param name="docPath">The path of the document to open.</param>
        /// <returns>An instance of ResultReport.</returns>
        public static ResultReport OpenPath(string docPath)
        {
            if (docPath == null)
                throw new ArgumentNullException(nameof(docPath));

            Process? proc = null;
            try
            {
                bool fallback = false;

                string? exePath = null;
                string quotePath = docPath.Contains(' ') ? $"\"{docPath}\"" : docPath;
                string ext = Path.GetExtension(docPath);
                if (ext == null)
                {
                    fallback = true;
                }
                else
                {
                    exePath = RegistryHelper.GetDefaultExecutablePath(ext, true);
                    if (exePath == null)
                        fallback = true;
                }

                if (fallback || exePath == null)
                {
                    proc = Process.Start(new ProcessStartInfo
                    {
                        UseShellExecute = true,
                        FileName = docPath,
                    });
                }
                else
                {
                    proc = UACHelper.UACHelper.StartWithShell(new ProcessStartInfo
                    {
                        UseShellExecute = false,
                        FileName = exePath,
                        Arguments = quotePath,
                    });
                }
            }
            catch (Exception e)
            {
                return new ResultReport(e);
            }
            finally
            {
                if (proc != null)
                    proc.Dispose();
            }

            return new ResultReport(true);
        }

        /// <summary>
        /// ShellExecute without Administrator privilege.
        /// </summary>
        /// <param name="exePath">The path of the executable to open a document.</param>
        /// <param name="docPath">The path of the document to open.</param>
        /// <returns>An instance of ResultReport.</returns>
        public static ResultReport OpenPath(string exePath, string docPath)
        {
            if (exePath == null)
                throw new ArgumentNullException(nameof(exePath));
            if (docPath == null)
                throw new ArgumentNullException(nameof(docPath));

            Process? proc = null;
            try
            {
                try
                {
                    string quotePath = docPath.Contains(' ') ? $"\"{docPath}\"" : docPath;
                    proc = UACHelper.UACHelper.StartWithShell(new ProcessStartInfo
                    {
                        UseShellExecute = false,
                        FileName = exePath,
                        Arguments = quotePath,
                    });
                }
                catch
                {
                    proc = Process.Start(new ProcessStartInfo
                    {
                        UseShellExecute = true,
                        FileName = docPath,
                    });
                }
            }
            catch (Exception e)
            {
                return new ResultReport(e);
            }
            finally
            {
                if (proc != null)
                    proc.Dispose();
            }

            return new ResultReport(true);
        }
        #endregion

        #region WindowsVersion
        /// <summary>
        /// Read Windows version information from kernel32.dll, instead of deprecated Environment.OSVersion.
        /// </summary>
        public static Version WindowsVersion()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Environment.OSVersion is deprecated
                // https://github.com/dotnet/platform-compat/blob/master/docs/DE0009.md
                string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                string kernel32 = Path.Combine(winDir, "System32", "kernel32.dll");
                FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(kernel32);
                int major = fvi.FileMajorPart;
                int minor = fvi.FileMinorPart;
                int build = fvi.FileBuildPart;
                int revision = fvi.FilePrivatePart;
                return new Version(major, minor, build, revision);
            }
            else
            {
                throw new PlatformNotSupportedException();
            }
        }
        #endregion

        #region CheckWin32Path
        public static readonly char[] Win32InvalidFileNameChars = new char[]
        {
            // ASCII control codes
            '\u0000', '\u0001', '\u0002', '\u0003', '\u0004', '\u0005', '\u0006', '\u0007',
            '\u0008', '\u0009', '\u000A', '\u000B', '\u000C', '\u000D', '\u000E', '\u000F',
            '\u0010', '\u0011', '\u0012', '\u0013', '\u0014', '\u0015', '\u0016', '\u0017',
            '\u0018', '\u0019', '\u001A', '\u001B', '\u001C', '\u001D', '\u001E', '\u001F',
            // ASCII Punctuation & Symbols
            ':', '"', '<', '>',  '|',
        };

        public static readonly char[] Win32Wildcard = new char[]
        {
            // ASCII Punctuation & Symbols
            '*', '?',
        };

        public static readonly char[] DirSeparators = new char[]
        {
            // ASCII Punctuation & Symbols
            '\\', '/',
        };

        /// <summary>
        /// Return true if the path is valid Win32 path.
        /// </summary>
        public static bool CheckWin32Path(string path, bool allowDirSep, bool allowWildcard)
        {
            bool valid = !path.Any(ch => Win32InvalidFileNameChars.Contains(ch));
            if (!allowDirSep)
                valid &= !path.Any(ch => Win32Wildcard.Contains(ch));
            if (!allowWildcard)
                valid &= !path.Any(ch => DirSeparators.Contains(ch));
            return valid;
        }
        #endregion

        #region SubRootDirPath
        /// <summary>
        /// Remove the <paramref name="rootDir"/> (aka prefix) from <paramref name="path"/>.<br/>
        /// Ex) path = D:\A\B\C, rootDir = D:\A -> B\C
        /// </summary>
        /// <param name="path">The path to remove root directory path (aka prefix). Must start with <paramref name="rootDir"/>.</param>
        /// <param name="rootDir">The root directory path (aka prefix) to remove from the <paramref name="path"/>.</param>
        /// <returns>
        /// The suffix part of the <paramref name="path"/>.
        /// </returns>
        public static string SubRootDirPath(string path, string rootDir)
        {
            if (!path.StartsWith(rootDir, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"{nameof(path)} must start with {nameof(rootDir)}");
            // Using TrimStart('\\') helps to deal with abnormal path string (such as D:\A\\B\C).
            return path.AsSpan(rootDir.Length).TrimStart('\\').ToString();
        }
        #endregion
    }
    #endregion

    #region DirCopyOptions
    public class DirCopyOptions
    {
        public bool CopySubDirs;
        public bool Overwrite;
        public string? FileWildcard;
        public IProgress<string>? Progress;
    }
    #endregion
}
