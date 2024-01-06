﻿/*
    Copyright (C) 2016-2023 Hajin Jang
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

namespace PEBakery.Core
{
    #region CodeParser, UIParser, Commands
    public class InvalidCommandException : Exception
    {
        public string RawLine { get; } = string.Empty;

        public InvalidCommandException() { }
        public InvalidCommandException(string message) : base(message) { }
        public InvalidCommandException(string message, string rawLine) : base(message) { RawLine = rawLine; }
        public InvalidCommandException(string message, Exception inner) : base(message, inner) { }
    }

    public class InvalidCodeCommandException : Exception
    {
        public CodeCommand Cmd { get; }

        public InvalidCodeCommandException(CodeCommand cmd) { Cmd = cmd; }
        public InvalidCodeCommandException(string message, CodeCommand cmd) : base(message) { Cmd = cmd; }
    }

    /// <summary>
    /// Unable to continue parsing because of internal parser error
    /// </summary>
    public class InternalParserException : Exception
    {
        public InternalParserException() { }
        public InternalParserException(string message) : base(message) { }
        public InternalParserException(string message, Exception inner) : base(message, inner) { }
    }
    #endregion

    #region Script
    public class ScriptParseException : Exception
    {
        public ScriptParseException() { }
        public ScriptParseException(string message) : base(message) { }
        public ScriptParseException(string message, Exception inner) : base(message, inner) { }
    }
    #endregion

    #region Engine
    /// <summary>
    /// Such a critical error that build must be halt (Logged as CriticalError)
    /// </summary>
    public class CriticalErrorException : Exception
    {
        public CriticalErrorException() { }
        public CriticalErrorException(string message) : base(message) { }
        public CriticalErrorException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Execute Exception
    /// </summary>
    public class ExecuteException : Exception
    {
        public ExecuteException() { }
        public ExecuteException(string message) : base(message) { }
        public ExecuteException(string message, Exception inner) : base(message, inner) { }
    }
    #endregion

    #region Internal
    /// <summary>
    /// The exception which represents internal logic error (Logged as CriticalError)
    /// </summary>
    public class InternalException : Exception
    {
        public InternalException() { }
        public InternalException(string message) : base(message) { }
        public InternalException(string message, Exception inner) : base(message, inner) { }
    }
    #endregion
}
