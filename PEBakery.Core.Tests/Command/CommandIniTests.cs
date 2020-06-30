/*
    Copyright (C) 2018-2020 Hajin Jang
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
    [TestCategory(nameof(Command))]
    [TestCategory(nameof(Commands.CommandIni))]
    public class CommandIniTests
    {
        #region SampleStr
        private static readonly string[] SampleLines =
        {
            "[Sec1]", // 0
            "A=1", // 1
            "B=2", // 2
            "C=3", // 3
            string.Empty, // 4
            "[Sec2]", // 5
            "// Comment", // 6
            "X=%Four%", // 7
            "Y=5", // 8
            "Z=6", // 9
            string.Empty, // 10
            "; Unicode", // 11
            "[Sec3]", // 12
            "가=7", // 13
            "# Sharp", // 14
            "나=8", // 15
            "다=9", // 16
            string.Empty, // 17
            "[Doublequote]", // 18
            "CUR_DIR       = \"Cursors\\Material Design Cursors\"", // 21, See Issue #134
            "DQ1=\"A B C\"", // 19
            "DQ2  =  \"X\\Y\\Z\"", // 20, See Issue #134
        };

        private static string SampleStr()
        {
            StringBuilder b = new StringBuilder();
            foreach (string s in SampleLines)
                b.AppendLine(s);
            return b.ToString();
        }
        #endregion

        #region IniRead
        [TestMethod]
        public void IniRead()
        {
            EngineState s = EngineTests.CreateEngineState();
            string sampleStr = SampleStr();

            string tempDir = FileHelper.GetTempDir();
            try
            {
                string tempFile = Path.Combine(tempDir, "sample.ini");
                string tempFile2 = Path.Combine(tempDir, "empty.ini");

                ReadTemplate(s, CodeType.IniRead, $@"IniRead,{tempFile},Sec1,A,%Dest%", tempFile, sampleStr, "1");
                ReadTemplate(s, CodeType.IniRead, $@"IniRead,{tempFile},Sec1,B,%Dest%", tempFile, sampleStr, "2");
                ReadTemplate(s, CodeType.IniRead, $@"IniRead,{tempFile},Sec2,Z,%Dest%", tempFile, sampleStr, "6");
                ReadTemplate(s, CodeType.IniRead, $@"IniRead,{tempFile},Sec3,나,%Dest%", tempFile, sampleStr, "8");
                ReadTemplate(s, CodeType.IniRead, $@"IniRead,{tempFile},Sec2,無,%Dest%", tempFile, sampleStr, string.Empty);
                ReadTemplate(s, CodeType.IniRead, $@"IniRead,{tempFile},Sec2,Z", tempFile, string.Empty, null, ErrorCheck.ParserError);
                ReadTemplate(s, CodeType.IniRead, $@"IniRead,{tempFile},Doublequote,DQ1,%Dest%", tempFile, sampleStr, "#$qA B C#$q");
                ReadTemplate(s, CodeType.IniRead, $@"IniRead,{tempFile},Doublequote,DQ2,%Dest%", tempFile, sampleStr, "#$qX\\Y\\Z#$q");
                ReadTemplate(s, CodeType.IniRead, $@"IniRead,{tempFile},Doublequote,CUR_DIR,%Dest%", tempFile, sampleStr, "#$qCursors\\Material Design Cursors#$q");

                // Optimization
                ReadOptTemplate(s, CodeType.IniReadOp, new List<string>
                {
                    $@"IniRead,{tempFile},Sec1,A,%Dest0%",
                    $@"IniRead,{tempFile},Sec1,B,%Dest1%",
                    $@"IniRead,{tempFile},Sec2,Z,%Dest2%",
                    $@"IniRead,{tempFile},Sec3,나,%Dest3%",
                }, tempFile, sampleStr, new string[] { "1", "2", "6", "8" });
                ReadOptTemplate(s, null, new List<string>
                {
                    $@"IniRead,{tempFile},Sec1,A,%Dest0%",
                    $@"IniRead,{tempFile},Sec1,B,%Dest1%",
                    $@"IniRead,{tempFile},Sec2,Z,%Dest2%",
                    $@"IniRead,{tempFile2},Sec3,나,%Dest3%",
                }, tempFile, sampleStr, new string[] { "1", "2", "6" });
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
        #endregion

        #region IniWrite
        [TestMethod]
        public void IniWrite()
        {
            EngineState s = EngineTests.CreateEngineState();

            string tempDir = FileHelper.GetTempDir();
            try
            {
                string tempFile = Path.Combine(tempDir, "samples.ini");
                string tempFile2 = Path.Combine(tempDir, "empty.ini");

                StringBuilder b = new StringBuilder();
                b.AppendLine("[6DoF]");
                b.AppendLine("Descent=Overload");
                string resultStr = b.ToString();
                WriteTemplate(s, CodeType.IniWrite, $@"IniWrite,{tempFile},6DoF,Descent,Overload", tempFile, string.Empty, resultStr);

                b.Clear();
                b.AppendLine("[6DoF]");
                b.AppendLine("Descent=Overload");
                string sampleStr = b.ToString();
                b.Clear();
                b.AppendLine("[6DoF]");
                b.AppendLine("Descent=Overload");
                b.AppendLine();
                b.AppendLine("[Update]");
                b.AppendLine("Roguelike=Sublevel Zero Redux");
                resultStr = b.ToString();
                WriteTemplate(s, CodeType.IniWrite, $@"IniWrite,{tempFile},Update,Roguelike,Sublevel Zero Redux", tempFile, sampleStr, resultStr);

                b.Clear();
                b.AppendLine("[6DoF]");
                b.AppendLine("Descent=Overload");
                sampleStr = b.ToString();
                b.Clear();
                b.AppendLine("[6DoF]");
                b.AppendLine("Descent=Sublevel Zero Redux");
                b.AppendLine();
                resultStr = b.ToString();
                WriteTemplate(s, CodeType.IniWrite, $@"IniWrite,{tempFile},6DoF,Descent,Sublevel Zero Redux", tempFile, sampleStr, resultStr);

                b.Clear();
                b.AppendLine("[6DoF]");
                b.AppendLine("// Descent=1");
                b.AppendLine("# Descent=2");
                b.AppendLine("; Descent=Freespace");
                b.AppendLine("Descent=Overload");
                sampleStr = b.ToString();
                b.Clear();
                b.AppendLine("[6DoF]");
                b.AppendLine("// Descent=1");
                b.AppendLine("# Descent=2");
                b.AppendLine("; Descent=Freespace");
                b.AppendLine("Descent=Sublevel Zero Redux");
                b.AppendLine();
                resultStr = b.ToString();
                WriteTemplate(s, CodeType.IniWrite, $@"IniWrite,{tempFile},6DoF,Descent,Sublevel Zero Redux", tempFile, sampleStr, resultStr);

                WriteTemplate(s, CodeType.IniWrite, $@"IniWRite,{tempFile},A,B", tempFile, string.Empty, null, ErrorCheck.ParserError);

                // Optimization
                b.Clear();
                b.AppendLine("[6DoF]");
                b.AppendLine("Descent=Overload");
                b.AppendLine();
                b.AppendLine("[Update]");
                b.AppendLine("Roguelike=Sublevel Zero Redux");
                resultStr = b.ToString();
                WriteOptTemplate(s, CodeType.IniWriteOp, new List<string>
                {
                    $@"IniWrite,{tempFile},6DoF,Descent,Overload",
                    $@"IniWrite,{tempFile},Update,Roguelike,Sublevel Zero Redux",
                }, tempFile, string.Empty, resultStr);

                b.Clear();
                b.AppendLine("[6DoF]");
                b.AppendLine("// Descent=1");
                b.AppendLine("# Descent=2");
                b.AppendLine("; Descent=Freespace");
                b.AppendLine("Descent=Overload");
                sampleStr = b.ToString();
                b.Clear();
                b.AppendLine("[6DoF]");
                b.AppendLine("// Descent=1");
                b.AppendLine("# Descent=2");
                b.AppendLine("; Descent=Freespace");
                b.AppendLine("Descent=Sublevel Zero Redux");
                b.AppendLine();
                b.AppendLine();
                b.AppendLine("[Update]");
                b.AppendLine("Parallax=Revival");
                resultStr = b.ToString();
                WriteOptTemplate(s, CodeType.IniWriteOp, new List<string>
                {
                    $@"IniWrite,{tempFile},6DoF,Descent,Sublevel Zero Redux",
                    $@"IniWrite,{tempFile},Update,Parallax,Revival",
                }, tempFile, sampleStr, resultStr);

                b.Clear();
                b.AppendLine("[6DoF]");
                b.AppendLine("Descent=Overload");
                sampleStr = b.ToString();
                b.Clear();
                b.AppendLine("[6DoF]");
                b.AppendLine("Descent=Sublevel Zero Redux");
                b.AppendLine();
                resultStr = b.ToString();
                WriteOptTemplate(s, null, new List<string>
                {
                    $@"IniWrite,{tempFile},6DoF,Descent,Sublevel Zero Redux",
                    $@"IniWrite,{tempFile2},6DoF,Parallax,Revival",
                }, tempFile, sampleStr, resultStr);

                // Passthrough + Doublequote test, Issue #134
                b.Clear();
                b.AppendLine("[Passthrough]");
                b.AppendLine("DQ1  =  \"A B C\"");
                b.AppendLine("DQ2  =  \"X\\Y\\Z\"");
                sampleStr = b.ToString();
                b.Clear();
                b.AppendLine("[Passthrough]");
                b.AppendLine("DQ1=\"A B C D\"");
                b.AppendLine("DQ2  =  \"X\\Y\\Z\"");
                b.AppendLine();
                resultStr = b.ToString();
                WriteTemplate(s, CodeType.IniWrite, $@"IniWrite,{tempFile},Passthrough,DQ1,#$qA B C D#$q", tempFile, sampleStr, resultStr);

                // Compat (AutoCompactIniWriteCommand)
                s.CompatAutoCompactIniWriteCommand = true;
                b.Clear();
                b.AppendLine("[Section1] ");
                b.AppendLine("A=1");
                b.AppendLine(" B = 2");
                b.AppendLine("C = 3 ");
                b.AppendLine(" D = 4 ");
                b.AppendLine();
                b.AppendLine(" [Section2]");
                b.AppendLine("ㄱ=甲");
                b.AppendLine(" ㄴ = 乙");
                b.AppendLine("ㄷ = 丙 ");
                b.AppendLine(" ㄹ = 丁 ");
                b.AppendLine();
                sampleStr = b.ToString();
                b.Clear();
                b.AppendLine("[Section1]");
                b.AppendLine("A=6");
                b.AppendLine("B=2");
                b.AppendLine("C=3");
                b.AppendLine("D=4");
                b.AppendLine();
                b.AppendLine("[Section2]");
                b.AppendLine("ㄱ=甲");
                b.AppendLine("ㄴ=乙");
                b.AppendLine("ㄷ=丙");
                b.AppendLine("ㄹ=丁");
                b.AppendLine();
                resultStr = b.ToString();
                WriteTemplate(s, CodeType.IniWrite, $@"IniWrite,{tempFile},Section1,A,6", tempFile, sampleStr, resultStr);

                // Optimization + Compat (AutoCompactIniWriteCommand)
                b.Clear();
                b.AppendLine("[Section1] ");
                b.AppendLine("A=1");
                b.AppendLine(" B = 2");
                b.AppendLine("C = 3 ");
                b.AppendLine(" D = 4 ");
                b.AppendLine();
                b.AppendLine(" [Section2]");
                b.AppendLine("ㄱ=甲");
                b.AppendLine(" ㄴ = 乙");
                b.AppendLine("ㄷ = 丙 ");
                b.AppendLine(" ㄹ = 丁 ");
                b.AppendLine();
                sampleStr = b.ToString();
                b.Clear();
                b.AppendLine("[Section1]");
                b.AppendLine("A=6");
                b.AppendLine("B=2");
                b.AppendLine("C=3");
                b.AppendLine("D=4");
                b.AppendLine();
                b.AppendLine("[Section2]");
                b.AppendLine("ㄱ=甲");
                b.AppendLine("ㄴ=乙");
                b.AppendLine("ㄷ=丙");
                b.AppendLine("ㄹ=丁");
                b.AppendLine("ㅁ=戊");
                b.AppendLine();
                b.AppendLine("[Section3]");
                b.AppendLine("일=1");
                resultStr = b.ToString();
                WriteOptTemplate(s, CodeType.IniWriteOp, new List<string>
                {
                    $@"IniWrite,{tempFile},Section1,A,6",
                    $@"IniWrite,{tempFile},Section2,ㅁ,戊",
                    $@"IniWrite,{tempFile},Section3,일,1",
                }, tempFile, sampleStr, resultStr);
                s.CompatAutoCompactIniWriteCommand = false;
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
        #endregion

        #region IniDelete
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandIni")]
        public void IniDelete()
        {
            EngineState s = EngineTests.CreateEngineState();

            string tempDir = FileHelper.GetTempDir();
            try
            {
                string tempFile = Path.Combine(tempDir, Path.GetRandomFileName());
                string tempFile2 = Path.Combine(tempDir, Path.GetRandomFileName());

                WriteTemplate(s, CodeType.IniDelete, $@"IniDelete,{tempFile},6DoF,Descent", tempFile, string.Empty, string.Empty);

                StringBuilder b = new StringBuilder();
                b.AppendLine("[6DoF]");
                b.AppendLine("Descent=Overload");
                string sampleStr = b.ToString();
                b.Clear();
                b.AppendLine("[6DoF]");
                string resultStr = b.ToString();
                WriteTemplate(s, CodeType.IniDelete, $@"IniDelete,{tempFile},6DoF,Descent", tempFile, sampleStr, resultStr);

                b.Clear();
                b.AppendLine("[6DoF]");
                b.AppendLine("Descent=Overload");
                b.AppendLine();
                b.AppendLine("[Update]");
                b.AppendLine("Roguelike=Sublevel Zero Redux");
                sampleStr = b.ToString();
                b.Clear();
                b.AppendLine("[6DoF]");
                b.AppendLine("Descent=Overload");
                b.AppendLine();
                b.AppendLine("[Update]");
                resultStr = b.ToString();
                WriteTemplate(s, CodeType.IniDelete, $@"IniDelete,{tempFile},Update,Roguelike", tempFile, sampleStr, resultStr);

                b.Clear();
                b.AppendLine("[6DoF]");
                b.AppendLine("// Descent=1");
                b.AppendLine("# Descent=2");
                b.AppendLine("; Descent=Freespace");
                b.AppendLine("Descent=Overload");
                sampleStr = b.ToString();
                b.Clear();
                b.AppendLine("[6DoF]");
                b.AppendLine("// Descent=1");
                b.AppendLine("# Descent=2");
                b.AppendLine("; Descent=Freespace");
                resultStr = b.ToString();
                WriteTemplate(s, CodeType.IniDelete, $@"IniDelete,{tempFile},6DoF,Descent", tempFile, sampleStr, resultStr);

                WriteTemplate(s, CodeType.IniDelete, $@"IniDelete,{tempFile},A", tempFile, string.Empty, null, ErrorCheck.ParserError);

                // Optimization
                b.Clear();
                b.AppendLine("[6DoF]");
                b.AppendLine("Descent=Overload");
                b.AppendLine();
                b.AppendLine("[Update]");
                b.AppendLine("Roguelike=Sublevel Zero Redux");
                sampleStr = b.ToString();
                b.Clear();
                b.AppendLine("[6DoF]");
                b.AppendLine();
                b.AppendLine("[Update]");
                resultStr = b.ToString();
                WriteOptTemplate(s, CodeType.IniDeleteOp, new List<string>
                {
                    $@"IniDelete,{tempFile},6DoF,Descent",
                    $@"IniDelete,{tempFile},Update,Roguelike",
                }, tempFile, sampleStr, resultStr);

                b.Clear();
                b.AppendLine("[6DoF]");
                b.AppendLine("Descent=Overload");
                sampleStr = b.ToString();
                b.Clear();
                b.AppendLine("[6DoF]");
                resultStr = b.ToString();
                WriteOptTemplate(s, null, new List<string>
                {
                    $@"IniDelete,{tempFile},6DoF,Descent",
                    $@"IniDelete,{tempFile2},6DoF,Parallax",
                }, tempFile, sampleStr, resultStr);

                // Compat (AutoCompactIniWriteCommand)
                s.CompatAutoCompactIniWriteCommand = true;
                b.Clear();
                b.AppendLine("[Section1] ");
                b.AppendLine("A=1");
                b.AppendLine(" B = 2");
                b.AppendLine("C = 3 ");
                b.AppendLine(" D = 4 ");
                b.AppendLine();
                b.AppendLine(" [Section2]");
                b.AppendLine("ㄱ=甲");
                b.AppendLine(" ㄴ = 乙");
                b.AppendLine("ㄷ = 丙 ");
                b.AppendLine(" ㄹ = 丁 ");
                b.AppendLine();
                sampleStr = b.ToString();
                b.Clear();
                b.AppendLine("[Section1]");
                b.AppendLine("B=2");
                b.AppendLine("C=3");
                b.AppendLine("D=4");
                b.AppendLine();
                b.AppendLine("[Section2]");
                b.AppendLine("ㄱ=甲");
                b.AppendLine("ㄴ=乙");
                b.AppendLine("ㄷ=丙");
                b.AppendLine("ㄹ=丁");
                b.AppendLine();
                resultStr = b.ToString();
                WriteTemplate(s, CodeType.IniDelete, $@"IniDelete,{tempFile},Section1,A", tempFile, sampleStr, resultStr);

                // Optimization + Compat (AutoCompactIniWriteCommand)
                b.Clear();
                b.AppendLine("[Section1] ");
                b.AppendLine("A=1");
                b.AppendLine(" B = 2");
                b.AppendLine("C = 3 ");
                b.AppendLine(" D = 4 ");
                b.AppendLine();
                b.AppendLine(" [Section2]");
                b.AppendLine("ㄱ=甲");
                b.AppendLine(" ㄴ = 乙");
                b.AppendLine("ㄷ = 丙 ");
                b.AppendLine(" ㄹ = 丁 ");
                b.AppendLine();
                sampleStr = b.ToString();
                b.Clear();
                b.AppendLine("[Section1]");
                b.AppendLine("B=2");
                b.AppendLine("C=3");
                b.AppendLine("D=4");
                b.AppendLine();
                b.AppendLine("[Section2]");
                b.AppendLine("ㄱ=甲");
                b.AppendLine("ㄷ=丙");
                b.AppendLine("ㄹ=丁");
                b.AppendLine();
                resultStr = b.ToString();
                WriteOptTemplate(s, CodeType.IniDeleteOp, new List<string>
                {
                    $@"IniDelete,{tempFile},Section1,A",
                    $@"IniDelete,{tempFile},Section2,ㄴ",
                    $@"IniDelete,{tempFile},Section2,ㅁ",
                }, tempFile, sampleStr, resultStr);
                s.CompatAutoCompactIniWriteCommand = false;
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
        #endregion

        #region IniReadSection
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandIni")]
        public void IniReadSection()
        {
            EngineState s = EngineTests.CreateEngineState();
            string sampleStr = SampleStr();

            string tempDir = FileHelper.GetTempDir();
            try
            {
                string tempFile = Path.Combine(tempDir, Path.GetRandomFileName());
                string tempFile2 = Path.Combine(tempDir, Path.GetRandomFileName());

                const string sec1 = "A|1|B|2|C|3";
                const string sec2 = "X|#$pFour#$p|Y|5|Z|6";
                const string sec3 = "가|7|나|8|다|9";

                ReadTemplate(s, CodeType.IniReadSection, $@"IniReadSection,{tempFile},Sec1,%Dest%", tempFile, sampleStr, sec1);
                ReadTemplate(s, CodeType.IniReadSection, $@"IniReadSection,{tempFile},Sec2,%Dest%", tempFile, sampleStr, sec2);
                ReadTemplate(s, CodeType.IniReadSection, $@"IniReadSection,{tempFile},Sec3,%Dest%", tempFile, sampleStr, sec3);
                ReadTemplate(s, CodeType.IniReadSection, $@"IniReadSection,{tempFile},Sec1,Dest", tempFile, string.Empty, null, ErrorCheck.ParserError);

                // Optimization
                ReadOptTemplate(s, CodeType.IniReadSectionOp, new List<string>
                {
                    $@"IniReadSection,{tempFile},Sec1,%Dest0%",
                    $@"IniReadSection,{tempFile},Sec3,%Dest1%",
                }, tempFile, sampleStr, new string[] { sec1, sec3 });
                ReadOptTemplate(s, null, new List<string>
                {
                    $@"IniReadSection,{tempFile},Sec1,%Dest0%",
                    $@"IniReadSection,{tempFile2},Sec3,%Dest1%",
                }, tempFile, sampleStr, new string[] { sec1 });
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
        #endregion

        #region IniAddSection
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandIni")]
        public void IniAddSection()
        {
            EngineState s = EngineTests.CreateEngineState();

            string tempDir = FileHelper.GetTempDir();
            try
            {
                string tempFile = Path.Combine(tempDir, Path.GetRandomFileName());
                string tempFile2 = Path.Combine(tempDir, Path.GetRandomFileName());

                StringBuilder b = new StringBuilder();
                b.AppendLine("[6DoF]");
                string resultStr = b.ToString();
                WriteTemplate(s, CodeType.IniAddSection, $@"IniAddSection,{tempFile},6DoF", tempFile, string.Empty, resultStr);

                b.Clear();
                b.AppendLine("[6DoF]");
                b.AppendLine("Descent=Overload");
                string sampleStr = b.ToString();
                b.Clear();
                b.AppendLine("[6DoF]");
                b.AppendLine("Descent=Overload");
                b.AppendLine();
                b.AppendLine("[Update]");
                resultStr = b.ToString();
                WriteTemplate(s, CodeType.IniAddSection, $@"IniAddSection,{tempFile},Update", tempFile, sampleStr, resultStr);

                b.Clear();
                b.AppendLine("[6DoF]");
                b.AppendLine("Descent=Overload");
                b.AppendLine();
                sampleStr = b.ToString();
                WriteTemplate(s, CodeType.IniAddSection, $@"IniAddSection,{tempFile},6DoF", tempFile, sampleStr, sampleStr);

                b.Clear();
                b.AppendLine("// [6DoF]");
                sampleStr = b.ToString();
                b.Clear();
                b.AppendLine("// [6DoF]");
                b.AppendLine();
                b.AppendLine("[6DoF]");
                resultStr = b.ToString();
                WriteTemplate(s, CodeType.IniAddSection, $@"IniAddSection,{tempFile},6DoF", tempFile, sampleStr, resultStr);

                WriteTemplate(s, CodeType.IniAddSection, $@"IniAddSection,{tempFile},A,B", tempFile, string.Empty, null, ErrorCheck.ParserError);

                // Optimization
                b.Clear();
                b.AppendLine("[6DoF]");
                b.AppendLine();
                b.AppendLine("[Update]");
                resultStr = b.ToString();
                WriteOptTemplate(s, CodeType.IniAddSectionOp, new List<string>
                {
                    $@"IniAddSection,{tempFile},6DoF",
                    $@"IniAddSection,{tempFile},Update",
                }, tempFile, string.Empty, resultStr);

                b.Clear();
                b.AppendLine("[6DoF]");
                resultStr = b.ToString();
                WriteOptTemplate(s, null, new List<string>
                {
                    $@"IniAddSection,{tempFile},6DoF",
                    $@"IniAddSection,{tempFile2},Update",
                }, tempFile, string.Empty, resultStr);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
        #endregion

        #region IniDeleteSection
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandIni")]
        public void IniDeleteSection()
        {
            EngineState s = EngineTests.CreateEngineState();

            string tempDir = FileHelper.GetTempDir();
            try
            {
                string tempFile = Path.Combine(tempDir, Path.GetRandomFileName());
                string tempFile2 = Path.Combine(tempDir, Path.GetRandomFileName());

                WriteTemplate(s, CodeType.IniDeleteSection, $@"IniDeleteSection,{tempFile},6DoF", tempFile, string.Empty, string.Empty, ErrorCheck.RuntimeError);

                StringBuilder b = new StringBuilder();
                b.AppendLine("[6DoF]");
                b.AppendLine("Descent=Overload");
                b.AppendLine("Engine=Rebirth");
                string sampleStr = b.ToString();
                b.Clear();
                string resultStr = b.ToString();
                WriteTemplate(s, CodeType.IniDeleteSection, $@"IniDeleteSection,{tempFile},6DoF", tempFile, sampleStr, resultStr);

                b.Clear();
                b.AppendLine("[6DoF]");
                b.AppendLine("Descent=Overload");
                b.AppendLine("Engine=Rebirth");
                b.AppendLine();
                b.AppendLine("[Update]");
                b.AppendLine("Roguelike=Sublevel Zero Redux");
                sampleStr = b.ToString();
                b.Clear();
                b.AppendLine("[6DoF]");
                b.AppendLine("Descent=Overload");
                b.AppendLine("Engine=Rebirth");
                b.AppendLine();
                resultStr = b.ToString();
                WriteTemplate(s, CodeType.IniDeleteSection, $@"IniDeleteSection,{tempFile},Update", tempFile, sampleStr, resultStr);

                b.Clear();
                b.AppendLine("[6DoF]");
                b.AppendLine("// Descent=1");
                b.AppendLine("# Descent=2");
                b.AppendLine("; Descent=Freespace");
                b.AppendLine("Descent=Overload");
                b.AppendLine("Engine=Rebirth");
                sampleStr = b.ToString();
                b.Clear();
                resultStr = b.ToString();
                WriteTemplate(s, CodeType.IniDeleteSection, $@"IniDeleteSection,{tempFile},6DoF", tempFile, sampleStr, resultStr);

                WriteTemplate(s, CodeType.IniDeleteSection, $@"IniDeleteSection,{tempFile},A,B", tempFile, string.Empty, null, ErrorCheck.ParserError);

                // Optimization
                b.Clear();
                b.AppendLine("[6DoF]");
                b.AppendLine("Descent=Overload");
                b.AppendLine("Engine=Rebirth");
                b.AppendLine();
                b.AppendLine("[Update]");
                b.AppendLine("Roguelike=Sublevel Zero Redux");
                sampleStr = b.ToString();
                b.Clear();
                resultStr = b.ToString();
                WriteOptTemplate(s, CodeType.IniDeleteSectionOp, new List<string>
                {
                    $@"IniDeleteSection,{tempFile},6DoF",
                    $@"IniDeleteSection,{tempFile},Update",
                }, tempFile, sampleStr, resultStr);

                b.Clear();
                b.AppendLine("[6DoF]");
                b.AppendLine("Descent=Overload");
                b.AppendLine("Engine=Rebirth");
                sampleStr = b.ToString();
                b.Clear();
                resultStr = b.ToString();
                WriteOptTemplate(s, null, new List<string>
                {
                    $@"IniDeleteSection,{tempFile},6DoF",
                    $@"IniDeleteSection,{tempFile2},Update",
                }, tempFile, sampleStr, resultStr);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
        #endregion

        #region IniWriteTextLine
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandIni")]
        public void IniWriteTextLine()
        {
            EngineState s = EngineTests.CreateEngineState();

            string tempDir = FileHelper.GetTempDir();
            try
            {
                string tempFile = Path.Combine(tempDir, Path.GetRandomFileName());
                string tempFile2 = Path.Combine(tempDir, Path.GetRandomFileName());

                StringBuilder b = new StringBuilder();
                b.AppendLine("[6DoF]");
                b.AppendLine("Descent 2");
                string resultStr = b.ToString();
                WriteTemplate(s, CodeType.IniWriteTextLine, $@"IniWriteTextLine,{tempFile},6DoF,Descent 2", tempFile, string.Empty, resultStr);

                b.Clear();
                b.AppendLine("[6DoF]");
                b.AppendLine("Descent=Overload");
                string sampleStr = b.ToString();
                b.Clear();
                b.AppendLine("[6DoF]");
                b.AppendLine("Descent=Overload");
                b.AppendLine();
                b.AppendLine("[Update]");
                b.AppendLine("Sublevel Zero Redux");
                resultStr = b.ToString();
                WriteTemplate(s, CodeType.IniWriteTextLine, $@"IniWriteTextLine,{tempFile},Update,Sublevel Zero Redux", tempFile, sampleStr, resultStr);

                b.Clear();
                b.AppendLine("[6DoF]");
                b.AppendLine("// Descent=1");
                b.AppendLine("# Descent=2");
                b.AppendLine("; Descent=Freespace");
                b.AppendLine("Descent=Overload");
                sampleStr = b.ToString();
                b.Clear();
                b.AppendLine("[6DoF]");
                b.AppendLine("Descent");
                b.AppendLine("// Descent=1");
                b.AppendLine("# Descent=2");
                b.AppendLine("; Descent=Freespace");
                b.AppendLine("Descent=Overload");
                resultStr = b.ToString();
                WriteTemplate(s, CodeType.IniWriteTextLine, $@"IniWriteTextLine,{tempFile},6DoF,Descent", tempFile, sampleStr, resultStr);
                b.Clear();
                b.AppendLine("[6DoF]");
                b.AppendLine("// Descent=1");
                b.AppendLine("# Descent=2");
                b.AppendLine("; Descent=Freespace");
                b.AppendLine("Descent=Overload");
                b.AppendLine("Descent");
                resultStr = b.ToString();
                WriteTemplate(s, CodeType.IniWriteTextLine, $@"IniWriteTextLine,{tempFile},6DoF,Descent,APPEND", tempFile, sampleStr, resultStr);

                WriteTemplate(s, CodeType.IniWriteTextLine, $@"IniWriteTextLine,{tempFile},A", tempFile, string.Empty, null, ErrorCheck.ParserError);

                // Optimization
                b.Clear();
                b.AppendLine("[Update]");
                b.AppendLine("Sublevel Zero Redux");
                b.AppendLine();
                b.AppendLine("[6DoF]");
                b.AppendLine("Descent");
                resultStr = b.ToString();
                WriteOptTemplate(s, CodeType.IniWriteTextLineOp, new List<string>
                {
                    $@"IniWriteTextLine,{tempFile},6DoF,Descent",
                    $@"IniWriteTextLine,{tempFile},Update,Sublevel Zero Redux",
                }, tempFile, string.Empty, resultStr);

                b.Clear();
                b.AppendLine("[6DoF]");
                b.AppendLine("// Descent=1");
                b.AppendLine("# Descent=2");
                b.AppendLine("; Descent=Freespace");
                b.AppendLine("Descent=Overload");
                sampleStr = b.ToString();
                b.Clear();
                b.AppendLine("[6DoF]");
                b.AppendLine("Revival");
                b.AppendLine("// Descent=1");
                b.AppendLine("# Descent=2");
                b.AppendLine("; Descent=Freespace");
                b.AppendLine("Descent=Overload");
                b.AppendLine("Sublevel Zero Redux");
                resultStr = b.ToString();
                WriteOptTemplate(s, null, new List<string>
                {
                    $@"IniWriteTextLine,{tempFile},6DoF,Sublevel Zero Redux,APPEND",
                    $@"IniWriteTextLine,{tempFile},6DoF,Revival",
                }, tempFile, sampleStr, resultStr);

                b.Clear();
                b.AppendLine("[6DoF]");
                b.AppendLine("// Descent=1");
                b.AppendLine("# Descent=2");
                b.AppendLine("; Descent=Freespace");
                b.AppendLine("Descent=Overload");
                sampleStr = b.ToString();
                b.Clear();
                b.AppendLine("[6DoF]");
                b.AppendLine("// Descent=1");
                b.AppendLine("# Descent=2");
                b.AppendLine("; Descent=Freespace");
                b.AppendLine("Descent=Overload");
                b.AppendLine("Sublevel Zero Redux");
                resultStr = b.ToString();
                WriteOptTemplate(s, null, new List<string>
                {
                    $@"IniWriteTextLine,{tempFile},6DoF,Sublevel Zero Redux,APPEND",
                    $@"IniWriteTextLine,{tempFile2},Update,Revival",
                }, tempFile, sampleStr, resultStr);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
        #endregion

        #region IniMerge
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandIni")]
        public void IniMerge()
        {
            EngineState s = EngineTests.CreateEngineState();

            string tempDir = FileHelper.GetTempDir();
            try
            {
                string tempSrcFile = Path.Combine(tempDir, Path.GetRandomFileName());
                string tempDestFile = Path.Combine(tempDir, Path.GetRandomFileName());

                MergeTemplate(s, CodeType.IniMerge, $@"IniMerge,{tempSrcFile},{tempDestFile}",
                    tempSrcFile, string.Empty, tempDestFile, string.Empty, string.Empty);

                StringBuilder b = new StringBuilder();
                b.Clear();
                b.AppendLine("[6DoF]");
                b.AppendLine("Descent=Overload");
                string srcSampleStr = b.ToString();
                b.Clear();
                b.AppendLine("[Update]");
                b.AppendLine("Roguelike=Sublevel Zero Redux");
                string destSampleStr = b.ToString();
                b.Clear();
                b.AppendLine("[Update]");
                b.AppendLine("Roguelike=Sublevel Zero Redux");
                b.AppendLine();
                b.AppendLine("[6DoF]");
                b.AppendLine("Descent=Overload");
                string resultStr = b.ToString();
                MergeTemplate(s, CodeType.IniMerge, $@"IniMerge,{tempSrcFile},{tempDestFile}",
                    tempSrcFile, srcSampleStr, tempDestFile, destSampleStr, resultStr);

                b.Clear();
                b.AppendLine("[Sec1]");
                b.AppendLine("A=1");
                b.AppendLine("B=2");
                b.AppendLine();
                b.AppendLine("[Sec2]");
                b.AppendLine("// Descent=1");
                b.AppendLine("D=1");
                srcSampleStr = b.ToString();
                b.Clear();
                b.AppendLine("[Sec2]");
                b.AppendLine("# Descent=2");
                b.AppendLine("D=4");
                b.AppendLine("E=5");
                destSampleStr = b.ToString();
                b.Clear();
                b.AppendLine("[Sec2]");
                b.AppendLine("# Descent=2");
                b.AppendLine("D=1");
                b.AppendLine("E=5");
                b.AppendLine();
                b.AppendLine("[Sec1]");
                b.AppendLine("A=1");
                b.AppendLine("B=2");
                resultStr = b.ToString();
                MergeTemplate(s, CodeType.IniMerge, $@"IniMerge,{tempSrcFile},{tempDestFile}",
                    tempSrcFile, srcSampleStr, tempDestFile, destSampleStr, resultStr);

                WriteTemplate(s, CodeType.IniMerge, $@"IniMerge,{tempSrcFile}", tempSrcFile, string.Empty, null, ErrorCheck.ParserError);

                // Compat (AutoCompactIniWriteCommand)
                s.CompatAutoCompactIniWriteCommand = true;
                b.Clear();
                b.AppendLine("[Section1] ");
                b.AppendLine("A=6");
                b.AppendLine(" B = 7");
                b.AppendLine("C = 8 ");
                b.AppendLine(" D = 9 ");
                b.AppendLine();
                b.AppendLine(" [Section3]");
                b.AppendLine("일=一");
                b.AppendLine(" 이 = 二");
                b.AppendLine("삼 = 三 ");
                b.AppendLine(" 사 = 四 ");
                b.AppendLine();
                srcSampleStr = b.ToString();
                b.Clear();
                b.AppendLine("  [Section1]");
                b.AppendLine("A=1");
                b.AppendLine(" B = 2");
                b.AppendLine("C = 3 ");
                b.AppendLine(" D = 4 ");
                b.AppendLine();
                b.AppendLine(" [Section2]  ");
                b.AppendLine("ㄱ=甲");
                b.AppendLine(" ㄴ = 乙");
                b.AppendLine("ㄷ = 丙 ");
                b.AppendLine(" ㄹ = 丁 ");
                b.AppendLine();
                destSampleStr = b.ToString();
                b.Clear();
                b.AppendLine("[Section1]");
                b.AppendLine("A=6");
                b.AppendLine("B=7");
                b.AppendLine("C=8");
                b.AppendLine("D=9");
                b.AppendLine();
                b.AppendLine("[Section2]");
                b.AppendLine("ㄱ=甲");
                b.AppendLine("ㄴ=乙");
                b.AppendLine("ㄷ=丙");
                b.AppendLine("ㄹ=丁");
                b.AppendLine();
                b.AppendLine("[Section3]");
                b.AppendLine("일=一");
                b.AppendLine("이=二");
                b.AppendLine("삼=三");
                b.AppendLine("사=四");
                resultStr = b.ToString();
                MergeTemplate(s, CodeType.IniMerge, $@"IniMerge,{tempSrcFile},{tempDestFile}", tempSrcFile, srcSampleStr, tempDestFile, destSampleStr, resultStr);
                s.CompatAutoCompactIniWriteCommand = false;
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
        #endregion

        #region IniCompact
        [TestMethod]
        public void IniCompact()
        {
            EngineState s = EngineTests.CreateEngineState();

            string srcFile = Path.Combine(EngineTests.TestBench, "CommandIni", "BeforeCompact.ini");
            string compFile = Path.Combine(EngineTests.TestBench, "CommandIni", "AfterCompact.ini");

            string srcStr;
            using (StreamReader sr = new StreamReader(srcFile, Encoding.UTF8, false))
            {
                srcStr = sr.ReadToEnd();
            }
            string compStr;
            using (StreamReader sr = new StreamReader(compFile, Encoding.UTF8, false))
            {
                compStr = sr.ReadToEnd();
            }

            string destFile = FileHelper.GetTempFile(".ini");
            try
            {
                WriteTemplate(s, CodeType.IniCompact, $@"IniCompact,{destFile}", destFile, srcStr, compStr, ErrorCheck.Success);
                WriteTemplate(s, CodeType.IniCompact, $@"IniCompact,{destFile},Error", destFile, srcStr, compStr, ErrorCheck.ParserError);
            }
            finally
            {
                if (File.Exists(destFile))
                    File.Delete(destFile);
            }
        }
        #endregion

        #region Template
        private static void ReadTemplate(
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

                s.Variables.DeleteKey(VarsType.Local, "Dest");
                EngineTests.Eval(s, rawCode, type, check);
                if (check == ErrorCheck.Success || check == ErrorCheck.Warning)
                {
                    Assert.IsTrue(s.Variables.ContainsKey("Dest"));
                    Assert.IsTrue(s.Variables["Dest"].Equals(compStr, StringComparison.Ordinal));
                }
            }
            finally
            {
                if (File.Exists(testFile))
                    File.Delete(testFile);
            }
        }

        private static void ReadOptTemplate(
            EngineState s, CodeType? opType,
            List<string> rawCodes, string testFile, string sampleStr, string[] compStrs,
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

                for (int i = 0; i < compStrs.Length; i++)
                    s.Variables.DeleteKey(VarsType.Local, $"Dest{i}");
                EngineTests.EvalOptLines(s, opType, rawCodes, check);
                if (check == ErrorCheck.Success || check == ErrorCheck.Warning)
                {
                    for (int i = 0; i < compStrs.Length; i++)
                    {
                        string compStr = compStrs[i];
                        string destKey = $"Dest{i}";
                        Assert.IsTrue(s.Variables.ContainsKey(destKey));
                        Assert.IsTrue(s.Variables[destKey].Equals(compStr, StringComparison.Ordinal));
                    }
                }
            }
            finally
            {
                if (File.Exists(testFile))
                    File.Delete(testFile);
            }
        }

        private static void WriteTemplate(
            EngineState s, CodeType type,
            string rawCode, string testFile, string sampleStr, string expectStr,
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
                    string resultStr;
                    using (StreamReader r = new StreamReader(testFile, Encoding.UTF8))
                    {
                        resultStr = r.ReadToEnd();
                    }

                    Assert.IsTrue(resultStr.Equals(expectStr, StringComparison.Ordinal));
                }
            }
            finally
            {
                if (File.Exists(testFile))
                    File.Delete(testFile);
            }
        }

        private static void WriteOptTemplate(
            EngineState s, CodeType? opType,
            List<string> rawCodes, string testFile, string sampleStr, string expectStr,
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
                    string resultStr;
                    using (StreamReader r = new StreamReader(testFile, Encoding.UTF8))
                    {
                        resultStr = r.ReadToEnd();
                    }

                    Assert.IsTrue(resultStr.Equals(expectStr, StringComparison.Ordinal));
                }
            }
            finally
            {
                if (File.Exists(testFile))
                    File.Delete(testFile);
            }
        }

        private static void MergeTemplate(
            EngineState s, CodeType type,
            string rawCode, string srcFile, string srcSampleStr, string destFile, string destSampleStr, string expectStr,
            ErrorCheck check = ErrorCheck.Success)
        {
            if (File.Exists(srcFile))
                File.Delete(srcFile);
            if (File.Exists(destFile))
                File.Delete(destFile);
            try
            {
                File.Create(destFile).Close();

                EncodingHelper.WriteTextBom(srcFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(srcFile, true, Encoding.UTF8))
                {
                    w.Write(srcSampleStr);
                }
                EncodingHelper.WriteTextBom(destFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(destFile, true, Encoding.UTF8))
                {
                    w.Write(destSampleStr);
                }

                EngineTests.Eval(s, rawCode, type, check);
                if (check == ErrorCheck.Success || check == ErrorCheck.Warning)
                {
                    string dest;
                    using (StreamReader r = new StreamReader(destFile, Encoding.UTF8))
                    {
                        dest = r.ReadToEnd();
                    }

                    Assert.IsTrue(dest.Equals(expectStr, StringComparison.Ordinal));
                }
            }
            finally
            {
                if (File.Exists(srcFile))
                    File.Delete(srcFile);
                if (File.Exists(destFile))
                    File.Delete(destFile);
            }
        }
        #endregion
    }
}
