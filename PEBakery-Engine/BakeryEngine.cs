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
        System, ShellExecute, ShellExecuteEx, ShellExecuteDelete,
        // Branch
        Run, Exec, If, Begin, Else, Loop, End,
        // Control
        Set, GetParam, PackParam, AddVariables, Exit, Halt, Wait, Beep
    }

    /// <summary>
    /// Class to hold info of commands
    /// </summary>
    public class BakeryCommand
    {
        private string rawCode;
        private Opcode opcode;
        private string[] operands;
        private CommandAddress address;
        public int Depth;

        public string RawCode { get { return rawCode; } }
        public Opcode Opcode { get { return opcode; } }
        public string[] Operands { get { return operands; } }
        public CommandAddress Address { get { return address; } }

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
            this.Depth = 0;
        }

        /// <summary>
        /// Hold command information, with sub command.
        /// </summary>
        /// <param name="rawCode"></param>
        /// <param name="opcode"></param>
        /// <param name="operands"></param>
        /// <param name="subCommand"></param>
        public BakeryCommand(string rawCode, Opcode opcode, string[] operands, int depth)
        {
            this.rawCode = rawCode;
            this.opcode = opcode;
            this.operands = operands;
            this.Depth = depth;
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
            this.Depth = 0;
        }
        

        /// <summary>
        /// Hold command information, with address
        /// </summary>
        /// <param name="opcode"></param>
        /// <param name="operands"></param>
        /// <param name="optional"></param>
        public BakeryCommand(string rawCode, Opcode opcode, string[] operands, CommandAddress address, int depth)
        {
            this.rawCode = rawCode;
            this.opcode = opcode;
            this.operands = operands;
            this.address = address;
            this.Depth = depth;
        }

        public override string ToString()
        {
            return rawCode;
        }
    }

    public enum SubCommandType
    {
        System, Retrieve, StrFormat, IfCondition
    }

    /// <summary>
    /// Class to hold info of commands
    /// </summary>
    public class BakerySubCommand
    {
        private SubCommandType subCommandType;
        private Enum subOpcode;
        private string[] operands;
        // For IfCondition
        private bool? notFlag;

        public SubCommandType SubCommandType { get { return subCommandType; } }
        public Enum SubOpcode { get { return subOpcode; } }
        public string[] Operands { get { return operands; } }
        public bool NotFlag
        {
            get
            {
                if (notFlag == null)
                    throw new InvalidSubCommandException();
                else
                    return (bool)notFlag;
            }
            set
            {
                notFlag = value;
            }
        }

        public BakerySubCommand(SubCommandType subCommandType, Enum subOpcode, string[] operands)
        {
            this.subCommandType = subCommandType;
            this.subOpcode = subOpcode;
            this.operands = operands;
            this.notFlag = null;
        }

        public BakerySubCommand(SubCommandType subCommandType, Enum subOpcode, string[] operands, bool notFlag)
        {
            this.subCommandType = subCommandType;
            this.subOpcode = subOpcode;
            this.operands = operands;
            this.notFlag = notFlag;
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
    public class CriticalErrorException : Exception
    {
        private BakeryCommand cmd;
        public BakeryCommand Cmd { get { return cmd; } }
        public CriticalErrorException() { }
        public CriticalErrorException(string message) : base(message) { }
        public CriticalErrorException(BakeryCommand cmd) { this.cmd = cmd; }
        public CriticalErrorException(string message, BakeryCommand cmd) : base(message) { this.cmd = cmd; }
        public CriticalErrorException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Exception used in BakeryEngine::ParseCommand
    /// </summary>
    public class InvalidOpcodeException : Exception
    {
        private BakeryCommand cmd;
        public BakeryCommand Cmd { get { return cmd; } }
        public InvalidOpcodeException() { }
        public InvalidOpcodeException(string message) : base(message) { }
        public InvalidOpcodeException(BakeryCommand cmd) { this.cmd = cmd; }
        public InvalidOpcodeException(string message, BakeryCommand cmd) : base(message) { this.cmd = cmd; }
        public InvalidOpcodeException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Exception used in BakeryEngine::ParseCommand
    /// </summary>
    public class InvalidSubOpcodeException : Exception
    {
        private BakeryCommand cmd;
        public BakeryCommand Cmd { get { return cmd; } }
        public InvalidSubOpcodeException() { }
        public InvalidSubOpcodeException(string message) : base(message) { }
        public InvalidSubOpcodeException(BakeryCommand cmd) { this.cmd = cmd; }
        public InvalidSubOpcodeException(string message, BakeryCommand cmd) : base(message) { this.cmd = cmd; }
        public InvalidSubOpcodeException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Exception used in BakeryEngine::ParseCommand
    /// </summary>
    public class InvalidOperandException : Exception
    {
        private BakeryCommand cmd;
        public BakeryCommand Cmd { get { return cmd; } }
        public InvalidOperandException() { }
        public InvalidOperandException(string message) : base(message) { }
        public InvalidOperandException(BakeryCommand cmd) { this.cmd = cmd; }
        public InvalidOperandException(string message, BakeryCommand cmd) : base(message) { this.cmd = cmd; }
        public InvalidOperandException(string message, Exception inner) : base(message, inner) { }
    }

    public class InvalidLogFormatException : Exception
    {
        private BakeryCommand cmd;
        public BakeryCommand Cmd { get { return cmd; } }
        public InvalidLogFormatException() { }
        public InvalidLogFormatException(string message) : base(message) { }
        public InvalidLogFormatException(BakeryCommand cmd) { this.cmd = cmd; }
        public InvalidLogFormatException(string message, BakeryCommand cmd) : base(message) { this.cmd = cmd; }
        public InvalidLogFormatException(string message, Exception inner) : base(message, inner) { }
    }

    public class InvalidSubCommandException : Exception
    {
        private BakeryCommand cmd;
        public BakeryCommand Cmd { get { return cmd; } }
        public InvalidSubCommandException() { }
        public InvalidSubCommandException(string message) : base(message) { }
        public InvalidSubCommandException(BakeryCommand cmd) { this.cmd = cmd; }
        public InvalidSubCommandException(string message, BakeryCommand cmd) : base(message) { this.cmd = cmd; }
        public InvalidSubCommandException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Exception used in BakeryEngine::ParseCommand
    /// </summary>
    public class InvalidSubOperandException : Exception
    {
        private BakeryCommand cmd;
        public BakeryCommand Cmd { get { return cmd; } }
        private BakerySubCommand subCmd;
        public BakerySubCommand SubCmd { get { return subCmd; } }
        public InvalidSubOperandException(string message) : base(message) { }
        public InvalidSubOperandException(BakeryCommand cmd) { this.cmd = cmd; }
        public InvalidSubOperandException(BakeryCommand cmd, BakerySubCommand subCmd) { this.cmd = cmd; this.subCmd = subCmd; }
        public InvalidSubOperandException(string message, BakeryCommand cmd) : base(message) { this.cmd = cmd; }
        public InvalidSubOperandException(string message, BakeryCommand cmd, BakerySubCommand subCmd) : base(message) { this.cmd = cmd; this.subCmd = subCmd; }
        public InvalidSubOperandException(string message, Exception inner) : base(message, inner) { }
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

        public override bool Equals(object obj)
        {
            if (!(obj is CommandAddress))
                return false;

            CommandAddress addr = (CommandAddress)obj;

            bool result = true;
            if (plugin != addr.plugin || section != addr.section || line != addr.line || secLength != addr.secLength)
                result = false;
            return result;
        }

        public static bool operator ==(CommandAddress c1, CommandAddress c2)
        {
            return c1.Equals(c2);
        }

        public static bool operator !=(CommandAddress c1, CommandAddress c2)
        {
            return !c1.Equals(c2);
        }

        public override int GetHashCode()
        {
            return plugin.ShortPath.Length + section.SectionName.Length + (line ^ secLength);
        }
    }


    /// <summary>
    /// Interpreter of codes
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
        private int beginEndBlockCount;
        private int elseFlag; // 1, 0 : valid, -1 : invalid
        private int beginFlag; // 1, 0 : valid, -1 : invalid

        // Fields : System Commands
        private BakeryCommand onBuildExit;
        private BakeryCommand onPluginExit;

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
            this.variables = new BakeryVariables(logger);
            this.runOnePlugin = runOnePlugin;

            LoadDefaultGlobalVariables();
            logger.WriteGlobalVariables(variables);

            this.currentPlugin = entryPlugin;
            this.curPluginAddr = project.ActivePlugins.GetAddress(entryPlugin);
            this.currentCommand = null;
            this.returnAddress = new Stack<CommandAddress>();
            this.currentSectionParams = new string[0];
            this.beginEndBlockCount = 0;
            this.beginFlag = -1;
            this.elseFlag = -1;

            this.onBuildExit = null;
            this.onPluginExit = null;
        }

        // Methods
        private void DisplayOperation(BakeryCommand cmd)
        {
            for (int i = 0; i < cmd.Depth; i++)
                Console.Write("  ");
            Console.WriteLine(cmd.RawCode);
        }

        private void LoadDefaultGlobalVariables()
        {
            PEBakeryInfo info = new PEBakeryInfo();
            // BaseDir
            variables.SetValue(VarsType.Global, "BaseDir", info.BaseDir);
            // Tools
            variables.SetValue(VarsType.Global, "Tools", Path.Combine("%BaseDir%", "Projects", "Tools"));

            // Version
            variables.SetValue(VarsType.Global, "Version", info.Ver.Build.ToString());
            // Build
            variables.SetValue(VarsType.Global, "Build", $"{info.Ver.ToString():yyyy-MM-dd HH:mm}");
            // ProjectDir
            variables.SetValue(VarsType.Global, "ProjectDir", Path.Combine("%BaseDir%", "Projects", project.ProjectName));
            // TargetDir
            variables.SetValue(VarsType.Global, "TargetDir", Path.Combine("%BaseDir%", "Target", project.ProjectName));
        }

        private void LoadDefaultPluginVariables()
        {
            // ScriptFile, PluginFile
            variables.SetValue(VarsType.Local, "PluginFile", currentPlugin.FullPath);
            variables.SetValue(VarsType.Local, "ScriptFile", currentPlugin.FullPath);

            // [Variables]
            if (currentPlugin.Sections.ContainsKey("Variables"))
            {
                VarsType type = VarsType.Local;
                if (currentPlugin == project.MainPlugin)
                    type = VarsType.Global;
                variables.AddVariables(type, currentPlugin.Sections["Variables"], 0);
            }
        }

        /// <summary>
        /// Ready to run an plugin
        /// </summary>
        private void ReadyToRunPlugin()
        {
            // Turn off System,ErrorOff
            logger.ErrorOffCount = 0;
            // Turn off System,Log,Off
            logger.SuspendLog = false;

            currentPlugin = Plugins.GetPlugin(curPluginAddr);
            PluginSection section = currentPlugin.Sections["Process"];
            nextCommand = new CommandAddress(currentPlugin, section, 0, section.Count);
            logger.Write($"Processing plugin [{currentPlugin.ShortPath}] ({Plugins.GetFullIndex(curPluginAddr)}/{Plugins.Count})");
            logger.Write(new LogInfo(LogState.Info, $"Processing section [Process]"));

            variables.ResetVariables(VarsType.Local);
            LoadDefaultPluginVariables();
        }

        public void Build()
        {
            ReadyToRunPlugin();
            RunCommands();
        }

        /// <summary>
        /// Run array of commands.
        /// </summary>
        private void RunCommands()
        {
            while (true)
            {
                if (!(nextCommand.line < nextCommand.secLength)) // End of section
                {
                    currentSectionParams = new string[0];
                    logger.Write(new LogInfo(LogState.Info, $"End of section [{nextCommand.section.SectionName}]", returnAddress.Count - 1));

                    try
                    {
                        nextCommand = returnAddress.Pop();
                        continue;
                    }
                    catch (InvalidOperationException)
                    { // The Stack<T> is empty, readed plugin's end
                        logger.Write(new LogInfo(LogState.Info, $"End of plugin [{currentPlugin.ShortPath}]\n"));
                        if (runOnePlugin) // Just run one plugin
                            break; // Work is done, so exit
                        try
                        {
                            // PluginExit event callback
                            CheckAndRunCallback(ref onPluginExit, "OnPluginExit");
                            // Run next plugin
                            curPluginAddr = Plugins.GetNextAddress(curPluginAddr);
                            ReadyToRunPlugin();
                        }
                        catch (EndOfPluginLevelException)
                        { // End of plugins, build done. Exit.
                            // OnBuildExit event callback
                            CheckAndRunCallback(ref onBuildExit, "OnBuildExit");
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
                        logger.Write(ExecuteCommand(currentCommand), true);
                    }
                    catch (CriticalErrorException)
                    { // Critical Error, stop build
                        break;
                    }
                    catch (InvalidOpcodeException e)
                    {
                        logger.Write(new LogInfo(e.Cmd, LogState.CriticalError, e.Message));
                    }
                }
                catch (InvalidOpcodeException e)
                {
                    currentCommand = new BakeryCommand(rawCode, Opcode.Unknown, new string[0], returnAddress.Count);
                    logger.Write(new LogInfo(e.Cmd, LogState.CriticalError, e.Message));
                }
                catch (InvalidOperandException e)
                {
                    currentCommand = new BakeryCommand(rawCode, Opcode.Unknown, new string[0], returnAddress.Count);
                    logger.Write(new LogInfo(e.Cmd, LogState.CriticalError, e.Message));
                }

                if (0 <= elseFlag)
                    elseFlag--;
                nextCommand.line++;
            }
        }

        private void CheckAndRunCallback(ref BakeryCommand callback, string eventName)
        {
            if (callback != null)
            {
                logger.Write(new LogInfo(LogState.Info, $"Processing callback of event [{eventName}]"));
                if (callback.Opcode == Opcode.Run || callback.Opcode == Opcode.Exec)
                    logger.Write(RunExecCallback(callback, 0));
                else
                    logger.Write(ExecuteCommand(callback));
                logger.Write(new LogInfo(LogState.Info, $"End of callback [{eventName}]\n"));
                callback = null;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        private void RunCallbackSection(CommandAddress nextCommand, string[] sectionParams, int depth)
        {
            BakeryCommand currentCommand;
            while (true)
            {
                if (!(nextCommand.line < nextCommand.secLength)) // End of section
                {
                    // End of callback section
                    if (depth != 0)
                        logger.Write(new LogInfo(LogState.Info, $"End of section [{nextCommand.section.SectionName}]", depth - 1));
                    break;
                }

                // Fetch instructions
                int i = nextCommand.line;
                string rawCode = (nextCommand.section.Get() as string[])[i].Trim();

                try
                {
                    currentCommand = ParseCommand(rawCode, new CommandAddress(nextCommand.plugin, nextCommand.section, i, nextCommand.secLength));
                    currentCommand.Depth = depth;
                    try
                    {
                        if (currentCommand.Opcode == Opcode.Run || currentCommand.Opcode == Opcode.Exec)
                            logger.Write(RunExecCallback(currentCommand, depth + 1), true);
                        else
                            logger.Write(ExecuteCommand(currentCommand), true);
                    }
                    catch (CriticalErrorException)
                    { // Critical Error, stop build
                        break;
                    }
                    catch (InvalidOpcodeException e)
                    {
                        logger.Write(new LogInfo(e.Cmd, LogState.CriticalError, e.Message));
                    }
                }
                catch (InvalidOpcodeException e)
                {
                    currentCommand = new BakeryCommand(rawCode, Opcode.Unknown, new string[0], returnAddress.Count);
                    logger.Write(new LogInfo(e.Cmd, LogState.CriticalError, e.Message));
                }
                catch (InvalidOperandException e)
                {
                    currentCommand = new BakeryCommand(rawCode, Opcode.Unknown, new string[0], returnAddress.Count);
                    logger.Write(new LogInfo(e.Cmd, LogState.CriticalError, e.Message));
                }

                nextCommand.line++;
            }
        }

        /// <summary>
        /// Execute one command.
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="logger"></param>
        private LogInfo[] ExecuteCommand(BakeryCommand cmd)
        {
            LogInfo[] logs;

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
                    case Opcode.System:
                        logs = this.SystemCommands(cmd);
                        break;
                    case Opcode.ShellExecute:
                    case Opcode.ShellExecuteEx:
                    case Opcode.ShellExecuteDelete:
                        logs = this.ShellExecute(cmd);
                        break;
                    // Branch
                    case Opcode.Run:
                    case Opcode.Exec:
                        logs = this.RunExec(cmd);
                        break;
                    case Opcode.Begin:
                        logs = this.Begin(cmd);
                        break;
                    case Opcode.End:
                        logs = this.End(cmd);
                        break;
                    case Opcode.If:
                        logs = this.IfCondition(cmd);
                        break;
                    case Opcode.Else:
                        logs = this.Else(cmd);
                        break;
                    // Control
                    case Opcode.Set:
                        logs = this.Set(cmd);
                        break;
                    case Opcode.None:
                        logs = new LogInfo[] { new LogInfo(cmd, LogState.None, "NOP") };
                        break;
                    case Opcode.Comment:
                        logs = new LogInfo[] { new LogInfo(cmd, LogState.Ignore, "Comment") };
                        break;
                    default:
                        throw new InvalidOpcodeException($"Cannot execute [{cmd.Opcode.ToString()}] command", cmd);
                }
            }
            catch (CriticalErrorException e)
            {
                logger.Write(new LogInfo(cmd, LogState.CriticalError, e.GetType() + ": " + Helper.RemoveLastNewLine(e.Message)));
                throw e;
            }
            catch (Exception e)
            {
                logs = new LogInfo[] { new LogInfo(cmd, LogState.Error, e.GetType() + ": " + Helper.RemoveLastNewLine(e.Message)) };
            }

            return logs;
        }

        /// <summary>
        /// Parse raw command in string into BakeryCommand instance.
        /// </summary>
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
            string opcodeStr = slices[0].Trim();
            try
            {
                opcode = (Opcode)Enum.Parse(typeof(Opcode), opcodeStr, true);
                if (!Enum.IsDefined(typeof(Opcode), opcode) || opcode == Opcode.None || opcode == Opcode.Comment)
                    throw new ArgumentException();
            }
            catch (ArgumentException)
            {
                throw new InvalidOpcodeException($"Unknown command [{opcodeStr}]", new BakeryCommand(rawCode, Opcode.Unknown, new string[0], address, returnAddress.Count));
            }

            // Check doublequote's occurence - must be 2n
            if (Helper.CountStringOccurrences(rawCode, "\"") % 2 == 1)
                throw new InvalidCommandException("number of doublequotes must be times of 2");

            /// Parse operand
            ParseState state = ParseState.Normal;
            StringBuilder builder = new StringBuilder();

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
                            builder.Append(",");
                            builder.Append(slices[i]);
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
                                builder.Clear();
                                builder.Append(slices[i].Substring(1)); // Remove doublequote
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
                            builder.Append(",");
                            builder.Append(slices[i], 0, slices[i].Length - 1); // Remove doublequote
                            operandList.Add(builder.ToString());
                            builder.Clear();
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

            // string[] operands = EscapeStrings(operandList.ToArray());
            string[] operands = operandList.ToArray();

            // forge BakeryCommand
            return new BakeryCommand(rawCode, opcode, operands, address, returnAddress.Count);
        }

        private static readonly StringDictionary escapeChars = new StringDictionary()
        {
            { @"#$c", @"," },
            { @"#$p", @"%" },
            { @"#$q", @""""},
            { @"#$s", @" " },
            { @"#$t", @"\t"},
            { @"#$x", @"\n"},
            { @"#$z", @"\x00\x00"},
        };

        public string EscapeString(string operand)
        {
            return escapeChars.Keys.Aggregate(operand, (from, to) => from.Replace(to, escapeChars[to]));
        }

        public string[] EscapeStrings(string[] operands)
        {
            for (int i = 0; i < operands.Length; i++)
                operands[i] = EscapeString(operands[i]);
            return operands;
        }
    }
}
