using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;

namespace PEBakery
{
    // ReSharper disable RedundantExtendsListEntry
    public partial class App : Application
    {
        internal void App_Startup(object sender, StartupEventArgs e)
        {
            // If no command line arguments were provided, don't process them 
            if (e.Args.Length == 0)
                Global.Args = new string[0];
            else if (e.Args.Length > 0)
                Global.Args = e.Args;

            // Initialize zlib and wimlib
            Global.NativeGlobalInit(AppDomain.CurrentDomain.BaseDirectory);

            // Why Properties.Resources is not available in App_Startup?
            // Version = Properties.Resources.EngineVersion;
            Global.BuildDate = BuildTimestamp.ReadDateTime();
        }
    }

    public static class BuildTimestamp
    {
        public static string ReadString()
        {
            var attr = Assembly.GetExecutingAssembly()
                .GetCustomAttributesData()
                .First(x => x.AttributeType.Name.Equals("TimestampAttribute", StringComparison.Ordinal));

            return attr.ConstructorArguments.First().Value as string;
        }

        public static DateTime ReadDateTime()
        {
            var attr = Assembly.GetExecutingAssembly()
                .GetCustomAttributesData()
                .First(x => x.AttributeType.Name.Equals("TimestampAttribute", StringComparison.Ordinal));

            string timestampStr = attr.ConstructorArguments.First().Value as string;
            return DateTime.ParseExact(timestampStr, "yyyy-MM-ddTHH:mm:ss.fffZ", null, DateTimeStyles.AssumeUniversal).ToUniversalTime();
        }
    }
}
