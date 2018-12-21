/*
    Copyright (C) 2016-2018 Hajin Jang
    Licensed under GPL 3.0
 
    PEBakery is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.

    Additional permission under GNU GPL version 3 section 7

    If you modify this program, or any covered work, by linking
    or combining it with external libraries, containing parts
    covered by the terms of various license, the licensors of
    this program grant you additional permission to convey the
    resulting work. An external library is a library which is
    not derived from or based on this program. 
*/

using SQLite;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using PEBakery.Helper;

namespace PEBakery.Core
{
    #region ScriptCache
    public class ScriptCache : SQLiteConnection
    {
        #region Fields
        public static int DbLock = 0;
        #endregion

        #region Constructor
        public ScriptCache(string path) : base(path, SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.FullMutex)
        {
            CreateTable<DB_CacheRevision>();
            CreateTable<DB_ScriptCache>();
        }
        #endregion

        #region WaitClose
        public async void WaitClose()
        {
            while (true)
            {
                if (DbLock == 0)
                {
                    Close();
                    return;
                }
                await Task.Delay(200);
            }
        }
        #endregion

        #region CacheScripts
        public (int CachedCount, int UpdatedCount) CacheScripts(ProjectCollection projects, string baseDir)
        {
            try
            {
                SaveCacheRevision(baseDir);

                int cachedCount = 0;
                int updatedCount = 0;
                foreach (Project project in projects.ProjectList)
                {
                    // Remove duplicate
                    Script[] uniqueScripts = project.AllScripts
                        .Where(x => x.Type != ScriptType.Directory)
                        .Distinct(new ScriptComparer())
                        .ToArray();

                    DB_ScriptCache[] cachePool = Table<DB_ScriptCache>().ToArray();
                    List<DB_ScriptCache> updatePool = new List<DB_ScriptCache>();
                    Parallel.ForEach(uniqueScripts, sc =>
                    {
                        bool updated = SerializeScript(sc, cachePool, updatePool);

                        // Q) Can this value measured by updatePool.Count?
                        // A) Even if two scripts were serialized (e.g. .link), they should be counted as one script.
                        if (updated)
                            Interlocked.Increment(ref updatedCount);
                    });

                    cachedCount += uniqueScripts.Length;
                    InsertOrReplaceAll(updatePool);
                }

                return (cachedCount, updatedCount);
            }
            catch (SQLiteException e)
            { // Update failure
                string msg = $"SQLite Error : {e.Message}\r\nCache database is corrupted. Please delete PEBakeryCache.db and restart.";
                MessageBox.Show(msg, "SQLite Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown(1);
            }

            return (0, 0);
        }
        #endregion

        #region SerializeScript, DeserializeScript
        /// <returns>Return true if cache is updated</returns>
        private bool SerializeScript(Script sc, DB_ScriptCache[] cachePool, List<DB_ScriptCache> updatePool)
        {
            if (cachePool == null)
                throw new ArgumentNullException(nameof(cachePool));
            if (updatePool == null)
                throw new ArgumentNullException(nameof(updatePool));
            Debug.Assert(sc.Type != ScriptType.Directory);

            // If script file is not found in the disk, ignore it
            if (!File.Exists(sc.DirectRealPath))
                return false;
            FileInfo f = new FileInfo(sc.DirectRealPath);

            // Retrieve Cache
            bool updated = false;
            DB_ScriptCache scCache = cachePool.FirstOrDefault(x => x.Hash == sc.DirectRealPath.GetHashCode());

            // Update Cache into updateDB
            if (scCache == null)
            { // Cache not exists
                scCache = new DB_ScriptCache
                {
                    Hash = sc.DirectRealPath.GetHashCode(),
                    RealPath = sc.DirectRealPath,
                    LastWriteTimeUtc = f.LastWriteTimeUtc,
                    FileSize = f.Length,
                };

                BinaryFormatter formatter = new BinaryFormatter();
                using (MemoryStream ms = new MemoryStream())
                {
                    formatter.Serialize(ms, sc);
                    scCache.Serialized = ms.ToArray();
                }

                lock (updatePool)
                {
                    updatePool.Add(scCache);
                    updated = true;
                }
            }
            else if (scCache.RealPath.Equals(sc.DirectRealPath, StringComparison.OrdinalIgnoreCase) &&
                     (!DateTime.Equals(scCache.LastWriteTimeUtc, f.LastWriteTimeUtc) || scCache.FileSize != f.Length))
            { // Cache is outdated
                BinaryFormatter formatter = new BinaryFormatter();
                using (MemoryStream ms = new MemoryStream())
                {
                    formatter.Serialize(ms, sc);
                    scCache.Serialized = ms.ToArray();
                    scCache.LastWriteTimeUtc = f.LastWriteTimeUtc;
                    scCache.FileSize = f.Length;
                }

                lock (updatePool)
                {
                    updatePool.Add(scCache);
                    updated = true;
                }
            }

            if (sc.Type == ScriptType.Link && sc.LinkLoaded)
            {
                bool linkUpdated = SerializeScript(sc.Link, cachePool, updatePool);
                updated = updated || linkUpdated;
            }

            return updated;
        }

        public static (Script sc, bool cacheValid) DeserializeScript(string realPath, DB_ScriptCache[] cachePool)
        {
            Script sc = null;
            bool cacheValid = true;

            FileInfo f = new FileInfo(realPath);
            DB_ScriptCache scCache = cachePool.FirstOrDefault(x => x.Hash == realPath.GetHashCode());
            if (scCache != null &&
                scCache.RealPath.Equals(realPath, StringComparison.OrdinalIgnoreCase) &&
                DateTime.Equals(scCache.LastWriteTimeUtc, f.LastWriteTimeUtc) &&
                scCache.FileSize == f.Length)
            { // Cache Hit
                using (MemoryStream ms = new MemoryStream(scCache.Serialized))
                {
                    BinaryFormatter formatter = new BinaryFormatter();
                    sc = formatter.Deserialize(ms) as Script;

                    // Deserialization failed, mostly schema of Script is changed
                    if (sc == null)
                        cacheValid = false;
                }
            }

            return (sc, cacheValid);
        }
        #endregion

        #region SaveCacheRevision, CheckCacheRevision
        private const string EngineVersion = "EngineVersion";
        private const string BaseDir = "BaseDir";
        private const string CacheRevision = "CacheRevision";
        private const string AsteriskBugDirLink = "AsteriskBugDirLink";
        public void SaveCacheRevision(string baseDir)
        {
            DB_CacheRevision[] infos =
            {
                new DB_CacheRevision { Key = EngineVersion, Value = Global.Const.EngineVersion.ToString("000") },
                new DB_CacheRevision { Key = BaseDir, Value = baseDir },
                new DB_CacheRevision { Key = CacheRevision, Value = Global.Const.ScriptCacheRevision },
                new DB_CacheRevision { Key = AsteriskBugDirLink, Value = SerializeAsteriskBugDirLink() },
            };
            InsertOrReplaceAll(infos);
        }

        public bool CheckCacheRevision(string baseDir)
        {
            Dictionary<string, string> infoDict = Table<DB_CacheRevision>().ToDictionary(x => x.Key, x => x.Value);

            // Does key exist?
            if (!infoDict.ContainsKey(EngineVersion))
                return false;
            if (!infoDict.ContainsKey(BaseDir))
                return false;
            if (!infoDict.ContainsKey(CacheRevision))
                return false;
            if (!infoDict.ContainsKey(AsteriskBugDirLink))
                return false;

            // Does value match? (Used Ordinal instead of OrdinalIgnoreCase for cache safety)
            if (!infoDict[EngineVersion].Equals(Global.Const.EngineVersion.ToString("000"), StringComparison.Ordinal))
                return false;
            if (!infoDict[BaseDir].Equals(baseDir, StringComparison.Ordinal))
                return false;
            if (!infoDict[CacheRevision].Equals(Global.Const.ScriptCacheRevision, StringComparison.Ordinal))
                return false;
            if (!infoDict[AsteriskBugDirLink].Equals(SerializeAsteriskBugDirLink(), StringComparison.Ordinal))
                return false;

            return true;
        }

        public string SerializeAsteriskBugDirLink()
        {
            StringBuilder b = new StringBuilder();
            if (Global.Projects.FullyLoaded)
            { // Called by project refresh button
                foreach (Project p in Global.Projects)
                {
                    string key = p.ProjectName;
                    bool value = p.Compat.AsteriskBugDirLink;
                    b.AppendLine($"{key}={value}");
                }
            }
            else
            { // Called by Global.Init()
                foreach (string projectName in Global.Projects.ProjectNames)
                {
                    Debug.Assert(Global.Projects.CompatOptions.ContainsKey(projectName), "ProjectCollection error at ScriptCache.SerializeAsteriskBugDirLink");
                    CompatOption compat = Global.Projects.CompatOptions[projectName];

                    string key = projectName;
                    bool value = compat.AsteriskBugDirLink;
                    b.AppendLine($"{key}={value}");
                }
            }
            
            return b.ToString();
        }
        #endregion

        #region InsertOrReplaceAll
        public int InsertOrReplaceAll(IEnumerable objects)
        {
            int c = 0;
            RunInTransaction(() =>
            {
                foreach (object o in objects)
                {
                    c += InsertOrReplace(o);
                }
            });
            return c;
        }
        #endregion

        #region ClearTable
        public struct ClearTableOptions
        {
            public bool CacheInfo;
            public bool ScriptCache;
        }

        public void ClearTable(ClearTableOptions opts)
        {
            if (opts.CacheInfo)
                DeleteAll<DB_CacheRevision>();
            if (opts.ScriptCache)
                DeleteAll<DB_ScriptCache>();
            Execute("VACUUM");
        }
        #endregion
    }
    #endregion

    #region Model
    public class DB_CacheRevision
    {
        [PrimaryKey]
        public string Key { get; set; }
        public string Value { get; set; }

        public override string ToString() => $"Key [{Key}] = {Value}";
    }

    public class DB_ScriptCache
    {
        /// <summary>
        /// RealPath.GetHashCode()
        /// </summary>
        [PrimaryKey]
        public int Hash { get; set; }
        /// <summary>
        /// Equivalent to Script.DirectRealPath
        /// </summary>
        [MaxLength(32768)]
        public string RealPath { get; set; }
        public DateTime LastWriteTimeUtc { get; set; }
        public long FileSize { get; set; }
        public byte[] Serialized { get; set; }

        public override string ToString() => $"[{Hash}] {RealPath}";
    }
    #endregion
}
