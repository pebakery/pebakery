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
using System.Globalization;

namespace PEBakery.Core.Commands
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
                throw new InternalCodeInfoException();

            string pluginFile = StringEscaper.Unescape(info.PluginFile);
            string sectionName = StringEscaper.Preprocess(s, info.SectionName);
            List<string> paramList = StringEscaper.Preprocess(s, info.Parameters);

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
                    throw new ExecuteException($"No plugin in [{fullPath}]");
            }
            
            // Does section exists?
            if (!targetPlugin.Sections.ContainsKey(sectionName))
                throw new ExecuteException($"[{pluginFile}] does not have section [{sectionName}]");

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
                Engine.RunSection(s, nextAddr, paramList, s.CurDepth + 1, callback);

            s.CurDepth = depthBackup;
            s.Logger.LogEndOfSection(s.BuildId, nextAddr, s.CurDepth, inCurrentPlugin, cmd);
        }

        public static void Loop(EngineState s, CodeCommand cmd)
        {
            CodeInfo_Loop info = cmd.Info as CodeInfo_Loop;
            if (info == null)
                throw new InternalCodeInfoException();

            // TODO
            if (info.Break)
            {
                if (s.LoopRunning)
                {
                    s.LoopRunning = false;
                }
                else
                {
                    s.Logger.Build_Write(s.BuildId, new LogInfo(LogState.Error, "Loop is not running", cmd));
                }
            }
            else
            {
                string startIdxStr = StringEscaper.Preprocess(s, info.StartIdx);
                if (long.TryParse(startIdxStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out long startIdx) == false)
                    throw new ExecuteException($"Argument [{startIdxStr}] is not valid integer");
                string endIdxStr = StringEscaper.Preprocess(s, info.EndIdx);
                if (long.TryParse(endIdxStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out long endIdx) == false)
                    throw new ExecuteException($"Argument [{endIdxStr}] is not valid integer");
                long loopCount = endIdx - startIdx + 1;

                // Prepare Loop
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
                        throw new ExecuteException($"No plugin in [{fullPath}]");
                }

                // Does section exists?
                if (!targetPlugin.Sections.ContainsKey(sectionName))
                    throw new ExecuteException($"[{pluginFile}] does not have section [{sectionName}]");

                string logMessage;
                if (inCurrentPlugin)
                    logMessage = $"Loop Section [{sectionName}] [{loopCount}] times";
                else
                    logMessage = $"Loop [{targetPlugin.Title}]'s Section [{sectionName}] [{loopCount}] times";
                s.Logger.Build_Write(s.BuildId, new LogInfo(LogState.Info, logMessage, cmd, s.CurDepth));

                // Loop it
                SectionAddress nextAddr = new SectionAddress(targetPlugin, targetPlugin.Sections[sectionName]);
                for (s.LoopCounter = startIdx; s.LoopCounter <= endIdx; s.LoopCounter++)
                { // Counter Variable is [#c]
                    s.Logger.Build_Write(s.BuildId, new LogInfo(LogState.Info, $"Entering Loop [{s.LoopCounter}/{loopCount}]", cmd, s.CurDepth));
                    int depthBackup = s.CurDepth;
                    s.LoopRunning = true;
                    Engine.RunSection(s, nextAddr, info.Parameters, s.CurDepth + 1, true);
                    s.LoopRunning = false;
                    s.CurDepth = depthBackup;
                    s.Logger.Build_Write(s.BuildId, new LogInfo(LogState.Info, $"End of Loop [{s.LoopCounter}/{loopCount}]", cmd, s.CurDepth));
                }
            }
        }

        public static void If(EngineState s, CodeCommand cmd)
        {
            CodeInfo_If info = cmd.Info as CodeInfo_If;
            if (info == null)
                throw new InternalCodeInfoException();

            if (info.Condition.Check(s, out string msg))
            { // Condition matched, run it
                s.Logger.Build_Write(s.BuildId, new LogInfo(LogState.Success, msg, cmd, s.CurDepth));

                int depthBackup = s.CurDepth;
                Engine.RunCommands(s, cmd.Addr, info.Link, s.CurSectionParams, s.CurDepth + 1, false);
                s.CurDepth = depthBackup;
                s.Logger.Build_Write(s.BuildId, new LogInfo(LogState.Info, $"End of CodeBlock", cmd, s.CurDepth));

                s.ElseFlag = false;
            }
            else
            { // Do not run
                s.Logger.Build_Write(s.BuildId, new LogInfo(LogState.Ignore, msg, cmd, s.CurDepth));

                s.ElseFlag = true;
            }
        }

        public static void Else(EngineState s, CodeCommand cmd)
        {
            CodeInfo_Else info = cmd.Info as CodeInfo_Else;
            if (info == null)
                throw new InternalCodeInfoException();

            if (s.ElseFlag)
            {
                s.Logger.Build_Write(s.BuildId, new LogInfo(LogState.Success, "Else condition met", cmd, s.CurDepth));

                int depthBackup = s.CurDepth;
                Engine.RunCommands(s, cmd.Addr, info.Link, s.CurSectionParams, s.CurDepth + 1, false);
                s.CurDepth = depthBackup;
                s.Logger.Build_Write(s.BuildId, new LogInfo(LogState.Info, $"End of CodeBlock", cmd, s.CurDepth));

                s.ElseFlag = false;
            }
            else
            {
                s.Logger.Build_Write(s.BuildId, new LogInfo(LogState.Ignore, "Else condition not met", cmd, s.CurDepth));
            }
        }
    }
}
