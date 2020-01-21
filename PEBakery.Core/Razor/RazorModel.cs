/*
    Copyright (C) 2016-2020 Hajin Jang
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

namespace PEBakery.Core.Razor
{
    #region ViewModelBase
    public class ViewModelBase
    {
        // Information
        public string HeadTitle { get; set; }
        public string ExportEngineVersion { get; set; }
        public string ExportTimeStr { get; set; }
        // Embed
        public string EmbedBootstrapCss { get; set; }
        public string EmbedJQuerySlim { get; set; }
        public string EmbedBootstrapJs { get; set; }
    }
    #endregion

    #region SystemLogModel
    public class SystemLogModel : ViewModelBase
    {

        // Data
        public List<SystemLogItem> SysLogs { get; set; }
    }

    public class SystemLogItem
    {
        public string TimeStr { get; set; }
        public LogState State { get; set; }
        public string Message { get; set; }
    }
    #endregion

    #region BuildLogModel
    public class BuildLogModel : ViewModelBase
    {
        // Information
        public string BuiltEngineVersion { get; set; }
        public string BuildStartTimeStr { get; set; }
        public string BuildEndTimeStr { get; set; }
        public string BuildTookTimeStr { get; set; }
        public bool ShowLogFlags { get; set; }
        // Data
        public List<LogStatItem> LogStats { get; set; }
        public List<ScriptLogItem> Scripts { get; set; }
        public List<ScriptLogItem> RefScripts { get; set; }
        public List<VariableLogItem> Variables { get; set; }
        public Dictionary<ScriptLogItem, Tuple<CodeLogItem, string>[]> ErrorCodeDict { get; set; }
        public Dictionary<ScriptLogItem, Tuple<CodeLogItem, string>[]> WarnCodeDict { get; set; }
        public List<Tuple<ScriptLogItem, CodeLogItem[], VariableLogItem[]>> CodeLogs { get; set; }
    }

    public class LogStatItem
    {
        public LogState State { get; set; }
        public int Count { get; set; }
    }

    public class ScriptLogItem
    {
        public string IndexStr { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public string Version { get; set; }
        public string TimeStr { get; set; }
    }

    public class VariableLogItem
    {
        public VarsType Type { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }
    }

    public class CodeLogItem
    {
        public LogState State { get; set; }
        public string Message { get; set; }
        /// <summary>
        /// From LogModel.BuildLogFlag
        /// </summary>
        public LogModel.BuildLogFlag Flags { get; set; }
        /// <summary>
        /// Set to null if a log was not generated from a referenced script
        /// </summary>
        public string RefScriptTitle { get; set; }
        /// <summary>
        /// Optional, for error/warning logs
        /// </summary>
        public int Href { get; set; }

        // Used in BuildLogHtmlTemplate.cshtml
        public string FlagsStr
        {
            get
            {
                if ((Flags & LogModel.BuildLogFlag.Macro) == LogModel.BuildLogFlag.Macro)
                    return "Macro";
                else if ((Flags & LogModel.BuildLogFlag.RefScript) == LogModel.BuildLogFlag.RefScript)
                    return "Ref";
                else
                    return string.Empty;
            }
        }
    }
    #endregion
}
