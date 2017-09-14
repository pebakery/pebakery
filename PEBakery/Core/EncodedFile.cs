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

using PEBakery.Exceptions;
using PEBakery.Helper;
using PEBakery.Lib;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PEBakery.Core
{
    /*
    [Attachment Format]
    Streams are encoded in base64 format.
    Concat all lines into one long string, append '=', '==' or nothing according to length.
    (Need '=' padding to be appended to be .Net acknowledged base64 format)
    Decode base64 encoded string to get binary, which follows these 2 types
    
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
    0x200 - 0x207 : 8B -> Length of Raw File
    0x208 - 0x20F : 8B -> Length of Zlib-Compressed File
        Note : In Type 2, 0x208 entry is null-padded
    0x210 - 0x21F : 16B -> Null-padded
    0x220 - 0x223 : 4B -> CRC32 of raw file
    0x224 - 0x225 : 2B -> (Type 1) 00 02, (Type 2) 01 00

    [FinalFooter]
    Not compressed, 36Byte (0x24)
    0x00 - 0x04 : 4B -> CRC32 of Zlib-Compressed File and Zlib-Compressed FirstFooter
    0x04 - 0x08 : 4B -> Unknown - Always 1 
    0x08 - 0x0B : 4B -> WB082 ZLBArchive version - Always 2
    0x0C - 0x0F : 4B -> Zlib Compressed FirstFooter Length
    0x10 - 0x17 : 8B -> Zlib Compressed File Length
    0x18 - 0x1B : 4B -> Unknown - Always 1 
    0x1C - 0x23 : 8B -> Unknown - Always 0
    
    Note) Which purpose do Unknown entries have?
    0x04 : When changed, WB082 cannot recognize filename. Maybe related to filename encoding?
    0x18 : Decompress by WB082 is unaffected by this value
    0x1C : When changed, WB082 think the encoded file is corrupted
    
    [Note]
    "The archive was created with a different version of ZLBArchive (vXXXXXX)" error message appear if final footer is malformed.
    From this message, we can suspect WB082 writes its ZLBArchive version in some places.
    The version can be v1.2 or v1.0

    [How to improve?]
    - Use LZMA instead of zlib, for better compression rate.
    - Design more robust plugin format.
    */

    public class EncodedFile
    {
        #region Wrapper Methods
        public enum EncodeMode : ushort
        {
            Compress = 0x0200, // Type 1
            Raw = 0x0001, // Type 2
        }

        public static Plugin AttachFile(Plugin p, string dirName, string fileName, string srcFilePath, EncodeMode type = EncodeMode.Compress)
        {
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
            byte[] input = new byte[srcStream.Length];
            srcStream.Read(input, 0, input.Length);
            return Encode(p, dirName, fileName, input, type);
        }

        /// <summary>
        /// Return true if failed
        /// </summary>
        /// <param name="plugin"></param>
        /// <param name="dirName"></param>
        /// <param name="fileName"></param>
        /// <param name="mem"></param>
        /// <returns></returns>
        public static MemoryStream ExtractFile(Plugin plugin, string dirName, string fileName)
        {
            List<string> encoded = plugin.Sections[$"EncodedFile-{dirName}-{fileName}"].GetLinesOnce();
            return Decode(encoded);
        }

        /// <summary>
        /// Return true if failed
        /// </summary>
        /// <param name="plugin"></param>
        /// <param name="mem"></param>
        /// <returns></returns>
        public static MemoryStream ExtractLogo(Plugin plugin, out ImageHelper.ImageType type)
        {
            type = ImageHelper.ImageType.Bmp; // Dummy
            if (plugin.Sections.ContainsKey("AuthorEncoded") == false)
                throw new ExtractFileNotFoundException($"There is no encoded file by author");
            Dictionary<string, string> fileDict = plugin.Sections["AuthorEncoded"].GetIniDict();
            if (fileDict.ContainsKey("Logo") == false)
                throw new ExtractFileNotFoundException($"There is no logo in \'{plugin.Title}\'");
            string logoFile = fileDict["Logo"];
            if (ImageHelper.GetImageType(logoFile, out type))
                throw new ExtractFileNotFoundException("Unsupported image type");
            List<string> encoded = plugin.Sections[$"EncodedFile-AuthorEncoded-{logoFile}"].GetLinesOnce();
            return Decode(encoded);
        }

        /// <summary>
        /// Return true if failed
        /// </summary>
        /// <param name="p"></param>
        /// <param name="mem"></param>
        /// <returns></returns>
        public static MemoryStream ExtractInterfaceEncoded(Plugin p, string fileName)
        {
            List<string> encoded = p.Sections[$"EncodedFile-InterfaceEncoded-{fileName}"].GetLinesOnce();
            return Decode(encoded);
        }
        #endregion

        #region Encode, Decode
        private static byte[,] zlibHeader = new byte[4, 2]
        { // https://groups.google.com/forum/#!msg/comp.compression/_y2Wwn_Vq_E/EymIVcQ52cEJ
            { 0x78, 0x01 },
            { 0x78, 0x5e },
            { 0x78, 0x9c },
            { 0x78, 0xda },
        };

        private static bool IsZlibHeader(byte[] bin, int idx)
        {
            bool result = false;
            for (int i = 0; i < zlibHeader.GetLength(0); i++)
            {
                if (bin[idx] == zlibHeader[i, 0] &&
                    bin[idx + 1] == zlibHeader[i, 1])
                    result = true;
            }
            return result;
        }

        private static Plugin Encode(Plugin p, string dirName, string fileName, byte[] input, EncodeMode mode)
        {
            byte[] fileNameUTF8 = Encoding.UTF8.GetBytes(fileName);
            if (fileName.Length == 0 || 512 <= fileNameUTF8.Length)
            {
                throw new EncodedFileFailException($"Filename's length should be lower than 512B when UTF8 encoded");
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
                            using (ZlibStream zs = new ZlibStream(bodyStream, CompressionMode.Compress, CompressionLevel.Default, true, Encoding.UTF8))
                            {
                                zs.Write(input, 0, input.Length);
                                zs.Close();

                                bodyStream.Position = 0;
                            }
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
                    uint crc32c = Force.Crc32.Crc32Algorithm.Compute(input);
                    BitConverter.GetBytes(crc32c).CopyTo(rawFooter, 0x220);
                    // 0x224 - 0x225 : Footer Type 
                    BitConverter.GetBytes((ushort)mode).CopyTo(rawFooter, 0x224);
                }

                // [Stage 3] Compress first footer
                using (ZlibStream zs = new ZlibStream(footerStream, CompressionMode.Compress, CompressionLevel.Default, true, Encoding.UTF8))
                {
                    zs.Write(rawFooter, 0, rawFooter.Length);
                    zs.Close();

                    footerStream.Position = 0;
                }

                // [Stage 4] Concat body and footer
                bodyStream.CopyTo(concatStream);
                footerStream.CopyTo(concatStream);
                bodyStream.Position = 0;
                footerStream.Position = 0;

                // [Stage 5] Generate final footer
                {
                    byte[] finalFooter = new byte[0x24];

                    // 0x00 - 0x04 : 4B -> CRC32 of compressed body and compressed footer
                    uint crc32 = Force.Crc32.Crc32Algorithm.Compute(concatStream.ToArray());
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
                throw new EncodedFileFailException($"Error while writing encoded file into [{p.FullPath}]");
            }
            finally
            { // Delete temp script
                File.Delete(tempFile);
            }

            // [Stage 10] Refresh Plugin
            // TODO: How to update CurMainTree of MainWindows?
            return p.Project.RefreshPlugin(p);
        }

        private static MemoryStream Decode(List<string> encodedList)
        {
            if (Ini.GetKeyValueFromLine(encodedList[0], out string key, out string value))
                throw new EncodedFileFailException("Encoded lines are malformed");

            // [Stage 1] Concat sliced base64-encoded lines into one string
            byte[] decoded;
            {
                int.TryParse(value, out int blockCount);
                encodedList.RemoveAt(0); // Remove "lines=n"

                // Each line is 64KB block
                if (Ini.GetKeyValueFromLines(encodedList, out List<string> keys, out List<string> base64Blocks))
                    throw new EncodedFileFailException("Encoded lines are malformed");

                StringBuilder b = new StringBuilder();
                foreach (string block in base64Blocks)
                    b.Append(block);
                switch (b.Length % 4)
                {
                    case 0:
                        break;
                    case 1:
                        throw new EncodedFileFailException("Encoded lines are malformed");
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
                throw new EncodedFileFailException($"Encoded file is corrupted");
            uint calcFull_crc32 = Force.Crc32.Crc32Algorithm.Compute(decoded, 0, finalFooterIdx);
            if (full_crc32 != calcFull_crc32)
                throw new EncodedFileFailException($"Encoded file is corrupted");

            // [Stage 4] Decompress first footer
            byte[] rawFooter;
            using (MemoryStream rawFooterStream = new MemoryStream())
            {
                using (MemoryStream ms = new MemoryStream(decoded, compressedFooterIdx, compressedFooterLen))
                using (ZlibStream zs = new ZlibStream(ms, CompressionMode.Decompress, CompressionLevel.Default, false, Encoding.UTF8))
                {
                    zs.CopyTo(rawFooterStream);
                    rawFooter = rawFooterStream.ToArray();
                    zs.Close();
                }
            }

            // [Stage 5] Read first footer
            // 0x200 - 0x207 : 8B -> Length of raw file, in little endian
            int rawBodyLen = (int)BitConverter.ToUInt32(rawFooter, 0x200);
            // 0x208 - 0x20F : 8B -> Length of zlib-compressed file, in little endian
            //     Note: In Type 2, 0x208 entry is null - padded
            int compressedBodyLen2 = (int)BitConverter.ToUInt32(rawFooter, 0x208);
            // 0x220 - 0x223 : 4B -> CRC32C Checksum of zlib-compressed file
            uint compressedBody_crc32 = BitConverter.ToUInt32(rawFooter, 0x220);
            // 0x224 - 0x225 : 2B -> (Type 1) 01 00, (Type 2) 00 02
            ushort compMode = BitConverter.ToUInt16(rawFooter, 0x224);

            // [Stage 6] Validate first footer
            if (compMode == (ushort)EncodeMode.Compress)
            {
                if (compressedBodyLen2 == 0 || (compressedBodyLen2 != compressedBodyLen))
                    throw new EncodedFileFailException($"Encoded file is corrupted");
            }
            else if (compMode == (ushort)EncodeMode.Raw)
            {
                if (compressedBodyLen2 != 0)
                    throw new EncodedFileFailException($"Encoded file is corrupted");
            }
            else // Wrong compMode
                throw new EncodedFileFailException($"Encoded file is corrupted");

            // [Stage 7] Decompress body
            MemoryStream rawBodyStream; // This stream should be alive even after this method returns
            if (compMode == (ushort) EncodeMode.Compress)
            {
                rawBodyStream = new MemoryStream();

                using (MemoryStream ms = new MemoryStream(decoded, 0, compressedBodyLen))
                using (ZlibStream zs = new ZlibStream(ms, CompressionMode.Decompress, CompressionLevel.Default, false, Encoding.UTF8))
                {
                    zs.CopyTo(rawBodyStream);
                    zs.Close();

                    rawBodyStream.Position = 0;
                }
  
            }
            else if (compMode == (ushort)EncodeMode.Raw)
            {
                rawBodyStream = new MemoryStream(decoded, 0, rawBodyLen);
            }
            else
            {
                throw new EncodedFileFailException($"Encoded file is corrupted");
            }

            // [Stage 8] Validate decompressed body
            uint calcCompBody_crc32 = Force.Crc32.Crc32Algorithm.Compute(rawBodyStream.ToArray());
            if (compressedBody_crc32 != calcCompBody_crc32)
                throw new EncodedFileFailException($"Encoded file is corrupted");

            // [Stage 9] Return decompressed body stream
            rawBodyStream.Position = 0;
            return rawBodyStream;
        }
        #endregion

        #region Will-be-deprecated
        private static MemoryStream Decode_Type1_DotNetDeflate(List<string> encodedList)
        {
            // [이 메소드를 아직 못 지우는 이유]
            //
            // 특정 파일들은 시공의 폭풍에라도 빨려 들어갔는지 확률적으로 FinalFooter가 깨진다.
            //   Ex) Korean IME의 업로드 버튼 - 다른 스크립트와 같은 Base64 string이지만 여기서만 FinalFooter가 깨진다.
            // 특정 파일들은 WB082에서 업로드한 상태 그대로 압축 해제를 시도할 때 SharpCompress에서 Exception이 터진다.
            //   Ex) PEBakeryAlphaMemory.jpg
            // 
            // 원인이 대체 뭐지?

            if (Ini.GetKeyValueFromLine(encodedList[0], out string key, out string value))
                throw new EncodedFileFailException("Encoded lines are malformed");

            int.TryParse(value, out int blockCount);
            encodedList.RemoveAt(0); // Remove "lines=n"

            // Each line is 64KB block
            if (Ini.GetKeyValueFromLines(encodedList, out List<string> keys, out List<string> base64Blocks))
                throw new EncodedFileFailException("Encoded lines are malformed");
            keys = null; // Please GC this

            byte[] decoded;
            {
                StringBuilder b = new StringBuilder();
                foreach (string block in base64Blocks)
                    b.Append(block);

                switch (b.Length % 4)
                {
                    case 0:
                    case 1:
                        break;
                    case 2:
                        b.Append("==");
                        break;
                    case 3:
                        b.Append("=");
                        break;
                }

                string encoded = b.ToString();
                decoded = Convert.FromBase64String(encoded);
            }

            // Type 1, encoded with Zlib. 
            MemoryStream rawBodyStream = new MemoryStream();
            using (MemoryStream ms = new MemoryStream(decoded, 2, decoded.Length))
            using (DeflateStream zs = new DeflateStream(ms, CompressionMode.Decompress))
            {
                zs.CopyTo(rawBodyStream);
                zs.Close();
            }

            rawBodyStream.Position = 0;
            return rawBodyStream;
        }

        private static MemoryStream Decode_OldMethod(List<string> encodedList)
        {
            if (Ini.GetKeyValueFromLine(encodedList[0], out string key, out string value))
                throw new EncodedFileFailException("Encoded lines are malformed");

            int.TryParse(value, out int blockCount);
            encodedList.RemoveAt(0); // Remove "lines=n"

            // Each line is 64KB block
            if (Ini.GetKeyValueFromLines(encodedList, out List<string> keys, out List<string> base64Blocks))
                throw new EncodedFileFailException("Encoded lines are malformed");
            keys = null; // Please GC this

            StringBuilder builder = new StringBuilder();
            foreach (string block in base64Blocks)
                builder.Append(block);

            switch (builder.Length % 4)
            {
                case 0:
                case 1:
                    break;
                case 2:
                    builder.Append("==");
                    break;
                case 3:
                    builder.Append("=");
                    break;
            }

            MemoryStream mem = null;
            string encoded = builder.ToString();
            builder = null; // Please GC this
            byte[] decoded = Convert.FromBase64String(encoded);
            encoded = null; // Please GC this
            if (decoded[0] == 0x78 && decoded[1] == 0x01 || // No compression
                decoded[0] == 0x78 && decoded[1] == 0x9C || // Default compression
                decoded[0] == 0x78 && decoded[1] == 0xDA) // Best compression
            { // Type 1, encoded with Zlib. 
                using (MemoryStream zlibMem = new MemoryStream(decoded))
                {
                    decoded = null;
                    // Remove zlib magic number, converting to deflate data stream
                    zlibMem.ReadByte(); // 0x78
                    zlibMem.ReadByte(); // 0x9c

                    mem = new MemoryStream();
                    // DeflateStream internally use zlib library, starting from .Net 4.5
                    using (DeflateStream zlibStream = new DeflateStream(zlibMem, CompressionMode.Decompress))
                    {
                        mem.Position = 0;
                        zlibStream.CopyTo(mem);
                        zlibStream.Close();
                    }
                }
            }
            else
            { // Type 2, for already compressed file
                // Main file : encoded without zlib
                // Metadata at footer : zlib compressed -> do not used. Maybe for integrity purpose?
                bool failure = true;
                for (int i = decoded.Length - 1; 0 < i; i--)
                {
                    if (decoded[i - 1] == 0x78 && decoded[i] == 0x01 || // No compression
                        decoded[i - 1] == 0x78 && decoded[i] == 0x9C || // Default compression
                        decoded[i - 1] == 0x78 && decoded[i] == 0xDA) // Best compression
                    { // Found footer zlib stream
                        int idx = i - 1;
                        byte[] body = decoded.Take(idx).ToArray();
                        mem = new MemoryStream(body);
                        failure = false;
                        break;
                    }
                }
                if (failure)
                    throw new EncodedFileFailException("Extract failed");
            }

            return mem;
        }
        #endregion
    }
}
