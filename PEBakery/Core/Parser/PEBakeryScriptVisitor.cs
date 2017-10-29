using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime.Misc;

// https://codereview.stackexchange.com/questions/87889/hello-there-calculator

namespace PEBakery.Core.Parser
{
    public class PEBakeryScriptVisitor : PEBakeryScriptBaseVisitor<List<CodeCommand>>
    {
        public List<CodeCommand> codes = new List<CodeCommand>();

        public override List<CodeCommand> VisitCodes([NotNull] PEBakeryScriptParser.CodesContext context)
        {
            return base.VisitCodes(context);
        }

        public override List<CodeCommand> VisitCmd([NotNull] PEBakeryScriptParser.CmdContext context)
        {
            return base.VisitCmd(context);
        }

        public override List<CodeCommand> VisitCmd_filecopy([NotNull] PEBakeryScriptParser.Cmd_filecopyContext context)
        {
            // FileCopy,<SrcFile>,<DestPath>[,PRESERVE][,NOWARN][,NOREC]
            // cmd_filecopy : FILECOPY P_ STR P_ STR (P_ PRESERVE)? (P_ NOWARN)? (P_ NOREC)?;
            // context.FILECOPY().GetText();
            string srcFile = context.STR(0).GetText();
            string destPath = context.STR(1).GetText();
            bool preserve = (context.PRESERVE() != null);
            bool noWarn = (context.NOWARN() != null);
            bool noRec = (context.NOREC() != null);

            CodeInfo info = new CodeInfo_FileCopy(srcFile, destPath, preserve, noWarn, noRec);
            CodeCommand cmd = new CodeCommand(context.GetText(), CodeType.FileCopy, info);
            return new List<CodeCommand>() { cmd };
        }
    }
}
