using PEBakery.Core;
using PEBakery.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PEBakery.WPF;
using System.Windows;
using System.Windows.Threading;

namespace PEBakery.Core.Commands
{
    public static class CommandInterface
    {
        public static List<LogInfo> Visible(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_Visible info = cmd.Info as CodeInfo_Visible;
            if (info == null)
                throw new InternalCodeInfoException();

            string visibilityStr = StringEscaper.Preprocess(s, info.Visibility);
            bool visibility = false;
            if (visibilityStr.Equals("True", StringComparison.OrdinalIgnoreCase))
                visibility = true;
            else if (visibilityStr.Equals("False", StringComparison.OrdinalIgnoreCase) == false)
                throw new ExecuteException($"Invalid boolean value [{visibilityStr}]");

            if (cmd.Addr.Plugin.Sections.ContainsKey("Interface") == false)
            {
                logs.Add(new LogInfo(LogState.Error, $"Plugin [{cmd.Addr.Plugin.ShortPath}] does not have section [Interface]"));
                return logs;
            }

            List<UICommand> uiCodes = cmd.Addr.Plugin.Sections["Interface"].GetUICodes();
            UICommand uiCmd = uiCodes.FirstOrDefault(x => x.Key.Equals(info.InterfaceKey, StringComparison.OrdinalIgnoreCase));
            if (uiCmd == null)
            {
                logs.Add(new LogInfo(LogState.Error, $"Cannot find interface control [{info.InterfaceKey}] from section [Interface]"));
                return logs;
            }

            if (uiCmd.Visibility != visibility)
            {
                uiCmd.Visibility = visibility;
                UIRenderer.UpdatePlugin("Interface", uiCmd);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    MainWindow w = (Application.Current.MainWindow as MainWindow);
                    if (w.CurrentTree.Node.Data == cmd.Addr.Plugin)
                        w.DrawPlugin(cmd.Addr.Plugin);
                });
                
            }

            logs.Add(new LogInfo(LogState.Success, $"Interface control [{info.InterfaceKey}]'s visibility set to [{visibility}]"));

            return logs;
        }
    }
}
