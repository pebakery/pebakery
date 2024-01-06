﻿/*
    Copyright (C) 2018-2023 Hajin Jang
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

using MahApps.Metro.IconPacks;
using PEBakery.Core;
using PEBakery.Helper;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace PEBakery.WPF
{
    #region MainWindow
    public class BuildConOutForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return Brushes.Black;
            return (bool)value ? Brushes.Red : Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public class TaskBarProgressConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            return (double)values[1] / (double)values[0];
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return new object[] { Binding.DoNothing, Binding.DoNothing };
        }
    }

    public class ScriptLogoVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public class SyntaxCheckerResultIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || value.GetType() != typeof(SyntaxChecker.Result))
                return Binding.DoNothing;

            PackIconMaterialKind icon;
            SyntaxChecker.Result result = (SyntaxChecker.Result)value;
            switch (result)
            {
                case SyntaxChecker.Result.Clean:
                    icon = PackIconMaterialKind.Check;
                    break;
                case SyntaxChecker.Result.Warning:
                    icon = PackIconMaterialKind.Alert;
                    break;
                case SyntaxChecker.Result.Error:
                    icon = PackIconMaterialKind.Close;
                    break;
                default:
                    icon = PackIconMaterialKind.Magnify;
                    break;
            }
            return icon;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public class SyntaxCheckerResultColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || value.GetType() != typeof(SyntaxChecker.Result))
                return Binding.DoNothing;

            Brush brush;
            SyntaxChecker.Result result = (SyntaxChecker.Result)value;
            switch (result)
            {
                case SyntaxChecker.Result.Clean:
                    brush = Brushes.Green;
                    break;
                case SyntaxChecker.Result.Warning:
                    brush = Brushes.OrangeRed;
                    break;
                case SyntaxChecker.Result.Error:
                    brush = Brushes.Red;
                    break;
                default:
                    brush = Brushes.Gray;
                    break;
            }
            return brush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public class ScriptTitleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return Binding.DoNothing;
            if (value is not Script sc)
                return Binding.DoNothing;

            return sc.Title;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public class SelectedStateToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return Binding.DoNothing;
            if (value is not Script sc)
                return Binding.DoNothing;

            return sc.Selected == SelectedState.None ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public class IsTreeEntryDirMainToVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length != 2)
                return Visibility.Collapsed;
            if (!(values[0] is bool isTreeEntryFile && values[1] is bool isTreeEntryMain))
                return Visibility.Collapsed;

            return !isTreeEntryFile || isTreeEntryMain ? Visibility.Visible : Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return new object[2] { Binding.DoNothing, Binding.DoNothing };
        }
    }
    public class GridLengthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double val = (double)value;
            GridLength gridLength = new GridLength(val);

            return gridLength;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            GridLength val = (GridLength)value;

            return val.Value;
        }
    }
    #endregion

    #region SettingWindow
    public class ProjectPathEnabledConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return false;
            if (value is not Project p)
                return false;

            return p.IsPathSettingEnabled();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public class ProjectPathEnabledVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return Visibility.Collapsed;
            if (value is not Project p)
                return Visibility.Collapsed;

            return p.IsPathSettingEnabled() ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public class CompatOptionToggleButtonContentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not bool nextState)
                return false;
            return nextState ? "Select All" : "Select None";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public class ThemeTypeIsCustomConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not Setting.ThemeType typeVal)
                return false;
            Debug.Assert(Enum.IsDefined(typeVal), "Check SettingWindow.xaml's theme tab.");
            return typeVal == Setting.ThemeType.Custom;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not bool boolVal)
                return false;
            return !boolVal;
        }
    }

    public class ThemeTypeToSolidColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not Setting.ThemeType typeVal)
                return false;
            return new SolidColorBrush(Setting.ThemeSetting.QueryThemeMainColor(typeVal));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
    #endregion

    #region ScriptEditWindow
    public class AttachProgressValueToPercentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not double progress)
                return string.Empty;
            return 0 < progress ? $"{progress * 100:0.0}%" : string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return 0;
        }
    }

    public class AttachProgressValueToEnabledConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not double progress)
                return false;
            return 0 <= progress;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return true;
        }
    }

    public class ActiveInterfaceSectionVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not ObservableCollection<string> collection)
                return false;
            return 2 <= collection.Count ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    /// <summary>
    /// Convert an escaped string (Command, UIControl) into a raw string (interface).
    /// </summary>
    public class StringEscapeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string escapedStr)
                return string.Empty;

            return StringEscaper.Unescape(escapedStr);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string rawStr)
                return Binding.DoNothing;

            return StringEscaper.Escape(rawStr, false, true);
        }
    }
    #endregion

    #region LogWindow
    public class LocalTimeToStrConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return string.Empty;

            DateTime time = (DateTime)value;
            return time == DateTime.MinValue ? string.Empty : time.ToLocalTime().ToString("yyyy-MM-dd hh:mm:ss tt", CultureInfo.InvariantCulture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string str)
                return DateTime.Now;
            return DateTime.TryParse(str, out DateTime time) ? time : DateTime.Now;
        }
    }

    public class LogStateToStrConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return string.Empty;

            LogState state = (LogState)value;
            return state == LogState.None ? string.Empty : state.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public class LineIdxToStrConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return string.Empty;

            int lineIdx = (int)value;
            return lineIdx == 0 ? string.Empty : lineIdx.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public class GridViewColumnWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length != 2)
                return Setting.LogViewerSetting.MinColumnWidth;
            if (!(values[0] is bool visible && values[1] is double width))
                return Setting.LogViewerSetting.MinColumnWidth;

            // Binding will not work if returned value is not double (for column width).
            return visible ? width : 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            // Strange enough, using ConvertBack just locks column width.
            // So just return Binding.DoNothing.
            return new object[2] { Binding.DoNothing, Binding.DoNothing };
        }
    }

    public class RefScriptIdToTitleConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length != 3)
                return string.Empty;

            if (values[0] is not int scriptId)
                return string.Empty;
            if (values[1] is not int refScriptId)
                return string.Empty;
            if (values[2] is not Dictionary<int, string> scTitleDict)
                return string.Empty;

            if (refScriptId != 0) // Referenced Script
            {
                if (scTitleDict.ContainsKey(refScriptId))
                    return scTitleDict[refScriptId];
                else
                    return string.Empty;
            }
            else if (scriptId != 0)
            {
                if (scTitleDict.ContainsKey(scriptId))
                    return scTitleDict[scriptId];
                else
                    return string.Empty;
            }
            return string.Empty;
        }

        public object[] ConvertBack(object value, Type[] targetType, object parameter, CultureInfo culture)
        {
            return new object[] { Binding.DoNothing, Binding.DoNothing };
        }
    }

    public class BuildLogFlagToStrConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return string.Empty;

            LogModel.BuildLogFlag flags = (LogModel.BuildLogFlag)value;
            return LogModel.BuildLogFlagToString(flags);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return LogModel.BuildLogFlag.None;
            if (value is not string str)
                return LogModel.BuildLogFlag.None;

            return LogModel.ParseBuildLogFlag(str);
        }
    }

    public class LogStateToForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not LogState state)
                return Binding.DoNothing;

            switch (state)
            {
                case LogState.Ignore:
                case LogState.Muted:
                    return new SolidColorBrush(Color.FromRgb(108, 117, 125));
                default:
                    return new SolidColorBrush(Color.FromRgb(0, 0, 0));
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public class LogStateToStatBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not LogState state)
                return Binding.DoNothing;

            if (LogInfo.QueryStatBackgroundColor(state) is Color color)
                return new SolidColorBrush(color);
            return Binding.DoNothing;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public class LogStateToAlternateBackgroundConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 2)
                return Binding.DoNothing;
            if (values[0] is not LogState state)
                return Binding.DoNothing;
            if (values[1] is not int alterIdx)
                return Binding.DoNothing;

            if (LogInfo.QueryRowBackgroundColor(state) is Color color)
                return new SolidColorBrush(color);

            if (alterIdx % 2 == 0)
                return new SolidColorBrush(Color.FromRgb(255, 255, 255));
            else
                return new SolidColorBrush(Color.FromRgb(242, 242, 242));
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return new object[2] { Binding.DoNothing, Binding.DoNothing };
        }
    }

    public class VarsTypeToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not VarsType varsType)
                return Binding.DoNothing;

            switch (varsType)
            {
                case VarsType.Fixed:
                    return new SolidColorBrush(Color.FromRgb(204, 229, 255));
                case VarsType.Global:
                    return new SolidColorBrush(Color.FromRgb(212, 237, 218));
                case VarsType.Local:
                    return new SolidColorBrush(Color.FromRgb(250, 226, 202));
            }
            return Binding.DoNothing;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
    #endregion

    #region Font
    public class FontInfoDescriptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return string.Empty;
            if (value is not FontHelper.FontInfo fi)
                return string.Empty;

            return fi.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public class FontInfoFamilyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return Binding.DoNothing;
            if (value is not FontHelper.FontInfo fi)
                return Binding.DoNothing;

            return fi.FontFamily;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public class FontInfoWeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return Binding.DoNothing;
            if (value is not FontHelper.FontInfo fi)
                return Binding.DoNothing;

            return fi.FontWeight;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public class FontInfoSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return Binding.DoNothing;
            if (value is not FontHelper.FontInfo fi)
                return Binding.DoNothing;

            return fi.DeviceIndependentPixelSize;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public class BoolToFontWeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return FontWeights.Regular;
            if (value is not bool b)
                return FontWeights.Regular;

            return b ? FontWeights.Bold : FontWeights.Regular;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
    #endregion

    #region Boolean
    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return false;
            return !(bool)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return false;
            return !(bool)value;
        }
    }

    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return Visibility.Collapsed;
            if (value is not bool valBool)
                return Visibility.Collapsed;

            return valBool ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public class BooleanToParamConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not bool b)
                return Binding.DoNothing;
            if (!(parameter is Array paramArr && paramArr.Length == 2))
                return Binding.DoNothing;

            object? retObj = b ? paramArr.GetValue(0) : paramArr.GetValue(1);
            return retObj ?? Binding.DoNothing;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public class MultiBooleanAndToVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values is not Array valueArr)
                return Binding.DoNothing;

            bool boolAndVal = true;
            foreach (object val in valueArr)
            {
                if (val is not bool boolVal)
                    return Binding.DoNothing;
                boolAndVal &= boolVal;
            }

            return boolAndVal ? Visibility.Visible : Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return new object[] { Binding.DoNothing };
        }
    }

    public class MultiBooleanOrToVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values is not Array valueArr)
                return Binding.DoNothing;

            bool boolOrVal = false;
            foreach (object val in valueArr)
            {
                if (val is not bool boolVal)
                    return Binding.DoNothing;
                boolOrVal |= boolVal;
            }

            return boolOrVal ? Visibility.Visible : Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return new object[] { Binding.DoNothing };
        }
    }
    #endregion

    #region SolidColorBrush
    public class ColorToSolidColorBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return Binding.DoNothing;
            if (value is not Color c)
                return Binding.DoNothing;

            return new SolidColorBrush(c);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return Binding.DoNothing;
            if (value is not SolidColorBrush b)
                return Binding.DoNothing;

            return b.Color;
        }
    }

    public class MultiColorToSolidColorBrushConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length != 3)
                return Binding.DoNothing;
            if (!(values[0] is bool hasIssue &&
                values[1] is Color normal &&
                values[2] is Color issue))
                return Binding.DoNothing;

            SolidColorBrush brush = new SolidColorBrush(hasIssue ? issue : normal);
            brush.Freeze();
            return brush;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return new object[] { Binding.DoNothing, Binding.DoNothing, Binding.DoNothing, Binding.DoNothing, Binding.DoNothing };
        }
    }
    #endregion

    #region Enum
    public class CompareParamConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.Equals(parameter) ?? false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not bool val)
                return Binding.DoNothing;

            if (val)
                return parameter;
            else
                return Binding.DoNothing;
        }
    }
    #endregion
}
