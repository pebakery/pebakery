using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections;
using System.Text.RegularExpressions;
using System.Reflection;

namespace BakeryEngine
{
    using StringDictionary = Dictionary<string, string>;

    public enum Opcode
    {
        // Misc
        None = 0, Comment, Error, Unknown,
        // File
        CopyOrExpand, DirCopy, DirDelete, DirMove, DirMake, Expand, FileCopy, FileDelete, FileRename, FileMove, FileCreateBlank, FileExtractByte,
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
        System, ShellExecute,
        // Branch
        Run, Exec, If, Else, Loop, End,
        // Control
        Set, GetParam, PackParam, AddVariables, Exit, Halt, Wait, Beep
    }

    public enum NextCommand
    {
        None = 0,
        Next, // Noremal case : execute next command
        Last, // Last command of section
        Jump, // In Run command : execute target command
        IgnoreUntilEnd, // Begin not executed : ignore until find End
    }

    /// <summary>
    /// Class to hold info of commands
    /// </summary>
    public class BakeryCommand
    {
        private string rawCode;
        public string RawCode
        {
            get { return rawCode; }
        }
        private Opcode opcode;
        public Opcode Opcode
        {
            get { return opcode; }
        }
        private string[] operands;
        public string[] Operands 
        {
            get { return operands; }
        }
        private BakeryCommand subCommand;
        public BakeryCommand SubCommand
        {
            get { return subCommand; }
        }
        private CommandAddress address;
        public CommandAddress Address
        {
            get { return address; }
        }
        private int sectionDepth;
        public int SectionDepth
        {
            get { return sectionDepth; }
            set { sectionDepth = value; }
        }

        /// <summary>
        /// Hold command information.
        /// </summary>
        /// <param name="opcode"></param>
        /// <param name="operands"></param>
        /// <param name="optional"></param>
        public BakeryCommand(string rawCode, Opcode opcode, string[] operands)
        {
            this.rawCode = rawCode;
            this.opcode = opcode;
            this.operands = operands;
            this.subCommand = null;
            this.sectionDepth = 0;
        }

        /// <summary>
        /// Hold command information, with sub command.
        /// </summary>
        /// <param name="rawCode"></param>
        /// <param name="opcode"></param>
        /// <param name="operands"></param>
        /// <param name="subCommand"></param>
        public BakeryCommand(string rawCode, Opcode opcode, string[] operands, int sectionDepth)
        {
            this.rawCode = rawCode;
            this.opcode = opcode;
            this.operands = operands;
            this.sectionDepth = sectionDepth;
        }

        /// <summary>
        /// Hold command information, with sub command.
        /// </summary>
        /// <param name="rawCode"></param>
        /// <param name="opcode"></param>
        /// <param name="operands"></param>
        /// <param name="subCommand"></param>
        public BakeryCommand(string rawCode, Opcode opcode, string[] operands, BakeryCommand subCommand)
        {
            this.rawCode = rawCode;
            this.opcode = opcode;
            this.operands = operands;
            this.subCommand = subCommand;
            this.sectionDepth = 0;
        }

        /// <summary>
        /// Hold command information, with address
        /// </summary>
        /// <param name="opcode"></param>
        /// <param name="operands"></param>
        /// <param name="optional"></param>
        public BakeryCommand(string rawCode, Opcode opcode, string[] operands, CommandAddress address)
        {
            this.rawCode = rawCode;
            this.opcode = opcode;
            this.operands = operands;
            this.address = address;
            this.subCommand = null;
            this.sectionDepth = 0;
        }

        /// <summary>
        /// Hold command information, with sub command.
        /// </summary>
        /// <param name="rawCode"></param>
        /// <param name="opcode"></param>
        /// <param name="operands"></param>
        /// <param name="subCommand"></param>
        public BakeryCommand(string rawCode, Opcode opcode, string[] operands, BakeryCommand subCommand, int sectionDepth)
        {
            this.rawCode = rawCode;
            this.opcode = opcode;
            this.operands = operands;
            this.subCommand = subCommand;
            this.sectionDepth = sectionDepth;
        }

        /// <summary>
        /// Hold command information, with address
        /// </summary>
        /// <param name="opcode"></param>
        /// <param name="operands"></param>
        /// <param name="optional"></param>
        public BakeryCommand(string rawCode, Opcode opcode, string[] operands, CommandAddress address, int sectionDepth)
        {
            this.rawCode = rawCode;
            this.opcode = opcode;
            this.operands = operands;
            this.address = address;
            this.subCommand = null;
            this.sectionDepth = sectionDepth;
        }

        /// <summary>
        /// Hold command information, with address and subcommand
        /// </summary>
        /// <param name="opcode"></param>
        /// <param name="operands"></param>
        /// <param name="optional"></param>
        public BakeryCommand(string rawCode, Opcode opcode, string[] operands, CommandAddress address, BakeryCommand subCommand)
        {
            this.rawCode = rawCode;
            this.opcode = opcode;
            this.operands = operands;
            this.address = address;
            this.subCommand = subCommand;
        }

        /// <summary>
        /// Hold command information, with address and subcommand
        /// </summary>
        /// <param name="opcode"></param>
        /// <param name="operands"></param>
        /// <param name="optional"></param>
        public BakeryCommand(string rawCode, Opcode opcode, string[] operands, CommandAddress address, BakeryCommand subCommand, int sectionDepth)
        {
            this.rawCode = rawCode;
            this.opcode = opcode;
            this.operands = operands;
            this.address = address;
            this.subCommand = subCommand;
            this.sectionDepth = sectionDepth;
        }

        public override string ToString()
        {
            string str = string.Concat("[", this.rawCode, "]\n", this.opcode.ToString());
            foreach (string operand in this.operands)
                str = string.Concat(str, "_", operand);

            return str;
        }
    }

    /// <summary>
    /// Exception used in BakeryEngine::ParseCommand
    /// </summary>
    public class InvalidCommandException : Exception
    {
        public InvalidCommandException() { }
        public InvalidCommandException(string message) : base(message) { }
        public InvalidCommandException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Exception used in BakeryEngine::ParseCommand
    /// </summary>
    public class InvalidOpcodeException : Exception
    {
        private BakeryCommand command = null;
        public BakeryCommand Command
        {
            get { return command; }
        }
        public InvalidOpcodeException() { }
        public InvalidOpcodeException(string message) : base(message) { }
        public InvalidOpcodeException(BakeryCommand command) { }
        public InvalidOpcodeException(string message, BakeryCommand command) : base(message) { this.command = command; }
        public InvalidOpcodeException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Exception used in BakeryEngine::ParseCommand
    /// </summary>
    public class InvalidOperandException : Exception
    {
        private BakeryCommand command = null;
        public BakeryCommand Command
        {
            get { return command; }
        }
        public InvalidOperandException() { }
        public InvalidOperandException(string message) : base(message) { }
        public InvalidOperandException(BakeryCommand command) { }
        public InvalidOperandException(string message, BakeryCommand command) : base(message) { this.command = command; }
        public InvalidOperandException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Exception used in BakeryEngine::ParseCommand
    /// </summary>
    public class InternalParseException : Exception
    {
        public InternalParseException() { }
        public InternalParseException(string message) : base(message) { }
        public InternalParseException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Exception used in BakerOperations
    /// </summary>
    public class InternalUnknownException : Exception
    {
        public InternalUnknownException() { }
        public InternalUnknownException(string message) : base(message) { }
        public InternalUnknownException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Struct to point command's address
    /// </summary>
    public struct CommandAddress
    { // Return address format = <Section>'s <n'th line>
        public PluginSection section;
        public int line;
        public int secLength;
        public CommandAddress(PluginSection section, int line, int secLength)
        {
            this.section = section;
            this.line = line;
            this.secLength = secLength;
        }
    }

    /// <summary>
    /// Interpreter of raw codes
    /// </summary>
    public partial class BakeryEngine
    {
        // Fields used globally
        private Plugin[] plugins;
        private BakeryVariables variables;
        private Logger logger;

        // Fields : Engine's state (plugins)
        private int curPluginIdx; // currentPluginIndex

        // Fields : Engine's state (in-plugin)
        private BakeryCommand currentCommand;
        private string[] currentSectionParams;
        private CommandAddress nextCommand; // ProgramCounter
        private Stack<CommandAddress> returnAddress;

        // Enum
        private enum ParseState { Normal, Merge }

        // Constructors
        public BakeryEngine(Plugin[] plugins, Logger logger)
        {
            InternalConstructor(plugins, 0, logger);
        }

        public BakeryEngine(Plugin[] plugins, int pluginIndex, Logger logger)
        {
            InternalConstructor(plugins, pluginIndex, logger);
        }

        private void InternalConstructor(Plugin[] plugins, int pluginIndex, Logger logger)
        {
            this.plugins = plugins;
            this.logger = logger;
            this.variables = new BakeryVariables();

            LoadDefaultGlobalVariables();
            curPluginIdx = pluginIndex;
            currentCommand = null;
            returnAddress = new Stack<CommandAddress>();
            currentSectionParams = new string[0];
        }

        // Methods
        /// <summary>
        /// Print debug information
        /// </summary>
        public void Debug()
        {
            try
            {
                // Variables
                Console.WriteLine(variables);
            }
            catch (Exception e)
            {
                Console.WriteLine("[ERR]\n{0}", e.ToString());
            }
        }

        private void DisplayOperation(BakeryCommand cmd)
        {
            for (int i = 0; i < cmd.SectionDepth; i++)
                Console.Write("  ");
            Console.WriteLine(cmd.RawCode);
        }

        private void LoadDefaultGlobalVariables()
        {
            PEBakeryInfo info = new PEBakeryInfo();
            // BaseDir
            variables.GlobalSetValue("BaseDir", info.BaseDir);
            // Version
            variables.GlobalSetValue("Version", info.Ver.Build.ToString());
            // Build
            variables.GlobalSetValue("Build", string.Format("{0:yyyy-MM-dd HH:mm}", info.Ver.ToString()));
        }

        private void LoadDefaultPluginVariables()
        {
            // ScriptFile, PluginFile
            variables.LocalSetValue("PluginFile", plugins[curPluginIdx].FileName);
            variables.LocalSetValue("ScriptFile", plugins[curPluginIdx].FileName);
            variables.LocalAddVariables(plugins[curPluginIdx].Sections["Variables"]);
        }

        /// <summary>
        /// Run an plugin
        /// </summary>
        public void RunPlugin()
        {
            LoadDefaultPluginVariables();
            PluginSection section = plugins[curPluginIdx].Sections["Process"];
            nextCommand = new CommandAddress(section, 0, section.SecLines.Length);
            RunCommands();
            return;
        }

        /// <summary>
        /// Run array of commands.
        /// </summary>
        /// <param name="nextCommand"></param>
        private void RunCommands()
        {
            while (true)
            {
                if (!(nextCommand.line < nextCommand.secLength)) // End of section
                {
                    currentSectionParams = new string[0];
                    BakeryCommand logCmd = new BakeryCommand("End of section", Opcode.None, new string[0], returnAddress.Count);
                    string logMsg = string.Concat("Section '", nextCommand.section.SecName, "' End");
                    logger.Write(new LogInfo(logCmd, logMsg, LogState.Infomation));
                    try
                    {
                        nextCommand = returnAddress.Pop();
                        if (!(nextCommand.line < nextCommand.secLength)) // Is return address end of section?
                            continue;
                    }
                    catch (InvalidOperationException)
                    { // The Stack<T> is empty, terminate function
                        break;
                    } 
                }

                // Fetch instructions
                int i = nextCommand.line;
                string rawCode = nextCommand.section.SecLines[i].Trim();

                try
                {
                    currentCommand = ParseCommand(rawCode, new CommandAddress(nextCommand.section, i, nextCommand.secLength));
                    
                    try
                    {
                        ExecuteCommand(currentCommand, logger);
                    }
                    catch (InvalidOpcodeException e)
                    {
                        logger.Write(new LogInfo(e.Command, e.Message, LogState.Error));
                    }
                }
                catch (InvalidOpcodeException e)
                {
                    currentCommand = new BakeryCommand(rawCode, Opcode.Unknown, new string[0]);
                    logger.Write(new LogInfo(e.Command, e.Message, LogState.Error));
                }
                catch (InvalidOperandException e)
                {
                    currentCommand = new BakeryCommand(rawCode, Opcode.Unknown, new string[0]);
                    logger.Write(new LogInfo(e.Command, e.Message, LogState.Error));
                }
                nextCommand.line += 1;
            }

            logger.WriteVariables(variables);
        }

        /// <summary>
        /// Execute one command.
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="logger"></param>
        private void ExecuteCommand(BakeryCommand cmd, Logger logger)
        {
            LogInfo log = null;
            LogInfo[] logs = null;

            DisplayOperation(cmd);
            try
            {
                switch (cmd.Opcode)
                {
                    // File
                    case Opcode.FileCopy:
                        logs = this.FileCopy(cmd);
                        break;
                    case Opcode.FileDelete:
                        logs = this.FileDelete(cmd);
                        break;
                    case Opcode.FileRename:
                    case Opcode.FileMove:
                        logs = this.FileMove(cmd);
                        break;
                    case Opcode.FileCreateBlank:
                        logs = this.FileCreateBlank(cmd);
                        break;
                    // Registry
                    // Text
                    case Opcode.TXTAddLine:
                        logs = this.TXTAddLine(cmd);
                        break;
                    // INI
                    // Network
                    // Attach
                    // UI
                    // StringFormat
                    // System
                    // Branch
                    case Opcode.Run:
                    case Opcode.Exec:
                        logs = this.RunExec(cmd);
                        break;
                    // Control
                    case Opcode.Set:
                        logs = this.Set(cmd);
                        break;
                    case Opcode.None:
                        log = new LogInfo(cmd, "NOP", LogState.None);
                        break;
                    case Opcode.Comment:
                        log = new LogInfo(cmd, "Comment", LogState.Ignore);
                        break;
                    default:
                        throw new InvalidOpcodeException("Cannot execute \'" + cmd.Opcode.ToString() + "\' command", cmd);
                }
            }
            catch (Exception e)
            {
                logger.Write(new LogInfo(cmd, string.Concat(e.GetType(), ": ", Helper.RemoveLastNewLine(e.Message)), LogState.Error));
            }
            

            if (log != null)
                logger.Write(log);
            if (logs != null)
                logger.Write(logs);
        }

        /// <summary>
        /// Parse raw command in string into BakeryCommand instance.
        /// </summary>
        /// <param name="rawCode"></param>
        /// <returns></returns>
        private BakeryCommand ParseCommand(string rawCode, CommandAddress address)
        {
            Opcode opcode = Opcode.None;
            ArrayList operandList = new ArrayList();

            // Remove whitespace of rawCode's start and end
            rawCode = rawCode.Trim();

            // Check if rawCode is Empty
            if (string.Equals(rawCode, string.Empty))
                return new BakeryCommand(string.Empty, Opcode.None, new string[0], address, returnAddress.Count);

            // Comment Format : starts with '//' or '#', ';'
            if (rawCode.Substring(0, 2) == "//" || rawCode.Substring(0, 1) == "#" || rawCode.Substring(0, 1) == ";")
            {
                return new BakeryCommand(rawCode, Opcode.Comment, new string[0], address, returnAddress.Count);
            }

            // Splice with spaces
            string[] slices = rawCode.Split(',');

            // Parse opcode
            try
            {
                // https://msdn.microsoft.com/ko-kr/library/essfb559(v=vs.110).aspx
                Opcode opcodeValue = (Opcode)Enum.Parse(typeof(Opcode), slices[0].Trim(), true);
                if (Enum.IsDefined(typeof(Opcode), opcodeValue))
                {
                    if (opcodeValue != Opcode.None && opcodeValue != Opcode.Comment)
                        opcode = opcodeValue;
                    else
                        throw new InvalidOpcodeException("Unknown command \'" + slices[0].Trim() + "\'", new BakeryCommand(rawCode, Opcode.Unknown, new string[0], address));
                }
                else
                    throw new InvalidOpcodeException("Unknown command \'" + slices[0].Trim() + "\'", new BakeryCommand(rawCode, Opcode.Unknown, new string[0], address));
            }
            catch (ArgumentException)
            {
                throw new InvalidOpcodeException("Unknown command \'" + slices[0].Trim() + "\'", new BakeryCommand(rawCode, Opcode.Unknown, new string[0], address));
            } // Do nothing
            
            // Check doublequote's occurence - must be 2n
            if (Helper.CountStringOccurrences(rawCode, "\"") % 2 == 1)
                throw new InvalidCommandException("number of doublequotes must be times of 2");

            /// Parse operand
            ParseState state = ParseState.Normal;
            string tmpStr = string.Empty;

            for (int i = 1; i < slices.Length; i++)
            {
                // Remove whitespace
                slices[i] = slices[i].Trim();

                // Check if operand is doublequoted
                int idx = slices[i].IndexOf("\"");
                if (idx == -1) // Do not have doublequote
                {
                    switch(state)
                    {
                        case ParseState.Normal: // Add to operand
                            operandList.Add(slices[i]);
                            break;
                        case ParseState.Merge:
                            tmpStr = string.Concat(tmpStr, ",", slices[i]); 
                            break;
                        default:
                            throw new InternalParseException();
                    }
                }
                else if (idx == 0) // Startes with doublequote
                { // Merge this operand with next operand
                    switch (state)
                    {
                        case ParseState.Normal: // Add to operand
                            if (slices[i].IndexOf("\"", idx+1) != -1) // This operand starts and end with doublequote
                            { // Ex) FileCopy,"1 2.dll",34.dll
                                operandList.Add(slices[i].Substring(1, slices[i].Length-2)); // Remove doublequote
                            }
                            else
                            {
                                state = ParseState.Merge;
                                tmpStr = string.Concat(slices[i].Substring(1)); // Remove doublequote
                            }
                            break;
                        case ParseState.Merge:
                            throw new InvalidOperandException();
                        default:
                            throw new InternalParseException();
                    }
                }
                else if (idx == slices[i].Length-1) // Endes with doublequote
                {
                    switch (state)
                    {
                        case ParseState.Normal: // Add to operand
                            throw new InvalidOperandException();
                        case ParseState.Merge:
                            state = ParseState.Normal;
                            tmpStr = string.Concat(tmpStr, ",", slices[i].Substring(0, slices[i].Length - 1)); // Remove doublequote
                            operandList.Add(tmpStr);
                            tmpStr = string.Empty;
                            break;
                        default:
                            throw new InternalParseException();
                    }
                }
                else // doublequote is in the middle
                {
                    throw new InvalidOperandException();
                }
            }

            // doublequote is not matched by two!
            if (state == ParseState.Merge)
                throw new InvalidOperandException("ParseState == Merge");

            string[] operands  = operandList.ToArray(typeof(string)) as string[];
            for (int i = 0; i < operands.Length; i++)
            {
                // Process Escape Characters
                operands[i] = operands[i].Replace(@"$#c", ",");
                operands[i] = operands[i].Replace(@"$#p", "%");
                operands[i] = operands[i].Replace(@"$#q", "\"");
                operands[i] = operands[i].Replace(@"$#s", " ");
                operands[i] = operands[i].Replace(@"$#t", "\t");
                operands[i] = operands[i].Replace(@"$#x", "\n");
                operands[i] = operands[i].Replace(@"$#z", "\x00\x00");
            }

            // forge BakeryCommand
            return new BakeryCommand(rawCode, opcode, operands, address, returnAddress.Count);
        }
    }
}
