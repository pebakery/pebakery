using PEBakery.Exceptions;
using PEBakery.Lib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PEBakery.Core
{
    public static class CommandControl
    {
        public static List<LogInfo> Set(EngineState s, CodeCommand cmd)
        {
            CodeInfo_Set info = cmd.Info as CodeInfo_Set;
            if (info == null)
                throw new InvalidCodeCommandException("Command [Set] should have [CodeInfo_Set]", cmd);

            List<LogInfo> logs = Variables.SetVariable(s, info.VarKey, info.VarValue, info.Global, info.Permanent);
            logs.AddRange(LogInfo.AddCommand(logs, cmd));

            return logs;
        }

        public static List<LogInfo> GetParam(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_GetParam info = cmd.Info as CodeInfo_GetParam;
            if (info == null)
                throw new InvalidCodeCommandException("Command [GetParam] should have [CodeInfo_GetParam]", cmd);

            logs.Add(LogInfo.AddCommand(s.Variables.SetValue(VarsType.Local, info.VarName, s.CurSectionParams[info.Index]), cmd));

            return logs;
        }

        public static List<LogInfo> PackParam(EngineState s, CodeCommand cmd)
        { // TODO : Not fully understand WB082's internal mechanism
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_PackParam info = cmd.Info as CodeInfo_PackParam;
            if (info == null)
                throw new InvalidCodeCommandException("Command [PackParam] should have [CodeInfo_PackParam]", cmd);

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
                catch (KeyNotFoundException)
                {
                }

                if (i + 1 < s.CurSectionParams.Count)
                    b.Append(",");
            }

            logs.Add(LogInfo.AddCommand(s.Variables.SetValue(VarsType.Local, info.VarName, b.ToString()), cmd));

            return logs;
        }
    }
}
