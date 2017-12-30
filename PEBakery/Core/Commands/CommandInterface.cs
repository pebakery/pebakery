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
            List<LogInfo> logs = new List<LogInfo>(1);

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Visible));
            CodeInfo_Visible info = cmd.Info as CodeInfo_Visible;

            string visibilityStr = StringEscaper.Preprocess(s, info.Visibility);
            bool visibility = false;
            if (visibilityStr.Equals("True", StringComparison.OrdinalIgnoreCase))
                visibility = true;
            else if (visibilityStr.Equals("False", StringComparison.OrdinalIgnoreCase) == false)
            {
                logs.Add(new LogInfo(LogState.Error, $"Invalid boolean value [{visibilityStr}]"));
                return logs;
            }

            Plugin p = cmd.Addr.Plugin;
            PluginSection iface = p.GetInterface(out string ifaceSecName);
            if (iface == null)
            {
                logs.Add(new LogInfo(LogState.Error, $"Plugin [{cmd.Addr.Plugin.ShortPath}] does not have section [{ifaceSecName}]"));
                return logs;
            }

            List<UICommand> uiCodes = iface.GetUICodes(true);
            UICommand uiCmd = uiCodes.Find(x => x.Key.Equals(info.InterfaceKey, StringComparison.OrdinalIgnoreCase));
            if (uiCmd == null)
            {
                logs.Add(new LogInfo(LogState.Error, $"Cannot find interface control [{info.InterfaceKey}] from section [{ifaceSecName}]"));
                return logs;
            }

            if (uiCmd.Visibility != visibility)
            {
                uiCmd.Visibility = visibility;
                uiCmd.Update();

                // Re-render Plugin
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MainWindow w = (Application.Current.MainWindow as MainWindow);
                    if (w.CurMainTree.Plugin == cmd.Addr.Plugin)
                        w.DrawPlugin(cmd.Addr.Plugin);
                });
            }

            logs.Add(new LogInfo(LogState.Success, $"Interface control [{info.InterfaceKey}]'s visibility set to [{visibility}]"));

            return logs;
        }

        public static List<LogInfo> VisibleOp(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>(8);

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_VisibleOp));
            CodeInfo_VisibleOp infoOp = cmd.Info as CodeInfo_VisibleOp;

            Plugin p = cmd.Addr.Plugin;
            PluginSection iface = p.GetInterface(out string ifaceSecName);
            if (iface == null)
            {
                logs.Add(new LogInfo(LogState.Error, $"Plugin [{cmd.Addr.Plugin.ShortPath}] does not have section [{ifaceSecName}]"));
                return logs;
            }

            List<UICommand> uiCodes = iface.GetUICodes(true);

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
                UICommand uiCmd = uiCodes.Find(x => x.Key.Equals(args.Item1, StringComparison.OrdinalIgnoreCase));
                if (uiCmd == null)
                {
                    logs.Add(new LogInfo(LogState.Error, $"Cannot find interface control [{args.Item1}] from section [{ifaceSecName}]"));
                    continue;
                }

                uiCmd.Visibility = args.Item2;
                uiCmdList.Add(uiCmd);
            }

            UICommand.Update(uiCmdList);

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

        public static List<LogInfo> ReadInterface(EngineState s, CodeCommand cmd)
        { // ReadInterface,<Element>,<PluginFile>,<Section>,<Key>,<DestVar>
            List<LogInfo> logs = new List<LogInfo>(1);

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_ReadInterface));
            CodeInfo_ReadInterface info = cmd.Info as CodeInfo_ReadInterface;

            string pluginFile = StringEscaper.Preprocess(s, info.PluginFile);
            string section = StringEscaper.Preprocess(s, info.Section);
            string key = StringEscaper.Preprocess(s, info.Key);

            Plugin p = Engine.GetPluginInstance(s, cmd, s.CurrentPlugin.FullPath, pluginFile, out bool inCurrentPlugin);

            if (!p.Sections.ContainsKey(section))
            {
                logs.Add(new LogInfo(LogState.Error, $"Plugin [{pluginFile}] does not have section [{section}]"));
                return logs;
            }

            PluginSection iface = p.Sections[section];
            List<UICommand> uiCmds = iface.GetUICodes(true);
            UICommand uiCmd = uiCmds.Find(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (uiCmd == null)
            {
                logs.Add(new LogInfo(LogState.Error, $"Interface [{key}] does not exist"));
                return logs;
            }

            string destStr;
            switch (info.Element)
            {
                case InterfaceElement.Text:
                    destStr = uiCmd.Text;
                    break;
                case InterfaceElement.Visible:
                    destStr = uiCmd.Visibility.ToString();
                    break;
                case InterfaceElement.PosX:
                    destStr = ((int)uiCmd.Rect.X).ToString();
                    break;
                case InterfaceElement.PosY:
                    destStr = ((int)uiCmd.Rect.Y).ToString();
                    break;
                case InterfaceElement.Width:
                    destStr = ((int)uiCmd.Rect.Width).ToString();
                    break;
                case InterfaceElement.Height:
                    destStr = ((int)uiCmd.Rect.Height).ToString();
                    break;
                case InterfaceElement.Value:
                    destStr = uiCmd.GetValue();
                    if (destStr == null)
                    {
                        logs.Add(new LogInfo(LogState.Error, $"Reading [Value] from [{uiCmd.Type}] is not supported"));
                        return logs;
                    }
                    break;
                default:
                    throw new InternalException($"Internal Logic Error at ReadInterface");
            }

            // Do not expand read values
            List<LogInfo> varLogs = Variables.SetVariable(s, info.DestVar, destStr, false, false, false);
            logs.AddRange(varLogs);

            return logs;
        }

        public static List<LogInfo> WriteInterface(EngineState s, CodeCommand cmd)
        { // WriteInterface,<Element>,<PluginFile>,<Section>,<Key>,<Value>
            List<LogInfo> logs = new List<LogInfo>(2);

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_WriteInterface));
            CodeInfo_WriteInterface info = cmd.Info as CodeInfo_WriteInterface;

            string pluginFile = StringEscaper.Preprocess(s, info.PluginFile);
            string section = StringEscaper.Preprocess(s, info.Section);
            string key = StringEscaper.Preprocess(s, info.Key);
            string finalValue = StringEscaper.Preprocess(s, info.Value);

            Plugin p = Engine.GetPluginInstance(s, cmd, s.CurrentPlugin.FullPath, pluginFile, out bool inCurrentPlugin);

            if (!p.Sections.ContainsKey(section))
            {
                logs.Add(new LogInfo(LogState.Error, $"Plugin [{pluginFile}] does not have section [{section}]"));
                return logs;
            }

            PluginSection iface = p.Sections[section];
            List<UICommand> uiCmds = iface.GetUICodes(true);
            UICommand uiCmd = uiCmds.Find(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (uiCmd == null)
            {
                logs.Add(new LogInfo(LogState.Error, $"Interface [{key}] does not exist"));
                return logs;
            }

            switch (info.Element)
            {
                case InterfaceElement.Text:
                    uiCmd.Text = finalValue;
                    break;
                case InterfaceElement.Visible:
                    {
                        bool visibility = false;
                        if (finalValue.Equals("True", StringComparison.OrdinalIgnoreCase))
                            visibility = true;
                        else if (!finalValue.Equals("False", StringComparison.OrdinalIgnoreCase))
                        {
                            logs.Add(new LogInfo(LogState.Error, $"[{finalValue}] is not a valid boolean value"));
                            return logs;
                        }

                        uiCmd.Visibility = visibility;
                    }
                    break;
                case InterfaceElement.PosX:
                    {
                        if (!NumberHelper.ParseInt32(finalValue, out int x))
                        {
                            logs.Add(new LogInfo(LogState.Error, $"[{finalValue}] is not a valid integer"));
                            return logs;
                        }

                        uiCmd.Rect.X = x;
                    }
                    break;
                case InterfaceElement.PosY:
                    {
                        if (!NumberHelper.ParseInt32(finalValue, out int y))
                        {
                            logs.Add(new LogInfo(LogState.Error, $"[{finalValue}] is not a valid integer"));
                            return logs;
                        }

                        uiCmd.Rect.Y = y;
                    }
                    break;
                case InterfaceElement.Width:
                    {
                        if (!NumberHelper.ParseInt32(finalValue, out int width))
                        {
                            logs.Add(new LogInfo(LogState.Error, $"[{finalValue}] is not a valid integer"));
                            return logs;
                        }

                        uiCmd.Rect.Width = width;
                    }
                    break;
                case InterfaceElement.Height:
                    {
                        if (!NumberHelper.ParseInt32(finalValue, out int height))
                        {
                            logs.Add(new LogInfo(LogState.Error, $"[{finalValue}] is not a valid integer"));
                            return logs;
                        }

                        uiCmd.Rect.Height = height;
                    }
                    break;
                case InterfaceElement.Value:
                    {
                        bool success = uiCmd.SetValue(finalValue, false, out List<LogInfo> varLogs);
                        logs.AddRange(varLogs);

                        if (success == false && varLogs.Count == 0)
                        {
                            logs.Add(new LogInfo(LogState.Error, $"Wring [Value] to [{uiCmd.Type}] is not supported"));
                            return logs;
                        } 
                    }
                    break;
                default:
                    throw new InternalException($"Internal Logic Error at WriteInterface");
            }

            // Update uiCmd into file
            uiCmd.Update();

            // Rerender Plugin
            Application.Current?.Dispatcher.Invoke(() =>
            { // Application.Current is null in unit test
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

                Application.Current?.Dispatcher.Invoke(() =>
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

            logs.Add(new LogInfo(info.Warn ? LogState.Warning : LogState.Success, message, cmd));

            return logs;
        }

        public static List<LogInfo> EchoFile(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_EchoFile));
            CodeInfo_EchoFile info = cmd.Info as CodeInfo_EchoFile;

            string srcFile = StringEscaper.Preprocess(s, info.SrcFile);

            if (!File.Exists(srcFile))
            {
                logs.Add(new LogInfo(LogState.Warning, $"File [{srcFile}] does not exist", cmd));
                return logs;
            }

            if (info.Encode)
            { // Binary Mode -> encode files into log database
                string tempFile = Path.GetRandomFileName();
                try
                {
                    // Create dummy plugin instance
                    FileHelper.WriteTextBOM(tempFile, Encoding.UTF8);
                    Plugin p = cmd.Addr.Project.LoadPlugin(tempFile, true, false);

                    // Encode binary file into plugin instance
                    string fileName = Path.GetFileName(srcFile);
                    EncodedFile.AttachFile(p, "Folder", fileName, srcFile, EncodedFile.EncodeMode.Compress);

                    // Remove Plugin instance
                    p = null;

                    // Read encoded text strings into memory
                    string txtStr;
                    Encoding encoding = FileHelper.DetectTextEncoding(tempFile);
                    using (StreamReader r = new StreamReader(tempFile, encoding))
                    {
                        txtStr = r.ReadToEnd();
                    }

                    string logStr;
                    if (txtStr.EndsWith("\r\n", StringComparison.Ordinal))
                        logStr = $"Encoded File [{srcFile}]\r\n{txtStr}";
                    else
                        logStr = $"Encoded File [{srcFile}]\r\n{txtStr}\r\n";

                    s.MainViewModel.BuildEchoMessage = logStr;
                    logs.Add(new LogInfo(info.Warn ? LogState.Warning : LogState.Success, logStr, cmd));
                }
                finally
                {
                    File.Delete(tempFile);
                }
            }
            else
            { // Text Mode -> Just read with StreamReader
                string txtStr;
                Encoding encoding = FileHelper.DetectTextEncoding(srcFile);
                using (StreamReader r = new StreamReader(srcFile, encoding))
                {
                    txtStr = r.ReadToEnd();
                }

                string logStr;
                if (txtStr.EndsWith("\r\n", StringComparison.Ordinal))
                    logStr = $"File [{srcFile}]\r\n{txtStr}";
                else
                    logStr = $"File [{srcFile}]\r\n{txtStr}\r\n";

                s.MainViewModel.BuildEchoMessage = logStr;
                logs.Add(new LogInfo(info.Warn ? LogState.Warning : LogState.Success, logStr, cmd));
            }

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
                        Debug.Assert(info.SubInfo.GetType() == typeof(UserInputInfo_DirFile));
                        UserInputInfo_DirFile subInfo = info.SubInfo as UserInputInfo_DirFile;

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
                                logs.Add(new LogInfo(LogState.Error, "File path was not chosen by user"));
                                return logs;
                            }
                        }
                        else
                        {
                            VistaFolderBrowserDialog dialog = new VistaFolderBrowserDialog()
                            {
                                SelectedPath = initPath,
                            };

                            bool failure = false;
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                if (dialog.ShowDialog(Application.Current.MainWindow))
                                {
                                    selectedPath = dialog.SelectedPath;
                                    logs.Add(new LogInfo(LogState.Success, $"Directory path [{selectedPath}] was chosen by user"));
                                }
                                else
                                {
                                    logs.Add(new LogInfo(LogState.Error, "Directory path was not chosen by user"));
                                    failure = true;
                                }
                            });
                            if (failure)
                                return logs;
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
