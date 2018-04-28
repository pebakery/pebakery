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
using System.IO;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.Core;
using PEBakery.WPF;
using PEBakery.Exceptions;
using System.Linq;

namespace PEBakery.Tests.Core
{
    public enum ErrorCheck
    {
        Success = 0,
        Warning = 10,
        Overwrite = 11,
        Error = 20,
        ParserError = 21,
    }

    [TestClass]
    public class EngineTests
    {
        #region Static Fields
        public static Project Project;
        public static Logger Logger;
        public static string BaseDir;
        #endregion

        #region AssemblyInitalize, AssemblyCleanup
        [AssemblyInitialize]
        public static void PrepareTests(TestContext ctx)
        {
            BaseDir = Path.GetFullPath(Path.Combine("..", "..", "Samples"));
            ProjectCollection projects = new ProjectCollection(BaseDir, null);
            projects.PrepareLoad(out _);
            projects.Load(null);

            // Should be only one project named TestSuite
            Project = projects.Projects[0];

            // Init NativeAssembly
            NativeAssemblyInit();

            // Use InMemory Database for Tests
            Logger.DebugLevel = DebugLevel.PrintExceptionStackTrace;
            Logger = new Logger(":memory:");
            Logger.SystemWrite(new LogInfo(LogState.Info, "PEBakery.Tests launched"));

            App.Logger = Logger;
            App.BaseDir = BaseDir;
        }

        private static void NativeAssemblyInit()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string arch = IntPtr.Size == 8 ? "x64" : "x86";

            string zLibDllPath = Path.Combine(baseDir, arch, "zlibwapi.dll");
            string wimLibDllPath = Path.Combine(baseDir, arch, "libwim-15.dll");
            string xzDllPath = Path.Combine(baseDir, arch, "liblzma.dll");
            string lz4DllPath = Path.Combine(baseDir, arch, "liblz4.so.1.8.1.dll");
            Joveler.ZLibWrapper.ZLibNative.AssemblyInit(zLibDllPath);
            ManagedWimLib.Wim.GlobalInit(wimLibDllPath);
            PEBakery.XZLib.XZStream.GlobalInit(xzDllPath);
            PEBakery.LZ4Lib.LZ4FrameStream.GlobalInit(lz4DllPath);
        }

        [AssemblyCleanup]
        public static void AssemblyCleanup()
        {
            Logger.DB.Close();

            Joveler.ZLibWrapper.ZLibNative.AssemblyCleanup();
            ManagedWimLib.Wim.GlobalCleanup();
            PEBakery.XZLib.XZStream.GlobalCleanup();
            PEBakery.LZ4Lib.LZ4FrameStream.GlobalCleanup();
        }
        #endregion

        #region Utility Methods
        public static EngineState CreateEngineState(bool doClone = true, Script p = null)
        {
            // Clone is needed for parallel test execution
            if (doClone)
            {
                Project project = EngineTests.Project.Clone() as Project;
                Logger logger = EngineTests.Logger;
                MainViewModel model = new MainViewModel();
                if (p == null)
                    return new EngineState(project, logger, model, EngineMode.RunAll);
                else
                    return new EngineState(project, logger, model, EngineMode.RunOne, p);
            }
            else
            {
                Project.Variables.ResetVariables(VarsType.Local);
                MainViewModel model = new MainViewModel();
                if (p == null)
                    return new EngineState(Project, Logger, model, EngineMode.RunAll);
                else
                    return new EngineState(Project, Logger, model, EngineMode.RunOne, p);
            }
        }

        public static SectionAddress DummySectionAddress()
        {
            return new SectionAddress(Project.MainScript, Project.MainScript.Sections["Process"]);
        }

        public static EngineState Eval(EngineState s, string rawCode, CodeType type, ErrorCheck check)
        {
            return Eval(s, rawCode, type, check, out _);
        }

        public static EngineState Eval(EngineState s, string rawCode, CodeType type, ErrorCheck check, out CodeCommand cmd)
        {
            // Create CodeCommand
            SectionAddress addr = EngineTests.DummySectionAddress();
            cmd = CodeParser.ParseStatement(rawCode, addr);
            if (cmd.Type == CodeType.Error)
            {
                Console.WriteLine((cmd.Info as CodeInfo_Error).ErrorMessage);
                Assert.IsTrue(check == ErrorCheck.ParserError);
                return s;
            }
            Assert.IsTrue(cmd.Type == type);

            // Run CodeCommand
            List<LogInfo> logs = Engine.ExecuteCommand(s, cmd);

            // Assert
            EngineTests.CheckErrorLogs(logs, check);

            // Return EngineState
            return s;
        }

        public static EngineState Eval(string rawCode, CodeType type, ErrorCheck check)
        {
            EngineState s = EngineTests.CreateEngineState();
            return EngineTests.Eval(s, rawCode, type, check);
        }

        public static EngineState Eval(string rawCode, CodeType type, ErrorCheck check, out CodeCommand cmd)
        {
            EngineState s = EngineTests.CreateEngineState();
            return EngineTests.Eval(s, rawCode, type, check, out cmd);
        }

        public static EngineState EvalLines(EngineState s, List<string> rawCodes, CodeType type, ErrorCheck check)
        {
            return EvalLines(s, rawCodes, type, check, out List<CodeCommand> dummy);
        }

        public static EngineState EvalLines(EngineState s, List<string> rawCodes, CodeType type, ErrorCheck check, out List<CodeCommand> cmds)
        {
            // Create CodeCommand
            SectionAddress addr = EngineTests.DummySectionAddress();
            cmds = CodeParser.ParseStatements(rawCodes, addr, out List<LogInfo> errorLogs);
            if (errorLogs.Any(x => x.State == LogState.Error))
            { 
                Assert.IsTrue(check == ErrorCheck.ParserError);
                return s;
            }
            Assert.IsTrue(cmds[0].Type == type);

            // Run CodeCommand
            List<LogInfo> logs = Engine.ExecuteCommand(s, cmds[0]);

            // Assert
            EngineTests.CheckErrorLogs(logs, check);

            // Return EngineState
            return s;
        }

        public static void CheckErrorLogs(List<LogInfo> logs, ErrorCheck check)
        {
            switch (check)
            {
                case ErrorCheck.Success:
                    foreach (LogInfo log in logs)
                    {
                        Assert.IsTrue(log.State != LogState.Error);
                        Assert.IsTrue(log.State != LogState.CriticalError);
                        Assert.IsTrue(log.State != LogState.Warning);
                    }
                    break;
                case ErrorCheck.Warning:
                    {
                        bool result = false;
                        foreach (LogInfo log in logs)
                        {
                            Assert.IsTrue(log.State != LogState.Error);
                            Assert.IsTrue(log.State != LogState.CriticalError);
                            if (log.State == LogState.Warning)
                                result = true;
                        }
                        Assert.IsTrue(result);
                    }
                    break;
                case ErrorCheck.Overwrite:
                    {
                        bool result = false;
                        foreach (LogInfo log in logs)
                        {
                            Assert.IsTrue(log.State != LogState.Error);
                            Assert.IsTrue(log.State != LogState.CriticalError);
                            if (log.State == LogState.Overwrite)
                                result = true;
                        }
                        Assert.IsTrue(result);
                    }
                    break;
                case ErrorCheck.Error:
                    {
                        bool result = false;
                        foreach (LogInfo log in logs)
                        {
                            Assert.IsTrue(log.State != LogState.CriticalError);
                            if (log.State == LogState.Error)
                                result = true;
                        }
                        Assert.IsTrue(result);
                    }
                    break;
                default:
                    Assert.Fail();
                    break;
            }
        }
        #endregion

    }
}
