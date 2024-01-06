﻿/*
    Copyright (C) 2018-2023 Hajin Jang
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

using System;

namespace PEBakery.Core
{
    #region enum Compress
    public enum CompressLevel
    {
        Store = 0,
        Fastest = 1,
        Normal = 6,
        Best = 9,
    }

    public enum ArchiveCompressFormat
    {
        Zip = 1,
        /// <summary>
        /// Parsed from "7z"
        /// </summary>
        SevenZip = 2,
    }
    #endregion

    #region ArchiveFile
    public static class ArchiveFile
    {
        #region SevenZipSharp
        public static SevenZip.CompressionLevel ToSevenZipLevel(CompressLevel level)
        {
            SevenZip.CompressionLevel compLevel;
            switch (level)
            {
                case CompressLevel.Store:
                    compLevel = SevenZip.CompressionLevel.None;
                    break;
                case CompressLevel.Fastest:
                    compLevel = SevenZip.CompressionLevel.Fast;
                    break;
                case CompressLevel.Normal:
                    compLevel = SevenZip.CompressionLevel.Normal;
                    break;
                case CompressLevel.Best:
                    compLevel = SevenZip.CompressionLevel.Ultra;
                    break;
                default:
                    throw new ArgumentException($"Invalid ArchiveHelper.CompressLevel [{level}]");
            }
            return compLevel;
        }

        public static SevenZip.OutArchiveFormat ToSevenZipOutFormat(ArchiveCompressFormat format)
        {
            SevenZip.OutArchiveFormat outFormat;
            switch (format)
            {
                case ArchiveCompressFormat.Zip:
                    outFormat = SevenZip.OutArchiveFormat.Zip;
                    break;
                case ArchiveCompressFormat.SevenZip:
                    outFormat = SevenZip.OutArchiveFormat.SevenZip;
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
