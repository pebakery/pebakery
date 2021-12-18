﻿/*
    Copyright (C) 2018-2020 Hajin Jang
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
using PEBakery.Core.ViewModels;
using PEBakery.Helper;
using SQLite;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace PEBakery.Core
{
    #region Global
    public static class Global
    {
        #region Constants
        public static class Const
        {
            public const int EngineVersion = 96;
            public const string ScriptCacheRevision = "r21";
            public const string ProgramVersionStr = "0.9.7";
            public const string ProgramVersionStrFull = "0.9.7 beta7";

            public static readonly VersionEx ProgramVersionInst = VersionEx.Parse(ProgramVersionStr);

            // Update json version
            public const string UpdateSchemaMaxVerStr = "0.1.1";
            public const string UpdateSchemaMinVerStr = "0.1.1";
            public static readonly VersionEx UpdateSchemaMaxVerInst = VersionEx.Parse(UpdateSchemaMaxVerStr);
            public static readonly VersionEx UpdateSchemaMinVerInst = VersionEx.Parse(UpdateSchemaMinVerStr);
        }
        #endregion

        #region Fields and Properties
        // Build-time constant
        public static DateTime BuildDate { get; set; }

        // Start-time variables
        public static string[] Args { get; set; }
        public static string BaseDir { get; set; }


        // Buffer Pool
        public static RecyclableMemoryStreamManager MemoryStreamManager = new RecyclableMemoryStreamManager();

        // Global Instances
        public static Logger Logger { get; set; }
        public static MainViewModel MainViewModel { get; set; }
        public static Setting Setting { get; set; }
        public static ProjectCollection Projects { get; set; }
        public static ScriptCache ScriptCache { get; set; }

        // FileTypeDetector / LibMagic
        public static string MagicFile { get; set; }
        private static readonly object _fileTypeDetectorLock = new object();
        private static FileTypeDetector _fileTypeDetector;
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
            string baseDir = Environment.CurrentDirectory;
            for (int i = 0; i < Args.Length; i++)
            {
                if (Args[i].Equals("/basedir", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < Args.Length)
                    {
                        baseDir = Path.GetFullPath(Args[i + 1]);
                        if (!Directory.Exists(baseDir))
                        {
                            MessageBox.Show($"Directory [{baseDir}] does not exist", "Invalid BaseDir", MessageBoxButton.OK, MessageBoxImage.Error);
                            Environment.Exit(1); // Force Shutdown
                        }
                        Environment.CurrentDirectory = baseDir;
                    }
                    else
                    {
                        // ReSharper disable once LocalizableElement
                        Console.WriteLine("\'/basedir\' must be used with path\r\n");
                    }
                }
                else if (Args[i].Equals("/?", StringComparison.OrdinalIgnoreCase) ||
                         Args[i].Equals("/help", StringComparison.OrdinalIgnoreCase) ||
                         Args[i].Equals("/h", StringComparison.OrdinalIgnoreCase))
                {
                    // ReSharper disable once LocalizableElement
                    Console.WriteLine("Sorry, help message not implemented\r\n");
                }
            }
            BaseDir = baseDir;

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
                    string msg = $"SQLite Error : {e.Message}\r\n\r\nLog database is corrupted and not repairable.\r\nPlease delete PEBakeryLog.db and restart.";
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
            if (Setting.Interface.UseCustomTitle)
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
                    string msg = $"SQLite Error : {e.Message}\r\n\r\nCache database is corrupted and not repairable.\r\nPlease delete PEBakeryCache.db and restart.";
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
            string magicPath = GetNativeLibraryPath(baseDir, "libmagic-1.dll");
            string zlibPath = GetNativeLibraryPath(baseDir, "zlibwapi.dll");
            string xzPath = GetNativeLibraryPath(baseDir, "liblzma.dll");
            string wimlibPath = GetNativeLibraryPath(baseDir, "libwim-15.dll");
            string sevenZipPath = GetNativeLibraryPath(baseDir, "7z.dll");

            try
            {
                Joveler.FileMagician.Magic.GlobalInit(magicPath);
                Joveler.Compression.ZLib.ZLibInit.GlobalInit(zlibPath);
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
        private static string GetNativeLibraryPath(string baseDir, string filename)
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
    }
    #endregion

    #region BuildTimestamp
    public static class BuildTimestamp
    {
        public static string ReadString()
        {
            CustomAttributeData attr = Assembly.GetExecutingAssembly()
                .GetCustomAttributesData()
                .First(x => x.AttributeType.Name.Equals("TimestampAttribute", StringComparison.Ordinal));

            return attr.ConstructorArguments.First().Value as string;
        }

        public static DateTime ReadDateTime()
        {
            string timestampStr = ReadString();
            return DateTime.ParseExact(timestampStr, "yyyy-MM-ddTHH:mm:ss.fffZ", null, DateTimeStyles.AssumeUniversal).ToUniversalTime();
        }
    }
    #endregion
}
