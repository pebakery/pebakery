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

using MahApps.Metro.IconPacks;
using PEBakery.Core;
using PEBakery.Helper;
using System;
using System.Globalization;
using System.Linq;
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }
    }

    public class CodeValidatorResultIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || value.GetType() != typeof(CodeValidator.Result))
                return null;

            PackIconMaterialKind icon;
            CodeValidator.Result result = (CodeValidator.Result)value;
            switch (result)
            {
                case CodeValidator.Result.Clean:
                    icon = PackIconMaterialKind.Check;
                    break;
                case CodeValidator.Result.Warning:
                    icon = PackIconMaterialKind.Alert;
                    break;
                case CodeValidator.Result.Error:
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
            throw new NotImplementedException();
        }
    }

    public class CodeValidatorResultColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || value.GetType() != typeof(CodeValidator.Result))
                return null;

            Brush brush;
            CodeValidator.Result result = (CodeValidator.Result)value;
            switch (result)
            {
                case CodeValidator.Result.Clean:
                    brush = new SolidColorBrush(Colors.Green);
                    break;
                case CodeValidator.Result.Warning:
                    brush = new SolidColorBrush(Colors.OrangeRed);
                    break;
                case CodeValidator.Result.Error:
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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
            // Not Implemented
            return LogState.None;
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
            // Not Implemented
            return LogState.None;
        }
    }

    public class BuildLogFlagToStrConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return string.Empty;

            LogModel.BuildLogFlag flags = (LogModel.BuildLogFlag)value;
            string result = string.Empty;
            if ((flags & LogModel.BuildLogFlag.Comment) == LogModel.BuildLogFlag.Comment)
                result += 'C';
            if ((flags & LogModel.BuildLogFlag.Macro) == LogModel.BuildLogFlag.Macro)
                result += 'M';
            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return string.Empty;
            if (!(value is string str))
                return null;

            LogModel.BuildLogFlag flags = LogModel.BuildLogFlag.None;
            if (str.Contains('C'))
                flags |= LogModel.BuildLogFlag.Comment;
            if (str.Contains('M'))
                flags |= LogModel.BuildLogFlag.Macro;
            return flags;
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }
    }
    #endregion
}
