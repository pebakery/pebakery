using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;
using PEBakery.Core;

namespace PEBakery
{
    // ReSharper disable RedundantExtendsListEntry
    public partial class App : Application
    {
        internal void App_Startup(object sender, StartupEventArgs e)
        {
            Global.PreInit(e.Args);
        }
    }
}
