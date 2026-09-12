using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Silicons.IPC;

/// <summary>
/// Gives the player a lo-fi CRT "screen vision" effect.
/// Ported from starcup PR #911 (MKC screen vision). Also optionally applies
/// a glitch effect while the player is heavily damaged or in critical condition
/// (based on the glitch effect shader by Yui Kinomoto @arlez80, MIT).
/// Both are disabled by the accessibility option "Disable vision filters"
/// (<c>accessibility.no_vision_filters</c>).
/// </summary>
[RegisterComponent]
[NetworkedComponent]
public sealed partial class ScreenVisionComponent : Component
{
    /// <summary>
    /// Whether the screen glitches when the owner is heavily damaged or in crit.
    /// </summary>
    [DataField]
    public bool HealthGlitch = true;
}
