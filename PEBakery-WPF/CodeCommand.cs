using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PEBakery.Core
{
    using StringDictionary = Dictionary<string, string>;

    #region Opcode

    public enum Opcode
    {
        // Misc
        None = 0, Comment, Error, Unknown,
        // File
        CopyOrExpand, DirCopy, DirDelete, DirMove, DirMake, Expand, FileCopy, FileDelete, FileRename, FileMove, FileCreateBlank, FileByteExtract,
        // Registry
        RegHiveLoad, RegHiveUnload, RegImport, RegWrite, RegRead, RegDelete, RegWriteBin, RegReadBin, RegMulti,
        // Text, INI
        TXTAddLine, TXTReplace, TXTDelLine, TXTDelSpaces, TXTDelEmptyLines,
        INIWrite, INIRead, INIDelete, INIAddSection, INIDeleteSection, INIWriteTextLine, INIMerge,
        // Network
        WebGet, WebGetIfNotExist,
        // Attach, Interface
        ExtractFile, ExtractAndRun, ExtractAllFiles, ExtractAllFilesIfNotExist, Encode,
        Visible,
        // UI
        Message, Echo, Retrieve,
        // StringFormat
        StrFormat,
        // System
        System, ShellExecute, ShellExecuteEx, ShellExecuteDelete,
        // Branch
        Run, Exec, Loop,
        // Branch - Will be compiled
        If, Else, Begin, End,
        // Branch - Compiled microcode
        IfCompact, ElseCompact, Link,
        // Control
        Set, GetParam, PackParam, AddVariables, Exit, Halt, Wait, Beep,
        // External Macro
        Macro,
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

    /// <summary>
    /// Class to hold info of commands
    /// </summary>
    public class CodeCommand
    {
        public string RawCode;
        public Opcode Opcode;
        public string MacroOpcode;
        public List<string> Operands;
        public CodeCommandInfo Info; // TODO: 언제 이걸 저 밑의 생성자 리스트들에 전부 더하냐...
        public SectionAddress Address;
        public int Depth;
        public List<CodeCommand> Link;

        public CodeCommand(string rawCode, Opcode opcode, List<string> operands)
        { InternalConstructor(rawCode, opcode, null, operands, new SectionAddress(), 0, null); }
        public CodeCommand(string rawCode, Opcode opcode, List<string> operands, int depth)
        { InternalConstructor(rawCode, opcode, null, operands, new SectionAddress(), depth, null); }
        public CodeCommand(string rawCode, Opcode opcode, List<string> operands, SectionAddress address)
        { InternalConstructor(rawCode, opcode, null, operands, address, 0, null); }
        public CodeCommand(string rawCode, Opcode opcode, List<string> operands, SectionAddress address, int depth)
        { InternalConstructor(rawCode, opcode, null, operands, address, depth, null); }

        public CodeCommand(Opcode opcode, List<string> operands)
        { InternalConstructor(ForgeRawCode(opcode, operands), opcode, null, operands, new SectionAddress(), 0, null); }
        public CodeCommand(Opcode opcode, List<string> operands, int depth)
        { InternalConstructor(ForgeRawCode(opcode, operands), opcode, null, operands, new SectionAddress(), depth, null); }
        public CodeCommand(Opcode opcode, List<string> operands, SectionAddress address)
        { InternalConstructor(ForgeRawCode(opcode, operands), opcode, null, operands, address, 0, null); }
        public CodeCommand(Opcode opcode, List<string> operands, SectionAddress address, int depth)
        { InternalConstructor(ForgeRawCode(opcode, operands), opcode, null, operands, address, depth, null); }

        public CodeCommand(string rawCode, Opcode opcode, List<string> operands, List<CodeCommand> link)
        { InternalConstructor(rawCode, opcode, null, operands, new SectionAddress(), 0, link); }
        public CodeCommand(string rawCode, Opcode opcode, List<string> operands, int depth, List<CodeCommand> link)
        { InternalConstructor(rawCode, opcode, null, operands, new SectionAddress(), depth, link); }
        public CodeCommand(string rawCode, Opcode opcode, List<string> operands, SectionAddress address, List<CodeCommand> link)
        { InternalConstructor(rawCode, opcode, null, operands, address, 0, link); }
        public CodeCommand(string rawCode, Opcode opcode, List<string> operands, SectionAddress address, int depth, List<CodeCommand> link)
        { InternalConstructor(rawCode, opcode, null, operands, address, depth, link); }

        public CodeCommand(Opcode opcode, List<string> operands, List<CodeCommand> link)
        { InternalConstructor(ForgeRawCode(opcode, operands), opcode, null, operands, new SectionAddress(), 0, link); }
        public CodeCommand(Opcode opcode, List<string> operands, int depth, List<CodeCommand> link)
        { InternalConstructor(ForgeRawCode(opcode, operands), opcode, null, operands, new SectionAddress(), depth, link); }
        public CodeCommand(Opcode opcode, List<string> operands, SectionAddress address, List<CodeCommand> link)
        { InternalConstructor(ForgeRawCode(opcode, operands), opcode, null, operands, address, 0, link); }
        public CodeCommand(Opcode opcode, List<string> operands, SectionAddress address, int depth, List<CodeCommand> link)
        { InternalConstructor(ForgeRawCode(opcode, operands), opcode, null, operands, address, depth, link); }

        public CodeCommand(string rawCode, string macroOpcode, List<string> operands)
        { InternalConstructor(rawCode, Opcode.Macro, macroOpcode, operands, new SectionAddress(), 0, null); }
        public CodeCommand(string rawCode, string macroOpcode, List<string> operands, int depth)
        { InternalConstructor(rawCode, Opcode.Macro, macroOpcode, operands, new SectionAddress(), depth, null); }
        public CodeCommand(string rawCode, string macroOpcode, List<string> operands, SectionAddress address)
        { InternalConstructor(rawCode, Opcode.Macro, macroOpcode, operands, address, 0, null); }
        public CodeCommand(string rawCode, string macroOpcode, List<string> operands, SectionAddress address, int depth)
        { InternalConstructor(rawCode, Opcode.Macro, macroOpcode, operands, address, depth, null); }

        public CodeCommand(string macroOpcode, List<string> operands)
        { InternalConstructor(ForgeRawCode(macroOpcode, operands), Opcode.Macro, macroOpcode, operands, new SectionAddress(), 0, null); }
        public CodeCommand(string macroOpcode, List<string> operands, int depth)
        { InternalConstructor(ForgeRawCode(macroOpcode, operands), Opcode.Macro, macroOpcode, operands, new SectionAddress(), depth, null); }
        public CodeCommand(string macroOpcode, List<string> operands, SectionAddress address)
        { InternalConstructor(ForgeRawCode(macroOpcode, operands), Opcode.Macro, macroOpcode, operands, address, 0, null); }
        public CodeCommand(string macroOpcode, List<string> operands, SectionAddress address, int depth)
        { InternalConstructor(ForgeRawCode(macroOpcode, operands), Opcode.Macro, macroOpcode, operands, address, depth, null); }

        public CodeCommand(string rawCode, string macroOpcode, List<string> operands, List<CodeCommand> link)
        { InternalConstructor(rawCode, Opcode.Macro, macroOpcode, operands, new SectionAddress(), 0, link); }
        public CodeCommand(string rawCode, string macroOpcode, List<string> operands, int depth, List<CodeCommand> link)
        { InternalConstructor(rawCode, Opcode.Macro, macroOpcode, operands, new SectionAddress(), depth, link); }
        public CodeCommand(string rawCode, string macroOpcode, List<string> operands, SectionAddress address, List<CodeCommand> link)
        { InternalConstructor(rawCode, Opcode.Macro, macroOpcode, operands, address, 0, link); }
        public CodeCommand(string rawCode, string macroOpcode, List<string> operands, SectionAddress address, int depth, List<CodeCommand> link)
        { InternalConstructor(rawCode, Opcode.Macro, macroOpcode, operands, address, depth, link); }

        public CodeCommand(string macroOpcode, List<string> operands, List<CodeCommand> link)
        { InternalConstructor(ForgeRawCode(macroOpcode, operands), Opcode.Macro, macroOpcode, operands, new SectionAddress(), 0, link); }
        public CodeCommand(string macroOpcode, List<string> operands, int depth, List<CodeCommand> link)
        { InternalConstructor(ForgeRawCode(macroOpcode, operands), Opcode.Macro, macroOpcode, operands, new SectionAddress(), depth, link); }
        public CodeCommand(string macroOpcode, List<string> operands, SectionAddress address, List<CodeCommand> link)
        { InternalConstructor(ForgeRawCode(macroOpcode, operands), Opcode.Macro, macroOpcode, operands, address, 0, link); }
        public CodeCommand(string macroOpcode, List<string> operands, SectionAddress address, int depth, List<CodeCommand> link)
        { InternalConstructor(ForgeRawCode(macroOpcode, operands), Opcode.Macro, macroOpcode, operands, address, depth, link); }

        public void InternalConstructor(string rawCode, Opcode opcode, string macroOpcode, List<string> operands, SectionAddress address, int depth, List<CodeCommand> link)
        {
            this.RawCode = rawCode;
            this.Opcode = opcode;
            this.Operands = operands;
            this.MacroOpcode = macroOpcode;
            this.Address = address;
            this.Depth = depth;
            this.Link = link;
        }

        /// <summary>
        /// Return RawCode, built from opcode and operand itself
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return ForgeRawCode();
        }

        public string ForgeRawCode()
        {
            if (Opcode == Opcode.Macro)
                return CodeCommand.ForgeRawCode(MacroOpcode, Operands);
            else
                return CodeCommand.ForgeRawCode(Opcode, Operands);
        }

        public static string ForgeRawCode(Opcode opcode, List<string> operands)
        {
            StringBuilder builder = new StringBuilder(opcode.ToString());
            for (int i = 0; i < operands.Count; i++)
            {
                builder.Append(",");
                builder.Append(DoublequoteString(operands[i]));
            }

            return builder.ToString();
        }

        public static string ForgeRawCode(string macroOpcode, List<string> operands)
        {
            StringBuilder builder = new StringBuilder(macroOpcode);
            for (int i = 0; i < operands.Count; i++)
            {
                builder.Append(",");
                builder.Append(DoublequoteString(operands[i]));
            }

            return builder.ToString();
        }

        public static string DoublequoteString(string str)
        {
            if (str.Contains(" "))
                return "\"" + str + "\"";
            else
                return str;
        }
    }

    #endregion

    #region CodeCommandInfo

    public class CodeCommandInfo
    {
        
    }

    #region CodeCommandInfo - File
    public class CodeInfo_Expand : CodeCommandInfo
    {
        public string SrcCab;
        public string DestDir;
        public bool IsSingleFile;
        public string SingleFile;
        public bool Preserve;
        public bool NoWarn;
    }

    public class CodeInfo_FileCopy : CodeCommandInfo
    {
        public string SrcFile;
        public string DestPath;
        public bool Preserve;
        public bool NoWarn;
        public bool NoRec;
    }
    #endregion

    #endregion
}
