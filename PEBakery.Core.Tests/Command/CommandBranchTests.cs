/*
    Copyright (C) 2017-2023 Hajin Jang
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
using PEBakery.Core.Commands;
using PEBakery.Helper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PEBakery.Core.Tests.Command
{
    [TestClass]
    [TestCategory("CommandBranch")]
    public class CommandBranchTests
    {
        #region RunExec
        [TestMethod]
        public void RunExec()
        {
            string scPath = Path.Combine(EngineTests.Project.ProjectName, "Branch", "General.script");

            static void ScriptTemplate(string treePath, string entrySection, ErrorCheck check = ErrorCheck.Success)
            {
                (EngineState s, _) = EngineTests.EvalScript(treePath, check, entrySection);
                if (check == ErrorCheck.Success || check == ErrorCheck.Warning)
                {
                    string destStr = s.Variables["Dest"];
                    Assert.IsTrue(destStr.Equals("T", StringComparison.Ordinal));
                }
            }

            ScriptTemplate(scPath, "Process-Run");
            ScriptTemplate(scPath, "Process-Run-InParam");
            ScriptTemplate(scPath, "Process-RunEx-OutParam");
            ScriptTemplate(scPath, "Process-Exec");
            ScriptTemplate(scPath, "Process-Exec-MacroBak");
            ScriptTemplate(scPath, "Process-RunEx-OutParam-Error", ErrorCheck.RuntimeError);
        }
        #endregion

        #region Loop
        [TestMethod]
        public void Loop()
        {
            string scPath = Path.Combine(EngineTests.Project.ProjectName, "Branch", "General.script");

            void ScriptTemplate(string treePath, string entrySection, string compStr, bool letterCompat, ErrorCheck check = ErrorCheck.Success)
            {
                void Setting(EngineState x) => x.CompatAllowLetterInLoop = letterCompat;

                (EngineState s, _) = EngineTests.EvalScript(treePath, check, Setting, entrySection);
                string destStr = s.Variables["Dest"];
                Assert.IsTrue(destStr.Equals(compStr, StringComparison.Ordinal));
            }

            ScriptTemplate(scPath, "Process-Loop01", "1|Z|2|Z|3|Z", false);
            ScriptTemplate(scPath, "Process-Loop02", "1|Z|2|Z|3|Z", false);
            ScriptTemplate(scPath, "Process-LoopEx-OutParam", "1|Z|2|Z|3|Z", false);
            ScriptTemplate(scPath, "Process-LoopEx-OutParam-Error", string.Empty, false, ErrorCheck.RuntimeError);
            ScriptTemplate(scPath, "Process-LoopLetter01", "C|Z|D|Z|E|Z", false);
            ScriptTemplate(scPath, "Process-LoopLetter02", "C|Z|D|Z|E|Z", false);
            ScriptTemplate(scPath, "Process-LoopLetterEx-OutParam", "C|Z|D|Z|E|Z", false);
            ScriptTemplate(scPath, "Process-LoopLetterEx-OutParam-Error", string.Empty, false, ErrorCheck.RuntimeError);

            ScriptTemplate(scPath, "Process-LoopCompat01", "C|Z|D|Z|E|Z", true);
            ScriptTemplate(scPath, "Process-LoopCompat02", "C|Z|D|Z|E|Z", true);
            ScriptTemplate(scPath, "Process-LoopCompat01", string.Empty, false, ErrorCheck.RuntimeError);
            ScriptTemplate(scPath, "Process-LoopCompat02", string.Empty, false, ErrorCheck.RuntimeError);

            ScriptTemplate(scPath, "Process-LoopNest", "1|4|5|1|2|4|5|2|3|4|5|3", false);
        }
        #endregion

        #region IfElse
        [TestMethod]
        public void IfElse()
        {
            string scPath = Path.Combine(EngineTests.Project.ProjectName, "Branch", "General.script");

            static void ScriptTemplate(string treePath, string entrySection, ErrorCheck check = ErrorCheck.Success)
            {
                (EngineState s, _) = EngineTests.EvalScript(treePath, check, entrySection);
                string destStr = s.Variables["Dest"];
                Assert.IsTrue(destStr.Equals("T", StringComparison.Ordinal));
            }

            ScriptTemplate(scPath, "Process-IfElse01");
            ScriptTemplate(scPath, "Process-IfElse02");
            ScriptTemplate(scPath, "Process-IfElseChain01");
            ScriptTemplate(scPath, "Process-IfElseChain02");
            ScriptTemplate(scPath, "Process-IfElseChain03");

            // It shoudl be a ParserError, but EvalScript treats it as RuntimeError.
            ScriptTemplate(scPath, "Process-NestedElseFlag-Error", ErrorCheck.RuntimeError);
        }
        #endregion

        #region IfBeginEnd
        [TestMethod]
        public void IfBeginEnd()
        {
            string scPath = Path.Combine(EngineTests.Project.ProjectName, "Branch", "General.script");

            static void ScriptTemplate(string treePath, string entrySection, ErrorCheck check = ErrorCheck.Success)
            {
                (EngineState s, _) = EngineTests.EvalScript(treePath, check, entrySection);
                string destStr = s.Variables["Dest"];
                Assert.IsTrue(destStr.Equals("T", StringComparison.Ordinal));
            }

            ScriptTemplate(scPath, "Process-IfBeginEnd");
        }
        #endregion

        #region ExistFile
        [TestMethod]

        [TestCategory("CommandBranch")]
        public void IfExistFile()
        {
            EngineState s = EngineTests.CreateEngineState();
            const BranchConditionType type = BranchConditionType.ExistFile;

            string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string kernel32 = Path.Combine(winDir, "System32", "kernel32.dll");
            string invalid = Path.GetTempFileName();
            File.Delete(invalid);

            BranchCondition cond = new BranchCondition(type, false, kernel32);
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, invalid);
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));

            cond = new BranchCondition(type, true, kernel32);
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, true, invalid);
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));

            SingleTemplate(s, $"If,ExistFile,{kernel32},Set,%Dest%,T", "T");
            SingleTemplate(s, $"If,ExistFile,{invalid},Set,%Dest%,T", "F");
            SingleTemplate(s, $"If,Not,ExistFile,{kernel32},Set,%Dest%,T", "F");
            SingleTemplate(s, $"If,Not,ExistFile,{invalid},Set,%Dest%,T", "T");
        }
        #endregion

        #region ExistDir
        [TestMethod]

        [TestCategory("CommandBranch")]
        public void IfExistDir()
        {
            EngineState s = EngineTests.CreateEngineState();
            const BranchConditionType type = BranchConditionType.ExistDir;

            string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string invalid = Path.GetTempFileName();
            File.Delete(invalid);

            BranchCondition cond = new BranchCondition(type, false, winDir);
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, invalid);
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));

            cond = new BranchCondition(type, true, winDir);
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, true, invalid);
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));

            SingleTemplate(s, $"If,ExistDir,{winDir},Set,%Dest%,T", "T");
            SingleTemplate(s, $"If,ExistDir,{invalid},Set,%Dest%,T", "F");
            SingleTemplate(s, $"If,Not,ExistDir,{winDir},Set,%Dest%,T", "F");
            SingleTemplate(s, $"If,Not,ExistDir,{invalid},Set,%Dest%,T", "T");
        }
        #endregion

        #region ExistSection
        [TestMethod]

        [TestCategory("CommandBranch")]
        public void IfExistSection()
        {
            EngineState s = EngineTests.CreateEngineState();
            const BranchConditionType type = BranchConditionType.ExistSection;

            string tempPath = FileHelper.GetTempFile();
            try
            {
                using (StreamWriter w = new StreamWriter(tempPath, false, Encoding.UTF8))
                {
                    w.WriteLine("[Hello]");
                    w.WriteLine("A=1");
                    w.WriteLine();
                    w.WriteLine("[World]");
                    w.WriteLine("B=2");
                }

                BranchCondition cond = new BranchCondition(type, false, tempPath, "Hello");
                Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
                cond = new BranchCondition(type, false, tempPath, "PEBakery");
                Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));

                cond = new BranchCondition(type, true, tempPath, "Hello");
                Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
                cond = new BranchCondition(type, true, tempPath, "PEBakery");
                Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));

                SingleTemplate(s, $"If,ExistSection,{tempPath},Hello,Set,%Dest%,T", "T");
                SingleTemplate(s, $"If,ExistSection,{tempPath},PEBakery,Set,%Dest%,T", "F");
                SingleTemplate(s, $"If,Not,ExistSection,{tempPath},Hello,Set,%Dest%,T", "F");
                SingleTemplate(s, $"If,Not,ExistSection,{tempPath},PEBakery,Set,%Dest%,T", "T");
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }
        #endregion

        #region ExistRegSubKey
        [TestMethod]

        [TestCategory("CommandBranch")]
        public void IfExistRegSubKey()
        {
            EngineState s = EngineTests.CreateEngineState();
            const BranchConditionType type = BranchConditionType.ExistRegSubKey;

            BranchCondition cond = new BranchCondition(type, false, "HKLM", @"SOFTWARE\Microsoft\DirectX");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "HKLM", @"SOFTWARE\Microsoft\DirectXNotExist");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));

            cond = new BranchCondition(type, true, "HKLM", @"SOFTWARE\Microsoft\DirectX");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, true, "HKLM", @"SOFTWARE\Microsoft\DirectXNotExist");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));

            SingleTemplate(s, @"If,ExistRegSection,HKLM,SOFTWARE\Microsoft\DirectX,Set,%Dest%,T", "T");
            SingleTemplate(s, @"If,ExistRegSubKey,HKLM,SOFTWARE\Microsoft\DirectXNotExist,Set,%Dest%,T", "F");
            SingleTemplate(s, @"If,Not,ExistRegSection,HKLM,SOFTWARE\Microsoft\DirectX,Set,%Dest%,T", "F");
            SingleTemplate(s, @"If,Not,ExistRegSubKey,HKLM,SOFTWARE\Microsoft\DirectXNotExist,Set,%Dest%,T", "T");
        }
        #endregion

        #region ExistRegValue
        [TestMethod]

        [TestCategory("CommandBranch")]
        public void IfExistRegValue()
        {
            EngineState s = EngineTests.CreateEngineState();
            const BranchConditionType type = BranchConditionType.ExistRegValue;

            BranchCondition cond = new BranchCondition(type, false, "HKLM", @"SOFTWARE\Microsoft\DirectX", "Version");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "HKLM", @"SOFTWARE\Microsoft\DirectNotMusic", "Version");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "HKLM", @"SOFTWARE\Microsoft\DirectX", "NoFilePath");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));

            cond = new BranchCondition(type, true, "HKLM", @"SOFTWARE\Microsoft\DirectX", "Version");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, true, "HKLM", @"SOFTWARE\Microsoft\DirectNotMusic", "Version");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, true, "HKLM", @"SOFTWARE\Microsoft\DirectX", "NoFilePath");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));

            SingleTemplate(s, @"If,ExistRegKey,HKLM,SOFTWARE\Microsoft\DirectX,Version,Set,%Dest%,T", "T");
            SingleTemplate(s, @"If,ExistRegValue,HKLM,SOFTWARE\Microsoft\DirectNotMusic,Version,Set,%Dest%,T", "F");
            SingleTemplate(s, @"If,ExistRegValue,HKLM,SOFTWARE\Microsoft\DirectX,NoFilePath,Set,%Dest%,T", "F");
            SingleTemplate(s, @"If,Not,ExistRegKey,HKLM,SOFTWARE\Microsoft\DirectX,Version,Set,%Dest%,T", "F");
            SingleTemplate(s, @"If,Not,ExistRegValue,HKLM,SOFTWARE\Microsoft\DirectNotMusic,Version,Set,%Dest%,T", "T");
            SingleTemplate(s, @"If,Not,ExistRegValue,HKLM,SOFTWARE\Microsoft\DirectX,NoFilePath,Set,%Dest%,T", "T");
        }
        #endregion

        #region ExistRegMulti
        [TestMethod]

        [TestCategory("CommandBranch")]
        public void IfExistRegMulti()
        {
            EngineState s = EngineTests.CreateEngineState();
            const BranchConditionType type = BranchConditionType.ExistRegMulti;

            BranchCondition cond = new BranchCondition(type, false, "HKLM", @"SYSTEM\ControlSet001\Control\ServiceGroupOrder", "List", "FSFilter Infrastructure");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "HKLM", @"SYSTEM\ControlSet001\Control\ServiceGroupOrder", "List", "DoesNotExist");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "HKLM", @"SYSTEM\ControlSet001\Control\ServiceProvider\Order", "ExcluedProviders", "EMS");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));

            cond = new BranchCondition(type, true, "HKLM", @"SYSTEM\ControlSet001\Control\ServiceGroupOrder", "List", "FSFilter Infrastructure");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, true, "HKLM", @"SYSTEM\ControlSet001\Control\ServiceGroupOrder", "List", "DoesNotExist");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, true, "HKLM", @"SYSTEM\ControlSet001\Control\ServiceProvider\Order", "ExcluedProviders", "EMS");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));

            SingleTemplate(s, @"If,ExistRegMulti,HKLM,SYSTEM\ControlSet001\Control\ServiceGroupOrder,List,FSFilter#$sInfrastructure,Set,%Dest%,T", "T");
            SingleTemplate(s, @"If,ExistRegMulti,HKLM,SYSTEM\ControlSet001\Control\ServiceGroupOrder,List,DoesNotExist,Set,%Dest%,T", "F");
            SingleTemplate(s, @"If,ExistRegMulti,HKLM,SYSTEM\ControlSet001\Control\ServiceProvider\Order,ExcluedProviders,EMS,Set,%Dest%,T", "F");
            SingleTemplate(s, @"If,Not,ExistRegMulti,HKLM,SYSTEM\ControlSet001\Control\ServiceGroupOrder,List,FSFilter#$sInfrastructure,Set,%Dest%,T", "F");
            SingleTemplate(s, @"If,Not,ExistRegMulti,HKLM,SYSTEM\ControlSet001\Control\ServiceGroupOrder,List,DoesNotExist,Set,%Dest%,T", "T");
            SingleTemplate(s, @"If,Not,ExistRegMulti,HKLM,SYSTEM\ControlSet001\Control\ServiceProvider\Order,ExcluedProviders,EMS,Set,%Dest%,T", "T");
        }
        #endregion

        #region ExistVar
        [TestMethod]

        [TestCategory("CommandBranch")]
        public void IfExistVar()
        {
            EngineState s = EngineTests.CreateEngineState();
            const BranchConditionType type = BranchConditionType.ExistVar;

            s.Variables.SetValue(VarsType.Fixed, "F", "ixed");
            s.Variables.SetValue(VarsType.Fixed, "G", "lobal");
            s.Variables.SetValue(VarsType.Fixed, "L", "ocal");

            BranchCondition cond = new BranchCondition(type, false, "%F%");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "%G%");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "%L%");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "%N%");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));

            cond = new BranchCondition(type, true, "%F%");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, true, "%G%");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, true, "%L%");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, true, "%N%");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));

            SingleTemplate(s, @"If,ExistVar,%F%,Set,%Dest%,T", "T");
            SingleTemplate(s, @"If,ExistVar,%G%,Set,%Dest%,T", "T");
            SingleTemplate(s, @"If,ExistVar,%L%,Set,%Dest%,T", "T");
            SingleTemplate(s, @"If,ExistVar,%N%,Set,%Dest%,T", "F");
            SingleTemplate(s, @"If,Not,ExistVar,%F%,Set,%Dest%,T", "F");
            SingleTemplate(s, @"If,Not,ExistVar,%G%,Set,%Dest%,T", "F");
            SingleTemplate(s, @"If,Not,ExistVar,%L%,Set,%Dest%,T", "F");
            SingleTemplate(s, @"If,Not,ExistVar,%N%,Set,%Dest%,T", "T");
        }
        #endregion

        #region ExistMacro
        [TestMethod]

        [TestCategory("CommandBranch")]
        public void IfExistMacro()
        {
            EngineState s = EngineTests.CreateEngineState();
            const BranchConditionType type = BranchConditionType.ExistMacro;

            // Test if Unicode can be used in macro name
            CodeParser parser = new CodeParser(EngineTests.DummySection(), Global.Setting, EngineTests.Project.Compat);
            s.Macro.GlobalDict["대한"] = parser.ParseStatement("Echo,사람");
            s.Macro.LocalDict["Sonic"] = parser.ParseStatement("Echo,Tails");
            s.Variables.SetValue(VarsType.Local, "Tails", "Sonic");

            BranchCondition cond = new BranchCondition(type, false, "대한");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "민국");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "Sonic");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "%Tails%");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));

            cond = new BranchCondition(type, true, "대한");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, true, "민국");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, true, "Sonic");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, true, "%Tails%");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));

            SingleTemplate(s, @"If,ExistMacro,대한,Set,%Dest%,T", "T");
            SingleTemplate(s, @"If,ExistMacro,민국,Set,%Dest%,T", "F");
            SingleTemplate(s, @"If,ExistMacro,Sonic,Set,%Dest%,T", "T");
            SingleTemplate(s, @"If,ExistMacro,%Tails%,Set,%Dest%,T", "T");
            SingleTemplate(s, @"If,Not,ExistMacro,대한,Set,%Dest%,T", "F");
            SingleTemplate(s, @"If,Not,ExistMacro,민국,Set,%Dest%,T", "T");
            SingleTemplate(s, @"If,Not,ExistMacro,Sonic,Set,%Dest%,T", "F");
            SingleTemplate(s, @"If,Not,ExistMacro,%Tails%,Set,%Dest%,T", "F");
        }
        #endregion

        #region WimExistIndex
        [TestMethod]

        [TestCategory("CommandBranch")]
        public void IfWimExistIndex()
        {
            EngineState s = EngineTests.CreateEngineState();
            const BranchConditionType type = BranchConditionType.WimExistIndex;

            string srcWim = Path.Combine("%TestBench%", "CommandWim", "MultiImage.wim");

            BranchCondition cond = new BranchCondition(type, false, srcWim, "0");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, srcWim, "1");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, srcWim, "2");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, srcWim, "3");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, srcWim, "4");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));

            cond = new BranchCondition(type, true, srcWim, "0");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, true, srcWim, "1");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, true, srcWim, "2");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, true, srcWim, "3");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, true, srcWim, "4");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));

            SingleTemplate(s, $"If,WimExistIndex,{srcWim},0,Set,%Dest%,T", "F");
            SingleTemplate(s, $"If,WimExistIndex,{srcWim},1,Set,%Dest%,T", "T");
            SingleTemplate(s, $"If,WimExistIndex,{srcWim},2,Set,%Dest%,T", "T");
            SingleTemplate(s, $"If,WimExistIndex,{srcWim},3,Set,%Dest%,T", "T");
            SingleTemplate(s, $"If,WimExistIndex,{srcWim},4,Set,%Dest%,T", "F");
            SingleTemplate(s, $"If,Not,WimExistIndex,{srcWim},0,Set,%Dest%,T", "T");
            SingleTemplate(s, $"If,Not,WimExistIndex,{srcWim},1,Set,%Dest%,T", "F");
            SingleTemplate(s, $"If,Not,WimExistIndex,{srcWim},2,Set,%Dest%,T", "F");
            SingleTemplate(s, $"If,Not,WimExistIndex,{srcWim},3,Set,%Dest%,T", "F");
            SingleTemplate(s, $"If,Not,WimExistIndex,{srcWim},4,Set,%Dest%,T", "T");
        }
        #endregion

        #region WimExistFile
        [TestMethod]

        [TestCategory("CommandBranch")]
        public void IfWimExistFile()
        {
            EngineState s = EngineTests.CreateEngineState();
            const BranchConditionType type = BranchConditionType.WimExistFile;

            string srcWim = Path.Combine("%TestBench%", "CommandWim", "MultiImage.wim");

            BranchCondition cond = new BranchCondition(type, false, srcWim, "1", "A.txt");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, srcWim, "1", "B");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));

            cond = new BranchCondition(type, true, srcWim, "1", "A.txt");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, true, srcWim, "1", "B");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));

            SingleTemplate(s, $"If,WimExistFile,{srcWim},1,A.txt,Set,%Dest%,T", "T");
            SingleTemplate(s, $"If,WimExistFile,{srcWim},1,B,Set,%Dest%,T", "F");
            SingleTemplate(s, $"If,Not,WimExistFile,{srcWim},1,A.txt,Set,%Dest%,T", "F");
            SingleTemplate(s, $"If,Not,WimExistFile,{srcWim},1,B,Set,%Dest%,T", "T");
        }
        #endregion

        #region WimExistDir
        [TestMethod]

        [TestCategory("CommandBranch")]
        public void IfWimExistDir()
        {
            EngineState s = EngineTests.CreateEngineState();
            const BranchConditionType type = BranchConditionType.WimExistDir;

            string srcWim = Path.Combine("%TestBench%", "CommandWim", "MultiImage.wim");

            BranchCondition cond = new BranchCondition(type, false, srcWim, "1", "A.txt");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, srcWim, "1", "B");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));

            cond = new BranchCondition(type, true, srcWim, "1", "A.txt");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, true, srcWim, "1", "B");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));

            SingleTemplate(s, $"If,WimExistDir,{srcWim},1,A.txt,Set,%Dest%,T", "F");
            SingleTemplate(s, $"If,WimExistDir,{srcWim},1,B,Set,%Dest%,T", "T");
            SingleTemplate(s, $"If,Not,WimExistDir,{srcWim},1,A.txt,Set,%Dest%,T", "T");
            SingleTemplate(s, $"If,Not,WimExistDir,{srcWim},1,B,Set,%Dest%,T", "F");
        }
        #endregion

        #region WimExistImageInfo
        [TestMethod]
        public void IfWimExistImageInfo()
        {
            EngineState s = EngineTests.CreateEngineState();
            const BranchConditionType type = BranchConditionType.WimExistImageInfo;

            string srcWim = Path.Combine("%TestBench%", "CommandWim", "LZX.wim");

            BranchCondition cond = new BranchCondition(type, false, srcWim, "0", "Name");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, srcWim, "1", "Name");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, srcWim, "1", "Dummy");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, srcWim, "2", "Name");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));

            cond = new BranchCondition(type, true, srcWim, "0", "Name");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, true, srcWim, "1", "Name");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, true, srcWim, "1", "Dummy");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, true, srcWim, "2", "Name");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));

            SingleTemplate(s, $"If,WimExistImageInfo,{srcWim},0,Name,Set,%Dest%,T", "F");
            SingleTemplate(s, $"If,WimExistImageInfo,{srcWim},1,Name,Set,%Dest%,T", "T");
            SingleTemplate(s, $"If,WimExistImageInfo,{srcWim},1,Dummy,Set,%Dest%,T", "F");
            SingleTemplate(s, $"If,WimExistImageInfo,{srcWim},2,Name,Set,%Dest%,T", "F");
            SingleTemplate(s, $"If,Not,WimExistImageInfo,{srcWim},0,Name,Set,%Dest%,T", "T");
            SingleTemplate(s, $"If,Not,WimExistImageInfo,{srcWim},1,Name,Set,%Dest%,T", "F");
            SingleTemplate(s, $"If,Not,WimExistImageInfo,{srcWim},1,Dummy,Set,%Dest%,T", "T");
            SingleTemplate(s, $"If,Not,WimExistImageInfo,{srcWim},2,Name,Set,%Dest%,T", "T");
        }
        #endregion

        #region Equal, NotEqual (!=)
        [TestMethod]

        [TestCategory("CommandBranch")]
        public void IfEqual()
        {
            EngineState s = EngineTests.CreateEngineState();
            const BranchConditionType type = BranchConditionType.Equal;

            // Equal
            BranchCondition cond = new BranchCondition(type, false, "A", "A");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "A", "B");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "a", "A");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));

            cond = new BranchCondition(type, true, "A", "A");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, true, "A", "B");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));

            cond = new BranchCondition(type, false, "11.1", "11.1.0");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, true, "11.1", "11.1.0");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "10.9", "11.1");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "12", "12");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "11.1.2.9", "11.1.2.3");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));

            cond = new BranchCondition(type, false, "5", "5.0");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "5", "5.1.2600");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));

            // WB082 does not recognize hex integer representation
            // PEBakery support hex integer representation
            cond = new BranchCondition(type, false, "11", "0xC");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "12", "0xC");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "13", "0xC");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));

            // Test for a bug reported in http://theoven.org/index.php?topic=2271.msg25381#msg25381
            cond = new BranchCondition(type, false, "-1", "0");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));

            ComparisonTemplate(s, "A", "If,%Src%,Equal,A,Set,%Dest%,T", "T");
            ComparisonTemplate(s, "A", "If,%Src%,Equal,a,Set,%Dest%,T", "T");
            ComparisonTemplate(s, "10", "If,%Src%,Equal,09,Set,%Dest%,T", "F");
            ComparisonTemplate(s, "10", "If,%Src%,Equal,10,Set,%Dest%,T", "T");
            ComparisonTemplate(s, "10", "If,%Src%,Equal,11,Set,%Dest%,T", "F");

            ComparisonTemplate(s, "A", "If,%Src%,==,A,Set,%Dest%,T", "T");
            ComparisonTemplate(s, "A", "If,%Src%,==,a,Set,%Dest%,T", "T");
            ComparisonTemplate(s, "10", "If,%Src%,==,09,Set,%Dest%,T", "F");
            ComparisonTemplate(s, "10", "If,%Src%,==,10,Set,%Dest%,T", "T");
            ComparisonTemplate(s, "10", "If,%Src%,==,11,Set,%Dest%,T", "F");

            ComparisonTemplate(s, "A", "If,Not,%Src%,Equal,A,Set,%Dest%,T", "F");
            ComparisonTemplate(s, "A", "If,%Src%,NotEqual,a,Set,%Dest%,T", "F");
            ComparisonTemplate(s, "10", "If,%Src%,NotEqual,09,Set,%Dest%,T", "T");
            ComparisonTemplate(s, "10", "If,Not,%Src%,Equal,10,Set,%Dest%,T", "F");
            ComparisonTemplate(s, "10", "If,Not,%Src%,Equal,11,Set,%Dest%,T", "T");

            ComparisonTemplate(s, "A", "If,%Src%,!=,A,Set,%Dest%,T", "F");
            ComparisonTemplate(s, "A", "If,%Src%,!=,a,Set,%Dest%,T", "F");
            ComparisonTemplate(s, "10", "If,%Src%,!=,09,Set,%Dest%,T", "T");
            ComparisonTemplate(s, "10", "If,%Src%,!=,10,Set,%Dest%,T", "F");
            ComparisonTemplate(s, "10", "If,%Src%,!=,11,Set,%Dest%,T", "T");

            // Ambiguity Test - WinBuilder treat this as a If,ExistSection command
            {
                s.Variables["Dest"] = "F";
                EngineTests.EvalLines(s, new List<string> { "If,ExistSection,Equal,ExistSection,Set,%Dest%,T" }, ErrorCheck.RuntimeError, out CodeCommand[] cmds);
                CodeInfo_If info = (CodeInfo_If)cmds[0].Info;
                Assert.AreEqual(BranchConditionType.ExistSection, info.Condition.Type);
            }
        }
        #endregion

        #region Smaller
        [TestMethod]

        [TestCategory("CommandBranch")]
        public void IfSmaller()
        {
            EngineState s = EngineTests.CreateEngineState();
            const BranchConditionType type = BranchConditionType.Smaller;

            BranchCondition cond = new BranchCondition(type, false, "11.1", "11.1.0");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, true, "11.1", "11.1.0");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "10.9", "11.1");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "12", "12");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "11.1.2.9", "11.1.2.3");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));

            cond = new BranchCondition(type, false, "5", "5.0");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "5", "5.1.2600");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));

            // WB082 will return lexicographic compare result of two strings.
            // PEBakery will ignore them and treat them as just NotEqual
            cond = new BranchCondition(type, false, "A", "A");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "A", "B");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "B", "A");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));

            // WB082 does not recognize hex integer representation
            // PEBakery support hex integer representation
            cond = new BranchCondition(type, false, "11", "0xC");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "12", "0xC");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "13", "0xC");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));

            ComparisonTemplate(s, "A", "If,%Src%,Smaller,A,Set,%Dest%,T", "F");
            ComparisonTemplate(s, "A", "If,%Src%,Smaller,a,Set,%Dest%,T", "F");
            ComparisonTemplate(s, "10", "If,%Src%,Smaller,09,Set,%Dest%,T", "F");
            ComparisonTemplate(s, "10", "If,%Src%,Smaller,10,Set,%Dest%,T", "F");
            ComparisonTemplate(s, "10", "If,%Src%,Smaller,11,Set,%Dest%,T", "T");

            ComparisonTemplate(s, "A", "If,%Src%,<,A,Set,%Dest%,T", "F");
            ComparisonTemplate(s, "A", "If,%Src%,<,a,Set,%Dest%,T", "F");
            ComparisonTemplate(s, "10", "If,%Src%,<,09,Set,%Dest%,T", "F");
            ComparisonTemplate(s, "10", "If,%Src%,<,10,Set,%Dest%,T", "F");
            ComparisonTemplate(s, "10", "If,%Src%,<,11,Set,%Dest%,T", "T");
        }
        #endregion

        #region SmallerEqual
        [TestMethod]

        [TestCategory("CommandBranch")]
        public void IfSmallerEqual()
        {
            EngineState s = EngineTests.CreateEngineState();
            const BranchConditionType type = BranchConditionType.SmallerEqual;

            BranchCondition cond = new BranchCondition(type, false, "11.1", "11.1.0");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, true, "11.1", "11.1.0");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "10.9", "11.1");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "12", "12");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "11.1.2.9", "11.1.2.3");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));

            cond = new BranchCondition(type, false, "5", "5.0");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "5", "5.1.2600");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));

            // WB082 will return lexicographic compare result of two strings.
            // PEBakery will ignore them and treat them as just NotEqual
            cond = new BranchCondition(type, false, "A", "A");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "A", "B");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "B", "A");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));

            // WB082 does not recognize hex integer representation
            // PEBakery support hex integer representation
            cond = new BranchCondition(type, false, "11", "0xC");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "12", "0xC");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "13", "0xC");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));

            ComparisonTemplate(s, "A", "If,%Src%,SmallerEqual,A,Set,%Dest%,T", "T");
            ComparisonTemplate(s, "A", "If,%Src%,SmallerEqual,a,Set,%Dest%,T", "T");
            ComparisonTemplate(s, "10", "If,%Src%,SmallerEqual,09,Set,%Dest%,T", "F");
            ComparisonTemplate(s, "10", "If,%Src%,SmallerEqual,10,Set,%Dest%,T", "T");
            ComparisonTemplate(s, "10", "If,%Src%,SmallerEqual,11,Set,%Dest%,T", "T");

            ComparisonTemplate(s, "A", "If,%Src%,<=,A,Set,%Dest%,T", "T");
            ComparisonTemplate(s, "A", "If,%Src%,<=,a,Set,%Dest%,T", "T");
            ComparisonTemplate(s, "10", "If,%Src%,<=,09,Set,%Dest%,T", "F");
            ComparisonTemplate(s, "10", "If,%Src%,<=,10,Set,%Dest%,T", "T");
            ComparisonTemplate(s, "10", "If,%Src%,<=,11,Set,%Dest%,T", "T");
        }
        #endregion

        #region Bigger
        [TestMethod]

        [TestCategory("CommandBranch")]
        public void IfBigger()
        {
            EngineState s = EngineTests.CreateEngineState();
            const BranchConditionType type = BranchConditionType.Bigger;

            BranchCondition cond = new BranchCondition(type, false, "11.1", "11.1.0");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, true, "11.1", "11.1.0");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "10.9", "11.1");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "12", "12");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "11.1.2.9", "11.1.2.3");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));

            cond = new BranchCondition(type, false, "5", "5.0");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "5", "5.1.2600");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));

            // WB082 will return lexicographic compare result of two strings.
            // PEBakery will ignore them and treat them as just NotEqual
            cond = new BranchCondition(type, false, "A", "A");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "A", "B");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "B", "A");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));

            // WB082 does not recognize hex integer representation
            // PEBakery support hex integer representation
            cond = new BranchCondition(type, false, "11", "0xC");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "12", "0xC");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "13", "0xC");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));

            ComparisonTemplate(s, "A", "If,%Src%,Bigger,A,Set,%Dest%,T", "F");
            ComparisonTemplate(s, "A", "If,%Src%,Bigger,a,Set,%Dest%,T", "F");
            ComparisonTemplate(s, "10", "If,%Src%,Bigger,09,Set,%Dest%,T", "T");
            ComparisonTemplate(s, "10", "If,%Src%,Bigger,10,Set,%Dest%,T", "F");
            ComparisonTemplate(s, "10", "If,%Src%,Bigger,11,Set,%Dest%,T", "F");

            ComparisonTemplate(s, "A", "If,%Src%,>,A,Set,%Dest%,T", "F");
            ComparisonTemplate(s, "A", "If,%Src%,>,a,Set,%Dest%,T", "F");
            ComparisonTemplate(s, "10", "If,%Src%,>,09,Set,%Dest%,T", "T");
            ComparisonTemplate(s, "10", "If,%Src%,>,10,Set,%Dest%,T", "F");
            ComparisonTemplate(s, "10", "If,%Src%,>,11,Set,%Dest%,T", "F");
        }
        #endregion

        #region BiggerEqual
        [TestMethod]

        [TestCategory("CommandBranch")]
        public void IfBiggerEqual()
        {
            EngineState s = EngineTests.CreateEngineState();
            const BranchConditionType type = BranchConditionType.BiggerEqual;

            BranchCondition cond = new BranchCondition(type, false, "11.1", "11.1.0");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, true, "11.1", "11.1.0");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "10.9", "11.1");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "12", "12");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "11.1.2.9", "11.1.2.3");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));

            cond = new BranchCondition(type, false, "5", "5.0");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "5", "5.1.2600");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));

            // WB082 will return lexicographic compare result of two strings.
            // PEBakery will ignore them and treat them as just NotEqual
            cond = new BranchCondition(type, false, "A", "A");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "A", "B");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "B", "A");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));

            // WB082 does not recognize hex integer representation
            // PEBakery support hex integer representation
            cond = new BranchCondition(type, false, "11", "0xC");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "12", "0xC");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "13", "0xC");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));

            ComparisonTemplate(s, "A", "If,%Src%,BiggerEqual,A,Set,%Dest%,T", "T");
            ComparisonTemplate(s, "A", "If,%Src%,BiggerEqual,a,Set,%Dest%,T", "T");
            ComparisonTemplate(s, "10", "If,%Src%,BiggerEqual,09,Set,%Dest%,T", "T");
            ComparisonTemplate(s, "10", "If,%Src%,BiggerEqual,10,Set,%Dest%,T", "T");
            ComparisonTemplate(s, "10", "If,%Src%,BiggerEqual,11,Set,%Dest%,T", "F");

            ComparisonTemplate(s, "A", "If,%Src%,>=,A,Set,%Dest%,T", "T");
            ComparisonTemplate(s, "A", "If,%Src%,>=,a,Set,%Dest%,T", "T");
            ComparisonTemplate(s, "10", "If,%Src%,>=,09,Set,%Dest%,T", "T");
            ComparisonTemplate(s, "10", "If,%Src%,>=,10,Set,%Dest%,T", "T");
            ComparisonTemplate(s, "10", "If,%Src%,>=,11,Set,%Dest%,T", "F");
        }
        #endregion

        #region EqualX
        [TestMethod]

        [TestCategory("CommandBranch")]
        public void IfEqualX()
        {
            EngineState s = EngineTests.CreateEngineState();
            const BranchConditionType type = BranchConditionType.EqualX;

            BranchCondition cond = new BranchCondition(type, false, "A", "A");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "A", "B");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "a", "A");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));

            cond = new BranchCondition(type, true, "A", "A");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, true, "A", "B");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));

            cond = new BranchCondition(type, false, "11.1", "11.1.0");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, true, "11.1", "11.1.0");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "10.9", "11.1");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "12", "12");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "11.1.2.9", "11.1.2.3");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));

            cond = new BranchCondition(type, false, "5", "5.0");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "5", "5.1.2600");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));

            // WB082 does not recognize hex integer representation
            // PEBakery support hex integer representation
            cond = new BranchCondition(type, false, "11", "0xC");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "12", "0xC");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "13", "0xC");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));

            ComparisonTemplate(s, "A", "If,%Src%,EqualX,A,Set,%Dest%,T", "T");
            ComparisonTemplate(s, "A", "If,%Src%,EqualX,a,Set,%Dest%,T", "F");
            ComparisonTemplate(s, "10", "If,%Src%,EqualX,09,Set,%Dest%,T", "F");
            ComparisonTemplate(s, "10", "If,%Src%,EqualX,10,Set,%Dest%,T", "T");
            ComparisonTemplate(s, "10", "If,%Src%,EqualX,11,Set,%Dest%,T", "F");

            ComparisonTemplate(s, "A", "If,%Src%,===,A,Set,%Dest%,T", "T");
            ComparisonTemplate(s, "A", "If,%Src%,===,a,Set,%Dest%,T", "F");
            ComparisonTemplate(s, "10", "If,%Src%,===,09,Set,%Dest%,T", "F");
            ComparisonTemplate(s, "10", "If,%Src%,===,10,Set,%Dest%,T", "T");
            ComparisonTemplate(s, "10", "If,%Src%,===,11,Set,%Dest%,T", "F");
        }
        #endregion

        #region Ping
        [TestMethod]

        [TestCategory("CommandBranch")]
        public void IfPing()
        {
            EngineState s = EngineTests.CreateEngineState();
            const BranchConditionType type = BranchConditionType.Ping;

            // According to https://www.iana.org/domains/root/db, root domain .zzz does not exist
            BranchCondition cond = new BranchCondition(type, false, "aaa.zzz");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "localhost");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "127.0.0.1");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, false, "::1");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));

            cond = new BranchCondition(type, true, "aaa.zzz");
            Assert.IsTrue(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, true, "localhost");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, true, "127.0.0.1");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));
            cond = new BranchCondition(type, true, "::1");
            Assert.IsFalse(CommandBranch.EvalBranchCondition(s, cond, out _));

            SingleTemplate(s, @"If,Ping,aaa.zzz,Set,%Dest%,T", "F");
            SingleTemplate(s, @"If,Ping,localhost,Set,%Dest%,T", "T");
            SingleTemplate(s, @"If,Ping,127.0.0.1,Set,%Dest%,T", "T");
            SingleTemplate(s, @"If,Ping,::1,Set,%Dest%,T", "T");
            SingleTemplate(s, @"If,Not,Ping,aaa.zzz,Set,%Dest%,T", "T");
            SingleTemplate(s, @"If,Not,Ping,localhost,Set,%Dest%,T", "F");
            SingleTemplate(s, @"If,Not,Ping,127.0.0.1,Set,%Dest%,T", "F");
            SingleTemplate(s, @"If,Not,Ping,::1,Set,%Dest%,T", "F");
        }
        #endregion

        #region While
        [TestMethod]
        public void While()
        {
            string scPath = Path.Combine(EngineTests.Project.ProjectName, "Branch", "General.script");

            static void ScriptTemplate(string treePath, string entrySection, string expected, ErrorCheck check = ErrorCheck.Success)
            {
                (EngineState s, _) = EngineTests.EvalScript(treePath, check, entrySection);
                string destStr = s.ReturnValue;
                Assert.IsTrue(destStr.Equals(expected, StringComparison.Ordinal));
            }

            ScriptTemplate(scPath, "Process-While-Simple", "AAA");
            ScriptTemplate(scPath, "Process-While-If", "AAA");
            ScriptTemplate(scPath, "Process-While-Nested", "AAAAAA");
            ScriptTemplate(scPath, "Process-While-Break01", "AAAA");
            ScriptTemplate(scPath, "Process-While-Break02", "AAA");
            ScriptTemplate(scPath, "Process-While-Continue01", "AAAA");
            ScriptTemplate(scPath, "Process-While-Continue02", "AAA");
        }
        #endregion

        #region ForEach
        [TestMethod]
        public void ForEach()
        {
            string scPath = Path.Combine(EngineTests.Project.ProjectName, "Branch", "General.script");

            static void ScriptTemplate(string treePath, string entrySection, string expected, ErrorCheck check = ErrorCheck.Success)
            {
                (EngineState s, _) = EngineTests.EvalScript(treePath, check, entrySection);
                string destStr = s.ReturnValue;
                Assert.IsTrue(destStr.Equals(expected, StringComparison.Ordinal));
            }

            ScriptTemplate(scPath, "Process-ForEach-IntList", "17");
            ScriptTemplate(scPath, "Process-ForEach-StrList", "TomatoAppleOrange");
            ScriptTemplate(scPath, "Process-ForEach-IdxList", "ACB");
        }
        #endregion

        #region ForRange
        [TestMethod]
        public void ForRange()
        {
            string scPath = Path.Combine(EngineTests.Project.ProjectName, "Branch", "General.script");

            static void ScriptTemplate(string treePath, string entrySection, string start, string end, string step, string? expected, ErrorCheck check = ErrorCheck.Success)
            {
                void SetEngineStateOptions(EngineState s)
                {
                    s.Variables.SetValue(VarsType.Global, "Start", start);
                    s.Variables.SetValue(VarsType.Global, "End", end);
                    s.Variables.SetValue(VarsType.Global, "Step", step);
                }
                (EngineState s, _) = EngineTests.EvalScript(treePath, check, SetEngineStateOptions, entrySection);
                if (check == ErrorCheck.Success || check == ErrorCheck.Warning)
                {
                    string destStr = s.ReturnValue;
                    Assert.IsTrue(destStr.Equals(expected, StringComparison.Ordinal));
                }
            }

            ScriptTemplate(scPath, "Process-ForRange-Param", "0", "5", "1", "01234");
            ScriptTemplate(scPath, "Process-ForRange-Param", "0", "7", "2", "0246");
            ScriptTemplate(scPath, "Process-ForRange-Param", "0", "3", "5", "0");
            ScriptTemplate(scPath, "Process-ForRange-Param", "0", "3", "0", null, ErrorCheck.RuntimeError);
            ScriptTemplate(scPath, "Process-ForRange-Param", "0", "3", "-1", null, ErrorCheck.RuntimeError);
            ScriptTemplate(scPath, "Process-ForRange-Param", "5", "0", "-1", "54321");
            ScriptTemplate(scPath, "Process-ForRange-Param", "7", "0", "-2", "7531");
            ScriptTemplate(scPath, "Process-ForRange-Param", "3", "0", "-5", "3");
            ScriptTemplate(scPath, "Process-ForRange-Param", "3", "0", "0", null, ErrorCheck.RuntimeError);
            ScriptTemplate(scPath, "Process-ForRange-Param", "3", "0", "1", null, ErrorCheck.RuntimeError);
        }
        #endregion

        #region ForRangeEach
        [TestMethod]
        public void ForRangeEach()
        {
            string scPath = Path.Combine(EngineTests.Project.ProjectName, "Branch", "General.script");

            static void ScriptTemplate(string treePath, string entrySection, string expected, ErrorCheck check = ErrorCheck.Success)
            {
                (EngineState s, _) = EngineTests.EvalScript(treePath, check, entrySection);
                string destStr = s.ReturnValue;
                Assert.IsTrue(destStr.Equals(expected, StringComparison.Ordinal));
            }

            ScriptTemplate(scPath, "Process-ForRangeEach-Nested", "0X0Y1X1Y3X3YY3");
        }
        #endregion

        #region LoopSyntaxParams
        [TestMethod]
        public void LoopSyntaxParams()
        {
            string scPath = Path.Combine(EngineTests.Project.ProjectName, "Branch", "General.script");

            static void ScriptTemplate(string treePath, string entrySection, string expected, ErrorCheck check = ErrorCheck.Success)
            {
                (EngineState s, _) = EngineTests.EvalScript(treePath, check, entrySection);
                string destStr = s.ReturnValue;
                Console.WriteLine($"ReturnValue={destStr}");
                Assert.IsTrue(destStr.Equals(expected, StringComparison.Ordinal));
            }

            ScriptTemplate(scPath, "Process-LoopSyntaxParams", "Hello_cs-CZ");
        }
        #endregion

        #region Utility
        public static void SingleTemplate(EngineState s, string rawCode, string comp, ErrorCheck check = ErrorCheck.Success)
        { // Use EvalLines instead of Eval, because Eval does not fold embedded command of If/Else
            s.Variables["Dest"] = "F";
            EngineTests.EvalLines(s, new List<string> { rawCode }, check);
            Assert.IsTrue(s.Variables["Dest"].Equals(comp, StringComparison.Ordinal));
        }

        public static void ComparisonTemplate(EngineState s, string src, string rawCode, string comp, ErrorCheck check = ErrorCheck.Success)
        { // Use EvalLines instead of Eval, because Eval does not fold embedded command of If/Else
            s.Variables["Src"] = src;
            s.Variables["Dest"] = "F";
            EngineTests.EvalLines(s, new List<string> { rawCode }, check);
            Assert.IsTrue(s.Variables["Dest"].Equals(comp, StringComparison.Ordinal));
        }
        #endregion
    }
}
