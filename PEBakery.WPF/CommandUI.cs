using PEBakery.Exceptions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PEBakery.Core
{
    public static class CommandUI
    {
        public static List<LogInfo> Message(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_Message info = cmd.Info as CodeInfo_Message;
            if (info == null)
                throw new InternalCodeInfoException();

            string message = StringEscaper.Preprocess(s, info.Message);

            // TODO : Timeout support
            if (info.Timeout == null)
            {
                MessageBoxImage image;
                switch (info.Action)
                {
                    case CodeMessageAction.None:
                    case CodeMessageAction.Information:
                        image = MessageBoxImage.Information;
                        break;
                    case CodeMessageAction.Confirmation:
                        image = MessageBoxImage.Question;
                        break;
                    case CodeMessageAction.Error:
                        image = MessageBoxImage.Error;
                        break;
                    case CodeMessageAction.Warning:
                        image = MessageBoxImage.Warning;
                        break;
                    default:
                        throw new InternalErrorException("CodeInfo_Message's CodeMessageAction is invalid");
                }

                MessageBox.Show(message, cmd.Addr.Plugin.Title, MessageBoxButton.OK, image);
                logs.Add(new LogInfo(LogState.Success, $"MessageBox [{message}]", cmd));
            }
            else
            {
                System.Windows.Forms.MessageBoxIcon icon;
                switch (info.Action)
                {
                    case CodeMessageAction.None:
                    case CodeMessageAction.Information:
                        icon = System.Windows.Forms.MessageBoxIcon.Information;
                        break;
                    case CodeMessageAction.Confirmation:
                        icon = System.Windows.Forms.MessageBoxIcon.Question;
                        break;
                    case CodeMessageAction.Error:
                        icon = System.Windows.Forms.MessageBoxIcon.Error;
                        break;
                    case CodeMessageAction.Warning:
                        icon = System.Windows.Forms.MessageBoxIcon.Warning;
                        break;
                    default:
                        throw new InternalErrorException("CodeInfo_Message's CodeMessageAction is invalid");
                }

                string timeOutStr = StringEscaper.Preprocess(s, info.Timeout);
                if (int.TryParse(timeOutStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int timeOut) == false)
                    throw new ExecuteErrorException($"[{timeOutStr}] is not valid positive integer");
                if (timeOut <= 0)
                    throw new ExecuteErrorException($"Timeout must be positive integer [{timeOutStr}]");

                logs.Add(new LogInfo(LogState.Warning, $"Timeout of Message is not implemented yet", cmd));

                System.Windows.Forms.Form form = new System.Windows.Forms.Form()
                {
                    Size = new System.Drawing.Size(0, 0),
                };
                Task.Delay(TimeSpan.FromSeconds(timeOut)).ContinueWith((t) => form.Close(), TaskScheduler.FromCurrentSynchronizationContext());

                System.Windows.Forms.MessageBox.Show(form, message, $"Will close after {timeOut} seconds", System.Windows.Forms.MessageBoxButtons.OK, icon);
            }
            
            return logs;
        }

        public static List<LogInfo> Echo(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_Echo info = cmd.Info as CodeInfo_Echo;
            if (info == null)
                throw new InternalCodeInfoException();

            string message = StringEscaper.Preprocess(s, info.Message);

            // TODO
            logs.Add(new LogInfo(LogState.Warning, $"Echo is not implemented yet", cmd));

            logs.Add(new LogInfo(LogState.Success, $"Displayed [{message}]", cmd));

            return logs;
        }
    }
}
