/*
    Copyright (C) 2018-2019 Hajin Jang
    Licensed under GPL 3.0
 
    PEBakery is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.

    Additional permission under GNU GPL version 3 section 7

    If you modify this program, or any covered work, by linking
    or combining it with external libraries, containing parts
    covered by the terms of various license, the licensors of
    this program grant you additional permission to convey the
    resulting work. An external library is a library which is
    not derived from or based on this program. 
*/

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
