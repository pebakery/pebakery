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
*/

using PEBakery.Helper;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PEBakery.Core.Commands;
using System.Collections.Concurrent;
using SQLite;

namespace PEBakery.Core
{
    #region LogState, LogInfo
    public enum LogState
    {
        None = 0,
        Success = 100,
        Warning = 200,
        Error = 300,
        CriticalError = 301,
        Info = 400,
        Ignore = 401,
        Muted = 402,
    }

    [Serializable]
    public struct LogInfo
    {
        public LogState State;
        public string Message;
        public CodeCommand Command;
        public int Depth;

        #region Constructor - LogState, Message
        public LogInfo(LogState state, string message)
        {
            State = state;
            Message = message;
            Command = null;
            Depth = 0;
        }

        public LogInfo(LogState state, string message, CodeCommand command)
        {
            State = state;
            Message = message;
            Command = command;
            Depth = 0;
        }

        public LogInfo(LogState state, string message, int depth)
        {
            State = state;
            Message = message;
            Command = null;
            Depth = depth;
        }

        public LogInfo(LogState state, string message, CodeCommand command, int depth)
        {
            State = state;
            Message = message;
            Command = command;
            Depth = depth;
        }
        #endregion

        #region Constructor - LogState, Exception
        public LogInfo(LogState state, Exception e)
        {
            State = state;
            Message = Logger.LogExceptionMessage(e);
            Command = null;
            Depth = 0;
        }

        public LogInfo(LogState state, Exception e, CodeCommand command)
        {
            State = state;
            Message = Logger.LogExceptionMessage(e);
            Command = command;
            Depth = 0;
        }

        public LogInfo(LogState state, Exception e, int depth)
        {
            State = state;
            Message = Logger.LogExceptionMessage(e);
            Command = null;
            Depth = depth;
        }

        public LogInfo(LogState state, Exception e, CodeCommand command, int depth)
        {
            State = state;
            Message = Logger.LogExceptionMessage(e);
            Command = command;
            Depth = depth;
        }

        public static LogInfo AddCommand(LogInfo log, CodeCommand command)
        {
            if (log.Command == null)
                log.Command = command;
            return log;
        }

        public static List<LogInfo> AddCommand(List<LogInfo> logs, CodeCommand command)
        {
            for (int i = 0; i < logs.Count; i++)
            {
                LogInfo log = logs[i];
                if (log.Command == null)
                    log.Command = command;
                logs[i] = log;
            }

            return logs;
        }

        public static LogInfo AddDepth(LogInfo log, int depth)
        {
            log.Depth = depth;
            return log;
        }

        public static List<LogInfo> AddDepth(List<LogInfo> logs, int depth)
        {
            for (int i = 0; i < logs.Count; i++)
            {
                LogInfo log = logs[i];
                log.Depth = depth;
                logs[i] = log;
            }

            return logs;
        }

        public static LogInfo AddCommandDepth(LogInfo log, CodeCommand command, int depth)
        {
            if (log.Command == null)
                log.Command = command;
            log.Depth = depth;
            return log;
        }

        public static List<LogInfo> AddCommandDepth(List<LogInfo> logs, CodeCommand command, int depth)
        {
            for (int i = 0; i < logs.Count; i++)
            {
                LogInfo log = logs[i];
                if (log.Command == null)
                    log.Command = command;
                log.Depth = depth;
                logs[i] = log;
            }

            return logs;
        }
        #endregion
    }
    #endregion

    #region EventHandlers
    public class SystemLogUpdateEventArgs : EventArgs
    {
        public DB_SystemLog Log { get; set; }
        public SystemLogUpdateEventArgs(DB_SystemLog log) : base()
        {
            Log = log;
        }
    }
    public class BuildInfoUpdateEventArgs : EventArgs
    {
        public DB_BuildInfo Log { get; set; }
        public BuildInfoUpdateEventArgs(DB_BuildInfo log) : base()
        {
            Log = log;
        }
    }
    public class BuildLogUpdateEventArgs : EventArgs
    {
        public DB_BuildLog Log { get; set; }
        public BuildLogUpdateEventArgs(DB_BuildLog log) : base()
        {
            Log = log;
        }
    }
    public class PluginUpdateEventArgs : EventArgs
    {
        public DB_Plugin Log { get; set; }
        public PluginUpdateEventArgs(DB_Plugin log) : base()
        {
            Log = log;
        }
    }
    public class VariableUpdateEventArgs : EventArgs
    {
        public DB_Variable Log { get; set; }
        public VariableUpdateEventArgs(DB_Variable log) : base()
        {
            Log = log;
        }
    }

    public delegate void SystemLogUpdateEventHandler(object sender, SystemLogUpdateEventArgs e);
    public delegate void BuildLogUpdateEventHandler(object sender, BuildLogUpdateEventArgs e);
    public delegate void BuildInfoUpdateEventHandler(object sender, BuildInfoUpdateEventArgs e);
    public delegate void PluginUpdateEventHandler(object sender, PluginUpdateEventArgs e);
    public delegate void VariableUpdateEventHandler(object sender, VariableUpdateEventArgs e);
    #endregion

    #region Logger Class
    public enum LogExportType
    {
        Text, Html, Xlsx
    }

    /// <summary>
    /// How much information will be logged if an Exception is catched in ExecuteCommand?
    /// </summary>
    public enum DebugLevel
    {
        Production = 0, // Only Exception message
        PrintException = 1, // Print Exception message with Exception type
        PrintExceptionStackTrace = 2, // Print Exception message, type, and stack trace
    }

    public class Logger
    {    
        public LogDB DB;
        public int ErrorOffCount = 0;
        public bool SuspendLog = false;

        public static DebugLevel DebugLevel;
        public readonly ConcurrentStack<bool> TurnOff = new ConcurrentStack<bool>();

        private readonly ConcurrentDictionary<long, DB_BuildInfo> buildDict = new ConcurrentDictionary<long, DB_BuildInfo>();
        private readonly ConcurrentDictionary<long, Tuple<DB_Plugin, Stopwatch>> pluginDict = new ConcurrentDictionary<long, Tuple<DB_Plugin, Stopwatch>>();

        public event SystemLogUpdateEventHandler SystemLogUpdated;
        public event BuildLogUpdateEventHandler BuildLogUpdated;
        public event BuildInfoUpdateEventHandler BuildInfoUpdated;
        public event PluginUpdateEventHandler PluginUpdated;
        public event VariableUpdateEventHandler VariableUpdated;

        public static readonly string LogSeperator = "--------------------------------------------------------------------------------";

        public Logger(string path)
        {
            DB = new LogDB(path);
        }

        ~Logger()
        {
            DB.Close();
        }

        #region DB Write
        public long Build_Init(string name, EngineState s)
        {
            // Build Id
            DB_BuildInfo dbBuild = new DB_BuildInfo()
            {
                StartTime = DateTime.UtcNow,
                Name = name,
            };
            DB.Insert(dbBuild);
            buildDict[dbBuild.Id] = dbBuild;
            s.BuildId = dbBuild.Id;

            // Fire Event
            BuildInfoUpdated?.Invoke(this, new BuildInfoUpdateEventArgs(dbBuild));

            // Variables - Fixed, Global, Local
            foreach (VarsType type in Enum.GetValues(typeof(VarsType)))
            {
                Dictionary<string, string> dict = s.Variables.GetVarDict(type);
                foreach (var kv in dict)
                {
                    DB_Variable dbVar = new DB_Variable()
                    {
                        BuildId = dbBuild.Id,
                        Type = type,
                        Key = kv.Key,
                        Value = kv.Value,
                    };
                    DB.Insert(dbVar);

                    // Fire Event
                    VariableUpdated?.Invoke(this, new VariableUpdateEventArgs(dbVar));
                }
            }

            System_Write(new LogInfo(LogState.Info, $"Build [{name}] started"));
            
            return dbBuild.Id;
        }

        public void Build_Finish(long id)
        {
            buildDict.TryRemove(id, out DB_BuildInfo dbBuild);
            if (dbBuild == null)
                throw new KeyNotFoundException($"Unable to find DB_BuildInfo Instance, id = {id}");

            dbBuild.EndTime = DateTime.UtcNow;
            DB.Update(dbBuild);

            System_Write(new LogInfo(LogState.Info, $"Build [{dbBuild.Name}] finished"));
        }

        public long Build_Plugin_Init(long buildId, Plugin p, int order)
        {
            DB_Plugin dbPlugin = new DB_Plugin()
            {
                BuildId = buildId,
                Level = p.Level,
                Order = order,
                Name = p.Title,
                Path = p.ShortPath,
                Version = p.Version,
            };
            DB.Insert(dbPlugin);
            pluginDict[dbPlugin.Id] = new Tuple<DB_Plugin, Stopwatch>(dbPlugin, Stopwatch.StartNew());

            // Fire Event
            PluginUpdated?.Invoke(this, new PluginUpdateEventArgs(dbPlugin));

            return dbPlugin.Id;
        }

        public void Build_Plugin_Finish(long buildId, long pluginId, Dictionary<string, string> localVars)
        {
            // Plugins 
            pluginDict.TryRemove(pluginId, out Tuple<DB_Plugin, Stopwatch> tuple);
            if (tuple == null)
                throw new KeyNotFoundException($"Unable to find DB_Plugin Instance, id = {pluginId}");

            DB_Plugin dbPlugin = tuple.Item1;
            Stopwatch watch = tuple.Item2;
            watch.Stop();

            dbPlugin.ElapsedMilliSec = watch.ElapsedMilliseconds;
            if (localVars != null)
            {
                foreach (var kv in localVars)
                {
                    DB_Variable dbVar = new DB_Variable()
                    {
                        BuildId = buildId,
                        PluginId = pluginId,
                        Type = VarsType.Local,
                        Key = kv.Key,
                        Value = kv.Value,
                    };
                    DB.Insert(dbVar);

                    // Fire Event
                    VariableUpdated?.Invoke(this, new VariableUpdateEventArgs(dbVar));
                }
            }

            DB.Update(dbPlugin);
        }

        /// <summary>
        /// Write LogInfo into DB_CodeLog
        /// </summary>
        /// <param name="buildId"></param>
        /// <param name="time"></param>
        /// <param name="message"></param>

        public void Build_Write(long buildId, string message)
        {
            Build_Write(buildId, 0, message);
        }

        /// <summary>
        /// Write LogInfo into DB_CodeLog
        /// </summary>
        /// <param name="buildId"></param>
        /// <param name="time"></param>
        /// <param name="message"></param>
        public void Build_Write(long buildId, long pluginId, string message)
        {
            bool doNotLog = false;
            if (0 < TurnOff.Count)
            {
                if (TurnOff.TryPeek(out doNotLog) == false) // Stack Failure
                    doNotLog = false;
            }

            if (doNotLog == false)
            {
                DB_BuildLog dbCode = new DB_BuildLog()
                {
                    Time = DateTime.UtcNow,
                    BuildId = buildId,
                    PluginId = pluginId,
                    Message = message,
                };
                DB.Insert(dbCode);

                // Fire Event
                BuildLogUpdated?.Invoke(this, new BuildLogUpdateEventArgs(dbCode));
            }
        }

        public void Build_Write(EngineState s, string message)
        {
            Build_Write(s.BuildId, s.PluginId, message);
        }

        public void Build_Write(long buildId, LogInfo log)
        {
            Build_Write(buildId, 0, log);
        }

        public void Build_Write(long buildId, long pluginId, LogInfo log)
        {
            bool doNotLog = false;
            if (0 < TurnOff.Count)
            {
                if (TurnOff.TryPeek(out doNotLog) == false) // Stack Failure
                    doNotLog = false;
            }

            if (doNotLog == false)
            {
                DB_BuildLog dbCode = new DB_BuildLog()
                {
                    Time = DateTime.UtcNow,
                    BuildId = buildId,
                    PluginId = pluginId,
                    Depth = log.Depth,
                    State = log.State,
                };

                if (log.Command == null)
                {
                    dbCode.Message = log.Message;
                }
                else
                {
                    if (log.Message == string.Empty)
                        dbCode.Message = log.Command.Type.ToString();
                    else
                        dbCode.Message = $"{log.Command.Type} - {log.Message}";
                    dbCode.RawCode = log.Command.RawCode;
                }

                DB.Insert(dbCode);

                // Fire Event
                BuildLogUpdated?.Invoke(this, new BuildLogUpdateEventArgs(dbCode));
            }
        }

        public void Build_Write(EngineState s, LogInfo log)
        {
            Build_Write(s.BuildId, s.PluginId, log);
        }

        public void Build_Write(long buildId, IEnumerable<LogInfo> logs)
        {
            Build_Write(buildId, 0, logs);
        }

        public void Build_Write(long buildId, long pluginId, IEnumerable<LogInfo> logs)
        {
            bool doNotLog = false;
            if (0 < TurnOff.Count)
            {
                if (TurnOff.TryPeek(out doNotLog) == false) // Stack Failure
                    doNotLog = false;
            }

            if (doNotLog == false)
            {
                foreach (LogInfo log in logs)
                    Build_Write(buildId, pluginId, log);
            }
        }

        public void Build_Write(EngineState s, IEnumerable<LogInfo> logs)
        {
            Build_Write(s.BuildId, s.PluginId, logs);
        }

        public void System_Write(string message)
        {
            DB_SystemLog dbLog = new DB_SystemLog()
            {
                Time = DateTime.UtcNow,
                State = LogState.None,
                Message = message,
            };

            DB.Insert(dbLog);

            // Fire Event
            SystemLogUpdated?.Invoke(this, new SystemLogUpdateEventArgs(dbLog));
        }

        public void System_Write(LogInfo log)
        {
            DB_SystemLog dbLog = new DB_SystemLog()
            {
                Time = DateTime.UtcNow,
                State = log.State,
                Message = log.Message,
            };

            DB.Insert(dbLog);

            // Fire Event
            SystemLogUpdated?.Invoke(this, new SystemLogUpdateEventArgs(dbLog));
        }

        public void System_Write(IEnumerable<LogInfo> logs)
        {
            foreach (LogInfo log in logs)
                System_Write(log);
        }
        #endregion

        #region Debug_Write
        public void Debug_Write(string message)
        {
            Console.WriteLine(message);
        }

        public void Debug_Write(LogInfo log)
        {
            for (int i = 0; i < log.Depth; i++)
                Console.Write("  ");

            if (log.Command == null)
                Console.WriteLine($"[{log.State}] {log.Message}");
            else
                Console.WriteLine($"[{log.State}] {log.Command.Type} - {log.Message} ({log.Command})");
        }

        public void Debug_Write(IEnumerable<LogInfo> logs)
        {
            foreach (LogInfo log in logs)
                Debug_Write(log);
        }
        #endregion

        #region LogStartOfSection, LogEndOfSection
        public void LogStartOfSection(EngineState s, SectionAddress addr, int depth, bool logPluginName, Dictionary<int, string> sectionParam, CodeCommand cmd = null, bool forceLog = false)
        {
            LogStartOfSection(s.BuildId, s.PluginId, addr, depth, logPluginName, sectionParam, cmd, forceLog);
        }

        public void LogStartOfSection(long buildId, long pluginId, SectionAddress addr, int depth, bool logPluginName, Dictionary<int, string> paramDict = null, CodeCommand cmd = null, bool forceLog = false)
        { 
            bool turnOff = false;
            if (0 < TurnOff.Count)
            {
                if (TurnOff.TryPeek(out turnOff) == false) // Stack Failure
                    turnOff = false;
            }

            bool TurnOffOriginalValue = turnOff;
            if (forceLog && TurnOffOriginalValue)
                turnOff = false;

            if (logPluginName)
                LogStartOfSection(buildId, pluginId, addr.Section.SectionName, depth, paramDict, cmd);
            else
                LogStartOfSection(buildId, pluginId, addr.Plugin.ShortPath, addr.Section.SectionName, depth, paramDict, cmd);

            if (forceLog && TurnOffOriginalValue)
                turnOff = true;
        }

        public void LogStartOfSection(long buildId, long pluginId, string sectionName, int depth, Dictionary<int, string> paramDict = null, CodeCommand cmd = null)
        {
            string msg = $"Processing Section [{sectionName}]";
            if (cmd == null)
                Build_Write(buildId, pluginId, new LogInfo(LogState.Info, msg, depth));
            else
                Build_Write(buildId, pluginId, new LogInfo(LogState.Info, msg, cmd, depth));

            LogSectionParameter(buildId, pluginId, depth, paramDict, cmd);
        }

        public void LogStartOfSection(long buildId, long pluginId, string pluginName, string sectionName, int depth, Dictionary<int, string> paramDict = null, CodeCommand cmd = null)
        {
            string msg = $"Processing [{pluginName}]'s Section [{sectionName}]";
            if (cmd == null)
                Build_Write(buildId, pluginId, new LogInfo(LogState.Info, msg, depth));
            else
                Build_Write(buildId, pluginId, new LogInfo(LogState.Info, msg, cmd, depth));

            
        }

        public void LogEndOfSection(EngineState s, SectionAddress addr, int depth, bool logPluginName, CodeCommand cmd = null, bool forceLog = false)
        {
            LogEndOfSection(s.BuildId, s.PluginId, addr, depth, logPluginName, cmd, forceLog);
        }

        public void LogEndOfSection(long buildId, long pluginId, SectionAddress addr, int depth, bool logPluginName, CodeCommand cmd = null, bool forceLog = false)
        {
            bool turnOff = false;
            if (0 < TurnOff.Count)
            {
                if (TurnOff.TryPeek(out turnOff) == false) // Stack Failure
                    turnOff = false;
            }

            bool TurnOffOriginalValue = turnOff;
            if (forceLog && TurnOffOriginalValue)
                turnOff = false;

            if (logPluginName)
                LogEndOfSection(buildId, pluginId, addr.Section.SectionName, depth, cmd);
            else
                LogEndOfSection(buildId, pluginId, addr.Plugin.ShortPath, addr.Section.SectionName, depth, cmd);

            if (forceLog && TurnOffOriginalValue)
                turnOff = true;
        }

        public void LogEndOfSection(long buildId, long pluginId, string sectionName, int depth, CodeCommand cmd = null)
        {
            string msg = $"End of Section [{sectionName}]";
            if (cmd == null)
                Build_Write(buildId, pluginId, new LogInfo(LogState.Info, msg, depth));
            else
                Build_Write(buildId, pluginId, new LogInfo(LogState.Info, msg, cmd, depth));
        }

        public void LogEndOfSection(long buildId, long pluginId, string pluginName, string sectionName, int depth, CodeCommand cmd = null)
        {
            string msg = $"End of [{pluginName}]'s Section [{sectionName}]";
            if (cmd == null)
                Build_Write(buildId, pluginId, new LogInfo(LogState.Info, msg, depth));
            else
                Build_Write(buildId, pluginId, new LogInfo(LogState.Info, msg, cmd, depth));
        }
        #endregion

        #region LogSectionParameter
        public void LogSectionParameter(EngineState s, int depth, Dictionary<int, string> paramDict = null, CodeCommand cmd = null)
        {
            LogSectionParameter(s.BuildId, s.PluginId, depth, paramDict, cmd);
        }

        public void LogSectionParameter(long buildId, long pluginId, int depth, Dictionary<int, string> paramDict = null, CodeCommand cmd = null)
        {
            // Write Section Parameters
            if (paramDict != null && 0 < paramDict.Count)
            {
                int cnt = 0;
                StringBuilder b = new StringBuilder();
                b.Append("Params = { ");
                foreach (var kv in paramDict)
                {
                    b.Append($"#{kv.Key}:[{kv.Value}]");
                    if (cnt + 1 < paramDict.Count)
                        b.Append(", ");
                    cnt++;
                }
                b.Append(" }");

                if (cmd == null)
                    Build_Write(buildId, pluginId, new LogInfo(LogState.Info, b.ToString(), depth + 1));
                else
                    Build_Write(buildId, pluginId, new LogInfo(LogState.Info, b.ToString(), cmd, depth + 1));
            }
        }
        #endregion

        #region ExportSystemLog, ExportBuildLog
        public void ExportSystemLog(LogExportType type, string exportFile)
        {
            switch (type)
            {
                case LogExportType.Text:
                    using (StreamWriter writer = new StreamWriter(exportFile, false, Encoding.UTF8))
                    {
                        writer.WriteLine($"- PEBakery System Log -");
                        var logs = DB.Table<DB_SystemLog>().OrderBy(x => x.Time);
                        foreach (DB_SystemLog log in logs)
                        {
                            if (log.State == LogState.None)
                                writer.WriteLine($"[{log.TimeStr}] {log.Message}");
                            else
                                writer.WriteLine($"[{log.TimeStr}] [{log.State}] {log.Message}");
                        }
                        writer.Close();
                    }
                    break;
                case LogExportType.Html:
                    break;
            }
        }

        public void ExportBuildLog(LogExportType type, string exportFile, long buildId, bool exportLocalVars = false)
        {
            switch (type)
            {
                case LogExportType.Text:
                    {
                        using (StreamWriter writer = new StreamWriter(exportFile, false, Encoding.UTF8))
                        {
                            DB_BuildInfo dbBuild = DB.Table<DB_BuildInfo>().Where(x => x.Id == buildId).First();
                            writer.WriteLine($"- PEBakery Build <{dbBuild.Name}> -");
                            writer.WriteLine($"Started at  {dbBuild.StartTime.ToString("yyyy-MM-dd h:mm:ss tt", CultureInfo.InvariantCulture)}");
                            writer.WriteLine($"Finished at {dbBuild.EndTime.ToString("yyyy-MM-dd h:mm:ss tt", CultureInfo.InvariantCulture)}");
                            TimeSpan t = dbBuild.EndTime - dbBuild.StartTime;
                            writer.WriteLine($"Took {t:h\\:mm\\:ss}");
                            writer.WriteLine();
                            writer.WriteLine();

                            var plugins = DB.Table<DB_Plugin>()
                                .Where(x => x.BuildId == buildId)
                                .OrderBy(x => x.Order);

                            writer.WriteLine("<Plugins>");
                            {
                                int count = plugins.Count();
                                int idx = 1;
                                foreach (DB_Plugin p in plugins)
                                {
                                    writer.WriteLine($"[{idx}/{count}] {p.Name} v{p.Version} ({p.ElapsedMilliSec / 1000.0:0.000}sec)");
                                    idx++;
                                }

                                writer.WriteLine($"Total {count} Plugins");
                                writer.WriteLine();
                                writer.WriteLine();
                            }

                            writer.WriteLine("<Variables>");
                            VarsType[] typeList = new VarsType[] { VarsType.Fixed, VarsType.Global };
                            foreach (VarsType varsType in typeList)
                            {
                                writer.WriteLine($"- {varsType} Variables");
                                var vars = DB.Table<DB_Variable>()
                                    .Where(x => x.BuildId == buildId && x.Type == varsType)
                                    .OrderBy(x => x.Key);
                                foreach (DB_Variable log in vars)
                                    writer.WriteLine($"%{log.Key}% = {log.Value}");
                                writer.WriteLine();
                            }
                            writer.WriteLine();

                            writer.WriteLine("<Code Logs>");
                            {
                                foreach (DB_Plugin pLog in plugins)
                                {
                                    // Log codes
                                    var codeLogs = DB.Table<DB_BuildLog>()
                                        .Where(x => x.BuildId == buildId && x.PluginId == pLog.Id)
                                        .OrderBy(x => x.Id);
                                    foreach (DB_BuildLog log in codeLogs)
                                        writer.WriteLine(log.Export(type));

                                    // Log local variables
                                    if (exportLocalVars)
                                    {
                                        var varLogs = DB.Table<DB_Variable>()
                                        .Where(x => x.BuildId == buildId && x.PluginId == pLog.Id && x.Type == VarsType.Local)
                                        .OrderBy(x => x.Key);
                                        if (0 < varLogs.Count())
                                        {
                                            writer.WriteLine("[Local Variables]");
                                            foreach (DB_Variable vLog in varLogs)
                                                writer.WriteLine($"%{vLog.Key}% = {vLog.Value}");
                                            writer.WriteLine(Logger.LogSeperator);
                                        }
                                    }

                                    writer.WriteLine();
                                }
                            }

                            /*
                            writer.WriteLine("<Code Logs>");
                            {
                                var codeLogs = DB.Table<DB_BuildLog>()
                                    .Where(x => x.BuildId == buildId)
                                    .OrderBy(x => x.Id);
                                foreach (DB_BuildLog log in codeLogs)
                                    writer.WriteLine(log.Export(type));
                                writer.WriteLine();
                            }
                            */
                            writer.Close();
                        }
                    }
                    break;
                case LogExportType.Html:
                    {
                        // TODO
                        Console.WriteLine("TODO: Not Implemented");
                    }
                    break;
            }
        }
        #endregion

        #region static LogExceptionMessage
        public static string LogExceptionMessage(Exception e)
        {
            switch (Logger.DebugLevel)
            {
                case DebugLevel.Production:
                    if (e.GetType() == typeof(AggregateException))
                    {
                        StringBuilder builder = new StringBuilder();
                        builder.Append(StringHelper.RemoveLastNewLine(e.Message));
                        foreach (var inEx in (e as AggregateException).InnerExceptions)
                        {
                            builder.Append("\r\n    ");
                            builder.Append(StringHelper.RemoveLastNewLine(inEx.Message));
                        }
                        builder.Append("\r\n ");
                        return builder.ToString();
                    }
                    else
                        return StringHelper.RemoveLastNewLine(e.Message);
                case DebugLevel.PrintException:
                    if (e.GetType() == typeof(AggregateException))
                    {
                        StringBuilder builder = new StringBuilder();
                        builder.Append(e.GetType());
                        builder.Append(": ");
                        builder.Append(StringHelper.RemoveLastNewLine(e.Message));
                        foreach (var inEx in (e as AggregateException).InnerExceptions)
                        {
                            builder.Append("\r\n    ");
                            builder.Append(inEx.GetType());
                            builder.Append(": ");
                            builder.Append(StringHelper.RemoveLastNewLine(inEx.Message));
                        }
                        builder.Append("\r\n ");
                        return builder.ToString();
                    }
                    else
                        return e.GetType() + ": " + StringHelper.RemoveLastNewLine(e.Message);
                case DebugLevel.PrintExceptionStackTrace:
                    if (e.GetType() == typeof(AggregateException))
                    {
                        StringBuilder builder = new StringBuilder();
                        builder.Append(e.GetType());
                        builder.Append(": ");
                        builder.Append(StringHelper.RemoveLastNewLine(e.Message));
                        foreach (var inEx in (e as AggregateException).InnerExceptions)
                        {
                            builder.Append("\r\n    ");
                            builder.Append(inEx.GetType());
                            builder.Append(": ");
                            builder.Append(StringHelper.RemoveLastNewLine(inEx.Message));
                        }
                        builder.Append("\r\n");
                        builder.Append(e.StackTrace);
                        builder.Append("\r\n ");
                        return builder.ToString();
                    }
                    else
                        return e.GetType() + ": " + StringHelper.RemoveLastNewLine(e.Message) + "\r\n" + e.StackTrace + "\r\n ";
                default:
                    Debug.Assert(false);
                    return "Internal Logic Error";
            }
        }
        #endregion
    }
    #endregion

    #region SQLite Model
    public class DB_SystemLog
    {
        [PrimaryKey, AutoIncrement]
        public long Id { get; set; }
        public DateTime Time { get; set; }
        public LogState State { get; set; }
        [MaxLength(65535)]
        public string Message { get; set; }

        // Used in LogWindow
        [Ignore]
        public string StateStr
        {
            get
            {
                if (State == LogState.None)
                    return string.Empty;
                else
                    return State.ToString();
            }
        }
        [Ignore]
        public string TimeStr { get => Time.ToLocalTime().ToString("yyyy-MM-dd hh:mm:ss tt", CultureInfo.InvariantCulture); }

        public override string ToString()
        {
            return $"{Id} = [{State}] {Message}";
        }
    }

    public class DB_BuildInfo
    {
        [PrimaryKey, AutoIncrement]
        public long Id { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        [MaxLength(256)]
        public string Name { get; set; }

        /*
        [OneToMany(CascadeOperations = CascadeOperation.All)] 
        public List<DB_Plugin> Plugins { get; set; }
        [OneToMany(CascadeOperations = CascadeOperation.All)] 
        public List<DB_Variable> Variables { get; set; }
        [OneToMany(CascadeOperations = CascadeOperation.All)]
        public List<DB_BuildLog> Logs { get; set; }
        */

        public override string ToString()
        {
            return $"{Id} = {Name}";
        }
    }

    public class DB_Plugin
    {
        [PrimaryKey, AutoIncrement]
        public long Id { get; set; }
        [Indexed]
        public long BuildId { get; set; }
        public int Order { get; set; } // Starts from 1
        public int Level { get; set; }
        [MaxLength(256)]
        public string Name { get; set; }
        [MaxLength(32767)] // https://msdn.microsoft.com/library/windows/desktop/aa365247.aspx#maxpath
        public string Path { get; set; }
        public int Version { get; set; }
        public long ElapsedMilliSec { get; set; }

        public override string ToString()
        {
            return $"{BuildId},{Id} = {Level} {Name} {Version}";
        }
    }

    public class DB_Variable
    {
        [PrimaryKey, AutoIncrement]
        public long Id { get; set; }
        [Indexed]
        public long BuildId { get; set; }
        [Indexed]
        public long PluginId { get; set; }
        public VarsType Type { get; set; }
        [MaxLength(256)]
        public string Key { get; set; }
        [MaxLength(65535)]
        public string Value { get; set; }

        public override string ToString()
        {
            return $"{BuildId},{Id} = ({Type}) {Key}={Value}";
        }
    }

    public class DB_BuildLog
    {
        [PrimaryKey, AutoIncrement]
        public long Id { get; set; }
        public DateTime Time { get; set; }
        [Indexed]
        public long BuildId { get; set; }
        [Indexed]
        public long PluginId { get; set; }
        public int Depth { get; set; }
        public LogState State { get; set; }
        [MaxLength(65535)]
        public string Message { get; set; }
        [MaxLength(65535)]
        public string RawCode { get; set; }

        // Used in LogWindow
        [Ignore]
        public string StateStr
        {
            get
            {
                if (State == LogState.None)
                    return string.Empty;
                else
                    return State.ToString();
            }
        }
        [Ignore]
        public string TimeStr { get => Time.ToLocalTime().ToString("yyyy-MM-dd hh:mm:ss tt", CultureInfo.InvariantCulture); }

        [Ignore]
        public string Text { get => Export(LogExportType.Text); }

        public string Export(LogExportType type)
        {
            StringBuilder b = new StringBuilder();

            switch (type)
            {
                case LogExportType.Text:
                    {
                        for (int i = 0; i < Depth; i++)
                            b.Append("  ");

                        if (State == LogState.None)
                        { // No State
                            if (RawCode == null)
                                b.Append(Message);
                            else
                                b.Append($"{Message} ({RawCode})");
                        }
                        else
                        { // Has State
                            if (RawCode == null)
                                b.Append($"[{State}] {Message}");
                            else
                                b.Append($"[{State}] {Message} ({RawCode})");
                        }
                    }
                    break;
                case LogExportType.Html:
                    {

                    }
                    break;
            }

            return b.ToString();
        }

        public override string ToString()
        {
            return $"{BuildId}, {Id} = [{State}, {Depth}] {Message} ({RawCode})";
        }
    }
    #endregion

    #region LogDB
    public class LogDB : SQLiteConnection
    {
        // public LogDB(string path) : base(new SQLitePlatformWin32(), path)
        public LogDB(string path) : base(path)
        {
            CreateTable<DB_SystemLog>();
            CreateTable<DB_BuildInfo>();
            CreateTable<DB_Plugin>();
            CreateTable<DB_Variable>();
            CreateTable<DB_BuildLog>();
        }
    }
    #endregion
}
