using System;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BakeryEngine;
using System.Collections.Generic;

namespace CommandTest
{
    public partial class CommandTest
    {
        [TestCategory("01_File")]
        [TestMethod]
        public void DirCopy_Ready()
        {
            string workDir = Path.Combine(testSrcDir, "01_DirCopy");
            List<string> fileList = new List<string>();
            List<string> dirList = new List<string>();
            dirList.Add(Path.Combine(workDir));
            dirList.Add(Path.Combine(workDir, "D"));
            dirList.Add(Path.Combine(workDir, "E"));
            fileList.Add(Path.Combine(workDir, "A.txt"));
            fileList.Add(Path.Combine(workDir, "B.txt"));
            fileList.Add(Path.Combine(workDir, "C.txt"));
            fileList.Add(Path.Combine(workDir, "F.ini"));
            fileList.Add(Path.Combine(workDir, "D", "D.txt"));
            fileList.Add(Path.Combine(workDir, "D", "G.ini"));
            
            foreach (string d in dirList)
                Assert.IsTrue(Directory.Exists(d));

            foreach (string f in fileList)
                Assert.IsTrue(File.Exists(f));
        }

        [TestCategory("01_File")]
        [TestMethod]
        public void DirCopy_Result()
        {
            string workDir = Path.Combine(testDestDir, "01_DirCopy");
            List<string> fileList = new List<string>();
            List<string> dirList = new List<string>();
            dirList.Add(Path.Combine(workDir, "Normal"));
            dirList.Add(Path.Combine(workDir, "Normal", "D"));
            dirList.Add(Path.Combine(workDir, "Normal", "E"));
            dirList.Add(Path.Combine(workDir, "Wildcard"));
            dirList.Add(Path.Combine(workDir, "Wildcard", "D"));
            fileList.Add(Path.Combine(workDir, "Normal", "A.txt"));
            fileList.Add(Path.Combine(workDir, "Normal", "B.txt"));
            fileList.Add(Path.Combine(workDir, "Normal", "C.txt"));
            fileList.Add(Path.Combine(workDir, "Normal", "F.ini"));
            fileList.Add(Path.Combine(workDir, "Normal", "D", "D.txt"));
            fileList.Add(Path.Combine(workDir, "Normal", "D", "G.ini"));
            fileList.Add(Path.Combine(workDir, "Wildcard", "A.txt"));
            fileList.Add(Path.Combine(workDir, "Wildcard", "B.txt"));
            fileList.Add(Path.Combine(workDir, "Wildcard", "C.txt"));
            fileList.Add(Path.Combine(workDir, "Wildcard", "D", "D.txt"));

            foreach (string d in dirList)
                Assert.IsTrue(Directory.Exists(d));

            foreach (string f in fileList)
                Assert.IsTrue(File.Exists(f));
        }

        [TestCategory("01_File")]
        [TestMethod]
        public void DirDelete_Ready()
        {
            string workDir = Path.Combine(testSrcDir, "01_DirDelete");
            List<string> fileList = new List<string>();
            List<string> dirList = new List<string>();
            dirList.Add(Path.Combine(workDir));
            dirList.Add(Path.Combine(workDir, "D"));
            dirList.Add(Path.Combine(workDir, "E"));
            fileList.Add(Path.Combine(workDir, "A.txt"));
            fileList.Add(Path.Combine(workDir, "B.txt"));
            fileList.Add(Path.Combine(workDir, "C.txt"));
            fileList.Add(Path.Combine(workDir, "F.ini"));
            fileList.Add(Path.Combine(workDir, "D", "D.txt"));
            fileList.Add(Path.Combine(workDir, "D", "G.ini"));

            foreach (string d in dirList)
                Assert.IsTrue(Directory.Exists(d));

            foreach (string f in fileList)
                Assert.IsTrue(File.Exists(f));
        }

        [TestCategory("01_File")]
        [TestMethod]
        public void DirDelete_Result()
        {
            string workDir = Path.Combine(testDestDir, "01_DirDelete");
            List<string> dirList = new List<string>();
            dirList.Add(Path.Combine(workDir));

            foreach (string d in dirList)
                Assert.IsTrue(Directory.Exists(d));
        }

        [TestCategory("01_File")]
        [TestMethod]
        public void DirMake_Ready()
        {
            // Nothing to do
        }

        [TestCategory("01_File")]
        [TestMethod]
        public void DirMake_Result()
        {
            string workDir = Path.Combine(testDestDir, "01_DirMake");
            List<string> dirList = new List<string>();
            dirList.Add(Path.Combine(workDir));

            foreach (string d in dirList)
                Assert.IsTrue(Directory.Exists(d));
        }

        [TestCategory("01_File")]
        [TestMethod]
        public void DirMove_Ready()
        {
            string workDir = Path.Combine(testSrcDir, "01_DirMove");
            List<string> fileList = new List<string>();
            List<string> dirList = new List<string>();
            dirList.Add(Path.Combine(workDir));
            dirList.Add(Path.Combine(workDir, "D"));
            dirList.Add(Path.Combine(workDir, "E"));
            fileList.Add(Path.Combine(workDir, "A.txt"));
            fileList.Add(Path.Combine(workDir, "B.txt"));
            fileList.Add(Path.Combine(workDir, "C.txt"));
            fileList.Add(Path.Combine(workDir, "F.ini"));
            fileList.Add(Path.Combine(workDir, "D", "D.txt"));
            fileList.Add(Path.Combine(workDir, "D", "G.ini"));

            foreach (string d in dirList)
                Assert.IsTrue(Directory.Exists(d));

            foreach (string f in fileList)
                Assert.IsTrue(File.Exists(f));
        }

        [TestCategory("01_File")]
        [TestMethod]
        public void DirMove_Result()
        {
            string workDir = Path.Combine(testDestDir, "01_DirMove");
            List<string> fileList = new List<string>();
            List<string> dirList = new List<string>();
            dirList.Add(Path.Combine(workDir, "Dest"));
            dirList.Add(Path.Combine(workDir, "Dest", "D"));
            dirList.Add(Path.Combine(workDir, "Dest", "E"));
            fileList.Add(Path.Combine(workDir, "Dest", "A.txt"));
            fileList.Add(Path.Combine(workDir, "Dest", "B.txt"));
            fileList.Add(Path.Combine(workDir, "Dest", "C.txt"));
            fileList.Add(Path.Combine(workDir, "Dest", "F.ini"));
            fileList.Add(Path.Combine(workDir, "Dest", "D", "D.txt"));
            fileList.Add(Path.Combine(workDir, "Dest", "D", "G.ini"));

            foreach (string d in dirList)
                Assert.IsTrue(Directory.Exists(d));

            foreach (string f in fileList)
                Assert.IsTrue(File.Exists(f));
        }

        [TestCategory("01_File")]
        [TestMethod]
        public void FileCopy_Ready()
        {
            string workDir = Path.Combine(testSrcDir, "01_FileCopy");
            List<string> fileList = new List<string>();
            List<string> dirList = new List<string>();
            dirList.Add(Path.Combine(workDir));
            fileList.Add(Path.Combine(workDir, "Hello.txt"));
            fileList.Add(Path.Combine(workDir, "World_1.txt"));
            fileList.Add(Path.Combine(workDir, "World_2.txt"));

            foreach (string d in dirList)
                Assert.IsTrue(Directory.Exists(d));

            foreach (string f in fileList)
                Assert.IsTrue(File.Exists(f));
        }

        [TestCategory("01_File")]
        [TestMethod]
        public void FileCopy_Result()
        {
            string workDir = Path.Combine(testDestDir, "01_FileCopy");
            List<string> fileList = new List<string>();
            List<string> dirList = new List<string>();
            dirList.Add(Path.Combine(workDir));
            fileList.Add(Path.Combine(workDir, "World.txt"));
            fileList.Add(Path.Combine(workDir, "World_1.txt"));
            fileList.Add(Path.Combine(workDir, "World_2.txt"));

            foreach (string d in dirList)
                Assert.IsTrue(Directory.Exists(d));

            foreach (string f in fileList)
                Assert.IsTrue(File.Exists(f));
        }
    }
}
