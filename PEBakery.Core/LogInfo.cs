/*
   Copyright (C) 2016-2023 Hajin Jang
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
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace PEBakery.Core
{
    #region enum LogState
    public enum LogState
    {
        None = 0,
        Success = 100,
        /// <summary>
        /// Denotes ignorable errors.
        /// </summary>
        Warning = 200,
        /// <summary>
        /// Similar to <see cref="LogState.Warning"/>, but caused by overwriting files.
        /// </summary>
        Overwrite = 201,
        /// <summary>
        /// Fatal error that build stop is recommended.
        /// </summary>
        Error = 300,
        /// <summary>
        /// Hidden to users. Denote internal PEBakery error. Must not happen.
        /// </summary>
        CriticalError = 301,
        /// <summary>
        /// Normal informational log.
        /// </summary>
        Info = 400,
        /// <summary>
        /// Warnings that had been muted by [NoWarn] or etc.
        /// </summary>
        Ignore = 401,
        /// <summary>
        /// Errors that had been muted by System.ErrorOff.
        /// </summary>
        Muted = 402,
        /// <summary>
        /// Hidden to users, only used for development purpose.
        /// </summary>
        Debug = 403,
    }
    #endregion

    #region LogInfo
    [Serializable]
    public class LogInfo
    {
        #region Fields
        public LogState State { get; set; }
        public string Message { get; set; }
        public CodeCommand? Command { get; set; }
        public UIControl? UIControl { get; set; }
        public bool IsException { get; set; }
        public int Depth { get; set; }
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
            log.Command ??= cmd;
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
            log.Command ??= cmd;
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
        public static List<LogInfo> LogErrorMessage(List<LogInfo> logs, string msg)
        {
            logs.Add(new LogInfo(LogState.Error, msg));
            return logs;
        }

        /// <summary>
        /// Wrapper for one-line critical error terminate
        /// </summary>
        public static List<LogInfo> LogCriticalErrorMessage(List<LogInfo> logs, string msg)
        {
            logs.Add(new LogInfo(LogState.CriticalError, msg));
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

        #region (static) LogState Row Background Color
        /// <summary>
        /// Defines background color value of LogState row in LogViewer.
        /// </summary>
        private readonly static Dictionary<LogState, Color> LogStateBackgroundColorDict = new Dictionary<LogState, Color>()
        {
            [LogState.Success] = Color.FromRgb(212, 237, 218),
            [LogState.Warning] = Color.FromRgb(255, 238, 186),
            [LogState.Overwrite] = Color.FromRgb(250, 226, 202),
            [LogState.Error] = Color.FromRgb(248, 215, 218),
            [LogState.CriticalError] = Color.FromRgb(255, 190, 190),
            [LogState.Info] = Color.FromRgb(204, 229, 255),
            [LogState.Ignore] = Color.FromRgb(226, 227, 229),
            [LogState.Muted] = Color.FromRgb(214, 216, 217),
            [LogState.Debug] = Color.FromRgb(230, 204, 255),
        };

        /// <summary>
        /// Query background color value of LogState (for stat).
        /// </summary>
        public static Color? QueryStatBackgroundColor(LogState state)
        {
            if (LogStateBackgroundColorDict.ContainsKey(state))
                return LogStateBackgroundColorDict[state];
            else
                return null;
        }

        /// <summary>
        /// Query background color value of LogState (for message row).
        /// </summary>
        public static Color? QueryRowBackgroundColor(LogState state)
        {
            switch (state)
            {
                case LogState.Error:
                case LogState.CriticalError:
                case LogState.Warning:
                case LogState.Debug:
                    Debug.Assert(LogStateBackgroundColorDict.ContainsKey(state), $"{nameof(LogStateBackgroundColorDict)} does not have LogState [{state}].");
                    return LogStateBackgroundColorDict[state];
                default:
                    return null;
            }
        }
        #endregion
    }
    #endregion
}
