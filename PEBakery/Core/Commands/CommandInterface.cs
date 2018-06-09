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

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Diagnostics;
using System.Collections.Generic;
using PEBakery.Helper;
using PEBakery.WPF;
using PEBakery.WPF.Controls;
using Ookii.Dialogs.Wpf;

namespace PEBakery.Core.Commands
{
    public static class CommandInterface
    {
        public static List<LogInfo> Visible(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>(1);

            CodeInfo_Visible info = cmd.Info.Cast<CodeInfo_Visible>();

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
            UIControl uiCtrl = uiCtrls.Find(x => x.Key.Equals(info.UIControlKey, StringComparison.OrdinalIgnoreCase));
            if (uiCtrl == null)
                return LogInfo.LogErrorMessage(logs, $"Cannot find interface control [{info.UIControlKey}] in section [{ifaceSecName}]");

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

            logs.Add(new LogInfo(LogState.Success, $"Interface control [{info.UIControlKey}]'s visibility set to [{visibility}]"));

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

                prepArgs.Add((info.UIControlKey, visibility, subCmd));
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

        private static (bool, string) InternalReadInterface(UIControl uiCtrl, InterfaceElement element)
        {
            string destStr;
            switch (element)
            {
                #region General
                case InterfaceElement.Text:
                    destStr = uiCtrl.Text;
                    break;
                case InterfaceElement.Visible:
                    destStr = uiCtrl.Visibility.ToString();
                    break;
                case InterfaceElement.PosX:
                    destStr = ((int)uiCtrl.Rect.X).ToString();
                    break;
                case InterfaceElement.PosY:
                    destStr = ((int)uiCtrl.Rect.Y).ToString();
                    break;
                case InterfaceElement.Width:
                    destStr = ((int)uiCtrl.Rect.Width).ToString();
                    break;
                case InterfaceElement.Height:
                    destStr = ((int)uiCtrl.Rect.Height).ToString();
                    break;
                case InterfaceElement.Value:
                    destStr = uiCtrl.GetValue();
                    if (destStr == null)
                        return (false, $"Reading [{element}] from [{uiCtrl.Type}] is not supported");
                    break;
                case InterfaceElement.ToolTip:
                    destStr = uiCtrl.Info.ToolTip ?? string.Empty;
                    break;
                #endregion
                #region TextLabel, Bevel
                case InterfaceElement.FontSize:
                    {
                        switch (uiCtrl.Type)
                        {
                            case UIControlType.TextLabel:
                                {
                                    UIInfo_TextLabel subInfo = uiCtrl.Info.Cast<UIInfo_TextLabel>();

                                    destStr = subInfo.FontSize.ToString();
                                }
                                break;
                            case UIControlType.Bevel:
                                {
                                    UIInfo_Bevel subInfo = uiCtrl.Info.Cast<UIInfo_Bevel>();

                                    destStr = subInfo.FontSize.ToString();
                                }
                                break;
                            default:
                                return (false, $"Reading [{element}] from [{uiCtrl.Type}] is not supported");
                        }
                    }
                    break;
                case InterfaceElement.FontWeight:
                    {
                        switch (uiCtrl.Type)
                        {
                            case UIControlType.TextLabel:
                                {
                                    UIInfo_TextLabel subInfo = uiCtrl.Info.Cast<UIInfo_TextLabel>();

                                    destStr = subInfo.FontWeight.ToString();
                                }
                                break;
                            case UIControlType.Bevel:
                                {
                                    UIInfo_Bevel subInfo = uiCtrl.Info.Cast<UIInfo_Bevel>();

                                    destStr = subInfo.FontWeight == null ? "None" : subInfo.FontWeight.ToString();
                                }
                                break;
                            default:
                                return (false, $"Reading [{element}] from [{uiCtrl.Type}] is not supported");
                        }
                    }
                    break;
                case InterfaceElement.FontStyle:
                    {
                        switch (uiCtrl.Type)
                        {
                            case UIControlType.TextLabel:
                                {
                                    UIInfo_TextLabel subInfo = uiCtrl.Info.Cast<UIInfo_TextLabel>();

                                    destStr = subInfo.FontStyle == null ? "None" : subInfo.FontStyle.ToString();
                                    break;
                                }
                            case UIControlType.Bevel:
                                {
                                    UIInfo_Bevel subInfo = uiCtrl.Info.Cast<UIInfo_Bevel>();

                                    destStr = subInfo.FontStyle == null ? "None" : subInfo.FontStyle.ToString();
                                    break;
                                }
                            default:
                                return (false, $"Reading [{element}] from [{uiCtrl.Type}] is not supported");
                        }
                        break;
                    }
                #endregion
                #region NumberBox
                case InterfaceElement.NumberMin:
                    {
                        if (uiCtrl.Type != UIControlType.NumberBox)
                            return (false, $"Reading [{element}] from [{uiCtrl.Type}] is not supported");

                        UIInfo_NumberBox subInfo = uiCtrl.Info.Cast<UIInfo_NumberBox>();

                        destStr = subInfo.Min.ToString();
                    }
                    break;
                case InterfaceElement.NumberMax:
                    {
                        if (uiCtrl.Type != UIControlType.NumberBox)
                            return (false, $"Reading [{element}] from [{uiCtrl.Type}] is not supported");

                        UIInfo_NumberBox subInfo = uiCtrl.Info.Cast<UIInfo_NumberBox>();

                        destStr = subInfo.Max.ToString();
                    }
                    break;
                case InterfaceElement.NumberTick:
                    {
                        if (uiCtrl.Type != UIControlType.NumberBox)
                            return (false, $"Reading [{element}] from [{uiCtrl.Type}] is not supported");

                        UIInfo_NumberBox subInfo = uiCtrl.Info.Cast<UIInfo_NumberBox>();

                        destStr = subInfo.Tick.ToString();
                    }
                    break;
                #endregion
                #region Url - Image, WebLabel
                case InterfaceElement.Url:
                    {
                        switch (uiCtrl.Type)
                        {
                            case UIControlType.Image:
                                {
                                    UIInfo_Image subInfo = uiCtrl.Info.Cast<UIInfo_Image>();

                                    destStr = subInfo.Url ?? string.Empty;
                                }
                                break;
                            case UIControlType.WebLabel:
                                {
                                    UIInfo_WebLabel subInfo = uiCtrl.Info.Cast<UIInfo_WebLabel>();

                                    destStr = subInfo.Url;
                                }
                                break;
                            default:
                                return (false, $"Reading [{element}] from [{uiCtrl.Type}] is not supported");
                        }
                    }
                    break;
                #endregion
                #region Items - ComboBox, RadioGroup
                case InterfaceElement.Items:
                    {
                        switch (uiCtrl.Type)
                        {
                            case UIControlType.ComboBox:
                                {
                                    UIInfo_ComboBox subInfo = uiCtrl.Info.Cast<UIInfo_ComboBox>();

                                    destStr = StringHelper.ConcatStrings(subInfo.Items, UIControl.ItemSeperatorStr);
                                }
                                break;
                            case UIControlType.RadioGroup:
                                {
                                    UIInfo_RadioGroup subInfo = uiCtrl.Info.Cast<UIInfo_RadioGroup>();

                                    destStr = StringHelper.ConcatStrings(subInfo.Items, UIControl.ItemSeperatorStr);
                                }
                                break;
                            default:
                                return (false, $"Reading [{element}] from [{uiCtrl.Type}] is not supported");
                        }
                    }
                    break;
                #endregion
                #region Run - CheckBox, ComboBox, Button, RadioButton, RadioGroup
                case InterfaceElement.SectionName:
                    {
                        switch (uiCtrl.Type)
                        {
                            case UIControlType.CheckBox:
                                {
                                    UIInfo_CheckBox subInfo = uiCtrl.Info.Cast<UIInfo_CheckBox>();

                                    destStr = subInfo.SectionName ?? string.Empty;
                                }
                                break;
                            case UIControlType.ComboBox:
                                {
                                    UIInfo_ComboBox subInfo = uiCtrl.Info.Cast<UIInfo_ComboBox>();

                                    destStr = subInfo.SectionName ?? string.Empty;
                                }
                                break;
                            case UIControlType.Button:
                                {
                                    UIInfo_Button subInfo = uiCtrl.Info.Cast<UIInfo_Button>();

                                    destStr = subInfo.SectionName ?? string.Empty;
                                }
                                break;
                            case UIControlType.RadioButton:
                                {
                                    UIInfo_RadioButton subInfo = uiCtrl.Info.Cast<UIInfo_RadioButton>();

                                    destStr = subInfo.SectionName ?? string.Empty;
                                }
                                break;
                            case UIControlType.RadioGroup:
                                {
                                    UIInfo_RadioGroup subInfo = uiCtrl.Info.Cast<UIInfo_RadioGroup>();

                                    destStr = subInfo.SectionName ?? string.Empty;
                                }
                                break;
                            default:
                                return (false, $"Reading [{element}] from [{uiCtrl.Type}] is not supported");
                        }
                    }
                    break;
                case InterfaceElement.HideProgress:
                    {
                        switch (uiCtrl.Type)
                        {
                            case UIControlType.CheckBox:
                                {
                                    Debug.Assert(uiCtrl.Info.GetType() == typeof(UIInfo_CheckBox), "Invalid UIInfo");
                                    UIInfo_CheckBox subInfo = uiCtrl.Info as UIInfo_CheckBox;
                                    Debug.Assert(subInfo != null, "Invalid UIInfo");

                                    destStr = subInfo.SectionName == null ? "None" : subInfo.HideProgress.ToString();
                                }
                                break;
                            case UIControlType.ComboBox:
                                {
                                    Debug.Assert(uiCtrl.Info.GetType() == typeof(UIInfo_ComboBox), "Invalid UIInfo");
                                    UIInfo_ComboBox subInfo = uiCtrl.Info as UIInfo_ComboBox;
                                    Debug.Assert(subInfo != null, "Invalid UIInfo");

                                    destStr = subInfo.SectionName == null ? "None" : subInfo.HideProgress.ToString();
                                }
                                break;
                            case UIControlType.Button:
                                {
                                    Debug.Assert(uiCtrl.Info.GetType() == typeof(UIInfo_Button), "Invalid UIInfo");
                                    UIInfo_Button subInfo = uiCtrl.Info as UIInfo_Button;
                                    Debug.Assert(subInfo != null, "Invalid UIInfo");

                                    destStr = subInfo.SectionName == null ? "None" : subInfo.HideProgress.ToString();
                                }
                                break;
                            case UIControlType.RadioButton:
                                {
                                    UIInfo_RadioButton subInfo = uiCtrl.Info.Cast<UIInfo_RadioButton>();

                                    destStr = subInfo.SectionName == null ? "None" : subInfo.HideProgress.ToString();
                                }
                                break;
                            case UIControlType.RadioGroup:
                                {
                                    UIInfo_RadioGroup subInfo = uiCtrl.Info.Cast<UIInfo_RadioGroup>();

                                    destStr = subInfo.SectionName == null ? "None" : subInfo.HideProgress.ToString();
                                }
                                break;
                            default:
                                return (false, $"Reading [{element}] from [{uiCtrl.Type}] is not supported");
                        }
                    }
                    break;
                #endregion
                #region Error
                default:
                    throw new InternalException("Internal Logic Error at InternalReadInterface");
                    #endregion
            }

            return (true, destStr);
        }

        public static List<LogInfo> ReadInterface(EngineState s, CodeCommand cmd)
        { // ReadInterface,<Element>,<ScriptFile>,<Section>,<Key>,<DestVar>
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_ReadInterface info = cmd.Info.Cast<CodeInfo_ReadInterface>();

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
            List<UIControl> uiCtrls = iface.GetUICtrls(true);
            UIControl uiCtrl = uiCtrls.Find(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (uiCtrl == null)
                return LogInfo.LogErrorMessage(logs, $"Interface control [{key}] does not exist in section [{section}] of [{scriptFile}]");
            logs.Add(new LogInfo(LogState.Success, $"Interface control [{key}] found in section [{section}] of [{scriptFile}]"));

            // Read value from uiCtrl
            (bool success, string destStr) = InternalReadInterface(uiCtrl, info.Element);
            if (!success) // Operation failed, destStr contains error message
                return LogInfo.LogErrorMessage(logs, destStr);

            // Do not expand read values
            List<LogInfo> varLogs = Variables.SetVariable(s, info.DestVar, destStr, false, false, false);
            logs.AddRange(varLogs);

            return logs;
        }

        public static List<LogInfo> ReadInterfaceOp(EngineState s, CodeCommand cmd)
        { // ReadInterface,<Element>,<ScriptFile>,<Section>,<Key>,<DestVar>
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_ReadInterfaceOp infoOp = cmd.Info.Cast<CodeInfo_ReadInterfaceOp>();

            CodeInfo_ReadInterface firstInfo = infoOp.Infos[0];
            string scriptFile = StringEscaper.Preprocess(s, firstInfo.ScriptFile);
            string section = StringEscaper.Preprocess(s, firstInfo.Section);

            Debug.Assert(scriptFile != null, $"{nameof(scriptFile)} != null");
            Debug.Assert(section != null, $"{nameof(section)} != null");

            Script sc = Engine.GetScriptInstance(s, cmd, s.CurrentScript.RealPath, scriptFile, out _);
            if (!sc.Sections.ContainsKey(section))
                return LogInfo.LogErrorMessage(logs, $"Script [{scriptFile}] does not have section [{section}]");

            ScriptSection iface = sc.Sections[section];
            List<UIControl> uiCtrls = iface.GetUICtrls(true);

            var targets = new List<(UIControl, CodeInfo_ReadInterface, CodeCommand)>(infoOp.Cmds.Count);
            foreach (CodeCommand subCmd in infoOp.Cmds)
            {
                CodeInfo_ReadInterface info = subCmd.Info.Cast<CodeInfo_ReadInterface>();

                string key = StringEscaper.Preprocess(s, info.Key);

                UIControl uiCtrl = uiCtrls.FirstOrDefault(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
                if (uiCtrl == null)
                {
                    logs.Add(new LogInfo(LogState.Error, $"Interface control [{key}] does not exist", subCmd));
                    continue;
                }

                targets.Add((uiCtrl, info, subCmd));
            }

            int successCount = 0;
            foreach ((UIControl uiCtrl, CodeInfo_ReadInterface info, CodeCommand subCmd) in targets)
            {
                (bool success, string destStr) = InternalReadInterface(uiCtrl, info.Element);
                if (success)
                {
                    List<LogInfo> varLogs = Variables.SetVariable(s, info.DestVar, destStr, false, false, false);
                    LogInfo.AddCommand(varLogs, subCmd);
                    logs.AddRange(varLogs);

                    successCount += 1;
                }
                else
                { // Operation failed, destStr contains error message
                    logs.Add(new LogInfo(LogState.Error, destStr));
                }
            }

            if (1 < successCount)
                logs.Add(new LogInfo(LogState.Success, $"Read [{successCount}] values from section [{section}] of [{scriptFile}]"));
            else
                logs.Add(new LogInfo(LogState.Success, $"Read [{successCount}] value from section [{section}] of [{scriptFile}]"));
            return logs;
        }

        // ReSharper disable once UnusedMethodReturnValue.Local
        private static (bool, List<LogInfo>) InternalWriteInterface(UIControl uiCtrl, InterfaceElement element, string finalValue)
        {
            List<LogInfo> logs = new List<LogInfo>();

            (bool, List<LogInfo>) ReturnErrorLog(string message)
            {
                logs.Add(new LogInfo(LogState.Error, message));
                return (false, logs);
            }

            switch (element)
            {
                #region General
                case InterfaceElement.Text:
                    uiCtrl.Text = finalValue;
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
                            return ReturnErrorLog($"[{finalValue}] is not a valid boolean value");

                        uiCtrl.Visibility = visibility;
                    }
                    break;
                case InterfaceElement.PosX:
                    {
                        if (!NumberHelper.ParseInt32(finalValue, out int x))
                            return ReturnErrorLog($"[{finalValue}] is not a valid integer");

                        uiCtrl.Rect.X = x;
                    }
                    break;
                case InterfaceElement.PosY:
                    {
                        if (!NumberHelper.ParseInt32(finalValue, out int y))
                            return ReturnErrorLog($"[{finalValue}] is not a valid integer");

                        uiCtrl.Rect.Y = y;
                    }
                    break;
                case InterfaceElement.Width:
                    {
                        if (!NumberHelper.ParseInt32(finalValue, out int width) || width < 0)
                            return ReturnErrorLog($"[{finalValue}] is not a valid positive integer");

                        uiCtrl.Rect.Width = width;
                    }
                    break;
                case InterfaceElement.Height:
                    {
                        if (!NumberHelper.ParseInt32(finalValue, out int height) || height < 0)
                            return ReturnErrorLog($"[{finalValue}] is not a valid positive integer");

                        uiCtrl.Rect.Height = height;
                    }
                    break;
                case InterfaceElement.Value:
                    {
                        bool success = uiCtrl.SetValue(finalValue, false, out List<LogInfo> varLogs);
                        logs.AddRange(varLogs);

                        if (success == false && varLogs.Count == 0)
                            return ReturnErrorLog($"Writing [{element}] to [{uiCtrl.Type}] is not supported");
                    }
                    break;
                case InterfaceElement.ToolTip:
                    {
                        if (finalValue.Length == 0 || finalValue.Equals("NIL", StringComparison.OrdinalIgnoreCase))
                            uiCtrl.Info.ToolTip = null; // Deletion
                        else
                            uiCtrl.Info.ToolTip = finalValue; // Modify
                    }
                    break;
                #endregion
                #region TextLabel, Bevel
                case InterfaceElement.FontSize:
                    {
                        if (!NumberHelper.ParseInt32(finalValue, out int fontSize) || fontSize < 0)
                            return ReturnErrorLog($"[{finalValue}] is not a valid positive integer");

                        switch (uiCtrl.Type)
                        {
                            case UIControlType.TextLabel:
                                {
                                    UIInfo_TextLabel subInfo = uiCtrl.Info.Cast<UIInfo_TextLabel>();

                                    subInfo.FontSize = fontSize;
                                }
                                break;
                            case UIControlType.Bevel:
                                {
                                    UIInfo_Bevel subInfo = uiCtrl.Info.Cast<UIInfo_Bevel>();

                                    subInfo.FontSize = fontSize;
                                }
                                break;
                            default:
                                return ReturnErrorLog($"Writing [{element}] to [{uiCtrl.Type}] is not supported");
                        }
                    }
                    break;
                case InterfaceElement.FontWeight:
                    switch (uiCtrl.Type)
                    {
                        case UIControlType.TextLabel:
                            {
                                UIInfo_TextLabel subInfo = uiCtrl.Info.Cast<UIInfo_TextLabel>();

                                UIFontWeight? weight = UIParser.ParseUIFontWeight(finalValue);
                                if (weight == null)
                                    throw new InvalidCommandException($"Invalid FontWeight [{finalValue}]");
                                subInfo.FontWeight = (UIFontWeight)weight;
                            }
                            break;
                        case UIControlType.Bevel:
                            {
                                UIInfo_Bevel subInfo = uiCtrl.Info.Cast<UIInfo_Bevel>();

                                UIFontWeight? weight = UIParser.ParseUIFontWeight(finalValue);
                                subInfo.FontWeight = weight ?? throw new InvalidCommandException($"Invalid FontWeight [{finalValue}]");
                            }
                            break;
                        default:
                            return ReturnErrorLog($"Writing [{element}] to [{uiCtrl.Type}] is not supported");
                    }
                    break;
                case InterfaceElement.FontStyle:
                    switch (uiCtrl.Type)
                    {
                        case UIControlType.TextLabel:
                            {
                                UIInfo_TextLabel subInfo = uiCtrl.Info.Cast<UIInfo_TextLabel>();

                                UIFontStyle? style = UIParser.ParseUIFontStyle(finalValue);
                                subInfo.FontStyle = style ?? throw new InvalidCommandException($"Invalid FontStyle [{finalValue}]");
                            }
                            break;
                        case UIControlType.Bevel:
                            {
                                UIInfo_Bevel subInfo = uiCtrl.Info.Cast<UIInfo_Bevel>();

                                UIFontStyle? style = UIParser.ParseUIFontStyle(finalValue);
                                subInfo.FontStyle = style ?? throw new InvalidCommandException($"Invalid FontStyle [{finalValue}]");
                            }
                            break;
                        default:
                            return ReturnErrorLog($"Writing [{element}] to [{uiCtrl.Type}] is not supported");
                    }
                    break;
                #endregion
                #region NumberBox
                case InterfaceElement.NumberMin:
                    {
                        if (uiCtrl.Type != UIControlType.NumberBox)
                            return ReturnErrorLog($"Writing [{element}] to [{uiCtrl.Type}] is not supported");

                        if (!NumberHelper.ParseInt32(finalValue, out int min) || min < 0)
                            return ReturnErrorLog($"[{finalValue}] is not a valid positive integer");

                        UIInfo_NumberBox subInfo = uiCtrl.Info.Cast<UIInfo_NumberBox>();

                        subInfo.Min = min;
                        if (subInfo.Value < min)
                            subInfo.Value = min;
                    }
                    break;
                case InterfaceElement.NumberMax:
                    {
                        if (uiCtrl.Type != UIControlType.NumberBox)
                            return ReturnErrorLog($"Writing [{element}] to [{uiCtrl.Type}] is not supported");

                        if (!NumberHelper.ParseInt32(finalValue, out int max) || max < 0)
                            return ReturnErrorLog($"[{finalValue}] is not a valid positive integer");

                        UIInfo_NumberBox subInfo = uiCtrl.Info.Cast<UIInfo_NumberBox>();

                        subInfo.Max = max;
                        if (max < subInfo.Value)
                            subInfo.Value = max;
                    }
                    break;
                case InterfaceElement.NumberTick:
                    {
                        if (uiCtrl.Type != UIControlType.NumberBox)
                            return ReturnErrorLog($"Writing [{element}] to [{uiCtrl.Type}] is not supported");

                        if (!NumberHelper.ParseInt32(finalValue, out int tick) || tick < 0)
                            return ReturnErrorLog($"[{finalValue}] is not a valid positive integer");

                        UIInfo_NumberBox subInfo = uiCtrl.Info.Cast<UIInfo_NumberBox>();

                        subInfo.Tick = tick;
                    }
                    break;
                #endregion
                #region Url - Image, WebLabel
                case InterfaceElement.Url:
                    switch (uiCtrl.Type)
                    {
                        case UIControlType.Image:
                            {
                                UIInfo_Image subInfo = uiCtrl.Info.Cast<UIInfo_Image>();

                                if (finalValue.Length == 0 || finalValue.Equals("NIL", StringComparison.OrdinalIgnoreCase))
                                    subInfo.Url = null;
                                else
                                    subInfo.Url = finalValue;
                            }
                            break;
                        case UIControlType.WebLabel:
                            {
                                UIInfo_WebLabel subInfo = uiCtrl.Info.Cast<UIInfo_WebLabel>();

                                subInfo.Url = finalValue;
                            }
                            break;
                        default:
                            return ReturnErrorLog($"Writing [{element}] to [{uiCtrl.Type}] is not supported");
                    }
                    break;
                #endregion
                #region Items - ComboBox, RadioGroup
                case InterfaceElement.Items:
                    {
                        string[] newItems = finalValue.Split(UIControl.ItemSeperatorChar);

                        switch (uiCtrl.Type)
                        {
                            case UIControlType.ComboBox:
                                {
                                    UIInfo_ComboBox subInfo = uiCtrl.Info.Cast<UIInfo_ComboBox>();

                                    subInfo.Items = newItems.ToList();
                                    if (newItems.Length == 0)
                                        uiCtrl.Text = string.Empty;
                                    else if (!newItems.Contains(uiCtrl.Text, StringComparer.OrdinalIgnoreCase))
                                        uiCtrl.Text = subInfo.Items[0];
                                }
                                break;
                            case UIControlType.RadioGroup:
                                {
                                    UIInfo_RadioGroup subInfo = uiCtrl.Info.Cast<UIInfo_RadioGroup>();

                                    subInfo.Items = newItems.ToList();
                                    if (newItems.Length == 0 || newItems.Length <= subInfo.Selected)
                                        subInfo.Selected = 0;
                                    else if (!newItems.Contains(newItems[subInfo.Selected], StringComparer.OrdinalIgnoreCase))
                                        subInfo.Selected = 0;
                                }
                                break;
                            default:
                                return ReturnErrorLog($"Writing [{element}] to [{uiCtrl.Type}] is not supported");
                        }
                    }
                    break;
                #endregion
                #region Run - CheckBox, ComboBox, Button, RadioButton, RadioGroup
                case InterfaceElement.SectionName:
                    {
                        string sectionName;
                        if (finalValue.Length == 0 || finalValue.Equals("NIL", StringComparison.OrdinalIgnoreCase))
                            sectionName = null;
                        else
                            sectionName = finalValue;

                        switch (uiCtrl.Type)
                        {
                            case UIControlType.CheckBox:
                                {
                                    UIInfo_CheckBox subInfo = uiCtrl.Info.Cast<UIInfo_CheckBox>();

                                    subInfo.SectionName = sectionName;
                                }
                                break;
                            case UIControlType.ComboBox:
                                {
                                    UIInfo_ComboBox subInfo = uiCtrl.Info.Cast<UIInfo_ComboBox>();

                                    subInfo.SectionName = sectionName;
                                }
                                break;
                            case UIControlType.Button:
                                {
                                    UIInfo_Button subInfo = uiCtrl.Info.Cast<UIInfo_Button>();

                                    if (sectionName == null)
                                        return ReturnErrorLog("Cannot delete [SectionName] and [HideProgress] of [Button] UIControl");

                                    subInfo.SectionName = sectionName;
                                }
                                break;
                            case UIControlType.RadioButton:
                                {
                                    UIInfo_RadioButton subInfo = uiCtrl.Info.Cast<UIInfo_RadioButton>();

                                    subInfo.SectionName = sectionName;
                                }
                                break;
                            case UIControlType.RadioGroup:
                                {
                                    UIInfo_RadioGroup subInfo = uiCtrl.Info.Cast<UIInfo_RadioGroup>();

                                    subInfo.SectionName = sectionName;
                                }
                                break;
                            default:
                                return ReturnErrorLog($"Writing [{element}] to [{uiCtrl.Type}] is not supported");
                        }
                    }
                    break;
                case InterfaceElement.HideProgress:
                    {
                        bool? newValue;
                        if (finalValue.Length == 0 ||
                            finalValue.Equals("None", StringComparison.OrdinalIgnoreCase) ||
                            finalValue.Equals("NIL", StringComparison.OrdinalIgnoreCase))
                            newValue = null;
                        else if (finalValue.Equals("True", StringComparison.OrdinalIgnoreCase))
                            newValue = true;
                        else if (finalValue.Equals("False", StringComparison.OrdinalIgnoreCase))
                            newValue = false;
                        else
                            return ReturnErrorLog($"[{finalValue}] is not a valid boolean value");

                        switch (uiCtrl.Type)
                        {
                            case UIControlType.CheckBox:
                                {
                                    Debug.Assert(uiCtrl.Info.GetType() == typeof(UIInfo_CheckBox), "Invalid UIInfo");
                                    UIInfo_CheckBox subInfo = uiCtrl.Info as UIInfo_CheckBox;
                                    Debug.Assert(subInfo != null, "Invalid UIInfo");

                                    if (newValue == null)
                                        subInfo.SectionName = null;
                                    else if (subInfo.SectionName != null)
                                        subInfo.HideProgress = (bool)newValue;
                                    else
                                        return ReturnErrorLog("Please set [SectionName] first before setting [HideProgress]");
                                }
                                break;
                            case UIControlType.ComboBox:
                                {
                                    Debug.Assert(uiCtrl.Info.GetType() == typeof(UIInfo_ComboBox), "Invalid UIInfo");
                                    UIInfo_ComboBox subInfo = uiCtrl.Info as UIInfo_ComboBox;
                                    Debug.Assert(subInfo != null, "Invalid UIInfo");

                                    if (newValue == null)
                                        subInfo.SectionName = null;
                                    else if (subInfo.SectionName != null)
                                        subInfo.HideProgress = (bool)newValue;
                                    else
                                        return ReturnErrorLog("Please set [SectionName] first before setting [HideProgress]");
                                }
                                break;
                            case UIControlType.Button:
                                {
                                    Debug.Assert(uiCtrl.Info.GetType() == typeof(UIInfo_Button), "Invalid UIInfo");
                                    UIInfo_Button subInfo = uiCtrl.Info as UIInfo_Button;
                                    Debug.Assert(subInfo != null, "Invalid UIInfo");

                                    if (newValue == null)
                                        return ReturnErrorLog("Cannot delete [SectionName] and [HideProgress] of [Button] UIControl");

                                    subInfo.HideProgress = (bool)newValue;
                                }
                                break;
                            case UIControlType.RadioButton:
                                {
                                    Debug.Assert(uiCtrl.Info.GetType() == typeof(UIInfo_RadioButton), "Invalid UIInfo");
                                    UIInfo_RadioButton subInfo = uiCtrl.Info as UIInfo_RadioButton;
                                    Debug.Assert(subInfo != null, "Invalid UIInfo");

                                    if (newValue == null)
                                        subInfo.SectionName = null;
                                    else if (subInfo.SectionName != null)
                                        subInfo.HideProgress = (bool)newValue;
                                    else
                                        return ReturnErrorLog("Please set [SectionName] first before setting [HideProgress]");
                                }
                                break;
                            case UIControlType.RadioGroup:
                                {
                                    Debug.Assert(uiCtrl.Info.GetType() == typeof(UIInfo_RadioGroup), "Invalid UIInfo");
                                    UIInfo_RadioGroup subInfo = uiCtrl.Info as UIInfo_RadioGroup;
                                    Debug.Assert(subInfo != null, "Invalid UIInfo");

                                    if (newValue == null)
                                        subInfo.SectionName = null;
                                    else if (subInfo.SectionName != null)
                                        subInfo.HideProgress = (bool)newValue;
                                    else
                                        return ReturnErrorLog("Please set [SectionName] first before setting [HideProgress]");
                                }
                                break;
                            default:
                                return ReturnErrorLog($"Writing [{element}] to [{uiCtrl.Type}] is not supported");
                        }
                    }
                    break;
                #endregion
                #region Error
                default:
                    throw new InternalException("Internal Logic Error at WriteInterface");
                    #endregion
            }

            logs.Add(new LogInfo(LogState.Success, $"[{uiCtrl.Key}]'s [{element}] is updated to [{finalValue}]"));
            return (true, logs);
        }

        public static List<LogInfo> WriteInterface(EngineState s, CodeCommand cmd)
        { // WriteInterface,<Element>,<ScriptFile>,<Section>,<Key>,<Value>
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_WriteInterface info = cmd.Info.Cast<CodeInfo_WriteInterface>();

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
            List<UIControl> uiCtrls = iface.GetUICtrls(true);
            UIControl uiCtrl = uiCtrls.Find(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (uiCtrl == null)
                return LogInfo.LogErrorMessage(logs, $"Interface control [{key}] does not exist in section [{section}] of [{scriptFile}]");
            logs.Add(new LogInfo(LogState.Success, $"Interface control [{key}] found in section [{section}] of [{scriptFile}]"));

            // Write value to uiCtrl
            (_, List<LogInfo> resultLogs) = InternalWriteInterface(uiCtrl, info.Element, finalValue);
            logs.AddRange(resultLogs);

            // Update uiCtrl into file
            uiCtrl.Update();

            // Rerender Script
            Application.Current?.Dispatcher.Invoke(() =>
            { // Application.Current is null in unit test
                if (!(Application.Current.MainWindow is MainWindow w))
                    return;
                if (w.CurMainTree.Script.Equals(cmd.Addr.Script))
                    w.DrawScript(cmd.Addr.Script);
            });

            return logs;
        }

        public static List<LogInfo> WriteInterfaceOp(EngineState s, CodeCommand cmd)
        { // WriteInterface,<Element>,<ScriptFile>,<Section>,<Key>,<DestVar>
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_WriteInterfaceOp infoOp = cmd.Info.Cast<CodeInfo_WriteInterfaceOp>();

            CodeInfo_WriteInterface firstInfo = infoOp.Infos[0];
            string scriptFile = StringEscaper.Preprocess(s, firstInfo.ScriptFile);
            string section = StringEscaper.Preprocess(s, firstInfo.Section);

            Debug.Assert(scriptFile != null, $"{nameof(scriptFile)} != null");
            Debug.Assert(section != null, $"{nameof(section)} != null");

            Script sc = Engine.GetScriptInstance(s, cmd, s.CurrentScript.RealPath, scriptFile, out _);
            if (!sc.Sections.ContainsKey(section))
                return LogInfo.LogErrorMessage(logs, $"Script [{scriptFile}] does not have section [{section}]");

            ScriptSection iface = sc.Sections[section];
            List<UIControl> uiCtrls = iface.GetUICtrls(true);

            var targets = new List<(UIControl, InterfaceElement, string, CodeCommand)>(infoOp.Cmds.Count);
            foreach (CodeCommand subCmd in infoOp.Cmds)
            {
                CodeInfo_WriteInterface info = subCmd.Info.Cast<CodeInfo_WriteInterface>();

                string key = StringEscaper.Preprocess(s, info.Key);
                string finalValue = StringEscaper.Preprocess(s, info.Value);

                UIControl uiCtrl = uiCtrls.FirstOrDefault(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
                if (uiCtrl == null)
                {
                    logs.Add(new LogInfo(LogState.Error, $"Interface control [{key}] does not exist"));
                    continue;
                }

                targets.Add((uiCtrl, info.Element, finalValue, subCmd));
            }

            List<UIControl> updatedUICtrls = new List<UIControl>(targets.Count);
            foreach ((UIControl uiCtrl, InterfaceElement element, string finalValue, CodeCommand subCmd) in targets)
            {
                (bool success, List<LogInfo> resultLogs) = InternalWriteInterface(uiCtrl, element, finalValue);
                LogInfo.AddCommand(resultLogs, subCmd);
                logs.AddRange(resultLogs);

                if (success)
                    updatedUICtrls.Add(uiCtrl);
            }

            // Update uiCtrl into file
            UIControl.Update(updatedUICtrls);

            // Rerender Script
            Application.Current?.Dispatcher.Invoke(() =>
            { // Application.Current is null in unit test
                if (!(Application.Current.MainWindow is MainWindow w))
                    return;
                if (w.CurMainTree.Script.Equals(cmd.Addr.Script))
                    w.DrawScript(cmd.Addr.Script);
            });

            if (1 < updatedUICtrls.Count)
                logs.Add(new LogInfo(LogState.Success, $"Wrote [{updatedUICtrls.Count}] values from section [{section}] of [{scriptFile}]"));
            else
                logs.Add(new LogInfo(LogState.Success, $"Wrote [{updatedUICtrls.Count}] value from section [{section}] of [{scriptFile}]"));
            return logs;
        }

        public static List<LogInfo> Message(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_Message info = cmd.Info.Cast<CodeInfo_Message>();

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

            CodeInfo_Echo info = cmd.Info.Cast<CodeInfo_Echo>();

            string message = StringEscaper.Preprocess(s, info.Message);

            Debug.Assert(message != null, $"{nameof(message)} != null");

            s.MainViewModel.BuildEchoMessage = message;

            logs.Add(new LogInfo(info.Warn ? LogState.Warning : LogState.Success, message, cmd));

            return logs;
        }

        public static List<LogInfo> EchoFile(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_EchoFile info = cmd.Info.Cast<CodeInfo_EchoFile>();

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
                    FileHelper.WriteTextBom(tempFile, Encoding.UTF8);
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

            CodeInfo_UserInput info = cmd.Info.Cast<CodeInfo_UserInput>();

            UserInputType type = info.Type;
            switch (type)
            {
                case UserInputType.DirPath:
                case UserInputType.FilePath:
                    {
                        UserInputInfo_DirFile subInfo = info.SubInfo.Cast<UserInputInfo_DirFile>();

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
                            VistaFolderBrowserDialog dialog = new VistaFolderBrowserDialog
                            {
                                SelectedPath = initPath,
                            };

                            bool failure = false;
                            if (dialog.ShowDialog() == true)
                            {
                                selectedPath = dialog.SelectedPath;
                                logs.Add(new LogInfo(LogState.Success, $"Directory path [{selectedPath}] was chosen by user"));
                            }
                            else
                            {
                                logs.Add(new LogInfo(LogState.Error, "Directory path was not chosen by user"));
                                failure = true;
                            }

                            if (failure)
                                return logs;
                        }

                        s.MainViewModel.TaskbarProgressState = oldTaskbarItemProgressState;

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, selectedPath);
                        logs.AddRange(varLogs);
                    }
                    break;
                default: // Error
                    throw new InternalException("Internal Logic Error at CommandInterface.UserInput");
            }

            return logs;
        }

        public static List<LogInfo> AddInterface(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_AddInterface info = cmd.Info.Cast<CodeInfo_AddInterface>();

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
