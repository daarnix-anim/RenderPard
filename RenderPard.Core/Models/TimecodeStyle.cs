namespace RenderPard.Core.Models;

public class TimecodeStyle
{
    public string Name { get; set; } = "Default Style";
    public AspectRatioCategory AspectRatioRange { get; set; } = AspectRatioCategory.Landscape;
    
    public string Font { get; set; } = "Consolas";
    public int FontSize { get; set; } = 48;
    public string Color { get; set; } = "#FFFFFF";
    public double Opacity { get; set; } = 0.5; // "малозаметный таймкод", "полупрозрачный"
    
    public Position9 Position { get; set; } = Position9.BottomCenter;
    public int OffsetX { get; set; } = 0;
    public int OffsetY { get; set; } = 40;

    public TimecodeStyle Clone()
    {
        return (TimecodeStyle)MemberwiseClone();
    }
}
