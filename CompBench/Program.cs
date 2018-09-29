using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Joveler.ZLibWrapper;
using PEBakery.LZ4Lib;
using PEBakery.XZLib;
using System;
using System.Collections.Generic;
using System.IO;
using K4os.Compression.LZ4;
using K4os.Compression.LZ4.Streams;

namespace CompBench
{
    #region CompBench
    public class CompBench
    {
        private string _sampleDir;
        private string _destDir;

        public double CompRatio { get; set; }

        // SrcFiles
        [ParamsSource(nameof(SrcFileNames))]
        public string SrcFileName { get; set; }
        public string[] SrcFileNames { get; set; } = new string[3]
        {
            "Banner.bmp",
            "Banner.svg",
            "Type4.txt",
        };
        public Dictionary<string, byte[]> SrcFiles = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        // Levels
        [ParamsSource(nameof(Levels))]
        public string Level { get; set; }
        public string[] Levels { get; set; } = new string[3]
        {
            "Fastest",
            "Default",
            "Best",
        };

        // ZLibCompLevel
        public Dictionary<string, ZLibCompLevel> NativeZLibLevelDict = new Dictionary<string, ZLibCompLevel>(StringComparer.Ordinal)
        {
            ["Fastest"] = ZLibCompLevel.BestSpeed,
            ["Default"] = ZLibCompLevel.Default,
            ["Best"] = ZLibCompLevel.BestCompression,
        };

        public Dictionary<string, SharpCompress.Compressors.Deflate.CompressionLevel> ManagedZLibLevelDict = new Dictionary<string, SharpCompress.Compressors.Deflate.CompressionLevel>(StringComparer.Ordinal)
        {
            ["Fastest"] = SharpCompress.Compressors.Deflate.CompressionLevel.BestSpeed,
            ["Default"] = SharpCompress.Compressors.Deflate.CompressionLevel.Default,
            ["Best"] = SharpCompress.Compressors.Deflate.CompressionLevel.BestCompression,
        };

        // XZPreset
        public Dictionary<string, uint> XZPresetDict = new Dictionary<string, uint>(StringComparer.Ordinal)
        {
            ["Fastest"] = XZStream.MinimumPreset,
            ["Default"] = XZStream.DefaultPreset,
            ["Best"] = XZStream.MaximumPreset,
        };

        // LZ4CompLevel
        public Dictionary<string, LZ4CompLevel> NativeLZ4LevelDict = new Dictionary<string, LZ4CompLevel>(StringComparer.Ordinal)
        {
            ["Fastest"] = LZ4CompLevel.Fast,
            ["Default"] = LZ4CompLevel.High,
            ["Best"] = LZ4CompLevel.VeryHigh, // LZ4-HC
        };

        // ManagedLZ4CompLevel
        public Dictionary<string, LZ4Level> ManagedLZ4LevelDict = new Dictionary<string, LZ4Level>(StringComparer.Ordinal)
        {
            ["Fastest"] = LZ4Level.L00_FAST,
            ["Default"] = LZ4Level.L09_HC,
            ["Best"] = LZ4Level.L12_MAX, // LZ4-HC
        };

        [GlobalSetup]
        public void GlobalSetup()
        {
            Program.NativeGlobalInit();

            _sampleDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Samples"));

            _destDir = Path.GetTempFileName();
            File.Delete(_destDir);
            Directory.CreateDirectory(_destDir);

            foreach (string srcFileName in SrcFileNames)
            {
                string srcFile = Path.Combine(_sampleDir, "Raw", srcFileName);
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
            if (Directory.Exists(_destDir))
                Directory.Delete(_destDir);

            Program.NativeGlobalCleanup();
        }

        [Benchmark]
        public double Native_LZ4()
        {
            long compLen;
            byte[] rawData = SrcFiles[SrcFileName];
            using (MemoryStream ms = new MemoryStream())
            {
                using (MemoryStream rms = new MemoryStream(rawData))
                using (LZ4FrameStream lzs = new LZ4FrameStream(ms, LZ4Mode.Compress, NativeLZ4LevelDict[Level], true))
                {
                    rms.CopyTo(lzs);
                }

                ms.Flush();
                compLen = ms.Position;
            }

            CompRatio = (double)compLen / rawData.Length;
            return CompRatio;
        }

        [Benchmark]
        public double Managed_LZ4()
        {
            long compLen;
            byte[] rawData = SrcFiles[SrcFileName];
            using (MemoryStream ms = new MemoryStream())
            {
                using (MemoryStream rms = new MemoryStream(rawData))
                using (LZ4EncoderStream lzs = LZ4Stream.Encode(ms, ManagedLZ4LevelDict[Level], 0, true))
                {
                    rms.CopyTo(lzs);
                }

                ms.Flush();
                compLen = ms.Position;
            }

            CompRatio = (double)compLen / rawData.Length;
            return CompRatio;
        }

        [Benchmark]
        public double Native_ZLib()
        {
            long compLen;
            byte[] rawData = SrcFiles[SrcFileName];
            using (MemoryStream ms = new MemoryStream())
            {
                using (MemoryStream rms = new MemoryStream(rawData))
                using (ZLibStream zs = new ZLibStream(ms, ZLibMode.Compress, NativeZLibLevelDict[Level], true))
                {
                    rms.CopyTo(zs);
                }

                ms.Flush();
                compLen = ms.Position;
            }

            CompRatio = (double)compLen / rawData.Length;
            return CompRatio;
        }

        [Benchmark]
        public double Managed_ZLib()
        {
            long compLen;
            byte[] rawData = SrcFiles[SrcFileName];
            using (MemoryStream ms = new MemoryStream())
            {
                using (MemoryStream rms = new MemoryStream(rawData))
                using (SharpCompress.Compressors.Deflate.ZlibStream zs = new SharpCompress.Compressors.Deflate.ZlibStream(ms, SharpCompress.Compressors.CompressionMode.Compress, ManagedZLibLevelDict[Level]))
                {
                    rms.CopyTo(zs);
                    ms.Flush();
                    compLen = ms.Position;
                }
            }

            CompRatio = (double)compLen / rawData.Length;
            return CompRatio;
        }

        [Benchmark]
        public double Native_XZ()
        {
            long compLen;
            byte[] rawData = SrcFiles[SrcFileName];
            using (MemoryStream ms = new MemoryStream())
            {
                using (MemoryStream rms = new MemoryStream(rawData))
                using (XZStream xzs = new XZStream(ms, LzmaMode.Compress, XZPresetDict[Level], true))
                {
                    rms.CopyTo(xzs);
                }

                ms.Flush();
                compLen = ms.Position;
            }

            CompRatio = (double)compLen / rawData.Length;
            return CompRatio;
        }
    }
    #endregion

    #region DecompBench
    public class DecompBench
    {
        private string _sampleDir;
        private string _destDir;

        public double CompRatio { get; set; }

        // SrcFiles
        [ParamsSource(nameof(SrcFileNames))]
        public string SrcFileName { get; set; }
        public string[] SrcFileNames { get; set; } = new string[3]
        {
            "Banner.bmp",
            "Banner.svg",
            "Type4.txt",
        };
        public Dictionary<string, byte[]> SrcFiles = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        // Levels
        [ParamsSource(nameof(Levels))]
        public string Level { get; set; }
        public string[] Levels { get; set; } = new string[3]
        {
            "Fastest",
            "Default",
            "Best",
        };

        [GlobalSetup]
        public void GlobalSetup()
        {
            Program.NativeGlobalInit();

            _sampleDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Samples"));

            _destDir = Path.GetTempFileName();
            File.Delete(_destDir);
            Directory.CreateDirectory(_destDir);

            foreach (string level in Levels)
            {
                foreach (string srcFileName in SrcFileNames)
                {
                    foreach (string ext in new string[] { ".zz", ".xz", ".lz4" })
                    {
                        string srcFile = Path.Combine(_sampleDir, level, srcFileName + ext);
                        using (MemoryStream ms = new MemoryStream())
                        {
                            using (FileStream fs = new FileStream(srcFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                            {
                                fs.CopyTo(ms);
                            }

                            SrcFiles[$"{level}_{srcFileName}{ext}"] = ms.ToArray();
                        }
                    }
                }
            }
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            if (Directory.Exists(_destDir))
                Directory.Delete(_destDir);

            Program.NativeGlobalCleanup();
        }

        [Benchmark]
        public long Native_LZ4()
        {
            byte[] compData = SrcFiles[$"{Level}_{SrcFileName}.lz4"];
            using (MemoryStream ms = new MemoryStream())
            {
                using (MemoryStream rms = new MemoryStream(compData))
                using (LZ4FrameStream zs = new LZ4FrameStream(rms, LZ4Mode.Decompress))
                {
                    zs.CopyTo(ms);
                }

                ms.Flush();
                return ms.Length;
            }
        }

        [Benchmark]
        public long Managed_LZ4()
        {
            byte[] compData = SrcFiles[$"{Level}_{SrcFileName}.lz4"];
            using (MemoryStream ms = new MemoryStream())
            {
                using (MemoryStream rms = new MemoryStream(compData))
                using (LZ4DecoderStream zs = LZ4Stream.Decode(rms, 0))
                {
                    zs.CopyTo(ms);
                }

                ms.Flush();
                return ms.Length;
            }
        }

        [Benchmark]
        public long Native_ZLib()
        {
            byte[] compData = SrcFiles[$"{Level}_{SrcFileName}.zz"];
            using (MemoryStream ms = new MemoryStream())
            {
                using (MemoryStream rms = new MemoryStream(compData))
                using (ZLibStream zs = new ZLibStream(rms, ZLibMode.Decompress))
                {
                    zs.CopyTo(ms);
                }

                ms.Flush();
                return ms.Length;
            }
        }

        [Benchmark]
        public long Managed_ZLib()
        {
            byte[] compData = SrcFiles[$"{Level}_{SrcFileName}.zz"];
            using (MemoryStream ms = new MemoryStream())
            {
                using (MemoryStream rms = new MemoryStream(compData))
                using (SharpCompress.Compressors.Deflate.ZlibStream zs = new SharpCompress.Compressors.Deflate.ZlibStream(rms, SharpCompress.Compressors.CompressionMode.Decompress))
                {
                    zs.CopyTo(ms);
                }

                ms.Flush();
                return ms.Length;
            }
        }

        [Benchmark]
        public long Native_XZ()
        {
            byte[] compData = SrcFiles[$"{Level}_{SrcFileName}.xz"];
            using (MemoryStream ms = new MemoryStream())
            {
                using (MemoryStream rms = new MemoryStream(compData))
                using (XZStream zs = new XZStream(rms, LzmaMode.Decompress))
                {
                    zs.CopyTo(ms);
                }

                ms.Flush();
                return ms.Length;
            }
        }

        [Benchmark]
        public long Managed_XZ()
        {
            byte[] compData = SrcFiles[$"{Level}_{SrcFileName}.xz"];
            using (MemoryStream ms = new MemoryStream())
            {
                using (MemoryStream rms = new MemoryStream(compData))
                using (SharpCompress.Compressors.Xz.XZStream zs = new SharpCompress.Compressors.Xz.XZStream(rms))
                {
                    zs.CopyTo(ms);
                }

                ms.Flush();
                return ms.Length;
            }
        }
    }
    #endregion

    #region Program
    public class Program
    {
        public static void NativeGlobalInit()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string arch = IntPtr.Size == 8 ? "x64" : "x86";

            string zLibDllPath = Path.Combine(baseDir, arch, "zlibwapi.dll");
            string xzDllPath = Path.Combine(baseDir, arch, "liblzma.dll");
            string lz4DllPath = Path.Combine(baseDir, arch, "liblz4.so.1.8.2.dll");

            ZLibInit.GlobalInit(zLibDllPath, 64 * 1024);
            XZStream.GlobalInit(xzDllPath, 64 * 1024);
            LZ4FrameStream.GlobalInit(lz4DllPath);
        }

        public static void NativeGlobalCleanup()
        {
            ZLibInit.GlobalCleanup();
            XZStream.GlobalCleanup();
            LZ4FrameStream.GlobalCleanup();
        }

        public static void Main(string[] args)
        {
            BenchmarkRunner.Run<CompBench>();
            BenchmarkRunner.Run<DecompBench>();
        }
    }
    #endregion
}
