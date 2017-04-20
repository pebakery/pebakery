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

using PEBakery.Exceptions;
using PEBakery.Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.IO;
using PEBakery.Lib;
using System.Net.NetworkInformation;

namespace PEBakery.Core
{
    #region CodeType

    public enum CodeType
    {
        // 00 Misc
        None = 0, Comment, Error, Unknown,
        // 01 File
        CopyOrExpand = 100, DirCopy, DirDelete, DirMove, DirMake, Expand, FileCopy, FileDelete, FileRename, FileMove, FileCreateBlank, FileByteExtract,
        // 02 Registry
        RegHiveLoad = 200, RegHiveUnload, RegImport, RegWrite, RegRead, RegDelete, RegWriteBin, RegReadBin, RegMulti,
        // 03 Text
        TXTAddLine = 300, TXTReplace, TXTDelLine, TXTDelSpaces, TXTDelEmptyLines,
        // 04 INI
        INIWrite = 400, INIRead, INIDelete, INIAddSection, INIDeleteSection, INIWriteTextLine, INIMerge,
        // 05 Network
        WebGet = 500, WebGetIfNotExist,
        // 06 Attach, Interface
        ExtractFile = 600, ExtractAndRun, ExtractAllFiles, ExtractAllFilesIfNotExist, Encode,
        // 07 UI
        Message = 700, Echo, Retrieve, Visible,
        // 08 StringFormat
        StrFormat = 800,
        // 09 System
        System = 900, ShellExecute, ShellExecuteEx, ShellExecuteDelete,
        // 10 Branch
        Run = 1000, Exec, Loop, If, Else, Begin, End,
        // 11 Control
        Set = 1100, GetParam, PackParam, AddVariables, Exit, Halt, Wait, Beep, // GetParam and PackParam will be depracted, PEBakery can have infinite number of section params.
        // 12 External Macro
        Macro = 1200,
    }

    #endregion

    #region SectionAddress
    public struct SectionAddress
    {
        public Plugin Plugin;
        public PluginSection Section;

        public SectionAddress(Plugin plugin, PluginSection section)
        {
            this.Plugin = plugin;
            this.Section = section; ;
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
            return Plugin.FullPath.GetHashCode() ^ Section.SectionName.GetHashCode() ^ Section.Count;
        }
    }
    #endregion

    #region CodeCommand
    public class CodeCommand
    {
        public string RawCode;
        public SectionAddress Addr;

        public CodeType Type;
        public CodeInfo Info;

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
    }
    #endregion

    #region CodeCommandInfo
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
    public class CodeInfo_Expand : CodeInfo
    {
        public string SrcCab;
        public string DestDir;
        public bool IsSingleFile;
        public string SingleFile;
        public bool Preserve;
        public bool NoWarn;

        public CodeInfo_Expand(string srcCab, string destDir, bool isSingleFile, string singleFile, bool preserve, bool noWarn)
        {
            SrcCab = srcCab;
            DestDir = destDir;
            IsSingleFile = isSingleFile;
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
            if (IsSingleFile)
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

    public class CodeInfo_FileCopy : CodeInfo
    {
        public string SrcFile;
        public string DestPath;
        public bool Preserve;
        public bool NoWarn;
        public bool NoRec;
        public bool Show;

        public CodeInfo_FileCopy(string srcFile, string destPath, bool preserve, bool noWarn, bool noRec, bool show)
        {
            SrcFile = srcFile;
            DestPath = destPath;
            Preserve = preserve;
            NoWarn = noWarn;
            NoRec = noRec;
            Show = show;
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
            if (Show)
                b.Append(",SHOW");

            return b.ToString();
        }
    }
    #endregion

    #region CodeInfo 03 - Text
    public enum TXTAddLineMode { Append, Prepend, Place };
    public class CodeInfo_TXTAddLine : CodeInfo
    {
        public string FileName;
        public string Line;
        public TXTAddLineMode Mode;
        public int LineNum; // Optional, -1 if not used

        public CodeInfo_TXTAddLine(string fileName, string line, TXTAddLineMode mode, int lineNum = -1)
        {
            FileName = fileName;
            Line = line;
            Mode = mode;
            LineNum = lineNum;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(FileName);
            b.Append(",");
            b.Append(Line);
            b.Append(",");
            b.Append(Mode);
            if (LineNum != -1)
            {
                b.Append(",");
                b.Append(LineNum);
            }
            return b.ToString();
        }
    }

    public class CodeInfo_TXTReplace : CodeInfo
    {
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
    public class CodeInfo_INIWrite : CodeInfo
    {
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

    public class CodeInfo_INIRead : CodeInfo
    {
        public string FileName;
        public string SectionName;
        public string Key;
        public string VarName;

        public CodeInfo_INIRead(string fileName, string sectionName, string key, string varName)
        {
            FileName = fileName;
            SectionName = sectionName;
            Key = key;
            VarName = varName;
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
            b.Append(VarName);
            b.Append("%");
            return b.ToString();
        }
    }

    public class CodeInfo_INIDelete : CodeInfo
    {
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

    public class CodeInfo_INIAddSection : CodeInfo
    { 
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

    public class CodeInfo_INIDeleteSection : CodeInfo
    {
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

    public class CodeInfo_INIWriteTextLine : CodeInfo
    {
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

    public class CodeInfo_INIMerge : CodeInfo
    {
        // INIMerge,<SrcFileName>,<DestFileName>
        // INIMerge,<SrcFileName>,<SectionName>,<DestFileName>
        public string SrcFileName;
        public string DestFileName;
        public string SectionName; // optional

        public CodeInfo_INIMerge(string srcFileName, string destFileName, string sectionName = null)
        {
            SrcFileName = srcFileName;
            DestFileName = destFileName;
            SectionName = sectionName;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(SrcFileName);
            b.Append(",");
            b.Append(DestFileName);
            if (SectionName != null)
            {
                b.Append(",");
                b.Append(SectionName);
            }
            return b.ToString();
        }
    }
    #endregion

    #region CodeInfo 07 - UI
    public enum CodeMessageAction { Information, Confirmation, Error, Warning }
    public class CodeInfo_Message : CodeInfo
    {
        public string Message;
        public CodeMessageAction Action;
        public int Timeout; // Optional, set to -1 to disable

        public CodeInfo_Message(string message, CodeMessageAction action, int timeout = -1)
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
            if (Timeout == -1)
            {
                b.Append(",");
                b.Append(Timeout);
            }
            return b.ToString();
        }
    }

    public class CodeInfo_Echo : CodeInfo
    {
        public string Message;

        public CodeInfo_Echo(string message)
        {
            Message = message;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(Message);
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
        ExistFile, ExistDir, ExistSection, ExistRegSection, ExistRegKey, ExistVar, ExistMacro,
        // ETC
        Ping, Online, Question,
        // Deprecated
        License
    }

    public delegate string ArugmentPreprocess(string str);
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
                    throw new InternalUnknownException($"Wrong BranchCondition, [{type}] does not take 1 argument");
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
                    Arg1 = arg1;
                    break;
                default:
                    throw new InternalUnknownException($"Wrong BranchCondition, [{type}] does not take 1 argument");
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
                    Arg1 = arg1;
                    Arg2 = arg2;
                    break;
                default:
                    throw new InternalUnknownException($"Wrong BranchCondition, [{type}] does not take 2 arguments");
            }
        }

        public BranchCondition(BranchConditionType type, bool notFlag, string arg1, string arg2, string arg3)
        {
            Type = type;
            NotFlag = notFlag;
            switch (type)
            {
                case BranchConditionType.ExistRegKey:
                    Arg1 = arg1;
                    Arg2 = arg2;
                    Arg3 = arg3;
                    break;
                default:
                    throw new InternalUnknownException($"Wrong BranchCondition, [{type}] does not take 3 arguments");
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

                        CompareStringNumberResult comp = NumberHelper.CompareStringNumber(compArg1, compArg2);

                        switch (comp)
                        {
                            case CompareStringNumberResult.Equal: // For String and Number
                                {
                                    if (Type == BranchConditionType.Equal
                                        || Type == BranchConditionType.SmallerEqual
                                        || Type == BranchConditionType.BiggerEqual)
                                        match = true;
                                    logMessage = $"[{compArg1}] is equal to [{compArg2}]";
                                }
                                break;
                            case CompareStringNumberResult.Smaller: // For Number
                                {
                                    if (Type == BranchConditionType.Smaller
                                        || Type == BranchConditionType.SmallerEqual
                                        || Type == BranchConditionType.Bigger && NotFlag
                                        || Type == BranchConditionType.BiggerEqual && NotFlag)
                                        match = true;
                                    logMessage = $"[{compArg1}] is smaller than [{compArg2}]";
                                }
                                break;
                            case CompareStringNumberResult.Bigger: // For Number
                                {
                                    if (Type == BranchConditionType.Bigger
                                        || Type == BranchConditionType.BiggerEqual
                                        || Type == BranchConditionType.Smaller && NotFlag
                                        || Type == BranchConditionType.SmallerEqual && NotFlag)
                                        match = true;
                                    logMessage = $"[{compArg1}] is bigger than [{compArg2}]";
                                }
                                break;
                            case CompareStringNumberResult.NotEqual: // For String
                                {
                                    if (Type == BranchConditionType.Equal && NotFlag)
                                        match = true;
                                    logMessage = $"[{compArg1}] is not equal to [{compArg2}]";
                                }
                                break;
                            default:
                                throw new InternalUnknownException($"Cannot compare [{compArg1}] and [{compArg2}]");
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
                        if (filePathContainsWildcard)
                        {
                            string[] list = Directory.GetFiles(FileHelper.GetDirNameEx(filePath), Path.GetFileName(filePath));
                            if (0 < list.Length)
                                match = true;
                            else
                                match = false;
                        }
                        else
                            match = File.Exists(filePath);

                        if (match)
                            logMessage = $"File [{Arg1}] exists";
                        else
                            logMessage = $"File [{Arg1}] does not exist";
                    }
                    break;
                case BranchConditionType.ExistDir:
                    {
                        string dirPath = StringEscaper.Preprocess(s, Arg1);

                        // Check filePath contains wildcard
                        bool dirPathContainsWildcard = true;
                        if (dirPath.IndexOfAny(new char[] { '*', '?' }) == -1) // No wildcard
                            dirPathContainsWildcard = false;

                        // Check if file exists
                        if (dirPathContainsWildcard)
                        {
                            string[] list = Directory.GetDirectories(FileHelper.GetDirNameEx(dirPath), Path.GetFileName(dirPath));
                            if (0 < list.Length)
                                match = true;
                            else
                                match = false;
                        }
                        else
                            match = Directory.Exists(dirPath);

                        if (match)
                            logMessage = $"Directory [{Arg1}] exists";
                        else
                            logMessage = $"Directory [{Arg1}] does not exist";
                    }
                    break;
                case BranchConditionType.ExistSection:
                    {
                        string iniFile = StringEscaper.Preprocess(s, Arg1);
                        string section = StringEscaper.Preprocess(s, Arg2);

                        match = Ini.CheckSectionExist(iniFile, section);
                        if (match)
                            logMessage = $"Section [{section}] exists in INI file [{Arg1}]";
                        else
                            logMessage = $"Section [{section}] does not exist in INI file [{Arg1}]";
                    }
                    break;
                case BranchConditionType.ExistRegSection:
                    {
                        string rootKey = StringEscaper.Preprocess(s, Arg1);
                        string subKey = StringEscaper.Preprocess(s, Arg2);

                        using (RegistryKey regRoot = RegistryHelper.ParseRootKeyToRegKey(rootKey))
                        {
                            if (regRoot == null)
                                throw new InvalidRegKeyException($"Invalid registry root key [{rootKey}]");
                            using (RegistryKey regSubKey = regRoot.OpenSubKey(subKey))
                            {
                                match = (regSubKey != null);
                                if (match)
                                    logMessage = $"Registry Key [{rootKey}\\{subKey}] exists";
                                else
                                    logMessage = $"Registry Key [{rootKey}\\{subKey}] does not exist";
                            }
                        }
                    }
                    break;
                case BranchConditionType.ExistRegKey:
                    {
                        string rootKey = StringEscaper.Preprocess(s, Arg1);
                        string subKey = StringEscaper.Preprocess(s, Arg2);
                        string valueName = StringEscaper.Preprocess(s, Arg3);

                        match = true;
                        using (RegistryKey regRoot = RegistryHelper.ParseRootKeyToRegKey(rootKey))
                        {
                            if (regRoot == null)
                                throw new InvalidRegKeyException($"Invalid registry root key [{rootKey}]");
                            using (RegistryKey regSubKey = regRoot.OpenSubKey(subKey))
                            {
                                if (regSubKey == null)
                                    match = false;
                                else
                                {
                                    object value = regSubKey.GetValue(valueName);
                                    if (value == null)
                                        match = false;
                                }
                                if (match)
                                    logMessage = $"Registry Value [{rootKey}\\{subKey}\\{valueName}] exists";
                                else
                                    logMessage = $"Registry Value [{rootKey}\\{subKey}\\{valueName}] does not exist";
                            }
                        }
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
                    }
                    break;
                case BranchConditionType.Online:
                    {
                        match = NetworkInterface.GetIsNetworkAvailable();

                        if (match)
                            logMessage = "System is connected to internet";
                        else
                            logMessage = "System is not connected to internet";
                    }
                    break;
                default:
                    throw new InternalUnknownException($"Wrong BranchCondition check, [{Type}] need additional infomation");
            }
            return match;
        }

        public bool Check(ArugmentPreprocess pp, Variables variables)
        {
            bool match = false;
            switch (Type)
            {
                case BranchConditionType.ExistVar:
                    {
                        string variableName = Variables.TrimPercentMark(Arg1);
                        match = variables.ContainsKey(variableName);
                    }
                    break;
                case BranchConditionType.ExistMacro:
                    // TODO
                    break;
                default:
                    throw new InternalUnknownException($"Wrong BranchCondition check, [{Type}] is not ExistVar");
            }
            return match;
        }

        public bool Check(ArugmentPreprocess pp, Macro macro)
        {
            bool match = false;
            switch (Type)
            {
                case BranchConditionType.ExistMacro:
                    // TODO
                    break;
                default:
                    throw new InternalUnknownException($"Wrong BranchCondition check, [{Type}] is not ExistMacro");
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

    #region CodeInfo 10 - Branch
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

    public class CodeInfo_Loop : CodeInfo
    {
        public bool Break;
        public string PluginFile;
        public string SectionName;
        public int StartIdx;
        public int EndIdx;
        public List<string> Parameters;

        public CodeInfo_Loop(string pluginFile, string sectionName, int startIdx, int endIdx, List<string> parameters)
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
        { // TODO
            StringBuilder b = new StringBuilder();
            b.Append(Condition);
            b.Append(",");
            b.Append(Embed);
            return b.ToString();
        }
    }

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
        { // TODO
            StringBuilder b = new StringBuilder();
            b.Append(Embed);
            return b.ToString();
        }
    }
    #endregion

    #region CodeInfo 11 - Control
    public class CodeInfo_Set : CodeInfo
    {
        public string VarName;
        public string VarValue;
        public bool Global;
        public bool Permanent;

        public CodeInfo_Set(string varName, string varValue, bool global, bool permanent)
        {
            VarName = varName;
            VarValue = varValue;
            Global = global;
            Permanent = permanent;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append("%");
            b.Append(VarName);
            b.Append("%,");
            b.Append(VarValue);
            if (Global)
                b.Append(",GLOBAL");
            if (Permanent)
                b.Append(",PERMANENT");

            return b.ToString();
        }
    }

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

    public class CodeInfo_PackParam : CodeInfo
    { // PackParam,<StartIndex>,<VarName>[,VarNum] -- Cannot figure out how it works
        public int StartIndex;
        public string VarName;
        public string VarNum; // optional

        public CodeInfo_PackParam(int startIndex, string varName, string varNum)
        {
            StartIndex = startIndex;
            VarName = varName;
            VarNum = varNum;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(StartIndex);
            b.Append(",");
            b.Append(VarName);
            if (VarNum != null)
            {
                b.Append(",");
                b.Append(VarNum);
            }
            return b.ToString();
        }
    }
    #endregion

    #region CodeInfo 12 - Macro
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
