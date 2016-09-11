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

        // Properties
        public StringDictionary GlobalVars { get { return globalVars; } }
        public StringDictionary LocalVars { get { return localVars; } }


        /// <summary>
        /// Constructor
        /// </summary>
        public BakeryVariables()
        {
            localVars = new StringDictionary(StringComparer.OrdinalIgnoreCase);
            globalVars = new StringDictionary(StringComparer.OrdinalIgnoreCase);
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

        public void Add(VarsType type, string key, string rawValue)
        {
            SetValue(type, key, rawValue);
        }

        public void SetValue(VarsType type, string key, string rawValue)
        {
            StringDictionary vars = GetVarsMatchesType(type);
            // Check and remove circular reference
            if (rawValue.IndexOf("%" + key + "%", StringComparison.OrdinalIgnoreCase) == -1)
            { // Ex) %Joveler%=Variel\ied206.txt
                vars[key] = rawValue; 
            }
            else
            { // Ex) %Joveler%=Variel\%Joveler%\ied206.txt
                throw new VariableCircularReferenceException($"Var [%{key}%] contains itself in [{rawValue}]");
            }
        }

        public void Add(VarsType type, string key, string rawValue, Logger logger, int sectionDepth)
        {
            SetValue(type, key, rawValue, logger, sectionDepth);
        }

        public void SetValue(VarsType type, string key, string rawValue, Logger logger, int sectionDepth)
        {
            StringDictionary vars = GetVarsMatchesType(type);
            // Check and remove circular reference
            if (rawValue.IndexOf("%" + key + "%", StringComparison.OrdinalIgnoreCase) == -1)
            { // Ex) %Joveler%=Variel\ied206.txt
                vars[key] = rawValue;
                logger.Write(LogState.Success, $"Var [%{key}%] set to [{rawValue}]", sectionDepth, true);
            }
            else
            { // Ex) %Joveler%=Variel\%Joveler%\ied206.txt
                logger.Write(LogState.Error, $"Var [%{key}%] contains itself in [{rawValue}]", sectionDepth, true);
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
        public void AddVariables(VarsType type, PluginSection section, Logger logger, int sectionDepth)
        {
            if ((section.Get() as StringDictionary).Count != 0)
            {
                logger.Write(LogState.Info, $"Processing section [{section.SectionName}]", sectionDepth);
                StringDictionary vars = GetVarsMatchesType(type);
                InternalAddDictionary(vars, section.Get() as StringDictionary, logger, sectionDepth + 1);
                logger.Write(LogState.Info, $"End of section [{section.SectionName}]", sectionDepth);
            }
        }

        /// <summary>
        /// Add variables
        /// </summary>
        /// <param name="section"></param>
        public void AddVariables(VarsType type, PluginSection section)
        {
            StringDictionary vars = GetVarsMatchesType(type);
            InternalAddDictionary(vars, section.Get() as StringDictionary);
        }

        /// <summary>
        /// Add variables
        /// </summary>
        /// <param name="lines"></param>
        public void AddVariables(VarsType type, string[] lines)
        {
            StringDictionary vars = GetVarsMatchesType(type);
            StringDictionary dict = IniFile.ParseLinesVarStyle(lines);
            InternalAddDictionary(vars, dict);
        }

        /// <summary>
        /// Add local variables
        /// </summary>
        /// <param name="dict"></param>
        public void AddVariables(VarsType type, StringDictionary dict)
        {
            StringDictionary vars = GetVarsMatchesType(type);
            InternalAddDictionary(vars, dict);
        }

        /// <summary>
        /// Add local variables
        /// </summary>
        /// <param name="vars"></param>
        /// <param name="dict"></param>
        private void InternalAddDictionary(StringDictionary vars, StringDictionary dict)
        {
            foreach (var kv in dict)
            {
                if (kv.Value.IndexOf("%" + kv.Key + "%", StringComparison.OrdinalIgnoreCase) == -1)
                    vars[kv.Key] = kv.Value;
                else
                    throw new VariableCircularReferenceException($"Var [%{kv.Key}%] contains itself in [{kv.Value}]");
            }
        }

        /// <summary>
        /// Add local variables
        /// </summary>
        /// <param name="vars"></param>
        /// <param name="dict"></param>
        private void InternalAddDictionary(StringDictionary vars, StringDictionary dict, Logger logger, int sectionDepth)
        {
            foreach (var kv in dict)
            {
                if (kv.Value.IndexOf("%" + kv.Key + "%", StringComparison.OrdinalIgnoreCase) == -1)
                { // Ex) %TargetImage%=%TargetImage%
                    vars[kv.Key] = kv.Value;
                    logger.Write(LogState.Success, $"Var [%{kv.Key}%] set to [{kv.Value}]", sectionDepth);
                }
                else
                {
                    logger.Write(LogState.Error, $"Var [%{kv.Key}%] contains itself in [{kv.Value}]", sectionDepth, true);
                }
            }
        }

        public void ResetLocalVaribles()
        {
            localVars = new StringDictionary();
        }
    }
}
