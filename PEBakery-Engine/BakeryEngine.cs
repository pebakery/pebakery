using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

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

    /// <summary>
    /// Class to hold info of commands
    /// </summary>
    public class BakeryCommand
    {
        public string Origin;
        public Opcode Opcode;
        public string ExternalOpcode;
        public List<string> Operands;
        public SectionAddress Address;
        public int Depth;
        public List<BakeryCommand> Link;

        public BakeryCommand(string origin, Opcode opcode, List<string> operands)
        { InternalConstructor(origin, opcode, null, operands, new SectionAddress(), 0, null); }
        public BakeryCommand(string origin, Opcode opcode, List<string> operands, int depth)
        { InternalConstructor(origin, opcode, null, operands, new SectionAddress(), depth, null); }
        public BakeryCommand(string origin, Opcode opcode, List<string> operands, SectionAddress address)
        { InternalConstructor(origin, opcode, null, operands, address, 0, null); }
        public BakeryCommand(string origin, Opcode opcode, List<string> operands, SectionAddress address, int depth)
        { InternalConstructor(origin, opcode, null, operands, address, depth, null); }

        public BakeryCommand(Opcode opcode, List<string> operands)
        { InternalConstructor(ForgeRawCode(opcode, operands), opcode, null, operands, new SectionAddress(), 0, null); }
        public BakeryCommand(Opcode opcode, List<string> operands, int depth)
        { InternalConstructor(ForgeRawCode(opcode, operands), opcode, null, operands, new SectionAddress(), depth, null); }
        public BakeryCommand(Opcode opcode, List<string> operands, SectionAddress address)
        { InternalConstructor(ForgeRawCode(opcode, operands), opcode, null, operands, address, 0, null); }
        public BakeryCommand(Opcode opcode, List<string> operands, SectionAddress address, int depth)
        { InternalConstructor(ForgeRawCode(opcode, operands), opcode, null, operands, address, depth, null); }

        public BakeryCommand(string origin, Opcode opcode, List<string> operands, List<BakeryCommand> link)
        { InternalConstructor(origin, opcode, null, operands, new SectionAddress(), 0, link); }
        public BakeryCommand(string origin, Opcode opcode, List<string> operands, int depth, List<BakeryCommand> link)
        { InternalConstructor(origin, opcode, null, operands, new SectionAddress(), depth, link); }
        public BakeryCommand(string origin, Opcode opcode, List<string> operands, SectionAddress address, List<BakeryCommand> link)
        { InternalConstructor(origin, opcode, null, operands, address, 0, link); }
        public BakeryCommand(string origin, Opcode opcode, List<string> operands, SectionAddress address, int depth, List<BakeryCommand> link)
        { InternalConstructor(origin, opcode, null, operands, address, depth, link); }

        public BakeryCommand(Opcode opcode, List<string> operands, List<BakeryCommand> link)
        { InternalConstructor(ForgeRawCode(opcode, operands), opcode, null, operands, new SectionAddress(), 0, link); }
        public BakeryCommand(Opcode opcode, List<string> operands, int depth, List<BakeryCommand> link)
        { InternalConstructor(ForgeRawCode(opcode, operands), opcode, null, operands, new SectionAddress(), depth, link); }
        public BakeryCommand(Opcode opcode, List<string> operands, SectionAddress address, List<BakeryCommand> link)
        { InternalConstructor(ForgeRawCode(opcode, operands), opcode, null, operands, address, 0, link); }
        public BakeryCommand(Opcode opcode, List<string> operands, SectionAddress address, int depth, List<BakeryCommand> link)
        { InternalConstructor(ForgeRawCode(opcode, operands), opcode, null, operands, address, depth, link); }

        public BakeryCommand(string origin, string externalOpcode, List<string> operands)
        { InternalConstructor(origin, Opcode.Macro, externalOpcode, operands, new SectionAddress(), 0, null); }
        public BakeryCommand(string origin, string externalOpcode,  List<string> operands, int depth)
        { InternalConstructor(origin, Opcode.Macro, externalOpcode, operands, new SectionAddress(), depth, null); }
        public BakeryCommand(string origin, string externalOpcode,  List<string> operands, SectionAddress address)
        { InternalConstructor(origin, Opcode.Macro, externalOpcode, operands, address, 0, null); }
        public BakeryCommand(string origin, string externalOpcode,  List<string> operands, SectionAddress address, int depth)
        { InternalConstructor(origin, Opcode.Macro, externalOpcode, operands, address, depth, null); }

        public BakeryCommand(string externalOpcode,  List<string> operands)
        { InternalConstructor(ForgeRawCode(externalOpcode,  operands), Opcode.Macro, externalOpcode, operands, new SectionAddress(), 0, null); }
        public BakeryCommand(string externalOpcode,  List<string> operands, int depth)
        { InternalConstructor(ForgeRawCode(externalOpcode,  operands), Opcode.Macro, externalOpcode, operands, new SectionAddress(), depth, null); }
        public BakeryCommand(string externalOpcode,  List<string> operands, SectionAddress address)
        { InternalConstructor(ForgeRawCode(externalOpcode,  operands), Opcode.Macro, externalOpcode, operands, address, 0, null); }
        public BakeryCommand(string externalOpcode,  List<string> operands, SectionAddress address, int depth)
        { InternalConstructor(ForgeRawCode(externalOpcode,  operands), Opcode.Macro, externalOpcode, operands, address, depth, null); }

        public BakeryCommand(string origin, string externalOpcode,  List<string> operands, List<BakeryCommand> link)
        { InternalConstructor(origin, Opcode.Macro, externalOpcode, operands, new SectionAddress(), 0, link); }
        public BakeryCommand(string origin, string externalOpcode,  List<string> operands, int depth, List<BakeryCommand> link)
        { InternalConstructor(origin, Opcode.Macro, externalOpcode, operands, new SectionAddress(), depth, link); }
        public BakeryCommand(string origin, string externalOpcode,  List<string> operands, SectionAddress address, List<BakeryCommand> link)
        { InternalConstructor(origin, Opcode.Macro, externalOpcode,  operands, address, 0, link); }
        public BakeryCommand(string origin, string externalOpcode,  List<string> operands, SectionAddress address, int depth, List<BakeryCommand> link)
        { InternalConstructor(origin, Opcode.Macro, externalOpcode,  operands, address, depth, link); }

        public BakeryCommand(string externalOpcode,  List<string> operands, List<BakeryCommand> link)
        { InternalConstructor(ForgeRawCode(externalOpcode, operands), Opcode.Macro, externalOpcode,  operands, new SectionAddress(), 0, link); }
        public BakeryCommand(string externalOpcode,  List<string> operands, int depth, List<BakeryCommand> link)
        { InternalConstructor(ForgeRawCode(externalOpcode, operands), Opcode.Macro, externalOpcode,  operands, new SectionAddress(), depth, link); }
        public BakeryCommand(string externalOpcode,  List<string> operands, SectionAddress address, List<BakeryCommand> link)
        { InternalConstructor(ForgeRawCode(externalOpcode, operands), Opcode.Macro, externalOpcode,  operands, address, 0, link); }
        public BakeryCommand(string externalOpcode,  List<string> operands, SectionAddress address, int depth, List<BakeryCommand> link)
        { InternalConstructor(ForgeRawCode(externalOpcode, operands), Opcode.Macro, externalOpcode,  operands, address, depth, link); }

        public void InternalConstructor(string origin, Opcode opcode, string externalOpcode, List<string> operands, SectionAddress address, int depth, List<BakeryCommand> link)
        {
            this.Origin = origin;
            this.Opcode = opcode;
            this.Operands = operands;
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
            return ForgeRawCode(Opcode, Operands);
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

        public static string ForgeRawCode(string externalOpcode, List<string> operands)
        {
            StringBuilder builder = new StringBuilder(externalOpcode);
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

    public struct SectionAddress
    {
        public Plugin plugin;
        public PluginSection section;
        public SectionAddress(Plugin plugin, PluginSection section)
        {
            this.plugin = plugin;
            this.section = section;;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is SectionAddress))
                return false;

            SectionAddress addr = (SectionAddress)obj;

            bool result = true;
            if (plugin != addr.plugin || section != addr.section)
                result = false;
            return result;
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
            return plugin.ShortPath.Length + section.SectionName.Length + section.Count;
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
        private List<string> currentSectionParams;
        private bool runElse;

        // Fields : System Commands
        private BakeryCommand onBuildExit;
        private BakeryCommand onPluginExit;

        // Properties
        private PluginCollection Plugins { get { return project.ActivePlugins; } }

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

            LoadDefaultFixedVariables();
            logger.WriteGlobalVariables(variables);

            this.currentPlugin = entryPlugin;
            this.curPluginAddr = project.ActivePlugins.GetAddress(entryPlugin);
            this.currentSectionParams = new List<string>();
            this.runElse = false;

            this.onBuildExit = null;
            this.onPluginExit = null;
        }

        // Methods
        private void DisplayOperation(BakeryCommand cmd)
        {
            for (int i = 0; i < cmd.Depth; i++)
                Console.Write("  ");
            Console.WriteLine(cmd.Origin);
        }

        private void LoadDefaultFixedVariables()
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
            logger.Write($"Processing plugin [{currentPlugin.ShortPath}] ({Plugins.GetFullIndex(curPluginAddr)}/{Plugins.Count})");
            logger.Write(new LogInfo(LogState.Info, $"Processing section [Process]"));

            variables.ResetVariables(VarsType.Local);
            LoadDefaultPluginVariables();
        }

        public void Build()
        {            
            while(true)
            {
                ReadyToRunPlugin();
                RunSection(new SectionAddress(currentPlugin, currentPlugin.Sections["Process"]), new List<string>(), 0);
                try
                {
                    curPluginAddr = Plugins.GetNextAddress(curPluginAddr);
                }
                catch (EndOfPluginLevelException)
                { // End of plugins, build done. Exit.
                  // OnBuildExit event callback
                    CheckAndRunCallback(ref onBuildExit, "OnBuildExit");
                    break;
                }
            }
            
        }

        private void RunSection(SectionAddress addr, List<string> sectionParams, int depth)
        {
            List<BakeryCommand> codes = addr.section.GetCodes(true);
            RunCommands(codes, sectionParams, depth, true);
        }

        private void RunCommands(List<BakeryCommand> codes, List<string> sectionParams, int depth, bool inSection)
        {
            int idx = 0;
            BakeryCommand currentCommand = codes[0];
            while (true)
            {
                if (!(idx < codes.Count)) // End of section
                {
                    if (inSection)
                        logger.Write(new LogInfo(LogState.Info, $"End of section [{currentCommand.Address.section.SectionName}]", depth - 1));
                    if (depth == 0) // End of plugin
                        logger.Write(new LogInfo(LogState.Info, $"End of plugin [{currentPlugin.ShortPath}]\n"));
                        
                    // PluginExit event callback
                    CheckAndRunCallback(ref onPluginExit, "OnPluginExit");
                    break;
                }

                try
                {
                    currentCommand = codes[idx];
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
                catch (InvalidOperandException e)
                {
                    logger.Write(new LogInfo(e.Cmd, LogState.CriticalError, e.Message));
                }
                idx++;
            }
        }

        /*
        /// <summary>
        /// Run array of commands.
        /// </summary>
        private void RunCommands()
        {
            while (true)
            {
                if (!(nextCommand.line < nextCommand.secLength)) // End of section
                {
                    currentSectionParams = new List<string>();
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
                    currentCommand = new BakeryCommand(rawCode, Opcode.Unknown, new List<string>(), returnAddress.Count);
                    logger.Write(new LogInfo(e.Cmd, LogState.CriticalError, e.Message));
                }
                catch (InvalidOperandException e)
                {
                    currentCommand = new BakeryCommand(rawCode, Opcode.Unknown, new List<string>(), returnAddress.Count);
                    logger.Write(new LogInfo(e.Cmd, LogState.CriticalError, e.Message));
                }

                nextCommand.line++;
            }
        }
        */
        private void CheckAndRunCallback(ref BakeryCommand callback, string eventName)
        {
            if (callback != null)
            {
                logger.Write(new LogInfo(LogState.Info, $"Processing callback of event [{eventName}]"));
                if (callback.Opcode == Opcode.Run || callback.Opcode == Opcode.Exec)
                {
                    callback.Depth = 0;
                    logger.Write(RunExec(callback, 0));
                }
                else
                    logger.Write(ExecuteCommand(callback));
                logger.Write(new LogInfo(LogState.Info, $"End of callback [{eventName}]\n"));
                callback = null;
            }
        }

        /*
        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        private void RunCallbackSection(CommandAddress nextCommand, List<string> sectionParams, int depth)
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
                    currentCommand = new BakeryCommand(rawCode, Opcode.Unknown, new List<string>(), returnAddress.Count);
                    logger.Write(new LogInfo(e.Cmd, LogState.CriticalError, e.Message));
                }
                catch (InvalidOperandException e)
                {
                    currentCommand = new BakeryCommand(rawCode, Opcode.Unknown, new List<string>(), returnAddress.Count);
                    logger.Write(new LogInfo(e.Cmd, LogState.CriticalError, e.Message));
                }

                nextCommand.line++;
            }
        }
        */
        /// <summary>
        /// Execute one command.
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="logger"></param>
        private List<LogInfo> ExecuteCommand(BakeryCommand cmd)
        {
            List<LogInfo> logs;

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
                        // logs = this.RunExec(cmd);
                        logs = this.RunExec(cmd, cmd.Depth + 1);
                        break;
                    case Opcode.IfCompact:
                        logs = new List<LogInfo>();
                        this.IfCompact(cmd);
                        break;
                    case Opcode.ElseCompact:
                        logs = new List<LogInfo>();
                        this.ElseCompact(cmd);
                        break;
                    // Control
                    case Opcode.Set:
                        logs = this.Set(cmd);
                        break;
                    case Opcode.AddVariables:
                        logs = this.AddVariables(cmd);
                        break;
                    // Innormal case
                    case Opcode.None:
                        logs = new List<LogInfo>();
                        logs.Add(new LogInfo(cmd, LogState.None, "NOP"));
                        break;
                    case Opcode.Comment:
                        logs = new List<LogInfo>();
                        logs.Add(new LogInfo(cmd, LogState.Ignore, "Comment"));
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
                logs = new List<LogInfo>();
                logs.Add(new LogInfo(cmd, LogState.Error, e.GetType() + ": " + Helper.RemoveLastNewLine(e.Message)));
            }

            return logs;
        }

        private static readonly StringDictionary unescapeChars = new StringDictionary()
        {
            { @"#$c", @"," },
            { @"#$p", @"%" },
            { @"#$q", @""""},
            { @"#$s", @" " },
            { @"#$t", @"\t"},
            { @"#$x", @"\n"},
            { @"#$h", @"#" }, // Extended
            //{ @"#$z", @"\x00\x00"},
        };

        public string UnescapeString(string operand)
        {
            return unescapeChars.Keys.Aggregate(operand, (from, to) => from.Replace(to, unescapeChars[to]));
        }

        public List<string> UnescapeStrings(List<string> operands)
        {
            for (int i = 0; i < operands.Count; i++)
                operands[i] = UnescapeString(operands[i]);
            return operands;
        }

        public string EscapeString(string operand)
        {
            StringDictionary escapeChars = unescapeChars.ToDictionary(kp => kp.Value, kp => kp.Key);
            return escapeChars.Keys.Aggregate(operand, (from, to) => from.Replace(to, escapeChars[to]));
        }

        public List<string> EscapeStrings(List<string> operands)
        {
            for (int i = 0; i < operands.Count; i++)
                operands[i] = EscapeString(operands[i]);
            return operands;
        }
    }
}
