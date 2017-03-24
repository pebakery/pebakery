using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.Core;

namespace PEBakery.UnitTest
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void ExtractFile()
        {
            Plugin plugin = new Plugin(PluginType.Plugin, @"E:\WinPE\Win10PESE\Projects\Win10PESE\Tweaks\Korean_IME.script", @"E:\WinPE\Win10PESE", 3);
            EncodedFile.ExtractFile(plugin, "Fonts", "D2Coding-Ver1.1-TTC-20151103.7z", out byte[] file);
        }

        public void EmbedFile()
        {

        }
    }
}
