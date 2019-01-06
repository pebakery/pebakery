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
using PEBakery.Ini;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
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
    Type : UIControlType 0 ~ 14 (Except 7, 9)

    <Text>
     0 TextBox     = Caption
     1 TextLabel   = Caption 
     2 NumberBox   = <ControlName> 
     3 CheckBox    = Caption 
     4 ComboBox    = <SelectedItem> // no number, name of item
     5 Image       = <FileName> // "none" if image is not set
     6 TextFile    = <FileName> // "none" if text file is not set
     8 Button      = Caption 
    10 WebLabel    = Caption 
    11 RadioButton = Caption 
    12 Bevel       = Caption // If set to <ControlName> caption will be hidden. (For compatibility with scripts built in WB editor)
    13 FileBox     = <Path>  // It can be file or directory
    14 RadioGroup  = Caption 

    <OptionalValues>
     0 TextBox     = <StringValue>
     1 TextLabel   = <FontSize>,<FontWeight>,[FontStyle] 
                     <FontSize>   : Default 8
                     <FontWeight> : Normal, Bold (Compatible with WB082)
                     [FontStyle]  : Italic, Underline, Strike (Added in PEBakery) 
     2 NumberBox   = <IntegerValue>,<Min>,<Max>,<Tick>
     3 CheckBox    = <BooleanValue>  +[RunOptional]
     4 ComboBox    = <StringValue1>,<StringValue2>, ... ,<StringValueN>  +[RunOptional]
     5 Image       = [Url]
     8 Button      = <SectionName>,<Picture>,[HideProgress]  +[OldToolTip]  +[RunOptional]
                     [Picture] - 0 if no picture, or encoded file's name.
                     [OldToolTip] - ignored, originally used by WB077b2 ~ WB078
                     [RunOptional] - ignored
    10 WebLabel    = <StringValue> // URL
    11 RadioButton = <BooleanValue> +[RunOptional]
    12 Bevel       = [FontSize],[FontWeight],[FontStyle]
                     [FontSize]   : Default 8 (Added in PEBakery) 
                     [FontWeight] : Normal, Bold (Added in PEBakery) 
                     [FontStyle]  : Italic, Underline, Strike (Added in PEBakery) 
    13 FileBox     = [file|dir]
    14 RadioGroup  = <StringValue1>,<StringValue2>, ... ,<StringValueN>,<IntegerIndex>  +[RunOptional]
                     // IntegerIndex : selected index, starting from 0

    [RunOptional]
    For CheckBox, ComboBox, RadioButton, RadioGroup
    <SectionName>,<HideProgress>
    
    SectionToRun : (String) SectionName with _ at start and end
    HideProgress : (Bool)   

    [ToolTip]
    <StringValue> : ToolTip to show at mousehover event, always start with __
    */
    #endregion

    #region UIControl
    [Serializable]
    public class UIControl
    {
        #region Fields
        public string RawLine;
        public ScriptSection Section;

        public string Key;
        public string Text;
        public bool Visibility;
        public UIControlType Type;
        public int X;
        public int Y;
        public int Width;
        public int Height;
        public UIInfo Info;
        public int LineIdx;
        #endregion

        #region Properties
        public Rect Rect => new Rect(X, Y, Width, Height);
        #endregion

        #region Constructors
        public UIControl(string rawLine, ScriptSection section, string key)
        {
            RawLine = rawLine;
            Section = section;

            Key = key;
            Text = string.Empty;
            Visibility = false;
            Type = UIControlType.None;
            X = 0;
            Y = 0;
            Width = 0;
            Height = 0;
            Info = null;
            LineIdx = 0;
        }

        public UIControl(string rawLine, ScriptSection section, string key, string text, bool visibility, UIControlType type, int x, int y, int width, int height, UIInfo info, int lineIdx)
        {
            RawLine = rawLine;
            Section = section;

            Key = key;
            Text = text;
            Visibility = visibility;
            Type = type;
            X = x;
            Y = y;
            Width = width;
            Height = height;
            Info = info;
            LineIdx = lineIdx;
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
                b.Append(Key);
                b.Append("=");
            }
            b.Append(StringEscaper.QuoteEscape(Text));
            b.Append(",");
            b.Append(Visibility ? "1," : "0,");
            b.Append((int)Type);
            b.Append(",");
            b.Append(X);
            b.Append(",");
            b.Append(Y);
            b.Append(",");
            b.Append(Width);
            b.Append(",");
            b.Append(Height);
            b.Append(Info.ForgeRawLine());
            return b.ToString();
        }
        #endregion

        #region Update
        public bool Update()
        {
            // Update ScriptSection.Lines
            Section.UpdateIniKey(Key, ForgeRawLine(false));

            // Update actual file
            return IniReadWriter.WriteKey(Section.Script.RealPath, Section.Name, Key, ForgeRawLine(false));
        }

        public static bool Update(List<UIControl> uiCtrls)
        {
            if (uiCtrls.Count == 0)
                return true;

            string fullPath = uiCtrls[0].Section.Script.RealPath;
            List<IniKey> keys = new List<IniKey>(uiCtrls.Count);
            foreach (UIControl uiCtrl in uiCtrls)
            {
                Debug.Assert(fullPath.Equals(uiCtrl.Section.Script.RealPath, StringComparison.OrdinalIgnoreCase));

                // Update ScriptSection.Lines
                uiCtrl.Section.UpdateIniKey(uiCtrl.Key, uiCtrl.ForgeRawLine(false));

                // Prepare updating actual file
                keys.Add(new IniKey(uiCtrl.Section.Name, uiCtrl.Key, uiCtrl.ForgeRawLine(false)));
            }

            // Update actual file
            return IniReadWriter.WriteKeys(fullPath, keys);
        }
        #endregion

        #region Delete
        public bool Delete()
        {
            return IniReadWriter.DeleteKey(Section.Script.RealPath, Section.Name, Key);
        }

        public static bool Delete(List<UIControl> uiCtrls)
        {
            if (uiCtrls.Count == 0)
                return true;

            string fullPath = uiCtrls[0].Section.Script.RealPath;
            List<IniKey> keys = new List<IniKey>(uiCtrls.Count);
            foreach (UIControl uiCtrl in uiCtrls)
            {
                Debug.Assert(fullPath.Equals(uiCtrl.Section.Script.RealPath, StringComparison.OrdinalIgnoreCase));

                keys.Add(new IniKey(uiCtrl.Section.Name, uiCtrl.Key));
            }

            return IniReadWriter.DeleteKeys(fullPath, keys).Any(x => x);
        }
        #endregion

        #region GetValue, SetValue
        public string GetValue(bool strict)
        {
            string value = null;
            switch (Type)
            {
                case UIControlType.TextLabel:
                    if (strict)
                        value = Text;
                    break;
                case UIControlType.TextBox:
                    {
                        UIInfo_TextBox info = Info.Cast<UIInfo_TextBox>();
                        value = info.Value;
                    }
                    break;
                case UIControlType.NumberBox:
                    {
                        UIInfo_NumberBox info = Info.Cast<UIInfo_NumberBox>();
                        value = info.Value.ToString();
                    }
                    break;
                case UIControlType.CheckBox:
                    {
                        UIInfo_CheckBox info = Info.Cast<UIInfo_CheckBox>();
                        value = info.Value ? "True" : "False";
                    }
                    break;
                case UIControlType.ComboBox:
                    value = Text;
                    break;
                case UIControlType.RadioButton:
                    {
                        UIInfo_RadioButton info = Info.Cast<UIInfo_RadioButton>();
                        value = info.Selected ? "True" : "False";
                    }
                    break;
                case UIControlType.FileBox:
                    value = Text;
                    break;
                case UIControlType.RadioGroup:
                    {
                        UIInfo_RadioGroup info = Info.Cast<UIInfo_RadioGroup>();
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
                case UIControlType.TextLabel:
                    Text = newValue;

                    logs.Add(new LogInfo(LogState.Success, $"Interface control [{Key}] set to [{newValue}]"));
                    success = true;
                    break;
                case UIControlType.TextBox:
                    {
                        UIInfo_TextBox uiInfo = Info.Cast<UIInfo_TextBox>();
                        uiInfo.Value = newValue;

                        logs.Add(new LogInfo(LogState.Success, $"Interface control [{Key}] set to [{newValue}]"));
                        success = true;
                    }
                    break;
                case UIControlType.NumberBox:
                    {
                        UIInfo_NumberBox uiInfo = Info.Cast<UIInfo_NumberBox>();

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
                        UIInfo_CheckBox uiInfo = Info.Cast<UIInfo_CheckBox>();

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
                        UIInfo_ComboBox uiInfo = Info.Cast<UIInfo_ComboBox>();

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
                        UIInfo_RadioButton uiInfo = Info.Cast<UIInfo_RadioButton>();

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
                    Text = newValue;

                    logs.Add(new LogInfo(LogState.Success, $"Interface Control [{Key}] set to [{newValue}]"));
                    success = true;
                    break;
                case UIControlType.RadioGroup:
                    {
                        UIInfo_RadioGroup uiInfo = Info.Cast<UIInfo_RadioGroup>();

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
            if (!sc.Sections.ContainsKey(Section.Name))
                return false;

            Section = sc.Sections[Section.Name];
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
                [-1] = UIControlType.None,
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

        #region Const
        public const int DefaultFontPoint = 8; // WB082 hard-coded default font point to 8.
        public const double PointToDeviceIndependentPixel = 96f / 72f; // Point - 72DPI, Device Independent Pixel - 96DPI
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

        #region Cast
        /// <summary>
        /// Type safe casting helper
        /// </summary>
        /// <typeparam name="T">Child of UIInfo</typeparam>
        /// <returns>UIInfo casted as T</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Cast<T>() where T : UIInfo
        {
            Debug.Assert(GetType() == typeof(T), "Invalid UIInfo");
            T cast = this as T;
            Debug.Assert(cast != null, "Invalid UIInfo");
            return cast;
        }

        /// <summary>
        /// Type safe casting helper
        /// </summary>
        /// <typeparam name="T">Child of UIInfo</typeparam>
        /// <returns>UIInfo casted as T</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Cast<T>(UIInfo info) where T : UIInfo
        {
            return info.Cast<T>();
        }
        #endregion

        #region ForgeRawLine, ToString
        /// <summary>
        /// This function should only be called from child class
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

    public enum UIFontWeight
    {
        Normal = 0, Bold,
    }

    public enum UIFontStyle
    {
        Italic, Underline, Strike
    }

    [Serializable]
    public class UIInfo_TextBox : UIInfo
    {
        public string Value;

        public UIInfo_TextBox(string tooltip, string str)
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

        #region Const
        public const double AddWidth = UIControl.PointToDeviceIndependentPixel * UIControl.DefaultFontPoint * 1.2;
        #endregion
    }

    [Serializable]
    public class UIInfo_TextLabel : UIInfo
    {
        public int FontSize;
        public UIFontWeight FontWeight;
        public UIFontStyle? FontStyle;

        public UIInfo_TextLabel(string tooltip, int fontSize, UIFontWeight fontWeight, UIFontStyle? fontStyle)
            : base(tooltip)
        {
            FontSize = fontSize;
            FontWeight = fontWeight;
            FontStyle = fontStyle;
        }

        public override string ForgeRawLine()
        {
            StringBuilder b = new StringBuilder();
            b.Append(",");
            b.Append(FontSize);
            b.Append(",");
            b.Append(FontWeight);
            if (FontStyle != null)
            {
                b.Append(",");
                b.Append(FontStyle);
            }
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
        public int Tick;

        public UIInfo_NumberBox(string tooltip, int value, int min, int max, int tick)
            : base(tooltip)
        {
            Value = value;
            Min = min;
            Max = max;
            Tick = tick;
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
            builder.Append(Tick);
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

        public new static string Template(string key) => $"{key}=A,1,4,10,10,150,21,A,B,C";
    }

    [Serializable]
    public class UIInfo_Image : UIInfo
    {
        public string Url; // optional

        public UIInfo_Image(string toolTip, string url)
            : base(toolTip)
        {
            Url = url;
        }

        public override string ForgeRawLine()
        {
            StringBuilder b = new StringBuilder();
            if (Url != null)
            {
                b.Append(",");
                b.Append(Url);
            }
            b.Append(base.ForgeRawLine());
            return b.ToString();
        }

        public override string ToString() => ForgeRawLine();

        public new static string Template(string key) => $"{key}=none,1,5,10,10,100,100";

        #region Const None
        public const string NoResource = "none";
        #endregion
    }

    [Serializable]
    public class UIInfo_TextFile : UIInfo
    {
        public UIInfo_TextFile(string tooltip)
            : base(tooltip)
        {

        }

        public override string ToString() => ForgeRawLine();

        public new static string Template(string key) => $"{key}=none,1,6,10,10,200,200";

        #region Const None
        public const string NoResource = "none";
        #endregion
    }

    [Serializable]
    public class UIInfo_Button : UIInfo
    {
        public string SectionName;
        public string Picture; // Optional
        public bool HideProgress; // Optional

        public UIInfo_Button(string tooltip, string sectionName, string picture, bool hideProgress)
            : base(tooltip)
        {
            SectionName = sectionName;
            Picture = picture;
            HideProgress = hideProgress;
        }

        public override string ForgeRawLine()
        {
            StringBuilder b = new StringBuilder();
            b.Append(",");
            b.Append(SectionName);
            b.Append(",");
            b.Append(Picture ?? "0");
            b.Append(HideProgress ? ",True" : ",False");
            b.Append(base.ForgeRawLine());
            return b.ToString();
        }

        public override string ToString() => ForgeRawLine();

        public new static string Template(string key) => $"{key}={key},1,8,10,10,80,25,SectionName,0,True";

        #region Const 0
        public const string NoPicture = "0";
        #endregion
    }

    [Serializable]
    public class UIInfo_WebLabel : UIInfo
    {
        public string Url;

        public UIInfo_WebLabel(string tooltip, string url)
            : base(tooltip)
        {
            Url = url;
        }

        public override string ForgeRawLine()
        {
            StringBuilder b = new StringBuilder();
            b.Append(",");
            b.Append(StringEscaper.Escape(Url));
            b.Append(base.ForgeRawLine());
            return b.ToString();
        }

        public override string ToString() => ForgeRawLine();

        public new static string Template(string key) => $"{key}=Caption,1,10,10,10,200,18,https://github.com/pebakery/pebakery";
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

    [Serializable]
    public class UIInfo_Bevel : UIInfo
    {
        public int? FontSize;
        public UIFontWeight? FontWeight;
        public UIFontStyle? FontStyle;

        public UIInfo_Bevel(string tooltip, int? fontSize, UIFontWeight? fontWeight, UIFontStyle? fontStyle)
            : base(tooltip)
        {
            FontSize = fontSize;
            FontWeight = fontWeight;
            FontStyle = fontStyle;
        }

        public override string ForgeRawLine()
        {
            StringBuilder b = new StringBuilder();
            if (FontSize != null)
            {
                b.Append(",");
                b.Append(FontSize);
                if (FontWeight != null)
                {
                    b.Append(",");
                    b.Append(FontWeight);
                    if (FontStyle != null)
                    {
                        b.Append(",");
                        b.Append(FontStyle);
                    }
                }
            }
            b.Append(base.ForgeRawLine());
            return b.ToString();
        }

        public override string ToString() => ForgeRawLine();

        public new static string Template(string key) => $"{key}={key},1,12,10,10,160,60";

        public bool CaptionEnabled
        {
            get => FontSize != null;
            set
            {
                if (value)
                {
                    if (FontSize == null)
                        FontSize = UIControl.DefaultFontPoint;
                    if (FontWeight == null)
                        FontWeight = UIFontWeight.Normal;
                }
                else
                {
                    FontSize = null;
                    FontWeight = null;
                    FontStyle = null;
                }
            }
        }
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

        public UIInfo_RadioGroup(string tooltip, List<string> items, int selected, string sectionName = null, bool hideProgress = false)
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
