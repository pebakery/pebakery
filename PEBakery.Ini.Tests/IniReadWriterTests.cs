/*
    Copyright (C) 2017-2022 Hajin Jang
 
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
using PEBakery.Helper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PEBakery.Ini.Tests
{
    [TestClass]
    [TestCategory("PEBakery.Ini")]
    public class IniReadWriterTests
    {
        #region Encodings
        private static readonly Encoding[] Encodings =
        {
            // Wanted to include ANSI/DBCS encodings, but they does not support multilingial text.
            // EncodingHelper.DefaultAnsi
            // UTF-8 wo BOM text without any non-ASCII char is detected as DefaultAnsi.
            // new UTF8Encoding(false),
            Encoding.Unicode,
            Encoding.BigEndianUnicode,
            Encoding.UTF8,
        };
        #endregion

        #region ReadKey
        [TestMethod]
        public void ReadKey()
        {
            ReadKey_1();
            ReadKey_2();
            ReadKey_3();
            ReadKey_DoubleQuote();
        }

        public static void ReadKey_1()
        {
            string tempFile = FileHelper.GetTempFile();
            try
            {
                Assert.IsNull(IniReadWriter.ReadKey(tempFile, "Section", "Key"));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        public static void ReadKey_2()
        {
            foreach (Encoding srcEnc in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, srcEnc);
                    using (StreamWriter w = new StreamWriter(tempFile, false, srcEnc))
                    {
                        w.WriteLine("[Section]");
                        w.WriteLine("Key=Value");
                    }

                    string? val = IniReadWriter.ReadKey(tempFile, "Section", "Key");
                    Assert.IsNotNull(val);
                    Assert.IsTrue(val.Equals("Value", StringComparison.Ordinal));

                    val = IniReadWriter.ReadKey(tempFile, "Section", "key");
                    Assert.IsNotNull(val);
                    Assert.IsTrue(val.Equals("Value", StringComparison.Ordinal));

                    val = IniReadWriter.ReadKey(tempFile, "section", "Key");
                    Assert.IsNotNull(val);
                    Assert.IsTrue(val.Equals("Value", StringComparison.Ordinal));

                    val = IniReadWriter.ReadKey(tempFile, "section", "key");
                    Assert.IsNotNull(val);
                    Assert.IsTrue(val.Equals("Value", StringComparison.Ordinal));

                    val = IniReadWriter.ReadKey(tempFile, "Section", "Key");
                    Assert.IsNotNull(val);
                    Assert.IsFalse(val.Equals("value", StringComparison.Ordinal));

                    val = IniReadWriter.ReadKey(tempFile, "SectionNone", "Key");
                    Assert.IsNull(val);

                    val = IniReadWriter.ReadKey(tempFile, "Section", "KeyNone");
                    Assert.IsNull(val);

                    Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));
                }
                finally
                {
                    File.Delete(tempFile);
                }
            }
        }

        public static void ReadKey_3()
        {
            foreach (Encoding srcEnc in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, srcEnc);
                    using (StreamWriter w = new StreamWriter(tempFile, false, srcEnc))
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
                    }

                    string? val = IniReadWriter.ReadKey(tempFile, "Section1", "1");
                    Assert.IsNotNull(val);
                    Assert.IsTrue(val.Equals("A", StringComparison.Ordinal));

                    val = IniReadWriter.ReadKey(tempFile, "Section1", "2");
                    Assert.IsNotNull(val);
                    Assert.IsTrue(val.Equals("B", StringComparison.Ordinal));

                    val = IniReadWriter.ReadKey(tempFile, "section1", "3");
                    Assert.IsNotNull(val);
                    Assert.IsTrue(val.Equals("C", StringComparison.Ordinal));

                    val = IniReadWriter.ReadKey(tempFile, "Section2", "4");
                    Assert.IsNotNull(val);
                    Assert.IsTrue(val.Equals("D", StringComparison.Ordinal));

                    val = IniReadWriter.ReadKey(tempFile, "Section2", "5");
                    Assert.IsNotNull(val);
                    Assert.IsTrue(val.Equals("E", StringComparison.Ordinal));

                    val = IniReadWriter.ReadKey(tempFile, "section3", "6");
                    Assert.IsNotNull(val);
                    Assert.IsTrue(val.Equals("F", StringComparison.Ordinal));

                    val = IniReadWriter.ReadKey(tempFile, "section3", "7");
                    Assert.IsNotNull(val);
                    Assert.IsTrue(val.Equals("G", StringComparison.Ordinal));

                    val = IniReadWriter.ReadKey(tempFile, "section3", "8");
                    Assert.IsNotNull(val);
                    Assert.IsTrue(val.Equals("H", StringComparison.Ordinal));

                    val = IniReadWriter.ReadKey(tempFile, "Section999", "Key");
                    Assert.IsNull(val);

                    val = IniReadWriter.ReadKey(tempFile, "Section", "999");
                    Assert.IsNull(val);

                    Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));
                }
                finally
                {
                    File.Delete(tempFile);
                }
            }
        }

        public static void ReadKey_DoubleQuote()
        {
            foreach (Encoding srcEnc in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, srcEnc);
                    using (StreamWriter w = new StreamWriter(tempFile, false, srcEnc))
                    {
                        w.WriteLine("[Section1]");
                        w.WriteLine("CUR_DIR       = \"Cursors\\Material Design Cursors\"");
                        w.WriteLine("1     = \"ABC\\DEF\"");
                        w.WriteLine("2=\"XY Z\"");
                    }

                    string? val = IniReadWriter.ReadKey(tempFile, "Section1", "1");
                    Assert.IsNotNull(val);
                    Assert.IsTrue(val.Equals("\"ABC\\DEF\"", StringComparison.Ordinal));

                    val = IniReadWriter.ReadKey(tempFile, "Section1", "2");
                    Assert.IsNotNull(val);
                    Assert.IsTrue(val.Equals("\"XY Z\"", StringComparison.Ordinal));

                    val = IniReadWriter.ReadKey(tempFile, "Section1", "CUR_DIR");
                    Assert.IsNotNull(val);
                    Assert.IsTrue(val.Equals("\"Cursors\\Material Design Cursors\"", StringComparison.Ordinal));

                    val = IniReadWriter.ReadKey(tempFile, "Section999", "2");
                    Assert.IsNull(val);

                    Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));
                }
                finally
                {
                    File.Delete(tempFile);
                }
            }
        }
        #endregion

        #region ReadKeys
        [TestMethod]
        public void ReadKeys()
        {
            ReadKeys_1();
        }

        public static void ReadKeys_1()
        {
            foreach (Encoding srcEnc in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, srcEnc);
                    using (StreamWriter w = new StreamWriter(tempFile, false, srcEnc))
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

                    IniKey iniKey = keys[0];
                    Assert.IsTrue(iniKey.Value != null && iniKey.Value.Equals("C", StringComparison.Ordinal));
                    iniKey = keys[1];
                    Assert.IsTrue(iniKey.Value != null && iniKey.Value.Equals("H", StringComparison.Ordinal));
                    iniKey = keys[2];
                    Assert.IsTrue(iniKey.Value != null && iniKey.Value.Equals("E", StringComparison.Ordinal));
                    iniKey = keys[3];
                    Assert.IsTrue(iniKey.Value != null && iniKey.Value.Equals("F", StringComparison.Ordinal));
                    Assert.IsNull(keys[4].Value);
                    Assert.IsNull(keys[5].Value);
                    iniKey = keys[6];
                    Assert.IsTrue(iniKey.Value != null && iniKey.Value.Equals("B", StringComparison.Ordinal));
                    iniKey = keys[7];
                    Assert.IsTrue(iniKey.Value != null && iniKey.Value.Equals("G", StringComparison.Ordinal));
                    iniKey = keys[8];
                    Assert.IsTrue(iniKey.Value != null && iniKey.Value.Equals("A", StringComparison.Ordinal));
                    iniKey = keys[9];
                    Assert.IsTrue(iniKey.Value != null && iniKey.Value.Equals("D", StringComparison.Ordinal));

                    Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));
                }
                finally
                {
                    File.Delete(tempFile);
                }
            }
        }
        #endregion

        #region WriteKey
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

        public static void WriteKey_1()
        {
            string tempFile = FileHelper.GetTempFile();
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

        public static void WriteKey_2()
        {
            foreach (Encoding srcEnc in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, srcEnc);
                    using (StreamWriter w = new StreamWriter(tempFile, false, srcEnc))
                    {
                        w.WriteLine("[Section]");
                        w.WriteLine("Key=A");
                        w.Close();
                    }

                    Assert.IsTrue(IniReadWriter.WriteKey(tempFile, "Section", "Key", "B"));

                    string read;
                    Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                    using (StreamReader r = new StreamReader(tempFile, destEnc))
                    {
                        read = r.ReadToEnd();
                    }

                    StringBuilder b = new StringBuilder();
                    b.AppendLine("[Section]");
                    b.AppendLine("Key=B");
                    b.AppendLine();
                    string comp = b.ToString();

                    Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));

                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));
                }
                finally
                {
                    File.Delete(tempFile);
                }
            }
        }

        public static void WriteKey_3()
        { // Found while testing EncodedFile.EncodeFile()
            foreach (Encoding srcEnc in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, srcEnc);
                    using (StreamWriter w = new StreamWriter(tempFile, false, srcEnc))
                    {
                        w.WriteLine("[Section]");
                        w.WriteLine("Sect2");
                        w.Close();
                    }

                    Assert.IsTrue(IniReadWriter.WriteKey(tempFile, "Section2", "Key", "B"));

                    string read;
                    Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                    using (StreamReader r = new StreamReader(tempFile, destEnc))
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

                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));
                }
                finally
                {
                    File.Delete(tempFile);
                }
            }
        }

        public static void WriteKey_4()
        { // Found while testing EncodedFile.EncodeFile()
            foreach (Encoding srcEnc in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, srcEnc);
                    using (StreamWriter w = new StreamWriter(tempFile, false, srcEnc))
                    {
                        w.WriteLine("[Section]");
                        w.WriteLine("Section2");
                        w.Close();
                    }

                    Assert.IsTrue(IniReadWriter.WriteKey(tempFile, "Section2", "Key", "B"));

                    string read;
                    Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                    using (StreamReader r = new StreamReader(tempFile, destEnc))
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

                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));
                }
                finally
                {
                    File.Delete(tempFile);
                }
            }
        }

        public static void WriteKey_5()
        {
            string tempFile = FileHelper.GetTempFile();
            try
            {
                Assert.IsTrue(IniReadWriter.WriteKey(tempFile, "Section", "Key", "Value"));

                string read;
                using (StreamReader r = new StreamReader(tempFile, Encoding.UTF8))
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

        public static void WriteKey_6()
        { // https://github.com/pebakery/pebakery/issues/57
            foreach (Encoding srcEnc in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, srcEnc);
                    using (StreamWriter w = new StreamWriter(tempFile, false, srcEnc))
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
                    Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));
                    using (StreamReader r = new StreamReader(tempFile, destEnc))
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
        }
        #endregion

        #region WriteKeys
        [TestMethod]
        public void WriteKeys()
        {
            WriteKeys_1();
            WriteKeys_2();
        }

        public static void WriteKeys_1()
        {
            foreach (Encoding srcEnc in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, srcEnc);

                    IniKey[] keys = new IniKey[3];
                    keys[0] = new IniKey("Section2", "20", "English");
                    keys[1] = new IniKey("Section1", "10", "한국어");
                    keys[2] = new IniKey("Section3", "30", "Français");

                    Assert.IsTrue(IniReadWriter.WriteKeys(tempFile, keys));

                    string read;
                    Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));
                    using (StreamReader r = new StreamReader(tempFile, destEnc))
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
        }

        public static void WriteKeys_2()
        {
            foreach (Encoding srcEnc in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, srcEnc);
                    using (StreamWriter w = new StreamWriter(tempFile, false, srcEnc))
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
                    Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));
                    using (StreamReader r = new StreamReader(tempFile, destEnc))
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
        }
        #endregion

        #region WriteKeyCompact
        [TestMethod]
        public void WriteKeyCompact()
        {
            IniKey iniKey;
            bool TestWriteKey(string tempFile) => IniReadWriter.WriteKeyCompact(tempFile, iniKey);

            StringBuilder b = new StringBuilder();
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
            string src = b.ToString();

            b.Clear();
            b.AppendLine("[Section1]");
            b.AppendLine("A=5");
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
            iniKey = new IniKey("Section1", "A", "5");
            WriteTemplate(src, b.ToString(), TestWriteKey);

            b.Clear();
            b.AppendLine("[Section1]");
            b.AppendLine("A=1");
            b.AppendLine("B=2");
            b.AppendLine("C=3");
            b.AppendLine("D=4");
            b.AppendLine("Z=9");
            b.AppendLine();
            b.AppendLine("[Section2]");
            b.AppendLine("ㄱ=甲");
            b.AppendLine("ㄴ=乙");
            b.AppendLine("ㄷ=丙");
            b.AppendLine("ㄹ=丁");
            b.AppendLine();
            iniKey = new IniKey("Section1", "Z", "9");
            WriteTemplate(src, b.ToString(), TestWriteKey);

            b.Clear();
            b.AppendLine("[Section1]");
            b.AppendLine("A=1");
            b.AppendLine("B=2");
            b.AppendLine("C=3");
            b.AppendLine("D=4");
            b.AppendLine();
            b.AppendLine("[Section2]");
            b.AppendLine("ㄱ=戊");
            b.AppendLine("ㄴ=乙");
            b.AppendLine("ㄷ=丙");
            b.AppendLine("ㄹ=丁");
            b.AppendLine();
            iniKey = new IniKey("Section2", "ㄱ", "戊");
            WriteTemplate(src, b.ToString(), TestWriteKey);

            b.Clear();
            b.AppendLine("[Section1]");
            b.AppendLine("A=1");
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
            iniKey = new IniKey("Section2", "ㅁ", "戊");
            WriteTemplate(src, b.ToString(), TestWriteKey);

            b.Clear();
            b.AppendLine("[Section1]");
            b.AppendLine("A=1");
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
            b.AppendLine("[Section3]");
            b.AppendLine("One=일");
            iniKey = new IniKey("Section3", "One", "일");
            WriteTemplate(src, b.ToString(), TestWriteKey);
        }
        #endregion

        #region WriteRawLine
        [TestMethod]
        public void WriteRawLine()
        {
            WriteRawLine_1();
            WriteRawLine_2();
            WriteRawLine_3();
            WriteRawLine_4();
            WriteRawLine_5();
        }

        public static void WriteRawLine_1()
        {
            foreach (Encoding srcEnc in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, srcEnc);

                    Assert.IsTrue(IniReadWriter.WriteRawLine(tempFile, "Section", "RawLine"));

                    Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));

                    string read;
                    using (StreamReader r = new StreamReader(tempFile, destEnc))
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
        }

        public static void WriteRawLine_2()
        {
            foreach (Encoding srcEnc in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, srcEnc);
                    using (StreamWriter w = new StreamWriter(tempFile, false, srcEnc))
                    {
                        w.WriteLine("[Section]");
                        w.WriteLine("1=A");
                        w.Close();
                    }

                    Assert.IsTrue(IniReadWriter.WriteRawLine(tempFile, "Section", "LineAppend", true));

                    Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));

                    string read;
                    using (StreamReader r = new StreamReader(tempFile, destEnc))
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
        }

        public static void WriteRawLine_3()
        {
            foreach (Encoding srcEnc in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, srcEnc);
                    using (StreamWriter w = new StreamWriter(tempFile, false, srcEnc))
                    {
                        w.WriteLine("[Section]");
                        w.WriteLine("1=A");
                        w.Close();
                    }

                    Assert.IsTrue(IniReadWriter.WriteRawLine(tempFile, "Section", "LinePrepend", false));

                    Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));

                    string read;
                    using (StreamReader r = new StreamReader(tempFile, destEnc))
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
        }

        public static void WriteRawLine_4()
        { // Found while testing EncodedFile.EncodeFile()
            foreach (Encoding srcEnc in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, srcEnc);
                    using (StreamWriter w = new StreamWriter(tempFile, false, srcEnc))
                    {
                        w.WriteLine("[Section]");
                        w.WriteLine("Sect2");
                        w.Close();
                    }

                    Assert.IsTrue(IniReadWriter.WriteRawLine(tempFile, "Section2", "Key"));

                    Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));

                    string read;
                    using (StreamReader r = new StreamReader(tempFile, destEnc))
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
        }

        public static void WriteRawLine_5()
        { // Found while testing EncodedFile.EncodeFile()
            foreach (Encoding srcEnc in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, srcEnc);
                    using (StreamWriter w = new StreamWriter(tempFile, false, srcEnc))
                    {
                        w.WriteLine("[Section]");
                        w.WriteLine("Section2");
                    }

                    Assert.IsTrue(IniReadWriter.WriteRawLine(tempFile, "Section2", "Key"));

                    Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));

                    string read;
                    using (StreamReader r = new StreamReader(tempFile, destEnc))
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
        }
        #endregion

        #region WriteRawLines
        [TestMethod]
        public void WriteRawLines()
        {
            WriteRawLines_1();
            WriteRawLines_2();
        }

        public static void WriteRawLines_1()
        {
            foreach (Encoding srcEnc in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, srcEnc);
                    using (StreamWriter w = new StreamWriter(tempFile, false, srcEnc))
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
                    }

                    IniKey[] keys = new IniKey[5];
                    keys[0] = new IniKey("Section2", "영어");
                    keys[1] = new IniKey("Section1", "한중일 (CJK)");
                    keys[2] = new IniKey("Section3", "프랑스어");
                    keys[3] = new IniKey("Section4", "עברית");
                    keys[4] = new IniKey("Section4", "العربية");

                    Assert.IsTrue(IniReadWriter.WriteRawLines(tempFile, keys, false));

                    Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));

                    string read;
                    using (StreamReader r = new StreamReader(tempFile, destEnc, false))
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
        }

        public static void WriteRawLines_2()
        {
            foreach (Encoding srcEnc in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, srcEnc);
                    using (StreamWriter w = new StreamWriter(tempFile, false, srcEnc))
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

                    Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));

                    string read;
                    using (StreamReader r = new StreamReader(tempFile, destEnc))
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
        }
        #endregion

        #region RenameKey
        [TestMethod]
        public void RenameKey()
        {
            RenameKey_1();
            RenameKey_2();
            RenameKey_3();
        }

        public static void RenameKey_1()
        {
            foreach (Encoding srcEnc in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, srcEnc);
                    using (StreamWriter w = new StreamWriter(tempFile, false, srcEnc))
                    {
                        w.WriteLine("[Section]");
                        w.WriteLine("1=A");
                        w.WriteLine("2=B");
                        w.WriteLine("3=C");
                        w.WriteLine("4=D");
                    }

                    Assert.IsTrue(IniReadWriter.RenameKey(tempFile, "Section", "2", "0"));

                    Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));

                    string read;
                    using (StreamReader r = new StreamReader(tempFile, destEnc))
                    {
                        read = r.ReadToEnd();
                    }

                    StringBuilder b = new StringBuilder();
                    b.AppendLine("[Section]");
                    b.AppendLine("1=A");
                    b.AppendLine("0=B");
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
        }

        public static void RenameKey_2()
        {
            foreach (Encoding srcEnc in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, srcEnc);
                    using (StreamWriter w = new StreamWriter(tempFile, false, srcEnc))
                    {
                        w.WriteLine("[Section]");
                        w.WriteLine("1=A");
                        w.WriteLine("2=B");
                        w.WriteLine("3=C");
                        w.WriteLine("4=D");
                    }

                    // Induce Error
                    Assert.IsFalse(IniReadWriter.RenameKey(tempFile, "Section", "5", "0"));

                    Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));

                    string read;
                    using (StreamReader r = new StreamReader(tempFile, destEnc))
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
        }

        public static void RenameKey_3()
        {
            foreach (Encoding srcEnc in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, srcEnc);
                    using (StreamWriter w = new StreamWriter(tempFile, false, srcEnc))
                    {
                        w.WriteLine("[Section]");
                        w.WriteLine("1=A");
                        w.WriteLine("2=B");
                        w.WriteLine("3=C");
                        w.WriteLine("4=D");
                    }

                    Assert.IsTrue(IniReadWriter.RenameKey(tempFile, "Section", "1", "0"));
                    Assert.IsTrue(IniReadWriter.RenameKey(tempFile, "Section", "0", "9"));

                    Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));

                    string read;
                    using (StreamReader r = new StreamReader(tempFile, destEnc))
                    {
                        read = r.ReadToEnd();
                    }

                    StringBuilder b = new StringBuilder();
                    b.AppendLine("[Section]");
                    b.AppendLine("9=A");
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
        }
        #endregion

        #region RenameKeys
        [TestMethod]
        public void RenameKeys()
        {
            RenameKeys_1();
        }

        public static void RenameKeys_1()
        {
            foreach (Encoding srcEnc in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, srcEnc);
                    using (StreamWriter w = new StreamWriter(tempFile, false, srcEnc))
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
                        new IniKey("Section1", "02", "99"),
                        new IniKey("Section3", "21", "99"),
                        new IniKey("Section2", "10", "99"),
                    };

                    bool[] result = IniReadWriter.RenameKeys(tempFile, keys);
                    Assert.IsTrue(result.All(x => x));

                    Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));

                    string read;
                    using (StreamReader r = new StreamReader(tempFile, destEnc))
                    {
                        read = r.ReadToEnd();
                    }

                    StringBuilder b = new StringBuilder();
                    b.AppendLine("[Section1]");
                    b.AppendLine("00=A");
                    b.AppendLine("01=B");
                    b.AppendLine("99=C");
                    b.AppendLine();
                    b.AppendLine("[Section2]");
                    b.AppendLine("99=한");
                    b.AppendLine("11=국");
                    b.AppendLine("[Section3]");
                    b.AppendLine("20=韓");
                    b.AppendLine("99=國");

                    string comp = b.ToString();
                    Assert.IsTrue(read.Equals(comp, StringComparison.Ordinal));
                }
                finally
                {
                    File.Delete(tempFile);
                }
            }
        }
        #endregion

        #region DeleteKey
        [TestMethod]
        public void DeleteKey()
        {
            DeleteKey_1();
            DeleteKey_2();
            DeleteKey_3();
        }

        public static void DeleteKey_1()
        {
            foreach (Encoding srcEnc in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, srcEnc);
                    using (StreamWriter w = new StreamWriter(tempFile, false, srcEnc))
                    {
                        w.WriteLine("[Section]");
                        w.WriteLine("1=A");
                        w.WriteLine("2=B");
                        w.WriteLine("3=C");
                        w.WriteLine("4=D");
                    }

                    Assert.IsTrue(IniReadWriter.DeleteKey(tempFile, "Section", "2"));

                    Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));

                    string read;
                    using (StreamReader r = new StreamReader(tempFile, destEnc))
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
        }

        public static void DeleteKey_2()
        {
            foreach (Encoding srcEnc in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, srcEnc);
                    using (StreamWriter w = new StreamWriter(tempFile, false, srcEnc))
                    {
                        w.WriteLine("[Section]");
                        w.WriteLine("1=A");
                        w.WriteLine("2=B");
                        w.WriteLine("3=C");
                        w.WriteLine("4=D");
                    }

                    // Induce Error
                    Assert.IsFalse(IniReadWriter.DeleteKey(tempFile, "Section", "5"));

                    Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));

                    string read;
                    using (StreamReader r = new StreamReader(tempFile, destEnc))
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
        }

        public static void DeleteKey_3()
        {
            foreach (Encoding srcEnc in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, srcEnc);
                    using (StreamWriter w = new StreamWriter(tempFile, false, srcEnc))
                    {
                        w.WriteLine("[Section]");
                        w.WriteLine("1=A");
                        w.WriteLine("2=B");
                        w.WriteLine("3=C");
                        w.WriteLine("4=D");
                    }

                    Assert.IsTrue(IniReadWriter.DeleteKey(tempFile, "Section", "2"));
                    Assert.IsTrue(IniReadWriter.DeleteKey(tempFile, "Section", "4"));

                    Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));

                    string read;
                    using (StreamReader r = new StreamReader(tempFile, destEnc))
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
        }
        #endregion

        #region DeleteKeys
        [TestMethod]
        public void DeleteKeys()
        {
            DeleteKeys_1();
        }

        public static void DeleteKeys_1()
        {
            foreach (Encoding srcEnc in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, srcEnc);
                    using (StreamWriter w = new StreamWriter(tempFile, false, srcEnc))
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

                    Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));

                    string read;
                    using (StreamReader r = new StreamReader(tempFile, destEnc))
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
        }
        #endregion

        #region ReadSection
        [TestMethod]
        public void ReadSection()
        {
            ReadSection_1();
            ReadSection_2();
            ReadSection_3();
        }

        public static void ReadSection_1()
        {
            foreach (Encoding encoding in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, encoding);
                    using (StreamWriter w = new StreamWriter(tempFile, false, encoding))
                    {
                        w.WriteLine("[Section]");
                        w.WriteLine("1=A");
                        w.WriteLine("2=B");
                    }

                    IniKey[]? keys = IniReadWriter.ReadSection(tempFile, "Section");
                    Assert.IsNotNull(keys);
                    Assert.AreEqual(2, keys.Length);

                    IniKey iniKey = keys[0];
                    Assert.IsTrue(iniKey.Key != null && iniKey.Key.Equals("1", StringComparison.Ordinal));
                    Assert.IsTrue(iniKey.Value != null && iniKey.Value.Equals("A", StringComparison.Ordinal));
                    iniKey = keys[1];
                    Assert.IsTrue(iniKey.Key != null && iniKey.Key.Equals("2", StringComparison.Ordinal));
                    Assert.IsTrue(iniKey.Value != null && iniKey.Value.Equals("B", StringComparison.Ordinal));
                }
                finally
                {
                    File.Delete(tempFile);
                }
            }
        }

        public static void ReadSection_2()
        {
            foreach (Encoding encoding in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, encoding);
                    using (StreamWriter w = new StreamWriter(tempFile, false, encoding))
                    {
                        w.WriteLine("[Section]");
                        w.WriteLine("1=A");
                        w.WriteLine("2=B");
                    }

                    IniKey[]? keys = IniReadWriter.ReadSection(tempFile, "Dummy");
                    Assert.IsTrue(keys == null);
                }
                finally
                {
                    File.Delete(tempFile);
                }
            }
        }

        public static void ReadSection_3()
        {
            foreach (Encoding encoding in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, encoding);
                    using (StreamWriter w = new StreamWriter(tempFile, false, encoding))
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

                    IniKey[]? keys = IniReadWriter.ReadSection(tempFile, new IniKey("Section1"));
                    Assert.IsNotNull(keys);
                    Assert.AreEqual(3, keys.Length);

                    IniKey iniKey = keys[0];
                    Assert.IsTrue(iniKey.Key != null && iniKey.Key.Equals("00", StringComparison.Ordinal));
                    Assert.IsTrue(iniKey.Value != null && iniKey.Value.Equals("A", StringComparison.Ordinal));
                    iniKey = keys[1];
                    Assert.IsTrue(iniKey.Key != null && iniKey.Key.Equals("01", StringComparison.Ordinal));
                    Assert.IsTrue(iniKey.Value != null && iniKey.Value.Equals("B", StringComparison.Ordinal));
                    iniKey = keys[2];
                    Assert.IsTrue(iniKey.Key != null && iniKey.Key.Equals("02", StringComparison.Ordinal));
                    Assert.IsTrue(iniKey.Value != null && iniKey.Value.Equals("C", StringComparison.Ordinal));

                    keys = IniReadWriter.ReadSection(tempFile, "Section2");
                    Assert.IsNotNull(keys);
                    Assert.AreEqual(2, keys.Length);
                    iniKey = keys[0];
                    Assert.IsTrue(iniKey.Key != null && iniKey.Key.Equals("10", StringComparison.Ordinal));
                    Assert.IsTrue(iniKey.Value != null && iniKey.Value.Equals("한", StringComparison.Ordinal));
                    iniKey = keys[1];
                    Assert.IsTrue(iniKey.Key != null && iniKey.Key.Equals("11", StringComparison.Ordinal));
                    Assert.IsTrue(iniKey.Value != null && iniKey.Value.Equals("국", StringComparison.Ordinal));

                    keys = IniReadWriter.ReadSection(tempFile, "Section3");
                    Assert.IsNotNull(keys);
                    Assert.AreEqual(2, keys.Length);
                    iniKey = keys[0];
                    Assert.IsTrue(iniKey.Key != null && iniKey.Key.Equals("20", StringComparison.Ordinal));
                    Assert.IsTrue(iniKey.Value != null && iniKey.Value.Equals("韓", StringComparison.Ordinal));
                    iniKey = keys[1];
                    Assert.IsTrue(iniKey.Key != null && iniKey.Key.Equals("21", StringComparison.Ordinal));
                    Assert.IsTrue(iniKey.Value != null && iniKey.Value.Equals("國", StringComparison.Ordinal));
                }
                finally
                {
                    File.Delete(tempFile);
                }
            }
        }
        #endregion

        #region ReadSections
        [TestMethod]
        public void ReadSections()
        {
            ReadSections_1();
            ReadSections_2();
            ReadSections_3();
        }

        public static void ReadSections_1()
        {
            foreach (Encoding encoding in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, encoding);
                    using (StreamWriter w = new StreamWriter(tempFile, false, encoding))
                    {
                        w.WriteLine("[Section]");
                        w.WriteLine("1=A");
                        w.WriteLine("2=B");
                    }

                    Dictionary<string, IniKey[]?> keyDict = IniReadWriter.ReadSections(tempFile, new IniKey[] { new IniKey("Section") });
                    IniKey[]? keys = keyDict["Section"];
                    Assert.IsNotNull(keys);
                    Assert.AreEqual(2, keys.Length);

                    IniKey iniKey = keys[0];
                    Assert.IsTrue(iniKey.Key != null && iniKey.Key.Equals("1", StringComparison.Ordinal));
                    Assert.IsTrue(iniKey.Value != null && iniKey.Value.Equals("A", StringComparison.Ordinal));
                    iniKey = keys[1];
                    Assert.IsTrue(iniKey.Key != null && iniKey.Key.Equals("2", StringComparison.Ordinal));
                    Assert.IsTrue(iniKey.Value != null && iniKey.Value.Equals("B", StringComparison.Ordinal));
                }
                finally
                {
                    File.Delete(tempFile);
                }
            }
        }

        public static void ReadSections_2()
        {
            foreach (Encoding encoding in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, encoding);
                    using (StreamWriter w = new StreamWriter(tempFile, false, encoding))
                    {
                        w.WriteLine("[Section]");
                        w.WriteLine("1=A");
                        w.WriteLine("2=B");
                    }

                    Dictionary<string, IniKey[]?> keyDict = IniReadWriter.ReadSections(tempFile, new string[] { "Section", "Dummy" });
                    Assert.IsTrue(keyDict.ContainsKey("Section"));
                    IniKey[]? keys = keyDict["Section"];
                    Assert.IsNotNull(keys);

                    IniKey iniKey = keys[0];
                    Assert.IsTrue(iniKey.Key != null && iniKey.Key.Equals("1", StringComparison.Ordinal));
                    Assert.IsTrue(iniKey.Value != null && iniKey.Value.Equals("A", StringComparison.Ordinal));
                    iniKey = keys[1];
                    Assert.IsTrue(iniKey.Key != null && iniKey.Key.Equals("2", StringComparison.Ordinal));
                    Assert.IsTrue(iniKey.Value != null && iniKey.Value.Equals("B", StringComparison.Ordinal));

                    keys = keyDict["Dummy"];
                    Assert.IsTrue(keys == null);
                }
                finally
                {
                    File.Delete(tempFile);
                }
            }
        }

        public static void ReadSections_3()
        {
            foreach (Encoding encoding in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, encoding);
                    using (StreamWriter w = new StreamWriter(tempFile, false, encoding))
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

                    Dictionary<string, IniKey[]?> keyDict = IniReadWriter.ReadSections(tempFile, new string[] { "Section1", "Section2", "Section3" });

                    Assert.IsTrue(keyDict.ContainsKey("Section1"));
                    IniKey[]? keys = keyDict["Section1"];
                    Assert.IsNotNull(keys);
                    IniKey iniKey = keys[0];
                    Assert.IsTrue(iniKey.Key != null && iniKey.Key.Equals("00", StringComparison.Ordinal));
                    Assert.IsTrue(iniKey.Value != null && iniKey.Value.Equals("A", StringComparison.Ordinal));
                    iniKey = keys[1];
                    Assert.IsTrue(iniKey.Key != null && iniKey.Key.Equals("01", StringComparison.Ordinal));
                    Assert.IsTrue(iniKey.Value != null && iniKey.Value.Equals("B", StringComparison.Ordinal));
                    iniKey = keys[2];
                    Assert.IsTrue(iniKey.Key != null && iniKey.Key.Equals("02", StringComparison.Ordinal));
                    Assert.IsTrue(iniKey.Value != null && iniKey.Value.Equals("C", StringComparison.Ordinal));

                    Assert.IsTrue(keyDict.ContainsKey("Section2"));
                    keys = keyDict["Section2"];
                    Assert.IsNotNull(keys);
                    iniKey = keys[0];
                    Assert.IsTrue(iniKey.Key != null && iniKey.Key.Equals("10", StringComparison.Ordinal));
                    Assert.IsTrue(iniKey.Value != null && iniKey.Value.Equals("한", StringComparison.Ordinal));
                    iniKey = keys[1];
                    Assert.IsTrue(iniKey.Key != null && iniKey.Key.Equals("11", StringComparison.Ordinal));
                    Assert.IsTrue(iniKey.Value != null && iniKey.Value.Equals("국", StringComparison.Ordinal));

                    Assert.IsTrue(keyDict.ContainsKey("Section3"));
                    keys = keyDict["Section3"];
                    Assert.IsNotNull(keys);
                    iniKey = keys[0];
                    Assert.IsTrue(iniKey.Key != null && iniKey.Key.Equals("20", StringComparison.Ordinal));
                    Assert.IsTrue(iniKey.Value != null && iniKey.Value.Equals("韓", StringComparison.Ordinal));
                    iniKey = keys[1];
                    Assert.IsTrue(iniKey.Key != null && iniKey.Key.Equals("21", StringComparison.Ordinal));
                    Assert.IsTrue(iniKey.Value != null && iniKey.Value.Equals("國", StringComparison.Ordinal));
                }
                finally
                {
                    File.Delete(tempFile);
                }
            }
        }
        #endregion

        #region AddSection
        [TestMethod]
        public void AddSection()
        {
            AddSection_1();
            AddSection_2();
            AddSection_3();
        }

        public static void AddSection_1()
        {
            foreach (Encoding srcEnc in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, srcEnc);
                    Assert.IsTrue(IniReadWriter.AddSection(tempFile, "Section"));

                    Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));

                    string read;
                    using (StreamReader r = new StreamReader(tempFile, destEnc))
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
        }

        public static void AddSection_2()
        {
            foreach (Encoding srcEnc in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, srcEnc);
                    using (StreamWriter w = new StreamWriter(tempFile, false, srcEnc))
                    {
                        w.WriteLine("[Section]");
                        w.WriteLine("1=A");
                        w.WriteLine("2=B");
                    }

                    Assert.IsTrue(IniReadWriter.AddSection(tempFile, "Section"));

                    Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));

                    string read;
                    using (StreamReader r = new StreamReader(tempFile, destEnc))
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
        }

        public static void AddSection_3()
        {
            foreach (Encoding srcEnc in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, srcEnc);
                    using (StreamWriter w = new StreamWriter(tempFile, false, srcEnc))
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

                    Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));

                    string read;
                    using (StreamReader r = new StreamReader(tempFile, destEnc))
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
        }
        #endregion

        #region AddSections
        [TestMethod]
        public void AddSections()
        {
            AddSections_1();
            AddSections_2();
        }

        public static void AddSections_1()
        {
            foreach (Encoding srcEnc in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, srcEnc);

                    List<string> sections = new List<string>
                    {
                        "Section1",
                        "Section3",
                        "Section2",
                    };

                    Assert.IsTrue(IniReadWriter.AddSections(tempFile, sections));

                    Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));

                    string read;
                    using (StreamReader r = new StreamReader(tempFile, srcEnc))
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
        }

        public static void AddSections_2()
        {
            foreach (Encoding srcEnc in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, srcEnc);
                    using (StreamWriter w = new StreamWriter(tempFile, false, srcEnc))
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

                    Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));

                    string read;
                    using (StreamReader r = new StreamReader(tempFile, srcEnc))
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
        }
        #endregion

        #region WriteSectionFast
        [TestMethod]
        public void WriteSectionFast()
        {
            #region Template
            static void MultiStrTemplate(string section, string[] strs, string before, string after)
            {
                foreach (Encoding srcEnc in Encodings)
                {
                    string tempFile = FileHelper.GetTempFile();
                    try
                    {
                        EncodingHelper.WriteTextBom(tempFile, srcEnc);
                        using (StreamWriter w = new StreamWriter(tempFile, false, srcEnc))
                        {
                            w.WriteLine(before);
                        }

                        Assert.IsTrue(IniReadWriter.WriteSectionFast(tempFile, section, strs));

                        Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                        Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));

                        using (StreamReader r = new StreamReader(tempFile, destEnc))
                        {
                            string result = r.ReadToEnd();
                            Assert.IsTrue(after.Equals(result, StringComparison.Ordinal));
                        }
                    }
                    finally
                    {
                        File.Delete(tempFile);
                    }
                }
            }

            static void SingleStrTemplate(string section, string str, string before, string after)
            {
                foreach (Encoding srcEnc in Encodings)
                {
                    string tempFile = FileHelper.GetTempFile();
                    try
                    {
                        EncodingHelper.WriteTextBom(tempFile, srcEnc);
                        using (StreamWriter w = new StreamWriter(tempFile, false, srcEnc))
                        {
                            w.WriteLine(before);
                        }

                        Assert.IsTrue(IniReadWriter.WriteSectionFast(tempFile, section, str));

                        Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                        Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));

                        using (StreamReader r = new StreamReader(tempFile, destEnc))
                        {
                            string result = r.ReadToEnd();
                            Assert.IsTrue(after.Equals(result, StringComparison.Ordinal));
                        }
                    }
                    finally
                    {
                        File.Delete(tempFile);
                    }
                }
            }

            static void TextReaderTemplate(string section, string str, string before, string after)
            {
                foreach (Encoding srcEnc in Encodings)
                {
                    string tempSrcFile = FileHelper.GetTempFile();
                    string tempDestFile = FileHelper.GetTempFile();
                    try
                    {
                        EncodingHelper.WriteTextBom(tempSrcFile, srcEnc);
                        using (StreamWriter w = new StreamWriter(tempSrcFile, false, srcEnc))
                        {
                            w.WriteLine(str);
                        }

                        EncodingHelper.WriteTextBom(tempDestFile, srcEnc);
                        using (StreamWriter w = new StreamWriter(tempDestFile, false, srcEnc))
                        {
                            w.WriteLine(before);
                        }

                        using (StreamReader tr = new StreamReader(tempSrcFile, srcEnc))
                        {
                            Assert.IsTrue(IniReadWriter.WriteSectionFast(tempDestFile, section, tr));
                        }

                        Encoding destEnc = EncodingHelper.DetectEncoding(tempDestFile);
                        Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));

                        using (StreamReader r = new StreamReader(tempDestFile, destEnc))
                        {
                            string result = r.ReadToEnd();
                            Assert.IsTrue(after.Equals(result, StringComparison.Ordinal));
                        }
                    }
                    finally
                    {
                        File.Delete(tempSrcFile);
                        File.Delete(tempDestFile);
                    }
                }
            }
            #endregion

            // Test input : Multi string (string[])
            string[] multiStrContent = { "x64", "x86" };

            StringBuilder b = new StringBuilder();
            b.AppendLine();
            b.AppendLine("[Desktop]");
            b.AppendLine("x64");
            b.AppendLine("x86");
            string beforeStr1 = string.Empty;
            string afterStr1 = b.ToString();
            MultiStrTemplate("Desktop", multiStrContent, beforeStr1, afterStr1);

            b.Clear();
            b.AppendLine("[Desktop]");
            b.AppendLine("PPC");
            string beforeStr2 = b.ToString();
            b.Clear();
            b.AppendLine("[Desktop]");
            b.AppendLine("x64");
            b.AppendLine("x86");
            string afterStr2 = b.ToString();
            MultiStrTemplate("Desktop", multiStrContent, beforeStr2, afterStr2);

            b.Clear();
            b.AppendLine("[Mobile]");
            b.AppendLine("armhf");
            b.AppendLine("arm64");
            string beforeStr3 = b.ToString();
            b.Clear();
            b.AppendLine("[Mobile]");
            b.AppendLine("armhf");
            b.AppendLine("arm64");
            b.AppendLine();
            b.AppendLine("[Desktop]");
            b.AppendLine("x64");
            b.AppendLine("x86");
            string afterStr3 = b.ToString();
            MultiStrTemplate("Desktop", multiStrContent, beforeStr3, afterStr3);

            // Test input : Single String (string)
            b.Clear();
            b.AppendLine("x64");
            b.AppendLine("x86");
            string singleStrContent = b.ToString();
            afterStr1 += Environment.NewLine;
            afterStr2 += Environment.NewLine;
            afterStr3 += Environment.NewLine;
            SingleStrTemplate("Desktop", singleStrContent, beforeStr1, afterStr1);
            SingleStrTemplate("Desktop", singleStrContent, beforeStr2, afterStr2);
            SingleStrTemplate("Desktop", singleStrContent, beforeStr3, afterStr3);

            // Test input : TextReader
            TextReaderTemplate("Desktop", singleStrContent, beforeStr1, afterStr1);
            TextReaderTemplate("Desktop", singleStrContent, beforeStr2, afterStr2);
            TextReaderTemplate("Desktop", singleStrContent, beforeStr3, afterStr3);
        }
        #endregion

        #region RenameSection
        [TestMethod]
        public void RenameSection()
        {
            RenameSection_1();
            RenameSection_2();
            RenameSection_3();
        }

        public static void RenameSection_1()
        {
            foreach (Encoding srcEnc in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, srcEnc);

                    // Induce Error
                    Assert.IsFalse(IniReadWriter.RenameSection(tempFile, "Sec1", "Sec2"));

                    Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));

                    using (StreamReader r = new StreamReader(tempFile, destEnc))
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
        }

        public static void RenameSection_2()
        {
            foreach (Encoding srcEnc in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, srcEnc);
                    using (StreamWriter w = new StreamWriter(tempFile, false, srcEnc))
                    {
                        w.WriteLine("[Section]");
                        w.WriteLine("1=A");
                        w.WriteLine("2=B");
                    }

                    Assert.IsTrue(IniReadWriter.RenameSection(tempFile, "Section", "Another"));

                    Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));

                    string read;
                    using (StreamReader r = new StreamReader(tempFile, destEnc))
                    {
                        read = r.ReadToEnd();
                    }

                    StringBuilder b = new StringBuilder();
                    b.AppendLine("[Another]");
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
        }

        public static void RenameSection_3()
        {
            foreach (Encoding srcEnc in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, srcEnc);
                    using (StreamWriter w = new StreamWriter(tempFile, false, srcEnc))
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

                    Assert.IsTrue(IniReadWriter.RenameSection(tempFile, "Section2", "SectionB"));

                    Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));

                    string read;
                    using (StreamReader r = new StreamReader(tempFile, destEnc))
                    {
                        read = r.ReadToEnd();
                    }

                    StringBuilder b = new StringBuilder();
                    b.AppendLine("[Section1]");
                    b.AppendLine("00=A");
                    b.AppendLine("01=B");
                    b.AppendLine("02=C");
                    b.AppendLine();
                    b.AppendLine("[SectionB]");
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
        }
        #endregion

        #region RenameSections
        [TestMethod]
        public void RenameSections()
        {
            RenameSections_1();
            RenameSections_2();
        }

        public static void RenameSections_1()
        {
            foreach (Encoding srcEnc in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, srcEnc);
                    using (StreamWriter w = new StreamWriter(tempFile, false, srcEnc))
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
                        new IniKey("Section1", "SectionA"),
                        new IniKey("Section3", "SectionC"),
                    };
                    bool[] results = IniReadWriter.RenameSections(tempFile, keys);
                    Assert.IsTrue(results.All(x => x));

                    Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));

                    string read;
                    using (StreamReader r = new StreamReader(tempFile, destEnc))
                    {
                        read = r.ReadToEnd();
                    }

                    StringBuilder b = new StringBuilder();
                    b.AppendLine("[SectionA]");
                    b.AppendLine("00=A");
                    b.AppendLine("01=B");
                    b.AppendLine("02=C");
                    b.AppendLine();
                    b.AppendLine("[Section2]");
                    b.AppendLine("10=한");
                    b.AppendLine("11=국");
                    b.AppendLine("[SectionC]");
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
        }

        public static void RenameSections_2()
        {
            foreach (Encoding srcEnc in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, srcEnc);
                    using (StreamWriter w = new StreamWriter(tempFile, false, srcEnc))
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
                        new IniKey("Section4", "SectionD"),
                        new IniKey("Section2", "SectionB"),
                    };
                    bool[] results = IniReadWriter.RenameSections(tempFile, keys);
                    Assert.IsTrue(results.Any(x => !x));

                    Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));

                    string read;
                    using (StreamReader r = new StreamReader(tempFile, destEnc))
                    {
                        read = r.ReadToEnd();
                    }

                    StringBuilder b = new StringBuilder();
                    b.AppendLine("[Section1]");
                    b.AppendLine("00=A");
                    b.AppendLine("01=B");
                    b.AppendLine("02=C");
                    b.AppendLine();
                    b.AppendLine("[SectionB]");
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
        }
        #endregion

        #region DeleteSection
        [TestMethod]
        public void DeleteSection()
        {
            DeleteSection_1();
            DeleteSection_2();
            DeleteSection_3();
        }

        public static void DeleteSection_1()
        {
            foreach (Encoding srcEnc in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, srcEnc);

                    // Induce Error
                    Assert.IsFalse(IniReadWriter.DeleteSection(tempFile, "Section"));

                    Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));

                    using (StreamReader r = new StreamReader(tempFile, destEnc))
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
        }

        public static void DeleteSection_2()
        {
            foreach (Encoding srcEnc in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, srcEnc);
                    using (StreamWriter w = new StreamWriter(tempFile, false, srcEnc))
                    {
                        w.WriteLine("[Section]");
                        w.WriteLine("1=A");
                        w.WriteLine("2=B");
                    }

                    Assert.IsTrue(IniReadWriter.DeleteSection(tempFile, "Section"));

                    Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));

                    using (StreamReader r = new StreamReader(tempFile, destEnc))
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
        }

        public static void DeleteSection_3()
        {
            foreach (Encoding srcEnc in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, srcEnc);
                    using (StreamWriter w = new StreamWriter(tempFile, false, srcEnc))
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

                    Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));

                    string read;
                    using (StreamReader r = new StreamReader(tempFile, destEnc))
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
        }
        #endregion

        #region DeleteSections
        [TestMethod]
        public void DeleteSections()
        {
            DeleteSections_1();
            DeleteSections_2();
        }

        public static void DeleteSections_1()
        {
            foreach (Encoding srcEnc in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, srcEnc);
                    using (StreamWriter w = new StreamWriter(tempFile, false, srcEnc))
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

                    List<string> sections = new List<string>
                    {
                        "Section1",
                        "Section3",
                    };

                    bool[] results = IniReadWriter.DeleteSections(tempFile, sections);
                    Assert.IsTrue(results.All(x => x));

                    Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));

                    string read;
                    using (StreamReader r = new StreamReader(tempFile, destEnc))
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
        }

        public static void DeleteSections_2()
        {
            foreach (Encoding srcEnc in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, srcEnc);
                    using (StreamWriter w = new StreamWriter(tempFile, false, srcEnc))
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

                    List<string> sections = new List<string>
                    {
                        "Section4",
                        "Section2",
                    };

                    bool[] results = IniReadWriter.DeleteSections(tempFile, sections);
                    Assert.IsFalse(results[0]);
                    Assert.IsTrue(results[1]);

                    Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));

                    string read;
                    using (StreamReader r = new StreamReader(tempFile, destEnc))
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
        }
        #endregion

        #region ReadRawSection
        [TestMethod]
        public void ReadRawSection()
        {
            ReadRawSection_1();
            ReadRawSection_2();
            ReadRawSection_3();
            ReadRawSection_4();
        }

        public static void ReadRawSection_1()
        {
            foreach (Encoding encoding in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, encoding);
                    using (StreamWriter w = new StreamWriter(tempFile, false, encoding))
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
        }

        public static void ReadRawSection_2()
        {
            foreach (Encoding encoding in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, encoding);
                    using (StreamWriter w = new StreamWriter(tempFile, false, encoding))
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
        }

        public static void ReadRawSection_3()
        {
            foreach (Encoding encoding in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, encoding);
                    using (StreamWriter w = new StreamWriter(tempFile, false, encoding))
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
        }

        public static void ReadRawSection_4()
        {
            foreach (Encoding encoding in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, encoding);
                    using (StreamWriter w = new StreamWriter(tempFile, false, encoding))
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
        }
        #endregion

        #region ReadRawSections
        [TestMethod]
        public void ReadRawSections()
        {
            ReadRawSections_1();
            ReadRawSections_2();
        }

        public static void ReadRawSections_1()
        {
            foreach (Encoding encoding in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, encoding);
                    using (StreamWriter w = new StreamWriter(tempFile, false, encoding))
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
        }

        public static void ReadRawSections_2()
        {
            foreach (Encoding encoding in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, encoding);
                    using (StreamWriter w = new StreamWriter(tempFile, false, encoding))
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
        }
        #endregion

        #region Merge2
        [TestMethod]
        public void Merge2()
        {
            // 1st
            StringBuilder b = new StringBuilder();
            b.AppendLine("[Section1]");
            b.AppendLine("01=A");
            b.AppendLine("02=B");
            string src1 = b.ToString();
            Merge2Template(src1, string.Empty, src1);

            // 2nd
            b.Clear();
            b.AppendLine("[Section2]");
            b.AppendLine("03=C");
            string src2 = b.ToString();
            b.Clear();
            b.AppendLine("[Section2]");
            b.AppendLine("03=C");
            b.AppendLine();
            b.AppendLine("[Section1]");
            b.AppendLine("01=A");
            b.AppendLine("02=B");
            Merge2Template(src1, src2, b.ToString());

            // 3rd
            b.Clear();
            b.AppendLine("[Section1]");
            b.AppendLine("04=D");
            b.AppendLine();
            b.AppendLine("[Section2]");
            b.AppendLine("03=C");
            src2 = b.ToString();
            b.Clear();
            b.AppendLine("[Section1]");
            b.AppendLine("04=D");
            b.AppendLine("01=A");
            b.AppendLine("02=B");
            b.AppendLine();
            b.AppendLine("[Section2]");
            b.AppendLine("03=C");
            Merge2Template(src1, src2, b.ToString());

            // 4th
            b.Clear();
            b.AppendLine("[Section1]");
            b.AppendLine("02=D");
            b.AppendLine();
            b.AppendLine("[Section2]");
            b.AppendLine("03=C");
            src1 = b.ToString();
            b.Clear();
            b.AppendLine("[Section1]");
            b.AppendLine("01=A");
            b.AppendLine("02=B");
            src2 = b.ToString();
            b.Clear();
            b.AppendLine("[Section1]");
            b.AppendLine("01=A");
            b.AppendLine("02=D");
            b.AppendLine();
            b.AppendLine("[Section2]");
            b.AppendLine("03=C");
            Merge2Template(src1, src2, b.ToString());

            // 5th
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
            src1 = b.ToString();
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
            src2 = b.ToString();
            // Result
            b.Clear();
            b.AppendLine("  [Section1]");
            b.AppendLine("A=6");
            b.AppendLine("B=7");
            b.AppendLine("C=8");
            b.AppendLine("D=9");
            b.AppendLine();
            b.AppendLine(" [Section2]  ");
            b.AppendLine("ㄱ=甲");
            b.AppendLine(" ㄴ = 乙");
            b.AppendLine("ㄷ = 丙 ");
            b.AppendLine(" ㄹ = 丁 ");
            b.AppendLine();
            b.AppendLine("[Section3]");
            b.AppendLine("일=一");
            b.AppendLine("이=二");
            b.AppendLine("삼=三");
            b.AppendLine("사=四");
            Merge2Template(src1, src2, b.ToString());

            // Legacy tests
            Merge2_1();
            Merge2_2();
            Merge2_3();
            Merge2_4();
        }

        public static void Merge2_1()
        {
            foreach (Encoding srcEnc in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                string destFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile, srcEnc);
                    EncodingHelper.WriteTextBom(destFile, srcEnc);

                    using (StreamWriter w = new StreamWriter(tempFile, false, srcEnc))
                    {
                        w.WriteLine("[Section1]");
                        w.WriteLine("01=A");
                        w.WriteLine("02=B");
                    }

                    Assert.IsTrue(IniReadWriter.Merge(tempFile, destFile));

                    Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));

                    string read;
                    using (StreamReader r = new StreamReader(destFile, destEnc))
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
        }

        public static void Merge2_2()
        {
            string tempFile = FileHelper.GetTempFile();
            string destFile = FileHelper.GetTempFile();
            try
            {
                EncodingHelper.WriteTextBom(tempFile, Encoding.UTF8);
                EncodingHelper.WriteTextBom(destFile, Encoding.Unicode);

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
                Encoding encoding = EncodingHelper.DetectEncoding(destFile);
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

        public static void Merge2_3()
        {
            string tempFile = FileHelper.GetTempFile();
            string destFile = FileHelper.GetTempFile();
            try
            {
                EncodingHelper.WriteTextBom(tempFile, Encoding.Unicode);
                EncodingHelper.WriteTextBom(destFile, Encoding.UTF8);

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
                Encoding encoding = EncodingHelper.DetectEncoding(destFile);
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

        public static void Merge2_4()
        {
            string tempFile = FileHelper.GetTempFile();
            string destFile = FileHelper.GetTempFile();
            try
            {
                EncodingHelper.WriteTextBom(tempFile, Encoding.Unicode);
                EncodingHelper.WriteTextBom(destFile, Encoding.BigEndianUnicode);

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
                Encoding encoding = EncodingHelper.DetectEncoding(destFile);
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

        #region MergeCompact2
        [TestMethod]
        public void MergeCompact2()
        {
            // Prepare source file
            StringBuilder b = new StringBuilder();
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
            string src1 = b.ToString();

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
            string src2 = b.ToString();

            // Result
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
            Merge2Template(src1, src2, b.ToString(), true);
        }
        #endregion

        #region Merge3
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

        public static void Merge3_1()
        {
            foreach (Encoding encoding in Encodings)
            {
                string tempFile1 = FileHelper.GetTempFile();
                string tempFile2 = FileHelper.GetTempFile();
                string destFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(destFile, encoding);

                    Assert.IsTrue(IniReadWriter.Merge(tempFile1, tempFile2, destFile));

                    string read;
                    using (StreamReader r = new StreamReader(destFile, encoding))
                    {
                        read = r.ReadToEnd();
                    }

                    Assert.IsTrue(read.Length == 0);
                }
                finally
                {
                    File.Delete(tempFile1);
                    File.Delete(tempFile2);
                    File.Delete(destFile);
                }
            }
        }

        public static void Merge3_2()
        {
            foreach (Encoding encoding in Encodings)
            {
                string tempFile1 = FileHelper.GetTempFile();
                string tempFile2 = FileHelper.GetTempFile();
                string destFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile1, encoding);
                    EncodingHelper.WriteTextBom(destFile, encoding);

                    using (StreamWriter w = new StreamWriter(tempFile1, false, encoding))
                    {
                        w.WriteLine("[Section1]");
                        w.WriteLine("01=A");
                        w.WriteLine("02=B");
                    }

                    Assert.IsTrue(IniReadWriter.Merge(tempFile1, tempFile2, destFile));

                    string read;
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
        }

        public static void Merge3_3()
        {
            foreach (Encoding encoding in Encodings)
            {
                string tempFile1 = FileHelper.GetTempFile();
                string tempFile2 = FileHelper.GetTempFile();
                string destFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile1, encoding);
                    EncodingHelper.WriteTextBom(destFile, encoding);

                    using (StreamWriter w = new StreamWriter(tempFile1, false, encoding))
                    {
                        w.WriteLine("[Section1]");
                        w.WriteLine("01=A");
                        w.WriteLine("02=B");
                    }

                    using (StreamWriter w = new StreamWriter(tempFile2, false, encoding))
                    {
                        w.WriteLine("[Section2]");
                        w.WriteLine("03=C");
                    }

                    Assert.IsTrue(IniReadWriter.Merge(tempFile1, tempFile2, destFile));

                    string read;
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
        }

        public static void Merge3_4()
        {
            foreach (Encoding encoding in Encodings)
            {
                string tempFile1 = FileHelper.GetTempFile();
                string tempFile2 = FileHelper.GetTempFile();
                string destFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile1, encoding);
                    EncodingHelper.WriteTextBom(destFile, encoding);

                    using (StreamWriter w = new StreamWriter(tempFile1, false, encoding))
                    {
                        w.WriteLine("[Section1]");
                        w.WriteLine("01=A");
                        w.WriteLine("02=B");
                    }

                    using (StreamWriter w = new StreamWriter(tempFile2, false, encoding))
                    {
                        w.WriteLine("[Section1]");
                        w.WriteLine("04=D");
                        w.WriteLine();
                        w.WriteLine("[Section2]");
                        w.WriteLine("03=C");
                    }

                    Assert.IsTrue(IniReadWriter.Merge(tempFile1, tempFile2, destFile));

                    string read;
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
        }

        public static void Merge3_5()
        {
            foreach (Encoding encoding in Encodings)
            {
                string tempFile1 = FileHelper.GetTempFile();
                string tempFile2 = FileHelper.GetTempFile();
                string destFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile1, encoding);
                    EncodingHelper.WriteTextBom(tempFile2, encoding);
                    EncodingHelper.WriteTextBom(destFile, encoding);

                    using (StreamWriter w = new StreamWriter(tempFile1, false, encoding))
                    {
                        w.WriteLine("[Section1]");
                        w.WriteLine("01=A");
                        w.WriteLine("02=B");
                    }

                    using (StreamWriter w = new StreamWriter(tempFile2, false, encoding))
                    {
                        w.WriteLine("[Section1]");
                        w.WriteLine("02=D");
                        w.WriteLine();
                        w.WriteLine("[Section2]");
                        w.WriteLine("03=C");
                    }

                    Assert.IsTrue(IniReadWriter.Merge(tempFile1, tempFile2, destFile));

                    string read;
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
        }

        public static void Merge3_6()
        {
            foreach (Encoding encoding in Encodings)
            {
                string tempFile1 = FileHelper.GetTempFile();
                string tempFile2 = FileHelper.GetTempFile();
                string destFile = FileHelper.GetTempFile();
                try
                {
                    EncodingHelper.WriteTextBom(tempFile1, encoding);
                    EncodingHelper.WriteTextBom(destFile, encoding);

                    using (StreamWriter w = new StreamWriter(tempFile1, false, encoding))
                    {
                        w.WriteLine("[Section1]");
                        w.WriteLine("01=A");
                        w.WriteLine("02=B");
                    }

                    using (StreamWriter w = new StreamWriter(tempFile2, false, encoding))
                    {
                        w.WriteLine("[Section1]");
                        w.WriteLine("02=D");
                        w.WriteLine();
                        w.WriteLine("[Section2]");
                        w.WriteLine("03=C");
                    }

                    using (StreamWriter w = new StreamWriter(destFile, false, encoding))
                    {
                        w.WriteLine("[Section1]");
                        w.WriteLine("02=E");
                        w.WriteLine();
                        w.WriteLine("[Section2]");
                        w.WriteLine("04=F");
                    }

                    Assert.IsTrue(IniReadWriter.Merge(tempFile1, tempFile2, destFile));

                    string read;
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
        }
        #endregion

        #region Compact
        [TestMethod]
        public void Compact()
        {
            StringBuilder b = new StringBuilder();
            b.AppendLine("  Header  ");
            b.AppendLine();
            b.AppendLine("       [NonIniStyle]");
            b.AppendLine("1");
            b.AppendLine("  2");
            b.AppendLine("3  ");
            b.AppendLine(" 4    ");
            b.AppendLine();
            b.AppendLine("[IniStyle]           ");
            b.AppendLine("일=一");
            b.AppendLine(" 이 = 二 ");
            b.AppendLine("  삼  =  三  ");
            b.AppendLine("사     =       四    ");
            string srcStr = b.ToString();

            b.Clear();
            b.AppendLine("Header");
            b.AppendLine();
            b.AppendLine("[NonIniStyle]");
            b.AppendLine("1");
            b.AppendLine("2");
            b.AppendLine("3");
            b.AppendLine("4");
            b.AppendLine();
            b.AppendLine("[IniStyle]");
            b.AppendLine("일=一");
            b.AppendLine("이=二");
            b.AppendLine("삼=三");
            b.AppendLine("사=四");
            string compStr = b.ToString();

            foreach (Encoding srcEnc in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    using (StreamWriter sw = new StreamWriter(tempFile, false, srcEnc))
                    {
                        sw.Write(srcStr);
                    }

                    IniReadWriter.Compact(tempFile);

                    Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));

                    string resultStr;
                    using (StreamReader sr = new StreamReader(tempFile, destEnc, false))
                    {
                        resultStr = sr.ReadToEnd();
                    }
                    Assert.IsTrue(resultStr.Equals(compStr, StringComparison.Ordinal));
                }
                finally
                {
                    File.Delete(tempFile);
                }
            }
        }
        #endregion

        #region Fast Forward
        [TestMethod]
        public void FastForwardTextReader()
        {
            static void Template(string section, string src, string expected)
            {
                string remain;
                using (StringReader sr = new StringReader(src))
                {
                    IniReadWriter.FastForwardTextReader(sr, section);
                    remain = sr.ReadToEnd();
                }

                Assert.IsTrue(remain.Equals(expected, StringComparison.Ordinal));
            }

            StringBuilder b = new StringBuilder();
            b.AppendLine("[갑]");
            b.AppendLine("UnicodeA");
            b.AppendLine("[을]");
            b.AppendLine("UnicodeB");
            b.AppendLine("[병]");
            b.AppendLine("UnicodeC");
            string srcStr = b.ToString();

            // First section
            b.Clear();
            b.AppendLine("UnicodeA");
            b.AppendLine("[을]");
            b.AppendLine("UnicodeB");
            b.AppendLine("[병]");
            b.AppendLine("UnicodeC");
            Template("갑", srcStr, b.ToString());

            // Second section
            b.Clear();
            b.AppendLine("UnicodeB");
            b.AppendLine("[병]");
            b.AppendLine("UnicodeC");
            Template("을", srcStr, b.ToString());

            // Third section
            b.Clear();
            b.AppendLine("UnicodeC");
            Template("병", srcStr, b.ToString());

            // No target section
            Template("NUL", srcStr, string.Empty);
        }

        [TestMethod]
        public void FastForwardTextWriter()
        {
            static void Template(string? section, string src, string expected, bool copyFromNewSection)
            {
                string result;
                using (StringReader sr = new StringReader(src))
                using (StringWriter sw = new StringWriter())
                {
                    IniReadWriter.FastForwardTextWriter(sr, sw, section, copyFromNewSection);
                    result = sw.ToString();
                }

                Assert.IsTrue(result.Equals(expected, StringComparison.Ordinal));
            }

            StringBuilder b = new StringBuilder();
            b.AppendLine("[갑]");
            b.AppendLine("UnicodeA");
            b.AppendLine("[을]");
            b.AppendLine("UnicodeB");
            b.AppendLine("[병]");
            b.AppendLine("UnicodeC");
            string srcStr = b.ToString();

            // First section
            b.Clear();
            b.AppendLine("[갑]");
            Template("갑", srcStr, b.ToString(), false);

            // Second section
            b.Clear();
            b.AppendLine("[갑]");
            b.AppendLine("UnicodeA");
            b.AppendLine("[을]");
            Template("을", srcStr, b.ToString(), false);

            // Third section
            b.Clear();
            b.AppendLine("[갑]");
            b.AppendLine("UnicodeA");
            b.AppendLine("[을]");
            b.AppendLine("UnicodeB");
            b.AppendLine("[병]");
            Template("병", srcStr, b.ToString(), false);

            // No target section
            b.Clear();
            b.AppendLine("[갑]");
            b.AppendLine("UnicodeA");
            b.AppendLine("[을]");
            b.AppendLine("UnicodeB");
            b.AppendLine("[병]");
            b.AppendLine("UnicodeC");
            b.AppendLine();
            b.AppendLine("[NUL]");
            Template("NUL", srcStr, b.ToString(), false);

            // Test copyFromNewSection
            b.Clear();
            b.AppendLine("UnicodeA");
            b.AppendLine("[을]");
            b.AppendLine("UnicodeB");
            b.AppendLine("[병]");
            b.AppendLine("UnicodeC");
            srcStr = b.ToString();
            b.Clear();
            b.AppendLine("[을]");
            b.AppendLine("UnicodeB");
            b.AppendLine("[병]");
            b.AppendLine("UnicodeC");
            Template(null, srcStr, b.ToString(), true);
        }
        #endregion

        #region Template
        static void WriteTemplate(string srcStr, string expectStr, Func<string, bool> testFunc)
        {
            foreach (Encoding srcEnc in Encodings)
            {
                string tempFile = FileHelper.GetTempFile();
                try
                {
                    using (StreamWriter sw = new StreamWriter(tempFile, false, srcEnc))
                    {
                        sw.Write(srcStr);
                    }

                    Assert.IsTrue(testFunc.Invoke(tempFile));

                    Encoding destEnc = EncodingHelper.DetectEncoding(tempFile);
                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));

                    string resultStr;
                    using (StreamReader sr = new StreamReader(tempFile, destEnc, false))
                    {
                        resultStr = sr.ReadToEnd();
                    }

                    Assert.IsTrue(resultStr.Equals(expectStr, StringComparison.Ordinal));
                }
                finally
                {
                    File.Delete(tempFile);
                }
            }
        }

        static void Merge2Template(string srcStr1, string srcStr2, string expectStr, bool compact = false)
        {
            foreach (Encoding srcEnc in Encodings)
            {
                string srcTempFile = FileHelper.GetTempFile();
                string destTempFile = FileHelper.GetTempFile();
                try
                {
                    using (StreamWriter sw = new StreamWriter(srcTempFile, false, srcEnc))
                    {
                        sw.Write(srcStr1);
                    }
                    using (StreamWriter sw = new StreamWriter(destTempFile, false, srcEnc))
                    {
                        sw.Write(srcStr2);
                    }

                    bool result;
                    if (compact)
                        result = IniReadWriter.MergeCompact(srcTempFile, destTempFile);
                    else
                        result = IniReadWriter.Merge(srcTempFile, destTempFile);
                    Assert.IsTrue(result);

                    Encoding destEnc = EncodingHelper.DetectEncoding(destTempFile);
                    Assert.IsTrue(EncodingHelper.EncodingEquals(srcEnc, destEnc));

                    string resultStr;
                    using (StreamReader sr = new StreamReader(destTempFile, destEnc, false))
                    {
                        resultStr = sr.ReadToEnd();
                    }

                    Assert.IsTrue(resultStr.Equals(expectStr, StringComparison.Ordinal));
                }
                finally
                {
                    File.Delete(srcTempFile);
                    File.Delete(destTempFile);
                }
            }
        }
        #endregion
    }
}
