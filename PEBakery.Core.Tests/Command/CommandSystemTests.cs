/*
    Copyright (C) 2017-2020 Hajin Jang
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
using System;
using System.Collections.Generic;
using System.IO;
// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local

namespace PEBakery.Core.Tests.Command
{
    [TestClass]
    public class CommandSystemTests
    {
        #region ErrorOff
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandSystem")]
        public void ErrorOff()
        {
            EngineState s = EngineTests.CreateEngineState();

            void SingleTemplate(List<string> rawCodes, ErrorCheck check = ErrorCheck.Success)
            {
                EngineTests.EvalLines(s, rawCodes, check);
            }
            void ScriptTemplate(string treePath, ErrorCheck check = ErrorCheck.Success)
            {
                EngineTests.EvalScript(treePath, check);
            }

            SingleTemplate(new List<string>
            {
                @"System,ErrorOff",
                @"Error1",
            });
            SingleTemplate(new List<string>
            {
                @"System,ErrorOff,3",
                @"Error1",
                @"Error2",
                @"Error3",
            });
            SingleTemplate(new List<string>
            {
                @"System,ErrorOff,2",
                @"Error1",
                @"Error2",
                @"Error3",
            }, ErrorCheck.RuntimeError);

            string scPath = Path.Combine(EngineTests.Project.ProjectName, "System", "ErrorOff.script");
            ScriptTemplate(scPath);
        }
        #endregion

        #region OnBuildExit, OnScriptExit
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandSystem")]
        public void OnBuildScriptExit()
        {
            void ScriptTemplate(string treePath, string entrySection, ErrorCheck check = ErrorCheck.Success)
            {
                (EngineState s, _) = EngineTests.EvalScript(treePath, check, entrySection);
                string destStr = s.Variables["Dest"];
                Assert.IsTrue(destStr.Equals("T", StringComparison.Ordinal));
            }

            string scPath = Path.Combine(EngineTests.Project.ProjectName, "System", "Callback.script");

            // OnBuildExit
            ScriptTemplate(scPath, "Process-BuildCallback");

            // OnScriptExit
            ScriptTemplate(scPath, "Process-ScriptCallback");
        }
        #endregion

        #region LoadNewScript
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandSystem")]
        public void LoadNewScript()
        {
            void Template(string rawCode, string[] destTreePaths, ErrorCheck check = ErrorCheck.Success)
            { // Need new EngineState for every test
                EngineState s = EngineTests.CreateEngineState();
                EngineTests.Eval(s, rawCode, CodeType.System, check);
                if (check == ErrorCheck.Success || check == ErrorCheck.Warning)
                {
                    foreach (string destTreePath in destTreePaths)
                    {
                        string[] paths = destTreePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                        // "TestSuite" is root, so does not have a corresponding directory script instance
                        Assert.IsTrue(0 < paths.Length);
                        Assert.IsTrue(paths[0].Equals("TestSuite", StringComparison.OrdinalIgnoreCase));

                        for (int i = 1; i < paths.Length - 1; i++)
                        {
                            string destTreeDir = Project.PathKeyGenerator(paths, i);
                            Assert.IsNotNull(destTreeDir);
                            Assert.IsTrue(s.Project.ContainsScriptByTreePath(destTreeDir));
                        }
                        Assert.IsTrue(s.Project.ContainsScriptByTreePath(destTreePath));
                    }
                }
            }

            Template(@"System,LoadNewScript,%TestBench%\CommandSystem\Blank1.script,", new string[] { @"TestSuite\Blank1.script" });
            Template(@"System,LoadNewScript,%TestBench%\CommandSystem\Blank1.script,Load", new string[] { @"TestSuite\Load\Blank1.script" });
            Template(@"System,LoadNewScript,%TestBench%\CommandSystem\Blank?.script,Load", new string[]
            {
                @"TestSuite\Load\Blank1.script",
                @"TestSuite\Load\Blank2.script"
            });
            Template(@"System,LoadNewScript,%TestBench%\CommandSystem\*.script,Load\Tree", new string[]
            {
                @"TestSuite\Load\Tree\Blank1.script",
                @"TestSuite\Load\Tree\Blank2.script",
                @"TestSuite\Load\Tree\Sub\Sub1.script",
                @"TestSuite\Load\Tree\Sub\Sub2.script",
            });
            Template(@"System,LoadNewScript,%TestBench%\CommandSystem\*.script,Load,NOREC", new string[]
            {
                @"TestSuite\Load\Blank1.script",
                @"TestSuite\Load\Blank2.script",
            });
        }
        #endregion

        #region SetLocal, EndLocal
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandSystem")]
        public void SetEndLocal()
        {
            EngineState s = EngineTests.CreateEngineState();

            void SingleTemplate(List<string> rawCodes, string destComp, string retComp, ErrorCheck check = ErrorCheck.Success)
            {
                s.Variables.DeleteKey(VarsType.Local, "Dest");
                s.ReturnValue = string.Empty;

                EngineTests.EvalLines(s, rawCodes, check);
                if (check == ErrorCheck.Success || check == ErrorCheck.Warning)
                {
                    string dest = s.Variables["Dest"];
                    string ret = s.ReturnValue;
                    Assert.IsTrue(dest.Equals(destComp, StringComparison.Ordinal));
                    Assert.IsTrue(ret.Equals(retComp, StringComparison.Ordinal));
                }
            }
            void ScriptTemplate(string treePath, string entrySection, string destComp, string retComp, ErrorCheck check = ErrorCheck.Success)
            {
                s.Variables.DeleteKey(VarsType.Local, "Dest");
                s.ReturnValue = string.Empty;

                (EngineState st, _) = EngineTests.EvalScript(treePath, check, entrySection);
                if (check == ErrorCheck.Success || check == ErrorCheck.Warning)
                {
                    string dest = st.Variables["Dest"];
                    string ret = st.ReturnValue;
                    Assert.IsTrue(dest.Equals(destComp, StringComparison.Ordinal));
                    Assert.IsTrue(ret.Equals(retComp, StringComparison.Ordinal));
                }
            }

            SingleTemplate(new List<string>
            {
                @"Set,%Dest%,0",
                @"Set,#r,A",
                @"System,SetLocal",
                @"Set,%Dest%,1",
                @"Set,#r,B",
                @"System,EndLocal",
            }, "0", "B");
            SingleTemplate(new List<string>
            {
                @"System,SetLocal",
                @"System,SetLocal",
                @"System,EndLocal",
            }, null, null, ErrorCheck.RuntimeError);
            SingleTemplate(new List<string>
            {
                @"System,EndLocal",
            }, null, null, ErrorCheck.RuntimeError);

            string scPath = Path.Combine(EngineTests.Project.ProjectName, "System", "SetEndLocal.script");
            ScriptTemplate(scPath, "Process-Simple", "0", "B");
            ScriptTemplate(scPath, "Process-Branch", "0", "B");
            ScriptTemplate(scPath, "Process-ImplicitEnd", "-1", "A", ErrorCheck.Warning);
        }
        #endregion

        #region ShellExecute
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandSystem")]
        public void ShellExecute()
        {
            EngineState s = EngineTests.CreateEngineState();
            string pbBatch = Path.Combine("%TestBench%", "CommandSystem", "TestBatch.cmd");
            string destDir = Path.GetRandomFileName();
            try
            {
                File.Delete(destDir);
                Directory.CreateDirectory(destDir);
                string srcBatch = StringEscaper.Preprocess(s, pbBatch);
                string destBatch = Path.Combine(destDir, "TestBatch.cmd");

                void BaseTemplate(string rawCode, string exitKey, string compStr, bool enableCompat = false, ErrorCheck check = ErrorCheck.Success)
                {
                    s.Variables.DeleteKey(VarsType.Local, exitKey);
                    s.ReturnValue = string.Empty;

                    if (!exitKey.Equals("ExitCode", StringComparison.OrdinalIgnoreCase))
                        s.Variables[exitKey] = string.Empty;

                    if (enableCompat)
                        EngineTests.Eval(s, rawCode, CodeType.ShellExecute, check, new CompatOption { DisableExtendedSectionParams = true });
                    else
                        EngineTests.Eval(s, rawCode, CodeType.ShellExecute, check);

                    if (check == ErrorCheck.Success || check == ErrorCheck.Warning)
                    {
                        string dest = s.Variables[exitKey];
                        Assert.IsTrue(dest.Equals(compStr, StringComparison.Ordinal));

                        if (!enableCompat)
                            Assert.IsTrue(s.ReturnValue.Equals(compStr, StringComparison.Ordinal));
                    }

                    s.Variables.DeleteKey(VarsType.Local, "ExitCode");
                    if (!exitKey.Equals("ExitCode", StringComparison.OrdinalIgnoreCase))
                        s.Variables.DeleteKey(VarsType.Local, exitKey);
                }

                void DeleteTemplate(string rawCode, string exitKey, string compStr, ErrorCheck check = ErrorCheck.Success)
                {
                    File.Copy(srcBatch, destBatch, true);

                    s.Variables.DeleteKey(VarsType.Local, exitKey);
                    s.ReturnValue = string.Empty;

                    if (!exitKey.Equals("ExitCode", StringComparison.OrdinalIgnoreCase))
                        s.Variables[exitKey] = string.Empty;

                    EngineTests.Eval(s, rawCode, CodeType.ShellExecuteDelete, check);
                    if (check == ErrorCheck.Success || check == ErrorCheck.Warning)
                    {
                        string dest = s.Variables[exitKey];
                        Assert.IsTrue(dest.Equals(compStr, StringComparison.Ordinal));
                        Assert.IsFalse(File.Exists(destBatch));
                    }

                    s.Variables.DeleteKey(VarsType.Local, "ExitCode");
                    if (!exitKey.Equals("ExitCode", StringComparison.OrdinalIgnoreCase))
                        s.Variables.DeleteKey(VarsType.Local, exitKey);
                }

                BaseTemplate($@"ShellExecute,Open,{pbBatch},78", "ExitCode", "78");
                BaseTemplate($@"ShellExecute,Open,{pbBatch},78", "ExitCode", "78", true);
                BaseTemplate($@"ShellExecute,Open,{pbBatch},3,,%Dest%", "Dest", "3");
                BaseTemplate($@"ShellExecute,Open,{pbBatch},3,,%Dest%", "ExitCode", "3", false, ErrorCheck.Warning);
                DeleteTemplate($@"ShellExecuteDelete,Open,{destBatch},78", "ExitCode", "78");
                DeleteTemplate($@"ShellExecuteDelete,Open,{destBatch},3,,%Dest%", "Dest", "3");
                DeleteTemplate($@"ShellExecuteDelete,Open,{destBatch},3,,%Dest%", "ExitCode", "3", ErrorCheck.Warning);
            }
            finally
            {
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, true);
            }
        }
        #endregion
    }
}
