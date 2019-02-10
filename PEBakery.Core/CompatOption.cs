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

using PEBakery.Ini;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PEBakery.Helper;

namespace PEBakery.Core
{
    public class CompatOption
    {
        #region Fields and Properties
        public const string SectionName = "PEBakeryCompat";
        private readonly string _compatFile;

        // Asterisk
        public bool AsteriskBugDirCopy = false;
        public bool AsteriskBugDirLink = false;
        // Command
        public bool FileRenameCanMoveDir = false;
        public bool AllowLetterInLoop = false;
        public bool LegacyBranchCondition = false;
        public bool LegacyRegWrite = false;
        public bool AllowSetModifyInterface = false;
        public bool LegacyInterfaceCommand = false;
        public bool LegacySectionParamCommand = false;
        // Script Interface
        public bool IgnoreWidthOfWebLabel = false;
        // Variable
        public bool OverridableFixedVariables = false;
        public bool OverridableLoopCounter = false;
        public bool EnableEnvironmentVariables = false;
        public bool DisableExtendedSectionParams = false;
        #endregion

        #region Constructor
        /// <summary>
        /// Create empty compat options, with temp compatFile.
        /// Useful in testing code.
        /// </summary>
        public CompatOption()
        {
            _compatFile = FileHelper.GetTempFile();
        }

        /// <summary>
        /// Load compat options from a ini file.
        /// If compatFile does not exist, create an empty compat options.
        /// </summary>
        /// <param name="compatFile">PEBakeryCompat.ini to load compat options</param>
        public CompatOption(string compatFile)
        {
            _compatFile = compatFile;

            ReadFromFile();
        }

        /// <summary>
        /// Used for cloning
        /// </summary>
        /// <param name="compatFile"></param>
        /// <param name="src"></param>
        private CompatOption(string compatFile, CompatOption src)
        {
            _compatFile = compatFile;

            src.CopyTo(this);
        }
        #endregion

        #region SetToDefault
        public void SetToDefault()
        {
            // Asterisk
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
        #endregion

        #region ReadFromFile, WriteToFile
        public void ReadFromFile()
        {
            // Use default value if key/value does not exist or malformed.
            SetToDefault();

            // compatFile does not exist -> Default compat options
            if (!File.Exists(_compatFile))
                return;

            IniKey[] keys =
            {
                new IniKey(SectionName, nameof(AsteriskBugDirCopy)), // Boolean
                new IniKey(SectionName, nameof(AsteriskBugDirLink)), // Boolean
                new IniKey(SectionName, nameof(FileRenameCanMoveDir)), // Boolean
                new IniKey(SectionName, nameof(AllowLetterInLoop)), // Boolean
                new IniKey(SectionName, nameof(LegacyBranchCondition)), // Boolean
                new IniKey(SectionName, nameof(LegacyRegWrite)), // Boolean
                new IniKey(SectionName, nameof(AllowSetModifyInterface)), // Boolean
                new IniKey(SectionName, nameof(LegacyInterfaceCommand)), // Boolean
                new IniKey(SectionName, nameof(LegacySectionParamCommand)), // Boolean
                new IniKey(SectionName, nameof(IgnoreWidthOfWebLabel)), // Boolean
                new IniKey(SectionName, nameof(OverridableFixedVariables)), // Boolean
                new IniKey(SectionName, nameof(OverridableLoopCounter)), // Boolean
                new IniKey(SectionName, nameof(EnableEnvironmentVariables)), // Boolean
                new IniKey(SectionName, nameof(DisableExtendedSectionParams)), // Boolean
            };

            keys = IniReadWriter.ReadKeys(_compatFile, keys);
            Dictionary<string, string> keyDict = keys.ToDictionary(
                x => x.Key,
                x => x.Value,
                StringComparer.OrdinalIgnoreCase);

            // Asterisk
            AsteriskBugDirCopy = DictParser.ParseBoolean(keyDict, SectionName, nameof(AsteriskBugDirCopy), AsteriskBugDirCopy);
            AsteriskBugDirLink = DictParser.ParseBoolean(keyDict, SectionName, nameof(AsteriskBugDirLink), AsteriskBugDirLink);
            // Command
            FileRenameCanMoveDir = DictParser.ParseBoolean(keyDict, SectionName, nameof(FileRenameCanMoveDir), FileRenameCanMoveDir);
            AllowLetterInLoop = DictParser.ParseBoolean(keyDict, SectionName, nameof(AllowLetterInLoop), AllowLetterInLoop);
            LegacyBranchCondition = DictParser.ParseBoolean(keyDict, SectionName, nameof(LegacyBranchCondition), LegacyBranchCondition);
            LegacyRegWrite = DictParser.ParseBoolean(keyDict, SectionName, nameof(LegacyRegWrite), LegacyRegWrite);
            AllowSetModifyInterface = DictParser.ParseBoolean(keyDict, SectionName, nameof(AllowSetModifyInterface), AllowSetModifyInterface);
            LegacyInterfaceCommand = DictParser.ParseBoolean(keyDict, SectionName, nameof(LegacyInterfaceCommand), LegacyInterfaceCommand);
            LegacySectionParamCommand = DictParser.ParseBoolean(keyDict, SectionName, nameof(LegacySectionParamCommand), LegacySectionParamCommand);
            // Script Interface
            IgnoreWidthOfWebLabel = DictParser.ParseBoolean(keyDict, SectionName, nameof(IgnoreWidthOfWebLabel), IgnoreWidthOfWebLabel);
            // Variable
            OverridableFixedVariables = DictParser.ParseBoolean(keyDict, SectionName, nameof(OverridableFixedVariables), OverridableFixedVariables);
            OverridableLoopCounter = DictParser.ParseBoolean(keyDict, SectionName, nameof(OverridableLoopCounter), OverridableLoopCounter);
            EnableEnvironmentVariables = DictParser.ParseBoolean(keyDict, SectionName, nameof(EnableEnvironmentVariables), EnableEnvironmentVariables);
            DisableExtendedSectionParams = DictParser.ParseBoolean(keyDict, SectionName, nameof(DisableExtendedSectionParams), DisableExtendedSectionParams);
        }

        public void WriteToFile()
        {
            IniKey[] keys =
            {
                new IniKey(SectionName, nameof(AsteriskBugDirCopy), AsteriskBugDirCopy.ToString()), // Boolean
                new IniKey(SectionName, nameof(AsteriskBugDirLink), AsteriskBugDirLink.ToString()), // Boolean
                new IniKey(SectionName, nameof(FileRenameCanMoveDir), FileRenameCanMoveDir.ToString()), // Boolean
                new IniKey(SectionName, nameof(AllowLetterInLoop), AllowLetterInLoop.ToString()), // Boolean
                new IniKey(SectionName, nameof(LegacyBranchCondition), LegacyBranchCondition.ToString()), // Boolean
                new IniKey(SectionName, nameof(LegacyRegWrite), LegacyRegWrite.ToString()), // Boolean
                new IniKey(SectionName, nameof(AllowSetModifyInterface), AllowSetModifyInterface.ToString()), // Boolean
                new IniKey(SectionName, nameof(LegacyInterfaceCommand), LegacyInterfaceCommand.ToString()), // Boolean
                new IniKey(SectionName, nameof(LegacySectionParamCommand), LegacySectionParamCommand.ToString()), // Boolean
                new IniKey(SectionName, nameof(IgnoreWidthOfWebLabel), IgnoreWidthOfWebLabel.ToString()), // Boolean
                new IniKey(SectionName, nameof(OverridableFixedVariables), OverridableFixedVariables.ToString()), // Boolean
                new IniKey(SectionName, nameof(OverridableLoopCounter), OverridableLoopCounter.ToString()), // Boolean
                new IniKey(SectionName, nameof(EnableEnvironmentVariables), EnableEnvironmentVariables.ToString()), // Boolean
                new IniKey(SectionName, nameof(DisableExtendedSectionParams), DisableExtendedSectionParams.ToString()), // Boolean
            };
            IniReadWriter.WriteKeys(_compatFile, keys);
        }
        #endregion

        #region CopyFrom
        public void CopyTo(CompatOption dest)
        {
            // Asterisk
            dest.AsteriskBugDirCopy = AsteriskBugDirCopy;
            dest.AsteriskBugDirLink = AsteriskBugDirLink;
            // Command
            dest.FileRenameCanMoveDir = FileRenameCanMoveDir;
            dest.AllowLetterInLoop = AllowLetterInLoop;
            dest.LegacyBranchCondition = LegacyBranchCondition;
            dest.LegacyRegWrite = LegacyRegWrite;
            dest.AllowSetModifyInterface = AllowSetModifyInterface;
            dest.LegacyInterfaceCommand = LegacyInterfaceCommand;
            dest.LegacySectionParamCommand = LegacySectionParamCommand;
            // Script Interface
            dest.IgnoreWidthOfWebLabel = IgnoreWidthOfWebLabel;
            // Variable
            dest.OverridableFixedVariables = OverridableFixedVariables;
            dest.OverridableLoopCounter = OverridableLoopCounter;
            dest.EnableEnvironmentVariables = EnableEnvironmentVariables;
            dest.DisableExtendedSectionParams = DisableExtendedSectionParams;
        }
        #endregion

        #region Clone
        public CompatOption Clone()
        {
            return new CompatOption(_compatFile, this);
        }
        #endregion

        #region Diff
        public Dictionary<string, bool> Diff(CompatOption other)
        {
            return new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
            {
                // Asterisk
                [nameof(AsteriskBugDirCopy)] = AsteriskBugDirCopy != other.AsteriskBugDirCopy,
                [nameof(AsteriskBugDirLink)] = AsteriskBugDirLink != other.AsteriskBugDirLink,
                // Command
                [nameof(FileRenameCanMoveDir)] = FileRenameCanMoveDir != other.FileRenameCanMoveDir,
                [nameof(AllowLetterInLoop)] = AllowLetterInLoop != other.AllowLetterInLoop,
                [nameof(LegacyBranchCondition)] = LegacyBranchCondition != other.LegacyBranchCondition,
                [nameof(LegacyRegWrite)] = LegacyRegWrite != other.LegacyRegWrite,
                [nameof(AllowSetModifyInterface)] = AllowSetModifyInterface != other.AllowSetModifyInterface,
                [nameof(LegacyInterfaceCommand)] = LegacyInterfaceCommand != other.LegacyInterfaceCommand,
                [nameof(LegacySectionParamCommand)] = LegacySectionParamCommand != other.LegacySectionParamCommand,
                // Script Interface
                [nameof(IgnoreWidthOfWebLabel)] = IgnoreWidthOfWebLabel != other.IgnoreWidthOfWebLabel,
                // Variable
                [nameof(OverridableFixedVariables)] = OverridableFixedVariables != other.OverridableFixedVariables,
                [nameof(OverridableLoopCounter)] = OverridableLoopCounter != other.OverridableLoopCounter,
                [nameof(EnableEnvironmentVariables)] = EnableEnvironmentVariables != other.EnableEnvironmentVariables,
                [nameof(DisableExtendedSectionParams)] = DisableExtendedSectionParams != other.DisableExtendedSectionParams,
            };
        }
        #endregion
    }
}
