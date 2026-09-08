// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Overlays;
using Content.Shared.GameTicking;
using Content.Shared.NightVision;
using Content.Shared.Overlays;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client.NightVision;

/// <inheritdoc/>
public sealed partial class MonolithNightVisionSystem : SharedMonolithNightVisionSystem
{
    [Dependency] private IOverlayManager _overlayMan = default!;
    [Dependency] private IPlayerManager _player = default!;

    private MonolithNightVisionOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new MonolithNightVisionOverlay();

        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<MonolithNightVisionComponent, AfterAutoHandleStateEvent>(OnHandleState);
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

    private void OnHandleState(Entity<MonolithNightVisionComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        var localPlayer = _player.LocalSession?.AttachedEntity;
        if (localPlayer != null)
            RefreshOverlay(localPlayer.Value);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        var localPlayer = _player.LocalSession?.AttachedEntity;
        if (localPlayer != null)
            Deactivate(localPlayer.Value);
    }

    /// <summary>
    /// Update the state of the overlay. Add/remove/modify based on <see cref="MonolithNightVisionComponent"/>s if any.
    /// </summary>
    /// <param name="entity">The entity to have an overlay added/removed from.</param>
    /// <param name="entities">A list of entities with a <see cref="MonolithNightVisionComponent"/>.</param>
    private void Update(EntityUid entity, List<Entity<MonolithNightVisionComponent>> entities)
    {
        if (entity != _player.LocalSession?.AttachedEntity)
            return;

        // Find the component with the lowest noise.
        MonolithNightVisionComponent? nvision = null;
        foreach (var ent in entities)
        {
            if (!ent.Comp.Enabled)
                continue;

            if (ent.Comp.RelayOverlay == (ent.Owner == entity))
                continue;

            nvision = ent.Comp;

            // Take the first priority component
            if (ent.Comp.Prioritized)
                break;
        }

        // There is no active night vision components, so we disable the overlay.
        if (nvision == null)
        {
            Deactivate(entity);
            return;
        }

        // Relay all the settings from the component.
        _overlay.SetParameters(
            nvision.LightingColor,
            nvision.PhosphorColor,
            nvision.GoggleEffect,
            nvision.ViewCircleRadius,
            nvision.ViewCircleSpacing,
            nvision.ViewCircleCount,
            nvision.Amplification);

        if (!_overlayMan.HasOverlay<MonolithNightVisionOverlay>())
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
        var ev = new RefreshMonolithNightVisionEvent();
        RaiseLocalEvent(target, ref ev);

        if (ev.Entities.Count > 0)
            Update(target, ev.Entities);
        else
            Deactivate(target);
    }
}
