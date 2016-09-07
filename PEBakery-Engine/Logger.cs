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
        Success, Warning, Error, Information, Ignore
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
            sw.WriteLine($"PEBakery-Engine r{info.Ver.Build} (v{info.Ver.ToString()}) Alpha Log");
#if DEBUG
            sw.Flush();
#endif
        }

        public void Write(LogInfo logInfo)
        {
            if (logInfo == null || logInfo.State == LogState.None) // null means do not log
                return;
            for (int i = 0; i <= logInfo.Command.SectionDepth; i++)
                sw.Write("  ");
            if (logInfo.Command.Opcode == Opcode.None)
                sw.WriteLine($"[{logInfo.State.ToString()}] {logInfo.Result} ({logInfo.Command.RawCode})");
            else
                sw.WriteLine($"[{logInfo.State.ToString()}] {logInfo.Command.Opcode.ToString()} - {logInfo.Result} ({logInfo.Command.RawCode})");

#if DEBUG
            sw.Flush();
#endif
        }

        public void Write(LogInfo[] logInfos)
        {
            foreach (LogInfo logInfo in logInfos)
                Write(logInfo);
        }

        public void Write(string log)
        {
            sw.WriteLine(log);
#if DEBUG
            sw.Flush();
#endif
        }

        public void Write(LogState state, string log)
        {
            sw.WriteLine($"[{state.ToString()}] {log}");
#if DEBUG
            sw.Flush();
#endif
        }

        public void Write(LogState state, string log, int depth)
        {
            for (int i = 0; i <= depth; i++)
                sw.Write("  ");
            sw.WriteLine($"[{state.ToString()}] {log}");
#if DEBUG
            sw.Flush();
#endif
        }

        public void WriteVariables(BakeryVariables vars)
        {
            StringBuilder builder = new StringBuilder();

            builder.Append("[Global Variables]\n");
            foreach (var global in vars.GlobalVars)
            {
                builder.Append($"{global.Key} (Raw)   = {global.Value}\n");
                builder.Append($"{global.Key} (Value) = {vars.GetValue(VarsType.Global, global.Key)}\n");
            }
            sw.Write(builder.ToString());
#if DEBUG
            sw.Flush();
#endif
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
