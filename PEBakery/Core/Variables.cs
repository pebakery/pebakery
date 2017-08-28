﻿using PEBakery.Exceptions;
using PEBakery.Helper;
using PEBakery.Lib;
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

    public class Variables
    {
        /*
         * Variables 우선순위
         * local variables > global variables > fixed variables
         */

        // Fields
        private Project project;
        private Dictionary<string, string> fixedVars; // Once constructed, it must be read-only.
        private Dictionary<string, string> globalVars;
        private Dictionary<string, string> localVars;

        // Properties
        public Dictionary<string, string> FixedVars { get => fixedVars; }
        public Dictionary<string, string> GlobalVars { get => globalVars; }
        public Dictionary<string, string> LocalVars { get => localVars; }

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

        private List<LogInfo> LoadDefaultFixedVariables()
        {
            List<LogInfo> logs = new List<LogInfo>();

            #region Builder Variables
            // BaseDir
            logs.Add(SetFixedValue("BaseDir", project.BaseDir));
            // Tools
            logs.Add(SetFixedValue("Tools", Path.Combine("%BaseDir%", "Projects", "Tools")));
            // Version
            logs.Add(SetFixedValue("Version", App.Version.ToString()));
            #endregion

            #region Project Variables
            // Read from MainPlugin
            string fullPath = project.MainPlugin.FullPath;
            IniKey[] keys = new IniKey[]
            {
                new IniKey("Main", "SourceDir"),
                new IniKey("Main", "TargetDir"),
                new IniKey("Main", "ISOFile"),
            };
            keys = Ini.GetKeys(fullPath, keys);
            Dictionary<string, string> dict = keys.ToDictionary(x => x.Key, x => x.Value);

            // SourceDir
            string sourceDir = string.Empty;
            string sourceDirs = dict["SourceDir"];
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

            // ProjectDir
            logs.Add(SetFixedValue("ProjectDir", Path.Combine("%BaseDir%", "Projects", project.ProjectName)));
            // SourceDir
            logs.Add(SetFixedValue("SourceDir", sourceDir));
            // TargetDir
            logs.Add(SetFixedValue("TargetDir", dict["TargetDir"]));
            // ISOFile
            logs.Add(SetFixedValue("ISOFile", dict["ISOFile"]));
            // ISODir
            logs.Add(SetFixedValue("ISODir", Path.GetDirectoryName(dict["ISOFile"])));
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
                    logs.Add(SetFixedValue(tuple.Item2, envValue));
            }

            // WindowsVersion
            OperatingSystem sysVer = Environment.OSVersion;
            logs.Add(SetFixedValue("WindowsVersion", sysVer.Version.ToString()));
            #endregion

            return logs;
        }

        public List<LogInfo> LoadDefaultGlobalVariables()
        {
            List<LogInfo> logs = new List<LogInfo>();

            // [Variables]
            if (project.MainPlugin.Sections.ContainsKey("Variables"))
            {
                logs.AddRange(AddVariables(VarsType.Global, project.MainPlugin.Sections["Variables"]));
                logs.Add(new LogInfo(LogState.None, Logger.LogSeperator));
            }

            return logs;
        }

        public List<LogInfo> LoadDefaultPluginVariables(Plugin p)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // ScriptFile, PluginFile
            SetValue(VarsType.Local, "PluginFile", p.FullPath);
            SetValue(VarsType.Local, "ScriptFile", p.FullPath);

            // ScriptDir, PluginDir
            SetValue(VarsType.Local, "PluginDir", Path.GetDirectoryName(p.FullPath));
            SetValue(VarsType.Local, "ScriptDir", Path.GetDirectoryName(p.FullPath));

            // [Variables]
            if (p.Sections.ContainsKey("Variables"))
            {
                VarsType type = VarsType.Local;
                if (p.FullPath.Equals(project.MainPlugin.FullPath, StringComparison.OrdinalIgnoreCase))
                    type = VarsType.Global;

                List<LogInfo> subLogs = AddVariables(type, p.Sections["Variables"]);
                if (0 < subLogs.Count)
                {
                    logs.Add(new LogInfo(LogState.Info, "Import variables from [Variables]", 0));
                    logs.AddRange(LogInfo.AddDepth(subLogs, 1));
                    logs.Add(new LogInfo(LogState.Info, $"Imported {subLogs.Count} variables", 0));
                    logs.Add(new LogInfo(LogState.None, Logger.LogSeperator, 0));
                }
            }

            // [Interface]
            string interfaceSectionName = "Interface";
            if (p.MainInfo.ContainsKey("Interface"))
                interfaceSectionName = p.MainInfo["Interface"];

            if (p.Sections.ContainsKey(interfaceSectionName))
            {
                List<UICommand> uiCodes = null;
                try { uiCodes = p.Sections[interfaceSectionName].GetUICodes(true); }
                catch { } // No [Interface] section, or unable to get List<UICommand>

                if (uiCodes != null)
                {
                    List<LogInfo> subLogs = UICommandToVariables(uiCodes);
                    if (0 < subLogs.Count)
                    {
                        logs.Add(new LogInfo(LogState.Info, $"Import variables from [{interfaceSectionName}]", 0));
                        logs.AddRange(LogInfo.AddDepth(subLogs, 1));
                        logs.Add(new LogInfo(LogState.Info, $"Imported {subLogs.Count} variables", 0));
                        logs.Add(new LogInfo(LogState.None, Logger.LogSeperator, 0));
                    }
                }
            }

            return logs;
        }

        public List<LogInfo> UICommandToVariables(List<UICommand> uiCodes)
        {
            List<LogInfo> logs = new List<LogInfo>();

            foreach (UICommand uiCmd in uiCodes)
            {
                bool valid = true;
                string key = uiCmd.Key;
                string value = string.Empty;

                switch (uiCmd.Type)
                {
                    case UIType.TextBox:
                        {
                            Debug.Assert(uiCmd.Info.GetType() == typeof(UIInfo_TextBox));
                            UIInfo_TextBox info = uiCmd.Info as UIInfo_TextBox;

                            value = info.Value;
                        }
                        break;
                    case UIType.NumberBox:
                        {
                            Debug.Assert(uiCmd.Info.GetType() == typeof(UIInfo_NumberBox));
                            UIInfo_NumberBox info = uiCmd.Info as UIInfo_NumberBox;

                            value = info.Value.ToString();
                        }
                        break;
                    case UIType.CheckBox:
                        {
                            Debug.Assert(uiCmd.Info.GetType() == typeof(UIInfo_CheckBox));
                            UIInfo_CheckBox info = uiCmd.Info as UIInfo_CheckBox;

                            value = info.Value.ToString();
                        }
                        break;
                    case UIType.ComboBox:
                        {
                            value = uiCmd.Text;
                        }
                        break;
                    case UIType.RadioButton:
                        {
                            Debug.Assert(uiCmd.Info.GetType() == typeof(UIInfo_RadioButton));
                            UIInfo_RadioButton info = uiCmd.Info as UIInfo_RadioButton;

                            value = info.Selected.ToString();
                        }
                        break;
                    case UIType.FileBox:
                        {
                            value = uiCmd.Text;
                        }
                        break;
                    case UIType.RadioGroup:
                        {
                            Debug.Assert(uiCmd.Info.GetType() == typeof(UIInfo_RadioGroup));
                            UIInfo_RadioGroup info = uiCmd.Info as UIInfo_RadioGroup;

                            if (0 <= info.Selected && info.Selected < info.Items.Count)
                                value = info.Items[info.Selected];
                            else
                                value = string.Empty;
                        }
                        break;
                    default:
                        valid = false;
                        break;
                }

                if (valid)
                    logs.Add(SetValue(VarsType.Local, key, value));
            }

            return logs;
        }

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

        public ReadOnlyDictionary<string, string> GetVars(VarsType type)
        {
            Dictionary<string, string> vars = GetVarsMatchesType(type);
            ReadOnlyDictionary<string, string> readOnlyDictionary = 
                new ReadOnlyDictionary<string, string>(vars.ToDictionary(k => k.Key, v => v.Value));
            return readOnlyDictionary;
        }


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

            string str = StringEscaper.UnescapePercent(Expand(value));
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

        public LogInfo SetFixedValue(string key, string rawValue)
        {
            return InternalSetValue(VarsType.Fixed, key, rawValue, true);
        }

        public LogInfo SetValue(VarsType type, string key, string rawValue)
        {
            return InternalSetValue(type, key, rawValue, false);
        }

        /// <summary>
        /// Return true if success
        /// </summary>
        /// <param name="type"></param>
        /// <param name="key"></param>
        /// <param name="rawValue"></param>
        /// <param name="privFixed">Privilege for write info fixed variables</param>
        /// <returns></returns>
        public LogInfo InternalSetValue(VarsType type, string key, string rawValue, bool privFixed)
        {
            if (!privFixed && type == VarsType.Fixed)
                throw new InternalException("Fixed variables cannot be written without privilege!");

            Dictionary<string, string> vars = GetVarsMatchesType(type);
            // Check circular reference
            if (CheckCircularReference(key, rawValue))
            { // Ex) %Joveler%=Variel\%Joveler%\ied206.txt - Error!
                return new LogInfo(LogState.Error, $"Variable [%{key}%] has circular reference in [{rawValue}]");
            }
            else
            { // Ex) %Joveler%=Variel\ied206.txt - Success
                vars[key] = rawValue;
                return new LogInfo(LogState.Success, $"{type} variable [%{key}%] set to [{rawValue}]");
            }
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

        public bool ContainsKey(string key)
        {
            return localVars.ContainsKey(key) || globalVars.ContainsKey(key);
        }

        public bool ContainsKey(VarsType type, string key)
        {
            Dictionary<string, string> vars = GetVarsMatchesType(type);
            return vars.ContainsKey(key);
        }

        public bool ContainsValue(string key)
        {
            return localVars.ContainsValue(key) || globalVars.ContainsValue(key);
        }

        public bool ContainsValue(VarsType type, string key)
        {
            Dictionary<string, string> vars = GetVarsMatchesType(type);
            return vars.ContainsValue(key);
        }

        public override string ToString()
        {
            StringBuilder str = new StringBuilder();
            str.AppendLine("[Local Variables]");
            foreach (var local in localVars)
            {
                str.AppendLine($"[{local.Key}, {local.Value}, {Expand(local.Value)}]");
            }
            str.AppendLine("[Global Variables]");
            foreach (var global in globalVars)
                str.AppendLine($"[{global.Key}, {global.Value}, {Expand(global.Value)}]");
            return str.ToString();
        }

        public bool TryGetValue(string key, out string value)
        {
            bool fixedResult = fixedVars.TryGetValue(key, out value);
            bool globalResult = globalVars.TryGetValue(key, out value);
            bool localResult = localVars.TryGetValue(key, out value);
            value = Expand(value);
            return fixedResult || localResult || globalResult;
        }

        public string Expand(string str)
        {
            while (2 <= FileHelper.CountStringOccurrences(str, @"%"))
            {
                // Expand variable's name into value
                // Ex) 123%BaseDir%456%OS%789
                MatchCollection matches = Regex.Matches(str, @"%([^%]+)%", RegexOptions.Compiled);
                StringBuilder builder = new StringBuilder();

                for (int x = 0; x < matches.Count; x++)
                {
                    string varName = matches[x].Groups[1].ToString();
                    if (x == 0)
                        builder.Append(str.Substring(0, matches[0].Index));
                    else
                    {
                        int startOffset = matches[x - 1].Index + matches[x - 1].Value.Length;
                        int endOffset = matches[x].Index - startOffset;
                        builder.Append(str.Substring(startOffset, endOffset));
                    }

                    if (globalVars.ContainsKey(varName))
                        builder.Append(globalVars[varName]);
                    else if (localVars.ContainsKey(varName))
                        builder.Append(localVars[varName]);
                    else if (fixedVars.ContainsKey(varName))
                        builder.Append(fixedVars[varName]);
                    else // variable not found
                        builder.Append("#$p").Append(varName).Append("#$p");

                    if (x + 1 == matches.Count) // Last iteration
                        builder.Append(str.Substring(matches[x].Index + matches[x].Value.Length));
                }
                if (0 < matches.Count) // Only copy it if variable exists
                {
                    str = builder.ToString();
                }
            }

            return str;
        }

        public List<LogInfo> AddVariables(VarsType type, PluginSection section)
        {
            Dictionary<string, string> dict = null;

            if (section.DataType == SectionDataType.IniDict)
                dict = section.GetIniDict();
            else if (section.DataType == SectionDataType.Lines)
                dict = Ini.ParseIniLinesVarStyle(section.GetLines());

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

            List<LogInfo> list = new List<LogInfo>();
            foreach (var kv in dict)
            {
                if (kv.Value.IndexOf($"%{kv.Key}%", StringComparison.OrdinalIgnoreCase) == -1)
                { // Ex) %TargetImage%=%TargetImage%
                    vars[kv.Key] = StringEscaper.QuoteUnescape(kv.Value);
                    
                    list.Add(new LogInfo(LogState.Success, $"{type} variable [%{kv.Key}%] set to [{kv.Value}]"));
                }
                else
                {
                    list.Add(new LogInfo(LogState.Error, $"Variable [%{kv.Key}%] has circular reference in [{kv.Value}]"));
                }
            }
            return list;
        }

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
                if (FileHelper.CountStringOccurrences(varName, "%") == 2)
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
            Match match = Regex.Match(secParam, @"(#\d+)", RegexOptions.Compiled);
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

        public enum VarKeyType { None, Variable, SectionParams }
        public static VarKeyType DetermineType(string key)
        {
            if (key.StartsWith("%") && key.EndsWith("%")) // Ex) %A%
                return VarKeyType.Variable;
            else if (Regex.Match(key, @"(#\d+)", RegexOptions.Compiled).Success) // Ex) #1, #2, #3, ...
                return VarKeyType.SectionParams;
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
                return new LogInfo(LogState.Error, $"Section parmeter's index [{pIdx}] must be positive integer");
            if (value.IndexOf($"#{pIdx}", StringComparison.Ordinal) != -1)
                return new LogInfo(LogState.Error, $"Section parameter cannot have circular reference");
                
            s.CurSectionParams[pIdx] = value;
            return new LogInfo(LogState.Success, $"Section parameter [#{pIdx}] set to [{value}]");
        }

        public static List<LogInfo> SetVariable(EngineState s, string varKey, string varValue, bool global = false, bool permanent = false)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Determine varKey's type - %A% vs #1
            Variables.VarKeyType type = Variables.DetermineType(varKey);

            if (type == Variables.VarKeyType.Variable) // %A%
            {
                varKey = Variables.GetVariableName(s, varKey);
                if (varKey == null)
                    logs.Add(new LogInfo(LogState.Error, $"Invalid variable name [{varKey}], must start and end with %"));

                // Logs are written in variables.SetValue method
                if (global)
                {
                    string finalValue = StringEscaper.ExpandVariables(s, varValue); // WB082 Behavior : final form is written in GLOBAL / PERMANENT
                    logs.Add(s.Variables.SetValue(VarsType.Global, varKey, finalValue));
                }
                else if (permanent)
                {
                    string finalValue = StringEscaper.ExpandVariables(s, varValue); // WB082 Behavior : final form is written in GLOBAL / PERMANENT
                    LogInfo log = s.Variables.SetValue(VarsType.Global, varKey, finalValue); 
                    logs.Add(log);

                    if (log.State == LogState.Success)
                    { // SetValue success, write to IniFile
                        if (Ini.SetKey(s.Project.MainPlugin.FullPath, "Variables", $"%{varKey}%", finalValue)) // To ensure final form being written
                            logs.Add(new LogInfo(LogState.Success, $"Permanent variable [%{varKey}%] set to [{finalValue}]"));
                        else
                            logs.Add(new LogInfo(LogState.Error, $"Failed to write permanent variable [%{varKey}%] and its value [{finalValue}] into script.project"));
                    }
                    else
                    { // SetValue failed
                        logs.Add(new LogInfo(LogState.Error, $"Variable [%{varKey}%] contains itself in [{varValue}]"));
                    }
                }
                else // Local
                {
                    logs.Add(s.Variables.SetValue(VarsType.Local, varKey, varValue));
                }
            }
            else if (type == Variables.VarKeyType.SectionParams) // #1, #2, #3, ...
            {
                logs.Add(Variables.SetSectionParam(s, varKey, varValue));
            }
            else
            {
                throw new InvalidCodeCommandException($"Invalid variable name [{varKey}]");
            }

            return logs;
        }
        #endregion
    }
}
