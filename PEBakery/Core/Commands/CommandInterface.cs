/*
    Copyright (C) 2016-2018 Hajin Jang
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
using PEBakery.WPF.Controls;
using System.IO;
using PEBakery.Helper;
using Ookii.Dialogs.Wpf;

namespace PEBakery.Core.Commands
{
    public static class CommandInterface
    {
        public static List<LogInfo> Visible(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>(1);

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Visible), "Invalid CodeInfo");
            CodeInfo_Visible info = cmd.Info as CodeInfo_Visible;
            Debug.Assert(info != null, "Invalid CodeInfo");

            string visibilityStr = StringEscaper.Preprocess(s, info.Visibility);
            Debug.Assert(visibilityStr != null, $"{nameof(visibilityStr)} != null");

            bool visibility;
            if (visibilityStr.Equals("1", StringComparison.Ordinal) ||
                visibilityStr.Equals("True", StringComparison.OrdinalIgnoreCase))
                visibility = true;
            else if (visibilityStr.Equals("0", StringComparison.Ordinal) ||
                     visibilityStr.Equals("False", StringComparison.OrdinalIgnoreCase))
                visibility = false;
            else
                return LogInfo.LogErrorMessage(logs, $"Invalid boolean value [{visibilityStr}]");

            Script sc = cmd.Addr.Script;
            ScriptSection iface = sc.GetInterface(out string ifaceSecName);
            if (iface == null)
                return LogInfo.LogErrorMessage(logs, $"Script [{cmd.Addr.Script.TreePath}] does not have section [{ifaceSecName}]");

            List<UIControl> uiCtrls = iface.GetUICtrls(true);
            UIControl uiCtrl = uiCtrls.Find(x => x.Key.Equals(info.InterfaceKey, StringComparison.OrdinalIgnoreCase));
            if (uiCtrl == null)
                return LogInfo.LogErrorMessage(logs, $"Cannot find interface control [{info.InterfaceKey}] in section [{ifaceSecName}]");

            if (uiCtrl.Visibility != visibility)
            {
                uiCtrl.Visibility = visibility;
                uiCtrl.Update();

                // Re-render Script
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    MainWindow w = Application.Current.MainWindow as MainWindow;
                    if (w?.CurMainTree.Script.Equals(cmd.Addr.Script) == true)
                        w.DrawScript(cmd.Addr.Script);
                });
            }

            logs.Add(new LogInfo(LogState.Success, $"Interface control [{info.InterfaceKey}]'s visibility set to [{visibility}]"));

            return logs;
        }

        public static List<LogInfo> VisibleOp(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>(8);

            CodeInfo_VisibleOp infoOp = cmd.Info.Cast<CodeInfo_VisibleOp>();

            Script sc = cmd.Addr.Script;
            ScriptSection iface = sc.GetInterface(out string ifaceSecName);
            if (iface == null)
                return LogInfo.LogErrorMessage(logs, $"Script [{cmd.Addr.Script.TreePath}] does not have section [{ifaceSecName}]");

            List<UIControl> uiCtrls = iface.GetUICtrls(true);
            
            List<(string, bool, CodeCommand)> prepArgs = new List<(string, bool, CodeCommand)>(infoOp.Cmds.Count);
            foreach (CodeCommand subCmd in infoOp.Cmds)
            {
                CodeInfo_Visible info = subCmd.Info.Cast<CodeInfo_Visible>();

                string visibilityStr = StringEscaper.Preprocess(s, info.Visibility);
                bool visibility = false;
                if (visibilityStr.Equals("True", StringComparison.OrdinalIgnoreCase))
                    visibility = true;
                else if (!visibilityStr.Equals("False", StringComparison.OrdinalIgnoreCase))
                    return LogInfo.LogErrorMessage(logs, $"Invalid boolean value [{visibilityStr}]");

                prepArgs.Add((info.InterfaceKey, visibility, subCmd));
            }

            List<UIControl> uiCmds = new List<UIControl>();
            foreach ((string key, bool visibility, CodeCommand _) in prepArgs)
            {
                UIControl uiCmd = uiCtrls.FirstOrDefault(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
                if (uiCmd == null)
                    return LogInfo.LogErrorMessage(logs, $"Cannot find interface control [{key}] in section [{ifaceSecName}]");
                
                uiCmd.Visibility = visibility;
                uiCmds.Add(uiCmd);
            }

            UIControl.Update(uiCmds);

            foreach ((string key, bool visibility, CodeCommand subCmd) in prepArgs)
                logs.Add(new LogInfo(LogState.Success, $"Interface control [{key}]'s visibility set to [{visibility}]", subCmd));
            logs.Add(new LogInfo(LogState.Success, $"Total [{prepArgs.Count}] interface control set", cmd));

            // Rerender Script
            Application.Current?.Dispatcher.Invoke(() =>
            {
                MainWindow w = Application.Current.MainWindow as MainWindow;
                if (w?.CurMainTree.Script.Equals(cmd.Addr.Script) == true)
                    w.DrawScript(cmd.Addr.Script);
            });

            return logs;
        }

        public static List<LogInfo> ReadInterface(EngineState s, CodeCommand cmd)
        { // ReadInterface,<Element>,<ScriptFile>,<Section>,<Key>,<DestVar>
            List<LogInfo> logs = new List<LogInfo>(1);

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_ReadInterface), "Invalid CodeInfo");
            CodeInfo_ReadInterface info = cmd.Info as CodeInfo_ReadInterface;
            Debug.Assert(info != null, "Invalid CodeInfo");

            string scriptFile = StringEscaper.Preprocess(s, info.ScriptFile);
            string section = StringEscaper.Preprocess(s, info.Section);
            string key = StringEscaper.Preprocess(s, info.Key);

            Debug.Assert(scriptFile != null, $"{nameof(scriptFile)} != null");
            Debug.Assert(section != null, $"{nameof(section)} != null");
            Debug.Assert(key != null, $"{nameof(key)} != null");

            Script sc = Engine.GetScriptInstance(s, cmd, s.CurrentScript.RealPath, scriptFile, out _);

            if (!sc.Sections.ContainsKey(section))
                return LogInfo.LogErrorMessage(logs, $"Script [{scriptFile}] does not have section [{section}]");

            ScriptSection iface = sc.Sections[section];
            List<UIControl> uiCmds = iface.GetUICtrls(true);
            UIControl uiCmd = uiCmds.Find(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (uiCmd == null)
                return LogInfo.LogErrorMessage(logs, $"Interface control [{key}] does not exist");
            
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
                case InterfaceElement.ToolTip:
                    destStr = uiCmd.Info.ToolTip ?? string.Empty;
                    break;
                default:
                    throw new InternalException("Internal Logic Error at ReadInterface");
            }

            // Do not expand read values
            List<LogInfo> varLogs = Variables.SetVariable(s, info.DestVar, destStr, false, false, false);
            logs.AddRange(varLogs);

            return logs;
        }

        public static List<LogInfo> WriteInterface(EngineState s, CodeCommand cmd)
        { // WriteInterface,<Element>,<ScriptFile>,<Section>,<Key>,<Value>
            List<LogInfo> logs = new List<LogInfo>(2);

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_WriteInterface), "Invalid CodeInfo");
            CodeInfo_WriteInterface info = cmd.Info as CodeInfo_WriteInterface;
            Debug.Assert(info != null, "Invalid CodeInfo");

            string scriptFile = StringEscaper.Preprocess(s, info.ScriptFile);
            string section = StringEscaper.Preprocess(s, info.Section);
            string key = StringEscaper.Preprocess(s, info.Key);
            string finalValue = StringEscaper.Preprocess(s, info.Value);

            Debug.Assert(scriptFile != null, $"{nameof(scriptFile)} != null");
            Debug.Assert(section != null, $"{nameof(section)} != null");
            Debug.Assert(key != null, $"{nameof(key)} != null");
            Debug.Assert(finalValue != null, $"{nameof(finalValue)} != null");

            Script sc = Engine.GetScriptInstance(s, cmd, s.CurrentScript.RealPath, scriptFile, out _);

            if (!sc.Sections.ContainsKey(section))
                return LogInfo.LogErrorMessage(logs, $"Script [{scriptFile}] does not have section [{section}]");

            ScriptSection iface = sc.Sections[section];
            List<UIControl> uiCmds = iface.GetUICtrls(true);
            UIControl uiCmd = uiCmds.Find(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (uiCmd == null)
                return LogInfo.LogErrorMessage(logs, $"Interface control [{key}] does not exist");

            switch (info.Element)
            {
                case InterfaceElement.Text:
                    uiCmd.Text = finalValue;
                    break;
                case InterfaceElement.Visible:
                    {
                        bool visibility;
                        if (finalValue.Equals("1", StringComparison.Ordinal) || 
                            finalValue.Equals("True", StringComparison.OrdinalIgnoreCase))
                            visibility = true;
                        else if (finalValue.Equals("0", StringComparison.Ordinal) || 
                                 finalValue.Equals("False", StringComparison.OrdinalIgnoreCase))
                            visibility = false;
                        else
                            return LogInfo.LogErrorMessage(logs, $"[{finalValue}] is not a valid boolean value");

                        uiCmd.Visibility = visibility;
                    }
                    break;
                case InterfaceElement.PosX:
                    {
                        if (!NumberHelper.ParseInt32(finalValue, out int x))
                            return LogInfo.LogErrorMessage(logs, $"[{finalValue}] is not a valid integer");

                        uiCmd.Rect.X = x;
                    }
                    break;
                case InterfaceElement.PosY:
                    {
                        if (!NumberHelper.ParseInt32(finalValue, out int y))
                            return LogInfo.LogErrorMessage(logs, $"[{finalValue}] is not a valid integer");

                        uiCmd.Rect.Y = y;
                    }
                    break;
                case InterfaceElement.Width:
                    {
                        if (!NumberHelper.ParseInt32(finalValue, out int width))
                            return LogInfo.LogErrorMessage(logs, $"[{finalValue}] is not a valid integer");

                        uiCmd.Rect.Width = width;
                    }
                    break;
                case InterfaceElement.Height:
                    {
                        if (!NumberHelper.ParseInt32(finalValue, out int height))
                            return LogInfo.LogErrorMessage(logs, $"[{finalValue}] is not a valid integer");

                        uiCmd.Rect.Height = height;
                    }
                    break;
                case InterfaceElement.Value:
                    {
                        bool success = uiCmd.SetValue(finalValue, false, out List<LogInfo> varLogs);
                        logs.AddRange(varLogs);

                        if (success == false && varLogs.Count == 0)
                            return LogInfo.LogErrorMessage(logs, $"Writing [Value] to [{uiCmd.Type}] is not supported");
                    }
                    break;
                case InterfaceElement.ToolTip:
                    {
                        if (finalValue.Length == 0 || finalValue.Equals("NIL", StringComparison.OrdinalIgnoreCase))
                            uiCmd.Info.ToolTip = null; // Deletion
                        else
                            uiCmd.Info.ToolTip = finalValue; // Modify
                    }
                    break;
                default:
                    throw new InternalException("Internal Logic Error at WriteInterface");
            }

            // Update uiCmd into file
            uiCmd.Update();

            // Rerender Script
            Application.Current?.Dispatcher.Invoke(() =>
            { // Application.Current is null in unit test
                MainWindow w = Application.Current.MainWindow as MainWindow;
                if (w?.CurMainTree.Script.Equals(cmd.Addr.Script) == true)
                    w.DrawScript(cmd.Addr.Script);
            });

            return logs;
        }

        public static List<LogInfo> Message(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Message), "Invalid CodeInfo");
            CodeInfo_Message info = cmd.Info as CodeInfo_Message;
            Debug.Assert(info != null, "Invalid CodeInfo");

            string message = StringEscaper.Preprocess(s, info.Message);

            Debug.Assert(message != null, $"{nameof(message)} != null");

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
                    throw new InternalException("Internal Logic Error at Message");
            }

            System.Windows.Shell.TaskbarItemProgressState oldTaskbarItemProgressState = s.MainViewModel.TaskbarProgressState; // Save our progress state
            s.MainViewModel.TaskbarProgressState = System.Windows.Shell.TaskbarItemProgressState.Paused;

            if (info.Timeout == null)
            {
                MessageBox.Show(message, cmd.Addr.Script.Title, MessageBoxButton.OK, image);
            }
            else
            {
                string timeoutStr = StringEscaper.Preprocess(s, info.Timeout);
                
                if (NumberHelper.ParseInt32(timeoutStr, out int timeout) == false)
                    return LogInfo.LogErrorMessage(logs, $"[{timeoutStr}] is not a valid positive integer");
                if (timeout <= 0)
                    return LogInfo.LogErrorMessage(logs, $"Timeout must be a positive integer [{timeoutStr}]");

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    CustomMessageBox.Show(message, cmd.Addr.Script.Title, MessageBoxButton.OK, image, timeout);
                });
            }

            s.MainViewModel.TaskbarProgressState = oldTaskbarItemProgressState;

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

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Echo), "Invalid CodeInfo");
            CodeInfo_Echo info = cmd.Info as CodeInfo_Echo;
            Debug.Assert(info != null, "Invalid CodeInfo");
            
            string message = StringEscaper.Preprocess(s, info.Message);

            Debug.Assert(message != null, $"{nameof(message)} != null");

            s.MainViewModel.BuildEchoMessage = message;

            logs.Add(new LogInfo(info.Warn ? LogState.Warning : LogState.Success, message, cmd));

            return logs;
        }

        public static List<LogInfo> EchoFile(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_EchoFile), "Invalid CodeInfo");
            CodeInfo_EchoFile info = cmd.Info as CodeInfo_EchoFile;
            Debug.Assert(info != null, "Invalid CodeInfo");

            string srcFile = StringEscaper.Preprocess(s, info.SrcFile);

            Debug.Assert(srcFile != null, $"{nameof(srcFile)} != null");

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
                    // Create dummy script instance
                    FileHelper.WriteTextBOM(tempFile, Encoding.UTF8);
                    Script sc = cmd.Addr.Project.LoadScript(tempFile, tempFile, true, false);

                    // Encode binary file into script instance
                    string fileName = Path.GetFileName(srcFile);
                    EncodedFile.AttachFile(sc, "Folder", fileName, srcFile, EncodedFile.EncodeMode.ZLib);

                    // Read encoded text strings into memory
                    string txtStr;
                    Encoding encoding = FileHelper.DetectTextEncoding(tempFile);
                    using (StreamReader r = new StreamReader(tempFile, encoding))
                    {
                        txtStr = r.ReadToEnd();
                    }

                    string logStr = $"Encoded File [{srcFile}]\r\n{txtStr.Trim()}\r\n";
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

                string logStr = $"Encoded File [{srcFile}]\r\n{txtStr.Trim()}\r\n";
                s.MainViewModel.BuildEchoMessage = logStr;
                logs.Add(new LogInfo(info.Warn ? LogState.Warning : LogState.Success, logStr, cmd));
            }

            return logs;
        }

        public static List<LogInfo> UserInput(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_UserInput), "Invalid CodeInfo");
            CodeInfo_UserInput info = cmd.Info as CodeInfo_UserInput;
            Debug.Assert(info != null, "Invalid CodeInfo");

            UserInputType type = info.Type;
            switch (type)
            {
                case UserInputType.DirPath:
                case UserInputType.FilePath:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(UserInputInfo_DirFile), "Invalid UserInputInfo");
                        UserInputInfo_DirFile subInfo = info.SubInfo as UserInputInfo_DirFile;
                        Debug.Assert(subInfo != null, "Invalid UserInputInfo");

                        System.Windows.Shell.TaskbarItemProgressState oldTaskbarItemProgressState = s.MainViewModel.TaskbarProgressState; // Save our progress state
                        s.MainViewModel.TaskbarProgressState = System.Windows.Shell.TaskbarItemProgressState.Paused;

                        string initPath = StringEscaper.Preprocess(s, subInfo.InitPath);

                        Debug.Assert(initPath != null, $"{nameof(initPath)} != null");

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

                            string initDir = Path.GetDirectoryName(initPath);
                            if (initDir == null)
                                throw new InternalException("Internal Logic Error at UserInput");
                            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog
                            {
                                Filter = filter,
                                InitialDirectory = initDir,
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
                                MainWindow w = Application.Current.MainWindow as MainWindow;

                                if (dialog.ShowDialog(w) == true)
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

                        s.MainViewModel.TaskbarProgressState = oldTaskbarItemProgressState;

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

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_AddInterface), "Invalid CodeInfo");
            CodeInfo_AddInterface info = cmd.Info as CodeInfo_AddInterface;
            Debug.Assert(info != null, "Invalid CodeInfo");

            string scriptFile = StringEscaper.Preprocess(s, info.ScriptFile);
            string interfaceSection = StringEscaper.Preprocess(s, info.Interface);
            string prefix = StringEscaper.Preprocess(s, info.Prefix);

            Debug.Assert(scriptFile != null, $"{nameof(scriptFile)} != null");
            Debug.Assert(interfaceSection != null, $"{nameof(interfaceSection)} != null");
            Debug.Assert(prefix != null, $"{nameof(prefix)} != null");

            Script sc = Engine.GetScriptInstance(s, cmd, s.CurrentScript.RealPath, scriptFile, out _);
            if (sc.Sections.ContainsKey(interfaceSection))
            {
                List<UIControl> uiCtrls = null;
                try { uiCtrls = sc.Sections[interfaceSection].GetUICtrls(true); }
                catch { /* No [Interface] section, or unable to get List<UIControl> */ }

                if (uiCtrls != null)
                {
                    List<LogInfo> subLogs = s.Variables.UIControlToVariables(uiCtrls, prefix);
                    if (0 < subLogs.Count)
                    {
                        s.Logger.BuildWrite(s, new LogInfo(LogState.Info, $"Import variables from [{interfaceSection}]", cmd, s.CurDepth));
                        logs.AddRange(LogInfo.AddCommandDepth(subLogs, cmd, s.CurDepth + 1));
                        s.Logger.BuildWrite(s, subLogs);
                        s.Logger.BuildWrite(s, new LogInfo(LogState.Info, $"Imported {subLogs.Count} variables", cmd, s.CurDepth));
                    }
                }
            }

            return logs;
        }
    }
}
