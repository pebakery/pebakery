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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PEBakery.Core;
using System.IO;
using PEBakery.Helper;

namespace PEBakery.Tests.Core.Command
{
    [TestClass]
    public class CommandTextTests
    {
        #region TXTAddLine
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandText")]
        public void Text_TXTAddLine()
        { // TXTAddLine,<FileName>,<Line>,<Mode>
            EngineState s = EngineTests.CreateEngineState();

            string tempDir = Path.GetTempFileName();
            File.Delete(tempDir);
            Directory.CreateDirectory(tempDir);
            string tempFile = Path.Combine(tempDir, "Sample.txt");

            try
            {
                // Test empty string
                Text_Template(s, CodeType.TXTAddLine, $@"TXTAddLine,{tempFile},C,Append", tempFile, string.Empty, "C\r\n");
                Text_Template(s, CodeType.TXTAddLine, $@"TXTAddLine,{tempFile},C,Prepend", tempFile, string.Empty, "C\r\n");

                // Test normal text
                Text_Template(s, CodeType.TXTAddLine, $@"TXTAddLine,{tempFile},C,Append", tempFile, "A\r\nB", "A\r\nB\r\nC\r\n");
                Text_Template(s, CodeType.TXTAddLine, $@"TXTAddLine,{tempFile},C,Prepend", tempFile, "A\r\nB", "C\r\nA\r\nB\r\n");

                // Test unicode text
                Text_Template(s, CodeType.TXTAddLine, $@"TXTAddLine,{tempFile},나,Append", tempFile, "가\r\n", "가\r\n나\r\n");

                // Test error
                Text_Template(s, CodeType.TXTAddLine, $@"TXTAddLine,{tempFile},C,ErrorMode", tempFile, string.Empty, string.Empty, ErrorCheck.ParserError);           
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
        #endregion

        #region TXTReplace
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandText")]
        public void Text_TXTReplace()
        { // TXTReplace,<FileName>,<OldStr>,<NewStr>
            EngineState s = EngineTests.CreateEngineState();

            string tempDir = Path.GetTempFileName();
            File.Delete(tempDir);
            Directory.CreateDirectory(tempDir);
            string tempFile = Path.Combine(tempDir, "Sample.txt");

            try
            {
                Text_Template(s, CodeType.TXTReplace, $@"TXTReplace,{tempFile},AB,XYZ", tempFile, "ABCD", "XYZCD");
                Text_Template(s, CodeType.TXTReplace, $@"TXTReplace,{tempFile},ab,XYZ", tempFile, "ABCD", "XYZCD");
                Text_Template(s, CodeType.TXTReplace, $@"TXTReplace,{tempFile},AB,XYZ", tempFile, "abcd", "XYZcd");
                Text_Template(s, CodeType.TXTReplace, $@"TXTReplace,{tempFile},ab,XYZ", tempFile, "abcd", "XYZcd");

                // Unicode, NewLine Test
                Text_Template(s, CodeType.TXTReplace, $@"TXTReplace,{tempFile},#$x,나다", tempFile, "가\r\n라", "가나다라");
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
        #endregion

        #region TXTDelLine
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandText")]
        public void Text_TXTDelLine()
        { // TXTDelLine,<FileName>,<DeleteLine>
            EngineState s = EngineTests.CreateEngineState();

            string tempDir = Path.GetTempFileName();
            File.Delete(tempDir);
            Directory.CreateDirectory(tempDir);
            string tempFile = Path.Combine(tempDir, "TXTReplace.txt");

            string sampleStr = Generate_SampleText(true, true, true, true);

            try
            {
                // Test empty string
                // Strange, but WB082 works like this
                Text_Template(s, CodeType.TXTDelLine, $@"TXTDelLine,{tempFile},Z", tempFile, string.Empty, string.Empty);

                // Test normal text
                Text_Template(s, CodeType.TXTDelLine, $@"TXTDelLine,{tempFile},AB", tempFile, sampleStr, Generate_SampleText(false, true, true, true));
                Text_Template(s, CodeType.TXTDelLine, $@"TXTDelLine,{tempFile},ab", tempFile, sampleStr, Generate_SampleText(true, true, true, true));
                Text_Template(s, CodeType.TXTDelLine, $@"TXTDelLine,{tempFile},D", tempFile, sampleStr, Generate_SampleText(true, false, true, true));
                Text_Template(s, CodeType.TXTDelLine, $@"TXTDelLine,{tempFile},d", tempFile, sampleStr, Generate_SampleText(true, true, true, true));
                Text_Template(s, CodeType.TXTDelLine, $@"TXTDelLine,{tempFile},Z", tempFile, sampleStr, Generate_SampleText(true, true, true, true));
                Text_Template(s, CodeType.TXTDelLine, $@"TXTDelLine,{tempFile},z", tempFile, sampleStr, Generate_SampleText(true, true, true, true));

                // Test unicode text
                Text_Template(s, CodeType.TXTDelLine, $@"TXTDelLine,{tempFile},가", tempFile, sampleStr, Generate_SampleText(true, true, true, false));
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        public string Generate_SampleText(bool t1, bool t2, bool t3, bool t4)
        {
            StringBuilder b = new StringBuilder();
            if (t1) b.AppendLine("ABC");
            if (t2) b.AppendLine("DEF");
            if (t3) b.AppendLine("XYZ");
            if (t4) b.AppendLine("가나다");
            return b.ToString();
        }
        #endregion

        #region TXTDelSpaces
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandText")]
        public void Text_TXTDelSpaces()
        { // TXTDelSpaces,<FileName>
            EngineState s = EngineTests.CreateEngineState();

            string tempDir = Path.GetTempFileName();
            File.Delete(tempDir);
            Directory.CreateDirectory(tempDir);
            string tempFile = Path.Combine(tempDir, "Sample.txt");

            StringBuilder b = new StringBuilder();
            b.AppendLine("A B C");
            b.AppendLine(" D E F");
            b.AppendLine("  X Y Z");
            b.AppendLine();
            b.AppendLine("\t가 나 다");
            string sampleStr = b.ToString();

            try
            {
                Text_Template(s, CodeType.TXTDelSpaces, $@"TXTDelSpaces,{tempFile}", tempFile, string.Empty, string.Empty);

                b = new StringBuilder();
                b.AppendLine("A B C");
                b.AppendLine("D E F");
                b.AppendLine("X Y Z");
                b.AppendLine();
                b.AppendLine("가 나 다");
                Text_Template(s, CodeType.TXTDelSpaces, $@"TXTDelSpaces,{tempFile}", tempFile, sampleStr, b.ToString());
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
        #endregion

        #region TXTDelEmptyLines
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandText")]
        public void Text_TXTDelEmptyLines()
        { // TXTDelEmptyLines,<FileName>
            EngineState s = EngineTests.CreateEngineState();

            string tempDir = Path.GetTempFileName();
            File.Delete(tempDir);
            Directory.CreateDirectory(tempDir);
            string tempFile = Path.Combine(tempDir, "Sample.txt");

            StringBuilder b = new StringBuilder();
            b.AppendLine("A B C");
            b.AppendLine(" D E F");
            b.AppendLine("  X Y Z");
            b.AppendLine();
            b.AppendLine("\t가 나 다");
            b.AppendLine();
            b.AppendLine("힣");
            string sampleStr = b.ToString();

            try
            {
                Text_Template(s, CodeType.TXTDelEmptyLines, $@"TXTDelEmptyLines,{tempFile}", tempFile, string.Empty, string.Empty);

                b = new StringBuilder();
                b.AppendLine("A B C");
                b.AppendLine(" D E F");
                b.AppendLine("  X Y Z");
                b.AppendLine("\t가 나 다");
                b.AppendLine("힣");
                Text_Template(s, CodeType.TXTDelEmptyLines, $@"TXTDelEmptyLines,{tempFile}", tempFile, sampleStr, b.ToString());
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
        #endregion

        #region Utility
        private void Text_Template(
            EngineState s,
            CodeType type,
            string rawCode,
            string testFile,
            string sampleStr,
            string comp,
            ErrorCheck check = ErrorCheck.Success)
        {
            if (File.Exists(testFile))
                File.Delete(testFile);
            File.Create(testFile).Close();
            try
            {
                FileHelper.WriteTextBom(testFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(testFile, true, Encoding.UTF8))
                {
                    w.Write(sampleStr);
                }

                EngineTests.Eval(s, rawCode, type, check);
                if (check == ErrorCheck.Success || check == ErrorCheck.Warning)
                {
                    string dest;
                    using (StreamReader r = new StreamReader(testFile, Encoding.UTF8))
                    {
                        dest = r.ReadToEnd();
                    }

                    Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
                }
            }
            finally
            {
                if (File.Exists(testFile))
                    File.Delete(testFile);
            }
        }
        #endregion
    }
}
