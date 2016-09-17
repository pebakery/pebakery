using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BakeryEngine
{
    public enum ParseStringToNumberType
    {
        String, Integer, Decimal
    }

    [Flags]
    public enum CompareStringNumberResult
    {
        None = 0,
        Equal = 1,
        NotEqual = 2,
        Smaller = 4,
        Bigger = 8,
    }

    public static class NumberHelper
    {
        /// <summary>
        /// integer parser, supports base 10 and 16 at same time
        /// </summary>
        /// <returns></returns>
        public static bool ParseInt32(string str, out Int32 value)
        {
            if (str == null || string.Equals(str, string.Empty))
            {
                value = 0;
                return false;
            }

            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return Int32.TryParse(str.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
            else
                return Int32.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// integer parser, supports base 10 and 16 at same time
        /// </summary>
        /// <returns></returns>
        public static bool ParseUInt32(string str, out UInt32 value)
        {
            if (str == null || string.Equals(str, string.Empty))
            {
                value = 0;
                return false;
            }

            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return UInt32.TryParse(str.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
            else
                return UInt32.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// decimal parser, supports base 10 and 16 at same time
        /// </summary>
        /// <returns></returns>
        public static bool ParseDecimal(string str, out decimal value)
        {
            if (string.Equals(str, string.Empty))
            {
                value = 0;
                return false;
            }
            return decimal.TryParse(str, NumberStyles.AllowDecimalPoint | NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        

        /// <summary>
        /// Parse string to int or decimal
        /// </summary>
        /// <param name="str"></param>
        /// <param name="integer"></param>
        /// <param name="real"></param>
        /// <returns>Return true if string is number</returns>
        public static ParseStringToNumberType ParseStringToNumber(string str, out int integer, out decimal real)
        {
            integer = 0;
            real = 0;

            if (str == null || string.Equals(str, string.Empty))
                return ParseStringToNumberType.String;

            // base 16 integer - Z
            if (Regex.IsMatch(str, @"^0x\d+$", RegexOptions.Compiled))
            {
                if (Int32.TryParse(str.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out integer))
                    return ParseStringToNumberType.Integer;
                else
                    return ParseStringToNumberType.String;
            }
            // real number - R
            else if (Regex.IsMatch(str, @"^(\d+)\.(\d+)$", RegexOptions.Compiled))
            {
                if (decimal.TryParse(str, NumberStyles.AllowDecimalPoint | NumberStyles.Integer, CultureInfo.InvariantCulture, out real))
                    return ParseStringToNumberType.Decimal;
                else
                    return ParseStringToNumberType.String;
            }
            else
            {
                // integer - Z
                if (Int32.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out integer))
                    return ParseStringToNumberType.Integer;
                else
                    return ParseStringToNumberType.String;
            }
        }

        

        /// <summary>
        /// Compare string, which would be number
        /// </summary>
        /// <param name="str1"></param>
        /// <param name="str2"></param>
        public static CompareStringNumberResult CompareStringNumber(string str1, string str2)
        {
            int num1, num2;
            decimal real1, real2;
            ParseStringToNumberType type1 = ParseStringToNumber(str1, out num1, out real1);
            ParseStringToNumberType type2 = ParseStringToNumber(str2, out num2, out real2);

            if (type1 == ParseStringToNumberType.String || type2 == ParseStringToNumberType.String)
            { // One of arg is string, so just compare
                if (string.Equals(str1, str2, StringComparison.OrdinalIgnoreCase))
                    return CompareStringNumberResult.Equal;
                else
                    return CompareStringNumberResult.NotEqual;
            }
            else if (type1 == ParseStringToNumberType.Integer && type2 == ParseStringToNumberType.Integer)
            { // Args are both int
                int comp = num1 - num2;
                if (comp < 0)
                    return CompareStringNumberResult.NotEqual | CompareStringNumberResult.Smaller;
                else if (comp == 0)
                    return CompareStringNumberResult.Equal;
                else
                    return CompareStringNumberResult.NotEqual | CompareStringNumberResult.Bigger;
            }
            else if (type1 == ParseStringToNumberType.Integer && type2 == ParseStringToNumberType.Decimal)
            { // One arg is decimal
                decimal comp = num1 - real2;
                if (comp < 0)
                    return CompareStringNumberResult.NotEqual | CompareStringNumberResult.Smaller;
                else if (comp == 0)
                    return CompareStringNumberResult.Equal;
                else
                    return CompareStringNumberResult.NotEqual | CompareStringNumberResult.Bigger;
            }
            else if (type1 == ParseStringToNumberType.Decimal && type2 == ParseStringToNumberType.Integer)
            { // One arg is decimal
                decimal comp = real1 - num2;
                if (comp < 0)
                    return CompareStringNumberResult.NotEqual | CompareStringNumberResult.Smaller;
                else if (comp == 0)
                    return CompareStringNumberResult.Equal;
                else
                    return CompareStringNumberResult.NotEqual | CompareStringNumberResult.Bigger;
            }
            else
            { // All args is decimal
                decimal comp = real1 - real2;
                if (comp < 0)
                    return CompareStringNumberResult.NotEqual | CompareStringNumberResult.Smaller;
                else if (comp == 0)
                    return CompareStringNumberResult.Equal;
                else
                    return CompareStringNumberResult.NotEqual | CompareStringNumberResult.Bigger;
            }
        }
    }
}
