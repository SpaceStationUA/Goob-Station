// SPDX-License-Identifier: MIT
// Pirate: meson vision - ported from space-wizards/space-station-14#44601 ("Mesons (XRayVision)").

using Content.Shared._Pirate.Xray;
using Content.Shared.GameTicking;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;
using static Robust.Shared.GameObjects.SharedMapSystem;

namespace Content.Client._Pirate.Xray;

/// <inheritdoc/>
public sealed class XRayVisionSystem : SharedXRayVisionSystem
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private XRayVisionOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new XRayVisionOverlay();

        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<XRayVisionComponent, AfterAutoHandleStateEvent>(OnHandleState);
        SubscribeLocalEvent<TileChangedEvent>(OnTilesChanged);
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnPlayerAttached(LocalPlayerAttachedEvent args)
    {
        RefreshOverlay(args.Entity);
    }

    private void OnPlayerDetached(LocalPlayerDetachedEvent args)
    {
        Deactivate(_player.LocalEntity);
    }

    private void OnHandleState(Entity<XRayVisionComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        // State may arrive on the worn item, so refresh the attached player.
        RefreshOverlay(_player.LocalSession?.AttachedEntity ?? ent.Owner);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _overlay.InvalidateVisibility();
        var localPlayer = _player.LocalSession?.AttachedEntity;
        if (localPlayer != null)
            Deactivate(localPlayer.Value);
    }

    private void OnTilesChanged(ref TileChangedEvent args)
    {
        _overlay.InvalidateVisibility(args.Entity.Owner);
    }

    private void Update(EntityUid entity, List<Entity<XRayVisionComponent>> entities)
    {
        if (entity != _player.LocalSession?.AttachedEntity)
            return;

        XRayVisionComponent? xray = null;
        foreach (var ent in entities)
        {
            if (!ent.Comp.Enabled)
                continue;

            // Components being removed still relay events.
            if (TerminatingOrDeleted(ent.Owner))
                continue;

            if (ent.Comp.RelayOverlay == (ent.Owner == entity))
                continue;

            xray ??= ent.Comp;
        }

        if (xray == null)
        {
            Deactivate(entity);
            return;
        }

        _overlay.SetParameters(xray.ShowTiles, xray.Range, xray.TileAlpha);

        if (!_overlayMan.HasOverlay<XRayVisionOverlay>())
            _overlayMan.AddOverlay(_overlay);
    }

    private void Deactivate(EntityUid? ent)
    {
        if (ent != _player.LocalSession?.AttachedEntity)
            return;

        _overlayMan.RemoveOverlay(_overlay);
    }

    protected override void RefreshOverlay(EntityUid target)
    {
        if (target != _player.LocalSession?.AttachedEntity)
            return;

        var ev = new RefreshXRayVisionEvent();
        RaiseLocalEvent(target, ref ev);

        if (ev.Entities.Count > 0)
            Update(target, ev.Entities);
        else
            Deactivate(target);
    }
}
