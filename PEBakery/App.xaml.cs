using PEBakery.Core;
using System;
using System.Windows;

namespace PEBakery
{
    // ReSharper disable RedundantExtendsListEntry
    public partial class App : Application
    {
        internal void App_Startup(object sender, StartupEventArgs e)
        {
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
    }
}
