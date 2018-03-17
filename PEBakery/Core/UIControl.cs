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

using PEBakery.Helper;
using PEBakery.IniLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PEBakery.Core
{
    #region Enum UIControlType
    public enum UIControlType
    {
        None = -1,
        TextBox = 0,
        TextLabel = 1,
        NumberBox = 2,
        CheckBox = 3,
        ComboBox = 4,
        Image = 5,
        TextFile = 6,
        // 7 was to be EditValues, but WinBuilder 082 didn't implemented this
        Button = 8,
        // 9 is CheckList, but rarely used so deprecated
        WebLabel = 10,
        RadioButton = 11,
        Bevel = 12,
        FileBox = 13,
        RadioGroup = 14
    }
    #endregion

    #region Interface Representation Format
    /*
    <Key>=<Text>,Visibility,Type,X,Y,Width,Height,<OptionalValues>,[Tooltip]
    Visibility : 1 or 0
    Type : UIControlType 0 ~ 14

    <Text>
    TextBox     = Caption
    TextLabel   = Caption 
    NumberBox   = <ControlName> 
    CheckBox    = Caption 
    ComboBox    = <SelectedItem> // no number, name of item
    Image       = <FileName> 
    TextFile    = <FileName> 
    Button      = Caption 
    WebLabel    = Caption 
    RadioButton = Caption 
    Bevel       = Caption // If set to <ControlName> caption will be hidden. (For compatability with scripts built in wb editor. )
    FileBox     = <Path> // It can be file or directory
    RadioGroup  = Caption 

    <OptionalValues>
    TextBox     = <StringValue>
    TextLabel   = <FontSize>,<Style>
                  <Style> : Normal, Bold (in WB082)
                            Italic, Underline, Strike (Added in PEBakery)
    NumberBox   = <IntegerValue>,<Min>,<Max>,<IncrementUnit>
    CheckBox    = <BooleanValue>,[SectionToRun]  +[RunOptional]
    ComboBox    = <StringValue1>,<StringValue2>, ... ,<StringValueN>
    Image       = <StringValue> // URL
    Button      = <SectionToRun>,<Picture>,[HideProgress]  +[UnknownBoolean]  +[RunOptional]
                  [Picture] - 0 if no picture. or its value is Embedded File name.
    WebLabel    = <StringValue> // URL
    RadioButton = <BooleanValue> +[RunOptional]
    FileBox     = [FILE|DIR]
    Bevel       = <FontSize>,<Style> (Added in PEBakery)
                  <Style> : Normal, Bold
    RadioGroup  = <StringValue1>,<StringValue2>, ... ,<StringValueN>,<IntegerIndex>  +[RunOptional]
                  // IntegerIndex : selected index, starting from 0

    [RunOptional]
    For CheckBox, Button, RadioButton, RadioGroup
    <SectionToRun>,<HideProgress>
    
    SectionToRun : (String) SectionName with _ at start and end
    HideProgress : (Bool)   

    [Tooltip]
    <StringValue> : Tooltip to show when mousehover event, always start with __
    */
    #endregion

    #region UIControl
    [Serializable]
    public class UIControl
    {
        #region Fields
        public string RawLine;
        public SectionAddress Addr;

        public string Key;
        public string Text;
        public bool Visibility;
        public UIControlType Type;
        public Rect Rect;
        public UIInfo Info;
        #endregion

        #region Constructors
        public UIControl(string rawLine, SectionAddress addr, string key)
        {
            this.RawLine = rawLine;
            this.Addr = addr;

            this.Key = key;
            this.Text = string.Empty;
            this.Visibility = false;
            this.Type = UIControlType.None;
            this.Rect = new Rect(0, 0, 0, 0);

        }

        public UIControl(string rawLine, SectionAddress addr, string key, string text, bool visibility, UIControlType type, Rect rect, UIInfo info)
        {
            this.RawLine = rawLine;
            this.Addr = addr;

            this.Key = key;
            this.Text = text;
            this.Visibility = visibility;
            this.Type = type;
            this.Rect = rect;
            this.Info = info;
        }
        #endregion

        #region ToString, ForgeRawLine
        public override string ToString()
        {
            return ForgeRawLine(true);
        }

        public string ForgeRawLine(bool includeKey)
        {
            StringBuilder b = new StringBuilder();
            if (includeKey)
            {
                b.Append(StringEscaper.QuoteEscape(Key));
                b.Append("=");
            }

            b.Append(StringEscaper.QuoteEscape(Text));
            b.Append(",");
            if (Visibility)
                b.Append("1,");
            else
                b.Append("0,");
            b.Append((int) Type);
            b.Append(",");
            b.Append(Rect.Left);
            b.Append(",");
            b.Append(Rect.Top);
            b.Append(",");
            b.Append(Rect.Width);
            b.Append(",");
            b.Append(Rect.Height);

            b.Append(Info.ForgeRawLine());
            /*
            string optionalArgs = Info.ForgeRawLine();
            if (0 < optionalArgs.Length) // Only if optionalArgs is not empty
            {
                b.Append(",");
                b.Append(optionalArgs);
            }
            */

            return b.ToString();
        }
        #endregion

        #region Update
        public void Update()
        {
            Ini.SetKey(Addr.Script.RealPath, new IniKey(Addr.Section.Name, Key, ForgeRawLine(false)));
        }
        
        public static void Update(List<UIControl> uiCmdList)
        {
            if (0 < uiCmdList.Count)
            {
                string fullPath = uiCmdList[0].Addr.Script.RealPath;
                List<IniKey> keys = new List<IniKey>(uiCmdList.Count);
                for (int i = 0; i < uiCmdList.Count; i++)
                {
                    UIControl uiCmd = uiCmdList[i];
                    Debug.Assert(fullPath.Equals(uiCmd.Addr.Script.RealPath, StringComparison.OrdinalIgnoreCase));

                    keys.Add(new IniKey(uiCmd.Addr.Section.Name, uiCmd.Key, uiCmd.ForgeRawLine(false)));
                }

                Ini.SetKeys(fullPath, keys);
            }           
        }
        #endregion

        #region GetValue, SetValue
        public string GetValue()
        {
            string value = null;
            switch (Type)
            {
                case UIControlType.TextBox:
                    {
                        Debug.Assert(Info.GetType() == typeof(UIInfo_TextBox));
                        UIInfo_TextBox info = Info as UIInfo_TextBox;

                        value = info.Value;
                    }
                    break;
                case UIControlType.NumberBox:
                    {
                        Debug.Assert(Info.GetType() == typeof(UIInfo_NumberBox));
                        UIInfo_NumberBox info = Info as UIInfo_NumberBox;

                        value = info.Value.ToString();
                    }
                    break;
                case UIControlType.CheckBox:
                    {
                        Debug.Assert(Info.GetType() == typeof(UIInfo_CheckBox));
                        UIInfo_CheckBox info = Info as UIInfo_CheckBox;

                        value = info.Value ? "True" : "False";
                    }
                    break;
                case UIControlType.ComboBox:
                    {
                        value = Text;
                    }
                    break;
                case UIControlType.RadioButton:
                    {
                        Debug.Assert(Info.GetType() == typeof(UIInfo_RadioButton));
                        UIInfo_RadioButton info = Info as UIInfo_RadioButton;

                        value = info.Selected ? "True" : "False";
                    }
                    break;
                case UIControlType.FileBox:
                    {
                        value = Text;
                    }
                    break;
                case UIControlType.RadioGroup:
                    {
                        Debug.Assert(Info.GetType() == typeof(UIInfo_RadioGroup));
                        UIInfo_RadioGroup info = Info as UIInfo_RadioGroup;

                        value = info.Selected.ToString();
                    }
                    break;
            }

            return value;
        }

        public bool SetValue(string newValue, bool update, out List<LogInfo> logs)
        {
            logs = new List<LogInfo>(1);
            bool success = false;
            switch (Type)
            {
                case UIControlType.TextBox:
                    {
                        Debug.Assert(Info.GetType() == typeof(UIInfo_TextBox));
                        UIInfo_TextBox uiInfo = Info as UIInfo_TextBox;

                        uiInfo.Value = newValue;

                        logs.Add(new LogInfo(LogState.Success, $"Interface control [{Key}] set to [{newValue}]"));
                        success = true;
                    }
                    break;
                case UIControlType.NumberBox:
                    {
                        Debug.Assert(Info.GetType() == typeof(UIInfo_NumberBox));
                        UIInfo_NumberBox uiInfo = Info as UIInfo_NumberBox;

                        // WB082 just write string value in case of error, but PEBakery will throw error
                        if (!NumberHelper.ParseInt32(newValue, out int intVal))
                        {
                            logs.Add(new LogInfo(LogState.Error, $"[{newValue}] is not a valid integer"));
                            return false;
                        }

                        if (uiInfo.Min <= intVal && intVal <= uiInfo.Max)
                        {
                            uiInfo.Value = intVal;
                        }
                        else
                        {
                            logs.Add(new LogInfo(LogState.Error, $"[{newValue}] should be inside of [{uiInfo.Min}] ~ [{uiInfo.Max}]"));
                            return false;
                        }

                        logs.Add(new LogInfo(LogState.Success, $"Interface control [{Key}] set to [{newValue}]"));
                        success = true;
                    }
                    break;
                case UIControlType.CheckBox:
                    {
                        Debug.Assert(Info.GetType() == typeof(UIInfo_CheckBox));
                        UIInfo_CheckBox uiInfo = Info as UIInfo_CheckBox;

                        if (newValue.Equals("True", StringComparison.OrdinalIgnoreCase))
                        {
                            uiInfo.Value = true;

                            logs.Add(new LogInfo(LogState.Success, $"Interface control [{Key}] set to [True]"));
                            success = true;
                        }
                        else if (newValue.Equals("False", StringComparison.OrdinalIgnoreCase))
                        {
                            uiInfo.Value = false;

                            logs.Add(new LogInfo(LogState.Success, $"Interface control [{Key}] set to [False]"));
                            success = true;
                        }
                        else
                        { // WB082 just write string value in case of error, but PEBakery will throw error
                            logs.Add(new LogInfo(LogState.Error, $"[{newValue}] is not a valid boolean value"));
                            return false;
                        }
                    }
                    break;
                case UIControlType.ComboBox:
                    {
                        Debug.Assert(Info.GetType() == typeof(UIInfo_ComboBox));
                        UIInfo_ComboBox uiInfo = Info as UIInfo_ComboBox;

                        int idx = uiInfo.Items.FindIndex(x => x.Equals(newValue, StringComparison.OrdinalIgnoreCase));
                        if (idx == -1)
                        { // Invalid Index
                            logs.Add(new LogInfo(LogState.Error, $"[{newValue}] was not found in the item list"));
                            return false;
                        }

                        uiInfo.Index = idx;
                        Text = uiInfo.Items[idx];

                        logs.Add(new LogInfo(LogState.Success, $"Interface control [{Key}] set to [{Text}]"));
                        success = true;
                    }
                    break;
                case UIControlType.RadioButton:
                    {
                        Debug.Assert(Info.GetType() == typeof(UIInfo_RadioButton));
                        UIInfo_RadioButton uiInfo = Info as UIInfo_RadioButton;

                        if (newValue.Equals("True", StringComparison.OrdinalIgnoreCase))
                        {
                            uiInfo.Selected = true;

                            logs.Add(new LogInfo(LogState.Success, $"Interface control [{Key}] set to [True]"));
                            success = true;
                        }
                        else if (newValue.Equals("False", StringComparison.OrdinalIgnoreCase))
                        {
                            uiInfo.Selected = false;

                            logs.Add(new LogInfo(LogState.Success, $"Interface control [{Key}] set to [False]"));
                            success = true;
                        }
                        else
                        { // WB082 just write string value, but PEBakery will throw error
                            logs.Add(new LogInfo(LogState.Error, $"[{newValue}] is not a valid boolean value"));
                            return false;
                        }
                    }
                    break;
                case UIControlType.FileBox:
                    {
                        Debug.Assert(Info.GetType() == typeof(UIInfo_FileBox));
                        UIInfo_FileBox uiInfo = Info as UIInfo_FileBox;

                        Text = newValue;

                        logs.Add(new LogInfo(LogState.Success, $"Interface Control [{Key}] set to [{newValue}]"));
                        success = true;
                    }
                    break;
                case UIControlType.RadioGroup:
                    {
                        Debug.Assert(Info.GetType() == typeof(UIInfo_RadioGroup));
                        UIInfo_RadioGroup uiInfo = Info as UIInfo_RadioGroup;

                        if (!NumberHelper.ParseInt32(newValue, out int idx))
                        {
                            logs.Add(new LogInfo(LogState.Error, $"[{newValue}] is not a valid integer"));
                            return false;
                        }

                        if (0 <= idx && idx < uiInfo.Items.Count)
                        {
                            uiInfo.Selected = idx;
                        }
                        else
                        { // Invalid Index
                            logs.Add(new LogInfo(LogState.Error, $"Index [{newValue}] is invalid"));
                            return false;
                        }

                        logs.Add(new LogInfo(LogState.Success, $"Interface control [{Key}] set to [{newValue}]"));
                        success = true;
                    }
                    break;
            }

            if (success && update)
                Update();

            return success;
        }
        #endregion
    }
    #endregion

    #region UIInfo
    [Serializable]
    public class UIInfo
    {
        public string ToolTip; // optional

        public UIInfo(string tooltip)
        {
            this.ToolTip = tooltip;
        }

        /// <summary>
        /// This function should only be called from child Class
        /// Note : this function includes first ','
        /// </summary>
        /// <returns></returns>
        public virtual string ForgeRawLine()
        {
            if (ToolTip != null)
                return "," + StringEscaper.QuoteEscape($"__{ToolTip}");
            else
                return string.Empty;
        }

        public override string ToString()
        {
            return ForgeRawLine();
        }
    }

    [Serializable]
    public class UIInfo_TextBox : UIInfo
    {
        public string Value;

        public UIInfo_TextBox(string tooltip,  string str)
            : base(tooltip)
        {
            this.Value = str;
        }

        public override string ForgeRawLine()
        {
            StringBuilder b = new StringBuilder();
            b.Append(",");
            b.Append(StringEscaper.QuoteEscape(Value));
            b.Append(base.ForgeRawLine());
            return b.ToString();
        }

        public override string ToString()
        {
            return ForgeRawLine();
        }
    }

    public enum UIInfo_TextLabel_Style
    {
        Normal, Bold, Italic, Underline, Strike
    }

    [Serializable]
    public class UIInfo_TextLabel : UIInfo
    {
        public int FontSize;
        public UIInfo_TextLabel_Style Style;

        public UIInfo_TextLabel(string tooltip, int fontSize, UIInfo_TextLabel_Style style)
            : base(tooltip)
        {
            this.FontSize = fontSize;
            this.Style = style;
        }

        public override string ForgeRawLine()
        {
            StringBuilder b = new StringBuilder();
            b.Append(",");
            b.Append(FontSize);
            b.Append(",");
            b.Append(Style.ToString());
            b.Append(base.ForgeRawLine());
            return b.ToString(); 
        }

        public override string ToString()
        {
            return ForgeRawLine();
        }
    }

    [Serializable]
    public class UIInfo_NumberBox : UIInfo
    {
        public int Value;
        public int Min;
        public int Max;
        public int Interval;

        public UIInfo_NumberBox(string tooltip,  int value, int min, int max, int interval)
            : base(tooltip)
        {
            this.Value = value;
            this.Min = min;
            this.Max = max;
            this.Interval = interval;
        }

        public override string ForgeRawLine()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(",");
            builder.Append(Value);
            builder.Append(",");
            builder.Append(Min);
            builder.Append(",");
            builder.Append(Max);
            builder.Append(",");
            builder.Append(Interval);
            builder.Append(base.ForgeRawLine());
            return builder.ToString();
        }

        public override string ToString()
        {
            return ForgeRawLine();
        }
    }

    [Serializable]
    public class UIInfo_CheckBox : UIInfo
    {
        public bool Value;
        public string SectionName; // Optional
        public bool HideProgress; // Optional

        public UIInfo_CheckBox(string tooltip, bool value, string sectionName = null, bool hideProgress = false)
            : base(tooltip)
        {
            this.Value = value;
            this.SectionName = sectionName;
            this.HideProgress = hideProgress;
        }

        public override string ForgeRawLine()
        {
            StringBuilder b = new StringBuilder();
            if (Value)
                b.Append(",True");
            else
                b.Append(",False");
            if (SectionName != null)
            {
                b.Append(",_");
                b.Append(SectionName);
                b.Append("_");
                if (HideProgress)
                    b.Append(",True");
                else
                    b.Append(",False");
            }
            b.Append(base.ForgeRawLine());
            return b.ToString();
        }

        public override string ToString()
        {
            return ForgeRawLine();
        }
    }

    [Serializable]
    public class UIInfo_ComboBox : UIInfo
    {
        public List<string> Items;
        public int Index;
        public string SectionName; // Optional
        public bool HideProgress; // Optional

        public UIInfo_ComboBox(string tooltip,  List<string> items, int index, string sectionName = null, bool hideProgress = false)
            : base(tooltip)
        {
            Items = items;
            Index = index;
            SectionName = sectionName;
            HideProgress = hideProgress;
        }

        public override string ForgeRawLine()
        {
            StringBuilder b = new StringBuilder();
            for (int i = 0; i < Items.Count - 1; i++)
            {
                b.Append(",");
                b.Append(StringEscaper.QuoteEscape(Items[i]));
            }
            b.Append(",");
            b.Append(StringEscaper.QuoteEscape(Items.Last()));
            if (SectionName != null)
            {
                b.Append(",_");
                b.Append(SectionName);
                b.Append("_");
                if (HideProgress)
                    b.Append(",True");
                else
                    b.Append(",False");
            }
            b.Append(base.ForgeRawLine());
            return b.ToString();
        }

        public override string ToString()
        {
            return ForgeRawLine();
        }
    }

    [Serializable]
    public class UIInfo_Image : UIInfo
    {
        public string URL; // optional

        public UIInfo_Image(string toolTip, string url)
            : base(toolTip)
        {
            URL = url;
        }

        public override string ForgeRawLine()
        {
            StringBuilder b = new StringBuilder();
            b.Append(",");
            b.Append(URL);
            b.Append(base.ForgeRawLine());
            return b.ToString();
        }

        public override string ToString()
        {
            return ForgeRawLine();
        }
    }

    [Serializable]
    public class UIInfo_TextFile : UIInfo
    {
        public UIInfo_TextFile(string tooltip)
            : base(tooltip)
        {

        }

        public override string ForgeRawLine()
        {
            return base.ForgeRawLine();
        }

        public override string ToString()
        {
            return ForgeRawLine();
        }
    }

    [Serializable]
    public class UIInfo_Button : UIInfo
    {
        // Still had not figured why SectionName and ProgressShow duplicate
        public string SectionName;
        public string Picture; // Optional
        public bool ShowProgress; // Optional

        public UIInfo_Button(string tooltip, string sectionName, string picture, bool hideProgress)
            : base(tooltip)
        {
            this.SectionName = sectionName;
            this.Picture = picture;
            this.ShowProgress = hideProgress;
        }

        public override string ForgeRawLine()
        {
            StringBuilder b = new StringBuilder();
            b.Append(",");
            b.Append(SectionName);
            b.Append(",");
            if (Picture != null)
                b.Append(Picture);
            else
                b.Append("0");
            if (ShowProgress)
                b.Append(",True");
            else
                b.Append(",False");
            b.Append(base.ForgeRawLine());
            return b.ToString();

            /*
            StringBuilder builder = new StringBuilder();
            builder.Append(SectionName);
            builder.Append(",");
            if (Picture != null)
                builder.Append(Picture);
            else
                builder.Append("0");
            builder.Append(",");
            if (ShowProgress)
                builder.Append("True");
            else
                builder.Append("False");
            builder.Append(",");
            builder.Append(SectionName);
            builder.Append(",");
            if (ShowProgress)
                builder.Append("True");
            else
                builder.Append("False");
            builder.Append(base.ForgeRawLine());
            return builder.ToString();
            */
        }

        public override string ToString()
        {
            return ForgeRawLine();
        }
    }

    [Serializable]
    public class UIInfo_CheckList : UIInfo
    {
        public UIInfo_CheckList(string tooltip)
            : base(tooltip)
        {

        }

        public override string ForgeRawLine()
        {
            return base.ForgeRawLine();
        }

        public override string ToString()
        {
            return ForgeRawLine();
        }
    }

    [Serializable]
    public class UIInfo_WebLabel : UIInfo
    {
        public string URL;

        public UIInfo_WebLabel(string tooltip,  string url) 
            : base(tooltip)
        {
            this.URL = url;
        }

        public override string ForgeRawLine()
        {
            StringBuilder b = new StringBuilder();
            b.Append(",");
            b.Append(StringEscaper.Escape(URL));
            b.Append(base.ForgeRawLine());
            return b.ToString();
        }

        public override string ToString()
        {
            return ForgeRawLine();
        }
    }

    [Serializable]
    public class UIInfo_RadioButton : UIInfo
    {
        public bool Selected;
        public string SectionName; // Optional
        public bool HideProgress; // Optional

        public UIInfo_RadioButton(string tooltip, bool selected, string sectionName = null, bool hideProgress = false) 
            : base(tooltip)
        {
            this.Selected = selected;
            this.SectionName = sectionName;
            this.HideProgress = hideProgress;
        }

        public override string ForgeRawLine()
        {
            StringBuilder b = new StringBuilder();
            b.Append(",");
            b.Append(Selected);
            if (SectionName != null)
            {
                b.Append(",_");
                b.Append(SectionName);
                b.Append("_");
                if (HideProgress)
                    b.Append(",True");
                else
                    b.Append(",False");
            }
            b.Append(base.ForgeRawLine());
            return b.ToString();
        }

        public override string ToString()
        {
            return ForgeRawLine();
        }

    }

    public enum UIInfo_BevelCaption_Style
    {
        Normal, Bold
    }

    [Serializable]
    public class UIInfo_Bevel : UIInfo
    {
        public int? FontSize;
        public UIInfo_BevelCaption_Style? Style;

        public UIInfo_Bevel(string tooltip, int? fontSize, UIInfo_BevelCaption_Style? style)
            : base(tooltip)
        {
            this.FontSize = fontSize;
            this.Style = style;
        }

        public override string ForgeRawLine()
        {
            StringBuilder b = new StringBuilder();
            if (FontSize != null)
            {
                b.Append(",");
                b.Append(FontSize);
                if (Style != null)
                {
                    b.Append(",");
                    b.Append(Style.ToString());
                }
            }
            b.Append(base.ForgeRawLine());
            return b.ToString();
        }

        public override string ToString()
        {
            return ForgeRawLine();
        }
    }

    [Serializable]
    public class UIInfo_FileBox : UIInfo
    {
        public bool IsFile;

        public UIInfo_FileBox(string tooltip, bool isFile)
            : base(tooltip)
        {
            this.IsFile = isFile;
        }

        public override string ForgeRawLine()
        {
            StringBuilder b = new StringBuilder();
            if (IsFile)
                b.Append(",file");
            else
                b.Append(",dir");
            b.Append(base.ForgeRawLine());
            return b.ToString();
        }

        public override string ToString()
        {
            return ForgeRawLine();
        }
    }

    [Serializable]
    public class UIInfo_RadioGroup : UIInfo
    {
        public List<string> Items;
        public int Selected;
        public string SectionName; // Optional
        public bool HideProgress; // Optional

        public UIInfo_RadioGroup(string tooltip,  List<string> items, int selected, string sectionName = null, bool hideProgress = false)
            : base(tooltip)
        {
            this.Items = items;
            this.Selected = selected;
            this.SectionName = sectionName;
            this.HideProgress = hideProgress;
        }

        public override string ForgeRawLine()
        {
            StringBuilder b = new StringBuilder();
            for (int i = 0; i < Items.Count; i++)
            {
                b.Append(",");
                b.Append(StringEscaper.QuoteEscape(Items[i]));
            }
            b.Append(",");
            b.Append(Selected);
            if (SectionName != null)
            {
                b.Append(",_");
                b.Append(SectionName);
                b.Append("_");
                if (HideProgress)
                    b.Append(",True");
                else
                    b.Append(",False");
            }
            b.Append(base.ForgeRawLine());
            return b.ToString();
        }

        public override string ToString()
        {
            return ForgeRawLine();
        }
    }

    #endregion
}
