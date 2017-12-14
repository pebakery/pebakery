using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime.Misc;
using PEBakery.Exceptions;

// https://codereview.stackexchange.com/questions/87889/hello-there-calculator
// http://jakubdziworski.github.io/java/2016/04/01/antlr_visitor_vs_listener.html

namespace PEBakery.Core.Parser
{
    public class CodesVisitor : PEBakeryScriptBaseVisitor<List<CodeCommand>>
    {
        private SectionAddress addr;

        public CodesVisitor(SectionAddress addr)
        {
            this.addr = addr;
        }

        public override List<CodeCommand> VisitCodes([NotNull] PEBakeryScriptParser.CodesContext context)
        {
            return Visit(context.block());
        }

        public override List<CodeCommand> VisitBlock([NotNull] PEBakeryScriptParser.BlockContext context)
        {
            List<CodeCommand> stmts = new List<CodeCommand>();
            foreach (var stmt in context.stmt())
            {
                stmts.AddRange(Visit(stmt));
            }

            foreach (var stmt in stmts)
            {
                Console.WriteLine(stmt);
            }

            return stmts;
        }

        public override List<CodeCommand> VisitBranchBlock([NotNull] PEBakeryScriptParser.BranchBlockContext context)
        {
            return base.VisitBranchBlock(context);
        }

        public override List<CodeCommand> VisitNormalStmt([NotNull] PEBakeryScriptParser.NormalStmtContext context)
        {
            string rawCode = context.GetText().Trim();
            CodeCommand cmd = CodeParser.ParseStatement(rawCode, addr);

            Console.WriteLine(cmd);

            return new List<CodeCommand>() { cmd };
        }

        public override List<CodeCommand> VisitIfStmt([NotNull] PEBakeryScriptParser.IfStmtContext context)
        {
            List<CodeCommand> codes = new List<CodeCommand>();

            IfCondVisitor ifVisitor = new IfCondVisitor(addr);
            BranchCondition cond = context.ifCond().Accept(ifVisitor);

            List<CodeCommand> ifBlock = Visit(context.branchBlock());

            CodeCommand ifCmd = new CodeCommand(context.GetText(), CodeType.If, new CodeInfo_If(cond, ifBlock), 0);
            codes.Add(ifCmd);

            if (context.elseStmt() != null)
            {
                StmtVisitor elseBlockVisitor = new StmtVisitor(addr);
                CodeCommand elseCmd = context.elseStmt().Accept(elseBlockVisitor);
                codes.Add(elseCmd);
            }

            return codes;
        }
    }

    public class StmtVisitor : PEBakeryScriptBaseVisitor<CodeCommand>
    {
        private SectionAddress addr;

        public StmtVisitor(SectionAddress addr)
        {
            this.addr = addr;
        }

        public override CodeCommand VisitElseStmt([NotNull] PEBakeryScriptParser.ElseStmtContext context)
        {
            List<CodeCommand> codes = new List<CodeCommand>();

            CodesVisitor blockVisitor = new CodesVisitor(addr);
            List<CodeCommand> elseBlock = context.branchBlock().Accept(blockVisitor);

            Console.WriteLine(context.GetText());

            CodeCommand elseCmd = new CodeCommand(context.GetText(), CodeType.Else, new CodeInfo_Else(elseBlock), 0);
            return elseCmd;
        }
    }

    public class IfCondVisitor : PEBakeryScriptBaseVisitor<BranchCondition>
    {
        private SectionAddress addr;

        public IfCondVisitor(SectionAddress addr)
        {
            this.addr = addr;
        }

        public override BranchCondition VisitIfCondComp([NotNull] PEBakeryScriptParser.IfCondCompContext context)
        {
            bool notFlag = (context.NOT() != null);
            
            BranchConditionType type;
            if (context.EQUAL() != null)
                type = BranchConditionType.Equal;
            else if (context.SMALLER() != null)
                type = BranchConditionType.Smaller;
            else if (context.SMALLEREQUAL() != null)
                type = BranchConditionType.SmallerEqual;
            else if (context.BIGGER() != null)
                type = BranchConditionType.Bigger;
            else if (context.BIGGEREQUAL() != null)
                type = BranchConditionType.BiggerEqual;
            else if (context.EQUALX() != null)
                type = BranchConditionType.EqualX;
            else if (context.NOTEQUAL() != null)
            {
                type = BranchConditionType.Equal;
                notFlag = !notFlag;
            }
            else
                throw new InternalParserException("Internal Parser Error at [BranchCondition]");

            string arg1 = context.STR(0).GetText();
            string arg2 = context.STR(1).GetText();

            return new BranchCondition(type, notFlag, arg1, arg2);
        }

        public override BranchCondition VisitIfCondArg1([NotNull] PEBakeryScriptParser.IfCondArg1Context context)
        {
            bool notFlag = (context.NOT() != null);

            BranchConditionType type;
            if (context.EXISTFILE() != null)
                type = BranchConditionType.ExistFile;
            else if (context.EXISTDIR() != null)
                type = BranchConditionType.ExistDir;
            else if (context.EXISTVAR() != null)
                type = BranchConditionType.ExistVar;
            else if (context.EXISTMACRO() != null)
                type = BranchConditionType.ExistMacro;
            else if (context.PING() != null)
                type = BranchConditionType.Ping;
            else if (context.QUESTION() != null)
                type = BranchConditionType.Question;
            else if (context.NOTEXISTFILE() != null)
            {
                type = BranchConditionType.ExistFile;
                notFlag = !notFlag;
            }
            else if (context.NOTEXISTDIR() != null)
            {
                type = BranchConditionType.ExistDir;
                notFlag = !notFlag;
            }
            else if (context.NOTEXISTVAR() != null)
            {
                type = BranchConditionType.ExistVar;
                notFlag = !notFlag;
            }
            else
                throw new InternalParserException("Internal Parser Error at [BranchCondition]");

            string arg1 = context.STR().GetText();

            return new BranchCondition(type, notFlag, arg1);
        }

        public override BranchCondition VisitIfCondArg2([NotNull] PEBakeryScriptParser.IfCondArg2Context context)
        {
            bool notFlag = (context.NOT() != null);

            BranchConditionType type;
            if (context.EXISTSECTION() != null)
                type = BranchConditionType.ExistSection;
            else if (context.EXISTREGSECTION() != null)
                type = BranchConditionType.ExistRegSection;
            else if (context.EXISTREGSUBKEY() != null)
                type = BranchConditionType.ExistRegSubKey;
            else if (context.NOTEXISTSECTION() != null)
            {
                type = BranchConditionType.ExistSection;
                notFlag = !notFlag;
            }
            else if (context.NOTEXISTREGSECTION() != null)
            {
                type = BranchConditionType.ExistRegSection;
                notFlag = !notFlag;
            }
            else
                throw new InternalParserException("Internal Parser Error at [BranchCondition]");

            string arg1 = context.STR(0).GetText();
            string arg2 = context.STR(1).GetText();

            return new BranchCondition(type, notFlag, arg1, arg2);
        }

        public override BranchCondition VisitIfCondArg3([NotNull] PEBakeryScriptParser.IfCondArg3Context context)
        {
            bool notFlag = (context.NOT() != null);

            BranchConditionType type;
            if (context.EXISTREGKEY() != null)
                type = BranchConditionType.ExistRegKey;
            else if (context.EXISTREGVALUE() != null)
                type = BranchConditionType.ExistRegValue;
            else if (context.QUESTION() != null)
                type = BranchConditionType.Question;
            else if (context.NOTEXISTREGKEY() != null)
            {
                type = BranchConditionType.ExistRegKey;
                notFlag = !notFlag;
            }
            else
                throw new InternalParserException("Internal Parser Error at [BranchCondition]");

            string arg1 = context.STR(0).GetText();
            string arg2 = context.STR(1).GetText();
            string arg3 = context.STR(2).GetText();

            return new BranchCondition(type, notFlag, arg1, arg2, arg3);
        }
    }

    #region Test
    /*
    public class PEBakeryScriptVisitor : PEBakeryScriptBaseVisitor<List<CodeCommand>>
    {
        public CodeCommand Root = new CodeCommand("TreeRoot", CodeType.None, new CodeInfo());

        #region Visitor
        public override List<CodeCommand> VisitCodes([NotNull] PEBakeryScriptParser.CodesContext context)
        {
            return ConvertBlock(context.block());
        }

        private List<CodeCommand> ConvertBlock([NotNull] PEBakeryScriptParser.BlockContext context)
        {
            List<CodeCommand> cmds = new List<CodeCommand>();

            PEBakeryScriptParser.StmtsContext[] stmts = context.stmts();
            foreach (var stmt in stmts)
            {
                PEBakeryScriptParser.NormalStmtContext normalStmt = stmt.normalStmt();
                PEBakeryScriptParser.IfStmtContext ifStmt = stmt.ifStmt();

                if (normalStmt != null)
                {
                    cmds.Add(ConvertNormalStmt(normalStmt));
                }
                else if (ifStmt != null)
                {
                    cmds.Add(ConvertIfStmt(ifStmt));
                }
            }

            return cmds;
        }

        private CodeCommand ConvertNormalStmt([NotNull] PEBakeryScriptParser.NormalStmtContext context)
        {
            SectionAddress addr = new SectionAddress();

            string rawCode = context.STRLINE().GetText();
            CodeCommand cmd = CodeParser.ParseRawLine(rawCode, addr);

            Console.WriteLine(cmd);

            return cmd;
        }

        private CodeCommand ConvertIfStmt([NotNull] PEBakeryScriptParser.IfStmtContext context)
        {
            PEBakeryScriptParser.IfCondContext ifCond = context.ifCond();
            PEBakeryScriptParser.BranchBlockContext ifBranch = context.branchBlock(0);
            PEBakeryScriptParser.BranchBlockContext elseBranch = context.branchBlock(1);

            
        }

        private BranchCondition ConvertIfCondContext(PEBakeryScriptParser.IfCondContext context)
        {
            PEBakeryScriptParser.IfCondCompContext ifCondComp = context.ifCondComp();
            PEBakeryScriptParser.IfCondArg1Context ifCondArg1 = context.ifCondArg1();
            PEBakeryScriptParser.IfCondArg2Context ifCondArg2 = context.ifCondArg2();
            PEBakeryScriptParser.IfCondArg3Context ifCondArg3 = context.ifCondArg3();

            if (ifCondComp != null)
            {
                string arg1 = ifCondComp.VARNAME().GetText();
                string arg2;
                if (ifCondComp.EQUAL() != null)
                    arg2 = ifCondComp.EQUAL().GetText();
                string arg3 = ifCondComp.STR().GetText();

                return new BranchCondition(BranchConditionType.Equal, )
            }
            else if (ifCondArg1 != null)
            {
                string arg1;
                if (ifCondArg1.EQUAL() != null)
                {
                    ifCondComp.EQUAL().GetText();
                }
                string arg2 = ifCondComp.STR().GetText();
            }
        }
        #endregion
    }
    */
    #endregion
}
