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
    resulting work. An external library is a library which is
    not derived from or based on this program. 
*/

using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace PEBakery.Core.Commands
{
    public static class CommandMacro
    {
        public static void Macro(EngineState s, CodeCommand cmd)
        {
            CodeInfo_Macro info = cmd.Info.Cast<CodeInfo_Macro>();

            CodeCommand macroCmd;
            if (s.Macro.MacroDict.ContainsKey(info.MacroType))
            {
                macroCmd = s.Macro.MacroDict[info.MacroType];
                macroCmd.RawCode = cmd.RawCode;
            }
            else if (s.Macro.LocalDict.ContainsKey(info.MacroType))
            {
                macroCmd = s.Macro.LocalDict[info.MacroType];
                macroCmd.RawCode = cmd.RawCode;
            }
            else
            {
                s.Logger.BuildWrite(s, new LogInfo(LogState.Error, $"Invalid Command [{info.MacroType}]", s.CurDepth));
                return;
            }

            Dictionary<int, string> paramDict = new Dictionary<int, string>();
            for (int i = 0; i < info.Args.Count; i++)
                paramDict[i + 1] = StringEscaper.ExpandSectionParams(s, info.Args[i]);

            s.CurSectionParams = paramDict;

            if (s.LogMacro)
            {
                s.InMacro = true;
                CommandBranch.RunExec(s, macroCmd, true);
                s.InMacro = false;
            }
            else // Do not log macro
            {
                s.Logger.BuildWrite(s, new LogInfo(LogState.Info, $"Macro [{info.MacroType}] ({cmd.RawCode})", s.CurDepth + 1));
                s.Logger.TurnOff.Push(true);
                CommandBranch.RunExec(s, macroCmd, true);
                s.Logger.TurnOff.TryPop(out _);
            }
        }
    }
}
