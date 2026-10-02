using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Pirate.Shared.Shipyard.Prototypes;

[Prototype("vessel")]
public sealed partial class VesselPrototype : IPrototype
{
    [ViewVariables, IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name = string.Empty;

    [DataField(required: true)]
    public LocId Description = string.Empty;
    [DataField(required: true)]
    public int Price;

    [DataField(required: true)]
    public ResPath Path = default!;

    [DataField]
    public int Delay = 60;

    [DataField]
    public List<ProtoId<VesselCategoryPrototype>> Categories = new();

    [DataField]
    public EntityWhitelist? Whitelist;
}
