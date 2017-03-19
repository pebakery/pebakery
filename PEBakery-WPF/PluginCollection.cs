using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using PEBakery.Lib;

namespace PEBakery.WPF
{
    // using PluginDictionary = ConcurrentDictionary<PluginDictKey, Plugin[]>;


    /// <summary>
    /// Collection class to hold plugins and metadata.
    /// </summary>
    /// <remarks>
    /// TODO: More appropriate name?
    /// </remarks>
    public class PluginCollection
    {
        

    }


        

    /*
    public struct PluginDictKey
    {
        public int level;
        public string directory;
        public PluginDictKey(int level, string directory)
        {
            this.level = level;
            this.directory = directory;
        }

        public readonly static PluginDictKey Empty = new PluginDictKey(0, string.Empty);
        public override bool Equals(Object obj)
        {
            if (obj is PluginDictKey)
            {
                PluginDictKey key = (PluginDictKey)obj;
                return (key.level == this.level) && (key.directory == this.directory);
            }
            else
                return false;
        }

        public override string ToString()
        {
            return $"({level}, {directory})";
        }

        public override int GetHashCode()
        {
            return level.GetHashCode() ^ directory.GetHashCode();
        }

        public static bool operator ==(PluginDictKey x, PluginDictKey y)
        {
            return (x.level == y.level) && (x.directory == y.directory);
        }

        public static bool operator !=(PluginDictKey x, PluginDictKey y)
        {
            return !((x.level == y.level) && (x.directory == y.directory));
        }

        public static bool operator <(PluginDictKey x, PluginDictKey y)
        {
            if (x.level == y.level)
            {
                int result = StringComparer.OrdinalIgnoreCase.Compare(x.directory, y.directory);
                if (0 < result) 
                    return true;
                else
                    return false;
            }
            else
                return (x.level < y.level);
        }

        public static bool operator >(PluginDictKey x, PluginDictKey y)
        {
            if (x.level == y.level)
            {
                int result = StringComparer.OrdinalIgnoreCase.Compare(x.directory, y.directory);
                if (result < 0)
                    return true;
                else
                    return false;
            }
            else
                return (x.level > y.level);
        }

        public static bool operator <=(PluginDictKey x, PluginDictKey y)
        {
            if (x.level == y.level)
            {
                int result = StringComparer.OrdinalIgnoreCase.Compare(x.directory, y.directory);
                if (0 <= result)
                    return true;
                else
                    return false;
            }
            else
                return (x.level < y.level);
        }

        public static bool operator >=(PluginDictKey x, PluginDictKey y)
        {
            if (x.level == y.level)
            {
                int result = StringComparer.OrdinalIgnoreCase.Compare(x.directory, y.directory);
                if (result <= 0)
                    return true;
                else
                    return false;
            }
            else
                return (x.level > y.level);
        }
    }
    */

    /*
    /// <summary>
    /// Struct to address PluginDictionary
    /// </summary>
    public struct PluginAddress
    {
        // public PluginDictKey key;
        public PluginDictKey key;
        public int index;
        public PluginAddress(PluginDictKey key, int index)
        {
            this.key = key;
            this.index = index;
        }
    }
    */


}
