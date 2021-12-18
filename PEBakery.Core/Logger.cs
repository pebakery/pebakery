﻿/*
   Copyright (C) 2016-2020 Hajin Jang
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

namespace PEBakery.Core
{
    #region EventHandlers
    public sealed class SystemLogUpdateEventArgs : EventArgs
    {
        public LogModel.SystemLog Log { get; set; }
        public LogModel.SystemLog[] Logs { get; set; }

        public SystemLogUpdateEventArgs(LogModel.SystemLog log)
        {
            Log = log;
        }

        public SystemLogUpdateEventArgs(LogModel.SystemLog[] logs)
        {
            Logs = logs;
        }
    }
    public sealed class BuildInfoUpdateEventArgs : EventArgs
    {
        public LogModel.BuildInfo Log { get; set; }
        public BuildInfoUpdateEventArgs(LogModel.BuildInfo log)
        {
            Log = log;
        }
    }
    public sealed class BuildLogUpdateEventArgs : EventArgs
    {
        public LogModel.BuildLog Log { get; set; }
        public BuildLogUpdateEventArgs(LogModel.BuildLog log)
        {
            Log = log;
        }
    }
    public sealed class ScriptUpdateEventArgs : EventArgs
    {
        public LogModel.Script Log { get; set; }
        public ScriptUpdateEventArgs(LogModel.Script log)
        {
            Log = log;
        }
    }
    public sealed class VariableUpdateEventArgs : EventArgs
    {
        public LogModel.Variable Log { get; set; }
        public VariableUpdateEventArgs(LogModel.Variable log)
        {
            Log = log;
        }
    }

    // For NoDefer, PartDefer
    public delegate void SystemLogUpdateEventHandler(object sender, SystemLogUpdateEventArgs e);
    public delegate void BuildLogUpdateEventHandler(object sender, BuildLogUpdateEventArgs e);
    public delegate void BuildInfoUpdateEventHandler(object sender, BuildInfoUpdateEventArgs e);
    public delegate void ScriptUpdateEventHandler(object sender, ScriptUpdateEventArgs e);
    public delegate void VariableUpdateEventHandler(object sender, VariableUpdateEventArgs e);
    // For FullDefer
    public delegate void FullRefreshEventHandler(object sender, EventArgs e);
    #endregion

    #region LogEnum
    public enum LogExportType
    {
        Text, Html
    }

    /// <summary>
    /// How much information will be logged if an Exception is caught in ExecuteCommand?
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
        public LogModel.BuildInfo BuildInfo;
        public List<LogModel.Script> ScriptLogPool; // Only used in FullDeferredLogging
        public List<LogModel.Variable> VariablePool; // Only used in FullDeferredLogging
        public List<LogModel.BuildLog> BuildLogPool;

        private readonly Dictionary<int, int> _scriptIdMatchDict; // Only used in FullDeferredLogging

        public DeferredLogging(bool fullDeferred)
        {
            CurrentScriptId = 0;
            BuildInfo = null;
            BuildLogPool = new List<LogModel.BuildLog>(1024);
            if (fullDeferred)
            {
                ScriptLogPool = new List<LogModel.Script>(64);
                VariablePool = new List<LogModel.Variable>(128);
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
            LogModel.Script[] newScriptLogPool = ScriptLogPool.Where(x => x.Id < 0).ToArray();
            Dictionary<string, int> scriptOldIdDict = newScriptLogPool.ToDictionary(x => x.FullIdentifier, x => x.Id);
            foreach (LogModel.Script log in newScriptLogPool)
            {
                log.Id = 0;
                log.BuildId = buildId;
            }
            s.Logger.Db.InsertAll(newScriptLogPool);
            foreach (LogModel.Script log in newScriptLogPool)
            {
                int oldId = scriptOldIdDict[log.FullIdentifier];
                _scriptIdMatchDict[oldId] = log.Id;
            }

            // [DeferredVariablePool]
            // s.ScriptId is 0 -> fixed/global variables
            // s.ScriptId is -N -> local variables

            // Flush VariablePool
            LogModel.Variable[] newVariablePool = VariablePool.Where(x => x.Id == 0).ToArray();
            foreach (LogModel.Variable log in newVariablePool)
            {
                log.BuildId = buildId;
                log.ScriptId = _scriptIdMatchDict[log.ScriptId];
            }
            s.Logger.Db.InsertAll(newVariablePool);

            // Flush BuildLogPool
            LogModel.BuildLog[] newBuildLogPool = BuildLogPool.Where(x => x.Id == 0).ToArray();
            foreach (LogModel.BuildLog log in newBuildLogPool)
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
        public const string LogSeparator = "--------------------------------------------------------------------------------";

        public LogDatabase Db { get; private set; }
        public bool SuspendBuildLog = false;

        public static LogDebugLevel DebugLevel;
        public static bool MinifyHtmlExport;

        // Deferred logging and LogModel pool
        private DeferredLogging _deferred;
        private readonly ConcurrentDictionary<int, LogModel.BuildInfo> _buildDict = new ConcurrentDictionary<int, LogModel.BuildInfo>();
        private readonly ConcurrentDictionary<int, LogModel.Script> _scriptDict = new ConcurrentDictionary<int, LogModel.Script>();
        private readonly ConcurrentDictionary<string, LogModel.Script> _scriptRefIdDict = new ConcurrentDictionary<string, LogModel.Script>(StringComparer.Ordinal);

        // Event
        public event SystemLogUpdateEventHandler SystemLogUpdated;
        public event BuildLogUpdateEventHandler BuildLogUpdated;
        public event BuildInfoUpdateEventHandler BuildInfoUpdated;
        public event ScriptUpdateEventHandler ScriptUpdated;
        public event VariableUpdateEventHandler VariableUpdated;
        public event FullRefreshEventHandler FullRefresh;
        #endregion

        #region Constructor, Destructor
        public Logger(string path)
        {
            Db = new LogDatabase(path);
        }

        ~Logger()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
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

        #region ClearDeferredLogging
        public DeferredLogging ReadAndClearDeferredLogs()
        {
            DeferredLogging deferred = _deferred;
            _deferred = null;
            return deferred;
        }
        #endregion

        #region Build Init/Finish
        public int BuildInit(EngineState s, string name)
        {
            if (s.DisableLogger)
                return 0;

            // Build Id
            LogModel.BuildInfo dbBuild = new LogModel.BuildInfo
            {
                PEBakeryVersion = Global.Const.ProgramVersionStrFull,
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
            List<LogModel.Variable> varLogs = new List<LogModel.Variable>();
            foreach (VarsType type in Enum.GetValues(typeof(VarsType)))
            {
                Dictionary<string, string> dict = s.Variables.GetVarDict(type);
                foreach (var kv in dict)
                {
                    LogModel.Variable dbVar = new LogModel.Variable
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

            bool ret = _buildDict.TryRemove(s.BuildId, out LogModel.BuildInfo dbBuild);
            if (!ret)
            {
                string errMsg = $"Build {s.BuildId} was not logged properly";
                SystemWrite(new LogInfo(LogState.Error, errMsg));
                Debug.Assert(false, errMsg);
                return;
            }

            dbBuild.FinishTime = s.EndTime;
            switch (s.LogMode)
            {
                case LogMode.PartDefer:
                    Flush(s);
                    Db.Update(dbBuild);
                    break;
                case LogMode.NoDefer:
                    Db.Update(dbBuild);
                    break;
            }

            BuildInfoUpdated?.Invoke(this, new BuildInfoUpdateEventArgs(dbBuild));
            SystemWrite(new LogInfo(LogState.Info, $"Build [{dbBuild.Name}] finished ({s.Elapsed:h\\:mm\\:ss})"));

            _scriptRefIdDict.Clear();
        }
        #endregion

        #region Script Init/Finish
        public int BuildScriptInit(EngineState s, Script sc, int order)
        {
            if (s.DisableLogger)
                return 0;

            LogModel.Script dbScript = new LogModel.Script
            {
                BuildId = s.BuildId,
                Level = sc.Level,
                Order = order,
                Name = sc.Title,
                RealPath = sc.RealPath,
                TreePath = sc.TreePath,
                Version = sc.TidyVersion,
                StartTime = DateTime.UtcNow,
                FinishTime = DateTime.MinValue,
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

            _scriptDict[dbScript.Id] = dbScript;

            // Fire Event
            if (s.LogMode == LogMode.NoDefer)
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

            // Log elapsed time
            bool ret = _scriptDict.TryRemove(scriptId, out LogModel.Script dbScript);
            if (!ret)
            {
                string errMsg = $"Script {s.ScriptId} was not logged properly";
                SystemWrite(new LogInfo(LogState.Error, errMsg));
                Debug.Assert(false, errMsg);
                return;
            }
            dbScript.FinishTime = DateTime.UtcNow;

            if (localVars != null)
            {
                List<LogModel.Variable> varLogs = new List<LogModel.Variable>(localVars.Count);
                foreach (var kv in localVars)
                {
                    LogModel.Variable dbVar = new LogModel.Variable
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

        /// <summary>
        /// Log referenced script.
        /// </summary>
        /// <param name="s">EngineSection</param>
        /// <param name="sc">Script</param>
        /// <param name="isMacro">MacroScript?</param>
        /// <returns></returns>
        public int BuildRefScriptWrite(EngineState s, Script sc, bool isMacro)
        {
            // If logger is disabled, skip
            if (s.DisableLogger)
                return 0;

            // If this script is already logged to database, just return cached id.
            if (_scriptRefIdDict.ContainsKey(sc.FullIdentifier))
            {
                LogModel.Script storedScript = _scriptRefIdDict[sc.FullIdentifier];
                return storedScript.Id;
            }

            LogModel.Script dbScript = new LogModel.Script
            {
                BuildId = s.BuildId,
                Level = sc.Level,
                Order = isMacro ? -1 : 0, // Referenced script log must set this to 0 
                Name = sc.Title,
                RealPath = sc.RealPath,
                TreePath = sc.TreePath,
                Version = sc.TidyVersion,
                StartTime = DateTime.UtcNow,
                FinishTime = DateTime.UtcNow, // Not valid on referenced script
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

            _scriptRefIdDict[sc.FullIdentifier] = dbScript;
            return dbScript.Id;
        }
        #endregion

        #region BuildWrite
        private void InternalBuildWrite(EngineState s, LogModel.BuildLog dbCode)
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
                    case LogMode.NoDefer:
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

            EngineLocalState ls = s.PeekLocalState();

            LogModel.BuildLogFlag flags = LogModel.BuildLogFlag.None;
            if (ls.IsMacro)
            {
                flags |= LogModel.BuildLogFlag.Macro;
            }
            else
            {
                if (ls.IsRefScript && s.ScriptId != ls.RefScriptId)
                    flags |= LogModel.BuildLogFlag.RefScript;
            }

            LogModel.BuildLog dbCode = new LogModel.BuildLog
            {
                Time = DateTime.UtcNow,
                BuildId = s.BuildId,
                ScriptId = s.ScriptId,
                RefScriptId = ls.RefScriptId,
                Message = message,
                Flags = flags,
            };

            InternalBuildWrite(s, dbCode);
        }

        public void BuildWrite(EngineState s, LogInfo log)
        {
            // If logger is disabled or suspended, skip
            if (SuspendBuildLog || s.DisableLogger)
                return;

            EngineLocalState ls = s.PeekLocalState();

            // Normally this should be already done in Engine.ExecuteCommand().
            // But some commands like RunExec bypass Engine.ExecuteCommand() and call Logger.BuildWrite() directly when logging.
            // => Need to double-check 'muting logs' at Logger.BuildWrite().
            LogState state;
            if (s.ErrorOff != null &&
                (log.State == LogState.Error || log.State == LogState.Warning || log.State == LogState.Overwrite))
                state = LogState.Muted;
            else
                state = log.State;

            LogModel.BuildLog dbCode = new LogModel.BuildLog
            {
                Time = DateTime.UtcNow,
                BuildId = s.BuildId,
                ScriptId = s.ScriptId,
                RefScriptId = ls.RefScriptId,
                Depth = log.Depth,
                State = state,
            };

            LogModel.BuildLogFlag flags = LogModel.BuildLogFlag.None;
            if (log.IsException)
                flags |= LogModel.BuildLogFlag.Exception;
            if (ls.IsMacro)
                flags |= LogModel.BuildLogFlag.Macro;
            else if (ls.IsRefScript && s.ScriptId != ls.RefScriptId)
                flags |= LogModel.BuildLogFlag.RefScript;

            if (log.Command == null)
            {
                dbCode.Flags = flags;
                dbCode.Message = log.Message;
            }
            else
            {
                if (log.Command.Type == CodeType.Comment)
                    flags |= LogModel.BuildLogFlag.Comment;
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
                LogModel.BuildLog dbCode = new LogModel.BuildLog
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
                    dbCode.Flags = LogModel.BuildLogFlag.None;
                    dbCode.Message = log.Message;
                }
                else
                {
                    LogModel.BuildLogFlag flags = LogModel.BuildLogFlag.None;
                    if (log.Command.Type == CodeType.Comment)
                        flags |= LogModel.BuildLogFlag.Comment;
                    if (log.IsException)
                        flags |= LogModel.BuildLogFlag.Exception;
                    dbCode.Flags = flags;

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
            LogModel.SystemLog dbLog = new LogModel.SystemLog
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
            LogModel.SystemLog dbLog = new LogModel.SystemLog
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
            LogModel.SystemLog[] dbLogs = logs.Select(log => new LogModel.SystemLog
            {
                Time = DateTime.UtcNow,
                State = log.State,
                Message = log.Message,
            }).ToArray();

            if (dbLogs.Length == 0)
                return;

            Db.InsertAll(dbLogs);

            // Fire Event
            SystemLogUpdated?.Invoke(this, new SystemLogUpdateEventArgs(dbLogs));
        }
        #endregion

        #region InvokeFullRefresh
        public void InvokeFullRefresh()
        {
            FullRefresh?.Invoke(this, new EventArgs());
        }
        #endregion

        #region LogStartOfSection, LogEndOfSection
        public void LogStartOfSection(EngineState s, ScriptSection section, int depth, bool logScriptName, Dictionary<int, string> inParams, List<string> outParams, CodeCommand cmd = null)
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

        public void LogEndOfSection(EngineState s, ScriptSection section, int depth, bool logScriptName, CodeCommand cmd = null)
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
                using (StringWriter sw = new StringWriter())
                {
                    LogExporter exporter = new LogExporter(Db, type, sw);
                    exporter.ExportSystemLog();
                    rawHtml = sw.ToString();
                }

                // Do not collapse CRLF in td, th
                HtmlSettings uglifySettings = new HtmlSettings
                {
                    RemoveOptionalTags = false,
                };
                uglifySettings.TagsWithNonCollapsibleWhitespaces["td"] = true;
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
                using (StreamWriter sw = new StreamWriter(exportFile, false, Encoding.UTF8))
                {
                    LogExporter exporter = new LogExporter(Db, type, sw);
                    exporter.ExportSystemLog();
                }
            }
        }

        public void ExportBuildLog(LogExportType type, string exportFile, int buildId, BuildLogOptions opts)
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
                uglifySettings.TagsWithNonCollapsibleWhitespaces["td"] = true;
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
        public static string LogExceptionMessage(Exception e) => LogExceptionMessage(e, DebugLevel);

        public static string LogExceptionMessage(Exception e, LogDebugLevel debugLevel)
        {
            switch (debugLevel)
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
    public sealed class LogModel
    {
        public class SystemLog
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

        public class BuildInfo
        {
            [PrimaryKey, AutoIncrement]
            public int Id { get; set; }
            // ReSharper disable once InconsistentNaming
            [MaxLength(32)]
            public string PEBakeryVersion { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime FinishTime { get; set; }
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

        public class Script
        {
            [PrimaryKey, AutoIncrement]
            public int Id { get; set; }
            [Indexed]
            public int BuildId { get; set; }
            /// <summary>
            /// Macro script         : Set to -1
            /// Referenced scripts   : Set to 0
            /// Active build scripts : Starts from 1
            /// </summary>
            public int Order { get; set; }
            public int Level { get; set; }
            public string Name { get; set; }
            public string RealPath { get; set; }
            public string TreePath { get; set; }
            public string Version { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime FinishTime { get; set; }

            public override string ToString() => $"({BuildId}, {Id}) = {Level} {Name} {Version}";

            /// <summary>
            /// 
            /// </summary>
            /// <remarks>
            /// $"{Level}_{RealPath}_{TreePath}" is not enough.
            /// MainScript's [Process] section is called prior to every build.
            /// If one intended to run MainScript's another section, two LogModel.Script instance will have same FullIdentifier.
            /// To handle this, add Order and Name as a safeguard.
            /// </remarks>
            public string FullIdentifier => $"{Level}_{Order}_{Name}_{RealPath}_{TreePath}";
        }

        public class ScriptEqualityComparer : IEqualityComparer<Script>
        {
            public static ScriptEqualityComparer Instance = new ScriptEqualityComparer();

            private ScriptEqualityComparer()
            {

            }

            public bool Equals(Script x, Script y)
            {
                if (x == null)
                {
                    if (y == null)
                        return true;
                    else
                        return false;
                }
                else
                {
                    if (y == null)
                        return false;
                    else
                        return x.Id == y.Id;
                }
            }

            public int GetHashCode(Script obj)
            {
                return obj.Id;
            }
        }

        public class Variable
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
        public enum BuildLogFlag
        {
            None = 0x00,
            /// <summary>
            /// This log represents a comment.
            /// </summary>
            Comment = 0x01,
            /// <summary>
            /// This log was generated by macro command.
            /// </summary>
            Macro = 0x02,
            /// <summary>
            /// This log was generated from referenced script.
            /// </summary>
            RefScript = 0x04,
            /// <summary>
            /// This log was generated by unforeseen exception.
            /// </summary>
            Exception = 0x08,
        }

        public static string BuildLogFlagToString(BuildLogFlag flags)
        {
            string str = string.Empty;

            // Comment
            if ((flags & BuildLogFlag.Comment) == BuildLogFlag.Comment)
                str += 'C';

            // Macro and RefScript cannot be set to true at the same time
            if ((flags & BuildLogFlag.Macro) == BuildLogFlag.Macro)
                str += 'M';
            else if ((flags & BuildLogFlag.RefScript) == BuildLogFlag.RefScript)
                str += 'R';

            // Exception
            if ((flags & BuildLogFlag.Exception) == BuildLogFlag.Exception)
                str += 'E';

            return str;
        }

        public static BuildLogFlag ParseBuildLogFlag(string str)
        {
            BuildLogFlag flags = BuildLogFlag.None;
            if (str.Contains('C'))
                flags |= BuildLogFlag.Comment;
            if (str.Contains('M'))
                flags |= BuildLogFlag.Macro;
            if (str.Contains('R'))
                flags |= BuildLogFlag.RefScript;
            if (str.Contains('E'))
                flags |= BuildLogFlag.Exception;
            return flags;
        }

        public class BuildLog
        {
            [PrimaryKey, AutoIncrement]
            public int Id { get; set; }
            public DateTime Time { get; set; }
            [Indexed]
            public int BuildId { get; set; }
            /// <summary>
            /// Where the command was called
            /// </summary>
            [Indexed]
            public int ScriptId { get; set; }
            /// <summary>
            /// Where the command resides in (Run/Exec).
            /// 0 is treated as 'not referenced script'.
            /// </summary>
            public int RefScriptId { get; set; }
            public int Depth { get; set; }
            public LogState State { get; set; }
            [MaxLength(4096)]
            public string Message { get; set; }
            public int LineIdx { get; set; }
            [MaxLength(4096)]
            public string RawCode { get; set; }
            public BuildLogFlag Flags { get; set; }

            [Ignore]
            public string Text => Export(LogExportType.Text, true, false);
            public string Export(LogExportType type, bool logDepth, bool logFlags)
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
                                {
                                    if (!logFlags || Flags == BuildLogFlag.None)
                                        b.Append(Message);
                                    else
                                        b.Append($"[{BuildLogFlagToString(Flags)}] {Message}");
                                }
                                else
                                {
                                    if (!logFlags || Flags == BuildLogFlag.None)
                                        b.Append($"{Message} ({RawCode})");
                                    else
                                        b.Append($"[{BuildLogFlagToString(Flags)}] {Message} ({RawCode})");
                                }
                            }
                            else
                            { // Has State
                                if (RawCode == null)
                                {
                                    if (!logFlags || Flags == BuildLogFlag.None)
                                        b.Append($"[{State}] {Message}");
                                    else
                                        b.Append($"[{State}] [{BuildLogFlagToString(Flags)}] {Message}");
                                }
                                else
                                {
                                    if (!logFlags || Flags == BuildLogFlag.None)
                                        b.Append($"[{State}] {Message} ({RawCode})");
                                    else
                                        b.Append($"[{State}] [{BuildLogFlagToString(Flags)}] {Message} ({RawCode})");

                                }
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
    }
    #endregion

    #region LogDatabase
    public class LogDatabase : SQLiteConnection
    {
        public LogDatabase(string path)
            : base(path, SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.FullMutex)
        {
            CreateTable<LogModel.SystemLog>();
            CreateTable<LogModel.BuildInfo>();
            CreateTable<LogModel.Script>();
            CreateTable<LogModel.Variable>();
            CreateTable<LogModel.BuildLog>();
        }

        #region ClearTable, ClearBuildLog
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
            RunInTransaction(() =>
            {
                if (opts.SystemLog)
                    DeleteAll<LogModel.SystemLog>();
                if (opts.BuildInfo)
                    DeleteAll<LogModel.BuildInfo>();
                if (opts.Script)
                    DeleteAll<LogModel.Script>();
                if (opts.BuildLog)
                    DeleteAll<LogModel.BuildLog>();
                if (opts.Variable)
                    DeleteAll<LogModel.Variable>();
            });
            Execute("VACUUM");
        }

        public void ClearBuildLog(int buildId)
        {
            RunInTransaction(() =>
            {
                Table<LogModel.Variable>().Delete(x => x.BuildId == buildId);
                Table<LogModel.BuildLog>().Delete(x => x.BuildId == buildId);
                Table<LogModel.Script>().Delete(x => x.BuildId == buildId);
                Table<LogModel.BuildInfo>().Delete(x => x.Id == buildId);
            });
            Execute("VACUUM");
        }
        #endregion
    }
    #endregion
}
