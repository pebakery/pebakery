using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using PEBakery.Core;
using PEBakery.Ini;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Benchmark
{
    internal static class CoreBenchFixture
    {
        private static bool _initialized;

        public static string BaseDir { get; private set; } = string.Empty;
        public static string ProjectDir { get; private set; } = string.Empty;
        public static string IniTemplate { get; private set; } = string.Empty;

        public static void EnsureInitialized()
        {
            if (_initialized)
                return;

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            BaseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CoreBenchFixture");
            ProjectDir = Path.Combine(BaseDir, Project.Names.Projects, "BenchProject");
            Directory.CreateDirectory(ProjectDir);

            WriteProjectFixture();
            WriteIniFixture();

            _initialized = true;
        }

        public static Project LoadProject()
        {
            EnsureInitialized();

            ProjectCollection projects = new ProjectCollection(BaseDir);
            projects.PrepareLoad();
            projects.Load(null, null);
            return projects[0];
        }

        private static void WriteProjectFixture()
        {
            string mainScript = Path.Combine(ProjectDir, Project.Names.MainScriptFile);
            File.WriteAllLines(mainScript, CreateScriptLines("BenchProject", 0), Encoding.UTF8);

            for (int dirIdx = 0; dirIdx < 8; dirIdx++)
            {
                string dir = Path.Combine(ProjectDir, $"Dir{dirIdx:00}");
                Directory.CreateDirectory(dir);

                for (int fileIdx = 0; fileIdx < 16; fileIdx++)
                {
                    string script = Path.Combine(dir, $"Script{fileIdx:00}.script");
                    File.WriteAllLines(script, CreateScriptLines($"Script{dirIdx:00}_{fileIdx:00}", fileIdx), Encoding.UTF8);
                }
            }
        }

        private static string[] CreateScriptLines(string title, int seed)
        {
            List<string> lines = new List<string>(160)
            {
                "[Main]",
                $"Title={title}",
                "Author=Benchmark",
                "Description=Generated benchmark fixture",
                "Version=001",
                $"Level={seed % 8}",
                "Selected=True",
                "Mandatory=False",
                string.Empty,
                "[Variables]",
                "%BenchRoot%=%BaseDir%\\Bench",
                "%SrcFile%=%BenchRoot%\\Source.ini",
                "%Target%=%BenchRoot%\\Target",
                string.Empty,
                "[Process]",
                "System,SetLocal",
                "ReadInterface,Text,%ScriptFile%,Interface,FileBox01,%IfaceSection%",
                "ReadInterface,Visible,%ScriptFile%,%IfaceSection%,ActiveMarker,%isActive%",
                "ReadInterface,Value,%ScriptFile%,%IfaceSection%,Index01,%wimIndex%",
                "If,%isActive%,Equal,True,Begin",
                "  Set,%Target%,%BaseDir%\\Target",
            };

            for (int i = 0; i < 36; i++)
            {
                lines.Add($"  IniRead,%SrcFile%,Section{i % 6},Key{i % 8},%Dest{i:00}%");
                lines.Add($"  TXTAddLine,%Target%\\log.txt,\"Line {i} %Dest{i:00}%\",Append");
                lines.Add($"  WimExtract,\"%BaseDir%\\install.wim\",{1 + i % 3},\"\\Windows\\Path{i:00}\",%Target%");
                if (i % 9 == 0)
                    lines.Add("  // Benchmark comment");
            }

            lines.AddRange(new[]
            {
                "End",
                "Else,Begin",
                "  IniRead,%SrcFile%,A,B,%D1%",
                "  IniRead,%SrcFile%,C,D,%D2%",
                "  IniRead,%SrcFile%,E,F,%D3%",
                "  IniRead,%D1%,%D2%,%D3%,%D4%",
                "End",
                "WriteInterface,Resource,%ScriptFile%,Interface,Button01,Image.png",
                "WriteInterface,PosX,%ScriptFile%,Interface,Button01,100",
                "System,EndLocal",
                string.Empty,
                "[Interface]",
                "FileBox01=,1,0,0,200,24,file",
                "Button01=Run,1,0,32,80,24,button",
            });

            return lines.ToArray();
        }

        private static void WriteIniFixture()
        {
            IniTemplate = Path.Combine(BaseDir, "Bulk.ini");
            List<string> lines = new List<string>(1200);
            for (int section = 0; section < 64; section++)
            {
                lines.Add($"[Section{section}]");
                for (int key = 0; key < 16; key++)
                    lines.Add($"Key{key}=Value-{section}-{key}");
                lines.Add(string.Empty);
            }
            File.WriteAllLines(IniTemplate, lines, Encoding.UTF8);
        }
    }

    [MemoryDiagnoser]
    [ShortRunJob]
    public class CodePipelineBench
    {
        private ScriptSection _section = null!;
        private CodeParser.Options _parseOnlyOptions = null!;
        private CodeParser.Options _parseAndOptimizeOptions = null!;
        private CodeCommand[] _unoptimized = Array.Empty<CodeCommand>();

        [GlobalSetup]
        public void GlobalSetup()
        {
            Project project = CoreBenchFixture.LoadProject();
            _section = project.MainScript.Sections[ScriptSection.Names.Process];
            _parseOnlyOptions = CodeParser.Options.CreateOptions(null, project.Compat);
            _parseOnlyOptions.OptimizeCode = false;
            _parseAndOptimizeOptions = CodeParser.Options.CreateOptions(null, project.Compat);
            _parseAndOptimizeOptions.OptimizeCode = true;

            (_unoptimized, _) = new CodeParser(_section, _parseOnlyOptions).ParseStatements();
        }

        [Benchmark]
        public CodeCommand[] ParseOnly()
        {
            (CodeCommand[] cmds, _) = new CodeParser(_section, _parseOnlyOptions).ParseStatements();
            return cmds;
        }

        [Benchmark]
        public CodeCommand[] ParseAndOptimize()
        {
            (CodeCommand[] cmds, _) = new CodeParser(_section, _parseAndOptimizeOptions).ParseStatements();
            return cmds;
        }

        [Benchmark]
        public int OptimizeOnly()
        {
            List<CodeCommand> block = _unoptimized.ToList();
            CodeOptimizer.Optimize(block);
            return block.Count;
        }
    }

    [MemoryDiagnoser]
    [ShortRunJob]
    public class VariablesBench
    {
        private Variables _variables = null!;
        private string _shortInput = string.Empty;
        private string _nestedInput = string.Empty;
        private string _plainInput = string.Empty;

        [GlobalSetup]
        public void GlobalSetup()
        {
            Project project = CoreBenchFixture.LoadProject();
            _variables = new Variables(project, project.Compat);
            _variables.SetValue(VarsType.Local, "A", "Alpha");
            _variables.SetValue(VarsType.Local, "B", "%A%-Beta");
            _variables.SetValue(VarsType.Local, "C", "%B%-Gamma");
            _variables.SetValue(VarsType.Global, "Root", "%BaseDir%\\Root");

            _shortInput = "%BaseDir%\\Projects\\%ProjectName%\\%A%";
            _nestedInput = "%Root%\\%C%\\%B%\\%A%\\file.txt";
            _plainInput = "This string has no variable markers and should be a fast path.";
        }

        [Benchmark]
        public string ExpandPlain() => _variables.Expand(_plainInput);

        [Benchmark]
        public string ExpandShort() => _variables.Expand(_shortInput);

        [Benchmark]
        public string ExpandNested() => _variables.Expand(_nestedInput);
    }

    [MemoryDiagnoser]
    [ShortRunJob]
    public class ProjectLoadBench
    {
        [GlobalSetup]
        public void GlobalSetup()
        {
            CoreBenchFixture.EnsureInitialized();
        }

        [Benchmark]
        public int PrepareAndLoad()
        {
            ProjectCollection projects = new ProjectCollection(CoreBenchFixture.BaseDir);
            (int scriptCount, int linkCount) = projects.PrepareLoad();
            projects.Load(null, null);
            return projects.Count + scriptCount + linkCount;
        }
    }

    [MemoryDiagnoser]
    [ShortRunJob]
    public class IniBulkBench
    {
        private IniKey[] _readKeys = Array.Empty<IniKey>();
        private IniKey[] _writeKeys = Array.Empty<IniKey>();
        private string _workingFile = string.Empty;

        [GlobalSetup]
        public void GlobalSetup()
        {
            CoreBenchFixture.EnsureInitialized();

            _readKeys = Enumerable.Range(0, 256)
                .Select(i => new IniKey($"Section{i % 64}", $"Key{i % 16}"))
                .ToArray();
            _writeKeys = Enumerable.Range(0, 256)
                .Select(i => new IniKey($"Section{i % 64}", $"Key{i % 16}", $"Updated-{i}"))
                .ToArray();
            _workingFile = Path.Combine(CoreBenchFixture.BaseDir, "Bulk.Working.ini");
        }

        [IterationSetup(Target = nameof(WriteKeys))]
        public void ResetWriteFile()
        {
            File.Copy(CoreBenchFixture.IniTemplate, _workingFile, true);
        }

        [Benchmark]
        public IniKey[] ReadKeys() => IniReadWriter.ReadKeys(CoreBenchFixture.IniTemplate, _readKeys);

        [Benchmark]
        public bool WriteKeys() => IniReadWriter.WriteKeys(_workingFile, _writeKeys);
    }

    [MemoryDiagnoser]
    [ShortRunJob]
    public class LogExportBench
    {
        private Logger _logger = null!;
        private string _exportFile = string.Empty;
        private readonly BuildLogOptions _options = new BuildLogOptions
        {
            IncludeComments = false,
            IncludeMacros = false,
            ShowLogFlags = true,
        };

        [GlobalSetup]
        public void GlobalSetup()
        {
            CoreBenchFixture.EnsureInitialized();
            _logger = new Logger(":memory:");
            _exportFile = Path.Combine(CoreBenchFixture.BaseDir, "BuildLog.html");
            SeedLogDatabase(_logger.Db);
        }

        [Benchmark]
        public long ExportBuildLogText()
        {
            using (StreamWriter writer = new StreamWriter(_exportFile, false, Encoding.UTF8))
            {
                LogExporter exporter = new LogExporter(_logger.Db, LogExportFormat.Text, writer);
                exporter.ExportBuildLog(1, _options);
            }
            return new FileInfo(_exportFile).Length;
        }

        private static void SeedLogDatabase(LogDatabase db)
        {
            DateTime start = DateTime.UtcNow;
            db.Insert(new LogModel.BuildInfo
            {
                Id = 1,
                PEBakeryVersion = "bench",
                HostWindowsVersion = "bench",
                HostDotnetVersion = Environment.Version.ToString(),
                StartTime = start,
                FinishTime = start.AddSeconds(30),
                Name = "Benchmark Build",
            });

            List<LogModel.Script> scripts = new List<LogModel.Script>();
            List<LogModel.Variable> vars = new List<LogModel.Variable>();
            List<LogModel.BuildLog> logs = new List<LogModel.BuildLog>();

            int logId = 1;
            for (int scriptIdx = 1; scriptIdx <= 32; scriptIdx++)
            {
                scripts.Add(new LogModel.Script
                {
                    Id = scriptIdx,
                    BuildId = 1,
                    Order = scriptIdx,
                    Level = scriptIdx % 8,
                    Name = $"Script{scriptIdx:00}",
                    RealPath = $"C:\\Bench\\Script{scriptIdx:00}.script",
                    TreePath = $"Bench\\Script{scriptIdx:00}.script",
                    Version = "001",
                    StartTime = start.AddMilliseconds(scriptIdx),
                    FinishTime = start.AddMilliseconds(scriptIdx + 100),
                });

                for (int varIdx = 0; varIdx < 8; varIdx++)
                {
                    vars.Add(new LogModel.Variable
                    {
                        BuildId = 1,
                        ScriptId = scriptIdx,
                        Type = VarsType.Local,
                        Key = $"Var{varIdx}",
                        Value = $"Value-{scriptIdx}-{varIdx}",
                    });
                }

                for (int codeIdx = 0; codeIdx < 128; codeIdx++)
                {
                    LogState state = LogState.Success;
                    if (codeIdx % 97 == 0)
                        state = LogState.Warning;
                    if (codeIdx % 211 == 0)
                        state = LogState.Error;

                    logs.Add(new LogModel.BuildLog
                    {
                        Id = logId++,
                        Time = start.AddMilliseconds(logId),
                        BuildId = 1,
                        ScriptId = scriptIdx,
                        RefScriptId = 0,
                        Depth = codeIdx % 4,
                        State = state,
                        Message = $"Executed generated command {codeIdx}",
                        LineIdx = codeIdx + 1,
                        RawCode = $"Echo,Generated {codeIdx}",
                        Flags = codeIdx % 17 == 0 ? LogModel.BuildLogFlag.Comment : LogModel.BuildLogFlag.None,
                    });
                }
            }

            db.RunInTransaction(() =>
            {
                db.InsertAll(scripts);
                db.InsertAll(vars);
                db.InsertAll(logs);
            });
        }
    }
}
