using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows;

namespace PEBakery.Core.Arguments
{
    public abstract class ParamOptions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Cast<T>() where T : ParamOptions
        {
            return (T)this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Cast<T>(ParamOptions opts) where T : ParamOptions
        {
            return opts.Cast<T>();
        }
    }

    public class PEBakeryOptions : ParamOptions
    {
        [Option('b', "baseDir", Required = false, Default = null, HelpText = "Base directory to run PEBakery on.")]
        public string? BaseDir { get; set; }

        [Option("headless", Required = false, Default = false, HelpText = "Run in headless mode without GUI. Requires --build.")]
        public bool Headless { get; set; }

        [Option("build", Required = false, Default = false, HelpText = "Start building immediately. Use with --headless for CLI builds.")]
        public bool Build { get; set; }

        [Option('p', "project", Required = false, Default = null, HelpText = "Name of the project to build (e.g. 'PhoenixPE'). Uses the first project if not specified.")]
        public string? Project { get; set; }

        [Option('l', "logFile", Required = false, Default = null, HelpText = "Path to export the build log (HTML format).")]
        public string? LogFile { get; set; }

        [Usage(ApplicationAlias = "PEBakery.exe")]
        public static IEnumerable<Example> Examples
        {
            get
            {
                return new Example[]
                {
                    new Example("Run PEBakery with BaseDir", new PEBakeryOptions { BaseDir = @"D:\WinPE_dev" }),
                    new Example("Headless CLI build", new PEBakeryOptions { BaseDir = @"D:\WinPE_dev", Headless = true, Build = true, Project = "PhoenixPE" }),
                };
            }
        }
    }

    #region CommandLine
    public class ArgumentParser
    {
        private PEBakeryOptions? _opts = null;
        private ParserResult<PEBakeryOptions>? _parserResult = null;
        public bool IsArgumentValid { get; set; } = false;

        public PEBakeryOptions? Parse(string[] args)
        {
            Parser parser = new Parser(conf =>
            {
                conf.HelpWriter = null;
                conf.CaseInsensitiveEnumValues = true;
                conf.CaseSensitive = false;
            });

            _parserResult = parser.ParseArguments<PEBakeryOptions>(args);
            _parserResult.WithParsed<PEBakeryOptions>(x => _opts = x)
                .WithNotParsed(errs =>
                { // Print error message
                    string helpMessage = BuildHelpMessage();
                    if (Global.HeadlessMode)
                        Console.Error.WriteLine(helpMessage);
                    else
                        MessageBox.Show(helpMessage, "PEBakery CommandLine Help", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            return _opts;
        }

        public string BuildHelpMessage(string? appendMessage = null)
        {
            HelpText helpText = HelpText.AutoBuild(_parserResult, h =>
            {
                h.AddNewLineBetweenHelpSections = true;
                h.Heading = $"PEBakery {Global.Const.ProgramVersionStrFull}";
                if (appendMessage != null)
                    h.AddPreOptionsText(appendMessage);
                return h;
            });
            return helpText.ToString();
        }
    }
    #endregion
}
