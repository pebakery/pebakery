/*
    Copyright (C) 2018-2019 Hajin Jang
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

using Newtonsoft.Json;
using PEBakery.Core.ViewModels;
using PEBakery.Helper;
using PEBakery.Ini;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PEBakery.Core
{
    #region PEBakery's new update scheme
    /*
    - script.project
    [Update]
    // ProjectMethod={Static|...}
    ProjectBaseUrl=<Url>
    
    - Per script
    [Update]
    ScriptType={Project|Standalone}
    ScriptUrl=<Url>
    */
    #endregion

    #region Classic updates.ini (No plan to implement)
    // ReSharper disable CommentTypo
    /*
    - Classic updates.ini
    [Updates]
    Win10PESE=Folder
    Tools=Folder

    [Updates\Win10PESE]
    Win10PESE\Apps=Folder
    Win10PESE\Build=Folder

    [Updates\Win10PESE\Apps\Network]
    Win10PESE\Apps\Network\Firewall=Folder
    Win10PESE\Apps\Network\Remote Connect=Folder
    Flash_Add.Script=Projects/Win10PESE/Apps/Network/Flash_Add.Script,93cc0d650b4e1ff459c43d45a531903f,015,Flash#$sAdd,Adds#$sFlash#$sPlayer.,Lancelot,http://TheOven.org,#23082,2,
    Flash_Package.script=Projects/Win10PESE/Apps/Network/Flash_Package.script,bc42776b7140ea8d022b49d5b6c2f0de,030,Flash#$sPackage#$sx86,(v32.0.0.114#$s-#$s(x86#$s18#$sMB))#$sThis#$sis#$sa#$sFlash#$sPackage#$sPlugin#$sto#$sbe#$sused#$sby#$sother#$sPlugins.,Saydin77#$c#$sChrisR,http://TheOven.org,#9821195,0,
    Flash_Package64.script=Projects/Win10PESE/Apps/Network/Flash_Package64.script,a637cba7ddc866126cf903c07f9e4f79,030,Flash#$sPackage#$sx64,(v32.0.0.114#$s-#$s(x64#$s25#$sMB))#$sThis#$sis#$sa#$sFlash#$sPackage#$sPlugin#$sto#$sbe#$sused#$sby#$sother#$sPlugins.,Saydin77#$c#$sChrisR,http://TheOven.org,#11565119,0,
    folder.project=Projects/Win10PESE/Apps/Network/folder.project,5799a43137daa1554d36361da513b9a5,003,Net,Web#$sBrowsers#$sand#$sother#$sInternet#$srelated#$saddons,TheOven#$sChefs#$s(Galapo#$c#$sLancelot),http://TheOven.org,#4375,0,
    Mozilla_Firefox_ESR.Script=Projects/Win10PESE/Apps/Network/Mozilla_Firefox_ESR.Script,0b0a4fcaf7113aa4de40f7c10e1fd7a2,009,Mozilla#$sFirefox#$sESR#$s(P),(x86/x64#$sNT6x)#$sMozilla#$sFirefox#$sESR#$s(Extended#$sSupport#$sRelease).#$sCommitted#$sto#$syou#$c#$syour#$sprivacy#$sand#$san#$sopen#$sWeb.,ChrisR,http://TheOven.org,#3249630,2,
    Mozilla_Firefox_ESR_x64_File.Script=Projects/Win10PESE/Apps/Network/Mozilla_Firefox_ESR_x64_File.Script,797536a97821660f48ea6be36c934d12,003,Mozilla#$sFirefox#$sESR#$s(P)#$s-#$sx64#$sFile,File#$sContainer#$sPlugin,Lancelot,http://TheOven.org,#52183423,2,
    */
    // ReSharper restore CommentTypo
    #endregion

    #region ProjectUpdateInfo
    public class ProjectUpdateInfo
    {
        #region Const
        public static class Const
        {
            public const string ProjectUpdateSection = @"ProjectUpdate";
            public const string SelectedChannel = @"SelectedChannel";
            public const string BaseUrl = @"BaseUrl";
        }
        #endregion

        #region Fields and Properties
        public bool Empty => SelectedChannel == null || BaseUrl == null;
        public string SelectedChannel { get; }
        public string BaseUrl { get; }
        #endregion

        #region Constructor
        public ProjectUpdateInfo()
        {
            SelectedChannel = null;
            BaseUrl = null;
        }

        public ProjectUpdateInfo(string selectedChannel, string baseUrl)
        {
            // If baseUrl is not a proper uri, throw an exception
            if (StringHelper.GetUriProtocol(baseUrl) == null)
                throw new ArgumentException(nameof(baseUrl));

            SelectedChannel = selectedChannel ?? throw new ArgumentNullException(nameof(selectedChannel));
            BaseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
        }
        #endregion
    }
    #endregion

    #region FileHelper
    public class FileUpdater
    {
        #region Fields and Properties
        private readonly Project _p;
        private readonly MainViewModel _m;
        private readonly HttpFileDownloader _downloader;

        private readonly List<LogInfo> _logs = new List<LogInfo>();
        public LogInfo[] Logs
        {
            get
            {
                LogInfo[] logArray = _logs.ToArray();
                _logs.Clear();
                return logArray;
            }
        }
        #endregion

        #region Constructor
        public FileUpdater(Project p, MainViewModel mainViewModel, string customUserAgent)
        {
            _p = p;
            _m = mainViewModel;

            string userAgent = customUserAgent ?? Engine.DefaultUserAgent;
            _downloader = new HttpFileDownloader(_m, 10, userAgent);
        }
        #endregion

        #region Update one or more scripts
        public Script UpdateScript(Script sc, bool preserveScriptState)
        {
            if (!sc.IsUpdateable)
                return null;

            // Backup interface state of original script
            ScriptStateBackup stateBackup = null;
            if (preserveScriptState)
            {
                stateBackup = BackupScriptState(sc);
                Debug.Assert(stateBackup != null, "ScriptStateBackup is null");
            }

            Script newScript;
            _m?.SetBuildCommandProgress("Download Progress");
            try
            {
                if (_m != null)
                    _m.BuildEchoMessage = $"Updating script [{sc.Title}]...";
                newScript = InternalUpdateOneScript(sc, stateBackup);
            }
            finally
            {
                _m?.ResetBuildCommandProgress();
                if (_m != null)
                    _m.BuildEchoMessage = string.Empty;
            }
            return newScript;
        }

        public List<Script> UpdateScripts(IReadOnlyList<Script> scripts, bool preserveScriptState)
        {
            // Get updateable scripts urls
            Script[] updateableScripts = scripts.Where(s => s.IsUpdateable).ToArray();

            List<Script> newScripts = new List<Script>(updateableScripts.Length);

            if (_m != null)
                _m.BuildScriptProgressVisibility = Visibility.Collapsed;
            _m?.SetBuildCommandProgress("Download Progress");
            try
            {
                int i = 0;
                foreach (Script sc in updateableScripts)
                {
                    i++;

                    ScriptStateBackup stateBackup = null;
                    if (preserveScriptState)
                    {
                        stateBackup = BackupScriptState(sc);
                        Debug.Assert(stateBackup != null, "ScriptStateBackup is null");
                    }

                    if (_m != null)
                        _m.BuildEchoMessage = $"Updating script [{sc.Title}]... ({i}/{updateableScripts.Length})";
                    Script newScript = InternalUpdateOneScript(sc, stateBackup);
                    if (newScript != null)
                        newScripts.Add(newScript);
                }
            }
            finally
            {
                _m?.ResetBuildCommandProgress();
                if (_m != null)
                {
                    _m.BuildEchoMessage = string.Empty;
                    _m.BuildScriptProgressVisibility = Visibility.Visible;
                }
            }

            return newScripts;
        }

        private Script InternalUpdateOneScript(Script sc, ScriptStateBackup stateBackup)
        {
            // Parse version of local script
            VersionEx localSemVer = VersionEx.Parse(sc.TidyVersion);
            if (localSemVer == null) // Never be triggered, because constructor of Script class check it
            {
                _logs.Add(new LogInfo(LogState.Error, $"Local script [{sc.Title}] does not provide proper version information"));
                return null;
            }

            string updateUrl = sc.UpdateUrl;
            string metaJsonUrl = Path.ChangeExtension(updateUrl, ".meta.json");
            string metaJsonFile = FileHelper.GetTempFile();
            string tempScriptFile = FileHelper.GetTempFile();
            try
            {
                // Download .meta.json
                HttpFileDownloader.Report report = DownloadFile(metaJsonUrl, metaJsonFile);
                if (!report.Result)
                {
                    _logs.Add(new LogInfo(LogState.Error, $"Update is not available for [{sc.Title}]"));
                    return null;
                }

                // Check .meta.json
                (MetaJsonRoot metaJson, string errMsg) = CheckMetaJson(metaJsonFile);
                if (metaJson == null)
                {
                    _logs.Add(new LogInfo(LogState.Error, errMsg));
                    return null;
                }
                if (metaJson.ScriptMain.ParsedVersion <= localSemVer)
                {
                    _logs.Add(new LogInfo(LogState.Error, $"Update is not available for [{sc.Title}]"));
                    return null;
                }

                // Download .scripts
                report = DownloadFile(updateUrl, tempScriptFile);
                if (!report.Result)
                {
                    LogInfo.LogErrorMessage(_logs, report.ErrorMsg);
                    return null;
                }

                // Check if remote script is valid
                Script remoteScript = _p.LoadScriptRuntime(tempScriptFile, new LoadScriptRuntimeOptions
                {
                    IgnoreMain = false,
                    AddToProjectTree = false,
                    OverwriteToProjectTree = false,
                });
                if (remoteScript == null)
                {
                    LogInfo.LogErrorMessage(_logs, $"Remote script [{sc.Title}] is corrupted");
                    return null;
                }

                // Check downloaded script's version and check
                string newVerStr = IniReadWriter.ReadKey(tempScriptFile, "Main", "Version");
                VersionEx remoteSemVer = VersionEx.Parse(newVerStr);
                if (remoteSemVer == null)
                {
                    _logs.Add(new LogInfo(LogState.Error, $"Remote script [{sc.Title}] does not provide proper version information"));
                    return null;
                }
                if (!remoteSemVer.Equals(metaJson.ScriptMain.ParsedVersion))
                {
                    _logs.Add(new LogInfo(LogState.Error, $"Version of remote script [{sc.Title}] is inconsistent with .meta.json"));
                    return null;
                }
                if (remoteSemVer <= localSemVer)
                {
                    _logs.Add(new LogInfo(LogState.Error, $"Remote script [{sc.Title}] ({remoteSemVer}) is not newer than local script ({localSemVer})"));
                    return null;
                }

                // Overwrite backup state to new script
                if (stateBackup != null)
                {
                    RestoreScriptState(remoteScript, stateBackup);
                }

                // Copy remote scripts into new script
                File.Copy(tempScriptFile, sc.DirectRealPath, true);
                Script newScript = _p.RefreshScript(sc);
                if (newScript == null)
                {
                    _logs.Add(new LogInfo(LogState.Error, $"Remote script {sc.Title} is corrupted"));
                    return null;
                }

                // Return updated script instance
                _logs.Add(new LogInfo(LogState.Success,
                    $"Updated script [{sc.Title}] to [v{sc.RawVersion}] from [v{newScript.RawVersion}]"));
                return newScript;
            }
            finally
            {
                if (File.Exists(metaJsonFile))
                    File.Delete(metaJsonFile);
                if (File.Exists(tempScriptFile))
                    File.Delete(tempScriptFile);
            }
        }
        #endregion

        #region UpdateProject
        public void UpdateProject()
        {
            // List<LogInfo> logs = new List<LogInfo>();
            // return logs;
        }
        #endregion

        #region Backup & Restore Script
        private class ScriptStateBackup
        {
            public readonly SelectedState Selected;
            public readonly Dictionary<string, List<UIControl>> IfaceSectionDict;

            public ScriptStateBackup(SelectedState selected, Dictionary<string, List<UIControl>> ifaceSectionDict)
            {
                Selected = selected;
                IfaceSectionDict = ifaceSectionDict ?? new Dictionary<string, List<UIControl>>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static ScriptStateBackup BackupScriptState(Script sc)
        {
            List<string> ifaceSectionNames = sc.GetInterfaceSectionNames(false);
            Dictionary<string, List<UIControl>> ifaceDict =
                new Dictionary<string, List<UIControl>>(ifaceSectionNames.Count, StringComparer.OrdinalIgnoreCase);

            foreach (string ifaceSectionName in ifaceSectionNames)
            {
                // Unable to interface section
                Debug.Assert(ifaceSectionName != null, $"Internal error at {nameof(BackupScriptState)}");

                // Get uiCtrls of a script
                (List<UIControl> uiCtrls, _) = sc.GetInterfaceControls(ifaceSectionName);
                if (uiCtrls == null) // Mostly [Interface] section does not exist -> return empty ifaceDict
                    return new ScriptStateBackup(sc.Selected, ifaceDict);

                // Collect uiCtrls which have value
                List<UIControl> valueCtrls = new List<UIControl>(uiCtrls.Count);
                foreach (UIControl uiCtrl in uiCtrls)
                {
                    string value = uiCtrl.GetValue(false);
                    if (value != null)
                        valueCtrls.Add(uiCtrl);
                }

                ifaceDict[ifaceSectionName] = valueCtrls;
            }

            return new ScriptStateBackup(sc.Selected, ifaceDict);
        }

        /// <summary>
        /// To the best-effort to restore script state
        /// </summary>
        /// <remarks>
        /// This method does not refresh the Script instance, its responsibility of callee
        /// </remarks>
        /// <param name="sc"></param>
        /// <param name="backup"></param>
        private static void RestoreScriptState(Script sc, ScriptStateBackup backup)
        {
            List<string> ifaceSectionNames = sc.GetInterfaceSectionNames(false);

            // Restore selected state
            IniReadWriter.WriteKey(sc.RealPath, ScriptSection.Names.Main, Script.Const.Selected, backup.Selected.ToString());

            // Restore interfaces
            List<UIControl> newCtrls = new List<UIControl>();
            foreach (var kv in backup.IfaceSectionDict)
            {
                string ifaceSectionName = kv.Key;
                List<UIControl> bakCtrls = kv.Value;

                if (!ifaceSectionNames.Contains(ifaceSectionName))
                    continue;

                (List<UIControl> uiCtrls, _) = sc.GetInterfaceControls(ifaceSectionName);
                foreach (UIControl uiCtrl in uiCtrls)
                {
                    // Get old uiCtrl, equality identified by Type and Key.
                    UIControl bakCtrl = bakCtrls.FirstOrDefault(bak => bak.Type == uiCtrl.Type && bak.Key.Equals(uiCtrl.Key, StringComparison.OrdinalIgnoreCase));
                    if (bakCtrl == null)
                        continue;

                    // Get old value
                    string bakValue = bakCtrl.GetValue(false);
                    Debug.Assert(bakValue != null, "Internal Logic Error at FileUpdater.RestoreInterface");

                    // Add to newCtrls only if apply was successful
                    if (uiCtrl.SetValue(bakValue, false, out _))
                        newCtrls.Add(uiCtrl);
                }
            }

            // Write to file
            UIControl.Update(newCtrls);
        }
        #endregion

        #region MetaJsonRoot class (.meta.json)
        /// <summary>
        /// Check .meta.json
        /// </summary>
        /// <param name="metaJsonFile"></param>
        public (MetaJsonRoot MetaJson, string ErrorMsg) CheckMetaJson(string metaJsonFile)
        {
            // Prepare JsonSerializer
            JsonSerializerSettings settings = new JsonSerializerSettings { Culture = CultureInfo.InvariantCulture };
            JsonSerializer serializer = JsonSerializer.Create(settings);

            // Read json file
            MetaJsonRoot jsonRoot;
            try
            {
                using (StreamReader sr = new StreamReader(metaJsonFile, Encoding.UTF8, false))
                using (JsonTextReader jr = new JsonTextReader(sr))
                {
                    jsonRoot = serializer.Deserialize<MetaJsonRoot>(jr);
                }
            }
            catch (JsonReaderException)
            {
                return (null, "Remote script meta file is corrupted");
            }

            if (!jsonRoot.CheckSchema(out string errorMsg))
                return (null, errorMsg);

            if (!jsonRoot.ScriptMain.CheckSchema(out errorMsg))
                return (null, errorMsg);

            return (jsonRoot, null);
        }

        public void CreateMetaJson(Script sc, string destJsonFile)
        {
            // Create MetaJsonRoot instance
            MetaJsonRoot jsonRoot = new MetaJsonRoot
            {
                MetaSchemaVer = Global.Const.MetaSchemaVerStr,
                PEBakeryMinVer = Global.Const.ProgramVersionStr,
                ScriptMain = new MetaJsonScriptMain
                {
                    Title = sc.Title,
                    Desc = sc.Description,
                    Author = sc.Author,
                    Version = sc.RawVersion,
                }
            };

            // Validate MetaJsonRoot instance (Debug-mode only)
            const string assertErrorMessage = "Incorrect MetaJsonRoot instance creation";
            Debug.Assert(jsonRoot.MetaSchemaVer != null, assertErrorMessage);
            Debug.Assert(jsonRoot.PEBakeryMinVer != null, assertErrorMessage);
            Debug.Assert(jsonRoot.ScriptMain.Title != null, assertErrorMessage);
            Debug.Assert(jsonRoot.ScriptMain.Desc != null, assertErrorMessage);
            Debug.Assert(jsonRoot.ScriptMain.Author != null, assertErrorMessage);
            Debug.Assert(jsonRoot.ScriptMain.Version != null, assertErrorMessage);

            // Prepare JsonSerializer
            JsonSerializerSettings settings = new JsonSerializerSettings { Culture = CultureInfo.InvariantCulture };
            JsonSerializer serializer = JsonSerializer.Create(settings);

            using (StreamWriter sw = new StreamWriter(destJsonFile, false, Encoding.UTF8))
            using (JsonTextWriter jw = new JsonTextWriter(sw))
            {
#if DEBUG
                // https://www.newtonsoft.com/json/help/html/ReducingSerializedJSONSize.htm
                jw.Formatting = Formatting.Indented;
                jw.Indentation = 4;
#else
                jw.Formatting = Formatting.None;
#endif
                serializer.Serialize(jw, jsonRoot);
            }
        }

        // ReSharper disable once ClassNeverInstantiated.Local
        public class MetaJsonRoot
        {
            [JsonProperty(PropertyName = "meta_schema_ver")]
            public string MetaSchemaVer { get; set; }
            [JsonProperty(PropertyName = "pebakery_min_ver")]
            public string PEBakeryMinVer { get; set; }
            [JsonProperty(PropertyName = "hash_sha256")]
            public string HashSHA256 { get; set; }
            [JsonProperty(PropertyName = "script_main")]
            public MetaJsonScriptMain ScriptMain { get; set; }

            [JsonIgnore]
            private static readonly VersionEx SchemaParseVer = Global.Const.MetaSchemaVerInst;

            /// <summary>
            /// Return true if schema is valid
            /// </summary>
            /// <returns></returns>
            public bool CheckSchema(out string errorMsg)
            {
                errorMsg = string.Empty;

                // Check if properties are not null
                if (MetaSchemaVer == null || PEBakeryMinVer == null || ScriptMain == null)
                {
                    errorMsg = "Meta file of remote script is corrupted";
                    return false;
                }

                // Check if version string are valid
                VersionEx metaSchemaVer = VersionEx.Parse(MetaSchemaVer);
                VersionEx engineMinVer = VersionEx.Parse(PEBakeryMinVer);
                if (metaSchemaVer == null)
                {
                    errorMsg = "Meta file of remote script is corrupted";
                    return false;
                }
                if (engineMinVer == null)
                {
                    errorMsg = "Meta file of remote script is corrupted";
                    return false;
                }

                // Check meta_schema_ver
                if (SchemaParseVer < metaSchemaVer)
                {
                    errorMsg = "Meta file of remote script requires newer version of PEBakery";
                    return false;
                }

                // Check pebakery_min_ver
                if (Global.Const.ProgramVersionInst < engineMinVer)
                {
                    errorMsg = $"Remote script requires PEBakery {Global.Const.ProgramVersionStr} or higher";
                    return false;
                }

                return true;
            }
        }

        public class MetaJsonScriptMain
        {
            [JsonProperty(PropertyName = "title")]
            public string Title { get; set; }
            [JsonProperty(PropertyName = "desc")]
            public string Desc { get; set; }
            [JsonProperty(PropertyName = "author")]
            public string Author { get; set; }
            [JsonProperty(PropertyName = "version")]
            public string Version { get; set; }

            [JsonIgnore]
            private VersionEx _parsedVersion;
            [JsonIgnore]
            public VersionEx ParsedVersion => _parsedVersion ?? (_parsedVersion = VersionEx.Parse(Version));

            /// <summary>
            /// Return true if schema is valid
            /// </summary>
            /// <returns></returns>
            public bool CheckSchema(out string errorMsg)
            {
                errorMsg = string.Empty;

                // Check if properties are not null
                if (Title == null || Desc == null || Author == null || ParsedVersion == null)
                {
                    errorMsg = "Meta file of remote script is corrupted";
                    return false;
                }

                return true;
            }
        }
        #endregion

        #region Utility
        private HttpFileDownloader.Report DownloadFile(string url, string destFile)
        {
            try
            {
                Task<HttpFileDownloader.Report> task = _downloader.Download(url, destFile);
                task.Wait();

                return task.Result;
            }
            catch (Exception e)
            {
                return new HttpFileDownloader.Report(false, 0, Logger.LogExceptionMessage(e));
            }
        }
        #endregion
    }
    #endregion
}