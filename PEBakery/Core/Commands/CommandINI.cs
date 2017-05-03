using PEBakery.Exceptions;
using PEBakery.Lib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PEBakery.Core.Commands
{
    public static class CommandINI
    {
        public static List<LogInfo> INIRead(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Trace.Assert(cmd.Info.GetType() == typeof(CodeInfo_INIRead));
            CodeInfo_INIRead info = cmd.Info as CodeInfo_INIRead;

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string sectionName = StringEscaper.Preprocess(s, info.SectionName); // WB082 : 여기 값은 변수 Expand 안한다.
            string key = StringEscaper.Preprocess(s, info.Key); // WB082 : 여기 값은 변수 Expand는 안 하나, Escaping은 한다.

            if (sectionName.Equals(string.Empty, StringComparison.Ordinal))
                throw new InvalidCodeCommandException("Section name cannot be empty", cmd);
            if (key.Equals(string.Empty, StringComparison.Ordinal))
                throw new InvalidCodeCommandException("Key name cannot be empty", cmd);

            string value = Ini.GetKey(fileName, sectionName, key);
            if (value != null)
            {
                List<LogInfo> varLogs = Variables.SetVariable(s, info.VarName, value);
                foreach (LogInfo log in varLogs)
                {
                    LogInfo.AddCommand(log, cmd);
                    logs.Add(log);
                }
            }
            logs.Add(new LogInfo(LogState.Success, $"Var [{info.VarName}] set to [{value}], read from [{fileName}]", cmd));

            return logs;
        }

        public static List<LogInfo> INIWrite(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Trace.Assert(cmd.Info.GetType() == typeof(CodeInfo_INIWrite));
            CodeInfo_INIWrite info = cmd.Info as CodeInfo_INIWrite;

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
                logs.Add(new LogInfo(LogState.Success, $"Key [{key}] and its value [{value}] wrote to [{fileName}]", cmd));
            else
                logs.Add(new LogInfo(LogState.Error, $"Could not wrote key [{key}] and its value [{value}] to [{fileName}]", cmd));
            return logs;
        }

        public static List<LogInfo> INIDelete(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Trace.Assert(cmd.Info.GetType() == typeof(CodeInfo_INIDelete));
            CodeInfo_INIDelete info = cmd.Info as CodeInfo_INIDelete;

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string sectionName = StringEscaper.Preprocess(s, info.SectionName); // WB082 : 여기 값은 변수 Expand 안한다.
            string key = StringEscaper.Preprocess(s, info.Key); // WB082 : 여기 값은 변수 Expand는 안 하나, Escaping은 한다.

            if (sectionName.Equals(string.Empty, StringComparison.Ordinal))
                throw new InvalidCodeCommandException("Section name cannot be empty", cmd);
            if (key.Equals(string.Empty, StringComparison.Ordinal))
                throw new InvalidCodeCommandException("Key name cannot be empty", cmd);

            bool result = Ini.DeleteKey(fileName, sectionName, key);
            if (result)
                logs.Add(new LogInfo(LogState.Success, $"Key [{key}] deleted from [{fileName}]", cmd));
            else
                logs.Add(new LogInfo(LogState.Error, $"Could not delete key [{key}] from [{fileName}]", cmd));
            return logs;
        }

        public static List<LogInfo> INIAddSection(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Trace.Assert(cmd.Info.GetType() == typeof(CodeInfo_INIAddSection));
            CodeInfo_INIAddSection info = cmd.Info as CodeInfo_INIAddSection;

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string sectionName = StringEscaper.Preprocess(s, info.SectionName); // WB082 : 여기 값은 변수 Expand 안한다.

            if (sectionName.Equals(string.Empty, StringComparison.Ordinal))
                throw new InvalidCodeCommandException("Section name cannot be empty", cmd);

            bool result = Ini.AddSection(fileName, sectionName);
            if (result)
                logs.Add(new LogInfo(LogState.Success, $"Section [{sectionName}] added to [{fileName}]", cmd));
            else
                logs.Add(new LogInfo(LogState.Error, $"Could not add section [{sectionName}] to [{fileName}]", cmd));
            return logs;
        }

        public static List<LogInfo> INIDeleteSection(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Trace.Assert(cmd.Info.GetType() == typeof(CodeInfo_INIDeleteSection));
            CodeInfo_INIDeleteSection info = cmd.Info as CodeInfo_INIDeleteSection;

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string sectionName = StringEscaper.Preprocess(s, info.SectionName); // WB082 : 여기 값은 변수 Expand 안한다.

            if (sectionName.Equals(string.Empty, StringComparison.Ordinal))
                throw new InvalidCodeCommandException("Section name cannot be empty", cmd);

            bool result = Ini.DeleteSection(fileName, sectionName);
            if (result)
                logs.Add(new LogInfo(LogState.Success, $"Section [{sectionName}] deleted from [{fileName}]", cmd));
            else
                logs.Add(new LogInfo(LogState.Error, $"Could not delete section [{sectionName}] from [{fileName}]", cmd));
            return logs;
        }
    }
}
