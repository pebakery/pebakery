using PEBakery.Exceptions;
using PEBakery.WPF.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;

namespace PEBakery.Core.Commands
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

            if (info.Timeout == null)
            {
                MessageBox.Show(message, cmd.Addr.Plugin.Title, MessageBoxButton.OK, image);
            }
            else
            {
                string timeoutStr = StringEscaper.Preprocess(s, info.Timeout);
                if (int.TryParse(timeoutStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int timeout) == false)
                    throw new ExecuteException($"[{timeoutStr}] is not valid positive integer");
                if (timeout <= 0)
                    throw new ExecuteException($"Timeout must be positive integer [{timeoutStr}]");

                Application.Current.Dispatcher.Invoke(() =>
                {
                    CustomMessageBox.Show(message, cmd.Addr.Plugin.Title, MessageBoxButton.OK, image, timeout);
                });
            }

            string[] slices = message.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
            string firstLine = message;
            if (0 < slices.Length)
                firstLine = slices[0];
            logs.Add(new LogInfo(LogState.Success, $"MessageBox [{firstLine}]", cmd));

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
