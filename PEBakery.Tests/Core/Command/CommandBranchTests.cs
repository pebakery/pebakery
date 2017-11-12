using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PEBakery.Tests.Core.Command
{
    [TestClass]
    public class CommandBranchTests
    {
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandBranch")]
        public void Branch_Condition()
        {
            EngineState s = EngineTests.CreateEngineState();
            BranchCondition cond;
            string dummy;

            // Equal
            cond = new BranchCondition(BranchConditionType.Equal, false, "A", "A");
            Assert.AreEqual(true, cond.Check(s, out dummy));
            cond = new BranchCondition(BranchConditionType.Equal, false, "A", "B");
            Assert.AreEqual(false, cond.Check(s, out dummy));
            cond = new BranchCondition(BranchConditionType.Equal, true, "A", "A");
            Assert.AreEqual(false, cond.Check(s, out dummy));
            cond = new BranchCondition(BranchConditionType.Equal, true, "A", "B");
            Assert.AreEqual(true, cond.Check(s, out dummy));
            cond = new BranchCondition(BranchConditionType.Equal, false, "11.1", "11.1.0");
            Assert.AreEqual(false, cond.Check(s, out dummy));
            // WB082 does not recognize hex integer representation
            cond = new BranchCondition(BranchConditionType.Equal, false, "15", "0xF");
            Assert.AreEqual(false, cond.Check(s, out dummy));

            // Smaller
            cond = new BranchCondition(BranchConditionType.Smaller, false, "A", "A");
            Assert.AreEqual(false, cond.Check(s, out dummy));
        }
    }
}
