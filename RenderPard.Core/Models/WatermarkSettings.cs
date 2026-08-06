namespace RenderPard.Core.Models;

public class WatermarkSettings
{
    public string Text { get; set; } = string.Empty;
    public string Font { get; set; } = "Arial"; // Will be resolved to a path if necessary
    public int FontSize { get; set; } = 36;
    public string Color { get; set; } = "#FFFFFF"; // Hex color
    public double Opacity { get; set; } = 1.0; // 0.0 to 1.0
    public Position9 Position { get; set; } = Position9.BottomRight;
    public int OffsetX { get; set; } = 20;
    public int OffsetY { get; set; } = 20;

    public WatermarkSettings Clone()
    {
        return (WatermarkSettings)MemberwiseClone();
    }
}
