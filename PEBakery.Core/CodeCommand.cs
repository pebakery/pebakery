/*
    Copyright (C) 2016-2019 Hajin Jang
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

using Microsoft.Win32;
using PEBakery.Helper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

// ReSharper disable InconsistentNaming

namespace PEBakery.Core
{
    #region CodeType
    public enum CodeType
    {
        // 00 Misc
        None = 0, Error, Comment,
        // 01 File
        FileCopy = 100, FileDelete, FileRename, FileMove, FileCreateBlank, FileSize, FileVersion,
        DirCopy = 120, DirDelete, DirMove, DirMake, DirSize,
        PathMove = 160,
        // 02 Registry
        RegHiveLoad = 200, RegHiveUnload, RegRead, RegWrite, RegDelete, RegMulti, RegImport, RegExport, RegCopy,
        RegWriteLegacy = 260,
        // 03 Text
        TXTAddLine = 300, TXTDelLine, TXTReplace, TXTDelSpaces, TXTDelEmptyLines,
        TXTAddLineOp = 380, TXTReplaceOp, TXTDelLineOp,
        // 04 Ini
        IniWrite = 400, IniRead, IniDelete, IniReadSection, IniAddSection, IniDeleteSection, IniWriteTextLine, IniMerge,
        IniWriteOp = 480, IniReadOp, IniDeleteOp, IniReadSectionOp, IniAddSectionOp, IniDeleteSectionOp, IniWriteTextLineOp,
        // 05 Wim
        WimMount = 500, WimUnmount,
        WimInfo, WimApply, WimExtract, WimExtractBulk, WimCapture, WimAppend, WimDelete,
        WimPathAdd, WimPathDelete, WimPathRename, WimOptimize, WimExport,
        WimExtractOp = 580, WimPathOp,
        // 06 Archive
        Compress = 600, Decompress, Expand, CopyOrExpand,
        // 07 Network
        WebGet = 700,
        WebGetIfNotExist = 799, // Will be deprecated
        // 08 Hash
        Hash = 800,
        // 09 Script
        ExtractFile = 900, ExtractAndRun, ExtractAllFiles, Encode,
        // 10 Interface
        Visible = 1000, ReadInterface, WriteInterface, Message, Echo, EchoFile, UserInput, AddInterface,
        VisibleOp = 1080, ReadInterfaceOp, WriteInterfaceOp,
        Retrieve = 1099, // Will be deprecated in favor of [UserInput | FileSize | FileVersion | DirSize | Hash]
        // 20 String
        StrFormat = 2000,
        // 21 Math
        Math = 2100,
        // 22 List
        List = 2200,
        // 80 Branch
        Run = 8000, RunEx, Exec, Loop, LoopEx, LoopLetter, LoopLetterEx, If, Else, Begin, End,
        // 81 Control
        Set = 8100, SetMacro, AddVariables, Exit, Halt, Wait, Beep,
        GetParam = 8198, PackParam = 8199, // Will be deprecated
        // 82 System
        System = 8200, ShellExecute, ShellExecuteEx, ShellExecuteDelete,
        // 98 Debug
        Debug = 9800,
        // 99 External Macro
        Macro = 9900,
    }
    #endregion

    #region CodeCommand
    [Serializable]
    public class CodeCommand
    {
        public string RawCode;
        public ScriptSection Section;
        public CodeType Type;
        public CodeInfo Info;
        public int LineIdx;

        public CodeCommand(string rawCode, CodeType type, CodeInfo info, int lineIdx)
        {
            RawCode = rawCode;
            Type = type;
            Info = info;
            LineIdx = lineIdx;
        }

        public CodeCommand(string rawCode, ScriptSection section, CodeType type, CodeInfo info, int lineIdx)
        {
            RawCode = rawCode;
            Section = section;
            Type = type;
            Info = info;
            LineIdx = lineIdx;
        }

        public override string ToString()
        {
            return RawCode;
        }

        public static readonly CodeType[] DeprecatedCodeType =
        {
            CodeType.WebGetIfNotExist, // Better to have as Macro
            CodeType.GetParam,
            CodeType.PackParam,
        };

        public static readonly CodeType[] OptimizedCodeType =
        {
            CodeType.TXTAddLineOp,
            CodeType.TXTDelLineOp,
            CodeType.IniReadOp,
            CodeType.IniWriteOp,
            CodeType.IniAddSectionOp,
            CodeType.IniDeleteSectionOp,
            CodeType.IniWriteTextLineOp,
            CodeType.VisibleOp,
            CodeType.ReadInterfaceOp,
            CodeType.WriteInterfaceOp,
            CodeType.WimExtractOp,
            CodeType.WimPathOp,
        };
    }
    #endregion

    #region CodeInfo
    [Serializable]
    public class CodeInfo
    {
        #region Cast
        /// <summary>
        /// Type safe casting helper
        /// </summary>
        /// <typeparam name="T">Child of CodeInfo</typeparam>
        /// <returns>CodeInfo casted as T</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Cast<T>() where T : CodeInfo
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
        public static T Cast<T>(CodeInfo info) where T : CodeInfo
        {
            return info.Cast<T>();
        }
        #endregion

        #region Optimize
        public bool OptimizeCompare(CodeInfo info) => false;
        #endregion
    }
    #endregion

    #region CodeInfo 00 - Misc
    [Serializable]
    public class CodeInfo_Error : CodeInfo
    {
        public string ErrorMessage;

        public CodeInfo_Error(string errorMessage)
        {
            ErrorMessage = errorMessage;
        }

        public CodeInfo_Error(Exception e)
        {
            ErrorMessage = Logger.LogExceptionMessage(e);
        }

        public override string ToString()
        {
            return ErrorMessage;
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
    { // FileDelete,<FilePath>,[NOWARN],[NOREC]
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
    { // FileCreateBlank,<FilePath>,[Encoding],[PRESERVE],[NOWARN]
        public string FilePath;
        public bool Preserve;
        public bool NoWarn;
        public Encoding Encoding; // Optional, [UTF8|UTF16|UTF16BE|ANSI]

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
                if (Encoding.Equals(Encoding.UTF8))
                    b.Append(",UTF8");
                else if (Encoding.Equals(Encoding.Unicode))
                    b.Append(",UTF16");
                else if (Encoding.Equals(Encoding.BigEndianUnicode))
                    b.Append(",UTF16BE");
                else if (Encoding.Equals(EncodingHelper.DefaultAnsi))
                    b.Append(",ANSI");
                else
                    throw new InternalException("Internal Logic Error at CodeInfo_FileCreateBlank");
            }
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_FileSize : CodeInfo
    { // FileSize,<FilePath>,<%DestVar%>
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
    { // FileVersion,<FilePath>,<%DestVar%>
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
        public string DestDir;

        public CodeInfo_DirCopy(string srcDir, string destPath)
        {
            SrcDir = srcDir;
            DestDir = destPath;
        }

        public override string ToString()
        {
            return $"{SrcDir},{DestDir}";
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
    { // DirSize,<DirPath>,<%DestVar%>
        public string DirPath;
        public string DestVar;

        public CodeInfo_DirSize(string dirPath, string destVar)
        {
            DirPath = dirPath;
            DestVar = destVar;
        }

        public override string ToString()
        {
            return $"{DirPath},{DestVar}";
        }
    }

    [Serializable]
    public class CodeInfo_PathMove : CodeInfo
    { // PathMove,<SrcPath>,<DestPath>
        public string SrcPath;
        public string DestPath;

        public CodeInfo_PathMove(string srcPath, string destPath)
        {
            SrcPath = srcPath;
            DestPath = destPath;
        }

        public override string ToString()
        {
            return $"{SrcPath},{DestPath}";
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
    public class CodeInfo_RegRead : CodeInfo
    { // RegRead,<HKey>,<KeyPath>,<ValueName>,<%DestVar%>
        [NonSerialized]
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
    { // RegWrite,<HKey>,<ValueType>,<KeyPath>,<ValueName>,<ValueData | ValueDataList>,[NOWARN]
        [NonSerialized]
        public RegistryKey HKey;
        public RegistryValueKind ValueType;
        public string KeyPath;
        public string ValueName;
        public string ValueData;
        public string[] ValueDataList;
        public bool NoWarn;

        public CodeInfo_RegWrite(RegistryKey hKey, RegistryValueKind valueType, string keyPath, string valueName, string valueData, string[] valueDataList, bool noWarn)
        {
            HKey = hKey;
            ValueType = valueType;
            KeyPath = keyPath;
            ValueName = valueName;
            ValueData = valueData;
            ValueDataList = valueDataList;
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
            if (ValueDataList == null)
            {
                b.Append(ValueName);
                b.Append(",");
            }
            else
            {
                for (int i = 0; i < ValueDataList.Length; i++)
                {
                    b.Append(ValueDataList[i]);
                    if (i + 1 < ValueDataList.Length)
                        b.Append(",");
                }
            }
            if (NoWarn)
                b.Append(",NOWARN");
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_RegWriteLegacy : CodeInfo // Compatibility Shim for Win10PESE
    { // RegWrite,<HKey>,<ValueType>,<KeyPath>,<ValueName>,<ValueData | ValueDataList>
        public string HKey;
        public string ValueType;
        public string KeyPath;
        public string ValueName;
        public string[] ValueDataList;
        public bool NoWarn;

        public CodeInfo_RegWriteLegacy(string hKey, string valueType, string keyPath, string valueName, string[] valueDataList, bool noWarn)
        {
            HKey = hKey;
            ValueType = valueType;
            KeyPath = keyPath;
            ValueName = valueName;
            ValueDataList = valueDataList;
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
            foreach (string valueData in ValueDataList)
            {
                b.Append(",");
                b.Append(valueData);
            }
            if (NoWarn)
                b.Append(",NOWARN");
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_RegDelete : CodeInfo
    { // RegDelete,<HKey>,<KeyPath>,[ValueName]
        [NonSerialized]
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
        [NonSerialized]
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
        [NonSerialized]
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
    public class CodeInfo_RegCopy : CodeInfo
    { // RegCopy,<SrcKey>,<SrcKeyPath>,<DestKey>,<DestKeyPath>,[WILDCARD]
        [NonSerialized]
        public RegistryKey HSrcKey;
        public string SrcKeyPath;
        public RegistryKey HDestKey;
        public string DestKeyPath;
        public bool WildcardFlag;

        public CodeInfo_RegCopy(RegistryKey hSrcKey, string srcKeyPath, RegistryKey hDestKey, string destKeyPath, bool wildcard)
        {
            HSrcKey = hSrcKey;
            SrcKeyPath = srcKeyPath;
            HDestKey = hDestKey;
            DestKeyPath = destKeyPath;
            WildcardFlag = wildcard;
        }

        public override string ToString()
        {
            string HKeySrcStr = RegistryHelper.RegKeyToString(HSrcKey);
            string HKeyDestStr = RegistryHelper.RegKeyToString(HDestKey);
            return $"{HKeySrcStr},{SrcKeyPath},{HKeyDestStr},{DestKeyPath}{(WildcardFlag ? ",WILDCARD" : string.Empty)}";
        }
    }
    #endregion

    #region CodeInfo 03 - Text
    public enum TXTAddLineMode { Append, Prepend };
    [Serializable]
    public class CodeInfo_TXTAddLine : CodeInfo
    { // TXTAddLine,<FileName>,<Line>,<Mode>
        public string FileName;
        public string Line;
        public string Mode;

        public CodeInfo_TXTAddLine(string fileName, string line, string mode)
        {
            FileName = fileName;
            Line = line;
            Mode = mode;
        }

        public new bool OptimizeCompare(CodeInfo cmpInfo)
        {
            CodeInfo_TXTAddLine info = cmpInfo.Cast<CodeInfo_TXTAddLine>();
            if (info == null)
                return false;

            return FileName.Equals(info.FileName, StringComparison.OrdinalIgnoreCase) &&
                   Mode.Equals(info.Mode, StringComparison.OrdinalIgnoreCase);
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(FileName);
            b.Append(",");
            b.Append(Line);
            b.Append(",");
            b.Append(Mode);
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_TXTAddLineOp : CodeInfo
    {
        public List<CodeCommand> Cmds;
        public List<CodeInfo_TXTAddLine> Infos => Cmds.Select(x => x.Info.Cast<CodeInfo_TXTAddLine>()).ToList();

        public CodeInfo_TXTAddLineOp(List<CodeCommand> cmds)
        {
            Cmds = new List<CodeCommand>(cmds);
        }
    }

    [Serializable]
    public class CodeInfo_TXTReplace : CodeInfo
    { // TXTReplace,<FileName>,<OldStr>,<NewStr>
        public string FileName;
        public string OldStr;
        public string NewStr;

        public CodeInfo_TXTReplace(string fileName, string oldStr, string newStr)
        {
            FileName = fileName;
            OldStr = oldStr;
            NewStr = newStr;
        }

        public new bool OptimizeCompare(CodeInfo cmpInfo)
        {
            CodeInfo_TXTReplace info = cmpInfo.Cast<CodeInfo_TXTReplace>();
            if (info == null)
                return false;

            return FileName.Equals(info.FileName, StringComparison.OrdinalIgnoreCase);
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(FileName);
            b.Append(",");
            b.Append(OldStr);
            b.Append(",");
            b.Append(NewStr);
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_TXTReplaceOp : CodeInfo
    { // TXTReplace,<FileName>,<OldStr>,<NewStr>
        public List<CodeCommand> Cmds;
        public List<CodeInfo_TXTReplace> Infos => Cmds.Select(x => x.Info.Cast<CodeInfo_TXTReplace>()).ToList();

        public CodeInfo_TXTReplaceOp(List<CodeCommand> cmds)
        {
            Cmds = new List<CodeCommand>(cmds);
        }
    }

    [Serializable]
    public class CodeInfo_TXTDelLine : CodeInfo
    { // TXTDelLine,<FileName>,<DeleteLine>
        public string FileName;
        public string DeleteLine;

        public CodeInfo_TXTDelLine(string fileName, string deleteLine)
        {
            FileName = fileName;
            DeleteLine = deleteLine;
        }

        public new bool OptimizeCompare(CodeInfo cmpInfo)
        {
            CodeInfo_TXTDelLine info = cmpInfo.Cast<CodeInfo_TXTDelLine>();
            if (info == null)
                return false;

            return FileName.Equals(info.FileName, StringComparison.OrdinalIgnoreCase);
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(FileName);
            b.Append(",");
            b.Append(DeleteLine);
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_TXTDelLineOp : CodeInfo
    {
        public List<CodeCommand> Cmds;
        public List<CodeInfo_TXTDelLine> Infos => Cmds.Select(x => x.Info.Cast<CodeInfo_TXTDelLine>()).ToList();

        public CodeInfo_TXTDelLineOp(List<CodeCommand> cmds)
        {
            Cmds = new List<CodeCommand>(cmds);
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

    #region CodeInfo 04 - Ini
    [Serializable]
    public class CodeInfo_IniRead : CodeInfo
    { // IniRead,<FileName>,<Section>,<Key>,<%DestVar%>
        public string FileName;
        public string Section;
        public string Key;
        public string DestVar;

        public CodeInfo_IniRead(string fileName, string section, string key, string destVar)
        {
            FileName = fileName;
            Section = section;
            Key = key;
            DestVar = destVar;
        }

        public new bool OptimizeCompare(CodeInfo cmpInfo)
        {
            CodeInfo_IniRead info = cmpInfo.Cast<CodeInfo_IniRead>();
            if (info == null)
                return false;

            return FileName.Equals(info.FileName, StringComparison.OrdinalIgnoreCase);
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(FileName);
            b.Append(",");
            b.Append(Section);
            b.Append(",");
            b.Append(Key);
            b.Append(",%");
            b.Append(DestVar);
            b.Append("%");
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_IniReadOp : CodeInfo
    {
        public List<CodeCommand> Cmds;
        public List<CodeInfo_IniRead> Infos => Cmds.Select(x => x.Info.Cast<CodeInfo_IniRead>()).ToList();

        public CodeInfo_IniReadOp(List<CodeCommand> cmds)
        {
            Cmds = new List<CodeCommand>(cmds);
        }
    }

    [Serializable]
    public class CodeInfo_IniWrite : CodeInfo
    { // IniWrite,<FileName>,<Section>,<Key>,<Value>
        public string FileName;
        public string Section;
        public string Key;
        public string Value;

        public CodeInfo_IniWrite(string fileName, string section, string key, string value)
        {
            FileName = fileName;
            Section = section;
            Key = key;
            Value = value;
        }

        public new bool OptimizeCompare(CodeInfo cmpInfo)
        {
            CodeInfo_IniWrite info = cmpInfo.Cast<CodeInfo_IniWrite>();
            if (info == null)
                return false;

            return FileName.Equals(info.FileName, StringComparison.OrdinalIgnoreCase);
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(FileName);
            b.Append(",");
            b.Append(Section);
            b.Append(",");
            b.Append(Key);
            b.Append(",");
            b.Append(Value);
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_IniWriteOp : CodeInfo
    {
        public List<CodeCommand> Cmds;
        public List<CodeInfo_IniWrite> Infos => Cmds.Select(x => x.Info.Cast<CodeInfo_IniWrite>()).ToList();

        public CodeInfo_IniWriteOp(List<CodeCommand> cmds)
        {
            Cmds = new List<CodeCommand>(cmds);
        }
    }

    [Serializable]
    public class CodeInfo_IniDelete : CodeInfo
    { // IniDelete,<FileName>,<Section>,<Key>
        public string FileName;
        public string Section;
        public string Key;

        public CodeInfo_IniDelete(string fileName, string section, string key)
        {
            FileName = fileName;
            Section = section;
            Key = key;
        }

        public new bool OptimizeCompare(CodeInfo cmpInfo)
        {
            CodeInfo_IniDelete info = cmpInfo.Cast<CodeInfo_IniDelete>();
            if (info == null)
                return false;

            return FileName.Equals(info.FileName, StringComparison.OrdinalIgnoreCase);
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(FileName);
            b.Append(",");
            b.Append(Section);
            b.Append(",");
            b.Append(Key);
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_IniDeleteOp : CodeInfo
    {
        public List<CodeCommand> Cmds;
        public List<CodeInfo_IniDelete> Infos => Cmds.Select(x => x.Info.Cast<CodeInfo_IniDelete>()).ToList();

        public CodeInfo_IniDeleteOp(List<CodeCommand> cmds)
        {
            Cmds = new List<CodeCommand>(cmds);
        }
    }

    [Serializable]
    public class CodeInfo_IniReadSection : CodeInfo
    { // IniReadSection,<FileName>,<Section>,<%DestVar%>,[Delim=<Str>]
        public string FileName;
        public string Section;
        public string DestVar;
        public string Delim;

        public CodeInfo_IniReadSection(string fileName, string section, string destVar, string delim)
        {
            FileName = fileName;
            Section = section;
            DestVar = destVar;
            Delim = delim;
        }

        public new bool OptimizeCompare(CodeInfo cmpInfo)
        {
            CodeInfo_IniReadSection info = cmpInfo.Cast<CodeInfo_IniReadSection>();
            if (info == null)
                return false;

            return FileName.Equals(info.FileName, StringComparison.OrdinalIgnoreCase);
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(FileName);
            b.Append(",");
            b.Append(Section);
            b.Append(",");
            b.Append(DestVar);
            if (Delim != null)
            {
                b.Append(",");
                b.Append(Delim);
            }
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_IniReadSectionOp : CodeInfo
    {
        public List<CodeCommand> Cmds;
        public List<CodeInfo_IniReadSection> Infos => Cmds.Select(x => x.Info.Cast<CodeInfo_IniReadSection>()).ToList();

        public CodeInfo_IniReadSectionOp(List<CodeCommand> cmds)
        {
            Cmds = new List<CodeCommand>(cmds);
        }
    }

    [Serializable]
    public class CodeInfo_IniAddSection : CodeInfo
    { // IniAddSection,<FileName>,<Section>
        public string FileName;
        public string Section;

        public CodeInfo_IniAddSection(string fileName, string section)
        {
            FileName = fileName;
            Section = section;
        }

        public new bool OptimizeCompare(CodeInfo cmpInfo)
        {
            CodeInfo_IniAddSection info = cmpInfo.Cast<CodeInfo_IniAddSection>();
            if (info == null)
                return false;

            return FileName.Equals(info.FileName, StringComparison.OrdinalIgnoreCase);
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(FileName);
            b.Append(",");
            b.Append(Section);
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_IniAddSectionOp : CodeInfo
    {
        public List<CodeCommand> Cmds;
        public List<CodeInfo_IniAddSection> Infos => Cmds.Select(x => x.Info.Cast<CodeInfo_IniAddSection>()).ToList();

        public CodeInfo_IniAddSectionOp(List<CodeCommand> cmds)
        {
            Cmds = new List<CodeCommand>(cmds);
        }
    }

    [Serializable]
    public class CodeInfo_IniDeleteSection : CodeInfo
    { // IniDeleteSection,<FileName>,<Section>
        public string FileName;
        public string Section;

        public CodeInfo_IniDeleteSection(string fileName, string section)
        {
            FileName = fileName;
            Section = section;
        }

        public new bool OptimizeCompare(CodeInfo cmpInfo)
        {
            CodeInfo_IniDeleteSection info = cmpInfo.Cast<CodeInfo_IniDeleteSection>();
            if (info == null)
                return false;

            return FileName.Equals(info.FileName, StringComparison.OrdinalIgnoreCase);
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(FileName);
            b.Append(",");
            b.Append(Section);
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_IniDeleteSectionOp : CodeInfo
    {
        public List<CodeCommand> Cmds;
        public List<CodeInfo_IniDeleteSection> Infos => Cmds.Select(x => x.Info.Cast<CodeInfo_IniDeleteSection>()).ToList();

        public CodeInfo_IniDeleteSectionOp(List<CodeCommand> cmds)
        {
            Cmds = new List<CodeCommand>(cmds);
        }
    }

    [Serializable]
    public class CodeInfo_IniWriteTextLine : CodeInfo
    { // IniWriteTextLine,<FileName>,<Section>,<Line>,[APPEND] 
        public string FileName;
        public string Section;
        public string Line;
        public bool Append;

        public CodeInfo_IniWriteTextLine(string fileName, string section, string line, bool append)
        {
            FileName = fileName;
            Section = section;
            Line = line;
            Append = append;
        }

        public new bool OptimizeCompare(CodeInfo cmpInfo)
        {
            CodeInfo_IniWriteTextLine info = cmpInfo.Cast<CodeInfo_IniWriteTextLine>();
            if (info == null)
                return false;

            return FileName.Equals(info.FileName, StringComparison.OrdinalIgnoreCase) &&
                   Append == info.Append;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(FileName);
            b.Append(",");
            b.Append(Section);
            b.Append(",");
            b.Append(Line);
            if (Append)
                b.Append(",APPEND");
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_IniWriteTextLineOp : CodeInfo
    {
        public List<CodeCommand> Cmds;
        public List<CodeInfo_IniWriteTextLine> Infos => Cmds.Select(x => x.Info.Cast<CodeInfo_IniWriteTextLine>()).ToList();

        public CodeInfo_IniWriteTextLineOp(List<CodeCommand> cmds)
        {
            Cmds = new List<CodeCommand>(cmds);
        }
    }

    [Serializable]
    public class CodeInfo_IniMerge : CodeInfo
    { // IniMerge,<SrcFile>,<DestFile>
        public string SrcFile;
        public string DestFile;

        public CodeInfo_IniMerge(string srcFile, string destFile)
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

    #region CodeInfo 05 - Wim
    [Serializable]
    public class CodeInfo_WimMount : CodeInfo
    { // WimMount,<SrcWim>,<ImageIndex>,<MountDir>,<MountOption>
        public string SrcWim;
        public string ImageIndex;
        public string MountDir;
        public string MountOption; // <READONLY|READWRITE>

        public CodeInfo_WimMount(string srcWim, string imageIndex, string mountDir, string mountOption)
        {
            SrcWim = srcWim;
            ImageIndex = imageIndex;
            MountDir = mountDir;
            MountOption = mountOption;
        }

        public override string ToString()
        {
            return $"{SrcWim},{ImageIndex},{MountDir},{MountOption}";
        }
    }

    [Serializable]
    public class CodeInfo_WimUnmount : CodeInfo
    { // WimUnmount,<MountDir>,<UnmountOption>
        public string MountDir;
        public string UnmountOption; // <DISCARD|COMMIT>

        public CodeInfo_WimUnmount(string mountDir, string unmountOption)
        {
            MountDir = mountDir;
            UnmountOption = unmountOption;
        }

        public override string ToString()
        {
            return $"{MountDir},{UnmountOption}";
        }
    }

    [Serializable]
    public class CodeInfo_WimInfo : CodeInfo
    { // WimInfo,<SrcWim>,<ImageIndex>,<Key>,<%DestVar%>,[NOERR]
        public string SrcWim;
        public string ImageIndex;
        public string Key;
        public string DestVar;
        public bool NoErrFlag;

        public CodeInfo_WimInfo(string srcWim, string imageIndex, string key, string destVar, bool noErrFlag)
        {
            SrcWim = srcWim;
            ImageIndex = imageIndex;
            Key = key;
            DestVar = destVar;
            NoErrFlag = noErrFlag;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(SrcWim);
            b.Append(",");
            b.Append(ImageIndex);
            b.Append(",");
            b.Append(Key);
            b.Append(",");
            b.Append(DestVar);
            if (NoErrFlag)
                b.Append(",NOERR");
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_WimApply : CodeInfo
    { // WimApply,<SrcWim>,<ImageIndex>,<DestDir>,[Split=<Str>],[CHECK],[NOACL],[NOATTRIB]
        public string SrcWim;
        public string ImageIndex;
        public string DestDir;
        public string Split;
        public bool CheckFlag;
        public bool NoAclFlag;
        public bool NoAttribFlag;

        public CodeInfo_WimApply(string srcWim, string imageIndex, string destDir, string split, bool check, bool noAcl, bool noAttrib)
        {
            SrcWim = srcWim;
            ImageIndex = imageIndex;
            DestDir = destDir;
            Split = split;
            CheckFlag = check;
            NoAclFlag = noAcl;
            NoAttribFlag = noAttrib;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(SrcWim);
            b.Append(",");
            b.Append(ImageIndex);
            b.Append(",");
            b.Append(DestDir);
            if (Split != null)
            {
                b.Append(",");
                b.Append(Split);
            }
            if (CheckFlag)
                b.Append(",CHECK");
            if (NoAclFlag)
                b.Append(",NOACL");
            if (NoAttribFlag)
                b.Append(",NOATTRIB");
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_WimExtract : CodeInfo
    { // WimExtract,<SrcWim>,<ImageIndex>,<ExtractPath>,<DestDir>,[Split=<Str>],[CHECK],[NOACL],[NOATTRIB]
        // For extracting multiple path at once, rely on WimExtractOp or WimExtractBulk
        public string SrcWim;
        public string ImageIndex;
        public string ExtractPath;
        public string DestDir;
        public string Split; // Nullable
        public bool CheckFlag;
        public bool NoAclFlag;
        public bool NoAttribFlag;

        public CodeInfo_WimExtract(string srcWim, string imageIndex, string extractPath, string destDir, string split, bool check, bool noAcl, bool noAttrib)
        {
            SrcWim = srcWim;
            ImageIndex = imageIndex;
            ExtractPath = extractPath;
            DestDir = destDir;
            Split = split;
            CheckFlag = check;
            NoAclFlag = noAcl;
            NoAttribFlag = noAttrib;
        }

        public new bool OptimizeCompare(CodeInfo cmpInfo)
        { // Condition : Every argument except DestDir should be identical
            CodeInfo_WimExtract info = cmpInfo.Cast<CodeInfo_WimExtract>();
            if (info != null)
            {
                if (SrcWim.Equals(info.SrcWim, StringComparison.OrdinalIgnoreCase) &&
                    ImageIndex.Equals(info.ImageIndex, StringComparison.OrdinalIgnoreCase) &&
                    DestDir.Equals(info.DestDir, StringComparison.OrdinalIgnoreCase) &&
                    CheckFlag == info.CheckFlag &&
                    NoAclFlag == info.NoAclFlag &&
                    NoAttribFlag == info.NoAttribFlag)
                {
                    if (Split == null && info.Split == null)
                        return true;
                    if (Split != null && info.Split != null &&
                        Split.Equals(info.Split, StringComparison.OrdinalIgnoreCase))
                        return true;
                    return false;
                }
            }

            return false;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(SrcWim);
            b.Append(",");
            b.Append(ImageIndex);
            b.Append(",");
            b.Append(ExtractPath);
            b.Append(",");
            b.Append(DestDir);
            b.Append(",");
            b.Append(DestDir);
            if (Split != null)
            {
                b.Append(",");
                b.Append(Split);
            }
            if (CheckFlag)
                b.Append(",CHECK");
            if (NoAclFlag)
                b.Append(",NOACL");
            if (NoAttribFlag)
                b.Append(",NOATTRIB");
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_WimExtractOp : CodeInfo
    {
        public List<CodeCommand> Cmds;
        public List<CodeInfo_WimExtract> Infos => Cmds.Select(x => x.Info as CodeInfo_WimExtract).ToList();

        public CodeInfo_WimExtractOp(List<CodeCommand> cmds)
        {
            Cmds = new List<CodeCommand>(cmds);
        }
    }

    [Serializable]
    public class CodeInfo_WimExtractBulk : CodeInfo
    { // WimExtractBulk,<SrcWim>,<ImageIndex>,<ListFile>,<DestDir>,[Split=<Str>],[CHECK],[NOACL],[NOATTRIB],[NOERR],[NOWARN]
        public string SrcWim;
        public string ImageIndex;
        public string ListFile;
        public string DestDir;
        public string Split;
        public bool CheckFlag;
        public bool NoAclFlag;
        public bool NoAttribFlag;
        public bool NoErrFlag;
        public bool NoWarnFlag;

        public CodeInfo_WimExtractBulk(
            string srcWim, string imageIndex, string listFile, string destDir, string split,
            bool check, bool noAcl, bool noAttrib, bool noErr, bool noWarn)
        {
            SrcWim = srcWim;
            ImageIndex = imageIndex;
            ListFile = listFile;
            DestDir = destDir;
            Split = split;
            CheckFlag = check;
            NoAclFlag = noAcl;
            NoAttribFlag = noAttrib;
            NoErrFlag = noErr;
            NoWarnFlag = noWarn;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(SrcWim);
            b.Append(",");
            b.Append(ImageIndex);
            b.Append(",");
            b.Append(ListFile);
            b.Append(",");
            b.Append(DestDir);
            b.Append(",");
            b.Append(DestDir);
            if (Split != null)
            {
                b.Append(",");
                b.Append(Split);
            }
            if (CheckFlag)
                b.Append(",CHECK");
            if (NoAclFlag)
                b.Append(",NOACL");
            if (NoAttribFlag)
                b.Append(",NOATTRIB");
            if (NoErrFlag)
                b.Append(",NOERR");
            if (NoWarnFlag)
                b.Append(",NOWARN");
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_WimCapture : CodeInfo
    { // WimCapture,<SrcDir>,<DestWim>,<Compress>,[ImageName=<Str>],[ImageDesc=<Str>],[Flags=<Str>],[BOOT],[CHECK],[NOACL]
        public string SrcDir;
        public string DestWim;
        public string Compress; // [NONE|XPRESS|LZX|LZMS]
        public string ImageName; // Optional
        public string ImageDesc; // Optional
        public string WimFlags; // Optional
        public bool BootFlag; // Optional Flag
        public bool CheckFlag; // Optional Flag
        public bool NoAclFlag; // Optional Flag

        public CodeInfo_WimCapture(string srcDir, string destWim, string compress,
            string imageName, string imageDesc, string wimFlags,
            bool boot, bool check, bool noAcl)
        {
            SrcDir = srcDir;
            DestWim = destWim;
            Compress = compress;

            // Optional argument
            ImageName = imageName;
            ImageDesc = imageDesc;
            WimFlags = wimFlags;

            // Flags
            BootFlag = boot;
            CheckFlag = check;
            NoAclFlag = noAcl;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(SrcDir);
            b.Append(",");
            b.Append(DestWim);
            b.Append(",");
            b.Append(Compress);

            if (ImageName != null)
            {
                b.Append("ImageName=");
                b.Append(ImageName);
            }
            if (ImageDesc != null)
            {
                b.Append("ImageDesc=");
                b.Append(ImageDesc);
            }
            if (WimFlags != null)
            {
                b.Append("WimFlags=");
                b.Append(WimFlags);
            }

            if (BootFlag)
                b.Append(",BOOT");
            if (CheckFlag)
                b.Append(",CHECK");
            if (NoAclFlag)
                b.Append(",NOACL");
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_WimAppend : CodeInfo
    { // WimAppend,<SrcDir>,<DestWim>,[ImageName=STR],[ImageDesc=STR],[Flags=STR],[DeltaIndex=INT],[BOOT],[CHECK],[NOACL]
        public string SrcDir;
        public string DestWim;
        public string ImageName; // Optional
        public string ImageDesc; // Optional
        public string WimFlags; // Optional
        public string DeltaIndex; // Optional, for Delta Wim (like install.wim)
        public bool BootFlag; // Optional Flag
        public bool CheckFlag; // Optional Flag
        public bool NoAclFlag; // Optional Flag

        public CodeInfo_WimAppend(string srcDir, string destWim,
            string imageName, string imageDesc, string wimFlags, string deltaIndex,
            bool boot, bool check, bool noAcl)
        {
            SrcDir = srcDir;
            DestWim = destWim;

            // Optional argument
            ImageName = imageName;
            ImageDesc = imageDesc;
            WimFlags = wimFlags;
            DeltaIndex = deltaIndex;

            // Flags
            BootFlag = boot;
            CheckFlag = check;
            NoAclFlag = noAcl;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(SrcDir);
            b.Append(",");
            b.Append(DestWim);

            if (ImageName != null)
            {
                b.Append("ImageName=");
                b.Append(ImageName);
            }
            if (ImageDesc != null)
            {
                b.Append("ImageDesc=");
                b.Append(ImageDesc);
            }
            if (WimFlags != null)
            {
                b.Append("WimFlags=");
                b.Append(WimFlags);
            }
            if (DeltaIndex != null)
            {
                b.Append("DeltaIndex=");
                b.Append(DeltaIndex);
            }

            if (BootFlag)
                b.Append(",BOOT");
            if (CheckFlag)
                b.Append(",CHECK");
            if (NoAclFlag)
                b.Append(",NOACL");
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_WimDelete : CodeInfo
    { // WimDelete,<SrcWim>,<ImageIndex>,[CHECK]
        public string SrcWim;
        public string ImageIndex;
        public bool CheckFlag; // Optional Flag

        public CodeInfo_WimDelete(string srcWim, string imageIndex, bool check)
        {
            SrcWim = srcWim;
            ImageIndex = imageIndex;

            // Flags
            CheckFlag = check;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(SrcWim);
            b.Append(",");
            b.Append(ImageIndex);
            if (CheckFlag)
                b.Append(",CHECK");
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_WimPathAdd : CodeInfo
    { // WimPathAdd,<WimFile>,<ImageIndex>,<SrcPath>,<DestPath>,[CHECK],[NOACL],[PRESERVE],[REBUILD]
        // Note : If <SrcPath> is a file, <DestPath> must be a file. If <SrcPath> is a dir, <DestPath> must be a dir.
        //        It is different from standard PEBakery dest path convention, because it follows wimlib-imagex update convention.
        public string WimFile;
        public string ImageIndex;
        public string SrcPath;
        public string DestPath;
        public bool CheckFlag;
        public bool NoAclFlag;
        public bool PreserveFlag;
        public bool RebuildFlag;

        public CodeInfo_WimPathAdd(string wimFile, string imageIndex,
            string srcPath, string destPath,
            bool checkFlag, bool noAclFlag, bool preserveFlag, bool rebuildFlag)
        {
            // WimPath (WimUpdate) Series Common
            WimFile = wimFile;
            ImageIndex = imageIndex;
            // Common Optional Flags
            CheckFlag = checkFlag;
            RebuildFlag = rebuildFlag;

            // WimPathAdd Specific
            SrcPath = srcPath;
            DestPath = destPath;

            // Optional Flags
            NoAclFlag = noAclFlag;
            PreserveFlag = preserveFlag;
        }

        public new bool OptimizeCompare(CodeInfo cmpInfo)
        {
            if (cmpInfo.GetType() == typeof(CodeInfo_WimPathAdd))
            {
                CodeInfo_WimPathAdd info = cmpInfo.Cast<CodeInfo_WimPathAdd>();
                if (info != null)
                {
                    return WimFile.Equals(info.WimFile, StringComparison.OrdinalIgnoreCase) &&
                           ImageIndex.Equals(info.ImageIndex, StringComparison.OrdinalIgnoreCase) &&
                           CheckFlag == info.CheckFlag &&
                           RebuildFlag == info.RebuildFlag;
                }
            }
            else if (cmpInfo.GetType() == typeof(CodeInfo_WimPathRename))
            {
                CodeInfo_WimPathRename info = cmpInfo.Cast<CodeInfo_WimPathRename>();
                if (info != null)
                {
                    return WimFile.Equals(info.WimFile, StringComparison.OrdinalIgnoreCase) &&
                           ImageIndex.Equals(info.ImageIndex, StringComparison.OrdinalIgnoreCase) &&
                           CheckFlag == info.CheckFlag &&
                           RebuildFlag == info.RebuildFlag;
                }
            }
            else if (cmpInfo.GetType() == typeof(CodeInfo_WimPathDelete))
            {
                CodeInfo_WimPathDelete info = cmpInfo.Cast<CodeInfo_WimPathDelete>();
                if (info != null)
                {
                    return WimFile.Equals(info.WimFile, StringComparison.OrdinalIgnoreCase) &&
                           ImageIndex.Equals(info.ImageIndex, StringComparison.OrdinalIgnoreCase) &&
                           CheckFlag == info.CheckFlag &&
                           RebuildFlag == info.RebuildFlag;
                }
            }

            return false;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(WimFile);
            b.Append(",");
            b.Append(ImageIndex);
            b.Append(",");
            b.Append(SrcPath);
            b.Append(",");
            b.Append(DestPath);
            if (CheckFlag)
                b.Append(",CHECK");
            if (CheckFlag)
                b.Append(",NOACL");
            if (CheckFlag)
                b.Append(",PRESERVE");
            if (RebuildFlag)
                b.Append(",REBUILD");
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_WimPathDelete : CodeInfo
    { // WimPathDelete,<WimFile>,<ImageIndex>,<Path>,[CHECK],[REBUILD]
        public string WimFile;
        public string ImageIndex;
        public string Path;
        public bool CheckFlag;
        public bool RebuildFlag;

        public CodeInfo_WimPathDelete(string wimFile, string imageIndex, string path, bool checkFlag, bool rebuildFlag)
        {
            // WimPath (WimUpdate) Series Common
            WimFile = wimFile;
            ImageIndex = imageIndex;
            // Common Optional Flags
            CheckFlag = checkFlag;
            RebuildFlag = rebuildFlag;

            // WimPathDelete Specific
            Path = path;
        }

        public new bool OptimizeCompare(CodeInfo cmpInfo)
        {
            if (cmpInfo.GetType() == typeof(CodeInfo_WimPathAdd))
            {
                CodeInfo_WimPathAdd info = cmpInfo.Cast<CodeInfo_WimPathAdd>();
                if (info != null)
                {
                    return WimFile.Equals(info.WimFile, StringComparison.OrdinalIgnoreCase) &&
                           ImageIndex.Equals(info.ImageIndex, StringComparison.OrdinalIgnoreCase) &&
                           CheckFlag == info.CheckFlag &&
                           RebuildFlag == info.RebuildFlag;
                }
            }
            else if (cmpInfo.GetType() == typeof(CodeInfo_WimPathRename))
            {
                CodeInfo_WimPathRename info = cmpInfo.Cast<CodeInfo_WimPathRename>();
                if (info != null)
                {
                    return WimFile.Equals(info.WimFile, StringComparison.OrdinalIgnoreCase) &&
                           ImageIndex.Equals(info.ImageIndex, StringComparison.OrdinalIgnoreCase) &&
                           CheckFlag == info.CheckFlag &&
                           RebuildFlag == info.RebuildFlag;
                }
            }
            else if (cmpInfo.GetType() == typeof(CodeInfo_WimPathDelete))
            {
                CodeInfo_WimPathDelete info = cmpInfo.Cast<CodeInfo_WimPathDelete>();
                if (info != null)
                {
                    return WimFile.Equals(info.WimFile, StringComparison.OrdinalIgnoreCase) &&
                           ImageIndex.Equals(info.ImageIndex, StringComparison.OrdinalIgnoreCase) &&
                           CheckFlag == info.CheckFlag &&
                           RebuildFlag == info.RebuildFlag;
                }
            }

            return false;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(WimFile);
            b.Append(",");
            b.Append(ImageIndex);
            b.Append(",");
            b.Append(Path);
            if (CheckFlag)
                b.Append(",CHECK");
            if (RebuildFlag)
                b.Append(",REBUILD");
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_WimPathRename : CodeInfo
    { // WimPathRename,<WimFile>,<ImageIndex>,<SrcPath>,<DestPath>,[CHECK],[REBUILD]
        public string WimFile;
        public string ImageIndex;
        public string SrcPath;
        public string DestPath;
        public bool CheckFlag;
        public bool RebuildFlag;

        public CodeInfo_WimPathRename(string wimFile, string imageIndex, string srcPath, string destPath, bool checkFlag, bool rebuildFlag)
        {
            // WimPath (WimUpdate) Series Common
            WimFile = wimFile;
            ImageIndex = imageIndex;
            // Common Optional Flags
            CheckFlag = checkFlag;
            RebuildFlag = rebuildFlag;

            // WimPathDelete Specific
            SrcPath = srcPath;
            DestPath = destPath;
        }

        public new bool OptimizeCompare(CodeInfo cmpInfo)
        {
            if (cmpInfo.GetType() == typeof(CodeInfo_WimPathAdd))
            {
                CodeInfo_WimPathAdd info = cmpInfo.Cast<CodeInfo_WimPathAdd>();
                if (info != null)
                {
                    return WimFile.Equals(info.WimFile, StringComparison.OrdinalIgnoreCase) &&
                           ImageIndex.Equals(info.ImageIndex, StringComparison.OrdinalIgnoreCase) &&
                           CheckFlag == info.CheckFlag &&
                           RebuildFlag == info.RebuildFlag;
                }
            }
            else if (cmpInfo.GetType() == typeof(CodeInfo_WimPathRename))
            {
                CodeInfo_WimPathRename info = cmpInfo.Cast<CodeInfo_WimPathRename>();
                if (info != null)
                {
                    return WimFile.Equals(info.WimFile, StringComparison.OrdinalIgnoreCase) &&
                           ImageIndex.Equals(info.ImageIndex, StringComparison.OrdinalIgnoreCase) &&
                           CheckFlag == info.CheckFlag &&
                           RebuildFlag == info.RebuildFlag;
                }
            }
            else if (cmpInfo.GetType() == typeof(CodeInfo_WimPathDelete))
            {
                CodeInfo_WimPathDelete info = cmpInfo.Cast<CodeInfo_WimPathDelete>();
                if (info != null)
                {
                    return WimFile.Equals(info.WimFile, StringComparison.OrdinalIgnoreCase) &&
                           ImageIndex.Equals(info.ImageIndex, StringComparison.OrdinalIgnoreCase) &&
                           CheckFlag == info.CheckFlag &&
                           RebuildFlag == info.RebuildFlag;
                }
            }

            return false;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(WimFile);
            b.Append(",");
            b.Append(ImageIndex);
            b.Append(",");
            b.Append(SrcPath);
            b.Append(",");
            b.Append(DestPath);
            if (CheckFlag)
                b.Append(",CHECK");
            if (RebuildFlag)
                b.Append(",REBUILD");
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_WimPathOp : CodeInfo
    {
        public List<CodeCommand> Cmds;

        public CodeInfo_WimPathOp(List<CodeCommand> cmds)
        {
            Cmds = new List<CodeCommand>(cmds);
        }
    }

    [Serializable]
    public class CodeInfo_WimOptimize : CodeInfo
    { // WimOptimize,<WimFile>,[Recomp=<Str>],[CHECK|NOCHECK]
        public string WimFile;
        public string Recompress; // [KEEP|NONE|XPRESS|LZX|LZMS]
        public bool? CheckFlag; // Optional Flag

        public CodeInfo_WimOptimize(string wimFile, string recompress, bool? checkFlag)
        {
            WimFile = wimFile;
            // Optional Argument
            Recompress = recompress;
            // Flags
            CheckFlag = checkFlag;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(WimFile);
            if (Recompress != null)
            {
                b.Append(",");
                b.Append(Recompress);
            }
            if (CheckFlag != null)
                b.Append(CheckFlag == true ? ",CHECK" : ",NOCHECK");
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_WimExport : CodeInfo
    { // WimExport,<SrcWim>,<ImageIndex>,<DestWim>,[ImageName=<Str>],[ImageDesc=<Str>],[Split=<Str>],[Recomp=<Str>],[BOOT],[CHECK|NOCHECK]
        public string SrcWim;
        public string ImageIndex;
        public string DestWim;
        public string ImageName; // Optional
        public string ImageDesc; // Optional
        public string Recompress; // [KEEP|NONE|XPRESS|LZX|LZMS]
        public string Split;  // Optional
        public bool BootFlag; // Optional Flag
        public bool? CheckFlag; // Optional Flag

        public CodeInfo_WimExport(string srcWim, string imageIndex, string destWim,
            string imageName, string imageDesc, string split, string recompress,
            bool boot, bool? check)
        {
            SrcWim = srcWim;
            ImageIndex = imageIndex;
            DestWim = destWim;

            // Optional argument
            ImageName = imageName;
            ImageDesc = imageDesc;
            Split = split;
            Recompress = recompress;

            // Flags
            BootFlag = boot;
            CheckFlag = check;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(SrcWim);
            b.Append(",");
            b.Append(ImageIndex);
            b.Append(",");
            b.Append(DestWim);

            if (ImageName != null)
            {
                b.Append(",ImageName=");
                b.Append(ImageName);
            }
            if (ImageDesc != null)
            {
                b.Append(",ImageDesc=");
                b.Append(ImageDesc);
            }
            if (Split != null)
            {
                b.Append(",Split=");
                b.Append(Split);
            }
            if (Recompress != null)
            {
                b.Append(",Recomp=");
                b.Append(Recompress);
            }

            if (BootFlag)
                b.Append(",BOOT");
            if (CheckFlag != null)
                b.Append(CheckFlag == true ? ",CHECK" : ",NOCHECK");
            return b.ToString();
        }
    }
    #endregion

    #region CodeInfo 06 - Archive
    [Serializable]
    public class CodeInfo_Compress : CodeInfo
    { // Compress,<Format>,<SrcPath>,<DestArchive>[,Password=<Str>][,CompressLevel]
        public ArchiveFile.ArchiveCompressFormat Format;
        public string SrcPath;
        public string DestArchive;
        public ArchiveFile.CompressLevel? CompressLevel;

        public CodeInfo_Compress(
            ArchiveFile.ArchiveCompressFormat format, string srcDir, string destArchive, ArchiveFile.CompressLevel? compressLevel)
        {
            Format = format;
            SrcPath = srcDir;
            DestArchive = destArchive;
            CompressLevel = compressLevel;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            switch (Format)
            {
                case ArchiveFile.ArchiveCompressFormat.Zip:
                    b.Append("Zip");
                    break;
                case ArchiveFile.ArchiveCompressFormat.SevenZip:
                    b.Append("SevenZip");
                    break;
                default:
                    throw new InternalException($"Wrong ArchiveFormat [{Format}]");
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
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_Decompress : CodeInfo
    { // Decompress,<SrcArchive>,<DestDir>[,Password=<Str>]
        public string SrcArchive;
        public string DestDir;
        public string Password;

        public CodeInfo_Decompress(string srcArchive, string destArchive, string password)
        {
            SrcArchive = srcArchive;
            DestDir = destArchive;
            Password = password;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(SrcArchive);
            b.Append(",");
            b.Append(DestDir);
            if (Password != null)
            {
                b.Append(",");
                b.Append(Password);
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

    #region CodeInfo 07 - Network
    [Serializable]
    public class CodeInfo_WebGet : CodeInfo
    { // WebGet,<URL>,<DestPath>[<HashType>=<HashDigest>][,TimeOut=<Int>][,NOERR]
        // This command was rebuilt in PEBakery, not following WB082 spec.
        public string URL;
        public string DestPath;
        public HashHelper.HashType HashType; // Optional Argument
        public string HashDigest; // Optional Argument
        public string TimeOut; // Optional Argument
        public bool NoErrFlag; // Optional Flag

        public CodeInfo_WebGet(string url, string destPath, HashHelper.HashType hashType, string hashDigest, string timeOut, bool noErr)
        {
            URL = url;
            DestPath = destPath;
            HashType = hashType;
            HashDigest = hashDigest;
            TimeOut = timeOut;
            NoErrFlag = noErr;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(URL);
            b.Append(",");
            b.Append(DestPath);
            if (HashType != HashHelper.HashType.None && HashDigest != null)
            {
                b.Append(",");
                b.Append(HashType);
                b.Append(",");
                b.Append(HashDigest);
            }
            if (TimeOut != null)
            {
                b.Append(",");
                b.Append(TimeOut);
            }
            if (NoErrFlag)
                b.Append(",NOERR");
            return b.ToString();
        }
    }
    #endregion

    #region CodeInfo 08 - Hash
    [Serializable]
    public class CodeInfo_Hash : CodeInfo
    { // Hash,<HashType>,<FilePath>,<%DestVar%>
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

    #region CodeInfo 09 - Script
    [Serializable]
    public class CodeInfo_ExtractFile : CodeInfo
    { // ExtractFile,<ScriptFile>,<DirName>,<FileName>,<DestDir>
        public string ScriptFile;
        public string DirName;
        public string FileName;
        public string DestDir;

        public CodeInfo_ExtractFile(string scriptFile, string dirName, string fileName, string extractTo)
        {
            ScriptFile = scriptFile;
            DirName = dirName;
            FileName = fileName;
            DestDir = extractTo;
        }

        public override string ToString()
        {
            return $"{ScriptFile},{DirName},{FileName},{DestDir}";
        }
    }

    [Serializable]
    public class CodeInfo_ExtractAndRun : CodeInfo
    { // ExtractAndRun,<ScriptFile>,<DirName>,<FileName>,[Params]
        public string ScriptFile;
        public string DirName;
        public string FileName;
        public string Params;

        public CodeInfo_ExtractAndRun(string scriptFile, string dirName, string fileName, string parameters)
        {
            ScriptFile = scriptFile;
            DirName = dirName;
            FileName = fileName;
            Params = parameters;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(ScriptFile);
            b.Append(",");
            b.Append(DirName);
            b.Append(",");
            b.Append(FileName);
            if (Params != null)
            {
                b.Append(",");
                b.Append(Params);
            }
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_ExtractAllFiles : CodeInfo
    { // ExtractAllFiles,<ScriptFile>,<DirName>,<DestDir>
        public string ScriptFile;
        public string DirName;
        public string DestDir;

        public CodeInfo_ExtractAllFiles(string scriptFile, string dirName, string extractTo)
        {
            ScriptFile = scriptFile;
            DirName = dirName;
            DestDir = extractTo;
        }

        public override string ToString()
        {
            return $"{ScriptFile},{DirName},{DestDir}";
        }
    }

    [Serializable]
    public class CodeInfo_Encode : CodeInfo
    { // Encode,<ScriptFile>,<DirName>,<FilePath>,[Compression]
        public string ScriptFile;
        public string DirName;
        public string FilePath; // Can have Wildcard
        public string Compression;

        public CodeInfo_Encode(string scriptFile, string dirName, string filePath, string compression)
        {
            ScriptFile = scriptFile;
            DirName = dirName;
            FilePath = filePath;
            Compression = compression;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(ScriptFile);
            b.Append(',');
            b.Append(DirName);
            b.Append(',');
            b.Append(FilePath);
            if (Compression != null)
            {
                b.Append(',');
                b.Append(Compression);
            }
            return b.ToString();
        }
    }
    #endregion

    #region CodeInfo 10 - Interface
    [Serializable]
    public class CodeInfo_Visible : CodeInfo
    { // Visible,<%UIControlKey%>,<Visibility>
        public string UIControlKey; // Must start and end with %
        public string Visibility; // True or False

        public CodeInfo_Visible(string uiCtrlKey, string visibility)
        {
            UIControlKey = uiCtrlKey;
            Visibility = visibility;
        }

        public new bool OptimizeCompare(CodeInfo cmpInfo) => true;

        public override string ToString()
        {
            return $"{UIControlKey},{Visibility}";
        }
    }

    [Serializable]
    public class CodeInfo_VisibleOp : CodeInfo
    {
        public List<CodeCommand> Cmds;
        public List<CodeInfo_Visible> Infos => Cmds.Select(x => x.Info.Cast<CodeInfo_Visible>()).ToList();

        public CodeInfo_VisibleOp(List<CodeCommand> cmds)
        {
            Cmds = new List<CodeCommand>(cmds);
        }
    }

    public enum InterfaceElement
    {
        // General
        Text, Visible, PosX, PosY, Width, Height, Value, ToolTip,
        // TextLabel, Bevel 
        FontSize, FontWeight, FontStyle,
        // NumberBox
        NumberMin, NumberMax, NumberTick,
        // Image, WebLabel
        Url,
        // Items - ComboBox, RadioGroup
        Items,
        // Run - CheckBox, ComboBox, Button, RadioButton, RadioGroup
        SectionName, HideProgress
    }

    [Serializable]
    public class CodeInfo_ReadInterface : CodeInfo
    { // ReadInterface,<Element>,<ScriptFile>,<Section>,<Key>,<%DestVar%>,[Delim=<Str>]
        public InterfaceElement Element;
        public string ScriptFile;
        public string Section;
        public string Key;
        public string DestVar;
        public string Delim;

        public CodeInfo_ReadInterface(InterfaceElement element, string scriptFile, string section, string key, string destVar, string delim)
        {
            Element = element;
            ScriptFile = scriptFile;
            Section = section;
            Key = key;
            DestVar = destVar;
            Delim = delim;
        }

        public new bool OptimizeCompare(CodeInfo cmpInfo)
        {
            CodeInfo_ReadInterface info = cmpInfo.Cast<CodeInfo_ReadInterface>();
            if (info == null)
                return false;

            return ScriptFile.Equals(info.ScriptFile, StringComparison.OrdinalIgnoreCase) &&
                   Section.Equals(info.Section, StringComparison.OrdinalIgnoreCase);
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(Element);
            b.Append(",");
            b.Append(ScriptFile);
            b.Append(",");
            b.Append(Section);
            b.Append(",");
            b.Append(Key);
            b.Append(",");
            b.Append(DestVar);
            if (Delim != null)
            {
                b.Append("Delim=");
                b.Append(Delim);
            }
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_ReadInterfaceOp : CodeInfo
    {
        public List<CodeCommand> Cmds;
        public List<CodeInfo_ReadInterface> Infos => Cmds.Select(x => x.Info.Cast<CodeInfo_ReadInterface>()).ToList();

        public CodeInfo_ReadInterfaceOp(List<CodeCommand> cmds)
        {
            Cmds = new List<CodeCommand>(cmds);
        }
    }

    [Serializable]
    public class CodeInfo_WriteInterface : CodeInfo
    { // WriteInterface,<Element>,<ScriptFile>,<Section>,<Key>,<Value>,[Delim=<Str>]
        public InterfaceElement Element;
        public string ScriptFile;
        public string Section;
        public string Key;
        public string Value;
        public string Delim;

        public CodeInfo_WriteInterface(InterfaceElement element, string scriptFile, string section, string key, string value, string delim)
        {
            Element = element;
            ScriptFile = scriptFile;
            Section = section;
            Key = key;
            Value = value;
            Delim = delim;
        }

        public new bool OptimizeCompare(CodeInfo cmpInfo)
        {
            CodeInfo_WriteInterface info = cmpInfo.Cast<CodeInfo_WriteInterface>();
            if (info == null)
                return false;

            return ScriptFile.Equals(info.ScriptFile, StringComparison.OrdinalIgnoreCase) &&
                   Section.Equals(info.Section, StringComparison.OrdinalIgnoreCase);
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(Element);
            b.Append(",");
            b.Append(ScriptFile);
            b.Append(",");
            b.Append(Section);
            b.Append(",");
            b.Append(Key);
            b.Append(",");
            b.Append(Value);
            if (Delim != null)
            {
                b.Append("Delim=");
                b.Append(Delim);
            }
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_WriteInterfaceOp : CodeInfo
    {
        public List<CodeCommand> Cmds;
        public List<CodeInfo_WriteInterface> Infos => Cmds.Select(x => x.Info.Cast<CodeInfo_WriteInterface>()).ToList();

        public CodeInfo_WriteInterfaceOp(List<CodeCommand> cmds)
        {
            Cmds = new List<CodeCommand>(cmds);
        }
    }

    public enum CodeMessageAction
    {
        None, Information, Confirmation, Error, Warning
    }

    [Serializable]
    public class CodeInfo_Message : CodeInfo
    { // Message,<Message>,[Icon],[TIMEOUT]
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
    { // Echo,<Message>,[WARN]
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

    [Serializable]
    public class CodeInfo_EchoFile : CodeInfo
    { // EchoFile,<SrcFile>,[WARN]
        public string SrcFile;
        public bool Warn;

        public CodeInfo_EchoFile(string srcFile, bool warn)
        {
            SrcFile = srcFile;
            Warn = warn;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(SrcFile);
            if (Warn)
                b.Append(",WARN");
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_AddInterface : CodeInfo
    { // AddInterface,<ScriptFile>,<Interface>,<Prefix>
        public string ScriptFile;
        public string Section;
        public string Prefix;

        public CodeInfo_AddInterface(string scriptFile, string ifaceSection, string prefix)
        {
            ScriptFile = scriptFile;
            Section = ifaceSection;
            Prefix = prefix;
        }

        public override string ToString()
        {
            return $"{ScriptFile},{Section},{Prefix}";
        }
    }

    [Serializable]
    public class CodeInfo_UserInput : CodeInfo
    { // UserInput, ...
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

    #region UserInputType, UserInputInfo
    public enum UserInputType
    {
        DirPath,
        FilePath,
    }

    [Serializable]
    public class UserInputInfo : CodeInfo { }

    [Serializable]
    public class UserInputInfo_DirFile : UserInputInfo
    {
        // UserInput,File,<InitPath>,<%DestVar%>
        // UserInput,Dir,<InitPath>,<%DestVar%>
        public string InitPath;
        public string DestVar;

        public UserInputInfo_DirFile(string initPath, string destVar)
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
    #endregion

    #region StrFormatType, StrFormatInfo
    public enum StrFormatType
    {
        IntToBytes = 10, Bytes, // IntToBytes == Bytes
        BytesToInt,
        Hex = 20,
        Ceil = 30, Floor, Round, // Round added in PEBakery 
        Date = 40,
        FileName = 50, DirPath, Path, Ext, // DirPath == Path
        PathCombine = 60,
        Inc = 70, Dec, Mult, Div, // Use of Math Command is recommended
        Left = 80, Right,
        SubStr = 90, // Added in PEBakery
        Len = 100,
        LTrim = 110, RTrim, CTrim, NTrim,
        UCase = 120, LCase,
        Pos = 130, PosX,
        Replace = 140, ReplaceX,
        Split = 150,
        PadLeft = 160, PadRight,
        ShortPath = 800, LongPath, // Will be deprecated
    }

    [Serializable]
    public class StrFormatInfo : CodeInfo { }

    [Serializable]
    public class StrFormatInfo_IntToBytes : StrFormatInfo
    { // StrFormat,Bytes,<Integer>,<%DestVar%>
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
    { // StrFormat,BytesToInt,<Bytes>,<%DestVar%>
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
    { // StrFormat,Hex,<Integer>,<%DestVar%>
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
        // StrFormat,Ceil,<%SizeVar%>,<CeilTo>
        // StrFormat,Floor,<%SizeVar%>,<FloorTo>
        // StrFormat,Round,<%SizeVar%>,<RoundTo>
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
    { // StrFormat,Date,<%DestVar%>,<FormatString>
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
            // This does not show original format string, but in .Net format string!
            b.Append(StringEscaper.DoubleQuote(FormatString));
            return b.ToString();
        }
    }

    [Serializable]
    public class StrFormatInfo_Path : StrFormatInfo
    {
        // StrFormat,FileName,<FilePath>,<%DestVar%>
        // StrFormat,DirPath,<FilePath>,<%DestVar%> -- Same with StrFormat,Path
        // StrFormat,Ext,<FilePath>,<%DestVar%>
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
            b.Append(StringEscaper.DoubleQuote(FilePath));
            b.Append(",");
            b.Append(DestVar);
            return b.ToString();
        }
    }

    [Serializable]
    public class StrFormatInfo_PathCombine : StrFormatInfo
    { // StrFormat,PathCombine,<DirPath>,<FileName>,<%DestVar%>
        public string DirPath;
        public string FileName;
        public string DestVar;

        public StrFormatInfo_PathCombine(string dirPath, string fileName, string destVar)
        {
            DirPath = dirPath;
            FileName = fileName;
            DestVar = destVar;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(StringEscaper.DoubleQuote(DirPath));
            b.Append(",");
            b.Append(StringEscaper.DoubleQuote(FileName));
            b.Append(",");
            b.Append(DestVar);
            return b.ToString();
        }
    }

    [Serializable]
    public class StrFormatInfo_Arithmetic : StrFormatInfo
    {
        // StrFormat,Inc,<%DestVar%>,<Integer>
        // StrFormat,Dec,<%DestVar%>,<Integer>
        // StrFormat,Mult,<%DestVar%>,<Integer>
        // StrFormat,Div,<%DestVar%>,<Integer>

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
            b.Append(StringEscaper.DoubleQuote(Integer));
            return b.ToString();
        }
    }

    [Serializable]
    public class StrFormatInfo_LeftRight : StrFormatInfo
    { // Note : Integer can be negative integer, not like WB082's limitation
        // StrFormat,Left,<SrcStr>,<Count>,<%DestVar%>
        // StrFormat,Right,<SrcStr>,<Count>,<%DestVar%>
        public string SrcStr;
        public string Count;
        public string DestVar;

        public StrFormatInfo_LeftRight(string srcStr, string count, string destVar)
        {
            SrcStr = srcStr;
            Count = count;
            DestVar = destVar;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(SrcStr);
            b.Append(",");
            b.Append(StringEscaper.DoubleQuote(Count));
            b.Append(",");
            b.Append(DestVar);
            return b.ToString();
        }
    }

    [Serializable]
    public class StrFormatInfo_SubStr : StrFormatInfo
    { // StrFormat,SubStr,<SrcStr>,<StartPos>,<Length>,<%DestVar%>
        public string SrcStr;
        public string StartPos; // Index start from 1, not 0!
        public string Length;
        public string DestVar;

        public StrFormatInfo_SubStr(string srcStr, string startPos, string length, string destVar)
        {
            SrcStr = srcStr;
            StartPos = startPos;
            Length = length;
            DestVar = destVar;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(SrcStr);
            b.Append(",");
            b.Append(StringEscaper.DoubleQuote(StartPos));
            b.Append(",");
            b.Append(StringEscaper.DoubleQuote(Length));
            b.Append(",");
            b.Append(DestVar);
            return b.ToString();
        }
    }

    [Serializable]
    public class StrFormatInfo_Len : StrFormatInfo
    { // StrFormat,Len,<SrcStr>,<%DestVar%>
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
        // StrFormat,LTrim,<SrcStr>,<Integer>,<%DestVar%>
        // StrFormat,RTrim,<SrcStr>,<Integer>,<%DestVar%>
        // StrFormat,CTrim,<SrcStr>,<Chars>,<%DestVar%>

        public string SrcStr;
        public string ToTrim;
        public string DestVarName;

        public StrFormatInfo_Trim(string srcStr, string trimValue, string destVar)
        {
            SrcStr = srcStr;
            ToTrim = trimValue;
            DestVarName = destVar;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(SrcStr);
            b.Append(",");
            b.Append(StringEscaper.DoubleQuote(ToTrim));
            b.Append(",");
            b.Append(DestVarName);
            return b.ToString();
        }
    }

    [Serializable]
    public class StrFormatInfo_NTrim : StrFormatInfo
    { // StrFormat,NTrim,<SrcStr>,<%DestVar%>
        public string SrcStr;
        public string DestVar;

        public StrFormatInfo_NTrim(string srcStr, string destVar)
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
    public class StrFormatInfo_ULCase : StrFormatInfo
    {
        // StrFormat,UCase,<SrcStr>,<%DestVar%>
        // StrFormat,LCase,<SrcStr>,<%DestVar%>

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
    {
        // StrFormat,Pos,<SrcStr>,<SubStr>,<%DestVar%>
        // StrFormat,PosX,<SrcStr>,<SubStr>,<%DestVar%>

        public string SrcStr;
        public string SubStr;
        public string DestVar;

        public StrFormatInfo_Pos(string srcStr, string subStr, string destVar)
        {
            SrcStr = srcStr;
            SubStr = subStr;
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
        // StrFormat,Replace,<SrcStr>,<SearchStr>,<ReplaceStr>,<%DestVar%>
        // StrFormat,ReplaceX,<SrcStr>,<SearchStr>,<ReplaceStr>,<%DestVar%>

        public string SrcStr;
        public string SearchStr;
        public string ReplaceStr;
        public string DestVar;

        public StrFormatInfo_Replace(string srcStr, string searchStr, string replaceStr, string destVar)
        {
            SrcStr = srcStr;
            SearchStr = searchStr;
            ReplaceStr = replaceStr;
            DestVar = destVar;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(StringEscaper.QuoteEscape(SrcStr));
            b.Append(",");
            b.Append(StringEscaper.QuoteEscape(SearchStr));
            b.Append(",");
            b.Append(StringEscaper.QuoteEscape(ReplaceStr));
            b.Append(",");
            b.Append(DestVar);
            return b.ToString();
        }
    }

    [Serializable]
    public class StrFormatInfo_ShortLongPath : StrFormatInfo
    {
        // StrFormat,ShortPath,<SrcStr>,<%DestVar%>
        // StrFormat,LongPath,<SrcStr>,<%DestVar%>

        public string SrcStr;
        public string DestVar;

        public StrFormatInfo_ShortLongPath(string srcStr, string destVar)
        {
            SrcStr = srcStr;
            DestVar = destVar;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(StringEscaper.QuoteEscape(SrcStr));
            b.Append(",");
            b.Append(DestVar);
            return b.ToString();
        }
    }

    [Serializable]
    public class StrFormatInfo_Split : StrFormatInfo
    { // StrFormat,Split,<SrcStr>,<Delimiter>,<Index>,<%DestVar%>
        public string SrcStr;
        public string Delimiter;
        public string Index;
        public string DestVar;

        public StrFormatInfo_Split(string srcString, string delimiter, string index, string destVar)
        {
            SrcStr = srcString;
            Delimiter = delimiter;
            Index = index;
            DestVar = destVar;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(StringEscaper.QuoteEscape(SrcStr));
            b.Append(",");
            b.Append(StringEscaper.QuoteEscape(Delimiter));
            b.Append(",");
            b.Append(StringEscaper.QuoteEscape(Index));
            b.Append(",");
            b.Append(StringEscaper.QuoteEscape(DestVar));
            return b.ToString();
        }
    }

    [Serializable]
    public class StrFormatInfo_Pad : StrFormatInfo
    {
        // StrFormat,PadLeft,<SrcStr>,<Count>,<PadChar>,<%DestVar%>
        // StrFormat,PadRight,<SrcStr>,<Count>,<PadChar>,<%DestVar%>
        public string SrcStr;
        public string TotalWidth; // Positive Integer
        public string PadChar;
        public string DestVar;

        public StrFormatInfo_Pad(string srcStr, string totalWidth, string padChar, string destVar)
        {
            SrcStr = srcStr;
            TotalWidth = totalWidth;
            PadChar = padChar;
            DestVar = destVar;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(SrcStr);
            b.Append(",");
            b.Append(StringEscaper.DoubleQuote(TotalWidth));
            b.Append(",");
            b.Append(StringEscaper.DoubleQuote(PadChar));
            b.Append(",");
            b.Append(DestVar);
            return b.ToString();
        }
    }
    #endregion

    #region CodeInfo 20 - String
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
        Add = 10, Sub, Mul, Div,
        IntDiv,
        Neg = 20,
        ToSign = 30, ToUnsign,
        BoolAnd = 40, BoolOr, BoolXor,
        BoolNot,
        BitAnd = 50, BitOr, BitXor,
        BitNot,
        BitShift = 60,
        Ceil = 70, Floor, Round,
        Abs = 80,
        Pow = 90,
        Hex = 100, Dec,
        Rand = 110,
    }

    [Serializable]
    public class MathInfo : CodeInfo { }

    [Serializable]
    public class MathInfo_Arithmetic : MathInfo
    {
        // Math,Add,<%DestVar%>,<Src1>,<Src2>
        // Math,Sub,<%DestVar%>,<Src1>,<Src2>
        // Math,Mul,<%DestVar%>,<Src1>,<Src2>
        // Math,Div,<%DestVar%>,<Src1>,<Src2>

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
    { // Math,IntDiv,<%QuotientVar%>,<%RemainderVar%>,<Src1>,<Src2>
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
    { // Math,Neg,<%DestVar%>,<Src>
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
        // Math,ToSign,<%DestVar%>,<Src>,[BitSize]
        // Math,ToUnsign,<%DestVar%>,<Src>,[BitSize]

        public string DestVar;
        public string Src;
        public uint BitSize; // [8|16|32|64]

        public MathInfo_IntegerSignedness(string destVar, string src, uint bitSize)
        {
            DestVar = destVar;
            Src = src;
            BitSize = bitSize;
        }

        public override string ToString()
        {
            return $"{DestVar},{Src},{BitSize}";
        }
    }

    [Serializable]
    public class MathInfo_BoolLogicOperation : MathInfo
    {
        // Math,BoolAnd,<%DestVar%>,<Src1>,<Src2>
        // Math,BoolOr,<%DestVar%>,<Src1>,<Src2>
        // Math,BoolXor,<%DestVar%>,<Src1>,<Src2>

        public string DestVar;
        public string Src1;
        public string Src2;

        public MathInfo_BoolLogicOperation(string destVar, string src1, string src2)
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
    { // Math,BoolNot,<%DestVar%>,<Src>
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
    public class MathInfo_BitLogicOperation : MathInfo
    {
        // Math,BitAnd,<%DestVar%>,<Src1>,<Src2>
        // Math,BitOr,<%DestVar%>,<Src1>,<Src2>
        // Math,BitXor,<%DestVar%>,<Src1>,<Src2>

        public string DestVar;
        public string Src1; // Should be unsigned
        public string Src2; // Should be unsigned

        public MathInfo_BitLogicOperation(string destVar, string src1, string src2)
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
    { // Math,BitNot,<%DestVar%>,<Src>,[BitSize]
        public string DestVar;
        public string Src; // Should be unsigned
        public uint BitSize; // [8|16|32|64]

        public MathInfo_BitNot(string destVar, string src, uint bitSize)
        {
            DestVar = destVar;
            Src = src;
            BitSize = bitSize;
        }

        public override string ToString()
        {
            return $"{DestVar},{Src},{BitSize}";
        }
    }

    [Serializable]
    public class MathInfo_BitShift : MathInfo
    { // Math,BitShift,<%DestVar%>,<Src>,<Direction>,<Shift>,[BitSize],[UNSIGNED]
        public string DestVar;
        public string Src;
        public string Direction;
        public string Shift;
        public uint BitSize; // Optional, [8|16|32|64]
        public bool Unsigned; // Optional, UNSIGNED

        public MathInfo_BitShift(string destVar, string src, string direction, string shift, uint bitSize, bool _unsigned)
        {
            DestVar = destVar;
            Src = src;
            Direction = direction;
            Shift = shift;
            BitSize = bitSize;
            Unsigned = _unsigned;
        }

        public override string ToString()
        {
            return $"{DestVar},{Src},{Direction},{Shift},{BitSize},{Unsigned}";
        }
    }

    [Serializable]
    public class MathInfo_CeilFloorRound : MathInfo
    {
        // Math,Ceil,<%DestVar%>,<Src>,<Unit>
        // Math,Floor,<%DestVar%>,<Src>,<Unit>
        // Math,Round,<%DestVar%>,<Src>,<Unit>

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
    { // Math,Abs,<%DestVar%>,<Src>
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
    { // Math,Pow,<%DestVar%>,<Base>,<PowerOf>
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

    [Serializable]
    public class MathInfo_HexDec : MathInfo
    {
        // Math,Hex,<%DestVar%>,<Src>,[BitSize]
        // Math,Dec,<%DestVar%>,<Src>,[BitSize]
        public string DestVar;
        public string Src;
        public uint BitSize; // Optional, [8|16|32|64]

        public MathInfo_HexDec(string destVar, string src, uint bitSize)
        {
            DestVar = destVar;
            Src = src;
            BitSize = bitSize;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(DestVar);
            b.Append(",");
            b.Append(Src);
            b.Append(",");
            b.Append(BitSize);
            return b.ToString();
        }
    }

    [Serializable]
    public class MathInfo_Rand : MathInfo
    { // Math,Rand,<%DestVar%>[,Min,Max]
        public string DestVar;
        public string Min; // Optional, defaults to 0, must be zero or positive integer
        public string Max; // Optional, defaults to 65536, maximum is int.MaxValue (2147483647)
        // Min and Max must be used simultaneously.
        // Returned value can be 'Min' to 'Max - 1'.

        public MathInfo_Rand(string destVar, string min, string max)
        {
            DestVar = destVar;
            Min = min;
            Max = max;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(DestVar);
            if (Min != null)
            {
                b.Append(",");
                b.Append(Min);
                if (Max != null)
                {
                    b.Append(",");
                    b.Append(Max);
                }
            }
            return b.ToString();
        }
    }
    #endregion

    #region CodeInfo 21 - Math
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

    #region ListType, ListInfo
    public enum ListType
    {
        Get = 10,
        Set,
        Append = 20,
        Insert,
        Remove = 30, RemoveX,
        RemoveAt,
        Count = 40,
        Pos = 50, PosX, LastPos, LastPosX,
        Sort = 60, SortX, SortN, SortNX,
    }

    [Serializable]
    public class ListInfo : CodeInfo
    {
        public string ListVar;

        public ListInfo(string listVar)
        {
            ListVar = listVar;
        }
    }

    [Serializable]
    public class ListInfo_Get : ListInfo
    { // List,Get,<%ListVar%>,<Index>,<%DestVar%>,[Delim=<Str>]
        public string Index;
        public string DestVar;
        public string Delim;

        public ListInfo_Get(string listVar, string index, string destVar, string delim) : base(listVar)
        {
            Index = index;
            DestVar = destVar;
            Delim = delim;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(ListVar);
            b.Append(',');
            b.Append(Index);
            b.Append(',');
            b.Append(DestVar);
            if (Delim != null)
            {
                b.Append(",Delim=");
                b.Append(Delim);
            }
            return b.ToString();
        }
    }

    [Serializable]
    public class ListInfo_Set : ListInfo
    { // List,Set,<%ListVar%>,<Index>,<Item>,[Delim=<Str>]
        public string Index;
        public string Item;
        public string Delim;

        public ListInfo_Set(string listVar, string index, string item, string delim) : base(listVar)
        {
            Index = index;
            Item = item;
            Delim = delim;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(ListVar);
            b.Append(',');
            b.Append(Index);
            b.Append(',');
            b.Append(Item);
            if (Delim != null)
            {
                b.Append(",Delim=");
                b.Append(Delim);
            }
            return b.ToString();
        }
    }

    [Serializable]
    public class ListInfo_Append : ListInfo
    { // List,Append,<%ListVar%>,<Item>,[Delim=<Str>]
        public string Item;
        public string Delim;

        public ListInfo_Append(string listVar, string item, string delim) : base(listVar)
        {
            Item = item;
            Delim = delim;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(ListVar);
            b.Append(',');
            b.Append(Item);
            if (Delim != null)
            {
                b.Append(",Delim=");
                b.Append(Delim);
            }
            return b.ToString();
        }
    }

    [Serializable]
    public class ListInfo_Insert : ListInfo
    { // List,Insert,<%ListVar%>,<Index>,<Item>,[Delim=<Str>]
        public string Index;
        public string Item;
        public string Delim;

        public ListInfo_Insert(string listVar, string index, string item, string delim) : base(listVar)
        {
            Index = index;
            Item = item;
            Delim = delim;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(ListVar);
            b.Append(',');
            b.Append(Index);
            b.Append(',');
            b.Append(Item);
            if (Delim != null)
            {
                b.Append(",Delim=");
                b.Append(Delim);
            }
            return b.ToString();
        }
    }

    [Serializable]
    public class ListInfo_Remove : ListInfo
    { // List,Remove,<%ListVar%>,<Item>,[Delim=<Str>]
        public string Item;
        public string Delim;

        public ListInfo_Remove(string listVar, string item, string delim) : base(listVar)
        {
            Item = item;
            Delim = delim;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(ListVar);
            b.Append(',');
            b.Append(Item);
            if (Delim != null)
            {
                b.Append(",Delim=");
                b.Append(Delim);
            }
            return b.ToString();
        }
    }

    [Serializable]
    public class ListInfo_RemoveAt : ListInfo
    { // List,RemoveAt,<%ListVar%>,<Index>,[Delim=<Str>]
        public string Index;
        public string Delim;

        public ListInfo_RemoveAt(string listVar, string index, string delim) : base(listVar)
        {
            Index = index;
            Delim = delim;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(ListVar);
            b.Append(',');
            b.Append(Index);
            if (Delim != null)
            {
                b.Append(",Delim=");
                b.Append(Delim);
            }
            return b.ToString();
        }
    }

    [Serializable]
    public class ListInfo_Count : ListInfo
    { // List,Count,<%ListVar%>,<%DestVar%>,[Delim=<Str>]
        public string DestVar;
        public string Delim;

        public ListInfo_Count(string listVar, string destVar, string delim) : base(listVar)
        {
            DestVar = destVar;
            Delim = delim;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(ListVar);
            b.Append(',');
            b.Append(DestVar);
            if (Delim != null)
            {
                b.Append(",Delim=");
                b.Append(Delim);
            }
            return b.ToString();
        }
    }

    [Serializable]
    public class ListInfo_Pos : ListInfo
    {
        // List,Pos,<%ListVar%>,<Item>,<%DestVar%>,[Delim=<Str>]
        // List,PosX,<%ListVar%>,<Item>,<%DestVar%>,[Delim=<Str>]
        // List,LastPos,<%ListVar%>,<Item>,<%DestVar%>,[Delim=<Str>]
        // List,LastPosX,<%ListVar%>,<Item>,<%DestVar%>,[Delim=<Str>]
        public string Item;
        public string DestVar;
        public string Delim;

        public ListInfo_Pos(string listVar, string item, string destVar, string delim) : base(listVar)
        {
            Item = item;
            DestVar = destVar;
            Delim = delim;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(ListVar);
            b.Append(',');
            b.Append(Item);
            b.Append(',');
            b.Append(DestVar);
            if (Delim != null)
            {
                b.Append(",Delim=");
                b.Append(Delim);
            }
            return b.ToString();
        }
    }

    [Serializable]
    public class ListInfo_Sort : ListInfo
    {
        // List,Sort,<%ListVar%>,<Asc|Desc>,[Delim=<Str>]
        // List,SortX,<%ListVar%>,<Asc|Desc>,[Delim=<Str>]
        // List,SortN,<%ListVar%>,<Asc|Desc>,[Delim=<Str>]
        // List,SortNX,<%ListVar%>,<Asc|Desc>,[Delim=<Str>]
        public string Order;
        public string Delim;

        public ListInfo_Sort(string listVar, string order, string delim) : base(listVar)
        {
            Order = order;
            Delim = delim;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(ListVar);
            b.Append(',');
            b.Append(Order);
            if (Delim != null)
            {
                b.Append(",Delim=");
                b.Append(Delim);
            }
            return b.ToString();
        }
    }
    #endregion

    #region CodeInfo 22 - List
    [Serializable]
    public class CodeInfo_List : CodeInfo
    {
        public ListType Type;
        public ListInfo SubInfo;

        public CodeInfo_List(ListType type, ListInfo subInfo)
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

    #region BranchCondition
    public enum BranchConditionType
    {
        None = 0,
        // Comparison
        Equal, EqualX, Smaller, Bigger, SmallerEqual, BiggerEqual, // <%Var%>,Operator,<Value>
        // Existence
        // Note : Wrong terminology with ExistRegSection/ExistRegKey!
        // See https://msdn.microsoft.com/en-us/library/windows/desktop/ms724946(v=vs.85).aspx for details
        // ExistRegSubKey and ExistRegValue are proposed for more accurate terms
        ExistFile, // <FilePath>
        ExistDir, // <DirPath>
        ExistSection, // <IniFile>,<Section>
        ExistRegSection, ExistRegSubKey, // <RootKey>,<SubKey> 
        ExistRegKey, ExistRegValue, // <RootKey>,<SubKey>,<ValueName>
        ExistRegMulti, // <RootKey>,<SubKey>,<ValueName>,<SearchStr>
        ExistVar, // <%Var%>
        ExistMacro, // <MacroName>
        // Wim
        WimExistIndex, // <WimFile>,<ImageIndex>
        WimExistFile, // <WimFile>,<ImageIndex>,<FilePath>
        WimExistDir, // <WimFile>,<ImageIndex>,<DirPath>
        WimExistImageInfo, // <WimFile>,<ImageIndex>,<Key>
        // ETC
        Ping, // <Host>
        Online, // No Argument 
        Question, // <Message> or <Message>,<Timeout>,<DefaultChoice>
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
        public string Arg4;
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
                case BranchConditionType.EqualX:
                case BranchConditionType.ExistSection:
                case BranchConditionType.ExistRegSection:
                case BranchConditionType.ExistRegSubKey:
                case BranchConditionType.WimExistIndex:
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
                case BranchConditionType.WimExistFile:
                case BranchConditionType.WimExistDir:
                case BranchConditionType.WimExistImageInfo:
                    Arg1 = arg1;
                    Arg2 = arg2;
                    Arg3 = arg3;
                    break;
                default:
                    throw new InternalException($"Wrong BranchCondition, [{type}] does not take 3 arguments");
            }
        }

        public BranchCondition(BranchConditionType type, bool notFlag, string arg1, string arg2, string arg3, string arg4)
        {
            Type = type;
            NotFlag = notFlag;
            switch (type)
            {
                case BranchConditionType.ExistRegMulti:
                    Arg1 = arg1;
                    Arg2 = arg2;
                    Arg3 = arg3;
                    Arg4 = arg4;
                    break;
                default:
                    throw new InternalException($"Wrong BranchCondition, [{type}] does not take 3 arguments");
            }
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
                case BranchConditionType.EqualX:
                    if (NotFlag)
                        b.Append("Not,");
                    b.Append(Arg1);
                    b.Append(",");
                    b.Append(Type);
                    b.Append(",");
                    b.Append(Arg2);
                    break;
                case BranchConditionType.ExistFile:
                case BranchConditionType.ExistDir:
                case BranchConditionType.ExistVar:
                case BranchConditionType.ExistMacro:
                case BranchConditionType.Ping:
                    if (NotFlag)
                        b.Append("Not,");
                    b.Append(Type);
                    b.Append(",");
                    b.Append(Arg1);
                    break;
                case BranchConditionType.ExistSection:
                case BranchConditionType.ExistRegSection:
                case BranchConditionType.ExistRegSubKey:
                case BranchConditionType.WimExistIndex:
                    if (NotFlag)
                        b.Append("Not,");
                    b.Append(Type);
                    b.Append(",");
                    b.Append(Arg1);
                    b.Append(",");
                    b.Append(Arg2);
                    break;
                case BranchConditionType.ExistRegKey:
                case BranchConditionType.ExistRegValue:
                case BranchConditionType.WimExistFile:
                case BranchConditionType.WimExistDir:
                    if (NotFlag)
                        b.Append("Not,");
                    b.Append(Type);
                    b.Append(",");
                    b.Append(Arg1);
                    b.Append(",");
                    b.Append(Arg2);
                    b.Append(",");
                    b.Append(Arg3);
                    break;
                case BranchConditionType.Question: // can have 1 or 3 argument
                    if (NotFlag)
                        b.Append("Not,");
                    if (Arg2 != null)
                    {
                        b.Append(Type);
                        b.Append(",");
                        b.Append(Arg1);
                        b.Append(",");
                        b.Append(Arg2);
                        b.Append(",");
                        b.Append(Arg3);
                    }
                    else
                    {
                        b.Append(Type);
                        b.Append(",");
                        b.Append(Arg1);
                    }
                    break;
            }
            return b.ToString();
        }
    }
    #endregion

    #region CodeInfo 80 - Branch
    [Serializable]
    public class CodeInfo_RunExec : CodeInfo
    {
        // Run,<ScriptFile>,<Section>,[InParams]
        // RunEx,<ScriptFile>,<Section>,[InOutParams]
        // Exec,<ScriptFile>,<Section>,[InParams]
        // Ex) Run,%ScriptFile%,Test,5,%D%
        // Ex) RunEx,%ScriptFile%,Test,In=5,Out=%D%,In=8

        public string ScriptFile;
        public string SectionName;
        public List<string> InParams;
        public List<string> OutParams;

        public CodeInfo_RunExec(string scriptFile, string sectionName, List<string> inParams, List<string> outParams)
        {
            ScriptFile = scriptFile;
            SectionName = sectionName;
            InParams = inParams;
            OutParams = outParams;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(ScriptFile);
            b.Append(",");
            b.Append(SectionName);
            foreach (string inParam in InParams)
            {
                b.Append(",");
                b.Append(inParam);
            }
            if (OutParams != null)
            {
                foreach (string outParam in OutParams)
                {
                    b.Append(",Out=");
                    b.Append(outParam);
                }
            }
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_Loop : CodeInfo
    {
        // Loop,<ScriptFile>,<Section>,<StartIndex>,<EndIndex>[,InParams]
        // Loop,BREAK
        // LoopLetter,<ScriptFile>,<Section>,<StartLetter>,<EndLetter>[,InParams]
        // LoopLetter,BREAK

        // LoopEx,<ScriptFile>,<Section>,<StartIndex>,<EndIndex>[,InOutParams]
        // LoopEx,BREAK
        // LoopLetterEx,<ScriptFile>,<Section>,<StartLetter>,<EndLetter>[,InOutParams]
        // LoopLetterEx,BREAK

        public string ScriptFile;
        public string SectionName;
        public string StartIdx;
        public string EndIdx;
        public List<string> InParams;
        public List<string> OutParams;
        public bool Break;

        public CodeInfo_Loop(string scriptFile, string sectionName, string startIdx, string endIdx, List<string> inParams, List<string> outParams)
        {
            Break = false;
            ScriptFile = scriptFile;
            SectionName = sectionName;
            StartIdx = startIdx;
            EndIdx = endIdx;
            InParams = inParams;
            OutParams = outParams;
        }

        public CodeInfo_Loop()
        {
            Break = true;
        }

        public override string ToString()
        {
            if (Break)
                return "BREAK";

            StringBuilder b = new StringBuilder();
            b.Append(ScriptFile);
            b.Append(",");
            b.Append(SectionName);
            b.Append(",");
            b.Append(StartIdx);
            b.Append(",");
            b.Append(EndIdx);
            foreach (string inParam in InParams)
            {
                b.Append(",");
                b.Append(inParam);
            }
            if (OutParams != null)
            {
                foreach (string outParam in OutParams)
                {
                    b.Append(",Out=");
                    b.Append(outParam);
                }
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


        public override string ToString()
        {
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


        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(Embed);
            return b.ToString();
        }
    }
    #endregion

    #region CodeInfo 81 - Control
    [Serializable]
    public class CodeInfo_Set : CodeInfo
    { // Set,<%Var%>,<Value>,[GLOBAL|PERMANENT]
        public string VarKey;
        public string VarValue;
        public bool Global;
        public bool Permanent;

        public CodeInfo_Set(string varKey, string varValue, bool global, bool permanent)
        {
            VarKey = varKey;
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
    public class CodeInfo_SetMacro : CodeInfo
    { // SetMacro,<MacroName>,<MacroCommand>,[GLOBAL|PERMANENT]
        public string MacroName;
        public string MacroCommand;
        public bool Global;
        public bool Permanent;

        public CodeInfo_SetMacro(string macroName, string macroCommand, bool global, bool permanent)
        {
            MacroName = macroName;
            MacroCommand = macroCommand;
            Global = global;
            Permanent = permanent;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(MacroName);
            b.Append(",");
            b.Append(MacroCommand);
            if (Permanent)
                b.Append(",PERMANENT");
            else if (Global)
                b.Append(",GLOBAL");
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_AddVariables : CodeInfo
    { // AddVariables,%ScriptFile%,<Section>,[GLOBAL]
        public string ScriptFile;
        public string SectionName;
        public bool Global;

        public CodeInfo_AddVariables(string scriptFile, string sectionName, bool global)
        {
            ScriptFile = scriptFile;
            SectionName = sectionName;
            Global = global;
        }
    }

    [Serializable]
    public class CodeInfo_GetParam : CodeInfo
    { // GetParam,<Index>,<%DestVar%>
        public string Index;
        public string DestVar;

        public CodeInfo_GetParam(string index, string destVar)
        {
            Index = index;
            DestVar = destVar;
        }

        public override string ToString()
        {
            return $"{Index},{DestVar}";
        }
    }

    [Serializable]
    public class CodeInfo_PackParam : CodeInfo
    { // PackParam,<StartIndex>,<%DestVar%>,[VarCount]
        public string StartIndex;
        public string DestVar;
        public string VarCount; // optional

        public CodeInfo_PackParam(string startIndex, string destVar, string varCount)
        {
            StartIndex = startIndex;
            DestVar = destVar;
            VarCount = varCount;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(StartIndex);
            b.Append(",");
            b.Append(DestVar);
            if (VarCount != null)
            {
                b.Append(",");
                b.Append(VarCount);
            }
            return b.ToString();
        }
    }

    [Serializable]
    public class CodeInfo_Exit : CodeInfo
    { // Exit,<Message>,[NOWARN]
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
    { // Halt,<Message>,[NOWARN]
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

    #region CodeInfo 82 - System
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
            return $"{Type},{SubInfo}";
        }
    }

    #region SystemType, SystemInfo
    public enum SystemType
    {
        Cursor,
        ErrorOff,
        GetEnv,
        GetFreeDrive,
        GetFreeSpace,
        IsAdmin,
        OnBuildExit,
        OnScriptExit,
        RefreshInterface,
        RefreshAllScripts, RescanScripts,
        LoadNewScript,
        RefreshScript,
        // Load,
        SaveLog,
        SetLocal, EndLocal,
        // Deprecated, WB082 Compability Shim
        HasUAC,
        FileRedirect,
        RegRedirect,
        RebuildVars,
    }

    [Serializable]
    public class SystemInfo : CodeInfo { }

    [Serializable]
    public class SystemInfo_Cursor : SystemInfo
    { // System,Cursor,<State>
        public string State;

        public SystemInfo_Cursor(string state)
        {
            State = state;
        }

        public override string ToString()
        {
            return $"Cursor,{State}";
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
    { // System,GetEnv,<EnvVar>,<%DestVar%>
        public string EnvVar;
        public string DestVar;

        public SystemInfo_GetEnv(string envVar, string destVar)
        {
            EnvVar = envVar;
            DestVar = destVar;
        }

        public override string ToString()
        {
            return $"GetEnv,{EnvVar},{DestVar}";
        }
    }

    [Serializable]
    public class SystemInfo_GetFreeDrive : SystemInfo
    { // System,GetFreeDrive,<%DestVar%>
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
    { // System,GetFreeSpace,<Path>,<%DestVar%>
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
    { // System,HasUAC,<%DestVar%>
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
    { // System,IsAdmin,<%DestVar%>
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
    public class SystemInfo_OnScriptExit : SystemInfo
    { // System,OnScriptExit,<Command>
        public CodeCommand Cmd;

        public SystemInfo_OnScriptExit(CodeCommand cmd)
        {
            Cmd = cmd;
        }

        public override string ToString()
        {
            return $"OnScriptExit,{Cmd}";
        }
    }

    [Serializable]
    public class SystemInfo_LoadNewScript : SystemInfo
    { // System,LoadNewScript,<SrcFilePath>,<DestTreeDir>,[PRESERVE],[NOWARN],[NOREC]
        public string SrcFilePath;
        /// <summary>
        /// DestTreeDir should not include project root directory.
        /// Ex) ChrisPE\Core\IME.script -> Use 'Core' as DestTreeDir
        /// </summary>
        public string DestTreeDir;
        public bool PreserveFlag;
        public bool NoWarnFlag;
        public bool NoRecFlag;

        public SystemInfo_LoadNewScript(string srcFilePath, string destTreeDir, bool preserve, bool noWarn, bool noRec)
        {
            SrcFilePath = srcFilePath;
            DestTreeDir = destTreeDir;
            PreserveFlag = preserve;
            NoWarnFlag = noWarn;
            NoRecFlag = noRec;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder(8);
            b.Append("LoadNewScript,");
            b.Append(SrcFilePath);
            b.Append(",");
            b.Append(DestTreeDir);
            if (PreserveFlag)
                b.Append(",PRESERVE");
            if (NoWarnFlag)
                b.Append(",NOWARN");
            if (NoRecFlag)
                b.Append(",NOREC");
            return b.ToString();
        }
    }

    [Serializable]
    public class SystemInfo_RefreshScript : SystemInfo
    { // System,RefreshScript,<FilePath>,[NOREC]
        public string FilePath;
        public bool NoRecFlag;

        public SystemInfo_RefreshScript(string filePath, bool noRec)
        {
            FilePath = filePath;
            NoRecFlag = noRec;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder(8);
            b.Append("RefreshScript,");
            b.Append(FilePath);
            if (NoRecFlag)
                b.Append(",NOREC");
            return b.ToString();
        }
    }

    [Serializable]
    public class SystemInfo_SaveLog : SystemInfo
    { // System,SaveLog,<DestPath>,[LogFormat]
        public string DestPath;
        public string LogFormat;

        public SystemInfo_SaveLog(string destPath, string logFormat)
        {
            DestPath = destPath;
            LogFormat = logFormat;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append("SaveLog,");
            b.Append(DestPath);
            if (LogFormat == null)
            {
                b.Append(",");
                b.Append(LogFormat);
            }
            return b.ToString();
        }
    }
    #endregion

    [Serializable]
    public class CodeInfo_ShellExecute : CodeInfo
    {
        // ShellExecute,<Action>,<FilePath>[,Params][,WorkDir][,%ExitOutVar%]
        // ShellExecuteEx,<Action>,<FilePath>[,Params][,WorkDir]
        // ShellExecuteDelete,<Action>,<FilePath>[,Params][,WorkDir][,%ExitOutVar%]
        public string Action;
        public string FilePath;
        public string Params; // Optional
        public string WorkDir; // Optional
        public string ExitOutVar; // Optional

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

    #region CodeInfo 99 - Macro
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
