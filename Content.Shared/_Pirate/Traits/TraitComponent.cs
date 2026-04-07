using Content.Shared.Traits;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Traits;

/// <summary>
///     Component added to players on spawn to track their active traits.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(SharedTraitSystem))]
public sealed partial class TraitComponent : Component
{
    /// <summary>
    ///     The list of traits that have been applied to this player.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<TraitPrototype>> ActiveTraits = new();
}

[Serializable, NetSerializable]
public sealed class TraitComponentState : ComponentState
{
    public HashSet<ProtoId<TraitPrototype>> ActiveTraits;

    public TraitComponentState(HashSet<ProtoId<TraitPrototype>> activeTraits)
    {
        ActiveTraits = activeTraits;
    }
}
