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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PEBakery.WPF;
using System.Windows;
using System.Windows.Threading;
using System.Diagnostics;

namespace PEBakery.Core.Commands
{
    public static class CommandInterface
    {
        public static List<LogInfo> Visible(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Visible));
            CodeInfo_Visible info = cmd.Info as CodeInfo_Visible;

            string visibilityStr = StringEscaper.Preprocess(s, info.Visibility);
            bool visibility = false;
            if (visibilityStr.Equals("True", StringComparison.OrdinalIgnoreCase))
                visibility = true;
            else if (visibilityStr.Equals("False", StringComparison.OrdinalIgnoreCase) == false)
                throw new ExecuteException($"Invalid boolean value [{visibilityStr}]");

            if (cmd.Addr.Plugin.Sections.ContainsKey("Interface") == false)
            {
                logs.Add(new LogInfo(LogState.Error, $"Plugin [{cmd.Addr.Plugin.ShortPath}] does not have section [Interface]"));
                return logs;
            }

            List<UICommand> uiCodes = cmd.Addr.Plugin.Sections["Interface"].GetUICodes();
            UICommand uiCmd = uiCodes.FirstOrDefault(x => x.Key.Equals(info.InterfaceKey, StringComparison.OrdinalIgnoreCase));
            if (uiCmd == null)
            {
                logs.Add(new LogInfo(LogState.Error, $"Cannot find interface control [{info.InterfaceKey}] from section [Interface]"));
                return logs;
            }

            logs.Add(new LogInfo(LogState.Success, $"Interface control [{info.InterfaceKey}]'s visibility set to [{visibility}]"));

            if (uiCmd.Visibility != visibility)
            {
                uiCmd.Visibility = visibility;
                UIRenderer.UpdatePlugin("Interface", uiCmd);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    MainWindow w = (Application.Current.MainWindow as MainWindow);
                    if (w.CurrentTree.Node.Data == cmd.Addr.Plugin)
                        w.DrawPlugin(cmd.Addr.Plugin);
                });
            }

            return logs;
        }

        public static List<LogInfo> VisibleOp(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_VisibleOp));
            CodeInfo_VisibleOp infoOp = cmd.Info as CodeInfo_VisibleOp;

            if (cmd.Addr.Plugin.Sections.ContainsKey("Interface") == false)
            {
                logs.Add(new LogInfo(LogState.Error, $"Plugin [{cmd.Addr.Plugin.ShortPath}] does not have section [Interface]"));
                return logs;
            }

            List<UICommand> uiCodes = cmd.Addr.Plugin.Sections["Interface"].GetUICodes();

            List<Tuple<string, bool>> prepArgs = new List<Tuple<string, bool>>();
            foreach (CodeInfo_Visible info in infoOp.InfoList)
            {
                string visibilityStr = StringEscaper.Preprocess(s, info.Visibility);
                bool visibility = false;
                if (visibilityStr.Equals("True", StringComparison.OrdinalIgnoreCase))
                    visibility = true;
                else if (visibilityStr.Equals("False", StringComparison.OrdinalIgnoreCase) == false)
                    throw new ExecuteException($"Invalid boolean value [{visibilityStr}]");

                prepArgs.Add(new Tuple<string, bool>(info.InterfaceKey, visibility));
            }

            List<UICommand> uiCmdList = new List<UICommand>();
            foreach (Tuple<string, bool> args in prepArgs)
            {
                UICommand uiCmd = uiCodes.FirstOrDefault(x => x.Key.Equals(args.Item1, StringComparison.OrdinalIgnoreCase));
                if (uiCmd == null)
                {
                    logs.Add(new LogInfo(LogState.Error, $"Cannot find interface control [{args.Item1}] from section [Interface]"));
                    continue;
                }

                uiCmd.Visibility = args.Item2;
                uiCmdList.Add(uiCmd);
            }

            UIRenderer.UpdatePlugin("Interface", uiCmdList);

            foreach (Tuple<string, bool> args in prepArgs)
                logs.Add(new LogInfo(LogState.Success, $"Interface control [{args.Item1}]'s visibility set to [{args.Item2}]"));

            Application.Current.Dispatcher.Invoke(() =>
            {
                MainWindow w = (Application.Current.MainWindow as MainWindow);
                if (w.CurrentTree.Node.Data == cmd.Addr.Plugin)
                    w.DrawPlugin(cmd.Addr.Plugin);
            });

            return logs;
        }
    }
}
