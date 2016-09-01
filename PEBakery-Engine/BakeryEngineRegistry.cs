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
        /// RegHiveLoad,<KeyName>,<HiveFileName>
        /// 
        /// Load HiveFile into local machine's key
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        private LogInfo[] RegHiveLoad(BakeryCommand cmd)
        {
            ArrayList logs = new ArrayList();

            // Must-have operand : 2
            if (cmd.Operands.Length < 2)
                throw new InvalidOperandException("Not enough operand");
            else if (2 < cmd.Operands.Length)
                throw new InvalidOperandException("Too many operands");

            string keyName = cmd.Operands[0];
            string hiveFileName = cmd.Operands[1];

            if (!File.Exists(hiveFileName))
            {
                throw new FileNotFoundException(hiveFileName + " does not exists");
            }

            int ret = RegLoadKey(HKLM, keyName, hiveFileName);
            if (ret == ResultWin32.ERROR_SUCCESS)
            {
                logs.Add(new LogInfo(cmd, string.Concat("Loaded ", hiveFileName, " into HKLM\\", keyName), LogState.Success));
            }
            else
            {
                logs.Add(new LogInfo(cmd, string.Concat("RegLoadKey API returned error : ", ret, " (", ResultWin32.GetErrorName(ret), ")"), LogState.Error));
            }

            return logs.ToArray(typeof(LogInfo)) as LogInfo[];
        }

        /// <summary>
        /// RegHiveUnload,<KeyName>
        /// 
        /// Unload HiveFile from local machine
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        private LogInfo[] RegHiveUnload(BakeryCommand cmd)
        {
            ArrayList logs = new ArrayList();

            string logResult = string.Empty;
            LogState resState = LogState.Success;

            // Must-have operand : 2
            if (cmd.Operands.Length < 2)
                throw new InvalidOperandException("Not enough operand");
            else if (2 < cmd.Operands.Length)
                throw new InvalidOperandException("Too many operands");

            string keyName = cmd.Operands[0];
            string hiveFileName = cmd.Operands[1];

            if (!File.Exists(hiveFileName))
            {
                throw new FileNotFoundException(hiveFileName + " does not exists");
            }

            int ret = RegLoadKey(HKLM, keyName, hiveFileName);
            if (ret != ResultWin32.ERROR_SUCCESS)
            {
                logResult = string.Format("RegLoadKey API returned {0}:{1}", ret, ResultWin32.GetErrorName(ret));
                resState = LogState.Error;
            }

            logs.Add(new LogInfo(cmd, logResult, resState));
            return logs.ToArray(typeof(LogInfo)) as LogInfo[];
        }
    }
}