using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using PEBakery.Core;

namespace PEBakery
{
    // ReSharper disable RedundantExtendsListEntry
    public partial class App : Application
    {
        public static string[] Args;
        public static int Version = 0;
        public static Logger Logger;
        public static string BaseDir;

        internal void App_Startup(object sender, StartupEventArgs e)
        {
            // If no command line arguments were provided, don't process them 
            if (e.Args.Length == 0)
                Args = new string[0];
            else if (e.Args.Length > 0)
                Args = e.Args;

            // Initialize zlib and wimlib
            NativeGlobalInit(AppDomain.CurrentDomain.BaseDirectory);

            // Why Properties.Resources is not available in App_Startup?
            // Version = Properties.Resources.EngineVersion;
        }

        public static void NativeGlobalInit(string baseDir)
        {
            string arch = IntPtr.Size == 8 ? "x64" : "x86";

            string zLibDllPath = Path.Combine(baseDir, arch, "zlibwapi.dll");
            string wimLibDllPath = Path.Combine(baseDir, arch, "libwim-15.dll");
            string xzDllPath = Path.Combine(baseDir, arch, "liblzma.dll");

            Joveler.ZLibWrapper.ZLibInit.GlobalInit(zLibDllPath, 64 * 1024); // 64K
            ManagedWimLib.Wim.GlobalInit(wimLibDllPath);
            PEBakery.XZLib.XZStream.GlobalInit(xzDllPath, 64 * 1024); // 64K
        }

        public static void NativeGlobalCleanup()
        {
            Joveler.ZLibWrapper.ZLibInit.GlobalCleanup();
            ManagedWimLib.Wim.GlobalCleanup();
            PEBakery.XZLib.XZStream.GlobalCleanup();
        }
    }
}
