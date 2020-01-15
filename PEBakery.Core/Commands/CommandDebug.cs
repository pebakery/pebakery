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
    resulting work. An external library is a library which is
    not derived from or based on this program. 
*/

using PEBakery.Core.ViewModels;
using System.Collections.Generic;

namespace PEBakery.Core.Commands
{
    public static class CommandDebug
    {
        public static List<LogInfo> DebugCmd(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();
            CodeInfo_Debug info = cmd.Info.Cast<CodeInfo_Debug>();

            switch (info.Type)
            {
                case DebugType.Breakpoint:
                    {
                        DebugInfo_Breakpoint subInfo = info.SubInfo.Cast<DebugInfo_Breakpoint>();

                        bool pause = true;
                        if (subInfo.Cond != null)
                            pause = CommandBranch.EvalBranchCondition(s, subInfo.Cond, out _);

                        if (pause)
                        {
                            logs.Add(new LogInfo(LogState.Info, "Breakpoint triggered"));

                            // Activate debugger window
                            // DebugViewModel

                            // Wait until user closes debugger window 
                        }
                    }
                    break;
                default: // Error
                    throw new InternalException("Internal Logic Error at CommandDebug");
            }

            return logs;
        }
    }
}
