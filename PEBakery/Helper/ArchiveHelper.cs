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
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SevenZip;

namespace PEBakery.Helper
{
    #region ArchiveHelper
    public static class ArchiveHelper
    {
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

        public enum ArchiveCompressFormat
        {
            Zip = 1,
            /// <summary>
            /// Parsed from "7z"
            /// </summary>
            SevenZip = 2,
        }

        public enum CompressLevel
        {
            Store = 0,
            Fastest = 1,
            Normal = 6,
            Best = 9,
        }

        public static bool CompressNative(string srcPath, string destArchive, ArchiveHelper.ArchiveCompressFormat format, ArchiveHelper.CompressLevel level)
        {
            SevenZip.CompressionLevel compLevel;
            switch (level)
            {
                case ArchiveHelper.CompressLevel.Store:
                    compLevel = CompressionLevel.None;
                    break;
                case ArchiveHelper.CompressLevel.Fastest:
                    compLevel = CompressionLevel.Fast;
                    break;
                case ArchiveHelper.CompressLevel.Normal:
                    compLevel = CompressionLevel.Normal;
                    break;
                case ArchiveHelper.CompressLevel.Best:
                    compLevel = CompressionLevel.Ultra;
                    break;
                default:
                    throw new ArgumentException($"Invalid ArchiveHelper.CompressLevel [{level}]");
            }

            SevenZip.OutArchiveFormat outFormat;
            switch (format)
            {
                case ArchiveHelper.ArchiveCompressFormat.Zip:
                    outFormat = OutArchiveFormat.Zip;
                    break;
                case ArchiveHelper.ArchiveCompressFormat.SevenZip:
                    outFormat = OutArchiveFormat.SevenZip;
                    break;
                default:
                    throw new ArgumentException($"Invalid ArchiveHelper.ArchiveFormat [{format}]");
            }

            if (File.Exists(destArchive))
                File.Delete(destArchive);

            if (File.Exists(srcPath))
            {
                SevenZipCompressor compressor = new SevenZipCompressor
                {
                    ArchiveFormat = outFormat,
                    CompressionMode = CompressionMode.Create,
                    CompressionLevel = compLevel,
                    DirectoryStructure = false,
                };
                
                using (FileStream fs = new FileStream(destArchive, FileMode.Create))
                {
                    compressor.CompressFiles(fs, srcPath);
                }
            }
            else if (Directory.Exists(srcPath))
            {
                SevenZipCompressor compressor = new SevenZipCompressor
                {
                    ArchiveFormat = outFormat,
                    CompressionMode = CompressionMode.Create,
                    CompressionLevel = compLevel,
                    DirectoryStructure = true,
                    PreserveDirectoryRoot = true,
                    IncludeEmptyDirectories = true,
                };

                using (FileStream fs = new FileStream(destArchive, FileMode.Create))
                {
                    compressor.CompressDirectory(srcPath, fs);
                }
            }
            else
            {
                throw new ArgumentException($"Path [{level}] does not exist");
            }

            return File.Exists(destArchive);
        }

        public static bool CompressManagedZip(string srcPath, string destArchive, CompressLevel helperLevel, Encoding encoding = null)
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

            ZipWriterOptions opts = new ZipWriterOptions(CompressionType.Deflate)
            {
                LeaveStreamOpen = false,
                DeflateCompressionLevel = compLevel,
                UseZip64 = false,
            };
            if (encoding != null)
                opts.ArchiveEncoding = new ArchiveEncoding { Default = encoding };

            if (File.Exists(destArchive))
                File.Delete(destArchive);

            using (FileStream fs = new FileStream(destArchive, FileMode.Create, FileAccess.Write))
            {
                using (ZipWriter w = new ZipWriter(fs, opts))
                {
                    if (Directory.Exists(srcPath))
                        w.WriteAll(srcPath, "*", SearchOption.AllDirectories);
                    else if (File.Exists(srcPath))
                        w.Write(Path.GetFileName(srcPath), srcPath);
                    else
                        throw new ArgumentException($"[{srcPath}] does not exist");
                }
            }

            return File.Exists(destArchive);
        }

        public static void DecompressNative(string srcArchive, string destDir)
        {
            using (SevenZipExtractor extractor = new SevenZipExtractor(srcArchive))
            {
                extractor.ExtractArchive(destDir);
            }
        }

        public static void DecompressManaged(string srcArchive, string destDir, bool overwrite, Encoding encoding = null)
        {
            ExtractionOptions exOptions = new ExtractionOptions
            {
                ExtractFullPath = true,
                Overwrite = overwrite,
            };

            ReaderOptions opts = new ReaderOptions { LeaveStreamOpen = true, };
            if (encoding != null)
                opts.ArchiveEncoding = new ArchiveEncoding { Default = encoding };

            using (Stream stream = new FileStream(srcArchive, FileMode.Open, FileAccess.Read))
            using (IReader reader = ReaderFactory.Open(stream, opts))
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
