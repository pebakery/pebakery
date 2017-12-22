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
        private readonly PluginCache pluginCache;

        private readonly Dictionary<string, List<string>> pluginPathDict = new Dictionary<string, List<string>>();
        private readonly Dictionary<string, List<string>> dirLinkPathDict = new Dictionary<string, List<string>>();
        private readonly List<Plugin> allProjectPlugins = new List<Plugin>();
        private readonly List<string> allPluginPaths = new List<string>();
        private readonly List<string> allDirLinkPaths = new List<string>();

        // Properties
        public string ProjectRoot => projectRoot;
        public List<Project> Projects => projectDict.Values.OrderBy(x => x.ProjectName).ToList(); 
        public List<string> ProjectNames => projectDict.Keys.OrderBy(x => x).ToList(); 
        public Project this[int i] => Projects[i]; 
        public int Count => projectDict.Count;
        #endregion

        #region Constructor
        public ProjectCollection(string baseDir, PluginCache pluginCache)
        {
            this.baseDir = baseDir;
            this.projectRoot = Path.Combine(baseDir, "Projects");
            this.pluginCache = pluginCache;
        }
        #endregion

        public int PrepareLoad(out int processCount)
        {
            // Ex) projNameList = { "Win7PESE", "Win8PESE", "Win8.1SE", "Win10PESE" }
            // Ex) pluginPathDict = [plugin paths of Win7PESE, plugin paths of Win8PESE, ... ]
            List<string> projNameList = GetProjectNameList();
            GetPluginPaths(projNameList, out processCount);

            // Return count of all plugins
            return allPluginPaths.Count + allDirLinkPaths.Count;
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
        /// Get pluginPathDict and allPluginPathList
        /// </summary>
        /// <param name="projNameList"></param>
        public int GetPluginPaths(List<string> projNameList, out int linkCount)
        {
            int allCount = 0;
            linkCount = 0;
            foreach (string projName in projNameList)
            {
                List<string> pluginPathList = new List<string>();
                List<string> dirLinkPathList = new List<string>();

                string projectDir = Path.Combine(projectRoot, projName);

                string rootPlugin = Path.Combine(projectDir, "script.project");
                string[] scripts = Directory.GetFiles(projectDir, "*.script", SearchOption.AllDirectories);
                string[] plugins = Directory.GetFiles(projectDir, "*.pebakery", SearchOption.AllDirectories);
                string[] links = Directory.GetFiles(projectDir, "*.link", SearchOption.AllDirectories);

                pluginPathList.Add(rootPlugin);
                pluginPathList.AddRange(scripts);
                pluginPathList.AddRange(plugins);
                pluginPathList.AddRange(links);

                string rootLink = Path.Combine(projectDir, "folder.project");
                if (File.Exists(rootLink))
                {
                    foreach (string path in Ini.ParseRawSection(rootLink, "Links")
                        .Select(x => x.Trim())
                        .Where(x => x.Equals(string.Empty, StringComparison.Ordinal) == false))
                    {
                        string linkDirPath = Path.Combine(baseDir, Path.GetDirectoryName(path));
                        string linkWildcard = Path.GetFileName(path);

                        if (Directory.Exists(linkDirPath) && linkWildcard.IndexOfAny(new char[] { '*', '?' }) != -1)
                        {
                            string[] dirLinks = Directory.GetFiles(linkDirPath, linkWildcard, SearchOption.AllDirectories);
                            var dirScriptLinks = dirLinks.Where(p => Path.GetExtension(p).Equals(".script", StringComparison.OrdinalIgnoreCase));
                            dirLinkPathList.AddRange(dirScriptLinks);
                        }
                    }
                }

                allCount += pluginPathList.Count;
                linkCount += links.Length; // links should be added twice since they are processed twice

                pluginPathDict[projName] = pluginPathList;
                allPluginPaths.AddRange(pluginPathList);

                dirLinkPathDict[projName] = dirLinkPathList;
                allDirLinkPaths.AddRange(dirLinkPathList);
            }
            return allCount;
        }

        public List<LogInfo> Load(BackgroundWorker worker)
        {
            List<LogInfo> logs = new List<LogInfo>(32);
            try
            {
                foreach (var kv in pluginPathDict)
                {
                    Project project = new Project(baseDir, kv.Key);

                    // Load plugins
                    List<LogInfo> projLogs = project.Load(kv.Value, dirLinkPathDict[kv.Key], pluginCache, worker);
                    logs.AddRange(projLogs);

                    // Add Project.Plugins to ProjectCollections.Plugins
                    this.allProjectPlugins.AddRange(project.AllPlugins);

                    projectDict[kv.Key] = project;
                }

                // Populate *.link plugins
                List<LogInfo> linkLogs = LoadLinks(worker);
                logs.AddRange(linkLogs);

                // PostLoad plugins
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
            DB_PluginCache[] cacheDB = null;
            if (pluginCache != null)
                cacheDB = pluginCache.Table<DB_PluginCache>().Where(x => true).ToArray();

            var links = allProjectPlugins.Where(x => x.Type == PluginType.Link);
            Debug.Assert(links.Count(x => x.IsDirLink) == 0);
            Task[] tasks = links.Select(p =>
            {
                return Task.Run(() =>
                {
                    Plugin link = null;
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

                            // Load .link's linked plugins with cache
                            if (pluginCache != null)
                            { // Case of PluginCache enabled
                                FileInfo f = new FileInfo(linkFullPath);
                                DB_PluginCache pCache = cacheDB.FirstOrDefault(x => x.Hash == linkPath.GetHashCode());
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
                                            link = (Plugin)formatter.Deserialize(memStream);
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
                                PluginType type = PluginType.Plugin;
                                if (ext.Equals(".link", StringComparison.OrdinalIgnoreCase))
                                    type = PluginType.Link;
                                link = new Plugin(type, Path.Combine(baseDir, linkFullPath), p.Project, projectRoot, null, false, false, false);

                                Debug.Assert(p != null);
                            }

                            // Check Plugin Link's validity
                            // Also, convert nested link to one-depth link
                            if (link == null)
                                break;

                            if (link.Type == PluginType.Plugin)
                            {
                                valid = true;
                                break;
                            }
                            link = link.Link;
                        }
                        while (link.Type != PluginType.Plugin);
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
                        int idx = allProjectPlugins.IndexOf(p);
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
                allProjectPlugins.RemoveAt(idx);

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
        private int mainPluginIdx;
        private List<Plugin> allPlugins;
        private Variables variables;

        private int loadedPluginCount;
        private int allPluginCount;

        // Properties
        public string ProjectName => projectName;
        public string ProjectRoot => projectRoot;
        public string ProjectDir => projectDir;
        public string BaseDir => baseDir;
        public Plugin MainPlugin => allPlugins[mainPluginIdx];
        public List<Plugin> AllPlugins => allPlugins;
        public List<Plugin> ActivePlugins => CollectActivePlugins(allPlugins);
        public List<Plugin> VisiblePlugins => CollectVisiblePlugins(allPlugins);
        public Variables Variables { get => variables; set => variables = value; }
        public int LoadedPluginCount => loadedPluginCount; 
        public int AllPluginCount => allPluginCount;
        #endregion

        #region Constructor
        public Project(string baseDir, string projectName)
        {
            this.loadedPluginCount = 0;
            this.allPluginCount = 0;
            this.projectName = projectName;
            this.projectRoot = Path.Combine(baseDir, "Projects");
            this.projectDir = Path.Combine(baseDir, "Projects", projectName);
            this.baseDir = baseDir;
        }
        #endregion

        #region Load Plugins
        public List<LogInfo> Load(List<string> allPluginPathList, List<string> allDirLinkPathList, PluginCache pluginCache, BackgroundWorker worker)
        {
            List<LogInfo> logs = new List<LogInfo>(32);

            ReaderWriterLockSlim listLock = new ReaderWriterLockSlim();
            string mainPluginPath = Path.Combine(projectDir, "script.project");
            allPlugins = new List<Plugin>();

            // Doing this will consume memory, but also greatly increase performance.
            DB_PluginCache[] cacheDB = null;
            if (pluginCache != null)
                cacheDB = pluginCache.Table<DB_PluginCache>().Where(x => true).ToArray();

            // Item2 is IsDirLink
            // true -> dirLink, false -> plugin or pluginLink
            List<Tuple<string, bool>> pTupleList = new List<Tuple<string, bool>>();
            pTupleList.AddRange(allPluginPathList.Select(x => new Tuple<string, bool>(x, false)));
            pTupleList.AddRange(allDirLinkPathList.Select(x => new Tuple<string, bool>(x, true)));

            // Load plugins from disk or cache
            Task[] tasks = pTupleList.Select(pTuple =>
            {
                return Task.Run(() =>
                {
                    int cached = 0;
                    string pPath = pTuple.Item1;
                    bool isDirLink = pTuple.Item2;
                    Plugin p = null;
                    try
                    {
                        if (pluginCache != null)
                        { // PluginCache enabled
                            FileInfo f = new FileInfo(pPath);
                            string sPath = pPath.Remove(0, baseDir.Length + 1); // 1 for \
                            DB_PluginCache pCache = cacheDB.FirstOrDefault(x => x.Hash == sPath.GetHashCode());
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

                                        p = formatter.Deserialize(memStream) as Plugin;
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
                            bool isMainPlugin = false;
                            if (pPath.Equals(mainPluginPath, StringComparison.Ordinal))
                                isMainPlugin = true;

                            // TODO : Lazy loading of link, takes too much time at start
                            string ext = Path.GetExtension(pPath);
                            if (ext.Equals(".link", StringComparison.OrdinalIgnoreCase))
                                p = new Plugin(PluginType.Link, pPath, this, projectRoot, null, isMainPlugin, false, false);
                            else
                                p = new Plugin(PluginType.Plugin, pPath, this, projectRoot, null, isMainPlugin, false, isDirLink);

                            Debug.Assert(p != null);
                        }

                        listLock.EnterWriteLock();
                        try
                        {
                            allPlugins.Add(p);
                        }
                        finally
                        {
                            listLock.ExitWriteLock();
                        }

                        if (worker != null)
                            worker.ReportProgress(cached, Path.GetDirectoryName(p.ShortPath));
                    }
                    catch (Exception e)
                    {
                        logs.Add(new LogInfo(LogState.Error, Logger.LogExceptionMessage(e)));
                        if (worker != null)
                            worker.ReportProgress(cached);
                    }
                });
            }).ToArray();
            Task.WaitAll(tasks);

            // mainPluginIdx
            SetMainPluginIdx();

            return logs;
        }

        #region PostLoad, Sort
        public void PostLoad()
        {
            this.allPlugins = InternalSortPlugin(allPlugins);
            SetMainPluginIdx();

            this.Variables = new Variables(this);
        }

        private List<Plugin> InternalSortPlugin(List<Plugin> pList)
        {
            Tree<Plugin> pTree = new Tree<Plugin>();
            Dictionary<string, int> dirDict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            int rootId = pTree.AddNode(0, this.MainPlugin); // Root is script.project

            foreach (Plugin p in pList)
            {
                Debug.Assert(p != null);

                if (p == this.MainPlugin)
                    continue;

                int nodeId = rootId;
                string[] paths = p.ShortPath
                    .Substring(this.projectName.Length + 1)
                    .Split(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });

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
                        Plugin dirPlugin = new Plugin(PluginType.Directory, fullPath, this, projectRoot, p.Level, false, false, false);
                        nodeId = pTree.AddNode(nodeId, dirPlugin);
                        dirDict[key] = nodeId;
                    }
                }
                Debug.Assert(p != null);
                pTree.AddNode(nodeId, p);
            }

            // Sort - Plugin first, Directory last
            pTree.Sort((x, y) =>
            {
                if (x.Data.Level == y.Data.Level)
                {
                    if (x.Data.Type == PluginType.Directory)
                    {
                        if (y.Data.Type == PluginType.Directory)
                            return x.Data.FullPath.CompareTo(y.Data.FullPath);
                        else
                            return 1;
                    }
                    else
                    {
                        if (y.Data.Type == PluginType.Directory)
                            return -1;
                        else
                            return x.Data.FullPath.CompareTo(y.Data.FullPath);
                    }
                }
                else
                {
                    return x.Data.Level - y.Data.Level;
                }
            });

            List<Plugin> newList = new List<Plugin>();
            foreach (Plugin p in pTree)
            {
                if (p.Type != PluginType.Directory)
                    newList.Add(p);
            }

            return newList;
        }

        public void SetMainPluginIdx()
        {
            mainPluginIdx = allPlugins.FindIndex(x => x.IsMainPlugin);
            Debug.Assert(allPlugins.Count(x => x.IsMainPlugin) == 1);
            Debug.Assert(mainPluginIdx != -1);
        }
        #endregion

        public Plugin RefreshPlugin(Plugin plugin, EngineState s = null)
        {
            if (plugin == null) throw new ArgumentNullException("plugin");

            string pPath = plugin.FullPath;
            int aIdx = AllPlugins.FindIndex(x => x.FullPath.Equals(pPath, StringComparison.OrdinalIgnoreCase));

            Plugin p = null;
            if (aIdx == -1)
            {
                // Even if idx is not found in Projects directory, just proceed.
                // If not, cannot deal with monkey-patched plugins.
                p = LoadPlugin(pPath, true, plugin.IsDirLink);
            }
            else
            {
                // This one is in legit Project list, so [Main] cannot be ignored
                p = LoadPlugin(pPath, false, plugin.IsDirLink);
                if (p != null)
                {
                    allPlugins[aIdx] = p;
                    if (s != null)
                    {
                        // Investigate EngineState to update it on build list
                        int sIdx = s.Plugins.FindIndex(x => x.FullPath.Equals(pPath, StringComparison.OrdinalIgnoreCase));
                        if (sIdx != -1)
                            s.Plugins[sIdx] = p;
                    }
                }
            }
            return p;
        }

        /// <summary>
        /// Load plugin into project while running
        /// Return true if error
        /// </summary>
        /// <param name="plugin"></param>
        /// <returns></returns>
        public Plugin LoadPluginMonkeyPatch(string pPath, bool addToList = false, bool ignoreMain = false)
        {
            // Limit: fullPath must be in BaseDir
            if (pPath.StartsWith(this.baseDir, StringComparison.OrdinalIgnoreCase) == false)
                return null;

            Plugin p = LoadPlugin(pPath, ignoreMain, false);
            if (addToList)
            {
                allPlugins.Add(p);
                allPluginCount += 1;
            }

            return p;
        }

        public Plugin LoadPlugin(string pPath, bool ignoreMain, bool isDirLink)
        {
            Plugin p;
            try
            {
                if (pPath.Equals(Path.Combine(projectRoot, "script.project"), StringComparison.OrdinalIgnoreCase))
                {
                    p = new Plugin(PluginType.Plugin, pPath, this, projectRoot, 0, true, ignoreMain, isDirLink);
                }
                else
                {
                    string ext = Path.GetExtension(pPath);
                    if (ext.Equals(".link", StringComparison.OrdinalIgnoreCase))
                        p = new Plugin(PluginType.Link, pPath, this, projectRoot, null, false, false, false);
                    else
                        p = new Plugin(PluginType.Plugin, pPath, this, projectRoot, null, false, ignoreMain, isDirLink);
                }

                // Check Plugin Link's validity
                // Also, convert nested link to one-depth link
                if (p.Type == PluginType.Link)
                {
                    Plugin link = p.Link;
                    bool valid = false;
                    do
                    {
                        if (link == null)
                            return null;
                        if (link.Type == PluginType.Plugin)
                        {
                            valid = true;
                            break;
                        }
                        link = link.Link;
                    }
                    while (link.Type != PluginType.Plugin);

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

        #region Active, Visible Plugins
        private List<Plugin> CollectVisiblePlugins(List<Plugin> allPluginList)
        {
            List<Plugin> visiblePluginList = new List<Plugin>();
            foreach (Plugin p in allPluginList)
            {
                if (0 < p.Level)
                    visiblePluginList.Add(p);
            }
            return visiblePluginList;
        }

        private List<Plugin> CollectActivePlugins(List<Plugin> allPlugist)
        {
            List<Plugin> activePlugins = new List<Plugin>(allPlugist.Count)
            {
                MainPlugin
            };

            foreach (Plugin p in allPlugist.Where(x => !x.IsMainPlugin && (0 < x.Level)))
            {
                bool active = false;
                if (p.Type == PluginType.Plugin || p.Type == PluginType.Link)
                {                   
                    if (p.Selected != SelectedState.None)
                    {
                        if (p.Mandatory || p.Selected == SelectedState.True)
                            active = true;
                    }
                }

                if (active)
                    activePlugins.Add(p);
            }
            
            return activePlugins;
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

        #region GetPluginByPath
        public Plugin GetPluginByFullPath(string fullPath)
        {
            return AllPlugins.Find(x => string.Equals(x.FullPath, fullPath, StringComparison.OrdinalIgnoreCase));
        }

        public Plugin GetPluginByShortPath(string shortPath)
        {
            return AllPlugins.Find(x => string.Equals(x.ShortPath, shortPath, StringComparison.OrdinalIgnoreCase));
        }
        #endregion

        #region Variables
        public void UpdateProjectVariables()
        {
            if (variables != null)
            {
                if (MainPlugin.Sections.ContainsKey("Variables"))
                    variables.AddVariables(VarsType.Global, MainPlugin.Sections["Variables"]);
            }
        }
        #endregion

        #region Clone
        public object Clone()
        {
            Project project = new Project(baseDir, projectName)
            {
                mainPluginIdx = this.mainPluginIdx,
                allPlugins = new List<Plugin>(this.allPlugins),
                variables = this.variables.Clone() as Variables,
                loadedPluginCount = this.loadedPluginCount,
                allPluginCount = this.allPluginCount,
            };
            return project;
        }
        #endregion
    }
    #endregion
}
