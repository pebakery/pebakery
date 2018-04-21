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
using PEBakery.IniLib;

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
            // AttachFile_Template("Type3.pdf", EncodedFile.EncodeMode.XZ); // Type 3
            AttachFile_Template("PEBakeryAlphaMemory.jpg", EncodedFile.EncodeMode.ZLib);
        }

        public void AttachFile_Template(string fileName, EncodedFile.EncodeMode encodeMode)
        {
            EngineState s = EngineTests.CreateEngineState();
            string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "EncodedFile"));
            string blankPath = Path.Combine(dirPath, "EncodeFileTests_Blank.script");
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
                Assert.IsTrue(folders.Count == 1);
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
                using (FileStream fs = new FileStream(originFile, FileMode.Open))
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

        #region ExtractFile
        [TestMethod]
        [TestCategory("EncodedFile")]
        public void EncodedFile_ExtractFile()
        {
            ExtractFile_Template("Type1.jpg"); // Type 1
            ExtractFile_Template("Type2.7z"); // Type 2
            // ExtractFile_Template("Type3.pdf"); // Type 3
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
            using (FileStream fs = new FileStream(originFile, FileMode.Open))
            {
                originDigest = HashHelper.CalcHash(HashHelper.HashType.SHA256, fs);
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
            using (FileStream fs = new FileStream(originFile, FileMode.Open))
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
            using (FileStream fs = new FileStream(originFile, FileMode.Open))
            {
                originDigest = HashHelper.CalcHash(HashHelper.HashType.SHA256, fs);
            }

            // Compare Hash
            Assert.IsTrue(originDigest.SequenceEqual(extractDigest));
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
    }
}
