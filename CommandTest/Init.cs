using System;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BakeryEngine_Legacy;
using System.Collections.Generic;

namespace CommandTest
{
    [TestClass]
    public partial class CommandTest
    {
        private static string baseDir;
        private static string testSrcDir;
        private static string testDestDir;

        [ClassInitialize()]
        public static void ClassInitialize(TestContext testContext)
        {
            baseDir = Path.Combine(Environment.CurrentDirectory, "..", "..", "..", "TestSuite");
            testSrcDir = Path.Combine(baseDir, "WorkBench", "TestSuite", "Src");
            testDestDir = Path.Combine(baseDir, "WorkBench", "TestSuite", "Dest");

            Project project = new Project(baseDir, "TestSuite");
            Logger logger = new Logger(Path.Combine(baseDir, "log.txt"), LogFormat.Text);
            BakeryEngine_Legacy.BakeryEngine engine = new BakeryEngine_Legacy.BakeryEngine(baseDir, project, logger, DebugLevel.PrintExceptionStackTrace);
            Stopwatch stopwatch = Stopwatch.StartNew();
            Console.WriteLine("BakeryEngine start...");
            engine.Build();
            Console.WriteLine("BakeryEngine done");
            Console.WriteLine("Time elapsed: {0}\r\n", stopwatch.Elapsed);
        }
    }
}
