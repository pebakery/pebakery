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
        private readonly List<EnvInfoSection> _infoSections = new List<EnvInfoSection>();

        public ProgramInfoSection PEBakeryInfoSection { get; } = new ProgramInfoSection(EnvInfoSection.PEBakerySectionOrder);
        public HostInfoSection HostInfoSection { get; } = new HostInfoSection(EnvInfoSection.HostSectionOrder);


        public EnvInfoBuilder()
        {
            // [PEBakery] Section
            AddSection(PEBakeryInfoSection);

            // [Environment] Section
            AddSection(HostInfoSection);
        }

        public void AddSection(EnvInfoSection infoSection)
        {
            _infoSections.Add(infoSection);
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            foreach (EnvInfoSection section in _infoSections.OrderBy(x => x.Order))
            {
                b.AppendLine(section.ToString());
            }
            return b.ToString();
        }
    }

    public class EnvInfoSection
    {
        public const int FirstSectionOrder = -200;
        public const int PEBakerySectionOrder = -100;
        public const int MiddleSectionOrder = 0;
        public const int HostSectionOrder = 100;
        public const int LastSectionOrder = 200;

        /// <summary>
        /// 0 is [Environment] section.
        /// -100 is [PEBakery] section.
        /// </summary>
        public int Order { get; } = 1;
        public string SectionName { get; } = string.Empty;
        public List<EnvInfoKeyValue> KeyValues { get; } = new();
        // public List<KeyValuePair<string, string>> KeyValues { get; } = new();

        public EnvInfoSection(int order)
        {
            Order = order;
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
            if (0 < SectionName.Length)
                b.AppendLine($"[{SectionName}]");
            foreach (EnvInfoKeyValue kv in mergeKeyValues)
            {
                if (kv.Key.Length == 0)
                    b.AppendLine(kv.Value);
                else
                    b.AppendLine($"{kv.Key.PadRight(maxKeyWidth)} | {kv.Value}");
            }

            return b.ToString();
        }
    }

    public class EnvInfoKeyValue
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;

        public EnvInfoKeyValue() { }

        public EnvInfoKeyValue(string value)
        {
            Value = value;
        }

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

        public HostInfoSection(int order) :
            base(order, "Host")
        {
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
                new EnvInfoKeyValue("Windows", $"{WindowsVersion} ({SystemArch.ToString().ToLower()})"),
                new EnvInfoKeyValue(".NET Runtime", $"{DotnetVersion} ({ProccessArch.ToString().ToLower()})"),
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

        public ProgramInfoSection(int order) :
            base(order, "PEBakery")
        {
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
