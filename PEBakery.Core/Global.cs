﻿/*
    Copyright (C) 2018-2023 Hajin Jang
    Licensed under GPL 3.0
 
    PEBakery is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.

    Additional permission under GNU GPL version 3 section 7

    If you modify this program, or any covered work, by linking
    or combining it with external libraries, containing parts
    covered by the terms of various license, the licensors of
    this program grant you additional permission to convey the
    resulting work. An external library is a library which is
    not derived from or based on this program. 
*/

using Microsoft.IO;
using PEBakery.Core.Arguments;
using PEBakery.Core.ViewModels;
using PEBakery.Helper;
using SQLite;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PEBakery.Core
{
    #region Global
    public static class Global
    {
        #region Constants
        public static class Const
        {
            public const string ScriptCacheRevision = "r23";
            public const int EngineVersion = 100;
            public const string ProgramVersionStr = "1.1.1";
            public const string ProgramVersionStrFull = "1.1.1";

            private static readonly VersionEx? _programVersionInst = VersionEx.Parse(ProgramVersionStr);
            public static VersionEx ProgramVersionInst
            {
                get
                {
                    if (_programVersionInst == null)
                        throw new InternalException($"{nameof(_programVersionInst)} is null");
                    return _programVersionInst;
                }
            }

            // Update json version
            public const string UpdateSchemaMaxVerStr = "0.1.1";
            public const string UpdateSchemaMinVerStr = "0.1.1";
            private static readonly VersionEx? _updateSchemaMaxVerInst = VersionEx.Parse(UpdateSchemaMaxVerStr);
            public static VersionEx UpdateSchemaMaxVerInst
            {
                get
                {
                    if (_updateSchemaMaxVerInst == null)
                        throw new InternalException($"{nameof(_updateSchemaMaxVerInst)} is null");
                    return _updateSchemaMaxVerInst;
                }
            }
            private static readonly VersionEx? _updateSchemaMinVerInst = VersionEx.Parse(UpdateSchemaMinVerStr);
            public static VersionEx UpdateSchemaMinVerInst
            {
                get
                {
                    if (_updateSchemaMinVerInst == null)
                        throw new InternalException($"{nameof(_updateSchemaMinVerInst)} is null");
                    return _updateSchemaMinVerInst;
                }
            }
        }
        #endregion

        #region Fields and Properties
        // Build-time constant
        public static DateTime BuildDate { get; set; }

        // Start-time variables
        public static string[] Args { get; set; } = Array.Empty<string>();
        public static string BaseDir { get; set; } = string.Empty;

        // Buffer Pool
        private static readonly RecyclableMemoryStreamManager _recyclableMemoryStreamManager = new RecyclableMemoryStreamManager();
        public static RecyclableMemoryStreamManager MemoryStreamManager => _recyclableMemoryStreamManager;

        // Global Instances
        private static Logger? _logger;
        public static Logger Logger
        {
            get
            {
                if (_logger == null)
                    throw new InvalidOperationException($"{nameof(_logger)} is null");
                return _logger;
            }
            set => _logger = value;
        }
        public static MainViewModel MainViewModel { get; set; } = new MainViewModel();
        private static Setting? _setting;
        public static Setting Setting
        {
            get
            {
                if (_setting == null)
                    throw new InvalidOperationException($"{nameof(_setting)} is null");
                return _setting;
            }
            set => _setting = value;
        }
        public static ProjectCollection? Projects { get; private set; }
        public static ScriptCache? ScriptCache { get; private set; }

        // FileTypeDetector / LibMagic
        public static string? MagicFile { get; set; }
        private static readonly object _fileTypeDetectorLock = new object();
        private static FileTypeDetector? _fileTypeDetector;
        public static FileTypeDetector FileTypeDetector
        {
            get
            {
                // Wait until _fileTypeDetector is loaded
                while (true)
                {
                    lock (_fileTypeDetectorLock)
                    {
                        if (_fileTypeDetector != null)
                            break;
                    }
                    Task.Delay(TimeSpan.FromMilliseconds(50)).Wait();
                }

                return _fileTypeDetector;
            }
        }
        #endregion

        #region Init
        /// <summary>
        /// Launch before Application.Current is loaded
        /// </summary>
        /// <param name="args">Command-line argument</param>
        /// <param name="initMainViewModel">Set this to true when PEBakery.Core is used outside of PEBakery</param>
        public static void PreInit(string[] args, bool initMainViewModel)
        {
            // Process arguments
            Args = args;

            // Regsiter Non-Unicode Encodings
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            // Load BuildDate
            BuildDate = BuildTimestamp.ReadDateTime();

            // Initialize native libraries
            NativeGlobalInit(AppDomain.CurrentDomain.BaseDirectory);

            // Create MainViewModel
            if (initMainViewModel)
                MainViewModel = new MainViewModel();
        }

        /// <summary>
        /// Launch after Application.Current is loaded
        /// </summary>
        public static void Init()
        {
            // Set fallback BaseDir
            string baseDir = BaseDir = Environment.CurrentDirectory;

            // Setup unhandled exception handler (requires BaseDir to be set)
            AppDomain.CurrentDomain.UnhandledException += AppDomain_UnhandledException;

            // Run ArgumentParser
            ArgumentParser argParser = new ArgumentParser();
            PEBakeryOptions? opts = argParser.Parse(Args);
            if (opts == null) // Arguments parse fail
                Environment.Exit(1); // Force Shutdown

            // Setup BaseDir
            if (opts.BaseDir != null)
            {
                baseDir = Path.GetFullPath(opts.BaseDir);
                if (Directory.Exists(baseDir) == false)
                {
                    MessageBox.Show($"Directory [{baseDir}] does not exist.\r\nRun [PEBkaery --help] for commnad line help message.", "PEBakery CommandLine Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Environment.Exit(1); // Force Shutdown
                }
                Environment.CurrentDirectory = BaseDir = baseDir;
            }

            // Database directory
            string dbDir = Path.Combine(BaseDir, "Database");
            if (!Directory.Exists(dbDir))
                Directory.CreateDirectory(dbDir);

            // Log Database
            string logDbFile = Path.Combine(dbDir, "PEBakeryLog.db");
            try
            {
                Logger = new Logger(logDbFile);
                Logger.SystemWrite(new LogInfo(LogState.Info, "PEBakery launched"));
            }
            catch (SQLiteException)
            { // Update failure -> retry with clearing existing log db
                File.Delete(logDbFile);
                try
                {
                    Logger = new Logger(logDbFile);
                    Logger.SystemWrite(new LogInfo(LogState.Info, "PEBakery launched, log cleared due to an error"));
                }
                catch (SQLiteException e)
                { // Unable to continue -> raise an error message
                    string msg = $"SQLite Error : {e.Message}\r\n\r\nThe Log database is corrupted and was not able to be repaired.\r\nPlease delete {dbDir}\\PEBakeryLog.db and restart.";
                    MessageBox.Show(msg, "SQLite Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                    if (Application.Current != null)
                        Application.Current.Shutdown(1);
                    else
                        Environment.Exit(1);
                }
            }

            // Init ProjectCollection
            Projects = new ProjectCollection(BaseDir);

            // Setting File
            string settingFile = Path.Combine(BaseDir, "PEBakery.ini");
            Setting = new Setting(settingFile);
            Setting.ApplySetting();

            // Custom Title
            if (Setting.Interface.UseCustomTitle && MainViewModel != null)
                MainViewModel.TitleBar = Setting.Interface.CustomTitle;

            // Init script cache DB, regardless of Setting.Script.EnableCache
            string cacheDbFile = Path.Combine(dbDir, "PEBakeryCache.db");
            try
            {
                ScriptCache = new ScriptCache(cacheDbFile);
                int cachedScriptCount = ScriptCache.Table<CacheModel.ScriptCache>().Count();

                if (Setting.Script.EnableCache)
                    Logger.SystemWrite(new LogInfo(LogState.Info, $"ScriptCache enabled, {cachedScriptCount} cached scripts found"));
                else
                    Logger.SystemWrite(new LogInfo(LogState.Info, "ScriptCache disabled"));
            }
            catch (SQLiteException)
            { // Load failure -> Fallback, delete and remake database
                File.Delete(cacheDbFile);
                try
                {
                    ScriptCache = new ScriptCache(cacheDbFile);
                    Logger.SystemWrite(new LogInfo(LogState.Info, $"ScriptCache enabled, cache cleared due to an error"));
                }
                catch (SQLiteException e)
                { // Unable to continue -> raise an error message
                    string msg = $"SQLite Error : {e.Message}\r\n\r\nThe Cache database is corrupted and was not able to be repaired.\r\nPlease delete {dbDir}\\PEBakeryCache.db and restart.";
                    MessageBox.Show(msg, "SQLite Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                    if (Application.Current != null)
                        Application.Current.Shutdown(1);
                    else
                        Environment.Exit(1);
                }
            }
        }
        #endregion

        #region Cleanup
        public static void Cleanup()
        {
            ScriptCache?.WaitClose();
            Logger?.Dispose();
            FileTypeDetector?.Dispose();
            NativeGlobalCleanup();

            FileHelper.CleanBaseTempDir();
        }
        #endregion

        #region Load Native Libraries
        public static void NativeGlobalInit(string baseDir)
        {
            string? magicPath = GetNativeLibraryPath(baseDir, "libmagic-1.dll");
            string? zlibPath = GetNativeLibraryPath(baseDir, "zlib1.dll");
            string? xzPath = GetNativeLibraryPath(baseDir, "liblzma.dll");
            string? wimlibPath = GetNativeLibraryPath(baseDir, "libwim-15.dll");
            string? sevenZipPath = GetNativeLibraryPath(baseDir, "7z.dll");

            try
            {
                Joveler.FileMagician.Magic.GlobalInit(magicPath);
                Joveler.Compression.ZLib.ZLibInit.GlobalInit(zlibPath, new Joveler.Compression.ZLib.ZLibInitOptions()
                {
                    IsWindowsStdcall = false,
                    IsZLibNgModernAbi = false,
                });
                Joveler.Compression.XZ.XZInit.GlobalInit(xzPath);
                ManagedWimLib.Wim.GlobalInit(wimlibPath);
                SevenZip.SevenZipBase.SetLibraryPath(sevenZipPath);
            }
            catch (Exception e)
            {
                MessageBox.Show($"Unable to load library {e.Message}", "Library Error", MessageBoxButton.OK, MessageBoxImage.Error);
                if (Application.Current != null)
                    Application.Current.Shutdown(1);
                else
                    Environment.Exit(1);
            }

            // Decompress and load magic.mgc.gz in the background
            string magicGzipFile = Path.Combine(baseDir, "magic.mgc.gz");
            LoadMagicFileAsync(magicGzipFile);
        }

        public static void NativeGlobalCleanup()
        {
            Joveler.Compression.ZLib.ZLibInit.GlobalCleanup();
            Joveler.Compression.XZ.XZInit.GlobalCleanup();
            ManagedWimLib.Wim.GlobalCleanup();
            Joveler.FileMagician.Magic.GlobalCleanup();
        }

        /// <summary>
        /// Get a path of a given native library.
        /// </summary>
        /// <remarks>
        /// `dotnet run` command stores native library files in `runtimes\{rid}\native` directory.<br/>
        /// `dotnet publish` command with a {rid} flattens native library files.
        /// https://github.com/dotnet/sdk/issues/9643
        /// </remarks>
        /// <param name="baseDir">Location of an executable</param>
        /// <param name="filename">Native library file</param>
        /// <returns>A path of a given native library.</returns>
        private static string? GetNativeLibraryPath(string baseDir, string filename)
        {
            string libDir = "runtimes";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                libDir = Path.Combine(libDir, "win-");
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                libDir = Path.Combine(libDir, "linux-");
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                libDir = Path.Combine(libDir, "osx-");

            switch (RuntimeInformation.ProcessArchitecture)
            {
                case Architecture.X86:
                    libDir += "x86";
                    break;
                case Architecture.X64:
                    libDir += "x64";
                    break;
                case Architecture.Arm:
                    libDir += "arm";
                    break;
                case Architecture.Arm64:
                    libDir += "arm64";
                    break;
            }
            libDir = Path.Combine(libDir, "native");

            // `dotnet run` or `dotnet publish` wo {rid}.
            string runDllPath = Path.Combine(baseDir, libDir, filename);
            if (File.Exists(runDllPath))
                return runDllPath;

            // `dotnet publish` w/ {rid}
            string publishDllPath = Path.Combine(baseDir, filename);
            if (File.Exists(publishDllPath))
                return publishDllPath;

            // Error!
            return null;
        }

        public static Task LoadMagicFileAsync(string magicGzipPath)
        {
            // Decompress and load magic.mgc.gz in the background
            return Task.Run(() =>
            {
                string magicFile = FileHelper.GetTempFile("magic", "mgc");

                Joveler.Compression.ZLib.ZLibDecompressOptions opts = new Joveler.Compression.ZLib.ZLibDecompressOptions();
                using (FileStream sfs = new FileStream(magicGzipPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (FileStream dfs = new FileStream(magicFile, FileMode.Create, FileAccess.Write, FileShare.None))
                using (Joveler.Compression.ZLib.GZipStream gz = new Joveler.Compression.ZLib.GZipStream(sfs, opts))
                {
                    gz.CopyTo(dfs);
                }

                lock (_fileTypeDetectorLock)
                {
                    MagicFile = magicFile;
                    _fileTypeDetector = new FileTypeDetector(MagicFile);
                }
            });
        }
        #endregion

        #region UnhandledException Handler
        // Catch and log uncatched Exception thrown from everywhere
        private static void AppDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is not Exception ex)
                return;

            const string firstMessage = "PEBakery cannot continue due to an internal error.\r\nPlease post the crash log to the PEBakery issue tracker.";
            string exceptionMessage = Logger.LogExceptionMessage(ex, LogDebugLevel.PrintExceptionStackTrace);
            try
            {
                DateTime now = DateTime.Now;
                string crashLogFile = Path.Combine(BaseDir, $"PEBakery-crashlog_{now:yyyyMMdd_HHmmss}.txt");
                using (StreamWriter s = new StreamWriter(crashLogFile, false, Encoding.UTF8))
                {
                    EnvInfoBuilder envInfos = new EnvInfoBuilder();

                    // Banner Message
                    EnvInfoSection msgSection = new EnvInfoSection(EnvInfoBuilder.FirstSectionOrder);
                    msgSection.KeyValues.Add(new KeyValuePair<string, string>(string.Empty, firstMessage));
                    envInfos.AddSection(msgSection);

                    // [PEBakery] - CrashTime
                    envInfos.PEBakeryInfoSection.KeyValues.Add(new KeyValuePair<string, string>("CrashTime", $"{now:yyyy.MM.dd HH:mm:ss K}"));

                    // [Exception Trace]
                    EnvInfoSection exceptionSection = new EnvInfoSection(EnvInfoBuilder.LastSectionOrder, "Exception Trace");
                    exceptionSection.KeyValues.Add(new KeyValuePair<string, string>(string.Empty, exceptionMessage));
                    envInfos.AddSection(exceptionSection);

                    s.WriteLine(envInfos);
                }

                MessageBox.Show(firstMessage, "Unhandled Exception", MessageBoxButton.OK, MessageBoxImage.Error);
                FileHelper.OpenPath(crashLogFile);
            }
            catch
            {
                // Even if EnvInfoBuilder throws exception, at least print exception message as MessageBox.
                MessageBox.Show($"{firstMessage}\r\n\r\n{exceptionMessage}", "Critical Unhandled Exception", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion
    }
    #endregion

    #region BuildTimestamp
    public static class BuildTimestamp
    {
        public static string? ReadString()
        {
            CustomAttributeData attr = Assembly.GetExecutingAssembly()
                .GetCustomAttributesData()
                .First(x => x.AttributeType.Name.Equals("TimestampAttribute", StringComparison.Ordinal));

            return attr.ConstructorArguments.First().Value as string;
        }

        public static DateTime ReadDateTime()
        {
            if (ReadString() is not string timestampStr)
                return DateTime.MinValue;
            return DateTime.ParseExact(timestampStr, "yyyy-MM-ddTHH:mm:ss.fffZ", null, DateTimeStyles.AssumeUniversal).ToUniversalTime();
        }
    }
    #endregion
}
