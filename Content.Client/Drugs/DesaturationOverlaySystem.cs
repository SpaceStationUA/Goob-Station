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
///     System to handle desaturation (numb) overlays.
/// </summary>
public sealed class DesaturationOverlaySystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IOverlayManager _overlayMan = default!;

    private DesaturationOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DesaturationStatusEffectComponent, StatusEffectAppliedEvent>(OnApplied);
        SubscribeLocalEvent<DesaturationStatusEffectComponent, StatusEffectRemovedEvent>(OnRemoved);

        SubscribeLocalEvent<DesaturationStatusEffectComponent, StatusEffectRelayedEvent<LocalPlayerAttachedEvent>>(OnPlayerAttached);
        SubscribeLocalEvent<DesaturationStatusEffectComponent, StatusEffectRelayedEvent<LocalPlayerDetachedEvent>>(OnPlayerDetached);

        _overlay = new();
    }

    private void OnRemoved(Entity<DesaturationStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        if (_player.LocalEntity != args.Target)
            return;

        _overlay.CurrentGreyPower = 0;
        _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnApplied(Entity<DesaturationStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        if (_player.LocalEntity != args.Target)
            return;

        _overlayMan.AddOverlay(_overlay);
    }

    private void OnPlayerAttached(Entity<DesaturationStatusEffectComponent> ent, ref StatusEffectRelayedEvent<LocalPlayerAttachedEvent> args)
    {
        _overlayMan.AddOverlay(_overlay);
    }

    private void OnPlayerDetached(Entity<DesaturationStatusEffectComponent> ent, ref StatusEffectRelayedEvent<LocalPlayerDetachedEvent> args)
    {
        _overlay.CurrentGreyPower = 0;
        _overlayMan.RemoveOverlay(_overlay);
    }
}
