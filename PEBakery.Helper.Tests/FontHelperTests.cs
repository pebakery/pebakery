/*
    Copyright (C) 2022 Hajin Jang
 
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Globalization;

namespace PEBakery.Helper.Tests
{
    [TestClass]
    [TestCategory("PEBakery.Helper")]
    [TestCategory(nameof(FileHelper))]
    public class FontHelperTests
    {
        [TestMethod]
        public void IsFontInstalled()
        {
            Console.WriteLine("Running culture neutral tests...");

            // Every post-Windows 7 machine must have Segoe UI and Consolas installed.
            Assert.IsTrue(FontHelper.IsFontInstalled("Segoe UI"));
            Assert.IsTrue(FontHelper.IsFontInstalled("Consolas"));

            // No one wants to create a font named 'Never-exist-domain + rand hex' in Korean, right?
            Random rand = new Random();
            string neverExistFont = "절대로존재하지않는無글꼴" + rand.Next().ToString("X8");
            Assert.IsFalse(FontHelper.IsFontInstalled(neverExistFont));

            // Korean machine specific test - IsFontInstalled with CultureInfo
            CultureInfo ci = CultureInfo.CurrentCulture;
            if (ci.Name.Equals("ko-KR", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Running ko-KR specific tests...");
                Assert.IsTrue(FontHelper.IsFontInstalled("Malgun Gothic"));
                Assert.IsTrue(FontHelper.IsFontInstalled("맑은 고딕", ci));
            }
        }

        [TestMethod]
        public void DefaultMonospacedFontName()
        {
            string fontName = FontHelper.DefaultMonospacedFontName();
            Console.WriteLine($"Default monospaced font: {fontName}");
        }

        [TestMethod]
        public void DefaultMonospacedFontInfo()
        {
            FontHelper.FontInfo fontInfo = FontHelper.DefaultMonospacedFontInfo();
            Console.WriteLine($"Default monospaced font: {fontInfo.FontFamily.Source}, {fontInfo.FontWeight}, {fontInfo.PointSize}");
        }
    }
}
