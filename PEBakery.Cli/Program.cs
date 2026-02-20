/*
    Copyright (C) 2016-present Hajin Jang
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
*/

using PEBakery.Core;
using PEBakery.Core.Arguments;
using PEBakery.Core.ViewModels;
using PEBakery.Helper;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PEBakery.Cli
{
    internal class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            // Enable headless mode globally before any initialization
            Global.HeadlessMode = true;
            SystemHelper.HeadlessMode = true;

            Console.WriteLine($"PEBakery CLI {Global.Const.ProgramVersionStrFull}");
            Console.WriteLine("Headless build mode");
            Console.WriteLine();

            try
            {
                // Create MainViewModel on STA thread before PreInit
                // (required for WPF types in PEBakery.Core)
                MainViewModel? model = null;
                RunSTAThread(() => model = new MainViewModel());
                if (model == null)
                {
                    Console.Error.WriteLine("[FATAL] Failed to initialize MainViewModel.");
                    return 1;
                }
                Global.MainViewModel = model;

                // PreInit: register encodings, load native libraries, set build date
                // Pass initMainViewModel=false since we already created it above
                Global.PreInit(args, false);

                // Use Global.Init() which handles:
                // - Argument parsing, BaseDir setup
                // - Logger initialization (with SQLite retry)
                // - ProjectCollection initialization
                // - Settings loading
                // - ScriptCache initialization
                // All MessageBox calls in Init() are now guarded by HeadlessMode
                Global.Init();

                // Re-parse arguments to get our CLI-specific options
                ArgumentParser argParser = new ArgumentParser();
                PEBakeryOptions? opts = argParser.Parse(Global.Args);
                if (opts == null)
                {
                    PrintUsage();
                    return 1;
                }

                // Validate: --build is required for CLI mode
                if (!opts.Build)
                {
                    Console.Error.WriteLine("[ERROR] --build flag is required for CLI mode.");
                    Console.Error.WriteLine("Usage: PEBakery.Cli --baseDir <path> --build [--project <name>] [--logFile <path>]");
                    return 1;
                }

                if (Global.Projects == null || Global.Projects.Count == 0)
                {
                    Console.Error.WriteLine("[ERROR] No projects found. Check your baseDir and Projects directory.");
                    return 1;
                }

                // Load projects (PrepareLoad + Load)
                Console.WriteLine($"Loading projects from: {Global.BaseDir}");
                ScriptCache? scriptCache = Global.Setting.Script.EnableCache ? Global.ScriptCache : null;
                var (scriptCount, linkCount) = Global.Projects.PrepareLoad();
                Console.WriteLine($"Found {scriptCount} scripts, {linkCount} links");

                var errorLogs = Global.Projects.Load(scriptCache, null);
                if (errorLogs.Count > 0)
                {
                    foreach (var log in errorLogs)
                        Console.Error.WriteLine($"[{log.State}] {log.Message}");
                }

                if (Global.Projects.Count == 0)
                {
                    Console.Error.WriteLine("[ERROR] No projects loaded successfully.");
                    return 1;
                }

                // Find the target project
                Project? targetProject = null;
                if (opts.Project != null)
                {
                    for (int i = 0; i < Global.Projects.Count; i++)
                    {
                        if (Global.Projects[i].ProjectName.Equals(opts.Project, StringComparison.OrdinalIgnoreCase))
                        {
                            targetProject = Global.Projects[i];
                            break;
                        }
                    }

                    if (targetProject == null)
                    {
                        Console.Error.WriteLine($"[ERROR] Project '{opts.Project}' not found.");
                        Console.Error.WriteLine("Available projects:");
                        for (int i = 0; i < Global.Projects.Count; i++)
                            Console.Error.WriteLine($"  - {Global.Projects[i].ProjectName}");
                        return 1;
                    }
                }
                else
                {
                    targetProject = Global.Projects[0];
                }

                Console.WriteLine($"Building project: {targetProject.ProjectName}");
                Console.WriteLine($"Active scripts: {targetProject.ActiveScripts.Count}");
                Console.WriteLine();

                // Create EngineState and run the build
                EngineState engineState = new EngineState(targetProject, Global.Logger, model, null);
                engineState.SetOptions(Global.Setting);
                engineState.SetCompat(targetProject.Compat);

                if (!Engine.TryEnterLock())
                {
                    Console.Error.WriteLine("[ERROR] Another build is already running.");
                    return 1;
                }

                Engine.WorkingEngine = new Engine(engineState);

                Console.WriteLine("Build started...");
                DateTime buildStart = DateTime.Now;

                Task<int> buildTask = Engine.WorkingEngine.Run($"Project {targetProject.ProjectName}");
                buildTask.Wait();
                int buildId = buildTask.Result;

                DateTime buildEnd = DateTime.Now;
                TimeSpan elapsed = buildEnd - buildStart;

                Engine.WorkingEngine = null;
                Engine.ExitLock();

                // Report result
                string? resultReport = engineState.RunResultReport();
                Console.WriteLine();
                Console.WriteLine($"Build completed in {elapsed:hh\\:mm\\:ss}");
                Console.WriteLine($"Build ID: {buildId}");
                if (resultReport != null)
                    Console.WriteLine($"Result: {resultReport}");

                // Export log if requested
                if (opts.LogFile != null)
                {
                    string logPath = Path.GetFullPath(opts.LogFile);
                    Console.WriteLine($"Exporting build log to: {logPath}");
                    Global.Logger.ExportBuildLog(LogExportFormat.Html, logPath, buildId, new BuildLogOptions());
                    Console.WriteLine("Log exported successfully.");
                }

                // Cleanup
                Global.Cleanup();

                return engineState.HaltReturnFlags.CheckBuildHalt() ? 1 : 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[FATAL] {ex.Message}");
                Console.Error.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: PEBakery.Cli [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -b, --baseDir <path>    Base directory containing the Projects folder");
            Console.WriteLine("      --build             Start building (required)");
            Console.WriteLine("  -p, --project <name>    Project name to build (default: first project)");
            Console.WriteLine("  -l, --logFile <path>    Export build log to file (HTML format)");
            Console.WriteLine("      --headless          Run in headless mode (always on for CLI)");
            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.WriteLine("  PEBakery.Cli --baseDir D:\\PhoenixPE --build --project PhoenixPE");
        }

        private static void RunSTAThread(Action action)
        {
            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
                action.Invoke();
                return;
            }

            Thread thread = new Thread(() => action.Invoke());
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
        }
    }
}
