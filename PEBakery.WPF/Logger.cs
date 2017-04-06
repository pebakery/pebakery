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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PEBakery.Core
{
    #region LogState, LogInfo, DetailLog
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
/*
    public struct LogInfo
    {
        public LogState State;
        public string Message;

        public LogInfo(LogState state, string message)
        {
            State = state;
            Message = message;
        }

        public LogInfo(LogState state, Exception e)
        {
            State = state;
            Message = Logger.LogExceptionMessage(e);
        }
    }
    */
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
            info.Depth = command.Info.Depth;
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
        public int ErrorOffCount;
        public bool SuspendLog;

        public Logger(string path)
        {
            DB = new Database(path);
            ErrorOffCount = 0;
            SuspendLog = false;
        }

        ~Logger()
        {
            DB.Close();
        }

        public void Write(string message)
        { // TODO
            
        }

        public void Write(LogInfo log)
        { // TODO

        }

        public void Write(List<LogInfo> logs)
        { // TODO

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

    #region SQLite Connection 
    #region Model
    public class DB_NormalLog
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
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
        public int Id { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        [MaxLength(256)]
        public string ProjectName { get; set; }

        [OneToMany(CascadeOperations = CascadeOperation.All)] 
        public List<DB_Plugin> Plugins { get; set; }
        [OneToMany(CascadeOperations = CascadeOperation.All)] 
        public List<DB_Variable> Variables { get; set; }
        [OneToMany(CascadeOperations = CascadeOperation.All)]
        public List<DB_CodeLog> Logs { get; set; }

        public override string ToString()
        {
            return $"{Id} = {ProjectName}";
        }
    }

    public class DB_Plugin
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        [ForeignKey(typeof(DB_Build))]
        public int BuildId { get; set; }
        public int Level { get; set; }
        [MaxLength(256)]
        public string Name { get; set; }
        [MaxLength(32767)] // https://msdn.microsoft.com/library/windows/desktop/aa365247.aspx#maxpath
        public string Path { get; set; }
        public int Version { get; set; }
        public int ElapsedMilliSec { get; set; }

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
        public int Id { get; set; }
        [ForeignKey(typeof(DB_Build))]
        public int BuildId { get; set; }
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
        public int Id { get; set; }
        public DateTime Time { get; set; }
        [ForeignKey(typeof(DB_Build))]
        public int BuildId { get; set; }
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
