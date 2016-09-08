using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections;
using System.Collections.Concurrent;

namespace BakeryEngine
{
    using PluginDictionary = ConcurrentDictionary<int, Plugin[]>;
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
        public Plugin plugin;
        public PluginSection section;
        public int line;
        public int secLength;
        public CommandAddress(Plugin plugin,PluginSection section, int line, int secLength)
        {
            this.plugin = plugin;
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
        private Project project;
        private BakeryVariables variables;
        private Logger logger;
        private bool runOnePlugin;

        // Fields : Engine's state
        private Plugin currentPlugin;
        private PluginAddress curPluginAddr;
        private BakeryCommand currentCommand;
        private string[] currentSectionParams;
        private CommandAddress nextCommand; // ProgramCounter
        private Stack<CommandAddress> returnAddress;

        // Properties
        private PluginCollection Plugins { get { return project.ActivePlugins; } }

        // Enum
        private enum ParseState { Normal, Merge }

        // Constructors
        public BakeryEngine(Project project, Logger logger)
        {
            InternalConstructor(project, project.MainPlugin, logger, false);
        }

        public BakeryEngine(Project project, Logger logger, bool runOnePlugin)
        {
            InternalConstructor(project, project.MainPlugin, logger, runOnePlugin);
        }

        public BakeryEngine(Project project, Logger logger, string entryPlugin)
        {
            InternalConstructor(project, project.ActivePlugins.SearchByFullPath(entryPlugin), logger, false);
        }

        public BakeryEngine(Project project, Logger logger, string entryPlugin, bool runOnePlugin)
        {
            InternalConstructor(project, project.ActivePlugins.SearchByFullPath(entryPlugin), logger, runOnePlugin);
        }

        /// <summary>
        /// Internel Constructor
        /// </summary>
        /// <param name="project">Project Instance</param>
        /// <param name="entryPlugin">Plugin to start</param>
        /// <param name="logger">Logger Instance</param>
        /// <param name="runOnePlugin">Run one plugin (entryPlugin) and exit</param>
        private void InternalConstructor(Project project, Plugin entryPlugin, Logger logger, bool runOnePlugin)
        {
            this.project = project; 
            this.logger = logger;
            this.variables = new BakeryVariables();
            this.runOnePlugin = runOnePlugin;

            LoadDefaultGlobalVariables();
            this.currentPlugin = entryPlugin;
            this.curPluginAddr = project.ActivePlugins.GetAddress(entryPlugin);
            this.currentCommand = null;
            this.returnAddress = new Stack<CommandAddress>();
            this.currentSectionParams = new string[0];
        }

        // Methods
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
            variables.SetValue(VarsType.Global, "BaseDir", info.BaseDir);
            // Version
            variables.SetValue(VarsType.Global, "Version", info.Ver.Build.ToString());
            // Build
            variables.SetValue(VarsType.Global, "Build", $"{info.Ver.ToString():yyyy-MM-dd HH:mm}");
            // ProjectDir
            variables.SetValue(VarsType.Global, "ProjectDir", project.ProjectRoot);
            // TargetDir
            variables.SetValue(VarsType.Global, "TargetDir", Path.Combine(info.BaseDir, "Target"));
            // ProjectTemp
            variables.SetValue(VarsType.Global, "ProjectTemp", Path.Combine(info.BaseDir, "Temp", project.ProjectName));
        }

        private void LoadDefaultPluginVariables()
        {
            // ScriptFile, PluginFile
            variables.SetValue(VarsType.Local, "PluginFile", currentPlugin.FullPath);
            variables.SetValue(VarsType.Local, "ScriptFile", currentPlugin.FullPath);

            // [Variables]
            VarsType type = VarsType.Local;
            if (currentPlugin == project.MainPlugin)
                type = VarsType.Global;
            logger.Write(LogState.Information, $"Processing section [Variables]", 0);
            variables.AddVariables(type, currentPlugin.Sections["Variables"]);
            foreach (var kv in (currentPlugin.Sections["Variables"].Get() as StringDictionary))
                logger.Write(LogState.Success, $"Var [%{kv.Key}%] set to [{kv.Value}]", 1);
            logger.Write(LogState.Information, $"End of section [Variables]", 0);
        }

        /// <summary>
        /// Run an plugin
        /// </summary>
        public void RunPlugin()
        {
            PluginSection section = currentPlugin.Sections["Process"];
            nextCommand = new CommandAddress(currentPlugin, section, 0, section.Count);
            curPluginAddr = Plugins.GetAddress(currentPlugin);
            logger.Write($"\nProcessing plugin [{currentPlugin.ShortPath}] ({Plugins.GetFullIndex(curPluginAddr)}/{Plugins.Count})");

            variables.ResetLocalVaribles();
            LoadDefaultPluginVariables();

            RunCommands();
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
                    logger.Write(LogState.Information, $"End of section [{nextCommand.section.SectionName}]", returnAddress.Count);
                    try
                    {
                        nextCommand = returnAddress.Pop();
                        if (!(nextCommand.line < nextCommand.secLength)) // Is return address end of section?
                            continue;
                    }
                    catch (InvalidOperationException)
                    { // The Stack<T> is empty, readed plugin's end
                        logger.Write(LogState.Information, $"End of plugin [{currentPlugin.ShortPath}]\n");
                        if (runOnePlugin) // Just run one plugin
                            break; // Work is done, so exit
                        try
                        {
                            variables.ResetLocalVaribles();
                            curPluginAddr = Plugins.GetNextAddress(curPluginAddr);
                            currentPlugin = Plugins.GetFromAddress(curPluginAddr);
                            PluginSection section = currentPlugin.Sections["Process"];
                            nextCommand = new CommandAddress(currentPlugin, section, 0, section.Count);
                            logger.Write($"Processing plugin [{currentPlugin.ShortPath}] ({Plugins.GetFullIndex(curPluginAddr)}/{Plugins.Count})");
                        }   
                        catch (EndOfPluginLevelException)
                        { // End of sectoin, so exit
                            break;
                        }
                    } 
                }

                // Fetch instructions
                int i = nextCommand.line;
                string rawCode = (nextCommand.section.Get() as string[])[i].Trim();

                try
                {
                    currentCommand = ParseCommand(rawCode, new CommandAddress(nextCommand.plugin, nextCommand.section, i, nextCommand.secLength));
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

            // DisplayOperation(cmd);
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
                    case Opcode.INIRead:
                        logs = this.INIRead(cmd);
                        break;
                    case Opcode.INIWrite:
                        logs = this.INIWrite(cmd);
                        break;
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
                        throw new InvalidOpcodeException($"Cannot execute [{cmd.Opcode.ToString()}] command", cmd);
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
            List<string> operandList = new List<string>();

            // Remove whitespace of rawCode's start and end
            rawCode = rawCode.Trim();

            // Check if rawCode is Empty
            if (string.Equals(rawCode, string.Empty))
                return new BakeryCommand(string.Empty, Opcode.None, new string[0], address, returnAddress.Count);

            // Comment Format : starts with '//' or '#', ';'
            if (rawCode.StartsWith("//") || rawCode.StartsWith("#") || rawCode.StartsWith(";"))
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
                        throw new InvalidOpcodeException($"Unknown command [{slices[0].Trim()}]", new BakeryCommand(rawCode, Opcode.Unknown, new string[0], address));
                }
                else
                    throw new InvalidOpcodeException($"Unknown command [{slices[0].Trim()}]", new BakeryCommand(rawCode, Opcode.Unknown, new string[0], address));
            }
            catch (ArgumentException)
            {
                throw new InvalidOpcodeException($"Unknown command [{slices[0].Trim()}]", new BakeryCommand(rawCode, Opcode.Unknown, new string[0], address));
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
                throw new InvalidOperandException("When parsing ends, ParseState must not be in state of Merge");

            string[] operands = operandList.ToArray();
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
