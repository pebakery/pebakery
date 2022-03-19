/*
    Copyright (C) 2020-2022 Hajin Jang
    Licensed under MIT License.
 
    MIT License

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.
*/

using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PEBakery.Helper
{
    /// <summary>
    /// Helps to manage project resources
    /// </summary>
    public static class ResourceHelper
    {
        #region EmbeddedResource
        private readonly static (string, string)[] ResourceNameEscapeMap = new (string, string)[]
        {
            ("\\", "."),
            ("/", "."),
            (" ", "_"),
        };

        public static string EscapeEmbeddedResourcePath(string resourcePath)
        {
            return StringHelper.ReplaceEx(resourcePath, ResourceNameEscapeMap, StringComparison.Ordinal);
        }

        private static string? GetEmbeddedResourceName(Assembly assembly, string resourcePath)
        {
            if (assembly.GetName().Name is string namespaceStr)
                return $"{namespaceStr}.{resourcePath}";
            return null;
        }

        public static Stream? GetEmbeddedResourceContent(string resourcePath, Assembly assembly)
        {
            if (GetEmbeddedResourceName(assembly, resourcePath) is string resourceName)
                return assembly.GetManifestResourceStream(resourceName);
            return null;
        }

        public static Stream? GetEmbeddedResourceContent(string resourcePath)
        {
            Assembly assembly = Assembly.GetCallingAssembly();
            if (GetEmbeddedResourceName(assembly, resourcePath) is string resourceName)
                return assembly.GetManifestResourceStream(resourceName);
            return null;
        }

        public static string? GetEmbeddedResourceString(string resourcePath, Assembly assembly)
        {
            if (GetEmbeddedResourceName(assembly, resourcePath) is not string resourceName)
                return null;

            using (Stream? s = assembly.GetManifestResourceStream(resourceName))
            {
                if (s == null)
                    return null;

                using (StreamReader sr = new StreamReader(s, Encoding.UTF8, false))
                {
                    return sr.ReadToEnd();
                }
            }
        }

        public static string? GetEmbeddedResourceString(string resourcePath)
        {
            Assembly assembly = Assembly.GetCallingAssembly();
            if (GetEmbeddedResourceName(assembly, resourcePath) is not string resourceName)
                return null;

            using (Stream? s = assembly.GetManifestResourceStream(resourceName))
            {
                if (s == null)
                    return null;

                using (StreamReader sr = new StreamReader(s, Encoding.UTF8, false))
                {
                    return sr.ReadToEnd();
                }
            }
        }

        public static Task<string>? GetEmbeddedResourceStringAsync(string resourcePath, Assembly assembly)
        {
            if (GetEmbeddedResourceName(assembly, resourcePath) is not string resourceName)
                return null;

            using (Stream? s = assembly.GetManifestResourceStream(resourceName))
            {
                if (s == null)
                    return null;

                using (StreamReader sr = new StreamReader(s, Encoding.UTF8, false))
                {
                    return sr.ReadToEndAsync();
                }
            }
        }

        public static Task<string>? GetEmbeddedResourceStringAsync(string resourcePath)
        {
            Assembly assembly = Assembly.GetCallingAssembly();
            if (GetEmbeddedResourceName(assembly, resourcePath) is not string resourceName)
                return null;

            using (Stream? s = assembly.GetManifestResourceStream(resourceName))
            {
                if (s == null)
                    return null;

                using (StreamReader sr = new StreamReader(s, Encoding.UTF8, false))
                {
                    return sr.ReadToEndAsync();
                }
            }
        }
        #endregion
    }
}
