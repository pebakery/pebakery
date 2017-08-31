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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PEBakery.Core.Commands
{
    public static class CommandControl
    {
        public static List<LogInfo> Set(EngineState s, CodeCommand cmd)
        {
            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Set));
            CodeInfo_Set info = cmd.Info as CodeInfo_Set;

            List<LogInfo> logs = Variables.SetVariable(s, info.VarKey, info.VarValue, info.Global, info.Permanent);

            return logs;
        }

        public static List<LogInfo> AddVariables(EngineState s, CodeCommand cmd)
        {
            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_AddVariables));
            CodeInfo_AddVariables info = cmd.Info as CodeInfo_AddVariables;

            string pluginFile = StringEscaper.Preprocess(s, info.PluginFile);
            string sectionName = StringEscaper.Preprocess(s, info.SectionName);

            Plugin p = s.GetPluginInstance(cmd, pluginFile, out bool inCurrentPlugin);

            // Does section exists?
            if (!p.Sections.ContainsKey(sectionName))
                throw new ExecuteException($"[{pluginFile}] does not have section [{sectionName}]");

            SectionAddress addr = new SectionAddress(p, p.Sections[sectionName]);

            List<LogInfo> logs = s.Variables.AddVariables(info.Global ? VarsType.Global : VarsType.Local, addr.Section);

            return logs;
        }

        public static List<LogInfo> GetParam(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_GetParam));
            CodeInfo_GetParam info = cmd.Info as CodeInfo_GetParam;

            logs.Add(s.Variables.SetValue(VarsType.Local, info.VarName, s.CurSectionParams[info.Index]));

            return logs;
        }

        public static List<LogInfo> PackParam(EngineState s, CodeCommand cmd)
        { // TODO : Not fully understand WB082's internal mechanism
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_PackParam));
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

        public static List<LogInfo> Exit(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Exit));
            CodeInfo_Exit info = cmd.Info as CodeInfo_Exit;

            s.PassCurrentPluginFlag = true;

            logs.Add(new LogInfo(info.NoWarn ? LogState.Ignore : LogState.Warning, info.Message, cmd));

            return logs;
        }

        public static List<LogInfo> Halt(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Halt));
            CodeInfo_Halt info = cmd.Info as CodeInfo_Halt;

            s.ErrorHaltFlag = true;

            logs.Add(new LogInfo(LogState.Error, info.Message, cmd));

            return logs;
        }

        public static List<LogInfo> Wait(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Wait));
            CodeInfo_Wait info = cmd.Info as CodeInfo_Wait;

            if (int.TryParse(info.Second, NumberStyles.Integer, CultureInfo.InvariantCulture, out int second) == false)
                throw new InvalidCodeCommandException($"Argument [{info.Second}] is not valid number", cmd);

            // Task.Run(() => Thread.Sleep(second * 1000)).Wait();
            Task.Delay(second * 1000).Wait();

            logs.Add(new LogInfo(LogState.Success, $"Slept [{info.Second}] seconds", cmd));

            return logs;
        }

        public static List<LogInfo> Beep(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Beep));
            CodeInfo_Beep info = cmd.Info as CodeInfo_Beep;

            BeepType type = info.TypeStringToEnum(s);

            switch (type)
            {
                case BeepType.OK:
                    SystemSounds.Beep.Play();
                    break;
                case BeepType.Error:
                    SystemSounds.Exclamation.Play();
                    break;
                case BeepType.Confirmation:
                    SystemSounds.Question.Play();
                    break;
                case BeepType.Asterisk:
                    SystemSounds.Asterisk.Play();
                    break;
            }

            logs.Add(new LogInfo(LogState.Success, $"Played sound [{type}]", cmd));

            return logs;
        }
    }
}
