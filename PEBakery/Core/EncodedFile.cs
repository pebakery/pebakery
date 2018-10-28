/*
    Copyright (C) 2016-2018 Hajin Jang
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

// #define DEBUG_MIDDLE_FILE
#define OPT_ENCODE

using Joveler.Compression.XZ;
using Joveler.Compression.ZLib;
using PEBakery.Helper;
using PEBakery.Ini;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PEBakery.Core
{
    #region Attachment Format
    /*
    [Attachment Format]
    Streams are encoded in base64 format.
    Concat all lines into one long string, append '=', '==' or nothing according to length.
    (Need '=' padding to be appended to be .Net acknowledged base64 format)
    Decode base64 encoded string to get binary, which follows these 2 types.
    
    Note)
    All bytes is ordered in little endian.
    WB082-generated zlib magic number always starts with 0x78.
    CodecWBZip is a combination of Type 1 and 2, choosing algorithm based on file extension.

    See here for possible zlib stream magic numbers.
    https://groups.google.com/forum/#!msg/comp.compression/_y2Wwn_Vq_E/EymIVcQ52cEJ

    [Type 1]
    Zlib Compressed File + Zlib Compressed FirstFooter + Raw FinalFooter
    - Used in most file.

    [Type 2]
    Raw File + Zlib Compressed FirstFooter + Raw FinalFooter
    - Used in already compressed file (Ex 7z, zip).

    [Type 3] (PEBakery Only!)
    XZ Compressed File + Zlib Compressed FirstFooter + Raw FinalFooter
    - Use this for ultimate compress ratio.

    [FirstFooter]
    550Byte (0x226) (When decompressed)
    0x000 - 0x1FF (512B) -> L-V (Length - Value)
        1B : [Length of FileName]
        511B : [FileName]
    0x200 - 0x207 : 8B  -> Length of Raw File
    0x208 - 0x20F : 8B  -> (Type 1) Length of zlib-compressed File
                           (Type 2) Null-padded
                           (Type 3) Length of LZMA-compressed File
    0x210 - 0x21F : 16B -> Null-padded
    0x220 - 0x223 : 4B  -> CRC32 of Raw File
    0x224         : 1B  -> Compress Mode (Type 1 : 00, Type 2 : 01, Type 3 : 02)
    0x225         : 1B  -> Compress Level (Type 1, 3 : 01 ~ 09, Type 2 : 00)

    [FinalFooter]
    Not compressed, 36Byte (0x24)
    0x00 - 0x04   : 4B  -> CRC32 of Zlib-Compressed File and Zlib-Compressed FirstFooter
    0x04 - 0x08   : 4B  -> Unknown - Always 1 
    0x08 - 0x0B   : 4B  -> WB082 ZLBArchive Component version - Always 2
    0x0C - 0x0F   : 4B  -> Zlib Compressed FirstFooter Length
    0x10 - 0x17   : 8B  -> Zlib Compressed File Length
    0x18 - 0x1B   : 4B  -> Unknown - Always 1
    0x1C - 0x23   : 8B  -> Unknown - Always 0
    
    Note) Which purpose do Unknown entries have?
    0x04 : When changed, WB082 cannot recognize filename. Maybe related to filename encoding?
    0x08 : When changed to higher value than 2, WB082 refuses to decompress with error message
        Error Message = $"The archive was created with a different version of ZLBArchive v{value}"
    0x18 : Decompress by WB082 is unaffected by this value
    0x1C : When changed, WB082 thinks the encoded file is corrupted
    
    [Improvement Points]
    - Zopfli support in place of zlib, for better compression rate while keeping compability with WB082
    - Design more robust script format. 
    */
    #endregion

    #region EncodedFile
    public class EncodedFile
    {
        #region Enum EncodeMode 
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public enum EncodeMode : byte
        {
            ZLib = 0x00, // Type 1
            Raw = 0x01, // Type 2
            XZ = 0x02, // Type 3 (PEBakery Only)
        }

        public static EncodeMode ParseEncodeMode(string str)
        {
            EncodeMode mode;
            if (str.Equals("ZLib", StringComparison.OrdinalIgnoreCase) || str.Equals("Deflate", StringComparison.OrdinalIgnoreCase))
                mode = EncodeMode.ZLib;
            else if (str.Equals("Raw", StringComparison.OrdinalIgnoreCase) || str.Equals("None", StringComparison.OrdinalIgnoreCase))
                mode = EncodeMode.Raw;
            else if (str.Equals("XZ", StringComparison.OrdinalIgnoreCase) || str.Equals("LZMA2", StringComparison.OrdinalIgnoreCase))
                mode = EncodeMode.XZ;
            else
                throw new ArgumentException($"Wrong EncodeMode [{str}]");

            return mode;
        }

        public static string EncodeModeStr(EncodeMode? mode, bool containerName)
        {
            return mode == null ? "Unknown" : EncodeModeStr((EncodeMode)mode, containerName);
        }

        public static string EncodeModeStr(EncodeMode mode, bool containerName)
        {
            if (containerName)
                return mode.ToString();

            switch (mode)
            {
                case EncodeMode.ZLib:
                    return "Deflate";
                case EncodeMode.Raw:
                    return "None";
                case EncodeMode.XZ:
                    return "LZMA2";
                default:
                    throw new ArgumentException($"Wrong EncodeMode [{mode}]");
            }
        }
        #endregion

        #region Const Strings, String Factory
        public const long InterfaceSizeLimit = 4 * 1024 * 1024; // 4MB
        private const long BufferSize = 64 * 1024; // 64KB
        private const long ReportInterval = 1024 * 1024; // 1MB

        public const double CompReportFactor = 0.8;
        public const double Base64ReportFactor = 0.2;
        #endregion

        #region Dict ImageEncodeDict
        public static readonly ReadOnlyDictionary<ImageHelper.ImageType, EncodeMode> ImageEncodeDict = new ReadOnlyDictionary<ImageHelper.ImageType, EncodeMode>(
            new Dictionary<ImageHelper.ImageType, EncodeMode>
            {
                // Auto detect compress algorithm by extension.
                // Note: .ico file can be either raw (bitmap) or compressed (png).
                //       To be sure, use EncodeMode.ZLib in .ico file.
                { ImageHelper.ImageType.Bmp, EncodeMode.ZLib },
                { ImageHelper.ImageType.Jpg, EncodeMode.Raw },
                { ImageHelper.ImageType.Png, EncodeMode.Raw },
                { ImageHelper.ImageType.Gif, EncodeMode.Raw },
                { ImageHelper.ImageType.Ico, EncodeMode.ZLib },
                { ImageHelper.ImageType.Svg, EncodeMode.ZLib },
            });
        #endregion

        #region AttachFile, ContainsFile
        public static Task<Script> AttachFileAsync(Script sc, string folderName, string fileName, string srcFilePath, EncodeMode type, IProgress<double> progress)
        {
            return Task.Run(() => AttachFile(sc, folderName, fileName, srcFilePath, type, progress));
        }

        public static Script AttachFile(Script sc, string folderName, string fileName, string srcFilePath, EncodeMode type, IProgress<double> progress)
        {
            if (sc == null)
                throw new ArgumentNullException(nameof(sc));

            if (!StringEscaper.IsFileNameValid(folderName, new char[] { '[', ']', '\t' }))
                throw new ArgumentException($"[{folderName}] contains invalid character");
            if (!StringEscaper.IsFileNameValid(fileName, new char[] { '[', ']', '\t' }))
                throw new ArgumentException($"[{fileName}] contains invalid character");

            using (FileStream fs = new FileStream(srcFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return Encode(sc, folderName, fileName, fs, type, false, progress);
            }
        }

        public static Task<Script> AttachFileAsync(Script sc, string folderName, string fileName, Stream srcStream, EncodeMode type, IProgress<double> progress)
        {
            return Task.Run(() => AttachFile(sc, folderName, fileName, srcStream, type, progress));
        }

        public static Script AttachFile(Script sc, string folderName, string fileName, Stream srcStream, EncodeMode type, IProgress<double> progress)
        {
            if (sc == null)
                throw new ArgumentNullException(nameof(sc));

            if (!StringEscaper.IsFileNameValid(folderName, new char[] { '[', ']', '\t' }))
                throw new ArgumentException($"[{folderName}] contains invalid character");
            if (!StringEscaper.IsFileNameValid(fileName, new char[] { '[', ']', '\t' }))
                throw new ArgumentException($"[{fileName}] contains invalid character");

            return Encode(sc, folderName, fileName, srcStream, type, false, progress);
        }

        public static Task<Script> AttachFileAsync(Script sc, string folderName, string fileName, byte[] srcBuffer, EncodeMode type, IProgress<double> progress)
        {
            return Task.Run(() => AttachFile(sc, folderName, fileName, srcBuffer, type, progress));
        }

        public static Script AttachFile(Script sc, string folderName, string fileName, byte[] srcBuffer, EncodeMode type, IProgress<double> progress)
        {
            if (sc == null)
                throw new ArgumentNullException(nameof(sc));

            if (!StringEscaper.IsFileNameValid(folderName, new char[] { '[', ']', '\t' }))
                throw new ArgumentException($"[{folderName}] contains invalid character");
            if (!StringEscaper.IsFileNameValid(fileName, new char[] { '[', ']', '\t' }))
                throw new ArgumentException($"[{fileName}] contains invalid character");

            return Encode(sc, folderName, fileName, srcBuffer, type, false, progress);
        }

        public static bool ContainsFile(Script sc, string folderName, string fileName)
        {
            if (!sc.Sections.ContainsKey(folderName))
                return false;

            // Get encoded file index
            Dictionary<string, string> fileDict = sc.Sections[folderName].IniDict;
            if (fileDict == null)
                return false;
            if (!fileDict.ContainsKey(fileName))
                return false;

            return sc.Sections.ContainsKey(ScriptSection.Names.GetEncodedSectionName(folderName, fileName));
        }
        #endregion

        #region AttachInterface, ContainsInterface
        public static Task<Script> AttachInterfaceAsync(Script sc, string fileName, string srcFilePath, IProgress<double> progress)
        {
            return Task.Run(() => AttachInterface(sc, fileName, srcFilePath, progress));
        }

        public static Script AttachInterface(Script sc, string fileName, string srcFilePath, IProgress<double> progress)
        {
            if (!StringEscaper.IsFileNameValid(fileName, new char[] { '[', ']', '\t' }))
                throw new ArgumentException($"[{fileName}] contains invalid character");

            EncodeMode type = EncodeMode.ZLib;
            if (ImageHelper.GetImageType(srcFilePath, out ImageHelper.ImageType imageType))
            {
                if (ImageEncodeDict.ContainsKey(imageType))
                    type = ImageEncodeDict[imageType];
            }

            return AttachFile(sc, ScriptSection.Names.InterfaceEncoded, fileName, srcFilePath, type, progress);
        }

        public static bool ContainsInterface(Script sc, string fileName)
        {
            return ContainsFile(sc, ScriptSection.Names.InterfaceEncoded, fileName);
        }
        #endregion

        #region AttachLogo, ContainsLogo
        public static Task<Script> AttachLogoAsync(Script sc, string fileName, string srcFilePath)
        {
            return Task.Run(() => AttachLogo(sc, fileName, srcFilePath));
        }

        public static Script AttachLogo(Script sc, string fileName, string srcFilePath)
        {
            if (sc == null)
                throw new ArgumentNullException(nameof(sc));
            if (srcFilePath == null)
                throw new ArgumentNullException(nameof(srcFilePath));

            if (!StringEscaper.IsFileNameValid(fileName, new char[] { '[', ']', '\t' }))
                throw new ArgumentException($"[{fileName}] contains invalid character");

            if (!ImageHelper.GetImageType(srcFilePath, out ImageHelper.ImageType imageType))
                throw new ArgumentException($"Image [{Path.GetExtension(srcFilePath)}] is not supported");

            using (FileStream fs = new FileStream(srcFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return Encode(sc, ScriptSection.Names.AuthorEncoded, fileName, fs, ImageEncodeDict[imageType], true, null);
            }
        }

        public static Script AttachLogo(Script sc, string folderName, string fileName, Stream srcStream, EncodeMode type)
        {
            if (sc == null)
                throw new ArgumentNullException(nameof(sc));

            return Encode(sc, folderName, fileName, srcStream, type, true, null);
        }

        public static Script AttachLogo(Script sc, string folderName, string fileName, byte[] srcBuffer, EncodeMode type)
        {
            if (sc == null)
                throw new ArgumentNullException(nameof(sc));

            return Encode(sc, folderName, fileName, srcBuffer, type, true, null);
        }

        public static bool ContainsLogo(Script sc)
        {
            if (sc == null)
                throw new ArgumentNullException(nameof(sc));

            if (!sc.Sections.ContainsKey(ScriptSection.Names.AuthorEncoded))
                return false;

            Dictionary<string, string> fileDict = sc.Sections[ScriptSection.Names.AuthorEncoded].IniDict;
            if (!fileDict.ContainsKey("Logo"))
                return false;

            string logoName = fileDict["Logo"];
            return sc.Sections.ContainsKey(ScriptSection.Names.GetEncodedSectionName(ScriptSection.Names.AuthorEncoded, logoName));
        }
        #endregion

        #region AddFolder, ContainsFolder
        public static Task<Script> AddFolderAsync(Script sc, string folderName, bool overwrite)
        {
            return Task.Run(() => AddFolder(sc, folderName, overwrite));
        }

        public static Script AddFolder(Script sc, string folderName, bool overwrite)
        {
            if (sc == null)
                throw new ArgumentNullException(nameof(sc));
            if (folderName == null)
                throw new ArgumentNullException(nameof(folderName));

            if (!StringEscaper.IsFileNameValid(folderName, new char[] { '[', ']', '\t' }))
                throw new ArgumentException($"[{folderName}] contains invalid character");

            if (!overwrite)
            {
                if (sc.Sections.ContainsKey(folderName))
                    throw new InvalidOperationException($"Section [{folderName}] already exists");
            }

            // Write folder name into EncodedFolder (except AuthorEncoded, InterfaceEncoded)
            if (!folderName.Equals(ScriptSection.Names.AuthorEncoded, StringComparison.OrdinalIgnoreCase) &&
                !folderName.Equals(ScriptSection.Names.InterfaceEncoded, StringComparison.OrdinalIgnoreCase))
            {
                if (sc.Sections.ContainsKey(ScriptSection.Names.EncodedFolders))
                {
                    string[] folders = sc.Sections[ScriptSection.Names.EncodedFolders].Lines;
                    if (Array.FindIndex(folders, x => x.Equals(folderName, StringComparison.OrdinalIgnoreCase)) == -1)
                        IniReadWriter.WriteRawLine(sc.RealPath, ScriptSection.Names.EncodedFolders, folderName, false);
                }
                else
                {
                    IniReadWriter.WriteRawLine(sc.RealPath, ScriptSection.Names.EncodedFolders, folderName, false);
                }
            }

            IniReadWriter.AddSection(sc.RealPath, folderName);
            return sc.Project.RefreshScript(sc);
        }

        public static bool ContainsFolder(Script sc, string folderName)
        {
            if (sc == null)
                throw new ArgumentNullException(nameof(sc));
            if (folderName == null)
                throw new ArgumentNullException(nameof(folderName));

            // AuthorEncoded, InterfaceEncoded is not recorded to EncodedFolders
            if (folderName.Equals(ScriptSection.Names.AuthorEncoded, StringComparison.OrdinalIgnoreCase) ||
                folderName.Equals(ScriptSection.Names.InterfaceEncoded, StringComparison.OrdinalIgnoreCase))
                return sc.Sections.ContainsKey(folderName);

            if (sc.Sections.ContainsKey(folderName) && sc.Sections.ContainsKey(ScriptSection.Names.EncodedFolders))
            {
                string[] folders = sc.Sections[ScriptSection.Names.EncodedFolders].Lines;
                return Array.FindIndex(folders, x => x.Equals(folderName, StringComparison.OrdinalIgnoreCase)) != -1;
            }

            return false;
        }
        #endregion

        #region ExtractFile, ExtractFolder, ExtractLogo, ExtractInterface
        public static Task<long> ExtractFileAsync(Script sc, string folderName, string fileName, Stream outStream, IProgress<double> progress)
        {
            return Task.Run(() => ExtractFile(sc, folderName, fileName, outStream, progress));
        }

        public static long ExtractFile(Script sc, string folderName, string fileName, Stream outStream, IProgress<double> progress)
        {
            if (sc == null)
                throw new ArgumentNullException(nameof(sc));

            string section = ScriptSection.Names.GetEncodedSectionName(folderName, fileName);
            if (!sc.Sections.ContainsKey(section))
                throw new InvalidOperationException($"[{folderName}\\{fileName}] does not exists in [{sc.RealPath}]");

            return Decode(sc.RealPath, section, outStream, progress);
        }

        public static Task<MemoryStream> ExtractFileInMemAsync(Script sc, string folderName, string fileName)
        {
            return Task.Run(() => ExtractFileInMem(sc, folderName, fileName));
        }

        public static MemoryStream ExtractFileInMem(Script sc, string folderName, string fileName)
        {
            if (sc == null)
                throw new ArgumentNullException(nameof(sc));

            string section = ScriptSection.Names.GetEncodedSectionName(folderName, fileName);
            if (!sc.Sections.ContainsKey(section))
                throw new InvalidOperationException($"[{folderName}\\{fileName}] does not exists in [{sc.RealPath}]");

            string[] encoded = sc.Sections[section].Lines;
            if (encoded == null)
                throw new InvalidOperationException($"Unable to find [{folderName}\\{fileName}] from [{sc.RealPath}]");
            return DecodeInMem(encoded);
        }

        public static Task ExtractFolderAsync(Script sc, string folderName, string destDir, bool overwrite = false)
        {
            return Task.Run(() => ExtractFolder(sc, folderName, destDir, overwrite));
        }

        public static void ExtractFolder(Script sc, string folderName, string destDir, bool overwrite = false)
        {
            if (sc == null)
                throw new ArgumentNullException(nameof(sc));

            Dictionary<string, string> fileDict = sc.Sections[folderName].IniDict;
            if (fileDict == null)
                throw new InvalidOperationException($"Unable to find [{folderName}] from [{sc.RealPath}]");

            if (!Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            foreach (string fileName in fileDict.Keys)
            {
                string destFile = Path.Combine(destDir, fileName);
                if (!overwrite && File.Exists(destFile))
                    throw new InvalidOperationException($"File [{destFile}] cannot be overwritten");

                using (FileStream fs = new FileStream(destFile, FileMode.Create, FileAccess.Write))
                {
                    string section = ScriptSection.Names.GetEncodedSectionName(folderName, fileName);
                    if (!sc.Sections.ContainsKey(section))
                        throw new InvalidOperationException($"[{folderName}\\{fileName}] does not exists in [{sc.RealPath}]");

                    Decode(sc.RealPath, section, fs, null);
                }
            }
        }

        public static Task<MemoryStream> ExtractLogoAsync(Script sc)
        {
            return Task.Run(() => ExtractLogo(sc, out _));
        }

        public static MemoryStream ExtractLogo(Script sc, out ImageHelper.ImageType type)
        {
            if (sc == null)
                throw new ArgumentNullException(nameof(sc));

            if (!sc.Sections.ContainsKey(ScriptSection.Names.AuthorEncoded))
                throw new InvalidOperationException("Directory [AuthorEncoded] does not exist");

            Dictionary<string, string> fileDict = sc.Sections[ScriptSection.Names.AuthorEncoded].IniDict;
            if (!fileDict.ContainsKey("Logo"))
                throw new InvalidOperationException($"Logo does not exist in [{sc.Title}]");

            string logoFile = fileDict["Logo"];
            if (!ImageHelper.GetImageType(logoFile, out type))
                throw new ArgumentException($"Image [{Path.GetExtension(logoFile)}] is not supported");

            string section = ScriptSection.Names.GetEncodedSectionName(ScriptSection.Names.AuthorEncoded, logoFile);
            if (!sc.Sections.ContainsKey(section))
                throw new InvalidOperationException($"Unable to find [{logoFile}] from [{sc.RealPath}]");

            string[] encoded = sc.Sections[section].Lines;
            return DecodeInMem(encoded);
        }

        public static Task<MemoryStream> ExtractInterfaceAsync(Script sc, string fileName)
        {
            return Task.Run(() => ExtractInterface(sc, fileName));
        }

        public static MemoryStream ExtractInterface(Script sc, string fileName)
        {
            string section = ScriptSection.Names.GetEncodedSectionName(ScriptSection.Names.InterfaceEncoded, fileName);
            if (!sc.Sections.ContainsKey(section))
                throw new InvalidOperationException($"[{fileName}] does not exist in interface of [{sc.RealPath}]");

            string[] encoded = sc.Sections[section].Lines;
            return DecodeInMem(encoded);
        }
        #endregion

        #region GetFileInfo, GetLogoInfo, GetFolderInfo, GetAllFilesInfo

        public static Task<(EncodedFileInfo, string)> GetFileInfoAsync(Script sc, string folderName, string fileName, bool detail = false)
        {
            return Task.Run(() => GetFileInfo(sc, folderName, fileName, detail));
        }

        public static (EncodedFileInfo info, string errMsg) GetFileInfo(Script sc, string folderName, string fileName, bool detail = false)
        {
            if (sc == null)
                throw new ArgumentNullException(nameof(sc));

            EncodedFileInfo info = new EncodedFileInfo
            {
                FolderName = folderName,
                FileName = fileName,
            };

            if (!sc.Sections.ContainsKey(folderName))
                return (null, $"Directory [{folderName}] does not exist");

            Dictionary<string, string> fileDict = sc.Sections[folderName].IniDict;
            if (!fileDict.ContainsKey(fileName))
                return (null, $"File index of [{fileName}] does not exist");

            string fileIndex = fileDict[fileName].Trim();
            (info.RawSize, info.EncodedSize) = ParseFileIndex(fileIndex);
            if (info.RawSize == -1)
                return (null, $"Unable to parse raw size of [{fileName}]");
            if (info.EncodedSize == -1)
                return (null, $"Unable to parse encoded size of [{fileName}]");

            if (detail)
            {
                string section = ScriptSection.Names.GetEncodedSectionName(folderName, fileName);
                info.EncodeMode = GetEncodeMode(sc.RealPath, section);
            }

            return (info, null);
        }

        public static Task<(EncodedFileInfo info, string errMsg)> GetLogoInfoAsync(Script sc, bool detail = false)
        {
            return Task.Run(() => GetLogoInfo(sc, detail));
        }

        public static (EncodedFileInfo info, string errMsg) GetLogoInfo(Script sc, bool detail = false)
        {
            if (sc == null)
                throw new ArgumentNullException(nameof(sc));

            EncodedFileInfo info = new EncodedFileInfo { FolderName = ScriptSection.Names.AuthorEncoded };

            if (!sc.Sections.ContainsKey(ScriptSection.Names.AuthorEncoded))
                return (null, $"Directory [{ScriptSection.Names.AuthorEncoded}] does not exist");

            Dictionary<string, string> fileDict = sc.Sections[ScriptSection.Names.AuthorEncoded].IniDict;
            if (!fileDict.ContainsKey("Logo"))
                return (null, "Logo does not exist");

            info.FileName = fileDict["Logo"];
            if (!fileDict.ContainsKey(info.FileName))
                return (null, "File index of [Logo] does not exist");

            string fileIndex = fileDict[info.FileName].Trim();
            (info.RawSize, info.EncodedSize) = ParseFileIndex(fileIndex);
            if (info.RawSize == -1)
                return (null, $"Unable to parse raw size of [{info.FileName}]");
            if (info.EncodedSize == -1)
                return (null, $"Unable to parse encoded size of [{info.FileName}]");

            if (detail)
            {
                string section = ScriptSection.Names.GetEncodedSectionName(ScriptSection.Names.AuthorEncoded, info.FileName);
                if (!sc.Sections.ContainsKey(section))
                    throw new InvalidOperationException($"[{info.FileName}] does not exist in interface of [{sc.RealPath}]");

                string[] encoded = sc.Sections[section].Lines;
                info.EncodeMode = GetEncodeModeInMem(encoded);
            }

            return (info, null);
        }

        public static Task<(List<EncodedFileInfo> infos, string errMsg)> GetFolderInfoAsync(Script sc, string folderName, bool detail = false)
        {
            return Task.Run(() => GetFolderInfo(sc, folderName, detail));
        }

        public static (List<EncodedFileInfo> infos, string errMsg) GetFolderInfo(Script sc, string folderName, bool detail = false)
        {
            if (sc == null)
                throw new ArgumentNullException(nameof(sc));

            if (!sc.Sections.ContainsKey(folderName))
                return (null, $"Directory [{folderName}] does not exist");

            List<EncodedFileInfo> infos = new List<EncodedFileInfo>();
            Dictionary<string, string> fileDict = sc.Sections[folderName].IniDict;
            foreach (string fileName in fileDict.Keys)
            {
                EncodedFileInfo info = new EncodedFileInfo
                {
                    FolderName = folderName,
                    FileName = fileName,
                };

                if (!fileDict.ContainsKey(fileName))
                    return (null, $"File index of [{fileName}] does not exist");

                string fileIndex = fileDict[fileName].Trim();
                (info.RawSize, info.EncodedSize) = ParseFileIndex(fileIndex);
                if (info.RawSize == -1)
                    return (null, $"Unable to parse raw size of [{fileName}]");
                if (info.EncodedSize == -1)
                    return (null, $"Unable to parse encoded size of [{fileName}]");

                if (detail)
                {
                    string section = ScriptSection.Names.GetEncodedSectionName(folderName, fileName);
                    info.EncodeMode = GetEncodeMode(sc.RealPath, section);
                }

                infos.Add(info);
            }

            return (infos, null);
        }

        public static Task<(Dictionary<string, List<EncodedFileInfo>> infoDict, string errMsg)> GetAllFilesInfoAsync(Script sc, bool detail = false)
        {
            return Task.Run(() => GetAllFilesInfo(sc, detail));
        }

        public static (Dictionary<string, List<EncodedFileInfo>> infoDict, string errMsg) GetAllFilesInfo(Script sc, bool detail = false)
        {
            if (sc == null)
                throw new ArgumentNullException(nameof(sc));

            Dictionary<string, List<EncodedFileInfo>> infoDict = new Dictionary<string, List<EncodedFileInfo>>(StringComparer.OrdinalIgnoreCase);
            if (!sc.Sections.ContainsKey(ScriptSection.Names.EncodedFolders))
                return (infoDict, null); // Return empty dict

            List<string> folderNames = IniReadWriter.FilterCommentLines(sc.Sections[ScriptSection.Names.EncodedFolders].Lines);
            int aeIdx = folderNames.FindIndex(x => x.Equals(ScriptSection.Names.AuthorEncoded, StringComparison.OrdinalIgnoreCase));
            if (aeIdx != -1)
            {
                Global.Logger.SystemWrite(new LogInfo(LogState.Error, $"Error at script [{sc.TreePath}]\r\nSection [AuthorEncoded] should not be listed in [EncodedFolders]"));
                folderNames.RemoveAt(aeIdx);
            }

            int ieIdx = folderNames.FindIndex(x => x.Equals(ScriptSection.Names.InterfaceEncoded, StringComparison.OrdinalIgnoreCase));
            if (ieIdx != -1)
            {
                Global.Logger.SystemWrite(new LogInfo(LogState.Error, $"Error at script [{sc.TreePath}]\r\nSection [InterfaceEncoded] should not be listed in [EncodedFolders]"));
                folderNames.RemoveAt(ieIdx);
            }

            foreach (string folderName in folderNames)
            {
                if (!infoDict.ContainsKey(folderName))
                    infoDict[folderName] = new List<EncodedFileInfo>();

                // Follow WB082 behavior
                if (!sc.Sections.ContainsKey(folderName))
                    continue;

                /*
                   Example

                   [Fonts]
                   README.txt=522,696
                   D2Coding-OFL-License.txt=2102,2803
                   D2Coding-Ver1.2-TTC-20161024.7z=3118244,4157659
                */
                Dictionary<string, string> fileDict = sc.Sections[folderName].IniDict;
                foreach (var kv in fileDict)
                {
                    string fileName = kv.Key;
                    string fileIndex = kv.Value;

                    EncodedFileInfo info = new EncodedFileInfo
                    {
                        FolderName = folderName,
                        FileName = fileName,
                    };

                    if (!fileDict.ContainsKey(fileName))
                        return (null, $"File index of [{fileName}] does not exist");

                    (info.RawSize, info.EncodedSize) = ParseFileIndex(fileIndex);
                    if (info.RawSize == -1)
                        return (null, $"Unable to parse raw size of [{fileName}]");
                    if (info.EncodedSize == -1)
                        return (null, $"Unable to parse encoded size of [{fileName}]");

                    if (detail)
                    {
                        string section = ScriptSection.Names.GetEncodedSectionName(folderName, fileName);
                        info.EncodeMode = GetEncodeMode(sc.RealPath, section);
                    }

                    infoDict[folderName].Add(info);
                }
            }

            return (infoDict, null);
        }

        /// <summary>
        /// Parse file index
        /// </summary>
        /// <param name="fileIndex">String of file index Ex) "522,696"</param>
        /// <returns>
        /// If succeed, return (rawSize, encodedSize)
        /// If failes, return (-1, -1)
        /// </returns>
        private static (int rawSize, int encodedSize) ParseFileIndex(string fileIndex)
        {
            Match m = Regex.Match(fileIndex, @"([0-9]+),([0-9]+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
            if (!m.Success)
                return (-1, -1);

            if (!NumberHelper.ParseInt32(m.Groups[1].Value, out int rawSize))
                return (-1, 0);
            if (!NumberHelper.ParseInt32(m.Groups[2].Value, out int encodedSize))
                return (rawSize, -1);

            return (rawSize, encodedSize);
        }
        #endregion

        #region DeleteFile, DeleteFolder, DeleteLogo
        public static Task<(Script, string)> DeleteFileAsync(Script sc, string folderName, string fileName)
        {
            return Task.Run(() => DeleteFile(sc, folderName, fileName));
        }

        public static (Script, string) DeleteFile(Script sc, string folderName, string fileName)
        {
            if (sc == null)
                throw new ArgumentNullException(nameof(sc));
            if (folderName == null)
                throw new ArgumentNullException(nameof(folderName));
            if (fileName == null)
                throw new ArgumentNullException(nameof(fileName));
            string errorMsg = null;

            // Backup
            string backupFile = Path.GetTempFileName();
            File.Copy(sc.RealPath, backupFile, true);
            try
            {
                if (!sc.Sections.ContainsKey(folderName))
                    return (sc, $"Index of encoded folder [{folderName}] not found in [{sc.RealPath}]");

                // Get encoded file index
                Dictionary<string, string> fileDict = sc.Sections[folderName].IniDict;
                if (!fileDict.ContainsKey(fileName))
                    return (sc, $"Index of encoded file [{fileName}] not found in [{sc.RealPath}]");

                // Delete encoded file index
                if (!IniReadWriter.DeleteKey(sc.RealPath, folderName, fileName))
                    errorMsg = $"Unable to delete index of encoded file [{fileName}] from [{sc.RealPath}]";

                // Delete encoded file section
                if (!IniReadWriter.DeleteSection(sc.RealPath, ScriptSection.Names.GetEncodedSectionName(folderName, fileName)))
                    errorMsg = $"Unable to delete encoded file [{fileName}] from [{sc.RealPath}]";
            }
            catch
            { // Error -> Rollback!
                File.Copy(backupFile, sc.RealPath, true);
                throw;
            }
            finally
            { // Delete backup script
                if (File.Exists(backupFile))
                    File.Delete(backupFile);
            }

            // Return refreshed script
            sc = sc.Project.RefreshScript(sc);
            return (sc, errorMsg);
        }

        public static Task<(Script, string)> DeleteFolderAsync(Script sc, string folderName)
        {
            return Task.Run(() => DeleteFolder(sc, folderName));
        }

        public static (Script, string) DeleteFolder(Script sc, string folderName)
        {
            if (sc == null)
                throw new ArgumentNullException(nameof(sc));
            if (folderName == null)
                throw new ArgumentNullException(nameof(folderName));
            string errorMsg = null;

            // Backup
            string backupFile = Path.GetTempFileName();
            File.Copy(sc.RealPath, backupFile, true);
            try
            {
                if (!folderName.Equals(ScriptSection.Names.AuthorEncoded, StringComparison.OrdinalIgnoreCase) &&
                    !folderName.Equals(ScriptSection.Names.InterfaceEncoded, StringComparison.OrdinalIgnoreCase))
                {
                    if (!sc.Sections.ContainsKey(ScriptSection.Names.EncodedFolders))
                        return (sc, $"Index of encoded folder [{folderName}] not found in [{sc.RealPath}]");

                    List<string> folders = IniReadWriter.FilterCommentLines(sc.Sections[ScriptSection.Names.EncodedFolders].Lines);
                    int idx = folders.FindIndex(x => x.Equals(folderName, StringComparison.OrdinalIgnoreCase));
                    if (!folders.Contains(folderName, StringComparer.OrdinalIgnoreCase))
                        return (sc, $"Index of encoded folder [{folderName}] not found in [{sc.RealPath}]");

                    // Delete index of encoded folder
                    folders.RemoveAt(idx);
                    // Cannot use DeleteKey, since [EncodedFolders] does not use '=' in its content
                    if (!IniReadWriter.DeleteSection(sc.RealPath, ScriptSection.Names.EncodedFolders))
                        return (sc, $"Unable to delete index of encoded folder [{folderName}] from [{sc.RealPath}]");

                    foreach (IniKey key in folders.Select(x => new IniKey(ScriptSection.Names.EncodedFolders, x)))
                    {
                        if (!IniReadWriter.WriteRawLine(sc.RealPath, key))
                            return (sc, $"Unable to delete index of encoded folder [{folderName}] from [{sc.RealPath}]");
                    }
                }

                if (!sc.Sections.ContainsKey(folderName))
                {
                    errorMsg = $"Index of encoded folder [{folderName}] not found in [{sc.RealPath}]";
                }
                else
                {
                    Dictionary<string, string> fileDict = sc.Sections[folderName].IniDict;

                    // Get index of files
                    if (folderName.Equals(ScriptSection.Names.AuthorEncoded, StringComparison.OrdinalIgnoreCase))
                    {
                        if (fileDict.ContainsKey("Logo"))
                            fileDict.Remove("Logo");
                    }
                    var files = fileDict.Keys;

                    // Delete section [folderName]
                    if (!IniReadWriter.DeleteSection(sc.RealPath, folderName))
                        errorMsg = $"Encoded folder [{folderName}] not found in [{sc.RealPath}]";

                    // Delete encoded file section
                    foreach (string file in files)
                    {
                        if (!IniReadWriter.DeleteSection(sc.RealPath, ScriptSection.Names.GetEncodedSectionName(folderName, file)))
                            errorMsg = $"Encoded folder [{folderName}] not found in [{sc.RealPath}]";
                    }
                }
            }
            catch
            { // Error -> Rollback!
                File.Copy(backupFile, sc.RealPath, true);
                throw;
            }
            finally
            { // Delete backup script
                if (File.Exists(backupFile))
                    File.Delete(backupFile);
            }

            // Return refreshed script
            sc = sc.Project.RefreshScript(sc);
            return (sc, errorMsg);
        }

        public static Task<(Script, string)> DeleteLogoAsync(Script sc)
        {
            return Task.Run(() => DeleteLogo(sc));
        }

        public static (Script, string) DeleteLogo(Script sc)
        {
            if (sc == null)
                throw new ArgumentNullException(nameof(sc));

            string errorMsg = null;

            // Backup
            string backupFile = Path.GetTempFileName();
            File.Copy(sc.RealPath, backupFile, true);
            try
            {
                // Get encoded file index
                if (!sc.Sections.ContainsKey(ScriptSection.Names.AuthorEncoded))
                    return (sc, $"Logo not found in [{sc.RealPath}]");

                Dictionary<string, string> fileDict = sc.Sections[ScriptSection.Names.AuthorEncoded].IniDict;

                // Get filename of logo
                if (!fileDict.ContainsKey("Logo"))
                    return (sc, $"Logo not found in [{sc.RealPath}]");

                string logoFile = fileDict["Logo"];
                if (!fileDict.ContainsKey(logoFile))
                    return (sc, $"Logo not found in [{sc.RealPath}]");

                // Delete encoded file section
                if (!IniReadWriter.DeleteSection(sc.RealPath, ScriptSection.Names.GetEncodedSectionName(ScriptSection.Names.AuthorEncoded, logoFile)))
                    errorMsg = $"Encoded file [{logoFile}] not found in [{sc.RealPath}]";

                // Delete encoded file index
                if (!(IniReadWriter.DeleteKey(sc.RealPath, ScriptSection.Names.AuthorEncoded, logoFile) && IniReadWriter.DeleteKey(sc.RealPath, ScriptSection.Names.AuthorEncoded, "Logo")))
                    errorMsg = $"Unable to delete index of logo [{logoFile}] from [{sc.RealPath}]";
            }
            catch
            { // Error -> Rollback!
                File.Copy(backupFile, sc.RealPath, true);
                throw;
            }
            finally
            { // Delete backup script
                if (File.Exists(backupFile))
                    File.Delete(backupFile);
            }

            // Return refreshed script
            sc = sc.Project.RefreshScript(sc);
            return (sc, errorMsg);
        }
        #endregion

        #region Encode
        private static Script Encode(Script sc, string folderName, string fileName, byte[] input, EncodeMode mode, bool encodeLogo, IProgress<double> progress)
        {
            using (MemoryStream ms = Global.MemoryStreamManager.GetStream("EncodedFile.Encode", input, 0, input.Length))
            {
                return Encode(sc, folderName, fileName, ms, mode, encodeLogo, progress);
            }
        }

        private static Script Encode(Script sc, string folderName, string fileName, Stream inputStream, EncodeMode mode, bool encodeLogo, IProgress<double> progress)
        {
            // Check filename
            byte[] fileNameUtf8 = Encoding.UTF8.GetBytes(fileName);
            if (fileNameUtf8.Length == 0 || 512 <= fileNameUtf8.Length)
                throw new InvalidOperationException("UTF8 encoded filename should be shorter than 512B");
            string section = ScriptSection.Names.GetEncodedSectionName(folderName, fileName);

            // Check Overwrite
            bool fileOverwrite = false;
            if (sc.Sections.ContainsKey(folderName))
            {
                // Check if [{folderName}] section and [EncodedFile-{folderName}-{fileName}] section exists
                ScriptSection scSect = sc.Sections[folderName];
                if (scSect.IniDict.ContainsKey(fileName) && sc.Sections.ContainsKey(section))
                    fileOverwrite = true;
            }

            string tempCompressed = Path.GetTempFileName();
            string tempEncoded = Path.GetTempFileName();
            try
            {
                int encodedLen;
                using (FileStream encodeStream = new FileStream(tempCompressed, FileMode.Create, FileAccess.ReadWrite))
                {
                    // [Stage 1] Compress file with zlib
                    int bytesRead;
                    long offset = 0;
                    long inputLen = inputStream.Length;
                    byte[] buffer = new byte[BufferSize];
                    Crc32Checksum crc32 = new Crc32Checksum();
                    switch (mode)
                    {
                        case EncodeMode.ZLib:
                            using (ZLibStream zs = new ZLibStream(encodeStream, ZLibMode.Compress, ZLibCompLevel.Level6, true))
                            {
                                while ((bytesRead = inputStream.Read(buffer, 0, buffer.Length)) != 0)
                                {
                                    crc32.Append(buffer, 0, bytesRead);
                                    zs.Write(buffer, 0, bytesRead);

                                    offset += bytesRead;
                                    if (offset % ReportInterval == 0)
                                        progress?.Report((double)offset / inputLen * CompReportFactor);
                                }
                            }
                            break;
                        case EncodeMode.Raw:
                            while ((bytesRead = inputStream.Read(buffer, 0, buffer.Length)) != 0)
                            {
                                crc32.Append(buffer, 0, bytesRead);
                                encodeStream.Write(buffer, 0, bytesRead);

                                offset += bytesRead;
                                if (offset % ReportInterval == 0)
                                    progress?.Report((double)offset / inputLen * CompReportFactor);
                            }
                            break;
                        case EncodeMode.XZ:
                            // Multi-threading will take up too much memory, use single thread instead.
                            using (XZStream xzs = new XZStream(encodeStream, LzmaMode.Compress, XZStream.DefaultPreset, true))
                            {
                                while ((bytesRead = inputStream.Read(buffer, 0, buffer.Length)) != 0)
                                {
                                    crc32.Append(buffer, 0, bytesRead);
                                    xzs.Write(buffer, 0, bytesRead);

                                    offset += bytesRead;
                                    if (offset % ReportInterval == 0)
                                        progress?.Report((double)offset / inputLen * CompReportFactor);
                                }
                            }
                            break;
                        default:
                            throw new InternalException($"Wrong EncodeMode [{mode}]");
                    }
                    long compressedBodyLen = encodeStream.Position;
                    progress?.Report(0.8);

                    // [Stage 2] Generate first footer
                    byte[] rawFooter = new byte[0x226]; // 0x550
                    {
                        // 0x000 - 0x1FF : Filename and its length
                        rawFooter[0] = (byte)fileNameUtf8.Length;
                        fileNameUtf8.CopyTo(rawFooter, 1);
                        for (int i = 1 + fileNameUtf8.Length; i < 0x200; i++)
                            rawFooter[i] = 0; // Null Pad
                        // 0x200 - 0x207 : 8B -> Length of raw file, in little endian
                        BitConverter.GetBytes(inputLen).CopyTo(rawFooter, 0x200);
                        switch (mode)
                        {
                            case EncodeMode.ZLib: // Type 1
                            case EncodeMode.XZ: // Type 3
                                // 0x208 - 0x20F : 8B -> Length of compressed body, in little endian
                                BitConverter.GetBytes(compressedBodyLen).CopyTo(rawFooter, 0x208);
                                // 0x210 - 0x21F : 16B -> Null padding
                                for (int i = 0x210; i < 0x220; i++)
                                    rawFooter[i] = 0;
                                break;
                            case EncodeMode.Raw: // Type 2
                                // 0x208 - 0x21F : 16B -> Null padding
                                for (int i = 0x208; i < 0x220; i++)
                                    rawFooter[i] = 0;
                                break;
                            default:
                                throw new InternalException($"Wrong EncodeMode [{mode}]");
                        }
                        // 0x220 - 0x223 : CRC32 of raw file
                        BitConverter.GetBytes(crc32.Checksum).CopyTo(rawFooter, 0x220);
                        // 0x224         : 1B -> Compress Mode (Type 1 : 00, Type 2 : 01)
                        rawFooter[0x224] = (byte)mode;
                        // 0x225         : 1B -> ZLib Compress Level (Type 1 : 01 ~ 09, Type 2 : 00)
                        switch (mode)
                        {
                            case EncodeMode.ZLib: // Type 1
                                rawFooter[0x225] = (byte)ZLibCompLevel.Level6;
                                break;
                            case EncodeMode.Raw: // Type 2
                                rawFooter[0x225] = 0;
                                break;
                            case EncodeMode.XZ: // Type 3
                                rawFooter[0x225] = (byte)XZStream.DefaultPreset;
                                break;
                            default:
                                throw new InternalException($"Wrong EncodeMode [{mode}]");
                        }
                    }

                    // [Stage 3] Compress first footer and concat to body
                    long compressedFooterLen = encodeStream.Position;
                    using (ZLibStream zs = new ZLibStream(encodeStream, ZLibMode.Compress, ZLibCompLevel.Level6, true))
                    {
                        zs.Write(rawFooter, 0, rawFooter.Length);
                    }
                    encodeStream.Flush();
                    compressedFooterLen = encodeStream.Position - compressedFooterLen;

                    // [Stage 4] Generate final footer
                    {
                        byte[] finalFooter = new byte[0x24];

                        // 0x00 - 0x04 : 4B -> CRC32 of compressed body and compressed footer
                        BitConverter.GetBytes(CalcCrc32(encodeStream)).CopyTo(finalFooter, 0x00);
                        // 0x04 - 0x08 : 4B -> Unknown - Always 1
                        BitConverter.GetBytes((uint)1).CopyTo(finalFooter, 0x04);
                        // 0x08 - 0x0B : 4B -> Delphi ZLBArchive Component version (Always 2)
                        BitConverter.GetBytes((uint)2).CopyTo(finalFooter, 0x08);
                        // 0x0C - 0x0F : 4B -> Zlib Compressed Footer Length
                        BitConverter.GetBytes((int)compressedFooterLen).CopyTo(finalFooter, 0x0C);
                        // 0x10 - 0x17 : 8B -> Compressed/Raw File Length
                        BitConverter.GetBytes(compressedBodyLen).CopyTo(finalFooter, 0x10);
                        // 0x18 - 0x1B : 4B -> Unknown - Always 1
                        BitConverter.GetBytes((uint)1).CopyTo(finalFooter, 0x18);
                        // 0x1C - 0x23 : 8B -> Unknown - Always 0
                        for (int i = 0x1C; i < 0x24; i++)
                            finalFooter[i] = 0;

                        encodeStream.Write(finalFooter, 0, finalFooter.Length);
                    }

                    // [Stage 5] Encode with Base64 and split into 4090B
                    encodeStream.Flush();
                    using (StreamWriter tw = new StreamWriter(tempEncoded, false, Encoding.UTF8))
                    {
                        // compressedBodyLen
                        encodedLen = SplitBase64.Encode(encodeStream, tw, new Progress<long>(x =>
                        {
                            progress?.Report((double)x / compressedBodyLen * Base64ReportFactor + CompReportFactor);
                        }));
                    }
                    progress?.Report(1);
                }

                // [Stage 6] Before writing to file, backup original script
                string backupFile = Path.GetTempFileName();
                File.Copy(sc.RealPath, backupFile, true);

                // [Stage 7] Write to file
                try
                {
                    // Write folder info to [EncodedFolders]
                    if (!encodeLogo)
                    { // "AuthorEncoded" and "InterfaceEncoded" should not be listed here
                        bool writeFolderSection = true;
                        if (sc.Sections.ContainsKey(ScriptSection.Names.EncodedFolders))
                        {
                            string[] folders = sc.Sections[ScriptSection.Names.EncodedFolders].Lines;
                            if (0 < folders.Count(x => x.Equals(folderName, StringComparison.OrdinalIgnoreCase)))
                                writeFolderSection = false;
                        }

                        if (writeFolderSection &&
                            !folderName.Equals(ScriptSection.Names.AuthorEncoded, StringComparison.OrdinalIgnoreCase) &&
                            !folderName.Equals(ScriptSection.Names.InterfaceEncoded, StringComparison.OrdinalIgnoreCase))
                            IniReadWriter.WriteRawLine(sc.RealPath, ScriptSection.Names.EncodedFolders, folderName, false);
                    }

                    // Write file info into [{folderName}]
                    IniReadWriter.WriteKey(sc.RealPath, folderName, fileName, $"{inputStream.Length},{encodedLen}"); // UncompressedSize,EncodedSize

                    // Write encoded file into [EncodedFile-{folderName}-{fileName}]
                    if (fileOverwrite)
                        IniReadWriter.DeleteSection(sc.RealPath, section); // Delete existing encoded file
                    using (StreamReader tr = new StreamReader(tempEncoded, Encoding.UTF8, false))
                    {
                        IniReadWriter.WriteSectionFast(sc.RealPath, section, tr);
                    }

                    // Write additional line when encoding logo.
                    if (encodeLogo)
                    {
                        string lastLogo = IniReadWriter.ReadKey(sc.RealPath, ScriptSection.Names.AuthorEncoded, "Logo");
                        IniReadWriter.WriteKey(sc.RealPath, ScriptSection.Names.AuthorEncoded, "Logo", fileName);

                        if (lastLogo != null)
                        {
                            IniReadWriter.DeleteKey(sc.RealPath, ScriptSection.Names.AuthorEncoded, lastLogo);
                            IniReadWriter.DeleteSection(sc.RealPath, ScriptSection.Names.GetEncodedSectionName(ScriptSection.Names.AuthorEncoded, lastLogo));
                        }
                    }
                }
                catch
                { // Error -> Rollback!
                    File.Copy(backupFile, sc.RealPath, true);
                    throw new InvalidOperationException($"Error while writing encoded file into [{sc.RealPath}]");
                }
                finally
                { // Delete backup script
                    if (File.Exists(backupFile))
                        File.Delete(backupFile);
                }
            }
            finally
            {
                if (File.Exists(tempCompressed))
                    File.Delete(tempCompressed);
                if (File.Exists(tempEncoded))
                    File.Delete(tempEncoded);
            }

            // [Stage 8] Refresh Script
            return sc.Project.RefreshScript(sc);
        }
        #endregion

        #region Decode
        private static long Decode(string scPath, string section, Stream outStream, IProgress<double> progress)
        {
            string tempDecode = Path.GetTempFileName();
            string tempComp = Path.GetTempFileName();
            try
            {
                using (FileStream decodeStream = new FileStream(tempDecode, FileMode.Create, FileAccess.ReadWrite))
                {
                    // [Stage 1] Concat sliced base64-encoded lines into one string
                    int decodeLen;
                    Encoding encoding = FileHelper.DetectTextEncoding(scPath);
                    using (StreamReader tr = new StreamReader(scPath, encoding))
                    {
                        decodeLen = SplitBase64.Decode(tr, section, decodeStream, new Progress<(int Pos, int Total)>(x =>
                        {
                            progress?.Report((double)x.Pos / x.Total * Base64ReportFactor);
                        }));
                    }
                    progress?.Report(Base64ReportFactor);

                    // [Stage 2] Read final footer
                    const int finalFooterLen = 0x24;
                    byte[] finalFooter = new byte[finalFooterLen];
                    int finalFooterIdx = decodeLen - finalFooterLen;

                    decodeStream.Flush();
                    decodeStream.Position = finalFooterIdx;
                    int bytesRead = decodeStream.Read(finalFooter, 0, finalFooterLen);
                    Debug.Assert(bytesRead == finalFooterLen);

                    // 0x00 - 0x04 : 4B -> CRC32
                    uint fullCrc32 = BitConverter.ToUInt32(finalFooter, 0x00);
                    // 0x0C - 0x0F : 4B -> Zlib Compressed Footer Length
                    int compressedFooterLen = (int)BitConverter.ToUInt32(finalFooter, 0x0C);
                    int compressedFooterIdx = finalFooterIdx - compressedFooterLen;
                    // 0x10 - 0x17 : 8B -> Zlib Compressed File Length
                    int compressedBodyLen = (int)BitConverter.ToUInt64(finalFooter, 0x10);

                    // [Stage 3] Validate final footer
                    if (compressedBodyLen != compressedFooterIdx)
                        throw new InvalidOperationException("Encoded file is corrupted: finalFooter");
                    if (fullCrc32 != CalcCrc32(decodeStream, 0, finalFooterIdx))
                        throw new InvalidOperationException("Encoded file is corrupted: finalFooter");

                    // [Stage 4] Decompress first footer
                    byte[] firstFooter = new byte[0x226];
                    using (MemoryStream compressedFooter = Global.MemoryStreamManager.GetStream("EncodedFile.Decode.Stage4", compressedFooterLen))
                    {
                        decodeStream.Position = compressedFooterIdx;
                        decodeStream.CopyTo(compressedFooter, compressedFooterLen);
                        decodeStream.Position = 0;

                        compressedFooter.Flush();
                        compressedFooter.Position = 0;
                        using (ZLibStream zs = new ZLibStream(compressedFooter, ZLibMode.Decompress))
                        {
                            bytesRead = zs.Read(firstFooter, 0, firstFooter.Length);
                            Debug.Assert(bytesRead == firstFooter.Length);
                        }
                    }

                    // [Stage 5] Read first footer
                    // 0x200 - 0x207 : 8B -> Length of raw file, in little endian
                    int rawBodyLen = BitConverter.ToInt32(firstFooter, 0x200);
                    // 0x208 - 0x20F : 8B -> Length of zlib-compressed file, in little endian
                    //     Note: In Type 2, 0x208 entry is null - padded
                    int compressedBodyLen2 = BitConverter.ToInt32(firstFooter, 0x208);
                    // 0x220 - 0x223 : 4B -> CRC32C Checksum of zlib-compressed file
                    uint compressedBodyCrc32 = BitConverter.ToUInt32(firstFooter, 0x220);
                    // 0x224         : 1B -> Compress Mode (Type 1 : 00, Type 2 : 01)
                    byte compMode = firstFooter[0x224];
                    // 0x225         : 1B -> ZLib Compress Level (Type 1 : 01~09, Type 2 : 00)
                    byte compLevel = firstFooter[0x225];

                    // [Stage 6] Validate first footer
                    switch ((EncodeMode)compMode)
                    {
                        case EncodeMode.ZLib: // Type 1, zlib
                            if (compressedBodyLen2 == 0 ||
                                compressedBodyLen2 != compressedBodyLen)
                                throw new InvalidOperationException("Encoded file is corrupted: compMode");
                            if (compLevel < 1 || 9 < compLevel)
                                throw new InvalidOperationException("Encoded file is corrupted: compLevel");
                            break;
                        case EncodeMode.Raw: // Type 2, raw
                            if (compressedBodyLen2 != 0)
                                throw new InvalidOperationException("Encoded file is corrupted: compMode");
                            if (compLevel != 0)
                                throw new InvalidOperationException("Encoded file is corrupted: compLevel");
                            break;
                        case EncodeMode.XZ: // Type 3, LZMA
                            if (compressedBodyLen2 == 0 ||
                                compressedBodyLen2 != compressedBodyLen)
                                throw new InvalidOperationException("Encoded file is corrupted: compMode");
                            if (compLevel < 1 || 9 < compLevel)
                                throw new InvalidOperationException("Encoded file is corrupted: compLevel");
                            break;
                        default:
                            throw new InvalidOperationException("Encoded file is corrupted: compMode");
                    }

                    // [Stage 7] Decompress body
                    Crc32Checksum crc32 = new Crc32Checksum();
                    long outPosBak = outStream.Position;
                    byte[] buffer = new byte[BufferSize]; // 64KB
                    switch ((EncodeMode)compMode)
                    {
                        case EncodeMode.ZLib: // Type 1, zlib
                            using (FileStream compStream = new FileStream(tempComp, FileMode.Create, FileAccess.ReadWrite))
                            {
                                StreamSubCopy(decodeStream, compStream, 0, compressedBodyLen);

#if DEBUG_MIDDLE_FILE
                                compStream.Flush();
                                compStream.Position = 0;
                                string debugDir = Path.Combine(App.BaseDir, "Debug");
                                Directory.CreateDirectory(debugDir);
                                string debugFile = Path.Combine(debugDir, Path.GetFileName(Path.GetRandomFileName()) + ".zz");
                                using (FileStream debug = new FileStream(debugFile, FileMode.Create, FileAccess.Write))
                                {
                                    compStream.CopyTo(debug);
                                }
#endif

                                compStream.Flush();
                                compStream.Position = 0;

                                int offset = 0;
                                using (ZLibStream zs = new ZLibStream(compStream, ZLibMode.Decompress, true))
                                {
                                    while ((bytesRead = zs.Read(buffer, 0, buffer.Length)) != 0)
                                    {
                                        crc32.Append(buffer, 0, bytesRead);
                                        outStream.Write(buffer, 0, bytesRead);

                                        offset += bytesRead;
                                        if (offset % ReportInterval == 0)
                                            progress?.Report((double)offset / rawBodyLen * CompReportFactor + Base64ReportFactor);
                                    }
                                }
                            }
                            break;
                        case EncodeMode.Raw: // Type 2, raw
                            {
                                decodeStream.Flush();
                                decodeStream.Position = 0;

#if DEBUG_MIDDLE_FILE
                                string debugDir = Path.Combine(App.BaseDir, "Debug");
                                Directory.CreateDirectory(debugDir);
                                string debugFile = Path.Combine(debugDir, Path.GetFileName(Path.GetRandomFileName()) + ".bin");
                                FileStream debug = new FileStream(debugFile, FileMode.Create, FileAccess.Write);
#endif

                                int offset = 0;
                                while (offset < rawBodyLen)
                                {
                                    if (offset + buffer.Length < rawBodyLen)
                                        bytesRead = decodeStream.Read(buffer, 0, buffer.Length);
                                    else
                                        bytesRead = decodeStream.Read(buffer, 0, rawBodyLen - offset);

                                    crc32.Append(buffer, 0, bytesRead);
                                    outStream.Write(buffer, 0, bytesRead);

#if DEBUG_MIDDLE_FILE
                                    debug.Write(buffer, 0, readByte);
#endif

                                    offset += bytesRead;
                                    if (offset % ReportInterval == 0)
                                        progress?.Report((double)offset / rawBodyLen * CompReportFactor + Base64ReportFactor);
                                }

#if DEBUG_MIDDLE_FILE
                                debug.Close();
#endif
                            }
                            break;
                        case EncodeMode.XZ: // Type 3, LZMA
                            using (FileStream compStream = new FileStream(tempComp, FileMode.Create, FileAccess.ReadWrite))
                            {
                                StreamSubCopy(decodeStream, compStream, 0, compressedBodyLen);

#if DEBUG_MIDDLE_FILE
                                compStream.Flush();
                                compStream.Position = 0;
                                string debugDir = Path.Combine(App.BaseDir, "Debug");
                                Directory.CreateDirectory(debugDir);
                                string debugFile = Path.Combine(debugDir, Path.GetFileName(Path.GetRandomFileName()) + ".xz");
                                using (FileStream debug = new FileStream(debugFile, FileMode.Create, FileAccess.Write))
                                {
                                    compStream.CopyTo(debug);
                                }
#endif

                                compStream.Flush();
                                compStream.Position = 0;

                                int offset = 0;
                                using (XZStream xzs = new XZStream(compStream, LzmaMode.Decompress))
                                {
                                    while ((bytesRead = xzs.Read(buffer, 0, buffer.Length)) != 0)
                                    {
                                        crc32.Append(buffer, 0, bytesRead);
                                        outStream.Write(buffer, 0, bytesRead);

                                        offset += bytesRead;
                                        if (offset % ReportInterval == 0)
                                            progress?.Report((double)offset / rawBodyLen * CompReportFactor + Base64ReportFactor);
                                    }
                                }
                            }
                            break;
                        default:
                            throw new InvalidOperationException("Encoded file is corrupted: compMode");
                    }
                    long outLen = outStream.Position - outPosBak;

                    // [Stage 8] Validate decompressed body
                    if (compressedBodyCrc32 != crc32.Checksum)
                        throw new InvalidOperationException("Encoded file is corrupted: body");
                    progress?.Report(1);

                    return outLen;
                }
            }
            finally
            {
                if (!File.Exists(tempDecode))
                    File.Delete(tempDecode);
                if (!File.Exists(tempComp))
                    File.Delete(tempComp);
            }
        }
        #endregion

        #region DecodeInMem
        private static MemoryStream DecodeInMem(string[] encodedLines)
        {
            // [Stage 1] Concat sliced base64-encoded lines into one string
            byte[] decoded = SplitBase64.DecodeInMem(IniReadWriter.FilterInvalidIniLines(encodedLines));

            // [Stage 2] Read final footer
            const int finalFooterLen = 0x24;
            int finalFooterIdx = decoded.Length - finalFooterLen;
            // 0x00 - 0x04 : 4B -> CRC32
            uint fullCrc32 = BitConverter.ToUInt32(decoded, finalFooterIdx + 0x00);
            // 0x0C - 0x0F : 4B -> Zlib Compressed Footer Length
            int compressedFooterLen = (int)BitConverter.ToUInt32(decoded, finalFooterIdx + 0x0C);
            int compressedFooterIdx = decoded.Length - (finalFooterLen + compressedFooterLen);
            // 0x10 - 0x17 : 8B -> Zlib Compressed File Length
            int compressedBodyLen = (int)BitConverter.ToUInt64(decoded, finalFooterIdx + 0x10);

            // [Stage 3] Validate final footer
            if (compressedBodyLen != compressedFooterIdx)
                throw new InvalidOperationException("Encoded file is corrupted: finalFooter");
            uint calcFullCrc32 = Crc32Checksum.Crc32(decoded, 0, finalFooterIdx);
            if (fullCrc32 != calcFullCrc32)
                throw new InvalidOperationException("Encoded file is corrupted: finalFooter");

            // [Stage 4] Decompress first footer
            byte[] rawFooter;
            using (MemoryStream rawFooterStream = Global.MemoryStreamManager.GetStream("EncodedFile.DecodeInMem.Stage4"))
            {
                using (MemoryStream ms = new MemoryStream(decoded, compressedFooterIdx, compressedFooterLen))
                using (ZLibStream zs = new ZLibStream(ms, ZLibMode.Decompress))
                {
                    zs.CopyTo(rawFooterStream);
                }

                rawFooter = rawFooterStream.ToArray();
            }

            // [Stage 5] Read first footer
            // 0x200 - 0x207 : 8B -> Length of raw file, in little endian
            int rawBodyLen = BitConverter.ToInt32(rawFooter, 0x200);
            // 0x208 - 0x20F : 8B -> Length of zlib-compressed file, in little endian
            //     Note: In Type 2, 0x208 entry is null - padded
            int compressedBodyLen2 = BitConverter.ToInt32(rawFooter, 0x208);
            // 0x220 - 0x223 : 4B -> CRC32C Checksum of zlib-compressed file
            uint compressedBodyCrc32 = BitConverter.ToUInt32(rawFooter, 0x220);
            // 0x224         : 1B -> Compress Mode (Type 1 : 00, Type 2 : 01)
            byte compMode = rawFooter[0x224];
            // 0x225         : 1B -> ZLib Compress Level (Type 1 : 01~09, Type 2 : 00)
            byte compLevel = rawFooter[0x225];

            // [Stage 6] Validate first footer
            switch ((EncodeMode)compMode)
            {
                case EncodeMode.ZLib: // Type 1, zlib
                    if (compressedBodyLen2 == 0 ||
                        compressedBodyLen2 != compressedBodyLen)
                        throw new InvalidOperationException("Encoded file is corrupted: compMode");
                    if (compLevel < 1 || 9 < compLevel)
                        throw new InvalidOperationException("Encoded file is corrupted: compLevel");
                    break;
                case EncodeMode.Raw: // Type 2, raw
                    if (compressedBodyLen2 != 0)
                        throw new InvalidOperationException("Encoded file is corrupted: compMode");
                    if (compLevel != 0)
                        throw new InvalidOperationException("Encoded file is corrupted: compLevel");
                    break;
                case EncodeMode.XZ: // Type 3, LZMA
                    if (compressedBodyLen2 == 0 ||
                        compressedBodyLen2 != compressedBodyLen)
                        throw new InvalidOperationException("Encoded file is corrupted: compMode");
                    if (9 < compLevel)
                        throw new InvalidOperationException("Encoded file is corrupted: compLevel");
                    break;
                default:
                    throw new InvalidOperationException("Encoded file is corrupted: compMode");
            }

            // [Stage 7] Decompress body
            MemoryStream rawBodyStream = Global.MemoryStreamManager.GetStream("EncodedFile.DecodeInMem.Stage7"); // This stream should be alive even after this method returns
            switch ((EncodeMode)compMode)
            {
                case EncodeMode.ZLib: // Type 1, zlib
                    {
#if DEBUG_MIDDLE_FILE
                        string debugDir = Path.Combine(App.BaseDir, "Debug");
                        Directory.CreateDirectory(debugDir);
                        string debugFile = Path.Combine(debugDir, Path.GetFileName(Path.GetRandomFileName()) + ".zz");
                        using (MemoryStream ms = new MemoryStream(decoded, 0, compressedBodyLen))
                        using (FileStream debug = new FileStream(debugFile, FileMode.Create, FileAccess.Write))
                        {
                            ms.CopyTo(debug);
                        }
#endif
                        using (MemoryStream ms = Global.MemoryStreamManager.GetStream("EncodedFile.DecodeInMem.Stage7.ZLib", decoded, 0, compressedBodyLen))
                        using (ZLibStream zs = new ZLibStream(ms, ZLibMode.Decompress))
                        {
                            zs.CopyTo(rawBodyStream);
                        }
                    }
                    break;
                case EncodeMode.Raw: // Type 2, raw
                    {
                        rawBodyStream.Write(decoded, 0, rawBodyLen);
#if DEBUG_MIDDLE_FILE
                        string debugDir = Path.Combine(App.BaseDir, "Debug");
                        Directory.CreateDirectory(debugDir);
                        string debugFile = Path.Combine(debugDir, Path.GetFileName(Path.GetRandomFileName()) + ".bin");
                        using (FileStream debug = new FileStream(debugFile, FileMode.Create, FileAccess.Write))
                        {
                            debug.Write(decoded, 0, rawBodyLen);
                        }
#endif
                    }
                    break;
                case EncodeMode.XZ: // Type 3, LZMA
                    {
#if DEBUG_MIDDLE_FILE
                        string debugDir = Path.Combine(App.BaseDir, "Debug");
                        Directory.CreateDirectory(debugDir);
                        string debugFile = Path.Combine(debugDir, Path.GetFileName(Path.GetRandomFileName()) + ".xz");
                        using (MemoryStream ms = new MemoryStream(decoded, 0, compressedBodyLen))
                        using (FileStream debug = new FileStream(debugFile, FileMode.Create, FileAccess.Write))
                        {
                            ms.CopyTo(debug);
                        }
#endif
                        using (MemoryStream ms = Global.MemoryStreamManager.GetStream("EncodedFile.DecodeInMem.Stage7.XZ", decoded, 0, compressedBodyLen))
                        using (XZStream xzs = new XZStream(ms, LzmaMode.Decompress))
                        {
                            xzs.CopyTo(rawBodyStream);
                        }
                    }
                    break;
                default:
                    throw new InvalidOperationException("Encoded file is corrupted: compMode");
            }

            rawBodyStream.Position = 0;

            // [Stage 8] Validate decompressed body
            uint calcCompBodyCrc32 = Crc32Checksum.Crc32(rawBodyStream.ToArray());
            if (compressedBodyCrc32 != calcCompBodyCrc32)
                throw new InvalidOperationException("Encoded file is corrupted: body");

            // [Stage 9] Return decompressed body stream
            rawBodyStream.Position = 0;
            return rawBodyStream;
        }
        #endregion

        #region GetEncodeMode
        private static EncodeMode GetEncodeMode(string scPath, string section)
        {
            string tempDecode = Path.GetTempFileName();
            try
            {
                using (FileStream decodeStream = new FileStream(tempDecode, FileMode.Create, FileAccess.ReadWrite))
                {
                    // [Stage 1] Concat sliced base64-encoded lines into one string
                    int decodeLen;
                    Encoding encoding = FileHelper.DetectTextEncoding(scPath);
                    using (StreamReader tr = new StreamReader(scPath, encoding))
                    {
                        decodeLen = SplitBase64.Decode(tr, section, decodeStream);
                    }

                    // [Stage 2] Read final footer
                    const int finalFooterLen = 0x24;
                    byte[] finalFooter = new byte[finalFooterLen];
                    int finalFooterIdx = decodeLen - finalFooterLen;

                    decodeStream.Flush();
                    decodeStream.Position = finalFooterIdx;
                    int readByte = decodeStream.Read(finalFooter, 0, finalFooterLen);
                    Debug.Assert(readByte == finalFooterLen);

                    // 0x00 - 0x04 : 4B -> CRC32
                    uint full_crc32 = BitConverter.ToUInt32(finalFooter, 0x00);
                    // 0x0C - 0x0F : 4B -> Zlib Compressed Footer Length
                    int compressedFooterLen = (int)BitConverter.ToUInt32(finalFooter, 0x0C);
                    int compressedFooterIdx = finalFooterIdx - compressedFooterLen;
                    // 0x10 - 0x17 : 8B -> Zlib Compressed File Length
                    int compressedBodyLen = (int)BitConverter.ToUInt64(finalFooter, 0x10);

                    // [Stage 3] Validate final footer
                    if (compressedBodyLen != compressedFooterIdx)
                        throw new InvalidOperationException("Encoded file is corrupted: finalFooter");
                    if (full_crc32 != CalcCrc32(decodeStream, 0, finalFooterIdx))
                        throw new InvalidOperationException("Encoded file is corrupted: finalFooter");

                    // [Stage 4] Decompress first footer
                    byte[] firstFooter = new byte[0x226];
                    using (MemoryStream compressedFooter = Global.MemoryStreamManager.GetStream("EncodedFile.GetEncodeMode.Stage4", compressedFooterLen))
                    {
                        decodeStream.Position = compressedFooterIdx;
                        decodeStream.CopyTo(compressedFooter, compressedFooterLen);
                        decodeStream.Position = 0;

                        compressedFooter.Flush();
                        compressedFooter.Position = 0;
                        using (ZLibStream zs = new ZLibStream(compressedFooter, ZLibMode.Decompress))
                        {
                            readByte = zs.Read(firstFooter, 0, firstFooter.Length);
                            Debug.Assert(readByte == firstFooter.Length);
                        }
                    }

                    // [Stage 5] Read first footer
                    // 0x224         : 1B -> Compress Mode (Type 1 : 00, Type 2 : 01)
                    byte compMode = firstFooter[0x224];
                    // 0x225         : 1B -> ZLib Compress Level (Type 1 : 01~09, Type 2 : 00)
                    byte compLevel = firstFooter[0x225];

                    // [Stage 6] Validate first footer
                    switch ((EncodeMode)compMode)
                    {
                        case EncodeMode.ZLib: // Type 1, zlib
                            if (compLevel < 1 || 9 < compLevel)
                                throw new InvalidOperationException("Encoded file is corrupted: compLevel");
                            break;
                        case EncodeMode.Raw: // Type 2, raw
                            if (compLevel != 0)
                                throw new InvalidOperationException("Encoded file is corrupted: compLevel");
                            break;
                        case EncodeMode.XZ: // Type 3, LZMA
                            if (9 < compLevel)
                                throw new InvalidOperationException("Encoded file is corrupted: compLevel");
                            break;
                        default:
                            throw new InvalidOperationException("Encoded file is corrupted: compMode");
                    }

                    return (EncodeMode)compMode;
                }
            }
            finally
            {
                if (!File.Exists(tempDecode))
                    File.Delete(tempDecode);
            }
        }
        #endregion

        #region GetEncodeModeInMem
        private static EncodeMode GetEncodeModeInMem(string[] encodedLines)
        {
            // [Stage 1] Concat sliced base64-encoded lines into one string
            byte[] decoded = SplitBase64.DecodeInMem(IniReadWriter.FilterInvalidIniLines(encodedLines));

            // [Stage 2] Read final footer
            const int finalFooterLen = 0x24;
            int finalFooterIdx = decoded.Length - finalFooterLen;
            // 0x00 - 0x04 : 4B -> CRC32
            uint fullCrc32 = BitConverter.ToUInt32(decoded, finalFooterIdx + 0x00);
            // 0x0C - 0x0F : 4B -> Zlib Compressed Footer Length
            int compressedFooterLen = (int)BitConverter.ToUInt32(decoded, finalFooterIdx + 0x0C);
            int compressedFooterIdx = decoded.Length - (finalFooterLen + compressedFooterLen);
            // 0x10 - 0x17 : 8B -> Zlib Compressed File Length
            int compressedBodyLen = (int)BitConverter.ToUInt64(decoded, finalFooterIdx + 0x10);

            // [Stage 3] Validate final footer
            if (compressedBodyLen != compressedFooterIdx)
                throw new InvalidOperationException("Encoded file is corrupted: finalFooter");
            uint calcFullCrc32 = Crc32Checksum.Crc32(decoded, 0, finalFooterIdx);
            if (fullCrc32 != calcFullCrc32)
                throw new InvalidOperationException("Encoded file is corrupted: finalFooter");

            // [Stage 4] Decompress first footer
            byte[] rawFooter;
            using (MemoryStream rawFooterStream = Global.MemoryStreamManager.GetStream("EncodedFile.GetEncodeMode.Stage4"))
            {
                using (MemoryStream ms = Global.MemoryStreamManager.GetStream("EncodedFile.GetEncodeMode.Stage4.ZLib", decoded, compressedFooterIdx, compressedFooterLen))
                using (ZLibStream zs = new ZLibStream(ms, ZLibMode.Decompress))
                {
                    zs.CopyTo(rawFooterStream);
                }

                rawFooter = rawFooterStream.ToArray();
            }

            // [Stage 5] Read first footer
            // 0x224         : 1B -> Compress Mode (Type 1 : 00, Type 2 : 01)
            byte compMode = rawFooter[0x224];
            // 0x225         : 1B -> ZLib Compress Level (Type 1 : 01~09, Type 2 : 00)
            byte compLevel = rawFooter[0x225];

            // [Stage 6] Validate first footer
            switch ((EncodeMode)compMode)
            {
                case EncodeMode.ZLib: // Type 1, zlib
                    if (compLevel < 1 || 9 < compLevel)
                        throw new InvalidOperationException("Encoded file is corrupted: compLevel");
                    break;
                case EncodeMode.Raw: // Type 2, raw
                    if (compLevel != 0)
                        throw new InvalidOperationException("Encoded file is corrupted: compLevel");
                    break;
                case EncodeMode.XZ: // Type 3, LZMA
                    if (9 < compLevel)
                        throw new InvalidOperationException("Encoded file is corrupted: compLevel");
                    break;
                default:
                    throw new InvalidOperationException("Encoded file is corrupted: compMode");
            }

            return (EncodeMode)compMode;
        }
        #endregion

        #region Utility
        private static uint CalcCrc32(Stream stream)
        {
            long posBak = stream.Position;
            stream.Position = 0;

            Crc32Checksum calc = new Crc32Checksum();
            byte[] buffer = new byte[4096 * 1024]; // 4MB
            while (stream.Position < stream.Length)
            {
                int readByte = stream.Read(buffer, 0, buffer.Length);
                calc.Append(buffer, 0, readByte);
            }

            stream.Position = posBak;
            return calc.Checksum;
        }

        private static uint CalcCrc32(Stream stream, int startOffset, int length)
        {
            if (stream.Length <= startOffset)
                throw new ArgumentOutOfRangeException(nameof(startOffset));
            if (stream.Length <= startOffset + length)
                throw new ArgumentOutOfRangeException(nameof(length));

            long posBak = stream.Position;
            stream.Position = startOffset;

            int offset = startOffset;
            Crc32Checksum calc = new Crc32Checksum();
            byte[] buffer = new byte[4096 * 1024]; // 4MB
            while (offset < startOffset + length)
            {
                int readByte = stream.Read(buffer, 0, buffer.Length);
                if (offset + readByte < startOffset + length)
                    calc.Append(buffer, 0, readByte);
                else
                    calc.Append(buffer, 0, startOffset + length - offset);
                offset += readByte;
            }

            stream.Position = posBak;
            return calc.Checksum;
        }

        private static void StreamSubCopy(Stream srcStream, Stream destStream, long startOffset, long endOffset)
        {
            if (int.MaxValue < endOffset - startOffset)
                throw new ArgumentException("Copy length should be less than 2GB");

            byte[] buffer = new byte[4096 * 1024];

            srcStream.Position = startOffset;
            long offset = startOffset;
            while (offset < endOffset)
            {
                int readCount;
                if (offset + buffer.Length < endOffset)
                    readCount = buffer.Length;
                else
                    readCount = (int)(endOffset - offset);

                int readByte = srcStream.Read(buffer, 0, readCount);
                destStream.Write(buffer, 0, readByte);

                offset += readByte;
            }
        }
        #endregion
    }
    #endregion

    #region SplitBase64
    public static class SplitBase64
    {
        #region Encode
        public static int Encode(Stream srcStream, TextWriter writer, IProgress<long> progress = null)
        {
            const int reportInterval = 1024 * 1024; // 1MB

            int idx = 0;
            int encodedLen = 0;
            int lineCount = (int)(srcStream.Length * 4 / 3) / 4090;
            writer.Write("lines=");
            writer.WriteLine(lineCount);

            long posBak = srcStream.Position;
            srcStream.Position = 0;

            long offset = 0;
            long nextReport = reportInterval;
            byte[] buffer = new byte[4090 * 12]; // Process ~48KB at once (encode to ~64KB)
            while (srcStream.Position < srcStream.Length)
            {
                int readByte = srcStream.Read(buffer, 0, buffer.Length);
                string encodedStr = Convert.ToBase64String(buffer, 0, readByte);

                // Count (1) byte offset, and (2) base64 string length
                offset += readByte;
                encodedLen += encodedStr.Length;

                // Remove Base64 Padding (==, =)
                if (readByte < buffer.Length)
                    encodedStr = encodedStr.TrimEnd('=');

                // Tokenize encoded string by 4090 chars
                int encodeBlockLines = encodedStr.Length / 4090;
                for (int x = 0; x < encodeBlockLines; x++)
                {
                    writer.Write(idx);
                    writer.Write("=");
                    writer.WriteLine(encodedStr.Substring(x * 4090, 4090));
                    idx += 1;
                }

                string lastLine = encodedStr.Substring(encodeBlockLines * 4090);
                if (0 < lastLine.Length && encodeBlockLines < 1024 * 4)
                {
                    writer.Write(idx);
                    writer.Write("=");
                    writer.WriteLine(lastLine);
                }

                // Report progress
                if (progress != null && nextReport <= offset)
                {
                    progress.Report(offset);
                    nextReport += reportInterval;
                }
            }

            Debug.Assert(idx == lineCount);
            srcStream.Position = posBak;

            return encodedLen;
        }
        #endregion

        #region Decode
        public static int Decode(TextReader tr, string section, Stream outStream, IProgress<(int Pos, int Total)> progress = null)
        {
            int lineLen = -1;
            int lineCount = 0;
            int i = 0;

            int encodeLen = 0;
            int decodeLen = 0;

            // Process encoded block ~64KB at once
            // Avoid allocation larger than 85KB, to avoid Large Object Heap allocation
            StringBuilder b = new StringBuilder(4090 * 16);

            // Read base64 block directly from file
            string line;
            bool inTargetSection = section.Length == 0; // If section is empty, skip searching
            while ((line = tr.ReadLine()) != null)
            { // Read text line by line
                line = line.Trim();

                // Ignore comment
                if (line.StartsWith("#", StringComparison.Ordinal) ||
                    line.StartsWith(";", StringComparison.Ordinal) ||
                    line.StartsWith("//", StringComparison.Ordinal))
                    continue;

                if (line.StartsWith("[", StringComparison.Ordinal) &&
                    line.EndsWith("]", StringComparison.Ordinal))
                { // Start of section
                    if (inTargetSection)
                        break;

                    // Remove [ and ]
                    string foundSection = line.Substring(1, line.Length - 2);
                    if (section.Equals(foundSection, StringComparison.OrdinalIgnoreCase))
                        inTargetSection = true;
                }
                else if (inTargetSection)
                { // [Stage 1] Found target section
                    if (line.Length == 0)
                        continue;
                    
                    (string key, string block) = IniReadWriter.GetKeyValueFromLine(line);
                    if (key == null || block == null)
                        throw new InvalidOperationException("Encoded lines are malformed");

                    // [Stage 2] Get count of lines
                    if (key.Equals("lines", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!int.TryParse(block, NumberStyles.Integer, CultureInfo.InvariantCulture, out lineCount))
                            return -1; // Failure
                        lineCount += 1; // WB's "lines={count}" is 0-based.
                        continue;
                    }

                    // [Stage 3] Get length of line
                    if (!StringHelper.IsInteger(key))
                        throw new InvalidOperationException("Key of the encoded lines are malformed");
                    if (lineLen == -1)
                        lineLen = block.Length;
                    if (4090 < block.Length ||
                        i + 1 < lineCount && block.Length != lineLen)
                        throw new InvalidOperationException("Length of encoded lines is inconsistent");

                    // [Stage 4] Decode 
                    b.Append(block);
                    encodeLen += block.Length;

                    // If buffer is full, decode ~64KB to ~48KB raw bytes
                    if ((i + 1) % 4090 == 0)
                    {
                        byte[] buffer = Convert.FromBase64String(b.ToString());
                        outStream.Write(buffer, 0, buffer.Length);
                        decodeLen += buffer.Length;
                        b.Clear();
                    }

                    // Report progress
                    if (progress != null && i % 256 == 0)
                        progress.Report((i, lineCount));
                }

                i += 1;
            }

            // Append '=' padding
            switch (encodeLen % 4)
            {
                case 0:
                    break;
                case 1:
                    throw new InvalidOperationException("Wrong base64 padding");
                case 2:
                    b.Append("==");
                    break;
                case 3:
                    b.Append("=");
                    break;
            }

            byte[] finalBuffer = Convert.FromBase64String(b.ToString());
            decodeLen += finalBuffer.Length;
            outStream.Write(finalBuffer, 0, finalBuffer.Length);

            return decodeLen;
        }
        #endregion

        #region DecodeInMem
        public static byte[] DecodeInMem(List<string> encodedList)
        {
            // Remove "lines=n"
            encodedList.RemoveAt(0);

            (List<string> keys, List<string> base64Blocks) = IniReadWriter.GetKeyValueFromLines(encodedList);
            if (keys == null || base64Blocks == null)
                throw new InvalidOperationException("Encoded lines are malformed");
            if (!keys.All(StringHelper.IsInteger))
                throw new InvalidOperationException("Key of the encoded lines are malformed");
            if (base64Blocks.Count == 0)
                throw new InvalidOperationException("Encoded lines are not found");

            StringBuilder b = new StringBuilder();
            foreach (string block in base64Blocks)
                b.Append(block);
            switch (b.Length % 4)
            {
                case 0:
                    break;
                case 1:
                    throw new InvalidOperationException("Encoded lines are malformed");
                case 2:
                    b.Append("==");
                    break;
                case 3:
                    b.Append("=");
                    break;
            }

            return Convert.FromBase64String(b.ToString());
        }
        #endregion
    }
    #endregion

    #region EncodedFileInfo
    public class EncodedFileInfo : IEquatable<EncodedFileInfo>, ICloneable
    {
        public string FolderName;
        public string FileName;
        public int RawSize;
        public int EncodedSize;
        public EncodedFile.EncodeMode? EncodeMode;

        public bool Equals(EncodedFileInfo x)
        {
            if (x == null)
                return false;

            return FolderName.Equals(x.FolderName, StringComparison.OrdinalIgnoreCase) &&
                   FileName.Equals(x.FileName, StringComparison.OrdinalIgnoreCase) &&
                   RawSize == x.RawSize &&
                   EncodedSize == x.EncodedSize &&
                   EncodeMode == x.EncodeMode;
        }

        public object Clone()
        {
            return new EncodedFileInfo
            {
                FolderName = FolderName,
                FileName = FileName,
                RawSize = RawSize,
                EncodedSize = EncodedSize,
                EncodeMode = EncodeMode,
            };
        }

        public override string ToString()
        {
            return ScriptSection.Names.GetEncodedSectionName(FolderName, FileName);
        }
    }
    #endregion
}
