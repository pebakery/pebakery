/*
    Copyright (C) 2020 Hajin Jang
    Licensed under GPL 3.0
 
    PEBakery is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.

    Additional permission under GNU GPL version 3 section 7

    If you modify this program, or any covered work, by linking
    or combining it with external libraries, containing parts
    covered by the terms of various license, the licensors of
    this program grant you additional permission to convey the
    resulting work. An external library is a library which is
    not derived from or based on this program. 
*/

// using RazorLight;
// using RazorLight.Caching;
using PEBakery.Helper;
using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;
using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PEBakery.Core.Html
{
    #region HtmlRenderer
    internal static class HtmlRenderer
    {
        public static string ScribanObjectRenamer(MemberInfo member)
        {
            return member.Name;
        }

        /// <summary>
        /// Render the HTML page using a Razor Core template and a model instance.
        /// </summary>
        /// <typeparam name="TModel">Type of the model.</typeparam>
        /// <param name="templateKey"></param>
        /// <param name="model"></param>
        /// <param name="textWriter"></param>
        public static async Task RenderHtmlAsync(string templateKey, Assembly templateAssembly, SystemLogModel model, TextWriter textWriter)
        {
            string templateBody = ResourceHelper.GetEmbeddedResourceString(templateKey, templateAssembly);
            Template template = Template.Parse(templateBody);
            if (template.HasErrors)
            {
                StringBuilder b = new StringBuilder();
                foreach (LogMessage err in template.Messages)
                {
                    b.AppendLine(err.Message);
                }
                string errMsg = b.ToString();

                MessageBox.Show(errMsg, "HTML Template Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            ScriptObject root = new ScriptObject();
            root.Import(model, renamer: ScribanObjectRenamer);
            root.Import(nameof(LogStateCssTrClass), new Func<LogState, string>(LogStateCssTrClass));
            root.Import(nameof(LogStateCssTdClass), new Func<LogState, string>(LogStateCssTdClass));
            root.Import(nameof(LogStateFaIcon), new Func<LogState, string>(LogStateFaIcon));

            TemplateContext ctx = new TemplateContext();
            ctx.PushGlobal(root);
            ctx.TemplateLoader = new LogLayoutTemplateLoader(templateAssembly);
            ctx.LoopLimit = int.MaxValue;

            try
            {
                await textWriter.WriteAsync(template.Render(ctx));
            }
            catch (Exception e)
            {
                MessageBox.Show(Logger.LogExceptionMessage(e), "HTML Template Error", MessageBoxButton.OK, MessageBoxImage.Error);

            }
        }

        /// <summary>
        /// Render the HTML page using a Razor Core template and a model instance.
        /// </summary>
        /// <typeparam name="TModel">Type of the model.</typeparam>
        public static async Task RenderHtmlAsync(string templateKey, Assembly templateAssembly, BuildLogModel model, TextWriter textWriter)
        {
            string templateBody = ResourceHelper.GetEmbeddedResourceString(templateKey, templateAssembly);
            Template template = Template.Parse(templateBody);
            if (template.HasErrors)
            {
                StringBuilder b = new StringBuilder();
                foreach (LogMessage err in template.Messages)
                {
                    b.AppendLine(err.Message);
                }
                string errMsg = b.ToString();

                MessageBox.Show(errMsg, "HTML Template Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            ScriptObject so = new ScriptObject();
            so.Import(model, renamer: member => member.Name);

            TemplateContext ctx = new TemplateContext();
            ctx.PushGlobal(so);
            ctx.TemplateLoader = new LogLayoutTemplateLoader(templateAssembly);
            ctx.LoopLimit = int.MaxValue;

            try
            {
                await textWriter.WriteAsync(template.Render(ctx));
            }
            catch (Exception e)
            {
                MessageBox.Show(Logger.LogExceptionMessage(e), "HTML Template Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        public static string LogStateCssTrClass(LogState state)
        {
            switch (state)
            {
                case LogState.Warning:
                    return "table-warning";
                case LogState.CriticalError:
                case LogState.Error:
                    return "table-danger";
                case LogState.Muted:
                    return "text-muted";
                default:
                    return string.Empty;
            }
        }

        public static string LogStateCssTdClass(LogState state)
        {
            switch (state)
            {
                case LogState.Success:
                    return "text-success";
                case LogState.Overwrite:
                    return "text-overwrite";
                case LogState.Info:
                    return "text-info";
                default:
                    return string.Empty;
            }
        }

        public static string LogStateFaIcon(LogState state)
        {
            switch (state)
            {
                case LogState.Success:
                    return @"<i class=""fas fa-fw fa-check""></i>";
                case LogState.Warning:
                    return @"<i class=""fas fa-fw fa-exclamation-triangle""></i>";
                case LogState.Overwrite:
                    return @"<i class=""fas fa-fw fa-copy""></i>";
                case LogState.Error:
                case LogState.CriticalError:
                    return @"<i class=""fas fa-fw fa-times""></i>";
                case LogState.Info:
                    return @"<i class=""fas fa-fw fa-info-circle""></i>";
                case LogState.Ignore:
                    return @"<i class=""fas fa-fw fa-file""></i>";
                case LogState.Muted:
                    return @"<i class=""fas fa-fw fa-lock""></i>";
                default:
                    return string.Empty;
            }
        }
    }

    public class LogLayoutTemplateLoader : ITemplateLoader
    {
        private readonly Assembly _assembly;
        public LogLayoutTemplateLoader(Assembly assembly)
        {
            _assembly = assembly;
        }

        public string GetPath(TemplateContext context, SourceSpan callerSpan, string templateName)
        {
            return templateName;
        }

        public string Load(TemplateContext context, SourceSpan callerSpan, string templatePath)
        {
            string templateStr = ResourceHelper.GetEmbeddedResourceString("Html." + templatePath, _assembly);
            return templateStr ?? string.Empty;
        }

        public ValueTask<string> LoadAsync(TemplateContext context, SourceSpan callerSpan, string templatePath)
        {
            string templateBody = Load(context, callerSpan, templatePath);
            return new ValueTask<string>(templateBody);
        }
    }

     
}