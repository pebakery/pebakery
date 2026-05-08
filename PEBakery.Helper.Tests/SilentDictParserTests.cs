/*
    Copyright (C) 2019-present Hajin Jang
 
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
        private static readonly Dictionary<string, string?> NullableTestDict = new Dictionary<string, string?>(StringComparer.Ordinal)
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

        private static readonly Dictionary<string, string> NonNullableTestDict = new Dictionary<string, string>(StringComparer.Ordinal)
        {
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
        public void ParseString()
        {
            // Non-Nullable
            {
                string result = SilentDictParser.ParseString(NonNullableTestDict, "Str", "Default");
                Assert.IsTrue(result.Equals("ing", StringComparison.Ordinal));
                result = SilentDictParser.ParseString(NonNullableTestDict, "None", "Default");
                Assert.IsTrue(result.Equals("Default", StringComparison.Ordinal));
                result = SilentDictParser.ParseString(NonNullableTestDict, "Color3", "Default");
                Assert.IsTrue(result.Equals("0xFF, 0xA0, 0", StringComparison.Ordinal));
            }

            // Nullable
            {
                string? result = SilentDictParser.ParseStringNullable(NullableTestDict, "Str", "Default");
                Assert.IsNotNull(result);
                Assert.IsTrue(result.Equals("ing", StringComparison.Ordinal));
                result = SilentDictParser.ParseStringNullDefault(NullableTestDict, "Str", null);
                Assert.IsNotNull(result);
                Assert.IsTrue(result.Equals("ing", StringComparison.Ordinal));
                result = SilentDictParser.ParseStringNullable(NullableTestDict, "None", "Default");
                Assert.IsNotNull(result);
                Assert.IsTrue(result.Equals("Default", StringComparison.Ordinal));
                result = SilentDictParser.ParseStringNullDefault(NullableTestDict, "None", null);
                Assert.IsNull(result);
                result = SilentDictParser.ParseStringNullable(NullableTestDict, "Null", "Default");
                Assert.IsNotNull(result);
                Assert.IsTrue(result.Equals("Default", StringComparison.Ordinal));
                result = SilentDictParser.ParseStringNullDefault(NullableTestDict, "Null", null);
                Assert.IsNull(result);
            }
        }

        [TestMethod]
        public void ParseStringFallback()
        {
            // Non-Nullable with fallbackKeys
            {
                string result = SilentDictParser.ParseString(NonNullableTestDict, "Str", ["Int1", "Color1"], "Default");
                Assert.IsTrue(result.Equals("ing", StringComparison.Ordinal));
                result = SilentDictParser.ParseString(NonNullableTestDict, "None", ["Int1", "Color1"], "Default");
                Assert.IsTrue(result.Equals("100", StringComparison.Ordinal));
                result = SilentDictParser.ParseString(NonNullableTestDict, "None", ["Invalid", "Color1"], "Default");
                Assert.IsTrue(result.Equals("230, 0, 0", StringComparison.Ordinal));
                result = SilentDictParser.ParseString(NonNullableTestDict, "None", ["Invalid"], "Default");
                Assert.IsTrue(result.Equals("Default", StringComparison.Ordinal));
            }
            
            // Nullable with fallbackKeys
            {
                string? result = SilentDictParser.ParseStringNullable(NullableTestDict, "Str", ["Int1", "Color1"], "Default");
                Assert.IsNotNull(result);
                Assert.IsTrue(result.Equals("ing", StringComparison.Ordinal));
                result = SilentDictParser.ParseStringNullDefault(NullableTestDict, "None", ["Int1", "Color1"], null);
                Assert.IsNotNull(result);
                Assert.IsTrue(result.Equals("100", StringComparison.Ordinal));
                result = SilentDictParser.ParseStringNullable(NullableTestDict, "None", ["Invalid", "Color1"], "Default");
                Assert.IsNotNull(result);
                Assert.IsTrue(result.Equals("230, 0, 0", StringComparison.Ordinal));
                result = SilentDictParser.ParseStringNullDefault(NullableTestDict, "None", ["Invalid"], null);
                Assert.IsNull(result);
            }
        }
        #endregion

        #region ParseInteger
        [TestMethod]
        public void ParseInteger()
        {
            // With unparsableValue + Without nullable
            {
                int result = SilentDictParser.ParseInteger(NonNullableTestDict, "Int1", 0, out bool unparsableValue);
                Assert.AreEqual(100, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseInteger(NonNullableTestDict, "Int2", 0, out unparsableValue);
                Assert.AreEqual(0x100, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseInteger(NonNullableTestDict, "Int3", 0, out unparsableValue);
                Assert.AreEqual(-1, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseInteger(NonNullableTestDict, "None", 0, out unparsableValue);
                Assert.AreEqual(0, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseInteger(NonNullableTestDict, "None", -128, out unparsableValue);
                Assert.AreEqual(-128, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseInteger(NonNullableTestDict, "Str", 128, out unparsableValue);
                Assert.AreEqual(128, result);
                Assert.IsTrue(unparsableValue);
            }

            // Without unparsableValue + Without nullable
            {
                int result = SilentDictParser.ParseInteger(NonNullableTestDict, "Int1", 0);
                Assert.AreEqual(100, result);
                result = SilentDictParser.ParseInteger(NonNullableTestDict, "Int2", 0);
                Assert.AreEqual(0x100, result);
                result = SilentDictParser.ParseInteger(NonNullableTestDict, "Int3", 0);
                Assert.AreEqual(-1, result);
                result = SilentDictParser.ParseInteger(NonNullableTestDict, "None", 0);
                Assert.AreEqual(0, result);
                result = SilentDictParser.ParseInteger(NonNullableTestDict, "None", -128);
                Assert.AreEqual(-128, result);
                result = SilentDictParser.ParseInteger(NonNullableTestDict, "Str", 128);
                Assert.AreEqual(128, result);
            }

            // With unparsableValue + With nullable
            {
                int result = SilentDictParser.ParseIntegerNullable(NullableTestDict, "Int1", 0, out bool unparsableValue);
                Assert.AreEqual(100, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseIntegerNullable(NullableTestDict, "Int2", 0, out unparsableValue);
                Assert.AreEqual(0x100, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseIntegerNullable(NullableTestDict, "Int3", 0, out unparsableValue);
                Assert.AreEqual(-1, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseIntegerNullable(NullableTestDict, "None", 0, out unparsableValue);
                Assert.AreEqual(0, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseIntegerNullable(NullableTestDict, "None", -128, out unparsableValue);
                Assert.AreEqual(-128, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseIntegerNullable(NullableTestDict, "Str", 128, out unparsableValue);
                Assert.AreEqual(128, result);
                Assert.IsTrue(unparsableValue);
            }

            // Without unparsableValue + With nullable
            {
                int result = SilentDictParser.ParseIntegerNullable(NullableTestDict, "Int1", 0);
                Assert.AreEqual(100, result);
                result = SilentDictParser.ParseIntegerNullable(NullableTestDict, "Int2", 0);
                Assert.AreEqual(0x100, result);
                result = SilentDictParser.ParseIntegerNullable(NullableTestDict, "Int3", 0);
                Assert.AreEqual(-1, result);
                result = SilentDictParser.ParseIntegerNullable(NullableTestDict, "None", 0);
                Assert.AreEqual(0, result);
                result = SilentDictParser.ParseIntegerNullable(NullableTestDict, "None", -128);
                Assert.AreEqual(-128, result);
                result = SilentDictParser.ParseIntegerNullable(NullableTestDict, "Str", 128);
                Assert.AreEqual(128, result);
            }
        }

        [TestMethod]
        public void ParseIntegerFallback()
        {
            // With unparsableValue + Without nullable
            {
                int result = SilentDictParser.ParseInteger(NonNullableTestDict, "Int1", ["Int2", "Int3"], 0, out bool unparsableValue);
                Assert.AreEqual(100, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseInteger(NonNullableTestDict, "Int4", ["Int2", "Int3"], 0, out unparsableValue);
                Assert.AreEqual(0x100, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseInteger(NonNullableTestDict, "Int3", [], 0, out unparsableValue);
                Assert.AreEqual(-1, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseInteger(NonNullableTestDict, "None", [], 0, out unparsableValue);
                Assert.AreEqual(0, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseInteger(NonNullableTestDict, "None", [], -128, out unparsableValue);
                Assert.AreEqual(-128, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseInteger(NonNullableTestDict, "Str", ["Int3"], 128, out unparsableValue);
                Assert.AreEqual(128, result);
                Assert.IsTrue(unparsableValue);
            }

            // Without unparsableValue + Without nullable
            {
                int result = SilentDictParser.ParseInteger(NonNullableTestDict, "Int1", ["Int2", "Int3"], 0);
                Assert.AreEqual(100, result);
                result = SilentDictParser.ParseInteger(NonNullableTestDict, "Int4", ["Int2", "Int3"], 0);
                Assert.AreEqual(0x100, result);
                result = SilentDictParser.ParseInteger(NonNullableTestDict, "Int3", [], 0);
                Assert.AreEqual(-1, result);
                result = SilentDictParser.ParseInteger(NonNullableTestDict, "None", [], 0);
                Assert.AreEqual(0, result);
                result = SilentDictParser.ParseInteger(NonNullableTestDict, "None", [],  -128);
                Assert.AreEqual(-128, result);
                result = SilentDictParser.ParseInteger(NonNullableTestDict, "Str", ["Int3"], 128);
                Assert.AreEqual(128, result);
            }

            // With unparsableValue + With nullable
            {
                int result = SilentDictParser.ParseIntegerNullable(NullableTestDict, "Int1", ["Int2", "Int3"], 0, out bool unparsableValue);
                Assert.AreEqual(100, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseIntegerNullable(NullableTestDict, "Int4", ["Int2", "Int3"], 0, out unparsableValue);
                Assert.AreEqual(0x100, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseIntegerNullable(NullableTestDict, "Int3", [], 0, out unparsableValue);
                Assert.AreEqual(-1, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseIntegerNullable(NullableTestDict, "None", [], 0, out unparsableValue);
                Assert.AreEqual(0, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseIntegerNullable(NullableTestDict, "None", [], -128, out unparsableValue);
                Assert.AreEqual(-128, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseIntegerNullable(NullableTestDict, "Str", ["Int3"], 128, out unparsableValue);
                Assert.AreEqual(128, result);
                Assert.IsTrue(unparsableValue);
            }

            // Without unparsableValue + With nullable
            {
                int result = SilentDictParser.ParseIntegerNullable(NullableTestDict, "Int1", ["Int2", "Int3"], 0);
                Assert.AreEqual(100, result);
                result = SilentDictParser.ParseIntegerNullable(NullableTestDict, "Int2", ["Int2", "Int3"], 0);
                Assert.AreEqual(0x100, result);
                result = SilentDictParser.ParseIntegerNullable(NullableTestDict, "Int3", [], 0);
                Assert.AreEqual(-1, result);
                result = SilentDictParser.ParseIntegerNullable(NullableTestDict, "None", [], 0);
                Assert.AreEqual(0, result);
                result = SilentDictParser.ParseIntegerNullable(NullableTestDict, "None", [], -128);
                Assert.AreEqual(-128, result);
                result = SilentDictParser.ParseIntegerNullable(NullableTestDict, "Str", ["Int3"], 128);
                Assert.AreEqual(128, result);
            }
        }
        #endregion

        #region ParseStrEnum
        [TestMethod]
        public void ParseStrEnum()
        {
            // With unparsableValue + Without Nullable
            {
                TestEnum result = SilentDictParser.ParseStrEnum(NonNullableTestDict, "StrEnum1", TestEnum.None, out bool unparsableValue);
                Assert.AreEqual(TestEnum.First, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseStrEnum(NonNullableTestDict, "StrEnum2", TestEnum.None, out unparsableValue);
                Assert.AreEqual(TestEnum.Second, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseStrEnum(NonNullableTestDict, "StrEnum3", TestEnum.None, out unparsableValue);
                Assert.AreEqual(TestEnum.None, result);
                Assert.IsTrue(unparsableValue);
                result = SilentDictParser.ParseStrEnum(NonNullableTestDict, "None", TestEnum.None, out unparsableValue);
                Assert.AreEqual(TestEnum.None, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseStrEnum(NonNullableTestDict, "Null", TestEnum.None, out unparsableValue);
                Assert.AreEqual(TestEnum.None, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseStrEnum(NonNullableTestDict, "Int3", TestEnum.None, out unparsableValue);
                Assert.AreEqual(TestEnum.None, result);
                Assert.IsTrue(unparsableValue);
            }

            // Without unparsableValue + Without Nullable
            {
                TestEnum result = SilentDictParser.ParseStrEnum(NonNullableTestDict, "StrEnum1", TestEnum.None);
                Assert.AreEqual(TestEnum.First, result);
                result = SilentDictParser.ParseStrEnum(NonNullableTestDict, "StrEnum2", TestEnum.None);
                Assert.AreEqual(TestEnum.Second, result);
                result = SilentDictParser.ParseStrEnum(NonNullableTestDict, "StrEnum3", TestEnum.None);
                Assert.AreEqual(TestEnum.None, result);
                result = SilentDictParser.ParseStrEnum(NonNullableTestDict, "None", TestEnum.None);
                Assert.AreEqual(TestEnum.None, result);
                result = SilentDictParser.ParseStrEnum(NonNullableTestDict, "Null", TestEnum.None);
                Assert.AreEqual(TestEnum.None, result);
                result = SilentDictParser.ParseStrEnum(NonNullableTestDict, "Int3", TestEnum.None);
                Assert.AreEqual(TestEnum.None, result);
            }

            // With unparsableValue + With Nullable
            {
                TestEnum result = SilentDictParser.ParseStrEnumNullable(NullableTestDict, "StrEnum1", TestEnum.None, out bool unparsableValue);
                Assert.AreEqual(TestEnum.First, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseStrEnumNullable(NullableTestDict, "StrEnum2", TestEnum.None, out unparsableValue);
                Assert.AreEqual(TestEnum.Second, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseStrEnumNullable(NullableTestDict, "StrEnum3", TestEnum.None, out unparsableValue);
                Assert.AreEqual(TestEnum.None, result);
                Assert.IsTrue(unparsableValue);
                result = SilentDictParser.ParseStrEnumNullable(NullableTestDict, "None", TestEnum.None, out unparsableValue);
                Assert.AreEqual(TestEnum.None, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseStrEnumNullable(NullableTestDict, "Null", TestEnum.None, out unparsableValue);
                Assert.AreEqual(TestEnum.None, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseStrEnumNullable(NullableTestDict, "Int3", TestEnum.None, out unparsableValue);
                Assert.AreEqual(TestEnum.None, result);
                Assert.IsTrue(unparsableValue);
            }

            // Without unparsableValue + With Nullable
            {
                TestEnum result = SilentDictParser.ParseStrEnumNullable(NullableTestDict, "StrEnum1", TestEnum.None);
                Assert.AreEqual(TestEnum.First, result);
                result = SilentDictParser.ParseStrEnumNullable(NullableTestDict, "StrEnum2", TestEnum.None);
                Assert.AreEqual(TestEnum.Second, result);
                result = SilentDictParser.ParseStrEnumNullable(NullableTestDict, "StrEnum3", TestEnum.None);
                Assert.AreEqual(TestEnum.None, result);
                result = SilentDictParser.ParseStrEnumNullable(NullableTestDict, "None", TestEnum.None);
                Assert.AreEqual(TestEnum.None, result);
                result = SilentDictParser.ParseStrEnumNullable(NullableTestDict, "Null", TestEnum.None);
                Assert.AreEqual(TestEnum.None, result);
                result = SilentDictParser.ParseStrEnumNullable(NullableTestDict, "Int3", TestEnum.None);
                Assert.AreEqual(TestEnum.None, result);
            }
        }

        [TestMethod]
        public void ParseStrEnumFallback()
        {
            // With unparsableValue + Without Nullable
            {
                TestEnum result = SilentDictParser.ParseStrEnum(NonNullableTestDict, "StrEnum1", ["StrEnum2", "StrEnum3"], TestEnum.None, out bool unparsableValue);
                Assert.AreEqual(TestEnum.First, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseStrEnum(NonNullableTestDict, "StrEnum4", ["StrEnum2", "StrEnum3"], TestEnum.None, out unparsableValue);
                Assert.AreEqual(TestEnum.Second, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseStrEnum(NonNullableTestDict, "StrEnum3", [], TestEnum.None, out unparsableValue);
                Assert.AreEqual(TestEnum.None, result);
                Assert.IsTrue(unparsableValue);
                result = SilentDictParser.ParseStrEnum(NonNullableTestDict, "None", [], TestEnum.None, out unparsableValue);
                Assert.AreEqual(TestEnum.None, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseStrEnum(NonNullableTestDict, "Null", [], TestEnum.None, out unparsableValue);
                Assert.AreEqual(TestEnum.None, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseStrEnum(NonNullableTestDict, "Int3", ["Str3"], TestEnum.None, out unparsableValue);
                Assert.AreEqual(TestEnum.None, result);
                Assert.IsTrue(unparsableValue);
            }

            // Without unparsableValue + Without Nullable
            {
                TestEnum result = SilentDictParser.ParseStrEnum(NonNullableTestDict, "StrEnum1", ["StrEnum2", "StrEnum3"], TestEnum.None);
                Assert.AreEqual(TestEnum.First, result);
                result = SilentDictParser.ParseStrEnum(NonNullableTestDict, "StrEnum4", ["StrEnum2", "StrEnum3"], TestEnum.None);
                Assert.AreEqual(TestEnum.Second, result);
                result = SilentDictParser.ParseStrEnum(NonNullableTestDict, "StrEnum3", [], TestEnum.None);
                Assert.AreEqual(TestEnum.None, result);
                result = SilentDictParser.ParseStrEnum(NonNullableTestDict, "None", [], TestEnum.None);
                Assert.AreEqual(TestEnum.None, result);
                result = SilentDictParser.ParseStrEnum(NonNullableTestDict, "Null", [], TestEnum.None);
                Assert.AreEqual(TestEnum.None, result);
                result = SilentDictParser.ParseStrEnum(NonNullableTestDict, "Int3", ["Str3"], TestEnum.None);
                Assert.AreEqual(TestEnum.None, result);
            }

            // With unparsableValue + With Nullable
            {
                TestEnum result = SilentDictParser.ParseStrEnumNullable(NullableTestDict, "StrEnum1", ["StrEnum2", "StrEnum3"], TestEnum.None, out bool unparsableValue);
                Assert.AreEqual(TestEnum.First, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseStrEnumNullable(NullableTestDict, "StrEnum4", ["StrEnum2", "StrEnum3"], TestEnum.None, out unparsableValue);
                Assert.AreEqual(TestEnum.Second, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseStrEnumNullable(NullableTestDict, "StrEnum3", [], TestEnum.None, out unparsableValue);
                Assert.AreEqual(TestEnum.None, result);
                Assert.IsTrue(unparsableValue);
                result = SilentDictParser.ParseStrEnumNullable(NullableTestDict, "None", [], TestEnum.None, out unparsableValue);
                Assert.AreEqual(TestEnum.None, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseStrEnumNullable(NullableTestDict, "Null", [], TestEnum.None, out unparsableValue);
                Assert.AreEqual(TestEnum.None, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseStrEnumNullable(NullableTestDict, "Int3", ["Str3"], TestEnum.None, out unparsableValue);
                Assert.AreEqual(TestEnum.None, result);
                Assert.IsTrue(unparsableValue);
            }

            // Without unparsableValue + With Nullable
            {
                TestEnum result = SilentDictParser.ParseStrEnumNullable(NullableTestDict, "StrEnum1", ["StrEnum2", "StrEnum3"], TestEnum.None);
                Assert.AreEqual(TestEnum.First, result);
                result = SilentDictParser.ParseStrEnumNullable(NullableTestDict, "StrEnum4", ["StrEnum2", "StrEnum3"], TestEnum.None);
                Assert.AreEqual(TestEnum.Second, result);
                result = SilentDictParser.ParseStrEnumNullable(NullableTestDict, "StrEnum3", [], TestEnum.None);
                Assert.AreEqual(TestEnum.None, result);
                result = SilentDictParser.ParseStrEnumNullable(NullableTestDict, "None", [], TestEnum.None);
                Assert.AreEqual(TestEnum.None, result);
                result = SilentDictParser.ParseStrEnumNullable(NullableTestDict, "Null", [], TestEnum.None);
                Assert.AreEqual(TestEnum.None, result);
                result = SilentDictParser.ParseStrEnumNullable(NullableTestDict, "Int3", ["Str3"], TestEnum.None);
                Assert.AreEqual(TestEnum.None, result);
            }
        }
        #endregion

        #region ParseIntEnum
        [TestMethod]
        public void ParseIntEnum()
        {
            // With unparsableValue + Without Nullable
            {
                TestEnum result = SilentDictParser.ParseIntEnum(NonNullableTestDict, "IntEnum1", TestEnum.None, out bool unparsableValue);
                Assert.AreEqual(TestEnum.First, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseIntEnum(NonNullableTestDict, "IntEnum2", TestEnum.None, out unparsableValue);
                Assert.AreEqual(TestEnum.None, result);
                Assert.IsTrue(unparsableValue);
                result = SilentDictParser.ParseIntEnum(NonNullableTestDict, "IntEnum3", TestEnum.None, out unparsableValue);
                Assert.AreEqual(TestEnum.Third, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseIntEnum(NonNullableTestDict, "None", TestEnum.None, out unparsableValue);
                Assert.AreEqual(TestEnum.None, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseIntEnum(NonNullableTestDict, "Null", TestEnum.None, out unparsableValue);
                Assert.AreEqual(TestEnum.None, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseIntEnum(NonNullableTestDict, "StrEnum3", TestEnum.None, out unparsableValue);
                Assert.AreEqual(TestEnum.None, result);
                Assert.IsTrue(unparsableValue);
            }

            // Without unparsableValue + Without Nullable
            {
                TestEnum result = SilentDictParser.ParseIntEnum(NonNullableTestDict, "IntEnum1", TestEnum.None);
                Assert.AreEqual(TestEnum.First, result);
                result = SilentDictParser.ParseIntEnum(NonNullableTestDict, "IntEnum2", TestEnum.None);
                Assert.AreEqual(TestEnum.None, result);
                result = SilentDictParser.ParseIntEnum(NonNullableTestDict, "IntEnum3", TestEnum.None);
                Assert.AreEqual(TestEnum.Third, result);
                result = SilentDictParser.ParseIntEnum(NonNullableTestDict, "None", TestEnum.None);
                Assert.AreEqual(TestEnum.None, result);
                result = SilentDictParser.ParseIntEnum(NonNullableTestDict, "Null", TestEnum.None);
                Assert.AreEqual(TestEnum.None, result);
                result = SilentDictParser.ParseIntEnum(NonNullableTestDict, "StrEnum3", TestEnum.None);
                Assert.AreEqual(TestEnum.None, result);
            }

            // With unparsableValue + With Nullable
            {
                TestEnum result = SilentDictParser.ParseIntEnumNullable(NullableTestDict, "IntEnum1", TestEnum.None, out bool unparsableValue);
                Assert.AreEqual(TestEnum.First, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseIntEnumNullable(NullableTestDict, "IntEnum2", TestEnum.None, out unparsableValue);
                Assert.AreEqual(TestEnum.None, result);
                Assert.IsTrue(unparsableValue);
                result = SilentDictParser.ParseIntEnumNullable(NullableTestDict, "IntEnum3", TestEnum.None, out unparsableValue);
                Assert.AreEqual(TestEnum.Third, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseIntEnumNullable(NullableTestDict, "None", TestEnum.None, out unparsableValue);
                Assert.AreEqual(TestEnum.None, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseIntEnumNullable(NullableTestDict, "Null", TestEnum.None, out unparsableValue);
                Assert.AreEqual(TestEnum.None, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseIntEnumNullable(NullableTestDict, "StrEnum3", TestEnum.None, out unparsableValue);
                Assert.AreEqual(TestEnum.None, result);
                Assert.IsTrue(unparsableValue);
            }

            // Without unparsableValue + With Nullable
            {
                TestEnum result = SilentDictParser.ParseIntEnumNullable(NullableTestDict, "IntEnum1", TestEnum.None);
                Assert.AreEqual(TestEnum.First, result);
                result = SilentDictParser.ParseIntEnumNullable(NullableTestDict, "IntEnum2", TestEnum.None);
                Assert.AreEqual(TestEnum.None, result);
                result = SilentDictParser.ParseIntEnumNullable(NullableTestDict, "IntEnum3", TestEnum.None);
                Assert.AreEqual(TestEnum.Third, result);
                result = SilentDictParser.ParseIntEnumNullable(NullableTestDict, "None", TestEnum.None);
                Assert.AreEqual(TestEnum.None, result);
                result = SilentDictParser.ParseIntEnumNullable(NullableTestDict, "Null", TestEnum.None);
                Assert.AreEqual(TestEnum.None, result);
                result = SilentDictParser.ParseIntEnumNullable(NullableTestDict, "StrEnum3", TestEnum.None);
                Assert.AreEqual(TestEnum.None, result);
            }
        }

        [TestMethod]
        public void ParseIntEnumFallback()
        {
            // With unparsableValue + Without Nullable
            {
                TestEnum result = SilentDictParser.ParseIntEnum(NonNullableTestDict, "IntEnum1", ["IntEnum2", "IntEnum3"], TestEnum.None, out bool unparsableValue);
                Assert.AreEqual(TestEnum.First, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseIntEnum(NonNullableTestDict, "IntEnum4", ["IntEnum2", "IntEnum3"], TestEnum.None, out unparsableValue);
                Assert.AreEqual(TestEnum.None, result);
                Assert.IsTrue(unparsableValue);
                result = SilentDictParser.ParseIntEnum(NonNullableTestDict, "IntEnum3", [], TestEnum.None, out unparsableValue);
                Assert.AreEqual(TestEnum.Third, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseIntEnum(NonNullableTestDict, "None", [], TestEnum.None, out unparsableValue);
                Assert.AreEqual(TestEnum.None, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseIntEnum(NonNullableTestDict, "None", ["IntEnum1"], TestEnum.None, out unparsableValue);
                Assert.AreEqual(TestEnum.First, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseIntEnum(NonNullableTestDict, "StrEnum3", ["IntEnum1"], TestEnum.None, out unparsableValue);
                Assert.AreEqual(TestEnum.None, result);
                Assert.IsTrue(unparsableValue);
            }

            // Without unparsableValue + Without Nullable
            {
                TestEnum result = SilentDictParser.ParseIntEnum(NonNullableTestDict, "IntEnum1", ["IntEnum2", "IntEnum3"], TestEnum.None);
                Assert.AreEqual(TestEnum.First, result);
                result = SilentDictParser.ParseIntEnum(NonNullableTestDict, "IntEnum4", ["IntEnum2", "IntEnum3"], TestEnum.None);
                Assert.AreEqual(TestEnum.None, result);
                result = SilentDictParser.ParseIntEnum(NonNullableTestDict, "IntEnum3", [], TestEnum.None);
                Assert.AreEqual(TestEnum.Third, result);
                result = SilentDictParser.ParseIntEnum(NonNullableTestDict, "None", [], TestEnum.None);
                Assert.AreEqual(TestEnum.None, result);
                result = SilentDictParser.ParseIntEnum(NonNullableTestDict, "None", ["IntEnum1"], TestEnum.None);
                Assert.AreEqual(TestEnum.First, result);
                result = SilentDictParser.ParseIntEnum(NonNullableTestDict, "StrEnum3", ["IntEnum1"], TestEnum.None);
                Assert.AreEqual(TestEnum.None, result);
            }

            // With unparsableValue + With Nullable
            {
                TestEnum result = SilentDictParser.ParseIntEnumNullable(NullableTestDict, "IntEnum1", ["IntEnum2", "IntEnum3"], TestEnum.None, out bool unparsableValue);
                Assert.AreEqual(TestEnum.First, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseIntEnumNullable(NullableTestDict, "IntEnum4", ["IntEnum2", "IntEnum3"], TestEnum.None, out unparsableValue);
                Assert.AreEqual(TestEnum.None, result);
                Assert.IsTrue(unparsableValue);
                result = SilentDictParser.ParseIntEnumNullable(NullableTestDict, "IntEnum3", [], TestEnum.None, out unparsableValue);
                Assert.AreEqual(TestEnum.Third, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseIntEnumNullable(NullableTestDict, "None", [], TestEnum.None, out unparsableValue);
                Assert.AreEqual(TestEnum.None, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseIntEnumNullable(NullableTestDict, "None", ["IntEnum1"], TestEnum.None, out unparsableValue);
                Assert.AreEqual(TestEnum.First, result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseIntEnumNullable(NullableTestDict, "StrEnum3", ["IntEnum1"], TestEnum.None, out unparsableValue);
                Assert.AreEqual(TestEnum.None, result);
                Assert.IsTrue(unparsableValue);
            }

            // Without unparsableValue + With Nullable
            {
                TestEnum result = SilentDictParser.ParseIntEnumNullable(NullableTestDict, "IntEnum1", ["IntEnum2", "IntEnum3"], TestEnum.None);
                Assert.AreEqual(TestEnum.First, result);
                result = SilentDictParser.ParseIntEnumNullable(NullableTestDict, "IntEnum4", ["IntEnum2", "IntEnum3"], TestEnum.None);
                Assert.AreEqual(TestEnum.None, result);
                result = SilentDictParser.ParseIntEnumNullable(NullableTestDict, "IntEnum3", [], TestEnum.None);
                Assert.AreEqual(TestEnum.Third, result);
                result = SilentDictParser.ParseIntEnumNullable(NullableTestDict, "None", [], TestEnum.None);
                Assert.AreEqual(TestEnum.None, result);
                result = SilentDictParser.ParseIntEnumNullable(NullableTestDict, "None", ["IntEnum1"], TestEnum.None);
                Assert.AreEqual(TestEnum.First, result);
                result = SilentDictParser.ParseIntEnumNullable(NullableTestDict, "StrEnum3", ["IntEnum1"], TestEnum.None);
                Assert.AreEqual(TestEnum.None, result);
            }
        }
        #endregion

        #region ParseColor
        [TestMethod]
        public void ParseColor()
        {
            // With unparsableValue + Without Nullable
            {
                Color result = SilentDictParser.ParseColor(NonNullableTestDict, "Color1", Color.FromRgb(255, 255, 255), out bool unparsableValue);
                Assert.AreEqual(Color.FromRgb(230, 0, 0), result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseColor(NonNullableTestDict, "Color2", Color.FromRgb(255, 255, 255), out unparsableValue);
                Assert.AreEqual(Color.FromRgb(16, 32, 64), result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseColor(NonNullableTestDict, "Color3", Color.FromRgb(255, 255, 255), out unparsableValue);
                Assert.AreEqual(Color.FromRgb(255, 255, 255), result);
                Assert.IsTrue(unparsableValue);
                result = SilentDictParser.ParseColor(NonNullableTestDict, "None", Color.FromRgb(255, 255, 255), out unparsableValue);
                Assert.AreEqual(Color.FromRgb(255, 255, 255), result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseColor(NonNullableTestDict, "Null", Color.FromRgb(255, 255, 255), out unparsableValue);
                Assert.AreEqual(Color.FromRgb(255, 255, 255), result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseColor(NonNullableTestDict, "Str", Color.FromRgb(255, 255, 255), out unparsableValue);
                Assert.AreEqual(Color.FromRgb(255, 255, 255), result);
                Assert.IsTrue(unparsableValue);
            }

            // Without unparsableValue + Without Nullable
            {
                Color result = SilentDictParser.ParseColor(NonNullableTestDict, "Color1", Color.FromRgb(255, 255, 255));
                Assert.AreEqual(Color.FromRgb(230, 0, 0), result);
                result = SilentDictParser.ParseColor(NonNullableTestDict, "Color2", Color.FromRgb(255, 255, 255));
                Assert.AreEqual(Color.FromRgb(16, 32, 64), result);
                result = SilentDictParser.ParseColor(NonNullableTestDict, "Color3", Color.FromRgb(255, 255, 255));
                Assert.AreEqual(Color.FromRgb(255, 255, 255), result);
                result = SilentDictParser.ParseColor(NonNullableTestDict, "None", Color.FromRgb(255, 255, 255));
                Assert.AreEqual(Color.FromRgb(255, 255, 255), result);
                result = SilentDictParser.ParseColor(NonNullableTestDict, "Null", Color.FromRgb(255, 255, 255));
                Assert.AreEqual(Color.FromRgb(255, 255, 255), result);
                result = SilentDictParser.ParseColor(NonNullableTestDict, "Str", Color.FromRgb(255, 255, 255));
                Assert.AreEqual(Color.FromRgb(255, 255, 255), result);
            }

            // With unparsableValue + With Nullable
            {
                Color result = SilentDictParser.ParseColorNullable(NullableTestDict, "Color1", Color.FromRgb(255, 255, 255), out bool unparsableValue);
                Assert.AreEqual(Color.FromRgb(230, 0, 0), result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseColorNullable(NullableTestDict, "Color2", Color.FromRgb(255, 255, 255), out unparsableValue);
                Assert.AreEqual(Color.FromRgb(16, 32, 64), result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseColorNullable(NullableTestDict, "Color3", Color.FromRgb(255, 255, 255), out unparsableValue);
                Assert.AreEqual(Color.FromRgb(255, 255, 255), result);
                Assert.IsTrue(unparsableValue);
                result = SilentDictParser.ParseColorNullable(NullableTestDict, "None", Color.FromRgb(255, 255, 255), out unparsableValue);
                Assert.AreEqual(Color.FromRgb(255, 255, 255), result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseColorNullable(NullableTestDict, "Null", Color.FromRgb(255, 255, 255), out unparsableValue);
                Assert.AreEqual(Color.FromRgb(255, 255, 255), result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseColorNullable(NullableTestDict, "Str", Color.FromRgb(255, 255, 255), out unparsableValue);
                Assert.AreEqual(Color.FromRgb(255, 255, 255), result);
                Assert.IsTrue(unparsableValue);
            }

            // Without unparsableValue + With Nullable
            {
                Color result = SilentDictParser.ParseColorNullable(NullableTestDict, "Color1", Color.FromRgb(255, 255, 255));
                Assert.AreEqual(Color.FromRgb(230, 0, 0), result);
                result = SilentDictParser.ParseColorNullable(NullableTestDict, "Color2", Color.FromRgb(255, 255, 255));
                Assert.AreEqual(Color.FromRgb(16, 32, 64), result);
                result = SilentDictParser.ParseColorNullable(NullableTestDict, "Color3", Color.FromRgb(255, 255, 255));
                Assert.AreEqual(Color.FromRgb(255, 255, 255), result);
                result = SilentDictParser.ParseColorNullable(NullableTestDict, "None", Color.FromRgb(255, 255, 255));
                Assert.AreEqual(Color.FromRgb(255, 255, 255), result);
                result = SilentDictParser.ParseColorNullable(NullableTestDict, "Null", Color.FromRgb(255, 255, 255));
                Assert.AreEqual(Color.FromRgb(255, 255, 255), result);
                result = SilentDictParser.ParseColorNullable(NullableTestDict, "Str", Color.FromRgb(255, 255, 255));
                Assert.AreEqual(Color.FromRgb(255, 255, 255), result);
            }
        }

        [TestMethod]
        public void ParseColorFallback()
        {
            // With unparsableValue + Without Nullable
            {
                Color result = SilentDictParser.ParseColor(NonNullableTestDict, "Color1", ["Color2", "Color3"], Color.FromRgb(255, 255, 255), out bool unparsableValue);
                Assert.AreEqual(Color.FromRgb(230, 0, 0), result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseColor(NonNullableTestDict, "Color4", ["Color2", "Color3"], Color.FromRgb(255, 255, 255), out unparsableValue);
                Assert.AreEqual(Color.FromRgb(16, 32, 64), result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseColor(NonNullableTestDict, "Color3", [], Color.FromRgb(255, 255, 255), out unparsableValue);
                Assert.AreEqual(Color.FromRgb(255, 255, 255), result);
                Assert.IsTrue(unparsableValue);
                result = SilentDictParser.ParseColor(NonNullableTestDict, "None", [], Color.FromRgb(255, 255, 255), out unparsableValue);
                Assert.AreEqual(Color.FromRgb(255, 255, 255), result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseColor(NonNullableTestDict, "None", ["Color1"], Color.FromRgb(255, 255, 255), out unparsableValue);
                Assert.AreEqual(Color.FromRgb(230, 0, 0), result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseColor(NonNullableTestDict, "Str", [], Color.FromRgb(255, 255, 255), out unparsableValue);
                Assert.AreEqual(Color.FromRgb(255, 255, 255), result);
                Assert.IsTrue(unparsableValue);
            }

            // Without unparsableValue + Without Nullable
            {
                Color result = SilentDictParser.ParseColor(NonNullableTestDict, "Color1", ["Color2", "Color3"], Color.FromRgb(255, 255, 255));
                Assert.AreEqual(Color.FromRgb(230, 0, 0), result);
                result = SilentDictParser.ParseColor(NonNullableTestDict, "Color4", ["Color2", "Color3"], Color.FromRgb(255, 255, 255));
                Assert.AreEqual(Color.FromRgb(16, 32, 64), result);
                result = SilentDictParser.ParseColor(NonNullableTestDict, "Color3", [], Color.FromRgb(255, 255, 255));
                Assert.AreEqual(Color.FromRgb(255, 255, 255), result);
                result = SilentDictParser.ParseColor(NonNullableTestDict, "None", [], Color.FromRgb(255, 255, 255));
                Assert.AreEqual(Color.FromRgb(255, 255, 255), result);
                result = SilentDictParser.ParseColor(NonNullableTestDict, "None", ["Color1"], Color.FromRgb(255, 255, 255));
                Assert.AreEqual(Color.FromRgb(230, 0, 0), result);
                result = SilentDictParser.ParseColor(NonNullableTestDict, "Str", [], Color.FromRgb(255, 255, 255));
                Assert.AreEqual(Color.FromRgb(255, 255, 255), result);
            }

            // With unparsableValue + With Nullable
            {
                Color result = SilentDictParser.ParseColorNullable(NullableTestDict, "Color1", ["Color2", "Color3"], Color.FromRgb(255, 255, 255), out bool unparsableValue);
                Assert.AreEqual(Color.FromRgb(230, 0, 0), result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseColorNullable(NullableTestDict, "Color4", ["Color2", "Color3"], Color.FromRgb(255, 255, 255), out unparsableValue);
                Assert.AreEqual(Color.FromRgb(16, 32, 64), result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseColorNullable(NullableTestDict, "Color3", [], Color.FromRgb(255, 255, 255), out unparsableValue);
                Assert.AreEqual(Color.FromRgb(255, 255, 255), result);
                Assert.IsTrue(unparsableValue);
                result = SilentDictParser.ParseColorNullable(NullableTestDict, "None", [], Color.FromRgb(255, 255, 255), out unparsableValue);
                Assert.AreEqual(Color.FromRgb(255, 255, 255), result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseColorNullable(NullableTestDict, "None", ["Color1"], Color.FromRgb(255, 255, 255), out unparsableValue);
                Assert.AreEqual(Color.FromRgb(230, 0, 0), result);
                Assert.IsFalse(unparsableValue);
                result = SilentDictParser.ParseColorNullable(NullableTestDict, "Str", [], Color.FromRgb(255, 255, 255), out unparsableValue);
                Assert.AreEqual(Color.FromRgb(255, 255, 255), result);
                Assert.IsTrue(unparsableValue);
            }

            // Without unparsableValue + With Nullable
            {
                Color result = SilentDictParser.ParseColorNullable(NullableTestDict, "Color1", ["Color2", "Color3"], Color.FromRgb(255, 255, 255));
                Assert.AreEqual(Color.FromRgb(230, 0, 0), result);
                result = SilentDictParser.ParseColorNullable(NullableTestDict, "Color4", ["Color2", "Color3"], Color.FromRgb(255, 255, 255));
                Assert.AreEqual(Color.FromRgb(16, 32, 64), result);
                result = SilentDictParser.ParseColorNullable(NullableTestDict, "Color3", [], Color.FromRgb(255, 255, 255));
                Assert.AreEqual(Color.FromRgb(255, 255, 255), result);
                result = SilentDictParser.ParseColorNullable(NullableTestDict, "None", [], Color.FromRgb(255, 255, 255));
                Assert.AreEqual(Color.FromRgb(255, 255, 255), result);
                result = SilentDictParser.ParseColorNullable(NullableTestDict, "None", ["Color1"], Color.FromRgb(255, 255, 255));
                Assert.AreEqual(Color.FromRgb(230, 0, 0), result);
                result = SilentDictParser.ParseColorNullable(NullableTestDict, "Str", [], Color.FromRgb(255, 255, 255));
                Assert.AreEqual(Color.FromRgb(255, 255, 255), result);
            }
        }
        #endregion
    }
}
