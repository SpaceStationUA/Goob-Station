using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.LightPaint;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PaintedLightBulbComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color OriginalColor;
}
