using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace PEBakery.Core.Tests
{
    [TestClass]
    [TestCategory(nameof(Project))]
    public class ProjectLoadTests
    {
        [TestMethod]
        public void LoadRepeatedlyPreservesProjectSemantics()
        {
            string[]? expectedOrder = null;

            for (int i = 0; i < 8; i++)
            {
                ProjectCollection projects = new ProjectCollection(EngineTests.BaseDir);
                (int scriptCount, int linkCount) = projects.PrepareLoad();
                var logs = projects.Load(null, null);

                Assert.AreEqual(0, logs.Count);
                Assert.AreEqual(1, projects.Count);

                Project project = projects[0];
                Assert.AreEqual(1, project.AllScripts.Count(x => x.IsMainScript));
                Assert.AreEqual(Project.Names.MainScriptFile, Path.GetFileName(project.MainScript.RealPath));
                Assert.AreEqual(scriptCount + 1, project.AllScripts.Count(x => x.Type == ScriptType.Script));
                Assert.AreEqual(linkCount, project.AllScripts.Count(x => x.Type == ScriptType.Link));

                string[] order = project.AllScripts.Select(x => $"{x.Type}:{x.TreePath}").ToArray();
                if (expectedOrder == null)
                    expectedOrder = order;
                else
                    CollectionAssert.AreEqual(expectedOrder, order);
            }
        }
    }
}
