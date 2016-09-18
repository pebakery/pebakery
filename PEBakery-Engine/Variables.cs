using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BakeryEngine
{
    using StringDictionary = Dictionary<string, string>;
    public enum VarsType { Local, Global };

    public class VariableCircularReferenceException : Exception
    {
        public VariableCircularReferenceException() { }
        public VariableCircularReferenceException(string message) : base(message) { }
        public VariableCircularReferenceException(string message, Exception inner) : base(message, inner) { }
    }

    public class VariableInvalidFormatException : Exception
    {
        public VariableInvalidFormatException() { }
        public VariableInvalidFormatException(string message) : base(message) { }
        public VariableInvalidFormatException(string message, Exception inner) : base(message, inner) { }
    }

    public class BakeryVariables
    {
        /*
         * Variables 우선순위
         * local variables > global variables
         */
        
        // Fields
        private StringDictionary globalVars;
        private StringDictionary localVars;
        private Logger logger;

        // Properties
        public StringDictionary GlobalVars { get { return globalVars; } }
        public StringDictionary LocalVars { get { return localVars; } }


        /// <summary>
        /// Constructor
        /// </summary>
        public BakeryVariables(Logger logger)
        {
            this.logger = logger;
            this.localVars = new StringDictionary(StringComparer.OrdinalIgnoreCase);
            this.globalVars = new StringDictionary(StringComparer.OrdinalIgnoreCase);
        }

        private StringDictionary GetVarsMatchesType(VarsType type)
        {
            switch (type)
            {
                case VarsType.Local:
                    return localVars;
                case VarsType.Global:
                    return globalVars;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Check variables' circular reference.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="rawValue"></param>
        /// <returns>Return true if circular reference exists.</returns>
        public static bool CheckCircularReference(string key, string rawValue)
        {
            if (rawValue.IndexOf("%" + key + "%", StringComparison.OrdinalIgnoreCase) == -1)
            { // Ex) %Joveler%=Variel\ied206.txt
                return false;
            }
            else
            { // Ex) %Joveler%=Variel\%Joveler%\ied206.txt
                return true;
            }
        }

        public LogInfo SetValue(VarsType type, string key, string rawValue)
        {
            return InternalSetValue(type, null, key, rawValue, -1, false);
        }

        public LogInfo SetValue(VarsType type, string key, string rawValue, int depth)
        {
            return InternalSetValue(type, null, key, rawValue, depth, false);
        }

        public LogInfo SetValue(VarsType type, string key, string rawValue, bool errorOff)
        {
            return InternalSetValue(type, null, key, rawValue, -1, errorOff);
        }

        public LogInfo SetValue(VarsType type, string key, string rawValue, int depth, bool errorOff)
        {
            return InternalSetValue(type, null, key, rawValue, depth, errorOff);
        }

        public LogInfo SetValue(VarsType type, BakeryCommand cmd, string key, string rawValue)
        {
            return InternalSetValue(type, cmd, key, rawValue, -1, false);
        }

        public LogInfo SetValue(VarsType type, BakeryCommand cmd, string key, string rawValue, bool errorOff)
        {
            return InternalSetValue(type, cmd, key, rawValue, -1, errorOff);
        }

        public LogInfo InternalSetValue(VarsType type, BakeryCommand cmd, string key, string rawValue, int depth, bool errorOff)
        {
            StringDictionary vars = GetVarsMatchesType(type);
            LogInfo log;
            // Check and remove circular reference
            if (CheckCircularReference(key, rawValue))
            { // Ex) %Joveler%=Variel\%Joveler%\ied206.txt
                if (cmd == null)
                    log = new LogInfo(LogState.Error, $"Variable [%{key}%] contains itself in [{rawValue}]", depth, errorOff);
                else
                    log = new LogInfo(cmd, LogState.Error, $"Variable [%{key}%] contains itself in [{rawValue}]", errorOff);
            }
            else
            { // Ex) %Joveler%=Variel\ied206.txt
                vars[key] = rawValue;
                if (cmd == null)
                    log = new LogInfo(LogState.Success, $"{type} variable [%{key}%] set to [{rawValue}]", depth, errorOff);
                else
                    log = new LogInfo(cmd, LogState.Error, $"{type} variable [%{key}%] set to [{rawValue}]", errorOff);
            }
            return log;
        }

        public string GetValue(string key)
        {
            string value;
            bool result = TryGetValue(key, out value);
            if (result == false)
                value = string.Empty;
            return value;
        }

        public string GetValue(VarsType type, string key)
        {
            StringDictionary vars = GetVarsMatchesType(type);
            string value;
            bool result = vars.TryGetValue(key, out value);
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
            StringDictionary vars = GetVarsMatchesType(type);
            return vars.ContainsKey(key);
        }

        public bool ContainsValue(string key)
        {
            return localVars.ContainsValue(key) || globalVars.ContainsValue(key);
        }

        public bool ContainsValue(VarsType type, string key)
        {
            StringDictionary vars = GetVarsMatchesType(type);
            return vars.ContainsValue(key);
        }

        public override string ToString()
        {
            StringBuilder str = new StringBuilder("[Local Variables]\n");
            foreach (var local in localVars)
                str.Append($"[{local.Key}, {local.Value}, {Expand(local.Value)}]\n");
            str.Append("[Global Variables]\n");
            foreach (var global in globalVars)
                str.Append($"[{global.Key}, {global.Value}, {Expand(global.Value)}]\n");
            return str.ToString();
        }

        public bool TryGetValue(string key, out string value)
        {
            bool globalResult = globalVars.TryGetValue(key, out value);
            bool localResult = localVars.TryGetValue(key, out value);
            value = Expand(value);
            return localResult || globalResult;
        }

        public string Expand(string str)
        {
            while (0 < Helper.CountStringOccurrences(str, @"%"))
            {
                // Ex) Invalid : %Base%Dir%
                if (Helper.CountStringOccurrences(str, @"%") % 2 == 1)
                    throw new InvalidCommandException(@"Variable names must be enclosed by %");

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

        public LogInfo[] AddVariables(VarsType type, PluginSection section)
        {
            List<LogInfo> logs = new List<LogInfo>();
            StringDictionary vars = GetVarsMatchesType(type);
            if ((section.Get() as StringDictionary).Count != 0)
            {
                logs.Add(new LogInfo(LogState.Info, $"Processing section [{section.SectionName}]", -1));
                logs.AddRange(InternalAddDictionary(vars, section.Get() as StringDictionary, 0, false));
                logs.Add(new LogInfo(LogState.Info, $"End of section [{section.SectionName}]", -1));
            }
            return logs.ToArray();
        }

        public LogInfo[] AddVariables(VarsType type, PluginSection section, int depth)
        {
            List<LogInfo> logs = new List<LogInfo>();
            StringDictionary vars = GetVarsMatchesType(type);
            if ((section.Get() as StringDictionary).Count != 0)
            {
                logs.Add(new LogInfo(LogState.Info, $"Processing section [{section.SectionName}]", depth));
                logs.AddRange(InternalAddDictionary(vars, section.Get() as StringDictionary, depth + 1, false));
                logs.Add(new LogInfo(LogState.Info, $"End of section [{section.SectionName}]", depth));
            }
            return logs.ToArray();
        }

        public LogInfo[] AddVariables(VarsType type, PluginSection section, bool errorOff)
        {
            List<LogInfo> logs = new List<LogInfo>();
            StringDictionary vars = GetVarsMatchesType(type);
            if ((section.Get() as StringDictionary).Count != 0)
            {
                logs.Add(new LogInfo(LogState.Info, $"Processing section [{section.SectionName}]", -1));
                logs.AddRange(InternalAddDictionary(vars, section.Get() as StringDictionary, 0, errorOff));
                logs.Add(new LogInfo(LogState.Info, $"End of section [{section.SectionName}]", -1));
            }
            return logs.ToArray();
        }

        public LogInfo[] AddVariables(VarsType type, PluginSection section, int depth, bool errorOff)
        {
            List<LogInfo> logs = new List<LogInfo>();
            StringDictionary vars = GetVarsMatchesType(type);
            if ((section.Get() as StringDictionary).Count != 0)
            {
                logs.Add(new LogInfo(LogState.Info, $"Processing section [{section.SectionName}]", depth));
                logs.AddRange(InternalAddDictionary(vars, section.Get() as StringDictionary, depth + 1, errorOff));
                logs.Add(new LogInfo(LogState.Info, $"End of section [{section.SectionName}]", depth));
            }
            return logs.ToArray();
        }

        public LogInfo[] AddVariables(VarsType type, string[] lines)
        {
            StringDictionary vars = GetVarsMatchesType(type);
            StringDictionary dict = IniFile.ParseLinesVarStyle(lines);
            return InternalAddDictionary(vars, dict, -1, false);
        }

        public LogInfo[] AddVariables(VarsType type, string[] lines, int depth)
        {
            StringDictionary vars = GetVarsMatchesType(type);
            StringDictionary dict = IniFile.ParseLinesVarStyle(lines);
            return InternalAddDictionary(vars, dict, depth, false);
        }

        public LogInfo[] AddVariables(VarsType type, string[] lines, bool errorOff)
        {
            StringDictionary vars = GetVarsMatchesType(type);
            StringDictionary dict = IniFile.ParseLinesVarStyle(lines);
            return InternalAddDictionary(vars, dict, -1, errorOff);
        }

        public LogInfo[] AddVariables(VarsType type, string[] lines, int depth, bool errorOff)
        {
            StringDictionary vars = GetVarsMatchesType(type);
            StringDictionary dict = IniFile.ParseLinesVarStyle(lines);
            return InternalAddDictionary(vars, dict, depth, errorOff);
        }

        public LogInfo[] AddVariables(VarsType type, StringDictionary dict)
        {
            StringDictionary vars = GetVarsMatchesType(type);
            return InternalAddDictionary(vars, dict, -1, false);
        }

        public LogInfo[] AddVariables(VarsType type, StringDictionary dict, int depth)
        {
            StringDictionary vars = GetVarsMatchesType(type);
            return InternalAddDictionary(vars, dict, depth, false);
        }

        public LogInfo[] AddVariables(VarsType type, StringDictionary dict, bool errorOff)
        {
            StringDictionary vars = GetVarsMatchesType(type);
            return InternalAddDictionary(vars, dict, -1, errorOff);
        }

        public LogInfo[] AddVariables(VarsType type, StringDictionary dict, int depth, bool errorOff)
        {
            StringDictionary vars = GetVarsMatchesType(type);
            return InternalAddDictionary(vars, dict, depth, errorOff);
        }

        /// <summary>
        /// Add local variables
        /// </summary>
        /// <param name="vars"></param>
        /// <param name="dict"></param>
        /// <param name="sectionDepth"></param>
        /// <param name="errorOff"></param>
        /// <returns>Return true if success</returns>
        private LogInfo[] InternalAddDictionary(StringDictionary vars, StringDictionary dict, int sectionDepth, bool errorOff)
        {
            List<LogInfo> logs = new List<LogInfo>();
            foreach (var kv in dict)
            {
                if (kv.Value.IndexOf("%" + kv.Key + "%", StringComparison.OrdinalIgnoreCase) == -1)
                { // Ex) %TargetImage%=%TargetImage%
                    vars[kv.Key] = kv.Value;
                    logs.Add(new LogInfo(LogState.Success, $"Var [%{kv.Key}%] set to [{kv.Value}]", sectionDepth, errorOff));
                }
                else
                {
                    logs.Add(new LogInfo(LogState.Error, $"Var [%{kv.Key}%] contains itself in [{kv.Value}]", sectionDepth, errorOff));
                }
            }
            return logs.ToArray();
        }

        public void ResetVariables(VarsType type)
        {
            switch (type)
            {
                case VarsType.Local:
                    localVars = new StringDictionary();
                    break;
                case VarsType.Global:
                    globalVars = new StringDictionary();
                    break;
            }
        }

        public static string TrimPercentMark(string varName)
        {
            if (!(varName.StartsWith("%", StringComparison.OrdinalIgnoreCase) && varName.EndsWith("%", StringComparison.OrdinalIgnoreCase)))
                throw new VariableInvalidFormatException($"[{varName}] is not enclosed with %");
            varName = varName.Substring(1, varName.Length - 2);
            if (varName.Contains('%'))
                throw new VariableInvalidFormatException($"% cannot be placed in the middle of [{varName}]");
            return varName;
        }
    }
}
