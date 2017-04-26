using SQLite.Net;
using SQLite.Net.Attributes;
using SQLite.Net.Platform.Win32;
using SQLiteNetExtensions.Attributes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
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

        public void CachePlugin(Plugin p)
        {
            // Is cache exist?
            DateTime lastWriteTime = File.GetLastWriteTimeUtc(p.DirectFullPath);
            string sPath = p.DirectFullPath.Remove(0, p.Project.BaseDir.Length + 1);
            DB_PluginCache pCache = Table<DB_PluginCache>()
                .FirstOrDefault(x => x.Path.Equals(sPath, StringComparison.Ordinal));
            if (pCache == null) // Cache not exists
            {
                pCache = new DB_PluginCache()
                {
                    Path = sPath,
                    LastWriteTime = lastWriteTime,
                };

                BinaryFormatter formatter = new BinaryFormatter();
                using (MemoryStream mem = new MemoryStream())
                {
                    formatter.Serialize(mem, p);
                    pCache.Serialized = mem.ToArray();
                }

                Insert(pCache);
            }
            else if (DateTime.Equals(pCache.LastWriteTime, lastWriteTime) == false) // Cache is outdated
            {
                BinaryFormatter formatter = new BinaryFormatter();
                using (MemoryStream mem = new MemoryStream())
                {
                    formatter.Serialize(mem, p);
                    pCache.Serialized = mem.ToArray();
                    pCache.LastWriteTime = lastWriteTime;
                }

                Update(pCache);
            }

            if (p.Type == PluginType.Link && p.LinkLoaded)
                CachePlugin(p.Link);
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
        [Indexed]
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
