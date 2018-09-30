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

using PEBakery.Cab;
using SevenZipExtractor;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PEBakery.Helper
{
    #region ArchiveHelper
    public static class ArchiveHelper
    {
        public static readonly string SevenZipDllPath;

        static ArchiveHelper()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string arch = IntPtr.Size == 8 ? "x64" : "x86";

            SevenZipDllPath = Path.Combine(baseDir, arch, "7z.dll");
        }

        /// <summary>
        /// Expand cab file using P/invoked FDICreate, FDICopy, FDIDestroy
        /// </summary>
        public static bool ExtractCab(string srcCabFile, string destDir)
        {
            using (FileStream fs = new FileStream(srcCabFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (CabExtract cab = new CabExtract(fs))
            {
                return cab.ExtractAll(destDir, out _);
            }
        }

        /// <summary>
        /// Expand cab file using P/invoked FDICreate, FDICopy, FDIDestroy
        /// </summary>
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

        public static bool CompressManagedZip(string srcPath, string destArchive, CompressLevel helperLevel, Encoding encoding)
        {
            SharpCompress.Compressors.Deflate.CompressionLevel compLevel;
            switch (helperLevel)
            {
                case CompressLevel.Store:
                    compLevel = SharpCompress.Compressors.Deflate.CompressionLevel.None;
                    break;
                case CompressLevel.Fastest:
                    compLevel = SharpCompress.Compressors.Deflate.CompressionLevel.BestSpeed;
                    break;
                case CompressLevel.Normal:
                    compLevel = SharpCompress.Compressors.Deflate.CompressionLevel.Default;
                    break;
                case CompressLevel.Best:
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

            return File.Exists(destArchive);
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
            ExtractionOptions exOptions = new ExtractionOptions
            {
                ExtractFullPath = true,
                Overwrite = overwrite,
            };

            ReaderOptions rOptions = new ReaderOptions { LeaveStreamOpen = true, };
            if (encoding != null)
                rOptions.ArchiveEncoding = new ArchiveEncoding { Default = encoding };

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
}
