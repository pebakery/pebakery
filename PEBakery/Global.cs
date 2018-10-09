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

using PEBakery.Core;
using PEBakery.WPF;
using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.IO;

namespace PEBakery
{
    public static class Global
    {
        // Build-time constant
        public static int Version = 0;
        public static DateTime BuildDate;

        // Start-time variables
        public static string[] Args;
        public static string BaseDir;

        // Buffer Pool
        public static RecyclableMemoryStreamManager MemoryStreamManager = new RecyclableMemoryStreamManager();

        // Global Instances
        public static Logger Logger;
        public static MainViewModel MainViewModel;
        public static SettingViewModel Setting;
        public static ProjectCollection Projects;

        // Load Native Libraries
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
    }
}
