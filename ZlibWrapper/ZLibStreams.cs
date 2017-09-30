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
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace ZLibWrapper
{
    #region DeflateStream
    /// <summary>Provides methods and properties used to compress and decompress streams.</summary>
    public class DeflateStream : Stream
    { 
		long _bytesIn = 0;
		long _bytesOut = 0;
		bool _success = false;
		
		private Stream _stream;
		private CompressionMode _compMode;
        private CompressionLevel _compLevel;
        private ZStream _zstream;
        private GCHandle _zstreamPtr;
        private bool _leaveOpen;

        // Dispose Pattern
        private bool _zstreamDisposed = false;
        private bool _zstreamPtrDisposed = false;

        const int WORK_DATA_SIZE = 0x1000;
		byte[] _workData = new byte[WORK_DATA_SIZE];
		int _workDataPos = 0;

		public DeflateStream(Stream stream, CompressionMode mode)
			: this(stream, mode, CompressionLevel.Default)
		{
		}

		public DeflateStream(Stream stream, CompressionMode mode, bool leaveOpen) :
			this(stream, mode, CompressionLevel.Default, leaveOpen)
		{
        }

		public DeflateStream(Stream stream, CompressionMode mode, CompressionLevel level) :
			this(stream, mode, level, false)
		{
        }

		public DeflateStream(Stream stream, CompressionMode mode, CompressionLevel level, bool leaveOpen)
		{
            if (ZLibNative.Loaded == false)
                ZLibNative.AssemblyInit(); 

            this._zstream = new ZStream();
            this._zstream.Init();
            this._zstreamPtr = GCHandle.Alloc(_zstream, GCHandleType.Pinned);

            this._leaveOpen = leaveOpen;
			this._stream = stream;
			this._compMode = mode;
            this._compLevel = level;
            this._workDataPos = 0;


            int ret;
			if (this._compMode == CompressionMode.Compress)
				ret = ZLibNative.DeflateInit(ref _zstream, level, WriteType);
			else
				ret = ZLibNative.InflateInit(ref _zstream, OpenType);

			if (ret != ZLibReturnCode.Ok)
				throw new ZLibException(ret, _zstream.LastErrorMsg);

            _success = true;
		}

        #region Disposable Pattern
        ~DeflateStream()
		{
        	this.Dispose(false);
		}

        protected override void Dispose(bool disposing)
		{
            if (disposing)
            {
                if (_stream != null)
                {
                    if (_compMode == CompressionMode.Compress && _success)
                        Flush();
                    if (!_leaveOpen)
                        _stream.Close();
                    _stream = null;
                }

                if (_zstreamDisposed == false)
                {
                    if (this._compMode == CompressionMode.Compress)
                        ZLibNative.DeflateEnd(ref _zstream);
                    else
                        ZLibNative.InflateEnd(ref _zstream);
                    _zstreamDisposed = true;
                }

                if (_zstreamPtrDisposed == false)
                {
                    _zstreamPtr.Free();
                    _zstreamPtrDisposed = true;
                }
            }
		}
        #endregion

        #region Properties
        protected virtual ZLibOpenType OpenType
		{
			get { return ZLibOpenType.Deflate; }
		}

		protected virtual ZLibWriteType WriteType
		{
			get { return ZLibWriteType.Deflate; }
		}
        #endregion

        private void ValidateReadWriteArgs(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentException("[buffer] cannot be null");

            if (offset < 0)
                throw new ArgumentException("[offset] must be positive integer or 0");

            if (count < 0)
                throw new ArgumentException("[count] must be positive integer or 0");

            if (buffer.Length - offset < count)
                throw new ArgumentException("[count + offset] should be longer than [buffer.Length]");
        }

        /// <summary>Reads a number of decompressed bytes into the specified byte array.</summary>
        /// <param name="array">The array used to store decompressed bytes.</param>
        /// <param name="offset">The location in the array to begin reading.</param>
        /// <param name="count">The number of bytes decompressed.</param>
        /// <returns>The number of bytes that were decompressed into the byte array. If the end of the stream has been reached, zero or the number of bytes read is returned.</returns>
        public override int Read(byte[] buffer, int offset, int count)
		{
			if (_compMode == CompressionMode.Compress)
				throw new NotSupportedException("Can't read on a compress stream!");

            ValidateReadWriteArgs(buffer, offset, count);

            int readLen = 0;
            if (_workDataPos != -1)
            {
                using (PinnedArray workDataPtr = new PinnedArray(_workData)) // [In] Compressed
                using (PinnedArray bufferPtr = new PinnedArray(buffer)) // [Out] Will-be-decompressed
                {
                    _zstream.next_in = workDataPtr[_workDataPos];
                    _zstream.next_out = bufferPtr[offset];
                    _zstream.avail_out = (uint)count;

                    while (0 < _zstream.avail_out)
                    {
                        if (_zstream.avail_in == 0)
                        { // Compressed Data is no longer available in array, so read more from _stream
                            _workDataPos = 0;
                            _zstream.next_in = workDataPtr;
                            _zstream.avail_in = (uint)_stream.Read(_workData, 0, _workData.Length);
                            _bytesIn += _zstream.avail_in;
                        }

                        uint inCount = _zstream.avail_in;
                        uint outCount = _zstream.avail_out;

                        // flush method for inflate has no effect
                        int zlibError = ZLibNative.Inflate(ref _zstream, ZLibFlush.Z_NO_FLUSH); 

                        _workDataPos += (int)(inCount - _zstream.avail_in);
                        readLen += (int)(outCount - _zstream.avail_out); 

                        if (zlibError == ZLibReturnCode.StreamEnd)
                        {
                            _workDataPos = -1; // magic for StreamEnd
                            break;
                        }
                        else if (zlibError != ZLibReturnCode.Ok)
                        {
                            _success = false;
                            throw new ZLibException(zlibError, _zstream.LastErrorMsg);
                        }
                    }

                    _bytesOut += readLen;
                }
            }
            return readLen;
        }

        public override void Write(byte[] buffer, int offset, int count)
		{
			if (_compMode == CompressionMode.Decompress)
				throw new NotSupportedException("Can't write on a decompression stream!");

            _bytesIn += count;

			using (PinnedArray writePtr = new PinnedArray(_workData))
			using (PinnedArray bufferPtr = new PinnedArray(buffer))
			{
				_zstream.next_in = bufferPtr[offset];
				_zstream.avail_in = (uint)count;
				_zstream.next_out = writePtr[_workDataPos];
				_zstream.avail_out = (uint)(WORK_DATA_SIZE - _workDataPos);

				while (_zstream.avail_in != 0)
				{
					if (_zstream.avail_out == 0)
					{
						_stream.Write(_workData, 0, (int)WORK_DATA_SIZE);
						_bytesOut += WORK_DATA_SIZE;
						_workDataPos = 0;
						_zstream.next_out = writePtr;
						_zstream.avail_out = WORK_DATA_SIZE;
					}

					uint outCount = _zstream.avail_out;

					int zlibError = ZLibNative.Deflate(ref _zstream, ZLibFlush.Z_NO_FLUSH);

					_workDataPos += (int)(outCount - _zstream.avail_out);

					if (zlibError != ZLibReturnCode.Ok)
					{
						_success = false;
						throw new ZLibException(zlibError, _zstream.LastErrorMsg);
					}

				}
			}
		}

		/// <summary>Flushes the contents of the internal buffer of the current GZipStream object to the underlying stream.</summary>
		public override void Flush()
		{
			if (_compMode == CompressionMode.Decompress)
				throw new NotSupportedException("Can't flush a decompression stream.");

			using (PinnedArray workDataPtr = new PinnedArray(_workData))
			{
				_zstream.next_in = IntPtr.Zero;
				_zstream.avail_in = 0;
				_zstream.next_out = workDataPtr[_workDataPos];
				_zstream.avail_out = (uint)(WORK_DATA_SIZE - _workDataPos);

				int zlibError = ZLibReturnCode.Ok;
				while (zlibError != ZLibReturnCode.StreamEnd)
				{
					if (_zstream.avail_out != 0)
					{
						uint outCount = _zstream.avail_out;
						zlibError = ZLibNative.Deflate(ref _zstream, ZLibFlush.Z_FINISH);

						_workDataPos += (int)(outCount - _zstream.avail_out);
						if (zlibError != ZLibReturnCode.StreamEnd && zlibError != ZLibReturnCode.Ok)
						{
							_success = false;
							throw new ZLibException(zlibError, _zstream.LastErrorMsg);
						}
					}

					_stream.Write(_workData, 0, _workDataPos);
					_bytesOut += _workDataPos;
					_workDataPos = 0;
					_zstream.next_out = workDataPtr;
					_zstream.avail_out = WORK_DATA_SIZE;
				}
			}

			this._stream.Flush();
		}

		public long TotalIn
		{
			get { return this._bytesIn; }
		}

		public long TotalOut
		{
			get { return this._bytesOut; }
		}

		// The compression ratio obtained (same for compression/decompression).
		public double CompressionRatio
		{
			get
			{
				if (_compMode == CompressionMode.Compress)
					return ((_bytesIn == 0) ? 0.0 : (100.0 - ((double)_bytesOut * 100.0 / (double)_bytesIn)));
				else
					return ((_bytesOut == 0) ? 0.0 : (100.0 - ((double)_bytesIn * 100.0 / (double)_bytesOut)));
			}
		}

		/// <summary>Gets a value indicating whether the stream supports reading while decompressing a file.</summary>
		public override bool CanRead
		{
			get
			{
				return _compMode == CompressionMode.Decompress && _stream.CanRead;
			}
		}

		/// <summary>Gets a value indicating whether the stream supports writing.</summary>
		public override bool CanWrite
		{
			get
			{
				return _compMode == CompressionMode.Compress && _stream.CanWrite;
			}
		}

		/// <summary>Gets a value indicating whether the stream supports seeking.</summary>
		public override bool CanSeek
		{
			get { return (false); }
		}

		/// <summary>Gets a reference to the underlying stream.</summary>
		public Stream BaseStream
		{
			get { return (this._stream); }
		}

		/// <summary>This property is not supported and always throws a NotSupportedException.</summary>
		/// <param name="offset">The location in the stream.</param>
		/// <param name="origin">One of the SeekOrigin values.</param>
		/// <returns>A long value.</returns>
		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new NotSupportedException("Seek not supported");
		}

		/// <summary>This property is not supported and always throws a NotSupportedException.</summary>
		/// <param name="value">The length of the stream.</param>
		public override void SetLength(long value)
		{
			throw new NotSupportedException("SetLength not supported");
		}

		/// <summary>This property is not supported and always throws a NotSupportedException.</summary>
		public override long Length
		{
			get
			{
				throw new NotSupportedException("Length not supported.");
			}
		}

		/// <summary>This property is not supported and always throws a NotSupportedException.</summary>
		public override long Position
		{
			get
			{
				throw new NotSupportedException("Position not supported.");
			}
			set
			{
				throw new NotSupportedException("Position not supported.");
			}
		}
	}
    #endregion

    #region ZLibStream
    /// <summary>
    /// zlib header + adler32 et end.
    /// wraps a deflate stream
    /// </summary>
    public class ZLibStream : DeflateStream
	{
		public ZLibStream(Stream stream, CompressionMode mode)
			: base(stream, mode)
		{
		}
		public ZLibStream(Stream stream, CompressionMode mode, bool leaveOpen) :
			base(stream, mode, leaveOpen)
		{
		}
		public ZLibStream(Stream stream, CompressionMode mode, CompressionLevel level) :
			base(stream, mode, level)
		{
		}
		public ZLibStream(Stream stream, CompressionMode mode, CompressionLevel level, bool leaveOpen) :
			base(stream, mode, level, leaveOpen)
		{
		}

		protected override ZLibOpenType OpenType
		{
			get { return ZLibOpenType.ZLib; }
		}
		protected override ZLibWriteType WriteType
		{
			get { return ZLibWriteType.ZLib; }
		}
	}
    #endregion

    #region GZipStream
    /// <summary>
    /// Saved to file (.gz) can be opened with zip utils.
    /// Have hdr + crc32 at end.
    /// Wraps a deflate stream
    /// </summary>
    public class GZipStream : DeflateStream
	{
		public GZipStream(Stream stream, CompressionMode mode)
			: base(stream, mode)
		{
		}
		public GZipStream(Stream stream, CompressionMode mode, bool leaveOpen)
			: base(stream, mode, leaveOpen)
		{
		}
		public GZipStream(Stream stream, CompressionMode mode, CompressionLevel level)
			: base(stream, mode, level)
		{
		}
		public GZipStream(Stream stream, CompressionMode mode, CompressionLevel level, bool leaveOpen)
			: base(stream, mode, level, leaveOpen)
		{
		}

		protected override ZLibOpenType OpenType
		{
			get { return ZLibOpenType.GZip; }
		}
		protected override ZLibWriteType WriteType
		{
			get { return ZLibWriteType.GZip; }
		}
	}
    #endregion
}
