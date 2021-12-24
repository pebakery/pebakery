/*
    Copyright (C) 2016-2022 Hajin Jang
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

using PEBakery.Core.Html;
using PEBakery.Helper;
using Scriban.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace PEBakery.Core
{
    public class LogExporter
    {
        #region Fields and Constructors
        private readonly LogDatabase _db;
        private readonly LogExportType _exportType;
        private readonly TextWriter _w;

        public LogExporter(LogDatabase db, LogExportType type, TextWriter writer)
        {
            // The responsibility of closing _db and _w goes to the caller of LogExporter
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _w = writer ?? throw new ArgumentNullException(nameof(writer));
            _exportType = type;
        }
        #endregion

        #region ExportSystemLog
        public void ExportSystemLog()
        {
            switch (_exportType)
            {
                case LogExportType.Text:
                    {
                        _w.WriteLine("- PEBakery System Log -");
                        _w.WriteLine($"Exported by PEBakery {Global.Const.ProgramVersionStrFull}");
                        _w.WriteLine($"Exported at {DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss tt K", CultureInfo.InvariantCulture)}");
                        _w.WriteLine();
                        var logs = _db.Table<LogModel.SystemLog>().OrderBy(x => x.Time);
                        foreach (LogModel.SystemLog log in logs)
                        {
                            if (log.State == LogState.None)
                                _w.WriteLine($"[{log.TimeStr}] {log.Message}");
                            else
                                _w.WriteLine($"[{log.TimeStr}] [{log.State}] {log.Message}");
                        }
                    }
                    break;
                case LogExportType.Html:
                    {
                        Assembly assembly = Assembly.GetExecutingAssembly();
                        SystemLogModel m = new SystemLogModel
                        {
                            // Information
                            HeadTitle = "PEBakery System Log",
                            ExportEngineVersion = Global.Const.ProgramVersionStrFull,
                            ExportTimeStr = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss tt K", CultureInfo.InvariantCulture),
                            // Embed
                            EmbedBootstrapCss = ResourceHelper.GetEmbeddedResourceString("Html.bootstrap.min.css", assembly),
                            EmbedJQuerySlimJs = ResourceHelper.GetEmbeddedResourceString("Html.jquery.slim.min.js", assembly),
                            EmbedBootstrapJs = ResourceHelper.GetEmbeddedResourceString("Html.bootstrap.bundle.min.js", assembly),
                            // Data
                        };

                        foreach (LogModel.SystemLog log in _db.Table<LogModel.SystemLog>().OrderBy(x => x.Time))
                        {
                            m.SysLogs.AddItem(new SystemLogItem
                            {
                                TimeStr = log.TimeStr,
                                State = log.State,
                                Message = log.Message
                            });
                        }

                        HtmlRenderer.RenderHtmlAsync("Html._SystemLogView.sbnhtml", assembly, m, _w).Wait();
                    }
                    break;
            }
        }
        #endregion

        #region ExportBuildLog
        public void ExportBuildLog(int buildId, BuildLogOptions opts)
        {
            switch (_exportType)
            {
                #region Text
                case LogExportType.Text:
                    {
                        LogModel.BuildInfo dbBuild = _db.Table<LogModel.BuildInfo>().First(x => x.Id == buildId);
                        _w.WriteLine($"- PEBakery Build <{dbBuild.Name}> -");
                        _w.WriteLine($"Built    by PEBakery {dbBuild.PEBakeryVersion}");
                        _w.WriteLine($"Exported by PEBakery {Global.Const.ProgramVersionStrFull}");
                        _w.WriteLine();
                        _w.WriteLine($"Started  at {dbBuild.StartTime.ToLocalTime().ToString("yyyy-MM-dd hh:mm:ss tt K", CultureInfo.InvariantCulture)}");
                        if (dbBuild.FinishTime != DateTime.MinValue)
                        { // Put these lines only if a build successfully finished
                            _w.WriteLine($"Finished at {dbBuild.FinishTime.ToLocalTime().ToString("yyyy-MM-dd hh:mm:ss tt K", CultureInfo.InvariantCulture)}");
                            TimeSpan elapsed = dbBuild.FinishTime - dbBuild.StartTime;
                            _w.WriteLine($"Took {elapsed:h\\:mm\\:ss}");
                        }
                        _w.WriteLine();
                        _w.WriteLine();

                        // Log Statistics
                        _w.WriteLine("<Log Statistics>");
                        var states = ((LogState[])Enum.GetValues(typeof(LogState))).Where(x => x != LogState.None && x != LogState.CriticalError);
                        foreach (LogState state in states)
                        {
                            int count = _db.Table<LogModel.BuildLog>().Count(x => x.BuildId == buildId && x.State == state);
                            _w.WriteLine($"{state,-10}: {count}");
                        }
                        _w.WriteLine();
                        _w.WriteLine();

                        // Show ErrorLogs
                        LogModel.BuildLog[] errors = _db.Table<LogModel.BuildLog>().Where(x => x.BuildId == buildId && x.State == LogState.Error).ToArray();
                        if (0 < errors.Length)
                        {
                            _w.WriteLine("<Errors>");

                            int[] scLogIds = errors.Select(x => x.ScriptId).OrderBy(x => x).Distinct().ToArray();
                            int[] refScLogIds = errors.Select(x => x.RefScriptId).OrderBy(x => x).Distinct().ToArray();
                            LogModel.Script[] scLogs = _db.Table<LogModel.Script>()
                                .Where(x => x.BuildId == buildId && scLogIds.Contains(x.Id))
                                .ToArray();
                            LogModel.Script[] scOriginLogs = _db.Table<LogModel.Script>()
                                .Where(x => x.BuildId == buildId && (scLogIds.Contains(x.Id) || refScLogIds.Contains(x.Id)))
                                .ToArray();
                            foreach (LogModel.Script scLog in scLogs)
                            {
                                LogModel.BuildLog[] eLogs = errors.Where(x => x.ScriptId == scLog.Id).ToArray();
                                if (eLogs.Length == 1)
                                    _w.WriteLine($"- [{eLogs.Length}] Error in script [{scLog.Name}] ({scLog.TreePath})");
                                else
                                    _w.WriteLine($"- [{eLogs.Length}] Errors in script [{scLog.Name}] ({scLog.TreePath})");

                                foreach (LogModel.BuildLog eLog in eLogs)
                                {
                                    _w.WriteLine(eLog.Export(LogExportType.Text, false, false));

                                    string refScriptText = ExportRefScriptText(eLog, scOriginLogs);
                                    if (refScriptText != null)
                                    { // Referenced scripts
                                        _w.Write("  ");
                                        _w.WriteLine(refScriptText);
                                    }
                                }
                                _w.WriteLine();
                            }

                            _w.WriteLine();
                        }

                        // Show WarnLogs
                        LogModel.BuildLog[] warns = _db.Table<LogModel.BuildLog>().Where(x => x.BuildId == buildId && x.State == LogState.Warning).ToArray();
                        if (0 < warns.Length)
                        {
                            _w.WriteLine("<Warnings>");

                            int[] scLogIds = warns.Select(x => x.ScriptId).OrderBy(x => x).Distinct().ToArray();
                            int[] refScLogIds = warns.Select(x => x.RefScriptId).OrderBy(x => x).Distinct().ToArray();
                            LogModel.Script[] scLogs = _db.Table<LogModel.Script>()
                                .Where(x => x.BuildId == buildId && scLogIds.Contains(x.Id))
                                .ToArray();
                            LogModel.Script[] scOriginLogs = _db.Table<LogModel.Script>()
                                .Where(x => x.BuildId == buildId && refScLogIds.Contains(x.Id))
                                // .Where(x => x.BuildId == buildId && (scLogIds.Contains(x.Id) || refScLogIds.Contains(x.Id)))
                                .ToArray();

                            foreach (LogModel.Script scLog in scLogs)
                            {
                                LogModel.BuildLog[] wLogs = warns.Where(x => x.ScriptId == scLog.Id).ToArray();
                                Debug.Assert(0 < wLogs.Length);

                                if (wLogs.Length == 1)
                                    _w.WriteLine($"- [{wLogs.Length}] Warning in script [{scLog.Name}] ({scLog.TreePath})");
                                else
                                    _w.WriteLine($"- [{wLogs.Length}] Warnings in script [{scLog.Name}] ({scLog.TreePath})");

                                foreach (LogModel.BuildLog wLog in wLogs)
                                {
                                    _w.WriteLine(wLog.Export(LogExportType.Text, false, false));

                                    string refScriptText = ExportRefScriptText(wLog, scOriginLogs);
                                    if (refScriptText != null)
                                    { // Referenced scripts
                                        _w.Write("  ");
                                        _w.WriteLine(refScriptText);
                                    }
                                }
                                _w.WriteLine();
                            }

                            _w.WriteLine();
                        }

                        // Script
                        LogModel.Script[] scripts = _db.Table<LogModel.Script>()
                            .Where(x => x.BuildId == buildId)
                            .ToArray();

                        // Script - Processed Scripts
                        LogModel.Script[] processedScripts = scripts
                            .Where(x => 0 < x.Order)
                            .OrderBy(x => x.Order)
                            .ToArray();
                        int pathColumnPos;
                        _w.WriteLine("<Scripts>");
                        {
                            (string Title, string Elapsed, string Path)[] scriptStrs = new (string, string, string)[processedScripts.Length];
                            for (int i = 0; i < processedScripts.Length; i++)
                            {
                                LogModel.Script sc = processedScripts[i];

                                string titleStr = $"[{i + 1,3}/{processedScripts.Length,3}] {sc.Name} v{sc.Version}";
                                string elapsedStr;
                                if (sc.FinishTime != DateTime.MinValue)
                                {
                                    TimeSpan elapsed = sc.FinishTime - sc.StartTime;
                                    elapsedStr = $"{elapsed.TotalSeconds:0.0}s";
                                }
                                else
                                {
                                    elapsedStr = "-";
                                }

                                scriptStrs[i] = (titleStr, elapsedStr, sc.TreePath);
                            }

                            int titleMaxLen = scriptStrs.Max(x => x.Title.Length);
                            int elapsedMaxLen = scriptStrs.Max(x => x.Elapsed.Length);
                            pathColumnPos = titleMaxLen + elapsedMaxLen + 3;
                            foreach ((string title, string elapsed, string path) in scriptStrs)
                            {
                                _w.WriteLine($"{title.PadRight(titleMaxLen)} | {elapsed.PadLeft(elapsedMaxLen)} | {path}");
                            }

                            _w.WriteLine();
                            _w.WriteLine();
                        }

                        // Script - Referenced Scripts
                        LogModel.Script[] refScripts = scripts
                            .Where(x => x.Order <= 0)
                            .OrderBy(x => x.Order) // Put macro script first
                            .ThenBy(x => x.StartTime)
                            .ToArray();
                        _w.WriteLine("<Referenced Scripts>");
                        {
                            int idx = 1;
                            int refScriptCount = refScripts.Count(x => x.Order == 0); // Exclude macro script from counting
                            List<(string Title, string Path)> scriptStrs = new List<(string, string)>(refScripts.Length);
                            foreach (LogModel.Script sc in refScripts)
                            {
                                string idxStr;
                                if (sc.Order == -1)
                                {
                                    idxStr = "[ Macro ]";
                                }
                                else
                                {
                                    idxStr = $"[{idx,3}/{refScriptCount,3}]";
                                    idx += 1;
                                }

                                string titleStr = $"{idxStr} {sc.Name} v{sc.Version}";
                                pathColumnPos = Math.Max(titleStr.Length, pathColumnPos);
                                scriptStrs.Add((titleStr, sc.TreePath));
                            }

                            foreach ((string title, string path) in scriptStrs)
                            {
                                _w.WriteLine($"{title.PadRight(pathColumnPos)} | {path}");
                            }

                            _w.WriteLine();
                            _w.WriteLine();
                        }

                        // Variables
                        _w.WriteLine("<Variables>");
                        VarsType[] typeList = { VarsType.Fixed, VarsType.Global };
                        foreach (VarsType varsType in typeList)
                        {
                            _w.WriteLine($"- {varsType} Variables");
                            var vars = _db.Table<LogModel.Variable>()
                                .Where(x => x.BuildId == buildId && x.Type == varsType)
                                .OrderBy(x => x.Key);
                            foreach (LogModel.Variable log in vars)
                                _w.WriteLine($"%{log.Key}% = {log.Value}");
                            _w.WriteLine();
                        }
                        _w.WriteLine();

                        // Code Logs
                        _w.WriteLine("<Code Logs>");
                        {
                            foreach (LogModel.Script scLog in processedScripts)
                            {
                                // Log codes
                                var cLogs = _db.Table<LogModel.BuildLog>().Where(x => x.BuildId == buildId && x.ScriptId == scLog.Id);
                                if (!opts.IncludeComments)
                                    cLogs = cLogs.Where(x => (x.Flags & LogModel.BuildLogFlag.Comment) != LogModel.BuildLogFlag.Comment);
                                if (!opts.IncludeMacros)
                                    cLogs = cLogs.Where(x => (x.Flags & LogModel.BuildLogFlag.Macro) != LogModel.BuildLogFlag.Macro);
                                cLogs = cLogs.OrderBy(x => x.Id);
                                foreach (LogModel.BuildLog log in cLogs)
                                    _w.WriteLine(log.Export(LogExportType.Text, true, opts.ShowLogFlags));

                                // Log local variables
                                var vLogs = _db.Table<LogModel.Variable>()
                                    .Where(x => x.BuildId == buildId && x.ScriptId == scLog.Id && x.Type == VarsType.Local)
                                    .OrderBy(x => x.Key);
                                if (vLogs.Any())
                                {
                                    _w.WriteLine($"- Local Variables of Script [{scLog.Name}]");
                                    foreach (LogModel.Variable vLog in vLogs)
                                        _w.WriteLine($"%{vLog.Key}% = {vLog.Value}");
                                    _w.WriteLine(Logger.LogSeparator);
                                }

                                _w.WriteLine();
                            }
                        }
                    }
                    break;
                #endregion
                #region HTML
                case LogExportType.Html:
                    {
                        LogModel.BuildInfo dbBuild = _db.Table<LogModel.BuildInfo>().First(x => x.Id == buildId);
                        if (dbBuild.FinishTime == DateTime.MinValue)
                            dbBuild.FinishTime = DateTime.UtcNow;

                        Assembly assembly = Assembly.GetExecutingAssembly();
                        BuildLogModel m = new BuildLogModel
                        {
                            // Information
                            BuiltEngineVersion = dbBuild.PEBakeryVersion,
                            ExportEngineVersion = Global.Const.ProgramVersionStrFull,
                            HeadTitle = dbBuild.Name,
                            BuildStartTimeStr = dbBuild.StartTime.ToLocalTime().ToString("yyyy-MM-dd h:mm:ss tt K", CultureInfo.InvariantCulture),
                            BuildEndTimeStr = dbBuild.FinishTime.ToLocalTime().ToString("yyyy-MM-dd h:mm:ss tt K", CultureInfo.InvariantCulture),
                            BuildTookTimeStr = $"{dbBuild.FinishTime - dbBuild.StartTime:h\\:mm\\:ss}",
                            ShowLogFlags = opts.ShowLogFlags,
                            // Embed
                            EmbedBootstrapCss = ResourceHelper.GetEmbeddedResourceString("Html.bootstrap.min.css", assembly),
                            EmbedJQuerySlimJs = ResourceHelper.GetEmbeddedResourceString("Html.jquery.slim.min.js", assembly),
                            EmbedBootstrapJs = ResourceHelper.GetEmbeddedResourceString("Html.bootstrap.bundle.min.js", assembly),
                            // Data
                            // LogStats = new List<LogStatItem>(),
                        };

                        // Log Statistics
                        var states = ((LogState[])Enum.GetValues(typeof(LogState))).Where(x => x != LogState.None && x != LogState.CriticalError);
                        foreach (LogState state in states)
                        {
                            int count = _db.Table<LogModel.BuildLog>().Count(x => x.BuildId == buildId && x.State == state);

                            // type: LogStatItem[]
                            m.LogStats.AddItem(new LogStatItem
                            {
                                State = state,
                                Count = count,
                            });
                        }

                        // Show ErrorLogs
                        // m.ErrorCodeDict = new Dictionary<SriptLogItem, Tuple<CodeLogItem, string>[]>();
                        void BuildErrorWarnLogs(ScriptArray dest, LogState target)
                        {
                            int targetIdx = 0;
                            LogModel.BuildLog[] targetLogs = _db.Table<LogModel.BuildLog>().Where(x => x.BuildId == buildId && x.State == target).ToArray();
                            if (0 < targetLogs.Length)
                            {
                                int[] scLogIds = targetLogs.Select(x => x.ScriptId).OrderBy(x => x).Distinct().ToArray();
                                int[] refScLogIds = targetLogs.Select(x => x.RefScriptId).OrderBy(x => x).Distinct().ToArray();
                                LogModel.Script[] scLogs = _db.Table<LogModel.Script>()
                                    .Where(x => x.BuildId == buildId && scLogIds.Contains(x.Id))
                                    .ToArray();
                                LogModel.Script[] scOriginLogs = _db.Table<LogModel.Script>()
                                    .Where(x => x.BuildId == buildId && (scLogIds.Contains(x.Id) || refScLogIds.Contains(x.Id)))
                                    .ToArray();

                                foreach (LogModel.Script scLog in scLogs)
                                {
                                    /* type: [
                                        { 
                                            ScriptName = string,
                                            ScriptPath = string,
                                            Codes = [{
                                                State = string,
                                                Message = string,
                                                Href = string,
                                                RefScriptMsg = string,
                                            }]
                                        }, ...
                                    ] */
                                    ScriptArray codeArr = new ScriptArray();
                                    foreach (LogModel.BuildLog targetLog in targetLogs.Where(x => x.ScriptId == scLog.Id))
                                    {
                                        CodeLogItem logItem = new CodeLogItem()
                                        {
                                            State = targetLog.State,
                                            Message = targetLog.Export(LogExportType.Html, false, false),
                                            Href = targetIdx++,
                                            RefScriptMsg = ExportRefScriptText(targetLog, scOriginLogs)
                                        };
                                        ScriptObject logObj = new ScriptObject();
                                        logObj.Import(logItem, renamer: HtmlRenderer.ScribanObjectRenamer);

                                        codeArr.Add(logObj);
                                    }

                                    ScriptObject itemRoot = new ScriptObject
                                    {
                                        ["ScriptName"] = scLog.Name,
                                        ["ScriptPath"] = scLog.TreePath,
                                        ["Codes"] = codeArr,
                                    };
                                    dest.Add(itemRoot);
                                }
                            }
                        }

                        BuildErrorWarnLogs(m.ErrorSummaries, LogState.Error);
                        BuildErrorWarnLogs(m.WarnSummaries, LogState.Warning);

                        // Scripts - Build Scripts
                        var scripts = _db.Table<LogModel.Script>()
                            .Where(x => x.BuildId == buildId && 0 < x.Order)
                            .OrderBy(x => x.Order);
                        {
                            int idx = 1;
                            foreach (LogModel.Script scLog in scripts)
                            {
                                TimeSpan elapsed = scLog.FinishTime - scLog.StartTime;

                                // type: ScriptLogItem[]
                                m.Scripts.AddItem(new ScriptLogItem
                                {
                                    IndexStr = idx.ToString(),
                                    Name = scLog.Name,
                                    Path = scLog.TreePath,
                                    Version = $"v{scLog.Version}",
                                    TimeStr = $"{elapsed.TotalSeconds:0.0}s",
                                });
                                idx++;
                            }
                        }

                        // Script - Referenced Scripts
                        {
                            var refScripts = _db.Table<LogModel.Script>()
                                .Where(x => x.BuildId == buildId && x.Order <= 0)
                                .OrderBy(x => x.Order) // Put macro script first
                                .ThenBy(x => x.StartTime);

                            int idx = 0;
                            foreach (LogModel.Script scLog in refScripts)
                            {
                                string idxStr;
                                if (scLog.Order == -1)
                                {
                                    idxStr = "Macro";
                                }
                                else
                                {
                                    idx += 1;
                                    idxStr = idx.ToString();
                                }

                                // type: ScriptLogItem[]
                                m.RefScripts.AddItem(new ScriptLogItem
                                {
                                    IndexStr = idxStr,
                                    Name = scLog.Name,
                                    Path = scLog.TreePath,
                                    Version = $"v{scLog.Version}",
                                });
                            }
                        }

                        // Variables
                        {
                            var vars = _db.Table<LogModel.Variable>()
                                        .Where(x => x.BuildId == buildId && (x.Type == VarsType.Fixed || x.Type == VarsType.Global))
                                        .OrderBy(x => x.Type)
                                        .ThenBy(x => x.Key);
                            foreach (LogModel.Variable vLog in vars)
                            {
                                // type: VariableLogItem[]
                                m.Variables.AddItem(new VariableLogItem
                                {
                                    Type = vLog.Type,
                                    Key = vLog.Key,
                                    Value = vLog.Value,
                                });
                            }
                        }

                        // CodeLogs
                        {
                            int pIdx = 0;
                            int errIdx = 0;
                            int warnIdx = 0;

                            // Script Title Dict for script origin
                            Dictionary<int, string> scTitleDict = Global.Logger.Db.Table<LogModel.Script>()
                                .Where(x => x.BuildId == buildId)
                                .ToDictionary(x => x.Id, x => x.Name);

                            foreach (LogModel.Script scLog in scripts)
                            {
                                pIdx += 1;

                                // Log codes
                                var cLogs = _db.Table<LogModel.BuildLog>().Where(x => x.BuildId == buildId && x.ScriptId == scLog.Id);
                                if (!opts.IncludeComments)
                                    cLogs = cLogs.Where(x => (x.Flags & LogModel.BuildLogFlag.Comment) != LogModel.BuildLogFlag.Comment);
                                if (!opts.IncludeMacros)
                                    cLogs = cLogs.Where(x => (x.Flags & LogModel.BuildLogFlag.Macro) != LogModel.BuildLogFlag.Macro);
                                LogModel.BuildLog[] codeLogs = cLogs.OrderBy(x => x.Id).OrderBy(x => x.Id).ToArray();

                                // Populate CodeLogs.Script
                                ScriptLogItem scModel = new ScriptLogItem
                                {
                                    IndexStr = pIdx.ToString(),
                                    Name = scLog.Name,
                                    Path = scLog.TreePath,
                                };
                                ScriptObject scObj = new ScriptObject();
                                scObj.Import(scModel, renamer: HtmlRenderer.ScribanObjectRenamer);

                                // Populate CodeLogs.Codes
                                ScriptArray codeArr = new ScriptArray(codeLogs.Length);
                                foreach (LogModel.BuildLog log in codeLogs)
                                {
                                    CodeLogItem item = new CodeLogItem
                                    {
                                        State = log.State,
                                        // HTML log export handles flags itself
                                        Message = log.Export(LogExportType.Html, true, false),
                                        Flags = log.Flags,
                                    };

                                    // Referenced script
                                    if (opts.ShowLogFlags &&
                                        (log.Flags & LogModel.BuildLogFlag.RefScript) == LogModel.BuildLogFlag.RefScript)
                                    {
                                        if (scTitleDict.ContainsKey(log.RefScriptId))
                                            item.RefScriptTitle = scTitleDict[log.RefScriptId];
                                    }

                                    if (log.State == LogState.Error)
                                        item.Href = errIdx++;
                                    else if (log.State == LogState.Warning)
                                        item.Href = warnIdx++;

                                    codeArr.AddItem(item);
                                }

                                // Populate CodeLogs.Variables (record of local variables))
                                ScriptArray varArr = new ScriptArray();
                                foreach (LogModel.Variable varLog in _db.Table<LogModel.Variable>()
                                    .Where(x => x.BuildId == buildId && x.ScriptId == scLog.Id && x.Type == VarsType.Local)
                                    .OrderBy(x => x.Key))
                                {
                                    varArr.AddItem(new VariableLogItem
                                    {
                                        Type = varLog.Type,
                                        Key = varLog.Key,
                                        Value = varLog.Value,
                                    });
                                }

                                /* type: [
                                    { 
                                        Script = ScriptLogItem,
                                        Codes = ScriptArray of CodeLogItem,
                                        Variables = ScriptArray of VariableLogItem
                                    }, ...
                                ] */
                                ScriptObject rootItem = new ScriptObject
                                {
                                    ["Script"] = scObj,
                                    ["Codes"] = codeArr,
                                    ["Variables"] = varArr
                                };
                                m.CodeLogs.Add(rootItem);
                            }
                        }

                        HtmlRenderer.RenderHtmlAsync("Html._BuildLogView.sbnhtml", assembly, m, _w).Wait();
                    }
                    break;
                    #endregion
            }
        }
        #endregion

        #region ExportScriptOriginText
        private static string ExportRefScriptText(LogModel.BuildLog bLog, LogModel.Script[] scLogs)
        {
            if (bLog.RefScriptId != 0)
            { // Referenced script
                LogModel.Script refScLog = scLogs.FirstOrDefault(x => x.Id == bLog.RefScriptId);
                if (refScLog == null)
                    return "|-> Referenced an unknown script";

                string path = refScLog.TreePath;
                if (path.Length == 0)
                    path = refScLog.RealPath;
                return $"|-> Referenced script [{refScLog.Name}] ({path})";
            }
            else
            { // Not a referenced sript
                return null;
            }
        }
        #endregion


    }

    #region class BuildLogOptions
    public class BuildLogOptions
    {
        public bool IncludeComments;
        public bool IncludeMacros;
        public bool ShowLogFlags;
    }
    #endregion
}
