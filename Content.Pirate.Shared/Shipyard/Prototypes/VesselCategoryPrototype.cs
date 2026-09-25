using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.Shipyard.Prototypes;

/// <summary>
/// A selectable category for shipyard vessels.
/// </summary>
[Prototype("vesselCategory")]
public sealed partial class VesselCategoryPrototype : IPrototype
{
    [ViewVariables, IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    private LocId Name { get; set; }

    [ViewVariables(VVAccess.ReadOnly)]
    public string LocalizedName => Loc.GetString(Name);
}
