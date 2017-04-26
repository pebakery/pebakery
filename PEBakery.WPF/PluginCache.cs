using SQLite.Net;
using SQLite.Net.Attributes;
using SQLite.Net.Platform.Win32;
using SQLiteNetExtensions.Attributes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PEBakery.Core
{
    #region PluginCache
    public class PluginCache : SQLiteConnection
    {
        private ReaderWriterLockSlim listLock;

        public PluginCache(string path) : base(new SQLitePlatformWin32(), path)
        {
            CreateTable<DB_ExecutableInfo>();
            CreateTable<DB_PluginCache>();
        }

        public void CachePlugins(ProjectCollection projects, BackgroundWorker worker)
        {
            foreach (Project project in projects.Projects)
            {
                // Remove duplicate
                var pUniqueList = project.AllPluginList
                    .GroupBy(x => x.DirectFullPath)
                    .Select(x => x.First());

                listLock = new ReaderWriterLockSlim();
                List<DB_PluginCache> inMemDB = Table<DB_PluginCache>().ToList();
                var tasks = pUniqueList.Select(p =>
                {
                    return Task.Run(() =>
                    {
                        CachePlugin(p, inMemDB);

                        worker.ReportProgress(0);
                    });
                }).ToArray();
                Task.WaitAll(tasks);

                InsertOrReplaceAll(inMemDB);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="p"></param>
        /// <param name="inMemDB">Use NULL to put direct into .db file</param>
        public void CachePlugin(Plugin p, List<DB_PluginCache> inMemDB)
        {
            // Is cache exist?
            DateTime lastWriteTime = File.GetLastWriteTimeUtc(p.DirectFullPath);
            string sPath = p.DirectFullPath.Remove(0, p.Project.BaseDir.Length + 1);

            DB_PluginCache pCache = null;
            int inMemIdx = 0;
            if (inMemDB == null)
            {
                pCache = Table<DB_PluginCache>().FirstOrDefault(x => x.Hash == sPath.GetHashCode());
            }
            else
            {
                listLock.EnterReadLock();
                try
                {
                    inMemIdx = inMemDB.FindIndex(x => x.Hash == sPath.GetHashCode());
                    if (inMemIdx == -1)
                        pCache = null;
                    else
                        pCache = inMemDB[inMemIdx];
                }
                catch
                {
                    pCache = null;
                }
                finally
                {
                    listLock.ExitReadLock();
                }
                
            }

            if (pCache == null) // Cache not exists
            {
                pCache = new DB_PluginCache()
                {
                    Hash = sPath.GetHashCode(),
                    Path = sPath,
                    LastWriteTime = lastWriteTime,
                };

                BinaryFormatter formatter = new BinaryFormatter();
                using (MemoryStream mem = new MemoryStream())
                {
                    formatter.Serialize(mem, p);
                    pCache.Serialized = mem.ToArray();
                }


                if (inMemDB == null)
                {
                    Insert(pCache);
                }
                else
                {
                    listLock.EnterWriteLock();
                    try
                    {
                        inMemDB.Add(pCache);
                    }
                    finally
                    {
                        listLock.ExitWriteLock();
                    }
                }
            }
            else if (pCache.Path.Equals(sPath, StringComparison.Ordinal) && 
                DateTime.Equals(pCache.LastWriteTime, lastWriteTime) == false) // Cache is outdated
            {
                BinaryFormatter formatter = new BinaryFormatter();
                using (MemoryStream mem = new MemoryStream())
                {
                    formatter.Serialize(mem, p);
                    pCache.Serialized = mem.ToArray();
                    pCache.LastWriteTime = lastWriteTime;
                }

                if (inMemDB == null)
                {
                    Update(pCache);
                }
                else
                {
                    listLock.EnterWriteLock();
                    try
                    {
                        inMemDB[inMemIdx] = pCache;
                    }
                    finally
                    {
                        listLock.ExitWriteLock();
                    }
                }
            }

            if (p.Type == PluginType.Link && p.LinkLoaded)
                CachePlugin(p.Link, inMemDB);
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
        [PrimaryKey]
        public int Hash { get; set; }
        [MaxLength(32768)]
        public string Path { get; set; } // Without BaseDir
        public DateTime LastWriteTime { get; set; }
        public byte[] Serialized { get; set; }

        public override string ToString()
        {
            return $"{Hash} = [{Path}] Cache";
        }
    }
    #endregion
}
