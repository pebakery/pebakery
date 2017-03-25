using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace BakeryEngine_Legacy
{
    using PluginDictionary = ConcurrentDictionary<int, Plugin[]>;

    /// <summary>
    /// Struct to address PluginDictionary
    /// </summary>
    public struct PluginAddress
    { // Return address format = <Section>'s <n'th line>
        public int level;
        public int index;
        public PluginAddress(int level, int index)
        {
            this.level = level;
            this.index = index;
        }
    }

    public class PluginNotFoundException : Exception
    {
        public PluginNotFoundException() { }
        public PluginNotFoundException(string message) : base(message) { }
        public PluginNotFoundException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Reached end of plugin levels
    /// </summary>
    public class EndOfPluginLevelException : Exception
    {
        public EndOfPluginLevelException() { }
        public EndOfPluginLevelException(string message) : base(message) { }
        public EndOfPluginLevelException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Collection class to hold plugins and metadata.
    /// </summary>
    /// <remarks>
    /// TODO: More appropriate name?
    /// </remarks>
    public class PluginCollection
    {
        private PluginDictionary dict; // Plugin instances
        private int[] levels; // plugin's level array
        private int count; // how many plugins are holded in this class?

        public PluginDictionary Dict { get { return dict; } }
        public int[] Levels { get { return levels; } }
        public int Count { get { return count; } }
        public Plugin LastPlugin
        {
            get
            {
                return GetPlugin(GetLastPluginAddress());
            }
        }
        
        public PluginCollection(PluginDictionary dict, int[] levels, int count)
        {
            this.dict = dict;
            this.levels = levels;
            this.count = count;
        }

        public Plugin SearchByShortPath(string shortPath)
        {
            foreach (int level in dict.Keys)
            {
                for (int i = 0; i < dict[level].Length; i++)
                {
                    if (string.Equals(shortPath, dict[level][i].ShortPath, StringComparison.OrdinalIgnoreCase))
                        return dict[level][i];
                }
            }
            // not found
            throw new PluginNotFoundException($"Plugin [{shortPath}] not found");
        }

        public Plugin SearchByFullPath(string rawFullPath)
        {
            string fullPath = Path.GetFullPath(rawFullPath);
            foreach (int level in dict.Keys)
            {
                for (int i = 0; i < dict[level].Length; i++)
                {
                    if (string.Equals(fullPath, dict[level][i].FullPath, StringComparison.OrdinalIgnoreCase))
                        return dict[level][i];
                }
            }
            // not found
            throw new PluginNotFoundException($"Plugin [{rawFullPath}] not found");
        }

        public Plugin GetNextPlugin(Plugin plugin)
        {
            return GetPlugin(InternalGetNextAddress(GetAddress(plugin)));
        }

        public Plugin GetNextPlugin(PluginAddress addr)
        {
            return GetPlugin(InternalGetNextAddress(addr));
        }

        public Plugin GetPlugin(PluginAddress addr)
        {
            return dict[addr.level][addr.index];
        }

        public PluginAddress GetAddress(Plugin plugin)
        {
            int level = 0;
            int index = 0;
            bool found = false;

            for (int i = 0; i < levels.Length; i++)
            {
                level = levels[i];
                if (level == 0) // Level 0 is usually script
                    continue;
                index = Array.IndexOf<Plugin>(dict[level], plugin);
                if (index != -1) // found!
                {
                    found = true;
                    break;
                }
            }

            if (found)
                return new PluginAddress(level, index);
            else
                throw new PluginNotFoundException();
        }

        public PluginAddress GetNextAddress(PluginAddress plugin)
        {
            return InternalGetNextAddress(plugin);
        }

        public PluginAddress GetNextAddress(Plugin plugin)
        {
            return InternalGetNextAddress(GetAddress(plugin));
        }

        private PluginAddress InternalGetNextAddress(PluginAddress addr)
        {
            if (addr.index < dict[addr.level].Length - 1)
                addr.index++;
            else
            {
                // Increment level value
                int idx = Array.IndexOf<int>(levels, addr.level); // if fail, return -1
                if (levels.Length <= idx + 1) // end of level
                    throw new EndOfPluginLevelException();
                addr.level = levels[idx + 1];
                addr.index = 0;
            }

            return addr;
        }

        public PluginAddress GetLastPluginAddress()
        {
            PluginAddress addr = new PluginAddress();
            addr.level = levels[levels.Length - 1];
            addr.index = dict[addr.level].Length - 1;
            return addr;
        }

        public Plugin GetLastPlugin()
        {
            return GetPlugin(GetLastPluginAddress());
        }

        public int GetFullIndex(PluginAddress addr)
        {
            int fullIndex = addr.index + 1; // Human-readable index starts from 1
            for (int i = 0; i < levels.Length; i++)
            {
                if (levels[i] < addr.level)
                    fullIndex += dict[levels[i]].Length;
                else
                    break;
            }
            return fullIndex;
        }
    }
}
