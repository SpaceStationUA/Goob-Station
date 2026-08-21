using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.BarbellBench.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BarbellBenchComponent : Component
{
    [DataField("overlayPrototype")]
    public string OverlayPrototype = "BarbellBenchOverlay";

    [DataField("barbellSlotId")]
    public string BarbellSlotId = "barbell-slot";

    [DataField("repSoundCollection")]
    public string RepSoundCollection = "BarbellBenchRep";

    [DataField("repSoundDelay")]
    public float RepSoundDelay = 1f;

    [DataField, AutoNetworkedField]
    public bool IsPerformingRep = false;

    [DataField]
    public float RepDuration = 3.0f;

    [DataField]
    public double PinDurationSeconds = 27.0;

    [AutoNetworkedField]
    public EntityUid? OverlayEntity;
}
