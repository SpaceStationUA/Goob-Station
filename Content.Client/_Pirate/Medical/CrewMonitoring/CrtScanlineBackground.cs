using Robust.Client.Graphics;
using Robust.Client.UserInterface;

// Pirate: CrewMonitor visual helper.
namespace Content.Client.Medical.CrewMonitoring;

/// <summary>
/// CRT-style alternating horizontal scanlines (one pixel light, one dark).
/// </summary>
public sealed class CrtScanlineBackground : Control
{
    public Color LightLine { get; set; } = Color.FromHex("#2E2E34");
    public Color DarkLine { get; set; } = Color.FromHex("#1A1A1E");

    public CrtScanlineBackground()
    {
        MouseFilter = MouseFilterMode.Ignore;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var width = PixelWidth;
        for (var y = 0; y < PixelHeight; y += 2)
        {
            handle.DrawRect(new UIBox2(0, y, width, y + 1), LightLine);
            if (y + 1 < PixelHeight)
                handle.DrawRect(new UIBox2(0, y + 1, width, y + 2), DarkLine);
        }
    }
}
