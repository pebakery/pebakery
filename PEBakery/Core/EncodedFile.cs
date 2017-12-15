/*
    Copyright (C) 2016-2017 Hajin Jang
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
*/

using Joveler.ZLibWrapper;
using PEBakery.Exceptions;
using PEBakery.Helper;
using PEBakery.IniLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PEBakery.Core
{
    /*
    [Attachment Format]
    Streams are encoded in base64 format.
    Concat all lines into one long string, append '=', '==' or nothing according to length.
    (Need '=' padding to be appended to be .Net acknowledged base64 format)
    Decode base64 encoded string to get binary, which follows these 2 types.
    
    Note)
    All bytes is ordered in little endian
    WB082-generated zlib magic number always starts with 0x78

    [Type 1]
    Zlib Compressed File + Zlib Compressed FirstFooter + Raw FinalFooter
    - Used in most file

    [Type 2]
    Raw File + Zlib Compressed FirstFooter + Raw FinalFooter
    - Used in already compressed file (Ex 7z, zip)

    [FirstFooter]
    550Byte (0x226) (When decompressed)
    0x000 - 0x1FF (512B) -> L-V (Length - Value)
        1B : [Length of FileName]
        511B : [FileName]
    0x200 - 0x207 : 8B  -> Length of Raw File
    0x208 - 0x20F : 8B  -> (Type 1) Length of Zlib-Compressed File, (Type 2) Null-padded
    0x210 - 0x21F : 16B -> Null-padded
    0x220 - 0x223 : 4B  -> CRC32 of Raw File
    0x224         : 1B  -> Compress Mode (Type 1 : 00, Type 2 : 01)
    0x225         : 1B  -> ZLib Compress Level (Type 1 : 01 ~ 09, Type 2 : 00)

    [FinalFooter]
    Not compressed, 36Byte (0x24)
    0x00 - 0x04   : 4B  -> CRC32 of Zlib-Compressed File and Zlib-Compressed FirstFooter
    0x04 - 0x08   : 4B  -> Unknown - Always 1
    0x08 - 0x0B   : 4B  -> WB082 ZLBArchive version - Always 2
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
    
    [How to improve?]
    - Use LZMA instead of zlib, for better compression rate.
    - Design more robust plugin format.
    */

    // Possible zlib stream header
    // https://groups.google.com/forum/#!msg/comp.compression/_y2Wwn_Vq_E/EymIVcQ52cEJ

    #region EncodedFile
    public class EncodedFile
    {
        #region Wrapper Methods
        public enum EncodeMode : byte
        {
            Compress = 0x00, // Type 1 
            Raw = 0x01, // Type 2
        }

        public static Plugin AttachFile(Plugin p, string dirName, string fileName, string srcFilePath, EncodeMode type = EncodeMode.Compress)
        {
            if (p == null) throw new ArgumentNullException("p");

            byte[] input;
            using (FileStream fs = new FileStream(srcFilePath, FileMode.Open, FileAccess.Read))
            {
                input = new byte[fs.Length];
                fs.Read(input, 0, input.Length);
            }
            return Encode(p, dirName, fileName, input, type);
        }

        public static Plugin AttachFile(Plugin p, string dirName, string fileName, Stream srcStream, EncodeMode type = EncodeMode.Compress)
        {
            if (p == null) throw new ArgumentNullException("p");

            byte[] input = new byte[srcStream.Length];
            srcStream.Read(input, 0, input.Length);
            return Encode(p, dirName, fileName, input, type);
        }

        public static MemoryStream ExtractFile(Plugin p, string dirName, string fileName)
        {
            if (p == null) throw new ArgumentNullException("p");

            string section = $"EncodedFile-{dirName}-{fileName}";
            if (p.Sections.ContainsKey(section) == false)
                throw new FileDecodeFailException($"[{dirName}\\{fileName}] does not exists in [{p.FullPath}]");

            List<string> encoded = p.Sections[section].GetLinesOnce();
            return Decode(encoded);
        }

        public static MemoryStream ExtractLogo(Plugin p, out ImageHelper.ImageType type)
        {
            if (p == null) throw new ArgumentNullException("p");

            if (p.Sections.ContainsKey("AuthorEncoded") == false)
                throw new ExtractFileNotFoundException($"There is no AuthorEncoded files");

            Dictionary<string, string> fileDict = p.Sections["AuthorEncoded"].GetIniDict();

            if (fileDict.ContainsKey("Logo") == false)
                throw new ExtractFileNotFoundException($"There is no logo in \'{p.Title}\'");

            string logoFile = fileDict["Logo"];
            if (ImageHelper.GetImageType(logoFile, out type))
                throw new ExtractFileNotFoundException($"Image type of [{logoFile}] is not supported");

            List<string> encoded = p.Sections[$"EncodedFile-AuthorEncoded-{logoFile}"].GetLinesOnce();
            return Decode(encoded);
        }

        public static MemoryStream ExtractInterfaceEncoded(Plugin p, string fileName)
        {
            string section = $"EncodedFile-InterfaceEncoded-{fileName}";
            if (p.Sections.ContainsKey(section) == false)
                throw new FileDecodeFailException($"[InterfaceEncoded\\{fileName}] does not exists in [{p.FullPath}]");

            List<string> encoded = p.Sections[section].GetLinesOnce();
            return Decode(encoded);
        }
        #endregion

        #region Encode, Decode
        private static Plugin Encode(Plugin p, string dirName, string fileName, byte[] input, EncodeMode mode)
        {
            byte[] fileNameUTF8 = Encoding.UTF8.GetBytes(fileName);
            if (fileName.Length == 0 || 512 <= fileNameUTF8.Length)
            {
                throw new FileDecodeFailException($"Filename's length should be lower than 512B when UTF8 encoded");
            }

            // Check Overwrite
            bool fileOverwrite = false;
            if (p.Sections.ContainsKey(dirName))
            { // [{dirName}] section exists, check if there is already same file encoded
                List<string> lines = p.Sections[dirName].GetLines();
                if (lines.FirstOrDefault(x => x.Equals(fileName, StringComparison.OrdinalIgnoreCase)) != null)
                    fileOverwrite = true;
            }

            string encodedStr;
            using (MemoryStream bodyStream = new MemoryStream())
            using (MemoryStream footerStream = new MemoryStream())
            using (MemoryStream concatStream = new MemoryStream())
            {
                // [Stage 1] Compress file with zlib
                switch (mode)
                {
                    case EncodeMode.Compress:
                        {
                            using (ZLibStream zs = new ZLibStream(bodyStream, CompressionMode.Compress, CompressionLevel.Level6, true))
                            {
                                zs.Write(input, 0, input.Length);
                            }

                            bodyStream.Position = 0;
                        }
                        break;
                    case EncodeMode.Raw:
                        {
                            bodyStream.Write(input, 0, input.Length);
                            bodyStream.Position = 0;
                        }
                        break;
                    default:
                        throw new InternalException($"Wrong EncodeMode [{mode}]");
                }

                // [Stage 2] Generate first footer
                byte[] rawFooter = new byte[0x226]; // 0x550
                {
                    // 0x000 - 0x1FF : Filename and its length
                    rawFooter[0] = (byte)fileNameUTF8.Length;
                    fileNameUTF8.CopyTo(rawFooter, 1);
                    for (int i = 1 + fileNameUTF8.Length; i < 0x200; i++)
                        rawFooter[i] = 0; // Null Pad
                    // 0x200 - 0x207 : 8B -> Length of raw file, in little endian
                    BitConverter.GetBytes(input.Length).CopyTo(rawFooter, 0x200);
                    switch (mode)
                    {
                        case EncodeMode.Compress: // Type 1
                            // 0x208 - 0x20F : 8B -> Length of zlibed body, in little endian
                            BitConverter.GetBytes(bodyStream.Length).CopyTo(rawFooter, 0x208);
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
                    uint crc32 = Crc32Checksum.Crc32(input);
                    BitConverter.GetBytes(crc32).CopyTo(rawFooter, 0x220);
                    // 0x224         : 1B -> Compress Mode (Type 1 : 00, Type 2 : 01)
                    rawFooter[0x224] = (byte) mode;
                    // 0x225         : 1B -> ZLib Compress Level (Type 1 : 01 ~ 09, Type 2 : 00)
                    switch (mode)
                    {
                        case EncodeMode.Compress: // Type 1
                            rawFooter[0x225] = (byte)CompressionLevel.Level6;
                            break;
                        case EncodeMode.Raw: // Type 2
                            rawFooter[0x225] = 0;
                            break;
                        default:
                            throw new InternalException($"Wrong EncodeMode [{mode}]");
                    }
                }

                // [Stage 3] Compress first footer
                using (ZLibStream zs = new ZLibStream(footerStream, CompressionMode.Compress, CompressionLevel.Default, true))
                {
                    zs.Write(rawFooter, 0, rawFooter.Length);
                }
                footerStream.Position = 0;

                // [Stage 4] Concat body and footer
                bodyStream.CopyTo(concatStream);
                footerStream.CopyTo(concatStream);
                bodyStream.Position = 0;
                footerStream.Position = 0;

                // [Stage 5] Generate final footer
                {
                    byte[] finalFooter = new byte[0x24];

                    // 0x00 - 0x04 : 4B -> CRC32 of compressed body and compressed footer
                    uint crc32 = Crc32Checksum.Crc32(concatStream.ToArray());
                    BitConverter.GetBytes(crc32).CopyTo(finalFooter, 0x00);
                    // 0x04 - 0x08 : 4B -> Unknown - Always 1
                    BitConverter.GetBytes((uint)1).CopyTo(finalFooter, 0x04);
                    // 0x08 - 0x0B : 4B -> ZLBArchive version (Always 2)
                    BitConverter.GetBytes((uint)2).CopyTo(finalFooter, 0x08);
                    // 0x0C - 0x0F : 4B -> Zlib Compressed Footer Length
                    BitConverter.GetBytes((int)footerStream.Length).CopyTo(finalFooter, 0x0C);
                    // 0x10 - 0x17 : 8B -> Zlib Compressed File Length
                    BitConverter.GetBytes(bodyStream.Length).CopyTo(finalFooter, 0x10);
                    // 0x18 - 0x1B : 4B -> Unknown - Always 1
                    BitConverter.GetBytes((uint)1).CopyTo(finalFooter, 0x18);
                    // 0x1C - 0x23 : 8B -> Unknown - Always 0
                    for (int i = 0x1C; i < 0x24; i++)
                        finalFooter[i] = 0;

                    concatStream.Write(finalFooter, 0, finalFooter.Length);
                }

                // [Stage 6] Encode body, footer and finalFooter with Base64
                encodedStr = Convert.ToBase64String(concatStream.ToArray());
                // Remove Base64 Padding (==, =)
                if (encodedStr.EndsWith("==", StringComparison.Ordinal))
                    encodedStr = encodedStr.Substring(0, encodedStr.Length - 2);
                else if (encodedStr.EndsWith("=", StringComparison.Ordinal))
                    encodedStr = encodedStr.Substring(0, encodedStr.Length - 1);
            }

            // [Stage 7] Tokenize encoded string into 4090B.
            string section = $"EncodedFile-{dirName}-{fileName}";
            List<IniKey> keys = new List<IniKey>();
            for (int i = 0; i <= (encodedStr.Length / 4090); i++)
            {
                if (i < (encodedStr.Length / 4090)) // 1 Line is 4090 characters
                {
                    keys.Add(new IniKey(section, i.ToString(), encodedStr.Substring(i * 4090, 4090))); // X=eJyFk0Fr20AQhe8G...
                }
                else // Last Iteration
                {
                    keys.Add(new IniKey(section, i.ToString(), encodedStr.Substring(i * 4090, encodedStr.Length - (i * 4090)))); // X=N3q8ryccAAQWuBjqA5QvAAAAAA (end)
                    keys.Insert(0, new IniKey(section, "lines", i.ToString())); // lines=X
                }
            }

            // [Stage 8] Before writing to file, backup original plugin
            string tempFile = Path.GetTempFileName();
            File.Copy(p.FullPath, tempFile, true);

            // [Stage 9] Write to file
            try
            {
                // Write folder info to [EncodedFolders]
                Ini.WriteRawLine(p.FullPath, "EncodedFolders", dirName, false);

                // Write file info into [{dirName}]
                Ini.SetKey(p.FullPath, dirName, fileName, $"{input.Length},{encodedStr.Length}"); // UncompressedSize,EncodedSize

                // Write encoded file into [EncodedFile-{dirName}-{fileName}]
                if (fileOverwrite)
                    Ini.DeleteSection(p.FullPath, section); // Delete existing encoded file
                Ini.SetKeys(p.FullPath, keys); // Write into 
            }
            catch
            { // Error -> Rollback!
                File.Copy(tempFile, p.FullPath, true);
                throw new FileDecodeFailException($"Error while writing encoded file into [{p.FullPath}]");
            }
            finally
            { // Delete temp script
                File.Delete(tempFile);
            }

            // [Stage 10] Refresh Plugin
            return p.Project.RefreshPlugin(p);
        }

        private static MemoryStream Decode(List<string> encodedList)
        {
            if (Ini.GetKeyValueFromLine(encodedList[0], out string key, out string value))
                throw new FileDecodeFailException("Encoded lines are malformed");

            // [Stage 1] Concat sliced base64-encoded lines into one string
            byte[] decoded;
            {
                int.TryParse(value, out int blockCount);
                encodedList.RemoveAt(0); // Remove "lines=n"

                // Each line is 64KB block
                if (Ini.GetKeyValueFromLines(encodedList, out List<string> keys, out List<string> base64Blocks))
                    throw new FileDecodeFailException("Encoded lines are malformed");

                StringBuilder b = new StringBuilder();
                foreach (string block in base64Blocks)
                    b.Append(block);
                switch (b.Length % 4)
                {
                    case 0:
                        break;
                    case 1:
                        throw new FileDecodeFailException("Encoded lines are malformed");
                    case 2:
                        b.Append("==");
                        break;
                    case 3:
                        b.Append("=");
                        break;
                }

                decoded = Convert.FromBase64String(b.ToString());
            }

            // [Stage 2] Read final footer
            const int finalFooterLen = 0x24;
            int finalFooterIdx = decoded.Length - finalFooterLen;
            // 0x00 - 0x04 : 4B -> CRC32
            uint full_crc32 = BitConverter.ToUInt32(decoded, finalFooterIdx + 0x00);
            // 0x0C - 0x0F : 4B -> Zlib Compressed Footer Length
            int compressedFooterLen = (int) BitConverter.ToUInt32(decoded, finalFooterIdx + 0x0C);
            int compressedFooterIdx = decoded.Length - (finalFooterLen + compressedFooterLen);
            // 0x10 - 0x17 : 8B -> Zlib Compressed File Length
            int compressedBodyLen = (int) BitConverter.ToUInt64(decoded, finalFooterIdx + 0x10);

            // [Stage 3] Validate final footer
            if (compressedBodyLen != compressedFooterIdx)
                throw new FileDecodeFailException($"Encoded file is corrupted");
            uint calcFull_crc32 = Crc32Checksum.Crc32(decoded, 0, finalFooterIdx);
            if (full_crc32 != calcFull_crc32)
                throw new FileDecodeFailException($"Encoded file is corrupted");

            // [Stage 4] Decompress first footer
            byte[] rawFooter;
            using (MemoryStream rawFooterStream = new MemoryStream())
            {
                using (MemoryStream ms = new MemoryStream(decoded, compressedFooterIdx, compressedFooterLen))
                using (ZLibStream zs = new ZLibStream(ms, CompressionMode.Decompress, CompressionLevel.Default))
                {
                    zs.CopyTo(rawFooterStream);
                }

                rawFooter = rawFooterStream.ToArray();
            }

            // [Stage 5] Read first footer
            // 0x200 - 0x207 : 8B -> Length of raw file, in little endian
            int rawBodyLen = (int)BitConverter.ToUInt32(rawFooter, 0x200);
            // 0x208 - 0x20F : 8B -> Length of zlib-compressed file, in little endian
            //     Note: In Type 2, 0x208 entry is null - padded
            int compressedBodyLen2 = (int)BitConverter.ToUInt32(rawFooter, 0x208);
            // 0x220 - 0x223 : 4B -> CRC32C Checksum of zlib-compressed file
            uint compressedBody_crc32 = BitConverter.ToUInt32(rawFooter, 0x220);
            // 0x224         : 1B -> Compress Mode (Type 1 : 00, Type 2 : 01)
            byte compMode = rawFooter[0x224];
            // 0x225         : 1B -> ZLib Compress Level (Type 1 : 01~09, Type 2 : 00)
            byte compLevel = rawFooter[0x225];

            // [Stage 6] Validate first footer
            if (compMode == 0) // Type 1, zlib
            {
                if (compressedBodyLen2 == 0 || (compressedBodyLen2 != compressedBodyLen))
                    throw new FileDecodeFailException($"Encoded file is corrupted: compMode");
                if (compLevel < 1 || 9 < compLevel)
                    throw new FileDecodeFailException($"Encoded file is corrupted: compLevel");
            }
            else if (compMode == 1) // Type 2, Raw
            {
                if (compressedBodyLen2 != 0)
                    throw new FileDecodeFailException($"Encoded file is corrupted: compMode");
                if (compLevel != 0)
                    throw new FileDecodeFailException($"Encoded file is corrupted: compLevel");
            }
            else // Wrong compMode
            {
                throw new FileDecodeFailException($"Encoded file is corrupted: compMode");
            }

            // [Stage 7] Decompress body
            MemoryStream rawBodyStream; // This stream should be alive even after this method returns
            if (compMode == 0) // Type 1, zlib
            {
                rawBodyStream = new MemoryStream();

                using (MemoryStream ms = new MemoryStream(decoded, 0, compressedBodyLen))
                using (ZLibStream zs = new ZLibStream(ms, CompressionMode.Decompress, false))
                {
                    zs.CopyTo(rawBodyStream);
                }

                rawBodyStream.Position = 0;
            }
            else if (compMode == 1)  // Type 2, raw
            {
                rawBodyStream = new MemoryStream(decoded, 0, rawBodyLen);
            }
            else
            {
                throw new FileDecodeFailException($"Encoded file is corrupted");
            }

            // [Stage 8] Validate decompressed body
            uint calcCompBody_crc32 = Crc32Checksum.Crc32(rawBodyStream.ToArray());
            if (compressedBody_crc32 != calcCompBody_crc32)
                throw new FileDecodeFailException($"Encoded file is corrupted");

            // [Stage 9] Return decompressed body stream
            rawBodyStream.Position = 0;
            return rawBodyStream;
        }
        #endregion
    }
    #endregion

    #region EncodedFileInfo
    /// <summary>
    /// Class to handle malformed WB082-attached files
    /// </summary>
    public class EncodedFileInfo : IDisposable
    {
        public EncodedFile.EncodeMode? Mode;
        public bool? RawBodyValid = null; // null -> unknown
        public bool? CompressedBodyValid = null; // Adler32 Checksum
        public bool? FirstFooterValid = null;
        public bool? FinalFooterValid = null;
        public MemoryStream RawBodyStream = null;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (RawBodyStream != null)
                    RawBodyStream.Close();
            }
        }

        public EncodedFileInfo(Plugin p, string dirName, string fileName)
        {
            string section = $"EncodedFile-{dirName}-{fileName}";
            if (p.Sections.ContainsKey(section) == false)
                throw new FileDecodeFailException($"[{dirName}\\{fileName}] does not exists in [{p.FullPath}]");

            List<string> encodedList = p.Sections[$"EncodedFile-{dirName}-{fileName}"].GetLinesOnce();
            if (Ini.GetKeyValueFromLine(encodedList[0], out string key, out string value))
                throw new FileDecodeFailException("Encoded lines are malformed");

            // [Stage 1] Concat sliced base64-encoded lines into one string
            byte[] decoded;
            {
                int.TryParse(value, out int blockCount);
                encodedList.RemoveAt(0); // Remove "lines=n"

                // Each line is 64KB block
                if (Ini.GetKeyValueFromLines(encodedList, out List<string> keys, out List<string> base64Blocks))
                    throw new FileDecodeFailException("Encoded lines are malformed");

                StringBuilder b = new StringBuilder();
                foreach (string block in base64Blocks)
                    b.Append(block);
                switch (b.Length % 4)
                {
                    case 0:
                        break;
                    case 1:
                        throw new FileDecodeFailException("Encoded lines are malformed");
                    case 2:
                        b.Append("==");
                        break;
                    case 3:
                        b.Append("=");
                        break;
                }

                decoded = Convert.FromBase64String(b.ToString());
            }

            // [Stage 2] Read final footer
            const int finalFooterLen = 0x24;
            int finalFooterIdx = decoded.Length - finalFooterLen;
            // 0x00 - 0x04 : 4B -> CRC32
            uint full_crc32 = BitConverter.ToUInt32(decoded, finalFooterIdx + 0x00);
            // 0x0C - 0x0F : 4B -> Zlib Compressed Footer Length
            int compressedFooterLen = (int)BitConverter.ToUInt32(decoded, finalFooterIdx + 0x0C);
            int compressedFooterIdx = decoded.Length - (finalFooterLen + compressedFooterLen);
            // 0x10 - 0x17 : 8B -> Zlib Compressed File Length
            int compressedBodyLen = (int)BitConverter.ToUInt64(decoded, finalFooterIdx + 0x10);

            // [Stage 3] Validate final footer
            this.FinalFooterValid = true;
            if (compressedBodyLen != compressedFooterIdx)
                this.FinalFooterValid = false;
            uint calcFull_crc32 = Crc32Checksum.Crc32(decoded, 0, finalFooterIdx);
            if (full_crc32 != calcFull_crc32)
                this.FinalFooterValid = false;

            if (this.FinalFooterValid == false)
                return;


            // [Stage 4] Decompress first footer
            byte[] rawFooter;
            using (MemoryStream rawFooterStream = new MemoryStream())
            {
                using (MemoryStream ms = new MemoryStream(decoded, compressedFooterIdx, compressedFooterLen))
                using (ZLibStream zs = new ZLibStream(ms, CompressionMode.Decompress, CompressionLevel.Default))
                {
                    zs.CopyTo(rawFooterStream);
                }

                rawFooter = rawFooterStream.ToArray();
            }

            // [Stage 5] Read first footer
            this.FirstFooterValid = true;
            // 0x200 - 0x207 : 8B -> Length of raw file, in little endian
            int rawBodyLen = (int)BitConverter.ToUInt32(rawFooter, 0x200);
            // 0x208 - 0x20F : 8B -> Length of zlib-compressed file, in little endian
            //     Note: In Type 2, 0x208 entry is null - padded
            int compressedBodyLen2 = (int)BitConverter.ToUInt32(rawFooter, 0x208);
            // 0x220 - 0x223 : 4B -> CRC32C Checksum of zlib-compressed file
            uint compressedBody_crc32 = BitConverter.ToUInt32(rawFooter, 0x220);
            // 0x224         : 1B -> Compress Mode (Type 1 : 00, Type 2 : 01)
            byte compMode = rawFooter[0x224];
            // 0x225         : 1B -> ZLib Compress Level (Type 1 : 01~09, Type 2 : 00)
            byte compLevel = rawFooter[0x225];

            // [Stage 6] Validate first footer
            if (compMode == 0)
            {
                this.Mode = EncodedFile.EncodeMode.Compress;
                if (compLevel < 1 || 9 < compLevel)
                    this.FirstFooterValid = false;
                if (compressedBodyLen2 == 0 || (compressedBodyLen2 != compressedBodyLen))
                    this.FirstFooterValid = false;
                
            }
            else if (compMode == 1)
            {
                this.Mode = EncodedFile.EncodeMode.Raw;
                if (compLevel != 0)
                    this.FirstFooterValid = false;
                if (compressedBodyLen2 != 0)
                    this.FirstFooterValid = false;
            }
            else // Wrong compMode
            {
                this.FirstFooterValid = false;
            }

            if (this.FirstFooterValid == false)
                return;

            // [Stage 7] Decompress body
            if (compMode == (ushort)EncodedFile.EncodeMode.Compress)
            {
                this.RawBodyStream = new MemoryStream();

                using (MemoryStream ms = new MemoryStream(decoded, 0, compressedBodyLen))
                using (ZLibStream zs = new ZLibStream(ms, CompressionMode.Decompress, CompressionLevel.Default))
                {
                    zs.CopyTo(this.RawBodyStream);
                }

                this.RawBodyStream.Position = 0;
                this.CompressedBodyValid = true;
            }
            else if (compMode == (ushort)EncodedFile.EncodeMode.Raw)
            {
                this.CompressedBodyValid = true;
                this.RawBodyStream = new MemoryStream(decoded, 0, rawBodyLen);
            }
            else
            {
                throw new InternalException($"Wrong EncodeMode [{compMode}]");
            }

            // [Stage 8] Validate decompressed body
            this.RawBodyValid = true;
            uint calcCompBody_crc32 = Crc32Checksum.Crc32(RawBodyStream.ToArray());
            if (compressedBody_crc32 != calcCompBody_crc32)
                this.RawBodyValid = false;

            // [Stage 9] Return decompressed body stream
            this.RawBodyStream.Position = 0;
        }
    }
#endregion
}
