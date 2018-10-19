using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace PEBakery.Helper.Tests
{
    #region StringHelper
    [TestClass]
    public class StringHelperTests
    {
        [TestMethod]
        [TestCategory("Helper")]
        [TestCategory("StringHelper")]
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
        [TestCategory("Helper")]
        [TestCategory("StringHelper")]
        public void ReplaceEx()
        {
            string str = StringHelper.ReplaceEx(@"ABCD", "AB", "XYZ", StringComparison.Ordinal);
            Assert.IsTrue(str.Equals("XYZCD", StringComparison.Ordinal));

            str = StringHelper.ReplaceEx(@"ABCD", "ab", "XYZ", StringComparison.Ordinal);
            Assert.IsTrue(str.Equals("ABCD", StringComparison.Ordinal));

            str = StringHelper.ReplaceEx(@"abcd", "AB", "XYZ", StringComparison.OrdinalIgnoreCase);
            Assert.IsTrue(str.Equals("XYZcd", StringComparison.Ordinal));

            str = StringHelper.ReplaceEx(@"abcd", "ab", "XYZ", StringComparison.OrdinalIgnoreCase);
            Assert.IsTrue(str.Equals("XYZcd", StringComparison.Ordinal));
        }
    }
    #endregion
}
