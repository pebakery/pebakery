/*
    Copyright (C) 2017-2022 Hajin Jang
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PEBakery.Core.Tests
{
    public enum ErrorCheck
    {
        Success = 0,
        Warning = 10,
        Overwrite = 11,
        RuntimeError = 20,
        ParserError = 21,
    }

    public static class EngineTests
    {
        #region Static Fields and Properties
        private static Project? _project;
        public static Project Project
        {
            get
            {
                if (_project == null)
                    throw new InvalidOperationException($"{nameof(_project)} is null");
                return _project;
            }
            set => _project = value;
        }
        private static Logger? _logger;
        public static Logger Logger
        {
            get
            {
                if (_logger == null)
                    throw new InvalidOperationException($"{nameof(_logger)} is null");
                return _logger;
            }
            set => _logger = value;
        }
        public static string BaseDir { get; set; } = string.Empty;
        public static string TestBench { get; set; } = string.Empty;
        public static string MagicFile { get; set; } = string.Empty;
        public static bool IsOnline { get; set; }
        #endregion

        #region CreateEngineState, DummySection
        public static EngineState CreateEngineState(bool doCopy = true, Script? sc = null, string entrySection = ScriptSection.Names.Process)
        {
            // Clone is needed for parallel test execution (Partial Deep Clone)
            EngineState s;
            if (doCopy)
            {
                Project project = Project.PartialDeepCopy();
                MainViewModel? model = null;
                // TODO: .NET Core Band-aid. Without this line, entire WPF Control access would crash the test.
                RunSTAThread(() => model = new MainViewModel());
                Assert.IsNotNull(model);

                if (sc == null)
                    s = new EngineState(project, Logger, model, null, EngineMode.RunAll);
                else
                    s = new EngineState(project, Logger, model, null, EngineMode.RunOne, sc, entrySection);
            }
            else
            {
                Project.Variables.ResetVariables(VarsType.Local);
                MainViewModel? model = null;
                // TODO: .NET Core Band-aid. Without this line, entire WPF Control access would crash the test.
                RunSTAThread(() => model = new MainViewModel());
                Assert.IsNotNull(model);

                if (sc == null)
                    s = new EngineState(Project, Logger, model, null, EngineMode.RunAll);
                else
                    s = new EngineState(Project, Logger, model, null, EngineMode.RunOne, sc, entrySection);
            }

            s.LogMode = LogMode.NoDefer;
            s.TestMode = true;

            return s;
        }

        public static void PushDepthInfo(EngineState s, int targetDepth)
        {
            while (s.PeekDepth < targetDepth)
            {
                EngineLocalState ls = s.PeekLocalState();
                s.PushLocalState(ls.IsMacro, ls.RefScriptId);
            }
        }

        public static ScriptSection DummySection() => Project.MainScript.Sections["Process"];
        #endregion

        #region Eval
        public static List<LogInfo> Eval(EngineState s, string rawCode, CodeType type, ErrorCheck check)
        {
            ScriptSection section = DummySection();
            CodeParser parser = new CodeParser(section, Global.Setting, Project.Compat);
            return Eval(s, section, parser, rawCode, type, check, out _);
        }

        public static List<LogInfo> Eval(EngineState s, string rawCode, CodeType type, ErrorCheck check, out CodeCommand cmd)
        {
            ScriptSection section = DummySection();
            CodeParser parser = new CodeParser(section, Global.Setting, Project.Compat);
            return Eval(s, section, parser, rawCode, type, check, out cmd);
        }

        public static List<LogInfo> Eval(EngineState s, string rawCode, CodeType type, ErrorCheck check, CompatOption compat)
        {
            ScriptSection section = DummySection();
            CodeParser parser = new CodeParser(section, Global.Setting, compat);
            return Eval(s, section, parser, rawCode, type, check, compat, out _);
        }

        public static List<LogInfo> Eval(EngineState s, string rawCode, CodeType type, ErrorCheck check, CompatOption compat, out CodeCommand cmd)
        {
            ScriptSection section = DummySection();
            CodeParser parser = new CodeParser(section, Global.Setting, compat);
            return Eval(s, section, parser, rawCode, type, check, compat, out cmd);
        }

        public static List<LogInfo> Eval(EngineState s, ScriptSection section, string rawCode, CodeType type, ErrorCheck check)
        {
            CodeParser parser = new CodeParser(section, Global.Setting, Project.Compat);
            return Eval(s, section, parser, rawCode, type, check, out _);
        }

        public static List<LogInfo> Eval(EngineState s, ScriptSection section, string rawCode, CodeType type, ErrorCheck check, out CodeCommand cmd)
        {
            CodeParser parser = new CodeParser(section, Global.Setting, Project.Compat);
            return Eval(s, section, parser, rawCode, type, check, out cmd);
        }

        public static List<LogInfo> Eval(EngineState s, CodeParser parser, string rawCode, CodeType type, ErrorCheck check)
        {
            return Eval(s, DummySection(), parser, rawCode, type, check, out _);
        }

        public static List<LogInfo> Eval(EngineState s, ScriptSection section, CodeParser parser, string rawCode, CodeType type, ErrorCheck check, out CodeCommand cmd)
        {
            // Create CodeCommand
            cmd = parser.ParseStatement(rawCode);
            if (cmd.Type == CodeType.Error)
            {
                CodeInfo_Error info = (CodeInfo_Error)cmd.Info;
                Console.WriteLine(info.ErrorMessage);

                Assert.AreEqual(ErrorCheck.ParserError, check);
                return new List<LogInfo>();
            }
            Assert.AreEqual(type, cmd.Type);

            // Run CodeCommand
            s.CurrentSection = section;
            List<LogInfo> logs = Engine.ExecuteCommand(s, cmd);

            // Assert
            CheckErrorLogs(logs, check);

            // Return logs
            return logs;
        }

        public static List<LogInfo> Eval(EngineState s, ScriptSection section, CodeParser parser, string rawCode, CodeType type, ErrorCheck check, CompatOption compat, out CodeCommand cmd)
        {
            // Create CodeCommand
            cmd = parser.ParseStatement(rawCode);
            if (cmd.Type == CodeType.Error)
            {
                CodeInfo_Error info = (CodeInfo_Error)cmd.Info;
                Console.WriteLine(info.ErrorMessage);

                Assert.AreEqual(ErrorCheck.ParserError, check);
                return new List<LogInfo>();
            }
            Assert.AreEqual(type, cmd.Type);

            // Run CodeCommand
            s.SetCompat(compat);
            s.CurrentSection = section;
            List<LogInfo> logs = Engine.ExecuteCommand(s, cmd);
            s.SetCompat(Project.Compat); // Reset to default

            // Assert
            CheckErrorLogs(logs, check);

            // Return logs
            return logs;
        }
        #endregion

        #region EvalLines
        public static List<LogInfo> EvalLines(EngineState s, List<string> rawCodes, ErrorCheck check)
        {
            return EvalLines(s, rawCodes, check, out _);
        }

        public static List<LogInfo> EvalLines(EngineState s, List<string> rawCodes, ErrorCheck check, out CodeCommand[] cmds)
        {
            // Create CodeCommand
            List<LogInfo> errorLogs;
            ScriptSection dummySection = DummySection();
            CodeParser parser = new CodeParser(dummySection, Global.Setting, Project.Compat);
            (cmds, errorLogs) = parser.ParseStatements(rawCodes);
            if (errorLogs.Any(x => x.State == LogState.Error))
            {
                Assert.AreEqual(ErrorCheck.ParserError, check);
                return new List<LogInfo>();
            }

            // Reset halt flags
            s.ResetFull();

            // Run CodeCommands
            return Engine.RunCommands(s, dummySection, cmds, s.CurSectionInParams, s.CurSectionOutParams, false) ?? new List<LogInfo>();
        }
        #endregion

        #region EvalOptLines
        /// <summary>
        /// Eval for multiple lines of code
        /// </summary>
        /// <param name="s"></param>
        /// <param name="opType">Use null to check if rawCodes is not optimized</param>
        /// <param name="rawCodes"></param>
        /// <param name="check"></param>
        /// <returns></returns>
        public static List<LogInfo> EvalOptLines(EngineState s, CodeType? opType, List<string> rawCodes, ErrorCheck check)
        {
            ScriptSection section = DummySection();
            CodeParser parser = new CodeParser(section, Global.Setting, Project.Compat);
            return EvalOptLines(s, parser, section, opType, rawCodes, check, out _);
        }

        /// <summary>
        /// Eval for multiple lines of code
        /// </summary>
        /// <param name="s"></param>
        /// <param name="opType">Use null to check if rawCodes is not optimized</param>
        /// <param name="rawCodes"></param>
        /// <param name="check"></param>
        /// <param name="cmds"></param>
        /// <returns></returns>
        public static List<LogInfo> EvalOptLines(EngineState s, CodeType? opType, List<string> rawCodes, ErrorCheck check, out CodeCommand[] cmds)
        {
            ScriptSection section = DummySection();
            CodeParser parser = new CodeParser(section, Global.Setting, Project.Compat);
            return EvalOptLines(s, parser, section, opType, rawCodes, check, out cmds);
        }

        /// <summary>
        /// Eval for multiple lines of code
        /// </summary>
        /// <param name="s"></param>
        /// <param name="section"></param>
        /// <param name="opType">Use null to check if rawCodes is not optimized</param>
        /// <param name="rawCodes"></param>
        /// <param name="check"></param>
        /// <returns></returns>
        public static List<LogInfo> EvalOptLines(EngineState s, ScriptSection section, CodeType? opType, List<string> rawCodes, ErrorCheck check)
        {
            CodeParser parser = new CodeParser(section, Global.Setting, Project.Compat);
            return EvalOptLines(s, parser, section, opType, rawCodes, check, out _);
        }

        /// <summary>
        /// Eval for multiple lines of code
        /// </summary>
        /// <param name="s"></param>
        /// <param name="section"></param>
        /// <param name="opType">Use null to check if rawCodes is not optimized</param>
        /// <param name="rawCodes"></param>
        /// <param name="check"></param>
        /// <param name="cmds"></param>
        /// <returns></returns>
        public static List<LogInfo> EvalOptLines(EngineState s, ScriptSection section, CodeType? opType, List<string> rawCodes, ErrorCheck check, out CodeCommand[] cmds)
        {
            CodeParser parser = new CodeParser(section, Global.Setting, Project.Compat);
            return EvalOptLines(s, parser, section, opType, rawCodes, check, out cmds);
        }

        /// <summary>
        /// Eval for multiple lines of code
        /// </summary>
        /// <param name="s"></param>
        /// <param name="parser"></param>
        /// <param name="section"></param>
        /// <param name="opType">Use null to check if rawCodes is not optimized</param>
        /// <param name="rawCodes"></param>
        /// <param name="check"></param>
        /// <returns></returns>
        public static List<LogInfo> EvalOptLines(EngineState s, CodeParser parser, ScriptSection section, CodeType? opType, List<string> rawCodes, ErrorCheck check)
        {
            return EvalOptLines(s, parser, section, opType, rawCodes, check, out _);
        }

        /// <summary>
        /// Eval for multiple lines of code
        /// </summary>
        /// <param name="s"></param>
        /// <param name="parser"></param>
        /// <param name="section"></param>
        /// <param name="opType">Use null to check if rawCodes is not optimized</param>
        /// <param name="rawCodes"></param>
        /// <param name="check"></param>
        /// <param name="cmds"></param>
        /// <returns></returns>
        public static List<LogInfo> EvalOptLines(EngineState s, CodeParser parser, ScriptSection section, CodeType? opType, List<string> rawCodes, ErrorCheck check, out CodeCommand[] cmds)
        {
            // Parse CodeCommand
            List<LogInfo> errorLogs;
            (cmds, errorLogs) = parser.ParseStatements(rawCodes);

            if (errorLogs.Any(x => x.State == LogState.Error))
            {
                Assert.AreEqual(ErrorCheck.ParserError, check);
                return new List<LogInfo>();
            }

            if (opType is CodeType type)
            {
                Assert.AreEqual(1, cmds.Length);
                Assert.AreEqual(type, cmds[0].Type);
            }
            else
            {
                Assert.IsTrue(1 < cmds.Length);
            }

            // Reset halt flags
            s.ResetFull();

            // Run CodeCommands
            return Engine.RunCommands(s, section, cmds, s.CurSectionInParams, s.CurSectionOutParams, false) ?? new List<LogInfo>();
        }
        #endregion

        #region EvalScript
        public static (EngineState, List<LogInfo>) EvalScript(string treePath, ErrorCheck check, string entrySection = ScriptSection.Names.Process)
        {
            return EvalScript(treePath, check, null, entrySection);
        }

        public static (EngineState, List<LogInfo>) EvalScript(string treePath, ErrorCheck check, Action<EngineState>? setState, string entrySection = ScriptSection.Names.Process)
        {
            Script? sc = Project.GetScriptByTreePath(treePath);
            Assert.IsNotNull(sc);

            EngineState s = CreateEngineState(true, sc, entrySection);

            Engine engine = new Engine(s);
            setState?.Invoke(s);

            Task<int> t = engine.Run($"Test [{sc.Title}]");
            t.Wait();
            int buildId = t.Result;
            List<LogModel.BuildLog> buildLogs = s.Logger.Db.Table<LogModel.BuildLog>().Where(x => x.BuildId == buildId).ToList();

            List<LogInfo> logs = new List<LogInfo>(buildLogs.Count);
            foreach (LogModel.BuildLog buildLog in buildLogs)
                logs.Add(new LogInfo(buildLog));

            CheckErrorLogs(logs, check);

            return (s, logs);
        }
        #endregion

        #region CheckErrorLogs
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
                case ErrorCheck.RuntimeError:
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

        #region STAThread
        public static void RunSTAThread(Action action)
        {
            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
                action.Invoke();

            Thread thread = new Thread(() =>
            {
                action.Invoke();
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
        }
        #endregion
    }

    public class STATestMethodAttribute : TestMethodAttribute
    {
        public override TestResult[] Execute(ITestMethod testMethod)
        {
            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
                return new TestResult[] { testMethod.Invoke(null) };

            TestResult[] result = Array.Empty<TestResult>();
            Thread thread = new Thread(() =>
            {
                result = new TestResult[] { testMethod.Invoke(null) };
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            return result;
        }
    }
}
