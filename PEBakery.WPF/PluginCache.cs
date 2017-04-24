using SQLite.Net;
using SQLite.Net.Attributes;
using SQLite.Net.Platform.Win32;
using SQLiteNetExtensions.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PEBakery.Core
{
    #region PluginCache
    public class PluginCache : SQLiteConnection
    {
        public PluginCache(string path) : base(new SQLitePlatformWin32(), path)
        {
            CreateTable<DB_ExecutableInfo>();
            CreateTable<DB_PluginCache>();
        }

        
    }

    #endregion

    #region Model
    public class DB_ExecutableInfo
    {
        [PrimaryKey] // Will have only one value
        public byte[] SHA256 { get; set; }

        public override string ToString()
        {
            return $"[{SHA256}] Exe Info";
        }
    }

    public class DB_PluginCache
    {
        [PrimaryKey, AutoIncrement]
        public long Id { get; set; }     
        [MaxLength(32768)]
        public string Path { get; set; } // Without BaseDir
        public DateTime LastWriteTime { get; set; }
        public byte[] Serialized { get; set; }

        public override string ToString()
        {
            return $"{Id} = [{Path}] Cache";
        }
    }
    #endregion
}
