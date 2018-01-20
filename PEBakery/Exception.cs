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

    Additional permission under GNU GPL version 3 section 7

    If you modify this program, or any covered work, by linking
    or combining it with external libraries, containing parts
    covered by the terms of various license, the licensors of
    this program grant you additional permission to convey the
    resulting work. An external library is a library which is
    not derived from or based on this program. 
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PEBakery.Core;
using PEBakery.Core.Commands;
using System.Runtime.Serialization;

namespace PEBakery.Exceptions
{
    #region CodeCommandException, UICommandException
    public class CodeCommandException : Exception
    {
        private CodeCommand cmd;
        public CodeCommand Cmd { get => cmd; }
        public CodeCommandException() { }
        public CodeCommandException(string message) : base(message) { }
        public CodeCommandException(CodeCommand cmd) { this.cmd = cmd; }
        public CodeCommandException(string message, CodeCommand cmd) : base(message) { this.cmd = cmd; }
        public CodeCommandException(string message, Exception inner) : base(message, inner) { }
    }

    public class UICommandException : Exception
    {
        private UICommand uiCmd;
        public UICommand UICmd { get => uiCmd; }
        public UICommandException() { }
        public UICommandException(string message) : base(message) { }
        public UICommandException(UICommand uiCmd) { this.uiCmd = uiCmd; }
        public UICommandException(string message, UICommand uiCmd) : base(message) { this.uiCmd = uiCmd; }
        public UICommandException(string message, Exception inner) : base(message, inner) { }
    }
    #endregion

    #region CodeParser, UIParser, Commands
    [Serializable]
    public class InvalidCommandException : Exception
    {
        private string rawLine;
        public string RawLine { get => rawLine; }
        public InvalidCommandException() { }
        public InvalidCommandException(string message) : base(message) { }
        public InvalidCommandException(string message, string rawLine) : base(message) { this.rawLine = rawLine; }
        public InvalidCommandException(string message, Exception inner) : base(message, inner) { }
    }


    public class InvalidUICommandException : UICommandException
    {
        public InvalidUICommandException() { }
        public InvalidUICommandException(string message) : base(message) { }
        public InvalidUICommandException(UICommand uiCmd) : base(uiCmd) { }
        public InvalidUICommandException(string message, UICommand uiCmd) : base(message, uiCmd) { }
        public InvalidUICommandException(string message, Exception inner) : base(message, inner) { }
    }

    public class InvalidCodeCommandException : CodeCommandException
    {
        public InvalidCodeCommandException() { }
        public InvalidCodeCommandException(string message) : base(message) { }
        public InvalidCodeCommandException(CodeCommand cmd) : base(cmd) { }
        public InvalidCodeCommandException(string message, CodeCommand cmd) : base(message, cmd) { }
        public InvalidCodeCommandException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Internal error which unable to continue parsing
    /// </summary>
    [Serializable]
    public class InternalParserException : Exception
    {
        public InternalParserException() { }
        public InternalParserException(string message) : base(message) { }
        public InternalParserException(string message, Exception inner) : base(message, inner) { }
    }
    #endregion

    #region Plugin
    [Serializable]
    public class PluginParseException : Exception
    {
        public PluginParseException() { }
        public PluginParseException(string message) : base(message) { }
        public PluginParseException(string message, Exception inner) : base(message, inner) { }
    }
    #endregion

    #region Engine / EngineState
    /// <summary>
    /// Such a critical error that build must be halt
    /// </summary>
    [Serializable]
    public class CriticalErrorException : CodeCommandException
    {
        public CriticalErrorException() { }
        public CriticalErrorException(string message) : base(message) { }
        public CriticalErrorException(CodeCommand cmd) : base(cmd) { }
        public CriticalErrorException(string message, CodeCommand cmd) : base(message, cmd) { }
        public CriticalErrorException(string message, Exception inner) : base(message, inner) { }
    }

    [Serializable]
    public class InternalException : Exception
    {
        public InternalException() { }
        public InternalException(string message) : base(message) { }
        public InternalException(string message, Exception inner) : base(message, inner) { }
    }
    #endregion

    #region EncodedFile
    [Serializable]
    public class FileEncodeFailException : Exception
    {
        public FileEncodeFailException() { }
        public FileEncodeFailException(string message) : base(message) { }
        public FileEncodeFailException(string message, Exception inner) : base(message, inner) { }
    }

    [Serializable]
    public class FileDecodeFailException : Exception
    {
        public FileDecodeFailException() { }
        public FileDecodeFailException(string message) : base(message) { }
        public FileDecodeFailException(string message, Exception inner) : base(message, inner) { }
    }

    [Serializable]
    public class ExtractFileNotFoundException : Exception
    {
        public ExtractFileNotFoundException() { }
        public ExtractFileNotFoundException(string message) : base(message) { }
        public ExtractFileNotFoundException(string message, Exception inner) : base(message, inner) { }
    }
    #endregion

    #region Variables
    [Serializable]
    public class VariableCircularReferenceException : Exception
    {
        public VariableCircularReferenceException() { }
        public VariableCircularReferenceException(string message) : base(message) { }
        public VariableCircularReferenceException(string message, Exception inner) : base(message, inner) { }
    }

    [Serializable]
    public class VariableInvalidFormatException : Exception
    {
        public VariableInvalidFormatException() { }
        public VariableInvalidFormatException(string message) : base(message) { }
        public VariableInvalidFormatException(string message, Exception inner) : base(message, inner) { }
    }
    #endregion

    #region Regsitry
    public class InvalidRegKeyException : Exception
    {
        public InvalidRegKeyException() { }
        public InvalidRegKeyException(string message) : base(message) { }
        public InvalidRegKeyException(string message, Exception inner) : base(message, inner) { }
    }
    #endregion
}
