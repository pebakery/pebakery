/*
    Copyright (C) 2016-2022 Hajin Jang
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

using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using PEBakery.Helper;
using SQLite;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace PEBakery.Core
{
    #region ScriptCache
    public class ScriptCache : SQLiteConnection
    {
        #region Fields, Properties
        private static int _dbRefCount = 0;
        private readonly object _cachePoolLock = new object();
        private Dictionary<string, CacheModel.ScriptCache>? _cachePool;
        private readonly MessagePackSerializerOptions _msgPackOpts;

        public static int CacheCount
        {
            get
            {
                ScriptCache? scriptCache = Global.ScriptCache;
                if (scriptCache == null)
                    return 0;
                return scriptCache.Table<CacheModel.ScriptCache>().Count();
            }
        }
        #endregion

        #region Constructor
        public ScriptCache(string path) : base(path, SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.FullMutex)
        {
            // Create SQLite Tables
            CreateTable<CacheModel.CacheRevision>();
            CreateTable<CacheModel.ScriptCache>();

            // Prepare MessagePack resolvers
            IFormatterResolver[] resolvers = new IFormatterResolver[]
            {
                DynamicObjectResolverAllowPrivate.Instance,
                // StandardResolver.Instance,
                BuiltinResolver.Instance,
                DynamicEnumResolver.Instance,
                DynamicGenericResolver.Instance,
            };
            IFormatterResolver compositeResolver = CompositeResolver.Create(resolvers);
            _msgPackOpts = MessagePackSerializerOptions.Standard.WithResolver(compositeResolver);
        }
        #endregion

        #region Reference Count
        public static int Acquire()
        {
            return Interlocked.Increment(ref _dbRefCount);
        }

        public static int Release()
        {
            return Interlocked.Decrement(ref _dbRefCount);
        }

        public static bool IsRunning()
        {
            return 0 < _dbRefCount;
        }
        #endregion

        #region WaitClose
        public async void WaitClose()
        {
            while (true)
            {
                if (IsRunning() == false)
                {
                    Close();
                    return;
                }
                await Task.Delay(200);
            }
        }
        #endregion

        #region CachePool
        public void LoadCachePool()
        {
            lock (_cachePoolLock)
            {
                if (_cachePool == null)
                {
                    _cachePool = Table<CacheModel.ScriptCache>()
                        .Distinct(CacheModel.ScriptCacheComparer.Instance)
                        .ToDictionary(x => x.DirectRealPath, StringComparer.OrdinalIgnoreCase);
                }
            }
        }

        public void UnloadCachePool()
        {
            lock (_cachePoolLock)
            {
                _cachePool = null;
            }
        }
        #endregion

        #region CacheScripts
        public (int CachedCount, int UpdatedCount) CacheScripts(ProjectCollection projects, string baseDir)
        {
            try
            {
                LoadCachePool();
                SaveCacheRevision(baseDir, projects);

                int cachedCount = 0;
                int updatedCount = 0;
                foreach (Project project in projects.ProjectList)
                {
                    // Remove duplicate
                    Script[] uniqueScripts = project.AllScripts
                        .Where(x => x.Type != ScriptType.Directory)
                        .Distinct(ScriptComparer.Instance)
                        .ToArray();

                    List<CacheModel.ScriptCache> updatePool = new List<CacheModel.ScriptCache>();
                    Parallel.ForEach(uniqueScripts, sc =>
                    {
                        bool updated = SerializeScript(sc, updatePool);

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
                SystemHelper.MessageBoxDispatcherShow(msg, "SQLite Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown(1);
            }

            return (0, 0);
        }
        #endregion

        #region SerializeScript, DeserializeScript
        /// <returns>Return true if cache was updated</returns>
        private bool SerializeScript(Script sc, List<CacheModel.ScriptCache> updatePool)
        {
            if (updatePool == null)
                throw new ArgumentNullException(nameof(updatePool));
            if (_cachePool == null)
                throw new InvalidOperationException($"{nameof(_cachePool)} is null");
            Debug.Assert(sc.Type != ScriptType.Directory);

            // If script file is not found in the disk, ignore it
            if (!File.Exists(sc.DirectRealPath))
                return false;
            FileInfo f = new FileInfo(sc.DirectRealPath);

            // Retrieve Cache
            bool updated = false;
            CacheModel.ScriptCache? scCache = null;
            if (_cachePool.ContainsKey(sc.DirectRealPath))
                scCache = _cachePool[sc.DirectRealPath];

            // Update Cache into updateDB
            try
            {
                if (scCache == null)
                { // Cache does not exist

                    scCache = new CacheModel.ScriptCache
                    {
                        DirectRealPath = sc.DirectRealPath,
                        Serialized = MessagePackSerializer.Serialize(sc, _msgPackOpts),
                        LastWriteTimeUtc = f.LastWriteTimeUtc,
                        FileSize = f.Length,
                    };

                    lock (updatePool)
                    {
                        updatePool.Add(scCache);
                        updated = true;
                    }
                }
                else if (scCache.DirectRealPath.Equals(sc.DirectRealPath, StringComparison.OrdinalIgnoreCase) &&
                            (!DateTime.Equals(scCache.LastWriteTimeUtc, f.LastWriteTimeUtc) || scCache.FileSize != f.Length))
                { // Cache entry is outdated, update the entry.
                    scCache.Serialized = MessagePackSerializer.Serialize(sc, _msgPackOpts);
                    scCache.LastWriteTimeUtc = f.LastWriteTimeUtc;
                    scCache.FileSize = f.Length;

                    lock (updatePool)
                    {
                        updatePool.Add(scCache);
                        updated = true;
                    }
                }
            }
            catch (MessagePackSerializationException e)
            {
                Global.Logger.SystemWrite(new LogInfo(LogState.Error, e));
            }

            // Serialize also linked scripts
            if (sc.Type == ScriptType.Link && sc.LinkLoaded && sc.Link != null)
            {
                bool linkUpdated = SerializeScript(sc.Link, updatePool);
                updated = updated || linkUpdated;
            }

            return updated;
        }

        internal Script? DeserializeScript(string directRealPath, out bool isCacheValid)
        {
            LoadCachePool();

            if (_cachePool == null)
                throw new InvalidOperationException($"{nameof(_cachePool)} is null");

            Script? sc = null;
            isCacheValid = true;

            if (!_cachePool.ContainsKey(directRealPath))
                return null;

            FileInfo f = new FileInfo(directRealPath);
            CacheModel.ScriptCache scCache = _cachePool[directRealPath];
            if (scCache != null &&
                scCache.DirectRealPath.Equals(directRealPath, StringComparison.OrdinalIgnoreCase) &&
                DateTime.Equals(scCache.LastWriteTimeUtc, f.LastWriteTimeUtc) &&
                scCache.FileSize == f.Length)
            { // Cache Hit
                try
                {
                    sc = MessagePackSerializer.Deserialize<Script>(scCache.Serialized, _msgPackOpts);

                    // Deserialization failed (Casting failure)
                    // Ex) Field of the `Script` class have changed without revision update
                    if (sc == null)
                        isCacheValid = false;
                }
                catch (MessagePackSerializationException)
                { // Exception from MessagePackSerializer.Deserialize
                  // Cache is inconsistent, turn off script cache.
                  // Ex) `Script` class moved to different assembly
                    sc = null;
                    isCacheValid = false;
                }
            }

            return sc;
        }
        #endregion

        #region SaveCacheRevision, CheckCacheRevision
        private const string EngineVersion = "EngineVersion";
        private const string BaseDir = "BaseDir";
        private const string CacheRevision = "CacheRevision";
        private const string AsteriskBugDirLink = "AsteriskBugDirLink";

        public void SaveCacheRevision(string baseDir, ProjectCollection projects)
        {
            CacheModel.CacheRevision[] infos =
            {
                new CacheModel.CacheRevision { Key = EngineVersion, Value = Global.Const.EngineVersion.ToString("000") },
                new CacheModel.CacheRevision { Key = BaseDir, Value = baseDir },
                new CacheModel.CacheRevision { Key = CacheRevision, Value = Global.Const.ScriptCacheRevision },
                new CacheModel.CacheRevision { Key = AsteriskBugDirLink, Value = SerializeAsteriskBugDirLink(projects) },
            };
            InsertOrReplaceAll(infos);
        }

        public bool CheckCacheRevision(string baseDir, ProjectCollection projects)
        {
            Dictionary<string, string> infoDict = Table<CacheModel.CacheRevision>().ToDictionary(x => x.Key, x => x.Value);

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
            if (!infoDict[AsteriskBugDirLink].Equals(SerializeAsteriskBugDirLink(projects), StringComparison.Ordinal))
                return false;

            return true;
        }

        public static string SerializeAsteriskBugDirLink(ProjectCollection projects)
        {
            if (Global.Projects == null)
                return string.Empty;

            StringBuilder b = new StringBuilder();
            if (projects.FullyLoaded)
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
                foreach (string projectName in projects.ProjectNames)
                {
                    Debug.Assert(projects.CompatOptions.ContainsKey(projectName), "ProjectCollection error at ScriptCache.SerializeAsteriskBugDirLink");
                    CompatOption compat = projects.CompatOptions[projectName];

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
        public void ClearTable(ClearTableOptions opts)
        {
            if (opts.CacheInfo)
                DeleteAll<CacheModel.CacheRevision>();
            if (opts.ScriptCache)
                DeleteAll<CacheModel.ScriptCache>();
            Execute("VACUUM");
        }
        #endregion
    }
    #endregion

    #region ClearTableOptions
    public class ClearTableOptions
    {
        public bool CacheInfo;
        public bool ScriptCache;
    }
    #endregion

    #region Model
    internal class CacheModel
    {
        public class CacheRevision
        {
            [PrimaryKey]
            public string Key { get; set; } = string.Empty;
            public string Value { get; set; } = string.Empty;

            public override string ToString() => $"Key [{Key}] = {Value}";
        }

        public class ScriptCache
        {
            /// <summary>
            /// Equivalent to Script.DirectRealPath
            /// </summary>
            [PrimaryKey]
            [Indexed]
            [MaxLength(32768)]
            public string DirectRealPath { get; set; } = string.Empty;
            public DateTime LastWriteTimeUtc { get; set; }
            public long FileSize { get; set; }
            public byte[] Serialized { get; set; } = Array.Empty<byte>();

            public override string ToString() => $"[{FileSize}] {DirectRealPath}";
        }

        #region Comaparer
        public class ScriptCacheComparer : IEqualityComparer<ScriptCache>
        {
            public static ScriptCacheComparer Instance = new ScriptCacheComparer();

            public bool Equals(ScriptCache? x, ScriptCache? y)
            {
                if (x == null)
                {
                    if (y == null)
                        return true;
                    else
                        return false;
                }
                else
                {
                    if (y == null)
                        return false;
                    else
                        return x.DirectRealPath.Equals(y.DirectRealPath);
                }
            }

            public int GetHashCode(ScriptCache x)
            {
                return x.DirectRealPath.GetHashCode();
            }
        }
        #endregion
    }
    #endregion

    #region MessagePack - Formatter
    public class ScriptStringDictionaryFormatter<TValue>
       : DictionaryFormatterBase<string, TValue, Dictionary<string, TValue>, Dictionary<string, TValue>.Enumerator, Dictionary<string, TValue>>
    {
        public static ScriptStringDictionaryFormatter<TValue> Instance => new ScriptStringDictionaryFormatter<TValue>();

        protected override void Add(Dictionary<string, TValue> collection, int index, string key, TValue value, MessagePackSerializerOptions options)
        {
            collection.Add(key, value);
        }

        protected override Dictionary<string, TValue> Complete(Dictionary<string, TValue> intermediateCollection)
        {
            return intermediateCollection;
        }

        protected override Dictionary<string, TValue> Create(int count, MessagePackSerializerOptions options)
        {
            return new Dictionary<string, TValue>(count, StringComparer.OrdinalIgnoreCase);
        }

        protected override Dictionary<string, TValue>.Enumerator GetSourceEnumerator(Dictionary<string, TValue> source)
        {
            return source.GetEnumerator();
        }
    }
    #endregion
}
