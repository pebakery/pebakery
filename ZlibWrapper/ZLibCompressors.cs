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
	/// <summary>
	/// Classes that simplify a common use of compression streams
	/// </summary>

	delegate DeflateStream CreateStreamDelegate(Stream s, CompressionMode cm, bool leaveOpen);

    #region DeflateCompressor
    public static class DeflateCompressor
	{
		public static MemoryStream Compress(Stream source)
		{
			return CommonCompressor.Compress(CreateStream, source);
		}
		public static MemoryStream Decompress(Stream source)
		{
			return CommonCompressor.Decompress(CreateStream, source);
		}
		public static byte[] Compress(byte[] source)
		{
			return CommonCompressor.Compress(CreateStream, source);
		}
		public static byte[] Decompress(byte[] source)
		{
			return CommonCompressor.Decompress(CreateStream, source);
		}
		private static DeflateStream CreateStream(Stream s, CompressionMode cm, bool leaveOpen)
		{
			return new DeflateStream(s, cm, leaveOpen);
		}
	}
    #endregion

    #region ZLibCompressor
    public static class ZLibCompressor
    {
        public static MemoryStream Compress(Stream source)
        {
            return CommonCompressor.Compress(CreateStream, source);
        }
        public static MemoryStream Decompress(Stream source)
        {
            return CommonCompressor.Decompress(CreateStream, source);
        }
        public static byte[] Compress(byte[] source)
        {
            return CommonCompressor.Compress(CreateStream, source);
        }
        public static byte[] Decompress(byte[] source)
        {
            return CommonCompressor.Decompress(CreateStream, source);
        }
        private static DeflateStream CreateStream(Stream s, CompressionMode cm, bool leaveOpen)
        {
            return new ZLibStream(s, cm, leaveOpen);
        }
    }
    #endregion

    #region GZipCompressor
    public static class GZipCompressor
	{
		public static MemoryStream Compress(Stream source)
		{
			return CommonCompressor.Compress(CreateStream, source);
		}
		public static MemoryStream Decompress(Stream source)
		{
			return CommonCompressor.Decompress(CreateStream, source);
		}
		public static byte[] Compress(byte[] source)
		{
			return CommonCompressor.Compress(CreateStream, source);
		}
		public static byte[] Decompress(byte[] source)
		{
			return CommonCompressor.Decompress(CreateStream, source);
		}
		private static DeflateStream CreateStream(Stream s, CompressionMode cm, bool leaveOpen)
		{
			return new GZipStream(s, cm, leaveOpen);
		}
	}
    #endregion

    #region CommonCompressor
    internal class CommonCompressor
	{
		private static void Compress(CreateStreamDelegate sc, Stream source, Stream dest)
		{
            if (ZLibNative.Loaded == false)
                ZLibNative.AssemblyInit();

            using (DeflateStream zsDest = sc(dest, CompressionMode.Compress, true))
			{
				source.CopyTo(zsDest);
			}
		}

		private static void Decompress(CreateStreamDelegate sc, Stream source, Stream dest)
		{
            if (ZLibNative.Loaded == false)
                ZLibNative.AssemblyInit();

            using (DeflateStream zsSource = sc(source, CompressionMode.Decompress, true))
			{
				zsSource.CopyTo(dest);
			}
		}

		public static MemoryStream Compress(CreateStreamDelegate sc, Stream source)
		{
            if (ZLibNative.Loaded == false)
                ZLibNative.AssemblyInit();

            MemoryStream result = new MemoryStream();
			Compress(sc, source, result);
			result.Position = 0;
			return result;
		}

		public static MemoryStream Decompress(CreateStreamDelegate sc, Stream source)
		{
            if (ZLibNative.Loaded == false)
                ZLibNative.AssemblyInit();

            MemoryStream result = new MemoryStream();
			Decompress(sc, source, result);
			result.Position = 0;
			return result;
		}

		public static byte[] Compress(CreateStreamDelegate sc, byte[] source)
		{
            if (ZLibNative.Loaded == false)
                ZLibNative.AssemblyInit();

            using (MemoryStream srcStream = new MemoryStream(source))
            using (MemoryStream dstStream = Compress(sc, srcStream))
            {
                return dstStream.ToArray();
            }
		}

		public static byte[] Decompress(CreateStreamDelegate sc, byte[] source)
		{
            if (ZLibNative.Loaded == false)
                ZLibNative.AssemblyInit();

            using (MemoryStream srcStream = new MemoryStream(source))
            using (MemoryStream dstStream = Decompress(sc, srcStream))
            {
                return dstStream.ToArray();
            }
		}
	}
    #endregion
}
