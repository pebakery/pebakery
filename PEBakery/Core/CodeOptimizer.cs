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
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PEBakery.Exceptions;
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
        #region Enum, Dict and Delegate
        private static readonly List<CodeType> ToOptimize = new List<CodeType>
        {
            CodeType.TXTAddLine,
            CodeType.TXTReplace,
            CodeType.TXTDelLine,
            CodeType.INIRead,
            CodeType.INIWrite,
            CodeType.INIDelete,
            CodeType.INIReadSection,
            CodeType.INIAddSection,
            CodeType.INIDeleteSection,
            CodeType.INIWriteTextLine,
            CodeType.Visible,
            CodeType.WimExtract,
            CodeType.WimPathAdd, CodeType.WimPathDelete, CodeType.WimPathRename,
        };

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
                { CodeType.WimExtract, OptimizeWimExtract },
                { CodeType.WimPathAdd, OptimizeWimPath },
                { CodeType.WimPathDelete, OptimizeWimPath },
                { CodeType.WimPathRename, OptimizeWimPath },
            };

        private delegate CodeCommand PackCommandDelegate(List<CodeCommand> cmdList);
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
                        Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_If), "Invalid CodeInfo");
                        CodeInfo_If info = cmd.Info as CodeInfo_If;
                        Debug.Assert(info != null, "Invalid CodeInfo");

                        info.Link = Optimize(info.Link);
                        break;
                    }    
                    case CodeType.Else:
                    {
                        Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Else), "Invalid CodeInfo");
                        CodeInfo_Else info = cmd.Info as CodeInfo_Else;
                        Debug.Assert(info != null, "Invalid CodeInfo");

                        info.Link = Optimize(info.Link);
                        break;
                    }
                }
            }
            return opCodes;
        }

        private static List<CodeCommand> InternalOptimize(List<CodeCommand> codes)
        {
            List<CodeCommand> optimized = new List<CodeCommand>();
            
            Dictionary<CodeType, List<CodeCommand>> opDict = new Dictionary<CodeType, List<CodeCommand>>();
            foreach (CodeType type in ToOptimize)
                opDict[type] = new List<CodeCommand>();

            CodeType s = CodeType.None;
            foreach (CodeCommand cmd in codes)
            {
                switch (s)
                {
                    #region Default
                    case CodeType.None:
                        switch (cmd.Type)
                        {
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
                                Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_TXTAddLine), "Invalid CodeInfo");
                                CodeInfo_TXTAddLine firstInfo = opDict[s][0].Info as CodeInfo_TXTAddLine;
                                Debug.Assert(firstInfo != null, "Invalid CodeInfo");

                                if (cmd.Info is CodeInfo_TXTAddLine info &&
                                    info.FileName.Equals(firstInfo.FileName, StringComparison.OrdinalIgnoreCase) &&
                                    info.Mode.Equals(firstInfo.Mode, StringComparison.OrdinalIgnoreCase))
                                    opDict[s].Add(cmd);
                                else
                                    goto default;
                                break;
                            }    
                            case CodeType.Comment: // Remove comments
                                break;
                            default: // Optimize them
                                if (opDict[s].Count == 1)
                                {
                                    CodeCommand oneCmd = opDict[s][0];
                                    optimized.Add(oneCmd);
                                }
                                else
                                {
                                    CodeCommand opCmd = OptimizeTXTAddLine(opDict[s]);
                                    optimized.Add(opCmd);
                                }
                                opDict[s].Clear();
                                optimized.Add(cmd);
                                s = CodeType.None;
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
                                Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_TXTReplace), "Invalid CodeInfo");
                                CodeInfo_TXTReplace firstInfo = opDict[s][0].Info as CodeInfo_TXTReplace;
                                Debug.Assert(firstInfo != null, "Invalid CodeInfo");

                                if (cmd.Info is CodeInfo_TXTReplace info && info.FileName.Equals(firstInfo.FileName, StringComparison.OrdinalIgnoreCase))
                                    opDict[s].Add(cmd);
                                else
                                    goto default;
                                break;
                            }    
                            case CodeType.Comment: // Remove comments
                                break;
                            default: // Optimize them
                                if (opDict[s].Count == 1)
                                {
                                    CodeCommand oneCmd = opDict[s][0];
                                    optimized.Add(oneCmd);
                                }
                                else
                                {
                                    CodeCommand opCmd = OptimizeTXTReplace(opDict[s]);
                                    optimized.Add(opCmd);
                                }
                                opDict[s].Clear();
                                optimized.Add(cmd);
                                s = CodeType.None;
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
                                Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_TXTDelLine), "Invalid CodeInfo");
                                CodeInfo_TXTDelLine firstInfo = opDict[s][0].Info as CodeInfo_TXTDelLine;
                                Debug.Assert(firstInfo != null, "Invalid CodeInfo");

                                if (cmd.Info is CodeInfo_TXTDelLine info &&
                                    info.FileName.Equals(firstInfo.FileName, StringComparison.OrdinalIgnoreCase))
                                    opDict[s].Add(cmd);
                                else
                                    goto default;
                                break;
                            }    
                            case CodeType.Comment: // Remove comments
                                break;
                            default: // Optimize them
                                if (opDict[s].Count == 1)
                                {
                                    CodeCommand oneCmd = opDict[s][0];
                                    optimized.Add(oneCmd);
                                }
                                else
                                {
                                    CodeCommand opCmd = OptimizeTXTDelLine(opDict[s]);
                                    optimized.Add(opCmd);
                                }
                                opDict[s].Clear();
                                optimized.Add(cmd);
                                s = CodeType.None;
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
                                Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_IniRead), "Invalid CodeInfo");
                                CodeInfo_IniRead firstInfo = opDict[s][0].Info as CodeInfo_IniRead;
                                Debug.Assert(firstInfo != null, "Invalid CodeInfo");

                                if (cmd.Info is CodeInfo_IniRead info &&
                                    info.FileName.Equals(firstInfo.FileName, StringComparison.OrdinalIgnoreCase))
                                    opDict[s].Add(cmd);
                                else
                                    goto default;
                                break;
                            }        
                            case CodeType.Comment: // Remove comments
                                break;
                            default: // Optimize them
                                if (opDict[s].Count == 1)
                                {
                                    CodeCommand oneCmd = opDict[s][0];
                                    optimized.Add(oneCmd);
                                }
                                else
                                {
                                    CodeCommand opCmd = OptimizeINIRead(opDict[s]);
                                    optimized.Add(opCmd);
                                }
                                opDict[s].Clear();
                                optimized.Add(cmd);
                                s = CodeType.None;
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
                                Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_IniWrite), "Invalid CodeInfo");
                                CodeInfo_IniWrite firstInfo = opDict[s][0].Info as CodeInfo_IniWrite;
                                Debug.Assert(firstInfo != null, "Invalid CodeInfo");

                                if (cmd.Info is CodeInfo_IniWrite info &&
                                    info.FileName.Equals(firstInfo.FileName, StringComparison.OrdinalIgnoreCase))
                                    opDict[s].Add(cmd);
                                else
                                    goto default;
                                break;
                            }    
                            case CodeType.Comment: // Remove comments
                                break;
                            default: // Optimize them
                                if (opDict[s].Count == 1)
                                {
                                    CodeCommand oneCmd = opDict[s][0];
                                    optimized.Add(oneCmd);
                                }
                                else
                                {
                                    CodeCommand opCmd = OptimizeINIWrite(opDict[s]);
                                    optimized.Add(opCmd);
                                }
                                opDict[s].Clear();
                                optimized.Add(cmd);
                                s = CodeType.None;
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
                                Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_IniDelete), "Invalid CodeInfo");
                                CodeInfo_IniDelete firstInfo = opDict[s][0].Info as CodeInfo_IniDelete;
                                Debug.Assert(firstInfo != null, "Invalid CodeInfo");

                                if (cmd.Info is CodeInfo_IniDelete info &&
                                    info.FileName.Equals(firstInfo.FileName, StringComparison.OrdinalIgnoreCase))
                                    opDict[s].Add(cmd);
                                else
                                    goto default;
                                break;
                            }    
                            case CodeType.Comment: // Remove comments
                                break;
                            default: // Optimize them
                                if (opDict[s].Count == 1)
                                {
                                    CodeCommand oneCmd = opDict[s][0];
                                    optimized.Add(oneCmd);
                                }
                                else
                                {
                                    CodeCommand opCmd = OptimizeINIDelete(opDict[s]);
                                    optimized.Add(opCmd);
                                }
                                opDict[s].Clear();
                                optimized.Add(cmd);
                                s = CodeType.None;
                                break;
                        }
                        break;
                    #endregion
                    #region INIReadSection
                    case CodeType.INIReadSection:
                        Debug.Assert(opDict[s][0].Info.GetType() == typeof(CodeInfo_IniReadSection));
                        switch (cmd.Type)
                        {
                            case CodeType.INIAddSection:
                            {
                                Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_IniReadSection), "Invalid CodeInfo");
                                CodeInfo_IniReadSection firstInfo = opDict[s][0].Info as CodeInfo_IniReadSection;
                                Debug.Assert(firstInfo != null, "Invalid CodeInfo");

                                if (cmd.Info is CodeInfo_IniReadSection info &&
                                    info.FileName.Equals(firstInfo.FileName, StringComparison.OrdinalIgnoreCase))
                                    opDict[s].Add(cmd);
                                else
                                    goto default;
                                break;
                            }    
                            case CodeType.Comment: // Remove comments
                                break;
                            default: // Optimize them
                                if (opDict[s].Count == 1)
                                {
                                    CodeCommand oneCmd = opDict[s][0];
                                    optimized.Add(oneCmd);
                                }
                                else
                                {
                                    CodeCommand opCmd = OptimizeINIReadSection(opDict[s]);
                                    optimized.Add(opCmd);
                                }
                                opDict[s].Clear();
                                optimized.Add(cmd);
                                s = CodeType.None;
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
                                Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_IniAddSection), "Invalid CodeInfo");
                                CodeInfo_IniAddSection firstInfo = opDict[s][0].Info as CodeInfo_IniAddSection;
                                Debug.Assert(firstInfo != null, "Invalid CodeInfo");

                                if (cmd.Info is CodeInfo_IniAddSection info &&
                                    info.FileName.Equals(firstInfo.FileName, StringComparison.OrdinalIgnoreCase))
                                    opDict[s].Add(cmd);
                                else
                                    goto default;
                                break;
                            }    
                            case CodeType.Comment: // Remove comments
                                break;
                            default: // Optimize them
                                if (opDict[s].Count == 1)
                                {
                                    CodeCommand oneCmd = opDict[s][0];
                                    optimized.Add(oneCmd);
                                }
                                else
                                {
                                    CodeCommand opCmd = OptimizeINIAddSection(opDict[s]);
                                    optimized.Add(opCmd);
                                }
                                opDict[s].Clear();
                                optimized.Add(cmd);
                                s = CodeType.None;
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
                                Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_IniDeleteSection), "Invalid CodeInfo");
                                CodeInfo_IniDeleteSection firstInfo = opDict[s][0].Info as CodeInfo_IniDeleteSection;
                                Debug.Assert(firstInfo != null, "Invalid CodeInfo");

                                if (cmd.Info is CodeInfo_IniDeleteSection info &&
                                    info.FileName.Equals(firstInfo.FileName, StringComparison.OrdinalIgnoreCase))
                                    opDict[s].Add(cmd);
                                else
                                    goto default;
                                break;
                            }   
                            case CodeType.Comment: // Remove comments
                                break;
                            default: // Optimize them
                                if (opDict[s].Count == 1)
                                {
                                    CodeCommand oneCmd = opDict[s][0];
                                    optimized.Add(oneCmd);
                                }
                                else
                                {
                                    CodeCommand opCmd = OptimizeINIDeleteSection(opDict[s]);
                                    optimized.Add(opCmd);
                                }
                                opDict[s].Clear();
                                optimized.Add(cmd);
                                s = CodeType.None;
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
                                Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_IniWriteTextLine), "Invalid CodeInfo");
                                CodeInfo_IniWriteTextLine firstInfo = opDict[s][0].Info as CodeInfo_IniWriteTextLine;
                                Debug.Assert(firstInfo != null, "Invalid CodeInfo");

                                if (cmd.Info is CodeInfo_IniWriteTextLine info &&
                                    info.FileName.Equals(firstInfo.FileName, StringComparison.OrdinalIgnoreCase) &&
                                    info.Append == firstInfo.Append)
                                    opDict[s].Add(cmd);
                                else
                                    goto default;
                                break;
                            }    
                            case CodeType.Comment: // Remove comments
                                break;
                            default: // Optimize them
                                if (opDict[s].Count == 1)
                                {
                                    CodeCommand oneCmd = opDict[s][0];
                                    optimized.Add(oneCmd);
                                }
                                else
                                {
                                    CodeCommand opCmd = OptimizeINIWriteTextLine(opDict[s]);
                                    optimized.Add(opCmd);
                                }
                                opDict[s].Clear();
                                optimized.Add(cmd);
                                s = CodeType.None;
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
                                if (opDict[s].Count == 1)
                                {
                                    CodeCommand oneCmd = opDict[s][0];
                                    optimized.Add(oneCmd);
                                }
                                else
                                {
                                    CodeCommand opCmd = OptimizeVisible(opDict[s]);
                                    optimized.Add(opCmd);
                                }
                                opDict[s].Clear();
                                optimized.Add(cmd);
                                s = CodeType.None;
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
                                Finalize(s, opDict[s]);
                                optimized.Add(cmd);
                                s = CodeType.None;
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
                                Finalize(s, opDict[s]);
                                optimized.Add(cmd);
                                s = CodeType.None;
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

            void Finalize(CodeType state, List<CodeCommand> cmds)
            {
                if (cmds.Count == 1)
                    optimized.Add(cmds[0]);
                else
                    optimized.Add(PackDict[state](cmds));
                cmds.Clear();
            }

            #region Finalize
            foreach (var kv in opDict)
            {
                if (1 < kv.Value.Count)
                {
                    CodeCommand opCmd = PackDict[kv.Key](kv.Value);
                    /*
                    CodeCommand opCmd = null;
                    switch (kv.Key)
                    {
                        case CodeType.TXTAddLine:
                            opCmd = OptimizeTXTAddLine(kv.Value);
                            break;
                        case CodeType.TXTReplace:
                            opCmd = OptimizeTXTReplace(kv.Value);
                            break;
                        case CodeType.TXTDelLine:
                            opCmd = OptimizeTXTDelLine(kv.Value);
                            break;
                        case CodeType.INIWrite:
                            opCmd = OptimizeINIWrite(kv.Value);
                            break;
                        case CodeType.INIRead:
                            opCmd = OptimizeINIRead(kv.Value);
                            break;
                        case CodeType.INIDelete:
                            opCmd = OptimizeINIDelete(kv.Value);
                            break;
                        case CodeType.INIReadSection:
                            opCmd = OptimizeINIReadSection(kv.Value);
                            break;
                        case CodeType.INIAddSection:
                            opCmd = OptimizeINIAddSection(kv.Value);
                            break;
                        case CodeType.INIDeleteSection:
                            opCmd = OptimizeINIDeleteSection(kv.Value);
                            break;
                        case CodeType.INIWriteTextLine:
                            opCmd = OptimizeINIWriteTextLine(kv.Value);
                            break;
                        case CodeType.Visible:
                            opCmd = OptimizeVisible(kv.Value);
                            break;
                        case CodeType.WimExtract:
                            opCmd = OptimizeWimExtract(kv.Value);
                            break;
                        case CodeType.WimPathAdd:
                            opCmd = OptimizeWimPath(kv.Value);
                            break;
                    }
                    */
                    Debug.Assert(opCmd != null); // Logic Error
                    optimized.Add(opCmd);
                }
                else if (1 == kv.Value.Count)
                {
                    CodeCommand oneCmd = kv.Value[0];
                    optimized.Add(oneCmd);
                }
            }
            #endregion

            return optimized;
        }
        #endregion

        #region Optimize Individual
        // TODO: Is there any 'generic' way?
        private static CodeCommand OptimizeTXTAddLine(List<CodeCommand> cmdList)
        {
            Debug.Assert(0 < cmdList.Count);

            List<CodeInfo_TXTAddLine> infoList = new List<CodeInfo_TXTAddLine>();
            foreach (CodeCommand cmd in cmdList)
            {
                Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_TXTAddLine));
                CodeInfo_TXTAddLine info = cmd.Info as CodeInfo_TXTAddLine;

                infoList.Add(info);
            }

            string rawCode = $"Optimized TXTAddLine at [{cmdList[0].Addr.Section.Name}]";
            return new CodeCommand(rawCode, cmdList[0].Addr, CodeType.TXTAddLineOp, new CodeInfo_TXTAddLineOp(infoList), cmdList[0].LineIdx);
        }

        private static CodeCommand OptimizeTXTReplace(List<CodeCommand> cmdList)
        {
            Debug.Assert(0 < cmdList.Count);

            List<CodeInfo_TXTReplace> infoList = new List<CodeInfo_TXTReplace>();
            foreach (CodeCommand cmd in cmdList)
            {
                Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_TXTReplace));
                CodeInfo_TXTReplace info = cmd.Info as CodeInfo_TXTReplace;

                infoList.Add(info);
            }

            string rawCode = $"Optimized TXTReplace at [{cmdList[0].Addr.Section.Name}]";
            return new CodeCommand(rawCode, cmdList[0].Addr, CodeType.TXTReplaceOp, new CodeInfo_TXTReplaceOp(infoList), cmdList[0].LineIdx);
        }

        private static CodeCommand OptimizeTXTDelLine(List<CodeCommand> cmdList)
        {
            Debug.Assert(0 < cmdList.Count);

            List<CodeInfo_TXTDelLine> infoList = new List<CodeInfo_TXTDelLine>();
            foreach (CodeCommand cmd in cmdList)
            {
                Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_TXTDelLine));
                CodeInfo_TXTDelLine info = cmd.Info as CodeInfo_TXTDelLine;

                infoList.Add(info);
            }

            // string rawCode = $"Optimized TXTDelLine at [{cmdList[0].Addr.Section.SectionName}]";
            StringBuilder b = new StringBuilder();
            for (int i = 0; i < cmdList.Count; i++)
            {
                b.Append(cmdList[i].RawCode);
                if (i + 1 < cmdList.Count)
                    b.AppendLine();
            }
            return new CodeCommand(b.ToString(), cmdList[0].Addr, CodeType.TXTDelLineOp, new CodeInfo_TXTDelLineOp(infoList), cmdList[0].LineIdx);
        }

        private static CodeCommand OptimizeINIRead(List<CodeCommand> cmdList)
        {
            Debug.Assert(0 < cmdList.Count);
            
            List<CodeCommand> cmds = new List<CodeCommand>(cmdList);
            return new CodeCommand(MergeRawCodes(cmds), cmdList[0].Addr, CodeType.INIReadOp, new CodeInfo_IniReadOp(cmds), cmdList[0].LineIdx);
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

        private static CodeCommand OptimizeVisible(List<CodeCommand> cmdList)
        {
            Debug.Assert(0 < cmdList.Count);

            List<CodeInfo_Visible> infoList = new List<CodeInfo_Visible>();
            foreach (CodeCommand cmd in cmdList)
            {
                Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Visible));
                CodeInfo_Visible info = cmd.Info as CodeInfo_Visible;

                infoList.Add(info);
            }

            string rawCode = $"Optimized Visible at [{cmdList[0].Addr.Section.Name}]";
            return new CodeCommand(rawCode, cmdList[0].Addr, CodeType.VisibleOp, new CodeInfo_VisibleOp(infoList), cmdList[0].LineIdx);
        }

        private static CodeCommand OptimizeWimExtract(List<CodeCommand> cmdList)
        {
            Debug.Assert(0 < cmdList.Count);

            List<CodeCommand> cmds = new List<CodeCommand>(cmdList);
            string rawCode = $"Optimized WimExtract at [{cmdList[0].Addr.Section.Name}]";
            return new CodeCommand(rawCode, cmdList[0].Addr, CodeType.WimExtractOp, new CodeInfo_WimExtractOp(cmds), cmdList[0].LineIdx);
        }

        private static CodeCommand OptimizeWimPath(List<CodeCommand> cmdList)
        {
            Debug.Assert(0 < cmdList.Count);

            List<CodeCommand> cmds = new List<CodeCommand>(cmdList);
            string rawCode = $"Optimized WimPath at [{cmdList[0].Addr.Section.Name}]";
            return new CodeCommand(rawCode, cmdList[0].Addr, CodeType.WimPathOp, new CodeInfo_WimPathOp(cmds), cmdList[0].LineIdx);
        }
        #endregion

        #region Utility

        private static string MergeRawCodes(List<CodeCommand> cmdList)
        {
            StringBuilder b = new StringBuilder();
            for (int i = 0; i < cmdList.Count; i++)
            {
                b.Append(cmdList[i].RawCode);
                if (i + 1 < cmdList.Count)
                    b.AppendLine();
            }

            return b.ToString();
        }
        #endregion
    }
}
