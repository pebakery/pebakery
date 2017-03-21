using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Text.RegularExpressions;

// from https://github.com/Microsoft/WPF-Samples/tree/master/PerMonitorDPI/ImageScaling
namespace PEBakery.WPF
{
    // from https://github.com/Microsoft/WPF-Samples/tree/master/PerMonitorDPI/ImageScaling
    // under MIT License
    public static class ImageDpiHelper
    {
        /// <summary>
        /// Given an image, get its current DPI and choose the best source image to scale to that DPI.
        /// </summary>
        /// <param name="image">image element</param>
        /// <returns>image URL for the most appropriate scale, given DPI</returns>
        public static string GetDesiredImageUrlForDpi(Image image)
        {
            DpiScale imageScaleInfo = VisualTreeHelper.GetDpi(image);
            int bestScale = ImageDpiHelper.GetBestScale(imageScaleInfo.PixelsPerDip);

            var sourceUrl = image.Source.ToString();
            string imagePattern = Regex.Replace(sourceUrl, ".scale-[0-9]{3}.", ".scale-{0}.");

            string newImagePath = null;
            if (imagePattern != null)
            {
                newImagePath = string.Format(imagePattern, bestScale);
            }

            return newImagePath;
        }


        /// <summary>
        /// Given a target pixelsPerDip value, choose the best scale of image to use.
        /// Per the Windows team, this technique prefers scaling down, rather than up
        /// and recommends using 100, 200, and 400 scale iamges.
        /// </summary>
        /// <param name="currentPixelsPerDip"></param>
        /// <returns></returns>
        public static int GetBestScale(double currentPixelsPerDip)
        {
            int currentScale = (int)(currentPixelsPerDip * 100);
            int bestScale;

            if (currentScale > 200)
            {
                bestScale = 400;
            }
            else if (currentScale > 100)
            {
                bestScale = 200;
            }
            else
            {
                bestScale = 100;
            }

            return bestScale;
        }

        /// <summary>
        /// Updates the image's Source to the newImagePath
        /// </summary>
        /// <param name="image">image element</param>
        /// <param name="newImagePath">URI of the desired image to use</param>
        public static void UpdateImageSource(Image image, string newImagePath)
        {
            Uri uri = new Uri(newImagePath, UriKind.RelativeOrAbsolute);
            BitmapImage bitmapImage = new BitmapImage(uri);
            image.Source = bitmapImage;
        }
    }

    /// <summary>
    /// Image that has built in Dpi awareness to load appropriate scale image, and sets default size appropriately.
    /// </summary>
    public class DpiAwareImage : Image
    {
        double bestScale; // used in calculating default size

        public DpiAwareImage() : base()
        {
            this.Initialized += DpiAwareImage_Initialized;
        }

        // on initial load, ensure we are using the right scaled image, based on the DPI.
        private void DpiAwareImage_Initialized(object sender, EventArgs e)
        {
            DpiScale newDpi = VisualTreeHelper.GetDpi(sender as Visual);
            ScaleRightImage(newDpi);
        }
        
        // when DPI changes, ensure we are using the right scaled image, based on the DPI.
        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            ScaleRightImage(newDpi);
        }

        private void ScaleRightImage(DpiScale newDpi)
        {
            // update bestScale
            bestScale = ImageDpiHelper.GetBestScale(newDpi.PixelsPerDip);

            string imageUrl = ImageDpiHelper.GetDesiredImageUrlForDpi(this);
            UpdateImageSource(this, imageUrl);
        }

        public void UpdateImageSource(Image image, string newImagePath)
        {
            Uri uri = new Uri(newImagePath, UriKind.RelativeOrAbsolute);
            BitmapImage bitmapImage = new BitmapImage();
            
            bitmapImage.BeginInit();
            bitmapImage.UriSource = uri;
            bitmapImage.EndInit();

            image.Source = bitmapImage;

            if (!bitmapImage.IsDownloading)
            {
                SetScaledSizeForImageElement(bitmapImage);
            }
            else
            {
                bitmapImage.DownloadCompleted += BitmapImage_DownloadCompleted;
            }
        }

        private void BitmapImage_DownloadCompleted(object sender, EventArgs e)
        {
            BitmapImage bitmapImage = sender as BitmapImage;
            SetScaledSizeForImageElement(bitmapImage);
            bitmapImage.DownloadCompleted -= BitmapImage_DownloadCompleted;
        }

        // based on the scale of the image used, adjust the default value of width/height.
        private void SetScaledSizeForImageElement(BitmapImage bitmapImage)
        {
            this.SetCurrentValue(Image.WidthProperty, bitmapImage.PixelWidth * 100 / bestScale);
            this.SetCurrentValue(Image.HeightProperty, bitmapImage.PixelHeight * 100 / bestScale);
        }
    }
}

