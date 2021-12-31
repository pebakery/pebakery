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
    /// Generates host environment information text.
    /// Can be appnded by 
    /// </summary>
    /// <remarks>
    /// MUST NOT THROW EXCEPTIONS! This class is designed for in UnhandledException handler.
    /// </remarks>
    public class EnvInfoBuilder
    {
        public const int FirstSectionOrder = -200;
        public const int PEBakerySectionOrder = -100;
        public const int MiddleSectionOrder = 0;
        public const int HostSectionOrder = 100;
        public const int LastSectionOrder = 200;

        private readonly List<EnvInfoSectionBase> _infoSections = new List<EnvInfoSectionBase>();

        public ProgramInfoSection PEBakeryInfoSection { get; } = new ProgramInfoSection(PEBakerySectionOrder);
        public HostInfoSection HostInfoSection { get; } = new HostInfoSection(HostSectionOrder);


        public EnvInfoBuilder()
        {
            // [PEBakery] Section
            AddSection(PEBakeryInfoSection);

            // [Environment] Section
            AddSection(HostInfoSection);
        }

        public void AddSection(EnvInfoSectionBase infoSection)
        {
            _infoSections.Add(infoSection);
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            foreach (EnvInfoSectionBase section in _infoSections.OrderBy(x => x.Order))
                b.AppendLine(section.ToString());
            return b.ToString();
        }
    }

    /// <summary>
    /// Represents environment infomation section.
    /// </summary>
    /// <remarks>
    /// MUST NOT THROW EXCEPTIONS! This class is designed for in UnhandledException handler.
    /// </remarks>
    public abstract class EnvInfoSectionBase
    {
        public int Order { get; } = EnvInfoBuilder.HostSectionOrder + 1;
        /// <summary>
        /// Leave it as blank to hide [SectionName] banner.
        /// </summary>
        public string SectionName { get; } = string.Empty;
        /// <summary>
        /// Leave key as blank to print plain message.
        /// </summary>
        public List<KeyValuePair<string, string>> KeyValues { get; } = new();

        public EnvInfoSectionBase(int order)
        {
            Order = order;
        }

        public EnvInfoSectionBase(int order, string sectionName)
        {
            Order = order;
            SectionName = sectionName;
        }

        protected abstract List<KeyValuePair<string, string>> PropertyToKeyValue();

        public override string ToString()
        {
            List<KeyValuePair<string, string>> propKeyValues = PropertyToKeyValue();
            IEnumerable<KeyValuePair<string, string>> mergeKeyValues = propKeyValues.Concat(KeyValues);

            int maxKeyWidth = mergeKeyValues.Max(kv => kv.Key.Length);

            StringBuilder b = new StringBuilder();
            if (0 < SectionName.Length)
                b.AppendLine($"[{SectionName}]");
            foreach (var kv in mergeKeyValues)
            {
                if (kv.Key.Length == 0)
                    b.AppendLine(kv.Value);
                else
                    b.AppendLine($"{kv.Key.PadRight(maxKeyWidth)} | {kv.Value}");
            }

            return b.ToString();
        }
    }

    public sealed class EnvInfoSection : EnvInfoSectionBase
    {
        public EnvInfoSection(int order)
            : base(order)
        {
        }

        public EnvInfoSection(int order, string sectionName)
            : base(order, sectionName)
        {
        }

        protected override List<KeyValuePair<string, string>> PropertyToKeyValue() => new List<KeyValuePair<string, string>>();
    }
    #endregion

    #region class ProgramInfoSection
    public sealed  class ProgramInfoSection : EnvInfoSectionBase
    {
        public Version PEBakeryVersion { get; }
        public DateTime PEBakeryBuildDate { get; }

        public ProgramInfoSection(int order) :
            base(order, "PEBakery")
        {
            PEBakeryVersion = Global.Const.ProgramVersionInst.ToVersion();
            PEBakeryBuildDate = Global.BuildDate;
        }

        protected override List<KeyValuePair<string, string>> PropertyToKeyValue()
        {
            return new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>("Version", $"{PEBakeryVersion} (Build {PEBakeryBuildDate:yyyyMMdd})"),
            };
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
    public sealed class HostInfoSection : EnvInfoSectionBase
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

        protected override List<KeyValuePair<string, string>> PropertyToKeyValue()
        {
            return new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("Windows", $"{WindowsVersion} ({SystemArch.ToString().ToLower()})"),
                new KeyValuePair<string, string>(".NET Runtime", $"{DotnetVersion} ({ProccessArch.ToString().ToLower()})"),
                new KeyValuePair<string, string>("Language", Language.EnglishName),
                new KeyValuePair<string, string>("ANSI Encoding", $"{AnsiEncoding.EncodingName} ({AnsiEncoding.CodePage})"),
                new KeyValuePair<string, string>("OEM Encoding", $"{OemEncoding.EncodingName} ({OemEncoding.CodePage})"),
            };
        }
    }
    #endregion
}
