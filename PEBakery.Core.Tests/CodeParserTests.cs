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

namespace PEBakery.Core.Tests
{
    [TestClass]
    public class CodeParserTests
    {
        #region GetNextArgument
        [TestMethod]
        [TestCategory("CodeParser")]
        public void GetNextArgument()
        {
            CodeParser_GetNextArgument_1();
            CodeParser_GetNextArgument_2();
            CodeParser_GetNextArgument_3();
            CodeParser_GetNextArgument_4();
            CodeParser_GetNextArgument_5();
        }

        public static void CodeParser_GetNextArgument_Test(string code, List<Tuple<string, string>> testcases)
        {
            string remainder = code;

            foreach (Tuple<string, string> testcase in testcases)
            {
                string next;
                (next, remainder) = CodeParser.GetNextArgument(remainder);

                Console.WriteLine(next);
                Console.WriteLine(remainder ?? "null");

                Assert.IsTrue(next.Equals(testcase.Item1, StringComparison.Ordinal));
                if (remainder != null)
                    Assert.IsTrue(remainder.Equals(testcase.Item2, StringComparison.Ordinal));
                else
                    Assert.IsTrue(remainder == null);
            }
        }

        public void CodeParser_GetNextArgument_1()
        {
            const string code = @"TXTAddLine,#3.au3,""IniWrite(#$q#3.ini#$q,#$qInfoHostOS#$q,#$qSystemDir#$q,SHGetSpecialFolderPath(37))"",Append";
            List<Tuple<string, string>> testcases = new List<Tuple<string, string>>()
            {
                new Tuple<string, string>(@"TXTAddLine", @"#3.au3,""IniWrite(#$q#3.ini#$q,#$qInfoHostOS#$q,#$qSystemDir#$q,SHGetSpecialFolderPath(37))"",Append"),
                new Tuple<string, string>(@"#3.au3", @"""IniWrite(#$q#3.ini#$q,#$qInfoHostOS#$q,#$qSystemDir#$q,SHGetSpecialFolderPath(37))"",Append"),
                new Tuple<string, string>(@"IniWrite(#$q#3.ini#$q,#$qInfoHostOS#$q,#$qSystemDir#$q,SHGetSpecialFolderPath(37))", @"Append"),
                new Tuple<string, string>(@"Append", null),
            };

            CodeParser_GetNextArgument_Test(code, testcases);
        }

        public void CodeParser_GetNextArgument_2()
        {
            const string code = @"TXTAddLine,#3.au3,""   Return SetError($BOOL[0],0,DllStructGetData($lpszPath,1))  "",Append";
            List<Tuple<string, string>> testcases = new List<Tuple<string, string>>()
            {
                new Tuple<string, string>(@"TXTAddLine", @"#3.au3,""   Return SetError($BOOL[0],0,DllStructGetData($lpszPath,1))  "",Append"),
                new Tuple<string, string>(@"#3.au3", @"""   Return SetError($BOOL[0],0,DllStructGetData($lpszPath,1))  "",Append"),
                new Tuple<string, string>(@"   Return SetError($BOOL[0],0,DllStructGetData($lpszPath,1))  ", @"Append"),
                new Tuple<string, string>(@"Append", null),
            };

            CodeParser_GetNextArgument_Test(code, testcases);
        }

        public void CodeParser_GetNextArgument_3()
        {
            const string code = @"StrFormat,REPLACE,#2,\,,#8";
            List<Tuple<string, string>> testcases = new List<Tuple<string, string>>()
            {
                new Tuple<string, string>(@"StrFormat", @"REPLACE,#2,\,,#8"),
                new Tuple<string, string>(@"REPLACE", @"#2,\,,#8"),
                new Tuple<string, string>(@"#2", @"\,,#8"),
                new Tuple<string, string>(@"\", @",#8"),
                new Tuple<string, string>(string.Empty, @"#8"),
                new Tuple<string, string>(@"#8", null),
            };

            CodeParser_GetNextArgument_Test(code, testcases);
        }

        public void CodeParser_GetNextArgument_4()
        {
            const string code = @"Set,%Waik2Tools%,";
            List<Tuple<string, string>> testcases = new List<Tuple<string, string>>()
            {
                new Tuple<string, string>(@"Set", @"%Waik2Tools%,"),
                new Tuple<string, string>(@"%Waik2Tools%", string.Empty),
                new Tuple<string, string>(string.Empty, null),
            };

            CodeParser_GetNextArgument_Test(code, testcases);
        }

        public void CodeParser_GetNextArgument_5()
        {
            const string code = "Message,\"Hello\"\"World\",Information";
            List<Tuple<string, string>> testcases = new List<Tuple<string, string>>()
            {
                new Tuple<string, string>("Message", "\"Hello\"\"World\",Information"),
                new Tuple<string, string>("Hello\"\"World", "Information"),
                new Tuple<string, string>("Information", null),
            };

            CodeParser_GetNextArgument_Test(code, testcases);
        }
        #endregion
    }
}
