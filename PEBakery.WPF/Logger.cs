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
using SQLite.Net;
using SQLite.Net.Attributes;
using SQLite.Net.Platform.Win32;
using SQLiteNetExtensions.Attributes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            Depth = -1;
        }

        public LogInfo(LogState state, string message, CodeCommand command)
        {
            State = state;
            Message = message;
            Command = command;
            Depth = -1;
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
            Depth = -1;
        }

        public LogInfo(LogState state, Exception e, CodeCommand command)
        {
            State = state;
            Message = Logger.LogExceptionMessage(e);
            Command = command;
            Depth = -1;
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

    #region Logger Class
    public enum LogExportType
    {
        Text, HTML
    }

    public class Logger
    {    
        public LogDatabase DB;
        public int ErrorOffCount = 0;
        public bool SuspendLog = false;

        private readonly Dictionary<long, DB_Build> buildDict = new Dictionary<long, DB_Build>();
        private readonly Dictionary<long, Tuple<DB_Plugin, Stopwatch>> pluginDict = new Dictionary<long, Tuple<DB_Plugin, Stopwatch>>();

        public Logger(string path)
        {
            DB = new LogDatabase(path);
        }

        ~Logger()
        {
            DB.Close();
        }

        #region DB Manipulation
        public long Build_Init(DateTime startTime, string name, EngineState s)
        {
            // Build Id
            DB_Build dbBuild = new DB_Build()
            {
                StartTime = startTime,
                Name = name,
            };
            DB.Insert(dbBuild);
            buildDict[dbBuild.Id] = dbBuild;
            s.BuildId = dbBuild.Id;

            // Variables - Fixed, Global, Local
            foreach (VarsType type in Enum.GetValues(typeof(VarsType)))
            {
                ReadOnlyDictionary<string, string> dict = s.Variables.GetVars(type);
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
                }
            }

            return dbBuild.Id;
        }

        public void Build_Finish(long id)
        {
            DB_Build dbBuild = buildDict[id];
            dbBuild.EndTime = DateTime.Now;
            DB.Update(dbBuild);

            buildDict.Remove(id);
        }

        public long Build_Plugin_Init(long buildId, Plugin p, int order)
        {
            // Plugins 
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

            return dbPlugin.Id;
        }

        public void Build_Plugin_Finish(long id)
        {
            // Plugins 
            DB_Plugin dbPlugin = pluginDict[id].Item1;
            Stopwatch watch = pluginDict[id].Item2;
            dbPlugin.ElapsedMilliSec = watch.ElapsedMilliseconds;
            DB.Update(dbPlugin);

            watch.Stop();
            pluginDict.Remove(dbPlugin.Id);
        }

        /// <summary>
        /// Write LogInfo into DB_CodeLog
        /// </summary>
        /// <param name="buildId"></param>
        /// <param name="time"></param>
        /// <param name="message"></param>
        public void Build_Write(long buildId, string message)
        {
#if DEBUG
            Debug_Write(message);
#endif

            DB_CodeLog dbCode = new DB_CodeLog()
            {
                Time = DateTime.Now,
                BuildId = buildId,
                Message = message,
            };
            DB.Insert(dbCode);
        }

        public void Build_Write(long buildId, LogInfo log)
        {
#if DEBUG
            Debug_Write(log);
#endif

            DB_CodeLog dbCode = new DB_CodeLog()
            {
                Time = DateTime.Now,
                BuildId = buildId,
                Depth = log.Depth,
                State = log.State,
            };

            if (log.Command == null)
            {
                dbCode.Message = log.Message;
            }
            else
            {
                dbCode.Message = $"{log.Command.Type} - {log.Message}";
                dbCode.RawCode = log.Command.RawCode;
            }

            DB.Insert(dbCode);
        }

        public void Build_Write(long buildId, IEnumerable<LogInfo> logs)
        {
            foreach (LogInfo log in logs)
                Build_Write(buildId, log);
        }

        public void System_Write(LogInfo log)
        {
#if DEBUG
            Debug_Write(log);
#endif

            DB_SystemLog dbLog = new DB_SystemLog()
            {
                Time = DateTime.Now,
                State = log.State,
                Message = log.Message,
            };

            DB.Insert(dbLog);
        }

        public void Normal_Write(IEnumerable<LogInfo> logs)
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
        public void LogStartOfSection(long buildId, SectionAddress addr, int depth, bool logPluginName, CodeCommand cmd = null)
        {
            if (logPluginName)
                LogStartOfSection(buildId, addr.Section.SectionName, depth, cmd);
            else
                LogStartOfSection(buildId, addr.Plugin.ShortPath, addr.Section.SectionName, depth, cmd);
        }

        public void LogStartOfSection(long buildId, string sectionName, int depth, CodeCommand cmd = null)
        {
            string msg = $"Processing Section [{sectionName}]";
            if (cmd == null)
                Build_Write(buildId, new LogInfo(LogState.Info, msg, depth));
            else
                Build_Write(buildId, new LogInfo(LogState.Info, msg, cmd, depth));
        }

        public void LogStartOfSection(long buildId, string pluginName, string sectionName, int depth, CodeCommand cmd = null)
        {
            string msg = $"Processing [{pluginName}]'s Section [{sectionName}]";
            if (cmd == null)
                Build_Write(buildId, new LogInfo(LogState.Info, msg, depth));
            else
                Build_Write(buildId, new LogInfo(LogState.Info, msg, cmd, depth));
        }

        public void LogEndOfSection(long buildId, SectionAddress addr, int depth, bool logPluginName, CodeCommand cmd = null)
        {
            if (logPluginName)
                LogEndOfSection(buildId, addr.Section.SectionName, depth, cmd);
            else
                LogEndOfSection(buildId, addr.Plugin.ShortPath, addr.Section.SectionName, depth, cmd);
        }

        public void LogEndOfSection(long buildId, string sectionName, int depth, CodeCommand cmd = null)
        {
            string msg = $"End of Section [{sectionName}]";
            if (cmd == null)
                Build_Write(buildId, new LogInfo(LogState.Info, msg, depth));
            else
                Build_Write(buildId, new LogInfo(LogState.Info, msg, cmd, depth));
        }

        public void LogEndOfSection(long buildId, string pluginName, string sectionName, int depth, CodeCommand cmd = null)
        {
            string msg = $"End of [{pluginName}]'s Section [{sectionName}]";
            if (cmd == null)
                Build_Write(buildId, new LogInfo(LogState.Info, msg, depth));
            else
                Build_Write(buildId, new LogInfo(LogState.Info, msg, cmd, depth));
        }
        #endregion

        #region Export
        public void Export(LogExportType type, long buildId, string exportFile)
        {
            switch (type)
            {
                case LogExportType.Text:
                    {
                        using (StreamWriter writer = new StreamWriter(exportFile, false, Encoding.UTF8))
                        {
                            DB_Build dbBuild = DB.Table<DB_Build>().Where(x => x.Id == buildId).First();
                            writer.WriteLine($"- Build <{dbBuild.Name}> -");
                            writer.WriteLine($"Started at  {dbBuild.StartTime.ToString("yyyy-MM-dd h:mm:ss tt", CultureInfo.InvariantCulture)}");
                            writer.WriteLine($"Finished at {dbBuild.EndTime.ToString("yyyy-MM-dd h:mm:ss tt", CultureInfo.InvariantCulture)}");
                            TimeSpan t = dbBuild.EndTime - dbBuild.StartTime;
                            writer.WriteLine($"Took {t:h\\:mm\\:ss}");
                            writer.WriteLine();
                            writer.WriteLine();

                            writer.WriteLine("<Plugins>");
                            {
                                var plugins = DB.Table<DB_Plugin>()
                                    .Where(x => x.BuildId == buildId)
                                    .OrderBy(x => x.Order);

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
                                var codeLogs = DB.Table<DB_CodeLog>()
                                .Where(x => x.BuildId == buildId)
                                .OrderBy(x => x.Id);
                                foreach (DB_CodeLog log in codeLogs)
                                {
                                    for (int i = 0; i < log.Depth; i++)
                                        writer.Write("  ");

                                    if (log.State == LogState.None)
                                    { // No State
                                        if (log.RawCode == null)
                                            writer.WriteLine(log.Message);
                                        else
                                            writer.WriteLine($"{log.Message} ({log.RawCode})");
                                    }
                                    else
                                    { // Has State
                                        if (log.RawCode == null)
                                            writer.WriteLine($"[{log.State}] {log.Message}");
                                        else
                                            writer.WriteLine($"[{log.State}] {log.Message} ({log.RawCode})");
                                    }
                                }
                                writer.WriteLine();
                            }
                            writer.Close();
                        }
                    }
                    break;
                case LogExportType.HTML:
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
            switch (Engine.DebugLevel)
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
                case DebugLevel.PrintExceptionType:
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
                    return "Invalid DebugLevel. This is an internal error, PLEASE REPORT to PEBakery developer.";
            }
        }
        #endregion
    }
    #endregion


    #region SQLite Connection 
    #region Model
    public class DB_SystemLog
    {
        [PrimaryKey, AutoIncrement]
        public long Id { get; set; }
        public DateTime Time { get; set; }
        public LogState State { get; set; }
        [MaxLength(65535)]
        public string Message { get; set; }

        public override string ToString()
        {
            return $"{Id} = [{State}] {Message}";
        }
    }

    public class DB_Build
    {
        [PrimaryKey, AutoIncrement]
        public long Id { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        [MaxLength(256)]
        public string Name { get; set; }

        [OneToMany(CascadeOperations = CascadeOperation.All)] 
        public List<DB_Plugin> Plugins { get; set; }
        [OneToMany(CascadeOperations = CascadeOperation.All)] 
        public List<DB_Variable> Variables { get; set; }
        [OneToMany(CascadeOperations = CascadeOperation.All)]
        public List<DB_CodeLog> Logs { get; set; }

        public override string ToString()
        {
            return $"{Id} = {Name}";
        }
    }

    public class DB_Plugin
    {
        [PrimaryKey, AutoIncrement]
        public long Id { get; set; }
        [ForeignKey(typeof(DB_Build))]
        public long BuildId { get; set; }
        public int Order { get; set; } // Starts from 1
        public int Level { get; set; }
        [MaxLength(256)]
        public string Name { get; set; }
        [MaxLength(32767)] // https://msdn.microsoft.com/library/windows/desktop/aa365247.aspx#maxpath
        public string Path { get; set; }
        public int Version { get; set; }
        public long ElapsedMilliSec { get; set; }

        [ManyToOne]
        public DB_Build Build { get; set; }

        public override string ToString()
        {
            return $"{BuildId},{Id} = {Level} {Name} {Version}";
        }
    }

    public class DB_Variable
    {
        [PrimaryKey, AutoIncrement]
        public long Id { get; set; }
        [ForeignKey(typeof(DB_Build))]
        public long BuildId { get; set; }
        public VarsType Type { get; set; }
        [MaxLength(256)]
        public string Key { get; set; }
        [MaxLength(65535)]
        public string Value { get; set; }

        [ManyToOne]
        public DB_Build Build { get; set; }

        public override string ToString()
        {
            return $"{BuildId},{Id} = ({Type}) {Key}={Value}";
        }
    }

    public class DB_CodeLog
    {
        [PrimaryKey, AutoIncrement]
        public long Id { get; set; }
        public DateTime Time { get; set; }
        [ForeignKey(typeof(DB_Build))]
        public long BuildId { get; set; }
        public int Depth { get; set; }
        public LogState State { get; set; }
        [MaxLength(65535)]
        public string Message { get; set; }
        [MaxLength(65535)]
        public string RawCode { get; set; }

        [ManyToOne] 
        public DB_Build Build { get; set; }

        public override string ToString()
        {
            return $"{BuildId}, {Id} = [{State}, {Depth}] {Message} ({RawCode})";
        }
    }
    #endregion

    #region Database
    public class LogDatabase : SQLiteConnection
    {
        public LogDatabase(string path) : base(new SQLitePlatformWin32(), path)
        {
            CreateTable<DB_SystemLog>();
            CreateTable<DB_Build>();
            CreateTable<DB_Plugin>();
            CreateTable<DB_Variable>();
            CreateTable<DB_CodeLog>();
        }
    }
    #endregion
    #endregion
}
