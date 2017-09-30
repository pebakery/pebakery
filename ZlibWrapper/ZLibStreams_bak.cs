using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace ZLibWrapper
{
    /// <summary>Provides methods and properties used to compress and decompress streams.</summary>
    public class DeflateStream : Stream
    { 
		long pBytesIn = 0;
		long pBytesOut = 0;
		bool pSuccess;
		const int WORK_DATA_SIZE = 0x1000;
		byte[] pWorkData = new byte[WORK_DATA_SIZE];
		int pWorkDataPos = 0;

		private Stream stream;
		private CompressionMode compMode;
        private CompressionLevel compLevel;
		private ZStream pZstream = new ZStream();
		bool leaveOpen;

        private SafeLibraryHandle hZLibModule;

		public DeflateStream(Stream stream, CompressionMode mode, string zlibMoublePath = null)
			: this(stream, mode, CompressionLevel.Default, zlibMoublePath)
		{
		}

		public DeflateStream(Stream stream, CompressionMode mode, bool leaveOpen, string zlibMoublePath = null) :
			this(stream, mode, CompressionLevel.Default, leaveOpen, zlibMoublePath)
		{
        }

		public DeflateStream(Stream stream, CompressionMode mode, CompressionLevel level, string zlibMoublePath = null) :
			this(stream, mode, level, false, zlibMoublePath)
		{
        }

		public DeflateStream(Stream stream, CompressionMode mode, CompressionLevel level, bool leaveOpen, string zlibMoublePath = null)
		{
            hZLibModule = NativeLibrary.InitLibrary(zlibMoublePath);
            ZLibNative.LoadFuntions(hZLibModule);

			this.leaveOpen = leaveOpen;
			this.stream = stream;
			this.compMode = mode;

			int ret;
			if (this.compMode == CompressionMode.Compress)
				ret = ZLibNative.DeflateInit(ref pZstream, level, WriteType);
			else
				ret = ZLibNative.InflateInit(ref pZstream, OpenType);

			if (ret != ZLibReturnCode.Ok)
				throw new ZLibException(ret, pZstream.LastErrorMsg);

            pSuccess = true;
		}

		~DeflateStream()
		{
        	this.Dispose(false);
		}

        /// <summary>
        /// Stream.Close() ->   this.Dispose(true); + GC.SuppressFinalize(this);
        /// Stream.Dispose() ->  this.Close();
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
		{
			try
			{
				try
				{
					if (disposing) // Managed stuff
					{
						if (this.stream != null)
						{
							// Managed stuff
							if (this.compMode == CompressionMode.Compress && pSuccess)
							{
								Flush();
							}
							if (!leaveOpen)
								this.stream.Close();
							this.stream = null;
						}
					}
				}
				finally
				{
					// Unmanaged stuff
					FreeUnmanagedResources();
				}
			}
			finally
			{
				base.Dispose(disposing);
			}
		}

		// Finished, free the resources used.
		private void FreeUnmanagedResources()
		{
			if (this.compMode == CompressionMode.Compress)
				ZLibNative.DeflateEnd(ref pZstream);
			else
				ZLibNative.InflateEnd(ref pZstream);

            if (hZLibModule != null)
            {
                hZLibModule.Dispose();
            }
        }

		protected virtual ZLibOpenType OpenType
		{
			get { return ZLibOpenType.Deflate; }
		}

		protected virtual ZLibWriteType WriteType
		{
			get { return ZLibWriteType.Deflate; }
		}

        private bool ValidateReadWriteArgs(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                return false;

            if (offset < 0)
                return false;

            if (count < 0)
                return false;

            if (buffer.Length - offset < count)
                return false;

            return true;
        }

        /// <summary>Reads a number of decompressed bytes into the specified byte array.</summary>
        /// <param name="array">The array used to store decompressed bytes.</param>
        /// <param name="offset">The location in the array to begin reading.</param>
        /// <param name="count">The number of bytes decompressed.</param>
        /// <returns>The number of bytes that were decompressed into the byte array. If the end of the stream has been reached, zero or the number of bytes read is returned.</returns>
        public override int Read(byte[] buffer, int offset, int count)
		{
			if (compMode == CompressionMode.Compress)
				throw new NotSupportedException("Can't read on a compress stream!");

            ValidateReadWriteArgs(buffer, offset, count);

            int readLen = 0;
            if (pWorkDataPos != -1)
            {
                using (PinnedArray workDataPtr = new PinnedArray(pWorkData))
                using (PinnedArray bufferPtr = new PinnedArray(buffer))
                {
                    pZstream.next_in = workDataPtr;
                    pZstream.avail_in = (uint)stream.Read(pWorkData, 0, WORK_DATA_SIZE);

                    pZstream.next_in = workDataPtr[pWorkDataPos];
                    pZstream.next_out = bufferPtr[offset];
                    pZstream.avail_out = (uint)count;

                    while (pZstream.avail_out != 0)
                    {
                        if (pZstream.avail_in == 0)
                        {
                            pWorkDataPos = 0;
                            pZstream.next_in = workDataPtr;
                            pZstream.avail_in = (uint)stream.Read(pWorkData, 0, WORK_DATA_SIZE);
                            pBytesIn += pZstream.avail_in;
                        }

                        uint inCount = pZstream.avail_in;
                        uint outCount = pZstream.avail_out;

                        int zlibError = ZLibNative.Inflate(ref pZstream, ZLibFlush.Z_NO_FLUSH); // flush method for inflate has no effect

                        pWorkDataPos += (int)(inCount - pZstream.avail_in);
                        readLen += (int)(outCount - pZstream.avail_out);

                        if (zlibError == ZLibReturnCode.StreamEnd)
                        {
                            pWorkDataPos = -1; // magic for StreamEnd
                            break;
                        }
                        else if (zlibError != ZLibReturnCode.Ok)
                        {
                            pSuccess = false;
                            throw new ZLibException(zlibError, pZstream.LastErrorMsg);
                        }
                    }

                    pBytesOut += readLen;
                }
            }

            /*
            int readLen = 0;
			if (pWorkDataPos != -1)
			{
				using (PinnedArray workDataPtr = new PinnedArray(pWorkData))
				using (PinnedArray bufferPtr = new PinnedArray(buffer))
				{
					pZstream.next_in = workDataPtr[pWorkDataPos];
					pZstream.next_out = bufferPtr[offset];
					pZstream.avail_out = (uint)count;

					while (pZstream.avail_out != 0)
					{
						if (pZstream.avail_in == 0)
						{
							pWorkDataPos = 0;
							pZstream.next_in = workDataPtr;
							pZstream.avail_in = (uint)pStream.Read(pWorkData, 0, WORK_DATA_SIZE);
							pBytesIn += pZstream.avail_in;
						}

						uint inCount = pZstream.avail_in;
						uint outCount = pZstream.avail_out;

						int zlibError = ZLibNative.Inflate(ref pZstream, ZLibFlush.Z_NO_FLUSH); // flush method for inflate has no effect

						pWorkDataPos += (int)(inCount - pZstream.avail_in);
						readLen += (int)(outCount - pZstream.avail_out);

						if (zlibError == ZLibReturnCode.StreamEnd)
						{
							pWorkDataPos = -1; // magic for StreamEnd
							break;
						}
						else if (zlibError != ZLibReturnCode.Ok)
						{
							pSuccess = false;
							throw new ZLibException(zlibError, pZstream.LastErrorMsg);
						}
					}

					pBytesOut += readLen;
				}
			}
            */
            return readLen;
		}


		/// <summary>This property is not supported and always throws a NotSupportedException.</summary>
		/// <param name="array">The array used to store compressed bytes.</param>
		/// <param name="offset">The location in the array to begin reading.</param>
		/// <param name="count">The number of bytes compressed.</param>
		public override void Write(byte[] buffer, int offset, int count)
		{
			if (compMode == CompressionMode.Decompress)
				throw new NotSupportedException("Can't write on a decompression stream!");

			pBytesIn += count;

			using (PinnedArray writePtr = new PinnedArray(pWorkData))
			using (PinnedArray bufferPtr = new PinnedArray(buffer))
			{
				pZstream.next_in = bufferPtr[offset];
				pZstream.avail_in = (uint)count;
				pZstream.next_out = writePtr[pWorkDataPos];
				pZstream.avail_out = (uint)(WORK_DATA_SIZE - pWorkDataPos);

				while (pZstream.avail_in != 0)
				{
					if (pZstream.avail_out == 0)
					{
						stream.Write(pWorkData, 0, (int)WORK_DATA_SIZE);
						pBytesOut += WORK_DATA_SIZE;
						pWorkDataPos = 0;
						pZstream.next_out = writePtr;
						pZstream.avail_out = WORK_DATA_SIZE;
					}

					uint outCount = pZstream.avail_out;

					int zlibError = ZLibNative.Deflate(ref pZstream, ZLibFlush.Z_NO_FLUSH);

					pWorkDataPos += (int)(outCount - pZstream.avail_out);

					if (zlibError != ZLibReturnCode.Ok)
					{
						pSuccess = false;
						throw new ZLibException(zlibError, pZstream.LastErrorMsg);
					}

				}
			}
		}

		/// <summary>Flushes the contents of the internal buffer of the current GZipStream object to the underlying stream.</summary>
		public override void Flush()
		{
			if (compMode == CompressionMode.Decompress)
				throw new NotSupportedException("Can't flush a decompression stream.");

			using (PinnedArray workDataPtr = new PinnedArray(pWorkData))
			{
				pZstream.next_in = IntPtr.Zero;
				pZstream.avail_in = 0;
				pZstream.next_out = workDataPtr[pWorkDataPos];
				pZstream.avail_out = (uint)(WORK_DATA_SIZE - pWorkDataPos);

				int zlibError = ZLibReturnCode.Ok;
				while (zlibError != ZLibReturnCode.StreamEnd)
				{
					if (pZstream.avail_out != 0)
					{
						uint outCount = pZstream.avail_out;
						zlibError = ZLibNative.Deflate(ref pZstream, ZLibFlush.Z_FINISH);

						pWorkDataPos += (int)(outCount - pZstream.avail_out);
						if (zlibError != ZLibReturnCode.StreamEnd && zlibError != ZLibReturnCode.Ok)
						{
							pSuccess = false;
							throw new ZLibException(zlibError, pZstream.LastErrorMsg);
						}
					}

					stream.Write(pWorkData, 0, pWorkDataPos);
					pBytesOut += pWorkDataPos;
					pWorkDataPos = 0;
					pZstream.next_out = workDataPtr;
					pZstream.avail_out = WORK_DATA_SIZE;
				}
			}

			this.stream.Flush();
		}

		public long TotalIn
		{
			get { return this.pBytesIn; }
		}

		public long TotalOut
		{
			get { return this.pBytesOut; }
		}

		// The compression ratio obtained (same for compression/decompression).
		public double CompressionRatio
		{
			get
			{
				if (compMode == CompressionMode.Compress)
					return ((pBytesIn == 0) ? 0.0 : (100.0 - ((double)pBytesOut * 100.0 / (double)pBytesIn)));
				else
					return ((pBytesOut == 0) ? 0.0 : (100.0 - ((double)pBytesIn * 100.0 / (double)pBytesOut)));
			}
		}

		/// <summary>Gets a value indicating whether the stream supports reading while decompressing a file.</summary>
		public override bool CanRead
		{
			get
			{
				return compMode == CompressionMode.Decompress && stream.CanRead;
			}
		}

		/// <summary>Gets a value indicating whether the stream supports writing.</summary>
		public override bool CanWrite
		{
			get
			{
				return compMode == CompressionMode.Compress && stream.CanWrite;
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
			get { return (this.stream); }
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

	/// <summary>
	/// zlib header + adler32 et end.
	/// wraps a deflate stream
	/// </summary>
	public class ZLibStream : DeflateStream
	{
		public ZLibStream(Stream stream, CompressionMode mode, string zlibMoublePath = null)
			: base(stream, mode, zlibMoublePath)
		{
		}
		public ZLibStream(Stream stream, CompressionMode mode, bool leaveOpen, string zlibMoublePath = null) :
			base(stream, mode, leaveOpen, zlibMoublePath)
		{
		}
		public ZLibStream(Stream stream, CompressionMode mode, CompressionLevel level, string zlibMoublePath = null) :
			base(stream, mode, level, zlibMoublePath)
		{
		}
		public ZLibStream(Stream stream, CompressionMode mode, CompressionLevel level, bool leaveOpen, string zlibMoublePath = null) :
			base(stream, mode, level, leaveOpen, zlibMoublePath)
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

	/// <summary>
	/// Saved to file (.gz) can be opened with zip utils.
	/// Have hdr + crc32 at end.
	/// Wraps a deflate stream
	/// </summary>
	public class GZipStream : DeflateStream
	{
		public GZipStream(Stream stream, CompressionMode mode, string zlibMoublePath = null)
			: base(stream, mode, zlibMoublePath)
		{
		}
		public GZipStream(Stream stream, CompressionMode mode, bool leaveOpen, string zlibMoublePath = null)
			: base(stream, mode, leaveOpen, zlibMoublePath)
		{
		}
		public GZipStream(Stream stream, CompressionMode mode, CompressionLevel level, string zlibMoublePath = null)
			: base(stream, mode, level, zlibMoublePath)
		{
		}
		public GZipStream(Stream stream, CompressionMode mode, CompressionLevel level, bool leaveOpen, string zlibMoublePath = null)
			: base(stream, mode, level, leaveOpen, zlibMoublePath)
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


}
