/*
    Copyright (C) 2023 Hajin Jang
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
using System.IO;

namespace PEBakery.Core.Tests
{
    [TestClass]
    public class ScriptSectionTests
    {
        [TestMethod]
        [TestCategory(nameof(ScriptSection))]
        public void DeepInspect()
        {
            static void Template(string scriptPath, string sectionName, SectionType expectType)
            {
                EngineState s = EngineTests.CreateEngineState();
                string pbOriginScript = Path.Combine("%TestBench%", "ScriptSection", scriptPath);
                string originScript = StringEscaper.Preprocess(s, pbOriginScript);

                Script? sc = s.Project.LoadScriptRuntime(originScript, new LoadScriptRuntimeOptions());
                Assert.IsNotNull(sc);

                Assert.IsTrue(sc.Sections.ContainsKey(sectionName));
                ScriptSection section = sc.Sections[sectionName];
                SectionType beforeType = section.Type;
                section.DeepInspect();
                SectionType afterType = section.Type;
                Console.WriteLine($"Section [{sectionName}]: Before={beforeType}, After={afterType}");
                Assert.AreEqual(expectType, afterType);
            }

            const string targetScript = "DeepInspect.script";
            Template(targetScript, "Interface", SectionType.Interface);
            Template(targetScript, "HiddenInterface01", SectionType.Interface);
            Template(targetScript, "Process", SectionType.Code);
            Template(targetScript, "KnownCodeSection01", SectionType.Code);
            Template(targetScript, "OrphanCodeSection01", SectionType.Code);
            Template(targetScript, "Variables01", SectionType.Variables);
            Template(targetScript, "Variables02", SectionType.Variables);
            Template(targetScript, "Variables03", SectionType.Variables);
            Template(targetScript, "IniSection01", SectionType.SimpleIni);
            Template(targetScript, "Commentary01", SectionType.Commentary);
            Template(targetScript, "Commentary02", SectionType.Commentary);
        }
    }
}
