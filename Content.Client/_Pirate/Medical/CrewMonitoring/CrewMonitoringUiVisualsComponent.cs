namespace Content.Client._Pirate.Medical.CrewMonitoring;

/// <summary>
/// Single-hue screen theme for the crew monitor UI inside the PDA bezel.
/// Relative brightness of panels/buttons is preserved; only the gamma/hue changes.
/// </summary>
[RegisterComponent]
public sealed partial class CrewMonitoringUiVisualsComponent : Component
{
    /// <summary>
    /// Source color for the whole inner UI palette (CRT, frames, chrome, map wash).
    /// </summary>
    [DataField]
    public Color ThemeColor = Color.FromHex("#6A7080");
}
