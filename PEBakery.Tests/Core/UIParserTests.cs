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
using System.Threading.Tasks;
using PEBakery.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PEBakery.Tests.Core
{
    [TestClass]
    public class UIParserTests
    {
        #region BlockComments
        [TestMethod]
        [TestCategory("UIParser")]
        public void BlockComments()
        {
            void Template(List<string> rawLines, bool success = true)
            {
                SectionAddress addr = EngineTests.DummySectionAddress();
                UIParser.ParseStatements(rawLines, addr, out List<LogInfo> errLogs);

                if (success)
                    Assert.IsTrue(errLogs.Count == 0);
                else
                    Assert.IsTrue(0 < errLogs.Count);
            }

            Template(new List<string> { "/* Block Comment 1 */" });
            Template(new List<string> { "/* Block Comment 2", "ABC */" });
            Template(new List<string> { "/* Block Comment 3", "DEF", "*/" });
            Template(new List<string> { "/* Block Comment 4", "XYZ */ Error" });
            Template(new List<string> { "/* Block Comment 5", "No end identifier" }, false);
        }
        #endregion
    }
}
