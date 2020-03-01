/*
   Copyright (C) 2019-2020 Hajin Jang
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

namespace PEBakery.Core
{
    public class ResultReport
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;

        public ResultReport(bool success)
        {
            Success = success;
        }

        public ResultReport(bool success, string message)
        {
            if (message == null)
                message = string.Empty;

            Success = success;
            Message = message;
        }

        public LogInfo ToLogInfo()
        {
            LogState state = Success ? LogState.Success : LogState.Error;
            return new LogInfo(state, Message);
        }
    }

    public class ResultReport<T>
    {
        public bool Success { get; set; }
        public T Result { get; set; }
        public string Message { get; set; } = string.Empty;

        public ResultReport(bool success, T result)
        {
            Success = success;
            Result = result;
        }

        public ResultReport(bool success, T result, string message)
        {
            if (message == null)
                message = string.Empty;

            Success = success;
            Result = result;
            Message = message;
        }

        public LogInfo ToLogInfo()
        {
            LogState state = Success ? LogState.Success : LogState.Error;
            return new LogInfo(state, Message);
        }
    }

    public class ResultReport<T1, T2>
    {
        public bool Success { get; set; }
        public T1 Result1 { get; set; }
        public T2 Result2 { get; set; }
        public string Message { get; set; } = string.Empty;

        public ResultReport(bool success, T1 result1, T2 result2)
        {
            Success = success;
            Result1 = result1;
            Result2 = result2;
        }

        public ResultReport(bool success, T1 result1, T2 result2, string message)
        {
            if (message == null)
                message = string.Empty;

            Success = success;
            Result1 = result1;
            Result2 = result2;
            Message = message;
        }

        public LogInfo ToLogInfo()
        {
            LogState state = Success ? LogState.Success : LogState.Error;
            return new LogInfo(state, Message);
        }
    }
}
