using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using CommandLine;
using Joveler.Compression.LZ4;
using Joveler.Compression.XZ;
using Joveler.Compression.ZLib;
using Joveler.FileMagician;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Benchmark
{
    #region Parameter
    public abstract class ParamOptions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Cast<T>() where T : ParamOptions
        {
            T cast = this as T;
            Debug.Assert(cast != null);
            return cast;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Cast<T>(ParamOptions opts) where T : ParamOptions
        {
            return opts.Cast<T>();
        }
    }

    [Verb("all", HelpText = "Benchmark all")]
    public class AllBenchOptions : ParamOptions { }

    [Verb("enc-detect", HelpText = "Benchmark encoding detection")]
    public class EncDetectBenchOptions : ParamOptions { }

    [Verb("decomp-mgc", HelpText = "Benchmark magic.mgc decompression")]
    public class DecompMgcBenchOptions : ParamOptions { }
    #endregion

    #region Program
    public static class Program
    {
        #region PrintErrorAndExit
        internal static void PrintErrorAndExit(IEnumerable<Error> errs)
        {
            foreach (Error err in errs)
                Console.WriteLine(err.ToString());
            Environment.Exit(1);
        }
        #endregion

        #region Init and Cleanup
        public static void NativeGlobalInit()
        {
            string arch = null;
            switch (RuntimeInformation.OSArchitecture)
            {
                case Architecture.X86:
                    arch = "x86";
                    break;
                case Architecture.X64:
                    arch = "x64";
                    break;
                case Architecture.Arm:
                    arch = "armhf";
                    break;
                case Architecture.Arm64:
                    arch = "arm64";
                    break;
            }

            string magicLibPath, gzipLibPath, xzLibPath, lz4LibPath;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                magicLibPath = Path.Combine(arch, "libmagic-1.dll");
                gzipLibPath = Path.Combine(arch, "zlibwapi.dll");
                xzLibPath = Path.Combine(arch, "liblzma.dll");
                lz4LibPath = Path.Combine(arch, "liblz4.dll");
            }
            else
            {
                throw new PlatformNotSupportedException();
            }

            static void CheckLibPath(string libPath)
            {
                if (libPath == null || !File.Exists(libPath))
                    throw new PlatformNotSupportedException();
            }

            CheckLibPath(magicLibPath);
            CheckLibPath(gzipLibPath);
            CheckLibPath(xzLibPath);
            CheckLibPath(lz4LibPath);

            Magic.GlobalInit(magicLibPath);
            ZLibInit.GlobalInit(gzipLibPath);
            XZInit.GlobalInit(xzLibPath);
            LZ4Init.GlobalInit(lz4LibPath);
        }

        public static void NativeGlobalCleanup()
        {
            Magic.GlobalCleanup();
            ZLibInit.GlobalCleanup();
            XZInit.GlobalCleanup();
            LZ4Init.GlobalCleanup();
        }
        #endregion

        #region Main
        public static void Main(string[] args)
        {
            ParamOptions opts = null;
            Parser argParser = new Parser(conf =>
            {
                conf.HelpWriter = Console.Out;
                conf.CaseInsensitiveEnumValues = true;
                conf.CaseSensitive = false;
            });

            argParser.ParseArguments<AllBenchOptions,
                EncDetectBenchOptions, DecompMgcBenchOptions>(args)
                .WithParsed<AllBenchOptions>(x => opts = x)
                .WithParsed<EncDetectBenchOptions>(x => opts = x)
                .WithParsed<DecompMgcBenchOptions>(x => opts = x)
                .WithNotParsed(PrintErrorAndExit);
            Debug.Assert(opts != null, $"{nameof(opts)} != null");

            bool encDetectBench = false;
            bool decompMgcBench = false;
            switch (opts)
            {
                case EncDetectBenchOptions _:
                    Console.WriteLine("EncDetect");
                    encDetectBench = true;
                    break;
                case DecompMgcBenchOptions _:
                    Console.WriteLine("DecompMgc");
                    decompMgcBench = true;
                    break;
                case AllBenchOptions _:
                    encDetectBench = true;
                    decompMgcBench = true;
                    break;
                default:
                    Console.WriteLine("Please specify proper benchmarks.");
                    Environment.Exit(1);
                    break;
            }

            // IConfig config = DefaultConfig.Instance.With(ConfigOptions.DisableOptimizationsValidator);
            IConfig config = DefaultConfig.Instance;
            if (decompMgcBench)
                BenchmarkRunner.Run<DecompMgcBench>(config);
            if (encDetectBench)
                BenchmarkRunner.Run<EncDetectBench>(config);
        }
        #endregion
    }
    #endregion
}
