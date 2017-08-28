using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.Core;
using System.Collections.Generic;

namespace UnitTest
{
    [TestClass]
    public class UnitTest_CodeParser
    {
        public static void GetNextArgument_Test(string code, List<Tuple<string, string>> testcases)
        {
            Tuple<string, string> tuple;
            string next, remainder = code;

            foreach (Tuple<string, string> testcase in testcases)
            {
                tuple = CodeParser.GetNextArgument(remainder);
                next = tuple.Item1;
                remainder = tuple.Item2;

                Console.WriteLine(next);
                if (remainder != null)
                    Console.WriteLine(remainder);
                else
                    Console.WriteLine("null");

                Assert.IsTrue(next.Equals(testcase.Item1, StringComparison.Ordinal));
                if (remainder != null)
                    Assert.IsTrue(remainder.Equals(testcase.Item2, StringComparison.Ordinal));
                else
                    Assert.IsTrue(remainder == null);
            }
        }

        [TestMethod]
        public void GetNextArgument_1()
        {
            string code = @"TXTAddLine,#3.au3,""IniWrite(#$q#3.ini#$q,#$qInfoHostOS#$q,#$qSystemDir#$q,SHGetSpecialFolderPath(37))"",Append";
            List<Tuple<string, string>> testcases = new List<Tuple<string, string>>()
            {
                new Tuple<string, string>(@"TXTAddLine", @"#3.au3,""IniWrite(#$q#3.ini#$q,#$qInfoHostOS#$q,#$qSystemDir#$q,SHGetSpecialFolderPath(37))"",Append"),
                new Tuple<string, string>(@"#3.au3", @"""IniWrite(#$q#3.ini#$q,#$qInfoHostOS#$q,#$qSystemDir#$q,SHGetSpecialFolderPath(37))"",Append"),
                new Tuple<string, string>(@"IniWrite(#$q#3.ini#$q,#$qInfoHostOS#$q,#$qSystemDir#$q,SHGetSpecialFolderPath(37))", @"Append"),
                new Tuple<string, string>(@"Append", null),
            };

            GetNextArgument_Test(code, testcases);
        }

        [TestMethod]
        public void GetNextArgument_2()
        {
            string code = @"TXTAddLine,#3.au3,""   Return SetError($BOOL[0],0,DllStructGetData($lpszPath,1))  "",Append";
            List<Tuple<string, string>> testcases = new List<Tuple<string, string>>()
            {
                new Tuple<string, string>(@"TXTAddLine", @"#3.au3,""   Return SetError($BOOL[0],0,DllStructGetData($lpszPath,1))  "",Append"),
                new Tuple<string, string>(@"#3.au3", @"""   Return SetError($BOOL[0],0,DllStructGetData($lpszPath,1))  "",Append"),
                new Tuple<string, string>(@"   Return SetError($BOOL[0],0,DllStructGetData($lpszPath,1))  ", @"Append"),
                new Tuple<string, string>(@"Append", null),
            };

            GetNextArgument_Test(code, testcases);
        }

        [TestMethod]
        public void GetNextArgument_3()
        {
            string code = @"StrFormat,REPLACE,#2,\,,#8";
            List<Tuple<string, string>> testcases = new List<Tuple<string, string>>()
            {
                new Tuple<string, string>(@"StrFormat", @"REPLACE,#2,\,,#8"),
                new Tuple<string, string>(@"REPLACE", @"#2,\,,#8"),
                new Tuple<string, string>(@"#2", @"\,,#8"),
                new Tuple<string, string>(@"\", @",#8"),
                new Tuple<string, string>(string.Empty, @"#8"),
                new Tuple<string, string>(@"#8", null),
            };

            GetNextArgument_Test(code, testcases);
        }

        [TestMethod]
        public void GetNextArgument_4()
        {
            string code = @"Set,%Waik2Tools%,";
            List<Tuple<string, string>> testcases = new List<Tuple<string, string>>()
            {
                new Tuple<string, string>(@"Set", @"%Waik2Tools%,"),
                new Tuple<string, string>(@"%Waik2Tools%", string.Empty),
                new Tuple<string, string>(string.Empty, null),
            };

            GetNextArgument_Test(code, testcases);
        }
    }
}
