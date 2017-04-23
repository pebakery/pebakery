using PEBakery.Exceptions;
using PEBakery.Helper;
using PEBakery.Lib;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

            // BaseDir
            logs.Add(SetFixedValue("BaseDir", project.BaseDir));
            // Tools
            logs.Add(SetFixedValue("Tools", Path.Combine("%BaseDir%", "Projects", "Tools")));

            //  Version
            // Version version = FileHelper.GetProgramVersion();
            // logs.Add(SetFixedValue("Version", version.Build.ToString()));
            
            logs.Add(SetFixedValue("Version", WPF.App.Version.ToString()));
            // ProjectDir
            logs.Add(SetFixedValue("ProjectDir", Path.Combine("%BaseDir%", "Projects", project.ProjectName)));
            // TargetDir
            logs.Add(SetFixedValue("TargetDir", Path.Combine("%BaseDir%", "Target", project.ProjectName)));

            return logs;
        }

        public List<LogInfo> LoadDefaultGlobalVariables()
        {
            List<LogInfo> logs = new List<LogInfo>();

            // [Variables]
            if (project.MainPlugin.Sections.ContainsKey("Variables"))
            {
                logs.AddRange(AddVariables(VarsType.Global, project.MainPlugin.Sections["Variables"]));
            }

            return logs;
        }

        public List<LogInfo> LoadDefaultPluginVariables(Plugin p)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // ScriptFile, PluginFile
            SetValue(VarsType.Local, "PluginFile", p.FullPath);
            SetValue(VarsType.Local, "ScriptFile", p.FullPath);

            // [Variables]
            if (p.Sections.ContainsKey("Variables"))
            {
                VarsType type = VarsType.Local;
                if (string.Equals(p.FullPath, project.MainPlugin.FullPath, StringComparison.OrdinalIgnoreCase))
                    type = VarsType.Global;
                logs.AddRange(AddVariables(type, p.Sections["Variables"]));
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
                throw new InternalErrorException("Fixed variables cannot be written without privilege!");

            Dictionary<string, string> vars = GetVarsMatchesType(type);
            // Check and remove circular reference
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
            while (0 < FileHelper.CountStringOccurrences(str, @"%"))
            {
                // Ex) Invalid : %Base%Dir%
                //if (FileHelper.CountStringOccurrences(str, @"%") % 2 == 1)
                //    throw new InvalidCommandException(@"Variable names must be enclosed by %");

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
            Dictionary<string, string> vars = GetVarsMatchesType(type);
            Dictionary<string, string> dict = null;
            if (section.DataType == SectionDataType.IniDict)
                dict = section.GetIniDict();
            else if (section.DataType == SectionDataType.Lines)
                dict = Ini.ParseLinesVarStyle(section.GetLines());
            if (section.Count != 0)
            {
                return InternalAddDictionary(vars, dict);
            }
            else
            { // empty
                return new List<LogInfo>();
            }
        }

        public List<LogInfo> AddVariables(VarsType type, string[] lines)
        {
            Dictionary<string, string> vars = GetVarsMatchesType(type);
            Dictionary<string, string> dict = Ini.ParseLinesVarStyle(lines);
            return InternalAddDictionary(vars, dict);
        }

        public List<LogInfo> AddVariables(VarsType type, List<string> lines)
        {
            Dictionary<string, string> vars = GetVarsMatchesType(type);
            Dictionary<string, string> dict = Ini.ParseLinesVarStyle(lines);
            return InternalAddDictionary(vars, dict);
        }

        public List<LogInfo> AddVariables(VarsType type, Dictionary<string, string> dict)
        {
            Dictionary<string, string> vars = GetVarsMatchesType(type);
            return InternalAddDictionary(vars, dict);
        }

        /// <summary>
        /// Add local variables
        /// </summary>
        /// <param name="vars"></param>
        /// <param name="dict"></param>
        /// <param name="sectionDepth"></param>
        /// <param name="errorOff"></param>
        /// <returns>Return true if success</returns>
        private List<LogInfo> InternalAddDictionary(Dictionary<string, string> vars, Dictionary<string, string> dict)
        {
            List<LogInfo> list = new List<LogInfo>();
            foreach (var kv in dict)
            {
                if (kv.Value.IndexOf($"%{kv.Key}%", StringComparison.OrdinalIgnoreCase) == -1)
                { // Ex) %TargetImage%=%TargetImage%
                    vars[kv.Key] = kv.Value;
                    list.Add(new LogInfo(LogState.Success, $"Var [%{kv.Key}%] set to [{kv.Value}]"));
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
                    localVars = new Dictionary<string, string>();
                    break;
                case VarsType.Global:
                    globalVars = new Dictionary<string, string>();
                    break;
            }
        }

        #region Utility Static Methods
        public static string TrimPercentMark(string varName)
        {
            if (!(varName.StartsWith("%", StringComparison.OrdinalIgnoreCase) && varName.EndsWith("%", StringComparison.OrdinalIgnoreCase)))
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
        public static string GetVariableName(string varName)
        {
            if (varName.StartsWith("%") && varName.EndsWith("%"))
            {
                if (FileHelper.CountStringOccurrences(varName, "%") == 2)
                    return varName.Substring(1, varName.Length - 2);
                else
                    return null;
            }
            else
            {
                return null;
            }
        }

        public static int GetSectionParamIndex(string secParam)
        {
            Match matches = Regex.Match(secParam, @"(#\d+)", RegexOptions.Compiled);
            if (matches.Success)
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
                varKey = Variables.GetVariableName(varKey);
                if (varKey == null)
                    logs.Add(new LogInfo(LogState.Error, $"Invalid variable name [{varKey}], must start and end with %"));

                // Logs are written in variables.SetValue method
                if (global)
                {
                    logs.Add(s.Variables.SetValue(VarsType.Global, varKey, varValue));
                }
                else if (permanent)
                {
                    LogInfo log = s.Variables.SetValue(VarsType.Global, varKey, varValue);
                    logs.Add(log);

                    if (log.State == LogState.Success)
                    { // SetValue success, write to IniFile
                        if (Ini.SetKey(s.Project.MainPlugin.FullPath, "Variables", varKey, varValue))
                            logs.Add(new LogInfo(LogState.Success, $"Permanent variable [%{varKey}%] set to [{varValue}]"));
                        else
                            logs.Add(new LogInfo(LogState.Error, $"Failed to write permanent variable [%{varKey}%] and its value [{varValue}] into script.project"));
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
