/*
    Copyright (C) 2016-2017 Hajin Jang
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
            this.DB = DB ?? throw new ArgumentNullException(nameof(DB));
            this.w = writer ?? throw new ArgumentNullException(nameof(writer));
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

                            long[] pLogIds = errors.Select(x => x.ScriptId).Distinct().ToArray();
                            DB_Script[] scLogs = DB.Table<DB_Script>().Where(x => x.BuildId == buildId && pLogIds.Contains(x.Id)).ToArray();
                            foreach (DB_Script scLog in scLogs)
                            {
                                DB_BuildLog[] eLogs = errors.Where(x => x.ScriptId == scLog.Id).ToArray();
                                if (eLogs.Length == 1)
                                    w.WriteLine($"- [{eLogs.Length}] Error in Script [{scLog.Name}] ({scLog.Path})");
                                else
                                    w.WriteLine($"- [{eLogs.Length}] Errors in Script [{scLog.Name}] ({scLog.Path})");

                                foreach (DB_BuildLog eLog in eLogs)
                                    w.WriteLine(eLog.Export(LogExportType.Text, false));
                                w.WriteLine();
                            }

                            w.WriteLine();
                        }

                        // Show WarnLogs
                        DB_BuildLog[] warns = DB.Table<DB_BuildLog>().Where(x => x.BuildId == buildId && x.State == LogState.Warning).ToArray();
                        if (0 < errors.Length)
                        {
                            w.WriteLine("<Warnings>");

                            long[] pLogIds = warns.Select(x => x.ScriptId).Distinct().ToArray();
                            DB_Script[] scLogs = DB.Table<DB_Script>().Where(x => x.BuildId == buildId && pLogIds.Contains(x.Id)).ToArray();
                            foreach (DB_Script scLog in scLogs)
                            {
                                DB_BuildLog[] wLogs = warns.Where(x => x.ScriptId == scLog.Id).ToArray();
                                if (wLogs.Length == 1)
                                    w.WriteLine($"- [{wLogs.Length}] Warning in Script [{scLog.Name}] ({scLog.Path})");
                                else
                                    w.WriteLine($"- [{wLogs.Length}] Warnings in Script [{scLog.Name}] ({scLog.Path})");

                                foreach (DB_BuildLog eLog in wLogs)
                                    w.WriteLine(eLog.Export(LogExportType.Text, false));
                                w.WriteLine();
                            }

                            w.WriteLine();
                        }

                        // Script
                        var scripts = DB.Table<DB_Script>()
                            .Where(x => x.BuildId == buildId)
                            .OrderBy(x => x.Order);
                        w.WriteLine("<Scripts>");
                        {
                            int count = scripts.Count();
                            int idx = 1;
                            foreach (DB_Script sc in scripts)
                            {
                                w.WriteLine($"[{idx}/{count}] {sc.Name} v{sc.Version} ({sc.ElapsedMilliSec / 1000.0:0.000}sec)");
                                idx++;
                            }

                            w.WriteLine($"Total {count} Scripts");
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
                            foreach (DB_Script scLog in scripts)
                            {
                                // Log codes
                                var cLogs = DB.Table<DB_BuildLog>()
                                    .Where(x => x.BuildId == buildId && x.ScriptId == scLog.Id)
                                    .OrderBy(x => x.Id);
                                foreach (DB_BuildLog log in cLogs)
                                    w.WriteLine(log.Export(LogExportType.Text));

                                // Log local variables
                                var vLogs = DB.Table<DB_Variable>()
                                    .Where(x => x.BuildId == buildId && x.ScriptId == scLog.Id && x.Type == VarsType.Local)
                                    .OrderBy(x => x.Key);
                                if (vLogs.Any())
                                {
                                    w.WriteLine($"- Local Variables of Script [{scLog.Name}]");
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
                        m.ErrorCodeDicts = new Dictionary<ScriptHtmlModel, CodeLogHtmlModel[]>();
                        {
                            int errIdx = 0;
                            DB_BuildLog[] errors = DB.Table<DB_BuildLog>().Where(x => x.BuildId == buildId && x.State == LogState.Error).ToArray();
                            if (0 < errors.Length)
                            {
                                long[] pLogIds = errors.Select(x => x.ScriptId).Distinct().ToArray();
                                DB_Script[] scLogs = DB.Table<DB_Script>().Where(x => x.BuildId == buildId && pLogIds.Contains(x.Id)).ToArray();
                                foreach (DB_Script scLog in scLogs)
                                {
                                    ScriptHtmlModel pModel = new ScriptHtmlModel()
                                    {
                                        Name = scLog.Name,
                                        Path = scLog.Path,
                                    };
                                    m.ErrorCodeDicts[pModel] = errors
                                        .Where(x => x.ScriptId == scLog.Id)
                                        .Select(x => new CodeLogHtmlModel()
                                        {
                                            State = x.State,
                                            Message = x.Export(LogExportType.Html, false),
                                            Href = (errIdx++),
                                        }).ToArray();
                                }
                            }
                        }

                        // Show WarnLogs
                        m.WarnCodeDicts = new Dictionary<ScriptHtmlModel, CodeLogHtmlModel[]>();
                        {
                            int warnIdx = 0;
                            DB_BuildLog[] warns = DB.Table<DB_BuildLog>().Where(x => x.BuildId == buildId && x.State == LogState.Warning).ToArray();
                            if (0 < warns.Length)
                            {
                                long[] pLogIds = warns.Select(x => x.ScriptId).Distinct().ToArray();
                                DB_Script[] scLogs = DB.Table<DB_Script>().Where(x => x.BuildId == buildId && pLogIds.Contains(x.Id)).ToArray();
                                foreach (DB_Script scLog in scLogs)
                                {
                                    ScriptHtmlModel pModel = new ScriptHtmlModel()
                                    {
                                        Name = scLog.Name,
                                        Path = scLog.Path,
                                    };
                                    m.WarnCodeDicts[pModel] = warns
                                        .Where(x => x.ScriptId == scLog.Id)
                                        .Select(x => new CodeLogHtmlModel()
                                        {
                                            State = x.State,
                                            Message = x.Export(LogExportType.Html, false),
                                            Href = (warnIdx++),
                                        }).ToArray();
                                }
                            }
                        }

                        // Scripts
                        var scripts = DB.Table<DB_Script>()
                            .Where(x => x.BuildId == buildId)
                            .OrderBy(x => x.Order);
                        m.Scripts = new List<ScriptHtmlModel>();
                        {
                            int idx = 1;
                            foreach (DB_Script scLog in scripts)
                            {
                                m.Scripts.Add(new ScriptHtmlModel()
                                {
                                    Index = idx,
                                    Name = scLog.Name,
                                    Path = scLog.Path,
                                    Version = $"v{scLog.Version}",
                                    TimeStr = $"{scLog.ElapsedMilliSec / 1000.0:0.000}s",
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
                        m.CodeLogs = new List<Tuple<ScriptHtmlModel, CodeLogHtmlModel[], VarHtmlModel[]>>();
                        {
                            int pIdx = 0;
                            int errIdx = 0;
                            int warnIdx = 0;

                            foreach (DB_Script scLog in scripts)
                            {
                                pIdx += 1;

                                // Log codes
                                DB_BuildLog[] codeLogs = DB.Table<DB_BuildLog>()
                                    .Where(x => x.BuildId == buildId && x.ScriptId == scLog.Id)
                                    .OrderBy(x => x.Id).ToArray();

                                ScriptHtmlModel pModel = new ScriptHtmlModel()
                                {
                                    Index = pIdx,
                                    Name = scLog.Name,
                                    Path = scLog.Path,
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
                                    else if (log.State == LogState.Warning)
                                    {
                                        logModel.Add(new CodeLogHtmlModel()
                                        {
                                            State = log.State,
                                            Message = log.Export(LogExportType.Html),
                                            Href = (warnIdx++),
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
                                    .Where(x => x.BuildId == buildId && x.ScriptId == scLog.Id && x.Type == VarsType.Local)
                                    .OrderBy(x => x.Key)
                                    .Select(x => new VarHtmlModel()
                                    {
                                        Type = x.Type,
                                        Key = x.Key,
                                        Value = x.Value,
                                    }).ToArray();

                                m.CodeLogs.Add(new Tuple<ScriptHtmlModel, CodeLogHtmlModel[], VarHtmlModel[]>(pModel, logModel.ToArray(), localVarModel));
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
            public List<ScriptHtmlModel> Scripts { get; set; }
            public List<VarHtmlModel> Vars { get; set; }
            public Dictionary<ScriptHtmlModel, CodeLogHtmlModel[]> ErrorCodeDicts { get; set; }
            public Dictionary<ScriptHtmlModel, CodeLogHtmlModel[]> WarnCodeDicts { get; set; }
            public List<Tuple<ScriptHtmlModel, CodeLogHtmlModel[], VarHtmlModel[]>> CodeLogs { get; set; }
            // public List<CodeLogHtmlModel> CodeLogs { get; set; }
        }

        public class LogStatHtmlModel
        {
            public LogState State { get; set; }
            public int Count { get; set; }
        }

        public class ScriptHtmlModel
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
