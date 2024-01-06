/*
    Copyright (C) 2022 Hajin Jang
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
using System.Collections.Generic;

namespace PEBakery.Core.Tests
{
    [TestClass]
    public class CodeOptimizerTests
    {
        [TestMethod]
        [TestCategory(nameof(CodeOptimizer))]
        public void DepedencyHazard()
        {
            // Groups should be breaked 1-2
            {
                string[] lines = new string[]
                {
                    "IniRead,%SrcFile%,Section,Key,%Dest%",
                    // -- Should be separated --
                    "IniRead,%SrcFile%,%Dest%,Key,%Dest%",
                    "IniRead,%SrcFile%,Section,Key,%Dest%",
                };
                CodeCommand[] cmds = EngineTests.ParseLines(lines, out List<LogInfo> errorLogs);
                Assert.IsTrue(errorLogs.Count == 0);
                Assert.AreEqual(2, cmds.Length);

                IsSingleCommand(cmds[0], CodeType.IniRead);
                IsOptimizedCommand(cmds[1], CodeType.IniReadOp, 2);
            }
            // Groups should be breaked 2-1
            {
                string[] lines = new string[]
                {
                    "IniRead,%SrcFile%,Section,Key,%Dest%",
                    "IniRead,%SrcFile%,Section,Key,%Dest%",
                    // -- Should be separated --
                    "IniRead,%SrcFile%,%Dest%,Key,%Dest%",
                };
                CodeCommand[] cmds = EngineTests.ParseLines(lines, out List<LogInfo> errorLogs);
                Assert.IsTrue(errorLogs.Count == 0);
                Assert.AreEqual(2, cmds.Length);

                IsOptimizedCommand(cmds[0], CodeType.IniReadOp, 2);
                IsSingleCommand(cmds[1], CodeType.IniRead);
            }
        }

        [TestMethod]
        [TestCategory(nameof(CodeOptimizer))]
        public void MiddleComment()
        {
            string[] lines = new string[]
            {
                "IniRead,%SrcFile%,A,B,%D1%",
                "IniRead,%SrcFile%,A,B,%D2%",
                "// Comment HERE",
                "IniRead,%SrcFile%,A,B,%D3%",
            };
            CodeCommand[] cmds = EngineTests.ParseLines(lines, out List<LogInfo> errorLogs);
            Assert.IsTrue(errorLogs.Count == 0);
            Assert.AreEqual(1, cmds.Length);

            IsOptimizedCommand(cmds[0], CodeType.IniReadOp, 3);
        }

        [TestMethod]
        [TestCategory(nameof(CodeOptimizer))]
        public void MultipleCodeTypes()
        {
            // Without comments
            {
                string[] lines = new string[]
                {
                    "IniRead,%SrcFile%,A,B,%D1%",
                    "IniRead,%SrcFile%,A,B,%D2%",
                    "WimExtract,\"%BaseDir%\\install.wim\",3,\"\\Windows\",%Target%",
                    "WimExtract,\"%BaseDir%\\install.wim\",3,\"\\Program Files\",%Target%",
                };
                CodeCommand[] cmds = EngineTests.ParseLines(lines, out List<LogInfo> errorLogs);
                Assert.IsTrue(errorLogs.Count == 0);
                Assert.AreEqual(2, cmds.Length);

                IsOptimizedCommand(cmds[0], CodeType.IniReadOp, 2);
                IsOptimizedCommand(cmds[1], CodeType.WimExtractOp, 2);
            }

            // With comments (1)
            {
                string[] lines = new string[]
                {
                    "IniRead,%SrcFile%,A,B,%D1%",
                    "// Comment HERE",
                    "IniRead,%SrcFile%,A,B,%D2%",
                    "WimExtract,\"%BaseDir%\\install.wim\",3,\"\\Windows\",%Target%",
                    "// Comment HERE",
                    "WimExtract,\"%BaseDir%\\install.wim\",3,\"\\Program Files\",%Target%",
                };
                CodeCommand[] cmds = EngineTests.ParseLines(lines, out List<LogInfo> errorLogs);
                Assert.IsTrue(errorLogs.Count == 0);
                Assert.AreEqual(2, cmds.Length);

                IsOptimizedCommand(cmds[0], CodeType.IniReadOp, 2);
                IsOptimizedCommand(cmds[1], CodeType.WimExtractOp, 2);
            }

            // With comments (2)
            {
                string[] lines = new string[]
                {
                    "IniRead,%SrcFile%,A,B,%D1%",
                    "IniRead,%SrcFile%,A,B,%D2%",
                    "// Comment HERE",
                    "WimExtract,\"%BaseDir%\\install.wim\",3,\"\\Windows\",%Target%",
                    "WimExtract,\"%BaseDir%\\install.wim\",3,\"\\Program Files\",%Target%",
                };
                CodeCommand[] cmds = EngineTests.ParseLines(lines, out List<LogInfo> errorLogs);
                Assert.IsTrue(errorLogs.Count == 0);
                Assert.AreEqual(3, cmds.Length);

                IsOptimizedCommand(cmds[0], CodeType.IniReadOp, 2);
                IsSingleCommand(cmds[1], CodeType.Comment);
                IsOptimizedCommand(cmds[2], CodeType.WimExtractOp, 2);
            }

            // With unoptimizable command
            {
                string[] lines = new string[]
                {
                    "IniRead,%SrcFile%,A,B,%D1%",
                    "IniRead,%SrcFile%,A,B,%D2%",
                    "Echo,Dummy",
                    "WimExtract,\"%BaseDir%\\install.wim\",3,\"\\Windows\",%Target%",
                    "WimExtract,\"%BaseDir%\\install.wim\",3,\"\\Program Files\",%Target%",
                    "WimExtract,\"%BaseDir%\\install.wim\",3,\"\\Program Files (x86)\",%Target%",
                    "WimExtract,\"%BaseDir%\\install.wim\",3,\"\\Boot\",%Temp%",
                    "WimExtract,\"%BaseDir%\\install.wim\",3,\"\\Users\",%Temp%",
                };
                CodeCommand[] cmds = EngineTests.ParseLines(lines, out List<LogInfo> errorLogs);
                Assert.IsTrue(errorLogs.Count == 0);
                Assert.AreEqual(4, cmds.Length);

                IsOptimizedCommand(cmds[0], CodeType.IniReadOp, 2);
                IsSingleCommand(cmds[1], CodeType.Echo);
                IsOptimizedCommand(cmds[2], CodeType.WimExtractOp, 3);
                IsOptimizedCommand(cmds[3], CodeType.WimExtractOp, 2);
            }
        }

        [TestMethod]
        [TestCategory(nameof(CodeOptimizer))]
        public void Complex()
        {
            // ReadInterface, IniRead, WimExtract, WimPathOp
            string[] lines = new string[]
            {
                "System,SetLocal",
                "ReadInterface,Text,%ScriptFile%,Interface,FileBox01,%IfaceSection%",
                "ReadInterface,Visible,%ScriptFile%,%IfaceSection%,ActiveMarker,%isActive%",
                "ReadInterface,Value,%ScriptFile%,%IfaceSection%,Index01,%wimIndex%",
                "If,%isActive%,Equal,True,Begin",
                "  Set,%Target%,%BaseDir%\\Target",
                "  WimExtract,\"%BaseDir%\\install.wim\",3,\"\\Windows\",%Target%",
                "  WimExtract,\"%BaseDir%\\install.wim\",3,\"\\Program Files\",%Target%",
                "  WimExtract,\"%BaseDir%\\install.wim\",3,\"\\Program Files (x86)\",%Target%",
                "  Echo,Extracting...",
                "  Echo,\"This won't take long...\"",
                "  WimExtract,\"%BaseDir%\\install.wim\",2,\"\\Boot\",%Target%",
                "  WimExtract,\"%BaseDir%\\install.wim\",2,\"\\Users\",%Target%",
                "  ; Also copy fonts",
                "  WimExtract,\"%BaseDir%\\install.wim\",2,\"\\Windows\\Fonts\",%Target%",
                "End",
                "Else,Begin",
                "  IniRead,%SrcFile%,A,B,%D1%",
                "  IniRead,%SrcFile%,C,D,%D2%",
                "  // Just read some values.",
                "  IniRead,%SrcFile%,E,F,%D3%",
                "  IniRead,%D1%,%D2%,%D3%,%D4%",
                "End",
                "Echo,Dummy",
                "WriteInterface,Resource,%ScriptFile%,Interface,Button01,Image.png",
                "WriteInterface,PosX,%ScriptFile%,Interface,Button01,100",
            };
            CodeCommand[] cmds = EngineTests.ParseLines(lines, out List<LogInfo> errorLogs);
            Assert.IsTrue(errorLogs.Count == 0);
            Assert.AreEqual(7, cmds.Length);

            IsSingleCommand(cmds[0], CodeType.System);
            IsSingleCommand(cmds[1], CodeType.ReadInterface);
            IsOptimizedCommand(cmds[2], CodeType.ReadInterfaceOp, 2);
            IsSingleCommand(cmds[3], CodeType.If);
            IsSingleCommand(cmds[4], CodeType.Else);
            IsSingleCommand(cmds[5], CodeType.Echo);
            IsOptimizedCommand(cmds[6], CodeType.WriteInterfaceOp, 2);

            CodeInfo_If ifInfo = (CodeInfo_If)cmds[3].Info;
            IsSingleCommand(ifInfo.Link[0], CodeType.Set);
            IsOptimizedCommand(ifInfo.Link[1], CodeType.WimExtractOp, 3);
            IsSingleCommand(ifInfo.Link[2], CodeType.Echo);
            IsSingleCommand(ifInfo.Link[3], CodeType.Echo);
            IsOptimizedCommand(ifInfo.Link[4], CodeType.WimExtractOp, 3);

            CodeInfo_Else elseInfo = (CodeInfo_Else)cmds[4].Info;
            IsOptimizedCommand(elseInfo.Link[0], CodeType.IniReadOp, 3);
            IsSingleCommand(elseInfo.Link[1], CodeType.IniRead);
        }

        #region Utility
        public static void IsSingleCommand(CodeCommand actCmd, CodeType expType)
        {
            Assert.AreEqual(expType, actCmd.Type);
            Assert.IsTrue(actCmd.Info is not CodeOptInfo);
        }

        public static void IsOptimizedCommand(CodeCommand actCmd, CodeType expType, int mergedLines)
        {
            Assert.AreEqual(expType, actCmd.Type);
            Assert.IsTrue(actCmd.Info is CodeOptInfo);
            Assert.AreEqual(mergedLines, ((CodeOptInfo)actCmd.Info).Cmds.Count);
        }
        #endregion
    }


}
