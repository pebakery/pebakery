/*
    Copyright (C) 2016-2019 Hajin Jang
    Licensed under MIT License.
 
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
using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
// ReSharper disable InconsistentNaming

namespace PEBakery.Helper
{
    #region HashHelper
    public static class HashHelper
    {
        #region Fields
        public enum HashType { None, MD5, SHA1, SHA256, SHA384, SHA512 }
        public static readonly ReadOnlyDictionary<HashType, int> HashLenDict = new ReadOnlyDictionary<HashType, int>
        (
            new Dictionary<HashType, int>
            {
                [HashType.MD5] = 128 / 8,
                [HashType.SHA1] = 160 / 8,
                [HashType.SHA256] = 256 / 8,
                [HashType.SHA384] = 384 / 8,
                [HashType.SHA512] = 512 / 8,
            }
        );

        private const int BufferSize = 64 * 1024; // 64KB
        #endregion

        #region CalcHash
        public static byte[] GetHash(HashType type, byte[] input)
        {
            return GetHash(type, input, 0, null);
        }

        public static byte[] GetHash(HashType type, byte[] input, int reportInterval, IProgress<long> progress)
        {
            HashAlgorithm hash = null;
            try
            {
                switch (type)
                {
                    case HashType.MD5:
                        hash = MD5.Create();
                        break;
                    case HashType.SHA1:
                        hash = SHA1.Create();
                        break;
                    case HashType.SHA256:
                        hash = SHA256.Create();
                        break;
                    case HashType.SHA384:
                        hash = SHA384.Create();
                        break;
                    case HashType.SHA512:
                        hash = SHA512.Create();
                        break;
                    default:
                        throw new InvalidOperationException("Invalid Hash Type");
                }

                // No progress report
                if (reportInterval <= 0 || progress == null)
                    return hash.ComputeHash(input);

                // With progress report
                int offset = 0;
                while (offset < input.Length)
                {
                    if (offset + reportInterval < input.Length)
                    {
                        hash.TransformBlock(input, offset, reportInterval, input, offset);
                        offset += reportInterval;
                    }
                    else // Last run
                    {
                        int bytesRead = input.Length - offset;
                        hash.TransformFinalBlock(input, offset, bytesRead);
                        offset += bytesRead;
                    }

                    progress.Report(offset);
                }
                return hash.Hash;
            }
            finally
            {
                if (hash != null)
                    hash.Dispose();
            }
        }

        public static byte[] GetHash(HashType type, Stream stream)
        {
            return GetHash(type, stream, 0, null);
        }

        public static byte[] GetHash(HashType type, Stream stream, long reportInterval, IProgress<long> progress)
        {
            HashAlgorithm hash = null;
            try
            {
                switch (type)
                {
                    case HashType.MD5:
                        hash = MD5.Create();
                        break;
                    case HashType.SHA1:
                        hash = SHA1.Create();
                        break;
                    case HashType.SHA256:
                        hash = SHA256.Create();
                        break;
                    case HashType.SHA384:
                        hash = SHA384.Create();
                        break;
                    case HashType.SHA512:
                        hash = SHA512.Create();
                        break;
                    default:
                        throw new InvalidOperationException("Invalid Hash Type");
                }

                // No progress report
                if (reportInterval <= 0 || progress == null)
                    return hash.ComputeHash(stream);

                // With progress report
                long nextReport = reportInterval;
                long offset = stream.Position;
                byte[] buffer = new byte[BufferSize];
                int bytesRead;
                do
                {
                    bytesRead = stream.Read(buffer, 0, buffer.Length);
                    hash.TransformBlock(buffer, 0, bytesRead, buffer, 0);
                    offset += bytesRead;
                    if (nextReport <= offset)
                    {
                        progress.Report(offset);
                        nextReport += reportInterval;
                    }
                }
                while (0 < bytesRead);

                hash.TransformFinalBlock(buffer, 0, 0);
                return hash.Hash;
            }
            finally
            {
                if (hash != null)
                    hash.Dispose();
            }
        }
        #endregion

        #region DetectHashType
        public static HashType DetectHashType(ReadOnlySpan<byte> data)
        {
            return InternalDetectHashType(data.Length);
        }

        public static HashType DetectHashType(string hexStr)
        {
            if (StringHelper.IsHex(hexStr))
                return HashType.None;
            if (!NumberHelper.ParseHexStringToBytes(hexStr, out byte[] hashByte))
                return HashType.None;

            return InternalDetectHashType(hashByte.Length);
        }

        private static HashType InternalDetectHashType(int length)
        {
            foreach (var kv in HashLenDict)
            {
                if (length == kv.Value)
                    return kv.Key;
            }
            throw new InvalidOperationException("Cannot recognize valid hash string");
        }
        #endregion

        #region GetHashByteLen
        public static int GetHashByteLen(HashType hashType)
        {
            switch (hashType)
            {
                case HashType.MD5:
                case HashType.SHA1:
                case HashType.SHA256:
                case HashType.SHA384:
                case HashType.SHA512:
                    return HashLenDict[hashType];
                default:
                    throw new ArgumentException($"Wrong HashType [{hashType}]");
            }
        }
        #endregion

        #region ParseHashType
        public static HashType ParseHashType(string str)
        {
            HashType hashType;
            if (str.Equals("MD5", StringComparison.OrdinalIgnoreCase))
                hashType = HashType.MD5;
            else if (str.Equals("SHA1", StringComparison.OrdinalIgnoreCase))
                hashType = HashType.SHA1;
            else if (str.Equals("SHA256", StringComparison.OrdinalIgnoreCase))
                hashType = HashType.SHA256;
            else if (str.Equals("SHA384", StringComparison.OrdinalIgnoreCase))
                hashType = HashType.SHA384;
            else if (str.Equals("SHA512", StringComparison.OrdinalIgnoreCase))
                hashType = HashType.SHA512;
            else
                throw new ArgumentException($"Wrong HashType [{str}]");
            return hashType;
        }
        #endregion
    }
    #endregion
}
