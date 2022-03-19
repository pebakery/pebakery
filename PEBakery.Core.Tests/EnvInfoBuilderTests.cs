using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace PEBakery.Core.Tests
{
    [TestClass]
    [TestCategory(nameof(EnvInfoBuilder))]
    public class EnvInfoBuilderTests
    {
        #region EnvInfoBuilder
        [TestMethod]
        public void EnvInfoBuilderLog()
        {
            // Log purpose
            EnvInfoBuilder envInfos = new EnvInfoBuilder();
            Console.WriteLine("[*] Test Host");
            Console.WriteLine();
            Console.WriteLine($"{envInfos}");
        }
        #endregion
    }
}
