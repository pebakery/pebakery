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
        Success, Warning, Error, Infomation
    }

    public class LogInfo
    {
        private string rawCommand;
        public string RawCommand
        {
            get { return rawCommand; }
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

        public LogInfo(string rawCommand, string result, LogState state)
        {
            this.rawCommand = rawCommand;
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
        private FileStream fs;
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

                fs = new FileStream(this.logFileName, FileMode.Create, FileAccess.Write, FileShare.Write);
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


        public void Write(LogInfo logInfo)
        {
            string log = String.Format("[{0}] {1} ({2})", logInfo.State.ToString(), logInfo.Result, logInfo.RawCommand);
            sw.Write(log);
        }

        public void Close()
        {
            sw.Close();
            fs.Close();
        }
    }
}
