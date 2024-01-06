﻿/*
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
    resulting work. An external library is a library which is
    not derived from or based on this program. 
*/

using System.Collections.Generic;

namespace PEBakery.Core.Commands
{
    public static class CommandMacro
    {
        public static void Macro(EngineState s, CodeCommand cmd)
        {
            CodeInfo_Macro info = (CodeInfo_Macro)cmd.Info;

            CodeCommand macroCmd;
            if (s.Macro.GlobalDict.ContainsKey(info.MacroType))
            {
                macroCmd = s.Macro.GlobalDict[info.MacroType];
            }
            else if (s.Macro.LocalDict.ContainsKey(info.MacroType))
            {
                macroCmd = s.Macro.LocalDict[info.MacroType];
            }
            else
            {
                throw new ExecuteException($"Invalid command [{info.MacroType}]");
            }

            Dictionary<int, string> paramDict = new Dictionary<int, string>();
            for (int i = 0; i < info.Args.Count; i++)
                paramDict[i + 1] = StringEscaper.Preprocess(s, info.Args[i]);

            s.CurSectionInParams = paramDict;
            s.Logger.BuildWrite(s, new LogInfo(LogState.Info, $"Executing command [{info.MacroType}]", cmd, s.PeekDepth));

            if (macroCmd.Type == CodeType.Run || macroCmd.Type == CodeType.RunEx || macroCmd.Type == CodeType.Exec)
            {
                CommandBranch.RunExec(s, macroCmd, new RunExecOptions
                {
                    PreserveCurrentParams = true,
                    IsMacro = true,
                });
            }
            else
            {
                s.PushLocalState(true, s.Logger.BuildRefScriptWrite(s, macroCmd.Section.Script, true));
                Engine.ExecuteCommand(s, macroCmd);
                s.PopLocalState();
            }
        }
    }
}
