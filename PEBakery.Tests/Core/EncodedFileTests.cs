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
*/

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.Core;
using System.IO;
using System.Linq;
using PEBakery.Helper;
using System.Collections.Generic;
using PEBakery.Exceptions;
using PEBakery.Lib;
using System.Text;

namespace UnitTest.Core
{
    [TestClass]
    public class EncodedFileTests
    {
        #region AttachFile
        [TestMethod]
        [TestCategory("EncodedFile")]
        public void AttachFile()
        {
            AttachFile_Template("Type1.jpg"); // Type 1
            AttachFile_Template("Type2.7z"); // Type 2
            // For testing SharpCompress 0.18.1 Adler32 checksum bug
            AttachFile_Template("PEBakeryAlphaMemory.jpg"); // -> Seems SharpCompress Adler32 is not faulty when compressing
        }

        public void AttachFile_Template( string fileName)
        {
            EngineState s = EngineTests.CreateEngineState();
            string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "EncodedFile"));
            string blankPath = Path.Combine(dirPath, "EncodeFileTests_Blank.script");
            string pPath = Path.Combine(dirPath, "EncodeFileTests.script");
            File.Copy(blankPath, pPath, true);

            Plugin p = s.Project.LoadPluginMonkeyPatch(pPath);

            string originFile = Path.Combine(dirPath, fileName);
            p = EncodedFile.AttachFile(p, "FolderExample", fileName, originFile, EncodedFile.EncodeMode.Compress);

            try
            {
                // Check whether file was successfully encoded
                Assert.IsTrue(p.Sections.ContainsKey("EncodedFolders"));
                List<string> folders = p.Sections["EncodedFolders"].GetLines();
                folders = folders.Where(x => x.Equals(string.Empty, StringComparison.Ordinal) == false).ToList();
                Assert.IsTrue(folders.Count == 1);
                Assert.IsTrue(folders[0].Equals("FolderExample", StringComparison.Ordinal));

                Assert.IsTrue(p.Sections.ContainsKey("FolderExample"));
                List<string> fileInfos = p.Sections["FolderExample"].GetLinesOnce();
                fileInfos = fileInfos.Where(x => x.Equals(string.Empty, StringComparison.Ordinal) == false).ToList();
                Assert.IsTrue(fileInfos[0].StartsWith($"{fileName}=", StringComparison.Ordinal));

                Assert.IsTrue(p.Sections.ContainsKey($"EncodedFile-FolderExample-{fileName}"));
                List<string> encodedFile = p.Sections[$"EncodedFile-FolderExample-{fileName}"].GetLinesOnce();
                encodedFile = encodedFile.Where(x => x.Equals(string.Empty, StringComparison.Ordinal) == false).ToList();
                Assert.IsTrue(1 < encodedFile.Count);
                Assert.IsTrue(encodedFile[0].StartsWith("lines=", StringComparison.Ordinal));

                // Check whether file can be successfully extracted
                byte[] extractDigest;
                using (MemoryStream ms = EncodedFile.ExtractFile(p, "FolderExample", fileName))
                {
                    extractDigest = HashHelper.CalcHash(HashType.SHA256, ms);
                    ms.Close();
                }

                byte[] originDigest;
                using (FileStream fs = new FileStream(originFile, FileMode.Open))
                {
                    originDigest = HashHelper.CalcHash(HashType.SHA256, fs);
                    fs.Close();
                }

                Assert.IsTrue(originDigest.SequenceEqual(extractDigest));
            }
            finally
            {
                File.Delete(pPath);
            }
        }
        #endregion

        #region ExtractFile
        [TestMethod]
        [TestCategory("EncodedFile")]
        public void ExtractFile()
        {
            ExtractFile_1(); // Type 1
            ExtractFile_2(); // Type 2
        }

        public void ExtractFile_1()
        { // Type 1
            EngineState s = EngineTests.CreateEngineState();
            string pPath = Path.Combine("%TestBench%", "EncodedFile", "ExtractFileTests.script");
            pPath = StringEscaper.Preprocess(s, pPath);
            Plugin p = s.Project.LoadPluginMonkeyPatch(pPath);

            byte[] extractDigest;
            using (MemoryStream ms = EncodedFile.ExtractFile(p, "FolderExample", "Type1.jpg"))
            {
                extractDigest = HashHelper.CalcHash(HashType.SHA256, ms);
                ms.Close();
            }

            string originFile = Path.Combine("%TestBench%", "EncodedFile", "Type1.jpg");
            originFile = StringEscaper.Preprocess(s, originFile);
            byte[] originDigest;
            using (FileStream fs = new FileStream(originFile, FileMode.Open))
            {
                originDigest = HashHelper.CalcHash(HashType.SHA256, fs);
                fs.Close();
            }

            // Compare Hash
            Assert.IsTrue(originDigest.SequenceEqual(extractDigest));
        }

        public void ExtractFile_2()
        { // Type 2
            EngineState s = EngineTests.CreateEngineState();
            string pPath = Path.Combine("%TestBench%", "EncodedFile", "ExtractFileTests.script");
            pPath = StringEscaper.Preprocess(s, pPath);
            Plugin p = s.Project.LoadPluginMonkeyPatch(pPath);

            byte[] extractDigest;
            using (MemoryStream ms = EncodedFile.ExtractFile(p, "FolderExample", "Type2.7z"))
            {
                extractDigest = HashHelper.CalcHash(HashType.SHA256, ms);
                ms.Close();
            }

            string originFile = Path.Combine("%TestBench%", "EncodedFile", "Type2.7z");
            originFile = StringEscaper.Preprocess(s, originFile);
            byte[] originDigest;
            using (FileStream fs = new FileStream(originFile, FileMode.Open))
            {
                originDigest = HashHelper.CalcHash(HashType.SHA256, fs);
                fs.Close();
            }

            // Compare Hash
            Assert.IsTrue(originDigest.SequenceEqual(extractDigest));
        }
        #endregion

        #region ExtractLogo
        [TestMethod]
        [TestCategory("EncodedFile")]
        public void ExtractLogo()
        {
            ExtractLogo_1();
        }

        public void ExtractLogo_1()
        { // Type 1
            EngineState s = EngineTests.CreateEngineState();
            string pPath = Path.Combine("%TestBench%", "EncodedFile", "ExtractFileTests.script");
            pPath = StringEscaper.Preprocess(s, pPath);
            Plugin p = s.Project.LoadPluginMonkeyPatch(pPath);

            byte[] extractDigest;
            using (MemoryStream ms = EncodedFile.ExtractLogo(p, out ImageHelper.ImageType type))
            {
                Assert.IsTrue(type == ImageHelper.ImageType.Jpg);
                extractDigest = HashHelper.CalcHash(HashType.SHA256, ms);
                ms.Close();
            }

            string originFile = Path.Combine("%TestBench%", "EncodedFile", "Logo.jpg");
            originFile = StringEscaper.Preprocess(s, originFile);
            byte[] originDigest;
            using (FileStream fs = new FileStream(originFile, FileMode.Open))
            {
                originDigest = HashHelper.CalcHash(HashType.SHA256, fs);
                fs.Close();
            }

            // Compare Hash
            Assert.IsTrue(originDigest.SequenceEqual(extractDigest));
        }
        #endregion

        #region ExtractInterfaceEncoded
        [TestMethod]
        [TestCategory("EncodedFile")]
        public void ExtractInterfaceEncoded()
        {
            // Uncomment this line to test SharpCompress' ZlibStream failure
            ExtractInterfaceEncoded_1();
        }

        public void ExtractInterfaceEncoded_1()
        { // Type 1
            EngineState s = EngineTests.CreateEngineState();
            string pPath = Path.Combine("%TestBench%", "EncodedFile", "ExtractFileTests.script");
            pPath = StringEscaper.Preprocess(s, pPath);
            Plugin p = s.Project.LoadPluginMonkeyPatch(pPath);

            byte[] extractDigest;
            using (MemoryStream ms = EncodedFile.ExtractInterfaceEncoded(p, "PEBakeryAlphaMemory.jpg"))
            {
                extractDigest = HashHelper.CalcHash(HashType.SHA256, ms);
                ms.Close();
            }

            string originFile = Path.Combine("%TestBench%", "EncodedFile", "PEBakeryAlphaMemory.jpg");
            originFile = StringEscaper.Preprocess(s, originFile);
            byte[] originDigest;
            using (FileStream fs = new FileStream(originFile, FileMode.Open))
            {
                originDigest = HashHelper.CalcHash(HashType.SHA256, fs);
                fs.Close();
            }

            // Compare Hash
            Assert.IsTrue(originDigest.SequenceEqual(extractDigest));
        }
        #endregion
    }
}
