// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Heretic.Lock;
using Content.Server.Hands.Systems;
using Content.Shared.Hands.Components;
using Content.Shared.Hands;

namespace Content.Pirate.Server.Heretic.Lock;

public sealed class ShatteredRisenSystem : EntitySystem
{
    [Dependency] private readonly HandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShatteredRisenComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ShatteredRisenComponent, HandCountChangedEvent>(OnHandCountChanged);
    }

    private void OnMapInit(Entity<ShatteredRisenComponent> ent, ref MapInitEvent args)
    {
        RefreshHands(ent);
    }

    private void OnHandCountChanged(Entity<ShatteredRisenComponent> ent, ref HandCountChangedEvent args)
    {
        if (!TerminatingOrDeleted(ent))
            RefreshHands(ent);
    }

    private void RefreshHands(Entity<ShatteredRisenComponent> ent)
    {
        if (!TryComp(ent, out HandsComponent? hands) || hands.Count == 0)
            return;

        var handsEnt = (ent.Owner, hands);
        var hasWeapon1 = false;

        foreach (var held in _hands.EnumerateHeld(handsEnt))
        {
            var prototype = Prototype(held)?.ID;
            if (prototype == ent.Comp.Weapon1.Id)
            {
                hasWeapon1 = true;
                continue;
            }

            if (prototype == ent.Comp.Weapon2.Id)
                continue;

            if (!_hands.TryDrop(handsEnt, held, null, false, false))
                QueueDel(held);
        }

        var coordinates = Transform(ent).Coordinates;
        foreach (var hand in _hands.EnumerateHands(handsEnt))
        {
            if (_hands.TryGetHeldItem(handsEnt, hand, out _))
                continue;

            var toSpawn = hasWeapon1 ? ent.Comp.Weapon2 : ent.Comp.Weapon1;
            hasWeapon1 = true;

            var weapon = Spawn(toSpawn, coordinates);
            if (!_hands.TryForcePickup(handsEnt, weapon, hand, false, false, hands))
                QueueDel(weapon);
        }
    }
}
