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

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace PEBakery.IniLib.Tests
{
    [TestClass]
    public class IniLibTests
    {
        #region ReadKey
        [TestCategory("IniLib")]
        [TestMethod]
        public void IniLib_ReadKey()
        {
            IniLib_ReadKey_1();
            IniLib_ReadKey_2();
            IniLib_ReadKey_3();
        }

        public void IniLib_ReadKey_1()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                Assert.IsNull(Ini.ReadKey(tempFile, "Section", "Key"));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        public void IniLib_ReadKey_2()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section]");
                    w.WriteLine("Key=Value");
                    w.Close();
                }

                Assert.IsTrue(Ini.ReadKey(tempFile, "Section", "Key").Equals("Value", StringComparison.Ordinal));
                Assert.IsTrue(Ini.ReadKey(tempFile, "Section", "key").Equals("Value", StringComparison.Ordinal));
                Assert.IsTrue(Ini.ReadKey(tempFile, "section", "Key").Equals("Value", StringComparison.Ordinal));
                Assert.IsTrue(Ini.ReadKey(tempFile, "section", "key").Equals("Value", StringComparison.Ordinal));
                Assert.IsFalse(Ini.ReadKey(tempFile, "Section", "Key").Equals("value", StringComparison.Ordinal));

            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        public void IniLib_ReadKey_3()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile, Encoding.UTF8);
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

                Assert.IsTrue(Ini.ReadKey(tempFile, "Section1", "1").Equals("A", StringComparison.Ordinal));
                Assert.IsTrue(Ini.ReadKey(tempFile, "Section1", "2").Equals("B", StringComparison.Ordinal));
                Assert.IsTrue(Ini.ReadKey(tempFile, "section1", "3").Equals("C", StringComparison.Ordinal));
                Assert.IsTrue(Ini.ReadKey(tempFile, "Section2", "4").Equals("D", StringComparison.Ordinal));
                Assert.IsTrue(Ini.ReadKey(tempFile, "Section2", "5").Equals("E", StringComparison.Ordinal));
                Assert.IsTrue(Ini.ReadKey(tempFile, "section3", "6").Equals("F", StringComparison.Ordinal));
                Assert.IsTrue(Ini.ReadKey(tempFile, "section3", "7").Equals("G", StringComparison.Ordinal));
                Assert.IsTrue(Ini.ReadKey(tempFile, "section3", "8").Equals("H", StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
        #endregion

        #region ReadKeys
        [TestCategory("IniLib")]
        [TestMethod]
        public void IniLib_ReadKeys()
        {
            IniLib_ReadKeys_1();
        }

        public void IniLib_ReadKeys_1()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile, Encoding.UTF8);
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

                keys = Ini.ReadKeys(tempFile, keys);
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
        [TestCategory("IniLib")]
        [TestMethod]
        public void IniLib_WriteKey()
        {
            IniLib_WriteKey_1();
            IniLib_WriteKey_2();
            IniLib_WriteKey_3();
            IniLib_WriteKey_4();
            IniLib_WriteKey_5();
            IniLib_WriteKey_6();
        }

        public void IniLib_WriteKey_1()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                Assert.IsTrue(Ini.WriteKey(tempFile, "Section", "Key", "Value"));

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

        public void IniLib_WriteKey_2()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section]");
                    w.WriteLine("Key=A");
                    w.Close();
                }

                Assert.IsTrue(Ini.WriteKey(tempFile, "Section", "Key", "B"));

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

        public void IniLib_WriteKey_3()
        { // Found while testing EncodedFile.EncodeFile()
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section]");
                    w.WriteLine("Sect2");
                    w.Close();
                }

                Assert.IsTrue(Ini.WriteKey(tempFile, "Section2", "Key", "B"));

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

        public void IniLib_WriteKey_4()
        { // Found while testing EncodedFile.EncodeFile()
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section]");
                    w.WriteLine("Section2");
                    w.Close();
                }

                Assert.IsTrue(Ini.WriteKey(tempFile, "Section2", "Key", "B"));

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

        public void IniLib_WriteKey_5()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                Assert.IsTrue(Ini.WriteKey(tempFile, "Section", "Key", "Value"));

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

        public void IniLib_WriteKey_6()
        { // https://github.com/pebakery/pebakery/issues/57
            string tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                TestHelper.WriteTextBOM(tempFile, Encoding.UTF8);
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

                Assert.IsTrue(Ini.WriteKey(tempFile, "Interface", "FileBox2", "Overwrite2"));
                Assert.IsTrue(Ini.WriteKey(tempFile, "Interface", "FileBox3", "Overwrite3"));

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
        [TestCategory("IniLib")]
        [TestMethod]
        public void IniLib_WriteKeys()
        {
            IniLib_WriteKeys_1();
            IniLib_WriteKeys_2();
        }

        public void IniLib_WriteKeys_1()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile, Encoding.UTF8);

                IniKey[] keys = new IniKey[3];
                keys[0] = new IniKey("Section2", "20", "English");
                keys[1] = new IniKey("Section1", "10", "한국어");
                keys[2] = new IniKey("Section3", "30", "Français");

                Assert.IsTrue(Ini.WriteKeys(tempFile, keys));

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

        public void IniLib_WriteKeys_2()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile, Encoding.UTF8);
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

                Assert.IsTrue(Ini.WriteKeys(tempFile, keys));

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
        [TestCategory("IniLib")]
        [TestMethod]
        public void IniLib_WriteRawLine()
        {
            IniLib_WriteRawLine_1();
            IniLib_WriteRawLine_2();
            IniLib_WriteRawLine_3();
            IniLib_WriteRawLine_4();
            IniLib_WriteRawLine_5();
        }

        public void IniLib_WriteRawLine_1()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile, Encoding.UTF8);

                Assert.IsTrue(Ini.WriteRawLine(tempFile, "Section", "RawLine"));

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

        public void IniLib_WriteRawLine_2()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section]");
                    w.WriteLine("1=A");
                    w.Close();
                }

                Assert.IsTrue(Ini.WriteRawLine(tempFile, "Section", "LineAppend", true));

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

        public void IniLib_WriteRawLine_3()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section]");
                    w.WriteLine("1=A");
                    w.Close();
                }

                Assert.IsTrue(Ini.WriteRawLine(tempFile, "Section", "LinePrepend", false));

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

        public void IniLib_WriteRawLine_4()
        { // Found while testing EncodedFile.EncodeFile()
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section]");
                    w.WriteLine("Sect2");
                    w.Close();
                }

                Assert.IsTrue(Ini.WriteRawLine(tempFile, "Section2", "Key"));

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

        public void IniLib_WriteRawLine_5()
        { // Found while testing EncodedFile.EncodeFile()
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section]");
                    w.WriteLine("Section2");
                    w.Close();
                }

                Assert.IsTrue(Ini.WriteRawLine(tempFile, "Section2", "Key"));

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
        [TestCategory("IniLib")]
        [TestMethod]
        public void IniLib_WriteRawLines()
        {
            IniLib_WriteRawLines_1();
            IniLib_WriteRawLines_2();
        }

        public void IniLib_WriteRawLines_1()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile, Encoding.UTF8);
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

                Assert.IsTrue(Ini.WriteRawLines(tempFile, keys, false));

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

        public void IniLib_WriteRawLines_2()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile, Encoding.UTF8);
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

                Assert.IsTrue(Ini.WriteRawLines(tempFile, keys, true));

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
        [TestCategory("IniLib")]
        [TestMethod]
        public void IniLib_DeleteKey()
        {
            IniLib_DeleteKey_1();
            IniLib_DeleteKey_2();
            IniLib_DeleteKey_3();
        }

        public void IniLib_DeleteKey_1()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section]");
                    w.WriteLine("1=A");
                    w.WriteLine("2=B");
                    w.WriteLine("3=C");
                    w.WriteLine("4=D");
                }

                Assert.IsTrue(Ini.DeleteKey(tempFile, "Section", "2"));

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

        public void IniLib_DeleteKey_2()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section]");
                    w.WriteLine("1=A");
                    w.WriteLine("2=B");
                    w.WriteLine("3=C");
                    w.WriteLine("4=D");
                }

                // Induce Error
                Assert.IsFalse(Ini.DeleteKey(tempFile, "Section", "5"));

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

        public void IniLib_DeleteKey_3()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section]");
                    w.WriteLine("1=A");
                    w.WriteLine("2=B");
                    w.WriteLine("3=C");
                    w.WriteLine("4=D");
                }

                Assert.IsTrue(Ini.DeleteKey(tempFile, "Section", "2"));
                Assert.IsTrue(Ini.DeleteKey(tempFile, "Section", "4"));

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
        [TestCategory("IniLib")]
        [TestMethod]
        public void IniLib_DeleteKeys()
        {
            IniLib_DeleteKeys_1();
        }

        public void IniLib_DeleteKeys_1()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile, Encoding.UTF8);
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

                bool[] result = Ini.DeleteKeys(tempFile, keys);
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
        [TestCategory("IniLib")]
        [TestMethod]
        public void IniLIb_ReadSection()
        {
            IniLib_ReadSection_1();
            IniLib_ReadSection_2();
            IniLib_ReadSection_3();
        }

        public void IniLib_ReadSection_1()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section]");
                    w.WriteLine("1=A");
                    w.WriteLine("2=B");
                }

                IniKey[] keys = Ini.ReadSection(tempFile, "Section");

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

        public void IniLib_ReadSection_2()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section]");
                    w.WriteLine("1=A");
                    w.WriteLine("2=B");
                }

                IniKey[] keys = Ini.ReadSection(tempFile, "Dummy");
                // Assert.IsTrue(keys.Count() == 0);
                Assert.IsTrue(keys == null);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        public void IniLib_ReadSection_3()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile, Encoding.UTF8);
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

                IniKey[] keys = Ini.ReadSection(tempFile, new IniKey("Section1"));
                Assert.IsTrue(keys[0].Key.Equals("00", StringComparison.Ordinal));
                Assert.IsTrue(keys[0].Value.Equals("A", StringComparison.Ordinal));
                Assert.IsTrue(keys[1].Key.Equals("01", StringComparison.Ordinal));
                Assert.IsTrue(keys[1].Value.Equals("B", StringComparison.Ordinal));
                Assert.IsTrue(keys[2].Key.Equals("02", StringComparison.Ordinal));
                Assert.IsTrue(keys[2].Value.Equals("C", StringComparison.Ordinal));

                keys = Ini.ReadSection(tempFile, "Section2");
                Assert.IsTrue(keys[0].Key.Equals("10", StringComparison.Ordinal));
                Assert.IsTrue(keys[0].Value.Equals("한", StringComparison.Ordinal));
                Assert.IsTrue(keys[1].Key.Equals("11", StringComparison.Ordinal));
                Assert.IsTrue(keys[1].Value.Equals("국", StringComparison.Ordinal));

                keys = Ini.ReadSection(tempFile, "Section3");
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
        [TestCategory("IniLib")]
        [TestMethod]
        public void IniLib_ReadSections()
        {
            IniLib_ReadSections_1();
            IniLib_ReadSections_2();
            IniLib_ReadSections_3();
        }

        public void IniLib_ReadSections_1()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section]");
                    w.WriteLine("1=A");
                    w.WriteLine("2=B");
                }

                Dictionary<string, IniKey[]> keyDict = Ini.ReadSections(tempFile, new IniKey[] { new IniKey("Section") });
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

        public void IniLib_ReadSections_2()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section]");
                    w.WriteLine("1=A");
                    w.WriteLine("2=B");
                }

                Dictionary<string, IniKey[]> keyDict = Ini.ReadSections(tempFile, new string[] { "Section", "Dummy" });
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

        public void IniLib_ReadSections_3()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile, Encoding.UTF8);
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

                Dictionary<string, IniKey[]> keyDict = Ini.ReadSections(tempFile, new string[] { "Section1", "Section2", "Section3" });

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
        [TestCategory("IniLib")]
        [TestMethod]
        public void IniLIb_AddSection()
        {
            IniLib_AddSection_1();
            IniLib_AddSection_2();
            IniLib_AddSection_3();
        }

        public void IniLib_AddSection_1()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile, Encoding.UTF8);
                Assert.IsTrue(Ini.AddSection(tempFile, "Section"));

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

        public void IniLib_AddSection_2()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section]");
                    w.WriteLine("1=A");
                    w.WriteLine("2=B");
                }

                Assert.IsTrue(Ini.AddSection(tempFile, "Section"));

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

        public void IniLib_AddSection_3()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile, Encoding.UTF8);
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

                Assert.IsTrue(Ini.AddSection(tempFile, "Section4"));

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
        [TestCategory("IniLib")]
        [TestMethod]
        public void IniLib_AddSections()
        {
            IniLib_AddSections_1();
            IniLib_AddSections_2();
        }

        public void IniLib_AddSections_1()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile, Encoding.UTF8);

                List<string> sections = new List<string>
                {
                    "Section1",
                    "Section3",
                    "Section2",
                };

                Assert.IsTrue(Ini.AddSections(tempFile, sections));

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

        public void IniLib_AddSections_2()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile, Encoding.UTF8);
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

                Assert.IsTrue(Ini.AddSections(tempFile, sections));

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
        [TestCategory("IniLib")]
        [TestMethod]
        public void IniLib_DeleteSection()
        {
            IniLib_DeleteSection_1();
            IniLib_DeleteSection_2();
            IniLib_DeleteSection_3();
        }

        public void IniLib_DeleteSection_1()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile, Encoding.UTF8);

                // Induce Error
                Assert.IsFalse(Ini.DeleteSection(tempFile, "Section"));

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

        public void IniLib_DeleteSection_2()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile, Encoding.UTF8);
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section]");
                    w.WriteLine("1=A");
                    w.WriteLine("2=B");
                }

                Assert.IsTrue(Ini.DeleteSection(tempFile, "Section"));

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

        public void IniLib_DeleteSection_3()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile, Encoding.UTF8);
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

                Assert.IsTrue(Ini.DeleteSection(tempFile, "Section2"));

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
        [TestCategory("IniLib")]
        [TestMethod]
        public void IniLib_DeleteSections()
        {
            IniLib_DeleteSections_1();
            IniLib_DeleteSections_2();
        }

        public void IniLib_DeleteSections_1()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile, Encoding.UTF8);
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

                Assert.IsTrue(Ini.DeleteSections(tempFile, sections));

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

        public void IniLib_DeleteSections_2()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile, Encoding.UTF8);
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

                Assert.IsFalse(Ini.DeleteSections(tempFile, sections));

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

        #region Merge2
        [TestCategory("IniLib")]
        [TestMethod]
        public void IniLib_Merge2()
        {
            IniLib_Merge2_1();
            IniLib_Merge2_2();
            IniLib_Merge2_3();
            IniLib_Merge2_4();
        }

        public void IniLib_Merge2_1()
        {
            string tempFile = Path.GetTempFileName();
            string destFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile, Encoding.UTF8);
                TestHelper.WriteTextBOM(destFile, Encoding.UTF8);

                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section1]");
                    w.WriteLine("01=A");
                    w.WriteLine("02=B");
                }

                Assert.IsTrue(Ini.Merge(tempFile, destFile));

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

        public void IniLib_Merge2_2()
        {
            string tempFile = Path.GetTempFileName();
            string destFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile, Encoding.UTF8);
                TestHelper.WriteTextBOM(destFile, Encoding.Unicode);

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

                Assert.IsTrue(Ini.Merge(tempFile, destFile));

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

        public void IniLib_Merge2_3()
        {
            string tempFile = Path.GetTempFileName();
            string destFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile, Encoding.Unicode);
                TestHelper.WriteTextBOM(destFile, Encoding.UTF8);

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

                Assert.IsTrue(Ini.Merge(tempFile, destFile));

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

        public void IniLib_Merge2_4()
        {
            string tempFile = Path.GetTempFileName();
            string destFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile, Encoding.Unicode);
                TestHelper.WriteTextBOM(destFile, Encoding.BigEndianUnicode);

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

                Assert.IsTrue(Ini.Merge(tempFile, destFile));

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
        [TestCategory("IniLib")]
        [TestMethod]
        public void IniLib_Merge3()
        {
            IniLib_Merge3_1();
            IniLib_Merge3_2();
            IniLib_Merge3_3();
            IniLib_Merge3_4();
            IniLib_Merge3_5();
            IniLib_Merge3_6();
        }

        public void IniLib_Merge3_1()
        {
            string tempFile1 = Path.GetTempFileName();
            string tempFile2 = Path.GetTempFileName();
            string destFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(destFile, Encoding.UTF8);

                Assert.IsTrue(Ini.Merge(tempFile1, tempFile2, destFile));

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

        public void IniLib_Merge3_2()
        {
            string tempFile1 = Path.GetTempFileName();
            string tempFile2 = Path.GetTempFileName();
            string destFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile1, Encoding.UTF8);
                TestHelper.WriteTextBOM(destFile, Encoding.UTF8);

                using (StreamWriter w = new StreamWriter(tempFile1, false, Encoding.UTF8))
                {
                    w.WriteLine("[Section1]");
                    w.WriteLine("01=A");
                    w.WriteLine("02=B");
                }

                Assert.IsTrue(Ini.Merge(tempFile1, tempFile2, destFile));

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

        public void IniLib_Merge3_3()
        {
            string tempFile1 = Path.GetTempFileName();
            string tempFile2 = Path.GetTempFileName();
            string destFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile1, Encoding.UTF8);
                TestHelper.WriteTextBOM(destFile, Encoding.UTF8);

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

                Assert.IsTrue(Ini.Merge(tempFile1, tempFile2, destFile));

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

        public void IniLib_Merge3_4()
        {
            string tempFile1 = Path.GetTempFileName();
            string tempFile2 = Path.GetTempFileName();
            string destFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile1, Encoding.UTF8);
                TestHelper.WriteTextBOM(destFile, Encoding.UTF8);

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

                Assert.IsTrue(Ini.Merge(tempFile1, tempFile2, destFile));

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

        public void IniLib_Merge3_5()
        {
            string tempFile1 = Path.GetTempFileName();
            string tempFile2 = Path.GetTempFileName();
            string destFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile1, Encoding.UTF8);
                TestHelper.WriteTextBOM(tempFile2, Encoding.UTF8);
                TestHelper.WriteTextBOM(destFile, Encoding.UTF8);

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

                Assert.IsTrue(Ini.Merge(tempFile1, tempFile2, destFile));

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

        public void IniLib_Merge3_6()
        {
            string tempFile1 = Path.GetTempFileName();
            string tempFile2 = Path.GetTempFileName();
            string destFile = Path.GetTempFileName();
            try
            {
                TestHelper.WriteTextBOM(tempFile1, Encoding.UTF8);
                TestHelper.WriteTextBOM(destFile, Encoding.UTF8);

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

                Assert.IsTrue(Ini.Merge(tempFile1, tempFile2, destFile));

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
