using PEBakery.Exceptions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PEBakery.Core.Commands
{
    public static class CommandSystem
    {
        /// <summary>
        /// Function for ShellExecute, ShellExecuteDelete
        /// </summary>
        /// <param name="s"></param>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public static List<LogInfo> ShellExecute(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Trace.Assert(cmd.Info.GetType() == typeof(CodeInfo_ShellExecute));
            CodeInfo_ShellExecute info = cmd.Info as CodeInfo_ShellExecute;

            string verb = StringEscaper.Preprocess(s, info.Action);
            string filePath = StringEscaper.Preprocess(s, info.FilePath);

            StringBuilder b = new StringBuilder(filePath);

            Process proc = new Process();
            proc.StartInfo.FileName = filePath;

            if (info.Params != null)
            {
                string parameters = StringEscaper.Preprocess(s, info.Params);
                proc.StartInfo.Arguments = parameters;
                b.Append(" ");
                b.Append(parameters);
            }

            if (info.WorkDir != null)
            {
                string workDir = StringEscaper.Preprocess(s, info.WorkDir);
                proc.StartInfo.WorkingDirectory = workDir;
            }

            if (string.Equals(verb, "Open", StringComparison.OrdinalIgnoreCase))
            {
                proc.StartInfo.UseShellExecute = true;
                proc.StartInfo.Verb = "Open";
            }
            else if (string.Equals(verb, "Hide", StringComparison.OrdinalIgnoreCase))
            {
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.Verb = "Open";
                proc.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.RedirectStandardError = true;
            }
            else
                proc.StartInfo.Verb = verb;
            proc.Start();

            switch (cmd.Type)
            {
                case CodeType.ShellExecute:
                    proc.WaitForExit();
                    logs.Add(new LogInfo(LogState.Success, $"Executed [{b}], returned exit code [{proc.ExitCode}]"));
                    break;
                case CodeType.ShellExecuteEx:
                    logs.Add(new LogInfo(LogState.Success, $"Executed [{b}]"));
                    break;
                case CodeType.ShellExecuteDelete:
                    proc.WaitForExit();
                    File.Delete(filePath);
                    logs.Add(new LogInfo(LogState.Success, $"Executed and deleted [{b}], returned exit code [{proc.ExitCode}]"));
                    break;
                default:
                    throw new InternalException($"Internal Error! Invalid CodeType [{cmd.Type}]. Please report to issue tracker.");
            }

            if (cmd.Type != CodeType.ShellExecuteEx && info.ExitOutVar != null)
            {
                string exitOutVar = Variables.GetVariableName(s, info.ExitOutVar);
                LogInfo log = Variables.SetVariable(s, info.ExitOutVar, proc.ExitCode.ToString()).First();
                
                if (log.State == LogState.Success)
                    logs.Add(new LogInfo(LogState.Success, $"Exit code [{proc.ExitCode}] saved into variable [%{info.ExitOutVar}%]"));
                else if (log.State == LogState.Error)
                    logs.Add(log);
                else
                    throw new InternalException($"Internal Error! Invalid LogType [{log.State}]. Please report to issue tracker.");
            }

            return logs;
        }
    }
}
