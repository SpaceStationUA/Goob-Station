// SPDX-FileCopyrightText: 2026 Pirate
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.ModularSuit;
using Robust.Client.Graphics;
using Robust.Shared.Player;

namespace Content.Pirate.Client.ModularSuit;

public sealed class MesonVisionSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;

    private MesonVisionOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MesonVisionComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<MesonVisionComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<MesonVisionComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<MesonVisionComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);

        _overlay = new MesonVisionOverlay();
    }

    private void OnComponentInit(Entity<MesonVisionComponent> ent, ref ComponentInit args)
    {
        if (ent.Owner == _player.LocalEntity)
            AddOverlay();
    }

    private void OnComponentShutdown(Entity<MesonVisionComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Owner == _player.LocalEntity)
            RemoveOverlay();
    }

    private void OnPlayerAttached(Entity<MesonVisionComponent> ent, ref LocalPlayerAttachedEvent args)
    {
        AddOverlay();
    }

    private void OnPlayerDetached(Entity<MesonVisionComponent> ent, ref LocalPlayerDetachedEvent args)
    {
        RemoveOverlay();
    }

    private void AddOverlay()
    {
        if (!_overlayManager.HasOverlay<MesonVisionOverlay>())
            _overlayManager.AddOverlay(_overlay);
    }

    private void RemoveOverlay()
    {
        _overlayManager.RemoveOverlay(_overlay);
    }
}
