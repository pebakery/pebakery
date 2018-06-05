/*
    Copyright (C) 2018 Hajin Jang
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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PEBakery.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PEBakery.Tests.Core
{
    [TestClass]
    public class ScriptTests
    {
        #region BlockComment (ParseScript)
        [TestMethod]
        [TestCategory("Script")]
        public void BlockComment()
        {
            EngineState s = EngineTests.CreateEngineState();

            string treePath = Path.Combine("TestSuite", "Core", "ParseScript.script");
            Script sc = s.Project.GetScriptByTreePath(treePath);

            Assert.IsTrue(sc.Sections.ContainsKey("Section1"));
            Assert.IsTrue(sc.Sections.ContainsKey("Section2"));
            Assert.IsFalse(sc.Sections.ContainsKey("Section3"));

            Assert.IsFalse(EncodedFile.ContainsFile(sc, "AttachTest", "UTF8.txt"));
            Assert.IsFalse(sc.Sections.ContainsKey(EncodedFile.GetSectionName("AttachTest", "UTF8.txt")));
        }
        #endregion
    }
}
