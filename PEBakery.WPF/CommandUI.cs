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
                throw new InvalidCodeCommandException("Command [Message] should have [CodeInfo_Message]", cmd);

            string message = StringEscaper.Preprocess(s, info.Message);

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
                    throw new InternalUnknownException("CodeInfo_Message's CodeMessageAction is invalid");
            }

            // TODO : Timeout support
            if (info.Timeout != null)
            {
                string timeOutStr = StringEscaper.Preprocess(s, info.Timeout);
                if (int.TryParse(timeOutStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int timeOut) == false)
                    throw new InvalidCodeCommandException($"[{timeOutStr}] is not valid positive integer", cmd);
                if (timeOut <= 0)
                    throw new InvalidCodeCommandException($"Timeout must be positive integer [{timeOutStr}]", cmd);

                logs.Add(new LogInfo(LogState.Warning, $"Timeout of Message is not implemented yet", cmd));
            }

            MessageBox.Show(message, cmd.Addr.Plugin.Title, MessageBoxButton.OK, image);
            logs.Add(new LogInfo(LogState.Success, $"MessageBox [{message}]", cmd));

            return logs;
        }

        public static List<LogInfo> Echo(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_Echo info = cmd.Info as CodeInfo_Echo;
            if (info == null)
                throw new InvalidCodeCommandException("Command [Echo] should have [CodeInfo_Echo]", cmd);

            string message = StringEscaper.Preprocess(s, info.Message);

            // TODO
            logs.Add(new LogInfo(LogState.Warning, $"Echo is not implemented yet", cmd));

            logs.Add(new LogInfo(LogState.Success, $"Displayed [{message}]", cmd));

            return logs;
        }
    }
}
