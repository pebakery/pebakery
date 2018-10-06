/*
    Copyright (C) 2017-2018 Hajin Jang
 
    MIT License

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.
*/

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PEBakery.Ini.Tests
{
    [TestClass]
    public class IniLibTests
    {
        #region ReadKey
        [TestCategory("PEBakery.Ini")]
        [TestMethod]
        public void ReadKey()
        {
            ReadKey_1();
            ReadKey_2();
            ReadKey_3();
        }

        public void ReadKey_1()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                Assert.IsNull(IniReadWriter.ReadKey(tempFile, "Section", "Key"));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        public void ReadKey_2()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section]");
                    w.WriteLine("Key=Value");
                    w.Close();
                }

                Assert.IsTrue(IniReadWriter.ReadKey(tempFile, "Section", "Key").Equals("Value", StringComparison.Ordinal));
                Assert.IsTrue(IniReadWriter.ReadKey(tempFile, "Section", "key").Equals("Value", StringComparison.Ordinal));
                Assert.IsTrue(IniReadWriter.ReadKey(tempFile, "section", "Key").Equals("Value", StringComparison.Ordinal));
                Assert.IsTrue(IniReadWriter.ReadKey(tempFile, "section", "key").Equals("Value", StringComparison.Ordinal));
                Assert.IsFalse(IniReadWriter.ReadKey(tempFile, "Section", "Key").Equals("value", StringComparison.Ordinal));

            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        public void ReadKey_3()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section1]");
                    w.WriteLine("1=A");
                    w.WriteLine("2=B");
                    w.WriteLine("3=C");
                    w.WriteLine();
                    w.WriteLine("[Section2]");
                    w.WriteLine("4=D");
                    w.WriteLine("5=E");
                    w.WriteLine("[Section3]");
                    w.WriteLine("6=F");
                    w.WriteLine("7=G");
                    w.WriteLine("8=H");
                    w.Close();
                }

                Assert.IsTrue(IniReadWriter.ReadKey(tempFile, "Section1", "1").Equals("A", StringComparison.Ordinal));
                Assert.IsTrue(IniReadWriter.ReadKey(tempFile, "Section1", "2").Equals("B", StringComparison.Ordinal));
                Assert.IsTrue(IniReadWriter.ReadKey(tempFile, "section1", "3").Equals("C", StringComparison.Ordinal));
                Assert.IsTrue(IniReadWriter.ReadKey(tempFile, "Section2", "4").Equals("D", StringComparison.Ordinal));
                Assert.IsTrue(IniReadWriter.ReadKey(tempFile, "Section2", "5").Equals("E", StringComparison.Ordinal));
                Assert.IsTrue(IniReadWriter.ReadKey(tempFile, "section3", "6").Equals("F", StringComparison.Ordinal));
                Assert.IsTrue(IniReadWriter.ReadKey(tempFile, "section3", "7").Equals("G", StringComparison.Ordinal));
                Assert.IsTrue(IniReadWriter.ReadKey(tempFile, "section3", "8").Equals("H", StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
        #endregion

        #region ReadKeys
        [TestCategory("PEBakery.Ini")]
        [TestMethod]
        public void ReadKeys()
        {
            ReadKeys_1();
        }

        public void ReadKeys_1()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section1]");
                    w.WriteLine("1=A");
                    w.WriteLine("2=B");
                    w.WriteLine("3=C");
                    w.WriteLine();
                    w.WriteLine("[Section2]");
                    w.WriteLine("4=D");
                    w.WriteLine("5=E");
                    w.WriteLine("[Section3]");
                    w.WriteLine("6=F");
                    w.WriteLine("7=G");
                    w.WriteLine("8=H");
                    w.Close();
                }

                IniKey[] keys = new IniKey[10];
                keys[0] = new IniKey("Section1", "3");
                keys[1] = new IniKey("Section3", "8");
                keys[2] = new IniKey("Section2", "5");
                keys[3] = new IniKey("Section3", "6");
                keys[4] = new IniKey("Section1", "4");
                keys[5] = new IniKey("Section2", "1");
                keys[6] = new IniKey("Section1", "2");
                keys[7] = new IniKey("Section3", "7");
                keys[8] = new IniKey("Section1", "1");
                keys[9] = new IniKey("Section2", "4");

                keys = IniReadWriter.ReadKeys(tempFile, keys);
                Assert.IsTrue(keys[0].Value.Equals("C", StringComparison.Ordinal));
                Assert.IsTrue(keys[1].Value.Equals("H", StringComparison.Ordinal));
                Assert.IsTrue(keys[2].Value.Equals("E", StringComparison.Ordinal));
                Assert.IsTrue(keys[3].Value.Equals("F", StringComparison.Ordinal));
                Assert.IsNull(keys[4].Value);
                Assert.IsNull(keys[5].Value);
                Assert.IsTrue(keys[6].Value.Equals("B", StringComparison.Ordinal));
                Assert.IsTrue(keys[7].Value.Equals("G", StringComparison.Ordinal));
                Assert.IsTrue(keys[8].Value.Equals("A", StringComparison.Ordinal));
                Assert.IsTrue(keys[9].Value.Equals("D", StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
        #endregion

        #region WriteKey
        [TestCategory("PEBakery.Ini")]
        [TestMethod]
        public void WriteKey()
        {
            WriteKey_1();
            WriteKey_2();
            WriteKey_3();
            WriteKey_4();
            WriteKey_5();
            WriteKey_6();
        }

        public void WriteKey_1()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                Assert.IsTrue(IniReadWriter.WriteKey(tempFile, "Section", "Key", "Value"));

                string read;
                using (StreamReader r = new StreamReader(tempFile))
                {
                    read = r.ReadToEnd();
                }

                StringBuilder b = new StringBuilder();
                b.AppendLine("[Section]");
                b.AppendLine("Key=Value");
                string comp = b.ToString();

                Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        public void WriteKey_2()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section]");
                    w.WriteLine("Key=A");
                    w.Close();
                }

                Assert.IsTrue(IniReadWriter.WriteKey(tempFile, "Section", "Key", "B"));

                string read;
                Encoding encoding = TestHelper.DetectTextEncoding(tempFile);
                using (StreamReader r = new StreamReader(tempFile, encoding))
                {
                    read = r.ReadToEnd();
                }

                StringBuilder b = new StringBuilder();
                b.AppendLine("[Section]");
                b.AppendLine("Key=B");
                b.AppendLine();
                string comp = b.ToString();

                Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        public void WriteKey_3()
        { // Found while testing EncodedFile.EncodeFile()
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section]");
                    w.WriteLine("Sect2");
                    w.Close();
                }

                Assert.IsTrue(IniReadWriter.WriteKey(tempFile, "Section2", "Key", "B"));

                string read;
                Encoding encoding = TestHelper.DetectTextEncoding(tempFile);
                using (StreamReader r = new StreamReader(tempFile, encoding))
                {
                    read = r.ReadToEnd();
                }

                StringBuilder b = new StringBuilder();
                b.AppendLine("[Section]");
                b.AppendLine("Sect2");
                b.AppendLine();
                b.AppendLine("[Section2]");
                b.AppendLine("Key=B");
                string comp = b.ToString();

                Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        public void WriteKey_4()
        { // Found while testing EncodedFile.EncodeFile()
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section]");
                    w.WriteLine("Section2");
                    w.Close();
                }

                Assert.IsTrue(IniReadWriter.WriteKey(tempFile, "Section2", "Key", "B"));

                string read;
                Encoding encoding = TestHelper.DetectTextEncoding(tempFile);
                using (StreamReader r = new StreamReader(tempFile, encoding))
                {
                    read = r.ReadToEnd();
                }

                StringBuilder b = new StringBuilder();
                b.AppendLine("[Section]");
                b.AppendLine("Section2");
                b.AppendLine();
                b.AppendLine("[Section2]");
                b.AppendLine("Key=B");
                string comp = b.ToString();

                Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        public void WriteKey_5()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                Assert.IsTrue(IniReadWriter.WriteKey(tempFile, "Section", "Key", "Value"));

                string read;
                using (StreamReader r = new StreamReader(tempFile))
                {
                    read = r.ReadToEnd();
                }

                StringBuilder b = new StringBuilder();
                b.AppendLine("[Section]");
                b.AppendLine("Key=Value");
                string comp = b.ToString();

                Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        public void WriteKey_6()
        { // https://github.com/pebakery/pebakery/issues/57
            string tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Interface]");
                    w.WriteLine("pTextBox1=\"Plugin Title and Script Name: \",1,0,93,1,158,21,SMplayer");
                    w.WriteLine();
                    w.WriteLine();
                    w.WriteLine("FileBox2=,1,13,40,251,403,30,dir,\"__Output folder\"");
                    w.WriteLine("FileBox3=,1,13,40,311,403,30,file,\"__Select .7z file\"");
                    w.WriteLine();
                    w.WriteLine("Button1=Go!,1,8,343,71,58,25,process,0,True");
                    w.WriteLine("[Process]");
                    w.WriteLine("// Hello World");
                    w.Close();
                }

                Assert.IsTrue(IniReadWriter.WriteKey(tempFile, "Interface", "FileBox2", "Overwrite2"));
                Assert.IsTrue(IniReadWriter.WriteKey(tempFile, "Interface", "FileBox3", "Overwrite3"));

                string read;
                Encoding encoding = TestHelper.DetectTextEncoding(tempFile);
                using (StreamReader r = new StreamReader(tempFile, encoding))
                {
                    read = r.ReadToEnd();
                }

                StringBuilder b = new StringBuilder();
                b.AppendLine("[Interface]");
                b.AppendLine("pTextBox1=\"Plugin Title and Script Name: \",1,0,93,1,158,21,SMplayer");
                b.AppendLine();
                b.AppendLine();
                b.AppendLine("FileBox2=Overwrite2");
                b.AppendLine("FileBox3=Overwrite3");
                b.AppendLine();
                b.AppendLine("Button1=Go!,1,8,343,71,58,25,process,0,True");
                b.AppendLine();
                b.AppendLine("[Process]");
                b.AppendLine("// Hello World");
                string comp = b.ToString();

                Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
        #endregion

        #region WriteKeys
        [TestCategory("PEBakery.Ini")]
        [TestMethod]
        public void WriteKeys()
        {
            WriteKeys_1();
            WriteKeys_2();
        }

        public void WriteKeys_1()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);

                IniKey[] keys = new IniKey[3];
                keys[0] = new IniKey("Section2", "20", "English");
                keys[1] = new IniKey("Section1", "10", "한국어");
                keys[2] = new IniKey("Section3", "30", "Français");

                Assert.IsTrue(IniReadWriter.WriteKeys(tempFile, keys));

                string read;
                Encoding encoding = TestHelper.DetectTextEncoding(tempFile);
                using (StreamReader r = new StreamReader(tempFile, encoding))
                {
                    read = r.ReadToEnd();
                }

                StringBuilder b = new StringBuilder();
                b.AppendLine("[Section2]");
                b.AppendLine("20=English");
                b.AppendLine();
                b.AppendLine("[Section1]");
                b.AppendLine("10=한국어");
                b.AppendLine();
                b.AppendLine("[Section3]");
                b.AppendLine("30=Français");
                string comp = b.ToString();

                Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        public void WriteKeys_2()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section1]");
                    w.WriteLine("00=A");
                    w.WriteLine("01=B");
                    w.WriteLine("02=C");
                    w.WriteLine();
                    w.WriteLine("[Section2]");
                    w.WriteLine("10=한");
                    w.WriteLine("11=국");
                    w.WriteLine("[Section3]");
                    w.WriteLine("20=韓");
                    w.WriteLine("21=國");
                    w.Close();
                }

                IniKey[] keys = new IniKey[5];
                keys[0] = new IniKey("Section1", "03", "D");
                keys[1] = new IniKey("Section3", "22", "語");
                keys[2] = new IniKey("Section2", "12", "어");
                keys[3] = new IniKey("Section1", "04", "Unicode");
                keys[4] = new IniKey("Section2", "13", "한글");

                Assert.IsTrue(IniReadWriter.WriteKeys(tempFile, keys));

                string read;
                Encoding encoding = TestHelper.DetectTextEncoding(tempFile);
                using (StreamReader r = new StreamReader(tempFile, encoding))
                {
                    read = r.ReadToEnd();
                }

                StringBuilder b = new StringBuilder();
                b.AppendLine("[Section1]");
                b.AppendLine("00=A");
                b.AppendLine("01=B");
                b.AppendLine("02=C");
                b.AppendLine("03=D");
                b.AppendLine("04=Unicode");
                b.AppendLine();
                b.AppendLine("[Section2]");
                b.AppendLine("10=한");
                b.AppendLine("11=국");
                b.AppendLine("12=어");
                b.AppendLine("13=한글");
                b.AppendLine();
                b.AppendLine("[Section3]");
                b.AppendLine("20=韓");
                b.AppendLine("21=國");
                b.AppendLine("22=語");
                b.AppendLine();
                string comp = b.ToString();

                Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
        #endregion

        #region WriteRawLine
        [TestCategory("PEBakery.Ini")]
        [TestMethod]
        public void WriteRawLine()
        {
            WriteRawLine_1();
            WriteRawLine_2();
            WriteRawLine_3();
            WriteRawLine_4();
            WriteRawLine_5();
        }

        public void WriteRawLine_1()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);

                Assert.IsTrue(IniReadWriter.WriteRawLine(tempFile, "Section", "RawLine"));

                string read;
                Encoding encoding = TestHelper.DetectTextEncoding(tempFile);
                using (StreamReader r = new StreamReader(tempFile, encoding))
                {
                    read = r.ReadToEnd();
                }

                StringBuilder b = new StringBuilder();
                b.AppendLine("[Section]");
                b.AppendLine("RawLine");
                string comp = b.ToString();

                Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        public void WriteRawLine_2()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section]");
                    w.WriteLine("1=A");
                    w.Close();
                }

                Assert.IsTrue(IniReadWriter.WriteRawLine(tempFile, "Section", "LineAppend", true));

                string read;
                Encoding encoding = TestHelper.DetectTextEncoding(tempFile);
                using (StreamReader r = new StreamReader(tempFile, encoding))
                {
                    read = r.ReadToEnd();
                }

                StringBuilder b = new StringBuilder();
                b.AppendLine("[Section]");
                b.AppendLine("1=A");
                b.AppendLine("LineAppend");
                string comp = b.ToString();

                Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        public void WriteRawLine_3()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section]");
                    w.WriteLine("1=A");
                    w.Close();
                }

                Assert.IsTrue(IniReadWriter.WriteRawLine(tempFile, "Section", "LinePrepend", false));

                string read;
                Encoding encoding = TestHelper.DetectTextEncoding(tempFile);
                using (StreamReader r = new StreamReader(tempFile, encoding))
                {
                    read = r.ReadToEnd();
                }

                StringBuilder b = new StringBuilder();
                b.AppendLine("[Section]");
                b.AppendLine("LinePrepend");
                b.AppendLine("1=A");
                string comp = b.ToString();

                Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        public void WriteRawLine_4()
        { // Found while testing EncodedFile.EncodeFile()
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section]");
                    w.WriteLine("Sect2");
                    w.Close();
                }

                Assert.IsTrue(IniReadWriter.WriteRawLine(tempFile, "Section2", "Key"));

                string read;
                Encoding encoding = TestHelper.DetectTextEncoding(tempFile);
                using (StreamReader r = new StreamReader(tempFile, encoding))
                {
                    read = r.ReadToEnd();
                }

                StringBuilder b = new StringBuilder();
                b.AppendLine("[Section]");
                b.AppendLine("Sect2");
                b.AppendLine();
                b.AppendLine("[Section2]");
                b.AppendLine("Key");
                string comp = b.ToString();

                Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        public void WriteRawLine_5()
        { // Found while testing EncodedFile.EncodeFile()
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section]");
                    w.WriteLine("Section2");
                    w.Close();
                }

                Assert.IsTrue(IniReadWriter.WriteRawLine(tempFile, "Section2", "Key"));

                string read;
                Encoding encoding = TestHelper.DetectTextEncoding(tempFile);
                using (StreamReader r = new StreamReader(tempFile, encoding))
                {
                    read = r.ReadToEnd();
                }

                StringBuilder b = new StringBuilder();
                b.AppendLine("[Section]");
                b.AppendLine("Section2");
                b.AppendLine();
                b.AppendLine("[Section2]");
                b.AppendLine("Key");
                string comp = b.ToString();

                Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
        #endregion

        #region WriteRawLines
        [TestCategory("PEBakery.Ini")]
        [TestMethod]
        public void WriteRawLines()
        {
            WriteRawLines_1();
            WriteRawLines_2();
        }

        public void WriteRawLines_1()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section1]");
                    w.WriteLine("10=한국어");
                    w.WriteLine("11=中文");
                    w.WriteLine("12=にほんご");
                    w.WriteLine();
                    w.WriteLine("[Section2]");
                    w.WriteLine("20=English");
                    w.WriteLine("[Section3]");
                    w.WriteLine("30=Français");
                    w.Close();
                }

                IniKey[] keys = new IniKey[5];
                keys[0] = new IniKey("Section2", "영어");
                keys[1] = new IniKey("Section1", "한중일 (CJK)");
                keys[2] = new IniKey("Section3", "프랑스어");
                keys[3] = new IniKey("Section4", "עברית");
                keys[4] = new IniKey("Section4", "العربية");

                Assert.IsTrue(IniReadWriter.WriteRawLines(tempFile, keys, false));

                string read;
                Encoding encoding = TestHelper.DetectTextEncoding(tempFile);
                using (StreamReader r = new StreamReader(tempFile, encoding))
                {
                    read = r.ReadToEnd();
                }

                StringBuilder b = new StringBuilder();
                b.AppendLine("[Section1]");
                b.AppendLine("한중일 (CJK)");
                b.AppendLine("10=한국어");
                b.AppendLine("11=中文");
                b.AppendLine("12=にほんご");
                b.AppendLine();
                b.AppendLine("[Section2]");
                b.AppendLine("영어");
                b.AppendLine("20=English");
                b.AppendLine("[Section3]");
                b.AppendLine("프랑스어");
                b.AppendLine("30=Français");
                b.AppendLine();
                b.AppendLine("[Section4]");
                b.AppendLine("עברית");
                b.AppendLine("العربية");
                string comp = b.ToString();

                Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        public void WriteRawLines_2()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section1]");
                    w.WriteLine("10=한국어");
                    w.WriteLine("11=中文");
                    w.WriteLine("12=にほんご");
                    w.WriteLine();
                    w.WriteLine("[Section2]");
                    w.WriteLine("20=English");
                    w.WriteLine("[Section3]");
                    w.WriteLine("30=Français");
                    w.Close();
                }

                IniKey[] keys = new IniKey[5];
                keys[0] = new IniKey("Section2", "영어");
                keys[1] = new IniKey("Section1", "한중일 (CJK)");
                keys[2] = new IniKey("Section3", "프랑스어");
                keys[3] = new IniKey("Section4", "עברית");
                keys[4] = new IniKey("Section4", "العربية");

                Assert.IsTrue(IniReadWriter.WriteRawLines(tempFile, keys, true));

                string read;
                Encoding encoding = TestHelper.DetectTextEncoding(tempFile);
                using (StreamReader r = new StreamReader(tempFile, encoding))
                {
                    read = r.ReadToEnd();
                }

                StringBuilder b = new StringBuilder();
                b.AppendLine("[Section1]");
                b.AppendLine("10=한국어");
                b.AppendLine("11=中文");
                b.AppendLine("12=にほんご");
                b.AppendLine("한중일 (CJK)");
                b.AppendLine();
                b.AppendLine("[Section2]");
                b.AppendLine("20=English");
                b.AppendLine("영어");
                b.AppendLine("[Section3]");
                b.AppendLine("30=Français");
                b.AppendLine("프랑스어");
                b.AppendLine();
                b.AppendLine("[Section4]");
                b.AppendLine("עברית");
                b.AppendLine("العربية");
                string comp = b.ToString();

                Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
        #endregion

        #region DeleteKey
        [TestCategory("PEBakery.Ini")]
        [TestMethod]
        public void DeleteKey()
        {
            DeleteKey_1();
            DeleteKey_2();
            DeleteKey_3();
        }

        public void DeleteKey_1()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section]");
                    w.WriteLine("1=A");
                    w.WriteLine("2=B");
                    w.WriteLine("3=C");
                    w.WriteLine("4=D");
                }

                Assert.IsTrue(IniReadWriter.DeleteKey(tempFile, "Section", "2"));

                string read;
                Encoding encoding = TestHelper.DetectTextEncoding(tempFile);
                using (StreamReader r = new StreamReader(tempFile, encoding))
                {
                    read = r.ReadToEnd();
                }

                StringBuilder b = new StringBuilder();
                b.AppendLine("[Section]");
                b.AppendLine("1=A");
                b.AppendLine("3=C");
                b.AppendLine("4=D");
                string comp = b.ToString();

                Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        public void DeleteKey_2()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section]");
                    w.WriteLine("1=A");
                    w.WriteLine("2=B");
                    w.WriteLine("3=C");
                    w.WriteLine("4=D");
                }

                // Induce Error
                Assert.IsFalse(IniReadWriter.DeleteKey(tempFile, "Section", "5"));

                string read;
                Encoding encoding = TestHelper.DetectTextEncoding(tempFile);
                using (StreamReader r = new StreamReader(tempFile, encoding))
                {
                    read = r.ReadToEnd();
                }

                StringBuilder b = new StringBuilder();
                b.AppendLine("[Section]");
                b.AppendLine("1=A");
                b.AppendLine("2=B");
                b.AppendLine("3=C");
                b.AppendLine("4=D");
                string comp = b.ToString();

                Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        public void DeleteKey_3()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section]");
                    w.WriteLine("1=A");
                    w.WriteLine("2=B");
                    w.WriteLine("3=C");
                    w.WriteLine("4=D");
                }

                Assert.IsTrue(IniReadWriter.DeleteKey(tempFile, "Section", "2"));
                Assert.IsTrue(IniReadWriter.DeleteKey(tempFile, "Section", "4"));

                string read;
                Encoding encoding = TestHelper.DetectTextEncoding(tempFile);
                using (StreamReader r = new StreamReader(tempFile, encoding))
                {
                    read = r.ReadToEnd();
                }

                StringBuilder b = new StringBuilder();
                b.AppendLine("[Section]");
                b.AppendLine("1=A");
                b.AppendLine("3=C");
                string comp = b.ToString();

                Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
        #endregion

        #region DeleteKeys
        [TestCategory("PEBakery.Ini")]
        [TestMethod]
        public void DeleteKeys()
        {
            DeleteKeys_1();
        }

        public void DeleteKeys_1()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section1]");
                    w.WriteLine("00=A");
                    w.WriteLine("01=B");
                    w.WriteLine("02=C");
                    w.WriteLine();
                    w.WriteLine("[Section2]");
                    w.WriteLine("10=한");
                    w.WriteLine("11=국");
                    w.WriteLine("[Section3]");
                    w.WriteLine("20=韓");
                    w.WriteLine("21=國");
                }

                IniKey[] keys =
                {
                    new IniKey("Section1", "00"),
                    new IniKey("Section3", "20"),
                    new IniKey("Section2", "11"),
                };

                bool[] result = IniReadWriter.DeleteKeys(tempFile, keys);
                Assert.IsTrue(result.All(x => x));

                string read;
                Encoding encoding = TestHelper.DetectTextEncoding(tempFile);
                using (StreamReader r = new StreamReader(tempFile, encoding))
                {
                    read = r.ReadToEnd();
                }

                StringBuilder b = new StringBuilder();
                b.AppendLine("[Section1]");
                b.AppendLine("01=B");
                b.AppendLine("02=C");
                b.AppendLine();
                b.AppendLine("[Section2]");
                b.AppendLine("10=한");
                b.AppendLine("[Section3]");
                b.AppendLine("21=國");

                string comp = b.ToString();

                Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
        #endregion

        #region ReadSection
        [TestCategory("PEBakery.Ini")]
        [TestMethod]
        public void ReadSection()
        {
            ReadSection_1();
            ReadSection_2();
            ReadSection_3();
        }

        public void ReadSection_1()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section]");
                    w.WriteLine("1=A");
                    w.WriteLine("2=B");
                }

                IniKey[] keys = IniReadWriter.ReadSection(tempFile, "Section");

                Assert.IsTrue(keys[0].Key.Equals("1", StringComparison.Ordinal));
                Assert.IsTrue(keys[0].Value.Equals("A", StringComparison.Ordinal));
                Assert.IsTrue(keys[1].Key.Equals("2", StringComparison.Ordinal));
                Assert.IsTrue(keys[1].Value.Equals("B", StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        public void ReadSection_2()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section]");
                    w.WriteLine("1=A");
                    w.WriteLine("2=B");
                }

                IniKey[] keys = IniReadWriter.ReadSection(tempFile, "Dummy");
                // Assert.IsTrue(keys.Count() == 0);
                Assert.IsTrue(keys == null);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        public void ReadSection_3()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section1]");
                    w.WriteLine("00=A");
                    w.WriteLine("01=B");
                    w.WriteLine("02=C");
                    w.WriteLine();
                    w.WriteLine("[Section2]");
                    w.WriteLine("10=한");
                    w.WriteLine("11=국");
                    w.WriteLine("[Section3]");
                    w.WriteLine("20=韓");
                    w.WriteLine("21=國");
                }

                IniKey[] keys = IniReadWriter.ReadSection(tempFile, new IniKey("Section1"));
                Assert.IsTrue(keys[0].Key.Equals("00", StringComparison.Ordinal));
                Assert.IsTrue(keys[0].Value.Equals("A", StringComparison.Ordinal));
                Assert.IsTrue(keys[1].Key.Equals("01", StringComparison.Ordinal));
                Assert.IsTrue(keys[1].Value.Equals("B", StringComparison.Ordinal));
                Assert.IsTrue(keys[2].Key.Equals("02", StringComparison.Ordinal));
                Assert.IsTrue(keys[2].Value.Equals("C", StringComparison.Ordinal));

                keys = IniReadWriter.ReadSection(tempFile, "Section2");
                Assert.IsTrue(keys[0].Key.Equals("10", StringComparison.Ordinal));
                Assert.IsTrue(keys[0].Value.Equals("한", StringComparison.Ordinal));
                Assert.IsTrue(keys[1].Key.Equals("11", StringComparison.Ordinal));
                Assert.IsTrue(keys[1].Value.Equals("국", StringComparison.Ordinal));

                keys = IniReadWriter.ReadSection(tempFile, "Section3");
                Assert.IsTrue(keys[0].Key.Equals("20", StringComparison.Ordinal));
                Assert.IsTrue(keys[0].Value.Equals("韓", StringComparison.Ordinal));
                Assert.IsTrue(keys[1].Key.Equals("21", StringComparison.Ordinal));
                Assert.IsTrue(keys[1].Value.Equals("國", StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
        #endregion

        #region ReadSections
        [TestCategory("PEBakery.Ini")]
        [TestMethod]
        public void ReadSections()
        {
            ReadSections_1();
            ReadSections_2();
            ReadSections_3();
        }

        public void ReadSections_1()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section]");
                    w.WriteLine("1=A");
                    w.WriteLine("2=B");
                }

                Dictionary<string, IniKey[]> keyDict = IniReadWriter.ReadSections(tempFile, new IniKey[] { new IniKey("Section") });
                IniKey[] keys = keyDict["Section"];
                Assert.IsTrue(keys[0].Key.Equals("1", StringComparison.Ordinal));
                Assert.IsTrue(keys[0].Value.Equals("A", StringComparison.Ordinal));
                Assert.IsTrue(keys[1].Key.Equals("2", StringComparison.Ordinal));
                Assert.IsTrue(keys[1].Value.Equals("B", StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        public void ReadSections_2()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section]");
                    w.WriteLine("1=A");
                    w.WriteLine("2=B");
                }

                Dictionary<string, IniKey[]> keyDict = IniReadWriter.ReadSections(tempFile, new string[] { "Section", "Dummy" });
                IniKey[] keys = keyDict["Section"];
                Assert.IsTrue(keys[0].Key.Equals("1", StringComparison.Ordinal));
                Assert.IsTrue(keys[0].Value.Equals("A", StringComparison.Ordinal));
                Assert.IsTrue(keys[1].Key.Equals("2", StringComparison.Ordinal));
                Assert.IsTrue(keys[1].Value.Equals("B", StringComparison.Ordinal));

                keys = keyDict["Dummy"];
                Assert.IsTrue(keys == null);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        public void ReadSections_3()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section1]");
                    w.WriteLine("00=A");
                    w.WriteLine("01=B");
                    w.WriteLine("02=C");
                    w.WriteLine();
                    w.WriteLine("[Section2]");
                    w.WriteLine("10=한");
                    w.WriteLine("11=국");
                    w.WriteLine("[Section3]");
                    w.WriteLine("20=韓");
                    w.WriteLine("21=國");
                }

                Dictionary<string, IniKey[]> keyDict = IniReadWriter.ReadSections(tempFile, new string[] { "Section1", "Section2", "Section3" });

                Assert.IsTrue(keyDict.ContainsKey("Section1"));
                IniKey[] keys = keyDict["Section1"];
                Assert.IsTrue(keys[0].Key.Equals("00", StringComparison.Ordinal));
                Assert.IsTrue(keys[0].Value.Equals("A", StringComparison.Ordinal));
                Assert.IsTrue(keys[1].Key.Equals("01", StringComparison.Ordinal));
                Assert.IsTrue(keys[1].Value.Equals("B", StringComparison.Ordinal));
                Assert.IsTrue(keys[2].Key.Equals("02", StringComparison.Ordinal));
                Assert.IsTrue(keys[2].Value.Equals("C", StringComparison.Ordinal));

                Assert.IsTrue(keyDict.ContainsKey("Section2"));
                keys = keyDict["Section2"];
                Assert.IsTrue(keys[0].Key.Equals("10", StringComparison.Ordinal));
                Assert.IsTrue(keys[0].Value.Equals("한", StringComparison.Ordinal));
                Assert.IsTrue(keys[1].Key.Equals("11", StringComparison.Ordinal));
                Assert.IsTrue(keys[1].Value.Equals("국", StringComparison.Ordinal));

                Assert.IsTrue(keyDict.ContainsKey("Section3"));
                keys = keyDict["Section3"];
                Assert.IsTrue(keys[0].Key.Equals("20", StringComparison.Ordinal));
                Assert.IsTrue(keys[0].Value.Equals("韓", StringComparison.Ordinal));
                Assert.IsTrue(keys[1].Key.Equals("21", StringComparison.Ordinal));
                Assert.IsTrue(keys[1].Value.Equals("國", StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
        #endregion

        #region AddSection
        [TestCategory("PEBakery.Ini")]
        [TestMethod]
        public void AddSection()
        {
            AddSection_1();
            AddSection_2();
            AddSection_3();
        }

        public void AddSection_1()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);
                Assert.IsTrue(IniReadWriter.AddSection(tempFile, "Section"));

                string read;
                Encoding encoding = TestHelper.DetectTextEncoding(tempFile);
                using (StreamReader r = new StreamReader(tempFile, encoding))
                {
                    read = r.ReadToEnd();
                }

                StringBuilder b = new StringBuilder();
                b.AppendLine("[Section]");

                string comp = b.ToString();

                Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        public void AddSection_2()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section]");
                    w.WriteLine("1=A");
                    w.WriteLine("2=B");
                }

                Assert.IsTrue(IniReadWriter.AddSection(tempFile, "Section"));

                string read;
                Encoding encoding = TestHelper.DetectTextEncoding(tempFile);
                using (StreamReader r = new StreamReader(tempFile, encoding))
                {
                    read = r.ReadToEnd();
                }

                StringBuilder b = new StringBuilder();
                b.AppendLine("[Section]");
                b.AppendLine("1=A");
                b.AppendLine("2=B");

                string comp = b.ToString();

                Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        public void AddSection_3()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section1]");
                    w.WriteLine("00=A");
                    w.WriteLine("01=B");
                    w.WriteLine("02=C");
                    w.WriteLine();
                    w.WriteLine("[Section2]");
                    w.WriteLine("10=한");
                    w.WriteLine("11=국");
                    w.WriteLine("[Section3]");
                    w.WriteLine("20=韓");
                    w.WriteLine("21=國");
                }

                Assert.IsTrue(IniReadWriter.AddSection(tempFile, "Section4"));

                string read;
                Encoding encoding = TestHelper.DetectTextEncoding(tempFile);
                using (StreamReader r = new StreamReader(tempFile, encoding))
                {
                    read = r.ReadToEnd();
                }

                StringBuilder b = new StringBuilder();
                b.AppendLine("[Section1]");
                b.AppendLine("00=A");
                b.AppendLine("01=B");
                b.AppendLine("02=C");
                b.AppendLine();
                b.AppendLine("[Section2]");
                b.AppendLine("10=한");
                b.AppendLine("11=국");
                b.AppendLine("[Section3]");
                b.AppendLine("20=韓");
                b.AppendLine("21=國");
                b.AppendLine();
                b.AppendLine("[Section4]");

                string comp = b.ToString();

                Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
        #endregion

        #region AddSections
        [TestCategory("PEBakery.Ini")]
        [TestMethod]
        public void AddSections()
        {
            AddSections_1();
            AddSections_2();
        }

        public void AddSections_1()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);

                List<string> sections = new List<string>
                {
                    "Section1",
                    "Section3",
                    "Section2",
                };

                Assert.IsTrue(IniReadWriter.AddSections(tempFile, sections));

                string read;
                Encoding encoding = TestHelper.DetectTextEncoding(tempFile);
                using (StreamReader r = new StreamReader(tempFile, encoding))
                {
                    read = r.ReadToEnd();
                }

                StringBuilder b = new StringBuilder();
                b.AppendLine("[Section1]");
                b.AppendLine();
                b.AppendLine("[Section3]");
                b.AppendLine();
                b.AppendLine("[Section2]");

                string comp = b.ToString();
                Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        public void AddSections_2()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section1]");
                    w.WriteLine("00=A");
                    w.WriteLine("01=B");
                    w.WriteLine("02=C");
                    w.WriteLine();
                    w.WriteLine("[Section2]");
                    w.WriteLine("10=한");
                    w.WriteLine("11=국");
                    w.WriteLine("[Section3]");
                    w.WriteLine("20=韓");
                    w.WriteLine("21=國");
                }

                List<string> sections = new List<string>()
                {
                    "Section4",
                    "Section2",
                };

                Assert.IsTrue(IniReadWriter.AddSections(tempFile, sections));

                string read;
                Encoding encoding = TestHelper.DetectTextEncoding(tempFile);
                using (StreamReader r = new StreamReader(tempFile, encoding))
                {
                    read = r.ReadToEnd();
                }

                StringBuilder b = new StringBuilder();
                b.AppendLine("[Section1]");
                b.AppendLine("00=A");
                b.AppendLine("01=B");
                b.AppendLine("02=C");
                b.AppendLine();
                b.AppendLine("[Section2]");
                b.AppendLine("10=한");
                b.AppendLine("11=국");
                b.AppendLine("[Section3]");
                b.AppendLine("20=韓");
                b.AppendLine("21=國");
                b.AppendLine();
                b.AppendLine("[Section4]");

                string comp = b.ToString();
                Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
        #endregion

        #region DeleteSection
        [TestCategory("PEBakery.Ini")]
        [TestMethod]
        public void DeleteSection()
        {
            DeleteSection_1();
            DeleteSection_2();
            DeleteSection_3();
        }

        public void DeleteSection_1()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);

                // Induce Error
                Assert.IsFalse(IniReadWriter.DeleteSection(tempFile, "Section"));

                Encoding encoding = TestHelper.DetectTextEncoding(tempFile);
                using (StreamReader r = new StreamReader(tempFile, encoding))
                {
                    string read = r.ReadToEnd();
                    string comp = string.Empty;

                    Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
                }
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        public void DeleteSection_2()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section]");
                    w.WriteLine("1=A");
                    w.WriteLine("2=B");
                }

                Assert.IsTrue(IniReadWriter.DeleteSection(tempFile, "Section"));

                Encoding encoding = TestHelper.DetectTextEncoding(tempFile);
                using (StreamReader r = new StreamReader(tempFile, encoding))
                {
                    // Must be same
                    string read = r.ReadToEnd();
                    string comp = string.Empty;

                    Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
                }
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        public void DeleteSection_3()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section1]");
                    w.WriteLine("00=A");
                    w.WriteLine("01=B");
                    w.WriteLine("02=C");
                    w.WriteLine();
                    w.WriteLine("[Section2]");
                    w.WriteLine("10=한");
                    w.WriteLine("11=국");
                    w.WriteLine("[Section3]");
                    w.WriteLine("20=韓");
                    w.WriteLine("21=國");
                }

                Assert.IsTrue(IniReadWriter.DeleteSection(tempFile, "Section2"));

                string read;
                Encoding encoding = TestHelper.DetectTextEncoding(tempFile);
                using (StreamReader r = new StreamReader(tempFile, encoding))
                {
                    read = r.ReadToEnd();
                }

                StringBuilder b = new StringBuilder();
                b.AppendLine("[Section1]");
                b.AppendLine("00=A");
                b.AppendLine("01=B");
                b.AppendLine("02=C");
                b.AppendLine();
                b.AppendLine("[Section3]");
                b.AppendLine("20=韓");
                b.AppendLine("21=國");

                string comp = b.ToString();
                Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
        #endregion

        #region DeleteSections
        [TestCategory("PEBakery.Ini")]
        [TestMethod]
        public void DeleteSections()
        {
            DeleteSections_1();
            DeleteSections_2();
        }

        public void DeleteSections_1()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section1]");
                    w.WriteLine("00=A");
                    w.WriteLine("01=B");
                    w.WriteLine("02=C");
                    w.WriteLine();
                    w.WriteLine("[Section2]");
                    w.WriteLine("10=한");
                    w.WriteLine("11=국");
                    w.WriteLine("[Section3]");
                    w.WriteLine("20=韓");
                    w.WriteLine("21=國");
                }

                List<string> sections = new List<string>()
                {
                    "Section1",
                    "Section3",
                };

                Assert.IsTrue(IniReadWriter.DeleteSections(tempFile, sections));

                string read;
                Encoding encoding = TestHelper.DetectTextEncoding(tempFile);
                using (StreamReader r = new StreamReader(tempFile, encoding))
                {
                    read = r.ReadToEnd();
                }

                StringBuilder b = new StringBuilder();
                b.AppendLine("[Section2]");
                b.AppendLine("10=한");
                b.AppendLine("11=국");

                string comp = b.ToString();
                Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        public void DeleteSections_2()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section1]");
                    w.WriteLine("00=A");
                    w.WriteLine("01=B");
                    w.WriteLine("02=C");
                    w.WriteLine();
                    w.WriteLine("[Section2]");
                    w.WriteLine("10=한");
                    w.WriteLine("11=국");
                    w.WriteLine("[Section3]");
                    w.WriteLine("20=韓");
                    w.WriteLine("21=國");
                }

                List<string> sections = new List<string>()
                {
                    "Section4",
                    "Section2",
                };

                Assert.IsFalse(IniReadWriter.DeleteSections(tempFile, sections));

                string read;
                Encoding encoding = TestHelper.DetectTextEncoding(tempFile);
                using (StreamReader r = new StreamReader(tempFile, encoding))
                {
                    read = r.ReadToEnd();
                }

                StringBuilder b = new StringBuilder();
                b.AppendLine("[Section1]");
                b.AppendLine("00=A");
                b.AppendLine("01=B");
                b.AppendLine("02=C");
                b.AppendLine();
                b.AppendLine("[Section2]");
                b.AppendLine("10=한");
                b.AppendLine("11=국");
                b.AppendLine("[Section3]");
                b.AppendLine("20=韓");
                b.AppendLine("21=國");

                string comp = b.ToString();
                Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
        #endregion

        #region ReadRawSection
        [TestCategory("PEBakery.Ini")]
        [TestMethod]
        public void ReadRawSection()
        {
            ReadRawSection_1();
            ReadRawSection_2();
            ReadRawSection_3();
            ReadRawSection_4();
        }

        public void ReadRawSection_1()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section]");
                    w.WriteLine("1=A");
                    w.WriteLine();
                    w.WriteLine("유니코드");
                    w.WriteLine("    ");
                    w.WriteLine("ABXYZ");
                }

                List<string> lines = IniReadWriter.ReadRawSection(tempFile, "Section", false);

                List<string> comps = new List<string>
                {
                    "1=A",
                    "유니코드",
                    "ABXYZ"
                };

                Assert.IsTrue(comps.SequenceEqual(lines, StringComparer.Ordinal));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        public void ReadRawSection_2()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section]");
                    w.WriteLine("1=A");
                    w.WriteLine();
                    w.WriteLine("유니코드");
                    w.WriteLine("    ");
                    w.WriteLine("ABXYZ");
                }

                List<string> lines = IniReadWriter.ReadRawSection(tempFile, "Section", true);

                List<string> comps = new List<string>
                {
                    "1=A",
                    string.Empty,
                    "유니코드",
                    string.Empty,
                    "ABXYZ"
                };

                Assert.IsTrue(comps.SequenceEqual(lines, StringComparer.Ordinal));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        public void ReadRawSection_3()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section1]");
                    w.WriteLine();
                    w.WriteLine("1");
                    w.WriteLine("[Section2]");
                    w.WriteLine("1=A");
                    w.WriteLine();
                    w.WriteLine("유니코드");
                    w.WriteLine("    ");
                    w.WriteLine("ABXYZ");
                    w.WriteLine();
                    w.WriteLine("[Section3]");
                    w.WriteLine("3");
                }

                List<string> lines = IniReadWriter.ReadRawSection(tempFile, "Section2", false);

                List<string> comps = new List<string>
                {
                    "1=A",
                    "유니코드",
                    "ABXYZ"
                };

                Assert.IsTrue(comps.SequenceEqual(lines, StringComparer.Ordinal));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        public void ReadRawSection_4()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section1]");
                    w.WriteLine();
                    w.WriteLine("1");
                    w.WriteLine("[Section2]");
                    w.WriteLine("1=A");
                    w.WriteLine();
                    w.WriteLine("유니코드");
                    w.WriteLine("    ");
                    w.WriteLine("ABXYZ");
                    w.WriteLine();
                    w.WriteLine("[Section3]");
                    w.WriteLine("3");
                }

                List<string> lines = IniReadWriter.ReadRawSection(tempFile, "Section2", true);

                List<string> comps = new List<string>
                {
                    "1=A",
                    string.Empty,
                    "유니코드",
                    string.Empty,
                    "ABXYZ",
                    string.Empty,
                };

                Assert.IsTrue(comps.SequenceEqual(lines, StringComparer.Ordinal));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
        #endregion

        #region ReadRawSections
        [TestCategory("PEBakery.Ini")]
        [TestMethod]
        public void ReadRawSections()
        {
            ReadRawSections_1();
            ReadRawSections_2();
        }

        public void ReadRawSections_1()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section1]");
                    w.WriteLine();
                    w.WriteLine("1");
                    w.WriteLine("[Section2]");
                    w.WriteLine("1=A");
                    w.WriteLine();
                    w.WriteLine("유니코드");
                    w.WriteLine("    ");
                    w.WriteLine("ABXYZ");
                    w.WriteLine();
                    w.WriteLine("[Section3]");
                    w.WriteLine("3");
                }

                Dictionary<string, List<string>> lineDict = IniReadWriter.ReadRawSections(tempFile, new string[] { "Section3", "Section2" }, true);
                Dictionary<string, List<string>> compDict =
                    new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Section3"] = new List<string>
                        {
                            "3",
                        },
                        ["Section2"] = new List<string>
                        {
                            "1=A",
                            string.Empty,
                            "유니코드",
                            string.Empty,
                            "ABXYZ",
                            string.Empty,
                        }
                    };

                foreach (string key in lineDict.Keys)
                {
                    Assert.IsTrue(lineDict[key].SequenceEqual(compDict[key], StringComparer.Ordinal));
                }
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        public void ReadRawSections_2()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section1]");
                    w.WriteLine();
                    w.WriteLine("1");
                    w.WriteLine("[Section2]");
                    w.WriteLine("1=A");
                    w.WriteLine();
                    w.WriteLine("유니코드");
                    w.WriteLine("    ");
                    w.WriteLine("ABXYZ");
                    w.WriteLine();
                    w.WriteLine("[Section3]");
                    w.WriteLine("3");
                }

                Dictionary<string, List<string>> lineDict = IniReadWriter.ReadRawSections(tempFile, new string[] { "Section3", "Section2" }, false);
                Dictionary<string, List<string>> compDict =
                    new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Section3"] = new List<string>
                        {
                            "3",
                        },
                        ["Section2"] = new List<string>
                        {
                            "1=A",
                            "유니코드",
                            "ABXYZ",
                        }
                    };

                foreach (string key in lineDict.Keys)
                {
                    Assert.IsTrue(lineDict[key].SequenceEqual(compDict[key], StringComparer.Ordinal));
                }
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
        #endregion

        #region Merge2
        [TestCategory("PEBakery.Ini")]
        [TestMethod]
        public void Merge2()
        {
            Merge2_1();
            Merge2_2();
            Merge2_3();
            Merge2_4();
        }

        public void Merge2_1()
        {
            string tempFile = Path.GetTempFileName();
            string destFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);
                TestHelper.WriteTextBom(destFile, Encoding.UTF8);

                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section1]");
                    w.WriteLine("01=A");
                    w.WriteLine("02=B");
                }

                Assert.IsTrue(IniReadWriter.Merge(tempFile, destFile));

                string read;
                Encoding encoding = TestHelper.DetectTextEncoding(destFile);
                using (StreamReader r = new StreamReader(destFile, encoding))
                {
                    read = r.ReadToEnd();
                }

                StringBuilder b = new StringBuilder();
                b.AppendLine("[Section1]");
                b.AppendLine("01=A");
                b.AppendLine("02=B");
                string comp = b.ToString();

                Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile);
                File.Delete(destFile);
            }
        }

        public void Merge2_2()
        {
            string tempFile = Path.GetTempFileName();
            string destFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.UTF8);
                TestHelper.WriteTextBom(destFile, Encoding.Unicode);

                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section1]");
                    w.WriteLine("01=A");
                    w.WriteLine("02=B");
                }

                using (StreamWriter w = new StreamWriter(destFile, false, Encoding.Unicode))
                {
                    w.WriteLine("[Section2]");
                    w.WriteLine("03=C");
                }

                Assert.IsTrue(IniReadWriter.Merge(tempFile, destFile));

                string read;
                Encoding encoding = TestHelper.DetectTextEncoding(destFile);
                using (StreamReader r = new StreamReader(destFile, encoding))
                {
                    read = r.ReadToEnd();
                }

                StringBuilder b = new StringBuilder();
                b.AppendLine("[Section2]");
                b.AppendLine("03=C");
                b.AppendLine();
                b.AppendLine("[Section1]");
                b.AppendLine("01=A");
                b.AppendLine("02=B");
                string comp = b.ToString();

                Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile);
                File.Delete(destFile);
            }
        }

        public void Merge2_3()
        {
            string tempFile = Path.GetTempFileName();
            string destFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.Unicode);
                TestHelper.WriteTextBom(destFile, Encoding.UTF8);

                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.Unicode))
                {
                    w.WriteLine("[Section1]");
                    w.WriteLine("01=A");
                    w.WriteLine("02=B");
                }

                using (StreamWriter w = new StreamWriter(destFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section1]");
                    w.WriteLine("04=D");
                    w.WriteLine();
                    w.WriteLine("[Section2]");
                    w.WriteLine("03=C");
                }

                Assert.IsTrue(IniReadWriter.Merge(tempFile, destFile));

                string read;
                Encoding encoding = TestHelper.DetectTextEncoding(destFile);
                using (StreamReader r = new StreamReader(destFile, encoding))
                {
                    read = r.ReadToEnd();
                }

                StringBuilder b = new StringBuilder();
                b.AppendLine("[Section1]");
                b.AppendLine("04=D");
                b.AppendLine("01=A");
                b.AppendLine("02=B");
                b.AppendLine();
                b.AppendLine("[Section2]");
                b.AppendLine("03=C");
                string comp = b.ToString();

                Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile);
                File.Delete(destFile);
            }
        }

        public void Merge2_4()
        {
            string tempFile = Path.GetTempFileName();
            string destFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile, Encoding.Unicode);
                TestHelper.WriteTextBom(destFile, Encoding.BigEndianUnicode);

                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.Unicode))
                {
                    w.WriteLine("[Section1]");
                    w.WriteLine("02=D");
                    w.WriteLine();
                    w.WriteLine("[Section2]");
                    w.WriteLine("03=C");
                }

                using (StreamWriter w = new StreamWriter(destFile, false, Encoding.BigEndianUnicode))
                {
                    w.WriteLine("[Section1]");
                    w.WriteLine("01=A");
                    w.WriteLine("02=B");
                }

                Assert.IsTrue(IniReadWriter.Merge(tempFile, destFile));

                string read;
                Encoding encoding = TestHelper.DetectTextEncoding(destFile);
                using (StreamReader r = new StreamReader(destFile, encoding))
                {
                    read = r.ReadToEnd();
                }

                StringBuilder b = new StringBuilder();
                b.AppendLine("[Section1]");
                b.AppendLine("01=A");
                b.AppendLine("02=D");
                b.AppendLine();
                b.AppendLine("[Section2]");
                b.AppendLine("03=C");
                string comp = b.ToString();

                Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile);
                File.Delete(destFile);
            }
        }
        #endregion

        #region Merge3
        [TestCategory("PEBakery.Ini")]
        [TestMethod]
        public void Merge3()
        {
            Merge3_1();
            Merge3_2();
            Merge3_3();
            Merge3_4();
            Merge3_5();
            Merge3_6();
        }

        public void Merge3_1()
        {
            string tempFile1 = Path.GetTempFileName();
            string tempFile2 = Path.GetTempFileName();
            string destFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(destFile, Encoding.UTF8);

                Assert.IsTrue(IniReadWriter.Merge(tempFile1, tempFile2, destFile));

                string read;
                Encoding encoding = TestHelper.DetectTextEncoding(destFile);
                using (StreamReader r = new StreamReader(destFile, encoding))
                {
                    read = r.ReadToEnd();
                }

                Assert.IsTrue(read.Equals(string.Empty, StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile1);
                File.Delete(tempFile2);
                File.Delete(destFile);
            }
        }

        public void Merge3_2()
        {
            string tempFile1 = Path.GetTempFileName();
            string tempFile2 = Path.GetTempFileName();
            string destFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile1, Encoding.UTF8);
                TestHelper.WriteTextBom(destFile, Encoding.UTF8);

                using (StreamWriter w = new StreamWriter(tempFile1, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section1]");
                    w.WriteLine("01=A");
                    w.WriteLine("02=B");
                }

                Assert.IsTrue(IniReadWriter.Merge(tempFile1, tempFile2, destFile));

                string read;
                Encoding encoding = TestHelper.DetectTextEncoding(destFile);
                using (StreamReader r = new StreamReader(destFile, encoding))
                {
                    read = r.ReadToEnd();
                }

                StringBuilder b = new StringBuilder();
                b.AppendLine("[Section1]");
                b.AppendLine("01=A");
                b.AppendLine("02=B");
                string comp = b.ToString();

                Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile1);
                File.Delete(tempFile2);
                File.Delete(destFile);
            }
        }

        public void Merge3_3()
        {
            string tempFile1 = Path.GetTempFileName();
            string tempFile2 = Path.GetTempFileName();
            string destFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile1, Encoding.UTF8);
                TestHelper.WriteTextBom(destFile, Encoding.UTF8);

                using (StreamWriter w = new StreamWriter(tempFile1, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section1]");
                    w.WriteLine("01=A");
                    w.WriteLine("02=B");
                }

                using (StreamWriter w = new StreamWriter(tempFile2, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section2]");
                    w.WriteLine("03=C");
                }

                Assert.IsTrue(IniReadWriter.Merge(tempFile1, tempFile2, destFile));

                string read;
                Encoding encoding = TestHelper.DetectTextEncoding(destFile);
                using (StreamReader r = new StreamReader(destFile, encoding))
                {
                    read = r.ReadToEnd();
                }

                StringBuilder b = new StringBuilder();
                b.AppendLine("[Section1]");
                b.AppendLine("01=A");
                b.AppendLine("02=B");
                b.AppendLine();
                b.AppendLine("[Section2]");
                b.AppendLine("03=C");
                string comp = b.ToString();

                Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile1);
                File.Delete(tempFile2);
                File.Delete(destFile);
            }
        }

        public void Merge3_4()
        {
            string tempFile1 = Path.GetTempFileName();
            string tempFile2 = Path.GetTempFileName();
            string destFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile1, Encoding.UTF8);
                TestHelper.WriteTextBom(destFile, Encoding.UTF8);

                using (StreamWriter w = new StreamWriter(tempFile1, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section1]");
                    w.WriteLine("01=A");
                    w.WriteLine("02=B");
                }

                using (StreamWriter w = new StreamWriter(tempFile2, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section1]");
                    w.WriteLine("04=D");
                    w.WriteLine();
                    w.WriteLine("[Section2]");
                    w.WriteLine("03=C");
                }

                Assert.IsTrue(IniReadWriter.Merge(tempFile1, tempFile2, destFile));

                string read;
                Encoding encoding = TestHelper.DetectTextEncoding(destFile);
                using (StreamReader r = new StreamReader(destFile, encoding))
                {
                    read = r.ReadToEnd();
                }

                StringBuilder b = new StringBuilder();
                b.AppendLine("[Section1]");
                b.AppendLine("01=A");
                b.AppendLine("02=B");
                b.AppendLine("04=D");
                b.AppendLine();
                b.AppendLine("[Section2]");
                b.AppendLine("03=C");
                string comp = b.ToString();

                Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile1);
                File.Delete(tempFile2);
                File.Delete(destFile);
            }
        }

        public void Merge3_5()
        {
            string tempFile1 = Path.GetTempFileName();
            string tempFile2 = Path.GetTempFileName();
            string destFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile1, Encoding.UTF8);
                TestHelper.WriteTextBom(tempFile2, Encoding.UTF8);
                TestHelper.WriteTextBom(destFile, Encoding.UTF8);

                using (StreamWriter w = new StreamWriter(tempFile1, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section1]");
                    w.WriteLine("01=A");
                    w.WriteLine("02=B");
                }

                using (StreamWriter w = new StreamWriter(tempFile2, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section1]");
                    w.WriteLine("02=D");
                    w.WriteLine();
                    w.WriteLine("[Section2]");
                    w.WriteLine("03=C");
                }

                Assert.IsTrue(IniReadWriter.Merge(tempFile1, tempFile2, destFile));

                string read;
                Encoding encoding = TestHelper.DetectTextEncoding(destFile);
                using (StreamReader r = new StreamReader(destFile, encoding))
                {
                    read = r.ReadToEnd();
                }

                StringBuilder b = new StringBuilder();
                b.AppendLine("[Section1]");
                b.AppendLine("01=A");
                b.AppendLine("02=D");
                b.AppendLine();
                b.AppendLine("[Section2]");
                b.AppendLine("03=C");
                string comp = b.ToString();

                Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile1);
                File.Delete(tempFile2);
                File.Delete(destFile);
            }
        }

        public void Merge3_6()
        {
            string tempFile1 = Path.GetTempFileName();
            string tempFile2 = Path.GetTempFileName();
            string destFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBom(tempFile1, Encoding.UTF8);
                TestHelper.WriteTextBom(destFile, Encoding.UTF8);

                using (StreamWriter w = new StreamWriter(tempFile1, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section1]");
                    w.WriteLine("01=A");
                    w.WriteLine("02=B");
                }

                using (StreamWriter w = new StreamWriter(tempFile2, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section1]");
                    w.WriteLine("02=D");
                    w.WriteLine();
                    w.WriteLine("[Section2]");
                    w.WriteLine("03=C");
                }

                using (StreamWriter w = new StreamWriter(destFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section1]");
                    w.WriteLine("02=E");
                    w.WriteLine();
                    w.WriteLine("[Section2]");
                    w.WriteLine("04=F");
                }

                Assert.IsTrue(IniReadWriter.Merge(tempFile1, tempFile2, destFile));

                string read;
                Encoding encoding = TestHelper.DetectTextEncoding(destFile);
                using (StreamReader r = new StreamReader(destFile, encoding))
                {
                    read = r.ReadToEnd();
                }

                StringBuilder b = new StringBuilder();
                b.AppendLine("[Section1]");
                b.AppendLine("02=D");
                b.AppendLine("01=A");
                b.AppendLine();
                b.AppendLine("[Section2]");
                b.AppendLine("04=F");
                b.AppendLine("03=C");
                b.AppendLine();
                string comp = b.ToString();

                Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile1);
                File.Delete(tempFile2);
                File.Delete(destFile);
            }
        }
        #endregion
    }
}
