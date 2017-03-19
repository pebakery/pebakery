using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;

namespace BakeryEngine_Legacy
{
    public class PEBakery
    {
        public static int Main(string[] args)
        {
            string argBaseDir = FileHelper.GetProgramAbsolutePath();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "/basedir", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length)
                        argBaseDir = Path.GetFullPath(args[i + 1]);
                    else
                        Console.WriteLine("/basedir must be used with path\r\n");
                }
                else if (string.Equals(args[i], "/?", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(args[i], "/help", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(args[i], "/h", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Sorry, help message not implemented\r\n");
                }
            }

            Project project = new Project(argBaseDir, "TestSuite");
            Logger logger = new Logger(Path.Combine(argBaseDir, "log.txt"), LogFormat.Text);
            // BakeryEngine engine = new BakeryEngine(argBaseDir, project, logger, DebugLevel.Production);
            // BakeryEngine engine = new BakeryEngine(argBaseDir, project, logger, DebugLevel.PrintExceptionType);
            BakeryEngine engine = new BakeryEngine(argBaseDir, project, logger, DebugLevel.PrintExceptionStackTrace);
            Stopwatch stopwatch = Stopwatch.StartNew();
            Console.WriteLine("BakeryEngine start...");
            engine.Build();
            Console.WriteLine("BakeryEngine done");
            Console.WriteLine("Time elapsed: {0}\r\n", stopwatch.Elapsed);
            logger.Close();

            return 0;
        }
    }

    public class PEBakeryInfo
    {
        private Version ver;
        public Version Ver { get { return ver; } }
        private DateTime build;
        public DateTime Build { get { return build; } }
        public string baseDir;
        public string BaseDir { get { return baseDir; } }

        public PEBakeryInfo()
        {
            this.baseDir = FileHelper.GetProgramAbsolutePath();
            this.ver = FileHelper.GetProgramVersion();
            this.build = FileHelper.GetBuildDate();
        } 
    }
}
