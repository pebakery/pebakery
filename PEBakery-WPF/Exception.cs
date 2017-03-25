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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PEBakery.Core;

namespace PEBakery.Exceptions
{
    #region Plugin
    /// <summary>
    /// Cannot found plugin
    /// </summary>
    public class PluginNotFoundException : Exception
    {
        public PluginNotFoundException() { }
        public PluginNotFoundException(string message) : base(message) { }
        public PluginNotFoundException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Reached end of plugin levels
    /// </summary>
    public class EndOfPluginLevelException : Exception
    {
        public EndOfPluginLevelException() { }
        public EndOfPluginLevelException(string message) : base(message) { }
        public EndOfPluginLevelException(string message, Exception inner) : base(message, inner) { }
    }

    public class PluginSectionNotFoundException : Exception
    {
        public PluginSectionNotFoundException() { }
        public PluginSectionNotFoundException(string message) : base(message) { }
        public PluginSectionNotFoundException(string message, Exception inner) : base(message, inner) { }
    }

    public class PluginParseException : Exception
    {
        public PluginParseException() { }
        public PluginParseException(string message) : base(message) { }
        public PluginParseException(string message, Exception inner) : base(message, inner) { }
    }
#endregion

    #region Engine / EngineState

    /// <summary>
    /// So Critical error that build must be halt
    /// </summary>
    public class CriticalErrorException : Exception
    {
        private CodeCommand cmd;
        public CodeCommand Cmd { get { return cmd; } }
        public CriticalErrorException() { }
        public CriticalErrorException(string message) : base(message) { }
        public CriticalErrorException(CodeCommand cmd) { this.cmd = cmd; }
        public CriticalErrorException(string message, CodeCommand cmd) : base(message) { this.cmd = cmd; }
        public CriticalErrorException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Command contains invalid Opcode
    /// </summary>
    public class InvalidOpcodeException : Exception
    {
        private CodeCommand cmd;
        public CodeCommand Cmd { get { return cmd; } }
        public InvalidOpcodeException() { }
        public InvalidOpcodeException(string message) : base(message) { }
        public InvalidOpcodeException(CodeCommand cmd) { this.cmd = cmd; }
        public InvalidOpcodeException(string message, CodeCommand cmd) : base(message) { this.cmd = cmd; }
        public InvalidOpcodeException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// /// BakerySubCommandes contains invalid subOpcode
    /// </summary>
    public class InvalidSubOpcodeException : Exception
    {
        private CodeCommand cmd;
        public CodeCommand Cmd { get { return cmd; } }
        public InvalidSubOpcodeException() { }
        public InvalidSubOpcodeException(string message) : base(message) { }
        public InvalidSubOpcodeException(CodeCommand cmd) { this.cmd = cmd; }
        public InvalidSubOpcodeException(string message, CodeCommand cmd) : base(message) { this.cmd = cmd; }
        public InvalidSubOpcodeException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Command contains invalid Operand
    /// </summary>
    public class InvalidOperandException : Exception
    {
        private CodeCommand cmd;
        public CodeCommand Cmd { get { return cmd; } }
        public InvalidOperandException() { }
        public InvalidOperandException(string message) : base(message) { }
        public InvalidOperandException(CodeCommand cmd) { this.cmd = cmd; }
        public InvalidOperandException(string message, CodeCommand cmd) : base(message) { this.cmd = cmd; }
        public InvalidOperandException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// LogInfo contains invalid log format
    /// </summary>
    public class InvalidLogFormatException : Exception
    {
        private CodeCommand cmd;
        public CodeCommand Cmd { get { return cmd; } }
        public InvalidLogFormatException() { }
        public InvalidLogFormatException(string message) : base(message) { }
        public InvalidLogFormatException(CodeCommand cmd) { this.cmd = cmd; }
        public InvalidLogFormatException(string message, CodeCommand cmd) : base(message) { this.cmd = cmd; }
        public InvalidLogFormatException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Command contains invalid SubCommand
    /// </summary>
    public class InvalidSubCommandException : Exception
    {
        private CodeCommand cmd;
        public CodeCommand Cmd { get { return cmd; } }
        public InvalidSubCommandException() { }
        public InvalidSubCommandException(string message) : base(message) { }
        public InvalidSubCommandException(CodeCommand cmd) { this.cmd = cmd; }
        public InvalidSubCommandException(string message, CodeCommand cmd) : base(message) { this.cmd = cmd; }
        public InvalidSubCommandException(string message, Exception inner) : base(message, inner) { }
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
    /// BakeryCodeParser cannot continue parsing due to malformed command
    /// </summary>
    /// <remarks>
    /// Throw this if Command is not forged
    /// </remarks>
    public class InvalidCommandException : Exception
    {
        public InvalidCommandException() { }
        public InvalidCommandException(string message) : base(message) { }
        public InvalidCommandException(string message, Exception inner) : base(message, inner) { }
    }

    #endregion

    #region CodeParser

    /// <summary>
    /// BakeryCodeParser cannot continue parsing due to malformed grammar of command, especially If and Else
    /// </summary>
    /// <remarks>
    /// Throw this if Command is already forged
    /// </remarks>
    public class InvalidGrammarException : Exception
    {
        private CodeCommand cmd;
        public CodeCommand Cmd { get { return cmd; } }
        public InvalidGrammarException() { }
        public InvalidGrammarException(string message) : base(message) { }
        public InvalidGrammarException(CodeCommand cmd) { this.cmd = cmd; }
        public InvalidGrammarException(string message, CodeCommand cmd) : base(message) { this.cmd = cmd; }
        public InvalidGrammarException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Internal error which unable to continue parsing
    /// </summary>
    public class InternalParseException : Exception
    {
        public InternalParseException() { }
        public InternalParseException(string message) : base(message) { }
        public InternalParseException(string message, Exception inner) : base(message, inner) { }
    }

#endregion
}
