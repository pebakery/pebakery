/*
    Copyright (C) 2018 Hajin Jang
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
using SQLite;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;

namespace PEBakery.Core
{
    #region Global
    public static class Global
    {
        #region Constants
        public static class Const
        {
            public const int EngineVersion = 96;
            public const string ScriptCacheRevision = "r13";
            public const string StringVersion = "0.9.6";
            public const string StringVersionFull = "0.9.6 beta6";
        }
        #endregion

        #region Fields and Properties
        // Build-time constant
        public static DateTime BuildDate;

        // Start-time variables
        public static string[] Args;
        public static string BaseDir;

        // Buffer Pool
        public static RecyclableMemoryStreamManager MemoryStreamManager = new RecyclableMemoryStreamManager();

        // Global Instances
        public static Logger Logger;
        public static MainViewModel MainViewModel;
        public static Setting Setting;
        public static ProjectCollection Projects;
        public static ScriptCache ScriptCache;
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

            // Initialize native libraries
            NativeGlobalInit(AppDomain.CurrentDomain.BaseDirectory);

            // Load BuildDate
            BuildDate = BuildTimestamp.ReadDateTime();

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
            catch (SQLiteException e)
            { // Update failure
                string msg = $"SQLite Error : {e.Message}\r\n\r\nLog database is corrupted.\r\nPlease delete PEBakeryLog.db and restart.";
                MessageBox.Show(msg, "SQLite Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                if (Application.Current != null)
                    Application.Current.Shutdown(1);
                else
                    Environment.Exit(1);
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

            // Load script cache
            if (Setting.Script.EnableCache)
            {
                string cacheDbFile = Path.Combine(dbDir, "PEBakeryCache.db");
                try
                {
                    ScriptCache = new ScriptCache(cacheDbFile);
                    Logger.SystemWrite(new LogInfo(LogState.Info, $"ScriptCache enabled, {ScriptCache.Table<CacheModel.ScriptCache>().Count()} cached scripts found"));
                }
                catch (SQLiteException e)
                { // Load failure
                    string msg = $"SQLite Error : {e.Message}\r\n\r\nCache database is corrupted.\r\nPlease delete PEBakeryCache.db and restart.";
                    MessageBox.Show(msg, "SQLite Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                    if (Application.Current != null)
                        Application.Current.Shutdown(1);
                    else
                        Environment.Exit(1);
                }
            }
            else
            {
                Logger.SystemWrite(new LogInfo(LogState.Info, "ScriptCache disabled"));
            }
        }
        #endregion

        #region Load Native Libraries
        public static void NativeGlobalInit(string baseDir)
        {
            string arch;
            switch (RuntimeInformation.ProcessArchitecture)
            {
                case Architecture.X64:
                    arch = "x64";
                    break;
                case Architecture.X86:
                    arch = "x86";
                    break;
                default:
                    throw new PlatformNotSupportedException();
            }

            string zlibPath = Path.Combine(baseDir, arch, "zlibwapi.dll");
            string xzPath = Path.Combine(baseDir, arch, "liblzma.dll");
            string wimlibPath = Path.Combine(baseDir, arch, "libwim-15.dll");
            string sevenZipPath = Path.Combine(baseDir, arch, "7z.dll");

            Joveler.Compression.ZLib.ZLibInit.GlobalInit(zlibPath, 64 * 1024); // 64K
            Joveler.Compression.XZ.XZInit.GlobalInit(xzPath, 64 * 1024); // 64K
            ManagedWimLib.Wim.GlobalInit(wimlibPath);
            SevenZip.SevenZipBase.SetLibraryPath(sevenZipPath);
        }

        public static void NativeGlobalCleanup()
        {
            Joveler.Compression.ZLib.ZLibInit.GlobalCleanup();
            Joveler.Compression.XZ.XZInit.GlobalCleanup();
            ManagedWimLib.Wim.GlobalCleanup();
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
