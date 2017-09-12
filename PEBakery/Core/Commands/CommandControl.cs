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
using PEBakery.Helper;
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

            Plugin p = Engine.GetPluginInstance(s, cmd, s.CurrentPlugin.FullPath,  pluginFile, out bool inCurrentPlugin);

            // Does section exists?
            if (!p.Sections.ContainsKey(sectionName))
                throw new ExecuteException($"[{pluginFile}] does not have section [{sectionName}]");

            SectionAddress addr = new SectionAddress(p, p.Sections[sectionName]);

            List<LogInfo> logs = s.Variables.AddVariables(info.Global ? VarsType.Global : VarsType.Local, addr.Section);

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

            s.CmdHaltFlag = true;

            logs.Add(new LogInfo(LogState.Warning, info.Message, cmd));

            return logs;
        }

        public static List<LogInfo> Wait(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Wait));
            CodeInfo_Wait info = cmd.Info as CodeInfo_Wait;

            if (NumberHelper.ParseInt32(info.Second, out int second) == false)
                throw new InvalidCodeCommandException($"Argument [{info.Second}] is not valid number", cmd);

            Task.Delay(second * 1000).Wait();

            logs.Add(new LogInfo(LogState.Success, $"Slept [{info.Second}] seconds", cmd));

            return logs;
        }

        public static List<LogInfo> Beep(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Beep));
            CodeInfo_Beep info = cmd.Info as CodeInfo_Beep;

            switch (info.Type)
            {
                case BeepType.OK:
                    SystemSounds.Beep.Play();
                    break;
                case BeepType.Error:
                    SystemSounds.Hand.Play();
                    break;
                case BeepType.Asterisk:
                    SystemSounds.Asterisk.Play();
                    break;
                case BeepType.Confirmation:
                    SystemSounds.Question.Play();
                    break;
            }

            logs.Add(new LogInfo(LogState.Success, $"Played sound [{info.Type}]", cmd));

            return logs;
        }
    }
}
