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

using System.Collections.Generic;
using System.Text;
using Microsoft.Win32;
using System.IO;
using System.Net.NetworkInformation;
using System.Globalization;
using System;
using System.Linq;
using System.Windows;
using PEBakery.Exceptions;
using PEBakery.Helper;
using PEBakery.WPF.Controls;
using PEBakery.IniLib;

namespace PEBakery.Core
{
    #region CodeType
    public enum CodeType
    {
        // 00 Misc
        None = 0, Comment, Error, Unknown,
        // 01 File
        FileCopy = 100, FileDelete, FileRename, FileMove, FileCreateBlank, FileSize, FileVersion,
        DirCopy = 120, DirDelete, DirMove, DirMake, DirSize,
        // 02 Registry
        RegHiveLoad = 200, RegHiveUnload, RegImport, RegExport, RegRead, RegWrite, RegDelete, RegMulti,
        RegWriteLegacy = 260,
        // 03 Text
        TXTAddLine = 300, TXTDelLine, TXTReplace, TXTDelSpaces, TXTDelEmptyLines,
        TXTAddLineOp = 380, TXTDelLineOp,
        // 04 INI
        INIWrite = 400, INIRead, INIDelete, INIAddSection, INIDeleteSection, INIWriteTextLine, INIMerge,
        INIWriteOp = 480, INIReadOp, INIDeleteOp, INIAddSectionOp, INIDeleteSectionOp, INIWriteTextLineOp,
        // 05 Compress
        Compress = 500, Decompress, Expand, CopyOrExpand, 
        // 06 Network
        WebGet = 600, WebGetIfNotExist,
        // 07 Attach
        ExtractFile = 700, ExtractAndRun, ExtractAllFiles, Encode,
        // 08 Interface
        Visible = 800, Message, Echo, UserInput, AddInterface,
        VisibleOp = 880,
        Retrieve = 899, // Will be deprecated in favor of [UserInput | FileSize | FileVersion | DirSize | Hash]
        // 09 Hash
        Hash = 900,
        // 10 StringFormat
        StrFormat = 1000,
        // 11 Math
        Math = 1100,
        // 12 System
        System = 1200, ShellExecute, ShellExecuteEx, ShellExecuteDelete,
        // 13 Branch
        Run = 1300, Exec, Loop, If, Else, Begin, End,
        // 14 Control
        Set = 1400, AddVariables, Exit, Halt, Wait, Beep, 
        // 15 External Macro
        Macro = 1500,
    }
    #endregion

    #region SectionAddress
    [Serializable]
    public struct SectionAddress
    {
        public Plugin Plugin;
        public PluginSection Section;

        public SectionAddress(Plugin plugin, PluginSection section)
        {
            this.Plugin = plugin;
            this.Section = section;
        }

        public override bool Equals(object obj)
        {
            if (obj is SectionAddress addr)
            {
                bool result = true;
                if (Plugin != addr.Plugin || Section != addr.Section)
                    result = false;
                return result;
            }
            else
                return false;
        }

        public static bool operator ==(SectionAddress c1, SectionAddress c2)
        {
            return c1.Equals(c2);
        }

        public static bool operator !=(SectionAddress c1, SectionAddress c2)
        {
            return !c1.Equals(c2);
        }

        public override int GetHashCode()
        {
            return Plugin.FullPath.GetHashCode() ^ Section.SectionName.GetHashCode();
        }
    }
    #endregion

    #region CodeCommand
    [Serializable]
    public class CodeCommand
    {
        public string RawCode;
        public SectionAddress Addr;

        public CodeType Type;
        public CodeInfo Info;

        public CodeCommand(string rawCode, CodeType type, CodeInfo info)
        {
            RawCode = rawCode;
            Type = type;
            Info = info;
        }

        public CodeCommand(string rawCode, SectionAddress addr, CodeType type, CodeInfo info)
        {
            RawCode = rawCode;
            Addr = addr;
            Type = type;
            Info = info;
        }

        public override string ToString()
        {
            return RawCode;
        }

        public readonly static CodeType[] DeprecatedCodeType = new CodeType[]
        {
            CodeType.WebGetIfNotExist, // Better to have as Macro
            CodeType.ExtractAndRun, // Better to have as Macro
            CodeType.CopyOrExpand, // NT6 does not have cabinet files such as .ex_, .dl_
        };

        public readonly static CodeType[] OptimizedCodeType = new CodeType[]
        {
            CodeType.TXTAddLineOp, 
            CodeType.TXTDelLineOp,
            CodeType.INIReadOp,
            CodeType.INIWriteOp,
            CodeType.INIAddSectionOp,
            CodeType.INIDeleteSectionOp,
            CodeType.INIWriteTextLineOp,
            CodeType.VisibleOp,
        };
    }
    #endregion

    #region CodeInfo
    [Serializable]
    public class CodeInfo
    {
        /// <summary>
        /// This function should only be called from child Class
        /// Note : this function includes first ','
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return base.ToString();
        }
    }
    #endregion

    #region CodeInfo 01 - File
    [Serializable]
    public class CodeInfo_FileCopy : CodeInfo
    { // FileCopy,<SrcFile>,<DestPath>,[PRESERVE],[NOWARN],[NOREC]
        public string SrcFile;
        public string DestPath;
        public bool Preserve;
        public bool NoWarn;
        public bool NoRec;

        public CodeInfo_FileCopy(string srcFile, string destPath, bool preserve, bool noWarn, bool noRec)
        {
            SrcFile = srcFile;
            DestPath = destPath;
            Preserve = preserve;
            NoWarn = noWarn;
            NoRec = noRec;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(SrcFile);
            b.Append(",");
            b.Append(DestPath);
            if (Preserve)
                b.Append(",PRESERVE");
            if (NoWarn)
                b.Append(",NOWARN");
            if (NoRec)
                b.Append(",NOREC");

            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_FileDelete : CodeInfo
    { // FileDelete,<FilePath>[,NOWARN][,NOREC]
        public string FilePath;
        public bool NoWarn;
        public bool NoRec;

        public CodeInfo_FileDelete(string filePath, bool noWarn, bool noRec)
        {
            FilePath = filePath;
            NoWarn = noWarn;
            NoRec = noRec;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(FilePath);
            if (NoWarn)
                b.Append(",NOWARN");
            if (NoRec)
                b.Append(",NOREC");

            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_FileRename : CodeInfo
    { // FileRename,<SrcPath>,<DestPath>
        public string SrcPath;
        public string DestPath;

        public CodeInfo_FileRename(string srcPath, string destPath)
        {
            SrcPath = srcPath;
            DestPath = destPath;
        }

        public override string ToString()
        {
            return $"FileRename,{SrcPath},{DestPath}";
        }
    }

    [Serializable]
    public class CodeInfo_FileCreateBlank : CodeInfo
    { // FileCreateBlank,<FilePath>[,PRESERVE][,NOWARN][,UTF8|UTF16|UTF16BE|ANSI]
        public string FilePath;
        public bool Preserve;
        public bool NoWarn;
        public Encoding Encoding; // Optional
        
        public CodeInfo_FileCreateBlank(string filePath, bool preserve, bool noWarn, Encoding encoding)
        {
            FilePath = filePath;
            Preserve = preserve;
            NoWarn = noWarn;
            Encoding = encoding;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(StringEscaper.QuoteEscape(FilePath));
            if (Preserve)
                b.Append(",PRESERVE");
            if (NoWarn)
                b.Append(",NOWARN");
            if (Encoding != null)
            {
                if (Encoding == Encoding.UTF8)
                    b.Append(",UTF8");
                else if (Encoding == Encoding.Unicode)
                    b.Append(",UTF16");
                else if (Encoding == Encoding.BigEndianUnicode)
                    b.Append(",UTF16BE");
                else if (Encoding == Encoding.ASCII)
                    b.Append(",ANSI");
            }
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_FileSize : CodeInfo
    { // FileSize,<FileName>,<DestVar>
        public string FilePath;
        public string DestVar;

        public CodeInfo_FileSize(string filePath, string destVar)
        {
            FilePath = filePath;
            DestVar = destVar;
        }

        public override string ToString()
        {
            return $"{FilePath},{DestVar}";
        }
    }

    [Serializable]
    public class CodeInfo_FileVersion : CodeInfo
    { // FileVersion,<FilePath>,<DestVar>
        public string FilePath;
        public string DestVar;

        public CodeInfo_FileVersion(string filePath, string destVar)
        {
            FilePath = filePath;
            DestVar = destVar;
        }

        public override string ToString()
        {
            return $"{FilePath},{DestVar}";
        }
    }

    [Serializable]
    public class CodeInfo_DirCopy : CodeInfo
    { // DirCopy,<SrcDir>,<DestPath>
        public string SrcDir;
        public string DestPath;

        public CodeInfo_DirCopy(string srcDir, string destPath)
        {
            SrcDir = srcDir;
            DestPath = destPath;
        }

        public override string ToString()
        {
            return $"{SrcDir},{DestPath}";
        }
    }

    [Serializable]
    public class CodeInfo_DirDelete : CodeInfo
    { // DirDelete,<DirPath>
        public string DirPath;

        public CodeInfo_DirDelete(string dirPath)
        {
            DirPath = dirPath;
        }

        public override string ToString()
        {
            return $"{DirPath}";
        }
    }

    [Serializable]
    public class CodeInfo_DirMove : CodeInfo
    { // DirMove,<SrcDir>,<DestPath>
        public string SrcDir;
        public string DestPath;

        public CodeInfo_DirMove(string srcPath, string destPath)
        {
            SrcDir = srcPath;
            DestPath = destPath;
        }

        public override string ToString()
        {
            return $"{SrcDir},{DestPath}";
        }
    }

    [Serializable]
    public class CodeInfo_DirMake : CodeInfo
    { // DirMake,<DestDir>
        public string DestDir;

        public CodeInfo_DirMake(string destDir)
        {
            DestDir = destDir;
        }
    }

    [Serializable]
    public class CodeInfo_DirSize : CodeInfo
    { // DirSize,<Path>,<DestVar>
        public string Path;
        public string DestVar;

        public CodeInfo_DirSize(string path, string destVar)
        {
            Path = path;
            DestVar = destVar;
        }

        public override string ToString()
        {
            return $"{Path},{DestVar}";
        }
    }
    #endregion

    #region CodeInfo 02 - Registry
    [Serializable]
    public class CodeInfo_RegHiveLoad : CodeInfo
    { // RegHiveLoad,<KeyPath>,<HiveFile>
        public string KeyPath;
        public string HiveFile;

        public CodeInfo_RegHiveLoad(string keyPath, string hiveFile)
        {
            KeyPath = keyPath;
            HiveFile = hiveFile;
        }

        public override string ToString()
        {
            return $"{KeyPath},{HiveFile}";
        }
    }

    [Serializable]
    public class CodeInfo_RegHiveUnload : CodeInfo
    { // RegHiveUnload,<KeyPath>
        public string KeyPath;

        public CodeInfo_RegHiveUnload(string keyPath)
        {
            KeyPath = keyPath;
        }

        public override string ToString()
        {
            return KeyPath;
        }
    }

    [Serializable]
    public class CodeInfo_RegImport : CodeInfo
    { // RegImport,<RegFile>
        public string RegFile;

        public CodeInfo_RegImport(string regFile)
        {
            RegFile = regFile;
        }

        public override string ToString()
        {
            return RegFile;
        }
    }

    [Serializable]
    public class CodeInfo_RegExport : CodeInfo
    { // RegExport,<Key>,<RegFile>
        public RegistryKey HKey;
        public string KeyPath;
        public string RegFile;

        public CodeInfo_RegExport(RegistryKey hKey, string keyPath, string regFile)
        {
            HKey = hKey;
            KeyPath = keyPath;
            RegFile = regFile;
        }

        public override string ToString()
        {
            string HKeyStr = RegistryHelper.RegKeyToString(HKey);
            return $"{HKeyStr},{KeyPath},{RegFile}";
        }
    }

    [Serializable]
    public class CodeInfo_RegRead : CodeInfo
    { // RegRead,<HKey>,<KeyPath>,<ValueName>,<DestVar>
        public RegistryKey HKey;
        public string KeyPath;
        public string ValueName;
        public string DestVar;

        public CodeInfo_RegRead(RegistryKey hKey, string keyPath, string valueName, string destVar)
        {
            HKey = hKey;
            KeyPath = keyPath;
            ValueName = valueName;
            DestVar = destVar;
        }

        public override string ToString()
        {
            string HKeyStr = RegistryHelper.RegKeyToString(HKey);
            return $"{HKeyStr},{KeyPath},{ValueName},{DestVar}";
        }
    }

    [Serializable]
    public class CodeInfo_RegWrite : CodeInfo
    { // RegWrite,<HKey>,<ValueType>,<KeyPath>,<ValueName>,<ValueData | ValueDatas>,[NOWARN]
        public RegistryKey HKey;
        public RegistryValueKind ValueType;
        public string KeyPath;
        public string ValueName;
        public string ValueData;
        public string[] ValueDatas;
        public bool NoWarn;

        public CodeInfo_RegWrite(RegistryKey hKey, RegistryValueKind valueType, string keyPath, string valueName, string valueData, string[] valueDatas, bool noWarn)
        {
            HKey = hKey;
            ValueType = valueType;
            KeyPath = keyPath;
            ValueName = valueName;
            ValueData = valueData;
            ValueDatas = valueDatas;
            NoWarn = noWarn;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(RegistryHelper.RegKeyToString(HKey));
            b.Append(",0x");
            b.Append(((byte)ValueType).ToString("X"));
            b.Append(",");
            b.Append(KeyPath);
            b.Append(",");
            if (ValueDatas == null)
            {
                b.Append(ValueName);
                b.Append(",");
            }
            else
            {
                for (int i = 0; i < ValueDatas.Length; i++)
                {
                    b.Append(ValueDatas[i]);
                    if (i + 1 < ValueDatas.Length)
                        b.Append(",");
                }
            }
            if (NoWarn)
                b.Append(",NOWARN");
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_RegWriteLegacy : CodeInfo
    { // RegWrite,<HKey>,<ValueType>,<KeyPath>,<ValueName>,<ValueData | ValueDatas>
        public string HKey;
        public string ValueType;
        public string KeyPath;
        public string ValueName;
        public string[] ValueDatas;
        public bool NoWarn;

        public CodeInfo_RegWriteLegacy(string hKey, string valueType, string keyPath, string valueName, string[] valueDatas, bool noWarn)
        {
            HKey = hKey;
            ValueType = valueType;
            KeyPath = keyPath;
            ValueName = valueName;
            ValueDatas = valueDatas;
            NoWarn = noWarn;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(HKey);
            b.Append(",");
            b.Append(ValueType);
            b.Append(",");
            b.Append(KeyPath);
            for (int i = 0; i < ValueDatas.Length; i++)
            {
                b.Append(",");
                b.Append(ValueDatas[i]);
            }
            if (NoWarn)
                b.Append(",NOWARN");
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_RegDelete : CodeInfo
    { // RegDelete,<HKey>,<KeyPath>,[ValueName]
        public RegistryKey HKey;
        public string KeyPath;
        public string ValueName;

        public CodeInfo_RegDelete(RegistryKey hKey, string keyPath, string valueName = null)
        {
            HKey = hKey;
            KeyPath = keyPath;
            ValueName = valueName;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(RegistryHelper.RegKeyToString(HKey));
            b.Append(",");
            b.Append(KeyPath);
            if (ValueName != null)
            {
                b.Append(",");
                b.Append(ValueName);
            }
            return b.ToString();
        }
    }

    public enum RegMultiType
    {
        Append = 0, Prepend, Before, Behind, Place, Delete, Index
    }

    [Serializable]
    public class CodeInfo_RegMulti : CodeInfo
    { // RegMulti,<HKey>,<KeyPath>,<ValueName>,<Type>,<Arg1>,[Arg2]
        public RegistryKey HKey;
        public string KeyPath;
        public string ValueName;
        public RegMultiType ActionType;
        public string Arg1;
        public string Arg2;

        public CodeInfo_RegMulti(RegistryKey hKey, string keyPath, string valueName, RegMultiType actionType, string arg1, string arg2 = null)
        {
            HKey = hKey;
            KeyPath = keyPath;
            ValueName = valueName;
            ActionType = actionType;
            Arg1 = arg1;
            Arg2 = arg2;
        }

        public override string ToString()
        {
            string HKeyStr = RegistryHelper.RegKeyToString(HKey);

            StringBuilder b = new StringBuilder();
            b.Append(HKeyStr);
            b.Append(",");
            b.Append(KeyPath);
            b.Append(",");
            b.Append(ActionType.ToString().ToUpper());
            b.Append(",");
            b.Append(Arg1); // Always, should exist
            if (Arg2 != null)
            {
                b.Append(",");
                b.Append(Arg2);
            }
            return b.ToString();
        }
    }
    #endregion

    #region CodeInfo 03 - Text
    public enum TXTAddLineMode { Append, Prepend };
    [Serializable]
    public class CodeInfo_TXTAddLine : CodeInfo
    { // TXTAddLine,<FileName>,<Line>,<Mode>[,LineNum]
        public string FileName;
        public string Line;
        public string Mode;

        public CodeInfo_TXTAddLine(string fileName, string line, string mode)
        {
            FileName = fileName;
            Line = line;
            Mode = mode;
        }
    }

    [Serializable]
    public class CodeInfo_TXTAddLineOp : CodeInfo
    { 
        public List<CodeInfo_TXTAddLine> InfoList;

        public CodeInfo_TXTAddLineOp(List<CodeInfo_TXTAddLine> infoList)
        {
            InfoList = infoList;
        }
    }

    [Serializable]
    public class CodeInfo_TXTReplace : CodeInfo
    { // TXTReplace,<FileName>,<ToBeReplaced>,<ReplaceWith>
        public string FileName;
        public string ToBeReplaced;
        public string ReplaceWith;

        public CodeInfo_TXTReplace(string fileName, string toBeReplaced, string replaceWith)
        {
            FileName = fileName;
            ToBeReplaced = toBeReplaced;
            ReplaceWith = replaceWith;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(FileName);
            b.Append(",");
            b.Append(ToBeReplaced);
            b.Append(",");
            b.Append(ReplaceWith);
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_TXTDelLine : CodeInfo
    { // TXTDelLine,<FileName>,<DeleteIfBeginWith>
        public string FileName;
        public string DeleteIfBeginWith;

        public CodeInfo_TXTDelLine(string fileName, string deleteIfBeginWith)
        {
            FileName = fileName;
            DeleteIfBeginWith = deleteIfBeginWith;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(FileName);
            b.Append(",");
            b.Append(DeleteIfBeginWith);
            return b.ToString();
        }
    }

    public class CodeInfo_TXTDelLineOp : CodeInfo
    {
        public List<CodeInfo_TXTDelLine> InfoList;

        public CodeInfo_TXTDelLineOp(List<CodeInfo_TXTDelLine> infoList)
        {
            InfoList = infoList;
        }
    }

    [Serializable]
    public class CodeInfo_TXTDelSpaces : CodeInfo
    { // TXTDelSpaces,<FileName>
        public string FileName;

        public CodeInfo_TXTDelSpaces(string fileName)
        {
            FileName = fileName;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(FileName);
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_TXTDelEmptyLines : CodeInfo
    { // TXTDelEmptyLines,<FileName>
        public string FileName;

        public CodeInfo_TXTDelEmptyLines(string fileName)
        {
            FileName = fileName;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(FileName);
            return b.ToString();
        }
    }
    #endregion

    #region CodeInfo 04 - INI
    [Serializable]
    public class CodeInfo_INIRead : CodeInfo
    { // INIRead,<FileName>,<SectionName>,<Key>,<DestVar>
        public string FileName;
        public string SectionName;
        public string Key;
        public string DestVar;

        public CodeInfo_INIRead(string fileName, string sectionName, string key, string destVar)
        {
            FileName = fileName;
            SectionName = sectionName;
            Key = key;
            DestVar = destVar;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(FileName);
            b.Append(",");
            b.Append(SectionName);
            b.Append(",");
            b.Append(Key);
            b.Append(",%");
            b.Append(DestVar);
            b.Append("%");
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_INIReadOp : CodeInfo
    {    
        public List<CodeCommand> Cmds;
        public List<CodeInfo_INIRead> Infos
        {
            get => Cmds.Select(x => x.Info as CodeInfo_INIRead).ToList();
        }

        public CodeInfo_INIReadOp(List<CodeCommand> cmds)
        {
            Cmds = cmds;
        }
    }

    [Serializable]
    public class CodeInfo_INIWrite : CodeInfo
    { // INIWrite,<FileName>,<SectionName>,<Key>,<Value>
        public string FileName;
        public string SectionName;
        public string Key;
        public string Value;

        public CodeInfo_INIWrite(string fileName, string sectionName, string key, string value)
        {
            FileName = fileName;
            SectionName = sectionName;
            Key = key;
            Value = value;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(FileName);
            b.Append(",");
            b.Append(SectionName);
            b.Append(",");
            b.Append(Key);
            b.Append(",");
            b.Append(Value);
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_INIWriteOp : CodeInfo
    {
        public List<CodeCommand> Cmds;
        public List<CodeInfo_INIWrite> Infos
        {
            get => Cmds.Select(x => x.Info as CodeInfo_INIWrite).ToList();
        }

        public CodeInfo_INIWriteOp(List<CodeCommand> cmds)
        {
            Cmds = cmds;
        }
    }

    [Serializable]
    public class CodeInfo_INIDelete : CodeInfo
    { // INIDelete,<FileName>,<SectionName>,<Key>
        public string FileName;
        public string SectionName;
        public string Key;

        public CodeInfo_INIDelete(string fileName, string sectionName, string key)
        {
            FileName = fileName;
            SectionName = sectionName;
            Key = key;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(FileName);
            b.Append(",");
            b.Append(SectionName);
            b.Append(",");
            b.Append(Key);
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_INIDeleteOp : CodeInfo
    {
        public List<CodeCommand> Cmds;
        public List<CodeInfo_INIDelete> Infos
        {
            get => Cmds.Select(x => x.Info as CodeInfo_INIDelete).ToList();
        }

        public CodeInfo_INIDeleteOp(List<CodeCommand> cmds)
        {
            Cmds = cmds;
        }
    }

    [Serializable]
    public class CodeInfo_INIAddSection : CodeInfo
    { // INIAddSection,<FileName>,<SectionName>
        public string FileName;
        public string SectionName;

        public CodeInfo_INIAddSection(string fileName, string sectionName)
        {
            FileName = fileName;
            SectionName = sectionName;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(FileName);
            b.Append(",");
            b.Append(SectionName);
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_INIAddSectionOp : CodeInfo
    {
        public List<CodeCommand> Cmds;
        public List<CodeInfo_INIAddSection> Infos
        {
            get => Cmds.Select(x => x.Info as CodeInfo_INIAddSection).ToList();
        }

        public CodeInfo_INIAddSectionOp(List<CodeCommand> cmds)
        {
            Cmds = cmds;
        }
    }

    [Serializable]
    public class CodeInfo_INIDeleteSection : CodeInfo
    { // INIDeleteSection,<FileName>,<SectionName>
        public string FileName;
        public string SectionName;

        public CodeInfo_INIDeleteSection(string fileName, string sectionName)
        {
            FileName = fileName;
            SectionName = sectionName;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(FileName);
            b.Append(",");
            b.Append(SectionName);
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_INIDeleteSectionOp : CodeInfo
    { 
        public List<CodeCommand> Cmds;
        public List<CodeInfo_INIDeleteSection> Infos
        {
            get => Cmds.Select(x => x.Info as CodeInfo_INIDeleteSection).ToList();
        }

        public CodeInfo_INIDeleteSectionOp(List<CodeCommand> cmds)
        {
            Cmds = cmds;
        }
    }

    [Serializable]
    public class CodeInfo_INIWriteTextLine : CodeInfo
    { // IniWriteTextLine,<FileName>,<SectionName>,<Line>,[APPEND] 
        public string FileName;
        public string SectionName;
        public string Line;
        public bool Append;

        public CodeInfo_INIWriteTextLine(string fileName, string sectionName, string line, bool append)
        {
            FileName = fileName;
            SectionName = sectionName;
            Line = line;
            Append = append;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(FileName);
            b.Append(",");
            b.Append(SectionName);
            b.Append(",");
            b.Append(Line);
            if (Append)
                b.Append(",APPEND");
            return b.ToString();
        }
    }
   
    [Serializable]
    public class CodeInfo_INIWriteTextLineOp : CodeInfo
    {
        public List<CodeCommand> Cmds;
        public List<CodeInfo_INIWriteTextLine> Infos
        {
            get => Cmds.Select(x => x.Info as CodeInfo_INIWriteTextLine).ToList();
        }

        public CodeInfo_INIWriteTextLineOp(List<CodeCommand> cmds)
        {
            Cmds = cmds;
        }
    }

    [Serializable]
    public class CodeInfo_INIMerge : CodeInfo
    { // INIMerge,<SrcFile>,<DestFile>
        public string SrcFile;
        public string DestFile;

        public CodeInfo_INIMerge(string srcFile, string destFile)
        {
            SrcFile = srcFile;
            DestFile = destFile;
        }

        public override string ToString()
        {
            return $"{SrcFile},{DestFile}";
        }
    }
    #endregion

    #region CodeInfo 05 - Archive
    public enum ArchiveCompressFormat
    {
        Zip = 1,
    }

    [Serializable]
    public class CodeInfo_Compress : CodeInfo
    { // Compress,<Format>,<SrcPath>,<DestArchive>,[CompressLevel],[UTF8|UTF16|UTF16BE|ANSI]
        public ArchiveCompressFormat Format;
        public string SrcPath;
        public string DestArchive;
        public ArchiveHelper.CompressLevel? CompressLevel;
        public Encoding Encoding;

        public CodeInfo_Compress(ArchiveCompressFormat format, string srcDir, string destArchive, ArchiveHelper.CompressLevel? compressLevel, Encoding encoding)
        {
            Format = format;
            SrcPath = srcDir;
            DestArchive = destArchive;
            CompressLevel = compressLevel;
            Encoding = encoding;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            switch (Format)
            {
                case ArchiveCompressFormat.Zip:
                    b.Append("Zip");
                    break;
            }
            b.Append(",");
            b.Append(SrcPath);
            b.Append(",");
            b.Append(DestArchive);
            if (CompressLevel != null)
            {
                b.Append(",");
                b.Append(CompressLevel.ToString().ToUpper());
            }
            if (Encoding != null)
            {
                if (Encoding == Encoding.UTF8)
                    b.Append(",UTF8");
                else if (Encoding == Encoding.Unicode)
                    b.Append(",UTF16");
                else if (Encoding == Encoding.BigEndianUnicode)
                    b.Append(",UTF16BE");
                else if (Encoding == Encoding.ASCII)
                    b.Append(",ANSI");
            }
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_Decompress : CodeInfo
    { // Decompress,<SrcArchive>,<DestDir>,[UTF8|UTF16|UTF16BE|ANSI]
        public string SrcArchive;
        public string DestDir;
        public Encoding Encoding;

        public CodeInfo_Decompress(string srcArchive, string destArchive, Encoding encoding)
        {
            SrcArchive = srcArchive;
            DestDir = destArchive;
            Encoding = encoding;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(SrcArchive);
            b.Append(",");
            b.Append(DestDir);
            if (Encoding != null)
            {
                if (Encoding == Encoding.UTF8)
                    b.Append(",UTF8");
                else if (Encoding == Encoding.Unicode)
                    b.Append(",UTF16");
                else if (Encoding == Encoding.BigEndianUnicode)
                    b.Append(",UTF16BE");
                else if (Encoding == Encoding.ASCII)
                    b.Append(",ANSI");
            }
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_Expand : CodeInfo
    { // Expand,<SrcCab>,<DestDir>,[SingleFile],[PRESERVE],[NOWARN]
        public string SrcCab;
        public string DestDir;
        public string SingleFile;
        public bool Preserve; // Only enabled if SingleFile is set
        public bool NoWarn; // Only enabled if SingleFile is set

        public CodeInfo_Expand(string srcCab, string destDir, string singleFile, bool preserve, bool noWarn)
        {
            SrcCab = srcCab;
            DestDir = destDir;
            SingleFile = singleFile;
            Preserve = preserve;
            NoWarn = noWarn;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(SrcCab);
            b.Append(",");
            b.Append(DestDir);
            if (SingleFile != null)
            {
                b.Append(",");
                b.Append(SingleFile);
            }
            if (Preserve)
                b.Append(",PRESERVE");
            if (NoWarn)
                b.Append(",NOWARN");
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_CopyOrExpand : CodeInfo
    { // CopyOrExpand,<SrcFile>,<DestPath>,[PRESERVE],[NOWARN]
        public string SrcFile;
        public string DestPath;
        public bool Preserve;
        public bool NoWarn;

        public CodeInfo_CopyOrExpand(string srcCab, string destDir, bool preserve, bool noWarn)
        {
            SrcFile = srcCab;
            DestPath = destDir;
            Preserve = preserve;
            NoWarn = noWarn;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(SrcFile);
            b.Append(",");
            b.Append(DestPath);
            if (Preserve)
                b.Append(",PRESERVE");
            if (NoWarn)
                b.Append(",NOWARN");
            return b.ToString();
        }
    }
    #endregion

    #region CodeInfo 06 - Network
    [Serializable]
    public class CodeInfo_WebGet : CodeInfo
    { // WebGet,<URL>,<DestPath>,[HashType],[HashDigest]
        public string URL;
        public string DestPath;
        public string HashType;
        public string HashDigest;

        public CodeInfo_WebGet(string url, string destPath, string hashType, string hashDigest)
        {
            URL = url;
            DestPath = destPath;
            HashType = hashType;
            HashDigest = hashDigest;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(URL);
            b.Append(",");
            b.Append(DestPath);
            if (HashType != null && HashDigest != null)
            {
                b.Append(",");
                b.Append(HashType);
                b.Append(",");
                b.Append(HashDigest);
            }
            return b.ToString();
        }
    }
    #endregion

    #region CodeInfo 07 - Plugin
    [Serializable]
    public class CodeInfo_ExtractFile : CodeInfo
    { // ExtractFile,%PluginFile%,<DirName>,<FileName>,<ExtractTo>
        public string PluginFile;
        public string DirName;
        public string FileName;
        public string DestDir;

        public CodeInfo_ExtractFile(string pluginFile, string dirName, string fileName, string extractTo)
        {
            PluginFile = pluginFile;
            DirName = dirName;
            FileName = fileName;
            DestDir = extractTo;
        }

        public override string ToString()
        {
            return $"{PluginFile},{DirName},{FileName},{DestDir}";
        }
    }

    [Serializable]
    public class CodeInfo_ExtractAndRun : CodeInfo
    { // ExtractAndRun,%PluginFile%,<DirName>,<FileName>,[Params]
        public string PluginFile;
        public string DirName;
        public string FileName;
        public string[] Params;

        public CodeInfo_ExtractAndRun(string pluginFile, string dirName, string fileName, string[] parameters)
        {
            PluginFile = pluginFile;
            DirName = dirName;
            FileName = fileName;
            Params = parameters;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(PluginFile);
            b.Append(",");
            b.Append(DirName);
            b.Append(",");
            b.Append(FileName);
            b.Append(",");
            for (int i = 0; i < Params.Length; i++)
            {
                b.Append(Params[i]);
                if (i < Params.Length - 1)
                    b.Append(",");
            }
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_ExtractAllFiles : CodeInfo
    { // ExtractAllFiles,%PluginFile%,<DirName>,<ExtractTo>
        public string PluginFile;
        public string DirName;
        public string DestDir;

        public CodeInfo_ExtractAllFiles(string pluginFile, string dirName, string extractTo)
        {
            PluginFile = pluginFile;
            DirName = dirName;
            DestDir = extractTo;
        }

        public override string ToString()
        {
            return $"{PluginFile},{DirName},{DestDir}";
        }
    }

    [Serializable]
    public class CodeInfo_Encode : CodeInfo
    { // Encode,%PluginFile%,<DirName>,<FileName>
        public string PluginFile;
        public string DirName;
        public string FilePath; // Can have Wildcard

        public CodeInfo_Encode(string pluginFile, string dirName, string filePath)
        {
            PluginFile = pluginFile;
            DirName = dirName;
            FilePath = filePath;
        }

        public override string ToString()
        {
            return $"{PluginFile},{DirName},{FilePath}";
        }
    }
    #endregion

    #region CodeInfo 08 - Interface
    [Serializable]
    public class CodeInfo_Visible : CodeInfo
    { // Visible,<%InterfaceKey%>,<Visiblity>
        public string InterfaceKey; // Must start and end with %
        public string Visibility; // True / False

        public CodeInfo_Visible(string interfaceKey, string visibility)
        {
            InterfaceKey = interfaceKey;
            Visibility = visibility;
        }
    }

    [Serializable]
    public class CodeInfo_VisibleOp : CodeInfo
    { // Visible,<%InterfaceKey%>,<Visiblity>
        public List<CodeInfo_Visible> InfoList;

        public CodeInfo_VisibleOp(List<CodeInfo_Visible> infoList)
        {
            InfoList = infoList;
        }
    }

    [Serializable]
    public enum CodeMessageAction { None, Information, Confirmation, Error, Warning }

    [Serializable]
    public class CodeInfo_Message : CodeInfo
    { // Message,<Message>[,ICON][,TIMEOUT]
        public string Message;
        public CodeMessageAction Action; // Optional;
        public string Timeout; // Optional, Its type should be int, but set to string because of variable system

        public CodeInfo_Message(string message, CodeMessageAction action, string timeout)
        {
            Message = message;
            Action = action;
            Timeout = timeout;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(Message);
            b.Append(",");
            b.Append(Action);
            if (Timeout != null)
            {
                b.Append(",");
                b.Append(Timeout);
            }
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_Echo : CodeInfo
    {
        public string Message;
        public bool Warn;

        public CodeInfo_Echo(string message, bool warn)
        {
            Message = message;
            Warn = warn;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(Message);
            if (Warn)
                b.Append(",WARN");
            return b.ToString();
        }
    }

    #region UserInputType, UserInputInfo
    public enum UserInputType
    { 
        DirPath,
        FilePath,
    }

    [Serializable]
    public class UserInputInfo { }

    [Serializable]
    public class UserInputInfo_DirFilePath : UserInputInfo
    { // UserInput,DirFilePath,<InitPath>,<DestVar>
        public string InitPath;
        public string DestVar;

        public UserInputInfo_DirFilePath(string initPath, string destVar)
        {
            InitPath = initPath;
            DestVar = destVar;
        }

        public override string ToString()
        {
            return $"{InitPath},{DestVar}";
        }
    }
    #endregion

    [Serializable]
    public class CodeInfo_UserInput : CodeInfo
    {
        public UserInputType Type;
        public UserInputInfo SubInfo;

        public CodeInfo_UserInput(UserInputType type, UserInputInfo subInfo)
        {
            Type = type;
            SubInfo = subInfo;
        }

        public override string ToString()
        {
            return $"{Type},{SubInfo}";
        }
    }

    [Serializable]
    public class CodeInfo_AddInterface : CodeInfo
    { // AddInterface,<ScriptFile>,<Interface>,<Prefix>
        public string PluginFile;
        public string Interface;
        public string Prefix;

        public CodeInfo_AddInterface(string scriptFile, string interfaceSection, string prefix)
        {
            PluginFile = scriptFile;
            Interface = interfaceSection;
            Prefix = prefix;
        }

        public override string ToString()
        {
            return $"{PluginFile},{Interface},{Prefix}";
        }
    }
    #endregion

    #region CodeInfo 09 - Hash
    [Serializable]
    public class CodeInfo_Hash : CodeInfo
    { // Hash,<HashType>,<FilePath>,<DestVar>
        public string HashType;
        public string FilePath;
        public string DestVar;

        public CodeInfo_Hash(string hashType, string filePath, string destVar)
        {
            HashType = hashType;
            FilePath = filePath;
            DestVar = destVar;
        }

        public override string ToString()
        {
            return $"{HashType},{FilePath},{DestVar}";
        }
    }
    #endregion

    #region StrFormatType, StrFormatInfo
    public enum StrFormatType
    { // 아니 왜 사칙연산이 StrFormat에 있지...
        IntToBytes, Bytes, // IntToBytes == Bytes
        BytesToInt,
        Hex,
        Ceil, Floor, Round, // Round added in PEBakery 
        Date,
        FileName, DirPath, Path, Ext, // DirPath == Path
        Inc, Dec, Mult, Div,
        Left, Right,
        SubStr, // Added in PEBakery
        Len,
        LTrim, RTrim, CTrim, NTrim,
        UCase, LCase,
        Pos, PosX,
        Replace, ReplaceX,
        ShortPath, LongPath,
        Split,
    }

    public class StrFormatInfo { }

    [Serializable]
    public class StrFormatInfo_IntToBytes : StrFormatInfo
    { // StrFormat,Bytes,<Integer>,<DestVar>
        public string ByteSize;
        public string DestVar;

        public StrFormatInfo_IntToBytes(string byteSize, string destVar)
        {
            ByteSize = byteSize;
            DestVar = destVar;
        }

        public override string ToString()
        {
            return $"{ByteSize},{DestVar}";
        }
    }

    [Serializable]
    public class StrFormatInfo_BytesToInt : StrFormatInfo
    { // StrFormat,BytesToInt,<Bytes>,<DestVar>
        public string HumanReadableByteSize;
        public string DestVar;

        public StrFormatInfo_BytesToInt(string byteSize, string destVar)
        {
            HumanReadableByteSize = byteSize;
            DestVar = destVar;
        }

        public override string ToString()
        {
            return $"{HumanReadableByteSize},{DestVar}";
        }
    }

    [Serializable]
    public class StrFormatInfo_Hex : StrFormatInfo
    { // StrFormat,Hex,<Integer>,<DestVar>
        public string Integer;
        public string DestVar;

        public StrFormatInfo_Hex(string integer, string destVar)
        {
            Integer = integer;
            DestVar = destVar;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(Integer);
            b.Append(",");
            b.Append(DestVar);
            return b.ToString();
        }
    }

    [Serializable]
    public class StrFormatInfo_CeilFloorRound : StrFormatInfo
    {
        // StrFormat,Ceil,<SizeVar>,<CeilTo>
        // StrFormat,Floor,<SizeVar>,<FloorTo>
        // StrFormat,Round,<SizeVar>,<RoundTo>
        // <RoundTo> can be [PositiveInteger], [K], [M], [G], [T], [P]

        public string SizeVar;
        public string RoundTo;

        public StrFormatInfo_CeilFloorRound(string sizeVar, string roundTo)
        {
            SizeVar = sizeVar;
            RoundTo = roundTo;
        }

        public override string ToString()
        {
            return $"{SizeVar},{RoundTo}";
        }
    }

    [Serializable]
    public class StrFormatInfo_Date : StrFormatInfo
    { // StrFormat,Date,<DestVar>,<FormatString>
        public string DestVar;
        public string FormatString;

        public StrFormatInfo_Date(string destVar, string formatString)
        {
            DestVar = destVar;
            FormatString = formatString;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(DestVar);
            b.Append(",");
            b.Append(StringEscaper.Doublequote(FormatString));
            return b.ToString();
        }
    }

    [Serializable]
    public class StrFormatInfo_Path : StrFormatInfo
    {
        // StrFormat,FileName,<FilePath>,<DestVar>
        // StrFormat,DirPath,<FilePath>,<DestVar> -- Same with StrFormat,Path
        // StrFormat,Ext,<FilePath>,<DestVar>
        public string FilePath;
        public string DestVar;

        public StrFormatInfo_Path(string filePath, string destVar)
        {
            FilePath = filePath;
            DestVar = destVar;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(StringEscaper.Doublequote(FilePath));
            b.Append(",");
            b.Append(DestVar);            
            return b.ToString();
        }
    }

    [Serializable]
    public class StrFormatInfo_Arithmetic : StrFormatInfo
    {
        // StrFormat,Inc,<DestVar>,<Integer>
        // StrFormat,Dec,<DestVar>,<Integer>
        // StrFormat,Mult,<DestVar>,<Integer>
        // StrFormat,Div,<DestVar>,<Integer>

        public string DestVar;
        public string Integer; // These value's type must be integer, but set to string because of variables system

        public StrFormatInfo_Arithmetic(string destVar, string integer)
        {
            DestVar = destVar;
            Integer = integer;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(DestVar);
            b.Append(",");
            b.Append(StringEscaper.Doublequote(Integer));
            return b.ToString();
        }
    }

    [Serializable]
    public class StrFormatInfo_LeftRight : StrFormatInfo
    { // Note : Integer can be negative integer, not like WB082's limitation
        // StrFormat,Left,<SrcString>,<Integer>,<DestVar>
        // StrFormat,Right,<SrcString>,<Integer>,<DestVar>
        public string SrcStr;
        public string CutLen; 
        public string DestVar;

        public StrFormatInfo_LeftRight(string srcString, string integer, string destVar)
        {
            SrcStr = srcString;
            CutLen = integer;
            DestVar = destVar;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(SrcStr);
            b.Append(",");
            b.Append(StringEscaper.Doublequote(CutLen));
            b.Append(",");
            b.Append(DestVar);
            return b.ToString();
        }
    }

    [Serializable]
    public class StrFormatInfo_SubStr : StrFormatInfo
    { // StrFormat,SubStr,<SrcString>,<StartPos>,<Length>,<DestVar>
        public string SrcStr;
        public string StartPos; // Index start from 1, not 0!
        public string Length; 
        public string DestVar;

        public StrFormatInfo_SubStr(string srcString, string startPos, string length, string destVar)
        {
            SrcStr = srcString;
            StartPos = startPos;
            Length = length;
            DestVar = destVar;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(SrcStr);
            b.Append(",");
            b.Append(StringEscaper.Doublequote(StartPos));
            b.Append(",");
            b.Append(StringEscaper.Doublequote(Length));
            b.Append(",");
            b.Append(DestVar);
            return b.ToString();
        }
    }

    [Serializable]
    public class StrFormatInfo_Len : StrFormatInfo
    { // StrFormat,Len,<SrcString>,<DestVarName>
        public string SrcStr;
        public string DestVar;

        public StrFormatInfo_Len(string srcString, string destVar)
        {
            SrcStr = srcString;
            DestVar = destVar;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(SrcStr);
            b.Append(",");
            b.Append(DestVar);
            return b.ToString();
        }
    }

    [Serializable]
    public class StrFormatInfo_Trim : StrFormatInfo
    {
        // StrFormat,LTrim,<SrcString>,<Integer>,<DestVar>
        // StrFormat,RTrim,<SrcString>,<Integer>,<DestVar>
        // StrFormat,CTrim,<SrcString>,<Chars>,<DestVar>

        public string SrcStr;
        public string ToTrim;
        public string DestVarName;

        public StrFormatInfo_Trim(string srcString, string trimValue, string destVar)
        {
            SrcStr = srcString;
            ToTrim = trimValue;
            DestVarName = destVar;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(SrcStr);
            b.Append(",");
            b.Append(StringEscaper.Doublequote(ToTrim));
            b.Append(",");
            b.Append(DestVarName);
            return b.ToString();
        }
    }

    [Serializable]
    public class StrFormatInfo_NTrim : StrFormatInfo
    { // StrFormat,NTrim,<SrcString>,<DestVar>
        public string SrcStr;
        public string DestVar;

        public StrFormatInfo_NTrim(string srcString,  string destVar)
        {
            SrcStr = srcString;
            DestVar = destVar;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(SrcStr);
            b.Append(",");
            b.Append(DestVar);
            return b.ToString();
        }
    }

    [Serializable]
    public class StrFormatInfo_ULCase : StrFormatInfo
    {
        // StrFormat,UCase,<SrcString>,<DestVar>
        // StrFormat,LCase,<SrcString>,<DestVar>

        public string SrcStr;
        public string DestVar;

        public StrFormatInfo_ULCase(string srcStr, string destVar)
        {
            SrcStr = srcStr;
            DestVar = destVar;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(SrcStr);
            b.Append(",");
            b.Append(DestVar);
            return b.ToString();
        }
    }

    [Serializable]
    public class StrFormatInfo_Pos : StrFormatInfo
    { // StrFormat,Pos,<SrcString>,<SubString>,<DestVar>
        public string SrcStr;
        public string SubStr;
        public string DestVar;

        public StrFormatInfo_Pos(string srcString, string subString, string destVar)
        {
            SrcStr = srcString;
            SubStr = subString;
            DestVar = destVar;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(StringEscaper.QuoteEscape(SrcStr));
            b.Append(",");
            b.Append(StringEscaper.QuoteEscape(SubStr));
            b.Append(",");
            b.Append(DestVar);
            return b.ToString();
        }
    }

    [Serializable]
    public class StrFormatInfo_Replace : StrFormatInfo
    {
        // StrFormat,Replace,<SrcString>,<ToBeReplaced>,<ReplaceWith>,<DestVar>
        // StrFormat,ReplaceX,<SrcString>,<ToBeReplaced>,<ReplaceWith>,<DestVar>

        public string SrcString;
        public string ToBeReplaced;
        public string ReplaceWith;
        public string DestVar;

        public StrFormatInfo_Replace(string srcString, string toBeReplaced, string replaceWith, string destVar)
        {
            SrcString = srcString;
            ToBeReplaced = toBeReplaced;
            ReplaceWith = replaceWith;
            DestVar = destVar;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(StringEscaper.QuoteEscape(SrcString));
            b.Append(",");
            b.Append(StringEscaper.QuoteEscape(ToBeReplaced));
            b.Append(",");
            b.Append(StringEscaper.QuoteEscape(ReplaceWith));
            b.Append(",");
            b.Append(DestVar);
            return b.ToString();
        }
    }

    [Serializable]
    public class StrFormatInfo_ShortLongPath : StrFormatInfo
    {
        // StrFormat,ShortPath,<SrcString>,<DestVar>
        // StrFormat,LongPath,<SrcString>,<DestVar>

        public string SrcString;
        public string DestVar;

        public StrFormatInfo_ShortLongPath(string srcString, string destVar)
        {
            SrcString = srcString;
            DestVar = destVar;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(StringEscaper.QuoteEscape(SrcString));
            b.Append(",");
            b.Append(DestVar);
            return b.ToString();
        }
    }

    [Serializable]
    public class StrFormatInfo_Split : StrFormatInfo
    { // StrFormat,Split,<SrcString>,<Delimeter>,<Index>,<DestVar>
        public string SrcString;
        public string Delimeter;
        public string Index; 
        public string DestVar;

        public StrFormatInfo_Split(string srcString, string delimeter, string index, string destVar)
        {
            SrcString = srcString;
            Delimeter = delimeter;
            Index = index;
            DestVar = destVar;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(StringEscaper.QuoteEscape(SrcString));
            b.Append(",");
            b.Append(StringEscaper.QuoteEscape(Delimeter));
            b.Append(",");
            b.Append(StringEscaper.QuoteEscape(Index));
            b.Append(",");
            b.Append(StringEscaper.QuoteEscape(DestVar));
            return b.ToString();
        }
    }
    #endregion

    #region CodeInfo 10 - String
    [Serializable]
    public class CodeInfo_StrFormat : CodeInfo
    {
        public StrFormatType Type;
        public StrFormatInfo SubInfo;

        public CodeInfo_StrFormat(StrFormatType type, StrFormatInfo subInfo)
        {
            Type = type;
            SubInfo = subInfo;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(Type);
            b.Append(",");
            b.Append(SubInfo);
            return b.ToString();
        }
    }
    #endregion

    #region MathType, MathInfo
    public enum MathType
    { 
        Add, Sub, Mul, Div,
        IntDiv,
        Neg,
        ToSign, ToUnsign,
        BoolAnd, BoolOr, BoolXor,
        BoolNot,
        BitAnd, BitOr, BitXor,
        BitNot,
        BitShift,
        Ceil, Floor, Round, 
        Abs,
        Pow,
    }

    public class MathInfo { }

    [Serializable]
    public class MathInfo_Arithmetic : MathInfo
    { 
        // Math,Add,<DestVar>,<Src1>,<Src2>
        // Math,Sub,<DestVar>,<Src1>,<Src2>
        // Math,Mul,<DestVar>,<Src1>,<Src2>
        // Math,Div,<DestVar>,<Src1>,<Src2>

        public string DestVar;
        public string Src1;
        public string Src2;

        public MathInfo_Arithmetic(string destVar, string src1, string src2)
        {
            DestVar = destVar;
            Src1 = src1;
            Src2 = src2;
        }

        public override string ToString()
        {
            return $"{DestVar},{Src1},{Src2}";
        }
    }

    [Serializable]
    public class MathInfo_IntDiv : MathInfo
    { // Math,IntDiv,<QuotientVar>,<RemainderVar>,<Src1>,<Src2>
        public string QuotientVar;
        public string RemainderVar;
        public string Src1;
        public string Src2;

        public MathInfo_IntDiv(string quotientVar, string remainderVar, string src1, string src2)
        {
            QuotientVar = quotientVar;
            RemainderVar = remainderVar;
            Src1 = src1;
            Src2 = src2;
        }

        public override string ToString()
        {
            return $"{QuotientVar},{RemainderVar},{Src1},{Src2}";
        }
    }

    [Serializable]
    public class MathInfo_Neg : MathInfo
    { // Math,Neg,<DestVar>,<Src>
        public string DestVar;
        public string Src;

        public MathInfo_Neg(string destVar, string src)
        {
            DestVar = destVar;
            Src = src;
        }

        public override string ToString()
        {
            return $"{DestVar},{Src}";
        }
    }

    [Serializable]
    public class MathInfo_IntegerSignedness : MathInfo
    {
        // Math,ToSign,<DestVar>,<Src>,[8|16|32|64]
        // Math,ToUnsign,<DestVar>,<Src>,[8|16|32|64]

        public string DestVar;
        public string Src;
        public uint Size;

        public MathInfo_IntegerSignedness(string destVar, string src, uint size)
        {
            DestVar = destVar;
            Src = src;
            Size = size;
        }

        public override string ToString()
        {
            return $"{DestVar},{Src},{Size}";
        }
    }

    [Serializable]
    public class MathInfo_BoolLogicOper : MathInfo
    {
        // Math,BoolAnd,<DestVar>,<Src1>,<Src2>
        // Math,BoolOr,<DestVar>,<Src1>,<Src2>
        // Math,BoolXor,<DestVar>,<Src1>,<Src2>

        public string DestVar;
        public string Src1;
        public string Src2;

        public MathInfo_BoolLogicOper(string destVar, string src1, string src2)
        {
            DestVar = destVar;
            Src1 = src1;
            Src2 = src2;
        }

        public override string ToString()
        {
            return $"{DestVar},{Src1},{Src2}";
        }
    }

    [Serializable]
    public class MathInfo_BoolNot : MathInfo
    { // Math,BoolNot,<DestVar>,<Src>
        public string DestVar;
        public string Src;

        public MathInfo_BoolNot(string destVar, string src)
        {
            DestVar = destVar;
            Src = src;
        }

        public override string ToString()
        {
            return $"{DestVar},{Src}";
        }
    }

    [Serializable]
    public class MathInfo_BitLogicOper : MathInfo
    {
        // Math,BitAnd,<DestVar>,<Src1>,<Src2>
        // Math,BitOr,<DestVar>,<Src1>,<Src2>
        // Math,BitXor,<DestVar>,<Src1>,<Src2>

        public string DestVar;
        public string Src1; // Should be unsigned
        public string Src2; // Should be unsigned

        public MathInfo_BitLogicOper(string destVar, string src1, string src2)
        {
            DestVar = destVar;
            Src1 = src1;
            Src2 = src2;
        }

        public override string ToString()
        {
            return $"{DestVar},{Src1},{Src2}";
        }
    }

    [Serializable]
    public class MathInfo_BitNot : MathInfo
    { // Math,BitNot,<DestVar>,<Src>,[8|16|32|64]
        public string DestVar;
        public string Src; // Should be unsigned
        public uint Size;

        public MathInfo_BitNot(string destVar, string src, uint size)
        {
            DestVar = destVar;
            Src = src;
            Size = size;
        }

        public override string ToString()
        {
            return $"{DestVar},{Src},{Size}";
        }
    }

    [Serializable]
    public class MathInfo_BitShift : MathInfo
    { // Math,BitShift,<DestVar>,<Src>,<LEFT|RIGHT>,<Shift>,[8|16|32|64],[UNSIGNED]
        public string DestVar;
        public string Src;
        public string LeftRight;
        public string Shift;
        public uint Size;
        public bool Unsigned;

        public MathInfo_BitShift(string destVar, string src, string leftRight, string shift, uint size, bool _unsigned)
        {
            DestVar = destVar;
            Src = src;
            LeftRight = leftRight;
            Shift = shift;
            Size = size;
            Unsigned = _unsigned;
        }

        public override string ToString()
        {
            return $"{DestVar},{Src},{LeftRight},{Shift},{Size},{Unsigned}";
        }
    }

    [Serializable]
    public class MathInfo_CeilFloorRound : MathInfo
    {
        // Math,Ceil,<DestVar>,<Src>,<Unit>
        // Math,Floor,<DestVar>,<Src>,<Unit>
        // Math,Round,<DestVar>,<Src>,<Unit>

        public string DestVar;
        public string Src;
        public string Unit;

        public MathInfo_CeilFloorRound(string destVar, string src, string unit)
        {
            DestVar = destVar;
            Src = src;
            Unit = unit;
        }

        public override string ToString()
        {
            return $"{DestVar},{Src},{Unit}";
        }
    }

    [Serializable]
    public class MathInfo_Abs : MathInfo
    { // Math,Abs,<DestVar>,<Src>
        public string DestVar;
        public string Src;

        public MathInfo_Abs(string destVar, string src)
        {
            DestVar = destVar;
            Src = src;
        }

        public override string ToString()
        {
            return $"{DestVar},{Src}";
        }
    }

    [Serializable]
    public class MathInfo_Pow : MathInfo
    { // Math,Pow,<DestVar>,<Base>,<PowerOf>
        public string DestVar;
        public string Base;
        public string Power;

        public MathInfo_Pow(string destVar, string _base, string powerOf)
        {
            DestVar = destVar;
            Base = _base;
            Power = powerOf;
        }

        public override string ToString()
        {
            return $"{DestVar},{Base},{Power}";
        }
    }
    #endregion

    #region CodeInfo 11 - Math
    [Serializable]
    public class CodeInfo_Math : CodeInfo
    {
        public MathType Type;
        public MathInfo SubInfo;

        public CodeInfo_Math(MathType type, MathInfo subInfo)
        {
            Type = type;
            SubInfo = subInfo;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(Type);
            b.Append(",");
            b.Append(SubInfo);
            return b.ToString();
        }
    }
    #endregion

    #region CodeInfo 12 - System
    [Serializable]
    public class CodeInfo_System : CodeInfo
    {
        public SystemType Type;
        public SystemInfo SubInfo;

        public CodeInfo_System(SystemType type, SystemInfo subInfo)
        {
            Type = type;
            SubInfo = subInfo;
        }

        public override string ToString()
        {
            return $"{Type},{SubInfo.ToString()}";
        }
    }

    #region SystemType, SystemInfo
    public enum SystemType
    { // 아니 왜 사칙연산이 StrFormat에 있지...
        Cursor,
        ErrorOff,
        GetEnv,
        GetFreeDrive,
        GetFreeSpace,
        IsAdmin,
        Log,
        OnBuildExit,
        OnScriptExit, OnPluginExit,
        RefreshInterface,
        RescanScripts,
        SaveLog,

        // Deprecated, WB082 Compability Shim
        HasUAC, 
        FileRedirect, 
        RegRedirect,
        RebuildVars,
    }

    [Serializable]
    public class SystemInfo { }

    [Serializable]
    public class SystemInfo_Cursor : SystemInfo
    { // System,Cursor,<IconKind>
        public string IconKind;

        public SystemInfo_Cursor(string iconKind)
        {
            IconKind = iconKind;
        }

        public override string ToString()
        {
            return $"Cursor,{IconKind}";
        }
    }

    [Serializable]
    public class SystemInfo_ErrorOff : SystemInfo
    { // System,ErrorOff,[Lines]
        public string Lines;

        public SystemInfo_ErrorOff(string lines = "1")
        {
            Lines = lines;
        }

        public override string ToString()
        {
            return $"ErrorOff,{Lines}";
        }
    }

    [Serializable]
    public class SystemInfo_GetEnv : SystemInfo
    { // System,GetEnv,<EnvVarName>,<DestVar>
        public string EnvVarName;
        public string DestVar;

        public SystemInfo_GetEnv(string envVarName, string destVar)
        {
            EnvVarName = envVarName;
            DestVar = destVar;
        }

        public override string ToString()
        {
            return $"GetEnv,{EnvVarName},{DestVar}";
        }
    }

    [Serializable]
    public class SystemInfo_GetFreeDrive : SystemInfo
    { // System,GetFreeDrive,<DestVar>
        public string DestVar;

        public SystemInfo_GetFreeDrive(string destVar)
        {
            DestVar = destVar;
        }

        public override string ToString()
        {
            return $"GetFreeDrive,{DestVar}";
        }
    }

    [Serializable]
    public class SystemInfo_GetFreeSpace : SystemInfo
    { // System,GetFreeSpace,<Path>,<DestVar>
        public string Path;
        public string DestVar;

        public SystemInfo_GetFreeSpace(string path, string destVar)
        {
            Path = path;
            DestVar = destVar;
        }

        public override string ToString()
        {
            return $"GetFreeDrive,{Path},{DestVar}";
        }
    }

    [Serializable]
    public class SystemInfo_HasUAC : SystemInfo
    { // System,HasUAC,<DestVar>
        public string DestVar;

        public SystemInfo_HasUAC(string destVar)
        {
            DestVar = destVar;
        }

        public override string ToString()
        {
            return $"HasUAC,{DestVar}";
        }
    }

    [Serializable]
    public class SystemInfo_IsAdmin : SystemInfo
    { // System,IsAdmin,<DestVar>
        public string DestVar;

        public SystemInfo_IsAdmin(string destVar)
        {
            DestVar = destVar;
        }

        public override string ToString()
        {
            return $"IsAdmin,{DestVar}";
        }
    }

    [Serializable]
    public class SystemInfo_OnBuildExit : SystemInfo
    { // System,OnBuildExit,<Command>
        public CodeCommand Cmd;

        public SystemInfo_OnBuildExit(CodeCommand cmd)
        {
            Cmd = cmd;
        }

        public override string ToString()
        {
            return $"OnBuildExit,{Cmd}";
        }
    }

    [Serializable]
    public class SystemInfo_OnPluginExit : SystemInfo
    { // System,OnPluginExit,<Command>
        public CodeCommand Cmd;

        public SystemInfo_OnPluginExit(CodeCommand cmd)
        {
            Cmd = cmd;
        }

        public override string ToString()
        {
            return $"OnPluginExit,{Cmd}";
        }
    }

    [Serializable]
    public class SystemInfo_RefreshInterface : SystemInfo
    { // System,RefreshInterface
        public SystemInfo_RefreshInterface() { }
        public override string ToString() { return "RefreshInterface"; }
    }

    [Serializable]
    public class SystemInfo_RescanScripts : SystemInfo
    { // System,RescanScripts
        public SystemInfo_RescanScripts() { }
        public override string ToString() { return "RescanScripts"; }
    }

    [Serializable]
    public class SystemInfo_SaveLog : SystemInfo
    { // System,SaveLog,<DestPath>,[LogFormat]
        public string DestPath;
        public string LogFormat;

        public SystemInfo_SaveLog(string destPath, string logFormat = "HTML")
        {
            DestPath = destPath;
            LogFormat = logFormat;
        }

        public override string ToString()
        {
            return $"SaveLog,{DestPath},{LogFormat}";
        }
    }
    #endregion

    /// <summary>
    /// For ShellExecute, ShellExecuteEx, ShellExecuteDelete
    /// </summary>
    [Serializable]
    public class CodeInfo_ShellExecute : CodeInfo
    {
        // ShellExecute,<Action>,<FilePath>[,Params][,WorkDir][,%ExitOutVar%]
        // ShellExecuteEx,<Action>,<FilePath>[,Params][,WorkDir]
        // ShellExecuteDelete,<Action>,<FilePath>[,Params][,WorkDir][,%ExitOutVar%]

        public string Action;
        public string FilePath;
        public string Params; // Optional
        public string WorkDir;      // Optional
        public string ExitOutVar;   // Optional

        /// <summary>
        /// ShellExecute
        /// </summary>
        /// <param name="action"></param>
        /// <param name="filePath"></param>
        /// <param name="parameters">Optinal</param>
        /// <param name="workDir">Optinal</param>
        /// <param name="exitOutVar">Optinal - Variable</param>
        public CodeInfo_ShellExecute(string action, string filePath, string parameters, string workDir, string exitOutVar)
        {
            Action = action;
            FilePath = filePath;
            Params = parameters;
            WorkDir = workDir;
            ExitOutVar = exitOutVar;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(Action);
            b.Append(",");
            b.Append(FilePath);
            if (Params != null)
            {
                b.Append(",");
                b.Append(Params);
            }
            if (WorkDir != null)
            {
                b.Append(",");
                b.Append(WorkDir);
            }
            if (ExitOutVar != null)
            {
                b.Append(",");
                b.Append(ExitOutVar);
            }
            return b.ToString();
        }
    }
    #endregion

    #region BranchCondition
    public enum BranchConditionType
    {
        None = 0,
        // Comparison
        Equal, EqualX, Smaller, Bigger, SmallerEqual, BiggerEqual,
        // Existance
        // Note : Wrong Terminoloy with Registry, see https://msdn.microsoft.com/en-us/library/windows/desktop/ms724946(v=vs.85).aspx
        ExistFile,
        ExistDir,
        ExistSection,
        ExistRegSection, ExistRegSubKey,
        ExistRegKey, ExistRegValue,
        ExistVar,
        ExistMacro,
        // ETC
        Ping, Online, Question,
        // Deprecated
        License
    }

    [Serializable]
    public class BranchCondition
    {
        public BranchConditionType Type;
        public bool NotFlag;

        public string Arg1;
        public string Arg2;
        public string Arg3;
        public BranchCondition(BranchConditionType type, bool notFlag)
        {
            Type = type;
            NotFlag = notFlag;
            switch (type)
            {
                case BranchConditionType.Online:
                    break;
                default:
                    throw new InternalException($"Wrong BranchCondition, [{type}] does not take 1 argument");
            }
        }

        public BranchCondition(BranchConditionType type, bool notFlag, string arg1)
        {
            Type = type;
            NotFlag = notFlag;
            switch (type)
            {
                case BranchConditionType.ExistFile:
                case BranchConditionType.ExistDir:
                case BranchConditionType.ExistVar:
                case BranchConditionType.ExistMacro:
                case BranchConditionType.Ping:
                case BranchConditionType.Question: // can have 1 or 3 argument
                    Arg1 = arg1;
                    break;
                default:
                    throw new InternalException($"Wrong BranchCondition, [{type}] does not take 1 argument");
            }
        }

        public BranchCondition(BranchConditionType type, bool notFlag, string arg1, string arg2)
        {
            Type = type;
            NotFlag = notFlag;
            switch (type)
            {
                case BranchConditionType.Equal:
                case BranchConditionType.Smaller:
                case BranchConditionType.Bigger:
                case BranchConditionType.SmallerEqual:
                case BranchConditionType.BiggerEqual:
                case BranchConditionType.ExistSection:
                case BranchConditionType.ExistRegSection:
                case BranchConditionType.ExistRegSubKey:
                    Arg1 = arg1;
                    Arg2 = arg2;
                    break;
                default:
                    throw new InternalException($"Wrong BranchCondition, [{type}] does not take 2 arguments");
            }
        }

        public BranchCondition(BranchConditionType type, bool notFlag, string arg1, string arg2, string arg3)
        {
            Type = type;
            NotFlag = notFlag;
            switch (type)
            {
                case BranchConditionType.ExistRegKey:
                case BranchConditionType.ExistRegValue:
                case BranchConditionType.Question: // can have 1 or 3 argument
                    Arg1 = arg1;
                    Arg2 = arg2;
                    Arg3 = arg3;
                    break;
                default:
                    throw new InternalException($"Wrong BranchCondition, [{type}] does not take 3 arguments");
            }
        }

        /// <summary>
        /// Return true if matched
        /// </summary>
        /// <param name="s"></param>
        /// <param name="logMessage"></param>
        /// <returns></returns>
        public bool Check(EngineState s, out string logMessage)
        {
            bool match = false;
            switch (Type)
            {
                case BranchConditionType.Equal:
                case BranchConditionType.Smaller:
                case BranchConditionType.Bigger:
                case BranchConditionType.SmallerEqual:
                case BranchConditionType.BiggerEqual:
                    {
                        string compArg1 = StringEscaper.Preprocess(s, Arg1);
                        string compArg2 = StringEscaper.Preprocess(s, Arg2);

                        NumberHelper.CompareStringNumberResult comp = NumberHelper.CompareStringNumber(compArg1, compArg2);
                        switch (comp)
                        {
                            case NumberHelper.CompareStringNumberResult.Equal: // For String and Number
                                {
                                    if (Type == BranchConditionType.Equal && !NotFlag
                                        || Type == BranchConditionType.SmallerEqual && !NotFlag
                                        || Type == BranchConditionType.BiggerEqual && !NotFlag
                                        || Type == BranchConditionType.Smaller && NotFlag
                                        || Type == BranchConditionType.Bigger && NotFlag)
                                        match = true;
                                    logMessage = $"[{compArg1}] is equal to [{compArg2}]";
                                }
                                break;
                            case NumberHelper.CompareStringNumberResult.Smaller: // For Number
                                {
                                    if (Type == BranchConditionType.Smaller && !NotFlag
                                        || Type == BranchConditionType.SmallerEqual && !NotFlag
                                        || Type == BranchConditionType.Bigger && NotFlag
                                        || Type == BranchConditionType.BiggerEqual && NotFlag
                                        || Type == BranchConditionType.Equal && NotFlag)
                                        match = true;
                                    logMessage = $"[{compArg1}] is smaller than [{compArg2}]";
                                }
                                break;
                            case NumberHelper.CompareStringNumberResult.Bigger: // For Number
                                {
                                    if (Type == BranchConditionType.Bigger && !NotFlag
                                        || Type == BranchConditionType.BiggerEqual && !NotFlag
                                        || Type == BranchConditionType.Smaller && NotFlag
                                        || Type == BranchConditionType.SmallerEqual && NotFlag
                                        || Type == BranchConditionType.Equal && NotFlag)
                                        match = true;
                                    logMessage = $"[{compArg1}] is bigger than [{compArg2}]";
                                }
                                break;
                            case NumberHelper.CompareStringNumberResult.NotEqual: // For String
                                {
                                    if (Type == BranchConditionType.Equal && NotFlag
                                        || Type == BranchConditionType.Smaller && !NotFlag
                                        || Type == BranchConditionType.SmallerEqual && NotFlag
                                        || Type == BranchConditionType.Bigger && !NotFlag
                                        || Type == BranchConditionType.BiggerEqual && NotFlag)
                                        match = true;
                                    logMessage = $"[{compArg1}] is not equal to [{compArg2}]";
                                }
                                break;
                            default:
                                throw new InternalException($"Cannot compare [{compArg1}] and [{compArg2}]");
                        }
                    }
                    break;
                case BranchConditionType.ExistFile:
                    {
                        string filePath = StringEscaper.Preprocess(s, Arg1);

                        // Check filePath contains wildcard
                        bool filePathContainsWildcard = true;
                        if (filePath.IndexOfAny(new char[] { '*', '?' }) == -1) // No wildcard
                            filePathContainsWildcard = false;

                        // Check if file exists
                        if (filePath.Trim().Equals(string.Empty, StringComparison.Ordinal))
                        {
                            match = false;
                        }
                        else if (Directory.Exists(Path.GetDirectoryName(filePath)) == false)
                        {
                            match = false;
                        }
                        else if (filePathContainsWildcard) 
                        {
                            string[] list = Directory.GetFiles(FileHelper.GetDirNameEx(filePath), Path.GetFileName(filePath));
                            if (0 < list.Length)
                                match = true;
                            else
                                match = false;
                        }
                        else
                        {
                            match = File.Exists(filePath);
                        }

                        if (match)
                            logMessage = $"File [{filePath}] exists";
                        else
                            logMessage = $"File [{filePath}] does not exist";

                        if (NotFlag)
                            match = !match;
                    }
                    break;
                case BranchConditionType.ExistDir:
                    {
                        string dirPath = StringEscaper.Preprocess(s, Arg1);

                        // Check filePath contains wildcard
                        bool dirPathContainsWildcard = true;
                        if (dirPath.IndexOfAny(new char[] { '*', '?' }) == -1) // No wildcard
                            dirPathContainsWildcard = false;

                        // Check if directory exists
                        if (dirPath.Trim().Equals(string.Empty, StringComparison.Ordinal))
                        {
                            match = false;
                        }
                        else if (Directory.Exists(Path.GetDirectoryName(dirPath)) == false)
                        {
                            match = false;
                        }
                        else if (dirPathContainsWildcard)
                        {
                            string[] list = Directory.GetDirectories(FileHelper.GetDirNameEx(dirPath), Path.GetFileName(dirPath));
                            if (0 < list.Length)
                                match = true;
                            else
                                match = false;
                        }
                        else
                        {
                            match = Directory.Exists(dirPath);
                        }

                        if (match)
                            logMessage = $"Directory [{dirPath}] exists";
                        else
                            logMessage = $"Directory [{dirPath}] does not exist";

                        if (NotFlag)
                            match = !match;
                    }
                    break;
                case BranchConditionType.ExistSection:
                    {
                        string iniFile = StringEscaper.Preprocess(s, Arg1);
                        string section = StringEscaper.Preprocess(s, Arg2);

                        match = Ini.CheckSectionExist(iniFile, section);
                        if (match)
                            logMessage = $"Section [{section}] exists in INI file [{iniFile}]";
                        else
                            logMessage = $"Section [{section}] does not exist in INI file [{iniFile}]";

                        if (NotFlag)
                            match = !match;
                    }
                    break;
                case BranchConditionType.ExistRegSection:
                case BranchConditionType.ExistRegSubKey:
                    {
                        string rootKey = StringEscaper.Preprocess(s, Arg1);
                        string subKey = StringEscaper.Preprocess(s, Arg2);

                        RegistryKey regRoot = RegistryHelper.ParseStringToRegKey(rootKey);
                        if (regRoot == null)
                            throw new InvalidRegKeyException($"Invalid registry root key [{rootKey}]");
                        using (RegistryKey regSubKey = regRoot.OpenSubKey(subKey))
                        {
                            match = (regSubKey != null);
                            if (match)
                                logMessage = $"Registry SubKey [{rootKey}\\{subKey}] exists";
                            else
                                logMessage = $"Registry SubKey [{rootKey}\\{subKey}] does not exist";
                        }

                        if (NotFlag)
                            match = !match;
                    }
                    break;
                case BranchConditionType.ExistRegKey:
                case BranchConditionType.ExistRegValue:
                    {
                        string rootKey = StringEscaper.Preprocess(s, Arg1);
                        string subKey = StringEscaper.Preprocess(s, Arg2);
                        string valueName = StringEscaper.Preprocess(s, Arg3);

                        match = true;
                        RegistryKey regRoot = RegistryHelper.ParseStringToRegKey(rootKey);
                        if (regRoot == null)
                            throw new InvalidRegKeyException($"Invalid registry root key [{rootKey}]");
                        using (RegistryKey regSubKey = regRoot.OpenSubKey(subKey))
                        {
                            if (regSubKey == null)
                                match = false;
                            else
                            {
                                object value = regSubKey.GetValue(valueName, null);
                                if (value == null)
                                    match = false;
                            }
                            if (match)
                                logMessage = $"Registry Value [{rootKey}\\{subKey}\\{valueName}] exists";
                            else
                                logMessage = $"Registry Value [{rootKey}\\{subKey}\\{valueName}] does not exist";
                        }

                        if (NotFlag)
                            match = !match;
                    }
                    break;
                case BranchConditionType.Ping:
                    {
                        string host = StringEscaper.Preprocess(s, Arg1);

                        Ping pinger = new Ping();
                        try
                        {
                            PingReply reply = pinger.Send(host);
                            if (reply.Status == IPStatus.Success)
                                match = true;
                            else
                                match = false;

                            if (match)
                                logMessage = $"Ping to [{host}] successed";
                            else
                                logMessage = $"Ping to [{host}] failed";
                        }
                        catch (PingException e)
                        {
                            match = false;
                            logMessage = $"Error while pinging to [{host}] : [{e.Message}]";
                        }

                        if (NotFlag)
                            match = !match;
                    }
                    break;
                case BranchConditionType.Online:
                    {
                        match = NetworkInterface.GetIsNetworkAvailable();

                        if (match)
                            logMessage = "System is connected to internet";
                        else
                            logMessage = "System is not connected to internet";

                        if (NotFlag)
                            match = !match;
                    }
                    break;
                case BranchConditionType.Question: // can have 1 or 3 argument
                    {
                        string question = StringEscaper.Preprocess(s, Arg1);

                        bool autoTimeout = false;

                        if (Arg2 != null && Arg3 != null)
                            autoTimeout = true;

                        int timeout = 0;
                        bool defaultChoice = false;
                        if (autoTimeout)
                        {
                            string timeoutStr = StringEscaper.Preprocess(s, Arg2);
                            if (NumberHelper.ParseInt32(timeoutStr, out timeout) == false)
                                autoTimeout = false;

                            string defaultChoiceStr = StringEscaper.Preprocess(s, Arg3);
                            if (defaultChoiceStr.Equals("True", StringComparison.OrdinalIgnoreCase))
                                defaultChoice = true;
                            else if (defaultChoiceStr.Equals("False", StringComparison.OrdinalIgnoreCase))
                                defaultChoice = false;
                        }

                        if (autoTimeout)
                        {
                            MessageBoxResult result = MessageBoxResult.None; 
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                result = CustomMessageBox.Show(question, "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question, timeout);
                            });

                            if (result == MessageBoxResult.None)
                            {
                                match = defaultChoice;
                                if (defaultChoice)
                                    logMessage = "[Yes] was automatically chosen";
                                else
                                    logMessage = "[No] was automatically chosen";
                            }
                            if (result == MessageBoxResult.Yes)
                            {
                                match = true;
                                logMessage = "[Yes] was chosen";
                            }
                            else
                            {
                                match = false;
                                logMessage = "[No] was chosen";
                            }
                        }
                        else
                        {
                            MessageBoxResult result = MessageBox.Show(question, "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
                            if (result == MessageBoxResult.Yes)
                            {
                                match = true;
                                logMessage = "[Yes] was chosen";
                            }
                            else
                            {
                                match = false;
                                logMessage = "[No] was chosen";
                            }
                        }

                        if (NotFlag)
                            match = !match;
                    }
                    break;
                case BranchConditionType.ExistMacro:
                    {
                        string macroName = StringEscaper.Preprocess(s, Arg1);
                        match = s.Macro.MacroDict.ContainsKey(macroName);

                        if (match)
                            logMessage = $"Macro [{macroName}] exists";
                        else
                            logMessage = $"Macro [{macroName}] does not exists";

                        if (NotFlag)
                            match = !match;
                    }
                    break;
                case BranchConditionType.ExistVar:
                    {
                        string varName = Variables.TrimPercentMark(Arg1);
                        match = s.Variables.ContainsKey(varName);

                        if (match)
                            logMessage = $"Variable [{varName}] exists";
                        else
                            logMessage = $"Variable [{varName}] does not exists";

                        if (NotFlag)
                            match = !match;
                    }
                    break;
                default:
                    throw new InternalException($"Internal BranchCondition check error");
            }
            return match;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            switch (Type)
            {
                case BranchConditionType.Equal:
                case BranchConditionType.Smaller:
                case BranchConditionType.Bigger:
                case BranchConditionType.SmallerEqual:
                case BranchConditionType.BiggerEqual:
                    b.Append(Arg1);
                    b.Append(",");
                    b.Append(Type);
                    b.Append(",");
                    b.Append(Arg2);
                    break;
                case BranchConditionType.ExistFile:
                    b.Append("ExistFile,");
                    b.Append(Arg1);
                    break;
                case BranchConditionType.ExistDir:
                    b.Append("ExistDir,");
                    b.Append(Arg1);
                    break;
                case BranchConditionType.ExistSection:
                    b.Append("ExistSection,");
                    b.Append(Arg1);
                    b.Append(",");
                    b.Append(Arg2);
                    break;
                case BranchConditionType.ExistRegSection:
                    b.Append("ExistRegSection,");
                    b.Append(Arg1);
                    b.Append(",");
                    b.Append(Arg2);
                    break;
                case BranchConditionType.ExistRegKey:
                    b.Append("ExistRegKey,");
                    b.Append(Arg1);
                    b.Append(",");
                    b.Append(Arg2);
                    b.Append(",");
                    b.Append(Arg3);
                    break;
                case BranchConditionType.ExistVar:
                    b.Append("ExistVar,");
                    b.Append(Arg1);
                    break;
                case BranchConditionType.ExistMacro:
                    b.Append("ExistMacro,");
                    b.Append(Arg2);
                    break;
            }
            return b.ToString();
        }
    }
    #endregion

    #region CodeInfo 13 - Branch
    [Serializable]
    public class CodeInfo_RunExec : CodeInfo
    {
        public string PluginFile;
        public string SectionName;
        public List<string> Parameters;

        public CodeInfo_RunExec(string pluginFile, string sectionName, List<string> parameters)
        {
            PluginFile = pluginFile;
            SectionName = sectionName;
            Parameters = parameters;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(PluginFile);
            b.Append(",");
            b.Append(SectionName);
            foreach (string param in Parameters)
            {
                b.Append(",");
                b.Append(param);
            }
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_Loop : CodeInfo
    {
        public bool Break;
        public string PluginFile;
        public string SectionName;
        public string StartIdx;  //  Its type should be int, but set to string because of variable system
        public string EndIdx;   //  Its type should be int, but set to string because of variable system
        public List<string> Parameters;

        public CodeInfo_Loop(string pluginFile, string sectionName, string startIdx, string endIdx, List<string> parameters)
        {
            Break = false;
            PluginFile = pluginFile;
            SectionName = sectionName;
            Parameters = parameters;
            StartIdx = startIdx;
            EndIdx = endIdx;
        }

        public CodeInfo_Loop(bool _break)
        {
            Break = _break;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(PluginFile);
            b.Append(",");
            b.Append(SectionName);
            foreach (string param in Parameters)
            {
                b.Append(",");
                b.Append(param);
            }
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_If : CodeInfo
    {
        public BranchCondition Condition;
        public CodeCommand Embed;

        public bool LinkParsed;
        public List<CodeCommand> Link;

        public CodeInfo_If(BranchCondition cond, CodeCommand embed)
        {
            Condition = cond;
            Embed = embed;

            LinkParsed = false;
            Link = new List<CodeCommand>();
        }

        public CodeInfo_If(BranchCondition cond, List<CodeCommand> link)
        {
            Condition = cond;
            Embed = null;

            LinkParsed = true;
            Link = link;
        }

        public override string ToString()
        { // TODO
            StringBuilder b = new StringBuilder();
            b.Append(Condition);
            b.Append(",");
            b.Append(Embed);
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_Else : CodeInfo
    {
        public CodeCommand Embed;

        public bool LinkParsed;
        public List<CodeCommand> Link;

        public CodeInfo_Else(CodeCommand embed)
        {
            Embed = embed;

            LinkParsed = false;
            Link = new List<CodeCommand>();
        }

        public CodeInfo_Else(List<CodeCommand> link)
        {
            Embed = null;

            LinkParsed = true;
            Link = link;
        }

        public override string ToString()
        { // TODO
            StringBuilder b = new StringBuilder();
            b.Append(Embed);
            return b.ToString();
        }
    }
    #endregion

    #region CodeInfo 14 - Control
    [Serializable]
    public class CodeInfo_Set : CodeInfo
    {
        public string VarKey;
        public string VarValue;
        public bool Global;
        public bool Permanent;

        public CodeInfo_Set(string varName, string varValue, bool global, bool permanent)
        {
            VarKey = varName;
            VarValue = varValue;
            Global = global;
            Permanent = permanent;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append("%");
            b.Append(VarKey);
            b.Append("%,");
            b.Append(VarValue);
            if (Global)
                b.Append(",GLOBAL");
            if (Permanent)
                b.Append(",PERMANENT");

            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_AddVariables : CodeInfo
    {
        public string PluginFile;
        public string SectionName;
        public bool Global;

        public CodeInfo_AddVariables(string pluginFile, string sectionName, bool global)
        {
            PluginFile = pluginFile;
            SectionName = sectionName;
            Global = global;
        }
    }

    [Serializable]
    public class CodeInfo_GetParam : CodeInfo
    {
        public int Index;
        public string VarName;

        public CodeInfo_GetParam(int index, string varName)
        {
            Index = index;
            VarName = varName;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(Index);
            b.Append(",");
            b.Append(VarName);
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_PackParam : CodeInfo
    { // PackParam,<StartIndex>,<VarName>[,VarNum] -- Cannot figure out how it works
        public int StartIndex;
        public string DestVar;
        public string VarNum; // optional

        public CodeInfo_PackParam(int startIndex, string varName, string varNum)
        {
            StartIndex = startIndex;
            DestVar = varName;
            VarNum = varNum;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(StartIndex);
            b.Append(",");
            b.Append(DestVar);
            if (VarNum != null)
            {
                b.Append(",");
                b.Append(VarNum);
            }
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_Exit : CodeInfo
    { // Exit,<Message>[,NOWARN]
        public string Message;
        public bool NoWarn;

        public CodeInfo_Exit(string message, bool noWarn)
        {
            Message = message;
            NoWarn = noWarn;
        }
    }

    [Serializable]
    public class CodeInfo_Halt : CodeInfo
    { // Halt,<Message>[,NOWARN]
        public string Message;

        public CodeInfo_Halt(string message)
        {
            Message = message;
        }
    }

    [Serializable]
    public class CodeInfo_Wait : CodeInfo
    { // Wait,<Second>
        public string Second;

        public CodeInfo_Wait(string second)
        {
            Second = second;
        }
    }

    [Serializable]
    public enum BeepType { OK = 0, Error, Asterisk, Confirmation }

    [Serializable]
    public class CodeInfo_Beep : CodeInfo
    { // Beep,<Type>
        public BeepType Type;

        public CodeInfo_Beep(BeepType type)
        {
            Type = type;
        }
    }
    #endregion

    #region CodeInfo 15 - Macro
    [Serializable]
    public class CodeInfo_Macro : CodeInfo
    {
        public string MacroType;
        public List<string> Args;

        public CodeInfo_Macro(string macroType, List<string> args)
        {
            MacroType = macroType;
            Args = args;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(MacroType);
            b.Append(",");
            for (int i = 0; i < Args.Count; i++)
            {
                b.Append(Args[i]);
                if (i + 1 < Args.Count)
                    b.Append(",");
            }
            return b.ToString();
        }
    }
    #endregion
}
