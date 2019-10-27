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
using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace PEBakery.Core.Tests.Command
{
    [TestClass]
    [SuppressMessage("ReSharper", "ParameterOnlyUsedForPreconditionCheck.Local")]
    public class CommandMathTests
    {
        #region Arithmetic - Add, Sub, Mul, Div
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandMath")]
        public void Arithmetic()
        {
            EngineState s = EngineTests.CreateEngineState();

            // Add
            SuccessTemplate(s, "Math,Add,%Dest%,100,200", "300");
            SuccessTemplate(s, "Math,Add,%Dest%,100.1,200", "300.1");
            SuccessTemplate(s, "Math,Add,%Dest%,100,200.2", "300.2");
            SuccessTemplate(s, "Math,Add,%Dest%,100.1,200.2", "300.3");
            SuccessTemplate(s, "Math,Add,%Dest%,100.0,200.0", "300.0");
            SuccessTemplate(s, "Math,Add,%Dest%,-300,100", "-200");

            // Sub
            SuccessTemplate(s, "Math,Sub,%Dest%,200,100", "100");
            SuccessTemplate(s, "Math,Sub,%Dest%,200.1,100", "100.1");
            SuccessTemplate(s, "Math,Sub,%Dest%,200,100.2", "99.8");
            SuccessTemplate(s, "Math,Sub,%Dest%,200.1,100.2", "99.9");
            SuccessTemplate(s, "Math,Sub,%Dest%,200.0,100.0", "100.0");
            SuccessTemplate(s, "Math,Sub,%Dest%,100,200", "-100");
            SuccessTemplate(s, "Math,Sub,%Dest%,100,-200", "300");

            // Mul
            SuccessTemplate(s, "Math,Mul,%Dest%,3,2", "6");
            SuccessTemplate(s, "Math,Mul,%Dest%,3,2.1", "6.3");
            SuccessTemplate(s, "Math,Mul,%Dest%,3.2,2", "6.4");
            SuccessTemplate(s, "Math,Mul,%Dest%,3.2,2.1", "6.72");
            SuccessTemplate(s, "Math,Mul,%Dest%,3.0,2.0", "6.00");
            SuccessTemplate(s, "Math,Mul,%Dest%,-3,2", "-6");
            SuccessTemplate(s, "Math,Mul,%Dest%,0.2,0.5", "0.10");

            // Div
            SuccessTemplate(s, "Math,Div,%Dest%,4,2", "2");
            SuccessTemplate(s, "Math,Div,%Dest%,4.5,1.5", "3");
            SuccessTemplate(s, "Math,Div,%Dest%,4.5,-1.5", "-3");
            SuccessTemplate(s, "Math,Div,%Dest%,10,3", "3.3333333333333333333333333333"); // 28 numbers after dot

            // Test Error
            ErrorTemplate(s, "Math,Add,Dest,4", ErrorCheck.ParserError);
            ErrorTemplate(s, "Math,Sub,3,4", ErrorCheck.ParserError);
            ErrorTemplate(s, "Math,Mul,%Dest%,4", ErrorCheck.ParserError);
            ErrorTemplate(s, "Math,Div,%Dest%,4,2,1", ErrorCheck.ParserError);
            ErrorTemplate(s, "Math,Add,%Dest%,T,3", ErrorCheck.RuntimeError);
        }
        #endregion

        #region IntDiv
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandMath")]
        public void IntDiv()
        {
            EngineState s = EngineTests.CreateEngineState();

            void IntDivTemplate(string rawCode, string quotientCheck, string remainderCheck)
            {
                EngineTests.Eval(s, rawCode, CodeType.Math, ErrorCheck.Success);

                Assert.IsTrue(s.Variables["DestQ"].Equals(quotientCheck, StringComparison.Ordinal));
                Assert.IsTrue(s.Variables["DestR"].Equals(remainderCheck, StringComparison.Ordinal));
            }

            // IntDiv
            IntDivTemplate("Math,IntDiv,%DestQ%,%DestR%,0x3,2", "1", "1");
            IntDivTemplate("Math,IntDiv,%DestQ%,%DestR%,10,3", "3", "1");
            IntDivTemplate("Math,IntDiv,%DestQ%,%DestR%,10,-3", "-3", "1");
            IntDivTemplate("Math,IntDiv,%DestQ%,%DestR%,-10,3", "-3", "-1");
            IntDivTemplate("Math,IntDiv,%DestQ%,%DestR%,-10,-3", "3", "-1");

            // Test Error
            ErrorTemplate(s, "Math,IntDiv,DestQ,%DestR%,3,2", ErrorCheck.ParserError);
            ErrorTemplate(s, "Math,IntDiv,%DestQ%,DestR,3,2", ErrorCheck.ParserError);
            ErrorTemplate(s, "Math,IntDiv,%DestQ%,%DestR%,3,2,1", ErrorCheck.ParserError);
            ErrorTemplate(s, "Math,IntDiv,%DestQ%,%DestR%,3", ErrorCheck.ParserError);
            ErrorTemplate(s, "Math,IntDiv,%DestQ%,%DestR%,3,F", ErrorCheck.RuntimeError);
            ErrorTemplate(s, "Math,IntDiv,%DestQ%,%DestR%,A,F", ErrorCheck.RuntimeError);
            ErrorTemplate(s, "Math,IntDiv,%DestQ%,%DestR%,B,C", ErrorCheck.RuntimeError);
        }
        #endregion

        #region Neg
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandMath")]
        public void Neg()
        {
            EngineState s = EngineTests.CreateEngineState();

            // Neg
            SuccessTemplate(s, "Math,Neg,%Dest%,1", "-1");
            SuccessTemplate(s, "Math,Neg,%Dest%,-2", "2");
            SuccessTemplate(s, "Math,Neg,%Dest%,3.1", "-3.1");
            SuccessTemplate(s, "Math,Neg,%Dest%,-4.25", "4.25");

            // Test Error
            ErrorTemplate(s, "Math,Neg,Dest,1", ErrorCheck.ParserError);
            ErrorTemplate(s, "Math,Neg,%Dest%,1,2", ErrorCheck.ParserError);
            ErrorTemplate(s, "Math,Neg,%Dest%,XYZ", ErrorCheck.RuntimeError);
        }
        #endregion

        #region IntegerSignedness - IntSign, IntUnsign
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandMath")]
        public void IntegerSignedness()
        {
            EngineState s = EngineTests.CreateEngineState();
            s.Variables["BitSize"] = "16";
            s.Variables["Garbage"] = "128";

            // IntSign
            SuccessTemplate(s, "Math,ToSign,%Dest%,1,32", "1"); // 32
            SuccessTemplate(s, "Math,ToSign,%Dest%,4294967295,32", "-1"); // 32
            SuccessTemplate(s, "Math,ToSign,%Dest%,2,16", "2"); // 16
            SuccessTemplate(s, "Math,ToSign,%Dest%,65534,16", "-2"); // 16
            SuccessTemplate(s, "Math,ToSign,%Dest%,2,%BitSize%", "2"); // 16bit, Var
            SuccessTemplate(s, "Math,ToSign,%Dest%,65534,%BitSize%", "-2"); // 16bit, Var
            ErrorTemplate(s, "Math,ToSign,%Dest%,1", ErrorCheck.ParserError); // Had been valid prior to 0.9.6 beta6
            ErrorTemplate(s, "Math,ToSign,%Dest%,4294967295", ErrorCheck.ParserError); // Had been valid prior to 0.9.6 beta6

            // IntUnsign
            SuccessTemplate(s, "Math,ToUnsign,%Dest%,1,32", "1"); // 32
            SuccessTemplate(s, "Math,ToUnsign,%Dest%,-1,32", "4294967295"); // 32
            SuccessTemplate(s, "Math,ToUnsign,%Dest%,2,16", "2"); // 16
            SuccessTemplate(s, "Math,ToUnsign,%Dest%,-2,16", "65534"); // 16
            SuccessTemplate(s, "Math,ToUnsign,%Dest%,2,%BitSize%", "2"); // 16bit, Var
            SuccessTemplate(s, "Math,ToUnsign,%Dest%,-2,%BitSize%", "65534"); // 16bit, Var
            ErrorTemplate(s, "Math,ToUnsign,%Dest%,1", ErrorCheck.ParserError); // Had been valid prior to 0.9.6 beta6
            ErrorTemplate(s, "Math,ToUnsign,%Dest%,-1", ErrorCheck.ParserError); // Had been valid prior to 0.9.6 beta6

            // Test Error
            ErrorTemplate(s, "Math,ToSign,Dest,1", ErrorCheck.ParserError);
            ErrorTemplate(s, "Math,ToUnsign,%Dest%,1,2", ErrorCheck.ParserError);
            ErrorTemplate(s, "Math,ToSign,%Dest%,XYZ", ErrorCheck.ParserError);
            ErrorTemplate(s, "Math,ToUnsign,%Dest%,12.3", ErrorCheck.ParserError);
            ErrorTemplate(s, "Math,ToUnsign,%Dest%,12.0", ErrorCheck.ParserError);
            ErrorTemplate(s, "Math,ToUnsign,%Dest%,-2,%Garbage%", ErrorCheck.RuntimeError);
        }
        #endregion

        #region BoolLogicOper - BoolAnd, BoolOr, BoolXor
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandMath")]
        public void BoolLogicOper()
        {
            EngineState s = EngineTests.CreateEngineState();

            // BoolAnd
            SuccessTemplate(s, "Math,BoolAnd,%Dest%,True,True", "True");
            SuccessTemplate(s, "Math,BoolAnd,%Dest%,True,False", "False");
            SuccessTemplate(s, "Math,BoolAnd,%Dest%,False,True", "False");
            SuccessTemplate(s, "Math,BoolAnd,%Dest%,False,False", "False");
            SuccessTemplate(s, "Math,BoolAnd,%Dest%,1,1", "True");
            SuccessTemplate(s, "Math,BoolAnd,%Dest%,1,0", "False");
            SuccessTemplate(s, "Math,BoolAnd,%Dest%,0,1", "False");
            SuccessTemplate(s, "Math,BoolAnd,%Dest%,0,0", "False");

            // BoolOr
            SuccessTemplate(s, "Math,BoolOr,%Dest%,True,True", "True");
            SuccessTemplate(s, "Math,BoolOr,%Dest%,True,False", "True");
            SuccessTemplate(s, "Math,BoolOr,%Dest%,False,True", "True");
            SuccessTemplate(s, "Math,BoolOr,%Dest%,False,False", "False");
            SuccessTemplate(s, "Math,BoolOr,%Dest%,2,1", "True");
            SuccessTemplate(s, "Math,BoolOr,%Dest%,2,0", "True");
            SuccessTemplate(s, "Math,BoolOr,%Dest%,0,1", "True");
            SuccessTemplate(s, "Math,BoolOr,%Dest%,0,0", "False");

            // BoolXor
            SuccessTemplate(s, "Math,BoolXor,%Dest%,True,True", "False");
            SuccessTemplate(s, "Math,BoolXor,%Dest%,True,False", "True");
            SuccessTemplate(s, "Math,BoolXor,%Dest%,False,True", "True");
            SuccessTemplate(s, "Math,BoolXor,%Dest%,False,False", "False");
            SuccessTemplate(s, "Math,BoolXor,%Dest%,-1,-2", "False");
            SuccessTemplate(s, "Math,BoolXor,%Dest%,-234,0", "True");
            SuccessTemplate(s, "Math,BoolXor,%Dest%,0,2341345", "True");
            SuccessTemplate(s, "Math,BoolXor,%Dest%,0,0", "False");

            // Test Error
            ErrorTemplate(s, "Math,BoolAnd,Dest,4", ErrorCheck.ParserError);
            ErrorTemplate(s, "Math,BoolOr,3,4", ErrorCheck.ParserError);
            ErrorTemplate(s, "Math,BoolXor,%Dest%,4", ErrorCheck.ParserError);
            ErrorTemplate(s, "Math,BoolXor,%Dest%,B,E", ErrorCheck.RuntimeError);
        }
        #endregion

        #region BoolNot
        [TestCategory("Command")]
        [TestCategory("CommandMath")]
        [TestMethod]
        public void BoolNot()
        {
            EngineState s = EngineTests.CreateEngineState();

            // BoolNot
            SuccessTemplate(s, "Math,BoolNot,%Dest%,True", "False");
            SuccessTemplate(s, "Math,BoolNot,%Dest%,False", "True");
            SuccessTemplate(s, "Math,BoolNot,%Dest%,0x132", "False");
            SuccessTemplate(s, "Math,BoolNot,%Dest%,0", "True");

            // Test Error
            ErrorTemplate(s, "Math,BoolNot,Dest,True,2", ErrorCheck.ParserError);
            ErrorTemplate(s, "Math,BoolNot,%Dest%,True,2", ErrorCheck.ParserError);
            ErrorTemplate(s, "Math,BoolNot,%Dest%,ABC", ErrorCheck.RuntimeError);
        }
        #endregion

        #region BitLogicOper - BitAnd, BitOr, BitXor
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandMath")]
        public void BitLogicOper()
        {
            EngineState s = EngineTests.CreateEngineState();

            // BitAnd
            SuccessTemplate(s, "Math,BitAnd,%Dest%,0x1,0x3", "1");
            SuccessTemplate(s, "Math,BitAnd,%Dest%,0x7,0x8", "0");

            // BitOr
            SuccessTemplate(s, "Math,BitOr,%Dest%,0x1,0x3", "3");
            SuccessTemplate(s, "Math,BitOr,%Dest%,0x7,0x8", "15");

            // BitXor
            SuccessTemplate(s, "Math,BitXor,%Dest%,0x1,0x3", "2");
            SuccessTemplate(s, "Math,BitXor,%Dest%,0x7,0x8", "15");

            // Test Error
            ErrorTemplate(s, "Math,BitAnd,Dest,4", ErrorCheck.ParserError);
            ErrorTemplate(s, "Math,BitOr,%Dest%,1,2,37", ErrorCheck.ParserError);
            ErrorTemplate(s, "Math,BoolXor,%Dest%,B,E", ErrorCheck.RuntimeError);
        }
        #endregion

        #region BitNot
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandMath")]
        public void BitNot()
        {
            EngineState s = EngineTests.CreateEngineState();
            s.Variables["BitSize"] = "16";
            s.Variables["Garbage"] = "128";

            // BoolNot
            SuccessTemplate(s, "Math,BitNot,%Dest%,2,32", "4294967293"); // 32bit
            SuccessTemplate(s, "Math,BitNot,%Dest%,4294967293,32", "2"); // 32bit
            SuccessTemplate(s, "Math,BitNot,%Dest%,2,16", "65533"); // 16bit
            SuccessTemplate(s, "Math,BitNot,%Dest%,65533,16", "2"); // 16bit
            SuccessTemplate(s, "Math,BitNot,%Dest%,2,%BitSize%", "65533"); // 16bit, Var
            SuccessTemplate(s, "Math,BitNot,%Dest%,65533,%BitSize%", "2"); // 16bit, Var

            // Test Error
            ErrorTemplate(s, "Math,BitNot,%Dest%,2", ErrorCheck.ParserError); // Had been valid prior to 0.9.6 beta6
            ErrorTemplate(s, "Math,BitNot,%Dest%,4294967293", ErrorCheck.ParserError); // Had been valid prior to 0.9.6 beta6
            ErrorTemplate(s, "Math,BitNot,Dest,12,8", ErrorCheck.ParserError);
            ErrorTemplate(s, "Math,BitNot,%Dest%,12,17", ErrorCheck.ParserError);
            ErrorTemplate(s, "Math,BitNot,%Dest%,ABC", ErrorCheck.ParserError);
            ErrorTemplate(s, "Math,BitNot,%Dest%,2,%Garbage%", ErrorCheck.RuntimeError);
        }
        #endregion

        #region BitShift
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandMath")]
        public void BitShift()
        {
            EngineState s = EngineTests.CreateEngineState();

            // BitShift
            SuccessTemplate(s, "Math,BitShift,%Dest%,8,LEFT,2,32", "32"); // 32bit
            SuccessTemplate(s, "Math,BitShift,%Dest%,9,RIGHT,1,32", "4"); // 32bit
            SuccessTemplate(s, "Math,BitShift,%Dest%,7,LEFT,7,16", "896"); // 16bit
            SuccessTemplate(s, "Math,BitShift,%Dest%,7,LEFT,7,8", "128"); // 8bit

            // Test Error
            ErrorTemplate(s, "Math,BitShift,%Dest%,8,LEFT,2", ErrorCheck.ParserError); // Had been valid prior to 0.9.6 beta6
            ErrorTemplate(s, "Math,BitShift,%Dest%,9,RIGHT,1", ErrorCheck.ParserError); // Had been valid prior to 0.9.6 beta6
            ErrorTemplate(s, "Math,BitShift,Dest,8,LEFT,2", ErrorCheck.ParserError);
            ErrorTemplate(s, "Math,BitShift,%Dest%,12,9", ErrorCheck.ParserError);
            ErrorTemplate(s, "Math,BitShift,%Dest%,123,LEFT,7,19", ErrorCheck.ParserError);
            ErrorTemplate(s, "Math,BitShift,%Dest%,XYZ,LEFT,7,16", ErrorCheck.RuntimeError);
        }
        #endregion

        #region CeilFloorRound
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandMath")]
        public void CeilFloorRound()
        {
            EngineState s = EngineTests.CreateEngineState();

            // Ceil
            SuccessTemplate(s, "Math,Ceil,%Dest%,21,10", "30");
            SuccessTemplate(s, "Math,Ceil,%Dest%,21,8", "24");

            // Floor
            SuccessTemplate(s, "Math,Floor,%Dest%,21,10", "20");
            SuccessTemplate(s, "Math,Floor,%Dest%,21,8", "16");

            // Round
            SuccessTemplate(s, "Math,Round,%Dest%,21,10", "20");
            SuccessTemplate(s, "Math,Round,%Dest%,25,10", "30");
            SuccessTemplate(s, "Math,Round,%Dest%,27,10", "30");
            SuccessTemplate(s, "Math,Round,%Dest%,27,8", "24");

            // Test Error
            ErrorTemplate(s, "Math,Ceil,Dest,21,10", ErrorCheck.ParserError);
            ErrorTemplate(s, "Math,Floor,%Dest%,21", ErrorCheck.ParserError);
            ErrorTemplate(s, "Math,Round,%Dest%,21,-1", ErrorCheck.RuntimeError);
            ErrorTemplate(s, "Math,Round,%Dest%,XYZ,16", ErrorCheck.RuntimeError);
            ErrorTemplate(s, "Math,Round,%Dest%,21,XYZ", ErrorCheck.RuntimeError);
        }
        #endregion

        #region Abs
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandMath")]
        public void Abs()
        {
            EngineState s = EngineTests.CreateEngineState();

            // Abs
            SuccessTemplate(s, "Math,Abs,%Dest%,25", "25");
            SuccessTemplate(s, "Math,Abs,%Dest%,-25", "25");
            SuccessTemplate(s, "Math,Abs,%Dest%,0x25", "37");
            SuccessTemplate(s, "Math,Abs,%Dest%,1.2", "1.2");
            SuccessTemplate(s, "Math,Abs,%Dest%,-1.2", "1.2");

            // Test Error
            ErrorTemplate(s, "Math,Abs,Dest,21", ErrorCheck.ParserError);
            ErrorTemplate(s, "Math,Abs,%Dest%,21,10", ErrorCheck.ParserError);
        }
        #endregion

        #region Pow
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandMath")]
        public void Pow()
        {
            EngineState s = EngineTests.CreateEngineState();

            // Pow
            SuccessTemplate(s, "Math,Pow,%Dest%,10,2", "100");
            SuccessTemplate(s, "Math,Pow,%Dest%,0x10,2", "256");
            SuccessTemplate(s, "Math,Pow,%Dest%,1.2,2", "1.44");

            // Test Error
            ErrorTemplate(s, "Math,Pow,Dest,3,2", ErrorCheck.ParserError);
            ErrorTemplate(s, "Math,Pow,%Dest%,3", ErrorCheck.ParserError);
        }
        #endregion

        #region HexDec - Hex, Dec
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandMath")]
        public void Hex()
        {
            EngineState s = EngineTests.CreateEngineState();
            s.Variables["BitSize"] = "16";
            s.Variables["Garbage"] = "128";

            // 8bit
            SuccessTemplate(s, "Math,Hex,%Dest%,15,8", "0x0F");
            SuccessTemplate(s, "Math,Hex,%Dest%,0x0F,8", "0x0F");
            SuccessTemplate(s, "Math,Hex,%Dest%,-1,8", "0xFF");
            SuccessTemplate(s, "Math,Hex,%Dest%,255,8", "0xFF");
            ErrorTemplate(s, "Math,Hex,%Dest%,2000,8", ErrorCheck.RuntimeError);

            // 16bit
            SuccessTemplate(s, "Math,Hex,%Dest%,15,16", "0x000F");
            SuccessTemplate(s, "Math,Hex,%Dest%,0x0F,16", "0x000F");
            SuccessTemplate(s, "Math,Hex,%Dest%,-1,16", "0xFFFF");
            SuccessTemplate(s, "Math,Hex,%Dest%,255,16", "0x00FF");

            // 16bit (Variable)
            SuccessTemplate(s, "Math,Hex,%Dest%,15,%BitSize%", "0x000F");
            SuccessTemplate(s, "Math,Hex,%Dest%,0x0F,%BitSize%", "0x000F");
            SuccessTemplate(s, "Math,Hex,%Dest%,-1,%BitSize%", "0xFFFF");
            SuccessTemplate(s, "Math,Hex,%Dest%,255,%BitSize%", "0x00FF");

            // 32bit
            SuccessTemplate(s, "Math,Hex,%Dest%,15,32", "0x0000000F");
            SuccessTemplate(s, "Math,Hex,%Dest%,0x0F,32", "0x0000000F");
            SuccessTemplate(s, "Math,Hex,%Dest%,-1,32", "0xFFFFFFFF");
            SuccessTemplate(s, "Math,Hex,%Dest%,255,32", "0x000000FF");
            ErrorTemplate(s, "Math,Hex,%Dest%,15", ErrorCheck.ParserError); // Had been valid prior to 0.9.6 beta6
            ErrorTemplate(s, "Math,Hex,%Dest%,0x0F", ErrorCheck.ParserError); // Had been valid prior to 0.9.6 beta6
            ErrorTemplate(s, "Math,Hex,%Dest%,-1", ErrorCheck.ParserError); // Had been valid prior to 0.9.6 beta6
            ErrorTemplate(s, "Math,Hex,%Dest%,255", ErrorCheck.ParserError); // Had been valid prior to 0.9.6 beta6

            // 64bit
            SuccessTemplate(s, "Math,Hex,%Dest%,15,64", "0x000000000000000F");
            SuccessTemplate(s, "Math,Hex,%Dest%,0x0F,64", "0x000000000000000F");
            SuccessTemplate(s, "Math,Hex,%Dest%,-1,64", "0xFFFFFFFFFFFFFFFF");
            SuccessTemplate(s, "Math,Hex,%Dest%,255,64", "0x00000000000000FF");

            // Test Error
            ErrorTemplate(s, "Math,Hex,%Dest%", ErrorCheck.ParserError);
            ErrorTemplate(s, "Math,Hex,%Dest%,15,%Garbage%", ErrorCheck.RuntimeError);
            ErrorTemplate(s, "Math,Hex,%Dest%,256,9", ErrorCheck.ParserError);
            ErrorTemplate(s, "Math,Hex,%Dest%,256,9,12", ErrorCheck.ParserError);
            ErrorTemplate(s, "Math,Hex,%Dest%,256,8", ErrorCheck.RuntimeError);
        }

        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandMath")]
        public void Dec()
        {
            EngineState s = EngineTests.CreateEngineState();
            s.Variables["BitSize"] = "16";
            s.Variables["Garbage"] = "128";

            // 8bit
            SuccessTemplate(s, "Math,Dec,%Dest%,0x0F,8", "15");
            SuccessTemplate(s, "Math,Dec,%Dest%,0xFF,8", "255");
            SuccessTemplate(s, "Math,Dec,%Dest%,-1,8", "255");
            SuccessTemplate(s, "Math,Dec,%Dest%,255,8", "255");
            ErrorTemplate(s, "Math,Dec,%Dest%,0xFFFF,8", ErrorCheck.RuntimeError);

            // 16bit
            SuccessTemplate(s, "Math,Dec,%Dest%,0x000F,16", "15");
            SuccessTemplate(s, "Math,Dec,%Dest%,-1,16", "65535");
            SuccessTemplate(s, "Math,Dec,%Dest%,0xFFFF,16", "65535");
            ErrorTemplate(s, "Math,Dec,%Dest%,0x10000,16", ErrorCheck.RuntimeError);

            // 16bit (Variable)
            SuccessTemplate(s, "Math,Dec,%Dest%,0x000F,%BitSize%", "15");
            SuccessTemplate(s, "Math,Dec,%Dest%,-1,%BitSize%", "65535");
            SuccessTemplate(s, "Math,Dec,%Dest%,0xFFFF,%BitSize%", "65535");
            ErrorTemplate(s, "Math,Dec,%Dest%,0x10000,%BitSize%", ErrorCheck.RuntimeError);

            // 32bit
            SuccessTemplate(s, "Math,Dec,%Dest%,0x0F,32", "15");
            SuccessTemplate(s, "Math,Dec,%Dest%,-1,32", "4294967295");
            ErrorTemplate(s, "Math,Dec,%Dest%,0x100000000,32", ErrorCheck.RuntimeError);
            ErrorTemplate(s, "Math,Dec,%Dest%,0x0F", ErrorCheck.ParserError); // Had been valid prior to 0.9.6 beta6
            ErrorTemplate(s, "Math,Dec,%Dest%,-1", ErrorCheck.ParserError); // Had been valid prior to 0.9.6 beta6
            ErrorTemplate(s, "Math,Dec,%Dest%,0x100000000", ErrorCheck.ParserError);

            // 64bit
            SuccessTemplate(s, "Math,Dec,%Dest%,0x0F,64", "15");
            SuccessTemplate(s, "Math,Dec,%Dest%,-1,64", "18446744073709551615");

            // Test Error
            ErrorTemplate(s, "Math,Dec,%Dest%,0x000F,%Garbage%", ErrorCheck.RuntimeError);
            ErrorTemplate(s, "Math,Dec,%Dest%", ErrorCheck.ParserError);
            ErrorTemplate(s, "Math,Dec,%Dest%,256,9", ErrorCheck.ParserError);
            ErrorTemplate(s, "Math,Dec,%Dest%,256,9,12", ErrorCheck.ParserError);
        }
        #endregion

        #region Rand
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandMath")]
        public void Rand()
        {
            EngineState s = EngineTests.CreateEngineState();

            void Template(string rawCode, int min, int max)
            {
                // Try many times to check proper bound of Math.Rand
                for (int i = 0; i < 256; i++)
                {
                    EngineTests.Eval(s, rawCode, CodeType.Math, ErrorCheck.Success);
                    string dest = s.Variables["Dest"];
                    Assert.IsTrue(int.TryParse(dest, NumberStyles.Integer, CultureInfo.InvariantCulture, out int destInt));
                    Assert.IsTrue(min <= destInt);
                    Assert.IsTrue(destInt < max);
                }
            }

            Template("Math,Rand,%Dest%", 0, 65536);
            Template("Math,Rand,%Dest%,0,16", 0, 16);
            Template("Math,Rand,%Dest%,16,64", 16, 64);
            Template("Math,Rand,%Dest%,32768,65536", 32768, 65536);

            // Test Error
            ErrorTemplate(s, "Math,Rand", ErrorCheck.ParserError);
            ErrorTemplate(s, "Math,Rand,%Dest%,0,1,2", ErrorCheck.ParserError);
            ErrorTemplate(s, $"Math,Rand,%Dest%,0,{(long)int.MaxValue + 16}", ErrorCheck.RuntimeError);
        }
        #endregion

        #region Templates
        public static void SuccessTemplate(EngineState s, string rawCode, string destCheck)
        {
            s.Variables.DeleteKey(VarsType.Local, "Dest");
            EngineTests.Eval(s, rawCode, CodeType.Math, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals(destCheck, StringComparison.Ordinal));
        }

        public static void ErrorTemplate(EngineState s, string rawCode, ErrorCheck check)
        {
            EngineTests.Eval(s, rawCode, CodeType.Math, check);
        }
        #endregion
    }
}


