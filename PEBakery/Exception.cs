/*
    Copyright (C) 2016-2018 Hajin Jang
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

namespace PEBakery
{
    #region CodeParser, UIParser, Commands
    [Serializable]
    public class InvalidCommandException : Exception
    {
        public string RawLine { get; }
        public InvalidCommandException() { }
        public InvalidCommandException(string message) : base(message) { }
        public InvalidCommandException(string message, string rawLine) : base(message) { RawLine = rawLine; }
        public InvalidCommandException(string message, Exception inner) : base(message, inner) { }
    }

    [Serializable]
    public class InvalidCodeCommandException : Exception
    {
        public CodeCommand Cmd { get; }
        public InvalidCodeCommandException() { }
        public InvalidCodeCommandException(string message) : base(message) { }
        public InvalidCodeCommandException(CodeCommand cmd) { Cmd = cmd; }
        public InvalidCodeCommandException(string message, CodeCommand cmd) : base(message) { Cmd = cmd; }
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

    #region Script
    [Serializable]
    public class ScriptParseException : Exception
    {
        public ScriptParseException() { }
        public ScriptParseException(string message) : base(message) { }
        public ScriptParseException(string message, Exception inner) : base(message, inner) { }
    }

    [Serializable]
    public class ScriptSectionException : Exception
    {
        public ScriptSectionException() { }
        public ScriptSectionException(string message) : base(message) { }
        public ScriptSectionException(string message, Exception inner) : base(message, inner) { }
    }
    #endregion

    #region Engine and EngineState
    /// <summary>
    /// Such a critical error that build must be halt
    /// </summary>
    [Serializable]
    public class CriticalErrorException : Exception
    {
        public CriticalErrorException() { }
        public CriticalErrorException(string message) : base(message) { }
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

    #region Variables
    [Serializable]
    public class VariableCircularReferenceException : Exception
    {
        public VariableCircularReferenceException() { }
        public VariableCircularReferenceException(string message) : base(message) { }
        public VariableCircularReferenceException(string message, Exception inner) : base(message, inner) { }
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
