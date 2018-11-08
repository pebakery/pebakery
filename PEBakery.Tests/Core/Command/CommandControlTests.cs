/*
    Copyright (C) 2017-2018 Hajin Jang
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
using PEBakery.Core;
using PEBakery.Ini;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace PEBakery.Tests.Core.Command
{
    [TestClass]
    public class CommandControlTests
    {
        #region Set
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandControl")]
        public void Set()
        {
            EngineState s = EngineTests.CreateEngineState();

            SetLocal(s);
            DelLocal(s);
            SetGlobal(s);
            DelGlobal(s);
            SetDelPermanent(s);
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
        #endregion

        #region AddVariables
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandControl")]
        public void AddVariables()
        {
            AddVariables_1();
            AddVariables_2();
        }

        public void AddVariables_1()
        { // AddVariables,%PluginFile%,<Section>[,GLOBAL]
            EngineState s = EngineTests.CreateEngineState();
            const string tempFile = "AddVariables_1.script";
            string pPath = Path.Combine(s.BaseDir, "Temp", s.Project.ProjectName, tempFile);
            string pDir = Path.GetDirectoryName(pPath);
            Assert.IsNotNull(pDir);
            Directory.CreateDirectory(pDir);

            using (StreamWriter w = new StreamWriter(pPath, false, Encoding.UTF8))
            {
                w.WriteLine("[TestVars]");
                w.WriteLine("%A%=1");
                w.WriteLine("%B%=2");
                w.WriteLine("%C%=3");
            }

            string rawCode = $@"AddVariables,%ProjectTemp%\{tempFile},TestVars";
            EngineTests.Eval(s, rawCode, CodeType.AddVariables, ErrorCheck.Success);

            Assert.IsTrue(s.Variables.GetValue(VarsType.Local, "A").Equals("1", StringComparison.Ordinal));
            Assert.IsTrue(s.Variables.GetValue(VarsType.Local, "B").Equals("2", StringComparison.Ordinal));
            Assert.IsTrue(s.Variables.GetValue(VarsType.Local, "C").Equals("3", StringComparison.Ordinal));

            File.Delete(pPath);
        }

        public void AddVariables_2()
        { // AddVariables,%PluginFile%,<Section>[,GLOBAL]
            EngineState s = EngineTests.CreateEngineState();
            const string tempFile = "AddVariables_2.script";
            string pPath = Path.Combine(s.BaseDir, "Temp", s.Project.ProjectName, tempFile);
            string pDir = Path.GetDirectoryName(pPath);
            Assert.IsNotNull(pDir);
            Directory.CreateDirectory(pDir);

            using (StreamWriter w = new StreamWriter(pPath, false, Encoding.UTF8))
            {
                w.WriteLine("[TestVars]");
                w.WriteLine("%A%=1");
                w.WriteLine("%B%=2");
                w.WriteLine("%C%=3");
            }

            string rawCode = $"AddVariables,%ProjectTemp%\\{tempFile},TestVars,GLOBAL";
            EngineTests.Eval(s, rawCode, CodeType.AddVariables, ErrorCheck.Success);

            Assert.IsTrue(s.Variables.GetValue(VarsType.Global, "A").Equals("1", StringComparison.Ordinal));
            Assert.IsTrue(s.Variables.GetValue(VarsType.Global, "B").Equals("2", StringComparison.Ordinal));
            Assert.IsTrue(s.Variables.GetValue(VarsType.Global, "C").Equals("3", StringComparison.Ordinal));

            File.Delete(pPath);
        }
        #endregion

        #region Exit
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandControl")]
        public void Exit()
        {
            Exit_1();
            Exit_2();
        }

        public void Exit_1()
        {
            const string rawCode = "Exit,UnitTest";
            EngineState s = EngineTests.CreateEngineState();
            EngineTests.Eval(s, rawCode, CodeType.Exit, ErrorCheck.Warning);

            Assert.IsTrue(s.PassCurrentScriptFlag);
        }

        public void Exit_2()
        {
            const string rawCode = "Exit,UnitTest,NOWARN";
            EngineState s = EngineTests.CreateEngineState();
            EngineTests.Eval(s, rawCode, CodeType.Exit, ErrorCheck.Success);

            Assert.IsTrue(s.PassCurrentScriptFlag);
        }
        #endregion

        #region Halt
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandControl")]
        public void Halt()
        {
            Halt_1();
        }

        public void Halt_1()
        {
            const string rawCode = "Halt,UnitTest";
            EngineState s = EngineTests.CreateEngineState();
            EngineTests.Eval(s, rawCode, CodeType.Halt, ErrorCheck.Warning);

            Assert.IsTrue(s.CmdHaltFlag);
        }
        #endregion

        #region Wait
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandControl")]
        public void Wait()
        {
            Wait_1();
        }

        public void Wait_1()
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
        [TestCategory("Command")]
        [TestCategory("CommandControl")]
        public void Beep()
        {
            EngineState s = EngineTests.CreateEngineState();

            Beep_Template(s, "Beep,OK", BeepType.OK);
            Beep_Template(s, "Beep,Error", BeepType.Error);
            Beep_Template(s, "Beep,Asterisk", BeepType.Asterisk);
            Beep_Template(s, "Beep,Confirmation", BeepType.Confirmation);
        }

        public void Beep_Template(EngineState s, string rawCode, BeepType beepType)
        {
            CodeParser parser = new CodeParser(EngineTests.DummySection(), Global.Setting.ExportCodeParserOptions());
            CodeCommand cmd = parser.ParseStatement(rawCode);

            CodeInfo_Beep info = cmd.Info.Cast<CodeInfo_Beep>();
            Assert.IsTrue(info.Type == beepType);
        }
        #endregion
    }
}
