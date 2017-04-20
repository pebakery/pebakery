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

        public static LogInfo AddCommand(LogInfo info, CodeCommand command)
        {
            info.Command = command;
            return info;

        }
        public static LogInfo AddCommand(LogInfo info, CodeCommand command, int depth)
        {
            info.Command = command;
            info.Depth = depth;
            return info;
        }
        #endregion

        /*
        #region Constructor - LogInfo
        public DetailLog(LogInfo log)
        {
            State = log.State;
            Message = log.Message;
            Command = null;
            Depth = -1;
        }

        public DetailLog(LogInfo log, CodeCommand command)
        {
            State = log.State;
            Message = log.Message;
            Command = command;
            Depth = -1;
        }

        public DetailLog(LogInfo log, int depth)
        {
            State = log.State;
            Message = log.Message;
            Command = null;
            Depth = depth;
        }

        public DetailLog(LogInfo log, CodeCommand command, int depth)
        {
            State = log.State;
            Message = log.Message;
            Command = command;
            Depth = depth;
        }
        #endregion
        */
    }
    #endregion

    public class Logger
    {
        #region Logger Class
        public Database DB;
        public int ErrorOffCount = 0;
        public bool SuspendLog = false;

        private readonly Dictionary<long, DB_Build> buildDict = new Dictionary<long, DB_Build>();
        private readonly Dictionary<long, Tuple<DB_Plugin, Stopwatch>> pluginDict = new Dictionary<long, Tuple<DB_Plugin, Stopwatch>>();

        public Logger(string path)
        {
            DB = new Database(path);
        }

        ~Logger()
        {
            DB.Close();
        }

        #region DB Manipulation
        public long BuildLog_Init(DateTime startTime, string name, EngineState s)
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

        public void BuildLog_Finish(long id)
        {
            DB_Build dbBuild = buildDict[id];
            dbBuild.EndTime = DateTime.Now;
            DB.Update(dbBuild);

            buildDict.Remove(id);
        }

        public long BuildLog_Plugin_Init(long buildId, Plugin p, int order)
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

        public void BuildLog_Plugin_Finish(long id)
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
        public void BuildLog_Write(long buildId, string message)
        {
            Debug_Write(message);

            DB_CodeLog dbCode = new DB_CodeLog()
            {
                Time = DateTime.Now,
                BuildId = buildId,
                Message = message,
            };
            DB.Insert(dbCode);
        }

        public void BuildLog_Write(long buildId, LogInfo log)
        {
            Debug_Write(log);

            DB_CodeLog dbCode = new DB_CodeLog()
            {
                Time = DateTime.Now,
                BuildId = buildId,
                Depth = log.Depth,
                State = log.State,
                Message = log.Message,
            };

            if (log.Command != null)
                dbCode.RawCode = log.Command.RawCode;

            DB.Insert(dbCode);
        }

        public void BuildLog_Write(long buildId, List<LogInfo> logs)
        {
            foreach (LogInfo log in logs)
                BuildLog_Write(buildId, log);
        }
        #endregion

        #region Write
        public void Debug_Write(string message)
        {
            Console.WriteLine(message);
        }

        public void Debug_Write(LogInfo log)
        {
            for (int i = 0; i < log.Depth; i++)
            {
                Console.Write("  ");
            }
            if (log.Command == null)
                Console.WriteLine($"[{log.State}] {log.Message}");
            else
                Console.WriteLine($"[{log.State}] {log.Message} ({log.Command})");
        }

        public void Debug_Write(List<LogInfo> logs)
        {
            foreach (LogInfo log in logs)
            {
                Debug_Write(log);
            }
        }
        #endregion
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

    #region SQLite Connection 
    #region Model
    public class DB_NormalLog
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
    public class Database : SQLiteConnection
    {
        public Database(string path) : base(new SQLitePlatformWin32(), path)
        {
            CreateTable<DB_NormalLog>();
            CreateTable<DB_Build>();
            CreateTable<DB_Plugin>();
            CreateTable<DB_Variable>();
            CreateTable<DB_CodeLog>();
        }
    }
    #endregion
    #endregion
}
