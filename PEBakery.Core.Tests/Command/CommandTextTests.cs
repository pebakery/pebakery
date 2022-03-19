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
using PEBakery.Helper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local

namespace PEBakery.Core.Tests.Command
{
    [TestClass]
    public class CommandTextTests
    {
        #region TXTAddLine
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandText")]
        public void TXTAddLine()
        {
            EngineState s = EngineTests.CreateEngineState();

            string tempDir = FileHelper.GetTempDir();
            try
            {
                string tempFile = Path.Combine(tempDir, "Sample.txt");
                string tempFile2 = Path.Combine(tempDir, "Sample2.txt");

                // Test empty string
                SingleTemplate(s, CodeType.TXTAddLine, $@"TXTAddLine,{tempFile},C,Append", tempFile, string.Empty, "C\r\n");
                SingleTemplate(s, CodeType.TXTAddLine, $@"TXTAddLine,{tempFile},C,Prepend", tempFile, string.Empty, "C\r\n");

                // Test normal text
                SingleTemplate(s, CodeType.TXTAddLine, $@"TXTAddLine,{tempFile},C,Append", tempFile, "A\r\nB", "A\r\nB\r\nC\r\n");
                SingleTemplate(s, CodeType.TXTAddLine, $@"TXTAddLine,{tempFile},C,Prepend", tempFile, "A\r\nB", "C\r\nA\r\nB\r\n");

                // Test unicode text
                SingleTemplate(s, CodeType.TXTAddLine, $@"TXTAddLine,{tempFile},나,Append", tempFile, "가\r\n", "가\r\n나\r\n");

                // Test error
                SingleTemplate(s, CodeType.TXTAddLine, $@"TXTAddLine,{tempFile},C,ErrorMode", tempFile, string.Empty, string.Empty, ErrorCheck.ParserError);

                // Optimization
                OptTemplate(s, CodeType.TXTAddLineOp, new List<string>
                {
                    $@"TXTAddLine,{tempFile},X,Append",
                    $@"TXTAddLine,{tempFile},Y,Append",
                    $@"TXTAddLine,{tempFile},Z,Append",
                }, tempFile, string.Empty, "X\r\nY\r\nZ\r\n");
                OptTemplate(s, CodeType.TXTAddLineOp, new List<string>
                {
                    $@"TXTAddLine,{tempFile},X,Append",
                    $@"TXTAddLine,{tempFile},Y,Append",
                    $@"TXTAddLine,{tempFile},Z,Append",
                }, tempFile, "A\r\nB", "A\r\nB\r\nX\r\nY\r\nZ\r\n");
                OptTemplate(s, CodeType.TXTAddLineOp, new List<string>
                {
                    $@"TXTAddLine,{tempFile},X,Prepend",
                    $@"TXTAddLine,{tempFile},Y,Prepend",
                    $@"TXTAddLine,{tempFile},Z,Prepend",
                }, tempFile, "A\r\nB", "Z\r\nY\r\nX\r\nA\r\nB");
                OptTemplate(s, null, new List<string>
                {
                    $@"TXTAddLine,{tempFile},X,Append",
                    $@"TXTAddLine,{tempFile},Y,Append",
                    $@"TXTAddLine,{tempFile2},Z,Append",
                }, tempFile, "A\r\nB", "A\r\nB\r\nX\r\nY\r\n");
                OptTemplate(s, null, new List<string>
                {
                    $@"TXTAddLine,{tempFile},X,Prepend",
                    $@"TXTAddLine,{tempFile},Y,Prepend",
                    $@"TXTAddLine,{tempFile},Y,Append",
                    $@"TXTAddLine,{tempFile},Z,Prepend",
                }, tempFile, "A\r\nB", "Z\r\nY\r\nX\r\nA\r\nB\r\nY\r\n");
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
        public void TXTReplace()
        {
            EngineState s = EngineTests.CreateEngineState();

            string tempDir = FileHelper.GetTempDir();
            try
            {
                string tempFile = Path.Combine(tempDir, "Sample.txt");
                string tempFile2 = Path.Combine(tempDir, "Sample2.txt");

                SingleTemplate(s, CodeType.TXTReplace, $@"TXTReplace,{tempFile},AB,XYZ", tempFile, "ABCD", "XYZCD");
                SingleTemplate(s, CodeType.TXTReplace, $@"TXTReplace,{tempFile},ab,XYZ", tempFile, "ABCD", "XYZCD");
                SingleTemplate(s, CodeType.TXTReplace, $@"TXTReplace,{tempFile},AB,XYZ", tempFile, "abcd", "XYZcd");
                SingleTemplate(s, CodeType.TXTReplace, $@"TXTReplace,{tempFile},ab,XYZ", tempFile, "abcd", "XYZcd");

                // Unicode, NewLine Test
                SingleTemplate(s, CodeType.TXTReplace, $@"TXTReplace,{tempFile},#$x,나다", tempFile, "가\r\n라", "가나다라");

                // Optimization
                OptTemplate(s, CodeType.TXTReplaceOp, new List<string>
                {
                    $@"TXTReplace,{tempFile},AB,PE",
                    $@"TXTReplace,{tempFile},PE,XY",
                    $@"TXTReplace,{tempFile},XYZ,TEB",
                }, tempFile, "ABC\r\nXYZ\r\nABC\r\n가나다", "XYC\r\nTEB\r\nXYC\r\n가나다");
                OptTemplate(s, null, new List<string>
                {
                    $@"TXTReplace,{tempFile},AB,PE",
                    $@"TXTReplace,{tempFile2},XYZ,TEB",
                }, tempFile, "ABC\r\nXYZ\r\nABC\r\n가나다", "PEC\r\nXYZ\r\nPEC\r\n가나다");
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
        public void TXTDelLine()
        {
            EngineState s = EngineTests.CreateEngineState();

            string tempDir = FileHelper.GetTempDir();
            string sampleStr = GenerateSampleText(true, true, true, true);
            try
            {
                string tempFile = Path.Combine(tempDir, "TXTReplace.txt");
                string tempFile2 = Path.Combine(tempDir, "TXTReplace2.txt");

                // Test empty string
                // Strange, but WB082 works like this
                SingleTemplate(s, CodeType.TXTDelLine, $@"TXTDelLine,{tempFile},Z", tempFile, string.Empty, string.Empty);

                // Test normal text
                SingleTemplate(s, CodeType.TXTDelLine, $@"TXTDelLine,{tempFile},AB", tempFile, sampleStr, GenerateSampleText(false, true, true, true));
                SingleTemplate(s, CodeType.TXTDelLine, $@"TXTDelLine,{tempFile},ab", tempFile, sampleStr, GenerateSampleText(true, true, true, true));
                SingleTemplate(s, CodeType.TXTDelLine, $@"TXTDelLine,{tempFile},D", tempFile, sampleStr, GenerateSampleText(true, false, true, true));
                SingleTemplate(s, CodeType.TXTDelLine, $@"TXTDelLine,{tempFile},d", tempFile, sampleStr, GenerateSampleText(true, true, true, true));
                SingleTemplate(s, CodeType.TXTDelLine, $@"TXTDelLine,{tempFile},Z", tempFile, sampleStr, GenerateSampleText(true, true, true, true));
                SingleTemplate(s, CodeType.TXTDelLine, $@"TXTDelLine,{tempFile},z", tempFile, sampleStr, GenerateSampleText(true, true, true, true));

                // Test unicode text
                SingleTemplate(s, CodeType.TXTDelLine, $@"TXTDelLine,{tempFile},가", tempFile, sampleStr, GenerateSampleText(true, true, true, false));

                // Optimization
                OptTemplate(s, CodeType.TXTDelLineOp, new List<string>
                {
                    $@"TXTDelLine,{tempFile},AB",
                    $@"TXTDelLine,{tempFile},XY",
                }, tempFile, sampleStr, GenerateSampleText(false, true, false, true));
                OptTemplate(s, null, new List<string>
                {
                    $@"TXTDelLine,{tempFile},AB",
                    $@"TXTDelLine,{tempFile2},XY",
                }, tempFile, sampleStr, GenerateSampleText(false, true, true, true));
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }

            static string GenerateSampleText(bool t1, bool t2, bool t3, bool t4)
            {
                StringBuilder b = new StringBuilder();
                if (t1) b.AppendLine("ABC");
                if (t2) b.AppendLine("DEF");
                if (t3) b.AppendLine("XYZ");
                if (t4) b.AppendLine("가나다");
                return b.ToString();
            }
        }
        #endregion

        #region TXTDelSpaces
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandText")]
        public void TXTDelSpaces()
        {
            EngineState s = EngineTests.CreateEngineState();

            string tempDir = FileHelper.GetTempDir();
            try
            {
                string tempFile = Path.Combine(tempDir, "Sample.txt");

                StringBuilder b = new StringBuilder();
                b.AppendLine("A B C");
                b.AppendLine(" D E F");
                b.AppendLine("G H I ");
                b.AppendLine("  J K L");
                b.AppendLine("M N O  ");
                b.AppendLine("  X Y Z  ");
                b.AppendLine();
                b.AppendLine("\t가 나 다");
                string sampleStr = b.ToString();

                SingleTemplate(s, CodeType.TXTDelSpaces, $@"TXTDelSpaces,{tempFile}", tempFile, string.Empty, string.Empty);

                b = new StringBuilder();
                b.AppendLine("A B C");
                b.AppendLine("D E F");
                b.AppendLine("G H I");
                b.AppendLine("J K L");
                b.AppendLine("M N O");
                b.AppendLine("X Y Z");
                b.AppendLine();
                b.AppendLine("가 나 다");
                SingleTemplate(s, CodeType.TXTDelSpaces, $@"TXTDelSpaces,{tempFile}", tempFile, sampleStr, b.ToString());
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
        public void TXTDelEmptyLines()
        {
            EngineState s = EngineTests.CreateEngineState();

            string tempDir = FileHelper.GetTempDir();
            try
            {
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

                SingleTemplate(s, CodeType.TXTDelEmptyLines, $@"TXTDelEmptyLines,{tempFile}", tempFile, string.Empty, string.Empty);

                b = new StringBuilder();
                b.AppendLine("A B C");
                b.AppendLine(" D E F");
                b.AppendLine("  X Y Z");
                b.AppendLine("\t가 나 다");
                b.AppendLine("힣");
                SingleTemplate(s, CodeType.TXTDelEmptyLines, $@"TXTDelEmptyLines,{tempFile}", tempFile, sampleStr, b.ToString());
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
        #endregion

        #region Template
        private static void SingleTemplate(
            EngineState s, CodeType type,
            string rawCode, string testFile, string sampleStr, string compStr,
            ErrorCheck check = ErrorCheck.Success)
        {
            if (File.Exists(testFile))
                File.Delete(testFile);
            try
            {
                File.Create(testFile).Close();

                EncodingHelper.WriteTextBom(testFile, Encoding.UTF8);
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

                    Assert.IsTrue(dest.Equals(compStr, StringComparison.Ordinal));
                }
            }
            finally
            {
                if (File.Exists(testFile))
                    File.Delete(testFile);
            }
        }

        private static void OptTemplate(
            EngineState s, CodeType? opType,
            List<string> rawCodes, string testFile, string sampleStr, string compStr,
            ErrorCheck check = ErrorCheck.Success)
        {
            if (File.Exists(testFile))
                File.Delete(testFile);
            File.Create(testFile).Close();
            try
            {
                EncodingHelper.WriteTextBom(testFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(testFile, true, Encoding.UTF8))
                {
                    w.Write(sampleStr);
                }

                EngineTests.EvalOptLines(s, opType, rawCodes, check);
                if (check == ErrorCheck.Success || check == ErrorCheck.Warning)
                {
                    string dest;
                    using (StreamReader r = new StreamReader(testFile, Encoding.UTF8))
                    {
                        dest = r.ReadToEnd();
                    }

                    Assert.IsTrue(dest.Equals(compStr, StringComparison.Ordinal));
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
