using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using PEBakery.Helper;

namespace PEBakery.Core
{
    public class Setting
    {
        #region structs
        public struct GeneralSetting
        {
            public bool OptimizeCode;
            public bool ShowLogAfterBuild;
            public bool StopBuildOnError;
            public bool EnableLongFilePath;
            public bool UseCustomUserAgent;
            public string CustomUserAgent;

            public void Default()
            {
                OptimizeCode = true;
                ShowLogAfterBuild = true;
                StopBuildOnError = true;
                EnableLongFilePath = false;
                UseCustomUserAgent = false;
                // Custom User-Agent is set to Edge's on Windows 10 v1809
                CustomUserAgent = @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/64.0.3282.140 Safari/537.36 Edge/18.17763";
            }
        }

        public struct InterfaceSetting
        {
            public FontHelper.FontInfo MonospacedFont;
            public double ScaleFactor;
            public bool UseCustomEditor;
            public string CustomEditorPath;
            public bool DisplayShellExecuteConOut;
            public bool UseCustomTitle;
            public string CustomTitle;

            public void Default()
            {
                // Interface
                // Every Windows have Consolas installed
                MonospacedFont = new FontHelper.FontInfo(new FontFamily("Consolas"), FontWeights.Regular, 12);
                ScaleFactor = 100;
                DisplayShellExecuteConOut = true;
                UseCustomEditor = false;
                CustomEditorPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "notepad.exe");
                UseCustomTitle = false;
                CustomTitle = string.Empty;
            }
        }

        public struct ScriptSetting
        {
            public bool EnableCache;
            public bool AutoSyntaxCheck;

            public void Default()
            {
                // Script
                EnableCache = true;
                AutoSyntaxCheck = true;
            }
        }

        public struct LogSetting
        {
            public LogDebugLevel LogLevel;
            public bool DeferredLogging;
            public bool MinifyHtmlExport;

            public void Default()
            {
#if DEBUG
                LogLevel = LogDebugLevel.PrintExceptionStackTrace;
#else
                LogLevel = LogDebugLevel.Production;
#endif
                DeferredLogging = true;
                MinifyHtmlExport = true;
            }
        }

        public struct CompatSetting
        {
            // Asterisk Bug
            public bool AsteriskBugDirCopy;
            public bool AsteriskBugDirLink;
            // Command
            public bool FileRenameCanMoveDir;
            public bool AllowLetterInLoop;
            public bool LegacyBranchCondition;
            public bool LegacyRegWrite;
            public bool AllowSetModifyInterface;
            public bool LegacyInterfaceCommand;
            public bool LegacySectionParamCommand;
            // Script Interface
            public bool IgnoreWidthOfWebLabel;
            // Variable
            public bool OverridableFixedVariables;
            public bool OverridableLoopCounter;
            public bool EnableEnvironmentVariables;
            public bool DisableExtendedSectionParams;

            public void Default()
            {
                // Asterisk Bug
                AsteriskBugDirCopy = false;
                AsteriskBugDirLink = false;

                // Command
                FileRenameCanMoveDir = false;
                AllowLetterInLoop = false;
                LegacyBranchCondition = false;
                LegacyRegWrite = false;
                AllowSetModifyInterface = false;
                LegacyInterfaceCommand = false;
                LegacySectionParamCommand = false;

                // Script Interface
                IgnoreWidthOfWebLabel = false;

                // Variable
                OverridableFixedVariables = false;
                OverridableLoopCounter = false;
                EnableEnvironmentVariables = false;
                DisableExtendedSectionParams = false;
            }
        }
        #endregion

        #region Properties
        public GeneralSetting General;
        public InterfaceSetting Interface;
        public ScriptSetting Script;
        public LogSetting Log;
        public CompatSetting Compat;
        #endregion

        #region Constructor
        public Setting()
        {

        }
        #endregion

        #region ReadFromFile, WriteToFile
        #endregion
    }
}
