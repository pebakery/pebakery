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
using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Printing.IndexedProperties;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PEBakery.Core.Html
{
    #region HtmlRenderer
    internal class HtmlRenderer
    {
        // private readonly RazorLightEngine _engine = null;
        private readonly bool _isCachingEnabled;

        public HtmlRenderer(bool isCachingEnabled)
        {
            /*
            _isCachingEnabled = isCachingEnabled;

            RazorLightEngineBuilder builder = new RazorLightEngineBuilder().UseEmbeddedResourcesProject(typeof(Global));

            if (_isCachingEnabled)
                builder = builder.UseMemoryCachingProvider();

            _engine = builder.Build();
            */
        }

        /// <summary>
        /// Render the HTML page using a Razor Core template and a model instance.
        /// </summary>
        /// <typeparam name="TModel">Type of the model.</typeparam>
        /// <param name="templateKey"></param>
        /// <param name="model"></param>
        /// <param name="textWriter"></param>
        public async Task RenderHtmlAsync<TModel>(string templateKey, TModel model, TextWriter textWriter)
        {
            string templateBody = Properties.Resources.ResourceManager.GetString(templateKey);
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
            ctx.TemplateLoader = new LogLayoutTemplateLoader();
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
    }
    #endregion
    
    public class LogLayoutTemplateLoader : ITemplateLoader
    {
        public string GetPath(TemplateContext context, SourceSpan callerSpan, string templateName)
        {
            return templateName;
        }

        public string Load(TemplateContext context, SourceSpan callerSpan, string templatePath)
        {
            string templateStr = Properties.Resources.ResourceManager.GetString(templatePath);
            return templateStr ?? string.Empty;
        }

        public ValueTask<string> LoadAsync(TemplateContext context, SourceSpan callerSpan, string templatePath)
        {
            string templateBody = Load(context, callerSpan, templatePath);
            return new ValueTask<string>(templateBody);
        }
    }
}
