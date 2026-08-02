using Robust.Shared.GameStates;
using Robust.Shared.Maths;

namespace Content.Shared._Pirate.ZLevels.Apertures.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEZLevelApertureComponent : Component
{
    [DataField, AutoNetworkedField]
    public int TargetDepth = -1;

    [DataField, AutoNetworkedField]
    public Vector2i PixelOffset = new(4, 18);

    [DataField, AutoNetworkedField]
    public Vector2i PixelSize = new(24, 14);

    [DataField, AutoNetworkedField]
    public int SpritePixelSize = 32;
}
