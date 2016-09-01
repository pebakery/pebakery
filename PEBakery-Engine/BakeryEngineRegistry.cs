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
    using VariableDictionary = Dictionary<string, string>;

    /// <summary>
    /// Exception used in BakerOperations
    /// </summary>
    public class ileDoesNotExistException : Exception
    {
        public ileDoesNotExistException() { }
        public ileDoesNotExistException(string message) : base(message) { }
        public ileDoesNotExistException(string message, Exception inner) : base(message, inner) { }
    }

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
        /// Load HiveFile into local machine's key
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public LogInfo RegHiveLoad(BakeryCommand cmd)
        { // RegHiveLoad,<KeyName>,<HiveFileName>
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
                throw new FileDoesNotExistException(hiveFileName + " does not exists");
            }

            int ret = RegLoadKey(HKLM, keyName, hiveFileName);
            if (ret != ResultWin32.ERROR_SUCCESS)
            {
                logResult = string.Format("RegLoadKey API returned error {0}:{1}", ret, ResultWin32.GetErrorName(ret));
                resState = LogState.Error;
            }

            if (logResult == string.Empty)
                logResult = "Loaded " + hiveFileName + " info HKLM\\" + keyName;
            return new LogInfo(cmd, logResult, resState);
        }

        /// <summary>
        /// Unload HiveFile from local machine
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public LogInfo RegHiveUnload(BakeryCommand cmd)
        { // RegHiveUnload,<KeyName>
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
                throw new FileDoesNotExistException(hiveFileName + " does not exists");
            }

            int ret = RegLoadKey(HKLM, keyName, hiveFileName);
            if (ret != ResultWin32.ERROR_SUCCESS)
            {
                logResult = string.Format("RegLoadKey API returned {0}:{1}", ret, ResultWin32.GetErrorName(ret));
                resState = LogState.Error;
            }

            return new LogInfo(cmd, "TXTAddLine", resState);
        }
    }
}