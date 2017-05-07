using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Launcher
{
    class Launcher
    {
        static void Main(string[] args)
        {
            Process proc = new Process();
            proc.StartInfo.UseShellExecute = true;
            proc.StartInfo.Verb = "Open";
            proc.StartInfo.FileName = @"Binary\PEBakery.exe";
            proc.StartInfo.WorkingDirectory = GetProgramAbsolutePath();
            // StringBuilder b = new StringBuilder("/basedir .");
            StringBuilder b = new StringBuilder();
            foreach (string arg in args)
            {
                b.Append("\"");
                b.Append(arg);
                b.Append("\" ");
            }
            proc.StartInfo.Arguments = b.ToString();
            proc.Start();
        }

        public static string GetProgramAbsolutePath()
        {
            string path = AppDomain.CurrentDomain.BaseDirectory;
            if (Path.GetDirectoryName(path) != null)
                path = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return path;
        }
    }
}
