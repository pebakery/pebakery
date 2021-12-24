/*
    Copyright (C) 2018-2022 Hajin Jang
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
using System.Text;
using System.Web;

namespace PEBakery.Helper
{
    public static class HtmlHelper
    {
        public enum MimeType
        {
            // Web and Text
            PlainText = 0,
            Html = 1,
            Css = 2,
            Js = 3,
            // Image
            Bmp = 0x20,
            Jpeg = 0x21,
            Gif = 0x22,
            Png = 0x23,
            Svg = 0x24,
            Ico = 0x25,
            WebP = 0x26,
        }

        public static string MimeToString(MimeType mime)
        {
            switch (mime)
            {
                // Web and Text
                case MimeType.PlainText:
                    return "text/plain";
                case MimeType.Html:
                    return "text/html";
                case MimeType.Css:
                    return "text/css";
                case MimeType.Js:
                    return "application/x-javascript";
                // Image
                case MimeType.Bmp:
                    return "image/bmp";
                case MimeType.Jpeg:
                    return "image/jpeg";
                case MimeType.Gif:
                    return "image/gif";
                case MimeType.Png:
                    return "image/png";
                case MimeType.Svg:
                    return "image/svg+xml";
                case MimeType.Ico:
                    return "image/x-icon";
                case MimeType.WebP:
                    return "image/webp";
                default:
                    throw new ArgumentException(nameof(mime));
            }
        }

        public static string GenerateDataUri(MimeType mime, byte[] src)
        {
            string mimeStr = MimeToString(mime);
            string encodedSrc = Convert.ToBase64String(src, Base64FormattingOptions.None);
            return $"data:{mimeStr};base64,{encodedSrc}";
        }

        public static string GenerateDataUri(MimeType mime, string src, bool base64)
        {
            string mimeStr = MimeToString(mime);
            if (base64)
            {
                byte[] buffer = Encoding.UTF8.GetBytes(src);
                string encodedSrc = Convert.ToBase64String(buffer, Base64FormattingOptions.None);
                return $"data:{mimeStr};base64,{encodedSrc}";
            }
            else
            {
                string encodedSrc = HttpUtility.UrlEncode(src);
                return $"data:{mimeStr},{encodedSrc}";
            }
        }
    }
}
