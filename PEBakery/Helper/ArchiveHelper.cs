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

using SevenZip;
using System;
using System.IO;

namespace PEBakery.Helper
{
    #region ArchiveHelper
    public static class ArchiveHelper
    {
        #region SevenZipSharp
        public enum CompressLevel
        {
            Store = 0,
            Fastest = 1,
            Normal = 6,
            Best = 9,
        }

        public static SevenZip.CompressionLevel ToSevenZipLevel(ArchiveHelper.CompressLevel level)
        {
            SevenZip.CompressionLevel compLevel;
            switch (level)
            {
                case ArchiveHelper.CompressLevel.Store:
                    compLevel = SevenZip.CompressionLevel.None;
                    break;
                case ArchiveHelper.CompressLevel.Fastest:
                    compLevel = SevenZip.CompressionLevel.Fast;
                    break;
                case ArchiveHelper.CompressLevel.Normal:
                    compLevel = SevenZip.CompressionLevel.Normal;
                    break;
                case ArchiveHelper.CompressLevel.Best:
                    compLevel = SevenZip.CompressionLevel.Ultra;
                    break;
                default:
                    throw new ArgumentException($"Invalid ArchiveHelper.CompressLevel [{level}]");
            }
            return compLevel;
        }

        public enum ArchiveCompressFormat
        {
            Zip = 1,
            /// <summary>
            /// Parsed from "7z"
            /// </summary>
            SevenZip = 2,
        }

        public static SevenZip.OutArchiveFormat ToSevenZipOutFormat(ArchiveHelper.ArchiveCompressFormat format)
        {
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
            return outFormat;
        }

        public static bool CompressNative(string srcPath, string destArchive,
            ArchiveHelper.ArchiveCompressFormat format, ArchiveHelper.CompressLevel level)
        {
            SevenZip.OutArchiveFormat outFormat = ToSevenZipOutFormat(format);
            SevenZip.CompressionLevel compLevel = ToSevenZipLevel(level);

            // Overwrite is default
            if (File.Exists(destArchive))
                File.Delete(destArchive);

            SevenZipCompressor compressor = new SevenZipCompressor
            {
                ArchiveFormat = outFormat,
                CompressionMode = CompressionMode.Create,
                CompressionLevel = compLevel,

            };

            switch (outFormat)
            {
                case OutArchiveFormat.Zip:
                    compressor.CustomParameters["cu"] = "on"; // Force UTF-8 for filename
                    break;
            }

            if (File.Exists(srcPath))
            {
                compressor.DirectoryStructure = false;

                using (FileStream fs = new FileStream(destArchive, FileMode.Create))
                {
                    compressor.CompressFiles(fs, srcPath);
                }
            }
            else if (Directory.Exists(srcPath))
            {
                compressor.DirectoryStructure = true;
                compressor.PreserveDirectoryRoot = true;
                compressor.IncludeEmptyDirectories = true;

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

        public static void DecompressNative(string srcArchive, string destDir)
        {
            using (SevenZipExtractor extractor = new SevenZipExtractor(srcArchive))
            {
                extractor.ExtractArchive(destDir);
            }
        }
        #endregion
    }
    #endregion
}
