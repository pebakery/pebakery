/*
    Copyright (C) 2017-2019 Hajin Jang
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
using PEBakery.Ini;
using System;
using System.Diagnostics;
using System.IO;
// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local

namespace PEBakery.Core.Tests.Command
{
    [TestClass]
    public class CommandControlTests
    {
        #region Set
        [TestMethod]
        [TestCategory("CommandControl")]
        public void Set()
        {
            EngineState s = EngineTests.CreateEngineState();

            SetLocal(s);
            DelLocal(s);
            SetGlobal(s);
            DelGlobal(s);
            SetDelPermanent(s);
            SetReturnValue(s);
            DelReturnValue(s);
            SetLoopCounter(s);
            DelLoopCounter(s);
        }

        public void SetLocal(EngineState s)
        {
            const string rawCode = "Set,%Dest%,PEBakery";
            EngineTests.Eval(s, rawCode, CodeType.Set, ErrorCheck.Success);

            const string comp = "PEBakery";
            string dest = s.Variables.GetValue(VarsType.Local, "Dest");
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }

        public void DelLocal(EngineState s)
        {
            s.Variables["Dest"] = "PEBakery";

            EngineTests.Eval(s, "Set,%Dest%,NIL", CodeType.Set, ErrorCheck.Success);

            string comp = string.Empty;
            string dest = s.Variables.GetValue(VarsType.Local, "Dest");
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }

        public void SetGlobal(EngineState s)
        {
            EngineTests.Eval(s, "Set,%Dest%,PEBakery,GLOBAL", CodeType.Set, ErrorCheck.Success);

            const string comp = "PEBakery";
            string dest = s.Variables.GetValue(VarsType.Global, "Dest");
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));

            s.Variables.GetVarDict(VarsType.Global).Remove("PEBakery");
        }

        public void DelGlobal(EngineState s)
        {
            s.Variables.SetValue(VarsType.Global, "Dest", "PEBakery");

            EngineTests.Eval(s, "Set,%Dest%,NIL", CodeType.Set, ErrorCheck.Success);

            string comp = string.Empty;
            string dest = s.Variables.GetValue(VarsType.Global, "Dest");
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }

        public void SetDelPermanent(EngineState s)
        {
            string scPath = s.Project.MainScript.RealPath;
            IniReadWriter.DeleteKey(scPath, "Variables", "%PermDest%");
            try
            {
                // Set
                EngineTests.Eval(s, "Set,%PermDest%,PEBakery,PERMANENT", CodeType.Set, ErrorCheck.Success);

                string dest = s.Variables.GetValue(VarsType.Global, "PermDest");
                Assert.IsTrue(dest.Equals("PEBakery", StringComparison.Ordinal));

                // Check memory-cached script section
                ScriptSection varSect = s.Project.MainScript.Sections["Variables"];
                int idx = Array.FindIndex(varSect.Lines, x => x.StartsWith("%PermDest%="));
                Assert.AreNotEqual(-1, idx);

                // Check script file
                string permanent = IniReadWriter.ReadKey(scPath, "Variables", "%PermDest%");
                Assert.IsTrue(dest.Equals(permanent, StringComparison.Ordinal));

                // Delete
                EngineTests.Eval(s, "Set,%PermDest%,NIL,PERMANENT", CodeType.Set, ErrorCheck.Success);

                // Check memory-cached script section
                idx = Array.FindIndex(varSect.Lines, x => x.StartsWith("%PermDest%="));
                Assert.AreEqual(-1, idx);

                // Check script file
                permanent = IniReadWriter.ReadKey(scPath, "Variables", "%PermDest%");
                Assert.IsNull(permanent);
            }
            finally
            {
                IniReadWriter.DeleteKey(scPath, "Variables", "%PermDest%");
            }
        }

        public void SetReturnValue(EngineState s)
        {
            const string rawCode = "Set,#r,PEBakery";

            // Turn off compat option
            s.CompatDisableExtendedSectionParams = false;
            s.ReturnValue = string.Empty;
            EngineTests.Eval(s, rawCode, CodeType.Set, ErrorCheck.Success);
            Assert.IsTrue(s.ReturnValue.Equals("PEBakery", StringComparison.Ordinal));

            // Turn on compat option
            s.CompatDisableExtendedSectionParams = true;
            s.ReturnValue = string.Empty;
            EngineTests.Eval(s, rawCode, CodeType.Set, ErrorCheck.Warning);
            Assert.IsTrue(s.ReturnValue.Length == 0);
        }

        public void DelReturnValue(EngineState s)
        {
            const string rawCode = "Set,#r,NIL";

            // Turn off compat option
            s.CompatDisableExtendedSectionParams = false;
            s.ReturnValue = "PEBakery";
            EngineTests.Eval(s, rawCode, CodeType.Set, ErrorCheck.Success);
            Assert.IsTrue(s.ReturnValue.Length == 0);

            // Turn on compat option
            s.CompatDisableExtendedSectionParams = true;
            s.ReturnValue = "PEBakery";
            EngineTests.Eval(s, rawCode, CodeType.Set, ErrorCheck.Success);
            Assert.IsTrue(s.ReturnValue.Equals("PEBakery", StringComparison.Ordinal));
        }

        public void SetLoopCounter(EngineState s)
        {
            s.LoopStateStack.Clear();

            // Simulate Loop command
            const string rawLoopCode = "Set,#c,110";

            s.CompatOverridableLoopCounter = true;
            s.LoopStateStack.Push(new EngineLoopState(100));
            EngineTests.Eval(s, rawLoopCode, CodeType.Set, ErrorCheck.Success);
            EngineLoopState loop = s.LoopStateStack.Pop();
            Assert.AreEqual(110, loop.CounterIndex);

            s.CompatOverridableLoopCounter = false;
            s.LoopStateStack.Push(new EngineLoopState(100));
            EngineTests.Eval(s, rawLoopCode, CodeType.Set, ErrorCheck.Warning);
            loop = s.LoopStateStack.Pop();
            Assert.AreEqual(100, loop.CounterIndex);

            // Simulate LoopLetter command
            const string rawLoopLetterCode = "Set,#c,Z";

            s.CompatOverridableLoopCounter = true;
            s.LoopStateStack.Push(new EngineLoopState('C'));
            EngineTests.Eval(s, rawLoopLetterCode, CodeType.Set, ErrorCheck.Success);
            loop = s.LoopStateStack.Pop();
            Assert.AreEqual('Z', loop.CounterLetter);

            s.CompatOverridableLoopCounter = false;
            s.LoopStateStack.Push(new EngineLoopState('C'));
            EngineTests.Eval(s, rawLoopLetterCode, CodeType.Set, ErrorCheck.Warning);
            loop = s.LoopStateStack.Pop();
            Assert.AreEqual('C', loop.CounterLetter);

            // Error 
            s.LoopStateStack.Clear();
            EngineTests.Eval(s, rawLoopCode, CodeType.Set, ErrorCheck.Warning);
            Assert.AreEqual(0, s.LoopStateStack.Count);

            s.LoopStateStack.Clear();
            EngineTests.Eval(s, rawLoopCode, CodeType.Set, ErrorCheck.Warning);
            Assert.AreEqual(0, s.LoopStateStack.Count);

            s.LoopStateStack.Clear();
            EngineTests.Eval(s, rawLoopLetterCode, CodeType.Set, ErrorCheck.Warning);
            Assert.AreEqual(0, s.LoopStateStack.Count);

            s.LoopStateStack.Clear();
            EngineTests.Eval(s, rawLoopLetterCode, CodeType.Set, ErrorCheck.Warning);
            Assert.AreEqual(0, s.LoopStateStack.Count);
        }

        public void DelLoopCounter(EngineState s)
        {
            const string rawCode = "Set,#c,NIL";

            s.CompatOverridableLoopCounter = true;
            s.LoopStateStack.Push(new EngineLoopState(100));
            EngineTests.Eval(s, rawCode, CodeType.Set, ErrorCheck.Warning);
            EngineLoopState loop = s.LoopStateStack.Pop();
            Assert.AreEqual(100, loop.CounterIndex);
            Assert.AreEqual('\0', loop.CounterLetter);

            s.LoopStateStack.Push(new EngineLoopState('C'));
            EngineTests.Eval(s, rawCode, CodeType.Set, ErrorCheck.Warning);
            loop = s.LoopStateStack.Pop();
            Assert.AreEqual(0, loop.CounterIndex);
            Assert.AreEqual('C', loop.CounterLetter);

            s.CompatOverridableLoopCounter = false;
            s.LoopStateStack.Push(new EngineLoopState(100));
            EngineTests.Eval(s, rawCode, CodeType.Set, ErrorCheck.Warning);
            loop = s.LoopStateStack.Pop();
            Assert.AreEqual(100, loop.CounterIndex);
            Assert.AreEqual('\0', loop.CounterLetter);

            s.LoopStateStack.Push(new EngineLoopState('C'));
            EngineTests.Eval(s, rawCode, CodeType.Set, ErrorCheck.Warning);
            loop = s.LoopStateStack.Pop();
            Assert.AreEqual(0, loop.CounterIndex);
            Assert.AreEqual('C', loop.CounterLetter);
        }
        #endregion

        #region AddVariables
        [TestMethod]
        [TestCategory("CommandControl")]
        public void AddVariables()
        {
            EngineState s = EngineTests.CreateEngineState();
            string scPath = Path.Combine(EngineTests.BaseDir, Project.Names.Projects, "TestSuite", "Control", "General.script");

            void Template(string rawCode, VarsType varsType)
            {
                EngineTests.Eval(s, rawCode, CodeType.AddVariables, ErrorCheck.Success);

                Assert.IsTrue(s.Variables.GetValue(varsType, "A").Equals("1", StringComparison.Ordinal));
                Assert.IsTrue(s.Variables.GetValue(varsType, "B").Equals("2", StringComparison.Ordinal));
                Assert.IsTrue(s.Variables.GetValue(varsType, "C").Equals("3", StringComparison.Ordinal));
            }

            Template($"AddVariables,{scPath},TestVars", VarsType.Local);
            Template($"AddVariables,{scPath},TestVars,GLOBAL", VarsType.Global);
        }
        #endregion

        #region Exit
        [TestMethod]
        [TestCategory("CommandControl")]
        public void Exit()
        {
            void Template(string rawCode, ErrorCheck check)
            {
                EngineState s = EngineTests.CreateEngineState();
                EngineTests.Eval(s, rawCode, CodeType.Exit, check);

                Assert.IsTrue(s.PassCurrentScriptFlag);
            }

            Template("Exit,UnitTest", ErrorCheck.Warning);
            Template("Exit,UnitTest,NOWARN", ErrorCheck.Success);
        }
        #endregion

        #region Halt
        [TestMethod]
        [TestCategory("CommandControl")]
        public void Halt()
        {
            const string rawCode = "Halt,UnitTest";
            EngineState s = EngineTests.CreateEngineState();
            EngineTests.Eval(s, rawCode, CodeType.Halt, ErrorCheck.Warning);

            Assert.IsTrue(s.CmdHaltFlag);
        }
        #endregion

        #region Wait
        [TestMethod]
        [TestCategory("CommandControl")]
        public void Wait()
        {
            Stopwatch w = Stopwatch.StartNew();

            const string rawCode = "Wait,1";
            EngineState s = EngineTests.CreateEngineState();
            EngineTests.Eval(s, rawCode, CodeType.Wait, ErrorCheck.Success);

            long elapsed = w.ElapsedMilliseconds;
            Assert.IsTrue(1000 <= elapsed);
        }
        #endregion

        #region Beep
        [TestMethod]
        [TestCategory("CommandControl")]
        public void Beep()
        {
            void Template(string rawCode, BeepType beepType)
            {
                CodeParser parser = new CodeParser(EngineTests.DummySection(), Global.Setting, EngineTests.Project.Compat);
                CodeCommand cmd = parser.ParseStatement(rawCode);

                CodeInfo_Beep info = cmd.Info.Cast<CodeInfo_Beep>();
                Assert.AreEqual(beepType, info.Type);
            }

            Template("Beep,OK", BeepType.OK);
            Template("Beep,Error", BeepType.Error);
            Template("Beep,Asterisk", BeepType.Asterisk);
            Template("Beep,Confirmation", BeepType.Confirmation);
        }
        #endregion

        #region GetParam
        [TestMethod]
        [TestCategory("CommandControl")]
        public void GetParam()
        {
            string scPath = Path.Combine(EngineTests.Project.ProjectName, "Control", "General.script");
            
            void ScriptTemplate(string treePath, string entrySection, ErrorCheck check = ErrorCheck.Success)
            {
                void SetState(EngineState es) => es.Project.Compat.LegacySectionParamCommand = false;

                (EngineState s, _) = EngineTests.EvalScript(treePath, check, SetState, entrySection);
                if (check == ErrorCheck.Success || check == ErrorCheck.Warning)
                {
                    string destStr = s.Variables.GetValue(VarsType.Local, "Dest");
                    Assert.IsTrue(destStr.Equals("T", StringComparison.Ordinal));
                }
            }

            ScriptTemplate(scPath, "Process-GetParam00");
            ScriptTemplate(scPath, "Process-GetParam01");
            ScriptTemplate(scPath, "Process-GetParam09");
            ScriptTemplate(scPath, "Process-GetParam12");
            ScriptTemplate(scPath, "Process-GetParam16");
            ScriptTemplate(scPath, "Process-GetParam18");
        }
        #endregion
    }
}
