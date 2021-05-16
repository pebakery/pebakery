/*
    Copyright (C) 2018-2020 Hajin Jang
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

using PEBakery.Core.ViewModels;
using PEBakery.Helper;
using PEBakery.Ini;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
    
    */
    #endregion

    #region (Docs) ProjectMetaJson (project.meta.json)
    /*
    Example of *.meta.json
    
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
        #endregion

        #region Constructor
        public FileUpdater(Project p, MainViewModel mainViewModel, string customUserAgent)
        {
            _p = p;
            _m = mainViewModel;

            string userAgent = customUserAgent ?? Engine.DefaultUserAgent;
            _downloader = new HttpFileDownloader(_m, 10, userAgent, null);
        }
        #endregion

        #region Update one or more scripts
        public Task<(Script, LogInfo)> UpdateScriptAsync(Script sc, bool preserveScriptState)
        {
            return Task.Run(() => UpdateScript(sc, preserveScriptState));
        }

        public (Script, LogInfo) UpdateScript(Script sc, bool preserveScriptState)
        {
            if (!sc.IsUpdateable)
                return (null, new LogInfo(LogState.Error, $"Script [{sc.Title} is not updateable"));

            // Backup interface state of original script
            ScriptStateBackup stateBackup = null;
            if (preserveScriptState)
            {
                stateBackup = BackupScriptState(sc);
                Debug.Assert(stateBackup != null, "ScriptStateBackup is null");
            }

            ResultReport<Script> report;
            _m?.SetBuildCommandProgress("Download Progress", 1);
            try
            {
                if (_m != null)
                    _m.BuildEchoMessage = $"Updating script [{sc.Title}]...";

                report = InternalUpdateOneScript(sc, stateBackup);

                if (_m != null)
                    _m.BuildCommandProgressValue = 1;
            }
            finally
            {
                _m?.ResetBuildCommandProgress();
                if (_m != null)
                    _m.BuildEchoMessage = string.Empty;
            }

            return (report.Result, report.ToLogInfo());
        }

        public Task<(Script[], LogInfo[])> UpdateScriptsAsync(IEnumerable<Script> scripts, bool preserveScriptState)
        {
            return Task.Run(() => UpdateScripts(scripts, preserveScriptState));
        }

        public (Script[], LogInfo[]) UpdateScripts(IEnumerable<Script> scripts, bool preserveScriptState)
        {
            // Get updateable scripts urls
            Script[] updateableScripts = scripts.Where(s => s.IsUpdateable).ToArray();

            List<Script> newScripts = new List<Script>(updateableScripts.Length);
            List<LogInfo> logs = new List<LogInfo>(updateableScripts.Length);

            if (_m != null)
                _m.BuildScriptProgressVisibility = Visibility.Collapsed;
            _m?.SetBuildCommandProgress("Download Progress", updateableScripts.Length);
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
                    ResultReport<Script> report = InternalUpdateOneScript(sc, stateBackup);
                    if (report.Success && report.Result != null)
                        newScripts.Add(report.Result);
                    logs.Add(report.ToLogInfo());

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

            return (newScripts.ToArray(), logs.ToArray());
        }

        private ResultReport<Script> InternalUpdateOneScript(Script sc, ScriptStateBackup stateBackup)
        {
            // Never should be triggered, because Script class constructor check it
            Debug.Assert(sc.ParsedVersion != null, $"Local script [{sc.Title}] does not provide proper version information");

            string updateUrl = sc.UpdateUrl;
            string metaJsonUrl = Path.ChangeExtension(updateUrl, ".meta.json");
            string metaJsonFile = FileHelper.GetTempFile(".meta.json");
            string tempScriptFile = FileHelper.GetTempFile(".script");
            try
            {
                // Download .meta.json
                HttpFileDownloader.Report httpReport = DownloadFile(metaJsonUrl, metaJsonFile);
                if (!httpReport.Result)
                {
                    // Failed to send a request, such as network not available
                    if (httpReport.StatusCode == 0)
                        return new ResultReport<Script>(false, null, $"Unable to connect to the server [${updateUrl}]");

                    // Try downloading .deleted to check if a script is deleted
                    string errorMsg;
                    string deletedUrl = Path.ChangeExtension(updateUrl, ".deleted");
                    string deletedFile = Path.ChangeExtension(metaJsonFile, ".deleted");
                    try
                    {
                        httpReport = DownloadFile(deletedUrl, deletedFile);
                        if (httpReport.Result)
                        { // Successfully received response
                            if (httpReport.StatusCode == 200) // .deleted file exists in the server
                                errorMsg = $"[{sc.Title}] was deleted from the server";
                            else // There is no .deleted file in the server
                                errorMsg = $"Update is not available for [{sc.Title}]";
                        }
                        else
                        {
                            if (httpReport.StatusCode == 0) // Failed to send a request, such as network not available
                                errorMsg = $"Unable to connect to the server [${updateUrl}]";
                            else
                                errorMsg = $"Update is not available for [{sc.Title}]";
                        }
                    }
                    finally
                    {
                        if (File.Exists(deletedFile))
                            File.Delete(deletedFile);
                    }

                    return new ResultReport<Script>(false, null, errorMsg);
                }

                // Check and read .meta.json
                ResultReport<UpdateJson.Root> jsonReport = UpdateJson.ReadUpdateJson(metaJsonFile);
                if (!jsonReport.Success)
                    return new ResultReport<Script>(false, null, jsonReport.Message);

                UpdateJson.Root metaJson = jsonReport.Result;
                UpdateJson.FileIndex index = metaJson.Index;
                if (index.Kind != UpdateJson.IndexEntryKind.Script)
                    return new ResultReport<Script>(false, null, "Update json is not of a script file");
                UpdateJson.ScriptInfo scInfo = index.ScriptInfo;
                if (scInfo.Format != UpdateJson.ScriptFormat.IniBased)
                    return new ResultReport<Script>(false, null, $"Format [{scInfo.Format}] of remote script [{sc.Title}] is not supported");
                UpdateJson.IniBasedScript iniScInfo = scInfo.IniBased;
                if (iniScInfo.Version <= sc.ParsedVersion)
                    return new ResultReport<Script>(false, null, $"You are using the lastest version of script [{sc.Title}]");

                // Download .script file
                httpReport = DownloadFile(updateUrl, tempScriptFile);
                if (!httpReport.Result)
                    return new ResultReport<Script>(false, null, httpReport.ErrorMsg);

                // Verify downloaded .script file with FileMetadata
                ResultReport verifyReport = index.FileMetadata.VerifyFile(tempScriptFile);
                if (!verifyReport.Success)
                    return new ResultReport<Script>(false, null, $"Remote script [{sc.Title}] is corrupted");

                // Check downloaded script's version and check
                // Must have been checked with the UpdateJson
                string remoteVerStr = IniReadWriter.ReadKey(tempScriptFile, "Main", "Version");
                VersionEx remoteVer = VersionEx.Parse(remoteVerStr);
                if (remoteVer == null)
                    return new ResultReport<Script>(false, null, $"Version of remote script [{sc.Title}] is corrupted");
                if (!remoteVer.Equals(iniScInfo.Version))
                    return new ResultReport<Script>(false, null, $"Version of remote script [{sc.Title}] is corrupted");
                if (remoteVer <= sc.ParsedVersion)
                    return new ResultReport<Script>(false, null, $"Version of remote script [{sc.Title}] is corrupted");

                // Check if remote script is valid
                Script remoteScript = _p.LoadScriptRuntime(tempScriptFile, new LoadScriptRuntimeOptions
                {
                    IgnoreMain = false,
                    AddToProjectTree = false,
                    OverwriteToProjectTree = false,
                });
                if (remoteScript == null)
                    return new ResultReport<Script>(false, null, $"Remote script [{sc.Title}] is corrupted");

                // Overwrite backup state to new script
                if (stateBackup != null)
                {
                    RestoreScriptState(remoteScript, stateBackup);

                    // Let's be extra careful
                    remoteScript = _p.RefreshScript(remoteScript);
                    if (remoteScript == null)
                        return new ResultReport<Script>(false, null, $"Internal error at {nameof(FileUpdater)}.{nameof(RestoreScriptState)}");
                }

                // Copy downloaded remote script into new script
                File.Copy(tempScriptFile, sc.DirectRealPath, true);
                Script newScript = _p.RefreshScript(sc);

                // Return updated script instance
                return new ResultReport<Script>(true, newScript, $"Updated script [{sc.Title}] to [v{sc.RawVersion}] from [v{newScript.RawVersion}]");
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
        /// This method does not refresh the Script instance, it is the responsibility of a callee
        /// </remarks>
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