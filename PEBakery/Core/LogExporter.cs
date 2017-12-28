using RazorEngine;
using RazorEngine.Templating;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PEBakery.Core
{
    public class LogExporter
    {
        private LogDB DB;
        private LogExportType exportType;
        private StreamWriter w;

        public LogExporter(LogDB DB, LogExportType type, StreamWriter writer)
        {
            this.DB = DB ?? throw new ArgumentNullException("DB");
            this.w = writer ?? throw new ArgumentNullException("writer");
            this.exportType = type;
        }

        public void ExportSystemLog()
        {
            switch (exportType)
            {
                case LogExportType.Text:
                    {
                        w.WriteLine($"- PEBakery System Log -");
                        var logs = DB.Table<DB_SystemLog>().OrderBy(x => x.Time);
                        foreach (DB_SystemLog log in logs)
                        {
                            if (log.State == LogState.None)
                                w.WriteLine($"[{log.TimeStr}] {log.Message}");
                            else
                                w.WriteLine($"[{log.TimeStr}] [{log.State}] {log.Message}");
                        }
                    }
                    break;
                case LogExportType.Html:
                    {
                        ExportSystemLogHtmlModel m = new ExportSystemLogHtmlModel
                        {
                            PEBakeryVersion = Properties.Resources.StringVersion,
                            SysLogs = new List<SystemLogHtmlModel>(),
                        };

                        var logs = DB.Table<DB_SystemLog>().OrderBy(x => x.Time);
                        foreach (DB_SystemLog log in logs)
                        {
                            m.SysLogs.Add(new SystemLogHtmlModel()
                            {
                                TimeStr = log.TimeStr,
                                State = log.State,
                                Message = log.Message
                            });
                        }

                        string html = RazorEngine.Engine.Razor.RunCompile(Properties.Resources.SystemLogHtmlTemplate, "SystemLogHtmlTemplateKey", null, m);
                        w.WriteLine(html);
                    }
                    break;
            }
        }

        public void ExportBuildLog(long buildId)
        {
            switch (exportType)
            {
                #region Text
                case LogExportType.Text:
                    {
                        DB_BuildInfo dbBuild = DB.Table<DB_BuildInfo>().Where(x => x.Id == buildId).First();
                        if (dbBuild.EndTime == DateTime.MinValue)
                            dbBuild.EndTime = DateTime.UtcNow;

                        w.WriteLine($"- PEBakery Build <{dbBuild.Name}> -");
                        w.WriteLine($"Started at  {dbBuild.StartTime.ToLocalTime().ToString("yyyy-MM-dd h:mm:ss tt", CultureInfo.InvariantCulture)}");
                        w.WriteLine($"Finished at {dbBuild.EndTime.ToLocalTime().ToString("yyyy-MM-dd h:mm:ss tt", CultureInfo.InvariantCulture)}");
                        TimeSpan t = dbBuild.EndTime - dbBuild.StartTime;
                        w.WriteLine($"Took {t:h\\:mm\\:ss}");
                        w.WriteLine();
                        w.WriteLine();

                        w.WriteLine($"<Log Statistics>");
                        var states = ((LogState[])Enum.GetValues(typeof(LogState))).Where(x => x != LogState.None && x != LogState.CriticalError);
                        foreach (LogState state in states)
                        { 
                            int count = DB.Table<DB_BuildLog>()
                                .Where(x => x.BuildId == buildId)
                                .Where(x => x.State == state)
                                .Count();

                            w.WriteLine($"{state.ToString().PadRight(9)} : {count}");
                        }
                        w.WriteLine();
                        w.WriteLine();

                        // Show ErrorLogs
                        DB_BuildLog[] errors = DB.Table<DB_BuildLog>().Where(x => x.BuildId == buildId && x.State == LogState.Error).ToArray();
                        if (0 < errors.Length)
                        {
                            w.WriteLine("<Errors>");

                            long[] pLogIds = errors.Select(x => x.PluginId).Distinct().ToArray();
                            DB_Plugin[] pLogs = DB.Table<DB_Plugin>().Where(x => x.BuildId == buildId && pLogIds.Contains(x.Id)).ToArray();
                            foreach (DB_Plugin pLog in pLogs)
                            {
                                DB_BuildLog[] eLogs = errors.Where(x => x.PluginId == pLog.Id).ToArray();
                                if (eLogs.Length == 1)
                                    w.WriteLine($"- [{eLogs.Length}] Error in Plugin [{pLog.Name}] ({pLog.Path})");
                                else
                                    w.WriteLine($"- [{eLogs.Length}] Errors in Plugin [{pLog.Name}] ({pLog.Path})");

                                foreach (DB_BuildLog eLog in eLogs)
                                    w.WriteLine(eLog.Export(LogExportType.Text, false));
                                w.WriteLine();
                            }

                            w.WriteLine();
                        }

                        // Plugin
                        var plugins = DB.Table<DB_Plugin>()
                            .Where(x => x.BuildId == buildId)
                            .OrderBy(x => x.Order);
                        w.WriteLine("<Plugins>");
                        {
                            int count = plugins.Count();
                            int idx = 1;
                            foreach (DB_Plugin p in plugins)
                            {
                                w.WriteLine($"[{idx}/{count}] {p.Name} v{p.Version} ({p.ElapsedMilliSec / 1000.0:0.000}sec)");
                                idx++;
                            }

                            w.WriteLine($"Total {count} Plugins");
                            w.WriteLine();
                            w.WriteLine();
                        }

                        w.WriteLine("<Variables>");
                        VarsType[] typeList = new VarsType[] { VarsType.Fixed, VarsType.Global };
                        foreach (VarsType varsType in typeList)
                        {
                            w.WriteLine($"- {varsType} Variables");
                            var vars = DB.Table<DB_Variable>()
                                .Where(x => x.BuildId == buildId && x.Type == varsType)
                                .OrderBy(x => x.Key);
                            foreach (DB_Variable log in vars)
                                w.WriteLine($"%{log.Key}% = {log.Value}");
                            w.WriteLine();
                        }
                        w.WriteLine();

                        w.WriteLine("<Code Logs>");
                        {
                            foreach (DB_Plugin pLog in plugins)
                            {
                                // Log codes
                                var cLogs = DB.Table<DB_BuildLog>()
                                    .Where(x => x.BuildId == buildId && x.PluginId == pLog.Id)
                                    .OrderBy(x => x.Id);
                                foreach (DB_BuildLog log in cLogs)
                                    w.WriteLine(log.Export(LogExportType.Text));

                                // Log local variables
                                var vLogs = DB.Table<DB_Variable>()
                                    .Where(x => x.BuildId == buildId && x.PluginId == pLog.Id && x.Type == VarsType.Local)
                                    .OrderBy(x => x.Key);
                                if (0 < vLogs.Count())
                                {
                                    w.WriteLine($"- Local Variables of Plugin [{pLog.Name}]");
                                    foreach (DB_Variable vLog in vLogs)
                                        w.WriteLine($"%{vLog.Key}% = {vLog.Value}");
                                    w.WriteLine(Logger.LogSeperator);
                                }

                                w.WriteLine();
                            }
                        }
                    }
                    break;
                #endregion
                #region HTML
                case LogExportType.Html:
                    {
                        DB_BuildInfo dbBuild = DB.Table<DB_BuildInfo>().Where(x => x.Id == buildId).First();
                        if (dbBuild.EndTime == DateTime.MinValue)
                            dbBuild.EndTime = DateTime.UtcNow;
                        ExportBuildLogHtmlModel m = new ExportBuildLogHtmlModel()
                        {
                            PEBakeryVersion = Properties.Resources.StringVersion,
                            BuildName = dbBuild.Name,
                            BuildStartTimeStr = dbBuild.StartTime.ToLocalTime().ToString("yyyy-MM-dd h:mm:ss tt", CultureInfo.InvariantCulture),
                            BuildEndTimeStr = dbBuild.EndTime.ToLocalTime().ToString("yyyy-MM-dd h:mm:ss tt", CultureInfo.InvariantCulture),
                            BuildTookTimeStr = $"{(dbBuild.EndTime - dbBuild.StartTime):h\\:mm\\:ss}",
                        };

                        // Log Statistics
                        m.LogStats = new List<LogStatHtmlModel>();
                        var states = ((LogState[])Enum.GetValues(typeof(LogState))).Where(x => x != LogState.None && x != LogState.CriticalError);
                        foreach (LogState state in states)
                        {
                            int count = DB.Table<DB_BuildLog>()
                                .Where(x => x.BuildId == buildId && x.State == state)
                                .Count();

                            m.LogStats.Add(new LogStatHtmlModel()
                            {
                                State = state,
                                Count = count,
                            });
                        }

                        // Show ErrorLogs
                        m.ErrorCodeDicts = new Dictionary<PluginHtmlModel, CodeLogHtmlModel[]>();
                        {
                            int errIdx = 0;
                            DB_BuildLog[] errors = DB.Table<DB_BuildLog>().Where(x => x.BuildId == buildId && x.State == LogState.Error).ToArray();
                            if (0 < errors.Length)
                            {
                                long[] pLogIds = errors.Select(x => x.PluginId).Distinct().ToArray();
                                DB_Plugin[] pLogs = DB.Table<DB_Plugin>().Where(x => x.BuildId == buildId && pLogIds.Contains(x.Id)).ToArray();
                                foreach (DB_Plugin pLog in pLogs)
                                {
                                    PluginHtmlModel pModel = new PluginHtmlModel()
                                    {
                                        Name = pLog.Name,
                                        Path = pLog.Path,
                                    };
                                    m.ErrorCodeDicts[pModel] = errors
                                        .Where(x => x.PluginId == pLog.Id)
                                        .Select(x => new CodeLogHtmlModel()
                                        {
                                            State = x.State,
                                            Message = x.Export(LogExportType.Html, false),
                                            Href = (errIdx++),
                                        }).ToArray();
                                }
                            }
                        }

                        // Plugins
                        var plugins = DB.Table<DB_Plugin>()
                            .Where(x => x.BuildId == buildId)
                            .OrderBy(x => x.Order);
                        m.Plugins = new List<PluginHtmlModel>();
                        {
                            int idx = 1;
                            foreach (DB_Plugin pLog in plugins)
                            {
                                m.Plugins.Add(new PluginHtmlModel()
                                {
                                    Index = idx,
                                    Name = pLog.Name,
                                    Path = pLog.Path,
                                    Version = $"v{pLog.Version}",
                                    TimeStr = $"{pLog.ElapsedMilliSec / 1000.0:0.000}s",
                                });
                                idx++;
                            }
                        }

                        // Variables
                        m.Vars = new List<VarHtmlModel>();
                        {
                            var vars = DB.Table<DB_Variable>()
                                        .Where(x => x.BuildId == buildId && (x.Type == VarsType.Fixed || x.Type == VarsType.Global))
                                        .OrderBy(x => x.Type)
                                        .ThenBy(x => x.Key);
                            foreach (DB_Variable vLog in vars)
                            {
                                m.Vars.Add(new VarHtmlModel()
                                {
                                    Type = vLog.Type,
                                    Key = vLog.Key,
                                    Value = vLog.Value,
                                });
                            }
                        }

                        // CodeLogs
                        m.CodeLogs = new List<Tuple<PluginHtmlModel, CodeLogHtmlModel[], VarHtmlModel[]>>();
                        {
                            int pIdx = 0;
                            int errIdx = 0;

                            foreach (DB_Plugin pLog in plugins)
                            {
                                pIdx += 1;

                                // Log codes
                                DB_BuildLog[] codeLogs = DB.Table<DB_BuildLog>()
                                    .Where(x => x.BuildId == buildId && x.PluginId == pLog.Id)
                                    .OrderBy(x => x.Id).ToArray();

                                PluginHtmlModel pModel = new PluginHtmlModel()
                                {
                                    Index = pIdx,
                                    Name = pLog.Name,
                                    Path = pLog.Path,
                                };

                                List<CodeLogHtmlModel> logModel = new List<CodeLogHtmlModel>(codeLogs.Length);
                                foreach (DB_BuildLog log in codeLogs)
                                {
                                    if (log.State == LogState.Error)
                                    {
                                        logModel.Add(new CodeLogHtmlModel()
                                        {
                                            State = log.State,
                                            Message = log.Export(LogExportType.Html),
                                            Href = (errIdx++),
                                        });
                                    }
                                    else
                                    {
                                        logModel.Add(new CodeLogHtmlModel()
                                        {
                                            State = log.State,
                                            Message = log.Export(LogExportType.Html),
                                        });
                                    }
                                }

                                // Log local variables
                                VarHtmlModel[] localVarModel = DB.Table<DB_Variable>()
                                    .Where(x => x.BuildId == buildId && x.PluginId == pLog.Id && x.Type == VarsType.Local)
                                    .OrderBy(x => x.Key)
                                    .Select(x => new VarHtmlModel()
                                    {
                                        Type = x.Type,
                                        Key = x.Key,
                                        Value = x.Value,
                                    }).ToArray();

                                m.CodeLogs.Add(new Tuple<PluginHtmlModel, CodeLogHtmlModel[], VarHtmlModel[]>(pModel, logModel.ToArray(), localVarModel));
                            }                            
                        }

                        string html = RazorEngine.Engine.Razor.RunCompile(Properties.Resources.BuildLogHtmlTemplate, "BuildLogHtmlTemplateKey", null, m);
                        w.WriteLine(html);
                    }
                    break;
                    #endregion
            }
        }

        #region HtmlModel
        public class ExportSystemLogHtmlModel
        {
            public string PEBakeryVersion { get; set; }
            public List<SystemLogHtmlModel> SysLogs { get; set; }
        }

        public class SystemLogHtmlModel
        {
            public string TimeStr { get; set; }
            public LogState State { get; set; }
            public string Message { get; set; }
        }

        public class ExportBuildLogHtmlModel
        {
            public string PEBakeryVersion { get; set; }
            public string BuildName { get; set; }
            public string BuildStartTimeStr { get; set; }
            public string BuildEndTimeStr { get; set; }
            public string BuildTookTimeStr { get; set; }
            public List<LogStatHtmlModel> LogStats { get; set; }
            public List<PluginHtmlModel> Plugins { get; set; }
            public List<VarHtmlModel> Vars { get; set; }
            public Dictionary<PluginHtmlModel, CodeLogHtmlModel[]> ErrorCodeDicts { get; set; }
            public List<Tuple<PluginHtmlModel, CodeLogHtmlModel[], VarHtmlModel[]>> CodeLogs { get; set; }
            // public List<CodeLogHtmlModel> CodeLogs { get; set; }
        }

        public class LogStatHtmlModel
        {
            public LogState State { get; set; }
            public int Count { get; set; }
        }

        public class PluginHtmlModel
        {
            public int Index { get; set; }
            public string Name { get; set; }
            public string Path { get; set; }
            public string Version { get; set; }
            public string TimeStr { get; set; }
        }

        public class VarHtmlModel
        {
            public VarsType Type { get; set; }
            public string Key { get; set; }
            public string Value { get; set; }
        }

        public class CodeLogHtmlModel
        {
            public LogState State { get; set; }
            public string Message { get; set; }
            public int Href { get; set; } // Optional
        }
        #endregion
    }
}
