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
        /// <summary>
        /// Parse a boolean value from a DOM of an .ini file.
        /// </summary>
        /// <param name="dict">
        /// The DOM of an .ini file.
        /// Dict Key means ini section, and Dict Value means ini value.
        /// </param>
        /// <param name="key">Name of the ini section.</param>
        /// <param name="defaultValue">Default value to use when the dict value is empty.</param>
        /// <returns>Parsed boolean value.</returns>
        public static string ParseString(Dictionary<string, string> dict, string key, string defaultValue)
        {
            if (!dict.ContainsKey(key))
                return defaultValue;

            return dict[key];
        }
        #endregion

        #region ParseString (Nullable)
        /// <summary>
        /// Parse a boolean value from a DOM of an .ini file.
        /// </summary>
        /// <param name="dict">
        /// The DOM of an .ini file.
        /// Dict Key means ini section, and Dict Value means ini value.
        /// </param>
        /// <param name="key">Name of the ini section.</param>
        /// <param name="defaultValue">Default value to use when the dict value is empty.</param>
        /// <returns>Parsed boolean value.</returns>
        public static string ParseStringNullable(Dictionary<string, string?> dict, string key, string defaultValue)
        {
            if (!dict.ContainsKey(key))
                return defaultValue;

            if (dict[key] is not string valStr)
                return defaultValue;

            return valStr;
        }
        #endregion

        #region ParseString (NullDefault)
        /// <summary>
        /// Parse a boolean value from a DOM of an .ini file.
        /// </summary>
        /// <param name="dict">
        /// The DOM of an .ini file.
        /// Dict Key means ini section, and Dict Value means ini value.
        /// </param>
        /// <param name="key">Name of the ini section.</param>
        /// <param name="defaultValue">Default value to use when the dict value is empty.</param>
        /// <returns>Parsed boolean value.</returns>
        public static string? ParseStringNullDefault(Dictionary<string, string?> dict, string key, string? defaultValue)
        {
            if (!dict.ContainsKey(key))
                return defaultValue;

            if (dict[key] is not string valStr)
                return defaultValue;

            return valStr;
        }
        #endregion

        #region ParseBoolean
        /// <summary>
        /// Parse a boolean value from a DOM of an .ini file.
        /// </summary>
        /// <param name="dict">
        /// The DOM of an .ini file.
        /// Dict Key means ini section, and Dict Value means ini value.
        /// </param>
        /// <param name="key">Name of the ini section.</param>
        /// <param name="defaultValue">Default value to use when the dict value is empty.</param>
        /// <returns>Parsed boolean value.</returns>
        public static bool ParseBoolean(Dictionary<string, string> dict, string key, bool defaultValue)
        {
            return ParseBoolean(dict, key, defaultValue, out _);
        }

        /// <summary>
        /// Parse a boolean value from a DOM of an .ini file.
        /// </summary>
        /// <param name="dict">
        /// The DOM of an .ini file.
        /// Dict Key means ini section, and Dict Value means ini value.
        /// </param>
        /// <param name="key">Name of the ini section.</param>
        /// <param name="defaultValue">Default value to use when the dict value is empty.</param>
        /// <param name="incorrectValue">Set to true when the value is not correct. Will not be set to true on empty value.</param>
        /// <returns>Parsed boolean value.</returns>
        public static bool ParseBoolean(Dictionary<string, string> dict, string key, bool defaultValue, out bool incorrectValue)
        {
            // Check ContainsKey and null
            incorrectValue = false;
            if (!dict.ContainsKey(key))
                return defaultValue;

            if (dict[key] is not string valStr)
                return defaultValue;

            incorrectValue = true;
            bool val;
            if (valStr.Equals("True", StringComparison.OrdinalIgnoreCase))
                val = true;
            else if (valStr.Equals("False", StringComparison.OrdinalIgnoreCase))
                val = false;
            else
                return defaultValue;

            incorrectValue = false;
            return val;
        }
        #endregion

        #region ParseBoolean (Nullable)
        /// <summary>
        /// Parse a boolean value from a DOM of an .ini file.
        /// </summary>
        /// <param name="dict">
        /// The DOM of an .ini file.
        /// Dict Key means ini section, and Dict Value means ini value.
        /// </param>
        /// <param name="key">Name of the ini section.</param>
        /// <param name="defaultValue">Default value to use when the dict value is empty.</param>
        /// <returns>Parsed boolean value.</returns>
        public static bool ParseBooleanNullable(Dictionary<string, string?> dict, string key, bool defaultValue)
        {
            return ParseBooleanNullable(dict, key, defaultValue, out _);
        }

        /// <summary>
        /// Parse a boolean value from a DOM of an .ini file.
        /// </summary>
        /// <param name="dict">
        /// The DOM of an .ini file.
        /// Dict Key means ini section, and Dict Value means ini value.
        /// </param>
        /// <param name="key">Name of the ini section.</param>
        /// <param name="defaultValue">Default value to use when the dict value is empty.</param>
        /// <param name="incorrectValue">Set to true when the value is not correct. Will not be set to true on empty value.</param>
        /// <returns>Parsed boolean value.</returns>
        public static bool ParseBooleanNullable(Dictionary<string, string?> dict, string key, bool defaultValue, out bool incorrectValue)
        {
            // Check ContainsKey and null
            incorrectValue = false;
            if (!dict.ContainsKey(key))
                return defaultValue;

            if (dict[key] is not string valStr)
                return defaultValue;

            incorrectValue = true;
            bool val;
            if (valStr.Equals("True", StringComparison.OrdinalIgnoreCase))
                val = true;
            else if (valStr.Equals("False", StringComparison.OrdinalIgnoreCase))
                val = false;
            else
                return defaultValue;

            incorrectValue = false;
            return val;
        }
        #endregion

        #region ParseInteger
        /// <summary>
        /// Parse an int value from a DOM of an .ini file.
        /// </summary>
        /// <param name="dict">
        /// The DOM of an .ini file.
        /// Dict Key means ini section, and Dict Value means ini value.
        /// </param>
        /// <param name="key">Name of the ini section.</param>
        /// <param name="defaultValue">Default value to use when the dict value is empty.</param>
        /// <returns>Parsed int value.</returns>
        public static int ParseInteger(Dictionary<string, string> dict, string key, int defaultValue)
        {
            return ParseInteger(dict, key, defaultValue, null, null, out _);
        }

        /// <summary>
        /// Parse an int value from a DOM of an .ini file.
        /// </summary>
        /// <param name="dict">
        /// The DOM of an .ini file.
        /// Dict Key means ini section, and Dict Value means ini value.
        /// </param>
        /// <param name="key">Name of the ini section.</param>
        /// <param name="defaultValue">Default value to use when the dict value is empty.</param>
        /// <param name="min">Allowed minimal integer value.</param>
        /// <param name="max">Allowed maximum integer value.</param>
        /// <returns>Parsed int value.</returns>
        public static int ParseInteger(Dictionary<string, string> dict, string key, int defaultValue, int? min, int? max)
        {
            return ParseInteger(dict, key, defaultValue, min, max, out _);
        }

        /// <summary>
        /// Parse an int value from a DOM of an .ini file.
        /// </summary>
        /// <param name="dict">
        /// The DOM of an .ini file.
        /// Dict Key means ini section, and Dict Value means ini value.
        /// </param>
        /// <param name="key">Name of the ini section.</param>
        /// <param name="defaultValue">Default value to use when the dict value is empty.</param>
        /// <param name="incorrectValue">Set to true when the value is not correct. Will not be set to true on empty value.</param>
        /// <returns>Parsed int value.</returns>
        public static int ParseInteger(Dictionary<string, string> dict, string key, int defaultValue, out bool incorrectValue)
        {
            return ParseInteger(dict, key, defaultValue, null, null, out incorrectValue);
        }

        /// <summary>
        /// Parse an int value from a DOM of an .ini file.
        /// </summary>
        /// <param name="dict">
        /// The DOM of an .ini file.
        /// Dict Key means ini section, and Dict Value means ini value.
        /// </param>
        /// <param name="key">Name of the ini section.</param>
        /// <param name="defaultValue">Default value to use when the dict value is empty.</param>
        /// <param name="min">Allowed minimal integer value.</param>
        /// <param name="max">Allowed maximum integer value.</param>
        /// <param name="incorrectValue">Set to true when the value is not correct. Will not be set to true on empty value.</param>
        /// <returns>Parsed int value.</returns>
        public static int ParseInteger(Dictionary<string, string> dict, string key, int defaultValue, int? min, int? max, out bool incorrectValue)
        {
            // Check ContainsKey and null
            incorrectValue = false;
            if (!dict.ContainsKey(key))
                return defaultValue;

            if (dict[key] is not string valStr)
                return defaultValue;

            incorrectValue = true;
            if (!NumberHelper.ParseInt32(valStr, out int valInt))
                return defaultValue;

            if (min is int minVal && valInt < minVal)
                return minVal;
            if (max is int maxVal && maxVal < valInt)
                return maxVal;

            incorrectValue = false;
            return valInt;
        }
        #endregion

        #region ParseInteger (Nullable)
        /// <summary>
        /// Parse an int value from a DOM of an .ini file.
        /// </summary>
        /// <param name="dict">
        /// The DOM of an .ini file.
        /// Dict Key means ini section, and Dict Value means ini value.
        /// </param>
        /// <param name="key">Name of the ini section.</param>
        /// <param name="defaultValue">Default value to use when the dict value is empty.</param>
        /// <returns>Parsed int value.</returns>
        public static int ParseIntegerNullable(Dictionary<string, string?> dict, string key, int defaultValue)
        {
            return ParseIntegerNullable(dict, key, defaultValue, null, null, out _);
        }

        /// <summary>
        /// Parse an int value from a DOM of an .ini file.
        /// </summary>
        /// <param name="dict">
        /// The DOM of an .ini file.
        /// Dict Key means ini section, and Dict Value means ini value.
        /// </param>
        /// <param name="key">Name of the ini section.</param>
        /// <param name="defaultValue">Default value to use when the dict value is empty.</param>
        /// <param name="min">Allowed minimal integer value.</param>
        /// <param name="max">Allowed maximum integer value.</param>
        /// <returns>Parsed int value.</returns>
        public static int ParseIntegerNullable(Dictionary<string, string?> dict, string key, int defaultValue, int? min, int? max)
        {
            return ParseIntegerNullable(dict, key, defaultValue, min, max, out _);
        }

        /// <summary>
        /// Parse an int value from a DOM of an .ini file.
        /// </summary>
        /// <param name="dict">
        /// The DOM of an .ini file.
        /// Dict Key means ini section, and Dict Value means ini value.
        /// </param>
        /// <param name="key">Name of the ini section.</param>
        /// <param name="defaultValue">Default value to use when the dict value is empty.</param>
        /// <param name="incorrectValue">Set to true when the value is not correct. Will not be set to true on empty value.</param>
        /// <returns>Parsed int value.</returns>
        public static int ParseIntegerNullable(Dictionary<string, string?> dict, string key, int defaultValue, out bool incorrectValue)
        {
            return ParseIntegerNullable(dict, key, defaultValue, null, null, out incorrectValue);
        }

        /// <summary>
        /// Parse an int value from a DOM of an .ini file.
        /// </summary>
        /// <param name="dict">
        /// The DOM of an .ini file.
        /// Dict Key means ini section, and Dict Value means ini value.
        /// </param>
        /// <param name="key">Name of the ini section.</param>
        /// <param name="defaultValue">Default value to use when the dict value is empty.</param>
        /// <param name="min">Allowed minimal integer value.</param>
        /// <param name="max">Allowed maximum integer value.</param>
        /// <param name="incorrectValue">Set to true when the value is not correct. Will not be set to true on empty value.</param>
        /// <returns>Parsed int value.</returns>
        public static int ParseIntegerNullable(Dictionary<string, string?> dict, string key, int defaultValue, int? min, int? max, out bool incorrectValue)
        {
            // Check ContainsKey and null
            incorrectValue = false;
            if (!dict.ContainsKey(key))
                return defaultValue;

            if (dict[key] is not string valStr)
                return defaultValue;

            incorrectValue = true;
            if (!NumberHelper.ParseInt32(valStr, out int valInt))
                return defaultValue;

            if (min is int minVal && valInt < minVal)
                return minVal;
            if (max is int maxVal && maxVal < valInt)
                return maxVal;

            incorrectValue = false;
            return valInt;
        }
        #endregion

        #region ParseEnum
        /// <summary>
        /// Parse an Enum value from a DOM of an .ini file.
        /// </summary>
        /// <param name="dict">
        /// The DOM of an .ini file.
        /// Dict Key means ini section, and Dict Value means ini value.
        /// </param>
        /// <param name="key">Name of the ini section.</param>
        /// <param name="defaultValue">Default value to use when the dict value is empty.</param>
        /// <returns>Parsed Enum value.</returns>
        public static TEnum ParseStrEnum<TEnum>(Dictionary<string, string> dict, string key, TEnum defaultValue)
            where TEnum : struct, Enum
        {
            return ParseStrEnum(dict, key, defaultValue, out _);
        }

        /// <summary>
        /// Parse an Enum value from a DOM of an .ini file.
        /// </summary>
        /// <param name="dict">
        /// The DOM of an .ini file.
        /// Dict Key means ini section, and Dict Value means ini value.
        /// </param>
        /// <param name="key">Name of the ini section.</param>
        /// <param name="defaultValue">Default value to use when the dict value is empty.</param>
        /// <param name="incorrectValue">Set to true when the value is not correct. Will not be set to true on empty value.</param>
        /// <returns>Parsed Enum value.</returns>
        public static TEnum ParseStrEnum<TEnum>(Dictionary<string, string> dict, string key, TEnum defaultValue, out bool incorrectValue)
            where TEnum : struct, Enum
        {
            incorrectValue = false;
            if (!dict.ContainsKey(key))
                return defaultValue;

            if (dict[key] is not string valStr)
                return defaultValue;

            incorrectValue = true;
            if (!Enum.TryParse(valStr, true, out TEnum kind) || !Enum.IsDefined(typeof(TEnum), kind))
                return defaultValue;

            incorrectValue = false;
            return kind;
        }

        /// <summary>
        /// Parse an Enum value from a DOM of an .ini file.
        /// </summary>
        /// <param name="dict">
        /// The DOM of an .ini file.
        /// Dict Key means ini section, and Dict Value means ini value.
        /// </param>
        /// <param name="key">Name of the ini section.</param>
        /// <param name="defaultValue">Default value to use when the dict value is empty.</param>
        /// <returns>Parsed Enum value.</returns>
        public static TEnum ParseIntEnum<TEnum>(Dictionary<string, string> dict, string key, TEnum defaultValue)
            where TEnum : Enum
        {
            return ParseIntEnum(dict, key, defaultValue, out _);
        }

        /// <summary>
        /// Parse an Enum value from a DOM of an .ini file.
        /// </summary>
        /// <param name="dict">
        /// The DOM of an .ini file.
        /// Dict Key means ini section, and Dict Value means ini value.
        /// </param>
        /// <param name="key">Name of the ini section.</param>
        /// <param name="defaultValue">Default value to use when the dict value is empty.</param>
        /// <param name="incorrectValue">Set to true when the value is not correct. Will not be set to true on empty value.</param>
        /// <returns>Parsed Enum value.</returns>
        public static TEnum ParseIntEnum<TEnum>(Dictionary<string, string> dict, string key, TEnum defaultValue, out bool incorrectValue)
            where TEnum : Enum
        {
            incorrectValue = false;
            if (!dict.ContainsKey(key))
                return defaultValue;

            if (dict[key] is not string valStr)
                return defaultValue;

            incorrectValue = true;
            if (!NumberHelper.ParseInt32(valStr, out int valInt))
                return defaultValue;

            if (!Enum.IsDefined(typeof(TEnum), valInt))
                return defaultValue;

            incorrectValue = false;
            return (TEnum)Enum.ToObject(typeof(TEnum), valInt);
        }
        #endregion

        #region ParseEnumNullable
        /// <summary>
        /// Parse an Enum value from a DOM of an .ini file.
        /// </summary>
        /// <param name="dict">
        /// The DOM of an .ini file.
        /// Dict Key means ini section, and Dict Value means ini value.
        /// </param>
        /// <param name="key">Name of the ini section.</param>
        /// <param name="defaultValue">Default value to use when the dict value is empty.</param>
        /// <returns>Parsed Enum value.</returns>
        public static TEnum ParseStrEnumNullable<TEnum>(Dictionary<string, string?> dict, string key, TEnum defaultValue)
            where TEnum : struct, Enum
        {
            return ParseStrEnumNullable(dict, key, defaultValue, out _);
        }

        /// <summary>
        /// Parse an Enum value from a DOM of an .ini file.
        /// </summary>
        /// <param name="dict">
        /// The DOM of an .ini file.
        /// Dict Key means ini section, and Dict Value means ini value.
        /// </param>
        /// <param name="key">Name of the ini section.</param>
        /// <param name="defaultValue">Default value to use when the dict value is empty.</param>
        /// <param name="incorrectValue">Set to true when the value is not correct. Will not be set to true on empty value.</param>
        /// <returns>Parsed Enum value.</returns>
        public static TEnum ParseStrEnumNullable<TEnum>(Dictionary<string, string?> dict, string key, TEnum defaultValue, out bool incorrectValue)
            where TEnum : struct, Enum
        {
            incorrectValue = false;
            if (!dict.ContainsKey(key))
                return defaultValue;

            if (dict[key] is not string valStr)
                return defaultValue;

            incorrectValue = true;
            if (!Enum.TryParse(valStr, true, out TEnum kind) || !Enum.IsDefined(typeof(TEnum), kind))
                return defaultValue;

            incorrectValue = false;
            return kind;
        }

        /// <summary>
        /// Parse an Enum value from a DOM of an .ini file.
        /// </summary>
        /// <param name="dict">
        /// The DOM of an .ini file.
        /// Dict Key means ini section, and Dict Value means ini value.
        /// </param>
        /// <param name="key">Name of the ini section.</param>
        /// <param name="defaultValue">Default value to use when the dict value is empty.</param>
        /// <returns>Parsed Enum value.</returns>
        public static TEnum ParseIntEnumNullable<TEnum>(Dictionary<string, string?> dict, string key, TEnum defaultValue)
            where TEnum : Enum
        {
            return ParseIntEnumNullable(dict, key, defaultValue, out _);
        }

        /// <summary>
        /// Parse an Enum value from a DOM of an .ini file.
        /// </summary>
        /// <param name="dict">
        /// The DOM of an .ini file.
        /// Dict Key means ini section, and Dict Value means ini value.
        /// </param>
        /// <param name="key">Name of the ini section.</param>
        /// <param name="defaultValue">Default value to use when the dict value is empty.</param>
        /// <param name="incorrectValue">Set to true when the value is not correct. Will not be set to true on empty value.</param>
        /// <returns>Parsed Enum value.</returns>
        public static TEnum ParseIntEnumNullable<TEnum>(Dictionary<string, string?> dict, string key, TEnum defaultValue, out bool incorrectValue)
            where TEnum : Enum
        {
            incorrectValue = false;
            if (!dict.ContainsKey(key))
                return defaultValue;

            if (dict[key] is not string valStr)
                return defaultValue;

            incorrectValue = true;
            if (!NumberHelper.ParseInt32(valStr, out int valInt))
                return defaultValue;

            if (!Enum.IsDefined(typeof(TEnum), valInt))
                return defaultValue;

            incorrectValue = false;
            return (TEnum)Enum.ToObject(typeof(TEnum), valInt);
        }
        #endregion

        #region ParseColor
        /// <summary>
        /// Parse a color value from a DOM of an .ini file.
        /// </summary>
        /// <param name="dict">
        /// The DOM of an .ini file.
        /// Dict Key means ini section, and Dict Value means ini value.
        /// </param>
        /// <param name="key">Name of the ini section.</param>
        /// <param name="defaultValue">Default value to use when the dict value is empty.</param>
        /// <returns>Parsed color value.</returns>
        public static Color ParseColor(Dictionary<string, string> dict, string key, Color defaultValue)
        {
            return ParseColor(dict, key, defaultValue, out _);
        }

        /// <summary>
        /// Parse a color value from a DOM of an .ini file.
        /// </summary>
        /// <param name="dict">
        /// The DOM of an .ini file.
        /// Dict Key means ini section, and Dict Value means ini value.
        /// </param>
        /// <param name="key">Name of the ini section.</param>
        /// <param name="defaultValue">Default value to use when the dict value is empty.</param>
        /// <param name="incorrectValue">Set to true when the value is not correct. Will not be set to true on empty value.</param>
        /// <returns>Parsed color value.</returns>
        public static Color ParseColor(Dictionary<string, string> dict, string key, Color defaultValue, out bool incorrectValue)
        {
            incorrectValue = false;
            if (!dict.ContainsKey(key))
                return defaultValue;

            incorrectValue = true;
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

            incorrectValue = false;
            return Color.FromRgb(c[0], c[1], c[2]);
        }

        /// <summary>
        /// Parse a color value from a DOM of an .ini file.
        /// </summary>
        /// <param name="dict">
        /// The DOM of an .ini file.
        /// Dict Key means ini section, and Dict Value means ini value.
        /// </param>
        /// <param name="key">Name of the ini section.</param>
        /// <param name="defaultValue">Default value to use when the dict value is empty.</param>
        /// <returns>Parsed color value.</returns>
        public static Color ParseColorNullable(Dictionary<string, string?> dict, string key, Color defaultValue)
        {
            return ParseColorNullable(dict, key, defaultValue, out _);
        }

        /// <summary>
        /// Parse a color value from a DOM of an .ini file.
        /// </summary>
        /// <param name="dict">
        /// The DOM of an .ini file.
        /// Dict Key means ini section, and Dict Value means ini value.
        /// </param>
        /// <param name="key">Name of the ini section.</param>
        /// <param name="defaultValue">Default value to use when the dict value is empty.</param>
        /// <param name="incorrectValue">Set to true when the value is not correct. Will not be set to true on empty value.</param>
        /// <returns>Parsed color value.</returns>
        public static Color ParseColorNullable(Dictionary<string, string?> dict, string key, Color defaultValue, out bool incorrectValue)
        {
            incorrectValue = false;
            if (!dict.ContainsKey(key))
                return defaultValue;

            if (dict[key] is not string valStr)
                return defaultValue;

            // Format = R, G, B (in base 10)
            incorrectValue = true;
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

            incorrectValue = false;
            return Color.FromRgb(c[0], c[1], c[2]);
        }
        #endregion
    }
}
