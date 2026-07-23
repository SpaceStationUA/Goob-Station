// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Roudenn <romabond091@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client._Lavaland.Audio;
using Content.Pirate.Common.Cyberdeck.Components;
using Content.Pirate.Shared.Cyberdeck;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Pirate.Client.Cyberdeck;

public sealed class CyberdeckSystem : SharedCyberdeckSystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly BossMusicSystem _bossMusic = default!;

    private CyberdeckOverlay _overlay = default!;
    private EntityUid? _activeEntity;
    private string? _diveMusicId;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new CyberdeckOverlay();
        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<CyberdeckOverlayComponent, ComponentStartup>(OnOverlayStartup);
        SubscribeLocalEvent<CyberdeckOverlayComponent, ComponentShutdown>(OnOverlayShutdown);
    }

    private void OnPlayerAttached(LocalPlayerAttachedEvent args)
    {
        if (HasComp<CyberdeckOverlayComponent>(args.Entity))
            EnableEffects(args.Entity);
    }

    private void OnPlayerDetached(LocalPlayerDetachedEvent args)
    {
        if (_activeEntity == args.Entity)
            DisableEffects();
    }

    private void OnOverlayStartup(Entity<CyberdeckOverlayComponent> ent, ref ComponentStartup args)
    {
        if (_player.LocalEntity == ent.Owner)
            EnableEffects(ent.Owner);
    }

    private void OnOverlayShutdown(Entity<CyberdeckOverlayComponent> ent, ref ComponentShutdown args)
    {
        if (_activeEntity == ent.Owner)
            DisableEffects();
    }

    private void EnableEffects(EntityUid user)
    {
        if (_activeEntity == user)
            return;

        if (_activeEntity != null)
            DisableEffects();

        _activeEntity = user;
        _overlayManager.AddOverlay(_overlay);

        _diveMusicId = null;
        if (TryComp(user, out CyberdeckUserComponent? cyberdeck)
            && _bossMusic.TryStartBossMusic(cyberdeck.DiveMusicId))
            _diveMusicId = cyberdeck.DiveMusicId;
    }

    private void DisableEffects()
    {
        if (_activeEntity == null)
            return;

        _overlayManager.RemoveOverlay(_overlay);
        if (_diveMusicId is { } musicId)
            _bossMusic.TryEndBossMusic(musicId);

        _diveMusicId = null;
        _activeEntity = null;
    }
}
