/*
    Copyright (C) 2019 Hajin Jang
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
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PEBakery.Core.Tests
{
    [TestClass]
    public class FileTypeDetectorTests
    {
        #region IsText
        private readonly Dictionary<string, bool> _isTextResultDict = new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["Banner.7z"] = false,
            ["Banner.svg"] = true,
            ["Banner.zip"] = false,
            ["CP949.txt"] = true,
            ["Random.bin"] = false,
            ["ShiftJIS.html"] = true,
            ["Type3.pdf"] = false,
            ["UTF16BE.txt"] = true,
            ["UTF16LE.txt"] = true,
            ["UTF8.txt"] = true,
            ["UTF8woBOM.txt"] = true,
            ["Zero.bin"] = false,
        };

        [TestMethod]
        [TestCategory("StringEscaper")]
        public void IsText()
        {
            string testBench = EngineTests.Project.Variables.Expand("%TestBench%");
            string srcDir = Path.Combine(testBench, "FileTypeDetector");
            string[] files = Directory.GetDirectories(srcDir);
            foreach (string file in files)
            {
                string fileName = Path.GetFileName(file);
                Assert.IsNotNull(fileName);
                bool expected = _isTextResultDict[fileName];
                bool ret = FileTypeDetector.IsText(file);
                Assert.AreEqual(expected, ret);
            }
        }
        #endregion
    }
}
