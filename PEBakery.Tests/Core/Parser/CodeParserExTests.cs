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
        public void CodeParserEx_FileCopy()
        {
            PEBakeryScriptParser parser = Setup("FileCopy,1,2");

            parser.Remove
            PEBakeryScriptParser.CodesContext context = parser.codes();

            // PEBakeryScriptVisitor visitor = new PEBakeryScriptVisitor();
            // List<CodeCommand> codes = visitor.Visit(context);
            // Assert.IsTrue(codes != null);
        }
    }
}
