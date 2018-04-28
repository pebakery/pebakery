using PEBakery.Core;
using PEBakery.WPF;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace PEBakery
{
    public partial class App : Application
    {
        public static string[] Args;
        public static int Version = 0;
        public static Logger Logger;
        public static string BaseDir;

        void App_Startup(object sender, StartupEventArgs e)
        {
            // If no command line arguments were provided, don't process them 
            if (e.Args.Length == 0)
                Args = new string[0];
            else if (e.Args.Length > 0)
                Args = e.Args;

            // Initialize zlib and wimlib
            NativeAssemblyInit();

            // Why Properties.Resources is not available in App_Startup?
            // Version = Properties.Resources.EngineVersion;
        }

        private void NativeAssemblyInit()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string arch = IntPtr.Size == 8 ? "x64" : "x86";

            string zLibDllPath = Path.Combine(baseDir, arch, "zlibwapi.dll");
            string wimLibDllPath = Path.Combine(baseDir, arch, "libwim-15.dll");
            string xzDllPath = Path.Combine(baseDir, arch, "liblzma.dll");
            string lz4DllPath = Path.Combine(baseDir, arch, "liblz4.so.1.8.1.dll");

            Joveler.ZLibWrapper.ZLibNative.AssemblyInit(zLibDllPath);
            ManagedWimLib.Wim.GlobalInit(wimLibDllPath);
            PEBakery.XZLib.XZStream.GlobalInit(xzDllPath);
            PEBakery.LZ4Lib.LZ4FrameStream.GlobalInit(lz4DllPath);
        }

    }
}
