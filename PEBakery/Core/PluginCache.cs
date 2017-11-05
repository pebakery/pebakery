using PEBakery.Helper;
using SQLite;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace PEBakery.Core
{
    #region PluginCache
    public class PluginCache : SQLiteConnection
    {
        public static int dbLock = 0;
        private ReaderWriterLockSlim listLock;

        public PluginCache(string path) : base(path)
        {
            dbLock = 0;
            CreateTable<DB_CacheInfo>();
            CreateTable<DB_PluginCache>();
        }

        public async void WaitClose()
        {
            while (true)
            {
                if (dbLock == 0)
                {
                    base.Close();
                    return;
                }
                await Task.Delay(200);
            }
        }

        #region Cache Plugin or Plugins
        public void CachePlugins(ProjectCollection projects, string baseDir, BackgroundWorker worker)
        {
            try
            {
                DateTime buildDate = new FileInfo(Assembly.GetExecutingAssembly().Location).LastWriteTimeUtc;
                DB_CacheInfo[] infos = new DB_CacheInfo[]
                {
                    new DB_CacheInfo() { Key = "EngineVersion", Value = Properties.Resources.EngineVersion },
                    new DB_CacheInfo() { Key = "BuildDate", Value = Properties.Resources.BuildDate },
                    new DB_CacheInfo() { Key = "BaseDir", Value = baseDir },
                };
                InsertOrReplaceAll(infos);

                foreach (Project project in projects.Projects)
                {
                    // Remove duplicate
                    var pUniqueList = project.AllPlugins
                        .GroupBy(x => x.DirectFullPath)
                        .Select(x => x.First());

                    listLock = new ReaderWriterLockSlim();

                    List<DB_PluginCache> inMemDB = Table<DB_PluginCache>().ToList();
                    var tasks = pUniqueList.Select(p =>
                    {
                        return Task.Run(() =>
                        {
                            bool updated = CachePlugin(p, inMemDB);
                            worker.ReportProgress(updated ? 1 : 0); // 1 - updated, 0 - not updated
                        });
                    }).ToArray();
                    Task.WaitAll(tasks);
                    
                    InsertOrReplaceAll(inMemDB);
                }
            }
            catch (SQLiteException e)
            { // Update failure
                string msg = $"SQLite Error : {e.Message}\r\nCache database is corrupted. Please delete PEBakeryCache.db and restart.";
                MessageBox.Show(msg, "SQLite Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown(1);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="p"></param>
        /// <param name="inMemDB">Use NULL to put direct into .db file</param>
        /// <returns>Return true if cache is updated</returns>
        private bool CachePlugin(Plugin p, List<DB_PluginCache> inMemDB)
        {
            // Does cache exist?
            FileInfo f = new FileInfo(p.DirectFullPath);
            string sPath = p.DirectFullPath.Remove(0, p.Project.BaseDir.Length + 1);

            bool updated = false;
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
                    LastWriteTimeUtc = f.LastWriteTimeUtc,
                    FileSize = f.Length,
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
                    updated = true;
                }
                else
                {
                    listLock.EnterWriteLock();
                    try
                    {
                        inMemDB.Add(pCache);
                        updated = true;
                    }
                    finally
                    {
                        listLock.ExitWriteLock();
                    }
                }
            }
            else if (pCache.Path.Equals(sPath, StringComparison.Ordinal) && 
                (DateTime.Equals(pCache.LastWriteTimeUtc, f.LastWriteTime) == false ||
                pCache.FileSize != f.Length)) // Cache is outdated
            {
                BinaryFormatter formatter = new BinaryFormatter();
                using (MemoryStream mem = new MemoryStream())
                {
                    formatter.Serialize(mem, p);
                    pCache.Serialized = mem.ToArray();
                    pCache.LastWriteTimeUtc = f.LastWriteTimeUtc;
                    pCache.FileSize = f.Length;
                }

                if (inMemDB == null)
                {
                    Update(pCache);
                    updated = true;
                }
                else
                {
                    listLock.EnterWriteLock();
                    try
                    {
                        inMemDB[inMemIdx] = pCache;
                        updated = true;
                    }
                    finally
                    {
                        listLock.ExitWriteLock();
                    }
                }
            }

            if (p.Type == PluginType.Link && p.LinkLoaded)
            {
                bool linkUpdated = CachePlugin(p.Link, inMemDB);
                updated = updated || linkUpdated;
            }

            return updated;
        }
        #endregion
        
        #region IsGlobalCacheValid
        public bool IsGlobalCacheValid(string baseDir)
        {
            Dictionary<string, string> infoDict = Table<DB_CacheInfo>().ToDictionary(x => x.Key, x => x.Value);

            // Does key exist?
            if (infoDict.ContainsKey("EngineVersion") == false)
                return false;
            if (infoDict.ContainsKey("BuildDate") == false)
                return false;
            if (infoDict.ContainsKey("BaseDir") == false)
                return false;

            // Does value match?
            if (infoDict["EngineVersion"].Equals(Properties.Resources.EngineVersion, StringComparison.Ordinal) == false)
                return false;
            if (infoDict["BuildDate"].Equals(Properties.Resources.BuildDate, StringComparison.Ordinal) == false)
                return false;
            if (infoDict["BaseDir"].Equals(baseDir, StringComparison.Ordinal) == false)
                return false;

            return true;
        }
        #endregion

        #region InsertOrReplaceAll
        public int InsertOrReplaceAll(System.Collections.IEnumerable objects)
        {
            var c = 0;
            RunInTransaction(() =>
            {
                foreach (var r in objects)
                {
                    c += InsertOrReplace(r);
                }
            });
            return c;
        }
        #endregion
    }
    #endregion

    #region Model
    public class DB_CacheInfo
    {
        [PrimaryKey]
        public string Key { get; set; }
        public string Value { get; set; }
        
        public override string ToString()
        {
            return $"Key [{Key}] = {Value}";
        }
    }

    public class DB_PluginCache
    {
        [PrimaryKey]
        public int Hash { get; set; }
        [MaxLength(32768)]
        public string Path { get; set; } // Without BaseDir
        public DateTime LastWriteTimeUtc { get; set; }
        public long FileSize { get; set; }
        public byte[] Serialized { get; set; }

        public override string ToString()
        {
            return $"{Hash} = [{Path}] Cache";
        }
    }
    #endregion
}
