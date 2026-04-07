using Robust.Shared.Audio;

namespace Content.Pirate.Shared._JustDecor.MartialArts.Components;

[RegisterComponent]
public sealed partial class GrantComboHelperComponent : Component
{
    [DataField]
    public LocId LearnMessage = "combo-helper-success-learned";

    [DataField]
    public LocId AlreadyKnownMessage = "combo-helper-fail-already";

    [DataField]
    public bool MultiUse;

    [DataField]
    public string? SpawnedProto;

    [DataField]
    public SoundSpecifier? SoundOnUse =
        new SoundPathSpecifier("/Audio/_Goobstation/Items/handling/paper_use.ogg", AudioParams.Default.WithVolume(-2));
}
