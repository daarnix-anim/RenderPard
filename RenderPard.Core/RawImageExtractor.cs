using System;
using System.IO;
using ImageMagick;

namespace RenderPard.Core;

public static class RawImageExtractor
{
    /// <summary>
    /// Reads a RAW or HEIC image using Magick.NET and saves it as a temporary PNG file.
    /// </summary>
    /// <param name="filePath">The path to the input RAW/HEIC file.</param>
    /// <returns>The path to the temporary PNG file.</returns>
    public static string ExtractToTempPng(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        // Create a temporary file path
        string tempPngPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.png");

        try
        {
            // MagickImage automatically reads RAW and applies default profile/Bayer decoding
            using var image = new MagickImage(filePath);
            
            // Auto orient based on EXIF
            image.AutoOrient();
            
            // Strip profiles and EXIF except color profiles if needed, but keeping it simple:
            // Format to PNG
            image.Format = MagickFormat.Png;
            
            // Save to temp
            image.Write(tempPngPath);

            return tempPngPath;
        }
        catch (Exception ex)
        {
            // Clean up the temp file if it somehow was created but writing failed
            if (File.Exists(tempPngPath))
            {
                try { File.Delete(tempPngPath); } catch { }
            }
            throw new Exception($"Failed to decode image '{Path.GetFileName(filePath)}' with Magick.NET: {ex.Message}", ex);
        }
    }
}
