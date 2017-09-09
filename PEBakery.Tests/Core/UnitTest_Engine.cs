using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.Core;
using PEBakery.WPF;

namespace UnitTest.Core
{
    [TestClass]
    public class UnitTest_Engine
    {
        public static Project Project;
        public static Logger Logger;
        
        // [TestInitialize], [TestCleanup]
        [AssemblyInitialize]
        public static void PrepareTests(TestContext ctx)
        {
            string baseDir = @"..\..\Samples";
            ProjectCollection projects = new ProjectCollection(baseDir, null);
            projects.PrepareLoad(out int nop);
            projects.Load(null);

            // Should be only one project named TestSuite
            Project = projects.Projects[0];

            string logDBFile = Path.Combine(baseDir, "PEBakery.Tests.db");
            Logger = new Logger(logDBFile);
            Logger.System_Write(new LogInfo(LogState.Info, $"PEBakery.Tests launched"));
        }

        [AssemblyCleanup]
        public static void FinalizeTests()
        {
            Logger.DB.Close();
        }

        public static EngineState CreateEngineState(Plugin p = null)
        {
            return new EngineState(UnitTest_Engine.Project, UnitTest_Engine.Logger, new MainViewModel(), p);
        }

        public static SectionAddress DummySectionAddress()
        {
            return new SectionAddress(Project.MainPlugin, Project.MainPlugin.Sections["Process"]);
        }

        public static void CheckErrorLogs(List<LogInfo> logs)
        {
            foreach (LogInfo log in logs)
            {
                Assert.IsTrue(log.State != LogState.Error);
                Assert.IsTrue(log.State != LogState.CriticalError);
            }
        }
    }
}
