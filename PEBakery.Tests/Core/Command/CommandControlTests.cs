/*
    Copyright (C) 2017 Hajin Jang
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.Core;
using PEBakery.IniLib;
using System.IO;
using System.Text;
using System.Diagnostics;

namespace PEBakery.Tests.Core.Command
{
    [TestClass]
    public class CommandControlTests
    {
        #region Set
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandControl")]
        public void Control_Set()
        {
            Set_1();
            Set_2();
            Set_3();
        }

        public void Set_1()
        {
            string rawCode = "Set,%Dest%,PEBakery";
            EngineState s = EngineTests.Eval(rawCode, CodeType.Set, ErrorCheck.Success);

            string comp = "PEBakery";
            string dest = s.Variables.GetValue(VarsType.Local, "Dest");
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }

        public void Set_2()
        {
            string rawCode = "Set,%Dest%,PEBakery,GLOBAL";
            EngineState s = EngineTests.Eval(rawCode, CodeType.Set, ErrorCheck.Success);

            string comp = "PEBakery";
            string dest = s.Variables.GetValue(VarsType.Global, "Dest");
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));

            s.Variables.GetVars(VarsType.Global).Remove("PEBakery");
        }

        public void Set_3()
        {
            EngineState s = EngineTests.CreateEngineState();

            string pPath = s.Project.MainPlugin.FullPath;
            Ini.DeleteKey(pPath, "Variables", "%Set_3%");

            string rawCode = "Set,%Set_3%,PEBakery,PERMANENT";
            EngineTests.Eval(s, rawCode, CodeType.Set, ErrorCheck.Success);

            string comp = "PEBakery";
            string dest = s.Variables.GetValue(VarsType.Global, "Set_3");
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));

            string permanent = Ini.GetKey(pPath, "Variables", "%Set_3%");
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));

            Ini.DeleteKey(pPath, "Variables", "%Set_3%");
        }
        #endregion

        #region AddVariables
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandControl")]
        public void Control_AddVariables()
        {
            AddVariables_1();
            AddVariables_2();
        }

        public void AddVariables_1()
        { // AddVariables,%PluginFile%,<Section>[,GLOBAL]
            EngineState s = EngineTests.CreateEngineState();
            string tempFile = "AddVariables_1.script";
            string pPath = Path.Combine(s.BaseDir, "Temp", s.Project.ProjectName, tempFile);
            Directory.CreateDirectory(Path.GetDirectoryName(pPath));

            using (StreamWriter w = new StreamWriter(pPath, false, Encoding.UTF8))
            {
                w.WriteLine("[TestVars]");
                w.WriteLine("%A%=1");
                w.WriteLine("%B%=2");
                w.WriteLine("%C%=3");
                w.Close();
            }

            string rawCode = $"AddVariables,%ProjectTemp%\\{tempFile},TestVars";
            EngineTests.Eval(s, rawCode, CodeType.AddVariables, ErrorCheck.Success);

            Assert.IsTrue(s.Variables.GetValue(VarsType.Local, "A").Equals("1", StringComparison.Ordinal));
            Assert.IsTrue(s.Variables.GetValue(VarsType.Local, "B").Equals("2", StringComparison.Ordinal));
            Assert.IsTrue(s.Variables.GetValue(VarsType.Local, "C").Equals("3", StringComparison.Ordinal));

            File.Delete(pPath);
        }

        public void AddVariables_2()
        { // AddVariables,%PluginFile%,<Section>[,GLOBAL]
            EngineState s = EngineTests.CreateEngineState();
            string tempFile = "AddVariables_2.script";
            string pPath = Path.Combine(s.BaseDir, "Temp", s.Project.ProjectName, tempFile);
            Directory.CreateDirectory(Path.GetDirectoryName(pPath));

            using (StreamWriter w = new StreamWriter(pPath, false, Encoding.UTF8))
            {
                w.WriteLine("[TestVars]");
                w.WriteLine("%A%=1");
                w.WriteLine("%B%=2");
                w.WriteLine("%C%=3");
                w.Close();
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
        public void Control_Exit()
        {
            Exit_1();
            Exit_2();
        }

        public void Exit_1()
        { 
            string rawCode = $"Exit,UnitTest";
            EngineState s = EngineTests.Eval(rawCode, CodeType.Exit, ErrorCheck.Warning);

            Assert.IsTrue(s.PassCurrentPluginFlag);
        }

        public void Exit_2()
        {
            string rawCode = $"Exit,UnitTest,NOWARN";
            EngineState s = EngineTests.Eval(rawCode, CodeType.Exit, ErrorCheck.Success);

            Assert.IsTrue(s.PassCurrentPluginFlag);
        }
        #endregion

        #region Halt
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandControl")]
        public void Control_Halt()
        {
            Halt_1();
        }

        public void Halt_1()
        {
            string rawCode = $"Halt,UnitTest";
            EngineState s = EngineTests.Eval(rawCode, CodeType.Halt, ErrorCheck.Warning);

            Assert.IsTrue(s.CmdHaltFlag);
        }
        #endregion

        #region Wait
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandControl")]
        public void Control_Wait()
        {
            Wait_1();
        }

        public void Wait_1()
        {
            Stopwatch w = Stopwatch.StartNew();
            
            string rawCode = $"Wait,1";
            EngineState s = EngineTests.Eval(rawCode, CodeType.Wait, ErrorCheck.Success);

            long elapsed = w.ElapsedMilliseconds;
            Assert.IsTrue(1000 <= elapsed);
        }
        #endregion

        #region Beep
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandControl")]
        public void Control_Beep()
        {
            EngineState s = EngineTests.CreateEngineState();

            Beep_Template(s, "Beep,OK", BeepType.OK);
            Beep_Template(s, "Beep,Error", BeepType.Error);
            Beep_Template(s, "Beep,Asterisk", BeepType.Asterisk);
            Beep_Template(s, "Beep,Confirmation", BeepType.Confirmation);
        }

        public void Beep_Template(EngineState s, string rawCode, BeepType beepType)
        {
            SectionAddress addr = EngineTests.DummySectionAddress();
            CodeCommand cmd = CodeParser.ParseRawLine(rawCode, addr);

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Beep));
            CodeInfo_Beep info = cmd.Info as CodeInfo_Beep;

            Assert.IsTrue(info.Type == beepType);
        }
        #endregion
    }
}
