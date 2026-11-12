using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Reflection;

namespace BrightnessControl {
    internal static class IconRenderer {
        private static readonly int[] IconSizes = new int[] { 16, 32, 48 };
        private static readonly object cacheLock = new object();
        private static Bitmap cachedDarkBase;
        private static Bitmap cachedLightBase;
        private static Color cachedDarkColor;
        private static Color cachedLightColor;

        public static Bitmap GetResourceBitmap(string resourceName) {
            Assembly asm = Assembly.GetExecutingAssembly();
            using (Stream s = asm.GetManifestResourceStream(resourceName)) {
                if (s == null) return null;
                using (Bitmap temp = new Bitmap(s)) {
                    return new Bitmap(temp);
                }
            }
        }

        public static Bitmap GetAppIconBitmap() {
            return GetResourceBitmap("BrightnessControl.Resources.Icon.png");
        }

        public static Icon CreateTrayIcon(int brightnessPercent, bool lightTaskbar) {
            Bitmap baseBmp;
            Color rayColor;
            lock (cacheLock) {
                if (lightTaskbar) {
                    if (cachedLightBase == null) {
                        cachedLightBase = GetResourceBitmap("BrightnessControl.Resources.IconLight.png");
                        if (cachedLightBase != null) cachedLightColor = cachedLightBase.GetPixel(cachedLightBase.Width / 2, 2);
                    }
                    baseBmp = cachedLightBase;
                    rayColor = cachedLightColor;
                } else {
                    if (cachedDarkBase == null) {
                        cachedDarkBase = GetResourceBitmap("BrightnessControl.Resources.IconDark.png");
                        if (cachedDarkBase != null) cachedDarkColor = cachedDarkBase.GetPixel(cachedDarkBase.Width / 2, 2);
                    }
                    baseBmp = cachedDarkBase;
                    rayColor = cachedDarkColor;
                }
                if (baseBmp == null) return null;
                using (Bitmap workBmp = new Bitmap(baseBmp)) {
                    AdjustIconLook(workBmp, rayColor, brightnessPercent / 100f);
                    return CreateIconFromBitmap(workBmp, IconSizes);
                }
            }
        }

        private static void AdjustIconLook(Bitmap src, Color rayColor, float percentage) {
            if (percentage < 0f) percentage = 0f;
            if (percentage > 1f) percentage = 1f;

            int diameter = 57;
            Rectangle rect = new Rectangle(
                (src.Width - diameter) / 2,
                (src.Height - diameter) / 2,
                diameter,
                diameter
            );

            using (Graphics g = Graphics.FromImage(src))
            using (SolidBrush brush = new SolidBrush(rayColor))
            using (Pen pen = new Pen(rayColor, 5f)) {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                g.DrawEllipse(pen, rect);

                rect.Inflate(-10, -10);
                int fillWidth = (int)Math.Round(percentage * rect.Width);
                if (fillWidth > 0) {
                    g.SetClip(new Rectangle(rect.Left, 0, fillWidth, src.Height));
                    g.FillEllipse(brush, rect);
                }
            }
        }

        private static Icon CreateIconFromBitmap(Bitmap img, int[] sizes) {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms)) {
                bw.Write((ushort)0);
                bw.Write((ushort)1);
                bw.Write((ushort)sizes.Length);

                int dataOffset = 6 + 16 * sizes.Length;
                List<byte[]> chunks = new List<byte[]>(sizes.Length);

                foreach (int size in sizes) {
                    byte[] pngBytes = ResizeToPngBytes(img, size, size);
                    chunks.Add(pngBytes);

                    bw.Write((byte)(size >= 256 ? 0 : size));
                    bw.Write((byte)(size >= 256 ? 0 : size));
                    bw.Write((byte)0);
                    bw.Write((byte)0);
                    bw.Write((ushort)1);
                    bw.Write((ushort)Image.GetPixelFormatSize(img.PixelFormat));
                    bw.Write(pngBytes.Length);
                    bw.Write(dataOffset);

                    dataOffset += pngBytes.Length;
                }

                foreach (byte[] chunk in chunks) {
                    bw.Write(chunk);
                }
                bw.Flush();
                ms.Seek(0, SeekOrigin.Begin);
                return new Icon(ms);
            }
        }

        private static byte[] ResizeToPngBytes(Bitmap src, int width, int height) {
            using (Bitmap resized = new Bitmap(width, height))
            using (Graphics g = Graphics.FromImage(resized))
            using (MemoryStream ms = new MemoryStream()) {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.DrawImage(src, 0, 0, width, height);
                resized.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }
    }
}
