using PEBakery.Exceptions;
using PEBakery.Helper;
using System;
using System.Collections.Generic;
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
        public static List<UICommand> ParseRawLines(List<string> lines, SectionAddress addr)
        {
            // Select Code sections and compile
            List<UICommand> uiCmdList = new List<UICommand>();
            for (int i = 0; i < lines.Count; i++)
                uiCmdList.Add(ParseUICommand(lines, ref i, addr));

            return uiCmdList.Where(x => x.Type != UIControlType.None).ToList();
        }

        public static UICommand ParseUICommand(List<string> rawLines, ref int idx, SectionAddress addr)
        {
            UIControlType type = UIControlType.None;
            string rawLine = rawLines[idx].Trim();

            string key = string.Empty;
            int equalIdx = rawLine.IndexOf('=');
            if (equalIdx != -1) // there is key
            {
                key = rawLine.Substring(0, equalIdx);
                rawLine = rawLine.Substring(equalIdx + 1);
            }
            else
                return new UICommand(rawLine, addr, key);

            // Check if rawCode is Empty
            if (string.Equals(rawLine, string.Empty))
                return new UICommand(rawLine, addr, key);

            // Comment Format : starts with '//' or '#', ';'
            if (rawLine.StartsWith("//") || rawLine.StartsWith("#") || rawLine.StartsWith(";"))
                return new UICommand(rawLine, addr, key);

            // Splice with spaces
            List<string> slices = rawLine.Split(',').ToList();

            // Parse Operands
            List<string> operands = CodeParser.ParseOperands(slices, 0);

            // UICommand should have at least 7 operands
            //    Text, Visibility, Type, X, Y, width, height, [Optional]
            if (operands.Count < 7)
                return new UICommand(rawLine, addr, key);

            // Parse opcode
            type = UIParser.ParseControlType(operands[2]);

            // Remove UIControlType from operands
            //   Leftover : Text, Visibility, X, Y, width, height, [Optional]
            operands.RemoveAt(2);

            // Check doublequote's occurence - must be 2n
            if (FileHelper.CountStringOccurrences(rawLine, "\"") % 2 == 1)
                throw new InvalidCommandException("number of doublequotes must be times of 2");

            // Check if last operand is \ - MultiLine check - only if one or more operands exists
            if (0 < operands.Count)
            {
                while (string.Equals(operands.Last(), @"\", StringComparison.OrdinalIgnoreCase))
                { // Split next line and append to List<string> operands
                    if (rawLines.Count <= idx) // Section ended with \, invalid grammar!
                        throw new InvalidCommandException(@"A section's last command cannot end with '\'");
                    idx++;
                    operands.AddRange(rawLines[idx].Trim().Split(','));
                }
            }

            // Forge UICommand
            string text = Engine.UnescapeString(operands[0]);
            bool visibility = string.Equals(operands[1], "1", StringComparison.Ordinal);
            int.TryParse(operands[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x);
            int.TryParse(operands[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y);
            int.TryParse(operands[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int width);
            int.TryParse(operands[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out int height);
            Rect rect = new Rect(x, y, width, height);
            UICommandInfo info = ParseUICommandInfo(type, operands);
            return new UICommand(rawLine, addr, key, text, visibility, type, rect, info);
        }

        public static UIControlType ParseControlType(string typeStr)
        {
            UIControlType type = UIControlType.None;

            // typeStr must be number
            if (!Regex.IsMatch(typeStr, @"^[0-9]+$", RegexOptions.Compiled))
                throw new InvalidCommandException("Only number can be used as UICommand type");

            try
            {
                type = (UIControlType)Enum.Parse(typeof(UIControlType), typeStr, true);
                if (!Enum.IsDefined(typeof(UIControlType), type))
                    throw new ArgumentException();
            }
            catch (ArgumentException)
            {
                type = UIControlType.None;
            }
            return type;
        }

        private static UICommandInfo ParseUICommandInfo(UIControlType type, List<string> operands)
        {
            // Only use fields starting from 8th operand
            List<string> op = operands.Skip(6).ToList(); // Remove Text, Visibility, X, Y, width, height
            UICommandInfo error = new UICommandInfo(false, null);

            switch (type)
            {
                case UIControlType.TextBox:
                    {
                        const int minOpCount = 1;
                        const int maxOpCount = 1;
                        const int optOpCount = 1;
                        if (CheckInfoOperandLength(op, minOpCount, maxOpCount, optOpCount))
                            return error;

                        return new UIInfo_TextBox(true, GetInfoTooltip(op, optOpCount), Engine.UnescapeString(op[0]));
                    }
                case UIControlType.TextLabel:
                    {
                        const int minOpCount = 1;
                        const int maxOpCount = 2;
                        const int optOpCount = 1;
                        if (CheckInfoOperandLength(op, minOpCount, maxOpCount, optOpCount))
                            return error;

                        int.TryParse(op[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int fontSize);
                        UIInfo_TextLabel_Style style = UIInfo_TextLabel_Style.Normal;
                        if (string.Equals(op[1], "Bold", StringComparison.OrdinalIgnoreCase))
                            style = UIInfo_TextLabel_Style.Bold;
                        else if (string.Equals(op[1], "Italic", StringComparison.OrdinalIgnoreCase))
                            style = UIInfo_TextLabel_Style.Italic;
                        else if (string.Equals(op[1], "Underline", StringComparison.OrdinalIgnoreCase))
                            style = UIInfo_TextLabel_Style.Underline;
                        else if (string.Equals(op[1], "Strike", StringComparison.OrdinalIgnoreCase))
                            style = UIInfo_TextLabel_Style.Strike;

                        return new UIInfo_TextLabel(true, GetInfoTooltip(op, optOpCount), fontSize, style);
                    }
                case UIControlType.NumberBox:
                    break;
                case UIControlType.CheckBox:
                    {
                        const int minOpCount = 1;
                        const int maxOpCount = 1;
                        const int optOpCount = 2; // [SectionToRun],[Tooltip]
                        if (CheckInfoOperandLength(op, minOpCount, maxOpCount, optOpCount))
                            return error;

                        bool _checked = false;
                        if (string.Equals(op[0], "True", StringComparison.OrdinalIgnoreCase))
                            _checked = true;

                        return new UIInfo_CheckBox(true, GetInfoTooltip(op, optOpCount), _checked);
                    }
                case UIControlType.ComboBox:
                    break;
                case UIControlType.Image:
                    {
                        const int minOpCount = 0;
                        const int maxOpCount = 0;
                        const int optOpCount = 1; // [Tooltip]
                        if (CheckInfoOperandLength(op, minOpCount, maxOpCount, optOpCount))
                            return error;

                        return new UIInfo_Image(true, GetInfoTooltip(op, optOpCount));
                    }
                    break;
                case UIControlType.TextFile:
                    break;
                case UIControlType.Button:
                    break;
                case UIControlType.CheckList:
                    break;
                case UIControlType.WebLabel:
                    {
                        const int minOpCount = 1;
                        const int maxOpCount = 1;
                        const int optOpCount = 1;
                        if (CheckInfoOperandLength(op, minOpCount, maxOpCount, optOpCount))
                            return error;

                        return new UIInfo_WebLabel(true, GetInfoTooltip(op, optOpCount), Engine.UnescapeString(op[0]));
                    }
                case UIControlType.RadioButton:
                    break;
                case UIControlType.Bevel:
                    {
                        const int minOpCount = 0;
                        const int maxOpCount = 0;
                        const int optOpCount = 1;
                        if (CheckInfoOperandLength(op, minOpCount, maxOpCount, optOpCount))
                            return error;

                        return new UIInfo_Bevel(true, GetInfoTooltip(op, optOpCount));
                    }
                case UIControlType.FileBox:
                    break;
                case UIControlType.RadioGroup:
                    break;
                default:
                    break;
            }
            return error;
        }

        /// <summary>
        /// Check UICommandInfo's operand's validity. Return true if error.
        /// In reality, optional may be always 1 because of tooltip
        /// </summary>
        /// <param name="op"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns>Return true if error.</returns>
        private static bool CheckInfoOperandLength(List<string> op, int min, int max, int optional)
        {
            if (op.Count < min || max + optional < op.Count)
                return true;
            else
                return false;
        }

        private static string GetInfoTooltip(List<string> op, int max)
        {
            if (max < op.Count && op[max].StartsWith("__")) // Has tooltip
                return op[max];
            else
                return null;
        }
    }
}
