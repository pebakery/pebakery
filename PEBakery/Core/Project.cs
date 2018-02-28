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
        #region Fields and Properties
        // Fields
        private readonly string baseDir;
        private readonly string projectRoot;
        private readonly Dictionary<string, Project> projectDict = new Dictionary<string, Project>(StringComparer.Ordinal);
        private readonly ScriptCache scriptCache;

        private readonly Dictionary<string, List<string>> scriptPathDict = new Dictionary<string, List<string>>();
        private readonly Dictionary<string, List<string>> dirLinkPathDict = new Dictionary<string, List<string>>();
        private readonly List<Script> allProjectScripts = new List<Script>();
        private readonly List<string> allScriptPaths = new List<string>();
        private readonly List<string> allDirLinkPaths = new List<string>();

        // Properties
        public string ProjectRoot => projectRoot;
        public List<Project> Projects => projectDict.Values.OrderBy(x => x.ProjectName).ToList(); 
        public List<string> ProjectNames => projectDict.Keys.OrderBy(x => x).ToList(); 
        public Project this[int i] => Projects[i]; 
        public int Count => projectDict.Count;
        #endregion

        #region Constructor
        public ProjectCollection(string baseDir, ScriptCache scriptCache)
        {
            this.baseDir = baseDir;
            this.projectRoot = Path.Combine(baseDir, "Projects");
            this.scriptCache = scriptCache;
        }
        #endregion

        public int PrepareLoad(out int processCount)
        {
            // Ex) projNameList = { "Win7PESE", "Win8PESE", "Win8.1SE", "Win10PESE" }
            // Ex) scriptPathDict = [script paths of Win7PESE, script paths of Win8PESE, ... ]
            List<string> projNameList = GetProjectNameList();
            GetScriptPaths(projNameList, out processCount);

            // Return count of all scripts
            return allScriptPaths.Count + allDirLinkPaths.Count;
        }

        /// <summary>
        /// Get project names
        /// </summary>
        /// <returns></returns>
        public List<string> GetProjectNameList()
        {
            if (Directory.Exists(projectRoot))
            {
                string[] projArray = Directory.GetDirectories(projectRoot);
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

        /// <summary>
        /// Get scriptPathDict and allScriptPathList
        /// </summary>
        /// <param name="projNameList"></param>
        public int GetScriptPaths(List<string> projNameList, out int linkCount)
        {
            int allCount = 0;
            linkCount = 0;
            foreach (string projName in projNameList)
            {
                string projectDir = Path.Combine(projectRoot, projName);

                string rootScript = Path.Combine(projectDir, "script.project");
                string[] scripts = Directory.GetFiles(projectDir, "*.script", SearchOption.AllDirectories);
                string[] links = Directory.GetFiles(projectDir, "*.link", SearchOption.AllDirectories);

                List<string> scriptPathList = new List<string>();
                scriptPathList.Add(rootScript);
                scriptPathList.AddRange(scripts);
                scriptPathList.AddRange(links);

                allCount += scriptPathList.Count;
                linkCount += links.Length; // links should be added twice since they are processed twice

                scriptPathDict[projName] = scriptPathList;
                allScriptPaths.AddRange(scriptPathList);

                // Temporary disable folder.script, will fix it later
                // List<string> dirLinkPathList = GetFolderLinks(projectDir);
                List<string> dirLinkPathList = new List<string>();
                dirLinkPathDict[projName] = dirLinkPathList;
                allDirLinkPaths.AddRange(dirLinkPathList);
            }
            return allCount;
        }

        private List<string> GetFolderLinks(string projectDir)
        {
            List<string> dirLinkPathList = new List<string>();

            var links = Directory.EnumerateFiles(projectDir, "folder.project", SearchOption.AllDirectories);
            foreach (string link in links.Where(x => Ini.SectionExists(x, "Links")))
            {
                var paths = Ini.ParseRawSection(link, "Links").Select(x => x.Trim()).Where(x => x.Length != 0);
                foreach (string path in paths)
                {
                    if (Path.IsPathRooted(path))
                    { // Full Path
                        var files = Directory.EnumerateFiles(path, "*.script", SearchOption.AllDirectories);
                        dirLinkPathList.AddRange(files.Select(x => x.Substring(path.Length + 1)));

                        /*
                        string fileName = Path.GetFileName(path);
                        if (StringHelper.IsWildcard(fileName))
                            */
                    }
                    else
                    { // Relative Path to %BaseDir%
                        string dirPath = path;
                        if (StringHelper.IsWildcard(Path.GetFileName(path)))
                            dirPath = Path.GetDirectoryName(path);
                        string fullPath = Path.Combine(baseDir, dirPath);

                        var files = Directory.EnumerateFiles(fullPath, "*.script", SearchOption.AllDirectories);
                        dirLinkPathList.AddRange(files.Select(x => x.Substring(fullPath.Length + 1)));
                    }

                    /*
                    string linkDirPath = Path.Combine(baseDir, Path.GetDirectoryName(path));
                    string linkWildcard = Path.GetFileName(path);

                    if (Directory.Exists(linkDirPath) && StringHelper.IsWildcard(linkWildcard))
                    {
                        string[] dirLinks = Directory.GetFiles(linkDirPath, linkWildcard, SearchOption.AllDirectories);
                        var dirScriptLinks = dirLinks.Where(p => Path.GetExtension(p).Equals(".script", StringComparison.OrdinalIgnoreCase));
                        dirLinkPathList.AddRange(dirScriptLinks);
                    }
                    */
                }
            }

            return dirLinkPathList;
        }

        public List<LogInfo> Load(BackgroundWorker worker)
        {
            List<LogInfo> logs = new List<LogInfo>(32);
            try
            {
                foreach (var kv in scriptPathDict)
                {
                    Project project = new Project(baseDir, kv.Key);

                    // Load scripts
                    List<LogInfo> projLogs = project.Load(kv.Value, dirLinkPathDict[kv.Key], scriptCache, worker);
                    logs.AddRange(projLogs);

                    // Add Project.Scripts to ProjectCollections.Scripts
                    this.allProjectScripts.AddRange(project.AllScripts);

                    projectDict[kv.Key] = project;
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

            var links = allProjectScripts.Where(x => x.Type == ScriptType.Link);
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
                            if (scriptCache != null)
                            { // Case of ScriptCache enabled
                                FileInfo f = new FileInfo(linkFullPath);
                                DB_ScriptCache pCache = cacheDB.FirstOrDefault(x => x.Hash == linkPath.GetHashCode());
                                if (pCache != null &&
                                    pCache.Path.Equals(linkPath, StringComparison.Ordinal) &&
                                    DateTime.Equals(pCache.LastWriteTimeUtc, f.LastWriteTimeUtc) &&
                                    pCache.FileSize == f.Length)
                                {
                                    try
                                    {
                                        using (MemoryStream memStream = new MemoryStream(pCache.Serialized))
                                        {
                                            BinaryFormatter formatter = new BinaryFormatter();
                                            link = (Script)formatter.Deserialize(memStream);
                                        }
                                        link.Project = p.Project;
                                        link.IsDirLink = false;
                                        cached = 3;
                                    }
                                    catch { }
                                }
                            }

                            if (link == null)
                            {
                                // TODO : Lazy loading of link, takes too much time at start
                                string ext = Path.GetExtension(linkFullPath);
                                ScriptType type = ScriptType.Script;
                                if (ext.Equals(".link", StringComparison.OrdinalIgnoreCase))
                                    type = ScriptType.Link;
                                link = new Script(type, Path.Combine(baseDir, linkFullPath), p.Project, projectRoot, null, false, false, false);

                                Debug.Assert(p != null);
                            }

                            // Check Script Link's validity
                            // Also, convert nested link to one-depth link
                            if (link == null)
                                break;

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
                        if (worker != null)
                            worker.ReportProgress(cached, Path.GetDirectoryName(p.ShortPath));
                    }
                    else // Error
                    {
                        int idx = allProjectScripts.IndexOf(p);
                        removeIdxs.Add(idx);
                        if (worker != null)
                            worker.ReportProgress(cached);
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
    }
    #endregion

    #region Project
    public class Project : ICloneable
    {
        #region Fields and Properties
        // Fields
        private readonly string projectName;
        private readonly string projectRoot;
        private readonly string projectDir;
        private readonly string baseDir;
        private int mainScriptIdx;
        private List<Script> allScripts;
        private Variables variables;

        private int loadedScriptCount;
        private int allScriptCount;

        // Properties
        public string ProjectName => projectName;
        public string ProjectRoot => projectRoot;
        public string ProjectDir => projectDir;
        public string BaseDir => baseDir;
        public Script MainScript => allScripts[mainScriptIdx];
        public List<Script> AllScripts => allScripts;
        public List<Script> ActiveScripts => CollectActiveScripts(allScripts);
        public List<Script> VisibleScripts => CollectVisibleScripts(allScripts);
        public Variables Variables { get => variables; set => variables = value; }
        public int LoadedScriptCount => loadedScriptCount; 
        public int AllScriptCount => allScriptCount;
        #endregion

        #region Constructor
        public Project(string baseDir, string projectName)
        {
            this.loadedScriptCount = 0;
            this.allScriptCount = 0;
            this.projectName = projectName;
            this.projectRoot = Path.Combine(baseDir, "Projects");
            this.projectDir = Path.Combine(baseDir, "Projects", projectName);
            this.baseDir = baseDir;
        }
        #endregion

        #region Load Scripts
        public List<LogInfo> Load(List<string> allScriptPathList, List<string> allDirLinkPathList, ScriptCache scriptCache, BackgroundWorker worker)
        {
            List<LogInfo> logs = new List<LogInfo>(32);

            ReaderWriterLockSlim listLock = new ReaderWriterLockSlim();
            string mainScriptPath = Path.Combine(projectDir, "script.project");
            allScripts = new List<Script>();

            // Doing this will consume memory, but also greatly increase performance.
            DB_ScriptCache[] cacheDB = null;
            if (scriptCache != null)
                cacheDB = scriptCache.Table<DB_ScriptCache>().Where(x => true).ToArray();

            // Item2 is IsDirLink
            // true -> dirLink, false -> script or scriptLink
            List<Tuple<string, bool>> pTupleList = new List<Tuple<string, bool>>();
            pTupleList.AddRange(allScriptPathList.Select(x => new Tuple<string, bool>(x, false)));
            pTupleList.AddRange(allDirLinkPathList.Select(x => new Tuple<string, bool>(x, true)));

            // Load scripts from disk or cache
            Task[] tasks = pTupleList.Select(pTuple =>
            {
                return Task.Run(() =>
                {
                    int cached = 0;
                    string pPath = pTuple.Item1;
                    bool isDirLink = pTuple.Item2;
                    Script p = null;
                    try
                    {
                        if (scriptCache != null)
                        { // ScriptCache enabled
                            FileInfo f = new FileInfo(pPath);
                            string sPath = pPath.Remove(0, baseDir.Length + 1); // 1 for \
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

                                        p = formatter.Deserialize(memStream) as Script;
                                        p.Project = this;
                                        p.IsDirLink = isDirLink;
                                        cached = 1;
                                    }
                                }
                                catch { } // Cache Error
                            }
                        }

                        if (p == null)
                        { // Cache Miss
                            bool isMainScript = pPath.Equals(mainScriptPath, StringComparison.Ordinal);

                            // TODO : Lazy loading of link, takes too much time at start
                            string ext = Path.GetExtension(pPath);
                            if (ext.Equals(".link", StringComparison.OrdinalIgnoreCase))
                                p = new Script(ScriptType.Link, pPath, this, projectRoot, null, isMainScript, false, false);
                            else
                                p = new Script(ScriptType.Script, pPath, this, projectRoot, null, isMainScript, false, isDirLink);

                            Debug.Assert(p != null);
                        }

                        listLock.EnterWriteLock();
                        try
                        {
                            allScripts.Add(p);
                        }
                        finally
                        {
                            listLock.ExitWriteLock();
                        }

                        worker?.ReportProgress(cached, Path.GetDirectoryName(p.ShortPath));
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
            this.allScripts = InternalSortScripts(allScripts);
            SetMainScriptIdx();

            this.Variables = new Variables(this);
        }

        private List<Script> InternalSortScripts(IEnumerable<Script> pList)
        {
            Tree<Script> pTree = new Tree<Script>();
            Dictionary<string, int> dirDict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            int rootId = pTree.AddNode(0, this.MainScript); // Root is script.project

            foreach (Script p in pList)
            {
                Debug.Assert(p != null);

                if (p.IsMainScript)
                    continue;

                int nodeId = rootId;
                string[] paths = p.ShortPath
                    .Substring(this.projectName.Length + 1)
                    .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                // Ex) Apps\Network\Mozilla_Firefox_CR.script
                for (int i = 0; i < paths.Length - 1; i++)
                {
                    string pathKey = PathKeyGenerator(paths, i);
                    string key = p.Level.ToString() + pathKey;
                    if (dirDict.ContainsKey(key))
                    {
                        nodeId = dirDict[key];
                    }
                    else
                    {
                        string fullPath = Path.Combine(projectRoot, projectName, pathKey);
                        Script dirScript = new Script(ScriptType.Directory, fullPath, this, projectRoot, p.Level, false, false, false);
                        nodeId = pTree.AddNode(nodeId, dirScript);
                        dirDict[key] = nodeId;
                    }
                }
                Debug.Assert(p != null);
                pTree.AddNode(nodeId, p);
            }

            // Sort - Script first, Directory last
            pTree.Sort((x, y) =>
            {
                if (x.Data.Level == y.Data.Level)
                {
                    if (x.Data.Type == ScriptType.Directory)
                    {
                        if (y.Data.Type == ScriptType.Directory)
                            return string.Compare(x.Data.FullPath, y.Data.FullPath, StringComparison.Ordinal);
                        else
                            return 1;
                    }
                    else
                    {
                        if (y.Data.Type == ScriptType.Directory)
                            return -1;
                        else
                            return string.Compare(x.Data.FullPath, y.Data.FullPath, StringComparison.Ordinal);
                    }
                }
                else
                {
                    return x.Data.Level - y.Data.Level;
                }
            });

            List<Script> newList = new List<Script>();
            foreach (Script p in pTree)
            {
                if (p.Type != ScriptType.Directory)
                    newList.Add(p);
            }

            return newList;
        }

        public void SetMainScriptIdx()
        {
            mainScriptIdx = allScripts.FindIndex(x => x.IsMainScript);
            Debug.Assert(allScripts.Count(x => x.IsMainScript) == 1);
            Debug.Assert(mainScriptIdx != -1);
        }
        #endregion

        public Script RefreshScript(Script script, EngineState s = null)
        {
            if (script == null) throw new ArgumentNullException(nameof(script));

            string pPath = script.FullPath;
            int aIdx = AllScripts.FindIndex(x => x.FullPath.Equals(pPath, StringComparison.OrdinalIgnoreCase));

            Script p = null;
            if (aIdx == -1)
            {
                // Even if idx is not found in Projects directory, just proceed.
                // If not, cannot deal with monkey-patched scripts.
                p = LoadScript(pPath, true, script.IsDirLink);
            }
            else
            {
                // This one is in legit Project list, so [Main] cannot be ignored
                p = LoadScript(pPath, false, script.IsDirLink);
                if (p != null)
                {
                    allScripts[aIdx] = p;
                    if (s != null)
                    {
                        // Investigate EngineState to update it on build list
                        int sIdx = s.Scripts.FindIndex(x => x.FullPath.Equals(pPath, StringComparison.OrdinalIgnoreCase));
                        if (sIdx != -1)
                            s.Scripts[sIdx] = p;
                    }
                }
            }
            return p;
        }

        /// <summary>
        /// Load scripts into project while running
        /// Return true if error
        /// </summary>
        public Script LoadScriptMonkeyPatch(string pFullPath, bool addToProjectTree = false, bool ignoreMain = false)
        {
            // Limit: fullPath must be in BaseDir
            if (pFullPath.StartsWith(this.baseDir, StringComparison.OrdinalIgnoreCase) == false)
                return null;

            Script p = LoadScript(pFullPath, ignoreMain, false);
            if (addToProjectTree)
            {
                allScripts.Add(p);
                allScriptCount += 1;
            }

            return p;
        }

        public Script LoadScript(string pPath, bool ignoreMain, bool isDirLink)
        {
            Script p;
            try
            {
                if (pPath.Equals(Path.Combine(projectRoot, "script.project"), StringComparison.OrdinalIgnoreCase))
                {
                    p = new Script(ScriptType.Script, pPath, this, projectRoot, 0, true, ignoreMain, isDirLink);
                }
                else
                {
                    string ext = Path.GetExtension(pPath);
                    if (ext.Equals(".link", StringComparison.OrdinalIgnoreCase))
                        p = new Script(ScriptType.Link, pPath, this, projectRoot, null, false, false, false);
                    else
                        p = new Script(ScriptType.Script, pPath, this, projectRoot, null, false, ignoreMain, isDirLink);
                }

                // Check Script Link's validity
                // Also, convert nested link to one-depth link
                if (p.Type == ScriptType.Link)
                {
                    Script link = p.Link;
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
                        p.Link = link;
                    else
                        return null;
                }
            }
            catch
            { // Do nothing - intentionally left blank
                return null;
            }

            return p;
        }
        #endregion

        #region Active, Visible Scripts
        private List<Script> CollectVisibleScripts(List<Script> allScriptList)
        {
            List<Script> visibleScriptList = new List<Script>();
            foreach (Script p in allScriptList)
            {
                if (0 < p.Level)
                    visibleScriptList.Add(p);
            }
            return visibleScriptList;
        }

        private List<Script> CollectActiveScripts(List<Script> allPlugist)
        {
            List<Script> activeScripts = new List<Script>(allPlugist.Count)
            {
                MainScript
            };

            foreach (Script p in allPlugist.Where(x => !x.IsMainScript && (0 < x.Level)))
            {
                bool active = false;
                if (p.Type == ScriptType.Script || p.Type == ScriptType.Link)
                {                   
                    if (p.Selected != SelectedState.None)
                    {
                        if (p.Mandatory || p.Selected == SelectedState.True)
                            active = true;
                    }
                }

                if (active)
                    activeScripts.Add(p);
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
        public Script GetScriptByFullPath(string pFullPath)
        {
            return AllScripts.Find(x => x.FullPath.Equals(pFullPath, StringComparison.OrdinalIgnoreCase));
        }

        public Script GetScriptByShortPath(string pShortPath)
        {
            return AllScripts.Find(x => x.ShortPath.Equals(pShortPath, StringComparison.OrdinalIgnoreCase));
        }

        public bool ContainsScriptByFullPath(string pFullPath)
        {
            return (AllScripts.FindIndex(x => x.FullPath.Equals(pFullPath, StringComparison.OrdinalIgnoreCase)) != -1);
        }

        public bool ContainsScriptByShortPath(string pShortPath)
        {
            return (AllScripts.FindIndex(x => x.ShortPath.Equals(pShortPath, StringComparison.OrdinalIgnoreCase)) != -1);
        }
        #endregion

        #region Variables
        public void UpdateProjectVariables()
        {
            if (variables != null)
            {
                if (MainScript.Sections.ContainsKey("Variables"))
                    variables.AddVariables(VarsType.Global, MainScript.Sections["Variables"]);
            }
        }
        #endregion

        #region Clone
        public object Clone()
        {
            Project project = new Project(baseDir, projectName)
            {
                mainScriptIdx = this.mainScriptIdx,
                allScripts = new List<Script>(this.allScripts),
                variables = this.variables.Clone() as Variables,
                loadedScriptCount = this.loadedScriptCount,
                allScriptCount = this.allScriptCount,
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
            if (project == null) throw new ArgumentNullException("project");

            if (projectName.Equals(project.ProjectName, StringComparison.OrdinalIgnoreCase) &&
                projectRoot.Equals(project.ProjectRoot, StringComparison.OrdinalIgnoreCase) &&
                projectDir.Equals(project.ProjectDir, StringComparison.OrdinalIgnoreCase) &&
                allScriptCount == project.AllScriptCount)
                return true;
            else
                return false;

        }

        public override int GetHashCode()
        {
            return projectName.GetHashCode() ^ projectRoot.GetHashCode() ^ projectDir.GetHashCode() ^ allScriptCount.GetHashCode();
        }
        #endregion
    }
    #endregion
}
