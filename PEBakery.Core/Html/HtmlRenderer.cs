/*
    Copyright (C) 2020-2022 Hajin Jang
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
        public static async Task RenderHtmlAsync<TModel>(string templateKey, Assembly templateAssembly, TModel model, TextWriter textWriter)
        {
            string? templateBody = ResourceHelper.GetEmbeddedResourceString(templateKey, templateAssembly);
            if (templateBody == null)
            {
                SystemHelper.MessageBoxDispatcherShow("Failed to read HTML Template.", "HTML Template Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Template template = Template.Parse(templateBody);
            if (template.HasErrors)
            {
                StringBuilder b = new StringBuilder();
                foreach (LogMessage err in template.Messages)
                {
                    b.AppendLine(err.Message);
                }
                string errMsg = b.ToString();

                SystemHelper.MessageBoxDispatcherShow(errMsg, "HTML Template Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            ScriptObject root = new ScriptObject();
            // Import data model
            root.Import(model, renamer: ScribanObjectRenamer);
            // Import .NET functions
            root.Import(nameof(LogStateCssTrClass), new Func<LogState, string>(LogStateCssTrClass));
            root.Import(nameof(LogStateCssTdClass), new Func<LogState, string>(LogStateCssTdClass));
            root.Import(nameof(LogStateCssFaClass), new Func<LogState, string>(LogStateCssFaClass));
            root.Import(nameof(LogStateFaIcon), new Func<LogState, string>(LogStateFaIcon));
            root.Import(nameof(LogStateStr), new Func<bool, LogState, string>(LogStateStr));
            root.Import(nameof(BuildErrorWarnRefId), new Func<int, LogState, string>(BuildErrorWarnRefId));

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
                Global.Logger.SystemWrite(new LogInfo(LogState.Error, e));
                SystemHelper.MessageBoxDispatcherShow(Logger.LogExceptionMessage(e), "HTML Template Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static string LogStateCssTrClass(LogState state)
        {
            switch (state)
            {
                case LogState.Warning:
                    return "table-warning";
                case LogState.CriticalError:
                case LogState.Error:
                    return "table-danger";
                case LogState.Ignore:
                case LogState.Muted:
                    return "text-muted";
                case LogState.Debug:
                    return "table-debug";
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

        public static string LogStateCssFaClass(LogState state)
        {
            switch (state)
            {
                case LogState.Success:
                    return "text-success";
                case LogState.Warning:
                    return "text-warning";
                case LogState.Overwrite:
                    return "text-overwrite";
                case LogState.Error:
                case LogState.CriticalError:
                    return "text-danger";
                case LogState.Info:
                    return "text-info";
                case LogState.Ignore:
                case LogState.Muted:
                    return "text-muted";
                case LogState.Debug:
                    return "text-debug";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Return corresponding Font Awesome image
        /// </summary>
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
                    return @"<i class=""fas fa-fw fa-times""></i>";
                case LogState.CriticalError:
                    return @"<i class=""fas fa-fw fa-ban""></i>"; 
                case LogState.Info:
                    return @"<i class=""fas fa-fw fa-info-circle""></i>";
                case LogState.Ignore:
                    return @"<i class=""fas fa-fw fa-file""></i>";
                case LogState.Muted:
                    return @"<i class=""fas fa-fw fa-lock""></i>";
                case LogState.Debug:
                    return @"<i class=""fas fa-fw fa-bug""></i>";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Return corresponding bootstrap-icons image
        /// </summary>
        public static string LogStateBiIcon(LogState state)
        {
            switch (state)
            {
                case LogState.Success:
                    return @"<i class=""bi bi-check""></i>";
                case LogState.Warning:
                    return @"<i class=""bi bi-exclamation-triangle-fill""></i>";
                case LogState.Overwrite:
                    return @"<i class=""bi bi-files""></i>";
                case LogState.Error:
                    return @"<i class=""bi bi-x-circle-fill""></i>";
                case LogState.CriticalError:
                    return @"<i class=""bi bi-slash-circle""></i>"; 
                // return @"<i class=""bi bi-x""></i>";
                case LogState.Info:
                    return @"<i class=""bi bi-info-circle-fill""></i>";
                case LogState.Ignore:
                    return @"<i class=""bi bi-file-earmark-text""></i>";
                case LogState.Muted:
                    return @"<i class=""bi bi-lock-fill""></i>";
                case LogState.Debug:
                    return @"<i class=""bi bi-bug-fill""></i>";
                default:
                    return string.Empty;
            }
        }

        public static string LogStateStr(bool withSqureBrackets, LogState state)
        {
            switch (state)
            {
                case LogState.None:
                    return string.Empty;
                default:
                    if (withSqureBrackets)
                        return $"[{state}]";
                    else
                        return state.ToString();
            }
        }

        public static string BuildErrorWarnRefId(int href, LogState state)
        {
            switch (state)
            {
                case LogState.Warning:
                    return $"id=\'warn_{href}\'";
                case LogState.CriticalError:
                case LogState.Error:
                    return $"id=\'error_{href}\'";
                default:
                    return string.Empty;
            }
        }
    }
    #endregion

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
            string? templateStr = ResourceHelper.GetEmbeddedResourceString("Html." + templatePath, _assembly);
            return templateStr ?? string.Empty;
        }

        public ValueTask<string> LoadAsync(TemplateContext context, SourceSpan callerSpan, string templatePath)
        {
            string templateBody = Load(context, callerSpan, templatePath);
            return new ValueTask<string>(templateBody);
        }
    }
}