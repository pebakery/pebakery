using BenchmarkDotNet.Running;
using CommandLine;
using CommandLine.Text;
using Joveler.FileMagician;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

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

            string libPath = null;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                libPath = Path.Combine(arch, "libmagic-1.dll");
            else
                throw new PlatformNotSupportedException();

            if (libPath == null || !File.Exists(libPath))
                throw new PlatformNotSupportedException();

            Magic.GlobalInit(libPath);
        }

        public static void NativeGlobalCleanup()
        {
            Magic.GlobalCleanup();
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
                EncDetectBenchOptions>(args)
                .WithParsed<AllBenchOptions>(x => opts = x)
                .WithParsed<EncDetectBenchOptions>(x => opts = x)
                .WithNotParsed(PrintErrorAndExit);
            Debug.Assert(opts != null, $"{nameof(opts)} != null");

            switch (opts)
            {
                case AllBenchOptions _:
                    BenchmarkRunner.Run<EncDetectBench>();
                    break;
                case EncDetectBenchOptions _:
                    BenchmarkRunner.Run<EncDetectBench>();
                    break;
            }
        }
        #endregion
    }
    #endregion
}
