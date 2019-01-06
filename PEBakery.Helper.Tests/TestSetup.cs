using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace PEBakery.Helper.Tests
{
    [TestClass]
    public class TestSetup
    {
        public static string BaseDir;
        public static string SampleDir;

        [AssemblyInitialize]
        public static void Init(TestContext context)
        {
            BaseDir = Path.GetFullPath(Path.Combine(TestHelper.GetProgramAbsolutePath(), "..", ".."));
            SampleDir = Path.Combine(BaseDir, "Samples");
        }

        [AssemblyCleanup]
        public static void Cleanup()
        {
        }
    }

    public static class TestHelper
    {
        public static string GetProgramAbsolutePath()
        {
            string path = AppDomain.CurrentDomain.BaseDirectory;
            if (Path.GetDirectoryName(path) != null)
                path = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return path;
        }
    }
}
