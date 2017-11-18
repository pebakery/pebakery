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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace PEBakery.Core
{
    public static class UIParser
    {
        public static List<UICommand> ParseRawLines(List<string> lines, SectionAddress addr, out List<LogInfo> errorLogs)
        {
            // Select Code sections and compile
            errorLogs = new List<LogInfo>();
            List<UICommand> uiCmdList = new List<UICommand>();
            for (int i = 0; i < lines.Count; i++)
            {
                try
                {
                    uiCmdList.Add(ParseUICommand(lines, addr, ref i));
                }
                catch (EmptyLineException) { } // Do nothing
                catch (InvalidUICommandException e)
                {
                    errorLogs.Add(new LogInfo(LogState.Error, $"{Logger.LogExceptionMessage(e)} ({e.UICmd.RawLine})"));
                }
                catch (InvalidCommandException e)
                {
                    errorLogs.Add(new LogInfo(LogState.Error, $"{Logger.LogExceptionMessage(e)} ({e.RawLine})"));
                }
                catch (Exception e)
                {
                    errorLogs.Add(new LogInfo(LogState.Error, e));
                }
            }

            return uiCmdList.Where(x => x.Type != UIType.None).ToList();
        }

        public static UICommand ParseUICommand(List<string> rawLines, SectionAddress addr, ref int idx)
        {
            UIType type = UIType.None;
            string rawLine = rawLines[idx].Trim();

            // Check if rawCode is Empty
            if (rawLine.Equals(string.Empty))
                throw new EmptyLineException();

            // Comment Format : starts with '//' or '#', ';'
            if (rawLine.StartsWith("//") || rawLine.StartsWith("#") || rawLine.StartsWith(";"))
                throw new EmptyLineException();

            // Find key of interface control
            string key = string.Empty;
            string rawValue = string.Empty;
            int equalIdx = rawLine.IndexOf('=');
            if (equalIdx != -1)
            {
                key = rawLine.Substring(0, equalIdx);
                rawValue = rawLine.Substring(equalIdx + 1);
            }
            else
            {
                throw new InvalidCommandException($"Interface control [{rawValue}] does not have name", rawLine);
            }

            // Parse Arguments
            List<string> args = new List<string>();
            try
            {
                string remainder = rawValue;
                while (remainder != null)
                {
                    Tuple<string, string> tuple = CodeParser.GetNextArgument(remainder);
                    args.Add(tuple.Item1);
                    remainder = tuple.Item2;
                }
            }
            catch (InvalidCommandException e) { throw new InvalidCommandException(e.Message, rawLine); }

            // Check doublequote's occurence - must be 2n
            if (StringHelper.CountOccurrences(rawValue, "\"") % 2 == 1)
                throw new InvalidCommandException($"Interface control [{rawValue}]'s doublequotes mismatch", rawLine);

            // Check if last operand is \ - MultiLine check - only if one or more operands exists
            if (0 < args.Count)
            {
                while (args.Last().Equals(@"\", StringComparison.OrdinalIgnoreCase))
                { // Split next line and append to List<string> operands
                    if (rawLines.Count <= idx) // Section ended with \, invalid grammar!
                        throw new InvalidCommandException($@"Last interface control [{rawValue}] cannot end with '\' ", rawLine);
                    idx++;
                    args.AddRange(rawLines[idx].Trim().Split(','));
                }
            }

            // UICommand should have at least 7 operands
            //    Text, Visibility, Type, X, Y, width, height, [Optional]
            if (args.Count < 7)
                throw new InvalidCommandException($"Interface control [{rawValue}] must have at least 7 arguments", rawLine);

            // Parse opcode
            try { type = UIParser.ParseControlType(args[2]); }
            catch (InvalidCommandException e) { throw new InvalidCommandException(e.Message, rawLine); }

            // Remove UIControlType from operands
            //   Leftover : Text, Visibility, X, Y, width, height, [Optional]
            args.RemoveAt(2);

            // Forge UICommand
            string text = StringEscaper.Unescape(args[0]);
            bool visibility = string.Equals(args[1], "1", StringComparison.Ordinal);
            NumberHelper.ParseInt32(args[2], out int x);
            NumberHelper.ParseInt32(args[3], out int y);
            NumberHelper.ParseInt32(args[4], out int width);
            NumberHelper.ParseInt32(args[5], out int height);
            Rect rect = new Rect(x, y, width, height);
            UIInfo info;
            try { info = ParseUICommandInfo(type, args); }
            catch (InvalidCommandException e) { throw new InvalidCommandException(e.Message, rawLine); }
            return new UICommand(rawLine, addr, key, text, visibility, type, rect, info);
        }

        public static UIType ParseControlType(string typeStr)
        {
            // typeStr must be number
            if (!Regex.IsMatch(typeStr, @"^[0-9]+$", RegexOptions.Compiled))
                throw new InvalidCommandException("Only number can be used as UICommand type");

            bool failure = false;
            if (Enum.TryParse(typeStr, false, out UIType type) == false)
                failure = true;
            if (Enum.IsDefined(typeof(UIType), type) == false)
                failure = true;

            if (failure)
                throw new InvalidCommandException("Invalid UICommand type");

            return type;
        }

        private static UIInfo ParseUICommandInfo(UIType type, List<string> arguments)
        {
            // Only use fields starting from 8th operand
            List<string> args = arguments.Skip(6).ToList(); // Remove Text, Visibility, X, Y, width, height

            switch (type)
            {
                #region TextBox
                case UIType.TextBox:
                    {
                        const int minOpCount = 1;
                        const int maxOpCount = 1;
                        if (CodeParser.CheckInfoArgumentCount(args, minOpCount, maxOpCount + 1)) // +1 for tooltip
                            throw new InvalidCommandException($"[{type}] can have [{minOpCount}] ~ [{maxOpCount + 1}] arguments");

                        return new UIInfo_TextBox(GetInfoTooltip(args, maxOpCount), StringEscaper.Unescape(args[0]));
                    }
                #endregion
                #region TextLabel
                case UIType.TextLabel:
                    {
                        const int minOpCount = 1;
                        const int maxOpCount = 2;
                        if (CodeParser.CheckInfoArgumentCount(args, minOpCount, maxOpCount + 1)) // +1 for tooltip
                            throw new InvalidCommandException($"[{type}] can have [{minOpCount}] ~ [{maxOpCount + 1}] arguments");

                        NumberHelper.ParseInt32(args[0], out int fontSize);
                        UIInfo_TextLabel_Style style = UIInfo_TextLabel_Style.Normal;
                        if (args[1].Equals("Bold", StringComparison.OrdinalIgnoreCase))
                            style = UIInfo_TextLabel_Style.Bold;
                        else if (args[1].Equals("Italic", StringComparison.OrdinalIgnoreCase))
                            style = UIInfo_TextLabel_Style.Italic;
                        else if (args[1].Equals("Underline", StringComparison.OrdinalIgnoreCase))
                            style = UIInfo_TextLabel_Style.Underline;
                        else if (args[1].Equals("Strike", StringComparison.OrdinalIgnoreCase))
                            style = UIInfo_TextLabel_Style.Strike;

                        return new UIInfo_TextLabel(GetInfoTooltip(args, maxOpCount), fontSize, style);
                    }
                #endregion
                #region NumberBox
                case UIType.NumberBox:
                    {
                        const int minOpCount = 4;
                        const int maxOpCount = 4;
                        if (CodeParser.CheckInfoArgumentCount(args, minOpCount, maxOpCount + 1)) // +1 for tooltip
                            throw new InvalidCommandException($"[{type}] can have [{minOpCount}] ~ [{maxOpCount + 1}] arguments");

                        NumberHelper.ParseInt32(args[0], out int value);
                        NumberHelper.ParseInt32(args[1], out int min);
                        NumberHelper.ParseInt32(args[2], out int max);
                        NumberHelper.ParseInt32(args[3], out int interval);

                        return new UIInfo_NumberBox(GetInfoTooltip(args, maxOpCount), value, min, max, interval);
                    }
                #endregion
                #region CheckBox
                case UIType.CheckBox:
                    {
                        const int minOpCount = 1;
                        const int maxOpCount = 3; // +2 for [RunOptional]
                        if (CodeParser.CheckInfoArgumentCount(args, minOpCount, maxOpCount + 1)) // +1 for tooltip
                            throw new InvalidCommandException($"[{type}] can have [{minOpCount}] ~ [{maxOpCount + 1}] arguments");

                        bool _checked = false;
                        if (args[0].Equals("True", StringComparison.OrdinalIgnoreCase))
                            _checked = true;
                        else if (args[0].Equals("False", StringComparison.OrdinalIgnoreCase) == false)
                            throw new InvalidCommandException($"Invalid argument [{args[0]}], must be [True] or [False]");

                        string tooltip = null;
                        if (args.Last().StartsWith("__", StringComparison.Ordinal)) // Has <ToolTip>
                            tooltip = GetInfoTooltip(args, args.Count - 1);

                        string sectionName = null;
                        bool hideProgress = false;
                        if (3 <= args.Count &&
                            (args[2].Equals("True", StringComparison.OrdinalIgnoreCase) || args[2].Equals("False", StringComparison.OrdinalIgnoreCase)) &&
                            (args[1].StartsWith("_", StringComparison.Ordinal) && args[1].EndsWith("_", StringComparison.Ordinal)))
                        { // Has [RunOptinal] -> <SectionName>,<HideProgress>
                            if (args[2].Equals("True", StringComparison.OrdinalIgnoreCase))
                                hideProgress = true;
                            else if (args[2].Equals("False", StringComparison.OrdinalIgnoreCase) == false)
                                throw new InvalidCommandException($"Invalid argument [{args[2]}], must be [True] or [False]");

                            sectionName = args[1].Substring(1, args[1].Length - 2);
                        }

                        return new UIInfo_CheckBox(tooltip, _checked, sectionName, hideProgress);
                    }
                #endregion
                #region ComboBox
                case UIType.ComboBox:
                    { // Variable Length
                        List<string> items = new List<string>();
                        string last = args.Last();
                        string toolTip = null;
                        int count = 0;
                        if (last.StartsWith("__", StringComparison.Ordinal))
                        {
                            toolTip = last;
                            count = args.Count - 1;
                        }
                        else
                        {
                            count = args.Count;
                        }

                        for (int i = 0; i < count; i++)
                            items.Add(args[i]);

                        int idx = items.IndexOf(arguments[0]);
                        if (idx == -1)
                            throw new InvalidCommandException($"[{type}] has wrong selected value [{arguments[0]}]");

                        return new UIInfo_ComboBox(toolTip, items, idx);
                    }
                #endregion
                #region Image
                case UIType.Image:
                    {
                        const int minOpCount = 0;
                        const int maxOpCount = 1; // [URL]
                        if (CodeParser.CheckInfoArgumentCount(args, minOpCount, maxOpCount + 1))  // +1 for tooltip
                            throw new InvalidCommandException($"[{type}] can have [{minOpCount}] ~ [{maxOpCount + 1}] arguments");

                        string url = null;
                        if (maxOpCount < args.Count)
                            url = args[maxOpCount];

                        return new UIInfo_Image(GetInfoTooltip(args, maxOpCount), url);
                    }
                #endregion
                #region TextFile
                case UIType.TextFile:
                    {
                        const int minOpCount = 0;
                        const int maxOpCount = 0;
                        if (CodeParser.CheckInfoArgumentCount(args, minOpCount, maxOpCount))
                            throw new InvalidCommandException($"[{type}] can have [{minOpCount}] ~ [{maxOpCount + 1}] arguments");

                        return new UIInfo_TextFile(GetInfoTooltip(args, maxOpCount));
                    }
                #endregion
                #region Button
                case UIType.Button:
                    { // <SectionToRun>,<Picture>,[HideProgress]  +[UnknownBoolean] +[RunOptional]
                        // Ex)
                        // pButton1 =,1,8,382,47,24,24,Process-OpenDriver_x86,opendir.bmp,False,_Process-OpenDriver_x86,False,_Process-OpenDriver_x86_,False
                        // Button_Download=,1,8,403,21,24,24,DownloadXXX,DoubleJDesignRavenna3dArrowDown0016016.bmp,False,False,_DownloadXXX_,False,"__DOWNLOAD Plugin"
                        // OpendirSMFilesButton=,1,8,475,204,24,24,Opendir_SMFiles,opendir.bmp,"__Open Custom .ini Folder"
                        // Button_HiveUnload_Target="HiveUnload: Target + ProjectTemp + MountFolders",1,8,15,17,293,46,HiveUnload_Launch_B,HiveUnload3232.bmp,0,"__UnLoad hives"
                        // Button_Tools_Folder="Open Tools Folder",1,8,98,256,134,25,Open_Tools_Folder
                        const int minOpCount = 1;
                        if (CodeParser.CheckInfoArgumentCount(args, minOpCount, -1))
                            throw new InvalidCommandException($"[{type}] must have at least [{minOpCount}] arguments");

                        int cnt = args.Count;
                        string tooltip = null;
                        if (args.Last().StartsWith("__", StringComparison.Ordinal)) // Has <ToolTip>
                        {
                            tooltip = GetInfoTooltip(args, cnt - 1);
                            cnt -= 1;
                        }

                        string sectionName = args[0];

                        string picture = null;
                        if (2 <= cnt)
                        {
                            if (args[1].Equals("0", StringComparison.OrdinalIgnoreCase) == false)
                                picture = args[1];
                        }

                        bool hideProgress = false;
                        if (3 <= cnt)
                        {
                            if (args[2].Equals("True", StringComparison.OrdinalIgnoreCase))
                                hideProgress = true;
                            else if (args[2].Equals("False", StringComparison.OrdinalIgnoreCase) == false)
                            {
                                // WB082 Compability Shim
                                if (args[2].Equals("1", StringComparison.Ordinal))
                                    hideProgress = true;
                                else if (args[2].Equals("0", StringComparison.Ordinal) == false)
                                    throw new InvalidCommandException($"Invalid argument [{args[2]}], must be [True] or [False]");
                            }
                        }

                        // Ignore [UnknownBoolean] and [RunOptional]

                        return new UIInfo_Button(tooltip, args[0], picture, hideProgress);
                    }
                #endregion
                #region WebLabel
                case UIType.WebLabel:
                    {
                        const int minOpCount = 1;
                        const int maxOpCount = 1;
                        if (CodeParser.CheckInfoArgumentCount(args, minOpCount, maxOpCount + 1)) // +1 for tooltip
                            throw new InvalidCommandException($"[{type}] can have [{minOpCount}] ~ [{maxOpCount + 1}] arguments");

                        return new UIInfo_WebLabel(GetInfoTooltip(args, maxOpCount), StringEscaper.Unescape(args[0]));
                    }
                #endregion
                #region RadioButton
                case UIType.RadioButton:
                    {
                        const int minOpCount = 1;
                        const int maxOpCount = 3; // +2 for [RunOptional]
                        if (CodeParser.CheckInfoArgumentCount(args, minOpCount, maxOpCount + 1))
                            throw new InvalidCommandException($"[{type}] can have [{minOpCount}] ~ [{maxOpCount + 1}] arguments");

                        bool selected = false;
                        if (args[0].Equals("True", StringComparison.OrdinalIgnoreCase))
                            selected = true;
                        else if (args[0].Equals("False", StringComparison.OrdinalIgnoreCase) == false)
                            throw new InvalidCommandException($"Invalid argument [{args[0]}], must be [True] or [False]");

                        string tooltip = null;
                        if (args.Last().StartsWith("__", StringComparison.Ordinal)) // Has <ToolTip>
                            tooltip = GetInfoTooltip(args, args.Count - 1);

                        string sectionName = null;
                        bool hideProgress = false;
                        if (3 <= args.Count &&
                            (args[2].Equals("True", StringComparison.OrdinalIgnoreCase) || args[2].Equals("False", StringComparison.OrdinalIgnoreCase)) &&
                            (args[1].StartsWith("_", StringComparison.Ordinal) && args[1].EndsWith("_", StringComparison.Ordinal)))
                        { // Has [RunOptinal] -> <SectionName>,<HideProgress>
                            if (args[2].Equals("True", StringComparison.OrdinalIgnoreCase))
                                hideProgress = true;
                            else if (args[2].Equals("False", StringComparison.OrdinalIgnoreCase) == false)
                                throw new InvalidCommandException($"Invalid argument [{args[2]}], must be [True] or [False]");

                            sectionName = args[1].Substring(1, args[1].Length - 2);
                        }

                        return new UIInfo_RadioButton(tooltip, selected, sectionName, hideProgress);
                    }
                #endregion
                #region Bevel
                case UIType.Bevel:
                    {
                        const int minOpCount = 0;
                        const int maxOpCount = 0;
                        if (CodeParser.CheckInfoArgumentCount(args, minOpCount, maxOpCount + 1))
                            throw new InvalidCommandException($"[{type}] can have [{minOpCount}] ~ [{maxOpCount + 1}] arguments");

                        return new UIInfo_Bevel(GetInfoTooltip(args, maxOpCount));
                    }
                #endregion
                #region FileBox
                case UIType.FileBox:
                    {
                        const int minOpCount = 0;
                        const int maxOpCount = 1;
                        if (CodeParser.CheckInfoArgumentCount(args, minOpCount, maxOpCount + 1))
                            throw new InvalidCommandException($"[{type}] can have [{minOpCount}] ~ [{maxOpCount + 1}] arguments");

                        bool isFile = false;
                        if (0 < args.Count)
                        {
                            if (args[0].Equals("FILE", StringComparison.OrdinalIgnoreCase))
                                isFile = true;
                        }

                        return new UIInfo_FileBox(GetInfoTooltip(args, maxOpCount), isFile);
                    }
                #endregion
                #region RadioGroup
                case UIType.RadioGroup:
                    { // Variable Length
                        List<string> items = new List<string>();

                        string sectionName = null;
                        bool showProgress = false;

                        int cnt = args.Count - 1;
                        if (args.Last().StartsWith("__", StringComparison.Ordinal)) // Has <ToolTip>
                            cnt -= 1;

                        if ((args[cnt].Equals("True", StringComparison.OrdinalIgnoreCase) || args[cnt].Equals("False", StringComparison.OrdinalIgnoreCase)) &&
                                (args[cnt - 1].StartsWith("_", StringComparison.Ordinal) && args[cnt - 1].EndsWith("_", StringComparison.Ordinal)))
                        { // Has [RunOptinal] -> <SectionName>,<HideProgress>
                            if (args[cnt].Equals("True", StringComparison.OrdinalIgnoreCase))
                                showProgress = true;
                            else if (args[cnt].Equals("False", StringComparison.OrdinalIgnoreCase) == false)
                                throw new InvalidCommandException($"Invalid argument [{args[cnt]}], must be [True] or [False]");

                            sectionName = args[cnt - 1].Substring(1, args[cnt - 1].Length - 2);

                            cnt -= 2;
                        }

                        for (int i = 0; i < cnt; i++)
                            items.Add(args[i]);
                        
                        if (NumberHelper.ParseInt32(args[cnt], out int idx) == false)
                            throw new InvalidCommandException($"Invalid argument [{args[cnt]}], must be integer");

                        return new UIInfo_RadioGroup(GetInfoTooltip(args, args.Count), items, idx, sectionName, showProgress);
                    }
                #endregion
                #region default
                default:
                    Debug.Assert(false);
                    break;
                #endregion
            }

            throw new InvalidCommandException($"Invalid UICommand [{type}]");
        }

        /// <summary>
        /// Extract tooltip from operand
        /// </summary>
        /// <param name="op"></param>
        /// <param name="idx">Tooltip operator's operand index</param>
        /// <returns></returns>
        private static string GetInfoTooltip(List<string> op, int idx)
        {
            if (idx < op.Count && op[idx].StartsWith("__", StringComparison.Ordinal)) // Has tooltip
                return op[idx].Substring(2);
            else
                return null;
        }
    }
}
