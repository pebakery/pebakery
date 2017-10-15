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
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.Core;
using PEBakery.Core.Commands;
using System.Collections.Generic;
using PEBakery.Exceptions;
using System.Diagnostics;

namespace PEBakery.Tests.Core.Command
{
    [TestClass]
    public class CommandMathTests
    {
        #region Arithmetic - Add, Sub, Mul, Div
        [TestCategory("Command")]
        [TestCategory("CommandMath")]
        [TestMethod]
        public void Math_Arithmetic()
        {
            EngineState s = EngineTests.CreateEngineState();
            
            // Add
            Math_Template(s, "Math,Add,%Dest%,100,200", "300");
            Math_Template(s, "Math,Add,%Dest%,100.1,200", "300.1");
            Math_Template(s, "Math,Add,%Dest%,100,200.2", "300.2");
            Math_Template(s, "Math,Add,%Dest%,100.1,200.2", "300.3");
            Math_Template(s, "Math,Add,%Dest%,100.0,200.0", "300.0");
            Math_Template(s, "Math,Add,%Dest%,-300,100", "-200");

            // Sub
            Math_Template(s, "Math,Sub,%Dest%,200,100", "100");
            Math_Template(s, "Math,Sub,%Dest%,200.1,100", "100.1");
            Math_Template(s, "Math,Sub,%Dest%,200,100.2", "99.8");
            Math_Template(s, "Math,Sub,%Dest%,200.1,100.2", "99.9");
            Math_Template(s, "Math,Sub,%Dest%,200.0,100.0", "100.0");
            Math_Template(s, "Math,Sub,%Dest%,100,200", "-100");
            Math_Template(s, "Math,Sub,%Dest%,100,-200", "300");

            // Mul
            Math_Template(s, "Math,Mul,%Dest%,3,2", "6");
            Math_Template(s, "Math,Mul,%Dest%,3,2.1", "6.3");
            Math_Template(s, "Math,Mul,%Dest%,3.2,2", "6.4");
            Math_Template(s, "Math,Mul,%Dest%,3.2,2.1", "6.72");
            Math_Template(s, "Math,Mul,%Dest%,3.0,2.0", "6.00");
            Math_Template(s, "Math,Mul,%Dest%,-3,2", "-6");
            Math_Template(s, "Math,Mul,%Dest%,0.2,0.5", "0.10");

            // Div
            Math_Template(s, "Math,Div,%Dest%,4,2", "2");
            Math_Template(s, "Math,Div,%Dest%,4.5,1.5", "3");
            Math_Template(s, "Math,Div,%Dest%,4.5,-1.5", "-3");
            Math_Template(s, "Math,Div,%Dest%,10,3", "3.3333333333333333333333333333"); // 소숫점 이하 28자리
            
            // Test Error
            Math_Template_Error(s, "Math,Add,Dest,4", ErrorCheck.ParserError);
            Math_Template_Error(s, "Math,Sub,3,4", ErrorCheck.ParserError);
            Math_Template_Error(s, "Math,Mul,%Dest%,4", ErrorCheck.ParserError);
            Math_Template_Error(s, "Math,Div,%Dest%,4,2,1", ErrorCheck.ParserError);
            Math_Template_Error(s, "Math,Add,%Dest%,T,3", ErrorCheck.Error);
        }
        #endregion

        #region IntDiv
        [TestCategory("Command")]
        [TestCategory("CommandMath")]
        [TestMethod]
        public void Math_IntDiv()
        {
            EngineState s = EngineTests.CreateEngineState();

            // IntDiv
            Math_IntDiv_Template(s, "Math,IntDiv,%DestQ%,%DestR%,0x3,2", "1", "1");
            Math_IntDiv_Template(s, "Math,IntDiv,%DestQ%,%DestR%,10,3", "3", "1");
            Math_IntDiv_Template(s, "Math,IntDiv,%DestQ%,%DestR%,10,-3", "-3", "1");
            Math_IntDiv_Template(s, "Math,IntDiv,%DestQ%,%DestR%,-10,3", "-3", "-1");
            Math_IntDiv_Template(s, "Math,IntDiv,%DestQ%,%DestR%,-10,-3", "3", "-1");

            // Test Error
            Math_Template_Error(s, "Math,IntDiv,DestQ,%DestR%,3,2", ErrorCheck.ParserError);
            Math_Template_Error(s, "Math,IntDiv,%DestQ%,DestR,3,2", ErrorCheck.ParserError);
            Math_Template_Error(s, "Math,IntDiv,%DestQ%,%DestR%,3,2,1", ErrorCheck.ParserError);
            Math_Template_Error(s, "Math,IntDiv,%DestQ%,%DestR%,3", ErrorCheck.ParserError);
            Math_Template_Error(s, "Math,IntDiv,%DestQ%,%DestR%,3,F", ErrorCheck.Error);
            Math_Template_Error(s, "Math,IntDiv,%DestQ%,%DestR%,A,F", ErrorCheck.Error);
            Math_Template_Error(s, "Math,IntDiv,%DestQ%,%DestR%,B,C", ErrorCheck.Error);
        }

        public void Math_IntDiv_Template(EngineState s, string rawCode, string quotientCheck, string remainderCheck)
        {
            EngineTests.Eval(s, rawCode, CodeType.Math, ErrorCheck.Success);

            Assert.IsTrue(s.Variables["DestQ"].Equals(quotientCheck, StringComparison.Ordinal));
            Assert.IsTrue(s.Variables["DestR"].Equals(remainderCheck, StringComparison.Ordinal));
        }
        #endregion

        #region Neg
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandMath")]
        public void Math_Neg()
        {
            EngineState s = EngineTests.CreateEngineState();

            // Neg
            Math_Template(s, "Math,Neg,%Dest%,1", "-1");
            Math_Template(s, "Math,Neg,%Dest%,-2", "2");
            Math_Template(s, "Math,Neg,%Dest%,3.1", "-3.1");
            Math_Template(s, "Math,Neg,%Dest%,-4.25", "4.25");

            // Test Error
            Math_Template_Error(s, "Math,Neg,Dest,1", ErrorCheck.ParserError);
            Math_Template_Error(s, "Math,Neg,%Dest%,1,2", ErrorCheck.ParserError);
            Math_Template_Error(s, "Math,Neg,%Dest%,XYZ", ErrorCheck.Error);
        }
        #endregion

        #region IntegerSignedness - IntSign, IntUnsign
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandMath")]
        public void Math_IntegerSignedness()
        {
            EngineState s = EngineTests.CreateEngineState();

            // IntSign
            Math_Template(s, "Math,ToSign,%Dest%,1", "1"); // 32
            Math_Template(s, "Math,ToSign,%Dest%,4294967295", "-1"); // 32
            Math_Template(s, "Math,ToSign,%Dest%,2,16", "2"); // 16
            Math_Template(s, "Math,ToSign,%Dest%,65534,16", "-2"); // 16

            // IntUnsign
            Math_Template(s, "Math,ToUnsign,%Dest%,1", "1"); // 32
            Math_Template(s, "Math,ToUnsign,%Dest%,-1", "4294967295"); // 32
            Math_Template(s, "Math,ToUnsign,%Dest%,2,16", "2"); // 16
            Math_Template(s, "Math,ToUnsign,%Dest%,-2,16", "65534"); // 16

            // Test Error
            Math_Template_Error(s, "Math,ToSign,Dest,1", ErrorCheck.ParserError);
            Math_Template_Error(s, "Math,ToUnsign,%Dest%,1,2", ErrorCheck.ParserError);
            Math_Template_Error(s, "Math,ToSign,%Dest%,XYZ", ErrorCheck.Error);
            Math_Template_Error(s, "Math,ToUnsign,%Dest%,12.3", ErrorCheck.Error);
            Math_Template_Error(s, "Math,ToUnsign,%Dest%,12.0", ErrorCheck.Error);
        }
        #endregion

        #region BoolLogicOper - BoolAnd, BoolOr, BoolXor
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandMath")]
        public void Math_BoolLogicOper()
        {
            EngineState s = EngineTests.CreateEngineState();

            // BoolAnd
            Math_Template(s, "Math,BoolAnd,%Dest%,True,True", "True");
            Math_Template(s, "Math,BoolAnd,%Dest%,True,False", "False");
            Math_Template(s, "Math,BoolAnd,%Dest%,False,True", "False");
            Math_Template(s, "Math,BoolAnd,%Dest%,False,False", "False");

            // BoolOr
            Math_Template(s, "Math,BoolOr,%Dest%,True,True", "True");
            Math_Template(s, "Math,BoolOr,%Dest%,True,False", "True");
            Math_Template(s, "Math,BoolOr,%Dest%,False,True", "True");
            Math_Template(s, "Math,BoolOr,%Dest%,False,False", "False");

            // BoolXor
            Math_Template(s, "Math,BoolXor,%Dest%,True,True", "False");
            Math_Template(s, "Math,BoolXor,%Dest%,True,False", "True");
            Math_Template(s, "Math,BoolXor,%Dest%,False,True", "True");
            Math_Template(s, "Math,BoolXor,%Dest%,False,False", "False");

            // Test Error
            Math_Template_Error(s, "Math,BoolAnd,Dest,4", ErrorCheck.ParserError);
            Math_Template_Error(s, "Math,BoolOr,3,4", ErrorCheck.ParserError);
            Math_Template_Error(s, "Math,BoolXor,%Dest%,4", ErrorCheck.ParserError);
            Math_Template_Error(s, "Math,BoolXor,%Dest%,B,E", ErrorCheck.Error);
        }
        #endregion

        #region BoolNot
        [TestCategory("Command")]
        [TestCategory("CommandMath")]
        [TestMethod]
        public void Math_BoolNot()
        {
            EngineState s = EngineTests.CreateEngineState();

            // BoolNot
            Math_Template(s, "Math,BoolNot,%Dest%,True", "False");
            Math_Template(s, "Math,BoolNot,%Dest%,False", "True");

            // Test Error
            Math_Template_Error(s, "Math,BoolNot,Dest,True,2", ErrorCheck.ParserError);
            Math_Template_Error(s, "Math,BoolNot,%Dest%,True,2", ErrorCheck.ParserError);
            Math_Template_Error(s, "Math,BoolNot,%Dest%,3", ErrorCheck.Error);
            Math_Template_Error(s, "Math,BoolNot,%Dest%,ABC", ErrorCheck.Error);
        }
        #endregion

        #region BitLogicOper - BitAnd, BitOr, BitXor
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandMath")]
        public void Math_BitLogicOper()
        {
            EngineState s = EngineTests.CreateEngineState();

            // BitAnd
            Math_Template(s, "Math,BitAnd,%Dest%,0x1,0x3", "1");
            Math_Template(s, "Math,BitAnd,%Dest%,0x7,0x8", "0");

            // BitOr
            Math_Template(s, "Math,BitOr,%Dest%,0x1,0x3", "3");
            Math_Template(s, "Math,BitOr,%Dest%,0x7,0x8", "15");

            // BitXor
            Math_Template(s, "Math,BitXor,%Dest%,0x1,0x3", "2");
            Math_Template(s, "Math,BitXor,%Dest%,0x7,0x8", "15");

            // Test Error
            Math_Template_Error(s, "Math,BitAnd,Dest,4", ErrorCheck.ParserError);
            Math_Template_Error(s, "Math,BitOr,%Dest%,1,2,37", ErrorCheck.ParserError);
            Math_Template_Error(s, "Math,BoolXor,%Dest%,B,E", ErrorCheck.Error);
        }
        #endregion

        #region BitNot
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandMath")]
        public void Math_BitNot()
        {
            EngineState s = EngineTests.CreateEngineState();

            // BoolNot
            Math_Template(s, "Math,BitNot,%Dest%,2", "4294967293"); // 32bit
            Math_Template(s, "Math,BitNot,%Dest%,4294967293", "2"); // 32bit
            Math_Template(s, "Math,BitNot,%Dest%,2,16", "65533"); // 16bit
            Math_Template(s, "Math,BitNot,%Dest%,65533,16", "2"); // 16bit

            // Test Error
            Math_Template_Error(s, "Math,BitNot,Dest,12,8", ErrorCheck.ParserError);
            Math_Template_Error(s, "Math,BitNot,%Dest%,12,17", ErrorCheck.ParserError);
            Math_Template_Error(s, "Math,BitNot,%Dest%,ABC", ErrorCheck.Error);
        }
        #endregion

        #region BitShift
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandMath")]
        public void Math_BitShift()
        {
            EngineState s = EngineTests.CreateEngineState();

            // BitShift
            Math_Template(s, "Math,BitShift,%Dest%,8,LEFT,2", "32"); // 32bit
            Math_Template(s, "Math,BitShift,%Dest%,9,RIGHT,1", "4"); // 32bit
            Math_Template(s, "Math,BitShift,%Dest%,7,LEFT,7,16", "896"); // 16bit
            Math_Template(s, "Math,BitShift,%Dest%,7,LEFT,7,8", "128"); // 8bit

            // Test Error
            Math_Template_Error(s, "Math,BitShift,Dest,8,LEFT,2", ErrorCheck.ParserError);
            Math_Template_Error(s, "Math,BitShift,%Dest%,12,9", ErrorCheck.ParserError);
            Math_Template_Error(s, "Math,BitShift,%Dest%,123,LEFT,7,19", ErrorCheck.ParserError);
            Math_Template_Error(s, "Math,BitShift,%Dest%,XYZ,LEFT,7,16", ErrorCheck.Error);
        }
        #endregion

        #region CeilFloorRound
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandMath")]
        public void Math_CeilFloorRound()
        {
            EngineState s = EngineTests.CreateEngineState();

            // Ceil
            Math_Template(s, "Math,Ceil,%Dest%,21,10", "30");
            Math_Template(s, "Math,Ceil,%Dest%,21,8", "24");

            // Floor
            Math_Template(s, "Math,Floor,%Dest%,21,10", "20");
            Math_Template(s, "Math,Floor,%Dest%,21,8", "16");

            // Round
            Math_Template(s, "Math,Round,%Dest%,21,10", "20");
            Math_Template(s, "Math,Round,%Dest%,25,10", "30");
            Math_Template(s, "Math,Round,%Dest%,27,10", "30");
            Math_Template(s, "Math,Round,%Dest%,27,8", "24");

            // Test Error
            Math_Template_Error(s, "Math,Ceil,Dest,21,10", ErrorCheck.ParserError);
            Math_Template_Error(s, "Math,Floor,%Dest%,21", ErrorCheck.ParserError);
            Math_Template_Error(s, "Math,Round,%Dest%,21,-1", ErrorCheck.Error);
            Math_Template_Error(s, "Math,Round,%Dest%,XYZ,16", ErrorCheck.Error);
            Math_Template_Error(s, "Math,Round,%Dest%,21,XYZ", ErrorCheck.Error);
        }
        #endregion

        #region Abs
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandMath")]
        public void Math_Abs()
        {
            EngineState s = EngineTests.CreateEngineState();

            // Abs
            Math_Template(s, "Math,Abs,%Dest%,25", "25");
            Math_Template(s, "Math,Abs,%Dest%,-25", "25");
            Math_Template(s, "Math,Abs,%Dest%,0x25", "37");
            Math_Template(s, "Math,Abs,%Dest%,1.2", "1.2");
            Math_Template(s, "Math,Abs,%Dest%,-1.2", "1.2");

            // Test Error
            Math_Template_Error(s, "Math,Abs,Dest,21", ErrorCheck.ParserError);
            Math_Template_Error(s, "Math,Abs,%Dest%,21,10", ErrorCheck.ParserError);
        }
        #endregion

        #region Pow
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandMath")]
        public void Math_Pow()
        {
            EngineState s = EngineTests.CreateEngineState();

            // Abs
            Math_Template(s, "Math,Pow,%Dest%,10,2", "100");
            Math_Template(s, "Math,Pow,%Dest%,0x10,2", "256");
            Math_Template(s, "Math,Pow,%Dest%,1.2,2", "1.44");

            // Test Error
            Math_Template_Error(s, "Math,Pow,Dest,3,2", ErrorCheck.ParserError);
            Math_Template_Error(s, "Math,Pow,%Dest%,3", ErrorCheck.ParserError);
        }
        #endregion

        #region Math_Template
        public void Math_Template(EngineState s, string rawCode, string destCheck)
        {
            EngineTests.Eval(s, rawCode, CodeType.Math, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals(destCheck, StringComparison.Ordinal));
        }

        public void Math_Template_Error(EngineState s, string rawCode, ErrorCheck check)
        {
            EngineTests.Eval(s, rawCode, CodeType.Math, check);
        }
        #endregion
    }
}


