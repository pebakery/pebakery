/*
    Copyright (C) 2019-2022 Hajin Jang
 
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
using System.Windows.Media;

namespace PEBakery.Helper.Tests
{
    [TestClass]
    public class SilentDictParserTests
    {
        #region Test Dictionary, Enum
        private static readonly Dictionary<string, string?> TestDict = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Null"] = null,
            ["Str"] = "ing",
            ["Int1"] = "100",
            ["Int2"] = "0x100",
            ["Int3"] = "-1",
            ["StrEnum1"] = "First",
            ["StrEnum2"] = "sECOND",
            ["StrEnum3"] = "LAST",
            ["IntEnum1"] = "0x10",
            ["IntEnum2"] = "0x02",
            ["IntEnum3"] = "0x40",
            ["Color1"] = "230, 0, 0",
            ["Color2"] = "16, 32, 64",
            ["Color3"] = "0xFF, 0xA0, 0",
        };

        private enum TestEnum
        {
            None = 0,
            First = 0x10,
            Second = 0x20,
            Third = 0x40,
        }
        #endregion

        #region ParseString
        [TestMethod]
        [TestCategory("Helper")]
        [TestCategory("SilentDictParser")]
        public void ParseString()
        {
            string? result = SilentDictParser.ParseStringNullable(TestDict, "Str", "Default", out bool notFound);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Equals("ing", StringComparison.Ordinal));
            Assert.IsFalse(notFound);
            result = SilentDictParser.ParseStringNullDefault(TestDict, "Str", null, out notFound);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Equals("ing", StringComparison.Ordinal));
            Assert.IsFalse(notFound);
            result = SilentDictParser.ParseStringNullable(TestDict, "None", "Default", out notFound);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Equals("Default", StringComparison.Ordinal));
            Assert.IsTrue(notFound);
            result = SilentDictParser.ParseStringNullDefault(TestDict, "None", null, out notFound);
            Assert.IsNull(result);
            Assert.IsTrue(notFound);
            result = SilentDictParser.ParseStringNullable(TestDict, "Null", "Default", out notFound);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Equals("Default", StringComparison.Ordinal));
            Assert.IsTrue(notFound);
            result = SilentDictParser.ParseStringNullDefault(TestDict, "Null", null, out notFound);
            Assert.IsNull(result);
            Assert.IsTrue(notFound);

            result = SilentDictParser.ParseStringNullable(TestDict, "Str", "Default");
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Equals("ing", StringComparison.Ordinal));
            result = SilentDictParser.ParseStringNullDefault(TestDict, "Str", null);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Equals("ing", StringComparison.Ordinal));
            result = SilentDictParser.ParseStringNullable(TestDict, "None", "Default");
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Equals("Default", StringComparison.Ordinal));
            result = SilentDictParser.ParseStringNullDefault(TestDict, "None", null);
            Assert.IsNull(result);
            result = SilentDictParser.ParseStringNullable(TestDict, "Null", "Default");
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Equals("Default", StringComparison.Ordinal));
            result = SilentDictParser.ParseStringNullDefault(TestDict, "Null", null);
            Assert.IsNull(result);
        }
        #endregion

        #region ParseInteger
        [TestMethod]
        [TestCategory("Helper")]
        [TestCategory("SilentDictParser")]
        public void ParseInteger()
        {
            int result = SilentDictParser.ParseIntegerNullable(TestDict, "Int1", 0, out bool notFound);
            Assert.AreEqual(100, result);
            Assert.IsFalse(notFound);
            result = SilentDictParser.ParseIntegerNullable(TestDict, "Int2", 0, out notFound);
            Assert.AreEqual(0x100, result);
            Assert.IsFalse(notFound);
            result = SilentDictParser.ParseIntegerNullable(TestDict, "Int3", 0, out notFound);
            Assert.AreEqual(-1, result);
            Assert.IsFalse(notFound);
            result = SilentDictParser.ParseIntegerNullable(TestDict, "None", 0, out notFound);
            Assert.AreEqual(0, result);
            Assert.IsTrue(notFound);
            result = SilentDictParser.ParseIntegerNullable(TestDict, "None", -128, out notFound);
            Assert.AreEqual(-128, result);
            Assert.IsTrue(notFound);

            result = SilentDictParser.ParseIntegerNullable(TestDict, "Int1", 0);
            Assert.AreEqual(100, result);
            result = SilentDictParser.ParseIntegerNullable(TestDict, "Int2", 0);
            Assert.AreEqual(0x100, result);
            result = SilentDictParser.ParseIntegerNullable(TestDict, "Int3", 0);
            Assert.AreEqual(-1, result);
            result = SilentDictParser.ParseIntegerNullable(TestDict, "None", 0);
            Assert.AreEqual(0, result);
            result = SilentDictParser.ParseIntegerNullable(TestDict, "None", -128);
            Assert.AreEqual(-128, result);
        }
        #endregion

        #region ParseStrEnum
        [TestMethod]
        [TestCategory("Helper")]
        [TestCategory("SilentDictParser")]
        public void ParseStrEnum()
        {
            TestEnum result = SilentDictParser.ParseStrEnumNullable(TestDict, "StrEnum1", TestEnum.None, out bool notFound);
            Assert.AreEqual(TestEnum.First, result);
            Assert.IsFalse(notFound);
            result = SilentDictParser.ParseStrEnumNullable(TestDict, "StrEnum2", TestEnum.None, out notFound);
            Assert.AreEqual(TestEnum.Second, result);
            Assert.IsFalse(notFound);
            result = SilentDictParser.ParseStrEnumNullable(TestDict, "StrEnum3", TestEnum.None, out notFound);
            Assert.AreEqual(TestEnum.None, result);
            Assert.IsTrue(notFound);
            result = SilentDictParser.ParseStrEnumNullable(TestDict, "None", TestEnum.None, out notFound);
            Assert.AreEqual(TestEnum.None, result);
            Assert.IsTrue(notFound);
            result = SilentDictParser.ParseStrEnumNullable(TestDict, "Null", TestEnum.None, out notFound);
            Assert.AreEqual(TestEnum.None, result);
            Assert.IsTrue(notFound);

            result = SilentDictParser.ParseStrEnumNullable(TestDict, "StrEnum1", TestEnum.None);
            Assert.AreEqual(TestEnum.First, result);
            result = SilentDictParser.ParseStrEnumNullable(TestDict, "StrEnum2", TestEnum.None);
            Assert.AreEqual(TestEnum.Second, result);
            result = SilentDictParser.ParseStrEnumNullable(TestDict, "StrEnum3", TestEnum.None);
            Assert.AreEqual(TestEnum.None, result);
            result = SilentDictParser.ParseStrEnumNullable(TestDict, "None", TestEnum.None);
            Assert.AreEqual(TestEnum.None, result);
            result = SilentDictParser.ParseStrEnumNullable(TestDict, "Null", TestEnum.None);
            Assert.AreEqual(TestEnum.None, result);
        }
        #endregion

        #region ParseIntEnum
        [TestMethod]
        [TestCategory("Helper")]
        [TestCategory("SilentDictParser")]
        public void ParseIntEnum()
        {
            TestEnum result = SilentDictParser.ParseIntEnumNullable(TestDict, "IntEnum1", TestEnum.None, out bool notFound);
            Assert.AreEqual(TestEnum.First, result);
            Assert.IsFalse(notFound);
            result = SilentDictParser.ParseIntEnumNullable(TestDict, "IntEnum2", TestEnum.None, out notFound);
            Assert.AreEqual(TestEnum.None, result);
            Assert.IsTrue(notFound);
            result = SilentDictParser.ParseIntEnumNullable(TestDict, "IntEnum3", TestEnum.None, out notFound);
            Assert.AreEqual(TestEnum.Third, result);
            Assert.IsFalse(notFound);
            result = SilentDictParser.ParseIntEnumNullable(TestDict, "None", TestEnum.None, out notFound);
            Assert.AreEqual(TestEnum.None, result);
            Assert.IsTrue(notFound);
            result = SilentDictParser.ParseIntEnumNullable(TestDict, "Null", TestEnum.None, out notFound);
            Assert.AreEqual(TestEnum.None, result);
            Assert.IsTrue(notFound);

            result = SilentDictParser.ParseIntEnumNullable(TestDict, "IntEnum1", TestEnum.None);
            Assert.AreEqual(TestEnum.First, result);
            result = SilentDictParser.ParseIntEnumNullable(TestDict, "IntEnum2", TestEnum.None);
            Assert.AreEqual(TestEnum.None, result);
            result = SilentDictParser.ParseIntEnumNullable(TestDict, "IntEnum3", TestEnum.None);
            Assert.AreEqual(TestEnum.Third, result);
            result = SilentDictParser.ParseIntEnumNullable(TestDict, "None", TestEnum.None);
            Assert.AreEqual(TestEnum.None, result);
            result = SilentDictParser.ParseIntEnumNullable(TestDict, "Null", TestEnum.None);
            Assert.AreEqual(TestEnum.None, result);
        }
        #endregion

        #region ParseColor
        [TestMethod]
        [TestCategory("Helper")]
        [TestCategory("SilentDictParser")]
        public void ParseColor()
        {
            Color result = SilentDictParser.ParseColorNullable(TestDict, "Color1", Color.FromRgb(255, 255, 255), out bool notFound);
            Assert.AreEqual(Color.FromRgb(230, 0, 0), result);
            Assert.IsFalse(notFound);
            result = SilentDictParser.ParseColorNullable(TestDict, "Color2", Color.FromRgb(255, 255, 255), out notFound);
            Assert.AreEqual(Color.FromRgb(16, 32, 64), result);
            Assert.IsFalse(notFound);
            result = SilentDictParser.ParseColorNullable(TestDict, "Color3", Color.FromRgb(255, 255, 255), out notFound);
            Assert.AreEqual(Color.FromRgb(255, 255, 255), result);
            Assert.IsTrue(notFound);
            result = SilentDictParser.ParseColorNullable(TestDict, "None", Color.FromRgb(255, 255, 255), out notFound);
            Assert.AreEqual(Color.FromRgb(255, 255, 255), result);
            Assert.IsTrue(notFound);
            result = SilentDictParser.ParseColorNullable(TestDict, "Null", Color.FromRgb(255, 255, 255), out notFound);
            Assert.AreEqual(Color.FromRgb(255, 255, 255), result);
            Assert.IsTrue(notFound);

            result = SilentDictParser.ParseColorNullable(TestDict, "Color1", Color.FromRgb(255, 255, 255));
            Assert.AreEqual(Color.FromRgb(230, 0, 0), result);
            result = SilentDictParser.ParseColorNullable(TestDict, "Color2", Color.FromRgb(255, 255, 255));
            Assert.AreEqual(Color.FromRgb(16, 32, 64), result);
            result = SilentDictParser.ParseColorNullable(TestDict, "Color3", Color.FromRgb(255, 255, 255));
            Assert.AreEqual(Color.FromRgb(255, 255, 255), result);
            result = SilentDictParser.ParseColorNullable(TestDict, "None", Color.FromRgb(255, 255, 255));
            Assert.AreEqual(Color.FromRgb(255, 255, 255), result);
            result = SilentDictParser.ParseColorNullable(TestDict, "Null", Color.FromRgb(255, 255, 255));
            Assert.AreEqual(Color.FromRgb(255, 255, 255), result);
        }
        #endregion
    }
}
