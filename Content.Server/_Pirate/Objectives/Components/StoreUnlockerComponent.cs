using Content.Server._Pirate.Objectives.Systems;
using Content.Server._Pirate.Store.Conditions;
using Content.Shared.Store;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.Objectives.Components;

/// <summary>
/// Unlocks store listings that use <see cref="ObjectiveUnlockCondition"/>.
/// </summary>
[RegisterComponent, Access(typeof(StoreUnlockerSystem))]
public sealed partial class StoreUnlockerComponent : Component
{
    [DataField(required: true)]
    public List<ProtoId<ListingPrototype>> Listings = new();
}
