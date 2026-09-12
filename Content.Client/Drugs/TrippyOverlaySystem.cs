// SPDX-License-Identifier: MIT

using Content.Shared.Drugs;
using Content.Shared.StatusEffectNew;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Drugs;

/// <summary>
///     System to handle trippy hallucination overlays.
/// </summary>
public sealed class TrippyOverlaySystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IOverlayManager _overlayMan = default!;

    private TrippyOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TrippyStatusEffectComponent, StatusEffectAppliedEvent>(OnApplied);
        SubscribeLocalEvent<TrippyStatusEffectComponent, StatusEffectRemovedEvent>(OnRemoved);

        SubscribeLocalEvent<TrippyStatusEffectComponent, StatusEffectRelayedEvent<LocalPlayerAttachedEvent>>(OnPlayerAttached);
        SubscribeLocalEvent<TrippyStatusEffectComponent, StatusEffectRelayedEvent<LocalPlayerDetachedEvent>>(OnPlayerDetached);

        _overlay = new();
    }

    private void OnRemoved(Entity<TrippyStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        if (_player.LocalEntity != args.Target)
            return;

        _overlay.CurrentTripPower = 0;
        _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnApplied(Entity<TrippyStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        if (_player.LocalEntity != args.Target)
            return;

        _overlayMan.AddOverlay(_overlay);
    }

    private void OnPlayerAttached(Entity<TrippyStatusEffectComponent> ent, ref StatusEffectRelayedEvent<LocalPlayerAttachedEvent> args)
    {
        _overlayMan.AddOverlay(_overlay);
    }

    private void OnPlayerDetached(Entity<TrippyStatusEffectComponent> ent, ref StatusEffectRelayedEvent<LocalPlayerDetachedEvent> args)
    {
        _overlay.CurrentTripPower = 0;
        _overlayMan.RemoveOverlay(_overlay);
    }
}
