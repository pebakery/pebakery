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
        CheckList = 9,
        WebLabel = 10,
        RadioButton = 11,
        Bevel = 12,
        FileBox = 13,
        RadioGroup = 14
    }
    #endregion

    #region Interface Representation Format
    /*
    <Name>=<Text>,Visibility,Type,X,Y,Width,Height,<Variable>,[ButtonOptional],[Tooltip]
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
                  <Style> : Normal, Bold (in 082)
                            Italic, Underline, Strike (Added in PEBakery)
    NumberBox   = <IntegerValue>,<Min>,<Max>,<IncrementUnit>
    CheckBox    = <BooleanValue>,[SectionToRun]   +[OptionalInteger]
    ComboBox    = <StringValue1>,<StringValue2>, ... ,<StringValueN>
    Button      = <SectionToRun>,[Picture]   +[OptionalInteger]
                  [Picture] - 0 if no picture. or its value is Embedded File name.
    WebLabel    = <StringValue> // URL
    RadioButton = <BooleanValue>,[SectionToRun]   +[OptionalInteger]
    FileBox     = [FILE] // If file, FILE. If dir, nothing.
    RadioGroup  = <StringValue1>,<StringValue2>, ... ,<StringValueN>,<IntegerValue> // selected index, starting from 0

    [ButtonOptional]
    For CheckBox, RadioButton, Button
    <BooleanValue> : While running, show progress window or not?
    Need more research

    [Tooltip]
    <StringValue> : Tooltip to show when mousehover event

    */
    #endregion

    #region UICommand
    public class UICommand
    {
        public string RawLine;
        public SectionAddress Addr;

        public string Key;
        public string Text;
        public bool Visibility;
        public UIControlType Type;
        public Rect Rect;
        public UICommandInfo Info;

        public UICommand(string rawLine, SectionAddress addr, string key)
        {
            this.RawLine = rawLine;
            this.Addr = addr;

            this.Key = key;
            this.Text = string.Empty;
            this.Visibility = false;
            this.Type = UIControlType.None;
            this.Rect = new Rect(0, 0, 0, 0);

        }

        public UICommand(string rawLine, SectionAddress addr, string key, string text, bool visibility, UIControlType type, Rect rect, UICommandInfo info)
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
                builder.Append(Engine.QuoteEscapeStr(Key));
                builder.Append("=");
            }

            builder.Append(Engine.QuoteEscapeStr(Text));
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
    }
    #endregion

    #region UICommandInfo

    public class UICommandInfo
    {
        public bool Valid;
        public string ToolTip; // optional

        public UICommandInfo(bool valid, string tooltip)
        {
            this.Valid = valid;
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
                return "," + Engine.EscapeStr(ToolTip);
            else
                return string.Empty;
        }

        public override string ToString()
        {
            return ForgeRawLine();
        }
    }

    public class UIInfo_TextBox : UICommandInfo
    {
        public string Value;

        public UIInfo_TextBox(bool valid, string tooltip,  string str)
            : base(valid, tooltip)
        {
            this.Value = str;
        }

        public override string ForgeRawLine()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(Engine.QuoteEscapeStr(Value));
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

    public class UIInfo_TextLabel : UICommandInfo
    {
        public int FontSize;
        public UIInfo_TextLabel_Style Style;

        public UIInfo_TextLabel(bool valid, string tooltip, int fontSize, UIInfo_TextLabel_Style style)
            : base(valid, tooltip)
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

    public class UIInfo_NumberBox : UICommandInfo
    {
        public int Value;
        public int Min;
        public int Max;
        public int Interval;

        public UIInfo_NumberBox(bool valid, string tooltip,  int value, int min, int max, int interval)
            : base(valid, tooltip)
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

    public class UIInfo_CheckBox : UICommandInfo
    {
        public bool Value;
        public string SectionName; // Optional

        public UIInfo_CheckBox(bool valid, string tooltip, bool value, string sectionName = null)
            : base(valid, tooltip)
        {
            this.Value = value;
            this.SectionName = sectionName;
        }

        public override string ForgeRawLine()
        {
            StringBuilder builder = new StringBuilder();
            if (Value)
                builder.Append("True");
            else
                builder.Append("False");
            if (SectionName != null)
            {
                builder.Append(",");
                builder.Append(SectionName);
            }
            builder.Append(base.ForgeRawLine());
            return builder.ToString();
        }

        public override string ToString()
        {
            return ForgeRawLine();
        }
    }

    public class UIInfo_ComboBox : UICommandInfo
    {
        public List<string> Items;
        public int Index;

        public UIInfo_ComboBox(bool valid, string tooltip,  List<string> items, int index)
            : base(valid, tooltip)
        {
            Items = items;
            Index = index;
        }

        public override string ForgeRawLine()
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < Items.Count - 1; i++)
            {
                builder.Append(Engine.QuoteEscapeStr(Items[i]));
                builder.Append(",");
            }
            builder.Append(Items.Last());
            builder.Append(base.ForgeRawLine());
            return builder.ToString();
        }

        public override string ToString()
        {
            return ForgeRawLine();
        }
    }

    public class UIInfo_Image : UICommandInfo
    {
        public string URL; // optional

        public UIInfo_Image(bool valid, string toolTip, string url)
            : base(valid, toolTip)
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

    public class UIInfo_TextFile : UICommandInfo
    {
        public UIInfo_TextFile(bool valid, string tooltip)
            : base(valid, tooltip)
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

    public class UIInfo_Button : UICommandInfo
    {
        // Still had not figured why SectionName and ProgressShow duplicate
        public string SectionName;
        public string Picture; // Optional
        public bool ProgressShow;

        public UIInfo_Button(bool valid, string tooltip, string sectionName, string picture, bool progressShow)
            : base(valid, tooltip)
        {
            this.SectionName = sectionName;
            this.Picture = picture;
            this.ProgressShow = progressShow;
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
            if (ProgressShow)
                builder.Append("True");
            else
                builder.Append("False");
            builder.Append(",");
            builder.Append("_");
            builder.Append(SectionName);
            builder.Append("_");
            builder.Append(",");
            if (ProgressShow)
                builder.Append("True");
            else
                builder.Append("False");
            builder.Append(base.ForgeRawLine());
            return builder.ToString();
        }

        public override string ToString()
        {
            return ForgeRawLine();
        }
    }

    public class UIInfo_CheckList : UICommandInfo
    {
        public UIInfo_CheckList(bool valid, string tooltip)
            : base(valid, tooltip)
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

    public class UIInfo_WebLabel : UICommandInfo
    {
        public string URL;

        public UIInfo_WebLabel(bool valid, string tooltip,  string url) : base(valid, tooltip)
        {
            this.URL = url;
        }

        public override string ForgeRawLine()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(Engine.EscapeStr(URL));
            builder.Append(base.ForgeRawLine());
            return builder.ToString();
        }

        public override string ToString()
        {
            return ForgeRawLine();
        }
    }

    public class UIInfo_RadioButton : UICommandInfo
    { // TODO: [ButtonOptional]
        public bool Selected;
        public string SectionName; // optional

        public UIInfo_RadioButton(bool valid, string tooltip,  bool selected) : base(valid, tooltip)
        {
            this.Selected = selected;
            this.SectionName = null;
        }

        public UIInfo_RadioButton(bool valid, string tooltip,  bool selected, string sectionName) : base(valid, tooltip)
        {
            this.Selected = selected;
            this.SectionName = sectionName;
        }

        public override string ForgeRawLine()
        {
            StringBuilder builder = new StringBuilder();
            return builder.ToString();
        }

        public override string ToString()
        {
            return ForgeRawLine();
        }

    }

    public class UIInfo_Bevel : UICommandInfo
    {
        public UIInfo_Bevel(bool valid, string tooltip)
            : base(valid, tooltip)
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

    public class UIInfo_FileBox : UICommandInfo
    {
        public bool IsFile;

        public UIInfo_FileBox(bool valid, string tooltip,  bool isFile)
            : base(valid, tooltip)
        {
            this.IsFile = isFile;
        }

        public override string ForgeRawLine()
        {
            StringBuilder builder = new StringBuilder();
            return builder.ToString();
        }

        public override string ToString()
        {
            return ForgeRawLine();
        }
    }

    public class UIInfo_RadioGroup : UICommandInfo
    {
        public List<string> Items;
        public int Selected;

        public UIInfo_RadioGroup(bool valid, string tooltip,  List<string> items, int selected)
            : base(valid, tooltip)
        {
            this.Items = items;
            this.Selected = selected;
        }

        public override string ForgeRawLine()
        {
            StringBuilder builder = new StringBuilder();
            return builder.ToString();
        }

        public override string ToString()
        {
            return ForgeRawLine();
        }
    }

    #endregion
}
