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
        Success, Warning, Error, Info, Ignore, Muted
    }

    public class LogInfo
    {
        private BakeryCommand command;
        private BakerySubCommand subCommand;
        private string result;
        private int depth;
        
        public LogState State;

        public BakeryCommand Command { get { return command; } }
        public BakerySubCommand SubCommand { get { return subCommand;  } }
        public string Result { get { return result; } }
        public int Depth { get { return depth; } }
        

        /// <summary>
        /// Forge an log info
        /// </summary>
        /// <param name="command"></param>
        /// <param name="result"></param>
        /// <param name="state"></param>
        public LogInfo(BakeryCommand command, LogState state, string result)
        {
            this.command = command;
            this.subCommand = null;
            this.result = result;
            this.State = state;
            this.depth = command.SectionDepth;
        }

        public LogInfo(BakeryCommand command, LogState state, string result, bool errorOff)
        {
            this.command = command;
            this.subCommand = null;
            this.result = result;
            this.State = state;
            this.depth = command.SectionDepth;
        }

        public LogInfo(BakeryCommand command, BakerySubCommand subCommand, LogState state, string result)
        {
            this.command = command;
            this.subCommand = subCommand;
            this.result = result;
            this.State = state;
            this.depth = command.SectionDepth;
        }

        public LogInfo(BakeryCommand command, BakerySubCommand subCommand, LogState state, string result, bool errorOff)
        {
            this.command = command;
            this.subCommand = subCommand;
            this.result = result;
            this.State = state;
            this.depth = command.SectionDepth;
        }


        public LogInfo(LogState state, string result)
        {
            this.command = null;
            this.subCommand = null;
            this.result = result;
            this.State = state;
            this.depth = -1;
        }

        public LogInfo(LogState state, string result, int depth)
        {
            this.command = null;
            this.subCommand = null;
            this.result = result;
            this.State = state;
            this.depth = depth;
        }

        public LogInfo(LogState state, string result, int depth, bool errorOff)
        {
            this.command = null;
            this.subCommand = null;
            this.result = result;
            this.State = state;
            this.depth = depth;
        }
    }

    public class Logger
    {
        /// <summary>
        /// Fields
        /// </summary>
        private string logFile;
        private LogFormat logFormat;
        private StreamWriter writer;
        public uint ErrorOffCount;
        public bool SuspendLog;

        public string LogFile { get { return logFile; } }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logFileName"></param>
        /// <param name="logFormat"></param>
        public Logger(string logFile, LogFormat logFormat)
        {
            try
            {
                this.logFile = logFile;
                this.logFormat = logFormat;
                this.ErrorOffCount = 0;
                this.SuspendLog = false;

                Encoding encoding;
                if (logFormat == LogFormat.Text)
                    encoding = Encoding.UTF8; // With BOM, for txt
                else
                    encoding = new UTF8Encoding(false); // Without BOM, for HTML
                this.writer = new StreamWriter(new FileStream(this.logFile, FileMode.Create, FileAccess.Write), encoding); // With BOM, for txt

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

        public void Flush()
        {
            writer.Flush();
        }

        private void PrintBanner()
        {
            PEBakeryInfo info = new PEBakeryInfo();
            InternalWriter($"PEBakery-Engine r{info.Ver.Build} (v{info.Ver.ToString()}) Alpha Log\n", false);
        }

        public void WriteGlobalVariables(BakeryVariables vars)
        {
            StringBuilder builder = new StringBuilder();

            builder.Append("[Global Variables]\n");
            foreach (var global in vars.GlobalVars)
            {
                builder.Append($"{global.Key} (Raw)   = {global.Value}\n");
                builder.Append($"{global.Key} (Value) = {vars.GetValue(VarsType.Global, global.Key)}\n");
            }
            InternalWriter(builder.ToString(), false);
        }

        public void Write(LogInfo log)
        {
            InternalWriter(log, false, false);
        }

        public void Write(LogInfo log, bool errorOff)
        {
            InternalWriter(log, errorOff, true);
        }

        public void Write(LogInfo[] logs)
        {
            InternalWriter(logs, false);
        }

        public void Write(LogInfo[] logs, bool errorOff)
        {
            InternalWriter(logs, errorOff);
        }

        public void Write(string log)
        {
            InternalWriter(log, false);
        }

        public void Write(string log, bool errorOff)
        {
            InternalWriter(log, errorOff);
        }

        private void InternalWriter(string log, bool errorOff)
        {
            if (SuspendLog == true)
                return;
            writer.WriteLine(log);
            if (errorOff && 0 < ErrorOffCount)
                ErrorOffCount -= 1;
#if DEBUG
            writer.Flush();
#endif
        }

        private void InternalWriter(LogInfo[] logs, bool errorOff)
        {
            if (SuspendLog == true)
                return;

            foreach (LogInfo log in logs)
            {
                if (errorOff && 0 < ErrorOffCount)
                {
                    if (log.State == LogState.Error)
                        log.State = LogState.Muted;
                }
                InternalWriter(log, errorOff, false);
            }

            if (errorOff && 0 < ErrorOffCount)
                ErrorOffCount -= 1;
        }

        private void InternalWriter(LogInfo log, bool errorOff, bool decErrorOffCount)
        {
            if (SuspendLog == true)
                return;
            if (errorOff && 0 < ErrorOffCount)
            {
                if (log.State == LogState.Error)
                    log.State = LogState.Muted;
            }

            if (log == null || log.State == LogState.None) // null means do not log
                return;

            for (int i = 0; i <= log.Depth; i++)
                writer.Write("  ");

            if (log.Command != null)
            { // BakeryCommand exists
                if (log.Command.Opcode == Opcode.None)
                    writer.WriteLine($"[{log.State.ToString()}] {log.Result} ({log.Command.RawCode})");
                else
                    writer.WriteLine($"[{log.State.ToString()}] {log.Command.Opcode.ToString()} - {log.Result} ({log.Command.RawCode})");
            }
            else 
            { // No BakeryCommand
                writer.WriteLine($"[{log.State.ToString()}] {log.Result}");
            }
            
            if (errorOff && decErrorOffCount && 0 < ErrorOffCount)
                ErrorOffCount -= 1;
#if DEBUG
            writer.Flush();
#endif
        }

        public void Close()
        {
            try
            {
                writer.Close();
            }
            catch (ObjectDisposedException)
            {
                // StreamWriter already disposed, so pass.
            }
        }
    }
}
