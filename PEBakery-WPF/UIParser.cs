using PEBakery.Exceptions;
using PEBakery.Helper;
using System;
using System.Collections.Generic;
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
            string text = operands[0];
            bool visibility = string.Equals(operands[1], "1", StringComparison.Ordinal);
            int x = 0, y = 0, width = 0, height = 0; // In case of failure, value will be set to 0
            int.TryParse(operands[2], out x);
            int.TryParse(operands[3], out y);
            int.TryParse(operands[4], out width);
            int.TryParse(operands[5], out height);
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
                    break;
                case UIControlType.TextLabel:
                    {
                        if (CheckInfoOperandLength(op, 1, 2, 1))
                            return error;
                        int.TryParse(op[0], out int fontSize);
                        UIInfo_TextLabel_Style style = UIInfo_TextLabel_Style.Normal;
                        if (string.Equals(op[1], "Bold", StringComparison.OrdinalIgnoreCase))
                            style = UIInfo_TextLabel_Style.Bold;
                        else if (string.Equals(op[1], "Italic", StringComparison.OrdinalIgnoreCase))
                            style = UIInfo_TextLabel_Style.Italic;
                        else if (string.Equals(op[1], "Underline", StringComparison.OrdinalIgnoreCase))
                            style = UIInfo_TextLabel_Style.Underline;
                        else if (string.Equals(op[1], "Strike", StringComparison.OrdinalIgnoreCase))
                            style = UIInfo_TextLabel_Style.Strike;
                        return new UIInfo_TextLabel(true, GetInfoTooltip(op, 2), fontSize, style);
                    }
                case UIControlType.NumberBox:
                    break;
                case UIControlType.CheckBox:
                    break;
                case UIControlType.ComboBox:
                    break;
                case UIControlType.Image:
                    break;
                case UIControlType.TextFile:
                    break;
                case UIControlType.Button:
                    break;
                case UIControlType.CheckList:
                    break;
                case UIControlType.WebLabel:
                    break;
                case UIControlType.RadioButton:
                    break;
                case UIControlType.Bevel:
                    break;
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
            if (op.Count < min || max < op.Count)
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
