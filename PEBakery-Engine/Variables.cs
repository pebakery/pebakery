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

        public void Add(VarsType type, string key, string rawValue)
        {
            InternalSetValue(type, key, rawValue, 0, true);
        }

        public void Add(VarsType type, string key, string rawValue, int sectionDepth)
        {
            InternalSetValue(type, key, rawValue, sectionDepth, true);
        }

        public void SetValue(VarsType type, string key, string rawValue)
        {
            InternalSetValue(type, key, rawValue, 0, true);
        }

        public void SetValue(VarsType type, string key, string rawValue, int sectionDepth)
        {
            InternalSetValue(type, key, rawValue, sectionDepth, true);
        }

        public void InternalSetValue(VarsType type, string key, string rawValue, int sectionDepth, bool errorOff)
        {
            StringDictionary vars = GetVarsMatchesType(type);
            // Check and remove circular reference
            if (CheckCircularReference(key, rawValue))
            { // Ex) %Joveler%=Variel\%Joveler%\ied206.txt
                logger.Write(new LogInfo(LogState.Error, $"Var [%{key}%] contains itself in [{rawValue}]", sectionDepth, errorOff));
            }
            else
            { // Ex) %Joveler%=Variel\ied206.txt
                vars[key] = rawValue;
                logger.Write(new LogInfo(LogState.Success, $"Var [%{key}%] set to [{rawValue}]", sectionDepth, errorOff));
            }
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

        /// <summary>
        /// Add variables
        /// </summary>
        /// <param name="section"></param>
        public void AddVariables(VarsType type, PluginSection section, int sectionDepth)
        {
            if ((section.Get() as StringDictionary).Count != 0)
            {
                logger.Write(new LogInfo(LogState.Info, $"Processing section [{section.SectionName}]", sectionDepth));
                StringDictionary vars = GetVarsMatchesType(type);
                InternalAddDictionary(vars, section.Get() as StringDictionary, sectionDepth + 1, true);
                logger.Write(new LogInfo(LogState.Info, $"End of section [{section.SectionName}]", sectionDepth));
            }
        }

        /// <summary>
        /// Add variables
        /// </summary>
        /// <param name="section"></param>
        public void AddVariables(VarsType type, PluginSection section)
        {
            StringDictionary vars = GetVarsMatchesType(type);
            InternalAddDictionary(vars, section.Get() as StringDictionary, 0, true);
        }

        /// <summary>
        /// Add variables
        /// </summary>
        /// <param name="lines"></param>
        public void AddVariables(VarsType type, string[] lines)
        {
            StringDictionary vars = GetVarsMatchesType(type);
            StringDictionary dict = IniFile.ParseLinesVarStyle(lines);
            InternalAddDictionary(vars, dict, 0, true);
        }

        /// <summary>
        /// Add local variables
        /// </summary>
        /// <param name="dict"></param>
        public void AddVariables(VarsType type, StringDictionary dict)
        {
            StringDictionary vars = GetVarsMatchesType(type);
            InternalAddDictionary(vars, dict, 0, true);
        }

        /// <summary>
        /// Add local variables
        /// </summary>
        /// <param name="vars"></param>
        /// <param name="dict"></param>
        /// <param name="sectionDepth"></param>
        /// <param name="errorOff"></param>
        /// <returns>Return true if success</returns>
        private bool InternalAddDictionary(StringDictionary vars, StringDictionary dict, int sectionDepth, bool errorOff)
        {
            bool success = true;
            foreach (var kv in dict)
            {
                if (kv.Value.IndexOf("%" + kv.Key + "%", StringComparison.OrdinalIgnoreCase) == -1)
                { // Ex) %TargetImage%=%TargetImage%
                    vars[kv.Key] = kv.Value;
                    logger.Write(new LogInfo(LogState.Success, $"Var [%{kv.Key}%] set to [{kv.Value}]", sectionDepth, errorOff));
                }
                else
                {
                    success = false;
                    logger.Write(new LogInfo(LogState.Error, $"Var [%{kv.Key}%] contains itself in [{kv.Value}]", sectionDepth, errorOff));
                }
            }

            return success;
        }

        public void ResetLocalVaribles()
        {
            localVars = new StringDictionary();
        }
    }
}
