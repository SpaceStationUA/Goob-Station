using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.Alchemy.Components;

[RegisterComponent]
public sealed partial class AlchemistMimicJuiceComponent : Component
{
    [DataField]
    public EntProtoId ProjectorPrototype = "PirateMimicJuiceProjector";

    [DataField]
    public EntProtoId DisguisePrototype = "ChameleonDisguise";

    [DataField]
    public float SearchRadius = 6f;

    [DataField]
    public List<EntProtoId> FallbackPrototypes = new()
    {
        "Crowbar",
        "Wrench",
        "Screwdriver",
        "Wirecutter",
    };

    public EntityUid? ProjectorEntity;
    public EntityUid? DisguiseEntity;
}
