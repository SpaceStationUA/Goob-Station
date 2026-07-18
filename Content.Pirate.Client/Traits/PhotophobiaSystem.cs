// SPDX-FileCopyrightText: 2025 MarkerWicker <markerWicker@proton.me>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Client.Eye;
using Content.Pirate.Shared.Traits;
using Content.Shared.Flash.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Pirate.Client.Traits;

/// <summary>
/// Handles the photophobia overlay for the local player.
/// </summary>
public sealed class PhotophobiaSystem : SharedPhotophobiaSystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IOverlayManager _overlayManager = default!;

    private PhotophobiaOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PhotophobiaComponent, ComponentInit>(OnPhotophobiaInit);
        SubscribeLocalEvent<PhotophobiaComponent, ComponentShutdown>(OnPhotophobiaShutdown);
        SubscribeLocalEvent<PhotophobiaComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PhotophobiaComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<FlashImmunityComponent, GotEquippedEvent>(OnFlashProtectionEquipped);
        SubscribeLocalEvent<FlashImmunityComponent, GotUnequippedEvent>(OnFlashProtectionUnequipped);

        _overlay = new PhotophobiaOverlay();
    }

    private void OnPlayerAttached(Entity<PhotophobiaComponent> ent, ref LocalPlayerAttachedEvent args)
    {
        _overlayManager.AddOverlay(_overlay);
    }

    private void OnPlayerDetached(Entity<PhotophobiaComponent> ent, ref LocalPlayerDetachedEvent args)
    {
        _overlayManager.RemoveOverlay(_overlay);
    }

    private void OnPhotophobiaInit(Entity<PhotophobiaComponent> ent, ref ComponentInit args)
    {
        if (_player.LocalEntity == ent.Owner)
            _overlayManager.AddOverlay(_overlay);
    }

    private void OnPhotophobiaShutdown(Entity<PhotophobiaComponent> ent, ref ComponentShutdown args)
    {
        if (_player.LocalEntity == ent.Owner)
            _overlayManager.RemoveOverlay(_overlay);
    }

    private void OnFlashProtectionEquipped(Entity<FlashImmunityComponent> ent, ref GotEquippedEvent args)
    {
        if (_player.LocalEntity == args.Equipee && args.SlotFlags != SlotFlags.POCKET)
            _overlayManager.RemoveOverlay(_overlay);
    }

    private void OnFlashProtectionUnequipped(Entity<FlashImmunityComponent> ent, ref GotUnequippedEvent args)
    {
        if (_player.LocalEntity == args.Equipee && args.SlotFlags != SlotFlags.POCKET)
            _overlayManager.AddOverlay(_overlay);
    }
}
