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
    /// <summary>
    /// App.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class App : Application
    {
        public static string[] Args;
        public static int Version = 0;
        public static Logger Logger;

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

        void NativeAssemblyInit()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string arch;
            if (IntPtr.Size == 8)
                arch = "x64";
            else
                arch = "x86";

            string ZLibDllPath = Path.Combine(baseDir, arch, "zlibwapi.dll");
            string WimLibDllPath = Path.Combine(baseDir, arch, "libwim-15.dll");
            Joveler.ZLibWrapper.ZLibNative.AssemblyInit(ZLibDllPath);
            ManagedWimLib.WimLibNative.AssemblyInit(WimLibDllPath);
        }

    }
}
