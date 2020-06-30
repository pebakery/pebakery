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

using RazorLight;
using RazorLight.Caching;
using System.IO;
using System.Threading.Tasks;

namespace PEBakery.Core.Razor
{
    #region RazorRenderer
    internal class RazorRenderer
    {
        private readonly RazorLightEngine _engine = null;
        private readonly bool _isCachingEnabled;

        public RazorRenderer(bool isCachingEnabled)
        {
            _isCachingEnabled = isCachingEnabled;

            RazorLightEngineBuilder builder = new RazorLightEngineBuilder().UseEmbeddedResourcesProject(typeof(Global));

            if (_isCachingEnabled)
                builder = builder.UseMemoryCachingProvider();

            _engine = builder.Build();
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
            ITemplatePage templatePage = null;
            if (_engine.Handler.IsCachingEnabled)
            {
                TemplateCacheLookupResult cacheResult = _engine.Handler.Cache.RetrieveTemplate(templateKey);
                if (cacheResult.Success)
                    templatePage = cacheResult.Template.TemplatePageFactory();
            }

            if (templatePage == null)
                templatePage = await _engine.CompileTemplateAsync(templateKey);

            await _engine.RenderTemplateAsync(templatePage, model, textWriter);
        }
    }
    #endregion
}
