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

    #region Command

    /// <summary>
    /// Class to hold info of commands
    /// </summary>
    public class Command
    {
        public string RawCode;
        public Opcode Opcode;
        public string MacroOpcode;
        public List<string> Operands;
        public SectionAddress Address;
        public int Depth;
        public List<Command> Link;

        public Command(string rawCode, Opcode opcode, List<string> operands)
        { InternalConstructor(rawCode, opcode, null, operands, new SectionAddress(), 0, null); }
        public Command(string rawCode, Opcode opcode, List<string> operands, int depth)
        { InternalConstructor(rawCode, opcode, null, operands, new SectionAddress(), depth, null); }
        public Command(string rawCode, Opcode opcode, List<string> operands, SectionAddress address)
        { InternalConstructor(rawCode, opcode, null, operands, address, 0, null); }
        public Command(string rawCode, Opcode opcode, List<string> operands, SectionAddress address, int depth)
        { InternalConstructor(rawCode, opcode, null, operands, address, depth, null); }

        public Command(Opcode opcode, List<string> operands)
        { InternalConstructor(ForgeRawCode(opcode, operands), opcode, null, operands, new SectionAddress(), 0, null); }
        public Command(Opcode opcode, List<string> operands, int depth)
        { InternalConstructor(ForgeRawCode(opcode, operands), opcode, null, operands, new SectionAddress(), depth, null); }
        public Command(Opcode opcode, List<string> operands, SectionAddress address)
        { InternalConstructor(ForgeRawCode(opcode, operands), opcode, null, operands, address, 0, null); }
        public Command(Opcode opcode, List<string> operands, SectionAddress address, int depth)
        { InternalConstructor(ForgeRawCode(opcode, operands), opcode, null, operands, address, depth, null); }

        public Command(string rawCode, Opcode opcode, List<string> operands, List<Command> link)
        { InternalConstructor(rawCode, opcode, null, operands, new SectionAddress(), 0, link); }
        public Command(string rawCode, Opcode opcode, List<string> operands, int depth, List<Command> link)
        { InternalConstructor(rawCode, opcode, null, operands, new SectionAddress(), depth, link); }
        public Command(string rawCode, Opcode opcode, List<string> operands, SectionAddress address, List<Command> link)
        { InternalConstructor(rawCode, opcode, null, operands, address, 0, link); }
        public Command(string rawCode, Opcode opcode, List<string> operands, SectionAddress address, int depth, List<Command> link)
        { InternalConstructor(rawCode, opcode, null, operands, address, depth, link); }

        public Command(Opcode opcode, List<string> operands, List<Command> link)
        { InternalConstructor(ForgeRawCode(opcode, operands), opcode, null, operands, new SectionAddress(), 0, link); }
        public Command(Opcode opcode, List<string> operands, int depth, List<Command> link)
        { InternalConstructor(ForgeRawCode(opcode, operands), opcode, null, operands, new SectionAddress(), depth, link); }
        public Command(Opcode opcode, List<string> operands, SectionAddress address, List<Command> link)
        { InternalConstructor(ForgeRawCode(opcode, operands), opcode, null, operands, address, 0, link); }
        public Command(Opcode opcode, List<string> operands, SectionAddress address, int depth, List<Command> link)
        { InternalConstructor(ForgeRawCode(opcode, operands), opcode, null, operands, address, depth, link); }

        public Command(string rawCode, string macroOpcode, List<string> operands)
        { InternalConstructor(rawCode, Opcode.Macro, macroOpcode, operands, new SectionAddress(), 0, null); }
        public Command(string rawCode, string macroOpcode, List<string> operands, int depth)
        { InternalConstructor(rawCode, Opcode.Macro, macroOpcode, operands, new SectionAddress(), depth, null); }
        public Command(string rawCode, string macroOpcode, List<string> operands, SectionAddress address)
        { InternalConstructor(rawCode, Opcode.Macro, macroOpcode, operands, address, 0, null); }
        public Command(string rawCode, string macroOpcode, List<string> operands, SectionAddress address, int depth)
        { InternalConstructor(rawCode, Opcode.Macro, macroOpcode, operands, address, depth, null); }

        public Command(string macroOpcode, List<string> operands)
        { InternalConstructor(ForgeRawCode(macroOpcode, operands), Opcode.Macro, macroOpcode, operands, new SectionAddress(), 0, null); }
        public Command(string macroOpcode, List<string> operands, int depth)
        { InternalConstructor(ForgeRawCode(macroOpcode, operands), Opcode.Macro, macroOpcode, operands, new SectionAddress(), depth, null); }
        public Command(string macroOpcode, List<string> operands, SectionAddress address)
        { InternalConstructor(ForgeRawCode(macroOpcode, operands), Opcode.Macro, macroOpcode, operands, address, 0, null); }
        public Command(string macroOpcode, List<string> operands, SectionAddress address, int depth)
        { InternalConstructor(ForgeRawCode(macroOpcode, operands), Opcode.Macro, macroOpcode, operands, address, depth, null); }

        public Command(string rawCode, string macroOpcode, List<string> operands, List<Command> link)
        { InternalConstructor(rawCode, Opcode.Macro, macroOpcode, operands, new SectionAddress(), 0, link); }
        public Command(string rawCode, string macroOpcode, List<string> operands, int depth, List<Command> link)
        { InternalConstructor(rawCode, Opcode.Macro, macroOpcode, operands, new SectionAddress(), depth, link); }
        public Command(string rawCode, string macroOpcode, List<string> operands, SectionAddress address, List<Command> link)
        { InternalConstructor(rawCode, Opcode.Macro, macroOpcode, operands, address, 0, link); }
        public Command(string rawCode, string macroOpcode, List<string> operands, SectionAddress address, int depth, List<Command> link)
        { InternalConstructor(rawCode, Opcode.Macro, macroOpcode, operands, address, depth, link); }

        public Command(string macroOpcode, List<string> operands, List<Command> link)
        { InternalConstructor(ForgeRawCode(macroOpcode, operands), Opcode.Macro, macroOpcode, operands, new SectionAddress(), 0, link); }
        public Command(string macroOpcode, List<string> operands, int depth, List<Command> link)
        { InternalConstructor(ForgeRawCode(macroOpcode, operands), Opcode.Macro, macroOpcode, operands, new SectionAddress(), depth, link); }
        public Command(string macroOpcode, List<string> operands, SectionAddress address, List<Command> link)
        { InternalConstructor(ForgeRawCode(macroOpcode, operands), Opcode.Macro, macroOpcode, operands, address, 0, link); }
        public Command(string macroOpcode, List<string> operands, SectionAddress address, int depth, List<Command> link)
        { InternalConstructor(ForgeRawCode(macroOpcode, operands), Opcode.Macro, macroOpcode, operands, address, depth, link); }

        public void InternalConstructor(string rawCode, Opcode opcode, string macroOpcode, List<string> operands, SectionAddress address, int depth, List<Command> link)
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
                return Command.ForgeRawCode(MacroOpcode, Operands);
            else
                return Command.ForgeRawCode(Opcode, Operands);
        }

        public static string ForgeRawCode(Opcode opcode, List<string> operands)
        {
            StringBuilder builder = new StringBuilder(opcode.ToString());
            for (int i = 0; i < operands.Count; i++)
            {
                if (operands[i].Contains(" "))
                {
                    builder.Append(",\"");
                    builder.Append(operands[i]);
                    builder.Append("\"");
                }
                else
                {
                    builder.Append(",");
                    builder.Append(operands[i]);
                }
            }

            return builder.ToString();
        }

        public static string ForgeRawCode(string macroOpcode, List<string> operands)
        {
            StringBuilder builder = new StringBuilder(macroOpcode);
            for (int i = 0; i < operands.Count; i++)
            {
                if (operands[i].Contains(" "))
                {
                    builder.Append(",\"");
                    builder.Append(operands[i]);
                    builder.Append("\"");
                }
                else
                {
                    builder.Append(",");
                    builder.Append(operands[i]);
                }
            }

            return builder.ToString();
        }
    }

    #endregion

    #region Command State

    public class CmdState
    {
        
    }

    #region Command State - File
    public class CmdExpandState : CmdState
    {
        public string SrcCab;
        public string DestDir;
        public bool IsSingleFile;
        public string SingleFile;
        public bool Preserve;
        public bool NoWarn;
    }

    public class CmdFileCopyState : CmdState
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
