using BenchmarkDotNet.Attributes;
using Joveler.FileMagician;
using PEBakery.Helper.ThirdParty;
using System;
using System.Collections.Generic;
using System.IO;
using UtfUnknown;

namespace Benchmark
{
    public class EncDetectBench
    {
        private string _binaryDir;
        private string _sampleBaseDir;
        private string _sampleDir;
        private string _magicFile;
        private Magic _magic;
        private AdvTextEncDetect _autoitDetect;

        // SrcFiles
        [ParamsSource(nameof(SrcFileNames))]
        public string SrcFileName { get; set; }
        public IReadOnlyList<string> SrcFileNames { get; set; } = new string[]
        {
            "Banner.7z",
            "Banner.svg",
            "Banner.zip",
            "CP949.txt",
            "Random.bin",
            "ShiftJIS.html",
            "Type3.pdf",
            "UTF16BE.txt",
            "UTF16LE.txt",
            "UTF8.txt",
            "UTF8woBOM.txt",
            "Zero.bin",
        };
        public Dictionary<string, byte[]> SrcFiles = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        // BufferSize
        [ParamsSource(nameof(BufferSizes))]
        public int BufferSize { get; set; }
        public IReadOnlyList<int> BufferSizes { get; set; } = new int[]
        {
            4 * 1024,
            16 * 1024,
            64 * 1024,
            256 * 1024,
        };

        #region Setup and Cleanup
        [GlobalSetup]
        public void GlobalSetup()
        {
            Program.NativeGlobalInit();
            _binaryDir = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
            _sampleBaseDir = Path.GetFullPath(Path.Combine(_binaryDir, "..", "..", "..", "..", "..", "..", "..", "Samples"));

            _sampleDir = Path.Combine(_sampleBaseDir, "EncDetect");
            _magicFile = Path.Combine(_binaryDir, "magic.mgc");
            _magic = Magic.Open(_magicFile);
            _autoitDetect = new AdvTextEncDetect();

            foreach (string srcFileName in SrcFileNames)
            {
                string srcFile = Path.Combine(_sampleDir, srcFileName);
                using (MemoryStream ms = new MemoryStream())
                {
                    using (FileStream fs = new FileStream(srcFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        fs.CopyTo(ms);
                    }

                    SrcFiles[srcFileName] = ms.ToArray();
                }
            }
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            _magic.Dispose();
            Program.NativeGlobalCleanup();
        }
        #endregion

        #region Benchmark
        [Benchmark]
        public void FileMagician()
        {
            byte[] rawData = SrcFiles[SrcFileName];
            DetectFileMagician(rawData, BufferSize);
        }

        [Benchmark]
        public void AutoIt()
        {
            byte[] rawData = SrcFiles[SrcFileName];
            DetectAutoIt(rawData, BufferSize);
        }

        [Benchmark]
        public void UtfUnknown()
        {
            byte[] rawData = SrcFiles[SrcFileName];
            DetectUtfUnknown(rawData, BufferSize);
        }
        #endregion

        #region Real Code
        public enum TextType
        {
            Binary,
            Ansi,
            Utf16le,
            Utf16be,
            Utf8,
        }

        public TextType DetectFileMagician(ReadOnlySpan<byte> rawData, int sizeLimit)
        {
            // "utf-16be", "utf-16le", "utf-8", "us-ascii"/"iso-8859-1"/"unknown-8bit" - "text/plain", "text/html"
            _magic.SetFlags(MagicFlags.MIME_TYPE);
            string mimeType = _magic.CheckBuffer(rawData.Slice(0, sizeLimit));

            if (!mimeType.StartsWith("text/", StringComparison.Ordinal))
                return TextType.Binary;

            _magic.SetFlags(MagicFlags.MIME_ENCODING);
            string mimeEnc = _magic.CheckBuffer(rawData);

            TextType type;
            if (mimeEnc.Equals("utf-8", StringComparison.Ordinal))
                type = TextType.Utf8;
            else if (mimeEnc.Equals("utf-16le", StringComparison.Ordinal))
                type = TextType.Utf16le;
            else if (mimeEnc.Equals("utf-16be", StringComparison.Ordinal))
                type = TextType.Utf16be;
            else
                type = TextType.Ansi;
            return type;
        }

        public TextEncoding DetectAutoIt(ReadOnlySpan<byte> rawData, int sizeLimit)
        {
            return _autoitDetect.DetectEncoding(rawData.Slice(0, sizeLimit));
        }

        public DetectionResult DetectUtfUnknown(byte[] rawData, int sizeLimit)
        {
            using (MemoryStream ms = new MemoryStream(rawData, 0, Math.Min(sizeLimit, rawData.Length)))
            {
                return CharsetDetector.DetectFromStream(ms);
            }
        }
        #endregion
    }
}
