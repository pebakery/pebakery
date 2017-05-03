using PEBakery.Exceptions;
using PEBakery.Lib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PEBakery.Core.Commands
{
    public static class CommandControl
    {
        public static List<LogInfo> Set(EngineState s, CodeCommand cmd)
        {
            Trace.Assert(cmd.Info.GetType() == typeof(CodeInfo_Set));
            CodeInfo_Set info = cmd.Info as CodeInfo_Set;

            List<LogInfo> logs = Variables.SetVariable(s, info.VarKey, info.VarValue, info.Global, info.Permanent);

            return logs;
        }

        public static List<LogInfo> GetParam(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Trace.Assert(cmd.Info.GetType() == typeof(CodeInfo_GetParam));
            CodeInfo_GetParam info = cmd.Info as CodeInfo_GetParam;

            logs.Add(s.Variables.SetValue(VarsType.Local, info.VarName, s.CurSectionParams[info.Index]));

            return logs;
        }

        public static List<LogInfo> PackParam(EngineState s, CodeCommand cmd)
        { // TODO : Not fully understand WB082's internal mechanism
            List<LogInfo> logs = new List<LogInfo>();

            Trace.Assert(cmd.Info.GetType() == typeof(CodeInfo_PackParam));
            CodeInfo_PackParam info = cmd.Info as CodeInfo_PackParam;

            logs.Add(new LogInfo(LogState.Ignore,
                "DEVELOPER NOTE : Not sure how it works.\nIf you know its exact internal mechanism, please report at [https://github.com/ied206/PEBakery/issues]", cmd));

            StringBuilder b = new StringBuilder();
            for (int i = info.StartIndex; i < s.CurSectionParams.Keys.Max(); i++)
            {
                try
                {
                    string value = s.CurSectionParams[i];
                    b.Append("\"");
                    b.Append(value);
                    b.Append("\"");
                }
                catch (KeyNotFoundException) { }

                if (i + 1 < s.CurSectionParams.Count)
                    b.Append(",");
            }

            logs.Add(s.Variables.SetValue(VarsType.Local, info.VarName, b.ToString()));

            return logs;
        }
    }
}
