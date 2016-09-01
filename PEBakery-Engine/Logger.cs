using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;

namespace BakeryEngine
{
    public enum LogFormat
    {
        Text,
        HTML
    }

    public enum LogState
    {
        None = 0,
        Success, Warning, Error, Infomation, Ignore
    }

    public class LogInfo
    {
        private BakeryCommand command;
        public BakeryCommand Command
        {
            get { return command; }
        }
        private string result;
        public string Result
        {
            get { return result; }
        }
        private LogState state;
        public LogState State
        {
            get { return state;  }
        }

        /// <summary>
        /// Forge an log info
        /// </summary>
        /// <param name="command"></param>
        /// <param name="result"></param>
        /// <param name="state"></param>
        public LogInfo(BakeryCommand command, string result, LogState state)
        {
            this.command = command;
            this.result = result;
            this.state = state;
        }
    }

    public class Logger
    {
        /// <summary>
        /// Fields
        /// </summary>
        private string logFileName;
        private LogFormat logFormat;
        private StreamWriter sw;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logFileName"></param>
        /// <param name="logFormat"></param>
        public Logger(string logFileName, LogFormat logFormat)
        {
            try
            {
                this.logFileName = logFileName;
                this.logFormat = logFormat;

                FileStream fs = new FileStream(this.logFileName, FileMode.Create, FileAccess.Write, FileShare.Write);
                if (logFormat == LogFormat.Text)
                    sw = new StreamWriter(fs, Encoding.UTF8); // With BOM, for txt
                else
                    sw = new StreamWriter(fs, new UTF8Encoding(false)); // Without BOM, for HTML

                PrintBanner();
            }
            catch (Exception e)
            {
                Console.WriteLine("[ERR]\n{0}", e.ToString());
            }
        }

        ~Logger()
        {
            Close();
        }

        private void PrintBanner()
        {
            PEBakeryInfo info = new PEBakeryInfo();
            sw.WriteLine(string.Concat("PEBakery-Engine r", info.Ver.Build, " (v", info.Ver.ToString(), ") Alpha Log"));
            sw.Flush();
        }

        public void Write(LogInfo logInfo)
        {
            if (logInfo == null || logInfo.State == LogState.None) // null means do not log
                return;
            for (int i = 0; i < logInfo.Command.SectionDepth; i++)
                sw.Write("  ");
            if (logInfo.Command.Opcode == Opcode.None)
                sw.WriteLine(string.Format("[{0}] {1} ({2})", logInfo.State.ToString(), logInfo.Result, logInfo.Command.RawCode));
            else
                sw.WriteLine(string.Format("[{0}] {1} - {2} ({3})", logInfo.State.ToString(), logInfo.Command.Opcode.ToString(), logInfo.Result, logInfo.Command.RawCode));
            sw.Flush();
        }

        public void Write(LogInfo[] logInfos)
        {
            foreach (LogInfo logInfo in logInfos)
                Write(logInfo);
        }

        public void Write(string log)
        {
            sw.WriteLine(log);
            sw.Flush();
        }

        public void WriteVariables(BakeryVariables vars)
        {
            string str = "\n\n[Local Variables]\n";
            foreach (var local in vars.LocalValue)
            {
                str = string.Concat(str, local.Key, " (Raw)   = ", vars.LocalRaw[local.Key], "\n");
                str = string.Concat(str, local.Key, " (Value) = ", local.Value, "\n");
            }
            str += "\n[Global Variables]\n";
            foreach (var global in vars.GlobalValue)
            {
                str = string.Concat(str, global.Key, " (Raw)   = ", vars.GlobalRaw[global.Key], "\n");
                str = string.Concat(str, global.Key, " (Value) = ", global.Value, "\n");
            }
            sw.Write(str);
            sw.Flush();
        }

        

        public void Close()
        {
            try
            {
                sw.Close();
            }
            catch (ObjectDisposedException)
            {
                // StreamWriter already disposed, so pass.
            }
        }
    }
}
