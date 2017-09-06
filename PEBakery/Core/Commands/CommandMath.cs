/*
    Copyright (C) 2016-2017 Hajin Jang
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
*/

using PEBakery.Exceptions;
using PEBakery.Helper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PEBakery.Core.Commands
{
    public static class CommandMath
    {
        public static List<LogInfo> Math(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Math));
            CodeInfo_Math info = cmd.Info as CodeInfo_Math;

            MathType type = info.Type;
            switch (type)
            {
                case MathType.Add:
                case MathType.Sub:
                case MathType.Mul:
                case MathType.Div:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(MathInfo_Arithmetic));
                        MathInfo_Arithmetic subInfo = info.SubInfo as MathInfo_Arithmetic;

                        string srcStr1 = StringEscaper.Preprocess(s, subInfo.Src1);
                        string srcStr2 = StringEscaper.Preprocess(s, subInfo.Src2);

                        if (!NumberHelper.ParseDecimal(srcStr1, out decimal src1))
                            throw new ExecuteException($"[{srcStr1}] is not valid integer");
                        if (!NumberHelper.ParseDecimal(srcStr2, out decimal src2))
                            throw new ExecuteException($"[{srcStr2}] is not valid integer");

                        decimal destInt;
                        if (type == MathType.Add)
                            destInt = src1 + src2;
                        else if (type == MathType.Sub)
                            destInt = src1 - src2;
                        else if (type == MathType.Mul)
                            destInt = src1 * src2;
                        else if (type == MathType.Div)
                            destInt = src1 / src2;
                        else
                            throw new InternalException($"Internal Logic Error");

                        logs.AddRange(Variables.SetVariable(s, subInfo.DestVar, destInt.ToString()));

                        /*
                        NumberHelper.StringNumberType strType1 = NumberHelper.IsStringHexInteger(srcStr1);
                        NumberHelper.StringNumberType strType2 = NumberHelper.IsStringHexInteger(srcStr2);

                        if (strType1 == NumberHelper.StringNumberType.NotNumber ||
                            strType2 == NumberHelper.StringNumberType.NotNumber) // not number
                        {
                            throw new ExecuteException($"[{srcStr1}] is not a number");
                        }
                        else if (strType1 == NumberHelper.StringNumberType.Decimal ||
                            strType2 == NumberHelper.StringNumberType.Decimal) // decimal
                        {
                            if (!NumberHelper.ParseDecimal(srcStr1, out decimal src1))
                                throw new ExecuteException($"[{srcStr1}] is not valid integer");
                            if (!NumberHelper.ParseDecimal(srcStr2, out decimal src2))
                                throw new ExecuteException($"[{srcStr2}] is not valid integer");

                            decimal destInt;
                            if (type == MathType.Add)
                                destInt = src1 + src2;
                            else if (type == MathType.Sub)
                                destInt = src1 - src2;
                            else if (type == MathType.Mul)
                                destInt = src1 * src2;
                            else
                                throw new InternalException($"Internal Logic Error");
                            destStr = destInt.ToString();
                        }
                        else if (strType1 == NumberHelper.StringNumberType.NegativeInteger ||
                            strType2 == NumberHelper.StringNumberType.NegativeInteger) // long
                        {
                            if (!NumberHelper.ParseInt64(srcStr1, out long src1))
                                throw new ExecuteException($"[{srcStr1}] is not valid integer");
                            if (!NumberHelper.ParseInt64(srcStr2, out long src2))
                                throw new ExecuteException($"[{srcStr2}] is not valid integer");

                            long destInt;
                            if (type == MathType.Add)
                                destInt = src1 + src2;
                            else if (type == MathType.Sub)
                                destInt = src1 - src2;
                            else if (type == MathType.Mul)
                                destInt = src1 * src2;
                            else
                                throw new InternalException($"Internal Logic Error");
                            destStr = destInt.ToString();
                        }
                        else
                        {
                            if (!NumberHelper.ParseInt64(srcStr1, out long src1))
                                throw new ExecuteException($"[{srcStr1}] is not valid integer");
                            if (!NumberHelper.ParseInt64(srcStr2, out long src2))
                                throw new ExecuteException($"[{srcStr2}] is not valid integer");

                            long destInt;
                            if (type == MathType.Add)
                                destInt = src1 + src2;
                            else if (type == MathType.Sub)
                                destInt = src1 - src2;
                            else if (type == MathType.Mul)
                                destInt = src1 * src2;
                            else
                                throw new InternalException($"Internal Logic Error");
                            destStr = destInt.ToString();
                        }
                        */
                    }
                    break;
                case MathType.IntDiv:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(MathInfo_IntDiv));
                        MathInfo_IntDiv subInfo = info.SubInfo as MathInfo_IntDiv;

                        string srcStr1 = StringEscaper.Preprocess(s, subInfo.Src1);
                        string srcStr2 = StringEscaper.Preprocess(s, subInfo.Src2);
                        
                        if (srcStr1.StartsWith("-", StringComparison.Ordinal) ||
                            srcStr2.StartsWith("-", StringComparison.Ordinal))
                        { // Signed
                            if (!NumberHelper.ParseInt64(srcStr1, out long src1))
                                throw new ExecuteException($"[{srcStr1}] is not valid integer");
                            if (!NumberHelper.ParseInt64(srcStr2, out long src2))
                                throw new ExecuteException($"[{srcStr2}] is not valid integer");

                            long q = src1 / src2;
                            long r = src1 % src2;

                            logs.AddRange(Variables.SetVariable(s, subInfo.QuotientVar, q.ToString()));
                            logs.AddRange(Variables.SetVariable(s, subInfo.RemainderVar, r.ToString()));
                        }
                        else
                        { // Unsigned
                            if (!NumberHelper.ParseUInt64(srcStr1, out ulong src1))
                                throw new ExecuteException($"[{srcStr1}] is not valid integer");
                            if (!NumberHelper.ParseUInt64(srcStr2, out ulong src2))
                                throw new ExecuteException($"[{srcStr2}] is not valid integer");

                            ulong q = src1 / src2;
                            ulong r = src1 % src2;

                            logs.AddRange(Variables.SetVariable(s, subInfo.QuotientVar, q.ToString()));
                            logs.AddRange(Variables.SetVariable(s, subInfo.RemainderVar, r.ToString()));
                        } 
                    }
                    break;
                case MathType.BoolAnd:
                case MathType.BoolOr:
                case MathType.BoolXor:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(MathInfo_BoolLogicOper));
                        MathInfo_BoolLogicOper subInfo = info.SubInfo as MathInfo_BoolLogicOper;

                        string srcStr1 = StringEscaper.Preprocess(s, subInfo.Src1);
                        string srcStr2 = StringEscaper.Preprocess(s, subInfo.Src2);

                        bool src1 = false;
                        if (srcStr1.Equals("True", StringComparison.OrdinalIgnoreCase))
                            src1 = true;
                        else if (!srcStr1.Equals("False", StringComparison.OrdinalIgnoreCase))
                            throw new ExecuteException($"[{srcStr1}] is not valid boolean value");

                        bool src2 = false;
                        if (srcStr2.Equals("True", StringComparison.OrdinalIgnoreCase))
                            src2 = true;
                        else if (!srcStr2.Equals("False", StringComparison.OrdinalIgnoreCase))
                            throw new ExecuteException($"[{srcStr2}] is not valid boolean value");

                        bool dest;
                        if (type == MathType.BoolAnd)
                            dest = src1 && src2;
                        else if (type == MathType.BoolOr)
                            dest = src1 || src2;
                        else if (type == MathType.BoolXor)
                            dest = src1 ^ src2;
                        else
                            throw new InternalException($"Internal Logic Error");

                        logs.AddRange(Variables.SetVariable(s, subInfo.DestVar, dest.ToString()));
                    }
                    break;
                case MathType.BoolNot:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(MathInfo_BoolNot));
                        MathInfo_BoolNot subInfo = info.SubInfo as MathInfo_BoolNot;

                        bool src = false;
                        string srcStr = StringEscaper.Preprocess(s, subInfo.Src);
                        if (srcStr.Equals("True", StringComparison.OrdinalIgnoreCase))
                            src = true;
                        else if (!srcStr.Equals("False", StringComparison.OrdinalIgnoreCase))
                            throw new ExecuteException($"[{srcStr}] is not valid boolean value");

                        bool dest = !src;
                        logs.AddRange(Variables.SetVariable(s, subInfo.DestVar, dest.ToString()));
                    }
                    break;
                case MathType.BitAnd:
                case MathType.BitOr:
                case MathType.BitXor:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(MathInfo_BitLogicOper));
                        MathInfo_BitLogicOper subInfo = info.SubInfo as MathInfo_BitLogicOper;

                        string srcStr1 = StringEscaper.Preprocess(s, subInfo.Src1);
                        string srcStr2 = StringEscaper.Preprocess(s, subInfo.Src2);
                        string destStr;

                        switch (subInfo.Size)
                        {
                            case 8:
                                {
                                    if (!NumberHelper.ParseUInt8(srcStr1, out byte src1))
                                        throw new ExecuteException($"[{srcStr1}] is not valid integer");
                                    if (!NumberHelper.ParseUInt8(srcStr2, out byte src2))
                                        throw new ExecuteException($"[{srcStr2}] is not valid integer");

                                    byte dest;
                                    if (type == MathType.BitAnd)
                                        dest = (byte)(src1 & src2);
                                    else if (type == MathType.BitOr)
                                        dest = (byte)(src1 | src2);
                                    else if (type == MathType.BitXor)
                                        dest = (byte)(src1 ^ src2);
                                    else
                                        throw new InternalException($"Internal Logic Error");

                                    destStr = dest.ToString();
                                }
                                break;
                            case 16:
                                {
                                    if (!NumberHelper.ParseUInt16(srcStr1, out ushort src1))
                                        throw new ExecuteException($"[{srcStr1}] is not valid integer");
                                    if (!NumberHelper.ParseUInt16(srcStr2, out ushort src2))
                                        throw new ExecuteException($"[{srcStr2}] is not valid integer");

                                    ushort dest;
                                    if (type == MathType.BitAnd)
                                        dest = (ushort)(src1 & src2);
                                    else if (type == MathType.BitOr)
                                        dest = (ushort)(src1 | src2);
                                    else if (type == MathType.BitXor)
                                        dest = (ushort)(src1 ^ src2);
                                    else
                                        throw new InternalException($"Internal Logic Error");

                                    destStr = dest.ToString();
                                }
                                break;
                            case 32:
                                {
                                    if (!NumberHelper.ParseUInt32(srcStr1, out uint src1))
                                        throw new ExecuteException($"[{srcStr1}] is not valid integer");
                                    if (!NumberHelper.ParseUInt32(srcStr2, out uint src2))
                                        throw new ExecuteException($"[{srcStr2}] is not valid integer");

                                    uint dest;
                                    if (type == MathType.BitAnd)
                                        dest = src1 & src2;
                                    else if (type == MathType.BitOr)
                                        dest = src1 | src2;
                                    else if (type == MathType.BitXor)
                                        dest = src1 ^ src2;
                                    else
                                        throw new InternalException($"Internal Logic Error");

                                    destStr = dest.ToString();
                                }
                                break;
                            case 64:
                                {
                                    if (!NumberHelper.ParseUInt64(srcStr1, out ulong src1))
                                        throw new ExecuteException($"[{srcStr1}] is not valid integer");
                                    if (!NumberHelper.ParseUInt64(srcStr2, out ulong src2))
                                        throw new ExecuteException($"[{srcStr2}] is not valid integer");

                                    ulong dest;
                                    if (type == MathType.BitAnd)
                                        dest = src1 & src2;
                                    else if (type == MathType.BitOr)
                                        dest = src1 | src2;
                                    else if (type == MathType.BitXor)
                                        dest = src1 ^ src2;
                                    else
                                        throw new InternalException($"Internal Logic Error");

                                    destStr = dest.ToString();
                                }
                                break;
                            default:
                                throw new InternalException($"Internal Logic Error");
                        }

                        logs.AddRange(Variables.SetVariable(s, subInfo.DestVar, destStr));
                    }
                    break;
                case MathType.BitNot:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(MathInfo_BitNot));
                        MathInfo_BitNot subInfo = info.SubInfo as MathInfo_BitNot;

                        string srcStr = StringEscaper.Preprocess(s, subInfo.Src);
                        string destStr;

                        switch (subInfo.Size)
                        {
                            case 8:
                                {
                                    if (!NumberHelper.ParseUInt8(srcStr, out byte src))
                                        throw new ExecuteException($"[{srcStr}] is not valid integer");

                                    destStr = (~src).ToString();
                                }
                                break;
                            case 16:
                                {
                                    if (!NumberHelper.ParseUInt16(srcStr, out ushort src))
                                        throw new ExecuteException($"[{srcStr}] is not valid integer");

                                    destStr = (~src).ToString();
                                }
                                break;
                            case 32:
                                {
                                    if (!NumberHelper.ParseUInt32(srcStr, out uint src))
                                        throw new ExecuteException($"[{srcStr}] is not valid integer");

                                    destStr = (~src).ToString();
                                }
                                break;
                            case 64:
                                {
                                    if (!NumberHelper.ParseUInt64(srcStr, out ulong src))
                                        throw new ExecuteException($"[{srcStr}] is not valid integer");

                                    destStr = (~src).ToString();
                                }
                                break;
                            default:
                                throw new InternalException($"Internal Logic Error");
                        }

                        logs.AddRange(Variables.SetVariable(s, subInfo.DestVar, destStr));
                    }
                    break;
                case MathType.BitShift:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(MathInfo_BitShift));
                        MathInfo_BitShift subInfo = info.SubInfo as MathInfo_BitShift;

                        string srcStr = StringEscaper.Preprocess(s, subInfo.Src);

                        string shiftStr = StringEscaper.Preprocess(s, subInfo.Shift);
                        if (!NumberHelper.ParseInt32(shiftStr, out int shift))
                            throw new ExecuteException($"[{shiftStr}] is not valid integer");

                        string leftRightStr = StringEscaper.Preprocess(s, subInfo.LeftRight);
                        bool isLeft = false;
                        if (leftRightStr.Equals("Left", StringComparison.OrdinalIgnoreCase))
                            isLeft = true;
                        else if (!leftRightStr.Equals("Right", StringComparison.OrdinalIgnoreCase))
                            throw new ExecuteException($"[{leftRightStr}] must be one of [Left, Right]");

                        string destStr;
                        switch (subInfo.Size)
                        {
                            case 8:
                                {
                                    if (!NumberHelper.ParseUInt8(srcStr, out byte src))
                                        throw new ExecuteException($"[{srcStr}] is not valid integer");

                                    byte dest;
                                    if (isLeft)
                                        dest = (byte)(src << shift);
                                    else
                                        dest = (byte)(src >> shift);
                                    destStr = src.ToString();
                                }
                                break;
                            case 16:
                                {
                                    if (!NumberHelper.ParseUInt16(srcStr, out ushort src))
                                        throw new ExecuteException($"[{srcStr}] is not valid integer");

                                    ushort dest;
                                    if (isLeft)
                                        dest = (ushort)(src << shift);
                                    else
                                        dest = (ushort)(src >> shift);
                                    destStr = src.ToString();
                                }
                                break;
                            case 32:
                                {
                                    if (!NumberHelper.ParseUInt32(srcStr, out uint src))
                                        throw new ExecuteException($"[{srcStr}] is not valid integer");

                                    uint dest;
                                    if (isLeft)
                                        dest = (src << shift);
                                    else
                                        dest = (src >> shift);
                                    destStr = src.ToString();
                                }
                                break;
                            case 64:
                                {
                                    if (!NumberHelper.ParseUInt64(srcStr, out ulong src))
                                        throw new ExecuteException($"[{srcStr}] is not valid integer");

                                    ulong dest;
                                    if (isLeft)
                                        dest = (src << shift);
                                    else
                                        dest = (src >> shift);
                                    destStr = src.ToString();
                                }
                                break;
                            default:
                                throw new InternalException($"Internal Logic Error");
                        }

                        logs.AddRange(Variables.SetVariable(s, subInfo.DestVar, destStr));
                    }
                    break;
                case MathType.Ceil:
                case MathType.Floor:
                case MathType.Round:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(MathInfo_CeilFloorRound));
                        MathInfo_CeilFloorRound subInfo = info.SubInfo as MathInfo_CeilFloorRound;

                        // subInfo.SizeVar;
                        string unitStr = StringEscaper.Preprocess(s, subInfo.Unit);
                        // Is roundToStr number?
                        if (!NumberHelper.ParseInt64(unitStr, out long unit))
                            throw new ExecuteException($"[{unitStr}] is not valid integer");
                        if (unit < 0)
                            throw new ExecuteException($"[{unit}] must be positive integer");

                        string srcStr = StringEscaper.Preprocess(s, subInfo.Src);
                        if (!NumberHelper.ParseInt64(srcStr, out long srcInt))
                            throw new ExecuteException($"[{srcStr}] is not valid integer");

                        long destInt;
                        long remainder = srcInt % unit;
                        if (type == MathType.Ceil)
                        {
                            destInt = srcInt - remainder;
                        }
                        else if (type == MathType.Floor)
                        {
                            destInt = srcInt - remainder + unit;
                        }
                        else // if (type == StrFormatType.Round)
                        {
                            if ((unit - 1) / 2 < remainder)
                                destInt = srcInt - remainder + unit;
                            else
                                destInt = srcInt - remainder;
                        }

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, destInt.ToString());
                        logs.AddRange(varLogs);
                    }
                    break;
                case MathType.Abs:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(MathInfo_Abs));
                        MathInfo_Abs subInfo = info.SubInfo as MathInfo_Abs;

                        string srcStr = StringEscaper.Preprocess(s, subInfo.Src);
                        if (!NumberHelper.ParseInt64(srcStr, out long src))
                            throw new ExecuteException($"[{srcStr}] is not valid integer");

                        long dest = System.Math.Abs(src);
                        logs.AddRange(Variables.SetVariable(s, subInfo.DestVar, dest.ToString()));
                    }
                    break;
                case MathType.Pow:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(MathInfo_Pow));
                        MathInfo_Pow subInfo = info.SubInfo as MathInfo_Pow;

                        string baseStr = StringEscaper.Preprocess(s, subInfo.Base);
                        if (!NumberHelper.ParseDecimal(baseStr, out decimal _base))
                            throw new ExecuteException($"[{baseStr}] is not valid integer");

                        string powerStr = StringEscaper.Preprocess(s, subInfo.Power);
                        if (!NumberHelper.ParseInt32(baseStr, out int power))
                            throw new ExecuteException($"[{baseStr}] is not valid integer");

                        decimal dest = NumberHelper.DecimalPower(_base, power);
                        logs.AddRange(Variables.SetVariable(s, subInfo.DestVar, dest.ToString()));
                    }
                    break;
                default: // Error
                    throw new InvalidCodeCommandException($"Wrong MathType [{type}]");
            }

            return logs;
        }
    }
}
