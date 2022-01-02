using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Forms;

namespace NWSMM
{
    public static class ScreenshotUtil
    {

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hwnd, ref WindowRect rectangle);

        private const string ProcessName = "NewWorld"; // NewWorld, mpc-hc64

        public enum ScreenshotType
        {
            Full,
            Position
        }


        public struct WindowRect
        {
            public int Left { get; set; }
            public int Top { get; set; }
            public int Right { get; set; }
            public int Bottom { get; set; }
        }


        private static IntPtr GetWindowHandle()
        {
            Process[] processes = Process.GetProcessesByName(ProcessName);

            if (processes.Length == 0)
                return IntPtr.Zero;

            Process proc = processes[0];
            IntPtr ptr = proc.MainWindowHandle;

            return ptr;
        }

        private const int _xOffset = 265;
        private const int _yOffset = 20;
        private const int _posWidth = 277;
        private const int _posHeight = 16;

        public static Bitmap GetScreenshot(ScreenshotType screenshotType)
        {
            var windowHandle = GetWindowHandle();

            if (windowHandle != IntPtr.Zero)
            {
                WindowRect windowPosition = new WindowRect();
                GetWindowRect(windowHandle, ref windowPosition);

                double screenX = Screen.PrimaryScreen.Bounds.Left + windowPosition.Left;
                double screenY = Screen.PrimaryScreen.Bounds.Top + windowPosition.Top;

                // Adjust position depending on the screenshot type
                if (screenshotType == ScreenshotType.Position)
                {
                    // Width and Height set by percentage of the screen size
                    screenX = windowPosition.Right - _xOffset;
                    screenY = _yOffset;
                }

                //Create a new bitmap.
                Bitmap original = new Bitmap((int)_posWidth, (int)_posHeight);

                // Create a graphics object from the bitmap.
                var gfxScreenshot = Graphics.FromImage(original);

                try
                {
                    gfxScreenshot.CopyFromScreen((int)screenX, (int)screenY, 0, 0, original.Size, CopyPixelOperation.SourceCopy);
                    gfxScreenshot.Save();
                }
                catch (Exception e)
                {
                    return null;
                }


                return original;
            }

            return null;
        }
    }
}
