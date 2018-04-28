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
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PEBakery.LZ4Lib
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class LZ4FrameException : Exception
    {
        public ulong Code;

        private static string FrameGetErrorName(UIntPtr code)
        {
            if (!NativeMethods.Loaded)
                throw new InvalidOperationException(NativeMethods.MsgAlreadyInited);

            IntPtr strPtr = NativeMethods.GetErrorName(code);
            return Marshal.PtrToStringAnsi(strPtr);
        }

        public LZ4FrameException(UIntPtr code) : base(FrameGetErrorName(code))
        {
            if (!NativeMethods.Loaded)
                throw new InvalidOperationException(NativeMethods.MsgAlreadyInited);

            Code = code.ToUInt64();
        }

        public static void CheckLZ4Error(UIntPtr code)
        {
            if (!NativeMethods.Loaded)
                throw new InvalidOperationException(NativeMethods.MsgAlreadyInited);

            if (NativeMethods.FrameIsError(code) != 0)
                throw new LZ4FrameException(code);
        }
    }
}
