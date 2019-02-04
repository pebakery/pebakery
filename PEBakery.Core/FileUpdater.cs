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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;

namespace PEBakery.Core
{
    /*
     * - script.project
     * [Update]
     * // ProjectMethod={Static|...}
     * ProjectBaseUrl=<Url>
     *
     * - Per script
     * [Update]
     * ScriptType={Project|Standalone}
     * ScriptUrl=<Url>
     */
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

        #region UpdateScript, UpdateScripts
        public static (Script newScript, string msg) UpdateScript(Project p, Script sc, FileUpdaterOptions opts)
        {
            if (!sc.Sections.ContainsKey(UpdateSection))
                return (null, "Unable to find script update information");
            Dictionary<string, string> scUpdateDict = sc.Sections[UpdateSection].IniDict;

            // Parse ScriptUpdateType
            if (!scUpdateDict.ContainsKey(ScriptTypeKey))
                return (null, "Unable to find script update type");
            ScriptUpdateType scType = ParseScriptUpdateType(scUpdateDict[ScriptTypeKey]);
            if (scType == ScriptUpdateType.None)
                return (null, "Invalid script update type");

            // Get ScriptUrl
            if (!scUpdateDict.ContainsKey(ScriptUrlKey))
                return (null, "Unable to find script server url");
            string url = scUpdateDict[ScriptUrlKey].TrimStart('/');

            if (scType == ScriptUpdateType.Project)
            {
                // Get BaseUrl
                if (!p.MainScript.Sections.ContainsKey(UpdateSection))
                    return (null, "Unable to find project update information");
                Dictionary<string, string> pUpdateDict = p.MainScript.Sections[UpdateSection].IniDict;
                if (!pUpdateDict.ContainsKey(BaseUrlKey))
                    return (null, "Unable to find project update base url");
                string pBaseUrl = pUpdateDict[BaseUrlKey].TrimEnd('/');

                url = $"{url}\\{pBaseUrl}";
            }

            string tempFile = FileHelper.GetTempFile();
            opts.Model?.SetBuildCommandProgress("Download Progress");
            try
            {
                (bool result, string errorMsg) = DownloadFile(url, tempFile, opts);
                if (result)
                { // Success
                    File.Copy(tempFile, sc.DirectRealPath, true);
                    Script newScript = p.RefreshScript(sc);
                    return newScript != null ? (newScript, $"Updated script [{sc.Title}] to [v{sc.Version}] from [v{newScript.Version}]") : (null, @"Downloaded script is corrupted");
                }
                else
                { // Failure
                    return (null, errorMsg);
                }
            }
            finally
            {
                opts.Model?.ResetBuildCommandProgress();

                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        public static List<LogInfo> UpdateProject(Project p, FileUpdaterOptions opts)
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

        private struct InterfaceSectionBackup
        {
            public string SectionName;
            public List<UIControl> ValueCtrls;

            public InterfaceSectionBackup(string sectionName, List<UIControl> valueCtrls)
            {
                SectionName = sectionName;
                ValueCtrls = valueCtrls;
            }
        }

        private static InterfaceSectionBackup BackupInterface(Script sc)
        {
            (string ifaceSectionName, List<UIControl> uiCtrls, _) = sc.GetInterfaceControls();

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

        private static bool RestoreInterface(ref Script sc, InterfaceSectionBackup backup)
        {
            (string ifaceSectionName, List<UIControl> uiCtrls, _) = sc.GetInterfaceControls();

            if (!ifaceSectionName.Equals(backup.SectionName, StringComparison.OrdinalIgnoreCase))
                return false;

            List<UIControl> bakCtrls = backup.ValueCtrls;
            List<UIControl> newCtrls = new List<UIControl>(uiCtrls.Count);
            foreach (UIControl uiCtrl in uiCtrls)
            {
                // Get old uiCtrl, equaility identified by Key and Type.
                UIControl bakCtrl = bakCtrls.FirstOrDefault(bak =>
                    bak.Key.Equals(uiCtrl.Key, StringComparison.OrdinalIgnoreCase) && bak.Type == uiCtrl.Type);
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
            if (str.Equals("Project"))
                return ScriptUpdateType.Project;
            if (str.Equals("Standalone"))
                return ScriptUpdateType.Standalone;
            return ScriptUpdateType.None;
        }

        private static (bool, string) DownloadFile(string url, string destFile, FileUpdaterOptions opts)
        {
            Uri uri = new Uri(url);

            bool result = true;
            string errorMsg = null;
            Stopwatch watch = Stopwatch.StartNew();
            using (WebClient client = new WebClient())
            {
                client.Headers.Add("User-Agent", opts.UserAgent ?? Engine.DefaultUserAgent);
                if (opts.Model != null)
                {
                    client.DownloadProgressChanged += (object sender, DownloadProgressChangedEventArgs e) =>
                    {
                        opts.Model.BuildCommandProgressValue = e.ProgressPercentage;

                        TimeSpan t = watch.Elapsed;
                        double totalSec = t.TotalSeconds;
                        string downloaded = NumberHelper.ByteSizeToSIUnit(e.BytesReceived, 1);
                        string total = NumberHelper.ByteSizeToSIUnit(e.TotalBytesToReceive, 1);
                        if (NumberHelper.DoubleEquals(totalSec, 0))
                        {
                            opts.Model.BuildCommandProgressText = $"{url}\r\nTotal : {total}\r\nReceived : {downloaded}";
                        }
                        else
                        {
                            long bytePerSec = (long)(e.BytesReceived / totalSec); // Byte per sec
                            string speedStr = NumberHelper.ByteSizeToSIUnit((long)(e.BytesReceived / totalSec), 1) + "/s"; // KB/s, MB/s, ...

                            // ReSharper disable once PossibleLossOfFraction
                            TimeSpan r = TimeSpan.FromSeconds((e.TotalBytesToReceive - e.BytesReceived) / bytePerSec);
                            int hour = (int)r.TotalHours;
                            int min = r.Minutes;
                            int sec = r.Seconds;
                            opts.Model.BuildCommandProgressText = $"{url}\r\nTotal : {total}\r\nReceived : {downloaded}\r\nSpeed : {speedStr}\r\nRemaining Time : {hour}h {min}m {sec}s";
                        }
                    };
                }

                AutoResetEvent resetEvent = new AutoResetEvent(false);
                client.DownloadFileCompleted += (object sender, AsyncCompletedEventArgs e) =>
                {
                    // Check if error occured
                    if (e.Cancelled || e.Error != null)
                    {
                        result = false;
                        if (e.Error is WebException webEx)
                            errorMsg = $"[{webEx.Status}] {webEx.Message}";

                        if (File.Exists(destFile))
                            File.Delete(destFile);
                    }

                    resetEvent.Set();
                };

                client.DownloadFileAsync(uri, destFile);

                resetEvent.WaitOne();
            }
            watch.Stop();

            return (result, errorMsg);
        }
        #endregion
    }

    #region FileUpdaterOptions
    public struct FileUpdaterOptions
    {
        public string UserAgent { get; set; }
        public MainViewModel Model { get; set; }
    }
    #endregion
}