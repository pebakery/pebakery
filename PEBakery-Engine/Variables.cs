using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BakeryEngine
{
    using VariablesDictionary = Dictionary<string, string>;
    public class BakeryVariables
    {
        /*
         * Variables 우선순위
         * local variables > global variables
         */
        private VariablesDictionary globalVars;
        public VariablesDictionary GlobalVars
        {
            get { return globalVars ; }
        }
        private VariablesDictionary localVars;
        public VariablesDictionary LocalVars
        {
            get { return localVars; }
        }

        public BakeryVariables()
        {
            localVars = new VariablesDictionary(StringComparer.OrdinalIgnoreCase);
            globalVars = new VariablesDictionary(StringComparer.OrdinalIgnoreCase);
        }

        public void LocalAdd(string key, string rawValue)
        {
            localVars.Add(key, rawValue);
        }

        public void GlobalAdd(string key, string rawValue)
        {
            globalVars.Add(key, rawValue);
        }

        public void SetValue(string key, string rawValue)
        {
            this.LocalSetValue(key, rawValue);
        }

        public void LocalSetValue(string key, string rawValue)
        {
            localVars[key] = rawValue;
        }

        public void GlobalSetValue(string key, string rawValue)
        {
            globalVars[key] = rawValue;
        }

        public string GetValue(string key)
        {
            string value;
            bool result = this.TryGetValue(key, out value);
            if (result == false)
                value = string.Empty;
            return value;
        }

        public string LocalGetValue(string key)
        {
            string value;
            bool result = localVars.TryGetValue(key, out value);
            if (result)
                value = Expand(value);
            else
                value = string.Empty;
            return value;
        }

        public string GlobalGetValue(string key)
        {
            string value;
            bool result = globalVars.TryGetValue(key, out value);
            if (result)
                value = Expand(value);
            else
                value = string.Empty; if (result == false)
                value = string.Empty;
            return value;
        }

        public bool ContainsKey(string key)
        {
            return localVars.ContainsKey(key) || globalVars.ContainsKey(key);
        }

        public bool LocalContainsKey(string key)
        {
            return localVars.ContainsKey(key);
        }

        public bool GlobalContainsKey(string key)
        {
            return globalVars.ContainsKey(key);
        }

        public bool ContainsValue(string key)
        {
            return localVars.ContainsValue(key) || globalVars.ContainsValue(key);
        }

        public bool LocalContainsValue(string key)
        {
            return localVars.ContainsValue(key);
        }

        public bool GlobalContainsValue(string key)
        {
            return globalVars.ContainsValue(key);
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
                MatchCollection matches = Regex.Matches(str, @"%(.+)%");
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
    }

    public class BakeryVariables2
    {
        /*
         * Variables 우선순위
         * local variables > global variables
         */
        private VariablesDictionary globalRaw;
        public VariablesDictionary GlobalRaw
        {
            get { return globalRaw; }
        }
        private VariablesDictionary globalValue;
        public VariablesDictionary GlobalValue
        {
            get { return globalValue; }
        }
        private VariablesDictionary localRaw;
        public VariablesDictionary LocalRaw
        {
            get { return localRaw; }
        }
        private VariablesDictionary localValue;
        public VariablesDictionary LocalValue
        {
            get { return localValue; }
        }

        public BakeryVariables2()
        {
            localRaw = new VariablesDictionary(StringComparer.OrdinalIgnoreCase);
            localValue = new VariablesDictionary(StringComparer.OrdinalIgnoreCase);
            globalRaw = new VariablesDictionary(StringComparer.OrdinalIgnoreCase);
            globalValue = new VariablesDictionary(StringComparer.OrdinalIgnoreCase);
        }

        public void LocalAdd(string key, string rawValue)
        {
            localRaw.Add(key, rawValue);
            localValue.Add(key, Expand(rawValue));
        }

        public void GlobalAdd(string key, string rawValue)
        {
            globalRaw.Add(key, rawValue);
            globalValue.Add(key, Expand(rawValue));
        }

        public void SetValue(string key, string rawValue)
        {
            this.LocalSetValue(key, rawValue);
        }

        public void LocalSetValue(string key, string rawValue)
        {
            localRaw[key] = rawValue;
            localValue[key] = Expand(rawValue);
        }

        public void GlobalSetValue(string key, string rawValue)
        {
            globalRaw[key] = rawValue;
            globalValue[key] = Expand(rawValue);
        }

        public string GetValue(string key)
        {
            string value;
            bool result = this.TryGetValue(key, out value);
            if (result == false)
                value = string.Empty;
            return value;
        }

        public string LocalGetValue(string key)
        {
            string value;
            bool result = localValue.TryGetValue(key, out value);
            if (result == false)
                value = string.Empty;
            return value;
        }

        public string GlobalGetValue(string key)
        {
            string value;
            bool result = globalValue.TryGetValue(key, out value);
            if (result == false)
                value = string.Empty;
            return value;
        }

        public bool ContainsKey(string key)
        {
            return localValue.ContainsKey(key) || globalValue.ContainsKey(key);
        }

        public bool LocalContainsKey(string key)
        {
            return localValue.ContainsKey(key);
        }

        public bool GlobalContainsKey(string key)
        {
            return globalValue.ContainsKey(key);
        }

        public bool ContainsValue(string key)
        {
            return localValue.ContainsValue(key) || globalValue.ContainsValue(key);
        }

        public bool LocalContainsValue(string key)
        {
            return localValue.ContainsValue(key);
        }

        public bool GlobalContainsValue(string key)
        {
            return globalValue.ContainsValue(key);
        }

        public bool Remove(string key)
        {
            if (localValue.ContainsKey(key))
                return localValue.Remove(key);
            else if (globalValue.ContainsKey(key))
                return globalValue.Remove(key);
            return false;
        }

        public override string ToString()
        {
            string str = "\n[Local Variables]\n";
            foreach (var local in localValue)
                str = string.Concat(str, "[", local.Key, ", ", localRaw[local.Key], ", ", local.Value, "]\n");
            str += "[Global Variables]\n";
            foreach (var global in globalValue)
                str = string.Concat(str, "[", global.Key, ", ", globalRaw[global.Key], ", ", global.Value, "]\n");
            return str;
        }

        public bool TryGetValue(string key, out string value)
        {
            bool globalResult = globalValue.TryGetValue(key, out value);
            bool localResult = localValue.TryGetValue(key, out value);
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
                MatchCollection matches = Regex.Matches(str, @"%(.+)%");
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
                    if (globalValue.ContainsKey(varName))
                        dest = string.Concat(dest, globalValue[varName]);
                    else if (localValue.ContainsKey(varName))
                        dest = string.Concat(dest, localValue[varName]);

                    if (x + 1 == matches.Count) // Last iteration
                        dest = string.Concat(dest, str.Substring(matches[x].Index + matches[x].Value.Length));
                }
                if (0 < matches.Count) // Only copy it if variable exists
                    str = dest;
            }

            return str;
        }

        public void Rebuild()
        {
            foreach (var local in localRaw)
            {
                localValue[local.Key] = Expand(local.Value);
            }

            foreach (var global in globalRaw)
            {
                globalValue[global.Key] = Expand(global.Value);
            }
        }
    }
}
