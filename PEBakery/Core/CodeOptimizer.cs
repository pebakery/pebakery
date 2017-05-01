using PEBakery.WPF;
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
     * Basic of Code Optimization
     * 
     * 같은 파일에 대해 File IO를 하는 명령어가 연달아 있을 경우
     * -> 한번에 묶어서 처리하면 IO 오버헤드를 크게 줄일 수 있다.
     * 
     * TXTAddLine, IniRead, IniWrite, Visible 등이 해당.
     * 
     * Visible의 경우, 배치 처리할 경우 DrawPlugin의 호출 횟수도 줄일 수 있다.
     * 
     */

    public static class CodeOptimizer
    {
        public static List<CodeCommand> Optimize(List<CodeCommand> cmdList)
        {
            List<CodeCommand> optimized = new List<CodeCommand>();

            List<CodeType> toOptimize = new List<CodeType>()
            {
                CodeType.Visible,
            };
            Dictionary<CodeType, List<CodeCommand>> opDict = new Dictionary<CodeType, List<CodeCommand>>();
            foreach (CodeType type in toOptimize)
                opDict[type] = new List<CodeCommand>();

            CodeType state = CodeType.None;
            for (int i = 0; i < cmdList.Count; i++)
            {
                CodeCommand cmd = cmdList[i];
                
                switch (state)
                {
                    case CodeType.None:
                        switch (cmd.Type)
                        {
                            case CodeType.Visible:
                                state = CodeType.Visible;
                                opDict[CodeType.Visible].Add(cmd);
                                break;
                            default:
                                optimized.Add(cmd);
                                break;
                        }
                        break;
                    case CodeType.Visible:
                        switch (cmd.Type)
                        {
                            case CodeType.Visible:
                                state = CodeType.Visible;
                                opDict[CodeType.Visible].Add(cmd);
                                break;
                            case CodeType.Comment: // Remove comments
                                break;
                            default:
                                state = CodeType.None;
                                CodeCommand visibleOp = OptimizeVisible(opDict[CodeType.Visible]);
                                optimized.Add(visibleOp);
                                opDict[CodeType.Visible].Clear();
                                break;
                        }
                        break;
                    default:
                        Debug.Assert(false);
                        break;
                }
            }

            // Finalize
            foreach (var kv in opDict)
            {
                if (0 < kv.Value.Count)
                {
                    switch (kv.Key)
                    {
                        case CodeType.Visible:
                            CodeCommand visibleOp = OptimizeVisible(kv.Value);
                            optimized.Add(visibleOp);
                            break;
                    }
                    
                }
            }
            
            return optimized;
        }

        private static CodeCommand OptimizeVisible(List<CodeCommand> cmdList)
        {
            List<CodeInfo_Visible> infoList = new List<CodeInfo_Visible>();
            foreach (CodeCommand cmd in cmdList)
            {
                CodeInfo_Visible info = cmd.Info as CodeInfo_Visible;
                if (info == null)
                    throw new InternalCodeInfoException();

                infoList.Add(info);
            }

            return new CodeCommand($"Optimized Visible at [{cmdList[0].Addr.Section.SectionName}]", cmdList[0].Addr, CodeType.VisibleOp, new CodeInfo_VisibleOp(infoList));
        }
    }
}
