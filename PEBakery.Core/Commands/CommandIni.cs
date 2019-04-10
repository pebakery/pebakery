/*
    Copyright (C) 2016-2019 Hajin Jang
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

using PEBakery.Ini;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace PEBakery.Core.Commands
{
    public static class CommandIni
    {
        public static List<LogInfo> IniRead(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_IniRead info = cmd.Info.Cast<CodeInfo_IniRead>();

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string sectionName = StringEscaper.Preprocess(s, info.Section);
            string key = StringEscaper.Preprocess(s, info.Key);

            Debug.Assert(fileName != null, $"{nameof(fileName)} != null");
            Debug.Assert(sectionName != null, $"{nameof(sectionName)} != null");
            Debug.Assert(key != null, $"{nameof(key)} != null");

            if (sectionName.Length == 0)
                return LogInfo.LogErrorMessage(logs, "Section name cannot be empty");
            if (key.Length == 0)
                return LogInfo.LogErrorMessage(logs, "Key name cannot be empty");

            string value = IniReadWriter.ReadKey(fileName, sectionName, key);
            if (value != null)
            {
                logs.Add(new LogInfo(LogState.Success, $"Key [{key}] and it's value [{value}] read from [{fileName}]"));

                string escapedValue = StringEscaper.Escape(value, false, true);
                List<LogInfo> varLogs = Variables.SetVariable(s, info.DestVar, escapedValue, false, false, false);
                logs.AddRange(varLogs);
            }
            else
            {
                logs.Add(new LogInfo(LogState.Ignore, $"Key [{key}] does not exist in [{fileName}]"));

                List<LogInfo> varLogs = Variables.SetVariable(s, info.DestVar, string.Empty, false, false, false);
                logs.AddRange(varLogs);
            }

            return logs;
        }

        public static List<LogInfo> IniReadOp(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_IniReadOp infoOp = cmd.Info.Cast<CodeInfo_IniReadOp>();

            string fileName = StringEscaper.Preprocess(s, infoOp.Infos[0].FileName);
            Debug.Assert(fileName != null, $"{nameof(fileName)} != null");

            if (!StringEscaper.PathSecurityCheck(fileName, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            IniKey[] keys = new IniKey[infoOp.Cmds.Count];
            for (int i = 0; i < keys.Length; i++)
            {
                CodeInfo_IniRead info = infoOp.Infos[i];

                string sectionName = StringEscaper.Preprocess(s, info.Section);
                string key = StringEscaper.Preprocess(s, info.Key);

                if (sectionName.Length == 0)
                    return LogInfo.LogErrorMessage(logs, "Section name cannot be empty");
                if (key.Length == 0)
                    return LogInfo.LogErrorMessage(logs, "Key name cannot be empty");

                keys[i] = new IniKey(sectionName, key);
            }

            keys = IniReadWriter.ReadKeys(fileName, keys);

            int successCount = 0;
            for (int i = 0; i < keys.Length; i++)
            {
                IniKey kv = keys[i];
                CodeCommand subCmd = infoOp.Cmds[i];

                if (kv.Value != null)
                {
                    logs.Add(new LogInfo(LogState.Success, $"Key [{kv.Key}] and it's value [{kv.Value}] successfully read", subCmd));

                    string escapedValue = StringEscaper.Escape(kv.Value, false, true);
                    List<LogInfo> varLogs = Variables.SetVariable(s, infoOp.Infos[i].DestVar, escapedValue, false, false, false);
                    LogInfo.AddCommand(varLogs, subCmd);
                    logs.AddRange(varLogs);

                    successCount += 1;
                }
                else
                {
                    logs.Add(new LogInfo(LogState.Ignore, $"Key [{kv.Key}] does not exist", subCmd));

                    List<LogInfo> varLogs = Variables.SetVariable(s, infoOp.Infos[i].DestVar, string.Empty, false, false, false);
                    logs.AddRange(varLogs);
                }
            }
            logs.Add(new LogInfo(LogState.Success, $"Read [{successCount}] values from [{fileName}]", cmd));

            return logs;
        }

        public static List<LogInfo> IniWrite(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_IniWrite info = cmd.Info.Cast<CodeInfo_IniWrite>();

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string sectionName = StringEscaper.Preprocess(s, info.Section);
            string key = StringEscaper.Preprocess(s, info.Key);
            string value = StringEscaper.Preprocess(s, info.Value);

            Debug.Assert(fileName != null, $"{nameof(fileName)} != null");
            Debug.Assert(sectionName != null, $"{nameof(sectionName)} != null");
            Debug.Assert(key != null, $"{nameof(key)} != null");
            Debug.Assert(value != null, $"{nameof(value)} != null");

            if (sectionName.Length == 0)
                return LogInfo.LogErrorMessage(logs, "Section name cannot be empty");
            if (key.Length == 0)
                return LogInfo.LogErrorMessage(logs, "Key name cannot be empty");

            if (!StringEscaper.PathSecurityCheck(fileName, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            string dirPath = Path.GetDirectoryName(fileName);
            if (dirPath == null)
                throw new InternalException("Internal Logic Error at IniWrite");
            if (!Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);

            bool result = IniReadWriter.WriteKey(fileName, sectionName, key, value);
            if (result)
                logs.Add(new LogInfo(LogState.Success, $"Key [{key}] and it's value [{value}] written to [{fileName}]", cmd));
            else
                logs.Add(new LogInfo(LogState.Error, $"Could not write key [{key}] and it's value [{value}] to [{fileName}]", cmd));

            return logs;
        }

        public static List<LogInfo> IniWriteOp(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_IniWriteOp infoOp = cmd.Info.Cast<CodeInfo_IniWriteOp>();

            string fileName = StringEscaper.Preprocess(s, infoOp.Infos[0].FileName);
            Debug.Assert(fileName != null, $"{nameof(fileName)} != null");

            if (!StringEscaper.PathSecurityCheck(fileName, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            IniKey[] keys = new IniKey[infoOp.Cmds.Count];
            for (int i = 0; i < keys.Length; i++)
            {
                CodeInfo_IniWrite info = infoOp.Infos[i];

                string sectionName = StringEscaper.Preprocess(s, info.Section);
                string key = StringEscaper.Preprocess(s, info.Key);
                string value = StringEscaper.Preprocess(s, info.Value);

                if (sectionName.Length == 0)
                    return LogInfo.LogErrorMessage(logs, "Section name cannot be empty");
                if (key.Length == 0)
                    return LogInfo.LogErrorMessage(logs, "Key name cannot be empty");

                keys[i] = new IniKey(sectionName, key, value);
            }

            string dirPath = Path.GetDirectoryName(fileName);
            if (dirPath == null)
                throw new InternalException("Internal Logic Error at IniWriteOp");
            if (!Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);

            bool result = IniReadWriter.WriteKeys(fileName, keys);

            if (result)
            {
                for (int i = 0; i < keys.Length; i++)
                {
                    IniKey kv = keys[i];
                    logs.Add(new LogInfo(LogState.Success, $"Key [{kv.Key}] and it's value [{kv.Value}] written", infoOp.Cmds[i]));
                }
                logs.Add(new LogInfo(LogState.Success, $"Wrote [{keys.Length}] values to [{fileName}]", cmd));
            }
            else
            {
                for (int i = 0; i < keys.Length; i++)
                {
                    IniKey kv = keys[i];
                    logs.Add(new LogInfo(LogState.Error, $"Could not write key [{kv.Key}] and it's value [{kv.Value}]", infoOp.Cmds[i]));
                }
                logs.Add(new LogInfo(LogState.Error, $"Could not write [{keys.Length}] values to [{fileName}]", cmd));
            }

            return logs;
        }

        public static List<LogInfo> IniDelete(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_IniDelete info = cmd.Info.Cast<CodeInfo_IniDelete>();

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string sectionName = StringEscaper.Preprocess(s, info.Section);
            string key = StringEscaper.Preprocess(s, info.Key);

            Debug.Assert(fileName != null, $"{nameof(fileName)} != null");
            Debug.Assert(sectionName != null, $"{nameof(sectionName)} != null");
            Debug.Assert(key != null, $"{nameof(key)} != null");

            if (sectionName.Length == 0)
                return LogInfo.LogErrorMessage(logs, "Section name cannot be empty");
            if (key.Length == 0)
                return LogInfo.LogErrorMessage(logs, "Key name cannot be empty");

            if (!StringEscaper.PathSecurityCheck(fileName, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            bool result = IniReadWriter.DeleteKey(fileName, sectionName, key);
            if (result)
                logs.Add(new LogInfo(LogState.Success, $"Key [{key}] deleted from [{fileName}]", cmd));
            else
                logs.Add(new LogInfo(LogState.Ignore, $"Could not delete key [{key}] from [{fileName}]", cmd));

            return logs;
        }

        public static List<LogInfo> IniDeleteOp(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_IniDeleteOp infoOp = cmd.Info.Cast<CodeInfo_IniDeleteOp>();

            string fileName = StringEscaper.Preprocess(s, infoOp.Infos[0].FileName);

            Debug.Assert(fileName != null, $"{nameof(fileName)} != null");

            if (!StringEscaper.PathSecurityCheck(fileName, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            IniKey[] keys = new IniKey[infoOp.Cmds.Count];
            for (int i = 0; i < keys.Length; i++)
            {
                CodeInfo_IniDelete info = infoOp.Infos[i];

                string sectionName = StringEscaper.Preprocess(s, info.Section);
                string key = StringEscaper.Preprocess(s, info.Key);

                if (sectionName.Length == 0)
                    return LogInfo.LogErrorMessage(logs, "Section name cannot be empty");
                if (key.Length == 0)
                    return LogInfo.LogErrorMessage(logs, "Key name cannot be empty");

                keys[i] = new IniKey(sectionName, key);
            }

            bool[] result = IniReadWriter.DeleteKeys(fileName, keys);

            int successCount = 0;
            for (int i = 0; i < keys.Length; i++)
            {
                IniKey kv = keys[i];
                if (result[i])
                {
                    successCount += 1;
                    logs.Add(new LogInfo(LogState.Success, $"Key [{kv.Key}] deleted", infoOp.Cmds[i]));
                }
                else
                {
                    logs.Add(new LogInfo(LogState.Ignore, $"Could not delete key [{kv.Key}]", infoOp.Cmds[i]));
                }
            }

            if (0 < successCount)
                logs.Add(new LogInfo(LogState.Success, $"Deleted [{keys.Length}] values from [{fileName}]", cmd));
            if (0 < keys.Length - successCount)
                logs.Add(new LogInfo(LogState.Ignore, $"Could not delete [{keys.Length}] values from [{fileName}]", cmd));

            return logs;
        }

        public static List<LogInfo> IniReadSection(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_IniReadSection info = cmd.Info.Cast<CodeInfo_IniReadSection>();

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string section = StringEscaper.Preprocess(s, info.Section);
            string delim = "|";
            if (info.Delim != null)
                delim = StringEscaper.Preprocess(s, info.Delim);

            Debug.Assert(fileName != null, $"{nameof(fileName)} != null");
            Debug.Assert(section != null, $"{nameof(section)} != null");

            if (section.Length == 0)
                return LogInfo.LogErrorMessage(logs, "Section name cannot be empty");

            IniKey[] keys = IniReadWriter.ReadSection(fileName, section);
            if (keys != null)
            {
                List<string> kvList = new List<string>(keys.Length * 2);
                foreach (IniKey k in keys)
                {
                    kvList.Add(k.Key);
                    kvList.Add(k.Value);
                }
                string destStr = StringEscaper.PackListStr(kvList, delim);

                logs.Add(new LogInfo(LogState.Success, $"Section [{section}] read from [{fileName}]"));

                string escapedValue = StringEscaper.Escape(destStr, false, true);
                List<LogInfo> varLogs = Variables.SetVariable(s, info.DestVar, escapedValue, false, false, false);
                logs.AddRange(varLogs);
            }
            else
            {
                logs.Add(new LogInfo(LogState.Ignore, $"Section [{section}] does not exist in [{fileName}]"));

                List<LogInfo> varLogs = Variables.SetVariable(s, info.DestVar, string.Empty, false, false, false);
                logs.AddRange(varLogs);
            }

            return logs;
        }

        public static List<LogInfo> IniReadSectionOp(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_IniReadSectionOp infoOp = cmd.Info.Cast<CodeInfo_IniReadSectionOp>();

            string fileName = StringEscaper.Preprocess(s, infoOp.Infos[0].FileName);

            Debug.Assert(fileName != null, $"{nameof(fileName)} != null");

            string[] sections = new string[infoOp.Cmds.Count];
            string[] destVars = new string[infoOp.Cmds.Count];
            string[] delims = new string[infoOp.Cmds.Count];
            for (int i = 0; i < sections.Length; i++)
            {
                CodeInfo_IniReadSection info = infoOp.Infos[i];

                string section = StringEscaper.Preprocess(s, info.Section);
                if (section.Length == 0)
                    return LogInfo.LogErrorMessage(logs, "Section name cannot be empty");

                sections[i] = section;
                destVars[i] = info.DestVar;
                delims[i] = "|";
                if (info.Delim != null)
                    delims[i] = StringEscaper.Preprocess(s, info.Delim);
            }

            Dictionary<string, IniKey[]> keyDict = IniReadWriter.ReadSections(fileName, sections);

            int successCount = 0;
            for (int i = 0; i < sections.Length; i++)
            {
                string section = sections[i];
                string delim = delims[i];
                IniKey[] keys = keyDict[section];
                CodeCommand subCmd = infoOp.Cmds[i];

                if (keys != null)
                {
                    List<string> kvList = new List<string>(keys.Length * 2);
                    foreach (IniKey k in keys)
                    {
                        kvList.Add(k.Key);
                        kvList.Add(k.Value);
                    }
                    string destStr = StringEscaper.PackListStr(kvList, delim);
                    logs.Add(new LogInfo(LogState.Success, $"Section [{section}] read", subCmd));

                    string escapedValue = StringEscaper.Escape(destStr, false, true);
                    List<LogInfo> varLogs = Variables.SetVariable(s, destVars[i], escapedValue, false, false, false);
                    LogInfo.AddCommand(varLogs, subCmd);
                    logs.AddRange(varLogs);
                }
                else
                {
                    logs.Add(new LogInfo(LogState.Ignore, $"Section [{section}] does not exist", subCmd));

                    List<LogInfo> varLogs = Variables.SetVariable(s, destVars[i], string.Empty, false, false, false);
                    LogInfo.AddCommand(varLogs, subCmd);
                    logs.AddRange(varLogs);
                }
            }
            logs.Add(new LogInfo(LogState.Success, $"Read [{successCount}] sections from [{fileName}]", cmd));

            return logs;
        }

        public static List<LogInfo> IniAddSection(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_IniAddSection info = cmd.Info.Cast<CodeInfo_IniAddSection>();

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string section = StringEscaper.Preprocess(s, info.Section);

            Debug.Assert(fileName != null, $"{nameof(fileName)} != null");
            Debug.Assert(section != null, $"{nameof(section)} != null");

            if (section.Length == 0)
                return LogInfo.LogErrorMessage(logs, "Section name cannot be empty");

            if (!StringEscaper.PathSecurityCheck(fileName, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            string dirPath = Path.GetDirectoryName(fileName);
            if (dirPath == null)
                throw new InternalException("Internal Logic Error at IniAddSection");
            if (!Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);

            bool result = IniReadWriter.AddSection(fileName, section);
            if (result)
                logs.Add(new LogInfo(LogState.Success, $"Section [{section}] added to [{fileName}]", cmd));
            else
                logs.Add(new LogInfo(LogState.Error, $"Could not add section [{section}] to [{fileName}]", cmd));

            return logs;
        }

        public static List<LogInfo> IniAddSectionOp(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_IniAddSectionOp infoOp = cmd.Info.Cast<CodeInfo_IniAddSectionOp>();

            string fileName = StringEscaper.Preprocess(s, infoOp.Infos[0].FileName);

            Debug.Assert(fileName != null, $"{nameof(fileName)} != null");

            if (!StringEscaper.PathSecurityCheck(fileName, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            string[] sections = new string[infoOp.Cmds.Count];
            for (int i = 0; i < sections.Length; i++)
            {
                CodeInfo_IniAddSection info = infoOp.Infos[i];

                string sectionName = StringEscaper.Preprocess(s, info.Section);
                if (sectionName.Length == 0)
                    return LogInfo.LogErrorMessage(logs, "Section name cannot be empty");

                sections[i] = sectionName;
            }

            string dirPath = Path.GetDirectoryName(fileName);
            if (dirPath == null)
                throw new InternalException("Internal Logic Error at IniAddSectionOp");
            if (!Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);

            bool result = IniReadWriter.AddSections(fileName, sections);

            if (result)
            {
                for (int i = 0; i < sections.Length; i++)
                    logs.Add(new LogInfo(LogState.Success, $"Section [{sections[i]}] added", infoOp.Cmds[i]));
                logs.Add(new LogInfo(LogState.Success, $"Added [{sections.Length}] sections to [{fileName}]", cmd));
            }
            else
            {
                for (int i = 0; i < sections.Length; i++)
                    logs.Add(new LogInfo(LogState.Error, $"Could not add section [{sections[i]}]", infoOp.Cmds[i]));
                logs.Add(new LogInfo(LogState.Error, $"Could not add [{sections.Length}] sections to [{fileName}]", cmd));
            }

            return logs;
        }

        public static List<LogInfo> IniDeleteSection(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_IniDeleteSection info = cmd.Info.Cast<CodeInfo_IniDeleteSection>();

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string section = StringEscaper.Preprocess(s, info.Section);

            Debug.Assert(fileName != null, $"{nameof(fileName)} != null");
            Debug.Assert(section != null, $"{nameof(section)} != null");

            if (section.Length == 0)
                return LogInfo.LogErrorMessage(logs, "Section name cannot be empty");

            if (!StringEscaper.PathSecurityCheck(fileName, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            bool result = IniReadWriter.DeleteSection(fileName, section);

            if (result)
                logs.Add(new LogInfo(LogState.Success, $"Section [{section}] deleted from [{fileName}]", cmd));
            else
                logs.Add(new LogInfo(LogState.Error, $"Could not delete section [{section}] from [{fileName}]", cmd));
            return logs;
        }

        public static List<LogInfo> IniDeleteSectionOp(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_IniDeleteSectionOp infoOp = cmd.Info.Cast<CodeInfo_IniDeleteSectionOp>();

            string fileName = StringEscaper.Preprocess(s, infoOp.Infos[0].FileName);

            Debug.Assert(fileName != null, $"{nameof(fileName)} != null");

            if (!StringEscaper.PathSecurityCheck(fileName, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            string[] sections = new string[infoOp.Cmds.Count];
            for (int i = 0; i < sections.Length; i++)
            {
                CodeInfo_IniDeleteSection info = infoOp.Infos[i];

                string sectionName = StringEscaper.Preprocess(s, info.Section); // WB082 : 여기 값은 변수 Expand 안한다.
                if (sectionName.Length == 0)
                    return LogInfo.LogErrorMessage(logs, "Section name cannot be empty");

                sections[i] = sectionName;
            }

            bool[] results = IniReadWriter.DeleteSections(fileName, sections);
            int success = results.Count(x => x);
            int failure = results.Count(x => !x);
            for (int i = 0; i < sections.Length; i++)
            {
                if (results[i])
                    logs.Add(new LogInfo(LogState.Success, $"Section [{sections[i]}] deleted", infoOp.Cmds[i]));
                else
                    logs.Add(new LogInfo(LogState.Error, $"Could not delete section [{sections[i]}]", infoOp.Cmds[i]));
            }

            if (0 < success)
                logs.Add(new LogInfo(LogState.Success, $"Deleted [{success}] sections from [{fileName}]", cmd));
            if (0 < failure)
                logs.Add(new LogInfo(LogState.Error, $"Could not delete [{failure}] sections from [{fileName}]", cmd));

            return logs;
        }

        public static List<LogInfo> IniWriteTextLine(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_IniWriteTextLine info = cmd.Info.Cast<CodeInfo_IniWriteTextLine>();

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string section = StringEscaper.Preprocess(s, info.Section);
            string line = StringEscaper.Preprocess(s, info.Line);

            Debug.Assert(fileName != null, $"{nameof(fileName)} != null");
            Debug.Assert(section != null, $"{nameof(section)} != null");
            Debug.Assert(line != null, $"{nameof(line)} != null");

            if (section.Length == 0)
                return LogInfo.LogErrorMessage(logs, "Section name cannot be empty");

            if (!StringEscaper.PathSecurityCheck(fileName, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            string dirPath = Path.GetDirectoryName(fileName);
            if (dirPath == null)
                throw new InternalException("Internal Logic Error at IniWriteTextLine");
            if (!Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);

            bool result = IniReadWriter.WriteRawLine(fileName, section, line, info.Append);

            if (result)
                logs.Add(new LogInfo(LogState.Success, $"Line [{line}] wrote to [{fileName}]", cmd));
            else
                logs.Add(new LogInfo(LogState.Error, $"Could not write line [{line}] to [{fileName}]", cmd));

            return logs;
        }

        public static List<LogInfo> IniWriteTextLineOp(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_IniWriteTextLineOp infoOp = cmd.Info.Cast<CodeInfo_IniWriteTextLineOp>();

            string fileName = StringEscaper.Preprocess(s, infoOp.Infos[0].FileName);

            Debug.Assert(fileName != null, $"{nameof(fileName)} != null");

            bool append = infoOp.Infos[0].Append;

            if (!StringEscaper.PathSecurityCheck(fileName, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            List<IniKey> keyList = new List<IniKey>(infoOp.Infos.Count);
            for (int i = 0; i < infoOp.Infos.Count; i++)
            {
                CodeInfo_IniWriteTextLine info = infoOp.Infos[i];

                string sectionName = StringEscaper.Preprocess(s, info.Section);
                string line = StringEscaper.Preprocess(s, info.Line);

                if (append)
                    keyList.Add(new IniKey(sectionName, line));
                else // prepend
                    keyList.Insert(0, new IniKey(sectionName, line));
            }
            IniKey[] keys = keyList.ToArray();

            string dirPath = Path.GetDirectoryName(fileName);
            if (dirPath == null)
                throw new InternalException("Internal Logic Error at IniWriteTextLineOp");
            if (!Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);

            bool result = IniReadWriter.WriteRawLines(fileName, keyList, append);

            if (result)
            {
                for (int i = 0; i < keys.Length; i++)
                {
                    IniKey kv = keyList[i];
                    logs.Add(new LogInfo(LogState.Success, $"Line [{kv.Key}] written", infoOp.Cmds[i]));
                }
                logs.Add(new LogInfo(LogState.Success, $"Wrote [{keys.Length}] lines to [{fileName}]", cmd));
            }
            else
            {
                for (int i = 0; i < keys.Length; i++)
                {
                    IniKey kv = keyList[i];
                    logs.Add(new LogInfo(LogState.Error, $"Could not write line [{kv.Key}]", infoOp.Cmds[i]));
                }
                logs.Add(new LogInfo(LogState.Error, $"Could not write [{keys.Length}] lines to [{fileName}]", cmd));
            }

            return logs;
        }

        public static List<LogInfo> IniMerge(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_IniMerge info = cmd.Info.Cast<CodeInfo_IniMerge>();

            string srcFile = StringEscaper.Preprocess(s, info.SrcFile);
            string destFile = StringEscaper.Preprocess(s, info.DestFile);

            Debug.Assert(srcFile != null, $"{nameof(srcFile)} != null");
            Debug.Assert(destFile != null, $"{nameof(destFile)} != null");

            if (!StringEscaper.PathSecurityCheck(destFile, out string errorMsg))
                return LogInfo.LogErrorMessage(logs, errorMsg);

            bool result = IniReadWriter.Merge(srcFile, destFile);
            if (result)
                logs.Add(new LogInfo(LogState.Success, $"[{srcFile}] merged into [{destFile}]", cmd));
            else
                logs.Add(new LogInfo(LogState.Error, $"Could not merge [{srcFile}] into [{destFile}]", cmd));

            return logs;
        }
    }
}
