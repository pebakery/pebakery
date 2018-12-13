/*
   Copyright (C) 2016-2018 Hajin Jang
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

using NUglify;
using NUglify.Html;
using PEBakery.Helper;
using SQLite;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PEBakery.Core
{
    #region enum LogState
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
    #endregion

    #region struct LogInfo
    [Serializable]
    public struct LogInfo
    {
        #region Fields
        public LogState State;
        public string Message;
        public CodeCommand Command;
        public UIControl UIControl;
        public int Depth;
        #endregion

        #region Constructor - LogState, Message
        public LogInfo(LogState state, string message)
        {
            State = state;
            Message = message;
            Command = null;
            UIControl = null;
            Depth = 0;
        }

        public LogInfo(LogState state, string message, CodeCommand command)
        {
            State = state;
            Message = message;
            Command = command;
            UIControl = null;
            Depth = 0;
        }

        public LogInfo(LogState state, string message, int depth)
        {
            State = state;
            Message = message;
            Command = null;
            UIControl = null;
            Depth = depth;
        }

        public LogInfo(LogState state, string message, CodeCommand command, int depth)
        {
            State = state;
            Message = message;
            Command = command;
            UIControl = null;
            Depth = depth;
        }
        #endregion

        #region Constructor - LogState, Exception
        public LogInfo(LogState state, Exception e)
        {
            State = state;
            Message = Logger.LogExceptionMessage(e);
            Command = null;
            UIControl = null;
            Depth = 0;
        }

        public LogInfo(LogState state, Exception e, CodeCommand command)
        {
            State = state;
            Message = Logger.LogExceptionMessage(e);
            Command = command;
            UIControl = null;
            Depth = 0;
        }

        public LogInfo(LogState state, Exception e, int depth)
        {
            State = state;
            Message = Logger.LogExceptionMessage(e);
            Command = null;
            UIControl = null;
            Depth = depth;
        }

        public LogInfo(LogState state, Exception e, CodeCommand command, int depth)
        {
            State = state;
            Message = Logger.LogExceptionMessage(e);
            Command = command;
            UIControl = null;
            Depth = depth;
        }
        #endregion

        #region Constructor - LogState, UIControl
        public LogInfo(LogState state, string message, UIControl uiCtrl)
        {
            State = state;
            Message = message;
            Command = null;
            UIControl = uiCtrl;
            Depth = 0;
        }

        public LogInfo(LogState state, Exception e, UIControl uiCtrl)
        {
            State = state;
            Message = Logger.LogExceptionMessage(e);
            Command = null;
            UIControl = uiCtrl;
            Depth = 0;
        }
        #endregion

        #region Constructor - DB_BuildLog
        public LogInfo(DB_BuildLog buildLog)
        {
            State = buildLog.State;
            Message = buildLog.Message;
            Command = null;
            UIControl = null;
            Depth = buildLog.Depth;
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
            else if (UIControl != null)
            {
                if (0 < UIControl.LineIdx)
                    return $"[{State}] {Message} ({UIControl.RawLine}) (Line {UIControl.LineIdx})";
                else
                    return $"[{State}] {Message} ({UIControl.RawLine})";
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
        public SystemLogUpdateEventArgs(DB_SystemLog log)
        {
            Log = log;
        }
    }
    public class BuildInfoUpdateEventArgs : EventArgs
    {
        public DB_BuildInfo Log { get; set; }
        public BuildInfoUpdateEventArgs(DB_BuildInfo log)
        {
            Log = log;
        }
    }
    public class BuildLogUpdateEventArgs : EventArgs
    {
        public DB_BuildLog Log { get; set; }
        public BuildLogUpdateEventArgs(DB_BuildLog log)
        {
            Log = log;
        }
    }
    public class ScriptUpdateEventArgs : EventArgs
    {
        public DB_Script Log { get; set; }
        public ScriptUpdateEventArgs(DB_Script log)
        {
            Log = log;
        }
    }
    public class VariableUpdateEventArgs : EventArgs
    {
        public DB_Variable Log { get; set; }
        public VariableUpdateEventArgs(DB_Variable log)
        {
            Log = log;
        }
    }

    // For NoDelay, PartDelay
    public delegate void SystemLogUpdateEventHandler(object sender, SystemLogUpdateEventArgs e);
    public delegate void BuildLogUpdateEventHandler(object sender, BuildLogUpdateEventArgs e);
    public delegate void BuildInfoUpdateEventHandler(object sender, BuildInfoUpdateEventArgs e);
    public delegate void ScriptUpdateEventHandler(object sender, ScriptUpdateEventArgs e);
    public delegate void VariableUpdateEventHandler(object sender, VariableUpdateEventArgs e);
    // For FullDelay
    public delegate void FullRefreshEventHandler(object sender, EventArgs e);
    #endregion

    #region LogEnum
    public enum LogExportType
    {
        Text, Html
    }

    /// <summary>
    /// How much information will be logged if an Exception is catched in ExecuteCommand?
    /// </summary>
    public enum LogDebugLevel
    {
        Production = 0, // Only Exception message
        PrintException = 1, // Print Exception message with Exception type
        PrintExceptionStackTrace = 2, // Print Exception message, type, and stack trace
    }
    #endregion

    #region DeferredLogging
    public class DeferredLogging
    {
        public int CurrentScriptId;
        public DB_BuildInfo BuildInfo;
        public List<DB_Script> ScriptLogPool; // Only used in FullDeferredLogging
        public List<DB_Variable> VariablePool; // Only used in FullDeferredLogging
        public List<DB_BuildLog> BuildLogPool;

        private readonly Dictionary<int, int> _scriptIdMatchDict; // Only used in FullDeferredLogging

        public DeferredLogging(bool fullDelayed)
        {
            CurrentScriptId = 0;
            BuildInfo = null;
            BuildLogPool = new List<DB_BuildLog>(1024);
            if (fullDelayed)
            {
                ScriptLogPool = new List<DB_Script>(64);
                VariablePool = new List<DB_Variable>(128);
                _scriptIdMatchDict = new Dictionary<int, int>(64) { [0] = 0 };
            }
        }

        /// <summary>
        /// Flush FullDeferredLogging
        /// </summary>
        /// <param name="s">EngineState</param>
        /// <returns>Real BuildId after written to database</returns>
        public int FlushFullDeferred(EngineState s)
        {
            if (s.LogMode != LogMode.FullDefer)
                return s.BuildId;

            const string internalErrMsg = "Internal Logic Error at DeferredLogging.FlushFullDeferred";
            Debug.Assert(BuildInfo != null, internalErrMsg);
            Debug.Assert(ScriptLogPool != null, internalErrMsg);
            Debug.Assert(VariablePool != null, internalErrMsg);
            Debug.Assert(BuildLogPool != null, internalErrMsg);

            if (BuildInfo.Id <= 0)
            { // Write to database if it did not
                BuildInfo.Id = 0;
                s.Logger.Db.Insert(BuildInfo);
            }
            int buildId = BuildInfo.Id;

            // Flush ScriptLogPool
            DB_Script[] newScriptLogPool = ScriptLogPool.Where(x => x.Id < 0).ToArray();
            Dictionary<string, int> scriptOldIdDict = newScriptLogPool.ToDictionary(x => x.FullIdentifier, x => x.Id);
            foreach (DB_Script log in newScriptLogPool)
            {
                log.Id = 0;
                log.BuildId = buildId;
            }
            s.Logger.Db.InsertAll(newScriptLogPool);
            foreach (DB_Script log in newScriptLogPool)
            {
                int oldId = scriptOldIdDict[log.FullIdentifier];
                _scriptIdMatchDict[oldId] = log.Id;
            }

            // [DeferredVariablePool]
            // s.ScriptId is 0 -> fixed/global variables
            // s.ScriptId is -N -> local variables

            // Flush VariablePool
            DB_Variable[] newVariablePool = VariablePool.Where(x => x.Id == 0).ToArray();
            foreach (DB_Variable log in newVariablePool)
            {
                log.BuildId = buildId;
                log.ScriptId = _scriptIdMatchDict[log.ScriptId];
            }
            s.Logger.Db.InsertAll(newVariablePool);

            // Flush BuildLogPool
            DB_BuildLog[] newBuildLogPool = BuildLogPool.Where(x => x.Id == 0).ToArray();
            foreach (DB_BuildLog log in newBuildLogPool)
            {
                log.BuildId = buildId;
                log.ScriptId = _scriptIdMatchDict[log.ScriptId];
                log.RefScriptId = _scriptIdMatchDict[log.RefScriptId];
            }
            s.Logger.Db.InsertAll(newBuildLogPool);

            // ScriptIdMatchDict should be kept alive
            ScriptLogPool.Clear();
            VariablePool.Clear();
            BuildLogPool.Clear();
            s.Logger.InvokeFullRefresh();

            return buildId;
        }
    }
    #endregion

    #region Logger
    public class Logger : IDisposable
    {
        #region Fields and Properties
        public LogDatabase Db { get; private set; }
        public bool SuspendBuildLog = false;

        public static LogDebugLevel DebugLevel;
        public static bool MinifyHtmlExport;

        private readonly ConcurrentDictionary<int, DB_BuildInfo> _buildDict = new ConcurrentDictionary<int, DB_BuildInfo>();
        private readonly ConcurrentDictionary<int, Tuple<DB_Script, Stopwatch>> _scriptWatchDict = new ConcurrentDictionary<int, Tuple<DB_Script, Stopwatch>>();
        private readonly ConcurrentDictionary<string, int> _scriptRefIdDict = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);

        public event SystemLogUpdateEventHandler SystemLogUpdated;
        public event BuildLogUpdateEventHandler BuildLogUpdated;
        public event BuildInfoUpdateEventHandler BuildInfoUpdated;
        public event ScriptUpdateEventHandler ScriptUpdated;
        public event VariableUpdateEventHandler VariableUpdated;
        public event FullRefreshEventHandler FullRefresh;

        public const string LogSeperator = "--------------------------------------------------------------------------------";

        // DelayedLogging
        private DeferredLogging _deferred;
        public DeferredLogging Deferred
        {
            get
            {
                DeferredLogging ret = _deferred;
                _deferred = null;
                return ret;
            }
        }
        #endregion

        #region Constructor, Destructor
        public Logger(string path)
        {
            Db = new LogDatabase(path);
        }

        ~Logger()
        {
            Dispose(false);
            GC.SuppressFinalize(this);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing && Db != null)
            {
                Db.Close();
                Db = null;
            }
        }
        #endregion

        #region Flush
        /// <summary>
        /// Flush delayed logs
        /// </summary>
        /// <param name="s">EngineState</param>
        /// <returns>Real BuildId written to database</returns>
        public int Flush(EngineState s)
        {
            switch (s.LogMode)
            {
                case LogMode.PartDefer:
                    Db.InsertAll(_deferred.BuildLogPool);
                    _deferred.BuildLogPool.Clear();
                    break;
                case LogMode.FullDefer:
                    return _deferred.FlushFullDeferred(s);
            }
            return s.BuildId;
        }
        #endregion

        #region Build Init/Finish
        public int BuildInit(EngineState s, string name)
        {
            if (s.DisableLogger)
                return 0;

            // Build Id
            DB_BuildInfo dbBuild = new DB_BuildInfo
            {
                StartTime = s.StartTime,
                Name = name,
            };

            switch (s.LogMode)
            {
                case LogMode.PartDefer:
                    _deferred = new DeferredLogging(false);
                    break;
                case LogMode.FullDefer:
                    _deferred = new DeferredLogging(true);

                    dbBuild.Id = -1;
                    _deferred.BuildInfo = dbBuild;
                    break;
            }

            if (s.LogMode != LogMode.FullDefer)
            {
                Db.Insert(dbBuild);

                // Fire Event
                BuildInfoUpdated?.Invoke(this, new BuildInfoUpdateEventArgs(dbBuild));
            }

            s.BuildId = dbBuild.Id;
            _buildDict[dbBuild.Id] = dbBuild;

            // Variables - Fixed, Global, Local
            List<DB_Variable> varLogs = new List<DB_Variable>();
            foreach (VarsType type in Enum.GetValues(typeof(VarsType)))
            {
                Dictionary<string, string> dict = s.Variables.GetVarDict(type);
                foreach (var kv in dict)
                {
                    DB_Variable dbVar = new DB_Variable
                    {
                        BuildId = s.BuildId,
                        Type = type,
                        Key = kv.Key,
                        Value = kv.Value,
                    };
                    varLogs.Add(dbVar);

                    // Fire Event
                    if (s.LogMode != LogMode.FullDefer)
                        VariableUpdated?.Invoke(this, new VariableUpdateEventArgs(dbVar));
                }
            }

            if (s.LogMode == LogMode.FullDefer)
                _deferred.VariablePool.AddRange(varLogs);
            else
                Db.InsertAll(varLogs);

            SystemWrite(new LogInfo(LogState.Info, $"Build [{name}] started"));

            return s.BuildId;
        }

        public void BuildFinish(EngineState s)
        {
            if (s.DisableLogger)
                return;

            _buildDict.TryRemove(s.BuildId, out DB_BuildInfo dbBuild);
            if (dbBuild == null)
                throw new KeyNotFoundException($"Unable to find DB_BuildInfo instance, id = {s.BuildId}");

            dbBuild.EndTime = s.EndTime;
            switch (s.LogMode)
            {
                case LogMode.PartDefer:
                    Flush(s);
                    Db.Update(dbBuild);
                    break;
                case LogMode.NoDelay:
                    Db.Update(dbBuild);
                    break;
            }

            SystemWrite(new LogInfo(LogState.Info, $"Build [{dbBuild.Name}] finished ({s.Elapsed:h\\:mm\\:ss})"));

            _scriptRefIdDict.Clear();
        }
        #endregion

        #region Script Init/Finish
        public int BuildScriptInit(EngineState s, Script sc, int order, bool prepareBuild)
        {
            if (s.DisableLogger)
                return 0;

            DB_Script dbScript = new DB_Script
            {
                BuildId = s.BuildId,
                Level = sc.Level,
                Order = order,
                Name = sc.Title,
                RealPath = sc.RealPath,
                TreePath = sc.TreePath,
                Version = sc.Version,
            };

            if (prepareBuild)
            {
                dbScript.Name = "Prepare Build";
                dbScript.Version = "0";
            }

            if (s.LogMode == LogMode.FullDefer)
            {
                _deferred.CurrentScriptId -= 1;
                dbScript.Id = _deferred.CurrentScriptId;
                _deferred.ScriptLogPool.Add(dbScript);
            }
            else
            {
                Db.Insert(dbScript);
            }

            _scriptWatchDict[dbScript.Id] = new Tuple<DB_Script, Stopwatch>(dbScript, Stopwatch.StartNew());

            // Fire Event
            if (s.LogMode == LogMode.NoDelay)
                ScriptUpdated?.Invoke(this, new ScriptUpdateEventArgs(dbScript));

            return dbScript.Id;
        }

        public void BuildScriptFinish(EngineState s, Dictionary<string, string> localVars)
        {
            if (s.DisableLogger)
                return;

            if (s.LogMode == LogMode.PartDefer)
            {
                Db.InsertAll(_deferred.BuildLogPool);
                _deferred.BuildLogPool.Clear();
            }

            int buildId = s.BuildId;
            int scriptId = s.ScriptId;

            // Scripts 
            _scriptWatchDict.TryRemove(scriptId, out Tuple<DB_Script, Stopwatch> tuple);
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
                    DB_Variable dbVar = new DB_Variable
                    {
                        BuildId = buildId,
                        ScriptId = scriptId,
                        Type = VarsType.Local,
                        Key = kv.Key,
                        Value = kv.Value,
                    };
                    varLogs.Add(dbVar);

                    // Fire Event
                    if (s.LogMode != LogMode.FullDefer)
                        VariableUpdated?.Invoke(this, new VariableUpdateEventArgs(dbVar));
                }

                if (s.LogMode == LogMode.FullDefer)
                    _deferred.VariablePool.AddRange(varLogs);
                else
                    Db.InsertAll(varLogs);
            }

            if (s.LogMode != LogMode.FullDefer)
                Db.Update(dbScript);

            // Fire Event
            if (s.LogMode == LogMode.PartDefer)
                ScriptUpdated?.Invoke(this, new ScriptUpdateEventArgs(dbScript));
        }

        public int BuildRefScriptWrite(EngineState s, Script sc)
        {
            // If logger is disabled or suspended, skip
            if (SuspendBuildLog || s.DisableLogger)
                return 0;

            if (_scriptRefIdDict.ContainsKey(sc.FullIdentifier))
                return _scriptRefIdDict[sc.FullIdentifier];

            DB_Script dbScript = new DB_Script
            {
                BuildId = s.BuildId,
                Level = sc.Level,
                Order = 0, // Reference script log must set this to 0 
                Name = sc.Title,
                RealPath = sc.RealPath,
                TreePath = sc.TreePath,
                Version = sc.Version,
                ElapsedMilliSec = 0,
            };

            if (s.LogMode == LogMode.FullDefer)
            {
                _deferred.CurrentScriptId -= 1;
                dbScript.Id = _deferred.CurrentScriptId;
                _deferred.ScriptLogPool.Add(dbScript);
            }
            else
            {
                Db.Insert(dbScript);
            }

            _scriptRefIdDict[sc.FullIdentifier] = dbScript.Id;
            return dbScript.Id;
        }
        #endregion

        #region BuildWrite
        private void InternalBuildWrite(EngineState s, DB_BuildLog dbCode)
        {
            if (s == null)
            {
                Db.Insert(dbCode);

                // Fire Event
                BuildLogUpdated?.Invoke(this, new BuildLogUpdateEventArgs(dbCode));
            }
            else
            {
                switch (s.LogMode)
                {
                    case LogMode.FullDefer:
                    case LogMode.PartDefer:
                        _deferred.BuildLogPool.Add(dbCode);
                        break;
                    case LogMode.NoDelay:
                        Db.Insert(dbCode);
                        // Fire Event
                        BuildLogUpdated?.Invoke(this, new BuildLogUpdateEventArgs(dbCode));
                        break;
                }
            }
        }

        public void BuildWrite(EngineState s, string message)
        {
            // If logger is disabled or suspended, skip
            if (SuspendBuildLog || s.DisableLogger)
                return;

            DB_BuildLog dbCode = new DB_BuildLog
            {
                Time = DateTime.UtcNow,
                BuildId = s.BuildId,
                ScriptId = s.ScriptId,
                RefScriptId = s.RefScriptId,
                Message = message,
                Flags = s.InMacro ? DbBuildLogFlag.Macro : DbBuildLogFlag.None,
            };

            InternalBuildWrite(s, dbCode);
        }

        public void BuildWrite(EngineState s, LogInfo log)
        {
            // If logger is disabled or suspended, skip
            if (SuspendBuildLog || s.DisableLogger)
                return;

            // Normally this should be already done in Engine.ExecuteCommand.
            // But some commands like RunExec bypass Engine.ExecuteCommand and call Logger.BuildWrite directly when logging.
            // => Need to double-check 'muting logs' at Logger.BuildWrite.
            LogState state;
            if (s.ErrorOff != null &&
                (log.State == LogState.Error || log.State == LogState.Warning || log.State == LogState.Overwrite))
                state = LogState.Muted;
            else
                state = log.State;

            DB_BuildLog dbCode = new DB_BuildLog
            {
                Time = DateTime.UtcNow,
                BuildId = s.BuildId,
                ScriptId = s.ScriptId,
                RefScriptId = s.RefScriptId,
                Depth = log.Depth,
                State = state,
            };

            DbBuildLogFlag flags = s.InMacro ? DbBuildLogFlag.Macro : DbBuildLogFlag.None;
            if (log.Command == null)
            {
                dbCode.Flags = flags;
                dbCode.Message = log.Message;
            }
            else
            {
                if (log.Command.Type == CodeType.Comment)
                    flags |= DbBuildLogFlag.Comment;
                dbCode.Flags = flags;

                if (log.Message.Length == 0)
                    dbCode.Message = log.Command.Type.ToString();
                else
                    dbCode.Message = $"{log.Command.Type} - {log.Message}";
                dbCode.RawCode = log.Command.RawCode;
                dbCode.LineIdx = log.Command.LineIdx;
            }

            InternalBuildWrite(s, dbCode);
        }

        public void BuildWrite(EngineState s, IEnumerable<LogInfo> logs)
        {
            // If logger is disabled or suspended, skip
            if (SuspendBuildLog || s.DisableLogger)
                return;

            foreach (LogInfo log in logs)
                BuildWrite(s, log);
        }

        public void BuildWrite(int buildId, IEnumerable<LogInfo> logs)
        {
            foreach (LogInfo log in logs)
            {
                DB_BuildLog dbCode = new DB_BuildLog
                {
                    Time = DateTime.UtcNow,
                    BuildId = buildId,
                    ScriptId = 0,
                    RefScriptId = 0,
                    Depth = log.Depth,
                    State = log.State,
                };

                if (log.Command == null)
                {
                    dbCode.Flags = DbBuildLogFlag.None;
                    dbCode.Message = log.Message;
                }
                else
                {
                    dbCode.Flags = log.Command.Type == CodeType.Comment ? DbBuildLogFlag.Comment : DbBuildLogFlag.None;

                    if (log.Message.Length == 0)
                        dbCode.Message = log.Command.Type.ToString();
                    else
                        dbCode.Message = $"{log.Command.Type} - {log.Message}";
                    dbCode.RawCode = log.Command.RawCode;
                    dbCode.LineIdx = log.Command.LineIdx;
                }

                InternalBuildWrite(null, dbCode);
            }
        }
        #endregion

        #region SystemWrite
        public void SystemWrite(string message)
        {
            DB_SystemLog dbLog = new DB_SystemLog
            {
                Time = DateTime.UtcNow,
                State = LogState.None,
                Message = message,
            };

            Db.Insert(dbLog);

            // Fire Event
            SystemLogUpdated?.Invoke(this, new SystemLogUpdateEventArgs(dbLog));
        }

        public void SystemWrite(LogInfo log)
        {
            DB_SystemLog dbLog = new DB_SystemLog
            {
                Time = DateTime.UtcNow,
                State = log.State,
                Message = log.Message,
            };

            Db.Insert(dbLog);

            // Fire Event
            SystemLogUpdated?.Invoke(this, new SystemLogUpdateEventArgs(dbLog));
        }

        public void SystemWrite(IEnumerable<LogInfo> logs)
        {
            foreach (LogInfo log in logs)
                SystemWrite(log);
        }
        #endregion

        #region InvokeFullRefresh
        public void InvokeFullRefresh()
        {
            FullRefresh?.Invoke(this, new EventArgs());
        }
        #endregion

        #region LogStartOfSection, LogEndOfSection
        public void LogStartOfSection(EngineState s, ScriptSection section, int depth, bool logScriptName, Dictionary<int, string> inParams, List<string> outParams, CodeCommand cmd = null, bool forceLog = false)
        {
            // If logger is disabled or suspended, skip
            if (SuspendBuildLog || s.DisableLogger)
                return;

            if (logScriptName)
                LogStartOfSection(s, section.Name, depth, inParams, outParams, cmd);
            else
                LogStartOfSection(s, section.Script.TreePath, section.Name, depth, inParams, outParams, cmd);
        }

        public void LogStartOfSection(EngineState s, string sectionName, int depth, Dictionary<int, string> inParams = null, List<string> outParams = null, CodeCommand cmd = null)
        {
            // If logger is disabled or suspended, skip
            if (SuspendBuildLog || s.DisableLogger)
                return;

            string msg = $"Processing Section [{sectionName}]";
            if (cmd == null)
                BuildWrite(s, new LogInfo(LogState.Info, msg, depth));
            else
                BuildWrite(s, new LogInfo(LogState.Info, msg, cmd, depth));

            LogSectionParameter(s, depth, inParams, outParams, cmd);
        }

        public void LogStartOfSection(EngineState s, string scriptName, string sectionName, int depth, Dictionary<int, string> inParams = null, List<string> outParams = null, CodeCommand cmd = null)
        {
            // If logger is disabled or suspended, skip
            if (SuspendBuildLog || s.DisableLogger)
                return;

            string msg = $"Processing [{scriptName}]'s Section [{sectionName}]";
            if (cmd == null)
                BuildWrite(s, new LogInfo(LogState.Info, msg, depth));
            else
                BuildWrite(s, new LogInfo(LogState.Info, msg, cmd, depth));

            LogSectionParameter(s, depth, inParams, outParams, cmd);
        }

        public void LogEndOfSection(EngineState s, ScriptSection section, int depth, bool logScriptName, CodeCommand cmd = null, bool forceLog = false)
        {
            // If logger is disabled or suspended, skip
            if (SuspendBuildLog || s.DisableLogger)
                return;

            if (logScriptName)
                LogEndOfSection(s, section.Name, depth, cmd);
            else
                LogEndOfSection(s, section.Script.TreePath, section.Name, depth, cmd);
        }

        public void LogEndOfSection(EngineState s, string sectionName, int depth, CodeCommand cmd = null)
        {
            // If logger is disabled or suspended, skip
            if (SuspendBuildLog || s.DisableLogger)
                return;

            string msg = $"End of Section [{sectionName}]";
            if (cmd == null)
                BuildWrite(s, new LogInfo(LogState.Info, msg, depth));
            else
                BuildWrite(s, new LogInfo(LogState.Info, msg, cmd, depth));
        }

        public void LogEndOfSection(EngineState s, string scriptName, string sectionName, int depth, CodeCommand cmd = null)
        {
            // If logger is disabled or suspended, skip
            if (SuspendBuildLog || s.DisableLogger)
                return;

            string msg = $"End of [{scriptName}]'s Section [{sectionName}]";
            if (cmd == null)
                BuildWrite(s, new LogInfo(LogState.Info, msg, depth));
            else
                BuildWrite(s, new LogInfo(LogState.Info, msg, cmd, depth));
        }
        #endregion

        #region LogSectionParameter
        public void LogSectionParameter(EngineState s, int depth, Dictionary<int, string> inParams = null, List<string> outParams = null, CodeCommand cmd = null)
        {
            if (s.DisableLogger)
                return;

            // Write Section In Parameters
            if (inParams != null && 0 < inParams.Count)
            {
                int cnt = 0;
                StringBuilder b = new StringBuilder();
                b.Append("InParams = { ");
                foreach (var kv in inParams)
                {
                    b.Append($"#{kv.Key}:[{kv.Value}]");
                    if (cnt + 1 < inParams.Count)
                        b.Append(", ");
                    cnt++;
                }
                b.Append(" }");

                if (cmd == null)
                    BuildWrite(s, new LogInfo(LogState.Info, b.ToString(), depth + 1));
                else
                    BuildWrite(s, new LogInfo(LogState.Info, b.ToString(), cmd, depth + 1));
            }

            // Write Section Out Parameters
            if (outParams != null && 0 < outParams.Count && !s.CompatDisableExtendedSectionParams)
            {
                StringBuilder b = new StringBuilder();
                b.Append("OutParams = { ");
                for (int i = 0; i < outParams.Count; i++)
                {
                    b.Append($"#o{i}:[{outParams[i]}]");
                    if (i + 1 < outParams.Count)
                        b.Append(", ");
                }
                b.Append(" }");

                if (cmd == null)
                    BuildWrite(s, new LogInfo(LogState.Info, b.ToString(), depth + 1));
                else
                    BuildWrite(s, new LogInfo(LogState.Info, b.ToString(), cmd, depth + 1));
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
            if (type == LogExportType.Html && MinifyHtmlExport)
            {
                string rawHtml;
                using (StringWriter w = new StringWriter())
                {
                    LogExporter exporter = new LogExporter(Db, type, w);
                    exporter.ExportSystemLog();
                    rawHtml = w.ToString();
                }

                // Do not collapse CRLF in td, th
                HtmlSettings uglifySettings = new HtmlSettings
                {
                    RemoveOptionalTags = false,
                };
                uglifySettings.TagsWithNonCollapsableWhitespaces["td"] = true;
                UglifyResult res = Uglify.Html(rawHtml, uglifySettings);
                using (StreamWriter w = new StreamWriter(exportFile, false, Encoding.UTF8))
                {
                    if (res.HasErrors)
                    {
                        StringBuilder b = new StringBuilder($"{res.Errors.Count} error reported while minifying html");
                        foreach (UglifyError err in res.Errors)
                            b.AppendLine(err.ToString());

                        SystemWrite(new LogInfo(LogState.Success, b.ToString()));
                        w.Write(rawHtml);
                    }
                    else
                    {
                        w.Write(res.Code);
                    }
                }
            }
            else
            {
                using (StreamWriter w = new StreamWriter(exportFile, false, Encoding.UTF8))
                {
                    LogExporter exporter = new LogExporter(Db, type, w);
                    exporter.ExportSystemLog();
                }
            }
        }

        public void ExportBuildLog(LogExportType type, string exportFile, int buildId, LogExporter.BuildLogOptions opts)
        {
            if (type == LogExportType.Html && MinifyHtmlExport)
            {
                string rawHtml;
                using (StringWriter w = new StringWriter())
                {
                    LogExporter exporter = new LogExporter(Db, type, w);
                    exporter.ExportBuildLog(buildId, opts);
                    rawHtml = w.ToString();
                }

                // Do not collapse CRLF in td, th
                HtmlSettings uglifySettings = new HtmlSettings
                {
                    RemoveOptionalTags = false,
                };
                uglifySettings.TagsWithNonCollapsableWhitespaces["td"] = true;
                UglifyResult res = Uglify.Html(rawHtml, uglifySettings);
                using (StreamWriter w = new StreamWriter(exportFile, false, Encoding.UTF8))
                {
                    if (res.HasErrors)
                    {
                        StringBuilder b = new StringBuilder($"{res.Errors.Count} error reported while minifying html");
                        foreach (UglifyError err in res.Errors)
                            b.AppendLine(err.ToString());

                        SystemWrite(new LogInfo(LogState.Success, b.ToString()));
                        w.Write(rawHtml);
                    }
                    else
                    {
                        w.Write(res.Code);
                    }
                }
            }
            else
            {
                using (StreamWriter w = new StreamWriter(exportFile, false, Encoding.UTF8))
                {
                    LogExporter exporter = new LogExporter(Db, type, w);
                    exporter.ExportBuildLog(buildId, opts);
                }
            }
        }
        #endregion

        #region LogExceptionMessage
        public static string LogExceptionMessage(Exception e)
        {
            switch (Logger.DebugLevel)
            {
                case LogDebugLevel.Production:
                    {
                        if (e is AggregateException aggEx)
                        {
                            StringBuilder b = new StringBuilder();
                            b.Append(StringHelper.RemoveLastNewLine(aggEx.Message));
                            foreach (var inEx in aggEx.InnerExceptions)
                            {
                                b.Append("\r\n    ");
                                b.Append(StringHelper.RemoveLastNewLine(inEx.Message));
                            }
                            b.Append("\r\n ");
                            return b.ToString();
                        }
                        return StringHelper.RemoveLastNewLine(e.Message);
                    }
                case LogDebugLevel.PrintException:
                    {
                        if (e is AggregateException aggEx)
                        {
                            StringBuilder b = new StringBuilder();
                            b.Append(e.GetType());
                            b.Append(": ");
                            b.Append(StringHelper.RemoveLastNewLine(aggEx.Message));
                            foreach (var inEx in aggEx.InnerExceptions)
                            {
                                b.Append("\r\n    ");
                                b.Append(inEx.GetType());
                                b.Append(": ");
                                b.Append(StringHelper.RemoveLastNewLine(inEx.Message));
                            }
                            b.Append("\r\n ");
                            return b.ToString();
                        }
                        return e.GetType() + ": " + StringHelper.RemoveLastNewLine(e.Message);
                    }
                case LogDebugLevel.PrintExceptionStackTrace:
                    {
                        if (e is AggregateException aggEx)
                        {
                            StringBuilder b = new StringBuilder();
                            b.Append(e.GetType());
                            b.Append(": ");
                            b.Append(StringHelper.RemoveLastNewLine(aggEx.Message));
                            foreach (var inEx in aggEx.InnerExceptions)
                            {
                                b.Append("\r\n    ");
                                b.Append(inEx.GetType());
                                b.Append(": ");
                                b.Append(StringHelper.RemoveLastNewLine(inEx.Message));
                            }
                            b.Append("\r\n");
                            b.Append(e.StackTrace);
                            b.Append("\r\n ");
                            return b.ToString();
                        }
                        return e.GetType() + ": " + StringHelper.RemoveLastNewLine(e.Message) + "\r\n" + e.StackTrace + "\r\n ";
                    }
                default:
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
        public int Id { get; set; }
        public DateTime Time { get; set; }
        public LogState State { get; set; }
        [MaxLength(65535)]
        public string Message { get; set; }

        // Used in LogWindow
        [Ignore]
        public string StateStr => State == LogState.None ? string.Empty : State.ToString();

        [Ignore]
        public string TimeStr => Time.ToLocalTime().ToString("yyyy-MM-dd hh:mm:ss tt", CultureInfo.InvariantCulture);

        public override string ToString()
        {
            return $"{Id} = [{State}] {Message}";
        }
    }

    public class DB_BuildInfo
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        [MaxLength(256)]
        public string Name { get; set; }

        [Ignore]
        public string TimeStr => StartTime.ToLocalTime().ToString("yyyy-MM-dd hh:mm:ss tt", CultureInfo.InvariantCulture);
        [Ignore]
        public string Text => $"[{TimeStr}] {Name} ({Id})";

        public override string ToString()
        {
            return $"{Id} = {Name}";
        }
    }

    public class DB_Script
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        [Indexed]
        public int BuildId { get; set; }
        // Referenced scripts   : Set to 0 (Usually macro script)
        // Active build scripts : Starts from 1
        public int Order { get; set; }
        public int Level { get; set; }
        [MaxLength(256)]
        public string Name { get; set; }
        [MaxLength(32767)] // https://msdn.microsoft.com/library/windows/desktop/aa365247.aspx#maxpath
        public string RealPath { get; set; }
        public string TreePath { get; set; }
        public string Version { get; set; }
        public long ElapsedMilliSec { get; set; }

        public override string ToString()
        {
            return $"{BuildId},{Id} = {Level} {Name} {Version}";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// $"{Level}_{RealPath}_{TreePath}" is not enough.
        /// MainScript's [Process] section is called prior to every build.
        /// If one intended to run MainScript's another section, two DB_Script instance will have same FullIdentifier.
        /// To handle this, add Order and Name as a safeguard.
        /// </remarks>
        public string FullIdentifier => $"{Level}_{Order}_{Name}_{RealPath}_{TreePath}";
    }

    public class DB_Variable
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        [Indexed]
        public int BuildId { get; set; }
        [Indexed]
        public int ScriptId { get; set; }
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

    [Flags]
    public enum DbBuildLogFlag
    {
        None = 0x00,
        Comment = 0x01,
        Macro = 0x02
    }

    public class DB_BuildLog
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public DateTime Time { get; set; }
        [Indexed]
        public int BuildId { get; set; }
        [Indexed]
        public int ScriptId { get; set; } // Where the command was called
        public int RefScriptId { get; set; } // Where the command resides in (Run/Exec). 0 if invalid.
        public int Depth { get; set; }
        public LogState State { get; set; }
        [MaxLength(65535)]
        public string Message { get; set; }
        public int LineIdx { get; set; }
        [MaxLength(65535)]
        public string RawCode { get; set; }
        public DbBuildLogFlag Flags { get; set; }

        [Ignore]
        public string Text => Export(LogExportType.Text, true);
        public string Export(LogExportType type, bool logDepth)
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

                        if ((State == LogState.Error || State == LogState.Warning) && 0 < LineIdx)
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

                        if ((State == LogState.Error || State == LogState.Warning) && 0 < LineIdx)
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

    #region LogDatabase
    public class LogDatabase : SQLiteConnection
    {
        public LogDatabase(string path)
            : base(path, SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.FullMutex)
        {
            CreateTable<DB_SystemLog>();
            CreateTable<DB_BuildInfo>();
            CreateTable<DB_Script>();
            CreateTable<DB_Variable>();
            CreateTable<DB_BuildLog>();
        }

        #region ClearTable
        public struct ClearTableOptions
        {
            public bool SystemLog;
            public bool BuildInfo;
            public bool Script;
            public bool BuildLog;
            public bool Variable;
        }

        public void ClearTable(ClearTableOptions opts)
        {
            if (opts.SystemLog)
                DeleteAll<DB_SystemLog>();
            if (opts.BuildInfo)
                DeleteAll<DB_BuildInfo>();
            if (opts.Script)
                DeleteAll<DB_Script>();
            if (opts.BuildLog)
                DeleteAll<DB_BuildLog>();
            if (opts.Variable)
                DeleteAll<DB_Variable>();
            Execute("VACUUM");
        }
        #endregion
    }
    #endregion
}
