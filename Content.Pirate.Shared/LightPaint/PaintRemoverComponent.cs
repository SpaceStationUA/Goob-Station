using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.LightPaint;

/// <summary>
///     Cleans sprayed paint off a light bulb, restoring its original colour.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PaintRemoverComponent : Component
{
    [DataField]
    public TimeSpan CleanDelay = TimeSpan.FromSeconds(2);
}
