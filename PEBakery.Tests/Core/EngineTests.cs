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
        public static Project Project;
        public static Logger Logger;
        public static string BaseDir;
        
        // [TestInitialize], [TestCleanup]
        [AssemblyInitialize]
        public static void PrepareTests(TestContext ctx)
        {
            BaseDir = @"..\..\Samples";
            ProjectCollection projects = new ProjectCollection(BaseDir, null);
            projects.PrepareLoad(out int nop);
            projects.Load(null);

            // Should be only one project named TestSuite
            Project = projects.Projects[0];

            // Init ZLibAssembly
            ZLibAssemblyInit();

            Logger.DebugLevel = DebugLevel.PrintExceptionStackTrace;
            Logger = new Logger(":memory:");
            Logger.System_Write(new LogInfo(LogState.Info, $"PEBakery.Tests launched"));
            
        }

        private static void ZLibAssemblyInit()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string arch;
            if (IntPtr.Size == 8)
                arch = "x64";
            else
                arch = "x86";
            string ZLibDllPath = Path.Combine(baseDir, arch, "zlibwapi.dll");
            Joveler.ZLibWrapper.ZLibNative.AssemblyInit(ZLibDllPath);
        }

        [AssemblyCleanup]
        public static void FinalizeTests()
        {
            Logger.DB.Close();
        }

        #region Utility Methods
        public static EngineState CreateEngineState(bool doClone = true, Plugin p = null)
        {
            // Clone is needed for parallel test execution
            if (doClone)
            {
                Project project = EngineTests.Project.Clone() as Project;
                Logger logger = EngineTests.Logger;
                MainViewModel model = new MainViewModel();
                return new EngineState(project, logger, model, p);
            }
            else
            {
                Project.Variables.ResetVariables(VarsType.Local);
                MainViewModel model = new MainViewModel();
                return new EngineState(Project, Logger, model, p);
            }
            
        }

        public static SectionAddress DummySectionAddress()
        {
            return new SectionAddress(Project.MainPlugin, Project.MainPlugin.Sections["Process"]);
        }

        public static EngineState Eval(EngineState s, string rawCode, CodeType type, ErrorCheck check)
        {
            return Eval(s, rawCode, type, check, out CodeCommand dummy);
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
            if (0 < errorLogs.Where(x => x.State == LogState.Error).Count())
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
