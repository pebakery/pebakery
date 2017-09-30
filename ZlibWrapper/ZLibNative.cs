/*
 * Forked from zlibnet v1.3.3
 * https://zlibnet.codeplex.com/
 * 
 * Licensed under zlib license.
 * 
 * This software is provided 'as-is', without any express or implied
 * warranty.  In no event will the authors be held liable for any damages
 * arising from the use of this software.
 *
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 *
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would be
 *    appreciated but is not required.
 * 2. Altered source versions must be plainly marked as such, and must not be
 *    misrepresented as being the original software.
 * 3. This notice may not be removed or altered from any source distribution.
 */

using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.IO;
using Microsoft.Win32.SafeHandles;
using System.Runtime.ConstrainedExecution;
using System.Security.Permissions;
using System.ComponentModel;

namespace ZLibWrapper
{
    #region SafeLibraryHandle
    [SecurityPermission(SecurityAction.InheritanceDemand, UnmanagedCode = true)]
    [SecurityPermission(SecurityAction.Demand, UnmanagedCode = true)]
    public class SafeLibraryHandle : SafeHandleZeroOrMinusOneIsInvalid, IDisposable
    {
        #region Managed Methods
        public SafeLibraryHandle() : base(true) { }

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        protected override bool ReleaseHandle()
        {
            return FreeLibrary(this.handle);
        }
        #endregion

        #region Windows API
        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool FreeLibrary(IntPtr hModule);
        #endregion
    }
    #endregion

    #region ZLibNative
    public static class ZLibNative
    {
        internal const int DEF_MEM_LEVEL = 8;
        internal const int Z_DEFLATED = 8; 
        internal const string ZLIB_VERSION = "1.2.11"; // This code is based on zlib 1.2.11's zlib.h

        internal static SafeLibraryHandle hModule = null;
        public static bool Loaded => (hModule != null);

        // Does ZLibNative using .Net Framework's clrcompression.dll, or user provided zlibwapi.dll?
        public static bool ZLibProvided { get; private set; }

        #region Windows API
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern SafeLibraryHandle LoadLibrary([MarshalAs(UnmanagedType.LPTStr)] string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        internal static extern IntPtr GetProcAddress(SafeLibraryHandle hModule, [MarshalAs(UnmanagedType.LPStr)] string procName);
        #endregion

        #region AssemblyInit, AssemblyCleanup
        public static SafeLibraryHandle AssemblyInit(string dllPath = null)
        {
            if (hModule == null)
            {
                if (dllPath == null)
                { // Use .Net Framework's clrcompression instead
                    string fxDir = RuntimeEnvironment.GetRuntimeDirectory();
                    dllPath = Path.Combine(fxDir, "clrcompression.dll");
                    ZLibProvided = false;
                }
                else if (!File.Exists(dllPath))
                { // Check 
                    throw new ArgumentException("Specified dll does not exist");
                }
                else
                {
                    ZLibProvided = true;
                }

                hModule = LoadLibrary(dllPath);
                if (hModule.IsInvalid)
                    throw new ArgumentException($"Unable to load [{dllPath}]", new Win32Exception());

                // Check if dll is valid zlibwapi.dll
                if (GetProcAddress(hModule, "zlibCompileFlags") == IntPtr.Zero)
                {
                    AssemblyCleanup();
                    throw new ArgumentException($"[{dllPath}] is not valid zlibwapi.dll");
                }

                try
                {
                    LoadFuntions(hModule);
                }
                catch (Exception e)
                {
                    AssemblyCleanup();
                    throw e;
                }
            }

            return hModule;
        }

        public static void AssemblyCleanup()
        {
            if (hModule != null)
            {
                hModule.Close();
                hModule = null;
            }
        }

        private static void LoadFuntions(SafeLibraryHandle hModule)
        {
            IntPtr funcPtr = IntPtr.Zero;

            // deflateInit2_
            funcPtr = GetProcAddress(hModule, "deflateInit2_");
            if (funcPtr == null || funcPtr == IntPtr.Zero)
                throw new ArgumentException("Cannot import deflateInit2_", new Win32Exception());
            deflateInit2_ = (deflateInit2_Delegate)Marshal.GetDelegateForFunctionPointer(funcPtr, typeof(deflateInit2_Delegate));

            // deflate
            funcPtr = GetProcAddress(hModule, "deflate");
            if (funcPtr == null || funcPtr == IntPtr.Zero)
                throw new ArgumentException("Cannot import deflate", new Win32Exception());
            Deflate = (deflateDelegate)Marshal.GetDelegateForFunctionPointer(funcPtr, typeof(deflateDelegate));

            // deflateEnd
            funcPtr = GetProcAddress(hModule, "deflateEnd");
            if (funcPtr == null || funcPtr == IntPtr.Zero)
                throw new ArgumentException("Cannot import deflateEnd", new Win32Exception());
            DeflateEnd = (deflateEndDelegate)Marshal.GetDelegateForFunctionPointer(funcPtr, typeof(deflateEndDelegate));

            // inflateInit2_
            funcPtr = GetProcAddress(hModule, "inflateInit2_");
            if (funcPtr == null || funcPtr == IntPtr.Zero)
                throw new ArgumentException("Cannot import inflateInit2_", new Win32Exception());
            inflateInit2_ = (inflateInit2_Delegate)Marshal.GetDelegateForFunctionPointer(funcPtr, typeof(inflateInit2_Delegate));

            // inflate
            funcPtr = GetProcAddress(hModule, "inflate");
            if (funcPtr == null || funcPtr == IntPtr.Zero)
                throw new ArgumentException("Cannot import inflate", new Win32Exception());
            Inflate = (inflateDelegate)Marshal.GetDelegateForFunctionPointer(funcPtr, typeof(inflateDelegate));

            // inflateEnd
            funcPtr = GetProcAddress(hModule, "inflateEnd");
            if (funcPtr == null || funcPtr == IntPtr.Zero)
                throw new ArgumentException("Cannot import inflateEnd", new Win32Exception());
            InflateEnd = (inflateEndDelegate)Marshal.GetDelegateForFunctionPointer(funcPtr, typeof(inflateEndDelegate));

            // Only if user provided zlibwapi.dll
            if (ZLibProvided)
            {
                // adler32
                funcPtr = GetProcAddress(hModule, "adler32");
                if (funcPtr == null || funcPtr == IntPtr.Zero)
                    throw new ArgumentException("Cannot import adler32", new Win32Exception()); 
                Adler32 = (adler32Delegate)Marshal.GetDelegateForFunctionPointer(funcPtr, typeof(adler32Delegate));

                // crc32
                funcPtr = GetProcAddress(hModule, "crc32");
                if (funcPtr == null || funcPtr == IntPtr.Zero)
                    throw new ArgumentException("Cannot import crc32", new Win32Exception());
                Crc32 = (crc32Delegate)Marshal.GetDelegateForFunctionPointer(funcPtr, typeof(crc32Delegate));
            }
        }
        #endregion

        #region zlib Functions Delegates
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate int deflateInit2_Delegate(
            ref ZStream strm,
            int level,
            int method,
            int windowBits,
            int memLevel,
            int strategy,
            [MarshalAs(UnmanagedType.LPStr)] string version,
            int stream_size);
        private static deflateInit2_Delegate deflateInit2_;

        internal static int DeflateInit(ref ZStream strm, CompressionLevel level, ZLibWriteType windowBits)
        {
            return deflateInit2_(ref strm, (int)level, Z_DEFLATED, (int)windowBits, DEF_MEM_LEVEL,
                    (int)ZLibCompressionStrategy.Z_DEFAULT_STRATEGY, ZLibNative.ZLIB_VERSION, Marshal.SizeOf(typeof(ZStream)));
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate int deflateDelegate(
            ref ZStream strm,
            ZLibFlush flush);
        internal static deflateDelegate Deflate;

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate int deflateEndDelegate(
            ref ZStream strm);
        internal static deflateEndDelegate DeflateEnd;

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate int inflateInit2_Delegate(
            ref ZStream strm,
            int windowBits,
            [MarshalAs(UnmanagedType.LPStr)] string version,
            int stream_size);
        private static inflateInit2_Delegate inflateInit2_;

        internal static int InflateInit(ref ZStream strm, ZLibOpenType windowBits)
        {
            return inflateInit2_(ref strm, (int)windowBits, ZLibNative.ZLIB_VERSION, Marshal.SizeOf(typeof(ZStream)));
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate int inflateDelegate(
            ref ZStream strm,
            ZLibFlush flush);
        internal static inflateDelegate Inflate;

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate int inflateEndDelegate(
            ref ZStream strm);
        internal static inflateEndDelegate InflateEnd;

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate uint adler32Delegate(
            uint crc,
            IntPtr buf,
            uint len);
        internal static adler32Delegate Adler32;

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate uint crc32Delegate(
            uint crc,
            IntPtr buf,
            uint len);
        internal static crc32Delegate Crc32;
        #endregion
    }
    #endregion

    #region zlib Enums
    internal enum ZLibFlush : int
	{
		Z_NO_FLUSH = 0,
		Z_PARTIAL_FLUSH = 1,
		Z_SYNC_FLUSH = 2,
		Z_FULL_FLUSH = 3,
		Z_FINISH = 4,
        Z_BLOCK = 5,
        Z_TREES = 6,
	}

    internal enum ZLibCompressionStrategy : int
	{
		Z_FLITERED = 1,
		Z_HUFFMAN_ONLY = 2,
        Z_RLE = 3,
        Z_FIXED = 4,
        Z_DEFAULT_STRATEGY = 0,
	}

    internal enum ZLibDataType : int
	{
		Z_BINARY = 0,
		Z_ASCII = 1,
        Z_TEXT = 1,
		Z_UNKNOWN = 2,
	}

	public enum ZLibOpenType : int
	{
		//If a compressed stream with a larger window
		//size is given as input, inflate() will return with the error code
		//Z_DATA_ERROR instead of trying to allocate a larger window.
		Deflate = -15, // -8..-15
		ZLib = 15, // 8..15, 0 = use the window size in the zlib header of the compressed stream.
		GZip = 15 + 16,
		Both_ZLib_GZip = 15 + 32,
	}

	public enum ZLibWriteType : int // == WindowBits
	{
		//If a compressed stream with a larger window
		//size is given as input, inflate() will return with the error code
		//Z_DATA_ERROR instead of trying to allocate a larger window.
		Deflate = -15, // -8..-15
		ZLib = 15, // 8..15, 0 = use the window size in the zlib header of the compressed stream.
		GZip = 15 + 16,
		//		Both = 15 + 32,
	}

    /// <summary>Type of compression to use for the GZipStream. Currently only Decompress is supported.</summary>
	public enum CompressionMode
    {
        /// <summary>Compresses the underlying stream.</summary>
        Compress,
        /// <summary>Decompresses the underlying stream.</summary>
        Decompress,
    }

    public enum CompressionLevel : int
	{
		NoCompression = 0,
		Fastest = 1,
		Best = 9,
		Default = 6,
		Level0 = 0,
		Level1 = 1,
		Level2 = 2,
		Level3 = 3,
		Level4 = 4,
		Level5 = 5,
		Level6 = 6,
		Level7 = 7,
		Level8 = 8,
		Level9 = 9,
	}
    #endregion

    #region z_stream
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
	internal struct ZStream
	{
        internal void Init()
        {
            next_in = IntPtr.Zero;
            next_out = IntPtr.Zero;
            state = IntPtr.Zero;

            zalloc = IntPtr.Zero;
            zfree = IntPtr.Zero;
            opaque = IntPtr.Zero;
        }

        public IntPtr next_in;  // next input byte 
		public uint avail_in;  // number of bytes available at next_in
        public uint total_in;  // total number of input bytes read so far

        public IntPtr next_out; /* next output byte should be put there */
		public uint avail_out; /* remaining free space at next_out */
		public uint total_out; /* total nb of bytes output so far */

        private IntPtr msg;      /* last error message, NULL if no error */
        private IntPtr state; /* not visible by applications */

		private IntPtr zalloc;  /* used to allocate the internal state */
		private IntPtr zfree;   /* used to free the internal state */
		private IntPtr opaque;  /* private data object passed to zalloc and zfree */

		public int data_type;  /* best guess about the data type: ascii or binary */
		public uint adler;      /* adler32 value of the uncompressed data */
		public uint reserved;   /* reserved for future use */

        public string LastErrorMsg
        {
            get => Marshal.PtrToStringAnsi(msg);
        }
    }
    #endregion

    #region zlib Return Code
    internal static class ZLibReturnCode
	{
		public const int Ok = 0;
		public const int StreamEnd = 1; //positive = no error
		public const int NeedDictionary = 2; //positive = no error?
		public const int Errno = -1;
		public const int StreamError = -2;
		public const int DataError = -3; //CRC
		public const int MemoryError = -4;
		public const int BufferError = -5;
		public const int VersionError = -6;

		public static string GetMesage(int retCode)
		{
			switch (retCode)
			{
				case ZLibReturnCode.Ok:
					return "No error";
				case ZLibReturnCode.StreamEnd:
					return "End of stream reached";
				case ZLibReturnCode.NeedDictionary:
					return "A preset dictionary is needed";
				case ZLibReturnCode.Errno: // Consult error code
					return $"Unknown error {Marshal.GetLastWin32Error()}";
				case ZLibReturnCode.StreamError:
					return "Stream error";
				case ZLibReturnCode.DataError:
					return "Data was corrupted";
				case ZLibReturnCode.MemoryError:
					return "Out of memory";
				case ZLibReturnCode.BufferError:
					return "Not enough room in provided buffer";
				case ZLibReturnCode.VersionError:
					return "Incompatible zlib library version";
				default:
					return "Unknown error";
			}
		}
	}
    #endregion

    #region ZLibException
    [Serializable]
	public class ZLibException : ApplicationException
	{
		public ZLibException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
		}

		public ZLibException(int errorCode)
			: base(GetMsg(errorCode, null))
		{

		}

		public ZLibException(int errorCode, string lastStreamError)
			: base(GetMsg(errorCode, lastStreamError))
		{
		}

		private static string GetMsg(int errorCode, string lastStreamError)
		{
			string msg = "ZLib error " + errorCode + ": " + ZLibReturnCode.GetMesage(errorCode);
			if (lastStreamError != null && lastStreamError.Length > 0)
				msg += " (" + lastStreamError + ")";
			return msg;
		}
	}
    #endregion

    #region PinnedArray
    internal class PinnedArray : IDisposable
    {
        internal GCHandle hBuffer;
        internal Array buffer;

        public IntPtr this[int idx] => Marshal.UnsafeAddrOfPinnedArrayElement(buffer, idx);
        public static implicit operator IntPtr(PinnedArray fixedArray) => fixedArray[0];

        public PinnedArray(Array buffer)
        {
            this.buffer = buffer;
            hBuffer = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        }

        ~PinnedArray()
        {
            hBuffer.Free();
        }

        public void Dispose()
        {
            hBuffer.Free();
            GC.SuppressFinalize(this);
        }
    }
    #endregion
}
