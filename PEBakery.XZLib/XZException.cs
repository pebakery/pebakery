/*
    Derived from liblzma header files (Public Domain)

    C# Wrapper written by Hajin Jang
    Copyright (C) 2018 Hajin Jang

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
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PEBakery.XZLib
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class XZException : Exception
    {
        public LzmaRet Ret;

        private static readonly Dictionary<LzmaRet, string> ErrorMsgDict = new Dictionary<LzmaRet, string>
        {
            [LzmaRet.NO_CHECK] = "No integrity check; not verifying file integrity",
            [LzmaRet.UNSUPPORTED_CHECK] = "Unsupported type of integrity check; not verifying file integrity",
            [LzmaRet.MEM_ERROR] = "Not Enough Memory",
            [LzmaRet.MEMLIMIT_ERROR] = "Memory usage limit reached",
            [LzmaRet.OPTIONS_ERROR] = "Unsupported options",
            [LzmaRet.DATA_ERROR] = "Compressed data is corrupt",
            [LzmaRet.BUF_ERROR] = "Unexpected end of input",
        };

        private static string GetErrorMessage(LzmaRet ret) => ErrorMsgDict.ContainsKey(ret) ? ErrorMsgDict[ret] : ret.ToString();

        public XZException(LzmaRet ret) : base(GetErrorMessage(ret))
        {
            Ret = ret;
        }

        public static void CheckLzmaError(LzmaRet ret)
        {
            if (ret != LzmaRet.OK)
                throw new XZException(ret);
        }
    }
}
