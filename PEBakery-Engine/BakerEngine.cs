using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections;
using System.Text.RegularExpressions;

namespace PEBakery_Engine
{
    using VariableDictionary = Dictionary<string, string>;

    public enum BakerOpcode
    {
        // Misc
        None = 0, Comment, Error, Unknown, 
        // File
        FileCopy, FileDelete, FileRename, FileCreateBlank,
        // Text
        TXTAddLine,
        // Misc
        Set,
    }

    public enum BakerState
    {
        None = 0,

    }

    /// <summary>
    /// Class to hold info of commands
    /// </summary>
    public class BakerCommand
    {
        private string rawCode;
        public string RawCode
        {
            get { return rawCode; }
        }
        private BakerOpcode opcode;
        public BakerOpcode Opcode
        {
            get { return opcode; }
        }
        private string[] operands;
        public string[] Operands
        {
            get { return operands; }
        }

        /// <summary>
        /// Hold command information in BakerCommand instance.
        /// </summary>
        /// <param name="opcode"></param>
        /// <param name="operands"></param>
        /// <param name="optional"></param>
        public BakerCommand(string rawCode, BakerOpcode opcode, string[] operands)
        {
            this.rawCode = rawCode;
            this.opcode = opcode;
            this.operands = operands;
        }

        public override string ToString()
        {
            string str = String.Concat("[", this.rawCode, "]\n", this.opcode.ToString());
            foreach (string operand in this.operands)
                str = String.Concat(str, "_", operand);

            return str;
        }
    }

    /// <summary>
    /// Exception used in BakerEngine::ParseCommand
    /// </summary>
    public class InvalidCommandException : Exception
    {
        public InvalidCommandException() { }
        public InvalidCommandException(string message) : base(message) { }
        public InvalidCommandException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Exception used in BakerEngine::ParseCommand
    /// </summary>
    public class InvalidOpcodeException : Exception
    {
        public InvalidOpcodeException() { }
        public InvalidOpcodeException(string message) : base(message) { }
        public InvalidOpcodeException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Exception used in BakerEngine::ParseCommand
    /// </summary>
    public class InvalidOperandException : Exception
    {
        public InvalidOperandException() { }
        public InvalidOperandException(string message) : base(message) { }
        public InvalidOperandException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Exception used in BakerEngine::ParseCommand
    /// </summary>
    public class InternalParseException : Exception
    {
        public InternalParseException() { }
        public InternalParseException(string message) : base(message) { }
        public InternalParseException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Interpreter of raw codes
    /// </summary>
    public partial class BakerEngine
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
        private BakerCommand currentCommand;
        private BakerCommand nextCommand;

        // Enum
        private enum ParseState { Normal, Merge }

        // Constructor
        public BakerEngine(Plugin plugin, Logger logger)
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

                // Commands - EntryPoint
                Console.WriteLine("- EntryPoint");
                Console.WriteLine(secEntryPoint.SectionData);
                Console.WriteLine();
            }
            catch (Exception e)
            {
                Console.WriteLine("[ERR]\n{0}", e.ToString());
            }
        }

        public void LoadDefaultGlobalVariables()
        {
            // BaseDir
            globalVars.Add("BaseDir", Helper.RemoveLastDirectorySeparator(AppDomain.CurrentDomain.BaseDirectory));
            // Year, Month, Day
            DateTime todayDate = DateTime.Now;
            globalVars.Add("Year", todayDate.Year.ToString());
            globalVars.Add("Month", todayDate.Month.ToString());
            globalVars.Add("Day", todayDate.Day.ToString());
            // Build
            globalVars.Add("Build", String.Format("{0:yyyy-MM-dd HH:mm}", Helper.GetBuildDate()));
        }

        /// <summary>
        /// Run an plugin
        /// </summary>
        public void Run()
        {
            RunSection(secEntryPoint);
            return;
        }

        private void RunSection(PluginSection section)
        {
            string[] codes = section.SectionData.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);
            for (int i = 0; i < codes.Length; i++)
            {
                currentCommand = ParseCommand(codes[i]);
                logger.Write(ExecuteCommand(currentCommand));
            }
        }

        private LogInfo ExecuteCommand(BakerCommand cmd)
        {
            LogInfo log = null;
            switch (cmd.Opcode)
            {
                case BakerOpcode.FileCopy:
                    log = this.FileCopy(cmd);
                    break;
                case BakerOpcode.FileDelete:
                    log = this.FileDelete(cmd);
                    break;
                case BakerOpcode.FileRename:
                    log = this.FileRename(cmd);
                    break;
                case BakerOpcode.FileCreateBlank:
                    log = this.FileCreateBlank(cmd);
                    break;
                case BakerOpcode.TXTAddLine:
                    log = this.TXTAddLine(cmd);
                    break;
                case BakerOpcode.Set:
                    log = this.Set(cmd);
                    break;
                case BakerOpcode.None: // NOP
                    log = new LogInfo(cmd.RawCode, "NOP", LogState.None);
                    break;
                case BakerOpcode.Comment: // NOP
                    log = new LogInfo(cmd.RawCode, "Comment", LogState.Ignore);
                    break;
                case BakerOpcode.Unknown: // NOP
                    log = new LogInfo(cmd.RawCode, "Unknown", LogState.Error);
                    break;
                default:
                    throw new InvalidOpcodeException("ExecuteCommand does not know " + cmd.Opcode.ToString());
            }

            return log;
        }

        /// <summary>
        /// Parse raw command in string into BakerCommand instance.
        /// </summary>
        /// <param name="rawCode"></param>
        /// <returns></returns>
        private BakerCommand ParseCommand(string rawCode)
        {
            BakerOpcode opcode = BakerOpcode.None;
            ArrayList operandList = new ArrayList();

            // Remove whitespace of rawCode's start and end
            rawCode = rawCode.Trim();

            // Check if rawCode is Empty
            if (String.Equals(rawCode, String.Empty))
                return new BakerCommand(String.Empty, BakerOpcode.None, new string[0]);

            // Comment Format : starts with '//' or '#', ';'
            if (rawCode.Substring(0, 2) == "//" || rawCode.Substring(0, 1) == "#" || rawCode.Substring(0, 1) == ";")
            {
                return new BakerCommand(rawCode, BakerOpcode.Comment, new string[0]);
            }

            // Splice with spaces
            string[] slices = rawCode.Split(',');
            /*
            if (slices.Length < 2) // No ',' -> Invalid format! Every command must have at least one operand!
            {
                if (!(String.Equals(slices[0], "Begin", StringComparison.OrdinalIgnoreCase))
                    || String.Equals(slices[0], "End", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidCommandException();
            }
            */
            
            // Parse opcode
            try
            {
                // https://msdn.microsoft.com/ko-kr/library/essfb559(v=vs.110).aspx
                BakerOpcode opcodeValue = (BakerOpcode)Enum.Parse(typeof(BakerOpcode), slices[0].Trim(), true);
                if (Enum.IsDefined(typeof(BakerOpcode), opcodeValue))
                {
                    if (opcodeValue != BakerOpcode.None && opcodeValue != BakerOpcode.Comment)
                        opcode = opcodeValue;
                    else
                        throw new InvalidOpcodeException();
                }
                else
                    throw new InvalidOpcodeException();
            }
            catch (ArgumentException)
            {
                return new BakerCommand(rawCode, BakerOpcode.Unknown, new string[0]);
            }
            catch (InvalidOpcodeException)
            {
                return new BakerCommand(rawCode, BakerOpcode.Unknown, new string[0]);
            }

            // Check doublequote's occurence - must be 2n
            if (Helper.CountStringOccurrences(rawCode, "\"") % 2 == 1)
                throw new InvalidCommandException("number of doublequotes must be times of 2");

            /// Parse operand
            ParseState state = ParseState.Normal;
            string tmpStr = String.Empty;

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
                            tmpStr = String.Concat(tmpStr, ",", slices[i]); 
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
                                tmpStr = String.Concat(slices[i].Substring(1)); // Remove doublequote
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
                            tmpStr = String.Concat(tmpStr, ",", slices[i].Substring(0, slices[i].Length - 1)); // Remove doublequote
                            operandList.Add(tmpStr);
                            tmpStr = String.Empty;
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
                if (opcode != BakerOpcode.Set)
                    operands[i] = ExpandVariables(operands[i]);

                // Process Escape Characters
                operands[i] = operands[i].Replace(@"$#c", ",");
                operands[i] = operands[i].Replace(@"$#p", "%");
                operands[i] = operands[i].Replace(@"$#q", "\"");
                operands[i] = operands[i].Replace(@"$#s", " ");
                operands[i] = operands[i].Replace(@"$#t", "\t");
                operands[i] = operands[i].Replace(@"$#x", "\r\n");
                operands[i] = operands[i].Replace(@"$#z", "\x00\x00");
            }

            // forge BakerCommand
            return new BakerCommand(rawCode, opcode, operands);
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
            string expandStr = String.Empty;
            for (int x = 0; x < matches.Count; x++)
            {
                string varName = matches[x].Groups[1].ToString();
                expandStr = String.Concat(expandStr, operand.Substring(x == 0 ? 0 : matches[x - 1].Index, matches[x].Index));
                if (globalVars.ContainsKey(varName))
                    expandStr = String.Concat(expandStr, globalVars[varName]);
                else if (localVars.ContainsKey(varName))
                    expandStr = String.Concat(expandStr, localVars[varName]);
                else // no variable exists? log it and pass
                    logger.Write(new LogInfo(currentCommand.RawCode, "Variable [" + varName + "] does not exists", LogState.Warning));

                if (x + 1 == matches.Count) // Last iteration
                    expandStr = String.Concat(expandStr, operand.Substring(matches[x].Index + matches[x].Value.Length));
            }
            if (0 < matches.Count) // Only copy it if variable exists
                operand = expandStr;

            return operand;
        }

    }
}
