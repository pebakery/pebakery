using BenchmarkDotNet.Attributes;
using Joveler.Compression.LZ4;
using Joveler.Compression.XZ;
using Joveler.Compression.ZLib;
using K4os.Compression.LZ4.Streams;
using System;
using System.Collections.Generic;
using System.IO;

namespace Benchmark
{
    public class DecompMgcBench
    {
        private string _binaryDir = string.Empty;
        private string _sampleBaseDir = string.Empty;
        private string _sampleDir = string.Empty;
        private long _magicFileLen;

        // SrcFiles
        public enum DecompMethod
        {
            NativeGzip,
            NativeXZ,
            NativeLZ4,
            ManagedLZ4,
        }

        public IReadOnlyList<DecompMethod> DecompMethods { get; set; } = (DecompMethod[])Enum.GetValues(typeof(DecompMethod));

        private readonly Dictionary<DecompMethod, string> _srcFileNameDict = new Dictionary<DecompMethod, string>()
        {
            [DecompMethod.NativeGzip] = "magic.mgc.gz",
            [DecompMethod.NativeXZ] = "magic.mgc.xz",
            [DecompMethod.NativeLZ4] = "magic.mgc.lz4",
            [DecompMethod.ManagedLZ4] = "magic.mgc.lz4",
        };
        private readonly Dictionary<DecompMethod, byte[]> _srcFileBytesDict = new Dictionary<DecompMethod, byte[]>();

        #region Setup and Cleanup
        [GlobalSetup]
        public void GlobalSetup()
        {
            Program.NativeGlobalInit();
            _binaryDir = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
            _sampleBaseDir = Path.GetFullPath(Path.Combine(_binaryDir, "..", "..", "..", "..", "..", "..", "..", "Samples"));
            _sampleDir = Path.Combine(_sampleBaseDir, "DecompMgc");

            string magicFile = Path.Combine(_binaryDir, "magic.mgc");
            FileInfo fi = new FileInfo(magicFile);
            _magicFileLen = fi.Length;

            foreach (DecompMethod method in DecompMethods)
            {
                string srcFile = Path.Combine(_sampleDir, _srcFileNameDict[method]);
                byte[] buffer;
                using (FileStream fs = new FileStream(srcFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    buffer = new byte[fs.Length];
                    fs.Read(buffer, 0, buffer.Length);
                }
                _srcFileBytesDict[method] = buffer;
            }
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            Program.NativeGlobalCleanup();
        }
        #endregion

        #region Benchmark
        [Benchmark]
        public void NativeGZip()
        {
            byte[] buffer = _srcFileBytesDict[DecompMethod.NativeGzip];

            using (MemoryStream decompMs = new MemoryStream((int)_magicFileLen))
            using (MemoryStream compMs = new MemoryStream(buffer))
            using (GZipStream gs = new GZipStream(compMs, new ZLibDecompressOptions()))
            {
                gs.CopyTo(decompMs);
            }
        }

        [Benchmark]
        public void NativeXZ()
        {
            byte[] buffer = _srcFileBytesDict[DecompMethod.NativeXZ];

            using (MemoryStream decompMs = new MemoryStream((int)_magicFileLen))
            using (MemoryStream compMs = new MemoryStream(buffer))
            using (XZStream xzs = new XZStream(compMs, new XZDecompressOptions()))
            {
                xzs.CopyTo(decompMs);
            }
        }

        [Benchmark]
        public void NativeLZ4()
        {
            byte[] buffer = _srcFileBytesDict[DecompMethod.NativeLZ4];

            using (MemoryStream decompMs = new MemoryStream((int)_magicFileLen))
            using (MemoryStream compMs = new MemoryStream(buffer))
            {
                using (LZ4FrameStream ls = new LZ4FrameStream(compMs, new LZ4FrameDecompressOptions()))
                {
                    ls.CopyTo(decompMs);
                }
            }
        }

        [Benchmark]
        public void ManagedLZ4()
        {
            byte[] buffer = _srcFileBytesDict[DecompMethod.ManagedLZ4];

            using (MemoryStream decompMs = new MemoryStream((int)_magicFileLen))
            using (MemoryStream compMs = new MemoryStream(buffer))
            using (LZ4DecoderStream ls = LZ4Stream.Decode(compMs))
            {
                ls.CopyTo(decompMs);
            }
        }
        #endregion
    }
}
