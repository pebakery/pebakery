using RazorEngine;
using RazorEngine.Templating;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PEBakery.Core
{
    public class LogHtmlExporter
    {
        private LogDB DB;

        public LogHtmlExporter(LogDB DB)
        {
            this.DB = DB ?? throw new ArgumentNullException("DB");
        }

        public string ExportSystemLog()
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

            return RazorEngine.Engine.Razor.RunCompile(Properties.Resources.SystemLogHtmlTemplate, "SystemLogHtmlTemplateKey", null, m);
        }

        public string ExportBuildLog(long buildId)
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
            foreach (LogState state in Enum.GetValues(typeof(LogState)))
            {
                if (state == LogState.None || state == LogState.CriticalError)
                    continue;

                int count = DB.Table<DB_BuildLog>()
                    .Where(x => x.BuildId == buildId)
                    .Where(x => x.State == state)
                    .Count();

                m.LogStats.Add(new LogStatHtmlModel()
                {
                    State = state,
                    Count = count,
                });
            }

            // Plugins
            m.Plugins = new List<PluginHtmlModel>();
            {
                var plugins = DB.Table<DB_Plugin>()
                    .Where(x => x.BuildId == buildId)
                    .OrderBy(x => x.Order);

                int idx = 1;
                foreach (DB_Plugin pLog in plugins)
                {
                    m.Plugins.Add(new PluginHtmlModel()
                    {
                        Index = idx,
                        Name = pLog.Name,
                        Version = $"v{pLog.Version}",
                        TimeStr = $"{pLog.ElapsedMilliSec / 1000.0:0.000}s",
                    });
                    idx++;
                }
            }

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

            m.CodeLogs = new List<CodeLogHtmlModel>();
            {
                var codeLogs = DB.Table<DB_BuildLog>()
                    .Where(x => x.BuildId == buildId)
                    .OrderBy(x => x.Id);
                foreach (DB_BuildLog log in codeLogs)
                {
                    m.CodeLogs.Add(new CodeLogHtmlModel()
                    {
                        State = log.State,
                        Message = log.Export(LogExportType.Html),
                    });
                }
            }

            return RazorEngine.Engine.Razor.RunCompile(Properties.Resources.BuildLogHtmlTemplate, "BuildLogHtmlTemplateKey", null, m);
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
            public List<CodeLogHtmlModel> CodeLogs { get; set; }
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
        }
        #endregion
    }
}
