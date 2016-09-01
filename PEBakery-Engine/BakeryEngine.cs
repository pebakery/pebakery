using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections;
using System.Text.RegularExpressions;

namespace BakeryEngine
{
    using VariableDictionary = Dictionary<string, string>;

    public enum Opcode
    {
        // Misc
        None = 0, Comment, Error, Unknown, 
        // File
        FileCopy, FileDelete, FileRename, FileMove, FileCreateBlank,
        // Text
        TXTAddLine,
        // Misc
        Set,
        // If
        If,
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

        /// <summary>
        /// Hold command information in BakeryCommand instance.
        /// </summary>
        /// <param name="opcode"></param>
        /// <param name="operands"></param>
        /// <param name="optional"></param>
        public BakeryCommand(string rawCode, Opcode opcode, string[] operands)
        {
            this.rawCode = rawCode;
            this.opcode = opcode;
            this.operands = operands;
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
    public class FileDoesNotExistException : Exception
    {
        public FileDoesNotExistException() { }
        public FileDoesNotExistException(string message) : base(message) { }
        public FileDoesNotExistException(string message, Exception inner) : base(message, inner) { }
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
    /// Interpreter of raw codes
    /// </summary>
    public partial class BakeryEngine
    {
        // Fields used globally
        private Logger logger;
        private Plugin plugin;
        private PluginSection secMain;
        private PluginSection secVariables;
        private PluginSection secInterface;
        private PluginSection secEntryPoint;
        private PluginSection secAttachAuthor;
        private PluginSection secAttachInterface;
        private VariableDictionary globalVars;
        private VariableDictionary localVars;

        // Fields used as engine's state
        private BakeryCommand currentCommand;
        // private NextCommand next;

        // Enum
        private enum ParseState { Normal, Merge }

        // Constructor
        public BakeryEngine(Plugin plugin, Logger logger)
        {
            try
            {
                this.plugin = plugin;
                this.secMain = plugin.FindSection("Main");
                this.secInterface = plugin.FindSection("Interface");
                this.secVariables = plugin.FindSection("Variables");
                this.secEntryPoint = plugin.FindSection("Process");
                this.secAttachAuthor = plugin.FindSection("AuthorEncoded");
                this.secAttachInterface = plugin.FindSection("InterfaceEncoded");
                this.logger = logger;
                this.globalVars = new VariableDictionary(StringComparer.OrdinalIgnoreCase);
                this.localVars = new VariableDictionary(StringComparer.OrdinalIgnoreCase);
                LoadDefaultGlobalVariables();
                currentCommand = null;
            }
            catch (Exception e)
            {
                Console.WriteLine("[ERR]\n{0}", e.ToString());
            }
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
                Console.WriteLine("- GlobalVars");
                foreach (var vars in globalVars)
                    Console.WriteLine(vars);
                Console.WriteLine();
                Console.WriteLine("- LocalVars");
                foreach (var vars in localVars)
                    Console.WriteLine(vars);
                Console.WriteLine();
            }
            catch (Exception e)
            {
                Console.WriteLine("[ERR]\n{0}", e.ToString());
            }
        }

        private void DisplayOperation(string str)
        {
            Console.WriteLine(str);
        }

        private void LoadDefaultGlobalVariables()
        {
            // BaseDir
            globalVars.Add("BaseDir", Helper.RemoveLastDirChar(AppDomain.CurrentDomain.BaseDirectory));
            // Year, Month, Day
            DateTime todayDate = DateTime.Now;
            globalVars.Add("Year", todayDate.Year.ToString());
            globalVars.Add("Month", todayDate.Month.ToString());
            globalVars.Add("Day", todayDate.Day.ToString());
            // Build
            globalVars.Add("Build", string.Format("{0:yyyy-MM-dd HH:mm}", Helper.GetBuildDate()));
        }

        /// <summary>
        /// Run an plugin
        /// </summary>
        public void Run()
        {
            RunSection(secEntryPoint);
            return;
        }

        /// <summary>
        /// Run an section
        /// </summary>
        /// <param name="section"></param>
        private void RunSection(PluginSection section)
        {
            string[] codes = section.SectionData.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);
            for (int i = 0; i < codes.Length; i++)
            {
                string rawCode = codes[i].Trim();
                try
                {
                    currentCommand = ParseCommand(rawCode);
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
            }
        }

        private void ExecuteCommand(BakeryCommand cmd, Logger logger)
        {
            LogInfo log = null;
            LogInfo[] logs = null;

            DisplayOperation(cmd.RawCode);
            try
            {
                switch (cmd.Opcode)
                {
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
                    case Opcode.TXTAddLine:
                        log = this.TXTAddLine(cmd);
                        break;
                    case Opcode.Set:
                        log = this.Set(cmd);
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
        private BakeryCommand ParseCommand(string rawCode)
        {
            Opcode opcode = Opcode.None;
            ArrayList operandList = new ArrayList();

            // Remove whitespace of rawCode's start and end
            rawCode = rawCode.Trim();

            // Check if rawCode is Empty
            if (string.Equals(rawCode, string.Empty))
                return new BakeryCommand(string.Empty, Opcode.None, new string[0]);

            // Comment Format : starts with '//' or '#', ';'
            if (rawCode.Substring(0, 2) == "//" || rawCode.Substring(0, 1) == "#" || rawCode.Substring(0, 1) == ";")
            {
                return new BakeryCommand(rawCode, Opcode.Comment, new string[0]);
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
                        throw new InvalidOpcodeException("Unknown command \'" + slices[0].Trim() + "\'", new BakeryCommand(rawCode, Opcode.Unknown, new string[0]));
                }
                else
                    throw new InvalidOpcodeException("Unknown command \'" + slices[0].Trim() + "\'", new BakeryCommand(rawCode, Opcode.Unknown, new string[0]));
            }
            catch (ArgumentException)
            {
                throw new InvalidOpcodeException("Unknown command \'" + slices[0].Trim() + "\'", new BakeryCommand(rawCode, Opcode.Unknown, new string[0]));
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
            return new BakeryCommand(rawCode, opcode, operands);
        }

        /// <summary>
        /// Expand PEBakery variables
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public string ExpandVariables(string operand)
        {
            // Ex) Invalid : TXTAddLine,%Base%Dir%\Korean_IME_Fonts.txt,[Gulim],Append
            if (Helper.CountStringOccurrences(operand, @"%") % 2 == 1)
                throw new InvalidCommandException(@"Variable names must be enclosed by %");

            // Expand variable's name into value
            // Ex) 123%BaseDir%456%OS%789
            MatchCollection matches = Regex.Matches(operand, @"%(.+)%");
            string expandStr = string.Empty;
            for (int x = 0; x < matches.Count; x++)
            {
                string varName = matches[x].Groups[1].ToString();
                expandStr = string.Concat(expandStr, operand.Substring(x == 0 ? 0 : matches[x - 1].Index, matches[x].Index));
                if (globalVars.ContainsKey(varName))
                    expandStr = string.Concat(expandStr, globalVars[varName]);
                else if (localVars.ContainsKey(varName))
                    expandStr = string.Concat(expandStr, localVars[varName]);
                else // no variable exists? log it and pass
                    logger.Write(new LogInfo(currentCommand, "Variable [" + varName + "] does not exists", LogState.Warning));

                if (x + 1 == matches.Count) // Last iteration
                    expandStr = string.Concat(expandStr, operand.Substring(matches[x].Index + matches[x].Value.Length));
            }
            if (0 < matches.Count) // Only copy it if variable exists
                operand = expandStr;

            return operand;
        }

    }
}
