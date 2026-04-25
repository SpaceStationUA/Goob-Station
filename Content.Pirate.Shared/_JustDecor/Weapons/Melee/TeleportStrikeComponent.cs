using Robust.Shared.Audio;

namespace Content.Pirate.Shared._JustDecor.Weapons.Melee;

[RegisterComponent]
public sealed partial class TeleportStrikeComponent : Component
{
    [DataField]
    public float MaxRange = 7f;

    [DataField]
    public float BehindOffset = 0.5f;

    [DataField]
    public float ReturnDelay = 0.25f;

    [DataField]
    public SoundSpecifier? TeleportSound;
}
