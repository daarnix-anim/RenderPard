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
            { "youtube", "https://img.icons8.com/ios-filled/256/FFFFFF/youtube-play.png" },
            { "tiktok", "https://img.icons8.com/ios-filled/256/FFFFFF/tiktok.png" },
            { "instagram", "https://img.icons8.com/ios-filled/256/FFFFFF/instagram-new.png" },
            { "vk", "https://img.icons8.com/ios-filled/256/FFFFFF/vk-com.png" },
            { "telegram", "https://img.icons8.com/ios-filled/256/FFFFFF/telegram-app.png" },
            { "vimeo", "https://img.icons8.com/ios-filled/256/FFFFFF/vimeo.png" },
            { "whatsapp", "https://img.icons8.com/ios-filled/256/FFFFFF/whatsapp.png" },
            { "discord", "https://img.icons8.com/ios-filled/256/FFFFFF/discord-logo.png" },
            { "twitch", "https://img.icons8.com/ios-filled/256/FFFFFF/twitch.png" },
            { "video", "https://img.icons8.com/ios-filled/256/FFFFFF/video.png" },
            { "image", "https://img.icons8.com/ios-filled/256/FFFFFF/image.png" },
            { "audio", "https://img.icons8.com/ios-filled/256/FFFFFF/musical-notes.png" },
            { "tv", "https://img.icons8.com/ios-filled/256/FFFFFF/tv.png" },
            { "camera", "https://img.icons8.com/ios-filled/256/FFFFFF/camera.png" },
            { "settings", "https://img.icons8.com/ios-filled/256/FFFFFF/settings.png" }
        };

        string outDir = Path.Combine("RenderPard.UI", "Icons");
        Directory.CreateDirectory(outDir);

        using var client = new HttpClient();
        foreach (var icon in icons)
        {
            try
            {
                byte[] pngBytes = await client.GetByteArrayAsync(icon.Value);
                string outPath = Path.Combine(outDir, icon.Key + ".ico");
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
                Console.WriteLine($"Generated {icon.Key}.ico");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error {icon.Key}: {ex.Message}");
            }
        }
    }
}
