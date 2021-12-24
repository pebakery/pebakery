/*
    Copyright (C) 2016-2022 Hajin Jang
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

using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PEBakery.Helper
{
    #region ImageHelper
    public static class ImageHelper
    {
        #region ImageType
        public enum ImageFormat
        {
            Bmp, Jpg, Png, Gif, Ico, Svg
        }

        public static readonly ReadOnlyDictionary<string, ImageFormat> ImageFormatDict = new ReadOnlyDictionary<string, ImageFormat>(
            new Dictionary<string, ImageFormat>(StringComparer.OrdinalIgnoreCase)
            {
                { ".bmp", ImageFormat.Bmp },
                { ".jpg", ImageFormat.Jpg },
                { ".png", ImageFormat.Png },
                { ".gif", ImageFormat.Gif },
                { ".ico", ImageFormat.Ico },
                { ".svg", ImageFormat.Svg },
            });

        /// <summary>
        /// Return true if success
        /// </summary>
        /// <param name="path"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool GetImageFormat(string path, out ImageFormat type)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            string ext = Path.GetExtension(path);
            if (ImageFormatDict.ContainsKey(ext))
            {
                type = ImageFormatDict[ext];
                return true;
            }
            else
            {
                type = ImageFormat.Bmp; // Dummy
                return false;
            }
        }
        #endregion

        #region Bitmap
        public static BitmapImage ImageToBitmapImage(byte[] image)
        {
            BitmapImage bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = new MemoryStream(image);
            bitmap.EndInit();
            return bitmap;
        }

        public static BitmapImage ImageToBitmapImage(Stream stream)
        {
            BitmapImage bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            return bitmap;
        }

        public static (int, int) GetImageSize(Stream stream)
        {
            BitmapImage bitmap = ImageToBitmapImage(stream);
            return (bitmap.PixelWidth, bitmap.PixelHeight);
        }

        public static ImageBrush ImageToImageBrush(Stream stream)
        {
            BitmapImage bitmap = ImageToBitmapImage(stream);
            ImageBrush brush = new ImageBrush
            {
                ImageSource = bitmap
            };
            return brush;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ImageBrush BitmapImageToImageBrush(BitmapImage bitmap)
        {
            return new ImageBrush { ImageSource = bitmap };
        }
        #endregion

        #region Svg
        public static DrawingGroup SvgToDrawingGroup(Stream stream)
        {
            WpfDrawingSettings settings = new WpfDrawingSettings
            {
                CultureInfo = CultureInfo.InvariantCulture,
                IncludeRuntime = true,
            };

            using (FileSvgReader reader = new FileSvgReader(settings))
            {
                reader.Read(stream);
                return reader.Drawing;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DrawingBrush SvgToDrawingBrush(Stream stream)
        {
            return new DrawingBrush { Drawing = SvgToDrawingGroup(stream) };
        }

        public static (double Width, double Height) GetSvgSizeDouble(Stream stream)
        {
            DrawingGroup drawingGroup = SvgToDrawingGroup(stream);
            Rect rect = drawingGroup.Bounds;
            return (rect.Width, rect.Height);
        }

        public static (int Width, int Height) GetSvgSizeInt(Stream stream)
        {
            (double width, double height) = GetSvgSizeDouble(stream);
            int newWidth = (int)Math.Round(width, 0);
            int newHeight = (int)Math.Round(height, 0);
            return (newWidth, newHeight);
        }
        #endregion

        #region StretchSizeAspectRatio
        public static (int Width, int Height) StretchSizeAspectRatio(int currentWidth, int currentHeight, int targetWidth, int targetHeight)
        {
            double currentAspectRatio = (double)currentWidth / currentHeight;
            double targetAspectRatio = (double)targetWidth / targetHeight;

            int newWidth;
            int newHeight;
            // Aspect ratio is equal, return original target width and height
            if (NumberHelper.DoubleEquals(currentAspectRatio, targetAspectRatio))
            {
                newWidth = targetWidth;
                newHeight = targetHeight;
            }
            else if (currentAspectRatio < targetAspectRatio)
            { // Shrink width
                newWidth = (int)Math.Round(targetHeight * targetAspectRatio, 0);
                newHeight = targetHeight;
            }
            else
            { // Shrink height
                newWidth = targetWidth;
                newHeight = (int)Math.Round(targetWidth * targetAspectRatio, 0);
            }

            return (newWidth, newHeight);
        }

        public static (double Width, double Height) StretchSizeAspectRatio(double currentWidth, double currentHeight, double targetWidth, double targetHeight)
        {
            double currentAspectRatio = currentWidth / currentHeight;
            double targetAspectRatio = targetWidth / targetHeight;

            double newWidth;
            double newHeight;

            // Aspect ratio is equal, return original target width and height
            if (NumberHelper.DoubleEquals(currentAspectRatio, targetAspectRatio))
            {
                newWidth = targetWidth;
                newHeight = targetHeight;
            }
            else if (currentAspectRatio < targetAspectRatio)
            { // Shrink width
                newWidth = targetHeight * currentAspectRatio;
                newHeight = targetHeight;
            }
            else
            { // Shrink height
                newWidth = targetWidth;
                newHeight = targetWidth * currentAspectRatio;
            }

            return (newWidth, newHeight);
        }

        public static (int Width, int Height) DownSizeAspectRatio(int currentWidth, int currentHeight, int targetWidth, int targetHeight)
        {
            if (currentWidth <= targetWidth && currentHeight <= targetHeight)
                return (currentWidth, currentHeight);
            else
                return StretchSizeAspectRatio(currentWidth, currentHeight, targetWidth, targetHeight);
        }

        public static (double Width, double Height) DownSizeAspectRatio(double currentWidth, double currentHeight, double targetWidth, double targetHeight)
        {
            if (currentWidth <= targetWidth && currentHeight <= targetHeight)
                return (currentWidth, currentHeight);
            else
                return StretchSizeAspectRatio(currentWidth, currentHeight, targetWidth, targetHeight);
        }
        #endregion

        #region MaskWhiteAsTrapsarent
        /// <summary>
        /// If a pixel is #FFFFFFFF (White), convert it to #00FFFFFF (transparent)
        /// </summary>
        /// <param name="src"></param>
        /// <returns></returns>
        public static BitmapSource MaskWhiteAsTransparent(BitmapSource src)
        {
            // [Supported BMP pixel format]
            // - BGRA32
            // - BGRX32
            // - BGR24
            // - BGR555
            // - BGR565
            // [16bit to 24bit conversion]
            // https://stackoverflow.com/questions/2442576/how-does-one-convert-16-bit-rgb565-to-24-bit-rgb888

            if (src.Format.Equals(PixelFormats.Bgra32))
            { // Pixel is 4B, B(8)-G(8)-R(8)-A(8)
                int stride = src.PixelWidth * 4;
                int pixelFourBytes = src.PixelWidth * src.PixelHeight * 4;
                byte[] srcPixels = new byte[pixelFourBytes];
                byte[] destPixels = new byte[pixelFourBytes];
                src.CopyPixels(srcPixels, stride, 0);
                src.CopyPixels(destPixels, stride, 0);

                for (int y = 0; y < src.PixelHeight; y++)
                {
                    for (int x = 0; x < src.PixelWidth; x++)
                    {
                        int i = (x + y * src.PixelWidth) * 4;

                        byte r = srcPixels[i];
                        byte g = srcPixels[i + 1];
                        byte b = srcPixels[i + 2];
                        byte a = srcPixels[i + 3];

                        if (r == 255 && g == 255 && b == 255 & a == 255)
                            destPixels[i + 3] = 0; // Max transparency
                    }
                }

                Int32Rect rect = new Int32Rect(0, 0, src.PixelWidth, src.PixelHeight);
                WriteableBitmap dest = new WriteableBitmap(src.PixelWidth, src.PixelHeight, 96, 96, PixelFormats.Bgra32, null);
                dest.WritePixels(rect, destPixels, stride, 0);
                return dest;
            }
            else if (src.Format.Equals(PixelFormats.Bgr32))
            { // Pixel is 4B, B(8)-G(8)-R(8)-X(8)
                int stride = src.PixelWidth * 4;
                int pixelFourBytes = src.PixelWidth * src.PixelHeight * 4;
                byte[] srcPixels = new byte[pixelFourBytes];
                byte[] destPixels = new byte[pixelFourBytes];
                src.CopyPixels(srcPixels, stride, 0);
                src.CopyPixels(destPixels, stride, 0);

                for (int y = 0; y < src.PixelHeight; y++)
                {
                    for (int x = 0; x < src.PixelWidth; x++)
                    {
                        int i = (x + y * src.PixelWidth) * 4;

                        byte r = srcPixels[i];
                        byte g = srcPixels[i + 1];
                        byte b = srcPixels[i + 2];
                        if (r == 255 && g == 255 && b == 255)
                            destPixels[i + 3] = 0; // Max transparency
                        else
                            destPixels[i + 3] = 255; // No transparency
                    }
                }

                Int32Rect rect = new Int32Rect(0, 0, src.PixelWidth, src.PixelHeight);
                WriteableBitmap dest = new WriteableBitmap(src.PixelWidth, src.PixelHeight, 96, 96, PixelFormats.Bgra32, null);
                dest.WritePixels(rect, destPixels, stride, 0);
                return dest;
            }
            else if (src.Format.Equals(PixelFormats.Bgr24))
            { // Pixel is 3B, B(8)-G(8)-R(8)-x(8)
                int threeStride = src.PixelWidth * 3;
                int fourStride = src.PixelWidth * 4;
                int pixelThreeBytes = src.PixelWidth * src.PixelHeight * 3;
                int pixelFourBytes = src.PixelWidth * src.PixelHeight * 4;
                byte[] srcPixels = new byte[pixelThreeBytes];
                byte[] destPixels = new byte[pixelFourBytes];
                src.CopyPixels(srcPixels, threeStride, 0);

                for (int y = 0; y < src.PixelHeight; y++)
                {
                    for (int x = 0; x < src.PixelWidth; x++)
                    {
                        int threeIdx = (x + y * src.PixelWidth) * 3;
                        byte r = srcPixels[threeIdx];
                        byte g = srcPixels[threeIdx + 1];
                        byte b = srcPixels[threeIdx + 2];
                        byte a;
                        if (r == 255 && g == 255 && b == 255)
                            a = 0; // Max transparency
                        else
                            a = 255;

                        int fourIdx = (x + y * src.PixelWidth) * 4;
                        destPixels[fourIdx] = r;
                        destPixels[fourIdx + 1] = g;
                        destPixels[fourIdx + 2] = b;
                        destPixels[fourIdx + 3] = a;
                    }
                }

                Int32Rect rect = new Int32Rect(0, 0, src.PixelWidth, src.PixelHeight);
                WriteableBitmap dest = new WriteableBitmap(src.PixelWidth, src.PixelHeight, 96, 96, PixelFormats.Bgra32, null);
                dest.WritePixels(rect, destPixels, fourStride, 0);
                return dest;
            }
            else if (src.Format.Equals(PixelFormats.Bgr555))
            { // Pixel is 2B, B(5)-G(5)-R(5)-X(1)
                int twoStride = src.PixelWidth * 2;
                int fourStride = src.PixelWidth * 4;
                int pixelTwoBytes = src.PixelWidth * src.PixelHeight * 2;
                int pixelFourBytes = src.PixelWidth * src.PixelHeight * 4;
                byte[] srcPixels = new byte[pixelTwoBytes];
                byte[] destPixels = new byte[pixelFourBytes];
                src.CopyPixels(srcPixels, twoStride, 0);

                for (int y = 0; y < src.PixelHeight; y++)
                {
                    for (int x = 0; x < src.PixelWidth; x++)
                    {
                        int twoIdx = (x + y * src.PixelWidth) * 2;

                        ushort val = BitConverter.ToUInt16(srcPixels, twoIdx);

                        // val = xRrrrrGggggBbbbb
                        byte r = (byte)((val & 0b0111110000000000) >> 10); // 0 ~ 31
                        byte g = (byte)((val & 0b0000001111100000) >> 5); // 0 ~ 31
                        byte b = (byte)(val & 0b0000000000011111); // 0 ~ 31
                        byte a;
                        if (r == 31 && g == 31 && b == 31)
                            a = 0; // Max transparency
                        else
                            a = 255;

                        // Simulate iOS vImage conversion algorithm
                        int fourIdx = (x + y * src.PixelWidth) * 4;
                        destPixels[fourIdx] = (byte)((r * 255 + 15) / 31);
                        destPixels[fourIdx + 1] = (byte)((g * 255 + 15) / 31);
                        destPixels[fourIdx + 2] = (byte)((b * 255 + 15) / 31);
                        destPixels[fourIdx + 3] = a;
                    }
                }

                Int32Rect rect = new Int32Rect(0, 0, src.PixelWidth, src.PixelHeight);
                WriteableBitmap dest = new WriteableBitmap(src.PixelWidth, src.PixelHeight, 96, 96, PixelFormats.Bgra32, null);
                dest.WritePixels(rect, destPixels, fourStride, 0);
                return dest;
            }
            else if (src.Format.Equals(PixelFormats.Bgr565))
            { // Pixel is 2B, B(5)-G(6)-R(5)
                int twoStride = src.PixelWidth * 2;
                int fourStride = src.PixelWidth * 4;
                int pixelTwoBytes = src.PixelWidth * src.PixelHeight * 2;
                int pixelFourBytes = src.PixelWidth * src.PixelHeight * 4;
                byte[] srcPixels = new byte[pixelTwoBytes];
                byte[] destPixels = new byte[pixelFourBytes];
                src.CopyPixels(srcPixels, twoStride, 0);

                for (int y = 0; y < src.PixelHeight; y++)
                {
                    for (int x = 0; x < src.PixelWidth; x++)
                    {
                        int twoIdx = (x + y * src.PixelWidth) * 2;

                        ushort val = BitConverter.ToUInt16(srcPixels, twoIdx);

                        // val = RrrrrGgggggBbbbb
                        byte r = (byte)((val & 0b1111100000000000) >> 11); // 0 ~ 31
                        byte g = (byte)((val & 0b0000011111100000) >> 5); // 0 ~ 63
                        byte b = (byte)(val & 0b0000000000011111); // 0 ~ 31
                        byte a;
                        if (r == 31 && g == 63 && b == 31)
                            a = 0; // Max transparency
                        else
                            a = 255;

                        // Simulate iOS vImage conversion algorithm
                        int fourIdx = (x + y * src.PixelWidth) * 4;
                        destPixels[fourIdx] = (byte)((r * 255 + 15) / 31);
                        destPixels[fourIdx + 1] = (byte)((g * 255 + 31) / 63);
                        destPixels[fourIdx + 2] = (byte)((b * 255 + 15) / 31);
                        destPixels[fourIdx + 3] = a;
                    }
                }

                Int32Rect rect = new Int32Rect(0, 0, src.PixelWidth, src.PixelHeight);
                WriteableBitmap dest = new WriteableBitmap(src.PixelWidth, src.PixelHeight, 96, 96, PixelFormats.Bgra32, null);
                dest.WritePixels(rect, destPixels, fourStride, 0);
                return dest;
            }
            else
            {
                return src;
            }
        }
        #endregion
    }
    #endregion
}
