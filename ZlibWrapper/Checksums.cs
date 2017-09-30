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
using System.Linq;
using System.Text;
using System.IO;

namespace ZLibWrapper
{
    #region Crc32Stream
    public class Crc32Stream : Stream
	{
		private uint crc32 = 0;
		private Stream baseStream;

		public Crc32Stream(Stream stream)
		{
            if ((ZLibNative.Loaded && !ZLibNative.ZLibProvided) || !ZLibNative.Loaded)
                throw new InvalidOperationException("To use Crc32Stream, init ZLibWrapper first with user provided zlibwapi.dll");

            this.baseStream = stream;
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			int readLen = baseStream.Read(buffer, offset, count);
			using (PinnedArray bufferPtr = new PinnedArray(buffer))
			{
				crc32 = ZLibNative.Crc32(crc32, bufferPtr[offset], (uint)readLen);
			}
			return readLen;
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			baseStream.Write(buffer, offset, count);
			using (PinnedArray bufferPtr = new PinnedArray(buffer))
			{
				crc32 = ZLibNative.Crc32(crc32, bufferPtr[offset], (uint)count);
			}
		}

		public override void Flush()
		{
			this.baseStream.Flush();
		}

        public uint Crc32 => crc32;

        public override bool CanRead => baseStream.CanRead;

        public override bool CanWrite => baseStream.CanWrite;

        public override bool CanSeek => (baseStream.CanSeek);

        public Stream BaseStream => baseStream;

        public override long Seek(long offset, SeekOrigin origin)
        {
            return baseStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            baseStream.SetLength(value);
        }

        public override long Length => baseStream.Length;

        public override long Position
        {
            get => baseStream.Position;
            set => baseStream.Position = value;
        }
    }
    #endregion

    #region Adler32Stream
    public class Adler32Stream : Stream
    {
        private uint adler32 = 0;
        private Stream baseStream;

        public Adler32Stream(Stream stream)
        {
            if ((ZLibNative.Loaded && !ZLibNative.ZLibProvided) || !ZLibNative.Loaded)
                throw new InvalidOperationException("To use Adler32Stream, init ZLibWrapper first with user provided zlibwapi.dll");

            this.baseStream = stream;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int readLen = baseStream.Read(buffer, offset, count);
            using (PinnedArray bufferPtr = new PinnedArray(buffer))
            {
                adler32 = ZLibNative.Adler32(adler32, bufferPtr[offset], (uint)readLen);
            }
            return readLen;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            baseStream.Write(buffer, offset, count);
            using (PinnedArray bufferPtr = new PinnedArray(buffer))
            {
                adler32 = ZLibNative.Adler32(adler32, bufferPtr[offset], (uint)count);
            }
        }

        public override void Flush()
        {
            this.baseStream.Flush();
        }

        public uint Adler32 => adler32;

        public override bool CanRead => baseStream.CanRead;

        public override bool CanWrite => baseStream.CanWrite;

        public override bool CanSeek => (baseStream.CanSeek);

        public Stream BaseStream => baseStream;

        public override long Seek(long offset, SeekOrigin origin)
        {
            return baseStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            baseStream.SetLength(value);
        }

        public override long Length => baseStream.Length;

        public override long Position
        {
            get => baseStream.Position;
            set => baseStream.Position = value;
        }
    }
    #endregion

    #region ChecksumCalculator
    public static class ChecksumCalculator
	{
		public static uint Crc32(byte[] buffer)
		{
			using (PinnedArray bufferPtr = new PinnedArray(buffer))
			{
				return ZLibNative.Crc32(0, bufferPtr, (uint)buffer.Length);
			}
		}

        public static uint Adler32(byte[] buffer)
        {
            using (PinnedArray bufferPtr = new PinnedArray(buffer))
            {
                return ZLibNative.Adler32(0, bufferPtr, (uint)buffer.Length);
            }
        }
    }
    #endregion
}
