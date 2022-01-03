/* Copyright (C) 2021 - Mywk.Net
 * Licensed under the EUPL, Version 1.2
 * You may obtain a copy of the Licence at: https://joinup.ec.europa.eu/community/eupl/og_page/eupl
 * Unless required by applicable law or agreed to in writing, software distributed under the Licence is distributed on an "AS IS" basis, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 */
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;
using Tesseract;

namespace NWSMM
{
    public class OCR
    {
        // Tesseract engine
        private TesseractEngine _tesseractEngine = null;

        /// <summary>
        /// OCR related stuff
        /// </summary>
        public OCR()
        {
            _tesseractEngine = new TesseractEngine(@"./tessdata", "complexeng", EngineMode.Default);
            _tesseractEngine.SetVariable("tessedit_char_whitelist", "[]0123456789,. ");
        }

        /// <summary>
        /// This holds the current screenshot
        /// </summary>
        MemoryStream _bitmapMemoryStream = null;

        /// <summary>
        /// Convert a Bitmap to a BitmapImage
        /// </summary>
        /// <param name="src"></param>
        /// <returns></returns>
        public BitmapImage BitmapToBitmapImage(Bitmap src)
        {
            if (_bitmapMemoryStream != null)
            {
                _bitmapMemoryStream.Close();
                _bitmapMemoryStream = null;
            }

            _bitmapMemoryStream = new MemoryStream();
            ((System.Drawing.Bitmap)src).Save(_bitmapMemoryStream, System.Drawing.Imaging.ImageFormat.Bmp);
            BitmapImage image = new BitmapImage();
            image.BeginInit();
            _bitmapMemoryStream.Seek(0, SeekOrigin.Begin);
            image.StreamSource = _bitmapMemoryStream;
            image.EndInit();
            return image;
        }

        /// <summary>
        /// Processes a bitmap, changing everything within the threshold to black and the remaining of the bitmap to white
        /// </summary>
        /// <param name="bmp"></param>
        public void BitmapPreProcessingByColorMatch(Bitmap bmp, int rMin, int gMin, int bMin, int rMax, int gMax,
            int bMax)
        {
            var targetColor = Color.FromArgb(0, 0, 0).ToArgb();
            var nonTargetColor = Color.FromArgb(255, 255, 255).ToArgb();

            // Lock the array for direct access
            var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppPArgb);

            unsafe
            {
                // Store the length so we don't have to recalculate it
                var length = (int*)data.Scan0 + bmp.Height * bmp.Width;

                for (var p = (int*)data.Scan0; p < length; p++)
                {
                    // Get the rgb values
                    var r = ((*p >> 16) & 255);
                    var g = ((*p >> 8) & 255);
                    var b = ((*p >> 0) & 255);

                    // Compare it against the threshold
                    if (r >= rMin && g >= gMin && b >= bMin && r <= rMax && g <= gMax && b <= bMax)
                        *p = nonTargetColor;
                    else
                        *p = targetColor;
                }

                // Unlock the bitmap
                bmp.UnlockBits(data);
            }
        }


        /// <summary>
        /// Processes a bitmap to yellow with the given range, changing everything within the threshold to black and the remaining of the bitmap to white
        /// </summary>
        /// <param name="bmp"></param>
        /// <param name="range"></param>
        public void BitmapPreProcessingByYellowRange(Bitmap bmp, int range)
        {
            var targetColor = Color.FromArgb(0, 0, 0).ToArgb();
            var nonTargetColor = Color.FromArgb(255, 255, 255).ToArgb();

            // Lock the array for direct access
            var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppPArgb);

            unsafe
            {
                // Store the length so we don't have to recalculate it
                var length = (int*)data.Scan0 + bmp.Height * bmp.Width;

                for (var p = (int*)data.Scan0; p < length; p++)
                {
                    // Get the rgb values
                    var r = ((*p >> 16) & 255);
                    var g = ((*p >> 8) & 255);
                    var b = ((*p >> 0) & 255);

                    // Convert RGB to HSL
                    Color c = Color.FromArgb(255, r, g, b);

                    float hueC = c.GetHue();
                    float e = 1.5f * range;
                    float hueY = Color.Yellow.GetHue();
                    float delta = hueC - hueY;
                    bool ok = Math.Abs(delta) < e;

                    // Compare it against the threshold
                    if (ok)
                        *p = nonTargetColor;
                    else
                        *p = targetColor;
                }

                // Unlock the bitmap
                bmp.UnlockBits(data);
            }
        }

        /// <summary>
        /// Processes a bitmap, changing everything within the threshold to black and the remaining of the bitmap to white
        /// </summary>
        /// <param name="bmp"></param>
        /// <param name="colorToMatch"></param>
        /// <param name="threshold"></param>
        public void BitmapPreProcessingByThreshold(Bitmap bmp, Color colorToMatch, int threshold)
        {
            var targetColor = Color.FromArgb(0, 0, 0).ToArgb();
            var nonTargetColor = Color.FromArgb(255, 255, 255).ToArgb();

            // Lock the array for direct access
            var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppPArgb);

            // Convert the source to RGB
            int sR = colorToMatch.R, sG = colorToMatch.G, sB = colorToMatch.B;

            unsafe
            {
                // Store the length so we don't have to recalculate it
                var length = (int*)data.Scan0 + bmp.Height * bmp.Width;

                for (var p = (int*)data.Scan0; p < length; p++)
                {

                    // Get the rgb Distance
                    var r = ((*p >> 16) & 255) - sR;
                    var g = ((*p >> 8) & 255) - sG;
                    var b = ((*p >> 0) & 255) - sB;

                    // Compare it against the threshold
                    if (r * r + g * g + b * b > threshold)
                        *p = nonTargetColor;
                    else
                        *p = targetColor;
                }

                // Unlock the bitmap
                bmp.UnlockBits(data);
            }
        }

        

        public string ProcessImageToText(Bitmap bmp)
        {
            if (bmp == null)
                return "";

            using var eng = _tesseractEngine.Process(bmp);

            return eng.GetText();
        }

    }
}
