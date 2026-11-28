// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using Content.Client.Weapons.Ranged.Systems;
using Content.Shared._Pirate.Arenas;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.GameStates;
using Robust.Shared.Containers;

namespace Content.Client._Pirate.Arenas;

public sealed class ArenaMagazineDisplaySystem : EntitySystem
{
    [Dependency] private readonly GunSystem _guns = default!;
    [Dependency] private readonly IClientGameStateManager _states = default!;

    private readonly HashSet<EntityUid> _pending = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<ArenaMagazineDisplayComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ArenaMagazineDisplayComponent, EntInsertedIntoContainerMessage>(OnSlotChanged);
        SubscribeLocalEvent<ArenaMagazineDisplayComponent, EntRemovedFromContainerMessage>(OnSlotChanged);
    }

    public override void Shutdown()
    {
        _states.GameStateApplied -= OnStateApplied;
        _pending.Clear();
        base.Shutdown();
    }

    private void OnStartup(EntityUid uid, ArenaMagazineDisplayComponent component, ComponentStartup args)
    {
        QueueRefresh(uid);
    }

    private void OnSlotChanged(EntityUid uid, ArenaMagazineDisplayComponent component, ContainerModifiedMessage args)
    {
        if (args.Container.ID == SharedGunSystem.MagazineSlot)
            QueueRefresh(uid);
    }

    private void QueueRefresh(EntityUid uid)
    {
        // A magazine can enter the gun before its own ammo count is applied in the same snapshot.
        if (_pending.Count == 0)
            _states.GameStateApplied += OnStateApplied;
        _pending.Add(uid);
    }

    private void OnStateApplied(GameStateAppliedArgs args)
    {
        _states.GameStateApplied -= OnStateApplied;
        foreach (var uid in _pending)
            _guns.UpdateAmmoCount(uid, prediction: false);
        _pending.Clear();
    }
}
