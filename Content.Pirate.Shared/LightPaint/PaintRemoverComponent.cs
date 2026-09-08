using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.LightPaint;

[RegisterComponent, NetworkedComponent]
public sealed partial class PaintRemoverComponent : Component
{
    [DataField]
    public TimeSpan CleanDelay = TimeSpan.FromSeconds(2);
}
