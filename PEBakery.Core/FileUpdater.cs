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
using Newtonsoft.Json.Converters;
using PEBakery.Core.ViewModels;
using PEBakery.Helper;
using PEBakery.Ini;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PEBakery.Core
{
    #region (Docs) PEBakery Update Scheme
    /*
    - script.project
    [Update]
    // ProjectMethod={Static|...}
    ProjectBaseUrl=<Url>
    
    - Per script
    [Update]
    ScriptType={Project|Standalone}
    ScriptUrl=<Url>

    - Structure
    Each script has its pair script meta file. (ABC.script -> ABC.meta.json)
    Deleted script marks its deletiton with empty .deleted file. (ABC.script -> ABC.deleted)
    */
    #endregion

    #region (Docs) Script meta file (*.meta.json)
    /*
    Example of *.meta.json
    {
        "meta_schema_ver": "0.1",
        "pebakery_min_ver": "0.9.6",
        "hash_sha256": "ObyT2vDKEsNzvEPNCrd7TTwXtmsYvbxXr3kKY4obXsE=",
        "script_main": {
            "title": "PreserveInterface",
            "desc": "Standalone PreserveInterface",
            "author": "ied206",
            "version": "1.2"
        }
    }
    */
    #endregion

    #region (Docs) ProjectMetaJson (project.meta.json)
    /*
    Example of *.meta.json
    {
        "meta_schema_ver": "0.1",
        "pebakery_min_ver": "0.9.6",
        "hash_sha256": "ObyT2vDKEsNzvEPNCrd7TTwXtmsYvbxXr3kKY4obXsE=",
        "script_main": {
            "title": "PreserveInterface",
            "desc": "Standalone PreserveInterface",
            "author": "ied206",
            "version": "1.2"
        }
    }
    */
    #endregion

    #region (Docs) Classic updates.ini (No plan to implement)
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
        public bool IsUpdateable => SelectedChannel != null && BaseUrl != null;
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

    #region FileUpdater
    public class FileUpdater
    {
        #region Fields and Properties
        private readonly Project _p;
        private readonly MainViewModel _m;
        private readonly HttpFileDownloader _downloader;

        private readonly List<LogInfo> _logs = new List<LogInfo>();
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

        #region ReadAndClearLogs
        public LogInfo[] ReadAndClearLogs()
        {
            LogInfo[] logArray = _logs.ToArray();
            _logs.Clear();
            return logArray;
        }
        #endregion

        #region Update one or more scripts
        public Task<Script> UpdateScriptAsync(Script sc, bool preserveScriptState)
        {
            return Task.Run(() => UpdateScript(sc, preserveScriptState));
        }

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
            _m?.SetBuildCommandProgress("Download Progress", 1);

            try
            {
                if (_m != null)
                    _m.BuildEchoMessage = $"Updating script [{sc.Title}]...";

                newScript = InternalUpdateOneScript(sc, stateBackup);

                if (_m != null)
                    _m.BuildCommandProgressValue = 1;
            }
            finally
            {
                _m?.ResetBuildCommandProgress();
                if (_m != null)
                    _m.BuildEchoMessage = string.Empty;
            }
            return newScript;
        }

        public Task<List<Script>> UpdateScriptsAsync(IReadOnlyList<Script> scripts, bool preserveScriptState)
        {
            return Task.Run(() => UpdateScripts(scripts, preserveScriptState));
        }

        public List<Script> UpdateScripts(IReadOnlyList<Script> scripts, bool preserveScriptState)
        {
            // Get updateable scripts urls
            Script[] updateableScripts = scripts.Where(s => s.IsUpdateable).ToArray();

            List<Script> newScripts = new List<Script>(updateableScripts.Length);

            if (_m != null)
                _m.BuildScriptProgressVisibility = Visibility.Collapsed;
            _m?.SetBuildCommandProgress("Download Progress", scripts.Count);
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
                    if (_m != null)
                        _m.BuildCommandProgressValue += 1;
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
            // Never be triggered, because Script class constructor check it
            Debug.Assert(sc.ParsedVersion != null, $"Local script [{sc.Title}] does not provide proper version information");

            string updateUrl = sc.UpdateUrl;
            string metaJsonUrl = Path.ChangeExtension(updateUrl, ".meta.json");
            string metaJsonFile = FileHelper.GetTempFile(".meta.json");
            string tempScriptFile = FileHelper.GetTempFile(".script");
            try
            {
                // Download .meta.json
                HttpFileDownloader.Report report = DownloadFile(metaJsonUrl, metaJsonFile);
                if (!report.Result)
                {
                    if (report.StatusCode == 0)
                    {  // Failed to send a request, such as network not available
                        _logs.Add(new LogInfo(LogState.Error, $"Unable to connect to the server"));
                        return null;
                    }

                    // Try downloading .deleted to check if a script is deleted
                    string deletedUrl = Path.ChangeExtension(updateUrl, ".deleted");
                    string deletedFile = Path.ChangeExtension(metaJsonFile, ".deleted");
                    try
                    {
                        report = DownloadFile(deletedUrl, deletedFile);
                        if (report.Result)
                        { // Successfully received response
                            if (report.StatusCode == 200) // .deleted file exists in the server
                                _logs.Add(new LogInfo(LogState.Error, $"[{sc.Title}] was deleted from the server"));
                            else // There is no .deleted file in the server
                                _logs.Add(new LogInfo(LogState.Error, $"Update is not available for [{sc.Title}]"));
                        }
                        else
                        {
                            if (report.StatusCode == 0) // Failed to send a request, such as network not available
                                _logs.Add(new LogInfo(LogState.Error, $"Unable to connect to the server"));
                            else
                                _logs.Add(new LogInfo(LogState.Error, $"Update is not available for [{sc.Title}]"));
                        }
                    }
                    finally
                    {
                        if (File.Exists(deletedFile))
                            File.Delete(deletedFile);
                    }

                    return null;
                }

                // Check .meta.json
                (ScriptMetaJson.Root metaJson, string errMsg) = CheckScriptMetaJson(metaJsonFile);
                if (metaJson == null)
                {
                    _logs.Add(new LogInfo(LogState.Error, errMsg));
                    return null;
                }
                if (metaJson.ScriptFormat != ScriptMetaJson.ScriptFormat.Winbuilder)
                { // Currently only supports only "winbuilder" script_format
                    _logs.Add(new LogInfo(LogState.Error, $"Not supported script format {metaJson.ScriptFormat}"));
                    return null;
                }
                if (metaJson.WbScriptInfo.Version <= sc.ParsedVersion)
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

                // Calculate sha256 of the script
                byte[] sha256Digest;
                using (FileStream fs = new FileStream(tempScriptFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    sha256Digest = HashHelper.GetHash(HashHelper.HashType.SHA256, fs);
                }

                if (!sha256Digest.SequenceEqual(metaJson.HashSHA256))
                {
                    LogInfo.LogErrorMessage(_logs, $"Remote script [{sc.Title}] is corrupted");
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
                if (!remoteSemVer.Equals(metaJson.WbScriptInfo.Version))
                {
                    _logs.Add(new LogInfo(LogState.Error, $"Version of remote script [{sc.Title}] is inconsistent with .meta.json"));
                    return null;
                }
                if (remoteSemVer <= sc.ParsedVersion)
                {
                    _logs.Add(new LogInfo(LogState.Error, $"Remote script [{sc.Title}] ({remoteSemVer}) is not newer than local script ({sc.ParsedVersion})"));
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

        #region ScriptMetaJson methods (.meta.json)
        public static (ScriptMetaJson.Root MetaJson, string ErrorMsg) CheckScriptMetaJson(string metaJsonFile)
        {
            // Prepare JsonSerializer
            JsonSerializerSettings settings = new JsonSerializerSettings { Culture = CultureInfo.InvariantCulture };
            settings.Converters.Add(new VersionExJsonConverter());
            JsonSerializer serializer = JsonSerializer.Create(settings);

            // Read json file
            ScriptMetaJson.Root jsonRoot;
            try
            {
                using (StreamReader sr = new StreamReader(metaJsonFile, Encoding.UTF8, false))
                using (JsonTextReader jr = new JsonTextReader(sr))
                {
                    jsonRoot = serializer.Deserialize<ScriptMetaJson.Root>(jr);
                }
            }
            catch (JsonReaderException)
            {
                return (null, "Remote script meta file is corrupted");
            }

            if (!jsonRoot.CheckSchema(out string errorMsg))
                return (null, errorMsg);

            if (!jsonRoot.CheckScriptInfo(out errorMsg))
                return (null, errorMsg);

            return (jsonRoot, null);
        }

        public static Task CreateScriptMetaJsonAsync(Script sc, string destJsonFile)
        {
            return Task.Run(() => CreateScriptMetaJson(sc, destJsonFile));
        }

        /// <summary>
        /// Currently supports only "winbuilder" script format
        /// </summary>
        /// <param name="sc"></param>
        /// <param name="destJsonFile"></param>
        public static void CreateScriptMetaJson(Script sc, string destJsonFile)
        {
            // Calculate sha256 of the script
            byte[] hashDigest;
            using (FileStream fs = new FileStream(sc.RealPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                hashDigest = HashHelper.GetHash(HashHelper.HashType.SHA256, fs);
            }

            // Get last modified time and file size of the script
            FileInfo fi = new FileInfo(sc.RealPath);

            // Create MetaJsonRoot instance
            ScriptMetaJson.Root jsonRoot = new ScriptMetaJson.Root
            {
                MetaSchemaVer = Global.Const.MetaSchemaVerInst,
                PEBakeryMinVer = Global.Const.ProgramVersionInst,
                LastWrite = fi.LastWriteTimeUtc,
                FileSize = fi.Length,
                HashSHA256 = hashDigest,
                ScriptFormat = ScriptMetaJson.ScriptFormat.Winbuilder,
                WbScriptInfo = new ScriptMetaJson.WbScriptInfo
                {
                    Title = sc.Title,
                    Desc = sc.Description,
                    Author = sc.Author,
                    Version = sc.ParsedVersion,
                }
            };

            // Validate MetaJsonRoot instance (Debug-mode only)
            const string assertErrorMessage = "Incorrect ScriptMetaJson instance creation";
            Debug.Assert(jsonRoot.MetaSchemaVer != null, assertErrorMessage);
            Debug.Assert(jsonRoot.PEBakeryMinVer != null, assertErrorMessage);
            Debug.Assert(jsonRoot.LastWrite != null, assertErrorMessage);
            Debug.Assert(0 <= jsonRoot.FileSize, assertErrorMessage);
            Debug.Assert(jsonRoot.ScriptFormat == ScriptMetaJson.ScriptFormat.Winbuilder, assertErrorMessage);
            Debug.Assert(jsonRoot.HashSHA256 != null, assertErrorMessage);
            Debug.Assert(jsonRoot.WbScriptInfo.Title != null, assertErrorMessage);
            Debug.Assert(jsonRoot.WbScriptInfo.Desc != null, assertErrorMessage);
            Debug.Assert(jsonRoot.WbScriptInfo.Author != null, assertErrorMessage);
            Debug.Assert(jsonRoot.WbScriptInfo.Version != null, assertErrorMessage);

            // Prepare JsonSerializer
            JsonSerializerSettings settings = new JsonSerializerSettings { Culture = CultureInfo.InvariantCulture };
            settings.Converters.Add(new VersionExJsonConverter());
            JsonSerializer serializer = JsonSerializer.Create(settings);

            // Use UTF-8 without a BOM signature, as the file is going to be served in web server
            using (StreamWriter sw = new StreamWriter(destJsonFile, false, new UTF8Encoding(false)))
            using (JsonTextWriter jw = new JsonTextWriter(sw))
            {
                // https://www.newtonsoft.com/json/help/html/ReducingSerializedJSONSize.htm
                jw.Formatting = Formatting.Indented;
                jw.Indentation = 2;
                // jw.Formatting = Formatting.None;
                serializer.Serialize(jw, jsonRoot);
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

    #region class ScriptMetaJson (.meta.json)
    public class ScriptMetaJson
    {
        public enum ScriptFormat
        {
            /// <summary>
            /// WinBuilder format
            /// </summary>
            Winbuilder = 1,
        }

        public class Root
        {
            // Shared
            [JsonProperty(PropertyName = "meta_schema_ver")]
            public VersionEx MetaSchemaVer { get; set; }
            [JsonProperty(PropertyName = "pebakery_min_ver")]
            public VersionEx PEBakeryMinVer { get; set; }

            [JsonProperty(PropertyName = "hash_sha256")]
            public byte[] HashSHA256 { get; set; }
            /// <summary>
            /// Last modified time in UTC
            /// </summary>
            [JsonProperty(PropertyName = "last_write")]
            public DateTime LastWrite { get; set; }
            [JsonProperty(PropertyName = "file_size")]
            public long FileSize { get; set; }
            [JsonProperty(PropertyName = "script_format")]
            [JsonConverter(typeof(StringEnumConverter))]
            public ScriptFormat ScriptFormat { get; set; }

            // One instance per format
            [JsonProperty(PropertyName = "wb_script_info")]
            public WbScriptInfo WbScriptInfo { get; set; }

            [JsonIgnore]
            public static readonly VersionEx SchemaParseVer = Global.Const.MetaSchemaVerInst;

            #region Methods
            /// <summary>
            /// Return true if schema is valid
            /// </summary>
            /// <returns>True if valid</returns>
            public bool CheckSchema(out string errorMsg)
            {
                errorMsg = string.Empty;

                // Check if properties are not null
                if (MetaSchemaVer == null || PEBakeryMinVer == null)
                {
                    errorMsg = "Meta file of remote script is corrupted";
                    return false;
                }

                if (!Enum.IsDefined(typeof(ScriptFormat), ScriptFormat))
                {
                    errorMsg = $"Not supported script format {ScriptFormat}";
                    return false;
                }

                if (GetScriptInfo() == null)
                {
                    errorMsg = $"Unable to find script info of the format {ScriptFormat}";
                    return false;
                }

                // Check if version string are valid
                if (MetaSchemaVer == null)
                {
                    errorMsg = "Meta file of remote script is corrupted";
                    return false;
                }
                if (PEBakeryMinVer == null)
                {
                    errorMsg = "Meta file of remote script is corrupted";
                    return false;
                }

                // Check meta_schema_ver
                if (SchemaParseVer < MetaSchemaVer)
                {
                    errorMsg = "Meta file of remote script requires newer version of PEBakery";
                    return false;
                }

                // Check pebakery_min_ver
                if (Global.Const.ProgramVersionInst < PEBakeryMinVer)
                {
                    errorMsg = $"Remote script requires PEBakery {Global.Const.ProgramVersionStr} or higher";
                    return false;
                }

                return true;
            }

            public ScriptInfo GetScriptInfo()
            {
                switch (ScriptFormat)
                {
                    case ScriptFormat.Winbuilder:
                        return WbScriptInfo;
                    default:
                        return null;
                }
            }

            /// <summary>
            /// Return true if schema is valid
            /// </summary>
            /// <returns>True if valid</returns>
            public bool CheckScriptInfo(out string errorMsg)
            {
                ScriptInfo info = GetScriptInfo();
                if (info != null)
                {
                    return info.CheckScriptInfo(out errorMsg);
                }
                else
                {
                    errorMsg = $"Unable to find script info of the format {ScriptFormat}";
                    return false;
                }
            }
            #endregion
        }

        public abstract class ScriptInfo
        {
            #region Cast
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T Cast<T>() where T : ScriptInfo
            {
                T cast = this as T;
                Debug.Assert(cast != null, "Invalid CodeInfo");
                return cast;
            }

            /// <summary>
            /// Type safe casting helper
            /// </summary>
            /// <typeparam name="T">Child of CodeInfo</typeparam>
            /// <returns>CodeInfo casted as T</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static T Cast<T>(ScriptInfo info) where T : ScriptInfo
            {
                return info.Cast<T>();
            }
            #endregion

            #region CheckScriptInfo
            public abstract bool CheckScriptInfo(out string errorMsg);
            #endregion
        }

        public class WbScriptInfo : ScriptInfo
        {
            [JsonProperty(PropertyName = "title")]
            public string Title { get; set; }
            [JsonProperty(PropertyName = "desc")]
            public string Desc { get; set; }
            [JsonProperty(PropertyName = "author")]
            public string Author { get; set; }
            [JsonProperty(PropertyName = "version")]
            public VersionEx Version { get; set; }

            #region CheckScriptInfo
            /// <summary>
            /// Return true if schema is valid
            /// </summary>
            /// <returns></returns>
            public override bool CheckScriptInfo(out string errorMsg)
            {
                errorMsg = string.Empty;

                // Check if properties are not null
                if (Title == null || Desc == null || Author == null || Version == null)
                {
                    errorMsg = "Meta file of remote script is corrupted";
                    return false;
                }

                return true;
            }
            #endregion
        }
    }
    #endregion
}