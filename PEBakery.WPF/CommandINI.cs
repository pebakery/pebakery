using PEBakery.Exceptions;
using PEBakery.Lib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PEBakery.Core
{
    public static class CommandINI
    {
        public static List<LogInfo> INIRead(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_INIRead info = cmd.Info as CodeInfo_INIRead;
            if (info == null)
                throw new InvalidCodeCommandException("Command [INIRead] should have [CodeInfo_INIRead]", cmd);

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string sectionName = StringEscaper.Preprocess(s, info.SectionName); // WB082 : 여기 값은 변수 Expand 안한다.
            string key = StringEscaper.Preprocess(s, info.Key); // WB082 : 여기 값은 변수 Expand는 안 하나, Escaping은 한다.
            string varName = Variables.GetVariableName(info.VarName);

            if (sectionName.Equals(string.Empty, StringComparison.Ordinal))
                throw new InvalidCodeCommandException("Section name cannot be empty", cmd);
            if (key.Equals(string.Empty, StringComparison.Ordinal))
                throw new InvalidCodeCommandException("Key name cannot be empty", cmd);

            string value = Ini.GetKey(fileName, sectionName, key);
            if (value != null)
            {
                LogInfo log = s.Variables.SetValue(VarsType.Local, varName, value);
                log.Depth = info.Depth;
            }
            logs.Add(new LogInfo(LogState.Success, $"Var [%{varName}%] set to [{value}], read from [{info.FileName}]", cmd));

            return logs;
        }

        public static List<LogInfo> INIWrite(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_INIWrite info = cmd.Info as CodeInfo_INIWrite;
            if (info == null)
                throw new InvalidCodeCommandException("Command [INIRead] should have [CodeInfo_INIRead]", cmd);

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string sectionName = StringEscaper.Preprocess(s, info.SectionName); // WB082 : 여기 값은 변수 Expand 안한다.
            string key = StringEscaper.Preprocess(s, info.Key); // WB082 : 여기 값은 변수 Expand는 안 하나, Escaping은 한다.
            string value = StringEscaper.Preprocess(s, info.Value);

            if(sectionName.Equals(string.Empty, StringComparison.Ordinal))
                throw new InvalidCodeCommandException("Section name cannot be empty", cmd);
            if (key.Equals(string.Empty, StringComparison.Ordinal))
                throw new InvalidCodeCommandException("Key name cannot be empty", cmd);

            bool result = Ini.SetKey(fileName, sectionName, key, value);
            if (result)
                logs.Add(new LogInfo(LogState.Success, $"Key [{key}] and its value [{value}] wrote to [{info.FileName}]", cmd));
            else
                logs.Add(new LogInfo(LogState.Error, $"Could not wrote key [{key}] and its value [{value}] to [{info.FileName}]", cmd));
            return logs;
        }
    }
}
