using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RenderPard.UI
{
    public static class IconGenerator
    {
        private const int IconSize = 64;

        public static readonly string[] AvailableIcons = 
        {
            "Default", "Telegram", "Demo", 
            "MP4", "GIF", "WEBM", "MOV", "AVI",
            "JPG", "PNG", "WEBP",
            "Video_Red", "Video_Blue", "Video_Green", "Image_Yellow"
        };

        public static void EnsureIconsExist(IEnumerable<string> iconsToEnsure, string exeDir)
        {
            string iconsDir = Path.Combine(exeDir, "Icons");
            
            foreach (var iconName in iconsToEnsure)
            {
                if (string.IsNullOrEmpty(iconName) || iconName == "Default" || iconName == "Telegram" || iconName == "Demo")
                    continue; // Built-in icons, should be shipped with app
                    
                if (Path.IsPathRooted(iconName) && File.Exists(iconName))
                    continue; // External icon file
                    
                if (File.Exists(Path.Combine(iconsDir, iconName + ".ico")))
                    continue; // It exists in the Icons folder natively, no need to generate it

                string safeName = string.Join("_", iconName.Split(Path.GetInvalidFileNameChars()));
                string targetPath = Path.Combine(exeDir, $"icon_{safeName}.ico");

                if (!File.Exists(targetPath))
                {
                    GenerateIcon(iconName, targetPath);
                }
            }
        }

        public static BitmapSource GetIconImageSource(string iconName)
        {
            BitmapSource? result = null;
            Application.Current.Dispatcher.Invoke(() =>
            {
                var visual = new DrawingVisual();
                using (var ctx = visual.RenderOpen())
                {
                    // Default settings
                    Color topColor = Color.FromRgb(41, 128, 185);
                    Color bottomColor = Color.FromRgb(109, 213, 250);
                    string text = iconName;

                    // Configure based on name
                    if (iconName.StartsWith("Video_") || new[] { "MP4", "GIF", "WEBM", "MOV", "AVI" }.Contains(iconName))
                    {
                        topColor = Color.FromRgb(142, 68, 173); // Purple
                        bottomColor = Color.FromRgb(52, 152, 219); // Blue
                    }
                    if (iconName.StartsWith("Image_") || new[] { "JPG", "PNG", "WEBP" }.Contains(iconName))
                    {
                        topColor = Color.FromRgb(230, 126, 34); // Orange
                        bottomColor = Color.FromRgb(241, 196, 15); // Yellow
                    }

                    if (iconName == "Video_Red") { topColor = Color.FromRgb(192, 57, 43); bottomColor = Color.FromRgb(231, 76, 60); text = "REC"; }
                    else if (iconName == "Video_Blue") { topColor = Color.FromRgb(41, 128, 185); bottomColor = Color.FromRgb(52, 152, 219); text = "VID"; }
                    else if (iconName == "Video_Green") { topColor = Color.FromRgb(39, 174, 96); bottomColor = Color.FromRgb(46, 204, 113); text = "PLAY"; }
                    else if (iconName == "Image_Yellow") { topColor = Color.FromRgb(243, 156, 18); bottomColor = Color.FromRgb(241, 196, 15); text = "IMG"; }
                    
                    if (text.Length > 4) text = text.Substring(0, 4); // Keep it short

                    // Draw Background
                    var brush = new LinearGradientBrush(topColor, bottomColor, new Point(0, 0), new Point(0, 1));
                    var rect = new Rect(0, 0, IconSize, IconSize);
                    ctx.DrawRoundedRectangle(brush, null, rect, 16, 16);

                    // Draw Text
                    var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
                    var formattedText = new FormattedText(
                        text,
                        System.Globalization.CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        IconSize * 0.35,
                        Brushes.White,
                        VisualTreeHelper.GetDpi(visual).PixelsPerDip);

                    var textPos = new Point(
                        (IconSize - formattedText.Width) / 2,
                        (IconSize - formattedText.Height) / 2);

                    ctx.DrawText(formattedText, textPos);
                }

                var rtb = new RenderTargetBitmap(IconSize, IconSize, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(visual);
                rtb.Freeze();
                result = rtb;
            });
            return result!;
        }

        private static void GenerateIcon(string iconName, string outputPath)
        {
            var rtb = GetIconImageSource(iconName);
            
            Application.Current.Dispatcher.Invoke(() =>
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));

                using (var ms = new MemoryStream())
                {
                    encoder.Save(ms);
                    byte[] pngBytes = ms.ToArray();

                    using (var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                    using (var bw = new BinaryWriter(fs))
                    {
                        // ICO Header
                        bw.Write((short)0); // Reserved
                        bw.Write((short)1); // Type: 1 for ICO
                        bw.Write((short)1); // Count

                        // ICO Directory Entry
                        bw.Write((byte)(IconSize >= 256 ? 0 : IconSize)); // Width
                        bw.Write((byte)(IconSize >= 256 ? 0 : IconSize)); // Height
                        bw.Write((byte)0);  // Colors
                        bw.Write((byte)0);  // Reserved
                        bw.Write((short)1); // Color Planes
                        bw.Write((short)32);// BPP
                        bw.Write((int)pngBytes.Length); // Size of image data
                        bw.Write((int)22);  // Offset of image data (header + directory)

                        // PNG Data
                        bw.Write(pngBytes);
                    }
                }
            });
        }
    }
}
