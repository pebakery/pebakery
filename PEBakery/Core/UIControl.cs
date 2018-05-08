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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using PEBakery.Helper;
using PEBakery.IniLib;

// ReSharper disable InconsistentNaming

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
    <Key>=<Text>,Visibility,Type,X,Y,Width,Height,<OptionalValues>,[ToolTip]
    Visibility : 1 or 0
    Type : UIControlType 0 ~ 14

    <Text>
    TextBox     = Caption
    TextLabel   = Caption 
    NumberBox   = <ControlName> 
    CheckBox    = Caption 
    ComboBox    = <SelectedItem> // no number, name of item
    Image       = <FileName> // "none" if no image is set
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
                  <Style> : Normal, Bold (Compatible with WB082)
                            Italic, Underline, Strike (Added in PEBakery)
    NumberBox   = <IntegerValue>,<Min>,<Max>,<IncrementUnit>
    CheckBox    = <BooleanValue>  +[RunOptional]
    ComboBox    = <StringValue1>,<StringValue2>, ... ,<StringValueN>
    Image       = [Url]
    Button      = <SectionToRun>,<Picture>,[HideProgress]  +[UnknownBoolean]  +[RunOptional]
                  [Picture] - 0 if no picture, or encoded file's name.
    WebLabel    = <StringValue> // URL
    RadioButton = <BooleanValue> +[RunOptional]
    FileBox     = [file|dir]
    Bevel       = <FontSize>,<Style> (Added in PEBakery)
                  <Style> : Normal, Bold
    RadioGroup  = <StringValue1>,<StringValue2>, ... ,<StringValueN>,<IntegerIndex>  +[RunOptional]
                  // IntegerIndex : selected index, starting from 0

    [RunOptional]
    For CheckBox, Button, RadioButton, RadioGroup
    <SectionToRun>,<HideProgress>
    
    SectionToRun : (String) SectionName with _ at start and end
    HideProgress : (Bool)   

    [ToolTip]
    <StringValue> : ToolTip to show when mousehover event, always start with __
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
            RawLine = rawLine;
            Addr = addr;

            Key = key;
            Text = string.Empty;
            Visibility = false;
            Type = UIControlType.None;
            Rect = new Rect(0, 0, 0, 0);
        }

        public UIControl(string rawLine, SectionAddress addr, string key, string text, bool visibility, UIControlType type, Rect rect, UIInfo info)
        {
            RawLine = rawLine;
            Addr = addr;

            Key = key;
            Text = text;
            Visibility = visibility;
            Type = type;
            Rect = rect;
            Info = info;
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
            b.Append(Visibility ? "1," : "0,");
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
            return b.ToString();
        }
        #endregion

        #region Update
        public bool Update()
        {
            return Ini.WriteKey(Addr.Script.RealPath, Addr.Section.Name, Key, ForgeRawLine(false));
        }
        
        public static bool Update(List<UIControl> uiCtrls)
        {
            if (uiCtrls.Count == 0)
                return true;

            string fullPath = uiCtrls[0].Addr.Script.RealPath;
            List<IniKey> keys = new List<IniKey>(uiCtrls.Count);
            foreach (UIControl uiCtrl in uiCtrls)
            {
                Debug.Assert(fullPath.Equals(uiCtrl.Addr.Script.RealPath, StringComparison.OrdinalIgnoreCase));

                keys.Add(new IniKey(uiCtrl.Addr.Section.Name, uiCtrl.Key, uiCtrl.ForgeRawLine(false)));
            }

            return Ini.WriteKeys(fullPath, keys);
        }
        #endregion

        #region Delete
        public bool Delete()
        {
            return Ini.DeleteKey(Addr.Script.RealPath, Addr.Section.Name, Key);
        }

        public static bool Delete(List<UIControl> uiCtrls)
        {
            if (uiCtrls.Count == 0)
                return true;

            string fullPath = uiCtrls[0].Addr.Script.RealPath;
            List<IniKey> keys = new List<IniKey>(uiCtrls.Count);
            foreach (UIControl uiCtrl in uiCtrls)
            {
                Debug.Assert(fullPath.Equals(uiCtrl.Addr.Script.RealPath, StringComparison.OrdinalIgnoreCase));

                keys.Add(new IniKey(uiCtrl.Addr.Section.Name, uiCtrl.Key));
            }

            return Ini.DeleteKeys(fullPath, keys).Any(x => x);
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
                        Debug.Assert(Info.GetType() == typeof(UIInfo_TextBox), "Invalid UIInfo");
                        UIInfo_TextBox info = Info as UIInfo_TextBox;
                        Debug.Assert(info != null, "Invalid UIInfo");

                        value = info.Value;
                    }
                    break;
                case UIControlType.NumberBox:
                    {
                        Debug.Assert(Info.GetType() == typeof(UIInfo_NumberBox), "Invalid UIInfo");
                        UIInfo_NumberBox info = Info as UIInfo_NumberBox;
                        Debug.Assert(info != null, "Invalid UIInfo");

                        value = info.Value.ToString();
                    }
                    break;
                case UIControlType.CheckBox:
                    {
                        Debug.Assert(Info.GetType() == typeof(UIInfo_CheckBox), "Invalid UIInfo");
                        UIInfo_CheckBox info = Info as UIInfo_CheckBox;
                        Debug.Assert(info != null, "Invalid UIInfo");

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
                        Debug.Assert(Info.GetType() == typeof(UIInfo_RadioButton), "Invalid UIInfo");
                        UIInfo_RadioButton info = Info as UIInfo_RadioButton;
                        Debug.Assert(info != null, "Invalid UIInfo");

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
                        Debug.Assert(Info.GetType() == typeof(UIInfo_RadioGroup), "Invalid UIInfo");
                        UIInfo_RadioGroup info = Info as UIInfo_RadioGroup;
                        Debug.Assert(info != null, "Invalid UIInfo");

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
                        Debug.Assert(Info.GetType() == typeof(UIInfo_TextBox), "Invalid UIInfo");
                        UIInfo_TextBox uiInfo = Info as UIInfo_TextBox;
                        Debug.Assert(uiInfo != null, "Invalid UIInfo");

                        uiInfo.Value = newValue;

                        logs.Add(new LogInfo(LogState.Success, $"Interface control [{Key}] set to [{newValue}]"));
                        success = true;
                    }
                    break;
                case UIControlType.NumberBox:
                    {
                        Debug.Assert(Info.GetType() == typeof(UIInfo_NumberBox), "Invalid UIInfo");
                        UIInfo_NumberBox uiInfo = Info as UIInfo_NumberBox;
                        Debug.Assert(uiInfo != null, "Invalid UIInfo");

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
                        Debug.Assert(Info.GetType() == typeof(UIInfo_CheckBox), "Invalid UIInfo");
                        UIInfo_CheckBox uiInfo = Info as UIInfo_CheckBox;
                        Debug.Assert(uiInfo != null, "Invalid UIInfo");

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
                        Debug.Assert(Info.GetType() == typeof(UIInfo_ComboBox), "Invalid UIInfo");
                        UIInfo_ComboBox uiInfo = Info as UIInfo_ComboBox;
                        Debug.Assert(uiInfo != null, "Invalid UIInfo");

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
                        Debug.Assert(Info.GetType() == typeof(UIInfo_RadioButton), "Invalid UIInfo");
                        UIInfo_RadioButton uiInfo = Info as UIInfo_RadioButton;
                        Debug.Assert(uiInfo != null, "Invalid UIInfo");

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
                        // Debug.Assert(Info.GetType() == typeof(UIInfo_FileBox), "Invalid UIInfo");
                        // UIInfo_FileBox uiInfo = Info as UIInfo_FileBox;
                        // Debug.Assert(uiInfo != null, "Invalid UIInfo");

                        Text = newValue;

                        logs.Add(new LogInfo(LogState.Success, $"Interface Control [{Key}] set to [{newValue}]"));
                        success = true;
                    }
                    break;
                case UIControlType.RadioGroup:
                    {
                        Debug.Assert(Info.GetType() == typeof(UIInfo_RadioGroup), "Invalid UIInfo");
                        UIInfo_RadioGroup uiInfo = Info as UIInfo_RadioGroup;
                        Debug.Assert(uiInfo != null, "Invalid UIInfo");

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

        #region ReplaceScript
        public bool ReplaceScript(Script sc)
        {
            if (!sc.Sections.ContainsKey(Addr.Section.Name))
                return false;

            ScriptSection newSection = sc.Sections[Addr.Section.Name];
            Addr = new SectionAddress(sc, newSection);
            return true;
        }

        public static bool ReplaceAddress(IEnumerable<UIControl> uiCtrls, Script sc)
        {
            return uiCtrls.All(x => x.ReplaceScript(sc));
        }
        #endregion

        #region UIControl Dictionary 
        public static ReadOnlyDictionary<int, UIControlType> UIControlZeroBasedDict = new ReadOnlyDictionary<int, UIControlType>(
            new Dictionary<int, UIControlType>
            {
                [0] = UIControlType.TextBox,
                [1] = UIControlType.TextLabel,
                [2] = UIControlType.NumberBox,
                [3] = UIControlType.CheckBox,
                [4] = UIControlType.ComboBox,
                [5] = UIControlType.Image,
                [6] = UIControlType.TextFile,
                [7] = UIControlType.Button,
                [8] = UIControlType.WebLabel,
                [9] = UIControlType.RadioButton,
                [10] = UIControlType.Bevel,
                [11] = UIControlType.FileBox,
                [12] = UIControlType.RadioGroup,
            });

        public static string GetUIControlTemplate(UIControlType type, string key)
        {
            switch (type)
            {
                case UIControlType.TextBox:
                    return UIInfo_TextBox.Template(key);
                case UIControlType.TextLabel:
                    return UIInfo_TextLabel.Template(key);
                case UIControlType.NumberBox:
                    return UIInfo_NumberBox.Template(key);
                case UIControlType.CheckBox:
                    return UIInfo_CheckBox.Template(key);
                case UIControlType.ComboBox:
                    return UIInfo_ComboBox.Template(key);
                case UIControlType.Image:
                    return UIInfo_Image.Template(key);
                case UIControlType.TextFile:
                    return UIInfo_TextFile.Template(key);
                case UIControlType.Button:
                    return UIInfo_Button.Template(key);
                case UIControlType.WebLabel:
                    return UIInfo_WebLabel.Template(key);
                case UIControlType.RadioButton:
                    return UIInfo_RadioButton.Template(key);
                case UIControlType.Bevel:
                    return UIInfo_Bevel.Template(key);
                case UIControlType.FileBox:
                    return UIInfo_FileBox.Template(key);
                case UIControlType.RadioGroup:
                    return UIInfo_RadioGroup.Template(key);
                default:
                    throw new InvalidOperationException("Internal Logic Error at UIControl.GetUIControlTemplate");
            }
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
            ToolTip = tooltip;
        }

        #region ForgeRawLine, ToString
        /// <summary>
        /// This function should only be called from child Class
        /// Note : this function includes first ','
        /// </summary>
        /// <returns></returns>
        public virtual string ForgeRawLine()
        {
            if (ToolTip != null)
                return "," + StringEscaper.QuoteEscape($"__{ToolTip}");
            return string.Empty;
        }

        public override string ToString() => ForgeRawLine();
        #endregion

        #region Template
        public static string Template(string key) => string.Empty;
        #endregion
    }

    [Serializable]
    public class UIInfo_TextBox : UIInfo
    {
        public string Value;

        public UIInfo_TextBox(string tooltip,  string str)
            : base(tooltip)
        {
            Value = str;
        }

        public override string ForgeRawLine()
        {
            StringBuilder b = new StringBuilder();
            b.Append(",");
            b.Append(StringEscaper.QuoteEscape(Value));
            b.Append(base.ForgeRawLine());
            return b.ToString();
        }

        public override string ToString() => ForgeRawLine();

        public new static string Template(string key) => $"{key}=Caption,1,0,10,10,200,21,Content";
    }

    public enum UITextStyle
    {
        Normal = 0, Bold, Italic, Underline, Strike
    }

    [Serializable]
    public class UIInfo_TextLabel : UIInfo
    {
        public int FontSize;
        public UITextStyle Style;

        public UIInfo_TextLabel(string tooltip, int fontSize, UITextStyle style)
            : base(tooltip)
        {
            FontSize = fontSize;
            Style = style;
        }

        public override string ForgeRawLine()
        {
            StringBuilder b = new StringBuilder();
            b.Append(",");
            b.Append(FontSize);
            b.Append(",");
            b.Append(Style);
            b.Append(base.ForgeRawLine());
            return b.ToString(); 
        }

        public override string ToString() => ForgeRawLine();

        public new static string Template(string key) => $"{key}=Caption,1,1,10,10,200,16,8,Normal";
    }

    [Serializable]
    public class UIInfo_NumberBox : UIInfo
    {
        public int Value;
        public int Min;
        public int Max;
        public int Interval;

        public UIInfo_NumberBox(string tooltip, int value, int min, int max, int interval)
            : base(tooltip)
        {
            Value = value;
            Min = min;
            Max = max;
            Interval = interval;
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

        public override string ToString() => ForgeRawLine();

        public new static string Template(string key) => $"{key}={key},1,2,10,10,40,22,1,1,100,1";
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
            Value = value;
            SectionName = sectionName;
            HideProgress = hideProgress;
        }

        public override string ForgeRawLine()
        {
            StringBuilder b = new StringBuilder();
            b.Append(Value ? ",True" : ",False");
            if (SectionName != null)
            {
                b.Append(",_");
                b.Append(SectionName);
                b.Append("_");
                b.Append(HideProgress ? ",True" : ",False");
            }
            b.Append(base.ForgeRawLine());
            return b.ToString();
        }

        public override string ToString() => ForgeRawLine();

        public new static string Template(string key) => $"{key}={key},1,3,10,10,200,18,True";
    }

    [Serializable]
    public class UIInfo_ComboBox : UIInfo
    {
        public List<string> Items;
        public int Index; // Zero based index
        public string SectionName; // Optional
        public bool HideProgress; // Optional

        public UIInfo_ComboBox(string tooltip, List<string> items, int index, string sectionName = null, bool hideProgress = false)
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
                b.Append(HideProgress ? ",True" : ",False");
            }
            b.Append(base.ForgeRawLine());
            return b.ToString();
        }

        public override string ToString() => ForgeRawLine();

        public new static string Template(string key) => $"{key}=A,1,4,10,10,150,21,A,B,C,D";
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

        public override string ToString() => ForgeRawLine();

        public new static string Template(string key) => $"{key}=none,1,5,10,10,100,100";

        public const string ImageNone = "none";
    }

    [Serializable]
    public class UIInfo_TextFile : UIInfo
    {
        public UIInfo_TextFile(string tooltip)
            : base(tooltip)
        {

        }

        public override string ToString() => ForgeRawLine();

        public new static string Template(string key) => $"{key}={key},1,6,10,10,200,86";
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
            SectionName = sectionName;
            Picture = picture;
            ShowProgress = hideProgress;
        }

        public override string ForgeRawLine()
        {
            StringBuilder b = new StringBuilder();
            b.Append(",");
            b.Append(SectionName);
            b.Append(",");
            b.Append(Picture ?? "0");
            b.Append(ShowProgress ? ",True" : ",False");
            b.Append(base.ForgeRawLine());
            return b.ToString();
        }

        public override string ToString() => ForgeRawLine();

        public new static string Template(string key) => $"{key}={key},1,8,10,10,80,25,SectionToRun,0,True";
    }

    [Serializable]
    public class UIInfo_WebLabel : UIInfo
    {
        public string URL;

        public UIInfo_WebLabel(string tooltip,  string url) 
            : base(tooltip)
        {
            URL = url;
        }

        public override string ForgeRawLine()
        {
            StringBuilder b = new StringBuilder();
            b.Append(",");
            b.Append(StringEscaper.Escape(URL));
            b.Append(base.ForgeRawLine());
            return b.ToString();
        }

        public override string ToString() => ForgeRawLine();

        public new static string Template(string key) => $"{key}=Content,1,10,10,10,80,200,18,https://github.com/pebakery/pebakery";
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
            Selected = selected;
            SectionName = sectionName;
            HideProgress = hideProgress;
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
                b.Append(HideProgress ? ",True" : ",False");
            }
            b.Append(base.ForgeRawLine());
            return b.ToString();
        }

        public override string ToString() => ForgeRawLine();

        public new static string Template(string key) => $"{key}={key},1,11,10,10,120,20,False";
    }

    public enum UIBevelCaptionStyle
    {
        Normal = 0, Bold
    }

    [Serializable]
    public class UIInfo_Bevel : UIInfo
    {
        public int? FontSize;
        public UIBevelCaptionStyle? Style;

        public UIInfo_Bevel(string tooltip, int? fontSize, UIBevelCaptionStyle? style)
            : base(tooltip)
        {
            FontSize = fontSize;
            Style = style;
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
                    b.Append(Style);
                }
            }
            b.Append(base.ForgeRawLine());
            return b.ToString();
        }

        public override string ToString() => ForgeRawLine();

        public new static string Template(string key) => $"{key}={key},1,12,10,10,160,60";
    }

    [Serializable]
    public class UIInfo_FileBox : UIInfo
    {
        public bool IsFile;

        public UIInfo_FileBox(string tooltip, bool isFile)
            : base(tooltip)
        {
            IsFile = isFile;
        }

        public override string ForgeRawLine()
        {
            StringBuilder b = new StringBuilder();
            b.Append(IsFile ? ",file" : ",dir");
            b.Append(base.ForgeRawLine());
            return b.ToString();
        }

        public override string ToString() => ForgeRawLine();

        public new static string Template(string key) => $"{key}={key},1,13,10,10,200,20,file";
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
            Items = items;
            Selected = selected;
            SectionName = sectionName;
            HideProgress = hideProgress;
        }

        public override string ForgeRawLine()
        {
            StringBuilder b = new StringBuilder();
            foreach (string item in Items)
            {
                b.Append(",");
                b.Append(StringEscaper.QuoteEscape(item));
            }
            b.Append(",");
            b.Append(Selected);
            if (SectionName != null)
            {
                b.Append(",_");
                b.Append(SectionName);
                b.Append("_");
                b.Append(HideProgress ? ",True" : ",False");
            }
            b.Append(base.ForgeRawLine());
            return b.ToString();
        }

        public override string ToString() => ForgeRawLine();

        public new static string Template(string key) => $"{key}={key},1,14,10,10,150,60,A,B,C,1";
    }

    #endregion
}
