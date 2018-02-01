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
   resulting work. An external library is a module which is
   not derived from or based on this program. 
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
        Overwrite = 201,
        Error = 300,
        CriticalError = 301,
        Info = 400,
        Ignore = 401,
        Muted = 402,
    }

    [Serializable]
    public struct LogInfo
    {
        #region Fields
        public LogState State;
        public string Message;
        public CodeCommand Command;
        public int Depth;
        #endregion

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
        #endregion

        #region AddCommand, AddDepth
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

        #region LogErrorMessage
        /// <summary>
        /// Wrapper for one-line error terminate
        /// </summary>
        /// <param name="logs"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        public static List<LogInfo> LogErrorMessage(List<LogInfo> logs, string msg)
        {
            logs.Add(new LogInfo(LogState.Error, msg));
            return logs;
        }
        #endregion

        #region ToString
        public override string ToString()
        {
            if (Command != null)
            {
                if (0 < Command.LineIdx)
                    return $"[{State}] {Message} ({Command.RawCode}) (Line {Command.LineIdx})";
                else
                    return $"[{State}] {Message} ({Command.RawCode})";
            }
            else
            {
                return $"[{State}] {Message}";
            }
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
    public class ScriptUpdateEventArgs : EventArgs
    {
        public DB_Script Log { get; set; }
        public ScriptUpdateEventArgs(DB_Script log) : base()
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
    public delegate void ScriptUpdateEventHandler(object sender, ScriptUpdateEventArgs e);
    public delegate void VariableUpdateEventHandler(object sender, VariableUpdateEventArgs e);
    #endregion

    #region LogEnum
    public enum LogExportType
    {
        Text, Html
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
    #endregion

    #region Logger Class
    public class Logger
    {
        #region Fields, Constructor, Destructor
        public LogDB DB;
        public bool SuspendLog = false;

        public static DebugLevel DebugLevel;
        public readonly ConcurrentStack<bool> TurnOff = new ConcurrentStack<bool>();

        private List<DB_BuildLog> BuildLogPool = new List<DB_BuildLog>(4096);

        private readonly ConcurrentDictionary<long, DB_BuildInfo> buildDict = new ConcurrentDictionary<long, DB_BuildInfo>();
        private readonly ConcurrentDictionary<long, Tuple<DB_Script, Stopwatch>> scriptDict = new ConcurrentDictionary<long, Tuple<DB_Script, Stopwatch>>();

        public event SystemLogUpdateEventHandler SystemLogUpdated;
        public event BuildLogUpdateEventHandler BuildLogUpdated;
        public event BuildInfoUpdateEventHandler BuildInfoUpdated;
        public event ScriptUpdateEventHandler ScriptUpdated;
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
        #endregion

        #region Build Init/Finish
        public long Build_Init(EngineState s, string name)
        {
            if (s.DisableLogger)
                return 0;

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
            List<DB_Variable> varLogs = new List<DB_Variable>();
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
                    varLogs.Add(dbVar);

                    // Fire Event
                    VariableUpdated?.Invoke(this, new VariableUpdateEventArgs(dbVar));
                }
            }
            DB.InsertAll(varLogs);

            System_Write(new LogInfo(LogState.Info, $"Build [{name}] started"));
            
            return dbBuild.Id;
        }

        public void Build_Finish(EngineState s)
        {
            if (s.DisableLogger)
                return;

            buildDict.TryRemove(s.BuildId, out DB_BuildInfo dbBuild);
            if (dbBuild == null)
                throw new KeyNotFoundException($"Unable to find DB_BuildInfo Instance, id = {s.BuildId}");

            if (s.DelayedLogging)
            {
                DB.InsertAll(BuildLogPool);
                BuildLogPool.Clear();
            }

            dbBuild.EndTime = DateTime.UtcNow;
            DB.Update(dbBuild);

            System_Write(new LogInfo(LogState.Info, $"Build [{dbBuild.Name}] finished"));
        }

        public long Build_Script_Init(EngineState s, Script p, int order)
        {
            if (s.DisableLogger)
                return 0;

            long buildId = s.BuildId;
            DB_Script dbScript = new DB_Script()
            {
                BuildId = buildId,
                Level = p.Level,
                Order = order,
                Name = p.Title,
                Path = p.ShortPath,
                Version = p.Version,
            };
            DB.Insert(dbScript);
            scriptDict[dbScript.Id] = new Tuple<DB_Script, Stopwatch>(dbScript, Stopwatch.StartNew());

            // Fire Event
            if (!s.DelayedLogging)
                ScriptUpdated?.Invoke(this, new ScriptUpdateEventArgs(dbScript));

            return dbScript.Id;
        }

        public void Build_Script_Finish(EngineState s, Dictionary<string, string> localVars)
        {
            if (s.DisableLogger)
                return;

            if (s.DelayedLogging)
            {
                DB.InsertAll(BuildLogPool);
                BuildLogPool.Clear();
            }

            if (s.DisableLogger == false)
            {
                long buildId = s.BuildId;
                long scriptId = s.ScriptId;

                // Scripts 
                scriptDict.TryRemove(scriptId, out Tuple<DB_Script, Stopwatch> tuple);
                if (tuple == null)
                    throw new KeyNotFoundException($"Unable to find DB_Script Instance, id = {scriptId}");

                DB_Script dbScript = tuple.Item1;
                Stopwatch watch = tuple.Item2;
                watch.Stop();

                dbScript.ElapsedMilliSec = watch.ElapsedMilliseconds;
                if (localVars != null)
                {
                    List<DB_Variable> varLogs = new List<DB_Variable>(localVars.Count);
                    foreach (var kv in localVars)
                    {
                        DB_Variable dbVar = new DB_Variable()
                        {
                            BuildId = buildId,
                            ScriptId = scriptId,
                            Type = VarsType.Local,
                            Key = kv.Key,
                            Value = kv.Value,
                        };
                        varLogs.Add(dbVar);

                        // Fire Event
                        VariableUpdated?.Invoke(this, new VariableUpdateEventArgs(dbVar));
                    }
                    DB.InsertAll(varLogs);
                }

                DB.Update(dbScript);

                // Fire Event
                if (s.DelayedLogging)
                    ScriptUpdated?.Invoke(this, new ScriptUpdateEventArgs(dbScript));
            }
        }
        #endregion

        #region Build_Write
        public void Build_Write(EngineState s, string message)
        {
            if (s.DisableLogger)
                return;

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
                    BuildId = s.BuildId,
                    ScriptId = s.ScriptId,
                    Message = message,
                };

                if (s.DelayedLogging)
                {
                    BuildLogPool.Add(dbCode);
                }
                else
                {
                    DB.Insert(dbCode);

                    // Fire Event
                    BuildLogUpdated?.Invoke(this, new BuildLogUpdateEventArgs(dbCode));
                }   
            }
        }

        public void Build_Write(EngineState s, LogInfo log)
        {
            if (s.DisableLogger)
                return;

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
                    BuildId = s.BuildId,
                    ScriptId = s.ScriptId,
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
                    dbCode.LineIdx = log.Command.LineIdx;
                }

                if (s.DelayedLogging)
                {
                    BuildLogPool.Add(dbCode);
                }
                else
                {
                    DB.Insert(dbCode);

                    // Fire Event
                    BuildLogUpdated?.Invoke(this, new BuildLogUpdateEventArgs(dbCode));
                }
            }
        }

        public void Build_Write(EngineState s, IEnumerable<LogInfo> logs)
        {
            if (s.DisableLogger)
                return;

            bool doNotLog = false;
            if (0 < TurnOff.Count)
            {
                if (TurnOff.TryPeek(out doNotLog) == false) // Stack Failure
                    doNotLog = false;
            }

            if (doNotLog == false)
            {
                foreach (LogInfo log in logs)
                    Build_Write(s, log);
            }
        }

        public void Build_Write(long buildId, IEnumerable<LogInfo> logs)
        {
            foreach (LogInfo log in logs)
            {
                DB_BuildLog dbCode = new DB_BuildLog()
                {
                    Time = DateTime.UtcNow,
                    BuildId = buildId,
                    ScriptId = 0,
                    Depth = log.Depth,
                    State = log.State,
                };
                BuildLogPool.Add(dbCode);

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
                    dbCode.LineIdx = log.Command.LineIdx;
                }

                DB.Insert(dbCode);

                // Fire Event
                BuildLogUpdated?.Invoke(this, new BuildLogUpdateEventArgs(dbCode));
            }
        }
        #endregion

        #region System_Write
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

        #region LogStartOfSection, LogEndOfSection
        public void LogStartOfSection(EngineState s, SectionAddress addr, int depth, bool logScriptName, Dictionary<int, string> sectionParam, CodeCommand cmd = null, bool forceLog = false)
        {
            if (s.DisableLogger)
                return;

            bool turnOff = false;
            if (0 < TurnOff.Count)
            {
                if (TurnOff.TryPeek(out turnOff) == false) // Stack Failure
                    turnOff = false;
            }

            bool TurnOffOriginalValue = turnOff;
            if (forceLog && TurnOffOriginalValue)
                turnOff = false;

            if (logScriptName)
                LogStartOfSection(s, addr.Section.SectionName, depth, sectionParam, cmd);
            else
                LogStartOfSection(s, addr.Script.ShortPath, addr.Section.SectionName, depth, sectionParam, cmd);

            if (forceLog && TurnOffOriginalValue)
                turnOff = true;
        }

        public void LogStartOfSection(EngineState s, string sectionName, int depth, Dictionary<int, string> paramDict = null, CodeCommand cmd = null)
        {
            if (s.DisableLogger)
                return;

            string msg = $"Processing Section [{sectionName}]";
            if (cmd == null)
                Build_Write(s, new LogInfo(LogState.Info, msg, depth));
            else
                Build_Write(s, new LogInfo(LogState.Info, msg, cmd, depth));

            LogSectionParameter(s, depth, paramDict, cmd);
        }

        public void LogStartOfSection(EngineState s, string scriptName, string sectionName, int depth, Dictionary<int, string> paramDict = null, CodeCommand cmd = null)
        {
            if (s.DisableLogger)
                return;

            string msg = $"Processing [{scriptName}]'s Section [{sectionName}]";
            if (cmd == null)
                Build_Write(s, new LogInfo(LogState.Info, msg, depth));
            else
                Build_Write(s, new LogInfo(LogState.Info, msg, cmd, depth));

            LogSectionParameter(s, depth, paramDict, cmd);
        }

        public void LogEndOfSection(EngineState s, SectionAddress addr, int depth, bool logScriptName, CodeCommand cmd = null, bool forceLog = false)
        {
            if (s.DisableLogger)
                return;

            bool turnOff = false;
            if (0 < TurnOff.Count)
            {
                if (TurnOff.TryPeek(out turnOff) == false) // Stack Failure
                    turnOff = false;
            }

            bool TurnOffOriginalValue = turnOff;
            if (forceLog && TurnOffOriginalValue)
                turnOff = false;

            if (logScriptName)
                LogEndOfSection(s, addr.Section.SectionName, depth, cmd);
            else
                LogEndOfSection(s, addr.Script.ShortPath, addr.Section.SectionName, depth, cmd);

            if (forceLog && TurnOffOriginalValue)
                turnOff = true;
        }

        public void LogEndOfSection(EngineState s, string sectionName, int depth, CodeCommand cmd = null)
        {
            if (s.DisableLogger)
                return;

            string msg = $"End of Section [{sectionName}]";
            if (cmd == null)
                Build_Write(s, new LogInfo(LogState.Info, msg, depth));
            else
                Build_Write(s, new LogInfo(LogState.Info, msg, cmd, depth));
        }

        public void LogEndOfSection(EngineState s, string scriptName, string sectionName, int depth, CodeCommand cmd = null)
        {
            if (s.DisableLogger)
                return;

            string msg = $"End of [{scriptName}]'s Section [{sectionName}]";
            if (cmd == null)
                Build_Write(s, new LogInfo(LogState.Info, msg, depth));
            else
                Build_Write(s, new LogInfo(LogState.Info, msg, cmd, depth));
        }
        #endregion

        #region LogSectionParameter
        public void LogSectionParameter(EngineState s, int depth, Dictionary<int, string> paramDict = null, CodeCommand cmd = null)
        {
            if (s.DisableLogger)
                return;

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
                    Build_Write(s, new LogInfo(LogState.Info, b.ToString(), depth + 1));
                else
                    Build_Write(s, new LogInfo(LogState.Info, b.ToString(), cmd, depth + 1));
            }
        }
        #endregion

        #region ExportSystemLog, ExportBuildLog, ParseLogExportType
        public static LogExportType ParseLogExportType(string str)
        {
            LogExportType logFormat = LogExportType.Html;
            if (str.Equals("HTML", StringComparison.OrdinalIgnoreCase))
                logFormat = LogExportType.Html; 
            else if (str.Equals("Text", StringComparison.OrdinalIgnoreCase))
                logFormat = LogExportType.Text;

            return logFormat;
        }

        public void ExportSystemLog(LogExportType type, string exportFile)
        {
            using (StreamWriter w = new StreamWriter(exportFile, false, Encoding.UTF8))
            {
                LogExporter exporter = new LogExporter(DB, type, w);
                exporter.ExportSystemLog();
            }
        }

        public void ExportBuildLog(LogExportType type, string exportFile, long buildId)
        {
            using (StreamWriter w = new StreamWriter(exportFile, false, Encoding.UTF8))
            {
                LogExporter exporter = new LogExporter(DB, type, w);
                exporter.ExportBuildLog(buildId);
            }
        }
        #endregion

        #region LogExceptionMessage
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

        public override string ToString()
        {
            return $"{Id} = {Name}";
        }
    }

    public class DB_Script
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
        public long ScriptId { get; set; }
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
        public long ScriptId { get; set; }
        public int Depth { get; set; }
        public LogState State { get; set; }
        [MaxLength(65535)]
        public string Message { get; set; }
        public int LineIdx { get; set; }
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
        public string TimeStr => Time.ToLocalTime().ToString("yyyy-MM-dd hh:mm:ss tt", CultureInfo.InvariantCulture);
        [Ignore]
        public string Text => Export(LogExportType.Text);
        [Ignore]
        public string LineIdxStr => LineIdx.ToString();

        public string Export(LogExportType type, bool logDepth = true)
        {
            string str = string.Empty;
            switch (type)
            {
                #region Text
                case LogExportType.Text:
                    {
                        StringBuilder b = new StringBuilder();

                        if (logDepth)
                        {
                            for (int i = 0; i < Depth; i++)
                                b.Append("  ");
                        }

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

                        if (State == LogState.Error || State == LogState.Warning)
                            b.Append($" (Line {LineIdx})");

                        str = b.ToString();
                    }
                    break;
                #endregion
                #region HTML
                case LogExportType.Html:
                    {
                        StringBuilder b = new StringBuilder();

                        if (logDepth)
                        {
                            for (int i = 0; i < Depth; i++)
                                b.Append("  ");
                        }

                        if (RawCode == null)
                            b.Append(Message);
                        else
                            b.Append($"{Message} ({RawCode})");

                        if (State == LogState.Error || State == LogState.Warning)
                            b.Append($" (Line {LineIdx})");

                        str = b.ToString();
                    }
                    break;
                #endregion
            }

            return str;
        }

        public override string ToString()
        {
            return $"{BuildId}, {Id} = [{State}, {Depth}] {Message} ({RawCode})";
        }
    }
    #endregion

    #region LogDB
    public class LogDB : SQLiteConnectionWithLock
    {
        public LogDB(string path) : base(new SQLiteConnectionString(path, true), SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create)
        {
            CreateTable<DB_SystemLog>();
            CreateTable<DB_BuildInfo>();
            CreateTable<DB_Script>();
            CreateTable<DB_Variable>();
            CreateTable<DB_BuildLog>();
        }
    }
    #endregion
}
