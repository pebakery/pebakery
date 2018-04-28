/*
    Derived from LZ4 header files (BSD 2-Clause)
    Copyright (c) 2011-2016, Yann Collet

    C# Wrapper written by Hajin Jang
    Copyright (C) 2018 Hajin Jang

    Redistribution and use in source and binary forms, with or without modification,
    are permitted provided that the following conditions are met:

    * Redistributions of source code must retain the above copyright notice, this
      list of conditions and the following disclaimer.

    * Redistributions in binary form must reproduce the above copyright notice, this
      list of conditions and the following disclaimer in the documentation and/or
      other materials provided with the distribution.

    THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
    ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
    WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
    DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR
    ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
    (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
    LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON
    ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
    (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
    SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
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

namespace PEBakery.LZ4Lib
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
        public const string MsgInitFirstError = "Please call LZ4Stream.GlobalInit() first!";
        public const string MsgAlreadyInited = "PEBakery.LZ4Lib is already initialized.";
        #endregion

        #region Fields
        public static SafeLibraryHandle hModule = null;
        public static bool Loaded => hModule != null;
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
            #region Version - LzmaVersionNumber, LzmaVersionString
            VersionNumber = (LZ4_versionNumber)GetFuncPtr("LZ4_versionNumber", typeof(LZ4_versionNumber));
            VersionString = (LZ4_versionString)GetFuncPtr("LZ4_versionString", typeof(LZ4_versionString));
            GetFrameVersion = (LZ4F_getVersion) GetFuncPtr("LZ4F_getVersion", typeof(LZ4F_getVersion));
            #endregion

            #region Error - IsError, GetErrorName
            FrameIsError = (LZ4F_isError)GetFuncPtr("LZ4F_isError", typeof(LZ4F_isError));
            GetErrorName = (LZ4F_getErrorName)GetFuncPtr("LZ4F_getErrorName", typeof(LZ4F_getErrorName));
            #endregion

            #region FrameCompression
            CreateFrameCompressionContext = (LZ4F_createCompressionContext)GetFuncPtr("LZ4F_createCompressionContext", typeof(LZ4F_createCompressionContext));
            FreeFrameCompressionContext = (LZ4F_freeCompressionContext)GetFuncPtr("LZ4F_freeCompressionContext", typeof(LZ4F_freeCompressionContext));         
            FrameCompressionBegin = (LZ4F_compressBegin) GetFuncPtr("LZ4F_compressBegin", typeof(LZ4F_compressBegin));
            FrameCompressionBound = (LZ4F_compressBound)GetFuncPtr("LZ4F_compressBound", typeof(LZ4F_compressBound));
            FrameCompressionUpdate = (LZ4F_compressUpdate)GetFuncPtr("LZ4F_compressUpdate", typeof(LZ4F_compressUpdate));
            FrameFlush = (LZ4F_flush)GetFuncPtr("LZ4F_flush", typeof(LZ4F_flush));
            FrameCompressionEnd = (LZ4F_compressEnd)GetFuncPtr("LZ4F_compressEnd", typeof(LZ4F_compressEnd));
            #endregion

            #region FrameDecompression
            CreateFrameDecompressionContext = (LZ4F_createDecompressionContext)GetFuncPtr("LZ4F_createDecompressionContext", typeof(LZ4F_createDecompressionContext));
            FreeFrameDecompressionContext = (LZ4F_freeDecompressionContext)GetFuncPtr("LZ4F_freeDecompressionContext", typeof(LZ4F_freeDecompressionContext));
            GetFrameInfo = (LZ4F_getFrameInfo)GetFuncPtr("LZ4F_getFrameInfo", typeof(LZ4F_getFrameInfo));
            FrameDecompress = (LZ4F_decompress)GetFuncPtr("LZ4F_decompress", typeof(LZ4F_decompress));
            ResetDecompressionContext = (LZ4F_resetDecompressionContext)GetFuncPtr("LZ4F_resetDecompressionContext", typeof(LZ4F_resetDecompressionContext));
            #endregion
        }

        internal static void ResetFuntions()
        {
            #region Version - LZ4VersionNumber, LZ4VersionString
            VersionNumber = null;
            VersionString = null;
            #endregion

            #region Error - IsError, GetErrorName
            FrameIsError = null;
            GetErrorName = null;
            #endregion

            #region FrameCompression
            CreateFrameCompressionContext = null;
            FreeFrameCompressionContext = null;            
            FrameCompressionBegin = null;
            FrameCompressionBound = null;
            FrameCompressionUpdate = null;
            FrameFlush = null;
            FrameCompressionEnd = null;
            #endregion

            #region FrameDecompression
            CreateFrameDecompressionContext = null;
            FreeFrameDecompressionContext = null;
            GetFrameInfo = null;
            FrameDecompress = null;
            ResetDecompressionContext = null;
            #endregion
        }
        #endregion

        #region Windows API
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern SafeLibraryHandle LoadLibrary([MarshalAs(UnmanagedType.LPWStr)] string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        internal static extern IntPtr GetProcAddress(SafeLibraryHandle hModule, [MarshalAs(UnmanagedType.LPStr)] string procName);
        #endregion

        #region liblz4 Function Pointer
        #region Version - VersionNumber, VersionString
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate uint LZ4_versionNumber();
        internal static LZ4_versionNumber VersionNumber;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.LPStr)]
        internal delegate string LZ4_versionString();
        internal static LZ4_versionString VersionString;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate uint LZ4F_getVersion();
        internal static LZ4F_getVersion GetFrameVersion;
        #endregion

        #region Error - IsError, GetErrorName
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate uint LZ4F_isError(UIntPtr code); // size_t
        internal static LZ4F_isError FrameIsError;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate IntPtr LZ4F_getErrorName(UIntPtr code); // size_t
        internal static LZ4F_getErrorName GetErrorName;
        #endregion

        #region FrameCompression
        /// <summary>
        /// The first thing to do is to create a compressionContext object, which will be used in all compression operations.
        /// This is achieved using LZ4F_createCompressionContext(), which takes as argument a version.
        /// The version provided MUST be LZ4F_VERSION. It is intended to track potential version mismatch, notably when using DLL.
        /// The function will provide a pointer to a fully allocated LZ4F_cctx object.
        /// </summary>
        /// <returns>
        /// If @return != zero, there was an error during context creation.
        /// Object can release its memory using LZ4F_freeCompressionContext();
        /// </returns>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate UIntPtr LZ4F_createCompressionContext(
            ref IntPtr cctxPtr,
            uint version);
        internal static LZ4F_createCompressionContext CreateFrameCompressionContext;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate UIntPtr LZ4F_freeCompressionContext(IntPtr cctx);
        internal static LZ4F_freeCompressionContext FreeFrameCompressionContext;

        /// <summary>
        ///  will write the frame header into dstBuffer.
        ///  dstCapacity must be >= LZ4F_HEADER_SIZE_MAX bytes.
        /// `prefsPtr` is optional : you can provide NULL as argument, all preferences will then be set to default.
        /// </summary>
        /// <returns>
        /// number of bytes written into dstBuffer for the header
        /// or an error code (which can be tested using LZ4F_isError())
        /// </returns>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate UIntPtr LZ4F_compressBegin(
            IntPtr cctx,
            IntPtr dstBuffer,
            UIntPtr dstCapacity, // size_t
            FramePreferences prefsPtr);
        internal static LZ4F_compressBegin FrameCompressionBegin;

        /// <summary>
        ///  Provides minimum dstCapacity for a given srcSize to guarantee operation success in worst case situations.
        ///  prefsPtr is optional : when NULL is provided, preferences will be set to cover worst case scenario.
        ///  Result is always the same for a srcSize and prefsPtr, so it can be trusted to size reusable buffers.
        ///  When srcSize==0, LZ4F_compressBound() provides an upper bound for LZ4F_flush() and LZ4F_compressEnd() operations.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate UIntPtr LZ4F_compressBound(
            UIntPtr srcSize, // size_t
            FramePreferences prefsPtr);
        internal static LZ4F_compressBound FrameCompressionBound;

        /// <summary>
        ///  When data must be generated and sent immediately, without waiting for a block to be completely filled,
        ///  it's possible to call LZ4_flush(). It will immediately compress any data buffered within cctx.
        /// `dstCapacity` must be large enough to ensure the operation will be successful.
        /// `cOptPtr` is optional : it's possible to provide NULL, all options will be set to default.
        /// </summary>
        /// <return>
        /// number of bytes written into dstBuffer (it can be zero, which means there was no data stored within cctx)
        /// or an error code if it fails (which can be tested using LZ4F_isError())
        /// </return>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate UIntPtr LZ4F_compressUpdate(
            IntPtr cctx,
            IntPtr dstBuffer,
            UIntPtr dstCapacity, // size_t
            IntPtr srcBuffer,
            UIntPtr srcSize, // size_t
            FrameCompressOptions cOptPtr);
        internal static LZ4F_compressUpdate FrameCompressionUpdate;

        /// <summary>
        ///  When data must be generated and sent immediately, without waiting for a block to be completely filled,
        ///  it's possible to call LZ4_flush(). It will immediately compress any data buffered within cctx.
        /// `dstCapacity` must be large enough to ensure the operation will be successful.
        /// `cOptPtr` is optional : it's possible to provide NULL, all options will be set to default.
        /// </summary>
        /// <return>
        /// number of bytes written into dstBuffer (it can be zero, which means there was no data stored within cctx)
        /// or an error code if it fails (which can be tested using LZ4F_isError())
        /// </return>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate UIntPtr LZ4F_flush(
            IntPtr cctx,
            IntPtr dstBuffer,
            UIntPtr dstCapacity, // size_t
            FrameCompressOptions cOptPtr);
        internal static LZ4F_flush FrameFlush;

        /// <summary>
        ///  To properly finish an LZ4 frame, invoke LZ4F_compressEnd().
        ///  It will flush whatever data remained within `cctx` (like LZ4_flush())
        ///  and properly finalize the frame, with an endMark and a checksum.
        /// `cOptPtr` is optional : NULL can be provided, in which case all options will be set to default.
        /// </summary>
        /// /// <return>
        /// number of bytes written into dstBuffer (necessarily >= 4 (endMark), or 8 if optional frame checksum is enabled)
        /// or an error code if it fails (which can be tested using LZ4F_isError())
        /// A successful call to LZ4F_compressEnd() makes `cctx` available again for another compression task.
        /// </return>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate UIntPtr LZ4F_compressEnd(
            IntPtr cctx,
            IntPtr dstBuffer,
            UIntPtr dstCapacity, // size_t
            FrameCompressOptions cOptPtr);
        internal static LZ4F_compressEnd FrameCompressionEnd;
        #endregion

        #region FrameDecompression
        /// <summary>
        ///  Create an LZ4F_dctx object, to track all decompression operations.
        ///  The version provided MUST be LZ4F_VERSION.
        ///  The function provides a pointer to an allocated and initialized LZ4F_dctx object.
        ///  The result is an errorCode, which can be tested using LZ4F_isError().
        ///  dctx memory can be released using LZ4F_freeDecompressionContext();
        /// </summary>
        /// <returns>
        /// The result of LZ4F_freeDecompressionContext() is indicative of the current state of decompressionContext when being released.
        /// That is, it should be == 0 if decompression has been completed fully and correctly.
        /// </returns>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate UIntPtr LZ4F_createDecompressionContext(
            ref IntPtr cctxPtr,
            uint version);
        internal static LZ4F_createDecompressionContext CreateFrameDecompressionContext;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate UIntPtr LZ4F_freeDecompressionContext(IntPtr dctx);
        internal static LZ4F_freeDecompressionContext FreeFrameDecompressionContext;

        /// <summary>
        ///  This function extracts frame parameters (max blockSize, dictID, etc.).
        /// </summary>
        /// <remarks>
        ///  Its usage is optional.
        ///  Extracted information is typically useful for allocation and dictionary.
        ///  This function works in 2 situations :
        ///   - At the beginning of a new frame, in which case
        ///     it will decode information from `srcBuffer`, starting the decoding process.
        ///     Input size must be large enough to successfully decode the entire frame header.
        ///     Frame header size is variable, but is guaranteed to be &lt;= LZ4F_HEADER_SIZE_MAX bytes.
        ///     It's allowed to provide more input data than this minimum.
        ///   - After decoding has been started.
        ///     In which case, no input is read, frame parameters are extracted from dctx.
        ///   - If decoding has barely started, but not yet extracted information from header,
        ///     LZ4F_getFrameInfo() will fail.
        ///  The number of bytes consumed from srcBuffer will be updated within *srcSizePtr (necessarily &lt;= original value).
        ///  Decompression must resume from (srcBuffer + *srcSizePtr).
        /// </remarks>
        /// <returns>
        /// an hint about how many srcSize bytes LZ4F_decompress() expects for next call,
        ///           or an error code which can be tested using LZ4F_isError().
        ///  note 1 : in case of error, dctx is not modified. Decoding operation can resume from beginning safely.
        ///  note 2 : frame parameters are *copied into* an already allocated LZ4F_frameInfo_t structure.
        /// </returns>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate UIntPtr LZ4F_getFrameInfo(
            IntPtr dctx,
            FrameInfo frameInfoPtr,
            IntPtr srcCapacity, 
            UIntPtr srcSizePtr); // size_t
        internal static LZ4F_getFrameInfo GetFrameInfo;

        /// <summary>
        ///  Call this function repetitively to regenerate compressed data from `srcBuffer`.
        ///  The function will read up to *srcSizePtr bytes from srcBuffer,
        ///  and decompress data into dstBuffer, of capacity *dstSizePtr.
        ///
        ///  The number of bytes consumed from srcBuffer will be written into *srcSizePtr (necessarily &lt;= original value).
        ///  The number of bytes decompressed into dstBuffer will be written into *dstSizePtr (necessarily &lt;= original value).
        ///
        ///  The function does not necessarily read all input bytes, so always check value in *srcSizePtr.
        ///  Unconsumed source data must be presented again in subsequent invocations.
        ///
        /// `dstBuffer` can freely change between each consecutive function invocation.
        /// `dstBuffer` content will be overwritten.
        /// </summary>
        /// <returns>
        /// an hint of how many `srcSize` bytes LZ4F_decompress() expects for next call.
        ///  Schematically, it's the size of the current (or remaining) compressed block + header of next block.
        ///  Respecting the hint provides some small speed benefit, because it skips intermediate buffers.
        ///  This is just a hint though, it's always possible to provide any srcSize.
        ///
        ///  When a frame is fully decoded, @return will be 0 (no more data expected).
        ///  When provided with more bytes than necessary to decode a frame,
        ///  LZ4F_decompress() will stop reading exactly at end of current frame, and @return 0.
        ///
        ///  If decompression failed, @return is an error code, which can be tested using LZ4F_isError().
        ///  After a decompression error, the `dctx` context is not resumable.
        ///  Use LZ4F_resetDecompressionContext() to return to clean state.
        ///
        ///  After a frame is fully decoded, dctx can be used again to decompress another frame.
        /// </returns>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate UIntPtr LZ4F_decompress(
            IntPtr dctx,
            IntPtr dstBuffer,
            ref UIntPtr dstSizePtr, // size_t
            IntPtr srcBuffer,
            ref UIntPtr srcSizePtr, // size_t
            FrameDecompressOptions dOptPtr);
        internal static LZ4F_decompress FrameDecompress;

        /// <summary>
        /// In case of an error, the context is left in "undefined" state.
        /// In which case, it's necessary to reset it, before re-using it.
        /// This method can also be used to abruptly stop any unfinished decompression,
        /// and start a new one using same context resources.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void LZ4F_resetDecompressionContext(IntPtr dctx);
        internal static LZ4F_resetDecompressionContext ResetDecompressionContext;
        #endregion

        #endregion
    }
    #endregion
}
