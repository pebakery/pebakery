/*
    Copyright (C) 2017-2023 Hajin Jang
 
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
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;

namespace PEBakery.Helper.Tests
{
    [TestClass]
    [TestCategory("Helper")]
    [TestCategory("ImageHelper")]
    public class ImageHelperTests
    {
        [TestMethod]
        public void MaskWhiteAsTransparent()
        {
            string srcDir = Path.Combine(TestSetup.SampleDir, "ImageHelper", "MaskWhiteAsTransparent");

            void Template(string srcFileName, string expectFileName)
            {
                string srcFilePath = Path.Combine(srcDir, srcFileName);
                string expectFilePath = Path.Combine(srcDir, expectFileName);

                BitmapSource srcBitmap;
                using (FileStream fs = new FileStream(srcFilePath, FileMode.Open, FileAccess.Read))
                {
                    srcBitmap = ImageHelper.ImageToBitmapImage(fs);
                }
                BitmapSource expectBitmap;
                using (FileStream fs = new FileStream(expectFilePath, FileMode.Open, FileAccess.Read))
                {
                    expectBitmap = ImageHelper.ImageToBitmapImage(fs);
                }

                BitmapSource destBitmap = ImageHelper.MaskWhiteAsTransparent(srcBitmap);

                byte[] expectPixels = new byte[srcBitmap.PixelWidth * srcBitmap.PixelHeight * 4];
                expectBitmap.CopyPixels(expectPixels, srcBitmap.PixelWidth * 4, 0);
                byte[] destPixels = new byte[srcBitmap.PixelWidth * srcBitmap.PixelHeight * 4];
                destBitmap.CopyPixels(destPixels, srcBitmap.PixelWidth * 4, 0);
                Assert.IsTrue(expectPixels.SequenceEqual(destPixels));
            }

            Template("BeforeBGRA32.bmp", "AfterBGRA32.bmp");
            Template("BeforeBGR32.bmp", "AfterBGRA32.bmp");
            Template("BeforeBGR24.bmp", "AfterBGRA32.bmp");
            Template("BeforeBGR565.bmp", "AfterBGRA32.bmp");
            Template("BeforeBGR555.bmp", "AfterBGRA32.bmp");
        }
    }
}
