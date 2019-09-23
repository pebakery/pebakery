/*
    Copyright (C) 2016-2019 Hajin Jang
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
    #region (Docs) Attachment Format
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
                           (Type 3) Length of XZ-compressed File
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
    public static class EncodedFile
    {
        #region Enum EncodeMode 
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public enum EncodeMode : byte
        {
            ZLib = 0x00, // Type 1
            Raw = 0x01, // Type 2
            XZ = 0x02 // Type 3 (PEBakery Only)
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

        #region Const
        public const long DecodeInMemorySizeLimit = 4 * 1024 * 1024; // 4MB
        public const long InterfaceTextSizeLimit = 16 * 1024; // 16KB
        private const long BufferSize = 64 * 1024; // 64KB
        private const long ReportInterval = 1024 * 1024; // 1MB

        public const double CompReportFactor = 0.8;
        public const double Base64ReportFactor = 0.2;
        #endregion

        #region Dict ImageEncodeDict
        public static readonly ReadOnlyDictionary<ImageHelper.ImageFormat, EncodeMode> ImageEncodeDict = new ReadOnlyDictionary<ImageHelper.ImageFormat, EncodeMode>(
            new Dictionary<ImageHelper.ImageFormat, EncodeMode>
            {
                // Auto detect compress algorithm by extension.
                // Note: .ico file can be either raw (bitmap) or compressed (png).
                //       To be sure, use EncodeMode.ZLib in .ico file.
                { ImageHelper.ImageFormat.Bmp, EncodeMode.ZLib },
                { ImageHelper.ImageFormat.Jpg, EncodeMode.Raw },
                { ImageHelper.ImageFormat.Png, EncodeMode.Raw },
                { ImageHelper.ImageFormat.Gif, EncodeMode.Raw },
                { ImageHelper.ImageFormat.Ico, EncodeMode.ZLib },
                { ImageHelper.ImageFormat.Svg, EncodeMode.ZLib }
            });
        #endregion

        #region AttachFile, ContainsFile
        public static Task AttachFileAsync(Script sc, string folderName, string fileName, string srcFilePath, EncodeMode type, IProgress<double> progress)
        {
            return Task.Run(() => AttachFile(sc, folderName, fileName, srcFilePath, type, progress));
        }

        public static void AttachFile(Script sc, string folderName, string fileName, string srcFilePath, EncodeMode type, IProgress<double> progress)
        {
            if (sc == null)
                throw new ArgumentNullException(nameof(sc));

            if (!StringEscaper.IsFileNameValid(folderName, new char[] { '[', ']', '\t' }))
                throw new ArgumentException($"[{folderName}] contains invalid character");
            if (!StringEscaper.IsFileNameValid(fileName, new char[] { '[', ']', '\t' }))
                throw new ArgumentException($"[{fileName}] contains invalid character");

            using (FileStream fs = new FileStream(srcFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Encode(sc, folderName, fileName, fs, type, false, progress);
            }
        }

        public static Task AttachFileAsync(Script sc, string folderName, string fileName, Stream srcStream, EncodeMode type, IProgress<double> progress)
        {
            return Task.Run(() => AttachFile(sc, folderName, fileName, srcStream, type, progress));
        }

        public static void AttachFile(Script sc, string folderName, string fileName, Stream srcStream, EncodeMode type, IProgress<double> progress)
        {
            if (sc == null)
                throw new ArgumentNullException(nameof(sc));

            if (!StringEscaper.IsFileNameValid(folderName, new char[] { '[', ']', '\t' }))
                throw new ArgumentException($"[{folderName}] contains invalid character");
            if (!StringEscaper.IsFileNameValid(fileName, new char[] { '[', ']', '\t' }))
                throw new ArgumentException($"[{fileName}] contains invalid character");

            Encode(sc, folderName, fileName, srcStream, type, false, progress);
        }

        public static Task AttachFileAsync(Script sc, string folderName, string fileName, byte[] srcBuffer, EncodeMode type, IProgress<double> progress)
        {
            return Task.Run(() => AttachFile(sc, folderName, fileName, srcBuffer, type, progress));
        }

        public static void AttachFile(Script sc, string folderName, string fileName, byte[] srcBuffer, EncodeMode type, IProgress<double> progress)
        {
            if (sc == null)
                throw new ArgumentNullException(nameof(sc));

            if (!StringEscaper.IsFileNameValid(folderName, new char[] { '[', ']', '\t' }))
                throw new ArgumentException($"[{folderName}] contains invalid character");
            if (!StringEscaper.IsFileNameValid(fileName, new char[] { '[', ']', '\t' }))
                throw new ArgumentException($"[{fileName}] contains invalid character");

            Encode(sc, folderName, fileName, srcBuffer, type, false, progress);
        }

        public static Task AttachFilesAsync(Script sc, string folderName, (string Name, string Path)[] srcFiles, EncodeMode type, IProgress<double> progress)
        {
            return Task.Run(() => AttachFiles(sc, folderName, srcFiles, type, progress));
        }

        public static void AttachFiles(Script sc, string folderName, (string Name, string Path)[] srcFiles, EncodeMode type, IProgress<double> progress)
        {
            if (sc == null)
                throw new ArgumentNullException(nameof(sc));

            if (!StringEscaper.IsFileNameValid(folderName, new char[] { '[', ']', '\t' }))
                throw new ArgumentException($"[{folderName}] contains invalid character");
            foreach ((string fileName, _) in srcFiles)
            {
                if (!StringEscaper.IsFileNameValid(fileName, new char[] { '[', ']', '\t' }))
                    throw new ArgumentException($"[{fileName}] contains invalid character");
            }

            // TODO: Implement multiple file attachment in Encode() method.
            // This is just a temporary shim. Need proper rework.
            int i = 0;
            IProgress<double> progressShim = new Progress<double>(x => { progress.Report((x + i) / srcFiles.Length); });
            foreach ((string fileName, string srcFilePath) in srcFiles)
            {
#pragma warning disable IDE0068 // 권장 dispose 패턴 사용
                using (FileStream fs = new FileStream(srcFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
#pragma warning restore IDE0068 // 권장 dispose 패턴 사용
                {
                    Encode(sc, folderName, fileName, fs, type, false, progressShim);
                }
                i += 1;
            }
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
        public static Task AttachInterfaceAsync(Script sc, string fileName, string srcFilePath, IProgress<double> progress)
        {
            return Task.Run(() => AttachInterface(sc, fileName, srcFilePath, progress));
        }

        public static void AttachInterface(Script sc, string fileName, string srcFilePath, IProgress<double> progress)
        {
            if (!StringEscaper.IsFileNameValid(fileName, new char[] { '[', ']', '\t' }))
                throw new ArgumentException($"Filename [{fileName}] contains invalid character");

            if (fileName.Equals(UIInfo_Image.NoResource, StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals(UIInfo_TextFile.NoResource, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"Filename [{fileName}] is reserved");

            EncodeMode type = EncodeMode.ZLib;
            if (ImageHelper.GetImageFormat(srcFilePath, out ImageHelper.ImageFormat imageType))
            {
                if (ImageEncodeDict.ContainsKey(imageType))
                    type = ImageEncodeDict[imageType];
            }

            AttachFile(sc, ScriptSection.Names.InterfaceEncoded, fileName, srcFilePath, type, progress);
        }

        public static bool ContainsInterface(Script sc, string fileName)
        {
            if (fileName.Equals(UIInfo_Image.NoResource, StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals(UIInfo_TextFile.NoResource, StringComparison.OrdinalIgnoreCase))
                return false;

            return ContainsFile(sc, ScriptSection.Names.InterfaceEncoded, fileName);
        }

        public static Dictionary<string, int> GetInterfaceFileRefCount(Script sc)
        {
            (_, List<UIControl> uiCtrls, _) = sc.GetInterfaceControls();
            Dictionary<string, int> fileRefCountDict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (UIControl thisCtrl in uiCtrls)
            {
                switch (thisCtrl.Type)
                {
                    case UIControlType.Image:
                    case UIControlType.TextFile:
                        {
                            string resourceSection = StringEscaper.Unescape(thisCtrl.Text);
                            if (ContainsInterface(sc, resourceSection))
                            {
                                if (fileRefCountDict.ContainsKey(resourceSection))
                                    fileRefCountDict[resourceSection] += 1;
                                else
                                    fileRefCountDict[resourceSection] = 1;
                            }
                        }
                        break;
                    case UIControlType.Button:
                        {
                            UIInfo_Button info = thisCtrl.Info.Cast<UIInfo_Button>();

                            if (info.Picture != null && ContainsInterface(sc, info.Picture))
                            {
                                if (fileRefCountDict.ContainsKey(info.Picture))
                                    fileRefCountDict[info.Picture] += 1;
                                else
                                    fileRefCountDict[info.Picture] = 1;
                            }
                        }
                        break;
                }
            }

            return fileRefCountDict;
        }
        #endregion

        #region AttachLogo, ContainsLogo
        public static Task AttachLogoAsync(Script sc, string fileName, string srcFilePath)
        {
            return Task.Run(() => AttachLogo(sc, fileName, srcFilePath));
        }

        public static void AttachLogo(Script sc, string fileName, string srcFilePath)
        {
            if (sc == null)
                throw new ArgumentNullException(nameof(sc));
            if (srcFilePath == null)
                throw new ArgumentNullException(nameof(srcFilePath));

            if (!StringEscaper.IsFileNameValid(fileName, new char[] { '[', ']', '\t' }))
                throw new ArgumentException($"[{fileName}] contains invalid character");

            if (!ImageHelper.GetImageFormat(srcFilePath, out ImageHelper.ImageFormat imageType))
                throw new ArgumentException($"Image [{Path.GetExtension(srcFilePath)}] is not supported");

            using (FileStream fs = new FileStream(srcFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Encode(sc, ScriptSection.Names.AuthorEncoded, fileName, fs, ImageEncodeDict[imageType], true, null);
            }
        }

        public static void AttachLogo(Script sc, string folderName, string fileName, Stream srcStream, EncodeMode type)
        {
            if (sc == null)
                throw new ArgumentNullException(nameof(sc));

            Encode(sc, folderName, fileName, srcStream, type, true, null);
        }

        public static void AttachLogo(Script sc, string folderName, string fileName, byte[] srcBuffer, EncodeMode type)
        {
            if (sc == null)
                throw new ArgumentNullException(nameof(sc));

            Encode(sc, folderName, fileName, srcBuffer, type, true, null);
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

        #region RenameFile, RenameFolder
        public static Task<(Script, string)> RenameFileAsync(Script sc, string folderName, string oldFileName, string newFileName)
        {
            return Task.Run(() => RenameFile(sc, folderName, oldFileName, newFileName));
        }

        public static (Script, string) RenameFile(Script sc, string folderName, string oldFileName, string newFileName)
        {
            if (sc == null)
                throw new ArgumentNullException(nameof(sc));
            if (folderName == null)
                throw new ArgumentNullException(nameof(folderName));
            if (oldFileName == null)
                throw new ArgumentNullException(nameof(oldFileName));
            if (newFileName == null)
                throw new ArgumentNullException(nameof(newFileName));
            string errorMsg = null;

            // If oldFileName and newFileName is equal, report success without doing anything
            if (oldFileName.Equals(newFileName, StringComparison.OrdinalIgnoreCase))
                return (sc, null);

            // Backup
            string backupFile = FileHelper.GetTempFile("script");
            File.Copy(sc.RealPath, backupFile, true);
            try
            {
                if (!sc.Sections.ContainsKey(folderName))
                    return (sc, $"Index of encoded folder [{folderName}] not found in [{sc.RealPath}]");

                // Get encoded file index
                Dictionary<string, string> fileDict = sc.Sections[folderName].IniDict;
                if (!fileDict.ContainsKey(oldFileName))
                    return (sc, $"Index of encoded file [{oldFileName}] not found in [{sc.RealPath}]");

                // Rename encoded file index
                if (!IniReadWriter.RenameKey(sc.RealPath, folderName, oldFileName, newFileName))
                    errorMsg = $"Unable to rename index of encoded file to [{newFileName}] from [{oldFileName}]";

                // Rename encoded file section
                string oldEncodedSection = ScriptSection.Names.GetEncodedSectionName(folderName, oldFileName);
                string newEncodedSection = ScriptSection.Names.GetEncodedSectionName(folderName, newFileName);
                if (!IniReadWriter.RenameSection(sc.RealPath, oldEncodedSection, newEncodedSection))
                    errorMsg = $"Unable to rename encoded file to [{newFileName}] from [{oldFileName}]";
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

        public static Task<(Script, string)> RenameFolderAsync(Script sc, string oldFolderName, string newFolderName)
        {
            return Task.Run(() => RenameFolder(sc, oldFolderName, newFolderName));
        }

        public static (Script, string) RenameFolder(Script sc, string oldFolderName, string newFolderName)
        {
            if (sc == null)
                throw new ArgumentNullException(nameof(sc));
            if (oldFolderName == null)
                throw new ArgumentNullException(nameof(oldFolderName));
            if (newFolderName == null)
                throw new ArgumentNullException(nameof(newFolderName));
            string errorMsg = null;

            // If oldFileName and newFileName is equal, report success without doing anything
            if (oldFolderName.Equals(newFolderName, StringComparison.OrdinalIgnoreCase))
                return (sc, null);

            // Check if oldFileName is valid
            if (oldFolderName.Equals(ScriptSection.Names.AuthorEncoded, StringComparison.OrdinalIgnoreCase))
                return (sc, $"[{ScriptSection.Names.AuthorEncoded}] cannot be renamed");
            if (oldFolderName.Equals(ScriptSection.Names.InterfaceEncoded, StringComparison.OrdinalIgnoreCase))
                return (sc, $"[{ScriptSection.Names.InterfaceEncoded}] cannot be renamed");

            // Check if newFileName is valid
            if (newFolderName.Equals(ScriptSection.Names.AuthorEncoded, StringComparison.OrdinalIgnoreCase))
                return (sc, $"Encoded folder [{oldFolderName}] cannot be renamed to [{ScriptSection.Names.AuthorEncoded}]");
            if (newFolderName.Equals(ScriptSection.Names.InterfaceEncoded, StringComparison.OrdinalIgnoreCase))
                return (sc, $"Encoded folder [{oldFolderName}] cannot be renamed to [{ScriptSection.Names.InterfaceEncoded}]");

            // Check if script has [EncodedFolders]
            if (!sc.Sections.ContainsKey(ScriptSection.Names.EncodedFolders))
                return (sc, $"Encoded folders not found in [{sc.RealPath}]");

            // Check if script has an index of [oldFolderName]
            List<string> folders = IniReadWriter.FilterCommentLines(sc.Sections[ScriptSection.Names.EncodedFolders].Lines);
            if (!folders.Contains(oldFolderName, StringComparer.OrdinalIgnoreCase))
                return (sc, $"Index of encoded folder [{oldFolderName}] not found in [{sc.RealPath}]");

            // Backup
            string backupFile = FileHelper.GetTempFile("script");
            File.Copy(sc.RealPath, backupFile, true);
            try
            {
                // Rename index of encoded folder
                int idx = folders.FindIndex(x => x.Equals(oldFolderName, StringComparison.OrdinalIgnoreCase));
                folders[idx] = newFolderName;

                // Cannot use RenameKey, since [EncodedFolders] does not use '=' in its content.
                // Rewrite entire [EncodedFolders] with DeleteSection and WriteRawLine.
                if (!IniReadWriter.DeleteSection(sc.RealPath, ScriptSection.Names.EncodedFolders))
                    return (sc, $"Unable to rename index of encoded folder to [{newFolderName}] from [{oldFolderName}]");
                foreach (IniKey key in folders.Select(x => new IniKey(ScriptSection.Names.EncodedFolders, x)))
                {
                    if (!IniReadWriter.WriteRawLine(sc.RealPath, key))
                        return (sc, $"Unable to rename index of encoded folder to [{newFolderName}] from [{oldFolderName}]");
                }

                // Check if script has [oldFolderName]
                if (!sc.Sections.ContainsKey(oldFolderName))
                    return (sc, $"Index of encoded folder [{oldFolderName}] not found in [{sc.RealPath}]");

                // Rename section [oldFolderName] to [newFolderName]
                // Do continue even renaming failed, to ensure other orphan sections are cleared up.
                if (!IniReadWriter.RenameSection(sc.RealPath, oldFolderName, newFolderName))
                    errorMsg = $"Unable to rename encoded folder to [{newFolderName}] from [{oldFolderName}]";

                // Get index of files 
                Dictionary<string, string> fileDict = sc.Sections[oldFolderName].IniDict;
                if (oldFolderName.Equals(ScriptSection.Names.AuthorEncoded, StringComparison.OrdinalIgnoreCase))
                {
                    if (fileDict.ContainsKey("Logo"))
                        fileDict.Remove("Logo");
                }

                // Rename encoded file section
                foreach (string file in fileDict.Keys)
                {
                    string oldSectionName = ScriptSection.Names.GetEncodedSectionName(oldFolderName, file);
                    string newSectionName = ScriptSection.Names.GetEncodedSectionName(newFolderName, file);
                    if (!IniReadWriter.RenameSection(sc.RealPath, oldSectionName, newSectionName))
                        errorMsg = $"Unable to rename encoded folder to [{newFolderName}] from [{oldFolderName}]";
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
                string section = ScriptSection.Names.GetEncodedSectionName(folderName, fileName);
                if (!sc.Sections.ContainsKey(section))
                    throw new InvalidOperationException($"[{folderName}\\{fileName}] does not exists in [{sc.RealPath}]");

                string destFile = Path.Combine(destDir, fileName);
                if (!overwrite && File.Exists(destFile))
                    throw new InvalidOperationException($"File [{destFile}] cannot be overwritten");

#pragma warning disable IDE0068 // 권장 dispose 패턴 사용
                using (FileStream fs = new FileStream(destFile, FileMode.Create, FileAccess.Write))
#pragma warning restore IDE0068 // 권장 dispose 패턴 사용
                {
                    Decode(sc.RealPath, section, fs, null);
                }
            }
        }

        public static Task<MemoryStream> ExtractLogoAsync(Script sc)
        {
            return Task.Run(() => ExtractLogo(sc, out _, out _));
        }

        public static MemoryStream ExtractLogo(Script sc, out ImageHelper.ImageFormat type, out string filename)
        {
            if (sc == null)
                throw new ArgumentNullException(nameof(sc));

            if (!sc.Sections.ContainsKey(ScriptSection.Names.AuthorEncoded))
                throw new InvalidOperationException("Directory [AuthorEncoded] does not exist");

            Dictionary<string, string> fileDict = sc.Sections[ScriptSection.Names.AuthorEncoded].IniDict;
            if (!fileDict.ContainsKey("Logo"))
                throw new InvalidOperationException($"Logo does not exist in [{sc.Title}]");

            string logoFile = fileDict["Logo"];
            filename = logoFile;
            if (!ImageHelper.GetImageFormat(logoFile, out type))
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
        public static Task<ResultReport<EncodedFileInfo>> GetFileInfoAsync(Script sc, string folderName, string fileName, bool inspectEncodeMode = false)
        {
            return Task.Run(() => GetFileInfo(sc, folderName, fileName, inspectEncodeMode));
        }

        public static ResultReport<EncodedFileInfo> GetFileInfo(Script sc, string folderName, string fileName, bool inspectEncodeMode = false)
        {
            if (sc == null)
                throw new ArgumentNullException(nameof(sc));

            EncodedFileInfo info = new EncodedFileInfo
            {
                FolderName = folderName,
                FileName = fileName
            };

            if (!sc.Sections.ContainsKey(folderName))
                return new ResultReport<EncodedFileInfo>(false, null, $"Directory [{folderName}] does not exist");

            Dictionary<string, string> fileDict = sc.Sections[folderName].IniDict;
            if (!fileDict.ContainsKey(fileName))
                return new ResultReport<EncodedFileInfo>(false, null, $"File index of [{fileName}] does not exist");

            string fileIndex = fileDict[fileName].Trim();
            (info.RawSize, info.EncodedSize) = ParseFileIndex(fileIndex);
            if (info.RawSize == -1)
                return new ResultReport<EncodedFileInfo>(false, null, $"Unable to parse raw size of [{fileName}]");
            if (info.EncodedSize == -1)
                return new ResultReport<EncodedFileInfo>(false, null, $"Unable to parse encoded size of [{fileName}]");

            if (inspectEncodeMode)
            {
                string section = ScriptSection.Names.GetEncodedSectionName(folderName, fileName);
                info.EncodeMode = ReadEncodeMode(sc.RealPath, section);
            }

            return new ResultReport<EncodedFileInfo>(true, info, null);
        }

        public static Task<ResultReport<EncodedFileInfo>> GetLogoInfoAsync(Script sc, bool inspectEncodeMode = false)
        {
            return Task.Run(() => GetLogoInfo(sc, inspectEncodeMode));
        }

        public static ResultReport<EncodedFileInfo> GetLogoInfo(Script sc, bool inspectEncodeMode = false)
        {
            if (sc == null)
                throw new ArgumentNullException(nameof(sc));

            EncodedFileInfo info = new EncodedFileInfo { FolderName = ScriptSection.Names.AuthorEncoded };

            if (!sc.Sections.ContainsKey(ScriptSection.Names.AuthorEncoded))
                return new ResultReport<EncodedFileInfo>(false, null, $"Directory [{ScriptSection.Names.AuthorEncoded}] does not exist");

            Dictionary<string, string> fileDict = sc.Sections[ScriptSection.Names.AuthorEncoded].IniDict;
            if (!fileDict.ContainsKey("Logo"))
                return new ResultReport<EncodedFileInfo>(false, null, "Logo does not exist");

            info.FileName = fileDict["Logo"];
            if (!fileDict.ContainsKey(info.FileName))
                return new ResultReport<EncodedFileInfo>(false, null, "File index of [Logo] does not exist");

            string fileIndex = fileDict[info.FileName].Trim();
            (info.RawSize, info.EncodedSize) = ParseFileIndex(fileIndex);
            if (info.RawSize == -1)
                return new ResultReport<EncodedFileInfo>(false, null, $"Unable to parse raw size of [{info.FileName}]");
            if (info.EncodedSize == -1)
                return new ResultReport<EncodedFileInfo>(false, null, $"Unable to parse encoded size of [{info.FileName}]");

            if (inspectEncodeMode)
            {
                string section = ScriptSection.Names.GetEncodedSectionName(ScriptSection.Names.AuthorEncoded, info.FileName);
                if (!sc.Sections.ContainsKey(section))
                    throw new InvalidOperationException($"[{info.FileName}] does not exist in interface of [{sc.RealPath}]");

                string[] encoded = sc.Sections[section].Lines;
                info.EncodeMode = ReadEncodeModeInMem(encoded);
            }

            return new ResultReport<EncodedFileInfo>(true, info, null);
        }

        public static Task<ResultReport<EncodedFileInfo[]>> GetFolderInfoAsync(Script sc, string folderName, bool inspectEncodeMode = false)
        {
            return Task.Run(() => GetFolderInfo(sc, folderName, inspectEncodeMode));
        }

        public static ResultReport<EncodedFileInfo[]> GetFolderInfo(Script sc, string folderName, bool inspectEncodeMode = false)
        {
            if (sc == null)
                throw new ArgumentNullException(nameof(sc));

            if (!sc.Sections.ContainsKey(folderName))
                return new ResultReport<EncodedFileInfo[]>(false, null, $"Directory [{folderName}] does not exist");

            List<EncodedFileInfo> infos = new List<EncodedFileInfo>();
            Dictionary<string, string> fileDict = sc.Sections[folderName].IniDict;
            foreach (string fileName in fileDict.Keys)
            {
                EncodedFileInfo info = new EncodedFileInfo
                {
                    FolderName = folderName,
                    FileName = fileName
                };

                if (!fileDict.ContainsKey(fileName))
                    return new ResultReport<EncodedFileInfo[]>(false, null, $"File index of [{fileName}] does not exist");

                string fileIndex = fileDict[fileName].Trim();
                (info.RawSize, info.EncodedSize) = ParseFileIndex(fileIndex);
                if (info.RawSize == -1)
                    return new ResultReport<EncodedFileInfo[]>(false, null, $"Unable to parse raw size of [{fileName}]");
                if (info.EncodedSize == -1)
                    return new ResultReport<EncodedFileInfo[]>(false, null, $"Unable to parse encoded size of [{fileName}]");

                if (inspectEncodeMode)
                {
                    string section = ScriptSection.Names.GetEncodedSectionName(folderName, fileName);
                    info.EncodeMode = ReadEncodeMode(sc.RealPath, section);
                }

                infos.Add(info);
            }

            return new ResultReport<EncodedFileInfo[]>(true, infos.ToArray(), null);
        }

        public struct GetFileInfoOptions
        {
            public bool IncludeAuthorEncoded;
            public bool IncludeInterfaceEncoded;
            public bool InspectEncodeMode;
        }

        public static Task<ResultReport<Dictionary<string, List<EncodedFileInfo>>>> GetAllFilesInfoAsync(Script sc, GetFileInfoOptions opts)
        {
            return Task.Run(() => GetAllFilesInfo(sc, opts));
        }

        public static ResultReport<Dictionary<string, List<EncodedFileInfo>>> GetAllFilesInfo(Script sc, GetFileInfoOptions opts)
        {
            if (sc == null)
                throw new ArgumentNullException(nameof(sc));

            Dictionary<string, List<EncodedFileInfo>> infoDict = new Dictionary<string, List<EncodedFileInfo>>(StringComparer.OrdinalIgnoreCase);
            if (!sc.Sections.ContainsKey(ScriptSection.Names.EncodedFolders))
                return new ResultReport<Dictionary<string, List<EncodedFileInfo>>>(true, infoDict, null); // Return empty dict

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

            if (opts.IncludeAuthorEncoded && sc.Sections.ContainsKey(ScriptSection.Names.AuthorEncoded))
                folderNames.Add(ScriptSection.Names.AuthorEncoded);
            if (opts.IncludeInterfaceEncoded && sc.Sections.ContainsKey(ScriptSection.Names.InterfaceEncoded))
                folderNames.Add(ScriptSection.Names.InterfaceEncoded);

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
                        FileName = fileName
                    };

                    // In [AuthorEncoded], "Logo=" line does not contain proper encoded file information
                    if (opts.IncludeAuthorEncoded && fileName.Equals("Logo", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!fileDict.ContainsKey(fileName))
                        return new ResultReport<Dictionary<string, List<EncodedFileInfo>>>(false, null, $"File index of [{fileName}] does not exist");

                    (info.RawSize, info.EncodedSize) = ParseFileIndex(fileIndex);
                    if (info.RawSize == -1)
                        return new ResultReport<Dictionary<string, List<EncodedFileInfo>>>(false, null, $"Unable to parse raw size of [{fileName}]");
                    if (info.EncodedSize == -1)
                        return new ResultReport<Dictionary<string, List<EncodedFileInfo>>>(false, null, $"Unable to parse encoded size of [{fileName}]");

                    if (opts.InspectEncodeMode)
                    {
                        string section = ScriptSection.Names.GetEncodedSectionName(folderName, fileName);
                        info.EncodeMode = ReadEncodeMode(sc.RealPath, section);
                    }

                    infoDict[folderName].Add(info);
                }
            }

            return new ResultReport<Dictionary<string, List<EncodedFileInfo>>>(true, infoDict, null);
        }

        public static Task<EncodeMode> GetEncodeModeAsync(Script sc, string folderName, string fileName, bool inMem = false)
        {
            return Task.Run(() => GetEncodeMode(sc, folderName, fileName, inMem));
        }

        public static EncodeMode GetEncodeMode(Script sc, string folderName, string fileName, bool inMem = false)
        {
            string section = ScriptSection.Names.GetEncodedSectionName(folderName, fileName);
            if (inMem)
            {
                if (!sc.Sections.ContainsKey(section))
                    throw new InvalidOperationException($"Unable to find encoded section of [{fileName}]");
                string[] encoded = sc.Sections[section].Lines;
                return ReadEncodeModeInMem(encoded);
            }
            else
            {
                return ReadEncodeMode(sc.RealPath, section);
            }
        }

        /// <summary>
        /// Parse file index
        /// </summary>
        /// <param name="fileIndex">String of file index Ex) "522,696"</param>
        /// <returns>
        /// If succeed, return (rawSize, encodedSize)
        /// If fails, return (-1, -1)
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
        public static Task<ResultReport<Script>> DeleteFileAsync(Script sc, string folderName, string fileName)
        {
            return Task.Run(() => DeleteFile(sc, folderName, fileName));
        }

        public static ResultReport<Script> DeleteFile(Script sc, string folderName, string fileName)
        {
            if (sc == null)
                throw new ArgumentNullException(nameof(sc));
            if (folderName == null)
                throw new ArgumentNullException(nameof(folderName));
            if (fileName == null)
                throw new ArgumentNullException(nameof(fileName));
            string errorMsg = null;

            // Backup
            string backupFile = FileHelper.GetTempFile("script");
            File.Copy(sc.RealPath, backupFile, true);
            try
            {
                if (!sc.Sections.ContainsKey(folderName))
                    return new ResultReport<Script>(false, sc, $"Index of encoded folder [{folderName}] not found in [{sc.RealPath}]");

                // Get encoded file index
                Dictionary<string, string> fileDict = sc.Sections[folderName].IniDict;
                if (!fileDict.ContainsKey(fileName))
                    return new ResultReport<Script>(false, sc, $"Index of encoded file [{fileName}] not found in [{sc.RealPath}]");

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
            return new ResultReport<Script>(errorMsg == null, sc, errorMsg);
        }

        public static Task<ResultReport<Script, string[]>> DeleteFilesAsync(Script sc, string folderName, IReadOnlyList<string> fileNames)
        {
            return Task.Run(() => DeleteFiles(sc, folderName, fileNames));
        }

        public static ResultReport<Script, string[]> DeleteFiles(Script sc, string folderName, IReadOnlyList<string> fileNames)
        {
            if (sc == null)
                throw new ArgumentNullException(nameof(sc));
            if (folderName == null)
                throw new ArgumentNullException(nameof(folderName));
            if (fileNames == null)
                throw new ArgumentNullException(nameof(fileNames));

            List<string> errorMessages = new List<string>();

            // Backup
            string backupFile = FileHelper.GetTempFile("script");
            File.Copy(sc.RealPath, backupFile, true);
            try
            {
                if (!sc.Sections.ContainsKey(folderName))
                {
                    errorMessages.Add($"Index of encoded folder [{folderName}] not found in [{sc.RealPath}]");
                    return new ResultReport<Script, string[]>(false, sc, errorMessages.ToArray());
                }

                // Get encoded file index
                List<IniKey> iniKeys = new List<IniKey>();
                Dictionary<string, string> fileDict = sc.Sections[folderName].IniDict;
                foreach (string fileName in fileNames)
                {
                    if (fileDict.ContainsKey(fileName))
                        iniKeys.Add(new IniKey(folderName, fileName));
                    else
                        errorMessages.Add($"Index of encoded folder [{folderName}] not found in [{sc.RealPath}]");
                }

                // Delete encoded file index
                List<int> removeIdx = new List<int>();
                bool[] results = IniReadWriter.DeleteKeys(sc.RealPath, iniKeys);
                for (int i = 0; i < results.Length; i++)
                {
                    if (!results[i])
                    {
                        errorMessages.Add($"Unable to delete index of encoded file [{iniKeys[i].Value}] from [{sc.RealPath}]");
                        removeIdx.Add(i);
                    }
                }

                // Filter out invalid iniKeys
                foreach (int idx in removeIdx.OrderByDescending(i => i))
                    iniKeys.RemoveAt(idx);

                // Delete encoded file section
                var sectionNames = iniKeys.Select(x => ScriptSection.Names.GetEncodedSectionName(x.Section, x.Key));
                results = IniReadWriter.DeleteSections(sc.RealPath, sectionNames);
                for (int i = 0; i < results.Length; i++)
                {
                    if (!results[i])
                        errorMessages.Add($"Unable to delete section of encoded file [{iniKeys[i].Value}] from [{sc.RealPath}]");
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
            return new ResultReport<Script, string[]>(true, sc, errorMessages.ToArray());
        }

        public static Task<ResultReport<Script>> DeleteFolderAsync(Script sc, string folderName)
        {
            return Task.Run(() => DeleteFolder(sc, folderName));
        }

        public static ResultReport<Script> DeleteFolder(Script sc, string folderName)
        {
            if (sc == null)
                throw new ArgumentNullException(nameof(sc));
            if (folderName == null)
                throw new ArgumentNullException(nameof(folderName));
            string errorMsg = null;

            // Backup
            string backupFile = FileHelper.GetTempFile("script");
            File.Copy(sc.RealPath, backupFile, true);
            try
            {
                if (!folderName.Equals(ScriptSection.Names.AuthorEncoded, StringComparison.OrdinalIgnoreCase) &&
                    !folderName.Equals(ScriptSection.Names.InterfaceEncoded, StringComparison.OrdinalIgnoreCase))
                {
                    if (!sc.Sections.ContainsKey(ScriptSection.Names.EncodedFolders))
                        return new ResultReport<Script>(false, sc, $"Index of encoded folder [{folderName}] not found in [{sc.RealPath}]");

                    List<string> folders = IniReadWriter.FilterCommentLines(sc.Sections[ScriptSection.Names.EncodedFolders].Lines);
                    if (!folders.Contains(folderName, StringComparer.OrdinalIgnoreCase))
                        return new ResultReport<Script>(false, sc, $"Index of encoded folder [{folderName}] not found in [{sc.RealPath}]");

                    // Delete index of encoded folder
                    int idx = folders.FindIndex(x => x.Equals(folderName, StringComparison.OrdinalIgnoreCase));
                    folders.RemoveAt(idx);

                    // Cannot use RenameKey, since [EncodedFolders] does not use '=' in its content.
                    // Rewrite entire [EncodedFolders] with DeleteSection and WriteRawLine.
                    if (!IniReadWriter.DeleteSection(sc.RealPath, ScriptSection.Names.EncodedFolders))
                        return new ResultReport<Script>(false, sc, $"Unable to delete index of encoded folder [{folderName}] from [{sc.RealPath}]");

                    foreach (IniKey key in folders.Select(x => new IniKey(ScriptSection.Names.EncodedFolders, x)))
                    {
                        if (!IniReadWriter.WriteRawLine(sc.RealPath, key))
                            return new ResultReport<Script>(false, sc, $"Unable to delete index of encoded folder [{folderName}] from [{sc.RealPath}]");
                    }
                }

                if (!sc.Sections.ContainsKey(folderName))
                {
                    errorMsg = $"Index of encoded folder [{folderName}] not found in [{sc.RealPath}]";
                }
                else
                {
                    Dictionary<string, string> fileDict = sc.Sections[folderName].IniDict;

                    // Delete section [folderName]
                    if (!IniReadWriter.DeleteSection(sc.RealPath, folderName))
                        errorMsg = $"Encoded folder [{folderName}] not found in [{sc.RealPath}]";

                    // Get index of files
                    if (folderName.Equals(ScriptSection.Names.AuthorEncoded, StringComparison.OrdinalIgnoreCase))
                    {
                        if (fileDict.ContainsKey("Logo"))
                            fileDict.Remove("Logo");
                    }

                    // Delete encoded file section
                    foreach (string file in fileDict.Keys)
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
            return new ResultReport<Script>(errorMsg == null, sc, errorMsg);
        }

        public static Task<ResultReport<Script>> DeleteLogoAsync(Script sc)
        {
            return Task.Run(() => DeleteLogo(sc));
        }

        public static ResultReport<Script> DeleteLogo(Script sc)
        {
            if (sc == null)
                throw new ArgumentNullException(nameof(sc));

            string errorMsg = null;

            // Backup
            string backupFile = FileHelper.GetTempFile("script");
            File.Copy(sc.RealPath, backupFile, true);
            try
            {
                // Get encoded file index
                if (!sc.Sections.ContainsKey(ScriptSection.Names.AuthorEncoded))
                    return new ResultReport<Script>(false, sc, $"Logo not found in [{sc.RealPath}]");

                Dictionary<string, string> fileDict = sc.Sections[ScriptSection.Names.AuthorEncoded].IniDict;

                // Get filename of logo
                if (!fileDict.ContainsKey("Logo"))
                    return new ResultReport<Script>(false, sc, $"Logo not found in [{sc.RealPath}]");

                string logoFile = fileDict["Logo"];
                if (!fileDict.ContainsKey(logoFile))
                    return new ResultReport<Script>(false, sc, $"Logo not found in [{sc.RealPath}]");

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
            return new ResultReport<Script>(errorMsg == null, sc, errorMsg);
        }
        #endregion

        #region Encode
        private static void Encode(Script sc, string folderName, string fileName, byte[] input, EncodeMode mode, bool encodeLogo, IProgress<double> progress)
        {
            using (MemoryStream ms = Global.MemoryStreamManager.GetStream("EncodedFile.Encode", input, 0, input.Length))
            {
                Encode(sc, folderName, fileName, ms, mode, encodeLogo, progress);
            }
        }

        /// <summary>
        /// Encode a resource into a script.
        /// Refreshes scripts automatically.
        /// </summary>
        /// <param name="sc"></param>
        /// <param name="folderName"></param>
        /// <param name="fileName"></param>
        /// <param name="inputStream"></param>
        /// <param name="mode"></param>
        /// <param name="encodeLogo"></param>
        /// <param name="progress"></param>
        private static void Encode(Script sc, string folderName, string fileName, Stream inputStream, EncodeMode mode, bool encodeLogo, IProgress<double> progress)
        {
            // Check filename
            byte[] fileNameUtf8 = Encoding.UTF8.GetBytes(fileName);
            if (fileNameUtf8.Length == 0 || 512 <= fileNameUtf8.Length)
                throw new InvalidOperationException("UTF8 encoded filename should be shorter than 512B");
            string section = ScriptSection.Names.GetEncodedSectionName(folderName, fileName);

            // [Stage 1] Backup original script and prepare temp files
            string backupFile = FileHelper.GetTempFile("");
            File.Copy(sc.RealPath, backupFile, true);
            string tempCompressed = FileHelper.GetTempFile(".bin");
            try
            {
                int encodedLen;
                using (FileStream encodeStream = new FileStream(tempCompressed, FileMode.Create, FileAccess.ReadWrite))
                {
                    // [Stage 2] Compress file with zlib
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

                    // [Stage 3] Generate first footer
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

                    // [Stage 4] Compress first footer and concat to body
                    long compressedFooterLen = encodeStream.Position;
                    using (ZLibStream zs = new ZLibStream(encodeStream, ZLibMode.Compress, ZLibCompLevel.Level6, true))
                    {
                        zs.Write(rawFooter, 0, rawFooter.Length);
                    }
                    encodeStream.Flush();
                    compressedFooterLen = encodeStream.Position - compressedFooterLen;

                    // [Stage 5] Generate final footer
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

                    // [Stage 6] Encode with Base64 and split into 4090B, and write into the script
                    encodeStream.Flush();
                    encodeStream.Position = 0;

                    Encoding encoding = EncodingHelper.DetectBom(backupFile);
                    using (StreamReader tr = new StreamReader(backupFile, encoding, false))
                    using (StreamWriter tw = new StreamWriter(sc.RealPath, false, encoding))
                    {
                        // No need to check existing encoded file section
                        // Fresh write : Fast forward tr, tw to EOF
                        // Overwrite   : Fast forward tr, tw to start of target section
                        IniReadWriter.FastForwardTextWriter(tr, tw, section, false);

                        encodedLen = SplitBase64.Encode(encodeStream, tw, new Progress<long>(x =>
                        {
                            progress?.Report((double)x / compressedBodyLen * Base64ReportFactor + CompReportFactor);
                        }));

                        // Fresh write : Return immediately (tr is already at EOF)
                        // Overwrite   : Copy remaining line to tw from tr
                        IniReadWriter.FastForwardTextWriter(tr, tw, null, true);
                    }
                    progress?.Report(1);
                }

                // [Stage 7] Write to file
                // Write folder info to [EncodedFolders]
                if (!encodeLogo)
                { // "AuthorEncoded" and "InterfaceEncoded" should not be listed here
                    bool writeFolderSection = true;
                    if (sc.Sections.ContainsKey(ScriptSection.Names.EncodedFolders))
                    {
                        string[] folders = sc.Sections[ScriptSection.Names.EncodedFolders].Lines;
                        if (folders.Contains(folderName, StringComparer.OrdinalIgnoreCase))
                            writeFolderSection = false;
                    }

                    if (writeFolderSection &&
                        !folderName.Equals(ScriptSection.Names.AuthorEncoded, StringComparison.OrdinalIgnoreCase) &&
                        !folderName.Equals(ScriptSection.Names.InterfaceEncoded, StringComparison.OrdinalIgnoreCase))
                    { // Guaranteed that folderName does not exist in [EncodedFolders]
                        // Update file
                        IniReadWriter.WriteRawLine(sc.RealPath, ScriptSection.Names.EncodedFolders, folderName, false);
                    }

                }

                // Write file info into [{folderName}]
                IniReadWriter.WriteKey(sc.RealPath, folderName, fileName, $"{inputStream.Length},{encodedLen}"); // UncompressedSize,EncodedSize

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
            {
                if (File.Exists(backupFile))
                    File.Delete(backupFile);
                if (File.Exists(tempCompressed))
                    File.Delete(tempCompressed);
            }

            // [Stage 8] Refresh Script
            sc.RefreshSections();
        }
        #endregion

        #region Decode
        private static long Decode(string scPath, string section, Stream outStream, IProgress<double> progress)
        {
            string tempDecode = FileHelper.GetTempFile(".bin");
            string tempComp = FileHelper.GetTempFile(".bin");
            try
            {
                using (FileStream decodeStream = new FileStream(tempDecode, FileMode.Create, FileAccess.ReadWrite))
                {
                    // [Stage 1] Concat sliced base64-encoded lines into one string
                    int decodeLen;
                    Encoding encoding = EncodingHelper.DetectBom(scPath);
                    using (StreamReader tr = new StreamReader(scPath, encoding))
                    {
                        IniReadWriter.FastForwardTextReader(tr, section);
                        decodeLen = SplitBase64.Decode(tr, decodeStream, new Progress<(int Pos, int Total)>(x =>
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

        #region ReadEncodeMode
        private static EncodeMode ReadEncodeMode(string scPath, string section)
        {
            string tempDecode = FileHelper.GetTempFile();
            try
            {
                using (FileStream decodeStream = new FileStream(tempDecode, FileMode.Create, FileAccess.ReadWrite))
                {
                    // [Stage 1] Concat sliced base64-encoded lines into one string
                    int decodeLen;
                    Encoding encoding = EncodingHelper.DetectBom(scPath);
                    using (StreamReader tr = new StreamReader(scPath, encoding))
                    {
                        IniReadWriter.FastForwardTextReader(tr, section);
                        decodeLen = SplitBase64.Decode(tr, decodeStream);
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

        #region ReadEncodeModeInMem
        private static EncodeMode ReadEncodeModeInMem(string[] encodedLines)
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
    /// <summary>
    /// Memory-efficient 4090-char tokenizing base64 encoder/decoder
    /// </summary>
    public static class SplitBase64
    {
        #region Encode
        public static int Encode(Stream srcStream, TextWriter tw, IProgress<long> progress = null)
        {
            const int reportInterval = 1024 * 1024; // 1MB

            int idx = 0;
            int encodedLen = 0;
            int lineCount = (int)(srcStream.Length * 4 / 3) / 4090;
            tw.Write("lines=");
            tw.WriteLine(lineCount); // lines={count} should be 0-based index

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
                    tw.Write(idx);
                    tw.Write("=");
                    tw.WriteLine(encodedStr.Substring(x * 4090, 4090));
                    idx += 1;
                }

                string lastLine = encodedStr.Substring(encodeBlockLines * 4090);
                if (0 < lastLine.Length && encodeBlockLines < 1024 * 4)
                {
                    tw.Write(idx);
                    tw.Write("=");
                    tw.WriteLine(lastLine);
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
        public static int Decode(TextReader tr, Stream destStream, IProgress<(int Pos, int Total)> progress = null)
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
            while ((line = tr.ReadLine()) != null)
            { // Read text line by line
                line = line.Trim();
                if (line.Length == 0)
                    continue;

                // End of section
                if (line.StartsWith("[", StringComparison.Ordinal) &&
                    line.EndsWith("]", StringComparison.Ordinal))
                    break;

                (string key, string block) = IniReadWriter.GetKeyValueFromLine(line);
                if (key == null || block == null)
                    throw new InvalidOperationException("Encoded lines are malformed");

                // [Stage 1] Get count of lines
                if (key.Equals("lines", StringComparison.OrdinalIgnoreCase))
                {
                    if (!int.TryParse(block, NumberStyles.Integer, CultureInfo.InvariantCulture, out lineCount))
                        return -1; // Failure
                    lineCount += 1; // WB's "lines={count}" is 0-based.
                    continue;
                }

                // [Stage 2] Get length of line
                if (!StringHelper.IsInteger(key))
                    throw new InvalidOperationException("Key of the encoded lines are malformed");
                if (lineLen == -1)
                    lineLen = block.Length;
                if (4090 < block.Length ||
                    i + 1 < lineCount && block.Length != lineLen)
                    throw new InvalidOperationException("Length of encoded lines is inconsistent");

                // [Stage 3] Decode 
                b.Append(block);
                encodeLen += block.Length;

                // If buffer is full, decode ~64KB to ~48KB raw bytes
                // https://github.com/pebakery/pebakery/issues/90
                // lineCount != i + 1 -> if this line is last line, pass this block
                if (lineCount != i + 1 && (i + 1) % 16 == 0)
                {
                    byte[] buffer = Convert.FromBase64String(b.ToString());
                    destStream.Write(buffer, 0, buffer.Length);
                    decodeLen += buffer.Length;
                    b.Clear();
                }

                // Report progress
                if (progress != null && i % 256 == 0)
                    progress.Report((i, lineCount));

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
            destStream.Write(finalBuffer, 0, finalBuffer.Length);

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
                EncodeMode = EncodeMode
            };
        }

        public override string ToString()
        {
            return ScriptSection.Names.GetEncodedSectionName(FolderName, FileName);
        }
    }
    #endregion
}
