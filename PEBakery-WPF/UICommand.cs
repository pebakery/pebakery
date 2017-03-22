using System;
using System.Collections.Generic;
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
    }
    #endregion

    #region UICommandInfo

    public class UICommandInfo
    {
        public bool Valid;
        public string Tooltip; // optional

        public UICommandInfo(bool valid, string tooltip)
        {
            this.Valid = valid;
            this.Tooltip = tooltip;
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
    }

    public class UIInfo_NumberBox : UICommandInfo
    {
        public int Value;
        public int Min;
        public int Max;
        public int IncrementUnit;

        public UIInfo_NumberBox(bool valid, string tooltip,  int value, int min, int max, int incrementUnit)
            : base(valid, tooltip)
        {
            this.Value = value;
            this.Min = min;
            this.Max = max;
            this.IncrementUnit = incrementUnit;
        }
    }

    public class UIInfo_CheckBox : UICommandInfo
    { // TODO: [ButtonOptional]
        public bool Value;
        public string SectionName; // Optional

        public UIInfo_CheckBox(bool valid, string tooltip,  bool value)
            : base(valid, tooltip)
        {
            this.Value = value;
            this.SectionName = null;
        }

        public UIInfo_CheckBox(bool valid, string tooltip,  bool value, string sectionName)
            : base(valid, tooltip)
        {
            this.Value = value;
            this.SectionName = sectionName;
        }
    }

    public class UIInfo_ComboBox : UICommandInfo
    {
        public List<string> Items;

        public UIInfo_ComboBox(bool valid, string tooltip,  List<string> items)
            : base(valid, tooltip)
        {
            Items = items;
        }
    }

    public class UIInfo_Image : UICommandInfo
    {
        public UIInfo_Image(bool valid, string tooltip)
            : base(valid, tooltip)
        {

        }
    }

    public class UIInfo_TextFile : UICommandInfo
    {
        public UIInfo_TextFile(bool valid, string tooltip)
            : base(valid, tooltip)
        {

        }
    }

    public class UIInfo_Button : UICommandInfo
    { // TODO: [ButtonOptional]
        public string SectionName;
        public string Picture; // Optional

        public UIInfo_Button(bool valid, string tooltip,  string sectionName)
            : base(valid, tooltip)
        {
            this.SectionName = sectionName;
            this.Picture = null;
        }

        public UIInfo_Button(bool valid, string tooltip,  string sectionName, string picture)
            : base(valid, tooltip)
        {
            this.SectionName = sectionName;
            this.Picture = picture;
        }
    }

    public class UIInfo_CheckList : UICommandInfo
    {
        public UIInfo_CheckList(bool valid, string tooltip)
            : base(valid, tooltip)
        {

        }
    }

    public class UIInfo_WebLabel : UICommandInfo
    {
        public string URL;

        public UIInfo_WebLabel(bool valid, string tooltip,  string url) : base(valid, tooltip)
        {
            this.URL = url;
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

    }

    public class UIInfo_Bevel : UICommandInfo
    {
        public UIInfo_Bevel(bool valid, string tooltip)
            : base(valid, tooltip)
        {

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
    }

    #endregion
}
