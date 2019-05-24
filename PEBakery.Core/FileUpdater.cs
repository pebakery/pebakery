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

using PEBakery.Core.ViewModels;
using PEBakery.Helper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PEBakery.Ini;

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
    #endregion

    public class FileUpdater
    {
        #region Const and Enum
        private const string UpdateSection = @"Update";
        private const string MethodKey = @"ProjectMethod";
        private const string BaseUrlKey = @"ProjectBaseUrl";
        private const string ScriptTypeKey = @"ScriptType";
        private const string ScriptUrlKey = @"ScriptUrl";
        private enum ScriptUpdateType
        {
            None,
            Project,
            Standalone,
        }
        #endregion

        #region Fields and Properties
        private readonly Project _p;
        private readonly MainViewModel _m;
        private readonly string _userAgent;

        private readonly string _projectBaseUrl;
        #endregion

        #region Constructor
        public FileUpdater(Project p, MainViewModel mainViewModel, string customUserAgent)
        {
            _p = p;

            // Get Project BaseUrl if available
            if (_p.MainScript.Sections.ContainsKey(UpdateSection))
            {
                Dictionary<string, string> pUpdateDict = _p.MainScript.Sections[UpdateSection].IniDict;
                if (pUpdateDict.ContainsKey(BaseUrlKey))
                {
                    string pBaseUrl = pUpdateDict[BaseUrlKey].TrimEnd('/');
                    if (StringHelper.GetUriProtocol(pBaseUrl) != null)
                        _projectBaseUrl = pBaseUrl;
                }
            }

            _m = mainViewModel;
            _userAgent = customUserAgent ?? Engine.DefaultUserAgent;
        }
        #endregion

        #region UpdateScript, UpdateScripts
        public (Script newScript, string msg) UpdateScript(Script sc, bool preserveScriptState)
        {
            if (!sc.Sections.ContainsKey(UpdateSection))
                return (null, $"Script {sc.Title} does not provide proper update information: {UpdateSection} section");
            Dictionary<string, string> scUpdateDict = sc.Sections[UpdateSection].IniDict;

            // Parse ScriptUpdateType
            if (!scUpdateDict.ContainsKey(ScriptTypeKey))
                return (null, $"Script {sc.Title} does not provide proper update information: {ScriptTypeKey} key");
            ScriptUpdateType scType = ParseScriptUpdateType(scUpdateDict[ScriptTypeKey]);
            if (scType == ScriptUpdateType.None)
                return (null, $"Script {sc.Title} does not provide proper update information: {ScriptTypeKey} value");

            // Get ScriptUrl
            if (!scUpdateDict.ContainsKey(ScriptUrlKey))
                return (null, $"Script {sc.Title} does not provide proper update information: {ScriptUrlKey}");
            string url = scUpdateDict[ScriptUrlKey].TrimStart('/');

            if (scType == ScriptUpdateType.Project)
            {
                if (_projectBaseUrl == null)
                    return (null, $"Project {_p.ProjectName} does not provide proper update information: {BaseUrlKey}");
                
                url = $"{_projectBaseUrl}/{url}";
            }

            // Backup interface state of original script
            InterfaceSectionBackup ifaceBak = null;
            if (preserveScriptState)
            {
                ifaceBak = BackupInterface(sc);
            }

            string tempFile = FileHelper.GetTempFile();
            _m?.SetBuildCommandProgress("Download Progress");
            try
            {
                HttpFileDownloader downloader = new HttpFileDownloader(_m, 10, _userAgent);
                HttpFileDownloader.Report report;
                try
                {
                    Task<HttpFileDownloader.Report> task = downloader.Download(url, tempFile);
                    task.Wait();

                    report = task.Result;
                }
                catch (Exception e)
                {
                    report = new HttpFileDownloader.Report(false, 0, Logger.LogExceptionMessage(e));
                }

                // Download failure
                if (!report.Result)
                    return (null, report.ErrorMsg);

                // Check downloaded script's version
                string newVerStr = IniReadWriter.ReadKey(tempFile, "Main", "Version");
                NumberHelper.VersionEx localSemVer = NumberHelper.VersionEx.Parse(sc.TidyVersion);
                NumberHelper.VersionEx remoteSemVer = NumberHelper.VersionEx.Parse(newVerStr);
                if (remoteSemVer.CompareTo(localSemVer) != 1) // newSemVer < oldSemVer || newSemVer == oldSemVer
                    return (null, $"Remote script [{remoteSemVer}] is not newer than local script [{localSemVer}]");

                // Overwrite backup state to new script
                File.Copy(tempFile, sc.DirectRealPath, true);
                Script newScript = _p.RefreshScript(sc);
                if (newScript == null)
                    return (null, "Downloaded script is corrupted");

                // Overwrite backup state to new script
                if (preserveScriptState)
                {
                    Debug.Assert(ifaceBak != null, "Internal Error at FileUpdater.UpdateScript");
                    RestoreInterface(ref newScript, ifaceBak);
                }

                return (newScript, $"Updated script [{sc.Title}] to [v{sc.RawVersion}] from [v{newScript.RawVersion}]");
            }
            finally
            {
                _m?.ResetBuildCommandProgress();

                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        public List<LogInfo> UpdateProject()
        {
            List<LogInfo> logs = new List<LogInfo>();
            return logs;

            // Work in Progress
            /*
            List<(Script, string)> newScripts = new List<(Script, string)>(p.AllScripts.Count);

            // Get BaseUrl
            if (!p.MainScript.Sections.ContainsKey(UpdateSection))
                return LogInfo.LogErrorMessage(logs, "Unable to find project update information");
            Dictionary<string, string> pUpdateDict = IniUtil.ParseIniLinesIniStyle(p.MainScript.Sections[UpdateSection].GetLines());
            if (!pUpdateDict.ContainsKey(BaseUrlKey))
                return LogInfo.LogErrorMessage(logs, "Unable to find project update base url");
            string pBaseUrl = pUpdateDict[BaseUrlKey].TrimEnd('\\');

            foreach (Script sc in p.AllScripts)
            {
                if (!sc.Sections.ContainsKey(UpdateSection))
                    continue;
                Dictionary<string, string> scUpdateDict = IniUtil.ParseIniLinesIniStyle(sc.Sections[UpdateSection].GetLines());

                // Parse ScriptUpdateType
                if (!scUpdateDict.ContainsKey(ScriptTypeKey))
                    continue;
                ScriptUpdateType scType = ParseScriptUpdateType(scUpdateDict[ScriptTypeKey]);
                if (scType == ScriptUpdateType.None)
                {
                    logs.Add(new LogInfo(LogState.Error, "Invalid script update type"));
                    continue;
                }
                    
                // Get ScriptUrl
                if (!scUpdateDict.ContainsKey(ScriptUrlKey))
                {
                    logs.Add(new LogInfo(LogState.Error, "Unable to find script server url"));
                    continue;
                }
                string url = scUpdateDict[ScriptUrlKey].TrimStart('\\');

                // Final Url
                if (scType == ScriptUpdateType.Project)
                    url = $"{url}\\{pBaseUrl}";

                string tempFile = FileHelper.GetTempFileNameEx();
                opts.Model?.SetBuildCommandProgress("Download Progress");
                try
                {
                    (bool result, string errorMsg) = DownloadFile(url, tempFile, opts);
                    if (result)
                    { // Success
                        File.Copy(tempFile, sc.DirectRealPath, true);
                        Script newScript = p.RefreshScript(sc);
                        if (newScript != null)
                            newScripts.Add((newScript, $"Updated script [{sc.Title}] to [{sc.Version}] from [{newScript.Version}]"));
                        else
                            newScripts.Add((null, @"Downloaded script is corrupted"));
                    }
                    else
                    { // Failure
                        newScripts.Add((null, errorMsg));
                    }
                }
                finally
                {
                    opts.Model?.ResetBuildCommandProgress();

                    if (File.Exists(tempFile))
                        File.Delete(tempFile);
                }
            }

            return logs;
            */
        }
        #endregion

        #region {Backup,Restore}Interface
        public class InterfaceSectionBackup
        {
            public readonly string SectionName;
            public readonly List<UIControl> ValueCtrls;

            public InterfaceSectionBackup(string sectionName, List<UIControl> valueCtrls)
            {
                SectionName = sectionName;
                ValueCtrls = valueCtrls;
            }
        }

        public static InterfaceSectionBackup BackupInterface(Script sc)
        {
            (string ifaceSectionName, List<UIControl> uiCtrls, _) = sc.GetInterfaceControls();

            // Unable to interface section
            if (ifaceSectionName == null)
                return null;

            // Collect uiCtrls which have value
            List<UIControl> valueCtrls = new List<UIControl>();
            foreach (UIControl uiCtrl in uiCtrls)
            {
                string value = uiCtrl.GetValue(false);
                if (value != null)
                    valueCtrls.Add(uiCtrl);
            }

            return new InterfaceSectionBackup(ifaceSectionName, valueCtrls);
        }

        public static bool RestoreInterface(ref Script sc, InterfaceSectionBackup backup)
        {
            (string ifaceSectionName, List<UIControl> uiCtrls, _) = sc.GetInterfaceControls();

            if (!ifaceSectionName.Equals(backup.SectionName, StringComparison.OrdinalIgnoreCase))
                return false;

            List<UIControl> bakCtrls = backup.ValueCtrls;
            List<UIControl> newCtrls = new List<UIControl>(uiCtrls.Count);
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

            // Write to file
            UIControl.Update(newCtrls);
            sc = sc.Project.RefreshScript(sc);
            return true;
        }
        #endregion

        #region Utility
        private static ScriptUpdateType ParseScriptUpdateType(string str)
        {
            if (str.Equals("Project", StringComparison.OrdinalIgnoreCase))
                return ScriptUpdateType.Project;
            if (str.Equals("Standalone", StringComparison.OrdinalIgnoreCase))
                return ScriptUpdateType.Standalone;
            return ScriptUpdateType.None;
        }
        #endregion
    }
}