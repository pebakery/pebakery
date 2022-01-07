/*
    Copyright (C) 2019-2022 Hajin Jang
    Licensed under MIT License.
 
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows.Media;

namespace PEBakery.Helper
{
    public static class SilentDictParser
    {
        #region ParseString
        public static string ParseString(Dictionary<string, string> dict, string key, string defaultValue)
        {
            return ParseString(dict, key, defaultValue, out _);
        }

        public static string ParseString(Dictionary<string, string> dict, string key, string defaultValue, out bool notFound)
        {
            notFound = true;
            if (!dict.ContainsKey(key))
                return defaultValue;

            string valStr = dict[key];

            notFound = false;
            return valStr;
        }
        #endregion

        #region ParseString (Nullable)
        public static string ParseStringNullable(Dictionary<string, string?> dict, string key, string defaultValue)
        {
            return ParseStringNullable(dict, key, defaultValue, out _);
        }

        public static string ParseStringNullable(Dictionary<string, string?> dict, string key, string defaultValue, out bool notFound)
        {
            notFound = true;
            if (!dict.ContainsKey(key))
                return defaultValue;

            if (dict[key] is not string valStr)
                return defaultValue;

            notFound = false;
            return valStr;
        }
        #endregion

        #region ParseString (NullDefault)
        public static string? ParseStringNullDefault(Dictionary<string, string?> dict, string key, string? defaultValue)
        {
            return ParseStringNullDefault(dict, key, defaultValue, out _);
        }

        public static string? ParseStringNullDefault(Dictionary<string, string?> dict, string key, string? defaultValue, out bool notFound)
        {
            notFound = true;
            if (!dict.ContainsKey(key))
                return defaultValue;

            if (dict[key] is not string valStr)
                return defaultValue;

            notFound = false;
            return valStr;
        }
        #endregion

        #region ParseBoolean
        public static bool ParseBoolean(Dictionary<string, string> dict, string key, bool defaultValue)
        {
            return ParseBoolean(dict, key, defaultValue, out _);
        }

        public static bool ParseBoolean(Dictionary<string, string> dict, string key, bool defaultValue, out bool notFound)
        {
            // Check ContainsKey and null
            notFound = true;
            if (!dict.ContainsKey(key))
                return defaultValue;

            if (dict[key] is not string valStr)
                return defaultValue;

            bool val;
            if (valStr.Equals("True", StringComparison.OrdinalIgnoreCase))
                val = true;
            else if (valStr.Equals("False", StringComparison.OrdinalIgnoreCase))
                val = false;
            else
                return defaultValue;

            notFound = false;
            return val;
        }
        #endregion

        #region ParseBoolean (Nullable)
        public static bool ParseBooleanNullable(Dictionary<string, string?> dict, string key, bool defaultValue)
        {
            return ParseBooleanNullable(dict, key, defaultValue, out _);
        }

        public static bool ParseBooleanNullable(Dictionary<string, string?> dict, string key, bool defaultValue, out bool notFound)
        {
            // Check ContainsKey and null
            notFound = true;
            if (!dict.ContainsKey(key))
                return defaultValue;

            if (dict[key] is not string valStr)
                return defaultValue;

            bool val;
            if (valStr.Equals("True", StringComparison.OrdinalIgnoreCase))
                val = true;
            else if (valStr.Equals("False", StringComparison.OrdinalIgnoreCase))
                val = false;
            else
                return defaultValue;

            notFound = false;
            return val;
        }
        #endregion

        #region ParseInteger
        public static int ParseInteger(Dictionary<string, string> dict, string key, int defaultValue)
        {
            return ParseInteger(dict, key, defaultValue, null, null, out _);
        }

        public static int ParseInteger(Dictionary<string, string> dict, string key, int defaultValue, int? min, int? max)
        {
            return ParseInteger(dict, key, defaultValue, min, max, out _);
        }

        public static int ParseInteger(Dictionary<string, string> dict, string key, int defaultValue, out bool notFound)
        {
            return ParseInteger(dict, key, defaultValue, null, null, out notFound);
        }

        public static int ParseInteger(Dictionary<string, string> dict, string key, int defaultValue, int? min, int? max, out bool notFound)
        {
            // Check ContainsKey and null
            notFound = true;
            if (!dict.ContainsKey(key))
                return defaultValue;

            if (dict[key] is not string valStr)
                return defaultValue;

            if (!NumberHelper.ParseInt32(valStr, out int valInt))
                return defaultValue;

            if (min == null)
            {
                if (max != null && max < valInt)
                    return defaultValue;
            }
            else
            {
                if (max == null)
                {
                    if (valInt < min)
                        return defaultValue;
                }
                else
                {
                    if (valInt < min || max < valInt)
                        return defaultValue;
                }
            }

            notFound = false;
            return valInt;
        }
        #endregion

        #region ParseInteger (Nullable)
        public static int ParseIntegerNullable(Dictionary<string, string?> dict, string key, int defaultValue)
        {
            return ParseIntegerNullable(dict, key, defaultValue, null, null, out _);
        }

        public static int ParseIntegerNullable(Dictionary<string, string?> dict, string key, int defaultValue, int? min, int? max)
        {
            return ParseIntegerNullable(dict, key, defaultValue, min, max, out _);
        }

        public static int ParseIntegerNullable(Dictionary<string, string?> dict, string key, int defaultValue, out bool notFound)
        {
            return ParseIntegerNullable(dict, key, defaultValue, null, null, out notFound);
        }

        public static int ParseIntegerNullable(Dictionary<string, string?> dict, string key, int defaultValue, int? min, int? max, out bool notFound)
        {
            // Check ContainsKey and null
            notFound = true;
            if (!dict.ContainsKey(key))
                return defaultValue;

            if (dict[key] is not string valStr)
                return defaultValue;

            if (!NumberHelper.ParseInt32(valStr, out int valInt))
                return defaultValue;

            if (min == null)
            {
                if (max != null && max < valInt)
                    return defaultValue;
            }
            else
            {
                if (max == null)
                {
                    if (valInt < min)
                        return defaultValue;
                }
                else
                {
                    if (valInt < min || max < valInt)
                        return defaultValue;
                }
            }

            notFound = false;
            return valInt;
        }
        #endregion

        #region ParseEnum
        public static TEnum ParseStrEnum<TEnum>(Dictionary<string, string> dict, string key, TEnum defaultValue)
            where TEnum : struct, Enum
        {
            return ParseStrEnum(dict, key, defaultValue, out _);
        }

        public static TEnum ParseStrEnum<TEnum>(Dictionary<string, string> dict, string key, TEnum defaultValue, out bool notFound)
            where TEnum : struct, Enum
        {
            notFound = true;
            if (!dict.ContainsKey(key))
                return defaultValue;

            if (dict[key] is not string valStr)
                return defaultValue;

            if (!Enum.TryParse(valStr, true, out TEnum kind) || !Enum.IsDefined(typeof(TEnum), kind))
                return defaultValue;

            notFound = false;
            return kind;
        }

        public static TEnum ParseIntEnum<TEnum>(Dictionary<string, string> dict, string key, TEnum defaultValue)
            where TEnum : Enum
        {
            return ParseIntEnum(dict, key, defaultValue, out _);
        }

        public static TEnum ParseIntEnum<TEnum>(Dictionary<string, string> dict, string key, TEnum defaultValue, out bool notFound)
            where TEnum : Enum
        {
            notFound = true;
            if (!dict.ContainsKey(key))
                return defaultValue;

            string? valStr = dict[key];
            if (valStr == null)
                return defaultValue;

            if (!NumberHelper.ParseInt32(valStr, out int valInt))
                return defaultValue;

            if (!Enum.IsDefined(typeof(TEnum), valInt))
                return defaultValue;

            notFound = false;
            return (TEnum)Enum.ToObject(typeof(TEnum), valInt);
        }
        #endregion

        #region ParseEnumNullable
        public static TEnum ParseStrEnumNullable<TEnum>(Dictionary<string, string?> dict, string key, TEnum defaultValue)
            where TEnum : struct, Enum
        {
            return ParseStrEnumNullable(dict, key, defaultValue, out _);
        }

        public static TEnum ParseStrEnumNullable<TEnum>(Dictionary<string, string?> dict, string key, TEnum defaultValue, out bool notFound)
            where TEnum : struct, Enum
        {
            notFound = true;
            if (!dict.ContainsKey(key))
                return defaultValue;

            if (dict[key] is not string valStr)
                return defaultValue;

            if (!Enum.TryParse(valStr, true, out TEnum kind) || !Enum.IsDefined(typeof(TEnum), kind))
                return defaultValue;

            notFound = false;
            return kind;
        }

        public static TEnum ParseIntEnumNullable<TEnum>(Dictionary<string, string?> dict, string key, TEnum defaultValue)
            where TEnum : Enum
        {
            return ParseIntEnumNullable(dict, key, defaultValue, out _);
        }

        public static TEnum ParseIntEnumNullable<TEnum>(Dictionary<string, string?> dict, string key, TEnum defaultValue, out bool notFound)
            where TEnum : Enum
        {
            notFound = true;
            if (!dict.ContainsKey(key))
                return defaultValue;

            string? valStr = dict[key];
            if (valStr == null)
                return defaultValue;

            if (!NumberHelper.ParseInt32(valStr, out int valInt))
                return defaultValue;

            if (!Enum.IsDefined(typeof(TEnum), valInt))
                return defaultValue;

            notFound = false;
            return (TEnum)Enum.ToObject(typeof(TEnum), valInt);
        }
        #endregion

        #region ParseColor
        public static Color ParseColor(Dictionary<string, string> dict, string key, Color defaultValue)
        {
            return ParseColor(dict, key, defaultValue, out _);
        }

        public static Color ParseColor(Dictionary<string, string> dict, string key, Color defaultValue, out bool notFound)
        {
            notFound = true;
            if (!dict.ContainsKey(key))
                return defaultValue;

            string valStr = dict[key];

            // Format = R, G, B (in base 10)
            string[] colorStrs = valStr.Split(',').Select(x => x.Trim()).ToArray();
            if (colorStrs.Length != 3)
                return defaultValue;

            byte[] c = new byte[3]; // R, G, B
            for (int i = 0; i < 3; i++)
            {
                string colorStr = colorStrs[i];
                if (!byte.TryParse(colorStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte valByte))
                {
                    char ch;
                    switch (i)
                    {
                        case 0: // Red
                            ch = 'R';
                            break;
                        case 1: // Green
                            ch = 'G';
                            break;
                        case 2: // Blue
                            ch = 'B';
                            break;
                        default: // Unknown
                            ch = 'U';
                            break;
                    }
                    Debug.Assert(ch != 'U', "Unknown color parsing index");
                    return defaultValue;
                }
                c[i] = valByte;
            }

            notFound = false;
            return Color.FromRgb(c[0], c[1], c[2]);
        }

        public static Color ParseColorNullable(Dictionary<string, string?> dict, string key, Color defaultValue)
        {
            return ParseColorNullable(dict, key, defaultValue, out _);
        }

        public static Color ParseColorNullable(Dictionary<string, string?> dict, string key, Color defaultValue, out bool notFound)
        {
            notFound = true;
            if (!dict.ContainsKey(key))
                return defaultValue;

            if (dict[key] is not string valStr)
                return defaultValue;

            // Format = R, G, B (in base 10)
            string[] colorStrs = valStr.Split(',').Select(x => x.Trim()).ToArray();
            if (colorStrs.Length != 3)
                return defaultValue;

            byte[] c = new byte[3]; // R, G, B
            for (int i = 0; i < 3; i++)
            {
                string colorStr = colorStrs[i];
                if (!byte.TryParse(colorStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte valByte))
                {
                    char ch;
                    switch (i)
                    {
                        case 0: // Red
                            ch = 'R';
                            break;
                        case 1: // Green
                            ch = 'G';
                            break;
                        case 2: // Blue
                            ch = 'B';
                            break;
                        default: // Unknown
                            ch = 'U';
                            break;
                    }
                    Debug.Assert(ch != 'U', "Unknown color parsing index");
                    return defaultValue;
                }
                c[i] = valByte;
            }

            notFound = false;
            return Color.FromRgb(c[0], c[1], c[2]);
        }
        #endregion
    }
}
