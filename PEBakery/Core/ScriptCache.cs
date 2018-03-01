/*
    Copyright (C) 2016-2017 Hajin Jang
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
    #region ScriptCache
    public class ScriptCache : SQLiteConnection
    {
        public static int dbLock = 0;
        private ReaderWriterLockSlim listLock;

        public ScriptCache(string path) : base(path)
        {
            dbLock = 0;
            CreateTable<DB_CacheInfo>();
            CreateTable<DB_ScriptCache>();
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

        #region Cache Script or Scripts
        public void CacheScripts(ProjectCollection projects, string baseDir, BackgroundWorker worker)
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
                    var pUniqueList = project.AllScripts
                        .GroupBy(x => x.DirectRealPath)
                        .Select(x => x.First());

                    listLock = new ReaderWriterLockSlim();

                    DB_ScriptCache[] memDB = Table<DB_ScriptCache>().ToArray();
                    List<DB_ScriptCache> updateDB = new List<DB_ScriptCache>();
                    var tasks = pUniqueList.Select(p =>
                    {
                        return Task.Run(() =>
                        {
                            bool updated = CacheScript(p, memDB, updateDB);
                            worker.ReportProgress(updated ? 1 : 0); // 1 - updated, 0 - not updated
                        });
                    }).ToArray();
                    Task.WaitAll(tasks);
                    
                    InsertOrReplaceAll(updateDB);
                }
            }
            catch (SQLiteException e)
            { // Update failure
                string msg = $"SQLite Error : {e.Message}\r\nCache database is corrupted. Please delete PEBakeryCache.db and restart.";
                MessageBox.Show(msg, "SQLite Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown(1);
            }
        }

        /// <returns>Return true if cache is updated</returns>
        private bool CacheScript(Script p, DB_ScriptCache[] memDB, List<DB_ScriptCache> updateDB)
        {
            if (memDB == null) throw new ArgumentNullException(nameof(memDB));

            // Does cache exist?
            FileInfo f = new FileInfo(p.DirectRealPath);
            string sPath = p.DirectRealPath.Remove(0, p.Project.BaseDir.Length + 1);

            bool updated = false;
            // int memIdx = 0;
            DB_ScriptCache pCache = null;

            // Retrieve Cache
            pCache = memDB.FirstOrDefault(x => x.Hash == sPath.GetHashCode());

            // Update Cache into updateDB
            if (pCache == null)
            { // Cache not exists
                pCache = new DB_ScriptCache()
                {
                    Hash = sPath.GetHashCode(),
                    Path = sPath,
                    LastWriteTimeUtc = f.LastWriteTimeUtc,
                    FileSize = f.Length,
                };

                BinaryFormatter formatter = new BinaryFormatter();
                using (MemoryStream ms = new MemoryStream())
                {
                    formatter.Serialize(ms, p);
                    pCache.Serialized = ms.ToArray();
                }

                listLock.EnterWriteLock();
                try
                {
                    updateDB.Add(pCache);
                    updated = true;
                }
                finally
                {
                    listLock.ExitWriteLock();
                }
            }
            else if (pCache.Path.Equals(sPath, StringComparison.Ordinal) && 
                (DateTime.Equals(pCache.LastWriteTimeUtc, f.LastWriteTime) == false || pCache.FileSize != f.Length))
            { // Cache is outdated
                BinaryFormatter formatter = new BinaryFormatter();
                using (MemoryStream ms = new MemoryStream())
                {
                    formatter.Serialize(ms, p);
                    pCache.Serialized = ms.ToArray();
                    pCache.LastWriteTimeUtc = f.LastWriteTimeUtc;
                    pCache.FileSize = f.Length;
                }

                listLock.EnterWriteLock();
                try
                {
                    updateDB.Add(pCache);
                    updated = true;
                }
                finally
                {
                    listLock.ExitWriteLock();
                }
            }

            if (p.Type == ScriptType.Link && p.LinkLoaded)
            {
                bool linkUpdated = CacheScript(p.Link, memDB, updateDB);
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

    public class DB_ScriptCache
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
