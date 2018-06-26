﻿/*
    Copyright (C) 2017-2018 Hajin Jang
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
using System.Linq;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.Core;
// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local

namespace PEBakery.Tests.Core.Command
{
    [TestClass]
    public class CommandSystemTests
    {
        #region SetLocal, EndLocal
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
            }, ErrorCheck.Error);

            string scPath = Path.Combine(EngineTests.Project.ProjectName, "System", "ErrorOff.script");
            ScriptTemplate(scPath);
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
                        string destTreeDir = Path.GetDirectoryName(destTreePath);
                        Assert.IsTrue(s.Project.ContainsScriptByTreePath(destTreeDir));
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
                s.Variables.Delete(VarsType.Local, "Dest");
                s.SectionReturnValue = string.Empty;

                EngineTests.EvalLines(s, rawCodes, check);
                if (check == ErrorCheck.Success || check == ErrorCheck.Warning)
                {
                    string dest = s.Variables["Dest"];
                    string ret = s.SectionReturnValue;
                    Assert.IsTrue(dest.Equals(destComp, StringComparison.Ordinal));
                    Assert.IsTrue(ret.Equals(retComp, StringComparison.Ordinal));
                }
            }
            void ScriptTemplate(string treePath, string destComp, string retComp, ErrorCheck check = ErrorCheck.Success)
            {
                s.Variables.Delete(VarsType.Local, "Dest");
                s.SectionReturnValue = string.Empty;

                (EngineState st, _) = EngineTests.EvalScript(treePath, check);
                if (check == ErrorCheck.Success || check == ErrorCheck.Warning)
                {
                    string dest = st.Variables["Dest"];
                    string ret = st.SectionReturnValue;
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
            }, null, null, ErrorCheck.Error);
            SingleTemplate(new List<string>
            {
                @"System,EndLocal",
            }, null, null, ErrorCheck.Error);

            string scPath = Path.Combine(EngineTests.Project.ProjectName, "System", "SetEndLocal.script");
            ScriptTemplate(scPath, "0", "B");
        }
        #endregion
    }
}
