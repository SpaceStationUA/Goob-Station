using Content.Shared.Body.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Robust.Shared.Network;

namespace Content.Shared._Pirate.StarlightSpecies.Body;

/// <summary>Copies the source species' innate resistances to its initially spawned native limbs.</summary>
[RegisterComponent]
public sealed partial class StarlightBodyDamageModifiersComponent : Component;

public sealed class StarlightBodyDamageModifiersSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StarlightBodyDamageModifiersComponent, MapInitEvent>(OnMapInit,
            after: [typeof(SharedBodySystem)]);
    }

    private void OnMapInit(Entity<StarlightBodyDamageModifiersComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsClient || !TryComp<DamageableComponent>(ent, out var damageable))
            return;

        // Complex bodies apply resistance on the parts. Do not alter shared native part prototypes
        // or overwrite the innate resistance of subsequently transplanted limbs.
        _damage.SetDamageModifierSetId((ent.Owner, damageable), damageable.DamageModifierSetId);
    }
}
