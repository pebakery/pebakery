/*
    Copyright (C) 2016-2022 Hajin Jang
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PEBakery.Core
{
    /*
    [Basic of Code Optimization]
    If a sequence of commands access same file, one file will be opened many time.
    -> Pack them into one command to reduce disk access.

    [Optimization Process]
    (1) Group a seqeunce of commands which meets basic principles:
      - CodeType is equal (or, rarely, compatible) to each other
      - Commands perform equal operation to an same file.
    (2) Ensure that grouped commands do not have any varaible dependency between.
      - If dependency does exist, break the group into two, to prevent variable hazards.
    (3) Pack grouped commands into a single *Op command.
    */

    public static class CodeOptimizer
    {
        #region Optimize
        public static void Optimize(List<CodeCommand> rootBlock)
        {
            Queue<List<CodeCommand>> blockQueue = new Queue<List<CodeCommand>>();
            blockQueue.Enqueue(rootBlock);

            while (0 < blockQueue.Count)
            {
                List<CodeCommand> block = blockQueue.Dequeue();

                // Enqueue embedded codeblocks
                foreach (CodeCommand cmd in block)
                {
                    if (cmd.Info is CodeEmbedInfo eInfo)
                        blockQueue.Enqueue(eInfo.Link);
                }

                // [Pass 1] Group commands which can be possibly optimized
                List<OptRange> optimizableGroups = GroupOptimizableCommands(block);

                // [Pass 2] Perform variable dependency analysis, and break groups which has a dependency to each other.
                List<OptRange> optimizedGroups = VariableDependencyAnalysis(block, optimizableGroups);

                // [Pass 3] Optimize commands following optimizedGroups
                List<CodeCommand> optBlock = PackCommands(block, optimizedGroups);
                block.Clear();
                block.AddRange(optBlock);
            }
        }

        private static CodeType[] WimCodeTypeOp { get; } = new CodeType[] { CodeType.WimPathAdd, CodeType.WimPathDelete, CodeType.WimPathRename };
        private static List<OptRange> GroupOptimizableCommands(List<CodeCommand> block)
        {
            List<OptRange> optimizableGroups = new List<OptRange>();

            if (block.Count == 0)
                return optimizableGroups;

            for (int x = 0; x < block.Count; x++)
            {
                CodeCommand lastCmd = block[x];

                // Is next command optimizable? If yes, try again on next-next command.
                int optBlockEnd = -1;
                for (int y = x + 1; y < block.Count; y++)
                {
                    CodeCommand nextCmd = block[y];

                    // In a lot of case, lastCmd.Type and nextCmd.Type should be equal to be optimized.
                    if (lastCmd.Type != nextCmd.Type)
                    {
                        if (WimCodeTypeOp.Contains(lastCmd.Type) && WimCodeTypeOp.Contains(nextCmd.Type))
                        { // WimPathAdd, WimPathRename, WimPathDelete can be treated at once.
                        }
                        else if (nextCmd.Type == CodeType.Comment)
                        { // Do not check and ignore Comment.
                            continue;
                        }
                        else
                        { // CodeType is incompatible between lastCmd and nextCmd.
                            break;
                        }
                    }

                    if (lastCmd.Info.IsOptimizable(nextCmd.Info))
                        optBlockEnd = y;
                    else
                        break;
                }

                if (optBlockEnd != -1) // Found
                {
                    optimizableGroups.Add(new OptRange(lastCmd.Type, x, optBlockEnd + 1));
                    x = optBlockEnd;
                }
            }

            return optimizableGroups;
        }

        private static List<OptRange> VariableDependencyAnalysis(List<CodeCommand> block, List<OptRange> optimizableRange)
        {
            List<OptRange> optimizedGroups = new List<OptRange>();

            foreach (OptRange optRange in optimizableRange)
            {
                CodeCommand lastCmd = block[optRange.Begin];

                HashSet<string> blockOutVars = lastCmd.Info.OutVars();
                int lastIdx = optRange.Begin;
                for (int i = optRange.Begin; i < optRange.End; i++)
                {
                    if (lastIdx == i)
                        continue;
                        
                    CodeCommand cmd = block[i];

                    HashSet<string> inVars = cmd.Info.InVars();
                    HashSet<string> outVars = cmd.Info.OutVars();

                    if (inVars.Intersect(blockOutVars).Any())
                    { // Dependency exists between commands
                        optimizedGroups.Add(new OptRange(optRange.CodeType, lastIdx, i));
                        lastIdx = i;
                        blockOutVars.Clear();
                    }
                    else
                    { // No dependency
                        blockOutVars.UnionWith(outVars);
                    }
                }

                if (lastIdx + 1 < optRange.End)
                {
                    optimizedGroups.Add(new OptRange(optRange.CodeType, lastIdx, optRange.End));
                }
            }

            return optimizedGroups;
        }

        private static List<CodeCommand> PackCommands(List<CodeCommand> block, List<OptRange> optimizedRange)
        {
            if (optimizedRange.Count == 0)
                return new List<CodeCommand>(block);

            List<CodeCommand> optBlock = new List<CodeCommand>();

            int i = 0;
            while (i < block.Count)
            {
                CodeCommand cmd = block[i];

                if (OptRange.IsIndexInRanges(optimizedRange, i, out OptRange? detectedRange) && detectedRange != null)
                {
                    List<CodeCommand> cmdsToOpt = block.Skip(detectedRange.Begin).Take(detectedRange.Count).Where(x => x.Type != CodeType.Comment).ToList();
                    CodeCommand opCmd = PackCommand(detectedRange.CodeType, cmdsToOpt);
                    optBlock.Add(opCmd);
                    i += detectedRange.Count;
                }
                else
                {
                    optBlock.Add(cmd);
                    i += 1;
                }
            }

            return optBlock;
        }
        #endregion

        #region PackCommand
        private static Dictionary<CodeType, CodeType> RawOptCodeTypeDict { get; } = new Dictionary<CodeType, CodeType>()
        {
            [CodeType.TXTAddLine] = CodeType.TXTAddLineOp,
            [CodeType.TXTReplace] = CodeType.TXTReplaceOp,
            [CodeType.TXTDelLine] = CodeType.TXTDelLineOp,
            [CodeType.IniRead] = CodeType.IniReadOp,
            [CodeType.IniWrite] = CodeType.IniWriteOp,
            [CodeType.IniDelete] = CodeType.IniDeleteOp,
            [CodeType.IniReadSection] = CodeType.IniReadSectionOp,
            [CodeType.IniAddSection] = CodeType.IniAddSectionOp,
            [CodeType.IniDeleteSection] = CodeType.IniDeleteSectionOp,
            [CodeType.IniWriteTextLine] = CodeType.IniWriteTextLineOp,
            [CodeType.Visible] = CodeType.VisibleOp,
            [CodeType.ReadInterface] = CodeType.ReadInterfaceOp,
            [CodeType.WriteInterface] = CodeType.WriteInterfaceOp,
            [CodeType.WimExtract] = CodeType.WimExtractOp,
            [CodeType.WimPathAdd] = CodeType.WimPathOp,
            [CodeType.WimPathRename] = CodeType.WimPathOp,
            [CodeType.WimPathDelete] = CodeType.WimPathOp,
        };

        private static CodeCommand PackCommand(CodeType type, List<CodeCommand> cmds)
        {
            if (cmds.Count == 0)
                throw new InternalParserException($"No commands to optimize in {nameof(PackCommand)}");
            if (cmds.Count == 1)
                return cmds[0];

            if (RawOptCodeTypeDict.ContainsKey(type) == false)
                throw new InternalParserException($"Unsuppoerted CodeType [{type}] in {nameof(PackCommand)}");

            CodeType packType = RawOptCodeTypeDict[type];
            CodeInfo packInfo = new CodeOptInfo(cmds);
            return new CodeCommand(MergeRawCodes(cmds), cmds[0].Section, packType, packInfo, cmds[0].LineIdx);
        }

        private static string MergeRawCodes(List<CodeCommand> cmds)
        {
            StringBuilder b = new StringBuilder();
            for (int i = 0; i < cmds.Count; i++)
            {
                b.Append(cmds[i].RawCode);
                if (i + 1 < cmds.Count)
                    b.AppendLine();
            }

            return b.ToString();
        }
        #endregion

        #region (class) OptRange
        class OptRange
        {
            public CodeType CodeType { get; set; }
            public int Begin { get; set; }
            public int End { get; set; }
            public int Count => End - Begin;

            public OptRange(CodeType codeType, int begin, int end)
            {
                CodeType = codeType;
                Begin = begin;
                End = end;
            }

            public bool IsIndexInRanges(int idx)
            {
                return Begin <= idx && idx < End;
            }

            public static bool IsIndexInRanges(IEnumerable<OptRange> optRanges, int idx, out OptRange? outRange)
            {
                // 1 < x.Count was added to skip 1-count Range.
                OptRange? optRange = optRanges.FirstOrDefault(x => 1 < x.Count && x.IsIndexInRanges(idx));
                if (optRange != null)
                {
                    outRange = optRange;
                    return true;
                }
                outRange = null;
                return false;
            }

            public override string ToString() => $"OptRange({Begin}, {End})";
        }
        #endregion
    }
}
