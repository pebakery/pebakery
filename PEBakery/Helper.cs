/*
    Copyright (C) 2016-2017 Hajin Jang
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
using System.Linq;
using System.Text;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Security;
using System.Runtime.ConstrainedExecution;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SevenZipExtractor;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using BetterWin32Errors;
using Svg;
using MahApps.Metro.IconPacks;
using System.Windows.Interop;
using SharpCompress.Common;
using SharpCompress.Writers.Zip;
using SharpCompress.Writers;
using SharpCompress.Readers;
using PEBakery.CabLib;
using Microsoft.Win32.SafeHandles;

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
            else if (bom[0] == 0xFF && bom[1] == 0xFE)
                return Encoding.Unicode;
            else if (bom[0] == 0xFE && bom[1] == 0xFF)
                return Encoding.BigEndianUnicode;
            return Encoding.Default;
        }

        public static void WriteTextBOM(string path, Encoding encoding)
        {
            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                WriteTextBOM(fs, encoding);
            }
        }

        public static void WriteTextBOM(Stream stream, Encoding encoding)
        {
            long posBackup = stream.Position;
            stream.Position = 0;

            if (encoding == Encoding.UTF8)
            {
                byte[] bom = new byte[] { 0xEF, 0xBB, 0xBF };
                stream.Write(bom, 0, bom.Length);
            }
            else if (encoding == Encoding.Unicode)
            {
                byte[] bom = new byte[] { 0xFF, 0xFE };
                stream.Write(bom, 0, bom.Length);
            }
            else if (encoding == Encoding.BigEndianUnicode)
            {
                byte[] bom = new byte[] { 0xFE, 0xFF };
                stream.Write(bom, 0, bom.Length);
            }
            else if (encoding != Encoding.Default)
            { // Unsupported Encoding
                throw new ArgumentException($"[{encoding}] is not supported");
            }

            stream.Position = posBackup;
        }

        public static long TextBOMLength(string filePath)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return TextBOMLength(fs);
            }
        }

        public static long TextBOMLength(Stream stream)
        {
            byte[] bom = new byte[3];

            long posBackup = stream.Position;
            stream.Position = 0;
            stream.Read(bom, 0, bom.Length);
            stream.Position = posBackup;

            if (bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
                return 3;
            else if (bom[0] == 0xFF && bom[1] == 0xFE)
                return 2;
            else if (bom[0] == 0xFE && bom[1] == 0xFF)
                return 2;
            else
                return 0;
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
            return FileHelper.RemoveLastDirChar(AppDomain.CurrentDomain.BaseDirectory);
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
            string dirName = Path.GetDirectoryName(path);
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

            MemoryMappedFile mmap = MemoryMappedFile.CreateFromFile(srcFile, FileMode.Open);
            MemoryMappedViewAccessor accessor = mmap.CreateViewAccessor();

            byte[] buffer = new byte[signature.Length];
            bool found = false;

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

            accessor.Dispose();
            mmap.Dispose();

            return found;
        }

        public static void CopyOffset(string srcFile, string destFile, long offset, long length)
        {
            long size = FileHelper.GetFileSize(srcFile);

            using (MemoryMappedFile mmap = MemoryMappedFile.CreateFromFile(srcFile, FileMode.Open))
            using (MemoryMappedViewAccessor accessor = mmap.CreateViewAccessor())
            using (FileStream stream = new FileStream(destFile, FileMode.Create, FileAccess.Write))
            {
                const int block = 4096; // Memory Page is 4KB!
                byte[] buffer = new byte[block];
                for (long i = offset - (offset % block); i < offset + length; i += block)
                {
                    if (i == offset - (offset % block)) // First block
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
        /// <remarks>
        /// Based on Official MSDN Code
        /// https://docs.microsoft.com/en-us/dotnet/standard/io/how-to-copy-directories
        /// </remarks>
        /// <param name="srcDir"></param>
        /// <param name="destDir"></param>
        /// <param name="copySubDirs"></param>
        /// <param name="overwrite"></param>
        /// <param name="wildcard">Wildcard only for first-sublevel directories</param>
        public static void DirectoryCopy(string srcDir, string destDir, bool copySubDirs, bool overwrite, string fileWildcard = null)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dirInfo = new DirectoryInfo(srcDir);

            if (!dirInfo.Exists)
                throw new DirectoryNotFoundException($"Source directory does not exist or cannot be found: {srcDir}");

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
                    DirectoryCopy(subDir.FullName, tempPath, copySubDirs, overwrite, fileWildcard);
                }
            }
        }

        public static string[] GetDirectoriesEx(string dirPath, string searchPattern, SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            if (dirPath == null) throw new ArgumentNullException("dirPath");

            DirectoryInfo dirInfo = new DirectoryInfo(dirPath);
            if (!dirInfo.Exists)
                throw new DirectoryNotFoundException($"Source directory does not exist or could not be found: {dirPath}");

            List<string> foundDirs = new List<string>();
            return InternalGetDirectoriesEx(dirInfo, searchPattern, searchOption, foundDirs).ToArray();
        }

        private static List<string> InternalGetDirectoriesEx(DirectoryInfo dirInfo, string searchPattern, SearchOption searchOption, List<string> foundDirs)
        {
            if (dirInfo == null) throw new ArgumentNullException("dirInfo");
            if (searchPattern == null) throw new ArgumentNullException("searchPattern");

            DirectoryInfo[] dirs;
            try
            {
                dirs = dirInfo.GetDirectories();
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
            if (dirPath == null) throw new ArgumentNullException("dirPath");

            DirectoryInfo dirInfo = new DirectoryInfo(dirPath);
            if (!dirInfo.Exists)
                throw new DirectoryNotFoundException($"Source directory does not exist or could not be found: {dirPath}");

            List<string> foundFiles = new List<string>();
            return InternalGetFilesEx(dirInfo, searchPattern, searchOption, foundFiles).ToArray();
        }

        private static List<string> InternalGetFilesEx(DirectoryInfo dirInfo, string searchPattern, SearchOption searchOption, List<string> foundFiles)
        {
            if (dirInfo == null) throw new ArgumentNullException("dirInfo");
            if (searchPattern == null) throw new ArgumentNullException("searchPattern");

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

        public static void DirectoryDeleteEx(string path)
        {
            DirectoryInfo root;
            Stack<DirectoryInfo> fols;
            DirectoryInfo fol;
            fols = new Stack<DirectoryInfo>();
            root = new DirectoryInfo(path);
            fols.Push(root);
            while (fols.Count > 0)
            {
                fol = fols.Pop();
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

        private const int MAX_LONG_PATH = 32767;
        private static readonly string LONG_PATH_PREFIX = @"\\?\";

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

            if (isLongPathDisabled == false)
            {
                if (longPath.StartsWith(LONG_PATH_PREFIX, StringComparison.Ordinal) == false)
                    longPath = LONG_PATH_PREFIX + longPath;
            }

            StringBuilder shortPath = new StringBuilder(MAX_LONG_PATH);
            GetShortPathName(longPath, shortPath, MAX_LONG_PATH);

            string str = shortPath.ToString();
            if (isLongPathDisabled == false)
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

    #region HashHelper
    public enum HashType { None, MD5, SHA1, SHA256, SHA384, SHA512 };

    public static class HashHelper
    {
        public const int MD5Len = 128 / 8;
        public const int SHA1Len = 160 / 8;
        public const int SHA256Len = 256 / 8;
        public const int SHA384Len = 384 / 8;
        public const int SHA512Len = 512 / 8;

        public static byte[] CalcHash(HashType type, byte[] data)
        {
            return InternalCalcHash(type, data);
        }

        public static byte[] CalcHash(HashType type, string hex)
        {
            if (!NumberHelper.ParseHexStringToBytes(hex, out byte[] data))
                throw new InvalidOperationException("Failed to parse string into hexadecimal bytes");
            return InternalCalcHash(type, data);
        }

        public static byte[] CalcHash(HashType type, Stream stream)
        {
            return InternalCalcHash(type, stream);
        }

        public static string CalcHashString(HashType type, byte[] data)
        {
            byte[] h = InternalCalcHash(type, data);
            StringBuilder builder = new StringBuilder();
            foreach (byte b in h)
                builder.Append(b.ToString("x2"));
            return builder.ToString();
        }

        public static string CalcHashString(HashType type, string hex)
        {
            if (!NumberHelper.ParseHexStringToBytes(hex, out byte[] data))
                throw new InvalidOperationException("Failed to parse string into hexadecimal bytes");
            byte[] h = InternalCalcHash(type, data);
            StringBuilder builder = new StringBuilder();
            foreach (byte b in h)
                builder.Append(b.ToString("x2"));
            return builder.ToString();
        }

        public static string CalcHashString(HashType type, Stream stream)
        {
            byte[] h = InternalCalcHash(type, stream);
            StringBuilder builder = new StringBuilder();
            foreach (byte b in h)
                builder.Append(b.ToString("x2"));
            return builder.ToString();
        }

        private static byte[] InternalCalcHash(HashType type, byte[] data)
        {
            HashAlgorithm hash;
            switch (type)
            {
                case HashType.MD5:
                    hash = MD5.Create();
                    break;
                case HashType.SHA1:
                    hash = SHA1.Create();
                    break;
                case HashType.SHA256:
                    hash = SHA256.Create();
                    break;
                case HashType.SHA384:
                    hash = SHA384.Create();
                    break;
                case HashType.SHA512:
                    hash = SHA512.Create();
                    break;
                default:
                    throw new InvalidOperationException("Invalid Hash Type");
            }
            return hash.ComputeHash(data);
        }

        private static byte[] InternalCalcHash(HashType type, Stream stream)
        {
            HashAlgorithm hash;
            switch (type)
            {
                case HashType.MD5:
                    hash = MD5.Create();
                    break;
                case HashType.SHA1:
                    hash = SHA1.Create();
                    break;
                case HashType.SHA256:
                    hash = SHA256.Create();
                    break;
                case HashType.SHA384:
                    hash = SHA384.Create();
                    break;
                case HashType.SHA512:
                    hash = SHA512.Create();
                    break;
                default:
                    throw new InvalidOperationException("Invalid Hash Type");
            }
            return hash.ComputeHash(stream);
        }

        public static HashType DetectHashType(byte[] data)
        {
            return InternalDetectHashType(data.Length);
        }

        public static HashType DetectHashType(string hex)
        {
            if (StringHelper.IsHex(hex))
                return HashType.None;
            if (!NumberHelper.ParseHexStringToBytes(hex, out byte[] hashByte))
                return HashType.None;

            return InternalDetectHashType(hashByte.Length);
        }

        private static HashType InternalDetectHashType(int length)
        {
            HashType hashType = HashType.None;

            switch (length)
            {
                case HashHelper.MD5Len * 2:
                    hashType = HashType.MD5;
                    break;
                case HashHelper.SHA1Len * 2:
                    hashType = HashType.SHA1;
                    break;
                case HashHelper.SHA256Len * 2:
                    hashType = HashType.SHA256;
                    break;
                case HashHelper.SHA384Len * 2:
                    hashType = HashType.SHA384;
                    break;
                case HashHelper.SHA512Len * 2:
                    hashType = HashType.SHA512;
                    break;
                default:
                    throw new Exception($"Cannot recognize valid hash string");
            }

            return hashType;
        }
    }
    #endregion

    #region StringHelper
    public static class StringHelper
    {
        /// <summary>
        /// Remove last newline in the string, removes whitespaces also.
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string RemoveLastNewLine(string str)
        {
            return str.Trim().TrimEnd(Environment.NewLine.ToCharArray()).Trim();
        }

        /// <summary>
        /// Check if string is hex or not
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static bool IsHex(string str)
        {
            str = str.Trim();
            if (str.Length % 2 == 1)
                return false;

            if (Regex.IsMatch(str, @"^[A-Fa-f0-9]+$", RegexOptions.Compiled))
                return true;
            else
                return false;
        }

        /// <summary>
        /// Count occurrences of strings.
        /// http://www.dotnetperls.com/string-occurrence
        /// </summary>
        public static int CountOccurrences(string text, string pattern)
        {
            // Loop through all instances of the string 'text'.
            int count = 0;
            int i = 0;
            while ((i = text.IndexOf(pattern, i, StringComparison.Ordinal)) != -1)
            {
                i += pattern.Length;
                count++;
            }
            return count;
        }

        public static string ReplaceEx(string str, string oldValue, string newValue, StringComparison comp)
        {
            if (oldValue.Equals(string.Empty, comp))
                return str;

            if (str.IndexOf(oldValue, comp) != -1)
            {
                int idx = 0;
                StringBuilder b = new StringBuilder();
                while (idx < str.Length)
                {
                    int vIdx = str.IndexOf(oldValue, idx, comp);

                    if (vIdx == -1)
                    {
                        b.Append(str.Substring(idx));
                        break;
                    }
                    else
                    {
                        b.Append(str.Substring(idx, vIdx - idx));
                        b.Append(newValue);
                        idx = vIdx += oldValue.Length;
                    }
                }
                return b.ToString();
            }
            else
            {
                return str;
            }
        }
    }
    #endregion

    #region NumberHelper
    public static class NumberHelper
    {
        public enum ParseStringToNumberType
        {
            String, Integer, Decimal
        }

        [Flags]
        public enum CompareStringNumberResult
        {
            None = 0,
            Equal = 1,
            NotEqual = 2,
            Smaller = 4,
            Bigger = 8,
        }

        public enum StringNumberType
        {
            PositiveInteger, NegativeInteger, HexInteger, Decimal, NotNumber
        }

        public static StringNumberType IsStringHexInteger(string str)
        {
            int pCnt = StringHelper.CountOccurrences(str, ".");
            if (1 < pCnt)
                return StringNumberType.NotNumber;

            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            { // 0x
                if (pCnt == 1)
                    return StringNumberType.NotNumber;
                else
                    return StringNumberType.HexInteger;
            }
            else
            {
                if (pCnt == 1)
                {
                    return StringNumberType.Decimal;
                }
                else
                {
                    if (str.StartsWith("-", StringComparison.Ordinal))
                        return StringNumberType.NegativeInteger;
                    else
                        return StringNumberType.PositiveInteger;
                }
            }
        }

        /// <summary>
        /// integer parser, supports base 10 and 16 at same time
        /// </summary>
        /// <returns>Return false if failed</returns>
        public static bool ParseInt8(string str, out sbyte value)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                value = 0;
                return false;
            }

            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return sbyte.TryParse(str.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
            else
                return sbyte.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// integer parser, supports base 10 and 16 at same time
        /// </summary>
        /// <returns></returns>
        public static bool ParseUInt8(string str, out byte value)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                value = 0;
                return false;
            }

            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return byte.TryParse(str.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
            else
                return byte.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// integer parser, supports base 10 and 16 at same time
        /// </summary>
        /// <returns>Return false if failed</returns>
        public static bool ParseInt16(string str, out Int16 value)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                value = 0;
                return false;
            }

            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return Int16.TryParse(str.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
            else
                return Int16.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// integer parser, supports base 10 and 16 at same time
        /// </summary>
        /// <returns></returns>
        public static bool ParseUInt16(string str, out UInt16 value)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                value = 0;
                return false;
            }

            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return UInt16.TryParse(str.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
            else
                return UInt16.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// integer parser, supports base 10 and 16 at same time
        /// </summary>
        /// <returns>Return false if failed</returns>
        public static bool ParseInt32(string str, out Int32 value)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                value = 0;
                return false;
            }

            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return Int32.TryParse(str.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
            else
                return Int32.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// integer parser, supports base 10 and 16 at same time
        /// </summary>
        /// <returns></returns>
        public static bool ParseUInt32(string str, out UInt32 value)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                value = 0;
                return false;
            }

            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return UInt32.TryParse(str.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
            else
                return UInt32.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// integer parser, supports base 10 and 16 at same time
        /// </summary>
        /// <returns>Return false if failed</returns>
        public static bool ParseInt64(string str, out Int64 value)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                value = 0;
                return false;
            }

            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return Int64.TryParse(str.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
            else
                return Int64.TryParse(str, NumberStyles.Integer | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// integer parser, supports base 10 and 16 at same time
        /// </summary>
        /// <returns></returns>
        public static bool ParseUInt64(string str, out UInt64 value)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                value = 0;
                return false;
            }

            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return UInt64.TryParse(str.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
            else
                return UInt64.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// decimal parser, supports base 10 and 16 at same time
        /// </summary>
        /// <returns></returns>
        public static bool ParseDouble(string str, out double value)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                value = 0;
                return false;
            }

            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                bool result = ulong.TryParse(str.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong intValue);
                value = (double)intValue;
                return result;
            }
            else
            {
                return double.TryParse(str, NumberStyles.AllowDecimalPoint | NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
            }
        }

        /// <summary>
        /// decimal parser, supports base 10 and 16 at same time
        /// </summary>
        /// <returns></returns>
        public static bool ParseDecimal(string str, out decimal value)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                value = 0;
                return false;
            }

            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                bool result = ulong.TryParse(str.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong intValue);
                value = (decimal)intValue;
                return result;
            }  
            else
            {
                return decimal.TryParse(str, NumberStyles.AllowDecimalPoint | NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
            }
        }

        /// <summary>
        /// Parse string to int or decimal
        /// </summary>
        /// <param name="str"></param>
        /// <param name="integer"></param>
        /// <param name="real"></param>
        /// <returns>Return true if string is number</returns>
        public static ParseStringToNumberType ParseStringToNumber(string str, out long integer, out decimal real)
        {
            integer = 0;
            real = 0;

            if (string.IsNullOrEmpty(str))
                return ParseStringToNumberType.String;

            // base 10 integer - Z
            if (Regex.IsMatch(str, @"^[0-9]+$", RegexOptions.Compiled))
            {
                if (long.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out integer))
                    return ParseStringToNumberType.Integer;
                else
                    return ParseStringToNumberType.String;
            }
            // base 16 integer - Z
            if (Regex.IsMatch(str, @"^0x[0-9a-zA-Z]+$", RegexOptions.Compiled))
            {
                if (long.TryParse(str.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out integer))
                    return ParseStringToNumberType.Integer;
                else
                    return ParseStringToNumberType.String;
            }
            // real number - R
            else if (Regex.IsMatch(str, @"^([0-9]+)\.([0-9]+)$", RegexOptions.Compiled))
            {
                if (decimal.TryParse(str, NumberStyles.AllowDecimalPoint | NumberStyles.Integer, CultureInfo.InvariantCulture, out real))
                    return ParseStringToNumberType.Decimal;
                else
                    return ParseStringToNumberType.String;
            }
            else
            { // Just String
                return ParseStringToNumberType.String;
            }
        }

        /// <summary>
        /// Compare string, which would be number
        /// </summary>
        /// <param name="str1"></param>
        /// <param name="str2"></param>
        public static CompareStringNumberResult CompareStringNumber(string str1, string str2, bool ignoreCase = true)
        {
            // Try version number compare
            VersionEx v1 = VersionEx.Parse(str1);
            VersionEx v2 = VersionEx.Parse(str2);
            if (v1 != null && v2 != null)
            {
                int comp = v1.CompareTo(v2);
                if (comp < 0)
                    return CompareStringNumberResult.Smaller;
                else if (comp == 0)
                    return CompareStringNumberResult.Equal;
                else
                    return CompareStringNumberResult.Bigger;
            }

            // Do simple number or string compare
            ParseStringToNumberType type1 = ParseStringToNumber(str1, out long z1, out decimal r1);
            ParseStringToNumberType type2 = ParseStringToNumber(str2, out long z2, out decimal r2);

            if (type1 == ParseStringToNumberType.Integer && type2 == ParseStringToNumberType.Integer)
            { // Args are both int
                long comp = z1 - z2;
                if (comp < 0)
                    return CompareStringNumberResult.Smaller;
                else if (comp == 0)
                    return CompareStringNumberResult.Equal;
                else
                    return CompareStringNumberResult.Bigger;
            }
            else if (type1 == ParseStringToNumberType.Integer && type2 == ParseStringToNumberType.Decimal ||
                type1 == ParseStringToNumberType.Decimal && type2 == ParseStringToNumberType.Integer ||
                type1 == ParseStringToNumberType.Decimal && type2 == ParseStringToNumberType.Decimal)
            { // One arg is decimal
                decimal comp = z1 - r2;
                if (comp < 0)
                    return CompareStringNumberResult.Smaller;
                else if (comp == 0)
                    return CompareStringNumberResult.Equal;
                else
                    return CompareStringNumberResult.Bigger;
            }
            else // if (type1 == ParseStringToNumberType.String || type2 == ParseStringToNumberType.String)
            { // One of arg is string, so just compare
                StringComparison compOpt = StringComparison.Ordinal;
                if (ignoreCase)
                    compOpt = StringComparison.OrdinalIgnoreCase;

                if (str1.Equals(str2, compOpt))
                    return CompareStringNumberResult.Equal;
                else
                    return CompareStringNumberResult.NotEqual;
            }
        }

        /// <summary>
        /// Extended VersionEx to support single integer
        /// Ex) 5 vs 5.1.2600.1234
        /// </summary>
        public class VersionEx
        {
            public int Major { get; private set; }
            public int Minor { get; private set; }
            public int Build { get; private set; }
            public int Revision { get; private set; }

            public VersionEx(int major, int minor, int build, int revision)
            {
                if (major < 0) throw new ArgumentOutOfRangeException("major");
                if (minor < 0) throw new ArgumentOutOfRangeException("minor");
                if (build < -1) throw new ArgumentOutOfRangeException("build");
                if (revision < -1) throw new ArgumentOutOfRangeException("revision");

                Major = major;
                Minor = minor;
                Build = build;
                Revision = revision;
            }

            public static VersionEx Parse(string str)
            {
                int[] arr = new int[4] { 0, 0, -1, -1 };

                string[] parts = str.Split('.');
                if (parts.Length < 1 && 4 < parts.Length)
                    return null;

                for (int i = 0; i < parts.Length; i++)
                {
                    if (!int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out arr[i]))
                        return null;
                }

                try { return new VersionEx(arr[0], arr[1], arr[2], arr[3]); }
                catch { return null; }
            }

            public int CompareTo(VersionEx value)
            {
                if (value == null) throw new ArgumentNullException("value");

                if (Major != value.Major)
                {
                    if (Major > value.Major)
                        return 1;
                    else
                        return -1;
                }

                if (Minor != value.Minor)
                {
                    if (Minor > value.Minor)
                        return 1;
                    else
                        return -1;
                }

                if (Build != value.Build)
                {
                    if (Build > value.Build)
                        return 1;
                    else
                        return -1;
                }

                if (Revision != value.Revision)
                {
                    if (Revision > value.Revision)
                        return 1;
                    else
                        return -1;
                }

                return 0;
            }
        }

        /// <summary>
        /// Parse hex string into byte array. Hex string must be in form of A0B1C2. Return true if success.
        /// </summary>
        /// <param name="hex"></param>
        /// <param name="array"></param>
        /// <returns>Return true if success.</returns>
        public static bool ParseHexStringToBytes(string hex, out byte[] array)
        {
            // Encoding.UTF8.GetBytes

            if (hex.Length % 2 == 1) // hex's length must be even number
            {
                array = new byte[0];
                return false;
            }

            array = new byte[hex.Length / 2];
            for (int i = 0; i < hex.Length; i += 2)
                array[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return true;
        }

        const long PB = 1024L * 1024L * 1024L * 1024L * 1024L;
        const long TB = 1024L * 1024L * 1024L * 1024L;
        const long GB = 1024L * 1024L * 1024L;
        const long MB = 1024L * 1024L;
        const long KB = 1024L;

        public static string ByteSizeToHumanReadableString(long byteSize, int decPoint = 3)
        {
            if (decPoint < 0) throw new ArgumentOutOfRangeException("decPoint");

            string formatString = "0";
            if (0 < decPoint)
            { // formatString = "0.###"
                StringBuilder b = new StringBuilder(decPoint + 1);
                b.Append("0.");
                for (int i = 0; i < decPoint; i++)
                    b.Append("#");
                formatString = b.ToString();
            }
            
            string str;
            if (PB <= byteSize)
                str = $"{((decimal)byteSize / PB).ToString(formatString)}PB";
            else if (TB <= byteSize)
                str = $"{((decimal)byteSize / TB).ToString(formatString)}TB";
            else if (GB <= byteSize)
                str = $"{((decimal)byteSize / GB).ToString(formatString)}GB";
            else if (MB <= byteSize)
                str = $"{((decimal)byteSize / MB).ToString(formatString)}MB";
            else
                str = $"{((decimal)byteSize / KB).ToString(formatString)}KB";

            return str;
        }

        public static decimal HumanReadableStringToByteSize(string str)
        {
            long multifier = 1;
            int subStrEndIdx = 0;

            if (str.EndsWith("PB", StringComparison.OrdinalIgnoreCase))
            {
                multifier = PB;
                subStrEndIdx = 2;
            }
            else if (str.EndsWith("TB", StringComparison.OrdinalIgnoreCase))
            {
                multifier = TB;
                subStrEndIdx = 2;
            }
            else if (str.EndsWith("GB", StringComparison.OrdinalIgnoreCase))
            {
                multifier = GB;
                subStrEndIdx = 2;
            }
            else if (str.EndsWith("MB", StringComparison.OrdinalIgnoreCase))
            {
                multifier = MB;
                subStrEndIdx = 2;
            }
            else if (str.EndsWith("KB", StringComparison.OrdinalIgnoreCase))
            {
                multifier = KB;
                subStrEndIdx = 2;
            }

            if (str.EndsWith("P", StringComparison.OrdinalIgnoreCase))
            {
                multifier = PB;
                subStrEndIdx = 1;
            }
            else if (str.EndsWith("T", StringComparison.OrdinalIgnoreCase))
            {
                multifier = TB;
                subStrEndIdx = 1;
            }
            else if (str.EndsWith("G", StringComparison.OrdinalIgnoreCase))
            {
                multifier = GB;
                subStrEndIdx = 1;
            }
            else if (str.EndsWith("M", StringComparison.OrdinalIgnoreCase))
            {
                multifier = MB;
                subStrEndIdx = 1;
            }
            else if (str.EndsWith("K", StringComparison.OrdinalIgnoreCase))
            {
                multifier = KB;
                subStrEndIdx = 1;
            }

            str = str.Substring(0, str.Length - subStrEndIdx);
            return decimal.Parse(str, NumberStyles.Float, CultureInfo.InvariantCulture) * multifier;
        }

        public static decimal DecimalPower(decimal val, uint pow)
        {
            decimal ret = 1;
            for (uint i = 0; i < pow; i++)
                ret *= val;  
            return ret;
        }
    }
    #endregion

    #region RegistryHelper
    public static class RegistryHelper
    {
        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentProcess();
        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool OpenProcessToken(IntPtr ProcessHandle, UInt32 DesiredAccess, out IntPtr TokenHandle);
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, out LUID lpLuid);
        [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
        static extern bool AdjustTokenPrivileges(IntPtr htok, bool disableAllPrivileges, ref TOKEN_PRIVILEGES newState, UInt32 len, IntPtr prev, IntPtr relen);
        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern Int32 RegLoadKey(SafeRegistryHandle hKey, string lpSubKey, string lpFile);
        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern Int32 RegUnLoadKey(SafeRegistryHandle hKey, string lpSubKey);
        [DllImport("kernel32.dll", SetLastError = true)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        [SuppressUnmanagedCodeSecurity]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseHandle(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct LUID_AND_ATTRIBUTES
        {
            public LUID pLuid;
            public UInt32 Attributes;
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct TOKEN_PRIVILEGES
        {
            public int Count;
            public LUID Luid;
            public UInt32 Attr;
        }

        private const Int32 ANYSIZE_ARRAY = 1;
        private const UInt32 SE_PRIVILEGE_ENABLED = 0x00000002;
        private const UInt32 TOKEN_ADJUST_PRIVILEGES = 0x0020;
        private const UInt32 TOKEN_QUERY = 0x0008;

        /*
        public const UInt32 HKCR = 0x80000000; // HKEY_CLASSES_ROOT
        public const UInt32 HKCU = 0x80000001; // HKEY_CURRENT_USER
        public const UInt32 HKLM = 0x80000002; // HKEY_LOCAL_MACHINE
        public const UInt32 HKU = 0x80000003; // HKEY_USERS
        public const UInt32 HKPD = 0x80000004; // HKEY_PERFORMANCE_DATA
        public const UInt32 HKCC = 0x80000005; // HKEY_CURRENT_CONFIG
        */

        public static void GetAdminPrivileges()
        {
            TOKEN_PRIVILEGES pRestoreToken = new TOKEN_PRIVILEGES();
            TOKEN_PRIVILEGES pBackupToken = new TOKEN_PRIVILEGES();

            if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out IntPtr hToken))
                throw new BetterWin32Errors.Win32Exception("OpenProcessToken failed");

            if (!LookupPrivilegeValue(null, "SeRestorePrivilege", out LUID restoreLUID))
                throw new BetterWin32Errors.Win32Exception("LookupPrivilegeValue failed");

            if (!LookupPrivilegeValue(null, "SeBackupPrivilege", out LUID backupLUID))
                throw new BetterWin32Errors.Win32Exception("LookupPrivilegeValue failed");

            pRestoreToken.Count = 1;
            pRestoreToken.Luid = restoreLUID;
            pRestoreToken.Attr = SE_PRIVILEGE_ENABLED;

            pBackupToken.Count = 1;
            pBackupToken.Luid = backupLUID;
            pBackupToken.Attr = SE_PRIVILEGE_ENABLED;

            if (!AdjustTokenPrivileges(hToken, false, ref pRestoreToken, 0, IntPtr.Zero, IntPtr.Zero))
            {
                Win32Error error = BetterWin32Errors.Win32Exception.GetLastWin32Error();
                if (error == Win32Error.ERROR_NOT_ALL_ASSIGNED)
                    throw new BetterWin32Errors.Win32Exception("AdjustTokenPrivileges failed, try running this program with Administrator privilege.");
                else
                    throw new BetterWin32Errors.Win32Exception("AdjustTokenPrivileges failed");
            }

            if (!AdjustTokenPrivileges(hToken, false, ref pBackupToken, 0, IntPtr.Zero, IntPtr.Zero))
            {
                Win32Error error = BetterWin32Errors.Win32Exception.GetLastWin32Error();
                if (error == Win32Error.ERROR_NOT_ALL_ASSIGNED)
                    throw new BetterWin32Errors.Win32Exception("AdjustTokenPrivileges failed, try running this program with Administrator privilege.");
                else
                    throw new BetterWin32Errors.Win32Exception("AdjustTokenPrivileges failed");
            }
            CloseHandle(hToken);
        }

        public static RegistryKey ParseStringToRegKey(string rootKey)
        {
            RegistryKey regRoot;
            if (rootKey.Equals("HKCR", StringComparison.OrdinalIgnoreCase) ||
                rootKey.Equals("HKEY_CLASSES_ROOT", StringComparison.OrdinalIgnoreCase))
                regRoot = Registry.ClassesRoot; // HKEY_CLASSES_ROOT
            else if (rootKey.Equals("HKCU", StringComparison.OrdinalIgnoreCase) ||
                rootKey.Equals("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase))
                regRoot = Registry.CurrentUser; // HKEY_CURRENT_USER
            else if (rootKey.Equals("HKLM", StringComparison.OrdinalIgnoreCase) ||
                rootKey.Equals("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase))
                regRoot = Registry.LocalMachine; // HKEY_LOCAL_MACHINE
            else if (rootKey.Equals("HKU", StringComparison.OrdinalIgnoreCase) ||
                rootKey.Equals("HKEY_USERS", StringComparison.OrdinalIgnoreCase))
                regRoot = Registry.Users; // HKEY_USERS
            else if (rootKey.Equals("HKCC", StringComparison.OrdinalIgnoreCase) ||
                rootKey.Equals("HKEY_CURRENT_CONFIG", StringComparison.OrdinalIgnoreCase))
                regRoot = Registry.CurrentConfig; // HKEY_CURRENT_CONFIG
            else
                regRoot = null;
            return regRoot;
        }

        public static string RegKeyToString(RegistryKey regKey)
        {
            string rootKey;
            if (regKey == Registry.ClassesRoot)
                rootKey = "HKCR";
            else if (regKey == Registry.CurrentUser)
                rootKey = "HKCU";
            else if (regKey == Registry.LocalMachine)
                rootKey = "HKLM";
            else if (regKey == Registry.Users)
                rootKey = "HKU";
            else if (regKey == Registry.CurrentConfig)
                rootKey = "HKCC";
            else
                rootKey = null;
            return rootKey;
        }

        public static string RegKeyToFullString(RegistryKey regKey)
        {
            string rootKey;
            if (regKey == Registry.ClassesRoot)
                rootKey = "HKEY_CLASSES_ROOT";
            else if (regKey == Registry.CurrentUser)
                rootKey = "HKEY_CURRENT_USER";
            else if (regKey == Registry.LocalMachine)
                rootKey = "HKEY_LOCAL_MACHINE";
            else if (regKey == Registry.Users)
                rootKey = "HKEY_USERS";
            else if (regKey == Registry.CurrentConfig)
                rootKey = "HKEY_CURRENT_CONFIG";
            else
                rootKey = null;
            return rootKey;
        }

        public static SafeRegistryHandle ParseStringToHandle(string rootKey)
        {
            SafeRegistryHandle hKey;
            if (rootKey.Equals("HKCR", StringComparison.OrdinalIgnoreCase) ||
                rootKey.Equals("HKEY_CLASSES_ROOT", StringComparison.OrdinalIgnoreCase))
                hKey = Registry.ClassesRoot.Handle; // HKEY_CLASSES_ROOT
            else if (rootKey.Equals("HKCU", StringComparison.OrdinalIgnoreCase) ||
                rootKey.Equals("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase))
                hKey = Registry.CurrentUser.Handle; // HKEY_CURRENT_USER
            else if (rootKey.Equals("HKLM", StringComparison.OrdinalIgnoreCase) ||
                rootKey.Equals("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase))
                hKey = Registry.LocalMachine.Handle; // HKEY_LOCAL_MACHINE
            else if (rootKey.Equals("HKU", StringComparison.OrdinalIgnoreCase) ||
                rootKey.Equals("HKEY_USERS", StringComparison.OrdinalIgnoreCase))
                hKey = Registry.Users.Handle; // HKEY_USERS
            else if (rootKey.Equals("HKCC", StringComparison.OrdinalIgnoreCase) ||
                rootKey.Equals("HKEY_CURRENT_CONFIG", StringComparison.OrdinalIgnoreCase))
                hKey = Registry.CurrentConfig.Handle; // HKEY_CURRENT_CONFIG
            else
                hKey = new SafeRegistryHandle(IntPtr.Zero, true);
            return hKey;
        }


    }
    #endregion

    #region ArchiveHelper
    public static class ArchiveHelper
    {
        public static readonly string SevenZipDllPath;

        static ArchiveHelper()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string arch;
            if (IntPtr.Size == 8)
                arch = "x64";
            else
                arch = "x86";

            SevenZipDllPath = Path.Combine(baseDir, arch, "7z.dll");
        }

        /// <summary>
        /// Expand cab file using P/invoked FDICreate, FDICopy, FDIDestroy
        /// </summary>
        /// TODO: Use 
        /// </remarks>
        /// <param name="srcPath"></param>
        /// <param name="destPath"></param>
        /// <returns>Return true if success</returns>
        public static bool ExtractCab(string srcCabFile, string destDir)
        {
            using (FileStream fs = new FileStream(srcCabFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (CabExtract cab = new CabExtract(fs))
            {
                return cab.ExtractAll(destDir, out List<string> nop);
            }           
        }

        /// <summary>
        /// Expand cab file using P/invoked FDICreate, FDICopy, FDIDestroy
        /// </summary>
        /// TODO: Use 
        /// </remarks>
        /// <param name="srcPath"></param>
        /// <param name="destPath"></param>
        /// <returns>Return true if success</returns>
        public static bool ExtractCab(string srcCabFile, string destDir, out List<string> extractedList)
        {
            using (FileStream fs = new FileStream(srcCabFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (CabExtract cab = new CabExtract(fs))
            {
                return cab.ExtractAll(destDir, out extractedList);
            }
        }

        /// <summary>
        /// Expand cab file using P/invoked FDICreate, FDICopy, FDIDestroy
        /// </summary>
        /// TODO: Use 
        /// </remarks>
        /// <param name="srcPath"></param>
        /// <param name="destPath"></param>
        /// <returns>Return true if success</returns>
        public static bool ExtractCab(string srcCabFile, string destDir, string target)
        {
            using (FileStream fs = new FileStream(srcCabFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (CabExtract cab = new CabExtract(fs))
            {
                return cab.ExtractSingleFile(target, destDir);
            }
        }

        public enum CompressLevel
        {
            Store = 0,
            Fastest = 1,
            Normal = 6,
            Best = 9,
        }

        /*
        // Do not use System.IO.Compression, it causes lots of error when .Net Standard 2.0 is referenced!
        public static bool CompressNativeZip(string srcPath, string destArchive, ArchiveHelper.CompressLevel helperLevel, Encoding encoding)
        {
            CompressionLevel level;
            switch (helperLevel)
            {
                case ArchiveHelper.CompressLevel.Store:
                    level = CompressionLevel.NoCompression;
                    break;
                case ArchiveHelper.CompressLevel.Fastest:
                    level = CompressionLevel.Fastest;
                    break;
                case ArchiveHelper.CompressLevel.Normal:
                    level = CompressionLevel.Optimal;
                    break;
                case ArchiveHelper.CompressLevel.Best:
                    level = CompressionLevel.Optimal;
                    break;
                default:
                    throw new ArgumentException($"Invalid ArchiveHelper.CompressLevel [{helperLevel}]");
            }

            if (File.Exists(destArchive))
                File.Delete(destArchive);

            if (File.Exists(srcPath))
            {
                using (FileStream fs = new FileStream(destArchive, FileMode.Create))
                using (System.IO.Compression.ZipArchive arch = new System.IO.Compression.ZipArchive(fs, ZipArchiveMode.Create))
                {
                    arch.CreateEntryFromFile(srcPath, Path.GetFileName(srcPath));
                }
            }
            else if (Directory.Exists(srcPath))
            {
                ZipFile.CreateFromDirectory(srcPath, destArchive, level, false, encoding);
            }
            else
            {
                throw new ArgumentException($"Path [{helperLevel}] does not exist");
            }

            if (File.Exists(destArchive))
                return true;
            else
                return false;
        }
        */

        public static bool CompressManagedZip(string srcPath, string destArchive, ArchiveHelper.CompressLevel helperLevel, Encoding encoding)
        {
            SharpCompress.Compressors.Deflate.CompressionLevel compLevel;
            switch (helperLevel)
            {
                case ArchiveHelper.CompressLevel.Store:
                    compLevel = SharpCompress.Compressors.Deflate.CompressionLevel.None;
                    break;
                case ArchiveHelper.CompressLevel.Fastest:
                    compLevel = SharpCompress.Compressors.Deflate.CompressionLevel.BestSpeed;
                    break;
                case ArchiveHelper.CompressLevel.Normal:
                    compLevel = SharpCompress.Compressors.Deflate.CompressionLevel.Default;
                    break;
                case ArchiveHelper.CompressLevel.Best:
                    compLevel = SharpCompress.Compressors.Deflate.CompressionLevel.BestCompression;
                    break;
                default:
                    throw new ArgumentException($"Invalid ArchiveHelper.CompressLevel [{helperLevel}]");
            }

            ArchiveEncoding arcEnc = new ArchiveEncoding() { Default = encoding };
            ZipWriterOptions options = new ZipWriterOptions(CompressionType.Deflate)
            {
                LeaveStreamOpen = false,
                ArchiveEncoding = arcEnc,
                DeflateCompressionLevel = compLevel,
                UseZip64 = false,
            };

            if (File.Exists(destArchive))
                File.Delete(destArchive);

            using (FileStream stream = new FileStream(destArchive, FileMode.Create, FileAccess.Write))
            {
                using (ZipWriter writer = new ZipWriter(stream, options))
                {
                    if (Directory.Exists(srcPath))
                    {
                        writer.WriteAll(srcPath, "*", SearchOption.AllDirectories);
                    }
                    else
                    {
                        if (File.Exists(srcPath))
                            writer.Write(Path.GetFileName(srcPath), srcPath);
                        else
                            throw new ArgumentException($"[{srcPath}] does not exist");
                    }
                }

                stream.Close();
            }

            if (File.Exists(destArchive))
                return true;
            else
                return false;
        }

        public static void DecompressNative(string srcArchive, string destDir, bool overwrite)
        {
            using (ArchiveFile archiveFile = new ArchiveFile(srcArchive, SevenZipDllPath))
            {
                archiveFile.Extract(destDir, overwrite);
            }
        }

        public static void DecompressManaged(string srcArchive, string destDir, bool overwrite, Encoding encoding = null)
        {
            ExtractionOptions exOptions = new ExtractionOptions()
            {
                ExtractFullPath = true,
                Overwrite = overwrite,
            };

            ReaderOptions rOptions = new ReaderOptions() { LeaveStreamOpen = true, };
            if (encoding != null)
                rOptions.ArchiveEncoding = new ArchiveEncoding() { Default = encoding };

            using (Stream stream = new FileStream(srcArchive, FileMode.Open, FileAccess.Read))
            using (var reader = ReaderFactory.Open(stream, rOptions))
            {
                while (reader.MoveToNextEntry())
                {
                    if (!reader.Entry.IsDirectory)
                        reader.WriteEntryToDirectory(destDir, exOptions);
                }
            }
        }
    }
    #endregion

    #region ImageHelper
    public static class ImageHelper
    {
        public enum ImageType
        {
            Bmp, Jpg, Png, Gif, Ico, Svg
        }

        /// <summary>
        /// Return true if failed
        /// </summary>
        /// <param name="path"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool GetImageType(string path, out ImageType type)
        {
            type = ImageType.Bmp; // Dummy
            string logoType = Path.GetExtension(path);
            if (string.Equals(logoType, ".bmp", StringComparison.OrdinalIgnoreCase))
                type = ImageType.Bmp;
            else if (string.Equals(logoType, ".jpg", StringComparison.OrdinalIgnoreCase))
                type = ImageType.Jpg;
            else if (string.Equals(logoType, ".png", StringComparison.OrdinalIgnoreCase))
                type = ImageType.Png;
            else if (string.Equals(logoType, ".gif", StringComparison.OrdinalIgnoreCase))
                type = ImageType.Gif;
            else if (string.Equals(logoType, ".ico", StringComparison.OrdinalIgnoreCase))
                type = ImageType.Ico;
            else if (string.Equals(logoType, ".svg", StringComparison.OrdinalIgnoreCase))
                type = ImageType.Svg;
            else
                return true;
            return false;
        }

        public static BitmapImage ImageToBitmapImage(byte[] image)
        {
            BitmapImage bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = new MemoryStream(image);
            bitmap.EndInit();
            return bitmap;
        }

        public static BitmapImage ImageToBitmapImage(Stream stream)
        {
            BitmapImage bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            return bitmap;
        }

        public static ImageBrush ImageToImageBrush(Stream stream)
        {
            BitmapImage bitmap = ImageToBitmapImage(stream);
            ImageBrush brush = new ImageBrush
            {
                ImageSource = bitmap
            };
            return brush;
        }

        public static BitmapImage SvgToBitmapImage(Stream stream)
        {
            SvgDocument svgDoc = SvgDocument.Open<SvgDocument>(stream);
            return ImageHelper.ToBitmapImage(svgDoc.Draw());
        }

        public static void GetSvgSize(Stream stream, out double width, out double height)
        {
            SvgDocument svgDoc = SvgDocument.Open<SvgDocument>(stream);
            SizeF size = svgDoc.GetDimensions();
            width = size.Width;
            height = size.Height;
        }

        public static BitmapImage SvgToBitmapImage(Stream stream, out double width, out double height)
        {
            SvgDocument svgDoc = SvgDocument.Open<SvgDocument>(stream);
            SizeF size = svgDoc.GetDimensions();
            width = size.Width;
            height = size.Height;
            return ImageHelper.ToBitmapImage(svgDoc.Draw());
        }

        public static BitmapImage SvgToBitmapImage(Stream stream, double width, double height)
        {
            SvgDocument svgDoc = SvgDocument.Open<SvgDocument>(stream);
            return ImageHelper.ToBitmapImage(svgDoc.Draw((int)Math.Round(width), (int)Math.Round(height)));
        }

        public static BitmapImage SvgToBitmapImage(Stream stream, int width, int height)
        {
            SvgDocument svgDoc = SvgDocument.Open<SvgDocument>(stream);
            return ImageHelper.ToBitmapImage(svgDoc.Draw(width, height));
        }

        public static BitmapImage ToBitmapImage(Bitmap bitmap)
        {
            using (MemoryStream mem = new MemoryStream())
            {
                bitmap.Save(mem, ImageFormat.Png);
                mem.Position = 0;

                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = mem;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();

                return bitmapImage;
            }
        }

        public static ImageBrush SvgToImageBrush(Stream stream)
        {
            return ImageHelper.BitmapImageToImageBrush(ImageHelper.SvgToBitmapImage(stream));
        }
        public static ImageBrush SvgToImageBrush(Stream stream, double width, double height)
        {
            return ImageHelper.BitmapImageToImageBrush(ImageHelper.SvgToBitmapImage(stream, width, height));
        }

        public static ImageBrush SvgToImageBrush(Stream stream, int width, int height)
        {
            return ImageHelper.BitmapImageToImageBrush(ImageHelper.SvgToBitmapImage(stream, width, height));
        }

        public static ImageBrush BitmapImageToImageBrush(BitmapImage bitmap)
        {
            return new ImageBrush() { ImageSource = bitmap };
        }

        public static PackIconMaterial GetMaterialIcon(PackIconMaterialKind kind, double margin = 0)
        {
            PackIconMaterial icon = new PackIconMaterial()
            {
                Kind = kind,
                Width = Double.NaN,
                Height = Double.NaN,
                Margin = new Thickness(margin, margin, margin, margin),
            };
            return icon;
        }
    }
    #endregion

    #region FontHelper
    public static class FontHelper
    {
        // if we specify CharSet.Auto instead of CharSet.Ansi, then the string will be unreadable
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public class LOGFONT
        {
            public int lfHeight;
            public int lfWidth;
            public int lfEscapement;
            public int lfOrientation;
            public LogFontWeight lfWeight;
            [MarshalAs(UnmanagedType.U1)]
            public bool lfItalic;
            [MarshalAs(UnmanagedType.U1)]
            public bool lfUnderline;
            [MarshalAs(UnmanagedType.U1)]
            public bool lfStrikeOut;
            public LogFontCharSet lfCharSet;
            public LogFontPrecision lfOutPrecision;
            public LogFontClipPrecision lfClipPrecision;
            public LogFontQuality lfQuality;
            public LogFontPitchAndFamily lfPitchAndFamily;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string lfFaceName;
        }

        public enum LogFontWeight : int
        {
            FW_DONTCARE = 0,
            FW_THIN = 100,
            FW_EXTRALIGHT = 200,
            FW_LIGHT = 300,
            FW_REGULAR = 400,
            FW_MEDIUM = 500,
            FW_SEMIBOLD = 600,
            FW_BOLD = 700,
            FW_EXTRABOLD = 800,
            FW_HEAVY = 900,
        }

        public enum LogFontCharSet : byte
        {
            ANSI_CHARSET = 0,
            DEFAULT_CHARSET = 1,
            SYMBOL_CHARSET = 2,
            SHIFTJIS_CHARSET = 128,
            HANGEUL_CHARSET = 129,
            HANGUL_CHARSET = 129,
            GB2312_CHARSET = 134,
            CHINESEBIG5_CHARSET = 136,
            OEM_CHARSET = 255,
            JOHAB_CHARSET = 130,
            HEBREW_CHARSET = 177,
            ARABIC_CHARSET = 178,
            GREEK_CHARSET = 161,
            TURKISH_CHARSET = 162,
            VIETNAMESE_CHARSET = 163,
            THAI_CHARSET = 222,
            EASTEUROPE_CHARSET = 238,
            RUSSIAN_CHARSET = 204,
            MAC_CHARSET = 77,
            BALTIC_CHARSET = 186,
        }

        public enum LogFontPrecision : byte
        {
            OUT_DEFAULT_PRECIS = 0,
            OUT_STRING_PRECIS = 1,
            OUT_CHARACTER_PRECIS = 2,
            OUT_STROKE_PRECIS = 3,
            OUT_TT_PRECIS = 4,
            OUT_DEVICE_PRECIS = 5,
            OUT_RASTER_PRECIS = 6,
            OUT_TT_ONLY_PRECIS = 7,
            OUT_OUTLINE_PRECIS = 8,
            OUT_SCREEN_OUTLINE_PRECIS = 9,
            OUT_PS_ONLY_PRECIS = 10,
        }

        public enum LogFontClipPrecision : byte
        {
            CLIP_DEFAULT_PRECIS = 0,
            CLIP_CHARACTER_PRECIS = 1,
            CLIP_STROKE_PRECIS = 2,
            CLIP_MASK = 0xf,
            CLIP_LH_ANGLES = (1 << 4),
            CLIP_TT_ALWAYS = (2 << 4),
            CLIP_DFA_DISABLE = (4 << 4),
            CLIP_EMBEDDED = (8 << 4),
        }

        public enum LogFontQuality : byte
        {
            DEFAULT_QUALITY = 0,
            DRAFT_QUALITY = 1,
            PROOF_QUALITY = 2,
            NONANTIALIASED_QUALITY = 3,
            ANTIALIASED_QUALITY = 4,
            CLEARTYPE_QUALITY = 5,
            CLEARTYPE_NATURAL_QUALITY = 6,
        }

        [Flags]
        public enum LogFontPitchAndFamily : byte
        {
            DEFAULT_PITCH = 0,
            FIXED_PITCH = 1,
            VARIABLE_PITCH = 2,
            FF_DONTCARE = (0 << 4),
            FF_ROMAN = (1 << 4),
            FF_SWISS = (2 << 4),
            FF_MODERN = (3 << 4),
            FF_SCRIPT = (4 << 4),
            FF_DECORATIVE = (5 << 4),
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct CHOOSEFONT
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hDC;
            public IntPtr lpLogFont;
            public int iPointSize;
            public ChooseFontFlags Flags;
            public int rgbColors;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpTemplateName;
            public IntPtr hInstance;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpszStyle;
            public short nFontType;
            private short __MISSING_ALIGNMENT__;
            public int nSizeMin;
            public int nSizeMax;
        }

        [Flags]
        public enum ChooseFontFlags : int
        {
            CF_SCREENFONTS = 0x00000001,
            CF_PRINTERFONTS = 0x00000002,
            CF_BOTH = (CF_SCREENFONTS | CF_PRINTERFONTS),
            CF_SHOWHELP = 0x00000004,
            CF_ENABLEHOOK = 0x00000008,
            CF_ENABLETEMPLATE = 0x00000010,
            CF_ENABLETEMPLATEHANDLE = 0x00000020,
            CF_INITTOLOGFONTSTRUCT = 0x00000040,
            CF_USESTYLE = 0x00000080,
            CF_EFFECTS = 0x00000100,
            CF_APPLY = 0x00000200,
            CF_ANSIONLY = 0x00000400,
            CF_SCRIPTSONLY = CF_ANSIONLY,
            CF_NOVECTORFONTS = 0x00000800,
            CF_NOOEMFONTS = CF_NOVECTORFONTS,
            CF_NOSIMULATIONS = 0x00001000,
            CF_LIMITSIZE = 0x00002000,
            CF_FIXEDPITCHONLY = 0x00004000,
            CF_WYSIWYG = 0x00008000,
            CF_FORCEFONTEXIST = 0x00010000,
            CF_SCALABLEONLY = 0x00020000,
            CF_TTONLY = 0x00040000,
            CF_NOFACESEL = 0x00080000,
            CF_NOSTYLESEL = 0x00100000,
            CF_NOSIZESEL = 0x00200000,
            CF_SELECTSCRIPT = 0x00400000,
            CF_NOSCRIPTSEL = 0x00800000,
            CF_NOVERTFONTS = 0x01000000,
            CF_INACTIVEFONTS = 0x02000000
        }

        [DllImport("comdlg32.dll", CharSet = CharSet.Auto, EntryPoint = "ChooseFont", SetLastError = true)]
        public extern static bool ChooseFont([In, Out] ref CHOOSEFONT lpcf);

        public static LogFontWeight FontWeightConvert_WPFToLogFont(FontWeight weight)
        {
            if (weight == FontWeights.Thin)
                return LogFontWeight.FW_THIN;
            else if (weight == FontWeights.ExtraLight || weight == FontWeights.UltraLight)
                return LogFontWeight.FW_EXTRALIGHT;
            else if (weight == FontWeights.Light)
                return LogFontWeight.FW_LIGHT;
            else if (weight == FontWeights.Regular || weight == FontWeights.Normal)
                return LogFontWeight.FW_REGULAR;
            else if (weight == FontWeights.Medium)
                return LogFontWeight.FW_MEDIUM;
            else if (weight == FontWeights.SemiBold || weight == FontWeights.DemiBold)
                return LogFontWeight.FW_SEMIBOLD;
            else if (weight == FontWeights.Bold)
                return LogFontWeight.FW_BOLD;
            else if (weight == FontWeights.ExtraBold || weight == FontWeights.UltraBold)
                return LogFontWeight.FW_EXTRABOLD;
            else if (weight == FontWeights.Heavy || weight == FontWeights.Black)
                return LogFontWeight.FW_HEAVY;
            else
                return LogFontWeight.FW_REGULAR;
        }

        public static FontWeight FontWeightConvert_LogFontToWPF(LogFontWeight enumWeight)
        {
            switch (enumWeight)
            {
                case LogFontWeight.FW_THIN:
                    return FontWeights.Thin;
                case LogFontWeight.FW_EXTRALIGHT:
                    return FontWeights.ExtraLight;
                case LogFontWeight.FW_LIGHT:
                    return FontWeights.Light;
                case LogFontWeight.FW_REGULAR:
                    return FontWeights.Regular;
                case LogFontWeight.FW_MEDIUM:
                    return FontWeights.Medium;
                case LogFontWeight.FW_SEMIBOLD:
                    return FontWeights.SemiBold;
                case LogFontWeight.FW_BOLD:
                    return FontWeights.Bold;
                case LogFontWeight.FW_EXTRABOLD:
                    return FontWeights.ExtraBold;
                case LogFontWeight.FW_HEAVY:
                    return FontWeights.Heavy;
                default:
                    return FontWeights.Regular;
            }
        }

        public static FontWeight FontWeightConvert_StringToWPF(string str)
        {
            if (str.Equals("Thin", StringComparison.OrdinalIgnoreCase))
                return FontWeights.Thin;
            else if (str.Equals("ExtraLight", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("UltraLight", StringComparison.OrdinalIgnoreCase))
                return FontWeights.ExtraLight;
            else if (str.Equals("Light", StringComparison.OrdinalIgnoreCase))
                return FontWeights.Light;
            else if (str.Equals("Regular", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("Normal", StringComparison.OrdinalIgnoreCase))
                return FontWeights.Regular;
            else if (str.Equals("Medium", StringComparison.OrdinalIgnoreCase))
                return FontWeights.Medium;
            else if (str.Equals("SemiBold", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("DemiBold", StringComparison.OrdinalIgnoreCase))
                return FontWeights.SemiBold;
            else if (str.Equals("Bold", StringComparison.OrdinalIgnoreCase))
                return FontWeights.Bold;
            else if (str.Equals("ExtraBold", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("UltraBold", StringComparison.OrdinalIgnoreCase))
                return FontWeights.ExtraBold;
            else if (str.Equals("Heavy", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("Black", StringComparison.OrdinalIgnoreCase))
                return FontWeights.Heavy;
            else
                return FontWeights.Regular;
        }

        public struct WPFFont
        {
            public System.Windows.Media.FontFamily FontFamily;
            public System.Windows.FontWeight FontWeight;
            public int FontSizeInPoint; // In Point (72DPI)
            public int Win32FontSize { get => -(int)Math.Round(FontSizeInPoint * 96 / 72f);  }
            public double FontSizeInDIP { get => FontSizeInPoint * 96 / 72f; } // Device Independent Pixel (96DPI)

            public WPFFont(System.Windows.Media.FontFamily fontFamily, System.Windows.FontWeight fontWeight, int fontSize)
            {
                FontFamily = fontFamily;
                FontWeight = fontWeight;
                FontSizeInPoint = fontSize;
            }
        }

        public static WPFFont ChooseFontDialog(WPFFont font, Window window, bool useStyle = false, bool monospace = false)
        {
            LOGFONT logFont = new LOGFONT()
            {
                lfCharSet = LogFontCharSet.DEFAULT_CHARSET,
                lfPitchAndFamily = LogFontPitchAndFamily.DEFAULT_PITCH | LogFontPitchAndFamily.FF_DONTCARE,
                lfFaceName = font.FontFamily.Source,
                lfWeight = FontWeightConvert_WPFToLogFont(font.FontWeight),
                lfHeight = font.Win32FontSize,
            };
            IntPtr pLogFont = Marshal.AllocHGlobal(Marshal.SizeOf(logFont));
            Marshal.StructureToPtr(logFont, pLogFont, false);

            CHOOSEFONT chooseFont = new CHOOSEFONT()
            {
                hwndOwner = new WindowInteropHelper(window).Handle,
                lpLogFont = pLogFont,
                Flags = (ChooseFontFlags.CF_SCREENFONTS
                 | ChooseFontFlags.CF_FORCEFONTEXIST
                 | ChooseFontFlags.CF_INITTOLOGFONTSTRUCT // Use LOGFONT
                 | ChooseFontFlags.CF_SCALABLEONLY),
            };
            if (monospace)
                chooseFont.Flags |= ChooseFontFlags.CF_FIXEDPITCHONLY;
            if (useStyle)
                chooseFont.Flags |= ChooseFontFlags.CF_EFFECTS;
            chooseFont.lStructSize = Marshal.SizeOf(chooseFont);

            bool result = ChooseFont(ref chooseFont);
            Marshal.PtrToStructure(pLogFont, logFont);

            System.Windows.Media.FontFamily fontFamily = new System.Windows.Media.FontFamily(logFont.lfFaceName);
            System.Windows.FontWeight fontWeight = FontWeightConvert_LogFontToWPF(logFont.lfWeight);
            int fontSize = -(int) Math.Round(logFont.lfHeight * 72 / 96f); // Point - 72DPI, Device Independent Pixel - 96DPI

            Marshal.FreeHGlobal(pLogFont);

            return new WPFFont(fontFamily, fontWeight, fontSize);
        }
    }
    #endregion
}
