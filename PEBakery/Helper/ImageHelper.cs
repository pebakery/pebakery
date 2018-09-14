/*
    Copyright (C) 2016-2018 Hajin Jang
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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MahApps.Metro.IconPacks;
using Svg;

namespace PEBakery.Helper
{
    #region ImageHelper
    public static class ImageHelper
    {
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

        public static BitmapImage BitmapToBitmapImage(Bitmap srcBmp)
        {
            BitmapImage bitmap = new BitmapImage();
            using (MemoryStream ms = new MemoryStream())
            {
                srcBmp.Save(ms, ImageFormat.Bmp);
                ms.Position = 0;

                bitmap.BeginInit();
                bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
            }
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

        public static (int Width, int Height) GetSvgSize(Stream stream)
        {
            SvgDocument svgDoc = SvgDocument.Open<SvgDocument>(stream);
            SizeF size = svgDoc.GetDimensions();
            return ((int)size.Width, (int)size.Height);
        }

        public static BitmapImage SvgToBitmapImage(Stream stream)
        {
            SvgDocument svgDoc = SvgDocument.Open<SvgDocument>(stream);
            return ImageHelper.ToBitmapImage(svgDoc.Draw());
        }

        public static BitmapImage SvgToBitmapImage(Stream stream, double width, double height, bool keepAspectRatio = true)
        {
            return SvgToBitmapImage(stream, (int)Math.Round(width), (int)Math.Round(height), keepAspectRatio);
        }

        public static BitmapImage SvgToBitmapImage(Stream stream, int width, int height, bool keepAspectRatio = true)
        {
            SvgDocument svgDoc = SvgDocument.Open<SvgDocument>(stream);
            if (keepAspectRatio)
            {
                SizeF size = svgDoc.GetDimensions();
                float imageRatio = size.Width / size.Height;
                float drawRatio = (float)width / height;

                if (Math.Abs(imageRatio - drawRatio) < float.Epsilon) // Ratio is equal, do not touch it 
                    return ImageHelper.ToBitmapImage(svgDoc.Draw(width, height));
                else if (imageRatio < drawRatio)
                    return ImageHelper.ToBitmapImage(svgDoc.Draw((int)Math.Round(width / imageRatio, 0), height));
                else // if (drawRatio < imageRatio) 
                    return ImageHelper.ToBitmapImage(svgDoc.Draw(width, (int)Math.Round(height / imageRatio, 0)));
            }
            return ImageHelper.ToBitmapImage(svgDoc.Draw(width, height));
        }

        public static BitmapImage ToBitmapImage(Bitmap bitmap)
        {
            using (MemoryStream mem = new MemoryStream())
            {
                bitmap.Save(mem, ImageFormat.Png);
                mem.Position = 0;

                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = mem;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();

                return bitmapImage;
            }
        }

        public static ImageBrush SvgToImageBrush(Stream stream)
        {
            return ImageHelper.BitmapImageToImageBrush(ImageHelper.SvgToBitmapImage(stream));
        }
        public static ImageBrush SvgToImageBrush(Stream stream, double width, double height)
        {
            return ImageHelper.BitmapImageToImageBrush(ImageHelper.SvgToBitmapImage(stream, width, height));
        }

        public static ImageBrush SvgToImageBrush(Stream stream, int width, int height)
        {
            return ImageHelper.BitmapImageToImageBrush(ImageHelper.SvgToBitmapImage(stream, width, height));
        }

        public static ImageBrush BitmapImageToImageBrush(BitmapImage bitmap)
        {
            return new ImageBrush { ImageSource = bitmap };
        }
    }
    #endregion
}
