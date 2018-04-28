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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PEBakery.XZLib
{
    // ReSharper disable once InconsistentNaming
    public class XZStream : Stream
    {
        #region Fields and Properties
        private Stream _baseStream;
        private readonly LzmaMode _mode;
        private readonly bool _leaveOpen;
        private bool _disposed = false;

        private LzmaStream _lzmaStream;
        private GCHandle _lzmaStreamPin;

        private static int _internalBufferSize = 4 * 1024 * 1024; // 4MB
        private int _internalBufPos = 0;
        private const int ReadDone = -1;
        private readonly byte[] _internalBuf = new byte[_internalBufferSize];

        // Property
        public Stream BaseStream => _baseStream;

        public long TotalIn { get; private set; } = 0;
        public long TotalOut { get; private set; } = 0;
        // Const
        public const uint MinimumPreset = 0;
        public const uint DefaultPreset = 6;
        public const uint MaximumPreset = 9;
        public const uint ExtremeFlag = 1u << 31;
        #endregion

        #region Constructor
        public XZStream(Stream stream, LzmaMode mode)
            : this(stream, mode, DefaultPreset, 1, false) { }

        public XZStream(Stream stream, LzmaMode mode, uint preset)
            : this(stream, mode, preset, 1, false) { }

        public XZStream(Stream stream, LzmaMode mode, uint preset, int threads)
            : this(stream, mode, preset, threads, false) { }

        public XZStream(Stream stream, LzmaMode mode, bool leaveOpen)
            : this(stream, mode, 0, 1, leaveOpen) { }

        public XZStream(Stream stream, LzmaMode mode, uint preset, bool leaveOpen)
            : this(stream, mode, preset, 1, leaveOpen) { }

        public XZStream(Stream stream, LzmaMode mode, uint preset, int threads, bool leaveOpen)
        {
            if (!NativeMethods.Loaded)
                throw new InvalidOperationException(NativeMethods.MsgInitFirstError);

            if (9 < preset)
                throw new ArgumentOutOfRangeException(nameof(preset));
            if (threads < 0)
                throw new ArgumentOutOfRangeException(nameof(threads));

            if (threads == 0) // Use system's thread number by default
                threads = Environment.ProcessorCount;
            else if (Environment.ProcessorCount < threads) // If the number of CPU cores/threads exceeds system thread number,
                threads = Environment.ProcessorCount; // Limit the number of threads to keep memory usage lower.

            _baseStream = stream ?? throw new ArgumentNullException(nameof(stream));
            _mode = mode;
            _leaveOpen = leaveOpen;
            _disposed = false;

            _lzmaStream = new LzmaStream();
            _lzmaStreamPin = GCHandle.Alloc(_lzmaStream, GCHandleType.Pinned);
            
            switch (mode)
            {
                case LzmaMode.Compress:
                {
                        _lzmaStream.NextIn = IntPtr.Zero;
                        _lzmaStream.AvailIn = 0;
                        if (threads == 1)
                        { // Reference : 01_compress_easy.c
                            LzmaRet ret = NativeMethods.LzmaEasyEncoder(_lzmaStream, preset, LzmaCheck.CHECK_CRC64);
                            XZException.CheckLzmaError(ret);
                        }
                        else
                        { // Reference : 04_compress_easy_mt.c
                            LzmaMt mtOptions = new LzmaMt
                            {
                                // No flags are needed.
                                Flags = 0,
                                // Let liblzma determine a sane block size.
                                BlockSize = 0,
                                // Use no timeout for lzma_code() calls by setting timeout
                                // to zero. That is, sometimes lzma_code() might block for
                                // a long time (from several seconds to even minutes).
                                // If this is not OK, for example due to progress indicator
                                // needing updates, specify a timeout in milliseconds here.
                                // See the documentation of lzma_mt in lzma/container.h for
                                // information how to choose a reasonable timeout.
                                TimeOut = 0,
                                // To use a preset, filters must be set to NULL.
                                Filters = IntPtr.Zero,
                                Preset = preset,
                                // Use XZ default
                                Check = LzmaCheck.CHECK_CRC64,
                                // Set threads
                                Threads = (uint)threads,
                            };

                            // Initialize the threaded encoder.
                            LzmaRet ret = NativeMethods.LzmaStreamEncoderMt(_lzmaStream, mtOptions);
                            XZException.CheckLzmaError(ret);
                        }
                        break;
                    }
                case LzmaMode.Decompress:
                { // Reference : 02_decompress.c
                        if (1 < threads)
                        Trace.TraceWarning("Mulithread decompression is not supported");
                    LzmaRet ret = NativeMethods.LzmaStreamDecoder(_lzmaStream, ulong.MaxValue, LzmaDecodingFlag.CONCATENATED);
                    XZException.CheckLzmaError(ret);
                    break;
                }    
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode));
            }
        }
        #endregion

        #region Disposable Pattern
        ~XZStream()
        {
            Dispose(false);
            GC.SuppressFinalize(this);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                if (_lzmaStream != null)
                {
                    if (_mode == LzmaMode.Compress)
                    {
                        Flush();
                        FinishWrite();
                    }
                    else
                        _internalBufPos = ReadDone;

                    NativeMethods.LzmaEnd(_lzmaStream);
                    _lzmaStreamPin.Free();
                    _lzmaStream = null;
                }

                if (_baseStream != null)
                {
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
        public static void GlobalInit(string dllPath, int bufferSize = 4 * 1024 * 1024)
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

            // Check if dll is valid (liblzma.dll)
            if (NativeMethods.GetProcAddress(NativeMethods.hModule, "lzma_version_number") == IntPtr.Zero)
            {
                GlobalCleanup();
                throw new ArgumentException($"[{dllPath}] is not a valid liblzma library");
            }

            try
            {
                NativeMethods.LoadFuntions();
                _internalBufferSize = bufferSize;
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
             * Note from "lzma\version.h"
             *
             * The version number is of format xyyyzzzs where
             *  - x = major
             *  - yyy = minor
             *  - zzz = revision
             *  - s indicates stability: 0 = alpha, 1 = beta, 2 = stable
             *
             * The same xyyyzzz triplet is never reused with different stability levels.
             * For example, if 5.1.0alpha has been released, there will never be 5.1.0beta
             * or 5.1.0 stable.
             */

            int verInt = (int)NativeMethods.LzmaVersionNumber();
            int major = verInt / 10000000;
            int minor = verInt % 10000000 / 10000;
            int revision = verInt % 10000 / 10;
            int stability = verInt % 10;

            return new Version(major, minor, revision, stability);
        }

        public static string LibraryVersionString()
        {
            if (!NativeMethods.Loaded)
                throw new InvalidOperationException(NativeMethods.MsgInitFirstError);

            return NativeMethods.LzmaVersionString();
        }
        #endregion

        #region Stream Methods
        /// <summary>
        /// For Decompress
        /// </summary>
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_mode != LzmaMode.Decompress)
                throw new NotSupportedException("Read() not supported on compression");
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || buffer.Length < offset + count)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (count == 0)
                return 0;

            if (_internalBufPos == ReadDone)
                return 0;

            int readSize = 0;
            LzmaAction action = LzmaAction.RUN;

            using (PinnedArray pinRead = new PinnedArray(_internalBuf))
            using (PinnedArray pinBuffer = new PinnedArray(buffer))
            {
                _lzmaStream.NextIn = pinRead[_internalBufPos];
                _lzmaStream.NextOut = pinBuffer[offset];
                _lzmaStream.AvailOut = (uint)count;

                while (_lzmaStream.AvailOut != 0)
                {
                    if (_lzmaStream.AvailIn == 0)
                    {
                        // Read from _baseStream
                        int baseReadSize = _baseStream.Read(_internalBuf, 0, _internalBuf.Length);
                        TotalIn += baseReadSize;

                        _internalBufPos = 0;
                        _lzmaStream.NextIn = pinRead;
                        _lzmaStream.AvailIn = (uint) baseReadSize;

                        if (baseReadSize == 0) // End of stream
                            action = LzmaAction.FINISH;
                    }

                    ulong bakAvailIn = _lzmaStream.AvailIn;
                    ulong bakAvailOut = _lzmaStream.AvailOut;

                    LzmaRet ret = NativeMethods.LzmaCode(_lzmaStream, action);

                    _internalBufPos += (int)(bakAvailIn - _lzmaStream.AvailIn);
                    readSize += (int)(bakAvailOut - _lzmaStream.AvailOut);

                    // Once everything has been decoded successfully, the return value of lzma_code() will be LZMA_STREAM_END.
                    if (ret == LzmaRet.STREAM_END)
                    {
                        _internalBufPos = ReadDone;
                        break;
                    }

                    // Normally the return value of lzma_code() will be LZMA_OK until everything has been encoded.
                    XZException.CheckLzmaError(ret);
                }
            }

            TotalOut += readSize;
            return readSize;
        }

        /// <summary>
        /// For Compress
        /// </summary>
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_mode != LzmaMode.Compress)
                throw new NotSupportedException("Write() not supported on decompression");
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || buffer.Length < offset + count)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (count == 0)
                return;
            using (PinnedArray pinWrite = new PinnedArray(_internalBuf))
            using (PinnedArray pinBuffer = new PinnedArray(buffer))
            {
                _lzmaStream.NextIn = pinBuffer[offset];
                _lzmaStream.AvailIn = (uint)count;
                _lzmaStream.NextOut = pinWrite[_internalBufPos];
                _lzmaStream.AvailOut = (uint)(_internalBuf.Length - _internalBufPos);

                // Return condition : _lzmaStream.AvailIn == 0
                while (_lzmaStream.AvailIn != 0)
                {
                    // ulong bakAvailOut = _lzmaStream.AvailOut;
                    LzmaRet ret = NativeMethods.LzmaCode(_lzmaStream, LzmaAction.RUN);
                    _internalBufPos = (int)((ulong)_internalBuf.Length - _lzmaStream.AvailOut);

                    // If the output buffer is full, write the data from the output bufffer to the output file.
                    if (_lzmaStream.AvailOut == 0)
                    { 
                        // Write to _baseStream
                        _baseStream.Write(_internalBuf, 0, _internalBuf.Length);
                        TotalOut += _internalBuf.Length;

                        // Reset NextOut and AvailOut
                        _internalBufPos = 0;
                        _lzmaStream.NextOut = pinWrite;
                        _lzmaStream.AvailOut = (uint)_internalBuf.Length;
                    }

                    // Normally the return value of lzma_code() will be LZMA_OK until everything has been encoded.
                    XZException.CheckLzmaError(ret);
                }
            }

            TotalIn += count;
        }

        private void FinishWrite()
        {
            Debug.Assert(_mode == LzmaMode.Compress, "FinishWrite() must not be called in decompression");

            using (PinnedArray pinWrite = new PinnedArray(_internalBuf))
            {
                _lzmaStream.NextIn = IntPtr.Zero;
                _lzmaStream.AvailIn = 0;
                _lzmaStream.NextOut = pinWrite[_internalBufPos];
                _lzmaStream.AvailOut = (uint)(_internalBuf.Length - _internalBufPos);

                LzmaRet ret = LzmaRet.OK;
                while (ret != LzmaRet.STREAM_END)
                {
                    ulong bakAvailOut = _lzmaStream.AvailOut;
                    ret = NativeMethods.LzmaCode(_lzmaStream, LzmaAction.FINISH);
                    _internalBufPos = (int)(bakAvailOut - _lzmaStream.AvailOut);

                    // If the compression finished successfully,
                    // write the data from the output bufffer to the output file.
                    if (_lzmaStream.AvailOut == 0 || ret == LzmaRet.STREAM_END)
                    { // Write to _baseStream
                        // When lzma_code() has returned LZMA_STREAM_END, the output buffer is likely to be only partially
                        // full. Calculate how much new data there is to be written to the output file.
                        _baseStream.Write(_internalBuf, 0, _internalBufPos);
                        TotalOut += _internalBufPos;

                        // Reset NextOut and AvailOut
                        _internalBufPos = 0;
                        _lzmaStream.NextOut = pinWrite;
                        _lzmaStream.AvailOut = (uint)_internalBuf.Length;   
                    }
                    else
                    { // Once everything has been encoded successfully, the return value of lzma_code() will be LZMA_STREAM_END.
                        XZException.CheckLzmaError(ret);
                    }
                }
            }
        }

        public override void Flush()
        {
            if (_mode == LzmaMode.Decompress)
            {
                _baseStream.Flush();
                return;
            }
                
            using (PinnedArray pinWrite = new PinnedArray(_internalBuf))
            {
                _lzmaStream.NextIn = IntPtr.Zero;
                _lzmaStream.AvailIn = 0;
                _lzmaStream.NextOut = pinWrite[_internalBufPos];
                _lzmaStream.AvailOut = (uint)(_internalBuf.Length - _internalBufPos);

                LzmaRet ret = LzmaRet.OK;
                while (ret != LzmaRet.STREAM_END)
                {
                    int writeSize = 0;
                    if (_lzmaStream.AvailOut != 0)
                    {
                        ulong bakAvailOut = _lzmaStream.AvailOut;
                        ret = NativeMethods.LzmaCode(_lzmaStream, LzmaAction.FULL_FLUSH);
                        writeSize += (int) (bakAvailOut - _lzmaStream.AvailOut);
                    }
                    _internalBufPos += writeSize;

                    _baseStream.Write(_internalBuf, 0, _internalBufPos);
                    TotalOut += _internalBufPos;

                    // Reset NextOut and AvailOut
                    _internalBufPos = 0;
                    _lzmaStream.NextOut = pinWrite;
                    _lzmaStream.AvailOut = (uint)_internalBuf.Length;

                    // Once everything has been encoded successfully, the return value of lzma_code() will be LZMA_STREAM_END.
                    if (ret != LzmaRet.OK && ret != LzmaRet.STREAM_END)
                        throw new XZException(ret);
                }
            }

            _baseStream.Flush();
        }

        public override bool CanRead => _mode == LzmaMode.Decompress && _baseStream.CanRead;
        public override bool CanWrite => _mode == LzmaMode.Compress && _baseStream.CanWrite;
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
                if (_mode == LzmaMode.Compress)
                {
                    if (TotalIn == 0)
                        return 0;
                    return 100 - TotalOut * 100.0 / TotalIn;
                }
                else
                {
                    if (TotalOut == 0)
                        return 0;
                    return 100 - TotalIn * 100.0 / TotalOut;
                }
            }
        }
        #endregion
    }
}
