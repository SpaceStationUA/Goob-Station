using Robust.Shared.Prototypes;
using Content.Shared.Preferences.Loadouts;

namespace Content.Shared.Humanoid.Prototypes;

public sealed partial class SpeciesPrototype
{
    /// <summary>The parent species shown in the gallery for a selectable subspecies.</summary>
    [DataField]
    public ProtoId<SpeciesPrototype>? SubspeciesOf;

    [DataField]
    public LocId? SubspeciesName;

    [DataField]
    public bool HasSubspecies;

    [DataField]
    public ProtoId<RoleLoadoutPrototype>? Loadout;
}
