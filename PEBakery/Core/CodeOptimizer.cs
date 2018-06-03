/*
    Copyright (C) 2016-2018 Hajin Jang
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
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace PEBakery.Core
{
    /* 
     * [Basic of Code Optimization]
     * If a seqeunce of commands access same file, one file will be opened many time.
     * -> Compact them so all of them can be done in ONE FILE IO.
     * 같은 파일에 대해 File IO를 하는 명령어가 연달아 있을 경우
     * -> 한번에 묶어서 처리하면 IO 오버헤드를 크게 줄일 수 있다.
     */

    public static class CodeOptimizer
    {
        #region Dictionary and Delegate
        private delegate CodeCommand PackCommandDelegate(List<CodeCommand> cmdList);

        private static readonly Dictionary<CodeType, PackCommandDelegate> PackDict =
            new Dictionary<CodeType, PackCommandDelegate>
            {
                { CodeType.TXTAddLine, OptimizeTXTAddLine },
                { CodeType.TXTReplace, OptimizeTXTReplace },
                { CodeType.TXTDelLine, OptimizeTXTDelLine },
                { CodeType.INIRead, OptimizeINIRead },
                { CodeType.INIWrite, OptimizeINIWrite },
                { CodeType.INIDelete, OptimizeINIDelete },
                { CodeType.INIReadSection, OptimizeINIReadSection },
                { CodeType.INIAddSection, OptimizeINIAddSection },
                { CodeType.INIDeleteSection, OptimizeINIDeleteSection },
                { CodeType.INIWriteTextLine, OptimizeINIWriteTextLine },
                { CodeType.Visible, OptimizeVisible },
                { CodeType.ReadInterface, OptimizeReadInterface },
                { CodeType.WriteInterface, OptimizeWriteInterface },
                { CodeType.WimExtract, OptimizeWimExtract },
                { CodeType.WimPathAdd, OptimizeWimPath }, // WimPathAdd is a representative of WimPath{Add, Delete, Rename}
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
                            CodeInfo_If info = cmd.Info.Cast<CodeInfo_If>();

                            info.Link = Optimize(info.Link);
                        }
                        break;
                    case CodeType.Else:
                        {
                            CodeInfo_Else info = cmd.Info.Cast<CodeInfo_Else>();

                            info.Link = Optimize(info.Link);
                        }
                        break;
                }
            }
            return opCodes;
        }

        private static List<CodeCommand> InternalOptimize(List<CodeCommand> codes)
        {
            List<CodeCommand> optimized = new List<CodeCommand>();

            Dictionary<CodeType, List<CodeCommand>> opDict = PackDict.ToDictionary(x => x.Key, x => new List<CodeCommand>(codes.Count / 2));

            CodeType s = CodeType.None;
            foreach (CodeCommand cmd in codes)
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
                                case CodeType.INIRead:
                                case CodeType.INIWrite:
                                case CodeType.INIReadSection:
                                case CodeType.INIAddSection:
                                case CodeType.INIDeleteSection:
                                case CodeType.INIWriteTextLine:
                                case CodeType.Visible:
                                case CodeType.ReadInterface:
                                case CodeType.WriteInterface:
                                case CodeType.WimExtract:
                                    s = cmd.Type;
                                    opDict[cmd.Type].Add(cmd);
                                    break;
                                /*
                                case CodeType.TXTAddLine:
                                    s = CodeType.TXTAddLine;
                                    opDict[CodeType.TXTAddLine].Add(cmd);
                                    break;
                                case CodeType.TXTReplace:
                                    s = CodeType.TXTReplace;
                                    opDict[CodeType.TXTReplace].Add(cmd);
                                    break;
                                case CodeType.TXTDelLine:
                                    s = CodeType.TXTDelLine;
                                    opDict[CodeType.TXTDelLine].Add(cmd);
                                    break;
                                case CodeType.INIWrite:
                                    s = CodeType.INIWrite;
                                    opDict[CodeType.INIWrite].Add(cmd);
                                    break;
                                case CodeType.INIRead:
                                    s = CodeType.INIRead;
                                    opDict[CodeType.INIRead].Add(cmd);
                                    break;
                                case CodeType.INIReadSection:
                                    s = CodeType.INIReadSection;
                                    opDict[CodeType.INIReadSection].Add(cmd);
                                    break;
                                case CodeType.INIAddSection:
                                    s = CodeType.INIAddSection;
                                    opDict[CodeType.INIAddSection].Add(cmd);
                                    break;
                                case CodeType.INIDeleteSection:
                                    s = CodeType.INIDeleteSection;
                                    opDict[CodeType.INIDeleteSection].Add(cmd);
                                    break;
                                case CodeType.INIWriteTextLine:
                                    s = CodeType.INIWriteTextLine;
                                    opDict[CodeType.INIWriteTextLine].Add(cmd);
                                    break;
                                case CodeType.Visible:
                                    s = CodeType.Visible;
                                    opDict[CodeType.Visible].Add(cmd);
                                    break;
                                case CodeType.WimExtract:
                                    s = CodeType.WimExtract;
                                    opDict[CodeType.WimExtract].Add(cmd);
                                    break;
                                    */
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
                            Debug.Assert(opDict[s][0].Info.GetType() == typeof(CodeInfo_TXTAddLine), "Invalid CodeInfo");
                            switch (cmd.Type)
                            {
                                case CodeType.TXTAddLine:
                                {
                                    CodeInfo_TXTAddLine firstInfo = opDict[s][0].Info.Cast<CodeInfo_TXTAddLine>();
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
                            Debug.Assert(opDict[s][0].Info.GetType() == typeof(CodeInfo_TXTReplace));
                            switch (cmd.Type)
                            {
                                case CodeType.TXTReplace:
                                {
                                    CodeInfo_TXTReplace firstInfo = opDict[s][0].Info.Cast<CodeInfo_TXTReplace>();
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
                            Debug.Assert(opDict[s][0].Info.GetType() == typeof(CodeInfo_TXTDelLine), "Invalid CodeInfo");
                            switch (cmd.Type)
                            {
                                case CodeType.TXTDelLine:
                                {
                                    CodeInfo_TXTDelLine firstInfo = opDict[s][0].Info.Cast<CodeInfo_TXTDelLine>();
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
                        #region INIRead
                        case CodeType.INIRead:
                            Debug.Assert(opDict[s][0].Info.GetType() == typeof(CodeInfo_IniRead), "Invalid CodeInfo");
                            switch (cmd.Type)
                            {
                                case CodeType.INIRead:
                                {
                                    CodeInfo_IniRead firstInfo = opDict[s][0].Info.Cast<CodeInfo_IniRead>();
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
                        #region INIWrite
                        case CodeType.INIWrite:
                            Debug.Assert(opDict[s][0].Info.GetType() == typeof(CodeInfo_IniWrite), "Invalid CodeInfo");
                            switch (cmd.Type)
                            {
                                case CodeType.INIWrite:
                                {
                                    CodeInfo_IniWrite firstInfo = opDict[s][0].Info.Cast<CodeInfo_IniWrite>();
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
                        #region INIDelete
                        case CodeType.INIDelete:
                            Debug.Assert(opDict[s][0].Info.GetType() == typeof(CodeInfo_IniDelete), "Invalid CodeInfo");
                            switch (cmd.Type)
                            {
                                case CodeType.INIDelete:
                                {
                                    CodeInfo_IniDelete firstInfo = opDict[s][0].Info.Cast<CodeInfo_IniDelete>();
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
                        #region INIReadSection
                        case CodeType.INIReadSection:
                            Debug.Assert(opDict[s][0].Info.GetType() == typeof(CodeInfo_IniReadSection));
                            switch (cmd.Type)
                            {
                                case CodeType.INIReadSection:
                                {
                                    CodeInfo_IniReadSection firstInfo = opDict[s][0].Info.Cast<CodeInfo_IniReadSection>();
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
                        #region INIAddSection
                        case CodeType.INIAddSection:
                            Debug.Assert(opDict[s][0].Info.GetType() == typeof(CodeInfo_IniAddSection), "Invalid CodeInfo");
                            switch (cmd.Type)
                            {
                                case CodeType.INIAddSection:
                                {
                                    CodeInfo_IniAddSection firstInfo = opDict[s][0].Info.Cast<CodeInfo_IniAddSection>();
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
                        #region INIDeleteSection
                        case CodeType.INIDeleteSection:
                            Debug.Assert(opDict[s][0].Info.GetType() == typeof(CodeInfo_IniDeleteSection), "Invalid CodeInfo");
                            switch (cmd.Type)
                            {
                                case CodeType.INIDeleteSection:
                                {
                                    CodeInfo_IniDeleteSection firstInfo = opDict[s][0].Info.Cast<CodeInfo_IniDeleteSection>();
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
                        #region INIWriteTextLine
                        case CodeType.INIWriteTextLine:
                            Debug.Assert(opDict[s][0].Info.GetType() == typeof(CodeInfo_IniWriteTextLine), "Invalid CodeInfo");
                            switch (cmd.Type)
                            {
                                case CodeType.INIWriteTextLine:
                                {
                                    CodeInfo_IniWriteTextLine firstInfo = opDict[s][0].Info.Cast<CodeInfo_IniWriteTextLine>();
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
                            Debug.Assert(opDict[s][0].Info.GetType() == typeof(CodeInfo_ReadInterface), "Invalid CodeInfo");
                            switch (cmd.Type)
                            {
                                case CodeType.ReadInterface:
                                    {
                                        CodeInfo_ReadInterface firstInfo = opDict[s][0].Info.Cast<CodeInfo_ReadInterface>();
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
                            Debug.Assert(opDict[s][0].Info.GetType() == typeof(CodeInfo_WriteInterface), "Invalid CodeInfo");
                            switch (cmd.Type)
                            {
                                case CodeType.WriteInterface:
                                    {
                                        CodeInfo_WriteInterface firstInfo = opDict[s][0].Info.Cast<CodeInfo_WriteInterface>();
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
                            Debug.Assert(opDict[s][0].Info.GetType() == typeof(CodeInfo_WimExtract), "Invalid CodeInfo");
                            switch (cmd.Type)
                            {
                                case CodeType.WimExtract:
                                {
                                    CodeInfo_WimExtract firstInfo = opDict[s][0].Info.Cast<CodeInfo_WimExtract>();
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
                            Debug.Assert(opDict[s][0].Info.GetType() == typeof(CodeInfo_WimPathAdd) ||
                                         opDict[s][0].Info.GetType() == typeof(CodeInfo_WimPathDelete) ||
                                         opDict[s][0].Info.GetType() == typeof(CodeInfo_WimPathRename), "Invalid CodeInfo");
                            switch (cmd.Type)
                            {
                                case CodeType.WimPathAdd:
                                case CodeType.WimPathDelete:
                                case CodeType.WimPathRename:
                                {
                                    CodeCommand firstCmd = opDict[s][0];
                                    if (firstCmd.Type == CodeType.WimPathAdd)
                                    {
                                        CodeInfo_WimPathAdd firstInfo = opDict[s][0].Info.Cast<CodeInfo_WimPathAdd>();
                                        if (firstInfo.OptimizeCompare(cmd.Info))
                                            opDict[s].Add(cmd);
                                        else
                                            goto default;
                                    }
                                    else if (firstCmd.Type == CodeType.WimPathDelete)
                                    {
                                        CodeInfo_WimPathDelete firstInfo = opDict[s][0].Info.Cast<CodeInfo_WimPathDelete>();
                                        if (firstInfo.OptimizeCompare(cmd.Info))
                                            opDict[s].Add(cmd);
                                        else
                                            goto default;
                                    }
                                    else if (firstCmd.Type == CodeType.WimPathRename)
                                    {
                                        CodeInfo_WimPathRename firstInfo = opDict[s][0].Info.Cast<CodeInfo_WimPathRename>();
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
                            Debug.Assert(false);
                            break;
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
            void FinalizeSequence(CodeType state, List<CodeCommand> cmds)
            {
                CodeCommand opCmd;
                if (cmds.Count == 1)
                    opCmd = cmds[0];
                else if (1 < cmds.Count)
                    opCmd = PackDict[state](cmds);
                else // if (cmds.Count < 0)
                    return;

                Debug.Assert(opCmd != null, "Internal Logic Error in CodeOptimizer.Optimize"); // Logic Error
                optimized.Add(opCmd);

                cmds.Clear();
            }
            #endregion

            return optimized;
        }
        #endregion

        #region Optimize Individual
        // TODO: Is there any 'generic' way?
        private static CodeCommand OptimizeTXTAddLine(List<CodeCommand> cmds)
        {
            Debug.Assert(0 < cmds.Count);

            return new CodeCommand(MergeRawCodes(cmds), cmds[0].Addr, CodeType.TXTAddLineOp, new CodeInfo_TXTAddLineOp(cmds), cmds[0].LineIdx);
        }

        private static CodeCommand OptimizeTXTReplace(List<CodeCommand> cmds)
        {
            Debug.Assert(0 < cmds.Count);

            return new CodeCommand(MergeRawCodes(cmds), cmds[0].Addr, CodeType.TXTReplaceOp, new CodeInfo_TXTReplaceOp(cmds), cmds[0].LineIdx);
        }

        private static CodeCommand OptimizeTXTDelLine(List<CodeCommand> cmds)
        {
            Debug.Assert(0 < cmds.Count);

            return new CodeCommand(MergeRawCodes(cmds), cmds[0].Addr, CodeType.TXTDelLineOp, new CodeInfo_TXTDelLineOp(cmds), cmds[0].LineIdx);
        }

        private static CodeCommand OptimizeINIRead(List<CodeCommand> cmds)
        {
            Debug.Assert(0 < cmds.Count);
            
            return new CodeCommand(MergeRawCodes(cmds), cmds[0].Addr, CodeType.INIReadOp, new CodeInfo_IniReadOp(cmds), cmds[0].LineIdx);
        }

        private static CodeCommand OptimizeINIWrite(List<CodeCommand> cmdList)
        {
            Debug.Assert(0 < cmdList.Count);

            List<CodeCommand> cmds = new List<CodeCommand>(cmdList);
            return new CodeCommand(MergeRawCodes(cmds), cmdList[0].Addr, CodeType.INIWriteOp, new CodeInfo_IniWriteOp(cmds), cmdList[0].LineIdx);
        }

        private static CodeCommand OptimizeINIDelete(List<CodeCommand> cmdList)
        {
            Debug.Assert(0 < cmdList.Count);

            List<CodeCommand> cmds = new List<CodeCommand>(cmdList);
            return new CodeCommand(MergeRawCodes(cmds), cmdList[0].Addr, CodeType.INIWriteTextLineOp, new CodeInfo_IniDeleteOp(cmds), cmdList[0].LineIdx);
        }

        private static CodeCommand OptimizeINIReadSection(List<CodeCommand> cmdList)
        {
            Debug.Assert(0 < cmdList.Count);

            List<CodeCommand> cmds = new List<CodeCommand>(cmdList);
            return new CodeCommand(MergeRawCodes(cmds), cmdList[0].Addr, CodeType.INIReadSectionOp, new CodeInfo_IniReadSectionOp(cmds), cmdList[0].LineIdx);
        }

        private static CodeCommand OptimizeINIAddSection(List<CodeCommand> cmdList)
        {
            Debug.Assert(0 < cmdList.Count);

            List<CodeCommand> cmds = new List<CodeCommand>(cmdList);
            return new CodeCommand(MergeRawCodes(cmds), cmdList[0].Addr, CodeType.INIAddSectionOp, new CodeInfo_IniAddSectionOp(cmds), cmdList[0].LineIdx);
        }

        private static CodeCommand OptimizeINIDeleteSection(List<CodeCommand> cmdList)
        {
            Debug.Assert(0 < cmdList.Count);

            List<CodeCommand> cmds = new List<CodeCommand>(cmdList);
            return new CodeCommand(MergeRawCodes(cmds), cmdList[0].Addr, CodeType.INIDeleteSectionOp, new CodeInfo_IniDeleteSectionOp(cmds), cmdList[0].LineIdx);
        }

        private static CodeCommand OptimizeINIWriteTextLine(List<CodeCommand> cmdList)
        {
            Debug.Assert(0 < cmdList.Count);

            List<CodeCommand> cmds = new List<CodeCommand>(cmdList);
            return new CodeCommand(MergeRawCodes(cmds), cmdList[0].Addr, CodeType.INIWriteTextLineOp, new CodeInfo_IniWriteTextLineOp(cmds), cmdList[0].LineIdx);
        }

        private static CodeCommand OptimizeVisible(List<CodeCommand> cmds)
        {
            Debug.Assert(0 < cmds.Count);

            return new CodeCommand(MergeRawCodes(cmds), cmds[0].Addr, CodeType.VisibleOp, new CodeInfo_VisibleOp(cmds), cmds[0].LineIdx);
        }

        private static CodeCommand OptimizeReadInterface(List<CodeCommand> cmds)
        {
            Debug.Assert(0 < cmds.Count);

            return new CodeCommand(MergeRawCodes(cmds), cmds[0].Addr, CodeType.ReadInterfaceOp, new CodeInfo_ReadInterfaceOp(cmds), cmds[0].LineIdx);
        }

        private static CodeCommand OptimizeWriteInterface(List<CodeCommand> cmds)
        {
            Debug.Assert(0 < cmds.Count);

            return new CodeCommand(MergeRawCodes(cmds), cmds[0].Addr, CodeType.WriteInterfaceOp, new CodeInfo_WriteInterfaceOp(cmds), cmds[0].LineIdx);
        }

        private static CodeCommand OptimizeWimExtract(List<CodeCommand> cmds)
        {
            Debug.Assert(0 < cmds.Count);

            return new CodeCommand(MergeRawCodes(cmds), cmds[0].Addr, CodeType.WimExtractOp, new CodeInfo_WimExtractOp(cmds), cmds[0].LineIdx);
        }

        private static CodeCommand OptimizeWimPath(List<CodeCommand> cmds)
        {
            Debug.Assert(0 < cmds.Count);

            return new CodeCommand(MergeRawCodes(cmds), cmds[0].Addr, CodeType.WimPathOp, new CodeInfo_WimPathOp(cmds), cmds[0].LineIdx);
        }
        #endregion

        #region Utility
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
