using Content.Shared.Traits;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Traits;

/// <summary>
///     Shared base system for managing traits.
/// </summary>
public abstract class SharedTraitSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TraitComponent, ComponentGetState>(OnGetState);
        SubscribeLocalEvent<TraitComponent, ComponentHandleState>(OnHandleState);
    }

    private void OnGetState(EntityUid uid, TraitComponent component, ref ComponentGetState args)
    {
        args.State = new TraitComponentState(component.ActiveTraits);
    }

    private void OnHandleState(EntityUid uid, TraitComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not TraitComponentState state)
            return;

        component.ActiveTraits = new HashSet<ProtoId<TraitPrototype>>(state.ActiveTraits);
    }
}
