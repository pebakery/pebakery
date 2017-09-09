using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.Core;
using PEBakery.Core.Commands;
using System.Collections.Generic;

namespace UnitTest.Core.Command
{
    [TestClass]
    public class UnitTest_CommandString
    {
        public static EngineState Eval(string rawCode, bool checkError)
        {
            // Create CodeCommand
            SectionAddress addr = UnitTest_Engine.DummySectionAddress();
            CodeCommand cmd = CodeParser.ParseOneRawLine(rawCode, addr);

            // Run CodeCommand
            EngineState s = UnitTest_Engine.CreateEngineState();
            List<LogInfo> logs = CommandString.StrFormat(s, cmd);

            // Assert
            if (checkError)
                UnitTest_Engine.CheckErrorLogs(logs);

            // Return EngineState
            return s;
        }

        #region IntToBytes
        [TestMethod]
        public void StrFormat_IntToBytes_1()
        {
            string rawCode = "StrFormat,IntToBytes,10240,%Dest%";
            EngineState s = Eval(rawCode, true);

            // Assert
            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("10KB", StringComparison.Ordinal));
        }

        [TestMethod]
        public void StrFormat_IntToBytes_2()
        {
            string rawCode = "StrFormat,IntToBytes,4404020,%Dest%";
            EngineState s = Eval(rawCode, true);

            // Assert
            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("4.2MB", StringComparison.Ordinal));
        }

        [TestMethod]
        public void StrFormat_IntToBytes_3()
        {
            string rawCode = "StrFormat,IntToBytes,5561982650,%Dest%";
            EngineState s = Eval(rawCode, true);

            // Assert
            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("5.18GB", StringComparison.Ordinal));
        }

        [TestMethod]
        public void StrFormat_IntToBytes_4()
        {
            string rawCode = "StrFormat,IntToBytes,2193525697413,%Dest%";
            EngineState s = Eval(rawCode, true);

            // Assert
            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("1.995TB", StringComparison.Ordinal));
        }

        [TestMethod]
        public void StrFormat_IntToBytes_5()
        {
            string rawCode = "StrFormat,IntToBytes,2270940112101573,%Dest%";
            EngineState s = Eval(rawCode, true);

            // Assert
            string dest = s.Variables["Dest"];
            Assert.IsTrue(s.Variables["Dest"].Equals("2.017PB", StringComparison.Ordinal));
        }

        [TestMethod]
        public void StrFormat_IntToBytes_6()
        {
            string rawCode = "StrFormat,IntToBytes,2229281815548396000,%Dest%";
            EngineState s = Eval(rawCode, true);

            // Assert
            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("1980PB", StringComparison.Ordinal));
        }
        #endregion

    }
}
