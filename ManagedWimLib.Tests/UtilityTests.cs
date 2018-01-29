using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ManagedWimLib.Tests
{
    [TestClass]
    public class UtilityTests
    {
        [TestMethod]
        [TestCategory("WimLib")]
        public void GetErrorString()
        {
            Console.WriteLine(WimLibNative.GetErrorString(WimLibErrorCode.INVALID_IMAGE));
        }
    }
}
