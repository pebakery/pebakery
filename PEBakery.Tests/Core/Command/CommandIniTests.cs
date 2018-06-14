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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using PEBakery.Core;
using PEBakery.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local

namespace PEBakery.Tests.Core.Command
{
    [TestClass]
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
            "X=4", // 7
            "Y=5", // 8
            "Z=6", // 9
            string.Empty, // 10
            "; Unicode", // 11
            "[Sec3]", // 12
            "가=7", // 13
            "# Sharp", // 14
            "나=8", // 15
            "다=9", // 16
        };

        private static string SampleStr()
        {
            StringBuilder b = new StringBuilder();
            foreach (string s in SampleLines)
                b.AppendLine(s);
            return b.ToString();
        }

        private static string SampleStrDeleted(int[] deleted)
        {
            StringBuilder b = new StringBuilder();
            for (int i = 0; i < SampleLines.Length; i++)
            {
                if (!deleted.Contains(i))
                    b.AppendLine(SampleLines[i]);
            }
            return b.ToString();
        }
        #endregion

        #region IniRead
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandIni")]
        public void IniRead()
        { 
            EngineState s = EngineTests.CreateEngineState();
            string sampleStr = SampleStr();

            string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                Directory.CreateDirectory(tempDir);
                string tempFile = Path.Combine(tempDir, Path.GetRandomFileName());
                string tempFile2 = Path.Combine(tempDir, Path.GetRandomFileName());

                ReadTemplate(s, CodeType.IniRead, $@"IniRead,{tempFile},Sec1,A,%Dest%", tempFile, sampleStr, "1");
                ReadTemplate(s, CodeType.IniRead, $@"IniRead,{tempFile},Sec1,B,%Dest%", tempFile, sampleStr, "2");
                ReadTemplate(s, CodeType.IniRead, $@"IniRead,{tempFile},Sec2,Z,%Dest%", tempFile, sampleStr, "6");
                ReadTemplate(s, CodeType.IniRead, $@"IniRead,{tempFile},Sec3,나,%Dest%", tempFile, sampleStr, "8");
                ReadTemplate(s, CodeType.IniRead, $@"IniRead,{tempFile},Sec2,無,%Dest%", tempFile, sampleStr, string.Empty);
                ReadTemplate(s, CodeType.IniRead, $@"IniRead,{tempFile},Sec2,Z", tempFile, string.Empty, null, ErrorCheck.ParserError);

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
        [TestCategory("Command")]
        [TestCategory("CommandIni")]
        public void IniWrite()
        {
            EngineState s = EngineTests.CreateEngineState();

            string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                Directory.CreateDirectory(tempDir);
                string tempFile = Path.Combine(tempDir, Path.GetRandomFileName());
                string tempFile2 = Path.Combine(tempDir, Path.GetRandomFileName());

                {
                    StringBuilder b = new StringBuilder();
                    b.AppendLine("[6DoF]");
                    b.AppendLine("Descent=Overload");
                    string resultStr = b.ToString();

                    WriteTemplate(s, CodeType.IniWrite, $@"IniWrite,{tempFile},6DoF,Descent,Overload", tempFile, string.Empty, resultStr);
                }
                {
                    StringBuilder b = new StringBuilder();
                    b.AppendLine("[6DoF]");
                    b.AppendLine("Descent=Overload");
                    string sampleStr = b.ToString();

                    b = new StringBuilder();
                    b.AppendLine("[6DoF]");
                    b.AppendLine("Descent=Overload");
                    b.AppendLine();
                    b.AppendLine("[Update]");
                    b.AppendLine("Roguelike=Sublevel Zero Redux");
                    string resultStr = b.ToString();

                    WriteTemplate(s, CodeType.IniWrite, $@"IniWrite,{tempFile},Update,Roguelike,Sublevel Zero Redux", tempFile, sampleStr, resultStr);
                }
                {
                    StringBuilder b = new StringBuilder();
                    b.AppendLine("[6DoF]");
                    b.AppendLine("Descent=Overload");
                    string sampleStr = b.ToString();

                    b = new StringBuilder();
                    b.AppendLine("[6DoF]");
                    b.AppendLine("Descent=Sublevel Zero Redux");
                    b.AppendLine();
                    string resultStr = b.ToString();

                    WriteTemplate(s, CodeType.IniWrite, $@"IniWrite,{tempFile},6DoF,Descent,Sublevel Zero Redux", tempFile, sampleStr, resultStr);
                }
                {
                    StringBuilder b = new StringBuilder();
                    b.AppendLine("[6DoF]");
                    b.AppendLine("// Descent=1");
                    b.AppendLine("# Descent=2");
                    b.AppendLine("; Descent=Freespace");
                    b.AppendLine("Descent=Overload");
                    string sampleStr = b.ToString();

                    b = new StringBuilder();
                    b.AppendLine("[6DoF]");
                    b.AppendLine("// Descent=1");
                    b.AppendLine("# Descent=2");
                    b.AppendLine("; Descent=Freespace");
                    b.AppendLine("Descent=Sublevel Zero Redux");
                    b.AppendLine();
                    string resultStr = b.ToString();

                    WriteTemplate(s, CodeType.IniWrite, $@"IniWrite,{tempFile},6DoF,Descent,Sublevel Zero Redux", tempFile, sampleStr, resultStr);
                }
                WriteTemplate(s, CodeType.IniWrite, $@"IniWRite,{tempFile},A,B", tempFile, string.Empty, null, ErrorCheck.ParserError);

                // Optimization
                {
                    StringBuilder b = new StringBuilder();
                    b.AppendLine("[6DoF]");
                    b.AppendLine("Descent=Overload");
                    b.AppendLine();
                    b.AppendLine("[Update]");
                    b.AppendLine("Roguelike=Sublevel Zero Redux");
                    string resultStr = b.ToString();

                    WriteOptTemplate(s, CodeType.IniWriteOp, new List<string>
                    {
                        $@"IniWrite,{tempFile},6DoF,Descent,Overload",
                        $@"IniWrite,{tempFile},Update,Roguelike,Sublevel Zero Redux",
                    }, tempFile, string.Empty, resultStr);
                }
                {
                    StringBuilder b = new StringBuilder();
                    b.AppendLine("[6DoF]");
                    b.AppendLine("// Descent=1");
                    b.AppendLine("# Descent=2");
                    b.AppendLine("; Descent=Freespace");
                    b.AppendLine("Descent=Overload");
                    string sampleStr = b.ToString();

                    b = new StringBuilder();
                    b.AppendLine("[6DoF]");
                    b.AppendLine("// Descent=1");
                    b.AppendLine("# Descent=2");
                    b.AppendLine("; Descent=Freespace");
                    b.AppendLine("Descent=Sublevel Zero Redux");
                    b.AppendLine();
                    b.AppendLine();
                    b.AppendLine("[Update]");
                    b.AppendLine("Parallax=Revival");
                    string resultStr = b.ToString();

                    WriteOptTemplate(s, CodeType.IniWriteOp, new List<string>
                    {
                        $@"IniWrite,{tempFile},6DoF,Descent,Sublevel Zero Redux",
                        $@"IniWrite,{tempFile},Update,Parallax,Revival",
                    }, tempFile, sampleStr, resultStr);
                }
                {
                    StringBuilder b = new StringBuilder();
                    b.AppendLine("[6DoF]");
                    b.AppendLine("Descent=Overload");
                    string sampleStr = b.ToString();

                    b = new StringBuilder();
                    b.AppendLine("[6DoF]");
                    b.AppendLine("Descent=Sublevel Zero Redux");
                    b.AppendLine();
                    string resultStr = b.ToString();

                    WriteOptTemplate(s, null, new List<string>
                    {
                        $@"IniWrite,{tempFile},6DoF,Descent,Sublevel Zero Redux",
                        $@"IniWrite,{tempFile2},6DoF,Parallax,Revival",
                    }, tempFile, sampleStr, resultStr);
                }
            }
            finally
            {
                Directory.Delete(tempDir, true);
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

                FileHelper.WriteTextBom(testFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(testFile, true, Encoding.UTF8))
                {
                    w.Write(sampleStr);
                }

                s.Variables.Delete(VarsType.Local, "Dest");
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
                FileHelper.WriteTextBom(testFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(testFile, true, Encoding.UTF8))
                {
                    w.Write(sampleStr);
                }

                for (int i = 0; i < compStrs.Length; i++)
                    s.Variables.Delete(VarsType.Local, $"Dest{i}");
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
            string rawCode, string testFile, string sampleStr, string compStr,
            ErrorCheck check = ErrorCheck.Success)
        {
            if (File.Exists(testFile))
                File.Delete(testFile);
            try
            {
                File.Create(testFile).Close();

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

                    Assert.IsTrue(dest.Equals(compStr, StringComparison.Ordinal));
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
            List<string> rawCodes, string testFile, string sampleStr, string compStr,
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
