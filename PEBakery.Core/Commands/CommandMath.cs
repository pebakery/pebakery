/*
    Copyright (C) 2016-2022 Hajin Jang
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

using PEBakery.Helper;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace PEBakery.Core.Commands
{
    public static class CommandMath
    {
        public static List<LogInfo> Math(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_Math info = cmd.Info.Cast<CodeInfo_Math>();

            MathType type = info.Type;
            switch (type)
            {
                case MathType.Add:
                case MathType.Sub:
                case MathType.Mul:
                case MathType.Div:
                    {
                        MathInfo_Arithmetic subInfo = info.SubInfo.Cast<MathInfo_Arithmetic>();

                        string srcStr1 = StringEscaper.Preprocess(s, subInfo.Src1);
                        string srcStr2 = StringEscaper.Preprocess(s, subInfo.Src2);

                        if (!NumberHelper.ParseDecimal(srcStr1, out decimal src1))
                            return LogInfo.LogErrorMessage(logs, $"[{srcStr1}] is not a valid integer");
                        if (!NumberHelper.ParseDecimal(srcStr2, out decimal src2))
                            return LogInfo.LogErrorMessage(logs, $"[{srcStr2}] is not a valid integer");

                        decimal destInt;
                        switch (type)
                        {
                            case MathType.Add:
                                destInt = src1 + src2;
                                break;
                            case MathType.Sub:
                                destInt = src1 - src2;
                                break;
                            case MathType.Mul:
                                destInt = src1 * src2;
                                break;
                            case MathType.Div:
                                destInt = src1 / src2;
                                break;
                            default:
                                throw new InternalException("Internal Logic Error at Math,Arithmetic");
                        }

                        logs.AddRange(Variables.SetVariable(s, subInfo.DestVar, destInt.ToString(CultureInfo.InvariantCulture)));
                    }
                    break;
                case MathType.IntDiv:
                    {
                        MathInfo_IntDiv subInfo = info.SubInfo.Cast<MathInfo_IntDiv>();

                        string srcStr1 = StringEscaper.Preprocess(s, subInfo.Src1);
                        string srcStr2 = StringEscaper.Preprocess(s, subInfo.Src2);

                        if (srcStr1.StartsWith("-", StringComparison.Ordinal) ||
                            srcStr2.StartsWith("-", StringComparison.Ordinal))
                        { // Signed
                            if (!NumberHelper.ParseInt64(srcStr1, out long src1))
                                return LogInfo.LogErrorMessage(logs, $"[{srcStr1}] is not a valid integer");
                            if (!NumberHelper.ParseInt64(srcStr2, out long src2))
                                return LogInfo.LogErrorMessage(logs, $"[{srcStr2}] is not a valid integer");

                            long q = src1 / src2;
                            long r = src1 % src2;

                            logs.AddRange(Variables.SetVariable(s, subInfo.QuotientVar, q.ToString()));
                            logs.AddRange(Variables.SetVariable(s, subInfo.RemainderVar, r.ToString()));
                        }
                        else
                        { // Unsigned
                            if (!NumberHelper.ParseUInt64(srcStr1, out ulong src1))
                                return LogInfo.LogErrorMessage(logs, $"[{srcStr1}] is not a valid integer");
                            if (!NumberHelper.ParseUInt64(srcStr2, out ulong src2))
                                return LogInfo.LogErrorMessage(logs, $"[{srcStr2}] is not a valid integer");

                            ulong q = src1 / src2;
                            ulong r = src1 % src2;

                            logs.AddRange(Variables.SetVariable(s, subInfo.QuotientVar, q.ToString()));
                            logs.AddRange(Variables.SetVariable(s, subInfo.RemainderVar, r.ToString()));
                        }
                    }
                    break;
                case MathType.Neg:
                    {
                        MathInfo_Neg subInfo = info.SubInfo.Cast<MathInfo_Neg>();

                        string srcStr = StringEscaper.Preprocess(s, subInfo.Src);
                        if (!NumberHelper.ParseDecimal(srcStr, out decimal src))
                            return LogInfo.LogErrorMessage(logs, $"[{srcStr}] is not a valid integer");

                        decimal destInt = src * -1;
                        logs.AddRange(Variables.SetVariable(s, subInfo.DestVar, destInt.ToString(CultureInfo.InvariantCulture)));
                    }
                    break;
                case MathType.ToSign:
                case MathType.ToUnsign:
                    {
                        // Math,IntSign,<DestVar>,<Src>,<BitSize>
                        // Math,IntUnsign,<DestVar>,<Src>,<BitSize>
                        MathInfo_IntegerSignedness subInfo = info.SubInfo.Cast<MathInfo_IntegerSignedness>();

                        string srcStr = StringEscaper.Preprocess(s, subInfo.Src);
                        string bitSizeStr = StringEscaper.Preprocess(s, subInfo.BitSize);
                        string? errorMsg = ParseAndCheckBitSize(bitSizeStr, out int bitSize);
                        if (errorMsg != null)
                            return LogInfo.LogErrorMessage(logs, errorMsg);

                        string destStr;
                        if (info.Type == MathType.ToSign)
                        { // Unsigned int to signed int
                            switch (bitSize)
                            {
                                case 8:
                                    {
                                        if (!NumberHelper.ParseUInt8(srcStr, out byte src))
                                            return LogInfo.LogErrorMessage(logs, $"[{srcStr}] is not a valid integer");

                                        destStr = ((sbyte)src).ToString();
                                    }
                                    break;
                                case 16:
                                    {
                                        if (!NumberHelper.ParseUInt16(srcStr, out ushort src))
                                            return LogInfo.LogErrorMessage(logs, $"[{srcStr}] is not a valid integer");

                                        destStr = ((short)src).ToString();
                                    }
                                    break;
                                case 32:
                                    {
                                        if (!NumberHelper.ParseUInt32(srcStr, out uint src))
                                            return LogInfo.LogErrorMessage(logs, $"[{srcStr}] is not a valid integer");

                                        destStr = ((int)src).ToString();
                                    }
                                    break;
                                case 64:
                                    {
                                        if (!NumberHelper.ParseUInt64(srcStr, out ulong src))
                                            return LogInfo.LogErrorMessage(logs, $"[{srcStr}] is not a valid integer");

                                        destStr = ((long)src).ToString();
                                    }
                                    break;
                                default:
                                    throw new InternalException("Internal Logic Error at Math,ToSign");
                            }
                        }
                        else
                        { // Signed int to unsigned int
                            switch (bitSize)
                            {
                                case 8:
                                    {
                                        if (!NumberHelper.ParseInt8(srcStr, out sbyte src))
                                            return LogInfo.LogErrorMessage(logs, $"[{srcStr}] is not a valid integer");

                                        destStr = ((byte)src).ToString();
                                    }
                                    break;
                                case 16:
                                    {
                                        if (!NumberHelper.ParseInt16(srcStr, out short src))
                                            return LogInfo.LogErrorMessage(logs, $"[{srcStr}] is not a valid integer");

                                        destStr = ((ushort)src).ToString();
                                    }
                                    break;
                                case 32:
                                    {
                                        if (!NumberHelper.ParseInt32(srcStr, out int src))
                                            return LogInfo.LogErrorMessage(logs, $"[{srcStr}] is not a valid integer");

                                        destStr = ((uint)src).ToString();
                                    }
                                    break;
                                case 64:
                                    {
                                        if (!NumberHelper.ParseInt64(srcStr, out long src))
                                            return LogInfo.LogErrorMessage(logs, $"[{srcStr}] is not a valid integer");

                                        destStr = ((ulong)src).ToString();
                                    }
                                    break;
                                default:
                                    throw new InternalException("Internal Logic Error at Math,ToUnsign");
                            }
                        }

                        logs.AddRange(Variables.SetVariable(s, subInfo.DestVar, destStr));
                    }
                    break;
                case MathType.BoolAnd:
                case MathType.BoolOr:
                case MathType.BoolXor:
                    {
                        MathInfo_BoolLogicOperation subInfo = info.SubInfo.Cast<MathInfo_BoolLogicOperation>();

                        string srcStr1 = StringEscaper.Preprocess(s, subInfo.Src1);
                        string srcStr2 = StringEscaper.Preprocess(s, subInfo.Src2);

                        bool src1;
                        if (NumberHelper.ParseInt64(srcStr1, out long srcInt1)) // C-Style Boolean
                            src1 = srcInt1 != 0;
                        else if (srcStr1.Equals("True", StringComparison.OrdinalIgnoreCase))
                            src1 = true;
                        else if (srcStr1.Equals("False", StringComparison.OrdinalIgnoreCase))
                            src1 = false;
                        else
                            return LogInfo.LogErrorMessage(logs, $"[{srcStr1}] is not valid boolean value");

                        bool src2;
                        if (NumberHelper.ParseInt64(srcStr2, out long srcInt2)) // C-Style Boolean
                            src2 = srcInt2 != 0;
                        else if (srcStr2.Equals("True", StringComparison.OrdinalIgnoreCase))
                            src2 = true;
                        else if (srcStr2.Equals("False", StringComparison.OrdinalIgnoreCase))
                            src2 = false;
                        else
                            return LogInfo.LogErrorMessage(logs, $"[{srcStr2}] is not valid boolean value");

                        bool dest;
                        switch (type)
                        {
                            case MathType.BoolAnd:
                                dest = src1 && src2;
                                break;
                            case MathType.BoolOr:
                                dest = src1 || src2;
                                break;
                            case MathType.BoolXor:
                                dest = src1 ^ src2;
                                break;
                            default:
                                throw new InternalException("Internal Logic Error at Math,BoolLogicOper");
                        }

                        logs.AddRange(Variables.SetVariable(s, subInfo.DestVar, dest.ToString()));
                    }
                    break;
                case MathType.BoolNot:
                    {
                        MathInfo_BoolNot subInfo = info.SubInfo.Cast<MathInfo_BoolNot>();

                        bool src;
                        string srcStr = StringEscaper.Preprocess(s, subInfo.Src);
                        if (NumberHelper.ParseInt64(srcStr, out long srcInt)) // C-Style Boolean
                            src = srcInt != 0;
                        else if (srcStr.Equals("True", StringComparison.OrdinalIgnoreCase))
                            src = true;
                        else if (srcStr.Equals("False", StringComparison.OrdinalIgnoreCase))
                            src = false;
                        else
                            return LogInfo.LogErrorMessage(logs, $"[{srcStr}] is not valid boolean value");

                        bool dest = !src;
                        logs.AddRange(Variables.SetVariable(s, subInfo.DestVar, dest.ToString()));
                    }
                    break;
                case MathType.BitAnd:
                case MathType.BitOr:
                case MathType.BitXor:
                    {
                        MathInfo_BitLogicOperation subInfo = info.SubInfo.Cast<MathInfo_BitLogicOperation>();

                        string srcStr1 = StringEscaper.Preprocess(s, subInfo.Src1);
                        string srcStr2 = StringEscaper.Preprocess(s, subInfo.Src2);

                        if (!NumberHelper.ParseUInt64(srcStr1, out ulong src1))
                            return LogInfo.LogErrorMessage(logs, $"[{srcStr1}] is not a valid integer");
                        if (!NumberHelper.ParseUInt64(srcStr2, out ulong src2))
                            return LogInfo.LogErrorMessage(logs, $"[{srcStr2}] is not a valid integer");

                        ulong dest;
                        switch (type)
                        {
                            case MathType.BitAnd:
                                dest = src1 & src2;
                                break;
                            case MathType.BitOr:
                                dest = src1 | src2;
                                break;
                            case MathType.BitXor:
                                dest = src1 ^ src2;
                                break;
                            default:
                                throw new InternalException("Internal Logic Error at Math,BitLogicOper");
                        }

                        string destStr = dest.ToString();
                        logs.AddRange(Variables.SetVariable(s, subInfo.DestVar, destStr));
                    }
                    break;
                case MathType.BitNot:
                    {
                        MathInfo_BitNot subInfo = info.SubInfo.Cast<MathInfo_BitNot>();

                        string srcStr = StringEscaper.Preprocess(s, subInfo.Src);
                        string bitSizeStr = StringEscaper.Preprocess(s, subInfo.BitSize);
                        string? errorMsg = ParseAndCheckBitSize(bitSizeStr, out int bitSize);
                        if (errorMsg != null)
                            return LogInfo.LogErrorMessage(logs, errorMsg);

                        string destStr;
                        switch (bitSize)
                        {
                            case 8:
                                {
                                    if (!NumberHelper.ParseUInt8(srcStr, out byte src))
                                        return LogInfo.LogErrorMessage(logs, $"[{srcStr}] is not a valid integer");

                                    destStr = ((byte)~src).ToString();
                                }
                                break;
                            case 16:
                                {
                                    if (!NumberHelper.ParseUInt16(srcStr, out ushort src))
                                        return LogInfo.LogErrorMessage(logs, $"[{srcStr}] is not a valid integer");

                                    destStr = ((ushort)~src).ToString();
                                }
                                break;
                            case 32:
                                {
                                    if (!NumberHelper.ParseUInt32(srcStr, out uint src))
                                        return LogInfo.LogErrorMessage(logs, $"[{srcStr}] is not a valid integer");

                                    destStr = (~src).ToString();
                                }
                                break;
                            case 64:
                                {
                                    if (!NumberHelper.ParseUInt64(srcStr, out ulong src))
                                        return LogInfo.LogErrorMessage(logs, $"[{srcStr}] is not a valid integer");

                                    destStr = (~src).ToString();
                                }
                                break;
                            default:
                                throw new InternalException("Internal Logic Error at Math,BitNot");
                        }

                        logs.AddRange(Variables.SetVariable(s, subInfo.DestVar, destStr));
                    }
                    break;
                case MathType.BitShift:
                    {
                        MathInfo_BitShift subInfo = info.SubInfo.Cast<MathInfo_BitShift>();

                        string srcStr = StringEscaper.Preprocess(s, subInfo.Src);

                        string shiftStr = StringEscaper.Preprocess(s, subInfo.Shift);
                        if (!NumberHelper.ParseInt32(shiftStr, out int shift))
                            return LogInfo.LogErrorMessage(logs, $"[{shiftStr}] is not a valid integer");

                        string directionStr = StringEscaper.Preprocess(s, subInfo.Direction);
                        bool isLeft = false;
                        if (directionStr.Equals("Left", StringComparison.OrdinalIgnoreCase))
                            isLeft = true;
                        else if (!directionStr.Equals("Right", StringComparison.OrdinalIgnoreCase))
                            return LogInfo.LogErrorMessage(logs, $"[{directionStr}] must be one of [Left, Right]");

                        string bitSizeStr = StringEscaper.Preprocess(s, subInfo.BitSize);
                        string? errorMsg = ParseAndCheckBitSize(bitSizeStr, out int bitSize);
                        if (errorMsg != null)
                            return LogInfo.LogErrorMessage(logs, errorMsg);

                        string destStr;
                        switch (bitSize)
                        {
                            case 8:
                                {
                                    if (!NumberHelper.ParseUInt8(srcStr, out byte src))
                                        return LogInfo.LogErrorMessage(logs, $"[{srcStr}] is not a valid integer");

                                    byte dest;
                                    if (isLeft)
                                        dest = (byte)(src << shift);
                                    else
                                        dest = (byte)(src >> shift);
                                    destStr = dest.ToString();
                                }
                                break;
                            case 16:
                                {
                                    if (!NumberHelper.ParseUInt16(srcStr, out ushort src))
                                        return LogInfo.LogErrorMessage(logs, $"[{srcStr}] is not a valid integer");

                                    ushort dest;
                                    if (isLeft)
                                        dest = (ushort)(src << shift);
                                    else
                                        dest = (ushort)(src >> shift);
                                    destStr = dest.ToString();
                                }
                                break;
                            case 32:
                                {
                                    if (!NumberHelper.ParseUInt32(srcStr, out uint src))
                                        return LogInfo.LogErrorMessage(logs, $"[{srcStr}] is not a valid integer");

                                    uint dest;
                                    if (isLeft)
                                        dest = src << shift;
                                    else
                                        dest = src >> shift;
                                    destStr = dest.ToString();
                                }
                                break;
                            case 64:
                                {
                                    if (!NumberHelper.ParseUInt64(srcStr, out ulong src))
                                        return LogInfo.LogErrorMessage(logs, $"[{srcStr}] is not a valid integer");

                                    ulong dest;
                                    if (isLeft)
                                        dest = src << shift;
                                    else
                                        dest = src >> shift;
                                    destStr = dest.ToString();
                                }
                                break;
                            default:
                                throw new InternalException("Internal Logic Error at Math,BitShift");
                        }

                        logs.AddRange(Variables.SetVariable(s, subInfo.DestVar, destStr));
                    }
                    break;
                case MathType.Ceil:
                case MathType.Floor:
                case MathType.Round:
                    {
                        MathInfo_CeilFloorRound subInfo = info.SubInfo.Cast<MathInfo_CeilFloorRound>();

                        string srcStr = StringEscaper.Preprocess(s, subInfo.Src);
                        if (!NumberHelper.ParseInt64(srcStr, out long srcInt))
                            return LogInfo.LogErrorMessage(logs, $"[{srcStr}] is not a valid integer");

                        string unitStr = StringEscaper.Preprocess(s, subInfo.Unit);
                        // Is roundToStr number?
                        if (!NumberHelper.ParseInt64(unitStr, out long unit))
                            return LogInfo.LogErrorMessage(logs, $"[{unitStr}] is not a valid integer");
                        if (unit < 0)
                            return LogInfo.LogErrorMessage(logs, $"[{unit}] must be positive integer");

                        long destInt;
                        long remainder = srcInt % unit;
                        switch (type)
                        {
                            case MathType.Ceil:
                                destInt = srcInt - remainder + unit;
                                break;
                            case MathType.Floor:
                                destInt = srcInt - remainder;
                                break;
                            case MathType.Round:
                                if ((unit - 1) / 2 < remainder)
                                    destInt = srcInt - remainder + unit;
                                else
                                    destInt = srcInt - remainder;
                                break;
                            default:
                                throw new InternalException($"Internal Logic Error at Math,{info.Type}");
                        }

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, destInt.ToString());
                        logs.AddRange(varLogs);
                    }
                    break;
                case MathType.Abs:
                    {
                        MathInfo_Abs subInfo = info.SubInfo.Cast<MathInfo_Abs>();

                        string srcStr = StringEscaper.Preprocess(s, subInfo.Src);
                        if (!NumberHelper.ParseDecimal(srcStr, out decimal src))
                            return LogInfo.LogErrorMessage(logs, $"[{srcStr}] is not a valid integer");

                        decimal dest = System.Math.Abs(src);
                        logs.AddRange(Variables.SetVariable(s, subInfo.DestVar, dest.ToString(CultureInfo.InvariantCulture)));
                    }
                    break;
                case MathType.Pow:
                    {
                        MathInfo_Pow subInfo = info.SubInfo.Cast<MathInfo_Pow>();

                        string baseStr = StringEscaper.Preprocess(s, subInfo.Base);
                        if (!NumberHelper.ParseDecimal(baseStr, out decimal _base))
                            return LogInfo.LogErrorMessage(logs, $"[{baseStr}] is not a valid integer");

                        string powerStr = StringEscaper.Preprocess(s, subInfo.Power);
                        if (!NumberHelper.ParseUInt32(powerStr, out uint power))
                            return LogInfo.LogErrorMessage(logs, $"[{baseStr}] is not a postivie integer");

                        decimal dest = NumberHelper.DecimalPower(_base, power);
                        logs.AddRange(Variables.SetVariable(s, subInfo.DestVar, dest.ToString(CultureInfo.InvariantCulture)));
                    }
                    break;
                case MathType.Hex:
                case MathType.Dec:
                    {
                        MathInfo_HexDec subInfo = info.SubInfo.Cast<MathInfo_HexDec>();

                        string intStr = StringEscaper.Preprocess(s, subInfo.Src);
                        string bitSizeStr = StringEscaper.Preprocess(s, subInfo.BitSize);
                        string? errorMsg = ParseAndCheckBitSize(bitSizeStr, out int bitSize);
                        if (errorMsg != null)
                            return LogInfo.LogErrorMessage(logs, errorMsg);

                        string dest;
                        switch (bitSize)
                        {
                            case 8:
                                if (!NumberHelper.ParseSignedAsUInt8(intStr, out byte u8))
                                    return LogInfo.LogErrorMessage(logs, $"[{intStr}] is not a valid 8bit integer");
                                dest = info.Type == MathType.Hex ? $"0x{u8:X2}" : u8.ToString();
                                break;
                            case 16:
                                if (!NumberHelper.ParseSignedAsUInt16(intStr, out ushort u16))
                                    return LogInfo.LogErrorMessage(logs, $"[{intStr}] is not a valid 16bit integer");
                                dest = info.Type == MathType.Hex ? $"0x{u16:X4}" : u16.ToString();
                                break;
                            case 32:
                                if (!NumberHelper.ParseSignedAsUInt32(intStr, out uint u32))
                                    return LogInfo.LogErrorMessage(logs, $"[{intStr}] is not a valid 32bit integer");
                                dest = info.Type == MathType.Hex ? $"0x{u32:X8}" : u32.ToString();
                                break;
                            case 64:
                                if (!NumberHelper.ParseSignedAsUInt64(intStr, out ulong u64))
                                    return LogInfo.LogErrorMessage(logs, $"[{intStr}] is not a valid 64bit integer");
                                dest = info.Type == MathType.Hex ? $"0x{u64:X16}" : u64.ToString();
                                break;
                            default:
                                throw new InternalException($"Internal Logic Error at Math,{info.Type}");
                        }

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, dest);
                        logs.AddRange(varLogs);
                    }
                    break;
                case MathType.Rand:
                    {
                        MathInfo_Rand subInfo = info.SubInfo.Cast<MathInfo_Rand>();

                        int min = 0;
                        if (subInfo.Min != null)
                        {
                            string minStr = StringEscaper.Preprocess(s, subInfo.Min);
                            if (!NumberHelper.ParseInt32(minStr, out min))
                                return LogInfo.LogErrorMessage(logs, $"[{minStr}] is not a valid integer");
                            if (min < 0)
                                return LogInfo.LogErrorMessage(logs, $"[{min}] must be positive integer");
                        }

                        int max = 65536;
                        if (subInfo.Max != null)
                        {
                            string maxStr = StringEscaper.Preprocess(s, subInfo.Max);
                            if (!NumberHelper.ParseInt32(maxStr, out max))
                                return LogInfo.LogErrorMessage(logs, $"[{maxStr}] is not a valid integer");
                            if (max < 0)
                                return LogInfo.LogErrorMessage(logs, $"[{max}] must be positive integer");
                            if (max <= min)
                                return LogInfo.LogErrorMessage(logs, "Maximum bounds must be larger than minimum value");
                        }

                        int destInt = s.Random.Next() % (max - min) + min;

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, destInt.ToString());
                        logs.AddRange(varLogs);
                    }
                    break;
                default: // Error
                    throw new InternalException("Internal Logic Error at CommandMath.Math");
            }

            return logs;
        }

        /// <summary>
        /// Parse and check bitSizeStr
        /// </summary>
        /// <param name="bitSizeStr">String to parse</param>
        /// <param name="bitSize">Parsed bitSize integer</param>
        /// <returns>Null if succeed, an error message string if failed</returns>
        public static string? ParseAndCheckBitSize(string bitSizeStr, out int bitSize)
        {
            if (!NumberHelper.ParseInt32(bitSizeStr, out bitSize))
                return $"[{bitSizeStr}] is not a valid integer";
            if (!(bitSize == 8 || bitSize == 16 || bitSize == 32 || bitSize == 64))
                return $"[{bitSizeStr}] is not a valid bit size";
            return null;
        }
    }
}
