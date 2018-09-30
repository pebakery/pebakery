using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.Core;
using PEBakery.Tests.Core;
using PEBakery.WPF;
using System;
using System.IO;

namespace PEBakery.Tests
{
    [TestClass]
    public class TestSetup
    {
        #region AssemblyInitalize, AssemblyCleanup
        [AssemblyInitialize]
        public static void PrepareTests(TestContext ctx)
        {
            // Setting instance
            string emptyTempFile = Path.GetTempFileName();
            if (File.Exists(emptyTempFile))
                File.Delete(emptyTempFile);
            Global.Setting = new SettingViewModel(emptyTempFile); // Set to default

            // Load Project "TestSuite" (ScriptCache disabled)
            EngineTests.BaseDir = Path.GetFullPath(Path.Combine("..", "..", "Samples"));
            ProjectCollection projects = new ProjectCollection(EngineTests.BaseDir, null);
            projects.PrepareLoad();
            projects.Load(null);

            // Should be only one project named TestSuite
            EngineTests.Project = projects.ProjectList[0];
            Assert.IsTrue(projects.ProjectList.Count == 1);

            // Init NativeAssembly
            Global.NativeGlobalInit(AppDomain.CurrentDomain.BaseDirectory);

            // Use InMemory Database for Tests
            Logger.DebugLevel = DebugLevel.PrintExceptionStackTrace;
            EngineTests.Logger = new Logger(":memory:");
            EngineTests.Logger.SystemWrite(new LogInfo(LogState.Info, "PEBakery.Tests launched"));

            // Set Global 
            Global.Logger = EngineTests.Logger;
            Global.BaseDir = EngineTests.BaseDir;
            Global.BuildDate = BuildTimestamp.ReadDateTime();
        }

        [AssemblyCleanup]
        public static void AssemblyCleanup()
        {
            EngineTests.Logger?.Dispose();

            Global.NativeGlobalCleanup();
        }
        #endregion
    }
}
