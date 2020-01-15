/*
    Copyright (C) 2016-2020 Hajin Jang
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
using SQLite;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

        // These fields are being used only in pre-loading/loading stage
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
                if (!File.Exists(mainScript))
                    continue;

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
                    // Filter duplicated scripts
                    .Distinct(ScriptParseInfoComparer.Instance)
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
                    .Distinct(ScriptParseInfoComparer.Instance)
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
            List<ScriptParseInfo> dirLinks = new List<ScriptParseInfo>();
            IEnumerable<string> linkFiles = Directory.EnumerateFiles(projectDir, "folder.project", SearchOption.AllDirectories);
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

                // Local functions fo collect ScriptParseInfo
                void CollectScriptsFromDir(string dirPath)
                {
                    ScriptParseInfo CreateScriptParseInfo((string Path, bool IsDir) x) => new ScriptParseInfo
                    {
                        RealPath = x.Path,
                        TreePath = Path.Combine(prefix, Path.GetFileName(dirPath), x.Path.Substring(dirPath.Length).TrimStart('\\')),
                        IsDir = x.IsDir,
                        IsDirLink = true,
                    };

                    IEnumerable<ScriptParseInfo> scInfos = FileHelper.GetFilesExWithDirs(dirPath, "*.script", SearchOption.AllDirectories)
                        .Select(x => CreateScriptParseInfo(x));
                    IEnumerable<ScriptParseInfo> linkInfos = FileHelper.GetFilesExWithDirs(dirPath, "*.link", SearchOption.AllDirectories)
                        .Select(x => CreateScriptParseInfo(x));

                    // Duplicated ScriptParseInfo is removed later by GetScriptPaths.
                    dirLinks.AddRange(scInfos);
                    dirLinks.AddRange(linkInfos);
                }

                foreach (string path in rawPaths.Select(x => x.Trim()).Where(x => x.Length != 0))
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

                                CollectScriptsFromDir(subDir);
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

                                CollectScriptsFromDir(subDir);
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

                            CollectScriptsFromDir(dirPath);
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

                            CollectScriptsFromDir(fullPath);
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

            // Doing this will consume some memory, but also greatly increase performance.
            // -> Actually, using a cache relieves GC pressure a lot, so it is effective even for memory consumption.
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
                                linkTarget.IsDirLink = sc.IsDirLink;
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
                            linkTarget = new Script(type, fullPath, string.Empty, sc.Project, null, false, false, sc.IsDirLink);

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
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception e)
                { // Parser Error
                    logs.Add(new LogInfo(LogState.Error, Logger.LogExceptionMessage(e)));
                }
#pragma warning restore CA1031 // Do not catch general exception types

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
                    // Loading a project without a script cache generates a lot of Gen 2 heap object
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
}
