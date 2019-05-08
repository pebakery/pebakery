using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PEBakery.Helper;

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

                            // TODO: (Before v1.0) Activate debugger window
                        }
                    }
                    break;
            }

            return logs;
        }
    }
}
