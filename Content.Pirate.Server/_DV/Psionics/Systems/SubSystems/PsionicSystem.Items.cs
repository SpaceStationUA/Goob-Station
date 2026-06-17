using Content.Server.Atmos.EntitySystems;
using Content.Shared._DV.Psionics.Components;
using Content.Shared._DV.Psionics.Events;
using Content.Shared.Atmos.Components;
using Content.Shared.Damage;
using Content.Shared.Inventory;
using Content.Shared.Popups;

namespace Content.Server._DV.Psionics.Systems;

public sealed partial class PsionicSystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;

    public void InitializeItems()
    {
        SubscribeLocalEvent<PsionicallyInsulativeComponent, InventoryRelayedEvent<NoosphericFryEvent>>(OnFry);
    }

    private void OnFry(Entity<PsionicallyInsulativeComponent> gear, ref InventoryRelayedEvent<NoosphericFryEvent> args)
    {
        if (gear.Comp.CanBeFried)
        {
            Popup.PopupEntity(Loc.GetString("psionic-burns-up", ("item", gear)), gear.Owner, PopupType.MediumCaution);
            Audio.PlayEntity(gear.Comp.FrySound, gear, gear);
            Spawn("Ash", Transform(gear).Coordinates);
            QueueDel(gear);
        }
        else
        {
            Popup.PopupEntity(Loc.GetString("psionic-burn-resist", ("item", gear)), gear.Owner, PopupType.MediumCaution);
            Audio.PlayEntity(gear.Comp.FrySound, gear, gear);
        }

        var target = args.Args.Target;
        if (target == EntityUid.Invalid)
            return;

        _damageable.TryChangeDamage(target, args.Args.Damage);

        if (!TryComp<FlammableComponent>(target, out var flammable))
            return;

        _flammable.AdjustFireStacks(target, args.Args.FireStacks, flammable);
        _flammable.Ignite(target, gear, flammable);
    }
}
