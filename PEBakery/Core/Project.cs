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

using PEBakery.Helper;
using PEBakery.Ini;
using PEBakery.Tree;
using SQLite;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PEBakery.Core
{
    #region ProjectCollection
    public class ProjectCollection : IReadOnlyCollection<Project>
    {
        #region Static Fields
        public static bool AsteriskBugDirLink = false;
        #endregion

        #region Fields
        private readonly string _baseDir;
        private readonly Dictionary<string, Project> _projectDict = new Dictionary<string, Project>(StringComparer.Ordinal);
        private readonly ScriptCache _scriptCache;

        private readonly Dictionary<string, List<ScriptParseInfo>> _spiDict = new Dictionary<string, List<ScriptParseInfo>>();
        private readonly List<Script> _allProjectScripts = new List<Script>();
        private readonly List<ScriptParseInfo> _allScriptPaths = new List<ScriptParseInfo>();
        #endregion

        #region Properties
        public string ProjectRoot { get; }
        public List<Project> ProjectList => _projectDict.Values.OrderBy(x => x.ProjectName).ToList();
        public List<string> ProjectNames => _projectDict.Keys.OrderBy(x => x).ToList();
        public Project this[int i] => ProjectList[i];
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
        public (int TotalCount, int LinkCount) PrepareLoad()
        {
            // Ex) projNameList = { "ChrisPE", "MistyPE", "Win10PESE" }
            // Ex) scriptPathDict = [script paths of ChrisPE, script paths of MistyPE, ... ]
            List<string> projectNames = GetProjectNames();
            (_, int linkCount) = GetScriptPaths(projectNames);

            // Return count of all scripts (all .script + dir link)
            return (_allScriptPaths.Count, linkCount);
        }
        #endregion

        #region GetProjectNames
        /// <summary>
        /// Get project names
        /// </summary>
        /// <returns></returns>
        public List<string> GetProjectNames()
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
        public (int AllCount, int LinkCount) GetScriptPaths(List<string> projectNames)
        {
            int allCount = 0;
            int linkCount = 0;
            foreach (string projectName in projectNames)
            {
                string projectDir = Path.Combine(ProjectRoot, projectName);

                // Path of root script
                ScriptParseInfo rootScript = new ScriptParseInfo
                {
                    RealPath = Path.Combine(projectDir, "script.project"),
                    TreePath = Path.Combine(projectName, "script.project"),
                    IsDir = false,
                    IsDirLink = false,
                };

                // Path of normal and link scripts
                ScriptParseInfo[] scripts = FileHelper
                    .GetFilesExWithDirs(projectDir, "*.script", SearchOption.AllDirectories)
                    .Select(x => new ScriptParseInfo
                    {
                        RealPath = x.Path,
                        TreePath = x.Path.Substring(ProjectRoot.Length + 1),
                        IsDir = x.IsDir,
                        IsDirLink = false,
                    }).ToArray();
                ScriptParseInfo[] links = FileHelper
                    .GetFilesExWithDirs(projectDir, "*.link", SearchOption.AllDirectories)
                    .Select(x => new ScriptParseInfo
                    {
                        RealPath = x.Path,
                        TreePath = x.Path.Substring(ProjectRoot.Length + 1),
                        IsDir = x.IsDir,
                        IsDirLink = false,
                    }).ToArray();

                // Path of directory-linked scripts
                List<ScriptParseInfo> dirLinks = GetDirLinks(projectDir);

                int spiCount = 1 + scripts.Length + links.Length + dirLinks.Count;
                List<ScriptParseInfo> spis = new List<ScriptParseInfo>(spiCount) { rootScript };
                spis.AddRange(scripts);
                spis.AddRange(links);
                spis.AddRange(dirLinks);

                spis = spis
                    // Exclude %BaseDir%\Projects\{ProjectName} directory
                    .Where(x => !(x.RealPath.Equals(projectDir, StringComparison.OrdinalIgnoreCase) && x.IsDir))
                    // Filter duplicated scripts (such as directory covered both by scripts/links and dirLinks 
                    .Distinct(new ScriptParseInfoComparer())
                    // This sort is for more natural loading messages
                    // It should be sorted one more time later, using KwayTree
                    .OrderBy(x => x.RealPath, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                _spiDict[projectName] = spis;
                _allScriptPaths.AddRange(spis);

                // For stage2, links should be added twice since processed twice
                allCount += spis.Count;
                linkCount += spis.Count(x => x.RealPath.EndsWith(".link", StringComparison.OrdinalIgnoreCase));
            }
            return (allCount, linkCount);
        }
        #endregion

        #region GetDirLinks
        private List<ScriptParseInfo> GetDirLinks(string projectDir)
        {
            var dirLinks = new List<ScriptParseInfo>();
            var linkFiles = Directory.EnumerateFiles(projectDir, "folder.project", SearchOption.AllDirectories);
            foreach (string linkFile in linkFiles)
            {
                Debug.Assert(linkFile != null, $"{nameof(linkFile)} is wrong");
                Debug.Assert(linkFile.StartsWith(_baseDir), $"{nameof(linkFile)} [{linkFile}] does not start with %BaseDir%");

                List<string> rawPaths = IniUtil.ParseRawSection(linkFile, "Links");
                if (rawPaths == null) // No [Links] section -> do not process
                    continue;

                // TreePath of directory where folder.project exists
                string prefix = Path.GetDirectoryName(linkFile.Substring(ProjectRoot.Length + 1)); // +1 for \
                Debug.Assert(prefix != null, $"Wrong prefix of [{linkFile}]");

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
                                Global.Logger.SystemWrite(new LogInfo(LogState.Error, $"Unable to find path [{dirPath}] for directory link"));
                                continue;
                            }

                            string[] subDirs = Directory.GetDirectories(dirPath);
                            foreach (string subDir in subDirs)
                            {
                                Debug.Assert(subDir != null, "Internal Logic Error at ProjectCollection.GetDirLinks");

                                var infos = FileHelper.GetFilesExWithDirs(subDir, "*.script", SearchOption.AllDirectories)
                                    .Select(x => new ScriptParseInfo
                                    {
                                        RealPath = x.Path,
                                        TreePath = Path.Combine(prefix, Path.GetFileName(subDir), x.Path.Substring(subDir.Length).TrimStart('\\')),
                                        IsDir = x.IsDir,
                                        IsDirLink = true,
                                    });
                                dirLinks.AddRange(infos);
                            }
                        }
                        else
                        { // Relative to %BaseDir%
                            string fullPath = Path.Combine(_baseDir, dirPath);
                            if (!Directory.Exists(fullPath))
                            {
                                Global.Logger.SystemWrite(new LogInfo(LogState.Error, $"Unable to find path [{fullPath}] for directory link"));
                                continue;
                            }

                            string[] subDirs = Directory.GetDirectories(fullPath);
                            foreach (string subDir in subDirs)
                            {
                                Debug.Assert(subDir != null, "Internal Logic Error at ProjectCollection.GetDirLinks");

                                var infos = FileHelper.GetFilesExWithDirs(subDir, "*.script", SearchOption.AllDirectories)
                                    .Select(x => new ScriptParseInfo
                                    {
                                        RealPath = x.Path,
                                        TreePath = Path.Combine(prefix, Path.GetFileName(subDir), x.Path.Substring(subDir.Length).TrimStart('\\')),
                                        IsDir = x.IsDir,
                                        IsDirLink = true,
                                    });
                                dirLinks.AddRange(infos);
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
                                Global.Logger.SystemWrite(new LogInfo(LogState.Error, $"Unable to find path [{dirPath}] for directory link"));
                                continue;
                            }

                            var infos = FileHelper.GetFilesExWithDirs(dirPath, "*.script", SearchOption.AllDirectories)
                                .Select(x => new ScriptParseInfo
                                {
                                    RealPath = x.Path,
                                    TreePath = Path.Combine(prefix, Path.GetFileName(dirPath), x.Path.Substring(dirPath.Length).TrimStart('\\')),
                                    IsDir = x.IsDir,
                                    IsDirLink = true,
                                });
                            dirLinks.AddRange(infos);
                        }
                        else
                        { // Relative to %BaseDir%
                            Debug.Assert(dirPath != null, "Internal Logic Error at ProjectCollection.GetDirLinks");

                            string fullPath = Path.Combine(_baseDir, dirPath);
                            if (!Directory.Exists(fullPath))
                            {
                                Global.Logger.SystemWrite(new LogInfo(LogState.Error, $"Unable to find path [{fullPath}] for directory link"));
                                continue;
                            }

                            var infos = FileHelper.GetFilesExWithDirs(dirPath, "*.script", SearchOption.AllDirectories)
                                .Select(x => new ScriptParseInfo
                                {
                                    RealPath = x.Path,
                                    TreePath = Path.Combine(prefix, Path.GetFileName(dirPath), x.Path.Substring(fullPath.Length).TrimStart('\\')),
                                    IsDir = x.IsDir,
                                    IsDirLink = true,
                                });
                            dirLinks.AddRange(infos);
                        }
                    }
                }
            }

            return dirLinks;
        }
        #endregion

        #region Load, LoadLinks
        public List<LogInfo> Load(IProgress<(Project.LoadReport Type, string Path)> progress)
        {
            List<LogInfo> logs = new List<LogInfo>(32);
            try
            {
                foreach (string key in _spiDict.Keys)
                {
                    Project project = new Project(_baseDir, key);

                    // Load scripts
                    List<ScriptParseInfo> spis = _spiDict[key];
                    List<LogInfo> errLogs = project.Load(spis, _scriptCache, progress);
                    logs.AddRange(errLogs);

                    // Add Project.Scripts to ProjectCollections.Scripts
                    _allProjectScripts.AddRange(project.AllScripts);

                    _projectDict[key] = project;
                }

                // Populate *.link scripts
                List<LogInfo> linkLogs = LoadLinks(progress);
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

        private List<LogInfo> LoadLinks(IProgress<(Project.LoadReport Type, string Path)> progress)
        {
            List<LogInfo> logs = new List<LogInfo>(32);
            List<int> removeIdxs = new List<int>();

            // Doing this will consume memory, but also greatly increase performance.
            DB_ScriptCache[] cachePool = null;
            if (_scriptCache != null)
            {
                progress?.Report((Project.LoadReport.LoadingCache, null));
                cachePool = _scriptCache.Table<DB_ScriptCache>().ToArray();
            }

            string CheckLinkPath(Script sc, string linkRawPath)
            {
                // PEBakery's TreePath strips "%BaseDir%\Project" (ProjectRoot) from RealPath.
                // WinBuilder's .link file's link= path strips "%BaseDir%" from RealPath.
                // BE CAREFUL ABOUT THIS DIFFERENCE WHEN HANDLING LINK PATHS!

                string linkRealPath;
                if (Path.IsPathRooted(linkRawPath))
                { // Full Path
                    // Ex) link=E:\Link\HelloWorld.script
                    linkRealPath = linkRawPath;
                }
                else
                { // Non-rooted path
                    // Ex) link=Projects\TestSuite\Downloads\HelloWorld.script
                    /*
                    string linkRealPath = Path.Combine(_baseDir, linkRawPath);
                    string linkTreePath = linkRawPath.Substring("Projects".Length + 1);
                    if (!linkRawPath.StartsWith("Projects"))
                    {
                        Global.Logger.SystemWrite(new LogInfo(LogState.Error, $"Link [{sc.TreePath}] has invalid link path [{linkRawPath}]"));
                        return (null, null);
                    }
                    */

                    // Ex) link=Projects\TestSuite\Downloads\HelloWorld.script
                    linkRealPath = Path.Combine(_baseDir, linkRawPath);
                }

                // Unable to find 
                if (!File.Exists(linkRealPath))
                {
                    Global.Logger.SystemWrite(new LogInfo(LogState.Error, $"Link [{sc.TreePath}]'s link path [{linkRawPath}] cannot be found in disk"));
                    return null;
                }

                return linkRealPath;
            }

            bool cacheValid = true;
            Script[] linkSources = _allProjectScripts.Where(x => x.Type == ScriptType.Link).ToArray();
            Debug.Assert(linkSources.Count(x => x.IsDirLink) == 0);
            Parallel.ForEach(linkSources, sc =>
            {
                Script linkTarget = null;
                bool valid = false;
                Project.LoadReport cached = Project.LoadReport.Stage2;
                try
                {
                    do
                    {
                        string linkRawPath = sc.Sections["Main"].IniDict["Link"];
                        string linkRealPath = CheckLinkPath(sc, linkRawPath);
                        if (linkRealPath == null)
                            return;

                        // Load .link's linked scripts with cache
                        if (cachePool != null && cacheValid)
                        { // Case of ScriptCache enabled
                            (linkTarget, cacheValid) = ScriptCache.DeserializeScript(linkRealPath, cachePool);
                            if (linkTarget != null)
                            {
                                linkTarget.Project = sc.Project;
                                linkTarget.IsDirLink = false;
                                cached = Project.LoadReport.Stage2Cached;
                            }
                        }

                        if (linkTarget == null)
                        {
                            // TODO : Lazy loading of link, takes too much time at start
                            ScriptType type = ScriptType.Script;
                            string ext = Path.GetExtension(linkRealPath);
                            if (ext.Equals(".link", StringComparison.OrdinalIgnoreCase))
                                type = ScriptType.Link;
                            string fullPath = Path.Combine(_baseDir, linkRealPath);
                            linkTarget = new Script(type, fullPath, string.Empty, sc.Project, null, false, false, false);

                            Debug.Assert(sc != null);
                        }

                        // Convert nested link to one-depth link
                        if (linkTarget.Type == ScriptType.Script)
                        {
                            valid = true;
                            break;
                        }
                        linkTarget = linkTarget.Link;
                    }
                    while (linkTarget.Type != ScriptType.Script);
                }
                catch (Exception e)
                { // Parser Error
                    logs.Add(new LogInfo(LogState.Error, Logger.LogExceptionMessage(e)));
                }

                if (valid)
                {
                    sc.LinkLoaded = true;
                    sc.Link = linkTarget;
                    progress?.Report((cached, Path.GetDirectoryName(sc.TreePath)));
                }
                else // Error
                {
                    int idx = _allProjectScripts.IndexOf(sc);
                    removeIdxs.Add(idx);
                    progress?.Report((cached, null));
                }
            });

            // Remove malformed link
            var idxs = removeIdxs.OrderByDescending(x => x);
            foreach (int idx in idxs)
                _allProjectScripts.RemoveAt(idx);

            return logs;
        }
        #endregion

        #region GetEnumarator
        public IEnumerator<Project> GetEnumerator() => _projectDict.OrderBy(x => x.Key).Select(x => x.Value).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        #endregion
    }
    #endregion

    #region Project
    public class Project : IEquatable<Project>
    {
        #region Fields
        private int _mainScriptIdx;
        #endregion

        #region Properties
        public string ProjectName { get; }
        public string BaseDir { get; }
        public string ProjectRoot { get; } // {BaseDir}\Projects
        public string ProjectDir { get; } // {BaseDir}\Projects\{ProjectDirName}
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

        #region Load
        public enum LoadReport
        {
            None,
            FindingScript,
            LoadingCache,
            Stage1,
            Stage1Cached,
            Stage2,
            Stage2Cached,
        }

        public List<LogInfo> Load(List<ScriptParseInfo> spis, ScriptCache scriptCache, IProgress<(LoadReport Type, string Path)> progress)
        {
            List<LogInfo> logs = new List<LogInfo>(32);

            string mainScriptPath = Path.Combine(ProjectDir, "script.project");
            AllScripts = new List<Script>();

            DB_ScriptCache[] cachePool = null;
            if (scriptCache != null)
            {
                progress?.Report((LoadReport.LoadingCache, null));
                cachePool = scriptCache.Table<DB_ScriptCache>().ToArray();
            }

            // Load scripts from disk or cache
            bool cacheValid = true;
            object listLock = new object();
            Parallel.ForEach(spis, spi =>
            {
                Debug.Assert(spi.RealPath != null, "Internal Logic Error at Project.Load");
                Debug.Assert(spi.TreePath != null, "Internal Logic Error at Project.Load");

                LoadReport cached = LoadReport.Stage1;
                Script sc = null;
                try
                {
                    if (cachePool != null && cacheValid && !spi.IsDir)
                    { // ScriptCache enabled (disabled in Directory script)
                        (sc, cacheValid) = ScriptCache.DeserializeScript(spi.RealPath, cachePool);
                        if (sc != null)
                        {
                            sc.Project = this;
                            sc.IsDirLink = spi.IsDirLink;
                            cached = LoadReport.Stage1Cached;
                        }
                    }

                    if (sc == null)
                    { // Cache Miss
                        bool isMainScript = spi.RealPath.Equals(mainScriptPath, StringComparison.OrdinalIgnoreCase);

                        // TODO : Lazy loading of link, takes too much time at start
                        // Directory scripts will not be directly used (so level information is dummy)
                        // They are mainly used to store RealPath and TreePath information.
                        if (spi.IsDir) // level information is empty, will be modified in InternalSortScripts
                            sc = new Script(ScriptType.Directory, spi.RealPath, spi.TreePath, this, null, false, false, spi.IsDirLink);
                        else if (Path.GetExtension(spi.TreePath).Equals(".link", StringComparison.OrdinalIgnoreCase))
                            sc = new Script(ScriptType.Link, spi.RealPath, spi.TreePath, this, null, isMainScript, false, false);
                        else
                            sc = new Script(ScriptType.Script, spi.RealPath, spi.TreePath, this, null, isMainScript, false, spi.IsDirLink);

                        Debug.Assert(sc != null);
                    }

                    lock (listLock)
                        AllScripts.Add(sc);

                    progress?.Report((cached, Path.GetDirectoryName(sc.TreePath)));
                }
                catch (Exception e)
                {
                    logs.Add(new LogInfo(LogState.Error, Logger.LogExceptionMessage(e)));
                    progress?.Report((cached, null));
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
            Variables = new Variables(this, Global.Setting.ExportVariablesOptions());
        }

        public void SortAllScripts()
        {
            AllScripts = InternalSortScripts(AllScripts);
            SetMainScriptIdx();
        }

        private List<Script> InternalSortScripts(List<Script> scripts)
        {
            KwayTree<Script> scTree = new KwayTree<Script>();
            Dictionary<string, int> dirDict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            int rootId = scTree.AddNode(0, MainScript); // Root is script.project

            foreach (Script sc in scripts.Where(x => x.Type != ScriptType.Directory))
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
                        // Find ts, a Script instance of directory
                        string treePath = Path.Combine(ProjectName, pathKey);
                        Script ts = scripts.FirstOrDefault(x => x.TreePath.Equals(treePath, StringComparison.OrdinalIgnoreCase));
                        Debug.Assert(ts != null, "Internal Logic Error at InternalSortScripts");

                        Script dirScript = new Script(ScriptType.Directory, ts.RealPath, ts.TreePath, this, sc.Level, false, false, ts.IsDirLink);
                        nodeId = scTree.AddNode(nodeId, dirScript);
                        dirDict[key] = nodeId;
                    }
                }

                scTree.AddNode(nodeId, sc);
            }

            // Sort - Script first, Directory last
            scTree.Sort((x, y) =>
            {
                if (x.Data.Level == y.Data.Level)
                {
                    if (x.Data.Type == ScriptType.Directory)
                    {
                        if (y.Data.Type == ScriptType.Directory)
                            return string.Compare(x.Data.RealPath, y.Data.RealPath, StringComparison.InvariantCultureIgnoreCase);
                        else
                            return 1;
                    }
                    else
                    {
                        if (y.Data.Type == ScriptType.Directory)
                            return -1;
                        else
                            return string.Compare(x.Data.RealPath, y.Data.RealPath, StringComparison.InvariantCultureIgnoreCase);
                    }
                }
                else
                {
                    return x.Data.Level - y.Data.Level;
                }
            });

            List<Script> newList = new List<Script>();
            foreach (Script sc in scTree)
                newList.Add(sc);

            return newList;
        }

        public void SetMainScriptIdx()
        {
            _mainScriptIdx = AllScripts.FindIndex(x => x.IsMainScript);
            Debug.Assert(AllScripts.Count(x => x.IsMainScript) == 1, $"[{AllScripts.Count(x => x.IsMainScript)}] MainScript reported instead of [1]");
            Debug.Assert(_mainScriptIdx != -1, $"Unable to find MainScript of [{ProjectName}]");
        }
        #endregion

        #region RefreshScript
        public Script RefreshScript(Script sc, EngineState s = null)
        {
            if (sc == null)
                throw new ArgumentNullException(nameof(sc));
            if (sc.Type == ScriptType.Directory)
                return null;

            int aIdx = AllScripts.FindIndex(x => x.RealPath.Equals(sc.RealPath, StringComparison.OrdinalIgnoreCase));
            if (aIdx == -1)
            { // Even if idx is not found in Projects directory, just proceed to deal with monkey-patched scripts.
                sc = InternalLoadScript(sc.RealPath, sc.TreePath, true, sc.IsDirLink);
            }
            else
            {
                // This one is in legit Project list, so [Main] cannot be ignored
                sc = InternalLoadScript(sc.RealPath, sc.TreePath, false, sc.IsDirLink);
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

        #region InternalLoadScript
        /// <summary>
        /// Load script from file. 
        /// </summary>
        /// <param name="realPath"></param>
        /// <param name="treePath"></param>
        /// <param name="ignoreMain"></param>
        /// <param name="isDirLink"></param>
        /// <remarks>
        /// Project loader uses customized version of script loader. It bypasses caching.
        /// </remarks>
        /// <returns>Script instance of {realPath}</returns>
        private Script InternalLoadScript(string realPath, string treePath, bool ignoreMain, bool isDirLink)
        {
            Script sc;
            try
            {
                string mainScriptPath = Path.Combine(ProjectRoot, ProjectName, "script.project");
                if (realPath.Equals(mainScriptPath, StringComparison.OrdinalIgnoreCase))
                {
                    sc = new Script(ScriptType.Script, realPath, treePath, this, 0, true, ignoreMain, isDirLink);
                }
                else
                {
                    string ext = Path.GetExtension(realPath);
                    if (ext.Equals(".link", StringComparison.OrdinalIgnoreCase))
                        sc = new Script(ScriptType.Link, realPath, treePath, this, null, false, false, false);
                    else
                        sc = new Script(ScriptType.Script, realPath, treePath, this, null, false, ignoreMain, isDirLink);
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

        #region LoadScriptRuntime
        /// <summary>
        /// Load scripts into project while running
        /// Return true if error
        /// </summary>
        public Script LoadScriptRuntime(string realPath, LoadScriptRuntimeOptions opts)
        {
            Debug.Assert(realPath != null, $"nameof({realPath}) is null");

            // Temp script must have empty treePath
            // If a script is going to be added in script tree, it must not be empty
            string treePath = string.Empty;

            // If realPath is child of ProjectRoot ("%BaseDir%\Projects"), remove it 
            if (realPath.StartsWith(ProjectRoot, StringComparison.OrdinalIgnoreCase))
                treePath = realPath.Substring(ProjectRoot.Length + 1);

            return LoadScriptRuntime(realPath, treePath, opts);
        }

        /// <summary>
        /// Load scripts into project while running
        /// Return true if error
        /// </summary>
        public Script LoadScriptRuntime(string realPath, string treePath, LoadScriptRuntimeOptions opts)
        {
            Debug.Assert(realPath != null, $"nameof({realPath}) is null");
            Debug.Assert(treePath != null, $"nameof({treePath}) is null");

            // To use opts.AddToProjectTree, treePath should be valid
            if (opts.AddToProjectTree && treePath.Length == 0)
                throw new ArgumentException("Cannot add script to project tree because of empty treePath");

            // Load Script
            Script sc = InternalLoadScript(realPath, treePath, opts.IgnoreMain, false);

            // Add to project tree if option is set
            if (opts.AddToProjectTree)
            {
                int sIdx = AllScripts.FindIndex(x => x.TreePath.Equals(treePath, StringComparison.OrdinalIgnoreCase));
                if (sIdx != -1)
                { // Script already exists in project tree (collision)
                    if (opts.OverwriteToProjectTree)
                        AllScripts[sIdx] = sc;
                    else
                        throw new InvalidOperationException($"Unable to overwrite project tree [{treePath}]");
                }
                else
                { // Add to project tree
                    // Generate Directory Script if necessary
                    int bsIdx = sc.TreePath.IndexOf('\\');
                    if (bsIdx != -1)
                    {
                        string[] paths = sc.TreePath
                            .Substring(ProjectName.Length).TrimStart('\\')
                            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        for (int i = 0; i < paths.Length - 1; i++)
                        {
                            string pathKey = PathKeyGenerator(paths, i);
                            Script ts = AllScripts.FirstOrDefault(x =>
                                x.Level == sc.Level &&
                                x.TreePath.Equals(pathKey, StringComparison.OrdinalIgnoreCase));
                            if (ts == null)
                            {
                                string dirRealPath = Path.GetDirectoryName(realPath);
                                string dirTreePath = Path.Combine(ProjectName, pathKey);
                                Script dirScript = new Script(ScriptType.Directory, dirRealPath, dirTreePath, this, sc.Level, false, false, sc.IsDirLink);
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
        public static string PathKeyGenerator(string[] paths, int last)
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

        /*
        public Script GetScriptByTreePath(string sTreePath)
        {
            return AllScripts.Find(x => x.TreePath.Equals(sTreePath, StringComparison.OrdinalIgnoreCase));
        }
        */

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

            ScriptSection section = MainScript.RefreshSection(ScriptSection.Names.Variables);
            if (section != null)
                Variables.AddVariables(VarsType.Global, section);
        }
        #endregion

        #region Clone
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

        #region Equals, GetHashCode, ToString
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

        public override string ToString()
        {
            return ProjectName;
        }
        #endregion
    }
    #endregion

    #region struct LoadScriptRuntimeOptions
    public struct LoadScriptRuntimeOptions
    {
        public bool IgnoreMain;
        public bool AddToProjectTree;
        public bool OverwriteToProjectTree;
    }
    #endregion
}
