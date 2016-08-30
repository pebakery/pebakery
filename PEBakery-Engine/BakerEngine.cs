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
    public enum BakerOpcode
    {
        // Misc
        None = 0, Comment,
        // File
        FileCopy, FileDelete, FileRename, FileCreateBlank,
        // Text
        TXTAddLine
    }

    /// <summary>
    /// Class to hold info of commands
    /// </summary>
    public class BakerCommand
    {
        private BakerOpcode opcode;
        public BakerOpcode Opcode
        {
            get
            {
                return opcode;
            }
        }
        private string[] operands;
        public string[] Operands
        {
            get
            {
                return operands;
            }
        }

        /// <summary>
        /// Hold command information in BakerCommand instance.
        /// </summary>
        /// <param name="opcode"></param>
        /// <param name="operands"></param>
        /// <param name="optional"></param>
        public BakerCommand(BakerOpcode opcode, string[] operands)
        {
            this.opcode = opcode;
            this.operands = operands;
        }

        public override string ToString()
        {
            string str = this.opcode.ToString();
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
    public class BakerEngine
    {
        // Field
        private Logger logger;
        private Plugin plugin;
        private PluginSection secMain;
        private PluginSection secVariables;
        private PluginSection secInterface;
        private PluginSection secEntryPoint;
        private PluginSection secAttachAuthor;
        private PluginSection secAttachInterface;
        private Dictionary<string, string> variables;

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
                this.variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                LoadDefaultVariables();
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
                foreach (var vars in variables)
                {
                    Console.WriteLine(vars);
                }
                Console.WriteLine();

                // Commands - EntryPoint
                Console.WriteLine(secEntryPoint.SectionData);
                Console.WriteLine();

                // BakerCommand Test
                BakerCommand command = ParseCommand("FileCreateBlank,Korean_IME_TheOven.txt");
                Console.WriteLine(command);
                command = ParseCommand(@"TXTAddLine,Korean_IME_TheOven.txt,Test1,Append");
                Console.WriteLine(command);
                command = ParseCommand(@"TXTAddLine,%BaseDir%\Korean_IME_TheOven.txt,Test2,Append");
                Console.WriteLine(command);
                command = ParseCommand(@"//TXTAddLine,%BaseDir%\Korean_IME_TheOven.txt,Test2,Append");
                Console.WriteLine(command);
                command = ParseCommand(@"#TXTAddLine,%BaseDir%\Korean_IME_TheOven.txt,Test2,Append");
                Console.WriteLine(command);
            }
            catch (Exception e)
            {
                Console.WriteLine("[ERR]\n{0}", e.ToString());
            }
        }

        public void LoadDefaultVariables()
        {
            // BaseDir
            variables.Add("BaseDir", AppDomain.CurrentDomain.BaseDirectory);
            // Year, Month, Day
            DateTime todayDate = new DateTime();
            variables.Add("Year", todayDate.Year.ToString());
            variables.Add("Month", todayDate.Month.ToString());
            variables.Add("Day", todayDate.Day.ToString());
            // Build
            variables.Add("Build", String.Format("{0:yyyy-MM-dd HH:mm}", Helper.GetBuildDate()));
        }

        /// <summary>
        /// Run an plugin
        /// </summary>
        public void Run()
        {
            return;
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

            // Comment Format : starts with '//' or '#', ';'
            if (rawCode.Substring(0, 2) == "//" || rawCode.Substring(0, 1) == "#" || rawCode.Substring(0, 1) == ";")
            {
                return new BakerCommand(BakerOpcode.Comment, new string[0]);
            }

            // Splice with spaces
            string[] slices = rawCode.Split(',');
            if (slices.Length < 2) // No ',' -> Invalid format! Every command must have at least one operand!
                throw new InvalidCommandException();
            
            // Parse opcode
            try
            {
                // https://msdn.microsoft.com/ko-kr/library/essfb559(v=vs.110).aspx
                BakerOpcode opcodeValue = (BakerOpcode)Enum.Parse(typeof(BakerOpcode), slices[0], true);
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
                throw new InvalidOpcodeException();
            }
            catch (InvalidOpcodeException)
            {
                // Write an log : warning
            }

            // Check doublequote's occurence - must be 2n
            if (Helper.CountStringOccurrences(rawCode, "\"") % 2 == 1)
                throw new InvalidCommandException("number of doublequotes must be times of 2");

            /// Parse operand
            ParseState state = ParseState.Normal;
            string tmpStr = String.Empty;

            for (int i = 1; i < slices.Length; i++)
            {
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
                // Ex) Invalid : TXTAddLine,%Base%Dir%\Korean_IME_Fonts.txt,[Gulim],Append
                if (Helper.CountStringOccurrences(operands[i], @"%") % 2 == 1)
                    throw new InvalidCommandException(@"Variable names must be enclosed by %");

                // Expand variable's name into value
                // Ex) 123%BaseDir%456%OS%789
                MatchCollection matches = Regex.Matches(operands[i], @"%(.+)%");
                string expandStr = String.Empty;
                for (int x = 0; x < matches.Count; x++)
                {
                    expandStr = String.Concat(expandStr, operands[i].Substring(x == 0 ? 0 : matches[x-1].Index, matches[x].Index));
                    expandStr = String.Concat(expandStr, variables[matches[x].Groups[1].ToString()]);
                    if (x + 1 == matches.Count) // Last iteration
                        expandStr = String.Concat(expandStr, operands[i].Substring(matches[x].Index + matches[x].Value.Length));
                }
                if (0 < matches.Count)
                    operands[i] = expandStr;

                // Process Escape Characters
                operands[i] = operands[i].Replace(@"$#c", ",");
                operands[i] = operands[i].Replace(@"$#p", "%");
                operands[i] = operands[i].Replace(@"$#q", "\"");
                operands[i] = operands[i].Replace(@"$#s", " ");
                operands[i] = operands[i].Replace(@"$#x", "\r\n");
                // operands[i] = operands[i].Replace(@"$#z", "\x00\x00");
            }

            // forge BakerCommand
            return new BakerCommand(opcode, operands);
        }


        private void ExecuteCommand(BakerCommand command)
        {
            switch (command.Opcode)
            {
                case BakerOpcode.FileCopy:
                    BakerOperations.FileCopy(command.Operands);
                    break;
                case BakerOpcode.FileDelete:
                    BakerOperations.FileDelete(command.Operands);
                    break;
                case BakerOpcode.FileRename:
                    BakerOperations.FileRename(command.Operands);
                    break;
                case BakerOpcode.FileCreateBlank:
                    BakerOperations.FileCreateBlank(command.Operands);
                    break;
                case BakerOpcode.TXTAddLine:
                    BakerOperations.TXTAddLine(command.Operands);
                    break;
                case BakerOpcode.None: // NOP
                    break;
                case BakerOpcode.Comment: // NOP
                    break;
                default:
                    throw new InvalidOpcodeException();
            }
        }
    }
}
