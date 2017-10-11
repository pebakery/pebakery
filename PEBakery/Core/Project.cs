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
using SQLite.Net;
using System.Windows;
using PEBakery.WPF;
using PEBakery.Helper;

namespace PEBakery.Core
{
    #region ProjectCollection
    public class ProjectCollection
    {
        // Fields
        private readonly string baseDir;
        private readonly string projectRoot;
        private readonly Dictionary<string, Project> projectDict;
        private readonly PluginCache pluginCache;

        private readonly Dictionary<string, List<string>> pluginPathDict;
        private readonly List<Plugin> allPlugins;
        private readonly List<string> allPluginPaths;

        public const int MainLevel = -256;  // Reserved level for script.project

        // Properties
        public List<Project> Projects { get => projectDict.Values.OrderBy(x => x.ProjectName).ToList(); }
        public List<string> ProjectNames { get => projectDict.Keys.OrderBy(x => x).ToList(); }
        public Project this[int i] { get => Projects[i]; }
        public int Count { get => projectDict.Count; }

        public ProjectCollection(string baseDir, PluginCache pluginCache)
        {
            this.baseDir = baseDir;
            this.projectRoot = Path.Combine(baseDir, "Projects");
            this.projectDict = new Dictionary<string, Project>(StringComparer.Ordinal);
            this.pluginCache = pluginCache;

            this.pluginPathDict = new Dictionary<string, List<string>>();
            this.allPlugins = new List<Plugin>();
            this.allPluginPaths = new List<string>();
        }

        public int PrepareLoad(out int processCount)
        {
            // Ex) projNameList = { "Win7PESE", "Win8PESE", "Win8.1SE", "Win10PESE" }
            // Ex) pluginPathDict = [plugin paths of Win7PESE, plugin paths of Win8PESE, ... ]
            List<string> projNameList = GetProjectNameList();
            GetPluginPaths(projNameList, out processCount);

            // Return count of all plugins
            return allPluginPaths.Count;
        }

        /// <summary>
        /// Get project names
        /// </summary>
        /// <returns></returns>
        public List<string> GetProjectNameList()
        {
            List<string> projNameList = new List<string>();

            string[] projArray = Directory.GetDirectories(projectRoot);
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

                string projectDir = Path.Combine(projectRoot, projName);

                string rootPlugin = Path.Combine(projectDir, "script.project");
                string[] scripts = Directory.GetFiles(projectDir, "*.script", SearchOption.AllDirectories);
                string[] plugins = Directory.GetFiles(projectDir, "*.pebakery", SearchOption.AllDirectories);
                string[] links = Directory.GetFiles(projectDir, "*.link", SearchOption.AllDirectories);

                pluginPathList.Add(rootPlugin);
                pluginPathList.AddRange(scripts);
                pluginPathList.AddRange(plugins);
                pluginPathList.AddRange(links);

                allCount += pluginPathList.Count;
                linkCount += links.Length; // links should be added twice since they are processed twice

                pluginPathDict[projName] = pluginPathList;
                allPluginPaths.AddRange(pluginPathList);
            }
            return allCount;
        }

        public void Load(BackgroundWorker worker)
        {
            try
            {
                foreach (var kv in pluginPathDict)
                {
                    Project project = new Project(baseDir, kv.Key);

                    // Load plugins
                    project.Load(kv.Value, pluginCache, worker);

                    // Add them to list
                    allPlugins.AddRange(project.AllPlugins);

                    projectDict[kv.Key] = project;
                }

                // Populate *.link plugins
                LoadLinks(worker);

                // PostLoad plugins
                foreach (var kv in projectDict)
                    kv.Value.PostLoad();
            }
            catch (SQLiteException except)
            { // Update failure
                string msg = $"SQLite Error : {except.Message}\n\nCache Database is corrupted. Please delete PEBakeryCache.db and restart.";
                MessageBox.Show(msg, "SQLite Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown(1);
            }
        }

        private void LoadLinks(BackgroundWorker worker)
        {
            List<int> removeIdxs = new List<int>();

            // Doing this will consume memory, but also greatly increase performance.
            DB_PluginCache[] cacheDB = null;
            if (pluginCache != null)
                cacheDB = pluginCache.Table<DB_PluginCache>().Where(x => true).ToArray();
                    
            var links = allPlugins.Where(x => x.Type == PluginType.Link);
            Task[] tasks = links.Select(p =>
            {
                return Task.Run(() =>
                {
                    Plugin link = null;
                    bool valid = false;
                    int cached = 2;
                    do
                    {
                        // Load .link's linked plugins with cache
                        string linkPath = p.Sections["Main"].IniDict["Link"];
                        string linkFullPath = Path.Combine(baseDir, linkPath);
                        if (File.Exists(linkFullPath) == false) // Invalid link
                            throw new PluginParseException($"Invalid link path in plugin {p.FullPath}");

                        if (pluginCache != null)
                        { // Case of PluginCache enabled
                            DateTime lastWriteTime = File.GetLastWriteTimeUtc(linkFullPath);
                            DB_PluginCache pCache = cacheDB.FirstOrDefault(x => x.Hash == linkPath.GetHashCode());
                            if (pCache != null && 
                                pCache.Path.Equals(linkPath, StringComparison.Ordinal) &&
                                DateTime.Equals(pCache.LastWriteTime, lastWriteTime))
                            {
                                try
                                {
                                    using (MemoryStream memStream = new MemoryStream(pCache.Serialized))
                                    {
                                        BinaryFormatter formatter = new BinaryFormatter();
                                        link = (Plugin)formatter.Deserialize(memStream);
                                    }
                                    link.Project = p.Project;
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
                            link = new Plugin(type, Path.Combine(baseDir, linkFullPath), p.Project, projectRoot, null, false);

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

                    if (valid)
                    {
                        p.LinkLoaded = true;
                        p.Link = link;
                        if (worker != null)
                            worker.ReportProgress(cached, Path.GetDirectoryName(p.ShortPath));
                    }
                    else
                    {
                        int idx = allPlugins.IndexOf(p);
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
                allPlugins.RemoveAt(idx);
        }
    }
    #endregion

    #region Project
    public class Project : ICloneable
    {
        // Fields
        private readonly string projectName;
        private readonly string projectRoot;
        private readonly string projectDir;
        private readonly string baseDir;
        private Plugin mainPlugin;
        private List<Plugin> allPlugins;
        private Variables variables;
        public const int MainLevel = -256;  // Reserved level for script.project

        private int loadedPluginCount;
        private int allPluginCount;

        // Properties
        public string ProjectName => projectName;
        public string ProjectRoot => projectRoot;
        public string ProjectDir => projectDir; 
        public string BaseDir => baseDir; 
        public Plugin MainPlugin => mainPlugin;
        public List<Plugin> AllPlugins => allPlugins;
        public List<Plugin> ActivePlugins => CollectActivePlugins(allPlugins);
        public List<Plugin> VisiblePlugins => CollectVisiblePlugins(allPlugins);
        public Variables Variables { get => variables; set => variables = value; }
        public int LoadedPluginCount => loadedPluginCount; 
        public int AllPluginCount => allPluginCount; 

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
        public void Load(List<string> allPluginPathList, PluginCache pluginCache, BackgroundWorker worker)
        {
            ReaderWriterLockSlim listLock = new ReaderWriterLockSlim();
            string mainPluginPath = Path.Combine(projectDir, "script.project");
            allPlugins = new List<Plugin>();

            // Doing this will consume memory, but also greatly increase performance.
            DB_PluginCache[] cacheDB = null;
            if (pluginCache != null)
                cacheDB = pluginCache.Table<DB_PluginCache>().Where(x => true).ToArray();

            // Load plugins from disk or cache
            Task[] tasks = allPluginPathList.Select(pPath =>
            {
                return Task.Run(() =>
                {
                    int cached = 0;
                    Plugin p = null;
                    try
                    {
                        if (pluginCache != null)
                        { // PluginCache enabled
                            DateTime lastWriteTime = File.GetLastWriteTimeUtc(pPath);
                            string sPath = pPath.Remove(0, baseDir.Length + 1); // 1 for \
                            DB_PluginCache pCache = cacheDB.FirstOrDefault(x => x.Hash == sPath.GetHashCode());
                            if (pCache != null &&
                                pCache.Path.Equals(sPath, StringComparison.Ordinal) &&
                                DateTime.Equals(pCache.LastWriteTime, lastWriteTime))
                            { // Cache Hit
                                try
                                {
                                    using (MemoryStream memStream = new MemoryStream(pCache.Serialized))
                                    {
                                        BinaryFormatter formatter = new BinaryFormatter();

                                        p = formatter.Deserialize(memStream) as Plugin;
                                        p.Project = this;
                                        cached = 1;
                                    }
                                }
                                catch { } // Cache Error
                            }
                        }

                        if (p == null)
                        { // Cache Miss
                            int? level = null;
                            if (pPath.Equals(mainPluginPath, StringComparison.Ordinal))
                                level = MainLevel;

                            // TODO : Lazy loading of link, takes too much time at start
                            string ext = Path.GetExtension(pPath);
                            PluginType type = PluginType.Plugin;
                            if (ext.Equals(".link", StringComparison.OrdinalIgnoreCase))
                                type = PluginType.Link;
                            p = new Plugin(type, pPath, this, projectRoot, level, false);

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
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MainWindow w = Application.Current.MainWindow as MainWindow;
                            w.Logger.System_Write(new LogInfo(LogState.Error, e));
                        });
                        if (worker != null)
                            worker.ReportProgress(cached);
                    }
                });
            }).ToArray();
            Task.WaitAll(tasks);

            mainPlugin = allPlugins.Where(x => x.Level == MainLevel).FirstOrDefault();
            Debug.Assert(mainPlugin != null);
        }

        public void PostLoad()
        {
            // Sort - Plugin first, Directory last
            allPlugins.Sort((x, y) =>
            {
                if (x.Level == y.Level)
                {
                    if (x.Type == y.Type)
                    {
                        int xDepth = StringHelper.CountOccurrences(x.FullPath, @"\");
                        int yDepth = StringHelper.CountOccurrences(y.FullPath, @"\");
                        if (xDepth == yDepth)
                            return x.FullPath.CompareTo(y.FullPath);
                        else
                            return xDepth - yDepth;
                    }
                    else
                    {
                        return x.Type - y.Type;
                    }
                }
                else
                {
                    return x.Level - y.Level;
                }
            });

            this.Variables = new Variables(this);
        }

        /// <summary>
        /// Return true if error
        /// </summary>
        /// <param name="plugin"></param>
        /// <returns></returns>
        public Plugin RefreshPlugin(Plugin plugin)
        {
            string pPath = plugin.FullPath;
            int idx = AllPlugins.FindIndex(x => string.Equals(x.FullPath, pPath, StringComparison.OrdinalIgnoreCase));
            if (idx == -1)
            {
                // Even if idx is not found in Projects directory, just proceed.
                // If not, cannot deal with monkey-patched plugins.
                return LoadPlugin(pPath, true);
            }
            else
            {
                // This one is in legit Project list, so [Main] cannot be ignored
                Plugin p = LoadPlugin(pPath, false);

                if (p != null)
                    allPlugins[idx] = p;

                return p;
            }
            
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

            Plugin p = LoadPlugin(pPath, ignoreMain);
            if (addToList)
            {
                allPlugins.Add(p);
                allPluginCount += 1;
            }

            return p;
        }

        public Plugin LoadPlugin(string pPath, bool ignoreMain = false)
        {
            Plugin p;
            try
            {
                if (pPath.Equals(Path.Combine(projectRoot, "script.project"), StringComparison.OrdinalIgnoreCase))
                    p = new Plugin(PluginType.Plugin, pPath, this, projectRoot, MainLevel, ignoreMain);
                else
                {
                    string ext = Path.GetExtension(pPath);
                    if (ext.Equals(".link", StringComparison.OrdinalIgnoreCase))
                        p = new Plugin(PluginType.Link, pPath, this, projectRoot, null, false);
                    else
                        p = new Plugin(PluginType.Plugin, pPath, this, projectRoot, null, ignoreMain);
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
        public List<Plugin> GetActivePlugins()
        {
            return CollectActivePlugins(allPlugins);
        }

        private List<Plugin> CollectVisiblePlugins(List<Plugin> allPluginList)
        {
            List<Plugin> visiblePluginList = new List<Plugin>();
            foreach (Plugin p in allPluginList)
            {
                if (p.Level != 0)
                    visiblePluginList.Add(p);
            }
            return visiblePluginList;
        }

        /// <summary>
        /// Filter active plugins from allPlugins dict
        /// Active Plugins : Will-be-processed plugins
        /// </summary>
        /// <remarks>
        /// Took 1ms, Tested on i5-6200U, 151 plugins (Win10PESE 2016-09-01 default)
        /// Time elapsed: 00:00:14.0766582 -> 00:00:14.0767675
        /// 
        /// All Plugins : 155
        /// </remarks>
        private List<Plugin> CollectActivePlugins(List<Plugin> allPluginList)
        {
            List<Plugin> activePluginList = new List<Plugin>();
            foreach (Plugin p in allPluginList)
            {
                bool active = false;
                if (p.Type == PluginType.Plugin)
                {
                    if (p.Selected != SelectedState.None)
                    {
                        if (p.Mandatory || p.Selected == SelectedState.True)
                            active = true;
                    }
                }

                if (active)
                    activePluginList.Add(p);
            }
            return activePluginList;
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

        #region Clone
        public object Clone()
        {
            Project project = new Project(baseDir, projectName)
            {
                mainPlugin = this.mainPlugin,
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
