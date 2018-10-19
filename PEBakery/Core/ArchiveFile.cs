using SevenZip;
using System;

namespace PEBakery.Core
{
    #region ArchiveFile
    public static class ArchiveFile
    {
        #region SevenZipSharp
        public enum CompressLevel
        {
            Store = 0,
            Fastest = 1,
            Normal = 6,
            Best = 9,
        }

        public static SevenZip.CompressionLevel ToSevenZipLevel(ArchiveFile.CompressLevel level)
        {
            SevenZip.CompressionLevel compLevel;
            switch (level)
            {
                case ArchiveFile.CompressLevel.Store:
                    compLevel = SevenZip.CompressionLevel.None;
                    break;
                case ArchiveFile.CompressLevel.Fastest:
                    compLevel = SevenZip.CompressionLevel.Fast;
                    break;
                case ArchiveFile.CompressLevel.Normal:
                    compLevel = SevenZip.CompressionLevel.Normal;
                    break;
                case ArchiveFile.CompressLevel.Best:
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

        public static SevenZip.OutArchiveFormat ToSevenZipOutFormat(ArchiveFile.ArchiveCompressFormat format)
        {
            SevenZip.OutArchiveFormat outFormat;
            switch (format)
            {
                case ArchiveFile.ArchiveCompressFormat.Zip:
                    outFormat = OutArchiveFormat.Zip;
                    break;
                case ArchiveFile.ArchiveCompressFormat.SevenZip:
                    outFormat = OutArchiveFormat.SevenZip;
                    break;
                default:
                    throw new ArgumentException($"Invalid ArchiveHelper.ArchiveFormat [{format}]");
            }
            return outFormat;
        }
        #endregion
    }
    #endregion
}
