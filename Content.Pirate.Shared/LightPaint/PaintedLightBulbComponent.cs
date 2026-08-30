using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.LightPaint;

/// <summary>
///     Applied to a light bulb that has been sprayed, so the original factory colour
///     can be restored when the paint is cleaned off.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PaintedLightBulbComponent : Component
{
    /// <summary>
    ///     The bulb's colour from before it was first painted.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color OriginalColor;
}
