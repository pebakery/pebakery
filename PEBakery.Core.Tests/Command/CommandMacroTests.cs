using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
