/*
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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.Core;

namespace PEBakery.Tests.Core.Command
{
    [TestClass]
    public class CommandSystemTests
    {
        #region LoadNewScript
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandString")]
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
    }
}
