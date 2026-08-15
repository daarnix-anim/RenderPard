using System;
using RenderPard.Core.Models;

namespace RenderPard.Core;

public static class WebPreCalculator
{
    // The target is to fit within max size (e.g. 18MB).
    // File Size (MB) = (Video Bitrate + Audio Bitrate) * Duration / 8192 + Container Overhead
    // Or simpler: Total Bitrate (kbps) = (Target Size in MB * 8192) / Duration
    
    public static int CalculateVideoBitrateKbps(TranscodeTask task, Preset preset)
    {
        if (task.DurationSeconds <= 0)
        {
            return preset.TargetVideoBitrateKbps; // fallback
        }

        // We want a safe margin. Spec: "до 5-10% превышение допускается", but we want to target strictly under 18MB.
        // Let's use 95% of max size for calculation to provide a 5% safety margin.
        double targetSizeMb = preset.MaxSizeMb * 0.95; 

        // Total target bitrate in kbps
        double totalBitrateKbps = (targetSizeMb * 8192) / task.DurationSeconds;

        // Container overhead (approx 2%)
        totalBitrateKbps *= 0.98;

        int audioBitrateKbps = 0;
        if (task.HasAudio && preset.AudioMode == AudioMode.Encode)
        {
            audioBitrateKbps = preset.AudioBitrateKbps;
        }
        else if (task.HasAudio && preset.AudioMode == AudioMode.Copy)
        {
            // If copying, fallback estimate
            audioBitrateKbps = 128;
        }

        double videoBitrateKbps = totalBitrateKbps - audioBitrateKbps;

        // If calculated video bitrate is higher than preset's normal target, just use the normal target.
        // E.g. "Обычный целевой видеобитрейт: 1–2 Мбит/с. Приложение вычисляет прогнозный размер... Если прогноз превышает 18 МБ, видеобитрейт снижается."
        int finalBitrate = (int)Math.Max(videoBitrateKbps, 100); // minimum 100 kbps to avoid absolute garbage

        if (finalBitrate > preset.TargetVideoBitrateKbps)
        {
            finalBitrate = preset.TargetVideoBitrateKbps;
        }

        return finalBitrate;
    }
}
