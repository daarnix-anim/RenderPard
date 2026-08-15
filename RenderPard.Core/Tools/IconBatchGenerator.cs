using System;
using System.IO;
using System.Text.RegularExpressions;
using ImageMagick;

namespace RenderPard.Core.Tools;

public static class IconBatchGenerator
{
    public static void GenerateAll(string assetsDir, string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        
        var svgFiles = Directory.GetFiles(assetsDir, "*.svg");

        foreach (var svgPath in svgFiles)
        {
            string iconName = Path.GetFileNameWithoutExtension(svgPath);
            string svgContent = File.ReadAllText(svgPath);

            // 1. Generate White Icon
            string whiteSvg = Regex.Replace(svgContent, @"#([0-9a-fA-F]{6}|[0-9a-fA-F]{3})", "#FFFFFF");
            whiteSvg = whiteSvg.Replace("stroke=\"white\"", "stroke=\"#FFFFFF\"").Replace("fill=\"white\"", "fill=\"#FFFFFF\"");
            string whiteIcoPath = Path.Combine(outputDir, $"{iconName}.ico");
            ConvertSvgToIco(whiteSvg, whiteIcoPath);

            // 2. Generate Dark Icon
            string darkSvg = Regex.Replace(svgContent, @"#([0-9a-fA-F]{6}|[0-9a-fA-F]{3})", "#1E1E1E");
            darkSvg = darkSvg.Replace("stroke=\"white\"", "stroke=\"#1E1E1E\"").Replace("fill=\"white\"", "fill=\"#1E1E1E\"");
            darkSvg = darkSvg.Replace("stroke=\"#FFFFFF\"", "stroke=\"#1E1E1E\"").Replace("fill=\"#FFFFFF\"", "fill=\"#1E1E1E\"");
            string darkIcoPath = Path.Combine(outputDir, $"{iconName}_dark.ico");
            ConvertSvgToIco(darkSvg, darkIcoPath);
        }
    }

    private static void ConvertSvgToIco(string svgContent, string outputPath)
    {
        var readSettings = new MagickReadSettings
        {
            Format = MagickFormat.Svg,
            Width = 256,
            Height = 256,
            BackgroundColor = MagickColors.Transparent
        };

        using var collection = new MagickImageCollection();
        uint[] sizes = { 16, 24, 32, 48, 64 };
        
        foreach (uint size in sizes)
        {
            var img = new MagickImage(System.Text.Encoding.UTF8.GetBytes(svgContent), readSettings);
            img.Resize(size, size);
            collection.Add(img);
        }

        collection.Write(outputPath, MagickFormat.Ico);
    }
}
