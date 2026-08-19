# Design Specification: TGstick Preset for Telegram Video Stickers

## 1. Overview
This specification details the implementation of the `TGstick` built-in preset in RenderPard. The preset converts MOV videos (with or without alpha transparency, e.g., Apple ProRes 4444, Animation, RGBA) into `.webm` files that strictly adhere to Telegram's Video Sticker technical requirements.

---

## 2. Telegram Video Sticker Technical Requirements
According to official Telegram Video Sticker guidelines:
- **Container Format**: WebM (`.webm`)
- **Video Codec**: VP9 (`libvpx-vp9`)
- **Alpha / Transparency**: Supported via pixel format `yuva420p`
- **Resolution / Dimensions**:
  - One side **must be exactly 512 pixels**
  - The other side **must be 512 pixels or less** (e.g., 512×512, 512×288, 288×512)
  - Both dimensions must be even numbers
- **Frame Rate (FPS)**: Up to **30 FPS** maximum
- **Duration**: Maximum **3.0 seconds** (longer videos must be clipped to 3 seconds)
- **File Size**: Maximum **256 KB** (262,144 bytes)
- **Audio**: Prohibited — no audio track (`-an`)
- **Encoder flags**: `-auto-alt-ref 0` (disables alt-ref frames to ensure smooth looping and clean transparency in Telegram client renderer)

---

## 3. Architecture & Preset Configuration

### 3.1 Preset Model Additions (`RenderPard.Core.Models.Preset`)
To support general duration caps and FPS limits across presets (and specifically for `TGstick`):
- `MaxDurationSeconds` (double, default `0` = unlimited): When > 0, caps the encoding duration to at most this value (e.g., `3.0` for `TGstick`).
- `MaxFps` (int, default `0` = unlimited): When > 0, caps output frame rate if source exceeds it (e.g., `30` for `TGstick`).
- `ForceExactLongSide` (bool, default `false`): When `true` (or when `MaxLongSideSize == 512` for sticker presets), ensures the long side is scaled to exactly `MaxLongSideSize` even if the input is smaller than 512 px.

### 3.2 Preset Definition (`TGstick`)
In `PresetManager.GetDefaultPresets()`:
```csharp
new Preset
{
    Name = "TGstick",
    IsBuiltIn = true,
    SortOrder = 6,
    CustomIcon = "telegram",
    Container = ContainerFormat.WebM,
    VideoCodec = VideoCodec.Vp9,
    MaxLongSideSize = 512,
    TargetVideoBitrateKbps = 450,
    MaxDurationSeconds = 3.0,
    MaxFps = 30,
    AudioMode = AudioMode.None
}
```

### 3.3 FFmpeg Encoding Pipeline (`RenderPard.Core.FFmpegWrapper`)
1. **Duration Management**:
   - If `task.Preset.MaxDurationSeconds > 0`:
     - If user set custom trim points: duration = `min(task.EffectiveDurationSeconds, task.Preset.MaxDurationSeconds)`.
     - Otherwise: duration = `min(task.DurationSeconds, task.Preset.MaxDurationSeconds)`.
     - Appends `-t {duration}` to FFmpeg CLI arguments.
2. **Scaling & Aspect Ratio**:
   - When `MaxLongSideSize` is specified with exact dimension scaling (for 512px stickers):
     - Landscape / Square (`iw >= ih`): `scale=512:-2`
     - Portrait (`ih > iw`): `scale=-2:512`
3. **Framerate Limiting**:
   - When `task.Preset.MaxFps > 0` and `task.Fps > task.Preset.MaxFps` (or `task.Fps <= 0`):
     - Adds `fps={task.Preset.MaxFps}` (e.g. `fps=30`) to the video filter chain or `-r 30`.
4. **Transparency & Alpha Pixel Format**:
   - `format=yuva420p|yuv420p` ensures transparent source MOV (RGBA, ProRes 4444) encodes alpha into `yuva420p` VP9 stream.
5. **Quality & Size Optimization**:
   - VP9 encoder arguments: `-c:v libvpx-vp9 -crf 30 -b:v 450k -maxrate 550k -bufsize 500k -auto-alt-ref 0 -an`.
   - At 450 kbps for 3.0 seconds, file size is ~168 KB, guaranteeing it stays well below the 256 KB limit while retaining crisp visual fidelity.

---

## 4. UI & Localization Integration
- `Lang.ru-RU.xaml` and `Lang.en-US.xaml` updated if any new UI settings labels are needed.
- `PresetManager.MergeBuiltInPresets()` ensures existing users automatically receive the new `TGstick` preset without breaking custom presets.
- Windows Context Menu automatically includes `TGstick` with the Telegram icon for quick right-click conversions.

---

## 5. Verification Plan
- Unit test / manual test verifying:
  - Preset loaded with correct default values.
  - Command generation for 60 FPS input produces `fps=30` and `-t 3`.
  - Resolution scaling produces exact 512px on the longest side.
  - MOV with alpha channel converts to transparent WebM VP9 with no audio stream.
