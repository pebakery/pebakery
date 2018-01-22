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

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.Core;
using System.IO;
using System.Linq;
using PEBakery.Helper;
using System.Collections.Generic;
using PEBakery.Exceptions;
using System.Text;

namespace PEBakery.Tests.Core
{
    [TestClass]
    public class EncodedFileTests
    {
        #region AttachFile
        [TestMethod]
        [TestCategory("EncodedFile")]
        public void EncodedFile_AttachFile()
        {
            AttachFile_Template("Type1.jpg", EncodedFile.EncodeMode.Compress); // Type 1
            AttachFile_Template("Type2.7z", EncodedFile.EncodeMode.Raw); // Type 2
            AttachFile_Template("Type3.pdf", EncodedFile.EncodeMode.XZ); // Type 3
            AttachFile_Template("PEBakeryAlphaMemory.jpg", EncodedFile.EncodeMode.Compress);
        }

        public void AttachFile_Template(string fileName, EncodedFile.EncodeMode encodeMode)
        {
            EngineState s = EngineTests.CreateEngineState();
            string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "EncodedFile"));
            string blankPath = Path.Combine(dirPath, "EncodeFileTests_Blank.script");
            string pPath = Path.Combine(dirPath, "EncodeFileTests.script");
            File.Copy(blankPath, pPath, true);

            Script p = s.Project.LoadScriptMonkeyPatch(pPath);

            string originFile = Path.Combine(dirPath, fileName);
            p = EncodedFile.AttachFile(p, "FolderExample", fileName, originFile, encodeMode);

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
                }

                byte[] originDigest;
                using (FileStream fs = new FileStream(originFile, FileMode.Open))
                {
                    originDigest = HashHelper.CalcHash(HashType.SHA256, fs);
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
        public void EncodedFile_ExtractFile()
        {
            ExtractFile_Template("Type1.jpg"); // Type 1
            ExtractFile_Template("Type2.7z"); // Type 2
            ExtractFile_Template("Type3.pdf"); // Type 3
        }

        public void ExtractFile_Template(string fileName)
        { // Type 1
            EngineState s = EngineTests.CreateEngineState();
            string pPath = Path.Combine("%TestBench%", "EncodedFile", "ExtractFileTests.script");
            pPath = StringEscaper.Preprocess(s, pPath);
            Script p = s.Project.LoadScriptMonkeyPatch(pPath);

            byte[] extractDigest;
            using (MemoryStream ms = EncodedFile.ExtractFile(p, "FolderExample", fileName))
            {
                extractDigest = HashHelper.CalcHash(HashType.SHA256, ms);
            }

            string originFile = Path.Combine("%TestBench%", "EncodedFile", fileName);
            originFile = StringEscaper.Preprocess(s, originFile);
            byte[] originDigest;
            using (FileStream fs = new FileStream(originFile, FileMode.Open))
            {
                originDigest = HashHelper.CalcHash(HashType.SHA256, fs);
            }

            // Compare Hash
            Assert.IsTrue(originDigest.SequenceEqual(extractDigest));
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
            string pPath = Path.Combine("%TestBench%", "EncodedFile", "ExtractFileTests.script");
            pPath = StringEscaper.Preprocess(s, pPath);
            Script p = s.Project.LoadScriptMonkeyPatch(pPath);

            byte[] extractDigest;
            using (MemoryStream ms = EncodedFile.ExtractLogo(p, out ImageHelper.ImageType type))
            {
                Assert.IsTrue(type == ImageHelper.ImageType.Jpg);
                extractDigest = HashHelper.CalcHash(HashType.SHA256, ms);
            }

            string originFile = Path.Combine("%TestBench%", "EncodedFile", "Logo.jpg");
            originFile = StringEscaper.Preprocess(s, originFile);
            byte[] originDigest;
            using (FileStream fs = new FileStream(originFile, FileMode.Open))
            {
                originDigest = HashHelper.CalcHash(HashType.SHA256, fs);
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
            string pPath = Path.Combine("%TestBench%", "EncodedFile", "ExtractFileTests.script");
            pPath = StringEscaper.Preprocess(s, pPath);
            Script p = s.Project.LoadScriptMonkeyPatch(pPath);

            byte[] extractDigest;
            using (MemoryStream ms = EncodedFile.ExtractInterfaceEncoded(p, "PEBakeryAlphaMemory.jpg"))
            {
                extractDigest = HashHelper.CalcHash(HashType.SHA256, ms);
            }

            string originFile = Path.Combine("%TestBench%", "EncodedFile", "PEBakeryAlphaMemory.jpg");
            originFile = StringEscaper.Preprocess(s, originFile);
            byte[] originDigest;
            using (FileStream fs = new FileStream(originFile, FileMode.Open))
            {
                originDigest = HashHelper.CalcHash(HashType.SHA256, fs);
            }

            // Compare Hash
            Assert.IsTrue(originDigest.SequenceEqual(extractDigest));
        }
        #endregion
    }
}
