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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace PEBakery.Core.Tests
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
        public static bool IsOnline;
        #endregion

        #region CreateEngineState, DummySection
        public static EngineState CreateEngineState(bool doCopy = true, Script sc = null, string entrySection = "Process")
        {
            // Clone is needed for parallel test execution (Partial Deep Clone)
            EngineState s;
            if (doCopy)
            {
                Project project = EngineTests.Project.PartialDeepCopy();
                MainViewModel model = new MainViewModel();
                if (sc == null)
                    s = new EngineState(project, Logger, model, EngineMode.RunAll);
                else
                    s = new EngineState(project, Logger, model, EngineMode.RunOne, sc, entrySection);
            }
            else
            {
                Project.Variables.ResetVariables(VarsType.Local);
                MainViewModel model = new MainViewModel();
                if (sc == null)
                    s = new EngineState(Project, Logger, model, EngineMode.RunAll);
                else
                    s = new EngineState(Project, Logger, model, EngineMode.RunOne, sc, entrySection);
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
                s.PushLocalState(s, ls.IsMacro, ls.RefScriptId);
            }
        }

        public static ScriptSection DummySection() => Project.MainScript.Sections["Process"];
        #endregion

        #region Eval
        public static List<LogInfo> Eval(EngineState s, string rawCode, CodeType type, ErrorCheck check)
        {
            CodeParser parser = new CodeParser(DummySection(), Global.Setting, Project.Compat);
            return Eval(s, parser, rawCode, type, check, out _);
        }

        public static List<LogInfo> Eval(EngineState s, string rawCode, CodeType type, ErrorCheck check, out CodeCommand cmd)
        {
            CodeParser parser = new CodeParser(DummySection(), Global.Setting, Project.Compat);
            return Eval(s, parser, rawCode, type, check, out cmd);
        }

        public static List<LogInfo> Eval(EngineState s, string rawCode, CodeType type, ErrorCheck check, CompatOption opts)
        {
            CodeParser parser = new CodeParser(DummySection(), Global.Setting, opts);
            return Eval(s, parser, rawCode, type, check, out _);
        }

        public static List<LogInfo> Eval(EngineState s, string rawCode, CodeType type, ErrorCheck check, CompatOption opts, out CodeCommand cmd)
        {
            CodeParser parser = new CodeParser(DummySection(), Global.Setting, opts);
            return Eval(s, parser, rawCode, type, check, out cmd);
        }

        public static List<LogInfo> Eval(EngineState s, ScriptSection section, string rawCode, CodeType type, ErrorCheck check)
        {
            CodeParser parser = new CodeParser(section, Global.Setting, Project.Compat);
            return Eval(s, parser, rawCode, type, check, out _);
        }

        public static List<LogInfo> Eval(EngineState s, ScriptSection section, string rawCode, CodeType type, ErrorCheck check, out CodeCommand cmd)
        {
            CodeParser parser = new CodeParser(section, Global.Setting, Project.Compat);
            return Eval(s, parser, rawCode, type, check, out cmd);
        }

        public static List<LogInfo> Eval(EngineState s, CodeParser parser, string rawCode, CodeType type, ErrorCheck check)
        {
            return Eval(s, parser, rawCode, type, check, out _);
        }

        public static List<LogInfo> Eval(EngineState s, CodeParser parser, string rawCode, CodeType type, ErrorCheck check, out CodeCommand cmd)
        {
            // Create CodeCommand
            cmd = parser.ParseStatement(rawCode);
            if (cmd.Type == CodeType.Error)
            {
                CodeInfo_Error info = cmd.Info.Cast<CodeInfo_Error>();
                Console.WriteLine(info.ErrorMessage);

                Assert.AreEqual(ErrorCheck.ParserError, check);
                return new List<LogInfo>();
            }
            Assert.AreEqual(type, cmd.Type);

            // Run CodeCommand
            List<LogInfo> logs = Engine.ExecuteCommand(s, cmd);

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
            CodeParser parser = new CodeParser(dummySection, Global.Setting, EngineTests.Project.Compat);
            (cmds, errorLogs) = parser.ParseStatements(rawCodes);
            if (errorLogs.Any(x => x.State == LogState.Error))
            {
                Assert.AreEqual(ErrorCheck.ParserError, check);
                return new List<LogInfo>();
            }

            // Reset halt flags
            s.ResetFull();

            // Run CodeCommands
            return Engine.RunCommands(s, dummySection, cmds, s.CurSectionInParams, s.CurSectionOutParams, false);
        }
        #endregion

        #region EvalOptLines
        /// <summary>
        /// Eval for multiple lines of code
        /// </summary>
        /// <param name="s"></param>
        /// <param name="opType">Use null to check if rawCodes is not opitimzed</param>
        /// <param name="rawCodes"></param>
        /// <param name="check"></param>
        /// <returns></returns>
        public static List<LogInfo> EvalOptLines(EngineState s, CodeType? opType, List<string> rawCodes, ErrorCheck check)
        {
            ScriptSection section = DummySection();
            CodeParser parser = new CodeParser(section, Global.Setting, EngineTests.Project.Compat);
            return EvalOptLines(s, parser, section, opType, rawCodes, check, out _);
        }

        /// <summary>
        /// Eval for multiple lines of code
        /// </summary>
        /// <param name="s"></param>
        /// <param name="opType">Use null to check if rawCodes is not opitimzed</param>
        /// <param name="rawCodes"></param>
        /// <param name="check"></param>
        /// <param name="cmds"></param>
        /// <returns></returns>
        public static List<LogInfo> EvalOptLines(EngineState s, CodeType? opType, List<string> rawCodes, ErrorCheck check, out CodeCommand[] cmds)
        {
            ScriptSection section = DummySection();
            CodeParser parser = new CodeParser(section, Global.Setting, EngineTests.Project.Compat);
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
            CodeParser parser = new CodeParser(section, Global.Setting, EngineTests.Project.Compat);
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
            CodeParser parser = new CodeParser(section, Global.Setting, EngineTests.Project.Compat);
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
            return Engine.RunCommands(s, section, cmds, s.CurSectionInParams, s.CurSectionOutParams, false);
        }
        #endregion

        #region EvalScript
        public static (EngineState, List<LogInfo>) EvalScript(string treePath, ErrorCheck check, string entrySection = "Process")
        {
            return EvalScript(treePath, check, null, entrySection);
        }

        public static (EngineState, List<LogInfo>) EvalScript(string treePath, ErrorCheck check, Action<EngineState> applySetting, string entrySection = "Process")
        {
            Script sc = Project.GetScriptByTreePath(treePath);
            Assert.IsNotNull(sc);

            EngineState s = CreateEngineState(true, sc, entrySection);

            Engine engine = new Engine(s);
            applySetting?.Invoke(s);

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

        #region ExtractWith7z
        public static int ExtractWith7z(string sampleDir, string srcArchive, string destDir)
        {
            string binary;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                binary = Path.Combine(sampleDir, "7z.exe");
            else
                throw new PlatformNotSupportedException();

            Process proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    FileName = binary,
                    Arguments = $"x {srcArchive} -o{destDir}",
                }
            };
            proc.Start();
            proc.WaitForExit();
            return proc.ExitCode;
        }
        #endregion
    }
}
