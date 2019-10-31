/*
    Copyright (C) 2019 Hajin Jang
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;

namespace PEBakery.Core.Tests.Command
{
    [TestClass]
    [TestCategory("CommandMacro")]
    public class CommandMacroTests
    {
        #region Macro
        [TestMethod]
        public void Macro()
        {
            void ScriptTemplate(string treePath, string entrySection, ErrorCheck check = ErrorCheck.Success)
            {
                (EngineState s, _) = EngineTests.EvalScript(treePath, check, entrySection);
                if (check == ErrorCheck.Success || check == ErrorCheck.Warning)
                {
                    Dictionary<string, CodeCommand> macroDict = s.Macro.GetMacroDict(MacroType.Local);
                    Assert.IsTrue(macroDict.ContainsKey("SectionMacro"));
                    Assert.IsTrue(macroDict.ContainsKey("InlineMacro"));
                    Assert.IsTrue(macroDict.ContainsKey("CondMacro01"));
                    Assert.IsTrue(macroDict.ContainsKey("CondMacro02"));
                    Assert.IsTrue(macroDict.ContainsKey("PhoenixMacro"));

                    Assert.IsTrue(s.ReturnValue.Equals("T", StringComparison.Ordinal));
                }
            }

            string scPath = Path.Combine(EngineTests.Project.ProjectName, "Macro", "General.script");
            // ScriptTemplate(scPath, "Process-InlineMacro");
            // ScriptTemplate(scPath, "Process-SectionMacro");
            // ScriptTemplate(scPath, "Process-CondMacro01");
            // ScriptTemplate(scPath, "Process-CondMacro02");
            ScriptTemplate(scPath, "Process-PhoenixMacro");
        }
        #endregion
    }
}
