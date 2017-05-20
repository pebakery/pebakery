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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using PEBakery.Lib;
using PEBakery.Exceptions;
using System.Security.Cryptography;
using System.Runtime.Serialization.Formatters.Binary;
using SQLite.Net;
using System.Windows;
using PEBakery.WPF;

namespace PEBakery.Core
{
    public class ProjectCollection
    {
        // Fields
        private readonly string baseDir;
        private readonly string projectRoot;
        private readonly Dictionary<string, Project> projectDict;
        private readonly PluginCache pluginCache;

        private readonly Dictionary<string, List<string>> pluginPathDict;
        private readonly List<Plugin> allPluginList;
        private readonly List<string> allPluginPathList;

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
            this.allPluginList = new List<Plugin>();
            this.allPluginPathList = new List<string>();
        }

        public int PrepareLoad(out int processCount)
        {
            // Ex) projNameList = { "Win7PESE", "Win8PESE", "Win8.1SE", "Win10PESE" }
            // Ex) pluginPathDict = [plugin paths of Win7PESE, plugin paths of Win8PESE, ... ]
            List<string> projNameList = GetProjectNameList();
            GetPluginPaths(projNameList, out processCount);

            // Return count of all plugins
            return allPluginPathList.Count;
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
                allPluginPathList.AddRange(pluginPathList);
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
                    allPluginList.AddRange(project.AllPluginList);

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
                    
            var links = allPluginList.Where(x => x.Type == PluginType.Link);
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
                            link = new Plugin(type, Path.Combine(baseDir, linkFullPath), p.Project, projectRoot, null);

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
                        worker.ReportProgress(cached, Path.GetDirectoryName(p.ShortPath));
                    }
                    else
                    {
                        int idx = allPluginList.IndexOf(p);
                        removeIdxs.Add(idx);
                        worker.ReportProgress(cached);
                    }
                });
            }).ToArray();
            Task.WaitAll(tasks);

            // Remove malformed link
            var idxs = removeIdxs.OrderByDescending(x => x);
            foreach (int idx in idxs)
                allPluginList.RemoveAt(idx);
        }
    }

    public class Project
    {
        // Fields
        private readonly string projectName;
        private readonly string projectRoot;
        private readonly string projectDir;
        private readonly string baseDir;
        private Plugin mainPlugin;
        private List<Plugin> allPluginList;
        private Tree<Plugin> allPlugins;
        private Tree<Plugin> visiblePlugins;
        private Variables variables;
        public const int MainLevel = -256;  // Reserved level for script.project

        private int loadedPluginCount;
        private int allPluginCount;

        // Properties
        public string ProjectName { get { return projectName; } }
        public string ProjectDir { get { return projectDir; } }
        public string BaseDir { get => baseDir; }
        public Plugin MainPlugin { get { return mainPlugin; } }
        public List<Plugin> AllPluginList { get { return allPluginList; } }
        public Tree<Plugin> AllPlugins { get { return allPlugins; } }
        public Tree<Plugin> VisiblePlugins { get => visiblePlugins; }
        public Variables Variables { get => variables; set => variables = value; }
        public int LoadedPluginCount { get => loadedPluginCount; }
        public int AllPluginCount { get => allPluginCount; }

        public Project(string baseDir, string projectName)
        {
            this.loadedPluginCount = 0;
            this.allPluginCount = 0;
            this.projectName = projectName;
            this.projectRoot = Path.Combine(baseDir, "Projects");
            this.projectDir = Path.Combine(baseDir, "Projects", projectName);
            this.baseDir = baseDir;
        }

        public void Load(List<string> allPluginPathList, PluginCache pluginCache, BackgroundWorker worker)
        {
            ReaderWriterLockSlim listLock = new ReaderWriterLockSlim();
            string mainPluginPath = Path.Combine(projectDir, "script.project");
            allPluginList = new List<Plugin>();

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
                            {
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
                                catch { }
                            }
                        }

                        if (p == null)
                        {
                            int? level = null;
                            if (pPath.Equals(mainPluginPath, StringComparison.Ordinal))
                                level = MainLevel;

                            // TODO : Lazy loading of link, takes too much time at start
                            string ext = Path.GetExtension(pPath);
                            PluginType type = PluginType.Plugin;
                            if (ext.Equals(".link", StringComparison.OrdinalIgnoreCase))
                                type = PluginType.Link;
                            p = new Plugin(type, pPath, this, projectRoot, level);

                            Debug.Assert(p != null);
                        }

                        listLock.EnterWriteLock();
                        try
                        {
                            allPluginList.Add(p);
                        }
                        finally
                        {
                            listLock.ExitWriteLock();
                        }

                        worker.ReportProgress(cached, Path.GetDirectoryName(p.ShortPath));
                    }
                    catch (Exception e)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MainWindow w = Application.Current.MainWindow as MainWindow;
                            w.Logger.System_Write(new LogInfo(LogState.Error, e));
                        });
                        worker.ReportProgress(cached);
                    }
                });
            }).ToArray();
            Task.WaitAll(tasks);

            allPluginList = allPluginList.OrderBy(x => x.DirectFullPath).ToList();
            mainPlugin = allPluginList.Where(x => x.Level == MainLevel).FirstOrDefault();
            Debug.Assert(mainPlugin != null);
        }

        public void PostLoad()
        {
            this.allPlugins = PluginListToTree(allPluginList);
            List<Plugin> visiblePluginList = CollectVisiblePlugins(allPluginList);
            this.visiblePlugins = PluginListToTree(visiblePluginList);
            this.Variables = new Variables(this);
        }

        public Tree<Plugin> GetActivePlugin()
        {
            List<Plugin> activePluginList = CollectActivePlugins(allPluginList);
            return PluginListToTree(activePluginList);
        }

        public List<Plugin> GetActivePluginList()
        {
            return CollectActivePlugins(allPluginList);
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

        private string PathKeyGenerator(string[] paths, int last)
        { // last - start entry is 0
            StringBuilder builder = new StringBuilder();
            builder.Append(paths[0]);
            for (int i = 1; i <= last; i++)
            {
                builder.Append(Path.DirectorySeparatorChar);
                builder.Append(paths[i]);
            }
            return builder.ToString();
        }

        private Tree<Plugin> PluginListToTree(List<Plugin> pList)
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
                        Plugin dirPlugin = new Plugin(PluginType.Directory, Path.Combine(projectRoot, projectName, pathKey), this, projectRoot, p.Level);
                        nodeId = pTree.AddNode(nodeId, dirPlugin);
                        dirDict[key] = nodeId;
                    }
                }
                Debug.Assert(p != null);
                pTree.AddNode(nodeId, p);
            }

            // Sort - Plugin first, Directory last
            pTree.Sort((x, y) => {
                if (x.Data.Level == y.Data.Level)
                {
                    if (x.Data.Type == y.Data.Type)
                        return x.Data.FullPath.CompareTo(y.Data.FullPath);
                    else
                        return x.Data.Type - y.Data.Type;
                }
                else
                    return x.Data.Level - y.Data.Level;
            });

            // Reflect Directory's Selected value
            RecursiveDecideDirectorySelectedValue(pTree.Root, 0);

            return pTree;
        }

        // TODO: It violates Tree<T>'s abstraction...
        private SelectedState RecursiveDecideDirectorySelectedValue(List<Node<Plugin>> list, int depth)
        {
            SelectedState final = SelectedState.None;
            foreach (Node<Plugin> node in list)
            {
                if (0 < node.Child.Count)
                { // Has child plugins
                    SelectedState state = RecursiveDecideDirectorySelectedValue(node.Child, depth + 1);
                    if (depth != 0)
                    {
                        if (state == SelectedState.True)
                            final = node.Data.Selected = SelectedState.True;
                        else if (state == SelectedState.False)
                        {
                            if (final != SelectedState.True)
                                final = SelectedState.False;
                            if (node.Data.Selected != SelectedState.True)
                                node.Data.Selected = SelectedState.False;
                        }
                    }
                }
                else // Does not have child plugin
                {
                    switch (node.Data.Selected)
                    {
                        case SelectedState.True:
                            final = SelectedState.True;
                            break;
                        case SelectedState.False:
                            if (final == SelectedState.None)
                                final = SelectedState.False;
                            break;
                    }
                }
            }

            return final;
        }

        /// <summary>
        /// Return true if error
        /// </summary>
        /// <param name="plugin"></param>
        /// <returns></returns>
        public Plugin RefreshPlugin(Plugin plugin)
        {
            int idx = AllPluginList.FindIndex(x => string.Equals(x.FullPath, plugin.FullPath, StringComparison.OrdinalIgnoreCase));
            if (idx == -1)
                return null;

            Node<Plugin> node = allPlugins.SearchNode(plugin);
            string pPath = plugin.FullPath;
            Plugin p;
            try
            {
                if (string.Equals(pPath, Path.Combine(projectRoot, "script.project"), StringComparison.OrdinalIgnoreCase))
                    p = new Plugin(PluginType.Plugin, pPath, this, projectRoot, MainLevel);
                else
                {
                    string ext = Path.GetExtension(pPath);
                    if (string.Equals(ext, ".link", StringComparison.OrdinalIgnoreCase))
                        p = new Plugin(PluginType.Link, pPath, this, projectRoot, null);
                    else
                        p = new Plugin(PluginType.Plugin, pPath, this, projectRoot, null);
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

            allPluginList[idx] = p;
            node.Data = p;
            return p;
        }

        public Plugin GetPluginByFullPath(string fullPath)
        {
            return AllPluginList.Find(x => string.Equals(x.FullPath, fullPath, StringComparison.OrdinalIgnoreCase));
        }

        public Plugin DeterminePlugin(EngineState s, string pluginFile)
        {
            bool inCurrentPlugin = false;
            if (pluginFile.Equals("%PluginFile%", StringComparison.OrdinalIgnoreCase))
                inCurrentPlugin = true;
            else if (pluginFile.Equals("%ScriptFile%", StringComparison.OrdinalIgnoreCase))
                inCurrentPlugin = true;

            Plugin targetPlugin;
            if (inCurrentPlugin)
            {
                targetPlugin = s.CurrentPlugin;
            }
            else
            {
                string fullPath = StringEscaper.ExpandVariables(s, pluginFile);
                targetPlugin = s.Project.GetPluginByFullPath(fullPath);
                if (targetPlugin == null)
                    throw new ExecuteException($"No plugin in [{fullPath}]");
            }

            return targetPlugin;
        }
    }
}
