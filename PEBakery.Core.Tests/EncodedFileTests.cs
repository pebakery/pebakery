/*
    Copyright (C) 2017-2019 Hajin Jang
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.Helper;
using PEBakery.Ini;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;

namespace PEBakery.Core.Tests
{
    [TestClass]
    [TestCategory(nameof(EncodedFile))]
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
        public void AttachFile()
        {
            void Template(string fileName, EncodedFile.EncodeMode encodeMode)
            {
                EngineState s = EngineTests.CreateEngineState();
                string srcDir = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "EncodedFile"));
                string srcScript = Path.Combine(srcDir, "Blank.script");
                string destDir = FileHelper.GetTempDir();
                string destScript = Path.Combine(destDir, "EncodeFileTests.script");
                File.Copy(srcScript, destScript, true);
                try
                {
                    Script sc = s.Project.LoadScriptRuntime(destScript, new LoadScriptRuntimeOptions());

                    string originFile = Path.Combine(srcDir, fileName);
                    EncodedFile.AttachFile(sc, "FolderExample", fileName, originFile, encodeMode, null);

                    // Check whether file was successfully encoded
                    Assert.IsTrue(sc.Sections.ContainsKey("EncodedFolders"));
                    string[] folders = sc.Sections["EncodedFolders"].Lines
                        .Where(x => x.Length != 0)
                        .ToArray();
                    Assert.IsTrue(folders.Length == 2);
                    Assert.IsTrue(folders[0].Equals("FolderExample", StringComparison.Ordinal));

                    Assert.IsTrue(sc.Sections.ContainsKey("FolderExample"));
                    string[] fileInfos = sc.Sections["FolderExample"].Lines
                        .Where(x => x.Length != 0)
                        .ToArray();
                    Assert.IsTrue(fileInfos[0].StartsWith($"{fileName}=", StringComparison.Ordinal));

                    Assert.IsTrue(sc.Sections.ContainsKey($"EncodedFile-FolderExample-{fileName}"));
                    string[] encodedFile = sc.Sections[$"EncodedFile-FolderExample-{fileName}"].Lines
                        .Where(x => x.Length != 0)
                        .ToArray();
                    Assert.IsTrue(1 < encodedFile.Length);
                    Assert.IsTrue(encodedFile[0].StartsWith("lines=", StringComparison.Ordinal));

                    // Check whether file can be successfully extracted
                    byte[] extractDigest;
                    using (MemoryStream ms = new MemoryStream())
                    {
                        EncodedFile.ExtractFile(sc, "FolderExample", fileName, ms, null);
                        ms.Position = 0;
                        extractDigest = HashHelper.GetHash(HashHelper.HashType.SHA256, ms);
                    }

                    byte[] originDigest;
                    using (FileStream fs = new FileStream(originFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        originDigest = HashHelper.GetHash(HashHelper.HashType.SHA256, fs);
                    }

                    Assert.IsTrue(originDigest.SequenceEqual(extractDigest));
                }
                finally
                {
                    if (Directory.Exists(destDir))
                        Directory.Delete(destDir, true);
                    if (File.Exists(destScript))
                        File.Delete(destScript);
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
        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<보류 중>")]
        public void AddFolder()
        {
            void Template(string folderName, bool overwrite, bool result)
            {
                EngineState s = EngineTests.CreateEngineState();
                string pbOriginScript = Path.Combine("%TestBench%", "EncodedFile", "Blank.script");
                string originScript = StringEscaper.Preprocess(s, pbOriginScript);

                string destDir = FileHelper.GetTempDir();
                string destScript = Path.Combine(destDir, "AddFolderTest.script");

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
                    Assert.AreEqual(IniReadWriter.ContainsSection(destScript, folderName), result);

                    if (!folderName.Equals(AuthorEncoded, StringComparison.OrdinalIgnoreCase) &&
                        !folderName.Equals(InterfaceEncoded, StringComparison.OrdinalIgnoreCase))
                    {
                        string[] folders = sc.Sections[EncodedFolders].Lines;
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
        public void ExtractFile()
        {
            void Template(string fileName)
            { // Type 1
                EngineState s = EngineTests.CreateEngineState();
                string srcScript = Path.Combine("%TestBench%", "EncodedFile", "ExtractFileTests.script");
                srcScript = StringEscaper.Preprocess(s, srcScript);
                Script sc = s.Project.LoadScriptRuntime(srcScript, new LoadScriptRuntimeOptions());

                byte[] extractDigest;
                using (MemoryStream ms = new MemoryStream())
                {
                    EncodedFile.ExtractFile(sc, "FolderExample", fileName, ms, null);
                    ms.Position = 0;
                    extractDigest = HashHelper.GetHash(HashHelper.HashType.SHA256, ms);
                }

                string originFile = Path.Combine("%TestBench%", "EncodedFile", fileName);
                originFile = StringEscaper.Preprocess(s, originFile);
                byte[] originDigest;
                using (FileStream fs = new FileStream(originFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    originDigest = HashHelper.GetHash(HashHelper.HashType.SHA256, fs);
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
        public void ExtractFileInMem()
        {
            void Template(string fileName)
            { // Type 1
                EngineState s = EngineTests.CreateEngineState();
                string srcScript = Path.Combine("%TestBench%", "EncodedFile", "ExtractFileTests.script");
                srcScript = StringEscaper.Preprocess(s, srcScript);
                Script sc = s.Project.LoadScriptRuntime(srcScript, new LoadScriptRuntimeOptions());

                byte[] extractDigest;
                using (MemoryStream ms = EncodedFile.ExtractFileInMem(sc, "FolderExample", fileName))
                {
                    ms.Position = 0;
                    extractDigest = HashHelper.GetHash(HashHelper.HashType.SHA256, ms);
                }

                string originFile = Path.Combine("%TestBench%", "EncodedFile", fileName);
                originFile = StringEscaper.Preprocess(s, originFile);
                byte[] originDigest;
                using (FileStream fs = new FileStream(originFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    originDigest = HashHelper.GetHash(HashHelper.HashType.SHA256, fs);
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
        public void ExtractFolder()
        {
            EngineState s = EngineTests.CreateEngineState();

            void Template(string folderName)
            {
                string pbOriginScript = Path.Combine("%TestBench%", "EncodedFile", "ExtractFileTests.script");
                string originScript = StringEscaper.Preprocess(s, pbOriginScript);

                string destDir = FileHelper.GetTempDir();
                try
                {
                    Script sc = s.Project.LoadScriptRuntime(originScript, new LoadScriptRuntimeOptions());

                    EncodedFile.ExtractFolder(sc, folderName, destDir);

                    string[] comps = IniReadWriter.ParseIniLinesIniStyle(sc.Sections[folderName].Lines).Keys.ToArray();
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
        public void ExtractLogo()
        { // Type 1
            EngineState s = EngineTests.CreateEngineState();
            string scPath = Path.Combine("%TestBench%", "EncodedFile", "ExtractFileTests.script");
            scPath = StringEscaper.Preprocess(s, scPath);
            Script sc = s.Project.LoadScriptRuntime(scPath, new LoadScriptRuntimeOptions());

            byte[] extractDigest;
            using (MemoryStream ms = EncodedFile.ExtractLogo(sc, out ImageHelper.ImageFormat type, out _))
            {
                Assert.IsTrue(type == ImageHelper.ImageFormat.Jpg);
                extractDigest = HashHelper.GetHash(HashHelper.HashType.SHA256, ms);
            }

            string originFile = Path.Combine("%TestBench%", "EncodedFile", "Logo.jpg");
            originFile = StringEscaper.Preprocess(s, originFile);
            byte[] originDigest;
            using (FileStream fs = new FileStream(originFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                originDigest = HashHelper.GetHash(HashHelper.HashType.SHA256, fs);
            }

            // Compare Hash
            Assert.IsTrue(originDigest.SequenceEqual(extractDigest));
        }
        #endregion

        #region ExtractInterfaceEncoded
        [TestMethod]
        public void ExtractInterfaceEncoded()
        { // Type 1
            EngineState s = EngineTests.CreateEngineState();
            string scPath = Path.Combine("%TestBench%", "EncodedFile", "ExtractFileTests.script");
            scPath = StringEscaper.Preprocess(s, scPath);
            Script sc = s.Project.LoadScriptRuntime(scPath, new LoadScriptRuntimeOptions());

            byte[] extractDigest;
            using (MemoryStream ms = EncodedFile.ExtractInterface(sc, "PEBakeryAlphaMemory.jpg"))
            {
                extractDigest = HashHelper.GetHash(HashHelper.HashType.SHA256, ms);
            }

            string originFile = Path.Combine("%TestBench%", "EncodedFile", "PEBakeryAlphaMemory.jpg");
            originFile = StringEscaper.Preprocess(s, originFile);
            byte[] originDigest;
            using (FileStream fs = new FileStream(originFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                originDigest = HashHelper.GetHash(HashHelper.HashType.SHA256, fs);
            }

            // Compare Hash
            Assert.IsTrue(originDigest.SequenceEqual(extractDigest));
        }
        #endregion

        #region GetFileInfo, GetLogoInfo, GetFolderInfo, GetAllFilesInfo
        [TestMethod]
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
                ResultReport<EncodedFileInfo> report = EncodedFile.GetFileInfo(sc, folderExample, fileName, detail);
                Assert.IsTrue(report.Success);
                Assert.IsTrue(comp.Equals(report.Result));
            }

            Template("Type1.jpg", true, new EncodedFileInfo
            {
                FolderName = FolderExample,
                FileName = "Type1.jpg",
                RawSize = 7683,
                EncodedSize = 10244,
                EncodeMode = EncodedFile.EncodeMode.ZLib
            });
            Template("type1.jpg", false, new EncodedFileInfo
            {
                FolderName = folderExample,
                FileName = "Type1.jpg",
                RawSize = 7683,
                EncodedSize = 10244,
                EncodeMode = null
            });

            Template("Type2.7z", true, new EncodedFileInfo
            {
                FolderName = FolderExample,
                FileName = "Type2.7z",
                RawSize = 1631,
                EncodedSize = 2175,
                EncodeMode = EncodedFile.EncodeMode.Raw
            });
            Template("Type2.7z", false, new EncodedFileInfo
            {
                FolderName = FolderExample,
                FileName = "type2.7z",
                RawSize = 1631,
                EncodedSize = 2175,
                EncodeMode = null
            });

            Template("Type3.pdf", true, new EncodedFileInfo
            {
                FolderName = FolderExample,
                FileName = "Type3.pdf",
                RawSize = 88692,
                EncodedSize = 102908,
                EncodeMode = EncodedFile.EncodeMode.XZ
            });
            Template("Type3.pdf", false, new EncodedFileInfo
            {
                FolderName = folderExample,
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
                ResultReport<EncodedFileInfo> report = EncodedFile.GetLogoInfo(testScript, detail);
                if (comp == null)
                {
                    Assert.IsFalse(report.Success);
                    Assert.IsNull(report.Result);
                }
                else
                {
                    Assert.IsTrue(report.Success);
                    Assert.IsTrue(report.Result.Equals(comp));
                }
            }

            Template(logoScript, true, new EncodedFileInfo
            {
                FolderName = "AuthorEncoded",
                FileName = "logo.jpg",
                RawSize = 973,
                EncodedSize = 1298,
                EncodeMode = EncodedFile.EncodeMode.ZLib
            });
            Template(logoScript, false, new EncodedFileInfo
            {
                FolderName = "authorEncoded",
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
                ResultReport<EncodedFileInfo[]> report = EncodedFile.GetFolderInfo(sc, FolderExample, detail);
                Assert.IsTrue(report.Success);
                EncodedFileInfo[] infos = report.Result;
                Assert.AreEqual(comps.Count, infos.Length);
                for (int i = 0; i < comps.Count; i++)
                    Assert.IsTrue(comps[i].Equals(infos[i]));
            }

            List<EncodedFileInfo> compDetailList = new List<EncodedFileInfo>
            {
                new EncodedFileInfo
                {
                    FolderName = FolderExample,
                    FileName = "Type1.jpg",
                    RawSize = 7683,
                    EncodedSize = 10244,
                    EncodeMode = EncodedFile.EncodeMode.ZLib
                },
                new EncodedFileInfo
                {
                    FolderName = FolderExample,
                    FileName = "Type2.7z",
                    RawSize = 1631,
                    EncodedSize = 2175,
                    EncodeMode = EncodedFile.EncodeMode.Raw
                },
                new EncodedFileInfo
                {
                    FolderName = FolderExample,
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
            void Template(bool inspectEncodedSize, Dictionary<string, List<EncodedFileInfo>> compDict)
            {
                EncodedFile.GetFileInfoOptions opts = new EncodedFile.GetFileInfoOptions
                {
                    InspectEncodeMode = inspectEncodedSize,
                };
                ResultReport<Dictionary<string, List<EncodedFileInfo>>> report = EncodedFile.GetAllFilesInfo(sc, opts);
                Assert.IsTrue(report.Success);
                Dictionary<string, List<EncodedFileInfo>> infoDict = report.Result;
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
                        FolderName = FolderExample,
                        FileName = "Type1.jpg",
                        RawSize = 7683,
                        EncodedSize = 10244,
                        EncodeMode = EncodedFile.EncodeMode.ZLib
                    },
                    new EncodedFileInfo
                    {
                        FolderName = FolderExample,
                        FileName = "Type2.7z",
                        RawSize = 1631,
                        EncodedSize = 2175,
                        EncodeMode = EncodedFile.EncodeMode.Raw
                    },
                    new EncodedFileInfo
                    {
                        FolderName = FolderExample,
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
                        FolderName = "FolderRun",
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
                        FolderName = "BannerImage",
                        FileName = "Banner.bmp",
                        RawSize = 17626,
                        EncodedSize = 23502,
                        EncodeMode = EncodedFile.EncodeMode.ZLib
                    },
                    new EncodedFileInfo
                    {
                        FolderName = "BannerImage",
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

        #region RenameFile, RenameFolder
        [TestMethod]
        public void RenameFile()
        {
            EngineState s = EngineTests.CreateEngineState();
            string originScriptPath = Path.Combine(StringEscaper.Preprocess(s, "%TestBench%"), "EncodedFile", "ExtractFileTests.script");

            // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
            void Template(string folderName, string oldFileName, string newFileName, bool result)
            {
                string destDir = FileHelper.GetTempDir();
                string destScript = Path.Combine(destDir, "RenameFileTest.script");

                try
                {
                    File.Copy(originScriptPath, destScript, true);

                    Script sc = s.Project.LoadScriptRuntime(destScript, new LoadScriptRuntimeOptions());

                    string errMsg;
                    (sc, errMsg) = EncodedFile.RenameFile(sc, folderName, oldFileName, newFileName);
                    if (errMsg != null)
                    {
                        Assert.IsFalse(result);
                        return;
                    }

                    Assert.IsTrue(result);

                    Assert.IsFalse(sc.Sections.ContainsKey(GetSectionName(folderName, oldFileName)));
                    Assert.IsTrue(sc.Sections.ContainsKey(GetSectionName(folderName, newFileName)));

                    Dictionary<string, string> fileDict = sc.Sections[folderName].IniDict;
                    Assert.IsFalse(fileDict.ContainsKey(oldFileName));
                    Assert.IsTrue(fileDict.ContainsKey(newFileName));
                }
                finally
                {
                    if (Directory.Exists(destDir))
                        Directory.Delete(destDir, true);
                }
            }

            Template("FolderExample", "Type1.jpg", "JPEG.jpg", true);
            Template("FolderExample", "Type2.7z", "LZMA2.7z", true);
            Template("FolderExample", "Type3.pdf", "Postscript.pdf", true);
            Template(AuthorEncoded, "Logo.jpg", "L.jpg", true);
            Template(InterfaceEncoded, "PEBakeryAlphaMemory.jpg", "P.jpg", true);

            Template("BannerImage", "Should.fail", "Should.fail.2", false);
            Template("ShouldFail", "Should.fail", "Should.fail.2", false);
        }

        [TestMethod]
        public void RenameFolder()
        {
            EngineState s = EngineTests.CreateEngineState();
            string originScriptPath = Path.Combine(StringEscaper.Preprocess(s, "%TestBench%"), "EncodedFile", "ExtractFileTests.script");

            // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
            void Template(string oldFolderName, string newFolderName, bool result)
            {
                string destDir = FileHelper.GetTempDir();
                string destScript = Path.Combine(destDir, "RenameFolderTest.script");

                try
                {
                    File.Copy(originScriptPath, destScript, true);

                    Script sc = s.Project.LoadScriptRuntime(destScript, new LoadScriptRuntimeOptions());

                    Dictionary<string, string> fileDict = null;
                    if (result)
                        fileDict = sc.Sections[oldFolderName].IniDict;

                    string errMsg;
                    (sc, errMsg) = EncodedFile.RenameFolder(sc, oldFolderName, newFolderName);

                    if (errMsg != null)
                    {
                        Assert.IsFalse(result);
                        return;
                    }
                    Assert.IsTrue(result);

                    Assert.IsFalse(sc.Sections.ContainsKey(oldFolderName));
                    Assert.IsTrue(sc.Sections.ContainsKey(newFolderName));
                    Assert.IsFalse(IniReadWriter.ContainsSection(destScript, oldFolderName));
                    Assert.IsTrue(IniReadWriter.ContainsSection(destScript, newFolderName));

                    string[] folders = sc.Sections[EncodedFolders].Lines;
                    Assert.IsFalse(folders.Contains(oldFolderName, StringComparer.OrdinalIgnoreCase));
                    Assert.IsTrue(folders.Contains(newFolderName, StringComparer.OrdinalIgnoreCase));

                    foreach (string fileName in fileDict.Keys)
                    {
                        Assert.IsFalse(sc.Sections.ContainsKey(GetSectionName(oldFolderName, fileName)));
                        Assert.IsTrue(sc.Sections.ContainsKey(GetSectionName(newFolderName, fileName)));
                    }
                }
                finally
                {
                    if (Directory.Exists(destDir))
                        Directory.Delete(destDir, true);
                }
            }

            Template("FolderExample", "RenamedExample", true);
            Template("BannerImage", "BannerRenamed", true);
            Template(AuthorEncoded, "Hello", false);
            Template(InterfaceEncoded, "World", false);
            Template("FolderExample", AuthorEncoded, false);
            Template("BannerImage", InterfaceEncoded, false);
            Template("ShouldFail", "ShouldNotRename", false);
        }
        #endregion

        #region DeleteFile, DeleteFolder, DeleteLogo
        [TestMethod]
        public void DeleteFile()
        {
            EngineState s = EngineTests.CreateEngineState();
            string originScriptPath = Path.Combine(StringEscaper.Preprocess(s, "%TestBench%"), "EncodedFile", "ExtractFileTests.script");

            // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
            void Template(string folderName, string fileName, bool result)
            {
                string destDir = FileHelper.GetTempDir();
                string destScript = Path.Combine(destDir, "DeleteFileTest.script");

                try
                {
                    File.Copy(originScriptPath, destScript, true);

                    Script sc = s.Project.LoadScriptRuntime(destScript, new LoadScriptRuntimeOptions());

                    ResultReport<Script> report = EncodedFile.DeleteFile(sc, folderName, fileName);
                    if (!report.Success)
                    {
                        Assert.IsFalse(result);
                        return;
                    }
                    Assert.IsTrue(result);

                    sc = report.Result;
                    Assert.IsFalse(sc.Sections.ContainsKey(GetSectionName(folderName, fileName)));

                    Dictionary<string, string> fileDict = sc.Sections[folderName].IniDict;
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
        public void DeleteFolder()
        {
            EngineState s = EngineTests.CreateEngineState();
            string originScriptPath = Path.Combine(StringEscaper.Preprocess(s, "%TestBench%"), "EncodedFile", "ExtractFileTests.script");

            // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
            void Template(string folderName, bool result)
            {
                string destDir = FileHelper.GetTempDir();
                string destScript = Path.Combine(destDir, "DeleteFolderTest.script");

                try
                {
                    File.Copy(originScriptPath, destScript, true);

                    Script sc = s.Project.LoadScriptRuntime(destScript, new LoadScriptRuntimeOptions());

                    Dictionary<string, string> fileDict = null;
                    if (result)
                        fileDict = sc.Sections[folderName].IniDict;

                    ResultReport<Script> report = EncodedFile.DeleteFolder(sc, folderName);
                    if (!report.Success)
                    {
                        Assert.IsFalse(result);
                        return;
                    }
                    Assert.IsTrue(result);

                    sc = report.Result;
                    Assert.IsFalse(sc.Sections.ContainsKey(folderName));
                    Assert.IsFalse(IniReadWriter.ContainsSection(destScript, folderName));

                    string[] folders = sc.Sections[EncodedFolders].Lines;
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
        public void DeleteLogo()
        {
            EngineState s = EngineTests.CreateEngineState();
            string scriptDir = Path.Combine(StringEscaper.Preprocess(s, "%TestBench%"), "EncodedFile");
            string logoScriptPath = Path.Combine(scriptDir, "Blank.script");
            string noLogoScriptPath = Path.Combine(scriptDir, "CompleteBlank.script");

            // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
            void Template(string testScriptPath, bool result)
            {
                string destDir = FileHelper.GetTempDir();
                string destScript = Path.Combine(destDir, "DeleteLogoTest.script");

                try
                {
                    File.Copy(testScriptPath, destScript, true);

                    Script sc = s.Project.LoadScriptRuntime(destScript, new LoadScriptRuntimeOptions());
                    ResultReport<Script> report = EncodedFile.DeleteLogo(sc);

                    if (!report.Success)
                    {
                        Assert.IsFalse(result);
                        return;
                    }
                    Assert.IsTrue(result);

                    Assert.IsFalse(EncodedFile.ContainsLogo(report.Result));
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
        public void Base64Encode()
        {
            EngineState s = EngineTests.CreateEngineState();

            void Template(string binFileName, string encFileName)
            {
                string workDir = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "EncodedFile"));

                // Be careful! encFile will be converted to LF from CRLF in git tree!
                string binFile = Path.Combine(workDir, binFileName);
                string encFile = Path.Combine(workDir, encFileName);

                string compStr;
                StringBuilder b = new StringBuilder();
                using (StreamReader r = new StreamReader(encFile, Encoding.UTF8))
                {
                    string rawLine;
                    while ((rawLine = r.ReadLine()) != null)
                    {
                        b.AppendLine(rawLine);
                    }
                    compStr = b.ToString();
                }

                using (FileStream fs = new FileStream(binFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (StringWriter sw = new StringWriter())
                {
                    SplitBase64.Encode(fs, sw);
                    string result = sw.ToString();
                    Assert.IsTrue(result.Equals(compStr, StringComparison.Ordinal));
                }
            }

            Template("BigData.bin", "BigDataEnc4090.txt");
            Template("Type3.pdf", "Type3Enc4090.txt");
            Template("Type5.bin", "Type5Enc4090.txt");
        }

        [TestMethod]
        public void Base64Decode()
        {
            EngineState s = EngineTests.CreateEngineState();

            void Template(string binFileName, string encFileName, string section, bool inMem)
            {
                string workDir = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "EncodedFile"));

                // Be careful! encFile will be converted to LF from CRLF in git tree!
                string binFile = Path.Combine(workDir, binFileName);
                string encFile = Path.Combine(workDir, encFileName);

                byte[] binDigest;
                byte[] encDigest;
                using (FileStream fs = new FileStream(binFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    binDigest = HashHelper.GetHash(HashHelper.HashType.SHA256, fs);
                }

                if (inMem)
                {
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

                    byte[] decoded = SplitBase64.DecodeInMem(lines);
#if DEBUG_MIDDLE_FILE
                    using (FileStream fs = new FileStream(binFile + ".inMem.comp", FileMode.Create, FileAccess.Write))
                    {
                        fs.Write(decoded, 0, decoded.Length);
                    }
#endif
                    encDigest = HashHelper.GetHash(HashHelper.HashType.SHA256, decoded);
                }
                else
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (StreamReader tr = new StreamReader(encFile, Encoding.UTF8))
                        {
                            if (section.Length != 0)
                                IniReadWriter.FastForwardTextReader(tr, section);
                            SplitBase64.Decode(tr, ms);
                        }
                        ms.Position = 0;
#if DEBUG_MIDDLE_FILE
                        using (FileStream fs = new FileStream(binFile + ".noMem.comp", FileMode.Create, FileAccess.Write))
                        {
                            ms.CopyTo(fs);
                        }
                        ms.Position = 0;
#endif
                        encDigest = HashHelper.GetHash(HashHelper.HashType.SHA256, ms);
                    }
                }

                Assert.IsTrue(binDigest.SequenceEqual(encDigest));
            }

            Template("BigData.bin", "BigDataEnc4090.txt", string.Empty, true);
            Template("BigData.bin", "BigDataEnc4090.txt", string.Empty, false);
            Template("BigData.bin", "BigDataEnc4090S.txt", "Base64", false);
            Template("Type3.pdf", "Type3Enc4090.txt", string.Empty, true);
            Template("Type3.pdf", "Type3Enc4090.txt", string.Empty, false);
            Template("Type3.pdf", "Type3Enc4090S.txt", "Base64", false);
            Template("Type3.pdf", "Type3Enc1024.txt", string.Empty, true);
            Template("Type3.pdf", "Type3Enc1024.txt", string.Empty, false);
            Template("Type3.pdf", "Type3Enc1024S.txt", "Base64", false);
            // https://github.com/pebakery/pebakery/issues/90
            Template("Type5.bin", "Type5Enc4090.txt", string.Empty, true);
            Template("Type5.bin", "Type5Enc4090.txt", string.Empty, false);
        }
        #endregion

        #region 
        [TestMethod]
        public void PrintLzma2CompressMemUsage()
        {
            foreach (LzmaCompLevel level in Enum.GetValues(typeof(LzmaCompLevel)))
            {
                for (int th = 1; th <= Environment.ProcessorCount * 2; th++)
                {
                    ulong usage = EncodedFile.QueryLzma2CompressMemUsage(level, th);
                    string usageStr = NumberHelper.ByteSizeToSIUnit((long)usage, 1);
                    Console.WriteLine($"Memory usage of {level}, Threads {th} = {usageStr} ({usage})");
                }
            }    
        }
        #endregion
    }
}
