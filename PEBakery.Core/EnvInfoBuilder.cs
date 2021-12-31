using PEBakery.Helper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

#nullable enable

namespace PEBakery.Core
{
    #region EnvInfoBuilder
    /// <summary>
    /// Gather and stores host environment information.
    /// Can be appnded by 
    /// </summary>
    /// <remarks>
    /// MUST NOT THROW EXCEPTIONS! This class is designed for in UnhandledException handler.
    /// </remarks>
    public class EnvInfoBuilder
    {
        public Dictionary<string, EnvInfoSection> InfoDict { get; }
        public ProgramInfoSection ProgramInfoSection { get; }
        public HostInfoSection HostInfoSection { get; }

        public EnvInfoBuilder()
        {
            InfoDict = new Dictionary<string, EnvInfoSection>();

            // [PEBakery] Section
            ProgramInfoSection = new ProgramInfoSection(-100);
            InfoDict[ProgramInfoSection.SectionName] = ProgramInfoSection;

            // [Environment] Section
            HostInfoSection = new HostInfoSection(0);
            InfoDict[HostInfoSection.SectionName] = HostInfoSection;
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            foreach (KeyValuePair<string, EnvInfoSection> kv in InfoDict.OrderBy(kv => kv.Value.Order))
            {
                b.AppendLine(kv.Value.ToString());
            }
            return b.ToString();
        }
    }

    public class EnvInfoSection
    {
        /// <summary>
        /// 0 is [Environment] section
        /// </summary>
        public int Order { get; set; } = 1;
        public string SectionName { get; set; } = string.Empty;
        public List<EnvInfoKeyValue> KeyValues { get; set; } = new();

        public EnvInfoSection()
        {

        }

        public EnvInfoSection(int order, string sectionName)
        {
            Order = order;
            SectionName = sectionName;
        }

        protected virtual List<EnvInfoKeyValue> PropertyToKeyValue()
        {
            return new List<EnvInfoKeyValue>();
        }

        public override string ToString()
        {
            List<EnvInfoKeyValue> propKeyValues = PropertyToKeyValue();
            IEnumerable<EnvInfoKeyValue> mergeKeyValues = propKeyValues.Concat(KeyValues);

            int maxKeyWidth = mergeKeyValues.Max(kv => kv.Key.Length);

            StringBuilder b = new StringBuilder();
            b.AppendLine($"[{SectionName}]");
            foreach (EnvInfoKeyValue kv in mergeKeyValues)
                b.AppendLine($"{kv.Key.PadRight(maxKeyWidth)} | {kv.Value}");

            return b.ToString();
        }
    }

    public class EnvInfoKeyValue
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;

        public EnvInfoKeyValue() { }

        public EnvInfoKeyValue(string key, string value)
        {
            Key = key;
            Value = value;
        }
    }
    #endregion

    #region class HostInfoSection
    /// <summary>
    /// Trace host environment infomation.
    /// </summary>
    /// <remarks>
    /// MUST NOT THROW EXCEPTIONS! This class is designed for in UnhandledException handler.
    /// </remarks>
    public class HostInfoSection : EnvInfoSection
    {
        // [Environment] key-values
        public Version WindowsVersion { get; }
        public Version DotnetVersion { get; }
        public Architecture SystemArch { get; }
        public Architecture ProccessArch { get; }
        public CultureInfo Language { get; }
        public Encoding AnsiEncoding { get; }
        public Encoding OemEncoding { get; }

        public HostInfoSection(int order)
        {
            Order = order;
            SectionName = "Host";

            WindowsVersion = Environment.OSVersion.Version;
            DotnetVersion = Environment.Version;
            SystemArch = RuntimeInformation.OSArchitecture;
            ProccessArch = RuntimeInformation.ProcessArchitecture;
            Language = CultureInfo.CurrentCulture;
            AnsiEncoding = EncodingHelper.DefaultAnsi;
            OemEncoding = Console.OutputEncoding;
        }

        protected override List<EnvInfoKeyValue> PropertyToKeyValue()
        {
            return new List<EnvInfoKeyValue>
            {
                new EnvInfoKeyValue("Windows", $"{WindowsVersion} {SystemArch.ToString().ToLower()}"),
                new EnvInfoKeyValue(".NET Runtime", $"{DotnetVersion} {ProccessArch.ToString().ToLower()}"),
                new EnvInfoKeyValue("Language", Language.EnglishName),
                new EnvInfoKeyValue("ANSI Encoding", $"{AnsiEncoding.EncodingName} ({AnsiEncoding.CodePage})"),
                new EnvInfoKeyValue("OEM Encoding", $"{OemEncoding.EncodingName} ({OemEncoding.CodePage})"),
            };
        }
    }

    #endregion

    #region class ProgramInfoSection
    /// <summary>
    /// Trace PEBakery host environment infomation.
    /// </summary>
    /// <remarks>
    /// MUST NOT THROW EXCEPTIONS! This class is designed for in UnhandledException handler.
    /// </remarks>
    public class ProgramInfoSection : EnvInfoSection
    {
        public Version PEBakeryVersion { get; }
        public DateTime PEBakeryBuildDate { get; }

        public ProgramInfoSection(int order) : base()
        {
            Order = order;
            SectionName = "PEBakery";

            PEBakeryVersion = Global.Const.ProgramVersionInst.ToVersion();
            PEBakeryBuildDate = Global.BuildDate;
        }

        protected override List<EnvInfoKeyValue> PropertyToKeyValue()
        {
            return new List<EnvInfoKeyValue>()
            {
                new EnvInfoKeyValue("Version", $"{PEBakeryVersion} (Build {PEBakeryBuildDate:yyyyMMdd})"),
            };
        }
    }
    #endregion
}
