using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using PdfiumViewer;

namespace RenderPard.Core;

public static class PdfExtractor
{
    static PdfExtractor()
    {
        NativeLibrary.SetDllImportResolver(typeof(PdfiumViewer.PdfDocument).Assembly, (libraryName, assembly, searchPath) =>
        {
            if (libraryName.Equals("pdfium.dll", StringComparison.OrdinalIgnoreCase))
            {
                string arch = Environment.Is64BitProcess ? "x64" : "x86";
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, arch, "pdfium.dll");
                if (File.Exists(path))
                {
                    return NativeLibrary.Load(path);
                }
            }
            return IntPtr.Zero;
        });
    }
    /// <summary>
    /// Extracts all pages from a PDF or AI (PDF-compatible) file into a temporary directory as PNGs.
    /// Returns a list of paths to the extracted images.
    /// </summary>
    public static List<string> ExtractPages(string pdfFilePath)
    {
        var extractedFiles = new List<string>();
        
        string tempFolder = Path.Combine(Path.GetTempPath(), "RenderPard_Extracted", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempFolder);

        using (var document = PdfDocument.Load(pdfFilePath))
        {
            for (int i = 0; i < document.PageCount; i++)
            {
                // Determine page size in points to calculate a reasonable resolution
                // 1 point = 1/72 inch. For 300 DPI, we multiply by (300/72) = 4.16
                var size = document.PageSizes[i];
                int width = (int)(size.Width * 4.16f);
                int height = (int)(size.Height * 4.16f);

                using (var image = document.Render(i, width, height, 300, 300, true))
                {
                    string outFile = Path.Combine(tempFolder, $"page_{i + 1}.png");
                    image.Save(outFile, ImageFormat.Png);
                    extractedFiles.Add(outFile);
                }
            }
        }

        return extractedFiles;
    }
}
