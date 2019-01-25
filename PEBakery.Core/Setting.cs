/*
    Copyright (C) 2018 Hajin Jang
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

using PEBakery.Core.ViewModels;
using PEBakery.Helper;
using PEBakery.Ini;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace PEBakery.Core
{
    #region Setting
    public class Setting
    {
        #region Subclasses
        public class ProjectSetting
        {
            public const string SectionName = "Project";

            public string DefaultProject;

            public ProjectSetting()
            {
                Default();
            }

            public void Default()
            {
                DefaultProject = string.Empty;
            }
        }

        public class GeneralSetting
        {
            public const string SectionName = "General";

            public bool OptimizeCode;
            public bool ShowLogAfterBuild;
            public bool StopBuildOnError;
            public bool EnableLongFilePath;
            public bool UseCustomUserAgent;
            public string CustomUserAgent;

            public GeneralSetting()
            {
                Default();
            }

            public void Default()
            {
                OptimizeCode = true;
                ShowLogAfterBuild = true;
                StopBuildOnError = true;
                EnableLongFilePath = false;
                UseCustomUserAgent = false;
                // Default custom User-Agent is set to Edge's on Windows 10 v1809
                CustomUserAgent = @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/64.0.3282.140 Safari/537.36 Edge/18.17763";
                // Or Firefox 64?
                // Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:64.0) Gecko/20100101 Firefox/64.0
            }
        }

        public enum InterfaceSize
        {
            Adaptive = 0,
            Standard = 1,
            Small = 2,
        }

        public class InterfaceSetting
        {
            public const string SectionName = "Interface";

            public bool UseCustomTitle;
            public string CustomTitle;
            public bool UseCustomEditor;
            public string CustomEditorPath;
            public FontHelper.FontInfo MonospacedFont;
            public int ScaleFactor;
            public bool DisplayShellExecuteConOut;
            public InterfaceSize InterfaceSize;

            public FontFamily MonospacedFontFamily => MonospacedFont.FontFamily;
            public FontWeight MonospacedFontWeight => MonospacedFont.FontWeight;
            public int MonospacedFontSize => MonospacedFont.PointSize;

            public InterfaceSetting()
            {
                Default();
            }

            public void Default()
            {
                UseCustomTitle = false;
                CustomTitle = string.Empty;
                // Every Windows PC has notepad pre-installed.
                UseCustomEditor = false;
                CustomEditorPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "notepad.exe");
                // Every Windows PC has Consolas pre-installed.
                MonospacedFont = new FontHelper.FontInfo(new FontFamily("Consolas"), FontWeights.Regular, 12);
                ScaleFactor = 100;
                DisplayShellExecuteConOut = true;
                InterfaceSize = InterfaceSize.Adaptive;
            }
        }

        public enum ThemeType
        {
            Black = 0,
            Red = 1,
            Green = 2,
            Blue = 3,
            Custom = 255,
        }

        public class ThemeSetting
        {
            public const string SectionName = "Theme";

            // Preset
            public ThemeType ThemeType;
            // Custom
            public Color CustomTopPanelBackground;
            public Color CustomTopPanelForeground;
            public Color CustomTreePanelBackground;
            public Color CustomTreePanelForeground;
            public Color CustomTreePanelHighlight;
            public Color CustomScriptPanelBackground;
            public Color CustomScriptPanelForeground;
            public Color CustomStatusBarBackground;
            public Color CustomStatusBarForeground;

            public ThemeSetting()
            {
                Default();
            }

            public void Default()
            {
                ThemeType = ThemeType.Black;
                // Apply Classic Theme to Custom Properties
                CustomTopPanelBackground = Colors.LightBlue;
                CustomTopPanelForeground = Colors.Black;
                CustomTreePanelBackground = Colors.LightGreen;
                CustomTreePanelForeground = Colors.Black;
                CustomTreePanelHighlight = Colors.Red;
                CustomScriptPanelBackground = Colors.LightYellow;
                CustomScriptPanelForeground = Colors.Black;
                CustomStatusBarBackground = Colors.LightGray;
                CustomStatusBarForeground = Colors.Black;
            }

            #region Properties
            public Color TopPanelBackground
            {
                get
                {
                    switch (ThemeType)
                    {
                        case ThemeType.Black:
                            return Colors.Black;
                        case ThemeType.Red:
                            return Colors.DarkRed;
                        case ThemeType.Green:
                            return Colors.DarkGreen;
                        case ThemeType.Blue:
                            return Colors.DarkBlue;
                        case ThemeType.Custom:
                            return CustomTopPanelBackground;
                        default:
                            throw new InvalidOperationException("Undefined theme preset");
                    }
                }
            }
            public Color TopPanelForeground
            {
                get
                {
                    switch (ThemeType)
                    {
                        case ThemeType.Black:
                            return Color.FromRgb(238, 238, 238);
                        case ThemeType.Red:
                            return Colors.White;
                        case ThemeType.Green:
                            return Colors.White;
                        case ThemeType.Blue:
                            return Colors.White;
                        case ThemeType.Custom:
                            return CustomTopPanelForeground;
                        default:
                            throw new InvalidOperationException("Undefined theme preset");
                    }
                }
            }
            public Color TreePanelBackground
            {
                get
                {
                    switch (ThemeType)
                    {
                        case ThemeType.Black:
                            return Color.FromRgb(204, 204, 204);
                        case ThemeType.Red:
                            return Color.FromRgb(255, 211, 94);
                        case ThemeType.Green:
                            return Color.FromRgb(180, 255, 180);
                        case ThemeType.Blue:
                            return Color.FromRgb(180, 180, 255);
                        case ThemeType.Custom:
                            return CustomTreePanelBackground;
                        default:
                            throw new InvalidOperationException("Undefined theme preset");
                    }
                }
            }
            public Color TreePanelForeground
            {
                get
                {
                    switch (ThemeType)
                    {
                        case ThemeType.Black:
                        case ThemeType.Red:
                        case ThemeType.Green:
                        case ThemeType.Blue:
                            return Colors.Black;
                        case ThemeType.Custom:
                            return CustomTreePanelForeground;
                        default:
                            throw new InvalidOperationException("Undefined theme preset");
                    }
                }
            }
            public Color TreePanelHighlight
            {
                get
                {
                    switch (ThemeType)
                    {
                        case ThemeType.Black:
                            return Colors.Red;
                        case ThemeType.Red:
                            return Colors.DarkRed;
                        case ThemeType.Green:
                            return Colors.DarkGreen;
                        case ThemeType.Blue:
                            return Colors.DarkBlue;
                        case ThemeType.Custom:
                            return CustomTreePanelHighlight;
                        default:
                            throw new InvalidOperationException("Undefined theme preset");
                    }
                }
            }
            public Color ScriptPanelBackground
            {
                get
                {
                    switch (ThemeType)
                    {
                        case ThemeType.Black:
                            return Color.FromRgb(238, 238, 238);
                        case ThemeType.Red:
                            return Color.FromRgb(255, 255, 192);
                        case ThemeType.Green:
                            return Color.FromRgb(230, 255, 230);
                        case ThemeType.Blue:
                            return Color.FromRgb(230, 230, 255);
                        case ThemeType.Custom:
                            return CustomScriptPanelBackground;
                        default:
                            throw new InvalidOperationException("Undefined theme preset");
                    }
                }
            }
            public Color ScriptPanelForeground
            {
                get
                {
                    switch (ThemeType)
                    {
                        case ThemeType.Black:
                        case ThemeType.Red:
                        case ThemeType.Green:
                        case ThemeType.Blue:
                            return Colors.Black;
                        case ThemeType.Custom:
                            return CustomScriptPanelForeground;
                        default:
                            throw new InvalidOperationException("Undefined theme preset");
                    }
                }
            }
            public Color StatusBarBackground
            {
                get
                {
                    switch (ThemeType)
                    {
                        case ThemeType.Black:
                            return Color.FromRgb(238, 238, 238);
                        case ThemeType.Red:
                            return Color.FromRgb(255, 232, 208);
                        case ThemeType.Green:
                            return Color.FromRgb(210, 255, 210);
                        case ThemeType.Blue:
                            return Color.FromRgb(210, 210, 255);
                        case ThemeType.Custom:
                            return CustomStatusBarBackground;
                        default:
                            throw new InvalidOperationException("Undefined theme preset");
                    }
                }
            }
            public Color StatusBarForeground
            {
                get
                {
                    switch (ThemeType)
                    {
                        case ThemeType.Black:
                        case ThemeType.Red:
                        case ThemeType.Green:
                        case ThemeType.Blue:
                            return Colors.Black;
                        case ThemeType.Custom:
                            return CustomStatusBarForeground;
                        default:
                            throw new InvalidOperationException("Undefined theme preset");
                    }
                }
            }
            #endregion
        }

        public class ScriptSetting
        {
            public const string SectionName = "Script";

            public bool EnableCache;
            public bool AutoSyntaxCheck;

            public ScriptSetting()
            {
                Default();
            }

            public void Default()
            {
                // Script
                EnableCache = true;
                AutoSyntaxCheck = true;
            }
        }

        public class LogSetting
        {
            public const string SectionName = "Log";

            public LogDebugLevel DebugLevel;
            public bool DeferredLogging;
            public bool MinifyHtmlExport;

            public LogSetting()
            {
                Default();
            }

            public void Default()
            {
#if DEBUG
                DebugLevel = LogDebugLevel.PrintExceptionStackTrace;
#else
                DebugLevel = LogDebugLevel.Production;
#endif
                DeferredLogging = true;
                MinifyHtmlExport = true;
            }
        }

        // For LogWindow, this is not shown to SettingWindow. 
        public class LogViewerSetting
        {
            public const string SectionName = "LogViewer";

            public int LogWindowWidth;
            public int LogWindowHeight;
            public bool BuildFullLogTimeVisible;
            public int BuildFullLogTimeWidth;
            public bool BuildFullLogScriptOriginVisible;
            public int BuildFullLogScriptOriginWidth;
            public bool BuildFullLogDepthVisible;
            public int BuildFullLogDepthWidth;
            public bool BuildFullLogStateVisible;
            public int BuildFullLogStateWidth;
            public bool BuildFullLogFlagsVisible;
            public int BuildFullLogFlagsWidth;
            public bool BuildFullLogMessageVisible;
            public int BuildFullLogMessageWidth;
            public bool BuildFullLogRawCodeVisible;
            public int BuildFullLogRawCodeWidth;
            public bool BuildFullLogLineNumberVisible;
            public int BuildFullLogLineNumberWidth;

            public const int MinColumnWidth = 35;

            public LogViewerSetting()
            {
                Default();
            }

            public void Default()
            {
                LogWindowWidth = 900;
                LogWindowHeight = 640;
                BuildFullLogTimeVisible = true;
                BuildFullLogTimeWidth = 135;
                BuildFullLogScriptOriginVisible = false;
                BuildFullLogScriptOriginWidth = 135;
                BuildFullLogDepthVisible = true;
                BuildFullLogDepthWidth = 35;
                BuildFullLogStateVisible = true;
                BuildFullLogStateWidth = 55;
                BuildFullLogFlagsVisible = true;
                BuildFullLogFlagsWidth = 35;
                BuildFullLogMessageVisible = true;
                BuildFullLogMessageWidth = 340;
                BuildFullLogRawCodeVisible = true;
                BuildFullLogRawCodeWidth = 175;
                BuildFullLogLineNumberVisible = true;
                BuildFullLogLineNumberWidth = 40;
            }
        }
        #endregion

        #region Fields and Properties
        private readonly string _settingFile;

        public ProjectSetting Project { get; }
        public GeneralSetting General { get; }
        public InterfaceSetting Interface { get; }
        public ThemeSetting Theme { get; }
        public ScriptSetting Script { get; }
        public LogSetting Log { get; }
        public LogViewerSetting LogViewer { get; }
        #endregion

        #region Constructor
        public Setting(string settingFile)
        {
            _settingFile = settingFile;

            Project = new ProjectSetting();
            General = new GeneralSetting();
            Interface = new InterfaceSetting();
            Theme = new ThemeSetting();
            Script = new ScriptSetting();
            Log = new LogSetting();
            LogViewer = new LogViewerSetting();

            ReadFromFile();
        }
        #endregion

        #region ApplySetting
        public void ApplySetting()
        {
            // AppContext
            // Enabled  = Path Length Limit = 32767
            // Disabled = Path Length Limit = 260
            AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling", !General.EnableLongFilePath);

            // Static
            Engine.StopBuildOnError = General.StopBuildOnError;
            Logger.DebugLevel = Log.DebugLevel;
            Logger.MinifyHtmlExport = Log.MinifyHtmlExport;

            // MainViewModel (Without Theme)
            Global.MainViewModel.TitleBar = Interface.UseCustomTitle ? Interface.CustomTitle : MainViewModel.DefaultTitleBar;
            Global.MainViewModel.MonospacedFont = Interface.MonospacedFont;
            Global.MainViewModel.DisplayShellExecuteConOut = Interface.DisplayShellExecuteConOut;
            Global.MainViewModel.InterfaceSize = Interface.InterfaceSize;

            // MainViewModel (Theme)
            Global.MainViewModel.TopPanelBackground = Theme.TopPanelBackground;
            Global.MainViewModel.TopPanelForeground = Theme.TopPanelForeground;
            Global.MainViewModel.TreePanelBackground = Theme.TreePanelBackground;
            Global.MainViewModel.TreePanelForeground = Theme.TreePanelForeground;
            Global.MainViewModel.TreePanelHighlight = Theme.TreePanelHighlight;
            Global.MainViewModel.ScriptPanelBackground = Theme.ScriptPanelBackground;
            Global.MainViewModel.ScriptPanelForeground = Theme.ScriptPanelForeground;
            Global.MainViewModel.StatusBarBackground = Theme.StatusBarBackground;
            Global.MainViewModel.StatusBarForeground = Theme.StatusBarForeground;
        }
        #endregion

        #region SetToDefault
        public void SetToDefault()
        {
            Project.Default();
            General.Default();
            Interface.Default();
            Script.Default();
            Log.Default();
            LogViewer.Default();
        }
        #endregion

        #region ReadFromFile, WriteToFile
        public void ReadFromFile()
        {
            // Use default value if key/value does not exist or malformed.
            SetToDefault();

            if (!File.Exists(_settingFile))
                return;

            IniKey[] keys =
            {
                // Project
                new IniKey(ProjectSetting.SectionName, nameof(Project.DefaultProject)), // String
                // General
                new IniKey(GeneralSetting.SectionName, nameof(General.OptimizeCode)), // Boolean
                new IniKey(GeneralSetting.SectionName, nameof(General.ShowLogAfterBuild)), // Boolean
                new IniKey(GeneralSetting.SectionName, nameof(General.StopBuildOnError)), // Boolean
                new IniKey(GeneralSetting.SectionName, nameof(General.EnableLongFilePath)), // Boolean
                new IniKey(GeneralSetting.SectionName, nameof(General.UseCustomUserAgent)), // Boolean
                new IniKey(GeneralSetting.SectionName, nameof(General.CustomUserAgent)), // String
                // Interface
                new IniKey(InterfaceSetting.SectionName, nameof(Interface.UseCustomTitle)), // Boolean
                new IniKey(InterfaceSetting.SectionName, nameof(Interface.CustomTitle)), // String
                new IniKey(InterfaceSetting.SectionName, nameof(Interface.UseCustomEditor)), // Boolean
                new IniKey(InterfaceSetting.SectionName, nameof(Interface.CustomEditorPath)), // String
                new IniKey(InterfaceSetting.SectionName, nameof(Interface.MonospacedFontFamily)), // FontFamily
                new IniKey(InterfaceSetting.SectionName, nameof(Interface.MonospacedFontWeight)), // FontWeight
                new IniKey(InterfaceSetting.SectionName, nameof(Interface.MonospacedFontSize)), // FontSize
                new IniKey(InterfaceSetting.SectionName, nameof(Interface.ScaleFactor)), // Integer (70 - 200)
                new IniKey(InterfaceSetting.SectionName, nameof(Interface.DisplayShellExecuteConOut)), // Boolean
                new IniKey(InterfaceSetting.SectionName, nameof(Interface.InterfaceSize)), // Enum (InterfaceSize)
                // Theme
                new IniKey(ThemeSetting.SectionName, nameof(Theme.ThemeType)), // Enum (ThemeType)
                new IniKey(ThemeSetting.SectionName, nameof(Theme.CustomTopPanelBackground)), // Color
                new IniKey(ThemeSetting.SectionName, nameof(Theme.CustomTopPanelForeground)), // Color
                new IniKey(ThemeSetting.SectionName, nameof(Theme.CustomTreePanelBackground)), // Color
                new IniKey(ThemeSetting.SectionName, nameof(Theme.CustomTreePanelForeground)), // Color
                new IniKey(ThemeSetting.SectionName, nameof(Theme.CustomTreePanelHighlight)), // Color
                new IniKey(ThemeSetting.SectionName, nameof(Theme.CustomScriptPanelBackground)), // Color
                new IniKey(ThemeSetting.SectionName, nameof(Theme.CustomScriptPanelForeground)), // Color
                new IniKey(ThemeSetting.SectionName, nameof(Theme.CustomStatusBarBackground)), // Color
                new IniKey(ThemeSetting.SectionName, nameof(Theme.CustomStatusBarForeground)), // Color
                // Script
                new IniKey(ScriptSetting.SectionName, nameof(Script.EnableCache)), // Boolean
                new IniKey(ScriptSetting.SectionName, nameof(Script.AutoSyntaxCheck)), // Boolean
                // Log
                new IniKey(LogSetting.SectionName, nameof(Log.DebugLevel)), // Enum (LogDebugLevel)
                new IniKey(LogSetting.SectionName, nameof(Log.DeferredLogging)), // Boolean
                new IniKey(LogSetting.SectionName, nameof(Log.MinifyHtmlExport)), // Boolean
                // LogViewer
                new IniKey(LogViewerSetting.SectionName, nameof(LogViewer.LogWindowWidth)), // Integer (600 -)
                new IniKey(LogViewerSetting.SectionName, nameof(LogViewer.LogWindowHeight)), // Integer (480 -)
                new IniKey(LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogTimeVisible)), // Boolean
                new IniKey(LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogTimeWidth)), // Integer
                new IniKey(LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogScriptOriginVisible)), // Boolean
                new IniKey(LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogScriptOriginWidth)), // Integer
                new IniKey(LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogDepthVisible)), // Boolean
                new IniKey(LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogDepthWidth)), // Integer
                new IniKey(LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogStateVisible)), // Boolean
                new IniKey(LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogStateWidth)), // Integer
                new IniKey(LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogFlagsVisible)), // Boolean
                new IniKey(LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogFlagsWidth)), // Integer
                new IniKey(LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogMessageVisible)), // Boolean
                new IniKey(LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogMessageWidth)), // Integer
                new IniKey(LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogRawCodeVisible)), // Boolean
                new IniKey(LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogRawCodeWidth)), // Integer
                new IniKey(LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogLineNumberVisible)), // Boolean
                new IniKey(LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogLineNumberWidth)), // Integer
            };
            keys = IniReadWriter.ReadKeys(_settingFile, keys);
            Dictionary<string, Dictionary<string, string>> keyDict = keys
                .GroupBy(x => x.Section)
                .ToDictionary(
                    x => x.Key,
                    y => y.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase);

            // Project
            if (keyDict.ContainsKey(ProjectSetting.SectionName))
            {
                Dictionary<string, string> projectDict = keyDict[ProjectSetting.SectionName];

                Project.DefaultProject = DictParser.ParseString(projectDict, nameof(Project.DefaultProject), string.Empty);
            }

            // General
            if (keyDict.ContainsKey(GeneralSetting.SectionName))
            {
                Dictionary<string, string> generalDict = keyDict[GeneralSetting.SectionName];

                General.OptimizeCode = DictParser.ParseBoolean(generalDict, GeneralSetting.SectionName, nameof(General.OptimizeCode), General.OptimizeCode);
                General.ShowLogAfterBuild = DictParser.ParseBoolean(generalDict, GeneralSetting.SectionName, nameof(General.ShowLogAfterBuild), General.ShowLogAfterBuild);
                General.StopBuildOnError = DictParser.ParseBoolean(generalDict, GeneralSetting.SectionName, nameof(General.StopBuildOnError), General.StopBuildOnError);
                General.EnableLongFilePath = DictParser.ParseBoolean(generalDict, GeneralSetting.SectionName, nameof(General.EnableLongFilePath), General.EnableLongFilePath);
                General.UseCustomUserAgent = DictParser.ParseBoolean(generalDict, GeneralSetting.SectionName, nameof(General.UseCustomUserAgent), General.UseCustomUserAgent);
                General.CustomUserAgent = DictParser.ParseString(generalDict, nameof(General.CustomUserAgent), General.CustomUserAgent);
            }

            // Interface
            if (keyDict.ContainsKey(InterfaceSetting.SectionName))
            {
                Dictionary<string, string> ifaceDict = keyDict[InterfaceSetting.SectionName];

                // Parse MonospacedFont
                FontFamily monoFontFamily = Interface.MonospacedFont.FontFamily;
                FontWeight monoFontWeight = Interface.MonospacedFont.FontWeight;
                if (ifaceDict[nameof(Interface.MonospacedFontFamily)] != null)
                    monoFontFamily = new FontFamily(ifaceDict[nameof(Interface.MonospacedFontFamily)]);
                if (ifaceDict[nameof(Interface.MonospacedFontWeight)] != null)
                    monoFontWeight = FontHelper.ParseFontWeight(ifaceDict[nameof(Interface.MonospacedFontWeight)]);
                int monoFontSize = DictParser.ParseInteger(ifaceDict, InterfaceSetting.SectionName, nameof(Interface.MonospacedFontSize), Interface.MonospacedFont.PointSize, 1, -1);
                Interface.MonospacedFont = new FontHelper.FontInfo(monoFontFamily, monoFontWeight, monoFontSize);

                Interface.UseCustomTitle = DictParser.ParseBoolean(ifaceDict, InterfaceSetting.SectionName, nameof(Interface.UseCustomTitle), Interface.UseCustomTitle);
                Interface.CustomTitle = DictParser.ParseString(ifaceDict, nameof(Interface.CustomTitle), Interface.CustomTitle);
                Interface.UseCustomEditor = DictParser.ParseBoolean(ifaceDict, InterfaceSetting.SectionName, nameof(Interface.UseCustomEditor), Interface.UseCustomEditor);
                Interface.CustomEditorPath = DictParser.ParseString(ifaceDict, nameof(Interface.CustomEditorPath), Interface.CustomEditorPath);
                Interface.ScaleFactor = DictParser.ParseInteger(ifaceDict, InterfaceSetting.SectionName, nameof(Interface.ScaleFactor), Interface.ScaleFactor, 70, 200);
                Interface.DisplayShellExecuteConOut = DictParser.ParseBoolean(ifaceDict, InterfaceSetting.SectionName, nameof(Interface.DisplayShellExecuteConOut), Interface.DisplayShellExecuteConOut);
                Interface.InterfaceSize = DictParser.ParseIntEnum(ifaceDict, InterfaceSetting.SectionName, nameof(Interface.InterfaceSize), Interface.InterfaceSize);
            }

            // Theme
            if (keyDict.ContainsKey(ThemeSetting.SectionName))
            {
                Dictionary<string, string> scDict = keyDict[ThemeSetting.SectionName];

                Theme.ThemeType = DictParser.ParseIntEnum(scDict, ThemeSetting.SectionName, nameof(Theme.ThemeType), Theme.ThemeType);
                Theme.CustomTopPanelBackground = DictParser.ParseColor(scDict, ThemeSetting.SectionName, nameof(Theme.CustomTopPanelBackground), Theme.CustomTopPanelBackground);
                Theme.CustomTopPanelForeground = DictParser.ParseColor(scDict, ThemeSetting.SectionName, nameof(Theme.CustomTopPanelForeground), Theme.CustomTopPanelForeground);
                Theme.CustomTreePanelBackground = DictParser.ParseColor(scDict, ThemeSetting.SectionName, nameof(Theme.CustomTreePanelBackground), Theme.CustomTreePanelBackground);
                Theme.CustomTreePanelForeground = DictParser.ParseColor(scDict, ThemeSetting.SectionName, nameof(Theme.CustomTreePanelForeground), Theme.CustomTreePanelForeground);
                Theme.CustomTreePanelHighlight = DictParser.ParseColor(scDict, ThemeSetting.SectionName, nameof(Theme.CustomTreePanelHighlight), Theme.CustomTreePanelHighlight);
                Theme.CustomScriptPanelBackground = DictParser.ParseColor(scDict, ThemeSetting.SectionName, nameof(Theme.CustomScriptPanelBackground), Theme.CustomScriptPanelBackground);
                Theme.CustomScriptPanelForeground = DictParser.ParseColor(scDict, ThemeSetting.SectionName, nameof(Theme.CustomScriptPanelForeground), Theme.CustomScriptPanelForeground);
                Theme.CustomStatusBarBackground = DictParser.ParseColor(scDict, ThemeSetting.SectionName, nameof(Theme.CustomStatusBarBackground), Theme.CustomStatusBarBackground);
                Theme.CustomStatusBarForeground = DictParser.ParseColor(scDict, ThemeSetting.SectionName, nameof(Theme.CustomStatusBarForeground), Theme.CustomStatusBarForeground);
            }

            // Script
            if (keyDict.ContainsKey(ScriptSetting.SectionName))
            {
                Dictionary<string, string> scDict = keyDict[ScriptSetting.SectionName];

                Script.EnableCache = DictParser.ParseBoolean(scDict, ScriptSetting.SectionName, nameof(Script.EnableCache), Script.EnableCache);
                Script.AutoSyntaxCheck = DictParser.ParseBoolean(scDict, ScriptSetting.SectionName, nameof(Script.AutoSyntaxCheck), Script.AutoSyntaxCheck);
            }

            // Log
            if (keyDict.ContainsKey(LogSetting.SectionName))
            {
                Dictionary<string, string> logDict = keyDict[LogSetting.SectionName];

                Log.DebugLevel = DictParser.ParseIntEnum(logDict, LogSetting.SectionName, nameof(Log.DebugLevel), Log.DebugLevel);
                Log.DeferredLogging = DictParser.ParseBoolean(logDict, LogSetting.SectionName, nameof(Log.DeferredLogging), Log.DeferredLogging);
                Log.MinifyHtmlExport = DictParser.ParseBoolean(logDict, LogSetting.SectionName, nameof(Log.MinifyHtmlExport), Log.MinifyHtmlExport);
            }

            // LogViewer
            if (keyDict.ContainsKey(LogViewerSetting.SectionName))
            {
                Dictionary<string, string> logViewDict = keyDict[LogViewerSetting.SectionName];

                LogViewer.LogWindowWidth = DictParser.ParseInteger(logViewDict, LogViewerSetting.SectionName, nameof(LogViewer.LogWindowWidth), LogViewer.LogWindowWidth, 600, -1);
                LogViewer.LogWindowHeight = DictParser.ParseInteger(logViewDict, LogViewerSetting.SectionName, nameof(LogViewer.LogWindowHeight), LogViewer.LogWindowHeight, 480, -1);
                LogViewer.BuildFullLogTimeVisible = DictParser.ParseBoolean(logViewDict, LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogTimeVisible), LogViewer.BuildFullLogTimeVisible);
                LogViewer.BuildFullLogTimeWidth = DictParser.ParseInteger(logViewDict, LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogTimeWidth), LogViewer.BuildFullLogTimeWidth, LogViewerSetting.MinColumnWidth, -1);
                LogViewer.BuildFullLogScriptOriginVisible = DictParser.ParseBoolean(logViewDict, LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogScriptOriginVisible), LogViewer.BuildFullLogScriptOriginVisible);
                LogViewer.BuildFullLogScriptOriginWidth = DictParser.ParseInteger(logViewDict, LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogScriptOriginWidth), LogViewer.BuildFullLogScriptOriginWidth, LogViewerSetting.MinColumnWidth, -1);
                LogViewer.BuildFullLogDepthVisible = DictParser.ParseBoolean(logViewDict, LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogDepthVisible), LogViewer.BuildFullLogDepthVisible);
                LogViewer.BuildFullLogDepthWidth = DictParser.ParseInteger(logViewDict, LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogDepthWidth), LogViewer.BuildFullLogDepthWidth, LogViewerSetting.MinColumnWidth, -1);
                LogViewer.BuildFullLogStateVisible = DictParser.ParseBoolean(logViewDict, LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogStateVisible), LogViewer.BuildFullLogStateVisible);
                LogViewer.BuildFullLogStateWidth = DictParser.ParseInteger(logViewDict, LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogStateWidth), LogViewer.BuildFullLogStateWidth, LogViewerSetting.MinColumnWidth, -1);
                LogViewer.BuildFullLogFlagsVisible = DictParser.ParseBoolean(logViewDict, LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogFlagsVisible), LogViewer.BuildFullLogFlagsVisible);
                LogViewer.BuildFullLogFlagsWidth = DictParser.ParseInteger(logViewDict, LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogFlagsWidth), LogViewer.BuildFullLogFlagsWidth, LogViewerSetting.MinColumnWidth, -1);
                LogViewer.BuildFullLogMessageVisible = DictParser.ParseBoolean(logViewDict, LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogMessageVisible), LogViewer.BuildFullLogMessageVisible);
                LogViewer.BuildFullLogMessageWidth = DictParser.ParseInteger(logViewDict, LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogMessageWidth), LogViewer.BuildFullLogMessageWidth, LogViewerSetting.MinColumnWidth, -1);
                LogViewer.BuildFullLogRawCodeVisible = DictParser.ParseBoolean(logViewDict, LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogRawCodeVisible), LogViewer.BuildFullLogRawCodeVisible);
                LogViewer.BuildFullLogRawCodeWidth = DictParser.ParseInteger(logViewDict, LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogRawCodeWidth), LogViewer.BuildFullLogRawCodeWidth, LogViewerSetting.MinColumnWidth, -1);
                LogViewer.BuildFullLogLineNumberVisible = DictParser.ParseBoolean(logViewDict, LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogLineNumberVisible), LogViewer.BuildFullLogLineNumberVisible);
                LogViewer.BuildFullLogLineNumberWidth = DictParser.ParseInteger(logViewDict, LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogLineNumberWidth), LogViewer.BuildFullLogLineNumberWidth, LogViewerSetting.MinColumnWidth, -1);
            }
        }

        public void WriteToFile()
        {
            string WriteColor(Color c) => $"{c.R}, {c.G}, {c.B}";

            IniKey[] keys =
            {
                // Project
                new IniKey(ProjectSetting.SectionName, nameof(Project.DefaultProject), Project.DefaultProject), // String
                // General
                new IniKey(GeneralSetting.SectionName, nameof(General.EnableLongFilePath), General.EnableLongFilePath.ToString()), // Boolean
                new IniKey(GeneralSetting.SectionName, nameof(General.OptimizeCode), General.OptimizeCode.ToString()), // Boolean
                new IniKey(GeneralSetting.SectionName, nameof(General.ShowLogAfterBuild), General.ShowLogAfterBuild.ToString()), // Boolean
                new IniKey(GeneralSetting.SectionName, nameof(General.StopBuildOnError), General.StopBuildOnError.ToString()), // Boolean
                new IniKey(GeneralSetting.SectionName, nameof(General.UseCustomUserAgent), General.UseCustomUserAgent.ToString()), // Boolean
                new IniKey(GeneralSetting.SectionName, nameof(General.CustomUserAgent), General.CustomUserAgent), // String
                // Interface
                new IniKey(InterfaceSetting.SectionName, nameof(Interface.UseCustomTitle), Interface.UseCustomTitle.ToString()), // Boolean
                new IniKey(InterfaceSetting.SectionName, nameof(Interface.CustomTitle), Interface.CustomTitle), // String
                new IniKey(InterfaceSetting.SectionName, nameof(Interface.UseCustomEditor), Interface.UseCustomEditor.ToString()), // Boolean
                new IniKey(InterfaceSetting.SectionName, nameof(Interface.CustomEditorPath), Interface.CustomEditorPath), // String
                new IniKey(InterfaceSetting.SectionName, nameof(Interface.MonospacedFontFamily), Interface.MonospacedFont.FontFamily.Source),
                new IniKey(InterfaceSetting.SectionName, nameof(Interface.MonospacedFontWeight), Interface.MonospacedFont.FontWeight.ToString()),
                new IniKey(InterfaceSetting.SectionName, nameof(Interface.MonospacedFontSize), Interface.MonospacedFont.PointSize.ToString()),
                new IniKey(InterfaceSetting.SectionName, nameof(Interface.ScaleFactor), Interface.ScaleFactor.ToString(CultureInfo.InvariantCulture)), // Integer
                new IniKey(InterfaceSetting.SectionName, nameof(Interface.DisplayShellExecuteConOut), Interface.DisplayShellExecuteConOut.ToString()), // Boolean
                new IniKey(InterfaceSetting.SectionName, nameof(Interface.InterfaceSize), ((int)Interface.InterfaceSize).ToString()), // Integer
                // Theme
                new IniKey(ThemeSetting.SectionName, nameof(Theme.ThemeType), ((int)Theme.ThemeType).ToString()), // Integer
                new IniKey(ThemeSetting.SectionName, nameof(Theme.CustomTopPanelBackground), WriteColor(Theme.CustomTopPanelBackground)), // Color
                new IniKey(ThemeSetting.SectionName, nameof(Theme.CustomTopPanelForeground), WriteColor(Theme.CustomTopPanelForeground)), // Color
                new IniKey(ThemeSetting.SectionName, nameof(Theme.CustomTreePanelBackground), WriteColor(Theme.CustomTreePanelBackground)), // Color
                new IniKey(ThemeSetting.SectionName, nameof(Theme.CustomTreePanelForeground), WriteColor(Theme.CustomTreePanelForeground)), // Color
                new IniKey(ThemeSetting.SectionName, nameof(Theme.CustomTreePanelHighlight), WriteColor(Theme.CustomTreePanelHighlight)), // Color
                new IniKey(ThemeSetting.SectionName, nameof(Theme.CustomScriptPanelBackground), WriteColor(Theme.CustomScriptPanelBackground)), // Color
                new IniKey(ThemeSetting.SectionName, nameof(Theme.CustomScriptPanelForeground), WriteColor(Theme.CustomScriptPanelForeground)), // Color
                new IniKey(ThemeSetting.SectionName, nameof(Theme.CustomStatusBarBackground), WriteColor(Theme.CustomStatusBarBackground)), // Color
                new IniKey(ThemeSetting.SectionName, nameof(Theme.CustomStatusBarForeground), WriteColor(Theme.CustomStatusBarForeground)), // Color
                // Script
                new IniKey(ScriptSetting.SectionName, nameof(Script.EnableCache), Script.EnableCache.ToString()), // Boolean
                new IniKey(ScriptSetting.SectionName, nameof(Script.AutoSyntaxCheck), Script.AutoSyntaxCheck.ToString()), // Boolean
                // Log
                new IniKey(LogSetting.SectionName, nameof(Log.DebugLevel), ((int)Log.DebugLevel).ToString()), // Integer
                new IniKey(LogSetting.SectionName, nameof(Log.DeferredLogging), Log.DeferredLogging.ToString()), // Boolean
                new IniKey(LogSetting.SectionName, nameof(Log.MinifyHtmlExport), Log.MinifyHtmlExport.ToString()), // Boolean
                // LogViewer
                new IniKey(LogViewerSetting.SectionName, nameof(LogViewer.LogWindowWidth), LogViewer.LogWindowWidth.ToString()), // Integer
                new IniKey(LogViewerSetting.SectionName, nameof(LogViewer.LogWindowHeight), LogViewer.LogWindowHeight.ToString()), // Integer
                new IniKey(LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogTimeVisible), LogViewer.BuildFullLogTimeVisible.ToString()), // Boolean
                new IniKey(LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogTimeWidth), LogViewer.BuildFullLogTimeWidth.ToString()), // Integer
                new IniKey(LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogScriptOriginVisible), LogViewer.BuildFullLogScriptOriginVisible.ToString()), // Boolean
                new IniKey(LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogScriptOriginWidth), LogViewer.BuildFullLogScriptOriginWidth.ToString()), // Integer
                new IniKey(LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogDepthVisible), LogViewer.BuildFullLogDepthVisible.ToString()), // Boolean
                new IniKey(LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogDepthWidth), LogViewer.BuildFullLogDepthWidth.ToString()), // Integer
                new IniKey(LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogStateVisible), LogViewer.BuildFullLogStateVisible.ToString()), // Boolean
                new IniKey(LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogStateWidth), LogViewer.BuildFullLogStateWidth.ToString()), // Integer
                new IniKey(LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogFlagsVisible), LogViewer.BuildFullLogFlagsVisible.ToString()), // Boolean
                new IniKey(LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogFlagsWidth), LogViewer.BuildFullLogFlagsWidth.ToString()), // Integer
                new IniKey(LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogMessageVisible), LogViewer.BuildFullLogMessageVisible.ToString()), // Boolean
                new IniKey(LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogMessageWidth), LogViewer.BuildFullLogMessageWidth.ToString()), // Integer
                new IniKey(LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogRawCodeVisible), LogViewer.BuildFullLogRawCodeVisible.ToString()), // Boolean
                new IniKey(LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogRawCodeWidth), LogViewer.BuildFullLogRawCodeWidth.ToString()), // Integer
                new IniKey(LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogLineNumberVisible), LogViewer.BuildFullLogLineNumberVisible.ToString()), // Boolean
                new IniKey(LogViewerSetting.SectionName, nameof(LogViewer.BuildFullLogLineNumberWidth), LogViewer.BuildFullLogLineNumberWidth.ToString()), // Integer
            };
            IniReadWriter.WriteKeys(_settingFile, keys);
        }
        #endregion
    }
    #endregion

    #region DictParser
    public static class DictParser
    {
        public static string ParseString(Dictionary<string, string> dict, string key, string defaultValue)
        {
            return dict[key] ?? defaultValue;
        }

        public static bool ParseBoolean(Dictionary<string, string> dict, string key, bool defaultValue)
        {
            string valStr = dict[key];
            if (valStr == null) // No warning, just use default value
                return defaultValue;

            if (valStr.Equals("True", StringComparison.OrdinalIgnoreCase))
                return true;
            if (valStr.Equals("False", StringComparison.OrdinalIgnoreCase))
                return false;

            return defaultValue;
        }

        public static bool ParseBoolean(Dictionary<string, string> dict, string section, string key, bool defaultValue)
        {
            string valStr = dict[key];
            if (valStr == null) // No warning, just use default value
                return defaultValue;

            if (valStr.Equals("True", StringComparison.OrdinalIgnoreCase))
                return true;
            if (valStr.Equals("False", StringComparison.OrdinalIgnoreCase))
                return false;

            Global.Logger.SystemWrite(new LogInfo(LogState.Error, $"Setting [{section}.{key}] has wrong value: {valStr}"));
            return defaultValue;
        }

        public static int ParseInteger(Dictionary<string, string> dict, string key, int defaultValue, int min, int max)
        {
            string valStr = dict[key];
            if (valStr == null) // No warning, just use default value
                return defaultValue;

            if (int.TryParse(valStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int valInt))
            {
                if (min == -1)
                { // No Min
                    if (max == -1) // No Max
                        return valInt;
                    if (valInt <= max) // Have Min
                        return valInt;
                }
                else
                { // Have Min
                    if (max == -1 && min <= valInt) // No Max
                        return valInt;
                    if (min <= valInt && valInt <= max) // Have Min
                        return valInt;
                }
            }

            return defaultValue;
        }

        public static int ParseInteger(Dictionary<string, string> dict, string section, string key, int defaultValue, int min, int max)
        {
            string valStr = dict[key];
            if (valStr == null) // No warning, just use default value
                return defaultValue;

            if (int.TryParse(valStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int valInt))
            {
                if (min == -1)
                { // No Min
                    if (max == -1) // No Max
                        return valInt;
                    if (valInt <= max) // Have Min
                        return valInt;
                }
                else
                { // Have Min
                    if (max == -1 && min <= valInt) // No Max
                        return valInt;
                    if (min <= valInt && valInt <= max) // Have Min
                        return valInt;
                }
            }

            Global.Logger.SystemWrite(new LogInfo(LogState.Error, $"Setting [{section}.{key}] has wrong value: {valStr}"));
            return defaultValue;
        }

        public static TEnum ParseIntEnum<TEnum>(Dictionary<string, string> dict, string section, string key, TEnum defaultValue)
            where TEnum : Enum
        {
            string valStr = dict[key];
            if (valStr == null) // No warning, just use default value
                return defaultValue;

            if (int.TryParse(valStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int valInt))
            {
                if (Enum.IsDefined(typeof(TEnum), valInt))
                    return (TEnum)Enum.ToObject(typeof(TEnum), valInt);
            }

            Global.Logger.SystemWrite(new LogInfo(LogState.Error, $"Setting [{section}.{key}] has wrong value: {valStr}"));
            return defaultValue;
        }

        public static Color ParseColor(Dictionary<string, string> dict, string section, string key, Color defaultValue)
        {
            string valStr = dict[key];
            if (valStr == null) // No warning, just use default value
                return defaultValue;

            // Format = R, G, B (in base 10)
            string[] colorStrs = valStr.Split(',').Select(x => x.Trim()).ToArray();
            if (colorStrs.Length != 3)
            {
                Global.Logger.SystemWrite(new LogInfo(LogState.Error, $"Setting [{section}.{key}] has wrong value: {valStr}"));
                return defaultValue;
            }

            byte[] c = new byte[3]; // R, G, B
            for (int i = 0; i < 3; i++)
            {
                string colorStr = colorStrs[i];
                if (!byte.TryParse(colorStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte valByte))
                {
                    char ch;
                    switch (i)
                    {
                        case 0: // Red
                            ch = 'R';
                            break;
                        case 1: // Green
                            ch = 'G';
                            break;
                        case 2: // Blue
                            ch = 'B';
                            break;
                        default: // Unknown
                            ch = 'U';
                            break;
                    }
                    Debug.Assert(ch != 'U', "Unknown color parsing index");
                    Global.Logger.SystemWrite(new LogInfo(LogState.Error, $"Setting [{section}.{key}] has wrong [{ch}] value: {colorStr}"));
                    return defaultValue;
                }
                c[i] = valByte;
            }

            return Color.FromRgb(c[0], c[1], c[2]);
        }
    }
    #endregion
}
