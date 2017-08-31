/*
    Copyright (C) 2016-2017 Hajin Jang
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
*/

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

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_INIRead));
            CodeInfo_INIRead info = cmd.Info as CodeInfo_INIRead;

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string sectionName = StringEscaper.Preprocess(s, info.SectionName); // WB082 : 여기 값은 변수 Expand 안한다.
            string key = StringEscaper.Preprocess(s, info.Key); // WB082 : 여기 값은 변수 Expand는 안 하나, Escaping은 한다.

            if (sectionName.Equals(string.Empty, StringComparison.Ordinal))
                throw new ExecuteException("Section name cannot be empty");
            if (key.Equals(string.Empty, StringComparison.Ordinal))
                throw new ExecuteException("Key name cannot be empty");

            s.MainViewModel.BuildCommandProgressBarValue = 300;

            string value = Ini.GetKey(fileName, sectionName, key);

            s.MainViewModel.BuildCommandProgressBarValue = 700;

            if (value != null)
            {
                logs.Add(new LogInfo(LogState.Success, $"Key [{key}] and its value [{value}] read from [{fileName}]"));
                List<LogInfo> varLogs = Variables.SetVariable(s, info.VarName, value, true); // WB082 Behavior : put this into global, not local
                logs.AddRange(varLogs);
            }
            else
            {
                logs.Add(new LogInfo(LogState.Error, $"Could not read key [{key}]'s value from [{fileName}]"));
            }

            return logs;
        }

        public static List<LogInfo> INIReadOp(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_INIReadOp));
            CodeInfo_INIReadOp infoOp = cmd.Info as CodeInfo_INIReadOp;

            string fileName = StringEscaper.Preprocess(s, infoOp.Infos[0].FileName);
            s.MainViewModel.BuildCommandProgressBarValue = 100;

            if (StringEscaper.PathSecurityCheck(fileName, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            s.MainViewModel.BuildCommandProgressBarValue = 200;

            IniKey[] keys = new IniKey[infoOp.Cmds.Count];
            for (int i = 0; i < keys.Length; i++)
            {
                CodeInfo_INIRead info = infoOp.Infos[i];

                string sectionName = StringEscaper.Preprocess(s, info.SectionName); // WB082 : 여기 값은 변수 Expand 안한다.
                string key = StringEscaper.Preprocess(s, info.Key); // WB082 : 여기 값은 변수 Expand는 안 하나, Escaping은 한다.

                if (sectionName.Equals(string.Empty, StringComparison.Ordinal))
                    throw new ExecuteException("Section name cannot be empty");
                if (key.Equals(string.Empty, StringComparison.Ordinal))
                    throw new ExecuteException("Key name cannot be empty");

                keys[i] = new IniKey(sectionName, key);
            }

            s.MainViewModel.BuildCommandProgressBarValue = 300;

            keys = Ini.GetKeys(fileName, keys);

            s.MainViewModel.BuildCommandProgressBarValue = 700;

            int successCount = 0;
            for (int i = 0; i < keys.Length; i++)
            {
                IniKey kv = keys[i];
                CodeCommand subCmd = infoOp.Cmds[i];

                if (kv.Value != null)
                {
                    logs.Add(new LogInfo(LogState.Success, $"Key [{kv.Key}] and its value [{kv.Value}] successfully read [{i + 1}/{keys.Length}]", subCmd));
                    List<LogInfo> varLogs = Variables.SetVariable(s, infoOp.Infos[i].VarName, kv.Value, true); // WB082 Behavior : put this into global, not local
                    foreach (LogInfo varLog in varLogs)
                        logs.Add(LogInfo.AddCommand(varLog, subCmd));

                    successCount += 1;
                }
                else
                {
                    logs.Add(new LogInfo(LogState.Error, $"Could not read key [{kv.Key}]'s value [{i + 1}/{keys.Length}]", subCmd));
                }
            }
            logs.Add(new LogInfo(LogState.Success, $"Read [{successCount}] values from [{fileName}]", cmd));

            return logs;
        }
        
        public static List<LogInfo> INIWrite(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_INIWrite));
            CodeInfo_INIWrite info = cmd.Info as CodeInfo_INIWrite;

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string sectionName = StringEscaper.Preprocess(s, info.SectionName); // WB082 : 여기 값은 변수 Expand 안한다.
            string key = StringEscaper.Preprocess(s, info.Key); // WB082 : 여기 값은 변수 Expand는 안 하나, Escaping은 한다.
            string value = StringEscaper.Preprocess(s, info.Value);

            if(sectionName.Equals(string.Empty, StringComparison.Ordinal))
                throw new InvalidCodeCommandException("Section name cannot be empty", cmd);
            if (key.Equals(string.Empty, StringComparison.Ordinal))
                throw new InvalidCodeCommandException("Key name cannot be empty", cmd);

            s.MainViewModel.BuildCommandProgressBarValue = 300;

            if (StringEscaper.PathSecurityCheck(fileName, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            s.MainViewModel.BuildCommandProgressBarValue = 700;

            bool result = Ini.SetKey(fileName, sectionName, key, value);
            if (result)
                logs.Add(new LogInfo(LogState.Success, $"Key [{key}] and its value [{value}] written to [{fileName}]", cmd));
            else
                logs.Add(new LogInfo(LogState.Error, $"Could not write key [{key}] and its value [{value}] to [{fileName}]", cmd));
            return logs;
        }

        public static List<LogInfo> INIWriteOp(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_INIWriteOp));
            CodeInfo_INIWriteOp infoOp = cmd.Info as CodeInfo_INIWriteOp;

            string fileName = StringEscaper.Preprocess(s, infoOp.Infos[0].FileName);
            s.MainViewModel.BuildCommandProgressBarValue = 100;

            if (StringEscaper.PathSecurityCheck(fileName, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            s.MainViewModel.BuildCommandProgressBarValue = 200;

            IniKey[] keys = new IniKey[infoOp.Cmds.Count];
            for (int i = 0; i < keys.Length; i++)
            {
                CodeInfo_INIWrite info = infoOp.Infos[i];

                string sectionName = StringEscaper.Preprocess(s, info.SectionName); // WB082 : 여기 값은 변수 Expand 안한다.
                string key = StringEscaper.Preprocess(s, info.Key); // WB082 : 여기 값은 변수 Expand는 안 하나, Escaping은 한다.
                string value = StringEscaper.Preprocess(s, info.Value);

                if (sectionName.Equals(string.Empty, StringComparison.Ordinal))
                    throw new InvalidCodeCommandException("Section name cannot be empty", cmd);
                if (key.Equals(string.Empty, StringComparison.Ordinal))
                    throw new InvalidCodeCommandException("Key name cannot be empty", cmd);

                keys[i] = new IniKey(sectionName, key, value);
            }

            s.MainViewModel.BuildCommandProgressBarValue = 300;

            bool result = Ini.SetKeys(fileName, keys);

            s.MainViewModel.BuildCommandProgressBarValue = 700;

            if (result)
            {
                for (int i = 0; i < keys.Length; i++)
                {
                    IniKey kv = keys[i];
                    logs.Add(new LogInfo(LogState.Success, $"Key [{kv.Key}] and its value [{kv.Value}] written [{i + 1}/{keys.Length}]", infoOp.Cmds[i]));
                }
                logs.Add(new LogInfo(LogState.Success, $"Wrote [{keys.Length}] values to [{fileName}]", cmd));
            }
            else
            {
                for (int i = 0; i < keys.Length; i++)
                {
                    IniKey kv = keys[i];
                    logs.Add(new LogInfo(LogState.Error, $"Could not write key [{kv.Key}] and its value [{kv.Value}] [{i + 1}/{keys.Length}]", infoOp.Cmds[i]));
                }
                logs.Add(new LogInfo(LogState.Error, $"Could not write [{keys.Length}] values to [{fileName}]", cmd));
            }
            
            return logs;
        }

        public static List<LogInfo> INIDelete(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_INIDelete));
            CodeInfo_INIDelete info = cmd.Info as CodeInfo_INIDelete;

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string sectionName = StringEscaper.Preprocess(s, info.SectionName); // WB082 : 여기 값은 변수 Expand 안한다.
            string key = StringEscaper.Preprocess(s, info.Key); // WB082 : 여기 값은 변수 Expand는 안 하나, Escaping은 한다.

            if (sectionName.Equals(string.Empty, StringComparison.Ordinal))
                throw new InvalidCodeCommandException("Section name cannot be empty", cmd);
            if (key.Equals(string.Empty, StringComparison.Ordinal))
                throw new InvalidCodeCommandException("Key name cannot be empty", cmd);

            s.MainViewModel.BuildCommandProgressBarValue = 300;

            if (StringEscaper.PathSecurityCheck(fileName, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            s.MainViewModel.BuildCommandProgressBarValue = 700;

            bool result = Ini.DeleteKey(fileName, sectionName, key);
            if (result)
                logs.Add(new LogInfo(LogState.Success, $"Key [{key}] deleted from [{fileName}]", cmd));
            else
                logs.Add(new LogInfo(LogState.Error, $"Could not delete key [{key}] from [{fileName}]", cmd));
            return logs;
        }

        public static List<LogInfo> INIDeleteOp(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_INIDeleteOp));
            CodeInfo_INIDeleteOp infoOp = cmd.Info as CodeInfo_INIDeleteOp;

            string fileName = StringEscaper.Preprocess(s, infoOp.Infos[0].FileName);
            s.MainViewModel.BuildCommandProgressBarValue = 100;

            if (StringEscaper.PathSecurityCheck(fileName, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            s.MainViewModel.BuildCommandProgressBarValue = 200;

            IniKey[] keys = new IniKey[infoOp.Cmds.Count];
            for (int i = 0; i < keys.Length; i++)
            {
                CodeInfo_INIDelete info = infoOp.Infos[i];

                string sectionName = StringEscaper.Preprocess(s, info.SectionName); // WB082 : 여기 값은 변수 Expand 안한다.
                string key = StringEscaper.Preprocess(s, info.Key); // WB082 : 여기 값은 변수 Expand는 안 하나, Escaping은 한다.

                if (sectionName.Equals(string.Empty, StringComparison.Ordinal))
                    throw new InvalidCodeCommandException("Section name cannot be empty", cmd);
                if (key.Equals(string.Empty, StringComparison.Ordinal))
                    throw new InvalidCodeCommandException("Key name cannot be empty", cmd);

                keys[i] = new IniKey(sectionName, key);
            }

            s.MainViewModel.BuildCommandProgressBarValue = 300;

            bool result = Ini.DeleteKeys(fileName, keys);

            s.MainViewModel.BuildCommandProgressBarValue = 700;

            if (result)
            {
                for (int i = 0; i < keys.Length; i++)
                {
                    IniKey kv = keys[i];
                    logs.Add(new LogInfo(LogState.Success, $"Key [{kv.Key}] deleted [{i + 1}/{keys.Length}]", infoOp.Cmds[i]));
                }
                logs.Add(new LogInfo(LogState.Success, $"Deleted [{keys.Length}] values from [{fileName}]", cmd));
            }
            else
            {
                for (int i = 0; i < keys.Length; i++)
                {
                    IniKey kv = keys[i];
                    logs.Add(new LogInfo(LogState.Error, $"Could not delete key [{kv.Key}] [{i + 1}/{keys.Length}]", infoOp.Cmds[i]));
                }
                logs.Add(new LogInfo(LogState.Error, $"Could not delete [{keys.Length}] values from [{fileName}]", cmd));
            }

            return logs;
        }

        public static List<LogInfo> INIAddSection(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_INIAddSection));
            CodeInfo_INIAddSection info = cmd.Info as CodeInfo_INIAddSection;

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string sectionName = StringEscaper.Preprocess(s, info.SectionName); // WB082 : 여기 값은 변수 Expand 안한다.

            if (sectionName.Equals(string.Empty, StringComparison.Ordinal))
                throw new InvalidCodeCommandException("Section name cannot be empty", cmd);

            if (StringEscaper.PathSecurityCheck(fileName, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            s.MainViewModel.BuildCommandProgressBarValue = 300;

            bool result = Ini.AddSection(fileName, sectionName);

            s.MainViewModel.BuildCommandProgressBarValue = 700;

            if (result)
                logs.Add(new LogInfo(LogState.Success, $"Section [{sectionName}] added to [{fileName}]", cmd));
            else
                logs.Add(new LogInfo(LogState.Error, $"Could not add section [{sectionName}] to [{fileName}]", cmd));
            return logs;
        }

        public static List<LogInfo> INIAddSectionOp(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_INIAddSectionOp));
            CodeInfo_INIAddSectionOp infoOp = cmd.Info as CodeInfo_INIAddSectionOp;

            string fileName = StringEscaper.Preprocess(s, infoOp.Infos[0].FileName);
            s.MainViewModel.BuildCommandProgressBarValue = 100;

            if (StringEscaper.PathSecurityCheck(fileName, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            s.MainViewModel.BuildCommandProgressBarValue = 200;

            string[] sections = new string[infoOp.Cmds.Count];
            for (int i = 0; i < sections.Length; i++)
            {
                CodeInfo_INIAddSection info = infoOp.Infos[i];

                string sectionName = StringEscaper.Preprocess(s, info.SectionName); // WB082 : 여기 값은 변수 Expand 안한다.
                if (sectionName.Equals(string.Empty, StringComparison.Ordinal))
                    throw new InvalidCodeCommandException("Section name cannot be empty", cmd);

                sections[i] = sectionName;
            }

            s.MainViewModel.BuildCommandProgressBarValue = 300;

            bool result = Ini.AddSections(fileName, sections);

            s.MainViewModel.BuildCommandProgressBarValue = 700;

            if (result)
            {
                for (int i = 0; i < sections.Length; i++)
                    logs.Add(new LogInfo(LogState.Success, $"Section [{sections[i]}] added [{i + 1}/{sections.Length}]", infoOp.Cmds[i]));
                logs.Add(new LogInfo(LogState.Success, $"Added [{sections.Length}] sections to [{fileName}]", cmd));
            }
            else
            {
                for (int i = 0; i < sections.Length; i++)
                    logs.Add(new LogInfo(LogState.Error, $"Could not add section [{sections[i]}] [{i + 1}/{sections.Length}]", infoOp.Cmds[i]));
                logs.Add(new LogInfo(LogState.Error, $"Could not add [{sections.Length}] sections to [{fileName}]", cmd));
            }

            return logs;
        }

        public static List<LogInfo> INIDeleteSection(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_INIDeleteSection));
            CodeInfo_INIDeleteSection info = cmd.Info as CodeInfo_INIDeleteSection;

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string sectionName = StringEscaper.Preprocess(s, info.SectionName); // WB082 : 여기 값은 변수 Expand 안한다.

            if (sectionName.Equals(string.Empty, StringComparison.Ordinal))
                throw new InvalidCodeCommandException("Section name cannot be empty", cmd);

            if (StringEscaper.PathSecurityCheck(fileName, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            s.MainViewModel.BuildCommandProgressBarValue = 300;

            bool result = Ini.DeleteSection(fileName, sectionName);

            s.MainViewModel.BuildCommandProgressBarValue = 700;

            if (result)
                logs.Add(new LogInfo(LogState.Success, $"Section [{sectionName}] deleted from [{fileName}]", cmd));
            else
                logs.Add(new LogInfo(LogState.Error, $"Could not delete section [{sectionName}] from [{fileName}]", cmd));
            return logs;
        }

        public static List<LogInfo> INIDeleteSectionOp(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_INIDeleteSectionOp));
            CodeInfo_INIDeleteSectionOp infoOp = cmd.Info as CodeInfo_INIDeleteSectionOp;

            string fileName = StringEscaper.Preprocess(s, infoOp.Infos[0].FileName);
            s.MainViewModel.BuildCommandProgressBarValue = 100;

            if (StringEscaper.PathSecurityCheck(fileName, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            s.MainViewModel.BuildCommandProgressBarValue = 200;

            string[] sections = new string[infoOp.Cmds.Count];
            for (int i = 0; i < sections.Length; i++)
            {
                CodeInfo_INIDeleteSection info = infoOp.Infos[i];

                string sectionName = StringEscaper.Preprocess(s, info.SectionName); // WB082 : 여기 값은 변수 Expand 안한다.
                if (sectionName.Equals(string.Empty, StringComparison.Ordinal))
                    throw new InvalidCodeCommandException("Section name cannot be empty", cmd);

                sections[i] = sectionName;
            }

            s.MainViewModel.BuildCommandProgressBarValue = 300;

            bool result = Ini.DeleteSections(fileName, sections);

            s.MainViewModel.BuildCommandProgressBarValue = 700;

            if (result)
            {
                for (int i = 0; i < sections.Length; i++)
                    logs.Add(new LogInfo(LogState.Success, $"Section [{sections[i]}] deleted [{i + 1}/{sections.Length}]", infoOp.Cmds[i]));
                logs.Add(new LogInfo(LogState.Success, $"Deleted [{sections.Length}] sections from [{fileName}]", cmd));
            }
            else
            {
                for (int i = 0; i < sections.Length; i++)
                    logs.Add(new LogInfo(LogState.Error, $"Could not delete section [{sections[i]}] [{i + 1}/{sections.Length}]", infoOp.Cmds[i]));
                logs.Add(new LogInfo(LogState.Error, $"Could not delete [{sections.Length}] sections from [{fileName}]", cmd));
            }

            return logs;
        }

        public static List<LogInfo> INIWriteTextLine(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_INIWriteTextLine));
            CodeInfo_INIWriteTextLine info = cmd.Info as CodeInfo_INIWriteTextLine;

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string sectionName = StringEscaper.Preprocess(s, info.SectionName); // WB082 : 여기 값은 변수 Expand 안한다.
            string line = StringEscaper.Preprocess(s, info.Line); 

            if (sectionName.Equals(string.Empty, StringComparison.Ordinal))
                throw new InvalidCodeCommandException("Section name cannot be empty", cmd);

            if (StringEscaper.PathSecurityCheck(fileName, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            s.MainViewModel.BuildCommandProgressBarValue = 300;

            bool result = Ini.WriteRawLine(fileName, sectionName, line, info.Append);

            s.MainViewModel.BuildCommandProgressBarValue = 700;

            if (result)
                logs.Add(new LogInfo(LogState.Success, $"Line [{line}] wrote to [{fileName}]", cmd));
            else
                logs.Add(new LogInfo(LogState.Error, $"Could not write line [{line}] to [{fileName}]", cmd));
            return logs;
        }

        public static List<LogInfo> INIWriteTextLineOp(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_INIWriteTextLineOp));
            CodeInfo_INIWriteTextLineOp infoOp = cmd.Info as CodeInfo_INIWriteTextLineOp;

            string fileName = StringEscaper.Preprocess(s, infoOp.Infos[0].FileName);
            bool append = infoOp.Infos[0].Append;

            s.MainViewModel.BuildCommandProgressBarValue = 100;

            if (StringEscaper.PathSecurityCheck(fileName, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            s.MainViewModel.BuildCommandProgressBarValue = 200;

            IniKey[] keys = new IniKey[infoOp.Cmds.Count];
            for (int i = 0; i < keys.Length; i++)
            {
                CodeInfo_INIWriteTextLine info = infoOp.Infos[i];

                string sectionName = StringEscaper.Preprocess(s, info.SectionName); // WB082 : 여기 값은 변수 Expand 안한다.
                string line = StringEscaper.Preprocess(s, info.Line); // WB082 : 여기 값은 변수 Expand는 안 하나, Escaping은 한다.

                keys[i] = new IniKey(sectionName, line);
            }

            s.MainViewModel.BuildCommandProgressBarValue = 300;

            bool result = Ini.WriteRawLines(fileName, keys, append);

            s.MainViewModel.BuildCommandProgressBarValue = 700;

            if (result)
            {
                for (int i = 0; i < keys.Length; i++)
                {
                    IniKey kv = keys[i];
                    logs.Add(new LogInfo(LogState.Success, $"Line [{kv.Key}] written [{i + 1}/{keys.Length}]", infoOp.Cmds[i]));
                }
                logs.Add(new LogInfo(LogState.Success, $"Wrote [{keys.Length}] lines to [{fileName}]", cmd));
            }
            else
            {
                for (int i = 0; i < keys.Length; i++)
                {
                    IniKey kv = keys[i];
                    logs.Add(new LogInfo(LogState.Error, $"Could not write line [{kv.Key}] [{i + 1}/{keys.Length}]", infoOp.Cmds[i]));
                }
                logs.Add(new LogInfo(LogState.Error, $"Could not write [{keys.Length}] lines to [{fileName}]", cmd));
            }

            return logs;
        }
    }
}
