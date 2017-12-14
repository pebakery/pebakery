using Antlr4.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.Core;
using PEBakery.Core.Parser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PEBakery.Tests.Core.Parser
{
    [TestClass]
    public class CodeParserExTests
    {
        private PEBakeryScriptParser Setup(string text)
        {
            AntlrInputStream inputStream = new AntlrInputStream(text);
            PEBakeryScriptLexer scriptLexer = new PEBakeryScriptLexer(inputStream);
            CommonTokenStream commonTokenStream = new CommonTokenStream(scriptLexer);
            PEBakeryScriptParser scriptParser = new PEBakeryScriptParser(commonTokenStream);

            return scriptParser;
        }

        [TestMethod]
        [TestCategory("CodeParserEx")]
        public void CodeParserEx()
        {
            StringBuilder b = new StringBuilder();
            b.AppendLine("If,ExistDir,%BaseDir%,Begin");
            b.AppendLine("  Set,%A%,True");
            b.AppendLine("  Echo,Hello");
            b.AppendLine("End");
            b.AppendLine("Else,Echo,World");
            b.AppendLine(@"FileCopy,1,2");

            PEBakeryScriptParser parser = Setup(b.ToString());

            CodesVisitor visitor = new CodesVisitor(EngineTests.DummySectionAddress());
            List<CodeCommand> cmds = visitor.Visit(parser.codes());

            Assert.IsTrue(cmds != null);
        }
    }
}
