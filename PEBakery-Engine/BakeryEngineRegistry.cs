using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Win32.Interop;

namespace BakeryEngine
{
    /// <summary>
    /// Implementation of commands
    /// </summary>
    public partial class BakeryEngine
    {
        const UInt32 HKLM = 0x80000002;

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern Int32 RegLoadKey(UInt32 hKey, string lpSubKey, string lpFile);
        [DllImport("advapi32.dll", SetLastError = true)]
        static extern Int32 RegUnLoadKey(UInt32 hKey, string lpSubKey);

        /* 
         * Registry Commands
         */

        /// <summary>
        /// RegHiveLoad,<KeyName>,<HiveFile>
        /// </summary>
        /// <remarks>
        /// Load Hive into local machine
        /// </remarks>
        /// <param name="cmd"></param>
        /// <returns></returns>
        private LogInfo[] RegHiveLoad(BakeryCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 2, optional operand : 0
            const int necessaryOperandNum = 2;
            const int optionalOperandNum = 0;
            if (cmd.Operands.Length < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);
            else if (necessaryOperandNum + optionalOperandNum < cmd.Operands.Length)
                throw new InvalidOperandException("Too many operands", cmd);

            string keyName = EscapeString(variables.Expand(cmd.Operands[0]));
            string hiveFile = EscapeString(variables.Expand(cmd.Operands[1]));
            string rawHiveFile = cmd.Operands[1];

            if (!File.Exists(hiveFile))
            {
                throw new FileNotFoundException($"[{keyName}] does not exists");
            }

            int ret = RegLoadKey(HKLM, keyName, hiveFile);
            if (ret == ResultWin32.ERROR_SUCCESS)
                logs.Add(new LogInfo(cmd, LogState.Success, $"Loaded [{rawHiveFile}] into [HKLM\\{keyName}]"));
            else
                logs.Add(new LogInfo(cmd, LogState.Error, $"RegLoadKey API returned error = [{ret}, {ResultWin32.GetErrorName(ret)}]"));

            return logs.ToArray();
        }

        /// <summary>
        /// RegHiveUnload,<KeyName>
        /// </summary>
        /// <remarks>
        /// Unload Hive from local machine
        /// </remarks>
        /// <param name="cmd"></param>
        /// <returns></returns>
        private LogInfo[] RegHiveUnload(BakeryCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            string logResult = string.Empty;

            // Necessary operand : 1, optional operand : 0
            const int necessaryOperandNum = 1;
            const int optionalOperandNum = 0;
            if (cmd.Operands.Length < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);
            else if (necessaryOperandNum + optionalOperandNum < cmd.Operands.Length)
                throw new InvalidOperandException("Too many operands", cmd);

            string keyName = EscapeString(variables.Expand(cmd.Operands[0]));
            string rawKeyName = cmd.Operands[0];

            if (!File.Exists(keyName))
            {
                throw new FileNotFoundException($"[{keyName}] does not exists");
            }

            int ret = RegUnLoadKey(HKLM, keyName);
            if (ret == ResultWin32.ERROR_SUCCESS)
                logs.Add(new LogInfo(cmd, LogState.Success, $@"Unloaded [HKLM\{rawKeyName}]"));
            else
                logs.Add(new LogInfo(cmd, LogState.Error, $"RegUnloadKey API returned error = [{ret}, {ResultWin32.GetErrorName(ret)}]"));
            return logs.ToArray();
        }
    }
}