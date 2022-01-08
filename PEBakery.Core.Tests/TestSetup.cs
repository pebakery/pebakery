using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.Core.ViewModels;
using PEBakery.Helper;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PEBakery.Core.Tests
{
    [TestClass]
    [TestCategory(nameof(TestSetup))]
    public class TestSetup
    {
        #region AssemblyInitalize, AssemblyCleanup
        [AssemblyInitialize]
        public static void PrepareTests(TestContext ctx)
        {
            // Regsiter Non-Unicode Encodings
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            // TODO: .Net Core Band-aid. Without this line, entire WPF Control access would crash the test.
            EngineTests.RunSTAThread(() =>
            {
                // Set MainViewModel
                Global.MainViewModel = new MainViewModel();
            });

            // Instance of Setting
            string emptyTempFile = Path.GetTempFileName();
            if (File.Exists(emptyTempFile))
                File.Delete(emptyTempFile);
            Global.Setting = new Setting(emptyTempFile); // Set to default

            // Load Project "TestSuite" (ScriptCache disabled)
            EngineTests.BaseDir = Path.GetFullPath(Path.Combine("..", "..", "..", "Samples"));
            ProjectCollection projects = new ProjectCollection(EngineTests.BaseDir);
            projects.PrepareLoad();
            projects.Load(null, null);

            // Should be only one project named TestSuite
            EngineTests.Project = projects[0];
            Assert.IsTrue(projects.Count == 1);
            EngineTests.TestBench = EngineTests.Project.Variables["TestBench"];

            // Init NativeAssembly
            Global.NativeGlobalInit(AppDomain.CurrentDomain.BaseDirectory);
            EngineTests.MagicFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "magic.mgc");

            // Use InMemory Database for Tests
            Logger.DebugLevel = LogDebugLevel.PrintExceptionStackTrace;
            EngineTests.Logger = new Logger(":memory:");
            EngineTests.Logger.SystemWrite(new LogInfo(LogState.Info, "PEBakery.Tests launched"));

            // Set Global 
            Global.Logger = EngineTests.Logger;
            Global.BaseDir = EngineTests.BaseDir;
            Global.MagicFile = EngineTests.MagicFile;
            Global.BuildDate = BuildTimestamp.ReadDateTime();

            // IsOnline?
            EngineTests.IsOnline = NetworkHelper.IsOnline();
        }

        [AssemblyCleanup]
        public static void AssemblyCleanup()
        {
            StopWebFileServer();

            Global.Cleanup();
        }
        #endregion

        #region ExtractWith7z
        public static int ExtractWith7Z(string sampleDir, string srcArchive, string destDir)
        {
            string binary;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                binary = Path.Combine(sampleDir, "7z.exe");
            else
                throw new PlatformNotSupportedException();

            Process proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    FileName = binary,
                    Arguments = $"x {srcArchive} -o{destDir}",
                }
            };
            proc.Start();
            proc.WaitForExit();
            return proc.ExitCode;
        }
        #endregion

        #region FileEqual
        public static bool FileEqual(string x, string y)
        {
            byte[] h1;
            using (FileStream fs = new FileStream(x, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                h1 = HashHelper.GetHash(HashType.SHA256, fs);
            }

            byte[] h2;
            using (FileStream fs = new FileStream(y, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                h2 = HashHelper.GetHash(HashType.SHA256, fs);
            }

            return h1.SequenceEqual(h2);
        }
        #endregion

        #region Web File Server
        private static int _serverPort = 0;
        public static int ServerPort
        {
            get => _serverPort;
            private set => _serverPort = value;
        }

        private static readonly object FileServerLock = new object();
        private static Task? _fileServerTask;
        private static CancellationTokenSource? _fileServerCancel;

        public static string WebRoot { get; private set; } = string.Empty;
        public static string? UrlRoot { get; private set; }
        public static bool IsServerRunning
        {
            get
            {
                lock (FileServerLock)
                    return _fileServerTask != null;
            }
        }

        public static void StartWebFileServer()
        {
            lock (FileServerLock)
            {
                if (_fileServerTask != null)
                    return;

                WebRoot = Path.Combine(EngineTests.BaseDir, "WebServer");
                ServerPort = GetAvailableTcpPort();

                IWebHost host = new WebHostBuilder()
                    .UseKestrel()
                    .UseWebRoot(WebRoot)
                    .Configure(conf =>
                    {
                        // Set up custom content types - associating file extension to MIME type
                        FileExtensionContentTypeProvider provider = new FileExtensionContentTypeProvider
                        {
                            Mappings =
                            {
                                [".script"] = "text/plain",
                                [".deleted"] = "text/plain",
                            }
                        };

                        conf.UseStaticFiles(new StaticFileOptions
                        {
                            // ServeUnknownFileTypes = true,
                            // DefaultContentType = "text/plain",
                            ContentTypeProvider = provider,
                        });
                        conf.UseDefaultFiles();
                        conf.UseDirectoryBrowser();
                    })
                    .ConfigureKestrel((ctx, opts) => { opts.Listen(IPAddress.Loopback, ServerPort); })
                    .Build();

                UrlRoot = $"http://localhost:{ServerPort}";
                _fileServerCancel = new CancellationTokenSource();
                _fileServerTask = host.RunAsync(_fileServerCancel.Token);

                Console.WriteLine($"Launched web server at TCP {ServerPort}");
            }
        }

        public static void StopWebFileServer()
        {
            lock (FileServerLock)
            {
                if (_fileServerTask == null)
                    return;
                if (_fileServerCancel == null)
                    return;

                _fileServerCancel.Cancel();

                _fileServerCancel = null;
                _fileServerTask = null;
                UrlRoot = null;
            }
        }

        public static int GetAvailableTcpPort()
        {
            TcpListener tcpListener = new TcpListener(IPAddress.Loopback, 0);

            int port;
            try
            {
                tcpListener.Start();
                port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
            }
            finally
            {
                tcpListener.Stop();
            }
            return port;
        }
        #endregion
    }
}
