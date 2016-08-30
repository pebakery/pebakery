using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace PEBakery_Engine
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
        private string rawCode;
        public string RawCode
        {
            get { return rawCode; }
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

        public LogInfo(string rawCode, string result, LogState state)
        {
            this.rawCode = rawCode;
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
            }
            catch (Exception e)
            {
                Console.WriteLine("[ERR]\n{0}", e.ToString());
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logFileName"></param>
        /// <param name="logFormat"></param>
        public Logger(string logFileName, LogFormat logFormat, PEBakeryInfo info)
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

                PrintBanner(info);
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

        private void PrintBanner(PEBakeryInfo info)
        {
            sw.WriteLine("PEBakery " + info.Ver.ToString());
            sw.Flush();
        }

        public void Write(LogInfo logInfo)
        {
            if (logInfo == null || logInfo.State == LogState.None) // null means do not log
                return;
            sw.WriteLine(String.Format("[{0}] {1} ({2})", logInfo.State.ToString(), logInfo.Result, logInfo.RawCode));
            sw.Flush();
        }

        public void Write(string log)
        {
            sw.WriteLine(log);
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
