using PEBakery.Core;
using System.Windows;

namespace PEBakery
{
    // ReSharper disable RedundantExtendsListEntry
    public partial class App : Application
    {
        internal void App_Startup(object sender, StartupEventArgs e)
        {
            Global.PreInit(e.Args, false);
        }
    }
}
