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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization.Formatters.Binary;
using SQLite;
using System.Windows;
using PEBakery.Helper;
using PEBakery.TreeLib;
using PEBakery.IniLib;

namespace PEBakery.Core
{
    #region ProjectCollection
    public class ProjectCollection
    {
        #region Static Field
        public static bool AsteriskBugDirLink = false;
        #endregion

        #region Fields
        private readonly string _baseDir;
        private readonly Dictionary<string, Project> _projectDict = new Dictionary<string, Project>(StringComparer.Ordinal);
        private readonly ScriptCache _scriptCache;

        private readonly Dictionary<string, List<(string Path, bool IsDir)>> _scriptPathDict = new Dictionary<string, List<(string Path, bool IsDir)>>();
        private readonly Dictionary<string, List<(string RealPath, string TreePath, bool IsDir)>> _dirLinkPathDict = new Dictionary<string, List<(string RealPath, string TreePath, bool IsDir)>>();
        private readonly List<Script> _allProjectScripts = new List<Script>();
        private readonly List<(string Path, bool IsDir)> _allScriptPaths = new List<(string Path, bool IsDir)>();
        private readonly List<(string RealPath, string TreePath, bool IsDir)> _allDirLinkPaths = new List<(string RealPath, string TreePath, bool IsDir)>();
        #endregion

        #region Properties
        public string ProjectRoot { get; }
        public List<Project> Projects => _projectDict.Values.OrderBy(x => x.ProjectName).ToList();
        public List<string> ProjectNames => _projectDict.Keys.OrderBy(x => x).ToList();
        public Project this[int i] => Projects[i];
        public int Count => _projectDict.Count;
        #endregion

        #region Constructor
        public ProjectCollection(string baseDir, ScriptCache scriptCache)
        {
            _baseDir = baseDir;
            ProjectRoot = Path.Combine(baseDir, "Projects");
            _scriptCache = scriptCache;
        }
        #endregion

        #region PrepareLoad
        public int PrepareLoad(out int processCount)
        {
            // Ex) projNameList = { "ChrisPE", "MistyPE", "Win10PESE" }
            // Ex) scriptPathDict = [script paths of ChrisPE, script paths of MistyPE, ... ]
            List<string> projNameList = GetProjectNameList();
            GetScriptPaths(projNameList, out processCount);

            // Return count of all scripts
            return _allScriptPaths.Count + _allDirLinkPaths.Count;
        }
        #endregion

        #region GetProjectNameList
        /// <summary>
        /// Get project names
        /// </summary>
        /// <returns></returns>
        public List<string> GetProjectNameList()
        {
            if (Directory.Exists(ProjectRoot))
            {
                string[] projArray = Directory.GetDirectories(ProjectRoot);
                List<string> projNameList = new List<string>(projArray.Length);
                foreach (string projDir in projArray)
                {
                    // Ex) projectDir = E:\WinPE\Win10PESE\Projects
                    // Ex) projPath   = E:\WinPE\Win10PESE\Projects\Win10PESE\script.project
                    // Ex) projName -> E:\WinPE\Win10PESE\Projects\Win10PESE -> Win10PESE
                    if (File.Exists(Path.Combine(projDir, "script.project")))
                    {
                        string projName = Path.GetFileName(projDir);
                        projNameList.Add(projName);
                    }
                }

                return projNameList;
            }

            // Cannot find projectRoot, return empty list
            return new List<string>();
        }
        #endregion

        #region GetScriptPaths
        /// <summary>
        /// Get scriptPathDict and allScriptPathList
        /// </summary>
        public int GetScriptPaths(List<string> projNameList, out int linkCount)
        {
            int allCount = 0;
            linkCount = 0;
            foreach (string projName in projNameList)
            {
                string projectDir = Path.Combine(ProjectRoot, projName);

                (string, bool) rootScript = (Path.Combine(projectDir, "script.project"), false);
                (string, bool)[] scripts = FileHelper.GetFilesExWithDirs(projectDir, "*.script", SearchOption.AllDirectories);
                (string, bool)[] links = FileHelper.GetFilesExWithDirs(projectDir, "*.link", SearchOption.AllDirectories);

                // Path of root, normal and link scripts
                var scriptPathList = new List<(string Path, bool IsDir)>(1 + scripts.Length + links.Length)
                {
                    rootScript
                };
                scriptPathList.AddRange(scripts);
                scriptPathList.AddRange(links);

                allCount += scriptPathList.Count;
                linkCount += links.Length; // links should be added twice since they are processed twice

                _scriptPathDict[projName] = scriptPathList;
                _allScriptPaths.AddRange(scriptPathList);

                // Path of directoy linkes
                List<(string RealPath, string TreePath, bool IsDir)> dirLinkPathList = GetDirLinks(projectDir);
                _dirLinkPathDict[projName] = dirLinkPathList;
                _allDirLinkPaths.AddRange(dirLinkPathList);
            }
            return allCount;
        }
        #endregion

        #region GetDirLinks
        private List<(string RealPath, string TreePath, bool IsDir)> GetDirLinks(string projectDir)
        {
            var dirLinkPathList = new List<(string, string, bool)>();
            var linkFiles = Directory.EnumerateFiles(projectDir, "folder.project", SearchOption.AllDirectories);
            foreach (string linkFile in linkFiles.Where(x => Ini.ContainsSection(x, "Links")))
            {
                string prefix = Path.GetDirectoryName(linkFile);
                if (prefix == null)
                    continue;

                List<string> rawPaths = Ini.ParseRawSection(linkFile, "Links");
                if (rawPaths == null)
                    continue;
                var paths = rawPaths.Select(x => x.Trim()).Where(x => x.Length != 0);

                foreach (string path in paths)
                {
                    Debug.Assert(path != null, "Internal Logic Error at ProjectCollection.GetDirLinks");

                    bool isWildcard = StringHelper.IsWildcard(Path.GetFileName(path));
                    if (AsteriskBugDirLink && isWildcard)
                    { // Simulate WinBuilder *.* bug
                        string dirPath = Path.GetDirectoryName(path);
                        Debug.Assert(dirPath != null, "Internal Logic Error at ProjectCollection.GetDirLinks");

                        if (Path.IsPathRooted(dirPath))
                        { // Absolute Path
                            if (!Directory.Exists(dirPath))
                            {
                                App.Logger.SystemWrite(new LogInfo(LogState.Error, $"Unable to find path [{dirPath}] for directory link"));
                                continue;
                            }

                            string[] subDirs = Directory.GetDirectories(dirPath);
                            foreach (string subDir in subDirs)
                            {
                                Debug.Assert(subDir != null, "Internal Logic Error at ProjectCollection.GetDirLinks");

                                var tuples = FileHelper.GetFilesExWithDirs(subDir, "*.script", SearchOption.AllDirectories)
                                    .Select(x => (x.Path, Path.Combine(prefix, Path.GetFileName(subDir), x.Path.Substring(subDir.Length).TrimStart('\\')), x.IsDir));
                                dirLinkPathList.AddRange(tuples);
                            }
                        }
                        else
                        { // Relative to %BaseDir%
                            string fullPath = Path.Combine(_baseDir, dirPath);
                            if (!Directory.Exists(fullPath))
                            {
                                App.Logger.SystemWrite(new LogInfo(LogState.Error, $"Unable to find path [{fullPath}] for directory link"));
                                continue;
                            }

                            string[] subDirs = Directory.GetDirectories(fullPath);
                            foreach (string subDir in subDirs)
                            {
                                Debug.Assert(subDir != null, "Internal Logic Error at ProjectCollection.GetDirLinks");

                                var tuples = FileHelper.GetFilesExWithDirs(subDir, "*.script", SearchOption.AllDirectories)
                                    .Select(x => (x.Path, Path.Combine(prefix, Path.GetFileName(subDir), x.Path.Substring(subDir.Length).TrimStart('\\')), x.IsDir));
                                dirLinkPathList.AddRange(tuples);
                            }
                        }
                    }
                    else
                    { // Ignore wildcard
                        string dirPath = path;
                        if (isWildcard)
                            dirPath = Path.GetDirectoryName(path);

                        if (Path.IsPathRooted(dirPath))
                        { // Absolute Path
                            Debug.Assert(dirPath != null, "Internal Logic Error at ProjectCollection.GetDirLinks");

                            if (!Directory.Exists(dirPath))
                            {
                                App.Logger.SystemWrite(new LogInfo(LogState.Error, $"Unable to find path [{dirPath}] for directory link"));
                                continue;
                            }

                            var tuples = FileHelper.GetFilesExWithDirs(dirPath, "*.script", SearchOption.AllDirectories)
                                .Select(x => (x.Path, Path.Combine(prefix, Path.GetFileName(dirPath), x.Path.Substring(dirPath.Length).TrimStart('\\')), x.IsDir));
                            dirLinkPathList.AddRange(tuples);
                        }
                        else
                        { // Relative to %BaseDir%
                            Debug.Assert(dirPath != null, "Internal Logic Error at ProjectCollection.GetDirLinks");

                            string fullPath = Path.Combine(_baseDir, dirPath);
                            if (!Directory.Exists(fullPath))
                            {
                                App.Logger.SystemWrite(new LogInfo(LogState.Error, $"Unable to find path [{fullPath}] for directory link"));
                                continue;
                            }

                            var tuples = FileHelper.GetFilesExWithDirs(fullPath, "*.script", SearchOption.AllDirectories)
                                .Select(x => (x.Path, Path.Combine(prefix, Path.GetFileName(dirPath), x.Path.Substring(fullPath.Length).TrimStart('\\')), x.IsDir));
                            dirLinkPathList.AddRange(tuples);
                        }
                    }
                }
            }

            return dirLinkPathList;
        }
        #endregion

        #region Load, LoadLinks
        public List<LogInfo> Load(BackgroundWorker worker)
        {
            List<LogInfo> logs = new List<LogInfo>(32);
            try
            {
                foreach (string key in _scriptPathDict.Keys)
                {
                    Project project = new Project(_baseDir, key);

                    // Load scripts
                    List<LogInfo> projLogs = project.Load(_scriptPathDict[key], _dirLinkPathDict[key], _scriptCache, worker);
                    logs.AddRange(projLogs);

                    // Add Project.Scripts to ProjectCollections.Scripts
                    _allProjectScripts.AddRange(project.AllScripts);

                    _projectDict[key] = project;
                }

                // Populate *.link scripts
                List<LogInfo> linkLogs = LoadLinks(worker);
                logs.AddRange(linkLogs);

                // PostLoad scripts
                foreach (var kv in _projectDict)
                {
                    kv.Value.PostLoad();
                }
            }
            catch (SQLiteException e)
            { // Update failure
                string msg = $"SQLite Error : {e.Message}\r\nCache Database is corrupted. Please delete PEBakeryCache.db and restart.";
                MessageBox.Show(msg, "SQLite Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown(1);
            }

            return logs;
        }

        private List<LogInfo> LoadLinks(BackgroundWorker worker)
        {
            List<LogInfo> logs = new List<LogInfo>(32);
            List<int> removeIdxs = new List<int>();

            // Doing this will consume memory, but also greatly increase performance.
            DB_ScriptCache[] cacheDb = null;
            if (_scriptCache != null)
            {
                worker?.ReportProgress(-1);
                cacheDb = _scriptCache.Table<DB_ScriptCache>().ToArray();
            }

            bool cacheValid = true;
            Script[] links = _allProjectScripts.Where(x => x.Type == ScriptType.Link).ToArray();
            Debug.Assert(links.Count(x => x.IsDirLink) == 0);
            Parallel.ForEach(links, sc =>
            {
                Script link = null;
                bool valid = false;
                int cached = 2;
                try
                {
                    do
                    {
                        string linkPath = sc.Sections["Main"].IniDict["Link"];
                        string linkFullPath = Path.Combine(_baseDir, linkPath);
                        if (File.Exists(linkFullPath) == false) // Invalid link
                            break;

                        // Load .link's linked scripts with cache
                        if (cacheDb != null && cacheValid)
                        { // Case of ScriptCache enabled
                            FileInfo f = new FileInfo(linkFullPath);
                            DB_ScriptCache scCache = cacheDb.FirstOrDefault(x => x.Hash == linkPath.GetHashCode());
                            if (scCache != null &&
                                scCache.Path.Equals(linkPath, StringComparison.Ordinal) &&
                                DateTime.Equals(scCache.LastWriteTimeUtc, f.LastWriteTimeUtc) &&
                                scCache.FileSize == f.Length)
                            {
                                try
                                {
                                    using (MemoryStream ms = new MemoryStream(scCache.Serialized))
                                    {
                                        BinaryFormatter formatter = new BinaryFormatter();
                                        link = formatter.Deserialize(ms) as Script;
                                    }

                                    if (link == null)
                                    {
                                        cacheValid = false;
                                    }
                                    else
                                    {
                                        link.Project = sc.Project;
                                        link.IsDirLink = false;
                                        cached = 3;
                                    }
                                }
                                catch { link = null; }
                            }
                        }

                        if (link == null)
                        {
                            // TODO : Lazy loading of link, takes too much time at start
                            string ext = Path.GetExtension(linkFullPath);
                            ScriptType type = ScriptType.Script;
                            if (ext.Equals(".link", StringComparison.OrdinalIgnoreCase))
                                type = ScriptType.Link;
                            string fullPath = Path.Combine(_baseDir, linkFullPath);
                            link = new Script(type, fullPath, fullPath, sc.Project, ProjectRoot, null, false, false, false);

                            Debug.Assert(sc != null);
                        }

                        // Convert nested link to one-depth link
                        if (link.Type == ScriptType.Script)
                        {
                            valid = true;
                            break;
                        }
                        link = link.Link;
                    }
                    while (link.Type != ScriptType.Script);
                }
                catch (Exception e)
                { // Parser Error
                    logs.Add(new LogInfo(LogState.Error, Logger.LogExceptionMessage(e)));
                }

                if (valid)
                {
                    sc.LinkLoaded = true;
                    sc.Link = link;
                    worker?.ReportProgress(cached, Path.GetDirectoryName(sc.TreePath));
                }
                else // Error
                {
                    int idx = _allProjectScripts.IndexOf(sc);
                    removeIdxs.Add(idx);
                    worker?.ReportProgress(cached);
                }
            });

            // Remove malformed link
            var idxs = removeIdxs.OrderByDescending(x => x);
            foreach (int idx in idxs)
                _allProjectScripts.RemoveAt(idx);

            return logs;
        }
        #endregion
    }
    #endregion

    #region Project
    public class Project
    {
        #region Fields
        private int _mainScriptIdx;
        #endregion

        #region Properties
        public string ProjectName { get; }
        public string ProjectRoot { get; }
        public string ProjectDir { get; }
        public string BaseDir { get; }
        public Script MainScript => AllScripts[_mainScriptIdx];
        public List<Script> AllScripts { get; private set; }
        public List<Script> ActiveScripts => CollectActiveScripts(AllScripts);
        public List<Script> VisibleScripts => CollectVisibleScripts(AllScripts);
        public Variables Variables { get; set; }
        public int LoadedScriptCount { get; private set; }
        public int AllScriptCount { get; private set; }
        #endregion

        #region Constructor
        public Project(string baseDir, string projectName)
        {
            LoadedScriptCount = 0;
            AllScriptCount = 0;
            ProjectName = projectName;
            ProjectRoot = Path.Combine(baseDir, "Projects");
            ProjectDir = Path.Combine(baseDir, "Projects", projectName);
            BaseDir = baseDir;
        }
        #endregion

        #region Struct ScriptParseInfo
        private struct ScriptParseInfo
        {
            public readonly string RealPath;
            public readonly string TreePath;
            public readonly bool IsDir;
            public readonly bool IsDirLink;

            public ScriptParseInfo(string realPath, string treePath, bool isDir, bool isDirLink)
            {
                RealPath = realPath;
                TreePath = treePath;
                IsDir = isDir;
                IsDirLink = isDirLink;
            }
        }
        #endregion

        #region Load Scripts
        public List<LogInfo> Load(
            List<(string Path, bool IsDir)> allScriptPathList,
            List<(string RealPath, string TreePath, bool IsDir)> allDirLinkPathList,
            ScriptCache scriptCache,
            BackgroundWorker worker)
        {
            List<LogInfo> logs = new List<LogInfo>(32);

            ReaderWriterLockSlim listLock = new ReaderWriterLockSlim();
            string mainScriptPath = Path.Combine(ProjectDir, "script.project");
            AllScripts = new List<Script>();

            DB_ScriptCache[] cacheDb = null;
            if (scriptCache != null)
            {
                worker?.ReportProgress(-1);
                cacheDb = scriptCache.Table<DB_ScriptCache>().ToArray();
            }

            // ScriptParseInfo
            var spiList = new List<ScriptParseInfo>();
            spiList.AddRange(allScriptPathList.Select(x => new ScriptParseInfo(x.Path, x.Path, x.IsDir, false)));
            spiList.AddRange(allDirLinkPathList.Select(x => new ScriptParseInfo(x.RealPath, x.TreePath, x.IsDir, true)));

            // Load scripts from disk or cache
            bool cacheValid = true;
            Parallel.ForEach(spiList, spi =>
            {
                Debug.Assert(spi.RealPath != null, "Internal Logic Error at Project.Load");
                Debug.Assert(spi.TreePath != null, "Internal Logic Error at Project.Load");

                int cached = 0;
                Script sc = null;
                try
                {
                    if (cacheDb != null && cacheValid)
                    { // ScriptCache enabled
                        FileInfo f = new FileInfo(spi.RealPath);
                        string sPath = spi.TreePath.Remove(0, BaseDir.Length + 1); // 1 for \
                        DB_ScriptCache pCache = cacheDb.FirstOrDefault(x => x.Hash == sPath.GetHashCode());
                        if (pCache != null &&
                            pCache.Path.Equals(sPath, StringComparison.Ordinal) &&
                            DateTime.Equals(pCache.LastWriteTimeUtc, f.LastWriteTimeUtc) &&
                            pCache.FileSize == f.Length)
                        { // Cache Hit
                            try
                            {
                                using (MemoryStream memStream = new MemoryStream(pCache.Serialized))
                                {
                                    BinaryFormatter formatter = new BinaryFormatter();
                                    sc = formatter.Deserialize(memStream) as Script;
                                }

                                if (sc == null)
                                {
                                    cacheValid = false;
                                }
                                else
                                {
                                    sc.Project = this;
                                    sc.IsDirLink = spi.IsDirLink;
                                    cached = 1;
                                }
                            }
                            catch { sc = null; } // Cache Error
                        }
                    }

                    if (sc == null)
                    { // Cache Miss
                        bool isMainScript = spi.RealPath.Equals(mainScriptPath, StringComparison.Ordinal);

                        // TODO : Lazy loading of link, takes too much time at start
                        // Directory scripts will not be directly used (so level information is dummy)
                        // They are mainly used to store RealPath and TreePath information.
                        if (spi.IsDir) // level information is empty
                            sc = new Script(ScriptType.Directory, spi.RealPath, spi.TreePath, this, ProjectRoot, null, false, false, spi.IsDirLink);
                        else if (Path.GetExtension(spi.TreePath).Equals(".link", StringComparison.OrdinalIgnoreCase))
                            sc = new Script(ScriptType.Link, spi.RealPath, spi.TreePath, this, ProjectRoot, null, isMainScript, false, false);
                        else
                            sc = new Script(ScriptType.Script, spi.RealPath, spi.TreePath, this, ProjectRoot, null, isMainScript, false, spi.IsDirLink);

                        Debug.Assert(sc != null);
                    }

                    listLock.EnterWriteLock();
                    try
                    {
                        AllScripts.Add(sc);
                    }
                    finally
                    {
                        listLock.ExitWriteLock();
                    }

                    worker?.ReportProgress(cached, Path.GetDirectoryName(sc.TreePath));
                }
                catch (Exception e)
                {
                    logs.Add(new LogInfo(LogState.Error, Logger.LogExceptionMessage(e)));
                    worker?.ReportProgress(cached);
                }
            });

            // mainScriptIdx
            SetMainScriptIdx();

            return logs;
        }
        #endregion

        #region PostLoad, Sort
        public void PostLoad()
        {
            SortAllScripts();
            Variables = new Variables(this);
        }

        public void SortAllScripts()
        {
            AllScripts = InternalSortScripts(AllScripts);
            SetMainScriptIdx();
        }

        private List<Script> InternalSortScripts(List<Script> scList)
        {
            Tree<Script> pTree = new Tree<Script>();
            Dictionary<string, int> dirDict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            int rootId = pTree.AddNode(0, MainScript); // Root is script.project

            foreach (Script sc in scList)
            {
                Debug.Assert(sc != null, "Internal Logic Error at InternalSortScripts");

                if (sc.IsMainScript)
                    continue;

                int nodeId = rootId;
                string[] paths = sc.TreePath
                    .Substring(ProjectName.Length).TrimStart('\\')
                    .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                // Ex) Apps\Network\Mozilla_Firefox_CR.script
                for (int i = 0; i < paths.Length - 1; i++)
                {
                    string pathKey = PathKeyGenerator(paths, i);
                    string key = $"{sc.Level}_{pathKey}";
                    if (dirDict.ContainsKey(key))
                    {
                        nodeId = dirDict[key];
                    }
                    else
                    {
                        string treePath = Path.Combine(ProjectName, pathKey);
                        Script ts = scList.FirstOrDefault(x => x.TreePath.Equals(treePath, StringComparison.OrdinalIgnoreCase));
                        Debug.Assert(ts != null, "Internal Logic Error at InternalSortScripts");

                        Script dirScript = new Script(ScriptType.Directory, ts.RealPath, ts.TreePath, this, ProjectRoot, sc.Level, false, false, ts.IsDirLink);
                        nodeId = pTree.AddNode(nodeId, dirScript);
                        dirDict[key] = nodeId;
                    }
                }

                pTree.AddNode(nodeId, sc);
            }

            // Sort - Script first, Directory last
            pTree.Sort((x, y) =>
            {
                if (x.Data.Level == y.Data.Level)
                {
                    if (x.Data.Type == ScriptType.Directory)
                    {
                        if (y.Data.Type == ScriptType.Directory)
                            return string.Compare(x.Data.RealPath, y.Data.RealPath, StringComparison.Ordinal);
                        else
                            return 1;
                    }
                    else
                    {
                        if (y.Data.Type == ScriptType.Directory)
                            return -1;
                        else
                            return string.Compare(x.Data.RealPath, y.Data.RealPath, StringComparison.Ordinal);
                    }
                }
                else
                {
                    return x.Data.Level - y.Data.Level;
                }
            });

            List<Script> newList = new List<Script>();
            foreach (Script sc in pTree)
                newList.Add(sc);

            return newList;
        }

        public void SetMainScriptIdx()
        {
            _mainScriptIdx = AllScripts.FindIndex(x => x.IsMainScript);
            Debug.Assert(AllScripts.Count(x => x.IsMainScript) == 1);
            Debug.Assert(_mainScriptIdx != -1);
        }
        #endregion

        #region RefreshScript
        public Script RefreshScript(Script sc, EngineState s = null)
        {
            if (sc == null) throw new ArgumentNullException(nameof(sc));
            if (sc.Type == ScriptType.Directory)
                return null;

            int aIdx = AllScripts.FindIndex(x => x.RealPath.Equals(sc.RealPath, StringComparison.OrdinalIgnoreCase));
            if (aIdx == -1)
            { // Even if idx is not found in Projects directory, just proceed to deal with monkey-patched scripts.
                sc = LoadScript(sc.RealPath, sc.TreePath, true, sc.IsDirLink);
            }
            else
            {
                // This one is in legit Project list, so [Main] cannot be ignored
                sc = LoadScript(sc.RealPath, sc.TreePath, false, sc.IsDirLink);
                if (sc == null)
                    return null;

                AllScripts[aIdx] = sc;

                // Investigate EngineState to update it on build list
                if (s == null)
                    return sc;
                int sIdx = s.Scripts.FindIndex(x => x.RealPath.Equals(sc.RealPath, StringComparison.OrdinalIgnoreCase));
                if (sIdx != -1)
                    s.Scripts[sIdx] = sc;
            }
            return sc;
        }
        #endregion

        #region LoadScript, LoadScriptMonkeyPatch
        /// <summary>
        /// Load scripts into project while running
        /// Return true if error
        /// </summary>
        // public Script LoadScriptMonkeyPatch2(string fullPath, bool addToProjectTree = false, bool ignoreMain = false, bool overwriteProjectTree = false)
        public Script LoadScriptMonkeyPatch(string realPath, bool ignoreMain = false, bool addToProjectTree = false, bool overwriteProjectTree = false)
        {
            return LoadScriptMonkeyPatch(realPath, realPath, ignoreMain, addToProjectTree, overwriteProjectTree);
        }

        /// <summary>
        /// Load scripts into project while running
        /// Return true if error
        /// </summary>
        public Script LoadScriptMonkeyPatch(string realPath, string treePath, bool ignoreMain = false, bool addToProjectTree = false, bool overwriteProjectTree = false)
        {
            if (realPath == null)
                throw new ArgumentNullException(nameof(realPath));
            if (treePath == null)
                throw new ArgumentNullException(nameof(treePath));

            Script sc = LoadScript(realPath, treePath, ignoreMain, false);
            if (addToProjectTree)
            {
                int sIdx = AllScripts.FindIndex(x => x.TreePath.Equals(treePath, StringComparison.OrdinalIgnoreCase));
                if (sIdx != -1)
                { // TreePath collision
                    if (overwriteProjectTree)
                        AllScripts[sIdx] = sc;
                    else
                        throw new InvalidOperationException($"Unable to overwrite project tree [{treePath}]");
                }
                else
                {
                    // Generate Directory Script if necessary
                    int bsIdx = sc.TreePath.IndexOf('\\');
                    if (bsIdx != -1)
                    {
                        string[] paths = sc.TreePath
                            .Substring(ProjectName.Length).TrimStart('\\')
                            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        for (int i = 0; i < paths.Length - 1; i++)
                        {
                            string pathKey = Project.PathKeyGenerator(paths, i);
                            Script ts = AllScripts.FirstOrDefault(x =>
                                x.Level == sc.Level &&
                                x.TreePath.Equals(pathKey, StringComparison.OrdinalIgnoreCase));
                            if (ts == null)
                            {
                                string fullTreePath = Path.Combine(ProjectRoot, ProjectName, pathKey);
                                string fullRealPath = Path.GetDirectoryName(realPath);
                                Script dirScript = new Script(ScriptType.Directory, fullRealPath, fullTreePath, this, ProjectRoot, sc.Level, false, false, sc.IsDirLink);
                                AllScripts.Add(dirScript);
                            }
                        }
                    }

                    AllScripts.Add(sc);
                }

                AllScriptCount += 1;
            }

            return sc;
        }

        public Script LoadScript(string realPath, string treePath, bool ignoreMain, bool isDirLink)
        {
            Script sc;
            try
            {
                if (realPath.Equals(Path.Combine(ProjectRoot, "script.project"), StringComparison.OrdinalIgnoreCase))
                {
                    sc = new Script(ScriptType.Script, realPath, treePath, this, ProjectRoot, 0, true, ignoreMain, isDirLink);
                }
                else
                {
                    string ext = Path.GetExtension(realPath);
                    if (ext.Equals(".link", StringComparison.OrdinalIgnoreCase))
                        sc = new Script(ScriptType.Link, realPath, treePath, this, ProjectRoot, null, false, false, false);
                    else
                        sc = new Script(ScriptType.Script, realPath, treePath, this, ProjectRoot, null, false, ignoreMain, isDirLink);
                }

                // Check Script Link's validity
                // Also, convert nested link to one-depth link
                if (sc.Type == ScriptType.Link)
                {
                    Script link = sc.Link;
                    bool valid = false;
                    do
                    {
                        if (link == null)
                            return null;
                        if (link.Type == ScriptType.Script)
                        {
                            valid = true;
                            break;
                        }
                        link = link.Link;
                    }
                    while (link.Type != ScriptType.Script);

                    if (valid)
                        sc.Link = link;
                    else
                        return null;
                }
            }
            catch
            { // Do nothing - intentionally left blank
                return null;
            }

            return sc;
        }
        #endregion

        #region Active, Visible Scripts
        private static List<Script> CollectVisibleScripts(List<Script> allScripts)
        {
            List<Script> visibleScriptList = new List<Script>();
            foreach (Script sc in allScripts)
            {
                if (0 < sc.Level)
                    visibleScriptList.Add(sc);
            }
            return visibleScriptList;
        }

        private List<Script> CollectActiveScripts(List<Script> allScripts)
        {
            List<Script> activeScripts = new List<Script>(allScripts.Count)
            {
                MainScript
            };

            foreach (Script sc in allScripts.Where(x => !x.IsMainScript && 0 < x.Level))
            {
                bool active = false;
                if (sc.Type == ScriptType.Script || sc.Type == ScriptType.Link)
                {
                    if (sc.Selected != SelectedState.None)
                    {
                        if (sc.Mandatory || sc.Selected == SelectedState.True)
                            active = true;
                    }
                }

                if (active)
                    activeScripts.Add(sc);
            }

            return activeScripts;
        }
        #endregion

        #region PathKeyGenerator
        internal static string PathKeyGenerator(string[] paths, int last)
        { // last - start entry is 0
            StringBuilder b = new StringBuilder();
            b.Append(paths[0]);
            for (int i = 1; i <= last; i++)
            {
                b.Append(Path.DirectorySeparatorChar);
                b.Append(paths[i]);
            }
            return b.ToString();
        }
        #endregion

        #region GetScriptByPath, ContainsScript
        public Script GetScriptByRealPath(string sRealPath)
        {
            return AllScripts.Find(x => x.RealPath.Equals(sRealPath, StringComparison.OrdinalIgnoreCase));
        }

        public Script GetScriptByTreePath(string sTreePath)
        {
            return AllScripts.Find(x => x.TreePath.Equals(sTreePath, StringComparison.OrdinalIgnoreCase));
        }

        public bool ContainsScriptByRealPath(string sRealPath)
        {
            return AllScripts.FindIndex(x => x.RealPath.Equals(sRealPath, StringComparison.OrdinalIgnoreCase)) != -1;
        }

        public bool ContainsScriptByTreePath(string sTreePath)
        {
            return AllScripts.FindIndex(x => x.TreePath.Equals(sTreePath, StringComparison.OrdinalIgnoreCase)) != -1;
        }
        #endregion

        #region Variables
        public void UpdateProjectVariables()
        {
            if (Variables == null)
                return;

            ScriptSection section = MainScript.RefreshSection(Variables.VarSectionName);
            if (section != null)
                Variables.AddVariables(VarsType.Global, section);
        }
        #endregion

        #region DeepCopy
        public Project PartialDeepCopy()
        {
            Project project = new Project(BaseDir, ProjectName)
            {
                _mainScriptIdx = _mainScriptIdx,
                AllScripts = new List<Script>(AllScripts),
                Variables = Variables.DeepCopy(),
                LoadedScriptCount = LoadedScriptCount,
                AllScriptCount = AllScriptCount,
            };
            return project;
        }
        #endregion

        #region Equals
        public override bool Equals(object obj)
        {
            Project project = obj as Project;
            return Equals(project);
        }

        public bool Equals(Project project)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));

            return ProjectName.Equals(project.ProjectName, StringComparison.OrdinalIgnoreCase) &&
                   ProjectRoot.Equals(project.ProjectRoot, StringComparison.OrdinalIgnoreCase) &&
                   ProjectDir.Equals(project.ProjectDir, StringComparison.OrdinalIgnoreCase) &&
                   AllScriptCount == project.AllScriptCount;
        }

        public override int GetHashCode()
        {
            return ProjectName.GetHashCode() ^ ProjectRoot.GetHashCode() ^ ProjectDir.GetHashCode();
        }
        #endregion

        #region ToString
        public override string ToString()
        {
            return ProjectName;
        }
        #endregion
    }
    #endregion
}
