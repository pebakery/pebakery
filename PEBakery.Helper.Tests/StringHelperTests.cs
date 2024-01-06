/*
    Copyright (C) 2018-2023 Hajin Jang
 
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

namespace PEBakery.Helper.Tests
{
    [TestClass]
    [TestCategory(nameof(Helper))]
    [TestCategory(nameof(StringHelper))]
    public class StringHelperTests
    {
        [TestMethod]
        public void SplitEx()
        {
            List<string> strs = StringHelper.SplitEx(@"1|2|3|4|5", "|", StringComparison.Ordinal);
            Assert.AreEqual(5, strs.Count);
            Assert.IsTrue(strs[0].Equals("1", StringComparison.Ordinal));
            Assert.IsTrue(strs[1].Equals("2", StringComparison.Ordinal));
            Assert.IsTrue(strs[2].Equals("3", StringComparison.Ordinal));
            Assert.IsTrue(strs[3].Equals("4", StringComparison.Ordinal));
            Assert.IsTrue(strs[4].Equals("5", StringComparison.Ordinal));

            strs = StringHelper.SplitEx(@"1a2A3a4A5", "a", StringComparison.Ordinal);
            Assert.AreEqual(3, strs.Count);
            Assert.IsTrue(strs[0].Equals("1", StringComparison.Ordinal));
            Assert.IsTrue(strs[1].Equals("2A3", StringComparison.Ordinal));
            Assert.IsTrue(strs[2].Equals("4A5", StringComparison.Ordinal));

            strs = StringHelper.SplitEx(@"1a2A3a4A5", "a", StringComparison.OrdinalIgnoreCase);
            Assert.AreEqual(5, strs.Count);
            Assert.IsTrue(strs[0].Equals("1", StringComparison.Ordinal));
            Assert.IsTrue(strs[1].Equals("2", StringComparison.Ordinal));
            Assert.IsTrue(strs[2].Equals("3", StringComparison.Ordinal));
            Assert.IsTrue(strs[3].Equals("4", StringComparison.Ordinal));
            Assert.IsTrue(strs[4].Equals("5", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ReplaceEx()
        {
            // Single replace test
            string str = StringHelper.ReplaceEx(@"ABCD", "AB", "XYZ", StringComparison.Ordinal);
            Assert.IsTrue(str.Equals("XYZCD", StringComparison.Ordinal));

            str = StringHelper.ReplaceEx(@"ABCD", "ab", "XYZ", StringComparison.Ordinal);
            Assert.IsTrue(str.Equals("ABCD", StringComparison.Ordinal));

            str = StringHelper.ReplaceEx(@"abcd", "AB", "XYZ", StringComparison.OrdinalIgnoreCase);
            Assert.IsTrue(str.Equals("XYZcd", StringComparison.Ordinal));

            str = StringHelper.ReplaceEx(@"abcd", "ab", "XYZ", StringComparison.OrdinalIgnoreCase);
            Assert.IsTrue(str.Equals("XYZcd", StringComparison.Ordinal));

            // Multiple replace test
            (string, string)[] replaceMap = new (string, string)[]
            {
                ("bc", "BC"),
                ("ef", "EF"),
                ("fg", "FG"),
            };
            str = StringHelper.ReplaceEx(@"abcdefg", replaceMap, StringComparison.Ordinal);
            Assert.IsTrue(str.Equals("aBCdEFg", StringComparison.Ordinal));
        }
    }
}
