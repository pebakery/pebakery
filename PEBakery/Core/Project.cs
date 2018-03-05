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
using PEBakery.Exceptions;
using System.Runtime.Serialization.Formatters.Binary;
using SQLite;
using System.Windows;
using PEBakery.WPF;
using PEBakery.Helper;
using PEBakery.TreeLib;
using PEBakery.IniLib;

namespace PEBakery.Core
{
    #region ProjectCollection
    public class ProjectCollection
    {
        #region Fields
        private readonly string baseDir;
        private readonly Dictionary<string, Project> projectDict = new Dictionary<string, Project>(StringComparer.Ordinal);
        private readonly ScriptCache scriptCache;

        private readonly Dictionary<string, List<(string Path, bool IsDir)>> scriptPathDict = new Dictionary<string, List<(string Path, bool IsDir)>>();
        private readonly Dictionary<string, List<(string RealPath, string TreePath, bool IsDir)>> dirLinkPathDict = new Dictionary<string, List<(string RealPath, string TreePath, bool IsDir)>>();
        private readonly List<Script> allProjectScripts = new List<Script>();
        private readonly List<(string Path, bool IsDir)> allScriptPaths = new List<(string Path, bool IsDir)>();
        private readonly List<(string RealPath, string TreePath, bool IsDir)> allDirLinkPaths = new List<(string RealPath, string TreePath, bool IsDir)>();
        #endregion

        #region Properties
        public string ProjectRoot { get; }
        public List<Project> Projects => projectDict.Values.OrderBy(x => x.ProjectName).ToList(); 
        public List<string> ProjectNames => projectDict.Keys.OrderBy(x => x).ToList(); 
        public Project this[int i] => Projects[i];
        public int Count => projectDict.Count;
        #endregion

        #region Constructor
        public ProjectCollection(string baseDir, ScriptCache scriptCache)
        {
            this.baseDir = baseDir;
            this.ProjectRoot = Path.Combine(baseDir, "Projects");
            this.scriptCache = scriptCache;
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
            return allScriptPaths.Count + allDirLinkPaths.Count;
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
            else
            {
                // CAnnot find projectRoot, return empty list
                return new List<string>();
            }
        }
        #endregion

        #region GetScriptPaths, GetDirLinks
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

                // string[] scripts = Directory.GetFiles(projectDir, "*.script", SearchOption.AllDirectories);
                // string[] links = Directory.GetFiles(projectDir, "*.link", SearchOption.AllDirectories);

                // Path of root, normal and link scripts
                var scriptPathList = new List<(string Path, bool IsDir)>(1 + scripts.Length + links.Length)
                {
                    rootScript
                };
                scriptPathList.AddRange(scripts);
                scriptPathList.AddRange(links);

                allCount += scriptPathList.Count;
                linkCount += links.Length; // links should be added twice since they are processed twice

                scriptPathDict[projName] = scriptPathList;
                allScriptPaths.AddRange(scriptPathList);

                // Path of directoy linkes
                List<(string RealPath, string TreePath, bool IsDir)> dirLinkPathList = GetDirLinks(projectDir);
                dirLinkPathDict[projName] = dirLinkPathList;
                allDirLinkPaths.AddRange(dirLinkPathList);
            }
            return allCount;
        }

        private List<(string RealPath, string TreePath, bool IsDir)> GetDirLinks(string projectDir)
        {
            var dirLinkPathList = new List<(string, string, bool)>();
            var linkFiles = Directory.EnumerateFiles(projectDir, "folder.project", SearchOption.AllDirectories);
            foreach (string linkFile in linkFiles.Where(x => Ini.SectionExists(x, "Links")))
            {
                string prefix = Path.GetDirectoryName(linkFile);
                if (prefix == null)
                    continue;

                var paths = Ini.ParseRawSection(linkFile, "Links").Select(x => x.Trim()).Where(x => x.Length != 0);
                foreach (string path in paths)
                {
                    // Remove asterisk
                    string dirPath = path;
                    if (StringHelper.IsWildcard(Path.GetFileName(path)))
                        dirPath = Path.GetDirectoryName(path);
                    if (dirPath == null)
                        continue;

                    if (Path.IsPathRooted(dirPath))
                    { // Absolute Path
                        var tuples = FileHelper.GetFilesExWithDirs(dirPath, "*.script", SearchOption.AllDirectories)
                            .Select(x => (x.Path, Path.Combine(prefix, x.Path.Substring(dirPath.Length).TrimStart('\\')), x.IsDir));
                        dirLinkPathList.AddRange(tuples);
                    }
                    else
                    { // Relative Path to %BaseDir%
                        // Compat_AsteriskBugDirLink
                        string fullPath = Path.Combine(baseDir, dirPath);

                        var tuples = FileHelper.GetFilesExWithDirs(fullPath, "*.script", SearchOption.AllDirectories)
                            .Select(x => (x.Path, Path.Combine(prefix, Path.GetFileName(dirPath), x.Path.Substring(fullPath.Length).TrimStart('\\')), x.IsDir));
                        dirLinkPathList.AddRange(tuples);
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
                foreach (string key in scriptPathDict.Keys)
                {
                    Project project = new Project(baseDir, key);

                    // Load scripts
                    List<LogInfo> projLogs = project.Load(scriptPathDict[key], dirLinkPathDict[key], scriptCache, worker);
                    logs.AddRange(projLogs);

                    // Add Project.Scripts to ProjectCollections.Scripts
                    allProjectScripts.AddRange(project.AllScripts);

                    projectDict[key] = project;
                }

                // Populate *.link scripts
                List<LogInfo> linkLogs = LoadLinks(worker);
                logs.AddRange(linkLogs);

                // PostLoad scripts
                foreach (var kv in projectDict)
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
            DB_ScriptCache[] cacheDB = null;
            if (scriptCache != null)
                cacheDB = scriptCache.Table<DB_ScriptCache>().Where(x => true).ToArray();

            bool cacheValid = true;
            Script[] links = allProjectScripts.Where(x => x.Type == ScriptType.Link).ToArray();
            Debug.Assert(links.Count(x => x.IsDirLink) == 0);
            Task[] tasks = links.Select(p =>
            {
                return Task.Run(() =>
                {
                    Script link = null;
                    bool valid = false;
                    int cached = 2;
                    try
                    {
                        do
                        {
                            string linkPath = p.Sections["Main"].IniDict["Link"];
                            string linkFullPath = Path.Combine(baseDir, linkPath);
                            if (File.Exists(linkFullPath) == false) // Invalid link
                                break;

                            // Load .link's linked scripts with cache
                            if (cacheDB != null && cacheValid)
                            { // Case of ScriptCache enabled
                                FileInfo f = new FileInfo(linkFullPath);
                                DB_ScriptCache scCache = cacheDB.FirstOrDefault(x => x.Hash == linkPath.GetHashCode());
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
                                            link.Project = p.Project;
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
                                string fullPath = Path.Combine(baseDir, linkFullPath);
                                link = new Script(type, fullPath, fullPath, p.Project, ProjectRoot, null, false, false, false);

                                Debug.Assert(p != null);
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
                        p.LinkLoaded = true;
                        p.Link = link;
                        worker?.ReportProgress(cached, Path.GetDirectoryName(p.TreePath));
                    }
                    else // Error
                    {
                        int idx = allProjectScripts.IndexOf(p);
                        removeIdxs.Add(idx);
                        worker?.ReportProgress(cached);
                    }
                });
            }).ToArray();
            Task.WaitAll(tasks);

            // Remove malformed link
            var idxs = removeIdxs.OrderByDescending(x => x);
            foreach (int idx in idxs)
                allProjectScripts.RemoveAt(idx);

            return logs;
        }
        #endregion
    }
    #endregion

    #region Project
    public class Project : ICloneable
    {
        #region Static
        public static bool AsteriskBugDirLink = false;
        #endregion

        #region Fields
        private int mainScriptIdx;
        #endregion

        #region Properties
        public string ProjectName { get; private set; }
        public string ProjectRoot { get; private set; }
        public string ProjectDir { get; private set; }
        public string BaseDir { get; }
        public Script MainScript => AllScripts[mainScriptIdx];
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
            this.LoadedScriptCount = 0;
            this.AllScriptCount = 0;
            this.ProjectName = projectName;
            this.ProjectRoot = Path.Combine(baseDir, "Projects");
            this.ProjectDir = Path.Combine(baseDir, "Projects", projectName);
            this.BaseDir = baseDir;
        }
        #endregion

        #region Struct ScriptParseInfo
        private struct ScriptParseInfo
        {
            public string RealPath;
            public string TreePath;
            public bool IsDir;
            public bool IsDirLink;

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

            // Doing this will consume memory, but also greatly increase performance.
            DB_ScriptCache[] cacheDB = null;
            if (scriptCache != null)
                cacheDB = scriptCache.Table<DB_ScriptCache>().Where(x => true).ToArray();

            // ScriptParseInfo
            var pList = new List<ScriptParseInfo>();
            pList.AddRange(allScriptPathList.Select(x => new ScriptParseInfo(x.Path, x.Path, x.IsDir, false)));
            pList.AddRange(allDirLinkPathList.Select(x => new ScriptParseInfo(x.RealPath, x.TreePath, x.IsDir, true)));

            // Load scripts from disk or cache
            bool cacheValid = true;
            Task[] tasks = pList.Select(p =>
            {
                return Task.Run(() =>
                {
                    int cached = 0;
                    Script sc = null;
                    try
                    {
                        if (cacheDB != null && cacheValid)
                        { // ScriptCache enabled
                            FileInfo f = new FileInfo(p.RealPath);
                            string sPath = p.TreePath.Remove(0, BaseDir.Length + 1); // 1 for \
                            DB_ScriptCache pCache = cacheDB.FirstOrDefault(x => x.Hash == sPath.GetHashCode());
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
                                        sc.IsDirLink = p.IsDirLink;
                                        cached = 1;
                                    }
                                }
                                catch { sc = null; } // Cache Error
                            }
                        }

                        if (sc == null)
                        { // Cache Miss
                            bool isMainScript = p.RealPath.Equals(mainScriptPath, StringComparison.Ordinal);

                            // TODO : Lazy loading of link, takes too much time at start
                            // Directory scripts will not be directly used (so level information is dummy)
                            // They are mainly used to store RealPath and TreePath information.
                            if (p.IsDir) // level information is empty
                                sc = new Script(ScriptType.Directory, p.RealPath, p.TreePath, this, ProjectRoot, null, false, false, p.IsDirLink);
                            else if (Path.GetExtension(p.TreePath).Equals(".link", StringComparison.OrdinalIgnoreCase))
                                sc = new Script(ScriptType.Link, p.RealPath, p.TreePath, this, ProjectRoot, null, isMainScript, false, false);
                            else
                                sc = new Script(ScriptType.Script, p.RealPath, p.TreePath, this, ProjectRoot, null, isMainScript, false, p.IsDirLink);

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
            }).ToArray();
            Task.WaitAll(tasks);

            // mainScriptIdx
            SetMainScriptIdx();

            return logs;
        }

        #region PostLoad, Sort
        public void PostLoad()
        {
            this.AllScripts = InternalSortScripts(AllScripts);
            SetMainScriptIdx();

            this.Variables = new Variables(this);
        }

        private List<Script> InternalSortScripts(List<Script> scList)
        {
            Tree<Script> pTree = new Tree<Script>();
            Dictionary<string, int> dirDict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            int rootId = pTree.AddNode(0, this.MainScript); // Root is script.project

            foreach (Script sc in scList)
            {
                Debug.Assert(sc != null);

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
                        // Script dirScript = new Script(ScriptType.Directory, fullPath, fullPath, this, ProjectRoot, sc.Level, false, false, false);
                        string treePath = Path.Combine(ProjectName, pathKey);
                        Script ts = scList.FirstOrDefault(x => x.TreePath.Equals(treePath, StringComparison.OrdinalIgnoreCase));
                        Debug.Assert(ts != null);

                        Script dirScript = new Script(ScriptType.Directory, ts.RealPath, ts.TreePath, this, ProjectRoot, sc.Level, false, false, ts.IsDirLink);
                        nodeId = pTree.AddNode(nodeId, dirScript);
                        dirDict[key] = nodeId;
                    }
                }
                Debug.Assert(sc != null);
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
            {
               //  if (sc.Type != ScriptType.Directory)
                    newList.Add(sc);
            }

            return newList;
        }

        public void SetMainScriptIdx()
        {
            mainScriptIdx = AllScripts.FindIndex(x => x.IsMainScript);
            Debug.Assert(AllScripts.Count(x => x.IsMainScript) == 1);
            Debug.Assert(mainScriptIdx != -1);
        }
        #endregion

        public Script RefreshScript(Script sc, EngineState s = null)
        {
            if (sc == null) throw new ArgumentNullException(nameof(sc));

            int aIdx = AllScripts.FindIndex(x => x.RealPath.Equals(sc.RealPath, StringComparison.OrdinalIgnoreCase));

            if (aIdx == -1)
            {
                // Even if idx is not found in Projects directory, just proceed.
                // If not, cannot deal with monkey-patched scripts.
                sc = LoadScript(sc.RealPath, sc.TreePath, true, sc.IsDirLink);
            }
            else
            {
                // This one is in legit Project list, so [Main] cannot be ignored
                sc = LoadScript(sc.RealPath, sc.TreePath, false, sc.IsDirLink);
                if (sc != null)
                {
                    AllScripts[aIdx] = sc;
                    if (s != null)
                    {
                        // Investigate EngineState to update it on build list
                        int sIdx = s.Scripts.FindIndex(x => x.RealPath.Equals(sc.RealPath, StringComparison.OrdinalIgnoreCase));
                        if (sIdx != -1)
                            s.Scripts[sIdx] = sc;
                    }
                }
            }
            return sc;
        }

        /// <summary>
        /// Load scripts into project while running
        /// Return true if error
        /// </summary>
        public Script LoadScriptMonkeyPatch(string fullPath, bool addToProjectTree = false, bool ignoreMain = false)
        {
            // Limit: fullPath must be in BaseDir
            if (fullPath.StartsWith(this.BaseDir, StringComparison.OrdinalIgnoreCase) == false)
                return null;

            Script sc = LoadScript(fullPath, fullPath, ignoreMain, false);
            if (addToProjectTree)
            {
                AllScripts.Add(sc);
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
            if (Variables != null)
            {
                if (MainScript.Sections.ContainsKey("Variables"))
                    Variables.AddVariables(VarsType.Global, MainScript.Sections["Variables"]);
            }
        }
        #endregion

        #region Clone
        public object Clone()
        {
            Project project = new Project(BaseDir, ProjectName)
            {
                mainScriptIdx = this.mainScriptIdx,
                AllScripts = new List<Script>(this.AllScripts),
                Variables = this.Variables.Clone() as Variables,
                LoadedScriptCount = this.LoadedScriptCount,
                AllScriptCount = this.AllScriptCount,
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

            if (ProjectName.Equals(project.ProjectName, StringComparison.OrdinalIgnoreCase) &&
                ProjectRoot.Equals(project.ProjectRoot, StringComparison.OrdinalIgnoreCase) &&
                ProjectDir.Equals(project.ProjectDir, StringComparison.OrdinalIgnoreCase) &&
                AllScriptCount == project.AllScriptCount)
                return true;
            else
                return false;

        }

        public override int GetHashCode()
        {
            return ProjectName.GetHashCode() ^ ProjectRoot.GetHashCode() ^ ProjectDir.GetHashCode() ^ AllScriptCount.GetHashCode();
        }
        #endregion
    }
    #endregion
}
