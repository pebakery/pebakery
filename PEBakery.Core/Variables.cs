/*
    Copyright (C) 2016-2022 Hajin Jang
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

using PEBakery.Helper;
using PEBakery.Ini;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace PEBakery.Core
{
    public enum VarsType
    {
        Fixed = 0,
        Global = 1,
        Local = 2,
    }

    public class Variables
    {
        /*
         * Variables Search Order
         * if (OverridableFixedVariables) // WinBuilder Compatible, enabled by compat option
         *    1. Local Variables
         *    2. Global Variables
         *    3. Fixed Variables
         * else // PEBakery Standard
         *    1. Fixed Variables
         *    2. Local Variables
         *    3. Global Variables
         */

        #region struct Options
        public struct Options
        {
            // Compatibility
            public bool OverridableFixedVariables;
            public bool EnableEnvironmentVariables;

            public static Options CreateOptions(CompatOption compat)
            {
                return new Options
                {
                    OverridableFixedVariables = compat.OverridableFixedVariables,
                    EnableEnvironmentVariables = compat.EnableEnvironmentVariables,
                };
            }
        }
        #endregion

        #region Fields and Properties
        private readonly Project _project;
        private readonly Options _opts;
        private Dictionary<string, string> _fixedVars;
        private Dictionary<string, string> _globalVars;
        private Dictionary<string, string> _localVars;

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
        public Variables(Project project, Options opts)
        {
            _project = project;
            _localVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _globalVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _fixedVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _opts = opts;

            LoadDefaultFixedVariables();
            LoadDefaultGlobalVariables();
        }

        public Variables(Project project, CompatOption compat)
        {
            _project = project;
            _localVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _globalVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _fixedVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _opts = Options.CreateOptions(compat);

            LoadDefaultFixedVariables();
            LoadDefaultGlobalVariables();
        }

        public Variables(Project project, out List<LogInfo> logs)
        {
            _project = project;
            _localVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _globalVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _fixedVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            logs = LoadDefaultFixedVariables();
            logs.AddRange(LoadDefaultGlobalVariables());
        }
        #endregion

        #region LoadDefaults
        public List<LogInfo> LoadDefaultFixedVariables()
        {
            List<LogInfo> logs = new List<LogInfo>(32);

            #region Project Title
            // Read from MainScript
            string fullPath = _project.MainScript.RealPath;
            IniKey[] keys =
            {
                new IniKey("Main", "Title"),
            };
            keys = IniReadWriter.ReadKeys(fullPath, keys);
            Dictionary<string, string> dict = keys.ToDictionary(x => x.Key, x => x.Value);

            string projectTitle = dict["Title"];
            if (string.IsNullOrWhiteSpace(projectTitle))
                projectTitle = _project.ProjectName;

            // ProjectTitle
            logs.Add(SetValue(VarsType.Fixed, "ProjectTitle", projectTitle));
            #endregion

            #region Builder Variables
            // PEBakery
            logs.Add(SetValue(VarsType.Fixed, "PEBakery", "True"));
            // BaseDir
            logs.Add(SetValue(VarsType.Fixed, "BaseDir", _project.BaseDir.TrimEnd('\\')));
            // Version
            logs.Add(SetValue(VarsType.Fixed, "Version", "082")); // WB082 Compatibility Shim
            logs.Add(SetValue(VarsType.Fixed, "EngineVersion", Global.Const.EngineVersion.ToString("000")));
            logs.Add(SetValue(VarsType.Fixed, "PEBakeryVersion", Global.Const.ProgramVersionStr));
            #endregion

            #region Envrionment Variables
            if (_opts.EnableEnvironmentVariables)
            {
                List<(string WinVarName, string PebVarName)> envVarNames = new List<(string, string)>
                { // Item1 - Windows Env Var Name, Item2 - PEBakery Env Var Name
                    ("TEMP", "TempDir"),
                    ("USERNAME", "UserName"),
                    ("USERPROFILE", "UserProfile"),
                    ("WINDIR", "WindowsDir"),
                    ("ProgramFiles", "ProgramFilesDir"),
                };

                if (Environment.Is64BitProcess)
                    envVarNames.Add(("ProgramFiles(x86)", "ProgramFilesDir_x86"));

                foreach ((string winVarName, string pebVarName) in envVarNames)
                {
                    string envValue = Environment.GetEnvironmentVariable(winVarName);
                    if (envValue == null)
                        logs.Add(new LogInfo(LogState.Error, $"Cannot get [%{winVarName}%] from Windows"));
                    else
                        logs.Add(SetValue(VarsType.Fixed, pebVarName, envValue));
                }

                // WindowsVersion
                Version winVer = FileHelper.WindowsVersion();
                logs.Add(SetValue(VarsType.Fixed, "WindowsVersion", winVer.ToString()));

                // Processor Type
                switch (RuntimeInformation.OSArchitecture)
                {
                    // https://docs.microsoft.com/en-us/windows/desktop/api/sysinfoapi/ns-sysinfoapi-_system_info
                    // For compatibility with WinBuilder, use old SYSTEM_INFO.dwProcessorType description for x86 and x64.
                    case Architecture.X86:
                        logs.Add(SetValue(VarsType.Fixed, "ProcessorType", "586"));
                        break;
                    case Architecture.X64:
                        logs.Add(SetValue(VarsType.Fixed, "ProcessorType", "8664"));
                        break;
                    case Architecture.Arm:
                        logs.Add(SetValue(VarsType.Fixed, "ProcessorType", "Arm"));
                        break;
                    case Architecture.Arm64:
                        logs.Add(SetValue(VarsType.Fixed, "ProcessorType", "Arm64"));
                        break;
                    default:
                        logs.Add(SetValue(VarsType.Fixed, "ProcessorType", "Unknown"));
                        break;
                }
            }
            #endregion

            return logs;
        }

        public List<LogInfo> LoadDefaultGlobalVariables()
        {
            List<LogInfo> logs = new List<LogInfo>();

            #region SourceDir, TargetDir, ISOFile
            // Read from MainScript
            string fullPath = _project.MainScript.RealPath;
            IniKey[] keys =
            {
                new IniKey("Main", "PathSetting"),
                new IniKey("Main", "SourceDir"),
                new IniKey("Main", "TargetDir"),
                new IniKey("Main", "ISOFile"),
            };
            keys = IniReadWriter.ReadKeys(fullPath, keys);
            Dictionary<string, string> dict = keys.ToDictionary(x => x.Key, x => x.Value);

            // If PathSetting is set to False, do not set SourceDir, TargetDir and ISOFile
            bool pathEnabled = true;
            string pathEnabledStr = dict["PathSetting"];
            if (pathEnabledStr != null && pathEnabledStr.Equals("False", StringComparison.OrdinalIgnoreCase))
                pathEnabled = false;

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
                        if (0 < dir.Length)
                        {
                            sourceDir = dir;
                            break;
                        }
                    }
                }

                // SourceDir
                logs.Add(SetValue(VarsType.Global, "SourceDir", sourceDir));

                // TargetDir
                string targetDirStr = dict["TargetDir"];
                string targetDir = Path.Combine("%BaseDir%", "Target", _project.ProjectName);
                if (!string.IsNullOrEmpty(targetDirStr))
                    targetDir = targetDirStr;
                logs.Add(SetValue(VarsType.Global, "TargetDir", targetDir));

                // ISOFile, ISODir
                string isoFileStr = dict["ISOFile"];
                string isoFile = Path.Combine("%BaseDir%", "ISO", _project.ProjectName + ".iso");
                if (!string.IsNullOrEmpty(isoFileStr))
                    isoFile = isoFileStr;
                logs.Add(SetValue(VarsType.Global, "ISOFile", isoFile));
                logs.Add(SetValue(VarsType.Global, "ISODir", FileHelper.GetDirNameEx(isoFile)));
            }

            // ProjectDir
            logs.Add(SetValue(VarsType.Global, "ProjectDir", Path.Combine("%BaseDir%", "Projects", _project.ProjectName)));
            #endregion

            #region Project Variables
            // [Variables]
            if (_project.MainScript.Sections.ContainsKey(ScriptSection.Names.Variables))
            {
                logs = AddVariables(VarsType.Global, _project.MainScript.Sections[ScriptSection.Names.Variables]);
                logs.Add(new LogInfo(LogState.None, Logger.LogSeparator));
            }
            #endregion

            return logs;
        }

        public List<LogInfo> LoadDefaultScriptVariables(Script sc)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Per-Script Variables
            SetValue(VarsType.Fixed, "ScriptFile", sc.RealPath);
            SetValue(VarsType.Fixed, "ScriptDir", Path.GetDirectoryName(sc.RealPath));
            SetValue(VarsType.Fixed, "ScriptTitle", sc.Title);

            // [Variables]
            if (sc.Sections.ContainsKey(ScriptSection.Names.Variables))
            {
                List<LogInfo> subLogs = AddVariables(sc.IsMainScript ? VarsType.Global : VarsType.Local, sc.Sections[ScriptSection.Names.Variables]);
                if (0 < subLogs.Count)
                {
                    logs.Add(new LogInfo(LogState.Info, "Import Variables from [Variables]", 0));
                    logs.AddRange(LogInfo.AddDepth(subLogs, 1));
                    logs.Add(new LogInfo(LogState.Info, $"Imported {subLogs.Count} variables", 0));
                    logs.Add(new LogInfo(LogState.None, Logger.LogSeparator, 0));
                }
            }

            // [Interface]
            (string ifaceSectionName, List<UIControl> uiCtrls, _) = sc.GetInterfaceControls();
            if (ifaceSectionName != null && uiCtrls != null)
            {
                List<LogInfo> subLogs = UIControlToVariables(uiCtrls);
                if (0 < subLogs.Count)
                {
                    logs.Add(new LogInfo(LogState.Info, $"Import Variables from [{ifaceSectionName}]", 0));
                    logs.AddRange(LogInfo.AddDepth(subLogs, 1));
                    logs.Add(new LogInfo(LogState.Info, $"Imported {subLogs.Count} variables", 0));
                    logs.Add(new LogInfo(LogState.None, Logger.LogSeparator, 0));
                }
            }

            return logs;
        }
        #endregion

        #region UIControlToVariable
        public LogInfo UIControlToVariable(UIControl uiCmd, string prefix = null)
        {
            string destVar = uiCmd.Key;
            if (!string.IsNullOrEmpty(prefix))
                destVar = $"{prefix}_{uiCmd.Key}";

            string value = uiCmd.GetValue(true);
            if (value != null)
                return SetValue(VarsType.Local, destVar, value);
            return null;
        }

        public List<LogInfo> UIControlToVariables(List<UIControl> uiCtrls, string prefix = null)
        {
            List<LogInfo> logs = new List<LogInfo>(uiCtrls.Count);

            foreach (UIControl uiCtrl in uiCtrls)
            {
                LogInfo log = UIControlToVariable(uiCtrl, prefix);
                if (log != null)
                    logs.Add(log);
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
             * Set,%Joveler%,PEBakery\ied206.txt -> OK
             * Set,%Joveler%,PEBakery\%Joveler%\ied206.txt -> Wrong
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
            return type switch
            {
                VarsType.Local => _localVars,
                VarsType.Global => _globalVars,
                VarsType.Fixed => _fixedVars,
                _ => null,
            };
        }

        public Dictionary<string, string> GetVarDict(VarsType type)
        { // Return a copy of varDict
            return new Dictionary<string, string>(GetVarsMatchesType(type), StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Overwrite variable dicts.
        /// </summary>
        /// <param name="type">Which variables are to be overwritten?</param>
        /// <param name="varDict">New key-values to overwrite.</param>
        /// <param name="keysToPreserve">Do not overwrite these variables. It should not contain %. </param>
        public void SetVarDict(VarsType type, Dictionary<string, string> varDict, IEnumerable<string> keysToPreserve = null)
        {
            Dictionary<string, string> newDict = new Dictionary<string, string>(varDict, StringComparer.OrdinalIgnoreCase);

            // Preserve keys 
            if (keysToPreserve != null)
            {
                Dictionary<string, string> oldDict = null;
                switch (type)
                {
                    case VarsType.Local:
                        oldDict = _localVars;
                        break;
                    case VarsType.Global:
                        oldDict = _globalVars;
                        break;
                    case VarsType.Fixed:
                        oldDict = _fixedVars;
                        break;
                }
                Debug.Assert(oldDict != null, "Invalid VarsType");

                foreach (string key in keysToPreserve)
                {
                    Debug.Assert(key != null, "Invalid key name");
                    if (oldDict.ContainsKey(key))
                        newDict[key] = oldDict[key];
                }
            }

            switch (type)
            {
                case VarsType.Local:
                    _localVars = newDict;
                    break;
                case VarsType.Global:
                    _globalVars = newDict;
                    break;
                case VarsType.Fixed:
                    _fixedVars = newDict;
                    break;
            }
        }

        public LogInfo SetValue(VarsType type, string key, string value, bool expand = false)
        {
            Dictionary<string, string> vars = GetVarsMatchesType(type);

            if (expand)
                vars[key] = Expand(value);
            else
                vars[key] = value;

            return new LogInfo(LogState.Success, $"{type} variable [%{key}%] set to [{vars[key]}]");
        }

        public string GetValue(string key)
        {
            bool result = TryGetValue(key, out string value);
            return result ? value : string.Empty;
        }

        public string GetValue(VarsType type, string key)
        {
            Dictionary<string, string> vars = GetVarsMatchesType(type);
            bool result = vars.TryGetValue(key, out string value);
            return result ? Expand(value) : string.Empty;
        }

        public bool DeleteKey(VarsType type, string key)
        {
            Dictionary<string, string> vars = GetVarsMatchesType(type);
            if (!vars.ContainsKey(key))
                return false;

            vars.Remove(key);
            return true;
        }

        public bool ContainsKey(string key)
        {
            return _localVars.ContainsKey(key) || _globalVars.ContainsKey(key) || _fixedVars.ContainsKey(key);
        }

        public bool ContainsKey(VarsType type, string key)
        {
            Dictionary<string, string> vars = GetVarsMatchesType(type);
            return vars.ContainsKey(key);
        }

        public bool ContainsValue(string val)
        {
            return _localVars.ContainsValue(val) || _globalVars.ContainsValue(val) || _fixedVars.ContainsValue(val);
        }

        public bool ContainsValue(VarsType type, string val)
        {
            Dictionary<string, string> vars = GetVarsMatchesType(type);
            return vars.ContainsValue(val);
        }

        public override string ToString()
        {
            StringBuilder str = new StringBuilder();
            str.AppendLine("[Local Variables]");
            foreach (var local in _localVars)
                str.AppendLine($"[{local.Key}, {local.Value}, {Expand(local.Value)}]");
            str.AppendLine("[Global Variables]");
            foreach (var global in _globalVars)
                str.AppendLine($"[{global.Key}, {global.Value}, {Expand(global.Value)}]");
            return str.ToString();
        }

        public bool TryGetValue(string key, out string value)
        {
            bool fixedResult = _fixedVars.TryGetValue(key, out string fixedValue);
            bool globalResult = _globalVars.TryGetValue(key, out string globalValue);
            bool localResult = _localVars.TryGetValue(key, out string localValue);

            if (_opts.OverridableFixedVariables)
            { // WinBuilder compatible
                if (localResult)
                    value = Expand(localValue);
                else if (globalResult)
                    value = Expand(globalValue);
                else if (fixedResult)
                    value = Expand(fixedValue);
                else
                    value = string.Empty;
            }
            else
            { // PEBakery standard
                if (fixedResult)
                    value = Expand(fixedValue);
                else if (localResult)
                    value = Expand(localValue);
                else if (globalResult)
                    value = Expand(globalValue);
                else
                    value = string.Empty;
            }

            return fixedResult || localResult || globalResult;
        }
        #endregion

        #region Exists
        public bool Exists(string key)
        {
            bool fixedResult = _fixedVars.ContainsKey(key);
            bool globalResult = _globalVars.ContainsKey(key);
            bool localResult = _localVars.ContainsKey(key);
            return fixedResult || localResult || globalResult;
        }

        public bool Exists(VarsType type, string key)
        {
            Dictionary<string, string> vars = GetVarsMatchesType(type);
            return vars.ContainsKey(key);
        }
        #endregion

        #region Expand
        public string Expand(string str)
        {
            int iteration = 0;

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

                    if (_opts.OverridableFixedVariables)
                    { // WinBuilder compatible
                        if (_localVars.ContainsKey(varName))
                        {
                            string varValue = _localVars[varName];
                            b.Append(varValue);
                        }
                        else if (_globalVars.ContainsKey(varName))
                        {
                            string varValue = _globalVars[varName];
                            b.Append(varValue);
                        }
                        else if (_fixedVars.ContainsKey(varName))
                        {
                            string varValue = _fixedVars[varName];
                            b.Append(varValue);
                        }
                        else // variable not found
                        {
                            b.Append("#$p").Append(varName).Append("#$p");
                        }
                    }
                    else
                    { // PEBakery standard
                        if (_fixedVars.ContainsKey(varName))
                        {
                            string varValue = _fixedVars[varName];
                            b.Append(varValue);
                        }
                        else if (_localVars.ContainsKey(varName))
                        {
                            string varValue = _localVars[varName];
                            b.Append(varValue);
                        }
                        else if (_globalVars.ContainsKey(varName))
                        {
                            string varValue = _globalVars[varName];
                            b.Append(varValue);
                        }
                        else // variable not found
                        {
                            b.Append("#$p").Append(varName).Append("#$p");
                        }
                    }

                    if (x + 1 == matches.Count) // Last iteration
                        b.Append(str.Substring(matches[x].Index + matches[x].Value.Length));
                }

                if (0 < matches.Count) // Copy it only if variable exists
                    str = b.ToString();

                iteration++;
                if (32 < iteration)
                    throw new VariableCircularReferenceException($"Circular reference by [{str}]");
            }
            while (0 < matches.Count);

            return str;
        }
        #endregion

        #region AddVariables
        public List<LogInfo> AddVariables(VarsType type, ScriptSection section)
        {
            List<LogInfo> logs = new List<LogInfo>();

            string[] lines = section.Lines;
            if (lines == null)
            {
                logs.Add(new LogInfo(LogState.Warning, $"Unable to load section [{section.Name}]"));
                return logs;
            }

            Dictionary<string, string> dict = IniReadWriter.ParseIniLinesVarStyle(lines);
            if (0 < dict.Keys.Count)
                logs.AddRange(InternalAddDictionary(type, dict));

            return logs;
        }

        public List<LogInfo> AddVariables(VarsType type, IEnumerable<string> lines)
        {
            Dictionary<string, string> dict = IniReadWriter.ParseIniLinesVarStyle(lines);
            return InternalAddDictionary(type, dict);
        }

        public List<LogInfo> AddVariables(VarsType type, Dictionary<string, string> dict)
        {
            return InternalAddDictionary(type, dict);
        }

        /// <summary>
        /// Add local variables
        /// </summary>
        /// <returns>Return true if success</returns>
        private List<LogInfo> InternalAddDictionary(VarsType type, Dictionary<string, string> dict)
        {
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
                    _localVars.Clear();
                    break;
                case VarsType.Global:
                    _globalVars.Clear();
                    break;
            }
        }
        #endregion

        #region Static Methods - Utility
        public static string TrimPercentMark(string varKey)
        {
            if (varKey == null)
                throw new ArgumentNullException(nameof(varKey));

            if (!(varKey.StartsWith("%", StringComparison.Ordinal) && varKey.EndsWith("%", StringComparison.Ordinal)))
                return null;
            varKey = varKey.Substring(1, varKey.Length - 2);
            if (varKey.Contains('%'))
                return null;
            return varKey;
        }
        #endregion

        #region Static Methods - With EngineState
        /// <summary>
        /// Return % trimmed string, to use as variable key.
        /// Return null if this string cannot be used as variable key.
        /// </summary>
        /// <returns></returns>
        public static string GetVariableName(EngineState s, string varName)
        {
            if (!varName.StartsWith("%") || !varName.EndsWith("%"))
                return null;
            if (StringHelper.CountSubStr(varName, "%") != 2)
                return null;
            string varKey = varName.Substring(1, varName.Length - 2);
            return StringEscaper.ExpandSectionParams(s, varKey);
        }

        public const string VarKeyRegexContainsVariable = @"(%[a-zA-Z0-9_\-#\(\)\.]+%)";
        public const string VarKeyRegexContainsSectionInParams = @"(#[1-9])";
        public const string VarKeyRegexContainsSectionOutParams = @"(#[oO][1-9])";
        public const string VarKeyRegexVariable = @"^" + VarKeyRegexContainsVariable + @"$";
        public const string VarKeyRegexSectionInParams = @"^" + VarKeyRegexContainsSectionInParams + @"$";
        public const string VarKeyRegexSectionOutParams = @"^" + VarKeyRegexContainsSectionOutParams + @"$";
        public enum VarKeyType { None, Variable, SectionInParams, SectionOutParams, ReturnValue, LoopCounter }
        public static VarKeyType DetectType(string key)
        {
            if (Regex.Match(key, VarKeyRegexVariable, RegexOptions.Compiled | RegexOptions.CultureInvariant).Success) // Ex) %A%
                return VarKeyType.Variable;  // %#[0-9]+% -> Compatibility Shim
            if (Regex.Match(key, VarKeyRegexSectionInParams, RegexOptions.Compiled | RegexOptions.CultureInvariant).Success) // Ex) #1, #2, #3, ...
                return VarKeyType.SectionInParams;
            if (Regex.Match(key, VarKeyRegexSectionOutParams, RegexOptions.Compiled | RegexOptions.CultureInvariant).Success) // Ex) #o1, #o2, #o3, ...
                return VarKeyType.SectionOutParams;
            if (key.Equals("#r", StringComparison.OrdinalIgnoreCase)) // Return Value
                return VarKeyType.ReturnValue;
            if (key.Equals("#c", StringComparison.OrdinalIgnoreCase)) // Loop Counter
                return VarKeyType.LoopCounter;
            return VarKeyType.None;
        }

        public static int GetSectionInParamIndex(string secParam)
        {
            Match match = Regex.Match(secParam, VarKeyRegexContainsSectionInParams, RegexOptions.Compiled | RegexOptions.CultureInvariant);
            if (match.Success)
            {
                if (NumberHelper.ParseInt32(secParam.Substring(1), out int paramIdx))
                    return paramIdx;
                else
                    return 0; // Error
            }

            return 0; // Error
        }

        public static int GetSectionOutParamIndex(string secParam)
        {
            Match match = Regex.Match(secParam, VarKeyRegexContainsSectionOutParams, RegexOptions.Compiled | RegexOptions.CultureInvariant);
            if (match.Success)
            {
                if (NumberHelper.ParseInt32(secParam.Substring(2), out int paramIdx))
                    return paramIdx;
                else
                    return 0; // Error
            }

            return 0; // Error
        }

        public static string GetSectionOutParamVarKey(EngineState s, string secParam)
        {
            int soIdx = GetSectionOutParamIndex(secParam);
            if (soIdx == 0)
                return null; // Error
            if (s.CurSectionOutParams.Count == 0 || s.CurSectionOutParams.Count < soIdx)
                return null;

            return s.CurSectionOutParams[soIdx - 1]; // %Dest%
        }

        public static LogInfo SetSectionInParam(EngineState s, string key, string value)
        {
            int pIdx = GetSectionInParamIndex(key);
            return SetSectionInParam(s, pIdx, value);
        }

        public static LogInfo SetSectionInParam(EngineState s, int pIdx, string value)
        {
            if (pIdx <= 0)
                return new LogInfo(LogState.Error, $"Section parameter's index [{pIdx}] must be a positive integer");
            if (value.IndexOf($"#{pIdx}", StringComparison.Ordinal) != -1)
                return new LogInfo(LogState.Error, "Section parameter cannot have a circular reference");

            s.CurSectionInParams[pIdx] = value;
            return new LogInfo(LogState.Success, $"Section parameter [#{pIdx}] set to [{value}]");
        }

        public static LogInfo SetSectionOutParam(EngineState s, string key, string value)
        {
            int pIdx = GetSectionOutParamIndex(key);
            return SetSectionOutParam(s, pIdx, value);
        }

        public static LogInfo SetSectionOutParam(EngineState s, int pIdx, string value)
        {
            // pIdx starts from 1 
            if (pIdx <= 0)
                return new LogInfo(LogState.Error, $"Section out parameter's index [{pIdx}] must be a positive integer");
            if (value.IndexOf($"#o{pIdx}", StringComparison.OrdinalIgnoreCase) != -1)
                return new LogInfo(LogState.Error, "Section out parameter cannot have a circular reference");
            if (s.CurSectionOutParams.Count == 0 || s.CurSectionOutParams.Count <= pIdx - 1)
                return new LogInfo(LogState.Error, $"[#o{pIdx}] is not referencing any variables");

            // Write to varKey
            string varKey = s.CurSectionOutParams[pIdx - 1]; // %Dest%
            string key = GetVariableName(s, varKey); // %D%
            if (key == null) // This must not happen, must check before calling this method
                return new LogInfo(LogState.CriticalError, $"[#o{pIdx}] is referencing invalid variable");

            s.Variables.SetValue(VarsType.Local, key, value);
            return new LogInfo(LogState.Success, $"[{varKey}], reference of [#o{pIdx}], set to [{value}]");
        }

        /// <summary>
        /// Public interface for variables write/delete operation 
        /// </summary>
        /// <param name="s">EngineState</param>
        /// <param name="varKey">Key with its prefix/postfix. Ex) %A%, #1, #c, etc</param>
        /// <param name="varValue">Value to write. Put "NIL" for deletion</param>
        /// <param name="global"></param>
        /// <param name="permanent"></param>
        /// <param name="expand"></param>
        /// <returns></returns>
        public static List<LogInfo> SetVariable(EngineState s, string varKey, string varValue, bool global = false, bool permanent = false, bool expand = true)
        {
            List<LogInfo> logs = new List<LogInfo>(1);

            // WB082 Behavior : Final form (expanded string) is written to varaibles.
            //                  Note that $#p will not be unescaped to %.
            // When preprocessed value is "NIL", it will be removed from the dict.

            string finalValue;
            if (expand)
                finalValue = StringEscaper.ExpandVariables(s, varValue);
            else
                finalValue = varValue;

            VarKeyType type = DetectType(varKey);
            if (finalValue.Equals("NIL", StringComparison.OrdinalIgnoreCase))
            { // Remove variable
                // Determine varKey's type - %A% vs #1
                if (type == VarKeyType.Variable) // %A%
                {
                    string key = GetVariableName(s, varKey);
                    if (key == null)
                        logs.Add(new LogInfo(LogState.Error, $"Invalid variable name. [{varKey}] must start and end with %"));

                    if (permanent)
                    {
                        bool globalResult = s.Variables.DeleteKey(VarsType.Global, key);
                        bool localResult = s.Variables.DeleteKey(VarsType.Local, key);
                        if (globalResult || localResult)
                        {
                            // Delete variable line from file
                            if (IniReadWriter.DeleteKey(s.Project.MainScript.RealPath, "Variables", $"%{key}%"))
                                logs.Add(new LogInfo(LogState.Success, $"Permanent variable [%{key}%] was deleted"));
                            else
                                logs.Add(new LogInfo(LogState.Success, globalResult ? $"Global variable [%{key}%] was deleted" : $"Local variable [%{key}%] was deleted"));

                            // https://github.com/pebakery/pebakery/issues/88
                            // Delete variable line from memory-cached MainScript
                            if (s.Project.MainScript.Sections.ContainsKey(ScriptSection.Names.Variables))
                            {
                                ScriptSection varSect = s.Project.MainScript.Sections[ScriptSection.Names.Variables];
                                varSect.DeleteIniKey($"%{key}%");
                            }
                        }
                        else
                        {
                            logs.Add(new LogInfo(LogState.Ignore, $"Permanent variable [%{key}%] does not exist"));
                        }
                    }
                    else // Global, Local
                    {
                        bool globalResult = s.Variables.DeleteKey(VarsType.Global, key);
                        bool localResult = s.Variables.DeleteKey(VarsType.Local, key);
                        if (globalResult)
                            logs.Add(new LogInfo(LogState.Success, $"Global variable [%{key}%] was deleted"));
                        else if (localResult)
                            logs.Add(new LogInfo(LogState.Success, $"Local variable [%{key}%] was deleted"));
                        else
                            logs.Add(new LogInfo(LogState.Ignore, $"Variable [%{key}%] does not exist"));
                    }
                }
                else if (type == VarKeyType.SectionInParams) // #1, #2, #3, ...
                { // WB082 does not remove section parameter, just set to string "NIL"
                    logs.Add(SetSectionInParam(s, varKey, finalValue));
                }
                else if (type == VarKeyType.SectionOutParams) // #o1, #o2, #o3, ...
                { // WB082 does not remove section parameter, just set to string "NIL"
                    if (!s.CompatDisableExtendedSectionParams)
                        logs.Add(SetSectionOutParam(s, varKey, finalValue));
                    else
                        logs.Add(new LogInfo(LogState.Warning, "Section out parameters are disabled by the compatibility option"));
                }
                else if (type == VarKeyType.ReturnValue) // #r
                { // s.SectionReturnValue's default value is string.Empty
                    if (!s.CompatDisableExtendedSectionParams)
                    {
                        s.ReturnValue = string.Empty;
                        logs.Add(new LogInfo(LogState.Success, "ReturnValue [#r] deleted"));
                    }
                    else
                    {
                        logs.Add(new LogInfo(LogState.Ignore, "ReturnValue [#r] is disabled by the compatibility option"));
                    }
                }
                else if (type == VarKeyType.LoopCounter)
                { // #c
                    logs.Add(new LogInfo(LogState.Warning, "LoopCounter [#c] cannot be deleted"));
                }
                else
                {
                    throw new InvalidCommandException($"Invalid variable name [{varKey}]");
                }
            }
            else
            {
                // Determine varKey's type - %A% vs #1
                if (type == VarKeyType.Variable) // %A%
                {
                    string key = GetVariableName(s, varKey);
                    if (key == null)
                        logs.Add(new LogInfo(LogState.Error, $"Invalid variable name [{varKey}], must start and end with %"));

                    // Does this variable resides in fixedDict?
                    if (!s.Variables._opts.OverridableFixedVariables)
                    {
                        if (s.Variables.Exists(VarsType.Fixed, key))
                        {
                            logs.Add(new LogInfo(LogState.Warning, $"Fixed variable [{varKey}] cannot be overriden"));
                            return logs;
                        }
                    }

                    // Logs are written in variables.SetValue method
                    if (global)
                    {
                        LogInfo log = s.Variables.SetValue(VarsType.Global, key, finalValue);
                        logs.Add(log);

                        // Remove local variable if exist
                        if (log.State == LogState.Success)
                            s.Variables.DeleteKey(VarsType.Local, key);
                    }
                    else if (permanent)
                    {
                        LogInfo log = s.Variables.SetValue(VarsType.Global, key, finalValue);

                        if (log.State == LogState.Success)
                        { // SetValue success
                            // Write to MainScript
                            if (IniReadWriter.WriteKey(s.Project.MainScript.RealPath, ScriptSection.Names.Variables, $"%{key}%", finalValue)) // To ensure final form being written
                                logs.Add(new LogInfo(LogState.Success, $"Permanent variable [%{key}%] set to [{finalValue}]"));
                            else
                                logs.Add(new LogInfo(LogState.Error, $"Failed to write permanent variable [%{key}%] and its value [{finalValue}] into script.project"));

                            // https://github.com/pebakery/pebakery/issues/88
                            // Update memory-cached MainScript's Variables section 
                            if (s.Project.MainScript.Sections.ContainsKey(ScriptSection.Names.Variables))
                            {
                                ScriptSection varSect = s.Project.MainScript.Sections[ScriptSection.Names.Variables];
                                varSect.UpdateIniKey($"%{key}%", finalValue);
                            }
                            else
                            { // Create temp ScriptSection instance
                                ScriptSection varSect = new ScriptSection(
                                    s.Project.MainScript, ScriptSection.Names.Variables, SectionType.Variables,
                                    new string[] { $"%{key}%={finalValue}" }, 0);
                                s.Project.MainScript.Sections[ScriptSection.Names.Variables] = varSect;
                            }

                            // Remove local variable if exist
                            s.Variables.DeleteKey(VarsType.Local, key);
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
                else if (type == VarKeyType.SectionInParams) // #1, #2, #3, ...
                {
                    logs.Add(SetSectionInParam(s, varKey, finalValue));
                }
                else if (type == VarKeyType.SectionOutParams) // #o1, #o2, #o3, ...
                {
                    if (!s.CompatDisableExtendedSectionParams)
                        logs.Add(SetSectionOutParam(s, varKey, finalValue));
                    else
                        logs.Add(new LogInfo(LogState.Warning, "Section out parameters are disabled by the compatibility option"));
                }
                else if (type == VarKeyType.ReturnValue) // #r
                {
                    if (!s.CompatDisableExtendedSectionParams)
                    {
                        s.ReturnValue = finalValue;
                        logs.Add(new LogInfo(LogState.Success, $"ReturnValue [#r] set to [{finalValue}]"));
                    }
                    else
                    {
                        logs.Add(new LogInfo(LogState.Warning, "ReturnValue [#r] is disabled by the compatibility option"));
                    }
                }
                else if (type == VarKeyType.LoopCounter)
                { // #c
                    if (!s.CompatOverridableLoopCounter)
                    {
                        logs.Add(new LogInfo(LogState.Warning, "LoopCounter [#c] cannot be overriden"));
                        return logs;
                    }

                    // Escape #c (Loop Counter)
                    if (0 < s.LoopStateStack.Count)
                    {
                        EngineLoopState peekLoop = s.LoopStateStack.Peek();
                        switch (peekLoop.State)
                        {
                            case EngineLoopState.LoopState.OnIndex:
                                if (!NumberHelper.ParseInt64(finalValue, out long ctr))
                                {
                                    logs.Add(new LogInfo(LogState.Error, $"Loop is iterating an index, but new value [{finalValue}] is not a valid integer"));
                                    return logs;
                                }

                                // C#'s struct is immutable, so do pop and push.
                                EngineLoopState idxLoop = new EngineLoopState(ctr);
                                s.LoopStateStack.Pop();
                                s.LoopStateStack.Push(idxLoop);

                                logs.Add(new LogInfo(LogState.Success, $"LoopCounter [#c] set to [{ctr}]"));
                                break;
                            case EngineLoopState.LoopState.OnDriveLetter:
                                if (!(finalValue.Length == 1 && StringHelper.IsAlphabet(finalValue[0])))
                                {
                                    logs.Add(new LogInfo(LogState.Error, $"Loop is iterating a drive letter, but new value [{finalValue}] is not a valid drive letter"));
                                    return logs;
                                }

                                // C#'s struct is immutable, so do pop and push.
                                // EngineLoopState's constructor performs conversion to capital alphabet.
                                EngineLoopState charLoop = new EngineLoopState(finalValue[0]);
                                s.LoopStateStack.Pop();
                                s.LoopStateStack.Push(charLoop);

                                logs.Add(new LogInfo(LogState.Success, $"LoopCounter [#c] set to [{charLoop.CounterLetter}]"));
                                break;
                        }
                    }
                    else
                    {
                        logs.Add(new LogInfo(LogState.Error, "Loop is not running, unable to update LoopCounter [#c]"));
                        return logs;
                    }
                }
                else
                {
                    throw new InvalidCommandException($"Invalid variable name [{varKey}]");
                }
            }

            return logs;
        }

        /// <summary>
        /// Check if key is stored in Variables|SectionInParam|SectionOutParam|ReturnValue.
        /// </summary>
        /// <param name="s"></param>
        /// <param name="varKey"></param>
        /// <returns>Return null at error</returns>
        public static bool? ContainsKey(EngineState s, string varKey)
        {
            if (varKey == null)
                throw new ArgumentNullException(nameof(varKey));

            VarKeyType type = DetectType(varKey);
            switch (type)
            {
                case VarKeyType.Variable:
                    string key = TrimPercentMark(varKey);
                    return key != null && s.Variables.ContainsKey(key);
                case VarKeyType.SectionInParams:
                    int siIdx = GetSectionInParamIndex(varKey);
                    return siIdx != 0 && s.CurSectionInParams.ContainsKey(siIdx);
                case VarKeyType.SectionOutParams:
                    varKey = GetSectionOutParamVarKey(s, varKey);
                    goto case VarKeyType.Variable;
                case VarKeyType.ReturnValue:
                    return true;
                default:
                    return null;
            }
        }
        #endregion

        #region DeepCopy
        public Variables DeepCopy()
        {
            Variables variables = new Variables(_project, _opts)
            {
                _fixedVars = new Dictionary<string, string>(_fixedVars, StringComparer.OrdinalIgnoreCase),
                _globalVars = new Dictionary<string, string>(_globalVars, StringComparer.OrdinalIgnoreCase),
                _localVars = new Dictionary<string, string>(_localVars, StringComparer.OrdinalIgnoreCase),
            };
            return variables;
        }
        #endregion
    }
}
