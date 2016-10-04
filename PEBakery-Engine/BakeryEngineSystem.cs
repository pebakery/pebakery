using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections;
using System.Windows.Forms;
using System.Security.Principal;
using System.Diagnostics;

namespace BakeryEngine
{
    public enum SystemSubOpcode
    {
        None = 0,
        Cursor,
        ErrorOff, Log, SaveLog,
        GetFreeDrive, GetFreeSpace,
        GetEnv, IsAdmin, 
        OnBuildExit, OnScriptExit, OnPluginExit,
        RefreshInterface, RescanScript,
        ShellExecute,
        // Deprecated
        Comp80, FileRedirect, HasUAC, IsTerminal, RebuildVars, RegRedirect, SplitParameters
    }

    /// <summary>
    /// Class to hold info of commands
    /// </summary>
    public class BakerySystemCommand
    {
        public SystemSubOpcode SubOpcode;
        public List<string> Operands;

        public BakerySystemCommand(SystemSubOpcode subOpcode, List<string> operands)
        {
            this.SubOpcode = subOpcode;
            this.Operands = operands;
        }
    }

    public partial class BakeryEngine
    {
        /*
         * System with sub commands
         */

        /// <summary>
        /// System,<SubOpcode>
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public List<LogInfo> SystemCommands(BakeryCommand cmd)
        {
            List<LogInfo> logs;

            // Necessary operand : 1, optional operand : 0+
            const int necessaryOperandNum = 1;
            if (cmd.Operands.Count < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);

            string subOpcodeString = cmd.Operands[0];
            SystemSubOpcode subOpcode = SystemSubOpcode.None;
            BakerySystemCommand subCmd;

            // Check if rawCode is Empty
            if (string.Equals(subOpcodeString, string.Empty))
                throw new InvalidSubOpcodeException($"Invalid sub command [System.{subOpcodeString}]", cmd);

            // Parse opcode
            try
            {
                subOpcode = (SystemSubOpcode)Enum.Parse(typeof(SystemSubOpcode), subOpcodeString, true);
                if (Enum.IsDefined(typeof(SystemSubOpcode), subOpcode) && subOpcode != SystemSubOpcode.None)
                    subCmd = new BakerySystemCommand(subOpcode, cmd.Operands.Skip(1).ToList());
                else
                    throw new ArgumentException();
            }
            catch (ArgumentException)
            {
                throw new InvalidSubOpcodeException($"Invalid sub command [System.{subOpcodeString}]", cmd);
            }

            // Call sub command methods
            switch (subOpcode)
            {
                case SystemSubOpcode.Cursor:
                    logs = SystemCursor(cmd, subCmd);
                    break;
                case SystemSubOpcode.ErrorOff:
                    logs = SystemErrorOff(cmd, subCmd);
                    break;
                case SystemSubOpcode.Log:
                    logs = SystemLog(cmd, subCmd);
                    break;
                case SystemSubOpcode.SaveLog:
                    logs = SystemSaveLog(cmd, subCmd);
                    break;
                case SystemSubOpcode.GetEnv:
                    logs = SystemGetEnv(cmd, subCmd);
                    break;
                case SystemSubOpcode.GetFreeDrive:
                    logs = SystemGetFreeDrive(cmd, subCmd);
                    break;
                case SystemSubOpcode.GetFreeSpace:
                    logs = SystemGetFreeSpace(cmd, subCmd);
                    break;
                case SystemSubOpcode.IsAdmin:
                    logs = SystemIsAdmin(cmd, subCmd);
                    break;
                case SystemSubOpcode.OnBuildExit:
                    logs = SystemOnBuildExit(cmd, subCmd);
                    break;
                case SystemSubOpcode.OnScriptExit:
                case SystemSubOpcode.OnPluginExit:
                    logs = SystemOnPluginExit(cmd, subCmd);
                    break;
                default: // In fact, Enum.Parse logic must handle this. If this logic is called, it is definitely a BUG
                    throw new InvalidSubOpcodeException($"INTERNAL ERROR! Invalid sub command [System.{subOpcodeString}]", cmd);
            }
 
            return logs;
        }

        /// <summary>
        /// System,Cursor,<Wait|Normal>
        /// </summary>
        /// <param name="cmd">BakeryCommand</param>
        /// /// <param name="subCmd">BakerySystemCommand</param>
        /// <returns></returns>
        public List<LogInfo> SystemCursor(BakeryCommand cmd, BakerySystemCommand subCmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 1, optional operand : 0
            const int necessaryOperandNum = 1;
            const int optionalOperandNum = 0;

            if (subCmd.Operands.Count < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);
            else if (necessaryOperandNum + optionalOperandNum < subCmd.Operands.Count)
                throw new InvalidOperandException("Too many operands", cmd);

            string action = subCmd.Operands[0];

            if (string.Equals(action, "Wait", StringComparison.OrdinalIgnoreCase))
            {
                Cursor.Current = Cursors.WaitCursor;
                logs.Add(new LogInfo(cmd, LogState.Success, "Set mouse cursor to [Wait]"));
            }
            else if (string.Equals(action, "Normal", StringComparison.OrdinalIgnoreCase))
            {                
                Cursor.Current = Cursors.Default;
                logs.Add(new LogInfo(cmd, LogState.Success, "Set mouse cursor to [Normal]"));
            }
            else
            {
                throw new InvalidOperandException($"Invalid operand [{action}]", cmd);
            }

            return logs;
        }

        /// <summary>
        /// System,ErrorOff[,Lines] 
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="subCmd"></param>
        /// <returns></returns>
        public List<LogInfo> SystemErrorOff(BakeryCommand cmd, BakerySystemCommand subCmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 0, optional operand : 1
            const int necessaryOperandNum = 0;
            const int optionalOperandNum = 1;

            if (subCmd.Operands.Count < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);
            else if (necessaryOperandNum + optionalOperandNum < subCmd.Operands.Count)
                throw new InvalidOperandException("Too many operands", cmd);

            uint lines = 1; // must be (0 < lines)
            if (subCmd.Operands.Count == necessaryOperandNum + optionalOperandNum)
            {
                if (NumberHelper.ParseUInt32(subCmd.Operands[0], out lines) == false)
                    throw new InvalidOperandException($"[{subCmd.Operands[0]}] is not valid number", cmd);
                if (lines <= 0)
                    throw new InvalidOperandException($"[{subCmd.Operands[0]}] must be greater than 0", cmd);
            }

            logger.ErrorOffCount = lines + 1; // 1 is for ErrorOff itself
            logs.Add(new LogInfo(cmd, LogState.Success, $"Error log is off for [{lines}] lines"));

            return logs;
        }

        /// <summary>
        /// System,Log,<On|Off>
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="subCmd"></param>
        /// <returns></returns>
        public List<LogInfo> SystemLog(BakeryCommand cmd, BakerySystemCommand subCmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 1, optional operand : 0
            const int necessaryOperandNum = 1;
            const int optionalOperandNum = 0;

            if (subCmd.Operands.Count < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);
            else if (necessaryOperandNum + optionalOperandNum < subCmd.Operands.Count)
                throw new InvalidOperandException("Too many operands", cmd);

            string action = subCmd.Operands[0];
            if (string.Equals(action, "On", StringComparison.OrdinalIgnoreCase))
            {
                logger.SuspendLog = true;
                logs.Add(new LogInfo(cmd, LogState.Success, $"Logging turned on for plugin [{cmd.Address.plugin.ShortPath}]"));
            }
            else if (string.Equals(action, "Off", StringComparison.OrdinalIgnoreCase))
            {
                logger.SuspendLog = false;
                logs.Add(new LogInfo(cmd, LogState.Success, $"Logging turned off for plugin [{cmd.Address.plugin.ShortPath}]"));
            }
            else
            {
                throw new InvalidOperandException($"Invalid operand [{action}]", cmd);
            }

            return logs;
        }

        /// <summary>
        /// System,SaveLog,<File>
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="subCmd"></param>
        /// <returns></returns>
        public List<LogInfo> SystemSaveLog(BakeryCommand cmd, BakerySystemCommand subCmd)
        {
            // Necessary operand : 1, optional operand : 0
            const int necessaryOperandNum = 1;
            const int optionalOperandNum = 0;

            if (subCmd.Operands.Count < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);
            else if (necessaryOperandNum + optionalOperandNum < subCmd.Operands.Count)
                throw new InvalidOperandException("Too many operands", cmd);

            string dest = UnescapeString(variables.Expand(subCmd.Operands[0]));
            string rawDest = subCmd.Operands[0];
            logger.Flush();
            File.Copy(logger.LogFile, dest, true);

            return new List<LogInfo>();
        }

        /// <summary>
        /// System,GetFreeDrive,<%Variable%>
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="subCmd"></param>
        /// <returns></returns>
        public List<LogInfo> SystemGetFreeDrive(BakeryCommand cmd, BakerySystemCommand subCmd)
        { // Get Free Drive Letters
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 1, optional operand : 0
            const int necessaryOperandNum = 1;
            const int optionalOperandNum = 0;

            if (subCmd.Operands.Count < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);
            else if (necessaryOperandNum + optionalOperandNum < subCmd.Operands.Count)
                throw new InvalidOperandException("Too many operands", cmd);

            string varName = BakeryVariables.TrimPercentMark(subCmd.Operands[0]);

            // Get existing logical drive letters
            DriveInfo[] drives = DriveInfo.GetDrives();
            List<char> driveLetters = new List<char>();
            foreach (DriveInfo drive in drives)
                driveLetters.Add(char.ToUpper(drive.Name[0]));

            char lastFreeDriveLetter = 'Z';
            for (char ch = 'Z'; 'A' <= ch; ch--)
            {
                if (!driveLetters.Contains(ch))
                {
                    lastFreeDriveLetter = ch;
                    break;
                }
            }

            LogInfo log = variables.SetValue(VarsType.Local, varName, lastFreeDriveLetter + ":", cmd.Depth);
            if (log.State == LogState.Success)
                logs.Add(new LogInfo(cmd, LogState.Success, $"Last free drive letter is [{lastFreeDriveLetter}:], saved into variable [%{varName}%]"));
            else if (log.State == LogState.Error)
                logs.Add(log);
            else
                throw new InvalidLogFormatException($"Unknown internal log format error", cmd);

            return logs;
        }

        /// <summary>
        /// System,GetFreeSpace,<Path>,<%Variable%>
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="subCmd"></param>
        /// <returns></returns>
        public List<LogInfo> SystemGetFreeSpace(BakeryCommand cmd, BakerySystemCommand subCmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 2, optional operand : 0
            const int necessaryOperandNum = 2;
            const int optionalOperandNum = 0;

            if (subCmd.Operands.Count < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);
            else if (necessaryOperandNum + optionalOperandNum < subCmd.Operands.Count)
                throw new InvalidOperandException("Too many operands", cmd);

            string path = UnescapeString(variables.Expand(subCmd.Operands[0]));
            string varName = BakeryVariables.TrimPercentMark(subCmd.Operands[1]);
            string driveLetter = Path.GetPathRoot(Path.GetFullPath(path));
            long freeSpace = new DriveInfo(driveLetter).AvailableFreeSpace / (1024 * 1024); // Convert to MB

            LogInfo log = variables.SetValue(VarsType.Local, varName, freeSpace.ToString(), cmd.Depth);
            if (log.State == LogState.Success)
                logs.Add(new LogInfo(cmd, LogState.Success, $"Free Space of Drive [{driveLetter.Substring(0, 1)}:] is [{freeSpace}MB], saved into variable [%{varName}%]"));
            else if (log.State == LogState.Error)
                logs.Add(log);
            else
                throw new InvalidLogFormatException($"Unknown internal log format error", cmd);

            return logs;
        }
         /// <summary>
        /// System,GetEnv,<EnvVar>,%Variable%
        /// </summary>
        /// <remarks><EnvVar> does not enclosed with Percent</remarks>
        /// <param name="cmd"></param>
        /// <param name="subCmd"></param>
        /// <returns></returns>
        public List<LogInfo> SystemGetEnv(BakeryCommand cmd, BakerySystemCommand subCmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 2, optional operand : 0
            const int necessaryOperandNum = 2;
            const int optionalOperandNum = 0;

            if (subCmd.Operands.Count < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);
            else if (necessaryOperandNum + optionalOperandNum < subCmd.Operands.Count)
                throw new InvalidOperandException("Too many operands", cmd);

            string envVarName = UnescapeString(subCmd.Operands[0]);
            string bakeryVarName = BakeryVariables.TrimPercentMark(subCmd.Operands[1]);
            string envVarValue = Environment.GetEnvironmentVariable(envVarName); // return null when envVarName does not exist
            if (envVarValue == null)
                throw new InvalidOperandException($"There is no envrionment variable named [{envVarName}]");

            LogInfo log = variables.SetValue(VarsType.Local, envVarName, envVarValue, cmd.Depth);
            if (log.State == LogState.Success)
                logs.Add(new LogInfo(cmd, LogState.Success, $"Variable [%{bakeryVarName}%] set to envrionment variable [{envVarName}]'s value [{envVarValue}]"));
            else if (log.State == LogState.Error)
                logs.Add(log);
            else
                throw new InvalidLogFormatException($"Unknown internal log format error", cmd);

            return logs;
        }

        /// <summary>
        /// System,IsAdmin,%Varaible%
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="subCmd"></param>
        /// <returns></returns>
        public List<LogInfo> SystemIsAdmin(BakeryCommand cmd, BakerySystemCommand subCmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 1, optional operand : 0
            const int necessaryOperandNum = 1;
            const int optionalOperandNum = 0;

            if (subCmd.Operands.Count < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);
            else if (necessaryOperandNum + optionalOperandNum < subCmd.Operands.Count)
                throw new InvalidOperandException("Too many operands", cmd);

            string varName = BakeryVariables.TrimPercentMark(subCmd.Operands[0]);
            bool isAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
            LogInfo log = variables.SetValue(VarsType.Local, varName, isAdmin.ToString(), cmd.Depth);
            if (log.State == LogState.Error)
                logs.Add(log);
            else
            {
                if (isAdmin)
                    logs.Add(new LogInfo(cmd, LogState.Success, $"PEBakery has admin privileges, variable [%{varName}%] set to [{isAdmin}]"));
                else
                    logs.Add(new LogInfo(cmd, LogState.Success, $"PEBakery does not have admin privileges, variable [%{varName}%] set to [{isAdmin}]"));
            }

            return logs;
        }

        /// <summary>
        /// System,OnBuildExit,<Command>
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="subCmd"></param>
        /// <returns></returns>
        public List<LogInfo> SystemOnBuildExit(BakeryCommand cmd, BakerySystemCommand subCmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 0, optional operand : 0+
            if (subCmd.Operands.Count == 0)
            { // Remove onBuildExit callback
                onBuildExit = null; 
                logs.Add(new LogInfo(cmd, LogState.Success, $"Callback of event [OnBuildExit] removed"));
            }
            else
            { // Register onBuildExit callback
                string externalOpcode;
                Opcode opcode = BakeryCodeParser.ParseOpcode(subCmd.Operands[0], out externalOpcode);
                onBuildExit = new BakeryCommand(cmd.Origin, opcode, subCmd.Operands.Skip(1).ToList()); // Project's last plugin's last address
                logs.Add(new LogInfo(cmd, LogState.Success, $"Callback of event [OnBuildExit] registered"));
            }

            return logs;
        }

        /// <summary>
        /// System,OnPluginExit,<Command>
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="subCmd"></param>
        /// <returns></returns>
        public List<LogInfo> SystemOnPluginExit(BakeryCommand cmd, BakerySystemCommand subCmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 0, optional operand : 0+
            if (subCmd.Operands.Count == 0)
            { // Remove onBuildExit callback
                onPluginExit = null; 
                logs.Add(new LogInfo(cmd, LogState.Success, $"Callback of event [OnPluginExit] removed"));
            }
            else
            { // Register onBuildExit callback
                string externalOpcode;
                Opcode opcode = BakeryCodeParser.ParseOpcode(subCmd.Operands[0], out externalOpcode);
                onPluginExit = new BakeryCommand(cmd.Origin, opcode, subCmd.Operands.Skip(1).ToList()); // Current Plugin's last address
                logs.Add(new LogInfo(cmd, LogState.Success, $"Callback of event [OnPluginExit] registered"));
            }
            
            return logs;
        }

        /// <summary>
        /// ShellExecute,<Action>,<FilePath>[,Params][,WorkDir][,%ExitCode%]
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public List<LogInfo> ShellExecute(BakeryCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 2, optional operand : 3
            const int necessaryOperandNum = 2;
            const int optionalOperandNum = 3;
            if (cmd.Operands.Count < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);
            else if (necessaryOperandNum + optionalOperandNum < cmd.Operands.Count)
                throw new InvalidOperandException("Too many operands", cmd);
            if (cmd.Opcode == Opcode.ShellExecuteEx && cmd.Operands.Count == necessaryOperandNum + optionalOperandNum)
                throw new InvalidOperandException("Too many operands", cmd);

            string verb = UnescapeString(variables.Expand(cmd.Operands[0]));
            if (!(string.Equals(verb, "Open", StringComparison.OrdinalIgnoreCase) || string.Equals(verb, "Hide", StringComparison.OrdinalIgnoreCase)
                || string.Equals(verb, "Print", StringComparison.OrdinalIgnoreCase) || string.Equals(verb, "Explore", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperandException($"Invalid verb [{verb}]", cmd);
            string filePath = UnescapeString(variables.Expand(cmd.Operands[1]));
            string rawFilePath = cmd.Operands[1];
            string parameters = string.Empty;
            if (3 <= cmd.Operands.Count)
                parameters = UnescapeString(variables.Expand(cmd.Operands[2]));
            string workDir = Directory.GetCurrentDirectory();
            if (4 <= cmd.Operands.Count)
                workDir = UnescapeString(variables.Expand(cmd.Operands[3]));
            string exitCodeVar = null;
            if (5 <= cmd.Operands.Count)
                exitCodeVar = BakeryVariables.TrimPercentMark(cmd.Operands[4]);

            Process proc = new Process();
            proc.StartInfo.FileName = filePath;
            proc.StartInfo.Arguments = parameters;
            proc.StartInfo.WorkingDirectory = workDir;
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
            
            switch (cmd.Opcode)
            {
                case Opcode.ShellExecute:
                    proc.WaitForExit();
                    logs.Add(new LogInfo(cmd, LogState.Success, $"Executed [{rawFilePath}] with shell, returned exit code [{proc.ExitCode}]"));
                    if (exitCodeVar != null)
                    {
                        LogInfo log = variables.SetValue(VarsType.Local, exitCodeVar, proc.ExitCode.ToString(), cmd.Depth);
                        if (log.State == LogState.Success)
                            logs.Add(new LogInfo(cmd, LogState.Success, $"Exit code is [{proc.ExitCode}], saved into variable [%{exitCodeVar}%]"));
                        else if (log.State == LogState.Error)
                            logs.Add(log);
                        else
                            throw new InvalidLogFormatException($"Unknown internal log format error", cmd);                            
                    }
                    break;
                case Opcode.ShellExecuteEx:
                    logs.Add(new LogInfo(cmd, LogState.Success, $"Executed [{rawFilePath}] with shell"));
                    break;
                case Opcode.ShellExecuteDelete:
                    proc.WaitForExit();
                    File.Delete(filePath);
                    logs.Add(new LogInfo(cmd, LogState.Success, $"Executed and deleted [{rawFilePath}] with shell, returned exit code [{proc.ExitCode}]"));
                    if (exitCodeVar != null)
                    {
                        LogInfo log = variables.SetValue(VarsType.Local, exitCodeVar, proc.ExitCode.ToString(), cmd.Depth);
                        if (log.State == LogState.Success)
                            logs.Add(new LogInfo(cmd, LogState.Success, $"Exit code is [{proc.ExitCode}], saved into variable [%{exitCodeVar}%]"));
                        else if (log.State == LogState.Error)
                            logs.Add(log);
                        else
                            throw new InvalidLogFormatException($"Unknown internal log format error", cmd);
                    }
                    break;
                default:
                    throw new InvalidOperandException($"Invalid opcode [{cmd.Opcode}]", cmd);
            }
            

            return logs;
        }
    }
}