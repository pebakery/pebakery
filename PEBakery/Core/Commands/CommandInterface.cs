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
using System.Globalization;
using PEBakery.WPF.Controls;
using System.IO;
using PEBakery.Helper;

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

                // Re-render Plugin
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MainWindow w = (Application.Current.MainWindow as MainWindow);
                    if (w.CurMainTree.Plugin == cmd.Addr.Plugin)
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

            // Re-render Plugin
            Application.Current.Dispatcher.Invoke(() =>
            {
                MainWindow w = (Application.Current.MainWindow as MainWindow);
                if (w.CurMainTree.Plugin == cmd.Addr.Plugin)
                    w.DrawPlugin(cmd.Addr.Plugin);
            });

            return logs;
        }

        public static List<LogInfo> Message(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Message));
            CodeInfo_Message info = cmd.Info as CodeInfo_Message;

            string message = StringEscaper.Preprocess(s, info.Message);
            MessageBoxImage image;
            switch (info.Action)
            {
                case CodeMessageAction.None:
                case CodeMessageAction.Information:
                    image = MessageBoxImage.Information;
                    break;
                case CodeMessageAction.Confirmation:
                    image = MessageBoxImage.Question;
                    break;
                case CodeMessageAction.Error:
                    image = MessageBoxImage.Error;
                    break;
                case CodeMessageAction.Warning:
                    image = MessageBoxImage.Warning;
                    break;
                default: // Internal Logic Error
                    Debug.Assert(false);
                    image = MessageBoxImage.Information;
                    break;
            }

            if (info.Timeout == null)
            {
                MessageBox.Show(message, cmd.Addr.Plugin.Title, MessageBoxButton.OK, image);
            }
            else
            {
                string timeoutStr = StringEscaper.Preprocess(s, info.Timeout);
                
                if (NumberHelper.ParseInt32(timeoutStr, out int timeout) == false)
                    throw new ExecuteException($"[{timeoutStr}] is not valid positive integer");
                if (timeout <= 0)
                    throw new ExecuteException($"Timeout must be positive integer [{timeoutStr}]");

                Application.Current.Dispatcher.Invoke(() =>
                {
                    CustomMessageBox.Show(message, cmd.Addr.Plugin.Title, MessageBoxButton.OK, image, timeout);
                });
            }

            string[] slices = message.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
            string firstLine = message;
            if (0 < slices.Length)
                firstLine = slices[0];
            logs.Add(new LogInfo(LogState.Success, $"MessageBox [{firstLine}]", cmd));

            return logs;
        }

        public static List<LogInfo> Echo(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Echo));
            CodeInfo_Echo info = cmd.Info as CodeInfo_Echo;

            string message = StringEscaper.Preprocess(s, info.Message);

            s.MainViewModel.BuildEchoMessage = message;

            logs.Add(new LogInfo(LogState.Success, $"Displayed [{message}]", cmd));

            return logs;
        }

        public static List<LogInfo> UserInput(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_UserInput));
            CodeInfo_UserInput info = cmd.Info as CodeInfo_UserInput;

            UserInputType type = info.Type;
            switch (type)
            {
                case UserInputType.DirPath:
                case UserInputType.FilePath:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(UserInputInfo_DirFilePath));
                        UserInputInfo_DirFilePath subInfo = info.SubInfo as UserInputInfo_DirFilePath;

                        string initPath = StringEscaper.Preprocess(s, subInfo.InitPath);
                        string selectedPath = initPath;
                        if (type == UserInputType.FilePath)
                        {
                            string filter = "All Files|*.*";
                            string initFile = Path.GetFileName(initPath);
                            if (initFile.StartsWith("*.", StringComparison.Ordinal) || initFile.Equals("*", StringComparison.Ordinal))
                            { // If wildcard exists, apply to filter.
                                string ext = Path.GetExtension(initFile);
                                if (1 < ext.Length && ext.StartsWith(".", StringComparison.Ordinal))
                                    ext = ext.Substring(1);
                                filter = $"{ext} Files|{initFile}";
                            }

                            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog()
                            {
                                Filter = filter,
                                InitialDirectory = Path.GetDirectoryName(initPath),
                            };

                            if (dialog.ShowDialog() == true)
                            {
                                selectedPath = dialog.FileName;
                                logs.Add(new LogInfo(LogState.Success, $"File path [{selectedPath}] was chosen by user"));
                            }
                            else
                            {
                                throw new ExecuteException("File path was not chosen by user");
                            }
                        }
                        else
                        {
                            System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog()
                            {
                                ShowNewFolderButton = true,
                                SelectedPath = initPath,
                            };
                            System.Windows.Forms.DialogResult result = dialog.ShowDialog();

                            if (result == System.Windows.Forms.DialogResult.OK)
                            {
                                selectedPath = dialog.SelectedPath;
                                logs.Add(new LogInfo(LogState.Success, $"Directory path [{selectedPath}] was chosen by user"));
                            }
                            else
                            {
                                throw new ExecuteException("Directory path was not chosen by user");
                            }
                        }

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, selectedPath);
                        logs.AddRange(varLogs);
                    }
                    break;
                default: // Error
                    throw new InvalidCodeCommandException($"Wrong UserInputType [{type}]");
            }

            return logs;
        }

        public static List<LogInfo> AddInterface(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_AddInterface));
            CodeInfo_AddInterface info = cmd.Info as CodeInfo_AddInterface;

            string pluginFile = StringEscaper.Preprocess(s, info.PluginFile);
            string interfaceSection = StringEscaper.Preprocess(s, info.Interface);
            string prefix = StringEscaper.Preprocess(s, info.Prefix);

            Plugin p = Engine.GetPluginInstance(s, cmd, s.CurrentPlugin.FullPath, pluginFile, out bool inCurrentPlugin);
            if (p.Sections.ContainsKey(interfaceSection))
            {
                List<UICommand> uiCodes = null;
                try { uiCodes = p.Sections[interfaceSection].GetUICodes(true); }
                catch { } // No [Interface] section, or unable to get List<UICommand>

                if (uiCodes != null)
                {
                    List<LogInfo> subLogs = s.Variables.UICommandToVariables(uiCodes, prefix);
                    if (0 < subLogs.Count)
                    {
                        s.Logger.Build_Write(s, new LogInfo(LogState.Info, $"Import variables from [{interfaceSection}]", cmd, s.CurDepth));
                        logs.AddRange(LogInfo.AddCommandDepth(subLogs, cmd, s.CurDepth + 1));
                        s.Logger.Build_Write(s, subLogs);
                        s.Logger.Build_Write(s, new LogInfo(LogState.Info, $"Imported {subLogs.Count} variables", cmd, s.CurDepth));
                    }
                }
            }

            return logs;
        }
    }
}
