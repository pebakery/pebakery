/*
    Copyright (C) 2016-2019 Hajin Jang
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
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace PEBakery.Core
{
    #region ProjectCollection
    public class ProjectCollection : IReadOnlyList<Project>
    {
        #region Fields
        private readonly string _baseDir;

        // These fields are being used only in preloading/loading stage
        private readonly List<string> _projectNames = new List<string>();
        /// <summary>
        /// Dictionary for script parsing information
        /// </summary>
        private readonly Dictionary<string, ScriptParseInfo[]> _spiDict = new Dictionary<string, ScriptParseInfo[]>(StringComparer.OrdinalIgnoreCase);
        /// <summary>
        /// Dictionary for directory parsing information
        /// </summary>
        private readonly Dictionary<string, ScriptParseInfo[]> _dpiDict = new Dictionary<string, ScriptParseInfo[]>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, CompatOption> _compatDict = new Dictionary<string, CompatOption>(StringComparer.OrdinalIgnoreCase);
        private readonly List<Script> _allProjectScripts = new List<Script>();
        #endregion

        #region Properties
        // Auto properties
        public string ProjectRoot { get; }
        public List<Project> ProjectList { get; } = new List<Project>();

        private bool _fullyLoaded = false;
        public bool FullyLoaded
        {
            get => _fullyLoaded;
            set
            {
                _fullyLoaded = value;
                if (value)
                { // Load cleanup
                    _projectNames.Clear();
                    _spiDict.Clear();
                    _dpiDict.Clear();
                    _compatDict.Clear();
                    _allProjectScripts.Clear();
                }
            }
        }

        // Expose _projectNames and _compatDict to public
        public List<string> ProjectNames => FullyLoaded ?
            ProjectList.Select(x => x.ProjectName).ToList() :
            new List<string>(_projectNames);
        public Dictionary<string, CompatOption> CompatOptions => FullyLoaded ?
            ProjectList.ToDictionary(x => x.ProjectName, x => x.Compat) :
            new Dictionary<string, CompatOption>(_compatDict, StringComparer.OrdinalIgnoreCase);

        // Collection
        public Project this[int i] => ProjectList[i];
        public int Count => ProjectList.Count;
        #endregion

        #region Constructor
        public ProjectCollection(string baseDir)
        {
            _baseDir = baseDir;
            ProjectRoot = Path.Combine(baseDir, Project.Names.Projects);

            RefreshProjectEntries();
        }
        #endregion

        #region RefreshProjectEntries
        /// <summary>
        /// Scan ProjectRoot to generate a list of projects and their compat options.
        /// </summary>
        public void RefreshProjectEntries()
        {
            FullyLoaded = false;
            _projectNames.Clear();
            ProjectList.Clear();

            if (!Directory.Exists(ProjectRoot))
                return; // No projects

            // Ex) ProjectRoot = E:\WinPE\Win10XPE\Projects
            // Ex) projectDir  = E:\WinPE\Win10XPE\Projects\Win10XPE
            // Ex) projectPath = E:\WinPE\Win10XPE\Projects\Win10XPE\script.project
            // Ex) projectName = Win10XPE
            string[] projectDirs = Directory.GetDirectories(ProjectRoot);
            foreach (string projectDir in projectDirs)
            {
                string mainScript = Path.Combine(projectDir, Project.Names.MainScriptFile);
                if (File.Exists(mainScript))
                {
                    // Feed _projectNames.
                    string projectName = Path.GetFileName(projectDir);
                    _projectNames.Add(projectName);

                    // Load per-project compat options.
                    // Even if compatFile does not exist, CompatOption class will deal with it.
                    string compatFile = Path.Combine(projectDir, Project.Names.CompatFile);
                    CompatOption compat = new CompatOption(compatFile);
                    _compatDict[projectName] = compat;
                }
            }
        }
        #endregion

        #region PrepareLoad
        public (int ScriptCount, int LinkCount) PrepareLoad()
        {
            // Ex) projectNames = { "ChrisPE", "MistyPE", "Win10XPE", "Win7PESE" }
            // Ex) scriptPathDict = [script paths of ChrisPE, script paths of MistyPE, ... ]
            return GetScriptPaths();
        }
        #endregion

        #region GetScriptPaths
        /// <summary>
        /// Get scriptPathDict and allScriptPathList
        /// </summary>
        /// <returns>
        /// (Count of the .script/.link files, count of the .link files)
        /// </returns>
        private (int ScriptCount, int LinkCount) GetScriptPaths()
        {
            int scriptCount = 0;
            int linkCount = 0;
            foreach (string projectName in _projectNames)
            {
                string projectDir = Path.Combine(ProjectRoot, projectName);

                // Path of root script
                ScriptParseInfo rootScript = new ScriptParseInfo
                {
                    RealPath = Path.Combine(projectDir, Project.Names.MainScriptFile),
                    TreePath = Path.Combine(projectName, Project.Names.MainScriptFile),
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
                List<ScriptParseInfo> dirLinks = GetDirLinks(projectDir, _compatDict[projectName].AsteriskBugDirLink);

                int spiCount = 1 + scripts.Length + links.Length + dirLinks.Count;
                List<ScriptParseInfo> allParseInfos = new List<ScriptParseInfo>(spiCount) { rootScript };
                allParseInfos.AddRange(scripts);
                allParseInfos.AddRange(links);
                allParseInfos.AddRange(dirLinks);

                // Filter ScriptParseInfos by (.script, .link) vs (directory)
                ScriptParseInfo[] spis = allParseInfos
                    // Only .script and .link
                    .Where(x => !x.IsDir)
                    // Filter duplicated scripts (TODO: Used this for just in case, change it to assert later)
                    .Distinct(new ScriptParseInfoComparer())
                    // This sort is for more natural loading messages
                    // It should be sorted one more time later, using KwayTree
                    .OrderBy(x => x.RealPath, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                ScriptParseInfo[] dpis = allParseInfos
                    // Only directories
                    .Where(x => x.IsDir)
                    // Exclude %BaseDir%\Projects\{ProjectName} directory
                    .Where(x => !x.RealPath.Equals(projectDir, StringComparison.OrdinalIgnoreCase))
                    // Filter duplicated directories (such as directory covered both by scripts/links and dirLinks)
                    .Distinct(new ScriptParseInfoComparer())
                    // This sort is for more natural loading messages
                    // It should be sorted one more time later, using KwayTree
                    .OrderBy(x => x.RealPath, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                _spiDict[projectName] = spis;
                _dpiDict[projectName] = dpis;

                // Count .script and .link
                scriptCount += spis.Count(x => x.RealPath.EndsWith(".script", StringComparison.OrdinalIgnoreCase));
                linkCount += spis.Count(x => x.RealPath.EndsWith(".link", StringComparison.OrdinalIgnoreCase));
            }
            return (scriptCount, linkCount);
        }
        #endregion

        #region GetDirLinks
        private List<ScriptParseInfo> GetDirLinks(string projectDir, bool asteriskBugDirLink)
        {
            var dirLinks = new List<ScriptParseInfo>();
            var linkFiles = Directory.EnumerateFiles(projectDir, "folder.project", SearchOption.AllDirectories);
            foreach (string linkFile in linkFiles)
            {
                Debug.Assert(linkFile != null, $"{nameof(linkFile)} is wrong");
                Debug.Assert(linkFile.StartsWith(_baseDir), $"{nameof(linkFile)} [{linkFile}] does not start with %BaseDir%");

                List<string> rawPaths = IniReadWriter.ParseRawSection(linkFile, "Links");
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
                    if (asteriskBugDirLink && isWildcard)
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
        public List<LogInfo> Load(ScriptCache scriptCache, IProgress<(Project.LoadReport Type, string Path)> progress)
        {
            if (_projectNames.Count == 0)
            {
                return new List<LogInfo>
                {
                    new LogInfo(LogState.Error, "No project found"),
                };
            }

            List<LogInfo> logs = new List<LogInfo>(32);
            try
            {
                foreach (string key in _projectNames)
                {
                    Project project = new Project(_baseDir, key, _compatDict[key])
                    {
                        // Make a copy of _dpiDict[key]
                        DirEntries = _dpiDict[key].ToList(),
                    };

                    // Load scripts
                    List<LogInfo> errLogs = project.Load(_spiDict[key], scriptCache, progress);
                    logs.AddRange(errLogs);

                    // Add Project.Scripts to ProjectCollections.Scripts
                    _allProjectScripts.AddRange(project.AllScripts);

                    ProjectList.Add(project);
                }

                // Sort ProjectList
                ProjectList.Sort((x, y) =>
                    string.Compare(x.ProjectName, y.ProjectName, StringComparison.OrdinalIgnoreCase));

                // Populate *.link scripts
                List<LogInfo> linkLogs = LoadLinks(scriptCache, progress);
                logs.AddRange(linkLogs);

                // PostLoad scripts
                foreach (Project p in ProjectList)
                    p.PostLoad();

                FullyLoaded = true;
            }
            catch (SQLiteException e)
            {
                // Update failure
                string msg = $"SQLite Error : {e.Message}\r\nCache Database is corrupted. Please delete PEBakeryCache.db and restart.";
                MessageBox.Show(msg, "SQLite Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown(1);
            }

            // Loading a project without script cache generates a lot of Gen 2 heap object
            GC.Collect();

            return logs;
        }

        private List<LogInfo> LoadLinks(ScriptCache scriptCache, IProgress<(Project.LoadReport Type, string Path)> progress)
        {
            List<LogInfo> logs = new List<LogInfo>(32);
            List<int> removeIdxs = new List<int>();

            // Doing this will consume memory, but also greatly increase performance.
            CacheModel.ScriptCache[] cachePool = null;
            if (scriptCache != null)
            {
                progress?.Report((Project.LoadReport.LoadingCache, null));
                cachePool = scriptCache.Table<CacheModel.ScriptCache>().ToArray();
            }

            string CheckLinkPath(Script sc, string linkRawPath)
            {
                // PEBakery's TreePath strips "%BaseDir%\Project" (ProjectRoot) from RealPath.
                // WinBuilder's .link file's link= path strips "%BaseDir%" from RealPath.
                // BE CAREFUL ABOUT THIS DIFFERENCE WHEN HANDLING LINK PATHS!

                string linkRealPath;
                if (Path.IsPathRooted(linkRawPath)) // Ex) link=E:\Link\HelloWorld.script
                    linkRealPath = linkRawPath; // Full path, 
                else // Ex) link=Projects\TestSuite\Downloads\HelloWorld.script
                    linkRealPath = Path.Combine(_baseDir, linkRawPath); // Non-rooted path 

                // Unable to find 
                if (!File.Exists(linkRealPath))
                {
                    Global.Logger.SystemWrite(new LogInfo(LogState.Error, $"Link [{sc.TreePath}]'s link path [{linkRawPath}] cannot be found in disk"));
                    return null;
                }

                return linkRealPath;
            }

            int loadCount = 0;
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
                                linkTarget.TreePath = string.Empty;
                                linkTarget.Project = sc.Project;
                                linkTarget.IsDirLink = false;
                                cached = Project.LoadReport.Stage2Cached;
                            }
                        }

                        if (linkTarget == null)
                        {
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

                if (scriptCache == null)
                {
                    // Loading a project without script cache generates a lot of Gen 2 heap object
                    int thisCount = Interlocked.Increment(ref loadCount);
                    if (thisCount % Project.LoadGCInterval == 0)
                        GC.Collect();
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
        public IEnumerator<Project> GetEnumerator() => ProjectList.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        #endregion

        #region IndexOf
        public int IndexOf(Project targetProject)
        {
            for (int i = 0; i < ProjectList.Count; i++)
            {
                Project p = ProjectList[i];
                if (p.Equals(targetProject))
                    return i;
            }
            return -1;
        }

        public int IndexOf(string targetProjectName)
        {
            for (int i = 0; i < ProjectList.Count; i++)
            {
                Project p = ProjectList[i];
                if (p.ProjectName.Equals(targetProjectName, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }
        #endregion
    }
    #endregion

    #region Project
    public class Project : IEquatable<Project>
    {
        #region Constants
        public static class Names
        {
            public const string Projects = "Projects";
            public const string MainScriptFile = "script.project";
            public const string CompatFile = "PEBakeryCompat.ini";
        }

        public const int LoadGCInterval = 64;
        #endregion

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
        public List<ScriptParseInfo> DirEntries { get; set; }
        public Variables Variables { get; set; }
        public CompatOption Compat { get; private set; }

        public int LoadedScriptCount { get; private set; }
        public int AllScriptCount { get; private set; }
        #endregion

        #region Constructor
        public Project(string baseDir, string projectName, CompatOption compat)
        {
            LoadedScriptCount = 0;
            AllScriptCount = 0;
            ProjectName = projectName;
            ProjectRoot = Path.Combine(baseDir, Names.Projects);
            ProjectDir = Path.Combine(baseDir, Names.Projects, projectName);
            BaseDir = baseDir;
            Compat = compat;
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

        /// <summary>
        /// Load scripts of the project.
        /// .link files additionally requires another stage of loading later.
        /// </summary>
        /// <param name="spis">ScriptParseInfo of .script files and .link files.</param>
        /// <param name="scriptCache">ScriptCache instance. Set to null if cache is disabled.</param>
        /// <param name="progress">Delegate for reporting progress</param>
        /// <returns></returns>
        public List<LogInfo> Load(IList<ScriptParseInfo> spis, ScriptCache scriptCache, IProgress<(LoadReport Type, string Path)> progress)
        {
            List<LogInfo> logs = new List<LogInfo>(32);

            string mainScriptPath = Path.Combine(ProjectDir, Names.MainScriptFile);
            AllScripts = new List<Script>();

            CacheModel.ScriptCache[] cachePool = null;
            if (scriptCache != null)
            {
                progress?.Report((LoadReport.LoadingCache, null));
                cachePool = scriptCache.Table<CacheModel.ScriptCache>().ToArray();
            }

            // Load scripts from disk or cache
            bool cacheValid = true;
            object listLock = new object();
            Parallel.ForEach(spis, spi =>
            {
                Debug.Assert(spi.RealPath != null, "spi.RealPath is null");
                Debug.Assert(spi.TreePath != null, "spi.TreePath is null");
                Debug.Assert(!spi.IsDir, $"Project.{nameof(Load)} must not handle directory script instance");

                LoadReport cached = LoadReport.Stage1;
                Script sc = null;
                try
                {
                    if (cachePool != null && cacheValid)
                    { // ScriptCache enabled (disabled in Directory script)
                        (sc, cacheValid) = ScriptCache.DeserializeScript(spi.RealPath, cachePool);
                        if (sc != null)
                        {
                            sc.TreePath = spi.TreePath;
                            sc.Project = this;
                            sc.IsDirLink = spi.IsDirLink;
                            cached = LoadReport.Stage1Cached;
                        }
                    }

                    if (sc == null)
                    { // Cache Miss
                        bool isMainScript = spi.RealPath.Equals(mainScriptPath, StringComparison.OrdinalIgnoreCase);
                        // Directory scripts will not be directly used (so level information is dummy)
                        // They are mainly used to store RealPath and TreePath information.
                        if (Path.GetExtension(spi.TreePath).Equals(".link", StringComparison.OrdinalIgnoreCase))
                            sc = new Script(ScriptType.Link, spi.RealPath, spi.TreePath, this, null, isMainScript, false, false);
                        else
                            sc = new Script(ScriptType.Script, spi.RealPath, spi.TreePath, this, null, isMainScript, false, spi.IsDirLink);

                        Debug.Assert(sc != null);
                    }

                    lock (listLock)
                    {
                        AllScripts.Add(sc);

                        // Loading a project without script cache generates a lot of Gen 2 heap object
                        if (scriptCache == null && AllScripts.Count % LoadGCInterval == 0)
                            GC.Collect();
                    }

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
            Variables = new Variables(this, Compat);
        }

        public void SortAllScripts()
        {
            AllScripts = InternalSortScripts(AllScripts, DirEntries);
            SetMainScriptIdx();
        }

        private List<Script> InternalSortScripts(List<Script> scripts, List<ScriptParseInfo> dpis)
        {
            KwayTree<Script> scTree = new KwayTree<Script>();
            Dictionary<string, int> dirDict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            int rootId = scTree.AddNode(0, MainScript); // Root == script.project

            foreach (Script sc in scripts.Where(x => x.Type != ScriptType.Directory))
            {
                if (sc.IsMainScript)
                    continue;

                int nodeId = rootId;

                // Ex) sc.TreePath = TestSuite\Samples\SVG.script
                // Ex) paths = { "TestSuite", "Samples", "SVG.script" }
                // Project name should be ignored -> Use index starting from 1 in for loop
                string[] paths = sc.TreePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                Debug.Assert(1 <= paths.Length, $"Invalid TreePath ({sc.TreePath})");
                paths = paths.Skip(1).ToArray();

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
                        // Find ts, skeleton script instance of the directory
                        string treePath = Path.Combine(ProjectName, pathKey);

                        ScriptParseInfo dpi = dpis.FirstOrDefault(x =>
                            x.IsDirLink == sc.IsDirLink &&
                            x.TreePath.Equals(treePath, StringComparison.OrdinalIgnoreCase));
                        Debug.Assert(!dpi.Equals(default), $"Unable to find proper directory ({sc.TreePath})");

                        // Create new directory script instance from a directory parse info.
                        // Do not have to cache these scripts, these directory script instance is only used once.
                        Script dirScript = new Script(ScriptType.Directory, dpi.RealPath, dpi.TreePath, this, sc.Level, false, false, dpi.IsDirLink);
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
            Debug.Assert(AllScripts.Count(x => x.IsMainScript) == 1, $"[{AllScripts.Count(x => x.IsMainScript)}] MainScript reported instead of [1]");
            _mainScriptIdx = AllScripts.FindIndex(x => x.IsMainScript);
            Debug.Assert(_mainScriptIdx != -1, $"Unable to find MainScript of [{ProjectName}]");
        }
        #endregion

        #region RefreshScript
        /// <summary>
        /// Create new script instance from old one.
        /// While Project.RefreshScript create new script instance, Script.RefreshSections refresh only sections.
        /// </summary>
        public Script RefreshScript(Script sc, EngineState s = null)
        {
            if (sc == null)
                throw new ArgumentNullException(nameof(sc));
            if (sc.Type == ScriptType.Directory)
                return null;

            int aIdx = AllScripts.FindIndex(x => x.RealPath.Equals(sc.RealPath, StringComparison.OrdinalIgnoreCase));
            if (aIdx == -1)
            { // Even if idx is not found in Projects directory, just proceed to deal with runtime-loaded scripts.
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
                    // Generate directory script instance if necessary
                    if (sc.TreePath.IndexOf('\\') != -1)
                    {
                        string[] paths = sc.TreePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        Debug.Assert(1 <= paths.Length, $"Invalid TreePath ({sc.TreePath})");
                        paths = paths.Skip(1).ToArray();

                        for (int i = 0; i < paths.Length - 1; i++)
                        {
                            string pathKey = PathKeyGenerator(paths, i);

                            // Need to create directory script instance?
                            Script ts = AllScripts.FirstOrDefault(x =>
                                x.Level == sc.Level &&
                                x.IsDirLink == sc.IsDirLink &&
                                x.TreePath.Equals(pathKey, StringComparison.OrdinalIgnoreCase));
                            if (ts == null)
                            {
                                string dirRealPath = Path.GetDirectoryName(realPath);
                                string dirTreePath = Path.Combine(ProjectName, pathKey);

                                Script dirScript = new Script(ScriptType.Directory, dirRealPath, dirTreePath, this, sc.Level, false, false, sc.IsDirLink);
                                AllScripts.Add(dirScript);
                            }

                            // Need to create directory parse info?
                            ScriptParseInfo dpi = DirEntries.FirstOrDefault(x =>
                                x.IsDirLink == sc.IsDirLink &&
                                x.TreePath.Equals(treePath, StringComparison.OrdinalIgnoreCase));
                            if (dpi.Equals(default))
                            {
                                string dirRealPath = Path.GetDirectoryName(realPath);
                                string dirTreePath = Path.Combine(ProjectName, pathKey);

                                DirEntries.Add(new ScriptParseInfo
                                {
                                    RealPath = dirRealPath,
                                    TreePath = dirTreePath,
                                    IsDir = true,
                                    IsDirLink = sc.IsDirLink,
                                });
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
        public List<LogInfo> UpdateProjectVariables()
        {
            if (Variables == null)
                return new List<LogInfo>();

            ScriptSection section = MainScript.RefreshSection(ScriptSection.Names.Variables);
            if (section != null)
                return Variables.AddVariables(VarsType.Global, section);

            return new List<LogInfo>();
        }
        #endregion

        #region IsPathSettingEnabled
        public bool IsPathSettingEnabled()
        {
            // If key 'PathSetting' have invalid value or does not exist, default to true
            if (!MainScript.MainInfo.ContainsKey("PathSetting"))
                return true;

            string valStr = MainScript.MainInfo["PathSetting"];
            if (valStr.Equals("True", StringComparison.OrdinalIgnoreCase))
                return true;
            else if (valStr.Equals("False", StringComparison.OrdinalIgnoreCase))
                return false;
            else
                return true;
        }
        #endregion

        #region Clone
        public Project PartialDeepCopy()
        {
            Project project = new Project(BaseDir, ProjectName, Compat)
            {
                _mainScriptIdx = _mainScriptIdx,
                AllScripts = AllScripts.ToList(),
                Variables = Variables.DeepCopy(),
                LoadedScriptCount = LoadedScriptCount,
                AllScriptCount = AllScriptCount,
                DirEntries = DirEntries.ToList(),
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
