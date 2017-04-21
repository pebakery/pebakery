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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Win32;
using PEBakery.Exceptions;

namespace PEBakery.Core
{
    public static class CommandBranch
    {
        public static void RunExec(EngineState s, CodeCommand cmd, bool preserveCurParams = false)
        {
            RunExec(s, cmd, preserveCurParams, false);
        }

        public static void RunExec(EngineState s, CodeCommand cmd, bool preserveCurParams, bool callback)
        {
            CodeInfo_RunExec info = cmd.Info as CodeInfo_RunExec;
            if (info == null)
                throw new InvalidCodeCommandException($"Command [{cmd.Type}] should have [CodeInfo_RunExec]", cmd);

            // Get necesssary operand
            string pluginFile = StringEscaper.Unescape(info.PluginFile);
            string sectionName = StringEscaper.Preprocess(s, info.SectionName);
            List<string> parameters = StringEscaper.Preprocess(s, info.Parameters);

            bool inCurrentPlugin = false;
            if (info.PluginFile.Equals("%PluginFile%", StringComparison.OrdinalIgnoreCase))
                inCurrentPlugin = true;
            else if (info.PluginFile.Equals("%ScriptFile%", StringComparison.OrdinalIgnoreCase))
                inCurrentPlugin = true;

            Plugin targetPlugin;
            if (inCurrentPlugin)
                targetPlugin = s.CurrentPlugin;
            else
            {
                string fullPath = StringEscaper.ExpandVariables(s, pluginFile);
                targetPlugin = s.Project.GetPluginByFullPath(fullPath);
                if (targetPlugin == null)
                    throw new InvalidCodeCommandException($"No plugin in [{fullPath}]", cmd);
            }
            
            // Does section exists?
            if (!targetPlugin.Sections.ContainsKey(sectionName))
                throw new InvalidCodeCommandException($"[{pluginFile}] does not have section [{sectionName}]", cmd);

            // Branch to new section
            SectionAddress nextAddr = new SectionAddress(targetPlugin, targetPlugin.Sections[sectionName]);
            s.Logger.LogStartOfSection(s.BuildId, nextAddr, s.CurDepth, inCurrentPlugin, cmd);

            // Exec utilizes [Variables] section of the plugin
            if (cmd.Type == CodeType.Exec && targetPlugin.Sections.ContainsKey("Varaibles"))
            {
                s.Variables.AddVariables(VarsType.Local, targetPlugin.Sections["Variables"]);
            }

            // Run Section
            int depthBackup = s.CurDepth;
            if (preserveCurParams)
                Engine.RunSection(s, nextAddr, s.CurSectionParams, s.CurDepth + 1, callback);
            else
                Engine.RunSection(s, nextAddr, parameters, s.CurDepth + 1, callback);

            s.CurDepth = depthBackup;
            s.Logger.LogEndOfSection(s.BuildId, nextAddr, s.CurDepth, inCurrentPlugin, cmd);
        }

        public static void Loop(EngineState s, CodeCommand cmd)
        {
            CodeInfo_Loop info = cmd.Info as CodeInfo_Loop;
            if (info == null)
                throw new InvalidCodeCommandException("Command [Loop] should have [CodeInfo_Loop]", cmd);
            
            // TODO
        }

        public static void If(EngineState s, CodeCommand cmd)
        {
            CodeInfo_If info = cmd.Info as CodeInfo_If;
            if (info == null)
                throw new InvalidCodeCommandException("Command [If] should have [CodeInfo_If]", cmd);

            if (info.Condition.Check(s, out string msg))
            { // Condition matched, run it
                s.RunElse = false;
                s.Logger.Build_Write(s.BuildId, new LogInfo(LogState.Success, $"If - {msg}", cmd, s.CurDepth));

                int depthBackup = s.CurDepth;
                Engine.RunCommands(s, info.Link, s.CurSectionParams, s.CurDepth + 1, false);
                s.CurDepth = depthBackup;
                s.Logger.Build_Write(s.BuildId, new LogInfo(LogState.Info, $"End of CodeBlock", cmd, s.CurDepth));
            }
            else
            { // Do not run
                s.RunElse = true;
                s.Logger.Build_Write(s.BuildId, new LogInfo(LogState.Ignore, msg, cmd, s.CurDepth));
            }
        }

        public static void Else(EngineState s, CodeCommand cmd)
        {
            CodeInfo_Else info = cmd.Info as CodeInfo_Else;
            if (info == null)
                throw new InvalidCodeCommandException("Command [Else] should have [CodeInfo_Else]", cmd);

            if (s.RunElse)
            {
                s.RunElse = false;
                s.Logger.Build_Write(s.BuildId, new LogInfo(LogState.Success, "Else condition met", cmd, s.CurDepth));

                int depthBackup = s.CurDepth;
                Engine.RunCommands(s, info.Link, s.CurSectionParams, s.CurDepth + 1, false);
                s.CurDepth = depthBackup;
                s.Logger.Build_Write(s.BuildId, new LogInfo(LogState.Info, $"End of CodeBlock", cmd, s.CurDepth));
            }
            else
            {
                s.Logger.Build_Write(s.BuildId, new LogInfo(LogState.Ignore, "Else condition not met", cmd, s.CurDepth));
            }
        }
    }
}
