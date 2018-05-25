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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PEBakery.LZ4Lib
{
    // ReSharper disable once InconsistentNaming
    public class LZ4FrameStream : Stream
    {
        #region Fields and Properties
        // Field
        private Stream _baseStream;
        private readonly LZ4Mode _mode;
        private readonly bool _leaveOpen;
        private bool _disposed = false;

        private IntPtr _cctx = IntPtr.Zero;
        private IntPtr _dctx = IntPtr.Zero;

        private readonly byte[] _workBuf;

        // Compression
        private const int SrcBufSizeMax = 4 * 1024 * 1024; // 4MB
        private readonly uint _destBufSize; // 4MB

        // Decompression
        private const int DecompDone = -1;
        private bool _firstRead = true;
        private int _decompSrcIdx = 0;
        private int _decompSrcCount = 0;

        // Property
        public Stream BaseStream => _baseStream;

        public long TotalIn { get; private set; } = 0;
        public long TotalOut { get; private set; } = 0;

        // Const
        // https://github.com/lz4/lz4/blob/master/doc/lz4_Frame_format.md
        private static uint FrameVersion;
        private readonly byte[] FrameMagicNumber = new byte[4] { 0x04, 0x22, 0x4D, 0x18 }; // 0x184D2204 (LE)
        #endregion

        #region Constructor
        public LZ4FrameStream(Stream stream, LZ4Mode mode)
            : this(stream, mode, 0, false) { }

        public LZ4FrameStream(Stream stream, LZ4Mode mode, LZ4CompLevel compressionLevel)
            : this(stream, mode, compressionLevel, false) { }

        public LZ4FrameStream(Stream stream, LZ4Mode mode, bool leaveOpen)
            : this(stream, mode, 0, leaveOpen) { }

        public LZ4FrameStream(Stream stream, LZ4Mode mode, LZ4CompLevel compressionLevel, bool leaveOpen)
        {
            if (!NativeMethods.Loaded)
                throw new InvalidOperationException(NativeMethods.MsgInitFirstError);

            _baseStream = stream ?? throw new ArgumentNullException(nameof(stream));
            _mode = mode;
            _leaveOpen = leaveOpen;
            _disposed = false;
            
            switch (mode)
            {
                case LZ4Mode.Compress:
                {
                    UIntPtr ret = NativeMethods.CreateFrameCompressionContext(ref _cctx, FrameVersion);
                    LZ4FrameException.CheckLZ4Error(ret);

                    FramePreferences prefs = new FramePreferences
                    {
                        // Use default value of lz4 cli
                        FrameInfo = new FrameInfo
                        {
                            BlockSizeId = FrameBlockSizeId.Max4MB,
                            BlockMode = FrameBlockMode.BlockLinked,
                            ContentChecksumFlag = FrameContentChecksum.ContentChecksumEnabled,
                            FrameType = FrameType.Frame,
                            ContentSize = 0,
                            DictId = 0,
                            BlockChecksumFlag = FrameBlockChecksum.NoBlockChecksum,
                        },
                        CompressionLevel = (int)compressionLevel,
                        AutoFlush = 1,
                    };

                    UIntPtr frameSizeVal = NativeMethods.FrameCompressionBound((UIntPtr) SrcBufSizeMax, prefs);
                    Debug.Assert(frameSizeVal.ToUInt64() <= int.MaxValue);

                    uint frameSize = frameSizeVal.ToUInt32();
                    if (SrcBufSizeMax < frameSize)
                        _destBufSize = frameSize;

                    _workBuf = new byte[_destBufSize];
                    using (PinnedArray dstBufPin = new PinnedArray(_workBuf))
                    {
                        UIntPtr headerSizeVal = NativeMethods.FrameCompressionBegin(_cctx, dstBufPin, (UIntPtr)SrcBufSizeMax, prefs);
                        LZ4FrameException.CheckLZ4Error(headerSizeVal);

                        Debug.Assert(headerSizeVal.ToUInt64() < int.MaxValue);

                        int headerSize = (int)headerSizeVal.ToUInt32();
                        _baseStream.Write(_workBuf, 0, headerSize);
                        TotalOut += headerSize;
                    }
                    break;
                }
                case LZ4Mode.Decompress:
                {
                    UIntPtr ret = NativeMethods.CreateFrameDecompressionContext(ref _dctx, FrameVersion);
                    LZ4FrameException.CheckLZ4Error(ret);
                   
                    byte[] headerBuf = new byte[4];
                    int readHeaderSize = _baseStream.Read(headerBuf, 0, 4);
                    TotalIn += 4;

                    if (readHeaderSize != 4 || !headerBuf.SequenceEqual(FrameMagicNumber))
                    throw new InvalidDataException("BaseStream is not a valid LZ4 Frame Format");

                    _workBuf = new byte[SrcBufSizeMax];

                    break;
                }    
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode));
            }
        }
        #endregion

        #region Disposable Pattern
        ~LZ4FrameStream()
        {
            Dispose(false);
            GC.SuppressFinalize(this);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                if (_cctx != IntPtr.Zero)
                { // Compress
                    FinishWrite();

                    UIntPtr ret = NativeMethods.FreeFrameCompressionContext(_cctx);
                    LZ4FrameException.CheckLZ4Error(ret);

                    _cctx = IntPtr.Zero;
                }

                if (_dctx != IntPtr.Zero)
                {
                    UIntPtr ret = NativeMethods.FreeFrameDecompressionContext(_dctx);
                    LZ4FrameException.CheckLZ4Error(ret);

                    _dctx = IntPtr.Zero;
                    
                }

                if (_baseStream != null)
                {
                    Flush();
                    if (!_leaveOpen)
                        _baseStream.Dispose();
                    _baseStream = null;
                }
                
                _disposed = true;
            }
        }

        public override void Close()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        #region Global - (Static) GlobalInit, GlobalCleanup
        public static void GlobalInit(string dllPath)
        {
            if (NativeMethods.Loaded)
                throw new InvalidOperationException(NativeMethods.MsgAlreadyInited);

            if (dllPath == null)
                throw new ArgumentNullException(nameof(dllPath));
            if (!File.Exists(dllPath))
                throw new FileNotFoundException("Specified dll does not exist");

            NativeMethods.hModule = NativeMethods.LoadLibrary(dllPath);
            if (NativeMethods.hModule.IsInvalid)
                throw new ArgumentException($"Unable to load [{dllPath}]", new Win32Exception());

            // Check if dll is valid (liblz4.so.1.8.1.dll)
            if (NativeMethods.GetProcAddress(NativeMethods.hModule, "LZ4F_getVersion") == IntPtr.Zero)
            {
                GlobalCleanup();
                throw new ArgumentException($"[{dllPath}] is not a valid LZ4 library");
            }

            try
            {
                NativeMethods.LoadFuntions();

                FrameVersion = NativeMethods.GetFrameVersion();
            }
            catch (Exception)
            {
                GlobalCleanup();
                throw;
            }
        }

        public static void GlobalCleanup()
        {
            if (!NativeMethods.Loaded)
                throw new InvalidOperationException(NativeMethods.MsgInitFirstError);

            NativeMethods.ResetFuntions();

            NativeMethods.hModule.Close();
            NativeMethods.hModule = null;
        }
        #endregion

        #region Version - (Static)
        public static Version LibraryVersion()
        {
            if (!NativeMethods.Loaded)
                throw new InvalidOperationException(NativeMethods.MsgInitFirstError);

            /*
                Definition from "lz4.h"

                #define LZ4_VERSION_MAJOR    1 
                #define LZ4_VERSION_MINOR    8 
                #define LZ4_VERSION_RELEASE  1 

                #define LZ4_VERSION_NUMBER (LZ4_VERSION_MAJOR *100*100 + LZ4_VERSION_MINOR *100 + LZ4_VERSION_RELEASE)

                #define LZ4_LIB_VERSION LZ4_VERSION_MAJOR.LZ4_VERSION_MINOR.LZ4_VERSION_RELEASE
            */

            int verInt = (int)NativeMethods.VersionNumber();
            int major = verInt / 10000;
            int minor = verInt % 10000 / 100;
            int revision = verInt % 100;

            return new Version(major, minor, revision);
        }

        public static string LibraryVersionString()
        {
            if (!NativeMethods.Loaded)
                throw new InvalidOperationException(NativeMethods.MsgInitFirstError);

            return NativeMethods.VersionString();
        }
        #endregion

        #region Stream Methods
        /// <summary>
        /// For Decompress
        /// </summary>
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_mode != LZ4Mode.Decompress)
                throw new NotSupportedException("Read() not supported on compression");
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || buffer.Length < offset + count)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (count == 0)
                return 0;

            int readSize = 0;

            int destIdx = offset;
            int destCount = count;
            int destEndIdx = offset + count;

            using (PinnedArray pinSrc = new PinnedArray(_workBuf))
            using (PinnedArray pinDest = new PinnedArray(buffer))
            {
                if (_firstRead)
                {
                    using (PinnedArray pinHeader = new PinnedArray(FrameMagicNumber))
                    { // Write FrameMagicNumber into LZ4F_decompress
                        UIntPtr headerSizeVal = (UIntPtr)4;
                        UIntPtr destCountVal = (UIntPtr)destCount;

                        UIntPtr ret = NativeMethods.FrameDecompress(_dctx, pinDest[destIdx], ref destCountVal, pinHeader, ref headerSizeVal, null);
                        LZ4FrameException.CheckLZ4Error(ret);

                        Debug.Assert(headerSizeVal.ToUInt64() <= int.MaxValue);
                        Debug.Assert(destCountVal.ToUInt64() <= int.MaxValue);

                        if (headerSizeVal.ToUInt32() != 4u)
                            throw new InvalidOperationException("Not enough dest buffer");
                        int destWritten = (int)destCountVal.ToUInt32();

                        destIdx += destWritten;
                        TotalOut += destWritten;
                    }

                    _firstRead = false;
                }

                while (destIdx < destEndIdx)
                {
                    if (_decompSrcIdx == _decompSrcCount)
                    {
                        // Read from _baseStream
                        _decompSrcIdx = 0;
                        _decompSrcCount = _baseStream.Read(_workBuf, 0, _workBuf.Length);
                        TotalIn += _decompSrcCount;

                        // _baseStream reached its end
                        if (_decompSrcCount == 0)
                        {
                            _decompSrcIdx = DecompDone;
                            break;
                        }
                    }

                    UIntPtr srcCountVal = (UIntPtr)(_decompSrcCount - _decompSrcIdx);
                    UIntPtr destCountVal = (UIntPtr)destCount;

                    UIntPtr ret = NativeMethods.FrameDecompress(_dctx, pinDest[destIdx], ref destCountVal, pinSrc[_decompSrcIdx], ref srcCountVal, null);
                    LZ4FrameException.CheckLZ4Error(ret);

                    // The number of bytes consumed from srcBuffer will be written into *srcSizePtr (necessarily <= original value).
                    Debug.Assert(srcCountVal.ToUInt64() <= int.MaxValue);
                    int srcConsumed = (int)srcCountVal.ToUInt32();
                    _decompSrcIdx += srcConsumed;
                    Debug.Assert(_decompSrcIdx <= _decompSrcCount);

                    // The number of bytes decompressed into dstBuffer will be written into *dstSizePtr (necessarily <= original value).
                    Debug.Assert(destCountVal.ToUInt64() <= int.MaxValue);
                    int destWritten = (int)destCountVal.ToUInt32();
                    destIdx += destWritten;
                    TotalOut += destWritten;
                    readSize += destWritten;
                }
            }

            return readSize;
        }

        /// <summary>
        /// For Compress
        /// </summary>
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_mode != LZ4Mode.Compress)
                throw new NotSupportedException("Write() not supported on decompression");
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || buffer.Length < offset + count)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (count == 0)
                return;

            TotalIn += count;

            using (PinnedArray pinSrc = new PinnedArray(buffer))
            using (PinnedArray pinDest = new PinnedArray(_workBuf))
            {
                while (0 < count)
                {
                    int srcWorkSize = SrcBufSizeMax < count ? SrcBufSizeMax : count;

                    UIntPtr outSizeVal = NativeMethods.FrameCompressionUpdate(_cctx, pinDest, (UIntPtr)_destBufSize, pinSrc[offset], (UIntPtr)srcWorkSize, null);
                    LZ4FrameException.CheckLZ4Error(outSizeVal);

                    Debug.Assert(outSizeVal.ToUInt64() < int.MaxValue, "BufferSize should be <2GB");
                    int outSize = (int) outSizeVal.ToUInt64();

                    _baseStream.Write(_workBuf, 0, outSize);
                    TotalOut += outSize;

                    offset += srcWorkSize;
                    count -= srcWorkSize;
                    Debug.Assert(0 <= count, $"0 <= {count}");
                }
            }
        }

        private void FinishWrite()
        {
            Debug.Assert(_mode == LZ4Mode.Compress, "FinishWrite() must not be called in decompression");

            using (PinnedArray pinDest = new PinnedArray(_workBuf))
            {
                UIntPtr outSizeVal = NativeMethods.FrameCompressionEnd(_cctx, pinDest, (UIntPtr)_destBufSize, null);
                LZ4FrameException.CheckLZ4Error(outSizeVal);

                Debug.Assert(outSizeVal.ToUInt64() < int.MaxValue, "BufferSize should be <2GB");
                int outSize = (int)outSizeVal.ToUInt64();

                _baseStream.Write(_workBuf, 0, outSize);
                TotalOut += outSize;
            }
        }

        public override void Flush()
        {
            _baseStream.Flush();
        }

        public override bool CanRead => _mode == LZ4Mode.Decompress && _baseStream.CanRead;
        public override bool CanWrite => _mode == LZ4Mode.Compress && _baseStream.CanWrite;
        public override bool CanSeek => false;

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException("Seek() not supported");
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException("SetLength not supported");
        }

        public override long Length => throw new NotSupportedException("Length not supported");

        public override long Position
        {
            get => throw new NotSupportedException("Position not supported");
            set => throw new NotSupportedException("Position not supported");
        }

        public double CompressionRatio
        {
            get
            {
                switch (_mode)
                {
                    case LZ4Mode.Compress:
                        if (TotalIn == 0)
                            return 0;
                        return 100 - TotalOut * 100.0 / TotalIn;
                    case LZ4Mode.Decompress:
                        if (TotalOut == 0)
                            return 0;
                        return 100 - TotalIn * 100.0 / TotalOut;
                    default:
                        throw new InvalidOperationException("Internal Logic Error at LZ4Stream.CompressionRatio()");
                }
            }
        }
        #endregion
    }
}
