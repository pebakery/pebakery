/*
   Copyright (C) 2016-2019 Hajin Jang
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

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

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

    #region LogInfo
    [Serializable]
    public class LogInfo
    {
        #region Fields
        public LogState State;
        public string Message;
        public CodeCommand Command;
        // ReSharper disable once InconsistentNaming
        public UIControl UIControl;
        public bool IsException;
        public int Depth;
        #endregion

        #region Constructor - LogState, Message
        public LogInfo(LogState state, string message)
        {
            State = state;
            Message = message;
            Command = null;
            UIControl = null;
            IsException = false;
            Depth = 0;
        }

        public LogInfo(LogState state, string message, CodeCommand command)
        {
            State = state;
            Message = message;
            Command = command;
            UIControl = null;
            IsException = false;
            Depth = 0;
        }

        public LogInfo(LogState state, string message, int depth)
        {
            State = state;
            Message = message;
            Command = null;
            UIControl = null;
            IsException = false;
            Depth = depth;
        }

        public LogInfo(LogState state, string message, CodeCommand command, int depth)
        {
            State = state;
            Message = message;
            Command = command;
            UIControl = null;
            IsException = false;
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
            IsException = true;
            Depth = 0;
        }

        public LogInfo(LogState state, Exception e, CodeCommand command)
        {
            State = state;
            Message = Logger.LogExceptionMessage(e);
            Command = command;
            UIControl = null;
            IsException = true;
            Depth = 0;
        }

        public LogInfo(LogState state, Exception e, int depth)
        {
            State = state;
            Message = Logger.LogExceptionMessage(e);
            Command = null;
            UIControl = null;
            IsException = true;
            Depth = depth;
        }

        public LogInfo(LogState state, Exception e, CodeCommand command, int depth)
        {
            State = state;
            Message = Logger.LogExceptionMessage(e);
            Command = command;
            UIControl = null;
            IsException = true;
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
            IsException = false;
            Depth = 0;
        }

        public LogInfo(LogState state, Exception e, UIControl uiCtrl)
        {
            State = state;
            Message = Logger.LogExceptionMessage(e);
            Command = null;
            UIControl = uiCtrl;
            IsException = false;
            Depth = 0;
        }
        #endregion

        #region Constructor - LogModel.BuildLog
        public LogInfo(LogModel.BuildLog buildLog)
        {
            State = buildLog.State;
            Message = buildLog.Message;
            Command = null;
            UIControl = null;
            IsException = false;
            Depth = buildLog.Depth;
        }
        #endregion

        #region AddCommand, AddDepth
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LogInfo AddCommand(LogInfo log, CodeCommand cmd)
        {
            if (log.Command == null)
                log.Command = cmd;
            return log;
        }

        public static List<LogInfo> AddCommand(List<LogInfo> logs, CodeCommand cmd)
        {
            foreach (LogInfo log in logs)
                AddCommand(log, cmd);
            return logs;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LogInfo AddDepth(LogInfo log, int depth)
        {
            log.Depth = depth;
            return log;
        }

        public static List<LogInfo> AddDepth(List<LogInfo> logs, int depth)
        {
            foreach (LogInfo log in logs)
                AddDepth(log, depth);

            return logs;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LogInfo AddCommandDepth(LogInfo log, CodeCommand cmd, int depth)
        {
            if (log.Command == null)
                log.Command = cmd;
            log.Depth = depth;
            return log;
        }

        public static List<LogInfo> AddCommandDepth(List<LogInfo> logs, CodeCommand cmd, int depth)
        {
            foreach (LogInfo log in logs)
                AddCommandDepth(log, cmd, depth);

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
}
