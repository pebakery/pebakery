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
using System;

namespace PEBakery.Core.Tests
{
    [TestClass]
    public class UIParserTests
    {
        #region ParseTemplate
        private static (UIControl, TUIInfo) ParseTemplate<TUIInfo>(string rawLine) where TUIInfo : UIInfo
        {
            int idx = 0;
            ScriptSection section = EngineTests.DummySection();
            UIControl? uiCtrl = UIParser.ParseUIControl(new string[1] { rawLine }, section, ref idx);
            Assert.IsNotNull(uiCtrl);
            Assert.IsTrue(uiCtrl.Info is TUIInfo);
            TUIInfo info = (TUIInfo)uiCtrl.Info;
            return (uiCtrl, info);
        }
        #endregion

        #region FailTemplate
        private static void FailTemplate(string rawLine)
        {
            UIControl? uiCtrl = null;
            try
            {
                int idx = 0;
                ScriptSection section = EngineTests.DummySection();
                uiCtrl = UIParser.ParseUIControl(new string[1] { rawLine }, section, ref idx);
            }
            catch
            {
                Assert.IsNull(uiCtrl);
                return;
            }
            Assert.Fail();
        }
        #endregion

        #region ParseFileBox
        [TestMethod]
        [TestCategory(nameof(UIParser))]
        public void ParseFileBox()
        {
            UIControl uiCtrl;
            UIInfo_FileBox info;

            // File select dialog, with Title/Filter/RunOptional
            (uiCtrl, info) = ParseTemplate<UIInfo_FileBox>(@"pFileBox1=C:\Windows\notepad.exe,1,13,240,290,200,20,file,""Title=Select Files"",""Filter=Executable Files|*.exe""");
            // Check UIControl
            Assert.AreEqual(UIControlType.FileBox, uiCtrl.Type);
            Assert.IsTrue(uiCtrl.Text.Equals(@"C:\Windows\notepad.exe", StringComparison.Ordinal));
            Assert.IsTrue(uiCtrl.Visibility);
            Assert.AreEqual(240, uiCtrl.X);
            Assert.AreEqual(290, uiCtrl.Y);
            Assert.AreEqual(200, uiCtrl.Width);
            Assert.AreEqual(20, uiCtrl.Height);
            // Check UIInfo_PathBox
            Assert.IsTrue(info.IsFile);
            Assert.IsNotNull(info.Title);
            Assert.IsTrue(info.Title.Equals("Select Files", StringComparison.Ordinal));
            Assert.IsNotNull(info.Filter);
            Assert.IsTrue(info.Filter.Equals("Executable Files|*.exe", StringComparison.Ordinal));
            Assert.IsNull(info.ToolTip);

            // File select dialog, with RunOptional
            (uiCtrl, info) = ParseTemplate<UIInfo_FileBox>(@"pFileBox1=C:\Windows\notepad.exe,1,13,240,290,200,20,FILE,__IrisService");
            Assert.AreEqual(UIControlType.FileBox, uiCtrl.Type);
            // Check UIInfo_PathBox
            Assert.IsTrue(info.IsFile);
            Assert.IsNull(info.Title);
            Assert.IsNull(info.Filter);
            Assert.IsNotNull(info.ToolTip);
            Assert.IsTrue(info.ToolTip.Equals("IrisService", StringComparison.Ordinal));

            // File select dialog, with random arg order
            (uiCtrl, info) = ParseTemplate<UIInfo_FileBox>(@"pFileBox1=C:\Windows\notepad.exe,1,13,240,290,200,20,file,__TOOLTIP,""Title=Select Files"",""Filter=Executable Files|*.exe""");
            Assert.AreEqual(UIControlType.FileBox, uiCtrl.Type);
            // Check UIInfo_PathBox
            Assert.IsTrue(info.IsFile);
            Assert.IsNotNull(info.Title);
            Assert.IsTrue(info.Title.Equals("Select Files", StringComparison.Ordinal));
            Assert.IsNotNull(info.Filter);
            Assert.IsTrue(info.Filter.Equals("Executable Files|*.exe", StringComparison.Ordinal));
            Assert.IsNotNull(info.ToolTip);
            Assert.IsTrue(info.ToolTip.Equals("TOOLTIP", StringComparison.Ordinal));

            // Directory select dialog, with Title
            (uiCtrl, info) = ParseTemplate<UIInfo_FileBox>(@"pFileBox1=C:\Windows\notepad.exe,1,13,240,290,200,20,dir,""Title=Windows 11""");
            Assert.AreEqual(UIControlType.FileBox, uiCtrl.Type);
            Assert.IsFalse(info.IsFile);
            Assert.IsNotNull(info.Title);
            Assert.IsTrue(info.Title.Equals("Windows 11", StringComparison.Ordinal));
            Assert.IsNull(info.Filter);

            // File select dialog, with implicit IsFile
            (uiCtrl, info) = ParseTemplate<UIInfo_FileBox>(@"pFileBox1=C:\Windows\notepad.exe,1,13,240,290,200,20");
            Assert.AreEqual(UIControlType.FileBox, uiCtrl.Type);
            Assert.IsFalse(info.IsFile);
            Assert.IsNull(info.Title);
            Assert.IsNull(info.Filter);

            // Parse error - Too many arguments
            FailTemplate(@"pFileBox1=C:\Windows\notepad.exe,1,13,240,290,200,20,file,""Title=Windows 11"",""Filter=All Files|*.*"",_Hello_,True,""Vulkan=Leaks""");
            // Parse error - Not supported RunOptional
            FailTemplate(@"pFileBox1=C:\Windows\notepad.exe,1,13,240,290,200,20,file,_Hello_,True");
            // Parse error - Invalid TitleKey/FilterKey
            FailTemplate(@"pFileBox1=C:\Windows\notepad.exe,1,13,240,290,200,20,file,""Invalid=Select Files""");
            // Parse error - DIR with FilterKey
            FailTemplate(@"pFileBox1=C:\Windows\notepad.exe,1,13,240,290,200,20,dir,""Filter=All Files|*.*""");
            // Parse error - Invalid IsFile
            FailTemplate(@"pFileBox1=C:\Windows\notepad.exe,1,13,240,290,200,20,Ad");
        }
        #endregion

        #region ParsePathBox
        [TestMethod]
        [TestCategory(nameof(UIParser))]
        public void ParsePathBox()
        {
            UIControl uiCtrl;
            UIInfo_PathBox info;

            // File select dialog, with Title/Filter/RunOptional
            (uiCtrl, info) = ParseTemplate<UIInfo_PathBox>(@"pPathBox1=C:\Windows\notepad.exe,1,20,240,290,200,20,file,""Title=Select Files"",""Filter=Executable Files|*.exe"",_Hello_,True");
            // Check UIControl
            Assert.AreEqual(UIControlType.PathBox, uiCtrl.Type);
            Assert.IsTrue(uiCtrl.Key.Equals("pPathBox1", StringComparison.Ordinal));
            Assert.IsTrue(uiCtrl.Text.Equals(@"C:\Windows\notepad.exe", StringComparison.Ordinal));
            Assert.IsTrue(uiCtrl.Visibility);
            Assert.AreEqual(240, uiCtrl.X);
            Assert.AreEqual(290, uiCtrl.Y);
            Assert.AreEqual(200, uiCtrl.Width);
            Assert.AreEqual(20, uiCtrl.Height);
            // Check UIInfo_PathBox
            Assert.IsTrue(info.IsFile);
            Assert.IsNotNull(info.Title);
            Assert.IsTrue(info.Title.Equals("Select Files", StringComparison.Ordinal));
            Assert.IsNotNull(info.Filter);
            Assert.IsTrue(info.Filter.Equals("Executable Files|*.exe", StringComparison.Ordinal));
            Assert.IsNotNull(info.SectionName);
            Assert.IsTrue(info.SectionName.Equals("Hello", StringComparison.Ordinal));
            Assert.IsTrue(info.HideProgress);
            Assert.IsNull(info.ToolTip);

            // File select dialog, with RunOptional
            (uiCtrl, info) = ParseTemplate<UIInfo_PathBox>(@"pPathBox1=C:\Windows\notepad.exe,1,20,240,290,200,20,FILE,_World_,False,__IrisService");
            Assert.AreEqual(UIControlType.PathBox, uiCtrl.Type);
            // Check UIInfo_PathBox
            Assert.IsTrue(info.IsFile);
            Assert.IsNull(info.Title);
            Assert.IsNull(info.Filter);
            Assert.IsNotNull(info.SectionName);
            Assert.IsTrue(info.SectionName.Equals("World", StringComparison.Ordinal));
            Assert.IsFalse(info.HideProgress);
            Assert.IsNotNull(info.ToolTip);
            Assert.IsTrue(info.ToolTip.Equals("IrisService", StringComparison.Ordinal));

            // File select dialog, with random arg order
            (uiCtrl, info) = ParseTemplate<UIInfo_PathBox>(@"pPathBox1=C:\Windows\notepad.exe,1,20,240,290,200,20,file,_Hello_,True,__TOOLTIP,""Title=Select Files"",""Filter=Executable Files|*.exe""");
            Assert.AreEqual(UIControlType.PathBox, uiCtrl.Type);
            // Check UIInfo_PathBox
            Assert.IsTrue(info.IsFile);
            Assert.IsNotNull(info.Title);
            Assert.IsTrue(info.Title.Equals("Select Files", StringComparison.Ordinal));
            Assert.IsNotNull(info.Filter);
            Assert.IsTrue(info.Filter.Equals("Executable Files|*.exe", StringComparison.Ordinal));
            Assert.IsNotNull(info.SectionName);
            Assert.IsTrue(info.SectionName.Equals("Hello", StringComparison.Ordinal));
            Assert.IsTrue(info.HideProgress);
            Assert.IsNotNull(info.ToolTip);
            Assert.IsTrue(info.ToolTip.Equals("TOOLTIP", StringComparison.Ordinal));

            // Directory select dialog, with Title
            (uiCtrl, info) = ParseTemplate<UIInfo_PathBox>(@"pPathBox1=C:\Windows\notepad.exe,1,20,240,290,200,20,dir,""Title=Windows 11""");
            Assert.AreEqual(UIControlType.PathBox, uiCtrl.Type);
            Assert.IsFalse(info.IsFile);
            Assert.IsNotNull(info.Title);
            Assert.IsTrue(info.Title.Equals("Windows 11", StringComparison.Ordinal));
            Assert.IsNull(info.Filter);
            Assert.IsNull(info.SectionName);
            Assert.IsFalse(info.HideProgress);

            // Directory select dialog, with Title/RunOptional
            (uiCtrl, info) = ParseTemplate<UIInfo_PathBox>(@"pPathBox1=C:\Windows\notepad.exe,1,20,240,290,200,20,dir,""Title=Windows 11"",_I_Hate_Memory_Leak_,True");
            Assert.AreEqual(UIControlType.PathBox, uiCtrl.Type);
            Assert.AreEqual(UIControlType.PathBox, uiCtrl.Type);
            Assert.IsFalse(info.IsFile);
            Assert.IsNotNull(info.Title);
            Assert.IsTrue(info.Title.Equals("Windows 11", StringComparison.Ordinal));
            Assert.IsNull(info.Filter);
            Assert.IsNotNull(info.SectionName);
            Assert.IsTrue(info.SectionName.Equals("I_Hate_Memory_Leak", StringComparison.Ordinal));
            Assert.IsTrue(info.HideProgress);

            // Parse error - Too many arguments
            FailTemplate(@"pPathBox1=C:\Windows\notepad.exe,1,20,240,290,200,20,file,""Title=Windows 11"",""Filter=All Files|*.*"",_Hello_,True,""Vulkan=Leaks""");
            // Parse error - Invalid TitleKey/FilterKey
            FailTemplate(@"pPathBox1=C:\Windows\notepad.exe,1,20,240,290,200,20,file,""Invalid=Select Files"",_Hello_,True");
            // Parse error - DIR with FilterKey
            FailTemplate(@"pPathBox1=C:\Windows\notepad.exe,1,20,240,290,200,20,dir,""Filter=All Files|*.*""");
            // Parse error - Invalid IsFile
            FailTemplate(@"pPathBox1=C:\Windows\notepad.exe,1,20,240,290,200,20,Ad");
            // Parse error - No IsFile
            FailTemplate(@"pPathBox1=C:\Windows\notepad.exe,1,20,240,290,200,20");
            // Parse error - SectionName without HideProgress
            FailTemplate(@"pPathBox1=C:\Windows\notepad.exe,1,20,240,290,200,20,dir,_Win11_Has_Severe_Memory_Leak_");
            // Parse error - HideProgress without SectionName
            FailTemplate(@"pPathBox1=C:\Windows\notepad.exe,1,20,240,290,200,20,dir,False");
        }
        #endregion
    }
}
