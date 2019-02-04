/*
    Copyright (C) 2016-2018 Hajin Jang
    Licensed under MIT License.

    MIT License

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.
*/

#define ENABLE_DOTNETFX_472

using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;

namespace PEBakeryLauncher
{
    public class Launcher
    {
#if ENABLE_DOTNETFX_472
        public const string DotNetFxVerStr = "4.7.2";
        public const string DotNetFxInstallerUrl = "https://go.microsoft.com/fwlink/?LinkId=863265";
        public const uint DotNetFxReleaseValue = 461808;
#else
        public const string DotNetFxVerStr = "4.7.1";
        public const string DotNetFxInstallerUrl = "https://www.microsoft.com/en-us/download/details.aspx?id=56116";
        public const uint DotNetFxReleaseValue = 461308;
#endif

        public static void Main(string[] args)
        {
            // Check if PEBakery.exe exists
            string absPath = GetProgramAbsolutePath();
            string pebakeryPath = Path.Combine(absPath, "Binary", "PEBakery.exe");
            if (!File.Exists(pebakeryPath))
            {
                MessageBox.Show("Unable to find PEBakery.",
                    "Unable to find PEBakery",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Environment.Exit(1);
            }

            // Alert user to install .Net Framework to 4.7.x if not installed.
            // The launcher itself runs in .Net Framework 4 Client Profile.
            if (!CheckNetFrameworkVersion())
            {
                MessageBox.Show($"PEBakery requires .Net Framework {DotNetFxVerStr} or newer.", 
                                $"Install .Net Framework {DotNetFxVerStr}", 
                                MessageBoxButton.OK, 
                                MessageBoxImage.Error);
                Process.Start(DotNetFxInstallerUrl);
                Environment.Exit(1);
            }

            // Launch PEBakery.exe using ShellExecute
            StringBuilder b = new StringBuilder();
            foreach (string arg in args)
            {
                b.Append("\"");
                b.Append(arg);
                b.Append("\" ");
            }
            string argStr = b.ToString();
            Console.WriteLine(argStr);

            Process proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    Verb = "Open",
                    FileName = pebakeryPath,
                    WorkingDirectory = absPath,
                    Arguments = argStr,
                }
            };
            proc.Start();
        }

        public static string GetProgramAbsolutePath()
        {
            string path = AppDomain.CurrentDomain.BaseDirectory;
            if (Path.GetDirectoryName(path) != null)
                path = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return path;
        }

        private static bool CheckNetFrameworkVersion()
        { // https://docs.microsoft.com/ko-kr/dotnet/framework/migration-guide/how-to-determine-which-versions-are-installed#net_b
            const string ndpPath = @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full";
            using (RegistryKey ndpKey = Registry.LocalMachine.OpenSubKey(ndpPath, false))
            {
                if (ndpKey == null)
                    return false;

                uint revision = (uint)(int)ndpKey.GetValue("Release", 0);

                // PEBakery requires .Net Framework 4.7.x or later
                return DotNetFxReleaseValue <= revision;
            }
        }
    }
}
