/*
    Copyright (C) 2017 Hajin Jang
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

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.Core;
using System.IO;
using System.Linq;
using PEBakery.Helper;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using PEBakery.Exceptions;
using System.Text;
using Joveler.ZLibWrapper;
using PEBakery.IniLib;
using PEBakery.LZ4Lib;
using PEBakery.XZLib;

namespace PEBakery.Tests.Core
{
    [TestClass]
    public class EncodedFileTests
    {
        #region Const Strings, String Factory
        private const string EncodedFolders = "EncodedFolders";
        private const string AuthorEncoded = "AuthorEncoded";
        private const string InterfaceEncoded = "InterfaceEncoded";
        private static string GetSectionName(string dirName, string fileName) => $"EncodedFile-{dirName}-{fileName}";
        #endregion

        #region AttachFile
        [TestMethod]
        [TestCategory("EncodedFile")]
        public void EncodedFile_AttachFile()
        {
            AttachFile_Template("Type1.jpg", EncodedFile.EncodeMode.ZLib); // Type 1
            AttachFile_Template("Type2.7z", EncodedFile.EncodeMode.Raw); // Type 2
            AttachFile_Template("Type3.pdf", EncodedFile.EncodeMode.XZ); // Type 3
            AttachFile_Template("PEBakeryAlphaMemory.jpg", EncodedFile.EncodeMode.ZLib);
        }

        public void AttachFile_Template(string fileName, EncodedFile.EncodeMode encodeMode)
        {
            EngineState s = EngineTests.CreateEngineState();
            string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "EncodedFile"));
            string blankPath = Path.Combine(dirPath, "Blank.script");
            string scPath = Path.Combine(dirPath, "EncodeFileTests.script");
            File.Copy(blankPath, scPath, true);

            Script sc = s.Project.LoadScriptMonkeyPatch(scPath);

            string originFile = Path.Combine(dirPath, fileName);
            sc = EncodedFile.AttachFile(sc, "FolderExample", fileName, originFile, encodeMode);

            try
            {
                // Check whether file was successfully encoded
                Assert.IsTrue(sc.Sections.ContainsKey("EncodedFolders"));
                List<string> folders = sc.Sections["EncodedFolders"].GetLines();
                folders = folders.Where(x => x.Equals(string.Empty, StringComparison.Ordinal) == false).ToList();
                Assert.IsTrue(folders.Count == 2);
                Assert.IsTrue(folders[0].Equals("FolderExample", StringComparison.Ordinal));

                Assert.IsTrue(sc.Sections.ContainsKey("FolderExample"));
                List<string> fileInfos = sc.Sections["FolderExample"].GetLinesOnce();
                fileInfos = fileInfos.Where(x => x.Equals(string.Empty, StringComparison.Ordinal) == false).ToList();
                Assert.IsTrue(fileInfos[0].StartsWith($"{fileName}=", StringComparison.Ordinal));

                Assert.IsTrue(sc.Sections.ContainsKey($"EncodedFile-FolderExample-{fileName}"));
                List<string> encodedFile = sc.Sections[$"EncodedFile-FolderExample-{fileName}"].GetLinesOnce();
                encodedFile = encodedFile.Where(x => x.Equals(string.Empty, StringComparison.Ordinal) == false).ToList();
                Assert.IsTrue(1 < encodedFile.Count);
                Assert.IsTrue(encodedFile[0].StartsWith("lines=", StringComparison.Ordinal));

                // Check whether file can be successfully extracted
                byte[] extractDigest;
                using (MemoryStream ms = new MemoryStream())
                {
                    EncodedFile.ExtractFile(sc, "FolderExample", fileName, ms);
                    ms.Position = 0;
                    extractDigest = HashHelper.CalcHash(HashHelper.HashType.SHA256, ms);
                }

                byte[] originDigest;
                using (FileStream fs = new FileStream(originFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    originDigest = HashHelper.CalcHash(HashHelper.HashType.SHA256, fs);
                }

                Assert.IsTrue(originDigest.SequenceEqual(extractDigest));
            }
            finally
            {
                File.Delete(scPath);
            }
        }
        #endregion

        #region AddFolder
        [TestMethod]
        [TestCategory("EncodedFile")]
        public void EncodedFile_AddFolder()
        {
            void Template(string folderName, bool overwrite, bool result)
            {
                EngineState s = EngineTests.CreateEngineState();
                string pbOriginScript = Path.Combine("%TestBench%", "EncodedFile", "Blank.script");
                string originScript = StringEscaper.Preprocess(s, pbOriginScript);

                string pbDestDir = Path.Combine("%ProjectTemp%", Path.GetRandomFileName());
                string pbDestScript = Path.Combine(pbDestDir, "AddFolderTest.script");

                string destDir = StringEscaper.Preprocess(s, pbDestDir);
                string destScript = StringEscaper.Preprocess(s, pbDestScript);

                if (!Directory.Exists(destDir))
                    Directory.CreateDirectory(destDir);
                try
                {
                    File.Copy(originScript, destScript, true);

                    Script sc = s.Project.LoadScriptMonkeyPatch(destScript);
                    try
                    {
                        sc = EncodedFile.AddFolder(sc, folderName, overwrite);
                    }
                    catch (InvalidOperationException)
                    {
                        Assert.IsFalse(result);
                        return;
                    }

                    Assert.AreEqual(sc.Sections.ContainsKey(folderName), result);
                    Assert.AreEqual(Ini.ContainsSection(destScript, folderName), result);

                    if (!folderName.Equals(AuthorEncoded, StringComparison.OrdinalIgnoreCase) &&
                        !folderName.Equals(InterfaceEncoded, StringComparison.OrdinalIgnoreCase))
                    {
                        List<string> folders = sc.Sections[EncodedFolders].GetLinesOnce();
                        Assert.AreEqual(folders.Contains(folderName, StringComparer.OrdinalIgnoreCase), result);
                    }
                }
                finally
                {
                    if (Directory.Exists(destDir))
                        Directory.Delete(destDir, true);
                }
            }

            Template("UnitTestEncode", false, true);
            Template("DummySection", false, false);
            Template("DummySection", true, true);
            Template("AuthorEncoded", false, false);
            Template("AuthorEncoded", true, true);
            Template("InterfaceEncoded", false, true);
            Template("InterfaceEncoded", true, true);
        }
        #endregion

        #region ContainsFolder
        [TestMethod]
        [TestCategory("EncodedFile")]
        public void EncodedFile_ContainsFolder()
        {
            void Template(string folderName, bool result)
            {
                EngineState s = EngineTests.CreateEngineState();
                string pbOriginScript = Path.Combine("%TestBench%", "EncodedFile", "Blank.script");
                string originScript = StringEscaper.Preprocess(s, pbOriginScript);

                string pbDestDir = Path.Combine("%ProjectTemp%", Path.GetRandomFileName());
                string pbDestScript = Path.Combine(pbDestDir, "AddFolderTest.script");

                string destDir = StringEscaper.Preprocess(s, pbDestDir);
                string destScript = StringEscaper.Preprocess(s, pbDestScript);

                if (!Directory.Exists(destDir))
                    Directory.CreateDirectory(destDir);
                try
                {
                    File.Copy(originScript, destScript, true);

                    Script sc = s.Project.LoadScriptMonkeyPatch(destScript);
                    Assert.AreEqual(EncodedFile.ContainsFolder(sc, folderName), result);
                }
                finally
                {
                    if (Directory.Exists(destDir))
                        Directory.Delete(destDir, true);
                }
            }

            Template(AuthorEncoded, true);
            Template(InterfaceEncoded, false);
            Template("Attach", true);
            Template("Process", false);
        }
        #endregion

        #region ExtractFile
        [TestMethod]
        [TestCategory("EncodedFile")]
        public void EncodedFile_ExtractFile()
        {
            ExtractFile_Template("Type1.jpg"); // Type 1
            ExtractFile_Template("Type2.7z"); // Type 2
            ExtractFile_Template("Type3.pdf"); // Type 3
        }

        public void ExtractFile_Template(string fileName)
        { // Type 1
            EngineState s = EngineTests.CreateEngineState();
            string scPath = Path.Combine("%TestBench%", "EncodedFile", "ExtractFileTests.script");
            scPath = StringEscaper.Preprocess(s, scPath);
            Script sc = s.Project.LoadScriptMonkeyPatch(scPath);

            byte[] extractDigest;
            using (MemoryStream ms = new MemoryStream())
            {
                EncodedFile.ExtractFile(sc, "FolderExample", fileName, ms);
                ms.Position = 0;
                extractDigest = HashHelper.CalcHash(HashHelper.HashType.SHA256, ms);
            }

            string originFile = Path.Combine("%TestBench%", "EncodedFile", fileName);
            originFile = StringEscaper.Preprocess(s, originFile);
            byte[] originDigest;
            using (FileStream fs = new FileStream(originFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                originDigest = HashHelper.CalcHash(HashHelper.HashType.SHA256, fs);
            }

            // Compare Hash
            Assert.IsTrue(originDigest.SequenceEqual(extractDigest));
        }
        #endregion

        #region ExtractFileInMem
        [TestMethod]
        [TestCategory("EncodedFile")]
        public void EncodedFile_ExtractFileInMem()
        {
            ExtractFileInMem_Template("Type1.jpg"); // Type 1
            ExtractFileInMem_Template("Type2.7z"); // Type 2
            ExtractFileInMem_Template("Type3.pdf"); // Type 3
        }

        public void ExtractFileInMem_Template(string fileName)
        { // Type 1
            EngineState s = EngineTests.CreateEngineState();
            string scPath = Path.Combine("%TestBench%", "EncodedFile", "ExtractFileTests.script");
            scPath = StringEscaper.Preprocess(s, scPath);
            Script sc = s.Project.LoadScriptMonkeyPatch(scPath);

            byte[] extractDigest;
            using (MemoryStream ms = EncodedFile.ExtractFileInMem(sc, "FolderExample", fileName))
            {
                ms.Position = 0;
                extractDigest = HashHelper.CalcHash(HashHelper.HashType.SHA256, ms);
            }

            string originFile = Path.Combine("%TestBench%", "EncodedFile", fileName);
            originFile = StringEscaper.Preprocess(s, originFile);
            byte[] originDigest;
            using (FileStream fs = new FileStream(originFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                originDigest = HashHelper.CalcHash(HashHelper.HashType.SHA256, fs);
            }

            // Compare Hash
            Assert.IsTrue(originDigest.SequenceEqual(extractDigest));
        }
        #endregion

        #region ExtractFolder
        [TestMethod]
        [TestCategory("EncodedFile")]
        public void EncodedFile_ExtractFolder()
        {
            EngineState s = EngineTests.CreateEngineState();

            void Template(string folderName)
            {
                string pbOriginScript = Path.Combine("%TestBench%", "EncodedFile", "ExtractFileTests.script");
                string originScript = StringEscaper.Preprocess(s, pbOriginScript);

                string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                if (!Directory.Exists(destDir))
                    Directory.CreateDirectory(destDir);
                try
                {
                    Script sc = s.Project.LoadScriptMonkeyPatch(originScript);

                    EncodedFile.ExtractFolder(sc, folderName, destDir);

                    string[] comps = Ini.ParseIniLinesIniStyle(sc.Sections[folderName].GetLines()).Keys.ToArray();
                    string[] dests = Directory.EnumerateFiles(destDir).Select(Path.GetFileName).ToArray();

                    Assert.IsTrue(comps.SequenceEqual(dests, StringComparer.OrdinalIgnoreCase));
                }
                finally
                {
                    if (Directory.Exists(destDir))
                        Directory.Delete(destDir, true);
                }
            }

            Template("FolderExample");
        }
        #endregion

        #region ExtractLogo
        [TestMethod]
        [TestCategory("EncodedFile")]
        public void EncodedFile_ExtractLogo()
        {
            ExtractLogo_1();
        }

        public void ExtractLogo_1()
        { // Type 1
            EngineState s = EngineTests.CreateEngineState();
            string scPath = Path.Combine("%TestBench%", "EncodedFile", "ExtractFileTests.script");
            scPath = StringEscaper.Preprocess(s, scPath);
            Script sc = s.Project.LoadScriptMonkeyPatch(scPath);

            byte[] extractDigest;
            using (MemoryStream ms = EncodedFile.ExtractLogo(sc, out ImageHelper.ImageType type))
            {
                Assert.IsTrue(type == ImageHelper.ImageType.Jpg);
                extractDigest = HashHelper.CalcHash(HashHelper.HashType.SHA256, ms);
            }

            string originFile = Path.Combine("%TestBench%", "EncodedFile", "Logo.jpg");
            originFile = StringEscaper.Preprocess(s, originFile);
            byte[] originDigest;
            using (FileStream fs = new FileStream(originFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                originDigest = HashHelper.CalcHash(HashHelper.HashType.SHA256, fs);
            }

            // Compare Hash
            Assert.IsTrue(originDigest.SequenceEqual(extractDigest));
        }
        #endregion

        #region ExtractInterfaceEncoded
        [TestMethod]
        [TestCategory("EncodedFile")]
        public void EncodedFile_ExtractInterfaceEncoded()
        {
            ExtractInterfaceEncoded_1();
        }

        public void ExtractInterfaceEncoded_1()
        { // Type 1
            EngineState s = EngineTests.CreateEngineState();
            string scPath = Path.Combine("%TestBench%", "EncodedFile", "ExtractFileTests.script");
            scPath = StringEscaper.Preprocess(s, scPath);
            Script sc = s.Project.LoadScriptMonkeyPatch(scPath);

            byte[] extractDigest;
            using (MemoryStream ms = EncodedFile.ExtractInterfaceEncoded(sc, "PEBakeryAlphaMemory.jpg"))
            {
                extractDigest = HashHelper.CalcHash(HashHelper.HashType.SHA256, ms);
            }

            string originFile = Path.Combine("%TestBench%", "EncodedFile", "PEBakeryAlphaMemory.jpg");
            originFile = StringEscaper.Preprocess(s, originFile);
            byte[] originDigest;
            using (FileStream fs = new FileStream(originFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                originDigest = HashHelper.CalcHash(HashHelper.HashType.SHA256, fs);
            }

            // Compare Hash
            Assert.IsTrue(originDigest.SequenceEqual(extractDigest));
        }
        #endregion

        #region SplitBase64

        [TestMethod]
        [TestCategory("EncodedFile")]
        public void SplitBase64_Encode()
        {
            EngineState s = EngineTests.CreateEngineState();

            void Template(string binFileName, string encFileName, bool inMem)
            {
                string workDir = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "EncodedFile"));

                // Be careful! encFile will be converted to LF from CRLF in git tree!
                string binFile = Path.Combine(workDir, binFileName);
                string encFile = Path.Combine(workDir, encFileName);

                List<string> lines = new List<string>();
                using (StreamReader r = new StreamReader(encFile, Encoding.UTF8))
                {
                    string rawLine;
                    while ((rawLine = r.ReadLine()) != null)
                    {
                        string line = rawLine.Trim();
                        if (0 < line.Length)
                            lines.Add(line);
                    }
                }

                List<string> comps;
                if (inMem)
                {
                    byte[] buffer;
                    using (FileStream fs = new FileStream(binFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        buffer = new byte[fs.Length];
                        fs.Read(buffer, 0, buffer.Length);
                    }

                    (List<IniKey> keys, _) = SplitBase64.EncodeInMem(buffer, string.Empty);
                    comps = keys.Select(x => $"{x.Key}={x.Value}").ToList();
                }
                else
                {
                    List<IniKey> keys;
                    using (FileStream fs = new FileStream(binFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        (keys, _) = SplitBase64.Encode(fs, string.Empty);
                    }
                    comps = keys.Select(x => $"{x.Key}={x.Value}").ToList();
                }

                Assert.IsTrue(lines.SequenceEqual(comps));
            }

            Template("BigData.bin", "BigDataEnc4090.txt", true);
            Template("BigData.bin", "BigDataEnc4090.txt", false);
            Template("Type3.pdf", "Type3Enc4090.txt", true);
            Template("Type3.pdf", "Type3Enc4090.txt", false);
        }

        [TestMethod]
        [TestCategory("EncodedFile")]
        public void SplitBase64_Decode()
        {
            EngineState s = EngineTests.CreateEngineState();

            void Template(string binFileName, string encFileName, bool inMem)
            {
                string workDir = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "EncodedFile"));

                // Be careful! encFile will be converted to LF from CRLF in git tree!
                string binFile = Path.Combine(workDir, binFileName);
                string encFile = Path.Combine(workDir, encFileName);

                byte[] binDigest;
                byte[] encDigest;
                using (FileStream fs = new FileStream(binFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    binDigest = HashHelper.CalcHash(HashHelper.HashType.SHA256, fs);
                }

                List<string> lines = new List<string>();
                using (StreamReader r = new StreamReader(encFile, Encoding.UTF8))
                {
                    string rawLine;
                    while ((rawLine = r.ReadLine()) != null)
                    {
                        string line = rawLine.Trim();
                        if (0 < line.Length)
                            lines.Add(line);
                    }
                }

                if (inMem)
                {
                    byte[] decoded = SplitBase64.DecodeInMem(lines);
#if DEBUG_MIDDLE_FILE
                    using (FileStream fs = new FileStream(binFile + ".inMem.comp", FileMode.Create, FileAccess.Write))
                    {
                        fs.Write(decoded, 0, decoded.Length);
                    }
#endif
                    encDigest = HashHelper.CalcHash(HashHelper.HashType.SHA256, decoded);
                }
                else
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        SplitBase64.Decode(lines, ms);
                        ms.Position = 0;
#if DEBUG_MIDDLE_FILE
                        using (FileStream fs = new FileStream(binFile + ".noMem.comp", FileMode.Create, FileAccess.Write))
                        {
                            ms.CopyTo(fs);
                        }
                        ms.Position = 0;
#endif
                        encDigest = HashHelper.CalcHash(HashHelper.HashType.SHA256, ms);
                    }
                        
                }

                Assert.IsTrue(binDigest.SequenceEqual(encDigest));
            }

            Template("BigData.bin", "BigDataEnc4090.txt", true);
            Template("BigData.bin", "BigDataEnc4090.txt", false);
            Template("Type3.pdf", "Type3Enc4090.txt", true);
            Template("Type3.pdf", "Type3Enc4090.txt", false);
            Template("Type3.pdf", "Type3Enc1024.txt", true);
            Template("Type3.pdf", "Type3Enc1024.txt", false);
        }
        #endregion

        #region Benchmark
        [TestMethod]
        [TestCategory("EncodedFile")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public void EncodedFile_Benchmark()
        {
            EngineState s = EngineTests.CreateEngineState();

            void Template(string fileName)
            {
                string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                Directory.CreateDirectory(destDir);
                try
                {
                    string workDir = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "EncodedFile"));
                    string rawFile = Path.Combine(workDir, fileName);

                    byte[] rawFileData;
                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (FileStream fs = new FileStream(rawFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            fs.CopyTo(ms);
                        }

                        rawFileData = ms.ToArray();
                    }

                    // Compression
                    {
                        // RawFile
                        long rawFileLen = new FileInfo(rawFile).Length;

                        // zlib
                        (long, TimeSpan) ZLibBenchmarkCompress(CompressionLevel compLevel)
                        {
                            using (MemoryStream ms = new MemoryStream())
                            {
                                Stopwatch watch = Stopwatch.StartNew();
                                using (MemoryStream rms = new MemoryStream(rawFileData))
                                using (ZLibStream zs = new ZLibStream(ms, CompressionMode.Compress, compLevel, true))
                                {
                                    rms.CopyTo(zs);
                                }

                                ms.Flush();
                                return (ms.Position, watch.Elapsed);
                            }
                        }
                        (long zlibFastestLen, TimeSpan zlibFastestTime) = ZLibBenchmarkCompress(CompressionLevel.Fastest);
                        (long zlibDefaultLen, TimeSpan zlibDefaultTime) = ZLibBenchmarkCompress(CompressionLevel.Default);
                        (long zlibBestLen, TimeSpan zlibBestTime) = ZLibBenchmarkCompress(CompressionLevel.Best);

                        // xz
                        (long, TimeSpan) XZBenchmarkCompress(uint preset)
                        {
                            using (MemoryStream ms = new MemoryStream())
                            {
                                Stopwatch watch = Stopwatch.StartNew();
                                using (MemoryStream rms = new MemoryStream(rawFileData))
                                using (XZStream xzs = new XZStream(ms, LzmaMode.Compress, preset, true))
                                {
                                    rms.CopyTo(xzs);
                                }

                                ms.Flush();
                                return (ms.Position, watch.Elapsed);
                            }
                        }
                        (long xzFastestLen, TimeSpan xzFastestTime) = XZBenchmarkCompress(XZStream.MinimumPreset);
                        (long xzDefaultLen, TimeSpan xzDefaultTime) = XZBenchmarkCompress(XZStream.DefaultPreset);
                        (long xzBestLen, TimeSpan xzBestTime) = XZBenchmarkCompress(XZStream.MaximumPreset);

                        // lz4
                        (long, TimeSpan) LZ4BenchmarkCompress(LZ4CompLevel compLevel)
                        {
                            using (MemoryStream ms = new MemoryStream())
                            {
                                Stopwatch watch = Stopwatch.StartNew();
                                using (MemoryStream rms = new MemoryStream(rawFileData))
                                using (LZ4FrameStream lzs = new LZ4FrameStream(ms, LZ4Mode.Compress, compLevel, true))
                                {
                                    rms.CopyTo(lzs);
                                }

                                ms.Flush();
                                return (ms.Position, watch.Elapsed);
                            }
                        }
                        (long lz4FastestLen, TimeSpan lz4FastestTime) = LZ4BenchmarkCompress(LZ4CompLevel.Fast);
                        (long lz4DefaultLen, TimeSpan lz4DefaultTime) = LZ4BenchmarkCompress(LZ4CompLevel.High);
                        (long lz4BestLen, TimeSpan lz4BestTime) = LZ4BenchmarkCompress(LZ4CompLevel.VeryHigh); // Toggle lz4-hc mode

                        StringBuilder b = new StringBuilder();
                        b.AppendLine($"[{fileName} - Compress]");
                        b.AppendLine($"raw            : 100%, {rawFileLen}");
                        b.AppendLine($"zlib (Fastest) : {Math.Round(zlibFastestLen * 100.0 / rawFileLen, 0):##0}%, {zlibFastestTime.TotalMilliseconds}ms ({zlibFastestLen}B)");
                        b.AppendLine($"zlib (Default) : {Math.Round(zlibDefaultLen * 100.0 / rawFileLen, 0):##0}%, {zlibDefaultTime.TotalMilliseconds}ms ({zlibDefaultLen}B)");
                        b.AppendLine($"zlib (Best)    : {Math.Round(zlibBestLen * 100.0 / rawFileLen, 0):##0}%, {zlibBestTime.TotalMilliseconds}ms ({zlibBestLen}B)");
                        b.AppendLine($"xz   (Fastest) : {Math.Round(xzFastestLen * 100.0 / rawFileLen, 0):##0}%, {xzFastestTime.TotalMilliseconds}ms ({xzFastestLen}B)");
                        b.AppendLine($"xz   (Default) : {Math.Round(xzDefaultLen * 100.0 / rawFileLen, 0):##0}%, {xzDefaultTime.TotalMilliseconds}ms ({xzDefaultLen}B)");
                        b.AppendLine($"xz   (Best)    : {Math.Round(xzBestLen * 100.0 / rawFileLen, 0):##0}%, {xzBestTime.TotalMilliseconds}ms ({xzBestLen}B)");
                        b.AppendLine($"lz4  (Fastest) : {Math.Round(lz4FastestLen * 100.0 / rawFileLen, 0):##0}%, {lz4FastestTime.TotalMilliseconds}ms ({lz4FastestLen}B)");
                        b.AppendLine($"lz4  (Default) : {Math.Round(lz4DefaultLen * 100.0 / rawFileLen, 0):##0}%, {lz4DefaultTime.TotalMilliseconds}ms ({lz4DefaultLen}B)");
                        b.AppendLine($"lz4  (Best)    : {Math.Round(lz4BestLen * 100.0 / rawFileLen, 0):##0}%, {lz4BestTime.TotalMilliseconds}ms ({lz4BestLen}B)");
                        Console.WriteLine(b.ToString());
                    }

                    // Decompression
                    {
                        // zlib
                        TimeSpan ZLibBenchmarkDecompress(string dirName)
                        {
                            string zlibFile = Path.Combine(workDir, "Benchmark", dirName, fileName + ".zz");
                            byte[] zlibData;
                            using (MemoryStream ms = new MemoryStream())
                            {
                                using (FileStream fs = new FileStream(zlibFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                                {
                                    fs.CopyTo(ms);
                                }

                                zlibData = ms.ToArray();
                            }

                            using (MemoryStream ms = new MemoryStream())
                            {
                                Stopwatch watch = Stopwatch.StartNew();
                                using (MemoryStream rms = new MemoryStream(zlibData))
                                using (ZLibStream zs = new ZLibStream(rms, CompressionMode.Decompress))
                                {
                                    zs.CopyTo(ms);
                                }

                                ms.Flush();
                                return watch.Elapsed;
                            }
                        }
                        TimeSpan zlibFastestTime = ZLibBenchmarkDecompress("Fastest");
                        TimeSpan zlibDefaultTime = ZLibBenchmarkDecompress("Default");
                        TimeSpan zlibBestTime = ZLibBenchmarkDecompress("Best");

                        // xz
                        TimeSpan XZBenchmarkDecompress(string dirName)
                        {
                            string xzFile = Path.Combine(workDir, "Benchmark", dirName, fileName + ".xz");
                            byte[] xzData;
                            using (MemoryStream ms = new MemoryStream())
                            {
                                using (FileStream fs = new FileStream(xzFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                                {
                                    fs.CopyTo(ms);
                                }

                                xzData = ms.ToArray();
                            }

                            using (MemoryStream ms = new MemoryStream())
                            {
                                Stopwatch watch = Stopwatch.StartNew();
                                using (MemoryStream rms = new MemoryStream(xzData))
                                using (XZStream xzs = new XZStream(rms, LzmaMode.Decompress))
                                {
                                    xzs.CopyTo(ms);
                                }

                                ms.Flush();
                                return watch.Elapsed;
                            }
                        }
                        TimeSpan xzFastestTime = XZBenchmarkDecompress("Fastest");
                        TimeSpan xzDefaultTime = XZBenchmarkDecompress("Default");
                        TimeSpan xzBestTime = XZBenchmarkDecompress("Best");

                        // lz4
                        TimeSpan LZ4BenchmarkDecompress(string dirName)
                        {
                            string lz4File = Path.Combine(workDir, "Benchmark", dirName, fileName + ".lz4");
                            byte[] lz4Data;
                            using (MemoryStream ms = new MemoryStream())
                            {
                                using (FileStream fs = new FileStream(lz4File, FileMode.Open, FileAccess.Read, FileShare.Read))
                                {
                                    fs.CopyTo(ms);
                                }

                                lz4Data = ms.ToArray();
                            }

                            using (MemoryStream ms = new MemoryStream())
                            {
                                Stopwatch watch = Stopwatch.StartNew();
                                using (MemoryStream rms = new MemoryStream(lz4Data))
                                using (LZ4FrameStream lzs = new LZ4FrameStream(rms, LZ4Mode.Decompress))
                                {
                                    lzs.CopyTo(ms);
                                }

                                ms.Flush();
                                return watch.Elapsed;
                            }
                        }
                        TimeSpan lz4FastestTime = LZ4BenchmarkDecompress("Fastest");
                        TimeSpan lz4DefaultTime = LZ4BenchmarkDecompress("Default");
                        TimeSpan lz4BestTime = LZ4BenchmarkDecompress("Best"); // Toggle lz4-hc mode

                        StringBuilder b = new StringBuilder();
                        b.AppendLine($"[{fileName} - Decompress]");
                        b.AppendLine($"zlib (Fastest) : {zlibFastestTime.TotalMilliseconds}ms");
                        b.AppendLine($"zlib (Default) : {zlibDefaultTime.TotalMilliseconds}ms");
                        b.AppendLine($"zlib (Best)    : {zlibBestTime.TotalMilliseconds}ms");
                        b.AppendLine($"xz   (Fastest) : {xzFastestTime.TotalMilliseconds}ms");
                        b.AppendLine($"xz   (Default) : {xzDefaultTime.TotalMilliseconds}ms");
                        b.AppendLine($"xz   (Best)    : {xzBestTime.TotalMilliseconds}m");
                        b.AppendLine($"lz4  (Fastest) : {lz4FastestTime.TotalMilliseconds}ms");
                        b.AppendLine($"lz4  (Default) : {lz4DefaultTime.TotalMilliseconds}ms");
                        b.AppendLine($"lz4  (Best)    : {lz4BestTime.TotalMilliseconds}ms");
                        Console.WriteLine(b.ToString());
                    }

                }
                finally
                {
                    if (Directory.Exists(destDir))
                        Directory.Delete(destDir, true);
                }

                
            }

            Template("Type4.txt");
            Template("Banner.svg");
            Template("Banner.bmp");            
        }
        #endregion
    }
}
