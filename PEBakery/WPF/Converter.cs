/*
    Copyright (C) 2018-2019 Hajin Jang
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

    public class CodeValidatorResultIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || value.GetType() != typeof(SyntaxChecker.Result))
                return null;

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

    public class CodeValidatorResultColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || value.GetType() != typeof(SyntaxChecker.Result))
                return null;

            Brush brush;
            SyntaxChecker.Result result = (SyntaxChecker.Result)value;
            switch (result)
            {
                case SyntaxChecker.Result.Clean:
                    brush = new SolidColorBrush(Colors.Green);
                    break;
                case SyntaxChecker.Result.Warning:
                    brush = new SolidColorBrush(Colors.OrangeRed);
                    break;
                case SyntaxChecker.Result.Error:
                    brush = new SolidColorBrush(Colors.Red);
                    break;
                default:
                    brush = new SolidColorBrush(Colors.Gray);
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
                return null;
            if (!(value is Script sc))
                return null;

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
                return null;
            if (!(value is Script sc))
                return null;

            return sc.Selected == SelectedState.None ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
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
            if (!(value is Project p))
                return false;

            return p.IsPathSettingEnabled();
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
            if (value == null)
                return false;
            bool nextState = (bool)value;
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
            if (value == null)
                return false;
            Setting.ThemeType type = (Setting.ThemeType)value;
            Debug.Assert(Enum.IsDefined(typeof(Setting.ThemeType), type), "Check SettingWindow.xaml's theme tab.");
            return type == Setting.ThemeType.Custom;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return false;
            return !(bool)value;
        }
    }
    #endregion

    #region ScriptEditWindow
    public class AttachProgressValueToPercentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is double progress))
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
            if (!(value is double progress))
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
            if (!(value is ObservableCollection<string> collection))
                return false;
            return 2 <= collection.Count ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
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
            if (!(value is string str))
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

            // Binding won't work if returned value is not double (for column width)
            return visible ? width : 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            // Strange enough, using ConvertBack just locks column width.
            // So just return Binding.DoNothing...
            return new object[2] { Binding.DoNothing, Binding.DoNothing };
        }
    }

    public class RefScriptIdToTitleConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length != 2)
                return string.Empty;

            if (!(values[0] is int refScriptId))
                return string.Empty;
            if (!(values[1] is Dictionary<int, string> scTitleDict))
                return string.Empty;

            return scTitleDict.ContainsKey(refScriptId) ? scTitleDict[refScriptId] : string.Empty;
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
            if (!(value is string str))
                return LogModel.BuildLogFlag.None;

            return LogModel.ParseBuildLogFlag(str);
        }
    }
    #endregion

    #region FontHelper
    public class FontInfoDescriptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return string.Empty;
            if (!(value is FontHelper.FontInfo fi))
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
                return null;
            if (!(value is FontHelper.FontInfo fi))
                return null;

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
                return null;
            if (!(value is FontHelper.FontInfo fi))
                return null;

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
                return null;
            if (!(value is FontHelper.FontInfo fi))
                return null;

            return fi.DeviceIndependentPixelSize;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
    #endregion

    #region InverseBool
    public class InverseBoolConverter : IValueConverter
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
    #endregion

    #region SolidColorBrush
    public class ColorToSolidColorBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return null;
            if (!(value is Color c))
                return null;

            return new SolidColorBrush(c);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return null;
            if (!(value is SolidColorBrush b))
                return null;

            return b.Color;
        }
    }
    #endregion
}
