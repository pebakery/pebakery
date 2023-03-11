/*
    Copyright (C) 2016-2023 Hajin Jang
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
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
// ReSharper disable InconsistentNaming

namespace PEBakery.Helper
{
    public static class NumberHelper
    {
        #region IsStringHexInteger
        public enum StringNumberType
        {
            PositiveInteger, NegativeInteger, HexInteger, Decimal, NotNumber
        }

        public static StringNumberType IsStringHexInteger(string str)
        {
            int pCnt = StringHelper.CountSubStr(str, ".");
            if (1 < pCnt)
                return StringNumberType.NotNumber;

            // 0x
            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return pCnt == 1 ? StringNumberType.NotNumber : StringNumberType.HexInteger;

            if (pCnt == 1)
                return StringNumberType.Decimal;

            return str.StartsWith("-", StringComparison.Ordinal) ? StringNumberType.NegativeInteger : StringNumberType.PositiveInteger;
        }
        #endregion

        #region ParseInt / ParseUInt
        /// <summary>
        /// integer parser, supports base 10 and 16 at same time
        /// </summary>
        /// <returns>Return false if failed</returns>
        public static bool ParseInt8(string str, out sbyte value)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                value = 0;
                return false;
            }

            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return sbyte.TryParse(str.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
            else
                return sbyte.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// integer parser, supports base 10 and 16 at same time
        /// </summary>
        /// <returns></returns>
        public static bool ParseUInt8(string str, out byte value)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                value = 0;
                return false;
            }

            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return byte.TryParse(str.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
            else
                return byte.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// integer parser, supports base 10 and 16 at same time
        /// </summary>
        /// <returns>Return false if failed</returns>
        public static bool ParseInt16(string str, out short value)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                value = 0;
                return false;
            }

            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return short.TryParse(str.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
            else
                return short.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// integer parser, supports base 10 and 16 at same time
        /// </summary>
        /// <returns></returns>
        public static bool ParseUInt16(string str, out ushort value)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                value = 0;
                return false;
            }

            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return ushort.TryParse(str.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
            else
                return ushort.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// integer parser, supports base 10 and 16 at same time
        /// </summary>
        /// <returns>Return false if failed</returns>
        public static bool ParseInt32(string str, out int value)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                value = 0;
                return false;
            }

            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return int.TryParse(str.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
            else
                return int.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// integer parser, supports base 10 and 16 at same time
        /// </summary>
        /// <returns></returns>
        public static bool ParseUInt32(string str, out uint value)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                value = 0;
                return false;
            }

            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return uint.TryParse(str.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
            else
                return uint.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// integer parser, supports base 10 and 16 at same time
        /// </summary>
        /// <returns>Return false if failed</returns>
        public static bool ParseInt64(string str, out long value)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                value = 0;
                return false;
            }

            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return long.TryParse(str.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
            else
                return long.TryParse(str, NumberStyles.Integer | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// integer parser, supports base 10 and 16 at same time
        /// </summary>
        /// <returns></returns>
        public static bool ParseUInt64(string str, out ulong value)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                value = 0;
                return false;
            }

            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return ulong.TryParse(str.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
            else
                return ulong.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// decimal parser, supports base 10 and 16 at same time
        /// </summary>
        /// <returns></returns>
        public static bool ParseDouble(string str, out double value)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                value = 0;
                return false;
            }

            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                bool result = ulong.TryParse(str.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong intValue);
                value = intValue;
                return result;
            }
            else
            {
                return double.TryParse(str, NumberStyles.AllowDecimalPoint | NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
            }
        }

        /// <summary>
        /// decimal parser, supports base 10 and 16 at same time
        /// </summary>
        /// <returns></returns>
        public static bool ParseDecimal(string str, out decimal value)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                value = 0;
                return false;
            }

            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                bool result = ulong.TryParse(str.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong intValue);
                value = intValue;
                return result;
            }
            else
            {
                return decimal.TryParse(str, NumberStyles.AllowDecimalPoint | NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
            }
        }
        #endregion

        #region ParseSignedAsUInt
        /// <summary>
        /// integer parser, supports base 10 and 16 at same time
        /// </summary>
        /// <returns></returns>
        public static bool ParseSignedAsUInt8(string str, out byte value)
        {
            if (ParseUInt8(str, out byte uInt))
            {
                value = uInt;
                return true;
            }
            else if (ParseInt8(str, out sbyte sInt))
            {
                value = (byte)sInt;
                return true;
            }
            else
            {
                value = 0;
                return false;
            }
        }

        /// <summary>
        /// integer parser, supports base 10 and 16 at same time
        /// </summary>
        /// <returns></returns>
        public static bool ParseSignedAsUInt16(string str, out ushort value)
        {
            if (ParseUInt16(str, out ushort uInt))
            {
                value = uInt;
                return true;
            }
            else if (ParseInt16(str, out short sInt))
            {
                value = (ushort)sInt;
                return true;
            }
            else
            {
                value = 0;
                return false;
            }
        }

        /// <summary>
        /// integer parser, supports base 10 and 16 at same time
        /// </summary>
        /// <returns></returns>
        public static bool ParseSignedAsUInt32(string str, out uint value)
        {
            if (ParseUInt32(str, out uint uInt))
            {
                value = uInt;
                return true;
            }
            else if (ParseInt32(str, out int sInt))
            {
                value = (uint)sInt;
                return true;
            }
            else
            {
                value = 0;
                return false;
            }
        }

        /// <summary>
        /// integer parser, supports base 10 and 16 at same time
        /// </summary>
        /// <returns></returns>
        public static bool ParseSignedAsUInt64(string str, out ulong value)
        {
            if (ParseUInt64(str, out ulong uInt))
            {
                value = uInt;
                return true;
            }
            else if (ParseInt64(str, out long sInt))
            {
                value = (ulong)sInt;
                return true;
            }
            else
            {
                value = 0;
                return false;
            }
        }
        #endregion

        #region ParseStringToNumber
        public enum ParseStringToNumberType
        {
            String, Integer, Decimal
        }

        /// <summary>
        /// Parse string to int or decimal
        /// </summary>
        public static ParseStringToNumberType ParseStringToNumber(string str, out long integer, out decimal real)
        {
            integer = 0;
            real = 0;

            if (string.IsNullOrEmpty(str))
                return ParseStringToNumberType.String;

            // base 10 integer - Z
            if (Regex.IsMatch(str, @"^[0-9]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant))
            {
                if (long.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out integer))
                    return ParseStringToNumberType.Integer;
                else
                    return ParseStringToNumberType.String;
            }
            // base 16 integer - Z
            if (Regex.IsMatch(str, @"^0x[0-9a-zA-Z]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant))
            {
                if (long.TryParse(str.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out integer))
                    return ParseStringToNumberType.Integer;
                else
                    return ParseStringToNumberType.String;
            }

            // real number - R
            if (Regex.IsMatch(str, @"^([0-9]+)\.([0-9]+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant))
            {
                if (decimal.TryParse(str, NumberStyles.AllowDecimalPoint | NumberStyles.Integer, CultureInfo.InvariantCulture, out real))
                    return ParseStringToNumberType.Decimal;
                else
                    return ParseStringToNumberType.String;
            }

            // Just String
            return ParseStringToNumberType.String;
        }
        #endregion

        #region CompareStringNumber
        [Flags]
        public enum CompareStringNumberResult
        {
            None = 0,
            Equal = 1,
            NotEqual = 2,
            Smaller = 4,
            Bigger = 8,
        }

        /// <summary>
        /// Compare string, which would be number
        /// </summary>
        public static CompareStringNumberResult CompareStringNumber(string str1, string str2, bool ignoreCase = true)
        {
            // Try version number compare
            VersionEx? v1 = VersionEx.Parse(str1);
            VersionEx? v2 = VersionEx.Parse(str2);
            if (v1 is not null && v2 is not null)
            {
                int comp = v1.CompareTo(v2);
                if (comp < 0)
                    return CompareStringNumberResult.Smaller;
                else if (comp == 0)
                    return CompareStringNumberResult.Equal;
                else
                    return CompareStringNumberResult.Bigger;
            }

            // Do simple number or string compare
            ParseStringToNumberType type1 = ParseStringToNumber(str1, out long z1, out decimal r1);
            ParseStringToNumberType type2 = ParseStringToNumber(str2, out long z2, out decimal r2);

            if (type1 == ParseStringToNumberType.Integer && type2 == ParseStringToNumberType.Integer)
            { // Args are both int
                long comp = z1 - z2;
                if (comp < 0)
                    return CompareStringNumberResult.Smaller;
                else if (comp == 0)
                    return CompareStringNumberResult.Equal;
                else
                    return CompareStringNumberResult.Bigger;
            }

            if (type1 == ParseStringToNumberType.Integer && type2 == ParseStringToNumberType.Decimal ||
                type1 == ParseStringToNumberType.Decimal && type2 == ParseStringToNumberType.Integer ||
                type1 == ParseStringToNumberType.Decimal && type2 == ParseStringToNumberType.Decimal)
            { // One arg is decimal
                decimal comp = r1 - r2;
                if (comp < 0)
                    return CompareStringNumberResult.Smaller;
                else if (comp == 0)
                    return CompareStringNumberResult.Equal;
                else
                    return CompareStringNumberResult.Bigger;
            }

            // if (type1 == ParseStringToNumberType.String || type2 == ParseStringToNumberType.String)
            // One of arg is string, so just compare
            StringComparison compOpt = StringComparison.Ordinal;
            if (ignoreCase)
                compOpt = StringComparison.OrdinalIgnoreCase;

            if (str1.Equals(str2, compOpt))
                return CompareStringNumberResult.Equal;
            else
                return CompareStringNumberResult.NotEqual;
        }
        #endregion

        #region Bytes Manipulation
        /// <summary>
        /// Parse hex string into byte array. Hex string must be in form of A0B1C2. Return true if success.
        /// </summary>
        /// <param name="hex"></param>
        /// <param name="array"></param>
        /// <returns>Return true if success.</returns>
        public static bool ParseHexStringToBytes(string hex, out byte[] array)
        {
            if (hex.Length % 2 == 1) // hex's length must be even number
            {
                array = Array.Empty<byte>();
                return false;
            }

            array = new byte[hex.Length / 2];
            for (int i = 0; i < hex.Length; i += 2)
                array[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return true;
        }

        public const long PetaByte = 1024L * 1024L * 1024L * 1024L * 1024L;
        public const long TeraByte = 1024L * 1024L * 1024L * 1024L;
        public const long GigaByte = 1024L * 1024L * 1024L;
        public const long MegaByte = 1024L * 1024L;
        public const long KiloByte = 1024L;

        public static string ByteSizeToSIUnit(long byteSize, int decPoint = 3)
        {
            if (decPoint < 0) throw new ArgumentOutOfRangeException(nameof(decPoint));

            string formatString = "0";
            if (0 < decPoint)
            { // formatString = "0.###"
                StringBuilder b = new StringBuilder(decPoint + 1);
                b.Append("0.");
                for (int i = 0; i < decPoint; i++)
                    b.Append('#');
                formatString = b.ToString();
            }

            string str;
            if (PetaByte <= byteSize)
                str = $"{((decimal)byteSize / PetaByte).ToString(formatString)}PB";
            else if (TeraByte <= byteSize)
                str = $"{((decimal)byteSize / TeraByte).ToString(formatString)}TB";
            else if (GigaByte <= byteSize)
                str = $"{((decimal)byteSize / GigaByte).ToString(formatString)}GB";
            else if (MegaByte <= byteSize)
                str = $"{((decimal)byteSize / MegaByte).ToString(formatString)}MB";
            else
                str = $"{((decimal)byteSize / KiloByte).ToString(formatString)}KB";

            return str;
        }

        public static string NaturalByteSizeToSIUnit(long byteSize)
        {
            string str;
            if (PetaByte <= byteSize)
            {
                decimal rounded = Math.Round((decimal)byteSize / PetaByte, 1);
                str = $"{rounded}PB";
            }
            else if (TeraByte <= byteSize)
            {
                decimal rounded = Math.Round((decimal)byteSize / TeraByte, 1);
                str = $"{rounded}TB";
            }
            else if (GigaByte <= byteSize)
            {
                decimal rounded = Math.Round((decimal)byteSize / GigaByte, 1);
                str = $"{rounded}GB";
            }
            else if (MegaByte <= byteSize)
            {
                decimal rounded = Math.Round((decimal)byteSize / MegaByte, 1);
                str = $"{rounded}MB";
            }
            else
            {
                decimal rounded = Math.Ceiling((decimal)byteSize / KiloByte);
                str = $"{rounded}KB";
            }

            return str;
        }

        public static decimal HumanReadableStringToByteSize(string str)
        {
            long multiplier = 1;
            int subStrEndIdx = 0;

            if (str.EndsWith("PB", StringComparison.OrdinalIgnoreCase))
            {
                multiplier = PetaByte;
                subStrEndIdx = 2;
            }
            else if (str.EndsWith("TB", StringComparison.OrdinalIgnoreCase))
            {
                multiplier = TeraByte;
                subStrEndIdx = 2;
            }
            else if (str.EndsWith("GB", StringComparison.OrdinalIgnoreCase))
            {
                multiplier = GigaByte;
                subStrEndIdx = 2;
            }
            else if (str.EndsWith("MB", StringComparison.OrdinalIgnoreCase))
            {
                multiplier = MegaByte;
                subStrEndIdx = 2;
            }
            else if (str.EndsWith("KB", StringComparison.OrdinalIgnoreCase))
            {
                multiplier = KiloByte;
                subStrEndIdx = 2;
            }

            if (str.EndsWith("P", StringComparison.OrdinalIgnoreCase))
            {
                multiplier = PetaByte;
                subStrEndIdx = 1;
            }
            else if (str.EndsWith("T", StringComparison.OrdinalIgnoreCase))
            {
                multiplier = TeraByte;
                subStrEndIdx = 1;
            }
            else if (str.EndsWith("G", StringComparison.OrdinalIgnoreCase))
            {
                multiplier = GigaByte;
                subStrEndIdx = 1;
            }
            else if (str.EndsWith("M", StringComparison.OrdinalIgnoreCase))
            {
                multiplier = MegaByte;
                subStrEndIdx = 1;
            }
            else if (str.EndsWith("K", StringComparison.OrdinalIgnoreCase))
            {
                multiplier = KiloByte;
                subStrEndIdx = 1;
            }

            str = str[..^subStrEndIdx];
            return decimal.Parse(str, NumberStyles.Float, CultureInfo.InvariantCulture) * multiplier;
        }
        #endregion

        #region DecimalPower
        public static decimal DecimalPower(decimal val, uint pow)
        {
            decimal ret = 1;
            for (uint i = 0; i < pow; i++)
                ret *= val;
            return ret;
        }
        #endregion

        #region Round
        public static int Round(int src, int unit)
        {
            int remainder = src % unit;
            if ((unit - 1) / 2 < remainder)
                return src - remainder + unit;
            else
                return src - remainder;
        }

        public static long Round(long src, long unit)
        {
            long remainder = src % unit;
            if ((unit - 1) / 2 < remainder)
                return src - remainder + unit;
            else
                return src - remainder;
        }
        #endregion

        #region Compare of floating points
        public const float FloatCompareEpsilon = 1E-7F;
        public const double DoubleCompareEpsilon = 1E-14;

        public static bool FloatEquals(float x, float y)
        {
            return Math.Abs(x - y) < FloatCompareEpsilon;
        }
        public static bool DoubleEquals(double x, double y)
        {
            return Math.Abs(x - y) < DoubleCompareEpsilon;
        }
        #endregion

        #region ToEnglishOrdinal
        /// <summary>
        /// Convert integer to ordinal number string (English only)
        /// </summary>
        public static string ToEnglishOridnal(int intVal)
        {
            if (intVal <= 0)
                return intVal.ToString();

            switch (intVal % 100)
            {
                case 11:
                case 12:
                case 13:
                    return $"{intVal}th";
            }

            switch (intVal % 10)
            {
                case 1:
                    return $"{intVal}st";
                case 2:
                    return $"{intVal}nd";
                case 3:
                    return $"{intVal}rd";
                default:
                    return $"{intVal}th";
            }
        }
        #endregion
    }
}
