using Robust.Shared.Audio;

namespace Content.Pirate.Shared.ModularSuit;

[RegisterComponent]
public sealed partial class ModularSuitGrabberModuleComponent : Component;

[RegisterComponent]
public sealed partial class ModularSuitGrabberToolComponent : SharedItemModuleComponent
{
    [DataField] public int MaxContents = 2;
    [DataField] public float GrabDelay = 3f;

    [DataField]
    public SoundSpecifier StartGrabSound = new SoundPathSpecifier("/Audio/Mecha/sound_mecha_hydraulic.ogg");

    public EntityUid? AudioStream;

    [DataField]
    public SoundSpecifier EjectSound = new SoundPathSpecifier("/Audio/Mecha/sound_mecha_hydraulic.ogg");
}
