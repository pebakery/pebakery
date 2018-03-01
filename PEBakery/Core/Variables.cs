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

    Additional permission under GNU GPL version 3 section 7

    If you modify this program, or any covered work, by linking
    or combining it with external libraries, containing parts
    covered by the terms of various license, the licensors of
    this program grant you additional permission to convey the
    resulting work. An external library is a library which is
    not derived from or based on this program. 
*/

using PEBakery.Exceptions;
using PEBakery.Helper;
using PEBakery.IniLib;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PEBakery.Core
{
    public enum VarsType
    {
        Fixed = 0,
        Global = 1,
        Local = 2,
    };

    public class Variables : ICloneable
    {
        /*
         * Variables search order
         * 1. local variables
         * 2. global variables
         * 3. fixed variables
         */

        #region Field and Property
        private Project project;
        private Dictionary<string, string> fixedVars;
        private Dictionary<string, string> globalVars;
        private Dictionary<string, string> localVars;

        public string this[string key]
        {
            get => GetValue(key);
            set => SetValue(VarsType.Local, key, value);
        }

        public string this[VarsType type, string key]
        {
            get => GetValue(type, key);
            set => SetValue(type, key, value);
        }
        #endregion

        #region Constructor
        public Variables(Project project)
        {
            this.project = project;
            this.localVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            this.globalVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            this.fixedVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            LoadDefaultFixedVariables();
            LoadDefaultGlobalVariables();
        }

        public Variables(Project project, out List<LogInfo> logs)
        {
            this.project = project;
            this.localVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            this.globalVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            this.fixedVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            logs = LoadDefaultFixedVariables();
            logs.AddRange(LoadDefaultGlobalVariables());
        }
        #endregion

        #region LoadDefaults
        private List<LogInfo> LoadDefaultFixedVariables()
        {
            List<LogInfo> logs = new List<LogInfo>(32);

            #region Builder Variables
            // PEBakery
            logs.Add(SetValue(VarsType.Fixed, "PEBakery", "True"));
            // BaseDir
            logs.Add(SetValue(VarsType.Fixed, "BaseDir", project.BaseDir.TrimEnd(new char[] { '\\' } )));
            // Version
            logs.Add(SetValue(VarsType.Fixed, "Version", "082")); // WB082 Compatibility Shim
            logs.Add(SetValue(VarsType.Fixed, "EngineVersion", App.Version.ToString("000")));
            logs.Add(SetValue(VarsType.Fixed, "PEBakeryVersion", typeof(App).Assembly.GetName().Version.ToString()));
            #endregion

            #region Project Variables
            // Read from MainScript
            string fullPath = project.MainScript.RealPath;
            IniKey[] keys = new IniKey[]
            {
                new IniKey("Main", "Title"),
                new IniKey("Main", "PathSetting"),
                new IniKey("Main", "SourceDir"),
                new IniKey("Main", "TargetDir"),
                new IniKey("Main", "ISOFile"),
            };
            keys = Ini.GetKeys(fullPath, keys);
            Dictionary<string, string> dict = keys.ToDictionary(x => x.Key, x => x.Value);

            string projectTitle = dict["Title"];
            if (projectTitle == null || projectTitle.Equals(string.Empty, StringComparison.Ordinal))
                projectTitle = project.ProjectName;

            // If PathSetting is set to False, ignore SourceDir, TargetDir and ISOFile
            bool pathEnabled = true;
            string pathEnabledStr = dict["PathSetting"];
            if (pathEnabledStr != null && pathEnabledStr.Equals("False", StringComparison.OrdinalIgnoreCase))
                pathEnabled = false;

            string targetDir = Path.Combine("%BaseDir%", "Target", project.ProjectName);
            string isoFile = Path.Combine("%BaseDir%", "ISO", project.ProjectName + ".iso");
            if (pathEnabled)
            {
                // Get SourceDir
                string sourceDir = string.Empty;
                string sourceDirs = dict["SourceDir"];
                if (sourceDirs != null) // Empty
                {
                    string[] rawDirList = sourceDirs.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string rawDir in rawDirList)
                    {
                        string dir = rawDir.Trim();
                        if (dir.Equals(string.Empty, StringComparison.Ordinal) == false)
                        {
                            sourceDir = dir;
                            break;
                        }
                    }
                }

                // Set SourceDir
                logs.Add(SetValue(VarsType.Fixed, "SourceDir", sourceDir));

                string targetDirStr = dict["TargetDir"];
                if (targetDirStr != null && 0 < targetDirStr.Length)
                    targetDir = targetDirStr;

                string isoFileStr = dict["ISOFile"];
                if (isoFileStr != null && 0 < isoFileStr.Length)
                    isoFile = isoFileStr;
            }          

            // ProjectTitle
            logs.Add(SetValue(VarsType.Fixed, "ProjectTitle", projectTitle));
            // ProjectDir
            logs.Add(SetValue(VarsType.Fixed, "ProjectDir", Path.Combine("%BaseDir%", "Projects", project.ProjectName)));
            // TargetDir
            logs.Add(SetValue(VarsType.Fixed, "TargetDir", targetDir));
            // ISOFile
            logs.Add(SetValue(VarsType.Fixed, "ISOFile", isoFile));
            // ISODir
            logs.Add(SetValue(VarsType.Fixed, "ISODir", FileHelper.GetDirNameEx(isoFile)));
            #endregion

            #region Envrionment Variables
            List<Tuple<string, string>> envVarNames = new List<Tuple<string, string>>
            { // Item1 - Windows Env Var Name, Item2 - PEBakery Env Var Name
                new Tuple<string, string>("TEMP", "TempDir"),
                new Tuple<string, string>("USERNAME", "UserName"),
                new Tuple<string, string>("USERPROFILE", "UserProfile"),
                new Tuple<string, string>("WINDIR", "WindowsDir"),
                new Tuple<string, string>("ProgramFiles", "ProgramFilesDir"),
            };

            if (Environment.Is64BitProcess)
                envVarNames.Add(new Tuple<string, string>("ProgramFiles(x86)", "ProgramFilesDir_x86"));

            foreach (var tuple in envVarNames)
            {
                string envValue = Environment.GetEnvironmentVariable(tuple.Item1);
                if (envValue == null)
                    logs.Add(new LogInfo(LogState.Error, $"Cannot get [%{tuple.Item1}%] from Windows"));
                else
                    logs.Add(SetValue(VarsType.Fixed, tuple.Item2, envValue));
            }

            // WindowsVersion
            OperatingSystem sysVer = Environment.OSVersion;
            logs.Add(SetValue(VarsType.Fixed, "WindowsVersion", sysVer.Version.ToString()));
            #endregion

            return logs;
        }

        public List<LogInfo> LoadDefaultGlobalVariables()
        {
            List<LogInfo> logs = new List<LogInfo>();

            // [Variables]
            if (project.MainScript.Sections.ContainsKey("Variables"))
            {
                logs = AddVariables(VarsType.Global, project.MainScript.Sections["Variables"]);
                logs.Add(new LogInfo(LogState.None, Logger.LogSeperator));
            }

            return logs;
        }

        public List<LogInfo> LoadDefaultScriptVariables(Script p)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // ScriptFile
            SetValue(VarsType.Fixed, "ScriptFile", p.RealPath);

            // ScriptDir
            SetValue(VarsType.Fixed, "ScriptDir", Path.GetDirectoryName(p.RealPath));

            // ScriptTitle
            SetValue(VarsType.Fixed, "ScriptTitle", p.Title);

            // [Variables]
            if (p.Sections.ContainsKey("Variables"))
            {
                List<LogInfo> subLogs = AddVariables(p.IsMainScript ? VarsType.Global : VarsType.Local, p.Sections["Variables"]);
                if (0 < subLogs.Count)
                {
                    logs.Add(new LogInfo(LogState.Info, "Import Variables from [Variables]", 0));
                    logs.AddRange(LogInfo.AddDepth(subLogs, 1));
                    logs.Add(new LogInfo(LogState.Info, $"Imported {subLogs.Count} variables", 0));
                    logs.Add(new LogInfo(LogState.None, Logger.LogSeperator, 0));
                }
            }

            // [Interface]
            ScriptSection iface = p.GetInterface(out string ifaceSecName);
            if (iface != null)
            {
                List<UIControl> uiCtrls = null;
                try { uiCtrls = p.Sections[ifaceSecName].GetUICtrls(true); }
                catch { } // No [Interface] section, or unable to get List<UIControl>

                if (uiCtrls != null)
                {
                    List<LogInfo> subLogs = UIControlToVariables(uiCtrls);
                    if (0 < subLogs.Count)
                    {
                        logs.Add(new LogInfo(LogState.Info, $"Import Variables from [{ifaceSecName}]", 0));
                        logs.AddRange(LogInfo.AddDepth(subLogs, 1));
                        logs.Add(new LogInfo(LogState.Info, $"Imported {subLogs.Count} variables", 0));
                        logs.Add(new LogInfo(LogState.None, Logger.LogSeperator, 0));
                    }
                }
            }

            return logs;
        }
        #endregion

        #region UIControlToVariable
        public LogInfo? UIControlToVariable(UIControl uiCmd, string prefix = null)
        {
            string destVar = uiCmd.Key;
            if (!string.IsNullOrEmpty(prefix))
                destVar = $"{prefix}_{uiCmd.Key}";

            string value = uiCmd.GetValue();
            if (value != null)
                return SetValue(VarsType.Local, destVar, value);
            else
                return null;
        }

        public List<LogInfo> UIControlToVariables(List<UIControl> uiCtrls, string prefix = null)
        {
            List<LogInfo> logs = new List<LogInfo>(uiCtrls.Count);

            foreach (UIControl uiCmd in uiCtrls)
            {
                LogInfo? log = UIControlToVariable(uiCmd, prefix);
                if (log != null)
                    logs.Add((LogInfo)log);
            }

            return logs;
        }
        #endregion

        #region CircularReference
        /// <summary>
        /// Check variables' circular reference.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns>Return true if circular reference exists.</returns>
        public bool CheckCircularReference(string key, string value)
        {
            /*
            if (rawValue.IndexOf($"%{key}%", StringComparison.OrdinalIgnoreCase) == -1)
            { // Ex) %Joveler%=Variel\ied206.txt
                return false;
            }
            else
            { // Ex) %Joveler%=Variel\%Joveler%\ied206.txt
                // Try Expand 
                // Set,%A%,%B% / Set,%B%,%C% / Set,%C%,%A% can also cause circular reference
                return true;
            }
            */

            /*
             * Set,%Joveler%,Variel\ied206.txt -> OK
             * Set,%Joveler%,Variel\%Joveler%\ied206.txt -> Wrong
             * 
             * Set,%A%,%B%
             * Set,%B%,%C%
             * Set,%C%,%A% -> Wrong
             */

            string str = value;
            while (true)
            {
                if (str.IndexOf($"%{key}%", StringComparison.OrdinalIgnoreCase) != -1) // Found circular reference
                    return true;

                string next = StringEscaper.UnescapePercent(Expand(value));
                if (str.Equals(next, StringComparison.Ordinal))
                    break;
                str = next;
            }

            return false;
        }
        #endregion

        #region Get, Set, Expand Value
        private Dictionary<string, string> GetVarsMatchesType(VarsType type)
        {
            switch (type)
            {
                case VarsType.Local:
                    return localVars;
                case VarsType.Global:
                    return globalVars;
                case VarsType.Fixed:
                    return fixedVars;
                default:
                    return null;
            }
        }

        public Dictionary<string, string> GetVarDict(VarsType type)
        { // Return a copy of varDict
            return new Dictionary<string, string>(GetVarsMatchesType(type), StringComparer.OrdinalIgnoreCase);
        }

        public void SetVarDict(VarsType type, Dictionary<string, string> varDict)
        {
            switch (type)
            {
                case VarsType.Local:
                    localVars = new Dictionary<string, string>(varDict, StringComparer.OrdinalIgnoreCase);
                    break;
                case VarsType.Global:
                    globalVars = new Dictionary<string, string>(varDict, StringComparer.OrdinalIgnoreCase);
                    break;
                case VarsType.Fixed:
                    fixedVars = new Dictionary<string, string>(varDict, StringComparer.OrdinalIgnoreCase);
                    break;
            }
        }

        public LogInfo SetValue(VarsType type, string key, string _value, bool expand = false)
        {
            Dictionary<string, string> vars = GetVarsMatchesType(type);

            /*
            // Check circular reference
            if (CheckCircularReference(key, _value))
            { // Ex) %Joveler%=Variel\%Joveler%\ied206.txt - Error!
                // This code cannot handle this case : [Set,%ScriptPathShort%,\%ScriptPathShort%]
                // return new LogInfo(LogState.Error, $"Variable [%{key}%] has circular reference in [{rawValue}]");

                // To Handle [Set,%ScriptPathShort%,\%ScriptPathShort%], if curcular reference detected, bake into final form
                vars[key] = StringEscaper.UnescapePercent(Expand(_value));
            }
            else
            { // Ex) %Joveler%=Variel\ied206.txt -> Success
                vars[key] = _value;
            }

            return new LogInfo(LogState.Success, $"{type} variable [%{key}%] set to [{vars[key]}]");
            */

            if (expand)
                vars[key] = Expand(_value);
            else
                vars[key] = _value;

            return new LogInfo(LogState.Success, $"{type} variable [%{key}%] set to [{vars[key]}]");

            /*
            // Check circular reference
            if (CheckCircularReference(key, rawValue))
            { // Ex) %Joveler%=Variel\%Joveler%\ied206.txt - Error!
                // This code cannot handle this case : [Set,%ScriptPathShort%,\%ScriptPathShort%]
                // return new LogInfo(LogState.Error, $"Variable [%{key}%] has circular reference in [{rawValue}]");

                // To Handle [Set,%ScriptPathShort%,\%ScriptPathShort%], if curcular reference detected, bake into final form
                vars[key] = Expand(rawValue);
                return new LogInfo(LogState.Success, $"{type} variable [%{key}%] set to [{vars[key]}]");
            }
            else
            { // Ex) %Joveler%=Variel\ied206.txt - Success
                vars[key] = rawValue;
                return new LogInfo(LogState.Success, $"{type} variable [%{key}%] set to [{rawValue}]");
            }
            */
        }

        public string GetValue(string key)
        {
            bool result = TryGetValue(key, out string value);
            if (result == false)
                value = string.Empty;
            return value;
        }

        public string GetValue(VarsType type, string key)
        {
            Dictionary<string, string> vars = GetVarsMatchesType(type);
            bool result = vars.TryGetValue(key, out string value);
            if (result)
                value = Expand(value);
            else
                value = string.Empty;
            return value;
        }

        public bool Delete(VarsType type, string key)
        {
            Dictionary<string, string> vars = GetVarsMatchesType(type);
            if (vars.ContainsKey(key))
            {
                vars.Remove(key);
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool ContainsKey(string key)
        {
            return localVars.ContainsKey(key) || globalVars.ContainsKey(key) || fixedVars.ContainsKey(key); 
        }

        public bool ContainsKey(VarsType type, string key)
        {
            Dictionary<string, string> vars = GetVarsMatchesType(type);
            return vars.ContainsKey(key);
        }

        public bool ContainsValue(string _val)
        {
            return localVars.ContainsValue(_val) || globalVars.ContainsValue(_val) || fixedVars.ContainsValue(_val);
        }

        public bool ContainsValue(VarsType type, string _val)
        {
            Dictionary<string, string> vars = GetVarsMatchesType(type);
            return vars.ContainsValue(_val);
        }

        public override string ToString()
        {
            StringBuilder str = new StringBuilder();
            str.AppendLine("[Local Variables]");
            foreach (var local in localVars)
                str.AppendLine($"[{local.Key}, {local.Value}, {Expand(local.Value)}]");
            str.AppendLine("[Global Variables]");
            foreach (var global in globalVars)
                str.AppendLine($"[{global.Key}, {global.Value}, {Expand(global.Value)}]");
            return str.ToString();
        }

        public bool TryGetValue(string key, out string value)
        {
            bool fixedResult = fixedVars.TryGetValue(key, out string fixedValue);
            bool globalResult = globalVars.TryGetValue(key, out string globalValue);
            bool localResult = localVars.TryGetValue(key, out string localValue);

            if (localResult)
                value = Expand(localValue);
            else if(globalResult)
                value = Expand(globalValue);
            else if (fixedResult)
                value = Expand(fixedValue);
            else
                value = string.Empty;

            return fixedResult || localResult || globalResult;
        }
        #endregion

        #region Expand
        public string Expand(string str)
        {
            int iter = 0;

            MatchCollection matches;
            do
            {
                // Expand variable's name into value
                // Ex) 123%BaseDir%456%OS%789
                StringBuilder b = new StringBuilder();
                matches = Regex.Matches(str, @"%([^ %]+)%", RegexOptions.Compiled | RegexOptions.CultureInvariant);
                for (int x = 0; x < matches.Count; x++)
                {
                    string varName = matches[x].Groups[1].Value;

                    if (x == 0)
                    {
                        b.Append(str.Substring(0, matches[0].Index));
                    }
                    else
                    {
                        int startOffset = matches[x - 1].Index + matches[x - 1].Value.Length;
                        int endOffset = matches[x].Index - startOffset;
                        b.Append(str.Substring(startOffset, endOffset));
                    }

                    if (localVars.ContainsKey(varName))
                    {
                        string varValue = localVars[varName];
                        b.Append(varValue);
                    }
                    else if (globalVars.ContainsKey(varName))
                    {
                        string varValue = globalVars[varName];
                        b.Append(varValue);
                    }
                    else if (fixedVars.ContainsKey(varName))
                    {
                        string varValue = fixedVars[varName];
                        b.Append(varValue);
                    }
                    else // variable not found
                    {
                        b.Append("#$p").Append(varName).Append("#$p");
                    }

                    if (x + 1 == matches.Count) // Last iteration
                        b.Append(str.Substring(matches[x].Index + matches[x].Value.Length));
                }

                if (0 < matches.Count) // Copy it only if variable exists
                    str = b.ToString();

                iter++;
                if (32 < iter)
                    throw new VariableCircularReferenceException($"Circular Reference by [{str}]");
            }
            while (0 < matches.Count);

            return str;
        }
        #endregion

        #region AddVariables
        public List<LogInfo> AddVariables(VarsType type, ScriptSection section)
        {
            Dictionary<string, string> dict = null;

            if (section.DataType == SectionDataType.IniDict)
                dict = section.GetIniDict();
            else if (section.DataType == SectionDataType.Lines)
                dict = Ini.ParseIniLinesVarStyle(section.GetLines());
            else
                throw new ExecuteException($"Section [{section.SectionName}] is not IniDict or Lines");

            if (dict.Keys.Count != 0)
                return InternalAddDictionary(type, dict);
            else // empty
                return new List<LogInfo>();
        }

        public List<LogInfo> AddVariables(VarsType type, string[] lines)
        {
            Dictionary<string, string> vars = GetVarsMatchesType(type);
            Dictionary<string, string> dict = Ini.ParseIniLinesVarStyle(lines);
            return InternalAddDictionary(type, dict);
        }

        public List<LogInfo> AddVariables(VarsType type, IEnumerable<string> lines)
        {
            Dictionary<string, string> dict = Ini.ParseIniLinesVarStyle(lines);
            return InternalAddDictionary(type, dict);
        }

        public List<LogInfo> AddVariables(VarsType type, Dictionary<string, string> dict)
        {
            return InternalAddDictionary(type, dict);
        }

        /// <summary>
        /// Add local variables
        /// </summary>
        /// <param name="vars"></param>
        /// <param name="dict"></param>
        /// <param name="sectionDepth"></param>
        /// <param name="errorOff"></param>
        /// <returns>Return true if success</returns>
        private List<LogInfo> InternalAddDictionary(VarsType type, Dictionary<string, string> dict)
        {
            Dictionary<string, string> vars = GetVarsMatchesType(type);

            List<LogInfo> logs = new List<LogInfo>(64);
            foreach (var kv in dict)
            {
                string value = kv.Value.Trim().Trim('\"');
                logs.Add(SetValue(type, kv.Key, value));
            }
            return logs;
        }
        #endregion

        #region ResetVariables
        public void ResetVariables(VarsType type)
        {
            switch (type)
            {
                case VarsType.Local:
                    localVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    break;
                case VarsType.Global:
                    globalVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    break;
            }
        }
        #endregion

        #region Utility Static Methods
        public static string TrimPercentMark(string varName)
        {
            if (!(varName.StartsWith("%", StringComparison.Ordinal) && varName.EndsWith("%", StringComparison.Ordinal)))
                throw new VariableInvalidFormatException($"[{varName}] is not enclosed with %");
            varName = varName.Substring(1, varName.Length - 2);
            if (varName.Contains('%'))
                throw new VariableInvalidFormatException($"% cannot be placed in the middle of [{varName}]");
            return varName;
        }

        /// <summary>
        /// Return % trimmed string, to use as variable key.
        /// Return null if this string cannot be used as variable key.
        /// </summary>
        /// <param name="varName"></param>
        /// <returns></returns>
        public static string GetVariableName(EngineState s, string varName)
        {
            if (varName.StartsWith("%") && varName.EndsWith("%"))
            {
                if (StringHelper.CountOccurrences(varName, "%") == 2)
                {
                    string varKey = varName.Substring(1, varName.Length - 2);
                    return StringEscaper.ExpandSectionParams(s, varKey);
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        public static int GetSectionParamIndex(string secParam)
        {
            Match match = Regex.Match(secParam, @"(#[0-9]+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
            if (match.Success)
            {
                if (NumberHelper.ParseInt32(secParam.Substring(1), out int paramIdx))
                    return paramIdx;
                else
                    return 0; // Error
            }
            else
                return 0; // Error
        }

        public const string VarKeyRegex_ContainsVariable = @"(%[a-zA-Z0-9_\-#\(\)\.]+%)";
        public const string VarKeyRegex_ContainsSectionParams = @"(#[0-9]+)";
        public const string VarKeyRegex_Variable = @"^" + VarKeyRegex_ContainsVariable + @"$";
        public const string VarKeyRegex_SectionParams = @"^" + VarKeyRegex_ContainsSectionParams + @"$";
        public enum VarKeyType { None, Variable, SectionParams, ReturnValue }
        public static VarKeyType DetermineType(string key)
        {
            if (Regex.Match(key, Variables.VarKeyRegex_Variable, RegexOptions.Compiled | RegexOptions.CultureInvariant).Success) // Ex) %A%
                return VarKeyType.Variable;  // %#[0-9]+% -> Compatibility Shim
            else if (Regex.Match(key, Variables.VarKeyRegex_SectionParams, RegexOptions.Compiled | RegexOptions.CultureInvariant).Success) // Ex) #1, #2, #3, ...
                return VarKeyType.SectionParams;
            else if (key.Equals("#r", StringComparison.OrdinalIgnoreCase)) // Ex) #r
                return VarKeyType.ReturnValue;
            else
                return VarKeyType.None;
        }

        public static LogInfo SetSectionParam(EngineState s, string key, string value)
        {
            int pIdx = Variables.GetSectionParamIndex(key);
            return SetSectionParam(s, pIdx, value);
        }

        public static LogInfo SetSectionParam(EngineState s, int pIdx, string value)
        {
            if (pIdx <= 0)
                return new LogInfo(LogState.Error, $"Section parmeter's index [{pIdx}] must be a positive integer");
            if (value.IndexOf($"#{pIdx}", StringComparison.Ordinal) != -1)
                return new LogInfo(LogState.Error, $"Section parameter cannot have a circular reference");
                
            s.CurSectionParams[pIdx] = value;
            return new LogInfo(LogState.Success, $"Section parameter [#{pIdx}] set to [{value}]");
        }

        public static List<LogInfo> SetVariable(EngineState s, string key, string _value, bool global = false, bool permanent = false, bool expand = true)
        {
            List<LogInfo> logs = new List<LogInfo>(1);

            // WB082 Behavior : Final form (expanded string) is written to varaibles.
            //                  Note that $#p will not be unescaped to %.
            // When preprocessed value is nil, it will be removed from dict.

            string finalValue;
            if (expand)
                // finalValue = StringEscaper.Preprocess(s, _value, false);
                finalValue = StringEscaper.ExpandVariables(s, _value);
            else
                finalValue = _value;

            Variables.VarKeyType type = Variables.DetermineType(key);
            if (finalValue.Equals("NIL", StringComparison.OrdinalIgnoreCase))
            { // Remove variable
                // Determine varKey's type - %A% vs #1
                if (type == Variables.VarKeyType.Variable) // %A%
                {
                    key = Variables.GetVariableName(s, key);
                    if (key == null)
                        logs.Add(new LogInfo(LogState.Error, $"Invalid variable name. [{key}] must start and end with %"));

                    if (permanent)
                    {
                        bool globalResult = s.Variables.Delete(VarsType.Global, key);
                        bool localResult = s.Variables.Delete(VarsType.Local, key);
                        if (globalResult || localResult)
                        {
                            if (Ini.DeleteKey(s.Project.MainScript.RealPath, "Variables", $"%{key}%")) // Delete var line
                            {
                                logs.Add(new LogInfo(LogState.Success, $"Permanent variable [%{key}%] was deleted"));
                            }
                            else
                            {
                                if (globalResult)
                                    logs.Add(new LogInfo(LogState.Success, $"Global variable [%{key}%] was deleted"));
                                else if (localResult)
                                    logs.Add(new LogInfo(LogState.Success, $"Local variable [%{key}%] was deleted"));
                                else
                                    throw new InternalException("Internal Error at Variables.SetVariable");
                            }
                        }
                        else
                        {
                            logs.Add(new LogInfo(LogState.Ignore, $"Permanent variable [%{key}%] does not exist"));
                        }
                    }
                    else // Global, Local
                    {
                        bool globalResult = s.Variables.Delete(VarsType.Global, key);
                        bool localResult = s.Variables.Delete(VarsType.Local, key);
                        if (globalResult)
                            logs.Add(new LogInfo(LogState.Success, $"Global variable [%{key}%] was deleted"));
                        else if (localResult)
                            logs.Add(new LogInfo(LogState.Success, $"Local variable [%{key}%] was deleted"));
                        else
                            logs.Add(new LogInfo(LogState.Ignore, $"Variable [%{key}%] does not exist"));
                    }
                }
                else if (type == Variables.VarKeyType.SectionParams) // #1, #2, #3, ...
                { // WB082 does not remove section parameter, just set to string "NIL"
                    logs.Add(Variables.SetSectionParam(s, key, finalValue));
                }
                else if (type == Variables.VarKeyType.ReturnValue) // #r
                { // s.SectionReturnValue's defalt value is string.Empty
                    s.SectionReturnValue = string.Empty;
                    logs.Add(new LogInfo(LogState.Success, $"ReturnValue [#r] deleted"));
                }
                else
                {
                    throw new InvalidCodeCommandException($"Invalid variable name [{key}]");
                }
            }
            else
            {
                // Determine varKey's type - %A% vs #1
                if (type == Variables.VarKeyType.Variable) // %A%
                {
                    key = Variables.GetVariableName(s, key);
                    if (key == null)
                        logs.Add(new LogInfo(LogState.Error, $"Invalid variable name [{key}], must start and end with %"));

                    // Logs are written in variables.SetValue method
                    if (global)
                    {
                        LogInfo log = s.Variables.SetValue(VarsType.Global, key, finalValue);
                        logs.Add(log);

                        // Remove local variable if exist
                        if (log.State == LogState.Success)
                            s.Variables.Delete(VarsType.Local, key);
                    }
                    else if (permanent)
                    {
                        LogInfo log = s.Variables.SetValue(VarsType.Global, key, finalValue);

                        if (log.State == LogState.Success)
                        { // SetValue success, write to IniFile
                            if (Ini.SetKey(s.Project.MainScript.RealPath, "Variables", $"%{key}%", finalValue)) // To ensure final form being written
                                logs.Add(new LogInfo(LogState.Success, $"Permanent variable [%{key}%] set to [{finalValue}]"));
                            else
                                logs.Add(new LogInfo(LogState.Error, $"Failed to write permanent variable [%{key}%] and its value [{finalValue}] into script.project"));

                            // Remove local variable if exist
                            s.Variables.Delete(VarsType.Local, key);
                        }
                        else
                        { // SetValue failed
                            logs.Add(log);
                        }
                    }
                    else // Local
                    {
                        logs.Add(s.Variables.SetValue(VarsType.Local, key, finalValue));
                    }
                }
                else if (type == Variables.VarKeyType.SectionParams) // #1, #2, #3, ...
                {
                    logs.Add(Variables.SetSectionParam(s, key, finalValue));
                }
                else if (type == Variables.VarKeyType.ReturnValue) // #r
                {
                    s.SectionReturnValue = finalValue;
                    logs.Add(new LogInfo(LogState.Success, $"ReturnValue [#r] set to [{finalValue}]"));
                }
                else
                {
                    throw new InvalidCodeCommandException($"Invalid variable name [{key}]");
                }
            }

            return logs;
        }
        #endregion

        #region Clone
        public object Clone()
        {
            Variables variables = new Variables(project)
            {
                fixedVars = new Dictionary<string, string>(this.fixedVars, StringComparer.OrdinalIgnoreCase),
                globalVars = new Dictionary<string, string>(this.globalVars, StringComparer.OrdinalIgnoreCase),
                localVars = new Dictionary<string, string>(this.localVars, StringComparer.OrdinalIgnoreCase),
            };
            return variables;
        }
        #endregion
    }
}
