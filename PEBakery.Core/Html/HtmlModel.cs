/*
    Copyright (C) 2016-2023 Hajin Jang
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

using Scriban.Runtime;
using System.Collections.Generic;

namespace PEBakery.Core.Html
{
    #region LogLayoutModel
    public class LogLayoutModel
    {
        // Information
        public string HeadTitle { get; set; } = string.Empty;
        public string ExportEngineVersion { get; set; } = string.Empty;
        public string ExportTimeStr { get; set; } = string.Empty;
        // Embed
        public string? EmbedBootstrapCss { get; set; }
        public string? EmbedJQuerySlimJs { get; set; }
        public string? EmbedBootstrapJs { get; set; }
    }
    #endregion

    #region SystemLogModel
    public class SystemLogModel : LogLayoutModel
    {
        // Data
        public ScriptArray SysLogs { get; private set; } = new ScriptArray();
    }

    public class SystemLogItem
    {
        public string TimeStr { get; set; } = string.Empty;
        public LogState State { get; set; }
        public string StateStr => State.ToString();
        public string Message { get; set; } = string.Empty;
    }
    #endregion

    #region BuildLogModel
    public class BuildLogModel : LogLayoutModel
    {
        // Information
        public string BuiltEngineVersion { get; set; } = string.Empty;
        public string BuildStartTimeStr { get; set; } = string.Empty;
        public string BuildEndTimeStr { get; set; } = string.Empty;
        public string BuildTookTimeStr { get; set; } = string.Empty;
        public bool ShowLogFlags { get; set; }
        // Host Environment
        public string BuildHostWindowsVersion { get; set; } = string.Empty;
        public string BuildHostDotnetVersion { get; set; } = string.Empty;
        // Data
        // type: LogStatItem[]
        public ScriptArray LogStats { get; private set; } = new ScriptArray();
        /* type: [
            { 
                ScriptName = string,
                ScriptPath = string,
                Codes = [{
                    State = string,
                    Message = string,
                    Href = string,
                    RefScriptMsg = string,
                }]
            }, ...
        ] */
        public ScriptArray ErrorSummaries { get; private set; } = new ScriptArray();
        public ScriptArray WarnSummaries { get; private set; } = new ScriptArray();
        // type: ScriptLogItem[]
        public ScriptArray Scripts { get; private set; } = new ScriptArray();
        public ScriptArray RefScripts { get; private set; } = new ScriptArray();
        // type: VariableLogItem[]
        public ScriptArray Variables { get; private set; } = new ScriptArray();
        /* type: [
            { 
                Script = ScriptLogItem,
                Codes = ScriptArray of CodeLogItem,
                Variables = ScriptArray of VariableLogItem
            }, ...
        ] */
        public ScriptArray CodeLogs { get; private set; } = new ScriptArray();
    }

    public class LogStatItem
    {
        public LogState State { get; set; }
        public int Count { get; set; }
    }

    public class ScriptLogItem
    {
        public string IndexStr { get; set; } = string.Empty; // int Index or "Macro"
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string TimeStr { get; set; } = string.Empty;
    }

    public class VariableLogItem
    {
        public VarsType Type { get; set; }
        public string TypeStr => Type.ToString();
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    public class CodeLogItem
    {
        public LogState State { get; set; }
        public string Message { get; set; } = string.Empty;
        /// <summary>
        /// From LogModel.BuildLogFlag
        /// </summary>
        public LogModel.BuildLogFlag Flags { get; set; }
        /// <summary>
        /// Set to null if a log was not generated from a referenced script
        /// </summary>
        public string RefScriptTitle { get; set; } = string.Empty;
        /// <summary>
        /// Optional, for error/warning logs
        /// </summary>
        public int Href { get; set; }
        public string? RefScriptMsg { get; set; }

        // Used in _BuildLogView.sbnhtml
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

    #region ScriptArrayExtension
    public static class ScriptArrayExtension
    {
        public static void AddItem<T>(this ScriptArray sa, T item)
        {
            ScriptObject itemObj = new ScriptObject();
            itemObj.Import(item, renamer: HtmlRenderer.ScribanObjectRenamer);
            sa.Add(itemObj);
        }

        public static void AddItem<T>(this ScriptArray sa, IEnumerable<T> items)
        {
            ScriptArray itemArr = new ScriptArray();
            foreach (T item in items)
            {
                AddItem(itemArr, item);
            }
            sa.Add(itemArr);
        }

        public static void AddItem<T>(this ScriptObject so, string key, T item)
        {
            ScriptObject itemObj = new ScriptObject();
            itemObj.Import(item, renamer: HtmlRenderer.ScribanObjectRenamer);
            so[key] = itemObj;
        }

        public static void AddItem<T>(this ScriptObject so, string key, IEnumerable<T> items)
        {
            ScriptArray itemArr = new ScriptArray();
            foreach (T item in items)
            {
                AddItem(itemArr, item);
            }
            so[key] = itemArr;
        }
    }
    #endregion
}
