using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.Helper;
using PEBakery.Tests.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PEBakery.Tests
{
    [TestClass]
    public class FileHelperTests
    {
        [TestMethod]
        [TestCategory("Helper")]
        [TestCategory("FileHelper")]
        public void FileHelper_GetFilesEx()
        {
            string srcDir = Path.Combine(EngineTests.BaseDir, "WorkBench", "Helper", "FileHelper");

            // Test 1
            {
                string[] srcFiles = FileHelper.GetFilesEx(srcDir, "*.txt", SearchOption.AllDirectories);
                Assert.IsTrue(srcFiles.Length == 5);
            }
            // Test 2
            {
                string[] srcFiles = FileHelper.GetFilesEx(srcDir, "*.txt", SearchOption.TopDirectoryOnly);
                Assert.IsTrue(srcFiles.Length == 3);
            }
        }

        [TestMethod]
        [TestCategory("Helper")]
        [TestCategory("FileHelper")]
        public void FileHelper_DirectoryCopy()
        {
            string srcDir = Path.Combine(EngineTests.BaseDir, "WorkBench", "Helper", "FileHelper");
            string destDir = Path.Combine(EngineTests.BaseDir, "WorkBench", "Helper", "FileHelper_DirCopy");

            // Test 1
            FileHelper_DirectoryCopy_Template(destDir, () =>
            {
                FileHelper.DirectoryCopy(srcDir, destDir, true, true);
                string[] srcFiles = Directory.GetFiles(destDir, "*", SearchOption.AllDirectories);
                Assert.IsTrue(srcFiles.Length == 7);
            });

            // Test 2
            FileHelper_DirectoryCopy_Template(destDir, () =>
            {
                FileHelper.DirectoryCopy(srcDir, destDir, false, true);
                string[] srcFiles = Directory.GetFiles(destDir, "*", SearchOption.AllDirectories);
                Assert.IsTrue(srcFiles.Length == 4);
            });

            // Test 3
            FileHelper_DirectoryCopy_Template(destDir, () =>
            {
                FileHelper.DirectoryCopy(srcDir, destDir, true, true, "*.txt");
                string[] srcFiles = Directory.GetFiles(destDir, "*.txt", SearchOption.AllDirectories);
                Assert.IsTrue(srcFiles.Length == 5);
            });

            // Test 4
            FileHelper_DirectoryCopy_Template(destDir, () =>
            {
                FileHelper.DirectoryCopy(srcDir, destDir, false, true, "*.txt");
                string[] srcFiles = Directory.GetFiles(destDir, "*.txt", SearchOption.AllDirectories);
                Assert.IsTrue(srcFiles.Length == 3);
            });
        }

        public void FileHelper_DirectoryCopy_Template(string destDir, Action action)
        {
            Directory.CreateDirectory(destDir);
            try
            {
                action.Invoke();
            }
            finally
            {
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, true);
            }
        }
    }
}
