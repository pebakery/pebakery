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
    public class BakeryVariables
    {
        /*
         * Variables 우선순위
         * local variables > global variables
         */
        private StringDictionary globalVars;
        public StringDictionary GlobalVars
        {
            get { return globalVars ; }
        }
        private StringDictionary localVars;
        public StringDictionary LocalVars
        {
            get { return localVars; }
        }

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
            StringDictionary vars = GetVarsMatchesType(type);
            vars.Add(key, rawValue);
        }

        public void SetValue(VarsType type, string key, string rawValue)
        {
            StringDictionary vars = GetVarsMatchesType(type);
            vars[key] = rawValue;
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
            string str = "\n[Local Variables]\n";
            foreach (var local in localVars)
                str = string.Concat(str, "[", local.Key, ", ", local.Value, ", ", Expand(local.Value), "]\n");
            str += "[Global Variables]\n";
            foreach (var global in globalVars)
                str = string.Concat(str, "[", global.Key, ", ", global.Value, ", ", Expand(global.Value), "]\n");
            return str;
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
                MatchCollection matches = Regex.Matches(str, @"%(.+)%", RegexOptions.Compiled);
                string dest = string.Empty;
                for (int x = 0; x < matches.Count; x++)
                {
                    string varName = matches[x].Groups[1].ToString();
                    int startOffset;
                    if (x == 0)
                        startOffset = 0;
                    else
                        startOffset = matches[x - 1].Index;

                    dest = string.Concat(dest, str.Substring(startOffset, matches[x].Index));
                    if (globalVars.ContainsKey(varName))
                        dest = string.Concat(dest, globalVars[varName]);
                    else if (localVars.ContainsKey(varName))
                        dest = string.Concat(dest, localVars[varName]);

                    if (x + 1 == matches.Count) // Last iteration
                        dest = string.Concat(dest, str.Substring(matches[x].Index + matches[x].Value.Length));
                }
                if (0 < matches.Count) // Only copy it if variable exists
                    str = dest;
            }

            return str;
        }

        /// <summary>
        /// Add local variables
        /// </summary>
        /// <param name="section"></param>
        public void AddVariables(VarsType type, PluginSection section)
        {
            StringDictionary vars = GetVarsMatchesType(type);
            StringDictionary dict = IniFile.ParseLinesVarStyle(section.Lines);
            InternalAddDictionary(vars, dict);
        }

        /// <summary>
        /// Add local variables
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
            foreach (var d in dict)
                vars[d.Key] = d.Value;
        }
    }
}
