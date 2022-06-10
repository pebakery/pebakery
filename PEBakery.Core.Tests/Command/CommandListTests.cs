/*
    Copyright (C) 2018-2022 Hajin Jang
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

namespace PEBakery.Core.Tests.Command
{
    [TestClass]
    public class CommandListTests
    {
        #region ListGet
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandList")]
        public void ListGet()
        {
            EngineState s = EngineTests.CreateEngineState();

            ReadTemplate(s, "List,Get,%ListStr%,2,%Dest%", "1|2|3|4|5", "2");
            ReadTemplate(s, "List,Get,%ListStr%,0,%Dest%", "1|2|3|4|5", null, ErrorCheck.RuntimeError);
            ReadTemplate(s, "List,Get,%ListStr%,6,%Dest%", "1|2|3|4|5", null, ErrorCheck.RuntimeError);
            ReadTemplate(s, "List,Get,%ListStr%,2,%Dest%,Delim=$", "1$2$3$4$5", "2");
            ReadTemplate(s, "List,Get,%ListStr%,2,%Dest%,Delim=abc", "1abc2abc3abc4abc5", "2");
            ReadTemplate(s, "List,Get,%ListStr%,1,%Dest%,Delim=$", "1|2|3|4|5", "1|2|3|4|5");
            ReadTemplate(s, "List,Get,%ListStr%,2,%Dest%,Error", string.Empty, null, ErrorCheck.ParserError);
            ReadTemplate(s, "List,Get,%ListStr%,Z,%Dest%", "1|2|3|4|5", null, ErrorCheck.RuntimeError);
            ReadTemplate(s, "List,Get,ListStr,2,%Dest%", "1|2|3|4|5", null, ErrorCheck.ParserError);
            ReadTemplate(s, "List,Get,%ListStr%,2,Dest", "1|2|3|4|5", null, ErrorCheck.ParserError);
        }
        #endregion

        #region ListSet
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandList")]
        public void ListSet()
        {
            EngineState s = EngineTests.CreateEngineState();

            WriteTemplate(s, "List,Set,%ListStr%,2,Z", "1|2|3|4|5", "1|Z|3|4|5");
            WriteTemplate(s, "List,Set,%ListStr%,0,Z", "1|2|3|4|5", null, ErrorCheck.RuntimeError);
            WriteTemplate(s, "List,Set,%ListStr%,6,Z", "1|2|3|4|5", null, ErrorCheck.RuntimeError);
            WriteTemplate(s, "List,Set,%ListStr%,2,Z,Delim=$", "1$2$3$4$5", "1$Z$3$4$5");
            WriteTemplate(s, "List,Set,%ListStr%,2,Z,Delim=abc", "1abc2abc3abc4abc5", "1abcZabc3abc4abc5");
            WriteTemplate(s, "List,Set,%ListStr%,1,Z,Delim=$", "1|2|3|4|5", "Z");
            WriteTemplate(s, "List,Set,%ListStr%,2,Z,Error", "1|2|3|4|5", null, ErrorCheck.ParserError);
            WriteTemplate(s, "List,Set,%ListStr%,Z,Z", "1|2|3|4|5", null, ErrorCheck.RuntimeError);
            WriteTemplate(s, "List,Set,ListStr,Z,Z", "1|2|3|4|5", null, ErrorCheck.ParserError);
        }
        #endregion

        #region ListAppend
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandList")]
        public void ListAppend()
        {
            EngineState s = EngineTests.CreateEngineState();

            WriteTemplate(s, "List,Append,%ListStr%,Z", "1|2|3|4|5", "1|2|3|4|5|Z");
            WriteTemplate(s, "List,Append,%ListStr%,Z,Delim=$", "1|2|3|4|5", "1|2|3|4|5$Z");
            WriteTemplate(s, "List,Append,%ListStr%,Z,Delim=abc", "1|2|3|4|5", "1|2|3|4|5abcZ");
            WriteTemplate(s, "List,Append,%ListStr%,Z,Error", "1|2|3|4|5", null, ErrorCheck.ParserError);
            WriteTemplate(s, "List,Append,ListStr,Z", "1|2|3|4|5", null, ErrorCheck.ParserError);

            WriteTemplate(s, "List,Append,%ListStr%,Z", string.Empty, "Z");
            WriteTemplate(s, "List,Append,%ListStr%,Z", null, "Z");
        }
        #endregion

        #region ListInsert
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandList")]
        public void ListInsert()
        {
            EngineState s = EngineTests.CreateEngineState();

            WriteTemplate(s, "List,Insert,%ListStr%,3,Z", "1|2|3|4|5", "1|2|Z|3|4|5");
            WriteTemplate(s, "List,Insert,%ListStr%,0,Z", "1|2|3|4|5", null, ErrorCheck.RuntimeError);
            WriteTemplate(s, "List,Insert,%ListStr%,6,Z", "1|2|3|4|5", "1|2|3|4|5|Z");
            WriteTemplate(s, "List,Insert,%ListStr%,7,Z", "1|2|3|4|5", null, ErrorCheck.RuntimeError);
            WriteTemplate(s, "List,Insert,%ListStr%,2,Z,Delim=$", "1|2|3|4|5", "1|2|3|4|5$Z");
            WriteTemplate(s, "List,Insert,%ListStr%,2,Z,Delim=abc", "1|2|3|4|5", "1|2|3|4|5abcZ");
            WriteTemplate(s, "List,Insert,%ListStr%,2,Z,Delim=$", "1$2$3$4$5", "1$Z$2$3$4$5");
            WriteTemplate(s, "List,Insert,%ListStr%,Z,Z", "1|2|3|4|5", null, ErrorCheck.RuntimeError);
            WriteTemplate(s, "List,Insert,%ListStr%,Z", "1|2|3|4|5", null, ErrorCheck.ParserError);
            WriteTemplate(s, "List,Insert,ListStr,1,Z", "1|2|3|4|5", null, ErrorCheck.ParserError);
        }
        #endregion

        #region ListRemove
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandList")]
        public void ListRemove()
        {
            EngineState s = EngineTests.CreateEngineState();

            WriteTemplate(s, "List,Remove,%ListStr%,2", "1|2|3|4|5", "1|3|4|5");
            WriteTemplate(s, "List,Remove,%ListStr%,Z", "1|2|3|4|5", "1|2|3|4|5");
            WriteTemplate(s, "List,Remove,%ListStr%,b", "a|b|c|d|e", "a|c|d|e");
            WriteTemplate(s, "List,Remove,%ListStr%,b", "A|B|C|D|E", "A|C|D|E");
            WriteTemplate(s, "List,Remove,%ListStr%,2,Delim=$", "1|2|3|4|5", "1|2|3|4|5");
            WriteTemplate(s, "List,Remove,%ListStr%,2,Delim=$", "1$2$3$4$5", "1$3$4$5");
            WriteTemplate(s, "List,Remove,%ListStr%,Z,Error", "1|2|3|4|5", null, ErrorCheck.ParserError);
            WriteTemplate(s, "List,Remove,ListStr,Z", "1|2|3|4|5", null, ErrorCheck.ParserError);
        }
        #endregion

        #region ListRemoveX
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandList")]
        public void ListRemoveX()
        {
            EngineState s = EngineTests.CreateEngineState();

            WriteTemplate(s, "List,RemoveX,%ListStr%,2", "1|2|3|4|5", "1|3|4|5");
            WriteTemplate(s, "List,RemoveX,%ListStr%,Z", "1|2|3|4|5", "1|2|3|4|5");
            WriteTemplate(s, "List,RemoveX,%ListStr%,b", "a|b|c|d|e", "a|c|d|e");
            WriteTemplate(s, "List,RemoveX,%ListStr%,b", "A|B|C|D|E", "A|B|C|D|E");
            WriteTemplate(s, "List,RemoveX,%ListStr%,2,Delim=$", "1|2|3|4|5", "1|2|3|4|5");
            WriteTemplate(s, "List,RemoveX,%ListStr%,2,Delim=$", "1$2$3$4$5", "1$3$4$5");
            WriteTemplate(s, "List,RemoveX,%ListStr%,Z,Error", "1|2|3|4|5", null, ErrorCheck.ParserError);
            WriteTemplate(s, "List,RemoveX,ListStr,Z", "1|2|3|4|5", null, ErrorCheck.ParserError);
        }
        #endregion

        #region ListRemoveAt
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandList")]
        public void ListRemoveAt()
        {
            EngineState s = EngineTests.CreateEngineState();

            WriteTemplate(s, "List,RemoveAt,%ListStr%,2", "1|2|3|4|5", "1|3|4|5");
            WriteTemplate(s, "List,RemoveAt,%ListStr%,Z", "1|2|3|4|5", null, ErrorCheck.RuntimeError);
            WriteTemplate(s, "List,RemoveAt,%ListStr%,2,Delim=$", "1|2|3|4|5", null, ErrorCheck.RuntimeError);
            WriteTemplate(s, "List,RemoveAt,%ListStr%,2,Delim=$", "1$2$3$4$5", "1$3$4$5");
            WriteTemplate(s, "List,RemoveAt,%ListStr%,2,Error", "1|2|3|4|5", null, ErrorCheck.ParserError);
            WriteTemplate(s, "List,RemoveAt,ListStr,2", "1|2|3|4|5", null, ErrorCheck.ParserError);
        }
        #endregion

        #region ListCount
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandList")]
        public void ListCount()
        {
            EngineState s = EngineTests.CreateEngineState();

            ReadTemplate(s, "List,Count,%ListStr%,%Dest%", "1", "1");
            ReadTemplate(s, "List,Count,%ListStr%,%Dest%", "1|2|3", "3");
            ReadTemplate(s, "List,Count,%ListStr%,%Dest%", "1|2|3|4|5", "5");
            ReadTemplate(s, "List,Count,%ListStr%,%Dest%", "|10|98||50|32||0|1|5|2|4|3|", "14");
            ReadTemplate(s, "List,Count,ListStr,%Dest%", "1|2|3|4|5", null, ErrorCheck.ParserError);
            ReadTemplate(s, "List,Count,%ListStr%,Dest", "1|2|3|4|5", null, ErrorCheck.ParserError);
        }
        #endregion

        #region ListPos
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandList")]
        public void ListPos()
        {
            EngineState s = EngineTests.CreateEngineState();

            ReadTemplate(s, "List,Pos,%ListStr%,B,%Dest%", "A|B|C|D|B|E", "2");
            ReadTemplate(s, "List,Pos,%ListStr%,b,%Dest%", "A|B|C|D|B|E", "2");
            ReadTemplate(s, "List,Pos,%ListStr%,Z,%Dest%", "A|B|C|D|B|E", "0");
            ReadTemplate(s, "List,Pos,%ListStr%,z,%Dest%", "A|B|C|D|B|E", "0");
            ReadTemplate(s, "List,Pos,%ListStr%,B,%Dest%,Delim=$", "A$B$C$D$B$E", "2");
            ReadTemplate(s, "List,Pos,%ListStr%,Z,%Dest%,Delim=$", "A|B|C|D|B|E", "0");
            ReadTemplate(s, "List,Pos,%ListStr%,Z,%Dest%,Error", string.Empty, null, ErrorCheck.ParserError);
            ReadTemplate(s, "List,Pos,ListStr,Z,%Dest%", "A|B|C|D|B|E", null, ErrorCheck.ParserError);
            ReadTemplate(s, "List,Pos,%ListStr%,Z,Dest", "A|B|C|D|B|E", null, ErrorCheck.ParserError);
        }
        #endregion

        #region ListPosX
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandList")]
        public void ListPosX()
        {
            EngineState s = EngineTests.CreateEngineState();

            ReadTemplate(s, "List,PosX,%ListStr%,B,%Dest%", "A|B|C|D|B|E", "2");
            ReadTemplate(s, "List,PosX,%ListStr%,b,%Dest%", "A|B|C|D|B|E", "0");
            ReadTemplate(s, "List,PosX,%ListStr%,Z,%Dest%", "A|B|C|D|B|E", "0");
            ReadTemplate(s, "List,PosX,%ListStr%,z,%Dest%", "A|B|C|D|B|E", "0");
            ReadTemplate(s, "List,PosX,%ListStr%,B,%Dest%,Delim=$", "A$B$C$D$B$E", "2");
            ReadTemplate(s, "List,PosX,%ListStr%,Z,%Dest%,Delim=$", "A|B|C|D|B|E", "0");
            ReadTemplate(s, "List,PosX,%ListStr%,Z,%Dest%,Error", string.Empty, null, ErrorCheck.ParserError);
            ReadTemplate(s, "List,PosX,ListStr,Z,%Dest%", "A|B|C|D|B|E", null, ErrorCheck.ParserError);
            ReadTemplate(s, "List,PosX,%ListStr%,Z,Dest", "A|B|C|D|B|E", null, ErrorCheck.ParserError);
        }
        #endregion

        #region ListLastPos
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandList")]
        public void ListLastPos()
        {
            EngineState s = EngineTests.CreateEngineState();

            ReadTemplate(s, "List,LastPos,%ListStr%,B,%Dest%", "A|B|C|D|B|E", "5");
            ReadTemplate(s, "List,LastPos,%ListStr%,b,%Dest%", "A|B|C|D|B|E", "5");
            ReadTemplate(s, "List,LastPos,%ListStr%,Z,%Dest%", "A|B|C|D|B|E", "0");
            ReadTemplate(s, "List,LastPos,%ListStr%,z,%Dest%", "A|B|C|D|B|E", "0");
            ReadTemplate(s, "List,LastPos,%ListStr%,B,%Dest%,Delim=$", "A$B$C$D$B$E", "5");
            ReadTemplate(s, "List,LastPos,%ListStr%,Z,%Dest%,Delim=$", "A|B|C|D|B|E", "0");
            ReadTemplate(s, "List,LastPos,%ListStr%,Z,%Dest%,Error", string.Empty, null, ErrorCheck.ParserError);
            ReadTemplate(s, "List,LastPos,ListStr,Z,%Dest%", "A|B|C|D|B|E", null, ErrorCheck.ParserError);
            ReadTemplate(s, "List,LastPos,%ListStr%,Z,Dest", "A|B|C|D|B|E", null, ErrorCheck.ParserError);
        }
        #endregion

        #region ListLastPosX
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandList")]
        public void ListLastPosX()
        {
            EngineState s = EngineTests.CreateEngineState();

            ReadTemplate(s, "List,LastPosX,%ListStr%,B,%Dest%", "A|B|C|D|B|E", "5");
            ReadTemplate(s, "List,LastPosX,%ListStr%,b,%Dest%", "A|B|C|D|B|E", "0");
            ReadTemplate(s, "List,LastPosX,%ListStr%,Z,%Dest%", "A|B|C|D|B|E", "0");
            ReadTemplate(s, "List,LastPosX,%ListStr%,z,%Dest%", "A|B|C|D|B|E", "0");
            ReadTemplate(s, "List,LastPosX,%ListStr%,B,%Dest%,Delim=$", "A$B$C$D$B$E", "5");
            ReadTemplate(s, "List,LastPosX,%ListStr%,Z,%Dest%,Delim=$", "A|B|C|D|B|E", "0");
            ReadTemplate(s, "List,LastPosX,%ListStr%,Z,%Dest%,Error", string.Empty, null, ErrorCheck.ParserError);
            ReadTemplate(s, "List,LastPosX,ListStr,Z,%Dest%", "A|B|C|D|B|E", null, ErrorCheck.ParserError);
            ReadTemplate(s, "List,LastPosX,%ListStr%,Z,Dest", "A|B|C|D|B|E", null, ErrorCheck.ParserError);
        }
        #endregion

        #region ListSort
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandList")]
        public void ListSort()
        {
            EngineState s = EngineTests.CreateEngineState();

            WriteTemplate(s, "List,Sort,%ListStr%,ASC", "1|5|3|11|2|4", "1|11|2|3|4|5");
            WriteTemplate(s, "List,Sort,%ListStr%,DESC", "1|5|3|11|2|4", "5|4|3|2|11|1");
            WriteTemplate(s, "List,Sort,%ListStr%,ASC", "A1|a1|B|A11|A2", "A1|a1|A11|A2|B");
            WriteTemplate(s, "List,Sort,%ListStr%,DESC", "A1|a1|B|A11|A2", "B|A2|A11|a1|A1");
            WriteTemplate(s, "List,Sort,%ListStr%,ASC", "A|C|b|B|a", "A|a|B|b|C");
            WriteTemplate(s, "List,Sort,%ListStr%,DESC", "A|C|b|B|a", "C|b|B|a|A");
            WriteTemplate(s, "List,Sort,%ListStr%,ASC,Delim=$", "1|5|3|2|4", "1|5|3|2|4");
            WriteTemplate(s, "List,Sort,%ListStr%,ASC,Delim=$", "1$5$3$2$4", "1$2$3$4$5");
            WriteTemplate(s, "List,Sort,%ListStr%,Error", "1|2|3|4|5", null, ErrorCheck.RuntimeError);
            WriteTemplate(s, "List,Sort,ListStr,Z", "1|2|3|4|5", null, ErrorCheck.ParserError);
        }
        #endregion

        #region ListSortX
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandList")]
        public void ListSortX()
        {
            EngineState s = EngineTests.CreateEngineState();

            WriteTemplate(s, "List,SortX,%ListStr%,ASC", "1|5|3|11|2|4", "1|11|2|3|4|5");
            WriteTemplate(s, "List,SortX,%ListStr%,DESC", "1|5|3|11|2|4", "5|4|3|2|11|1");
            WriteTemplate(s, "List,SortX,%ListStr%,ASC", "A1|a1|B|A11|A2", "A1|A11|A2|B|a1");
            WriteTemplate(s, "List,SortX,%ListStr%,DESC", "A1|a1|B|A11|A2", "a1|B|A2|A11|A1");
            WriteTemplate(s, "List,SortX,%ListStr%,ASC", "A|C|b|B|a", "A|B|C|a|b");
            WriteTemplate(s, "List,SortX,%ListStr%,DESC", "A|C|b|B|a", "b|a|C|B|A");
            WriteTemplate(s, "List,SortX,%ListStr%,ASC,Delim=$", "1|5|3|2|4", "1|5|3|2|4");
            WriteTemplate(s, "List,SortX,%ListStr%,ASC,Delim=$", "1$5$3$2$4", "1$2$3$4$5");
            WriteTemplate(s, "List,SortX,%ListStr%,Error", "1|2|3|4|5", null, ErrorCheck.RuntimeError);
            WriteTemplate(s, "List,SortX,ListStr,Z", "1|2|3|4|5", null, ErrorCheck.ParserError);
        }
        #endregion

        #region ListSortN
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandList")]
        public void ListSortN()
        {
            EngineState s = EngineTests.CreateEngineState();

            WriteTemplate(s, "List,SortN,%ListStr%,ASC", "1|5|3|11|2|4", "1|2|3|4|5|11");
            WriteTemplate(s, "List,SortN,%ListStr%,DESC", "1|5|3|11|2|4", "11|5|4|3|2|1");
            WriteTemplate(s, "List,SortN,%ListStr%,ASC", "A1|a1|B|A11|A2", "A1|a1|A2|A11|B");
            WriteTemplate(s, "List,SortN,%ListStr%,DESC", "A1|a1|B|A11|A2", "B|A11|A2|a1|A1");
            WriteTemplate(s, "List,SortN,%ListStr%,ASC", "A|C|b|B|a", "A|a|B|b|C");
            WriteTemplate(s, "List,SortN,%ListStr%,DESC", "A|C|b|B|a", "C|b|B|a|A");
            WriteTemplate(s, "List,SortN,%ListStr%,ASC,Delim=$", "1|5|3|2|4", "1|5|3|2|4");
            WriteTemplate(s, "List,SortN,%ListStr%,ASC,Delim=$", "1$5$3$2$4", "1$2$3$4$5");
            WriteTemplate(s, "List,SortN,%ListStr%,Error", "1|2|3|4|5", null, ErrorCheck.RuntimeError);
            WriteTemplate(s, "List,SortN,ListStr,Z", "1|2|3|4|5", null, ErrorCheck.ParserError);
        }
        #endregion

        #region ListSortNX
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandList")]
        public void ListSortNX()
        {
            EngineState s = EngineTests.CreateEngineState();

            WriteTemplate(s, "List,SortNX,%ListStr%,ASC", "1|5|3|11|2|4", "1|2|3|4|5|11");
            WriteTemplate(s, "List,SortNX,%ListStr%,DESC", "1|5|3|11|2|4", "11|5|4|3|2|1");
            WriteTemplate(s, "List,SortNX,%ListStr%,ASC", "A1|a1|B|A11|A2", "A1|A2|A11|B|a1");
            WriteTemplate(s, "List,SortNX,%ListStr%,DESC", "A1|a1|B|A11|A2", "a1|B|A11|A2|A1");
            WriteTemplate(s, "List,SortNX,%ListStr%,ASC", "A|C|b|B|a", "A|B|C|a|b");
            WriteTemplate(s, "List,SortNX,%ListStr%,DESC", "A|C|b|B|a", "b|a|C|B|A");
            WriteTemplate(s, "List,SortNX,%ListStr%,ASC,Delim=$", "1|5|3|2|4", "1|5|3|2|4");
            WriteTemplate(s, "List,SortNX,%ListStr%,ASC,Delim=$", "1$5$3$2$4", "1$2$3$4$5");
            WriteTemplate(s, "List,SortNX,%ListStr%,Error", "1|2|3|4|5", null, ErrorCheck.RuntimeError);
            WriteTemplate(s, "List,SortNX,ListStr,Z", "1|2|3|4|5", null, ErrorCheck.ParserError);
        }
        #endregion

        #region ListRange
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandList")]
        public void ListRange()
        {
            EngineState s = EngineTests.CreateEngineState();


            WriteTemplate(s, "List,Range,%ListStr%,0,7,1", null, "0|1|2|3|4|5|6");
            WriteTemplate(s, "List,Range,%ListStr%,0,7,2", null, "0|2|4|6");
            WriteTemplate(s, "List,Range,%ListStr%,0,7,8", null, "0");
            WriteTemplate(s, "List,Range,%ListStr%,7,0,-1", null, "7|6|5|4|3|2|1");
            WriteTemplate(s, "List,Range,%ListStr%,7,0,-2", null, "7|5|3|1");
            WriteTemplate(s, "List,Range,%ListStr%,7,0,-8", null, "7");

            WriteTemplate(s, "List,Range,%ListStr%,0,7,1,Delim=$", null, "0$1$2$3$4$5$6");
            WriteTemplate(s, "List,Range,%ListStr%,0,7,1,Delim=abc", null, "0abc1abc2abc3abc4abc5abc6");

            WriteTemplate(s, "List,Range,%ListStr%,0,7,", null, null, ErrorCheck.RuntimeError);
            WriteTemplate(s, "List,Range,%ListStr%,0,7,-1", null, null, ErrorCheck.RuntimeError);
            WriteTemplate(s, "List,Range,%ListStr%,7,0,1", null, null, ErrorCheck.RuntimeError);
            WriteTemplate(s, "List,Range,%ListStr%,7,0,2", null, null, ErrorCheck.RuntimeError);

            WriteTemplate(s, "List,Range,%ListStr%,0,", null, null, ErrorCheck.ParserError);
            WriteTemplate(s, "List,Range,%ListStr%,0,7", null, null, ErrorCheck.ParserError);
            WriteTemplate(s, "List,Range,%ListStr%,7,0", null, null, ErrorCheck.ParserError);
            WriteTemplate(s, "List,Range,%ListStr%,GDP", null, null, ErrorCheck.ParserError);
        }
        #endregion

        #region Template
        public static void ReadTemplate(EngineState s, string rawCode, string listStr, string? expected, ErrorCheck check = ErrorCheck.Success)
        {
            s.Variables.DeleteKey(VarsType.Local, "Dest");
            s.Variables["ListStr"] = listStr;

            EngineTests.Eval(s, rawCode, CodeType.List, check);
            if (check == ErrorCheck.Success || check == ErrorCheck.Warning)
            {
                string dest = s.Variables["Dest"];
                Assert.IsTrue(dest.Equals(expected, StringComparison.Ordinal));
            }
        }

        public static void WriteTemplate(EngineState s, string rawCode, string? listStr, string? expected, ErrorCheck check = ErrorCheck.Success)
        {
            if (listStr == null)
                s.Variables.DeleteKey(VarsType.Local, "listStr");
            else
                s.Variables["ListStr"] = listStr;

            EngineTests.Eval(s, rawCode, CodeType.List, check);
            if (check == ErrorCheck.Success || check == ErrorCheck.Warning)
            {
                string dest = s.Variables["ListStr"];
                Assert.IsTrue(dest.Equals(expected, StringComparison.Ordinal));
            }
        }
        #endregion
    }
}
