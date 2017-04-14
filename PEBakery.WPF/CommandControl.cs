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
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_Set info = cmd.Info as CodeInfo_Set;
            if (info == null)
                throw new InvalidCodeCommandException("Command [Set] should have [CodeInfo_Set]", cmd);

            // Determine varKey's type - %A% vs #1
            string varName = info.VarName;
            string varValue = info.VarValue;
            if (varValue.StartsWith("%") && varValue.EndsWith("%")) // %A%
            {
                // Logs are written in variables.SetValue method
                if (info.Global)
                {
                    logs.Add(LogInfo.AddCommand(s.Variables.SetValue(VarsType.Global, varName, varValue), cmd));
                }
                if (info.Permanent)
                {
                    LogInfo log = LogInfo.AddCommand(s.Variables.SetValue(VarsType.Global, varName, varValue), cmd);
                    logs.Add(log);

                    if (log.State == LogState.Success)
                    { // SetValue success, write to IniFile
                        if (Ini.SetKey(s.Project.MainPlugin.FullPath, "Variables", varName, varValue))
                            logs.Add(new LogInfo(LogState.Success, $"Permanent variable [%{varName}%] set to [{varValue}]", cmd));
                        else
                            logs.Add(new LogInfo(LogState.Error, $"Failed to write permanent variable [%{varName}%] and its value [{varValue}] into script.project", cmd));
                    }
                    else
                    { // SetValue failed
                        logs.Add(new LogInfo(LogState.Error, $"Variable [%{varName}%] contains itself in [{varValue}]", cmd));
                    }
                }
                else // Local
                {
                    logs.Add(LogInfo.AddCommand(s.Variables.SetValue(VarsType.Local, varName, varValue), cmd));
                }
            }
            else if (Regex.Match(varValue, @"(#\d+)", RegexOptions.Compiled).Success) // #1
            {
                // List<string> curSecParams = s.SectionParams.Peek();
                List<string> curSecParams = s.CurSectionParams;

                int paramIdx = Variables.GetSectionParamIndex(varValue) - 1; // -1 for (#1 == curSectionParams[0])
                if (paramIdx < 0)
                    throw new InvalidCodeCommandException($"[{varValue}]'s index [{paramIdx + 1}] cannot be negative number", cmd);
                else if (paramIdx < curSecParams.Count)
                    curSecParams[paramIdx] = varValue;
                else
                {
                    for (int i = curSecParams.Count; i < paramIdx; i++)
                        curSecParams.Add("");
                    curSecParams.Add(varValue);
                }
            }
            else
                throw new InvalidCodeCommandException($"Invalid variable name [{varValue}]", cmd);

            return logs;
        }

        public static List<LogInfo> GetParam(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_GetParam info = cmd.Info as CodeInfo_GetParam;
            if (info == null)
                throw new InvalidCodeCommandException("Command [GetParam] should have [CodeInfo_GetParam]", cmd);

            // List<string> curSecParams = s.SectionParams.Peek();
            // logs.Add(LogInfo.AddCommand(s.Variables.SetValue(VarsType.Local, info.VarName, curSecParams[info.Index]), cmd));
            logs.Add(LogInfo.AddCommand(s.Variables.SetValue(VarsType.Local, info.VarName, s.CurSectionParams[info.Index]), cmd));

            return logs;
        }

        public static List<LogInfo> PackParam(EngineState s, CodeCommand cmd)
        { // TODO : Not fully understand WB082's internal mechanism
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_PackParam info = cmd.Info as CodeInfo_PackParam;
            if (info == null)
                throw new InvalidCodeCommandException("Command [PackParam] should have [CodeInfo_PackParam]", cmd);

            logs.Add(new LogInfo(LogState.Ignore, "DEVELOPER NOTE : Not sure how it works.\nIf you know its exact internal mechanism, please report at [https://github.com/ied206/PEBakery/issues]", cmd));

            StringBuilder b = new StringBuilder();
            /*
            List<string> curSecParams = s.SectionParams.Peek();
            for (int i = info.StartIndex; i < curSecParams.Count; i++)
            {
                b.Append("\"");
                b.Append(curSecParams[i]);
                b.Append("\"");
                if (i + 1 < curSecParams.Count)
                    b.Append(",");
            }
            */
            for (int i = info.StartIndex; i < s.CurSectionParams.Count; i++)
            {
                b.Append("\"");
                b.Append(s.CurSectionParams[i]);
                b.Append("\"");
                if (i + 1 < s.CurSectionParams.Count)
                    b.Append(",");
            }

            logs.Add(LogInfo.AddCommand(s.Variables.SetValue(VarsType.Local, info.VarName, b.ToString()), cmd));

            return logs;
        }
    }
}
