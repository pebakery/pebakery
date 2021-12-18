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
using PEBakery.Tree;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PEBakery.Core
{
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

        #region Fields and Properties
        private int _mainScriptIdx = -1; // -1 means not initialized

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
        public CompatOption Compat { get; }
        public ProjectUpdateInfo UpdateInfo { get; private set; }
        public bool IsUpdateable => UpdateInfo != null && UpdateInfo.IsUpdateable;

        public int LoadedScriptCount { get; private set; }
        public int AllScriptCount { get; private set; }

        // FileSystemWatcher to live-update changed script.
        // Useful for live script development and testing.
        // TODO: Filter out changes made by PEBakery itself.
        private readonly FileSystemWatcher _fsWatcher;

        private EventHandler<string> _scriptFileUpdated = null;
        public event EventHandler<string> ScriptFileUpdated
        {
            add => _scriptFileUpdated += value;
            remove => _scriptFileUpdated -= value;
        }
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

            // Watch filesystem to catch edit of script-level file change.
            _fsWatcher = new FileSystemWatcher(ProjectDir);
            _fsWatcher.Filters.Add("*.script");
            _fsWatcher.Filters.Add("script.project");
            _fsWatcher.NotifyFilter = NotifyFilters.LastWrite;
            _fsWatcher.Changed += FileSystemWatcher_Changed;
            _fsWatcher.IncludeSubdirectories = true;
            _fsWatcher.EnableRaisingEvents = true; // Start running filesystem watcher
        }
        #endregion

        #region enum LoadReport
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
        internal List<LogInfo> Load(ScriptCache scriptCache, IList<ScriptParseInfo> spis, IProgress<(LoadReport Type, string Path)> progress)
        {
            List<LogInfo> logs = new List<LogInfo>(32);

            string mainScriptPath = Path.Combine(ProjectDir, Names.MainScriptFile);
            AllScripts = new List<Script>();

            // Load scripts from disk or cache
            bool cacheValid = true;
            object listLock = new object();
            Parallel.ForEach(spis, spi =>
            {
                Debug.Assert(spi.RealPath != null, "spi.RealPath is null");
                Debug.Assert(spi.TreePath != null, "spi.TreePath is null");
                Debug.Assert(!spi.IsDir, $"{nameof(Project)}.{nameof(Load)} must not handle directory script instance");

                LoadReport cached = LoadReport.Stage1;
                Script sc = null;
                try
                {
                    if (scriptCache != null && cacheValid)
                    { // ScriptCache enabled (disabled in Directory script)
                        sc = scriptCache.DeserializeScript(spi.RealPath, out cacheValid);
                        if (sc != null)
                        {
                            sc.PostDeserialization(spi.TreePath, this, spi.IsDirLink);
                            cached = LoadReport.Stage1Cached;
                        }
                    }

                    if (sc == null)
                    { // Cache Miss
                        bool isMainScript = spi.RealPath.Equals(mainScriptPath, StringComparison.OrdinalIgnoreCase);
                        // Directory scripts will not be directly used (so level information is dummy)
                        // They are mainly used to store RealPath and TreePath information.
                        if (Path.GetExtension(spi.TreePath).Equals(".link", StringComparison.OrdinalIgnoreCase))
                            sc = new Script(ScriptType.Link, spi.RealPath, spi.TreePath, this, null, isMainScript, false, spi.IsDirLink);
                        else
                            sc = new Script(ScriptType.Script, spi.RealPath, spi.TreePath, this, null, isMainScript, false, spi.IsDirLink);

                        Debug.Assert(sc != null);
                    }

                    lock (listLock)
                    {
                        AllScripts.Add(sc);

                        // Loading a project without script cache generates a lot of Gen 2 heap object
                        // TODO: Remove this part of code?
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
            SetMainScriptIndex();

            // Read [ProjectUpdate]
            ReadUpdateSection();

            return logs;
        }
        #endregion

        #region SetMainScriptIdx, ReadUpdateSection
        public void SetMainScriptIndex()
        {
            Debug.Assert(AllScripts.Count(x => x.IsMainScript) == 1, $"[{AllScripts.Count(x => x.IsMainScript)}] MainScript reported instead of [1]");
            _mainScriptIdx = AllScripts.FindIndex(x => x.IsMainScript);
            Debug.Assert(_mainScriptIdx != -1, $"Unable to find MainScript of [{ProjectName}]");
        }

        public void ReadUpdateSection()
        {
            Debug.Assert(_mainScriptIdx != -1, $"Please call {nameof(SetMainScriptIndex)} first");

            // If [ProjectUpdateSection] is not available, return empty ProjectUpdateInfo
            if (!MainScript.Sections.ContainsKey(ProjectUpdateInfo.Const.ProjectUpdateSection))
            {
                UpdateInfo = new ProjectUpdateInfo();
                return;
            }

            // Read [ProjectUpdateSection]
            Dictionary<string, string> pUpdateDict = MainScript.Sections[ProjectUpdateInfo.Const.ProjectUpdateSection].IniDict;

            // Read AvailableChannel=, SelectedChannel=, BaseUrl
            if (!(pUpdateDict.ContainsKey(ProjectUpdateInfo.Const.SelectedChannel) &&
                  pUpdateDict.ContainsKey(ProjectUpdateInfo.Const.BaseUrl)))
            {
                UpdateInfo = new ProjectUpdateInfo();
                return;
            }

            // Check integrity of IniDict value
            string selectedChannel = pUpdateDict[ProjectUpdateInfo.Const.SelectedChannel];
            string pBaseUrl = pUpdateDict[ProjectUpdateInfo.Const.BaseUrl].TrimEnd('/');
            if (StringHelper.GetUriProtocol(pBaseUrl) == null)
            {
                UpdateInfo = new ProjectUpdateInfo();
                return;
            }

            try
            {
                UpdateInfo = new ProjectUpdateInfo(selectedChannel, pBaseUrl);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (ArgumentException)
            {
                UpdateInfo = new ProjectUpdateInfo();
            }
#pragma warning restore CA1031 // Do not catch general exception types
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
            SetMainScriptIndex();
        }

        private List<Script> InternalSortScripts(IReadOnlyList<Script> scripts, IReadOnlyList<ScriptParseInfo> dpis)
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
                        Debug.Assert(dpi != null, $"Unable to find proper directory of ({sc.TreePath})");

                        // Create new directory script instance from a directory parse info.
                        // Do not have to cache these scripts, these directory script instance is only used once.
                        Script dirScript = new Script(ScriptType.Directory, dpi.RealPath, dpi.TreePath, this, sc.Level, false, false, dpi.IsDirLink);
                        nodeId = scTree.AddNode(nodeId, dirScript);
                        dirDict[key] = nodeId;
                    }
                }

                scTree.AddNode(nodeId, sc);
            }

            // Sort - script first, directory last
            // Use StringComparison.InvariantCultureIgnoreCase to emulate WinBuilder sorting
            // .link files should use RealPath (path of linked .script), not DirectRealPath (path of .link itself)
            scTree.Sort((x, y) =>
            {
                if (x.Data.Level == y.Data.Level)
                {
                    if (x.Data.Type == ScriptType.Directory)
                    {
                        if (y.Data.Type == ScriptType.Directory)
                        {
                            string xPath = Path.GetFileName(x.Data.RealPath);
                            string yPath = Path.GetFileName(y.Data.RealPath);
                            return string.Compare(xPath, yPath, StringComparison.InvariantCultureIgnoreCase);
                        }
                        else
                        {
                            return 1;
                        }
                    }
                    else
                    {
                        if (y.Data.Type == ScriptType.Directory)
                        {
                            return -1;
                        }
                        else
                        {
                            string xPath = Path.GetFileName(x.Data.RealPath);
                            string yPath = Path.GetFileName(y.Data.RealPath);
                            return string.Compare(xPath, yPath, StringComparison.InvariantCultureIgnoreCase);
                        }
                    }
                }
                else
                {
                    return x.Data.Level - y.Data.Level;
                }
            });

            return scTree.ToList();
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
                        sc.SetLink(link);
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
                treePath = FileHelper.SubRootDirPath(realPath, ProjectRoot);

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
                            if (dpi == null)
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
            if (!MainScript.MainInfo.ContainsKey(Script.Const.PathSetting))
                return true;

            string valStr = MainScript.MainInfo[Script.Const.PathSetting];
            return !valStr.Equals("False", StringComparison.OrdinalIgnoreCase) &&
                   !valStr.Equals("0", StringComparison.OrdinalIgnoreCase);
        }
        #endregion

        #region FileSystemWatcher
        private void FileSystemWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            // Check if the event is a change of file.
            if (e.ChangeType != WatcherChangeTypes.Changed)
                return;

            // Check if the changed one is a file.
            if (File.Exists(e.FullPath) == false)
                return;

            // Check if the changed file is one of the visible scripts
            if (VisibleScripts.Any(x => x.RealPath.Equals(e.FullPath, StringComparison.OrdinalIgnoreCase)) == false)
                return;

            // Invoke event listeners
            _scriptFileUpdated?.Invoke(this, e.FullPath);
        }

        public void ClearFileSystemWatcherEvents()
        {
            _scriptFileUpdated = null;
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
            if (project == null)
                return false;

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

    #region LoadScriptRuntimeOptions
    public class LoadScriptRuntimeOptions
    {
        /// <summary>
        /// Do not check integrity of [Main] section
        /// </summary>
        public bool IgnoreMain;
        /// <summary>
        /// Add to project tree if the script was not
        /// </summary>
        public bool AddToProjectTree;
        /// <summary>
        /// Overwrite script if project tree already has it
        /// </summary>
        public bool OverwriteToProjectTree;
    }
    #endregion
}
