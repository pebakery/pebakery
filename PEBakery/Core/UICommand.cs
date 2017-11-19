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
    #region Enum PluginUIControlType
    public enum UIType
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
    <Name>=<Text>,Visibility,Type,X,Y,Width,Height,<Variable>,[Tooltip]
    Visibility : 1 or 0
    Type : PluginInterfaceControlType 0 ~ 14

    <Text>
    TextBox     = Caption
    TextLabel   = Caption 
    NumberBox   = <ComponentName> 
    CheckBox    = Caption 
    ComboBox    = <SelectedItem> // no number, name of item
    Image       = <FileName> 
    TextFile    = <FileName> 
    Button      = Caption 
    CheckList   = <SectionName>
                  That section must contains lists, by this format
                  Ex) <Key>=<IntegerValue> // 0 or 1, checked or not
    WebLabel    = Caption 
    RadioButton = Caption 
    Bevel       = <ComponentName> 
    FileBox     = <Path> // It can be file or directory
    RadioGroup  = Caption 

    <Variable>
    TextBox     = <StringValue>
    TextLabel   = <FontSize>,<Style>
                  <Style> : Normal, Bold (in WB082)
                            Italic, Underline, Strike (Added in PEBakery)
    NumberBox   = <IntegerValue>,<Min>,<Max>,<IncrementUnit>
    CheckBox    = <BooleanValue>,[SectionToRun]  +[ButtonOptional]
    ComboBox    = <StringValue1>,<StringValue2>, ... ,<StringValueN>
    Button      = <SectionToRun>,<Picture>,[HideProgress]  +[UnknownBoolean] +[RunOptional]
                  [Picture] - 0 if no picture. or its value is Embedded File name.
    WebLabel    = <StringValue> // URL
    RadioButton = <BooleanValue> +[RunOptional]
    FileBox     = [FILE] // If file, FILE. If dir, nothing.
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

    #region UICommand
    [Serializable]
    public class UICommand
    {
        public string RawLine;
        public SectionAddress Addr;

        public string Key;
        public string Text;
        public bool Visibility;
        public UIType Type;
        public Rect Rect;
        public UIInfo Info;

        public UICommand(string rawLine, SectionAddress addr, string key)
        {
            this.RawLine = rawLine;
            this.Addr = addr;

            this.Key = key;
            this.Text = string.Empty;
            this.Visibility = false;
            this.Type = UIType.None;
            this.Rect = new Rect(0, 0, 0, 0);

        }

        public UICommand(string rawLine, SectionAddress addr, string key, string text, bool visibility, UIType type, Rect rect, UIInfo info)
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

        public override string ToString()
        {
            return ForgeRawLine(true);
        }

        public string ForgeRawLine(bool includeKey)
        {
            StringBuilder builder = new StringBuilder();
            if (includeKey)
            {
                builder.Append(StringEscaper.QuoteEscape(Key));
                builder.Append("=");
            }

            builder.Append(StringEscaper.QuoteEscape(Text));
            builder.Append(",");
            if (Visibility)
                builder.Append("1,");
            else
                builder.Append("0,");
            builder.Append((int) Type);
            builder.Append(",");
            builder.Append(Rect.Left);
            builder.Append(",");
            builder.Append(Rect.Top);
            builder.Append(",");
            builder.Append(Rect.Width);
            builder.Append(",");
            builder.Append(Rect.Height);
            builder.Append(",");
            builder.Append(Info.ForgeRawLine());
            return builder.ToString();
        }

        public void Update()
        {
            Ini.SetKey(Addr.Plugin.FullPath, new IniKey(Addr.Section.SectionName, Key, ForgeRawLine(false)));
        }
        
        public static void Update(List<UICommand> uiCmdList)
        {
            List<IniKey> keys = new List<IniKey>(uiCmdList.Count);
            foreach (UICommand uiCmd in uiCmdList)
                keys.Add(new IniKey(uiCmd.Addr.Section.SectionName, uiCmd.Key, uiCmd.ForgeRawLine(false)));

            Ini.SetKeys(uiCmdList[0].Addr.Plugin.FullPath, keys);
        }
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
            StringBuilder builder = new StringBuilder();
            builder.Append(StringEscaper.QuoteEscape(Value));
            builder.Append(base.ForgeRawLine());
            return builder.ToString();
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
            StringBuilder builder = new StringBuilder();
            builder.Append(FontSize);
            builder.Append(",");
            builder.Append(Style.ToString());
            builder.Append(base.ForgeRawLine());
            return builder.ToString(); 
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
                b.Append("True");
            else
                b.Append("False");
            if (SectionName != null)
            {
                b.Append(",_");
                b.Append(SectionName);
                b.Append("_,");
                if (HideProgress)
                    b.Append("True");
                else
                    b.Append("False");
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

        public UIInfo_ComboBox(string tooltip,  List<string> items, int index)
            : base(tooltip)
        {
            Items = items;
            Index = index;
        }

        public override string ForgeRawLine()
        {
            StringBuilder b = new StringBuilder();
            for (int i = 0; i < Items.Count - 1; i++)
            {
                b.Append(StringEscaper.QuoteEscape(Items[i]));
                b.Append(",");
            }
            b.Append(StringEscaper.QuoteEscape(Items.Last()));
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
            StringBuilder builder = new StringBuilder();
            builder.Append(URL);
            builder.Append(base.ForgeRawLine());
            return builder.ToString();
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
            builder.Append(base.ForgeRawLine());
            return builder.ToString();

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
            StringBuilder builder = new StringBuilder();
            builder.Append(StringEscaper.Escape(URL));
            builder.Append(base.ForgeRawLine());
            return builder.ToString();
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
            b.Append(Selected);
            if (SectionName != null)
            {
                b.Append(",_");
                b.Append(SectionName);
                b.Append("_,");
                if (HideProgress)
                    b.Append("True");
                else
                    b.Append("False");
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
    public class UIInfo_Bevel : UIInfo
    {
        public UIInfo_Bevel(string tooltip)
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
                b.Append("FILE");
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
                b.Append(StringEscaper.QuoteEscape(Items[i]));
                b.Append(",");
            }
            b.Append(Selected);
            if (SectionName != null)
            {
                b.Append(",_");
                b.Append(SectionName);
                b.Append("_,");
                if (HideProgress)
                    b.Append("True");
                else
                    b.Append("False");
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
