using PEBakery.Core;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;

namespace PEBakery
{
    // ReSharper disable RedundantExtendsListEntry
    public partial class App : Application
    {
        private bool _automationPeerErrorLogged = false;

        internal void App_Startup(object sender, StartupEventArgs e)
        {
            DispatcherUnhandledException += App_DispatcherUnhandledException;

            try
            {
                Global.PreInit(e.Args, false);
            }
            catch (Exception ex)
            {
                string errMsg = Logger.LogExceptionMessage(ex, LogDebugLevel.PrintExceptionStackTrace);
                MessageBox.Show(errMsg, "PreInit Eror", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// WPF's UI Automation layer can throw KeyNotFoundException from it's item peer cache (MS.Internal.WeakDictionary,
        /// reached via ItemAutomationPeer.AddToParentProxyWeakRefCache or ItemsControlAutomationPeer.GetChildrenCore)
        /// while firing automation events after a layout pass on a TreeView/ListView. Upstream: dotnet/wpf#7542.
        /// Happens when a UI Automation client (screen reader, touch keyboard, accessibility/automation tools, etc.) is active.
        /// Since the exception is raised inside the framework's automation cache bookkeeping and does not corrupt PEBakery's state,
        /// we log it to the System log and swallow it instead of letting it reach the AppDomain crash handler.
        /// </summary>
        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            if (!IsWpfAutomationPeerBug(e.Exception))
                return;

            e.Handled = true;

            // Log only once to avoid flooding the system log if the automation client keeps triggering it.
            if (_automationPeerErrorLogged)
                return;
            _automationPeerErrorLogged = true;
            Global.Logger?.SystemWrite(new LogInfo(LogState.Warning, $"[Ignore] Internal WPF UI Automation error: {Logger.LogExceptionMessage(e.Exception, LogDebugLevel.PrintExceptionStackTrace)}"));
        }

        private static bool IsWpfAutomationPeerBug(Exception ex)
        {
            return ex is KeyNotFoundException &&
                ex.StackTrace?.Contains("System.Windows.Automation.Peers.", StringComparison.Ordinal) == true;
        }
    }
}
