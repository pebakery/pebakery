using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.Core;
using System.Collections.Generic;

namespace UnitTest
{
    [TestClass]
    public class UnitTest_CodeParser
    {
        [TestMethod]
        public void GetNextArgument()
        {
            List<string> codes = new List<string>
            {
                @"TXTAddLine,#3.au3,""IniWrite(#$q#3.ini#$q,#$qInfoHostOS#$q,#$qSystemDir#$q,SHGetSpecialFolderPath(37))"",Append",
                @"TXTAddLine,#3.au3,""   Return SetError($BOOL[0],0,DllStructGetData($lpszPath,1))  "",Append",
                @"StrFormat,REPLACE,#2,\,,#8",
            };

            List<List<Tuple<string, string>>> testcases = new List<List<Tuple<string, string>>>()
            {
                new List<Tuple<string, string>>()
                {
                    new Tuple<string, string>(@"TXTAddLine", @"#3.au3,""IniWrite(#$q#3.ini#$q,#$qInfoHostOS#$q,#$qSystemDir#$q,SHGetSpecialFolderPath(37))"",Append"),
                    new Tuple<string, string>(@"#3.au3", @"""IniWrite(#$q#3.ini#$q,#$qInfoHostOS#$q,#$qSystemDir#$q,SHGetSpecialFolderPath(37))"",Append"),
                    new Tuple<string, string>(@"IniWrite(#$q#3.ini#$q,#$qInfoHostOS#$q,#$qSystemDir#$q,SHGetSpecialFolderPath(37))", @"Append"),
                    new Tuple<string, string>(@"Append", string.Empty),
                    new Tuple<string, string>(string.Empty, string.Empty),
                },
                new List<Tuple<string, string>>()
                {
                    new Tuple<string, string>(@"TXTAddLine", @"#3.au3,""   Return SetError($BOOL[0],0,DllStructGetData($lpszPath,1))  "",Append"),
                    new Tuple<string, string>(@"#3.au3", @"""   Return SetError($BOOL[0],0,DllStructGetData($lpszPath,1))  "",Append"),
                    new Tuple<string, string>(@"   Return SetError($BOOL[0],0,DllStructGetData($lpszPath,1))  ", @"Append"),
                    new Tuple<string, string>(@"Append", string.Empty),
                    new Tuple<string, string>(string.Empty, string.Empty),
                },
                new List<Tuple<string, string>>()
                {
                    new Tuple<string, string>(@"StrFormat", @"REPLACE,#2,\,,#8"),
                    new Tuple<string, string>(@"REPLACE", @"#2,\,,#8"),
                    new Tuple<string, string>(@"#2", @"\,,#8"),
                    new Tuple<string, string>(@"\", @",#8"),
                    new Tuple<string, string>(string.Empty, @"#8"),
                    new Tuple<string, string>(@"#8", string.Empty),
                    new Tuple<string, string>(string.Empty, string.Empty),
                },
            };
            
            for (int i = 0; i < codes.Count; i++)
            {
                Tuple<string, string> tuple;
                string next, remainder = codes[i];
                
                foreach (Tuple<string, string> testcase in testcases[i])
                {
                    tuple = CodeParser.GetNextArgument(remainder);
                    next = tuple.Item1;
                    remainder = tuple.Item2;

                    Console.WriteLine(next);
                    Console.WriteLine(remainder);

                    Assert.IsTrue(next.Equals(testcase.Item1, StringComparison.Ordinal));
                    Assert.IsTrue(remainder.Equals(testcase.Item2, StringComparison.Ordinal));
                }
            }
        }
    }
}
