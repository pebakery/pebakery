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
using System.ComponentModel;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

// ReSharper disable ArrangeTypeMemberModifiers
// ReSharper disable InconsistentNaming

namespace PEBakery.XZLib
{
    #region SafeLibraryHandle
    [SecurityPermission(SecurityAction.InheritanceDemand, UnmanagedCode = true)]
    [SecurityPermission(SecurityAction.Demand, UnmanagedCode = true)]
    public class SafeLibraryHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        #region Managed Methods
        public SafeLibraryHandle() : base(true) { }

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        protected override bool ReleaseHandle()
        {
            return FreeLibrary(handle);
        }
        #endregion

        #region Windows API
        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool FreeLibrary(IntPtr hModule);
        #endregion
    }
    #endregion

    #region PinnedObject, PinnedArray
    internal class PinnedObject : IDisposable
    {
        private GCHandle _hObject;
        public IntPtr Ptr => _hObject.AddrOfPinnedObject();

        public PinnedObject(object _object)
        {
            _hObject = GCHandle.Alloc(_object, GCHandleType.Pinned);
        }

        ~PinnedObject()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_hObject.IsAllocated)
                    _hObject.Free();
            }
        }
    }

    internal class PinnedArray : IDisposable
    {
        private GCHandle hArray;
        public Array Array;
        public IntPtr Ptr => hArray.AddrOfPinnedObject();

        public IntPtr this[int idx] => Marshal.UnsafeAddrOfPinnedArrayElement(Array, idx);
        public static implicit operator IntPtr(PinnedArray fixedArray) => fixedArray[0];

        public PinnedArray(Array array)
        {
            Array = array;
            hArray = GCHandle.Alloc(array, GCHandleType.Pinned);
        }

        ~PinnedArray()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (hArray.IsAllocated)
                    hArray.Free();
            }
        }
    }
    #endregion

    #region NativeMethods
    internal static class NativeMethods
    {
        #region Const
        public const string MsgInitFirstError = "Please call XZStream.GlobalInit() first!";
        public const string MsgAlreadyInited = "PEBakery.XZLib is already initialized.";
        #endregion

        #region Fields
        public static SafeLibraryHandle hModule = null;
        public static bool Loaded => hModule != null;

        public static string ErrorFile = null;
        public static bool PrintErrorsEnabled = false;
        #endregion

        #region LoadFunctions, ResetFunctions
        private static Delegate GetFuncPtr(string exportFunc, Type delegateType)
        {
            IntPtr funcPtr = GetProcAddress(hModule, exportFunc);
            if (funcPtr == null || funcPtr == IntPtr.Zero)
                throw new ArgumentException($"Cannot import [{exportFunc}]", new Win32Exception());
            return Marshal.GetDelegateForFunctionPointer(funcPtr, delegateType);
        }

        internal static void LoadFuntions()
        {
            #region Base - LzmaCode, LzmaEnd, LzmaGetProgress
            LzmaCode = (lzma_code)GetFuncPtr("lzma_code", typeof(lzma_code));
            LzmaEnd = (lzma_end)GetFuncPtr("lzma_end", typeof(lzma_end));
            LzmaGetProgress = (lzma_get_progress)GetFuncPtr("lzma_get_progress", typeof(lzma_get_progress));
            #endregion

            #region Container - Encoders and Decoders
            LzmaEasyEncoder = (lzma_easy_encoder)GetFuncPtr("lzma_easy_encoder", typeof(lzma_easy_encoder));
            LzmaEasyBufferEncode = (lzma_easy_buffer_encode)GetFuncPtr("lzma_easy_buffer_encode", typeof(lzma_easy_buffer_encode));
            LzmaStreamEncoder = (lzma_stream_encoder)GetFuncPtr("lzma_stream_encoder", typeof(lzma_stream_encoder));
            LzmaStreamEncoderMt = (lzma_stream_encoder_mt)GetFuncPtr("lzma_stream_encoder_mt", typeof(lzma_stream_encoder_mt));
            LzmaStreamBufferEncode = (lzma_stream_buffer_encode)GetFuncPtr("lzma_stream_buffer_encode", typeof(lzma_stream_buffer_encode));
            LzmaStreamDecoder = (lzma_stream_decoder)GetFuncPtr("lzma_stream_decoder", typeof(lzma_stream_decoder));
            LzmaStreamBufferDecode = (lzma_stream_buffer_decode)GetFuncPtr("lzma_stream_buffer_decode", typeof(lzma_stream_buffer_decode));
            #endregion

            #region Version - LzmaVersionNumber, LzmaVersionString
            LzmaVersionNumber = (lzma_version_number)GetFuncPtr("lzma_version_number", typeof(lzma_version_number));
            LzmaVersionString = (lzma_version_string)GetFuncPtr("lzma_version_string", typeof(lzma_version_string));
            #endregion
        }

        internal static void ResetFuntions()
        {
            #region Base - LzmaCode, LzmaEnd, LzmaGetProgress
            LzmaCode = null;
            LzmaEnd = null;
            LzmaGetProgress = null;
            #endregion

            #region Container - Encoders and Decoders
            LzmaEasyEncoder = null;
            LzmaEasyBufferEncode = null;
            LzmaStreamEncoder = null;
            LzmaStreamEncoderMt = null;
            LzmaStreamBufferEncode = null;
            LzmaStreamDecoder = null;
            LzmaStreamBufferDecode = null;
            #endregion

            #region Version - LzmaVersionNumber, LzmaVersionString
            LzmaVersionNumber = null;
            LzmaVersionString = null;
            #endregion
        }
        #endregion

        #region Windows API
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern SafeLibraryHandle LoadLibrary([MarshalAs(UnmanagedType.LPWStr)] string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        internal static extern IntPtr GetProcAddress(SafeLibraryHandle hModule, [MarshalAs(UnmanagedType.LPStr)] string procName);
        #endregion

        #region liblzma Function Pointer
        #region Base - LzmaCode, LzmaEnd, LzmaGetProgress
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate LzmaRet lzma_code(
            LzmaStream strm,
            LzmaAction action);
        internal static lzma_code LzmaCode;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void lzma_end(LzmaStream strm);
        internal static lzma_end LzmaEnd;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void lzma_get_progress(
            LzmaStream strm,
            ref ulong progress_in,
            ref ulong progress_out);
        internal static lzma_get_progress LzmaGetProgress;
        #endregion

        #region Container - Encoders and Decoders
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate LzmaRet lzma_easy_encoder(
            LzmaStream strm,
            uint preset,
            LzmaCheck check);
        internal static lzma_easy_encoder LzmaEasyEncoder;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate LzmaRet lzma_easy_buffer_encode(
            uint preset,
            LzmaCheck check,
            IntPtr allocator,
            IntPtr in_buf,
            UIntPtr in_size, // size_t
            IntPtr out_buf,
            ref UIntPtr out_pos, // size_t
            UIntPtr out_size); // size_t
        internal static lzma_easy_buffer_encode LzmaEasyBufferEncode;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate LzmaRet lzma_stream_encoder(
            LzmaStream strm,
            [MarshalAs(UnmanagedType.LPArray)] LzmaFilter[] filters,
            LzmaCheck check);
        internal static lzma_stream_encoder LzmaStreamEncoder;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate LzmaRet lzma_stream_encoder_mt(
            LzmaStream strm,
            LzmaMt options);
        internal static lzma_stream_encoder_mt LzmaStreamEncoderMt;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate LzmaRet lzma_stream_buffer_encode(
            [MarshalAs(UnmanagedType.LPArray)] LzmaFilter[] filters,
            LzmaCheck check,
            IntPtr allocator,
            IntPtr in_buf,
            UIntPtr in_size, // size_t
            IntPtr out_buf,
            ref UIntPtr out_pos, // size_t
            UIntPtr out_size); // size_t
        internal static lzma_stream_buffer_encode LzmaStreamBufferEncode;

        /// <summary>
        /// Initialize .xz Stream decoder
        /// </summary>
        /// <param name="strm">Pointer to properly prepared lzma_stream</param>
        /// <param name="memlimit">
        /// Memory usage limit as bytes.
        /// Use UINT64_MAX to effectively disable the limiter.
        /// </param>
        /// <param name="flags">
        /// Bitwise-or of zero or more of the decoder flags
        /// </param>
        /// <returns>
        /// LZMA_OK: Initialization was successful.
        /// LZMA_MEM_ERROR: Cannot allocate memory.
        /// LZMA_OPTIONS_ERROR: Unsupported flags
        /// LZMA_PROG_ERROR
        /// </returns>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate LzmaRet lzma_stream_decoder(
            LzmaStream strm,
            ulong memlimit,
            LzmaDecodingFlag flags);
        internal static lzma_stream_decoder LzmaStreamDecoder;

        /// <summary>
        /// Single-call .xz Stream decoder
        /// </summary>
        /// <param name="memlimit">
        /// Pointer to how much memory the decoder is allowed
        /// to allocate. The value pointed by this pointer is
        /// modified if and only if LZMA_MEMLIMIT_ERROR is
        /// returned.
        /// </param>
        /// <param name="flags">
        /// Bitwise-or of zero or more of the decoder flags:
        /// LZMA_TELL_NO_CHECK, LZMA_TELL_UNSUPPORTED_CHECK,
        /// LZMA_CONCATENATED. Note that LZMA_TELL_ANY_CHECK
        /// is not allowed and will return LZMA_PROG_ERROR.
        /// </param>
        /// <param name="allocator">
        /// lzma_allocator for custom allocator functions.
        /// Set to NULL to use malloc() and free().
        /// </param>
        /// <param name="in_buf">
        /// Beginning of the input buffer
        /// </param>
        /// <param name="in_pos">
        /// The next byte will be read from in[*in_pos].
        /// *in_pos is updated only if decoding succeeds.
        /// </param>
        /// <param name="in_size">
        /// Size of the input buffer; the first byte that
        /// won't be read is in[in_size].
        /// </param>
        /// <param name="out_buf">
        /// Beginning of the output buffer
        /// </param>
        /// <param name="out_pos">
        /// The next byte will be written to out[*out_pos].
        /// *out_pos is updated only if decoding succeeds.
        /// </param>
        /// <param name="out_size">
        /// Size of the out buffer; the first byte into
        /// which no data is written to is out[out_size].
        /// </param>
        /// <returns>
        /// - LZMA_OK: Decoding was successful.
        /// - LZMA_FORMAT_ERROR
        /// - LZMA_OPTIONS_ERROR
        /// - LZMA_DATA_ERROR
        /// - LZMA_NO_CHECK: This can be returned only if using
        ///   the LZMA_TELL_NO_CHECK flag.
        /// - LZMA_UNSUPPORTED_CHECK: This can be returned only if using
        ///   the LZMA_TELL_UNSUPPORTED_CHECK flag.
        /// - LZMA_MEM_ERROR
        /// - LZMA_MEMLIMIT_ERROR: Memory usage limit was reached.
        ///   The minimum required memlimit value was stored to *memlimit.
        /// - LZMA_BUF_ERROR: Output buffer was too small.
        /// - LZMA_PROG_ERROR
        /// </returns>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate LzmaRet lzma_stream_buffer_decode(
            uint memlimit,
            LzmaDecodingFlag flags,
            IntPtr allocator,
            byte[] in_buf,
            ref UIntPtr in_pos,
            UIntPtr in_size, // size_t
            byte[] out_buf,
            ref UIntPtr out_pos, // size_t
            UIntPtr out_size); // size_t
        internal static lzma_stream_buffer_decode LzmaStreamBufferDecode;
        #endregion

        #region Version - LzmaVersionNumber, LzmaVersionString
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate uint lzma_version_number();
        internal static lzma_version_number LzmaVersionNumber;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.LPStr)]
        internal delegate string lzma_version_string();
        internal static lzma_version_string LzmaVersionString;
        #endregion
        #endregion
    }
    #endregion
}
