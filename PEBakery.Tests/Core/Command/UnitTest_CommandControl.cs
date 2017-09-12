using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.Core;
using PEBakery.Lib;
using System.IO;
using System.Text;
using System.Diagnostics;

namespace UnitTest.Core.Command
{
    [TestClass]
    public class UnitTest_CommandControl
    {
        #region Set
        [TestMethod]
        [TestCategory("CommandControl")]
        public void CommandControl_Set()
        {
            Set_1();
            Set_2();
            Set_3();
        }

        public void Set_1()
        {
            string rawCode = "Set,%Dest%,PEBakery";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.Set, ErrorCheck.Success);

            string comp = "PEBakery";
            string dest = s.Variables.GetValue(VarsType.Local, "Dest");
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }

        public void Set_2()
        {
            string rawCode = "Set,%Dest%,PEBakery,GLOBAL";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.Set, ErrorCheck.Success);

            string comp = "PEBakery";
            string dest = s.Variables.GetValue(VarsType.Global, "Dest");
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }

        public void Set_3()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();

            string pPath = s.Project.MainPlugin.FullPath;
            Ini.DeleteKey(pPath, "Variables", "%Set_3%");

            string rawCode = "Set,%Set_3%,PEBakery,PERMANENT";
            UnitTest_Engine.Eval(s, rawCode, CodeType.Set, ErrorCheck.Success);

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
        [TestCategory("CommandControl")]
        public void CommandControl_AddVariables()
        {
            AddVariables_1();
            AddVariables_2();
        }

        public void AddVariables_1()
        { // AddVariables,%PluginFile%,<Section>[,GLOBAL]
            EngineState s = UnitTest_Engine.CreateEngineState();
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
            UnitTest_Engine.Eval(s, rawCode, CodeType.AddVariables, ErrorCheck.Success);

            Assert.IsTrue(s.Variables.GetValue(VarsType.Local, "A").Equals("1", StringComparison.Ordinal));
            Assert.IsTrue(s.Variables.GetValue(VarsType.Local, "B").Equals("2", StringComparison.Ordinal));
            Assert.IsTrue(s.Variables.GetValue(VarsType.Local, "C").Equals("3", StringComparison.Ordinal));

            File.Delete(pPath);
        }

        public void AddVariables_2()
        { // AddVariables,%PluginFile%,<Section>[,GLOBAL]
            EngineState s = UnitTest_Engine.CreateEngineState();
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
            UnitTest_Engine.Eval(s, rawCode, CodeType.AddVariables, ErrorCheck.Success);

            Assert.IsTrue(s.Variables.GetValue(VarsType.Global, "A").Equals("1", StringComparison.Ordinal));
            Assert.IsTrue(s.Variables.GetValue(VarsType.Global, "B").Equals("2", StringComparison.Ordinal));
            Assert.IsTrue(s.Variables.GetValue(VarsType.Global, "C").Equals("3", StringComparison.Ordinal));

            File.Delete(pPath);
        }
        #endregion

        #region Exit
        [TestMethod]
        [TestCategory("CommandControl")]
        public void CommandControl_Exit()
        {
            Exit_1();
            Exit_2();
        }

        public void Exit_1()
        { 
            string rawCode = $"Exit,UnitTest";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.Exit, ErrorCheck.Warning);

            Assert.IsTrue(s.PassCurrentPluginFlag);
        }

        public void Exit_2()
        {
            string rawCode = $"Exit,UnitTest,NOWARN";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.Exit, ErrorCheck.Success);

            Assert.IsTrue(s.PassCurrentPluginFlag);
        }
        #endregion

        #region Halt
        [TestMethod]
        [TestCategory("CommandControl")]
        public void CommandControl_Halt()
        {
            Halt_1();
        }

        public void Halt_1()
        {
            string rawCode = $"Halt,UnitTest";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.Halt, ErrorCheck.Warning);

            Assert.IsTrue(s.CmdHaltFlag);
        }
        #endregion

        #region Wait
        [TestMethod]
        [TestCategory("CommandControl")]
        public void CommandControl_Wait()
        {
            Wait_1();
        }

        public void Wait_1()
        {
            Stopwatch w = Stopwatch.StartNew();
            
            string rawCode = $"Wait,1";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.Wait, ErrorCheck.Success);

            long elapsed = w.ElapsedMilliseconds;
            Assert.IsTrue(1000 <= elapsed);
        }
        #endregion

        #region Beep
        [TestMethod]
        [TestCategory("CommandControl")]
        public void CommandControl_Beep()
        {
            Beep_1();
            Beep_2();
            Beep_3();
            Beep_4();
        }

        public void Beep_1()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            SectionAddress addr = UnitTest_Engine.DummySectionAddress();
            string rawCode = $"Beep,OK";
            CodeCommand cmd = CodeParser.ParseOneRawLine(rawCode, addr);

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Beep));
            CodeInfo_Beep info = cmd.Info as CodeInfo_Beep;

            Assert.IsTrue(info.Type == BeepType.OK);
        }

        public void Beep_2()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            SectionAddress addr = UnitTest_Engine.DummySectionAddress();
            string rawCode = $"Beep,Error";
            CodeCommand cmd = CodeParser.ParseOneRawLine(rawCode, addr);

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Beep));
            CodeInfo_Beep info = cmd.Info as CodeInfo_Beep;

            Assert.IsTrue(info.Type == BeepType.Error);
        }

        public void Beep_3()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            SectionAddress addr = UnitTest_Engine.DummySectionAddress();
            string rawCode = $"Beep,Asterisk";
            CodeCommand cmd = CodeParser.ParseOneRawLine(rawCode, addr);

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Beep));
            CodeInfo_Beep info = cmd.Info as CodeInfo_Beep;

            Assert.IsTrue(info.Type == BeepType.Asterisk);
        }

        public void Beep_4()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            SectionAddress addr = UnitTest_Engine.DummySectionAddress();
            string rawCode = $"Beep,Confirmation";
            CodeCommand cmd = CodeParser.ParseOneRawLine(rawCode, addr);

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Beep));
            CodeInfo_Beep info = cmd.Info as CodeInfo_Beep;

            Assert.IsTrue(info.Type == BeepType.Confirmation);
        }
        #endregion
    }
}
