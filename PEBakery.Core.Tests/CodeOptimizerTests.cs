/*
    Copyright (C) 2022 Hajin Jang
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PEBakery.Core.Tests
{
    [TestClass]
    public class CodeOptimizerTests
    {
        [TestMethod]
        [TestCategory(nameof(CodeOptimizer))]
        public void DepedencyCheck()
        {
            string[] lines = new string[]
            {
                "IniRead,%SrcFile%,Section,Key,%Dest%",
                "IniRead,%SrcFile%,%Dest%,Key,%Dest%",
                "IniRead,%SrcFile%,Section,Key,%Dest%",
            };
            CodeCommand[] cmds = EngineTests.ParseLines(lines, out List<LogInfo> errorLogs);
            Assert.IsTrue(errorLogs.Count == 0);
            Assert.AreEqual(2, cmds.Length);

            Assert.IsTrue(cmds[0].Info is CodeInfo_IniRead);
            {
                Assert.IsTrue(cmds[1].Info is CodeInfo_IniReadOp);
                CodeInfo_IniReadOp opCmdInfo = (CodeInfo_IniReadOp)cmds[1].Info;
                Assert.AreEqual(2, opCmdInfo.Infos.Count);
            }

        }
    }
}
