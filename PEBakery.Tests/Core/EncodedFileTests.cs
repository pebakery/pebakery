/*
    Copyright (C) 2017-2018 Hajin Jang
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
using System.IO;
using System.Linq;
using PEBakery.Helper;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.Core;
using PEBakery.IniLib;
using PEBakery.LZ4Lib;
using PEBakery.XZLib;
using Joveler.ZLibWrapper;

namespace PEBakery.Tests.Core
{
    [TestClass]
    public class EncodedFileTests
    {
        #region Const Strings, String Factory
        private const string EncodedFolders = "EncodedFolders";
        private const string AuthorEncoded = "AuthorEncoded";
        private const string InterfaceEncoded = "InterfaceEncoded";
        private static string GetSectionName(string folderName, string fileName) => $"EncodedFile-{folderName}-{fileName}";
        #endregion

        #region AttachFile
        [TestMethod]
        [TestCategory("EncodedFile")]
        public void AttachFile()
        {
            void Template(string fileName, EncodedFile.EncodeMode encodeMode)
            {
                EngineState s = EngineTests.CreateEngineState();
                string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "EncodedFile"));
                string blankPath = Path.Combine(dirPath, "Blank.script");
                string scPath = Path.Combine(dirPath, "EncodeFileTests.script");
                File.Copy(blankPath, scPath, true);

                Script sc = s.Project.LoadScriptRuntime(scPath, new LoadScriptRuntimeOptions());

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

            Template("Type1.jpg", EncodedFile.EncodeMode.ZLib); // Type 1
            Template("Type2.7z", EncodedFile.EncodeMode.Raw); // Type 2
            Template("Type3.pdf", EncodedFile.EncodeMode.XZ); // Type 3
            Template("PEBakeryAlphaMemory.jpg", EncodedFile.EncodeMode.ZLib);
        }
        #endregion

        #region ContainsFile
        [TestMethod]
        [TestCategory("EncodedFile")]
        public void ContainsFile()
        {
            void Template(string scriptPath, string folderName, string fileName, bool result)
            {
                EngineState s = EngineTests.CreateEngineState();
                string pbOriginScript = Path.Combine("%TestBench%", "EncodedFile", scriptPath);
                string originScript = StringEscaper.Preprocess(s, pbOriginScript);

                Script sc = s.Project.LoadScriptRuntime(originScript, new LoadScriptRuntimeOptions());

                Assert.AreEqual(EncodedFile.ContainsFile(sc, folderName, fileName), result);
            }

            Template("ExtractFileTests.script", "FolderExample", "Type1.jpg", true);
            Template("ExtractFileTests.script", "FolderExample", "ShouldFail", false);
            Template("ExtractFileTests.script", "ShouldFail", "Type2.7z", false);
            Template("CompleteBlank.script", "ShouldFail", "ShouldFail", false);
        }
        #endregion

        #region ContainsLogo
        [TestMethod]
        [TestCategory("EncodedFile")]
        public void ContainsLogo()
        {
            void Template(string fileName, bool result)
            {
                EngineState s = EngineTests.CreateEngineState();
                string pbOriginScript = Path.Combine("%TestBench%", "EncodedFile", fileName);
                string originScript = StringEscaper.Preprocess(s, pbOriginScript);

                Script sc = s.Project.LoadScriptRuntime(originScript, new LoadScriptRuntimeOptions());

                Assert.AreEqual(EncodedFile.ContainsLogo(sc), result);
            }

            Template("Blank.script", true);
            Template("CompleteBlank.script", false);
        }
        #endregion

        #region AddFolder
        [TestMethod]
        [TestCategory("EncodedFile")]
        public void AddFolder()
        {
            void Template(string folderName, bool overwrite, bool result)
            {
                EngineState s = EngineTests.CreateEngineState();
                string pbOriginScript = Path.Combine("%TestBench%", "EncodedFile", "Blank.script");
                string originScript = StringEscaper.Preprocess(s, pbOriginScript);

                string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                string destScript = Path.Combine(destDir, "AddFolderTest.script");

                Directory.CreateDirectory(destDir);
                try
                {
                    File.Copy(originScript, destScript, true);

                    Script sc = s.Project.LoadScriptRuntime(destScript, new LoadScriptRuntimeOptions());
                    try
                    {
                        sc = EncodedFile.AddFolder(sc, folderName, overwrite);
                    }
                    catch (Exception e)
                    {
                        switch (e)
                        {
                            case InvalidOperationException _:
                                Assert.IsFalse(result);
                                return;
                            case ArgumentException _:
                                Assert.IsFalse(result);
                                return;
                        }
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

            Template("Wrong[String]", false, false);
            Template("Tab\tChar", false, false);
            Template("New\r\nLine", false, false);
            Template("Invalid?", false, false);
            Template("Invalid:", false, false);
        }
        #endregion

        #region ContainsFolder
        [TestMethod]
        [TestCategory("EncodedFile")]
        public void ContainsFolder()
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

                    Script sc = s.Project.LoadScriptRuntime(destScript, new LoadScriptRuntimeOptions());
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
        public void ExtractFile()
        {
            void Template(string fileName)
            { // Type 1
                EngineState s = EngineTests.CreateEngineState();
                string scPath = Path.Combine("%TestBench%", "EncodedFile", "ExtractFileTests.script");
                scPath = StringEscaper.Preprocess(s, scPath);
                Script sc = s.Project.LoadScriptRuntime(scPath, new LoadScriptRuntimeOptions());

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

            Template("Type1.jpg"); // Type 1
            Template("Type2.7z"); // Type 2
            Template("Type3.pdf"); // Type 3
        }
        #endregion

        #region ExtractFileInMem
        [TestMethod]
        [TestCategory("EncodedFile")]
        public void ExtractFileInMem()
        {
            void Template(string fileName)
            { // Type 1
                EngineState s = EngineTests.CreateEngineState();
                string scPath = Path.Combine("%TestBench%", "EncodedFile", "ExtractFileTests.script");
                scPath = StringEscaper.Preprocess(s, scPath);
                Script sc = s.Project.LoadScriptRuntime(scPath, new LoadScriptRuntimeOptions());

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

            Template("Type1.jpg"); // Type 1
            Template("Type2.7z"); // Type 2
            Template("Type3.pdf"); // Type 3
        }
        #endregion

        #region ExtractFolder
        [TestMethod]
        [TestCategory("EncodedFile")]
        public void ExtractFolder()
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
                    Script sc = s.Project.LoadScriptRuntime(originScript, new LoadScriptRuntimeOptions());

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
        public void ExtractLogo()
        {
            ExtractLogo_1();
        }

        public void ExtractLogo_1()
        { // Type 1
            EngineState s = EngineTests.CreateEngineState();
            string scPath = Path.Combine("%TestBench%", "EncodedFile", "ExtractFileTests.script");
            scPath = StringEscaper.Preprocess(s, scPath);
            Script sc = s.Project.LoadScriptRuntime(scPath, new LoadScriptRuntimeOptions());

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
        public void ExtractInterfaceEncoded()
        {
            ExtractInterfaceEncoded_1();
        }

        public void ExtractInterfaceEncoded_1()
        { // Type 1
            EngineState s = EngineTests.CreateEngineState();
            string scPath = Path.Combine("%TestBench%", "EncodedFile", "ExtractFileTests.script");
            scPath = StringEscaper.Preprocess(s, scPath);
            Script sc = s.Project.LoadScriptRuntime(scPath, new LoadScriptRuntimeOptions());

            byte[] extractDigest;
            using (MemoryStream ms = EncodedFile.ExtractInterface(sc, "PEBakeryAlphaMemory.jpg"))
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

        #region GetFileInfo, GetLogoInfo, GetFolderInfo, GetAllFilesInfo
        [TestMethod]
        [TestCategory("EncodedFile")]
        public void GetFileInfo()
        {
            // ReSharper disable once InconsistentNaming
            const string FolderExample = "FolderExample";
            const string folderExample = "folderExample";

            EngineState s = EngineTests.CreateEngineState();
            string pbOriginScript = Path.Combine("%TestBench%", "EncodedFile", "ExtractFileTests.script");
            string originScript = StringEscaper.Preprocess(s, pbOriginScript);
            Script sc = s.Project.LoadScriptRuntime(originScript, new LoadScriptRuntimeOptions());

            // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
            void Template(string fileName, bool detail, EncodedFileInfo comp)
            {
                (EncodedFileInfo info, string errMsg) = EncodedFile.GetFileInfo(sc, folderExample, fileName, detail);
                Assert.IsNull(errMsg);
                Assert.IsTrue(comp.Equals(info));
            }

            Template("Type1.jpg", true, new EncodedFileInfo
            {
                DirName = FolderExample,
                FileName = "Type1.jpg",
                RawSize = 7683,
                EncodedSize = 10244,
                EncodeMode = EncodedFile.EncodeMode.ZLib
            });
            Template("type1.jpg", false, new EncodedFileInfo
            {
                DirName = folderExample,
                FileName = "Type1.jpg",
                RawSize = 7683,
                EncodedSize = 10244,
                EncodeMode = null
            });

            Template("Type2.7z", true, new EncodedFileInfo
            {
                DirName = FolderExample,
                FileName = "Type2.7z",
                RawSize = 1631,
                EncodedSize = 2175,
                EncodeMode = EncodedFile.EncodeMode.Raw
            });
            Template("Type2.7z", false, new EncodedFileInfo
            {
                DirName = FolderExample,
                FileName = "type2.7z",
                RawSize = 1631,
                EncodedSize = 2175,
                EncodeMode = null
            });

            Template("Type3.pdf", true, new EncodedFileInfo
            {
                DirName = FolderExample,
                FileName = "Type3.pdf",
                RawSize = 88692,
                EncodedSize = 102908,
                EncodeMode = EncodedFile.EncodeMode.XZ
            });
            Template("Type3.pdf", false, new EncodedFileInfo
            {
                DirName = folderExample,
                FileName = "type3.pdf",
                RawSize = 88692,
                EncodedSize = 102908,
                EncodeMode = null
            });
        }

        [TestMethod]
        [TestCategory("EncodedFile")]
        public void GetLogoInfo()
        {
            EngineState s = EngineTests.CreateEngineState();
            string scriptDir = Path.Combine(StringEscaper.Preprocess(s, "%TestBench%"), "EncodedFile");

            string logoScriptFile = Path.Combine(scriptDir, "Blank.script");
            Script logoScript = s.Project.LoadScriptRuntime(logoScriptFile, new LoadScriptRuntimeOptions());

            string noLogoScriptFile = Path.Combine(scriptDir, "CompleteBlank.script");
            Script noLogoScript = s.Project.LoadScriptRuntime(noLogoScriptFile, new LoadScriptRuntimeOptions());

            // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
            void Template(Script testScript, bool detail, EncodedFileInfo comp)
            {
                (EncodedFileInfo info, string errMsg) = EncodedFile.GetLogoInfo(testScript, detail);
                if (comp == null)
                {
                    Assert.IsNotNull(errMsg);
                    Assert.IsNull(info);
                }
                else
                {
                    Assert.IsNull(errMsg);
                    Assert.IsTrue(info.Equals(comp));
                }
            }

            Template(logoScript, true, new EncodedFileInfo
            {
                DirName = "AuthorEncoded",
                FileName = "logo.jpg",
                RawSize = 973,
                EncodedSize = 1298,
                EncodeMode = EncodedFile.EncodeMode.ZLib
            });
            Template(logoScript, false, new EncodedFileInfo
            {
                DirName = "authorEncoded",
                FileName = "Logo.jpg",
                RawSize = 973,
                EncodedSize = 1298,
                EncodeMode = null
            });

            Template(noLogoScript, true, null);
            Template(noLogoScript, false, null);
        }

        [TestMethod]
        [TestCategory("EncodedFile")]
        public void GetFolderInfo()
        {
            // ReSharper disable once InconsistentNaming
            const string FolderExample = "FolderExample";

            EngineState s = EngineTests.CreateEngineState();
            string pbOriginScript = Path.Combine("%TestBench%", "EncodedFile", "ExtractFileTests.script");
            string originScript = StringEscaper.Preprocess(s, pbOriginScript);

            Script sc = s.Project.LoadScriptRuntime(originScript, new LoadScriptRuntimeOptions());

            // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
            void Template(bool detail, List<EncodedFileInfo> comps)
            {
                (List<EncodedFileInfo> infos, string errMsg) = EncodedFile.GetFolderInfo(sc, FolderExample, detail);
                Assert.IsNull(errMsg);
                Assert.AreEqual(comps.Count, infos.Count);
                for (int i = 0; i < comps.Count; i++)
                    Assert.IsTrue(comps[i].Equals(infos[i]));
            }

            List<EncodedFileInfo> compDetailList = new List<EncodedFileInfo>
            {
                new EncodedFileInfo
                {
                    DirName = FolderExample,
                    FileName = "Type1.jpg",
                    RawSize = 7683,
                    EncodedSize = 10244,
                    EncodeMode = EncodedFile.EncodeMode.ZLib
                },
                new EncodedFileInfo
                {
                    DirName = FolderExample,
                    FileName = "Type2.7z",
                    RawSize = 1631,
                    EncodedSize = 2175,
                    EncodeMode = EncodedFile.EncodeMode.Raw
                },
                new EncodedFileInfo
                {
                    DirName = FolderExample,
                    FileName = "Type3.pdf",
                    RawSize = 88692,
                    EncodedSize = 102908,
                    EncodeMode = EncodedFile.EncodeMode.XZ
                }
            };

            List<EncodedFileInfo> compNoDetailList = new List<EncodedFileInfo>();
            foreach (EncodedFileInfo info in compDetailList)
            {
                EncodedFileInfo clone = info.Clone() as EncodedFileInfo;
                Assert.IsTrue(clone != null);
                clone.EncodeMode = null;
                compNoDetailList.Add(clone);
            }
            
            Template(true, compDetailList);
            Template(false, compNoDetailList);
        }

        [TestMethod]
        [TestCategory("EncodedFile")]
        public void GetAllFilesInfo()
        {
            // ReSharper disable once InconsistentNaming
            const string FolderExample = "FolderExample";

            EngineState s = EngineTests.CreateEngineState();
            string pbOriginScript = Path.Combine("%TestBench%", "EncodedFile", "ExtractFileTests.script");
            string originScript = StringEscaper.Preprocess(s, pbOriginScript);

            Script sc = s.Project.LoadScriptRuntime(originScript, new LoadScriptRuntimeOptions());

            // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
            void Template(bool detail, Dictionary<string, List<EncodedFileInfo>> compDict)
            {
                (Dictionary<string, List<EncodedFileInfo>> infoDict, string errMsg) = EncodedFile.GetAllFilesInfo(sc, detail);
                Assert.IsNull(errMsg);
                Assert.AreEqual(compDict.Count, infoDict.Count);
                foreach (var kv in compDict)
                {
                    Assert.IsTrue(infoDict.ContainsKey(kv.Key));
                    Assert.AreEqual(kv.Value.Count, infoDict[kv.Key].Count);
                    foreach (EncodedFileInfo fileInfo in kv.Value)
                        Assert.IsTrue(infoDict[kv.Key].Contains(fileInfo));
                } 
            }

            Dictionary<string, List<EncodedFileInfo>> compDetailDict = new Dictionary<string, List<EncodedFileInfo>>
            {
                ["FolderExample"] = new List<EncodedFileInfo>
                {
                    new EncodedFileInfo
                    {
                        DirName = FolderExample,
                        FileName = "Type1.jpg",
                        RawSize = 7683,
                        EncodedSize = 10244,
                        EncodeMode = EncodedFile.EncodeMode.ZLib
                    },
                    new EncodedFileInfo
                    {
                        DirName = FolderExample,
                        FileName = "Type2.7z",
                        RawSize = 1631,
                        EncodedSize = 2175,
                        EncodeMode = EncodedFile.EncodeMode.Raw
                    },
                    new EncodedFileInfo
                    {
                        DirName = FolderExample,
                        FileName = "Type3.pdf",
                        RawSize = 88692,
                        EncodedSize = 102908,
                        EncodeMode = EncodedFile.EncodeMode.XZ
                    }
                },
                ["FolderRun"] = new List<EncodedFileInfo>
                {
                    new EncodedFileInfo
                    {
                        DirName = "FolderRun",
                        FileName = "TestBatch.cmd",
                        RawSize = 34,
                        EncodedSize = 144,
                        EncodeMode = EncodedFile.EncodeMode.Raw
                    }
                },
                ["BannerImage"] = new List<EncodedFileInfo>
                {
                    new EncodedFileInfo
                    {
                        DirName = "BannerImage",
                        FileName = "Banner.bmp",
                        RawSize = 17626,
                        EncodedSize = 23502,
                        EncodeMode = EncodedFile.EncodeMode.ZLib
                    },
                    new EncodedFileInfo
                    {
                        DirName = "BannerImage",
                        FileName = "Banner.svg",
                        RawSize = 4715,
                        EncodedSize = 6287,
                        EncodeMode = EncodedFile.EncodeMode.ZLib
                    },
                }
            };

            Dictionary<string, List<EncodedFileInfo>> compNoDetailDict = new Dictionary<string, List<EncodedFileInfo>>();
            foreach (var kv in compDetailDict)
            {
                compNoDetailDict[kv.Key] = new List<EncodedFileInfo>();
                foreach (EncodedFileInfo info in kv.Value)
                {
                    EncodedFileInfo clone = info.Clone() as EncodedFileInfo;
                    Assert.IsTrue(clone != null);

                    clone.EncodeMode = null;

                    compNoDetailDict[kv.Key].Add(clone);
                }
            }

            Template(true, compDetailDict);
            Template(false, compNoDetailDict);
        }
        #endregion

        #region DeleteFile, DeleteFolder, DeleteLogo
        [TestMethod]
        [TestCategory("EncodedFile")]
        public void DeleteFile()
        {
            EngineState s = EngineTests.CreateEngineState();
            string originScriptPath = Path.Combine(StringEscaper.Preprocess(s, "%TestBench%"), "EncodedFile", "ExtractFileTests.script");

            // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
            void Template(string folderName, string fileName, bool result)
            {
                string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                string destScript = Path.Combine(destDir, "DeleteFileTest.script");

                Directory.CreateDirectory(destDir);
                try
                {
                    File.Copy(originScriptPath, destScript, true);

                    Script sc = s.Project.LoadScriptRuntime(destScript, new LoadScriptRuntimeOptions());

                    string errMsg;
                    (sc, errMsg) = EncodedFile.DeleteFile(sc, folderName, fileName);
                    if (errMsg != null)
                    {
                        Assert.IsFalse(result);
                        return;
                    }

                    Assert.IsTrue(result);
                    
                    Assert.IsFalse(sc.Sections.ContainsKey(GetSectionName(folderName, fileName)));

                    Dictionary<string, string> fileDict;
                    switch (sc.Sections[folderName].DataType)
                    {
                        case SectionDataType.IniDict:
                            fileDict = sc.Sections[folderName].GetIniDict();
                            break;
                        case SectionDataType.Lines:
                            fileDict = Ini.ParseIniLinesIniStyle(sc.Sections[folderName].GetLines());
                            break;
                        default:
                            throw new InternalException("Internal Logic Error at EncodedFile.ExtractFolder");
                    }

                    Assert.IsFalse(fileDict.ContainsKey(fileName));
                }
                finally
                {
                    if (Directory.Exists(destDir))
                        Directory.Delete(destDir, true);
                }
            }

            Template("FolderExample", "Type1.jpg", true);
            Template("FolderExample", "Type2.7z", true);
            Template("FolderExample", "Type3.pdf", true);
            Template(AuthorEncoded, "Logo.jpg", true);
            Template(InterfaceEncoded, "PEBakeryAlphaMemory.jpg", true);

            Template("BannerImage", "Should.fail", false);
            Template("ShouldFail", "Should.fail", false);
        }

        [TestMethod]
        [TestCategory("EncodedFile")]
        public void DeleteFolder()
        {
            EngineState s = EngineTests.CreateEngineState();
            string originScriptPath = Path.Combine(StringEscaper.Preprocess(s, "%TestBench%"), "EncodedFile", "ExtractFileTests.script");

            // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
            void Template(string folderName, bool result)
            {
                string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                string destScript = Path.Combine(destDir, "DeleteFolderTest.script");

                Directory.CreateDirectory(destDir);
                try
                {
                    File.Copy(originScriptPath, destScript, true);

                    Script sc = s.Project.LoadScriptRuntime(destScript, new LoadScriptRuntimeOptions());

                    Dictionary<string, string> fileDict = null;
                    if (result)
                    {
                        switch (sc.Sections[folderName].DataType)
                        {
                            case SectionDataType.IniDict:
                                fileDict = sc.Sections[folderName].GetIniDict();
                                break;
                            case SectionDataType.Lines:
                                fileDict = Ini.ParseIniLinesIniStyle(sc.Sections[folderName].GetLines());
                                break;
                            default:
                                throw new InternalException("Internal Logic Error at EncodedFile.ExtractFolder");
                        }
                    }

                    string errMsg;
                    (sc, errMsg) = EncodedFile.DeleteFolder(sc, folderName);

                    if (errMsg != null)
                    {
                        Assert.IsFalse(result);
                        return;
                    }
                    Assert.IsTrue(result);

                    Assert.IsFalse(sc.Sections.ContainsKey(folderName));
                    Assert.IsFalse(Ini.ContainsSection(destScript, folderName));

                    List<string> folders = sc.Sections[EncodedFolders].GetLinesOnce();
                    Assert.IsFalse(folders.Contains(folderName, StringComparer.OrdinalIgnoreCase));

                    foreach (string fileName in fileDict.Keys)
                    {
                        Assert.IsFalse(sc.Sections.ContainsKey(GetSectionName(folderName, fileName)));
                    }
                }
                finally
                {
                    if (Directory.Exists(destDir))
                        Directory.Delete(destDir, true);
                }
            }

            Template("FolderExample", true);
            Template("BannerImage", true);
            Template(AuthorEncoded, true);
            Template(InterfaceEncoded, true);
            Template("ShouldFail", false);
        }

        [TestMethod]
        [TestCategory("EncodedFile")]
        public void DeleteLogo()
        {
            EngineState s = EngineTests.CreateEngineState();
            string scriptDir = Path.Combine(StringEscaper.Preprocess(s, "%TestBench%"), "EncodedFile");
            string logoScriptPath = Path.Combine(scriptDir, "Blank.script");
            string noLogoScriptPath = Path.Combine(scriptDir, "CompleteBlank.script");

            // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
            void Template(string testScriptPath, bool result)
            {
                string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                string destScript = Path.Combine(destDir, "DeleteLogoTest.script");

                Directory.CreateDirectory(destDir);
                try
                {
                    File.Copy(testScriptPath, destScript, true);

                    string errMsg;
                    Script sc = s.Project.LoadScriptRuntime(destScript, new LoadScriptRuntimeOptions());
                    (sc, errMsg) = EncodedFile.DeleteLogo(sc);

                    if (errMsg != null)
                    {
                        Assert.IsFalse(result);
                        return;
                    }
                    Assert.IsTrue(result);

                    Assert.IsFalse(EncodedFile.ContainsLogo(sc));
                }
                finally
                {
                    if (Directory.Exists(destDir))
                        Directory.Delete(destDir, true);
                }
            }

            Template(logoScriptPath, true);
            Template(noLogoScriptPath, false);
        }
        #endregion

        #region SplitBase64
        [TestMethod]
        [TestCategory("EncodedFile")]
        public void Base64Encode()
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
        public void Base64Decode()
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
    }
}
