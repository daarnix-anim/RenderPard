using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        var icons = new System.Collections.Generic.Dictionary<string, string>
        {
            // Orange / Amber colored audio icons for high-visibility differentiation in Context Menu
            { "audio_color", "https://img.icons8.com/ios-filled/256/FF9800/musical-notes.png" },
            { "fmt_mp3_color", "https://img.icons8.com/ios-filled/256/FF9800/mp3.png" },
            { "fmt_wav_color", "https://img.icons8.com/ios-filled/256/FF9800/wav.png" },
            { "fmt_flac_color", "https://img.icons8.com/ios-filled/256/FF9800/flac.png" },
            { "fmt_aac_color", "https://img.icons8.com/ios-filled/256/FF9800/aac.png" },
            { "fmt_ogg_color", "https://img.icons8.com/ios-filled/256/FF9800/ogg.png" },
            { "tv_color", "https://img.icons8.com/ios-filled/256/FF9800/tv.png" }
        };

        string[] targetDirs = new[]
        {
            Path.Combine("RenderPard.UI", "Icons"),
            Path.Combine("PublishOutput", "Icons")
        };

        foreach (var dir in targetDirs)
        {
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        }

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");

        foreach (var icon in icons)
        {
            try
            {
                byte[] pngBytes = await client.GetByteArrayAsync(icon.Value);
                foreach (var dir in targetDirs)
                {
                    string outPath = Path.Combine(dir, icon.Key + ".ico");
                    using (var fs = new FileStream(outPath, FileMode.Create))
                    using (var bw = new BinaryWriter(fs))
                    {
                        bw.Write((short)0);
                        bw.Write((short)1);
                        bw.Write((short)1);
                        bw.Write((byte)0); // width 256
                        bw.Write((byte)0); // height 256
                        bw.Write((byte)0);
                        bw.Write((byte)0);
                        bw.Write((short)1);
                        bw.Write((short)32);
                        bw.Write((int)pngBytes.Length);
                        bw.Write((int)22);
                        bw.Write(pngBytes);
                    }
                    Console.WriteLine($"Generated {outPath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error {icon.Key}: {ex.Message}");
            }
        }
    }
}
