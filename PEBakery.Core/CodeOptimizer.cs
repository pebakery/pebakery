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

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace PEBakery.Core
{
    // [Basic of Code Optimization]
    // If a sequence of commands access same file, one file will be opened many time.
    // -> Pack them into one command to reduce disk access
    public static class CodeOptimizer
    {
        #region OptimizedCodeTypes
        private static readonly CodeType[] OptimizedCodeTypes =
        {
            CodeType.TXTAddLine,
            CodeType.TXTReplace,
            CodeType.TXTDelLine,
            CodeType.IniRead,
            CodeType.IniWrite,
            CodeType.IniDelete,
            CodeType.IniReadSection,
            CodeType.IniAddSection,
            CodeType.IniDeleteSection,
            CodeType.IniWriteTextLine,
            CodeType.Visible,
            CodeType.ReadInterface,
            CodeType.WriteInterface,
            CodeType.WimExtract,
            CodeType.WimPathAdd, // WimPathAdd is a representative of WimPath{Add, Delete, Rename}
        };
        #endregion

        #region OptimizeCommands
        public static List<CodeCommand> Optimize(List<CodeCommand> codes)
        {
            List<CodeCommand> opCodes = InternalOptimize(codes);
            foreach (CodeCommand cmd in opCodes)
            {
                switch (cmd.Type)
                {
                    case CodeType.If:
                        {
                            if (cmd.Info is null)
                                throw new InvalidCodeCommandException("cmd.Info is null", cmd);
                            CodeInfo_If info = (CodeInfo_If)cmd.Info;
                            info.Link = Optimize(info.Link);
                        }
                        break;
                    case CodeType.Else:
                        {
                            if (cmd.Info is null)
                                throw new InvalidCodeCommandException("cmd.Info is null", cmd);
                            CodeInfo_Else info = (CodeInfo_Else)cmd.Info;
                            info.Link = Optimize(info.Link);
                        }
                        break;
                }
            }
            return opCodes;
        }

        private static List<CodeCommand> InternalOptimize(List<CodeCommand> cmds)
        {
            List<CodeCommand> optimized = new List<CodeCommand>(cmds.Count);

            Dictionary<CodeType, List<CodeCommand>> opDict = OptimizedCodeTypes.ToDictionary(x => x, x => new List<CodeCommand>(cmds.Count / 2));

            CodeType s = CodeType.None;
            foreach (CodeCommand cmd in cmds)
            {
                bool loopAgain;
                do
                {
                    loopAgain = false;
                    switch (s)
                    {
                        #region Default
                        case CodeType.None:
                            switch (cmd.Type)
                            {
                                case CodeType.TXTAddLine:
                                case CodeType.TXTReplace:
                                case CodeType.TXTDelLine:
                                case CodeType.IniRead:
                                case CodeType.IniWrite:
                                case CodeType.IniDelete:
                                case CodeType.IniReadSection:
                                case CodeType.IniAddSection:
                                case CodeType.IniDeleteSection:
                                case CodeType.IniWriteTextLine:
                                case CodeType.Visible:
                                case CodeType.ReadInterface:
                                case CodeType.WriteInterface:
                                case CodeType.WimExtract:
                                    s = cmd.Type;
                                    opDict[cmd.Type].Add(cmd);
                                    break;
                                case CodeType.WimPathAdd:
                                case CodeType.WimPathDelete:
                                case CodeType.WimPathRename:
                                    s = CodeType.WimPathAdd; // Use WimPathAdd as representative
                                    opDict[CodeType.WimPathAdd].Add(cmd);
                                    break;
                                default:
                                    optimized.Add(cmd);
                                    break;
                            }
                            break;
                        #endregion
                        #region TXTAddLine
                        case CodeType.TXTAddLine:
                            Debug.Assert(opDict[s][0].Info is CodeInfo_TXTAddLine, "Invalid CodeInfo");
                            switch (cmd.Type)
                            {
                                case CodeType.TXTAddLine:
                                    {
                                        CodeInfo_TXTAddLine firstInfo = (CodeInfo_TXTAddLine)opDict[s][0].Info;
                                        if (firstInfo.OptimizeCompare(cmd.Info))
                                            opDict[s].Add(cmd);
                                        else
                                            goto default;
                                        break;
                                    }
                                case CodeType.Comment: // Remove comments
                                    break;
                                default: // Optimize them
                                    FinalizeSequence(s, opDict[s]);
                                    s = CodeType.None;
                                    loopAgain = true;
                                    break;
                            }
                            break;
                        #endregion
                        #region TXTReplace
                        case CodeType.TXTReplace:
                            Debug.Assert(opDict[s][0].Info is CodeInfo_TXTReplace);
                            switch (cmd.Type)
                            {
                                case CodeType.TXTReplace:
                                    {
                                        CodeInfo_TXTReplace firstInfo = (CodeInfo_TXTReplace)opDict[s][0].Info;
                                        if (firstInfo.OptimizeCompare(cmd.Info))
                                            opDict[s].Add(cmd);
                                        else
                                            goto default;
                                        break;
                                    }
                                case CodeType.Comment: // Remove comments
                                    break;
                                default: // Optimize them
                                    FinalizeSequence(s, opDict[s]);
                                    s = CodeType.None;
                                    loopAgain = true;
                                    break;
                            }
                            break;
                        #endregion
                        #region TXTDelLine
                        case CodeType.TXTDelLine:
                            Debug.Assert(opDict[s][0].Info is CodeInfo_TXTDelLine, "Invalid CodeInfo");
                            switch (cmd.Type)
                            {
                                case CodeType.TXTDelLine:
                                    {
                                        CodeInfo_TXTDelLine firstInfo = (CodeInfo_TXTDelLine)opDict[s][0].Info;
                                        if (firstInfo.OptimizeCompare(cmd.Info))
                                            opDict[s].Add(cmd);
                                        else
                                            goto default;
                                        break;
                                    }
                                case CodeType.Comment: // Remove comments
                                    break;
                                default: // Optimize them
                                    FinalizeSequence(s, opDict[s]);
                                    s = CodeType.None;
                                    loopAgain = true;
                                    break;
                            }
                            break;
                        #endregion
                        #region IniRead
                        case CodeType.IniRead:
                            Debug.Assert(opDict[s][0].Info is CodeInfo_IniRead, "Invalid CodeInfo");
                            switch (cmd.Type)
                            {
                                case CodeType.IniRead:
                                    {
                                        CodeInfo_IniRead firstInfo = (CodeInfo_IniRead)opDict[s][0].Info;
                                        if (firstInfo.OptimizeCompare(cmd.Info))
                                            opDict[s].Add(cmd);
                                        else
                                            goto default;
                                        break;
                                    }
                                case CodeType.Comment: // Remove comments
                                    break;
                                default: // Optimize them
                                    FinalizeSequence(s, opDict[s]);
                                    s = CodeType.None;
                                    loopAgain = true;
                                    break;
                            }
                            break;
                        #endregion
                        #region IniWrite
                        case CodeType.IniWrite:
                            Debug.Assert(opDict[s][0].Info is CodeInfo_IniWrite, "Invalid CodeInfo");
                            switch (cmd.Type)
                            {
                                case CodeType.IniWrite:
                                    {
                                        CodeInfo_IniWrite firstInfo = (CodeInfo_IniWrite)opDict[s][0].Info;
                                        if (firstInfo.OptimizeCompare(cmd.Info))
                                            opDict[s].Add(cmd);
                                        else
                                            goto default;
                                        break;
                                    }
                                case CodeType.Comment: // Remove comments
                                    break;
                                default: // Optimize them
                                    FinalizeSequence(s, opDict[s]);
                                    s = CodeType.None;
                                    loopAgain = true;
                                    break;
                            }
                            break;
                        #endregion
                        #region IinDelete
                        case CodeType.IniDelete:
                            Debug.Assert(opDict[s][0].Info is CodeInfo_IniDelete, "Invalid CodeInfo");
                            switch (cmd.Type)
                            {
                                case CodeType.IniDelete:
                                    {
                                        CodeInfo_IniDelete firstInfo = (CodeInfo_IniDelete)opDict[s][0].Info;
                                        if (firstInfo.OptimizeCompare(cmd.Info))
                                            opDict[s].Add(cmd);
                                        else
                                            goto default;
                                        break;
                                    }
                                case CodeType.Comment: // Remove comments
                                    break;
                                default: // Optimize them
                                    FinalizeSequence(s, opDict[s]);
                                    s = CodeType.None;
                                    loopAgain = true;
                                    break;
                            }
                            break;
                        #endregion
                        #region IniReadSection
                        case CodeType.IniReadSection:
                            Debug.Assert(opDict[s][0].Info is CodeInfo_IniReadSection);
                            switch (cmd.Type)
                            {
                                case CodeType.IniReadSection:
                                    {
                                        CodeInfo_IniReadSection firstInfo = (CodeInfo_IniReadSection)opDict[s][0].Info;
                                        if (firstInfo.OptimizeCompare(cmd.Info))
                                            opDict[s].Add(cmd);
                                        else
                                            goto default;
                                        break;
                                    }
                                case CodeType.Comment: // Remove comments
                                    break;
                                default: // Optimize them
                                    FinalizeSequence(s, opDict[s]);
                                    s = CodeType.None;
                                    loopAgain = true;
                                    break;
                            }
                            break;
                        #endregion
                        #region IniAddSection
                        case CodeType.IniAddSection:
                            Debug.Assert(opDict[s][0].Info is CodeInfo_IniAddSection, "Invalid CodeInfo");
                            switch (cmd.Type)
                            {
                                case CodeType.IniAddSection:
                                    {
                                        CodeInfo_IniAddSection firstInfo = (CodeInfo_IniAddSection)opDict[s][0].Info;
                                        if (firstInfo.OptimizeCompare(cmd.Info))
                                            opDict[s].Add(cmd);
                                        else
                                            goto default;
                                        break;
                                    }
                                case CodeType.Comment: // Remove comments
                                    break;
                                default: // Optimize them
                                    FinalizeSequence(s, opDict[s]);
                                    s = CodeType.None;
                                    loopAgain = true;
                                    break;
                            }
                            break;
                        #endregion
                        #region IniDeleteSection
                        case CodeType.IniDeleteSection:
                            Debug.Assert(opDict[s][0].Info is CodeInfo_IniDeleteSection, "Invalid CodeInfo");
                            switch (cmd.Type)
                            {
                                case CodeType.IniDeleteSection:
                                    {
                                        CodeInfo_IniDeleteSection firstInfo = (CodeInfo_IniDeleteSection)opDict[s][0].Info;
                                        if (firstInfo.OptimizeCompare(cmd.Info))
                                            opDict[s].Add(cmd);
                                        else
                                            goto default;
                                        break;
                                    }
                                case CodeType.Comment: // Remove comments
                                    break;
                                default: // Optimize them
                                    FinalizeSequence(s, opDict[s]);
                                    s = CodeType.None;
                                    loopAgain = true;
                                    break;
                            }
                            break;
                        #endregion
                        #region IniWriteTextLine
                        case CodeType.IniWriteTextLine:
                            Debug.Assert(opDict[s][0].Info is CodeInfo_IniWriteTextLine, "Invalid CodeInfo");
                            switch (cmd.Type)
                            {
                                case CodeType.IniWriteTextLine:
                                    {
                                        CodeInfo_IniWriteTextLine firstInfo = (CodeInfo_IniWriteTextLine)opDict[s][0].Info;
                                        if (firstInfo.OptimizeCompare(cmd.Info))
                                            opDict[s].Add(cmd);
                                        else
                                            goto default;
                                        break;
                                    }
                                case CodeType.Comment: // Remove comments
                                    break;
                                default: // Optimize them
                                    FinalizeSequence(s, opDict[s]);
                                    s = CodeType.None;
                                    loopAgain = true;
                                    break;
                            }
                            break;
                        #endregion
                        #region Visible
                        case CodeType.Visible:
                            switch (cmd.Type)
                            {
                                case CodeType.Visible:
                                    opDict[s].Add(cmd);
                                    break;
                                case CodeType.Comment: // Remove comments
                                    break;
                                default: // Optimize them
                                    FinalizeSequence(s, opDict[s]);
                                    s = CodeType.None;
                                    loopAgain = true;
                                    break;
                            }
                            break;
                        #endregion
                        #region ReadInterface
                        case CodeType.ReadInterface:
                            Debug.Assert(opDict[s][0].Info is CodeInfo_ReadInterface, "Invalid CodeInfo");
                            switch (cmd.Type)
                            {
                                case CodeType.ReadInterface:
                                    {
                                        CodeInfo_ReadInterface firstInfo = (CodeInfo_ReadInterface)opDict[s][0].Info;
                                        if (firstInfo.OptimizeCompare(cmd.Info))
                                            opDict[s].Add(cmd);
                                        else
                                            goto default;
                                        break;
                                    }
                                case CodeType.Comment: // Remove comments
                                    break;
                                default: // Optimize them
                                    FinalizeSequence(s, opDict[s]);
                                    s = CodeType.None;
                                    loopAgain = true;
                                    break;
                            }
                            break;
                        #endregion
                        #region WriteInterface
                        case CodeType.WriteInterface:
                            Debug.Assert(opDict[s][0].Info is CodeInfo_WriteInterface, "Invalid CodeInfo");
                            switch (cmd.Type)
                            {
                                case CodeType.WriteInterface:
                                    {
                                        CodeInfo_WriteInterface firstInfo = (CodeInfo_WriteInterface)opDict[s][0].Info;
                                        if (firstInfo.OptimizeCompare(cmd.Info))
                                            opDict[s].Add(cmd);
                                        else
                                            goto default;
                                        break;
                                    }
                                case CodeType.Comment: // Remove comments
                                    break;
                                default: // Optimize them
                                    FinalizeSequence(s, opDict[s]);
                                    s = CodeType.None;
                                    loopAgain = true;
                                    break;
                            }
                            break;
                        #endregion
                        #region WimExtract
                        case CodeType.WimExtract:
                            Debug.Assert(opDict[s][0].Info is CodeInfo_WimExtract, "Invalid CodeInfo");
                            switch (cmd.Type)
                            {
                                case CodeType.WimExtract:
                                    {
                                        CodeInfo_WimExtract firstInfo = (CodeInfo_WimExtract)opDict[s][0].Info;
                                        if (firstInfo.OptimizeCompare(cmd.Info))
                                            opDict[s].Add(cmd);
                                        else
                                            goto default;
                                        break;
                                    }
                                case CodeType.Comment: // Remove comments
                                    break;
                                default: // Optimize them
                                    FinalizeSequence(s, opDict[s]);
                                    s = CodeType.None;
                                    loopAgain = true;
                                    break;
                            }
                            break;
                        #endregion
                        #region WimPath Series
                        case CodeType.WimPathAdd: // Use WimPathAdd as a representative of WimPath{Add, Delete, Rename}
                            Debug.Assert(opDict[s][0].Info is CodeInfo_WimPathAdd ||
                                         opDict[s][0].Info is CodeInfo_WimPathDelete ||
                                         opDict[s][0].Info is CodeInfo_WimPathRename, "Invalid CodeInfo");
                            switch (cmd.Type)
                            {
                                case CodeType.WimPathAdd:
                                case CodeType.WimPathDelete:
                                case CodeType.WimPathRename:
                                    {
                                        CodeCommand firstCmd = opDict[s][0];
                                        if (firstCmd.Type == CodeType.WimPathAdd)
                                        {
                                            CodeInfo_WimPathAdd firstInfo = (CodeInfo_WimPathAdd)opDict[s][0].Info;
                                            if (firstInfo.OptimizeCompare(cmd.Info))
                                                opDict[s].Add(cmd);
                                            else
                                                goto default;
                                        }
                                        else if (firstCmd.Type == CodeType.WimPathDelete)
                                        {
                                            CodeInfo_WimPathDelete firstInfo = (CodeInfo_WimPathDelete)opDict[s][0].Info;
                                            if (firstInfo.OptimizeCompare(cmd.Info))
                                                opDict[s].Add(cmd);
                                            else
                                                goto default;
                                        }
                                        else if (firstCmd.Type == CodeType.WimPathRename)
                                        {
                                            CodeInfo_WimPathRename firstInfo = (CodeInfo_WimPathRename)opDict[s][0].Info;
                                            if (firstInfo.OptimizeCompare(cmd.Info))
                                                opDict[s].Add(cmd);
                                            else
                                                goto default;
                                        }
                                        break;
                                    }
                                case CodeType.Comment: // Remove comments
                                    break;
                                default: // Optimize them
                                    FinalizeSequence(s, opDict[s]);
                                    s = CodeType.None;
                                    loopAgain = true;
                                    break;
                            }
                            break;
                        #endregion
                        #region Error
                        default:
                            throw new InternalException("Internal Logic Error at CodeOptimizer.InternalOptimize()");
                            #endregion
                    }
                }
                while (loopAgain);
            }

            #region Finish
            foreach (var kv in opDict)
                FinalizeSequence(kv.Key, kv.Value);
            #endregion

            #region FinalizeSequence
            void FinalizeSequence(CodeType state, List<CodeCommand> cmdSeq)
            {
                CodeCommand opCmd;
                if (cmdSeq.Count == 1)
                    opCmd = cmdSeq[0];
                else if (1 < cmdSeq.Count)
                    opCmd = PackCommand(state, new List<CodeCommand>(cmdSeq));
                else // if (cmds.Count <= 0)
                    return;

                Debug.Assert(opCmd != null, "Internal Logic Error in CodeOptimizer.Optimize");
                optimized.Add(opCmd);

                cmdSeq.Clear();
            }
            #endregion

            return optimized;
        }
        #endregion

        #region PackCommand
        private static CodeCommand PackCommand(CodeType type, List<CodeCommand> cmds)
        {
            Debug.Assert(0 < cmds.Count);

            CodeType packType;
            CodeInfo packInfo;

            switch (type)
            {
                case CodeType.TXTAddLine:
                    packType = CodeType.TXTAddLineOp;
                    packInfo = new CodeInfo_TXTAddLineOp(cmds);
                    break;
                case CodeType.TXTReplace:
                    packType = CodeType.TXTReplaceOp;
                    packInfo = new CodeInfo_TXTReplaceOp(cmds);
                    break;
                case CodeType.TXTDelLine:
                    packType = CodeType.TXTDelLineOp;
                    packInfo = new CodeInfo_TXTDelLineOp(cmds);
                    break;
                case CodeType.IniRead:
                    packType = CodeType.IniReadOp;
                    packInfo = new CodeInfo_IniReadOp(cmds);
                    break;
                case CodeType.IniWrite:
                    packType = CodeType.IniWriteOp;
                    packInfo = new CodeInfo_IniWriteOp(cmds);
                    break;
                case CodeType.IniDelete:
                    packType = CodeType.IniDeleteOp;
                    packInfo = new CodeInfo_IniDeleteOp(cmds);
                    break;
                case CodeType.IniReadSection:
                    packType = CodeType.IniReadSectionOp;
                    packInfo = new CodeInfo_IniReadSectionOp(cmds);
                    break;
                case CodeType.IniAddSection:
                    packType = CodeType.IniAddSectionOp;
                    packInfo = new CodeInfo_IniAddSectionOp(cmds);
                    break;
                case CodeType.IniDeleteSection:
                    packType = CodeType.IniDeleteSectionOp;
                    packInfo = new CodeInfo_IniDeleteSectionOp(cmds);
                    break;
                case CodeType.IniWriteTextLine:
                    packType = CodeType.IniWriteTextLineOp;
                    packInfo = new CodeInfo_IniWriteTextLineOp(cmds);
                    break;
                case CodeType.Visible:
                    packType = CodeType.VisibleOp;
                    packInfo = new CodeInfo_VisibleOp(cmds);
                    break;
                case CodeType.ReadInterface:
                    packType = CodeType.ReadInterfaceOp;
                    packInfo = new CodeInfo_ReadInterfaceOp(cmds);
                    break;
                case CodeType.WriteInterface:
                    packType = CodeType.WriteInterfaceOp;
                    packInfo = new CodeInfo_WriteInterfaceOp(cmds);
                    break;
                case CodeType.WimExtract:
                    packType = CodeType.WimExtractOp;
                    packInfo = new CodeInfo_WimExtractOp(cmds);
                    break;
                case CodeType.WimPathAdd: // Use WimPathAdd as representative of WimPath*
                    packType = CodeType.WimPathOp;
                    packInfo = new CodeInfo_WimPathOp(cmds);
                    break;
                default:
                    throw new InternalException("Internal Logic Error at CodeOptimizer.InternalOptimize");
            }

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
    }
}
