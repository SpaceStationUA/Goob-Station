// SPDX-FileCopyrightText: 2025 MarkerWicker <markerWicker@proton.me>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Item.ItemToggle.Components;
using Content.Shared.Flash;

namespace Content.Pirate.Shared.Traits;

/// <summary>
/// Flashes photophobic entities hit by an item toggle ray.
/// </summary>
public abstract class SharedPhotophobiaSystem : EntitySystem
{
    [Dependency] private readonly SharedFlashSystem _flash = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PhotophobiaComponent, ItemToggleRayHitEvent>(OnLightRayHit);
    }

    private void OnLightRayHit(Entity<PhotophobiaComponent> ent, ref ItemToggleRayHitEvent args)
    {
        _flash.Flash(
            ent.Owner,
            user: null,
            used: null,
            TimeSpan.FromSeconds(ent.Comp.FlashDuration),
            ent.Comp.FlashSlowdown);
    }
}
