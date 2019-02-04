/*
    Copyright (C) 2016-2019 Hajin Jang
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
        public enum ImageType
        {
            Bmp, Jpg, Png, Gif, Ico, Svg
        }

        public static readonly ReadOnlyDictionary<string, ImageType> ImageTypeDict = new ReadOnlyDictionary<string, ImageType>(
            new Dictionary<string, ImageType>(StringComparer.OrdinalIgnoreCase)
            {
                { ".bmp", ImageType.Bmp },
                { ".jpg", ImageType.Jpg },
                { ".png", ImageType.Png },
                { ".gif", ImageType.Gif },
                { ".ico", ImageType.Ico },
                { ".svg", ImageType.Svg },
            });

        /// <summary>
        /// Return true if success
        /// </summary>
        /// <param name="path"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool GetImageType(string path, out ImageType type)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            string ext = Path.GetExtension(path);
            if (ImageTypeDict.ContainsKey(ext))
            {
                type = ImageTypeDict[ext];
                return true;
            }
            else
            {
                type = ImageType.Bmp; // Dummy
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
            FileSvgReader reader = new FileSvgReader(new WpfDrawingSettings
            {
                CultureInfo = CultureInfo.InvariantCulture,
                IncludeRuntime = true,
            });
            reader.Read(stream);
            return reader.Drawing;
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
        #endregion

        #region MaskWhiteAsTrapsarent
        private static void WriteToBrga32Bitmap(byte[] pixels, int idx, byte r, byte g, byte b, byte a)
        {
            pixels[idx] = r;
            pixels[idx + 1] = g;
            pixels[idx + 2] = b;
            pixels[idx + 3] = a;
        }

        private static (byte r, byte g, byte b, byte a) ReadFromBrga32Bitmap(byte[] pixels, int idx)
        {
            byte r = pixels[idx];
            byte g = pixels[idx + 1];
            byte b = pixels[idx + 2];
            byte a = pixels[idx + 3];

            return (r, g, b, a);
        }

        private static (byte r, byte g, byte b) ReadFromBrg24Bitmap(byte[] pixels, int idx)
        {
            byte r = pixels[idx];
            byte g = pixels[idx + 1];
            byte b = pixels[idx + 2];

            return (r, g, b);
        }

        /// <summary>
        /// If a pixel is #FFFFFFFF (White), convert it to #00FFFFFF (transparent)
        /// </summary>
        /// <param name="src"></param>
        /// <returns></returns>
        public static BitmapSource MaskWhiteAsTransparent(BitmapSource src)
        {
            if (src.Format.Equals(PixelFormats.Bgr24))
            {
                int stride = src.PixelWidth * 3;
                byte[] srcPixels = new byte[stride * src.PixelHeight];
                src.CopyPixels(srcPixels, stride, 0);

                WriteableBitmap dest =
                    new WriteableBitmap(src.PixelWidth, src.PixelHeight, 96, 96, PixelFormats.Bgra32, null);
                byte[] destPixels = new byte[src.PixelWidth * src.PixelHeight * 4];
                for (int y = 0; y < src.PixelHeight; y++)
                {
                    for (int x = 0; x < src.PixelWidth; x++)
                    {
                        int rgb24idx = (x + y * src.PixelWidth) * 3;
                        int rgba32idx = (x + y * src.PixelWidth) * 4;

                        byte a = 255;
                        (byte r, byte g, byte b) = ReadFromBrg24Bitmap(srcPixels, rgb24idx);
                        if (r == 255 && g == 255 && b == 255)
                            a = 0; // Max transparency
                        WriteToBrga32Bitmap(destPixels, rgba32idx, r, g, b, a);
                    }
                }

                Int32Rect rect = new Int32Rect(0, 0, src.PixelWidth, src.PixelHeight);
                dest.WritePixels(rect, destPixels, src.PixelWidth * 4, 0);
                return dest;
            }

            if (src.Format.Equals(PixelFormats.Bgra32))
            {
                int stride = src.PixelWidth * 4;
                byte[] srcPixels = new byte[stride * src.PixelHeight];
                src.CopyPixels(srcPixels, stride, 0);

                WriteableBitmap dest =
                    new WriteableBitmap(src.PixelWidth, src.PixelHeight, 96, 96, PixelFormats.Bgra32, null);
                byte[] destPixels = new byte[src.PixelWidth * src.PixelHeight * 4];
                for (int y = 0; y < src.PixelHeight; y++)
                {
                    for (int x = 0; x < src.PixelWidth; x++)
                    {
                        int rgba32idx = (x + y * src.PixelWidth) * 4;

                        (byte r, byte g, byte b, byte a) = ReadFromBrga32Bitmap(srcPixels, rgba32idx);
                        if (r == 255 && g == 255 && b == 255 & a == 255)
                            a = 0; // Max transparency
                        WriteToBrga32Bitmap(destPixels, rgba32idx, r, g, b, a);
                    }
                }

                Int32Rect rect = new Int32Rect(0, 0, src.PixelWidth, src.PixelHeight);
                dest.WritePixels(rect, destPixels, src.PixelWidth * 4, 0);
                return dest;
            }

            return src;
        }
        #endregion
    }
    #endregion
}
